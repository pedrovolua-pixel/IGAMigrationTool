using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentRuns;
using Npgsql;
using SyntheticAiExecution;
using SyntheticAiValidation;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;
using SyntheticOutcomePriority;

namespace SyntheticEvaluationSchemaProvenanceIntegration;

public sealed class Phase1BAcceptedSchemaSourceAdapter
{
    private readonly Phase1BPopulationSourceAdapter population;
    private readonly SyntheticAiExecutionStore ai;
    public Phase1BAcceptedSchemaSourceAdapter(SyntheticDurableRunEngine runs, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes)
    { population = new(runs, ai, outcomes); this.ai = ai ?? throw new ArgumentNullException(nameof(ai)); }
    private static Phase1BAcceptedSchemaResult Deny(Phase1BAcceptedSchemaIssue issue) => new(issue, null);
    public async Task<Phase1BAcceptedSchemaResult> CaptureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId,
        AiAuthority aiAuthority, OutcomeAuthority outcomeAuthority, CancellationToken cancellationToken = default)
    {
        var source = await population.CaptureAsync(connection, transaction, runId, aiAuthority, outcomeAuthority, cancellationToken);
        if (source.Issue is { } issue) return Deny(Enum.Parse<Phase1BAcceptedSchemaIssue>(issue.ToString()));
        if (source.Population is null) return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
        string digest;
        try
        {
            using var capture = JsonDocument.Parse(source.Population.SourceCanonicalJson);
            digest = capture.RootElement.GetProperty("aiSnapshotDigest").GetString()!;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        { return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch); }
        var schema = await ai.ReadAcceptedSchemaInTransactionAsync(connection, transaction, aiAuthority, runId, digest, cancellationToken);
        if (schema.Issue is { } aiIssue) return Deny(Map(aiIssue));
        if (schema.Value is null) return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
        return Compose(source.Population.CanonicalJson, source.Population.ContentDigest, schema.Value);
    }
    internal static Phase1BAcceptedSchemaResult Compose(string populationCanonicalJson, string populationContentDigest, AiAcceptedSchemaProof proof)
    {
        try
        {
            if (AiExecutionCanonical.Hash(populationCanonicalJson) != populationContentDigest || AiExecutionCanonical.Hash(proof.CanonicalJson) != proof.ContentDigest)
                return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
            using var document = JsonDocument.Parse(populationCanonicalJson); var p = document.RootElement;
            using var capture = JsonDocument.Parse(p.GetProperty("sourceCanonicalJson").GetString()!); var c = capture.RootElement;
            using var snapshot = JsonDocument.Parse(c.GetProperty("aiSnapshotJson").GetString()!); var a = snapshot.RootElement;
            var selfEmpty = JsonNode.Parse(a.GetRawText())!.AsObject(); selfEmpty["contentDigest"] = "";
            var runId = p.GetProperty("runId").GetGuid(); var runRevision = p.GetProperty("runRevision").GetInt64();
            if (p.GetProperty("schemaVersion").GetString() != "synthetic-phase1b-evaluation-population-v1" ||
                c.GetProperty("runId").GetGuid() != runId || c.GetProperty("runRevision").GetInt64() != runRevision ||
                AiExecutionCanonical.Hash(c.GetRawText()) != p.GetProperty("sourceCaptureDigest").GetString() ||
                c.GetProperty("aiSnapshotDigest").GetString() != proof.SourceSnapshotDigest || a.GetProperty("contentDigest").GetString() != proof.SourceSnapshotDigest ||
                AiExecutionCanonical.Digest(selfEmpty) != proof.SourceSnapshotDigest || proof.RunLock.RunId != runId || proof.RunLock.Scope != AiScope.Fixed ||
                AiExecutionCanonical.Serialize(proof.RunLock) != c.GetProperty("aiRunLockJson").GetString() ||
                c.GetProperty("inputDigest").GetString() != proof.RunLock.InputDigest ||
                AiExecutionCanonical.Serialize(a.GetProperty("runLock")) != AiExecutionCanonical.Serialize(proof.RunLock))
                return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
            var nativeScope = c.GetProperty("scope");
            if (nativeScope.GetProperty("customerId").GetString() != proof.RunLock.Scope.CustomerId || nativeScope.GetProperty("projectId").GetString() != proof.RunLock.Scope.ProjectId ||
                nativeScope.GetProperty("environmentId").GetString() != proof.RunLock.Scope.EnvironmentId || proof.Works.Count != a.GetProperty("works").GetArrayLength() ||
                proof.Works.Select(w => w.WorkId).Distinct(StringComparer.Ordinal).Count() != proof.Works.Count)
                return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
            foreach (var work in proof.Works)
            {
                var native = a.GetProperty("works").EnumerateArray().Single(x => x.GetProperty("work").GetProperty("workId").GetString() == work.WorkId);
                var raw = native.GetProperty("work"); using var input = JsonDocument.Parse(raw.GetProperty("packetInputJson").GetString()!);
                var built = SyntheticAiPacketBuilder.Build(raw.GetProperty("packetInputJson").GetString());
                if (raw.GetProperty("category").GetString() != work.Category || native.GetProperty("revision").GetInt64() != work.WorkRevision ||
                    native.GetProperty("state").GetString() != work.State.ToString() || !built.Succeeded || built.Packet!.ContentDigest != work.PacketDigest ||
                    input.RootElement.GetProperty("schemaVersion").GetString() != work.InputSchemaVersion ||
                    work.State is not (AiWorkState.Succeeded or AiWorkState.Failed)) return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
                if (work.State == AiWorkState.Failed)
                { if (work.Accepted is not null || work.MissingReason != "NoAcceptedOutput") return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch); continue; }
                var accepted = work.Accepted;
                if (accepted is null || work.MissingReason is not null) return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
                var attempts = native.GetProperty("attempts"); var last = attempts[attempts.GetArrayLength() - 1]; var receipt = last.GetProperty("receipt");
                if (AiExecutionCanonical.Serialize(accepted.Attempt) != AiExecutionCanonical.Serialize(last.GetProperty("key")) ||
                    AiExecutionCanonical.Serialize(accepted.Attempt) != AiExecutionCanonical.Serialize(receipt.GetProperty("attempt")) ||
                    receipt.GetProperty("receiptId").GetString() != accepted.ReceiptId || receipt.GetProperty("outputDigest").GetString() != accepted.OutputDigest ||
                    receipt.GetProperty("outcome").GetString() != "Response" || accepted.Attempt.PacketDigest != work.PacketDigest ||
                    !AiExecutionPolicy.ValidDigest(accepted.OutputDigest) || !AiExecutionPolicy.ValidDigest(accepted.AcceptedSnapshotDigest) ||
                    !AiExecutionPolicy.ValidText(accepted.OutputSchemaVersion, 256) || !AiExecutionPolicy.ValidText(accepted.AcceptedSnapshotSchemaVersion, 256))
                    return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
                foreach (var outcome in native.GetProperty("outcomes").EnumerateArray())
                    if (outcome.GetProperty("finding").ValueKind != JsonValueKind.Null && outcome.GetProperty("finding").GetProperty("proposalDigest").GetString() != accepted.AcceptedSnapshotDigest)
                        return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
            }
            var bindings = new List<Phase1BAcceptedSchemaVersionBinding>();
            foreach (var value in p.GetProperty("versionBindings").EnumerateArray())
            {
                string? Text(string name) => value.GetProperty(name).ValueKind == JsonValueKind.Null ? null : value.GetProperty(name).GetString();
                var kind = Enum.Parse<SamplingVersionKind>(value.GetProperty("kind").GetString()!); var state = Enum.Parse<Phase1BPopulationBindingState>(value.GetProperty("state").GetString()!);
                var binding = new Phase1BAcceptedSchemaVersionBinding(kind, state, Text("reference"), Text("valueJson"), Text("knownFactsJson"),
                    value.GetProperty("originPaths").EnumerateArray().Select(x => x.GetString()!), Text("missingReason"));
                if (kind == SamplingVersionKind.AiSchema && proof.Works.Any(w => w.Accepted is not null)) binding = SchemaBinding(proof, binding.KnownFactsJson);
                bindings.Add(binding);
            }
            if (bindings.Count != Enum.GetValues<SamplingVersionKind>().Length || bindings.Select(b => b.Kind).Distinct().Count() != bindings.Count ||
                bindings.Any(b => !Enum.IsDefined(b.Kind) || !Enum.IsDefined(b.State)) ||
                bindings.Where(b => b.Kind != SamplingVersionKind.AiSchema).Any(b => b.State == Phase1BPopulationBindingState.SourceBound && (b.Reference is null || b.ValueJson is null || b.MissingReason is not null) ||
                    b.State == Phase1BPopulationBindingState.Missing && (b.Reference is not null || b.ValueJson is not null || string.IsNullOrEmpty(b.MissingReason))))
                return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch);
            return new(null, new(runId, runRevision, p.GetProperty("scopeId").GetString()!, p.GetProperty("environmentId").GetString()!,
                populationCanonicalJson, populationContentDigest, proof.CanonicalJson, proof.ContentDigest, bindings));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or OverflowException or KeyNotFoundException)
        { return Deny(Phase1BAcceptedSchemaIssue.IntegrityMismatch); }
    }
    internal static Phase1BAcceptedSchemaVersionBinding SchemaBinding(AiAcceptedSchemaProof proof, string? knownFacts)
    {
        using var document = JsonDocument.Parse(proof.CanonicalJson); var root = document.RootElement;
        var nodes = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["Q.$.sourceSnapshotDigest"] = root.GetProperty("sourceSnapshotDigest").Clone() };
        var index = 0;
        foreach (var work in root.GetProperty("works").EnumerateArray())
        {
            var prefix = "Q.$.works[" + index++ + "].";
            foreach (var name in new[] { "workId", "category", "workRevision", "state", "inputSchemaVersion", "packetDigest", "missingReason" }) nodes.Add(prefix + name, work.GetProperty(name).Clone());
            var accepted = work.GetProperty("accepted");
            if (accepted.ValueKind == JsonValueKind.Null) continue;
            foreach (var name in new[] { "receiptId", "outputDigest", "outputSchemaVersion", "acceptedSnapshotDigest", "acceptedSnapshotSchemaVersion" }) nodes.Add(prefix + "accepted." + name, accepted.GetProperty(name).Clone());
            foreach (var name in new[] { "logicalKey", "attemptId", "ordinal", "packetDigest" }) nodes.Add(prefix + "accepted.attempt." + name, accepted.GetProperty("attempt").GetProperty(name).Clone());
        }
        var json = AiExecutionCanonical.Serialize(nodes);
        return new(SamplingVersionKind.AiSchema, Phase1BPopulationBindingState.SourceBound, SchemaReference.Encode(proof.RunLock.Scope, json), json, knownFacts, nodes.Keys, null);
    }
    private static Phase1BAcceptedSchemaIssue Map(AiIssue issue) => issue switch
    {
        AiIssue.InvalidInput => Phase1BAcceptedSchemaIssue.InvalidInput,
        AiIssue.Denied or AiIssue.WrongScope => Phase1BAcceptedSchemaIssue.Denied,
        AiIssue.NotInitialized => Phase1BAcceptedSchemaIssue.NotInitialized,
        AiIssue.NotFound => Phase1BAcceptedSchemaIssue.NotFound,
        AiIssue.MigrationDrift => Phase1BAcceptedSchemaIssue.MigrationDrift,
        AiIssue.InvalidState => Phase1BAcceptedSchemaIssue.SourceUnavailable,
        _ => Phase1BAcceptedSchemaIssue.IntegrityMismatch
    };
}
