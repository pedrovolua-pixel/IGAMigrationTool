namespace ReportPublication.PostgreSql;

/// <summary>Original validity only. Neither database waits nor clock rollback creates a new window.</summary>
internal sealed class OriginalValidityLeaseV1 : IDisposable
{
    private readonly TimeProvider clock;
    private readonly DateTimeOffset admittedUtc;
    private readonly long admittedTimestamp;
    private readonly CancellationTokenSource expired;
    private readonly CancellationTokenSource invalidated;
    private readonly CancellationTokenSource linked;
    private readonly CancellationToken token;
    private readonly ITimer timer;
    private readonly object gate = new();
    private DateTimeOffset deadline;
    private bool disposed;

    internal OriginalValidityLeaseV1(TimeProvider clock, DateTimeOffset originalDeadlineUtc, CancellationToken caller)
    {
        if (originalDeadlineUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid publication validity.");
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        admittedUtc = clock.GetUtcNow(); admittedTimestamp = clock.GetTimestamp(); deadline = originalDeadlineUtc;
        if (deadline <= admittedUtc) throw new OperationCanceledException(caller);
        expired = new(); invalidated = new();
        linked = CancellationTokenSource.CreateLinkedTokenSource(caller, expired.Token, invalidated.Token);
        token = linked.Token;
        timer = clock.CreateTimer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        try { Schedule(); Check(); } catch { Dispose(); throw; }
    }
    internal DateTimeOffset DeadlineUtc { get { lock (gate) return deadline; } }
    internal CancellationToken Token => token;
    internal void Narrow(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid publication validity.");
        lock (gate) { if (disposed) throw new OperationCanceledException(); if (value < deadline) deadline = value; }
        Schedule(); Check();
    }
    internal void Check()
    {
        lock (gate)
        {
            if (disposed) throw new OperationCanceledException(Token);
            if (Remaining() <= TimeSpan.Zero) { CancelExpiry(); throw new OperationCanceledException(Token); }
        }
        Token.ThrowIfCancellationRequested();
    }
    internal async ValueTask<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken caller)
    {
        Check();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(Token, caller);
        var pending = action(operation.Token).AsTask();
        try { var value = await pending.WaitAsync(operation.Token); Check(); caller.ThrowIfCancellationRequested(); return value; }
        catch
        {
            // Observe late completion without making its result available after invalidation.
            if (!pending.IsCompleted) _ = pending.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }
    private TimeSpan Remaining()
    {
        var byUtc = deadline - clock.GetUtcNow();
        var byElapsed = deadline - admittedUtc - clock.GetElapsedTime(admittedTimestamp, clock.GetTimestamp());
        return byUtc < byElapsed ? byUtc : byElapsed;
    }
    private void Schedule()
    {
        lock (gate)
        {
            if (disposed) return;
            var remaining = Remaining();
            if (remaining <= TimeSpan.Zero) { CancelExpiry(); return; }
            // System timers have a finite due-time range; resampling never resets the admission origin.
            var maximumTimerDue = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
            timer.Change(remaining < maximumTimerDue ? remaining : maximumTimerDue, Timeout.InfiniteTimeSpan);
        }
    }
    private void Tick() { Schedule(); }
    private void CancelExpiry() { if (!expired.IsCancellationRequested) expired.Cancel(); }
    internal void Invalidate() { if (!invalidated.IsCancellationRequested) invalidated.Cancel(); }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; }
        Invalidate(); timer.Dispose(); linked.Dispose(); invalidated.Dispose(); expired.Dispose();
    }
}
