using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IgaMigration.BffFoundation;

// Trusted context is established only by the public middleware after cookie authentication.
// A subject identity alone cannot let another session reuse a synchronizer token.
public sealed class PublicAuthenticationAntiforgeryDataProvider : IAntiforgeryAdditionalDataProvider
{
    public string GetAdditionalData(HttpContext context) => Binding(context) ??
        throw new InvalidOperationException("Verified authentication CSRF context is required.");

    public bool ValidateAdditionalData(HttpContext context, string additionalData) =>
        Binding(context) is { } binding && string.Equals(binding, additionalData, StringComparison.Ordinal);

    private static string? Binding(HttpContext context)
    {
        if (context.Features.Get<BffValidatedSession>() is { } session)
            return $"session:{session.Subject.TenantId:D}:{session.Subject.ObjectId:D}:{session.SessionReference:D}:{session.SecurityVersion}";
        // Anonymous issuance is confined to the explicit public context. Generic
        // authenticated endpoints use the same verified feature as these routes.
        return context.User.Identity?.IsAuthenticated != true &&
            context.Features.Get<PublicAuthenticationCsrfContext>()?.Binding == "anonymous" ? "anonymous" : null;
    }
}

internal sealed record PublicAuthenticationCsrfContext(string Binding);

public static class PublicAuthenticationRegistration
{
    // Explicit opt-in; the diagnostic executable does not call this or install routes.
    public static IServiceCollection AddBffPublicAuthenticationTransport(this IServiceCollection services)
    {
        services.AddSingleton<IAntiforgeryAdditionalDataProvider, PublicAuthenticationAntiforgeryDataProvider>();
        return services;
    }
}
