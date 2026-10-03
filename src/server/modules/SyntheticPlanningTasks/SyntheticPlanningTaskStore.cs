using System.Collections.Immutable;
using System.Text.Json;
using Npgsql;
using SyntheticSourceFence;

namespace SyntheticPlanningTasks;

/// <summary>Scoped fictional planning records only; never remediation, execution or customer authority.</summary>
public sealed class SyntheticPlanningTaskStore
{
    private readonly string connectionString;
    private readonly PlanningTaskScope scope;
    private readonly PlanningTaskSourceReader readSource;
    private readonly ISyntheticPlanningTaskCommitObserver? observer;
    public SyntheticPlanningTaskStore(string connectionString, PlanningTaskScope trustedScope, PlanningTaskSourceReader readSource, ISyntheticPlanningTaskCommitObserver? observer = null)
    {
        if (trustedScope != PlanningTaskScope.Fixed) throw new ArgumentException("Only the fixed fictional scope is supported.", nameof(trustedScope));
        ArgumentNullException.ThrowIfNull(readSource);
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database is null || !connection.Database.StartsWith("iga_synthetic_cycle14_", StringComparison.Ordinal))
            throw new ArgumentException("Planning tasks require a loopback iga_synthetic_cycle14_ database.", nameof(connectionString));
        connection.CommandTimeout = 15; this.connectionString = connection.ConnectionString;
        scope = trustedScope; this.readSource = readSource; this.observer = observer;
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    { await using var connection = await Open(cancellationToken); await SyntheticPlanningTaskMigration.InitializeAsync(connection, scope, cancellationToken); }
    public async Task<PlanningTaskReadResult> ReadAsync(Guid runId, PlanningTaskAuthority authority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty) return new(PlanningTaskIssue.InvalidInput, null);
        if (PlanningTaskPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Fence(connection, transaction, runId, cancellationToken);
        if (await SyntheticPlanningTaskMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var captured = await Capture(connection, transaction, runId, authority, cancellationToken);
            if (captured.Issue is PlanningTaskIssue.Denied or PlanningTaskIssue.WrongScope or PlanningTaskIssue.IntegrityMismatch) return new(captured.Issue, null);
            var loaded = await Load(connection, transaction, runId, authority, cancellationToken);
            var source = captured.Source;
            if (source is not null && !Available(loaded, source)) source = null;
            if (source is not null) CheckCurrent(loaded, source);
            var entries = loaded.Tasks.Select(task => Project(task, source, authority)).ToImmutableArray();
            var options = source is null ? [] : source.Options.Select(option => option with
            {
                CanCreate = option.CanCreate && !loaded.Tasks.Any(task => task.Identity.TaskId == option.Identity.TaskId) &&
                    PlanningTaskPolicy.Authorize(authority, scope, PlanningTaskAction.Create, option.Identity.CategoryId) is null
            }).ToImmutableArray();
            await transaction.CommitAsync(cancellationToken);
            return new(source is null ? PlanningTaskIssue.SourceUnavailable : null, new(source?.Binding, authority.ActorId, options, entries));
        }
        catch (TaskAuthorizationException) { return new(PlanningTaskIssue.Denied, null); }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
    public async Task<PlanningTaskApplyResult> ApplyAsync(Guid runId, string taskId, PlanningTaskAuthority authority, PlanningTaskCommand command, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !PlanningTaskPolicy.ValidDigest(taskId) || PlanningTaskPolicy.ValidateCommand(command) is not null) return new(PlanningTaskIssue.InvalidInput, null);
        if (PlanningTaskPolicy.Authorize(authority, scope, PlanningTaskPolicy.Grant(command.Kind)) is { } denied) return new(denied, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Fence(connection, transaction, runId, cancellationToken);
        if (await SyntheticPlanningTaskMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var capture = await Capture(connection, transaction, runId, authority, cancellationToken);
            if (!capture.Succeeded) return new(capture.Issue ?? PlanningTaskIssue.SourceUnavailable, null);
            var source = capture.Source!;
            var loaded = await Load(connection, transaction, runId, authority, cancellationToken);
            if (!Available(loaded, source)) return new(PlanningTaskIssue.SourceUnavailable, null);
            CheckCurrent(loaded, source);
            var existing = loaded.Tasks.SingleOrDefault(item => item.Identity.TaskId == taskId);
            if (existing is not null && existing.Assignee != authority.ActorId) return new(PlanningTaskIssue.Denied, null);
            var semantic = PlanningTaskCanonical.CommandDigest(scope, runId, taskId, authority.ActorId, command);
            // Module-wide accepted UUID lookup precedes new expected-source, duplicate and transition conditions.
            var replay = loaded.Tasks.SelectMany(task => task.Events).SingleOrDefault(item => item.Event.EventId == command.EventId);
            if (replay is not null) return replay.CommandDigest == semantic ? new(null, replay.Receipt, true) : new(PlanningTaskIssue.EventConflict, null);
            var option = source.Options.SingleOrDefault(item => item.Identity.TaskId == taskId);
            if (option is null) return new(PlanningTaskIssue.NotFound, null);
            if (command.ExpectedSourceDigest != source.Binding.ArtifactSource.SourceDigest || !Equal(command.ExpectedAttestations, option.CurrentAttestations)) return new(PlanningTaskIssue.SourceConflict, null);
            if (command.Kind == PlanningTaskKind.Create && existing is not null) return new(null, null, false, taskId);
            if (command.Kind == PlanningTaskKind.Create)
            { if (!option.CanCreate) return new(PlanningTaskIssue.InvalidState, null); }
            else
            {
                if (existing is null) return new(PlanningTaskIssue.NotFound, null);
                if (command.ExpectedRevision != existing.Events.Length) return new(PlanningTaskIssue.RevisionConflict, null);
                var projected = Project(existing, source, authority);
                if (PlanningTaskPolicy.Transition(projected.Status, projected.Freshness, option.FindingState, option.CanCreate, command.Kind) is { } transition) return new(transition, null);
            }
            var previous = existing?.Events.LastOrDefault()?.Event;
            var priorRevision = previous?.Revision ?? 0;
            if (priorRevision >= PlanningTaskPolicy.MaximumRevision) return new(PlanningTaskIssue.RevisionOverflow, null);
            await Register(connection, transaction, source, option.Identity, authority.ActorId, existing is null, cancellationToken);
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
            var now = (DateTime)(await clock.ExecuteScalarAsync(cancellationToken))!;
            var planningId = command.Kind is PlanningTaskKind.Create or PlanningTaskKind.ReconfirmPlan ? command.EventId : previous!.PlanningEventId;
            var item = new PlanningTaskEvent(command.EventId, priorRevision + 1, command.Kind, authority.ActorId, ["Consultant"], new(now), command.Reason,
                source.Binding, option.CurrentAttestations, PlanningTaskPolicy.After(previous?.RecordedStatus ?? PlanningTaskStatus.Planned, command.Kind), planningId);
            var receipt = Receipt(runId, taskId, item);
            await Execute(connection, transaction, """
                INSERT INTO synthetic_planning_tasks.events VALUES (@run,@task,@event,@expected,@revision,@proof,@commandJson,@commandDigest,@json,@digest,@after,@time)
                """, cancellationToken, ("run", runId), ("task", taskId), ("event", item.EventId), ("expected", priorRevision), ("revision", item.Revision), ("proof", Proof(source)),
                ("commandJson", PlanningTaskCanonical.Json(command)), ("commandDigest", semantic), ("json", PlanningTaskCanonical.Json(item)),
                ("digest", PlanningTaskCanonical.Digest(item)), ("after", PlanningTaskCanonical.Json(Current(item))), ("time", now));
            await Execute(connection, transaction, "INSERT INTO synthetic_planning_tasks.receipts VALUES (@run,@event,@json,@digest)", cancellationToken,
                ("run", runId), ("event", item.EventId), ("json", PlanningTaskCanonical.Json(receipt)), ("digest", PlanningTaskCanonical.Digest(receipt)));
            await Execute(connection, transaction, "UPDATE synthetic_planning_tasks.task_current SET revision=@revision,current_json=@json WHERE run_id=@run AND task_id=@task", cancellationToken,
                ("revision", item.Revision), ("json", PlanningTaskCanonical.Json(Current(item))), ("run", runId), ("task", taskId));
            if (observer is not null) await observer.BeforeCommitAsync("apply", runId, taskId, command.EventId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, receipt);
        }
        catch (TaskAuthorizationException) { return new(PlanningTaskIssue.Denied, null); }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
    private Task Fence(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, CancellationToken ct) =>
        SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, ct);
    private async Task<PlanningTaskSourceResult> Capture(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, PlanningTaskAuthority authority, CancellationToken ct)
    {
        var captured = await readSource(connection, transaction, runId, authority, ct);
        if (captured is null || !captured.Succeeded) return new(captured?.Issue ?? PlanningTaskIssue.SourceUnavailable, null);
        var source = captured.Source!;
        var rebuilt = PlanningTaskSourceBuilder.Build(source.Packages, source.Artifacts);
        if (!rebuilt.Succeeded || !Equal(source.Binding, rebuilt.Source!.Binding) || !Equal(source.Options, rebuilt.Source.Options)) return new(PlanningTaskIssue.IntegrityMismatch, null);
        if (source.Binding.ArtifactSource.Scope != new SyntheticFixReview.ArtifactReviewScope(scope.CustomerId, scope.ProjectId, scope.EnvironmentId)) return new(PlanningTaskIssue.WrongScope, null);
        if (source.Binding.ArtifactSource.RunId != runId) return new(PlanningTaskIssue.SourceConflict, null);
        if (source.Artifacts.ActorId != authority.ActorId) return new(PlanningTaskIssue.Denied, null);
        foreach (var finding in source.Packages.Guidance.Findings)
            if (PlanningTaskPolicy.Authorize(authority, scope, category: finding.CategoryId) is { } denied) return new(denied, null);
        return rebuilt;
    }
    private sealed record StoredCurrent(long Revision, Guid? EventId, PlanningTaskStatus Status, Guid? PlanningEventId);
    private sealed record StoredEvent(PlanningTaskEvent Event, PlanningTaskReceipt Receipt, string CommandDigest, string ProofDigest);
    private sealed record StoredTask(PlanningTaskIdentity Identity, string Assignee, ImmutableArray<StoredEvent> Events);
    private sealed record Loaded(Dictionary<string, PlanningTaskSource> Sources, ImmutableArray<StoredTask> Tasks);
    private static StoredCurrent Current(PlanningTaskEvent item) => new(item.Revision, item.EventId, item.RecordedStatus, item.PlanningEventId);
    private static PlanningTaskReceipt Receipt(Guid runId, string taskId, PlanningTaskEvent item) =>
        new("synthetic-planning-task-receipt-v1", item.EventId, runId, taskId, item.Kind, item.Revision, item.ActorId, item.RecordedAtUtc, item.Source.ArtifactSource.SourceDigest);
    private static string Proof(PlanningTaskSource source) => PlanningTaskCanonical.Digest(new { source.Binding, artifactSnapshot = source.Artifacts });
    private static bool Equal<T>(T first, T second) => PlanningTaskCanonical.Json(first) == PlanningTaskCanonical.Json(second);
    private static bool IntegrityFailure(Exception exception) => exception is PlanningTaskIntegrityException or JsonException or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException or KeyNotFoundException;
    private async Task<NpgsqlConnection> Open(CancellationToken ct)
    { var connection = new NpgsqlConnection(connectionString); try { await connection.OpenAsync(ct); return connection; } catch { await connection.DisposeAsync(); throw; } }
    private static Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        SyntheticPlanningTaskMigration.Execute(connection, transaction, sql, ct, parameters);
    private static NpgsqlCommand Query(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid runId)
    { var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("run", runId); return command; }
    private static PlanningTaskFreshness Freshness(PlanningTaskEvent plan, PlanningTaskSource source, PlanningTaskOption option) =>
        Equal(plan.Source, source.Binding) && Equal(plan.Attestations, option.CurrentAttestations) ? PlanningTaskFreshness.CurrentPlan : PlanningTaskFreshness.NeedsReconfirmation;
    private PlanningTaskEntry Project(StoredTask task, PlanningTaskSource? source, PlanningTaskAuthority authority)
    {
        var history = task.Events.Select(item => item.Event).ToImmutableArray();
        var latest = history[^1]; var plan = history.Single(item => item.EventId == latest.PlanningEventId);
        var option = source?.Options.SingleOrDefault(item => item.Identity.TaskId == task.Identity.TaskId);
        var freshness = source is null || option is null ? PlanningTaskFreshness.SourceUnavailable : Freshness(plan, source, option);
        bool Can(PlanningTaskKind kind) => source is not null && option is not null && task.Assignee == authority.ActorId &&
            PlanningTaskPolicy.Authorize(authority, scope, PlanningTaskPolicy.Grant(kind), task.Identity.CategoryId) is null &&
            PlanningTaskPolicy.Transition(latest.RecordedStatus, freshness, option.FindingState, option.CanCreate, kind) is null;
        return new(task.Identity, task.Assignee, latest.Revision, latest.RecordedStatus, freshness, history[0], plan, history,
            Can(PlanningTaskKind.ReconfirmPlan), Can(PlanningTaskKind.StartProgress), Can(PlanningTaskKind.ReturnToPlanned), Can(PlanningTaskKind.Complete), Can(PlanningTaskKind.Cancel), Can(PlanningTaskKind.Reopen), Can(PlanningTaskKind.Comment));
    }
    private static bool Available(Loaded loaded, PlanningTaskSource current) => loaded.Tasks.All(task => current.Options.Any(option => Equal(option.Identity, task.Identity)));
    private static void CheckCurrent(Loaded loaded, PlanningTaskSource current)
    {
        foreach (var old in loaded.Sources.Values)
        {
            CheckSources(current, old);
        }
    }
    private static void CheckSources(PlanningTaskSource newer, PlanningTaskSource older)
    {
        if (!SourceDominates(newer, older)) throw new PlanningTaskIntegrityException();
    }
    private static bool SourceDominates(PlanningTaskSource newer, PlanningTaskSource older)
    {
        var first = newer.Binding.ArtifactSource; var second = older.Binding.ArtifactSource;
        if (!PlanningTaskSourceBuilder.Compatible(first, second) || !PlanningTaskSourceBuilder.Dominates(first, second) ||
            !Equal(newer.Options.Select(option => option.Identity).ToImmutableArray(), older.Options.Select(option => option.Identity).ToImmutableArray())) return false;
        // Artifact histories advance even when the complete package binding stays unchanged.
        // Every retained proof must preserve complete prior events, including reasons and actor/time/source fields.
        var actualEntries = newer.Artifacts.Entries.ToDictionary(entry => entry.Artifact.ArtifactId, StringComparer.Ordinal);
        foreach (var previous in older.Artifacts.Entries)
            if (!actualEntries.TryGetValue(previous.Artifact.ArtifactId, out var actual) || actual.Revision < previous.Revision ||
                !Equal(actual.History.Take(previous.History.Length).ToImmutableArray(), previous.History)) return false;
        return true;
    }
    private async Task<Loaded> Load(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, PlanningTaskAuthority authority, CancellationToken ct)
    {
        var seeds = new List<(PlanningTaskIdentity Identity, string Actor, string Proof)>();
        await using (var command = Query(connection, transaction, "SELECT task_id,identity_json,identity_digest,assignee_id,creation_proof_digest FROM synthetic_planning_tasks.task_seeds WHERE run_id=@run ORDER BY task_id COLLATE \"C\"", runId))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                // Immutable seed ownership is checked before task-owned source/history content or UUID dispatch.
                if (reader.GetString(3) != authority.ActorId) throw new TaskAuthorizationException();
                var identity = PlanningTaskCanonical.Parse<PlanningTaskIdentity>(reader.GetString(1));
                if (identity.TaskId != reader.GetString(0) || reader.GetString(1) != PlanningTaskCanonical.Json(identity) || reader.GetString(2) != PlanningTaskCanonical.Digest(identity) ||
                    string.IsNullOrWhiteSpace(reader.GetString(3))) throw new PlanningTaskIntegrityException();
                if (PlanningTaskPolicy.Authorize(authority, scope, category: identity.CategoryId) is not null) throw new TaskAuthorizationException();
                seeds.Add((identity, reader.GetString(3), reader.GetString(4)));
            }
        var versions = new Dictionary<string, PlanningTaskSource>(StringComparer.Ordinal);
        await using (var command = Query(connection, transaction, "SELECT proof_digest,customer_id,project_id,environment_id,canonical_package,binding_json,artifact_snapshot_json FROM synthetic_planning_tasks.source_versions WHERE run_id=@run", runId))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var source = PlanningTaskSourceBuilder.Restore(reader.GetString(4), reader.GetString(6));
                if (reader.GetString(0) != Proof(source) || source.Binding.ArtifactSource.RunId != runId || reader.GetString(1) != scope.CustomerId || reader.GetString(2) != scope.ProjectId ||
                    reader.GetString(3) != scope.EnvironmentId || reader.GetString(5) != PlanningTaskCanonical.Json(source.Binding)) throw new PlanningTaskIntegrityException();
                versions.Add(reader.GetString(0), source);
            }
        // Validate retained source monotonicity independently even when no current dependency proof is available.
        foreach (var first in versions.Values) foreach (var second in versions.Values)
            if (!SourceDominates(first, second) && !SourceDominates(second, first)) throw new PlanningTaskIntegrityException();
        var tasks = ImmutableArray.CreateBuilder<StoredTask>(); var usedProofs = new HashSet<string>(StringComparer.Ordinal); var usedIds = new HashSet<Guid>();
        foreach (var seed in seeds)
        {
            if (!versions.TryGetValue(seed.Proof, out var creationSource) || !creationSource.Options.Any(option => Equal(option.Identity, seed.Identity))) throw new PlanningTaskIntegrityException();
            var history = await History(connection, transaction, runId, seed.Identity, seed.Actor, seed.Proof, versions, usedIds, ct);
            foreach (var item in history) usedProofs.Add(item.ProofDigest);
            tasks.Add(new(seed.Identity, seed.Actor, history));
        }
        if (!usedProofs.SetEquals(versions.Keys)) throw new PlanningTaskIntegrityException();
        foreach (var (table, count) in new[] { ("task_current", (long)seeds.Count), ("events", tasks.Sum(task => (long)task.Events.Length)), ("receipts", tasks.Sum(task => (long)task.Events.Length)) })
        {
            await using var query = Query(connection, transaction, "SELECT count(*) FROM synthetic_planning_tasks." + table + " WHERE run_id=@run", runId);
            if ((long)(await query.ExecuteScalarAsync(ct))! != count) throw new PlanningTaskIntegrityException();
        }
        return new(versions, tasks.ToImmutable());
    }
    private sealed class TaskAuthorizationException() : Exception("Planning task assignment/category denied.");
    private static async Task<ImmutableArray<StoredEvent>> History(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, PlanningTaskIdentity identity,
        string assignee, string creationProof, Dictionary<string, PlanningTaskSource> versions, HashSet<Guid> usedIds, CancellationToken ct)
    {
        StoredCurrent stored;
        await using (var command = Query(connection, transaction, "SELECT revision,current_json FROM synthetic_planning_tasks.task_current WHERE run_id=@run AND task_id=@task", runId))
        {
            command.Parameters.AddWithValue("task", identity.TaskId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new PlanningTaskIntegrityException();
            stored = PlanningTaskCanonical.Parse<StoredCurrent>(reader.GetString(1));
            if (stored.Revision != reader.GetInt64(0) || reader.GetString(1) != PlanningTaskCanonical.Json(stored)) throw new PlanningTaskIntegrityException();
        }
        var events = ImmutableArray.CreateBuilder<StoredEvent>(); PlanningTaskEvent? plan = null;
        await using (var command = Query(connection, transaction, """
            SELECT e.event_id,e.expected_revision,e.result_revision,e.proof_digest,e.command_json,e.command_digest,e.event_json,e.event_digest,e.after_json,e.recorded_at,
            r.receipt_json,r.receipt_digest FROM synthetic_planning_tasks.events e LEFT JOIN synthetic_planning_tasks.receipts r ON r.run_id=e.run_id AND r.event_id=e.event_id
            WHERE e.run_id=@run AND e.task_id=@task ORDER BY e.result_revision
            """, runId))
        {
            command.Parameters.AddWithValue("task", identity.TaskId); await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(10) || reader.IsDBNull(11)) throw new PlanningTaskIntegrityException();
                var item = PlanningTaskCanonical.Parse<PlanningTaskEvent>(reader.GetString(6));
                var value = PlanningTaskCanonical.Parse<PlanningTaskCommand>(reader.GetString(4));
                if (!versions.TryGetValue(reader.GetString(3), out var source)) throw new PlanningTaskIntegrityException();
                var option = source.Options.Single(option => Equal(option.Identity, identity));
                var previous = events.LastOrDefault()?.Event;
                if (item.Revision != events.Count + 1L || item.Revision > PlanningTaskPolicy.MaximumRevision || item.EventId != reader.GetGuid(0) || !usedIds.Add(item.EventId) ||
                    item.ActorId != assignee || source.Artifacts.ActorId != assignee || !item.ActorRoles.SequenceEqual(["Consultant"]) || item.RecordedAtUtc.Offset != TimeSpan.Zero || item.RecordedAtUtc.UtcDateTime != reader.GetDateTime(9) ||
                    reader.GetInt64(1) != item.Revision - 1 || reader.GetInt64(2) != item.Revision || PlanningTaskPolicy.ValidateCommand(value) is not null || value.ExpectedRevision != item.Revision - 1 ||
                    value.EventId != item.EventId || value.Kind != item.Kind || value.Reason != item.Reason || value.ExpectedSourceDigest != source.Binding.ArtifactSource.SourceDigest ||
                    !Equal(value.ExpectedAttestations, option.CurrentAttestations) || !Equal(item.Attestations, option.CurrentAttestations) || !Equal(item.Source, source.Binding) ||
                    reader.GetString(4) != PlanningTaskCanonical.Json(value) || reader.GetString(5) != PlanningTaskCanonical.CommandDigest(PlanningTaskScope.Fixed, runId, identity.TaskId, assignee, value) ||
                    reader.GetString(6) != PlanningTaskCanonical.Json(item) || reader.GetString(7) != PlanningTaskCanonical.Digest(item) || reader.GetString(8) != PlanningTaskCanonical.Json(Current(item))) throw new PlanningTaskIntegrityException();
                if (previous is null)
                {
                    if (item.Kind != PlanningTaskKind.Create || reader.GetString(3) != creationProof || !option.CanCreate || item.RecordedStatus != PlanningTaskStatus.Planned || item.PlanningEventId != item.EventId) throw new PlanningTaskIntegrityException();
                    plan = item;
                }
                else
                {
                    if (PlanningTaskPolicy.Transition(previous.RecordedStatus, Freshness(plan!, source, option), option.FindingState, option.CanCreate, item.Kind) is not null ||
                        item.RecordedStatus != PlanningTaskPolicy.After(previous.RecordedStatus, item.Kind) || item.PlanningEventId != (item.Kind == PlanningTaskKind.ReconfirmPlan ? item.EventId : previous.PlanningEventId)) throw new PlanningTaskIntegrityException();
                    CheckSources(source, versions[events[^1].ProofDigest]);
                    foreach (var before in previous.Attestations)
                        if (option.CurrentAttestations.Single(after => after.ArtifactId == before.ArtifactId).Revision < before.Revision) throw new PlanningTaskIntegrityException();
                    if (item.Kind == PlanningTaskKind.ReconfirmPlan) plan = item;
                }
                var receipt = PlanningTaskCanonical.Parse<PlanningTaskReceipt>(reader.GetString(10));
                if (receipt != Receipt(runId, identity.TaskId, item) || reader.GetString(10) != PlanningTaskCanonical.Json(receipt) || reader.GetString(11) != PlanningTaskCanonical.Digest(receipt)) throw new PlanningTaskIntegrityException();
                events.Add(new(item, receipt, reader.GetString(5), reader.GetString(3)));
            }
        }
        if (events.Count == 0 || stored != Current(events[^1].Event)) throw new PlanningTaskIntegrityException();
        return events.ToImmutable();
    }
    private static async Task Register(NpgsqlConnection connection, NpgsqlTransaction transaction, PlanningTaskSource source, PlanningTaskIdentity identity, string assignee, bool first, CancellationToken ct)
    {
        var binding = source.Binding.ArtifactSource;
        await Execute(connection, transaction, "INSERT INTO synthetic_planning_tasks.source_versions VALUES (@run,@proof,@customer,@project,@environment,@package,@binding,@artifacts) ON CONFLICT DO NOTHING", ct,
            ("run", binding.RunId), ("proof", Proof(source)), ("customer", binding.Scope.CustomerId), ("project", binding.Scope.ProjectId), ("environment", binding.Scope.EnvironmentId),
            ("package", source.CanonicalPackage), ("binding", PlanningTaskCanonical.Json(source.Binding)), ("artifacts", PlanningTaskCanonical.Json(source.Artifacts)));
        if (!first) return;
        await Execute(connection, transaction, "INSERT INTO synthetic_planning_tasks.task_seeds VALUES (@run,@task,@identity,@digest,@actor,@proof)", ct,
            ("run", binding.RunId), ("task", identity.TaskId), ("identity", PlanningTaskCanonical.Json(identity)), ("digest", PlanningTaskCanonical.Digest(identity)), ("actor", assignee), ("proof", Proof(source)));
        await Execute(connection, transaction, "INSERT INTO synthetic_planning_tasks.task_current VALUES (@run,@task,0,@json)", ct,
            ("run", binding.RunId), ("task", identity.TaskId), ("json", PlanningTaskCanonical.Json(new StoredCurrent(0, null, PlanningTaskStatus.Planned, null))));
    }
}
