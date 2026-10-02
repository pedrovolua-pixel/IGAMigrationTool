using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Executes the sealed prior-cycle factories in a separate load context. No snapshot is forged.
internal static class LegacyCapture
{
    internal static void Run(string assemblyPath, string outputPath)
    {
        if (!Path.IsPathFullyQualified(assemblyPath) || !Path.IsPathFullyQualified(outputPath)) throw new InvalidOperationException("capture-path");
        var context = new PreviousContext(assemblyPath);
        try
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var catalog = assembly.GetType("AssessmentRuns.DemoFixtureCatalog", true)!;
            var method = catalog.GetMethod("CreateStartRequest")!;
            var orchestration = context.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(assemblyPath)!, "AssessmentOrchestration.dll"));
            var planner = orchestration.GetType("AssessmentOrchestration.SyntheticBaselineInventoryPlanner", true)!;
            var plan = planner.GetMethod("Plan")!;
            var entries = new List<object>();
            foreach (var profile in new[] { "profile-standard", "profile-comparison", "synthetic-analysis-equal-v1", "synthetic-analysis-operations-v1", "synthetic-review-maturity-equal-v1", "synthetic-review-maturity-operations-v1" })
            {
                var baseline = profile.StartsWith("profile-", StringComparison.Ordinal) ? "baseline-complete" : "synthetic-analysis-findings-v1";
                var request = method.Invoke(null, new object[] { baseline, profile, "v10-legacy-oracle" })!;
                object Value(string property) => request.GetType().GetProperty(property)!.GetValue(request)!;
                var versions = Value("Versions");
                var planned = plan.Invoke(null, new[] { Value("Capability"), Value("Baseline"), Value("Scope") })!;
                var plannedValue = planned.GetType().GetProperty("Plan")!.GetValue(planned)!;
                var versionsJson = JsonSerializer.Serialize(versions, versions.GetType());
                var planJson = JsonSerializer.Serialize(plannedValue, plannedValue.GetType());
                var parts = new[] { "synthetic-run-input-lock-v1", planJson, versionsJson, baseline, profile };
                var bytes = Encoding.UTF8.GetBytes(string.Concat(parts.Select(value => value.Length + ":" + value)));
                entries.Add(new { baselineId = baseline, profileId = profile, versionsJson, inputDigest = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
            }
            var metadata = new { authority = "Executed original six-profile catalog from preserved pre-Cycle10 assembly in isolated load context; independent input digest recipe", assemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assemblyPath))), entries };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            Console.WriteLine("PASS V10 original six-profile compatibility oracle captured; no fixture payload logged.");
        }
        finally { context.Unload(); }
    }
    private sealed class PreviousContext(string path) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver resolver = new(path);
        protected override Assembly? Load(AssemblyName name)
        {
            var location = resolver.ResolveAssemblyToPath(name);
            return location is null ? null : LoadFromAssemblyPath(location);
        }
    }
}
