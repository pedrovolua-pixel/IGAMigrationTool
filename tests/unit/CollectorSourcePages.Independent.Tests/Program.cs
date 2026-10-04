using CollectorSafety;
using CollectorSourcePages;

var failures = new List<string>(); var passed = 0;
void Require(bool v) { if (!v) throw new InvalidOperationException("Independent assertion failed."); }
void NoPage(SourcePageResult r) => Require(r.Page is null && r.Receipt is null && r.ToString() == "SourcePageResult");
async Task Check(string name, Func<Task> action)
{ try { await action().WaitAsync(TimeSpan.FromSeconds(5)); passed++; } catch (Exception e) { failures.Add(name + ": " + e.GetType().Name + " " + e.Message); } }
await Check("first-minimum-zero-native-keys-no-sentinel-no-lookahead", async () =>
{
    var f = new IndependentPorts(); f.SetRows(int.MinValue, 0); var r = await f.Kernel().RunPageAsync(f.Request(), default);
    Require(r.Outcome == SourcePageOutcome.PageReady && !r.Page!.Terminal && r.Page.Rows[0].Fields[0].Value!.IntegerValue == int.MinValue && r.Page.NextContinuation!.IntegerValue == 0);
    Require(r.Counters.ReadCalls == 2 && f.Executed!.Boundary is null && f.Executed.PageSize.Value.IntegerValue == 2);
});
foreach (var count in new[] { 0, 1 }) await Check("short-empty-terminal-" + count, async () =>
{ var f = new IndependentPorts(); f.SetRows(Enumerable.Range(0, count).Select(i => (long)i).ToArray()); var r = await f.Kernel().RunPageAsync(f.Request(), default); Require(r.Page!.Terminal && r.Page.NextContinuation is null && r.Counters.ReadCalls == count + 1); });
await Check("full-then-explicit-empty-terminal-continuation", async () =>
{
    var f = new IndependentPorts(); f.SetRows(-2, 0); var kernel = f.Kernel(); var first = await kernel.RunPageAsync(f.Request(), default); f.Prior = first.Receipt;
    f.SetRows(); var second = await kernel.RunPageAsync(f.Request(), default); Require(second.Page!.Terminal && second.Page.Rows.Count == 0 && second.Receipt!.CumulativeRows == 2 && f.Executed!.Boundary!.Value.IntegerValue == 0 && f.ActiveGenerations.Count == 0);
});
await Check("rowcap-partial-cannot-resume", async () =>
{
    var f = new IndependentPorts(); f.Limits = new(2, 2, 64, 256, 1024, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2)); f.SetRows(0, 1);
    var kernel = f.Kernel(); var first = await kernel.RunPageAsync(f.Request(), default); Require(first.Outcome == SourcePageOutcome.Partial && first.Reason == SourcePageReason.RowCap && first.Page is not null && first.Page.NextContinuation is null);
    f.Prior = first.Receipt; var r = await kernel.RunPageAsync(f.Request(boundary: SourceNativeValue.Integer("int", 1)), default); NoPage(r); Require(r.Counters.Executions == 0);
});
foreach (var sql in new[] { "SELECT TOP (@take) K, U FROM dbo.FictionalSource WHERE K > @after ORDER BY K ASC", "SELECT K, U FROM dbo.FictionalSource ORDER BY K ASC", "SELECT TOP (@take) U, K FROM dbo.FictionalSource ORDER BY K ASC", "SELECT DISTINCT TOP (@take) K, U FROM dbo.FictionalSource ORDER BY K ASC", "SELECT TOP (@take) K AS Other, U FROM dbo.FictionalSource ORDER BY K ASC" })
    await Check("first-family-negative-" + passed, async () =>
    { var f = new IndependentPorts(); f.Pair = f.MakePair(first: sql); f.Binding = f.MakeBinding(); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Counters.Executions == 0 && r.Counters.ConnectionOpens == 0); });
