using System.Collections;
using SyntheticEvaluation;

internal static class Program
{
    private static int checks;
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly EvaluationReviewerScope Scope = new("synthetic-customer", "synthetic-project", "synthetic-environment", "synthetic-assessment", "synthetic-evaluation");
    private static readonly EvaluationConflictClearance Clearance = new("synthetic-reason", "synthetic-authority", true, true, true);

    private static int Main()
    {
        try
        {
            RolesAndActions();
            SixConditionsAndVersions();
            ConflictsAndContext();
            CaptureAdmissionAndDetachment();
            ReassignmentAndCurrentHistory();
            HighVolumeCurrentDecisions();
            Console.WriteLine($"PASS: {checks} independent synthetic reviewer assertions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL after {checks} assertions: {ex}");
            return 1;
        }
    }

    private static EvaluationFixtureIdentity Identity() => new("synthetic-person", 1, true, EvaluationFixtureState.Active);
    private static EvaluationFixtureAssignment Assignment() => new("synthetic-assignment", 1, "synthetic-person", EvaluationReviewerRole.Consultant,
        Scope, Now.AddDays(-2), Now.AddDays(20), EvaluationFixtureState.Active, true, "synthetic-basis", "synthetic-authority", true, true, true,
        new List<string> { "synthetic-category" }, new List<string> { "synthetic-category" }, true, true);
    private static EvaluationFixtureMember Member() => new("synthetic-member", 1, Scope, "synthetic-category", EvaluationFixtureState.Active, true, true, true, true);
    private static EvaluationFixtureConflict Conflict() => new("synthetic-assignment", "synthetic-member", 1,
        EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No,
        EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, EvaluationConflictAnswer.No, null);
    private static EvaluationReviewerRegistryInput Fixture() => new("synthetic-registry-v1", "synthetic-policy-v1", "synthetic-instructions-v1",
        new List<EvaluationFixtureIdentity> { Identity() }, new List<EvaluationFixtureAssignment> { Assignment() },
        new List<EvaluationFixtureMember> { Member() }, new List<EvaluationFixtureConflict> { Conflict() });
    private static EvaluationReviewerRequest Request(EvaluationReviewerAction action = EvaluationReviewerAction.ScoredReview) =>
        new("synthetic-person", "synthetic-assignment", "synthetic-member", Scope, action, Now, "synthetic-registry-v1", 1, 1, 1,
            action == EvaluationReviewerAction.ScoredReview ? 1 : null);

    private static EvaluationReviewerRegistry Capture(EvaluationReviewerRegistryInput input)
    {
        var result = EvaluationReviewerPolicy.Capture(input);
        Check(result.Issue is null && result.HasRegistry && result.Registry is not null, "valid fixture capture");
        return result.Registry!;
    }

    private static void Decision(EvaluationReviewerRegistryInput input, EvaluationReviewerRequest request, EvaluationReviewerIssue? expected, bool? context = null)
        => Decision(Capture(input), request, expected, context);
    private static void Decision(EvaluationReviewerRegistry registry, EvaluationReviewerRequest request, EvaluationReviewerIssue? expected, bool? context = null)
    {
        var actual = EvaluationReviewerPolicy.Decide(registry, request);
        Check(actual.Issue == expected, $"decision expected {expected}, actual {actual.Issue}");
        Check(actual.IsAuthorized == (expected is null), "authorization flag");
        Check(actual.AuthorizedContextSufficient == context, "context projected only for authorized scored review");
    }

    private static EvaluationReviewerRegistryInput WithIdentity(EvaluationFixtureIdentity identity) => Fixture() with { Identities = [identity] };
    private static EvaluationReviewerRegistryInput WithAssignment(EvaluationFixtureAssignment assignment) => Fixture() with { Assignments = [assignment] };
    private static EvaluationReviewerRegistryInput WithMember(EvaluationFixtureMember member) => Fixture() with { Members = [member] };
    private static EvaluationReviewerRegistryInput WithConflict(EvaluationFixtureConflict conflict) => Fixture() with { Conflicts = [conflict] };

