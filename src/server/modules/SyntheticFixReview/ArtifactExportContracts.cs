using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SyntheticFixReview;

public enum ArtifactExportRole { Consultant, Auditor }
public sealed record ArtifactExportAuthority(string ActorId, bool Authenticated, bool Active, bool Revoked,
    bool AssignmentActive, ArtifactReviewScope AssignedScope, ArtifactExportRole Role, ImmutableArray<string> Categories,
    bool TaskExportGranted, bool CustomerExportAllowed, bool AuditorScopedExportGranted, bool ResourceAvailable);
public static class ArtifactExportPolicy
{
    public static ArtifactReviewIssue? Authorize(ArtifactExportAuthority? authority, ArtifactReviewScope scope, string? category = null)
    {
        if (scope != ArtifactReviewScope.Fixed) return ArtifactReviewIssue.WrongScope;
        if (authority is null || string.IsNullOrWhiteSpace(authority.ActorId) || !authority.Authenticated || !authority.Active || authority.Revoked ||
            !authority.AssignmentActive || authority.AssignedScope != scope || !Enum.IsDefined(authority.Role) || !authority.TaskExportGranted ||
            !authority.CustomerExportAllowed || authority.Role == ArtifactExportRole.Auditor && !authority.AuditorScopedExportGranted || !authority.ResourceAvailable ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(item => item is not ("SECURITY" or "OPERATIONS")) ||
            category is not null && !authority.Categories.Contains(category)) return ArtifactReviewIssue.Denied;
        return null;
    }
}

public sealed record ArtifactExportVector(string ArtifactId, long Revision, Guid? EventId, string? Kind, string State, string? SourceDigest);
public sealed record ArtifactExportCapture(string MetadataDigest, ImmutableArray<ArtifactExportVector> Vectors,
    [property: JsonIgnore] ArtifactReviewSnapshot Snapshot);
public sealed record ArtifactExportReadResult(ArtifactReviewIssue? Issue, ArtifactExportCapture? Capture)
{ public bool Succeeded => Issue is null && Capture is not null; }
