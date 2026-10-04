using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using FindingReview;
using Npgsql;

internal static class DatabaseCases
{
    internal const string DatabaseName = "iga_synthetic_cycle12_v12";
    private static string connection = "";
    private static SyntheticDurableRunEngine Engine() => new(connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(5), 2));
    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult value)
    { Check.That(value.Succeeded && value.Snapshot is not null, "saved-run-accepted"); return value.Snapshot!; }
    private static JsonObject Analysis(SyntheticRunSnapshot run, DemoReviewContext context) => JsonSerializer.SerializeToNode(DemoAnalysisProjection.Detail(run, context), V12Program.Web)!.AsObject();
    private static DemoRecommendationGuidanceDetail Captured(SyntheticRunSnapshot run, DemoReviewContext context)
    {
        var states = context.Snapshot?.Findings.ToDictionary(f => f.Seed.FindingId, f => Enum.Parse<AssessmentScoring.ScoringFindingState>(f.Current.State.ToString()), StringComparer.Ordinal);
        return DemoRecommendationGuidanceProjection.Detail(run, SyntheticDemoAnalysisAdapter.Project(run, states, context.Snapshot?.SnapshotDigest), context);
    }
    private static string Artifacts(JsonObject fix) => Expected.Canonical(fix["snapshot"]!["packages"]);
    private static void Capture(SyntheticRunSnapshot run, DemoReviewContext context, JsonObject analysis, string stage)
    {
        var evidence = new JsonObject
        {
            ["schema"] = "v12-observed-synthetic-source-v1",
            ["stage"] = stage,
            ["savedRun"] = JsonSerializer.SerializeToNode(run, V12Program.SourceWeb),
            ["capturedReview"] = JsonSerializer.SerializeToNode(context, V12Program.SourceWeb),
            ["returnedAnalysis"] = analysis.DeepClone()
        };
        var target = Path.Combine(AppContext.BaseDirectory, "v12-observed-" + stage + "-" + run.RunId.ToString("D") + ".json");
        if (File.Exists(target)) throw new InvalidOperationException("observed-source-capture-already-exists");
        File.WriteAllText(target, evidence.ToJsonString(V12Program.SourceWeb));
    }
    private static void Verify(SyntheticRunSnapshot run, DemoReviewContext review, JsonObject analysis)
    {
        var fix = analysis["fixPackages"]!.AsObject();
        Check.That(fix.Select(p => p.Key).Order().SequenceEqual(new[] { "schemaVersion", "runId", "runRevision", "runInputDigest", "baselineId", "profileId", "status", "reasonCode", "snapshot" }.Order()), "exact-nine-field-fix-DTO");
        Check.Equal(fix["schemaVersion"]!.GetValue<string>(), Expected.Schema, "schema-exact");
        Check.Equal(fix["runId"]!.GetValue<string>(), run.RunId.ToString("D"), "DTO-actual-saved-run");
        Check.Equal(fix["runRevision"]!.GetValue<long>(), run.Revision, "DTO-actual-saved-revision");
        Check.Equal(fix["runInputDigest"]!.GetValue<string>(), run.InputDigest, "DTO-actual-saved-input-lock");
        Check.Equal(fix["baselineId"]!.GetValue<string>(), run.BaselineCatalogId, "DTO-actual-baseline");
        Check.Equal(fix["profileId"]!.GetValue<string>(), Expected.Profile, "DTO-opt-in-only");
        Check.Equal(run.InputDigest, Expected.InputDigest(JsonSerializer.Serialize(run.Plan), JsonSerializer.Serialize(run.FrozenInputs), run.BaselineCatalogId, run.ProfileCatalogId), "entire-saved-input-independent-recipe");
        var guidance = analysis["recommendationGuidance"]!.AsObject();
        if (guidance["status"]!.GetValue<string>() != "Ready")
        {
            Check.Equal(fix["status"]!.GetValue<string>(), "Unavailable", "upstream-unavailable-never-ready");
            Check.That(fix["snapshot"] is null && !string.IsNullOrWhiteSpace(fix["reasonCode"]!.GetValue<string>()), "unavailable-no-partial-package");
            return;
        }
        Check.Equal(fix["status"]!.GetValue<string>(), "Ready", "actual-composition-ready");
        Check.That(fix["reasonCode"] is null, "ready-no-denial");
        var captured = guidance["snapshot"]!.AsObject(); var snapshot = fix["snapshot"]!.AsObject();
        var payload = Expected.FixPayload(captured); var canonical = Expected.Canonical(payload);
        Check.Equal(snapshot["canonicalJson"]!.GetValue<string>(), canonical, "full-independent-source-template-package-byte-recipe");
        Check.Equal(snapshot["contentDigest"]!.GetValue<string>(), Expected.Hash(canonical), "full-independent-package-content-digest");
        var actual = (JsonObject)snapshot.DeepClone(); actual.Remove("canonicalJson"); actual.Remove("contentDigest");
        Check.Equal(Expected.Canonical(actual), canonical, "every-actual-snapshot-property-matches-canonical");
        Check.Equal(Expected.Canonical(snapshot["guidance"]), Expected.Canonical(captured), "one-captured-complete-guidance-value");
        var source = captured["source"]!.AsObject();
        Check.Equal(source["reviewSnapshotDigest"]!.GetValue<string>(), review.Snapshot!.SnapshotDigest, "current-review-digest-same-capture");
        Check.Equal(source["reviewRunRevision"]!.GetValue<long>(), run.Revision, "review-source-run-revision");
        Check.Equal(source["profileId"]!.GetValue<string>(), Expected.Profile, "source-profile-unrelabeled");
        Check.Equal(source["runInputDigest"]!.GetValue<string>(), run.InputDigest, "source-original-durable-lock");
        Check.Equal(Expected.Canonical(source["frozenVersions"]), Expected.Canonical(JsonSerializer.SerializeToNode(run.FrozenInputs, V12Program.Web)), "complete-twelve-field-source-lock");
        Check.Equal(Expected.Canonical(source["capabilityLock"]), Expected.Canonical(JsonSerializer.SerializeToNode(run.Plan.CapabilityLock, V12Program.SourceWeb)), "actual-capability-lock");
        foreach (var finding in captured["findings"]!.AsArray())
        {
            var current = review.Snapshot.Findings.Single(f => f.Seed.FindingId == finding!["findingId"]!.GetValue<string>());
            Check.Equal(finding!["findingRevision"]!.GetValue<long>(), current.Current.Revision, "per-finding-current-revision");
            Check.Equal(finding["currentState"]!.GetValue<string>(), current.Current.State.ToString(), "per-finding-current-state");
            Check.Equal(finding["presentationTitle"]!.GetValue<string>(), current.Current.PresentationTitle, "current-presentation-title");
            Check.Equal(finding["originalTitle"]!.GetValue<string>(), current.Seed.OriginalTitle, "immutable-original-title");
        }
        Check.Equal(snapshot["status"]!.GetValue<string>(), "Unverified", "review-never-approves-artifacts");
    }
    private static void Unavailable(SyntheticRunSnapshot run, DemoRecommendationGuidanceDetail? guidance, DemoReviewContext? review)
    {
        var detail = JsonSerializer.SerializeToNode(DemoFixPackageProjection.Detail(run, guidance, review), V12Program.Web)!.AsObject();
        Check.Equal(detail["status"]!.GetValue<string>(), "Unavailable", "bad-source-denied");
        Check.That(detail["snapshot"] is null && !string.IsNullOrWhiteSpace(detail["reasonCode"]!.GetValue<string>()), "bad-source-no-partial-fallback");
    }
    internal static async Task Run()
    {
        connection = Environment.GetEnvironmentVariable("IGA_FIX_PACKAGE_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle12_v12;Username=iga_synthetic";
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Port != 55433 || settings.Database != DatabaseName) throw new InvalidOperationException("database-guard");
        Check.Last = "owned-database-admin-open";
        await using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Database = "postgres" }.ConnectionString))
        {
            await admin.OpenAsync(); await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname='iga_synthetic_cycle12_v12'", admin);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
            { await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_cycle12_v12", admin); await create.ExecuteNonQueryAsync(); }
        }
        Check.Last = "owned-database-initialize-engine";
        var engine = Engine(); await engine.InitializeAsync(); var store = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed); await store.InitializeAsync(); var reviews = new DemoReviewService(store);
        async Task<SyntheticRunSnapshot> Complete(string baseline, string profile)
        {
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, Guid.NewGuid().ToString("D"));
            var initial = Accept(await engine.StartAsync(request)); var replay = await engine.StartAsync(request);
            Check.That(replay.AlreadyApplied && replay.Snapshot!.RunId == initial.RunId, "actual-start-idempotency");
            if (profile == Expected.Profile) Unavailable(initial, null, null);
            var leased = Accept(await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, initial.RunId, "v12-independent-verifier"));
            var results = DemoFixtureCatalog.Baselines.Single(b => b.Id == baseline).ScriptedResults;
            var checkpoint = Accept(await engine.CheckpointAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease!.Generation, leased.Revision, results));
            return Accept(await engine.CompleteCoverageAsync(DemoFixtureCatalog.Scope, initial.RunId, leased.Lease.Generation, checkpoint.Revision));
        }
        SyntheticRunSnapshot? normal = null; DemoReviewContext? normalContext = null;
        foreach (var preset in Expected.Presets)
        {
            var run = await Complete(preset, Expected.Profile); var context = await reviews.ReadAsync(run); var detail = Analysis(run, context);
            Verify(run, context, detail);
            Capture(run, context, detail, preset);
            if (preset.EndsWith("healthy-v1", StringComparison.Ordinal)) Check.Equal(detail["fixPackages"]!["snapshot"]!["packages"]!.AsArray().Count, 0, "healthy-empty-package-explicit");
            if (preset.EndsWith("findings-v1", StringComparison.Ordinal)) { normal = run; normalContext = context; }
            var frozen = Expected.Canonical(detail); var counts = await Counts();
            for (var i = 0; i < 3; i++)
            {
                var fresh = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId));
                var freshStore = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed); var freshReview = await new DemoReviewService(freshStore).ReadAsync(fresh);
                Check.Equal(Expected.Canonical(Analysis(fresh, freshReview)), frozen, "fresh-engine-review-store-restart-identical-detached-view");
            }
            Check.Equal(await Counts(), counts, "repeat-package-read-no-persisted-artifact-effect");
        }
        var currentRun = normal!; var currentReview = normalContext!; var beforeDetail = Analysis(currentRun, currentReview); var beforeFix = beforeDetail["fixPackages"]!.AsObject();
        var initialArtifacts = Artifacts(beforeFix); var originals = currentReview.Snapshot!.Findings.Select(f => Expected.Canonical(JsonSerializer.SerializeToNode(f.Seed, V12Program.Web))).ToArray();
        var first = currentReview.Snapshot.Findings.First(f => f.Current.State == SyntheticFindingState.Proposed);
        foreach (var kind in new[] { SyntheticReviewEventKind.Comment, SyntheticReviewEventKind.EditPresentation, SyntheticReviewEventKind.Confirm })
        {
            var oldReview = currentReview; var oldDigest = beforeFix["snapshot"]!["contentDigest"]!.GetValue<string>();
            var finding = currentReview.Snapshot!.Findings.Single(f => f.Seed.FindingId == first.Seed.FindingId);
            var command = new SyntheticReviewCommand(Guid.NewGuid(), finding.Current.Revision, kind, kind == SyntheticReviewEventKind.Confirm ? "Fictional verification reason" : null, kind == SyntheticReviewEventKind.Comment ? "Fictional review note" : null, kind == SyntheticReviewEventKind.EditPresentation ? "Fictional reviewed title café 中文 😀" : null, kind == SyntheticReviewEventKind.EditPresentation ? "Fictional current context" : null);
            var applied = await reviews.ApplyAsync(currentRun.RunId, finding.Seed.FindingId, command); Check.That(applied.Succeeded, "actual-current-finding-review-event");
            var replay = await reviews.ApplyAsync(currentRun.RunId, finding.Seed.FindingId, command); Check.That(replay.Succeeded && replay.AlreadyApplied, "actual-review-event-idempotency");
            currentRun = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, currentRun.RunId)); currentReview = await reviews.ReadAsync(currentRun);
            var newDetail = Analysis(currentRun, currentReview); Verify(currentRun, currentReview, newDetail);
            Capture(currentRun, currentReview, newDetail, "review-" + kind.ToString());
            Check.Equal(currentRun.Revision, normal!.Revision, "finding-review-does-not-change-run-revision");
            Check.That(currentReview.Snapshot!.SnapshotDigest != oldReview.Snapshot!.SnapshotDigest, "same-run-revision-new-current-review-digest");
            var newFix = newDetail["fixPackages"]!.AsObject(); Check.That(newFix["snapshot"]!["contentDigest"]!.GetValue<string>() != oldDigest, "new-review-derives-new-package-content-digest");
            Check.Equal(Artifacts(newFix), initialArtifacts, "fixed-artifact-identity-text-status-invariant-after-review");
            Check.That(currentReview.Snapshot.Findings.Select(f => Expected.Canonical(JsonSerializer.SerializeToNode(f.Seed, V12Program.Web))).SequenceEqual(originals), "finding-originals-and-occurrences-immutable");
            var captured = Captured(currentRun, currentReview);
            Unavailable(currentRun, captured, oldReview);
            beforeFix = newFix;
        }
        var actualAnalysis = SyntheticDemoAnalysisAdapter.Project(currentRun).Projection!;
        Check.Equal(actualAnalysis.Scoring.Provisional.Overall.DisplayScore, 44.2m, "equal-health-provisional-remains-literal");
        var matchingAnalysis = SyntheticDemoAnalysisAdapter.Project(currentRun, currentReview.Snapshot!.Findings.ToDictionary(f => f.Seed.FindingId, f => Enum.Parse<AssessmentScoring.ScoringFindingState>(f.Current.State.ToString())), currentReview.Snapshot.SnapshotDigest);
        Check.That(matchingAnalysis.IsAvailable, "current-health-overlay-preserved");
        Check.Group("V12-PG source/current-review/reload/no-artifact-storage/original invariants");
        var ready = Analysis(currentRun, currentReview); var guidance = Captured(currentRun, currentReview);
        foreach (var bad in new[] { currentRun with { RunId = Guid.Empty }, currentRun with { Scope = new("foreign", "synthetic-project", "synthetic-environment") }, currentRun with { State = SyntheticRunState.Running }, currentRun with { CancelRequested = true }, currentRun with { InputDigest = new string('0', 64) }, currentRun with { Results = Array.AsReadOnly(Array.Empty<CoverageItem>()) }, currentRun with { FrozenInputs = currentRun.FrozenInputs with { FixPackageTemplateDigest = null } }, currentRun with { FrozenInputs = currentRun.FrozenInputs with { FixPackageTemplateDigest = new string('0', 64) } }, currentRun with { Plan = currentRun.Plan with { BaselineId = "foreign" } }, currentRun with { Revision = 0 } }) Unavailable(bad, guidance, currentReview);
        Unavailable(currentRun, null, currentReview); Unavailable(currentRun, guidance, null);
        var foreign = await Complete("synthetic-analysis-findings-v1", Expected.Profile); Unavailable(currentRun, guidance, await reviews.ReadAsync(foreign));
        await Tamper(currentRun, currentReview);
        using var oldLocks = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "historical-six-locks.json")));
        foreach (var item in oldLocks.RootElement.GetProperty("entries").EnumerateArray())
        {
            var historical = await Complete(item.GetProperty("baselineId").GetString()!, item.GetProperty("profileId").GetString()!);
            Check.Equal(JsonSerializer.Serialize(historical.FrozenInputs), item.GetProperty("versionsJson").GetString(), "actual-old-six-full-byte-lock-envelopes");
            Check.Equal(historical.InputDigest, item.GetProperty("inputDigest").GetString(), "actual-old-six-input-identities");
            var detail = Analysis(historical, await reviews.ReadAsync(historical)); Check.That(detail.ContainsKey("fixPackages") && detail["fixPackages"] is null, "actual-historical-required-null-fix-field");
        }
        foreach (var profile in new[] { "profile-ai-preview-v1", "profile-ai-preview-empty-v1" })
        {
            var historical = await Complete("baseline-ai-configuration-v1", profile); var detail = Analysis(historical, await reviews.ReadAsync(historical));
            Check.That(detail["fixPackages"] is null && detail["aiPreview"]!["status"]!.GetValue<string>() == "Ready", "old-AI-preview-retained-with-null-fix");
        }
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var schema = new NpgsqlCommand("SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('synthetic_fix_packages','fix_packages','synthetic_artifacts')", db);
        Check.Equal(Convert.ToInt32(await schema.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 0, "no-fix-package-persistence-schema");
        foreach (var schemaName in new[] { "synthetic_assessment", "synthetic_review" })
        { await using var migration = new NpgsqlCommand("SELECT count(*) FROM " + schemaName + ".schema_migrations", db); Check.Equal(Convert.ToInt32(await migration.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 1, "existing-single-migration-no-new-DDL"); }
        Check.Group("V12-PG negative durable sources/tamper/historical-eight/schema invariants");
    }
    private static async Task<string> Counts()
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT (SELECT count(*) FROM synthetic_assessment.runs)::text||'|'||(SELECT count(*) FROM synthetic_assessment.results)::text||'|'||(SELECT count(*) FROM synthetic_review.events)::text", db);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
    private static async Task Tamper(SyntheticRunSnapshot run, DemoReviewContext review)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using (var guard = new NpgsqlCommand("SELECT current_database()", db)) Check.Equal((string)(await guard.ExecuteScalarAsync())!, DatabaseName, "owned-database-before-bounded-tamper");
        async Task<string> Column(string column) { await using var c = new NpgsqlCommand("SELECT " + column + " FROM synthetic_assessment.runs WHERE run_id=@run", db); c.Parameters.AddWithValue("run", run.RunId); return (string)(await c.ExecuteScalarAsync())!; }
        var versions = await Column("versions_json"); var plan = await Column("plan_json");
        async Task Sql(string sql) { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var c = new NpgsqlCommand(sql, clean); await c.ExecuteNonQueryAsync(); }
        async Task Write(string v, string p, string d)
        { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var c = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET versions_json=@v,plan_json=@p,input_digest=@d WHERE run_id=@run", clean); c.Parameters.AddWithValue("v", v); c.Parameters.AddWithValue("p", p); c.Parameters.AddWithValue("d", d); c.Parameters.AddWithValue("run", run.RunId); await c.ExecuteNonQueryAsync(); }
        var refused = false; try { await Write(versions, plan, new string('0', 64)); } catch (PostgresException) { refused = true; }
        Check.That(refused, "immutable-input-trigger-denies-real-update");
        var inputDisabled = false;
        try
        {
            await Sql("ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs"); inputDisabled = true;
            await Sql("ALTER TABLE synthetic_assessment.results DISABLE TRIGGER immutable_results");
            async Task Change(string v, string p, bool oldDigest)
            {
                try
                {
                    await Write(v, p, oldDigest ? run.InputDigest : Expected.InputDigest(p, v, run.BaselineCatalogId, run.ProfileCatalogId));
                    var read = await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId);
                    if (oldDigest) Check.Equal(read.Issue, SyntheticRunIssue.InputIntegrityMismatch, "persisted-old-digest-corruption-denied");
                    else if (!read.Succeeded) Check.That(read.Issue is SyntheticRunIssue.InputIntegrityMismatch or SyntheticRunIssue.InvalidInput, "persisted-invalid-input-denied");
                    else
                    {
                        Check.Last = "persisted-coherent-source-analysis-projection";
                        var deniedContext = await new DemoReviewService(new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed)).ReadAsync(read.Snapshot!);
                        var analysis = Analysis(read.Snapshot!, deniedContext); Check.Equal(analysis["fixPackages"]!["status"]!.GetValue<string>(), "Unavailable", "persisted-coherently-rehashed-wrong-source-unavailable");
                    }
                }
                finally { await Write(versions, plan, run.InputDigest); }
                Check.Equal(Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)).InputDigest, run.InputDigest, "owned-original-row-restored");
            }
            var altered = JsonNode.Parse(versions)!.AsObject(); altered["FixPackageTemplateDigest"] = new string('0', 64); await Change(altered.ToJsonString(), plan, true); await Change(altered.ToJsonString(), plan, false);
            altered.Remove("FixPackageTemplateDigest"); await Change(altered.ToJsonString(), plan, false);
            altered = JsonNode.Parse(versions)!.AsObject(); altered["ApplicationVersion"] = "foreign-application"; await Change(altered.ToJsonString(), plan, false);
            altered = JsonNode.Parse(plan)!.AsObject(); altered["BaselineId"] = "foreign-baseline"; await Change(versions, altered.ToJsonString(), false);
            await using var query = new NpgsqlCommand("SELECT inventory_id,category_id,result_digest FROM synthetic_assessment.results WHERE run_id=@run ORDER BY inventory_id LIMIT 1", db); query.Parameters.AddWithValue("run", run.RunId);
            string id, category, digest; await using (var reader = await query.ExecuteReaderAsync()) { Check.That(await reader.ReadAsync(), "saved-result-for-tamper"); id = reader.GetString(0); category = reader.GetString(1); digest = reader.GetString(2); }
            async Task Result(string value) { await using var clean = new NpgsqlConnection(connection); await clean.OpenAsync(); await using var c = new NpgsqlCommand("UPDATE synthetic_assessment.results SET result_digest=@d WHERE run_id=@run AND inventory_id=@id AND category_id=@category", clean); c.Parameters.AddWithValue("d", value); c.Parameters.AddWithValue("run", run.RunId); c.Parameters.AddWithValue("id", id); c.Parameters.AddWithValue("category", category); await c.ExecuteNonQueryAsync(); }
            try { await Result(new string('0', 64)); Check.Equal((await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)).Issue, SyntheticRunIssue.InputIntegrityMismatch, "actual-result-digest-corruption-denied"); } finally { await Result(digest); }
        }
        finally
        {
            try { if (inputDisabled) await Write(versions, plan, run.InputDigest); }
            finally { try { await Sql("ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs"); } finally { await Sql("ALTER TABLE synthetic_assessment.results ENABLE TRIGGER immutable_results"); } }
        }
        await using (var enabled = new NpgsqlCommand("SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='synthetic_assessment' AND ((c.relname='runs' AND t.tgname='immutable_run_inputs') OR (c.relname='results' AND t.tgname='immutable_results')) AND t.tgenabled='O'", db)) Check.Equal(Convert.ToInt32(await enabled.ExecuteScalarAsync(), CultureInfo.InvariantCulture), 2, "both-owned-triggers-enabled-after-tamper");
        await Engine().InitializeAsync(); var restored = Accept(await Engine().ReadAsync(DemoFixtureCatalog.Scope, run.RunId)); var restoredDetail = Analysis(restored, review); Verify(restored, review, restoredDetail); Capture(restored, review, restoredDetail, "restored-source");
    }
}
