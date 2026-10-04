using System.Collections.Immutable;

namespace SyntheticMcp;

/// <summary>Dependency-free synthetic boundary. No transport or production adapter is composed here.</summary>
public sealed class McpHarness
{
    private readonly IPublicationReader source;
    private readonly IReadPolicy policy;
    private readonly IMcpAudit audit;
    private readonly IMonotonicClock clock;
    private readonly object fence = new();
    private readonly LocalLimits limits = new();
    private readonly CursorRegistry cursors = new();
    private TimeSpan highWatermark;

    public McpHarness(IPublicationReader source, IReadPolicy policy, IMcpAudit audit, IMonotonicClock clock)
    {
        this.source = source;
        this.policy = policy;
        this.audit = audit;
        this.clock = clock;
    }

    public async Task<McpResult> ReadAsync(Caller caller, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        var invocation = new Invocation(caller);
        ReadRequest? request = null;
        ReadGrant? grant = null;
        Action? release = null;
        var policyFailure = false;
        TimeSpan start = default;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            start = Now();
            invocation.Start = start;
            request = RequestParser.Parse(bytes);
            invocation.Kind = request?.Kind;
            if (ValidIdentity(caller) && request is not null)
            {
                try { grant = policy.GetGrant(caller, request); }
                catch { Signal(OperationalFailure.PolicyUnavailable, caller.CorrelationId); policyFailure = true; }
                if (grant is not null && !ValidBinding(caller, request, grant))
                { grant = null; policyFailure = true; }
            }
            lock (fence)
            {
                var now = Now();
                release = limits.Acquire(caller, grant, now);
                if (grant is not null) cursors.Purge(grant, now);
            }
            if (release is null) return Finish(invocation, McpOutcome.Limited, grant);
            CheckDeadline(start, token, deadline.Token);
            if (policyFailure) return Finish(invocation, McpOutcome.DependencyUnavailable);
            if (request is null) return Finish(invocation, McpOutcome.InvalidRequest);
            if (grant is null) return Finish(invocation, McpOutcome.Unavailable);
            if (!ValidFields(grant)) return Finish(invocation, McpOutcome.DependencyUnavailable);
            if (!Allowed(grant)) return Finish(invocation, McpOutcome.Unavailable, grant);

            var position = 0;
            TimeSpan? expiry = null;
            if (request.Cursor is not null)
            {
                CursorRegistry.Entry? entry;
                lock (fence) entry = cursors.Resolve(request.Cursor, grant, request.PageSize, Now());
                if (entry is null) return Finish(invocation, McpOutcome.Unavailable, grant);
                position = entry.Position;
                expiry = entry.Expires;
            }
            ManifestSource? manifestSource;
            try
            {
                manifestSource = await source.ReadManifestAsync(grant.Scope, request.AssessmentId, request.ReportVersionId, deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { Signal(OperationalFailure.SourceUnavailable, caller.CorrelationId); return Finish(invocation, McpOutcome.DependencyUnavailable, grant); }
            CheckDeadline(start, token, deadline.Token);
            if (manifestSource is null) return Finish(invocation, McpOutcome.Unavailable, grant);
            var manifest = PublicationCodec.Validate(manifestSource, grant);
            if (manifest is null) return Finish(invocation, McpOutcome.Unavailable, grant);
            var selected = manifest.Items.Where(i => i.Kind == request.Kind && grant.Categories.Contains(i.Category)).OrderBy(i => i.ItemId, StringComparer.Ordinal).ToArray();
            if (selected.Length == 0 && manifest.Items.Any(i => i.Kind == request.Kind)) return Finish(invocation, McpOutcome.Unavailable, grant);
            if (position < 0 || position > selected.Length) return Finish(invocation, McpOutcome.Unavailable, grant);
            var page = selected.Skip(position).Take(request.PageSize).ToArray();
            var projected = ImmutableArray.CreateBuilder<ProjectedItem>();
            foreach (var item in page)
            {
                CheckDeadline(start, token, deadline.Token);
                ImmutableArray<byte>? payload;
                try
                {
                    payload = await source.ReadItemAsync(grant.Scope, request.ReportVersionId, request.Kind, item.ItemId, deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { Signal(OperationalFailure.SourceUnavailable, caller.CorrelationId); return Finish(invocation, McpOutcome.DependencyUnavailable, grant); }
                CheckDeadline(start, token, deadline.Token);
                if (payload is null) return Finish(invocation, McpOutcome.Unavailable, grant);
                var content = PublicationCodec.ReadPayload(payload.Value, item, manifest);
                if (content is null) return Finish(invocation, McpOutcome.Unavailable, grant);
                ReferenceOverlay? overlay = null;
                if (request.Kind == ResourceKind.ProtectedReferences)
                {
                    try { overlay = policy.GetReferenceAvailability(grant, item.ItemId); }
                    catch { Signal(OperationalFailure.PolicyUnavailable, caller.CorrelationId); return Finish(invocation, McpOutcome.DependencyUnavailable, grant); }
                }
                var row = Projection.Project(content.Value, item, manifest, grant, overlay);
                if (row is null) return Finish(invocation, McpOutcome.DependencyUnavailable, grant);
                projected.Add(row);
            }
            CheckDeadline(start, token, deadline.Token);
            var nextPosition = position + page.Length;
            var hasNext = nextPosition < selected.Length;
            McpResult? result = null;
            bool committed;
            try
            {
                committed = policy.TryCommit(grant, () =>
                {
                    lock (fence)
                    {
                        if (result is not null) return;
                        CheckDeadline(start, token, deadline.Token);
                        var now = Now();
                        CursorRegistry.Reservation? reservation = null;
                        try
                        {
                            if (hasNext)
                            {
                                reservation = cursors.Reserve(grant, request.PageSize, nextPosition, expiry ?? now + TimeSpan.FromSeconds(300), now);
                                if (reservation is null) { result = Finish(invocation, McpOutcome.Limited, grant); return; }
                            }
                            var envelope = Projection.Envelope(manifest, request.Kind, projected, reservation?.Handle);
                            if (envelope.IsDefaultOrEmpty || envelope.Length > McpContract.MaxResponseBytes)
                            { result = Finish(invocation, McpOutcome.DependencyUnavailable, grant); return; }
                            CheckDeadline(start, token, deadline.Token);
                            var returned = projected.SelectMany(p => p.ReturnedFields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
                            var redacted = projected.SelectMany(p => p.RedactedFields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
                            if (!Complete(invocation, McpOutcome.Success, grant, returned, redacted))
                            { result = McpResult.Denied(McpOutcome.DependencyUnavailable); return; }
                            if (reservation is not null) cursors.Publish(reservation);
                            result = new McpResult(McpOutcome.Success, envelope, reservation?.Handle);
                        }
                        finally { cursors.Release(reservation); }
                    }
                });
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                lock (fence) cursors.Remove(result?.NextCursor);
                Signal(OperationalFailure.PolicyUnavailable, caller.CorrelationId);
                return Finish(invocation, McpOutcome.DependencyUnavailable);
            }
            if (!committed || result is null)
            {
                lock (fence) cursors.Remove(result?.NextCursor);
                return Finish(invocation, McpOutcome.Unavailable);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return Finish(invocation, token.IsCancellationRequested ? McpOutcome.Cancelled : McpOutcome.DependencyUnavailable, grant);
        }
        catch (ClockException)
        {
            Signal(OperationalFailure.ClockUnavailable, caller.CorrelationId);
            return Finish(invocation, McpOutcome.DependencyUnavailable, grant);
        }
        catch
        {
            return Finish(invocation, McpOutcome.DependencyUnavailable, grant);
        }
        finally
        {
            if (release is not null) lock (fence) release();
        }
    }

    private TimeSpan Now()
    {
        lock (fence)
        {
            TimeSpan value;
            try { value = clock.Elapsed; }
            catch { throw new ClockException(); }
            if (value < TimeSpan.Zero) throw new ClockException();
            if (value > highWatermark) highWatermark = value;
            return highWatermark;
        }
    }
    private void CheckDeadline(TimeSpan start, CancellationToken caller, CancellationToken deadline)
    {
        caller.ThrowIfCancellationRequested();
        deadline.ThrowIfCancellationRequested();
        if (Now() - start >= TimeSpan.FromSeconds(5)) throw new OperationCanceledException(deadline);
    }
    private static bool ValidIdentity(Caller caller) => caller.Kind is IdentityKind.NamedUser or IdentityKind.Service && PublicationCodec.IsId(caller.IdentityId);
    private static bool ValidBinding(Caller caller, ReadRequest request, ReadGrant grant) => grant.IdentityKind == caller.Kind && grant.IdentityId == caller.IdentityId &&
        grant.Kind == request.Kind && grant.AssessmentId == request.AssessmentId && grant.ReportVersionId == request.ReportVersionId && grant.Revision >= 0 &&
        grant.Scope is not null && PublicationCodec.IsId(grant.Scope.CustomerId) && PublicationCodec.IsId(grant.Scope.ProjectId) && PublicationCodec.IsId(grant.Scope.EnvironmentId) &&
        grant.ManifestDigest is { Length: 64 } && grant.ManifestDigest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidFields(ReadGrant grant) => grant.Categories is not null && grant.Fields is not null &&
        grant.Categories.All(Enum.IsDefined) && grant.Fields.IsSubsetOf(Projection.Fields(grant.Kind));
    private static bool Allowed(ReadGrant grant) => grant.ActiveIdentity && grant.ActiveAssignment && grant.CustomerMcpPolicy && grant.ResourceAvailable &&
        !grant.ReadBlocked && grant.ActionAllowed && grant.Categories.Count > 0;
    private McpResult Finish(Invocation invocation, McpOutcome outcome, ReadGrant? grant = null) =>
        McpResult.Denied(Complete(invocation, outcome, grant, [], []) ? outcome : McpOutcome.DependencyUnavailable);
    private bool Complete(Invocation invocation, McpOutcome outcome, ReadGrant? grant, ImmutableArray<string> returned, ImmutableArray<string> redacted)
    {
        if (Interlocked.Exchange(ref invocation.Completed, 1) != 0) return invocation.AuditSucceeded;
        long elapsed;
        var clockAvailable = true;
        try { elapsed = Math.Max(0, (long)(Now() - invocation.Start).TotalMilliseconds); }
        catch { elapsed = 0; clockAvailable = false; outcome = McpOutcome.DependencyUnavailable; Signal(OperationalFailure.ClockUnavailable, invocation.Caller.CorrelationId); }
        var caller = invocation.Caller;
        var safeIdentity = Enum.IsDefined(caller.Kind) && PublicationCodec.IsId(caller.IdentityId);
        var authorizedScope = grant is not null && ValidFields(grant) && Allowed(grant) ? grant.Scope : null;
        try
        {
            invocation.AuditSucceeded = audit.Complete(new McpAuditEvent(safeIdentity ? caller.Kind : null, safeIdentity ? caller.IdentityId : null,
                authorizedScope, invocation.Kind, outcome, caller.CorrelationId, elapsed, returned, redacted));
        }
        catch { invocation.AuditSucceeded = false; }
        if (!invocation.AuditSucceeded) Signal(OperationalFailure.AuditUnavailable, caller.CorrelationId);
        return invocation.AuditSucceeded && clockAvailable;
    }
    private void Signal(OperationalFailure failure, Guid correlation)
    {
        try { audit.OperationalFailure(failure, correlation); } catch { /* No untrusted exception escapes the boundary. */ }
    }
    private sealed class Invocation(Caller caller)
    {
        internal readonly Caller Caller = caller;
        internal TimeSpan Start;
        internal ResourceKind? Kind;
        internal int Completed;
        internal bool AuditSucceeded;
    }
    private sealed class ClockException : Exception;
}
