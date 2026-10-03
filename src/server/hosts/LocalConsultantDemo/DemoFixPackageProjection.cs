using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using FindingReview;
using RecommendationGuidance;
using SyntheticFixPackages;

internal sealed record DemoFixPackageDetail(string SchemaVersion, Guid RunId, long RunRevision,
    string RunInputDigest, string BaselineId, string ProfileId, string Status,
    string? ReasonCode, FixPackageSnapshot? Snapshot);

/// <summary>One captured current guidance/review pair and fixed fictional templates; no HTML, storage or authority resolution.</summary>
internal static class DemoFixPackageProjection
{
    internal static DemoFixPackageDetail? Detail(SyntheticRunSnapshot run,
        DemoRecommendationGuidanceDetail? capturedGuidance, DemoReviewContext? capturedReview)
    {
        if (!DemoFixPackageCatalog.IsProfile(run.ProfileCatalogId) && !DemoArtifactReviewCatalog.IsProfile(run.ProfileCatalogId) && !DemoPlanningTaskCatalog.IsProfile(run.ProfileCatalogId)) return null;
        DemoFixPackageDetail Result(string? reason, FixPackageSnapshot? snapshot = null) => new(
            "synthetic-fix-package-demo-v1", run.RunId, run.Revision, run.InputDigest,
            run.BaselineCatalogId, run.ProfileCatalogId, snapshot is null ? "Unavailable" : "Ready", reason, snapshot);
        try
        {
            if (!(DemoFixPackageCatalog.MatchesFrozenFixture(run) || DemoArtifactReviewCatalog.MatchesFrozenFixture(run) || DemoPlanningTaskCatalog.MatchesFrozenFixture(run)) || !Complete(run))
                return Result("fix_packages_source_unavailable");
            if (capturedGuidance is not { Status: "Ready", ReasonCode: null, Snapshot: not null } ||
                capturedReview is not { ReasonCode: null, Snapshot: not null } ||
                !MatchesCapture(run, capturedGuidance.Snapshot, capturedReview.Snapshot))
                return Result("fix_packages_capture_mismatch");
            var packages = FixPackageBuilder.Build(capturedGuidance.Snapshot);
            if (!packages.Succeeded || packages.Snapshot!.TemplateVersion != DemoFixPackageCatalog.TemplateVersion ||
                packages.Snapshot.TemplateDigest != DemoFixPackageCatalog.TemplateDigest)
                return Result("fix_packages_integrity_denied");
            return Result(null, packages.Snapshot);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException or JsonException or OverflowException)
        {
            return Result("fix_packages_source_unavailable");
        }
    }

    private static bool Complete(SyntheticRunSnapshot run)
    {
        if (run.State != SyntheticRunState.Scoring || run.CancelRequested || run.Lease is not null ||
            run.CheckpointSequence < 1 || run.InFlightKeys is null || run.InFlightKeys.Count != 0 ||
            run.Results is null || run.CoverageSummary is null) return false;
        var expected = DemoAnalysisCatalog.WorkResults(run);
        if (run.Results.Count != expected.Count || run.Results.Any(item => item?.Key is null)) return false;
        static IEnumerable<CoverageItem> Ordered(IEnumerable<CoverageItem> values) => values
            .OrderBy(item => item.Key.InventoryId, StringComparer.Ordinal).ThenBy(item => item.Key.EvidenceCategory, StringComparer.Ordinal);
        if (!Ordered(run.Results).SequenceEqual(Ordered(expected))) return false;
        var completion = CoverageCompletionProjector.Project(run.Plan.ExpectedKeys, run.Results);
        if (!completion.HasProjection) return false;
        var summary = new SyntheticCoverageStageSummary(completion.Kind!.Value,
            CoverageCountProjector.Project(run.Plan.ExpectedKeys, run.Results).Counts!,
            ExecutableCoverageProjector.Project(run.Plan.ExpectedKeys, run.Results).Measure!,
            CoverageLimitationProjector.Project(run.Plan.ExpectedKeys, run.Results).Limitations!);
        return JsonSerializer.Serialize(run.CoverageSummary) == JsonSerializer.Serialize(summary);
    }

