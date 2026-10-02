using AssessmentScoring;
using AssessmentCoverage;

var passed = 0;
var versions = new ScoringVersions("pilot-health-v1", "synthetic-scoring-input-v1", "synthetic-baseline-v1",
    "synthetic-catalog-v1", "synthetic-profile-v1", new string('a', 64), new string('b', 64));
var equal = new ScoringProfile("synthetic-profile-v1", ScoringWeightMode.EqualAssessedCategories,
    [new("SECURITY", .5m), new("OPERATIONS", .5m)]);
var pass = Unit("pass");
var finding = Unit("finding", CoverageState.Finding, ScoringSeverity.Medium, ScoringFindingState.Confirmed);
var baseline = Input([pass, finding]);

foreach (var (severity, expected) in new[]
{
    (ScoringSeverity.Critical, 0m), (ScoringSeverity.High, 20m), (ScoringSeverity.Medium, 55m),
    (ScoringSeverity.Low, 80m), (ScoringSeverity.Informational, 100m)
})
{
    Check($"SYN-S4 severity {severity}", () =>
    {
        var projection = Project(Input([finding with { Severity = severity }]));
        Score(projection.Provisional.Overall, expected);
        Score(projection.PublishableCurrent.Overall, expected);
    });
}
foreach (var state in new[]
{
    ScoringFindingState.Confirmed, ScoringFindingState.Deferred, ScoringFindingState.AcceptedRisk,
    ScoringFindingState.RemediationPlanned, ScoringFindingState.InProgress,
    ScoringFindingState.RemediatedPendingValidation, ScoringFindingState.Reopened
})
{
    Check($"SYN-S4 retained deterministic {state}", () =>
        Score(Project(Input([finding with { Severity = ScoringSeverity.High, FindingState = state }])).PublishableCurrent.Overall, 20m));
    Check($"SYN-S4 retained AI {state}", () =>
        Score(Project(Input([finding with { Severity = ScoringSeverity.High, FindingState = state,
            Method = ScoringDetectionMethod.AI, ConfidencePercent = 75m }])).PublishableCurrent.Overall, 40m));
}
foreach (var (confidence, expected) in new[] { (0m, 60m), (25m, 60m), (50m, 60m), (75m, 40m), (100m, 20m) })
    Check($"SYN-S4 AI clamp {confidence}", () => Score(Project(Input([finding with
        { Severity = ScoringSeverity.High, Method = ScoringDetectionMethod.AI, ConfidencePercent = confidence }])).PublishableCurrent.Overall, expected));
Check("SYN-S4 deterministic confidence remains one", () => Score(Project(Input([finding with { ConfidencePercent = 0 }])).PublishableCurrent.Overall, 55m));
Check("SYN-S4 rejected findings retain execution but earn full weight", () =>
{
    var projection = Project(Input([finding with { Severity = ScoringSeverity.Critical, FindingState = ScoringFindingState.Rejected }]));
    Score(projection.PublishableCurrent.Overall, 100m);
    Require(projection.Quality.CoverageCounts.Single(item => item.State == CoverageState.Finding).Count == 1);
});
Check("SYN-S4 explicit lower-severity auto confirmation", () =>
    Score(Project(Input([finding with { FindingState = ScoringFindingState.AutoConfirmed }])).PublishableCurrent.Overall, 55m));
foreach (var method in Enum.GetValues<ScoringDetectionMethod>())
    Check($"SYN-S4 proposals remain provisional {method}", () =>
    {
        var projection = Project(Input([finding with { Severity = ScoringSeverity.High, FindingState = ScoringFindingState.Proposed, Method = method },
            pass with { CategoryId = "OPERATIONS" }]));
        Score(projection.Provisional.Overall, 60m);
        Score(projection.PublishableCurrent.Overall, 100m);
        Require(!projection.PublishableCurrent.Categories.Single(item => item.Id == "SECURITY").Score.IsAvailable);
        Require(projection.Quality.ProposedFindings == 1 && projection.Quality.UnreviewedMandatoryFindings == 1);
    });
