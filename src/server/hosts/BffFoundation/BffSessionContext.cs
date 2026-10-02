using System.Globalization;
using System.Security.Claims;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;

namespace IgaMigration.BffFoundation;

public sealed record BffValidatedSession(HumanSubject Subject, Guid SessionReference, long SecurityVersion, DateTimeOffset AuthenticatedUtc);

public static class BffSessionContext
{
    public static bool TryRead(AuthenticationTicket ticket, SubjectAdmission? admission, DateTimeOffset now, out BffValidatedSession? session)
    {
        session = null;
        if (!IsCurrent(ticket, admission, now)) return false;
        var context = SessionTicket.Validate(ticket, now);
        session = new(new(context.Subject.TenantId, context.Subject.ObjectId),
            Guid.ParseExact(SessionTicket.Property(ticket, SessionTicket.SessionReference)!, "D"), context.Version, context.Authenticated);
        return true;
    }

    public static bool IsCurrent(AuthenticationTicket ticket, SubjectAdmission? admission, DateTimeOffset now)
    {
        try
        {
            var context = SessionTicket.Validate(ticket, now);
            return admission?.Subject == new HumanSubject(context.Subject.TenantId, context.Subject.ObjectId) &&
                BffIdentity.IsAdmitted(admission, now) && admission!.SecurityVersion == context.Version &&
                now - context.Authenticated < SessionTicket.AbsoluteLifetime &&
                context.Authenticated >= admission.SignInValidFromUtc &&
                Guid.TryParseExact(SessionTicket.Property(ticket, SessionTicket.SessionReference), "D", out var reference) && reference != Guid.Empty &&
                ticket.Principal.FindAll("roles").Select(c => c.Value).ToHashSet(StringComparer.Ordinal).SetEquals(admission.CoarseRoles);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }
}

internal static class BffAuthenticationEvidence
{
    internal const string ChallengeReference = "bff.pendingReference";
    internal const string ChallengeIssued = "bff.pendingIssuedUtc";

    internal static bool TryReadChallenge(AuthenticationProperties properties, out PendingAuthenticationChallenge challenge)
    {
        challenge = null!;
        properties.Items.TryGetValue(ChallengeReference, out var reference);
        properties.Items.TryGetValue(ChallengeIssued, out var issuedText);
        if (reference is null || SessionTicket.HashKey(reference) is null ||
            !DateTimeOffset.TryParseExact(issuedText, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issued) || issued.Offset != TimeSpan.Zero)
        {
            return false;
        }
        challenge = new(reference, issued);
        return true;
    }

    internal static bool TryReadAuthentication(ClaimsPrincipal principal, AuthenticationProperties properties,
        DateTimeOffset now, out DateTimeOffset authenticated)
    {
        authenticated = default;
        var times = principal.FindAll("auth_time").ToArray();
        if (!TryReadChallenge(properties, out var challenge) || challenge.IssuedUtc > now ||
            now - challenge.IssuedUtc >= TimeSpan.FromMinutes(15) || times.Length != 1 ||
            !long.TryParse(times[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < challenge.IssuedUtc.ToUnixTimeSeconds() || seconds > now.ToUnixTimeSeconds())
        {
            return false;
        }
        try { authenticated = DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { return false; }
        return now - authenticated < TimeSpan.FromMinutes(15);
    }

    internal static bool OrganizationalOrigin(ClaimsPrincipal principal, SubjectAdmission state, Guid tenant)
    {
        var providers = principal.FindAll("idp").ToArray();
        var accounts = principal.FindAll("acct").ToArray();
        if (providers.Length > 1 || accounts.Length > 1 || (accounts.Length == 1 && accounts[0].Value is not ("0" or "1"))) return false;
        var provider = providers.SingleOrDefault()?.Value;
        if (provider is not null && (provider.Equals("live.com", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("https://sts.windows.net/9188040d-6c67-4c5b-b112-36a304b66dad/", StringComparison.OrdinalIgnoreCase))) return false;
        var guest = state.IsGuest || accounts.SingleOrDefault()?.Value == "1" ||
            (provider is not null && provider != $"https://sts.windows.net/{tenant:D}/" && provider != $"https://login.microsoftonline.com/{tenant:D}/v2.0");
        return !guest || state.IsGuest && state.OrganizationalGuestOriginVerified &&
            state.OrganizationalGuestHomeTenantId is { } home && home != Guid.Empty &&
            home != Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad") &&
            (provider == $"https://sts.windows.net/{home:D}/" || provider == $"https://login.microsoftonline.com/{home:D}/v2.0");
    }
}
