using System.Text.Json;

namespace ReportDrafts;

public enum DraftReportIssue { InvalidInput, UnknownVersion, WrongScope, InvalidSource, SourceMismatch, InvalidContent, DuplicateId, InvalidSnapshot }
public sealed record DraftScope(string CustomerId, string ProjectId, string EnvironmentId);
/// <summary>Already-authorized synthetic bindings, not an integrity validator or authority grant.</summary>
public sealed record DraftSourceBinding(DraftScope Scope, Guid RunId, long RunRevision, string RunState,
    string RunInputDigest, string BaselineId, string ProfileId, JsonElement FrozenVersions, JsonElement CapabilityLock, JsonElement AnalysisLock,
    string AnalysisFixtureDigest, string AnalysisContentDigest, string ScoringContentDigest, string SavedCoverageDigest,
    Guid ReviewRunId, long ReviewRunRevision, string ReviewSnapshotDigest,
    string MaturityFixtureDigest, string MaturityInputDigest, string MaturityContentDigest);
/// <summary>Host-created data-only content; never a client DTO or raw evidence resolver.</summary>
public sealed record DraftReportInput(DraftSourceBinding Source, JsonElement Content);
/// <summary>Detached immutable returned value. No durable ReportVersion or publication is established.</summary>
public sealed record DraftReportSnapshot(string SchemaVersion, string Status, DraftSourceBinding Source,
    string CanonicalContentDigest, JsonElement Content);
public sealed record DraftReportResult(DraftReportIssue? Issue, DraftReportSnapshot? Snapshot)
{
    public bool Succeeded => Issue is null && Snapshot is not null;
}
