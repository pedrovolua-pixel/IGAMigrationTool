using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentOrchestration;
using AssessmentRuns;
using RecommendationGuidance;
using SyntheticFixPackages;

internal static class PortableCases
{
    private static string FileName(string name) => Path.Combine(AppContext.BaseDirectory, name);
    internal static void Run()
    {
        Check.Equal(Expected.Hash(Expected.Canonical(new JsonObject { ["templateVersion"] = Expected.TemplateVersion, ["templates"] = Expected.TemplateNodes() })), Expected.TemplateDigest, "independent-three-template-byte-recipe");
        foreach (var name in new[] { "normal", "hostile", "empty" })
        {
            var input = JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(FileName(name + "-input.json")), V12Program.Web)!;
            var guidance = RecommendationGuidanceBuilder.Build(input);
            Check.That(guidance.Succeeded && guidance.Snapshot is not null, "actual-new-profile-guidance-builder");
            var original = guidance.Snapshot!;
            Check.Bytes(RecommendationGuidanceBuilder.CanonicalPayload(original), File.ReadAllBytes(FileName(name + "-guidance-golden.json")), "independent-complete-literal-guidance-bytes");
            var built = FixPackageBuilder.Build(original);
            Check.That(built.Succeeded && built.Snapshot is not null, "actual-new-profile-fix-builder");
            Check.Bytes(Encoding.UTF8.GetBytes(built.Snapshot!.CanonicalJson), File.ReadAllBytes(FileName(name + "-fix-golden.json")), "independent-complete-literal-fix-bytes");
            var expected = Expected.FixPayload(JsonSerializer.SerializeToNode(original, V12Program.Web)!.AsObject());
            Check.Equal(Expected.Canonical(expected), built.Snapshot.CanonicalJson, "independent-upstream-input-complete-fix-recipe");
            Check.Equal(built.Snapshot.ContentDigest, Expected.Hash(Expected.Canonical(expected)), "independent-complete-fix-content-digest");
            Check.Equal(built.Snapshot.TemplateDigest, Expected.TemplateDigest, "exact-frozen-template-digest");
            Check.Equal(built.Snapshot.Guidance.ContentDigest, original.ContentDigest, "actual-captured-guidance-unchanged");
            foreach (var current in new[] { "Proposed", "Confirmed", "Rejected", "Deferred" })
            {
                var changed = input with { Findings = input.Findings.Select(f => f with { CurrentState = current, FindingRevision = 4 }).ToImmutableArray() };
                var revised = RecommendationGuidanceBuilder.Build(changed);
                Check.That(revised.Succeeded, "new-profile-current-guidance-state-valid");
                var package = FixPackageBuilder.Build(revised.Snapshot);
                Check.That(package.Succeeded && package.Snapshot!.Packages.SelectMany(p => p.Options).SelectMany(o => o.Artifacts).All(a => a.Status == "Unverified"), "finding-review-never-approves-artifact");
                Check.That(package.Snapshot!.Packages.Select(p => p.PackageId).SequenceEqual(built.Snapshot.Packages.Select(p => p.PackageId)), "package-identity-preserved-across-finding-review");
            }
        }
        var validInput = JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(FileName("normal-input.json")), V12Program.Web)!;
        var validGuidance = RecommendationGuidanceBuilder.Build(validInput).Snapshot!;
        void Deny(GuidanceSnapshot? snapshot)
        { var denied = FixPackageBuilder.Build(snapshot); Check.That(!denied.Succeeded && denied.Snapshot is null, "malformed-complete-guidance-no-partial-package"); }
        Deny(null); Deny(validGuidance with { ContentDigest = new string('0', 64) });
        Deny(validGuidance with { Source = validGuidance.Source with { ReviewSnapshotDigest = new string('0', 64) } });
        Deny(validGuidance with { Findings = validGuidance.Findings.Select(f => f with { FindingRevision = 99 }).ToImmutableArray() });
        Deny(validGuidance with { Status = "Approved" }); Deny(validGuidance with { Warnings = [] });
        var permuted = validInput with { Findings = validInput.Findings.Reverse().Select(f => f with { Occurrences = f.Occurrences.Reverse().ToImmutableArray(), Options = f.Options.Reverse().ToImmutableArray() }).ToImmutableArray() };
        var permutedBuild = FixPackageBuilder.Build(RecommendationGuidanceBuilder.Build(permuted).Snapshot);
        Check.Equal(permutedBuild.Snapshot!.CanonicalJson, File.ReadAllText(FileName("normal-fix-golden.json")), "independent-permutation-full-bytes-stable");
        foreach (var mutation in new Action<JsonObject>[]
        {
            v => v.Remove("fixPackageTemplateDigest"), v => v["fixPackageTemplateDigest"] = new string('0', 64),
            v => v["applicationVersion"] = "synthetic-review-maturity-app-v1", v => v["unexpected"] = true
        })
        {
            var versions = JsonNode.Parse(validInput.Source.FrozenVersions.GetRawText())!.AsObject(); mutation(versions);
            var result = RecommendationGuidanceBuilder.Build(validInput with { Source = validInput.Source with { FrozenVersions = JsonSerializer.SerializeToElement(versions) } });
            Check.That(!result.Succeeded && result.Snapshot is null, "new-profile-exact-app-twelve-lock-contract-denied");
        }
        using var historical = JsonDocument.Parse(File.ReadAllText(FileName("historical-six-locks.json")));
        foreach (var entry in historical.RootElement.GetProperty("entries").EnumerateArray())
        {
            var baseline = entry.GetProperty("baselineId").GetString()!; var profile = entry.GetProperty("profileId").GetString()!;
            var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, "v12-independent-historical");
            var versions = JsonSerializer.Serialize(request.Versions);
            Check.Equal(versions, entry.GetProperty("versionsJson").GetString(), "historical-six-full-input-envelopes-unchanged");
            var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
            Check.Equal(Expected.InputDigest(JsonSerializer.Serialize(plan), versions, baseline, profile), entry.GetProperty("inputDigest").GetString(), "historical-six-independent-input-identities-unchanged");
            Check.That(!versions.Contains("FixPackageTemplateDigest", StringComparison.Ordinal), "historical-input-new-optional-lock-omitted");
        }
        foreach (var baseline in DemoFixtureCatalog.Baselines)
        {
            var allowed = Expected.Presets.Contains(baseline.Id, StringComparer.Ordinal);
            if (!allowed)
            {
                var denied = false; try { _ = DemoFixtureCatalog.CreateStartRequest(baseline.Id, Expected.Profile, "invalid-new-profile"); } catch (ArgumentException) { denied = true; }
                Check.That(denied, "opt-in-only-existing-four-analysis-presets");
                continue;
            }
            var request = DemoFixtureCatalog.CreateStartRequest(baseline.Id, Expected.Profile, "v12-independent-new-lock");
            var original = DemoFixtureCatalog.CreateStartRequest(baseline.Id, "synthetic-review-maturity-equal-v1", "v12-independent-old-lock");
            var versions = JsonSerializer.SerializeToNode(request.Versions)!.AsObject();
            Check.Equal(versions["FixPackageTemplateDigest"]!.GetValue<string>(), Expected.TemplateDigest, "exact-new-profile-optional-input-lock");
            Check.Equal(versions["ApplicationVersion"]!.GetValue<string>(), Expected.Application, "dedicated-application-version");
            Check.Equal(request.Versions.AnalysisFixtureDigest, original.Versions.AnalysisFixtureDigest, "explicit-existing-equal-analysis-lock");
            Check.Equal(request.Versions.MaturityFixtureDigest, original.Versions.MaturityFixtureDigest, "explicit-existing-maturity-lock");
            Check.That(request.Versions.AiPreviewFixtureDigest is null && request.Versions.DesiredOutcomeVersion is null, "new-profile-no-AI-or-customer-outcome-authority");
            versions.Remove("FixPackageTemplateDigest"); versions["ApplicationVersion"] = "synthetic-review-maturity-app-v1";
            Check.Equal(Expected.Canonical(versions), Expected.Canonical(JsonSerializer.SerializeToNode(original.Versions)), "new-input-only-settled-two-field-difference");
        }
        foreach (var profile in DemoFixtureCatalog.Profiles.Where(p => p.Id != Expected.Profile && p.Id != DemoArtifactReviewCatalog.ProfileId && !DemoPlanningTaskCatalog.IsProfile(p.Id)))
            Check.That(!JsonSerializer.Serialize(profile.Versions).Contains("FixPackageTemplateDigest", StringComparison.Ordinal), "all-eight-historical-profile-new-lock-omitted");
        Check.Equal(DemoFixtureCatalog.Profiles.Count, 11, "explicit-task-profile-added-to-historical-ten");
        Check.Equal(DemoFixtureCatalog.Profiles.Count(p => !DemoPlanningTaskCatalog.IsProfile(p.Id)), 10, "two-explicit-fix-profiles-plus-eight-earlier-profiles");
        Check.Equal(DemoFixtureCatalog.Baselines.Count, 9, "no-new-baseline");
        Check.Group("V12-PORTABLE literal-source/full-canonical/template/identity/review/old-lock/opt-in invariants");
    }
}
