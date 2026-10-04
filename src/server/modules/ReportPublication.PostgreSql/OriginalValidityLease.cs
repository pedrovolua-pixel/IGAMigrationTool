namespace ReportPublication.PostgreSql;

/// <summary>Original validity only. Neither database waits nor clock rollback creates a new window.</summary>
internal sealed class OriginalValidityLeaseV1 : IDisposable
{
    private readonly TimeProvider clock;
    private readonly DateTimeOffset admittedUtc;
    private readonly long admittedTimestamp;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken token;
    private readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration callerRegistration;
    private readonly ITimer timer;
    private readonly object gate = new();
    private DateTimeOffset deadline;
    private bool disposed;

    internal OriginalValidityLeaseV1(TimeProvider clock, DateTimeOffset originalDeadlineUtc, CancellationToken caller)
    {
        if (originalDeadlineUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid publication validity.");
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        caller.ThrowIfCancellationRequested();
        admittedUtc = clock.GetUtcNow(); admittedTimestamp = clock.GetTimestamp(); deadline = originalDeadlineUtc;
        if (deadline <= admittedUtc) throw new OperationCanceledException(caller);
        lifetime = new(); token = lifetime.Token;
        try { timer = clock.CreateTimer(_ => Schedule(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
        catch { lifetime.Dispose(); throw; }
        callerRegistration = caller.UnsafeRegister(static state => ((OriginalValidityLeaseV1)state!).Invalidate(), this);
        try { Schedule(); Check(); } catch { Dispose(); throw; }
    }
    internal DateTimeOffset DeadlineUtc { get { lock (gate) return deadline; } }
    internal CancellationToken Token => token;
    internal void Narrow(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid publication validity.");
        lock (gate) { if (disposed) throw new OperationCanceledException(token); if (value < deadline) deadline = value; }
        Schedule(); Check();
    }
    internal void Check()
    {
        bool mustEnd;
        lock (gate) mustEnd = disposed || Remaining() <= TimeSpan.Zero;
        if (mustEnd) { End(); throw new OperationCanceledException(token); }
        token.ThrowIfCancellationRequested();
    }
    internal async ValueTask<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken caller)
    {
        Check(); caller.ThrowIfCancellationRequested();
        using var operation = new CancellationTokenSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var originalRegistration = token.UnsafeRegister(_ => CancelOperation(), null);
        using var nestedRegistration = caller.UnsafeRegister(_ => CancelOperation(), null);
        operation.Token.ThrowIfCancellationRequested();
        var pending = action(operation.Token).AsTask();
        try
        {
            // This signal is independent of dependency callback dispatch: a stalled callback cannot delay expiry.
            var winner = await Task.WhenAny(pending, ended.Task, cancelled.Task);
            if (winner != pending) { CancelWithoutCallbackWait(operation); throw new OperationCanceledException(operation.Token); }
            var value = await pending;
            Check(); caller.ThrowIfCancellationRequested(); operation.Token.ThrowIfCancellationRequested();
            return value;
        }
        catch
        {
            ObserveLateFault(pending);
            throw;
        }
        void CancelOperation() { cancelled.TrySetResult(); CancelWithoutCallbackWait(operation); }
    }
    private TimeSpan Remaining()
    {
        var byUtc = deadline - clock.GetUtcNow();
        var byElapsed = deadline - admittedUtc - clock.GetElapsedTime(admittedTimestamp, clock.GetTimestamp());
        return byUtc < byElapsed ? byUtc : byElapsed;
    }
    private void Schedule()
    {
        bool mustEnd;
        lock (gate)
        {
            if (disposed) return;
            var remaining = Remaining();
            mustEnd = remaining <= TimeSpan.Zero;
            if (!mustEnd)
            {
                // System timers have a finite due-time range; resampling never resets the admission origin.
                var maximumTimerDue = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
                timer.Change(remaining < maximumTimerDue ? remaining : maximumTimerDue, Timeout.InfiniteTimeSpan);
            }
        }
        if (mustEnd) End();
    }
    private void End() { ended.TrySetResult(); CancelWithoutCallbackWait(lifetime); }
    private static void CancelWithoutCallbackWait(CancellationTokenSource source)
    {
        // CancelAsync marks the token synchronously and dispatches callbacks outside the caller/state lock.
        // Callback faults are observed; they cannot escape a timer or skip owned cleanup.
        try { ObserveLateFault(source.CancelAsync()); } catch (ObjectDisposedException) { }
    }
    private static void ObserveLateFault(Task task) => _ = task.ContinueWith(t => _ = t.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    internal void Invalidate() => End();
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; }
        End();
        try { timer.Dispose(); }
        finally { try { callerRegistration.Dispose(); } finally { lifetime.Dispose(); } }
    }
}
