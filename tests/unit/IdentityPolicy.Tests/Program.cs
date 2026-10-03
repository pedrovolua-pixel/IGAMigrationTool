using System.Collections.Immutable;
using IdentityPolicy;

var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
var subject = new HumanSubjectKey(Guid.NewGuid(), Guid.NewGuid(), ActorKind.Human);
var scope = new HumanScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
var resource = Guid.NewGuid();
var session = Guid.NewGuid();
var count = 0;
var allConditions = Enum.GetValues<GrantCondition>().ToImmutableHashSet();
var categories = ImmutableHashSet.Create(StringComparer.Ordinal, "protected-code");
var policy = new HumanCustomerPolicy(true, categories, true, true, true, true, true);

// Independent approved-matrix rows: role membership does not derive from production evaluator.
var allowed = new Dictionary<HumanAction, HumanRole[]>
{
    [HumanAction.ViewDashboard] = Enum.GetValues<HumanRole>(),
    [HumanAction.ViewNormalizedExcerpt] = [HumanRole.Consultant, HumanRole.CustomerReviewer, HumanRole.EvidenceAuthorizer, HumanRole.CustomerRiskOwner, HumanRole.Auditor],
    [HumanAction.ViewProtectedRawEvidence] = [HumanRole.Consultant, HumanRole.CustomerReviewer, HumanRole.EvidenceAuthorizer, HumanRole.CustomerRiskOwner],
    [HumanAction.AuthorizeProtectedEvidence] = [HumanRole.EvidenceAuthorizer],
    [HumanAction.ConfigureStartAssessment] = [HumanRole.Consultant],
    [HumanAction.ConfigureProfileRulesOutcomesAiBudget] = [HumanRole.Consultant],
    [HumanAction.OverrideAiBudget] = [HumanRole.Consultant],
    [HumanAction.ReviewFinding] = [HumanRole.Consultant, HumanRole.CustomerReviewer],
    [HumanAction.EditPresentationBusinessContext] = [HumanRole.Consultant, HumanRole.CustomerReviewer],
    [HumanAction.AcceptResidualRisk] = [HumanRole.CustomerRiskOwner],
    [HumanAction.CreateRecommendationPackage] = [HumanRole.Consultant],
    [HumanAction.CommentReviewRecommendation] = [HumanRole.Consultant, HumanRole.CustomerReviewer],
    [HumanAction.ExportConsultantTasksCsv] = [HumanRole.Consultant, HumanRole.Auditor],
    [HumanAction.PublishReport] = [HumanRole.Consultant],
    [HumanAction.AcknowledgeReport] = [HumanRole.CustomerRiskOwner, HumanRole.Executive],
    [HumanAction.CreateReportLink] = [HumanRole.Consultant],
    [HumanAction.RevokeReportLink] = [HumanRole.Consultant],
    [HumanAction.DeleteWithinPolicy] = [HumanRole.Consultant, HumanRole.CustomerRiskOwner],
    [HumanAction.ViewAuditHistory] = [HumanRole.Consultant, HumanRole.CustomerReviewer, HumanRole.EvidenceAuthorizer, HumanRole.CustomerRiskOwner, HumanRole.Auditor, HumanRole.PlatformSupport],
    [HumanAction.RunRemediationOrMigration] = [],
    [HumanAction.SecurityRoleAdministration] = []
};
foreach (var action in Enum.GetValues<HumanAction>())
    foreach (var role in Enum.GetValues<HumanRole>())
    {
        var request = Request(action, role);
        var snapshot = Snapshot(request, role);
        var expected = allowed[action].Contains(role);
        await Check($"matrix {action}/{role}", subject, request, snapshot, expected);
        if (!expected) continue;
        foreach (var (name, mutate) in Mutations())
            await Check($"deny {action}/{role}/{name}", subject, request, mutate(snapshot), false);
        await Check($"stale revision {action}/{role}", subject, request with { ExpectedRevision = 2 }, snapshot, false);
        await Check($"ID substitution {action}/{role}", subject, request with { ResourceId = Guid.NewGuid() }, snapshot, false);
        await Check($"category substitution {action}/{role}", subject, request with { EvidenceCategory = "other" }, snapshot, false);
        await Check($"subject substitution {action}/{role}", subject with { ObjectId = Guid.NewGuid() }, request, snapshot, false);
    }

