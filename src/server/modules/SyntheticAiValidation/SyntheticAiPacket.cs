using System.Collections.Immutable;

namespace SyntheticAiValidation;

// Internal fictional value only: no actor grant, source resolver or live provider eligibility.
public sealed class SyntheticAiPacket
{
    internal SyntheticAiPacket(string canonicalJson, string contentDigest, Guid runId,
        ImmutableArray<string> evidenceIds, ImmutableArray<string> ruleIds)
    {
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
        RunId = runId;
        EvidenceIds = evidenceIds;
        RuleIds = ruleIds;
    }

    public string CanonicalJson { get; }
    public string ContentDigest { get; }
    public Guid RunId { get; }
    public ImmutableArray<string> EvidenceIds { get; }
    public ImmutableArray<string> RuleIds { get; }
}
