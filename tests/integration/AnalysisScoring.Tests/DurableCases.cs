using System.Text.Json;
using AssessmentRuns;
using AssessmentOrchestration;
using AssessmentScoring;
using Npgsql;

internal static class DurableCases
{
    private const string Connection = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v4;Username=iga_synthetic";
    internal static async Task Run(string[] args)
    {
        if (!args.Contains("--postgres", StringComparer.Ordinal))
        {
            Console.WriteLine("NOT VERIFIED AS-STORE-001: actual PostgreSQL checks require --postgres and disposable iga_synthetic_v4 database.");
            return;
        }
        var connection = Environment.GetEnvironmentVariable("IGA_ANALYSIS_TEST_DATABASE") ?? Connection;
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Host != "127.0.0.1" || builder.Database != "iga_synthetic_v4")
            throw new InvalidOperationException("Analysis verification accepts only its explicitly disposable loopback database.");
        SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
        var engine = Engine();
        await engine.InitializeAsync();
        foreach (var (baseline, provisionalDisplay, publishableDisplay, findings, proposals, numerator, denominator) in new[]
        {
            ("synthetic-analysis-healthy-v1", 100m, 100m, 0, 0, 10, 10),
            ("synthetic-analysis-findings-v1", 44.2m, 78.3m, 10, 4, 10, 10),
            ("synthetic-analysis-mixed-v1", 66.7m, 89.2m, 5, 2, 7, 10)
        })
        {
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, "synthetic-analysis-equal-v1", Guid.NewGuid().ToString("D"));
            var initial = Accept(await engine.StartAsync(request));
            DenyAnalysis(initial, "coverage_not_ready", "partial saved run cannot score");
            var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, initial.RunId, "independent-analysis-worker"));
            var expectedCoverage = DemoFixtureCatalog.Baselines.Single(item => item.Id == baseline).ScriptedResults;
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease!.Generation, leased.Revision, expectedCoverage));
            DenyAnalysis(checkpoint, "coverage_not_ready", "complete results without canonical coverage-stage commit cannot score");
            var complete = Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease.Generation, checkpoint.Revision));
            var projected = Analysis(complete);
            Program.Require(projected.Scoring.Provisional.Overall.DisplayScore == provisionalDisplay &&
                projected.Scoring.PublishableCurrent.Overall.DisplayScore == publishableDisplay,
                $"literal {baseline} independent display goldens");
            Program.Require(projected.Analysis.Findings.Length == findings && projected.Scoring.Quality.ProposedFindings == proposals &&
                projected.Scoring.Quality.ExecutableCoverage is { } coverage && coverage.ExecutedUnits == numerator && coverage.ApplicablePlannedUnits == denominator,
                $"literal {baseline} findings/review/quality counts");
            if (baseline == "synthetic-analysis-healthy-v1")
                Program.Score(projected.Scoring.Provisional.Overall, 100m, "all actual predicate passes yield100");
            if (baseline == "synthetic-analysis-findings-v1")
                Program.Require(projected.Scoring.Provisional.Overall.RawScore is > 44.16m and < 44.17m &&
                    projected.Scoring.PublishableCurrent.Overall.RawScore is > 78.33m and < 78.34m &&
                    projected.Scoring.Provisional.Overall.Status == HealthStatus.Red && projected.Scoring.PublishableCurrent.Overall.Status == HealthStatus.Yellow,
                    "unrounded recurring-decimal raw goldens and provisional/publishable status");
            var fresh = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, complete.RunId));
            Program.Require(fresh.InputDigest == initial.InputDigest && fresh.FrozenInputs == request.Versions &&
                JsonSerializer.Serialize(Analysis(fresh)) == JsonSerializer.Serialize(projected), "fresh database read reproduces exact full frozen analysis/scoring projection");
            DenyAnalysis(fresh with { FrozenInputs = fresh.FrozenInputs with { AnalysisFixtureDigest = new('0', 64) } }, "frozen_fixture_mismatch", "analysis pack lock mismatch refuses");
            DenyAnalysis(fresh with { FrozenInputs = fresh.FrozenInputs with { ProfileVersion = "unknown-profile-version" } }, "frozen_fixture_mismatch", "analysis profile version mismatch refuses");
            DenyAnalysis(fresh with { FrozenInputs = fresh.FrozenInputs with { ScoringAlgorithmVersion = "unknown-algorithm" } }, "frozen_fixture_mismatch", "analysis algorithm version mismatch refuses");
            foreach (var changed in new[]
            {
                fresh.FrozenInputs with { ApplicationVersion = "unknown-app" },
                fresh.FrozenInputs with { AiPolicyVersion = "unknown-ai-policy" },
                fresh.FrozenInputs with { PromptVersion = "unknown-prompt" },
                fresh.FrozenInputs with { ModelVersion = "unknown-model" },
                fresh.FrozenInputs with { WorkSchemaVersion = "unknown-work-schema" },
                fresh.FrozenInputs with { DesiredOutcomeVersion = "unknown-outcomes" },
                fresh.FrozenInputs with { ScriptedResultsDigest = new('0', 64) }
            }) DenyAnalysis(fresh with { FrozenInputs = changed }, "frozen_fixture_mismatch", "entire frozen version tuple must match selected exact fixture");
            foreach (var changedPlan in new[]
            {
                fresh.Plan with { CapabilityLock = fresh.Plan.CapabilityLock with { MatrixVersion = "same-key-altered-matrix" } },
                fresh.Plan with { Permission = SyntheticBaselinePermission.EligibleWithWarning },
                fresh.Plan with { Objects = fresh.Plan.Objects.Select((item, index) => index == 0 ? item with { NativeType = "same-key-altered-type" } : item).ToArray() },
                fresh.Plan with { Objects = fresh.Plan.Objects.Select((item, index) => index == 0 ? item with { NativeIdentity = "same-key-altered-native-identity" } : item).ToArray() }
            }) DenyAnalysis(fresh with { Plan = changedPlan }, "frozen_fixture_mismatch", "same exact expected keys cannot conceal altered frozen plan metadata");
            DenyAnalysis(fresh with { InputDigest = "invalid-format" }, "invalid_input_digest", "malformed digest never projects");
            DenyAnalysis(fresh with { Scope = fresh.Scope with { CustomerId = "synthetic-other" } }, "invalid_synthetic_scope", "wrong scoped saved projection refuses");
            DenyAnalysis(fresh with { CancelRequested = true }, "coverage_not_ready", "cancel-requested snapshot cannot score");
            DenyAnalysis(fresh with { Results = fresh.Results.Take(9).ToArray() }, "saved_plan_mismatch", "missing terminal result cannot score");
            DenyAnalysis(fresh with { Results = fresh.Results.Select((item, index) => index == 0 ? item with { EvidenceReference = "fixture-evidence:wrong" } : item).ToArray() },
                "saved_result_mismatch", "actual saved coverage provenance mismatch cannot score");
            Program.Require(await Count(connection, initial.RunId) == 10, "actual database has ten unique saved rule-subject results");
        }
        var gapRequest = DemoFixtureCatalog.CreateStartRequest("synthetic-analysis-gaps-v1", "synthetic-analysis-equal-v1", Guid.NewGuid().ToString("D"));
        var gapStart = Accept(await engine.StartAsync(gapRequest));
        var gapLease = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, gapStart.RunId, "independent-gap-worker"));
        var gapCheckpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, gapStart.RunId, gapLease.Lease!.Generation, gapLease.Revision,
            DemoFixtureCatalog.Baselines.Single(item => item.Id == gapRequest.BaselineCatalogId).ScriptedResults));
        var gapComplete = Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, gapStart.RunId, gapLease.Lease.Generation, gapCheckpoint.Revision));
        var gaps = Analysis(gapComplete);
        Program.Unavailable(gaps.Scoring.Provisional.Overall, "all missing/excluded saved facts do not become100");
        Program.Unavailable(gaps.Scoring.PublishableCurrent.Overall, "all gaps unavailable publishable");
        Program.Require(gaps.Scoring.Quality is { ExpectedKeys: 10, RepresentedKeys: 10, ExecutableCoverage: { ExecutedUnits: 0, ApplicablePlannedUnits: 10 } } &&
            gaps.Scoring.Quality.Limitations.Count == 2 && gaps.Analysis.Findings.Length == 0, "ten saved gaps retain quality without fabricated healthy controls");

        var oldRequest = DemoFixtureCatalog.CreateStartRequest("baseline-complete", "profile-standard", Guid.NewGuid().ToString("D"));
        var oldJson = JsonSerializer.Serialize(oldRequest.Versions);
        var expectedLegacyJson = "{\"ProfileVersion\":\"synthetic-profile-v1\",\"DesiredOutcomeVersion\":null,\"ScoringAlgorithmVersion\":\"synthetic-scoring-unimplemented-v1\",\"AiPolicyVersion\":\"synthetic-ai-disabled-v1\",\"PromptVersion\":\"synthetic-prompt-disabled-v1\",\"ModelVersion\":\"synthetic-model-disabled-v1\",\"ApplicationVersion\":\"synthetic-app-v1\",\"WorkSchemaVersion\":\"synthetic-run-work-v1\",\"ScriptedResultsDigest\":\"" + oldRequest.Versions.ScriptedResultsDigest + "\"}";
        Program.Require(oldJson == expectedLegacyJson && !oldJson.Contains("AnalysisFixtureDigest", StringComparison.Ordinal), "historical coverage-only envelope preserves exact old serialization with absent optional metadata");
        var oldStart = Accept(await engine.StartAsync(oldRequest));
        DenyAnalysis(oldStart, "coverage_only_fixture", "old coverage-only preset never implicitly opts into analysis");
        var oldLease = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, oldStart.RunId, "independent-historical-worker"));
        var oldCheckpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, oldStart.RunId, oldLease.Lease!.Generation, oldLease.Revision,
            DemoFixtureCatalog.Baselines.Single(item => item.Id == oldRequest.BaselineCatalogId).ScriptedResults));
        var oldComplete = Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, oldStart.RunId, oldLease.Lease.Generation, oldCheckpoint.Revision));
        var oldReload = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, oldStart.RunId));
        Program.Require(JsonSerializer.Serialize(oldReload.FrozenInputs) == oldJson && oldReload.InputDigest == oldStart.InputDigest && oldReload.Results.Count == 4,
            "fresh storage read preserves exact old frozen envelope/digest and four terminal results");
        DenyAnalysis(oldComplete, "coverage_only_fixture", "old complete coverage cannot acquire new scores retroactively");
        await using (var database = new NpgsqlConnection(connection))
        {
            await database.OpenAsync();
            try
            {
                await using (var disable = new NpgsqlCommand("ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs", database))
                    await disable.ExecuteNonQueryAsync();
                await using var corrupt = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET input_digest=@digest WHERE run_id=@run", database);
                corrupt.Parameters.AddWithValue("digest", new string('0', 64));
                corrupt.Parameters.AddWithValue("run", oldComplete.RunId);
                await corrupt.ExecuteNonQueryAsync();
                var denied = await Engine().ReadAsync(DemoFixtureCatalog.Scope, oldComplete.RunId);
                Program.Require(!denied.Succeeded && denied.Snapshot is null && denied.Issue == SyntheticRunIssue.InputIntegrityMismatch,
                    "actual stored raw input digest corruption refuses engine read before analysis projection");
            }
            finally
            {
                await using var restore = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET input_digest=@digest WHERE run_id=@run", database);
                restore.Parameters.AddWithValue("digest", oldComplete.InputDigest);
                restore.Parameters.AddWithValue("run", oldComplete.RunId);
                await restore.ExecuteNonQueryAsync();
                await using var enable = new NpgsqlCommand("ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs", database);
                await enable.ExecuteNonQueryAsync();
            }
            await Engine().InitializeAsync();
            Program.Require(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, oldComplete.RunId)).InputDigest == oldComplete.InputDigest,
                "restored synthetic row/trigger preserve original historical input evidence");
        }

        var cancelStart = Accept(await engine.StartAsync(gapRequest with { IdempotencyKey = Guid.NewGuid().ToString("D") }));
        var requested = Accept(await engine.RequestCancelAsync(DemoFixtureCatalog.Scope, cancelStart.RunId, cancelStart.Revision));
        var cancelled = Accept(await engine.FinalizeCancellationAsync(DemoFixtureCatalog.Scope, cancelStart.RunId, requested.Revision));
        DenyAnalysis(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, cancelled.RunId)), "coverage_not_ready", "persisted cancelled run cannot acquire synthetic score");
        Program.Group("AS-STORE-001 actual PostgreSQL complete-only/reload/version/provenance/historical/cancelled boundary");
    }

    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result)
    {
        Program.Require(result.Succeeded, $"durable command accepted, actual {result.Issue}");
        return result.Snapshot!;
    }
    private static SyntheticDemoAnalysisProjection Analysis(SyntheticRunSnapshot run)
    {
        var result = SyntheticDemoAnalysisAdapter.Project(run);
        Program.Require(result.IsAvailable && result.Projection is not null, $"complete saved analysis available, actual {result.ReasonCode}");
        return result.Projection!;
    }
    private static void DenyAnalysis(SyntheticRunSnapshot run, string expected, string name)
    {
        var result = SyntheticDemoAnalysisAdapter.Project(run);
        Program.Require(!result.IsAvailable && result.Projection is null && result.ReasonCode == expected, $"{name}, expected {expected}, actual {result.ReasonCode}");
    }
    private static async Task<long> Count(string connectionString, Guid runId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT count(*) FROM synthetic_assessment.results WHERE run_id=@run", connection);
        query.Parameters.AddWithValue("run", runId);
        return (long)(await query.ExecuteScalarAsync())!;
    }
}