Check("SYN-S4 medium deterministic proposals are not silently auto confirmed", () =>
    Require(!Project(Input([finding with { FindingState = ScoringFindingState.Proposed }])).PublishableCurrent.Overall.IsAvailable));
Check("SYN-S4 validated closure requires a passing result and explicit reference", () =>
    Score(Project(Input([pass with { Severity = ScoringSeverity.High, FindingState = ScoringFindingState.ValidatedClosed,
        HasPassingValidationEvidence = true, PassingValidationEvidenceId = "synthetic-new-pass-reference" }])).PublishableCurrent.Overall, 100m));

foreach (var gap in Enum.GetValues<CoverageState>().Where(state => state is not (CoverageState.Pass or CoverageState.Finding)))
    Check($"SYN-S4 unavailable health and separate quality {gap}", () =>
    {
        var projection = Project(Input([Unit("gap", gap)]));
        Require(!projection.Provisional.Overall.IsAvailable && !projection.PublishableCurrent.Overall.IsAvailable);
        Require(projection.Provisional.Overall.RawScore is null && projection.Provisional.Overall.DisplayScore is null && projection.Provisional.Overall.Status is null);
        Require(projection.Quality.ExpectedKeys == 1 && projection.Quality.RepresentedKeys == 1);
        Require(projection.Quality.ExecutableCoverage == new ExecutableCoverageMeasure(0, gap == CoverageState.NotApplicable ? 0 : 1));
        Require(projection.Quality.Limitations.Count == (gap == CoverageState.NotApplicable ? 0 : 1));
        Require(projection.Quality.DeterministicUnits == 0);
        var mixed = Project(Input([pass, Unit("gap", gap)]));
        Score(mixed.PublishableCurrent.Overall, 100m);
    });
Check("SYN-S4 empty input is unavailable", () =>
{
    var projection = Project(Input([]));
    Require(!projection.Provisional.Overall.IsAvailable && !projection.Quality.ExecutableCoverage.HasApplicableUnits);
    Require(projection.Provisional.Categories.Count == 2 && projection.Provisional.Categories.All(item => !item.Score.IsAvailable));
});
Check("SYN-S4 weighted catalog units", () =>
    Score(Project(Input([pass with { CatalogWeight = 3m }, finding with { Severity = ScoringSeverity.Critical }])).PublishableCurrent.Overall, 75m));
