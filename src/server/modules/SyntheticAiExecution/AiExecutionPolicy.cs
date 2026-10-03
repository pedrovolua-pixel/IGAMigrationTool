using System.Collections.Immutable;
using System.Text.Json;
using AssessmentCoverage;
using SyntheticAiValidation;

namespace SyntheticAiExecution;

public static class AiExecutionPolicy
{
    public const string ContractDigest = "471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5";
    public const string PolicyVersion = "synthetic-automatic-ai-policy-v1";
    public const string ProviderVersion = "synthetic-fixed-provider-v1";
    public const string PromptVersion = "fixture-prompt-v1";
    public const string FixtureVersion = "synthetic-ai-execution-fixtures-v1";
    public const long MaximumRevision = 9_007_199_254_740_991;
    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidText(string? value, int maximum = 2000) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value)) == value;
    public static AiIssue? Authorize(AiAuthority? authority, AiAction action, string? category = null, bool billingOnly = false)
    {
        if (authority is null || authority.Scope != AiScope.Fixed) return AiIssue.WrongScope;
        if (!ValidText(authority.ActorId, 256) || !authority.Authenticated || !authority.Active || !authority.AssignmentActive || authority.Revoked ||
            authority.Roles.IsDefaultOrEmpty || authority.Roles.Any(x => !Enum.IsDefined(x)) || authority.Actions.IsDefaultOrEmpty ||
            authority.Actions.Any(x => !Enum.IsDefined(x)) || !authority.Actions.Contains(action) || authority.Categories.IsDefaultOrEmpty ||
            authority.Categories.Any(x => x is not ("SECURITY" or "OPERATIONS")) || category is not null && !authority.Categories.Contains(category) ||
            !Enum.IsDefined(authority.ResourceState)) return AiIssue.Denied;
        if (action == AiAction.Override && !authority.Roles.SequenceEqual([AiRole.Consultant]) ||
            action is AiAction.Dispatch or AiAction.Reconcile && !authority.Roles.SequenceEqual([AiRole.Worker]) ||
            action == AiAction.Read && !authority.Roles.SequenceEqual([AiRole.Consultant]) && !authority.Roles.SequenceEqual([AiRole.Worker])) return AiIssue.Denied;
        if (!billingOnly && (!authority.AiPolicyAllowed || authority.ResourceState != AiResourceState.Mutable)) return AiIssue.Denied;
        if (billingOnly && action != AiAction.Reconcile) return AiIssue.Denied;
        return null;
    }
    public static AiIssue? ValidateRun(AiRunLock? locked, ImmutableArray<AiWork> works)
    {
        if (locked is null || locked.RunId == Guid.Empty || locked.Scope != AiScope.Fixed || locked.ProfileId != "synthetic-phase1b-combined-v1" ||
            locked.ApplicationVersion != "synthetic-phase1b-app-v1" || locked.PolicyVersion != PolicyVersion || locked.ProviderVersion != ProviderVersion ||
            locked.PromptVersion != PromptVersion || locked.FixtureVersion != FixtureVersion || !ValidText(locked.Epoch, 256) ||
            !ValidText(locked.InitiatingConsultantId, 256) || new[] { locked.InputDigest, locked.BaselineDigest, locked.ProfileDigest, locked.SourceDigest, locked.MappingDigest }.Any(x => !ValidDigest(x)) ||
            works.IsDefaultOrEmpty || works.Length > 16 || works.Any(x => x is null) || works.Select(x => x.WorkId).Distinct(StringComparer.Ordinal).Count() != works.Length)
            return AiIssue.InvalidInput;
        var keys = new HashSet<CoverageKey>();
        foreach (var work in works)
        {
            if (!ValidText(work.WorkId, 256) || work.Category is not ("SECURITY" or "OPERATIONS") || !Enum.IsDefined(work.Scenario) ||
                work.Units.IsDefaultOrEmpty || work.Units.Length > 16 || work.Units.Any(x => x is null) ||
                work.Units.Select(x => x.ProposalId).Distinct(StringComparer.Ordinal).Count() != work.Units.Length) return AiIssue.InvalidInput;
            var packet = SyntheticAiPacketBuilder.Build(work.PacketInputJson);
            if (!packet.Succeeded || packet.Packet!.RunId != locked.RunId) return AiIssue.InvalidInput;
            using var document = JsonDocument.Parse(packet.Packet.CanonicalJson);
            var source = document.RootElement.GetProperty("source");
            if (source.GetProperty("baselineDigest").GetString() != locked.BaselineDigest || source.GetProperty("profileDigest").GetString() != locked.ProfileDigest)
                return AiIssue.SourceConflict;
            foreach (var unit in work.Units)
                if (unit.Key is null || !ValidText(unit.Key.InventoryId, 256) || !ValidText(unit.Key.EvidenceCategory, 256) || !keys.Add(unit.Key) ||
                    !ValidText(unit.ObjectType, 256) || !ValidText(unit.ModuleId, 256) || !ValidText(unit.RuleId, 256) || !packet.Packet.RuleIds.Contains(unit.RuleId) ||
                    !ValidText(unit.RuleVersion, 256) || !ValidText(unit.Title, 256) || !Enum.IsDefined(unit.Severity) || !ValidText(unit.Impact) || !ValidText(unit.Likelihood) ||
                    !ValidText(unit.RootCause) || unit.ProposalId is not { Length: 11 } || !unit.ProposalId.StartsWith("proposal-", StringComparison.Ordinal) ||
                    !unit.ProposalId[9..].All(char.IsAsciiDigit) || unit.EvidenceIds.IsDefaultOrEmpty || unit.EvidenceIds.Length > 16 ||
                    unit.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != unit.EvidenceIds.Length || unit.EvidenceIds.Any(x => !packet.Packet.EvidenceIds.Contains(x)))
                    return AiIssue.InvalidInput;
        }
        return locked.MappingDigest == MappingDigest(works) ? null : AiIssue.SourceConflict;
    }
    public static string MappingDigest(ImmutableArray<AiWork> works) => AiExecutionCanonical.Digest(works.OrderBy(x => x.WorkId, StringComparer.Ordinal)
        .Select(x => new { x.WorkId, x.Category, x.Units }));
    public static string LogicalKey(AiRunLock locked, AiWork work) => AiExecutionCanonical.Digest(new
    { schemaVersion = "synthetic-ai-logical-work-v1", locked.Scope, locked.RunId, work.WorkId, packetDigest = SyntheticAiPacketBuilder.Build(work.PacketInputJson).Packet!.ContentDigest, locked.ProviderVersion, locked.PromptVersion, locked.PolicyVersion, locked.MappingDigest });
    public static ImmutableArray<AiUnitOutcome> Gaps(AiWork work, CoverageState state, string reason) => work.Units.Select(x =>
        new AiUnitOutcome(x.Key, state, reason, "AI", null)).ToImmutableArray();
    public static AiOperationResult<ImmutableArray<AiUnitOutcome>> Map(AiRunLock locked, AiWork work, AiAttemptKey attempt, string? output)
    {
        var packet = SyntheticAiPacketBuilder.Build(work.PacketInputJson);
        if (!packet.Succeeded) return new(AiIssue.IntegrityMismatch, default);
        var accepted = SyntheticAiProposalValidator.Validate(packet.Packet, output);
        if (!accepted.Succeeded) return new(AiIssue.OutputRejected, default);
        using var document = JsonDocument.Parse(accepted.Snapshot!.CanonicalJson);
        var proposals = document.RootElement.GetProperty("proposals").EnumerateArray().ToArray();
        if (proposals.Any(p => !work.Units.Any(u => u.ProposalId == p.GetProperty("proposalId").GetString()))) return new(AiIssue.OutputRejected, default);
        var outcomes = new List<AiUnitOutcome>();
        foreach (var unit in work.Units)
        {
            var values = proposals.Where(x => x.GetProperty("proposalId").GetString() == unit.ProposalId).ToArray();
            if (values.Length == 0) { outcomes.Add(new(unit.Key, CoverageState.NotAssessed, "AI_NO_VALIDATED_CONCLUSION", "AI", null)); continue; }
            var proposal = values[0];
            foreach (var field in new[] { "facts", "inferences", "assumptions", "suggestions" })
                foreach (var statement in proposal.GetProperty(field).EnumerateArray())
                    if (statement.GetProperty("evidenceIds").EnumerateArray().Any(x => !unit.EvidenceIds.Contains(x.GetString()!)) ||
                        statement.GetProperty("ruleIds").EnumerateArray().Any(x => x.GetString() != unit.RuleId)) return new(AiIssue.OutputRejected, default);
            if (proposal.GetProperty("conflictingEvidenceIds").EnumerateArray().Any(x => !unit.EvidenceIds.Contains(x.GetString()!))) return new(AiIssue.OutputRejected, default);
            var occurrence = AiExecutionCanonical.Digest(new { schemaVersion = "synthetic-ai-occurrence-v1", locked.Scope, locked.RunId, unit.Key });
            var original = new AiFindingOriginal(occurrence, unit.Key, unit.ObjectType, unit.ModuleId, work.Category, unit.RuleId, unit.RuleVersion, unit.Title,
                unit.Severity, unit.Impact, unit.Likelihood, unit.RootCause, "AI", "Proposed", 80m, 1m, unit.ProposalId,
                AiExecutionCanonical.Serialize(proposal), unit.EvidenceIds, packet.Packet!.ContentDigest, accepted.Snapshot.ContentDigest, "", attempt);
            original = original with { OriginalDigest = AiExecutionCanonical.Digest(original) };
            outcomes.Add(new(unit.Key, CoverageState.Finding, null, "AI", original));
        }
        return new(null, outcomes.ToImmutableArray());
    }
}
