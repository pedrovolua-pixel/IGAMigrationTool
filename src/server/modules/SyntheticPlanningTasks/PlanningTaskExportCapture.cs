using System.Collections.Immutable;
using Npgsql;

namespace SyntheticPlanningTasks;

public sealed partial class SyntheticPlanningTaskStore
{
    public static SyntheticPlanningTaskStore CreateForPhase1B(string connectionString, PlanningTaskScope scope,
        PlanningTaskSourceReader sourceReader, ISyntheticPlanningTaskCommitObserver? observer = null) => new(connectionString, scope, sourceReader, observer, true);
    public async Task<PlanningTaskReadResult> ReadInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, PlanningTaskAuthority authority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty) return new(PlanningTaskIssue.InvalidInput, null);
        if (PlanningTaskPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username) return new(PlanningTaskIssue.InvalidInput, null);
        await Fence(connection, transaction, runId, cancellationToken);
        if (await SyntheticPlanningTaskMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var captured = await Capture(connection, transaction, runId, authority, cancellationToken);
            if (captured.Issue is PlanningTaskIssue.Denied or PlanningTaskIssue.WrongScope or PlanningTaskIssue.IntegrityMismatch) return new(captured.Issue, null);
            var loaded = await Load(connection, transaction, runId, authority, cancellationToken); var source = captured.Source;
            if (source is not null && !Available(loaded, source)) source = null;
            if (source is not null) CheckCurrent(loaded, source);
            var entries = loaded.Tasks.Select(task => Project(task, source, authority)).ToImmutableArray();
            var options = source is null ? [] : source.Options.Select(option => option with
            {
                CanCreate = option.CanCreate && !loaded.Tasks.Any(task => task.Identity.TaskId == option.Identity.TaskId) &&
                PlanningTaskPolicy.Authorize(authority, scope, PlanningTaskAction.Create, option.Identity.CategoryId) is null
            }).ToImmutableArray();
            return new(source is null ? PlanningTaskIssue.SourceUnavailable : null, new(source?.Binding, authority.ActorId, options, entries));
        }
        catch (TaskAuthorizationException) { return new(PlanningTaskIssue.Denied, null); }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
    /// <summary>Supplied transaction only. Genuine export authority does not confer any legacy task mutation/read grant.</summary>
    public async Task<PlanningTaskExportResult> CaptureForExportInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, PlanningTaskExportAuthority authority, PlanningTaskExportSourceReader sourceReader, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || sourceReader is null) return new(PlanningTaskIssue.InvalidInput, null);
        if (PlanningTaskExportPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username) return new(PlanningTaskIssue.InvalidInput, null);
        await Fence(connection, transaction, runId, cancellationToken);
        if (await SyntheticPlanningTaskMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var captured = await sourceReader(connection, transaction, runId, authority, cancellationToken);
            if (captured is null || !captured.Succeeded) return new(captured?.Issue ?? PlanningTaskIssue.SourceUnavailable, null);
            var source = captured.Source!;
            var rebuilt = PlanningTaskSourceBuilder.Build(source.Packages, source.Artifacts);
            if (!rebuilt.Succeeded || !Equal(source.Binding, rebuilt.Source!.Binding) || !Equal(source.Options, rebuilt.Source.Options)) return new(PlanningTaskIssue.IntegrityMismatch, null);
            if (source.Binding.ArtifactSource.RunId != runId || source.Binding.ArtifactSource.Scope != new SyntheticFixReview.ArtifactReviewScope(scope.CustomerId, scope.ProjectId, scope.EnvironmentId)) return new(PlanningTaskIssue.WrongScope, null);
            foreach (var option in source.Options)
                if (PlanningTaskExportPolicy.Authorize(authority, scope, option.Identity.CategoryId) is { } categoryDenied) return new(categoryDenied, null);
            var loaded = await Load(connection, transaction, runId, null, cancellationToken, authority);
            if (!Available(loaded, source)) return new(PlanningTaskIssue.SourceUnavailable, null);
            CheckCurrent(loaded, source);
            var rows = loaded.Tasks.Select(task =>
            {
                var history = task.Events.Select(item => item.Event).ToImmutableArray();
                var plan = history.Last(item => item.Kind is PlanningTaskKind.Create or PlanningTaskKind.ReconfirmPlan);
                var option = source.Options.Single(item => Equal(item.Identity, task.Identity)); var latest = history[^1];
                return new PlanningTaskExportRow(task.Identity, task.Assignee, latest.Revision, latest.RecordedStatus,
                    Freshness(plan, source, option), history[0].RecordedAtUtc, plan.RecordedAtUtc, plan.Source.ArtifactSource.SourceDigest, option.CurrentAttestations);
            }).OrderBy(row => row.Identity.TaskId, StringComparer.Ordinal).ToImmutableArray();
            return new(null, new(scope, runId, source.Binding, rows));
        }
        catch (TaskAuthorizationException) { return new(PlanningTaskIssue.Denied, null); }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
}