Check("SYN-S4 equal categories do not average all units together", () =>
{
    var units = Enumerable.Range(0, 9).Select(index => Unit($"pass-{index}")).Append(
        Unit("critical", CoverageState.Finding, ScoringSeverity.Critical, ScoringFindingState.Confirmed, "OPERATIONS")).ToArray();
    var projection = Project(Input(units));
    Score(projection.PublishableCurrent.Overall, 50m);
    var weighted = equal with
    {
        WeightMode = ScoringWeightMode.ExplicitCategoryWeights,
        Categories = [new("SECURITY", .25m), new("OPERATIONS", .75m)]
    };
    Score(Project(Input(units, weighted)).PublishableCurrent.Overall, 25m);
});
Check("SYN-S4 unassessed-category weights renormalize", () =>
{
    var profile = equal with
    {
        WeightMode = ScoringWeightMode.ExplicitCategoryWeights,
        Categories = [new("SECURITY", .25m), new("OPERATIONS", .75m)]
    };
    Score(Project(Input([finding], profile)).PublishableCurrent.Overall, 55m);
});
Check("SYN-S4 multidimensional views and approved outcomes do not duplicate overall", () =>
{
    var units = new[]
    {
        pass with { ObjectType = "type-a", ModuleId = "QBM", OutcomeIds = new[] { "approved-a", "approved-a", "advisory" } },
        finding with { Severity = ScoringSeverity.Critical, ObjectType = "type-b", ModuleId = "CCC", OutcomeIds = new[] { "approved-a", "approved-b" } }
    };
    var projection = Project(Input(units, outcomes: [new("approved-a", true), new("approved-b", true), new("approved-empty", true), new("advisory", false)]));
    Score(projection.PublishableCurrent.Overall, 50m);
    Require(projection.PublishableCurrent.Overall.ScoredUnits == 2 && projection.Units[1].OutcomeIds.Count == 2);
    Score(projection.PublishableCurrent.ObjectTypes.Single(item => item.Id == "type-a").Score, 100m);
    Score(projection.PublishableCurrent.Modules.Single(item => item.Id == "CCC").Score, 0m);
    Score(projection.PublishableCurrent.ApprovedOutcomes.Single(item => item.Id == "approved-a").Score, 50m);
    Score(projection.PublishableCurrent.ApprovedOutcomes.Single(item => item.Id == "approved-b").Score, 0m);
    Require(!projection.PublishableCurrent.ApprovedOutcomes.Single(item => item.Id == "approved-empty").Score.IsAvailable);
    Require(projection.PublishableCurrent.ApprovedOutcomes.All(item => item.Id != "advisory"));
});
Check("SYN-S4 per-object penalty grows linearly", () =>
{
    var results = new List<decimal>();
    for (var criticalCount = 0; criticalCount <= 5; criticalCount++)
    {
        var units = Enumerable.Range(0, 10).Select(index => index < criticalCount
            ? Unit($"subject-{index}", CoverageState.Finding, ScoringSeverity.Critical, ScoringFindingState.Confirmed)
            : Unit($"subject-{index}")).ToArray();
        var projection = Project(Input(units));
        results.Add(projection.PublishableCurrent.Overall.RawScore!.Value);
        Require(projection.PublishableCurrent.Overall.ScoredUnits == 10);
    }
    Require(results.SequenceEqual(new[] { 100m, 90m, 80m, 70m, 60m, 50m }));
});
Check("SYN-S4 unrounded ratio and display-only rounding", () =>
{
    var projection = Project(Input([pass, finding with { Severity = ScoringSeverity.Critical },
        finding with { Key = new("finding-two", "synthetic-rule"), Severity = ScoringSeverity.Critical }]));
    Require(projection.PublishableCurrent.Overall.RawScore == 100m / 3m);
    Require(projection.PublishableCurrent.Overall.DisplayScore == 33.3m);
    var tie = Project(Input([pass with { CatalogWeight = 79.95m },
        finding with { Severity = ScoringSeverity.Critical, CatalogWeight = 20.05m }]));
    Require(tie.PublishableCurrent.Overall.RawScore == 79.95m && tie.PublishableCurrent.Overall.DisplayScore == 80m);
    Require(tie.PublishableCurrent.Overall.Status == HealthStatus.Yellow);
});
foreach (var (percentPass, status) in new[] { (49.999m, HealthStatus.Red), (50m, HealthStatus.Yellow), (79.999m, HealthStatus.Yellow), (80m, HealthStatus.Green) })
    Check($"SYN-S4 raw status boundary {percentPass}", () =>
    {
        var projection = Project(Input([pass with { CatalogWeight = percentPass },
            finding with { Severity = ScoringSeverity.Critical, CatalogWeight = 100m - percentPass }]));
        Require(projection.PublishableCurrent.Overall.RawScore == percentPass && projection.PublishableCurrent.Overall.Status == status);
    });

