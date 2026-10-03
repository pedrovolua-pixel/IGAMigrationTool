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
const string connectionString = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_phase1b_csv_tests;Username=iga_synthetic";
var auditStore = new CsvAuditStore(connectionString); await auditStore.InitializeAsync(); await auditStore.InitializeAsync();
await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
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
Console.WriteLine($"PASS {checks} CSV integration assertions; actual sandbox and owned PostgreSQL.");
