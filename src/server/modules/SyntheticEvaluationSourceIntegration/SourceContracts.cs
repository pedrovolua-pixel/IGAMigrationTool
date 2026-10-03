using System.Runtime.CompilerServices;
using AssessmentCoverage;
[assembly: InternalsVisibleTo("SyntheticEvaluationSourceIntegration.UnitTests")]
namespace SyntheticEvaluationSourceIntegration;

public enum Phase1BEvaluationSourceIssue
{
    InvalidInput, Denied, NotFound, NotInitialized, MigrationDrift, SourceUnavailable, IntegrityMismatch
}
public sealed record Phase1BEvaluationSourceResult(Phase1BEvaluationSourceIssue? Issue, Phase1BEvaluationSourceCapture? Capture);
public sealed class Phase1BEvaluationSourceOccurrence
{
    internal Phase1BEvaluationSourceOccurrence(string occurrenceId, CoverageKey key, string originalJson,
        string originalDigest, string generatedFindingJson)
    {
        OccurrenceId = occurrenceId; CoverageKey = new(key.InventoryId, key.EvidenceCategory);
        OriginalJson = originalJson; OriginalDigest = originalDigest; GeneratedFindingJson = generatedFindingJson;
    }
    public string OccurrenceId { get; }
    public CoverageKey CoverageKey { get; }
    public string OriginalJson { get; }
    public string OriginalDigest { get; }
    public string GeneratedFindingJson { get; }
}
public sealed class Phase1BEvaluationSourceMember
{
    internal Phase1BEvaluationSourceMember(string groupId, int affectedObjectCount, IEnumerable<Phase1BEvaluationSourceOccurrence> occurrences)
    {
        GroupId = groupId; AffectedObjectCount = affectedObjectCount;
        Occurrences = Array.AsReadOnly(occurrences.OrderBy(o => o.OccurrenceId, StringComparer.Ordinal).ToArray());
    }
    public string GroupId { get; }
    public int AffectedObjectCount { get; }
    public IReadOnlyList<Phase1BEvaluationSourceOccurrence> Occurrences { get; }
}
public sealed class Phase1BEvaluationSourceGap
{
    internal Phase1BEvaluationSourceGap(CoverageKey key, CoverageState state, string? reasonCode, string stage)
    { CoverageKey = new(key.InventoryId, key.EvidenceCategory); State = state; ReasonCode = reasonCode; Stage = stage; }
    public CoverageKey CoverageKey { get; }
    public CoverageState State { get; }
    public string? ReasonCode { get; }
    public string Stage { get; }
}
public sealed class Phase1BEvaluationSourceCapture
{
    internal Phase1BEvaluationSourceCapture(Guid runId, long runRevision, string inputDigest, string aiSnapshotDigest,
        string outcomeLockDigest, string analysisDigest, DateTimeOffset observedAtDatabaseUtc,
        IEnumerable<Phase1BEvaluationSourceMember> members, IEnumerable<Phase1BEvaluationSourceGap> gaps, string canonicalJson)
    {
        RunId = runId; RunRevision = runRevision; InputDigest = inputDigest; AiSnapshotDigest = aiSnapshotDigest;
        OutcomeLockDigest = outcomeLockDigest; AnalysisDigest = analysisDigest; ObservedAtDatabaseUtc = observedAtDatabaseUtc;
        Members = Array.AsReadOnly(members.OrderBy(m => m.GroupId, StringComparer.Ordinal).ToArray());
        Gaps = Array.AsReadOnly(gaps.OrderBy(g => g.CoverageKey.EvidenceCategory, StringComparer.Ordinal)
            .ThenBy(g => g.CoverageKey.InventoryId, StringComparer.Ordinal).ToArray());
        CanonicalJson = canonicalJson; ContentDigest = SourceCanonical.Hash(canonicalJson);
    }
    public Guid RunId { get; }
    public long RunRevision { get; }
    public string InputDigest { get; }
    public string AiSnapshotDigest { get; }
    public string OutcomeLockDigest { get; }
    public string AnalysisDigest { get; }
    public DateTimeOffset ObservedAtDatabaseUtc { get; }
    public IReadOnlyList<Phase1BEvaluationSourceMember> Members { get; }
    public IReadOnlyList<Phase1BEvaluationSourceGap> Gaps { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
