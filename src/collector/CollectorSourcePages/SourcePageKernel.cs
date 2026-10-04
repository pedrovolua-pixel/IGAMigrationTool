using CollectorSafety;

namespace CollectorSourcePages;

/// <summary>Single-page scripted-port orchestration; no physical source or persistence adapter.</summary>
public sealed class SourcePageKernel
{
    private readonly ITrustedSourceAuthority _authority;
    private readonly ITrustedPageHistory _history;
    private readonly ISourcePageTransport _transport;
    private readonly ITrustedConnectionPermission _permissions;
    private readonly IWarningAuditReceiptWriter _warningAudit;
    private readonly ITrustedImpactGate _impact;
    private readonly INativeKeySemantics _nativeKeys;
    private readonly ITrustedReturnedValuePolicy _valuePolicy;
    private readonly TimeProvider _time;
    private int _active;

    public SourcePageKernel(ITrustedSourceAuthority authority, ITrustedPageHistory history, ISourcePageTransport transport,
        ITrustedConnectionPermission permissions, IWarningAuditReceiptWriter warningAudit, ITrustedImpactGate impact,
        INativeKeySemantics nativeKeys, ITrustedReturnedValuePolicy valuePolicy, TimeProvider timeProvider)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _warningAudit = warningAudit ?? throw new ArgumentNullException(nameof(warningAudit));
        _impact = impact ?? throw new ArgumentNullException(nameof(impact));
        _nativeKeys = nativeKeys ?? throw new ArgumentNullException(nameof(nativeKeys));
        _valuePolicy = valuePolicy ?? throw new ArgumentNullException(nameof(valuePolicy));
        _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<SourcePageResult> RunPageAsync(SourcePageRequest? request, CancellationToken cancellationToken)
    {
        if (!Valid(request)) return new(SourcePageOutcome.Refused, SourcePageReason.InvalidInput, default);
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return new(SourcePageOutcome.Refused, SourcePageReason.ConcurrentCall, default);
        try { return await RunOwnedAsync(new Attempt(_time, request!, cancellationToken)); }
        catch (Exception error) when (NonFatal(error)) { return new(SourcePageOutcome.Refused, SourcePageReason.PortFailure, default); }
        finally { Volatile.Write(ref _active, 0); }
    }

