using System.Collections.Immutable;
using System.Text.Json;
using AssessmentOrchestration;
using AssessmentRuns;
using FindingReview;
using ReportDrafts;

internal static class PersistenceCases
{
    internal static async Task Run(string[] args)
    {
        if (!args.Contains("--postgres", StringComparer.Ordinal)) { Console.WriteLine("NOT VERIFIED DR-PG: actual saved-run source/review requires --postgres."); return; }
        var connection = Environment.GetEnvironmentVariable("IGA_DRAFT_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v6;Username=iga_synthetic";
        var guard = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (guard.Host != "127.0.0.1" || guard.Database != "iga_synthetic_v6") throw new InvalidOperationException("Only own disposable synthetic v6 database allowed.");
        SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
        var engine = Engine(); await engine.InitializeAsync();
        var store = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed); await store.InitializeAsync();
        var actor = new SyntheticReviewAuthority("synthetic-consultant", true, true, false, true, SyntheticReviewScope.Fixed,
            ImmutableArray.Create(SyntheticReviewRole.Consultant), ImmutableArray.Create("SECURITY", "OPERATIONS"),
            ImmutableArray.Create(SyntheticReviewAction.Read, SyntheticReviewAction.Review, SyntheticReviewAction.AddComment, SyntheticReviewAction.EditPresentation));
        async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
        {
            var started = Accept(await engine.StartAsync(DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"))));
            var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, started.RunId, "independent-v6-verifier"));
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, started.RunId, leased.Lease!.Generation, leased.Revision, DemoFixtureCatalog.Baselines.Single(b => b.Id == baseline).ScriptedResults));
            return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, started.RunId, leased.Lease.Generation, checkpoint.Revision));
        }
        var run = await Complete("synthetic-analysis-findings-v1", "synthetic-review-maturity-equal-v1");
        var analysis = SyntheticDemoAnalysisAdapter.Project(run).Projection!.Analysis;
        var seeds = analysis.Groups.Select(group =>
        {
            var members = analysis.Findings.Where(f => f.RootCauseKey == group.RootCauseKey).OrderBy(f => f.OccurrenceId, StringComparer.Ordinal).ToArray(); var first = members[0];
            return new SyntheticFindingSeed(group.RootCauseKey, first.CategoryId, Enum.Parse<SyntheticFindingState>(first.InitialDisposition.ToString()), first.Title,
                members.Select(f => f.GeneratedOriginalDigest).Distinct().Order(StringComparer.Ordinal).ToImmutableArray(), members.Select(f => new SyntheticOccurrenceReference(f.OccurrenceId, f.ObjectId, f.Provenance.RuleId, f.Provenance.RuleVersion, f.GeneratedOriginalDigest)).ToImmutableArray());
        }).OrderBy(f => f.FindingId, StringComparer.Ordinal).ToImmutableArray();
        Check.That((await store.SeedAsync(new(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest, analysis.ContentDigest, SyntheticReviewResourceState.Mutable, seeds), actor)).Succeeded, "saved actual run seeds exact immutable originals");
        async Task<SyntheticReviewSnapshot> Read(SyntheticReviewStore current)
        {
            var response = await current.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, actor); Check.That(response.Succeeded && response.Snapshot is not null, "authorized actual review snapshot"); return response.Snapshot!;
        }
        var initialReview = await Read(store); var initial = CoreCases.Accept(SavedDraftFixture.Create(run, initialReview));
        Scores(initial, "44.2", "78.3", 4);
        Check.That(initial.Source.RunInputDigest == run.InputDigest && initial.Source.ReviewSnapshotDigest == initialReview.SnapshotDigest && initial.Source.RunState == "Scoring", "draft binds exact actual source and stays Scoring");
        Check.That(initial.Content.GetProperty("maturity").GetProperty("level").GetString() == "Managed" && initial.Content.GetProperty("maturity").GetProperty("improvementMissingDistinctAssessments").GetInt32() == 5, "independent actual Managed/single-assessment limitation goldens");
        var retainedBytes = DraftSnapshotBuilder.CanonicalPayload(initial); var retainedMarkdown = StructuredDraftMarkdown.Render(initial);
        var critical = analysis.Findings.First(f => f.Severity == DeterministicAnalysis.SyntheticSeverity.Critical).RootCauseKey;
        var high = analysis.Findings.First(f => f.Severity == DeterministicAnalysis.SyntheticSeverity.High).RootCauseKey;
        Check.That((await store.ApplyAsync(SyntheticReviewScope.Fixed, run.RunId, critical, actor, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm))).Succeeded, "actual saved confirm");
        var confirmed = CoreCases.Accept(SavedDraftFixture.Create(run, await Read(store))); Scores(confirmed, "44.2", "39.2", 2);
        Check.That((await store.ApplyAsync(SyntheticReviewScope.Fixed, run.RunId, high, actor, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: "Independent synthetic rejection explanation"))).Succeeded, "actual saved reject");
        var rejected = CoreCases.Accept(SavedDraftFixture.Create(run, await Read(store))); Scores(rejected, "64.2", "64.2", 0);
        Check.That(initial.CanonicalContentDigest != confirmed.CanonicalContentDigest && confirmed.CanonicalContentDigest != rejected.CanonicalContentDigest, "each coherent actual review creates new draft identity");
        Check.That(DraftSnapshotBuilder.CanonicalPayload(initial).SequenceEqual(retainedBytes) && StructuredDraftMarkdown.Render(initial) == retainedMarkdown && DraftSnapshotBuilder.ValidateSnapshot(initial), "retained old value/Markdown unchanged after actual saved review");
        Check.Equal(initial.Content.GetProperty("maturity").GetRawText(), rejected.Content.GetProperty("maturity").GetRawText(), "review cannot rewrite captured frozen maturity");
        var freshRun = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)); var freshReview = await Read(new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed));
        var reloaded = CoreCases.Accept(SavedDraftFixture.Create(freshRun, freshReview));
        Check.That(freshRun.ObservedAtDatabaseUtc != run.ObservedAtDatabaseUtc && reloaded.CanonicalContentDigest == rejected.CanonicalContentDigest && StructuredDraftMarkdown.Render(reloaded) == StructuredDraftMarkdown.Render(rejected), "fresh database observation time excluded; reload exact draft/Markdown bytes");
        Check.That((await Engine().ReadAsync(new("wrong-customer", "synthetic-project", "synthetic-environment"), run.RunId)).Issue == SyntheticRunIssue.WrongScope &&
            (await store.ReadAsync(new("wrong-customer", "synthetic-project", "synthetic-environment"), run.RunId, actor)).Issue == SyntheticReviewIssue.WrongScope, "actual saved source denies cross-scope resolution");
        foreach (var profile in HistoricalGoldens.Values.Keys)
        {
            var baseline = profile.StartsWith("profile-", StringComparison.Ordinal) ? "baseline-complete" : "synthetic-analysis-findings-v1";
            var legacy = await Complete(baseline, profile); var golden = HistoricalGoldens.Values[profile];
            Check.That(JsonSerializer.Serialize(legacy.FrozenInputs) == golden.Versions && legacy.InputDigest == golden.InputDigest, "all four historical locked inputs retain independent pre-cycle literal bytes/digests");
            var denial = DraftSnapshotBuilder.Build(CoreFixture.Create() with { Source = initial.Source with { ProfileId = profile } });
            Check.That(!denial.Succeeded && denial.Snapshot is null, "legacy profiles never acquire draft through pure version guard");
        }
        await using var database = new Npgsql.NpgsqlConnection(connection); await database.OpenAsync();
        await using var command = database.CreateCommand(); command.CommandText = "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('synthetic_report','report_drafts','synthetic_drafts')";
        Check.Equal(Convert.ToInt32(await command.ExecuteScalarAsync()), 0, "no new report persistence schema introduced");
        Check.Group("DR-PG-001 actual saved engine/review draft coherence/current change/old immutable value/fresh reload/wrongscope/historical locks/no storage");
    }
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result) { Check.That(result.Succeeded && result.Snapshot is not null, $"actual saved assessment accepted {result.Issue}"); return result.Snapshot!; }
    private static void Scores(DraftReportSnapshot snapshot, string p, string c, int pending)
    {
        Check.That(snapshot.Content.GetProperty("provisional").GetProperty("display").GetString() == p && snapshot.Content.GetProperty("publishableCurrent").GetProperty("display").GetString() == c && snapshot.Content.GetProperty("quality").GetProperty("proposedReviewUnits").GetInt32() == pending, "independent actual health/mandatory-review literals");
    }
}