Denial("SYN-S4 null input", ScoringIssue.InvalidInput, null);
Denial("SYN-S4 malformed frozen digest", ScoringIssue.InvalidInput, baseline with { Versions = versions with { FrozenInputDigest = "unknown" } });
Denial("SYN-S4 unknown algorithm", ScoringIssue.UnknownVersion, baseline with { Versions = versions with { AlgorithmVersion = "pilot-health-v2" } });
Denial("SYN-S4 unknown input schema", ScoringIssue.UnknownVersion, baseline with { Versions = versions with { InputSchemaVersion = "synthetic-scoring-input-v2" } });
Denial("SYN-S4 profile version mismatch", ScoringIssue.VersionMismatch, baseline with { Profile = equal with { ProfileVersion = "changed" } });
Denial("SYN-S4 empty categories", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { Categories = [] } });
Denial("SYN-S4 unknown profile mode", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { WeightMode = (ScoringWeightMode)999 } });
Denial("SYN-S4 zero category weight", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { Categories = [new("SECURITY", 0m)] } });
Denial("SYN-S4 duplicate categories", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { Categories = [new("SECURITY", .5m), new("SECURITY", .5m)] } });
Denial("SYN-S4 unequal default weights", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { Categories = [new("SECURITY", .25m), new("OPERATIONS", .75m)] } });
Denial("SYN-S4 explicit weight sum", ScoringIssue.InvalidProfile, baseline with { Profile = equal with { WeightMode = ScoringWeightMode.ExplicitCategoryWeights, Categories = [new("SECURITY", .3m), new("OPERATIONS", .3m)] } });
Denial("SYN-S4 missing terminal result", ScoringIssue.InvalidCoverage, baseline with { Coverage = baseline.Coverage.Take(1).ToArray() });
Denial("SYN-S4 duplicate expected key", ScoringIssue.InvalidCoverage, baseline with { ExpectedKeys = [.. baseline.ExpectedKeys, pass.Key] });
Denial("SYN-S4 duplicate result", ScoringIssue.InvalidCoverage, baseline with { Coverage = [.. baseline.Coverage, baseline.Coverage.First()] });
Denial("SYN-S4 missing unit", ScoringIssue.InvalidCoverage, baseline with { Units = [pass] });
Denial("SYN-S4 duplicate unit", ScoringIssue.DuplicateUnit, baseline with { Units = [pass, pass, finding] });
Denial("SYN-S4 terminal state mismatch", ScoringIssue.CoverageStateMismatch, baseline with { Units = [pass with { CoverageState = CoverageState.Finding }, finding] });
UnitDenial("SYN-S4 unknown category", ScoringIssue.UnknownCategory, finding with { CategoryId = "unknown" });
UnitDenial("SYN-S4 unknown outcome", ScoringIssue.UnknownOutcome, finding with { OutcomeIds = new[] { "unknown" } });
UnitDenial("SYN-S4 zero catalog weight", ScoringIssue.InvalidUnit, finding with { CatalogWeight = 0m });
UnitDenial("SYN-S4 negative catalog weight", ScoringIssue.InvalidUnit, finding with { CatalogWeight = -1m });
UnitDenial("SYN-S4 confidence below zero", ScoringIssue.InvalidUnit, finding with { ConfidencePercent = -1m });
UnitDenial("SYN-S4 confidence above hundred", ScoringIssue.InvalidUnit, finding with { ConfidencePercent = 101m });
UnitDenial("SYN-S4 unknown severity", ScoringIssue.InvalidUnit, finding with { Severity = (ScoringSeverity)999 });
UnitDenial("SYN-S4 unknown detection", ScoringIssue.InvalidUnit, finding with { Method = (ScoringDetectionMethod)999 });
UnitDenial("SYN-S4 unknown disposition", ScoringIssue.InvalidUnit, finding with { FindingState = (ScoringFindingState)999 });
UnitDenial("SYN-S4 finding lacks severity", ScoringIssue.InvalidUnit, finding with { Severity = null });
UnitDenial("SYN-S4 finding lacks disposition", ScoringIssue.InvalidUnit, finding with { FindingState = null });
UnitDenial("SYN-S4 critical auto confirmation denied", ScoringIssue.InvalidReviewGate, finding with { Severity = ScoringSeverity.Critical, FindingState = ScoringFindingState.AutoConfirmed });
UnitDenial("SYN-S4 high auto confirmation denied", ScoringIssue.InvalidReviewGate, finding with { Severity = ScoringSeverity.High, FindingState = ScoringFindingState.AutoConfirmed });
UnitDenial("SYN-S4 AI auto confirmation denied", ScoringIssue.InvalidReviewGate, finding with { Method = ScoringDetectionMethod.AI, FindingState = ScoringFindingState.AutoConfirmed });
UnitDenial("SYN-S4 finding closure cannot erase observed issue", ScoringIssue.InvalidValidatedClosure, finding with
{ FindingState = ScoringFindingState.ValidatedClosed, HasPassingValidationEvidence = true, PassingValidationEvidenceId = "synthetic-reference" });
UnitDenial("SYN-S4 passing closure flag alone insufficient", ScoringIssue.InvalidValidatedClosure, pass with { FindingState = ScoringFindingState.ValidatedClosed, HasPassingValidationEvidence = true });
UnitDenial("SYN-S4 passing closure requires validation marker", ScoringIssue.InvalidValidatedClosure, pass with
{ FindingState = ScoringFindingState.ValidatedClosed, PassingValidationEvidenceId = "synthetic-reference" });
UnitDenial("SYN-S4 proof without closure malformed", ScoringIssue.InvalidValidatedClosure, pass with { HasPassingValidationEvidence = true, PassingValidationEvidenceId = "synthetic-reference" });
UnitDenial("SYN-S4 pass cannot conceal active finding disposition", ScoringIssue.InvalidUnit, pass with { FindingState = ScoringFindingState.Confirmed });
UnitDenial("SYN-S4 numeric overflow returns typed denial", ScoringIssue.NumericOverflow, pass with { CatalogWeight = decimal.MaxValue });

