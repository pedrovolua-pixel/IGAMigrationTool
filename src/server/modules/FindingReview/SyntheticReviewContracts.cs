using System.Collections.Immutable;

namespace FindingReview;

public sealed record SyntheticReviewScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static SyntheticReviewScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public enum SyntheticReviewRole { Consultant, QualifiedReviewer, Auditor, Executive, CustomerRiskOwner, PlatformSupport }
public enum SyntheticReviewAction { Read, Review, AddComment, EditPresentation }
public enum SyntheticReviewResourceState { Mutable, Published, Deleted, Blocked, Expired }
public enum SyntheticFindingState { Proposed, AutoConfirmed, Confirmed, Rejected, Deferred }
public enum SyntheticReviewEventKind { Confirm, Reject, Defer, Comment, EditPresentation }
public enum SyntheticReviewIssue
{
    InvalidInput, Denied, WrongScope, InvalidState, NotFound, RevisionConflict,
    EventConflict, SeedConflict, IntegrityMismatch, MigrationDrift, NotInitialized
}

/// <summary>Server-supplied local test double only. Never accept actor/grants from HTTP or infer Entra authority.</summary>
public sealed record SyntheticReviewAuthority(
    string ActorId, bool Authenticated, bool Active, bool Revoked, bool AssignmentActive,
    SyntheticReviewScope AssignedScope, ImmutableArray<SyntheticReviewRole> Roles,
    ImmutableArray<string> Categories, ImmutableArray<SyntheticReviewAction> Actions);
public sealed record SyntheticOccurrenceReference(
    string OccurrenceId, string ObjectId, string RuleId, string RuleVersion, string OriginalDigest);
public sealed record SyntheticFindingSeed(
    string FindingId, string CategoryId, SyntheticFindingState InitialState, string OriginalTitle,
    ImmutableArray<string> OriginalDigests, ImmutableArray<SyntheticOccurrenceReference> Occurrences);
public sealed record SyntheticReviewRunSeed(
    SyntheticReviewScope Scope, Guid RunId, string RunInputDigest, string AnalysisDigest,
    SyntheticReviewResourceState ResourceState, ImmutableArray<SyntheticFindingSeed> Findings);
public sealed record SyntheticReviewCommand(
    Guid EventId, long ExpectedRevision, SyntheticReviewEventKind Kind,
    string? Reason = null, string? Text = null, string? Title = null, string? BusinessContext = null);
public sealed record SyntheticFindingCurrent(
    string FindingId, string CategoryId, long Revision, SyntheticFindingState State,
    string PresentationTitle, string BusinessContext);
public sealed record SyntheticReviewEvent(
    Guid EventId, string ActorId, ImmutableArray<SyntheticReviewRole> ActorRoles,
    SyntheticReviewCommand Command, DateTimeOffset RecordedAtUtc, SyntheticFindingCurrent Outcome);
public sealed record SyntheticReviewedFinding(
    SyntheticFindingSeed Seed, SyntheticFindingCurrent Current, ImmutableArray<SyntheticReviewEvent> History);
public sealed record SyntheticReviewSnapshot(
    SyntheticReviewRunSeed RunSeed, ImmutableArray<SyntheticReviewedFinding> Findings, string SnapshotDigest);
public sealed record SyntheticReviewReadResult(SyntheticReviewIssue? Issue, SyntheticReviewSnapshot? Snapshot)
{
    public bool Succeeded => Issue is null && Snapshot is not null;
}
public sealed record SyntheticReviewSeedResult(SyntheticReviewIssue? Issue, bool AlreadyApplied = false)
{
    public bool Succeeded => Issue is null;
}
/// <summary>Replay returns the original event outcome, not a later current state. Read a fresh run snapshot for coherent UI/score overlays.</summary>
public sealed record SyntheticReviewApplyResult(
    SyntheticReviewIssue? Issue, SyntheticFindingCurrent? Outcome, bool AlreadyApplied = false)
{
    public bool Succeeded => Issue is null && Outcome is not null;
}
public interface ISyntheticReviewCommitObserver
{
    Task BeforeCommitAsync(string operation, Guid runId, Guid? eventId, CancellationToken cancellationToken);
}
