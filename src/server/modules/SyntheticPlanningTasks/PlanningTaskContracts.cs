using System.Collections.Immutable;
using Npgsql;
using SyntheticFixPackages;
using SyntheticFixReview;

namespace SyntheticPlanningTasks;

public sealed record PlanningTaskScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static PlanningTaskScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public enum PlanningTaskRole { Consultant, QualifiedReviewer, Auditor, Executive, CustomerRiskOwner, PlatformSupport }
public enum PlanningTaskAction { Read, Create, Manage, Comment }
public enum PlanningTaskResourceState { Mutable, Published, Deleted, Blocked, Expired }
public enum PlanningTaskStatus { Planned, InProgress, Completed, Cancelled }
public enum PlanningTaskFreshness { CurrentPlan, NeedsReconfirmation, SourceUnavailable }
public enum PlanningTaskKind { Create, ReconfirmPlan, StartProgress, ReturnToPlanned, Complete, Cancel, Reopen, Comment }
public enum PlanningTaskIssue { InvalidInput, Denied, WrongScope, InvalidState, NotFound, RevisionConflict, SourceConflict, EventConflict, SeedConflict, IntegrityMismatch, MigrationDrift, NotInitialized, SourceUnavailable, RevisionOverflow }
public sealed record PlanningTaskAuthority(string ActorId, bool Authenticated, bool Active, bool Revoked, bool AssignmentActive,
    PlanningTaskScope AssignedScope, ImmutableArray<PlanningTaskRole> Roles, ImmutableArray<string> Categories,
    ImmutableArray<PlanningTaskAction> Actions, PlanningTaskResourceState ResourceState);
public sealed record PlanningTaskIdentity(string TaskId, string FindingId, string CategoryId, string PackageId, string ScopedOptionId, ImmutableArray<string> ArtifactIds);
public sealed record PlanningTaskAttestation(string ArtifactId, long Revision, Guid? EventId, ArtifactReviewKind? Kind, ArtifactReviewState State, string? SourceDigest);
public sealed record PlanningTaskSourceBinding(ArtifactReviewSourceBinding ArtifactSource, string PlanningTaskContractDigest);
public sealed record PlanningTaskOption(PlanningTaskIdentity Identity, string FindingState, ImmutableArray<PlanningTaskAttestation> CurrentAttestations, bool CanCreate);
public sealed record PlanningTaskCommand(Guid EventId, PlanningTaskKind Kind, long ExpectedRevision, string ExpectedSourceDigest, ImmutableArray<PlanningTaskAttestation> ExpectedAttestations, string Reason);
public sealed record PlanningTaskEvent(Guid EventId, long Revision, PlanningTaskKind Kind, string ActorId, ImmutableArray<string> ActorRoles, DateTimeOffset RecordedAtUtc, string Reason,
    PlanningTaskSourceBinding Source, ImmutableArray<PlanningTaskAttestation> Attestations, PlanningTaskStatus RecordedStatus, Guid PlanningEventId);
public sealed record PlanningTaskReceipt(string SchemaVersion, Guid EventId, Guid RunId, string TaskId, PlanningTaskKind Kind, long Revision, string ActorId, DateTimeOffset RecordedAtUtc, string SourceDigest);
public sealed record PlanningTaskEntry(PlanningTaskIdentity Identity, string AssigneeId, long Revision, PlanningTaskStatus Status, PlanningTaskFreshness Freshness,
    PlanningTaskEvent Creation, PlanningTaskEvent Plan, ImmutableArray<PlanningTaskEvent> History,
    bool CanReconfirm, bool CanStart, bool CanReturnToPlanned, bool CanComplete, bool CanCancel, bool CanReopen, bool CanComment);
public sealed record PlanningTaskSnapshot(PlanningTaskSourceBinding? Source, string ActorId, ImmutableArray<PlanningTaskOption> Options, ImmutableArray<PlanningTaskEntry> Entries);
public sealed record PlanningTaskUnavailableHistory(Guid EventId, long Revision, PlanningTaskKind Kind, string ActorId, ImmutableArray<string> ActorRoles, DateTimeOffset RecordedAtUtc, PlanningTaskStatus RecordedStatus, Guid PlanningEventId);
public sealed record PlanningTaskUnavailableEntry(PlanningTaskIdentity Identity, string AssigneeId, long Revision, PlanningTaskStatus Status, PlanningTaskFreshness Freshness, ImmutableArray<PlanningTaskUnavailableHistory> History);
public sealed class PlanningTaskSource
{
    internal PlanningTaskSource(FixPackageSnapshot packages, ArtifactReviewSnapshot artifacts, PlanningTaskSourceBinding binding, ImmutableArray<PlanningTaskOption> options)
    { Packages = packages; Artifacts = artifacts; Binding = binding; Options = options; }
    internal FixPackageSnapshot Packages { get; }
    internal ArtifactReviewSnapshot Artifacts { get; }
    internal string CanonicalPackage => Packages.CanonicalJson;
    public PlanningTaskSourceBinding Binding { get; }
    public ImmutableArray<PlanningTaskOption> Options { get; }
}
public sealed record PlanningTaskSourceResult(PlanningTaskIssue? Issue, PlanningTaskSource? Source) { public bool Succeeded => Issue is null && Source is not null; }
public sealed record PlanningTaskReadResult(PlanningTaskIssue? Issue, PlanningTaskSnapshot? Snapshot) { public bool Succeeded => Issue is null && Snapshot is not null; }
public sealed record PlanningTaskApplyResult(PlanningTaskIssue? Issue, PlanningTaskReceipt? Receipt, bool AlreadyApplied = false, string? AlreadyExistsTaskId = null)
{ public bool Succeeded => Issue is null && (Receipt is not null || AlreadyExistsTaskId is not null); }
public delegate Task<PlanningTaskSourceResult> PlanningTaskSourceReader(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, PlanningTaskAuthority authority, CancellationToken cancellationToken);
public interface ISyntheticPlanningTaskCommitObserver
{ Task BeforeCommitAsync(string operation, Guid runId, string taskId, Guid eventId, CancellationToken cancellationToken); }
public sealed class PlanningTaskIntegrityException() : Exception("Synthetic planning task integrity denied.");
