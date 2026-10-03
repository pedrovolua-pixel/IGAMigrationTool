using System.Collections.Immutable;
using System.Text.Json;
using Npgsql;
using RecommendationGuidance;
using SyntheticFixReview;

internal static class DatabaseCases
{
    private static string Connection => Environment.GetEnvironmentVariable("IGA_A13_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle13_a13_v3_20261002;Username=iga_synthetic";
    private static NpgsqlConnectionStringBuilder Guard(string value)
    {
        var builder = new NpgsqlConnectionStringBuilder(value);
        if (builder.Host is not ("127.0.0.1" or "localhost" or "::1") || builder.Database is null ||
            !builder.Database.StartsWith("iga_synthetic_cycle13_", StringComparison.Ordinal) || builder.Username != "iga_synthetic")
            throw new InvalidOperationException("Owned Cycle13 test database configuration denied.");
        return builder;
    }
    private static Guid ownedRunId;
    internal static async Task Run()
    {
        var expectedDatabase = Guard(Connection).Database;
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SELECT current_database(),current_user", connection))
        await using (var reader = await guard.ExecuteReaderAsync())
        {
            Program.Check(await reader.ReadAsync() && reader.GetString(0) == expectedDatabase && reader.GetString(1) == "iga_synthetic", "owned dedicated PostgreSQL guard");
        }
        var input = Program.Input();
        ownedRunId = Guid.NewGuid();
        input = input with { Source = input.Source with { RunId = ownedRunId, ReviewRunId = ownedRunId } };
        var callbackCount = 0;
        var available = true;
        ArtifactReviewSourceResult Capture() => available ? ArtifactReviewSourceBuilder.Build(Program.Package(input)) : new(ArtifactReviewIssue.SourceUnavailable, null);
        ArtifactReviewSourceReader read = (_, _) => { callbackCount++; return Task.FromResult(Capture()); };
        SyntheticFixReviewStore Store(ISyntheticFixReviewCommitObserver? observer = null) => new(Connection, ArtifactReviewScope.Fixed, read, observer);
        var store = Store(); var runId = input.Source.RunId;
        var initialized = await Scalar<bool>(connection, "SELECT to_regclass('synthetic_fix_review.schema_migrations') IS NOT NULL");
        var uninitialized = await store.ReadAsync(runId, Program.Authority);
        Program.Check(initialized ? uninitialized.Succeeded : uninitialized.Issue == ArtifactReviewIssue.NotInitialized, "actual initialized/not-initialized state");
        if (!initialized)
        {
            await Execute(connection, "CREATE SCHEMA IF NOT EXISTS synthetic_fix_review; CREATE FUNCTION synthetic_fix_review.a13_unknown_fixture() RETURNS integer LANGUAGE sql AS 'SELECT 1'");
            try
            {
                try { await store.InitializeAsync(); Program.Check(false, "unknown schema function must deny migration"); }
                catch (SyntheticFixReviewMigrationException) { Program.Check(true, "unknown schema function denied before migration"); }
                Program.Check(await Scalar<bool>(connection, "SELECT to_regclass('synthetic_fix_review.schema_migrations') IS NULL"), "failed initialize rolled back its metadata");
            }
            finally { await Execute(connection, "DROP FUNCTION synthetic_fix_review.a13_unknown_fixture()"); }
        }
        var originalConnection = Environment.GetEnvironmentVariable("IGA_A13_ORIGINAL_DATABASE");
        if (originalConnection is not null)
        {
            _ = Guard(originalConnection);
            var originalStore = new SyntheticFixReviewStore(originalConnection, ArtifactReviewScope.Fixed, read);
            Program.Check((await originalStore.ReadAsync(runId, Program.Authority)).Issue == ArtifactReviewIssue.MigrationDrift, "original older fingerprint is typed denied without rewriting");
        }
        else Console.WriteLine("NOT VERIFIED optional older-generation fingerprint case: no explicit original database configured.");
        await store.InitializeAsync(); await store.InitializeAsync();
        var migration = await Scalar<string>(connection, "SELECT digest FROM synthetic_fix_review.schema_migrations");
        Program.Check(migration.Length == 64, "embedded migration registered exactly");
        // Precision alteration is safe only while the entire event table is empty; never round preserved prior rows.
        if (await Scalar<long>(connection, "SELECT count(*) FROM synthetic_fix_review.events") == 0)
        {
            await Execute(connection, "ALTER TABLE synthetic_fix_review.events ALTER COLUMN recorded_at TYPE timestamptz(0)");
            try
            {
                var drift = await store.ReadAsync(runId, Program.Authority);
                Program.Check(drift.Issue == ArtifactReviewIssue.MigrationDrift && drift.Snapshot is null, "datetime precision drift denied before Ready source capture");
                var source = Capture().Source!;
                var rejected = await store.ApplyAsync(runId, source.Artifacts[0].ArtifactId, Program.Authority,
                    new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, 0, source.Binding.SourceDigest, "Fictional drift probe"));
                Program.Check(rejected.Issue == ArtifactReviewIssue.MigrationDrift && rejected.Receipt is null, "datetime precision drift denied before any mutation");
                Program.Check(await Count(connection, "events") == 0 && await Count(connection, "source_versions") == 0, "precision drift leaves no source/history writes");
            }
            finally { await Execute(connection, "ALTER TABLE synthetic_fix_review.events ALTER COLUMN recorded_at TYPE timestamptz"); }
        }
        else Console.WriteLine("NOT VERIFIED optional precision drift case: populated owned database preserved unchanged.");
        await store.InitializeAsync();
        if (await Scalar<bool>(connection, "SELECT rolsuper FROM pg_roles WHERE rolname=current_user"))
        {
            var trigger = await Scalar<string>(connection, "SELECT t.tgname FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='synthetic_fix_review' AND c.relname='events' AND t.tgisinternal ORDER BY t.tgname LIMIT 1");
            var identifier = new NpgsqlCommandBuilder().QuoteIdentifier(trigger);
            await Execute(connection, "ALTER TABLE synthetic_fix_review.events DISABLE TRIGGER " + identifier);
            try
            {
                Program.Check((await store.ReadAsync(runId, Program.Authority)).Issue == ArtifactReviewIssue.MigrationDrift, "internal FK enforcement drift denied before read");
                var source = Capture().Source!;
                Program.Check((await store.ApplyAsync(runId, source.Artifacts[0].ArtifactId, Program.Authority,
                    new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, 0, source.Binding.SourceDigest, "Fictional FK drift probe"))).Issue == ArtifactReviewIssue.MigrationDrift, "internal FK enforcement drift denied before mutation");
                Program.Check(await Count(connection, "events") == 0 && await Count(connection, "source_versions") == 0, "internal FK drift causes no source/history writes");
            }
            finally { await Execute(connection, "ALTER TABLE synthetic_fix_review.events ENABLE TRIGGER " + identifier); }
            await store.InitializeAsync();
        }
        else Console.WriteLine("NOT VERIFIED optional FK enforcement drift case: existing role lacks permission; no role changes made.");
        var initial = await store.ReadAsync(runId, Program.Authority);
        Program.Check(initial.Succeeded && initial.Snapshot!.Entries.Length == 3 && initial.Snapshot.Entries.All(entry => entry.Revision == 0 && entry.State == ArtifactReviewState.Unverified && entry.CanReview && !entry.CanWithdraw && entry.History.IsEmpty), "initial complete ordered three-kind overlay");
        Program.Check(await Count(connection, "source_versions") == 0 && await Count(connection, "artifact_seeds") == 0 && await Count(connection, "events") == 0 && await Count(connection, "receipts") == 0, "reads never seed or append");
        var sourceA = initial.Snapshot!.Source.SourceDigest; var artifact = initial.Snapshot.Entries[0].Artifact.ArtifactId;
        var secondArtifact = initial.Snapshot.Entries[1].Artifact.ArtifactId;
        ArtifactReviewCommand Command(string id, ArtifactReviewKind kind, long revision, string source, string reason = "  Fictional planning only.\0\r\n é e\u0301 😀  ") => new(Guid.Parse(id), kind, revision, source, reason);
        var reviewA = Command("8aafc670-52bb-41c4-9a92-a7d2d66192bc", ArtifactReviewKind.ReviewForPlanning, 0, sourceA);
        var calledBefore = callbackCount;
        foreach (var authority in new[] { Program.Authority with { Authenticated = false }, Program.Authority with { Active = false }, Program.Authority with { Revoked = true }, Program.Authority with { AssignmentActive = false }, Program.Authority with { Roles = [ArtifactReviewRole.QualifiedReviewer] }, Program.Authority with { Actions = [ArtifactReviewAction.Read] }, Program.Authority with { ResourceState = ArtifactReviewResourceState.Published } })
        {
            var result = await store.ApplyAsync(runId, artifact, authority, reviewA);
            Program.Check(result.Issue == ArtifactReviewIssue.Denied && result.Receipt is null && !result.AlreadyApplied, "closed identity denial");
        }
        Program.Check(callbackCount == calledBefore, "identity denial precedes source callback");
        var categoryDenial = await store.ReadAsync(runId, Program.Authority with { Categories = ["OPERATIONS"] });
        Program.Check(categoryDenial.Issue == ArtifactReviewIssue.Denied && categoryDenial.Snapshot is null, "whole-source category denial");
        var noArtifact = await store.ApplyAsync(runId, new string('0', 64), Program.Authority, reviewA);
        Program.Check(noArtifact.Issue == ArtifactReviewIssue.NotFound && noArtifact.Receipt is null, "foreign artifact denied");
        var stale = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA with { ExpectedRevision = 1 });
        Program.Check(stale.Issue == ArtifactReviewIssue.RevisionConflict, "revision conflict before registration");
        stale = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA with { ExpectedSourceDigest = new string('0', 64) });
        Program.Check(stale.Issue == ArtifactReviewIssue.SourceConflict, "source conflict before registration");
        stale = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA with { Kind = ArtifactReviewKind.WithdrawReview });
        Program.Check(stale.Issue == ArtifactReviewIssue.InvalidState, "withdraw unreviewed denied");
        Program.Check(await Count(connection, "source_versions") == 0 && await Count(connection, "events") == 0, "invalid commands make no source/event writes");
        var rollback = Store(new ThrowBeforeCommit());
        try { await rollback.ApplyAsync(runId, artifact, Program.Authority, reviewA); Program.Check(false, "observer rollback must throw"); } catch (RollbackException) { Program.Check(true, "controlled precommit rollback"); }
        Program.Check(await Count(connection, "source_versions") == 0 && await Count(connection, "artifact_seeds") == 0 && await Count(connection, "artifact_current") == 0 && await Count(connection, "events") == 0 && await Count(connection, "receipts") == 0, "all five atomic components rolled back");
        var beforeBytes = Program.Package(input).CanonicalJson;
        var accepted = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA);
        Program.Check(accepted.Succeeded && !accepted.AlreadyApplied && accepted.Receipt!.Revision == 1 && accepted.Receipt.SourceDigest == sourceA && accepted.Receipt.SchemaVersion == "synthetic-fix-review-receipt-v1", "first review metadata receipt");
        Program.Check(Program.Package(input).CanonicalJson == beforeBytes, "generated originals unchanged after attestation");
        var snapshot = await Store().ReadAsync(runId, Program.Authority); var current = snapshot.Snapshot!.Entries.Single(entry => entry.Artifact.ArtifactId == artifact);
        Program.Check(current.Revision == 1 && current.State == ArtifactReviewState.ReviewedForPlanning && !current.CanReview && current.CanWithdraw && current.History[0].Reason == reviewA.Reason, "actual restart exact reason/state/history");
        var receiptJson = Program.Canonical(accepted.Receipt);
        Program.Check(!receiptJson.Contains("reason", StringComparison.Ordinal) && !receiptJson.Contains("Fictional planning", StringComparison.Ordinal) && !receiptJson.Contains("text", StringComparison.Ordinal), "audit excludes reason/content");
        var replay = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA);
        Program.Check(replay.Succeeded && replay.AlreadyApplied && replay.Receipt == accepted.Receipt, "exact historical replay");
        foreach (var changed in new[] { reviewA with { Reason = "changed" }, reviewA with { ExpectedRevision = 1 }, reviewA with { ExpectedSourceDigest = new string('0', 64) }, reviewA with { Kind = ArtifactReviewKind.WithdrawReview } })
            Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, changed)).Issue == ArtifactReviewIssue.EventConflict, "changed semantic replay conflict");
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority with { ActorId = "another-synthetic-consultant" }, reviewA)).Issue == ArtifactReviewIssue.EventConflict, "changed trusted actor event conflict");
        var duplicate = reviewA with { EventId = Guid.Parse("20e9c590-b5a3-48c1-b3ea-c653e8caf566"), ExpectedRevision = 1 };
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, duplicate)).Issue == ArtifactReviewIssue.InvalidState, "fresh UUID duplicate review denied");
        input = input with { Findings = [input.Findings[0] with { FindingRevision = 2, PresentationTitle = "Later fictional title" }], Source = input.Source with { ReviewSnapshotDigest = new string('d', 64) } };
        var sourceB = Capture().Source!.Binding.SourceDigest;
        snapshot = await store.ReadAsync(runId, Program.Authority); current = snapshot.Snapshot!.Entries.Single(entry => entry.Artifact.ArtifactId == artifact);
        Program.Check(current.State == ArtifactReviewState.NeedsReview && current.CanReview && !current.CanWithdraw && current.Revision == 1 && current.History[0].Source.SourceDigest == sourceA, "complete source B invalidates A without write");
        Program.Check(await Count(connection, "source_versions") == 1 && await Count(connection, "events") == 1, "invalidation read makes no source/event writes");
        replay = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA);
        Program.Check(replay.AlreadyApplied && replay.Receipt == accepted.Receipt && (await store.ReadAsync(runId, Program.Authority)).Snapshot!.Entries.Single(entry => entry.Artifact.ArtifactId == artifact).State == ArtifactReviewState.NeedsReview, "historical replay after B never revives A");
        available = false; replay = await store.ApplyAsync(runId, artifact, Program.Authority, reviewA);
        Program.Check(replay.Issue == ArtifactReviewIssue.SourceUnavailable && replay.Receipt is null, "unavailable current source before replay lookup"); available = true;
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, duplicate with { Kind = ArtifactReviewKind.WithdrawReview })).Issue == ArtifactReviewIssue.SourceConflict, "stale A withdraw source conflict first");
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, duplicate with { Kind = ArtifactReviewKind.WithdrawReview, ExpectedSourceDigest = sourceB })).Issue == ArtifactReviewIssue.InvalidState, "withdraw stale attestation denied");
        var reviewB = Command("b089ae20-33cb-4cf1-9dba-3ab1de1f8718", ArtifactReviewKind.ReviewForPlanning, 1, sourceB);
        var b = await store.ApplyAsync(runId, artifact, Program.Authority, reviewB); Program.Check(b.Succeeded && b.Receipt!.Revision == 2, "fresh B review");
        var withdrawB = Command("e5778f1b-e166-411b-aef3-4df3aa5a218f", ArtifactReviewKind.WithdrawReview, 2, sourceB);
        var withdrawn = await store.ApplyAsync(runId, artifact, Program.Authority, withdrawB); Program.Check(withdrawn.Succeeded && withdrawn.Receipt!.Revision == 3, "current B withdrawal");
        input = input with { Findings = [input.Findings[0] with { FindingRevision = 3, PresentationTitle = "Current fictional title" }], Source = input.Source with { ReviewSnapshotDigest = new string('e', 64) } };
        snapshot = await store.ReadAsync(runId, Program.Authority); current = snapshot.Snapshot!.Entries.Single(entry => entry.Artifact.ArtifactId == artifact);
        Program.Check(current.State == ArtifactReviewState.Unverified && current.Revision == 3 && current.History.Length == 3 && current.CanReview && !current.CanWithdraw, "withdrawal remains unverified after return to original prose with later revision");
        var sourceC = snapshot.Snapshot.Source.SourceDigest;
        var reviewC = Command("f27ff098-19e6-4fd8-b8c3-684beef5cf64", ArtifactReviewKind.ReviewForPlanning, 3, sourceC);
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, reviewC)).Succeeded, "review new source C");
        var latestInput = input;
        input = Program.Input();
        input = input with { Source = input.Source with { RunId = ownedRunId, ReviewRunId = ownedRunId } };
        Program.Check((await store.ReadAsync(runId, Program.Authority)).Issue == ArtifactReviewIssue.IntegrityMismatch, "run-wide source rollback denies read");
        Program.Check((await store.ApplyAsync(runId, secondArtifact, Program.Authority, reviewA with { EventId = Guid.NewGuid() })).Issue == ArtifactReviewIssue.IntegrityMismatch, "cross-artifact source rollback denied before first artifact seed");
        Program.Check((await store.ApplyAsync(runId, artifact, Program.Authority, reviewA)).Issue == ArtifactReviewIssue.IntegrityMismatch, "source rollback denies historical replay");
        input = latestInput with { Source = latestInput.Source with { ReviewSnapshotDigest = new string('f', 64) } };
        Program.Check((await store.ReadAsync(runId, Program.Authority)).Issue == ArtifactReviewIssue.IntegrityMismatch, "same run/vector rehashed different source denied");
        input = latestInput with { Source = latestInput.Source with { RunRevision = 14, ReviewRunRevision = 14 } };
        var later = await store.ReadAsync(runId, Program.Authority);
        Program.Check(later.Succeeded && later.Snapshot!.Source.RunRevision == 14 && later.Snapshot.Entries.Single(entry => entry.Artifact.ArtifactId == artifact).State == ArtifactReviewState.NeedsReview, "later run revision with same finding vector admitted");
        // Two identical-revision competing fresh commands: one commits and one conflicts under the fence.
        var competitionSource = later.Snapshot!.Source.SourceDigest;
        var raceA = Command("8100ec02-ebdd-4710-a576-5b655f973f32", ArtifactReviewKind.ReviewForPlanning, 0, competitionSource);
        var raceB = raceA with { EventId = Guid.Parse("343d9a83-b205-40f7-b7cc-0a71b2a0fa1c") };
        var races = await Task.WhenAll(Store().ApplyAsync(runId, secondArtifact, Program.Authority, raceA), Store().ApplyAsync(runId, secondArtifact, Program.Authority, raceB));
        Program.Check(races.Count(result => result.Succeeded) == 1 && races.Count(result => result.Issue == ArtifactReviewIssue.RevisionConflict) == 1, "concurrent artifact expected revision serialized");
        var raceReplayCommand = races[0].Succeeded ? raceA : raceB;
        var sameRaces = await Task.WhenAll(Store().ApplyAsync(runId, secondArtifact, Program.Authority, raceReplayCommand), Store().ApplyAsync(runId, secondArtifact, Program.Authority, raceReplayCommand));
        Program.Check(sameRaces.All(result => result.AlreadyApplied) && sameRaces[0].Receipt == sameRaces[1].Receipt, "concurrent exact replays preserve original receipt");
        var semantic = new { schemaVersion = "synthetic-fix-review-command-v1", scope = ArtifactReviewScope.Fixed, runId, artifactId = artifact, actorId = "synthetic-consultant", command = reviewA };
        var expectedCommandDigest = Program.Hash(Program.Canonical(semantic));
        var actualCommandDigest = await Scalar<string>(connection, "SELECT command_digest FROM synthetic_fix_review.events WHERE run_id='" + runId.ToString("D") + "' AND event_id='8aafc670-52bb-41c4-9a92-a7d2d66192bc'");
        Program.Check(actualCommandDigest == expectedCommandDigest, "independent complete command identity digest");
        foreach (var table in new[] { "source_versions", "artifact_seeds", "events", "receipts", "schema_migrations", "data_plane_scope" })
        {
            foreach (var verb in new[] { "DELETE FROM", "TRUNCATE" })
            {
                try { await using var commandSql = new NpgsqlCommand(verb + " synthetic_fix_review." + table, connection); await commandSql.ExecuteNonQueryAsync(); Program.Check(false, "immutable SQL must deny"); }
                catch (PostgresException exception) { Program.Check(exception.SqlState is "55000" or "0A000", "append-only direct SQL " + verb + table); }
            }
        }
        try { await using var direct = new NpgsqlCommand("UPDATE synthetic_fix_review.artifact_current SET revision=revision+1", connection); await direct.ExecuteNonQueryAsync(); Program.Check(false, "current must match event"); } catch (PostgresException exception) { Program.Check(exception.SqlState == "55000", "current advance needs event"); }
        // One bounded owned tamper; always restore exact bytes and trigger state before continuing.
        var originalCurrent = await Scalar<string>(connection, "SELECT current_json FROM synthetic_fix_review.artifact_current WHERE artifact_id='" + artifact + "'");
        await Execute(connection, "ALTER TABLE synthetic_fix_review.artifact_current DISABLE TRIGGER current_revision_guard");
        try
        {
            await using (var tamper = new NpgsqlCommand("UPDATE synthetic_fix_review.artifact_current SET current_json='{}' WHERE artifact_id=@artifact", connection)) { tamper.Parameters.AddWithValue("artifact", artifact); await tamper.ExecuteNonQueryAsync(); }
            await Execute(connection, "ALTER TABLE synthetic_fix_review.artifact_current ENABLE TRIGGER current_revision_guard");
            Program.Check((await store.ReadAsync(runId, Program.Authority)).Issue == ArtifactReviewIssue.IntegrityMismatch, "tampered current JSON denied after trigger restored");
            await Execute(connection, "ALTER TABLE synthetic_fix_review.artifact_current DISABLE TRIGGER current_revision_guard");
        }
        finally
        {
            try { await using var restore = new NpgsqlCommand("UPDATE synthetic_fix_review.artifact_current SET current_json=@json WHERE artifact_id=@artifact", connection); restore.Parameters.AddWithValue("json", originalCurrent); restore.Parameters.AddWithValue("artifact", artifact); await restore.ExecuteNonQueryAsync(); }
            finally { await Execute(connection, "ALTER TABLE synthetic_fix_review.artifact_current ENABLE TRIGGER current_revision_guard"); }
        }
        Program.Check((await store.ReadAsync(runId, Program.Authority)).Succeeded, "owned current bytes restored and full history verified");
        await store.InitializeAsync(); Program.Check(await Scalar<string>(connection, "SELECT digest FROM synthetic_fix_review.schema_migrations") == migration, "migration fingerprint/restart unchanged");
        Program.Check(callbackCount > 20, "actual trusted callbacks executed");
    }
    private static async Task<T> Scalar<T>(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); return (T)(await command.ExecuteScalarAsync())!; }
    private static Task<long> Count(NpgsqlConnection connection, string table) => Scalar<long>(connection, "SELECT count(*) FROM synthetic_fix_review." + table + " WHERE run_id='" + ownedRunId.ToString("D") + "'");
    private static async Task Execute(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private sealed class RollbackException : Exception;
    private sealed class ThrowBeforeCommit : ISyntheticFixReviewCommitObserver
    { public Task BeforeCommitAsync(string operation, Guid runId, string artifactId, Guid eventId, CancellationToken cancellationToken) => throw new RollbackException(); }
}
