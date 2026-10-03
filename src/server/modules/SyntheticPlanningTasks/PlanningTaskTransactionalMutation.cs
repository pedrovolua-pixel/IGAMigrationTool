using Npgsql;
using System.Collections.Immutable;

namespace SyntheticPlanningTasks;

public sealed partial class SyntheticPlanningTaskStore
{
    public async Task<PlanningTaskApplyResult> ApplyInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, string taskId, PlanningTaskAuthority authority, PlanningTaskCommand command, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !PlanningTaskPolicy.ValidDigest(taskId) || PlanningTaskPolicy.ValidateCommand(command) is not null) return new(PlanningTaskIssue.InvalidInput, null);
        if (PlanningTaskPolicy.Authorize(authority, scope, PlanningTaskPolicy.Grant(command.Kind)) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username) return new(PlanningTaskIssue.InvalidInput, null);
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
            return new(null, receipt);
        }
        catch (TaskAuthorizationException) { return new(PlanningTaskIssue.Denied, null); }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
}
