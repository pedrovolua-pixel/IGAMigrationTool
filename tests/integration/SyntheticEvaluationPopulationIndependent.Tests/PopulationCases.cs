using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssessmentRuns;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;

internal static partial class Program
{
    private static string Reference(string kind, params string[] parts)
    {
        using var bytes = new MemoryStream();
        bytes.Write(Encoding.ASCII.GetBytes("iga.synthetic-evaluation.native-reference.v1\0"));
        var strict = new UTF8Encoding(false, true);
        void Field(string value)
        {
            var data = strict.GetBytes(value);
            var size = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(size, checked((uint)data.Length));
            bytes.Write(size); bytes.Write(data);
        }
        foreach (var item in new[] { kind, "synthetic-customer", "synthetic-project", "synthetic-environment" }) Field(item);
        var count = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)parts.Length)); bytes.Write(count);
        foreach (var part in parts) Field(part);
        return "synthetic-ref-" + Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }
    private static void LiteralReferences()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference-literals.json")));
        foreach (var vector in doc.RootElement.GetProperty("vectors").EnumerateArray())
            Check(Reference(vector.GetProperty("kind").GetString()!, vector.GetProperty("parts").EnumerateArray().Select(x => x.GetString()!).ToArray()) == vector.GetProperty("reference").GetString(), "independent Python/C# framing parity " + vector.GetProperty("kind").GetString());
    }
    private static void VerifyPopulation(Phase1BPopulationProjection population, SyntheticRunSnapshot run)
    {
        using var source = JsonDocument.Parse(population.SourceCanonicalJson);
        var c = source.RootElement;
        Check(Hash(population.SourceCanonicalJson) == population.SourceCaptureDigest, "old capture bytes/hash retained exactly");
        Check(population.RunId == run.RunId && population.RunRevision == run.Revision, "exact same-run revision identity");
        Check(population.RunPlanJson == JsonSerializer.Serialize(run.Plan) && population.CapabilityLockJson == JsonSerializer.Serialize(run.Plan.CapabilityLock), "owning default plan/capability bytes exact");
        Check(population.FrozenInputsJson == c.GetProperty("frozenInputsJson").GetString(), "exact first capture frozen-input string");
        Check(SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, run.FrozenInputs, run.BaselineCatalogId, run.ProfileCatalogId) == c.GetProperty("inputDigest").GetString(), "public independently recomputed actual input digest");
        Check(population.SourceObservedAtDatabaseUtc == c.GetProperty("observedAtDatabaseUtc").GetDateTimeOffset(), "first native source observation retained");
        Check(population.SourceObservedAtDatabaseUtc.Offset == TimeSpan.Zero && population.RunObservedAtDatabaseUtc.Offset == TimeSpan.Zero, "both source observations are UTC");
        Check(population.RunObservedAtDatabaseUtc >= population.SourceObservedAtDatabaseUtc, "second actual same-transaction observation follows first");
        Check(population.ScopeId == Reference("scope") && population.EnvironmentId == Reference("environment", "synthetic-environment"), "literal scope/environment reference mapping");
        Check(population.Members.Select(x => x.MemberId).SequenceEqual(population.Members.Select(x => x.MemberId).Order(StringComparer.Ordinal)), "member reference ordinal order");
        foreach (var member in population.Members)
        {
            Check(member.NativeGroupId is Expected.ScheduleGroup or Expected.RetryGroup && member.MemberId == Reference("member", member.NativeGroupId), "native root key exact scoped reference");
            Check(member.ScopeId == population.ScopeId && member.EnvironmentId == population.EnvironmentId && member.AffectedObjectCount == 1 && member.Occurrences.Count == 1, "one native group/object member, inherited scope");
            var schedule = member.NativeGroupId == Expected.ScheduleGroup;
            Check(member.Severity == (schedule ? SamplingSeverity.High : SamplingSeverity.Medium) && member.NativeModuleId == "SyntheticOperations" && member.NativeCategoryId == "OPERATIONS", "literal native severity/module/category");
            Check(member.NativeConfidenceBand == "Fictional fixed confidence" && member.NativeConfidencePercent == 80, "literal fixed confidence text/scalar without rebucketing");
            Check(member.PrimaryModuleId == Reference("module", member.NativeModuleId) && member.CategoryId == Reference("category", member.NativeCategoryId), "exact module/category compatibility references");
            Check(member.NativeRuleVersion == "synthetic-rule-v1" && member.NativeRuleId == (schedule ? "fixture-rule-schedule-v1" : "fixture-rule-conflict-v1") && member.RuleVersionId == Reference("rule-version", member.NativeRuleId, member.NativeRuleVersion), "literal rule plus version reference");
            Check(member.ModelPromptVersionId == Reference("model-prompt", "synthetic-fixed-provider-v1", "fixture-prompt-v1") && member.ConfidenceBandId == Reference("confidence-band", member.NativeConfidenceBand), "native provider/prompt/confidence references");
            var occurrence = member.Occurrences.Single();
            Expected.Original(JsonDocument.Parse(occurrence.OriginalJson).RootElement);
            var old = c.GetProperty("members").EnumerateArray().SelectMany(m => m.GetProperty("occurrences").EnumerateArray()).Single(o => o.GetProperty("occurrenceId").GetString() == occurrence.OccurrenceId);
            Check(occurrence.OriginalJson == old.GetProperty("originalJson").GetString() && occurrence.GeneratedFindingJson == old.GetProperty("generatedFindingJson").GetString() && occurrence.OriginalDigest == old.GetProperty("originalDigest").GetString(), "all native old occurrence JSON/digests unchanged");
        }
        Check(population.VersionBindings.Count == 23 && population.VersionBindings.Select(v => v.Kind).SequenceEqual(Enum.GetValues<SamplingVersionKind>()), "all23 version kinds declaration order");
        Check(population.VersionBindings.Count(v => v.State == Phase1BPopulationBindingState.SourceBound) == 13 && population.VersionBindings.Count(v => v.State == Phase1BPopulationBindingState.Missing) == 10, "literal13 SourceBound/10 Missing every saved scenario");
        Check(!population.CompleteSamplingReady && population.MissingVersionKinds.Count == 10, "missing facts deny complete sampling readiness");
        VerifyBindings(population, c);
        using var envelope = JsonDocument.Parse(population.CanonicalJson);
        var e = envelope.RootElement;
        string[] keys = ["schemaVersion", "runId", "runRevision", "sourceCaptureDigest", "sourceCanonicalJson", "runPlanJson", "capabilityLockJson", "frozenInputsJson", "sourceObservedAtDatabaseUtc", "runObservedAtDatabaseUtc", "scopeId", "environmentId", "members", "gaps", "versionBindings", "missingVersionKinds", "completeSamplingReady"];
        Check(e.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal)), "exact closed17-field new envelope");
        Check(Canonical(e) == population.CanonicalJson && Hash(population.CanonicalJson) == population.ContentDigest, "independent canonical property order/UTF8 SHA256");
        Check(e.GetProperty("sourceObservedAtDatabaseUtc").GetString() == population.SourceObservedAtDatabaseUtc.ToString("O", CultureInfo.InvariantCulture) && e.GetProperty("runObservedAtDatabaseUtc").GetString() == population.RunObservedAtDatabaseUtc.ToString("O", CultureInfo.InvariantCulture), "both UTC observation O byte format fixed7fractional digits");
        Check(e.GetProperty("sourceCanonicalJson").GetString() == population.SourceCanonicalJson && e.GetProperty("runPlanJson").GetString() == population.RunPlanJson, "all raw source/plan bytes bound in new envelope");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "fr-FR", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                Check(Canonical(e) == population.CanonicalJson && Reference("scope") == population.ScopeId, "independent canonical/reference culture parity " + name);
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }
    private static void VerifyBindings(Phase1BPopulationProjection population, JsonElement capture)
    {
        using var frozen = JsonDocument.Parse(capture.GetProperty("frozenInputsJson").GetString()!);
        using var locked = JsonDocument.Parse(capture.GetProperty("aiRunLockJson").GetString()!);
        using var ai = JsonDocument.Parse(capture.GetProperty("aiSnapshotJson").GetString()!);
        using var plan = JsonDocument.Parse(population.RunPlanJson);
        var roots = new Dictionary<string, List<(string Pointer, JsonElement Node)>>
        {
            ["C"] = [("C", capture)],
            ["F"] = [("F", frozen.RootElement)],
            ["L"] = [("L", locked.RootElement)],
            ["A"] = [("A", ai.RootElement)],
            ["P"] = [("P", plan.RootElement)],
            ["I"] = [],
            ["O"] = [],
            ["G"] = []
        };
        var decoded = new List<JsonDocument>();
        try
        {
            var workIndex = 0;
            foreach (var work in ai.RootElement.GetProperty("works").EnumerateArray())
            {
                var doc = JsonDocument.Parse(work.GetProperty("work").GetProperty("packetInputJson").GetString()!); decoded.Add(doc);
                roots["I"].Add(($"I[{workIndex++}]", doc.RootElement));
            }
            var memberIndex = 0;
            foreach (var member in capture.GetProperty("members").EnumerateArray())
            {
                var occurrenceIndex = 0;
                foreach (var occurrence in member.GetProperty("occurrences").EnumerateArray())
                {
                    foreach (var (alias, field) in new[] { ("O", "originalJson"), ("G", "generatedFindingJson") })
                    {
                        var doc = JsonDocument.Parse(occurrence.GetProperty(field).GetString()!); decoded.Add(doc);
                        roots[alias].Add(($"{alias}[{memberIndex},{occurrenceIndex}]", doc.RootElement));
                    }
                    occurrenceIndex++;
                }
                memberIndex++;
            }
            using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "inventory.json")));
            var missing = new List<SamplingVersionKind>();
            foreach (var kind in inventory.RootElement.GetProperty("kinds").EnumerateArray())
            {
                var name = kind.GetProperty("kind").GetString()!;
                var binding = population.VersionBindings.Single(x => x.Kind.ToString() == name);
                var state = kind.GetProperty("status").GetString()!;
                var required = Bundle(roots, kind.GetProperty("sourcePaths"), false);
                var known = Bundle(roots, kind.GetProperty("knownFactPaths"), true);
                var origins = required.Keys.Concat(known.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                Check(binding.OriginPaths.SequenceEqual(origins), "exact independent concrete owner origin paths " + name);
                Check(binding.KnownFactsJson == (known.Count == 0 ? null : Canonical(JsonSerializer.SerializeToElement(known))), "exact independent known partial facts " + name);
                if (state == "SourceBound")
                {
                    var value = Canonical(JsonSerializer.SerializeToElement(required));
                    Check(binding.State == Phase1BPopulationBindingState.SourceBound && binding.ValueJson == value && binding.Reference == Reference("version-binding", name, value) && binding.MissingReason is null, "exact required bundle/reference from independent owner traversal " + name);
                }
                else
                {
                    missing.Add(binding.Kind);
                    Check(binding.State == Phase1BPopulationBindingState.Missing && binding.Reference is null && binding.ValueJson is null && binding.MissingReason == kind.GetProperty("missingReason").GetString(), "exact Missing null required reference/value/reason " + name);
                }
            }
            Check(population.MissingVersionKinds.SequenceEqual(missing), "exact missing-kind list follows declaration order");
            Check(population.VersionBindings.Single(x => x.Kind == SamplingVersionKind.AiSchema).MissingReason == "AcceptedOutputSchemaNotSupplied", "schema never inferred from private accepted snapshot or provider constants");
        }
        finally { foreach (var document in decoded) document.Dispose(); }
    }
    private static Dictionary<string, JsonElement> Bundle(Dictionary<string, List<(string Pointer, JsonElement Node)>> roots, JsonElement paths, bool optional)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var sourcePath in paths.EnumerateArray())
        {
            var source = sourcePath.GetString()!;
            var split = source.IndexOf(".$.", StringComparison.Ordinal);
            var alias = source[..split]; var path = source[(split + 3)..];
            var brace = Regex.Match(path, "^(.*)\\{([^{}]+)\\}(.*)$");
            var expanded = brace.Success ? brace.Groups[2].Value.Split(',').Select(field => brace.Groups[1].Value + field + brace.Groups[3].Value) : [path];
            foreach (var fieldPath in expanded)
                foreach (var root in roots[alias])
                    Walk(root.Node, fieldPath.Split('.'), 0, root.Pointer + ".$", optional, result);
        }
        return result;
    }
    private static void Walk(JsonElement node, string[] path, int next, string pointer, bool optional, Dictionary<string, JsonElement> values)
    {
        if (next == path.Length)
        {
            if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            { if (!optional) throw new InvalidOperationException("independent required source leaf missing " + pointer); return; }
            values.Add(pointer, node);
            return;
        }
        var segment = path[next];
        var array = segment.EndsWith("[*]", StringComparison.Ordinal);
        var field = array ? segment[..^3] : segment;
        if (!node.TryGetProperty(field, out var value))
        { if (optional) return; throw new InvalidOperationException("independent required source path missing " + pointer + "." + field); }
        if (!array) { Walk(value, path, next + 1, pointer + "." + field, optional, values); return; }
        var index = 0;
        foreach (var item in value.EnumerateArray()) Walk(item, path, next + 1, pointer + "." + field + "[" + index++ + "]", optional, values);
    }
}
