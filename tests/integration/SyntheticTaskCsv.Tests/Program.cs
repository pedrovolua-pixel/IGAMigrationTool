using System.Collections.Immutable;
using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;
using SyntheticTaskCsv;
using SyntheticTaskCsvIntegration;

var checks = 0;
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
var golden = CsvCodec.Parse(File.ReadAllText(Path.Combine(root, "tests/unit/SyntheticTaskCsv.Tests/Fixtures/golden-envelope.json"))).Envelope!;
var empty = CsvCodec.Freeze(golden with { Rows = [], SelectedAttestations = [] }).Envelope!;
var renderer = Path.Combine(root, "src/server/workers/SyntheticCsvRenderer/bin/Release/net10.0");
var rendered = await new CsvSandboxRunner(renderer).RenderAsync(golden);
Assert(rendered.Succeeded && rendered.Bytes!.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "tests/unit/SyntheticTaskCsv.Tests/Fixtures/golden-one-row.csv"))), "actual nonroot mac sandbox renderer full independent byte parity");
var probe = Path.Combine(root, "tests/integration/SyntheticTaskCsv.Tests/Probe/bin/Release/net10.0");
var probeRoot = Path.Combine(root, "tests/integration/SyntheticTaskCsv.Tests/bin/probes");
File.WriteAllText("/private/tmp/iga-csv-credential-marker", "fictional-secret-marker");
Environment.SetEnvironmentVariable("CSV_TEST_SECRET", "fictional-parent-marker");
try
{
    foreach (var mode in new[] { "network", "write", "read", "fork", "env", "timeout", "output", "stderr", "memory", "rss" })
    {
        var directory = Path.Combine(probeRoot, "probe-" + mode); Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(probe, "*")) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), true);
        var watch = System.Diagnostics.Stopwatch.StartNew(); var result = await new CsvSandboxRunner(directory).RenderAsync(empty);
        Assert(mode is "timeout" or "output" or "stderr" or "memory" or "rss" ? !result.Succeeded : result.Succeeded, "isolation probe " + mode);
        if (mode == "timeout") Assert(watch.Elapsed.TotalSeconds < 12, "wall timeout bounded");
    }
    Assert(!File.Exists("/private/tmp/iga-csv-probe-forbidden-output"), "no unauthorized file created");
}
finally { File.Delete("/private/tmp/iga-csv-credential-marker"); Environment.SetEnvironmentVariable("CSV_TEST_SECRET", null); }
var authority = new PlanningTaskExportAuthority("synthetic-auditor", true, true, false, true, PlanningTaskScope.Fixed, PlanningTaskExportRole.Auditor, ["SECURITY", "OPERATIONS"], true, true, true, true);
Assert(PlanningTaskExportPolicy.Authorize(authority, PlanningTaskScope.Fixed) is null, "genuine scoped Auditor export authorized");
foreach (var denied in new[] { authority with { AuditorScopedExportGranted = false }, authority with { TaskExportGranted = false }, authority with { CustomerExportAllowed = false }, authority with { Revoked = true }, authority with { AssignmentActive = false }, authority with { ResourceAvailable = false }, authority with { Categories = ["SECURITY"] } })
    Assert(PlanningTaskExportPolicy.Authorize(denied, PlanningTaskScope.Fixed, "OPERATIONS") is not null, "export authorization denied independently");
Assert(CsvExportCaptureAdapter.Recheck(golden, golden, authority) is null, "final exact capture authority recheck");
Assert(CsvExportCaptureAdapter.Recheck(golden, empty, authority) == CsvIssue.SourceConflict, "snapshot change prevents first byte");
Assert(CsvExportCaptureAdapter.Recheck(golden, golden, authority with { Revoked = true }) == CsvIssue.Denied, "revocation prevents first byte");
var artifactAuthority = new ArtifactExportAuthority("synthetic-auditor", true, true, false, true, ArtifactReviewScope.Fixed, ArtifactExportRole.Auditor, ["SECURITY", "OPERATIONS"], true, true, true, true);
Assert(ArtifactExportPolicy.Authorize(artifactAuthority, ArtifactReviewScope.Fixed) is null, "owning artifact Auditor capture policy");
Assert(ArtifactReviewPolicy.Authorize(new("synthetic-auditor", true, true, false, true, ArtifactReviewScope.Fixed, [ArtifactReviewRole.Auditor], ["SECURITY", "OPERATIONS"], [ArtifactReviewAction.Read], ArtifactReviewResourceState.Mutable), ArtifactReviewScope.Fixed) is not null, "historical artifact Consultant policy remains closed to Auditor");

