using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using SyntheticMcp;

var assertions = 0;
void Check(bool condition, string name) { assertions++; if (!condition) throw new InvalidOperationException($"FAIL {name}"); }
void Denied(McpResult result, McpOutcome outcome, string name) => Check(result.Outcome == outcome && result.Envelope.IsEmpty && result.NextCursor is null, name);
string[] Ids(McpResult result)
{
    using var document = JsonDocument.Parse(result.Envelope.AsMemory());
    return document.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetString()!).ToArray();
}

// T03/T04: separate grants, all deny flags, identity and closed public input.
foreach (var alter in new Func<ReadGrant, ReadGrant?>[] { _ => null, g => g with { ActiveIdentity = false }, g => g with { ActiveAssignment = false }, g => g with { CustomerMcpPolicy = false }, g => g with { ResourceAvailable = false }, g => g with { ReadBlocked = true }, g => g with { ActionAllowed = false }, g => g with { Categories = [] } })
{
    var fixture = new Fixture { ChangeGrant = alter };
    Denied(await fixture.Read(), McpOutcome.Unavailable, "distinct-action-deny");
    Check(fixture.ManifestReads == 0 && fixture.ItemReads == 0 && fixture.Events.Count == 1, "deny-before-content-once");
}
foreach (var kind in new[] { IdentityKind.Anonymous, IdentityKind.ShareLink, IdentityKind.Support, IdentityKind.Workload, (IdentityKind)99 })
{
    var fixture = new Fixture();
    Denied(await fixture.Read(caller: fixture.Caller with { Kind = kind }), McpOutcome.Unavailable, "unsupported-identity");
    Check(fixture.PolicyCalls == 0 && fixture.ManifestReads == 0, "identity-before-policy-source");
}
foreach (var resource in new[] { ResourceKind.Coverage, ResourceKind.PublishedStatus, ResourceKind.Scores })
{
    var fixture = new Fixture { ChangeGrant = g => g with { Categories = ImmutableHashSet.Create(EvidenceCategory.Identity) } };
    Denied(await fixture.Read(Fixture.Request(resource)), McpOutcome.Unavailable, "no-authorized-kind-metadata");
    Check(fixture.ItemReads == 0, "category-before-payload");
}
foreach (var alter in new Func<ReadGrant, ReadGrant>[] { g => g with { Scope = new("bad-scope", "syn-p", "syn-e") }, g => g with { IdentityId = "syn-foreign" }, g => g with { AssessmentId = "syn-other" }, g => g with { Kind = ResourceKind.Findings }, g => g with { Fields = ImmutableHashSet.Create("protected-sentinel") }, g => g with { Categories = ImmutableHashSet.Create((EvidenceCategory)99) } })
{
    var fixture = new Fixture { ChangeGrant = g => alter(g) };
    Denied(await fixture.Read(), McpOutcome.DependencyUnavailable, "invalid-trusted-binding");
    Check(fixture.ManifestReads == 0 && !JsonSerializer.Serialize(fixture.Events).Contains("protected-sentinel", StringComparison.Ordinal), "invalid-port-safe");
}
var legitimate = new Fixture();
Check((await legitimate.Read()).Outcome == McpOutcome.Success, "nonblocking-retention-read");
var empty = new Fixture(0);
Check(Ids(await empty.Read()).Length == 0, "authorized-empty-collection");
var validText = Encoding.UTF8.GetString(Fixture.Request());
var hostile = new List<byte[]> { Array.Empty<byte>(), new byte[] { 0xff }, Encoding.UTF8.GetBytes("[]"), new byte[16385] };
foreach (var method in new[] { "start", "run", "cancel", "resume", "acknowledgment", "publish", "comment", "export", "link", "task", "remediate", "migrate", "raw", "tool", "coverage", "0", "6", "ProtectedReferences.read", "https://protected-sentinel" })
    hostile.Add(Encoding.UTF8.GetBytes(validText.Replace("Coverage", method, StringComparison.Ordinal)));
