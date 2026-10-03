using System.Diagnostics.CodeAnalysis;

namespace SyntheticEvaluation;

public enum EvaluationReviewerRole
{
    Consultant, QualifiedCustomerReviewer, CustomerEvidenceAuthorizer, CustomerRiskOwner,
    Executive, Auditor, PlatformSupport, Mcp, ShareViewer
}

public enum EvaluationReviewerAction { ScoredReview, PresentationCorrection, RelatedHistory }
public enum EvaluationFixtureState { Active, Revoked, Suspended, Deleted, Expired }
public enum EvaluationConflictAnswer { No, Yes, Unknown }
public enum EvaluationReviewerIssue
{
    InvalidInput, InvalidReference, InvalidVersion, InvalidRegistry, DuplicateEntry,
    UnknownIdentity, UnknownAssignment, UnknownMember, StaleVersion, IdentityDenied,
    AssignmentDenied, ScopeDenied, RoleActionDenied, CategoryDenied, ResourceDenied,
    CustomerPolicyDenied, LifecycleDenied, QualificationDenied, ConflictDenied, HistoryDenied
}

public sealed record EvaluationReviewerScope(string CustomerId, string ProjectId, string EnvironmentId,
    string AssessmentId, string EvaluationId);
public sealed record EvaluationFixtureIdentity(string Id, long Revision, bool Authenticated, EvaluationFixtureState State);
public sealed record EvaluationFixtureAssignment(string Id, long Revision, string IdentityId,
    EvaluationReviewerRole Role, EvaluationReviewerScope Scope, DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidUntilUtc, EvaluationFixtureState State, bool Qualified,
    string? QualificationBasisId, string AssignmentAuthorityId, bool ScoredReviewGranted,
    bool PresentationCorrectionGranted, bool RelatedHistoryGranted, IReadOnlyList<string> ReviewCategoryIds,
    IReadOnlyList<string> HistoryCategoryIds, bool CustomerPolicyPermits, bool LifecyclePermits);
public sealed record EvaluationFixtureMember(string Id, long Revision, EvaluationReviewerScope Scope,
    string CategoryId, EvaluationFixtureState State, bool ResourcePermits, bool CustomerPolicyPermits,
    bool LifecyclePermits, bool AuthorizedContextSufficient);
public sealed record EvaluationConflictClearance(string ReasonId, string AuthorityId,
    bool OwnerApproved, bool SmeReviewed, bool SecurityReviewed);
public sealed record EvaluationFixtureConflict(string AssignmentId, string MemberId, long Revision,
    EvaluationConflictAnswer FindingOrAnswerOrCorrectionAuthor, EvaluationConflictAnswer RuleOrCatalogContributor,
    EvaluationConflictAnswer AssessmentOrSourceContributor, EvaluationConflictAnswer ModelPromptOrProviderContributor,
    EvaluationConflictAnswer CustomerConclusionOrRemediationResponsible, EvaluationConflictAnswer OtherMaterialContributor,
    EvaluationConflictAnswer GeneralRelationship, EvaluationConflictClearance? Clearance);
public sealed record EvaluationReviewerRegistryInput(string VersionId, string PolicyVersionId,
    string InstructionVersionId, IReadOnlyList<EvaluationFixtureIdentity> Identities,
    IReadOnlyList<EvaluationFixtureAssignment> Assignments, IReadOnlyList<EvaluationFixtureMember> Members,
    IReadOnlyList<EvaluationFixtureConflict> Conflicts);
public sealed record EvaluationReviewerRequest(string IdentityId, string AssignmentId, string MemberId,
    EvaluationReviewerScope Scope, EvaluationReviewerAction Action, DateTimeOffset AtUtc,
    string ExpectedRegistryVersionId, long ExpectedIdentityRevision, long ExpectedAssignmentRevision,
    long ExpectedMemberRevision, long? ExpectedConflictRevision);
public sealed record EvaluationReviewerCaptureResult(EvaluationReviewerIssue? Issue, EvaluationReviewerRegistry? Registry)
{
    public bool HasRegistry => Issue is null && Registry is not null;
}
public sealed record EvaluationReviewerDecision(EvaluationReviewerIssue? Issue, bool? AuthorizedContextSufficient)
{
    public bool IsAuthorized => Issue is null;
}

/// <summary>Detached supplied fixture authority; not an identity or permission store.</summary>
public sealed class EvaluationReviewerRegistry
{
    private readonly Dictionary<string, EvaluationFixtureIdentity> identitiesById;
    private readonly Dictionary<string, EvaluationFixtureAssignment> assignmentsById;
    private readonly Dictionary<string, EvaluationFixtureMember> membersById;
    private readonly Dictionary<(string AssignmentId, string MemberId), EvaluationFixtureConflict> conflictsByIds;
    private readonly Dictionary<string, HashSet<string>> reviewCategoriesByAssignment;
    private readonly Dictionary<string, HashSet<string>> historyCategoriesByAssignment;
    internal EvaluationReviewerRegistry(string versionId, string policyVersionId, string instructionVersionId,
        EvaluationFixtureIdentity[] identities, EvaluationFixtureAssignment[] assignments,
        EvaluationFixtureMember[] members, EvaluationFixtureConflict[] conflicts)
    {
        VersionId = versionId;
        PolicyVersionId = policyVersionId;
        InstructionVersionId = instructionVersionId;
        Identities = Array.AsReadOnly(identities);
        Assignments = Array.AsReadOnly(assignments);
        Members = Array.AsReadOnly(members);
        Conflicts = Array.AsReadOnly(conflicts);
        identitiesById = identities.ToDictionary(x => x.Id, StringComparer.Ordinal);
        assignmentsById = assignments.ToDictionary(x => x.Id, StringComparer.Ordinal);
        membersById = members.ToDictionary(x => x.Id, StringComparer.Ordinal);
        conflictsByIds = conflicts.ToDictionary(x => (x.AssignmentId, x.MemberId));
        reviewCategoriesByAssignment = assignments.ToDictionary(x => x.Id,
            x => x.ReviewCategoryIds.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        historyCategoriesByAssignment = assignments.ToDictionary(x => x.Id,
            x => x.HistoryCategoryIds.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }
    internal bool TryGetIdentity(string id, [NotNullWhen(true)] out EvaluationFixtureIdentity? identity)
        => identitiesById.TryGetValue(id, out identity);
    internal bool TryGetAssignment(string id, [NotNullWhen(true)] out EvaluationFixtureAssignment? assignment)
        => assignmentsById.TryGetValue(id, out assignment);
    internal bool TryGetMember(string id, [NotNullWhen(true)] out EvaluationFixtureMember? member)
        => membersById.TryGetValue(id, out member);
    internal bool TryGetConflict(string assignmentId, string memberId, out EvaluationFixtureConflict? conflict)
        => conflictsByIds.TryGetValue((assignmentId, memberId), out conflict);

    internal bool HasReviewCategory(string assignmentId, string categoryId)
        => reviewCategoriesByAssignment[assignmentId].Contains(categoryId);
    internal bool HasHistoryCategory(string assignmentId, string categoryId)
        => historyCategoriesByAssignment[assignmentId].Contains(categoryId);

    public string VersionId { get; }
    public string PolicyVersionId { get; }
    public string InstructionVersionId { get; }
    public IReadOnlyList<EvaluationFixtureIdentity> Identities { get; }
    public IReadOnlyList<EvaluationFixtureAssignment> Assignments { get; }
    public IReadOnlyList<EvaluationFixtureMember> Members { get; }
    public IReadOnlyList<EvaluationFixtureConflict> Conflicts { get; }
}
