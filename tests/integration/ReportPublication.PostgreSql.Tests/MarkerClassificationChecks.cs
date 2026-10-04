using System.Text.Json.Nodes;
using ReportPublication;

internal static class MarkerClassificationChecks
{
    internal static async Task<int> RunAsync()
    {
        var count = 0;
        foreach (var integrity in new[] { true, false })
        {
            var probe = new Probe(integrity);
            var publisher = new NativeReportPublisherV1(probe, probe, probe, probe, probe);
            var published = await publisher.PublishAsync(probe.Actor, probe.Command, default);
            Require(!published.Succeeded && published.Receipt is null && published.Issue == (integrity ? PublicationIssueV1.IntegrityMismatch : PublicationIssueV1.DependencyUnavailable));
            Require(probe.ProtectedBlobLoads == 0 && probe.Outcome?.Reason == (integrity ? PublicationAuditReasonV1.IntegrityMismatch : PublicationAuditReasonV1.DependencyUnavailable)); count++;
            probe = new Probe(integrity);
            var reader = new NativePublishedReportReaderV1(probe, probe, probe, probe, probe, probe, probe.Clock);
            var read = await reader.ReadExactAsync(probe.Actor, probe.Request, (_, _) => throw new InvalidOperationException("A failed metadata read reached callback."), default);
            Require(read.EventId is null && read.Issue == (integrity ? PublicationIssueV1.IntegrityMismatch : PublicationIssueV1.DependencyUnavailable));
            Require(probe.ProtectedBlobLoads == 0 && probe.Deliveries == 0 && probe.Outcome?.Reason == (integrity ? PublicationAuditReasonV1.IntegrityMismatch : PublicationAuditReasonV1.DependencyUnavailable)); count++;
        }
        Console.WriteLine($"PASS: {count} bounded native trusted-marker/unknown-error classifications; scripted preparation only.");
        return count;
    }
    internal sealed record CleanupResult(string Name, bool Passed, string? FailureType, bool Propagated, int OutcomeCount, int UnknownSignals, int ProtectedLoads, int Deliveries);
    internal static async Task<IReadOnlyList<CleanupResult>> RunCleanupAsync()
    {
        var results = new List<CleanupResult>();
        foreach (var publisher in new[] { true, false })
            foreach (var disposal in new[] { 1, 2 })
            {
                var name = (publisher ? "publisher" : "reader") + "-precommit-" + (disposal == 1 ? "transaction" : "fence") + "-dispose-uncertainty";
                var probe = new Probe(false, disposal); var propagated = false;
                try
                {
                    if (publisher) _ = await new NativeReportPublisherV1(probe, probe, probe, probe, probe).PublishAsync(probe.Actor, probe.Command, default);
                    else _ = await new NativePublishedReportReaderV1(probe, probe, probe, probe, probe, probe, probe.Clock)
                        .ReadExactAsync(probe.Actor, probe.Request, (_, _) => { probe.Deliveries++; return ValueTask.CompletedTask; }, default);
                }
                catch (PublicationCommitUncertainException) { propagated = true; }
                catch (Exception ex) { results.Add(new(name, false, ex.GetType().Name, false, probe.OutcomeCount, probe.UnknownSignals, probe.ProtectedBlobLoads, probe.Deliveries)); continue; }
                var passed = propagated && probe.OutcomeCount == 0 && probe.UnknownSignals == 1 && probe.ProtectedBlobLoads == 0 && probe.Deliveries == 0;
                results.Add(new(name, passed, passed ? null : propagated ? "WrongSignalOrOutcome" : "ExplicitUncertaintyNotPropagated",
                    propagated, probe.OutcomeCount, probe.UnknownSignals, probe.ProtectedBlobLoads, probe.Deliveries));
            }
        foreach (var r in results.Where(x => !x.Passed)) Console.WriteLine("FAIL: " + r.Name + " (" + r.FailureType + ")");
        Console.WriteLine($"Scripted core cleanup: {results.Count(x => x.Passed)} PASS / {results.Count(x => !x.Passed)} FAIL; no physical release evidence.");
        return results;
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Independent marker classification assertion failed."); }
    private sealed class Probe : IPublicationAuthorityV1, IPublicationStoreV1, IPublicationSourceV1,
        IImmutablePublicationBlobsV1, IPublicationOutcomeAuditV1, IReferenceAvailabilityV1, ITrustedReportDeliverySinkV1
    {
        private readonly bool integrity;
        private readonly int uncertainDispose;
        internal readonly FixtureClock Clock = new(DateTimeOffset.Parse("2026-10-03T12:00:00.0000000Z", System.Globalization.CultureInfo.InvariantCulture));
        internal readonly PublicationActorV1 Actor;
        internal readonly PublishCommandV1 Command;
        internal readonly ExactReportRequestV1 Request;
        internal PublicationAuditIntentV1? Outcome;
        internal int ProtectedBlobLoads, Deliveries, OutcomeCount, UnknownSignals;
        internal Probe(bool integrity, int uncertainDispose = 0)
        {
            this.integrity = integrity; this.uncertainDispose = uncertainDispose;
            var command = JsonNode.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", "completed.command.json")))!;
            command["invocationId"] = Guid.NewGuid().ToString("D"); command["correlationId"] = Guid.NewGuid().ToString("D");
            Actor = FrozenFixtureLoader.Load<PublicationActorV1>(command["actor"]!); Command = FrozenFixtureLoader.Load<PublishCommandV1>(command);
            var request = JsonNode.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", "completed.read-request.json")))!;
            request["correlationId"] = Guid.NewGuid().ToString("D"); Request = FrozenFixtureLoader.Load<ExactReportRequestV1>(request);
        }
        private Exception Refusal() => integrity ? new PublicationIntegrityException() : new IOException("Closed fictional dependency failure.");
        public ValueTask<PublicationFenceV1?> EnterPublishAsync(PublicationActorV1 actor, PublishCommandV1 command, CancellationToken ct)
            => ValueTask.FromResult<PublicationFenceV1?>(new Fence(this, actor, command.Scope));
        public ValueTask<PublicationFenceV1?> EnterExactReadAsync(PublicationActorV1 actor, ExactReportRequestV1 request, CancellationToken ct)
            => ValueTask.FromResult<PublicationFenceV1?>(new Fence(this, actor, request.Scope));
        public ValueTask<IPublicationTransactionV1> BeginAsync(PublicationScopeV1 scope, CancellationToken ct)
            => ValueTask.FromResult<IPublicationTransactionV1>(new Transaction(this, scope));
        public ValueTask<SourceCaptureV1?> CaptureAsync(IPublicationTransactionV1 transaction, PublicationActorV1 actor, PublishCommandV1 command, PublicationFenceV1 fence, CancellationToken ct) => throw Refusal();
        public ValueTask<StagedBlobV1> PutIfAbsentAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind, ReadOnlyMemory<byte> bytes, CancellationToken ct) => throw new InvalidOperationException("Source failure reached staging.");
        public ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind, string digest, CancellationToken ct)
        { ProtectedBlobLoads++; throw new InvalidOperationException("Metadata failure reached blob loading."); }
        public ValueTask RecordOutcomeAsync(PublicationAuditIntentV1 intent, CancellationToken ct) { Outcome = intent; OutcomeCount++; return ValueTask.CompletedTask; }
        public void OperationalSignal(PublicationOperationalSignalV1 signal, Guid invocationId, Guid correlationId)
        { if (signal == PublicationOperationalSignalV1.CommitOutcomeUnknown) UnknownSignals++; }
        public ValueTask<IReadOnlyList<PublicationReferenceAvailabilityV1>> ReadAsync(IPublicationTransactionV1 transaction, IReadOnlyList<Guid> ids, CancellationToken ct) => throw new InvalidOperationException("Metadata failure reached reference loading.");
        public ValueTask DeliverAsync(ReadOnlyMemory<byte> bytes, IReadOnlyList<PublicationReferenceAvailabilityV1> availability, CancellationToken ct)
        { Deliveries++; throw new InvalidOperationException("Metadata failure reached delivery."); }
        private sealed class Fence(Probe owner, PublicationActorV1 actor, PublicationScopeV1 scope) : PublicationFenceV1(actor, scope, owner.Clock.GetUtcNow() + TimeSpan.FromMinutes(2))
        {
            public override ValueTask<bool> RevalidateAsync(RequiredPublicationAccessV1 access, CancellationToken ct) => ValueTask.FromResult(true);
            public override ValueTask DisposeAsync() => owner.uncertainDispose == 2
                ? ValueTask.FromException(new PublicationCommitUncertainException(owner.Request.InvocationId)) : ValueTask.CompletedTask;
        }
        private sealed class Transaction(Probe owner, PublicationScopeV1 scope) : IPublicationTransactionV1
        {
            public PublicationScopeV1 Scope => scope;
            public Guid TransactionId { get; } = Guid.NewGuid();
            public ValueTask<DateTimeOffset> ReadDatabaseUtcAsync(CancellationToken ct) => ValueTask.FromResult(owner.Clock.GetUtcNow());
            public ValueTask<PublicationReceiptLookupV1> ResolvePublicationReceiptAsync(Guid id, PublicationActorV1 actor, string digest, CancellationToken ct) => ValueTask.FromResult(new PublicationReceiptLookupV1(owner.uncertainDispose != 0 ? ReceiptLookupStatusV1.Conflict : ReceiptLookupStatusV1.NotFound, null));
            public ValueTask<ExactReadReceiptLookupV1> ResolveReadReceiptAsync(Guid id, PublicationActorV1 actor, string digest, CancellationToken ct) => throw owner.Refusal();
            public ValueTask<CommittedReportVersionV1?> ReadCommittedVersionAsync(Guid id, CancellationToken ct) => owner.uncertainDispose != 0
                ? ValueTask.FromResult<CommittedReportVersionV1?>(null) : throw owner.Refusal();
            public ValueTask<CommittedPublicationAuditV1> AppendAuditAsync(PublicationAuditIntentV1 intent, CancellationToken ct) => throw new InvalidOperationException("Metadata failure reached successful audit.");
            public ValueTask AddPublicationAsync(PublicationCommitV1 publication, CancellationToken ct) => throw new InvalidOperationException("Source failure reached version storage.");
            public ValueTask AddReadReceiptAsync(ExactReadReceiptV1 receipt, CancellationToken ct) => throw new InvalidOperationException("Metadata failure reached receipt storage.");
            public ValueTask<bool> RevalidateSourceAsync(Guid runId, long revision, string digest, CancellationToken ct) => throw owner.Refusal();
            public ValueTask CommitAsync(CancellationToken ct) => throw new InvalidOperationException("Failure reached successful commit.");
            public ValueTask DisposeAsync() => owner.uncertainDispose == 1
                ? ValueTask.FromException(new PublicationCommitUncertainException(owner.Request.InvocationId)) : ValueTask.CompletedTask;
        }
    }
}
