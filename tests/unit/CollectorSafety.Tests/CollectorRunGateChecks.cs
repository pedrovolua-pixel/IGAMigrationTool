using CollectorSafety;

internal static class CollectorRunGateChecks
{
    internal static async Task<int> RunAsync()
    {
        var gate = new CollectorRunGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = gate.RunOnceAsync("SCOPE-1", async _ =>
        {
            entered.SetResult();
            await release.Task;
        }, CancellationToken.None);
        await entered.Task;

        var overlapInvoked = false;
        var overlap = await gate.RunOnceAsync("SCOPE-1", _ =>
        {
            overlapInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);
        Require(overlap == CollectorRunGateDecision.SkippedOverlap && !overlapInvoked,
            "same-scope overlap is skipped");

        var distinct = await gate.RunOnceAsync("SCOPE-2", _ => Task.CompletedTask,
            CancellationToken.None);
        Require(distinct == CollectorRunGateDecision.Completed,
            "different scope can run");

        release.SetResult();
        Require(await first == CollectorRunGateDecision.Completed,
            "first run completes");

        var afterCompletion = await gate.RunOnceAsync("SCOPE-1", _ => Task.CompletedTask,
            CancellationToken.None);
        Require(afterCompletion == CollectorRunGateDecision.Completed,
            "scope releases after completion");

        try
        {
            await gate.RunOnceAsync("SCOPE-1", _ => throw new InvalidOperationException("synthetic failure"),
                CancellationToken.None);
            throw new Exception("Expected synthetic failure.");
        }
        catch (InvalidOperationException)
        {
            // The gate must release its scope while propagating the failure.
        }

        var afterFailure = await gate.RunOnceAsync("SCOPE-1", _ => Task.CompletedTask,
            CancellationToken.None);
        Require(afterFailure == CollectorRunGateDecision.Completed,
            "scope releases after failure");

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var invoked = false;
        try
        {
            await gate.RunOnceAsync("SCOPE-1", _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }, canceled.Token);
            throw new Exception("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
            Require(!invoked, "canceled run is not invoked");
        }

        var afterCancellation = await gate.RunOnceAsync("SCOPE-1", _ => Task.CompletedTask,
            CancellationToken.None);
        Require(afterCancellation == CollectorRunGateDecision.Completed,
            "scope releases after cancellation");

        var invalidInvoked = false;
        var invalid = await gate.RunOnceAsync(" ", _ =>
        {
            invalidInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);
        Require(invalid == CollectorRunGateDecision.InvalidScope && !invalidInvoked,
            "blank scope is rejected");

        return 8;
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new Exception($"Collector run gate case failed: {name}.");
        }
    }
}
