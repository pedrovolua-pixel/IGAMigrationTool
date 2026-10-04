using System.Collections.Immutable;
using Npgsql;

namespace SyntheticPlanningTasks;

public enum PlanningTaskExportRole { Consultant, Auditor }
public sealed record PlanningTaskExportAuthority(string ActorId, bool Authenticated, bool Active, bool Revoked, bool AssignmentActive,
    PlanningTaskScope AssignedScope, PlanningTaskExportRole Role, ImmutableArray<string> Categories, bool TaskExportGranted,
    bool CustomerExportAllowed, bool AuditorScopedExportGranted, bool ResourceAvailable);
public sealed record PlanningTaskExportRow(PlanningTaskIdentity Identity, string AssigneeId, long Revision, PlanningTaskStatus Status,
    PlanningTaskFreshness Freshness, DateTimeOffset CreatedAtUtc, DateTimeOffset PlannedAtUtc, string PlannedSourceDigest,
    ImmutableArray<PlanningTaskAttestation> CurrentAttestations);
public sealed record PlanningTaskExportCapture(PlanningTaskScope Scope, Guid RunId, PlanningTaskSourceBinding Source,
    ImmutableArray<PlanningTaskExportRow> Rows);
public sealed record PlanningTaskExportResult(PlanningTaskIssue? Issue, PlanningTaskExportCapture? Capture) { public bool Succeeded => Issue is null && Capture is not null; }
public delegate Task<PlanningTaskSourceResult> PlanningTaskExportSourceReader(NpgsqlConnection connection, NpgsqlTransaction transaction,
    Guid runId, PlanningTaskExportAuthority authority, CancellationToken cancellationToken);
public static class PlanningTaskExportPolicy
{
    public static PlanningTaskIssue? Authorize(PlanningTaskExportAuthority? authority, PlanningTaskScope scope, string? category = null)
    {
        if (scope != PlanningTaskScope.Fixed) return PlanningTaskIssue.WrongScope;
        if (authority is null || string.IsNullOrWhiteSpace(authority.ActorId) || !authority.Authenticated || !authority.Active || authority.Revoked ||
            !authority.AssignmentActive || authority.AssignedScope != scope || !Enum.IsDefined(authority.Role) || !authority.TaskExportGranted ||
            !authority.CustomerExportAllowed || authority.Role == PlanningTaskExportRole.Auditor && !authority.AuditorScopedExportGranted || !authority.ResourceAvailable ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(item => item is not ("SECURITY" or "OPERATIONS")) ||
            category is not null && !authority.Categories.Contains(category)) return PlanningTaskIssue.Denied;
        return null;
    }
}
