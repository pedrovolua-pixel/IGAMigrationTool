using System.Security.Cryptography;
using System.Text;
using CollectorSafety;
using CollectorSourcePages;

namespace CollectorSourcePages.Tests;

internal sealed class ScriptedClock : TimeProvider
{
    private DateTimeOffset _utc = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private long _ticks;
    public override DateTimeOffset GetUtcNow() => _utc;
    public override long GetTimestamp() => _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    internal void Advance(TimeSpan elapsed) { _utc += elapsed; _ticks += elapsed.Ticks; }
    internal void MoveUtc(TimeSpan elapsed) => _utc += elapsed;
    internal void AdvanceMonotonic(TimeSpan elapsed) => _ticks += elapsed.Ticks;
}

/// <summary>Fictional finite fixtures, never vendor permission or native ordering evidence.</summary>
internal sealed class ScriptedSourcePorts : ITrustedSourceAuthority, ITrustedPageHistory, ISourcePageTransport,
    ITrustedConnectionPermission, IWarningAuditReceiptWriter, ITrustedImpactGate, INativeKeySemantics, ITrustedReturnedValuePolicy
{
    internal static readonly Guid PairId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid PackId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    internal SourceScope Scope { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"),
        Guid.Parse("40000000-0000-0000-0000-000000000004"), Guid.Parse("50000000-0000-0000-0000-000000000005"), Guid.Parse("60000000-0000-0000-0000-000000000006"));
    internal readonly ScriptedClock Clock = new();
    internal TimeProvider Time { get; set; }
    internal readonly List<string> Trace = [];
    internal readonly Dictionary<string, Action> Hooks = [];
    private readonly Dictionary<Guid, ScriptedConnection> _active = [];
    internal SourceAuthorityState AuthorityState = SourceAuthorityState.Current;
    internal SourceAuthorityState RevalidationState = SourceAuthorityState.Current;
    internal SourceImpactState ImpactState = SourceImpactState.Continue;
    internal SourceRegistryBinding Binding { get; set; }
    internal SourceRegistryBinding? ComparisonBinding;
    internal SourceRegistryBinding? ClassificationBinding;
    internal SourceHistoryResolution History { get; set; }
    internal SourceReturnedSchema Schema { get; set; }
    internal SourceReturnedRow[] Rows { get; set; }
    internal SourceCapability[] Capabilities = [SourceCapability.MinimumRead];
    internal bool MinimumSatisfied = true;
    internal bool AuditMissing, AuditSubstitution, PermissionSubstitution, UnregisterBeforeProbe, DisposalFailure;
    internal bool UnsupportedPaging;
    internal string? HoldAt;
    internal Func<int, SourceNativeValue, FieldDisposition>? Classifier;
    internal SourceBoundCommand? LastCommand;
    internal SourceWarningIdentity? LastWarning;
    internal ScriptedConnection? LastConnection, ProbedConnection;
    internal QueryPackDescriptor Descriptor { get; set; }
    internal SourceQueryPair Pair { get; set; }
    internal QueryPackExpectedBindings Expected { get; set; }
    internal int ComparisonOrdinalDelta, ClassificationOrdinalDelta;
    internal ScriptedSourcePorts()
    {
        Time = Clock;
        var fields = new[] { new QueryPackField("K", "int", false, FieldClassification.ApprovedReference),
            new QueryPackField("U", "varchar(20)", false, FieldClassification.ApprovedReference),
            new QueryPackField("V", "varbinary(8)", true, FieldClassification.ApprovedReference) };
        const string continuation = "SELECT TOP (@PageSize) K, U, V FROM fixture.NativeFixture WHERE K > @After ORDER BY K ASC";
        Descriptor = new(PackId, "1.0.0", "fictional-query", "1.0.0", continuation, Hash(continuation),
            new("fictional-query", ["fixture-build"], "fixture-module", ["fixture-module-build"]), "fixture", "NativeFixture", "fixture-category",
            fields, new("fixture-policy", "policy-v1", new string('A', 64)), "fixture-min-read", "minimum-v1", "K", "fictional-key-review",
            "@PageSize", "@After", [new("@PageSize", "int", false), new("@After", "int", false)], 10, 100, TimeSpan.FromMinutes(10), "fictional-query-review");
        Expected = new(PackId, "1.0.0", "fictional-query", "1.0.0", Hash(continuation),
            new("fixture-build", [new("fixture-module", "fixture-module-build")]),
            new("fixture-policy", "policy-v1", new string('A', 64), fields.Select(f => new FieldKey("fixture-category", f.Name)).ToHashSet(), new HashSet<string>()),
            "fixture-min-read", "minimum-v1");
        Pair = MakePair(); Binding = MakeBinding();
        Schema = DefaultSchema(); Rows = [Row(0, -1), Row(1, 0)];
        History = new(SourceHistoryState.Initial, Time.GetUtcNow(), null);
    }
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(value)));
    internal SourceQueryPair MakePair(QueryPackDescriptor? descriptor = null, string? firstSql = null, string? firstHash = null,
        int packRevision = 1, int policyRevision = 1, string? uidField = "U", string schemaVersion = "schema-v1", string normalizationVersion = "normalization-v1")
    {
        firstSql ??= "SELECT TOP (@PageSize) K, U, V FROM fixture.NativeFixture ORDER BY K ASC";
        return new(PairId, descriptor ?? Descriptor, firstSql, firstHash ?? Hash(firstSql), packRevision, policyRevision,
            uidField, schemaVersion, normalizationVersion, "fictional-repeatability-review");
    }
    internal SourceRegistryBinding MakeBinding(SourceScope? scope = null, Guid? pairId = null, QueryPackExpectedBindings? expected = null,
        string? firstHash = null, int packRevision = 1, int policyRevision = 1, string? uidField = "U", string schemaVersion = "schema-v1", string normalizationVersion = "normalization-v1") =>
        new(scope ?? Scope, pairId ?? PairId, expected ?? Expected, firstHash ?? Pair.FirstSqlSha256, packRevision, policyRevision,
            uidField, schemaVersion, normalizationVersion, "fictional-repeatability-review");
    internal SourcePageLimits Limits(long maximumRows = 100, long fieldBytes = 100, long pageBytes = 500, long totalBytes = 4096,
        TimeSpan? duration = null, TimeSpan? timeout = null, TimeSpan? retention = null) =>
        new(10, maximumRows, fieldBytes, pageBytes, totalBytes, duration ?? TimeSpan.FromMinutes(10), timeout ?? TimeSpan.FromSeconds(10), retention ?? TimeSpan.FromHours(1));
    internal SourcePageRequest Request(int size = 2, SourceQueryPair? pair = null, SourceScope? scope = null, long ordinal = 0,
        SourceQueryPhase phase = SourceQueryPhase.First, SourceNativeValue? continuation = null, SourcePageLimits? limits = null) =>
        new(scope ?? Scope, pair ?? Pair, ordinal, phase, size, continuation, limits ?? Limits());
    internal SourcePageKernel Kernel() => new(this, this, this, this, this, this, this, this, Time);
    internal SourceReturnedSchema DefaultSchema() => new(Descriptor.Fields.Select(f => new SourceColumnMetadata(f.Name, f.SqlType, f.Nullable)));
    internal static SourceReturnedRow Row(long ordinal, long key, string uid = "fixture-uid", byte[]? bytes = null) =>
        new(ordinal, [SourceNativeValue.Integer("int", key), SourceNativeValue.Text("varchar(20)", uid, uid.Length),
            SourceNativeValue.Binary("varbinary(8)", bytes ?? [1, 2])]);
    private async ValueTask TouchAsync(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Trace.Add(name);
        if (Hooks.TryGetValue(name, out var action)) action();
        if (HoldAt == name) await Task.Delay(Timeout.InfiniteTimeSpan, token);
    }
    public async ValueTask<SourceAuthorityResolution> ResolveAsync(SourcePageRequest request, CancellationToken cancellationToken)
    { await TouchAsync("resolve", cancellationToken); return new(AuthorityState, AuthorityState == SourceAuthorityState.Current ? Binding : null); }
    public async ValueTask<SourceAuthorityState> RevalidateAsync(SourceRegistryBinding binding, Guid connectionGeneration, CancellationToken cancellationToken)
    { await TouchAsync("revalidate", cancellationToken); return _active.ContainsKey(connectionGeneration) ? RevalidationState : SourceAuthorityState.Missing; }
    public async ValueTask<SourceHistoryResolution> LoadAsync(SourcePageIdentity identity, CancellationToken cancellationToken)
    { await TouchAsync("history", cancellationToken); return History; }
    public async ValueTask<ISourcePageConnection> OpenAsync(SourceRegistryBinding binding, CancellationToken cancellationToken)
    {
        await TouchAsync("open", cancellationToken);
        LastConnection = new(this, Guid.NewGuid()); _active.Add(LastConnection.Generation, LastConnection); return LastConnection;
    }
    public async ValueTask<SourcePermissionReceipt> ProbeAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken)
    {
        await TouchAsync("probe", cancellationToken);
        if (UnregisterBeforeProbe) _active.Remove(generation);
        if (!_active.TryGetValue(generation, out var actual)) throw new InvalidOperationException("fixture-unregistered-generation");
        ProbedConnection = actual;
        return new(Scope, PairId, PermissionSubstitution ? Guid.NewGuid() : actual.Generation, "fixture-min-read", "minimum-v1", new(MinimumSatisfied, Capabilities));
    }
    public async ValueTask<SourceWarningReceipt?> CommitAsync(SourceWarningIdentity identity, CancellationToken cancellationToken)
    {
        await TouchAsync("warning", cancellationToken); LastWarning = identity;
        if (AuditMissing) return null;
        return new(AuditSubstitution ? new(identity.Scope, identity.PairId, identity.PageOrdinal, Guid.NewGuid(), identity.MinimumReadSetId, identity.MinimumReadSetVersion) : identity);
    }
    public async ValueTask<SourceImpactState> CheckAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken)
    { await TouchAsync("impact", cancellationToken); return ImpactState; }
    public SourceNativeComparisonReceipt Compare(SourceRegistryBinding binding, SourceNativePurpose purpose, int fieldOrdinal,
        QueryPackField field, SourceNativeValue left, SourceNativeValue right)
    {
        Trace.Add("compare"); if (Hooks.TryGetValue("compare", out var action)) action();
        // These fixture semantics are explicitly fictional; product has no comparator.
        var comparison = purpose == SourceNativePurpose.Paging && !UnsupportedPaging && left.Kind == SourceNativeKind.Integer && right.Kind == SourceNativeKind.Integer
            ? left.IntegerValue.CompareTo(right.IntegerValue)
            : purpose == SourceNativePurpose.ObjectIdentity && left.Kind == SourceNativeKind.Text && right.Kind == SourceNativeKind.Text
                ? string.CompareOrdinal(left.TextValue, right.TextValue) : int.MinValue;
        var order = comparison == int.MinValue ? SourceNativeOrder.Unsupported : comparison < 0 ? SourceNativeOrder.Less : comparison > 0 ? SourceNativeOrder.Greater : SourceNativeOrder.Equal;
        return new(ComparisonBinding ?? binding, purpose, fieldOrdinal + ComparisonOrdinalDelta, order);
    }
    public SourceValueClassificationReceipt Classify(SourceRegistryBinding binding, int fieldOrdinal, QueryPackField field, SourceNativeValue value)
    {
        Trace.Add("classify"); if (Hooks.TryGetValue("classify", out var action)) action();
        return new(ClassificationBinding ?? binding, fieldOrdinal + ClassificationOrdinalDelta, Classifier?.Invoke(fieldOrdinal, value) ?? FieldDisposition.Included);
    }
    internal sealed class ScriptedConnection(ScriptedSourcePorts owner, Guid generation) : ISourcePageConnection
    {
        private readonly Guid _originalGeneration = generation;
        public Guid Generation { get; private set; } = generation;
        internal void ReplaceGeneration() => Generation = Guid.NewGuid();
        public async ValueTask<ISourcePageReader> ExecuteAsync(SourceBoundCommand command, CancellationToken cancellationToken)
        { await owner.TouchAsync("execute", cancellationToken); owner.LastCommand = command; return new ScriptedReader(owner); }
        public ValueTask DisposeAsync()
        {
            owner.Trace.Add("dispose-connection"); owner._active.Remove(_originalGeneration);
            if (owner.Hooks.TryGetValue("dispose-connection", out var action)) action();
            if (owner.DisposalFailure) throw new InvalidOperationException("fixture-disposal"); return ValueTask.CompletedTask;
        }
    }
    private sealed class ScriptedReader(ScriptedSourcePorts owner) : ISourcePageReader
    {
        private int _position;
        public SourceReturnedSchema Schema
        {
            get { owner.Trace.Add("schema"); if (owner.Hooks.TryGetValue("schema", out var action)) action(); return owner.Schema; }
        }
        public async ValueTask<SourceReturnedRow?> ReadAsync(CancellationToken cancellationToken)
        { await owner.TouchAsync("read", cancellationToken); return _position < owner.Rows.Length ? owner.Rows[_position++] : null; }
        public ValueTask DisposeAsync()
        {
            owner.Trace.Add("dispose-reader"); if (owner.Hooks.TryGetValue("dispose-reader", out var action)) action();
            if (owner.DisposalFailure) throw new InvalidOperationException("fixture-disposal"); return ValueTask.CompletedTask;
        }
    }
}
