using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using SyntheticAiPreview;
using SyntheticAiValidation;

internal sealed record DemoAiPreviewDetail(string SchemaVersion, Guid RunId, long RunRevision,
    string RunInputDigest, string BaselineId, string ProfileId, string FixtureDigest,
    string Status, string? ReasonCode, SyntheticAiPreviewSnapshot? Snapshot);

/// <summary>Pure read of one exact frozen fictional source. No persistence, provider, resolver or actions.</summary>
internal static class DemoAiPreviewProjection
{
    internal static DemoAiPreviewDetail? Detail(SyntheticRunSnapshot run)
    {
        if (!DemoAiPreviewCatalog.IsProfile(run.ProfileCatalogId)) return null;
        DemoAiPreviewDetail Result(string? reason, SyntheticAiPreviewSnapshot? snapshot = null) => new(
            "synthetic-ai-demo-preview-v1", run.RunId, run.Revision, run.InputDigest,
            run.BaselineCatalogId, run.ProfileCatalogId, DemoAiPreviewCatalog.FixtureDigest(run.ProfileCatalogId),
            snapshot is null ? "Unavailable" : "Ready", reason, snapshot);
        if (!DemoAiPreviewCatalog.MatchesFrozenFixture(run) || !Complete(run))
            return Result("ai_preview_source_unavailable");
        var packetInput = JsonNode.Parse(DemoAiPreviewCatalog.PacketTemplate)!.AsObject();
        var source = packetInput["source"]!.AsObject();
        source["runId"] = run.RunId.ToString("D");
        source["baselineDigest"] = DemoAiPreviewCatalog.PacketTemplateDigest;
        source["profileDigest"] = run.InputDigest;
        var packet = SyntheticAiPacketBuilder.Build(packetInput.ToJsonString());
        if (!packet.Succeeded) return Result("ai_preview_packet_denied");
        var output = JsonNode.Parse(DemoAiPreviewCatalog.ResponseTemplate(run.ProfileCatalogId))!.AsObject();
        output["runId"] = run.RunId.ToString("D");
        output["packetDigest"] = packet.Packet!.ContentDigest;
        var proposals = SyntheticAiProposalValidator.Validate(packet.Packet, output.ToJsonString());
        if (!proposals.Succeeded) return Result("ai_preview_proposal_denied");
        var preview = SyntheticAiPreviewBuilder.Build(packet.Packet, proposals.Snapshot);
        return preview.Succeeded ? Result(null, preview.Snapshot) : Result("ai_preview_projection_denied");
    }

    private static bool Complete(SyntheticRunSnapshot run)
    {
        if (run.State != SyntheticRunState.Scoring || run.CancelRequested || run.Lease is not null ||
            run.CheckpointSequence < 1 || run.InFlightKeys is null || run.InFlightKeys.Count != 0 ||
            run.Results is null || run.CoverageSummary is null) return false;
        var expectedResults = DemoAiPreviewCatalog.CreateBaseline().ScriptedResults;
        if (run.Results.Count != expectedResults.Count || run.Results.Any(item => item?.Key is null)) return false;
        if (!run.Results.OrderBy(item => item.Key.InventoryId, StringComparer.Ordinal).SequenceEqual(expectedResults)) return false;
        var completion = CoverageCompletionProjector.Project(run.Plan.ExpectedKeys, run.Results);
        if (!completion.HasProjection) return false;
        var expectedSummary = new SyntheticCoverageStageSummary(completion.Kind!.Value,
            CoverageCountProjector.Project(run.Plan.ExpectedKeys, run.Results).Counts!,
            ExecutableCoverageProjector.Project(run.Plan.ExpectedKeys, run.Results).Measure!,
            CoverageLimitationProjector.Project(run.Plan.ExpectedKeys, run.Results).Limitations!);
        return JsonSerializer.Serialize(run.CoverageSummary) == JsonSerializer.Serialize(expectedSummary);
    }
}
