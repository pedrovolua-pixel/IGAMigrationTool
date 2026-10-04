using FindingReview;

internal static class ReviewPolicyCases
{
    internal static SyntheticReviewAuthority Consultant => new("synthetic-independent-consultant", true, true, false, true,
        SyntheticReviewScope.Fixed, [SyntheticReviewRole.Consultant], ["SECURITY", "OPERATIONS"],
        [SyntheticReviewAction.Read, SyntheticReviewAction.Review, SyntheticReviewAction.AddComment, SyntheticReviewAction.EditPresentation]);

    internal static void Run()
    {
        foreach (var action in Enum.GetValues<SyntheticReviewAction>())
        {
            Check.Equal(SyntheticReviewPolicy.Authorize(Consultant, SyntheticReviewScope.Fixed, "SECURITY", action, SyntheticReviewResourceState.Mutable), null,
                $"active scoped consultant allowed {action}");
            Check.Equal(SyntheticReviewPolicy.Authorize(Consultant with { Roles = [SyntheticReviewRole.QualifiedReviewer] }, SyntheticReviewScope.Fixed, "SECURITY", action, SyntheticReviewResourceState.Mutable), null,
                $"explicitly action-granted qualified reviewer allowed {action}");
            foreach (var changed in new[]
            {
                Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { Revoked = true },
                Consultant with { AssignmentActive = false }, Consultant with { AssignedScope = SyntheticReviewScope.Fixed with { CustomerId = "wrong-customer" } },
                Consultant with { AssignedScope = SyntheticReviewScope.Fixed with { ProjectId = "wrong-project" } },
                Consultant with { AssignedScope = SyntheticReviewScope.Fixed with { EnvironmentId = "wrong-environment" } },
                Consultant with { Categories = ["OPERATIONS"] }, Consultant with { Actions = [] },
                Consultant with { Roles = [SyntheticReviewRole.Auditor] }, Consultant with { Roles = [SyntheticReviewRole.Executive] },
                Consultant with { Roles = [SyntheticReviewRole.CustomerRiskOwner] }, Consultant with { Roles = [SyntheticReviewRole.PlatformSupport] }
            }) Check.Equal(SyntheticReviewPolicy.Authorize(changed, SyntheticReviewScope.Fixed, "SECURITY", action, SyntheticReviewResourceState.Mutable), SyntheticReviewIssue.Denied,
                "identity/assignment/role/category/action cannot supply unauthorized review authority");
            Check.Equal(SyntheticReviewPolicy.Authorize(null, SyntheticReviewScope.Fixed, "SECURITY", action, SyntheticReviewResourceState.Mutable), SyntheticReviewIssue.Denied, "null actor denied");
            Check.Equal(SyntheticReviewPolicy.Authorize(Consultant, SyntheticReviewScope.Fixed with { CustomerId = "wrong-customer" }, "SECURITY", action, SyntheticReviewResourceState.Mutable),
                SyntheticReviewIssue.WrongScope, "wrong resource scope cannot route to fixed data plane");
            foreach (var state in new[] { SyntheticReviewResourceState.Published, SyntheticReviewResourceState.Deleted, SyntheticReviewResourceState.Blocked, SyntheticReviewResourceState.Expired })
                Check.Equal(SyntheticReviewPolicy.Authorize(Consultant, SyntheticReviewScope.Fixed, "SECURITY", action, state), SyntheticReviewIssue.Denied, "unsupported resource state never grants mutation/read authority");
        }
        Check.Equal(SyntheticReviewPolicy.Authorize(Consultant, SyntheticReviewScope.Fixed, "SECURITY", (SyntheticReviewAction)999, SyntheticReviewResourceState.Mutable), SyntheticReviewIssue.Denied,
            "unknown action cannot introduce risk or remediation grant");
        Check.Group("RM-POL-001 server-supplied identity role/scope/category/assignment/state deny matrix");

        var current = new SyntheticFindingCurrent(new string('a', 64), "SECURITY", 0, SyntheticFindingState.Proposed, "Original fictional title", "");
        foreach (var kind in new[] { SyntheticReviewEventKind.Confirm, SyntheticReviewEventKind.Reject, SyntheticReviewEventKind.Defer })
        {
            var command = new SyntheticReviewCommand(Guid.NewGuid(), 0, kind, Reason: kind == SyntheticReviewEventKind.Reject ? "Independent false-positive reason" : null);
            Check.Equal(SyntheticReviewPolicy.ValidateCommand(command), null, "bounded disposition payload valid");
            Check.Equal(SyntheticReviewPolicy.ValidateTransition(current, command), null, "only proposed transitions allowed");
            foreach (var state in new[] { SyntheticFindingState.AutoConfirmed, SyntheticFindingState.Confirmed, SyntheticFindingState.Rejected, SyntheticFindingState.Deferred })
                Check.Equal(SyntheticReviewPolicy.ValidateTransition(current with { State = state }, command), SyntheticReviewIssue.InvalidState, "extra disposition transition refused");
        }
        foreach (var reason in new string?[] { null, "", "  ", new('r', 2001) })
            Invalid(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: reason), "Reject requires bounded nonblank reason");
        foreach (var command in new[]
        {
            new SyntheticReviewCommand(Guid.Empty, 0, SyntheticReviewEventKind.Confirm),
            new SyntheticReviewCommand(Guid.NewGuid(), -1, SyntheticReviewEventKind.Confirm),
            new SyntheticReviewCommand(Guid.NewGuid(), long.MaxValue, SyntheticReviewEventKind.Confirm),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, (SyntheticReviewEventKind)999),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm, Text: "unexpected"),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Reason: "unexpected", Text: "comment"),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: ""),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: new('x', 2001)),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: " "),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: new('x', 251)),
            new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, BusinessContext: new('x', 2001))
        }) Invalid(command, "invalid or extra payload fields fail closed");
        var hostile = "<script>window.syntheticInjected=true</script><img src=x onerror=alert(1)> & ' quoted";
        Check.Equal(SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: hostile)), null, "plain hostile text stays data; browser encoding is tested separately");
        Check.Equal(SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: new('x', 2000))), null, "exact2000 character comment accepted");
        Check.Equal(SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: new('x', 250), BusinessContext: "")), null,
            "exact250 title and empty business-context clearing accepted");
        Check.Group("RM-POL-002 proposed-only transitions reason/text boundaries and unsupported grants");
    }
    private static void Invalid(SyntheticReviewCommand command, string name)
        => Check.Equal(SyntheticReviewPolicy.ValidateCommand(command), SyntheticReviewIssue.InvalidInput, name);
}
