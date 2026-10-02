using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AssessmentCoverage;

namespace AssessmentScoring;

/// <summary>
/// Pure pilot-health-v1 projection over trusted, complete, explicitly synthetic
/// rule/subject units. It does not validate source integrity, authorize an outcome,
/// mutate review states, promote a catalog, calculate maturity or publish a score.
/// ConfidencePercent is the locked unit confidence, including for retained-penalty states.
/// Raw scores and status remain unrounded; display-only rounding is one decimal,
/// MidpointRounding.AwayFromZero, as settled for the cycle-04 internal demo.
/// </summary>
public static class PilotHealthScorer
{
    public const string AlgorithmVersion = "pilot-health-v1";
    public const string InputSchemaVersion = "synthetic-scoring-input-v1";

    public static ScoringResult Project(ScoringInput? input)
    {
        if (input is null || input.Versions is null || input.Profile is null ||
            input.ExpectedKeys is null || input.Coverage is null || input.Units is null || input.Outcomes is null)
            return Deny(ScoringIssue.InvalidInput);
        try
        {
            return ProjectValidated(input);
        }
        catch (OverflowException)
        {
            return Deny(ScoringIssue.NumericOverflow);
        }
    }

    private static ScoringResult ProjectValidated(ScoringInput input)
    {
        var versions = input.Versions;
        if (!ValidText(versions.AlgorithmVersion) || !ValidText(versions.InputSchemaVersion) ||
            !ValidText(versions.BaselineId) || !ValidText(versions.RuleCatalogVersion) || !ValidText(versions.ProfileVersion) ||
            !ValidDigest(versions.FrozenInputDigest) || !ValidDigest(versions.AnalysisContentDigest))
            return Deny(ScoringIssue.InvalidInput);
        if (versions.AlgorithmVersion != AlgorithmVersion || versions.InputSchemaVersion != InputSchemaVersion)
            return Deny(ScoringIssue.UnknownVersion);
        if (input.Profile.ProfileVersion != versions.ProfileVersion)
            return Deny(ScoringIssue.VersionMismatch);
        if (!ValidText(input.Profile.ProfileVersion) || !Enum.IsDefined(input.Profile.WeightMode) || input.Profile.Categories is null)
            return Deny(ScoringIssue.InvalidProfile);

        var categories = input.Profile.Categories.ToArray();
        if (categories.Length == 0 || categories.Any(item => item is null || !ValidText(item.CategoryId) || item.Weight <= 0) ||
            categories.Select(item => item.CategoryId).Distinct(StringComparer.Ordinal).Count() != categories.Length)
            return Deny(ScoringIssue.InvalidProfile);
        if (input.Profile.WeightMode == ScoringWeightMode.ExplicitCategoryWeights && categories.Sum(item => item.Weight) != 1m)
            return Deny(ScoringIssue.InvalidProfile);
        if (input.Profile.WeightMode == ScoringWeightMode.EqualAssessedCategories && categories.Any(item => item.Weight != categories[0].Weight))
            return Deny(ScoringIssue.InvalidProfile);
        var profile = input.Profile with
        {
            Categories = Array.AsReadOnly(categories.OrderBy(item => item.CategoryId, StringComparer.Ordinal).ToArray())
        };

        var expected = input.ExpectedKeys.ToArray();
        var coverage = input.Coverage.ToArray();
        if (!CoverageReconciler.Reconcile(expected, coverage).IsComplete)
            return Deny(ScoringIssue.InvalidCoverage);
        expected = expected.OrderBy(key => key.InventoryId, StringComparer.Ordinal).ThenBy(key => key.EvidenceCategory, StringComparer.Ordinal).ToArray();
        coverage = coverage.OrderBy(item => item.Key.InventoryId, StringComparer.Ordinal).ThenBy(item => item.Key.EvidenceCategory, StringComparer.Ordinal).ToArray();
        var resultsByKey = coverage.ToDictionary(item => item.Key);
        var outcomeInput = input.Outcomes.ToArray();
        if (outcomeInput.Any(item => item is null || !ValidText(item.Id)) ||
            outcomeInput.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != outcomeInput.Length)
            return Deny(ScoringIssue.InvalidInput);
        var outcomes = outcomeInput.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var outcomeIds = outcomes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var categoryIds = categories.Select(item => item.CategoryId).ToHashSet(StringComparer.Ordinal);
        var unitKeys = new HashSet<CoverageKey>();
        var units = new List<ScoringUnit>();
        foreach (var unit in input.Units.ToArray())
        {
            if (unit is null || unit.Key is null || !ValidText(unit.Key.InventoryId) || !ValidText(unit.Key.EvidenceCategory) ||
                !ValidText(unit.CategoryId) || !ValidText(unit.ObjectType) || !ValidText(unit.ModuleId) || unit.OutcomeIds is null ||
                unit.CatalogWeight <= 0 || !Enum.IsDefined(unit.CoverageState) || !Enum.IsDefined(unit.Method) ||
                (unit.Severity is not null && !Enum.IsDefined(unit.Severity.Value)) ||
                (unit.FindingState is not null && !Enum.IsDefined(unit.FindingState.Value)) ||
                unit.ConfidencePercent is < 0 or > 100)
                return Deny(ScoringIssue.InvalidUnit);
            if (!unitKeys.Add(unit.Key)) return Deny(ScoringIssue.DuplicateUnit);
            if (!categoryIds.Contains(unit.CategoryId)) return Deny(ScoringIssue.UnknownCategory);
            if (!resultsByKey.TryGetValue(unit.Key, out var result)) return Deny(ScoringIssue.InvalidCoverage);
            if (result.State != unit.CoverageState) return Deny(ScoringIssue.CoverageStateMismatch);
            var links = unit.OutcomeIds.ToArray();
            if (links.Any(id => !ValidText(id))) return Deny(ScoringIssue.InvalidUnit);
            if (links.Any(id => !outcomeIds.Contains(id))) return Deny(ScoringIssue.UnknownOutcome);
            var closed = unit.FindingState == ScoringFindingState.ValidatedClosed;
            if (closed && (unit.CoverageState != CoverageState.Pass || !unit.HasPassingValidationEvidence || !ValidText(unit.PassingValidationEvidenceId)))
                return Deny(ScoringIssue.InvalidValidatedClosure);
            if (!closed && (unit.HasPassingValidationEvidence || unit.PassingValidationEvidenceId is not null))
                return Deny(ScoringIssue.InvalidValidatedClosure);
            if (unit.CoverageState == CoverageState.Finding)
            {
                if (unit.Severity is null || unit.FindingState is null) return Deny(ScoringIssue.InvalidUnit);
                if (unit.FindingState == ScoringFindingState.AutoConfirmed &&
                    (unit.Method == ScoringDetectionMethod.AI || unit.Severity is ScoringSeverity.Critical or ScoringSeverity.High))
                    return Deny(ScoringIssue.InvalidReviewGate);
            }
            else if (unit.CoverageState == CoverageState.Pass)
            {
                if (unit.FindingState is not null && !closed) return Deny(ScoringIssue.InvalidUnit);
                if (unit.Severity is not null && !closed) return Deny(ScoringIssue.InvalidUnit);
            }
            else if (unit.Severity is not null || unit.FindingState is not null)
            {
                return Deny(ScoringIssue.InvalidUnit);
            }
            units.Add(unit with { OutcomeIds = Array.AsReadOnly(links.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray()) });
        }
        if (unitKeys.Count != expected.Length || expected.Any(key => !unitKeys.Contains(key))) return Deny(ScoringIssue.InvalidCoverage);
        var frozenUnits = units.OrderBy(unit => unit.Key.InventoryId, StringComparer.Ordinal).ThenBy(unit => unit.Key.EvidenceCategory, StringComparer.Ordinal).ToArray();
        var provisional = Score(frozenUnits, outcomes, profile, true);
        var publishable = Score(frozenUnits, outcomes, profile, false);
        var findings = frozenUnits.Where(unit => unit.CoverageState == CoverageState.Finding).ToArray();
        var mandatory = findings.Where(unit => unit.Severity is ScoringSeverity.Critical or ScoringSeverity.High).ToArray();
        var unreviewed = mandatory.Count(unit => unit.FindingState == ScoringFindingState.Proposed);
        var quality = new ScoringQuality(expected.Length, coverage.Length,
            ExecutableCoverageProjector.Project(expected, coverage).Measure!,
            Array.AsReadOnly(CoverageCountProjector.Project(expected, coverage).Counts!.ToArray()),
            Array.AsReadOnly(CoverageLimitationProjector.Project(expected, coverage).Limitations!.ToArray()),
            findings.Count(unit => unit.FindingState == ScoringFindingState.Proposed), mandatory.Length, mandatory.Length - unreviewed,
            unreviewed, frozenUnits.Count(unit => IsApplicable(unit) && unit.Method == ScoringDetectionMethod.Deterministic),
            frozenUnits.Count(unit => IsApplicable(unit) && unit.Method == ScoringDetectionMethod.AI),
            coverage.Count(item => item.State == CoverageState.Error));
        var digest = Digest(versions, profile, expected, coverage, frozenUnits, outcomes, provisional, publishable);
        return new(null, new(versions, profile, Array.AsReadOnly(expected), Array.AsReadOnly(coverage),
            Array.AsReadOnly(frozenUnits), Array.AsReadOnly(outcomes), provisional, publishable, quality, digest));
    }

