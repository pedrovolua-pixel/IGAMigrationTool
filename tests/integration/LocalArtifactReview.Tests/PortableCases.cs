using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentOrchestration;
using AssessmentRuns;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;

internal static class PortableCases
{
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
    internal static GuidanceInput Input(string name) => JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(Fixture(name + "-input.json")), V13Program.Web)!;
    internal static FixPackageSnapshot Package(string name)
    {
        var guidance = RecommendationGuidanceBuilder.Build(Input(name));
        Check.That(guidance.Succeeded, "real-approved-new-profile-guidance");
        var result = FixPackageBuilder.Build(guidance.Snapshot);
        Check.That(result.Succeeded, "real-guidance-to-original-package");
        return result.Snapshot!;
    }
    internal static ArtifactReviewSource Source(string name)
    {
        var result = ArtifactReviewSourceBuilder.Build(Package(name));
        Check.That(result.Source is not null && result.Issue is null, "real-package-to-source-no-forged-acceptance");
        return result.Source!;
    }
    private static FixPackageSnapshot Forged(FixPackageSnapshot original, string property, object? value)
    {
        // Reflection is confined to rejection tests. Every accepted input comes from real builders.
        var clone = (FixPackageSnapshot)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(original, null)!;
        typeof(FixPackageSnapshot).GetField("<" + property + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clone, value);
        return clone;
    }
    internal static void Run()
    {
        Check.Equal(Expected.Hash(Expected.Canonical(new JsonObject { ["templateVersion"] = Expected.TemplateVersion, ["templates"] = Expected.TemplateNodes() })), Expected.TemplateDigest, "independent-three-exact-template-registry");
        foreach (var name in new[] { "normal", "hostile", "empty", "source-b", "source-return", "run-later" })
        {
            var original = Package(name);
            var frozen = original.CanonicalJson;
            Check.Bytes(RecommendationGuidanceBuilder.CanonicalPayload(original.Guidance), File.ReadAllBytes(Fixture(name + "-guidance-golden.json")), "complete-independent-guidance-literal-bytes");
            Check.Bytes(Encoding.UTF8.GetBytes(frozen), File.ReadAllBytes(Fixture(name + "-fix-golden.json")), "complete-independent-original-package-literal-bytes");
            var expected = JsonNode.Parse(File.ReadAllText(Fixture(name + "-fix-golden.json")))!.AsObject();
            Check.Equal(Expected.Canonical(Expected.FixPayload(expected["guidance"]!.AsObject())), frozen, "independent-package-identity-full-payload-recipe");
            Check.Equal(original.ContentDigest, Expected.Hash(frozen), "independent-original-content-digest");
            var built = ArtifactReviewSourceBuilder.Build(original);
            Check.That(built.Issue is null && built.Source is not null, "actual-source-builder-accepts-real-composition");
            Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(built.Source!.Binding, V13Program.Web)), File.ReadAllText(Fixture(name + "-binding-golden.json")), "full-independent-source-binding-literal");
            Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(built.Source.Artifacts, V13Program.Web)), File.ReadAllText(Fixture(name + "-artifacts-golden.json")), "full-independent-artifact-binding-literal");
            Check.Equal(Expected.Canonical(Expected.Binding(expected)), File.ReadAllText(Fixture(name + "-binding-golden.json")), "independent-source-recipe-vs-frozen-literal");
            Check.Equal(Expected.Canonical(Expected.Artifacts(expected)), File.ReadAllText(Fixture(name + "-artifacts-golden.json")), "independent-all-kind-text-id-digests");
            Check.Equal(original.CanonicalJson, frozen, "original-remains-byte-invariant-after-source-build");
            Check.That(original.Packages.SelectMany(p => p.Options).SelectMany(o => o.Artifacts).All(a => a.Status == "Unverified"), "generated-originals-always-unverified");
            var input = Input(name);
            var reversed = input with { Findings = input.Findings.Reverse().Select(f => f with { Occurrences = f.Occurrences.Reverse().ToImmutableArray(), Options = f.Options.Reverse().ToImmutableArray() }).ToImmutableArray() };
            var permutation = FixPackageBuilder.Build(RecommendationGuidanceBuilder.Build(reversed).Snapshot).Snapshot;
            var permuted = ArtifactReviewSourceBuilder.Build(permutation);
            Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(permuted.Source!.Artifacts, V13Program.Web)), File.ReadAllText(Fixture(name + "-artifacts-golden.json")), "source-order-independent-from-input-permutation");
        }
        var normal = Package("normal");
        void Deny(FixPackageSnapshot? value)
        {
            var result = ArtifactReviewSourceBuilder.Build(value);
            Check.That(result.Source is null && result.Issue is not null, "malformed-forged-source-no-partial-acceptance");
        }
        Deny(null);
        foreach (var pair in new (string Property, object? Value)[]
        {
            ("SchemaVersion", "foreign"), ("Status", "ReviewedForPlanning"), ("Disclaimer", ""),
            ("TemplateVersion", "foreign"), ("TemplateDigest", new string('0',64)),
            ("CanonicalJson", "{}"), ("ContentDigest", new string('0',64)),
            ("Warnings", ImmutableArray<string>.Empty), ("UnavailableSections", ImmutableArray<string>.Empty),
            ("Guidance", normal.Guidance with { ContentDigest = new string('0',64) }),
            ("Guidance", normal.Guidance with { Source = normal.Guidance.Source with { RunId = Guid.Empty } }),
            ("Guidance", normal.Guidance with { Findings = normal.Guidance.Findings.Select(f => f with { FindingRevision = 0 }).ToImmutableArray() }),
            ("Packages", normal.Packages.Select(p => p with { PackageId = new string('0',64) }).ToImmutableArray()),
            ("Packages", normal.Packages.Select(p => p with { Options = p.Options.Select(o => o with { Artifacts = o.Artifacts.Select(a => a with { Text = "changed" }).ToImmutableArray() }).ToImmutableArray() }).ToImmutableArray()),
            ("Templates", normal.Templates.Select(t => t with { Text = "changed" }).ToImmutableArray())
        }) Deny(Forged(normal, pair.Property, pair.Value));
        foreach (var name in new[] { "normal", "hostile", "empty" })
        {
            var historical = JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(Fixture("historical-" + name + "-input.json")), V13Program.Web)!;
            var old = FixPackageBuilder.Build(RecommendationGuidanceBuilder.Build(historical).Snapshot);
            Check.That(old.Succeeded, "historical-fix-profile-real-build-retained");
            Check.Equal(old.Snapshot!.CanonicalJson, File.ReadAllText(Fixture("historical-" + name + "-fix-golden.json")), "historical-original-full-golden-not-regenerated");
            Deny(old.Snapshot);
        }
        foreach (var edit in new Action<JsonObject>[]
        {
            value => value.Remove("fixReviewContractDigest"), value => value["fixReviewContractDigest"] = new string('0',64),
            value => value["applicationVersion"] = "synthetic-fix-packages-app-v1", value => value["unexpected"] = true
        })
        {
            var input = Input("normal"); var versions = JsonNode.Parse(input.Source.FrozenVersions.GetRawText())!.AsObject(); edit(versions);
            var bad = input with { Source = input.Source with { FrozenVersions = JsonSerializer.SerializeToElement(versions) } };
            var guidance = RecommendationGuidanceBuilder.Build(bad);
            var package = FixPackageBuilder.Build(guidance.Snapshot);
            Deny(package.Snapshot);
        }
        using var history = JsonDocument.Parse(File.ReadAllText(Fixture("historical-historical-six-locks.json")));
        foreach (var entry in history.RootElement.GetProperty("entries").EnumerateArray())
        {
            var baseline = entry.GetProperty("baselineId").GetString()!; var profile = entry.GetProperty("profileId").GetString()!;
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, "v13-independent-historical");
            var versions = JsonSerializer.Serialize(request.Versions);
            Check.Equal(versions, entry.GetProperty("versionsJson").GetString(), "historical-six-exact-input-bytes");
            var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
            Check.Equal(Expected.InputDigest(JsonSerializer.Serialize(plan), versions, baseline, profile), entry.GetProperty("inputDigest").GetString(), "historical-six-exact-input-digests");
        }
        Check.Equal(DemoFixtureCatalog.Profiles.Count, 10, "exact-nine-historical-plus-one-opt-in");
        Check.Equal(DemoFixtureCatalog.Baselines.Count, 9, "no-new-baseline-authority");
        foreach (var baseline in Expected.Presets)
        {
            var added = DemoFixtureCatalog.CreateStartRequest(baseline, Expected.Profile, "v13-new");
            var old = DemoFixtureCatalog.CreateStartRequest(baseline, "synthetic-review-maturity-fix-packages-equal-v1", "v13-old");
            var versions = JsonSerializer.SerializeToNode(added.Versions)!.AsObject();
            Check.Equal(versions["FixReviewContractDigest"]!.GetValue<string>(), Expected.ContractDigest, "exact-contract-input-lock");
            versions.Remove("FixReviewContractDigest"); versions["ApplicationVersion"] = "synthetic-fix-packages-app-v1";
            Check.Equal(Expected.Canonical(versions), Expected.Canonical(JsonSerializer.SerializeToNode(old.Versions)), "only-approved-profile-app-and-contract-input-delta");
        }
        foreach (var old in DemoFixtureCatalog.Profiles.Where(p => p.Id != Expected.Profile))
            Check.That(!JsonSerializer.Serialize(old.Versions).Contains("FixReviewContractDigest", StringComparison.Ordinal), "historical-nine-new-lock-omitted");
        Check.Group("AR13-T01/T03/T08 portable real composition independent literals and original invariants");
    }
}
