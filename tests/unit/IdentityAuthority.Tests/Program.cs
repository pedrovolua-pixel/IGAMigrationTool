using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using IdentityAuthority;
using IdentityPolicy;
using IdentitySessions;

var checks = 0;
void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
void Denied(Action action, string label)
{ try { action(); throw new Exception("Expected denial: " + label); } catch (Exception e) when (e is InvalidOperationException or JsonException or ArgumentException) { checks++; } }
var clock = new MutableClock();
var subject = new SessionSubject(Guid.NewGuid(), Guid.NewGuid());
var actor = new SessionSubject(subject.TenantId, Guid.NewGuid());
var attribution = new DecisionAttributionV1(Guid.NewGuid(), actor, clock.GetUtcNow());
var enrollment = new SubjectEnrollmentV1("subject-enrollment-v1", subject, 1, EnrollmentLifecycle.Active,
    OrganizationalOrigin.InternalOrganizational, subject.TenantId, clock.GetUtcNow(), attribution);
var roles = Enum.GetValues<CoarseAppRole>().Select(r => new ProviderAppRoleV1(Guid.NewGuid(), r, true)).ToImmutableArray();
var binding = new ProviderRoleBindingV1("provider-role-binding-v1", subject.TenantId, Guid.NewGuid(), Guid.NewGuid(), roles, 1, new string('a', 64), Guid.NewGuid());
AuthorityCodec.Validate(enrollment); AuthorityCodec.Validate(binding);
var wire = AuthorityCodec.Serialize(enrollment);
Check(AuthorityCodec.Parse<SubjectEnrollmentV1>(Encoding.UTF8.GetBytes(wire)) == enrollment, "Closed canonical enrollment roundtrip");
foreach (var member in JsonDocument.Parse(wire).RootElement.EnumerateObject().Select(p => p.Name))
{
    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wire)!; dict.Remove(member);
    Denied(() => AuthorityCodec.Parse<SubjectEnrollmentV1>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dict))), "Required member " + member);
}
foreach (var malformed in new[] { wire.Replace("subject-enrollment-v1", "v2"), wire[..^1] + ",\"extra\":true}", wire[..^1] + ",\"revision\":1}",
    wire.Replace("\"Active\"", "666"), wire.Replace("\"Active\"", "\"Unknown\""), wire.Replace("\"Active\"", "\"active\""), wire.Replace("\"Active\"", "\"1\""), wire.Replace(".0000000Z", ".0000000+00:00"), wire.Replace(subject.ObjectId.ToString(), subject.ObjectId.ToString().ToUpperInvariant()) })
    Denied(() => AuthorityCodec.Parse<SubjectEnrollmentV1>(Encoding.UTF8.GetBytes(malformed)), "Schema/enum/duplicate/time/id corpus");
foreach (var invalid in new[] { enrollment with { Revision = 0 }, enrollment with { HomeTenantId = Guid.NewGuid() }, enrollment with { HomeTenantId = AuthorityCodec.ConsumerTenant }, enrollment with { Subject = default }, enrollment with { Attribution = attribution with { ApprovedDecisionId = Guid.Empty } } })
    Denied(() => AuthorityCodec.Validate(invalid), "Invalid enrollment invariant");
foreach (var invalid in new[] { binding with { AppRoles = roles.RemoveAt(0) }, binding with { AppRoles = [roles[0], roles[0], roles[2], roles[3]] }, binding with { AppRoles = roles.SetItem(0, roles[0] with { Enabled = false }) }, binding with { AppRoles = roles.SetItem(0, roles[0] with { AppRoleId = Guid.Empty }) }, binding with { ManifestSha256 = new string('A', 64) } })
    Denied(() => AuthorityCodec.Validate(invalid), "Invalid role binding");
var scope = new HumanScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
var assignment = new HumanAssignmentV1("human-assignment-v1", Guid.NewGuid(), subject, scope, HumanRole.Consultant, true,
    clock.GetUtcNow(), clock.GetUtcNow().AddDays(3), ["normalized"], [], 1, attribution);
foreach (var invalid in new[] { assignment with { EvidenceCategories = ["normalized", "normalized"] }, assignment with { Conditions = [GrantCondition.AuditedOverride, GrantCondition.AuditedOverride] }, assignment with { ExpiresAtUtc = assignment.StartsAtUtc }, assignment with { Scope = scope with { CustomerId = Guid.Empty } }, assignment with { Role = (HumanRole)99 } })
    Denied(() => AuthorityCodec.Validate(invalid), "Exact assignment invariant");