    private async ValueTask<SourcePageResult> RunOwnedAsync(Attempt state)
    {
        ISourcePageConnection? connection = null;
        ISourcePageReader? reader = null;
        SourcePageResult result;
        var transportOperation = false;
        try
        {
            state.StartTimeout();
            state.Gate();
            var request = state.Request;
            state.AuthorityResolutions++;
            var resolution = await _authority.ResolveAsync(request, state.Token);
            state.Gate();
            if (resolution?.State != SourceAuthorityState.Current || resolution.Binding is null)
                Stop(SourcePageOutcome.Refused, resolution?.State == SourceAuthorityState.Revoked ? SourcePageReason.AuthorityRevoked : SourcePageReason.AuthorityMissing);
            var binding = resolution!.Binding!;
            if (!request.Scope.SameAs(binding.Scope)) Stop(SourcePageOutcome.Refused, SourcePageReason.RegistryMismatch);
            state.HistoryLoads++;
            var history = await _history.LoadAsync(request.Identity, state.Token);
            state.Gate();
            if (!ValidHistory(request, binding, history)) Stop(SourcePageOutcome.Refused, SourcePageReason.HistoryMismatch);
            var pairReason = SourceQueryPairPreflight.Evaluate(request.Pair, binding);
            if (pairReason != SourcePageReason.None)
            {
                state.Gaps.AddRange(PlannedGaps(request.Pair, binding));
                Stop(SourcePageOutcome.Refused, pairReason);
            }
            state.ApplyHistory(history!);
            state.Gate();
            var remainingRows = state.MaximumRows - state.PriorRows;
            if (remainingRows <= 0) Stop(SourcePageOutcome.Partial, SourcePageReason.RowCap);
            if (request.RequestedPageSize > remainingRows) Stop(SourcePageOutcome.Refused, SourcePageReason.InvalidInput);
            if (state.PriorBytes >= request.Limits.MaximumTotalBytes) Stop(SourcePageOutcome.Partial, SourcePageReason.TotalByteCap);
            transportOperation = true;
            state.ConnectionOpens++;
            connection = await _transport.OpenAsync(binding, state.Token);
            transportOperation = false;
            state.Gate();
            if (connection is null || connection.Generation == Guid.Empty) Stop(SourcePageOutcome.Disconnected, SourcePageReason.TransportFailure);
            var generation = connection!.Generation;
            await CurrentAsync(state, binding, generation);
            state.Gate();
            state.PermissionProbes++;
            var permission = await _permissions.ProbeAsync(binding, generation, state.Token);
            state.Gate();
            var descriptor = request.Pair.Descriptor;
            if (permission is null || !request.Scope.SameAs(permission.Scope) || permission.PairId != request.Pair.PairId ||
                permission.Generation != generation || permission.MinimumReadSetId != descriptor.MinimumReadSetId ||
                permission.MinimumReadSetVersion != descriptor.MinimumReadSetVersion)
                Stop(SourcePageOutcome.Refused, SourcePageReason.PermissionBlocked);
            var decision = PermissionAttestation.Evaluate(permission!.Probe);
            if (decision.BlocksEvidenceQueries) Stop(SourcePageOutcome.Refused, SourcePageReason.PermissionBlocked);
            if (decision.RequiresWarningAndAudit)
            {
                state.WarningAudits++;
                var identity = new SourceWarningIdentity(request.Scope, request.Pair.PairId, request.PageOrdinal,
                    generation, descriptor.MinimumReadSetId, descriptor.MinimumReadSetVersion);
                var warning = await _warningAudit.CommitAsync(identity, state.Token);
                state.Gate();
                if (warning?.Identity is null || !identity.SameAs(warning.Identity)) Stop(SourcePageOutcome.Refused, SourcePageReason.WarningAuditMissing);
            }
            await ImpactAsync(state, binding, generation);
            state.Gate();
            if (connection.Generation != generation) Stop(SourcePageOutcome.Refused, SourcePageReason.PermissionBlocked);
            var command = new SourceBoundCommand(request, generation, state.Remaining());
            transportOperation = true;
            state.Executions++;
            reader = await connection.ExecuteAsync(command, state.Token);
            state.Gate();
            if (reader is null || !SchemaMatches(reader.Schema, request.Pair.Fields)) Stop(SourcePageOutcome.Refused, SourcePageReason.SchemaMismatch);
            transportOperation = false;
            var rows = new List<SourceMinimizedRow>();
            var keyOrdinal = request.Pair.Fields.ToList().FindIndex(f => f.Name == descriptor.StableKey);
            var uidOrdinal = request.Pair.Fields.ToList().FindIndex(f => f.Name == request.Pair.ApprovedUidField);
            SourceNativeValue? priorKey = request.Continuation;
            SourceRowReference? priorReference = history!.Previous is null ? null : new(history.Previous.Identity, history.Previous.LastRowOrdinal);
            var terminal = false;
            var extractedAtUtc = _time.GetUtcNow();
            for (var index = 0; index < request.RequestedPageSize; index++)
            {
                state.Gate();
                await ImpactAsync(state, binding, generation);
                state.Gate();
                if (connection.Generation != generation) Stop(SourcePageOutcome.Refused, SourcePageReason.PermissionBlocked);
                transportOperation = true;
                state.ReadCalls++;
                var row = await reader!.ReadAsync(state.Token);
                transportOperation = false;
                state.Gate();
                if (row is null) { terminal = true; break; }
                state.RowsObserved++;
                var measurable = true;
                foreach (var observed in row.Values)
                {
                    if (observed is null || !observed.TryObservedBytes(out var observedBytes)) { measurable = false; continue; }
                    // Payload has already arrived from the bounded reader. Refusing it
                    // must not erase measurable bytes or invent an unknown length.
                    state.BytesObserved = checked(state.BytesObserved + observedBytes);
                }
                if (!measurable) Stop(SourcePageOutcome.Refused, SourcePageReason.NativeValueInvalid);
                if (row.RowOrdinal != index || row.Values.Count != request.Pair.Fields.Count) Stop(SourcePageOutcome.Refused, SourcePageReason.SchemaMismatch);
                foreach (var (value, ordinal) in row.Values.Select((value, ordinal) => (value, ordinal)))
                {
                    state.Gate();
                    if (!value.TryBytes(request.Pair.Fields[ordinal], out var bytes)) Stop(SourcePageOutcome.Refused, SourcePageReason.NativeValueInvalid);
                    if (bytes > request.Limits.MaximumFieldBytes) Stop(SourcePageOutcome.Partial, SourcePageReason.FieldByteCap);
                }
                if (state.BytesObserved > request.Limits.MaximumPageBytes) Stop(SourcePageOutcome.Partial, SourcePageReason.PageByteCap);
                if (checked(state.PriorBytes + state.BytesObserved) > request.Limits.MaximumTotalBytes) Stop(SourcePageOutcome.Partial, SourcePageReason.TotalByteCap);
                var minimized = new List<SourceMinimizedField>();
                for (var fieldOrdinal = 0; fieldOrdinal < request.Pair.Fields.Count; fieldOrdinal++)
                {
                    state.Gate();
                    var field = request.Pair.Fields[fieldOrdinal]; var value = row.Values[fieldOrdinal];
                    state.ValueClassifications++;
                    var classified = _valuePolicy.Classify(binding, fieldOrdinal, field, value!);
                    state.Gate();
                    if (classified is null || classified.Binding is null || !binding.SameAs(classified.Binding) || classified.FieldOrdinal != fieldOrdinal ||
                        !Enum.IsDefined(classified.Disposition)) Stop(SourcePageOutcome.Refused, SourcePageReason.RegistryMismatch);
                    var disposition = classified!.Disposition;
                    if (disposition is FieldDisposition.Prohibited or FieldDisposition.Unclassified ||
                        fieldOrdinal == keyOrdinal && disposition != FieldDisposition.Included) Stop(SourcePageOutcome.Quarantined, SourcePageReason.ClassifiedContent);
                    minimized.Add(new(new SourceProvenance(request, binding, index, fieldOrdinal, extractedAtUtc, disposition),
                        disposition, disposition == FieldDisposition.Included ? value : null));
                }
                var key = minimized[keyOrdinal].Value!;
                if (Compare(binding, SourceNativePurpose.Paging, keyOrdinal, request.Pair.Fields[keyOrdinal], key, key) != SourceNativeOrder.Equal)
                    Stop(SourcePageOutcome.Refused, SourcePageReason.NativeOrderUnsupported);
                var currentReference = new SourceRowReference(request.Identity, index);
                if (priorKey is not null)
                {
                    var comparison = Compare(binding, SourceNativePurpose.Paging, keyOrdinal, request.Pair.Fields[keyOrdinal], priorKey, key);
                    if (comparison != SourceNativeOrder.Less)
                    {
                        state.Conflicts.Add(new(comparison == SourceNativeOrder.Equal ? SourceConflictKind.PagingKeyTie : SourceConflictKind.PagingOrder,
                            priorReference!, currentReference));
                        Stop(SourcePageOutcome.Refused, comparison == SourceNativeOrder.Equal ? SourcePageReason.PagingKeyConflict : SourcePageReason.PagingOrderViolation);
                    }
                }
                if (uidOrdinal >= 0 && minimized[uidOrdinal].Value is { Kind: not SourceNativeKind.SqlNull } uid)
                {
                    foreach (var earlier in rows)
                    {
                        var oldUid = earlier.Fields[uidOrdinal].Value;
                        if (oldUid is not null && oldUid.Kind != SourceNativeKind.SqlNull &&
                            Compare(binding, SourceNativePurpose.ObjectIdentity, uidOrdinal, request.Pair.Fields[uidOrdinal], oldUid, uid) == SourceNativeOrder.Equal)
                            state.Conflicts.Add(new(SourceConflictKind.ObjectUid, new(request.Identity, earlier.RowOrdinal), currentReference));
                    }
                }
                state.Gate();
                rows.Add(new(index, minimized)); priorKey = key; priorReference = currentReference;
            }
            await CurrentAsync(state, binding, generation);
            await ImpactAsync(state, binding, generation);
            state.Gate();
            if (connection.Generation != generation) Stop(SourcePageOutcome.Refused, SourcePageReason.PermissionBlocked);
            var cumulativeRows = checked(state.PriorRows + state.RowsObserved);
            var rowCap = !terminal && cumulativeRows >= state.MaximumRows;
            var page = new SourcePage(request, rows, state.Conflicts, terminal, terminal || rowCap ? null : priorKey,
                state.BytesObserved, extractedAtUtc);
            var receipt = new SourcePageReceipt(request, binding, page, history.OriginalStartedAtUtc,
                cumulativeRows, checked(state.PriorBytes + state.BytesObserved), rowCap);
            result = new(rowCap ? SourcePageOutcome.Partial : SourcePageOutcome.PageReady,
                rowCap ? SourcePageReason.RowCap : SourcePageReason.None, state.Counters, page, receipt, state.Conflicts);
        }
        catch (PageStop stopped) { result = state.Result(stopped.Outcome, stopped.Reason); }
        catch (OperationCanceledException) { result = state.CanceledResult(transportOperation); }
        catch (OverflowException) { result = state.Result(SourcePageOutcome.Refused, SourcePageReason.InvalidInput); }
        catch (Exception error) when (NonFatal(error))
        {
            result = state.Result(transportOperation ? SourcePageOutcome.Disconnected : SourcePageOutcome.Refused,
            transportOperation ? SourcePageReason.TransportFailure : SourcePageReason.PortFailure);
        }
        var disposalFailed = false;
        try { if (reader is not null) await reader.DisposeAsync(); }
        catch (Exception error) when (NonFatal(error)) { disposalFailed = true; }
        try { if (connection is not null) await connection.DisposeAsync(); }
        catch (Exception error) when (NonFatal(error)) { disposalFailed = true; }
        if (disposalFailed) result = state.Result(SourcePageOutcome.Disconnected, SourcePageReason.TransportFailure);
        else
        {
            // Admission is provisional until cleanup ends; a slow dispose cannot renew the age.
            try { state.Gate(); }
            catch (PageStop stopped) { result = state.Result(stopped.Outcome, stopped.Reason); }
        }
        state.Dispose();
        return result;
    }

