using System.Security.Cryptography;
using System.Text;
using CollectorSafety;
using CollectorSourcePages;

internal sealed class IndependentPorts : ITrustedSourceAuthority, ITrustedPageHistory, ISourcePageTransport,
    ITrustedConnectionPermission, IWarningAuditReceiptWriter, ITrustedImpactGate, INativeKeySemantics, ITrustedReturnedValuePolicy
{
    internal readonly ReviewClock Clock = new();
    internal SourceScope Scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    internal Guid PairId = Guid.NewGuid();
    internal QueryPackDescriptor Descriptor;
    internal QueryPackExpectedBindings Expected;
    internal SourceQueryPair Pair;
    internal SourceRegistryBinding Binding;
    internal SourcePageLimits Limits = new(2, 8, 64, 256, 1024, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));
    internal SourcePageReceipt? Prior;
    internal DateTimeOffset Start;
    internal SourceReturnedSchema Schema;
    internal List<SourceReturnedRow> Rows = [];
    internal readonly List<string> Trace = [];
    internal readonly HashSet<Guid> ActiveGenerations = [];
    internal bool Denied = false, Revoked = false, Excess = false, Blocked = false, WrongPermission = false, WrongWarning = false, WrongClassification = false, WrongComparison = false, UnsupportedOrder = false, ReaderDisposeFailure = false, ConnectionDisposeFailure = false, UnrequestedCancel = false;
    internal SourceImpactState Impact = SourceImpactState.Continue;
    internal FieldDisposition ReturnedDisposition = FieldDisposition.Included;
    internal SourceBoundCommand? Executed;
    internal Func<string, CancellationToken, ValueTask>? Hook;
    internal Action<string>? Observe;
    internal IndependentPorts(bool uid = false)
    {
        const string sql = "SELECT TOP (@take) K, U FROM dbo.FictionalSource WHERE K > @after ORDER BY K ASC";
        Descriptor = new(Guid.NewGuid(), "1.0.0", "fictional-query", "1.0.0", sql, Hash(sql),
            new("fictional-query", ["10.0.77"], "Core", ["10.0.77"]), "dbo", "FictionalSource", "FictionalCategory",
            [new("K", "int", false, FieldClassification.ApprovedReference), new("U", "varchar(16)", true, FieldClassification.ApprovedReference)],
            new("fictional-policy", "1.0.0", new string('a', 64)), "fictional-read", "1.0.0", "K", "fictional-key-review",
            "@take", "@after", [new("@take", "int", false), new("@after", "int", false)], 2, 8, TimeSpan.FromMinutes(1), "fictional-query-review");
        Expected = new(Descriptor.PackId, "1.0.0", Descriptor.QueryId, "1.0.0", Descriptor.SqlSha256,
            new("10.0.77", [new("Core", "10.0.77")]), new("fictional-policy", "1.0.0", new string('a', 64),
                new HashSet<FieldKey> { new("FictionalCategory", "K"), new("FictionalCategory", "U") }, new HashSet<string>()), "fictional-read", "1.0.0");
        Pair = MakePair(uid: uid ? "U" : null);
        Binding = MakeBinding();
        Schema = new(Pair.Fields.Select(f => new SourceColumnMetadata(f.Name, f.SqlType, f.Nullable)));
        Start = Clock.Utc;
    }
    internal static string Hash(string sql) => Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(sql)));
    internal SourceQueryPair MakePair(string? first = null, string? uid = null, QueryPackDescriptor? descriptor = null)
    { var sql = first ?? "SELECT TOP (@take) K, U FROM dbo.FictionalSource ORDER BY K ASC"; return new(PairId, descriptor ?? Descriptor, sql, Hash(sql), 1, 1, uid, "schema-v1", "normalization-v1", "repeatability-review-v1"); }
    internal SourceRegistryBinding MakeBinding(Guid? pairId = null) => new(Scope, pairId ?? PairId, Expected, Pair.FirstSqlSha256, 1, 1, Pair.ApprovedUidField, "schema-v1", "normalization-v1", "repeatability-review-v1");
    internal SourcePageRequest Request(int size = 2, SourcePageLimits? limits = null, SourceNativeValue? boundary = null) => new(Scope, Pair, Prior is null ? 0 : Prior.Identity.PageOrdinal + 1, Prior is null ? SourceQueryPhase.First : SourceQueryPhase.Continuation, size, boundary ?? Prior?.NextContinuation, limits ?? Limits);
    internal SourcePageKernel Kernel() => new(this, this, this, this, this, this, this, this, Clock);
    internal void SetRows(params long[] keys) => Rows = keys.Select((key, i) => new SourceReturnedRow(i, [SourceNativeValue.Integer("int", key), SourceNativeValue.Text("varchar(16)", "same", 4)])).ToList();
    internal async ValueTask Call(string stage, CancellationToken ct)
    { Trace.Add(stage); Observe?.Invoke(stage); if (Hook is not null) await Hook(stage, ct); ct.ThrowIfCancellationRequested(); }
    public async ValueTask<SourceAuthorityResolution> ResolveAsync(SourcePageRequest request, CancellationToken ct)
    { await Call("resolve", ct); return new(Denied ? SourceAuthorityState.Missing : SourceAuthorityState.Current, Denied ? null : Binding); }
    public async ValueTask<SourceAuthorityState> RevalidateAsync(SourceRegistryBinding binding, Guid generation, CancellationToken ct)
    { await Call("revalidate", ct); return Revoked ? SourceAuthorityState.Revoked : SourceAuthorityState.Current; }
    public async ValueTask<SourceHistoryResolution> LoadAsync(SourcePageIdentity identity, CancellationToken ct)
    { await Call("history", ct); return new(Prior is null ? SourceHistoryState.Initial : SourceHistoryState.Previous, Start, Prior); }
    public async ValueTask<ISourcePageConnection> OpenAsync(SourceRegistryBinding binding, CancellationToken ct)
    { await Call("open", ct); var connection = new Connection(this); ActiveGenerations.Add(connection.Generation); return connection; }
    public async ValueTask<SourcePermissionReceipt> ProbeAsync(SourceRegistryBinding binding, Guid generation, CancellationToken ct)
    {
        await Call("probe", ct); if (!ActiveGenerations.Contains(generation)) throw new InvalidOperationException("Inactive fictional generation.");
        SourceCapability[] capabilities = Blocked ? [SourceCapability.MinimumRead, SourceCapability.Write] : Excess ? [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly] : [SourceCapability.MinimumRead];
        return new(Scope, PairId, WrongPermission ? Guid.NewGuid() : generation, "fictional-read", "1.0.0", new(true, capabilities));
    }
    public async ValueTask<SourceWarningReceipt?> CommitAsync(SourceWarningIdentity identity, CancellationToken ct)
    { await Call("warning", ct); return new(WrongWarning ? new(identity.Scope, identity.PairId, identity.PageOrdinal, Guid.NewGuid(), identity.MinimumReadSetId, identity.MinimumReadSetVersion) : identity); }
    public async ValueTask<SourceImpactState> CheckAsync(SourceRegistryBinding binding, Guid generation, CancellationToken ct)
    { await Call("impact", ct); return Impact; }
    public SourceNativeComparisonReceipt Compare(SourceRegistryBinding binding, SourceNativePurpose purpose, int ordinal, QueryPackField field, SourceNativeValue left, SourceNativeValue right)
    {
        Trace.Add("compare");
        // Explicitly fictional integer-key order and equality-only fictional UID semantics.
        var order = UnsupportedOrder ? SourceNativeOrder.Unsupported : purpose == SourceNativePurpose.Paging ? left.IntegerValue < right.IntegerValue ? SourceNativeOrder.Less : left.IntegerValue == right.IntegerValue ? SourceNativeOrder.Equal : SourceNativeOrder.Greater : left.TextValue == right.TextValue ? SourceNativeOrder.Equal : SourceNativeOrder.Less;
        return new(WrongComparison ? MakeBinding(Guid.NewGuid()) : Binding, purpose, ordinal, order);
    }
    public SourceValueClassificationReceipt Classify(SourceRegistryBinding binding, int ordinal, QueryPackField field, SourceNativeValue value)
    { Trace.Add("classify"); return new(WrongClassification ? MakeBinding(Guid.NewGuid()) : Binding, ordinal, ReturnedDisposition); }
    private sealed class Connection(IndependentPorts f) : ISourcePageConnection
    {
        public Guid Generation { get; } = Guid.NewGuid();
        public async ValueTask<ISourcePageReader> ExecuteAsync(SourceBoundCommand command, CancellationToken ct)
        { await f.Call("execute", ct); f.Executed = command; return new Reader(f); }
        public ValueTask DisposeAsync() { f.Trace.Add("connection.dispose"); f.ActiveGenerations.Remove(Generation); if (f.ConnectionDisposeFailure) throw new IOException("FICTIONAL_PROTECTED_CANARY"); return ValueTask.CompletedTask; }
    }
    private sealed class Reader(IndependentPorts f) : ISourcePageReader
    {
        private int index;
        public SourceReturnedSchema Schema => f.Schema;
        public async ValueTask<SourceReturnedRow?> ReadAsync(CancellationToken ct)
        { await f.Call("read", ct); if (f.UnrequestedCancel) throw new OperationCanceledException("FICTIONAL_PROTECTED_CANARY"); return index < f.Rows.Count ? f.Rows[index++] : null; }
        public ValueTask DisposeAsync() { f.Trace.Add("reader.dispose"); if (f.ReaderDisposeFailure) throw new IOException("FICTIONAL_PROTECTED_CANARY"); return ValueTask.CompletedTask; }
    }
}

internal sealed class ReviewClock : TimeProvider
{
    internal DateTimeOffset Utc = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private long timestamp;
    private readonly List<ReviewTimer> timers = [];
    public override DateTimeOffset GetUtcNow() => Utc;
    public override long GetTimestamp() => timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { var timer = new ReviewTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer; }
    internal void Advance(TimeSpan elapsed, TimeSpan? utc = null) { timestamp += elapsed.Ticks; Utc += utc ?? elapsed; foreach (var timer in timers.ToArray()) timer.Fire(timestamp); }
    private sealed class ReviewTimer(ReviewClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long due = long.MaxValue; private bool disposed;
        public bool Change(TimeSpan duration, TimeSpan period) { if (disposed) return false; due = duration == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.timestamp + duration.Ticks; return true; }
        internal void Fire(long now) { if (!disposed && now >= due) { due = long.MaxValue; callback(state); } }
        public void Dispose() => disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
