using System.Collections.Immutable;
using System.Text.Json;

namespace SyntheticMcp;

public enum ResourceKind { PublishedStatus, Coverage, Scores, Findings, Recommendations, ProtectedReferences }
public enum EvidenceCategory { Summary, Configuration, Identity, Operations }
public enum IdentityKind { NamedUser, Service, Anonymous, ShareLink, Support, Workload }
public enum McpOutcome { Success, InvalidRequest, Unavailable, Limited, Cancelled, DependencyUnavailable }
public enum Availability { Available, Unavailable, Redacted }
public enum SafeReason { None, DeclaredGap, Unavailable, Expired, Redacted }
public enum OperationalFailure { AuditUnavailable, PolicyUnavailable, SourceUnavailable, ClockUnavailable }

public static class McpContract
{
    public const string Version = "synthetic-published-health-read-v1";
    public const string FixtureKind = "SyntheticPublishedFixture";
    public const int MaxResponseBytes = 262144;
}

public sealed record Scope(string CustomerId, string ProjectId, string EnvironmentId);
public sealed record Caller(IdentityKind Kind, string IdentityId, Guid CorrelationId);
public sealed record ReadRequest(ResourceKind Kind, string AssessmentId, string ReportVersionId,
    int PageSize = 25, string? Cursor = null);
public sealed record ReadGrant(IdentityKind IdentityKind, string IdentityId, long Revision, Scope Scope,
    ResourceKind Kind, string AssessmentId, string ReportVersionId, string ManifestDigest,
    bool ActiveIdentity, bool ActiveAssignment, bool CustomerMcpPolicy, bool ResourceAvailable,
    bool ReadBlocked, bool ActionAllowed, ImmutableHashSet<EvidenceCategory> Categories,
    ImmutableHashSet<string> Fields);
public sealed record ManifestSource(ImmutableArray<byte> Bytes, string Digest);
public sealed record ManifestBindings(string ContractVersion, string FixtureKind, Scope Scope,
    string AssessmentId, string ReportVersionId, string BaselineVersion, string CatalogVersion,
    string ScoringProfileVersion, string MaturityProfileVersion, string ApplicationVersion,
    string AssessmentState, string ApprovalState);
public sealed record ItemDescriptor(ResourceKind Kind, string ItemId, EvidenceCategory Category, string PayloadDigest);
public sealed record ValidatedManifest(ManifestBindings Bindings, string Digest, ImmutableArray<ItemDescriptor> Items);
public sealed record ReferenceOverlay(Availability CurrentAvailability, SafeReason AvailabilityReason);
public sealed record ProjectedItem(JsonElement Content, ImmutableArray<string> ReturnedFields, ImmutableArray<string> RedactedFields);
public sealed record McpResult(McpOutcome Outcome, ImmutableArray<byte> Envelope, string? NextCursor = null)
{
    public static McpResult Denied(McpOutcome outcome) => new(outcome, ImmutableArray<byte>.Empty);
}
public sealed record McpAuditEvent(IdentityKind? IdentityKind, string? IdentityId, Scope? Scope,
    ResourceKind? ResourceKind, McpOutcome Outcome, Guid CorrelationId, long ElapsedMilliseconds,
    ImmutableArray<string> ReturnedFields, ImmutableArray<string> RedactedFields);

/// <summary>Only a server-owned synthetic reader; no raw resolver or locator input.</summary>
public interface IPublicationReader
{
    ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token);
    ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token);
}

/// <summary>Test authority must serialize revocation and TryCommit with the same fence.</summary>
public interface IReadPolicy
{
    ReadGrant? GetGrant(Caller caller, ReadRequest request);
    ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId);
    bool TryCommit(ReadGrant grant, Action commit);
}

public interface IMcpAudit
{
    bool Complete(McpAuditEvent auditEvent);
    void OperationalFailure(OperationalFailure failure, Guid correlationId);
}

public interface IMonotonicClock
{
    TimeSpan Elapsed { get; }
}

// A-D1 freezes these method names; bodies belong to PublicationCodec.cs / Projection.cs.
// PublicationCodec.Validate(ManifestSource, ReadGrant) -> ValidatedManifest?
// PublicationCodec.ReadPayload(ImmutableArray<byte>, ItemDescriptor, ValidatedManifest) -> JsonElement?
// PublicationCodec.CanonicalBytes(JsonElement) -> byte[]
// PublicationCodec.IsId(string?) -> bool
// Projection.Fields(ResourceKind) -> ImmutableHashSet<string>
// Projection.Project(JsonElement, ItemDescriptor, ValidatedManifest, ReadGrant, ReferenceOverlay?) -> ProjectedItem?
// Projection.Envelope(ValidatedManifest, ResourceKind, IEnumerable<ProjectedItem>, string?) -> ImmutableArray<byte>
// B-D1 entry: new McpHarness(IPublicationReader, IReadPolicy, IMcpAudit, IMonotonicClock)
// McpHarness.ReadAsync(Caller, ReadOnlyMemory<byte>, CancellationToken = default) -> Task<McpResult>
