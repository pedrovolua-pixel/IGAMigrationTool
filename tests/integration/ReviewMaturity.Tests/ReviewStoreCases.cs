using System.Collections.Immutable;
using System.Text.Json;
using FindingReview;
using Npgsql;

internal static class ReviewStoreCases
{
    private const string DefaultConnection = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v5;Username=iga_synthetic";
    internal static async Task Run(string[] args)
    {
        if (!args.Contains("--postgres", StringComparer.Ordinal))
        {
            Console.WriteLine("NOT VERIFIED RM-STORE: actual PostgreSQL requires --postgres and own disposable iga_synthetic_v5.");
            return;
        }
        var connection = Environment.GetEnvironmentVariable("IGA_REVIEW_TEST_DATABASE") ?? DefaultConnection;
        var guard = new NpgsqlConnectionStringBuilder(connection);
        if (guard.Host != "127.0.0.1" || guard.Database != "iga_synthetic_v5") throw new InvalidOperationException("Only the authorized disposable loopback review verification database is permitted.");
        SyntheticReviewStore Store(ISyntheticReviewCommitObserver? observer = null) => new(connection, SyntheticReviewScope.Fixed, observer);
        var authority = ReviewPolicyCases.Consultant;
        var store = Store();
        await store.InitializeAsync();
        var seed = ReviewFixtures.Run();
        Check.That((await store.SeedAsync(seed, authority)).Succeeded, "independent immutable seed accepted");
        var seeded = Read(await store.ReadAsync(seed.Scope, seed.RunId, authority));
        var originalJson = JsonSerializer.Serialize(seeded.RunSeed);
        Check.That(seeded.Findings.Length == 3 && seeded.Findings.All(item => item.Current.Revision == 0 && item.History.Length == 0 && item.Seed.Occurrences.Length == 2),
            "three original groups retain six exact per-object occurrences at revision0");
        Check.That((await store.SeedAsync(seed, authority)) is { Succeeded: true, AlreadyApplied: true }, "identical seed is idempotent");
        Check.Equal((await store.SeedAsync(seed with { RunInputDigest = ReviewFixtures.Digest("changed") }, authority)).Issue, SyntheticReviewIssue.SeedConflict, "changed frozen run-input seed conflicts");
        var first = seed.Findings[0].FindingId;
        var second = seed.Findings[1].FindingId;
        var confirm = new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm);
        var accepted = Apply(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, confirm));
        Check.That(accepted is { Revision: 1, State: SyntheticFindingState.Confirmed }, "confirm first finding revision1");
        var hostile = "<script>window.syntheticInjected=true</script><img src=x onerror=alert(1)> & ' quoted";
        var comment = new SyntheticReviewCommand(Guid.NewGuid(), 1, SyntheticReviewEventKind.Comment, Text: hostile);
        Apply(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, comment));
        var edit = new SyntheticReviewCommand(Guid.NewGuid(), 2, SyntheticReviewEventKind.EditPresentation, Title: "Independent presentation title", BusinessContext: hostile);
        var edited = Apply(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, edit));
        Check.That(edited is { Revision: 3, State: SyntheticFindingState.Confirmed, PresentationTitle: "Independent presentation title" } && edited.BusinessContext == hostile,
            "comment and presentation edits do not alter disposition and preserve inert text");
        var after = Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority));
        var timeline = after.Findings.Single(item => item.Current.FindingId == first);
        Check.That(timeline.History.Length == 3 && timeline.History.Select(item => item.Outcome.Revision).SequenceEqual(new long[] { 1, 2, 3 }) &&
            timeline.History.All(item => item.ActorId == authority.ActorId && item.RecordedAtUtc.Offset == TimeSpan.Zero && item.RecordedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-5)),
            "fresh read retains attributed database-time append-only revision history");
        Check.That(timeline.Seed.OriginalTitle == seed.Findings[0].OriginalTitle && JsonSerializer.Serialize(after.RunSeed) == originalJson,
            "all generated originals, occurrence refs and input/analysis locks remain immutable");
        Check.That(timeline.History[1].Command.Text == hostile && timeline.History[2].Command.BusinessContext == hostile && after.SnapshotDigest != seeded.SnapshotDigest,
            "history contains exact text/action payload and coherent changed snapshot digest");
        Check.Equal(JsonSerializer.Serialize(Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority))), JsonSerializer.Serialize(after), "fresh independent store reload reproduces snapshot exactly");
        Check.Group("RM-STORE-001 immutable originals, attributed atomic history and fresh reload");

        var replay = await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, confirm);
        Check.That(replay is { Succeeded: true, AlreadyApplied: true } && replay.Outcome == accepted && replay.Outcome.Revision == 1,
            "identical replay returns original outcome after later comment/edit, not latest revision3");
        Deny(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, confirm with { Reason = "changed accepted payload" }), SyntheticReviewIssue.EventConflict, "changed event payload refuses");
        Deny(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, confirm with { EventId = Guid.NewGuid(), ExpectedRevision = 0, Kind = SyntheticReviewEventKind.Defer }), SyntheticReviewIssue.RevisionConflict,
            "stale expected revision wins over now-invalid transition and writes nothing");
        Deny(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 3, SyntheticReviewEventKind.Defer)), SyntheticReviewIssue.InvalidState, "extra confirmed-to-deferred transition refused");
        Deny(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority with { Revoked = true }, confirm), SyntheticReviewIssue.Denied, "revocation checked even for identical event replay");
        Deny(await store.ApplyAsync(seed.Scope with { EnvironmentId = "wrong-environment" }, seed.RunId, first, authority, comment), SyntheticReviewIssue.WrongScope, "direct wrong-scope substitution denied");
        Deny(await store.ApplyAsync(seed.Scope, seed.RunId, first, authority with { Categories = ["OPERATIONS"] }, comment), SyntheticReviewIssue.Denied, "wrong category does not reveal history");
        Check.Equal(JsonSerializer.Serialize(Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority))), JsonSerializer.Serialize(after), "all replay conflicts/stale/denial cases leave event/current/original snapshot unchanged");
        var secondSameId = await store.ApplyAsync(seed.Scope, seed.RunId, second, authority, confirm);
        Check.That(secondSameId.Succeeded && !secondSameId.AlreadyApplied && secondSameId.Outcome is { Revision: 1, State: SyntheticFindingState.Confirmed },
            "same event UUID on another finding is independent and cannot leak prior outcome");
        var concurrent = await Task.WhenAll(
            Store().ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 3, SyntheticReviewEventKind.Comment, Text: "writer-a")),
            Store().ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 3, SyntheticReviewEventKind.EditPresentation, Title: "writer-b")));
        Check.That(concurrent.Count(item => item.Succeeded && !item.AlreadyApplied) == 1 && concurrent.Count(item => item.Issue == SyntheticReviewIssue.RevisionConflict) == 1,
            "two actual PostgreSQL competing revision3 writers commit exactly one event");
        var raced = Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority));
        Check.That(raced.Findings.Single(item => item.Seed.FindingId == first) is { Current.Revision: 4, History.Length: 4 }, "race yields one monotonic current revision and four history events");
        Check.Group("RM-STORE-002 replay namespace/changed payload/stale CAS/revocation/concurrent row locking");

        var beforeRollback = JsonSerializer.Serialize(raced);
        var injected = Store(new ThrowingObserver("apply"));
        var threw = false;
        try { await injected.ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 4, SyntheticReviewEventKind.Comment, Text: "must rollback")); }
        catch (InjectedCommitFailure) { threw = true; }
        Check.That(threw, "actual precommit failure injected after both writes staged");
        Check.Equal(JsonSerializer.Serialize(Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority))), beforeRollback, "failed commit observer rolls back event and projection together");
        var failedSeed = ReviewFixtures.Run();
        threw = false;
        try { await Store(new ThrowingObserver("seed")).SeedAsync(failedSeed, authority); }
        catch (InjectedCommitFailure) { threw = true; }
        Check.That(threw && (await Store().ReadAsync(seed.Scope, failedSeed.RunId, authority)) is { Issue: SyntheticReviewIssue.NotFound, Snapshot: null },
            "failed seed transaction leaves no visible partial run or finding originals");
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync();
        foreach (var statement in new[]
        {
            "UPDATE synthetic_review.run_seeds SET seed_json=seed_json WHERE run_id=@run",
            "UPDATE synthetic_review.finding_seeds SET seed_json=seed_json WHERE run_id=@run",
            "UPDATE synthetic_review.events SET event_json=event_json WHERE run_id=@run",
            "DELETE FROM synthetic_review.events WHERE run_id=@run",
            "UPDATE synthetic_review.finding_current SET revision=revision+1 WHERE run_id=@run"
        })
        {
            var refused = false;
            try { await using var command = new NpgsqlCommand(statement, database); command.Parameters.AddWithValue("run", seed.RunId); await command.ExecuteNonQueryAsync(); }
            catch (PostgresException exception) when (exception.SqlState == "55000") { refused = true; }
            Check.That(refused, "database refuses original/history rewrite or current update without matching append");
        }
        try
        {
            await Execute(database, "ALTER TABLE synthetic_review.events ADD COLUMN independent_drift_probe text");
            Check.That((await Store().ReadAsync(seed.Scope, seed.RunId, authority)) is { Issue: SyntheticReviewIssue.MigrationDrift, Snapshot: null }, "actual unknown schema column drift refuses all review projection");
            Deny(await Store().ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 4, SyntheticReviewEventKind.Comment, Text: "drift denied")), SyntheticReviewIssue.MigrationDrift, "schema drift cannot append event");
            var refused = false;
            try { await Store().InitializeAsync(); } catch (SyntheticReviewMigrationException) { refused = true; }
            Check.That(refused, "migration initialization refuses actual schema drift");
        }
        finally { await Execute(database, "ALTER TABLE synthetic_review.events DROP COLUMN independent_drift_probe"); }
        await Store().InitializeAsync();
        Check.Equal(JsonSerializer.Serialize(Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority))), beforeRollback, "schema restoration preserves all original/current/history evidence exactly");
        foreach (var (table, trigger, keyColumn, key, expectedIssue) in new[]
        {
            ("run_seeds", "run_seed_immutable", "run_id", (object)seed.RunId, SyntheticReviewIssue.IntegrityMismatch),
            ("schema_migrations", "migration_immutable", "migration_id", (object)"synthetic-review-001", SyntheticReviewIssue.MigrationDrift)
        })
        {
            var column = table == "run_seeds" ? "seed_digest" : "digest";
            string digest;
            await using (var query = new NpgsqlCommand($"SELECT {column} FROM synthetic_review.{table} WHERE {keyColumn}=@key", database))
            {
                query.Parameters.AddWithValue("key", key);
                digest = (string)(await query.ExecuteScalarAsync())!;
            }
            try
            {
                await ProtectedDigest(database, table, trigger, column, keyColumn, key, new string('0', 64));
                Check.That((await Store().ReadAsync(seed.Scope, seed.RunId, authority)) is { Snapshot: null } read && read.Issue == expectedIssue,
                    "actual stored original/migration-content digest corruption refuses projection");
                Deny(await Store().ApplyAsync(seed.Scope, seed.RunId, first, authority, new(Guid.NewGuid(), 4, SyntheticReviewEventKind.Comment, Text: "corrupt denied")), expectedIssue,
                    "actual stored digest corruption cannot append review");
                Check.Equal((await Store().SeedAsync(seed, authority)).Issue, expectedIssue, "reseed cannot mask existing integrity/content corruption");
            }
            finally { await ProtectedDigest(database, table, trigger, column, keyColumn, key, digest); }
            await Store().InitializeAsync();
            Check.Equal(JsonSerializer.Serialize(Read(await Store().ReadAsync(seed.Scope, seed.RunId, authority))), beforeRollback,
                "restored digest/trigger preserve original current/history exactly");
        }
        Check.Group("RM-STORE-003 actual precommit rollback, immutable SQL guards and schema/content/original drift restoration");
    }
    internal static SyntheticReviewSnapshot Read(SyntheticReviewReadResult result)
    {
        Check.That(result.Succeeded && result.Snapshot is not null, $"review read accepted, actual {result.Issue}");
        return result.Snapshot!;
    }
    private static SyntheticFindingCurrent Apply(SyntheticReviewApplyResult result)
    {
        Check.That(result.Succeeded && result.Outcome is not null, $"review apply accepted, actual {result.Issue}");
        return result.Outcome!;
    }
    private static void Deny(SyntheticReviewApplyResult result, SyntheticReviewIssue expected, string description)
        => Check.That(!result.Succeeded && result.Issue == expected, $"{description}; expected {expected}, actual {result.Issue}");
    private static async Task Execute(NpgsqlConnection connection, string statement)
    {
        await using var command = new NpgsqlCommand(statement, connection);
        await command.ExecuteNonQueryAsync();
    }
    // Only constant identifiers from the two owned corruption probes reach this
    // helper; trigger bypass is immediately restored before engine reads.
    private static async Task ProtectedDigest(NpgsqlConnection database, string table, string trigger, string column, string keyColumn, object key, string digest)
    {
        await Execute(database, $"ALTER TABLE synthetic_review.{table} DISABLE TRIGGER {trigger}");
        try
        {
            await using var change = new NpgsqlCommand($"UPDATE synthetic_review.{table} SET {column}=@digest WHERE {keyColumn}=@key", database);
            change.Parameters.AddWithValue("digest", digest);
            change.Parameters.AddWithValue("key", key);
            await change.ExecuteNonQueryAsync();
        }
        finally { await Execute(database, $"ALTER TABLE synthetic_review.{table} ENABLE TRIGGER {trigger}"); }
    }
    private sealed class InjectedCommitFailure : Exception;
    private sealed class ThrowingObserver(string target) : ISyntheticReviewCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid runId, Guid? eventId, CancellationToken cancellationToken)
            => operation == target ? throw new InjectedCommitFailure() : Task.CompletedTask;
    }
}
