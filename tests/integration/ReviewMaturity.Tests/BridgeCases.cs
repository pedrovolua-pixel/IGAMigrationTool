using System.Collections.Immutable;
using System.Text.Json;
using AssessmentMaturity;
using AssessmentRuns;
using AssessmentScoring;
using FindingReview;

internal static class BridgeCases
{
    internal static async Task Run(string[] args)
    {
        Check.That(DemoFixtureCatalog.Profiles.Count == 8 && DemoFixtureCatalog.Profiles.Count(profile => !DemoAiPreviewCatalog.IsProfile(profile.Id)) == 6 && DemoFixtureCatalog.Profiles.Where(profile => DemoAnalysisCatalog.IsReviewMaturityProfile(profile.Id)).Select(profile => profile.Id)
            .Order(StringComparer.Ordinal).SequenceEqual(new[] { "synthetic-review-maturity-equal-v1", "synthetic-review-maturity-operations-v1" }), "exactly two explicit review/maturity profiles added; four historical profiles retained");
        if (!args.Contains("--postgres", StringComparer.Ordinal)) { Console.WriteLine("NOT VERIFIED RM-BRIDGE: actual saved-run checks require --postgres."); return; }
        var connection = Environment.GetEnvironmentVariable("IGA_REVIEW_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v5;Username=iga_synthetic";
        var guard = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (guard.Host != "127.0.0.1" || guard.Database != "iga_synthetic_v5") throw new InvalidOperationException("Only own disposable review verification database is allowed.");
        SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
        var engine = Engine(); await engine.InitializeAsync();
        var authority = ReviewPolicyCases.Consultant;
        var store = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed); await store.InitializeAsync();
        async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
        {
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"));
            var start = Accept(await engine.StartAsync(request));
            var lease = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, start.RunId, "independent-review-bridge"));
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, start.RunId, lease.Lease!.Generation, lease.Revision,
                DemoFixtureCatalog.Baselines.Single(item => item.Id == baseline).ScriptedResults));
            return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, start.RunId, lease.Lease.Generation, checkpoint.Revision));
        }
        foreach (var (profile, display) in new[] { ("synthetic-review-maturity-equal-v1", 44.2m), ("synthetic-review-maturity-operations-v1", 61.3m) })
        {
            var run = await Complete("synthetic-analysis-findings-v1", profile);
            var original = Project(run);
            var fixture = DemoAnalysisCatalog.FreezeMaturity(run.BaselineCatalogId, profile);
            var maturity = MaturityCases.Accept(fixture.Input);
            Check.That(run.FrozenInputs.MaturityFixtureDigest == fixture.ContentDigest && maturity.Level == MaturityLevel.Managed &&
                original.Scoring.Provisional.Overall.DisplayScore == display && original.Scoring.PublishableCurrent.Overall.DisplayScore == 78.3m,
                "literal new-profile health and independent frozen Managed maturity");
            var seed = new SyntheticReviewRunSeed(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest, original.Analysis.ContentDigest, SyntheticReviewResourceState.Mutable,
                original.Analysis.Groups.Select(group =>
                {
                    var members = original.Analysis.Findings.Where(item => item.RootCauseKey == group.RootCauseKey).ToArray();
                    return new SyntheticFindingSeed(group.RootCauseKey, members[0].CategoryId, Enum.Parse<SyntheticFindingState>(members[0].InitialDisposition.ToString()), members[0].Title,
                        members.Select(item => item.GeneratedOriginalDigest).ToImmutableArray(), members.Select(item => new SyntheticOccurrenceReference(item.OccurrenceId, item.ObjectId,
                            item.Provenance.RuleId, item.Provenance.RuleVersion, item.GeneratedOriginalDigest)).ToImmutableArray());
                }).ToImmutableArray());
            Check.That((await store.SeedAsync(seed, authority)).Succeeded, "canonical run analysis seeds trusted immutable per-object originals");
            var review = ReviewStoreCases.Read(await store.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, authority));
            Dictionary<string, ScoringFindingState> States(SyntheticReviewSnapshot snapshot) => snapshot.Findings.ToDictionary(item => item.Seed.FindingId, item => Enum.Parse<ScoringFindingState>(item.Current.State.ToString()));
            var initial = Project(run, States(review), review.SnapshotDigest);
            Check.That(initial.Scoring.Quality.UnreviewedMandatoryFindings == 4 && initial.Scoring.Provisional.Overall.DisplayScore == display &&
                initial.Scoring.PublishableCurrent.Overall.DisplayScore == 78.3m, "initial coherent review snapshot preserves four mandatory proposed occurrences");
            var critical = original.Analysis.Findings.First(item => item.Severity == DeterministicAnalysis.SyntheticSeverity.Critical).RootCauseKey;
            Check.That((await store.ApplyAsync(SyntheticReviewScope.Fixed, run.RunId, critical, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm))).Succeeded, "persisted confirm accepted");
            var confirmed = ReviewStoreCases.Read(await store.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, authority));
            var confirmation = Project(run, States(confirmed), confirmed.SnapshotDigest);
            Check.That(confirmation.Scoring.Quality.UnreviewedMandatoryFindings == 2 && confirmation.Scoring.Units.Count(item => item.FindingState == ScoringFindingState.Confirmed) == 2 &&
                confirmation.Scoring.Provisional.Overall.DisplayScore == display && confirmation.Scoring.PublishableCurrent.Overall.DisplayScore == (profile.EndsWith("equal-v1", StringComparison.Ordinal) ? 39.2m : 58.8m),
                "confirm overlays all two occurrences; penalty retained with independent literal score");
            var high = original.Analysis.Findings.First(item => item.Severity == DeterministicAnalysis.SyntheticSeverity.High).RootCauseKey;
            Check.That((await store.ApplyAsync(SyntheticReviewScope.Fixed, run.RunId, high, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: "Independent synthetic false-positive reason"))).Succeeded, "persisted reject accepted");
            var rejected = ReviewStoreCases.Read(await store.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, authority));
            var rejection = Project(run, States(rejected), rejected.SnapshotDigest);
            var expected = profile.EndsWith("equal-v1", StringComparison.Ordinal) ? 64.2m : 71.3m;
            Check.That(rejection.Scoring.Quality.UnreviewedMandatoryFindings == 0 && rejection.Scoring.Units.Count(item => item.FindingState == ScoringFindingState.Rejected) == 2 &&
                rejection.Scoring.Provisional.Overall.DisplayScore == expected && rejection.Scoring.PublishableCurrent.Overall.DisplayScore == expected,
                "reject removes current penalty for two occurrences; all mandatory review coherent");
            Check.That(JsonSerializer.Serialize(rejection.Analysis) == JsonSerializer.Serialize(original.Analysis) &&
                MaturityCases.Accept(fixture.Input).ContentDigest == maturity.ContentDigest, "review current health cannot rewrite analysis originals or frozen maturity");
            var fresh = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId));
            Check.That(fresh.InputDigest == run.InputDigest && fresh.FrozenInputs == run.FrozenInputs &&
                JsonSerializer.Serialize(Project(fresh, States(rejected), rejected.SnapshotDigest)) == JsonSerializer.Serialize(rejection), "fresh saved run reload reproduces exact overlay and original input locks");
            var malformed = States(rejected); malformed.Remove(critical);
            Check.Equal(SyntheticDemoAnalysisAdapter.Project(run, malformed, rejected.SnapshotDigest).ReasonCode, "invalid_review_snapshot", "missing group state never silently defaults partial review overlay");
            malformed = States(rejected); malformed[critical] = ScoringFindingState.AcceptedRisk;
            Check.Equal(SyntheticDemoAnalysisAdapter.Project(run, malformed, rejected.SnapshotDigest).ReasonCode, "invalid_review_snapshot", "unsupported risk cannot enter bounded review overlay");
            Check.Equal(SyntheticDemoAnalysisAdapter.Project(run with { FrozenInputs = run.FrozenInputs with { MaturityFixtureDigest = new string('0', 64) } }).ReasonCode,
                "frozen_fixture_mismatch", "altered whole maturity lock denies analysis");
        }
        foreach (var profile in new[] { "profile-standard", "profile-comparison", "synthetic-analysis-equal-v1", "synthetic-analysis-operations-v1" })
        {
            var baseline = profile.StartsWith("profile-", StringComparison.Ordinal) ? "baseline-complete" : "synthetic-analysis-findings-v1";
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, "independent-legacy-check");
            var encoded = JsonSerializer.Serialize(request.Versions);
            var golden = HistoricalGoldens.Values[profile];
            Check.Equal(encoded, golden.Versions, "literal pre-cycle baseline versions envelope stays byte identical");
            Check.That(request.Versions.MaturityFixtureDigest is null && !encoded.Contains("MaturityFixtureDigest", StringComparison.Ordinal) && !DemoAnalysisCatalog.IsReviewMaturityProfile(profile),
                "historical profile retains absent optional maturity metadata and no new review grant");
            var run = await Complete(baseline, profile);
            var fresh = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId));
            Check.That(JsonSerializer.Serialize(fresh.FrozenInputs) == encoded && fresh.InputDigest == golden.InputDigest && run.InputDigest == golden.InputDigest,
                "historical four profiles retain exact frozen envelope/digest after persistence reload");
            if (!profile.StartsWith("profile-", StringComparison.Ordinal))
            {
                var original = Project(fresh);
                var states = original.Analysis.Groups.ToDictionary(group => group.RootCauseKey, _ => ScoringFindingState.Confirmed);
                Check.Equal(SyntheticDemoAnalysisAdapter.Project(fresh, states, new string('a', 64)).ReasonCode, "invalid_review_snapshot", "historical analysis remains read-only even with supplied overlay");
            }
        }
        Check.Group("RM-BRIDGE-001 actual new-profile occurrence overlays, maturity locks and historical four-profile compatibility");
    }
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result)
    {
        Check.That(result.Succeeded && result.Snapshot is not null, $"assessment command accepted, actual {result.Issue}"); return result.Snapshot!;
    }
    private static SyntheticDemoAnalysisProjection Project(SyntheticRunSnapshot run, IReadOnlyDictionary<string, ScoringFindingState>? states = null, string? digest = null)
    {
        var result = SyntheticDemoAnalysisAdapter.Project(run, states, digest);
        Check.That(result.IsAvailable && result.Projection is not null, $"analysis overlay available, actual {result.ReasonCode}"); return result.Projection!;
    }
}
