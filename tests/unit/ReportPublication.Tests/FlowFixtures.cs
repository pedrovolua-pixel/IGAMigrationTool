using System.Text.Json.Nodes;
using ReportPublication;

internal sealed class FlowFixture : IPublicationAuthorityV1, IPublicationSourceV1, IPublicationStoreV1,
    IImmutablePublicationBlobsV1, IPublicationOutcomeAuditV1, IReferenceAvailabilityV1, ITrustedReportDeliverySinkV1
{
    internal static FlowFixture? LastCreated;
    internal readonly List<string> Trace = [];
    internal readonly List<PublicationAuditIntentV1> Outcomes = [];
    internal readonly List<PublicationOperationalSignalV1> Signals = [];
    internal readonly List<PublicationAuditIntentV1> CommittedAudit = [];
    internal readonly Dictionary<(PublicationBlobKindV1, string), byte[]> Blobs = [];
    internal readonly List<byte[]> Deliveries = [];
    internal IReadOnlyList<PublicationReferenceAvailabilityV1>? DeliveredOverlay;
    internal readonly ManualPublicationClock Clock = new();
    internal SourceCaptureV1 Source;
    internal PublicationActorV1 Actor;
    internal PublishCommandV1 Command;
    internal ExactReportRequestV1 Request;
    internal CommittedReportVersionV1? Visible;
    internal PublicationReceiptV1? Receipt;
    internal ExactReadReceiptV1? ReadReceipt;
    internal ScriptFence? LastFence;
    internal ScriptTransaction? LastTransaction;
    internal VerifiedPublishedReportV1? LastView;
    internal bool DenyEntry, DenyAccess, FailAudit, FailOutcomeAudit, CorruptBlob, FailFenceDispose = false, FailTransactionDispose = false;
    internal bool SourceCurrent = true;
    internal ReceiptLookupStatusV1? PublicationLookupOverride, ReadLookupOverride;
    internal string CommitMode = "Success";
    internal int BeginCalls, SourceMaterializations, SinkCalls;
    internal Action<FlowFixture, string>? OnTrace;
    internal Func<FlowFixture, PublicationAuditIntentV1, CancellationToken, ValueTask>? AuditHook;
    internal Func<FlowFixture, IReadOnlyList<PublicationReferenceAvailabilityV1>>? Overlay;
    internal Func<FlowFixture, CancellationToken, ValueTask>? SinkHook = null;
    internal Func<FlowFixture, PublicationBlobKindV1, CancellationToken, ValueTask>? BlobHook;
    internal Func<FlowFixture, CancellationToken, ValueTask>? ReferenceHook;
    internal Func<FlowFixture, string, CancellationToken, ValueTask>? MetadataHook;
    internal FlowFixture(string bundle = "completed")
    {
        LastCreated = this;
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures");
        JsonNode Read(string file) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(path, bundle + file)))!;
        Source = FixtureLoader.Source(Read(".source.json"));
        var c = Read(".command.json"); c["invocationId"] = Guid.NewGuid().ToString(); c["correlationId"] = Guid.NewGuid().ToString();
        Actor = FixtureLoader.Load<PublicationActorV1>(c["actor"]!); Command = FixtureLoader.Load<PublishCommandV1>(c);
        var r = Read(".read-request.json"); r["correlationId"] = Guid.NewGuid().ToString(); Request = FixtureLoader.Load<ExactReportRequestV1>(r);
        var m = FixtureLoader.Load<PublicationManifestV1>(Read(".manifest.json"));
        Visible = new(m, NativePublicationCanonicalV1.Hash(File.ReadAllBytes(Path.Combine(path, bundle + ".manifest.json"))), PublicationLifecycleStateV1.Active, 1, m.Retention.ExpiresAtUtc, false);
        foreach (var kind in new[] { PublicationBlobKindV1.Projection, PublicationBlobKindV1.Score, PublicationBlobKindV1.Manifest })
        {
            var bytes = File.ReadAllBytes(Path.Combine(path, bundle + "." + kind.ToString().ToLowerInvariant() + ".json"));
            Blobs[(kind, NativePublicationCanonicalV1.Hash(bytes))] = bytes;
        }
        Clock.Utc = m.CreatedAtUtc;
    }
    internal NativeReportPublisherV1 Publisher() => new(this, this, this, this, this);
    internal NativePublishedReportReaderV1 Reader() => new(this, this, this, this, this, this, Clock);
    internal void Mark(string value) { Trace.Add(value); OnTrace?.Invoke(this, value); }
    internal void Require(bool condition) { if (!condition) throw new InvalidOperationException("Trusted fixture assertion failed."); }
    public ValueTask<PublicationFenceV1?> EnterPublishAsync(PublicationActorV1 actor, PublishCommandV1 command, CancellationToken ct)
    { Mark("authority.publish"); Require(actor == Actor && command.Scope == Source.Projection.Scope); return Enter(actor, command.Scope); }
    public ValueTask<PublicationFenceV1?> EnterExactReadAsync(PublicationActorV1 actor, ExactReportRequestV1 request, CancellationToken ct)
    { Mark("authority.read"); Require(actor == Actor && request.Scope == Source.Projection.Scope); return Enter(actor, request.Scope); }
    private ValueTask<PublicationFenceV1?> Enter(PublicationActorV1 actor, PublicationScopeV1 scope)
    { LastFence = DenyEntry ? null : new(this, actor, scope, Clock.Utc + TimeSpan.FromSeconds(10)); return ValueTask.FromResult<PublicationFenceV1?>(LastFence); }
    public async ValueTask<IPublicationTransactionV1> BeginAsync(PublicationScopeV1 scope, CancellationToken ct)
    { Mark("transaction.begin"); BeginCalls++; if (MetadataHook is not null) await MetadataHook(this, "transaction.begin", ct); ct.ThrowIfCancellationRequested(); LastTransaction = new(this, scope); return LastTransaction; }
    public async ValueTask<SourceCaptureV1?> CaptureAsync(IPublicationTransactionV1 transaction, PublicationActorV1 actor, PublishCommandV1 command, PublicationFenceV1 fence, CancellationToken ct)
    {
        Mark("source.capture"); Require(ReferenceEquals(transaction, LastTransaction) && ReferenceEquals(fence, LastFence) && actor == Actor && ReferenceEquals(command, Command));
        // Owning adapter obtains safe requirements metadata first; no protected source materialization before this authorization.
        if (!await fence.RevalidateAsync(new(Source.RequiredCategories, Source.RequiredFields), ct)) return null;
        Mark("source.materialize"); SourceMaterializations++; return Source;
    }
    public ValueTask<StagedBlobV1> PutIfAbsentAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        Mark("blob.put." + kind); Require(scope == Source.Projection.Scope); var digest = NativePublicationCanonicalV1.Hash(bytes.Span);
        Blobs.TryAdd((kind, digest), bytes.ToArray()); return ValueTask.FromResult(new StagedBlobV1(kind, digest, bytes.Length));
    }
    public async ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind, string expectedDigest, CancellationToken ct)
    {
        Mark("blob.read." + kind); Require(scope == Source.Projection.Scope);
        if (BlobHook is not null) await BlobHook(this, kind, ct);
        ct.ThrowIfCancellationRequested();
        if (!Blobs.TryGetValue((kind, expectedDigest), out var bytes)) return null;
        if (CorruptBlob) { var bad = bytes.ToArray(); bad[0] ^= 1; return bad; }
        return bytes;
    }
    public ValueTask RecordOutcomeAsync(PublicationAuditIntentV1 intent, CancellationToken ct)
    { Mark("audit.outcome"); if (FailOutcomeAudit) throw new IOException("Scripted outcome outage."); Outcomes.Add(intent); return ValueTask.CompletedTask; }
    public void OperationalSignal(PublicationOperationalSignalV1 signal, Guid invocationId, Guid correlationId)
    { Mark("signal." + signal); Signals.Add(signal); }
    public async ValueTask<IReadOnlyList<PublicationReferenceAvailabilityV1>> ReadAsync(IPublicationTransactionV1 transaction, IReadOnlyList<Guid> linkedReferenceIds, CancellationToken ct)
    {
        Mark("references.read"); Require(ReferenceEquals(transaction, LastTransaction));
        if (ReferenceHook is not null) await ReferenceHook(this, ct);
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<PublicationReferenceAvailabilityV1> value = Overlay?.Invoke(this) ?? linkedReferenceIds.Select(id => new PublicationReferenceAvailabilityV1(id, PublicationAvailabilityV1.Unavailable, PublicationReasonV1.Expired, 1)).ToArray();
        return value;
    }
    public async ValueTask DeliverAsync(ReadOnlyMemory<byte> projection, IReadOnlyList<PublicationReferenceAvailabilityV1> overlay, CancellationToken ct)
    {
        Mark("sink.enter"); Require(ReadReceipt is not null); ct.ThrowIfCancellationRequested();
        if (SinkHook is not null) await SinkHook(this, ct);
        ct.ThrowIfCancellationRequested(); SinkCalls++; Deliveries.Add(projection.ToArray()); DeliveredOverlay = overlay.ToArray(); Mark("sink.delivered");
    }
    internal sealed class ScriptFence(FlowFixture f, PublicationActorV1 actor, PublicationScopeV1 scope, DateTimeOffset deadline) : PublicationFenceV1(actor, scope, deadline)
    {
        internal bool Disposed;
        public override async ValueTask<bool> RevalidateAsync(RequiredPublicationAccessV1 access, CancellationToken ct)
        {
            f.Mark("authority.full"); f.Require(access.Categories.ToHashSet().SetEquals(f.Source.RequiredCategories) && access.Fields.ToHashSet().SetEquals(f.Source.RequiredFields));
            if (f.MetadataHook is not null) await f.MetadataHook(f, "authority.full", ct);
            ct.ThrowIfCancellationRequested(); return !Disposed && !f.DenyAccess;
        }
        public override ValueTask DisposeAsync() { Disposed = true; f.Mark("fence.dispose"); if (f.FailFenceDispose) throw new IOException("Scripted fence disposal."); return ValueTask.CompletedTask; }
    }
    internal sealed class ScriptTransaction(FlowFixture f, PublicationScopeV1 scope) : IPublicationTransactionV1
    {
        public PublicationScopeV1 Scope => scope;
        public Guid TransactionId { get; } = Guid.NewGuid();
        internal bool Disposed, Applied;
        internal PublicationCommitV1? PendingPublication;
        internal ExactReadReceiptV1? PendingReadReceipt;
        internal readonly List<PublicationAuditIntentV1> PendingAudit = [];
        public async ValueTask<DateTimeOffset> ReadDatabaseUtcAsync(CancellationToken ct) { f.Mark("database.time"); if (f.MetadataHook is not null) await f.MetadataHook(f, "database.time", ct); ct.ThrowIfCancellationRequested(); return f.Clock.Utc; }
        public ValueTask<PublicationReceiptLookupV1> ResolvePublicationReceiptAsync(Guid operationId, PublicationActorV1 actor, string commandDigest, CancellationToken ct)
        { f.Mark("receipt.publish.lookup"); return ValueTask.FromResult(new PublicationReceiptLookupV1(f.PublicationLookupOverride ?? (f.Receipt is null ? ReceiptLookupStatusV1.NotFound : ReceiptLookupStatusV1.Found), f.Receipt)); }
        public async ValueTask<ExactReadReceiptLookupV1> ResolveReadReceiptAsync(Guid invocationId, PublicationActorV1 actor, string requestDigest, CancellationToken ct)
        { f.Mark("receipt.read.lookup"); if (f.MetadataHook is not null) await f.MetadataHook(f, "receipt.read.lookup", ct); ct.ThrowIfCancellationRequested(); return new(f.ReadLookupOverride ?? (f.ReadReceipt is null ? ReceiptLookupStatusV1.NotFound : ReceiptLookupStatusV1.Found), f.ReadReceipt); }
        public async ValueTask<CommittedReportVersionV1?> ReadCommittedVersionAsync(Guid versionId, CancellationToken ct)
        { f.Mark("version.read"); if (f.MetadataHook is not null) await f.MetadataHook(f, "version.read", ct); ct.ThrowIfCancellationRequested(); return f.Visible; }
        public async ValueTask<CommittedPublicationAuditV1> AppendAuditAsync(PublicationAuditIntentV1 intent, CancellationToken ct)
        {
            f.Mark("audit.append"); if (f.FailAudit) throw new IOException("Scripted audit outage.");
            if (f.AuditHook is not null) await f.AuditHook(f, intent, ct);
            PendingAudit.Add(intent); return new(Guid.NewGuid(), new string('d', 64), f.Clock.Utc);
        }
        public ValueTask AddPublicationAsync(PublicationCommitV1 publication, CancellationToken ct) { f.Mark("publication.pending"); PendingPublication = publication; return ValueTask.CompletedTask; }
        public ValueTask AddReadReceiptAsync(ExactReadReceiptV1 receipt, CancellationToken ct) { f.Mark("readreceipt.pending"); PendingReadReceipt = receipt; return ValueTask.CompletedTask; }
        public ValueTask<bool> RevalidateSourceAsync(Guid runId, long revision, string digest, CancellationToken ct)
        { f.Mark("source.revalidate"); f.Require(runId == f.Source.Projection.RunId && revision == f.Source.Projection.RunRevision && digest == NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.SourceBytes(f.Source))); return ValueTask.FromResult(f.SourceCurrent); }
        public ValueTask CommitAsync(CancellationToken ct)
        {
            f.Mark("commit.attempt"); ct.ThrowIfCancellationRequested();
            if (f.CommitMode == "NotApplied") throw new PublicationCommitNotAppliedException();
            if (f.CommitMode == "UnknownBefore") throw new IOException("Scripted unknown before apply.");
            if (PendingPublication is { } publication)
            {
                f.Receipt = publication.Receipt; f.Visible = new(publication.Manifest, publication.ManifestDigest, PublicationLifecycleStateV1.Active, 1, publication.Manifest.Retention.ExpiresAtUtc, false);
            }
            if (PendingReadReceipt is { } read) f.ReadReceipt = read;
            f.CommittedAudit.AddRange(PendingAudit); Applied = true; f.Mark("commit.applied");
            if (f.CommitMode == "UnknownAfter") throw new IOException("Scripted unknown after apply.");
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() { Disposed = true; f.Mark("transaction.dispose"); if (f.FailTransactionDispose) throw new IOException("Scripted transaction disposal."); return ValueTask.CompletedTask; }
    }
}

internal sealed class ManualPublicationClock : TimeProvider
{
    internal DateTimeOffset Utc;
    private long timestamp;
    private readonly List<ManualTimer> timers = [];
    public override DateTimeOffset GetUtcNow() => Utc;
    public override long GetTimestamp() => timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { var timer = new ManualTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer; }
    internal void Advance(TimeSpan elapsed, TimeSpan? utc = null)
    {
        timestamp += elapsed.Ticks; Utc += utc ?? elapsed;
        foreach (var timer in timers.ToArray()) timer.Fire(timestamp);
    }
    private sealed class ManualTimer(ManualPublicationClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long due = long.MaxValue;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        { if (disposed) return false; due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.timestamp + dueTime.Ticks; return true; }
        internal void Fire(long now) { if (!disposed && now >= due) { due = long.MaxValue; callback(state); } }
        public void Dispose() { disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
