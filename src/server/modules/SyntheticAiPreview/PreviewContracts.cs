using System.Collections.Immutable;

namespace SyntheticAiPreview;

public enum PreviewIssue { MissingPacket, MissingProposal, InvalidInput, IntegrityMismatch, SourceMismatch }
public enum PreviewRenderIssue { MissingSnapshot, InvalidSnapshot, OutputTooLarge }

public sealed record PreviewSource(string CustomerId, string ProjectId, string EnvironmentId, Guid RunId,
    string BaselineDigest, string ProfileDigest, string NormalizationVersion, string RedactionVersion, string PromptVersion);
public sealed record PreviewStatement(string Text, ImmutableArray<string> EvidenceIds, ImmutableArray<string> RuleIds);
public sealed record PreviewProposal(string ProposalId, ImmutableArray<PreviewStatement> Facts,
    ImmutableArray<PreviewStatement> Inferences, ImmutableArray<PreviewStatement> Assumptions,
    ImmutableArray<string> MissingContext, ImmutableArray<PreviewStatement> Suggestions,
    string Uncertainty, ImmutableArray<string> ConflictingEvidenceIds);

public sealed class SyntheticAiPreviewSnapshot
{
    public const string FixedDisclaimer = "Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.";

    internal SyntheticAiPreviewSnapshot(string canonicalJson, string contentDigest, PreviewSource source,
        string packetDigest, string proposalDigest, ImmutableArray<PreviewProposal> proposals)
    {
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
        Source = source;
        PacketDigest = packetDigest;
        ProposalDigest = proposalDigest;
        Proposals = proposals;
    }

    public string SchemaVersion => "synthetic-ai-preview-v1";
    public string Status => "Proposed";
    public string Disclaimer => FixedDisclaimer;
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
    public PreviewSource Source { get; }
    public string PacketDigest { get; }
    public string ProposalDigest { get; }
    public ImmutableArray<PreviewProposal> Proposals { get; }
}

public sealed class PreviewResult
{
    private PreviewResult(SyntheticAiPreviewSnapshot? snapshot, PreviewIssue? issue) { Snapshot = snapshot; Issue = issue; }
    public bool Succeeded => Snapshot is not null;
    public SyntheticAiPreviewSnapshot? Snapshot { get; }
    public PreviewIssue? Issue { get; }
    internal static PreviewResult Accepted(SyntheticAiPreviewSnapshot snapshot) => new(snapshot, null);
    internal static PreviewResult Denied(PreviewIssue issue) => new(null, issue);
}

public sealed class PreviewHtmlSnapshot
{
    internal PreviewHtmlSnapshot(string html, string contentDigest, string previewDigest)
    { Html = html; ContentDigest = contentDigest; PreviewDigest = previewDigest; }
    public string Html { get; }
    public string ContentDigest { get; }
    public string PreviewDigest { get; }
}

public sealed class PreviewRenderResult
{
    private PreviewRenderResult(PreviewHtmlSnapshot? snapshot, PreviewRenderIssue? issue) { Snapshot = snapshot; Issue = issue; }
    public bool Succeeded => Snapshot is not null;
    public PreviewHtmlSnapshot? Snapshot { get; }
    public PreviewRenderIssue? Issue { get; }
    internal static PreviewRenderResult Accepted(PreviewHtmlSnapshot snapshot) => new(snapshot, null);
    internal static PreviewRenderResult Denied(PreviewRenderIssue issue) => new(null, issue);
}