foreach (var extra in new[] { "method", "scope", "customerId", "role", "url", "path", "raw", "storage", "page_size", "ResourceKind", "contractVersion" })
    hostile.Add(Encoding.UTF8.GetBytes(validText[..^1] + $",\"{extra}\":\"protected-sentinel\"}}"));
foreach (var size in new[] { "0", "101", "-1", "1.5", "\"1\"", "null", "true", "{}", "1e400" })
    hostile.Add(Encoding.UTF8.GetBytes(validText[..^1] + $",\"pageSize\":{size}}}"));
hostile.Add(Fixture.Request(ResourceKind.PublishedStatus, 1));
hostile.Add(Fixture.Request(ResourceKind.Scores, cursor: "bad"));
foreach (var request in hostile)
{
    var fixture = new Fixture();
    Denied(await fixture.Read(request), McpOutcome.InvalidRequest, "closed-request");
    Check(fixture.PolicyCalls == 0 && fixture.ManifestReads == 0 && fixture.Events.Count == 1 && fixture.Events[0].ResourceKind is null, "invalid-input-zero-source-safe-audit");
}

// T06/T07: repeatable ordered snapshot, immutable bindings and every-page authority.
var paging = new Fixture(4);
var first = await paging.Read(Fixture.Request(size: 1));
Check(Ids(first).SequenceEqual(new[] { "syn-coverage-0000" }) && first.NextCursor is { Length: 64 }, "first-opaque-page");
Check(first.NextCursor!.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && first.NextCursor != paging.Manifest.Digest, "random-handle-not-authority");
var second = await paging.Read(Fixture.Request(size: 1, cursor: first.NextCursor));
var replay = await paging.Read(Fixture.Request(size: 1, cursor: first.NextCursor));
Check(Ids(second).SequenceEqual(Ids(replay)) && Ids(second)[0] == "syn-coverage-0001", "repeatable-cursor");
var third = await paging.Read(Fixture.Request(size: 1, cursor: second.NextCursor));
var fourth = await paging.Read(Fixture.Request(size: 1, cursor: third.NextCursor));
Check(Ids(third)[0] == "syn-coverage-0002" && Ids(fourth)[0] == "syn-coverage-0003" && fourth.NextCursor is null, "ordered-final-page");
foreach (var request in new[] { Fixture.Request(size: 2, cursor: first.NextCursor), Fixture.Request(ResourceKind.Findings, 1, first.NextCursor), Fixture.Request(size: 1, cursor: "x"), Fixture.Request(size: 1, cursor: new string('a', 64)), Fixture.Request(size: 1, cursor: first.NextCursor!.ToUpperInvariant()), Fixture.Request(size: 1, cursor: new string('z', 1000)) })
    Denied(await paging.Read(request), McpOutcome.Unavailable, "cursor-binding-denial");
Denied(await paging.Read(Fixture.Request(size: 1, cursor: first.NextCursor), paging.Caller with { IdentityId = "syn-other" }), McpOutcome.Unavailable, "foreign-identity-cursor");
Denied(await new Fixture().Read(Fixture.Request(size: 1, cursor: first.NextCursor)), McpOutcome.Unavailable, "restart-cursor-loss");
lock (paging.PolicyFence) paging.Revision++;
Denied(await paging.Read(Fixture.Request(size: 1, cursor: first.NextCursor)), McpOutcome.Unavailable, "revision-revoke-regrant");
var race = new Fixture { BeforeCommit = null };
race.BeforeCommit = () => race.Revision++;
Denied(await race.Read(Fixture.Request(size: 1)), McpOutcome.Unavailable, "revocation-wins-emission-fence");
Check(race.Events.Count == 1 && race.Events[0].Scope is null, "stale-authority-audit-generic");
var deleted = new Fixture();
var deletedPage = await deleted.Read(Fixture.Request(size: 1));
deleted.MissingSource = true;
Denied(await deleted.Read(Fixture.Request(size: 1, cursor: deletedPage.NextCursor)), McpOutcome.Unavailable, "source-deleted-between-pages");
var timed = new Fixture();
var timedPage = await timed.Read(Fixture.Request(size: 1));
timed.Time = TimeSpan.FromSeconds(299.999);
Check((await timed.Read(Fixture.Request(size: 1, cursor: timedPage.NextCursor))).Outcome == McpOutcome.Success, "cursor-before-expiry");
timed.Time = TimeSpan.FromSeconds(300);
Denied(await timed.Read(Fixture.Request(size: 1, cursor: timedPage.NextCursor)), McpOutcome.Unavailable, "exact-expiry");
timed.Time = TimeSpan.Zero;
Denied(await timed.Read(Fixture.Request(size: 1, cursor: timedPage.NextCursor)), McpOutcome.Unavailable, "rollback-cannot-restore-cursor");

