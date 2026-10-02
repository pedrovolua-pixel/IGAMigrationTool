using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;

internal static class SyntheticFixture
{
    internal static readonly SyntheticAuthorizedScope Scope = new("synthetic-v3-customer", "synthetic-v3-project", "synthetic-v3-environment");
    internal static readonly SyntheticRunPolicy Policy = new(TimeSpan.FromMinutes(5), 2);

    internal static SyntheticStartRequest Start(string key)
    {
        var modules = new[] { new ModuleVersion("QBM", "synthetic-module-v1") };
        var capability = new CapabilitySnapshot("synthetic-matrix-v1", CapabilityLifecycleState.FixtureVerified,
            "10.synthetic.1", "synthetic-schema-v1", "synthetic-hotfix-v1", "synthetic-sql-v1", 160,
            modules, "synthetic-pack-v1", "synthetic-normalized-v1", "synthetic-rules-v1");
        var compatibility = new BaselineCompatibility("10.synthetic.1", "synthetic-schema-v1", "synthetic-hotfix-v1",
            "synthetic-sql-v1", 160, modules, "synthetic-pack-v1", "synthetic-normalized-v1");
        var baseline = new SyntheticBaselineInventory("synthetic-baseline-inventory-v1", "synthetic-v3-baseline", Scope,
            SyntheticBaselinePermission.EligibleWithWarning, compatibility,
            [new("synthetic-object", "Process", null, "QBM",
                [new("assessment-a", SyntheticApplicability.RequiresAssessment),
                 new("assessment-b", SyntheticApplicability.RequiresAssessment),
                 new("ignore", SyntheticApplicability.NotApplicable, "FEATURE-NOT-APPLICABLE", "planner"),
                 new("semantics", SyntheticApplicability.Unsupported, "SEMANTIC-DEFERRED", "planner"),
                 new("unavailable", SyntheticApplicability.NotAssessed, "NOT-EXECUTED", "planner")])]);
        return new(Scope, key, "synthetic-baseline-catalog", "synthetic-profile-catalog", capability, baseline,
            new("synthetic-profile-v1", "synthetic-outcomes-v1", "synthetic-scoring-v1", "synthetic-ai-disabled-v1",
                "synthetic-prompt-v1", "synthetic-model-disabled-v1", "synthetic-application-v1", "synthetic-run-work-v1",
                DemoFixtureCatalog.ComputeScriptedResultsDigest([Pass, Finding])));
    }

    internal static CoverageItem Pass => new(new("synthetic-object", "assessment-a"), CoverageState.Pass);
    internal static CoverageItem Finding => new(new("synthetic-object", "assessment-b"), CoverageState.Finding);
}