    private static void RolesAndActions()
    {
        // Explicit expected role table; no implementation result is used as oracle.
        var expectations = new (EvaluationReviewerRole Role, bool Score, bool Presentation, bool History)[]
        {
            (EvaluationReviewerRole.Consultant, true, true, true),
            (EvaluationReviewerRole.QualifiedCustomerReviewer, true, true, true),
            (EvaluationReviewerRole.CustomerEvidenceAuthorizer, false, false, false),
            (EvaluationReviewerRole.CustomerRiskOwner, false, false, false),
            (EvaluationReviewerRole.Executive, false, false, false),
            (EvaluationReviewerRole.Auditor, false, false, true),
            (EvaluationReviewerRole.PlatformSupport, false, false, false),
            (EvaluationReviewerRole.Mcp, false, false, false),
            (EvaluationReviewerRole.ShareViewer, false, false, false)
        };
        foreach (var (role, score, presentation, history) in expectations)
        {
            var input = WithAssignment(Assignment() with { Role = role });
            Decision(input, Request(), score ? null : EvaluationReviewerIssue.RoleActionDenied, score ? true : null);
            Decision(input, Request(EvaluationReviewerAction.PresentationCorrection), presentation ? null : EvaluationReviewerIssue.RoleActionDenied);
            Decision(input, Request(EvaluationReviewerAction.RelatedHistory), history ? null : EvaluationReviewerIssue.RoleActionDenied);
        }
        Decision(WithAssignment(Assignment() with { ScoredReviewGranted = false }), Request(), EvaluationReviewerIssue.RoleActionDenied);
        Decision(WithAssignment(Assignment() with { PresentationCorrectionGranted = false }), Request(EvaluationReviewerAction.PresentationCorrection), EvaluationReviewerIssue.RoleActionDenied);
        Decision(WithAssignment(Assignment() with { RelatedHistoryGranted = false }), Request(EvaluationReviewerAction.RelatedHistory), EvaluationReviewerIssue.HistoryDenied);
        Decision(WithAssignment(Assignment() with { Qualified = false, QualificationBasisId = null }), Request(), EvaluationReviewerIssue.QualificationDenied);
        Decision(WithAssignment(Assignment() with { Qualified = false, QualificationBasisId = null }), Request(EvaluationReviewerAction.RelatedHistory), null);
    }

