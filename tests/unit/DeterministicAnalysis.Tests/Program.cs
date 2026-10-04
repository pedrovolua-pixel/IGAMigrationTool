using System.Collections.Immutable;
using AssessmentCoverage;
using DeterministicAnalysis;

var checks = 0;
var runId = Guid.Parse("a47754f2-12d7-4692-9785-279f8b43a3ad");
foreach (var rule in SyntheticAnalysisFixturePack.Rules)
{
    var evidence = SyntheticAnalysisFixturePack.GetPreset("synthetic-analysis-findings-v1").Objects.First(item => item.ModuleId == rule.ModuleId);
    var positive = SyntheticRuleEvaluator.Evaluate(rule, evidence);
    Check($"{rule.Id} positive", positive.State == CoverageState.Finding && positive.ObservedFact?.Count == 1);
    var negativeEvidence = evidence with { Facts = [new(rule.RequiredFact, SyntheticFactAvailability.Known, 0)] };
    Check($"{rule.Id} negative", SyntheticRuleEvaluator.Evaluate(rule, negativeEvidence).State == CoverageState.Pass);
    var insufficient = SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [] });
    Check($"{rule.Id} insufficient", insufficient.State == CoverageState.InsufficientEvidence && insufficient.ReasonCode == "SYNTHETIC-FACT-MISSING");
    var excluded = SyntheticRuleEvaluator.Evaluate(rule, evidence with { Excluded = true });
    Check($"{rule.Id} exclusion", excluded.State == CoverageState.Excluded && excluded.ReasonCode == "SYNTHETIC-FIXTURE-EXCLUSION");
    Check($"{rule.Id} exact compatible", positive.State == CoverageState.Finding);
    var incompatible = SyntheticRuleEvaluator.Evaluate(rule, evidence with { Compatibility = evidence.Compatibility with { ProductVersion = "fixture-product-v2" } });
    Check($"{rule.Id} incompatible product", incompatible.State == CoverageState.Unsupported && incompatible.ReasonCode == "SYNTHETIC-VERSION-INCOMPATIBLE");
    Check($"{rule.Id} incompatible schema", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Compatibility = evidence.Compatibility with { EvidenceSchemaVersion = "fixture-facts-v2" } }).State == CoverageState.Unsupported);
    Check($"{rule.Id} incompatible rule", SyntheticRuleEvaluator.Evaluate(rule with { Version = "synthetic-rule-v2" }, evidence).State == CoverageState.Unsupported);
    Check($"{rule.Id} wrong source", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Compatibility = evidence.Compatibility with { SourceProduct = "OneIdentity" } }).State == CoverageState.Unsupported);
    Check($"{rule.Id} wrong predicate language", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Compatibility = evidence.Compatibility with { RuleLanguageVersion = "unknown" } }).State == CoverageState.Unsupported);
    Check($"{rule.Id} wrong type", SyntheticRuleEvaluator.Evaluate(rule, evidence with { ObjectType = "OtherType" }).State == CoverageState.NotApplicable);
    Check($"{rule.Id} wrong module", SyntheticRuleEvaluator.Evaluate(rule, evidence with { ModuleId = "OtherModule" }).State == CoverageState.NotApplicable);
    Check($"{rule.Id} conflicting", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [new(rule.RequiredFact, SyntheticFactAvailability.Conflicting, null)] }).State == CoverageState.InsufficientEvidence);
    Check($"{rule.Id} duplicate fact", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [positive.ObservedFact!, positive.ObservedFact!] }).ReasonCode == "SYNTHETIC-FACT-CONFLICT");
    Check($"{rule.Id} redacted", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [new(rule.RequiredFact, SyntheticFactAvailability.Redacted, null)] }).State == CoverageState.Redacted);
    Check($"{rule.Id} negative count", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [new(rule.RequiredFact, SyntheticFactAvailability.Known, -1)] }).State == CoverageState.InsufficientEvidence);
    Check($"{rule.Id} null known count", SyntheticRuleEvaluator.Evaluate(rule, evidence with { Facts = [new(rule.RequiredFact, SyntheticFactAvailability.Known, null)] }).State == CoverageState.InsufficientEvidence);
    Check($"{rule.Id} complete catalog metadata", rule.Weight > 0 && rule.Threshold == 0 &&
        !string.IsNullOrEmpty(rule.Purpose) && !string.IsNullOrEmpty(rule.Risk) && !string.IsNullOrEmpty(rule.Applicability) &&
        !string.IsNullOrEmpty(rule.ResultBehavior) && !string.IsNullOrEmpty(rule.GuidanceReference) &&
        rule.Options.Length == 2 && rule.ValidationGuidance.Length == 2 && rule.KnownFalsePositiveConditions.Length > 0);
}

