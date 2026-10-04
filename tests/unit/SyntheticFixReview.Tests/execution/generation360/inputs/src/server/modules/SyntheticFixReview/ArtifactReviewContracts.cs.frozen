using System.Collections.Immutable;
using SyntheticFixPackages;

namespace SyntheticFixReview;

public sealed record ArtifactReviewScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static ArtifactReviewScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public enum ArtifactReviewRole { Consultant, QualifiedReviewer, Auditor, Executive, CustomerRiskOwner, PlatformSupport }
public enum ArtifactReviewAction { Read, Review }
public enum ArtifactReviewResourceState { Mutable, Published, Deleted, Blocked, Expired }
public enum ArtifactReviewKind { ReviewForPlanning, WithdrawReview }
public enum ArtifactReviewState { Unverified, ReviewedForPlanning, NeedsReview }
public enum ArtifactReviewIssue
{
    InvalidInput, Denied, WrongScope, InvalidState, NotFound, RevisionConflict, SourceConflict,
    EventConflict, SeedConflict, IntegrityMismatch, MigrationDrift, NotInitialized, SourceUnavailable, RevisionOverflow
}
/// <summary>Trusted local server authority, never a client payload or production identity.</summary>
public sealed record ArtifactReviewAuthority(string ActorId, bool Authenticated, bool Active, bool Revoked,
    bool AssignmentActive, ArtifactReviewScope AssignedScope, ImmutableArray<ArtifactReviewRole> Roles,
    ImmutableArray<string> Categories, ImmutableArray<ArtifactReviewAction> Actions, ArtifactReviewResourceState ResourceState);
public sealed record ArtifactReviewFindingRevision(string FindingId, long Revision);
public sealed record ArtifactReviewSourceBinding(ArtifactReviewScope Scope, Guid RunId, long RunRevision,
    string RunInputDigest, string BaselineId, string ProfileId, string ApplicationVersion, string ContractDigest,
    string SourceDigest, string GuidanceDigest, string FindingReviewDigest, string TemplateVersion, string TemplateDigest,
    ImmutableArray<ArtifactReviewFindingRevision> FindingRevisions);
public sealed record ArtifactReviewArtifact(string FindingId, string CategoryId, string PackageId,
    string ScopedOptionId, string ArtifactId, string TemplateId, string Kind, string ArtifactTextDigest);
public sealed record ArtifactReviewCommand(Guid EventId, ArtifactReviewKind Kind, long ExpectedRevision,
    string ExpectedSourceDigest, string Reason);
public sealed record ArtifactReviewEvent(Guid EventId, long Revision, ArtifactReviewKind Kind, string ActorId,
    ImmutableArray<string> ActorRoles, DateTimeOffset RecordedAtUtc, string Reason,
    ArtifactReviewSourceBinding Source, ArtifactReviewState RecordedState);
public sealed record ArtifactReviewReceipt(string SchemaVersion, Guid EventId, Guid RunId, string ArtifactId,
    ArtifactReviewKind Kind, long Revision, string ActorId, DateTimeOffset RecordedAtUtc, string SourceDigest);
public sealed record ArtifactReviewEntry(ArtifactReviewArtifact Artifact, long Revision, ArtifactReviewState State,
    bool CanReview, bool CanWithdraw, ImmutableArray<ArtifactReviewEvent> History);
public sealed record ArtifactReviewSnapshot(ArtifactReviewSourceBinding Source, string ActorId, ImmutableArray<ArtifactReviewEntry> Entries);
public sealed class ArtifactReviewSource
{
    internal ArtifactReviewSource(FixPackageSnapshot packages, ArtifactReviewSourceBinding binding, ImmutableArray<ArtifactReviewArtifact> artifacts)
    { Packages = packages; Binding = binding; Artifacts = artifacts; }
    internal FixPackageSnapshot Packages { get; }
    internal string CanonicalPackage => Packages.CanonicalJson;
    public ArtifactReviewSourceBinding Binding { get; }
    public ImmutableArray<ArtifactReviewArtifact> Artifacts { get; }
}
public sealed record ArtifactReviewSourceResult(ArtifactReviewIssue? Issue, ArtifactReviewSource? Source)
{ public bool Succeeded => Issue is null && Source is not null; }
public sealed record ArtifactReviewReadResult(ArtifactReviewIssue? Issue, ArtifactReviewSnapshot? Snapshot)
{ public bool Succeeded => Issue is null && Snapshot is not null; }
public sealed record ArtifactReviewApplyResult(ArtifactReviewIssue? Issue, ArtifactReviewReceipt? Receipt, bool AlreadyApplied = false)
{ public bool Succeeded => Issue is null && Receipt is not null; }
public delegate Task<ArtifactReviewSourceResult> ArtifactReviewSourceReader(Guid runId, CancellationToken cancellationToken);
public interface ISyntheticFixReviewCommitObserver
{
    Task BeforeCommitAsync(string operation, Guid runId, string artifactId, Guid eventId, CancellationToken cancellationToken);
}
