using System.Collections.Immutable;
using AssessmentRuns;
using FindingReview;

internal sealed record DemoReviewContext(string? ReasonCode, SyntheticReviewSnapshot? Snapshot);

/// <summary>Approved loopback-only test identity; never derives grants or originals from client fields.</summary>
internal sealed class DemoReviewService(SyntheticReviewStore store)
{
    internal static SyntheticReviewAuthority Authority { get; } = new("synthetic-consultant", true, true, false, true,
        SyntheticReviewScope.Fixed, [SyntheticReviewRole.Consultant], ["SECURITY", "OPERATIONS"],
        [SyntheticReviewAction.Read, SyntheticReviewAction.Review, SyntheticReviewAction.AddComment, SyntheticReviewAction.EditPresentation]);

    internal async Task<DemoReviewContext> ReadAsync(SyntheticRunSnapshot run)
    {
        if (!DemoAnalysisCatalog.IsReviewMaturityProfile(run.ProfileCatalogId)) return new("review_profile_required", null);
        var original = SyntheticDemoAnalysisAdapter.Project(run);
        if (!original.IsAvailable) return new(original.ReasonCode, null);
        var analysis = original.Projection!.Analysis;
        var seeds = analysis.Groups.Select(group =>
        {
            var members = analysis.Findings.Where(finding => group.OccurrenceIds.Contains(finding.OccurrenceId))
                .OrderBy(finding => finding.OccurrenceId, StringComparer.Ordinal).ToArray();
            var first = members[0];
            return new SyntheticFindingSeed(group.RootCauseKey, first.CategoryId,
                Enum.Parse<SyntheticFindingState>(first.InitialDisposition.ToString()), first.Title,
                members.Select(member => member.GeneratedOriginalDigest).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
                members.Select(member => new SyntheticOccurrenceReference(member.OccurrenceId, member.ObjectId,
                    member.Provenance.RuleId, member.Provenance.RuleVersion, member.GeneratedOriginalDigest)).ToImmutableArray());
        }).OrderBy(seed => seed.FindingId, StringComparer.Ordinal).ToImmutableArray();
        var seed = new SyntheticReviewRunSeed(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest,
            analysis.ContentDigest, SyntheticReviewResourceState.Mutable, seeds);
        var seeded = await store.SeedAsync(seed, Authority);
        if (!seeded.Succeeded) return new("review_input_denied", null);
        var read = await store.ReadAsync(SyntheticReviewScope.Fixed, run.RunId, Authority);
        if (!read.Succeeded || SyntheticReviewDigest.Compute(read.Snapshot!.RunSeed) != SyntheticReviewDigest.Compute(seed))
            return new("review_input_denied", null);
        return new(null, read.Snapshot);
    }

    internal Task<SyntheticReviewApplyResult> ApplyAsync(Guid runId, string findingId, SyntheticReviewCommand command) =>
        store.ApplyAsync(SyntheticReviewScope.Fixed, runId, findingId, Authority, command);

    internal static object Detail(SyntheticRunSnapshot run, DemoReviewContext context)
    {
        var analysis = SyntheticDemoAnalysisAdapter.Project(run).Projection?.Analysis;
        return new
        {
            schemaVersion = 1,
            demoOnly = true,
            runId = run.RunId,
            runRevision = run.Revision,
            status = context.Snapshot is null ? "Unavailable" : "Ready",
            context.ReasonCode,
            snapshotDigest = context.Snapshot?.SnapshotDigest,
            actor = context.Snapshot is null ? null : Authority.ActorId,
            findings = context.Snapshot is null ? [] : context.Snapshot.Findings.OrderBy(finding =>
                (int)analysis!.Findings.First(original => original.RootCauseKey == finding.Seed.FindingId).Severity)
                .ThenBy(finding => finding.Seed.FindingId, StringComparer.Ordinal).Select(finding => new
                {
                    id = finding.Seed.FindingId,
                    finding.Current.Revision,
                    category = finding.Seed.CategoryId,
                    state = finding.Current.State.ToString(),
                    initialState = finding.Seed.InitialState.ToString(),
                    finding.Seed.OriginalTitle,
                    title = finding.Current.PresentationTitle,
                    finding.Current.BusinessContext,
                    originalDigests = finding.Seed.OriginalDigests,
                    occurrenceIds = finding.Seed.Occurrences.Select(occurrence => occurrence.OccurrenceId).ToArray(),
                    actions = new
                    {
                        confirm = Can(finding, context.Snapshot, SyntheticReviewAction.Review) && finding.Current.State == SyntheticFindingState.Proposed,
                        reject = Can(finding, context.Snapshot, SyntheticReviewAction.Review) && finding.Current.State == SyntheticFindingState.Proposed,
                        defer = Can(finding, context.Snapshot, SyntheticReviewAction.Review) && finding.Current.State == SyntheticFindingState.Proposed,
                        comment = Can(finding, context.Snapshot, SyntheticReviewAction.AddComment),
                        editPresentation = Can(finding, context.Snapshot, SyntheticReviewAction.EditPresentation)
                    },
                    history = finding.History.Select(item => new
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
                }).ToArray()
        };
    }

    private static bool Can(SyntheticReviewedFinding finding, SyntheticReviewSnapshot snapshot, SyntheticReviewAction action) =>
        SyntheticReviewPolicy.Authorize(Authority, snapshot.RunSeed.Scope, finding.Seed.CategoryId, action, snapshot.RunSeed.ResourceState) is null;
}
