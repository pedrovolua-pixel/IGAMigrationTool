using System.Collections.Immutable;
using System.Text.Json;
using SyntheticFixReview;

namespace SyntheticPlanningTasks;

public static class PlanningTaskPolicy
{
    public const long MaximumRevision = 9_007_199_254_740_991;
    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidVector(ImmutableArray<PlanningTaskAttestation> vector) => !vector.IsDefault && vector.Length == 3 &&
        vector.All(item => item is not null && ValidDigest(item.ArtifactId) && item.Revision >= 0 && item.Revision <= MaximumRevision && Enum.IsDefined(item.State) &&
            (item.Revision == 0 ? item.EventId is null && item.Kind is null && item.SourceDigest is null && item.State == ArtifactReviewState.Unverified :
                item.EventId is not null && item.EventId != Guid.Empty && item.Kind is not null && Enum.IsDefined(item.Kind.Value) && ValidDigest(item.SourceDigest) &&
                (item.Kind == ArtifactReviewKind.WithdrawReview ? item.State == ArtifactReviewState.Unverified : item.State is ArtifactReviewState.ReviewedForPlanning or ArtifactReviewState.NeedsReview))) &&
        vector.Select(item => item.ArtifactId).SequenceEqual(vector.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    public static PlanningTaskIssue? ValidateCommand(PlanningTaskCommand? command) => command is null || command.EventId == Guid.Empty || !Enum.IsDefined(command.Kind) ||
        command.ExpectedRevision < 0 || command.ExpectedRevision > MaximumRevision || command.Kind == PlanningTaskKind.Create && command.ExpectedRevision != 0 ||
        !ValidDigest(command.ExpectedSourceDigest) || !ValidVector(command.ExpectedAttestations) || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 2000 ||
        JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(command.Reason)) != command.Reason ? PlanningTaskIssue.InvalidInput : null;
    public static PlanningTaskAction Grant(PlanningTaskKind kind) => kind switch { PlanningTaskKind.Create => PlanningTaskAction.Create, PlanningTaskKind.Comment => PlanningTaskAction.Comment, _ => PlanningTaskAction.Manage };
    public static PlanningTaskIssue? Authorize(PlanningTaskAuthority? authority, PlanningTaskScope scope, PlanningTaskAction action = PlanningTaskAction.Read, string? category = null)
    {
        if (scope != PlanningTaskScope.Fixed) return PlanningTaskIssue.WrongScope;
        if (authority is null || string.IsNullOrWhiteSpace(authority.ActorId) || !authority.Authenticated || !authority.Active || authority.Revoked || !authority.AssignmentActive ||
            authority.AssignedScope != scope || authority.Roles.IsDefaultOrEmpty || !authority.Roles.SequenceEqual([PlanningTaskRole.Consultant]) ||
            authority.Actions.IsDefaultOrEmpty || authority.Actions.Any(value => !Enum.IsDefined(value)) || !authority.Actions.Contains(PlanningTaskAction.Read) || !authority.Actions.Contains(action) ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(value => value is not ("SECURITY" or "OPERATIONS")) || category is not null && !authority.Categories.Contains(category) ||
            authority.ResourceState != PlanningTaskResourceState.Mutable) return PlanningTaskIssue.Denied;
        return null;
    }
    public static PlanningTaskIssue? Transition(PlanningTaskStatus status, PlanningTaskFreshness freshness, string findingState, bool allReviewed, PlanningTaskKind kind) =>
        !Enum.IsDefined(status) || !Enum.IsDefined(freshness) || !Enum.IsDefined(kind) || freshness == PlanningTaskFreshness.SourceUnavailable ? PlanningTaskIssue.InvalidState : kind switch
        {
            PlanningTaskKind.StartProgress when status == PlanningTaskStatus.Planned && freshness == PlanningTaskFreshness.CurrentPlan && findingState != "Rejected" => null,
            PlanningTaskKind.Complete when status == PlanningTaskStatus.InProgress && freshness == PlanningTaskFreshness.CurrentPlan && findingState != "Rejected" => null,
            PlanningTaskKind.ReturnToPlanned when status == PlanningTaskStatus.InProgress => null,
            PlanningTaskKind.Cancel when status is PlanningTaskStatus.Planned or PlanningTaskStatus.InProgress => null,
            PlanningTaskKind.Reopen when status is PlanningTaskStatus.Completed or PlanningTaskStatus.Cancelled && findingState != "Rejected" => null,
            PlanningTaskKind.ReconfirmPlan when status is PlanningTaskStatus.Planned or PlanningTaskStatus.InProgress && freshness == PlanningTaskFreshness.NeedsReconfirmation && findingState != "Rejected" && allReviewed => null,
            PlanningTaskKind.Comment => null,
            _ => PlanningTaskIssue.InvalidState
        };
    internal static PlanningTaskStatus After(PlanningTaskStatus status, PlanningTaskKind kind) => kind switch
    {
        PlanningTaskKind.Create or PlanningTaskKind.ReturnToPlanned or PlanningTaskKind.Reopen => PlanningTaskStatus.Planned,
        PlanningTaskKind.StartProgress => PlanningTaskStatus.InProgress,
        PlanningTaskKind.Complete => PlanningTaskStatus.Completed,
        PlanningTaskKind.Cancel => PlanningTaskStatus.Cancelled,
        _ => status
    };
}
