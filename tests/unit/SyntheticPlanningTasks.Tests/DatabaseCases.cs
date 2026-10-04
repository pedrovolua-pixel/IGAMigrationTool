using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;
using SyntheticPlanningTasks;
using SyntheticSourceFence;

internal static class DatabaseCases
{
    private static string Connection => Environment.GetEnvironmentVariable("IGA_A14_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle14_a14_20261003_v1;Username=iga_synthetic";
    private static string Guard()
    {
        var value = new NpgsqlConnectionStringBuilder(Connection);
        if (value.Host != "127.0.0.1" || value.Port != 55433 || value.Database is null || !value.Database.StartsWith("iga_synthetic_cycle14_a14_", StringComparison.Ordinal) || value.Username != "iga_synthetic") throw new InvalidOperationException("Owned A14 test database guard denied.");
        return value.Database;
    }
    private static ArtifactReviewAuthority ArtifactAuthority => new("synthetic-consultant", true, true, false, true, ArtifactReviewScope.Fixed,
        [ArtifactReviewRole.Consultant], ["SECURITY", "OPERATIONS"], [ArtifactReviewAction.Read, ArtifactReviewAction.Review], ArtifactReviewResourceState.Mutable);
    internal static async Task Run()
    {
        var database = Guard(); await using var connection = new NpgsqlConnection(Connection); await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("SELECT current_database(),current_user", connection)) await using (var reader = await command.ExecuteReaderAsync())
            Program.Check(await reader.ReadAsync() && reader.GetString(0) == database && reader.GetString(1) == "iga_synthetic", "actual owned DB and role guard");
        var input = Program.Input(); var run = Guid.NewGuid(); input = input with { Source = input.Source with { RunId = run, ReviewRunId = run } };
        var available = true; var captures = 0; var sameTransaction = 0;
        ArtifactReviewSourceResult ArtifactCapture() => ArtifactReviewSourceBuilder.Build(Program.Package(input));
        var artifacts = new SyntheticFixReviewStore(Connection, ArtifactReviewScope.Fixed, (_, _) => Task.FromResult(ArtifactCapture()));
        await artifacts.InitializeAsync();
        PlanningTaskSourceReader readerCallback = async (db, tx, id, authority, ct) =>
        {
            captures++; Program.Check(id == run && tx.Connection == db && db.State == System.Data.ConnectionState.Open, "supplied exact transaction callback");
            await using var isolation = new NpgsqlCommand("SELECT current_database(),pg_backend_pid()", db, tx); await using (var proof = await isolation.ExecuteReaderAsync(ct))
                Program.Check(await proof.ReadAsync(ct) && proof.GetString(0) == database && proof.GetInt32(1) > 0, "same live DB backend callback");
            sameTransaction++;
            if (!available) return new(PlanningTaskIssue.SourceUnavailable, null);
            var package = Program.Package(input); var upstream = ArtifactReviewSourceBuilder.Build(package);
            if (!upstream.Succeeded) return new(PlanningTaskIssue.SourceUnavailable, null);
            var overlay = await artifacts.ReadInTransactionAsync(db, tx, id, ArtifactAuthority with { ActorId = authority.ActorId }, upstream.Source!, ct);
            return overlay.Succeeded ? PlanningTaskSourceBuilder.Build(package, overlay.Snapshot) : new(PlanningTaskIssue.SourceUnavailable, null);
        };
        SyntheticPlanningTaskStore Store(ISyntheticPlanningTaskCommitObserver? observer = null) => new(Connection, PlanningTaskScope.Fixed, readerCallback, observer);
        var store = Store();
        var initialized = await Scalar<bool>(connection, "SELECT to_regclass('synthetic_planning_tasks.schema_migrations') IS NOT NULL");
        Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == (initialized ? null : PlanningTaskIssue.NotInitialized), "initialized or initial absent task schema");
        if (!initialized)
        {
            await Execute(connection, "CREATE SCHEMA IF NOT EXISTS synthetic_planning_tasks; CREATE FUNCTION synthetic_planning_tasks.a14_unknown_fixture() RETURNS integer LANGUAGE sql AS 'SELECT 1'");
            try { try { await store.InitializeAsync(); Program.Check(false, "unknown schema fixture must fail"); } catch (SyntheticPlanningTaskMigrationException) { Program.Check(true, "unknown untracked schema denied"); } }
            finally { await Execute(connection, "DROP FUNCTION synthetic_planning_tasks.a14_unknown_fixture()"); }
        }
        await store.InitializeAsync(); await store.InitializeAsync();
        // Probe precision only before ANY task event exists; populated reruns never round preserved history.
        if (await Scalar<long>(connection, "SELECT count(*) FROM synthetic_planning_tasks.events") == 0)
        {
            await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events ALTER COLUMN recorded_at TYPE timestamptz(0)");
            try
            {
                var beforePrecisionCapture = captures;
                Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == PlanningTaskIssue.MigrationDrift && captures == beforePrecisionCapture, "precision drift before capture");
                Program.Check(await Scalar<long>(connection, "SELECT count(*) FROM synthetic_planning_tasks.events") == 0, "precision probe never writes events");
            }
            finally { await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events ALTER COLUMN recorded_at TYPE timestamptz"); }
        }
        else Console.WriteLine("NOT VERIFIED precision subcase on populated owned database; original timestamps preserved.");
        if (await Scalar<bool>(connection, "SELECT rolsuper FROM pg_roles WHERE rolname=current_user"))
        {
            var trigger = await Scalar<string>(connection, "SELECT t.tgname FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='synthetic_planning_tasks' AND c.relname='events' AND t.tgisinternal ORDER BY t.tgname LIMIT 1");
            var identifier = new NpgsqlCommandBuilder().QuoteIdentifier(trigger);
            await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events DISABLE TRIGGER " + identifier);
            try { var captureBefore = captures; Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == PlanningTaskIssue.MigrationDrift && captures == captureBefore, "internal FK trigger drift before capture"); }
            finally { await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events ENABLE TRIGGER " + identifier); }
        }
        else Console.WriteLine("NOT VERIFIED internal FK trigger drift: existing role permission insufficient; no role changes.");
        var counts = await Counts(connection); var current = await store.ReadAsync(run, Program.Authority);
        Program.Check(current.Succeeded && current.Snapshot!.Entries.IsEmpty && current.Snapshot.Options.Length == 1 && !current.Snapshot.Options[0].CanCreate, "actual no seed read complete option");
        Program.Check(await Counts(connection) == counts, "actual read writes no rows");
        var option = current.Snapshot!.Options[0]; var taskId = option.Identity.TaskId;
        PlanningTaskCommand Command(PlanningTaskKind kind, long revision, PlanningTaskSnapshot snapshot, Guid? id = null, string reason = "  fictional plan \0\r\n é e\u0301 😀  ") =>
            new(id ?? Guid.NewGuid(), kind, revision, snapshot.Source!.ArtifactSource.SourceDigest, snapshot.Options.Single(item => item.Identity.TaskId == taskId).CurrentAttestations, reason);
        var before = captures;
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority with { Actions = [PlanningTaskAction.Read] }, Command(PlanningTaskKind.Create, 0, current.Snapshot!))).Issue == PlanningTaskIssue.Denied, "authority before connection/source");
        Program.Check(captures == before, "denied grant never captures");
        var denied = await store.ApplyAsync(run, taskId, Program.Authority, Command(PlanningTaskKind.Create, 0, current.Snapshot!));
        Program.Check(denied.Issue == PlanningTaskIssue.InvalidState && await Counts(connection) == counts, "unreviewed absent creation no writes");
        // Real owning artifact writes; tasks never forge or bypass attestations in database checks.
        foreach (var id in option.Identity.ArtifactIds)
        {
            var read = await artifacts.ReadAsync(run, ArtifactAuthority); var item = read.Snapshot!.Entries.Single(entry => entry.Artifact.ArtifactId == id);
            Program.Check((await artifacts.ApplyAsync(run, id, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, item.Revision, read.Snapshot.Source.SourceDigest, "Fictional current artifact review"))).Succeeded, "real artifact review accepted");
        }
        current = await store.ReadAsync(run, Program.Authority); Program.Check(current.Snapshot!.Options[0].CanCreate, "real three reviewed conversion eligible");
        var create = Command(PlanningTaskKind.Create, 0, current.Snapshot!); var accepted = await store.ApplyAsync(run, taskId, Program.Authority, create);
        Program.Check(accepted.Succeeded && accepted.Receipt is { Revision: 1, Kind: PlanningTaskKind.Create } && !accepted.AlreadyApplied && accepted.AlreadyExistsTaskId is null, "atomic creation receipt");
        var recordedSemantic = await Value(connection, run, "SELECT command_digest FROM synthetic_planning_tasks.events WHERE run_id=@run ORDER BY result_revision LIMIT 1");
        var expectedSemantic = Program.Hash(Program.Canonical(new { schemaVersion = "synthetic-planning-task-command-v1", scope = PlanningTaskScope.Fixed, runId = run, taskId, actorId = Program.Authority.ActorId, command = create }));
        Program.Check(recordedSemantic == expectedSemantic, "independent actual semantic command digest");
        current = await store.ReadAsync(run, Program.Authority); var entry = current.Snapshot!.Entries.Single();
        Program.Check(entry.Revision == 1 && entry.Status == PlanningTaskStatus.Planned && entry.Freshness == PlanningTaskFreshness.CurrentPlan && entry.Creation == entry.Plan && entry.History.Length == 1 && entry.CanStart && !entry.CanReconfirm && !current.Snapshot.Options[0].CanCreate, "actual creation/current/binding flags");
        var foreign = Program.Authority with { ActorId = "other-trusted-consultant" };
        var foreignRead = await store.ReadAsync(run, foreign);
        Program.Check(foreignRead.Issue == PlanningTaskIssue.Denied && foreignRead.Snapshot is null, "stored owner authority before historical read projection");
        var foreignReplay = await store.ApplyAsync(run, taskId, foreign, create);
        Program.Check(foreignReplay.Issue == PlanningTaskIssue.Denied && foreignReplay.Receipt is null && !foreignReplay.AlreadyApplied && foreignReplay.AlreadyExistsTaskId is null, "stored owner authority before UUID probe");
        var rowsAfterCreate = await Counts(connection);
        var duplicate = create with { EventId = Guid.NewGuid() }; var exists = await store.ApplyAsync(run, taskId, Program.Authority, duplicate);
        Program.Check(exists.AlreadyExistsTaskId == taskId && exists.Receipt is null && !exists.AlreadyApplied && await Counts(connection) == rowsAfterCreate, "fresh duplicate has no UUID or receipt/history/source writes");
        var replay = await store.ApplyAsync(run, taskId, Program.Authority, create);
        Program.Check(replay.AlreadyApplied && replay.Receipt == accepted.Receipt && await Counts(connection) == rowsAfterCreate, "exact create replay original receipt no writes");
        Program.Check((await store.ApplyAsync(run, new string('0', 64), Program.Authority, create)).Issue == PlanningTaskIssue.EventConflict, "module-wide UUID changed task before NotFound");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create with { Reason = "changed" })).Issue == PlanningTaskIssue.EventConflict, "accepted UUID changed exact payload conflicts");
        var redundant = await store.ApplyAsync(run, taskId, Program.Authority, Command(PlanningTaskKind.ReconfirmPlan, 1, current.Snapshot!));
        Program.Check(redundant.Issue == PlanningTaskIssue.InvalidState && await Counts(connection) == rowsAfterCreate, "already current fresh reconfirm denied");
        var start = Command(PlanningTaskKind.StartProgress, 1, current.Snapshot!);
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, start)).Succeeded, "planned to progress");
        current = await store.ReadAsync(run, Program.Authority);
        var revision2 = await Counts(connection);
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, start with { EventId = Guid.NewGuid() })).Issue == PlanningTaskIssue.RevisionConflict && await Counts(connection) == revision2, "stale revision no writes");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, duplicate)).AlreadyExistsTaskId == taskId && await Counts(connection) == revision2, "duplicate initial revision0 ignores existing revision2");
        // Selected withdrawal invalidates without changing full package digest or workflow.
        var selected = option.Identity.ArtifactIds[0]; var ar = await artifacts.ReadAsync(run, ArtifactAuthority); var ae = ar.Snapshot!.Entries.Single(item => item.Artifact.ArtifactId == selected);
        Program.Check((await artifacts.ApplyAsync(run, selected, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.WithdrawReview, ae.Revision, ar.Snapshot.Source.SourceDigest, "Fictional withdrawal"))).Succeeded, "actual selected withdrawal");
        current = await store.ReadAsync(run, Program.Authority); entry = current.Snapshot!.Entries[0];
        Program.Check(entry.Status == PlanningTaskStatus.InProgress && entry.Freshness == PlanningTaskFreshness.NeedsReconfirmation && !entry.CanComplete && !entry.CanReconfirm && entry.CanComment && entry.CanCancel && entry.CanReturnToPlanned && current.Snapshot.Source!.ArtifactSource.SourceDigest == create.ExpectedSourceDigest, "same-package vector invalidation explicit");
        replay = await store.ApplyAsync(run, taskId, Program.Authority, create); Program.Check(replay.AlreadyApplied && replay.Receipt == accepted.Receipt, "accepted replay ignores later withdrawal eligibility");
        counts = await Counts(connection); var changedDuplicate = create with { EventId = Guid.NewGuid(), ExpectedAttestations = current.Snapshot.Options[0].CurrentAttestations };
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, changedDuplicate)).AlreadyExistsTaskId == taskId && await Counts(connection) == counts, "fresh inspected withdrawn duplicate identity only");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, duplicate)).Issue == PlanningTaskIssue.SourceConflict, "old expected selected vector conflicts before duplicate");
        var comment = Command(PlanningTaskKind.Comment, 2, current.Snapshot!); Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, comment)).Succeeded, "stale maintenance comment accepted");
        current = await store.ReadAsync(run, Program.Authority); Program.Check(current.Snapshot!.Entries[0].Plan.EventId == create.EventId && current.Snapshot.Entries[0].History[2].Attestations[0].Revision == 2, "comment preserves plan and observed vector");
        ar = await artifacts.ReadAsync(run, ArtifactAuthority); ae = ar.Snapshot!.Entries.Single(item => item.Artifact.ArtifactId == selected);
        Program.Check((await artifacts.ApplyAsync(run, selected, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, ae.Revision, ar.Snapshot.Source.SourceDigest, "Fictional re-review"))).Succeeded, "real same-text re-review");
        current = await store.ReadAsync(run, Program.Authority); Program.Check(current.Snapshot!.Entries[0].CanReconfirm && current.Snapshot.Entries[0].Freshness == PlanningTaskFreshness.NeedsReconfirmation, "re-review never resurrects old binding");
        var reconfirm = Command(PlanningTaskKind.ReconfirmPlan, 3, current.Snapshot!); var confirmed = await store.ApplyAsync(run, taskId, Program.Authority, reconfirm);
        Program.Check(confirmed.Succeeded && confirmed.Receipt!.Revision == 4, "explicit reconfirmation binding event");
        current = await store.ReadAsync(run, Program.Authority); entry = current.Snapshot!.Entries[0];
        Program.Check(entry.Status == PlanningTaskStatus.InProgress && entry.Freshness == PlanningTaskFreshness.CurrentPlan && entry.Creation.EventId == create.EventId && entry.Plan.EventId == reconfirm.EventId, "reconfirm preserves creation/status");
        // UUID returned by fresh AlreadyExists was not reserved; it can identify a later accepted Comment.
        var reusable = Command(PlanningTaskKind.Comment, 4, current.Snapshot!, duplicate.EventId); Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, reusable)).Succeeded, "duplicate UUID not reserved");
        current = await store.ReadAsync(run, Program.Authority);
        var complete = Command(PlanningTaskKind.Complete, 5, current.Snapshot!); Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, complete)).Succeeded, "progress to completed planning only");
        current = await store.ReadAsync(run, Program.Authority);
        Program.Check(current.Snapshot!.Entries[0].Status == PlanningTaskStatus.Completed && current.Snapshot.Entries[0].CanReopen && !current.Snapshot.Entries[0].CanCancel && !current.Snapshot.Entries[0].CanReconfirm, "terminal flags exact");
        var beforeFinding = input; var beforeArtifact = Program.Canonical((await artifacts.ReadAsync(run, ArtifactAuthority)).Snapshot);
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, Command(PlanningTaskKind.Reopen, 6, current.Snapshot!))).Succeeded, "explicit completed reopen");
        Program.Check(input == beforeFinding && Program.Canonical((await artifacts.ReadAsync(run, ArtifactAuthority)).Snapshot) == beforeArtifact, "tasks never mutate finding/artifact data");
        // Observed B during a stale Comment must become a high-water even though latest plan remains A.
        var sourceA = input; input = input with { Findings = [input.Findings[0] with { FindingRevision = 2, PresentationTitle = "Later fictional B" }], Source = input.Source with { ReviewSnapshotDigest = new string('d', 64) } };
        current = await store.ReadAsync(run, Program.Authority); Program.Check(current.Snapshot!.Entries[0].Freshness == PlanningTaskFreshness.NeedsReconfirmation, "full B source invalidates");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, Command(PlanningTaskKind.Comment, 7, current.Snapshot!))).Succeeded, "B observed maintenance records high-water");
        input = sourceA;
        Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == PlanningTaskIssue.IntegrityMismatch, "rollback A denied after observed B comment");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create)).Issue == PlanningTaskIssue.IntegrityMismatch, "rollback before accepted replay denied");
        input = input with { Findings = [input.Findings[0] with { FindingRevision = 2, PresentationTitle = "Later fictional B" }], Source = input.Source with { ReviewSnapshotDigest = new string('d', 64) } };
        current = await store.ReadAsync(run, Program.Authority); Program.Check(current.Succeeded, "restore exact B proof resumes");
        var rowBeforeFailure = await Counts(connection); var failedCommand = Command(PlanningTaskKind.Comment, 8, current.Snapshot!);
        var failed = await Store(new FailObserver()).ApplyAsync(run, taskId, Program.Authority, failedCommand);
        Program.Check(failed.Issue == PlanningTaskIssue.IntegrityMismatch && await Counts(connection) == rowBeforeFailure, "observer failure all rows rolled back");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, failedCommand)).Succeeded, "failed UUID not reserved atomic retry");
        available = false; current = await store.ReadAsync(run, Program.Authority);
        Program.Check(current.Issue == PlanningTaskIssue.SourceUnavailable && current.Snapshot is { Source: null } && current.Snapshot.Options.IsEmpty && current.Snapshot.Entries.Length == 1 && current.Snapshot.Entries[0].History.Length == 9 && current.Snapshot.Entries[0].Freshness == PlanningTaskFreshness.SourceUnavailable && !current.Snapshot.Entries[0].CanComment && !current.Snapshot.Entries[0].CanCancel, "verified task-owned unavailable history no action");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create)).Issue == PlanningTaskIssue.SourceUnavailable, "unavailable prevents replay");
        var foreignUnavailable = await store.ReadAsync(run, foreign); Program.Check(foreignUnavailable.Issue == PlanningTaskIssue.Denied && foreignUnavailable.Snapshot is null, "unavailable history denies foreign owner"); available = true;
        Program.Check((await Store().ReadAsync(run, Program.Authority)).Succeeded && (await Store().ApplyAsync(run, taskId, Program.Authority, reconfirm)).AlreadyApplied, "new store/reconnect preserves historical replay");
        var readOnly = await store.ReadAsync(run, Program.Authority with { Actions = [PlanningTaskAction.Read] });
        Program.Check(readOnly.Succeeded && readOnly.Snapshot!.Options.All(item => !item.CanCreate) && readOnly.Snapshot.Entries.All(item => !item.CanComment && !item.CanCancel && !item.CanReconfirm), "current partial grants suppress flags");
        Program.Check((await store.ReadAsync(run, Program.Authority with { Categories = ["OPERATIONS"] })).Issue == PlanningTaskIssue.Denied, "complete stored/current category denied");
        available = false; Program.Check((await store.ReadAsync(run, Program.Authority with { Categories = ["OPERATIONS"] })).Issue == PlanningTaskIssue.Denied, "unavailable stored category still denied"); available = true;
        // Existing immutable triggers refuse direct rewrites, and fingerprints detect a disabled guard before any source callback.
        foreach (var table in new[] { "source_versions", "task_seeds", "events", "receipts", "data_plane_scope", "schema_migrations" })
        {
            try { await Execute(connection, "DELETE FROM synthetic_planning_tasks." + table); Program.Check(false, "immutable table rewrite"); }
            catch (PostgresException exception) when (exception.SqlState == "55000") { Program.Check(true, "immutable direct delete " + table); }
        }
        await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events DISABLE TRIGGER event_immutable");
        try { before = captures; Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == PlanningTaskIssue.MigrationDrift && captures == before, "guard drift before source capture"); Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create)).Issue == PlanningTaskIssue.MigrationDrift, "guard drift before replay"); }
        finally { await Execute(connection, "ALTER TABLE synthetic_planning_tasks.events ENABLE TRIGGER event_immutable"); }
        Program.Check((await store.ReadAsync(run, Program.Authority)).Succeeded, "drift restoration exact");
        // Bounded targeted corruption probes restore exact original row bytes and guard state in finally.
        foreach (var (table, column, trigger) in new[] { ("events", "event_json", "event_immutable"), ("receipts", "receipt_json", "receipt_immutable"), ("task_current", "current_json", "current_revision_guard"), ("source_versions", "canonical_package", "source_immutable") })
        {
            var originalRows = new List<(string Key, string Value)>();
            var keyColumn = table is "events" or "receipts" ? "event_id" : table == "task_current" ? "task_id" : "proof_digest";
            await using (var query = new NpgsqlCommand("SELECT " + keyColumn + "::text," + column + " FROM synthetic_planning_tasks." + table + " WHERE run_id=@run ORDER BY " + keyColumn + " LIMIT 1", connection))
            { query.Parameters.AddWithValue("run", run); await using var rows = await query.ExecuteReaderAsync(); while (await rows.ReadAsync()) originalRows.Add((rows.GetString(0), rows.GetString(1))); }
            Program.Check(originalRows.Count == 1, "owned corrupt probe one exact row"); var row = originalRows[0];
            async Task WriteRow(string value)
            {
                await Execute(connection, "ALTER TABLE synthetic_planning_tasks." + table + " DISABLE TRIGGER " + trigger);
                try
                {
                    await using var update = new NpgsqlCommand("UPDATE synthetic_planning_tasks." + table + " SET " + column + "=@value WHERE run_id=@run AND " + keyColumn + "::text=@key", connection);
                    update.Parameters.AddWithValue("value", value); update.Parameters.AddWithValue("run", run); update.Parameters.AddWithValue("key", row.Key);
                    Program.Check(await update.ExecuteNonQueryAsync() == 1, "exact targeted owned corruption/restore");
                }
                finally { await Execute(connection, "ALTER TABLE synthetic_planning_tasks." + table + " ENABLE TRIGGER " + trigger); }
            }
            try
            {
                await WriteRow("{}"); var noWrites = await Counts(connection);
                Program.Check((await store.ReadAsync(run, Program.Authority)).Issue == PlanningTaskIssue.IntegrityMismatch, "actual forged " + table + " denial");
                Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create)).Issue == PlanningTaskIssue.IntegrityMismatch && await Counts(connection) == noWrites, "corruption before replay writes nothing " + table);
            }
            finally { await WriteRow(row.Value); }
            Program.Check((await store.ReadAsync(run, Program.Authority)).Succeeded, "exact original row restoration " + table);
        }
        // Coherent task-owned snapshots must agree on complete earlier artifact events even without current proof.
        var originalProof = await Value(connection, run, "SELECT creation_proof_digest FROM synthetic_planning_tasks.task_seeds WHERE run_id=@run");
        string originalPackage; string originalBinding; string originalArtifacts;
        await using (var query = new NpgsqlCommand("SELECT canonical_package,binding_json,artifact_snapshot_json FROM synthetic_planning_tasks.source_versions WHERE run_id=@run AND proof_digest=@proof", connection))
        {
            query.Parameters.AddWithValue("run", run); query.Parameters.AddWithValue("proof", originalProof);
            await using var row = await query.ExecuteReaderAsync(); Program.Check(await row.ReadAsync(), "exact retained creation source exists");
            originalPackage = row.GetString(0); originalBinding = row.GetString(1); originalArtifacts = row.GetString(2);
        }
        var retained = JsonSerializer.Deserialize<ArtifactReviewSnapshot>(originalArtifacts, Program.Json)!;
        var earlier = retained.Entries[0]; var fork = retained with
        {
            Entries = retained.Entries.SetItem(0, earlier with { History = earlier.History.SetItem(0, earlier.History[0] with { Reason = "Forked historical reason with unchanged UUID revision source and vector" }) })
        };
        using (var packageDocument = JsonDocument.Parse(originalPackage))
        {
            var guidance = JsonSerializer.Deserialize<GuidanceSnapshot>(packageDocument.RootElement.GetProperty("guidance"), Program.Json)!;
            var package = FixPackageBuilder.Build(guidance);
            Program.Check(package.Succeeded && PlanningTaskSourceBuilder.Build(package.Snapshot, fork).Succeeded, "fork is individually complete valid proof with matching canonical package");
        }
        var forkJson = Program.Canonical(fork); var retainedBinding = JsonSerializer.Deserialize<PlanningTaskSourceBinding>(originalBinding, Program.Json)!;
        var forkProof = Program.Hash(Program.Canonical(new { binding = retainedBinding, artifactSnapshot = fork }));
        var prefixRows = await Counts(connection); var forkInstalled = false;
        async Task SwapProof(bool corrupt)
        {
            var from = corrupt ? originalProof : forkProof; var to = corrupt ? forkProof : originalProof; var contents = corrupt ? forkJson : originalArtifacts;
            await using var transaction = await connection.BeginTransactionAsync();
            async Task ExecuteSwap(string sql, params (string Name, object Value)[] values)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
                await command.ExecuteNonQueryAsync();
            }
            try
            {
                await ExecuteSwap("ALTER TABLE synthetic_planning_tasks.source_versions DISABLE TRIGGER source_immutable; ALTER TABLE synthetic_planning_tasks.task_seeds DISABLE TRIGGER task_seed_immutable; ALTER TABLE synthetic_planning_tasks.events DISABLE TRIGGER event_immutable");
                await ExecuteSwap("INSERT INTO synthetic_planning_tasks.source_versions (run_id,proof_digest,customer_id,project_id,environment_id,canonical_package,binding_json,artifact_snapshot_json) SELECT run_id,@to,customer_id,project_id,environment_id,canonical_package,binding_json,@json FROM synthetic_planning_tasks.source_versions WHERE run_id=@run AND proof_digest=@from",
                    ("to", to), ("json", contents), ("run", run), ("from", from));
                await ExecuteSwap("UPDATE synthetic_planning_tasks.task_seeds SET creation_proof_digest=@to WHERE run_id=@run AND creation_proof_digest=@from; UPDATE synthetic_planning_tasks.events SET proof_digest=@to WHERE run_id=@run AND proof_digest=@from; DELETE FROM synthetic_planning_tasks.source_versions WHERE run_id=@run AND proof_digest=@from",
                    ("to", to), ("run", run), ("from", from));
            }
            finally
            {
                await ExecuteSwap("ALTER TABLE synthetic_planning_tasks.source_versions ENABLE TRIGGER source_immutable; ALTER TABLE synthetic_planning_tasks.task_seeds ENABLE TRIGGER task_seed_immutable; ALTER TABLE synthetic_planning_tasks.events ENABLE TRIGGER event_immutable");
            }
            await transaction.CommitAsync();
        }
        try
        {
            await SwapProof(true); forkInstalled = true;
            Program.Check(await Counts(connection) == prefixRows, "coherent prefix probe preserves exact row counts");
            var forkReady = await store.ReadAsync(run, Program.Authority);
            Program.Check(forkReady.Issue == PlanningTaskIssue.IntegrityMismatch && forkReady.Snapshot is null, "forked retained artifact prefix denies ready history");
            available = false;
            var forkUnavailable = await store.ReadAsync(run, Program.Authority);
            Program.Check(forkUnavailable.Issue == PlanningTaskIssue.IntegrityMismatch && forkUnavailable.Snapshot is null, "forked retained artifact prefix denies unavailable metadata");
        }
        finally
        {
            available = true; if (forkInstalled) await SwapProof(false);
        }
        Program.Check(await Value(connection, run, "SELECT artifact_snapshot_json FROM synthetic_planning_tasks.source_versions WHERE run_id=@run AND proof_digest=(SELECT creation_proof_digest FROM synthetic_planning_tasks.task_seeds WHERE run_id=@run)") == originalArtifacts && await Counts(connection) == prefixRows,
            "prefix probe restores exact original snapshot links and row counts");
        Program.Check((await store.ReadAsync(run, Program.Authority)).Succeeded, "legitimate same-package attestation progress and exact restored prefixes remain accepted");
        available = false;
        var restoredUnavailable = await store.ReadAsync(run, Program.Authority);
        Program.Check(restoredUnavailable.Issue == PlanningTaskIssue.SourceUnavailable && restoredUnavailable.Snapshot is not null, "consistent retained prefix unavailable metadata remains accepted");
        available = true;
        // A task transaction holds the shared fence through commit; an owning artifact write follows it.
        foreach (var artifactId in option.Identity.ArtifactIds)
        {
            var currentArtifacts = await artifacts.ReadAsync(run, ArtifactAuthority); var artifactEntry = currentArtifacts.Snapshot!.Entries.Single(item => item.Artifact.ArtifactId == artifactId);
            if (artifactEntry.CanReview) Program.Check((await artifacts.ApplyAsync(run, artifactId, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, artifactEntry.Revision, currentArtifacts.Snapshot.Source.SourceDigest, "Fictional B review before fence probe"))).Succeeded, "actual current B review");
        }
        current = await store.ReadAsync(run, Program.Authority);
        var pendingCommand = Command(PlanningTaskKind.Comment, current.Snapshot!.Entries[0].Revision, current.Snapshot);
        var artifactBeforeFence = await artifacts.ReadAsync(run, ArtifactAuthority); var toWithdraw = artifactBeforeFence.Snapshot!.Entries.Single(item => item.Artifact.ArtifactId == selected);
        var barrier = new BarrierObserver(); var heldWrite = Store(barrier).ApplyAsync(run, taskId, Program.Authority, pendingCommand);
        Task<ArtifactReviewApplyResult>? waitingArtifact = null;
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            waitingArtifact = artifacts.ApplyAsync(run, selected, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.WithdrawReview, toWithdraw.Revision, artifactBeforeFence.Snapshot.Source.SourceDigest, "Fictional serialized withdrawal"));
            await Task.Delay(100); Program.Check(!waitingArtifact.IsCompleted, "real artifact writer waits behind task commit fence");
        }
        finally
        {
            barrier.Release.TrySetResult();
            try { await heldWrite.WaitAsync(TimeSpan.FromSeconds(10)); }
            finally { if (waitingArtifact is not null) await waitingArtifact.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        Program.Check(heldWrite.Result.Succeeded && waitingArtifact!.Result.Succeeded, "both owning writes serialize and finish");
        current = await store.ReadAsync(run, Program.Authority);
        var firstConcurrent = Command(PlanningTaskKind.Comment, current.Snapshot!.Entries[0].Revision, current.Snapshot); var secondConcurrent = firstConcurrent with { EventId = Guid.NewGuid(), Reason = "Separate explicit comment" };
        var writers = await Task.WhenAll(store.ApplyAsync(run, taskId, Program.Authority, firstConcurrent), store.ApplyAsync(run, taskId, Program.Authority, secondConcurrent));
        Program.Check(writers.Count(item => item.Succeeded) == 1 && writers.Count(item => item.Issue == PlanningTaskIssue.RevisionConflict) == 1, "two task revision writers exactly one winner");
        current = await store.ReadAsync(run, Program.Authority); var cancellationRows = await Counts(connection);
        using (var cancellation = new CancellationTokenSource())
        {
            var cancellationBarrier = new BarrierObserver();
            var canceled = Store(cancellationBarrier).ApplyAsync(run, taskId, Program.Authority, Command(PlanningTaskKind.Comment, current.Snapshot!.Entries[0].Revision, current.Snapshot), cancellation.Token);
            try
            {
                await cancellationBarrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
                try { await canceled.WaitAsync(TimeSpan.FromSeconds(10)); Program.Check(false, "controlled cancellation must be observed"); }
                catch (OperationCanceledException) { Program.Check(true, "owned cancellation observed before commit"); }
            }
            finally
            {
                cancellation.Cancel(); cancellationBarrier.Release.TrySetResult();
                try { await canceled.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) { /* Expected cancellation is observed during bounded cleanup too. */ }
            }
        }
        Program.Check(await Counts(connection) == cancellationRows && (await store.ReadAsync(run, Program.Authority)).Succeeded, "cancellation restores all event/current/receipt/source rows");
        var verifiedB = input; input = input with { Findings = [] };
        var lostIdentity = await store.ReadAsync(run, Program.Authority);
        Program.Check(lostIdentity.Issue == PlanningTaskIssue.SourceUnavailable && lostIdentity.Snapshot!.Source is null && lostIdentity.Snapshot.Entries.All(item => item.Freshness == PlanningTaskFreshness.SourceUnavailable && !item.CanComment), "missing stable option proof metadata only");
        Program.Check((await store.ApplyAsync(run, taskId, Program.Authority, create)).Issue == PlanningTaskIssue.SourceUnavailable, "lost stable identity denies accepted replay"); input = verifiedB;
        Program.Check((await store.ReadAsync(run, Program.Authority)).Succeeded, "exact stable proof restoration resumes read");
        // Separate owned run has two options; concurrent conversions and module-wide UUID isolation.
        var firstRun = run; var firstInput = input; run = Guid.NewGuid(); input = Program.Input();
        input = input with { Source = input.Source with { RunId = run, ReviewRunId = run }, Findings = [input.Findings[0] with { Options = [.. input.Findings[0].Options, new("compare-fixture", "Compare fictional evidence.", "A separate fixture run.", "No closure claim.", "Retain originals.")] }] };
        current = await store.ReadAsync(run, Program.Authority);
        foreach (var id in current.Snapshot!.Options.SelectMany(item => item.Identity.ArtifactIds))
        {
            var original = await artifacts.ReadAsync(run, ArtifactAuthority); var actual = original.Snapshot!.Entries.Single(item => item.Artifact.ArtifactId == id);
            Program.Check((await artifacts.ApplyAsync(run, id, ArtifactAuthority, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, actual.Revision, original.Snapshot.Source.SourceDigest, "Fictional other run review"))).Succeeded, "actual other run artifact review");
        }
        current = await store.ReadAsync(run, Program.Authority); var nextOption = current.Snapshot!.Options[0]; var otherOption = current.Snapshot.Options[1];
        var nextCommand = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Create, 0, current.Snapshot.Source!.ArtifactSource.SourceDigest, nextOption.CurrentAttestations, "Fictional concurrent conversion");
        var conversions = await Task.WhenAll(store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, nextCommand), store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, nextCommand with { EventId = Guid.NewGuid() }));
        Program.Check(conversions.Count(item => item.Receipt is not null) == 1 && conversions.Count(item => item.AlreadyExistsTaskId == nextOption.Identity.TaskId) == 1, "concurrent conversions one atomic task and one no-write identity");
        var winner = conversions.Single(item => item.Receipt is not null).Receipt!;
        Program.Check((await store.ApplyAsync(run, otherOption.Identity.TaskId, Program.Authority, nextCommand with { EventId = winner.EventId, ExpectedAttestations = otherOption.CurrentAttestations })).Issue == PlanningTaskIssue.EventConflict, "accepted UUID cannot identify another valid option task");
        Program.Check(nextOption.Identity.TaskId != taskId, "stable task identity differs across runs");
        input = input with { Findings = [input.Findings[0] with { CurrentState = "Rejected", FindingRevision = 2 }], Source = input.Source with { ReviewSnapshotDigest = new string('d', 64) } };
        current = await store.ReadAsync(run, Program.Authority); nextOption = current.Snapshot!.Options.Single(item => item.Identity.TaskId == nextOption.Identity.TaskId); otherOption = current.Snapshot.Options.Single(item => item.Identity.TaskId == otherOption.Identity.TaskId);
        var rejectedRows = await Counts(connection);
        var rejectedDuplicate = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Create, 0, current.Snapshot.Source!.ArtifactSource.SourceDigest, nextOption.CurrentAttestations, "Explicitly inspected Rejected source");
        Program.Check((await store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, rejectedDuplicate)).AlreadyExistsTaskId == nextOption.Identity.TaskId && await Counts(connection) == rejectedRows, "inspected Rejected duplicate no eligibility reapplication");
        Program.Check((await store.ApplyAsync(run, otherOption.Identity.TaskId, Program.Authority, rejectedDuplicate with { EventId = Guid.NewGuid(), ExpectedAttestations = otherOption.CurrentAttestations })).Issue == PlanningTaskIssue.InvalidState && await Counts(connection) == rejectedRows, "absent Rejected conversion denies without rows");
        var maintenance = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Cancel, 1, current.Snapshot.Source.ArtifactSource.SourceDigest, nextOption.CurrentAttestations, "Fictional Rejected maintenance cancellation");
        Program.Check((await store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, maintenance)).Succeeded, "Rejected Planned maintenance cancel");
        Program.Check((await store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, maintenance with { EventId = Guid.NewGuid(), ExpectedRevision = 2, Kind = PlanningTaskKind.Reopen })).Issue == PlanningTaskIssue.InvalidState, "Rejected terminal reopen denied");
        Program.Check((await store.ApplyAsync(run, nextOption.Identity.TaskId, Program.Authority, maintenance with { EventId = Guid.NewGuid(), ExpectedRevision = 2, Kind = PlanningTaskKind.Comment })).Succeeded, "Rejected Cancelled comment allowed");
        run = firstRun; input = firstInput;
        Program.Check(captures == sameTransaction, "every actual callback bound to transaction");
        Console.WriteLine("NOT VERIFIED author host hard-kill/default startup; PostgreSQL reconnect/transaction rollback and real owning-module attestation checks executed.");
    }
    private sealed class BarrierObserver : ISyntheticPlanningTaskCommitObserver
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid runId, string taskId, Guid eventId, CancellationToken ct)
        { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); }
    }
    private sealed class FailObserver : ISyntheticPlanningTaskCommitObserver
    { public Task BeforeCommitAsync(string operation, Guid runId, string taskId, Guid eventId, CancellationToken cancellationToken) => throw new InvalidOperationException("Controlled owned rollback fixture."); }
    private static async Task<T> Scalar<T>(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); return (T)(await command.ExecuteScalarAsync())!; }
    private static async Task Execute(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private static async Task<string> Value(NpgsqlConnection connection, Guid runId, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue("run", runId); return (string)(await command.ExecuteScalarAsync())!; }
    private static async Task<string> Counts(NpgsqlConnection connection)
    {
        var values = new List<long>(); foreach (var table in new[] { "source_versions", "task_seeds", "task_current", "events", "receipts" }) values.Add(await Scalar<long>(connection, "SELECT count(*) FROM synthetic_planning_tasks." + table));
        return string.Join(',', values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }
}