var row = golden.Rows[0]; var binding = golden.CurrentSourceBinding; var versions = golden.Versions;
var artifactBinding = new ArtifactReviewSourceBinding(ArtifactReviewScope.Fixed, golden.RunId, binding.RunRevision, versions.RunInputDigest, versions.BaselineId,
    versions.ProfileId, versions.FrozenVersions.ApplicationVersion, versions.FrozenVersions.FixReviewContractDigest, binding.SourceDigest, binding.GuidanceDigest,
    binding.FindingReviewDigest, "synthetic-fix-package-template-v1", versions.FrozenVersions.FixPackageTemplateDigest,
    binding.FindingRevisions.Select(item => new ArtifactReviewFindingRevision(item.FindingId, item.Revision)).ToImmutableArray());
var exportRow = new PlanningTaskExportRow(new(row.TaskId, row.FindingId, "SECURITY", row.PackageId, row.ScopedOptionId, golden.SelectedAttestations.Select(item => item.ArtifactId).ToImmutableArray()),
    row.AssigneeId, row.TaskRevision, PlanningTaskStatus.Completed, PlanningTaskFreshness.NeedsReconfirmation, DateTimeOffset.Parse(row.CreatedAtUtc), DateTimeOffset.Parse(row.PlannedAtUtc), row.PlannedSourceDigest,
    golden.SelectedAttestations.Select(item => new PlanningTaskAttestation(item.ArtifactId, item.Revision, item.EventId, null, ArtifactReviewState.Unverified, null)).ToImmutableArray());
var owningCapture = new PlanningTaskExportCapture(PlanningTaskScope.Fixed, golden.RunId, new(artifactBinding, versions.FrozenVersions.PlanningTaskContractDigest), [exportRow]);
var adapted = CsvExportCaptureAdapter.CreateEnvelope(owningCapture, versions);
Assert(adapted.Succeeded && CsvCanonical.Json(adapted.Envelope) == CsvCanonical.Json(golden), "adapter preserves full literal metadata source/vector/row binding");
Assert(!CsvExportCaptureAdapter.CreateEnvelope(owningCapture, versions with { RunInputDigest = new string('f', 64) }).Succeeded, "adapter denies mismatched run/version locks");
var ignoredProof = new ArtifactExportCapture(new string('a', 64), [], new(artifactBinding, "secret-history-marker", []));
Assert(!System.Text.Json.JsonSerializer.Serialize(ignoredProof).Contains("secret-history-marker", StringComparison.Ordinal), "artifact full verification proof never serialized");
var minimized = CsvCanonical.Json(adapted.Envelope);
Assert(!minimized.Contains("reason", StringComparison.OrdinalIgnoreCase) && !minimized.Contains("comment", StringComparison.OrdinalIgnoreCase) && !minimized.Contains("artifactText", StringComparison.Ordinal), "renderer envelope omits reasons comments and artifacts");
const string connectionString = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_phase1b_csv_tests;Username=iga_synthetic";
var auditStore = new CsvAuditStore(connectionString); await auditStore.InitializeAsync(); await auditStore.InitializeAsync();
await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();

