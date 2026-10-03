using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SyntheticEvaluation;

internal static class Program
{
    private static int checks;
    private static readonly DateTimeOffset Cutoff = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static int Main()
    {
        try
        {
            Compose();
            Console.WriteLine($"PASS {checks} independent composed synthetic evaluation policy assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static SamplingMember Member(int i) => new($"synthetic-item-{i:D6}", "synthetic-scope",
        i < 60 ? "synthetic-env-a" : "synthetic-env-b", "synthetic-module", "synthetic-category",
        "synthetic-rule", "synthetic-model", "synthetic-confidence",
        i < 20 ? SamplingSeverity.Critical : i < 40 ? SamplingSeverity.High : SamplingSeverity.Medium,
        true, true, null, []);
    private static EvaluationReviewerScope Scope(string environment) =>
        new("synthetic-customer", "synthetic-scope", environment, "synthetic-assessment", "synthetic-evaluation");
    private static string AssignmentId(string environment) => environment + "-assignment";

    private static EvaluationFixtureAssignment Assignment(string environment) =>
        new(AssignmentId(environment), 1, "synthetic-reviewer", EvaluationReviewerRole.Consultant,
            Scope(environment), Cutoff.AddDays(-1), Cutoff.AddDays(1), EvaluationFixtureState.Active,
            true, "synthetic-qualification", "synthetic-assignment-authority", true, true, true,
            ["synthetic-category"], ["synthetic-category"], true, true);

    private static EvaluationReviewerRegistryInput RegistryInput(SamplingProjection sample) =>
        new("synthetic-registry-v1", "synthetic-policy-v1", "synthetic-instruction-v1",
            [new("synthetic-reviewer", 1, true, EvaluationFixtureState.Active)],
            [Assignment("synthetic-env-a"), Assignment("synthetic-env-b")],
            sample.Selected.Select(s => new EvaluationFixtureMember(s.Member.Id, 1, Scope(s.Member.EnvironmentId),
                s.Member.CategoryId, EvaluationFixtureState.Active, true, true, true, true)).ToArray(),
            sample.Selected.Select(s => new EvaluationFixtureConflict(AssignmentId(s.Member.EnvironmentId), s.Member.Id, 1,
                EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No,
                EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No,
                EvaluationConflictAnswer.No, null)).ToArray());

    private static EvaluationReviewerRequest Request(SamplingMember member, string registryVersion = "synthetic-registry-v1",
        EvaluationReviewerAction action = EvaluationReviewerAction.ScoredReview) =>
        new("synthetic-reviewer", AssignmentId(member.EnvironmentId), member.Id, Scope(member.EnvironmentId),
            action, Cutoff, registryVersion, 1, 1, 1, action == EvaluationReviewerAction.ScoredReview ? 1 : null);

    private static EvaluationProjection Accuracy(SamplingProjection sample, string version,
        IEnumerable<EvaluationReview> reviews, DateTimeOffset cutoff) =>
        EvaluationAccuracyBuilder.Build(new(new(version, sample.ScopeId, sample.PopulationDigest, sample.ContentDigest,
            version == "synthetic-baseline" ? sample.VersionManifestDigest : Hash(version), cutoff),
            sample.Selected.Select(s => new EvaluationMember(s.Member.Id, EvaluationTrack.GeneralAi)).ToArray(),
            reviews.ToArray())).Projection ?? throw new Exception("accuracy admission");

    private static EvaluationWarningProjection Warning(SamplingProjection sample, EvaluationProjection accuracy) =>
        EvaluationWarningBuilder.Build(new(accuracy,
            sample.Selected.Select(s => new EvaluationMemberMetadata(s.Member.Id,
                Enum.Parse<EvaluationOriginSeverity>(s.Member.Severity.ToString()), s.Member.EnvironmentId,
                s.Member.PrimaryModuleId, s.Member.CategoryId, s.Member.RuleVersion, s.Member.ModelPromptVersion,
                s.Member.ConfidenceBandId)).ToArray())).Projection ?? throw new Exception("warning admission");

    private static EvaluationRegressionResult Compare(SamplingProjection sample,
        EvaluationWarningProjection baseline, EvaluationWarningProjection candidate,
        EvaluationSafetyDeclarations? safety = null, EvaluationStratumAttribution[]? causes = null)
    {
        var locks = new EvaluationComparisonLocks("synthetic-set-v1", sample.PopulationDigest,
            Hash("synthetic-instructions"), Hash("synthetic-source"));
        var required = sample.Strata.Select(s => new EvaluationRequiredStratum(s.EnvironmentId, s.PrimaryModuleId,
            s.CategoryId, Enum.Parse<EvaluationOriginSeverity>(s.Severity.ToString()))).ToArray();
        return EvaluationRegressionBuilder.Build(new(baseline, candidate, locks, locks, required,
            safety ?? new(EvaluationSafetyAssessment.AssessedNoEvent, EvaluationSafetyAssessment.AssessedNoEvent,
                EvaluationSafetyAssessment.AssessedNoEvent, EvaluationSafetyAssessment.AssessedNoEvent), causes ?? []));
    }

    private static void Compose()
    {
        var population = Enumerable.Range(0, 120).Select(Member).ToArray();
        var versions = new SamplingVersionManifest(Enum.GetValues<SamplingVersionKind>()
            .Select(k => new SamplingVersionBinding(k, "synthetic-" + k.ToString().ToLowerInvariant())).ToArray(),
            Cutoff, Cutoff.AddHours(1));
        var input = new SamplingInput("synthetic-sample", "synthetic-population", "synthetic-scope",
            new('1', 64), versions, population);
        var sampleResult = EvaluationSampler.Build(input);
        Check(sampleResult.HasProjection, "sampling admitted");
        var sample = sampleResult.Projection!;
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "expected-v1.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        Check(sample.PopulationDigest == fixture.RootElement.GetProperty("populationDigest").GetString(), "independent population digest");
        Check(sample.ContentDigest == fixture.RootElement.GetProperty("sampleDigest").GetString(), "independent complete sample digest");
        Check(sample.Selected.Select(s => s.Member.Id).SequenceEqual(
            fixture.RootElement.GetProperty("expectedSelectedIds").EnumerateArray().Select(e => e.GetString()!)), "independent exact100 IDs");
        Check(sample.Selected.Count == 100 && sample.Selected.Count(s => s.Mandatory) == 40, "40 mandatory plus60 lower");
        Check(sample.Strata.Select(s => s.Allocation).SequenceEqual(new[] { 15, 45 }), "independent lower proportional allocations");
        Check(EvaluationSampler.Build(input with { HardReviewBudget = 99 }).Issue == SamplingIssue.InsufficientBudget, "no silent lower budget shrink");

        var registryInput = RegistryInput(sample);
        var registry = EvaluationReviewerPolicy.Capture(registryInput).Registry ?? throw new Exception("trusted registry admission");
        foreach (var selection in sample.Selected)
        {
            var decision = EvaluationReviewerPolicy.Decide(registry, Request(selection.Member));
            Check(decision.IsAuthorized && decision.AuthorizedContextSufficient == true, "qualified independent scoped fixture");
        }

        var mandatoryIds = sample.Selected.Where(s => s.Mandatory).Select(s => s.Member.Id).ToArray();
        var lowerIds = sample.Selected.Where(s => !s.Mandatory).Select(s => s.Member.Id).ToArray();
        var baselineRejected = mandatoryIds.Take(2).Concat(lowerIds.Take(3)).ToHashSet(StringComparer.Ordinal);
        var exactRejected = mandatoryIds.Take(4).Concat(lowerIds.Take(1)).ToHashSet(StringComparer.Ordinal);
        var declinedRejected = mandatoryIds.Take(5).ToHashSet(StringComparer.Ordinal);
        EvaluationReview[] Reviews(HashSet<string> rejected) => sample.Selected.Select(s =>
            new EvaluationReview(s.Member.Id, rejected.Contains(s.Member.Id) ? EvaluationReviewOutcome.Rejected : EvaluationReviewOutcome.Confirmed)).ToArray();
        var baseline = Accuracy(sample, "synthetic-baseline", Reviews(baselineRejected), Cutoff);
        var baselineDigest = baseline.ContentDigest;
        var baselineWarning = Warning(sample, baseline);
        Check(baseline.GeneralAi.Denominator == 100 && baseline.GeneralAi.Confirmed == 95, "baseline95/100");
        var exact = Warning(sample, Accuracy(sample, "synthetic-candidate-exact", Reviews(exactRejected), Cutoff.AddHours(2)));
        var exactComparison = Compare(sample, baselineWarning, exact).Projection!;
        Check(exactComparison.Status == EvaluationRegressionStatus.NoRegressionDetected, "exact5pp CH decline alone no block; independent later cutoff allowed");
        Check(exactComparison.Rules.Single(r => r.Rule == EvaluationRegressionRule.CriticalHighDecline).State == EvaluationRuleState.NoBlockDetected, "38/40 to36/40 exact boundary");
        var declined = Warning(sample, Accuracy(sample, "synthetic-candidate-decline", Reviews(declinedRejected), Cutoff.AddHours(3)));
        var declineComparison = Compare(sample, baselineWarning, declined).Projection!;
        Check(declineComparison.Status == EvaluationRegressionStatus.Blocked, "38/40 to35/40 material decline");
        Check(declineComparison.Rules.Single(r => r.Rule == EvaluationRegressionRule.OverallAccuracy).State == EvaluationRuleState.NoBlockDetected, "95% overall cannot hide CH regression");

        var target = sample.Selected[0].Member;
        var withoutContext = registryInput with
        {
            VersionId = "synthetic-registry-context",
            Members = registryInput.Members.Select(m => m.Id == target.Id ? m with { AuthorizedContextSufficient = false } : m).ToArray()
        };
        var contextRegistry = EvaluationReviewerPolicy.Capture(withoutContext).Registry!;
        var authorized = EvaluationReviewerPolicy.Decide(contextRegistry, Request(target, withoutContext.VersionId));
        Check(authorized.IsAuthorized && authorized.AuthorizedContextSufficient == false, "authorized insufficient context distinct from denial");
        var indeterminateReviews = sample.Selected.Select(s => new EvaluationReview(s.Member.Id,
            s.Member.Id == target.Id ? EvaluationReviewOutcome.Indeterminate : EvaluationReviewOutcome.Confirmed)).ToArray();
        var indeterminateProjection = Accuracy(sample, "synthetic-context-indeterminate", indeterminateReviews, Cutoff.AddMinutes(1));
        Check(indeterminateProjection.GeneralAi.Selected == 100 && indeterminateProjection.GeneralAi.Indeterminate == 1
            && indeterminateProjection.GeneralAi.Denominator == 99 && indeterminateProjection.GeneralAi.Unreviewed == 0,
            "authorized context insufficiency becomes Indeterminate without substitute");

        var unavailableInput = input with
        {
            SampleId = "synthetic-unavailable-sample",
            Members = population.Select(m => m.Id == target.Id
                ? m with { Available = false, UnavailableReason = "synthetic-source-expired" } : m).ToArray()
        };
        var unavailableSample = EvaluationSampler.Build(unavailableInput).Projection!;
        Check(unavailableSample.Selected.Count == 100 && unavailableSample.Selected.Any(s => s.Member.Id == target.Id
            && s.Mandatory && !s.Member.Available && s.Member.UnavailableReason == "synthetic-source-expired"),
            "unavailable mandatory member remains selected with frozen reason");
        var unavailableDigest = unavailableSample.ContentDigest;
        var unavailableRegistryInput = RegistryInput(unavailableSample) with { VersionId = "synthetic-registry-unavailable" };
        unavailableRegistryInput = unavailableRegistryInput with
        {
            Members = unavailableRegistryInput.Members.Select(m => m.Id == target.Id
                ? m with { ResourcePermits = false } : m).ToArray()
        };
        var unavailableRegistry = EvaluationReviewerPolicy.Capture(unavailableRegistryInput).Registry!;
        var unavailableDecision = EvaluationReviewerPolicy.Decide(unavailableRegistry,
            Request(target, unavailableRegistryInput.VersionId));
        Check(unavailableDecision.Issue == EvaluationReviewerIssue.ResourceDenied
            && unavailableDecision.AuthorizedContextSufficient is null, "unavailable fixture review denial carries no outcome/context");
        var unavailableReviews = unavailableSample.Selected.Select(s => new EvaluationReview(s.Member.Id,
            s.Member.Id == target.Id ? EvaluationReviewOutcome.Unreviewed : EvaluationReviewOutcome.Confirmed)).ToArray();
        var unavailableAccuracy = Accuracy(unavailableSample, "synthetic-unavailable-review", unavailableReviews, Cutoff.AddMinutes(2));
        Check(unavailableAccuracy.GeneralAi.Selected == 100 && unavailableAccuracy.GeneralAi.Unreviewed == 1
            && unavailableAccuracy.GeneralAi.Indeterminate == 0 && unavailableAccuracy.GeneralAi.Denominator == 99
            && unavailableSample.ContentDigest == unavailableDigest, "coordinator records unavailable same member Unreviewed; immutable sample unchanged");

        var conflicts = registryInput with
        {
            VersionId = "synthetic-registry-conflict",
            Conflicts = registryInput.Conflicts.Select(c => c.MemberId == target.Id
                ? c with { FindingOrAnswerOrCorrectionAuthor = EvaluationConflictAnswer.Yes } : c).ToArray()
        };
        var conflictRegistry = EvaluationReviewerPolicy.Capture(conflicts).Registry!;
        var denied = EvaluationReviewerPolicy.Decide(conflictRegistry, Request(target, conflicts.VersionId));
        Check(denied.Issue == EvaluationReviewerIssue.ConflictDenied && denied.AuthorizedContextSufficient is null, "material author cannot independently score");
        var excluded = sample.Selected.Select(s => new EvaluationReview(s.Member.Id,
            s.Member.Id == target.Id ? EvaluationReviewOutcome.Unreviewed : EvaluationReviewOutcome.Confirmed)).ToArray();
        var excludedProjection = Accuracy(sample, "synthetic-unreviewed", excluded, Cutoff.AddHours(4));
        Check(excludedProjection.GeneralAi.Selected == 100 && excludedProjection.GeneralAi.Unreviewed == 1
            && excludedProjection.GeneralAi.Denominator == 99, "coordinator retains denied member separately as Unreviewed; no replacement");
        var corrected = Reviews(baselineRejected).Select(r => r.MemberId == target.Id
            ? r with { Outcome = EvaluationReviewOutcome.Corrected, OriginatingClassification = EvaluationOriginClassification.Rejected } : r).ToArray();
        var correctedProjection = Accuracy(sample, "synthetic-corrected", corrected, Cutoff.AddHours(5));
        Check(correctedProjection.GeneralAi.Confirmed == 95 && correctedProjection.GeneralAi.Rejected == 5
            && correctedProjection.GeneralAi.Corrected == 1, "correction originating rejection counted once");

        var revokedInput = registryInput with
        {
            VersionId = "synthetic-registry-revoked",
            Identities = [registryInput.Identities[0] with { State = EvaluationFixtureState.Revoked }]
        };
        var revoked = EvaluationReviewerPolicy.Capture(revokedInput).Registry!;
        Check(EvaluationReviewerPolicy.Decide(revoked, Request(target)).Issue == EvaluationReviewerIssue.StaleVersion, "stale registry expectation denied");
        Check(EvaluationReviewerPolicy.Decide(revoked, Request(target, revokedInput.VersionId)).Issue == EvaluationReviewerIssue.IdentityDenied, "current revoked fixture denied");
        Check(baseline.ContentDigest == baselineDigest && sample.ContentDigest == fixture.RootElement.GetProperty("sampleDigest").GetString(), "prior frozen baseline/membership stable after correction/conflict/revocation");

        var lost = sample.Selected.Select(s => new EvaluationReview(s.Member.Id,
            s.Member.EnvironmentId == "synthetic-env-b" && !s.Mandatory
                ? EvaluationReviewOutcome.Unreviewed : EvaluationReviewOutcome.Confirmed)).ToArray();
        var lostWarning = Warning(sample, Accuracy(sample, "synthetic-loss", lost, Cutoff.AddHours(6)));
        var stratum = new EvaluationRequiredStratum("synthetic-env-b", "synthetic-module", "synthetic-category", EvaluationOriginSeverity.Medium);
        var caused = Compare(sample, baselineWarning, lostWarning, causes: [new(stratum, EvaluationCoverageCause.CandidateCaused)]).Projection!;
        Check(caused.Status == EvaluationRegressionStatus.Blocked, "candidate loss of required stratum blocks despite100% available accuracy");
        var external = Compare(sample, baselineWarning, lostWarning, causes: [new(stratum, EvaluationCoverageCause.ExternalEvidence)]).Projection!;
        Check(external.Status == EvaluationRegressionStatus.NotVerified, "external unavailable coverage cannot claim no regression");
        var emptyReviews = sample.Selected.Select(s => new EvaluationReview(s.Member.Id, EvaluationReviewOutcome.Unreviewed)).ToArray();
        var empty = Warning(sample, Accuracy(sample, "synthetic-empty", emptyReviews, Cutoff.AddHours(7)));
        var safety = new EvaluationSafetyDeclarations(EvaluationSafetyAssessment.AssessedNoEvent,
            EvaluationSafetyAssessment.Event, EvaluationSafetyAssessment.NotAssessed, EvaluationSafetyAssessment.NotAssessed);
        var blocked = Compare(sample, baselineWarning, empty, safety).Projection!;
        Check(blocked.Status == EvaluationRegressionStatus.Blocked
            && blocked.Rules.Any(r => r.State == EvaluationRuleState.NotVerified), "safety block at D0 preserves unavailable rules");

        var desired = EvaluationAccuracyBuilder.Build(new(new("synthetic-desired-evaluation", "synthetic-desired-scope",
            Hash("synthetic-desired-population"), Hash("synthetic-desired-sample"), Hash("synthetic-desired-versions"), Cutoff),
            [new("synthetic-goal-member", EvaluationTrack.ApprovedDesiredOutcome, "synthetic-approved-goal", EvaluationOutcomeApproval.CustomerApproved)],
            [new("synthetic-goal-member", EvaluationReviewOutcome.Confirmed)])).Projection!;
        var desiredWarning = EvaluationWarningBuilder.Build(new(desired,
            [new("synthetic-goal-member", EvaluationOriginSeverity.Low, "synthetic-env-a", "synthetic-module",
                "synthetic-category", "synthetic-rule", "synthetic-model", "synthetic-confidence")])).Projection!;
        Check(desiredWarning.DesiredOutcome.Counts.Confirmed == 1 && desiredWarning.DesiredOutcome.LowSampleWarning
            && desiredWarning.DesiredOutcome.Counts.ExceedsGeneralAiThreshold is null, "separate desired scope warning without general threshold");
        Check(baseline.GeneralAi.Confirmed == 95, "desired quality cannot repair general ratio");
    }
}
