using SyntheticEvaluation;

namespace SyntheticEvaluationWorkflow;

internal static class EvaluationWorkflowPolicy
{
    internal const long MaxRevision = 9007199254740991;
    internal const string PopulationDigest = "069dd6f848250dfa46be775b382d563f7979e15e6349a29f59d4770a55955aa1";
    internal const string SampleDigest = "5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110";
    internal static bool Reference(string? value) => value is { Length: >= 11 and <= 128 } &&
        value.StartsWith("synthetic-", StringComparison.Ordinal) &&
        value.AsSpan(10).ToArray().All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');
    internal static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Revision(long value) => value is >= 0 and <= MaxRevision;
    internal static bool Text(string? value) => value is { Length: >= 1 and <= 2000 } && !string.IsNullOrWhiteSpace(value) && !value.Contains('\0') && ValidUtf16(value);
    private static bool ValidUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i])) return false;
            }
            else if (char.IsLowSurrogate(value[i])) return false;
        }
        return true;
    }
    internal static bool References(IReadOnlyList<string>? values) => values is { Count: <= 16 } &&
        values.All(Reference) && values.SequenceEqual(values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    internal static bool Correction(EvaluationWorkflowCorrection? c) => c is not null &&
        new[] { c.Severity, c.Category, c.RootCause, c.Recommendation }.Any(v => v is not null) &&
        new[] { c.Severity, c.Category, c.RootCause, c.Recommendation }.All(v => v is null || Text(v));
    internal static bool Command(EvaluationWorkflowCommand? c)
    {
        if (c is null || c.EventId == Guid.Empty || !Reference(c.MemberId) || !Enum.IsDefined(c.Kind) ||
            !Revision(c.ExpectedAggregateRevision) || !Revision(c.ExpectedMemberRevision) ||
            !Reference(c.ExpectedRegistryVersionId) || !Digest(c.ExpectedSourceDigest) || !Digest(c.ExpectedSampleDigest) ||
            !Text(c.Reason) || !References(c.EvidenceReferenceIds) ||
            c.Outcome is { } outcome && !Enum.IsDefined(outcome) ||
            c.OriginatingClassification is { } origin && !Enum.IsDefined(origin)) return false;
        if (c.Kind == EvaluationWorkflowCommandKind.PresentationCorrection)
            return c.Outcome is null && c.OriginatingClassification is null && Correction(c.Correction);
        return c.Outcome switch
        {
            EvaluationReviewOutcome.Confirmed or EvaluationReviewOutcome.Rejected =>
                c.EvidenceReferenceIds.Count > 0 && c.OriginatingClassification is null && c.Correction is null,
            EvaluationReviewOutcome.Indeterminate => c.OriginatingClassification is null && c.Correction is null,
            EvaluationReviewOutcome.Corrected => c.OriginatingClassification is not null &&
                c.EvidenceReferenceIds.Count > 0 && Correction(c.Correction),
            _ => false
        };
    }
    internal static EvaluationWorkflowCommand Detach(EvaluationWorkflowCommand c) =>
        c with { EvidenceReferenceIds = EvaluationWorkflowCanonical.List(c.EvidenceReferenceIds), Correction = c.Correction is null ? null : c.Correction with { } };
    internal static (WorkflowSource Source, SamplingProjection Sample, EvaluationReviewerRegistryInput Registry) Seed(EvaluationWorkflowSeed? input)
    {
        if (input is null || input.Sampling is null || input.Registry is null || input.Originals is not { Count: 100 })
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        var sample = EvaluationSampler.Build(input.Sampling).Projection ??
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        if (sample.PopulationDigest != PopulationDigest || sample.ContentDigest != SampleDigest ||
            sample.Population.Count != 120 || sample.Selected.Count != 100)
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        var originals = EvaluationWorkflowCanonical.List(input.Originals.OrderBy(o => o?.MemberId, StringComparer.Ordinal).Select(o =>
        {
            if (o is null || !Reference(o.MemberId) || !Text(o.Title) || !Text(o.Severity) || !Text(o.Category) ||
                !Text(o.RootCause) || !Text(o.Recommendation) || !References(o.EvidenceReferenceIds) ||
                o.EvidenceReferenceIds.Count == 0) throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
            return o with { EvidenceReferenceIds = EvaluationWorkflowCanonical.List(o.EvidenceReferenceIds) };
        }));
        if (!originals.Select(o => o.MemberId).SequenceEqual(sample.Selected.Select(s => s.Member.Id).Order(StringComparer.Ordinal)))
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        var registry = EvaluationWorkflowCanonical.Registry(input.Registry);
        if (registry.Identities.Count != 1 || registry.Identities[0].Id != "synthetic-reviewer" ||
            !registry.Identities[0].Authenticated || registry.Identities[0].Revision != 1 ||
            registry.Identities[0].State != EvaluationFixtureState.Active ||
            registry.Assignments.Count != 2 || registry.Members.Count != 100 || registry.Conflicts.Count != 100)
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        foreach (var s in sample.Selected)
        {
            var m = registry.Members.SingleOrDefault(m => m.Id == s.Member.Id);
            var a = registry.Assignments.SingleOrDefault(a => a.Id == Assignment(s.Member.EnvironmentId));
            var conflict = registry.Conflicts.SingleOrDefault(c => c.MemberId == s.Member.Id && c.AssignmentId == a?.Id);
            if (m is null || a is null || conflict is null || m.Scope != Scope(s.Member.EnvironmentId) ||
                m.CategoryId != s.Member.CategoryId || m.Revision != 1 || m.State != EvaluationFixtureState.Active ||
                !m.ResourcePermits || !m.CustomerPolicyPermits || !m.LifecyclePermits || !m.AuthorizedContextSufficient ||
                a.Scope != m.Scope || a.IdentityId != "synthetic-reviewer" || a.Role != EvaluationReviewerRole.Consultant ||
                a.Revision != 1 || a.State != EvaluationFixtureState.Active || !a.Qualified ||
                !a.ScoredReviewGranted || !a.PresentationCorrectionGranted || !a.RelatedHistoryGranted ||
                !a.CustomerPolicyPermits || !a.LifecyclePermits ||
                !a.ReviewCategoryIds.SequenceEqual(new[] { "synthetic-category" }) ||
                !a.HistoryCategoryIds.SequenceEqual(new[] { "synthetic-category" }) ||
                a.ValidFromUtc != new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) ||
                a.ValidUntilUtc != new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero) ||
                conflict.Revision != 1 || conflict.FindingOrAnswerOrCorrectionAuthor != EvaluationConflictAnswer.No ||
                conflict.RuleOrCatalogContributor != EvaluationConflictAnswer.No ||
                conflict.AssessmentOrSourceContributor != EvaluationConflictAnswer.No ||
                conflict.ModelPromptOrProviderContributor != EvaluationConflictAnswer.No ||
                conflict.CustomerConclusionOrRemediationResponsible != EvaluationConflictAnswer.No ||
                conflict.OtherMaterialContributor != EvaluationConflictAnswer.No ||
                conflict.GeneralRelationship != EvaluationConflictAnswer.No || conflict.Clearance is not null)
                throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        }
        var sampling = sample.Population is not null ? input.Sampling with
        {
            Members = EvaluationWorkflowCanonical.List(sample.Population),
            Versions = sample.Versions with { Bindings = EvaluationWorkflowCanonical.List(sample.Versions.Bindings.OrderBy(b => b.Kind)) }
        } : throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        return (new("synthetic-evaluation-workflow-source-v1", "synthetic-scope", sampling, originals), sample, registry);
    }
    internal static string Assignment(string environment) => environment + "-assignment";
    internal static EvaluationReviewerScope Scope(string environment) =>
        new("synthetic-customer", "synthetic-scope", environment, "synthetic-assessment", "synthetic-evaluation");
    internal static EvaluationReviewerDecision Decide(EvaluationReviewerRegistry registry, EvaluationWorkflowActor actor,
        SamplingMember member, EvaluationReviewerAction action, DateTimeOffset atUtc)
    {
        var identity = registry.Identities.SingleOrDefault(i => i.Id == actor.IdentityId);
        var assignment = registry.Assignments.SingleOrDefault(a => a.Id == Assignment(member.EnvironmentId));
        var resource = registry.Members.SingleOrDefault(m => m.Id == member.Id);
        var conflict = registry.Conflicts.SingleOrDefault(c => c.AssignmentId == assignment?.Id && c.MemberId == member.Id);
        return EvaluationReviewerPolicy.Decide(registry, new(actor.IdentityId, Assignment(member.EnvironmentId), member.Id,
            Scope(member.EnvironmentId), action, atUtc, registry.VersionId, identity?.Revision ?? 1,
            assignment?.Revision ?? 1, resource?.Revision ?? 1,
            action == EvaluationReviewerAction.ScoredReview ? conflict?.Revision : null));
    }
    internal static bool Context(EvaluationWorkflowCommand command, EvaluationReviewerDecision decision)
    {
        if (!decision.IsAuthorized) return false;
        if (command.Kind == EvaluationWorkflowCommandKind.PresentationCorrection) return true;
        return decision.AuthorizedContextSufficient == (command.Outcome != EvaluationReviewOutcome.Indeterminate);
    }
    internal static WorkflowCurrent Apply(WorkflowCurrent prior, EvaluationWorkflowCommand command)
    {
        var outcome = command.Outcome ?? (prior.Outcome is EvaluationReviewOutcome.Confirmed or EvaluationReviewOutcome.Rejected
            ? EvaluationReviewOutcome.Corrected : prior.Outcome);
        var origin = command.Kind == EvaluationWorkflowCommandKind.Review ? command.OriginatingClassification :
            prior.Outcome == EvaluationReviewOutcome.Confirmed ? EvaluationOriginClassification.Confirmed :
            prior.Outcome == EvaluationReviewOutcome.Rejected ? EvaluationOriginClassification.Rejected : prior.OriginatingClassification;
        return new(prior.MemberId, prior.Revision + 1, outcome, origin, command.Correction);
    }
    internal static bool RegistryReplacement(EvaluationReviewerRegistryInput ceiling, EvaluationReviewerRegistryInput previous,
        EvaluationReviewerRegistryInput next)
    {
        if (next.VersionId == previous.VersionId || next.Identities.Count != 1 || next.Assignments.Count != 2 ||
            next.Members.Count != 100 || next.Conflicts.Count != 100) return false;
        foreach (var i in next.Identities)
        {
            var p = previous.Identities.SingleOrDefault(x => x.Id == i.Id);
            if (p is null || i.Revision == p.Revision && EvaluationWorkflowCanonical.Json(i) != EvaluationWorkflowCanonical.Json(p) || i.Revision < p.Revision || i.Revision > p.Revision + 1 || i.Revision > MaxRevision) return false;
        }
        foreach (var a in next.Assignments)
        {
            var c = ceiling.Assignments.SingleOrDefault(x => x.Id == a.Id);
            var p = previous.Assignments.SingleOrDefault(x => x.Id == a.Id);
            if (c is null || p is null || a.Revision == p.Revision && EvaluationWorkflowCanonical.Json(a) != EvaluationWorkflowCanonical.Json(p) || a.Scope != c.Scope || a.IdentityId != c.IdentityId || a.Role != c.Role ||
                a.AssignmentAuthorityId != c.AssignmentAuthorityId || a.Revision < p.Revision || a.Revision > p.Revision + 1 ||
                a.Revision > MaxRevision || a.Qualified && a.QualificationBasisId != c.QualificationBasisId ||
                a.ScoredReviewGranted && !c.ScoredReviewGranted || a.PresentationCorrectionGranted && !c.PresentationCorrectionGranted ||
                a.RelatedHistoryGranted && !c.RelatedHistoryGranted || a.ReviewCategoryIds.Except(c.ReviewCategoryIds).Any() ||
                a.HistoryCategoryIds.Except(c.HistoryCategoryIds).Any() || a.ValidFromUtc < c.ValidFromUtc || a.ValidUntilUtc > c.ValidUntilUtc)
                return false;
        }
        foreach (var m in next.Members)
        {
            var c = ceiling.Members.SingleOrDefault(x => x.Id == m.Id);
            var p = previous.Members.SingleOrDefault(x => x.Id == m.Id);
            if (c is null || p is null || m.Revision == p.Revision && EvaluationWorkflowCanonical.Json(m) != EvaluationWorkflowCanonical.Json(p) || m.Scope != c.Scope || m.CategoryId != c.CategoryId ||
                m.Revision < p.Revision || m.Revision > p.Revision + 1 || m.Revision > MaxRevision) return false;
        }
        foreach (var c in next.Conflicts)
        {
            var p = previous.Conflicts.SingleOrDefault(x => x.AssignmentId == c.AssignmentId && x.MemberId == c.MemberId);
            if (p is null || c.Revision == p.Revision && EvaluationWorkflowCanonical.Json(c) != EvaluationWorkflowCanonical.Json(p) || c.Revision < p.Revision || c.Revision > p.Revision + 1 || c.Revision > MaxRevision) return false;
        }
        return true;
    }
}
