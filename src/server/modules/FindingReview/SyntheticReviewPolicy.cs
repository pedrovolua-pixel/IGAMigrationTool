namespace FindingReview;

/// <summary>Approved bounded local policy; no risk acceptance, closure, recurrence, publication or mutation grants.</summary>
public static class SyntheticReviewPolicy
{
    public static SyntheticReviewIssue? Authorize(SyntheticReviewAuthority? authority, SyntheticReviewScope? scope,
        string category, SyntheticReviewAction action, SyntheticReviewResourceState resourceState)
    {
        if (scope != SyntheticReviewScope.Fixed) return SyntheticReviewIssue.WrongScope;
        if (authority is null || !ValidText(authority.ActorId, 250) || !authority.Authenticated || !authority.Active ||
            authority.Revoked || !authority.AssignmentActive || authority.AssignedScope != scope ||
            authority.Roles.IsDefaultOrEmpty || authority.Actions.IsDefault || authority.Categories.IsDefault ||
            !Enum.IsDefined(action) || !Enum.IsDefined(resourceState) || resourceState != SyntheticReviewResourceState.Mutable ||
            !authority.Roles.Any(role => role is SyntheticReviewRole.Consultant or SyntheticReviewRole.QualifiedReviewer) ||
            authority.Roles.Any(role => !Enum.IsDefined(role)) || authority.Actions.Any(grant => !Enum.IsDefined(grant)) ||
            !authority.Actions.Contains(action) || !ValidText(category, 250) || !authority.Categories.Contains(category, StringComparer.Ordinal))
            return SyntheticReviewIssue.Denied;
        return null;
    }

    public static SyntheticReviewIssue? ValidateCommand(SyntheticReviewCommand? command)
    {
        if (command is null || command.EventId == Guid.Empty || command.ExpectedRevision < 0 ||
            command.ExpectedRevision == long.MaxValue || !Enum.IsDefined(command.Kind)) return SyntheticReviewIssue.InvalidInput;
        var valid = command.Kind switch
        {
            SyntheticReviewEventKind.Confirm or SyntheticReviewEventKind.Defer =>
                (command.Reason is null || ValidText(command.Reason, 2000)) && command.Text is null && command.Title is null && command.BusinessContext is null,
            SyntheticReviewEventKind.Reject => ValidText(command.Reason, 2000) && command.Text is null && command.Title is null && command.BusinessContext is null,
            SyntheticReviewEventKind.Comment => ValidText(command.Text, 2000) && command.Reason is null && command.Title is null && command.BusinessContext is null,
            SyntheticReviewEventKind.EditPresentation => (command.Title is not null || command.BusinessContext is not null) &&
                (command.Title is null || ValidText(command.Title, 250)) && (command.BusinessContext is null || command.BusinessContext.Length <= 2000) &&
                command.Reason is null && command.Text is null,
            _ => false
        };
        return valid ? null : SyntheticReviewIssue.InvalidInput;
    }

    public static SyntheticReviewAction ActionFor(SyntheticReviewEventKind kind) => kind switch
    {
        SyntheticReviewEventKind.Confirm or SyntheticReviewEventKind.Reject or SyntheticReviewEventKind.Defer => SyntheticReviewAction.Review,
        SyntheticReviewEventKind.Comment => SyntheticReviewAction.AddComment,
        SyntheticReviewEventKind.EditPresentation => SyntheticReviewAction.EditPresentation,
        _ => (SyntheticReviewAction)(-1)
    };

    public static SyntheticReviewIssue? ValidateTransition(SyntheticFindingCurrent current, SyntheticReviewCommand command)
    {
        if (!Enum.IsDefined(current.State)) return SyntheticReviewIssue.InvalidState;
        if (command.Kind is SyntheticReviewEventKind.Confirm or SyntheticReviewEventKind.Reject or SyntheticReviewEventKind.Defer &&
            current.State != SyntheticFindingState.Proposed) return SyntheticReviewIssue.InvalidState;
        return null;
    }

    public static SyntheticFindingCurrent Next(SyntheticFindingCurrent current, SyntheticReviewCommand command) => current with
    {
        Revision = checked(current.Revision + 1),
        State = command.Kind switch
        {
            SyntheticReviewEventKind.Confirm => SyntheticFindingState.Confirmed,
            SyntheticReviewEventKind.Reject => SyntheticFindingState.Rejected,
            SyntheticReviewEventKind.Defer => SyntheticFindingState.Deferred,
            _ => current.State
        },
        PresentationTitle = command.Title ?? current.PresentationTitle,
        BusinessContext = command.BusinessContext ?? current.BusinessContext
    };

    internal static bool ValidText(string? text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum;
    internal static bool ValidDigest(string? digest) => digest is { Length: 64 } && digest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
