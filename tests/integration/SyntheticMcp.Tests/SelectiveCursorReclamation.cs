using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

// Contract/fixture oracle was pinned before inspecting runtime; no production oracle helpers.
internal static class SelectiveCursorReclamation
{
    private const string Digest = "613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d";
    private static readonly Scope FixtureScope = new("syn-customer-a", "syn-project-a", "syn-environment-a");
    private static readonly string[] Fields = ["category", "itemId", "severity", "title"];
    private static readonly string[] Redacted = ["confidence", "mandatoryReview", "referenceIds", "reviewState", "summary"];
    private sealed record ManifestRead(Scope Scope, string Assessment, string Report);
    private sealed record ItemRead(Scope Scope, string Report, ResourceKind Kind, string Id);
    private sealed record Evaluation(Caller Caller, ReadRequest Request, ReadGrant Grant);
    private sealed record Commit(ReadGrant Grant, bool Accepted);
    private sealed record Handle(int Identity, long Revision, TimeSpan Created, TimeSpan Expires);
    private sealed record RequestObservation(int Identity, TimeSpan Raw, TimeSpan Effective);

    internal static async Task RunAsync()
    {
        var assertions = 0;
        var scenarios = 0;
        var invocations = 0;
        void Check(bool condition, string label)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("FAIL cycle07 selective reclamation " + label);
        }
        var oracle = new Oracle(Check);
        foreach (var kind in new[] { IdentityKind.NamedUser, IdentityKind.Service })
        {
            await Scenario(kind, false);
            await Scenario(kind, true);
        }
        Check(scenarios == 4 && invocations == 2808, "exact four worlds and invocation total");
        Console.WriteLine($"PASS independent cycle07 selective cursor reclamation: {scenarios} scenarios, {invocations} invocations, {assertions} assertions; predicted250 expired/103 revised, representative stale/survivor replays, exact250/103 refill ceilings; P1D-T06/07/08/09/10/11/12 synthetic fixture only");

