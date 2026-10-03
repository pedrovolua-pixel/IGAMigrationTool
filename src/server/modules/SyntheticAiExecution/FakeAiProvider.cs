using System.Text.Json;
using SyntheticAiValidation;

namespace SyntheticAiExecution;

/// <summary>Fixed pure fixture only. No network, database, clock, environment, credentials or arbitrary callback.</summary>
public sealed class FakeAiProvider
{
    public AiProviderReceipt Dispatch(AiRecovery recovery)
    {
        if (recovery.BillingOnly || recovery.PacketInputJson is null) throw new ArgumentException("Fake dispatch requires eligible fixture work.");
        if (recovery.Scenario is AiScenario.LostResponse or AiScenario.PermanentUnknown) return Unknown(recovery.Attempt);
        return Known(recovery);
    }
    public AiProviderReceipt Lookup(AiRecovery recovery) => recovery.Scenario == AiScenario.PermanentUnknown ? Unknown(recovery.Attempt) : Known(recovery);
    public static string ReceiptIdentity(AiAttemptKey attempt, AiProviderOutcome outcome, int? input, int? output) =>
        AiExecutionCanonical.Digest(new { schemaVersion = "synthetic-ai-usage-receipt-v1", attempt, outcome, input, output });
    private static AiProviderReceipt Unknown(AiAttemptKey attempt) => new(ReceiptIdentity(attempt, AiProviderOutcome.Unknown, null, null), attempt, AiProviderOutcome.Unknown, null, null, null, null);
    private static AiProviderReceipt Known(AiRecovery recovery)
    {
        var retry = recovery.Scenario == AiScenario.RetryTwice && recovery.Attempt.Ordinal < 3;
        var outcome = retry ? AiProviderOutcome.RetryableFailure : AiProviderOutcome.Response;
        var input = 80; var outputUse = retry ? 0 : 160;
        if (recovery.BillingOnly || retry) return new(ReceiptIdentity(recovery.Attempt, outcome, input, outputUse), recovery.Attempt, outcome, input, outputUse, null, null);
        var packet = SyntheticAiPacketBuilder.Build(recovery.PacketInputJson);
        if (!packet.Succeeded || packet.Packet!.ContentDigest != recovery.Attempt.PacketDigest) throw new ArgumentException("Fake packet identity differs.");
        var output = recovery.Scenario == AiScenario.InvalidOutput ? "{\"tools\":[\"fetch\"]}" : AiExecutionCanonical.Serialize(new
        {
            schemaVersion = "synthetic-ai-fixture-output-v1",
            runId = packet.Packet.RunId.ToString("D"),
            packetDigest = packet.Packet.ContentDigest,
            proposals = recovery.Scenario == AiScenario.Empty ? [] : recovery.Units.Select(unit => new
            {
                proposalId = unit.ProposalId,
                facts = new[] { new { text = "Fictional configuration statement; model-labelled fact remains untrusted.", evidenceIds = unit.EvidenceIds, ruleIds = new[] { unit.RuleId } } },
                inferences = Array.Empty<object>(),
                assumptions = Array.Empty<object>(),
                missingContext = new[] { "No real provider or customer validation." },
                suggestions = new[] { new { text = "Inspect fictional controls; <script>throw 1</script> is inert data.", evidenceIds = unit.EvidenceIds, ruleIds = new[] { unit.RuleId } } },
                uncertainty = "Fictional provider only.",
                conflictingEvidenceIds = Array.Empty<string>()
            }).ToArray()
        });
        if (recovery.Scenario == AiScenario.InvalidCitation) output = output.Replace("ev-", "foreign-", StringComparison.Ordinal);
        return new(ReceiptIdentity(recovery.Attempt, outcome, input, outputUse), recovery.Attempt, outcome, input, outputUse, output, AiExecutionCanonical.Hash(output));
    }
}
