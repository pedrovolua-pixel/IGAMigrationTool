using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IgaMigration.BffFoundation;

public static class PublicAuthenticationEndpoints
{
    private const string SessionPath = "/bff/v1/session";
    private const string SignInPath = "/bff/v1/sign-in";
    private const string SignOutPath = "/bff/v1/sign-out";

    // Install after UseAuthentication and before generic product request protection.
    public static IApplicationBuilder UseBffPublicAuthenticationEndpoints(this IApplicationBuilder app,
        PublicAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (app.ApplicationServices.GetRequiredService<IAntiforgeryAdditionalDataProvider>() is not PublicAuthenticationAntiforgeryDataProvider)
            throw new InvalidOperationException("Exact-session CSRF binding must be registered.");
        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            var path = request.Path.Value ?? "";
            var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? path;
            var rawPath = rawTarget.Split('?')[0];
            if (!path.StartsWith("/bff/v1", StringComparison.OrdinalIgnoreCase) &&
                !Uri.UnescapeDataString(rawPath).StartsWith("/bff/v1", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            if (request.PathBase.HasValue || rawPath != path || path is not (SessionPath or SignInPath or SignOutPath))
            {
                Deny(context, 404);
                return;
            }
            var expectedMethod = path == SessionPath ? "GET" : "POST";
            if (request.Method != expectedMethod)
            {
                context.Response.Headers.Allow = expectedMethod;
                Deny(context, 405);
                return;
            }
            var settings = context.RequestServices.GetRequiredService<BffOptions>();
            if (path == SignInPath && !settings.LiveSignInEnabled)
            {
                Deny(context, 403);
                return;
            }
            AuthenticateResult authentication;
            try { authentication = await context.AuthenticateAsync(BffRegistration.CookieScheme); }
            catch (Exception) { Deny(context, 403); return; }
            var authenticated = authentication.Succeeded && authentication.Principal is not null;
            if (path == SignOutPath && !authenticated) { Deny(context, 401); return; }
            if (path == SignInPath && authenticated) { Deny(context, 409); return; }
            if (path == SessionPath ? !options.MatchesTransport(request) : !options.MatchesOrigin(request))
            {
                Deny(context, 403);
                return;
            }
            if (request.QueryString.HasValue) { Deny(context, 400); return; }
            context.User = authenticated ? authentication.Principal! : new System.Security.Claims.ClaimsPrincipal();
            var binding = "anonymous";
            if (authenticated)
            {
                var verified = context.Features.Get<BffValidatedSession>();
                if (verified is null)
                {
                    Deny(context, 403);
                    return;
                }
                binding = $"session:{verified.Subject.TenantId:D}:{verified.Subject.ObjectId:D}:{verified.SessionReference:D}:{verified.SecurityVersion}";
            }
            context.Features.Set(new PublicAuthenticationCsrfContext(binding));
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (path == SessionPath)
            {
                if (authenticated && !await CurrentAsync(context, authentication)) { Deny(context, 403); return; }
                var tokens = antiforgery.GetAndStoreTokens(context);
                await context.Response.WriteAsJsonAsync(new
                {
                    schemaVersion = "bff-session-v1",
                    authenticated,
                    csrfToken = tokens.RequestToken!
                }, context.RequestAborted);
                return;
            }
            var formToken = path == SignInPath ? await PublicAuthenticationBody.SignInTokenAsync(request) : null;
            if (path == SignInPath ? formToken is null : !await PublicAuthenticationBody.EmptyJsonAsync(request))
            {
                Deny(context, 400);
                return;
            }
            if (path == SignOutPath && (request.Headers["X-CSRF-Token"].Count != 1 ||
                string.IsNullOrEmpty(request.Headers["X-CSRF-Token"][0])))
            {
                Deny(context, 403);
                return;
            }
            var originalHeader = request.Headers["X-CSRF-Token"];
            if (path == SignInPath) request.Headers["X-CSRF-Token"] = formToken;
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { Deny(context, 403); return; }
            finally { if (path == SignInPath) request.Headers["X-CSRF-Token"] = originalHeader; }
            if (path == SignOutPath)
            {
                if (!await CurrentAsync(context, authentication)) { Deny(context, 403); return; }
                try
                {
                    // The supported cookie handler awaits this exact store removal before deleting its cookie.
                    await context.SignOutAsync(BffRegistration.CookieScheme);
                }
                catch (Exception) { Deny(context, 403); return; }
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            try
            {
                await context.ChallengeAsync(BffRegistration.OidcScheme,
                    new OpenIdConnectChallengeProperties { RedirectUri = SessionPath, MaxAge = TimeSpan.Zero });
            }
            catch (Exception) { Deny(context, 403); }
        });
    }

    private static async Task<bool> CurrentAsync(HttpContext context, AuthenticateResult authentication)
    {
        try
        {
            var settings = context.RequestServices.GetRequiredService<BffOptions>();
            if (authentication.Ticket is null || !BffIdentity.TryReadSubject(authentication.Principal!, settings.TenantId, out var subject))
                return false;
            var state = await context.RequestServices.GetRequiredService<IBffSubjectAuthority>()
                .CheckAsync(subject, context.RequestAborted);
            return BffSessionContext.IsCurrent(authentication.Ticket, state,
                context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        }
        catch (Exception) { return false; }
    }

    private static void Deny(HttpContext context, int status) => context.Response.StatusCode = status;
}
