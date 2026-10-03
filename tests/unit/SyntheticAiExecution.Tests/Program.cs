using System.Collections.Immutable;
using AssessmentCoverage;
using AssessmentScoring;
using SyntheticAiExecution;
using SyntheticAiValidation;

public static class Program
{
    public static int Checks;
    public static void Check(bool value, string name) { Checks++; if (!value) throw new InvalidOperationException("FAIL: " + name); }
    public static void Main()
    {
        var fixture = AiFixtures.Create(Guid.Parse("11111111-2222-4333-8444-555555555555"), "fixture-epoch-unit-v1");
        Check(AiExecutionPolicy.ValidateRun(fixture.Locked, fixture.Works) is null, "closed valid fixture");
        Check(AiExecutionCanonical.Serialize(new { z = 2, a = "é" }) == "{\"a\":\"\\u00E9\",\"z\":2}", "independent canonical literal");
        Check(AiExecutionPolicy.Authorize(AiFixtures.Worker, AiAction.Dispatch) is null, "scoped worker dispatch");
        Check(AiExecutionPolicy.Authorize(AiFixtures.Consultant, AiAction.Override) is null, "consultant override");
        foreach (var role in Enum.GetValues<AiRole>())
        {
            Check((AiExecutionPolicy.Authorize(AiFixtures.Worker with { Roles = [role] }, AiAction.Dispatch) is null) == (role == AiRole.Worker), "dispatch role " + role);
            Check((AiExecutionPolicy.Authorize(AiFixtures.Consultant with { Roles = [role] }, AiAction.Override) is null) == (role == AiRole.Consultant), "override role " + role);
        }
        foreach (var bad in new[] { AiFixtures.Worker with { Authenticated = false }, AiFixtures.Worker with { Active = false }, AiFixtures.Worker with { AssignmentActive = false }, AiFixtures.Worker with { Revoked = true }, AiFixtures.Worker with { Categories = ["SECURITY"] }, AiFixtures.Worker with { AiPolicyAllowed = false }, AiFixtures.Worker with { ResourceState = AiResourceState.Deleted }, AiFixtures.Worker with { Actions = [] } })
            Check(AiExecutionPolicy.Authorize(bad, AiAction.Dispatch, "OPERATIONS") is not null, "authority denied");
        Check(AiExecutionPolicy.Authorize(AiFixtures.Worker with { ResourceState = AiResourceState.Deleted }, AiAction.Reconcile, "OPERATIONS", true) is null, "scoped billing metadata reconciliation");
        foreach (var bad in new[] { fixture.Locked with { RunId = Guid.Empty }, fixture.Locked with { PolicyVersion = "unknown" }, fixture.Locked with { ProfileId = "historical" }, fixture.Locked with { MappingDigest = new string('b', 64) }, fixture.Locked with { Scope = AiScope.Fixed with { ProjectId = "foreign" } } })
            Check(AiExecutionPolicy.ValidateRun(bad, fixture.Works) is not null, "lock mismatches");
        var work = fixture.Works[0]; var packet = SyntheticAiPacketBuilder.Build(work.PacketInputJson).Packet!;
        var key = new AiAttemptKey(AiExecutionPolicy.LogicalKey(fixture.Locked, work), Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"), 1, packet.ContentDigest);
        var recovery = new AiRecovery(key, AiScenario.Benign, work.PacketInputJson, work.Units, false); var provider = new FakeAiProvider();
        var receipt = provider.Dispatch(recovery);
        Check(receipt.InputUse == 80 && receipt.OutputUse == 160, "literal benign component use");
        Check(receipt == provider.Lookup(recovery), "pure exact same attempt receipt");
        var mapped = AiExecutionPolicy.Map(fixture.Locked, work, key, receipt.OutputJson);
        Check(mapped.Succeeded && mapped.Value.Length == 2, "two real accepted unit proposals");
        foreach (var outcome in mapped.Value)
        {
            Check(outcome.State == CoverageState.Finding && outcome.Finding!.State == "Proposed" && outcome.Finding.ConfidencePercent == 80 && outcome.Finding.Weight == 1 && outcome.Finding.DetectionMethod == "AI", "AI cannot auto confirm");
            Check(outcome.Finding!.ProposalCanonicalJson.Contains("\\u003Cscript\\u003E", StringComparison.Ordinal), "hostile content inert canonical text");
        }
        var duplicates = fixture.Works.SetItem(0, work with { Units = work.Units.Add(work.Units[0]) });
        Check(AiExecutionPolicy.ValidateRun(fixture.Locked with { MappingDigest = AiExecutionPolicy.MappingDigest(duplicates) }, duplicates) is not null, "same actual key/proposal duplication denies");
        foreach (var scenario in new[] { AiScenario.InvalidOutput, AiScenario.InvalidCitation })
        { var bad = provider.Dispatch(recovery with { Scenario = scenario }); Check(!AiExecutionPolicy.Map(fixture.Locked, work, key, bad.OutputJson).Succeeded, "invalid complete result " + scenario); }
        var empty = provider.Dispatch(recovery with { Scenario = AiScenario.Empty });
        var emptyMapped = AiExecutionPolicy.Map(fixture.Locked, work, key, empty.OutputJson);
        Check(emptyMapped.Succeeded && emptyMapped.Value.All(x => x.State == CoverageState.NotAssessed && x.ReasonCode == "AI_NO_VALIDATED_CONCLUSION"), "empty is explicit gap not pass");
        var foreign = receipt.OutputJson!.Replace("ev-111", "ev-222", StringComparison.Ordinal);
        Check(!AiExecutionPolicy.Map(fixture.Locked, work, key, foreign).Succeeded, "cross-unit citations denied");
        Check(provider.Dispatch(recovery with { Scenario = AiScenario.LostResponse }).Outcome == AiProviderOutcome.Unknown && provider.Lookup(recovery with { Scenario = AiScenario.LostResponse }).Outcome == AiProviderOutcome.Response, "lost outcome exact lookup");
        var billing = provider.Lookup(recovery with { BillingOnly = true, PacketInputJson = null, Units = [] });
        Check(billing.InputUse == 80 && billing.OutputUse == 160 && billing.OutputJson is null, "billing-only lookup excludes input/output");
        for (var ordinal = 1; ordinal <= 3; ordinal++)
            Check(provider.Dispatch(recovery with { Scenario = AiScenario.RetryTwice, Attempt = key with { Ordinal = ordinal } }).Outcome == (ordinal < 3 ? AiProviderOutcome.RetryableFailure : AiProviderOutcome.Response), "three fixed attempt script");
        ScoreGoldens();
        Console.WriteLine($"PASS {Checks} synthetic AI execution portable assertions");
    }
    private static void ScoreGoldens()
    {
        var expected = new CoverageKey[] { new("object-det", "CONFIGURATION"), new("object-ai", "CONFIGURATION") };
        foreach (var severity in Enum.GetValues<ScoringSeverity>())
        {
            var values = new[] { 20m, 36m, 64m, 84m, 100m }; // Independently specified approved literal oracle.
            var coverage = new CoverageItem[] { new(expected[0], CoverageState.Pass), new(expected[1], CoverageState.Finding) };
            var units = new ScoringUnit[] { new(expected[0], "SECURITY", "Control", "Module", [], CoverageState.Pass, 1m, null, ScoringDetectionMethod.Deterministic, null, 100m),
                new(expected[1], "OPERATIONS", "Control", "Module", [], CoverageState.Finding, 1m, severity, ScoringDetectionMethod.AI, ScoringFindingState.Proposed, 80m) };
            var input = new ScoringInput(new("pilot-health-v1", "synthetic-scoring-input-v1", "baseline", "catalog", "profile", AiFixtures.Digest, AiFixtures.Digest), new("profile", ScoringWeightMode.EqualAssessedCategories, [new("SECURITY", 1), new("OPERATIONS", 1)]), expected, coverage, units, []);
            var result = PilotHealthScorer.Project(input);
            Check(result.HasProjection && result.Projection!.Provisional.Categories.Single(x => x.Id == "OPERATIONS").Score.RawScore == values[(int)severity], "independent AI severity oracle");
            Check(result.Projection!.PublishableCurrent.Overall.RawScore == 100m, "proposed AI excluded publishable");
            if (severity == ScoringSeverity.High)
            {
                Check(result.Projection.Provisional.Overall.RawScore == 68m, "independent 68 overall");
                Check(PilotHealthScorer.Project(input with { Units = [units[0], units[1] with { FindingState = ScoringFindingState.Confirmed }] }).Projection!.PublishableCurrent.Overall.RawScore == 68m, "confirmed exact68");
                Check(PilotHealthScorer.Project(input with { Units = [units[0], units[1] with { FindingState = ScoringFindingState.Rejected }] }).Projection!.PublishableCurrent.Overall.RawScore == 100m, "rejected full100");
            }
            var gapInput = input with { Coverage = [coverage[0], new(expected[1], CoverageState.Error, "AI_UNKNOWN_OUTCOME", "AI")], Units = [units[0], units[1] with { CoverageState = CoverageState.Error, Severity = null, FindingState = null }] };
            var gap = PilotHealthScorer.Project(gapInput);
            Check(gap.HasProjection && gap.Projection!.Provisional.Overall.RawScore == 100m && gap.Projection.Quality.RepresentedKeys == 2, "gap scorer unit represented, no health penalty");
            Check(!PilotHealthScorer.Project(gapInput with { Units = [units[0]] }).HasProjection, "dropping gap scorer unit denied");
        }
    }
}