    private static bool MatchesCapture(SyntheticRunSnapshot run, GuidanceSnapshot guidance, SyntheticReviewSnapshot review)
    {
        var analysisResponse = SyntheticDemoAnalysisAdapter.Project(run);
        var analysis = analysisResponse.Projection?.Analysis;
        if (!analysisResponse.IsAvailable || analysis is null || review.RunSeed is null ||
            review.Findings.IsDefault || review.RunSeed.Findings.IsDefault || guidance.Findings.IsDefault ||
            review.RunSeed.Scope != SyntheticReviewScope.Fixed || review.RunSeed.ResourceState != SyntheticReviewResourceState.Mutable ||
            review.RunSeed.RunId != run.RunId || review.RunSeed.RunInputDigest != run.InputDigest ||
            review.RunSeed.AnalysisDigest != analysis.ContentDigest ||
            review.SnapshotDigest != SyntheticReviewDigest.Compute(review with { SnapshotDigest = "" }) ||
            review.Findings.Length != analysis.Groups.Length || guidance.Findings.Length != analysis.Groups.Length ||
            review.RunSeed.Findings.Length != analysis.Groups.Length ||
            review.Findings.Select(item => item.Seed.FindingId).Distinct(StringComparer.Ordinal).Count() != review.Findings.Length ||
            guidance.Findings.Select(item => item.FindingId).Distinct(StringComparer.Ordinal).Count() != guidance.Findings.Length)
            return false;
        var expectedSource = new GuidanceSourceBinding(new(run.Scope.CustomerId, run.Scope.ProjectId, run.Scope.EnvironmentId),
            run.RunId, run.Revision, run.State.ToString(), run.InputDigest, run.Plan.BaselineId, run.ProfileCatalogId,
            JsonSerializer.SerializeToElement(run.FrozenInputs, DemoReportDraftProjection.JsonOptions),
            JsonSerializer.SerializeToElement(run.Plan.CapabilityLock, DemoReportDraftProjection.JsonOptions),
            JsonSerializer.SerializeToElement(analysis.FrozenInputs, DemoReportDraftProjection.JsonOptions),
            run.FrozenInputs.AnalysisFixtureDigest!, analysis.ContentDigest, analysis.SavedCoverageDigest,
            review.RunSeed.RunId, run.Revision, review.SnapshotDigest);
        if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(guidance.Source, DemoReportDraftProjection.JsonOptions),
                JsonSerializer.SerializeToNode(expectedSource, DemoReportDraftProjection.JsonOptions))) return false;
        foreach (var group in analysis.Groups)
        {
            var members = analysis.Findings.Where(item => group.OccurrenceIds.Contains(item.OccurrenceId))
                .OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).ToArray();
            var captured = review.Findings.SingleOrDefault(item => item.Seed.FindingId == group.RootCauseKey);
            var finding = guidance.Findings.SingleOrDefault(item => item.FindingId == group.RootCauseKey);
            var seed = review.RunSeed.Findings.SingleOrDefault(item => item.FindingId == group.RootCauseKey);
            if (members.Length == 0 || captured is null || finding is null || seed is null ||
                SyntheticReviewDigest.Compute(seed) != SyntheticReviewDigest.Compute(captured.Seed)) return false;
            var original = members[0];
            var references = members.Select(item => new SyntheticOccurrenceReference(item.OccurrenceId, item.ObjectId,
                item.Provenance.RuleId, item.Provenance.RuleVersion, item.GeneratedOriginalDigest)).ToImmutableArray();
            if (captured.Current.FindingId != group.RootCauseKey || captured.Current.CategoryId != original.CategoryId ||
                captured.Seed.CategoryId != original.CategoryId || captured.Seed.OriginalTitle != original.Title ||
                captured.Seed.InitialState.ToString() != original.InitialDisposition.ToString() ||
                !captured.Seed.Occurrences.OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).SequenceEqual(references) ||
                !captured.Seed.OriginalDigests.Order(StringComparer.Ordinal).SequenceEqual(members.Select(item => item.GeneratedOriginalDigest).Order(StringComparer.Ordinal)) ||
                finding.RuleId != group.RuleId || finding.RuleVersion != group.RuleVersion || finding.CategoryId != original.CategoryId ||
                finding.Severity != original.Severity.ToString() || finding.OriginalTitle != original.Title ||
                finding.PresentationTitle != captured.Current.PresentationTitle || finding.BusinessContext != captured.Current.BusinessContext ||
                finding.InitialState != original.InitialDisposition.ToString() || finding.CurrentState != captured.Current.State.ToString() ||
                finding.FindingRevision != captured.Current.Revision || finding.RootCause != original.RootCause ||
                !finding.Occurrences.SequenceEqual(members.Select(item => new GuidanceOccurrence(item.OccurrenceId, item.ObjectId,
                    item.ObjectType, item.ModuleId, item.GeneratedOriginalDigest, item.Provenance.EvidenceReference))) ||
                !finding.Options.Select(item => new GuidanceOptionInput(item.OptionId, item.Text, item.Prerequisites, item.Risk, item.RecoveryGuidance))
                    .SequenceEqual(original.RecommendationOptions.OrderBy(item => item.Id, StringComparer.Ordinal)
                        .Select(item => new GuidanceOptionInput(item.Id, item.Text, item.Prerequisites, item.Risk, item.RecoveryGuidance))) ||
                !finding.ValidationGuidance.SequenceEqual(original.ValidationGuidance) ||
                !finding.GuidanceReferences.SequenceEqual([original.GuidanceReference]) ||
                !finding.Assumptions.SequenceEqual(original.Assumptions) || !finding.Limitations.SequenceEqual(original.Limitations))
                return false;
        }
        return true;
    }
}
