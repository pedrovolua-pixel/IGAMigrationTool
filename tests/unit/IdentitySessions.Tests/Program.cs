using System.Security.Claims;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;

var now = DateTimeOffset.Parse("2026-10-01T12:00:00+00:00");
var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
var subject = Guid.Parse("22222222-2222-2222-2222-222222222222");
var count = 0;
void Check(bool value) { if (!value) throw new Exception("Session unit assertion failed."); count++; }
AuthenticationTicket Ticket() => new(new ClaimsPrincipal(new ClaimsIdentity([
    new Claim("tid", tenant.ToString()), new Claim("oid", subject.ToString()),
    new Claim("roles", "PilotConsultant")], "Cookie", "oid", "roles")), new AuthenticationProperties(new Dictionary<string, string?>
    {
        [SessionTicket.AuthenticatedUtc] = now.ToString("O"),
        [SessionTicket.ProviderCheckedUtc] = now.ToString("O"),
        [SessionTicket.SecurityVersion] = "1",
        [SessionTicket.MfaCaVerified] = "false"
    }), "Cookie");
void Refused(Action<AuthenticationTicket> mutate)
{
    var ticket = Ticket(); mutate(ticket);
    try { SessionTicket.Validate(ticket, now); throw new Exception("Invalid context accepted."); }
    catch (InvalidOperationException) { count++; }
}
var keys = Enumerable.Range(0, 10000).Select(_ => SessionTicket.NewKey()).ToArray();
Check(keys.Distinct().Count() == keys.Length && keys.All(k => k.Length == 43));
Check(keys.All(k => SessionTicket.HashKey(k)?.Length == 64));
Check(SessionTicket.HashKey(keys[0]) == SessionTicket.HashKey(keys[0]) && SessionTicket.HashKey(keys[0]) != keys[0]);
foreach (var bad in new[] { "", "abc", new string('!', 43), new string('A', 42) + "B", new string('A', 44) })
    Check(SessionTicket.HashKey(bad) is null);
Check(SessionTicket.Validate(Ticket(), now).Subject == new SessionSubject(tenant, subject));
Refused(t => ((ClaimsIdentity)t.Principal.Identity!).AddClaim(new Claim("access_token", "synthetic-forbidden")));
Refused(t => ((ClaimsIdentity)t.Principal.Identity!).AddClaim(new Claim("roles", "Administrator")));
Refused(t => ((ClaimsIdentity)t.Principal.Identity!).AddClaim(new Claim("oid", subject.ToString())));
Refused(t => ((ClaimsIdentity)t.Principal.Identity!).RemoveClaim(t.Principal.FindFirst("tid")!));
Refused(t => t.Properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "synthetic-forbidden" }]));
Refused(t => t.Properties.Items[SessionTicket.SecurityVersion] = "0");
Refused(t => t.Properties.Items[SessionTicket.AuthenticatedUtc] = now.AddSeconds(1).ToString("O"));
Refused(t => t.Properties.Items[SessionTicket.ProviderCheckedUtc] = now.AddMinutes(-15).ToString("O"));
Refused(t => t.Properties.Items[SessionTicket.MfaCaVerified] = "claim-amr");
Refused(t => t.Properties.Items.Remove(SessionTicket.AuthenticatedUtc));
Refused(t => t.Properties.RedirectUri = "https://synthetic.example/external");
Refused(t => t.Properties.RedirectUri = "//synthetic.example/external");
Refused(t => t.Properties.RedirectUri = "/bff/%2fexternal");
var redirected = Ticket(); redirected.Properties.RedirectUri = "/bff/review";
Check(SessionTicket.Validate(redirected, now).Subject == new SessionSubject(tenant, subject));
Check(SessionTicket.Minimal(redirected, now.AddHours(8), now, now).Properties.RedirectUri is null);
var minimal = SessionTicket.Minimal(Ticket(), now.AddHours(0.1), now, now);
Check(minimal.Properties.ExpiresUtc == now.AddHours(0.1));
Check(!minimal.Properties.Items.Keys.Any(k => k.StartsWith(".Token", StringComparison.Ordinal)));
Check(TicketSerializer.Default.Serialize(minimal).Length < 2048);
Console.WriteLine($"PASS: {count} session entropy/context/minimal-ticket checks; persistence verified separately.");

var writer = new SessionSubject(tenant, subject);
var binding = new SecurityAuditBindingV1(Guid.NewGuid(), "synthetic-unit", Guid.NewGuid(), writer, Guid.NewGuid());
var evt = new SecurityAuditEventV1(Guid.NewGuid(), Guid.NewGuid(), now, SecurityAuditActorKind.Human, writer, Guid.NewGuid(),
    SecurityAuditAction.SessionIssued, SecurityAuditOutcome.Succeeded, SecurityAuditReason.None, writer, Guid.NewGuid(), null, long.MaxValue);
