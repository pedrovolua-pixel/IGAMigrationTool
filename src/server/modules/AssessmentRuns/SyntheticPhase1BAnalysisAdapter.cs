using System.Collections.Immutable;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentScoring;
using DeterministicAnalysis;
using SyntheticAiExecution;
using SyntheticOutcomePriority;

namespace AssessmentRuns;

/// <summary>Pure projection of one owning-module verified compound capture; never queries latest state or substitutes missing proofs.</summary>
public static class SyntheticPhase1BAnalysisAdapter
{
    public static SyntheticDemoAnalysisResponse Project(SyntheticRunSnapshot run, AiExecutionSnapshot ai, LockedOutcomeSet locked,
        IReadOnlyDictionary<string, ScoringFindingState>? reviewedStates = null, string? reviewSnapshotDigest = null)
    {
        var analysis = Originals(run, ai, locked);
        if (analysis is null) return Deny("phase1b_compound_source_denied");
        if (reviewedStates is not null && (reviewSnapshotDigest is not { Length: 64 } ||
            !reviewedStates.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(analysis.Groups.Select(g => g.RootCauseKey)))) return Deny("phase1b_review_denied");
        if (reviewedStates is not null && analysis.Findings.Any(f => f.InitialDisposition == SyntheticInitialDisposition.AutoConfirmed
            ? reviewedStates[f.RootCauseKey] != ScoringFindingState.AutoConfirmed
            : reviewedStates[f.RootCauseKey] is not (ScoringFindingState.Proposed or ScoringFindingState.Confirmed or ScoringFindingState.Rejected or ScoringFindingState.Deferred)))
            return Deny("phase1b_review_denied");
        var findings = analysis.Findings.ToDictionary(f => new CoverageKey(f.ObjectId, f.Provenance.RuleId), f => f);
        var units = analysis.Results.Select(result =>
        {
            var finding = result.Unit.RuleId.StartsWith("fixture-rule-", StringComparison.Ordinal)
                ? analysis.Findings.SingleOrDefault(f => f.ObjectId == result.Unit.ObjectId && f.Provenance.RuleId == result.Unit.RuleId)
                : findings.GetValueOrDefault(result.Unit.Key);
            var aiUnit = DemoPhase1BCatalog.AiKeys.Contains(result.Unit.Key);
            return new ScoringUnit(result.Unit.Key, result.Unit.CategoryId, result.Unit.ObjectType, result.Unit.ModuleId,
                locked.Outcomes.Where(o => o.Content.UnitLinks.Contains(result.Unit.Key)).Select(o => o.Content.OutcomeId).ToArray(),
                result.Coverage.State, result.Unit.Weight, finding is null ? null : Enum.Parse<ScoringSeverity>(finding.Severity.ToString()),
                aiUnit ? ScoringDetectionMethod.AI : ScoringDetectionMethod.Deterministic,
                finding is null ? null : reviewedStates?.GetValueOrDefault(finding.RootCauseKey) ?? Enum.Parse<ScoringFindingState>(finding.InitialDisposition.ToString()), aiUnit ? 80m : 100m);
        }).ToArray();
        var profile = new ScoringProfile(run.FrozenInputs.ProfileVersion, ScoringWeightMode.EqualAssessedCategories,
            new[] { new ScoringCategoryWeight("SECURITY", 1m), new ScoringCategoryWeight("OPERATIONS", 1m) });
        var versions = new ScoringVersions(PilotHealthScorer.AlgorithmVersion, PilotHealthScorer.InputSchemaVersion,
            run.Plan.BaselineId, analysis.FrozenInputs.CatalogVersion, run.FrozenInputs.ProfileVersion, run.InputDigest,
            SyntheticCanonicalDigest.Compute(new { analysisDigest = analysis.ContentDigest, reviewSnapshotDigest, outcomeLockDigest = locked.ContentDigest, aiLedgerDigest = ai.ContentDigest }));
        var outcomes = SyntheticOutcomePriorityStore.ToScoringOutcomes(locked);
        if (outcomes is null) return Deny("phase1b_outcome_proof_denied");
        var scored = PilotHealthScorer.Project(new(versions, profile, run.Plan.ExpectedKeys, run.Results, units, outcomes.Value));
        return scored.HasProjection ? new(null, new(analysis, scored.Projection!)) : Deny("phase1b_scoring_denied");
    }
    public static SyntheticAnalysisResult? Originals(SyntheticRunSnapshot run, AiExecutionSnapshot ai, LockedOutcomeSet locked)
    {
        if (!DemoPhase1BCatalog.MatchesFrozenFixture(run) || run.State != SyntheticRunState.Scoring || run.CancelRequested ||
            run.CoverageSummary is null || !CoverageReconciler.Reconcile(run.Plan.ExpectedKeys, run.Results).IsComplete ||
            locked.RunId != run.RunId || locked.ContentDigest != run.FrozenInputs.Phase1BLocks!.OutcomeLockDigest ||
            SyntheticOutcomePriorityStore.ValidateApplicability(locked, run.Plan.ExpectedKeys) is not null ||
            AiExecutionCanonical.Serialize(ai.RunLock) != AiExecutionCanonical.Serialize(DemoPhase1BAiFixture.RunLock(run)) ||
            AiExecutionCanonical.Serialize(ai.Works.Select(w => w.Work).ToImmutableArray()) != AiExecutionCanonical.Serialize(DemoPhase1BAiFixture.Works(run.RunId)) ||
            ai.ContentDigest != AiExecutionCanonical.Digest(ai with { ContentDigest = "" })) return null;
        var outcomes = ai.Works.SelectMany(w => w.Outcomes).ToArray();
        if (outcomes.Length != DemoPhase1BCatalog.AiKeys.Count || !outcomes.Select(o => o.Key).ToHashSet().SetEquals(DemoPhase1BCatalog.AiKeys)) return null;
        foreach (var outcome in outcomes)
            if (!run.Results.Contains(Coverage(outcome)) || outcome.Finding is { } f && (f.State != "Proposed" || f.DetectionMethod != "AI" ||
                f.ConfidencePercent != 80m || f.Weight != 1m || f.OriginalDigest != AiExecutionCanonical.Digest(f with { OriginalDigest = "" }))) return null;
        return OriginalsCore(run, outcomes, ai.Works.SelectMany(w => w.Work.Units).ToDictionary(u => u.Key));
    }
    public static SyntheticAnalysisResult? OriginalsForExport(SyntheticRunSnapshot run, AiExportVerification proof)
    {
        if (!DemoPhase1BCatalog.MatchesFrozenFixture(run) || run.State != SyntheticRunState.Scoring || run.CancelRequested ||
            !CoverageReconciler.Reconcile(run.Plan.ExpectedKeys, run.Results).IsComplete ||
            AiExecutionCanonical.Serialize(proof.RunLock) != AiExecutionCanonical.Serialize(DemoPhase1BAiFixture.RunLock(run)) ||
            proof.Units.Length != DemoPhase1BCatalog.AiKeys.Count || !proof.Units.Select(u => u.Key).ToHashSet().SetEquals(DemoPhase1BCatalog.AiKeys)) return null;
        var works = DemoPhase1BAiFixture.Works(run.RunId);
        var mappings = works.SelectMany(w => w.Units).ToDictionary(u => u.Key);
        var outcomes = new List<AiUnitOutcome>();
        foreach (var unit in proof.Units)
        {
            AiFindingOriginal? finding = null;
            if (unit.State == CoverageState.Finding)
            {
                if (unit.Attempt is null) return null;
                var work = works.Single(w => w.Units.Any(m => m.Key == unit.Key));
                var receipt = new FakeAiProvider().Lookup(new(unit.Attempt, work.Scenario, work.PacketInputJson, work.Units, false));
                var mapped = AiExecutionPolicy.Map(proof.RunLock, work, unit.Attempt, receipt.OutputJson);
                if (!mapped.Succeeded) return null;
                finding = mapped.Value.Single(o => o.Key == unit.Key).Finding;
                if (finding is null || finding.OccurrenceId != unit.OccurrenceId || finding.OriginalDigest != unit.OriginalDigest ||
                    finding.PacketDigest != unit.PacketDigest || finding.ProposalDigest != unit.ProposalDigest) return null;
            }
            else if (unit.OccurrenceId is not null || unit.OriginalDigest is not null || unit.PacketDigest is not null || unit.ProposalDigest is not null || unit.Attempt is not null) return null;
            var outcome = new AiUnitOutcome(unit.Key, unit.State, unit.ReasonCode, "AI", finding);
            if (!run.Results.Contains(Coverage(outcome))) return null;
            outcomes.Add(outcome);
        }
        return OriginalsCore(run, outcomes.ToArray(), mappings);
    }
    private static SyntheticAnalysisResult? OriginalsCore(SyntheticRunSnapshot run, AiUnitOutcome[] outcomes, Dictionary<CoverageKey, AiUnitMapping> aiMappings)
    {
        var detKeys = DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.ToHashSet();
        var det = SyntheticAnalysisEngine.Analyze(DemoPhase1BCatalog.DeterministicLock, run.RunId, run.Results.Where(r => detKeys.Contains(r.Key)).ToArray());
        if (!det.Succeeded) return null;
        var additional = outcomes.Select(o =>
        {
            var m = aiMappings[o.Key];
            var unit = new SyntheticAnalysisUnit($"{m.RuleId}:{m.Key.InventoryId}", m.Key, m.RuleId, m.RuleVersion, m.Key.InventoryId,
                "OPERATIONS", m.ModuleId, m.ObjectType, 1m, []);
            return new SyntheticAnalysisRuleResult(unit, Coverage(o), null, o.Finding?.PacketDigest ?? "synthetic-ai-gap");
        }).ToImmutableArray();
        var findings = det.Analysis!.Findings.Concat(outcomes.Where(o => o.Finding is not null).Select(o => Finding(run, o.Finding!))).ToImmutableArray();
        var groups = findings.GroupBy(f => f.RootCauseKey).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            new SyntheticRootCauseGroup(g.Key, g.First().Provenance.RuleId, g.First().Provenance.RuleVersion,
                g.Select(f => f.OccurrenceId).Order(StringComparer.Ordinal).ToImmutableArray(), g.Select(f => f.ObjectId).Order(StringComparer.Ordinal).ToImmutableArray())).ToImmutableArray();
        var combined = new SyntheticAnalysisResult(run.RunId, det.Analysis.FrozenInputs, det.Analysis.Results.AddRange(additional), findings, groups,
            SyntheticCanonicalDigest.Compute(run.Results.OrderBy(r => r.Key.EvidenceCategory, StringComparer.Ordinal).ThenBy(r => r.Key.InventoryId, StringComparer.Ordinal).ToArray()), "");
        return combined with { ContentDigest = SyntheticCanonicalDigest.Compute(combined) };
    }
    public static CoverageItem Coverage(AiUnitOutcome outcome) => new(outcome.Key, outcome.State, outcome.ReasonCode, outcome.Stage, outcome.Finding?.PacketDigest);
    private static SyntheticGeneratedFinding Finding(SyntheticRunSnapshot run, AiFindingOriginal original)
    {
        var frozen = DemoPhase1BCatalog.DeterministicLock;
        var group = SyntheticCanonicalDigest.Compute(new { schemaVersion = "synthetic-phase1b-ai-root-v1", run.Scope, original.RuleId, original.RuleVersion, original.RootCause });
        var provenance = new SyntheticFindingProvenance(run.RunId, frozen.PackVersion, frozen.PackDigest, frozen.CatalogVersion, frozen.CatalogDigest,
            original.RuleId, original.RuleVersion, run.BaselineCatalogId, "synthetic-phase1b-evidence-v1", original.PacketDigest,
            original.PacketDigest, frozen.Scope, frozen.Compatibility);
        using var proposal = JsonDocument.Parse(original.ProposalCanonicalJson);
        string Claims(string field) => string.Join(" ", proposal.RootElement.GetProperty(field).EnumerateArray().Select(s => s.GetProperty("text").GetString()));
        return new(original.OccurrenceId, group, original.Title, original.Category, original.Key.InventoryId, original.ObjectType, original.ModuleId, [], "AI",
            provenance, [], "Untrusted model-labelled statements: " + Claims("facts") + " " + Claims("inferences"),
            ["Fictional fixed provider only; no customer validation."], Enum.Parse<SyntheticSeverity>(original.Severity.ToString()), 80m,
            "Fictional fixed confidence", original.Impact, original.Likelihood, original.RootCause, "fixture-guidance:phase1b-ai-v1",
            [new("inspect-fictional-configuration", "Inspect the fictional setting and dependencies.", "Confirm the fictional assumptions.", "No operational instruction is approved.", "Use fictional reversible planning only.")],
            ["Reassess against approved evidence; no execution is enabled."], ["Untrusted proposed finding; requires Consultant review."],
            SyntheticInitialDisposition.Proposed, "Every AI severity requires review.", original.OriginalDigest);
    }
    private static SyntheticDemoAnalysisResponse Deny(string reason) => new(reason, null);
}
