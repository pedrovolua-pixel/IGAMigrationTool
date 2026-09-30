namespace EvidenceGovernance;

/// <summary>
/// Checks role coverage using approvals verified against the trusted reviewer
/// snapshot for this evaluation instant. It never authorizes activation.
/// </summary>
public static class ReviewEvidenceCoverage
{
    public static ReviewEvidenceCoverageResult Evaluate(
        string? expectedArtifactSha256,
        string? expectedScopeId,
        string? authorId,
        IReadOnlyCollection<string>? requiredRoles,
        IReadOnlyCollection<VerifiedReviewerApproval>? approvals,
        DateTimeOffset now)
    {
        if (!GateCheckBundleRules.IsHex(expectedArtifactSha256, 64) ||
            !GateCheckBundleRules.IsCheckId(expectedScopeId) ||
            !GateCheckBundleRules.IsCheckId(authorId) ||
            requiredRoles is null || requiredRoles.Count == 0 ||
            requiredRoles.Any(role => !GateCheckBundleRules.IsCheckId(role)) ||
            requiredRoles.Count != requiredRoles.Distinct(StringComparer.Ordinal).Count() ||
            approvals is null || now.Offset != TimeSpan.Zero)
        {
            return ReviewEvidenceCoverageResult.InvalidInput;
        }

        var required = requiredRoles.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(string ReviewerId, string Role)>();
        var approvedRoles = new HashSet<string>(StringComparer.Ordinal);
        var independentApproval = false;
        foreach (var approval in approvals)
        {
            if (approval is null ||
                !string.Equals(approval.ArtifactSha256, expectedArtifactSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approval.ScopeId, expectedScopeId, StringComparison.Ordinal) ||
                !required.Contains(approval.Role))
            {
                return ReviewEvidenceCoverageResult.WrongScope;
            }

            if (approval.VerifiedAt != now || approval.DecisionAt > now ||
                GateEvidenceRetention.Calculate(approval.DecisionAt).OrdinaryAccessEndsAt <= now)
            {
                return ReviewEvidenceCoverageResult.StaleVerification;
            }

            if (!seen.Add((approval.ReviewerId, approval.Role)))
            {
                return ReviewEvidenceCoverageResult.DuplicateApproval;
            }

            approvedRoles.Add(approval.Role);
            independentApproval |= !string.Equals(approval.ReviewerId, authorId, StringComparison.Ordinal);
        }

        if (!approvedRoles.SetEquals(required))
        {
            return ReviewEvidenceCoverageResult.MissingRequiredRole;
        }

        return independentApproval
            ? ReviewEvidenceCoverageResult.CompleteForReview
            : ReviewEvidenceCoverageResult.AuthorOnly;
    }
}

public enum ReviewEvidenceCoverageResult
{
    CompleteForReview,
    InvalidInput,
    WrongScope,
    StaleVerification,
    DuplicateApproval,
    MissingRequiredRole,
    AuthorOnly
}
