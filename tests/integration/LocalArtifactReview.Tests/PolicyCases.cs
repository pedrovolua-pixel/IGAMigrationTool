using SyntheticFixReview;

internal static class PolicyCases
{
    internal static ArtifactReviewAuthority Consultant { get; } = new("synthetic-consultant", true, true, false, true,
        new("synthetic-customer", "synthetic-project", "synthetic-environment"), [ArtifactReviewRole.Consultant],
        ["SECURITY", "OPERATIONS"], [ArtifactReviewAction.Read, ArtifactReviewAction.Review], ArtifactReviewResourceState.Mutable);
    internal static void Run()
    {
        Check.That(ArtifactReviewPolicy.Authorize(Consultant, ArtifactReviewScope.Fixed, "SECURITY") is null, "approved-trusted-consultant-exact-authority");
        foreach (var bad in new[]
        {
            Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { Revoked = true },
            Consultant with { AssignmentActive = false }, Consultant with { ActorId = "" }, Consultant with { Roles = [] },
            Consultant with { AssignedScope = new("foreign", "synthetic-project", "synthetic-environment") },
            Consultant with { AssignedScope = new("synthetic-customer", "foreign", "synthetic-environment") },
            Consultant with { AssignedScope = new("synthetic-customer", "synthetic-project", "foreign") },
            Consultant with { Categories = [] }, Consultant with { Categories = ["OPERATIONS"] },
            Consultant with { Actions = [] }, Consultant with { Actions = [ArtifactReviewAction.Read] },
            Consultant with { Actions = [ArtifactReviewAction.Review] }, Consultant with { Actions = [(ArtifactReviewAction)999] },
            Consultant with { Roles = [(ArtifactReviewRole)999] }, Consultant with { Categories = ["SECURITY", "foreign"] }
        }) Check.That(ArtifactReviewPolicy.Authorize(bad, ArtifactReviewScope.Fixed, "SECURITY") is not null, "authority-denials-no-client-grant-fallback");
        foreach (var role in Enum.GetValues<ArtifactReviewRole>().Where(r => r != ArtifactReviewRole.Consultant))
            Check.That(ArtifactReviewPolicy.Authorize(Consultant with { Roles = [role] }, ArtifactReviewScope.Fixed) is not null, "all-other-artifact-roles-alone-denied");
        foreach (var state in Enum.GetValues<ArtifactReviewResourceState>().Where(s => s != ArtifactReviewResourceState.Mutable))
            Check.That(ArtifactReviewPolicy.Authorize(Consultant with { ResourceState = state }, ArtifactReviewScope.Fixed) is not null, "immutable-deleted-blocked-expired-artifact-authority-denied");
        Check.That(ArtifactReviewPolicy.Authorize(null, ArtifactReviewScope.Fixed) is not null, "missing-trusted-actor-denied");
        var valid = new ArtifactReviewCommand(Guid.Parse("abcdef01-2345-4567-89ab-cdef01234567"), ArtifactReviewKind.ReviewForPlanning, 0, new string('a', 64), "  Fictional café 中文 😀\0literal\r\nCRLF\rCR  ");
        Check.That(ArtifactReviewPolicy.ValidateCommand(valid) is null, "exact-original-reason-control-unicode-allowed");
        foreach (var reason in new[] { new string('a', 2000), string.Concat(Enumerable.Repeat("😀", 1000)), "\0" })
            Check.That(ArtifactReviewPolicy.ValidateCommand(valid with { Reason = reason }) is null, "exact-UTF16-reason-boundary-and-nonblank-NUL");
        foreach (var bad in new[]
        {
            valid with { EventId = Guid.Empty }, valid with { Kind = (ArtifactReviewKind)999 }, valid with { ExpectedRevision = -1 },
            valid with { ExpectedRevision = Expected.MaximumRevision + 1 }, valid with { ExpectedSourceDigest = "" },
            valid with { ExpectedSourceDigest = new string('A',64) }, valid with { Reason = "" }, valid with { Reason = " \r\n\t " }, valid with { Reason = "\uD800" }, valid with { Reason = "\uDC00" },
            valid with { Reason = new string('a',2001) }, valid with { Reason = string.Concat(Enumerable.Repeat("😀",1000))+"a" }
        }) Check.That(ArtifactReviewPolicy.ValidateCommand(bad) is not null, "closed-command-shape-reason-safe-revision-denied");
        Check.That(ArtifactReviewPolicy.ValidateCommand(valid with { ExpectedRevision = Expected.MaximumRevision }) is null, "JS-safe-maximum-command-validation-accepted-before-actual-overflow");
        foreach (var state in Enum.GetValues<ArtifactReviewState>())
            foreach (var kind in Enum.GetValues<ArtifactReviewKind>())
            {
                var allowed = kind == ArtifactReviewKind.ReviewForPlanning ? state is ArtifactReviewState.Unverified or ArtifactReviewState.NeedsReview : state == ArtifactReviewState.ReviewedForPlanning;
                Check.Equal(ArtifactReviewPolicy.Transition(state, kind) is null, allowed, "complete-explicit-review-withdraw-transition-matrix");
            }
        Check.Group("AR13-T02/T03/T04 authority/reason/range/transition matrix");
    }
}
