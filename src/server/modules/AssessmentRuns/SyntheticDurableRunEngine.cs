using System.Text;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentOrchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AssessmentRuns;

public sealed class SyntheticRunIntegrityException(string message) : Exception(message);

/// <summary>
/// PostgreSQL-backed internal SYNTHETIC execution. Its fixed trusted scope is not
/// authentication or customer routing. No real baseline, rule, provider, scoring,
/// customer-source connection, durable broker or production activation is supplied.
/// Coverage-stage completion pauses at Scoring; it never completes an assessment.
/// </summary>
public sealed class SyntheticDurableRunEngine
{
    public const string WorkSchemaVersion = "synthetic-run-work-v1";
    public const string MinimumWorkerVersion = "synthetic-worker-v1";
    private readonly DbContextOptions<SyntheticRunDbContext> options;
    private readonly SyntheticAuthorizedScope trustedScope;
    private readonly SyntheticRunPolicy policy;
    private readonly ISyntheticRunCommitObserver? observer;

    public SyntheticDurableRunEngine(string connectionString, SyntheticAuthorizedScope trustedScope,
        SyntheticRunPolicy policy, ISyntheticRunCommitObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(trustedScope);
        ArgumentNullException.ThrowIfNull(policy);
        if (!ValidScope(trustedScope) || policy.LeaseDuration <= TimeSpan.Zero || policy.LeaseDuration > TimeSpan.FromMinutes(10) ||
            policy.MaxAttempts is < 1 or > 20 || policy.MaxCheckpointItems is < 1 or > 10_000)
            throw new ArgumentException("Invalid explicitly synthetic scope or execution settings.");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
            connection.Database is null || !connection.Database.StartsWith("iga_synthetic_", StringComparison.Ordinal))
            throw new ArgumentException("Only an explicitly named iga_synthetic_ database on loopback is permitted.", nameof(connectionString));
        options = new DbContextOptionsBuilder<SyntheticRunDbContext>().UseNpgsql(connection.ConnectionString,
            provider => provider.CommandTimeout(15)).EnableSensitiveDataLogging(false).Options;
        this.trustedScope = trustedScope;
        this.policy = policy;
        this.observer = observer;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = CreateDb();
        await SyntheticRunMigration.InitializeAsync(db, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO synthetic_assessment.data_plane_scope VALUES (true,{trustedScope.CustomerId},{trustedScope.ProjectId},{trustedScope.EnvironmentId}) ON CONFLICT DO NOTHING", cancellationToken);
        await using var command = NativeCommand(db, "SELECT customer_id,project_id,environment_id FROM synthetic_assessment.data_plane_scope WHERE singleton=true");
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != trustedScope.CustomerId ||
                reader.GetString(1) != trustedScope.ProjectId || reader.GetString(2) != trustedScope.EnvironmentId)
                throw new SyntheticMigrationDriftException("The synthetic database is bound to another trusted scope.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<SyntheticRunCommandResult> StartAsync(SyntheticStartRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) return Deny(SyntheticRunIssue.InvalidInput);
        if (!MatchesScope(request.Scope)) return Deny(SyntheticRunIssue.WrongScope);
        if (!ValidText(request.IdempotencyKey) || !ValidText(request.BaselineCatalogId) || !ValidText(request.ProfileCatalogId) ||
            !ValidVersions(request.Versions)) return Deny(SyntheticRunIssue.InvalidInput);
        var planned = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, trustedScope);
        if (!planned.HasPlan) return Deny(SyntheticRunIssue.InvalidPlan);
        var planJson = JsonSerializer.Serialize(planned.Plan!);
        var versionsJson = JsonSerializer.Serialize(request.Versions);
        var digest = InputDigest(planJson, versionsJson, request.BaselineCatalogId, request.ProfileCatalogId);
        await using var db = CreateDb();
        var bindingIssue = await BindingIssueAsync(db, cancellationToken);
        if (bindingIssue is not null) return Deny(bindingIssue.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734021004)", cancellationToken);
        var existing = await db.Runs.FromSqlInterpolated($"SELECT * FROM synthetic_assessment.runs WHERE project_id={trustedScope.ProjectId} AND idempotency_key={request.IdempotencyKey} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.InputDigest != digest) return Deny(SyntheticRunIssue.IdempotencyConflict);
            var original = await SnapshotAsync(db, existing, cancellationToken);
            return new(null, original, true);
        }
        var now = await ClockAsync(db, cancellationToken);
        var run = new RunRow
        {
            RunId = Guid.NewGuid(),
            CustomerId = trustedScope.CustomerId,
            ProjectId = trustedScope.ProjectId,
            EnvironmentId = trustedScope.EnvironmentId,
            IdempotencyKey = request.IdempotencyKey,
            BaselineCatalogId = request.BaselineCatalogId,
            ProfileCatalogId = request.ProfileCatalogId,
            InputDigest = digest,
            VersionsJson = versionsJson,
            PlanJson = planJson,
            State = (int)SyntheticRunState.Planned,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        db.PlanUnits.AddRange(planned.Plan!.ExpectedKeys.Select(key => new PlanUnitRow
        {
            RunId = run.RunId,
            InventoryId = key.InventoryId,
            CategoryId = key.EvidenceCategory
        }));
        await db.SaveChangesAsync(cancellationToken);
        db.Results.AddRange(planned.Plan.DeclaredItems.Select(item => Result(run.RunId, item)));
        AddEvent(db, run, "planned", now);
        await db.SaveChangesAsync(cancellationToken);
        var snapshot = await SnapshotAsync(db, run, cancellationToken);
        await BeforeCommitAsync("start", run.RunId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(null, snapshot);
    }

    public async Task<SyntheticRunCommandResult> ReadAsync(SyntheticAuthorizedScope scope, Guid runId, CancellationToken cancellationToken = default)
    {
        if (!MatchesScope(scope)) return Deny(SyntheticRunIssue.WrongScope);
        await using var db = CreateDb();
        var bindingIssue = await BindingIssueAsync(db, cancellationToken);
        if (bindingIssue is not null) return Deny(bindingIssue.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var row = await ScopedRuns(db).AsNoTracking().SingleOrDefaultAsync(row => row.RunId == runId, cancellationToken);
        if (row is null) return Deny(SyntheticRunIssue.NotFound);
        try { return new(null, await SnapshotAsync(db, row, cancellationToken)); }
        catch (SyntheticRunIntegrityException) { return Deny(SyntheticRunIssue.InputIntegrityMismatch); }
    }

    public async Task<IReadOnlyList<SyntheticRunSnapshot>> ListAsync(SyntheticAuthorizedScope scope, CancellationToken cancellationToken = default)
    {
        if (!MatchesScope(scope)) throw new ArgumentException("Wrong trusted synthetic scope.", nameof(scope));
        await using var db = CreateDb();
        if (await BindingIssueAsync(db, cancellationToken) is not null)
            throw new InvalidOperationException("Synthetic database scope binding unavailable or mismatched.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var rows = await ScopedRuns(db).AsNoTracking().OrderByDescending(row => row.CreatedAt).ThenBy(row => row.RunId).ToArrayAsync(cancellationToken);
        var snapshots = new List<SyntheticRunSnapshot>();
        foreach (var row in rows) snapshots.Add(await SnapshotAsync(db, row, cancellationToken));
        return Array.AsReadOnly(snapshots.ToArray());
    }

    public Task<SyntheticRunCommandResult> AcquireLeaseAsync(SyntheticAuthorizedScope scope, Guid runId, string workerId,
        long? expectedRevision = null, CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "lease", async (db, run, now) =>
    {
        if (!ValidText(workerId)) return (SyntheticRunIssue.InvalidInput, false);
        if (expectedRevision is not null && run.Revision != expectedRevision) return (SyntheticRunIssue.RevisionConflict, false);
        if (run.CancelRequested) return (SyntheticRunIssue.CancellationRequested, false);
        if ((SyntheticRunState)run.State is not (SyntheticRunState.Planned or SyntheticRunState.Running)) return (SyntheticRunIssue.InvalidState, false);
        if (run.LeaseExpiresAt > now) return (SyntheticRunIssue.LeaseUnavailable, false);
        run.LeaseOwner = workerId;
        run.LeaseGeneration = Guid.NewGuid();
        run.LeaseExpiresAt = now + policy.LeaseDuration;
        run.ActiveWorkJson = "[]";
        run.State = (int)SyntheticRunState.Running;
        Advance(db, run, "running", now);
        await Task.CompletedTask;
        return (null, false);
    }, cancellationToken);

    public Task<SyntheticRunCommandResult> HeartbeatAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "heartbeat", (db, run, now) =>
    {
        if (!LiveLease(run, leaseGeneration, now)) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.StaleLease, false));
        if ((SyntheticRunState)run.State != SyntheticRunState.Running) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.InvalidState, false));
        if (run.CancelRequested) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.CancellationRequested, false));
        run.LeaseExpiresAt = now + policy.LeaseDuration;
        return Task.FromResult<(SyntheticRunIssue?, bool)>((null, false));
    }, cancellationToken);

    public Task<SyntheticRunCommandResult> BeginWorkAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        long expectedRevision, IReadOnlyCollection<CoverageKey> keys, CancellationToken cancellationToken = default) =>
        MutateAsync(scope, runId, "work-begin", async (db, run, now) =>
        {
            var issue = Guard(run, leaseGeneration, expectedRevision, now, false);
            if (issue is not null) return (issue, false);
            if (keys is null || keys.Count == 0 || keys.Count > policy.MaxCheckpointItems) return (SyntheticRunIssue.InvalidInput, false);
            var plan = ReadPlan(run);
            var supplied = keys.ToArray();
            if (supplied.Any(key => key is null || !plan.ExpectedKeys.Contains(key)) || supplied.Distinct().Count() != supplied.Length)
                return (SyntheticRunIssue.UnknownWork, false);
            var results = await db.Results.Where(row => row.RunId == runId).ToArrayAsync(cancellationToken);
            if (supplied.Any(key => results.Any(row => row.InventoryId == key.InventoryId && row.CategoryId == key.EvidenceCategory)))
                return (SyntheticRunIssue.ResultConflict, false);
            var active = ReadActive(run);
            if (active.Length != 0 && !active.SequenceEqual(supplied.OrderBy(key => key.InventoryId, StringComparer.Ordinal).ThenBy(key => key.EvidenceCategory, StringComparer.Ordinal)))
                return (SyntheticRunIssue.InvalidState, false);
            if (active.Length != 0) return (null, true);
            run.ActiveWorkJson = JsonSerializer.Serialize(supplied.OrderBy(key => key.InventoryId, StringComparer.Ordinal).ThenBy(key => key.EvidenceCategory, StringComparer.Ordinal).ToArray());
            Advance(db, run, "work-begun", now);
            return (null, false);
        }, cancellationToken);

    public Task<SyntheticRunCommandResult> CheckpointAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        long expectedRevision, IReadOnlyCollection<CoverageItem> results, CancellationToken cancellationToken = default) =>
        MutateAsync(scope, runId, "checkpoint", async (db, run, now) =>
        {
            var issue = Guard(run, leaseGeneration, null, now, true);
            if (issue is not null) return (issue, false);
            if (results is null || results.Count == 0 || results.Count > policy.MaxCheckpointItems) return (SyntheticRunIssue.InvalidInput, false);
            var supplied = results.ToArray();
            var existing = await db.Results.Where(row => row.RunId == runId).ToListAsync(cancellationToken);
            var plan = ReadPlan(run);
            if (!CoverageProgressProjector.Project(plan.ExpectedKeys, supplied).HasProjection) return (SyntheticRunIssue.InvalidCoverage, false);
            var fresh = new List<CoverageItem>();
            foreach (var item in supplied)
            {
                var previous = existing.SingleOrDefault(row => row.InventoryId == item.Key.InventoryId && row.CategoryId == item.Key.EvidenceCategory);
                if (previous is not null)
                {
                    if (previous.ResultDigest != ResultDigest(item)) return (SyntheticRunIssue.ResultConflict, false);
                }
                else fresh.Add(item);
            }
            if (fresh.Count == 0) return (null, true);
            if (run.Revision != expectedRevision) return (SyntheticRunIssue.RevisionConflict, false);
            var active = ReadActive(run);
            if (run.CancelRequested && fresh.Any(item => !active.Contains(item.Key))) return (SyntheticRunIssue.CancellationRequested, false);
            if (!CoverageProgressProjector.Project(plan.ExpectedKeys, existing.Select(Item).Concat(fresh).ToArray()).HasProjection)
                return (SyntheticRunIssue.InvalidCoverage, false);
            foreach (var item in fresh)
            {
                var attempts = await db.Attempts.CountAsync(row => row.RunId == runId && row.InventoryId == item.Key.InventoryId && row.CategoryId == item.Key.EvidenceCategory, cancellationToken);
                if (attempts >= policy.MaxAttempts) return (SyntheticRunIssue.InvalidState, false);
                db.Results.Add(Result(runId, item));
                db.Attempts.Add(Attempt(runId, item.Key, attempts + 1, SyntheticAttemptOutcome.Succeeded, null, leaseGeneration, now));
            }
            run.ActiveWorkJson = JsonSerializer.Serialize(active.Where(key => !fresh.Any(item => item.Key == key)).ToArray());
            run.CheckpointSequence++;
            Advance(db, run, "checkpoint", now);
            return (null, false);
        }, cancellationToken);

    public Task<SyntheticRunCommandResult> RecordAttemptFailureAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        long expectedRevision, CoverageKey key, string reasonCode, CancellationToken cancellationToken = default) =>
        MutateAsync(scope, runId, "attempt", async (db, run, now) =>
        {
            var issue = Guard(run, leaseGeneration, expectedRevision, now, false);
            if (issue is not null) return (issue, false);
            if (key is null || !ValidText(reasonCode)) return (SyntheticRunIssue.InvalidInput, false);
            if (!ReadPlan(run).ExpectedKeys.Contains(key)) return (SyntheticRunIssue.UnknownWork, false);
            if (await db.Results.AnyAsync(row => row.RunId == runId && row.InventoryId == key.InventoryId && row.CategoryId == key.EvidenceCategory, cancellationToken))
                return (SyntheticRunIssue.ResultConflict, false);
            var number = 1 + await db.Attempts.CountAsync(row => row.RunId == runId && row.InventoryId == key.InventoryId && row.CategoryId == key.EvidenceCategory, cancellationToken);
            if (number > policy.MaxAttempts) return (SyntheticRunIssue.InvalidState, false);
            var exhausted = number == policy.MaxAttempts;
            db.Attempts.Add(Attempt(runId, key, number, exhausted ? SyntheticAttemptOutcome.Exhausted : SyntheticAttemptOutcome.Failed, reasonCode, leaseGeneration, now));
            if (exhausted)
            {
                db.Results.Add(Result(runId, new CoverageItem(key, CoverageState.Error, reasonCode, "synthetic-worker")));
                run.CheckpointSequence++;
            }
            run.ActiveWorkJson = JsonSerializer.Serialize(ReadActive(run).Where(item => item != key).ToArray());
            Advance(db, run, exhausted ? "attempt-exhausted" : "attempt-failed", now);
            return (null, false);
        }, cancellationToken);

    public Task<SyntheticRunCommandResult> CompleteCoverageAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        long expectedRevision, CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "coverage", async (db, run, now) =>
    {
        var issue = Guard(run, leaseGeneration, expectedRevision, now, false);
        if (issue is not null) return (issue, false);
        var plan = ReadPlan(run);
        var results = (await db.Results.Where(row => row.RunId == runId).ToArrayAsync(cancellationToken)).Select(Item).ToArray();
        var completion = CoverageCompletionProjector.Project(plan.ExpectedKeys, results);
        if (!completion.HasProjection) return (SyntheticRunIssue.InvalidCoverage, false);
        var summary = new SyntheticCoverageStageSummary(completion.Kind!.Value,
            CoverageCountProjector.Project(plan.ExpectedKeys, results).Counts!,
            ExecutableCoverageProjector.Project(plan.ExpectedKeys, results).Measure!,
            CoverageLimitationProjector.Project(plan.ExpectedKeys, results).Limitations!);
        run.SummaryJson = JsonSerializer.Serialize(summary);
        run.State = (int)SyntheticRunState.Scoring;
        ClearLease(run);
        run.ActiveWorkJson = "[]";
        Advance(db, run, "coverage-stage-completed-scoring-paused", now);
        return (null, false);
    }, cancellationToken);

    public Task<SyntheticRunCommandResult> RequestCancelAsync(SyntheticAuthorizedScope scope, Guid runId, long expectedRevision,
        CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "cancel", (db, run, now) =>
    {
        if (run.Revision != expectedRevision) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.RevisionConflict, false));
        if ((SyntheticRunState)run.State is not (SyntheticRunState.Planned or SyntheticRunState.Running))
            return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.InvalidState, false));
        if (run.CancelRequested) return Task.FromResult<(SyntheticRunIssue?, bool)>((null, true));
        run.CancelRequested = true;
        Advance(db, run, "cancellation-requested", now);
        return Task.FromResult<(SyntheticRunIssue?, bool)>((null, false));
    }, cancellationToken);

    public Task<SyntheticRunCommandResult> FinalizeCancellationAsync(SyntheticAuthorizedScope scope, Guid runId, long expectedRevision,
        Guid? leaseGeneration = null, CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "cancel-finalize", (db, run, now) =>
    {
        if ((SyntheticRunState)run.State == SyntheticRunState.Cancelled) return Task.FromResult<(SyntheticRunIssue?, bool)>((null, true));
        if (run.Revision != expectedRevision) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.RevisionConflict, false));
        if (!run.CancelRequested || (SyntheticRunState)run.State is not (SyntheticRunState.Planned or SyntheticRunState.Running))
            return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.InvalidState, false));
        if (run.LeaseExpiresAt > now && (leaseGeneration is null || !LiveLease(run, leaseGeneration.Value, now)))
            return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.LeaseUnavailable, false));
        run.State = (int)SyntheticRunState.Cancelled;
        run.ActiveWorkJson = "[]";
        ClearLease(run);
        Advance(db, run, "cancelled", now);
        return Task.FromResult<(SyntheticRunIssue?, bool)>((null, false));
    }, cancellationToken);

    public Task<SyntheticRunCommandResult> ReleaseLeaseAsync(SyntheticAuthorizedScope scope, Guid runId, Guid leaseGeneration,
        CancellationToken cancellationToken = default) => MutateAsync(scope, runId, "release", (db, run, now) =>
    {
        if (!LiveLease(run, leaseGeneration, now)) return Task.FromResult<(SyntheticRunIssue?, bool)>((SyntheticRunIssue.StaleLease, false));
        ClearLease(run);
        run.ActiveWorkJson = "[]";
        Advance(db, run, "lease-released", now);
        return Task.FromResult<(SyntheticRunIssue?, bool)>((null, false));
    }, cancellationToken);

    public async Task<IReadOnlyList<SyntheticRunOutboxEvent>> ReadOutboxAsync(SyntheticAuthorizedScope scope, Guid? runId = null,
        CancellationToken cancellationToken = default)
    {
        if (!MatchesScope(scope)) throw new ArgumentException("Wrong trusted synthetic scope.", nameof(scope));
        await using var db = CreateDb();
        if (await BindingIssueAsync(db, cancellationToken) is not null)
            throw new InvalidOperationException("Synthetic database scope binding unavailable or mismatched.");
        var scopedRunIds = ScopedRuns(db).Select(row => row.RunId);
        var rows = await db.Outbox.AsNoTracking().Where(row => scopedRunIds.Contains(row.RunId) &&
            (runId == null || row.RunId == runId)).OrderBy(row => row.CreatedAt).ThenBy(row => row.EventId).ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(row => new SyntheticRunOutboxEvent(row.EventId, row.RunId, row.RunRevision,
            row.SchemaVersion, row.MinimumWorkerVersion, row.Kind, row.CreatedAt, row.DispatchedAt)).ToArray());
    }

    /// <summary>Local at-least-once receipt demonstration, not a Service Bus adapter.</summary>
    public async Task<SyntheticOutboxDeliveryResult> DeliverOutboxAsync(SyntheticAuthorizedScope scope, Guid eventId, string consumerId,
        CancellationToken cancellationToken = default)
    {
        if (!MatchesScope(scope)) return new(SyntheticRunIssue.WrongScope, false);
        if (!ValidText(consumerId)) return new(SyntheticRunIssue.InvalidInput, false);
        await using var db = CreateDb();
        var bindingIssue = await BindingIssueAsync(db, cancellationToken);
        if (bindingIssue is not null) return new(bindingIssue, false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM synthetic_assessment.outbox WHERE event_id={eventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (message is null || !await ScopedRuns(db).AnyAsync(row => row.RunId == message.RunId, cancellationToken)) return new(SyntheticRunIssue.NotFound, false);
        if (message.SchemaVersion != WorkSchemaVersion || message.MinimumWorkerVersion != MinimumWorkerVersion)
            return new(SyntheticRunIssue.UnknownEventVersion, false);
        if (await db.Inbox.AnyAsync(row => row.EventId == eventId && row.ConsumerId == consumerId, cancellationToken)) return new(null, true);
        var now = await ClockAsync(db, cancellationToken);
        db.Inbox.Add(new InboxRow { ConsumerId = consumerId, EventId = eventId, ReceivedAt = now });
        message.DispatchedAt ??= now;
        await db.SaveChangesAsync(cancellationToken);
        await BeforeCommitAsync("outbox", message.RunId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(null, false);
    }

    private async Task<SyntheticRunCommandResult> MutateAsync(SyntheticAuthorizedScope scope, Guid runId, string operation,
        Func<SyntheticRunDbContext, RunRow, DateTimeOffset, Task<(SyntheticRunIssue? Issue, bool AlreadyApplied)>> action,
        CancellationToken cancellationToken)
    {
        if (!MatchesScope(scope)) return Deny(SyntheticRunIssue.WrongScope);
        await using var db = CreateDb();
        var bindingIssue = await BindingIssueAsync(db, cancellationToken);
        if (bindingIssue is not null) return Deny(bindingIssue.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (runId == Guid.Empty) return Deny(SyntheticRunIssue.NotFound);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(db.Database.GetDbConnection(),
            transaction.GetDbTransaction(), scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        var run = await db.Runs.FromSqlInterpolated($"SELECT * FROM synthetic_assessment.runs WHERE run_id={runId} AND customer_id={trustedScope.CustomerId} AND project_id={trustedScope.ProjectId} AND environment_id={trustedScope.EnvironmentId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (run is null) return Deny(SyntheticRunIssue.NotFound);
        try
        {
            ValidateInputDigest(run);
            var originalLeaseGeneration = run.LeaseGeneration;
            var originalLeaseExpiresAt = run.LeaseExpiresAt;
            var now = await ClockAsync(db, cancellationToken);
            var outcome = await action(db, run, now);
            if (outcome.Issue is not null) return Deny(outcome.Issue.Value);
            if (!outcome.AlreadyApplied)
            {
                run.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                var snapshot = await SnapshotAsync(db, run, cancellationToken);
                await BeforeCommitAsync(operation, runId, cancellationToken);
                var commitTime = await ClockAsync(db, cancellationToken);
                if (operation is "heartbeat" or "work-begin" or "checkpoint" or "attempt" or "coverage" or "release")
                {
                    if (originalLeaseGeneration is null || originalLeaseExpiresAt <= commitTime)
                        return Deny(SyntheticRunIssue.StaleLease);
                }
                if (operation == "lease" && run.LeaseExpiresAt <= commitTime)
                    return Deny(SyntheticRunIssue.StaleLease);
                await transaction.CommitAsync(cancellationToken);
                return new(null, snapshot);
            }
            return new(null, await SnapshotAsync(db, run, cancellationToken), outcome.AlreadyApplied);
        }
        catch (SyntheticRunIntegrityException) { return Deny(SyntheticRunIssue.InputIntegrityMismatch); }
        catch (DbUpdateConcurrencyException) { return Deny(SyntheticRunIssue.RevisionConflict); }
    }

    private async Task<SyntheticRunSnapshot> SnapshotAsync(SyntheticRunDbContext db, RunRow run, CancellationToken cancellationToken)
    {
        ValidateInputDigest(run);
        var plan = ReadPlan(run);
        var results = (await db.Results.AsNoTracking().Where(row => row.RunId == run.RunId).ToArrayAsync(cancellationToken))
            .OrderBy(row => row.InventoryId, StringComparer.Ordinal).ThenBy(row => row.CategoryId, StringComparer.Ordinal).Select(row =>
            {
                var item = Item(row);
                if (row.ResultDigest != ResultDigest(item)) throw new SyntheticRunIntegrityException("Stored result digest mismatch.");
                return item;
            }).ToArray();
        if (!CoverageProgressProjector.Project(plan.ExpectedKeys, results).HasProjection)
            throw new SyntheticRunIntegrityException("Stored coverage is invalid.");
        var attempts = (await db.Attempts.AsNoTracking().Where(row => row.RunId == run.RunId).ToArrayAsync(cancellationToken))
            .OrderBy(row => row.InventoryId, StringComparer.Ordinal).ThenBy(row => row.CategoryId, StringComparer.Ordinal).ThenBy(row => row.AttemptNumber)
            .Select(row => new SyntheticRunAttempt(new(row.InventoryId, row.CategoryId), row.AttemptNumber,
                (SyntheticAttemptOutcome)row.Outcome, row.ReasonCode, row.LeaseGeneration, row.CreatedAt)).ToArray();
        var versions = JsonSerializer.Deserialize<SyntheticRunInputVersions>(run.VersionsJson)
            ?? throw new SyntheticRunIntegrityException("Stored input versions missing.");
        var summary = run.SummaryJson is null ? null : JsonSerializer.Deserialize<SyntheticCoverageStageSummary>(run.SummaryJson);
        if (summary is not null) summary = summary with
        {
            Counts = Array.AsReadOnly(summary.Counts.ToArray()),
            Limitations = Array.AsReadOnly(summary.Limitations.ToArray())
        };
        return new(run.RunId, new(run.CustomerId, run.ProjectId, run.EnvironmentId), run.BaselineCatalogId, run.ProfileCatalogId,
            (SyntheticRunState)run.State, run.Revision, run.CancelRequested, run.CheckpointSequence, run.InputDigest, versions, plan,
            Array.AsReadOnly(results), Array.AsReadOnly(attempts),
            run.LeaseGeneration is null ? null : new(run.LeaseOwner!, run.LeaseGeneration.Value, run.LeaseExpiresAt!.Value),
            summary, run.CreatedAt, run.UpdatedAt, Array.AsReadOnly(ReadActive(run)), await ClockAsync(db, cancellationToken));
    }

    private SyntheticRunDbContext CreateDb() => new(options);
    private async Task<SyntheticRunIssue?> BindingIssueAsync(SyntheticRunDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = NativeCommand(db, "SELECT customer_id,project_id,environment_id FROM synthetic_assessment.data_plane_scope WHERE singleton=true");
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return SyntheticRunIssue.InvalidState;
            return reader.GetString(0) == trustedScope.CustomerId && reader.GetString(1) == trustedScope.ProjectId &&
                reader.GetString(2) == trustedScope.EnvironmentId ? null : SyntheticRunIssue.WrongScope;
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            return SyntheticRunIssue.InvalidState;
        }
    }
    private IQueryable<RunRow> ScopedRuns(SyntheticRunDbContext db) => db.Runs.Where(row =>
        row.CustomerId == trustedScope.CustomerId && row.ProjectId == trustedScope.ProjectId && row.EnvironmentId == trustedScope.EnvironmentId);
    private bool MatchesScope(SyntheticAuthorizedScope? scope) => scope is not null && scope == trustedScope;
    private static bool ValidScope(SyntheticAuthorizedScope scope) => ValidText(scope.CustomerId) && ValidText(scope.ProjectId) && ValidText(scope.EnvironmentId);
    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200;
    private static bool ValidVersions(SyntheticRunInputVersions? versions) => versions is not null &&
        ValidText(versions.ProfileVersion) && (versions.DesiredOutcomeVersion is null || ValidText(versions.DesiredOutcomeVersion)) &&
        ValidText(versions.ScoringAlgorithmVersion) && ValidText(versions.AiPolicyVersion) && ValidText(versions.PromptVersion) &&
        ValidText(versions.ModelVersion) && ValidText(versions.ApplicationVersion) && versions.WorkSchemaVersion == WorkSchemaVersion &&
        versions.ScriptedResultsDigest is { Length: 64 } && versions.ScriptedResultsDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        (versions.AnalysisFixtureDigest is null || versions.AnalysisFixtureDigest is { Length: 64 } &&
            versions.AnalysisFixtureDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) &&
        (versions.MaturityFixtureDigest is null || versions.MaturityFixtureDigest is { Length: 64 } &&
            versions.MaturityFixtureDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) &&
        (versions.AiPreviewFixtureDigest is null || versions.AiPreviewFixtureDigest is { Length: 64 } &&
            versions.AiPreviewFixtureDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) &&
        (versions.FixPackageTemplateDigest is null || versions.FixPackageTemplateDigest is { Length: 64 } &&
            versions.FixPackageTemplateDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) &&
        (versions.FixReviewContractDigest is null || versions.FixReviewContractDigest is { Length: 64 } &&
            versions.FixReviewContractDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    private static bool LiveLease(RunRow run, Guid generation, DateTimeOffset now) => generation != Guid.Empty &&
        run.LeaseGeneration == generation && run.LeaseExpiresAt > now;
    private static SyntheticRunIssue? Guard(RunRow run, Guid generation, long? revision, DateTimeOffset now, bool allowCancellation)
    {
        if (!LiveLease(run, generation, now)) return SyntheticRunIssue.StaleLease;
        if ((SyntheticRunState)run.State != SyntheticRunState.Running) return SyntheticRunIssue.InvalidState;
        if (revision is not null && run.Revision != revision) return SyntheticRunIssue.RevisionConflict;
        if (!allowCancellation && run.CancelRequested) return SyntheticRunIssue.CancellationRequested;
        return null;
    }
    private static void ClearLease(RunRow run) { run.LeaseOwner = null; run.LeaseGeneration = null; run.LeaseExpiresAt = null; }
    private static void Advance(SyntheticRunDbContext db, RunRow run, string kind, DateTimeOffset now)
    {
        run.Revision++;
        AddEvent(db, run, kind, now);
    }
    private static void AddEvent(SyntheticRunDbContext db, RunRow run, string kind, DateTimeOffset now) => db.Outbox.Add(new OutboxRow
    {
        EventId = Guid.NewGuid(),
        RunId = run.RunId,
        RunRevision = run.Revision,
        SchemaVersion = WorkSchemaVersion,
        MinimumWorkerVersion = MinimumWorkerVersion,
        Kind = kind,
        CreatedAt = now
    });
    private Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken) =>
        observer?.BeforeCommitAsync(operation, runId, cancellationToken) ?? Task.CompletedTask;
    private static SyntheticRunCommandResult Deny(SyntheticRunIssue issue) => new(issue, null);
    private static CoverageItem Item(ResultRow row) => new(new(row.InventoryId, row.CategoryId), (CoverageState)row.State,
        row.ReasonCode, row.ResponsibleStage, row.EvidenceReference);
    private static ResultRow Result(Guid runId, CoverageItem item) => new()
    {
        RunId = runId,
        InventoryId = item.Key.InventoryId,
        CategoryId = item.Key.EvidenceCategory,
        State = (int)item.State,
        ReasonCode = item.ReasonCode,
        ResponsibleStage = item.ResponsibleStage,
        EvidenceReference = item.EvidenceReference,
        ResultDigest = ResultDigest(item)
    };
    private static string ResultDigest(CoverageItem item) => SyntheticRunMigration.Hash(JsonSerializer.Serialize(item));
    private static AttemptRow Attempt(Guid runId, CoverageKey key, int number, SyntheticAttemptOutcome outcome, string? reason, Guid generation, DateTimeOffset now) =>
        new()
        {
            RunId = runId,
            InventoryId = key.InventoryId,
            CategoryId = key.EvidenceCategory,
            AttemptNumber = number,
            Outcome = (int)outcome,
            ReasonCode = reason,
            LeaseGeneration = generation,
            CreatedAt = now
        };
    private static SyntheticInventoryPlan ReadPlan(RunRow run)
    {
        var plan = JsonSerializer.Deserialize<SyntheticInventoryPlan>(run.PlanJson)
            ?? throw new SyntheticRunIntegrityException("Stored plan missing.");
        return plan with
        {
            CapabilityLock = plan.CapabilityLock with { Modules = Array.AsReadOnly(plan.CapabilityLock.Modules.ToArray()) },
            Objects = Array.AsReadOnly(plan.Objects.Select(item => item with { Categories = Array.AsReadOnly(item.Categories.ToArray()) }).ToArray()),
            ExpectedKeys = Array.AsReadOnly(plan.ExpectedKeys.ToArray()),
            DeclaredItems = Array.AsReadOnly(plan.DeclaredItems.ToArray())
        };
    }
    private static CoverageKey[] ReadActive(RunRow run) => JsonSerializer.Deserialize<CoverageKey[]>(run.ActiveWorkJson)
        ?? throw new SyntheticRunIntegrityException("Stored in-flight checkpoint is invalid.");
    // Pure local input-lock recipe; a digest does not confer source or actor authority.
    public static string ComputeInputDigest(SyntheticInventoryPlan plan, SyntheticRunInputVersions versions, string baselineId, string profileId) =>
        InputDigest(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(versions), baselineId, profileId);

    private static string InputDigest(string planJson, string versionsJson, string baselineId, string profileId)
    {
        var values = new StringBuilder();
        foreach (var value in new[] { "synthetic-run-input-lock-v1", planJson, versionsJson, baselineId, profileId })
            values.Append(value.Length).Append(':').Append(value);
        return SyntheticRunMigration.Hash(values.ToString());
    }
    private static void ValidateInputDigest(RunRow run)
    {
        if (run.InputDigest != InputDigest(run.PlanJson, run.VersionsJson, run.BaselineCatalogId, run.ProfileCatalogId))
            throw new SyntheticRunIntegrityException("Stored synthetic input digest mismatch.");
    }
    private static NpgsqlCommand NativeCommand(SyntheticRunDbContext db, string sql) => new(sql,
        (NpgsqlConnection)db.Database.GetDbConnection(), (NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction());
    private static async Task<DateTimeOffset> ClockAsync(SyntheticRunDbContext db, CancellationToken cancellationToken)
    {
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = NativeCommand(db, "SELECT clock_timestamp()");
        var value = (DateTime)(await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Database clock unavailable."));
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
