using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;

internal static class DatabaseCases
{
    internal const string DatabaseName = "iga_synthetic_cycle13_v13_v2";
    internal static string Connection { get; private set; } = "";
    internal static void Configure()
    {
        Connection = Environment.GetEnvironmentVariable("IGA_ARTIFACT_REVIEW_TEST_DATABASE") ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle13_v13_v2;Username=iga_synthetic";
        Guard(Connection);
    }
    internal static async Task Run()
    {
        Configure();
        await using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Connection) { Database = "postgres" }.ConnectionString))
        {
            await admin.OpenAsync();
            await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin);
            exists.Parameters.AddWithValue("name", DatabaseName);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
            {
                await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_cycle13_v13_v2", admin);
                await create.ExecuteNonQueryAsync();
            }
        }
        var initial = new DomainFixture();
        await initial.Store().InitializeAsync();
        await StatesAndReplay();
        await AuthorityAndValidation();
        await MonotonicAndConcurrent();
        await RollbackAndSchema();
        await SavedSourceCases.Run();
        Check.Group("AR13-T02/T03/T04/T05/T06/T08/T11/T12 owned PostgreSQL");
    }
    internal static void Guard(string value)
    {
        var keys = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Split('=', 2)[0].Trim().ToLowerInvariant()).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length || !keys.Order(StringComparer.Ordinal).SequenceEqual(new[] { "database", "host", "port", "username" }))
            throw new InvalidOperationException("owned_database_duplicate_or_extra_key");
        var settings = new NpgsqlConnectionStringBuilder(value);
        if (settings.Host != "127.0.0.1" || settings.Port != 55433 || settings.Database != DatabaseName || settings.Username != "iga_synthetic")
            throw new InvalidOperationException("owned_database_guard");
    }
    internal static async Task<T?> Scalar<T>(string sql, params (string Name, object Value)[] values)
    {
        Guard(Connection);
        await using var db = new NpgsqlConnection(Connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        return (T?)await command.ExecuteScalarAsync();
    }
    internal static async Task Sql(string sql, params (string Name, object Value)[] values)
    {
        Guard(Connection);
        await using var db = new NpgsqlConnection(Connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        await command.ExecuteNonQueryAsync();
    }
    internal static async Task<string> Counts(Guid run)
    {
        var parts = new List<string>();
        foreach (var table in new[] { "source_versions", "artifact_seeds", "artifact_current", "events", "receipts" })
            parts.Add((await Scalar<long>("SELECT count(*) FROM synthetic_fix_review." + table + " WHERE run_id=@run", ("run", run))).ToString(CultureInfo.InvariantCulture));
        return string.Join("|", parts);
    }
    internal sealed class DomainFixture
    {
        internal readonly Guid RunId = Guid.NewGuid();
        internal GuidanceInput Current;
        internal int Reads;
        internal bool Unavailable;
        internal DomainFixture(string name = "normal")
        {
            var input = PortableCases.Input(name);
            Current = input with { Source = input.Source with { RunId = RunId, ReviewRunId = RunId } };
        }
        internal ArtifactReviewSourceResult Build()
        {
            if (Unavailable) return new(ArtifactReviewIssue.SourceUnavailable, null);
            var guidance = RecommendationGuidanceBuilder.Build(Current);
            Check.That(guidance.Succeeded, "domain-fixture-real-guidance-no-reflection");
            var package = FixPackageBuilder.Build(guidance.Snapshot);
            Check.That(package.Succeeded, "domain-fixture-real-original-package");
            return ArtifactReviewSourceBuilder.Build(package.Snapshot);
        }
        internal Task<ArtifactReviewSourceResult> Read(Guid run, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested(); Reads++;
            return Task.FromResult(run == RunId ? Build() : new(ArtifactReviewIssue.SourceUnavailable, null));
        }
        internal SyntheticFixReviewStore Store(ISyntheticFixReviewCommitObserver? observer = null) => new(Connection, ArtifactReviewScope.Fixed, Read, observer);
        internal string Digest => Build().Source!.Binding.SourceDigest;
        internal string[] Artifacts => Build().Source!.Artifacts.Select(a => a.ArtifactId).ToArray();
        internal void Advance(bool restore = false)
        {
            var revision = Current.Findings[0].FindingRevision + 1;
            Current = Current with
            {
                Source = Current.Source with { ReviewSnapshotDigest = Expected.Hash("fixture-current-review-" + revision) },
                Findings = Current.Findings.Select(f => f with
                {
                    FindingRevision = revision,
                    PresentationTitle = restore ? "Current fictional V11 title " + (f.FindingId[0] == 'b' ? "1" : "2") : "Changed fictional B title"
                }).ToImmutableArray()
            };
        }
    }
    internal static ArtifactReviewCommand Command(string digest, long revision, ArtifactReviewKind kind = ArtifactReviewKind.ReviewForPlanning, string reason = "Fictional explicit planning review") => new(Guid.NewGuid(), kind, revision, digest, reason);
    internal static ArtifactReviewEntry Entry(ArtifactReviewReadResult read, string id)
    {
        Check.That(read.Succeeded && read.Snapshot is not null, "durable-read-success");
        return read.Snapshot!.Entries.Single(e => e.Artifact.ArtifactId == id);
    }
    internal static void State(ArtifactReviewReadResult read, string id, string expected, long revision)
    {
        var entry = Entry(read, id);
        Check.Equal(entry.State.ToString(), expected, "latest-event-current-source-derived-state");
        Check.Equal(entry.Revision, revision, "continuous-current-artifact-revision");
        Check.Equal(entry.History.Length, checked((int)revision), "full-ordered-history-not-receipt-projection");
        Check.That(entry.History.Select(e => e.Revision).SequenceEqual(Enumerable.Range(1, entry.History.Length).Select(i => (long)i)), "history-continuous-starts-at-one");
        Check.Equal(entry.CanReview, expected != "ReviewedForPlanning", "exact-current-review-capability");
        Check.Equal(entry.CanWithdraw, expected == "ReviewedForPlanning", "exact-current-withdraw-capability");
        if (entry.History.Length > 0)
        {
            var latest = entry.History[^1];
            Check.Equal(Expected.State(latest.Kind.ToString(), latest.Source.SourceDigest, read.Snapshot!.Source.SourceDigest), expected, "independent-latest-state-oracle");
            Check.That(entry.History.All(e => e.ActorId == PolicyCases.Consultant.ActorId && e.ActorRoles.SequenceEqual(new[] { "Consultant" }) && e.RecordedAtUtc.Offset == TimeSpan.Zero), "trusted-attribution-UTC-on-all-events");
        }
    }
    private static async Task StatesAndReplay()
    {
        var f = new DomainFixture("hostile"); var store = f.Store(); var artifact = f.Artifacts[0]; var before = await Counts(f.RunId);
        for (var i = 0; i < 3; i++) State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "Unverified", 0);
        Check.Equal(await Counts(f.RunId), before, "reads-never-register-source-seed-events-receipts");
        var original = f.Build().Source!; var reason = "  Fictional café 中文 😀\0NUL\r\nCRLF\rCR  ";
        var command = Command(f.Digest, 0, reason: reason); var accepted = await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command);
        Check.That(accepted.Succeeded && !accepted.AlreadyApplied, "initial-review-atomic-success");
        var receiptJson = Expected.Canonical(JsonSerializer.SerializeToNode(accepted.Receipt, V13Program.Web));
        Check.That(!receiptJson.Contains("reason", StringComparison.Ordinal) && !receiptJson.Contains("text", StringComparison.Ordinal), "receipt-payload-free-no-reason-artifact-evidence");
        var current = await store.ReadAsync(f.RunId, PolicyCases.Consultant); State(current, artifact, "ReviewedForPlanning", 1);
        Check.Equal(Entry(current, artifact).History[0].Reason, reason, "exact-untrimmed-UTF16-control-reason-durable");
        Check.Equal(Entry(current, artifact).History[0].Source.SourceDigest, original.Binding.SourceDigest, "event-attests-exact-full-source-A");
        var storedDigest = await Scalar<string>("SELECT command_digest FROM synthetic_fix_review.events WHERE run_id=@run AND artifact_id=@artifact AND event_id=@event", ("run", f.RunId), ("artifact", artifact), ("event", command.EventId));
        Check.Equal(storedDigest, Expected.CommandDigest(JsonSerializer.SerializeToNode(original.Binding, V13Program.Web)!.AsObject(), artifact, PolicyCases.Consultant.ActorId, JsonSerializer.SerializeToNode(command, V13Program.Web)!), "independent-private-semantic-command-byte-digest");
        var initialCounts = await Counts(f.RunId);
        var replay = await f.Store().ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command);
        Check.That(replay.Succeeded && replay.AlreadyApplied, "restart-original-command-exact-replay");
        Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(replay.Receipt, V13Program.Web)), receiptJson, "replay-original-actor-time-outcome-receipt");
        Check.Equal(await Counts(f.RunId), initialCounts, "replay-never-appends");
        var duplicate = await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, Command(f.Digest, 1));
        Check.Equal(duplicate.Issue, ArtifactReviewIssue.InvalidState, "fresh-id-current-review-denied");
        var withdrawal = Command(f.Digest, 1, ArtifactReviewKind.WithdrawReview, "Fictional explicit withdrawal");
        Check.That((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, withdrawal)).Succeeded, "current-A-withdrawal-success");
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "Unverified", 2);
        f.Advance(); var sourceB = f.Digest;
        State(await f.Store().ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "Unverified", 2);
        var historic = await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command);
        Check.That(historic.Succeeded && historic.AlreadyApplied, "historical-A-replay-after-withdraw-and-refresh-inert");
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "Unverified", 2);
        Check.Equal((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command with { Reason = "changed" })).Issue, ArtifactReviewIssue.EventConflict, "same-event-changed-command-conflict");
        Check.Equal((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command with { ExpectedRevision = 2, EventId = Guid.NewGuid() })).Issue, ArtifactReviewIssue.SourceConflict, "new-id-stale-A-source-denied");
        var bReview = Command(sourceB, 2); Check.That((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, bReview)).Succeeded, "fresh-B-review-after-withdraw");
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "ReviewedForPlanning", 3);
        f.Advance(restore: true);
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "NeedsReview", 3);
        Check.Equal((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, Command(f.Digest, 3, ArtifactReviewKind.WithdrawReview))).Issue, ArtifactReviewIssue.InvalidState, "stale-B-withdrawal-denied");
        Check.That((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, withdrawal)).AlreadyApplied, "withdraw-A-historical-replay-current-needs-review");
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "NeedsReview", 3);
        var reReview = Command(f.Digest, 3); Check.That((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, reReview)).Succeeded, "later-prose-return-fresh-review-required");
        Check.That((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, Command(f.Digest, 4, ArtifactReviewKind.WithdrawReview))).Succeeded, "later-current-withdraw-success");
        State(await f.Store().ReadAsync(f.RunId, PolicyCases.Consultant), artifact, "Unverified", 5);
        var noCurrent = await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, Command(f.Digest, 5, ArtifactReviewKind.WithdrawReview));
        Check.Equal(noCurrent.Issue, ArtifactReviewIssue.InvalidState, "no-current-attestation-withdraw-denied");
        Check.Equal((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, Command(f.Digest, 0))).Issue, ArtifactReviewIssue.RevisionConflict, "new-id-stale-revision-denied");
        Check.Equal((await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant with { ActorId = "other-consultant" }, command)).Issue, ArtifactReviewIssue.EventConflict, "same-event-other-trusted-actor-is-not-replay");
        f.Unavailable = true;
        Check.That(!(await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, command)).Succeeded, "missing-current-source-denies-historical-replay");
        f.Unavailable = false;
        Check.That(!(await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant with { Revoked = true }, command)).Succeeded, "revoked-current-actor-denies-historical-replay");
        Check.Group("AR13-T03/T04 all explicit source/state/withdraw/re-review/historical receipt sequences");
    }
    private static async Task AuthorityAndValidation()
    {
        var f = new DomainFixture(); var store = f.Store(); var artifact = f.Artifacts[0]; var command = Command(f.Digest, 0); var before = await Counts(f.RunId);
        var denials = new List<ArtifactReviewAuthority>
        {
            PolicyCases.Consultant with { Authenticated=false }, PolicyCases.Consultant with { Active=false }, PolicyCases.Consultant with { Revoked=true },
            PolicyCases.Consultant with { AssignmentActive=false }, PolicyCases.Consultant with { AssignedScope=new("foreign","synthetic-project","synthetic-environment") },
            PolicyCases.Consultant with { AssignedScope=new("synthetic-customer","foreign","synthetic-environment") },
            PolicyCases.Consultant with { AssignedScope=new("synthetic-customer","synthetic-project","foreign") },
            PolicyCases.Consultant with { Actions=[ArtifactReviewAction.Read] }, PolicyCases.Consultant with { Actions=[ArtifactReviewAction.Review] },
            PolicyCases.Consultant with { Categories=["SECURITY"] }, PolicyCases.Consultant with { Categories=["OPERATIONS"] }
        };
        denials.AddRange(Enum.GetValues<ArtifactReviewRole>().Where(v => v != ArtifactReviewRole.Consultant).Select(v => PolicyCases.Consultant with { Roles = [v] }));
        denials.AddRange(Enum.GetValues<ArtifactReviewResourceState>().Where(v => v != ArtifactReviewResourceState.Mutable).Select(v => PolicyCases.Consultant with { ResourceState = v }));
        foreach (var denied in denials)
        {
            var read = await store.ReadAsync(f.RunId, denied); Check.That(!read.Succeeded && read.Snapshot is null, "real-store-denied-read-no-history-partial-source");
            var apply = await store.ApplyAsync(f.RunId, artifact, denied, command); Check.That(!apply.Succeeded && apply.Receipt is null, "real-store-denied-action-no-audit-leak");
        }
        foreach (var bad in new[] { command with { EventId = Guid.Empty }, command with { Kind = (ArtifactReviewKind)999 }, command with { ExpectedRevision = -1 }, command with { ExpectedRevision = Expected.MaximumRevision + 1 }, command with { ExpectedSourceDigest = new string('A', 64) }, command with { Reason = " " }, command with { Reason = new string('a', 2001) } })
            Check.That(!(await store.ApplyAsync(f.RunId, artifact, PolicyCases.Consultant, bad)).Succeeded, "invalid-new-command-no-write");
        Check.That(!(await store.ApplyAsync(f.RunId, new string('0', 64), PolicyCases.Consultant, command)).Succeeded, "foreign-artifact-direct-operation-denied");
        Check.That(!(await store.ApplyAsync(Guid.NewGuid(), artifact, PolicyCases.Consultant, command)).Succeeded, "foreign-run-direct-operation-denied");
        Check.Equal(await Counts(f.RunId), before, "all-invalid-authority-command-cases-zero-effects");
        var empty = new DomainFixture("empty"); var counts = await Counts(empty.RunId); var emptyRead = await empty.Store().ReadAsync(empty.RunId, PolicyCases.Consultant);
        Check.That(emptyRead.Succeeded && emptyRead.Snapshot!.Entries.IsEmpty, "empty-actual-package-valid-read-no-controls");
        Check.Equal(await Counts(empty.RunId), counts, "empty-read-no-source-artifact-registration");
    }
    private sealed class HoldObserver : ISyntheticFixReviewCommitObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid runId, string artifactId, Guid eventId, CancellationToken ct)
        { Check.Equal(operation, "apply", "real-before-commit-observer"); Entered.TrySetResult(); await Released.Task.WaitAsync(ct); }
    }
    private sealed class RejectObserver : ISyntheticFixReviewCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid runId, string artifactId, Guid eventId, CancellationToken ct) => throw new InvalidOperationException("controlled_test_rollback");
    }
    private static async Task MonotonicAndConcurrent()
    {
        var f = new DomainFixture(); var old = f.Current; var store = f.Store(); var ids = f.Artifacts; var a = Command(f.Digest, 0);
        Check.That((await store.ApplyAsync(f.RunId, ids[0], PolicyCases.Consultant, a)).Succeeded, "artifact-X-attests-A");
        f.Advance(); var sourceB = f.Digest;
        Check.That((await store.ApplyAsync(f.RunId, ids[1], PolicyCases.Consultant, Command(sourceB, 0))).Succeeded, "artifact-Y-registers-later-source-B");
        State(await store.ReadAsync(f.RunId, PolicyCases.Consultant), ids[0], "NeedsReview", 1);
        var newer = f.Current; var counts = await Counts(f.RunId); f.Current = old;
        var backward = await store.ReadAsync(f.RunId, PolicyCases.Consultant); Check.That(!backward.Succeeded && backward.Snapshot is null, "cross-artifact-backward-source-Ready-read-denied");
        Check.That(!(await store.ApplyAsync(f.RunId, ids[0], PolicyCases.Consultant, a)).Succeeded, "backward-source-historical-replay-denied");
        Check.That(!(await store.ApplyAsync(f.RunId, ids[2], PolicyCases.Consultant, Command(f.Digest, 0))).Succeeded, "backward-source-new-action-denied");
        Check.Equal(await Counts(f.RunId), counts, "monotonic-read-replay-action-denials-zero-writes"); f.Current = newer;
        var equal = f.Current;
        f.Current = equal with { Source = equal.Source with { ReviewSnapshotDigest = new string('9', 64) } };
        Check.That(!(await store.ReadAsync(f.RunId, PolicyCases.Consultant)).Succeeded, "equal-run-and-finding-vector-different-full-digest-denied");
        f.Current = equal with { Source = equal.Source with { RunRevision = 18, ReviewRunRevision = 18 } };
        Check.That((await store.ReadAsync(f.RunId, PolicyCases.Consultant)).Succeeded, "later-run-revision-unchanged-finding-vector-valid"); f.Current = equal;
        var concurrent = new DomainFixture(); var target = concurrent.Artifacts[0]; var hold = new HoldObserver();
        var first = concurrent.Store(hold).ApplyAsync(concurrent.RunId, target, PolicyCases.Consultant, Command(concurrent.Digest, 0));
        ArtifactReviewApplyResult[] results;
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var second = concurrent.Store().ApplyAsync(concurrent.RunId, target, PolicyCases.Consultant, Command(concurrent.Digest, 0));
            hold.Released.TrySetResult(); results = await Task.WhenAll(first, second);
        }
        finally { hold.Released.TrySetResult(); await first; }
        Check.Equal(results.Count(v => v.Succeeded), 1, "same-revision-two-writers-exactly-one-atomic-commit");
        Check.Equal(results.Count(v => v.Issue == ArtifactReviewIssue.RevisionConflict), 1, "concurrent-loser-revision-conflict");
        State(await concurrent.Store().ReadAsync(concurrent.RunId, PolicyCases.Consultant), target, "ReviewedForPlanning", 1);
        Check.Equal(await Counts(concurrent.RunId), "1|1|1|1|1", "concurrent-no-orphan-seed-event-current-receipt");
    }
    private static async Task RollbackAndSchema()
    {
        var f = new DomainFixture(); var target = f.Artifacts[0]; var before = await Counts(f.RunId); var failed = Command(f.Digest, 0);
        try { _ = await f.Store(new RejectObserver()).ApplyAsync(f.RunId, target, PolicyCases.Consultant, failed); }
        catch (InvalidOperationException) { Check.That(true, "controlled-before-commit-failure-observed"); }
        Check.Equal(await Counts(f.RunId), before, "failed-commit-rolls-back-source-seed-current-event-receipt");
        var hold = new HoldObserver(); using var cancellation = new CancellationTokenSource();
        var pending = f.Store(hold).ApplyAsync(f.RunId, target, PolicyCases.Consultant, failed, cancellation.Token);
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15)); cancellation.Cancel();
            try { _ = await pending; } catch (OperationCanceledException) { Check.That(true, "controlled-cancellation-observed"); }
        }
        finally
        {
            cancellation.Cancel(); hold.Released.TrySetResult();
            try { await pending; } catch (OperationCanceledException) { }
        }
        Check.Equal(await Counts(f.RunId), before, "cancelled-commit-atomic-rollback");
        var retry = await f.Store().ApplyAsync(f.RunId, target, PolicyCases.Consultant, failed);
        Check.That(retry.Succeeded && !retry.AlreadyApplied, "rolled-back-event-exact-retry-accepted-once");
        var receipt = Expected.Canonical(JsonSerializer.SerializeToNode(retry.Receipt, V13Program.Web));
        NpgsqlConnection.ClearAllPools();
        var restart = await f.Store().ApplyAsync(f.RunId, target, PolicyCases.Consultant, failed);
        Check.That(restart.Succeeded && restart.AlreadyApplied, "DB-reconnect-new-store-original-event-replay");
        Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(restart.Receipt, V13Program.Web)), receipt, "reconnect-original-receipt-exact");
        var counts = await Counts(f.RunId);
        foreach (var table in new[] { "source_versions", "artifact_seeds", "events", "receipts" })
            foreach (var sql in new[] { "DELETE FROM synthetic_fix_review." + table + " WHERE run_id=@run", "TRUNCATE synthetic_fix_review." + table + " CASCADE" })
            {
                var denied = false; try { await Sql(sql, ("run", f.RunId)); } catch (PostgresException) { denied = true; }
                Check.That(denied, "append-only-delete-truncate-denied-at-database");
            }
        Check.Equal(await Counts(f.RunId), counts, "append-only-denials-leave-durable-rows-unchanged");
        var triggerDisabled = false;
        try
        {
            Check.Equal(await Scalar<string>("SELECT current_database()"), DatabaseName, "exact-owned-db-before-bounded-schema-drift");
            await Sql("ALTER TABLE synthetic_fix_review.events DISABLE TRIGGER event_immutable"); triggerDisabled = true;
            var denied = await f.Store().ReadAsync(f.RunId, PolicyCases.Consultant);
            Check.That(!denied.Succeeded && denied.Snapshot is null, "disabled-immutable-trigger-schema-fence-denied");
            Check.Equal(denied.Issue, ArtifactReviewIssue.MigrationDrift, "schema-fence-typed-trigger-drift-code");
        }
        finally { if (triggerDisabled) await Sql("ALTER TABLE synthetic_fix_review.events ENABLE TRIGGER event_immutable"); }
        Check.Equal(await Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgname='event_immutable' AND tgrelid='synthetic_fix_review.events'::regclass"), "O", "original-trigger-enabled-after-finally");
        Check.That((await f.Store().ReadAsync(f.RunId, PolicyCases.Consultant)).Succeeded, "restored-schema-exact-history-read");
        foreach (var changes in new[] { new NpgsqlConnectionStringBuilder(Connection) { Host = "foreign.invalid" }, new NpgsqlConnectionStringBuilder(Connection) { Database = "postgres" } })
        {
            var denied = false; try { var bad = new SyntheticFixReviewStore(changes.ConnectionString, ArtifactReviewScope.Fixed, f.Read); await bad.InitializeAsync(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { denied = true; }
            Check.That(denied, "foreign-nonsynthetic-db-refused-before-network-mutation");
        }
    }
}
