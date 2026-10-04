using System.Collections.Immutable;
using System.Text.Json;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class Policies
{
    internal static PlanningTaskAuthority Consultant { get; } = new("synthetic-consultant", true, true, false, true,
        PlanningTaskScope.Fixed, [PlanningTaskRole.Consultant], ["SECURITY", "OPERATIONS"],
        [PlanningTaskAction.Read, PlanningTaskAction.Create, PlanningTaskAction.Manage, PlanningTaskAction.Comment], PlanningTaskResourceState.Mutable);
    internal static ArtifactReviewAuthority ArtifactConsultant { get; } = new("synthetic-consultant", true, true, false, true,
        ArtifactReviewScope.Fixed, [ArtifactReviewRole.Consultant], ["SECURITY", "OPERATIONS"],
        [ArtifactReviewAction.Read, ArtifactReviewAction.Review], ArtifactReviewResourceState.Mutable);
    internal static ImmutableArray<PlanningTaskAttestation> Vector() =>
        JsonSerializer.Deserialize<Dictionary<string, ImmutableArray<PlanningTaskAttestation>>>(File.ReadAllText(OracleCases.Fixture("selected-vector-golden.json")), V14Program.Web)!["reviewed"];
    internal static IEnumerable<PlanningTaskAuthority> InvalidAuthorities()
    {
        yield return Consultant with { Authenticated = false };
        yield return Consultant with { Active = false };
        yield return Consultant with { Revoked = true };
        yield return Consultant with { AssignmentActive = false };
        yield return Consultant with { ActorId = "" };
        yield return Consultant with { AssignedScope = new("foreign", "synthetic-project", "synthetic-environment") };
        yield return Consultant with { AssignedScope = new("synthetic-customer", "foreign", "synthetic-environment") };
        yield return Consultant with { AssignedScope = new("synthetic-customer", "synthetic-project", "foreign") };
        yield return Consultant with { Roles = [] };
        yield return Consultant with { Roles = [PlanningTaskRole.Consultant, PlanningTaskRole.Auditor] };
        yield return Consultant with { Roles = [(PlanningTaskRole)999] };
        yield return Consultant with { Categories = [] };
        yield return Consultant with { Categories = ["OPERATIONS"] };
        yield return Consultant with { Categories = ["SECURITY", "foreign"] };
        yield return Consultant with { Actions = [] };
        yield return Consultant with { Actions = [PlanningTaskAction.Create] };
        yield return Consultant with { Actions = [PlanningTaskAction.Read] };
        yield return Consultant with { Actions = [PlanningTaskAction.Read, (PlanningTaskAction)999] };
        foreach (var role in Enum.GetValues<PlanningTaskRole>().Where(r => r != PlanningTaskRole.Consultant)) yield return Consultant with { Roles = [role] };
        foreach (var state in Enum.GetValues<PlanningTaskResourceState>().Where(s => s != PlanningTaskResourceState.Mutable)) yield return Consultant with { ResourceState = state };
    }
    internal static void Run()
    {
        foreach (var action in Enum.GetValues<PlanningTaskAction>())
        {
            Check.That(PlanningTaskPolicy.Authorize(Consultant, PlanningTaskScope.Fixed, action, "SECURITY") is null, "actual-explicit-trusted-sole-consultant-action-category-grant");
            Check.That(PlanningTaskPolicy.Authorize(Consultant with { Actions = Consultant.Actions.Where(a => a != action).ToImmutableArray() }, PlanningTaskScope.Fixed, action, "SECURITY") is not null, "missing-exact-action-no-builder-eligibility-grant");
        }
        foreach (var bad in InvalidAuthorities())
            Check.That(PlanningTaskPolicy.Authorize(bad, PlanningTaskScope.Fixed, PlanningTaskAction.Create, "SECURITY") is not null, "actual-all-untrusted-task-authorities-denied");
        Check.That(PlanningTaskPolicy.Authorize(null, PlanningTaskScope.Fixed) is not null, "no-trusted-task-authority-denied");
        var valid = new PlanningTaskCommand(Guid.Parse("abcdef01-2345-4567-89ab-cdef01234560"), PlanningTaskKind.Create, 0, new string('a', 64), Vector(), "  Fictional V14 café 中文 😀\0NUL\r\nCRLF\rCR  ");
        Check.That(PlanningTaskPolicy.ValidateCommand(valid) is null, "exact-roundtrip-control-unicode-command");
        foreach (var text in new[] { new string('a', 2000), string.Concat(Enumerable.Repeat("😀", 1000)), "\0" })
            Check.That(PlanningTaskPolicy.ValidateCommand(valid with { Reason = text }) is null, "exact2000utf16-or-nonblank-NUL-boundary");
        foreach (var bad in new[]
        {
            valid with { EventId = Guid.Empty }, valid with { Kind = (PlanningTaskKind)999 }, valid with { ExpectedRevision = -1 },
            valid with { ExpectedRevision = 1 }, valid with { ExpectedRevision = Expected.MaximumRevision + 1 },
            valid with { ExpectedSourceDigest = "" }, valid with { ExpectedSourceDigest = new string('A',64) },
            valid with { Reason = "" }, valid with { Reason = " \t\r\n" }, valid with { Reason = "\uD800" }, valid with { Reason = "\uDC00" },
            valid with { Reason = new string('a',2001) }, valid with { Reason = string.Concat(Enumerable.Repeat("😀",1000))+"a" },
            valid with { ExpectedAttestations = default }, valid with { ExpectedAttestations = [] }, valid with { ExpectedAttestations = Vector()[..2] },
            valid with { ExpectedAttestations = Vector().Reverse().ToImmutableArray() }, valid with { ExpectedAttestations = [Vector()[0], Vector()[0], Vector()[2]] },
            valid with { ExpectedAttestations = Vector().Select(a => a with { Revision = -1 }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { Revision = Expected.MaximumRevision + 1 }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { EventId = null }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { Kind = null }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { SourceDigest = null }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { State = (ArtifactReviewState)999 }).ToImmutableArray() },
            valid with { ExpectedAttestations = Vector().Select(a => a with { Kind = ArtifactReviewKind.WithdrawReview }).ToImmutableArray() }
        }) Check.That(PlanningTaskPolicy.ValidateCommand(bad) is not null, "closed-invalid-UUID-revision-text-selected-vector-no-acceptance");
        Check.That(PlanningTaskPolicy.ValidateCommand(valid with { Kind = PlanningTaskKind.Comment, ExpectedRevision = Expected.MaximumRevision }) is null, "JS-safe-revision-input-boundary-before-store-overflow");
        foreach (var status in Enum.GetValues<PlanningTaskStatus>())
            foreach (var freshness in new[] { PlanningTaskFreshness.CurrentPlan, PlanningTaskFreshness.NeedsReconfirmation })
                foreach (var finding in new[] { "Proposed", "Confirmed", "Deferred", "Rejected" })
                    foreach (var reviewed in new[] { false, true })
                        foreach (var kind in Enum.GetValues<PlanningTaskKind>())
                        {
                            // Exhaustive literal approved predicates. SourceUnavailable is a
                            // store/capture denial before this transition helper, tested there.
                            var allowed = kind switch
                            {
                                PlanningTaskKind.StartProgress => status == PlanningTaskStatus.Planned && freshness == PlanningTaskFreshness.CurrentPlan && finding != "Rejected",
                                PlanningTaskKind.Complete => status == PlanningTaskStatus.InProgress && freshness == PlanningTaskFreshness.CurrentPlan && finding != "Rejected",
                                PlanningTaskKind.ReturnToPlanned => status == PlanningTaskStatus.InProgress,
                                PlanningTaskKind.Cancel => status is PlanningTaskStatus.Planned or PlanningTaskStatus.InProgress,
                                PlanningTaskKind.Reopen => status is PlanningTaskStatus.Completed or PlanningTaskStatus.Cancelled && finding != "Rejected",
                                PlanningTaskKind.ReconfirmPlan => status is PlanningTaskStatus.Planned or PlanningTaskStatus.InProgress && freshness == PlanningTaskFreshness.NeedsReconfirmation && finding != "Rejected" && reviewed,
                                PlanningTaskKind.Comment => true,
                                _ => false
                            };
                            Check.Equal(PlanningTaskPolicy.Transition(status, freshness, finding, reviewed, kind) is null, allowed, "all512-approved-existing-task-transition-freshness-predicates");
                        }
        Check.Group("TC14-T02/T03/T09/T11 authority/closed input/exhaustive verified-source lifecycle policy");
    }
}