var taskStore = SyntheticPlanningTaskStore.CreateForPhase1B(connectionString, PlanningTaskScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningTaskSourceResult(PlanningTaskIssue.SourceUnavailable, null)));
await taskStore.InitializeAsync();
await using (var tx = await connection.BeginTransactionAsync())
{
    var calls = 0;
    Task<PlanningTaskSourceResult> Capture(NpgsqlConnection supplied, NpgsqlTransaction transaction, Guid run, PlanningTaskExportAuthority actor, CancellationToken token)
    {
        calls++; Assert(ReferenceEquals(supplied, connection) && ReferenceEquals(transaction, tx) && run == golden.RunId && actor.Role == PlanningTaskExportRole.Auditor,
            "owning capture receives same caller transaction and genuine Auditor");
        return Task.FromResult(new PlanningTaskSourceResult(PlanningTaskIssue.SourceUnavailable, null));
    }
    var unavailable = await taskStore.CaptureForExportInTransactionAsync(connection, tx, golden.RunId, authority, Capture);
    Assert(unavailable.Issue == PlanningTaskIssue.SourceUnavailable && unavailable.Capture is null, "owning unavailable source denies complete export");
    var denied = await taskStore.CaptureForExportInTransactionAsync(connection, tx, golden.RunId, authority with { AuditorScopedExportGranted = false }, Capture);
    Assert(denied.Issue == PlanningTaskIssue.Denied && calls == 1, "owning permission denied before source content capture");
    Assert(tx.Connection == connection, "owning capture never commits caller transaction");
    await tx.RollbackAsync();
}
try { new SyntheticPlanningTaskStore(connectionString, PlanningTaskScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningTaskSourceResult(PlanningTaskIssue.SourceUnavailable, null))); throw new Exception("historical constructor widened"); }
catch (ArgumentException) { Assert(true, "old constructor database guard unchanged"); }
try { SyntheticPlanningTaskStore.CreateForPhase1B(connectionString.Replace("55433", "55434", StringComparison.Ordinal), PlanningTaskScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningTaskSourceResult(PlanningTaskIssue.SourceUnavailable, null))); throw new Exception("new constructor port widened"); }
catch (ArgumentException) { Assert(true, "new combined factory fixed database port"); }
await using (var count = new NpgsqlCommand("SELECT (SELECT count(*) FROM synthetic_planning_tasks.task_seeds)+(SELECT count(*) FROM synthetic_planning_tasks.events)+(SELECT count(*) FROM synthetic_planning_tasks.source_versions)", connection))
    Assert((long)(await count.ExecuteScalarAsync())! == 0, "read-only denied capture seeds no task/source/history rows");