    private static void SixConditionsAndVersions()
    {
        var registry = Capture(Fixture());
        Decision(registry, Request() with { IdentityId = "synthetic-unknown" }, EvaluationReviewerIssue.UnknownIdentity);
        Decision(registry, Request() with { AssignmentId = "synthetic-unknown" }, EvaluationReviewerIssue.UnknownAssignment);
        Decision(registry, Request() with { MemberId = "synthetic-unknown" }, EvaluationReviewerIssue.UnknownMember);
        Decision(registry, Request() with { ExpectedRegistryVersionId = "synthetic-other" }, EvaluationReviewerIssue.StaleVersion);
        Decision(registry, Request() with { ExpectedIdentityRevision = 2 }, EvaluationReviewerIssue.StaleVersion);
        Decision(registry, Request() with { ExpectedAssignmentRevision = 2 }, EvaluationReviewerIssue.StaleVersion);
        Decision(registry, Request() with { ExpectedMemberRevision = 2 }, EvaluationReviewerIssue.StaleVersion);
        Decision(registry, Request() with { ExpectedConflictRevision = 2 }, EvaluationReviewerIssue.StaleVersion);
        Decision(WithIdentity(Identity() with { Authenticated = false }), Request(), EvaluationReviewerIssue.IdentityDenied);
        foreach (var state in new[] { EvaluationFixtureState.Revoked, EvaluationFixtureState.Suspended, EvaluationFixtureState.Deleted, EvaluationFixtureState.Expired })
        {
            Decision(WithIdentity(Identity() with { State = state }), Request(), EvaluationReviewerIssue.IdentityDenied);
            Decision(WithAssignment(Assignment() with { State = state }), Request(), EvaluationReviewerIssue.AssignmentDenied);
            Decision(WithMember(Member() with { State = state }), Request(), EvaluationReviewerIssue.ResourceDenied);
        }
        Decision(Fixture(), Request() with { AtUtc = Assignment().ValidFromUtc }, null, true);
        Decision(Fixture(), Request() with { AtUtc = Assignment().ValidFromUtc.AddTicks(-1) }, EvaluationReviewerIssue.AssignmentDenied);
        Decision(Fixture(), Request() with { AtUtc = Assignment().ValidUntilUtc }, EvaluationReviewerIssue.AssignmentDenied);
        var scopes = new[]
        {
            Scope with { CustomerId = "synthetic-other" }, Scope with { ProjectId = "synthetic-other" },
            Scope with { EnvironmentId = "synthetic-other" }, Scope with { AssessmentId = "synthetic-other" },
            Scope with { EvaluationId = "synthetic-other" }
        };
        foreach (var scope in scopes)
        {
            Decision(registry, Request() with { Scope = scope }, EvaluationReviewerIssue.ScopeDenied);
            // A changed member scope is valid registry state once its declaration is absent.
            Decision(WithMember(Member() with { Scope = scope }) with { Conflicts = [] }, Request(), EvaluationReviewerIssue.ScopeDenied);
        }
        Decision(WithMember(Member() with { ResourcePermits = false }), Request(), EvaluationReviewerIssue.ResourceDenied);
        Decision(WithAssignment(Assignment() with { CustomerPolicyPermits = false }), Request(), EvaluationReviewerIssue.CustomerPolicyDenied);
        Decision(WithMember(Member() with { CustomerPolicyPermits = false }), Request(), EvaluationReviewerIssue.CustomerPolicyDenied);
        Decision(WithAssignment(Assignment() with { LifecyclePermits = false }), Request(), EvaluationReviewerIssue.LifecycleDenied);
        Decision(WithMember(Member() with { LifecyclePermits = false }), Request(), EvaluationReviewerIssue.LifecycleDenied);
        Decision(WithAssignment(Assignment() with { ReviewCategoryIds = [] }), Request(), EvaluationReviewerIssue.CategoryDenied);
        Decision(WithAssignment(Assignment() with { ReviewCategoryIds = [] }), Request(EvaluationReviewerAction.PresentationCorrection), EvaluationReviewerIssue.CategoryDenied);
        Decision(WithAssignment(Assignment() with { HistoryCategoryIds = [] }), Request(EvaluationReviewerAction.RelatedHistory), EvaluationReviewerIssue.CategoryDenied);
        var other = Identity() with { Id = "synthetic-other" };
        Decision(Fixture() with { Identities = [Identity(), other] }, Request() with { IdentityId = other.Id }, EvaluationReviewerIssue.AssignmentDenied);
    }

