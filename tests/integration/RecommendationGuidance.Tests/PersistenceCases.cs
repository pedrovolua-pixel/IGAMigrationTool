using System.Collections.Immutable;
using System.Text.Json;
using AssessmentRuns;
using FindingReview;
using RecommendationGuidance;
using ReportDrafts;

internal static class PersistenceCases
{
    internal static async Task Run(string[] args)
    {
        if (!args.Contains("--postgres", StringComparer.Ordinal)) { Console.WriteLine("NOT VERIFIED RG-PG: actual saved run/review requires --postgres."); return; }
        var connection = Environment.GetEnvironmentVariable("IGA_GUIDANCE_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v7;Username=iga_synthetic";
        var guard = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (guard.Host != "127.0.0.1" || guard.Database != "iga_synthetic_v7") throw new InvalidOperationException("Only own disposable synthetic v7 database allowed.");
        SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
        var engine = Engine(); await engine.InitializeAsync();
        var store = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed); await store.InitializeAsync();
        var actor = new SyntheticReviewAuthority("synthetic-consultant", true, true, false, true, SyntheticReviewScope.Fixed,
            ImmutableArray.Create(SyntheticReviewRole.Consultant), ImmutableArray.Create("SECURITY", "OPERATIONS"),
            ImmutableArray.Create(SyntheticReviewAction.Read, SyntheticReviewAction.Review, SyntheticReviewAction.AddComment, SyntheticReviewAction.EditPresentation));
        async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
        {
            var started = Accept(await engine.StartAsync(DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"))));
            var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, started.RunId, "independent-v7-verifier"));
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, started.RunId, leased.Lease!.Generation, leased.Revision, DemoFixtureCatalog.Baselines.Single(b => b.Id == baseline).ScriptedResults));
            return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, started.RunId, leased.Lease.Generation, checkpoint.Revision));
        }
        async Task<SyntheticReviewSnapshot> SeedRead(SyntheticRunSnapshot run, SyntheticReviewStore current)
        {
            var analysis = SyntheticDemoAnalysisAdapter.Project(run).Projection!.Analysis;
            var seeds = analysis.Groups.Select(group =>
            {
                var members = analysis.Findings.Where(f => f.RootCauseKey == group.RootCauseKey).OrderBy(f => f.OccurrenceId, StringComparer.Ordinal).ToArray(); var first = members[0];
                return new SyntheticFindingSeed(group.RootCauseKey, first.CategoryId, Enum.Parse<SyntheticFindingState>(first.InitialDisposition.ToString()), first.Title,
                    members.Select(f => f.GeneratedOriginalDigest).Distinct().Order(StringComparer.Ordinal).ToImmutableArray(), members.Select(f => new SyntheticOccurrenceReference(f.OccurrenceId, f.ObjectId, f.Provenance.RuleId, f.Provenance.RuleVersion, f.GeneratedOriginalDigest)).ToImmutableArray());
            }).OrderBy(f => f.FindingId, StringComparer.Ordinal).ToImmutableArray();
            Check.That((await current.SeedAsync(new(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest, analysis.ContentDigest, SyntheticReviewResourceState.Mutable, seeds), actor)).Succeeded, "exact immutable generated originals seed actual review");
            return await Read(run, current);
        }
        async Task<SyntheticReviewSnapshot> Read(SyntheticRunSnapshot run, SyntheticReviewStore current)
        {
            var response = await current.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, actor); Check.That(response.Succeeded && response.Snapshot is not null, "authorized actual captured review snapshot"); return response.Snapshot!;
        }
        var run = await Complete("synthetic-analysis-findings-v1", "synthetic-review-maturity-equal-v1");
        var review = await SeedRead(run, store); var initial = CoreCases.Accept(SavedGuidanceFixture.Create(run, review));
        Check.Equal(initial.Findings.Length, 5, "five actual root-cause groups independent literal");
        Check.Equal(initial.Findings.Sum(f => f.Occurrences.Length), 10, "ten generated original occurrences literal");
        Check.Equal(initial.Findings.Sum(f => f.Options.Length), 10, "two original options in all five groups literal");
        Check.Equal(initial.Findings.SelectMany(f => f.Options).Select(o => o.ScopedOptionId).Distinct().Count(), 10, "all repeated catalog option IDs receive distinct scope/run/group identities");
        foreach (var finding in initial.Findings)
        {
            Check.That(finding.Options.Select(o => o.OptionId).SequenceEqual(new[] { "compare-new-fixture", "inspect-fixture" }), "literal original stable alternative order");
            Check.Equal(finding.Options[0].Text, "Compare new passing synthetic evidence as a separate run.", "literal original compare text");
            Check.Equal(finding.Options[0].Prerequisites, "A new immutable fixture evidence baseline.", "literal original compare prerequisites");
            Check.Equal(finding.Options[0].Risk, "A disposition alone cannot validate closure.", "literal original compare risk");
            Check.Equal(finding.Options[0].RecoveryGuidance, "Retain the original generated occurrence and comparison history.", "literal original compare recovery");
            Check.Equal(finding.Options[1].Text, "Review the synthetic marker and its typed fact before changing anything.", "literal original inspect text");
            Check.Equal(finding.Options[1].Prerequisites, "Consultant review of the generated original.", "literal original inspect prerequisites");
            Check.Equal(finding.Options[1].Risk, "Unverified recommendation; do not execute.", "literal original inspect risk");
            Check.Equal(finding.Options[1].RecoveryGuidance, "Keep the prior evidence baseline; compare a new fixture run.", "literal original inspect recovery");
            Check.That(finding.Options.All(o => o.Status == "Unverified"), "auto-confirmed/proposed finding never validates advice");
            Check.That(finding.ValidationGuidance.SequenceEqual(new[] { "Check the exact rule/version, fixture baseline and evidence reference.", "Confirm the known marker count is zero in new passing evidence; never overwrite this original." }), "literal ordered full validation guidance");
            Check.That(finding.GuidanceReferences.SequenceEqual(new[] { "fixture-guidance:" + finding.RuleId + ":v1" }), "literal authoritative fictional reference bound to rule");
        }
        Check.That(initial.Source.RunId == run.RunId && initial.Source.RunRevision == run.Revision && initial.Source.RunState == "Scoring" && initial.Source.ReviewSnapshotDigest == review.SnapshotDigest, "exact actual saved source and captured review bindings");
        var retainedBytes = CoreCases.Canonical(initial); var retainedDraft = DraftSnapshotBuilder.Build(SavedDraftFixture.Create(run, review)).Snapshot!;
        Check.That(retainedDraft.Content.GetProperty("provisional").GetProperty("display").GetString() == "44.2" && retainedDraft.Content.GetProperty("publishableCurrent").GetProperty("display").GetString() == "78.3", "unchanged independent health literals");
        var critical = initial.Findings.Single(f => f.Severity == "Critical"); var high = initial.Findings.Single(f => f.Severity == "High");
        async Task<GuidanceSnapshot> Change(GuidanceFinding finding, SyntheticReviewEventKind kind, long revision, string? reason = null, string? title = null, string? context = null)
        {
            Check.That((await store.ApplyAsync(SyntheticReviewScope.Fixed, run.RunId, finding.FindingId, actor, new(Guid.NewGuid(), revision, kind, Reason: reason, Title: title, BusinessContext: context))).Succeeded, "existing approved actual review operation only");
            return CoreCases.Accept(SavedGuidanceFixture.Create(run, await Read(run, store)));
        }
        var confirmed = await Change(critical, SyntheticReviewEventKind.Confirm, 0);
        Check.Equal(confirmed.Findings.Single(f => f.FindingId == critical.FindingId).CurrentState, "Confirmed", "current confirmed finding state retained");
        Check.That(confirmed.Findings.Single(f => f.FindingId == critical.FindingId).Options.SequenceEqual(critical.Options), "Confirm cannot review/validate/remodel advice");
        var rejected = await Change(high, SyntheticReviewEventKind.Reject, 0, "Independent synthetic rejection explanation");
        Check.Equal(rejected.Findings.Single(f => f.FindingId == high.FindingId).CurrentState, "Rejected", "current rejected finding state retained");
        Check.That(rejected.Findings.Single(f => f.FindingId == high.FindingId).Options.SequenceEqual(high.Options), "Reject retains complete original unverified guidance");
        var edited = await Change(critical, SyntheticReviewEventKind.EditPresentation, 1, title: CoreFixture.Hostile, context: CoreFixture.Hostile);
        var editedFinding = edited.Findings.Single(f => f.FindingId == critical.FindingId);
        Check.That(editedFinding.PresentationTitle == CoreFixture.Hostile && editedFinding.BusinessContext == CoreFixture.Hostile && editedFinding.OriginalTitle == critical.OriginalTitle, "captured hostile current presentation distinct from unchanged original");
        Check.That(editedFinding.Options.SequenceEqual(critical.Options) && editedFinding.Occurrences.SequenceEqual(critical.Occurrences), "actual saved edits preserve exact original alternatives/provenance/scoped identities");
        Check.Equal(new[] { initial.ContentDigest, confirmed.ContentDigest, rejected.ContentDigest, edited.ContentDigest }.Distinct().Count(), 4, "each coherent actual review changes complete current snapshot digest");
        Check.That(CoreCases.Canonical(initial).SequenceEqual(retainedBytes), "earlier returned snapshot byte immutable after database review mutations");
        var freshRun = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)); var freshStore = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed);
        var freshReview = await Read(freshRun, freshStore); var fresh = CoreCases.Accept(SavedGuidanceFixture.Create(freshRun, freshReview));
        Check.Equal(fresh.ContentDigest, edited.ContentDigest, "fresh engine/store read excludes observation time, reproduces exact digest");
        Check.That(CoreCases.Canonical(fresh).SequenceEqual(CoreCases.Canonical(edited)), "fresh saved read canonical bytes reproducible");
        Check.That((await Engine().ReadAsync(new("wrong-customer", "synthetic-project", "synthetic-environment"), run.RunId)).Issue == SyntheticRunIssue.WrongScope &&
            (await store.ReadAsync(new("wrong-customer", "synthetic-project", "synthetic-environment"), run.RunId, actor)).Issue == SyntheticReviewIssue.WrongScope, "actual saved run/review resolvers deny cross-scope reads");
        foreach (var baseline in new[] { "synthetic-analysis-healthy-v1", "synthetic-analysis-gaps-v1" })
        {
            var emptyRun = await Complete(baseline, "synthetic-review-maturity-equal-v1");
            var empty = CoreCases.Accept(SavedGuidanceFixture.Create(emptyRun, await SeedRead(emptyRun, store)));
            Check.Equal(empty.Findings.Length, 0, "actual healthy/all-gap sources never fabricate advice");
        }
        foreach (var profile in HistoricalGoldens.Values.Keys)
        {
            var baseline = profile.StartsWith("profile-", StringComparison.Ordinal) ? "baseline-complete" : "synthetic-analysis-findings-v1";
            var legacy = await Complete(baseline, profile); var golden = HistoricalGoldens.Values[profile];
            Check.That(JsonSerializer.Serialize(legacy.FrozenInputs) == golden.Versions && legacy.InputDigest == golden.InputDigest, "all four legacy frozen envelopes/input digests remain exact independent pre-cycle literals");
            Check.That(RecommendationGuidanceBuilder.Build(InputFixture.Create() with { Source = initial.Source with { ProfileId = profile } }).Snapshot is null, "legacy profiles never acquire guidance through pure version guard");
        }
        await using var database = new Npgsql.NpgsqlConnection(connection); await database.OpenAsync();
        await using var command = database.CreateCommand(); command.CommandText = "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('synthetic_guidance','recommendation_guidance','synthetic_recommendations')";
        Check.Equal(Convert.ToInt32(await command.ExecuteScalarAsync()), 0, "no new guidance/recommendation persistence schema");
        Check.Group("RG-PG-001 actual saved grouped options/current review/immutable original/scoped identity/reload/old locks/no storage");
    }
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result) { Check.That(result.Succeeded && result.Snapshot is not null, $"actual saved assessment accepted {result.Issue}"); return result.Snapshot!; }
}
