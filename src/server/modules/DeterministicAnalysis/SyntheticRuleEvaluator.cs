using AssessmentCoverage;

namespace DeterministicAnalysis;

/// <summary>Pure typed fixture predicate. Not an authorization, catalog activation or customer evidence entry point.</summary>
public static class SyntheticRuleEvaluator
{
    public static SyntheticRuleEvaluation Evaluate(SyntheticRule rule, SyntheticEvidenceObject evidence)
    {
        const string stage = "SyntheticDeterministicRule";
        if (rule.Version != SyntheticAnalysisFixturePack.RuleVersion ||
            evidence.Compatibility != SyntheticAnalysisFixturePack.Compatibility ||
            rule.Predicate != SyntheticPredicate.CountGreaterThan || !Enum.IsDefined(rule.RequiredFact))
            return new(CoverageState.Unsupported, "SYNTHETIC-VERSION-INCOMPATIBLE", stage, null);
        if (rule.ModuleId != evidence.ModuleId || rule.ObjectType != evidence.ObjectType)
            return new(CoverageState.NotApplicable, "SYNTHETIC-TYPE-NOT-APPLICABLE", stage, null);
        if (evidence.Excluded)
            return new(CoverageState.Excluded, "SYNTHETIC-FIXTURE-EXCLUSION", stage, null);
        var matches = evidence.Facts.Where(fact => fact.Kind == rule.RequiredFact).ToArray();
        if (matches.Length != 1)
            return new(CoverageState.InsufficientEvidence, matches.Length == 0 ? "SYNTHETIC-FACT-MISSING" : "SYNTHETIC-FACT-CONFLICT", stage, null);
        var fact = matches[0];
        if (fact.Availability == SyntheticFactAvailability.Redacted)
            return new(CoverageState.Redacted, "SYNTHETIC-FACT-REDACTED", stage, null);
        if (fact.Availability != SyntheticFactAvailability.Known || fact.Count is null or < 0)
            return new(CoverageState.InsufficientEvidence,
                fact.Availability == SyntheticFactAvailability.Conflicting ? "SYNTHETIC-FACT-CONFLICT" : "SYNTHETIC-FACT-MISSING", stage, null);
        return new(fact.Count > rule.Threshold ? CoverageState.Finding : CoverageState.Pass, null, stage, fact);
    }
}