    private static void ConflictsAndContext()
    {
        Decision(Fixture() with { Conflicts = [] }, Request(), EvaluationReviewerIssue.ConflictDenied);
        Decision(Fixture(), Request() with { ExpectedConflictRevision = null }, EvaluationReviewerIssue.ConflictDenied);
        var original = Conflict();
        Func<EvaluationConflictAnswer, EvaluationFixtureConflict>[] dimensions =
        [
            a => original with { FindingOrAnswerOrCorrectionAuthor = a },
            a => original with { RuleOrCatalogContributor = a },
            a => original with { AssessmentOrSourceContributor = a },
            a => original with { ModelPromptOrProviderContributor = a },
            a => original with { CustomerConclusionOrRemediationResponsible = a },
            a => original with { OtherMaterialContributor = a }
        ];
        foreach (var dimension in dimensions)
        {
            Decision(WithConflict(dimension(EvaluationConflictAnswer.Unknown)), Request(), EvaluationReviewerIssue.ConflictDenied);
            Decision(WithConflict(dimension(EvaluationConflictAnswer.Yes)), Request(), EvaluationReviewerIssue.ConflictDenied);
            Decision(WithConflict(dimension(EvaluationConflictAnswer.Yes) with { Clearance = Clearance }), Request(), EvaluationReviewerIssue.ConflictDenied);
            Decision(WithConflict(dimension(EvaluationConflictAnswer.Yes)), Request(EvaluationReviewerAction.PresentationCorrection), null);
        }
        Decision(WithConflict(original with { GeneralRelationship = EvaluationConflictAnswer.Unknown }), Request(), EvaluationReviewerIssue.ConflictDenied);
        Decision(WithConflict(original with { GeneralRelationship = EvaluationConflictAnswer.Yes }), Request(), EvaluationReviewerIssue.ConflictDenied);
        Decision(WithConflict(original with { GeneralRelationship = EvaluationConflictAnswer.Yes, Clearance = Clearance }), Request(), null, true);
        foreach (var clearance in new[] { Clearance with { OwnerApproved = false }, Clearance with { SmeReviewed = false }, Clearance with { SecurityReviewed = false } })
            Decision(WithConflict(original with { GeneralRelationship = EvaluationConflictAnswer.Yes, Clearance = clearance }), Request(), EvaluationReviewerIssue.ConflictDenied);
        Decision(WithMember(Member() with { AuthorizedContextSufficient = false }), Request(), null, false);
        Decision(WithMember(Member() with { AuthorizedContextSufficient = false }) with { Identities = [Identity() with { Authenticated = false }] }, Request(), EvaluationReviewerIssue.IdentityDenied);
        Decision(Fixture(), Request(EvaluationReviewerAction.RelatedHistory) with { ExpectedConflictRevision = 2 }, EvaluationReviewerIssue.StaleVersion);
        Decision(Fixture() with { Conflicts = [] }, Request(EvaluationReviewerAction.RelatedHistory), null);
    }

    private static void CaptureDeny(EvaluationReviewerRegistryInput? input, EvaluationReviewerIssue issue)
    {
        var result = EvaluationReviewerPolicy.Capture(input);
        Check(result.Issue == issue, $"capture expected {issue}, actual {result.Issue}");
        Check(!result.HasRegistry && result.Registry is null, "capture denial contains no registry");
    }

