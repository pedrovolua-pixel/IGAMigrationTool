using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using FindingReview;

internal static class ReviewFixtures
{
    internal static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static SyntheticReviewRunSeed Run(Guid? id = null)
    {
        return new(SyntheticReviewScope.Fixed, id ?? Guid.NewGuid(), Digest("independent-run-input-v1"), Digest("independent-analysis-v1"),
            SyntheticReviewResourceState.Mutable,
            [Finding("independent-critical", "SECURITY", SyntheticFindingState.Proposed),
             Finding("independent-high", "SECURITY", SyntheticFindingState.Proposed),
             Finding("independent-medium", "OPERATIONS", SyntheticFindingState.AutoConfirmed)]);
    }
    private static SyntheticFindingSeed Finding(string label, string category, SyntheticFindingState state)
    {
        var occurrences = Enumerable.Range(1, 2).Select(index => new SyntheticOccurrenceReference(Digest($"{label}-occurrence-{index}"),
            $"independent-object-{label}-{index}", $"independent-rule-{label}", "independent-rule-v1", Digest($"{label}-original-{index}"))).ToImmutableArray();
        return new(Digest(label), category, state, $"Original {label} title", occurrences.Select(item => item.OriginalDigest).ToImmutableArray(), occurrences);
    }
}