await Check("continuation-projection-order-refused-before-open", async () =>
{
    var f = new IndependentPorts(); var sql = f.Descriptor.Sql.Replace("K, U", "U, K", StringComparison.Ordinal); f.Descriptor = f.Descriptor with { Sql = sql, SqlSha256 = IndependentPorts.Hash(sql) }; f.Expected = f.Expected with { SqlSha256 = f.Descriptor.SqlSha256 }; f.Pair = f.MakePair(); f.Binding = f.MakeBinding(); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Counters.ConnectionOpens == 0);
});
foreach (var field in new[] { FieldClassification.Redacted, FieldClassification.Prohibited, FieldClassification.Unknown })
    await Check("projected-declaration-no-value-gap-" + field, async () =>
    { var f = new IndependentPorts(); f.Descriptor = f.Descriptor with { Fields = [f.Descriptor.Fields.First(), f.Descriptor.Fields.Last() with { Classification = field }] }; f.Pair = f.MakePair(); f.Binding = f.MakeBinding(); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Counters.ConnectionOpens == 0 && r.PlannedGaps.Count > 0); });
foreach (var kind in new[] { "authority", "blocked", "permission", "warning", "impact" })
    await Check("generation-gate-before-evidence-" + kind, async () =>
    { var f = new IndependentPorts { Denied = kind == "authority", Blocked = kind == "blocked", WrongPermission = kind == "permission", Excess = kind == "warning", WrongWarning = kind == "warning", Impact = kind == "impact" ? SourceImpactState.Unknown : SourceImpactState.Continue }; var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Counters.Executions == 0 && f.Executed is null && f.ActiveGenerations.Count == 0); });
await Check("excess-warning-before-execute-fresh-generation-every-call", async () =>
{
    var f = new IndependentPorts { Excess = true }; f.SetRows(0, 1); var kernel = f.Kernel(); var first = await kernel.RunPageAsync(f.Request(), default); var generation = f.Executed!.Generation; Require(f.Trace.IndexOf("warning") < f.Trace.IndexOf("execute")); f.Trace.Clear(); f.Prior = first.Receipt; f.SetRows(2); var next = await kernel.RunPageAsync(f.Request(), default); Require(next.Page!.Terminal && f.Executed.Generation != generation && f.Trace.Contains("probe") && f.Trace.Contains("warning"));
});
foreach (var kind in new[] { "policy", "native", "unsupported" }) await Check("bound-returned-decision-" + kind, async () =>
{ var f = new IndependentPorts { WrongClassification = kind == "policy", WrongComparison = kind == "native", UnsupportedOrder = kind == "unsupported" }; f.SetRows(0); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Outcome == SourcePageOutcome.Refused); });
foreach (var disposition in new[] { FieldDisposition.Prohibited, FieldDisposition.Unclassified, FieldDisposition.Redacted, FieldDisposition.Excluded })
    await Check("key-disposition-no-typed-quarantine-" + disposition, async () =>
    { var f = new IndependentPorts { ReturnedDisposition = disposition }; f.SetRows(0); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Outcome == SourcePageOutcome.Quarantined); });
await Check("duplicate-key-two-no-value-occurrences", async () =>
{ var f = new IndependentPorts(); f.SetRows(1, 1); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Reason == SourcePageReason.PagingKeyConflict && r.Conflicts.Single().First.RowOrdinal == 0 && r.Conflicts.Single().Second.RowOrdinal == 1); });
await Check("explicit-uid-duplicates-preserved-without-inferred-semantics", async () =>
{
    var declared = new IndependentPorts(uid: true); declared.SetRows(0, 1); var r = await declared.Kernel().RunPageAsync(declared.Request(), default); Require(r.Page!.Rows.Count == 2 && r.Conflicts.Single().Kind == SourceConflictKind.ObjectUid);
    var undeclared = new IndependentPorts(); undeclared.SetRows(0, 1); var plain = await undeclared.Kernel().RunPageAsync(undeclared.Request(), default); Require(plain.Page!.Rows.Count == 2 && plain.Conflicts.Count == 0);
});
await Check("history-boundary-and-widened-budget-denial", async () =>
{
    var f = new IndependentPorts(); f.SetRows(0, 1); var kernel = f.Kernel(); var prior = await kernel.RunPageAsync(f.Request(), default); f.Prior = prior.Receipt;
    var wrong = await kernel.RunPageAsync(f.Request(boundary: SourceNativeValue.Integer("int", 0)), default); NoPage(wrong); Require(wrong.Counters.Executions == 0);
    var limits = new SourcePageLimits(2, 9, 64, 256, 1024, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2)); var widened = await kernel.RunPageAsync(f.Request(limits: limits), default); NoPage(widened); Require(widened.Counters.Executions == 0);
});
foreach (var dispose in new[] { "reader", "connection" }) await Check("provisional-disposal-denial-" + dispose, async () =>
{ var f = new IndependentPorts { ReaderDisposeFailure = dispose == "reader", ConnectionDisposeFailure = dispose == "connection" }; f.SetRows(0); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Outcome == SourcePageOutcome.Disconnected && !r.ToString().Contains("CANARY", StringComparison.Ordinal) && f.ActiveGenerations.Count == 0); });
foreach (var wait in new[] { "open", "probe", "execute", "read" }) foreach (var rollback in new[] { false, true })
    await Check("original-deadline-cancelable-" + wait + "-rollback-" + rollback, async () =>
    {
        var f = new IndependentPorts(); f.SetRows(0); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var caller = new CancellationTokenSource();
        f.Hook = async (stage, ct) => { if (stage == wait) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); } };
        var run = f.Kernel().RunPageAsync(f.Request(), caller.Token).AsTask(); await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10), rollback ? TimeSpan.FromSeconds(-30) : null);
        try { var r = await run.WaitAsync(TimeSpan.FromMilliseconds(150)); NoPage(r); Require(r.Outcome == SourcePageOutcome.TimedOut && f.ActiveGenerations.Count == 0); }
        finally { caller.Cancel(); await run.WaitAsync(TimeSpan.FromSeconds(1)); }
    });
