using System.Collections.Immutable;
using AssessmentRuns;
using FindingReview;
using RecommendationGuidance;

internal static class SavedGuidanceFixture
{
    internal static GuidanceInput Create(SyntheticRunSnapshot run, SyntheticReviewSnapshot review)
    {
        // Typed engine/review output supplies input only. Expected results are literal independent assertions.
        var d = SavedDraftFixture.Create(run, review).Source;
        var analysis = SyntheticDemoAnalysisAdapter.Project(run).Projection!.Analysis;
        var source = new GuidanceSourceBinding(new(d.Scope.CustomerId, d.Scope.ProjectId, d.Scope.EnvironmentId), d.RunId,
            d.RunRevision, d.RunState, d.RunInputDigest, d.BaselineId, d.ProfileId, d.FrozenVersions, d.CapabilityLock,
            d.AnalysisLock, d.AnalysisFixtureDigest, d.AnalysisContentDigest, d.SavedCoverageDigest,
            d.ReviewRunId, d.ReviewRunRevision, d.ReviewSnapshotDigest);
        var findings = analysis.Groups.Select(group =>
        {
            var members = analysis.Findings.Where(f => f.RootCauseKey == group.RootCauseKey).ToArray(); var original = members[0];
            var captured = review.Findings.Single(f => f.Seed.FindingId == group.RootCauseKey);
            return new GuidanceFindingInput(group.RootCauseKey, original.Provenance.RuleId, original.Provenance.RuleVersion,
                original.CategoryId, original.Severity.ToString(), captured.Seed.OriginalTitle, captured.Current.PresentationTitle,
                captured.Current.BusinessContext, captured.Seed.InitialState.ToString(), captured.Current.State.ToString(),
                captured.Current.Revision, original.RootCause,
                members.Select(f => new GuidanceOccurrence(f.OccurrenceId, f.ObjectId, f.ObjectType, f.ModuleId,
                    f.GeneratedOriginalDigest, f.Provenance.EvidenceReference)).ToImmutableArray(),
                original.RecommendationOptions.Select(o => new GuidanceOptionInput(o.Id, o.Text, o.Prerequisites, o.Risk, o.RecoveryGuidance)).ToImmutableArray(),
                original.ValidationGuidance, ImmutableArray.Create(original.GuidanceReference), original.Assumptions, original.Limitations);
        }).ToImmutableArray();
        return new(source, findings);
    }
}
