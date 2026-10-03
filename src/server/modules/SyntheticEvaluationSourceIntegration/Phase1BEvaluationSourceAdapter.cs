using System.Data;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using Npgsql;
using SyntheticAiExecution;
using SyntheticOutcomePriority;
using SyntheticSourceFence;
namespace SyntheticEvaluationSourceIntegration;

/// <summary>Non-mutating, in-memory Consultant source capture. Caller owns the existing transaction.</summary>
public sealed class Phase1BEvaluationSourceAdapter
{
    private readonly SyntheticDurableRunEngine runs;
    private readonly SyntheticAiExecutionStore ai;
    private readonly SyntheticOutcomePriorityStore outcomes;
    public Phase1BEvaluationSourceAdapter(SyntheticDurableRunEngine runs, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes)
    {
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        this.ai = ai ?? throw new ArgumentNullException(nameof(ai));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }
    private static Phase1BEvaluationSourceResult Deny(Phase1BEvaluationSourceIssue issue) => new(issue, null);
    public async Task<Phase1BEvaluationSourceResult> CaptureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, AiAuthority aiAuthority, OutcomeAuthority outcomeAuthority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !ActiveTransaction(connection, transaction))
            return Deny(Phase1BEvaluationSourceIssue.InvalidInput);
        var supplied = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (supplied.Host is not ("127.0.0.1" or "localhost") || supplied.Port != 55433 || supplied.Username != "iga_synthetic" ||
            supplied.Database?.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal) != true)
            return Deny(Phase1BEvaluationSourceIssue.InvalidInput);
        if (aiAuthority is null || outcomeAuthority is null || aiAuthority.Roles.IsDefaultOrEmpty || outcomeAuthority.Roles.IsDefaultOrEmpty ||
            !aiAuthority.Roles.SequenceEqual([AiRole.Consultant]) ||
            !outcomeAuthority.Roles.SequenceEqual([OutcomeRole.Consultant]) || aiAuthority.ActorId != outcomeAuthority.ActorId ||
            AiExecutionPolicy.Authorize(aiAuthority, AiAction.Read) is not null ||
            OutcomePriorityPolicy.Authorize(outcomeAuthority, OutcomeScope.Fixed, OutcomeAction.ReadOutcome) is not null)
            return Deny(Phase1BEvaluationSourceIssue.Denied);
        try
        {
            try { await SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(connection, transaction, OutcomeScope.Fixed, cancellationToken); }
            catch (ArgumentException) { return Deny(Phase1BEvaluationSourceIssue.InvalidInput); }
            await SyntheticRunSourceFence.AcquireAsync(connection, transaction, AiScope.Fixed.CustomerId, AiScope.Fixed.ProjectId,
                AiScope.Fixed.EnvironmentId, runId, cancellationToken);
            var readRun = await runs.ReadInTransactionAsync(connection, transaction, DemoFixtureCatalog.Scope, runId, cancellationToken);
            if (readRun.Issue is { } runIssue) return Deny(Map(runIssue));
            var run = readRun.Snapshot;
            if (run is null || !DemoPhase1BCatalog.MatchesFrozenFixture(run) || run.State != SyntheticRunState.Scoring || run.CancelRequested ||
                run.CoverageSummary is null || !CoverageReconciler.Reconcile(run.Plan.ExpectedKeys, run.Results).IsComplete)
                return Deny(Phase1BEvaluationSourceIssue.SourceUnavailable);
            var readOutcome = await outcomes.ReadLockedAsync(connection, transaction, runId, outcomeAuthority, cancellationToken);
            if (readOutcome.Issue is { } outcomeIssue) return Deny(Map(outcomeIssue));
            var readAi = await ai.ReadInTransactionAsync(connection, transaction, aiAuthority, runId, cancellationToken);
            if (readAi.Issue is { } aiIssue) return Deny(Map(aiIssue));
            if (readOutcome.Lock is null || readAi.Value is null || readAi.Value.Works.Any(w => w.State is not (AiWorkState.Succeeded or AiWorkState.Failed)))
                return Deny(Phase1BEvaluationSourceIssue.SourceUnavailable);
            var analysis = SyntheticPhase1BAnalysisAdapter.Originals(run, readAi.Value, readOutcome.Lock);
            if (analysis is null) return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
            return Build(run, readAi.Value, readOutcome.Lock, analysis);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { return Deny(Phase1BEvaluationSourceIssue.NotInitialized); }
    }
    private static bool ActiveTransaction(NpgsqlConnection? connection, NpgsqlTransaction? transaction)
    {
        try
        {
            if (connection is null || transaction is null || connection.State != ConnectionState.Open || transaction.Connection != connection) return false;
            // Assignment validates Npgsql's completed/disposed transaction state without executing SQL.
            using var admission = new NpgsqlCommand { Connection = connection, Transaction = transaction };
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { return false; }
    }
    internal static Phase1BEvaluationSourceResult Build(SyntheticRunSnapshot run, AiExecutionSnapshot ai,
        LockedOutcomeSet locked, SyntheticAnalysisResult analysis)
    {
        var sourceOutcomes = ai.Works.SelectMany(w => w.Outcomes).ToArray();
        if (sourceOutcomes.Length != DemoPhase1BCatalog.AiKeys.Count || !sourceOutcomes.Select(o => o.Key).ToHashSet().SetEquals(DemoPhase1BCatalog.AiKeys))
            return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
        var originals = sourceOutcomes.Where(o => o.Finding is not null).Select(o => o.Finding!).ToArray();
        if (originals.Select(o => o.OccurrenceId).Distinct(StringComparer.Ordinal).Count() != originals.Length)
            return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
        var generated = analysis.Findings.Where(f => f.DetectionMethod == "AI").ToArray();
        if (generated.Length != originals.Length || !generated.Select(f => f.OccurrenceId).ToHashSet(StringComparer.Ordinal).SetEquals(originals.Select(o => o.OccurrenceId)))
            return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
        var members = new List<Phase1BEvaluationSourceMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in analysis.Groups)
        {
            var all = analysis.Findings.Where(f => group.OccurrenceIds.Contains(f.OccurrenceId)).ToArray();
            if (!all.Any(f => f.DetectionMethod == "AI")) continue;
            if (all.Length != group.OccurrenceIds.Length || all.Any(f => f.DetectionMethod != "AI") ||
                all.Select(f => f.ModuleId).Distinct(StringComparer.Ordinal).Count() != 1 ||
                all.Select(f => f.CategoryId).Distinct(StringComparer.Ordinal).Count() != 1 ||
                all.Select(f => f.Severity).Distinct().Count() != 1 ||
                all.Any(f => f.RootCauseKey != group.RootCauseKey || f.Provenance.RuleId != group.RuleId || f.Provenance.RuleVersion != group.RuleVersion))
                return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
            var occurrences = new List<Phase1BEvaluationSourceOccurrence>();
            foreach (var finding in all)
            {
                var original = originals.SingleOrDefault(o => o.OccurrenceId == finding.OccurrenceId);
                if (original is null || !seen.Add(finding.OccurrenceId) || original.OriginalDigest != finding.GeneratedOriginalDigest ||
                    original.Key.InventoryId != finding.ObjectId || original.RuleId != finding.Provenance.RuleId ||
                    original.RuleVersion != finding.Provenance.RuleVersion || original.ModuleId != finding.ModuleId || original.Category != finding.CategoryId)
                    return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
                occurrences.Add(new(finding.OccurrenceId, original.Key, AiExecutionCanonical.Serialize(original), original.OriginalDigest,
                    SourceCanonical.GeneratedJson(finding)));
            }
            members.Add(new(group.RootCauseKey, all.Select(f => f.ObjectId).Distinct(StringComparer.Ordinal).Count(), occurrences));
        }
        if (seen.Count != originals.Length || members.Select(m => m.GroupId).Distinct(StringComparer.Ordinal).Count() != members.Count)
            return Deny(Phase1BEvaluationSourceIssue.IntegrityMismatch);
        var orderedMembers = members.OrderBy(m => m.GroupId, StringComparer.Ordinal).ToArray();
        var gaps = sourceOutcomes.Where(o => o.Finding is null).OrderBy(o => o.Key.EvidenceCategory, StringComparer.Ordinal)
            .ThenBy(o => o.Key.InventoryId, StringComparer.Ordinal).Select(o => new Phase1BEvaluationSourceGap(o.Key, o.State, o.ReasonCode, o.Stage)).ToArray();
        var json = SourceCanonical.Json(new
        {
            schemaVersion = "synthetic-phase1b-evaluation-source-v1",
            scope = new { run.Scope.CustomerId, run.Scope.ProjectId, run.Scope.EnvironmentId },
            run.RunId,
            runRevision = run.Revision,
            run.CheckpointSequence,
            run.InputDigest,
            baselineId = run.BaselineCatalogId,
            profileId = run.ProfileCatalogId,
            runState = run.State,
            run.CancelRequested,
            runUpdatedAtUtc = run.UpdatedAt,
            observedAtDatabaseUtc = run.ObservedAtDatabaseUtc,
            frozenInputsJson = SourceCanonical.Json(run.FrozenInputs),
            aiRunLockJson = AiExecutionCanonical.Serialize(ai.RunLock),
            aiSnapshotJson = AiExecutionCanonical.Serialize(ai),
            aiSnapshotDigest = ai.ContentDigest,
            lockedOutcomeSetJson = OutcomePriorityCanonical.Json(locked),
            outcomeLockDigest = locked.ContentDigest,
            analysisDigest = analysis.ContentDigest,
            members = orderedMembers.Select(m => new
            {
                m.GroupId,
                m.AffectedObjectCount,
                occurrences = m.Occurrences.Select(o => new { o.OccurrenceId, key = o.CoverageKey, o.OriginalJson, o.OriginalDigest, o.GeneratedFindingJson })
            }),
            gaps = gaps.Select(g => new { key = g.CoverageKey, g.State, g.ReasonCode, g.Stage })
        });
        return new(null, new(run.RunId, run.Revision, run.InputDigest, ai.ContentDigest, locked.ContentDigest,
            analysis.ContentDigest, run.ObservedAtDatabaseUtc, orderedMembers, gaps, json));
    }
    private static Phase1BEvaluationSourceIssue Map(SyntheticRunIssue issue) => issue switch
    {
        SyntheticRunIssue.InvalidInput => Phase1BEvaluationSourceIssue.InvalidInput,
        SyntheticRunIssue.WrongScope => Phase1BEvaluationSourceIssue.Denied,
        SyntheticRunIssue.NotFound => Phase1BEvaluationSourceIssue.NotFound,
        SyntheticRunIssue.MigrationDrift => Phase1BEvaluationSourceIssue.MigrationDrift,
        SyntheticRunIssue.InvalidState => Phase1BEvaluationSourceIssue.NotInitialized,
        _ => Phase1BEvaluationSourceIssue.IntegrityMismatch
    };
    private static Phase1BEvaluationSourceIssue Map(AiIssue issue) => issue switch
    {
        AiIssue.InvalidInput => Phase1BEvaluationSourceIssue.InvalidInput,
        AiIssue.Denied or AiIssue.WrongScope => Phase1BEvaluationSourceIssue.Denied,
        AiIssue.NotFound => Phase1BEvaluationSourceIssue.NotFound,
        AiIssue.NotInitialized => Phase1BEvaluationSourceIssue.NotInitialized,
        AiIssue.MigrationDrift => Phase1BEvaluationSourceIssue.MigrationDrift,
        AiIssue.InvalidState or AiIssue.SourceConflict => Phase1BEvaluationSourceIssue.SourceUnavailable,
        _ => Phase1BEvaluationSourceIssue.IntegrityMismatch
    };
    private static Phase1BEvaluationSourceIssue Map(OutcomePriorityIssue issue) => issue switch
    {
        OutcomePriorityIssue.InvalidInput => Phase1BEvaluationSourceIssue.InvalidInput,
        OutcomePriorityIssue.Denied or OutcomePriorityIssue.WrongScope => Phase1BEvaluationSourceIssue.Denied,
        OutcomePriorityIssue.NotFound => Phase1BEvaluationSourceIssue.NotFound,
        OutcomePriorityIssue.NotInitialized => Phase1BEvaluationSourceIssue.NotInitialized,
        OutcomePriorityIssue.MigrationDrift => Phase1BEvaluationSourceIssue.MigrationDrift,
        OutcomePriorityIssue.InvalidState or OutcomePriorityIssue.SourceConflict or OutcomePriorityIssue.SourceUnavailable => Phase1BEvaluationSourceIssue.SourceUnavailable,
        _ => Phase1BEvaluationSourceIssue.IntegrityMismatch
    };
}
