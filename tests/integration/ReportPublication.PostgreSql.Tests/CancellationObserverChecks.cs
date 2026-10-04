using ReportPublication.PostgreSql;

internal static class CancellationObserverChecks
{
    internal static async Task<IReadOnlyList<AdapterPortableChecks.Result>> RunAsync()
    {
        var results = new List<AdapterPortableChecks.Result>();
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); results.Add(new(name, true, null)); }
            catch (Exception ex) { results.Add(new(name, false, ex.GetType().Name)); }
        }
        var origin = new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero);
        foreach (var originalCaller in new[] { true, false })
            await Check("pending-stalled-dispatch:" + (originalCaller ? "original" : "per-dependency"), async () =>
            {
                var clock = new FixtureClock(origin); using var caller = new CancellationTokenSource();
                using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromMinutes(2), originalCaller ? caller.Token : default);
                var ignored = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = lease.InvokeAsync(_ => new ValueTask<int>(ignored.Task), originalCaller ? default : caller.Token).AsTask();
                using var release = new ManualResetEventSlim();
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var blocker = caller.Token.Register(() => { started.TrySetResult(); release.Wait(TimeSpan.FromSeconds(3)); });
                var cancellation = caller.CancelAsync();
                try
                {
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(2)); Require(caller.IsCancellationRequested);
                    await Canceled(async () => _ = await pending.WaitAsync(TimeSpan.FromMilliseconds(500)));
                }
                finally
                {
                    release.Set(); await cancellation.WaitAsync(TimeSpan.FromSeconds(2));
                    await Canceled(async () => _ = await pending.WaitAsync(TimeSpan.FromSeconds(2))); ignored.TrySetResult(7);
                }
            });
        await Check("disposed-original-source-no-admission", async () =>
        {
            var caller = new CancellationTokenSource(); var token = caller.Token; caller.Dispose(); var calls = 0;
            try
            {
                using var lease = new OriginalValidityLeaseV1(new FixtureClock(origin), origin + TimeSpan.FromMinutes(2), token);
                _ = await lease.InvokeAsync(_ => { calls++; return ValueTask.FromResult(7); }, default);
            }
            catch (ObjectDisposedException) { Require(calls == 0); return; }
            catch (OperationCanceledException) { Require(calls == 0); return; }
            throw new InvalidOperationException("Disposed caller admitted protected work.");
        });
        await Check("disposed-per-dependency-source-no-start", async () =>
        {
            var caller = new CancellationTokenSource(); var token = caller.Token; caller.Dispose(); var calls = 0;
            using var lease = new OriginalValidityLeaseV1(new FixtureClock(origin), origin + TimeSpan.FromMinutes(2), default);
            try { _ = await lease.InvokeAsync(_ => { calls++; return ValueTask.FromResult(7); }, token); }
            catch (ObjectDisposedException) { Require(calls == 0); return; }
            catch (OperationCanceledException) { Require(calls == 0); return; }
            throw new InvalidOperationException("Disposed dependency caller admitted protected work.");
        });
        await Check("caller-handle-owned-after-dispose", async () =>
        {
            var clock = new FixtureClock(origin); using var caller = new CancellationTokenSource(); var handle = caller.Token.WaitHandle;
            var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromMinutes(2), caller.Token);
            _ = await lease.InvokeAsync(_ => ValueTask.FromResult(7), caller.Token);
            lease.Dispose(); lease.Dispose(); Require(!handle.WaitOne(0) && clock.TimerDisposals == 1);
            caller.Cancel(); Require(handle.WaitOne(0));
            await Canceled(async () => _ = await lease.InvokeAsync(_ => ValueTask.FromResult(7), default));
        });
        await Check("noncancelable-current-invoke", async () =>
        {
            using var lease = new OriginalValidityLeaseV1(new FixtureClock(origin), origin + TimeSpan.FromMinutes(2), default);
            Require(await lease.InvokeAsync(_ => ValueTask.FromResult(7), default) == 7);
        });
        await Check("dispose-pending-before-raced-caller", async () =>
        {
            var clock = new FixtureClock(origin); using var caller = new CancellationTokenSource();
            var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromMinutes(2), caller.Token);
            var ignored = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = lease.InvokeAsync(_ => new ValueTask<int>(ignored.Task), caller.Token).AsTask();
            lease.Dispose(); lease.Dispose(); await Canceled(async () => _ = await pending.WaitAsync(TimeSpan.FromSeconds(2)));
            await caller.CancelAsync(); Require(clock.TimerDisposals == 1 && lease.Token.IsCancellationRequested);
            var calls = 0; await Canceled(async () => _ = await lease.InvokeAsync(_ => { calls++; return ValueTask.FromResult(9); }, default));
            Require(calls == 0); ignored.TrySetResult(7);
        });
        foreach (var r in results.Where(x => !x.Passed)) Console.WriteLine("FAIL: " + r.Name + " (" + r.FailureType + ")");
        Console.WriteLine($"Portable cancellation observers: {results.Count(x => x.Passed)} PASS / {results.Count(x => !x.Passed)} FAIL; persisted mechanisms NOT EXECUTED.");
        return results;
    }
    private static void Require(bool value) { if (!value) throw new InvalidOperationException("Independent cancellation observer assertion failed."); }
    private static async Task Canceled(Func<Task> action)
    {
        try { await action(); } catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Canceled pending result escaped.");
    }
}
