using System.Text.Json.Nodes;
using AssessmentRuns;
using SyntheticAiPreview;
using SyntheticAiValidation;

internal static class CompositionCases
{
    internal static void Run()
    {
        foreach (var profile in new[] { Expected.Normal, Expected.Empty })
        {
            // The settled template's literal placeholder source is valid synthetic input.
            var packet = SyntheticAiPacketBuilder.Build(DemoAiPreviewCatalog.PacketTemplate);
            Check.That(packet.Succeeded && packet.Packet is not null, "portable-actual-packet-builder");
            var response = JsonNode.Parse(DemoAiPreviewCatalog.ResponseTemplate(profile))!.AsObject();
            response["packetDigest"] = packet.Packet!.ContentDigest;
            var proposal = SyntheticAiProposalValidator.Validate(packet.Packet, response.ToJsonString());
            Check.That(proposal.Succeeded && proposal.Snapshot is not null, "portable-actual-fixed-response-validator");
            var preview = SyntheticAiPreviewBuilder.Build(packet.Packet, proposal.Snapshot);
            Check.That(preview.Succeeded && preview.Snapshot is not null, "portable-actual-preview-builder");
            var oracle = Expected.ForSource("11111111-2222-3333-4444-555555555555", new string('b', 64), new string('a', 64), profile == Expected.Empty);
            Check.Equal(packet.Packet.CanonicalJson, oracle.Packet, "portable-full-independent-packet-bytes");
            Check.Equal(proposal.Snapshot!.CanonicalJson, oracle.Proposal, "portable-full-independent-proposal-bytes");
            Check.Equal(preview.Snapshot!.CanonicalJson, oracle.Preview, "portable-full-independent-preview-bytes");
            Check.Equal(preview.Snapshot.ContentDigest, Expected.Hash(oracle.Preview), "portable-full-independent-preview-digest");
            response["runId"] = "22222222-3333-4444-5555-666666666666";
            Check.That(!SyntheticAiProposalValidator.Validate(packet.Packet, response.ToJsonString()).Succeeded, "portable-foreign-fixed-response-run-denied");
            response["runId"] = "11111111-2222-3333-4444-555555555555";
            response["packetDigest"] = new string('0', 64);
            Check.That(!SyntheticAiProposalValidator.Validate(packet.Packet, response.ToJsonString()).Succeeded, "portable-foreign-fixed-response-packet-denied");
        }
        Check.Group("V10-002 portable real builder/validator/preview full independent oracles and source denial");
    }
}