await Check("retention-crossing-real-clock-state-before-admission", async () =>
{ var f = new IndependentPorts(); f.SetRows(0); f.Observe = stage => { if (stage == "read") f.Clock.Advance(TimeSpan.FromMinutes(2)); }; var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Outcome == SourcePageOutcome.Expired && f.ActiveGenerations.Count == 0); });
await Check("native-binary-defensive-copy", () =>
{ byte[] input = [1, 2, 3]; var native = SourceNativeValue.Binary("varbinary(3)", input); input[0] = 9; var output = native.BinaryValue!; output[1] = 9; Require(native.BinaryValue!.SequenceEqual(new byte[] { 1, 2, 3 }) && native.ToString() == "SourceNativeValue"); return Task.CompletedTask; });
await Check("unrequested-operation-cancellation-never-user-canceled", async () =>
{ var f = new IndependentPorts { UnrequestedCancel = true }; var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Outcome == SourcePageOutcome.Disconnected && r.Reason == SourcePageReason.TransportFailure && f.ActiveGenerations.Count == 0); });
await Check("unrequested-trusted-port-cancellation-is-closed-port-failure", async () =>
{
    var f = new IndependentPorts(); f.Hook = (stage, _) => stage == "probe" ? ValueTask.FromException(new OperationCanceledException("FICTIONAL_PROTECTED_CANARY")) : ValueTask.CompletedTask;
    var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r);
    Require(r.Outcome == SourcePageOutcome.Refused && r.Reason == SourcePageReason.PortFailure && f.ActiveGenerations.Count == 0 && r.Counters.Executions == 0);
});
await Check("unmeasurable-native-length-keeps-known-payload-only", async () =>
{
    var f = new IndependentPorts(); f.Rows = [new(0, [SourceNativeValue.Integer("int", 0), SourceNativeValue.Text("varchar(16)", "x", -1)])];
    var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r);
    Require(r.Reason == SourcePageReason.NativeValueInvalid && r.Counters.RowsObserved == 1 && r.Counters.BytesObserved == 4 && r.Counters.ValueClassifications == 0);
});
await Check("rejected-native-width-payload-in-observed-counter", async () =>
{ var f = new IndependentPorts(); f.Rows = [new(0, [SourceNativeValue.Integer("int", 0), SourceNativeValue.Text("varchar(16)", new string('x', 17), 17)])]; var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Reason == SourcePageReason.NativeValueInvalid && r.Counters.RowsObserved == 1 && r.Counters.BytesObserved == 21); });
await Check("actual-schema-order-refusal-before-value-read", async () =>
{ var f = new IndependentPorts(); f.Schema = new(f.Pair.Fields.Reverse().Select(v => new SourceColumnMetadata(v.Name, v.SqlType, v.Nullable))); f.SetRows(0); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Reason == SourcePageReason.SchemaMismatch && r.Counters.ReadCalls == 0 && r.Counters.ValueClassifications == 0); });
await Check("admitted-binary-immutable-input-and-output", async () =>
{
    var f = new IndependentPorts(); f.Descriptor = f.Descriptor with { Fields = [f.Descriptor.Fields.First(), f.Descriptor.Fields.Last() with { SqlType = "varbinary(3)" }] };
    f.Pair = f.MakePair(); f.Binding = f.MakeBinding(); f.Schema = new(f.Pair.Fields.Select(v => new SourceColumnMetadata(v.Name, v.SqlType, v.Nullable)));
    byte[] bytes = [1, 2, 3]; var row = new SourceReturnedRow(0, [SourceNativeValue.Integer("int", 0), SourceNativeValue.Binary("varbinary(3)", bytes)]); f.Rows = [row]; bytes[0] = 9;
    var r = await f.Kernel().RunPageAsync(f.Request(), default); var admitted = r.Page!.Rows[0].Fields[1].Value!; var copy = admitted.BinaryValue!; copy[1] = 9; Require(admitted.BinaryValue!.SequenceEqual(new byte[] { 1, 2, 3 }) && r.Page.ObservedBytes == 7);
});
await Check("descriptor-and-trusted-policy-collection-freeze", async () =>
{
    var f = new IndependentPorts(); var fields = f.Descriptor.Fields.ToArray(); f.Descriptor = f.Descriptor with { Fields = fields }; f.Pair = f.MakePair(); f.Binding = f.MakeBinding();
    fields[0] = fields[0] with { Classification = FieldClassification.Prohibited }; ((HashSet<FieldKey>)f.Expected.Policy.IncludedFields).Clear(); f.SetRows(0);
    var r = await f.Kernel().RunPageAsync(f.Request(), default); Require(r.Page is not null && r.Page.Rows[0].Fields[0].Disposition == FieldDisposition.Included);
});
await Check("integer-native-width-refusal-before-classification", async () =>
{ var f = new IndependentPorts(); f.SetRows((long)int.MaxValue + 1); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Reason == SourcePageReason.NativeValueInvalid && r.Counters.ValueClassifications == 0); });
await Check("exact-total-byte-ceiling-stops-next-query", async () =>
{
    var f = new IndependentPorts(); f.Limits = new(2, 8, 4, 16, 16, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2)); f.SetRows(0, 1);
    var kernel = f.Kernel(); var first = await kernel.RunPageAsync(f.Request(), default); Require(first.Page is not null && first.Receipt!.CumulativeBytes == 16); f.Prior = first.Receipt;
    var next = await kernel.RunPageAsync(f.Request(), default); NoPage(next); Require(next.Reason == SourcePageReason.TotalByteCap && next.Counters.ConnectionOpens == 0);
});
foreach (var age in new[] { "future", "expired" }) await Check("trusted-history-start-" + age, async () =>
{ var f = new IndependentPorts(); f.Start = age == "future" ? f.Clock.Utc + TimeSpan.FromTicks(1) : f.Clock.Utc - TimeSpan.FromMinutes(2); var r = await f.Kernel().RunPageAsync(f.Request(), default); NoPage(r); Require(r.Counters.ConnectionOpens == 0 && (age != "expired" || r.Outcome == SourcePageOutcome.Expired)); });
await Check("original-start-never-renews-on-continuation", async () =>
{
    var f = new IndependentPorts(); f.SetRows(0, 1); var kernel = f.Kernel(); var first = await kernel.RunPageAsync(f.Request(), default); f.Prior = first.Receipt; f.Clock.Advance(TimeSpan.FromMinutes(1)); f.SetRows(2);
    var next = await kernel.RunPageAsync(f.Request(), default); NoPage(next); Require(next.Outcome == SourcePageOutcome.TimedOut && next.Counters.ConnectionOpens == 0);
});
await Check("single-call-ownership-and-recovery-after-timeout", async () =>
{
    var f = new IndependentPorts(); f.SetRows(0); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    f.Hook = async (stage, ct) => { if (stage == "open") { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); } };
    var kernel = f.Kernel(); var first = kernel.RunPageAsync(f.Request(), default).AsTask(); await entered.Task;
    var overlap = await kernel.RunPageAsync(f.Request(), default); NoPage(overlap); Require(overlap.Reason == SourcePageReason.ConcurrentCall && overlap.Counters.Executions == 0);
    f.Clock.Advance(TimeSpan.FromSeconds(10)); var stopped = await first.WaitAsync(TimeSpan.FromSeconds(1)); NoPage(stopped); f.Hook = null;
    var resumed = await kernel.RunPageAsync(f.Request(), default); Require(resumed.Page is not null);
});
Console.WriteLine($"Independent collector source-page checks: {passed} passed, {failures.Count} failed; physical SP18/SP19 not verified.");
foreach (var failure in failures) Console.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