    private static HealthScoreSet Score(ScoringUnit[] units, ScoringOutcome[] outcomes, ScoringProfile profile, bool provisional)
    {
        var eligible = units.Where(unit => IsApplicable(unit) &&
            (provisional || unit.FindingState != ScoringFindingState.Proposed)).ToArray();
        var categories = profile.Categories.Select(category => new GroupHealthMeasure(category.CategoryId,
            Measure(eligible.Where(unit => unit.CategoryId == category.CategoryId)))).ToArray();
        var assessed = categories.Where(category => category.Score.IsAvailable).ToArray();
        HealthMeasure overall;
        if (assessed.Length == 0) overall = Unavailable();
        else
        {
            var weights = profile.Categories.ToDictionary(item => item.CategoryId, item =>
                profile.WeightMode == ScoringWeightMode.EqualAssessedCategories ? 1m : item.Weight, StringComparer.Ordinal);
            var denominator = assessed.Sum(item => weights[item.Id]);
            var raw = assessed.Sum(item => item.Score.RawScore!.Value * weights[item.Id]) / denominator;
            overall = Available(raw, eligible.Length);
        }
        IReadOnlyList<GroupHealthMeasure> Groups(IEnumerable<string> ids, Func<ScoringUnit, string, bool> belongs) =>
            Array.AsReadOnly(ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => new GroupHealthMeasure(id, Measure(eligible.Where(unit => belongs(unit, id))))).ToArray());
        return new(overall, Array.AsReadOnly(categories),
            Groups(units.Select(unit => unit.ObjectType), (unit, id) => unit.ObjectType == id),
            Groups(units.Select(unit => unit.ModuleId), (unit, id) => unit.ModuleId == id),
            Groups(outcomes.Where(outcome => outcome.IsCustomerApprovedFixtureFlag).Select(outcome => outcome.Id),
                (unit, id) => unit.OutcomeIds.Contains(id)));
    }

    private static HealthMeasure Measure(IEnumerable<ScoringUnit> selected)
    {
        var units = selected.ToArray();
        if (units.Length == 0) return Unavailable();
        var applicable = units.Sum(unit => unit.CatalogWeight);
        var earned = units.Sum(Earned);
        return Available(100m * earned / applicable, units.Length);
    }

    private static decimal Earned(ScoringUnit unit)
    {
        if (unit.CoverageState == CoverageState.Pass || unit.FindingState == ScoringFindingState.Rejected) return unit.CatalogWeight;
        var severity = unit.Severity switch
        {
            ScoringSeverity.Critical => 1m,
            ScoringSeverity.High => .8m,
            ScoringSeverity.Medium => .45m,
            ScoringSeverity.Low => .2m,
            ScoringSeverity.Informational => 0m,
            _ => throw new InvalidOperationException("Validated finding severity missing.")
        };
        var confidence = unit.Method == ScoringDetectionMethod.Deterministic ? 1m : Math.Clamp(unit.ConfidencePercent / 100m, .5m, 1m);
        return unit.CatalogWeight * (1m - severity * confidence);
    }

    private static HealthMeasure Available(decimal raw, int count) => new(raw, decimal.Round(raw, 1, MidpointRounding.AwayFromZero),
        raw < 50m ? HealthStatus.Red : raw < 80m ? HealthStatus.Yellow : HealthStatus.Green, count);
    private static HealthMeasure Unavailable() => new(null, null, null, 0);
    private static bool IsApplicable(ScoringUnit unit) => unit.CoverageState is CoverageState.Pass or CoverageState.Finding;
    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static ScoringResult Deny(ScoringIssue issue) => new(issue, null);

    private static string Digest(ScoringVersions versions, ScoringProfile profile, CoverageKey[] expected, CoverageItem[] coverage,
        ScoringUnit[] units, ScoringOutcome[] outcomes, HealthScoreSet provisional, HealthScoreSet publishable)
    {
        var canonical = new StringBuilder();
        void Text(string? value)
        {
            if (value is null) canonical.Append("-1:");
            else canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
        void Number(decimal value) => Text(value.ToString("G29", CultureInfo.InvariantCulture));
        void Key(CoverageKey key) { Text(key.InventoryId); Text(key.EvidenceCategory); }
        void Measures(HealthScoreSet set)
        {
            void MeasureValue(HealthMeasure measure)
            {
                Text(measure.RawScore?.ToString("G29", CultureInfo.InvariantCulture));
                Text(measure.DisplayScore?.ToString("G29", CultureInfo.InvariantCulture));
                Text(measure.Status?.ToString()); Text(measure.ScoredUnits.ToString(CultureInfo.InvariantCulture));
            }
            MeasureValue(set.Overall);
            foreach (var groups in new[] { set.Categories, set.ObjectTypes, set.Modules, set.ApprovedOutcomes })
            {
                Text(groups.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var group in groups) { Text(group.Id); MeasureValue(group.Score); }
            }
        }
        Text("synthetic-scoring-projection-v1"); Text(versions.AlgorithmVersion); Text(versions.InputSchemaVersion);
        Text(versions.BaselineId); Text(versions.RuleCatalogVersion); Text(versions.ProfileVersion);
        Text(versions.FrozenInputDigest); Text(versions.AnalysisContentDigest); Text(profile.WeightMode.ToString());
        Text(profile.Categories.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var category in profile.Categories) { Text(category.CategoryId); Number(category.Weight); }
        Text(expected.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var key in expected) Key(key);
        Text(coverage.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var item in coverage)
        {
            Key(item.Key); Text(item.State.ToString()); Text(item.ReasonCode); Text(item.ResponsibleStage); Text(item.EvidenceReference);
        }
        Text(units.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var unit in units)
        {
            Key(unit.Key); Text(unit.CategoryId); Text(unit.ObjectType); Text(unit.ModuleId); Number(unit.CatalogWeight);
            Text(unit.CoverageState.ToString()); Text(unit.Severity?.ToString()); Text(unit.Method.ToString());
            Text(unit.FindingState?.ToString()); Number(unit.ConfidencePercent); Text(unit.HasPassingValidationEvidence.ToString());
            Text(unit.PassingValidationEvidenceId); Text(unit.OutcomeIds.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var id in unit.OutcomeIds) Text(id);
        }
        Text(outcomes.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var outcome in outcomes) { Text(outcome.Id); Text(outcome.IsCustomerApprovedFixtureFlag.ToString()); }
        Measures(provisional); Measures(publishable);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}
