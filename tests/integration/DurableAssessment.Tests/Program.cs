using System.Diagnostics;
using System.Reflection;
using AssessmentCoverage;
using AssessmentRuns;
using Npgsql;

internal static class Program
{
    private static string Connection => Environment.GetEnvironmentVariable("IGA_SYNTHETIC_TEST_DATABASE")
        ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_v3;Username=iga_synthetic";
    private static int assertions;

    private static async Task Main(string[] args)
    {
        var builder = new NpgsqlConnectionStringBuilder(Connection);
        if (builder.Host != "127.0.0.1" || builder.Database != "iga_synthetic_v3")
            throw new InvalidOperationException("Verification accepts only its explicitly disposable loopback database.");
        if (args.Length > 0)
        {
            await Child(args);
            return;
        }

        var engine = Engine();
        await engine.InitializeAsync();
        var request = SyntheticFixture.Start(Guid.NewGuid().ToString("N"));
        var starts = await Task.WhenAll(engine.StartAsync(request), engine.StartAsync(request));
        var initial = Accept(starts[0]);
        Require(Accept(starts[1]).RunId == initial.RunId, "concurrent identical start has one identity");
        Require(initial.State == SyntheticRunState.Planned && initial.Progress is
        { PlannedUnits: 5, TerminalUnits: 3, RemainingUnits: 2, AllTerminal: false }, "independent five-key initial plan");
        Require(initial.FrozenInputs == request.Versions && initial.Plan.HasPermissionWarning, "full synthetic tuple and warning frozen");
        var reordered = request with
        {
            Baseline = request.Baseline with
            {
                Objects =
            [request.Baseline.Objects.Single() with { Categories = request.Baseline.Objects.Single().Categories.Reverse().ToArray() }]
            }
        };
        Require(Accept(await engine.StartAsync(reordered)).RunId == initial.RunId, "canonical start ignores input category order");
        Deny(await engine.StartAsync(request with { Versions = request.Versions with { ProfileVersion = "synthetic-profile-v2" } }),
            SyntheticRunIssue.IdempotencyConflict, "same start key changed frozen tuple conflicts");
        Console.WriteLine("PASS DUR-PG-001 concurrent/canonical start and full locks");

        var wrong = SyntheticFixture.Scope with { CustomerId = "synthetic-other-customer" };
        Deny(await engine.ReadAsync(wrong, initial.RunId), SyntheticRunIssue.WrongScope, "wrong customer read denied");
        Deny(await engine.ReadAsync(SyntheticFixture.Scope with { ProjectId = "synthetic-other-project" }, initial.RunId),
            SyntheticRunIssue.WrongScope, "wrong project read denied");
        Deny(await engine.ReadAsync(SyntheticFixture.Scope with { EnvironmentId = "synthetic-other-environment" }, initial.RunId),
            SyntheticRunIssue.WrongScope, "wrong environment read denied");
        Deny(await engine.RequestCancelAsync(wrong, initial.RunId, initial.Revision), SyntheticRunIssue.WrongScope, "wrong scope mutation denied");
        try { await engine.ListAsync(wrong); throw new Exception("Wrong scope history was disclosed."); }
        catch (ArgumentException) { assertions++; }
        Deny(await new SyntheticDurableRunEngine(Connection, wrong, SyntheticFixture.Policy).ReadAsync(wrong, initial.RunId),
            SyntheticRunIssue.WrongScope, "constructing another scope without initialize cannot bypass database marker");
        Console.WriteLine("PASS DUR-PG-002 scope denial");

        var leased = Accept(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, initial.RunId, "synthetic-worker-a", initial.Revision));
        var generation = leased.Lease!.Generation;
        Deny(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, initial.RunId, "synthetic-worker-b"),
            SyntheticRunIssue.LeaseUnavailable, "live lease excludes overlapping worker");
        Deny(await engine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, leased.Revision - 1, [SyntheticFixture.Pass]),
            SyntheticRunIssue.RevisionConflict, "stale revision denied before new result");
        var beforeOutbox = (await engine.ReadOutboxAsync(SyntheticFixture.Scope, initial.RunId)).Count;
        var checkpoint = Accept(await engine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, leased.Revision, [SyntheticFixture.Pass]));
        Require(checkpoint.Results.Count == 4 && checkpoint.CheckpointSequence == leased.CheckpointSequence + 1,
            "one successful unit advances one checkpoint");
        Require((await engine.ReadOutboxAsync(SyntheticFixture.Scope, initial.RunId)).Count == beforeOutbox + 1,
            "result and checkpoint have one corresponding outbox event");
        var replay = await engine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, leased.Revision, [SyntheticFixture.Pass]);
        Require(replay.AlreadyApplied && Accept(replay).Revision == checkpoint.Revision && replay.Snapshot!.Results.Count == 4,
            "same terminal result replay has no repeated successful effect");
        Deny(await engine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, checkpoint.Revision,
            [SyntheticFixture.Pass with { State = CoverageState.Finding }]), SyntheticRunIssue.ResultConflict, "successful result cannot change");
        Deny(await engine.CompleteCoverageAsync(SyntheticFixture.Scope, initial.RunId, generation, checkpoint.Revision),
            SyntheticRunIssue.InvalidCoverage, "missing required unit cannot advance to scoring");
        Console.WriteLine("PASS DUR-PG-003 CAS/result replay and missing completion");

        var rollbackEngine = Engine(new ThrowObserver("checkpoint"));
        try
        {
            await rollbackEngine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, checkpoint.Revision, [SyntheticFixture.Finding]);
            throw new Exception("Injected transaction failure was not reached.");
        }
        catch (InjectedFailure) { assertions++; }
        var afterRollback = Accept(await engine.ReadAsync(SyntheticFixture.Scope, initial.RunId));
        Require(afterRollback.Revision == checkpoint.Revision && afterRollback.CheckpointSequence == checkpoint.CheckpointSequence &&
            afterRollback.Results.Count == 4 && (await engine.ReadOutboxAsync(SyntheticFixture.Scope, initial.RunId)).Count == beforeOutbox + 1,
            "failed commit rolls back result/revision/checkpoint/outbox together");
        await KillChild("before-checkpoint", initial.RunId, generation, checkpoint.Revision);
        var afterKill = Accept(await Engine().ReadAsync(SyntheticFixture.Scope, initial.RunId));
        Require(afterKill.Revision == checkpoint.Revision && afterKill.Results.Count == 4 &&
            (await engine.ReadOutboxAsync(SyntheticFixture.Scope, initial.RunId)).Count == beforeOutbox + 1,
            "hard-killed process before commit leaves transaction effects absent");
        Console.WriteLine("PASS DUR-PG-004 thrown failure and actual process termination rollback");

        await KillChild("after-checkpoint", initial.RunId, generation, checkpoint.Revision);
        var committed = Accept(await Engine().ReadAsync(SyntheticFixture.Scope, initial.RunId));
        Require(committed.Results.Count == 5 && committed.Progress.AllTerminal && committed.FrozenInputs == request.Versions &&
            committed.InputDigest == initial.InputDigest, "fresh process reads committed results and identical full locks");
        var postCrashReplay = await engine.CheckpointAsync(SyntheticFixture.Scope, initial.RunId, generation, checkpoint.Revision, [SyntheticFixture.Finding]);
        Require(postCrashReplay.AlreadyApplied && Accept(postCrashReplay).Revision == committed.Revision, "post-commit response loss retry stays idempotent");
        Require(await Count("SELECT count(*) FROM synthetic_assessment.results WHERE run_id=@run", initial.RunId) == 5 &&
            await Count("SELECT count(*) FROM synthetic_assessment.plan_units WHERE run_id=@run", initial.RunId) == 5 &&
            await Count("SELECT count(*) FROM synthetic_assessment.attempts WHERE run_id=@run AND outcome=1", initial.RunId) == 2,
            "independent PostgreSQL counts prove five unique results and two successful attempts");
        var scoring = Accept(await engine.CompleteCoverageAsync(SyntheticFixture.Scope, initial.RunId, generation, committed.Revision));
        Require(scoring.State == SyntheticRunState.Scoring && scoring.ScoringPaused &&
            scoring.CoverageSummary is { Kind: CoverageCompletionKind.CompleteWithGaps, ExecutableCoverage: { ExecutedUnits: 2, ApplicablePlannedUnits: 4 } },
            "coverage completion pauses unimplemented scoring and preserves independent two-of-four measure");
        Require(scoring.CoverageSummary!.Limitations.SequenceEqual(new CoverageLimitation[]
        {
            new(CoverageState.NotAssessed, "NOT-EXECUTED", "planner", 1),
            new(CoverageState.Unsupported, "SEMANTIC-DEFERRED", "planner", 1)
        }), "actual stored summary has exact expected gap reasons/stages");
        var eventToDeliver = (await engine.ReadOutboxAsync(SyntheticFixture.Scope, initial.RunId)).First();
        var delivery = await engine.DeliverOutboxAsync(SyntheticFixture.Scope, eventToDeliver.EventId, "synthetic-v3-consumer");
        Require(delivery.Issue is null && !delivery.AlreadyApplied, "first durable inbox delivery records application");
        var duplicateDelivery = await Engine().DeliverOutboxAsync(SyntheticFixture.Scope, eventToDeliver.EventId, "synthetic-v3-consumer");
        Require(duplicateDelivery.Issue is null && duplicateDelivery.AlreadyApplied, "fresh process/store duplicate event delivery deduplicates");
        Console.WriteLine("PASS DUR-PG-005 commit-before-response recovery/coverage/outbox inbox");

        var cancellation = Accept(await engine.StartAsync(SyntheticFixture.Start(Guid.NewGuid().ToString("N"))));
        var cancelLease = Accept(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, cancellation.RunId, "synthetic-cancel-worker"));
        var begun = Accept(await engine.BeginWorkAsync(SyntheticFixture.Scope, cancellation.RunId, cancelLease.Lease!.Generation,
            cancelLease.Revision, [SyntheticFixture.Pass.Key]));
        var cancelRequested = Accept(await engine.RequestCancelAsync(SyntheticFixture.Scope, cancellation.RunId, begun.Revision));
        Require(cancelRequested.CancelRequested, "cancellation intent persisted");
        Deny(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, cancellation.RunId, "synthetic-new-worker"),
            SyntheticRunIssue.CancellationRequested, "cancellation stops newly acquired work");
        Deny(await engine.CheckpointAsync(SyntheticFixture.Scope, cancellation.RunId, cancelLease.Lease.Generation,
            cancelRequested.Revision, [SyntheticFixture.Finding]), SyntheticRunIssue.CancellationRequested, "unbegun unit cannot write after cancellation");
        var inFlight = Accept(await engine.CheckpointAsync(SyntheticFixture.Scope, cancellation.RunId, cancelLease.Lease.Generation,
            cancelRequested.Revision, [SyntheticFixture.Pass]));
        var cancelled = Accept(await engine.FinalizeCancellationAsync(SyntheticFixture.Scope, cancellation.RunId, inFlight.Revision, cancelLease.Lease.Generation));
        Require(cancelled.State == SyntheticRunState.Cancelled && cancelled.Results.Count == 4 && cancelled.FrozenInputs == cancellation.FrozenInputs,
            "cancel preserves allowed in-flight and previously declared results");
        var cancelledReload = Accept(await Engine().ReadAsync(SyntheticFixture.Scope, cancelled.RunId));
        Require(cancelledReload.State == SyntheticRunState.Cancelled && cancelledReload.Results.Count == 4, "fresh engine cannot resurrect cancelled work");
        Deny(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, cancelled.RunId, "synthetic-resurrection-worker"),
            SyntheticRunIssue.CancellationRequested, "cancelled run cannot reacquire work");
        Console.WriteLine("PASS DUR-PG-006 durable cancellation preserves results");

        // Real database expiry, independent of the process clock. A short fixture lease is
        // deliberately not a production setting; the second process replaces only expired work.
        var shortEngine = Engine(policy: new(TimeSpan.FromMilliseconds(100), 2));
        var expiring = Accept(await shortEngine.StartAsync(SyntheticFixture.Start(Guid.NewGuid().ToString("N"))));
        var oldLease = Accept(await shortEngine.AcquireLeaseAsync(SyntheticFixture.Scope, expiring.RunId, "synthetic-expired-worker"));
        await Task.Delay(250);
        var replacement = Accept(await engine.AcquireLeaseAsync(SyntheticFixture.Scope, expiring.RunId, "synthetic-replacement-worker"));
        Require(replacement.Lease!.Generation != oldLease.Lease!.Generation, "expired lease replacement changes fencing generation");
        Deny(await shortEngine.HeartbeatAsync(SyntheticFixture.Scope, expiring.RunId, oldLease.Lease.Generation),
            SyntheticRunIssue.StaleLease, "expired worker cannot heartbeat after replacement");
        Deny(await shortEngine.CheckpointAsync(SyntheticFixture.Scope, expiring.RunId, oldLease.Lease.Generation, replacement.Revision, [SyntheticFixture.Pass]),
            SyntheticRunIssue.StaleLease, "stale generation cannot commit even with current revision");
        Deny(await shortEngine.ReleaseLeaseAsync(SyntheticFixture.Scope, expiring.RunId, oldLease.Lease.Generation),
            SyntheticRunIssue.StaleLease, "stale generation cannot release replacement lease");
        var failed = Accept(await engine.RecordAttemptFailureAsync(SyntheticFixture.Scope, expiring.RunId,
            replacement.Lease.Generation, replacement.Revision, SyntheticFixture.Pass.Key, "synthetic-transient-failure"));
        Require(failed.Results.Count == 3 && failed.Attempts.Single().Outcome == SyntheticAttemptOutcome.Failed,
            "failed attempt remains recorded without fabricating terminal success");
        var retried = Accept(await engine.CheckpointAsync(SyntheticFixture.Scope, expiring.RunId,
            replacement.Lease.Generation, failed.Revision, [SyntheticFixture.Pass]));
        Require(retried.Results.Count == 4 && retried.Attempts.Count == 2 &&
            retried.Attempts.Count(attempt => attempt.Outcome == SyntheticAttemptOutcome.Succeeded) == 1,
            "new retry attempt preserves prior failure and has one successful effect");
        Console.WriteLine("PASS DUR-PG-007 actual lease expiry and stale writer fencing");

        var delayedPolicy = new SyntheticRunPolicy(TimeSpan.FromMilliseconds(500), 2);
        var delayedEngine = Engine(policy: delayedPolicy);
        var delayRun = Accept(await delayedEngine.StartAsync(SyntheticFixture.Start(Guid.NewGuid().ToString("N"))));
        var delayLease = Accept(await delayedEngine.AcquireLeaseAsync(SyntheticFixture.Scope, delayRun.RunId, "synthetic-delay-worker"));
        var delayOutbox = (await delayedEngine.ReadOutboxAsync(SyntheticFixture.Scope, delayRun.RunId)).Count;
        var expiresDuringCommit = Engine(new DelayObserver("checkpoint", TimeSpan.FromMilliseconds(800)), delayedPolicy);
        Deny(await expiresDuringCommit.CheckpointAsync(SyntheticFixture.Scope, delayRun.RunId, delayLease.Lease!.Generation,
            delayLease.Revision, [SyntheticFixture.Pass]), SyntheticRunIssue.StaleLease, "lease expiring inside staged transaction cannot commit");
        var delayReload = Accept(await engine.ReadAsync(SyntheticFixture.Scope, delayRun.RunId));
        Require(delayReload.Results.Count == 3 && delayReload.Attempts.Count == 0 &&
            delayReload.CheckpointSequence == delayLease.CheckpointSequence && delayReload.Revision == delayLease.Revision &&
            (await engine.ReadOutboxAsync(SyntheticFixture.Scope, delayRun.RunId)).Count == delayOutbox,
            "lease expiry after staged changes rolls back result/attempt/checkpoint/revision/outbox atomically");
        Console.WriteLine("PASS DUR-PG-010 lease expiry during staged transaction");

        if (Environment.GetEnvironmentVariable("IGA_ALLOW_TEST_CLUSTER_RESTART") == "1")
        {
            await RestartPostgres();
            var restarted = Accept(await Engine().ReadAsync(SyntheticFixture.Scope, initial.RunId));
            Require(restarted.State == SyntheticRunState.Scoring && restarted.Results.Count == 5 &&
                restarted.InputDigest == initial.InputDigest && restarted.FrozenInputs == request.Versions, "actual PostgreSQL restart retains full committed run");
            Require(Accept(await Engine().ReadAsync(SyntheticFixture.Scope, cancelled.RunId)).State == SyntheticRunState.Cancelled,
                "database restart retains cancellation without resurrection");
            Console.WriteLine("PASS DUR-PG-008 actual PostgreSQL crash restart");
        }
        else Console.WriteLine("NOT VERIFIED DUR-PG-008: explicit disposable-cluster restart opt-in absent.");

        await using (var connection = new NpgsqlConnection(Connection))
        {
            await connection.OpenAsync();
            await RefuseMutation(connection, "UPDATE synthetic_assessment.runs SET versions_json='changed' WHERE run_id=@run", initial.RunId);
            await RefuseMutation(connection, "UPDATE synthetic_assessment.results SET state=1 WHERE run_id=@run", initial.RunId);
            Require(await Count("SELECT count(*) FROM synthetic_assessment.results WHERE run_id=@run", initial.RunId) == 5,
                "database immutable guards preserve actual results");
            await using var readDigest = new NpgsqlCommand("SELECT digest FROM synthetic_assessment.schema_migrations WHERE migration_id='synthetic-assessment-runs-001'", connection);
            var digest = (string)(await readDigest.ExecuteScalarAsync())!;
            try
            {
                await Sql(connection, "UPDATE synthetic_assessment.schema_migrations SET digest='synthetic-corrupt-digest' WHERE migration_id='synthetic-assessment-runs-001'");
                await RefuseDrift();
            }
            finally
            {
                await using var restore = new NpgsqlCommand("UPDATE synthetic_assessment.schema_migrations SET digest=@digest WHERE migration_id='synthetic-assessment-runs-001'", connection);
                restore.Parameters.AddWithValue("digest", digest);
                await restore.ExecuteNonQueryAsync();
            }
            try
            {
                await Sql(connection, "ALTER TABLE synthetic_assessment.runs ADD COLUMN verification_drift text");
                await RefuseDrift();
            }
            finally { await Sql(connection, "ALTER TABLE synthetic_assessment.runs DROP COLUMN verification_drift"); }
            try
            {
                await Sql(connection, "ALTER TABLE synthetic_assessment.results DISABLE TRIGGER immutable_results");
                await RefuseDrift();
            }
            finally { await Sql(connection, "ALTER TABLE synthetic_assessment.results ENABLE TRIGGER immutable_results"); }
            await Engine().InitializeAsync();
            string originalVersions;
            await using (var readVersions = new NpgsqlCommand("SELECT versions_json FROM synthetic_assessment.runs WHERE run_id=@run", connection))
            {
                readVersions.Parameters.AddWithValue("run", initial.RunId);
                originalVersions = (string)(await readVersions.ExecuteScalarAsync())!;
            }
            try
            {
                await Sql(connection, "ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs");
                await using var corrupt = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET versions_json='invalid-synthetic-lock' WHERE run_id=@run", connection);
                corrupt.Parameters.AddWithValue("run", initial.RunId);
                await corrupt.ExecuteNonQueryAsync();
                Deny(await Engine().ReadAsync(SyntheticFixture.Scope, initial.RunId), SyntheticRunIssue.InputIntegrityMismatch,
                    "corrupted frozen input refuses read before projection");
            }
            finally
            {
                await using var restore = new NpgsqlCommand("UPDATE synthetic_assessment.runs SET versions_json=@versions WHERE run_id=@run", connection);
                restore.Parameters.AddWithValue("versions", originalVersions);
                restore.Parameters.AddWithValue("run", initial.RunId);
                await restore.ExecuteNonQueryAsync();
                await Sql(connection, "ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs");
            }
            try
            {
                await new SyntheticDurableRunEngine(Connection, wrong, SyntheticFixture.Policy).InitializeAsync();
                throw new Exception("Synthetic database was rebound to a different scope.");
            }
            catch (SyntheticMigrationDriftException) { assertions++; }
        }
        Require(Accept(await engine.ReadAsync(SyntheticFixture.Scope, initial.RunId)).InputDigest == initial.InputDigest,
            "restored synthetic schema fingerprint preserves frozen run evidence");
        Console.WriteLine("PASS DUR-PG-009 migration digest and actual column drift refused without rewriting evidence");

        Console.WriteLine($"{assertions} independent durable PostgreSQL assertions passed. Synthetic local execution only; production authorization, Service Bus and cloud recovery NOT VERIFIED.");
    }

    private static SyntheticDurableRunEngine Engine(ISyntheticRunCommitObserver? observer = null, SyntheticRunPolicy? policy = null) =>
        new(Connection, SyntheticFixture.Scope, policy ?? SyntheticFixture.Policy, observer);

    private static SyntheticRunSnapshot Accept(SyntheticRunCommandResult result)
    {
        Require(result.Succeeded && result.Snapshot is not null, $"command accepted, actual issue: {result.Issue}");
        return result.Snapshot!;
    }

    private static void Deny(SyntheticRunCommandResult result, SyntheticRunIssue expected, string name) =>
        Require(!result.Succeeded && result.Issue == expected, $"{name}, actual issue: {result.Issue}");

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception($"Independent durable verification failed: {name}");
        assertions++;
    }

    private static async Task<long> Count(string sql, Guid runId)
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("run", runId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task Sql(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RefuseDrift()
    {
        try { await Engine().InitializeAsync(); throw new Exception("Synthetic migration drift was accepted."); }
        catch (SyntheticMigrationDriftException) { assertions++; }
    }

    private static async Task RefuseMutation(NpgsqlConnection connection, string sql, Guid runId)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("run", runId);
        try { await command.ExecuteNonQueryAsync(); throw new Exception("Immutable synthetic database evidence was changed."); }
        catch (PostgresException error) when (error.SqlState == "P0001") { assertions++; }
    }

    private static async Task Child(string[] args)
    {
        var runId = Guid.Parse(args[1]);
        var generation = Guid.Parse(args[2]);
        var revision = long.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        if (args[0] == "before-checkpoint")
            await Engine(new BarrierObserver("checkpoint")).CheckpointAsync(SyntheticFixture.Scope, runId, generation, revision, [SyntheticFixture.Finding]);
        else if (args[0] == "after-checkpoint")
        {
            Accept(await Engine().CheckpointAsync(SyntheticFixture.Scope, runId, generation, revision, [SyntheticFixture.Finding]));
            Console.WriteLine("READY");
            Console.Out.Flush();
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        else throw new InvalidOperationException("Unknown synthetic child operation.");
    }

    private static async Task KillChild(string mode, Guid runId, Guid generation, long revision)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("IGA_DOTNET") ?? "dotnet")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var argument in new[] { mode, runId.ToString("D"), generation.ToString("D"), revision.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new Exception("Synthetic child process did not start.");
        try
        {
            var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (ready != "READY") throw new Exception($"Synthetic child did not reach transaction barrier: {await child.StandardError.ReadToEndAsync()}");
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Require(child.ExitCode != 0, "separate worker process was actually terminated");
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
    }

    private static async Task RestartPostgres()
    {
        var data = Environment.GetEnvironmentVariable("IGA_SYNTHETIC_CLUSTER_DATA");
        if (data != "/private/tmp/iga-cycle03-pg") throw new InvalidOperationException("Unknown disposable cluster directory.");
        var ctl = Environment.GetEnvironmentVariable("IGA_PG_CTL") ?? "pg_ctl";
        await Control("stop", "immediate");
        await Control("start", null);
        NpgsqlConnection.ClearAllPools();

        async Task Control(string command, string? mode)
        {
            var start = new ProcessStartInfo(ctl) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var argument in new[] { "-D", data, "-w", command }) start.ArgumentList.Add(argument);
            if (mode is not null) { start.ArgumentList.Add("-m"); start.ArgumentList.Add(mode); }
            if (command == "start")
            {
                start.ArgumentList.Add("-l"); start.ArgumentList.Add(Path.Combine(data, "verification-restart.log"));
                start.ArgumentList.Add("-o"); start.ArgumentList.Add("-h 127.0.0.1 -p 55433 -k /private/tmp");
            }
            using var process = Process.Start(start) ?? throw new Exception("Synthetic PostgreSQL control did not start.");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Require(process.ExitCode == 0, "disposable PostgreSQL control completed");
        }
    }

    private sealed class InjectedFailure : Exception;
    private sealed class ThrowObserver(string expectedOperation) : ISyntheticRunCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken) =>
            operation == expectedOperation ? throw new InjectedFailure() : Task.CompletedTask;
    }

    private sealed class DelayObserver(string expectedOperation, TimeSpan delay) : ISyntheticRunCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken) =>
            operation == expectedOperation ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
    }

    private sealed class BarrierObserver(string expectedOperation) : ISyntheticRunCommitObserver
    {
        public async Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken)
        {
            if (operation != expectedOperation) return;
            Console.WriteLine("READY");
            Console.Out.Flush();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
