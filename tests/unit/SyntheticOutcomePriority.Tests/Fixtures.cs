using System.Collections.Immutable;
using AssessmentCoverage;
using AssessmentScoring;
using SyntheticOutcomePriority;

internal static class Fixtures
{
    public static int Assertions;
    public static void Check(bool value, string label) { Assertions++; if (!value) throw new Exception(label); }
    public static readonly Guid Run = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly string Hash = new('a', 64);
    public static OutcomeAuthority Consultant => new("synthetic-consultant", true, true, false, true, OutcomeScope.Fixed, [OutcomeRole.Consultant], ["SECURITY", "OPERATIONS"], [OutcomeAction.ReadOutcome, OutcomeAction.ManageOutcome, OutcomeAction.ReadPlanning, OutcomeAction.ManagePlanning], OutcomeResourceState.Mutable);
    public static OutcomeAuthority Approver => Consultant with { ActorId = OutcomePriorityContract.ApproverId, Roles = [OutcomeRole.CustomerOutcomeApprover], Actions = [OutcomeAction.ReadOutcome, OutcomeAction.ApproveOutcome] };
    public static OutcomeContentVersion Content(string id = "access-governance", long version = 1, string title = "Fictional access governance") => OutcomePriorityCanonical.Seal(new OutcomeContentVersion(id, version, "SECURITY", title, "Fictional governance is explicitly evidenced.", OutcomeOrigin.Inferred, [new CoverageKey("object-det", "CONFIGURATION"), new CoverageKey("object-ai", "CONFIGURATION")], ["fixture-reference"], ["Fictional assertion; no customer evidence."], version > 1 ? version - 1 : null, ""));
    public static PriorityFactorVector Factors => new(ScoringSeverity.High, Exposure.Internal, Enumerable.Range(1, 10).Select(i => "object-" + i).ToImmutableArray(), DependencyCriticality.Ordinary, EffortSize.S, [new("access-governance", 1m), new("resilience", .75m)], ["access-governance"]);
    public static Phase1BPlanningSource Source(long revision = 1) => OutcomePriorityCanonical.Seal(new Phase1BPlanningSource(OutcomeScope.Fixed, Run, OutcomePriorityContract.ProfileId, OutcomePriorityContract.ApplicationVersion, OutcomePriorityContract.ContractDigest, Hash, new string(revision == 1 ? 'b' : 'c', 64), revision, [new("finding-one", "option-one", "SECURITY", Factors)], ""));
}