var conditional = new (HumanAction, HumanRole, GrantCondition)[]
{
    (HumanAction.ViewDashboard, HumanRole.EvidenceAuthorizer, GrantCondition.SeparateViewer),
    (HumanAction.ViewDashboard, HumanRole.PlatformSupport, GrantCondition.SupportMetadataPolicy),
    (HumanAction.ViewNormalizedExcerpt, HumanRole.EvidenceAuthorizer, GrantCondition.SeparateViewer),
    (HumanAction.ViewNormalizedExcerpt, HumanRole.CustomerRiskOwner, GrantCondition.SeparateViewer),
    (HumanAction.ViewNormalizedExcerpt, HumanRole.Auditor, GrantCondition.ApprovedReportProvenance),
    (HumanAction.ViewProtectedRawEvidence, HumanRole.EvidenceAuthorizer, GrantCondition.SeparateViewer),
    (HumanAction.ViewProtectedRawEvidence, HumanRole.Consultant, GrantCondition.CustomerProtectedAuthorization),
    (HumanAction.ViewProtectedRawEvidence, HumanRole.Consultant, GrantCondition.DedicatedProtectedPermission),
    (HumanAction.OverrideAiBudget, HumanRole.Consultant, GrantCondition.AuditedOverride),
    (HumanAction.EditPresentationBusinessContext, HumanRole.CustomerReviewer, GrantCondition.ReviewerEditPermission),
    (HumanAction.AcceptResidualRisk, HumanRole.CustomerRiskOwner, GrantCondition.RiskRequiredFields),
    (HumanAction.ExportConsultantTasksCsv, HumanRole.Auditor, GrantCondition.ExplicitExportGrant),
    (HumanAction.PublishReport, HumanRole.Consultant, GrantCondition.PublishWarningAcknowledged),
    (HumanAction.AcknowledgeReport, HumanRole.Executive, GrantCondition.ExecutiveAcknowledgmentAuthority),
    (HumanAction.DeleteWithinPolicy, HumanRole.Consultant, GrantCondition.ExplicitDeletionPermission),
    (HumanAction.DeleteWithinPolicy, HumanRole.CustomerRiskOwner, GrantCondition.SeparateCustomerAdministrator),
    (HumanAction.DeleteWithinPolicy, HumanRole.CustomerRiskOwner, GrantCondition.ExplicitDeletionPermission),
    (HumanAction.ViewAuditHistory, HumanRole.PlatformSupport, GrantCondition.SupportMetadataPolicy)
};
foreach (var (action, role, condition) in conditional)
{
    var request = Request(action, role);
    var snapshot = Snapshot(request, role);
    var deficient = snapshot.Assignments[0] with { Conditions = allConditions.Remove(condition) };
    await Check($"missing condition {action}/{role}/{condition}", subject, request, snapshot with { Assignments = [deficient] }, false);
    var unrelated = Assignment(HumanRole.Executive) with { Conditions = allConditions };
    if (role == HumanRole.Executive) unrelated = Assignment(HumanRole.EvidenceAuthorizer);
    await Check($"no condition borrowing {action}/{role}/{condition}", subject, request,
        snapshot with { Assignments = [deficient, unrelated] }, false);
}

foreach (var (action, roles) in allowed)
    foreach (var role in roles)
    {
        var request = Request(action, role);
        var snapshot = Snapshot(request, role);
        var publishedAllowed = action is HumanAction.ViewDashboard or HumanAction.ViewNormalizedExcerpt
            or HumanAction.ViewProtectedRawEvidence or HumanAction.ViewAuditHistory or HumanAction.AcknowledgeReport
            or HumanAction.CreateReportLink or HumanAction.RevokeReportLink or HumanAction.DeleteWithinPolicy;
        await Check($"published immutable {action}/{role}", subject, request, snapshot with { State = ResourceState.Published }, publishedAllowed);
        await Check($"published unsupported lifecycle {action}/{role}", subject, request,
            snapshot with { State = ResourceState.Published, ResourceAllowedActions = [] }, false);
    }