var request = Guid.NewGuid(); var start = DateTimeOffset.UtcNow;
var stages = new[] { CsvAuditStage.Request, CsvAuditStage.Dispatch, CsvAuditStage.Render, CsvAuditStage.Delivery };
var output = CsvCanonical.Hash(rendered.Bytes!);
foreach (var (stage, index) in stages.Select((stage, index) => (stage, index)))
{
    var audit = new CsvAuditEvent(Guid.NewGuid(), request, golden.RunId, "synthetic-consultant", CsvScope.Fixed, start.AddTicks(index), stage, CsvAuditReason.None, golden.SnapshotDigest, stage is CsvAuditStage.Render or CsvAuditStage.Delivery ? output : null, 1);
    await using var tx = await connection.BeginTransactionAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, audit) is null, "audit " + stage + " supplied transaction");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, audit) is null, "exact audit replay " + stage);
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, audit with { ActorId = "other-consultant" }) == CsvIssue.IntegrityMismatch, "audit semantic UUID conflict");
    await tx.CommitAsync();
}
// A cancelled body write leaves the immutable authorization record intact and appends one bounded marker.
var interrupted = new CsvAuditEvent(Guid.NewGuid(), request, golden.RunId, "synthetic-consultant", CsvScope.Fixed,
    start.AddSeconds(1), CsvAuditStage.Denial, CsvAuditReason.TransferInterrupted, golden.SnapshotDigest, output, 1);
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    try { await using var body = new MemoryStream(); await body.WriteAsync(rendered.Bytes!, cancellation.Token); throw new Exception("cancelled transfer accepted"); }
    catch (OperationCanceledException) { Assert(true, "cancelled standalone body write triggers terminal audit path"); }
}
await using (var tx = await connection.BeginTransactionAsync())
{
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { RequestId = Guid.NewGuid() }) == CsvIssue.IntegrityMismatch, "interruption denied before authorized Delivery");
    foreach (var incomplete in new[] { interrupted with { SnapshotDigest = null }, interrupted with { OutputSha256 = null }, interrupted with { RowCount = null } })
        Assert(await auditStore.AppendInTransactionAsync(connection, tx, incomplete) == CsvIssue.InvalidInput, "interruption requires all original metadata bindings");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { Reason = CsvAuditReason.Denied }) == CsvIssue.IntegrityMismatch, "ordinary denial cannot follow Delivery");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { SnapshotDigest = new string('f', 64) }) == CsvIssue.SourceConflict, "interruption rejects changed snapshot");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { OutputSha256 = new string('f', 64) }) == CsvIssue.IntegrityMismatch, "interruption rejects changed output");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { RowCount = 2 }) == CsvIssue.IntegrityMismatch, "interruption rejects changed row count");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted) is null, "single interrupted-transfer metadata marker appended after Delivery");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted) is null, "exact interrupted-transfer marker replay accepted");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { EventId = Guid.NewGuid() }) == CsvIssue.IntegrityMismatch, "second interrupted-transfer marker denied");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted with { Reason = CsvAuditReason.SourceConflict }) == CsvIssue.IntegrityMismatch, "interrupted marker UUID content conflict denied");
    await tx.CommitAsync();
}
await using (var count = new NpgsqlCommand("SELECT count(*),count(*) FILTER(WHERE stage='Delivery'),count(*) FILTER(WHERE reason='TransferInterrupted') FROM synthetic_task_csv.export_audit WHERE request_id=@request", connection))
{
    count.Parameters.AddWithValue("request", request); await using var reader = await count.ExecuteReaderAsync(); await reader.ReadAsync();
    Assert(reader.GetInt64(0) == 5 && reader.GetInt64(1) == 1 && reader.GetInt64(2) == 1, "Delivery authorization remains with exactly one terminal interruption marker");
}
await using (var tx = await connection.BeginTransactionAsync())
{
    await using var corrupt = new NpgsqlCommand("ALTER TABLE synthetic_task_csv.export_audit DISABLE TRIGGER export_audit_immutable; UPDATE synthetic_task_csv.export_audit SET event_digest=repeat('0',64) WHERE request_id=@request AND stage='Delivery'; ALTER TABLE synthetic_task_csv.export_audit ENABLE TRIGGER export_audit_immutable", connection, tx);
    corrupt.Parameters.AddWithValue("request", request); await corrupt.ExecuteNonQueryAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, interrupted) == CsvIssue.IntegrityMismatch, "interruption replay verifies prior Delivery proof");
    await tx.RollbackAsync();
}
await using (var tx = await connection.BeginTransactionAsync())
{
    var changed = interrupted with { RecordedAtUtc = new DateTimeOffset(interrupted.RecordedAtUtc.Ticks - interrupted.RecordedAtUtc.Ticks % 10, TimeSpan.Zero), Reason = CsvAuditReason.Denied };
    await using var corrupt = new NpgsqlCommand("ALTER TABLE synthetic_task_csv.export_audit DISABLE TRIGGER export_audit_immutable; UPDATE synthetic_task_csv.export_audit SET reason='Denied',event_digest=@digest WHERE event_id=@event; ALTER TABLE synthetic_task_csv.export_audit ENABLE TRIGGER export_audit_immutable", connection, tx);
    corrupt.Parameters.AddWithValue("digest", CsvCanonical.Hash(CsvCanonical.Bytes(changed))); corrupt.Parameters.AddWithValue("event", interrupted.EventId); await corrupt.ExecuteNonQueryAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, changed) == CsvIssue.IntegrityMismatch, "rehashed ordinary denial after Delivery fails closed stage semantics");
    await tx.RollbackAsync();
}
await using (var history = new NpgsqlCommand("SELECT count(*) FROM synthetic_task_csv.schema_migrations WHERE migration_id IN ('synthetic-task-csv-001','synthetic-task-csv-002')", connection))
    Assert((long)(await history.ExecuteScalarAsync())! == 2, "additive interruption migration preserves original history and initializes idempotently");
await using (var tx = await connection.BeginTransactionAsync())
{
    var audit = new CsvAuditEvent(Guid.NewGuid(), Guid.NewGuid(), golden.RunId, "synthetic-consultant", CsvScope.Fixed, start, CsvAuditStage.Dispatch, CsvAuditReason.None, golden.SnapshotDigest, null, 1);
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, audit) == CsvIssue.IntegrityMismatch, "dispatch denied without durable request audit");
    var rollback = audit with { Stage = CsvAuditStage.Request }; Assert(await auditStore.AppendInTransactionAsync(connection, tx, rollback) is null, "uncommitted request appended"); await tx.RollbackAsync();
    await using var count = new NpgsqlCommand("SELECT count(*) FROM synthetic_task_csv.export_audit WHERE request_id=@request", connection); count.Parameters.AddWithValue("request", audit.RequestId);
    Assert((long)(await count.ExecuteScalarAsync())! == 0, "caller rollback atomically removes audit write");
}
foreach (var sql in new[] { "UPDATE synthetic_task_csv.export_audit SET actor_id=actor_id", "DELETE FROM synthetic_task_csv.export_audit", "TRUNCATE synthetic_task_csv.export_audit" })
{
    try { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); throw new Exception("append guard failed"); } catch (PostgresException e) when (e.SqlState == "P0001") { Assert(true, "audit update/delete/truncate denied"); }
}

