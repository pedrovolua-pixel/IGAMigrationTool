using ReportPublication;

internal static class FlowTests
{
    internal static async Task<int> RunAsync()
    {
        var failed = new List<string>(); var passed = 0;
        async Task Check(string name, Func<Task> test)
        { try { await test().WaitAsync(TimeSpan.FromSeconds(5)); passed++; } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + " " + e.Message + " | Trace=" + string.Join(",", FlowFixture.LastCreated?.Trace ?? []) + " | Outcomes=" + string.Join(",", FlowFixture.LastCreated?.Outcomes.Select(v => v.Outcome + "/" + v.Reason) ?? [])); } }
        void Assert(bool value) { if (!value) throw new InvalidOperationException("Independent flow assertion failed."); }
        FlowFixture PublishFixture(string bundle = "completed") => new(bundle) { Visible = null };
        void NoContent(FlowFixture f) => Assert(f.SinkCalls == 0 && f.Deliveries.Count == 0);
        void RolledBack(FlowFixture f) => Assert(f.Receipt is null && f.CommittedAudit.Count == 0 && f.LastTransaction is { Disposed: true } && f.LastFence is { Disposed: true });
        ValueTask<ExactReadResultV1> Deliver(FlowFixture f, CancellationToken ct = default) => f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, token) => { f.LastView = view; await view.DeliverAsync(token); }, ct);
        async Task LateRefused(VerifiedPublishedReportV1 view)
        { try { await view.DeliverAsync(default); } catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { return; } throw new Exception("Late view delivered."); }
        void Before(FlowFixture f, string first, string second) => Assert(f.Trace.IndexOf(first) >= 0 && f.Trace.IndexOf(first) < f.Trace.IndexOf(second));
        await Check("publish-authority-preload-stage-audit-visibility", async () =>
        {
            var f = PublishFixture(); f.OnTrace = (state, trace) => { if (trace.StartsWith("blob.") || trace == "audit.append" || trace == "publication.pending") Assert(state.Visible is null && state.Receipt is null); };
            var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default);
            Assert(r.Succeeded && r.Receipt == f.Receipt && f.Visible is not null && f.CommittedAudit.Count == 1);
            Before(f, "authority.publish", "source.capture"); Before(f, "authority.full", "source.materialize"); Before(f, "audit.append", "commit.applied");
            Assert(f.LastFence!.Disposed && f.LastTransaction!.Disposed && f.SourceMaterializations == 1);
        });
        await Check("publish-invalid-no-protected-access", async () =>
        {
            var f = PublishFixture(); f.Command = new(Guid.Empty, f.Command.InvocationId, f.Command.CorrelationId, f.Command.Scope, f.Command.RunId, f.Command.ExpectedRunRevision, f.Command.ExpectedSourceDigest, []);
            var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default);
            Assert(r.Issue == PublicationIssueV1.InvalidInput && f.BeginCalls == 0 && f.SourceMaterializations == 0 && !f.Trace.Contains("authority.publish"));
        });
        await Check("publish-entry-denied-no-source-store", async () =>
        { var f = PublishFixture(); f.DenyEntry = true; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(!r.Succeeded && f.BeginCalls == 0 && f.SourceMaterializations == 0); });
        await Check("publish-source-full-access-denial-before-materialization", async () =>
        { var f = PublishFixture(); f.DenyAccess = true; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(!r.Succeeded && f.SourceMaterializations == 0 && !f.Trace.Any(s => s.StartsWith("blob."))); RolledBack(f); });
        await Check("publish-warning-exact-acknowledgment", async () =>
        { var f = PublishFixture("warned"); f.Command = new(f.Command.OperationId, f.Command.InvocationId, f.Command.CorrelationId, f.Command.Scope, f.Command.RunId, f.Command.ExpectedRunRevision, f.Command.ExpectedSourceDigest, []); var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(!r.Succeeded && !f.Trace.Any(s => s.StartsWith("blob."))); RolledBack(f); });
        await Check("publish-revision-bind-before-staging", async () =>
        { var f = PublishFixture(); f.Command = new(f.Command.OperationId, f.Command.InvocationId, f.Command.CorrelationId, f.Command.Scope, f.Command.RunId, f.Command.ExpectedRunRevision + 1, f.Command.ExpectedSourceDigest, []); var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.RevisionConflict && !f.Trace.Any(s => s.StartsWith("blob."))); });
        await Check("publish-source-hash-bind-before-staging", async () =>
        { var f = PublishFixture(); f.Command = new(f.Command.OperationId, f.Command.InvocationId, f.Command.CorrelationId, f.Command.Scope, f.Command.RunId, f.Command.ExpectedRunRevision, new string('f', 64), []); var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.RevisionConflict && !f.Trace.Any(s => s.StartsWith("blob."))); });
        await Check("publish-source-revoked-before-commit", async () =>
        { var f = PublishFixture(); f.SourceCurrent = false; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(!r.Succeeded && !f.Trace.Contains("commit.attempt")); RolledBack(f); Before(f, "transaction.dispose", "audit.outcome"); });
        await Check("publish-audit-outage-no-visible-marker", async () =>
        { var f = PublishFixture(); f.FailAudit = true; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.DependencyUnavailable && f.Blobs.Count >= 3); RolledBack(f); });
        await Check("publish-stage-integrity-classification", async () =>
        { var f = PublishFixture(); f.CorruptBlob = true; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.IntegrityMismatch && f.Outcomes.Single().Reason == PublicationAuditReasonV1.IntegrityMismatch); RolledBack(f); });
        await Check("publish-safe-audit-outage-payload-free-signal", async () =>
        { var f = PublishFixture(); f.DenyEntry = true; f.FailOutcomeAudit = true; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.DependencyUnavailable && f.Outcomes.Count == 0 && f.Signals.SequenceEqual(new[] { PublicationOperationalSignalV1.AuditUnavailable })); });
        await Check("publish-cancel-before-commit-certain-rollback", async () =>
        { var f = PublishFixture(); using var cts = new CancellationTokenSource(); f.OnTrace = (_, trace) => { if (trace == "source.revalidate") cts.Cancel(); }; try { await f.Publisher().PublishAsync(f.Actor, f.Command, cts.Token); throw new Exception("Cancellation ignored."); } catch (OperationCanceledException) { } RolledBack(f); Assert(!f.Trace.Contains("commit.attempt") && f.Outcomes.Single().Outcome == PublicationAuditOutcomeV1.Cancelled); });
        foreach (var mode in new[] { "NotApplied", "UnknownBefore", "UnknownAfter" })
            await Check("publish-commit-" + mode, async () =>
            {
                var f = PublishFixture(); f.CommitMode = mode;
                if (mode == "NotApplied") { var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.DependencyUnavailable); RolledBack(f); Assert(f.Outcomes.Single().Outcome == PublicationAuditOutcomeV1.Failed); }
                else { try { await f.Publisher().PublishAsync(f.Actor, f.Command, default); throw new Exception("Unknown commit reported certain."); } catch (PublicationCommitUncertainException) { } Assert(f.Outcomes.Count == 0 && f.Signals.Contains(PublicationOperationalSignalV1.CommitOutcomeUnknown)); Assert((f.Receipt is not null) == (mode == "UnknownAfter")); }
            });
        await Check("publish-replay-same-operation-no-source-stages-or-second-audit", async () =>
        {
            var f = PublishFixture(); var first = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(first.Succeeded); f.Trace.Clear();
            f.Command = new(f.Command.OperationId, Guid.NewGuid(), Guid.NewGuid(), f.Command.Scope, f.Command.RunId, f.Command.ExpectedRunRevision, f.Command.ExpectedSourceDigest, f.Command.AcknowledgedWarnings);
            var replay = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(replay.Receipt == first.Receipt && f.CommittedAudit.Count == 1 && !f.Trace.Contains("source.capture") && !f.Trace.Any(s => s.StartsWith("blob.put")));
        });
        await Check("publish-replay-conflict", async () =>
        { var f = PublishFixture(); f.PublicationLookupOverride = ReceiptLookupStatusV1.Conflict; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.IdempotencyConflict && !f.Trace.Contains("source.capture")); });
        await Check("publish-replay-tampered-receipt", async () =>
        { var f = PublishFixture(); await f.Publisher().PublishAsync(f.Actor, f.Command, default); f.Receipt = f.Receipt! with { ExpectedRunRevision = f.Command.ExpectedRunRevision + 1 }; var r = await f.Publisher().PublishAsync(f.Actor, f.Command, default); Assert(r.Issue == PublicationIssueV1.IntegrityMismatch && f.CommittedAudit.Count == 1); });
        await Check("read-authority-audit-commit-before-delivery-and-ownership", async () =>
        {
            var f = new FlowFixture(); var original = f.Blobs[(PublicationBlobKindV1.Projection, f.Visible!.Manifest.ProjectionDigest)].ToArray();
            var r = await Deliver(f); Assert(r.Issue is null && r.EventId is not null && f.SinkCalls == 1 && f.Deliveries.Single().SequenceEqual(original));
            Before(f, "authority.full", "blob.read.Manifest"); Before(f, "commit.applied", "sink.delivered");
            Assert(f.Blobs[(PublicationBlobKindV1.Projection, f.Visible.Manifest.ProjectionDigest)].SequenceEqual(original)); await LateRefused(f.LastView!);
            Assert(f.LastTransaction!.Disposed && f.LastFence!.Disposed && f.Outcomes.Count == 0);
        });
        await Check("read-entry-denied-no-store-blobs-references", async () =>
        { var f = new FlowFixture { DenyEntry = true }; await Deliver(f); NoContent(f); Assert(f.BeginCalls == 0 && !f.Trace.Contains("references.read") && !f.Trace.Any(s => s.StartsWith("blob."))); });
        await Check("read-full-authority-denied-before-blobs-references", async () =>
        { var f = new FlowFixture { DenyAccess = true }; await Deliver(f); NoContent(f); Assert(!f.Trace.Contains("references.read") && !f.Trace.Any(s => s.StartsWith("blob."))); });
        foreach (var state in new[] { PublicationLifecycleStateV1.SoftDeleted, PublicationLifecycleStateV1.Expired })
            await Check("read-lifecycle-" + state, async () => { var f = new FlowFixture(); f.Visible = f.Visible! with { State = state }; await Deliver(f); NoContent(f); Assert(!f.Trace.Any(s => s.StartsWith("blob."))); });
        await Check("read-request-manifest-mismatch-before-blobs", async () =>
        { var f = new FlowFixture(); f.Request = f.Request with { ExpectedManifestDigest = new string('f', 64) }; await Deliver(f); NoContent(f); Assert(!f.Trace.Any(s => s.StartsWith("blob."))); });
        await Check("read-corrupt-blob-refused", async () =>
        { var f = new FlowFixture { CorruptBlob = true }; var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.IntegrityMismatch && f.ReadReceipt is null); NoContent(f); });
        await Check("read-audit-outage-no-receipt-or-emit", async () =>
        { var f = new FlowFixture { FailAudit = true }; var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.DependencyUnavailable && f.ReadReceipt is null && f.CommittedAudit.Count == 0); NoContent(f); });
        await Check("read-current-overlay-changes-with-original-bytes-frozen", async () =>
        {
            var f = new FlowFixture("warned"); var reads = 0; f.Overlay = state => state.Source.Projection.TechnicalAppendices.ProtectedReferences.Select(v => new PublicationReferenceAvailabilityV1(v.Id, PublicationAvailabilityV1.Unavailable, ++reads == 1 ? PublicationReasonV1.Expired : PublicationReasonV1.Deleted, reads)).ToArray();
            var r = await Deliver(f); Assert(r.Issue is null && f.DeliveredOverlay!.Single().Reason == PublicationReasonV1.Deleted);
            var original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "warned.projection.json")); Assert(f.Deliveries.Single().SequenceEqual(original));
        });
        await Check("read-overlay-missing-or-wrong-record-refused", async () =>
        { var f = new FlowFixture(); f.Overlay = _ => []; var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.IntegrityMismatch && f.ReadReceipt is null); NoContent(f); });
        await Check("read-lifecycle-changes-during-audit-wait", async () =>
        { var f = new FlowFixture(); f.AuditHook = (state, _, _) => { state.Visible = state.Visible! with { ReadBlocked = true, LifecycleRevision = 2 }; return ValueTask.CompletedTask; }; await Deliver(f); NoContent(f); Assert(f.ReadReceipt is null && f.CommittedAudit.Count == 0); });
        await Check("read-revocation-after-audit-before-sink", async () =>
        { var f = new FlowFixture(); var r = await f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) => { f.LastView = view; f.DenyAccess = true; await view.DeliverAsync(ct); }, default); Assert(r.Issue is not null && f.ReadReceipt is not null && f.Outcomes.Count == 0); NoContent(f); await LateRefused(f.LastView!); });
        await Check("read-invocation-replay-one-audit", async () =>
        { var f = new FlowFixture(); var first = await Deliver(f); var second = await Deliver(f); Assert(first.EventId == second.EventId && f.CommittedAudit.Count == 1 && f.SinkCalls == 2); });
        await Check("read-invocation-conflict-no-content", async () =>
        { var f = new FlowFixture(); f.ReadLookupOverride = ReceiptLookupStatusV1.Conflict; var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.IdempotencyConflict); NoContent(f); });
        foreach (var mode in new[] { "NotApplied", "UnknownBefore", "UnknownAfter" })
            await Check("read-commit-" + mode, async () =>
            { var f = new FlowFixture { CommitMode = mode }; if (mode == "NotApplied") { var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.DependencyUnavailable && f.Outcomes.Single().Outcome == PublicationAuditOutcomeV1.Failed && f.Outcomes.Single().Reason == PublicationAuditReasonV1.DependencyUnavailable); } else { try { await Deliver(f); throw new Exception("Unknown read commit reported certain."); } catch (PublicationCommitUncertainException) { } Assert(f.Outcomes.Count == 0 && f.Signals.Contains(PublicationOperationalSignalV1.CommitOutcomeUnknown)); } NoContent(f); });
        await Check("read-deadline-minus-one-tick-delivers", async () =>
        { var f = new FlowFixture(); var r = await f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) => { f.LastView = view; f.Clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1)); await view.DeliverAsync(ct); }, default); Assert(r.Issue is null && f.SinkCalls == 1); });
        foreach (var extraTick in new[] { 0, 1 })
            await Check("read-exact-plus-deadline-" + extraTick, async () =>
            { var f = new FlowFixture(); var r = await f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) => { f.LastView = view; f.Clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(extraTick)); await view.DeliverAsync(ct); }, default); Assert(r.Issue is not null && f.ReadReceipt is not null && f.Outcomes.Count == 0); NoContent(f); await LateRefused(f.LastView!); });
        foreach (var rollback in new[] { false, true })
            await Check("read-ignored-cancellation-paused-callback-rollback-" + rollback, async () =>
            {
                var f = new FlowFixture(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var lateDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var read = f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) =>
                { f.LastView = view; entered.SetResult(); await resume.Task; try { await LateRefused(view); } finally { lateDone.SetResult(); } }, default).AsTask();
                await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10), rollback ? TimeSpan.FromSeconds(-30) : null);
                var result = await read.WaitAsync(TimeSpan.FromSeconds(2)); Assert(result.Issue is not null && f.LastTransaction!.Disposed && f.LastFence!.Disposed && f.ReadReceipt is not null && f.Outcomes.Count == 0); NoContent(f);
                resume.SetResult(); await lateDone.Task.WaitAsync(TimeSpan.FromSeconds(2)); NoContent(f);
            });
        await Check("read-caller-cancel-paused-callback-late-refusal", async () =>
        {
            var f = new FlowFixture(); using var cts = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var read = f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) => { f.LastView = view; entered.SetResult(); await resume.Task; await LateRefused(view); late.SetResult(); }, cts.Token).AsTask();
            await entered.Task; cts.Cancel(); try { await read; throw new Exception("Caller cancellation ignored."); } catch (OperationCanceledException) { }
            Assert(f.LastFence!.Disposed && f.LastTransaction!.Disposed && f.ReadReceipt is not null && f.Outcomes.Count == 0); resume.SetResult(); await late.Task.WaitAsync(TimeSpan.FromSeconds(2)); NoContent(f);
        });
        await Check("read-null-overlay-integrity-classification", async () =>
        { var f = new FlowFixture(); f.Overlay = _ => new PublicationReferenceAvailabilityV1[] { null! }; var r = await Deliver(f); Assert(r.Issue == PublicationIssueV1.IntegrityMismatch && f.Outcomes.Single().Reason == PublicationAuditReasonV1.IntegrityMismatch); NoContent(f); });
        await Check("read-no-new-protected-load-after-original-deadline", async () =>
        { var f = new FlowFixture(); f.OnTrace = (_, trace) => { if (trace == "blob.read.Manifest") f.Clock.Advance(TimeSpan.FromSeconds(10)); }; await Deliver(f); NoContent(f); Assert(!f.Trace.Contains("blob.read.Projection") && !f.Trace.Contains("blob.read.Score") && !f.Trace.Contains("references.read")); });
        foreach (var wait in new[] { "blob", "references", "audit" })
            await Check("read-expiry-cancels-paused-" + wait + "-wait", async () =>
            {
                var f = new FlowFixture(); using var caller = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                async ValueTask Pause(CancellationToken ct) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                if (wait == "blob") f.BlobHook = (_, _, ct) => Pause(ct);
                if (wait == "references") f.ReferenceHook = (_, ct) => Pause(ct);
                if (wait == "audit") f.AuditHook = (_, _, ct) => Pause(ct);
                var read = Deliver(f, caller.Token).AsTask(); await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10));
                try
                {
                    await read.WaitAsync(TimeSpan.FromMilliseconds(150));
                    Assert(f.LastFence!.Disposed && f.LastTransaction!.Disposed); NoContent(f);
                }
                catch (TimeoutException) { throw new Exception("Original deadline did not cancel paused " + wait + " port wait; fence remained held."); }
                finally { caller.Cancel(); try { await read.WaitAsync(TimeSpan.FromSeconds(1)); } catch (OperationCanceledException) { } }
            });
        await Check("read-pending-trusted-sink-cancels-before-delivery", async () =>
        {
            var f = new FlowFixture(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            f.SinkHook = async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
            var read = Deliver(f).AsTask(); await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10)); var r = await read;
            Assert(r.Issue is not null && f.ReadReceipt is not null && f.Outcomes.Count == 0); NoContent(f); await LateRefused(f.LastView!);
        });
        foreach (var disposition in new[] { "transaction", "fence" })
            await Check("publish-disposal-after-known-commit-" + disposition, async () =>
            {
                var f = PublishFixture(); f.FailTransactionDispose = disposition == "transaction"; f.FailFenceDispose = disposition == "fence";
                try { await f.Publisher().PublishAsync(f.Actor, f.Command, default); }
                catch (PublicationCommitUncertainException) { }
                Assert(f.Receipt is not null && f.CommittedAudit.Count == 1 && f.Outcomes.Count == 0 && f.LastFence!.Disposed && f.LastTransaction!.Disposed);
            });
        await Check("read-report-deadline-blocks-further-protected-loads", async () =>
        {
            var f = new FlowFixture(); f.Visible = f.Visible! with { ExpiresAtUtc = f.Clock.Utc + TimeSpan.FromSeconds(1) };
            f.OnTrace = (_, trace) => { if (trace == "blob.read.Manifest") f.Clock.Advance(TimeSpan.FromSeconds(1)); };
            await Deliver(f); NoContent(f); Assert(!f.Trace.Contains("blob.read.Projection") && !f.Trace.Contains("references.read"));
        });
        await Check("read-monotonic-rollback-ceiling-in-audit-wait", async () =>
        {
            var f = new FlowFixture(); using var caller = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            f.AuditHook = async (_, _, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
            var read = Deliver(f, caller.Token).AsTask(); await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(-30));
            try { var result = await read.WaitAsync(TimeSpan.FromMilliseconds(150)); Assert(result.Issue is not null && f.LastFence!.Disposed && f.LastTransaction!.Disposed); NoContent(f); }
            finally { caller.Cancel(); try { await read.WaitAsync(TimeSpan.FromSeconds(1)); } catch (OperationCanceledException) { } }
        });
        await Check("read-deleted-lifecycle-after-first-delivery-blocks-next", async () =>
        {
            var f = new FlowFixture(); var r = await f.Reader().ReadExactAsync(f.Actor, f.Request, async (view, ct) =>
            { f.LastView = view; await view.DeliverAsync(ct); f.Visible = f.Visible! with { State = PublicationLifecycleStateV1.SoftDeleted, LifecycleRevision = 2 }; await view.DeliverAsync(ct); }, default);
            Assert(r.Issue is not null && f.SinkCalls == 1 && f.CommittedAudit.Count == 1 && f.Outcomes.Count == 0); await LateRefused(f.LastView!);
        });
        await Check("read-current-overlay-defensive-ownership-before-sink", async () =>
        {
            var f = new FlowFixture(); PublicationReferenceAvailabilityV1[]? retainedAdapterArray = null;
            f.Overlay = state => retainedAdapterArray = state.Source.Projection.TechnicalAppendices.ProtectedReferences
                .Select(v => new PublicationReferenceAvailabilityV1(v.Id, PublicationAvailabilityV1.Unavailable, PublicationReasonV1.Expired, 1)).ToArray();
            f.SinkHook = (_, _) => { retainedAdapterArray![0] = new(Guid.NewGuid(), PublicationAvailabilityV1.Available, PublicationReasonV1.None, 2); return ValueTask.CompletedTask; };
            var r = await Deliver(f); Assert(r.Issue is null);
            var original = f.Source.Projection.TechnicalAppendices.ProtectedReferences.Single().Id;
            Assert(f.DeliveredOverlay!.Single().ReferenceId == original && f.DeliveredOverlay!.Single().Reason == PublicationReasonV1.Expired && f.DeliveredOverlay!.Single().Revision == 1);
        });
        foreach (var wait in new[] { "transaction.begin", "version.read", "database.time", "receipt.read.lookup", "authority.full" })
            await Check("read-admission-original-deadline-cancels-" + wait, async () =>
            {
                var f = new FlowFixture(); using var caller = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                f.MetadataHook = async (_, stage, ct) => { if (stage == wait) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); } };
                var read = Deliver(f, caller.Token).AsTask(); await entered.Task; f.Clock.Advance(TimeSpan.FromSeconds(10));
                try { var r = await read.WaitAsync(TimeSpan.FromMilliseconds(150)); Assert(r.Issue is not null && f.LastFence!.Disposed && (f.LastTransaction is null || f.LastTransaction.Disposed)); NoContent(f); }
                finally { caller.Cancel(); try { await read.WaitAsync(TimeSpan.FromSeconds(1)); } catch (OperationCanceledException) { } }
            });
        Console.WriteLine($"Scripted actual flow checks: {passed} passed, {failed.Count} failed; no actual PostgreSQL or source authority claim.");
        foreach (var value in failed) Console.WriteLine(value);
        return failed.Count;
    }
}