// T08/T09: exact rolling edges, scoped denied admission and customer atomic pairing.
var identityBudget = new Fixture { ChangeGrant = g => g with { ActionAllowed = false } };
for (var i = 0; i < 60; i++) Denied(await identityBudget.Read(), McpOutcome.Unavailable, "denied-consumes-budget");
Denied(await identityBudget.Read(), McpOutcome.Limited, "identity-plus-one");
identityBudget.Time = TimeSpan.FromSeconds(59.999);
Denied(await identityBudget.Read(), McpOutcome.Limited, "rolling-before-edge");
identityBudget.Time = TimeSpan.FromSeconds(60);
Denied(await identityBudget.Read(), McpOutcome.Unavailable, "rolling-exact-edge");
identityBudget.Time = TimeSpan.FromSeconds(1);
for (var i = 0; i < 59; i++) Denied(await identityBudget.Read(), McpOutcome.Unavailable, "rollback-does-not-age-budget");
Denied(await identityBudget.Read(), McpOutcome.Limited, "rollback-no-budget-reset");
var customerBudget = new Fixture { ChangeGrant = g => g with { ActionAllowed = false } };
for (var i = 0; i < 300; i++) Denied(await customerBudget.Read(caller: customerBudget.Caller with { IdentityId = $"syn-user-{i / 60}" }), McpOutcome.Unavailable, "customer-budget-admission");
var newCaller = customerBudget.Caller with { IdentityId = "syn-fresh" };
Denied(await customerBudget.Read(caller: newCaller), McpOutcome.Limited, "customer-plus-one");
customerBudget.ChangeGrant = g => g with { Scope = g.Scope with { CustomerId = "syn-unrelated" }, ActionAllowed = false };
for (var i = 0; i < 60; i++) Denied(await customerBudget.Read(caller: newCaller), McpOutcome.Unavailable, "failed-pair-does-not-charge-identity");
var ingress = new Fixture { ChangeGrant = _ => null };
for (var i = 0; i < 600; i++) Denied(await ingress.Read(caller: ingress.Caller with { IdentityId = $"syn-ingress-{i}" }), McpOutcome.Unavailable, "bounded-unresolved-ingress");
Denied(await ingress.Read(), McpOutcome.Limited, "ingress-plus-one");
ingress.ChangeGrant = g => g with { ActionAllowed = false };
Denied(await ingress.Read(), McpOutcome.Unavailable, "resolved-customer-independent-ingress");

