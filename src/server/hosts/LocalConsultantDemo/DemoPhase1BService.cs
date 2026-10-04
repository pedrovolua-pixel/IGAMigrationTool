using System.Collections.Immutable;
using System.Text.Json;
using AssessmentRuns;
using AssessmentScoring;
using DeterministicAnalysis;
using FindingReview;
using Npgsql;
using SyntheticAiExecution;
using SyntheticFixReview;
using SyntheticOutcomePriority;
using SyntheticPlanningTasks;
using SyntheticSourceFence;

internal sealed class Phase1BDeniedException(string reason) : Exception(reason);
internal sealed record Phase1BCapture(SyntheticRunSnapshot Run, AiExecutionSnapshot Ai, LockedOutcomeSet Outcomes,
    SyntheticAnalysisResult Originals, DemoReviewContext Review, DemoAnalysisCapture Analysis, ArtifactReviewSource ArtifactSource,
    ArtifactReviewReadResult Artifacts);
/// <summary>One guarded fictional composition. All owning reads and writes join the same ordered source transaction.</summary>
internal sealed partial class DemoPhase1BService
{
    private readonly string connection;
    private readonly SyntheticDurableRunEngine engine;
    private readonly SyntheticReviewStore reviews;
    internal readonly SyntheticAiExecutionStore Ai;
    internal readonly SyntheticOutcomePriorityStore Outcomes;
    private readonly SyntheticFixReviewStore artifacts;
    internal readonly SyntheticPlanningTaskStore Tasks;
    internal static OutcomeAuthority Consultant { get; } = new("synthetic-consultant", true, true, false, true, OutcomeScope.Fixed,
        [OutcomeRole.Consultant], ["SECURITY", "OPERATIONS"], [OutcomeAction.ReadOutcome, OutcomeAction.ManageOutcome, OutcomeAction.ReadPlanning, OutcomeAction.ManagePlanning], OutcomeResourceState.Mutable);
    internal static OutcomeAuthority CustomerApprover { get; } = Consultant with { ActorId = "synthetic-customer-outcome-approver-v1", Roles = [OutcomeRole.CustomerOutcomeApprover], Actions = [OutcomeAction.ReadOutcome, OutcomeAction.ApproveOutcome] };
    internal static AiAuthority Worker { get; } = new("synthetic-worker", AiScope.Fixed, [AiRole.Worker], [AiAction.Read, AiAction.Dispatch, AiAction.Reconcile], ["SECURITY", "OPERATIONS"]);
    internal static AiAuthority AiConsultant { get; } = new("synthetic-consultant", AiScope.Fixed, [AiRole.Consultant], [AiAction.Read, AiAction.Override], ["SECURITY", "OPERATIONS"]);
    internal bool Auditor { get; }
    internal DemoPhase1BService(string connection, SyntheticDurableRunEngine engine, SyntheticReviewStore reviews, bool auditor)
    {
        var db = new NpgsqlConnectionStringBuilder(connection);
        if (db.Host is not ("127.0.0.1" or "localhost") || db.Port != 55433 || db.Username != "iga_synthetic" || db.Database?.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Phase1B requires its dedicated loopback fictional database.");
        this.connection = connection; this.engine = engine; this.reviews = reviews; Auditor = auditor;
        Ai = new(connection);
        Outcomes = new(OutcomeScope.Fixed, PlanningSource);
        artifacts = new(connection, ArtifactReviewScope.Fixed, (_, _) => Task.FromResult(new ArtifactReviewSourceResult(ArtifactReviewIssue.SourceUnavailable, null)));
        Tasks = SyntheticPlanningTaskStore.CreateForPhase1B(connection, PlanningTaskScope.Fixed, TaskSource);
    }
    internal async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var c = new NpgsqlConnection(connection); await c.OpenAsync(ct); await Outcomes.InitializeAsync(c, ct);
        await Ai.InitializeAsync(ct); await artifacts.InitializeAsync(ct); await Tasks.InitializeAsync(ct);
    }
    internal async Task<T> Fenced<T>(Guid runId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operation, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connection); await c.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(c, t, OutcomeScope.Fixed, ct);
        if (runId != Guid.Empty) await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", runId, ct);
        var result = await operation(c, t); await t.CommitAsync(ct); return result;
    }
    internal Task<OutcomeRegistrySnapshot> Registry(CancellationToken ct) => Fenced(Guid.Empty, async (c, t) =>
    {
        var read = await Outcomes.ReadRegistryAsync(c, t, Consultant, ct); Require(read.Issue); return read.Snapshot!;
    }, ct);
    internal Task<OutcomeApplyResult> Outcome(OutcomeCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(Guid.Empty, async (c, t) =>
        {
            if (command.Content is { } content && content.UnitLinks.Any(k => !DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.Concat(DemoPhase1BCatalog.AiKeys).Contains(k)))
                throw new Phase1BDeniedException("InvalidInput");
            var actor = command.Kind == OutcomeKind.Approve ? CustomerApprover : Consultant;
            if (command.Kind == OutcomeKind.Retire)
            {
                var registry = await Outcomes.ReadRegistryAsync(c, t, Consultant, ct); Require(registry.Issue);
                var entry = registry.Snapshot!.Entries.SingleOrDefault(e => e.Content.OutcomeId == command.OutcomeId && e.Content.Version == command.Version);
                if (entry is null) throw new Phase1BDeniedException("NotFound");
                if (entry.History.Any(e => e.Kind == OutcomeKind.Approve)) actor = CustomerApprover;
            }
            var result = await Outcomes.ApplyOutcomeAsync(c, t, actor, command, ct);
            Require(result.Issue); return result;
        }, ct);
    }
    internal Task<SyntheticRunCommandResult> Start(Guid requestId, ImmutableArray<OutcomeSelection> selected, CancellationToken ct)
    {
        Mutable(); if (requestId == Guid.Empty) throw new Phase1BDeniedException("InvalidInput");
        return Fenced(requestId, async (c, t) =>
        {
            var locked = await Outcomes.LockOutcomesAsync(c, t, requestId, Consultant, selected, ct); Require(locked.Issue);
            var request = DemoPhase1BCatalog.CreateStartRequest(requestId.ToString("D"), DemoPhase1BAiFixture.Locks(requestId, locked.Lock!.ContentDigest),
                AiExecutionPolicy.PolicyVersion, AiExecutionPolicy.PromptVersion, AiExecutionPolicy.ProviderVersion);
            var run = await engine.StartInTransactionAsync(c, t, request, requestId, ct); Require(run.Issue);
            Require(SyntheticOutcomePriorityStore.ValidateApplicability(locked.Lock, run.Snapshot!.Plan.ExpectedKeys));
            var ai = await Ai.EnsureRunAsync(c, t, Worker, DemoPhase1BAiFixture.RunLock(run.Snapshot), DemoPhase1BAiFixture.Works(requestId), ct); Require(ai.Issue);
            return run;
        }, ct);
    }
    internal async Task<Phase1BCapture> Core(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, CancellationToken ct)
    {
        var read = await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId, ct); Require(read.Issue);
        var run = read.Snapshot!;
        if (!DemoPhase1BCatalog.MatchesFrozenFixture(run)) throw new Phase1BDeniedException("SourceConflict");
        var locked = await Outcomes.ReadLockedAsync(c, t, runId, Consultant, ct); Require(locked.Issue);
        var ai = await Ai.ReadInTransactionAsync(c, t, AiConsultant, runId, ct); Require(ai.Issue);
        var originals = SyntheticPhase1BAnalysisAdapter.Originals(run, ai.Value!, locked.Lock!) ?? throw new Phase1BDeniedException("SourceUnavailable");
        var seed = Seed(run, originals);
        var seeded = await reviews.SeedInTransactionAsync(c, t, seed, DemoReviewService.Authority, ct); Require(seeded.Issue);
        var review = await reviews.ReadInTransactionAsync(c, t, SyntheticReviewScope.Fixed, runId, DemoReviewService.Authority, ct); Require(review.Issue);
        if (SyntheticReviewDigest.Compute(review.Snapshot!.RunSeed) != SyntheticReviewDigest.Compute(seed)) throw new Phase1BDeniedException("IntegrityMismatch");
        var context = new DemoReviewContext(null, review.Snapshot);
        var states = review.Snapshot.Findings.ToDictionary(f => f.Seed.FindingId, f => Enum.Parse<ScoringFindingState>(f.Current.State.ToString()), StringComparer.Ordinal);
        var response = SyntheticPhase1BAnalysisAdapter.Project(run, ai.Value!, locked.Lock!, states, review.Snapshot.SnapshotDigest);
        if (!response.IsAvailable) throw new Phase1BDeniedException(response.ReasonCode ?? "IntegrityMismatch");
        var capture = DemoAnalysisProjection.Capture(run, context, response);
        var source = ArtifactReviewSourceBuilder.Build(capture.FixPackages?.Snapshot); Require(source.Issue);
        var artifact = await artifacts.ReadInTransactionAsync(c, t, runId, DemoArtifactReviewService.Authority, source.Source!, ct); Require(artifact.Issue);
        return new(run, ai.Value!, locked.Lock!, originals, context, capture, source.Source!, artifact);
    }
    internal Task<object> Ledger(Guid runId, CancellationToken ct) => Fenced(runId, async (c, t) =>
    {
        Mutable(); var read = await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId, ct); Require(read.Issue);
        if (!DemoPhase1BCatalog.MatchesFrozenFixture(read.Snapshot)) throw new Phase1BDeniedException("SourceConflict");
        var ai = await Ai.ReadInTransactionAsync(c, t, AiConsultant, runId, ct); Require(ai.Issue);
        var locked = await Outcomes.ReadLockedAsync(c, t, runId, Consultant, ct); Require(locked.Issue);
        if (locked.Lock!.ContentDigest != read.Snapshot!.FrozenInputs.Phase1BLocks!.OutcomeLockDigest ||
            AiExecutionCanonical.Serialize(ai.Value!.RunLock) != AiExecutionCanonical.Serialize(DemoPhase1BAiFixture.RunLock(read.Snapshot))) throw new Phase1BDeniedException("IntegrityMismatch");
        return (object)new
        {
            runId,
            runRevision = read.Snapshot.Revision,
            actor = "synthetic-consultant",
            ai = ai.Value,
            outcomes = locked.Lock,
            canOverrideBudget = !read.Snapshot.CancelRequested && read.Snapshot.State is SyntheticRunState.Planned or SyntheticRunState.Running && ai.Value.Works.Any(w => w.State is AiWorkState.Pending or AiWorkState.Retryable)
        };
    }, ct);
    internal Task<object> Workspace(Guid runId, CancellationToken ct) => Fenced(runId, async (c, t) =>
    {
        Mutable(); // Auditor receives only the separately authorized metadata export/navigation capture.
        var core = await Core(c, t, runId, ct);
        var task = await Tasks.ReadInTransactionAsync(c, t, runId, DemoPlanningTaskService.Authority, ct); Require(task.Issue);
        var priority = await Outcomes.ReadPlanningAsync(c, t, runId, Consultant, ct); Require(priority.Issue);
        var registry = await Outcomes.ReadRegistryAsync(c, t, Consultant, ct); Require(registry.Issue);
        core.Analysis.Analysis["artifactReview"] = JsonSerializer.SerializeToNode(DemoArtifactReviewService.Detail(core.Artifacts), DemoReportDraftProjection.JsonOptions);
        core.Analysis.Analysis["planningTasks"] = JsonSerializer.SerializeToNode(DemoPlanningTaskService.Detail(task), DemoReportDraftProjection.JsonOptions);
        return (object)new
        {
            schemaVersion = 1,
            demoOnly = true,
            runId,
            analysis = core.Analysis.Analysis,
            ai = core.Ai,
            outcomes = core.Outcomes,
            priority = PlanningPresentation(priority.Snapshot!, core),
            currentRegistry = registry.Snapshot,
            actor = "synthetic-consultant",
            canPlan = true,
            canOverrideBudget = core.Ai.Works.Any(w => w.State is AiWorkState.Pending or AiWorkState.Retryable),
            fictionalDefaults = "Priority uses fixed fictional Internal exposure, Ordinary dependency and S effort. Approved outcome IDs access-governance, resilience and maintainability map to objective weights 1, 0.75 and 0.5; other outcome IDs have no planning objective match. No customer assessment is asserted.",
            objectiveMap = new[] { new CustomerObjective("access-governance", 1m), new CustomerObjective("resilience", .75m), new CustomerObjective("maintainability", .5m) },
            unmappedOutcomeIds = core.Outcomes.Outcomes.Select(o => o.Content.OutcomeId).Where(id => id is not ("access-governance" or "resilience" or "maintainability")).ToArray()
        };
    }, ct);
    private static object PlanningPresentation(PlanningSnapshot snapshot, Phase1BCapture core)
    {
        var guidance = core.Analysis.FixPackages!.Snapshot!.Guidance;
        return new
        {
            snapshot.Source,
            Entries = snapshot.Entries.Select(entry =>
        {
            var finding = guidance.Findings.Single(f => f.FindingId == entry.Original.FindingId);
            var option = finding.Options.Single(o => o.ScopedOptionId == entry.Original.OptionId);
            return new
            {
                entry.Original,
                entry.SourceDigest,
                entry.Revision,
                entry.OriginalPriority,
                entry.EffectivePriority,
                entry.OriginalEffort,
                entry.EffectiveEffort,
                entry.EffortApproval,
                entry.HasPriorityOverride,
                entry.HasEffortOverride,
                entry.History,
                OriginalContext = new
                {
                    GuidanceDigest = snapshot.Source.GuidanceDigest,
                    Assumptions = finding.Assumptions.Add("Fixed fictional Internal exposure, Ordinary dependency and S effort."),
                    Prerequisites = new[] { option.Prerequisites },
                    SourceReferences = finding.GuidanceReferences,
                    ObjectiveMapVersion = "synthetic-phase1b-objective-map-v1",
                    ObjectiveMapDigest = OutcomePriorityCanonical.Digest(entry.Original.Factors.Objectives),
                    Objectives = entry.Original.Factors.Objectives,
                    MatchedObjectiveIds = entry.Original.Factors.MatchedObjectiveIds
                }
            };
        }).ToArray()
        };
    }
    private async Task<PlanningTaskSourceResult> TaskSource(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, PlanningTaskAuthority authority, CancellationToken ct)
    {
        var core = await Core(c, t, runId, ct); return PlanningTaskSourceBuilder.Build(core.Analysis.FixPackages!.Snapshot, core.Artifacts.Snapshot);
    }
    private async Task<PlanningSourceResult> PlanningSource(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, OutcomeAuthority authority, CancellationToken ct)
    {
        var core = await Core(c, t, runId, ct);
        var guidance = DemoRecommendationGuidanceProjection.FromVerifiedAnalysis(core.Run, core.Originals, core.Review);
        if (guidance?.Snapshot is null) return new(OutcomePriorityIssue.SourceUnavailable, null);
        var options = guidance.Snapshot.Findings.SelectMany(f => f.Options.Select(o =>
        {
            var members = core.Originals.Findings.Where(x => x.RootCauseKey == f.FindingId).ToArray();
            return new PlanningSourceOption(f.FindingId, o.ScopedOptionId, members[0].CategoryId,
                new(Enum.Parse<ScoringSeverity>(members[0].Severity.ToString()), Exposure.Internal,
                    members.Select(x => x.ObjectId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(), DependencyCriticality.Ordinary, EffortSize.S,
                    [new("access-governance", 1m), new("resilience", .75m), new("maintainability", .5m)],
                    core.Outcomes.Outcomes.Where(x => members.Any(m => core.Originals.Results.Where(r => r.Unit.ObjectId == m.ObjectId && r.Unit.RuleId == m.Provenance.RuleId).Any(r => x.Content.UnitLinks.Contains(r.Unit.Key))))
                        .Select(x => x.Content.OutcomeId).Where(x => x is "access-governance" or "resilience" or "maintainability").Distinct().Order(StringComparer.Ordinal).ToImmutableArray()));
        })).OrderBy(o => o.OptionId, StringComparer.Ordinal).ToImmutableArray();
        var revision = checked(core.Run.Revision + core.Review.Snapshot!.Findings.Sum(f => f.Current.Revision));
        var source = new Phase1BPlanningSource(OutcomeScope.Fixed, runId, DemoPhase1BCatalog.ProfileId, DemoPhase1BCatalog.ApplicationVersion,
            DemoPhase1BCatalog.OutcomeContractDigest, core.Run.InputDigest, guidance.Snapshot.ContentDigest, revision, options, "");
        return new(null, OutcomePriorityCanonical.Seal(source));
    }
    internal Task<object> Review(Guid runId, string findingId, SyntheticReviewCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(runId, async (c, t) =>
        {
            await Core(c, t, runId, ct);
            var applied = await reviews.ApplyInTransactionAsync(c, t, SyntheticReviewScope.Fixed, runId, findingId, DemoReviewService.Authority, command, ct); Require(applied.Issue);
            var core = await Core(c, t, runId, ct); return DemoReviewService.Detail(core.Run, core.Review, core.Originals);
        }, ct);
    }
    internal Task<PlanningApplyResult> Planning(Guid runId, PlanningCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(runId, async (c, t) => { var r = await Outcomes.ApplyPlanningAsync(c, t, runId, Consultant, command, ct); Require(r.Issue); return r; }, ct);
    }
    internal Task<ArtifactReviewApplyResult> Artifact(Guid runId, string artifactId, ArtifactReviewCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(runId, async (c, t) => { var core = await Core(c, t, runId, ct); var r = await artifacts.ApplyInTransactionAsync(c, t, runId, artifactId, DemoArtifactReviewService.Authority, command, core.ArtifactSource, ct); Require(r.Issue); return r; }, ct);
    }
    internal Task<PlanningTaskApplyResult> TaskEvent(Guid runId, string taskId, PlanningTaskCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(runId, async (c, t) => { var r = await Tasks.ApplyInTransactionAsync(c, t, runId, taskId, DemoPlanningTaskService.Authority, command, ct); Require(r.Issue); return r; }, ct);
    }
    internal Task<AiOperationResult<AiOverrideEvent>> Override(Guid runId, AiOverrideCommand command, CancellationToken ct)
    {
        Mutable(); return Fenced(runId, async (c, t) =>
        {
            var read = await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId, ct); Require(read.Issue);
            if (read.Snapshot!.CancelRequested || read.Snapshot.State is SyntheticRunState.Cancelled or SyntheticRunState.Failed) throw new Phase1BDeniedException("InvalidState");
            var r = await Ai.OverrideAsync(c, t, AiConsultant, runId, command, ct); Require(r.Issue); return r;
        }, ct);
    }
    internal static SyntheticReviewRunSeed Seed(SyntheticRunSnapshot run, SyntheticAnalysisResult analysis) => new(SyntheticReviewScope.Fixed, run.RunId, run.InputDigest,
        analysis.ContentDigest, SyntheticReviewResourceState.Mutable, analysis.Groups.Select(group =>
        {
            var members = analysis.Findings.Where(f => group.OccurrenceIds.Contains(f.OccurrenceId)).OrderBy(f => f.OccurrenceId, StringComparer.Ordinal).ToArray();
            return new SyntheticFindingSeed(group.RootCauseKey, members[0].CategoryId, Enum.Parse<SyntheticFindingState>(members[0].InitialDisposition.ToString()), members[0].Title,
                members.Select(m => m.GeneratedOriginalDigest).Distinct().Order(StringComparer.Ordinal).ToImmutableArray(), members.Select(m => new SyntheticOccurrenceReference(m.OccurrenceId, m.ObjectId, m.Provenance.RuleId, m.Provenance.RuleVersion, m.GeneratedOriginalDigest)).ToImmutableArray());
        }).OrderBy(f => f.FindingId, StringComparer.Ordinal).ToImmutableArray());
    internal static void Require(object? issue) { if (issue is not null) throw new Phase1BDeniedException(issue.ToString()!); }
    internal void Mutable() { if (Auditor) throw new Phase1BDeniedException("Denied"); }
}
