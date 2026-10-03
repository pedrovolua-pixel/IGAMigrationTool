namespace AssessmentRuns;

/// <summary>Explicit eleventh local fictional profile. No earlier source is promoted.</summary>
public static class DemoPlanningTaskCatalog
{
    public const string ProfileId = "synthetic-review-maturity-planning-tasks-equal-v1";
    public const string ApplicationVersion = "synthetic-planning-tasks-app-v1";
    public const string ContractDigest = "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2";
    public static bool IsProfile(string? id) => id == ProfileId;
    public static bool MatchesFrozenFixture(SyntheticRunSnapshot? run) => run is not null && IsProfile(run.ProfileCatalogId) &&
        run.FrozenInputs.PlanningTaskContractDigest == ContractDigest &&
        run.FrozenInputs.FixReviewContractDigest == DemoArtifactReviewCatalog.ContractDigest &&
        run.FrozenInputs.FixPackageTemplateDigest == DemoFixPackageCatalog.TemplateDigest &&
        run.FrozenInputs.ApplicationVersion == ApplicationVersion && DemoAnalysisCatalog.MatchesFrozenFixture(run);
}
