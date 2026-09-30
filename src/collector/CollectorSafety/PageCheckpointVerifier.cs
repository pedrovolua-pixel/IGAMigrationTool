namespace CollectorSafety;

public sealed record PageCheckpointContext(
    string QueryId,
    string QueryVersion,
    string ExactBuild,
    string ScopeId,
    string PolicyVersion,
    string OrderingKey);

public sealed record PageCheckpoint(
    PageCheckpointContext Context,
    string PageBoundary,
    string ContentSha256);

public enum PageCheckpointDecision
{
    NewPage,
    IdempotentReplay,
    ContentConflict,
    IncompatibleContext,
    InvalidInput,
    CorruptCompletedPages
}

/// <summary>
/// Pure checkpoint admission for already completed immutable pages. The caller
/// must read/write the ledger atomically; this type performs no persistence.
/// </summary>
public static class PageCheckpointVerifier
{
    public static PageCheckpointDecision Evaluate(
        PageCheckpointContext? expectedContext,
        IReadOnlyCollection<PageCheckpoint>? completedPages,
        PageCheckpoint? candidate)
    {
        if (!ValidContext(expectedContext) || completedPages is null || !ValidCheckpoint(candidate))
        {
            return PageCheckpointDecision.InvalidInput;
        }

        if (candidate!.Context != expectedContext)
        {
            return PageCheckpointDecision.IncompatibleContext;
        }

        string? completedDigest = null;
        foreach (var completed in completedPages)
        {
            if (!ValidCheckpoint(completed) || completed.Context != expectedContext)
            {
                return PageCheckpointDecision.CorruptCompletedPages;
            }

            if (completed.PageBoundary != candidate.PageBoundary)
            {
                continue;
            }

            if (completedDigest is not null &&
                !string.Equals(completedDigest, completed.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                return PageCheckpointDecision.CorruptCompletedPages;
            }

            completedDigest = completed.ContentSha256;
        }

        if (completedDigest is null)
        {
            return PageCheckpointDecision.NewPage;
        }

        return string.Equals(completedDigest, candidate.ContentSha256, StringComparison.OrdinalIgnoreCase)
            ? PageCheckpointDecision.IdempotentReplay
            : PageCheckpointDecision.ContentConflict;
    }

    private static bool ValidCheckpoint(PageCheckpoint? checkpoint) =>
        checkpoint is not null && ValidContext(checkpoint.Context) &&
        !string.IsNullOrWhiteSpace(checkpoint.PageBoundary) &&
        checkpoint.ContentSha256 is { Length: 64 } digest &&
        digest.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private static bool ValidContext(PageCheckpointContext? context) =>
        context is not null &&
        !string.IsNullOrWhiteSpace(context.QueryId) &&
        !string.IsNullOrWhiteSpace(context.QueryVersion) &&
        !string.IsNullOrWhiteSpace(context.ExactBuild) &&
        !string.IsNullOrWhiteSpace(context.ScopeId) &&
        !string.IsNullOrWhiteSpace(context.PolicyVersion) &&
        !string.IsNullOrWhiteSpace(context.OrderingKey);
}