var privilegedActions = new[] { HumanAction.AuthorizeProtectedEvidence, HumanAction.AcceptResidualRisk,
    HumanAction.PublishReport, HumanAction.CreateReportLink, HumanAction.RevokeReportLink, HumanAction.DeleteWithinPolicy };
foreach (var action in privilegedActions)
{
    var role = allowed[action][0]; var request = Request(action, role); var snapshot = Snapshot(request, role);
    var authentication = Authentication(snapshot, action);
    await Check($"15-minute boundary {action}", subject, request, snapshot, true, authentication with { AuthenticatedAt = now.AddMinutes(-15) });
    foreach (var bad in new[]
    {
        authentication with { AuthenticatedAt = now.AddMinutes(-15).AddTicks(-1) },
        authentication with { AuthenticatedAt = now.AddTicks(1) },
        authentication with { Subject = subject with { ObjectId = Guid.NewGuid() } },
        authentication with { SessionId = Guid.NewGuid() }, authentication with { SecurityVersion = 2 },
        authentication with { Action = HumanAction.RunRemediationOrMigration },
        authentication with { RequiredMfaVerified = false }, authentication with { RequiredConditionalAccessVerified = false }
    }) await Check($"privileged denial {action}", subject, request, snapshot, false, bad);
    await Check($"missing MFA adapter {action}", subject, request, snapshot, false, missingVerifier: true);
}

foreach (var (action, mutatePolicy) in new (HumanAction, Func<HumanCustomerPolicy, HumanCustomerPolicy>)[]
{
    (HumanAction.ViewProtectedRawEvidence, p => p with { ProtectedEvidenceAllowed = false }),
    (HumanAction.AuthorizeProtectedEvidence, p => p with { ProtectedEvidenceAllowed = false }),
    (HumanAction.ConfigureProfileRulesOutcomesAiBudget, p => p with { AiAllowed = false }),
    (HumanAction.OverrideAiBudget, p => p with { AiAllowed = false }),
    (HumanAction.ExportConsultantTasksCsv, p => p with { ExportAllowed = false }),
    (HumanAction.CreateReportLink, p => p with { ExportAllowed = false }),
    (HumanAction.CreateReportLink, p => p with { SharingAllowed = false }),
    (HumanAction.RevokeReportLink, p => p with { ExportAllowed = false }),
    (HumanAction.RevokeReportLink, p => p with { SharingAllowed = false }),
    (HumanAction.DeleteWithinPolicy, p => p with { DeletionAllowed = false })
})
{
    var request = Request(action, allowed[action][0]); var snapshot = Snapshot(request, allowed[action][0]);
    await Check($"customer policy gate {action}", subject, request,
        snapshot with { CustomerPolicy = mutatePolicy(policy) }, false);
}

var simpleRequest = Request(HumanAction.ViewDashboard, HumanRole.Consultant);
var simple = Snapshot(simpleRequest, HumanRole.Consultant);
var guest = new GuestLifecycle(Guid.NewGuid(), true, now.AddDays(-30), now.AddDays(60), now.AddDays(-30), false);
await Check("guest90/review30 exact boundaries", subject, simpleRequest, simple with { IsGuest = true, Guest = guest }, true);
foreach (var badGuest in new[]
{
    guest with { SponsorId = Guid.Empty }, guest with { SponsorActive = false },
    guest with { SponsorOrEngagementChanged = true }, guest with { ExpiresAt = now },
    guest with { ExpiresAt = guest.AssignedAt.AddDays(90).AddTicks(1) },
    guest with { LastReviewedAt = now.AddDays(-30).AddTicks(-1) }, guest with { LastReviewedAt = now.AddTicks(1) },
    guest with { AssignedAt = now.AddTicks(1) }, guest with { LastReviewedAt = guest.AssignedAt.AddTicks(-1) }
}) await Check("guest invalid lifecycle", subject, simpleRequest, simple with { IsGuest = true, Guest = badGuest }, false);
await Check("guest without record", subject, simpleRequest, simple with { IsGuest = true }, false);
await Check("nonguest with guest record", subject, simpleRequest, simple with { Guest = guest }, false);
foreach (var kind in Enum.GetValues<ActorKind>().Where(k => k != ActorKind.Human).Append((ActorKind)999))
    await Check($"nonhuman {kind}", subject with { Kind = kind }, simpleRequest, simple, false);