    private async ValueTask CurrentAsync(Attempt state, SourceRegistryBinding binding, Guid generation)
    {
        state.Gate(); state.AuthorityRevalidations++;
        var current = await _authority.RevalidateAsync(binding, generation, state.Token);
        state.Gate();
        if (current != SourceAuthorityState.Current) Stop(SourcePageOutcome.Refused,
            current == SourceAuthorityState.Revoked ? SourcePageReason.AuthorityRevoked : SourcePageReason.RegistryMismatch);
    }
    private async ValueTask ImpactAsync(Attempt state, SourceRegistryBinding binding, Guid generation)
    {
        state.Gate(); state.ImpactChecks++;
        var impact = await _impact.CheckAsync(binding, generation, state.Token);
        state.Gate();
        if (impact != SourceImpactState.Continue) Stop(SourcePageOutcome.Partial,
            impact == SourceImpactState.Stop ? SourcePageReason.ImpactStopped : SourcePageReason.ImpactUnknown);
    }
    private SourceNativeOrder Compare(SourceRegistryBinding binding, SourceNativePurpose purpose, int ordinal,
        QueryPackField field, SourceNativeValue left, SourceNativeValue right)
    {
        var receipt = _nativeKeys.Compare(binding, purpose, ordinal, field, left, right);
        if (receipt is null || receipt.Binding is null || !binding.SameAs(receipt.Binding) || receipt.Purpose != purpose || receipt.FieldOrdinal != ordinal)
            Stop(SourcePageOutcome.Refused, SourcePageReason.RegistryMismatch);
        if (receipt!.Order == SourceNativeOrder.Unsupported || !Enum.IsDefined(receipt.Order)) Stop(SourcePageOutcome.Refused, SourcePageReason.NativeOrderUnsupported);
        return receipt.Order;
    }
    private static bool SchemaMatches(SourceReturnedSchema? schema, IReadOnlyList<QueryPackField> fields) =>
        schema?.Fields.Count == fields.Count && schema.Fields.Select((f, i) => f is not null && f.Name == fields[i].Name &&
            f.SqlType == fields[i].SqlType && f.Nullable == fields[i].Nullable).All(match => match);
    private static bool Valid(SourcePageRequest? request) => request?.Scope is not null && request.Scope.Valid &&
        request.Pair is not null && request.Limits is not null && request.Limits.Valid && request.PageOrdinal >= 0 &&
        Enum.IsDefined(request.Phase) && request.RequestedPageSize > 0 && request.RequestedPageSize <= request.Limits.MaximumPageSize &&
        request.RequestedPageSize <= request.Pair.Descriptor.MaximumPageSize &&
        (request.Phase == SourceQueryPhase.First ? request.PageOrdinal == 0 && request.Continuation is null : request.PageOrdinal > 0 && request.Continuation is not null);
    private static bool ValidHistory(SourcePageRequest request, SourceRegistryBinding binding, SourceHistoryResolution? history)
    {
        if (history is null || history.OriginalStartedAtUtc < DateTimeOffset.UnixEpoch) return false;
        if (request.Phase == SourceQueryPhase.First) return history.State == SourceHistoryState.Initial && history.Previous is null;
        var previous = history.Previous;
        return history.State == SourceHistoryState.Previous && previous is not null && !previous.Terminal && !previous.RowCap &&
            previous.NextContinuation is not null && request.Continuation!.SameAs(previous.NextContinuation) && previous.LastRowOrdinal >= 0 &&
            previous.Identity.PageOrdinal < long.MaxValue && request.PageOrdinal == previous.Identity.PageOrdinal + 1 &&
            request.Scope.SameAs(previous.Identity.Scope) && request.Pair.SameAs(previous.Pair) && binding.SameAs(previous.Binding) &&
            request.Limits.Narrows(previous.Limits) && history.OriginalStartedAtUtc == previous.OriginalStartedAtUtc &&
            previous.CumulativeRows > 0 && previous.CumulativeBytes >= 0;
    }
    private static IEnumerable<SourcePlannedGap> PlannedGaps(SourceQueryPair pair, SourceRegistryBinding binding) =>
        pair.Fields.Select((field, i) => new SourcePlannedGap(i, FieldMinimizer.Evaluate(binding.Expected.Policy,
            new(new(pair.Descriptor.Category, field.Name), field.Classification, null)).Disposition)).Where(g => g.Disposition != FieldDisposition.Included);
    private static bool NonFatal(Exception error) => error is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
    private static void Stop(SourcePageOutcome outcome, SourcePageReason reason) => throw new PageStop(outcome, reason);
    private sealed class PageStop(SourcePageOutcome outcome, SourcePageReason reason) : Exception
    {
        internal SourcePageOutcome Outcome { get; } = outcome;
        internal SourcePageReason Reason { get; } = reason;
    }
    private sealed class Attempt(TimeProvider time, SourcePageRequest request, CancellationToken caller) : IDisposable
    {
        private readonly DateTimeOffset _entryUtc = time.GetUtcNow();
        private readonly long _entryTimestamp = time.GetTimestamp();
        private TimeSpan _ceiling = request.Limits.CommandTimeout;
        private DateTimeOffset? _originalStart;
        private TimeSpan _duration;
        private CancellationTokenSource? _timeout;
        private CancellationTokenSource? _linked;
        internal SourcePageRequest Request => request;
        internal CancellationToken Token => _linked?.Token ?? caller;
        internal long AuthorityResolutions, AuthorityRevalidations, HistoryLoads, ConnectionOpens, PermissionProbes,
            WarningAudits, ImpactChecks, Executions, ReadCalls, RowsObserved, BytesObserved, ValueClassifications;
        internal long PriorRows, PriorBytes, MaximumRows;
        internal List<SourceConflict> Conflicts { get; } = [];
        internal List<SourcePlannedGap> Gaps { get; } = [];
        internal SourcePageCounters Counters => new(AuthorityResolutions, AuthorityRevalidations, HistoryLoads, ConnectionOpens,
            PermissionProbes, WarningAudits, ImpactChecks, Executions, ReadCalls, RowsObserved, BytesObserved, ValueClassifications);
        internal void StartTimeout()
        {
            _timeout = new(request.Limits.CommandTimeout, time);
            _linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _timeout.Token);
        }
        internal void ApplyHistory(SourceHistoryResolution history)
        {
            if (history.OriginalStartedAtUtc > _entryUtc) Stop(SourcePageOutcome.Refused, SourcePageReason.HistoryMismatch);
            _originalStart = history.OriginalStartedAtUtc;
            _duration = request.Limits.MaximumDuration < request.Pair.Descriptor.MaximumDuration ? request.Limits.MaximumDuration : request.Pair.Descriptor.MaximumDuration;
            MaximumRows = Math.Min(request.Limits.MaximumRows, request.Pair.Descriptor.MaximumRows);
            PriorRows = history.Previous?.CumulativeRows ?? 0; PriorBytes = history.Previous?.CumulativeBytes ?? 0;
            var age = _entryUtc - _originalStart.Value;
            _ceiling = Minimum(_ceiling, _duration - age, request.Limits.Retention - age);
            Gate();
            _timeout!.CancelAfter(Remaining());
        }
        internal TimeSpan Remaining() => _ceiling - time.GetElapsedTime(_entryTimestamp, time.GetTimestamp());
        internal void Gate()
        {
            if (caller.IsCancellationRequested) Stop(SourcePageOutcome.Canceled, SourcePageReason.None);
            if (_originalStart.HasValue && time.GetUtcNow() - _originalStart.Value >= request.Limits.Retention)
                Stop(SourcePageOutcome.Expired, SourcePageReason.Retention);
            if (_originalStart.HasValue && time.GetUtcNow() - _originalStart.Value >= _duration || Remaining() <= TimeSpan.Zero || Token.IsCancellationRequested)
                Stop(SourcePageOutcome.TimedOut, SourcePageReason.Deadline);
        }
        internal SourcePageResult CanceledResult(bool transportOperation)
        {
            try { Gate(); }
            catch (PageStop stop) { return Result(stop.Outcome, stop.Reason); }
            return Result(transportOperation ? SourcePageOutcome.Disconnected : SourcePageOutcome.Refused,
                transportOperation ? SourcePageReason.TransportFailure : SourcePageReason.PortFailure);
        }
        internal SourcePageResult Result(SourcePageOutcome outcome, SourcePageReason reason) => new(outcome, reason, Counters, conflicts: Conflicts, gaps: Gaps);
        private static TimeSpan Minimum(TimeSpan a, TimeSpan b, TimeSpan c) => a < b ? a < c ? a : c : b < c ? b : c;
        public void Dispose() { _linked?.Dispose(); _timeout?.Dispose(); }
    }
}
