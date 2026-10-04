using System.Security.Cryptography;
using System.Text;
using CollectorSafety;

namespace CollectorSourcePages;

public static class SourceQueryPairPreflight
{
    public static SourcePageReason Evaluate(SourceQueryPair? pair, SourceRegistryBinding? binding)
    {
        if (pair is null || binding is null || string.IsNullOrWhiteSpace(pair.FirstSql) || pair.PairId == Guid.Empty || pair.LocalPackRevision <= 0 || pair.LocalPolicyRevision <= 0 ||
            !Label(pair.SchemaVersion) || !Label(pair.NormalizationVersion) || !Label(pair.RepeatabilityReviewReference)) return SourcePageReason.InvalidInput;
        if (pair.PairId != binding.PairId || pair.LocalPackRevision != binding.LocalPackRevision || pair.LocalPolicyRevision != binding.LocalPolicyRevision ||
            pair.ApprovedUidField != binding.UidField || pair.SchemaVersion != binding.SchemaVersion ||
            pair.NormalizationVersion != binding.NormalizationVersion || pair.RepeatabilityReviewReference != binding.RepeatabilityReviewReference ||
            !string.Equals(pair.FirstSqlSha256, binding.FirstSqlSha256, StringComparison.OrdinalIgnoreCase)) return SourcePageReason.RegistryMismatch;
        if (QueryPackPreflight.Evaluate(pair.Descriptor, binding.Expected).Decision != QueryPackPreflightDecision.StructurallyReady)
            return SourcePageReason.InvalidQueryPair;
        if (pair.ApprovedUidField is not null && !pair.Fields.Any(f => f.Name == pair.ApprovedUidField)) return SourcePageReason.InvalidQueryPair;
        string hash;
        try { hash = Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(pair.FirstSql))); }
        catch (EncoderFallbackException) { return SourcePageReason.InvalidQueryPair; }
        if (!string.Equals(hash, pair.FirstSqlSha256, StringComparison.OrdinalIgnoreCase)) return SourcePageReason.InvalidQueryPair;
        var descriptor = pair.Descriptor;
        var shape = new StrictKeysetSqlPolicy(descriptor.Schema, descriptor.Table, pair.Fields.Select(f => f.Name).ToArray(),
            descriptor.StableKey, descriptor.PageSizeParameter, descriptor.BoundaryParameter);
        return FirstPageSqlValidator.Evaluate(pair.FirstSql, shape) == StrictKeysetSqlDecision.StructurallyReady &&
            FirstPageSqlValidator.Check(descriptor.Sql, shape, first: false) == StrictKeysetSqlDecision.StructurallyReady
            ? SourcePageReason.None : SourcePageReason.InvalidQueryPair;
    }
    private static bool Label(string? value) => value is { Length: > 0 and <= 128 } && !value.Any(char.IsWhiteSpace) &&
        !value.Any(char.IsControl) && !value.Contains('*') && !value.Contains('?') && value.Split('.').All(p => p.Length != 0 && !string.Equals(p, "x", StringComparison.OrdinalIgnoreCase));
}