await Check("unknown action", subject, simpleRequest with { Action = (HumanAction)999 }, simple, false);
await Check("unknown projection", subject, simpleRequest with { Projection = (ReadProjection)999 }, simple, false);
await Check("empty resource", subject, simpleRequest with { ResourceId = Guid.Empty }, simple, false);
await Check("null request", subject, null, simple, false);
await Check("null subject", null, simpleRequest, simple, false);
await Check("no authority adapter", subject, simpleRequest, simple, false, missingSource: true);
await Check("authority exception", subject, simpleRequest, simple, false, sourceThrows: true);
await Check("authority unavailable", subject, simpleRequest, null, false);
await Check("one-minute authority bound", subject, simpleRequest, simple with { ResolvedAt = now.AddMinutes(-1) }, true);
await Check("15-minute provider bound", subject, simpleRequest, simple with { ProviderStatusCheckedAt = now.AddMinutes(-15) }, true);

// Explicit overlap does not allow consultant authority to accept risk or authorize raw evidence.
var riskRequest = Request(HumanAction.AcceptResidualRisk, HumanRole.CustomerRiskOwner);
await Check("consultant cannot accept risk", subject, riskRequest, Snapshot(riskRequest, HumanRole.Consultant), false);
var authRequest = Request(HumanAction.AuthorizeProtectedEvidence, HumanRole.EvidenceAuthorizer);
await Check("consultant cannot authorize protected evidence", subject, authRequest, Snapshot(authRequest, HumanRole.Consultant), false);
await Check("explicit separate risk role", subject, riskRequest,
    Snapshot(riskRequest, HumanRole.Consultant) with { Assignments = [Assignment(HumanRole.Consultant), Assignment(HumanRole.CustomerRiskOwner)] }, true);
foreach (var projection in Enum.GetValues<ReadProjection>().Where(p => p != ReadProjection.SummaryExcerpts))
{
    var request = Request(HumanAction.ViewDashboard, HumanRole.Executive) with { Projection = projection };
    await Check("executive projection cannot broaden", subject, request, Snapshot(request, HumanRole.Executive), false);
}
foreach (var role in Enum.GetValues<HumanRole>())
{
    var request = Request(HumanAction.ViewDashboard, role); var snapshot = Snapshot(request, role);
    var coarse = role switch
    {
        HumanRole.Consultant => CoarseAppRole.PilotConsultant,
        HumanRole.Auditor => CoarseAppRole.PilotAuditor,
        HumanRole.PlatformSupport => CoarseAppRole.PilotPlatformOperator,
        _ => CoarseAppRole.PilotCustomerUser
    };
    await Check($"coarse mapping {role}", subject, request, snapshot with { CoarseAppRoles = [coarse] }, true);
    await Check($"wrong coarse role {role}", subject, request,
        snapshot with { CoarseAppRoles = [coarse == CoarseAppRole.PilotConsultant ? CoarseAppRole.PilotAuditor : CoarseAppRole.PilotConsultant] }, false);
}
await Check("same role separate scope", subject, simpleRequest,
    simple with { Assignments = [Assignment(HumanRole.Consultant), Assignment(HumanRole.Consultant) with { Scope = scope with { CustomerId = Guid.NewGuid() } }] }, true);
var wrongCase = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "PROTECTED-CODE");
await Check("category grant comparison is exact", subject, simpleRequest,
    simple with { Assignments = [Assignment(HumanRole.Consultant) with { EvidenceCategories = wrongCase }] }, false);
await Check("policy category comparison is exact", subject, simpleRequest,
    simple with { CustomerPolicy = policy with { AllowedEvidenceCategories = wrongCase } }, false);
Console.WriteLine($"PASS: {count} independent human policy matrix, refusal, boundary and overlap checks.");

