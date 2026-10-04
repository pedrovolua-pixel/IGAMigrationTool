using System.Text.Json;
using AssessmentCoverage;
using AssessmentScoring;

internal static class Program
{
    private static int assertions;

    private static async Task Main(string[] args)
    {
        ScoringGoldens();
        await AnalysisAndStoreCases.Run(args);
        Console.WriteLine($"{assertions} independent analysis/scoring assertions passed. Synthetic fixtures only; production rules, AI, review grants and publication NOT VERIFIED.");
    }

    private static void ScoringGoldens()
    {
        foreach (var (severity, expected) in new[]
        {
            (ScoringSeverity.Critical, 0m), (ScoringSeverity.High, 20m), (ScoringSeverity.Medium, 55m),
            (ScoringSeverity.Low, 80m), (ScoringSeverity.Informational, 100m)
        })
        {
            var projection = Accept(ScoringFixture.Input(ScoringFixture.Finding("one", severity)));
            Score(projection.Provisional.Overall, expected, $"literal {severity} provisional");
            Score(projection.PublishableCurrent.Overall, expected, $"literal {severity} publishable");
        }
        Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.High, confidence: 2m))).Provisional.Overall,
            20m, "deterministic confidence cannot discount severity");
        Group("AS-GOLD-001 literal severity factors");

        Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.High,
            method: ScoringDetectionMethod.AI, confidence: 70m))).PublishableCurrent.Overall, 44m, "literal confirmed AI70 High");
        foreach (var confidence in new[] { 0m, 49m, 50m })
            Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Critical,
                method: ScoringDetectionMethod.AI, confidence: confidence))).Provisional.Overall, 50m, "AI minimum clamp");
        Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Critical,
            method: ScoringDetectionMethod.AI))).Provisional.Overall, 0m, "AI maximum clamp");
        foreach (var invalid in new[] { -1m, 101m })
            Deny(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.High, method: ScoringDetectionMethod.AI,
                confidence: invalid)), ScoringIssue.InvalidUnit, "invalid percentage refuses rather than silently alter input");
        Group("AS-GOLD-002 AI confidence pure fixtures");

        foreach (var severity in new[] { ScoringSeverity.Critical, ScoringSeverity.High })
        {
            var proposed = Accept(ScoringFixture.Input(ScoringFixture.Finding("one", severity, ScoringFindingState.Proposed)));
            Score(proposed.Provisional.Overall, severity == ScoringSeverity.Critical ? 0m : 20m, "mandatory review proposal provisional");
            Unavailable(proposed.PublishableCurrent.Overall, "unreviewed mandatory finding omitted from publishable");
            Require(proposed.Quality is { MandatoryReviewFindings: 1, UnreviewedMandatoryFindings: 1 }, "review requirement remains explicit");
            Deny(ScoringFixture.Input(ScoringFixture.Finding("one", severity, ScoringFindingState.AutoConfirmed)),
                ScoringIssue.InvalidReviewGate, "Critical/High cannot auto-confirm");
        }
        var ai = Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Low, ScoringFindingState.Proposed, method: ScoringDetectionMethod.AI)));
        Score(ai.Provisional.Overall, 80m, "proposed AI provisional");
        Unavailable(ai.PublishableCurrent.Overall, "proposed AI never publishable");
        Deny(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Low, ScoringFindingState.AutoConfirmed, method: ScoringDetectionMethod.AI)),
            ScoringIssue.InvalidReviewGate, "AI cannot auto-confirm");
        Group("AS-GOLD-003 mandatory review and provisional separation");

        foreach (var state in new[] { ScoringFindingState.AutoConfirmed, ScoringFindingState.Confirmed, ScoringFindingState.Deferred,
            ScoringFindingState.AcceptedRisk, ScoringFindingState.RemediationPlanned, ScoringFindingState.InProgress,
            ScoringFindingState.RemediatedPendingValidation, ScoringFindingState.Reopened })
            Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Medium, state))).PublishableCurrent.Overall,
                55m, $"{state} retains confirmed penalty");
        Score(Accept(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Medium, ScoringFindingState.Rejected))).PublishableCurrent.Overall,
            100m, "rejected finding earns full weight");
        Deny(ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Medium, ScoringFindingState.ValidatedClosed)),
            ScoringIssue.InvalidValidatedClosure, "disposition cannot erase finding without new pass");
        var validated = ScoringFixture.Pass("one") with
        {
            FindingState = ScoringFindingState.ValidatedClosed,
            HasPassingValidationEvidence = true,
            PassingValidationEvidenceId = "synthetic-new-passing-evidence-v2"
        };
        Score(Accept(ScoringFixture.Input(validated)).PublishableCurrent.Overall, 100m, "explicit new passing validation evidence earns full weight");
        Deny(ScoringFixture.Input(validated with { PassingValidationEvidenceId = null }), ScoringIssue.InvalidValidatedClosure, "passing validation requires opaque evidence reference");
        Group("AS-GOLD-004 lifecycle penalty preservation and new pass");

        var categories = ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.High, category: "a"), ScoringFixture.Pass("two", "b"));
        categories = categories with { Profile = categories.Profile with { Categories = [new("a", 1m), new("b", 1m), new("c", 1m)] } };
        var equal = Accept(categories);
        Score(equal.Provisional.Overall, 60m, "equal assessed categories20+100 mean60");
        Unavailable(equal.Provisional.Categories.Single(row => row.Id == "c").Score, "unassessed category stays unavailable");
        var weighted = categories with
        {
            Profile = categories.Profile with
            {
                WeightMode = ScoringWeightMode.ExplicitCategoryWeights,
                Categories = [new("a", .6m), new("b", .2m), new("c", .2m)]
            }
        };
        Score(Accept(weighted).Provisional.Overall, 40m, "explicit weight renormalization excludes unassessed c");
        Deny(weighted with { Profile = weighted.Profile with { Categories = [new("a", .6m), new("b", .2m), new("c", .1m)] } },
            ScoringIssue.InvalidProfile, "invalid sum refuses");
        Deny(weighted with { Profile = weighted.Profile with { Categories = [new("a", 1m), new("b", 0m), new("c", 0m)] } },
            ScoringIssue.InvalidProfile, "zero weights refuse");
        Group("AS-GOLD-005 assessed category weights and renormalization");

        Score(Accept(ScoringFixture.Input(ScoringFixture.Pass("pass"), ScoringFixture.Finding("first", ScoringSeverity.Medium))).Provisional.Overall,
            77.5m, "one Medium occurrence plus pass");
        var repeated = Accept(ScoringFixture.Input(ScoringFixture.Pass("pass"), ScoringFixture.Finding("first", ScoringSeverity.Medium),
            ScoringFixture.Finding("second", ScoringSeverity.Medium)));
        Score(repeated.Provisional.Overall, 70m, "two separate Medium occurrences plus pass stay linear");
        var multipleOutcomes = ScoringFixture.Input(ScoringFixture.Finding("one", ScoringSeverity.Medium) with { OutcomeIds = ["approved-a", "approved-b", "draft"] });
        multipleOutcomes = multipleOutcomes with { Outcomes = [new("approved-a", true), new("approved-b", true), new("draft", false)] };
        var outcomes = Accept(multipleOutcomes);
        Score(outcomes.Provisional.Overall, 55m, "multiple outcome links do not duplicate overall unit");
        Require(outcomes.Provisional.Overall.ScoredUnits == 1 && outcomes.Provisional.ApprovedOutcomes.Count == 2 &&
            outcomes.Provisional.ApprovedOutcomes.All(row => row.Score.RawScore == 55m), "only approved outcomes score and retain same single unit");
        Group("AS-GOLD-006 per-object linearity and no outcome duplication");

        var gaps = new[] { CoverageState.NotAssessed, CoverageState.InsufficientEvidence, CoverageState.Excluded,
            CoverageState.Inaccessible, CoverageState.Redacted, CoverageState.Unsupported, CoverageState.Error, CoverageState.NotApplicable };
        var withGaps = Accept(ScoringFixture.Input([ScoringFixture.Pass("pass"), .. gaps.Select((state, index) => ScoringFixture.Gap($"gap-{index}", state))]));
        Score(withGaps.Provisional.Overall, 100m, "explained gaps neither improve nor reduce health");
        Require(withGaps.Quality is { ExpectedKeys: 9, RepresentedKeys: 9, ExecutableCoverage: { ExecutedUnits: 1, ApplicablePlannedUnits: 8 } },
            "independent9terminal keys and1of8executable quality");
        Require(withGaps.Quality.Limitations.Count == 7 && withGaps.Quality.RuleExecutionFailures == 1, "all seven gap states retain limitations and error count");
        Unavailable(Accept(ScoringFixture.Input(ScoringFixture.Gap("gap", CoverageState.Unsupported))).PublishableCurrent.Overall, "all gaps unavailable health");
        var empty = ScoringFixture.Input();
        empty = empty with { Profile = empty.Profile with { Categories = [new("a", 1m)] } };
        Unavailable(Accept(empty).Provisional.Overall, "empty eligible score set unavailable health");
        Group("AS-GOLD-007 gap/quality separation and unavailable empty score");

        var fifty = Accept(ScoringFixture.Input(ScoringFixture.Pass("pass"), ScoringFixture.Finding("critical", ScoringSeverity.Critical))).Provisional.Overall;
        Score(fifty, 50m, "exact50 boundary"); Require(fifty.Status == HealthStatus.Yellow, "50 is yellow");
        var nearFifty = Accept(ScoringFixture.Input(ScoringFixture.Pass("pass", weight: 5000m), ScoringFixture.Finding("critical", ScoringSeverity.Critical, weight: 5001m))).Provisional.Overall;
        Require(nearFifty.RawScore is > 49.99m and < 50m && nearFifty.DisplayScore == 50m && nearFifty.Status == HealthStatus.Red, "rounded50 remains raw-below50 red");
        var eighty = Accept(ScoringFixture.Input(ScoringFixture.Pass("pass", weight: 3m), ScoringFixture.Finding("high", ScoringSeverity.High))).Provisional.Overall;
        Score(eighty, 80m, "exact80 boundary"); Require(eighty.Status == HealthStatus.Green, "80 is green");
        var nearEighty = Accept(ScoringFixture.Input(ScoringFixture.Pass("pass", weight: 15000m), ScoringFixture.Finding("high", ScoringSeverity.High, weight: 5001m))).Provisional.Overall;
        Require(nearEighty.RawScore is > 79.99m and < 80m && nearEighty.DisplayScore == 80m && nearEighty.Status == HealthStatus.Yellow, "rounded80 remains raw-below80 yellow");
        Group("AS-GOLD-008 raw status boundaries independent of display");

        var valid = ScoringFixture.Input(ScoringFixture.Pass("pass"), ScoringFixture.Finding("medium", ScoringSeverity.Medium));
        Deny(valid with { Coverage = valid.Coverage.Take(1).ToArray() }, ScoringIssue.InvalidCoverage, "missing terminal coverage refuses score");
        Deny(valid with { Units = valid.Units.Take(1).ToArray() }, ScoringIssue.InvalidCoverage, "missing scoring unit refuses score");
        Deny(valid with { Units = [.. valid.Units, valid.Units.First()] }, ScoringIssue.DuplicateUnit, "duplicate scoring key refuses score");
        Deny(valid with { Units = [valid.Units.First() with { CoverageState = CoverageState.Finding }, valid.Units.Last()] },
            ScoringIssue.CoverageStateMismatch, "unit state mismatch refuses score");
        Deny(valid with { Versions = valid.Versions with { AlgorithmVersion = "unknown" } }, ScoringIssue.UnknownVersion, "unknown algorithm refuses");
        Deny(valid with { Versions = valid.Versions with { ProfileVersion = "changed" } }, ScoringIssue.VersionMismatch, "profile lock mismatch refuses");
        Deny(valid with { Units = [valid.Units.First() with { CatalogWeight = -1m }, valid.Units.Last()] }, ScoringIssue.InvalidUnit, "negative catalog weight refuses");
        Deny(valid with { Units = [valid.Units.First() with { CategoryId = "unknown" }, valid.Units.Last()] }, ScoringIssue.UnknownCategory, "undeclared category refuses");
        Deny(valid with { Units = [valid.Units.First() with { OutcomeIds = ["unknown"] }, valid.Units.Last()] }, ScoringIssue.UnknownOutcome, "undeclared outcome refuses");
        Deny(ScoringFixture.Input(ScoringFixture.Pass("a", weight: decimal.MaxValue), ScoringFixture.Pass("b", weight: decimal.MaxValue)),
            ScoringIssue.NumericOverflow, "large valid decimals fail closed on overflow");
        Group("AS-GOLD-009 adversarial reconciliation/version/numeric inputs");

        var immutable = Accept(valid);
        var digest = immutable.ContentDigest;
        var encoded = JsonSerializer.Serialize(immutable);
        var reordered = Accept(valid with { ExpectedKeys = valid.ExpectedKeys.Reverse().ToArray(), Coverage = valid.Coverage.Reverse().ToArray(), Units = valid.Units.Reverse().ToArray() });
        Require(reordered.ContentDigest == digest && JsonSerializer.Serialize(reordered) == encoded, "input order canonicalizes identical content");
        ((ScoringUnit[])valid.Units)[0] = ScoringFixture.Finding("replacement", ScoringSeverity.Critical);
        Require(immutable.ContentDigest == digest && JsonSerializer.Serialize(immutable) == encoded, "caller array mutation cannot rewrite frozen score projection");
        Require(Accept(ScoringFixture.Input(ScoringFixture.Pass("pass"), ScoringFixture.Finding("medium", ScoringSeverity.Low))).ContentDigest != digest,
            "material result/severity change changes content digest");
        Group("AS-GOLD-010 immutable canonical projection and digest");
        var scale = Enumerable.Range(0, 100_000).Select(index => index < 50_000
            ? ScoringFixture.Finding($"independent-scale-{index:D6}", ScoringSeverity.Critical, ScoringFindingState.Proposed)
            : ScoringFixture.Pass($"independent-scale-{index:D6}")).ToArray();
        var scaled = Accept(ScoringFixture.Input(scale));
        Score(scaled.Provisional.Overall, 50m, "independent100000 units half proposed Critical half pass provisional50");
        Score(scaled.PublishableCurrent.Overall, 100m, "50000 mandatory proposals omitted from publishable health");
        Require(scaled.Quality.ExpectedKeys == 100_000 && scaled.Quality.UnreviewedMandatoryFindings == 50_000 &&
            scaled.Provisional.Overall.ScoredUnits == 100_000 && scaled.PublishableCurrent.Overall.ScoredUnits == 50_000,
            "independent generator retains scale/unit/review counts without grouped collapse");
        Group("AS-GOLD-011 independently generated100000 keys literal50/100 and review counts");

    }

    internal static ScoringProjection Accept(ScoringInput input)
    {
        var result = PilotHealthScorer.Project(input);
        Require(result.HasProjection, $"scoring accepts valid fixture, actual issue {result.Issue}");
        return result.Projection!;
    }

    internal static void Deny(ScoringInput input, ScoringIssue expected, string name)
    {
        var result = PilotHealthScorer.Project(input);
        Require(!result.HasProjection && result.Projection is null && result.Issue == expected, $"{name}, expected {expected}, actual {result.Issue}");
    }

    internal static void Score(HealthMeasure actual, decimal expected, string name) =>
        Require(actual.IsAvailable && actual.RawScore == expected, $"{name}, expected {expected}, actual {actual.RawScore}");
    internal static void Unavailable(HealthMeasure actual, string name) =>
        Require(!actual.IsAvailable && actual.RawScore is null && actual.DisplayScore is null && actual.Status is null && actual.ScoredUnits == 0, name);
    internal static void Require(bool value, string name)
    {
        if (!value) throw new Exception($"Independent V4 verification failed: {name}");
        assertions++;
    }
    internal static void Group(string name) => Console.WriteLine($"PASS {name}");
}
