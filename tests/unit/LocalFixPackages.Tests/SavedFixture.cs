using System.Collections.Immutable;
using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;
using FindingReview;
using SyntheticFixPackages;

internal static class SavedFixture
{
    internal static SyntheticRunSnapshot Run(string baseline, string profile)
    {
        var request = DemoFixtureCatalog.CreateStartRequest(baseline, profile, "portable-input");
        var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
        var results = DemoFixtureCatalog.Baselines.Single(item => item.Id == baseline).ScriptedResults.Concat(plan.DeclaredItems).ToArray();
        var summary = new SyntheticCoverageStageSummary(CoverageCompletionProjector.Project(plan.ExpectedKeys, results).Kind!.Value,
            CoverageCountProjector.Project(plan.ExpectedKeys, results).Counts!,
            ExecutableCoverageProjector.Project(plan.ExpectedKeys, results).Measure!,
            CoverageLimitationProjector.Project(plan.ExpectedKeys, results).Limitations!);
        var clock = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        return new(Guid.Parse("7eedaf91-6e83-4e82-8b2b-deefc910b27b"), request.Scope, baseline, profile,
            SyntheticRunState.Scoring, 13, false, 1, SyntheticDurableRunEngine.ComputeInputDigest(plan, request.Versions, baseline, profile),
            request.Versions, plan, results, [], null, summary, clock, clock, [], clock);
    }

    internal static DemoReviewContext Review(SyntheticRunSnapshot run)
    {
        // Actual engine values supply test input only; expected counts/artifacts/recipes are independently declared.
        var analysis = SyntheticDemoAnalysisAdapter.Project(run).Projection!.Analysis;
        var seeds = analysis.Groups.Select(group =>
        {
            var members = analysis.Findings.Where(item => group.OccurrenceIds.Contains(item.OccurrenceId)).OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).ToArray();
            var first = members[0];
            return new SyntheticFindingSeed(group.RootCauseKey, first.CategoryId,
                Enum.Parse<SyntheticFindingState>(first.InitialDisposition.ToString()), first.Title,
                members.Select(item => item.GeneratedOriginalDigest).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
                members.Select(item => new SyntheticOccurrenceReference(item.OccurrenceId, item.ObjectId,
                    item.Provenance.RuleId, item.Provenance.RuleVersion, item.GeneratedOriginalDigest)).ToImmutableArray());
        }).OrderBy(item => item.FindingId, StringComparer.Ordinal).ToImmutableArray();
        var seed = new SyntheticReviewRunSeed(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest, analysis.ContentDigest, SyntheticReviewResourceState.Mutable, seeds);
        var findings = seeds.Select(item => new SyntheticReviewedFinding(item,
            new(item.FindingId, item.CategoryId, 0, item.InitialState, item.OriginalTitle, ""), [])).ToImmutableArray();
        return Freeze(new(seed, findings, ""));
    }

    internal static DemoReviewContext Change(DemoReviewContext context, SyntheticFindingState state, string title, string businessContext)
    {
        var before = context.Snapshot!;
        var findings = before.Findings.Select(item =>
        {
            if (item.Seed.InitialState != SyntheticFindingState.Proposed) return item;
            var edit = new SyntheticReviewCommand(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), 0,
                SyntheticReviewEventKind.EditPresentation, Title: title, BusinessContext: businessContext);
            var edited = SyntheticReviewPolicy.Next(item.Current, edit);
            var command = new SyntheticReviewCommand(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"), 1,
                state switch { SyntheticFindingState.Confirmed => SyntheticReviewEventKind.Confirm, SyntheticFindingState.Rejected => SyntheticReviewEventKind.Reject, _ => SyntheticReviewEventKind.Defer }, "Fictional review reason");
            if (SyntheticReviewPolicy.ValidateCommand(edit) is not null || SyntheticReviewPolicy.ValidateCommand(command) is not null ||
                SyntheticReviewPolicy.ValidateTransition(edited, command) is not null) throw new InvalidOperationException("Invalid test review command.");
            var current = SyntheticReviewPolicy.Next(edited, command);
            var clock = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
            return item with
            {
                Current = current,
                History = [new(edit.EventId, "synthetic-consultant", [SyntheticReviewRole.Consultant], edit, clock, edited),
                new(command.EventId, "synthetic-consultant", [SyntheticReviewRole.Consultant], command, clock.AddSeconds(1), current)]
            };
        }).ToImmutableArray();
        return Freeze(before with { Findings = findings, SnapshotDigest = "" });
    }
    internal static DemoReviewContext Freeze(SyntheticReviewSnapshot value) => new(null,
        value with { SnapshotDigest = SyntheticReviewDigest.Compute(value with { SnapshotDigest = "" }) });
}

internal sealed class PackageValueComparer : IEqualityComparer<FixPackage>
{
    public bool Equals(FixPackage? x, FixPackage? y) => System.Text.Json.JsonSerializer.Serialize(x) == System.Text.Json.JsonSerializer.Serialize(y);
    public int GetHashCode(FixPackage value) => StringComparer.Ordinal.GetHashCode(value.PackageId);
}