Check("SYN-S4 reordered input and numeric representation deterministic", () =>
{
    var input = Input([pass with { CatalogWeight = 1.000m }, finding], outcomes: [new("advisory", false), new("approved", true)]);
    var reordered = input with
    {
        ExpectedKeys = input.ExpectedKeys.Reverse().ToArray(),
        Coverage = input.Coverage.Reverse().ToArray(),
        Units = new[] { finding, pass },
        Outcomes = input.Outcomes.Reverse().ToArray(),
        Profile = equal with { Categories = equal.Categories.Reverse().ToArray() }
    };
    Require(Project(input).ContentDigest == Project(reordered).ContentDigest);
});
Check("SYN-S4 frozen nested inputs and read-only projections", () =>
{
    var mutableLinks = new[] { "approved" };
    var mutableUnits = new[] { pass with { OutcomeIds = mutableLinks }, finding };
    var mutableCategories = equal.Categories.ToArray();
    var mutableOutcomes = new[] { new ScoringOutcome("approved", true) };
    var input = Input(mutableUnits, equal with { Categories = mutableCategories }, mutableOutcomes);
    var projection = Project(input);
    var digest = projection.ContentDigest;
    mutableLinks[0] = "changed";
    mutableUnits[0] = finding;
    mutableCategories[0] = new("changed", .5m);
    mutableOutcomes[0] = new("changed", false);
    Require(projection.ContentDigest == digest && projection.Units.Single(item => item.Key == pass.Key).OutcomeIds.Single() == "approved");
    Require(((IList<ScoringUnit>)projection.Units).IsReadOnly && ((IList<string>)projection.Units.Single(item => item.Key == pass.Key).OutcomeIds).IsReadOnly);
    Require(((IList<ScoringCategoryWeight>)projection.Profile.Categories).IsReadOnly && ((IList<CoverageItem>)projection.Coverage).IsReadOnly);
    Require(((IList<GroupHealthMeasure>)projection.PublishableCurrent.Categories).IsReadOnly && ((IList<CoverageLimitation>)projection.Quality.Limitations).IsReadOnly);
});
Check("SYN-S4 digest binds versions, unit facts and proof", () =>
{
    var original = Project(baseline).ContentDigest;
    Require(Project(baseline with { Versions = versions with { FrozenInputDigest = new string('c', 64) } }).ContentDigest != original);
    Require(Project(Input([pass, finding with { FindingState = ScoringFindingState.AcceptedRisk }])).ContentDigest != original);
    Require(Project(Input([pass, finding with { CatalogWeight = 2m }])).ContentDigest != original);
});
Check("SYN-S4 deterministic generated range and weight scaling properties", () =>
{
    // Immutable generator seed 41: 200 small, independently bounded synthetic mixtures.
    var random = new Random(41);
    for (var iteration = 0; iteration < 200; iteration++)
    {
        var units = Enumerable.Range(0, 10).Select(index => random.Next(2) == 0
            ? Unit($"generated-{index}") with { CatalogWeight = random.Next(1, 5) }
            : Unit($"generated-{index}", CoverageState.Finding, (ScoringSeverity)random.Next(5), ScoringFindingState.Confirmed)
                with
            { CatalogWeight = random.Next(1, 5), Method = ScoringDetectionMethod.AI, ConfidencePercent = random.Next(101) }).ToArray();
        var projection = Project(Input(units));
        Require(projection.Provisional.Overall.RawScore is >= 0 and <= 100);
        Require(projection.Provisional.Overall.RawScore == projection.PublishableCurrent.Overall.RawScore);
        var scaled = Project(Input(units.Select(unit => unit with { CatalogWeight = unit.CatalogWeight * 3m }).ToArray()));
        Require(scaled.PublishableCurrent.Overall.RawScore == projection.PublishableCurrent.Overall.RawScore);
    }
});
Check("SYN-S4 100000 immutable units", () =>
{
    var units = Enumerable.Range(0, 100_000).Reverse().Select(index => Unit($"scale-{index:D6}") with
    { CategoryId = index % 2 == 0 ? "SECURITY" : "OPERATIONS" }).ToArray();
    var projection = Project(Input(units));
    Score(projection.PublishableCurrent.Overall, 100m);
    Require(projection.PublishableCurrent.Overall.ScoredUnits == 100_000 && projection.Quality.ExecutableCoverage == new ExecutableCoverageMeasure(100_000, 100_000));
    Require(projection.Units[0].Key.InventoryId == "scale-000000");
});
Console.WriteLine($"{passed} pure scoring groups passed (TP-HAS-003/005/007/017 synthetic subset; no maturity, AI provider or publication).");