    private static void CaptureAdmissionAndDetachment()
    {
        CaptureDeny(null, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Identities = null! }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Assignments = null! }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Members = null! }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Conflicts = null! }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Identities = [null!] }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Assignments = [null!] }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Members = [null!] }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Conflicts = [null!] }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(WithAssignment(Assignment() with { Scope = null! }), EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(WithMember(Member() with { Scope = null! }), EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(WithAssignment(Assignment() with { ReviewCategoryIds = null! }), EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(WithAssignment(Assignment() with { HistoryCategoryIds = null! }), EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(Fixture() with { Identities = [Identity(), Identity()] }, EvaluationReviewerIssue.DuplicateEntry);
        CaptureDeny(Fixture() with { Assignments = [Assignment(), Assignment()] }, EvaluationReviewerIssue.DuplicateEntry);
        CaptureDeny(Fixture() with { Members = [Member(), Member()] }, EvaluationReviewerIssue.DuplicateEntry);
        CaptureDeny(Fixture() with { Conflicts = [Conflict(), Conflict()] }, EvaluationReviewerIssue.DuplicateEntry);
        CaptureDeny(WithAssignment(Assignment() with { ReviewCategoryIds = ["synthetic-category", "synthetic-category"] }), EvaluationReviewerIssue.DuplicateEntry);
        CaptureDeny(WithAssignment(Assignment() with { HistoryCategoryIds = ["synthetic-category", "synthetic-category"] }), EvaluationReviewerIssue.DuplicateEntry);
        foreach (var bad in new[] { "", "synthetic-", "Synthetic-x", "synthetic-X", "synthetic- x", "synthetic-x/y", "synthetic-é", "synthetic-" + new string('a', 119) })
        {
            CaptureDeny(Fixture() with { VersionId = bad }, EvaluationReviewerIssue.InvalidReference);
            CaptureDeny(WithMember(Member() with { CategoryId = bad }), EvaluationReviewerIssue.InvalidReference);
            CaptureDeny(WithAssignment(Assignment() with { ReviewCategoryIds = [bad] }), EvaluationReviewerIssue.InvalidReference);
        }
        Capture(Fixture() with { VersionId = "synthetic-" + new string('a', 118) });
        CaptureDeny(WithIdentity(Identity() with { Revision = 0 }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithAssignment(Assignment() with { Revision = -1 }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithMember(Member() with { Revision = 0 }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithConflict(Conflict() with { Revision = 0 }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithAssignment(Assignment() with { ValidFromUtc = Now.ToOffset(TimeSpan.FromHours(1)) }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithAssignment(Assignment() with { ValidUntilUtc = Assignment().ValidFromUtc }), EvaluationReviewerIssue.InvalidVersion);
        CaptureDeny(WithIdentity(Identity() with { State = (EvaluationFixtureState)99 }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithAssignment(Assignment() with { Role = (EvaluationReviewerRole)99 }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithConflict(Conflict() with { GeneralRelationship = (EvaluationConflictAnswer)99 }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithAssignment(Assignment() with { IdentityId = "synthetic-absent" }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithConflict(Conflict() with { MemberId = "synthetic-absent" }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithConflict(Conflict() with { AssignmentId = "synthetic-absent" }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithMember(Member() with { Scope = Scope with { ProjectId = "synthetic-other" } }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithAssignment(Assignment() with { Qualified = true, QualificationBasisId = null }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithAssignment(Assignment() with { Qualified = false }), EvaluationReviewerIssue.InvalidRegistry);
        CaptureDeny(WithConflict(Conflict() with { Clearance = Clearance with { ReasonId = "payload" } }), EvaluationReviewerIssue.InvalidReference);
        CaptureDeny(Fixture() with { Identities = new EvaluationFixtureIdentity[100001] }, EvaluationReviewerIssue.InvalidInput);
        CaptureDeny(WithAssignment(Assignment() with { ReviewCategoryIds = new string[100001] }), EvaluationReviewerIssue.InvalidInput);
        var categories = Enumerable.Range(0, 100000).Select(i => $"synthetic-category-{i}").ToArray();
        Capture(WithAssignment(Assignment() with { ReviewCategoryIds = categories, HistoryCategoryIds = [] }));
        CaptureDeny(WithAssignment(Assignment() with { ReviewCategoryIds = categories }), EvaluationReviewerIssue.InvalidInput);
        Capture(Fixture() with { Identities = [], Assignments = [], Members = [], Conflicts = [] });

        var input = Fixture();
        var captured = Capture(input);
        ((List<EvaluationFixtureIdentity>)input.Identities).Clear();
        ((List<EvaluationFixtureMember>)input.Members).Clear();
        ((List<EvaluationFixtureConflict>)input.Conflicts).Clear();
        ((List<string>)input.Assignments[0].ReviewCategoryIds).Clear();
        ((List<string>)input.Assignments[0].HistoryCategoryIds).Clear();
        ((List<EvaluationFixtureAssignment>)input.Assignments).Clear();
        Decision(captured, Request(), null, true);
        Decision(captured, Request(EvaluationReviewerAction.RelatedHistory), null);
        ThrowsReadOnly((IList)captured.Identities);
        ThrowsReadOnly((IList)captured.Assignments);
        ThrowsReadOnly((IList)captured.Members);
        ThrowsReadOnly((IList)captured.Conflicts);
        ThrowsReadOnly((IList)captured.Assignments[0].ReviewCategoryIds);
        ThrowsReadOnly((IList)captured.Assignments[0].HistoryCategoryIds);
        var nullDecision = EvaluationReviewerPolicy.Decide(null, Request());
        Check(nullDecision.Issue == EvaluationReviewerIssue.InvalidInput && nullDecision.AuthorizedContextSufficient is null, "null registry denial");
        Check(EvaluationReviewerPolicy.Decide(captured, null).Issue == EvaluationReviewerIssue.InvalidInput, "null request denial");
        Decision(captured, Request() with { Scope = null! }, EvaluationReviewerIssue.InvalidInput);
        Decision(captured, Request() with { IdentityId = "payload" }, EvaluationReviewerIssue.InvalidReference);
        Decision(captured, Request() with { Action = (EvaluationReviewerAction)99 }, EvaluationReviewerIssue.InvalidInput);
        Decision(captured, Request() with { AtUtc = Now.ToOffset(TimeSpan.FromHours(-1)) }, EvaluationReviewerIssue.InvalidVersion);
        Decision(captured, Request() with { ExpectedIdentityRevision = 0 }, EvaluationReviewerIssue.InvalidVersion);
        Decision(captured, Request() with { ExpectedConflictRevision = 0 }, EvaluationReviewerIssue.InvalidVersion);
    }

    private static void ReassignmentAndCurrentHistory()
    {
        var independent = Identity() with { Id = "synthetic-independent" };
        var assignment = Assignment() with { Id = "synthetic-independent-assignment", IdentityId = independent.Id };
        var input = Fixture() with
        {
            Identities = [Identity(), independent],
            Assignments = [Assignment(), assignment],
            Conflicts = [Conflict() with { FindingOrAnswerOrCorrectionAuthor = EvaluationConflictAnswer.Yes },
                Conflict() with { AssignmentId = assignment.Id }]
        };
        var old = Capture(input);
        Decision(old, Request(), EvaluationReviewerIssue.ConflictDenied);
        Decision(old, Request() with { IdentityId = independent.Id, AssignmentId = assignment.Id }, null, true);
        var revoked = Capture(input with
        {
            VersionId = "synthetic-registry-v2",
            Identities = [Identity(), independent with { State = EvaluationFixtureState.Revoked, Revision = 2 }]
        });
        var newRequest = Request() with { IdentityId = independent.Id, AssignmentId = assignment.Id };
        Decision(revoked, newRequest, EvaluationReviewerIssue.StaleVersion);
        Decision(revoked, newRequest with { ExpectedRegistryVersionId = "synthetic-registry-v2", ExpectedIdentityRevision = 2 }, EvaluationReviewerIssue.IdentityDenied);
        Decision(old, newRequest, null, true); // Prior value snapshot persists; caller must supply current registry.
        Decision(WithAssignment(Assignment() with { RelatedHistoryGranted = false }), Request(EvaluationReviewerAction.RelatedHistory), EvaluationReviewerIssue.HistoryDenied);
        Decision(WithAssignment(Assignment() with { ReviewCategoryIds = [] }), Request(EvaluationReviewerAction.RelatedHistory), null);
        Decision(WithAssignment(Assignment() with { HistoryCategoryIds = [] }), Request(), null, true);
    }

    private static void HighVolumeCurrentDecisions()
    {
        // Every admitted member has an independently declared No-conflict policy. Reverse
        // traversal exercises repeated current lookups rather than an early linear-scan case.
        var categories = Enumerable.Range(0, 100000).Select(i => $"synthetic-volume-category-{i}").ToArray();
        var members = Enumerable.Range(0, 100000).Select(i => Member() with
        { Id = $"synthetic-volume-{i}", CategoryId = categories[^1] }).ToArray();
        var conflicts = members.Select(m => Conflict() with { MemberId = m.Id }).ToArray();
        var registry = Capture(Fixture() with
        {
            Assignments = [Assignment() with { ReviewCategoryIds = categories, HistoryCategoryIds = [] }],
            Members = members,
            Conflicts = conflicts
        });
        categories[^1] = "synthetic-changed-category";
        members[0] = members[0] with { State = EvaluationFixtureState.Deleted };
        conflicts[0] = conflicts[0] with { FindingOrAnswerOrCorrectionAuthor = EvaluationConflictAnswer.Yes };
        for (var i = 99999; i >= 0; i--)
            Decision(registry, Request() with { MemberId = $"synthetic-volume-{i}" }, null, true);
        Decision(registry, Request() with { MemberId = "synthetic-volume-0" }, null, true);
        Check(registry.Members.Count == 100000 && registry.Conflicts.Count == 100000, "bounded full registry retained");
    }

    private static void ThrowsReadOnly(IList list)
    {
        try { list.Clear(); throw new InvalidOperationException("mutable captured list"); }
        catch (NotSupportedException) { checks++; }
    }
    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(reason);
    }
}
