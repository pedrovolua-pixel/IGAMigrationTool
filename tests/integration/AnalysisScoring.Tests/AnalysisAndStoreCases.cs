using System.Collections.Immutable;
using System.Text.Json;
using AssessmentCoverage;
using DeterministicAnalysis;

internal static class AnalysisAndStoreCases
{
    private static readonly Guid SyntheticRunId = Guid.Parse("0e671399-45dc-4fe2-8c2d-4773b7b7e001");
    internal static async Task Run(string[] args)
    {
        RuleClasses();
        AnalysisGoldens();
        await DurableCases.Run(args);
    }

    private static void RuleClasses()
    {
        var expected = new Dictionary<string, (SyntheticFactKind Fact, SyntheticSeverity Severity, string Module, string Category)>
        {
            ["SYN-GUARD"] = (SyntheticFactKind.GuardFailureCount, SyntheticSeverity.Critical, "SyntheticSecurity", "SECURITY"),
            ["SYN-TRACE"] = (SyntheticFactKind.TraceFailureCount, SyntheticSeverity.High, "SyntheticSecurity", "SECURITY"),
            ["SYN-PENDING"] = (SyntheticFactKind.PendingMarkerCount, SyntheticSeverity.Medium, "SyntheticOperations", "OPERATIONS"),
            ["SYN-DUPLICATE"] = (SyntheticFactKind.DuplicateMarkerCount, SyntheticSeverity.Low, "SyntheticOperations", "OPERATIONS"),
            ["SYN-UNUSED"] = (SyntheticFactKind.UnusedMarkerCount, SyntheticSeverity.Informational, "SyntheticOperations", "OPERATIONS")
        };
        Program.Require(SyntheticAnalysisFixturePack.Rules.Length == 5, "fixed five rule classes independently expected");
        foreach (var rule in SyntheticAnalysisFixturePack.Rules)
        {
            var literal = expected[rule.Id];
            Program.Require(rule.RequiredFact == literal.Fact && rule.Severity == literal.Severity && rule.ModuleId == literal.Module &&
                rule.CategoryId == literal.Category && rule.Version == "synthetic-rule-v1" && rule.Weight == 1m, "exact rule metadata");
            var evidence = new SyntheticEvidenceObject("independent-object", "SyntheticControl", literal.Module,
                "fixture-evidence:independent-object:v1", SyntheticAnalysisFixturePack.Compatibility, false,
                [new(literal.Fact, SyntheticFactAvailability.Known, 1)]);
            Evaluation(rule, evidence, CoverageState.Finding, null);
            Evaluation(rule, evidence with { Facts = [new(literal.Fact, SyntheticFactAvailability.Known, 0)] }, CoverageState.Pass, null);
            Evaluation(rule, evidence with { Facts = [] }, CoverageState.InsufficientEvidence, "SYNTHETIC-FACT-MISSING");
            Evaluation(rule, evidence with { Excluded = true }, CoverageState.Excluded, "SYNTHETIC-FIXTURE-EXCLUSION");
            Evaluation(rule, evidence with { Compatibility = evidence.Compatibility with { ProductVersion = "unknown-version" } },
                CoverageState.Unsupported, "SYNTHETIC-VERSION-INCOMPATIBLE");
            Evaluation(rule with { Version = "unknown-rule-version" }, evidence, CoverageState.Unsupported, "SYNTHETIC-VERSION-INCOMPATIBLE");
            Evaluation(rule, evidence with { Facts = [new(literal.Fact, SyntheticFactAvailability.Redacted, null)] }, CoverageState.Redacted, "SYNTHETIC-FACT-REDACTED");
            Evaluation(rule, evidence with { Facts = [new(literal.Fact, SyntheticFactAvailability.Conflicting, 1)] }, CoverageState.InsufficientEvidence, "SYNTHETIC-FACT-CONFLICT");
            Evaluation(rule, evidence with { Facts = [new(literal.Fact, SyntheticFactAvailability.Known, 1), new(literal.Fact, SyntheticFactAvailability.Known, 1)] },
                CoverageState.InsufficientEvidence, "SYNTHETIC-FACT-CONFLICT");
            Evaluation(rule, evidence with { ModuleId = "uninstalled-fixture-module" }, CoverageState.NotApplicable, "SYNTHETIC-TYPE-NOT-APPLICABLE");
        }
        Program.Group("AS-RULE-001 five rules positive/negative/insufficient/exclusion/compatibility/redaction/conflict");
    }

