namespace SyntheticAiValidation;

// Payload-free fixture denials; these codes do not convey live-provider eligibility.
public enum ProposalValidationIssue
{
    MissingPacket,
    InvalidJson,
    InvalidShape,
    UnsupportedVersion,
    PacketMismatch,
    InvalidProposal,
    InvalidCitation,
    InvalidConflict
}

public sealed class ProposalValidationResult
{
    private ProposalValidationResult(SyntheticAiProposalSnapshot? snapshot, ProposalValidationIssue? issue)
    {
        Snapshot = snapshot;
        Issue = issue;
    }

    public bool Succeeded => Snapshot is not null;
    public ProposalValidationIssue? Issue { get; }
    public SyntheticAiProposalSnapshot? Snapshot { get; }

    internal static ProposalValidationResult Accepted(SyntheticAiProposalSnapshot snapshot) => new(snapshot, null);
    internal static ProposalValidationResult Denied(ProposalValidationIssue issue) => new(null, issue);
}

// Detached immutable proposed data only. No review, persistence or execution authority.
public sealed class SyntheticAiProposalSnapshot
{
    internal SyntheticAiProposalSnapshot(string canonicalJson, string contentDigest, int proposalCount)
    {
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
        ProposalCount = proposalCount;
    }

    public string CanonicalJson { get; }
    public string ContentDigest { get; }
    public int ProposalCount { get; }
    public string Status => "Proposed";
}
