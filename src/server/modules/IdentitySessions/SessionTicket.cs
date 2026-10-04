using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentitySessions;

public readonly record struct SessionSubject(Guid TenantId, Guid ObjectId);

// The adapter must read current authoritative product eligibility (including
// guest sponsorship/expiry/review), not claims, request fields or cached grants.
public interface ISessionAdmissionPolicy
{
    ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken);
}

public sealed record SessionDescriptor(Guid Reference, DateTimeOffset CreatedUtc,
    DateTimeOffset LastSeenUtc, DateTimeOffset AbsoluteExpiresUtc, bool Revoked);

public static class SessionTicket
{
    public const string AuthenticatedUtc = "bff.authenticatedUtc";
    public const string SecurityVersion = "bff.securityVersion";
    public const string ProviderCheckedUtc = "bff.providerCheckedUtc";
    public const string MfaCaVerified = "bff.mfaCaVerified";
    public const string SessionReference = "bff.sessionReference";
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan ProviderFreshness = TimeSpan.FromMinutes(15);
    private static readonly HashSet<string> Roles = ["PilotConsultant", "PilotCustomerUser", "PilotAuditor", "PilotPlatformOperator"];
    private static readonly HashSet<string> Properties = [AuthenticatedUtc, SecurityVersion,
        ProviderCheckedUtc, MfaCaVerified, SessionReference, ".issued", ".expires", ".persistent", ".refresh", ".redirect"];

    public static (SessionSubject Subject, long Version, DateTimeOffset Authenticated) Validate(
        AuthenticationTicket ticket, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (ticket.AuthenticationScheme.Length is < 1 or > 128 ||
            ticket.Principal.Identities.Count() != 1 || !ticket.Principal.Identity!.IsAuthenticated ||
            ticket.Principal.Claims.Any(c => c.Type is not ("tid" or "oid" or "roles")) ||
            ticket.Principal.Claims.Any(c => c.Type == "roles" && !Roles.Contains(c.Value)) ||
            ticket.Principal.Claims.Count() > 6 || ticket.Properties.Items.Keys.Any(k => !Properties.Contains(k)) ||
            ticket.Properties.Parameters.Count != 0)
        {
            throw new InvalidOperationException("Session ticket contains unsupported identity or credential material.");
        }
        var subject = new SessionSubject(ClaimId(ticket, "tid"), ClaimId(ticket, "oid"));
        var redirect = ticket.Properties.RedirectUri;
        if (redirect is not null && (!Regex.IsMatch(redirect, "^/[A-Za-z0-9/_-]*$", RegexOptions.CultureInvariant) ||
            redirect.StartsWith("//", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Session callback return path must be a local literal path.");
        }
        if (!long.TryParse(SessionTicket.Property(ticket, SecurityVersion), NumberStyles.None,
                CultureInfo.InvariantCulture, out var version) || version <= 0 ||
            !DateTimeOffset.TryParseExact(SessionTicket.Property(ticket, AuthenticatedUtc), "O",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var authenticated) ||
            !DateTimeOffset.TryParseExact(SessionTicket.Property(ticket, ProviderCheckedUtc), "O",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var provider) ||
            authenticated > now || provider > now || now - provider >= ProviderFreshness ||
            SessionTicket.Property(ticket, MfaCaVerified) is not ("true" or "false"))
        {
            throw new InvalidOperationException("Session ticket lacks current trusted authentication context.");
        }
        return (subject, version, authenticated);
    }

    public static AuthenticationTicket Minimal(AuthenticationTicket ticket, DateTimeOffset absoluteExpires,
        DateTimeOffset now, DateTimeOffset providerChecked)
    {
        var identity = new ClaimsIdentity(ticket.Principal.Claims.Select(c => new Claim(c.Type, c.Value)),
            ticket.AuthenticationScheme, "oid", "roles");
        var properties = new AuthenticationProperties();
        foreach (var name in new[] { AuthenticatedUtc, SecurityVersion, MfaCaVerified })
        {
            properties.Items[name] = ticket.Properties.Items[name];
        }
        properties.Items[ProviderCheckedUtc] = providerChecked.ToString("O", CultureInfo.InvariantCulture);
        properties.IssuedUtc = now;
        properties.ExpiresUtc = new[] { now + IdleLifetime, absoluteExpires }.Min();
        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, ticket.AuthenticationScheme);
    }

    public static string NewKey() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string? Property(AuthenticationTicket ticket, string name) =>
        ticket.Properties.Items.TryGetValue(name, out var value) ? value : null;

    public static string? HashKey(string key)
    {
        if (key is null || key.Length != 43)
        {
            return null;
        }
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(key);
            return bytes.Length == 32 && WebEncoders.Base64UrlEncode(bytes) == key
                ? Convert.ToHexStringLower(SHA256.HashData(bytes)) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Guid ClaimId(AuthenticationTicket ticket, string name)
    {
        var claims = ticket.Principal.FindAll(name).ToArray();
        if (claims.Length != 1 || !Guid.TryParseExact(claims[0].Value, "D", out var id) || id == Guid.Empty)
        {
            throw new InvalidOperationException("Session ticket requires one immutable tenant and subject.");
        }
        return id;
    }
}