    private static void Evaluation(SyntheticRule rule, SyntheticEvidenceObject evidence, CoverageState state, string? reason)
    {
        var result = SyntheticRuleEvaluator.Evaluate(rule, evidence);
        Program.Require(result.State == state && result.ReasonCode == reason && result.ResponsibleStage == "SyntheticDeterministicRule",
            $"rule {rule.Id} expected {state}/{reason}, actual {result.State}/{result.ReasonCode}");
        if (state is CoverageState.Pass or CoverageState.Finding)
            Program.Require(result.ObservedFact is { Availability: SyntheticFactAvailability.Known } && result.ObservedFact.Count == (state == CoverageState.Pass ? 0 : 1), "actual typed fact retained");
        else Program.Require(result.ObservedFact is null, "gap cannot expose fabricated observed fact");
    }

    private static void AnalysisGoldens()
    {
        var frozen = SyntheticAnalysisFixturePack.Freeze(SyntheticAnalysisFixturePack.Scope, "synthetic-analysis-findings-v1", "synthetic-analysis-equal-v1");
        var plan = SyntheticAnalysisEngine.Plan(frozen);
        var expectedKeys = new[]
        {
            "SyntheticSecurity-OBJECT-1/SYN-GUARD", "SyntheticSecurity-OBJECT-2/SYN-GUARD",
            "SyntheticSecurity-OBJECT-1/SYN-TRACE", "SyntheticSecurity-OBJECT-2/SYN-TRACE",
            "SyntheticOperations-OBJECT-1/SYN-PENDING", "SyntheticOperations-OBJECT-2/SYN-PENDING",
            "SyntheticOperations-OBJECT-1/SYN-DUPLICATE", "SyntheticOperations-OBJECT-2/SYN-DUPLICATE",
            "SyntheticOperations-OBJECT-1/SYN-UNUSED", "SyntheticOperations-OBJECT-2/SYN-UNUSED"
        }.Order(StringComparer.Ordinal).ToArray();
        Program.Require(plan.ExpectedKeys.Select(key => $"{key.InventoryId}/{key.EvidenceCategory}").Order(StringComparer.Ordinal).SequenceEqual(expectedKeys), "literal ten rule-subject keys");
        var actual = Accept(frozen, plan.ExpectedResults);
        Program.Require(actual.Findings.Length == 10 && actual.Groups.Length == 5 && actual.Groups.All(group => group.OccurrenceIds.Length == 2 && group.ObjectIds.Length == 2),
            "five root-cause presentation groups preserve ten separate object occurrences");
        Program.Require(actual.Findings.Count(finding => finding.InitialDisposition == SyntheticInitialDisposition.Proposed) == 4 &&
            actual.Findings.Count(finding => finding.InitialDisposition == SyntheticInitialDisposition.AutoConfirmed) == 6, "four mandatory proposals and six eligible catalog auto-confirmed findings");
        foreach (var finding in actual.Findings)
        {
            Program.Require(finding.DetectionMethod == "Deterministic" && finding.ConfidencePercent == 100m && finding.Facts.Length == 1 && finding.Facts.Single().Count == 1,
                "deterministic provenance and explicit actual marker fact");
            Program.Require(finding.Provenance is { BaselineId: "synthetic-analysis-findings-v1", BaselineVersion: "synthetic-evidence-v1", RuleVersion: "synthetic-rule-v1", CatalogVersion: "synthetic-analysis-catalog-v1" } &&
                finding.Provenance.Scope == SyntheticAnalysisFixturePack.Scope && finding.Provenance.EvidenceDigest == frozen.EvidenceDigest &&
                finding.Provenance.EvidenceReference == $"fixture-evidence:synthetic-analysis-findings-v1:{finding.ObjectId}", "exact frozen baseline/version/evidence scope reference");
            Program.Require(finding.RecommendationOptions.Length == 2 && finding.ValidationGuidance.Length == 2 && finding.Assumptions.Length == 2 &&
                finding.GeneratedOriginalDigest.Length == 64 && finding.RootCauseKey.Length == 64 && finding.OccurrenceId.Length == 64 &&
                !string.IsNullOrWhiteSpace(finding.Inference) && !string.IsNullOrWhiteSpace(finding.Impact) && !string.IsNullOrWhiteSpace(finding.RootCause),
                "generated original retains inference/assumptions/impact/root cause/advice/validation and digest");
        }
        Program.Require(actual.RunId == SyntheticRunId && actual.Findings.All(finding => finding.Provenance.RunId == SyntheticRunId), "exact run occurrence provenance retained");
        var nextRun = SyntheticAnalysisEngine.Analyze(frozen, Guid.Parse("0e671399-45dc-4fe2-8c2d-4773b7b7e002"), plan.ExpectedResults).Analysis!;
        Program.Require(nextRun.Findings.Select(finding => finding.OccurrenceId).Intersect(actual.Findings.Select(finding => finding.OccurrenceId)).Count() == 0 &&
            nextRun.Groups.Select(group => group.RootCauseKey).SequenceEqual(actual.Groups.Select(group => group.RootCauseKey)),
            "new run creates distinct immutable occurrences with stable root-cause correlation");
        var encoded = JsonSerializer.Serialize(actual);
        var reordered = Accept(frozen, plan.ExpectedResults.Reverse().ToArray());
        Program.Require(reordered.ContentDigest == actual.ContentDigest && JsonSerializer.Serialize(reordered) == encoded, "coverage order preserves canonical generated-original projection");
        var caller = plan.ExpectedResults.ToArray();
        var copied = Accept(frozen, caller);
        caller[0] = caller[0] with { State = CoverageState.Pass };
        Program.Require(copied.ContentDigest == actual.ContentDigest && JsonSerializer.Serialize(copied) == encoded, "caller mutation cannot rewrite generated originals");
        Deny(frozen, caller, SyntheticAnalysisIssue.ResultMismatch, "complete coverage must match evaluated facts");
        Deny(frozen, plan.ExpectedResults.Take(9).ToArray(), SyntheticAnalysisIssue.IncompleteCoverage, "missing coverage refuses complete analysis");
        Deny(frozen, [.. plan.ExpectedResults, plan.ExpectedResults[0]], SyntheticAnalysisIssue.IncompleteCoverage, "duplicate coverage refuses analysis");
        Deny(frozen with { Scope = frozen.Scope with { CustomerId = "synthetic-other-customer" } }, plan.ExpectedResults, SyntheticAnalysisIssue.WrongScope, "wrong scope refuses analysis");
        Deny(frozen with { PackDigest = new('0', 64) }, plan.ExpectedResults, SyntheticAnalysisIssue.FrozenInputMismatch, "pack digest drift refuses analysis");
        Deny(frozen with { CatalogVersion = "unknown-catalog" }, plan.ExpectedResults, SyntheticAnalysisIssue.FrozenInputMismatch, "catalog version drift refuses analysis");
        Deny(frozen with { EvidenceDigest = new('0', 64) }, plan.ExpectedResults, SyntheticAnalysisIssue.FrozenInputMismatch, "frozen evidence digest drift refuses analysis");
        Deny(frozen with { ProfileDigest = new('0', 64) }, plan.ExpectedResults, SyntheticAnalysisIssue.FrozenInputMismatch, "frozen profile digest drift refuses analysis");
        Deny(frozen with { Compatibility = frozen.Compatibility with { EvidenceSchemaVersion = "unknown-schema" } }, plan.ExpectedResults,
            SyntheticAnalysisIssue.FrozenInputMismatch, "schema compatibility drift refuses analysis");
        var healthyLock = SyntheticAnalysisFixturePack.Freeze(SyntheticAnalysisFixturePack.Scope, "synthetic-analysis-healthy-v1", "synthetic-analysis-equal-v1");
        var healthyPlan = SyntheticAnalysisEngine.Plan(healthyLock);
        Program.Require(healthyPlan.ExpectedResults.Length == 10 && healthyPlan.ExpectedResults.All(item => item.State == CoverageState.Pass) &&
            Accept(healthyLock, healthyPlan.ExpectedResults).Findings.Length == 0, "healthy marker zero produces literal ten passes without findings");
        var gapsLock = SyntheticAnalysisFixturePack.Freeze(SyntheticAnalysisFixturePack.Scope, "synthetic-analysis-gaps-v1", "synthetic-analysis-equal-v1");
        var gapsPlan = SyntheticAnalysisEngine.Plan(gapsLock);
        Program.Require(gapsPlan.ExpectedResults.Count(item => item.State == CoverageState.InsufficientEvidence) == 5 && gapsPlan.ExpectedResults.Count(item => item.State == CoverageState.Excluded) == 5 &&
            Accept(gapsLock, gapsPlan.ExpectedResults).Findings.Length == 0, "missing/excluded facts yield literal five/five explained gaps without health claims");
        Program.Group("AS-RULE-002 actual predicate results/provenance/grouping/immutability/digest incompatibility");
    }

    internal static SyntheticAnalysisResult Accept(SyntheticAnalysisLock frozen, IReadOnlyCollection<CoverageItem> coverage)
    {
        var response = SyntheticAnalysisEngine.Analyze(frozen, SyntheticRunId, coverage);
        Program.Require(response.Succeeded && response.Analysis is not null, $"analysis accepts valid fixture, actual {response.Issue}");
        return response.Analysis!;
    }

    private static void Deny(SyntheticAnalysisLock frozen, IReadOnlyCollection<CoverageItem> coverage, SyntheticAnalysisIssue expected, string name)
    {
        var response = SyntheticAnalysisEngine.Analyze(frozen, SyntheticRunId, coverage);
        Program.Require(!response.Succeeded && response.Analysis is null && response.Issue == expected, $"{name}, expected {expected}, actual {response.Issue}");
    }
}
