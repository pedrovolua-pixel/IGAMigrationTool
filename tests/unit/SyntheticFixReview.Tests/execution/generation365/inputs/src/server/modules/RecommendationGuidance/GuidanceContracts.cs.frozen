using System.Collections.Immutable;
using System.Text.Json;
namespace RecommendationGuidance;

public enum GuidanceIssue { InvalidInput, UnknownVersion, WrongScope, InvalidSource, SourceMismatch, InvalidContent, DuplicateId }
public sealed record GuidanceScope(string CustomerId, string ProjectId, string EnvironmentId);
// Already-authorized host bindings; this module grants no authority and resolves no evidence.
public sealed record GuidanceSourceBinding(GuidanceScope Scope, Guid RunId, long RunRevision, string RunState,
    string RunInputDigest, string BaselineId, string ProfileId, JsonElement FrozenVersions, JsonElement CapabilityLock,
    JsonElement AnalysisLock, string AnalysisFixtureDigest, string AnalysisContentDigest, string SavedCoverageDigest,
    Guid ReviewRunId, long ReviewRunRevision, string ReviewSnapshotDigest);
public sealed record GuidanceOccurrence(string OccurrenceId, string ObjectId, string ObjectType, string ModuleId,
    string OriginalDigest, string EvidenceReference);
public sealed record GuidanceOptionInput(string OptionId, string Text, string Prerequisites, string Risk, string RecoveryGuidance);
public sealed record GuidanceFindingInput(string FindingId, string RuleId, string RuleVersion, string CategoryId,
    string Severity, string OriginalTitle, string PresentationTitle, string BusinessContext, string InitialState,
    string CurrentState, long FindingRevision, string RootCause, ImmutableArray<GuidanceOccurrence> Occurrences,
    ImmutableArray<GuidanceOptionInput> Options, ImmutableArray<string> ValidationGuidance,
    ImmutableArray<string> GuidanceReferences, ImmutableArray<string> Assumptions, ImmutableArray<string> Limitations);
public sealed record GuidanceInput(GuidanceSourceBinding Source, ImmutableArray<GuidanceFindingInput> Findings);
public sealed record GuidanceOption(string ScopedOptionId, string OptionId, string Status, string Text,
    string Prerequisites, string Risk, string RecoveryGuidance);
public sealed record GuidanceFinding(string FindingId, string RuleId, string RuleVersion, string CategoryId,
    string Severity, string OriginalTitle, string PresentationTitle, string BusinessContext, string InitialState,
    string CurrentState, long FindingRevision, string RootCause, ImmutableArray<GuidanceOccurrence> Occurrences,
    ImmutableArray<GuidanceOption> Options, ImmutableArray<string> ValidationGuidance,
    ImmutableArray<string> GuidanceReferences, ImmutableArray<string> Assumptions, ImmutableArray<string> Limitations);
public sealed record GuidanceSnapshot(string SchemaVersion, string Status, GuidanceSourceBinding Source,
    string ContentDigest, ImmutableArray<GuidanceFinding> Findings, ImmutableArray<string> Warnings,
    ImmutableArray<string> UnavailableSections);
public sealed record GuidanceResult(GuidanceIssue? Issue, GuidanceSnapshot? Snapshot)
{ public bool Succeeded => Issue is null && Snapshot is not null; }
// public static GuidanceResult RecommendationGuidanceBuilder.Build(GuidanceInput? input)
// schemaVersion: synthetic-recommendation-guidance-v1; status: SyntheticUnverified; every option status: Unverified.
// ScopedOptionId = lowercase SHA256 of UTF8 canonical JSON (no trailing newline), sorted ordinal property names:
// {"findingId":...,"optionId":...,"runId":lowercase UUID,"scope":{"customerId":...,"environmentId":...,"projectId":...}}.
// ContentDigest binds every schema/status/source/content field except ContentDigest itself; ordinal object keys,
// arrays Findings/Occurrences/Options sorted by FindingId/OccurrenceId/OptionId; ordered guidance string arrays retain order.
