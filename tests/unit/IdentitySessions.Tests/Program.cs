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