var canonical = SecurityAuditCanonical.Event(binding, evt, 1, SecurityAuditCanonical.EmptyDigest);
Check(canonical.Contains("\"securityVersion\":\"9223372036854775807\"", StringComparison.Ordinal));
Check(SecurityAuditCanonical.ReadEvent(binding, canonical, 1, SecurityAuditCanonical.EmptyDigest) == evt);
Check(SecurityAuditCanonical.Hash(canonical).Length == 64 && canonical == SecurityAuditCanonical.Event(binding, evt, 1, SecurityAuditCanonical.EmptyDigest));
void AuditRefused(Action action)
{
    try { action(); throw new Exception("Invalid audit contract accepted."); }
    catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException or ArgumentException or FormatException) { count++; }
}
foreach (var invalid in new[] { evt with { EventId = Guid.Empty }, evt with { OperationId = Guid.Empty }, evt with { Action = (SecurityAuditAction)99 },
    evt with { ActorKind = SecurityAuditActorKind.Anonymous }, evt with { Actor = null }, evt with { Outcome = SecurityAuditOutcome.Denied },
    evt with { Reason = SecurityAuditReason.Conflict }, evt with { SessionReference = null }, evt with { Target = null },
    evt with { PreviousSessionReference = Guid.NewGuid() }, evt with { SecurityVersion = 0 }, evt with { EventAtUtc = now.ToOffset(TimeSpan.FromHours(1)) },
    evt with { CustomerId = Guid.NewGuid() }, evt with { CustomerId = Guid.NewGuid(), ProjectId = Guid.NewGuid() } })
    AuditRefused(() => SecurityAuditCanonical.Event(binding, invalid, 1, SecurityAuditCanonical.EmptyDigest));
var receiptRequest = new OperationReceiptRequestV1(evt.OperationId, writer, SecurityAuditActorKind.Human, writer, SecurityAuditAction.SessionIssued,
    writer, SecurityAuditCanonical.Hash("synthetic-closed-command"));
var receipt = new OperationReceiptV1(receiptRequest, SecurityAuditOutcome.Succeeded, now, long.MaxValue, long.MaxValue, evt.SessionReference, [evt.EventId]);
var receiptJson = SecurityAuditCanonical.Receipt(receipt);
Check(SecurityAuditCanonical.ReadReceipt(receiptJson).SecurityVersion == long.MaxValue);
Check(receiptJson.Contains("\"securityVersion\":\"9223372036854775807\"", StringComparison.Ordinal));
foreach (var bad in new[] {
    receiptJson.Replace("\"schemaVersion\":\"operation-receipt-v1\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
    receiptJson.Replace("\"schemaVersion\":\"operation-receipt-request-v1\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
    receiptJson.Replace("\"securityVersion\":\"9223372036854775807\"", "\"securityVersion\":9223372036854775807", StringComparison.Ordinal),
    receiptJson.Replace("\"securityVersion\":\"9223372036854775807\"", "\"securityVersion\":\"09223372036854775807\"", StringComparison.Ordinal),
    receiptJson.Replace("\"securityVersion\":\"9223372036854775807\"", "\"securityVersion\":\"9223372036854775807\",\"securityVersion\":\"1\"", StringComparison.Ordinal),
    receiptJson.Replace("\"outcome\":\"Succeeded\",", "", StringComparison.Ordinal),
    receiptJson.Replace("\"outcome\":\"Succeeded\"", "\"outcome\":\"Succeeded\",\"rawClaims\":\"synthetic-prohibited\"", StringComparison.Ordinal),
    receiptJson.Replace("\"issuer\":{", "\"issuer\":{\"objectId\":\"" + subject.ToString("D") + "\",", StringComparison.Ordinal),
    receiptJson.Replace(".0000000Z", "+00:00", StringComparison.Ordinal) })
    AuditRefused(() => SecurityAuditCanonical.ReadReceipt(bad));
var checkpoint = new AuditCheckpointV1(binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference, 1, SecurityAuditCanonical.Hash(canonical), now, Guid.NewGuid());
var entry = new AuditIntegrityEntryV1(evt.EventId, binding.StreamId, 1, SecurityAuditCanonical.EmptyDigest, checkpoint.EventSha256, canonical, null);
AuditIntegrityVerifier.Verify(binding, checkpoint, 1, checkpoint.EventSha256, [entry], [receipt]); count++;
foreach (var field in new[] { "applicationClientId", "writerBindingReference", "streamId", "eventId", "operationId", "previousEventSha256", "schemaVersion", "scope", "securityVersion" })
{
    var node = System.Text.Json.Nodes.JsonNode.Parse(canonical)!;
    node[field] = field.EndsWith("Id", StringComparison.Ordinal) || field == "writerBindingReference" ? Guid.NewGuid().ToString("D") : "synthetic-invalid";
    var corrupted = node.ToJsonString(); var hash = SecurityAuditCanonical.Hash(corrupted);
    AuditRefused(() => AuditIntegrityVerifier.Verify(binding, checkpoint with { EventSha256 = hash }, 1, hash, [entry with { CanonicalEvent = corrupted, EventSha256 = hash }], [receipt]));
}
AuditRefused(() => AuditIntegrityVerifier.Verify(binding, checkpoint, 1, checkpoint.EventSha256, [entry], [receipt with { Request = receiptRequest with { OperationId = Guid.NewGuid() } }]));
AuditRefused(() => AuditIntegrityVerifier.Verify(binding, checkpoint, 1, checkpoint.EventSha256, [entry], [receipt with { SecurityVersion = 1 }]));
AuditRefused(() => AuditIntegrityVerifier.Verify(binding, checkpoint, 1, checkpoint.EventSha256, [entry], [receipt with { NewSessionReference = Guid.NewGuid() }]));
AuditRefused(() => AuditIntegrityVerifier.Verify(binding, checkpoint, 1, checkpoint.EventSha256, [entry], []));
Console.WriteLine($"PASS: {count} total session and closed audit/canonical/receipt/witness checks.");
