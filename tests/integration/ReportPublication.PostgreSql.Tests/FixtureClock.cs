// Trusted fixture validity owner; PostgreSQL clock is advanced separately by the controlled helper.
internal sealed class FixtureClock(DateTimeOffset initial) : TimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset utc = initial;
    private long timestamp;
    private readonly List<Timer> timers = [];
    internal int TimerDisposals { get; private set; }
    public override DateTimeOffset GetUtcNow() { lock (gate) return utc; }
    public override long GetTimestamp() { lock (gate) return timestamp; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate) { var timer = new Timer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer; }
    }
    internal void Advance(TimeSpan elapsed, TimeSpan? utcElapsed = null)
    {
        if (elapsed < TimeSpan.Zero) throw new InvalidOperationException("Monotonic fixture time cannot roll back.");
        List<Action> due;
        lock (gate)
        {
            timestamp = checked(timestamp + elapsed.Ticks); utc += utcElapsed ?? elapsed;
            due = timers.Select(x => x.Due(timestamp)).OfType<Action>().ToList();
        }
        foreach (var callback in due) callback();
    }
    private sealed class Timer(FixtureClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long due = long.MaxValue;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan) throw new InvalidOperationException("Fixture lifetime uses one-shot timers only.");
            lock (clock.gate)
            { if (disposed) return false; due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock.timestamp + dueTime.Ticks); return true; }
        }
        internal Action? Due(long now)
        { if (disposed || now < due) return null; due = long.MaxValue; return () => callback(state); }
        public void Dispose() { lock (clock.gate) { if (disposed) return; disposed = true; due = long.MaxValue; clock.TimerDisposals++; } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
