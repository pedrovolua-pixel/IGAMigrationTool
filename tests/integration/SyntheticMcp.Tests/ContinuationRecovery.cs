using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

// The contract/fixture oracle was pinned before implementation inspection; see the adjacent note.
internal static class ContinuationRecovery
{
    private const string Digest = "613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(2);
    private static readonly Scope FixtureScope = new("syn-customer-a", "syn-project-a", "syn-environment-a");
    private static readonly Dictionary<ResourceKind, string[]> Allowed = new()
    {
        [ResourceKind.Findings] = ["itemId", "category", "summary", "severity", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "summary", "options", "reviewLabel"]
    };
    private static readonly Dictionary<ResourceKind, string[]> AllFields = new()
    {
        [ResourceKind.Findings] = ["itemId", "category", "title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "summary", "options", "priority", "effort", "reviewLabel"]
    };
    private sealed record ItemRead(Scope Scope, string Report, ResourceKind Kind, string Id);
    private sealed record ManifestRead(Scope Scope, string Assessment, string Report);
    private sealed record Commit(long Revision, bool Executed);
    private enum AuditMode { Accept, Reject, Throw }
    private enum Failure { Cancel, Deadline, AuditFalse, AuditThrow }

    internal static async Task RunAsync()
    {
        var assertions = 0;
        var scenarios = 0;
        var invocations = 0;
        void Check(bool value, string label)
        {
            assertions++;
            if (!value) throw new InvalidOperationException("FAIL cycle04 continuation " + label);
        }
        var oracle = new Oracle(Check);
        foreach (var identity in new[] { IdentityKind.NamedUser, IdentityKind.Service })
        {
            foreach (var kind in Allowed.Keys)
            {
                await Overlap(identity, kind);
                foreach (var failure in Enum.GetValues<Failure>()) await Recovery(identity, kind, failure);
                await Revision(identity, kind);
            }
        }
        Check(scenarios == 24 && invocations == 112, "exact scenario/invocation partition");
        Console.WriteLine($"PASS independent cycle04 continuation: {scenarios} scenarios, {invocations} invocations, {assertions} assertions; selected-item overlap/recovery/revision; P1D-T06/07/09/10 synthetic fixture only");

        World NewWorld(IdentityKind identity, ResourceKind kind) => new(oracle, identity, kind);
        Task<McpResult> Invoke(World world, Caller caller, string? cursor = null, CancellationToken token = default)
        {
            invocations++;
            return world.Host.ReadAsync(caller, Request(world.Kind, cursor), token);
        }
        async Task<string> First(World world)
        {
            var caller = world.Caller();
            var result = await Invoke(world, caller).WaitAsync(Bound);
            Success(world, caller, result, 0, Allowed[world.Kind], true);
            Reads(world, 0, 0, oracle.Id(world.Kind, 0));
            return result.NextCursor!;
        }
        void Reads(World world, int manifestStart, int itemStart, params string[] ids)
        {
            var manifests = world.Source.Manifests.ToArray().Skip(manifestStart).ToArray();
            var items = world.Source.Items.ToArray().Skip(itemStart).ToArray();
            Check(manifests.SequenceEqual(ids.Select(_ => new ManifestRead(FixtureScope, "syn-assessment-a", "syn-report-a"))), "exact manifest read bindings/count");
            Check(items.SequenceEqual(ids.Select(id => new ItemRead(FixtureScope, "syn-report-a", world.Kind, id))), "exact selected item calls/no other content");
        }
        void Audit(World world, Caller caller, McpOutcome outcome, bool completed, bool generic = false)
        {
            var attempts = world.Audit.Attempts.Where(a => a.CorrelationId == caller.CorrelationId).ToArray();
            var events = world.Audit.Events.Where(a => a.CorrelationId == caller.CorrelationId).ToArray();
            Check(attempts.Length == 1 && events.Length == (completed ? 1 : 0), "one audit attempt/distinct durable completion");
            var audit = attempts.Single();
            Check(audit.Outcome == outcome && audit.IdentityKind == world.Identity && audit.IdentityId == world.IdentityId && audit.ResourceKind == world.Kind && audit.ElapsedMilliseconds >= 0, "typed trusted audit context");
            if (generic) Check(audit.Scope is null && audit.ReturnedFields.IsEmpty && audit.RedactedFields.IsEmpty, "stale generic scope-free empty-field audit");
        }
        void Success(World world, Caller caller, McpResult result, int ordinal, string[] fields, bool hasNext)
        {
            Check(result.Outcome == McpOutcome.Success && !result.Envelope.IsDefaultOrEmpty, "successful envelope");
            Check(hasNext ? Opaque(result.NextCursor) : result.NextCursor is null, "opaque successor/final null");
            if (hasNext) Check(result.NextCursor != Digest, "handle is not manifest digest");
            var actual = JsonNode.Parse(result.Envelope.AsSpan())!.AsObject();
            var expected = new JsonObject
            {
                ["contractVersion"] = "synthetic-published-health-read-v1", ["resourceKind"] = world.Kind.ToString(),
                ["manifestDigest"] = Digest, ["bindings"] = oracle.Bindings.DeepClone(),
                ["items"] = new JsonArray(oracle.Row(world.Kind, ordinal, fields)), ["nextCursor"] = result.NextCursor
            };
            Check(JsonNode.DeepEquals(actual, expected), "exact ordinal item/field set/typed data/original scalar bindings/digest/envelope schema");
            Audit(world, caller, McpOutcome.Success, true);
            var audit = world.Audit.Attempts.Single(a => a.CorrelationId == caller.CorrelationId);
            Check(audit.Scope == FixtureScope && audit.ReturnedFields.SequenceEqual(fields.Order(StringComparer.Ordinal)) && audit.RedactedFields.SequenceEqual(AllFields[world.Kind].Except(fields).Order(StringComparer.Ordinal)), "exact success scope/returned/redacted schema fields");
        }
        void Denied(McpResult result, McpOutcome outcome) => Check(result.Outcome == outcome && result.Envelope.IsDefaultOrEmpty && result.NextCursor is null, "terminal failure exposes neither content nor new handle");
        void Totals(World world, int evaluations, int commits, int attempts, int events)
        {
            Check(world.Policy.Grants.Count == evaluations && world.Policy.Commits.Count == commits, "exact policy evaluation/commit attempts");
            Check(world.Audit.Attempts.Count == attempts && world.Audit.Events.Count == events, "exact audit attempts/completed events");
        }
        async Task Overlap(IdentityKind identity, ResourceKind kind)
        {
            var world = NewWorld(identity, kind);
            var original = await First(world);
            var barrier = world.Source.Hold(oracle.Id(kind, 1), 2);
            using var cleanup = new CancellationTokenSource();
            var leftCaller = world.Caller(); var rightCaller = world.Caller();
            var left = Invoke(world, leftCaller, original, cleanup.Token);
            var right = Invoke(world, rightCaller, original, cleanup.Token);
            try
            {
                await barrier.Arrived.Task.WaitAsync(Bound);
                Check(!left.IsCompleted && !right.IsCompleted && world.Policy.Grants.Count == 3 && world.Audit.Attempts.Count == 1, "both resolved continuations overlap at observable selected-item calls before audit");
                Reads(world, 1, 1, oracle.Id(kind, 1), oracle.Id(kind, 1));
                barrier.Release();
                var results = await Task.WhenAll(left, right).WaitAsync(Bound);
                Success(world, leftCaller, results[0], 1, Allowed[kind], true);
                Success(world, rightCaller, results[1], 1, Allowed[kind], true);
                Check(results[0].NextCursor != results[1].NextCursor && results.All(r => r.NextCursor != original), "distinct successor handles for duplicate replay");
                var stableLeft = JsonNode.Parse(results[0].Envelope.AsSpan())!.AsObject();
                var stableRight = JsonNode.Parse(results[1].Envelope.AsSpan())!.AsObject();
                stableLeft.Remove("nextCursor"); stableRight.Remove("nextCursor");
                Check(JsonNode.DeepEquals(stableLeft, stableRight), "stable content equal despite independent random handles");
                foreach (var result in results)
                {
                    var caller = world.Caller();
                    Success(world, caller, await Invoke(world, caller, result.NextCursor).WaitAsync(Bound), 2, Allowed[kind], false);
                }
                var replayCaller = world.Caller();
                Success(world, replayCaller, await Invoke(world, replayCaller, original).WaitAsync(Bound), 1, Allowed[kind], true);
                Reads(world, 0, 0, oracle.Id(kind, 0), oracle.Id(kind, 1), oracle.Id(kind, 1), oracle.Id(kind, 2), oracle.Id(kind, 2), oracle.Id(kind, 1));
                Totals(world, 6, 6, 6, 6);
                Check(world.Policy.Commits.All(c => c.Executed) && world.Audit.Failures.Count == 0, "separate accepted commit/audit atoms without fault");
                scenarios++;
            }
            finally
            {
                cleanup.Cancel(); barrier.Release();
                await Task.WhenAll(left, right).WaitAsync(Bound);
            }
        }
        async Task Recovery(IdentityKind identity, ResourceKind kind, Failure failure)
        {
            var world = NewWorld(identity, kind);
            var original = await First(world);
            var barrier = world.Source.Hold(oracle.Id(kind, 1), 1);
            using var cancellation = new CancellationTokenSource();
            var caller = world.Caller();
            var pending = Invoke(world, caller, original, cancellation.Token);
            try
            {
                await barrier.Arrived.Task.WaitAsync(Bound);
                Check(!pending.IsCompleted && world.Policy.Grants.Count == 2 && world.Audit.Attempts.Count == 1, "resolved continuation held before terminal failure");
                Reads(world, 1, 1, oracle.Id(kind, 1));
                if (failure == Failure.Cancel) cancellation.Cancel();
                else
                {
                    if (failure == Failure.Deadline) world.Clock.Set(TimeSpan.FromSeconds(5));
                    if (failure == Failure.AuditFalse) world.Audit.Mode = AuditMode.Reject;
                    if (failure == Failure.AuditThrow) world.Audit.Mode = AuditMode.Throw;
                    barrier.Release();
                }
                var result = await pending.WaitAsync(Bound);
                var expected = failure == Failure.Cancel ? McpOutcome.Cancelled : McpOutcome.DependencyUnavailable;
                var auditFailure = failure is Failure.AuditFalse or Failure.AuditThrow;
                Denied(result, expected);
                Audit(world, caller, auditFailure ? McpOutcome.Success : expected, !auditFailure);
                if (failure == Failure.Deadline) Check(world.Audit.Attempts.Single(a => a.CorrelationId == caller.CorrelationId).ElapsedMilliseconds == 5000, "exact five-second terminal edge");
                Check(world.Audit.Failures.SequenceEqual(auditFailure ? new[] { (OperationalFailure.AuditUnavailable, caller.CorrelationId) } : []), "exact safe operational audit failure signal");
                Totals(world, 2, auditFailure ? 2 : 1, 2, auditFailure ? 1 : 2);
                world.Audit.Mode = AuditMode.Accept; barrier.Release();
                Check(world.Clock.Elapsed < TimeSpan.FromSeconds(300), "retry before original expiry without authority change");
                var retryCaller = world.Caller();
                var retry = await Invoke(world, retryCaller, original).WaitAsync(Bound);
                Success(world, retryCaller, retry, 1, Allowed[kind], true);
                Check(retry.NextCursor != original && world.Policy.Grants.All(g => g.Revision == 1), "original cursor retry/current unchanged authority/new handle");
                var lastCaller = world.Caller();
                Success(world, lastCaller, await Invoke(world, lastCaller, retry.NextCursor).WaitAsync(Bound), 2, Allowed[kind], false);
                Reads(world, 0, 0, oracle.Id(kind, 0), oracle.Id(kind, 1), oracle.Id(kind, 1), oracle.Id(kind, 2));
                Totals(world, 4, auditFailure ? 4 : 3, 4, auditFailure ? 3 : 4);
                scenarios++;
            }
            finally
            {
                cancellation.Cancel(); barrier.Release();
                await pending.WaitAsync(Bound);
            }
        }
        async Task Revision(IdentityKind identity, ResourceKind kind)
        {
            var world = NewWorld(identity, kind);
            var original = await First(world);
            var barrier = world.Source.Hold(oracle.Id(kind, 1), 1);
            using var cleanup = new CancellationTokenSource();
            var staleCaller = world.Caller();
            var pending = Invoke(world, staleCaller, original, cleanup.Token);
            try
            {
                await barrier.Arrived.Task.WaitAsync(Bound);
                Reads(world, 1, 1, oracle.Id(kind, 1));
                world.Policy.Change(false, Allowed[kind]);
                barrier.Release();
                Denied(await pending.WaitAsync(Bound), McpOutcome.Unavailable);
                Audit(world, staleCaller, McpOutcome.Unavailable, true, true);
                Check(world.Policy.Commits.SequenceEqual(new[] { new Commit(1, true), new Commit(1, false) }), "revision wins trusted emission fence after selected-item read");
                var revokedCaller = world.Caller();
                Denied(await Invoke(world, revokedCaller, original).WaitAsync(Bound), McpOutcome.Unavailable);
                Audit(world, revokedCaller, McpOutcome.Unavailable, true, true);
                Reads(world, 2, 2);
                var currentFields = new[] { "itemId", "category", kind == ResourceKind.Findings ? "title" : "effort" };
                world.Policy.Change(true, currentFields);
                var regrantedCaller = world.Caller();
                Denied(await Invoke(world, regrantedCaller, original).WaitAsync(Bound), McpOutcome.Unavailable);
                Audit(world, regrantedCaller, McpOutcome.Unavailable, true);
                Reads(world, 2, 2);
                var freshCaller = world.Caller();
                var fresh = await Invoke(world, freshCaller).WaitAsync(Bound);
                Success(world, freshCaller, fresh, 0, currentFields, true);
                var nextCaller = world.Caller();
                Success(world, nextCaller, await Invoke(world, nextCaller, fresh.NextCursor).WaitAsync(Bound), 1, currentFields, true);
                Reads(world, 0, 0, oracle.Id(kind, 0), oracle.Id(kind, 1), oracle.Id(kind, 0), oracle.Id(kind, 1));
                Check(world.Policy.Grants.Select(g => g.Revision).SequenceEqual(new long[] { 1, 1, 2, 3, 3, 3 }), "each retry independently gets current authority revision");
                Totals(world, 6, 4, 6, 6);
                Check(world.Audit.Failures.Count == 0, "revision denial does not report dependency fault");
                scenarios++;
            }
            finally
            {
                cleanup.Cancel(); barrier.Release();
                await pending.WaitAsync(Bound);
            }
        }
    }

    private static bool Opaque(string? handle) => handle is { Length: 64 } && handle.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static byte[] Request(ResourceKind kind, string? cursor)
    {
        var request = new JsonObject
        {
            ["contractVersion"] = "synthetic-published-health-read-v1", ["resourceKind"] = kind.ToString(),
            ["assessmentId"] = "syn-assessment-a", ["reportVersionId"] = "syn-report-a", ["pageSize"] = 1
        };
        if (cursor is not null) request["cursor"] = cursor;
        return JsonSerializer.SerializeToUtf8Bytes(request);
    }
    private sealed class Oracle
    {
        internal readonly byte[] Manifest;
        internal readonly JsonObject Bindings = new();
        internal readonly Dictionary<string, byte[]> Payloads = [];
        private readonly Dictionary<ResourceKind, string[]> ids = [];
        internal Oracle(Action<bool, string> check)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures");
            var goldenBytes = File.ReadAllBytes(Path.Combine(path, "golden-hashes.json"));
            check(Hash(goldenBytes) == "c50954fcd4881b3c1ca70e6865cd2db4c3a8d2e886c217a2335c5d42db3bdb6f", "immutable original golden index");
            var golden = JsonNode.Parse(goldenBytes)!.AsObject();
            foreach (var pair in golden)
            {
                var bytes = File.ReadAllBytes(Path.Combine(path, pair.Key));
                check(Hash(bytes) == pair.Value!.GetValue<string>(), "original fixture hash " + pair.Key);
            }
            Manifest = File.ReadAllBytes(Path.Combine(path, "manifest.json"));
            check(Hash(Manifest) == Digest, "pinned independent manifest digest");
            var manifest = JsonNode.Parse(Manifest)!.AsObject();
            foreach (var pair in manifest.Where(p => p.Key is not "collections" and not "items")) Bindings.Add(pair.Key, pair.Value!.DeepClone());
            foreach (var kind in Allowed.Keys)
            {
                ids[kind] = manifest["items"]!.AsArray().Where(d => d!["kind"]!.GetValue<string>() == kind.ToString()).Select(d => d!["itemId"]!.GetValue<string>()).ToArray();
                check(ids[kind].Length == 3 && ids[kind].SequenceEqual(ids[kind].Order(StringComparer.Ordinal)), "original three-item ordinal group " + kind);
                foreach (var id in ids[kind]) Payloads.Add(id, File.ReadAllBytes(Path.Combine(path, id + ".json")));
            }
        }
        internal string Id(ResourceKind kind, int ordinal) => ids[kind][ordinal];
        internal JsonObject Row(ResourceKind kind, int ordinal, string[] fields)
        {
            var source = JsonNode.Parse(Payloads[Id(kind, ordinal)])!.AsObject();
            var row = new JsonObject();
            foreach (var field in fields) row.Add(field, source[field]!.DeepClone());
            return row;
        }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
    private sealed class World
    {
        internal readonly IdentityKind Identity;
        internal readonly string IdentityId;
        internal readonly ResourceKind Kind;
        internal readonly Reader Source;
        internal readonly Policy Policy;
        internal readonly AuditSpy Audit = new();
        internal readonly Clock Clock = new();
        internal readonly McpHarness Host;
        internal World(Oracle oracle, IdentityKind identity, ResourceKind kind)
        {
            Identity = identity; IdentityId = identity == IdentityKind.NamedUser ? "syn-user-a" : "syn-service-a"; Kind = kind;
            Source = new(oracle); Policy = new(identity, IdentityId, kind);
            Host = new(Source, Policy, Audit, Clock);
        }
        internal Caller Caller() => new(Identity, IdentityId, Guid.NewGuid());
    }
    private sealed class Clock : IMonotonicClock
    {
        private long ticks;
        public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref ticks));
        internal void Set(TimeSpan time) => Interlocked.Exchange(ref ticks, time.Ticks);
    }
    private sealed class Barrier(string id, int arrivals)
    {
        internal readonly string Id = id;
        internal readonly TaskCompletionSource Arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int count;
        internal async Task WaitAsync(CancellationToken token)
        {
            if (Interlocked.Increment(ref count) >= arrivals) Arrived.TrySetResult();
            await release.Task.WaitAsync(token);
        }
        internal void Release() => release.TrySetResult();
    }
    private sealed class Reader(Oracle oracle) : IPublicationReader
    {
        internal readonly System.Collections.Concurrent.ConcurrentQueue<ManifestRead> Manifests = new();
        internal readonly System.Collections.Concurrent.ConcurrentQueue<ItemRead> Items = new();
        private Barrier? barrier;
        internal Barrier Hold(string id, int arrivals) => barrier = new(id, arrivals);
        public ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token)
        {
            Manifests.Enqueue(new(scope, assessmentId, reportVersionId)); token.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ManifestSource?>(new(oracle.Manifest.ToImmutableArray(), Digest));
        }
        public async ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token)
        {
            Items.Enqueue(new(scope, reportVersionId, kind, itemId));
            var selectedBarrier = barrier;
            if (selectedBarrier?.Id == itemId) await selectedBarrier.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            return oracle.Payloads[itemId].ToImmutableArray();
        }
    }
    private sealed class Policy(IdentityKind identity, string identityId, ResourceKind kind) : IReadPolicy
    {
        private readonly object fence = new();
        private long revision = 1;
        private bool assigned = true;
        private ImmutableHashSet<string> fields = Allowed[kind].ToImmutableHashSet(StringComparer.Ordinal);
        internal readonly System.Collections.Concurrent.ConcurrentQueue<ReadGrant> Grants = new();
        internal readonly System.Collections.Concurrent.ConcurrentQueue<Commit> Commits = new();
        internal void Change(bool active, string[] currentFields)
        {
            lock (fence) { revision++; assigned = active; fields = currentFields.ToImmutableHashSet(StringComparer.Ordinal); }
        }
        public ReadGrant? GetGrant(Caller caller, ReadRequest request)
        {
            lock (fence)
            {
                var grant = new ReadGrant(identity, identityId, revision, FixtureScope, kind, "syn-assessment-a", "syn-report-a", Digest,
                    true, assigned, true, true, false, true, Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(), fields);
                Grants.Enqueue(grant); return grant;
            }
        }
        public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId) => throw new InvalidOperationException("unexpected reference overlay read");
        public bool TryCommit(ReadGrant grant, Action commit)
        {
            lock (fence)
            {
                var current = grant.Revision == revision && assigned && grant.Fields.SetEquals(fields);
                Commits.Enqueue(new(grant.Revision, current));
                if (!current) return false;
                commit(); return true;
            }
        }
    }
    private sealed class AuditSpy : IMcpAudit
    {
        internal AuditMode Mode;
        internal readonly System.Collections.Concurrent.ConcurrentQueue<McpAuditEvent> Attempts = new();
        internal readonly System.Collections.Concurrent.ConcurrentQueue<McpAuditEvent> Events = new();
        internal readonly System.Collections.Concurrent.ConcurrentQueue<(OperationalFailure, Guid)> Failures = new();
        public bool Complete(McpAuditEvent auditEvent)
        {
            Attempts.Enqueue(auditEvent);
            if (Mode == AuditMode.Throw) throw new InvalidOperationException("synthetic audit fault");
            if (Mode == AuditMode.Reject) return false;
            Events.Enqueue(auditEvent); return true;
        }
        public void OperationalFailure(OperationalFailure failure, Guid correlationId) => Failures.Enqueue((failure, correlationId));
    }
}
