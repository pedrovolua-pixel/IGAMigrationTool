using System.Collections.Immutable;
using AssessmentCoverage;

namespace DeterministicAnalysis;

/// <summary>Read-only exact-lock projection from complete saved synthetic coverage; independent of run storage/scoring.</summary>
public static class SyntheticAnalysisEngine
{
    public static SyntheticAnalysisIssue? ValidateLock(SyntheticAnalysisLock? frozen)
    {
        if (frozen is null) return SyntheticAnalysisIssue.InvalidInput;
        if (frozen.Scope != SyntheticAnalysisFixturePack.Scope) return SyntheticAnalysisIssue.WrongScope;
        if (!SyntheticAnalysisFixturePack.Presets.Any(preset => preset.Id == frozen.PresetId)) return SyntheticAnalysisIssue.UnknownPreset;
        if (!SyntheticAnalysisFixturePack.Profiles.Any(profile => profile.Id == frozen.ProfileId)) return SyntheticAnalysisIssue.UnknownProfile;
        var exact = SyntheticAnalysisFixturePack.Freeze(frozen.Scope, frozen.PresetId, frozen.ProfileId);
        return frozen != exact ? SyntheticAnalysisIssue.FrozenInputMismatch : null;
    }

    public static SyntheticAnalysisPlan Plan(SyntheticAnalysisLock frozen)
    {
        if (ValidateLock(frozen) is { } issue) throw new ArgumentException($"Synthetic analysis lock denied: {issue}.", nameof(frozen));
        var preset = SyntheticAnalysisFixturePack.GetPreset(frozen.PresetId);
        var units = ImmutableArray.CreateBuilder<SyntheticAnalysisUnit>();
        var results = ImmutableArray.CreateBuilder<SyntheticAnalysisRuleResult>();
        foreach (var rule in SyntheticAnalysisFixturePack.Rules.OrderBy(rule => rule.Id, StringComparer.Ordinal))
        {
            foreach (var evidence in preset.Objects.Where(evidence => evidence.ModuleId == rule.ModuleId)
                .OrderBy(evidence => evidence.ObjectId, StringComparer.Ordinal))
            {
                var key = new CoverageKey(evidence.ObjectId, rule.Id);
                var unit = new SyntheticAnalysisUnit($"{rule.Id}:{evidence.ObjectId}", key, rule.Id, rule.Version,
                    evidence.ObjectId, rule.CategoryId, rule.ModuleId, evidence.ObjectType, rule.Weight, []);
                var evaluation = SyntheticRuleEvaluator.Evaluate(rule, evidence);
                var coverage = new CoverageItem(key, evaluation.State, evaluation.ReasonCode,
                    evaluation.State is CoverageState.Pass or CoverageState.Finding ? null : evaluation.ResponsibleStage,
                    evidence.EvidenceReference);
                units.Add(unit);
                results.Add(new(unit, coverage, evaluation.ObservedFact, evidence.EvidenceReference));
            }
        }
        var frozenResults = results.ToImmutable();
        return new(frozen, units.ToImmutable(), preset.Objects, frozenResults, SyntheticCanonicalDigest.Compute(frozenResults));
    }

    public static SyntheticAnalysisResponse Analyze(SyntheticAnalysisLock? frozen, Guid runId, IReadOnlyCollection<CoverageItem>? savedCoverage)
    {
        if (ValidateLock(frozen) is { } issue) return new(issue, null);
        if (runId == Guid.Empty || savedCoverage is null) return new(SyntheticAnalysisIssue.InvalidInput, null);
        var plan = Plan(frozen!);
        if (!CoverageReconciler.Reconcile(plan.ExpectedKeys, savedCoverage).IsComplete)
            return new(SyntheticAnalysisIssue.IncompleteCoverage, null);
        var canonical = savedCoverage.OrderBy(item => item.Key.EvidenceCategory, StringComparer.Ordinal)
            .ThenBy(item => item.Key.InventoryId, StringComparer.Ordinal).ToImmutableArray();
        if (!canonical.SequenceEqual(plan.ExpectedResults)) return new(SyntheticAnalysisIssue.ResultMismatch, null);
        var findings = plan.Results.Where(result => result.Coverage.State == CoverageState.Finding)
            .Select(result => Finding(frozen!, runId, result)).ToImmutableArray();
        var groups = findings.GroupBy(finding => finding.RootCauseKey).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SyntheticRootCauseGroup(group.Key, group.First().Provenance.RuleId,
                group.First().Provenance.RuleVersion, group.Select(finding => finding.OccurrenceId).ToImmutableArray(),
                group.Select(finding => finding.ObjectId).ToImmutableArray())).ToImmutableArray();
        var result = new SyntheticAnalysisResult(runId, frozen!, plan.Results, findings, groups, SyntheticCanonicalDigest.Compute(canonical), "");
        return new(null, result with { ContentDigest = SyntheticCanonicalDigest.Compute(result) });
    }

    private static SyntheticGeneratedFinding Finding(SyntheticAnalysisLock frozen, Guid runId, SyntheticAnalysisRuleResult result)
    {
        var rule = SyntheticAnalysisFixturePack.Rules.Single(rule => rule.Id == result.Unit.RuleId);
        var rootCauseKey = SyntheticCanonicalDigest.Compute(new { frozen.Scope, rule.Id, rule.Version, rule.RootCause });
        var occurrenceId = SyntheticCanonicalDigest.Compute(new { runId, frozen.Scope, frozen.PresetId, frozen.EvidenceDigest, rule.Id, rule.Version, result.Unit.ObjectId });
        var provenance = new SyntheticFindingProvenance(runId, frozen.PackVersion, frozen.PackDigest, frozen.CatalogVersion,
            frozen.CatalogDigest, rule.Id, rule.Version, frozen.PresetId, frozen.PresetVersion,
            frozen.EvidenceDigest, result.EvidenceReference, frozen.Scope, frozen.Compatibility);
        var finding = new SyntheticGeneratedFinding(occurrenceId, rootCauseKey, rule.Title, rule.CategoryId,
            result.Unit.ObjectId, result.Unit.ObjectType, result.Unit.ModuleId, [], "Deterministic", provenance,
            [new(result.EvidenceReference, result.ObservedFact!.Kind, result.ObservedFact.Count!.Value,
                $"Fixed synthetic {result.ObservedFact.Kind} count exceeds {rule.Threshold}.")],
            "The typed fixture condition is present. This establishes no actual One Identity defect or causal relationship.",
            rule.Assumptions, rule.Severity, 100m, "Deterministic synthetic evidence", rule.Impact, rule.Likelihood,
            rule.RootCause, rule.GuidanceReference, rule.Options, rule.ValidationGuidance, rule.Limitations,
            rule.Severity is SyntheticSeverity.Critical or SyntheticSeverity.High || !rule.AutoConfirm
                ? SyntheticInitialDisposition.Proposed : SyntheticInitialDisposition.AutoConfirmed,
            rule.Severity is SyntheticSeverity.Critical or SyntheticSeverity.High
                ? "Mandatory human review pending; no disposition operation is enabled."
                : "Explicit fixed synthetic catalog auto-confirm policy; no customer authority is granted.", "");
        return finding with { GeneratedOriginalDigest = SyntheticCanonicalDigest.Compute(finding) };
    }
}
