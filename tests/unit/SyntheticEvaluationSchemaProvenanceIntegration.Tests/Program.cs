using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;
using SyntheticEvaluationSchemaProvenanceIntegration;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; Console.WriteLine("PASS " + label); }
    private static int Main()
    {
        try { Execute(); return 0; } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static AiAcceptedSchemaProof Fixture(string json)
    {
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var locked = JsonSerializer.Deserialize<AiRunLock>(root.GetProperty("runLock"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var works = new List<AiAcceptedSchemaWork>();
        foreach (var w in root.GetProperty("works").EnumerateArray())
        {
            var accepted = w.GetProperty("accepted"); AiAcceptedSchemaRecord? record = null;
            if (accepted.ValueKind != JsonValueKind.Null)
            {
                var key = JsonSerializer.Deserialize<AiAttemptKey>(accepted.GetProperty("attempt"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                record = new(key, accepted.GetProperty("receiptId").GetString()!, accepted.GetProperty("outputDigest").GetString()!, accepted.GetProperty("outputSchemaVersion").GetString()!,
                    accepted.GetProperty("acceptedSnapshotDigest").GetString()!, accepted.GetProperty("acceptedSnapshotSchemaVersion").GetString()!);
            }
            works.Add(new(w.GetProperty("workId").GetString()!, w.GetProperty("category").GetString()!, w.GetProperty("workRevision").GetInt64(), Enum.Parse<AiWorkState>(w.GetProperty("state").GetString()!),
                w.GetProperty("inputSchemaVersion").GetString()!, w.GetProperty("packetDigest").GetString()!, record, w.GetProperty("missingReason").ValueKind == JsonValueKind.Null ? null : w.GetProperty("missingReason").GetString()));
        }
        return new(locked, root.GetProperty("sourceSnapshotDigest").GetString()!, works);
    }
    private static void Execute()
    {
        var proofLiteral = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "normal-proof-canonical-preimplementation.json"));
        var readinessLiteral = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "normal-readiness-canonical-representative-preimplementation.json"));
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "canonical-alias-literals-preimplementation.json")));
        using var ready = JsonDocument.Parse(readinessLiteral); var oldJson = ready.RootElement.GetProperty("populationCanonicalJson").GetString()!;
        var oldDigest = ready.RootElement.GetProperty("populationContentDigest").GetString()!;
        var proof = Fixture(proofLiteral);
        Check(proof.CanonicalJson == proofLiteral && proof.ContentDigest == "5037eb5895a42ab3491b3d403ece0123810b0580b6ee7a0051ae2ea82f36c3bd", "independent full proof literal/hash");
        var result = Phase1BAcceptedSchemaSourceAdapter.Compose(oldJson, oldDigest, proof);
        Check(result.HasReadiness && result.Readiness is not null, "independent representative actual saved source composes");
        var projection = result.Readiness!;
        Check(projection.CanonicalJson == readinessLiteral && projection.ContentDigest == expected.RootElement.GetProperty("readinessDigest").GetString(), "independent full129579-byte readiness literal/hash");
        var binding = projection.VersionBindings.Single(b => b.Kind == SamplingVersionKind.AiSchema);
        Check(binding.ValueJson == expected.RootElement.GetProperty("aiSchemaValueJson").GetString() && binding.Reference == "synthetic-ref-3d4175b218ea076c0e7aebec38530165e774f5c15955fa7565ba9348ee1550f6", "independent17-key bundle/reference literal");
        Check(binding.OriginPaths.SequenceEqual(expected.RootElement.GetProperty("originPaths").EnumerateArray().Select(p => p.GetString()!)), "exact concrete Q paths");
        Check(projection.VersionBindings.Count == 23 && projection.MissingVersionKinds.Count == 9 && !projection.CompleteSamplingReady, "fourteen bound/nine missing never sampling-ready");
        Check(projection.PopulationCanonicalJson == oldJson && projection.PopulationContentDigest == oldDigest, "embedded old population strings untouched");
        using var old = JsonDocument.Parse(oldJson);
        foreach (var oldBinding in old.RootElement.GetProperty("versionBindings").EnumerateArray())
        {
            var kind = Enum.Parse<SamplingVersionKind>(oldBinding.GetProperty("kind").GetString()!);
            var current = projection.VersionBindings.Single(b => b.Kind == kind);
            if (kind != SamplingVersionKind.AiSchema) Check(AiExecutionCanonical.Serialize(current) == oldBinding.GetRawText(), "old exact binding preserved " + kind);
            else Check(current.KnownFactsJson == oldBinding.GetProperty("knownFactsJson").GetString(), "AiSchema old partial known facts retained");
        }
        using (var fields = JsonDocument.Parse(projection.CanonicalJson))
            Check(fields.RootElement.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "completeSamplingReady", "environmentId", "missingVersionKinds", "populationCanonicalJson", "populationContentDigest", "runId", "runRevision", "schemaProofCanonicalJson", "schemaProofContentDigest", "schemaVersion", "scopeId", "versionBindings" }), "exact new envelope fields/no self digest");
        foreach (var culture in new[] { "en-US", "fr-FR", "tr-TR", "ar-EG" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Check(Fixture(proofLiteral).CanonicalJson == proofLiteral && Phase1BAcceptedSchemaSourceAdapter.Compose(oldJson, oldDigest, proof).Readiness!.CanonicalJson == readinessLiteral, "culture-independent literal " + culture);
        }
        foreach (var field in new[] { "sourceSnapshotDigest", "runLock", "workRevision", "attemptId", "receiptId", "packetDigest", "accepted" })
        {
            var changed = JsonNode.Parse(proofLiteral)!.AsObject(); var work = changed["works"]![0]!.AsObject();
            switch (field)
            {
                case "sourceSnapshotDigest": changed[field] = new string('a', 64); break;
                case "runLock": changed["runLock"]!["runId"] = Guid.NewGuid().ToString("D"); break;
                case "workRevision": work[field] = work[field]!.GetValue<long>() + 1; break;
                case "accepted": work[field] = null; break;
                case "attemptId": work["accepted"]!["attempt"]![field] = Guid.NewGuid().ToString("D"); break;
                case "receiptId": work["accepted"]![field] = "foreign-receipt"; break;
                default: work[field] = new string('a', 64); break;
            }
            var altered = Fixture(changed.ToJsonString());
            Check(altered.ContentDigest != proof.ContentDigest && Phase1BAcceptedSchemaSourceAdapter.Compose(oldJson, oldDigest, altered).Issue == Phase1BAcceptedSchemaIssue.IntegrityMismatch, "proof identity/provenance tamper closed " + field);
        }
        var damaged = JsonNode.Parse(oldJson)!.AsObject(); damaged["runRevision"] = 99999;
        var damagedJson = AiExecutionCanonical.Serialize(damaged);
        Check(Phase1BAcceptedSchemaSourceAdapter.Compose(damagedJson, AiExecutionCanonical.Hash(damagedJson), proof).Issue == Phase1BAcceptedSchemaIssue.IntegrityMismatch, "assessment revision cannot borrow work revision");
        Check(Phase1BAcceptedSchemaSourceAdapter.Compose(oldJson, new string('a', 64), proof).Issue == Phase1BAcceptedSchemaIssue.IntegrityMismatch, "whole old population digest checked");
        foreach (var field in new[] { "scopeId", "environmentId", "referenceConflict" })
        {
            var changed = JsonNode.Parse(oldJson)!.AsObject();
            if (field == "referenceConflict") changed["versionBindings"]![1]!["reference"] = changed["versionBindings"]![0]!["reference"]!.GetValue<string>();
            else changed[field] = "synthetic-ref-" + new string('a', 64);
            var json = AiExecutionCanonical.Serialize(changed);
            Check(Phase1BAcceptedSchemaSourceAdapter.Compose(json, AiExecutionCanonical.Hash(json), proof).Issue == Phase1BAcceptedSchemaIssue.IntegrityMismatch, "scope/reference collision counterexample closed " + field);
        }
        var list = new List<AiAcceptedSchemaWork>(proof.Works); var detached = new AiAcceptedSchemaProof(proof.RunLock, proof.SourceSnapshotDigest, list); list.Clear();
        Check(detached.Works.Count == 1 && detached.CanonicalJson == proofLiteral, "proof collections detached from construction inputs");
        Check(typeof(AiAcceptedSchemaProof).GetConstructors().Length == 0 && typeof(Phase1BAcceptedSchemaReadinessProjection).GetConstructors().Length == 0 &&
            typeof(Phase1BAcceptedSchemaSourceAdapter).GetMethods(BindingFlags.Public | BindingFlags.Static).Length == 0, "no public arbitrary snapshot/proof factories");
        Check(new Phase1BAcceptedSchemaResult(Phase1BAcceptedSchemaIssue.Denied, null).HasReadiness == false, "typed denial has no content");
        Console.WriteLine($"PASS {checks} accepted-schema portable assertions; independent preimplementation literals.");
    }
}
