using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentRuns;
using FindingReview;
using Npgsql;
using SyntheticFixReview;

internal static class SavedSourceCases
{
    private static SyntheticDurableRunEngine Engine(string? connection = null) => new(connection ?? DatabaseCases.Connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result)
    { Check.That(result.Succeeded && result.Snapshot is not null, "actual-durable-run-command-accepted"); return result.Snapshot!; }
    internal sealed class Capture
    {
        internal SyntheticRunSnapshot? Run;
        internal DemoReviewContext? Review;
        internal DemoRecommendationGuidanceDetail? Guidance;
        internal DemoFixPackageDetail? Package;
        internal int Calls;
        internal async Task<ArtifactReviewSourceResult> Read(Guid runId, CancellationToken ct)
        {
            Calls++;
            var saved = await Engine().ReadAsync(DemoFixtureCatalog.Scope, runId, ct);
            if (!saved.Succeeded || saved.Snapshot is null) return new(ArtifactReviewIssue.SourceUnavailable, null);
            Run = saved.Snapshot;
            var reviewService = new DemoReviewService(new SyntheticReviewStore(DatabaseCases.Connection, SyntheticReviewScope.Fixed));
            Review = await reviewService.ReadExistingAsync(Run, ct);
            var states = Review.Snapshot?.Findings.ToDictionary(f => f.Seed.FindingId, f => Enum.Parse<AssessmentScoring.ScoringFindingState>(f.Current.State.ToString()), StringComparer.Ordinal);
            Guidance = DemoRecommendationGuidanceProjection.Detail(Run, SyntheticDemoAnalysisAdapter.Project(Run, states, Review.Snapshot?.SnapshotDigest), Review);
            Package = DemoFixPackageProjection.Detail(Run, Guidance, Review);
            return ArtifactReviewSourceBuilder.Build(Package?.Snapshot);
        }
        internal SyntheticFixReviewStore Store(ISyntheticFixReviewCommitObserver? observer = null) => new(DatabaseCases.Connection, ArtifactReviewScope.Fixed, Read, observer);
    }
    internal static async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
    {
        var engine = Engine();
        var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"));
        var run = Accept(await engine.StartAsync(request));
        var replay = await engine.StartAsync(request); Check.That(replay.AlreadyApplied && replay.Snapshot!.RunId == run.RunId, "saved-run-start-idempotency-retained");
        var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, run.RunId, "v13-independent-verifier"));
        var results = DemoFixtureCatalog.Baselines.Single(b => b.Id == baseline).ScriptedResults;
        var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, run.RunId, leased.Lease!.Generation, leased.Revision, results));
        return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, run.RunId, leased.Lease.Generation, checkpoint.Revision));
    }
    private static async Task Seed(SyntheticRunSnapshot run)
    {
        var reviews = new DemoReviewService(new SyntheticReviewStore(DatabaseCases.Connection, SyntheticReviewScope.Fixed));
        var seeded = await reviews.ReadAsync(run); Check.That(seeded.Snapshot is not null, "initial-finding-seed-outside-artifact-fence");
    }
    private static void Verify(Capture capture, ArtifactReviewReadResult read)
    {
        Check.That(read.Succeeded && read.Snapshot is not null, "actual-saved-source-artifact-read-ready");
        Check.That(capture.Run is not null && capture.Review?.Snapshot is not null && capture.Guidance?.Snapshot is not null && capture.Package?.Snapshot is not null, "one-actual-captured-source-chain-present");
        var run = capture.Run!; var review = capture.Review!.Snapshot!; var original = capture.Package!.Snapshot!;
        var expected = Expected.FixPayload(JsonSerializer.SerializeToNode(capture.Guidance!.Snapshot, V13Program.Web)!.AsObject());
        Check.Equal(Expected.Canonical(expected), original.CanonicalJson, "actual-saved-input-independent-original-package-recipe");
        Check.Equal(Expected.Canonical(Expected.Binding(expected)), Expected.Canonical(JsonSerializer.SerializeToNode(read.Snapshot!.Source, V13Program.Web)), "actual-saved-full-source-binding-independent-recipe");
        Check.Equal(Expected.Canonical(Expected.Artifacts(expected)), Expected.Canonical(JsonSerializer.SerializeToNode(read.Snapshot.Entries.Select(e => e.Artifact), V13Program.Web)), "actual-all-saved-artifact-bindings-independent-recipe");
        Check.Equal(read.Snapshot.Source.RunInputDigest, Expected.InputDigest(JsonSerializer.Serialize(run.Plan), JsonSerializer.Serialize(run.FrozenInputs), run.BaselineCatalogId, run.ProfileCatalogId), "complete-saved-input-digest-independent-framing");
        Check.Equal(read.Snapshot.Source.FindingReviewDigest, review.SnapshotDigest, "one-captured-current-review-digest");
        Check.Equal(read.Snapshot.Source.RunRevision, run.Revision, "one-captured-durable-run-revision");
        Check.That(read.Snapshot.Source.FindingRevisions.SequenceEqual(review.Findings.OrderBy(f => f.Seed.FindingId, StringComparer.Ordinal).Select(f => new ArtifactReviewFindingRevision(f.Seed.FindingId, f.Current.Revision))), "actual-full-finding-revision-vector");
        Check.Equal(original.Status, "Unverified", "actual-generated-package-always-unverified");
        Observe(capture, read);
    }
    private static void Observe(Capture capture, ArtifactReviewReadResult read)
    {
        // Observed values are reproduction inputs/evidence, never expected fixtures.
        var value = new JsonObject { ["schema"] = "v13-observed-synthetic-capture-v1", ["savedRun"] = JsonSerializer.SerializeToNode(capture.Run, V13Program.Web), ["capturedReview"] = JsonSerializer.SerializeToNode(capture.Review, V13Program.Web), ["capturedGuidance"] = JsonSerializer.SerializeToNode(capture.Guidance, V13Program.Web), ["originalPackage"] = JsonSerializer.SerializeToNode(capture.Package, V13Program.Web), ["returnedReview"] = JsonSerializer.SerializeToNode(read, V13Program.Web) };
        var name = "v13-observed-" + capture.Run!.RunId.ToString("D") + "-" + Guid.NewGuid().ToString("D") + ".json";
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, name), value.ToJsonString(V13Program.Web));
    }
    internal static async Task Run()
    {
        var engine = Engine(); await engine.InitializeAsync(); var findingStore = new SyntheticReviewStore(DatabaseCases.Connection, SyntheticReviewScope.Fixed); await findingStore.InitializeAsync();
        SyntheticRunSnapshot? selected = null;
        foreach (var preset in Expected.Presets)
        {
            var run = await Complete(preset, Expected.Profile); await Seed(run); var capture = new Capture(); var store = capture.Store();
            var before = await DatabaseCases.Counts(run.RunId); var read = await store.ReadAsync(run.RunId, PolicyCases.Consultant); Verify(capture, read);
            Check.Equal(capture.Calls, 1, "source-callback-exactly-once-under-fence");
            var canonical = Expected.Canonical(JsonSerializer.SerializeToNode(read, V13Program.Web));
            for (var i = 0; i < 2; i++)
            {
                var repeatedCapture = new Capture(); var repeated = await repeatedCapture.Store().ReadAsync(run.RunId, PolicyCases.Consultant);
                Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(repeated, V13Program.Web)), canonical, "new-instance-reconnect-repeat-detached-source-identical");
                Check.Equal(repeatedCapture.Calls, 1, "no-second-review-capture-on-repeat");
            }
            Check.Equal(await DatabaseCases.Counts(run.RunId), before, "all-four-preset-reads-write-free");
            if (preset == "synthetic-analysis-healthy-v1") Check.That(read.Snapshot!.Entries.IsEmpty, "saved-healthy-empty-no-actions");
            if (preset == "synthetic-analysis-findings-v1") selected = run;
        }
        var current = selected!; var initialCapture = new Capture(); var initial = await initialCapture.Store().ReadAsync(current.RunId, PolicyCases.Consultant); Verify(initialCapture, initial);
        var target = initial.Snapshot!.Entries[0].Artifact.ArtifactId; var first = DatabaseCases.Command(initial.Snapshot.Source.SourceDigest, 0);
        var receipts = await initialCapture.Store().ApplyAsync(current.RunId, target, PolicyCases.Consultant, first); Check.That(receipts.Succeeded, "actual-saved-source-initial-artifact-review");
        var originalArtifacts = Expected.Canonical(JsonSerializer.SerializeToNode(initial.Snapshot.Entries.Select(e => e.Artifact), V13Program.Web));
        var oldRunRevision = current.Revision;
        var reviews = new DemoReviewService(findingStore);
        foreach (var kind in new[] { SyntheticReviewEventKind.Comment, SyntheticReviewEventKind.EditPresentation, SyntheticReviewEventKind.Confirm })
        {
            var priorCapture = new Capture(); var prior = await priorCapture.Store().ReadAsync(current.RunId, PolicyCases.Consultant);
            var finding = priorCapture.Review!.Snapshot!.Findings.First(f => f.Current.State == SyntheticFindingState.Proposed);
            var eventCommand = new SyntheticReviewCommand(Guid.NewGuid(), finding.Current.Revision, kind, kind == SyntheticReviewEventKind.Confirm ? "Fictional confirmation" : null, kind == SyntheticReviewEventKind.Comment ? "Fictional source note café 中文 😀\0NUL\r\nCRLF" : null, kind == SyntheticReviewEventKind.EditPresentation ? "Changed fictional current title" : null, kind == SyntheticReviewEventKind.EditPresentation ? "Changed fictional business context" : null);
            Check.That((await reviews.ApplyAsync(current.RunId, finding.Seed.FindingId, eventCommand)).Succeeded, "actual-finding-action-accepted-without-artifact-event");
            var capture = new Capture(); var read = await capture.Store().ReadAsync(current.RunId, PolicyCases.Consultant); Verify(capture, read);
            Check.Equal(capture.Run!.Revision, oldRunRevision, "finding-action-run-revision-unchanged");
            Check.That(read.Snapshot!.Source.SourceDigest != prior.Snapshot!.Source.SourceDigest, "same-run-revision-full-source-changes");
            DatabaseCases.State(read, target, "NeedsReview", 1);
            Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(read.Snapshot.Entries.Select(e => e.Artifact), V13Program.Web)), originalArtifacts, "all-fixed-artifact-ids-kind-text-digests-unchanged");
            Check.Equal(read.Snapshot.Entries.Sum(e => e.History.Length), 1, "finding-actions-never-create-artifact-events");
        }
        await SourceWriterConcurrency(current, target);
        // Nine historical profiles are actual saved sources; new routes never promote them.
        var historical = JsonNode.Parse(File.ReadAllText(PortableCases.Fixture("historical-historical-six-locks.json")))!["entries"]!.AsArray()
            .Select(e => (Baseline: e!["baselineId"]!.GetValue<string>(), Profile: e["profileId"]!.GetValue<string>()))
            .Concat(new[] { ("baseline-ai-configuration-v1", "profile-ai-preview-v1"), ("baseline-ai-configuration-v1", "profile-ai-preview-empty-v1"), ("synthetic-analysis-findings-v1", "synthetic-review-maturity-fix-packages-equal-v1") }).ToArray();
        Check.Equal(historical.Length, 9, "independent-exact-historical-nine-inventory");
        foreach (var item in historical)
        {
            var baseline = item.Item1; var profile = item.Item2;
            var known = DemoFixtureCatalog.Profiles.Single(p => p.Id == profile);
            var run = await Complete(baseline, profile);
            Check.That(run.FrozenInputs.FixReviewContractDigest is null, "all-historical-nine-lock-absent-on-real-saved-run");
            var capture = new Capture(); var read = await capture.Store().ReadAsync(run.RunId, PolicyCases.Consultant);
            Check.That(!read.Succeeded && read.Snapshot is null, "historical-nine-artifact-operation-source-denied");
            Check.Equal(await DatabaseCases.Counts(run.RunId), "0|0|0|0|0", "historical-nine-no-artifact-write-effects");
            Check.That(!JsonSerializer.Serialize(known.Versions).Contains("FixReviewContractDigest", StringComparison.Ordinal), "historical-new-lock-omission-unchanged");
        }
    }
    private sealed class Hold : ISyntheticFixReviewCommitObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid run, string artifact, Guid @event, CancellationToken ct)
        { Entered.TrySetResult(); await Released.Task.WaitAsync(ct); }
    }
    private static async Task SourceWriterConcurrency(SyntheticRunSnapshot run, string target)
    {
        var capture = new Capture(); var before = await capture.Store().ReadAsync(run.RunId, PolicyCases.Consultant);
        var hold = new Hold(); var reviewCommand = DatabaseCases.Command(before.Snapshot!.Source.SourceDigest, 1);
        var review = capture.Store(hold).ApplyAsync(run.RunId, target, PolicyCases.Consultant, reviewCommand);
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var finding = capture.Review!.Snapshot!.Findings[0];
            var ownerConnection = new NpgsqlConnectionStringBuilder(DatabaseCases.Connection) { ApplicationName = "v13-owned-source-writer" }.ConnectionString;
            var writer = new DemoReviewService(new SyntheticReviewStore(ownerConnection, SyntheticReviewScope.Fixed));
            var update = writer.ApplyAsync(run.RunId, finding.Seed.FindingId, new(Guid.NewGuid(), finding.Current.Revision, SyntheticReviewEventKind.Comment, null, "Fictional concurrent source change", null, null));
            var assessmentConnection = new NpgsqlConnectionStringBuilder(DatabaseCases.Connection) { ApplicationName = "v13-owned-assessment-writer" }.ConnectionString;
            var assessment = Engine(assessmentConnection).RequestCancelAsync(DemoFixtureCatalog.Scope, run.RunId, run.Revision);
            var seedConnection = new NpgsqlConnectionStringBuilder(DatabaseCases.Connection) { ApplicationName = "v13-owned-seed-writer" }.ConnectionString;
            var seed = new DemoReviewService(new SyntheticReviewStore(seedConnection, SyntheticReviewScope.Fixed)).ReadAsync(run);
            var waitSeen = false;
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    waitSeen = (await DatabaseCases.Scalar<long>("SELECT count(*) FROM pg_stat_activity WHERE datname=@db AND application_name IN ('v13-owned-source-writer','v13-owned-assessment-writer','v13-owned-seed-writer') AND wait_event='advisory'", ("db", DatabaseCases.DatabaseName))) == 3;
                    if (waitSeen) break;
                    await Task.Delay(20);
                }
                Check.That(waitSeen && !update.IsCompleted && !assessment.IsCompleted && !seed.IsCompleted, "actual-finding-assessment-and-seed-writers-join-same-run-advisory-fence");
            }
            finally { hold.Released.TrySetResult(); }
            Check.That((await review).Succeeded, "review-A-commits-before-fenced-source-update-B");
            Check.That((await update).Succeeded, "waiting-source-writer-commits-after-review");
            Check.Equal((await assessment).Issue, SyntheticRunIssue.InvalidState, "fenced-assessment-command-retains-original-scoring-contract");
            Check.That((await seed).Snapshot is not null, "fenced-existing-finding-seed-original-identity");
            var finalCapture = new Capture(); var final = await finalCapture.Store().ReadAsync(run.RunId, PolicyCases.Consultant);
            DatabaseCases.State(final, target, "NeedsReview", 2);
            Check.Equal((await finalCapture.Store().ApplyAsync(run.RunId, target, PolicyCases.Consultant, DatabaseCases.Command(before.Snapshot.Source.SourceDigest, 2))).Issue, ArtifactReviewIssue.SourceConflict, "source-B-first-obsolete-A-new-attestation-denied");
        }
        finally { hold.Released.TrySetResult(); await review; }
    }
}