var scope = SyntheticAnalysisFixturePack.Scope;
const string profileId = "synthetic-analysis-equal-v1";
var frozen = SyntheticAnalysisFixturePack.Freeze(scope, "synthetic-analysis-findings-v1", profileId);
var plan = SyntheticAnalysisEngine.Plan(frozen);
Check("ten unique rule/object keys", plan.Units.Length == 10 && plan.ExpectedKeys.Distinct().Count() == 10);
Check("unit catalog and object mapping", plan.Units.All(unit => unit.Key.InventoryId == unit.ObjectId && unit.Key.EvidenceCategory == unit.RuleId && unit.OutcomeIds.IsEmpty && unit.Weight == 1m));
Check("complete actual predicate results", plan.ExpectedResults.All(item => item.State == CoverageState.Finding));
Check("result digest full metadata", plan.ResultDigest == SyntheticCanonicalDigest.Compute(plan.Results));
var response = SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults);
Check("complete analysis", response.Succeeded && response.Analysis!.Findings.Length == 10);
var analysis = response.Analysis!;
Check("grouping retains per object occurrences", analysis.Groups.Length == 5 && analysis.Groups.All(group => group.OccurrenceIds.Length == 2 && group.ObjectIds.Distinct().Count() == 2));
Check("occurrence identities unique", analysis.Findings.Select(finding => finding.OccurrenceId).Distinct().Count() == 10);
Check("critical high remain proposed", analysis.Findings.Where(finding => finding.Severity is SyntheticSeverity.Critical or SyntheticSeverity.High).All(finding => finding.InitialDisposition == SyntheticInitialDisposition.Proposed));
Check("catalog lower explicitly auto confirmed", analysis.Findings.Where(finding => finding.Severity is not (SyntheticSeverity.Critical or SyntheticSeverity.High)).All(finding => finding.InitialDisposition == SyntheticInitialDisposition.AutoConfirmed));
Check("severity separate deterministic confidence", analysis.Findings.All(finding => finding.ConfidencePercent == 100m && finding.ConfidenceBand == "Deterministic synthetic evidence" && finding.DetectionMethod == "Deterministic"));
foreach (var finding in analysis.Findings)
{
    Check($"{finding.OccurrenceId} original provenance complete", finding.Provenance.PackDigest == frozen.PackDigest && finding.Provenance.CatalogDigest == frozen.CatalogDigest &&
        finding.Provenance.BaselineId == frozen.PresetId && finding.Provenance.EvidenceDigest == frozen.EvidenceDigest &&
        finding.Provenance.RuleVersion == SyntheticAnalysisFixturePack.RuleVersion && finding.Provenance.Scope == scope &&
        finding.Facts.Length == 1 && finding.Facts[0].Count == 1 && finding.Facts[0].EvidenceReference == finding.Provenance.EvidenceReference &&
        !string.IsNullOrEmpty(finding.Inference) && finding.Assumptions.Length == 2 && !string.IsNullOrEmpty(finding.Impact) &&
        !string.IsNullOrEmpty(finding.RootCause) && finding.RecommendationOptions.Length == 2 && finding.ValidationGuidance.Length == 2);
    Check($"{finding.OccurrenceId} original digest", finding.GeneratedOriginalDigest == SyntheticCanonicalDigest.Compute(finding with { GeneratedOriginalDigest = "" }));
}
Check("empty run ID denied", SyntheticAnalysisEngine.Analyze(frozen, Guid.Empty, plan.ExpectedResults).Issue == SyntheticAnalysisIssue.InvalidInput);
var otherRun = SyntheticAnalysisEngine.Analyze(frozen, Guid.Parse("350f003e-96ab-49ea-9a0b-fd2b10cc14eb"), plan.ExpectedResults).Analysis!;
Check("occurrences run bound", !otherRun.Findings.Select(finding => finding.OccurrenceId).Intersect(analysis.Findings.Select(finding => finding.OccurrenceId)).Any());
Check("root causes stable across runs", otherRun.Groups.Select(group => group.RootCauseKey).SequenceEqual(analysis.Groups.Select(group => group.RootCauseKey)));
Check("original provenance run bound", analysis.RunId == runId && analysis.Findings.All(finding => finding.Provenance.RunId == runId));
Check("content digest", analysis.ContentDigest == SyntheticCanonicalDigest.Compute(analysis with { ContentDigest = "" }));
Check("repeat reproducible", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults).Analysis!.ContentDigest == analysis.ContentDigest);
Check("saved result reorder canonical", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults.Reverse().ToArray()).Analysis!.ContentDigest == analysis.ContentDigest);
var mutableResults = plan.ExpectedResults.ToArray();
var saved = SyntheticAnalysisEngine.Analyze(frozen, runId, mutableResults).Analysis!;
mutableResults[0] = mutableResults[0] with { State = CoverageState.Pass };
Check("generated original survives caller mutation", saved.ContentDigest == analysis.ContentDigest && saved.Findings.Length == 10);
Check("edited saved result denied", SyntheticAnalysisEngine.Analyze(frozen, runId, mutableResults).Issue == SyntheticAnalysisIssue.ResultMismatch);
Check("missing result denied", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults.Skip(1).ToArray()).Issue == SyntheticAnalysisIssue.IncompleteCoverage);
Check("duplicate result denied", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults.Add(plan.ExpectedResults[0])).Issue == SyntheticAnalysisIssue.IncompleteCoverage);
Check("unknown result denied", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults.Add(new(new("unknown", "unknown"), CoverageState.Pass))).Issue == SyntheticAnalysisIssue.IncompleteCoverage);
Check("null results denied", SyntheticAnalysisEngine.Analyze(frozen, runId, null).Issue == SyntheticAnalysisIssue.InvalidInput);
Check("null lock denied", SyntheticAnalysisEngine.Analyze(null, runId, plan.ExpectedResults).Issue == SyntheticAnalysisIssue.InvalidInput);
foreach (var change in new[]
{
    plan.ExpectedResults[0] with { EvidenceReference = "fixture-evidence:wrong" },
    plan.ExpectedResults[0] with { ReasonCode = "fabricated" },
    plan.ExpectedResults[0] with { ResponsibleStage = "fabricated" }
})
{
    Check("all saved coverage facts bound", SyntheticAnalysisEngine.Analyze(frozen, runId, plan.ExpectedResults.SetItem(0, change)).Issue == SyntheticAnalysisIssue.ResultMismatch);
}
foreach (var tampered in new[]
{
    frozen with { PackVersion = "other" }, frozen with { PackDigest = new string('0', 64) },
    frozen with { PresetVersion = "other" }, frozen with { EvidenceDigest = new string('1', 64) },
    frozen with { CatalogVersion = "other" }, frozen with { CatalogDigest = new string('2', 64) },
    frozen with { ProfileVersion = "other" }, frozen with { ProfileDigest = new string('3', 64) },
    frozen with { Compatibility = frozen.Compatibility with { SourceProduct = "OneIdentity" } }
})
{
    var denied = SyntheticAnalysisEngine.Analyze(tampered, runId, plan.ExpectedResults);
    Check("exact frozen lock mismatch denied", denied.Issue == SyntheticAnalysisIssue.FrozenInputMismatch && denied.Analysis is null);
}
Check("scope denied", SyntheticAnalysisEngine.Analyze(frozen with { Scope = scope with { CustomerId = "other" } }, runId, plan.ExpectedResults).Issue == SyntheticAnalysisIssue.WrongScope);
Check("unknown preset denied", SyntheticAnalysisEngine.Analyze(frozen with { PresetId = "other" }, runId, plan.ExpectedResults).Issue == SyntheticAnalysisIssue.UnknownPreset);
Check("unknown profile denied", SyntheticAnalysisEngine.Analyze(frozen with { ProfileId = "other" }, runId, plan.ExpectedResults).Issue == SyntheticAnalysisIssue.UnknownProfile);
Check("profile selection affects content", SyntheticAnalysisEngine.Analyze(SyntheticAnalysisFixturePack.Freeze(scope, frozen.PresetId, "synthetic-analysis-operations-v1"), runId, plan.ExpectedResults).Analysis!.ContentDigest != analysis.ContentDigest);

