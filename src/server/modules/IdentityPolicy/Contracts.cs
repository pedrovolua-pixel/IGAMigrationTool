using System.Collections.Immutable;

namespace IdentityPolicy;

public enum ActorKind { Human, Worker, ShareLink, Mcp }
public enum HumanRole { Consultant, CustomerReviewer, EvidenceAuthorizer, CustomerRiskOwner, Executive, Auditor, PlatformSupport }
public enum CoarseAppRole { PilotConsultant, PilotCustomerUser, PilotAuditor, PilotPlatformOperator }
public enum HumanAction
{
    ViewDashboard, ViewNormalizedExcerpt, ViewProtectedRawEvidence, AuthorizeProtectedEvidence,
    ConfigureStartAssessment, ConfigureProfileRulesOutcomesAiBudget, OverrideAiBudget,
    ReviewFinding, EditPresentationBusinessContext, AcceptResidualRisk, CreateRecommendationPackage,
    CommentReviewRecommendation, ExportConsultantTasksCsv, PublishReport, AcknowledgeReport,
    CreateReportLink, RevokeReportLink, DeleteWithinPolicy, ViewAuditHistory,
    RunRemediationOrMigration, SecurityRoleAdministration
}
public enum ResourceState { Active, Published, SoftDeleted, Expired }
public enum ReadProjection
{
    None, Dashboard, SummaryExcerpts, ApprovedRecords, OperationalMetadata,
    NormalizedExcerpt, ProtectedRaw, ProjectBusinessAudit, RelatedReviewHistory,
    AuthorizationEvents, RiskAcknowledgmentEvents, ApprovedAuditScope
}
public enum GrantCondition
{
    SeparateViewer, CustomerProtectedAuthorization, DedicatedProtectedPermission,
    ReviewerEditPermission, RiskRequiredFields, AuditedOverride, ExplicitExportGrant,
    PublishWarningAcknowledged, ExecutiveAcknowledgmentAuthority, ExplicitDeletionPermission,
    SeparateCustomerAdministrator, ApprovedReportProvenance, SupportMetadataPolicy
}

public sealed record HumanSubjectKey(Guid TenantId, Guid ObjectId, ActorKind Kind);
public sealed record HumanScope(Guid CustomerId, Guid ProjectId, Guid EnvironmentId, Guid AssessmentId);
// These identifiers select a resource; they confer no resolved authority.
public sealed record HumanAuthorizationRequest(HumanScope Scope, Guid ResourceId, long ExpectedRevision,
    HumanAction Action, string EvidenceCategory, ReadProjection Projection);
public sealed record HumanAssignment(HumanRole Role, HumanScope Scope, bool Active,
    DateTimeOffset ExpiresAt, ImmutableHashSet<string> EvidenceCategories,
    ImmutableHashSet<GrantCondition> Conditions);
public sealed record GuestLifecycle(Guid SponsorId, bool SponsorActive, DateTimeOffset AssignedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset LastReviewedAt, bool SponsorOrEngagementChanged);
public sealed record HumanCustomerPolicy(bool Active, ImmutableHashSet<string> AllowedEvidenceCategories,
    bool ProtectedEvidenceAllowed, bool AiAllowed, bool ExportAllowed, bool SharingAllowed, bool DeletionAllowed);

// Authoritative snapshots are internal service contracts from trusted control-plane adapters.
// They must never be model-bound from request JSON, headers, claims or browser state.
public sealed record AuthoritativeHumanSnapshot(HumanSubjectKey Subject, bool Authenticated, bool Active,
    bool TrustedTenant, long SubjectSecurityVersion, long SessionSecurityVersion, Guid SessionId,
    DateTimeOffset ResolvedAt, DateTimeOffset ProviderStatusCheckedAt,
    ImmutableHashSet<CoarseAppRole> CoarseAppRoles, bool IsGuest, GuestLifecycle? Guest,
    ImmutableArray<HumanAssignment> Assignments, HumanScope ResourceScope, Guid ResourceId,
    long ResourceRevision, ResourceState State, DateTimeOffset? ResourceExpiresAt,
    ReadProjection ResourceProjection, string ResourceEvidenceCategory, ImmutableHashSet<HumanAction> ResourceAllowedActions, bool Revoked,
    bool HoldBlocksAction, bool RetentionBlocksAction, bool CapabilityBlocksAction,
    HumanCustomerPolicy CustomerPolicy);

public sealed record VerifiedPrivilegedAuthentication(HumanSubjectKey Subject, Guid SessionId,
    long SecurityVersion, HumanAction Action, DateTimeOffset AuthenticatedAt,
    bool RequiredMfaVerified, bool RequiredConditionalAccessVerified);

public interface IHumanAuthoritySnapshotSource
{
    ValueTask<AuthoritativeHumanSnapshot?> ResolveAsync(HumanSubjectKey subject,
        HumanAuthorizationRequest request, CancellationToken cancellationToken);
}
public interface IPrivilegedAuthenticationVerifier
{
    ValueTask<VerifiedPrivilegedAuthentication?> VerifyAsync(HumanSubjectKey subject, Guid sessionId,
        long securityVersion, HumanAction action, CancellationToken cancellationToken);
}
public sealed record HumanAuthorizationDecision(bool Allowed, HumanRole? Role, ReadProjection? Projection)
{
    public static HumanAuthorizationDecision Denied { get; } = new(false, null, null);
}
