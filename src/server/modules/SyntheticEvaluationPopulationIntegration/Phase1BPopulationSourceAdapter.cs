using System.Text.Json;
using AssessmentCoverage;
using AssessmentRuns;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationSourceIntegration;
using SyntheticOutcomePriority;

namespace SyntheticEvaluationPopulationIntegration;

/// <summary>Detached saved-source readiness only. The caller owns the supplied transaction and current read authorities.</summary>
public sealed class Phase1BPopulationSourceAdapter
{
    private readonly SyntheticDurableRunEngine runs;
    private readonly Phase1BEvaluationSourceAdapter source;
    public Phase1BPopulationSourceAdapter(SyntheticDurableRunEngine runs, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes)
    {
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        source = new(runs, ai, outcomes);
    }
    private static Phase1BPopulationResult Deny(Phase1BPopulationIssue issue) => new(issue, null);
    public async Task<Phase1BPopulationResult> CaptureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, AiAuthority aiAuthority, OutcomeAuthority outcomeAuthority, CancellationToken cancellationToken = default)
    {
        var captured = await source.CaptureAsync(connection, transaction, runId, aiAuthority, outcomeAuthority, cancellationToken);
        if (captured.Issue is { } issue) return Deny(Enum.Parse<Phase1BPopulationIssue>(issue.ToString()));
        if (captured.Capture is null) return Deny(Phase1BPopulationIssue.SourceUnavailable);
        try
        {
            var read = await runs.ReadInTransactionAsync(connection, transaction, DemoFixtureCatalog.Scope, runId, cancellationToken);
            if (read.Issue is { } runIssue) return Deny(runIssue switch
            {
                SyntheticRunIssue.InvalidInput => Phase1BPopulationIssue.InvalidInput,
                SyntheticRunIssue.WrongScope => Phase1BPopulationIssue.Denied,
                SyntheticRunIssue.NotFound => Phase1BPopulationIssue.NotFound,
                SyntheticRunIssue.MigrationDrift => Phase1BPopulationIssue.MigrationDrift,
                SyntheticRunIssue.InvalidState => Phase1BPopulationIssue.NotInitialized,
                _ => Phase1BPopulationIssue.IntegrityMismatch
            });
            if (read.Snapshot is null || !DemoPhase1BCatalog.MatchesFrozenFixture(read.Snapshot) || read.Snapshot.State != SyntheticRunState.Scoring ||
                read.Snapshot.CancelRequested || read.Snapshot.CoverageSummary is null || !CoverageReconciler.Reconcile(read.Snapshot.Plan.ExpectedKeys, read.Snapshot.Results).IsComplete)
                return Deny(Phase1BPopulationIssue.SourceUnavailable);
            return Build(captured.Capture, read.Snapshot);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { return Deny(Phase1BPopulationIssue.NotInitialized); }
    }
    internal static Phase1BPopulationResult Build(Phase1BEvaluationSourceCapture captured, SyntheticRunSnapshot run)
    {
        try
        {
            using var sourceDocument = JsonDocument.Parse(captured.CanonicalJson);
            var c = sourceDocument.RootElement;
            var frozenJson = c.GetProperty("frozenInputsJson").GetString()!;
            if (captured.ContentDigest != PopulationCanonical.Hash(captured.CanonicalJson) || captured.RunId != run.RunId || captured.RunRevision != run.Revision ||
                captured.InputDigest != run.InputDigest || c.GetProperty("runId").GetGuid() != run.RunId || c.GetProperty("runRevision").GetInt64() != run.Revision ||
                c.GetProperty("inputDigest").GetString() != run.InputDigest || c.GetProperty("baselineId").GetString() != run.BaselineCatalogId ||
                c.GetProperty("profileId").GetString() != run.ProfileCatalogId || c.GetProperty("runState").GetString() != run.State.ToString() ||
                c.GetProperty("cancelRequested").GetBoolean() != run.CancelRequested || c.GetProperty("checkpointSequence").GetInt64() != run.CheckpointSequence ||
                c.GetProperty("runUpdatedAtUtc").GetDateTimeOffset() != run.UpdatedAt || frozenJson != AiExecutionCanonical.Serialize(run.FrozenInputs) ||
                c.GetProperty("observedAtDatabaseUtc").GetDateTimeOffset() != captured.ObservedAtDatabaseUtc ||
                SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, run.FrozenInputs, run.BaselineCatalogId, run.ProfileCatalogId) != run.InputDigest)
                return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            var scopeNode = c.GetProperty("scope");
            var scope = new AiScope(scopeNode.GetProperty("customerId").GetString()!, scopeNode.GetProperty("projectId").GetString()!, scopeNode.GetProperty("environmentId").GetString()!);
            if (scope.CustomerId != run.Scope.CustomerId || scope.ProjectId != run.Scope.ProjectId || scope.EnvironmentId != run.Scope.EnvironmentId)
                return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            using var frozenDocument = JsonDocument.Parse(frozenJson);
            using var lockDocument = JsonDocument.Parse(c.GetProperty("aiRunLockJson").GetString()!);
            using var aiDocument = JsonDocument.Parse(c.GetProperty("aiSnapshotJson").GetString()!);
            var l = lockDocument.RootElement;
            if (l.GetProperty("runId").GetGuid() != run.RunId || l.GetProperty("inputDigest").GetString() != run.InputDigest ||
                l.GetProperty("profileId").GetString() != run.ProfileCatalogId ||
                PopulationCanonical.Encode(l) != PopulationCanonical.Encode(aiDocument.RootElement.GetProperty("runLock")))
                return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            var planJson = JsonSerializer.Serialize(run.Plan); var capabilityJson = JsonSerializer.Serialize(run.Plan.CapabilityLock);
            using var planDocument = JsonDocument.Parse(planJson);
            var documents = new List<PopulationNativeDocument> { new("C", c), new("F", frozenDocument.RootElement), new("L", l), new("A", aiDocument.RootElement), new("P", planDocument.RootElement) };
            var works = aiDocument.RootElement.GetProperty("works").EnumerateArray().ToArray();
            if (works.Length == 0) return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            for (var w = 0; w < works.Length; w++)
            {
                using var input = JsonDocument.Parse(works[w].GetProperty("work").GetProperty("packetInputJson").GetString()!);
                documents.Add(new("I[" + w + "]", input.RootElement.Clone()));
            }
            var references = new PopulationReferences(scope);
            var scopeId = references.Add("scope"); var environmentId = references.Add("environment", scope.EnvironmentId);
            var members = new List<Phase1BPopulationMember>();
            var sourceMembers = c.GetProperty("members").EnumerateArray().ToArray();
            if (sourceMembers.Length != captured.Members.Count) return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            for (var m = 0; m < captured.Members.Count; m++)
            {
                var member = captured.Members[m];
                var emitted = sourceMembers[m]; var emittedOccurrences = emitted.GetProperty("occurrences").EnumerateArray().ToArray();
                if (emitted.GetProperty("groupId").GetString() != member.GroupId || emitted.GetProperty("affectedObjectCount").GetInt32() != member.AffectedObjectCount ||
                    emittedOccurrences.Length != member.Occurrences.Count || member.Occurrences.Count == 0 || member.AffectedObjectCount < 1)
                    return Deny(Phase1BPopulationIssue.IntegrityMismatch);
                NativeTuple? expected = null;
                for (var o = 0; o < member.Occurrences.Count; o++)
                {
                    var occurrence = member.Occurrences[o]; var raw = emittedOccurrences[o];
                    if (raw.GetProperty("occurrenceId").GetString() != occurrence.OccurrenceId || raw.GetProperty("originalJson").GetString() != occurrence.OriginalJson ||
                        raw.GetProperty("originalDigest").GetString() != occurrence.OriginalDigest || raw.GetProperty("generatedFindingJson").GetString() != occurrence.GeneratedFindingJson ||
                        raw.GetProperty("key").GetProperty("inventoryId").GetString() != occurrence.CoverageKey.InventoryId ||
                        raw.GetProperty("key").GetProperty("evidenceCategory").GetString() != occurrence.CoverageKey.EvidenceCategory)
                        return Deny(Phase1BPopulationIssue.IntegrityMismatch);
                    using var original = JsonDocument.Parse(occurrence.OriginalJson); using var generated = JsonDocument.Parse(occurrence.GeneratedFindingJson);
                    var native = Tuple(original.RootElement, generated.RootElement, occurrence, member.GroupId);
                    if (expected is not null && expected != native) return Deny(Phase1BPopulationIssue.IntegrityMismatch);
                    expected = native;
                    documents.Add(new("O[" + m + "," + o + "]", original.RootElement.Clone())); documents.Add(new("G[" + m + "," + o + "]", generated.RootElement.Clone()));
                }
                var tuple = expected!;
                members.Add(new(references.Add("member", member.GroupId), scopeId, environmentId, references.Add("module", tuple.Module),
                    references.Add("category", tuple.Category), references.Add("rule-version", tuple.Rule, tuple.RuleVersion),
                    references.Add("model-prompt", l.GetProperty("providerVersion").GetString()!, l.GetProperty("promptVersion").GetString()!),
                    references.Add("confidence-band", tuple.ConfidenceBand), tuple.Severity, member.GroupId, tuple.Module, tuple.Category,
                    tuple.Rule, tuple.RuleVersion, tuple.ConfidenceBand, tuple.Confidence, member.AffectedObjectCount, member.Occurrences));
            }
            if (members.Select(m => m.MemberId).Distinct(StringComparer.Ordinal).Count() != members.Count ||
                members.SelectMany(m => m.Occurrences).Select(o => o.OccurrenceId).Distinct(StringComparer.Ordinal).Count() != members.Sum(m => m.Occurrences.Count))
                return Deny(Phase1BPopulationIssue.IntegrityMismatch);
            var bindings = PopulationInventory.Build(documents, references);
            return new(null, new(run.RunId, run.Revision, captured.ContentDigest, captured.CanonicalJson, planJson, capabilityJson, frozenJson,
                captured.ObservedAtDatabaseUtc, run.ObservedAtDatabaseUtc, scopeId, environmentId, members, captured.Gaps, bindings));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or OverflowException or KeyNotFoundException)
        { return Deny(Phase1BPopulationIssue.IntegrityMismatch); }
    }
    private sealed record NativeTuple(string Module, string Category, string Rule, string RuleVersion, SamplingSeverity Severity, string ConfidenceBand, decimal Confidence);
    private static NativeTuple Tuple(JsonElement original, JsonElement generated, Phase1BEvaluationSourceOccurrence occurrence, string group)
    {
        var provenance = generated.GetProperty("Provenance");
        var module = original.GetProperty("moduleId").GetString()!; var category = original.GetProperty("category").GetString()!;
        var rule = original.GetProperty("ruleId").GetString()!; var version = original.GetProperty("ruleVersion").GetString()!;
        var severity = original.GetProperty("severity").GetString()!; var confidence = original.GetProperty("confidencePercent").GetDecimal();
        if (!Enum.TryParse<SamplingSeverity>(severity, false, out var parsed) || !Enum.IsDefined(parsed) ||
            generated.GetProperty("ModuleId").GetString() != module || generated.GetProperty("CategoryId").GetString() != category ||
            generated.GetProperty("OccurrenceId").GetString() != occurrence.OccurrenceId || generated.GetProperty("RootCauseKey").GetString() != group ||
            original.GetProperty("occurrenceId").GetString() != occurrence.OccurrenceId || provenance.GetProperty("RuleId").GetString() != rule ||
            provenance.GetProperty("RuleVersion").GetString() != version || generated.GetProperty("Severity").GetInt32() != (int)parsed ||
            generated.GetProperty("ConfidencePercent").GetDecimal() != confidence || original.GetProperty("originalDigest").GetString() != occurrence.OriginalDigest ||
            generated.GetProperty("GeneratedOriginalDigest").GetString() != occurrence.OriginalDigest || generated.GetProperty("DetectionMethod").GetString() != "AI" ||
            original.GetProperty("detectionMethod").GetString() != "AI") throw new InvalidOperationException("Inconsistent native finding tuple.");
        return new(module, category, rule, version, parsed, generated.GetProperty("ConfidenceBand").GetString()!, confidence);
    }
}