foreach (var preset in SyntheticAnalysisFixturePack.Presets)
{
    var fixtureLock = SyntheticAnalysisFixturePack.Freeze(scope, preset.Id, profileId);
    var fixturePlan = SyntheticAnalysisEngine.Plan(fixtureLock);
    var actual = SyntheticAnalysisEngine.Analyze(fixtureLock, runId, fixturePlan.ExpectedResults).Analysis!;
    var states = fixturePlan.ExpectedResults.GroupBy(item => item.State).ToDictionary(group => group.Key, group => group.Count());
    var expected = preset.Id switch
    {
        "synthetic-analysis-healthy-v1" => new Dictionary<CoverageState, int> { [CoverageState.Pass] = 10 },
        "synthetic-analysis-findings-v1" => new Dictionary<CoverageState, int> { [CoverageState.Finding] = 10 },
        "synthetic-analysis-gaps-v1" => new Dictionary<CoverageState, int> { [CoverageState.InsufficientEvidence] = 5, [CoverageState.Excluded] = 5 },
        _ => new Dictionary<CoverageState, int> { [CoverageState.Finding] = 5, [CoverageState.Pass] = 2, [CoverageState.InsufficientEvidence] = 3 }
    };
    Check($"{preset.Id} independently expected states", expected.Count == states.Count && expected.All(pair => states.GetValueOrDefault(pair.Key) == pair.Value));
    Check($"{preset.Id} immutable evidence digest", fixtureLock.EvidenceDigest == SyntheticCanonicalDigest.Compute(preset));
    Check($"{preset.Id} gaps explained", fixturePlan.ExpectedResults.Where(item => item.State is not (CoverageState.Pass or CoverageState.Finding)).All(item => item.ReasonCode is not null && item.ResponsibleStage is not null && item.EvidenceReference is not null));
    Check($"{preset.Id} findings only from actual finding", actual.Findings.Length == states.GetValueOrDefault(CoverageState.Finding));
    var changedEvidence = preset with { Objects = preset.Objects.SetItem(0, preset.Objects[0] with { EvidenceReference = "fixture-evidence:changed" }) };
    Check($"{preset.Id} evidence metadata content bound", SyntheticCanonicalDigest.Compute(changedEvidence) != fixtureLock.EvidenceDigest);
}
var ruleChange = SyntheticAnalysisFixturePack.Rules.SetItem(0, SyntheticAnalysisFixturePack.Rules[0] with { Impact = "changed synthetic impact" });
Check("catalog metadata content bound", SyntheticCanonicalDigest.Compute(new { CatalogVersion = SyntheticAnalysisFixturePack.CatalogVersion, Rules = ruleChange }) != SyntheticAnalysisFixturePack.CatalogDigest);
foreach (var profile in SyntheticAnalysisFixturePack.Profiles)
{
    Check("positive fixed profile weights", profile.CategoryWeights.All(weight => weight.Weight > 0) && profile.CategoryWeights.Sum(weight => weight.Weight) == 1m);
    Check("profile metadata content bound", SyntheticCanonicalDigest.Compute(profile with { Label = "changed label" }) != SyntheticCanonicalDigest.Compute(profile));
}
Console.WriteLine($"{checks} deterministic synthetic analysis checks passed.");
Console.WriteLine($"Fixture pack SHA256: {SyntheticAnalysisFixturePack.PackDigest}");

void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    checks++;
}
