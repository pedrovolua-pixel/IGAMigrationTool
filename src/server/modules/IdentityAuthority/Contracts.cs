using System.Collections.Immutable;
using IdentityPolicy;
using IdentitySessions;

namespace IdentityAuthority;

public enum EnrollmentLifecycle { Pending, Active, Suspended, Revoked }
public enum OrganizationalOrigin { InternalOrganizational, ExternalOrganizational }
public enum AuthorityOperation { EnrollPending, ActivateEnrollment, SuspendSubject, RevokeSubject, SetAssignment, RevokeAssignment, ApproveExternalLifecycle, MarkExternalChange }
public enum AuthorityReason { None, ApprovedOnboarding, ApprovedAssignment, ApprovedRenewal, Incident, Termination, SponsorOrEngagementChanged }
public enum ProviderUserType { Member, Guest }
public enum InvitationState { Accepted, PendingAcceptance }

public sealed record DecisionAttributionV1(Guid ApprovedDecisionId, SessionSubject ApprovedBySubject, DateTimeOffset ApprovedAtUtc);
public sealed record SubjectEnrollmentV1(string SchemaVersion, SessionSubject Subject, long Revision,
    EnrollmentLifecycle Lifecycle, OrganizationalOrigin Origin, Guid HomeTenantId, DateTimeOffset EnrolledAtUtc,
    DecisionAttributionV1 Attribution);
public sealed record HumanAssignmentV1(string SchemaVersion, Guid AssignmentId, SessionSubject Subject,
    HumanScope Scope, HumanRole Role, bool Active, DateTimeOffset StartsAtUtc, DateTimeOffset ExpiresAtUtc,
    ImmutableArray<string> EvidenceCategories, ImmutableArray<GrantCondition> Conditions, long Revision,
    DecisionAttributionV1 Attribution);
public sealed record GuestLifecycleV1(string SchemaVersion, SessionSubject Subject, SessionSubject Sponsor,
    DateTimeOffset AssignedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset LastReviewedAtUtc,
    Guid EngagementReference, long EngagementRevision, bool SponsorOrEngagementChanged, DecisionAttributionV1 Attribution);
public sealed record ProviderAppRoleV1(Guid AppRoleId, CoarseAppRole Role, bool Enabled);
public sealed record ProviderRoleBindingV1(string SchemaVersion, Guid ResourceTenantId, Guid ClientId,
    Guid ResourceServicePrincipalId, ImmutableArray<ProviderAppRoleV1> AppRoles, long ManifestRevision,
    string ManifestSha256, Guid ApprovedDecisionId);
public sealed record ProviderObservationV1(string SchemaVersion, SessionSubject Subject, Guid ClientId,
    Guid ResourceServicePrincipalId, long Sequence, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc,
    long EnrollmentRevision, long SecurityVersion, Guid ReturnedSubjectId, bool AccountEnabled,
    ProviderUserType UserType, InvitationState? InvitationState, DateTimeOffset ResourceCutoffUtc,
    ImmutableArray<Guid> AppRoleIds, bool Complete, Guid CorrelationId);
public sealed record AuthorityDecisionV1(string SchemaVersion, Guid CommandId, long ExpectedRevision,
    SessionSubject Subject, HumanScope? Scope, AuthorityOperation Operation, Guid DecisionReference,
    AuthorityReason Reason, SessionSubject Administrator, string PayloadSha256);

// A trusted internal command supplies exactly one record appropriate to its operation.
// It is never bound from a controller, request headers, claims or a browser.
public sealed record AuthorityCommandV1(string SchemaVersion, AuthorityDecisionV1 Decision,
    SubjectEnrollmentV1? Enrollment, HumanAssignmentV1? Assignment, GuestLifecycleV1? Guest);
public sealed record AuthorityReceiptV1(Guid OperationId, long Revision, long SecurityVersion, DateTimeOffset CommittedAtUtc, ImmutableArray<Guid> EventIds);
public sealed record AuthoritySnapshotV1(SubjectEnrollmentV1 Enrollment, long SecurityVersion,
    ImmutableArray<HumanAssignmentV1> Assignments, GuestLifecycleV1? Guest, ProviderObservationV1? Provider,
    bool ProviderStatusValid, long? ProviderPublishedSecurityVersion, bool SponsorActive, bool HomeStatusVerified,
    DateTimeOffset? HomeStatusCheckedAtUtc, DateTimeOffset? HomeCutoffUtc);

// No production home-status source exists. Synthetic proof is exact subject/home/revision-bound.
public sealed record HomeStatusEvidenceV1(SessionSubject Subject, Guid HomeTenantId, long EnrollmentRevision,
    long SecurityVersion, DateTimeOffset CheckedAtUtc, DateTimeOffset CutoffUtc, bool Active, bool Complete);
public interface IHomeStatusEvidenceSource
{
    ValueTask<HomeStatusEvidenceV1?> ReadAsync(SubjectEnrollmentV1 enrollment, long securityVersion, CancellationToken cancellationToken);
}
public sealed class DenyingHomeStatusEvidenceSource : IHomeStatusEvidenceSource
{
    public ValueTask<HomeStatusEvidenceV1?> ReadAsync(SubjectEnrollmentV1 enrollment, long securityVersion, CancellationToken cancellationToken)
        => ValueTask.FromResult<HomeStatusEvidenceV1?>(null);
}
public sealed record ProviderReadResultV1(ProviderObservationV1 Observation, HomeStatusEvidenceV1? HomeStatus);
public interface ISyntheticProviderSource
{
    ValueTask<ReadOnlyMemory<byte>?> ReadUserAsync(SessionSubject subject, CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>?> ReadDirectRolesPageAsync(Uri exactQuery, CancellationToken cancellationToken);
}
