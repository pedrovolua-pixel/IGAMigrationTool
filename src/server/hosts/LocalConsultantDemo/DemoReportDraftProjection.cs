using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AssessmentCoverage;
using AssessmentMaturity;
using AssessmentRuns;
using FindingReview;
using ReportDrafts;

/// <summary>Only an existing validated synthetic read; no draft storage, publication or authority resolution.</summary>
internal static class DemoReportDraftProjection
{
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal static object Detail(SyntheticRunSnapshot run, SyntheticDemoAnalysisResponse analysisResponse,
        DemoReviewContext? review, MaturityResult? maturityResponse, JsonElement currentRead)
    {
        var analysis = analysisResponse.Projection?.Analysis;
        var scoring = analysisResponse.Projection?.Scoring;
        var snapshot = review?.Snapshot;
        var maturity = maturityResponse?.Projection;
        if (!analysisResponse.IsAvailable || analysis is null || scoring is null || snapshot is null || maturity is null ||
            snapshot.RunSeed.RunId != run.RunId || snapshot.RunSeed.RunInputDigest != run.InputDigest ||
            snapshot.RunSeed.AnalysisDigest != analysis.ContentDigest ||
            snapshot.RunSeed.Scope != SyntheticReviewScope.Fixed || run.Scope != DemoFixtureCatalog.Scope ||
            currentRead.GetProperty("reviewSnapshotDigest").GetString() != snapshot.SnapshotDigest ||
            currentRead.GetProperty("contentDigest").GetString() != scoring.ContentDigest)
            return Deny("draft_source_unavailable");

        var source = new DraftSourceBinding(new(run.Scope.CustomerId, run.Scope.ProjectId, run.Scope.EnvironmentId),
            run.RunId, run.Revision, run.State.ToString(), run.InputDigest, run.Plan.BaselineId, run.ProfileCatalogId,
            JsonSerializer.SerializeToElement(run.FrozenInputs, JsonOptions),
            JsonSerializer.SerializeToElement(run.Plan.CapabilityLock, JsonOptions),
            JsonSerializer.SerializeToElement(analysis.FrozenInputs, JsonOptions),
            run.FrozenInputs.AnalysisFixtureDigest!, analysis.ContentDigest, scoring.ContentDigest, analysis.SavedCoverageDigest,
            snapshot.RunSeed.RunId, run.Revision, snapshot.SnapshotDigest,
            run.FrozenInputs.MaturityFixtureDigest!, maturity.InputDigest, maturity.ContentDigest);
        var content = new JsonObject();
        foreach (var field in new[] { "provisional", "publishableCurrent", "categories", "objectTypes", "modules", "outcomes",
                     "quality", "findings", "warnings", "maturity" })
            content[field] = JsonNode.Parse(currentRead.GetProperty(field).GetRawText());
        foreach (var node in content["findings"]!.AsArray())
        {
            var finding = node!.AsObject();
            var captured = snapshot.Findings.Single(item => item.Seed.FindingId == finding["id"]!.GetValue<string>());
            finding["revision"] = captured.Current.Revision;
            finding["businessContext"] = captured.Current.BusinessContext;
            finding["occurrenceIds"] = JsonSerializer.SerializeToNode(captured.Seed.Occurrences.Select(item => item.OccurrenceId), JsonOptions);
        }
        content["reviewHistory"] = JsonSerializer.SerializeToNode(snapshot.Findings.Select(finding => new
        {
            findingId = finding.Seed.FindingId,
            finding.Current.Revision,
            finding.Seed.OriginalTitle,
            finding.Current.BusinessContext,
            events = finding.History.Select(item => new
            {
                item.EventId,
                item.ActorId,
                actorRoles = item.ActorRoles.Select(role => role.ToString()).ToArray(),
                kind = item.Command.Kind.ToString(),
                item.RecordedAtUtc,
                revision = item.Outcome.Revision,
                state = item.Outcome.State.ToString(),
                item.Command.Reason,
                item.Command.Text,
                item.Command.Title,
                item.Command.BusinessContext
            }).ToArray()
        }).ToArray(), JsonOptions);
        content["healthyControls"] = JsonSerializer.SerializeToNode(analysis.Results.Where(result => result.Coverage.State == CoverageState.Pass)
            .Select(result => new { result.Unit.ObjectId, result.Unit.RuleId, result.Unit.RuleVersion, state = "Pass" }).ToArray(), JsonOptions);
        content["limitations"] = JsonSerializer.SerializeToNode(analysis.Results.Where(result => result.Coverage.State is not
                (CoverageState.Pass or CoverageState.Finding or CoverageState.NotApplicable))
            .Select(result => new { result.Unit.ObjectId, result.Unit.RuleId, state = result.Coverage.State.ToString(), result.Coverage.ReasonCode }).ToArray(), JsonOptions);
        content["methodology"] = JsonSerializer.SerializeToNode(new[]
        {
            "Synthetic fixture rules and fictional evidence only; this draft is unpublished and does not establish a completed assessment.",
            "Health uses pilot-health-v1 decimal calculations. Proposed severe findings are visible in provisional health; reviewed current health follows the captured finding dispositions.",
            "Default health excludes evidence gaps; quality retains every explained limitation. Unavailable health is not zero or 100.",
            "Maturity uses pilot-maturity-v1 cumulative evidence gates independently of health. All declared mandatory domains remain in its denominator.",
            "Original findings and evidence references remain immutable. Current presentation and history are captured from one attributed synthetic review snapshot.",
            "Summary, technical detail and inert Markdown identify this same canonical draft digest. Refresh after review returns a new current value; no durable report history or publication is created."
        }, JsonOptions);
        content["unavailableSections"] = JsonSerializer.SerializeToNode(new[]
        {
            "Customer risk acceptance and validated remediation closure are unavailable in this synthetic fixture.",
            "Reassessment comparison and recurrence are unavailable.",
            "Customer-approved outcome adherence is unavailable when no approved outcomes are present.",
            "AI analysis, full recommendations/fix packages and task workflows are unavailable; any shown fixture guidance is unverified and cannot execute.",
            "Actual report publication, acknowledgment, PDF, report downloads and sharing are unavailable."
        }, JsonOptions);
        var result = DraftSnapshotBuilder.Build(new(source, JsonSerializer.SerializeToElement(content, JsonOptions)));
        if (!result.Succeeded) return Deny($"draft_{result.Issue}");
        var markdown = StructuredDraftMarkdown.Render(result.Snapshot!);
        return new { status = "Ready", reasonCode = (string?)null, snapshot = result.Snapshot, markdown };
    }
    private static object Deny(string reasonCode) => new { status = "Unavailable", reasonCode, snapshot = (object?)null, markdown = (object?)null };
}
