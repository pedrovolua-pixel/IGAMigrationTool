using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentRuns;
using FindingReview;
using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class SavedSourceCases
{
    internal static SyntheticDurableRunEngine Engine(string? connection = null) => new(connection ?? OwnedDatabase.Connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result)
    { Check.That(result.Succeeded && result.Snapshot is not null, "actual-durable-run-command"); return result.Snapshot!; }
    internal static async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
    {
        var engine = Engine(); var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"));
        var run = Accept(await engine.StartAsync(request)); var lease = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, run.RunId, "v14-independent-verifier"));
        var results = DemoFixtureCatalog.Baselines.Single(b => b.Id == baseline).ScriptedResults;
        var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, run.RunId, lease.Lease!.Generation, lease.Revision, results));
        return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, run.RunId, lease.Lease.Generation, checkpoint.Revision));
    }
    private static DemoReviewService Reviews(string? connection = null) => new(new SyntheticReviewStore(connection ?? OwnedDatabase.Connection, SyntheticReviewScope.Fixed));
    internal sealed class Capture
    {
        internal DemoAnalysisCapture? Value;
        internal int Calls;
        internal async Task<PlanningTaskSourceResult> Read(NpgsqlConnection db, NpgsqlTransaction tx, Guid run, PlanningTaskAuthority authority, CancellationToken ct)
        {
            Calls++;
            Check.That(tx.Connection == db && db.State == System.Data.ConnectionState.Open, "real-saved-source-single-caller-transaction");
            var saved = await Engine().ReadInTransactionAsync(db, tx, DemoFixtureCatalog.Scope, run, ct);
            if (!saved.Succeeded || !DemoPlanningTaskCatalog.MatchesFrozenFixture(saved.Snapshot)) return new(PlanningTaskIssue.SourceUnavailable, null);
            var review = await Reviews().ReadExistingInTransactionAsync(db, tx, saved.Snapshot!, ct);
            Value = DemoAnalysisProjection.Capture(saved.Snapshot!, review);
            var package = Value.FixPackages?.Snapshot;
            var source = ArtifactReviewSourceBuilder.Build(package);
            if (!source.Succeeded) return new(PlanningTaskIssue.SourceUnavailable, null);
            var artifacts = await ArtifactStore().ReadInTransactionAsync(db, tx, run, Policies.ArtifactConsultant with { ActorId = authority.ActorId }, source.Source!, ct);
            Check.That(tx.Connection == db && db.State == System.Data.ConnectionState.Open, "all-owning-source-reads-retain-original-open-transaction");
            return PlanningTaskSourceBuilder.Build(package, artifacts.Snapshot);
        }
        internal SyntheticPlanningTaskStore Store(ISyntheticPlanningTaskCommitObserver? observer = null) => new(OwnedDatabase.Connection, PlanningTaskScope.Fixed, Read, observer);
    }
    internal static SyntheticFixReviewStore ArtifactStore() => new(OwnedDatabase.Connection, ArtifactReviewScope.Fixed, async (run, ct) =>
    {
        var saved = await Engine().ReadAsync(DemoFixtureCatalog.Scope, run, ct);
        if (!saved.Succeeded || !DemoPlanningTaskCatalog.MatchesFrozenFixture(saved.Snapshot)) return new(ArtifactReviewIssue.SourceUnavailable, null);
        var review = await Reviews().ReadExistingAsync(saved.Snapshot!, ct);
        return ArtifactReviewSourceBuilder.Build(DemoAnalysisProjection.Capture(saved.Snapshot!, review).FixPackages?.Snapshot);
    });
    private static void Verify(Capture capture, PlanningTaskSnapshot snapshot)
    {
        Check.Equal(capture.Calls, 1, "one-real-saved-run-review-package-attestation-capture");
        Check.That(capture.Value?.Review?.Snapshot is not null && capture.Value.FixPackages?.Snapshot is not null, "complete-real-saved-captured-review-and-original-package");
        var value = capture.Value!; var package = value.FixPackages!.Snapshot!;
        var original = JsonSerializer.SerializeToNode(package, V14Program.Web)!.AsObject(); original.Remove("canonicalJson"); original.Remove("contentDigest");
        Check.Equal(Expected.Canonical(Expected.FixPayload(original["guidance"]!.AsObject())), package.CanonicalJson, "actual-saved-full-original-guidance-package-independent-recipe");
        Check.Equal(Expected.Canonical(Expected.TaskBinding(original)), Expected.Canonical(JsonSerializer.SerializeToNode(snapshot.Source, V14Program.Web)), "actual-saved-full-task-and-artifact-binding-independent-recipe");
        Check.Equal(snapshot.Source!.ArtifactSource.RunInputDigest, Expected.InputDigest(JsonSerializer.Serialize(value.Run.Plan), JsonSerializer.Serialize(value.Run.FrozenInputs), value.Run.BaselineCatalogId, value.Run.ProfileCatalogId), "actual-saved-complete-input-lock-independent-framing");
        Check.Equal(snapshot.Source.ArtifactSource.FindingReviewDigest, value.Review!.Snapshot!.SnapshotDigest, "same-captured-current-finding-digest");
        Check.Equal(snapshot.Source.ArtifactSource.RunRevision, value.Run.Revision, "same-captured-durable-run-revision");
        foreach (var option in snapshot.Options)
            Check.Equal(option.Identity.TaskId, Expected.TaskId(Expected.Binding(original), option.Identity.FindingId, option.Identity.ScopedOptionId), "actual-saved-every-stable-task-identity-independent-recipe");
        // Captured actual outputs are reproduction evidence, never expected fixtures.
        var folder = Path.Combine(AppContext.BaseDirectory, "observed"); Directory.CreateDirectory(folder);
        var observed = new JsonObject
        {
            ["schema"] = "v14-observed-synthetic-saved-source-v1",
            ["run"] = JsonSerializer.SerializeToNode(value.Run, V14Program.Web),
            ["review"] = JsonSerializer.SerializeToNode(value.Review, V14Program.Web),
            ["package"] = JsonSerializer.SerializeToNode(value.FixPackages, V14Program.Web),
            ["returnedTasks"] = JsonSerializer.SerializeToNode(snapshot, V14Program.Web)
        };
        File.WriteAllText(Path.Combine(folder, Guid.NewGuid().ToString("D") + ".json"), observed.ToJsonString(V14Program.Web));
    }
    internal static async Task Run()
    {
        await Engine().InitializeAsync(); await new SyntheticReviewStore(OwnedDatabase.Connection, SyntheticReviewScope.Fixed).InitializeAsync();
        await ArtifactStore().InitializeAsync(); var initialCapture = new Capture(); await initialCapture.Store().InitializeAsync();
        SyntheticRunSnapshot? selected = null;
        foreach (var preset in Expected.Presets)
        {
            var run = await Complete(preset, Expected.Profile); var seed = await Reviews().ReadAsync(run);
            Check.That(seed.Snapshot is not null, "actual-finding-seed-outside-task-fence-only");
            var capture = new Capture(); var before = await OwnedDatabase.FullRows(run.RunId); var read = await capture.Store().ReadAsync(run.RunId, Policies.Consultant);
            Check.That(read.Succeeded, "all-four-real-saved-preset-task-reads-ready"); Verify(capture, read.Snapshot!);
            Check.Equal(await OwnedDatabase.FullRows(run.RunId), before, "all-four-real-saved-source-reads-never-register-task-proof");
            var nextCapture = new Capture(); var next = await nextCapture.Store().ReadAsync(run.RunId, Policies.Consultant);
            Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(next.Snapshot, V14Program.Web)), Expected.Canonical(JsonSerializer.SerializeToNode(read.Snapshot, V14Program.Web)), "fresh-instance-repeat-saved-source-byte-identical");
            Check.Equal(nextCapture.Calls, 1, "repeat-no-second-capture");
            if (preset == "synthetic-analysis-healthy-v1") Check.That(read.Snapshot!.Options.IsEmpty && read.Snapshot.Entries.IsEmpty, "actual-saved-healthy-empty-task-slice");
            if (preset == "synthetic-analysis-findings-v1") selected = run;
        }
        await FindingRefreshAndFence(selected!);
        await HistoricalTen();
        Check.Group("TC14-T01/T03/T05/T08 actual durable saved sources/current review and ten historical profiles");
    }
    private static async Task FindingRefreshAndFence(SyntheticRunSnapshot run)
    {
        var capture = new Capture(); var initial = await capture.Store().ReadAsync(run.RunId, Policies.Consultant); Verify(capture, initial.Snapshot!);
        var target = initial.Snapshot!.Options[0];
        foreach (var id in target.Identity.ArtifactIds)
        {
            var artifacts = await ArtifactStore().ReadAsync(run.RunId, Policies.ArtifactConsultant); var entry = artifacts.Snapshot!.Entries.Single(e => e.Artifact.ArtifactId == id);
            Check.That((await ArtifactStore().ApplyAsync(run.RunId, id, Policies.ArtifactConsultant, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, entry.Revision, artifacts.Snapshot.Source.SourceDigest, "Fictional saved current artifact attestation"))).Succeeded, "actual-saved-selected-three-attestations");
        }
        capture = new Capture(); var ready = await capture.Store().ReadAsync(run.RunId, Policies.Consultant); target = DatabaseCases.Option(ready.Snapshot!, target.Identity.TaskId);
        var creation = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Create, 0, ready.Snapshot!.Source!.ArtifactSource.SourceDigest, target.CurrentAttestations, "Fictional saved-source explicit conversion");
        Check.That((await capture.Store().ApplyAsync(run.RunId, target.Identity.TaskId, Policies.Consultant, creation)).Succeeded, "actual-saved-source-task-creation");
        var oldRunRevision = run.Revision; var oldSource = ready.Snapshot.Source.ArtifactSource.SourceDigest;
        var reviewed = capture.Value!.Review!.Snapshot!.Findings.Single(f => f.Seed.FindingId == target.Identity.FindingId);
        Check.That((await Reviews().ApplyAsync(run.RunId, reviewed.Seed.FindingId, new(Guid.NewGuid(), reviewed.Current.Revision, SyntheticReviewEventKind.Comment, null, "Fictional real finding source update café 中文 😀\0NUL\r\nCRLF", null, null))).Succeeded, "actual-finding-comment-updates-source-without-task-command");
        var freshCapture = new Capture(); var fresh = await freshCapture.Store().ReadAsync(run.RunId, Policies.Consultant); Verify(freshCapture, fresh.Snapshot!);
        Check.Equal(freshCapture.Value!.Run.Revision, oldRunRevision, "real-finding-source-change-run-revision-unchanged");
        Check.That(fresh.Snapshot!.Source!.ArtifactSource.SourceDigest != oldSource, "complete-package-source-changes-at-same-run-revision");
        Check.Equal(DatabaseCases.Entry(fresh.Snapshot, target.Identity.TaskId).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "real-finding-action-invalidates-plan-independently-of-workflow");
        var beforeReplay = await OwnedDatabase.FullRows(run.RunId);
        var replay = await freshCapture.Store().ApplyAsync(run.RunId, target.Identity.TaskId, Policies.Consultant, creation);
        Check.That(replay.AlreadyApplied, "actual-saved-original-replay-precedes-new-source-precondition");
        Check.Equal(await OwnedDatabase.FullRows(run.RunId), beforeReplay, "actual-saved-replay-does-not-rebind-stale-plan");
        await SourceWriterFence(run, target.Identity.TaskId);
    }

    private sealed class Hold : ISyntheticPlanningTaskCommitObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid run, string task, Guid id, CancellationToken ct)
        { Entered.TrySetResult(); await Released.Task.WaitAsync(ct); }
    }
    private static async Task SourceWriterFence(SyntheticRunSnapshot run, string task)
    {
        var capture = new Capture(); var read = await capture.Store().ReadAsync(run.RunId, Policies.Consultant);
        var option = DatabaseCases.Option(read.Snapshot!, task); var entry = DatabaseCases.Entry(read.Snapshot!, task);
        var command = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Comment, entry.Revision, read.Snapshot!.Source!.ArtifactSource.SourceDigest, option.CurrentAttestations, "Fictional fenced saved-source comment");
        var hold = new Hold(); var pending = capture.Store(hold).ApplyAsync(run.RunId, task, Policies.Consultant, command);
        Task<SyntheticReviewApplyResult>? finding = null; Task<SyntheticRunCommandResult>? assessment = null; Task<DemoReviewContext>? seed = null;
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var originalFinding = capture.Value!.Review!.Snapshot!.Findings[0];
            string Tagged(string name) => new NpgsqlConnectionStringBuilder(OwnedDatabase.Connection) { ApplicationName = name }.ConnectionString;
            finding = Reviews(Tagged("v14-owned-finding-writer")).ApplyAsync(run.RunId, originalFinding.Seed.FindingId,
                new(Guid.NewGuid(), originalFinding.Current.Revision, SyntheticReviewEventKind.Comment, null, "Fictional later source writer", null, null));
            assessment = Engine(Tagged("v14-owned-assessment-writer")).RequestCancelAsync(DemoFixtureCatalog.Scope, run.RunId, run.Revision);
            seed = Reviews(Tagged("v14-owned-seed-writer")).ReadAsync(run);
            var deadline = DateTime.UtcNow.AddSeconds(15); var waiting = false;
            while (DateTime.UtcNow < deadline)
            {
                waiting = await OwnedDatabase.Scalar<long>("SELECT count(*) FROM pg_stat_activity WHERE datname=@db AND application_name IN ('v14-owned-finding-writer','v14-owned-assessment-writer','v14-owned-seed-writer') AND wait_event='advisory'", ("db", OwnedDatabase.DomainName)) == 3;
                if (waiting) break; await Task.Delay(20);
            }
            Check.That(waiting && !finding.IsCompleted && !assessment.IsCompleted && !seed.IsCompleted, "actual-finding-assessment-seed-writers-block-on-task-single-run-fence");
        }
        finally
        {
            hold.Released.TrySetResult();
            try { await pending; }
            finally { try { if (finding is not null) await finding; } finally { try { if (assessment is not null) await assessment; } finally { if (seed is not null) await seed; } } }
        }
        Check.That((await pending).Succeeded && (await finding!).Succeeded && (await seed!).Snapshot is not null, "task-commit-precedes-real-finding-update-and-idempotent-seed");
        Check.Equal((await assessment!).Issue, SyntheticRunIssue.InvalidState, "real-assessment-writer-fenced-original-scoring-contract-preserved");
        var freshCapture = new Capture(); var fresh = await freshCapture.Store().ReadAsync(run.RunId, Policies.Consultant);
        Check.That(fresh.Snapshot!.Source!.ArtifactSource.SourceDigest != command.ExpectedSourceDigest && freshCapture.Value!.Run.Revision == run.Revision, "real-later-finding-source-changes-at-same-durable-run-revision");
        var stale = await freshCapture.Store().ApplyAsync(run.RunId, task, Policies.Consultant, command with { EventId = Guid.NewGuid(), ExpectedRevision = entry.Revision + 1 });
        Check.Equal(stale.Issue, PlanningTaskIssue.SourceConflict, "actual-source-writer-first-new-obsolete-task-command-denied");
    }

    private static async Task HistoricalTen()
    {
        var six = JsonNode.Parse(File.ReadAllText(OracleCases.Fixture("historical-six-locks.json")))!["entries"]!.AsArray();
        var inventory = six.Select(e => (e!["baselineId"]!.GetValue<string>(), e["profileId"]!.GetValue<string>())).Concat(new[]
        {
            ("baseline-ai-configuration-v1", "profile-ai-preview-v1"), ("baseline-ai-configuration-v1", "profile-ai-preview-empty-v1"),
            ("synthetic-analysis-findings-v1", "synthetic-review-maturity-fix-packages-equal-v1"),
            ("synthetic-analysis-findings-v1", "synthetic-review-maturity-fix-review-equal-v1")
        }).ToArray();
        Check.Equal(inventory.Length, 10, "independent-exact-historical-ten-inventory");
        foreach (var (baseline, profile) in inventory)
        {
            var saved = await Complete(baseline, profile);
            Check.That(saved.FrozenInputs.PlanningTaskContractDigest is null && !JsonSerializer.Serialize(saved.FrozenInputs).Contains("PlanningTaskContractDigest", StringComparison.Ordinal), "actual-all-ten-historical-input-new-lock-omitted");
            var oldCapture = new Capture(); var read = await oldCapture.Store().ReadAsync(saved.RunId, Policies.Consultant);
            Check.That(read.Issue == PlanningTaskIssue.SourceUnavailable, "actual-old-profile-never-promoted-to-task-source");
            Check.Equal(await OwnedDatabase.FullRows(saved.RunId), string.Join("\n", Enumerable.Repeat("[]", 10)), "actual-ten-historical-profiles-no-task-or-artifact-write-effect");
        }
        foreach (var literal in six)
        {
            var request = DemoFixtureCatalog.CreateStartRequest(literal!["baselineId"]!.GetValue<string>(), literal["profileId"]!.GetValue<string>(), "v14-independent-historical-original");
            Check.Equal(JsonSerializer.Serialize(request.Versions), literal["versionsJson"]!.GetValue<string>(), "original-historical-six-lock-bytes-not-regenerated");
        }
        Check.Equal(DemoFixtureCatalog.Profiles.Count, 11, "exact-ten-historical-plus-one-task-profile");
        Check.Equal(DemoFixtureCatalog.Baselines.Count, 9, "no-new-baseline-authority");
    }
}