ScoringInput Input(IReadOnlyCollection<ScoringUnit> units, ScoringProfile? profile = null, IReadOnlyCollection<ScoringOutcome>? outcomes = null) =>
    new(versions, profile ?? equal, units.Select(unit => unit.Key).ToArray(),
        units.Select(unit => new CoverageItem(unit.Key, unit.CoverageState,
            unit.CoverageState is CoverageState.Pass or CoverageState.Finding ? null : "synthetic-gap-reason",
            unit.CoverageState is CoverageState.Pass or CoverageState.Finding ? null : "synthetic-rule")).ToArray(), units, outcomes ?? []);
void UnitDenial(string id, ScoringIssue issue, ScoringUnit unit) => Denial(id, issue, Input([unit]));
void Denial(string id, ScoringIssue issue, ScoringInput? input) => Check(id, () =>
{
    var result = PilotHealthScorer.Project(input);
    Require(!result.HasProjection && result.Projection is null && result.Issue == issue);
});
void Check(string id, Action action) { try { action(); passed++; } catch (Exception exception) { throw new Exception($"Failed {id}", exception); } }
static ScoringUnit Unit(string id, CoverageState state = CoverageState.Pass, ScoringSeverity? severity = null,
    ScoringFindingState? disposition = null, string category = "SECURITY") =>
    new(new(id, "synthetic-rule"), category, "synthetic-object-type", "QBM", [], state, 1m, severity,
        ScoringDetectionMethod.Deterministic, disposition, 100m);
static ScoringProjection Project(ScoringInput input)
{
    var result = PilotHealthScorer.Project(input);
    if (!result.HasProjection) throw new Exception($"Scoring denied: {result.Issue}");
    return result.Projection!;
}
static void Score(HealthMeasure measure, decimal expected) => Require(measure.IsAvailable && measure.RawScore == expected);
static void Require(bool condition) { if (!condition) throw new Exception("Unexpected synthetic score result."); }
