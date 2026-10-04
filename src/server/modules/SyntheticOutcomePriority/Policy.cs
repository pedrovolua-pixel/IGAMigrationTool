using System.Collections.Immutable;
using System.Text.Json;
using AssessmentScoring;

namespace SyntheticOutcomePriority;

public static class OutcomePriorityContract
{
    public const string ProfileId = "synthetic-phase1b-combined-v1";
    public const string ApplicationVersion = "synthetic-phase1b-app-v1";
    public const string ContractDigest = "f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8";
    public static Guid RegistryFenceId { get; } = Guid.Parse("1b000000-0000-4000-8000-000000000001");
    public const long MaximumRevision = 9_007_199_254_740_991;
    public const string ApproverId = "synthetic-customer-outcome-approver-v1";
}
public static class OutcomePriorityPolicy
{
    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool Text(string? value, int maximum = 2000) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Contains('\0') && JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value)) == value;
    public static bool Id(string? value) => Text(value, 128) && value!.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    public static bool Category(string value) => value is "SECURITY" or "OPERATIONS";
    public static OutcomePriorityIssue? Authorize(OutcomeAuthority? authority, OutcomeScope scope, OutcomeAction action, string? category = null)
    {
        if (scope != OutcomeScope.Fixed) return OutcomePriorityIssue.WrongScope;
        if (authority is null || !Id(authority.ActorId) || !authority.Authenticated || !authority.Active || authority.Revoked || !authority.AssignmentActive || authority.AssignedScope != scope ||
            authority.Roles.IsDefaultOrEmpty || authority.Roles.Length != 1 || !Enum.IsDefined(authority.Roles[0]) || authority.Actions.IsDefaultOrEmpty || authority.Actions.Any(a => !Enum.IsDefined(a)) || !authority.Actions.Contains(action) ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(c => !Category(c)) || authority.Categories.Distinct(StringComparer.Ordinal).Count() != authority.Categories.Length ||
            category is not null && !authority.Categories.Contains(category) || authority.ResourceState != OutcomeResourceState.Mutable) return OutcomePriorityIssue.Denied;
        var role = authority.Roles[0];
        if (action == OutcomeAction.ApproveOutcome)
            return role == OutcomeRole.CustomerOutcomeApprover && authority.ActorId == OutcomePriorityContract.ApproverId ? null : OutcomePriorityIssue.Denied;
        if (action is OutcomeAction.ManageOutcome or OutcomeAction.ManagePlanning or OutcomeAction.ReadPlanning)
            return role == OutcomeRole.Consultant ? null : OutcomePriorityIssue.Denied;
        return role is OutcomeRole.Consultant or OutcomeRole.CustomerOutcomeApprover ? null : OutcomePriorityIssue.Denied;
    }
    public static bool Content(OutcomeContentVersion c) => c is not null && Id(c.OutcomeId) && c.Version is > 0 and <= OutcomePriorityContract.MaximumRevision && Category(c.CategoryId) && Text(c.Title, 256) && Text(c.Behavior) && Enum.IsDefined(c.Origin) &&
        !c.UnitLinks.IsDefaultOrEmpty && c.UnitLinks.Length <= 256 && c.UnitLinks.All(k => k is not null && Id(k.InventoryId) && Id(k.EvidenceCategory)) && c.UnitLinks.Distinct().Count() == c.UnitLinks.Length &&
        !c.ReferenceIds.IsDefault && c.ReferenceIds.Length <= 256 && c.ReferenceIds.All(Id) && c.ReferenceIds.Distinct(StringComparer.Ordinal).Count() == c.ReferenceIds.Length &&
        !c.Assumptions.IsDefault && c.Assumptions.Length <= 64 && c.Assumptions.All(v => Text(v)) &&
        (c.PredecessorVersion is null || c.PredecessorVersion > 0 && c.PredecessorVersion < c.Version) && ValidDigest(c.ContentDigest) && OutcomePriorityCanonical.Json(c) == OutcomePriorityCanonical.Json(OutcomePriorityCanonical.Seal(c));
    public static bool Command(OutcomeCommand c) => c is not null && c.EventId != Guid.Empty && Enum.IsDefined(c.Kind) && Id(c.OutcomeId) && c.Version is > 0 and <= OutcomePriorityContract.MaximumRevision && c.ExpectedRevision is >= 0 and <= OutcomePriorityContract.MaximumRevision && c.ExpectedRegistryRevision is >= 0 and <= OutcomePriorityContract.MaximumRevision && ValidDigest(c.ExpectedContentDigest) && Text(c.Reason) &&
        (c.Kind == OutcomeKind.CreateDraft ? c.ExpectedRevision == 0 && c.Content is not null && Content(c.Content) && c.Content.OutcomeId == c.OutcomeId && c.Content.Version == c.Version && c.Content.ContentDigest == c.ExpectedContentDigest && c.ExpectedReviewEventId is null : c.Content is null);
    public static bool Command(PlanningCommand c) => c is not null && c.EventId != Guid.Empty && Enum.IsDefined(c.Kind) && Id(c.OptionId) && c.ExpectedRevision is >= 0 and <= OutcomePriorityContract.MaximumRevision && ValidDigest(c.ExpectedSourceDigest) && Text(c.Reason) && !c.Assumptions.IsDefault && c.Assumptions.Length <= 64 && c.Assumptions.All(a => Text(a)) &&
        (c.Kind == PlanningKind.OverridePriority ? c.PriorityOverride is not null && Enum.IsDefined(c.PriorityOverride.Value) : c.PriorityOverride is null) &&
        (c.Kind == PlanningKind.ReplaceEffort ? c.ReplacementSize is not null && Enum.IsDefined(c.ReplacementSize.Value) && c.Assumptions.Length > 0 : c.ReplacementSize is null && c.Assumptions.Length == 0);
    public static bool Source(Phase1BPlanningSource s) => s is not null && s.Scope == OutcomeScope.Fixed && s.RunId != Guid.Empty && s.ProfileId == OutcomePriorityContract.ProfileId && s.ApplicationVersion == OutcomePriorityContract.ApplicationVersion && s.ContractDigest == OutcomePriorityContract.ContractDigest && ValidDigest(s.RunInputDigest) && ValidDigest(s.GuidanceDigest) && s.SourceRevision is >= 0 and <= OutcomePriorityContract.MaximumRevision && !s.Options.IsDefault && s.Options.Length <= 256 && s.Options.All(o => o is not null && Id(o.FindingId) && Id(o.OptionId) && Category(o.CategoryId) && PriorityProjector.Valid(o.Factors)) && s.Options.Select(o => o.OptionId).Distinct(StringComparer.Ordinal).Count() == s.Options.Length && ValidDigest(s.SourceDigest) && OutcomePriorityCanonical.Json(s) == OutcomePriorityCanonical.Json(OutcomePriorityCanonical.Seal(s));
}
public static class PriorityProjector
{
    public static bool Valid(PriorityFactorVector f) => f is not null && Enum.IsDefined(f.Severity) && (f.Exposure is null || Enum.IsDefined(f.Exposure.Value)) && (f.AffectedObjectIds.IsDefault || f.AffectedObjectIds.Length <= 100_000 && f.AffectedObjectIds.All(OutcomePriorityPolicy.Id) && f.AffectedObjectIds.Distinct(StringComparer.Ordinal).Count() == f.AffectedObjectIds.Length) &&
        (f.Dependency is null || Enum.IsDefined(f.Dependency.Value)) && (f.OriginalEffort is null || Enum.IsDefined(f.OriginalEffort.Value)) &&
        (f.Objectives.IsDefault || f.Objectives.Length <= 64 && f.Objectives.All(o => o is not null && OutcomePriorityPolicy.Id(o.ObjectiveId) && o.Weight is >= 0 and <= 1) && f.Objectives.Select(o => o.ObjectiveId).Distinct(StringComparer.Ordinal).Count() == f.Objectives.Length) &&
        !f.MatchedObjectiveIds.IsDefault && f.MatchedObjectiveIds.Length <= 64 && f.MatchedObjectiveIds.All(OutcomePriorityPolicy.Id) && f.MatchedObjectiveIds.Distinct(StringComparer.Ordinal).Count() == f.MatchedObjectiveIds.Length &&
        (f.Objectives.IsDefault || f.MatchedObjectiveIds.All(id => f.Objectives.Any(o => o.ObjectiveId == id)));
    public static PriorityProjection? Project(PriorityFactorVector factors)
    {
        if (!Valid(factors)) return null;
        var missing = ImmutableArray.CreateBuilder<string>();
        if (factors.AffectedObjectIds.IsDefault) missing.Add("affectedObjects");
        if (factors.Exposure is null) missing.Add("exposure"); if (factors.Dependency is null) missing.Add("dependency"); if (factors.OriginalEffort is null) missing.Add("effort"); if (factors.Objectives.IsDefault) missing.Add("objectives");
        if (missing.Count != 0) return new("synthetic-priority-policy-v1", null, null, null, missing.ToImmutable(), []);
        decimal severity = factors.Severity switch { ScoringSeverity.Critical => 1m, ScoringSeverity.High => .8m, ScoringSeverity.Medium => .45m, ScoringSeverity.Low => .2m, _ => 0m };
        decimal exposure = factors.Exposure switch { Exposure.Isolated => 0m, Exposure.Internal => .25m, Exposure.PartnerFacing => .5m, _ => 1m };
        decimal dependency = factors.Dependency switch { DependencyCriticality.None => 0m, DependencyCriticality.Ordinary => .25m, DependencyCriticality.Important => .5m, _ => 1m };
        decimal quick = factors.OriginalEffort switch { EffortSize.XS => 1m, EffortSize.S => .75m, EffortSize.M => .5m, EffortSize.L => .25m, _ => 0m };
        var objectives = factors.Objectives.Where(o => factors.MatchedObjectiveIds.Contains(o.ObjectiveId)).Select(o => o.Weight).DefaultIfEmpty(0m).Max();
        var terms = new[] { ("severity", severity, .30m), ("exposure", exposure, .20m), ("affectedObjects", Math.Min(factors.AffectedObjectIds.Length, 100) / 100m, .15m), ("dependency", dependency, .15m), ("effort", quick, .10m), ("objectives", objectives, .10m) }.Select(t => new PriorityContribution(t.Item1, t.Item2, t.Item3, 100m * t.Item2 * t.Item3)).ToImmutableArray();
        var raw = terms.Sum(t => t.Points);
        return new("synthetic-priority-policy-v1", raw, decimal.Round(raw, 1, MidpointRounding.AwayFromZero), Band(raw), [], terms);
    }
    public static PriorityBand Band(decimal raw) => raw >= 80 ? PriorityBand.Immediate : raw >= 60 ? PriorityBand.High : raw >= 40 ? PriorityBand.Medium : PriorityBand.Low;
    public static EffortEstimate Estimate(EffortSize size) => size switch
    {
        EffortSize.XS => new("synthetic-effort-policy-v1", size, 1, 2),
        EffortSize.S => new("synthetic-effort-policy-v1", size, 2, 8),
        EffortSize.M => new("synthetic-effort-policy-v1", size, 8, 24),
        EffortSize.L => new("synthetic-effort-policy-v1", size, 24, 80),
        EffortSize.XL => new("synthetic-effort-policy-v1", size, 80, 160),
        _ => throw new ArgumentException("Invalid fictional effort size.")
    };
}
