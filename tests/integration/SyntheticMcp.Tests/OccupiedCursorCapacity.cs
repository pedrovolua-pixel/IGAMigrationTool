using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

// Expectations come from the preserved contract/fixture oracle, not production projection helpers.
internal static class OccupiedCursorCapacity
{
    private const string Digest = "613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d";
    private static readonly Scope FixtureScope = new("syn-customer-a", "syn-project-a", "syn-environment-a");
    private static readonly Dictionary<ResourceKind, string[]> Allowed = new()
    {
        [ResourceKind.Findings] = ["itemId", "category", "title", "severity", "mandatoryReview"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "options", "reviewLabel"]
    };
    private static readonly Dictionary<ResourceKind, string[]> AllFields = new()
    {
        [ResourceKind.Findings] = ["itemId", "category", "title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "summary", "options", "priority", "effort", "reviewLabel"]
    };
    private enum AuditMode { Accept, Reject, Throw }
    private sealed record ItemRead(Scope Scope, string Report, ResourceKind Kind, string Id);
    private sealed record ManifestRead(Scope Scope, string Assessment, string Report);
    private sealed record Evaluation(Caller Caller, ReadRequest Request, ReadGrant Grant);
    private sealed record Handle(int Identity, int Position);

    internal static async Task RunAsync()
    {
        var assertions = 0;
        var scenarios = 0;
        var invocations = 0;
        void Check(bool value, string label)
        {
            assertions++;
            if (!value) throw new InvalidOperationException("FAIL cycle05 occupied cursor capacity " + label);
        }
        var oracle = new Oracle(Check);
        foreach (var identity in new[] { IdentityKind.NamedUser, IdentityKind.Service })
            foreach (var kind in Allowed.Keys)
                foreach (var customerCeiling in new[] { false, true })
                    foreach (var failure in new[] { AuditMode.Reject, AuditMode.Throw })
                        await Scenario(identity, kind, customerCeiling, failure);
        Check(scenarios == 16 && invocations == 5200, "exact sixteen worlds and invocation partition");
        Console.WriteLine($"PASS independent cycle05 occupied cursor capacity: {scenarios} scenarios, {invocations} invocations, {assertions} assertions; identity128/customer512 audit recovery and final-page progress; P1D-T06/08/09/10 synthetic fixture only");

        async Task Scenario(IdentityKind identity, ResourceKind kind, bool customerCeiling, AuditMode failure)
        {
            var cap = customerCeiling ? 512 : 128;
            var world = new World(oracle, identity, kind);
            var handles = new Dictionary<string, Handle>(StringComparer.Ordinal);
            var requestWindows = new List<(int Identity, TimeSpan Time)>();
            var calls = 0;
            var identityCount = customerCeiling ? 5 : 1;
            async Task<McpResult> Invoke(int identityIndex, int ordinal, string? cursor = null,
                McpOutcome outcome = McpOutcome.Success, AuditMode mode = AuditMode.Accept)
            {
                world.Clock.Time = TimeSpan.FromSeconds(calls / (customerCeiling ? 250 : 50) * 60);
                Check(world.Clock.Time <= TimeSpan.FromSeconds(120) && world.Clock.Time < TimeSpan.FromSeconds(300), "logical pacing preserves original expiry");
                requestWindows.RemoveAll(r => world.Clock.Time - r.Time >= TimeSpan.FromSeconds(60));
                requestWindows.Add((identityIndex, world.Clock.Time));
                Check(requestWindows.Count < 300 && requestWindows.Count(r => r.Identity == identityIndex) < 60, "observed request ledger below unrelated paired rate caps");
                var caller = new Caller(identity, IdentityId(identity, identityIndex), Guid.NewGuid());
                var expectedRequest = new ReadRequest(kind, "syn-assessment-a", "syn-report-a", 1, cursor);
                if (cursor is not null) Check(handles.TryGetValue(cursor, out var bound) && bound == new Handle(identityIndex, ordinal), "continuation belongs to retained published-handle ledger");
                var before = calls;
                var acceptedBefore = world.Audit.Events.Count;
                var failuresBefore = world.Audit.Failures.Count;
                world.Audit.Mode = mode;
                var result = await world.Host.ReadAsync(caller, Request(kind, cursor)).WaitAsync(TimeSpan.FromSeconds(5));
                calls++; invocations++;
                Check(world.Source.Manifests.Count == calls && world.Source.Manifests[before] == new ManifestRead(FixtureScope, "syn-assessment-a", "syn-report-a"), "one exact original manifest source call");
                Check(world.Source.Items.Count == calls && world.Source.Items[before] == new ItemRead(FixtureScope, "syn-report-a", kind, oracle.Id(kind, ordinal)), "one exact selected item call even at terminal capacity denial");
                Check(world.Policy.Evaluations.Count == calls && world.Policy.Commits.Count == calls, "one authority evaluation and final trusted commit per invocation");
                var evaluation = world.Policy.Evaluations[before];
                Check(evaluation.Caller == caller && evaluation.Request == expectedRequest && evaluation.Grant == world.Policy.Commits[before], "exact caller/request/grant commit ledger");
                Check(evaluation.Grant.IdentityKind == identity && evaluation.Grant.IdentityId == caller.IdentityId && evaluation.Grant.Revision == 1 && evaluation.Grant.Scope == FixtureScope && evaluation.Grant.ManifestDigest == Digest && evaluation.Grant.Fields.SetEquals(Allowed[kind]), "unchanged exact grant identity/revision/scope/digest/fields");
                Check(world.Audit.Attempts.Count == calls && world.Audit.Events.Count == acceptedBefore + (mode == AuditMode.Accept ? 1 : 0), "one attempted completion, accepted event only on true audit result");
                var audit = world.Audit.Attempts[before];
                var attemptedOutcome = mode == AuditMode.Accept ? outcome : McpOutcome.Success;
                Check(audit.CorrelationId == caller.CorrelationId && audit.IdentityKind == identity && audit.IdentityId == caller.IdentityId && audit.Scope == FixtureScope && audit.ResourceKind == kind && audit.Outcome == attemptedOutcome && audit.ElapsedMilliseconds == 0, "exact safe typed audit context and within-request logical elapsed");
                var successFields = attemptedOutcome == McpOutcome.Success;
                Check(audit.ReturnedFields.SequenceEqual(successFields ? Allowed[kind].Order(StringComparer.Ordinal) : []) && audit.RedactedFields.SequenceEqual(successFields ? AllFields[kind].Except(Allowed[kind]).Order(StringComparer.Ordinal) : []), "exact attempted returned/redacted schema field names");
                Check(world.Audit.Failures.Count == failuresBefore + (mode == AuditMode.Accept ? 0 : 1), "safe operational failure count");
                if (mode != AuditMode.Accept) Check(world.Audit.Failures[^1] == (OperationalFailure.AuditUnavailable, caller.CorrelationId), "safe audit unavailable signal with trusted correlation");
                else Check(world.Audit.Events[^1] == audit, "accepted event is the sole attempted completion");
                Check(result.Outcome == outcome, "exact result outcome");
                if (outcome != McpOutcome.Success)
                {
                    Check(result.Envelope.IsDefaultOrEmpty && result.NextCursor is null, "failure exposes neither envelope nor unpublished handle");
                    return result;
                }
                Check(!result.Envelope.IsDefaultOrEmpty, "successful detached envelope");
                var hasNext = ordinal < 2;
                Check(hasNext ? Opaque(result.NextCursor) : result.NextCursor is null, "opaque allocating successor or terminal null");
                var expected = new JsonObject
                {
                    ["contractVersion"] = "synthetic-published-health-read-v1",
                    ["resourceKind"] = kind.ToString(),
                    ["manifestDigest"] = Digest,
                    ["bindings"] = oracle.Bindings.DeepClone(),
                    ["items"] = new JsonArray(oracle.Row(kind, ordinal)),
                    ["nextCursor"] = result.NextCursor
                };
                Check(JsonNode.DeepEquals(JsonNode.Parse(result.Envelope.AsSpan()), expected), "exact typed ordinal fields/data/original scalar bindings/digest/closed envelope");
                if (hasNext)
                {
                    Check(result.NextCursor != Digest && handles.TryAdd(result.NextCursor!, new Handle(identityIndex, ordinal + 1)), "new unique published opaque handle counted exactly once");
                }
                return result;
            }

            var original = (await Invoke(0, 0)).NextCursor!;
            var retainedFinal = (await Invoke(0, 1, original)).NextCursor!;
            string? otherFinal = null;
            if (customerCeiling)
            {
                var otherOriginal = (await Invoke(1, 0)).NextCursor!;
                otherFinal = (await Invoke(1, 1, otherOriginal)).NextCursor!;
            }
            while (handles.Count < cap - 1) await Invoke(customerCeiling ? calls % identityCount : 0, 0);
            Check(handles.Count == cap - 1 && handles.ContainsKey(original) && handles.ContainsKey(retainedFinal), "cap-minus-one includes original and retained final-page handles");
            Check(handles.Values.GroupBy(h => h.Identity).All(g => g.Count() < 128), "every identity below its cursor ceiling before final slot");
            var beforeFailure = handles.Count;
            await Invoke(0, 1, original, McpOutcome.DependencyUnavailable, failure);
            Check(handles.Count == beforeFailure && world.Audit.Events.Count == cap - 1, "failed audit did not publish or accept a completion");
            var recovered = (await Invoke(0, 1, original)).NextCursor!;
            Check(handles.Count == cap, "same original handle recovers precisely the final free reservation slot");
            if (customerCeiling) Check(handles.Values.GroupBy(h => h.Identity).All(g => g.Count() < 128), "customer capacity denial cannot be masked by identity quota");
            else Check(handles.Values.Count(h => h.Identity == 0) == 128 && handles.Count < 512, "identity capacity denial cannot be masked by customer quota");
            await Invoke(0, 1, original, McpOutcome.Limited);
            Check(handles.Count == cap, "allocating limited response preserves all occupied published handles");
            await Invoke(0, 2, recovered);
            await Invoke(0, 2, retainedFinal);
            Check(handles.Count == cap, "new and retained final pages need no allocation at full capacity");
            if (customerCeiling)
            {
                await Invoke(1, 2, otherFinal);
                Check(handles.Count == 512, "another same-customer identity's retained final-page handle survives full customer occupancy");
            }
            else
            {
                await Invoke(1, 0);
                Check(handles.Count == 129 && handles.Values.Count(h => h.Identity == 1) == 1, "spare identity progresses in the same customer without disturbing full identity quota");
            }
            Check(calls == cap + 5 && world.Audit.Attempts.Count == calls && world.Audit.Events.Count == calls - 1 && world.Audit.Failures.Count == 1, "exact scenario call/attempt/accepted/failure totals");
            Check(world.Clock.Time == TimeSpan.FromSeconds(120), "all proof completed at120 logical seconds before300 expiry");
            scenarios++;
        }
    }

    private static string IdentityId(IdentityKind identity, int index) => (identity == IdentityKind.NamedUser ? "syn-user-" : "syn-service-") + index;
    private static bool Opaque(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static byte[] Request(ResourceKind kind, string? cursor)
    {
        var request = new JsonObject
        {
            ["contractVersion"] = "synthetic-published-health-read-v1",
            ["resourceKind"] = kind.ToString(),
            ["assessmentId"] = "syn-assessment-a",
            ["reportVersionId"] = "syn-report-a",
            ["pageSize"] = 1
        };
        if (cursor is not null) request["cursor"] = cursor;
        return JsonSerializer.SerializeToUtf8Bytes(request);
    }

    private sealed class Oracle
    {
        internal readonly byte[] Manifest;
        internal readonly JsonObject Bindings = new();
        internal readonly Dictionary<string, byte[]> Payloads = [];
        internal Oracle(Action<bool, string> check)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures");
            var goldenBytes = File.ReadAllBytes(Path.Combine(path, "golden-hashes.json"));
            check(Hash(goldenBytes) == "c50954fcd4881b3c1ca70e6865cd2db4c3a8d2e886c217a2335c5d42db3bdb6f", "pinned original golden index");
            foreach (var pair in JsonNode.Parse(goldenBytes)!.AsObject())
                check(Hash(File.ReadAllBytes(Path.Combine(path, pair.Key))) == pair.Value!.GetValue<string>(), "pinned fixture " + pair.Key);
            Manifest = File.ReadAllBytes(Path.Combine(path, "manifest.json"));
            check(Hash(Manifest) == Digest, "original independently pinned manifest digest");
            var manifest = JsonNode.Parse(Manifest)!.AsObject();
            foreach (var pair in manifest.Where(p => p.Key is not "collections" and not "items")) Bindings.Add(pair.Key, pair.Value!.DeepClone());
            check(Bindings.Count == 14, "all fourteen original scalar bindings");
            foreach (var kind in Allowed.Keys)
            {
                var ids = manifest["items"]!.AsArray().Where(d => d!["kind"]!.GetValue<string>() == kind.ToString()).Select(d => d!["itemId"]!.GetValue<string>()).ToArray();
                check(ids.SequenceEqual(Enumerable.Range(0, 3).Select(i => Id(kind, i))), "exact original a/b/c ordinal group");
                foreach (var id in ids) Payloads.Add(id, File.ReadAllBytes(Path.Combine(path, id + ".json")));
                for (var i = 0; i < 3; i++)
                {
                    var fixture = JsonNode.Parse(Payloads[Id(kind, i)])!.AsObject();
                    var selected = new JsonObject();
                    foreach (var field in Allowed[kind]) selected.Add(field, fixture[field]!.DeepClone());
                    check(JsonNode.DeepEquals(selected, Row(kind, i)), "explicit typed expected atoms/labels agree with original fixture bytes");
                }
            }
        }
        internal string Id(ResourceKind kind, int ordinal) => (kind == ResourceKind.Findings ? "syn-finding-" : "syn-rec-") + (char)('a' + ordinal);
        internal JsonObject Row(ResourceKind kind, int ordinal)
        {
            var row = new JsonObject
            {
                ["itemId"] = Id(kind, ordinal),
                ["category"] = kind == ResourceKind.Findings ? new[] { "Summary", "Configuration", "Identity" }[ordinal] : "Summary"
            };
            if (kind == ResourceKind.Findings)
            {
                row["title"] = "Fictional finding.";
                row["severity"] = ordinal == 0 ? "High" : "Low";
                row["mandatoryReview"] = true;
            }
            else
            {
                row["findingId"] = "syn-finding-" + (char)('a' + ordinal);
                row["options"] = new JsonArray("Fictional option.");
                row["reviewLabel"] = "Unverified";
            }
            return row;
        }
        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
    private sealed class World
    {
        internal readonly Reader Source;
        internal readonly Policy Policy;
        internal readonly AuditSpy Audit = new();
        internal readonly Clock Clock = new();
        internal readonly McpHarness Host;
        internal World(Oracle oracle, IdentityKind identity, ResourceKind kind)
        {
            Source = new(oracle); Policy = new(identity, kind);
            Host = new(Source, Policy, Audit, Clock);
        }
    }
    private sealed class Clock : IMonotonicClock
    {
        internal TimeSpan Time;
        public TimeSpan Elapsed => Time;
    }
    private sealed class Reader(Oracle oracle) : IPublicationReader
    {
        internal readonly List<ManifestRead> Manifests = [];
        internal readonly List<ItemRead> Items = [];
        public ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token)
        {
            Manifests.Add(new(scope, assessmentId, reportVersionId)); token.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ManifestSource?>(new(oracle.Manifest.ToImmutableArray(), Digest));
        }
        public ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token)
        {
            Items.Add(new(scope, reportVersionId, kind, itemId)); token.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ImmutableArray<byte>?>(oracle.Payloads[itemId].ToImmutableArray());
        }
    }
    private sealed class Policy(IdentityKind identity, ResourceKind kind) : IReadPolicy
    {
        private readonly object fence = new();
        internal readonly List<Evaluation> Evaluations = [];
        internal readonly List<ReadGrant> Commits = [];
        public ReadGrant? GetGrant(Caller caller, ReadRequest request)
        {
            lock (fence)
            {
                var grant = new ReadGrant(identity, caller.IdentityId, 1, FixtureScope, kind, "syn-assessment-a", "syn-report-a", Digest,
                    true, true, true, true, false, true, Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(), Allowed[kind].ToImmutableHashSet(StringComparer.Ordinal));
                Evaluations.Add(new(caller, request, grant)); return grant;
            }
        }
        public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId) => throw new InvalidOperationException("unexpected reference overlay read");
        public bool TryCommit(ReadGrant grant, Action commit)
        {
            lock (fence) { Commits.Add(grant); commit(); return true; }
        }
    }
    private sealed class AuditSpy : IMcpAudit
    {
        internal AuditMode Mode;
        internal readonly List<McpAuditEvent> Attempts = [];
        internal readonly List<McpAuditEvent> Events = [];
        internal readonly List<(OperationalFailure, Guid)> Failures = [];
        public bool Complete(McpAuditEvent auditEvent)
        {
            Attempts.Add(auditEvent);
            if (Mode == AuditMode.Throw) throw new InvalidOperationException("synthetic audit fault");
            if (Mode == AuditMode.Reject) return false;
            Events.Add(auditEvent); return true;
        }
        public void OperationalFailure(OperationalFailure failure, Guid correlationId) => Failures.Add((failure, correlationId));
    }
}
