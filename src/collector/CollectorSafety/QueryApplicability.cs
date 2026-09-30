namespace CollectorSafety;

public sealed record InstalledModule(string ModuleId, string ExactVersion);

public sealed record SourceBuildClaim(
    string ExactBuild,
    IReadOnlyCollection<InstalledModule> InstalledModules);

public sealed record QueryApplicabilityRule(
    string QueryId,
    IReadOnlyCollection<string> SupportedExactBuilds,
    string? ModuleId,
    IReadOnlyCollection<string> SupportedExactModuleVersions);

public enum QueryApplicabilityDecision
{
    Compatible,
    UnsupportedBuild,
    ModuleNotInstalled,
    UnsupportedModuleVersion,
    InvalidInput
}

/// <summary>
/// Pure exact-match check over trusted discovery and reviewed pack metadata.
/// Compatible does not authorize a SQL query or promote a query pack.
/// </summary>
public static class QueryApplicability
{
    public static QueryApplicabilityDecision Evaluate(
        SourceBuildClaim? source,
        QueryApplicabilityRule? rule)
    {
        if (!ValidSource(source) || !ValidRule(rule))
        {
            return QueryApplicabilityDecision.InvalidInput;
        }

        if (!rule!.SupportedExactBuilds.Contains(source!.ExactBuild, StringComparer.Ordinal))
        {
            return QueryApplicabilityDecision.UnsupportedBuild;
        }

        if (rule.ModuleId is null)
        {
            return QueryApplicabilityDecision.Compatible;
        }

        var module = source.InstalledModules.SingleOrDefault(
            installed => string.Equals(installed.ModuleId, rule.ModuleId, StringComparison.Ordinal));
        if (module is null)
        {
            return QueryApplicabilityDecision.ModuleNotInstalled;
        }

        return rule.SupportedExactModuleVersions.Contains(module.ExactVersion, StringComparer.Ordinal)
            ? QueryApplicabilityDecision.Compatible
            : QueryApplicabilityDecision.UnsupportedModuleVersion;
    }

    private static bool ValidSource(SourceBuildClaim? source) =>
        source is not null &&
        Exact(source.ExactBuild) &&
        source.InstalledModules is not null &&
        source.InstalledModules.All(module =>
            module is not null &&
            !string.IsNullOrWhiteSpace(module.ModuleId) &&
            Exact(module.ExactVersion)) &&
        source.InstalledModules.Select(module => module.ModuleId)
            .Distinct(StringComparer.Ordinal).Count() == source.InstalledModules.Count;

    private static bool ValidRule(QueryApplicabilityRule? rule) =>
        rule is not null &&
        !string.IsNullOrWhiteSpace(rule.QueryId) &&
        rule.SupportedExactBuilds is { Count: > 0 } &&
        rule.SupportedExactBuilds.All(Exact) &&
        rule.SupportedExactModuleVersions is not null &&
        (rule.ModuleId is null
            ? rule.SupportedExactModuleVersions.Count == 0
            : !string.IsNullOrWhiteSpace(rule.ModuleId) &&
              rule.SupportedExactModuleVersions.Count > 0 &&
              rule.SupportedExactModuleVersions.All(Exact));

    private static bool Exact(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains('*') &&
        !value.Contains('?') &&
        !value.Split('.').Any(segment => string.Equals(segment, "x", StringComparison.OrdinalIgnoreCase));
}