var guestLifecycle = new GuestLifecycleV1("guest-lifecycle-v1", subject, actor, clock.GetUtcNow(), clock.GetUtcNow().AddDays(90), clock.GetUtcNow(), Guid.NewGuid(), 1, false, attribution);
foreach (var invalid in new[] { guestLifecycle with { Sponsor = subject }, guestLifecycle with { ExpiresAtUtc = guestLifecycle.AssignedAtUtc.AddDays(90).AddTicks(1) },
    guestLifecycle with { LastReviewedAtUtc = guestLifecycle.AssignedAtUtc.AddTicks(-1) }, guestLifecycle with { EngagementRevision = 0 }, guestLifecycle with { EngagementReference = Guid.Empty },
    guestLifecycle with { Sponsor = default }, guestLifecycle with { AssignedAtUtc = guestLifecycle.AssignedAtUtc.ToOffset(TimeSpan.FromHours(1)) } })
    Denied(() => AuthorityCodec.Validate(invalid), "Closed external lifecycle invariant");
var decision = new AuthorityDecisionV1("authority-decision-v1", Guid.NewGuid(), 1, subject, scope, AuthorityOperation.SetAssignment,
    attribution.ApprovedDecisionId, AuthorityReason.ApprovedAssignment, actor, new string('0', 64));
