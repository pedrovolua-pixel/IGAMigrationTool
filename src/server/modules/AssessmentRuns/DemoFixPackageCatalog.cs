namespace AssessmentRuns;

/// <summary>One opt-in fictional template lock. Historical review profiles retain their original envelope.</summary>
public static class DemoFixPackageCatalog
{
    public const string ProfileId = "synthetic-review-maturity-fix-packages-equal-v1";
    public const string ApplicationVersion = "synthetic-fix-packages-app-v1";
    public const string TemplateVersion = "fictional-fix-templates-v1";
    public const string TemplateDigest = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
    public static bool IsProfile(string? id) => id == ProfileId;

    // DemoAnalysisCatalog never delegates here; its worker gate remains independent of active run state.
    public static bool MatchesFrozenFixture(SyntheticRunSnapshot? run)
    {
        try
        {
            return run is not null && IsProfile(run.ProfileCatalogId) && run.RunId != Guid.Empty && run.Revision > 0 &&
                run.FrozenInputs is not null && run.Plan is not null &&
                run.FrozenInputs.FixPackageTemplateDigest == TemplateDigest &&
                run.FrozenInputs.ApplicationVersion == ApplicationVersion &&
                DemoAnalysisCatalog.MatchesFrozenFixture(run) &&
                run.InputDigest == SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, run.FrozenInputs,
                    run.BaselineCatalogId, run.ProfileCatalogId);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return false;
        }
    }
}
