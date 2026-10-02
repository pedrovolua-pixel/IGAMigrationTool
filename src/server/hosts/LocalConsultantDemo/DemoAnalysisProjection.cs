using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using AssessmentScoring;
using FindingReview;

internal static class DemoAnalysisProjection
{
    internal static object Detail(SyntheticRunSnapshot run, DemoReviewContext? review = null)
    {
        var reviewProfile = DemoAnalysisCatalog.IsReviewMaturityProfile(run.ProfileCatalogId);
        var states = review?.Snapshot?.Findings.ToDictionary(finding => finding.Seed.FindingId,
            finding => Enum.Parse<ScoringFindingState>(finding.Current.State.ToString()), StringComparer.Ordinal);
        var response = reviewProfile && review?.Snapshot is null
            ? new SyntheticDemoAnalysisResponse(review?.ReasonCode ?? "review_input_denied", null)
            : SyntheticDemoAnalysisAdapter.Project(run, states, review?.Snapshot?.SnapshotDigest);
        var data = response.Projection;
        var analysis = data?.Analysis;
        var scoring = data?.Scoring;
        var findings = analysis is null ? [] : analysis.Groups
            .OrderBy(group => (int)analysis.Findings.First(finding => group.OccurrenceIds.Contains(finding.OccurrenceId)).Severity)
            .ThenBy(group => group.RuleId, StringComparer.Ordinal).Select(group =>
        {
            var members = analysis.Findings.Where(finding => group.OccurrenceIds.Contains(finding.OccurrenceId)).ToArray();
            var first = members[0];
            var current = review?.Snapshot?.Findings.Single(finding => finding.Seed.FindingId == group.RootCauseKey).Current;
            return new
            {
                id = group.RootCauseKey,
                title = current?.PresentationTitle ?? first.Title,
                originalTitle = first.Title,
                initialState = first.InitialDisposition.ToString(),
                category = first.CategoryId,
                severity = first.Severity.ToString(),
                confidencePercent = Decimal(first.ConfidencePercent),
                first.ConfidenceBand,
                state = current?.State.ToString() ?? first.InitialDisposition.ToString(),
                method = first.DetectionMethod,
                reviewRequired = (current?.State.ToString() ?? first.InitialDisposition.ToString()) == "Proposed" &&
                    first.Severity is DeterministicAnalysis.SyntheticSeverity.Critical or DeterministicAnalysis.SyntheticSeverity.High,
                ruleId = first.Provenance.RuleId,
                ruleVersion = first.Provenance.RuleVersion,
                baselineId = first.Provenance.BaselineId,
                rootCause = first.RootCause,
                first.RootCauseKey,
                objectIds = group.ObjectIds,
                outcomeIds = members.SelectMany(member => member.OutcomeIds).Distinct().Order(StringComparer.Ordinal),
                originalDigests = members.Select(member => member.GeneratedOriginalDigest),
                evidenceReferences = members.Select(member => member.Provenance.EvidenceReference),
                facts = members.SelectMany(member => member.Facts.Select(fact => $"{member.ObjectId}: {fact.Description} Count: {fact.Count}.")),
                inferences = members.Select(member => member.Inference).Distinct(),
                first.Assumptions,
                first.Impact,
                first.Likelihood,
                first.Limitations,
                recommendations = first.RecommendationOptions.Select(option => $"{option.Text} Prerequisites: {option.Prerequisites} Risk: {option.Risk} Recovery: {option.RecoveryGuidance}"),
                validationGuidance = string.Join(" ", first.ValidationGuidance),
                sources = new[] { first.GuidanceReference }
            };
        }).ToArray();
        var counts = run.Results.GroupBy(item => item.State).ToDictionary(group => group.Key, group => group.Count());
        int Count(CoverageState state) => counts.GetValueOrDefault(state);
        var gaps = run.Results.Count(item => item.State is not (CoverageState.Pass or CoverageState.Finding or CoverageState.NotApplicable));
        var warnings = new List<string>();
        if (scoring is not null)
        {
            warnings.Add("Synthetic fixture rules only; this is not a validated One Identity assessment or published report.");
            if (scoring.Quality.UnreviewedMandatoryFindings > 0)
                warnings.Add($"{scoring.Quality.UnreviewedMandatoryFindings} Critical/High finding occurrences await mandatory review and are excluded from publishable-current health.");
            if (gaps > 0) warnings.Add($"{gaps} explained gap units reduce executable coverage and are excluded from default health. An unavailable score is not 100.");
        }
        var maturityResponse = reviewProfile && response.IsAvailable ? DemoMaturityProjection.Project(run) : null;
        var detail = new
        {
            schemaVersion = 1,
            demoOnly = true,
            runId = run.RunId,
            runRevision = run.Revision,
            status = response.IsAvailable ? "Ready" : "Unavailable",
            response.ReasonCode,
            algorithmVersion = scoring?.Versions.AlgorithmVersion,
            fixtureDigest = scoring is null ? null : run.FrozenInputs.AnalysisFixtureDigest,
            contentDigest = scoring?.ContentDigest,
            reviewSnapshotDigest = scoring is null ? null : review?.Snapshot?.SnapshotDigest,
            review = reviewProfile ? DemoReviewService.Detail(run, review ?? new("review_input_denied", null)) : null,
            maturity = maturityResponse is null ? null : DemoMaturityProjection.Detail(maturityResponse),
            provisional = scoring is null ? null : Measure(scoring.Provisional.Overall),
            publishableCurrent = scoring is null ? null : Measure(scoring.PublishableCurrent.Overall),
            categories = scoring is null ? [] : scoring.Profile.Categories.OrderBy(category => category.CategoryId, StringComparer.Ordinal).Select(category => new
            {
                id = category.CategoryId,
                provisional = Measure(scoring.Provisional.Categories.Single(item => item.Id == category.CategoryId).Score),
                publishableCurrent = Measure(scoring.PublishableCurrent.Categories.Single(item => item.Id == category.CategoryId).Score),
                provisionalWeight = EffectiveWeight(scoring, scoring.Provisional, category.CategoryId),
                publishableWeight = EffectiveWeight(scoring, scoring.PublishableCurrent, category.CategoryId)
            }).ToArray(),
            objectTypes = scoring is null ? [] : Dimension(scoring.Provisional.ObjectTypes, scoring.PublishableCurrent.ObjectTypes),
            modules = scoring is null ? [] : Dimension(scoring.Provisional.Modules, scoring.PublishableCurrent.Modules),
            outcomes = scoring is null ? [] : Dimension(scoring.Provisional.ApprovedOutcomes, scoring.PublishableCurrent.ApprovedOutcomes),
            quality = scoring is null ? null : new
            {
                plannedUnits = run.Plan.ExpectedKeys.Count,
                executedUnits = Count(CoverageState.Pass) + Count(CoverageState.Finding),
                gapUnits = gaps,
                notApplicableUnits = Count(CoverageState.NotApplicable),
                proposedReviewUnits = scoring.Quality.UnreviewedMandatoryFindings,
                totalFindingUnits = Count(CoverageState.Finding)
            },
            findings,
            warnings
        };
        var node = JsonSerializer.SerializeToNode(detail, DemoReportDraftProjection.JsonOptions)!.AsObject();
        var sourceContent = JsonSerializer.SerializeToElement(detail, DemoReportDraftProjection.JsonOptions);
        node["reportDraft"] = JsonSerializer.SerializeToNode(reviewProfile
            ? DemoReportDraftProjection.Detail(run, response, review, maturityResponse, sourceContent) : null,
            DemoReportDraftProjection.JsonOptions);
        return node;
    }
    private static object Measure(HealthMeasure score) => new
    {
        raw = score.RawScore is null ? null : Decimal(score.RawScore.Value),
        display = score.DisplayScore?.ToString("0.0", CultureInfo.InvariantCulture),
        status = score.Status?.ToString() ?? "Unavailable",
        eligibleUnits = score.ScoredUnits
    };
    private static object[] Dimension(IReadOnlyList<GroupHealthMeasure> provisional, IReadOnlyList<GroupHealthMeasure> publishable) =>
        provisional.Select(row => (object)new
        {
            id = row.Id,
            provisional = Measure(row.Score),
            publishableCurrent = Measure(publishable.Single(item => item.Id == row.Id).Score)
        }).ToArray();
    private static string? EffectiveWeight(ScoringProjection scoring, HealthScoreSet scores, string id)
    {
        var assessed = scores.Categories.Where(item => item.Score.IsAvailable).Select(item => item.Id).ToHashSet();
        if (!assessed.Contains(id)) return null;
        if (scoring.Profile.WeightMode == ScoringWeightMode.EqualAssessedCategories) return Decimal(1m / assessed.Count);
        var denominator = scoring.Profile.Categories.Where(item => assessed.Contains(item.CategoryId)).Sum(item => item.Weight);
        return Decimal(scoring.Profile.Categories.Single(item => item.CategoryId == id).Weight / denominator);
    }
    private static string Decimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
