using System.Runtime.CompilerServices;
using SyntheticEvaluation;
using SyntheticEvaluationSourceIntegration;

[assembly: InternalsVisibleTo("SyntheticEvaluationPopulationIntegration.UnitTests")]
namespace SyntheticEvaluationPopulationIntegration;

public enum Phase1BPopulationIssue { InvalidInput, Denied, NotInitialized, NotFound, SourceUnavailable, MigrationDrift, IntegrityMismatch }
public enum Phase1BPopulationBindingState { SourceBound, Missing }
public sealed record Phase1BPopulationResult(Phase1BPopulationIssue? Issue, Phase1BPopulationProjection? Population)
{
    public bool HasPopulation => Issue is null && Population is not null;
}
public sealed class Phase1BPopulationVersionBinding
{
    internal Phase1BPopulationVersionBinding(SamplingVersionKind kind, Phase1BPopulationBindingState state,
        string? reference, string? valueJson, string? knownFactsJson, IEnumerable<string> originPaths, string? missingReason)
    {
        Kind = kind; State = state; Reference = reference; ValueJson = valueJson; KnownFactsJson = knownFactsJson;
        OriginPaths = Array.AsReadOnly(originPaths.Order(StringComparer.Ordinal).ToArray()); MissingReason = missingReason;
    }
    public SamplingVersionKind Kind { get; }
    public Phase1BPopulationBindingState State { get; }
    public string? Reference { get; }
    public string? ValueJson { get; }
    public string? KnownFactsJson { get; }
    public IReadOnlyList<string> OriginPaths { get; }
    public string? MissingReason { get; }
}
public sealed class Phase1BPopulationMember
{
    internal Phase1BPopulationMember(string memberId, string scopeId, string environmentId, string primaryModuleId,
        string categoryId, string ruleVersionId, string modelPromptVersionId, string confidenceBandId, SamplingSeverity severity,
        string nativeGroupId, string nativeModuleId, string nativeCategoryId, string nativeRuleId, string nativeRuleVersion,
        string nativeConfidenceBand, decimal nativeConfidencePercent, int affectedObjectCount,
        IEnumerable<Phase1BEvaluationSourceOccurrence> occurrences)
    {
        MemberId = memberId; ScopeId = scopeId; EnvironmentId = environmentId; PrimaryModuleId = primaryModuleId;
        CategoryId = categoryId; RuleVersionId = ruleVersionId; ModelPromptVersionId = modelPromptVersionId; ConfidenceBandId = confidenceBandId;
        Severity = severity; NativeGroupId = nativeGroupId; NativeModuleId = nativeModuleId; NativeCategoryId = nativeCategoryId;
        NativeRuleId = nativeRuleId; NativeRuleVersion = nativeRuleVersion; NativeConfidenceBand = nativeConfidenceBand;
        NativeConfidencePercent = nativeConfidencePercent; AffectedObjectCount = affectedObjectCount;
        Occurrences = Array.AsReadOnly(occurrences.OrderBy(o => o.OccurrenceId, StringComparer.Ordinal).ToArray());
    }
    public string MemberId { get; }
    public string ScopeId { get; }
    public string EnvironmentId { get; }
    public string PrimaryModuleId { get; }
    public string CategoryId { get; }
    public string RuleVersionId { get; }
    public string ModelPromptVersionId { get; }
    public string ConfidenceBandId { get; }
    public SamplingSeverity Severity { get; }
    public string NativeGroupId { get; }
    public string NativeModuleId { get; }
    public string NativeCategoryId { get; }
    public string NativeRuleId { get; }
    public string NativeRuleVersion { get; }
    public string NativeConfidenceBand { get; }
    public decimal NativeConfidencePercent { get; }
    public int AffectedObjectCount { get; }
    public IReadOnlyList<Phase1BEvaluationSourceOccurrence> Occurrences { get; }
}
public sealed class Phase1BPopulationProjection
{
    internal Phase1BPopulationProjection(Guid runId, long runRevision, string sourceCaptureDigest, string sourceCanonicalJson,
        string runPlanJson, string capabilityLockJson, string frozenInputsJson, DateTimeOffset sourceObservedAtDatabaseUtc,
        DateTimeOffset runObservedAtDatabaseUtc, string scopeId, string environmentId, IEnumerable<Phase1BPopulationMember> members,
        IEnumerable<Phase1BEvaluationSourceGap> gaps, IEnumerable<Phase1BPopulationVersionBinding> versionBindings)
    {
        RunId = runId; RunRevision = runRevision; SourceCaptureDigest = sourceCaptureDigest; SourceCanonicalJson = sourceCanonicalJson;
        RunPlanJson = runPlanJson; CapabilityLockJson = capabilityLockJson; FrozenInputsJson = frozenInputsJson;
        SourceObservedAtDatabaseUtc = sourceObservedAtDatabaseUtc; RunObservedAtDatabaseUtc = runObservedAtDatabaseUtc;
        ScopeId = scopeId; EnvironmentId = environmentId;
        Members = Array.AsReadOnly(members.OrderBy(m => m.MemberId, StringComparer.Ordinal).ToArray());
        Gaps = Array.AsReadOnly(gaps.OrderBy(g => g.CoverageKey.EvidenceCategory, StringComparer.Ordinal).ThenBy(g => g.CoverageKey.InventoryId, StringComparer.Ordinal).ToArray());
        VersionBindings = Array.AsReadOnly(versionBindings.OrderBy(b => (int)b.Kind).ToArray());
        MissingVersionKinds = Array.AsReadOnly(VersionBindings.Where(b => b.State == Phase1BPopulationBindingState.Missing).Select(b => b.Kind).ToArray());
        CompleteSamplingReady = VersionBindings.Count == Enum.GetValues<SamplingVersionKind>().Length && MissingVersionKinds.Count == 0;
        CanonicalJson = PopulationCanonical.Json(new
        {
            schemaVersion = "synthetic-phase1b-evaluation-population-v1", RunId, RunRevision, SourceCaptureDigest, SourceCanonicalJson,
            RunPlanJson, CapabilityLockJson, FrozenInputsJson,
            sourceObservedAtDatabaseUtc = PopulationCanonical.Date(SourceObservedAtDatabaseUtc),
            runObservedAtDatabaseUtc = PopulationCanonical.Date(RunObservedAtDatabaseUtc), ScopeId, EnvironmentId,
            members = Members.Select(m => new
            {
                m.MemberId, m.ScopeId, m.EnvironmentId, m.PrimaryModuleId, m.CategoryId, m.RuleVersionId, m.ModelPromptVersionId,
                m.ConfidenceBandId, m.Severity, m.NativeGroupId, m.NativeModuleId, m.NativeCategoryId, m.NativeRuleId, m.NativeRuleVersion,
                m.NativeConfidenceBand, m.NativeConfidencePercent, m.AffectedObjectCount,
                occurrences = m.Occurrences.Select(o => new { o.OccurrenceId, o.CoverageKey, o.OriginalJson, o.OriginalDigest, o.GeneratedFindingJson })
            }),
            gaps = Gaps.Select(g => new { g.CoverageKey, g.State, g.ReasonCode, g.Stage }),
            VersionBindings, MissingVersionKinds, CompleteSamplingReady
        });
        ContentDigest = PopulationCanonical.Hash(CanonicalJson);
    }
    public Guid RunId { get; }
    public long RunRevision { get; }
    public string SourceCaptureDigest { get; }
    public string SourceCanonicalJson { get; }
    public string RunPlanJson { get; }
    public string CapabilityLockJson { get; }
    public string FrozenInputsJson { get; }
    public DateTimeOffset SourceObservedAtDatabaseUtc { get; }
    public DateTimeOffset RunObservedAtDatabaseUtc { get; }
    public string ScopeId { get; }
    public string EnvironmentId { get; }
    public IReadOnlyList<Phase1BPopulationMember> Members { get; }
    public IReadOnlyList<Phase1BEvaluationSourceGap> Gaps { get; }
    public IReadOnlyList<Phase1BPopulationVersionBinding> VersionBindings { get; }
    public IReadOnlyList<SamplingVersionKind> MissingVersionKinds { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
    public bool CompleteSamplingReady { get; }
}
