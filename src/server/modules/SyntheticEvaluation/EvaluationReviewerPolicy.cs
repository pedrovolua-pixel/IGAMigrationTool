namespace SyntheticEvaluation;

/// <summary>Pure eligibility against the current supplied synthetic fixture snapshot.</summary>
public static class EvaluationReviewerPolicy
{
    private const int MaximumEntries = 100_000;

    public static EvaluationReviewerCaptureResult Capture(EvaluationReviewerRegistryInput? input)
    {
        if (input is null || !WithinBound(input.Identities) || !WithinBound(input.Assignments)
            || !WithinBound(input.Members) || !WithinBound(input.Conflicts))
            return CaptureDeny(EvaluationReviewerIssue.InvalidInput);
        var identities = input.Identities.ToArray();
        var assignments = input.Assignments.ToArray();
        var members = input.Members.ToArray();
        var conflicts = input.Conflicts.ToArray();
        if (!WithinBound(identities) || !WithinBound(assignments) || !WithinBound(members) || !WithinBound(conflicts)
            || identities.Any(x => x is null) || assignments.Any(x => x is null) || members.Any(x => x is null)
            || conflicts.Any(x => x is null)) return CaptureDeny(EvaluationReviewerIssue.InvalidInput);
        if (!Reference(input.VersionId) || !Reference(input.PolicyVersionId) || !Reference(input.InstructionVersionId))
            return CaptureDeny(EvaluationReviewerIssue.InvalidReference);

        var identityIds = new HashSet<string>(StringComparer.Ordinal);
        var assignmentsById = new Dictionary<string, EvaluationFixtureAssignment>(StringComparer.Ordinal);
        var membersById = new Dictionary<string, EvaluationFixtureMember>(StringComparer.Ordinal);
        var conflictIds = new HashSet<(string, string)>();
        long categoryCount = 0;
        for (var i = 0; i < identities.Length; i++)
        {
            var identity = identities[i];
            if (!Reference(identity.Id)) return CaptureDeny(EvaluationReviewerIssue.InvalidReference);
            if (identity.Revision <= 0) return CaptureDeny(EvaluationReviewerIssue.InvalidVersion);
            if (!Enum.IsDefined(identity.State)) return CaptureDeny(EvaluationReviewerIssue.InvalidRegistry);
            if (!identityIds.Add(identity.Id)) return CaptureDeny(EvaluationReviewerIssue.DuplicateEntry);
            identities[i] = identity with { };
        }
        for (var i = 0; i < assignments.Length; i++)
        {
            var assignment = assignments[i];
            if (assignment.Scope is null || !WithinBound(assignment.ReviewCategoryIds) || !WithinBound(assignment.HistoryCategoryIds))
                return CaptureDeny(EvaluationReviewerIssue.InvalidInput);
            var reviewCategories = assignment.ReviewCategoryIds.ToArray();
            var historyCategories = assignment.HistoryCategoryIds.ToArray();
            categoryCount += reviewCategories.LongLength + historyCategories.LongLength;
            if (categoryCount > MaximumEntries || !WithinBound(reviewCategories) || !WithinBound(historyCategories))
                return CaptureDeny(EvaluationReviewerIssue.InvalidInput);
            if (!Reference(assignment.Id) || !Reference(assignment.IdentityId) || !ScopeValid(assignment.Scope)
                || !Reference(assignment.AssignmentAuthorityId)
                || assignment.QualificationBasisId is not null && !Reference(assignment.QualificationBasisId)
                || reviewCategories.Any(x => !Reference(x)) || historyCategories.Any(x => !Reference(x)))
                return CaptureDeny(EvaluationReviewerIssue.InvalidReference);
            if (assignment.Revision <= 0 || assignment.ValidFromUtc.Offset != TimeSpan.Zero
                || assignment.ValidUntilUtc.Offset != TimeSpan.Zero || assignment.ValidUntilUtc <= assignment.ValidFromUtc)
                return CaptureDeny(EvaluationReviewerIssue.InvalidVersion);
            if (!Enum.IsDefined(assignment.Role) || !Enum.IsDefined(assignment.State)
                || assignment.Qualified != (assignment.QualificationBasisId is not null) || !identityIds.Contains(assignment.IdentityId))
                return CaptureDeny(EvaluationReviewerIssue.InvalidRegistry);
            if (!assignmentsById.TryAdd(assignment.Id, assignment)
                || reviewCategories.Distinct(StringComparer.Ordinal).Count() != reviewCategories.Length
                || historyCategories.Distinct(StringComparer.Ordinal).Count() != historyCategories.Length)
                return CaptureDeny(EvaluationReviewerIssue.DuplicateEntry);
            assignments[i] = assignment with
            {
                Scope = assignment.Scope with { },
                ReviewCategoryIds = Array.AsReadOnly(reviewCategories),
                HistoryCategoryIds = Array.AsReadOnly(historyCategories)
            };
        }
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.Scope is null) return CaptureDeny(EvaluationReviewerIssue.InvalidInput);
            if (!Reference(member.Id) || !Reference(member.CategoryId) || !ScopeValid(member.Scope))
                return CaptureDeny(EvaluationReviewerIssue.InvalidReference);
            if (member.Revision <= 0) return CaptureDeny(EvaluationReviewerIssue.InvalidVersion);
            if (!Enum.IsDefined(member.State)) return CaptureDeny(EvaluationReviewerIssue.InvalidRegistry);
            if (!membersById.TryAdd(member.Id, member)) return CaptureDeny(EvaluationReviewerIssue.DuplicateEntry);
            members[i] = member with { Scope = member.Scope with { } };
        }
        for (var i = 0; i < conflicts.Length; i++)
        {
            var conflict = conflicts[i];
            if (!Reference(conflict.AssignmentId) || !Reference(conflict.MemberId)
                || conflict.Clearance is { } clearance && (!Reference(clearance.ReasonId) || !Reference(clearance.AuthorityId)))
                return CaptureDeny(EvaluationReviewerIssue.InvalidReference);
            if (conflict.Revision <= 0) return CaptureDeny(EvaluationReviewerIssue.InvalidVersion);
            if (Answers(conflict).Any(x => !Enum.IsDefined(x))
                || !assignmentsById.TryGetValue(conflict.AssignmentId, out var assignment)
                || !membersById.TryGetValue(conflict.MemberId, out var member) || assignment.Scope != member.Scope)
                return CaptureDeny(EvaluationReviewerIssue.InvalidRegistry);
            if (!conflictIds.Add((conflict.AssignmentId, conflict.MemberId))) return CaptureDeny(EvaluationReviewerIssue.DuplicateEntry);
            conflicts[i] = conflict with { Clearance = conflict.Clearance is null ? null : conflict.Clearance with { } };
        }
        return new(null, new EvaluationReviewerRegistry(input.VersionId, input.PolicyVersionId, input.InstructionVersionId,
            identities, assignments, members, conflicts));
    }

    public static EvaluationReviewerDecision Decide(EvaluationReviewerRegistry? registry, EvaluationReviewerRequest? request)
    {
        if (registry is null || request?.Scope is null || !Enum.IsDefined(request.Action)) return Deny(EvaluationReviewerIssue.InvalidInput);
        if (!Reference(request.IdentityId) || !Reference(request.AssignmentId) || !Reference(request.MemberId)
            || !Reference(request.ExpectedRegistryVersionId) || !ScopeValid(request.Scope)) return Deny(EvaluationReviewerIssue.InvalidReference);
        if (request.AtUtc.Offset != TimeSpan.Zero || request.ExpectedIdentityRevision <= 0 || request.ExpectedAssignmentRevision <= 0
            || request.ExpectedMemberRevision <= 0 || request.ExpectedConflictRevision is <= 0) return Deny(EvaluationReviewerIssue.InvalidVersion);
        if (request.ExpectedRegistryVersionId != registry.VersionId) return Deny(EvaluationReviewerIssue.StaleVersion);
        if (!registry.TryGetIdentity(request.IdentityId, out var identity)) return Deny(EvaluationReviewerIssue.UnknownIdentity);
        if (!registry.TryGetAssignment(request.AssignmentId, out var assignment)) return Deny(EvaluationReviewerIssue.UnknownAssignment);
        if (!registry.TryGetMember(request.MemberId, out var member)) return Deny(EvaluationReviewerIssue.UnknownMember);
        registry.TryGetConflict(assignment.Id, member.Id, out var conflict);
        if (request.ExpectedIdentityRevision != identity.Revision || request.ExpectedAssignmentRevision != assignment.Revision
            || request.ExpectedMemberRevision != member.Revision
            || request.ExpectedConflictRevision is { } revision && conflict is not null && revision != conflict.Revision)
            return Deny(EvaluationReviewerIssue.StaleVersion);
        if (request.ExpectedConflictRevision is not null && conflict is null && request.Action != EvaluationReviewerAction.ScoredReview)
            return Deny(EvaluationReviewerIssue.StaleVersion);
        if (!identity.Authenticated || identity.State != EvaluationFixtureState.Active) return Deny(EvaluationReviewerIssue.IdentityDenied);
        if (assignment.IdentityId != identity.Id || assignment.State != EvaluationFixtureState.Active
            || request.AtUtc < assignment.ValidFromUtc || request.AtUtc >= assignment.ValidUntilUtc) return Deny(EvaluationReviewerIssue.AssignmentDenied);
        if (assignment.Scope != request.Scope || member.Scope != request.Scope) return Deny(EvaluationReviewerIssue.ScopeDenied);
        if (member.State != EvaluationFixtureState.Active || !member.ResourcePermits) return Deny(EvaluationReviewerIssue.ResourceDenied);
        if (!assignment.CustomerPolicyPermits || !member.CustomerPolicyPermits) return Deny(EvaluationReviewerIssue.CustomerPolicyDenied);
        if (!assignment.LifecyclePermits || !member.LifecyclePermits) return Deny(EvaluationReviewerIssue.LifecycleDenied);

        var reviewerRole = assignment.Role is EvaluationReviewerRole.Consultant or EvaluationReviewerRole.QualifiedCustomerReviewer;
        if (request.Action == EvaluationReviewerAction.RelatedHistory)
        {
            if (!reviewerRole && assignment.Role != EvaluationReviewerRole.Auditor) return Deny(EvaluationReviewerIssue.RoleActionDenied);
            if (!registry.HasHistoryCategory(assignment.Id, member.CategoryId)) return Deny(EvaluationReviewerIssue.CategoryDenied);
            if (!assignment.RelatedHistoryGranted) return Deny(EvaluationReviewerIssue.HistoryDenied);
            return new(null, null);
        }
        if (!reviewerRole || (request.Action == EvaluationReviewerAction.ScoredReview
            ? !assignment.ScoredReviewGranted : !assignment.PresentationCorrectionGranted)) return Deny(EvaluationReviewerIssue.RoleActionDenied);
        if (!registry.HasReviewCategory(assignment.Id, member.CategoryId)) return Deny(EvaluationReviewerIssue.CategoryDenied);
        if (request.Action == EvaluationReviewerAction.PresentationCorrection) return new(null, null);
        if (!assignment.Qualified) return Deny(EvaluationReviewerIssue.QualificationDenied);
        if (conflict is null || request.ExpectedConflictRevision is null || Answers(conflict).Any(x => x == EvaluationConflictAnswer.Unknown)
            || MaterialAnswers(conflict).Any(x => x == EvaluationConflictAnswer.Yes)) return Deny(EvaluationReviewerIssue.ConflictDenied);
        if (conflict.GeneralRelationship == EvaluationConflictAnswer.Yes
            && conflict.Clearance is not { OwnerApproved: true, SmeReviewed: true, SecurityReviewed: true }) return Deny(EvaluationReviewerIssue.ConflictDenied);
        return new(null, member.AuthorizedContextSufficient);
    }

    private static EvaluationReviewerCaptureResult CaptureDeny(EvaluationReviewerIssue issue) => new(issue, null);
    private static EvaluationReviewerDecision Deny(EvaluationReviewerIssue issue) => new(issue, null);
    private static bool WithinBound<T>(IReadOnlyCollection<T>? entries) => entries is not null && entries.Count <= MaximumEntries;
    private static bool ScopeValid(EvaluationReviewerScope scope) => Reference(scope.CustomerId) && Reference(scope.ProjectId)
        && Reference(scope.EnvironmentId) && Reference(scope.AssessmentId) && Reference(scope.EvaluationId);
    private static bool Reference(string? value)
    {
        const string prefix = "synthetic-";
        return value is not null && value.Length > prefix.Length && value.Length <= 128 && value.StartsWith(prefix, StringComparison.Ordinal)
            && value.AsSpan(prefix.Length).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789._-") < 0;
    }
    private static EvaluationConflictAnswer[] MaterialAnswers(EvaluationFixtureConflict conflict) =>
    [
        conflict.FindingOrAnswerOrCorrectionAuthor, conflict.RuleOrCatalogContributor, conflict.AssessmentOrSourceContributor,
        conflict.ModelPromptOrProviderContributor, conflict.CustomerConclusionOrRemediationResponsible, conflict.OtherMaterialContributor
    ];
    private static EvaluationConflictAnswer[] Answers(EvaluationFixtureConflict conflict) => [.. MaterialAnswers(conflict), conflict.GeneralRelationship];
}
