using System.Text.Json;
using AssessmentOrchestration;
using AssessmentRuns;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--capture-legacy") { LegacyCapture.Run(args[1], args[2]); return; }
            if (args.Any(x => x != "--postgres")) throw new InvalidOperationException();
            Expected.CheckTemplatePrimitives();
            CompositionCases.Run();
            using var legacy = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "legacy-goldens.json")));
            foreach (var item in legacy.RootElement.GetProperty("entries").EnumerateArray())
            {
                var baseline = item.GetProperty("baselineId").GetString()!; var profile = item.GetProperty("profileId").GetString()!;
                var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, "v10-independent-lock");
                Check.Equal(JsonSerializer.Serialize(request.Versions), item.GetProperty("versionsJson").GetString(), "original-six-profile-version-envelope");
                var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
                Check.Equal(Expected.InputDigest(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(request.Versions), baseline, profile), item.GetProperty("inputDigest").GetString(), "original-six-profile-input-digest");
                Check.That(!JsonSerializer.Serialize(request.Versions).Contains("AiPreviewFixtureDigest", StringComparison.Ordinal), "historical-optional-AI-field-omitted");
                Check.That(request.Versions.AiPolicyVersion == "synthetic-ai-disabled-v1" && request.Versions.PromptVersion == "synthetic-prompt-disabled-v1" && request.Versions.ModelVersion == "synthetic-model-disabled-v1", "historical-disabled-AI-locks");
            }
            foreach (var profile in DemoFixtureCatalog.Profiles)
                foreach (var baseline in DemoFixtureCatalog.Baselines)
                {
                    var newProfile = profile.Id is Expected.Normal or Expected.Empty;
                    var newBaseline = baseline.Id == Expected.Baseline;
                    if (newProfile != newBaseline)
                    {
                        var denied = false; try { DemoFixtureCatalog.CreateStartRequest(baseline.Id, profile.Id, "invalid-compatibility"); } catch (ArgumentException) { denied = true; }
                        Check.That(denied, "dedicated-opt-in-compatibility-denial");
                    }
                }
            Check.Equal(DemoFixtureCatalog.Profiles.Count, 10, "six-original-plus-two-offline-ai-plus-fix-preview-and-artifact-review");
            Check.Equal(DemoFixtureCatalog.Baselines.Count, 9, "eight-historical-plus-one-configuration-baseline");
            Check.Group("V10-001 independent whole-template/literal-proposal/six-old-lock/opt-in contracts");
            if (args.Contains("--postgres", StringComparer.Ordinal)) await DatabaseCases.Run();
            else Console.WriteLine("NOT VERIFIED V10-PG: actual saved-source checks require --postgres.");
            Console.WriteLine($"{Check.Count} independent local AI preview assertions passed. Fictional fixed response only; provider, production authorization, budgets, retention, manual accessibility and gates NOT VERIFIED.");
        }
        catch
        {
            Console.Error.WriteLine("FAIL V10 independent code=" + Check.Last + "; supplied exception/payload suppressed.");
            Environment.ExitCode = 1;
        }
    }
}
