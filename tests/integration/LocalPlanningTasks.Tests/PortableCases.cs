using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;
using SyntheticFixPackages;

internal static class PortableCases
{
    internal static GuidanceInput Input(string name) => JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(OracleCases.Fixture(name + "-input.json")), V14Program.Web)!;
    internal static FixPackageSnapshot Package(string name)
    {
        var guidance = RecommendationGuidanceBuilder.Build(Input(name));
        Check.That(guidance.Succeeded, "actual-frozen-profile-guidance-builder-no-reflection-accepted-path");
        var package = FixPackageBuilder.Build(guidance.Snapshot);
        Check.That(package.Succeeded, "actual-guidance-to-original-package-builder");
        return package.Snapshot!;
    }
    internal static void Run()
    {
        foreach (var name in new[] { "normal", "hostile", "empty", "source-b", "source-return", "run-later" })
        {
            var package = Package(name);
            var before = package.CanonicalJson;
            Check.Bytes(RecommendationGuidanceBuilder.CanonicalPayload(package.Guidance), File.ReadAllBytes(OracleCases.Fixture(name + "-guidance-golden.json")), "actual-guidance-complete-independent-literal-byte-oracle");
            Check.Bytes(Encoding.UTF8.GetBytes(package.CanonicalJson), File.ReadAllBytes(OracleCases.Fixture(name + "-fix-golden.json")), "actual-package-complete-independent-literal-byte-oracle");
            Check.Equal(package.ContentDigest, Expected.Hash(before), "actual-original-package-independent-full-byte-digest");
            Check.That(package.Packages.SelectMany(p => p.Options).SelectMany(o => o.Artifacts).All(a => a.Status == "Unverified"), "actual-generated-original-artifacts-always-unverified");
            var input = Input(name);
            var reversed = input with
            {
                Findings = input.Findings.Reverse().Select(f => f with
                { Occurrences = f.Occurrences.Reverse().ToImmutableArray(), Options = f.Options.Reverse().ToImmutableArray() }).ToImmutableArray()
            };
            var permuted = FixPackageBuilder.Build(RecommendationGuidanceBuilder.Build(reversed).Snapshot);
            Check.That(permuted.Succeeded, "actual-permuted-independent-fixture-build");
            Check.Equal(permuted.Snapshot!.CanonicalJson, before, "actual-permutation-normalizes-complete-package-order");
            Check.Equal(package.CanonicalJson, before, "actual-original-invariance");
        }
        Check.Group("TC14-T01/T03/T08 portable original source composition; task snapshot/store acceptance tested separately");
    }
}
