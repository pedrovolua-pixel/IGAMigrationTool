using System.Text.Json;

namespace SyntheticFixReview;

public static class ArtifactReviewPolicy
{
    public const long MaximumRevision = 9_007_199_254_740_991;
    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static ArtifactReviewIssue? ValidateCommand(ArtifactReviewCommand? command) =>
        command is null || command.EventId == Guid.Empty || !Enum.IsDefined(command.Kind) ||
        command.ExpectedRevision < 0 || command.ExpectedRevision > MaximumRevision ||
        !ValidDigest(command.ExpectedSourceDigest) || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 2000 ||
        JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(command.Reason)) != command.Reason
            ? ArtifactReviewIssue.InvalidInput : null;
    public static ArtifactReviewIssue? Authorize(ArtifactReviewAuthority? authority, ArtifactReviewScope scope, string? category = null)
    {
        if (scope != ArtifactReviewScope.Fixed) return ArtifactReviewIssue.WrongScope;
        if (authority is null || string.IsNullOrWhiteSpace(authority.ActorId) || !authority.Authenticated || !authority.Active ||
            authority.Revoked || !authority.AssignmentActive || authority.AssignedScope != scope ||
            authority.Roles.IsDefaultOrEmpty || !authority.Roles.SequenceEqual([ArtifactReviewRole.Consultant]) ||
            authority.Actions.IsDefaultOrEmpty || authority.Actions.Any(action => !Enum.IsDefined(action)) ||
            !authority.Actions.Contains(ArtifactReviewAction.Read) || !authority.Actions.Contains(ArtifactReviewAction.Review) ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(value => value is not ("SECURITY" or "OPERATIONS")) ||
            authority.ResourceState != ArtifactReviewResourceState.Mutable || category is not null && !authority.Categories.Contains(category)) return ArtifactReviewIssue.Denied;
        return null;
    }
    public static ArtifactReviewState CurrentState(ArtifactReviewEvent? latest, string sourceDigest) => latest is null || latest.Kind == ArtifactReviewKind.WithdrawReview
        ? ArtifactReviewState.Unverified : latest.Source.SourceDigest == sourceDigest ? ArtifactReviewState.ReviewedForPlanning : ArtifactReviewState.NeedsReview;
    public static ArtifactReviewIssue? Transition(ArtifactReviewState state, ArtifactReviewKind kind) => kind switch
    {
        ArtifactReviewKind.ReviewForPlanning when state is ArtifactReviewState.Unverified or ArtifactReviewState.NeedsReview => null,
        ArtifactReviewKind.WithdrawReview when state == ArtifactReviewState.ReviewedForPlanning => null,
        _ => ArtifactReviewIssue.InvalidState
    };
}
