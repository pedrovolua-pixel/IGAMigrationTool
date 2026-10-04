namespace IdentityPolicy;

public sealed class HumanAuthorizer(IHumanAuthoritySnapshotSource? authoritySource,
    IPrivilegedAuthenticationVerifier? privilegedVerifier, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async ValueTask<HumanAuthorizationDecision> AuthorizeAsync(HumanSubjectKey? subject,
        HumanAuthorizationRequest? request, CancellationToken cancellationToken = default)
    {
        if (authoritySource is null || subject is null || request is null || subject.Kind != ActorKind.Human)
            return HumanAuthorizationDecision.Denied;
        try
        {
            var snapshot = await authoritySource.ResolveAsync(subject, request, cancellationToken);
            if (snapshot is null) return HumanAuthorizationDecision.Denied;
            VerifiedPrivilegedAuthentication? authentication = null;
            if (HumanPolicy.IsPrivileged(request.Action))
            {
                if (privilegedVerifier is null) return HumanAuthorizationDecision.Denied;
                authentication = await privilegedVerifier.VerifyAsync(subject, snapshot.SessionId,
                    snapshot.SubjectSecurityVersion, request.Action, cancellationToken);
            }
            return HumanPolicy.Evaluate(subject, request, snapshot, authentication, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return HumanAuthorizationDecision.Denied; }
    }
}

internal static class HumanPolicy
{
    internal static bool IsPrivileged(HumanAction action) => action is
        HumanAction.AuthorizeProtectedEvidence or HumanAction.AcceptResidualRisk or HumanAction.PublishReport
        or HumanAction.CreateReportLink or HumanAction.RevokeReportLink or HumanAction.DeleteWithinPolicy
        or HumanAction.SecurityRoleAdministration;

    internal static HumanAuthorizationDecision Evaluate(HumanSubjectKey subject, HumanAuthorizationRequest request,
        AuthoritativeHumanSnapshot snapshot, VerifiedPrivilegedAuthentication? authentication, DateTimeOffset now)
    {
        if (!ValidSubject(subject) || !Enum.IsDefined(request.Action) || !Enum.IsDefined(request.Projection)
            || !ValidScope(request.Scope) || request.ResourceId == Guid.Empty || request.ExpectedRevision < 1
            || string.IsNullOrWhiteSpace(request.EvidenceCategory) || snapshot.Subject != subject
            || !snapshot.Authenticated || !snapshot.Active || !snapshot.TrustedTenant
            || snapshot.SubjectSecurityVersion < 1 || snapshot.SubjectSecurityVersion != snapshot.SessionSecurityVersion
            || snapshot.SessionId == Guid.Empty || !Fresh(snapshot.ResolvedAt, now, TimeSpan.FromMinutes(1))
            || !Fresh(snapshot.ProviderStatusCheckedAt, now, TimeSpan.FromMinutes(15))
            || snapshot.ResourceScope != request.Scope || snapshot.ResourceId != request.ResourceId
            || snapshot.ResourceRevision != request.ExpectedRevision || snapshot.ResourceProjection != request.Projection
            || snapshot.ResourceEvidenceCategory != request.EvidenceCategory || snapshot.ResourceAllowedActions is null
            || snapshot.ResourceAllowedActions.Any(action => !Enum.IsDefined(action))
            || !snapshot.ResourceAllowedActions.Contains(request.Action) || !Enum.IsDefined(snapshot.State)
            || snapshot.State is ResourceState.SoftDeleted or ResourceState.Expired
            || snapshot.ResourceExpiresAt <= now || snapshot.Revoked || snapshot.HoldBlocksAction
            || snapshot.RetentionBlocksAction || snapshot.CapabilityBlocksAction || snapshot.CustomerPolicy is null
            || !snapshot.CustomerPolicy.Active || snapshot.CustomerPolicy.AllowedEvidenceCategories is null
            || !snapshot.CustomerPolicy.AllowedEvidenceCategories.Any(category => string.Equals(category, request.EvidenceCategory, StringComparison.Ordinal))
            || snapshot.CoarseAppRoles is null || snapshot.CoarseAppRoles.Any(role => !Enum.IsDefined(role))
            || snapshot.Assignments.IsDefault || snapshot.Assignments.Length == 0
            || snapshot.Assignments.Any(a => a is null || !ValidAssignment(a))
            || snapshot.Assignments.Select(a => (a.Role, a.Scope)).Distinct().Count() != snapshot.Assignments.Length)
            return HumanAuthorizationDecision.Denied;

        if (snapshot.IsGuest ? !ValidGuest(snapshot.Guest, now) : snapshot.Guest is not null)
            return HumanAuthorizationDecision.Denied;
        if (snapshot.State == ResourceState.Published && !AllowedPublishedLifecycle(request.Action))
            return HumanAuthorizationDecision.Denied;
        if (!PolicyPermits(snapshot.CustomerPolicy, request.Action)) return HumanAuthorizationDecision.Denied;
        if (IsPrivileged(request.Action) && !ValidAuthentication(authentication, subject, snapshot, request.Action, now))
            return HumanAuthorizationDecision.Denied;

        foreach (var assignment in snapshot.Assignments)
        {
            if (!assignment.Active || assignment.ExpiresAt <= now || assignment.Scope != request.Scope
                || !assignment.EvidenceCategories.Any(category => string.Equals(category, request.EvidenceCategory, StringComparison.Ordinal))
                || !snapshot.CoarseAppRoles.Contains(CoarseRole(assignment.Role))) continue;
            if (RolePermits(assignment, request.Action, request.Projection))
                return new(true, assignment.Role, request.Projection);
        }
        return HumanAuthorizationDecision.Denied;
    }

    private static bool ValidSubject(HumanSubjectKey subject) => subject.TenantId != Guid.Empty
        && subject.ObjectId != Guid.Empty && subject.Kind == ActorKind.Human;
    private static bool ValidScope(HumanScope? scope) => scope is not null && scope.CustomerId != Guid.Empty
        && scope.ProjectId != Guid.Empty && scope.EnvironmentId != Guid.Empty && scope.AssessmentId != Guid.Empty;
    private static bool ValidAssignment(HumanAssignment assignment) => Enum.IsDefined(assignment.Role)
        && ValidScope(assignment.Scope) && assignment.EvidenceCategories is not null
        && assignment.EvidenceCategories.All(category => !string.IsNullOrWhiteSpace(category))
        && assignment.Conditions is not null && assignment.Conditions.All(condition => Enum.IsDefined(condition));
    private static bool Fresh(DateTimeOffset timestamp, DateTimeOffset now, TimeSpan maximumAge)
        => timestamp <= now && now - timestamp <= maximumAge;
    private static bool ValidGuest(GuestLifecycle? guest, DateTimeOffset now) => guest is not null
        && guest.SponsorId != Guid.Empty && guest.SponsorActive && !guest.SponsorOrEngagementChanged
        && guest.AssignedAt <= now && guest.ExpiresAt > now && guest.ExpiresAt > guest.AssignedAt
        && guest.ExpiresAt - guest.AssignedAt <= TimeSpan.FromDays(90)
        && guest.LastReviewedAt >= guest.AssignedAt && Fresh(guest.LastReviewedAt, now, TimeSpan.FromDays(30));
    private static bool ValidAuthentication(VerifiedPrivilegedAuthentication? authentication, HumanSubjectKey subject,
        AuthoritativeHumanSnapshot snapshot, HumanAction action, DateTimeOffset now) => authentication is not null
        && authentication.Subject == subject && authentication.SessionId == snapshot.SessionId
        && authentication.SecurityVersion == snapshot.SubjectSecurityVersion && authentication.Action == action
        && authentication.RequiredMfaVerified && authentication.RequiredConditionalAccessVerified
        && Fresh(authentication.AuthenticatedAt, now, TimeSpan.FromMinutes(15));
    private static bool AllowedPublishedLifecycle(HumanAction action) => IsRead(action) || action is
        HumanAction.AcknowledgeReport or HumanAction.CreateReportLink or HumanAction.RevokeReportLink
        or HumanAction.DeleteWithinPolicy;
    private static bool IsRead(HumanAction action) => action is HumanAction.ViewDashboard
        or HumanAction.ViewNormalizedExcerpt or HumanAction.ViewProtectedRawEvidence or HumanAction.ViewAuditHistory;
    private static bool PolicyPermits(HumanCustomerPolicy policy, HumanAction action) => action switch
    {
        HumanAction.ViewProtectedRawEvidence or HumanAction.AuthorizeProtectedEvidence => policy.ProtectedEvidenceAllowed,
        HumanAction.ConfigureProfileRulesOutcomesAiBudget or HumanAction.OverrideAiBudget => policy.AiAllowed,
        HumanAction.ExportConsultantTasksCsv => policy.ExportAllowed,
        HumanAction.CreateReportLink or HumanAction.RevokeReportLink => policy.ExportAllowed && policy.SharingAllowed,
        HumanAction.DeleteWithinPolicy => policy.DeletionAllowed,
        _ => true
    };
    private static CoarseAppRole CoarseRole(HumanRole role) => role switch
    {
        HumanRole.Consultant => CoarseAppRole.PilotConsultant,
        HumanRole.Auditor => CoarseAppRole.PilotAuditor,
        HumanRole.PlatformSupport => CoarseAppRole.PilotPlatformOperator,
        _ => CoarseAppRole.PilotCustomerUser
    };

    private static bool RolePermits(HumanAssignment assignment, HumanAction action, ReadProjection projection)
    {
        bool Has(GrantCondition condition) => assignment.Conditions.Contains(condition);
        var role = assignment.Role;
        if (!IsRead(action) && projection != ReadProjection.None) return false;
        return action switch
        {
            HumanAction.ViewDashboard => role switch
            {
                HumanRole.Consultant or HumanRole.CustomerReviewer or HumanRole.CustomerRiskOwner => projection == ReadProjection.Dashboard,
                HumanRole.EvidenceAuthorizer => Has(GrantCondition.SeparateViewer) && projection == ReadProjection.Dashboard,
                HumanRole.Executive => projection == ReadProjection.SummaryExcerpts,
                HumanRole.Auditor => projection == ReadProjection.ApprovedRecords,
                HumanRole.PlatformSupport => Has(GrantCondition.SupportMetadataPolicy) && projection == ReadProjection.OperationalMetadata,
                _ => false
            },
            HumanAction.ViewNormalizedExcerpt => projection == ReadProjection.NormalizedExcerpt && (role switch
            {
                HumanRole.Consultant or HumanRole.CustomerReviewer => true,
                HumanRole.EvidenceAuthorizer or HumanRole.CustomerRiskOwner => Has(GrantCondition.SeparateViewer),
                HumanRole.Auditor => Has(GrantCondition.ApprovedReportProvenance),
                _ => false
            }),
            HumanAction.ViewProtectedRawEvidence => projection == ReadProjection.ProtectedRaw
                && Has(GrantCondition.CustomerProtectedAuthorization) && Has(GrantCondition.DedicatedProtectedPermission)
                && (role is HumanRole.Consultant or HumanRole.CustomerReviewer or HumanRole.CustomerRiskOwner
                    || role == HumanRole.EvidenceAuthorizer && Has(GrantCondition.SeparateViewer)),
            HumanAction.AuthorizeProtectedEvidence => role == HumanRole.EvidenceAuthorizer,
            HumanAction.ConfigureStartAssessment or HumanAction.ConfigureProfileRulesOutcomesAiBudget => role == HumanRole.Consultant,
            HumanAction.OverrideAiBudget => role == HumanRole.Consultant && Has(GrantCondition.AuditedOverride),
            HumanAction.ReviewFinding => role is HumanRole.Consultant or HumanRole.CustomerReviewer,
            HumanAction.EditPresentationBusinessContext => role == HumanRole.Consultant
                || role == HumanRole.CustomerReviewer && Has(GrantCondition.ReviewerEditPermission),
            HumanAction.AcceptResidualRisk => role == HumanRole.CustomerRiskOwner && Has(GrantCondition.RiskRequiredFields),
            HumanAction.CreateRecommendationPackage => role == HumanRole.Consultant,
            HumanAction.CommentReviewRecommendation => role is HumanRole.Consultant or HumanRole.CustomerReviewer,
            HumanAction.ExportConsultantTasksCsv => role == HumanRole.Consultant
                || role == HumanRole.Auditor && Has(GrantCondition.ExplicitExportGrant),
            HumanAction.PublishReport => role == HumanRole.Consultant && Has(GrantCondition.PublishWarningAcknowledged),
            HumanAction.AcknowledgeReport => role == HumanRole.CustomerRiskOwner
                || role == HumanRole.Executive && Has(GrantCondition.ExecutiveAcknowledgmentAuthority),
            HumanAction.CreateReportLink or HumanAction.RevokeReportLink => role == HumanRole.Consultant,
            HumanAction.DeleteWithinPolicy => Has(GrantCondition.ExplicitDeletionPermission)
                && (role == HumanRole.Consultant || role == HumanRole.CustomerRiskOwner && Has(GrantCondition.SeparateCustomerAdministrator)),
            HumanAction.ViewAuditHistory => (role, projection) switch
            {
                (HumanRole.Consultant, ReadProjection.ProjectBusinessAudit) => true,
                (HumanRole.CustomerReviewer, ReadProjection.RelatedReviewHistory) => true,
                (HumanRole.EvidenceAuthorizer, ReadProjection.AuthorizationEvents) => true,
                (HumanRole.CustomerRiskOwner, ReadProjection.RiskAcknowledgmentEvents) => true,
                (HumanRole.Auditor, ReadProjection.ApprovedAuditScope) => true,
                (HumanRole.PlatformSupport, ReadProjection.OperationalMetadata) => Has(GrantCondition.SupportMetadataPolicy),
                _ => false
            },
            _ => false
        };
    }
}