// Read-only export policies do not broaden either historical mutation policy.
Assert(PlanningTaskPolicy.Authorize(new("synthetic-auditor", true, true, false, true, PlanningTaskScope.Fixed,
    [PlanningTaskRole.Auditor], ["SECURITY", "OPERATIONS"], [PlanningTaskAction.Read, PlanningTaskAction.Create], PlanningTaskResourceState.Mutable), PlanningTaskScope.Fixed) is not null,
    "historical task policy remains closed to Auditor");
var denial = new CsvAuditEvent(Guid.NewGuid(), Guid.NewGuid(), golden.RunId, "synthetic-consultant", CsvScope.Fixed, DateTimeOffset.UtcNow,
    CsvAuditStage.Denial, CsvAuditReason.Denied, null, null, null);
await using (var tx = await connection.BeginTransactionAsync())
{
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, denial) is null, "denial audit contains no source payload");
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, denial with { EventId = Guid.NewGuid(), Stage = CsvAuditStage.Request, Reason = CsvAuditReason.None }) == CsvIssue.IntegrityMismatch,
        "terminal denial cannot redispatch");
    await tx.CommitAsync();
}
var race = denial with { EventId = Guid.NewGuid(), RequestId = Guid.NewGuid(), Stage = CsvAuditStage.Request, Reason = CsvAuditReason.None };
async Task<CsvIssue?> Race()
{
    await using var db = new NpgsqlConnection(connectionString); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
    var result = await auditStore.AppendInTransactionAsync(db, tx, race); await tx.CommitAsync(); return result;
}
var results = await Task.WhenAll(Race(), Race());
Assert(results.All(result => result is null), "concurrent identical audit command is idempotent");
await using (var command = new NpgsqlCommand("SELECT count(*) FROM synthetic_task_csv.export_audit WHERE request_id=@request", connection))
{
    command.Parameters.AddWithValue("request", race.RequestId); Assert((long)(await command.ExecuteScalarAsync())! == 1, "concurrent audit persists one event");
}
await using (var tx = await connection.BeginTransactionAsync())
{
    await using var drift = new NpgsqlCommand("CREATE TABLE synthetic_task_csv.unapproved_drift (id integer)", connection, tx); await drift.ExecuteNonQueryAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, race with { EventId = Guid.NewGuid() }) == CsvIssue.IntegrityMismatch, "schema drift denies dispatch audit");
    await tx.RollbackAsync();
}
await using (var tx = await connection.BeginTransactionAsync())
{
    await using var corrupt = new NpgsqlCommand("ALTER TABLE synthetic_task_csv.export_audit DISABLE TRIGGER export_audit_immutable; UPDATE synthetic_task_csv.export_audit SET event_digest=repeat('0',64) WHERE request_id=@request; ALTER TABLE synthetic_task_csv.export_audit ENABLE TRIGGER export_audit_immutable", connection, tx);
    corrupt.Parameters.AddWithValue("request", race.RequestId); await corrupt.ExecuteNonQueryAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, race) == CsvIssue.IntegrityMismatch, "receipt metadata corruption denies exact replay");
    await tx.RollbackAsync();
}
await using (var tx = await connection.BeginTransactionAsync())
{
    await using var gone = new NpgsqlCommand("DROP TABLE synthetic_task_csv.export_audit", connection, tx); await gone.ExecuteNonQueryAsync();
    Assert(await auditStore.AppendInTransactionAsync(connection, tx, race) is CsvIssue.IntegrityMismatch or CsvIssue.AuditUnavailable, "audit unavailable denies all output stages");
    await tx.RollbackAsync();
}
await using (var columns = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='synthetic_task_csv' AND column_name IN ('payload','csv','canonical_input','artifact','comment','reason_text','storage_locator','download_token')", connection))
    Assert((long)(await columns.ExecuteScalarAsync())! == 0, "audit schema has no payload/cache/artifact/token columns");
Console.WriteLine($"PASS {checks} CSV integration assertions; actual sandbox and owned PostgreSQL.");