HumanAssignment Assignment(HumanRole role) => new(role, scope, true, now.AddHours(1), categories, allConditions);
HumanAuthorizationRequest Request(HumanAction action, HumanRole role) => new(scope, resource, 1, action, "protected-code", action switch
{
    HumanAction.ViewDashboard => role switch
    {
        HumanRole.Executive => ReadProjection.SummaryExcerpts,
        HumanRole.Auditor => ReadProjection.ApprovedRecords,
        HumanRole.PlatformSupport => ReadProjection.OperationalMetadata,
        _ => ReadProjection.Dashboard
    },
    HumanAction.ViewNormalizedExcerpt => ReadProjection.NormalizedExcerpt,
    HumanAction.ViewProtectedRawEvidence => ReadProjection.ProtectedRaw,
    HumanAction.ViewAuditHistory => role switch
    {
        HumanRole.Consultant => ReadProjection.ProjectBusinessAudit,
        HumanRole.CustomerReviewer => ReadProjection.RelatedReviewHistory,
        HumanRole.EvidenceAuthorizer => ReadProjection.AuthorizationEvents,
        HumanRole.CustomerRiskOwner => ReadProjection.RiskAcknowledgmentEvents,
        HumanRole.Auditor => ReadProjection.ApprovedAuditScope,
        HumanRole.PlatformSupport => ReadProjection.OperationalMetadata,
        _ => ReadProjection.None
    },
    _ => ReadProjection.None
});
AuthoritativeHumanSnapshot Snapshot(HumanAuthorizationRequest request, HumanRole role) => new(subject, true, true, true,
    1, 1, session, now, now, Enum.GetValues<CoarseAppRole>().ToImmutableHashSet(), false, null, [Assignment(role)],
    request.Scope, resource, 1, ResourceState.Active, null, request.Projection, request.EvidenceCategory,
    Enum.GetValues<HumanAction>().ToImmutableHashSet(), false, false, false, false, policy);
VerifiedPrivilegedAuthentication Authentication(AuthoritativeHumanSnapshot snapshot, HumanAction action)
    => new(subject, snapshot.SessionId, snapshot.SubjectSecurityVersion, action, now, true, true);