var command = new AuthorityCommandV1("authority-command-v1", decision, null, assignment, null);
command = command with { Decision = decision with { PayloadSha256 = AuthorityCodec.PayloadDigest(command) } };
AuthorityCodec.Validate(command);
Denied(() => AuthorityCodec.Validate(command with { Assignment = assignment with { EvidenceCategories = ["protected"] } }), "Digest binds categories");
Denied(() => AuthorityCodec.Validate(command with { Assignment = assignment with { Conditions = [GrantCondition.DedicatedProtectedPermission] } }), "Digest binds conditions");
var source = new FixtureProvider();
var reader = new SyntheticProviderReader(source, binding, clock);
string User(bool enabled = true, string type = "Member", string? invitation = null, string? cutoff = null) => JsonSerializer.Serialize(new
{ id = subject.ObjectId.ToString("D"), accountEnabled = enabled, userType = type, externalUserState = invitation, signInSessionsValidFromDateTime = cutoff ?? clock.GetUtcNow().AddMinutes(-10).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'") });
string Role(string kind = "User", Guid? principal = null, Guid? resource = null, Guid? role = null) => JsonSerializer.Serialize(new
{ id = "synthetic-assignment", appRoleId = (role ?? roles[0].AppRoleId).ToString("D"), principalId = (principal ?? subject.ObjectId).ToString("D"), principalType = kind, resourceId = (resource ?? binding.ResourceServicePrincipalId).ToString("D") });
string Page(string role, string? next = null) => "{\"value\":[" + role + "]" + (next is null ? "}" : ",\"@odata.nextLink\":" + JsonSerializer.Serialize(next) + "}");
source.User = User(); source.Pages = [Page(Role())];
var valid = await reader.ReadAsync(enrollment, 1, 1);
Check(valid is not null && valid.Observation.StartedAtUtc == clock.GetUtcNow(), "Provider start before first await");
foreach (var badUser in new[] { "{}", User().Replace("true", "\"true\""), User().Replace(subject.ObjectId.ToString(), Guid.NewGuid().ToString()), User(type: "unknown"), User(type: "member"), User(type: "0"), User(invitation: "accepted"), User(invitation: "0"), User(cutoff: "bad"), User(cutoff: clock.GetUtcNow().AddSeconds(1).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'")), User().Replace("\"signInSessionsValidFromDateTime\":", "\"extra\":null,\"signInSessionsValidFromDateTime\":"), User().Replace("\"accountEnabled\":true", "\"accountEnabled\":true,\"accountEnabled\":true") })
{ source.User = badUser; Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Malformed user/cutoff/type denied"); }
source.User = User();
foreach (var badRole in new[] { Role("Group"), Role(principal: Guid.NewGuid()), Role(resource: Guid.NewGuid()), Role(role: Guid.NewGuid()), Role(role: Guid.Empty), Role().Replace("\"User\"", "1"), Role().Replace("\"id\":", "\"extra\":true,\"id\":") })
{ source.Pages = [Page(badRole)]; Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Wrong direct role binding denied"); }
foreach (var continuation in new[] { "http://graph.microsoft.com/x", "https://evil.invalid/x", "https://graph.microsoft.com/v1.0/users", reader.RoleQuery(subject).AbsoluteUri, reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=abc&$filter=x", reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=abc#fragment", reader.RoleQuery(subject).AbsoluteUri.Replace("https://", "https://user@") + "&$skiptoken=x" })
{ source.Pages = [Page(Role(), continuation)]; Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Hostile/malformed continuation denied"); }
source.Pages = [Page(Role(), reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=abc"), "{\"value\":[]}"];
Check(await reader.ReadAsync(enrollment, 1, 1) is not null, "Exact fixed-query continuation accepted");
source.Pages = [Page(Role(), reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=abc"), Page(Role())];
Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Duplicate paged role denied");
source.Pages = [Page(Role())];
var external = enrollment with { Origin = OrganizationalOrigin.ExternalOrganizational, HomeTenantId = Guid.NewGuid() };
source.User = User(type: "Member", invitation: "Accepted");
Check(await reader.ReadAsync(external, 1, 1) is null, "External Member cannot bypass home evidence");
var home = new FixtureHome();
var guestReader = new SyntheticProviderReader(source, binding, clock, home);
home.Evidence = new(subject, external.HomeTenantId, 1, 1, clock.GetUtcNow(), clock.GetUtcNow().AddHours(-1), true, true);
Check(await guestReader.ReadAsync(external, 1, 1) is not null, "Exact reviewed synthetic organizational external Member");
foreach (var invalid in new[] { home.Evidence with { HomeTenantId = AuthorityCodec.ConsumerTenant }, home.Evidence with { Subject = actor }, home.Evidence with { SecurityVersion = 2 }, home.Evidence with { EnrollmentRevision = 2 }, home.Evidence with { Complete = false }, home.Evidence with { Active = false }, home.Evidence with { CheckedAtUtc = clock.GetUtcNow().AddMinutes(-15) }, home.Evidence with { CutoffUtc = clock.GetUtcNow().AddSeconds(1) } })
{ home.Evidence = invalid; Check(await guestReader.ReadAsync(external, 1, 1) is null, "Ambiguous home status denied"); }
source.User = User(); source.Delay = () => clock.Advance(TimeSpan.FromMinutes(15));
Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Network read crossing exact15-minute boundary denied");
source.Delay = null;
foreach (var exception in new Exception[] { new TimeoutException(), new IOException(), new OperationCanceledException(), new InvalidOperationException("Synthetic403") })
{ source.Failure = exception; Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Synthetic unavailable/timeout/error cannot manufacture freshness"); }
source.Failure = null; source.User = null;
Check(await reader.ReadAsync(enrollment, 1, 1) is null, "Synthetic404/null provider response denies");
checks += await GraphProviderProjectionChecks.RunAsync();
Console.WriteLine($"PASS {checks} closed authority/provider assertions");

sealed class MutableClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan span) => now += span;
}
sealed class FixtureProvider : ISyntheticProviderSource
{
    public string? User { get; set; }
    public string[] Pages { get; set; } = [];
    public Action? Delay { get; set; }
    public Exception? Failure { get; set; }
    private int page;
    public ValueTask<ReadOnlyMemory<byte>?> ReadUserAsync(SessionSubject subject, CancellationToken cancellationToken)
    { page = 0; if (Failure is not null) throw Failure; Delay?.Invoke(); return ValueTask.FromResult<ReadOnlyMemory<byte>?>(User is null ? null : Encoding.UTF8.GetBytes(User)); }
    public ValueTask<ReadOnlyMemory<byte>?> ReadDirectRolesPageAsync(Uri query, CancellationToken cancellationToken)
        => ValueTask.FromResult<ReadOnlyMemory<byte>?>(page >= Pages.Length ? null : Encoding.UTF8.GetBytes(Pages[page++]));
}
sealed class FixtureHome : IHomeStatusEvidenceSource
{
    public HomeStatusEvidenceV1? Evidence { get; set; }
    public ValueTask<HomeStatusEvidenceV1?> ReadAsync(SubjectEnrollmentV1 enrollment, long version, CancellationToken cancellationToken) => ValueTask.FromResult(Evidence);
}
