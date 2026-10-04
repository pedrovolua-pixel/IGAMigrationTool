using SyntheticEvaluation;
using SyntheticEvaluationWorkflow;

namespace SyntheticEvaluationWorkflowFixtures;

/// <summary>Fictional immutable source; never a Phase1B or customer evidence adapter.</summary>
public static class FictionalEvaluationFixture
{
    public static EvaluationWorkflowSeed BuildSeed()
    {
        var cutoff = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var population = Enumerable.Range(0, 120).Select(i => new SamplingMember(
            $"synthetic-item-{i:D6}", "synthetic-scope", i < 60 ? "synthetic-env-a" : "synthetic-env-b",
            "synthetic-module", "synthetic-category", "synthetic-rule", "synthetic-model", "synthetic-confidence",
            i < 20 ? SamplingSeverity.Critical : i < 40 ? SamplingSeverity.High : SamplingSeverity.Medium,
            true, true, null, [])).ToArray();
        var versions = new SamplingVersionManifest(Enum.GetValues<SamplingVersionKind>()
            .Select(kind => new SamplingVersionBinding(kind, "synthetic-" + kind.ToString().ToLowerInvariant())).ToArray(),
            cutoff, cutoff.AddHours(1));
        var input = new SamplingInput("synthetic-sample", "synthetic-population", "synthetic-scope", new('1', 64), versions, population);
        var sample = EvaluationSampler.Build(input).Projection ?? throw new InvalidOperationException("Fictional fixture admission failed.");
        if (sample.PopulationDigest != "069dd6f848250dfa46be775b382d563f7979e15e6349a29f59d4770a55955aa1" ||
            sample.ContentDigest != "5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110" ||
            sample.Selected.Count != 100 || sample.Selected.Count(selection => selection.Mandatory) != 40 ||
            !sample.Strata.Select(stratum => stratum.Allocation).SequenceEqual(new[] { 15, 45 }))
            throw new InvalidOperationException("Fictional fixture differs from its independent immutable golden.");
        EvaluationReviewerScope Scope(string environment) => new("synthetic-customer", "synthetic-scope", environment,
            "synthetic-assessment", "synthetic-evaluation");
        EvaluationFixtureAssignment Assignment(string environment) => new(environment + "-assignment", 1,
            "synthetic-reviewer", EvaluationReviewerRole.Consultant, Scope(environment),
            new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EvaluationFixtureState.Active, true, "synthetic-qualification", "synthetic-assignment-authority",
            true, true, true, ["synthetic-category"], ["synthetic-category"], true, true);
        var registry = new EvaluationReviewerRegistryInput("synthetic-registry-v1", "synthetic-policy-v1", "synthetic-instruction-v1",
            [new("synthetic-reviewer", 1, true, EvaluationFixtureState.Active)],
            [Assignment("synthetic-env-a"), Assignment("synthetic-env-b")],
            sample.Selected.Select(selection => new EvaluationFixtureMember(selection.Member.Id, 1,
                Scope(selection.Member.EnvironmentId), selection.Member.CategoryId, EvaluationFixtureState.Active,
                true, true, true, true)).ToArray(),
            sample.Selected.Select(selection => new EvaluationFixtureConflict(selection.Member.EnvironmentId + "-assignment",
                selection.Member.Id, 1, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No,
                EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, null)).ToArray());
        var originals = sample.Selected.Select(selection => new EvaluationWorkflowOriginal(selection.Member.Id,
            "Fictional configuration observation " + selection.Member.Id,
            selection.Member.Severity.ToString(), "Fictional configuration",
            "This fictional source illustrates a configuration condition for evaluation practice.",
            "Inspect the permitted fictional reference and independently assess the original conclusion.",
            [selection.Member.Id + "-evidence"])).ToArray();
        return new(input, registry, originals);
    }
}
