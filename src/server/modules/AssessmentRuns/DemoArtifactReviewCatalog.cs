namespace AssessmentRuns;

/// <summary>Approved dedicated local artifact-review profile; no historical profile is promoted.</summary>
public static class DemoArtifactReviewCatalog
{
    public const string ProfileId = "synthetic-review-maturity-fix-review-equal-v1";
    public const string ApplicationVersion = "synthetic-fix-review-app-v1";
    public const string ContractDigest = "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f";
    public static bool IsProfile(string? id) => id == ProfileId;
    public static bool MatchesFrozenFixture(SyntheticRunSnapshot? run)
    {
        try
        {
            return run is not null && IsProfile(run.ProfileCatalogId) && run.RunId != Guid.Empty && run.Revision > 0 &&
                run.FrozenInputs is not null && run.Plan is not null &&
                run.FrozenInputs.FixPackageTemplateDigest == DemoFixPackageCatalog.TemplateDigest &&
                run.FrozenInputs.FixReviewContractDigest == ContractDigest &&
                run.FrozenInputs.ApplicationVersion == ApplicationVersion &&
                DemoAnalysisCatalog.MatchesFrozenFixture(run) &&
                run.InputDigest == SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, run.FrozenInputs,
                    run.BaselineCatalogId, run.ProfileCatalogId);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        { return false; }
    }
}