        async Task Scenario(IdentityKind kind, bool revisionWorld)
        {
            var world = new World(oracle, kind);
            var handles = new Dictionary<string, Handle>(StringComparer.Ordinal);
            var requests = new List<RequestObservation>();
            var effective = TimeSpan.Zero;
            var calls = 0;
            var maxIdentityRequests = 0;
            var maxCustomerRequests = 0;
            var maxOccupancy = 0;
            var revision = new long[] { 1, 1, 1, 1, 1 };
            var oldest = new string[5];
            var later = new string[5];
            var newest = new string[5];
            var replacements = new List<string>();
            int LiveCount() => handles.Values.Count(h => h.Expires > effective && h.Revision == revision[h.Identity]);
            void ObserveClock(double raw)
            {
                world.Clock.Raw = TimeSpan.FromSeconds(raw);
                if (world.Clock.Raw > effective) effective = world.Clock.Raw;
            }
            async Task<McpResult> Invoke(int identity, double raw, string? cursor = null,
                McpOutcome expected = McpOutcome.Success)
            {
                ObserveClock(raw);
                var at = new RequestObservation(identity, world.Clock.Raw, effective);
                requests.Add(at);
                var window = requests.Where(r => effective - r.Effective < TimeSpan.FromSeconds(60)).ToArray();
                var identityWindow = window.Count(r => r.Identity == identity);
                maxCustomerRequests = Math.Max(maxCustomerRequests, window.Length);
                maxIdentityRequests = Math.Max(maxIdentityRequests, identityWindow);
                Check(window.Length < 300 && identityWindow < 60, "every call including denial/replay below unrelated admission caps");
                Check(at.Raw <= at.Effective && (requests.Count < 2 || requests[^2].Effective <= at.Effective), "independent raw/effective monotonic request ledger");
                if (cursor is not null)
                {
                    Check(handles.TryGetValue(cursor, out var handle) && handle.Identity == identity, "replay from external successful identity-bound handle ledger");
                    var valid = handle!.Expires > effective && handle.Revision == revision[identity] && world.Policy.Active[identity];
                    Check(valid == (expected == McpOutcome.Success), "external expiry/revision/authority oracle predicts replay result");
                }
                else
                {
                    Check(expected is McpOutcome.Success or McpOutcome.Limited, "allocating invocation expected outcome vocabulary");
                    Check(expected == McpOutcome.Success ? LiveCount() < 512 : LiveCount() == 512, "independent live ledger predicts available slot or occupied customer ceiling");
                }
                var caller = new Caller(kind, IdentityId(kind, identity), Guid.NewGuid());
                var expectedRequest = new ReadRequest(ResourceKind.Findings, "syn-assessment-a", "syn-report-a", 2, cursor);
                var beforeManifest = world.Source.Manifests.Count;
                var beforeItems = world.Source.Items.Count;
                var beforeCommit = world.Policy.Commits.Count;
                var beforeAudit = world.Audit.Events.Count;
                var result = await world.Host.ReadAsync(caller, Request(cursor)).WaitAsync(TimeSpan.FromSeconds(5));
                calls++; invocations++;
                var sourceExpected = expected != McpOutcome.Unavailable;
                Check(world.Source.Manifests.Count == beforeManifest + (sourceExpected ? 1 : 0), "exact manifest count; stale/denied before protected reads");
                var ids = cursor is null ? new[] { "syn-finding-a", "syn-finding-b" } : new[] { "syn-finding-c" };
                Check(world.Source.Items.Count == beforeItems + (sourceExpected ? ids.Length : 0), "exact minimized selected payload count");
                if (sourceExpected)
                {
                    Check(world.Source.Manifests[beforeManifest] == new ManifestRead(FixtureScope, "syn-assessment-a", "syn-report-a"), "exact manifest source arguments");
                    Check(world.Source.Items.Skip(beforeItems).SequenceEqual(ids.Select(id => new ItemRead(FixtureScope, "syn-report-a", ResourceKind.Findings, id))), "exact ordered item source arguments");
                }
                Check(world.Policy.Evaluations.Count == calls, "one current policy resolution every invocation");
                var evaluation = world.Policy.Evaluations[^1];
                var grant = evaluation.Grant;
                Check(evaluation.Caller == caller && evaluation.Request == expectedRequest, "exact trusted caller/request resolution arguments");
                Check(grant.IdentityKind == kind && grant.IdentityId == caller.IdentityId && grant.Revision == revision[identity] && grant.Scope == FixtureScope && grant.Kind == ResourceKind.Findings && grant.AssessmentId == "syn-assessment-a" && grant.ReportVersionId == "syn-report-a" && grant.ManifestDigest == Digest, "exact immutable authority bindings and independent identity revision");
                Check(grant.ActiveIdentity == world.Policy.Active[identity] && grant.ActiveAssignment && grant.CustomerMcpPolicy && grant.ResourceAvailable && !grant.ReadBlocked && grant.ActionAllowed && grant.Fields.SetEquals(Fields) && grant.Categories.SetEquals(Enum.GetValues<EvidenceCategory>()), "exact read flags/categories/minimized field grant");
                Check(world.Policy.Commits.Count == beforeCommit + (sourceExpected ? 1 : 0), "one final commit for sourced result; no commit on stale cursor/denied authority");
                if (sourceExpected) Check(world.Policy.Commits[^1] == new Commit(grant, true), "exact current grant accepted under synthetic commit fence");
                Check(world.Audit.Events.Count == beforeAudit + 1 && world.Audit.Attempts.Count == calls && world.Audit.Failures.Count == 0, "one accepted completion audit per call; zero operational failures");
                var audit = world.Audit.Events[^1];
                Check(world.Audit.Attempts[^1] == audit && audit.IdentityKind == kind && audit.IdentityId == caller.IdentityId && audit.Scope == (world.Policy.Active[identity] ? FixtureScope : null) && audit.ResourceKind == ResourceKind.Findings && audit.Outcome == expected && audit.CorrelationId == caller.CorrelationId && audit.ElapsedMilliseconds == 0, "exact payload-free audit context including revoked null scope");
                Check(audit.ReturnedFields.SequenceEqual(expected == McpOutcome.Success ? Fields : []) && audit.RedactedFields.SequenceEqual(expected == McpOutcome.Success ? Redacted : []), "exact returned/redacted audit schema names");
                Check(result.Outcome == expected, "exact typed outcome");
                if (expected != McpOutcome.Success)
                {
                    Check(result.Envelope.IsDefaultOrEmpty && result.NextCursor is null, "empty denial and no published cursor");
                    return result;
                }
                Check(!result.Envelope.IsDefaultOrEmpty, "success has detached serialized bytes");
                Check(cursor is null ? Opaque(result.NextCursor) : result.NextCursor is null, "allocating opaque handle or nonallocating final-page null");
                var envelope = new JsonObject
                {
                    ["contractVersion"] = "synthetic-published-health-read-v1",
                    ["resourceKind"] = "Findings",
                    ["manifestDigest"] = Digest,
                    ["bindings"] = oracle.Bindings.DeepClone(),
                    ["items"] = cursor is null ? new JsonArray(Row(0), Row(1)) : new JsonArray(Row(2)),
                    ["nextCursor"] = result.NextCursor
                };
                Check(JsonNode.DeepEquals(JsonNode.Parse(result.Envelope.AsSpan()), envelope), "exact closed fields/data/all fourteen original bindings/digest/envelope");
                if (cursor is null)
                {
                    Check(result.NextCursor != Digest && handles.TryAdd(result.NextCursor!, new(identity, revision[identity], effective, effective + TimeSpan.FromSeconds(300))), "distinct opaque successful handle entered independent lifetime ledger");
                    var live = handles.Values.Where(h => h.Expires > effective && h.Revision == revision[h.Identity]).ToArray();
                    maxOccupancy = Math.Max(maxOccupancy, live.Length);
                    Check(live.Length <= 512 && live.GroupBy(h => h.Identity).All(g => g.Count() < 128), "external live occupancy within customer and below identity cursor cap");
                }
                return result;
            }

            for (var cohort = 0; cohort < 3; cohort++)
                for (var n = 0; n < (cohort < 2 ? 250 : 12); n++)
                {
                    var identity = n % 5;
                    var handle = (await Invoke(identity, cohort * 60)).NextCursor!;
                    if (cohort == 0 && n < 5) oldest[identity] = handle;
                    if (cohort == 1 && n < 5) later[identity] = handle;
                    if (cohort == 2 && n < 5) newest[identity] = handle;
                }
            Check(handles.Count == 512 && LiveCount() == 512 && Enumerable.Range(0, 5).Select(i => handles.Values.Count(h => h.Identity == i)).SequenceEqual([103, 103, 102, 102, 102]), "exact occupied512 external handles distributed103/103/102/102/102");
            if (!revisionWorld)
            {
                await Invoke(4, 299.999, expected: McpOutcome.Limited);
                await Invoke(0, 299.999, oldest[0]);
                await Invoke(1, 299.999, later[1]);
                await Invoke(2, 299.999, newest[2]);
                await Invoke(0, 300, oldest[0], McpOutcome.Unavailable);
                Check(handles.Values.Count(h => h.Expires <= effective) == 250 && LiveCount() == 262, "predicted250 expired original cohort and262 retained; representative rejection separate from cohort prediction");
                await Invoke(1, 300, later[1]);
                for (var n = 0; n < 250; n++) replacements.Add((await Invoke(n % 5, 0)).NextCursor!);
                Check(world.Clock.Raw == TimeSpan.Zero && effective == TimeSpan.FromSeconds(300) && replacements.Count == 250 && LiveCount() == 512, "exact250 successful refill at raw rollback0/effective300");
                await Invoke(4, 0, expected: McpOutcome.Limited);
                await Invoke(1, 0, later[1]);
                await Invoke(0, 0, replacements[0]);
                Check(handles[replacements[0]].Created == TimeSpan.FromSeconds(300) && handles[replacements[0]].Expires == TimeSpan.FromSeconds(600), "independent new monotonic lifetime prediction");
                await Invoke(0, 599.999, replacements[0]);
                await Invoke(0, 600, replacements[0], McpOutcome.Unavailable);
                await Invoke(0, 0, replacements[0], McpOutcome.Unavailable);
                Check(world.Clock.Raw == TimeSpan.Zero && effective == TimeSpan.FromSeconds(600), "second rollback cannot revive newly expired handle");
                Check(calls == 774 && handles.Count == 762, "exact expiry-world observed call/successful allocation totals");
            }
            else
            {
                ObserveClock(180);
                world.Policy.Change(0, false, 2); revision[0] = 2;
                await Invoke(0, 180, oldest[0], McpOutcome.Unavailable);
                Check(handles.Values.Count(h => h.Revision != revision[h.Identity]) == 103 && LiveCount() == 409, "only103 target revision1 handles predicted invalidated");
                world.Policy.Change(0, true, 3); revision[0] = 3;
                await Invoke(0, 180, oldest[0], McpOutcome.Unavailable);
                for (var n = 0; n < 50; n++) replacements.Add((await Invoke(0, 180)).NextCursor!);
                Check(LiveCount() == 459 && replacements.Count == 50, "first50 reclaimed slots at180");
                for (var identity = 1; identity < 5; identity++) await Invoke(identity, 180, oldest[identity]);
                await Invoke(0, 180, replacements[0]);
                for (var n = 0; n < 53; n++) replacements.Add((await Invoke(0, 240)).NextCursor!);
                Check(replacements.Count == 103 && LiveCount() == 512 && handles.Values.Count(h => h.Identity == 0 && h.Revision == 3) == 103, "exact103 target replacement handles and full customer capacity");
                await Invoke(0, 240, expected: McpOutcome.Limited);
                for (var identity = 1; identity < 5; identity++) await Invoke(identity, 240, later[identity]);
                await Invoke(0, 240, replacements[^1]);
                await Invoke(0, 0, oldest[0], McpOutcome.Unavailable);
                await Invoke(0, 0, replacements[0]);
                Check(world.Clock.Raw == TimeSpan.Zero && effective == TimeSpan.FromSeconds(240) && revision.SequenceEqual([3L, 1L, 1L, 1L, 1L]), "rollback retains revised target denial and unchanged four authority revisions");
                Check(calls == 630 && handles.Count == 615, "exact revision-world observed call/successful allocation totals");
            }
            Check(maxOccupancy == 512 && maxIdentityRequests < 60 && maxCustomerRequests < 300 && requests.Count == calls && world.Audit.Events.Count == calls, "external capacity/request ledger and accepted audit final totals");
            Console.WriteLine($"PASS cycle07 {kind}/{(revisionWorld ? "revision" : "expiry")}: {calls} calls, max request window identity={maxIdentityRequests} customer={maxCustomerRequests}, max observed live handles={maxOccupancy}; raw={world.Clock.Raw.TotalSeconds} effective={effective.TotalSeconds}");
            scenarios++;
        }
    }

    private static string IdentityId(IdentityKind kind, int index) => (kind == IdentityKind.NamedUser ? "syn-user-" : "syn-service-") + index;
    private static bool Opaque(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static JsonObject Row(int ordinal) => new()
    {
        ["category"] = new[] { "Summary", "Configuration", "Identity" }[ordinal],
        ["itemId"] = "syn-finding-" + (char)('a' + ordinal),
        ["severity"] = ordinal == 0 ? "High" : "Low",
        ["title"] = "Fictional finding."
    };
    private static byte[] Request(string? cursor)
    {
        var request = new JsonObject
        {
            ["contractVersion"] = "synthetic-published-health-read-v1",
            ["resourceKind"] = "Findings",
            ["assessmentId"] = "syn-assessment-a",
            ["reportVersionId"] = "syn-report-a",
            ["pageSize"] = 2
        };
        if (cursor is not null) request["cursor"] = cursor;
        return JsonSerializer.SerializeToUtf8Bytes(request);
    }
    private sealed class Oracle
    {
        internal readonly byte[] Manifest;
        internal readonly Dictionary<string, byte[]> Payloads = [];
        internal readonly JsonObject Bindings = new()
        {
            ["contractVersion"] = "synthetic-published-health-read-v1",
            ["fixtureKind"] = "SyntheticPublishedFixture",
            ["customerId"] = "syn-customer-a",
            ["projectId"] = "syn-project-a",
            ["environmentId"] = "syn-environment-a",
            ["assessmentId"] = "syn-assessment-a",
            ["reportVersionId"] = "syn-report-a",
            ["baselineVersion"] = "syn-baseline-v1",
            ["catalogVersion"] = "syn-catalog-v1",
            ["scoringProfileVersion"] = "syn-scoring-v1",
            ["maturityProfileVersion"] = "syn-maturity-v1",
            ["applicationVersion"] = "syn-application-v1",
            ["assessmentState"] = "CompletedWithGaps",
            ["approvalState"] = "SyntheticApproved"
        };
        internal Oracle(Action<bool, string> check)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures");
            var golden = File.ReadAllBytes(Path.Combine(path, "golden-hashes.json"));
            check(Hash(golden) == "c50954fcd4881b3c1ca70e6865cd2db4c3a8d2e886c217a2335c5d42db3bdb6f", "pinned unchanged original golden index");
            foreach (var pair in JsonNode.Parse(golden)!.AsObject())
                check(Hash(File.ReadAllBytes(Path.Combine(path, pair.Key))) == pair.Value!.GetValue<string>(), "original fixture byte hash " + pair.Key);
            Manifest = File.ReadAllBytes(Path.Combine(path, "manifest.json"));
            check(Hash(Manifest) == Digest, "independently pinned original manifest bytes/digest");
            var manifest = JsonNode.Parse(Manifest)!.AsObject();
            var scalarBindings = new JsonObject();
            foreach (var pair in manifest.Where(p => p.Key is not "collections" and not "items")) scalarBindings.Add(pair.Key, pair.Value!.DeepClone());
            check(Bindings.Count == 14 && JsonNode.DeepEquals(Bindings, scalarBindings), "explicit fourteen binding atoms match pinned fixture");
            var descriptors = manifest["items"]!.AsArray().Where(d => d!["kind"]!.GetValue<string>() == "Findings").ToArray();
            check(descriptors.Length == 3, "exact original three findings");
            for (var n = 0; n < 3; n++)
            {
                var id = "syn-finding-" + (char)('a' + n);
                var payload = File.ReadAllBytes(Path.Combine(path, id + ".json"));
                Payloads.Add(id, payload);
                check(descriptors[n]!["itemId"]!.GetValue<string>() == id && descriptors[n]!["payloadDigest"]!.GetValue<string>() == Hash(payload), "original ordinal/descriptor/payload hash binding");
                var fixture = JsonNode.Parse(payload)!.AsObject();
                var selected = new JsonObject();
                foreach (var field in Fields) selected.Add(field, fixture[field]!.DeepClone());
                check(JsonNode.DeepEquals(selected, Row(n)), "explicit independent typed minimized row atoms match original fixture");
            }
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
        internal World(Oracle oracle, IdentityKind kind)
        {
            Source = new(oracle); Policy = new(kind); Host = new(Source, Policy, Audit, Clock);
        }
    }
    private sealed class Clock : IMonotonicClock
    {
        internal TimeSpan Raw;
        public TimeSpan Elapsed => Raw;
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
    private sealed class Policy(IdentityKind kind) : IReadPolicy
    {
        private readonly object fence = new();
        internal readonly bool[] Active = [true, true, true, true, true];
        private readonly long[] revisions = [1, 1, 1, 1, 1];
        internal readonly List<Evaluation> Evaluations = [];
        internal readonly List<Commit> Commits = [];
        internal void Change(int identity, bool active, long revision)
        {
            lock (fence) { Active[identity] = active; revisions[identity] = revision; }
        }
        public ReadGrant? GetGrant(Caller caller, ReadRequest request)
        {
            lock (fence)
            {
                var identity = Enumerable.Range(0, 5).Single(i => caller.IdentityId == IdentityId(kind, i));
                var grant = new ReadGrant(kind, caller.IdentityId, revisions[identity], FixtureScope, ResourceKind.Findings, "syn-assessment-a", "syn-report-a", Digest,
                    Active[identity], true, true, true, false, true, Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(), Fields.ToImmutableHashSet(StringComparer.Ordinal));
                Evaluations.Add(new(caller, request, grant)); return grant;
            }
        }
        public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId) => throw new InvalidOperationException("unexpected reference overlay read");
        public bool TryCommit(ReadGrant grant, Action commit)
        {
            lock (fence)
            {
                var identity = Enumerable.Range(0, 5).Single(i => grant.IdentityId == IdentityId(kind, i));
                var accepted = Active[identity] && grant.Revision == revisions[identity];
                Commits.Add(new(grant, accepted));
                if (accepted) commit();
                return accepted;
            }
        }
    }
    private sealed class AuditSpy : IMcpAudit
    {
        internal readonly List<McpAuditEvent> Attempts = [];
        internal readonly List<McpAuditEvent> Events = [];
        internal readonly List<(OperationalFailure, Guid)> Failures = [];
        public bool Complete(McpAuditEvent auditEvent) { Attempts.Add(auditEvent); Events.Add(auditEvent); return true; }
        public void OperationalFailure(OperationalFailure failure, Guid correlationId) => Failures.Add((failure, correlationId));
    }
}
