using System.Collections.Immutable;
using RecommendationGuidance;

internal static class InputFixture
{
    internal static GuidanceInput Create()
    {
        // Pre-cycle independent synthetic draft fixture supplies only data, never expected results.
        var d = CoreFixture.Create().Source;
        var source = new GuidanceSourceBinding(new(d.Scope.CustomerId, d.Scope.ProjectId, d.Scope.EnvironmentId), d.RunId,
            d.RunRevision, d.RunState, d.RunInputDigest, d.BaselineId, d.ProfileId, d.FrozenVersions, d.CapabilityLock,
            d.AnalysisLock, d.AnalysisFixtureDigest, d.AnalysisContentDigest, d.SavedCoverageDigest,
            d.ReviewRunId, d.ReviewRunRevision, d.ReviewSnapshotDigest);
        var first = new GuidanceFindingInput(CoreFixture.Hex('d'), "SYN-GUARD", "synthetic-rule-v1", "SECURITY", "Critical",
            "Original fictional title", "Current fictional title", "Independent business context", "Proposed", "Rejected", 2,
            "Fictional shared root cause", ImmutableArray.Create(
                new GuidanceOccurrence(CoreFixture.Hex('9'), "SYN-OBJECT-Z", "SyntheticControl", "SyntheticSecurity", CoreFixture.Hex('8'), "fixture:evidence-z"),
                new GuidanceOccurrence(CoreFixture.Hex('0'), "SYN-OBJECT-A", "SyntheticControl", "SyntheticSecurity", CoreFixture.Hex('9'), "fixture:evidence-a")),
            ImmutableArray.Create(new GuidanceOptionInput("OPT-Z", "Second existing fictional option", "Second prerequisite", "Second risk", "Second recovery"),
                new GuidanceOptionInput("OPT-A", "First existing fictional option", "First prerequisite", "First risk", "First recovery")),
            ImmutableArray.Create("Validate first", "Validate second"), ImmutableArray.Create("fixture:reference-first", "fixture:reference-second"),
            ImmutableArray.Create("Fictional assumption"), ImmutableArray.Create("Unverified fixture limitation"));
        var second = first with
        {
            FindingId = CoreFixture.Hex('e'),
            RuleId = "SYN-TRACE",
            CategoryId = "OPERATIONS",
            Severity = "Medium",
            OriginalTitle = "Second original",
            PresentationTitle = "Second original",
            BusinessContext = "",
            InitialState = "AutoConfirmed",
            CurrentState = "AutoConfirmed",
            FindingRevision = 0,
            RootCause = "Second root cause",
            Occurrences = ImmutableArray.Create(new GuidanceOccurrence(CoreFixture.Hex('1'), "SYN-OBJECT-B", "SyntheticControl", "SyntheticOperations", CoreFixture.Hex('a'), "fixture:evidence-b")),
            Options = ImmutableArray.Create(new GuidanceOptionInput("OPT-A", "Repeated option ID in another finding", "Existing prerequisite", "Existing risk", "Existing recovery"))
        };
        return new(source, ImmutableArray.Create(second, first));
    }
}
