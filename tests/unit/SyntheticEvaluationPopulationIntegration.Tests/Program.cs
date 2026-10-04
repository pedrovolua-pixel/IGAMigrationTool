using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;
using SyntheticEvaluationSourceIntegration;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
    private static int Main()
    {
        try { Execute(); Console.WriteLine($"PASS {checks} population portable assertions; no database/provider."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Execute()
    {
        var scope = AiScope.Fixed;
        Check(PopulationReference.Encode(scope, "scope") == "synthetic-ref-8daf6b0955696c8fcb844db1ef5c5c58955811855ad3c3884172657689aa2388", "independent scope reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "scope")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000573636f70650000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e7400000000", "independent scope byte framing");
        Check(PopulationReference.Encode(scope, "environment", "synthetic-environment") == "synthetic-ref-85402e72ec00d086ffd2df172bf6c3c4371fd44ef4e077e05dd8c8d4bec76328", "independent environment reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "environment", "synthetic-environment")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000b656e7669726f6e6d656e740000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000010000001573796e7468657469632d656e7669726f6e6d656e74", "independent environment byte framing");
        Check(PopulationReference.Encode(scope, "member", "e3003bf710a58e5af23e1a1af61f3af16dd9496bbc7ef146b4c88f734428100d") == "synthetic-ref-3f190fdd20b7688328fc199566dfea06e87998cb6a9baf78236f7d073c24297e", "independent member reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "member", "e3003bf710a58e5af23e1a1af61f3af16dd9496bbc7ef146b4c88f734428100d")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e763100000000066d656d6265720000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000010000004065333030336266373130613538653561663233653161316166363166336166313664643934393662626337656631343662346338386637333434323831303064", "independent member byte framing");
        Check(PopulationReference.Encode(scope, "module", "SyntheticOperations") == "synthetic-ref-54336c6e98655b24aaa114de36f78eec3f0795ca9e3631c90a637e27249e5e0d", "independent module reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "module", "SyntheticOperations")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e763100000000066d6f64756c650000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000010000001353796e7468657469634f7065726174696f6e73", "independent module byte framing");
        Check(PopulationReference.Encode(scope, "category", "OPERATIONS") == "synthetic-ref-daab2d721879873322fd815321cc0904b2ccec2cc95d2312ffe2b20a591dddf5", "independent category reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "category", "OPERATIONS")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000863617465676f72790000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000010000000a4f5045524154494f4e53", "independent category byte framing");
        Check(PopulationReference.Encode(scope, "rule-version", "fixture-rule-schedule-v1", "synthetic-rule-v1") == "synthetic-ref-14f432f915553dd6845927e0e8fc75ccf2a52ef9496fdd0b6da1030c096a0ccd", "independent rule-version reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "rule-version", "fixture-rule-schedule-v1", "synthetic-rule-v1")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000c72756c652d76657273696f6e0000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e740000000200000018666978747572652d72756c652d7363686564756c652d76310000001173796e7468657469632d72756c652d7631", "independent rule-version byte framing");
        Check(PopulationReference.Encode(scope, "model-prompt", "synthetic-fixed-provider-v1", "fixture-prompt-v1") == "synthetic-ref-f783ae7e1b4f8788386dc7ee79bebfbc2e6e34225f5d12aa1ea0eeb4a1887364", "independent model-prompt reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "model-prompt", "synthetic-fixed-provider-v1", "fixture-prompt-v1")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000c6d6f64656c2d70726f6d70740000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000020000001b73796e7468657469632d66697865642d70726f76696465722d763100000011666978747572652d70726f6d70742d7631", "independent model-prompt byte framing");
        Check(PopulationReference.Encode(scope, "confidence-band", "Fictional fixed confidence") == "synthetic-ref-1ddedd67695857b7dbaed38cfb46248132af2fdc6d0595790bea752d24c69016", "independent confidence-band reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "confidence-band", "Fictional fixed confidence")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000f636f6e666964656e63652d62616e640000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000010000001a46696374696f6e616c20666978656420636f6e666964656e6365", "independent confidence-band byte framing");
        Check(PopulationReference.Encode(scope, "version-binding", "AiProvider", "{\"L.$.providerVersion\":\"synthetic-fixed-provider-v1\"}") == "synthetic-ref-b4f756794ed31c6ad819e21c8027ebaf2c7086afeacbb6249cb5853935581d3f", "independent version-binding reference literal");
        Check(Convert.ToHexStringLower(PopulationReference.Frame(scope, "version-binding", "AiProvider", "{\"L.$.providerVersion\":\"synthetic-fixed-provider-v1\"}")) == "6967612e73796e7468657469632d6576616c756174696f6e2e6e61746976652d7265666572656e63652e7631000000000f76657273696f6e2d62696e64696e670000001273796e7468657469632d637573746f6d65720000001173796e7468657469632d70726f6a6563740000001573796e7468657469632d656e7669726f6e6d656e74000000020000000a416950726f7669646572000000357b224c2e242e70726f766964657256657273696f6e223a2273796e7468657469632d66697865642d70726f76696465722d7631227d", "independent version-binding byte framing");
        var categories = new[] { "OPERATIONS", "operations", " OPERATIONS", "OPERATIONS ", "é", "e\u0301", "Ω", "<script>", "", "\0" };
        Check(categories.Select(x => PopulationReference.Encode(scope, "category", x)).Distinct().Count() == categories.Length, "exact case/space/Unicode without collapse");
        foreach (var value in categories)
        {
            var reference = PopulationReference.Encode(scope, "category", value);
            Check(reference.Length == 78 && reference.StartsWith("synthetic-ref-", StringComparison.Ordinal) && reference[14..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "restricted syntax");
            Check(reference != PopulationReference.Encode(scope with { CustomerId = "other-customer" }, "category", value), "scope binds aliases");
        }
        Check(PopulationReference.Encode(scope, "rule-version", "ab", "c") != PopulationReference.Encode(scope, "rule-version", "a", "bc"), "length framing avoids concatenation alias");
        Check(PopulationReference.Encode(scope, "rule-version", "a", "b") != PopulationReference.Encode(scope, "rule-version", "b", "a"), "native part order binds alias");
        foreach (var action in new Action[] { () => PopulationReference.Encode(scope, "category", "\uD800"), () => PopulationReference.Encode(scope, "unknown", "a"), () => PopulationReference.Encode(scope, "scope", "a"), () => PopulationReference.Encode(scope, "rule-version", "a") })
        {
            try { action(); throw new InvalidOperationException("Malformed reference accepted."); }
            catch (ArgumentException) { Check(true, "malformed UTF16/kind/cardinality rejects"); }
        }
        var references = new PopulationReferences(scope);
        Check(references.Add("category", "OPERATIONS") == references.Add("category", "OPERATIONS"), "consistent repeated aliases retained");
        const string literal = "{\"a\":\"\\u003C\\u0026\\u00E9\\n\",\"decimal\":80,\"enum\":\"High\",\"z\":null}";
        var canonical = PopulationCanonical.Json(new { z = (string?)null, @enum = SamplingSeverity.High, @decimal = 80.00m, a = "<&é\n" });
        Check(canonical == literal && PopulationCanonical.Hash(canonical) == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(literal))), "literal canonical escaping/null/decimal/named-enum/order/hash");
        Check(!canonical.EndsWith('\n') && !canonical.StartsWith('\uFEFF'), "compact no BOM/newline");
        var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Check(PopulationCanonical.Json(new { n = 12.3400m }) == "{\"n\":12.34}", "invariant G29 decimal"); }
        finally { CultureInfo.CurrentCulture = culture; }
        Check(PopulationCanonical.Date(DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(3))) == "1970-01-01T00:00:00.0000000+00:00", "exact UTC date seven fractions");
        var paths = new List<string> { "Z.$.z", "A.$.a" };
        var binding = new Phase1BPopulationVersionBinding(SamplingVersionKind.AiSchema, Phase1BPopulationBindingState.Missing, null, null, "{\"I[0].$.schemaVersion\":\"input\"}", paths, "AcceptedOutputSchemaNotSupplied");
        paths.Clear(); Check(binding.OriginPaths.SequenceEqual(new[] { "A.$.a", "Z.$.z" }), "binding paths copied and sorted");
        var occurrences = new List<Phase1BEvaluationSourceOccurrence>();
        var member = new Phase1BPopulationMember("synthetic-member", "scope", "environment", "module", "category", "rule", "model", "band", SamplingSeverity.High,
            "native-group", "NativeModule", "NATIVE_CATEGORY", "native-rule", "native-rule-version", "Native band é", 80.00m, 1, occurrences);
        var members = new List<Phase1BPopulationMember> { member };
        var bindings = new List<Phase1BPopulationVersionBinding> { binding };
        var gaps = new List<Phase1BEvaluationSourceGap>();
        var projection = new Phase1BPopulationProjection(Guid.Parse("11111111-2222-3333-4444-555555555555"), 3, "digest", "raw < source", "raw plan", "raw capability", "raw frozen", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddTicks(1), "scope", "environment", members, gaps, bindings);
        members.Clear(); bindings.Clear(); gaps.Clear();
        Check(projection.Members.Count == 1 && projection.VersionBindings.Count == 1 && !projection.CompleteSamplingReady, "projection detached and incomplete");
        foreach (var collection in new object[] { member.Occurrences, binding.OriginPaths, projection.Members, projection.Gaps, projection.VersionBindings, projection.MissingVersionKinds })
        {
            Check(((IList)collection).IsReadOnly, "read-only detached view");
            try { ((IList)collection).Clear(); throw new InvalidOperationException("Mutable collection."); }
            catch (NotSupportedException) { Check(true, "mutation refused"); }
        }
        foreach (var type in new[] { typeof(Phase1BPopulationProjection), typeof(Phase1BPopulationMember), typeof(Phase1BPopulationVersionBinding) })
        {
            Check(type.IsSealed && type.GetConstructors().Length == 0, "closed get-only type has no unchecked public constructor");
            foreach (var property in type.GetProperties()) Check(property.SetMethod is null, "get-only property " + property.Name);
        }
        using var envelope = JsonDocument.Parse(projection.CanonicalJson);
        Check(envelope.RootElement.EnumerateObject().Select(x => x.Name).SequenceEqual(new[] { "capabilityLockJson", "completeSamplingReady", "environmentId", "frozenInputsJson", "gaps", "members", "missingVersionKinds", "runId", "runObservedAtDatabaseUtc", "runPlanJson", "runRevision", "schemaVersion", "scopeId", "sourceCanonicalJson", "sourceCaptureDigest", "sourceObservedAtDatabaseUtc", "versionBindings" }), "literal top-level canonical property set/order");
        Check(envelope.RootElement.GetProperty("sourceCanonicalJson").GetString() == "raw < source", "raw owner payload lossless");
        Check(envelope.RootElement.GetProperty("versionBindings")[0].GetProperty("valueJson").ValueKind == JsonValueKind.Null, "explicit Missing null");
        Check(projection.ContentDigest == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(projection.CanonicalJson))), "independent exact envelope hash");
        var golden = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "full-envelope-literal.json"));
        Check(Encoding.UTF8.GetBytes(projection.CanonicalJson).SequenceEqual(golden), "independent full envelope 1327-byte golden");
        Check(projection.ContentDigest == "d7d4c9800da435d069d19374aa7f21aa3758687fec2cf0cbe14cb8798b68ea32", "independent full envelope SHA literal");
        var changed = new Phase1BPopulationProjection(projection.RunId, 3, "digest", "raw < source", "raw plan", "raw capability", "raw frozen", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddTicks(2), "scope", "environment", projection.Members, projection.Gaps, projection.VersionBindings);
        Check(changed.ContentDigest != projection.ContentDigest, "supplementary observation bound in hash");
        foreach (var definition in PopulationInventory.Definitions)
        {
            Check(definition.MissingReason is null ? definition.RequiredPaths.Length > 0 : definition.RequiredPaths.Length == 0, "binding complete vs missing rule " + definition.Kind);
        }
        Check(PopulationInventory.Definitions.Select(d => d.Kind).SequenceEqual(Enum.GetValues<SamplingVersionKind>()) && PopulationInventory.Definitions.Count(d => d.MissingReason is null) == 13, "exact 23 order/13 required source definitions");
        foreach (var native in new[] { "line\n", "é", "Ω", "<script>&" })
        {
            using var node = JsonDocument.Parse(PopulationCanonical.Json(new { native }));
            Check(node.RootElement.GetProperty("native").GetString() == native, "native value retained through encoder");
        }
    }
}
