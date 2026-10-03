using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SyntheticEvaluationSchemaProvenanceIntegration.UnitTests")]
namespace SyntheticAiExecution;

public sealed class AiAcceptedSchemaRecord
{
    internal AiAcceptedSchemaRecord(AiAttemptKey attempt, string receiptId, string outputDigest, string outputSchemaVersion, string acceptedSnapshotDigest, string acceptedSnapshotSchemaVersion)
    { Attempt = attempt; ReceiptId = receiptId; OutputDigest = outputDigest; OutputSchemaVersion = outputSchemaVersion; AcceptedSnapshotDigest = acceptedSnapshotDigest; AcceptedSnapshotSchemaVersion = acceptedSnapshotSchemaVersion; }
    public AiAttemptKey Attempt { get; }
    public string ReceiptId { get; }
    public string OutputDigest { get; }
    public string OutputSchemaVersion { get; }
    public string AcceptedSnapshotDigest { get; }
    public string AcceptedSnapshotSchemaVersion { get; }
}
public sealed class AiAcceptedSchemaWork
{
    internal AiAcceptedSchemaWork(string workId, string category, long workRevision, AiWorkState state, string inputSchemaVersion, string packetDigest, AiAcceptedSchemaRecord? accepted, string? missingReason)
    { WorkId = workId; Category = category; WorkRevision = workRevision; State = state; InputSchemaVersion = inputSchemaVersion; PacketDigest = packetDigest; Accepted = accepted; MissingReason = missingReason; }
    public string WorkId { get; }
    public string Category { get; }
    public long WorkRevision { get; }
    public AiWorkState State { get; }
    public string InputSchemaVersion { get; }
    public string PacketDigest { get; }
    public AiAcceptedSchemaRecord? Accepted { get; }
    public string? MissingReason { get; }
}
public sealed class AiAcceptedSchemaProof
{
    internal AiAcceptedSchemaProof(AiRunLock runLock, string sourceSnapshotDigest, IEnumerable<AiAcceptedSchemaWork> works)
    {
        RunLock = runLock; SourceSnapshotDigest = sourceSnapshotDigest;
        Works = Array.AsReadOnly(works.OrderBy(w => w.WorkId, StringComparer.Ordinal).ToArray());
        CanonicalJson = AiExecutionCanonical.Serialize(new { schemaVersion = "synthetic-ai-accepted-schema-proof-v1", RunLock, SourceSnapshotDigest, Works });
        ContentDigest = AiExecutionCanonical.Hash(CanonicalJson);
    }
    public AiRunLock RunLock { get; }
    public string SourceSnapshotDigest { get; }
    public IReadOnlyList<AiAcceptedSchemaWork> Works { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
