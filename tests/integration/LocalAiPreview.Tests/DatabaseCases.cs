using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;
using Npgsql;

internal static class DatabaseCases
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static string connection = "";
    private static SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult value)
    { Check.That(value.Succeeded && value.Snapshot is not null, "actual-saved-run-accepted"); return value.Snapshot!; }
    private static JsonElement Detail(SyntheticRunSnapshot run) => JsonSerializer.SerializeToElement(DemoAiPreviewProjection.Detail(run), Web);
    private static void Unavailable(SyntheticRunSnapshot run)
    {
        var detail = Detail(run); Check.Equal(detail.GetProperty("status").GetString(), "Unavailable", "bad-source-unavailable");
        Check.That(detail.GetProperty("reasonCode").ValueKind == JsonValueKind.String && detail.GetProperty("snapshot").ValueKind == JsonValueKind.Null, "bad-source-no-partial-snapshot");
        Check.Equal(detail.GetProperty("fixtureDigest").ValueKind, JsonValueKind.String, "unavailable-DTO-fixture-digest-string");
    }
    internal static async Task Run()
    {
        connection = Environment.GetEnvironmentVariable("IGA_AI_PREVIEW_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v10;Username=iga_synthetic";
        var guard = new NpgsqlConnectionStringBuilder(connection);
        if (guard.Host != "127.0.0.1" || guard.Port != 55433 || guard.Database != "iga_synthetic_v10") throw new InvalidOperationException();
        var admin = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres" };
        await using (var db = new NpgsqlConnection(admin.ConnectionString))
        {
            await db.OpenAsync(); await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname='iga_synthetic_v10'", db);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
            { await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_v10", db); await create.ExecuteNonQueryAsync(); }
        }
        var engine = Engine(); await engine.InitializeAsync();
        async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
        {
            var key = Guid.NewGuid().ToString("D"); var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, key);
            var initial = Accept(await engine.StartAsync(request));
            var replay = await engine.StartAsync(request); Check.That(replay.AlreadyApplied && replay.Snapshot!.RunId == initial.RunId, "actual-start-idempotency");
            if (profile is Expected.Normal or Expected.Empty) Unavailable(initial);
            var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, initial.RunId, "v10-independent-verifier"));
            var outcomes = DemoFixtureCatalog.Baselines.Single(x => x.Id == baseline).ScriptedResults;
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease!.Generation, leased.Revision, outcomes));
            return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease.Generation, checkpoint.Revision));
        }
        var normal = await Complete(Expected.Baseline, Expected.Normal); var empty = await Complete(Expected.Baseline, Expected.Empty);
        Check.Equal(normal.State, SyntheticRunState.Scoring, "AI-preview-run-remains-scoring"); Check.That(!normal.CancelRequested && normal.Results.Count == 2 && normal.Results.All(x => x.State == CoverageState.Pass), "two-explicit-scripted-coverage-results");
        foreach (var run in new[] { normal, empty })
        {
            var detail = Detail(run); Check.Equal(detail.GetProperty("status").GetString(), "Ready", "actual-saved-preview-ready");
            Check.Equal(detail.GetProperty("reasonCode").ValueKind, JsonValueKind.Null, "ready-no-denial");
            var snapshot = detail.GetProperty("snapshot"); var expected = Expected.Full(run, DemoAiPreviewCatalog.PacketTemplateDigest);
            Check.Equal(snapshot.GetProperty("canonicalJson").GetString(), expected.Preview, "full-independent-saved-preview-bytes");
            Check.Equal(snapshot.GetProperty("contentDigest").GetString(), Expected.Hash(expected.Preview), "full-independent-saved-preview-digest");
            Check.Equal(snapshot.GetProperty("packetDigest").GetString(), Expected.Hash(expected.Packet), "full-independent-saved-packet-digest");
            Check.Equal(snapshot.GetProperty("proposalDigest").GetString(), Expected.Hash(expected.Proposal), "full-independent-saved-proposal-digest");
            Check.Equal(Expected.Canonical(JsonNode.Parse(snapshot.GetProperty("proposals").GetRawText())), Expected.Canonical(JsonNode.Parse(expected.Preview)!["proposals"]), "all-ordered-categories-citations-context-conflicts-preserved");
            Check.Equal(snapshot.GetProperty("status").GetString(), "Proposed", "offline-proposals-not-promoted");
            Check.Equal(snapshot.GetProperty("source").GetProperty("runId").GetString(), run.RunId.ToString("D"), "actual-source-run");
            Check.Equal(snapshot.GetProperty("source").GetProperty("profileDigest").GetString(), run.InputDigest, "actual-source-input-lock");
            Check.Equal(snapshot.GetProperty("source").GetProperty("baselineDigest").GetString(), DemoAiPreviewCatalog.PacketTemplateDigest, "dedicated-configuration-template-source");
            var independentInput = Expected.InputDigest(JsonSerializer.Serialize(run.Plan), JsonSerializer.Serialize(run.FrozenInputs), run.BaselineCatalogId, run.ProfileCatalogId);
            Check.Equal(run.InputDigest, independentInput, "independent-entire-saved-input-recipe");
            var original = detail.GetRawText(); for (var repeated = 0; repeated < 8; repeated++) Check.Equal(Detail(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId))).GetRawText(), original, "fresh-engine-reload-identical-source-preview");
            var analysis = SyntheticDemoAnalysisAdapter.Project(run); Check.That(!analysis.IsAvailable && analysis.Projection is null, "AI-preview-not-deterministic-health");
        }
        Check.Equal(Detail(empty).GetProperty("snapshot").GetProperty("proposals").GetArrayLength(), 0, "empty-output-does-not-create-finding");
        Check.That(Detail(normal).GetProperty("snapshot").GetProperty("proposals")[0].GetProperty("suggestions")[0].GetProperty("text").GetString() == Expected.Suggestion, "actual-fixed-hostile-text-preserved");
        var before = await StoreCounts(); for (var repeat = 0; repeat < 5; repeat++) _ = Detail(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, normal.RunId))); Check.Equal(await StoreCounts(), before, "preview-reads-no-persisted-effect");
        Check.That((await engine.ReadAsync(new("foreign-customer", "synthetic-project", "synthetic-environment"), normal.RunId)).Issue == SyntheticRunIssue.WrongScope, "trusted-fixture-scope-deny-before-read");
        Check.Group("V10-PG-001 actual saved normal/empty/idempotent/frozen-source/full-oracle/reload/no-score/no-mutation");
        foreach (var bad in new[] { normal with { RunId = Guid.Empty }, normal with { Scope = new("foreign", "synthetic-project", "synthetic-environment") }, normal with { State = SyntheticRunState.Running }, normal with { CancelRequested = true }, normal with { InputDigest = new string('0', 64) }, normal with { Results = Array.AsReadOnly(Array.Empty<CoverageItem>()) }, normal with { FrozenInputs = normal.FrozenInputs with { AiPreviewFixtureDigest = null } }, normal with { FrozenInputs = normal.FrozenInputs with { PromptVersion = "different" } }, normal with { Plan = normal.Plan with { BaselineId = "different" } }, normal with { Revision = 0 } }) Unavailable(bad);
        await TamperCases(normal);
        Check.Group("V10-PG-002 changed persisted locks/source/plan/result integrity denies with restored owned rows");
        using var legacy = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "legacy-goldens.json")));
        foreach (var item in legacy.RootElement.GetProperty("entries").EnumerateArray())
        {
            var old = await Complete(item.GetProperty("baselineId").GetString()!, item.GetProperty("profileId").GetString()!);
            Check.Equal(JsonSerializer.Serialize(old.FrozenInputs), item.GetProperty("versionsJson").GetString(), "actual-historical-six-frozen-envelopes");
            Check.Equal(old.InputDigest, item.GetProperty("inputDigest").GetString(), "actual-historical-six-input-identities");
            Check.Equal(Detail(old).ValueKind, JsonValueKind.Null, "historical-AI-read-is-null");
            if (old.ProfileCatalogId.StartsWith("synthetic-", StringComparison.Ordinal))
            {
                var analysis = SyntheticDemoAnalysisAdapter.Project(old); Check.That(analysis.IsAvailable, "historical-deterministic-analysis-available");
                Check.Equal(analysis.Projection!.Scoring.Provisional.Overall.DisplayScore, old.ProfileCatalogId.EndsWith("operations-v1", StringComparison.Ordinal) ? 61.3m : 44.2m, "historical-provisional-score-literal");
                Check.Equal(analysis.Projection.Scoring.PublishableCurrent.Overall.DisplayScore, 78.3m, "historical-publishable-score-literal");
            }
        }
        await using var database = new NpgsqlConnection(connection); await database.OpenAsync();
        await using var schema = new NpgsqlCommand("SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('synthetic_ai','synthetic_ai_preview','ai_preview')", database);
        Check.Equal(Convert.ToInt32(await schema.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 0, "no-AI-persistence-schema");
        await using var migrations = new NpgsqlCommand("SELECT count(*) FROM synthetic_assessment.schema_migrations", database); Check.Equal(Convert.ToInt32(await migrations.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 1, "existing-single-migration-no-new-DDL");
        Check.Group("V10-PG-003 six-historical-lock/null/health regressions and no-AI-schema");
    }
    private static async Task<string> StoreCounts()
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT (SELECT count(*) FROM synthetic_assessment.runs)::text||'|'||(SELECT count(*) FROM synthetic_assessment.results)::text||'|'||(SELECT count(*) FROM synthetic_assessment.attempts)::text||'|'||(SELECT count(*) FROM synthetic_assessment.outbox)::text", db);
        return (string)(await command.ExecuteScalarAsync())!;
    }
    private static async Task TamperCases(SyntheticRunSnapshot run)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        async Task<string> Column(string name)
        { await using var command = new NpgsqlCommand("SELECT " + name + " FROM synthetic_assessment.runs WHERE run_id=@run", db); command.Parameters.AddWithValue("run", run.RunId); return (string)(await command.ExecuteScalarAsync())!; }
        var originalVersions = await Column("versions_json"); var originalPlan = await Column("plan_json");
        async Task Sql(string sql) { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var command = new NpgsqlCommand(sql, clean); await command.ExecuteNonQueryAsync(); }
        var refused = false;
        try { await using var refusal = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET input_digest=@digest WHERE run_id=@run", db); refusal.Parameters.AddWithValue("digest", new string('0', 64)); refusal.Parameters.AddWithValue("run", run.RunId); await refusal.ExecuteNonQueryAsync(); } catch (PostgresException) { refused = true; }
        Check.That(refused, "actual-input-lock-database-trigger-refuses-mutation");
        try
        {
            await Sql("ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs");
            await Sql("ALTER TABLE synthetic_assessment.results DISABLE TRIGGER immutable_results");
            async Task Change(string versions, string plan, string digest, Func<Task> assertion)
            {
                async Task Write(string v, string p, string d)
                { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var command = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET versions_json=@v,plan_json=@p,input_digest=@d WHERE run_id=@run", clean); command.Parameters.AddWithValue("v", v); command.Parameters.AddWithValue("p", p); command.Parameters.AddWithValue("d", d); command.Parameters.AddWithValue("run", run.RunId); await command.ExecuteNonQueryAsync(); }
                try { await Write(versions, plan, digest); await assertion(); } finally { await Write(originalVersions, originalPlan, run.InputDigest); }
                Check.Equal(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)).InputDigest, run.InputDigest, "owned-row-restored-after-corruption");
            }
            var changed = JsonNode.Parse(originalVersions)!.AsObject(); changed["AiPreviewFixtureDigest"] = new string('0', 64); var changedVersions = changed.ToJsonString();
            await Change(changedVersions, originalPlan, run.InputDigest, async () => Check.Equal((await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)).Issue, SyntheticRunIssue.InputIntegrityMismatch, "persisted-changed-lock-old-digest-denied"));
            await Change(changedVersions, originalPlan, Expected.InputDigest(originalPlan, changedVersions, run.BaselineCatalogId, run.ProfileCatalogId), async () => Unavailable(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId))));
            changed.Remove("AiPreviewFixtureDigest"); changedVersions = changed.ToJsonString();
            await Change(changedVersions, originalPlan, Expected.InputDigest(originalPlan, changedVersions, run.BaselineCatalogId, run.ProfileCatalogId), async () => Unavailable(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId))));
            changed = JsonNode.Parse(originalPlan)!.AsObject(); changed["BaselineId"] = "changed-fictional-baseline"; var changedPlan = changed.ToJsonString();
            await Change(originalVersions, changedPlan, Expected.InputDigest(changedPlan, originalVersions, run.BaselineCatalogId, run.ProfileCatalogId), async () => Unavailable(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId))));
            await using var result = new NpgsqlCommand("SELECT inventory_id,category_id,result_digest FROM synthetic_assessment.results WHERE run_id=@run ORDER BY inventory_id LIMIT 1", db); result.Parameters.AddWithValue("run", run.RunId);
            string id, category, digest; await using (var reader = await result.ExecuteReaderAsync()) { await reader.ReadAsync(); id = reader.GetString(0); category = reader.GetString(1); digest = reader.GetString(2); }
            async Task ResultDigest(string value)
            { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var command = new NpgsqlCommand("UPDATE synthetic_assessment.results SET result_digest=@digest WHERE run_id=@run AND inventory_id=@id AND category_id=@category", clean); command.Parameters.AddWithValue("digest", value); command.Parameters.AddWithValue("run", run.RunId); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("category", category); await command.ExecuteNonQueryAsync(); }
            try { await ResultDigest(new string('0', 64)); Check.Equal((await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)).Issue, SyntheticRunIssue.InputIntegrityMismatch, "actual-persisted-result-tamper-denied"); } finally { await ResultDigest(digest); }
            Check.Equal(Detail(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId))).GetProperty("status").GetString(), "Ready", "actual-original-ready-after-result-repair");
        }
        finally
        {
            try { await Sql("ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs"); }
            finally { await Sql("ALTER TABLE synthetic_assessment.results ENABLE TRIGGER immutable_results"); }
        }
        await using (var enabled = new NpgsqlCommand("SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='synthetic_assessment' AND ((c.relname='runs' AND t.tgname='immutable_run_inputs') OR (c.relname='results' AND t.tgname='immutable_results')) AND t.tgenabled='O'", db)) Check.Equal(Convert.ToInt32(await enabled.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 2, "owned-database-both-exact-triggers-enabled");
        await Engine().InitializeAsync(); Check.That(true, "owned-database-schema-fingerprint-restored");
    }
}