// Real blocked tasks establish simultaneous reads, not counters fabricated by the test.
var concurrent = new Fixture { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
using (var cancel = new CancellationTokenSource())
{
    var reads = Enumerable.Range(0, 4).Select(_ => concurrent.Read(token: cancel.Token)).ToArray();
    Check(concurrent.ManifestReads == 4 && reads.All(t => !t.IsCompleted), "four-active-real-tasks");
    Denied(await concurrent.Read(), McpOutcome.Limited, "identity-concurrency-plus-one");
    cancel.Cancel();
    foreach (var result in await Task.WhenAll(reads)) Denied(result, McpOutcome.Cancelled, "blocked-cancellation");
    concurrent.Block.SetResult(); concurrent.Block = null;
    Check((await concurrent.Read()).Outcome == McpOutcome.Success, "cancel-releases-lease");
}
var customerConcurrency = new Fixture { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
using (var cancel = new CancellationTokenSource())
{
    var reads = Enumerable.Range(0, 16).Select(i => customerConcurrency.Read(caller: customerConcurrency.Caller with { IdentityId = $"syn-active-{i / 4}" }, token: cancel.Token)).ToArray();
    Check(customerConcurrency.ManifestReads == 16 && reads.All(t => !t.IsCompleted), "sixteen-customer-real-tasks");
    Denied(await customerConcurrency.Read(caller: customerConcurrency.Caller with { IdentityId = "syn-seventeenth" }), McpOutcome.Limited, "customer-concurrency-plus-one");
    customerConcurrency.ChangeGrant = g => g with { Scope = g.Scope with { CustomerId = "syn-other" }, ActionAllowed = false };
    Denied(await customerConcurrency.Read(), McpOutcome.Unavailable, "other-customer-concurrent-progress");
    cancel.Cancel();
    foreach (var result in await Task.WhenAll(reads)) Denied(result, McpOutcome.Cancelled, "customer-lease-cancellation");
    customerConcurrency.Block.SetResult();
}
var deadlineFixture = new Fixture { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
var deadlineRead = deadlineFixture.Read();
deadlineFixture.Time = TimeSpan.FromSeconds(5);
deadlineFixture.Block.SetResult();
Denied(await deadlineRead, McpOutcome.DependencyUnavailable, "exact-fake-deadline");
deadlineFixture.Block = null;
Check((await deadlineFixture.Read()).Outcome == McpOutcome.Success, "deadline-releases-lease");
var realDeadline = new Fixture { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
var watch = System.Diagnostics.Stopwatch.StartNew();
Denied(await realDeadline.Read(), McpOutcome.DependencyUnavailable, "real-deadline-ignoring-source");
Check(watch.Elapsed >= TimeSpan.FromSeconds(4.8) && watch.Elapsed < TimeSpan.FromSeconds(10), "real-deadline-bounded");
realDeadline.Block.SetResult(); realDeadline.Block = null;
Check((await realDeadline.Read()).Outcome == McpOutcome.Success, "real-deadline-release");

// Registry bounds and non-sliding lifetime; failure reservation cleanup is observable.
var capacity = new Fixture();
for (var i = 0; i < 128; i++)
{
    if (i is 50 or 100) capacity.Time += TimeSpan.FromSeconds(60);
    Check((await capacity.Read(Fixture.Request(size: 1))).Outcome == McpOutcome.Success, "cursor-identity-capacity");
}
Denied(await capacity.Read(Fixture.Request(size: 1)), McpOutcome.Limited, "cursor-identity-plus-one");
Check(capacity.Events[^1].Outcome == McpOutcome.Limited, "capacity-audits-actual-outcome");
capacity.Time = TimeSpan.FromSeconds(300);
Check((await capacity.Read(Fixture.Request(size: 1))).Outcome == McpOutcome.Success, "expired-capacity-reclaimed");
var customerCapacity = new Fixture();
for (var i = 0; i < 512; i++)
{
    if (i > 0 && i % 250 == 0) customerCapacity.Time += TimeSpan.FromSeconds(60);
    Check((await customerCapacity.Read(Fixture.Request(size: 1), customerCapacity.Caller with { IdentityId = $"syn-cursor-{i % 5}" })).Outcome == McpOutcome.Success, "cursor-customer-capacity");
}
Denied(await customerCapacity.Read(Fixture.Request(size: 1), customerCapacity.Caller with { IdentityId = "syn-cursor-fresh" }), McpOutcome.Limited, "cursor-customer-plus-one");
var chain = new Fixture(5);
var chainFirst = await chain.Read(Fixture.Request(size: 1));
chain.Time = TimeSpan.FromSeconds(299);
var chainNext = await chain.Read(Fixture.Request(size: 1, cursor: chainFirst.NextCursor));
chain.Time = TimeSpan.FromSeconds(300);
Denied(await chain.Read(Fixture.Request(size: 1, cursor: chainNext.NextCursor)), McpOutcome.Unavailable, "continuation-does-not-slide-expiry");

// T09/T10: UTF-8 budget, audit every outcome, dependency failure no cursor and cleanup.
var unicode = new Fixture(100, 120, true);
var unicodeResult = await unicode.Read(Fixture.Request(size: 100));
Check(unicodeResult.Outcome == McpOutcome.Success && unicodeResult.Envelope.Length <= 262144 && Encoding.UTF8.GetString(unicodeResult.Envelope.AsSpan()).Length < unicodeResult.Envelope.Length, "utf8-byte-under-cap");
var oversized = new Fixture(100, 130, true);
Denied(await oversized.Read(Fixture.Request(size: 100)), McpOutcome.DependencyUnavailable, "utf8-byte-over-cap-no-partial");
foreach (var throwAudit in new[] { false, true })
{
    var fixture = new Fixture { AuditAvailable = false, ThrowAudit = throwAudit };
    for (var i = 0; i < 128; i++)
    {
        if (i is 50 or 100) fixture.Time += TimeSpan.FromSeconds(60);
        Denied(await fixture.Read(Fixture.Request(size: 1)), McpOutcome.DependencyUnavailable, "audit-failure-no-content-cursor");
    }
    Check(fixture.Events.Count == 128 && fixture.Failures.All(f => f == OperationalFailure.AuditUnavailable), "audit-once-safe-signal");
    fixture.AuditAvailable = true; fixture.ThrowAudit = false;
    Check((await fixture.Read(Fixture.Request(size: 1))).Outcome == McpOutcome.Success, "audit-failure-releases-cursor-reservations");
}
foreach (var mode in new[] { "source", "policy", "clock", "negative-clock" })
{
    var fixture = new Fixture { ThrowSource = mode == "source", ThrowPolicy = mode == "policy", ThrowClock = mode == "clock", Time = mode == "negative-clock" ? TimeSpan.FromTicks(-1) : TimeSpan.Zero };
    Denied(await fixture.Read(), McpOutcome.DependencyUnavailable, "typed-dependency-failure");
    Check(fixture.Events.Count == 1 && !JsonSerializer.Serialize(fixture.Events).Contains("protected-", StringComparison.Ordinal), "failure-no-exception-payload");
}
var auditFixture = new Fixture { ChangeGrant = g => g with { Fields = ImmutableHashSet.Create("assessed") } };
var audited = await auditFixture.Read(Fixture.Request(size: 1));
Check(audited.Outcome == McpOutcome.Success && auditFixture.Events.Count == 1 && auditFixture.Events[0].ReturnedFields.SequenceEqual(new[] { "assessed", "category", "itemId" }) && auditFixture.Events[0].RedactedFields.Contains("label"), "safe-field-name-audit");
Check(!JsonSerializer.Serialize(auditFixture.Events).Contains("Fictional", StringComparison.Ordinal) && auditFixture.Events[0].CorrelationId == auditFixture.Caller.CorrelationId, "audit-payload-free-server-correlation");
await auditFixture.Read(Fixture.Request(size: 1));
Check(auditFixture.Events.Count == 2 && auditFixture.PolicyCalls == 2, "distinct-retry-distinct-audit-policy");
Console.WriteLine($"PASS boundary: {assertions} synthetic policy/request/paging/race/limit/audit assertions; no listener or production adapter");
