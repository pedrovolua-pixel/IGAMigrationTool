using System.Collections.Immutable;

namespace SyntheticTaskCsv;

public enum CsvIssue { InvalidInput, WrongScope, VersionMismatch, IntegrityMismatch, LimitExceeded, SourceConflict, Denied, RendererFailure, AuditUnavailable }
public sealed record CsvScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static CsvScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public sealed record CsvPhase1BLocks(string SchemaVersion, string OutcomeContractDigest, string PriorityPolicyVersion,
    string AiContractDigest, string CsvContractDigest, string OutcomeLockDigest, string AiFixtureDigest,
    string AiMappingDigest, string AiPacketDigest, string FixtureEpoch);
public sealed record CsvFrozenVersions(string ProfileVersion, string? DesiredOutcomeVersion, string ScoringAlgorithmVersion,
    string AiPolicyVersion, string PromptVersion, string ModelVersion, string ApplicationVersion, string WorkSchemaVersion,
    string ScriptedResultsDigest, string AnalysisFixtureDigest, string MaturityFixtureDigest, string FixPackageTemplateDigest,
    string FixReviewContractDigest, string PlanningTaskContractDigest, CsvPhase1BLocks Phase1bLocks);
public sealed record CsvVersionLocks(string BaselineId, string ProfileId, string RunInputDigest, CsvFrozenVersions FrozenVersions);
public sealed record CsvFindingRevision(string FindingId, long Revision);
public sealed record CsvSourceBinding(string SourceDigest, string GuidanceDigest, string FindingReviewDigest, long RunRevision,
    ImmutableArray<CsvFindingRevision> FindingRevisions);
public sealed record CsvAttestation(string TaskId, string ArtifactId, long Revision, Guid? EventId, string? Kind, string State, string? SourceDigest);
public sealed record CsvTaskRow(string CsvContractVersion, string RunId, string TaskId, string FindingId, string PackageId,
    string ScopedOptionId, string AssigneeId, long TaskRevision, string TaskStatus, string PlanFreshness,
    string CreatedAtUtc, string PlannedAtUtc, string CurrentSourceDigest, string PlannedSourceDigest,
    string ExportSnapshotDigest, string TaskLink, string FindingLink);
public sealed record CsvEnvelope(string SchemaVersion, CsvScope Scope, Guid RunId, CsvVersionLocks Versions,
    CsvSourceBinding CurrentSourceBinding, ImmutableArray<CsvAttestation> SelectedAttestations,
    ImmutableArray<CsvTaskRow> Rows, string SnapshotDigest);
public sealed record CsvResult(CsvIssue? Issue, CsvEnvelope? Envelope) { public bool Succeeded => Issue is null && Envelope is not null; }
public sealed record CsvRenderReceipt(string SchemaVersion, Guid RequestId, Guid RunId, string SnapshotDigest,
    string OutputSha256, string CsvContractVersion, int RowCount, bool ParityVerified);
public sealed record CsvVerificationResult(CsvIssue? Issue, CsvRenderReceipt? Receipt) { public bool Succeeded => Issue is null && Receipt is not null; }
