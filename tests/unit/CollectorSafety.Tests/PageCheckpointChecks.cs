using CollectorSafety;

internal static class PageCheckpointChecks
{
    internal static int Run()
    {
        var context = new PageCheckpointContext("QUERY-1", "1.0.0", "10.0.1-HF1",
            "SCOPE-1", "POLICY-1", "UID");
        var otherContext = context with { PolicyVersion = "POLICY-2" };
        var digest = new string('a', 64);
        var otherDigest = new string('b', 64);
        var page = new PageCheckpoint(context, "UID-100", digest, 1, false);
        var checks = 0;

        Check("new page", PageCheckpointDecision.NewPage, context, [], page);
        Check("same page replay", PageCheckpointDecision.IdempotentReplay, context, [page], page);
        Check("digest case does not create conflict", PageCheckpointDecision.IdempotentReplay,
            context, [page], page with { ContentSha256 = digest.ToUpperInvariant() });
        Check("changed page conflicts", PageCheckpointDecision.ContentConflict,
            context, [page], page with { ContentSha256 = otherDigest });
        Check("changed row count conflicts", PageCheckpointDecision.ContentConflict,
            context, [page], page with { RowCount = 2 });
        Check("changed terminal state conflicts", PageCheckpointDecision.ContentConflict,
            context, [page], page with { IsTerminal = true });
        Check("negative row count invalid", PageCheckpointDecision.InvalidInput,
            context, [], page with { RowCount = -1 });
        Check("terminal page blocks another boundary", PageCheckpointDecision.ContentConflict,
            context, [page with { IsTerminal = true }], page with { PageBoundary = "UID-200" });
        Check("new boundary", PageCheckpointDecision.NewPage, context, [page],
            page with { PageBoundary = "UID-200" });
        Check("changed policy context", PageCheckpointDecision.IncompatibleContext,
            context, [page], page with { Context = otherContext });
        Check("stored context drift", PageCheckpointDecision.CorruptCompletedPages,
            context, [page with { Context = otherContext }], page);
        Check("stored digest conflict", PageCheckpointDecision.CorruptCompletedPages,
            context, [page, page with { ContentSha256 = otherDigest }], page);
        Check("malformed digest", PageCheckpointDecision.InvalidInput, context, [],
            page with { ContentSha256 = "bad" });
        Check("missing boundary", PageCheckpointDecision.InvalidInput, context, [],
            page with { PageBoundary = "" });
        Check("missing expected context", PageCheckpointDecision.InvalidInput, null, [], page);
        Check("missing completed set", PageCheckpointDecision.InvalidInput, context, null, page);

        return checks;

        void Check(string name, PageCheckpointDecision expected,
            PageCheckpointContext? expectedContext,
            IReadOnlyCollection<PageCheckpoint>? completed,
            PageCheckpoint? candidate)
        {
            var actual = PageCheckpointVerifier.Evaluate(expectedContext, completed, candidate);
            if (actual != expected)
            {
                throw new Exception($"{name}: expected {expected}, got {actual}.");
            }

            checks++;
        }
    }
}
