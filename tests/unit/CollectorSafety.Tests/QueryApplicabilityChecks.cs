using CollectorSafety;

internal static class QueryApplicabilityChecks
{
    internal static int Run()
    {
        var source = new SourceBuildClaim("10.0.1-HF1",
            [new InstalledModule("ITSHOP", "10.0.1-HF1")]);
        var baseRule = new QueryApplicabilityRule("BASE-QUERY", ["10.0.1-HF1"], null, []);
        var moduleRule = new QueryApplicabilityRule("ITSHOP-QUERY", ["10.0.1-HF1"],
            "ITSHOP", ["10.0.1-HF1"]);
        var count = 0;

        Check("base exact build", QueryApplicabilityDecision.Compatible, source, baseRule);
        Check("installed exact module", QueryApplicabilityDecision.Compatible, source, moduleRule);
        Check("other build", QueryApplicabilityDecision.UnsupportedBuild,
            source with { ExactBuild = "10.0.2-HF1" }, moduleRule);
        Check("build case is exact", QueryApplicabilityDecision.UnsupportedBuild,
            source with { ExactBuild = "10.0.1-hf1" }, moduleRule);
        Check("module absent", QueryApplicabilityDecision.ModuleNotInstalled,
            source with { InstalledModules = [] }, moduleRule);
        Check("module version differs", QueryApplicabilityDecision.UnsupportedModuleVersion,
            source with { InstalledModules = [new InstalledModule("ITSHOP", "10.0.1-HF2")] }, moduleRule);
        Check("module id case is exact", QueryApplicabilityDecision.ModuleNotInstalled,
            source with { InstalledModules = [new InstalledModule("itshop", "10.0.1-HF1")] }, moduleRule);
        Check("unknown source build", QueryApplicabilityDecision.InvalidInput,
            source with { ExactBuild = "10.x" }, moduleRule);
        Check("wildcard pack build", QueryApplicabilityDecision.InvalidInput,
            source, moduleRule with { SupportedExactBuilds = ["10.*"] });
        Check("missing pack builds", QueryApplicabilityDecision.InvalidInput,
            source, moduleRule with { SupportedExactBuilds = [] });
        Check("module rule without versions", QueryApplicabilityDecision.InvalidInput,
            source, moduleRule with { SupportedExactModuleVersions = [] });
        Check("base rule with module versions", QueryApplicabilityDecision.InvalidInput,
            source, baseRule with { SupportedExactModuleVersions = ["10.0.1-HF1"] });
        Check("duplicate installed module", QueryApplicabilityDecision.InvalidInput,
            source with
            {
                InstalledModules =
                [new InstalledModule("ITSHOP", "10.0.1-HF1"), new InstalledModule("ITSHOP", "10.0.1-HF2")]
            }, moduleRule);
        Check("missing source", QueryApplicabilityDecision.InvalidInput, null, moduleRule);

        return count;

        void Check(string name, QueryApplicabilityDecision expected,
            SourceBuildClaim? claim, QueryApplicabilityRule? rule)
        {
            var actual = QueryApplicability.Evaluate(claim, rule);
            if (actual != expected)
            {
                throw new Exception($"{name}: expected {expected}, got {actual}.");
            }

            count++;
        }
    }
}
