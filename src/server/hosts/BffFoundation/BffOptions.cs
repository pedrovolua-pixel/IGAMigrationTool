using System.Security.Claims;
using System.Collections.Frozen;

namespace IgaMigration.BffFoundation;

public sealed record BffOptions
{
    public bool LiveSignInEnabled { get; init; }
    public Guid TenantId { get; init; }
    public Guid ClientId { get; init; }
    public Guid ManagedIdentityClientId { get; init; }
    public string CookiePath { get; init; } = "/bff";
    public string CallbackPath { get; init; } = "/bff/signin-oidc";
    public string SignedOutCallbackPath { get; init; } = "/bff/signout-callback-oidc";

    internal void Validate()
    {
        if (TenantId == Guid.Empty || ClientId == Guid.Empty || ManagedIdentityClientId == Guid.Empty ||
            ClientId == ManagedIdentityClientId || TenantId == Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad") ||
            !System.Text.RegularExpressions.Regex.IsMatch(CookiePath, "^/[a-zA-Z0-9_-]+(?:/[a-zA-Z0-9_-]+)*$") ||
            !IsCallback(CallbackPath) || !IsCallback(SignedOutCallbackPath) || CallbackPath == SignedOutCallbackPath)
        {
            throw new ArgumentException("Explicit single-tenant identity and narrow callback configuration required.");
        }
    }

    private bool IsCallback(string path) => path.StartsWith(CookiePath + "/", StringComparison.Ordinal) &&
        System.Text.RegularExpressions.Regex.IsMatch(path, "^/[a-zA-Z0-9_-]+(?:/[a-zA-Z0-9_-]+)*$");
}

public readonly record struct HumanSubject(Guid TenantId, Guid ObjectId);

// Implementations consult trusted provider/product state. No request claim proves
// onboarding, assignment, provider freshness, or privileged MFA/Conditional Access.
public interface IBffSubjectAuthority
{
    ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken);
}

public sealed record SubjectAdmission(HumanSubject Subject, bool Active, bool Assigned, bool GuestOnboardingValid,
    bool ProviderStatusValid, DateTimeOffset ProviderCheckedUtc, DateTimeOffset AuthenticatedUtc,
    bool MfaCaVerified, long SecurityVersion, IReadOnlyList<string> CoarseRoles);

public static class BffIdentity
{
    public static readonly IReadOnlySet<string> AllowedRoles = new[]
    {
        "PilotConsultant", "PilotCustomerUser", "PilotAuditor", "PilotPlatformOperator"
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool TryReadSubject(ClaimsPrincipal principal, Guid expectedTenant, out HumanSubject subject)
    {
        subject = default;
        if (principal.Identities.Count() != 1 || principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        var tenants = principal.FindAll("tid").ToArray();
        var objects = principal.FindAll("oid").ToArray();
        if (tenants.Length != 1 || objects.Length != 1 ||
            !Guid.TryParseExact(tenants[0].Value, "D", out var tenant) || tenant != expectedTenant ||
            !Guid.TryParseExact(objects[0].Value, "D", out var objectId) || objectId == Guid.Empty)
        {
            return false;
        }
        subject = new HumanSubject(tenant, objectId);
        return true;
    }

    public static bool IsAdmitted(SubjectAdmission? state, DateTimeOffset now) => state is not null &&
        state.Active && state.Assigned && state.GuestOnboardingValid && state.ProviderStatusValid &&
        state.SecurityVersion > 0 && state.ProviderCheckedUtc <= now &&
        now - state.ProviderCheckedUtc < TimeSpan.FromMinutes(15) && state.AuthenticatedUtc <= now &&
        now - state.AuthenticatedUtc < TimeSpan.FromHours(8) && state.CoarseRoles.Count > 0 &&
        state.CoarseRoles.All(AllowedRoles.Contains) && state.CoarseRoles.Distinct(StringComparer.Ordinal).Count() == state.CoarseRoles.Count;
}
