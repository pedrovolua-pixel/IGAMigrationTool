using System.Collections.Immutable;
using System.Text.Json;
using AssessmentRuns;
using DeterministicAnalysis;
using FindingReview;
using RecommendationGuidance;

internal sealed class DemoRecommendationGuidanceDetail(string status, string? reasonCode, GuidanceSnapshot? snapshot)
{
    public string Status { get; } = status;
    public string? ReasonCode { get; } = reasonCode;
    public GuidanceSnapshot? Snapshot { get; } = snapshot;
}

/// <summary>Already-validated saved synthetic originals plus one captured current review. No advice approval or authority resolution.</summary>
internal static class DemoRecommendationGuidanceProjection
{
    internal static DemoRecommendationGuidanceDetail Detail(SyntheticRunSnapshot run, SyntheticDemoAnalysisResponse response, DemoReviewContext? review)
    {
        var analysis = response.Projection?.Analysis;
        var snapshot = review?.Snapshot;
        if (!response.IsAvailable || analysis is null || snapshot is null ||
            run.Scope != DemoFixtureCatalog.Scope || !DemoAnalysisCatalog.IsReviewMaturityProfile(run.ProfileCatalogId) ||
            analysis.RunId != run.RunId || snapshot.RunSeed.RunId != run.RunId ||
            snapshot.RunSeed.Scope != SyntheticReviewScope.Fixed || snapshot.RunSeed.ResourceState != SyntheticReviewResourceState.Mutable ||
            snapshot.RunSeed.RunInputDigest != run.InputDigest || snapshot.RunSeed.AnalysisDigest != analysis.ContentDigest ||
            snapshot.Findings.Length != analysis.Groups.Length)
            return Deny("guidance_source_unavailable");

        var findings = ImmutableArray.CreateBuilder<GuidanceFindingInput>();
        foreach (var group in analysis.Groups)
        {
            var members = analysis.Findings.Where(item => group.OccurrenceIds.Contains(item.OccurrenceId))
                .OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).ToArray();
            var captured = snapshot.Findings.SingleOrDefault(item => item.Seed.FindingId == group.RootCauseKey);
            if (members.Length == 0 || captured is null || captured.Current.FindingId != group.RootCauseKey)
                return Deny("guidance_finding_unavailable");
            var first = members[0];
            var occurrenceReferences = members.Select(item => new SyntheticOccurrenceReference(item.OccurrenceId,
                item.ObjectId, item.Provenance.RuleId, item.Provenance.RuleVersion, item.GeneratedOriginalDigest)).ToArray();
            if (captured.Seed.OriginalTitle != first.Title || captured.Seed.CategoryId != first.CategoryId ||
                captured.Current.CategoryId != first.CategoryId || captured.Seed.InitialState.ToString() != first.InitialDisposition.ToString() ||
                group.RuleId != first.Provenance.RuleId || group.RuleVersion != first.Provenance.RuleVersion ||
                !captured.Seed.Occurrences.OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).SequenceEqual(occurrenceReferences) ||
                !captured.Seed.OriginalDigests.Order(StringComparer.Ordinal).SequenceEqual(members.Select(item => item.GeneratedOriginalDigest).Order(StringComparer.Ordinal)) ||
                members.Any(item => item.RootCauseKey != group.RootCauseKey || item.Title != first.Title || item.CategoryId != first.CategoryId ||
                    item.Severity != first.Severity || item.InitialDisposition != first.InitialDisposition || item.RootCause != first.RootCause ||
                    item.Provenance.RunId != run.RunId || item.Provenance.Scope != SyntheticAnalysisFixturePack.Scope ||
                    item.Provenance.RuleId != group.RuleId || item.Provenance.RuleVersion != group.RuleVersion ||
                    !item.RecommendationOptions.SequenceEqual(first.RecommendationOptions) || !item.ValidationGuidance.SequenceEqual(first.ValidationGuidance) ||
                    item.GuidanceReference != first.GuidanceReference || !item.Assumptions.SequenceEqual(first.Assumptions) || !item.Limitations.SequenceEqual(first.Limitations)))
                return Deny("guidance_original_mismatch");
            findings.Add(new(group.RootCauseKey, group.RuleId, group.RuleVersion, first.CategoryId, first.Severity.ToString(),
                first.Title, captured.Current.PresentationTitle, captured.Current.BusinessContext, first.InitialDisposition.ToString(),
                captured.Current.State.ToString(), captured.Current.Revision, first.RootCause,
                members.Select(item => new GuidanceOccurrence(item.OccurrenceId, item.ObjectId, item.ObjectType, item.ModuleId,
                    item.GeneratedOriginalDigest, item.Provenance.EvidenceReference)).ToImmutableArray(),
                first.RecommendationOptions.Select(item => new GuidanceOptionInput(item.Id, item.Text, item.Prerequisites, item.Risk, item.RecoveryGuidance)).ToImmutableArray(),
                first.ValidationGuidance, [first.GuidanceReference], first.Assumptions, first.Limitations));
        }

        var source = new GuidanceSourceBinding(new(run.Scope.CustomerId, run.Scope.ProjectId, run.Scope.EnvironmentId),
            run.RunId, run.Revision, run.State.ToString(), run.InputDigest, run.Plan.BaselineId, run.ProfileCatalogId,
            JsonSerializer.SerializeToElement(run.FrozenInputs, DemoReportDraftProjection.JsonOptions),
            JsonSerializer.SerializeToElement(run.Plan.CapabilityLock, DemoReportDraftProjection.JsonOptions),
            JsonSerializer.SerializeToElement(analysis.FrozenInputs, DemoReportDraftProjection.JsonOptions),
            run.FrozenInputs.AnalysisFixtureDigest!, analysis.ContentDigest, analysis.SavedCoverageDigest,
            snapshot.RunSeed.RunId, run.Revision, snapshot.SnapshotDigest);
        var result = RecommendationGuidanceBuilder.Build(new(source, findings.ToImmutable()));
        return result.Succeeded
            ? new("Ready", null, result.Snapshot)
            : Deny($"guidance_{result.Issue}");
    }

    private static DemoRecommendationGuidanceDetail Deny(string reasonCode) => new("Unavailable", reasonCode, null);
}