async Task Check(string name, HumanSubjectKey? actor, HumanAuthorizationRequest? request, AuthoritativeHumanSnapshot? snapshot,
    bool expected, VerifiedPrivilegedAuthentication? authentication = null, bool missingVerifier = false,
    bool missingSource = false, bool sourceThrows = false)
{
    var authorizer = new HumanAuthorizer(missingSource ? null : new Source(snapshot, sourceThrows),
        missingVerifier ? null : new Verifier(authentication ?? (snapshot is null ? null : Authentication(snapshot, request?.Action ?? HumanAction.ViewDashboard))),
        new Clock(now));
    var result = await authorizer.AuthorizeAsync(actor, request);
    if (result.Allowed != expected || (!expected && (result.Role is not null || result.Projection is not null))
        || (expected && result.Projection != request!.Projection)) throw new Exception($"FAILED: {name}");
    count++;
}
IEnumerable<(string, Func<AuthoritativeHumanSnapshot, AuthoritativeHumanSnapshot>)> Mutations()
{
    yield return ("null customer policy", s => s with { CustomerPolicy = null! });
    yield return ("null policy categories", s => s with { CustomerPolicy = policy with { AllowedEvidenceCategories = null! } });
    yield return ("null coarse roles", s => s with { CoarseAppRoles = null! });
    yield return ("default assignments", s => s with { Assignments = default });
    yield return ("null assignment", s => s with { Assignments = [null!] });
    yield return ("null conditions", s => s with { Assignments = [s.Assignments[0] with { Conditions = null! }] });
    yield return ("null categories", s => s with { Assignments = [s.Assignments[0] with { EvidenceCategories = null! }] });
    yield return ("unknown state", s => s with { State = (ResourceState)999 });
    yield return ("unknown resource action", s => s with { ResourceAllowedActions = [(HumanAction)999] });
    yield return ("null resource actions", s => s with { ResourceAllowedActions = null! });
    yield return ("unauthenticated", s => s with { Authenticated = false });
    yield return ("inactive", s => s with { Active = false });
    yield return ("untrusted tenant", s => s with { TrustedTenant = false });
    yield return ("securityversion", s => s with { SessionSecurityVersion = 2 });
    yield return ("empty session", s => s with { SessionId = Guid.Empty });
    yield return ("stale provider", s => s with { ProviderStatusCheckedAt = now.AddMinutes(-15).AddTicks(-1) });
    yield return ("future provider", s => s with { ProviderStatusCheckedAt = now.AddTicks(1) });
    yield return ("stale authority", s => s with { ResolvedAt = now.AddMinutes(-1).AddTicks(-1) });
    yield return ("wrong customer", s => s with { ResourceScope = scope with { CustomerId = Guid.NewGuid() } });
    yield return ("wrong project", s => s with { ResourceScope = scope with { ProjectId = Guid.NewGuid() } });
    yield return ("wrong environment", s => s with { ResourceScope = scope with { EnvironmentId = Guid.NewGuid() } });
    yield return ("wrong assessment", s => s with { ResourceScope = scope with { AssessmentId = Guid.NewGuid() } });
    yield return ("wrong resource", s => s with { ResourceId = Guid.NewGuid() });
    yield return ("wrong revision", s => s with { ResourceRevision = 2 });
    yield return ("wrong category", s => s with { ResourceEvidenceCategory = "other" });
    yield return ("wrong projection", s => s with { ResourceProjection = (ReadProjection)999 });
    yield return ("deleted", s => s with { State = ResourceState.SoftDeleted });
    yield return ("expired", s => s with { State = ResourceState.Expired });
    yield return ("expiry equality", s => s with { ResourceExpiresAt = now });
    yield return ("revoked", s => s with { Revoked = true });
    yield return ("hold", s => s with { HoldBlocksAction = true });
    yield return ("retention", s => s with { RetentionBlocksAction = true });
    yield return ("capability", s => s with { CapabilityBlocksAction = true });
    yield return ("policy inactive", s => s with { CustomerPolicy = policy with { Active = false } });
    yield return ("policy category", s => s with { CustomerPolicy = policy with { AllowedEvidenceCategories = [] } });
    yield return ("missing app role", s => s with { CoarseAppRoles = [] });
    yield return ("unknown app role", s => s with { CoarseAppRoles = [(CoarseAppRole)999] });
    yield return ("missing product role", s => s with { Assignments = [] });
    yield return ("revoked assignment", s => s with { Assignments = [s.Assignments[0] with { Active = false }] });
    yield return ("expired assignment", s => s with { Assignments = [s.Assignments[0] with { ExpiresAt = now }] });
    yield return ("assignment wrong scope", s => s with { Assignments = [s.Assignments[0] with { Scope = scope with { CustomerId = Guid.NewGuid() } }] });
    yield return ("assignment category", s => s with { Assignments = [s.Assignments[0] with { EvidenceCategories = [] }] });
    yield return ("unknown role", s => s with { Assignments = [s.Assignments[0] with { Role = (HumanRole)999 }] });
    yield return ("unknown condition", s => s with { Assignments = [s.Assignments[0] with { Conditions = [(GrantCondition)999] }] });
    yield return ("duplicate role", s => s with { Assignments = [s.Assignments[0], s.Assignments[0]] });
    yield return ("resource action", s => s with { ResourceAllowedActions = [] });
}
sealed class Source(AuthoritativeHumanSnapshot? snapshot, bool throws) : IHumanAuthoritySnapshotSource
{
    public ValueTask<AuthoritativeHumanSnapshot?> ResolveAsync(HumanSubjectKey subject, HumanAuthorizationRequest request, CancellationToken cancellationToken)
        => throws ? throw new InvalidOperationException("Synthetic authority outage") : ValueTask.FromResult(snapshot);
}
sealed class Verifier(VerifiedPrivilegedAuthentication? authentication) : IPrivilegedAuthenticationVerifier
{
    public ValueTask<VerifiedPrivilegedAuthentication?> VerifyAsync(HumanSubjectKey subject, Guid sessionId, long securityVersion, HumanAction action, CancellationToken cancellationToken)
        => ValueTask.FromResult(authentication);
}
sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
