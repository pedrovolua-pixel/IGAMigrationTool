using System.Runtime.CompilerServices;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;

[assembly: InternalsVisibleTo("SyntheticEvaluationSchemaProvenanceIntegration.UnitTests")]
namespace SyntheticEvaluationSchemaProvenanceIntegration;

public enum Phase1BAcceptedSchemaIssue { InvalidInput, Denied, NotInitialized, NotFound, SourceUnavailable, MigrationDrift, IntegrityMismatch }
public sealed record Phase1BAcceptedSchemaResult(Phase1BAcceptedSchemaIssue? Issue, Phase1BAcceptedSchemaReadinessProjection? Readiness)
{
    public bool HasReadiness => Issue is null && Readiness is not null;
}
public sealed class Phase1BAcceptedSchemaVersionBinding
{
    internal Phase1BAcceptedSchemaVersionBinding(SamplingVersionKind kind, Phase1BPopulationBindingState state, string? reference, string? valueJson,
        string? knownFactsJson, IEnumerable<string> originPaths, string? missingReason)
    { Kind = kind; State = state; Reference = reference; ValueJson = valueJson; KnownFactsJson = knownFactsJson; OriginPaths = Array.AsReadOnly(originPaths.Order(StringComparer.Ordinal).ToArray()); MissingReason = missingReason; }
    public SamplingVersionKind Kind { get; }
    public Phase1BPopulationBindingState State { get; }
    public string? Reference { get; }
    public string? ValueJson { get; }
    public string? KnownFactsJson { get; }
    public IReadOnlyList<string> OriginPaths { get; }
    public string? MissingReason { get; }
}
public sealed class Phase1BAcceptedSchemaReadinessProjection
{
    internal Phase1BAcceptedSchemaReadinessProjection(Guid runId, long runRevision, string scopeId, string environmentId, string populationCanonicalJson,
        string populationContentDigest, string schemaProofCanonicalJson, string schemaProofContentDigest, IEnumerable<Phase1BAcceptedSchemaVersionBinding> bindings)
    {
        RunId = runId; RunRevision = runRevision; ScopeId = scopeId; EnvironmentId = environmentId;
        PopulationCanonicalJson = populationCanonicalJson; PopulationContentDigest = populationContentDigest;
        SchemaProofCanonicalJson = schemaProofCanonicalJson; SchemaProofContentDigest = schemaProofContentDigest;
        VersionBindings = Array.AsReadOnly(bindings.OrderBy(b => (int)b.Kind).ToArray());
        MissingVersionKinds = Array.AsReadOnly(VersionBindings.Where(b => b.State == Phase1BPopulationBindingState.Missing).Select(b => b.Kind).ToArray());
        CompleteSamplingReady = VersionBindings.Count == Enum.GetValues<SamplingVersionKind>().Length && MissingVersionKinds.Count == 0;
        CanonicalJson = AiExecutionCanonical.Serialize(new
        {
            schemaVersion = "synthetic-phase1b-evaluation-schema-readiness-v1",
            RunId,
            RunRevision,
            ScopeId,
            EnvironmentId,
            PopulationCanonicalJson,
            PopulationContentDigest,
            SchemaProofCanonicalJson,
            SchemaProofContentDigest,
            VersionBindings,
            MissingVersionKinds,
            CompleteSamplingReady
        });
        ContentDigest = AiExecutionCanonical.Hash(CanonicalJson);
    }
    public Guid RunId { get; }
    public long RunRevision { get; }
    public string ScopeId { get; }
    public string EnvironmentId { get; }
    public string PopulationCanonicalJson { get; }
    public string PopulationContentDigest { get; }
    public string SchemaProofCanonicalJson { get; }
    public string SchemaProofContentDigest { get; }
    public IReadOnlyList<Phase1BAcceptedSchemaVersionBinding> VersionBindings { get; }
    public IReadOnlyList<SamplingVersionKind> MissingVersionKinds { get; }
    public bool CompleteSamplingReady { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
