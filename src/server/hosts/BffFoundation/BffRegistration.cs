using System.Globalization;
using IdentitySessions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace IgaMigration.BffFoundation;

public static class BffRegistration
{
    public const string CookieScheme = "IgaBffCookie";
    public const string OidcScheme = "IgaBffOidc";

    public static IServiceCollection AddBffFoundation(this IServiceCollection services, BffOptions settings,
        ITicketStore ticketStore, IBffSubjectAuthority authority, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ticketStore);
        ArgumentNullException.ThrowIfNull(authority);
        settings.Validate();
        services.AddSingleton(settings);
        services.AddSingleton(authority);
        services.AddSingleton(clock ?? TimeProvider.System);
        services.TryAddSingleton<IPendingAuthenticationChallengeStore, DenyingAuthenticationChallengeStore>();
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieScheme;
            options.DefaultAuthenticateScheme = CookieScheme;
            options.DefaultChallengeScheme = CookieScheme;
        }).AddMicrosoftIdentityWebApp(options =>
        {
            options.Instance = "https://login.microsoftonline.com/";
            options.TenantId = settings.TenantId.ToString("D");
            options.ClientId = settings.ClientId.ToString("D");
            options.CallbackPath = settings.CallbackPath;
            options.SignedOutCallbackPath = settings.SignedOutCallbackPath;
            options.RemoteSignOutPath = settings.CookiePath + "/signout-oidc";
            options.ClientCredentials = [new CredentialDescription
            {
                SourceType = CredentialSource.SignedAssertionFromManagedIdentity,
                ManagedIdentityClientId = settings.ManagedIdentityClientId.ToString("D")
            }];
        }, cookieScheme: CookieScheme, openIdConnectScheme: OidcScheme)
            .EnableTokenAcquisitionToCallDownstreamApi([]);
        services.AddSingleton<IMsalTokenCacheProvider, DiscardTokenCacheProvider>();
        services.PostConfigure<AuthenticationOptions>(options =>
            options.Schemes.Single(scheme => scheme.Name == OidcScheme).HandlerType = typeof(GuardedOpenIdConnectHandler));
        services.PostConfigure<CookieAuthenticationOptions>(CookieScheme, options =>
        {
            options.TimeProvider = clock ?? TimeProvider.System;
            options.Cookie.Name = "__Secure-IgaBff";
            options.Cookie.Path = settings.CookiePath;
            options.Cookie.Domain = null;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SessionStore = ticketStore;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false; // The shared store owns idle/absolute bounds.
            options.Events.OnValidatePrincipal = async context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps || context.Principal is null ||
                    !BffIdentity.TryReadSubject(context.Principal, settings.TenantId, out var subject))
                {
                    context.RejectPrincipal();
                    return;
                }
                try
                {
                    var admission = await authority.CheckAsync(subject, context.HttpContext.RequestAborted);
                    if (!BffSessionContext.TryRead(new AuthenticationTicket(context.Principal, context.Properties, CookieScheme),
                        admission, (clock ?? TimeProvider.System).GetUtcNow(), out var session))
                    {
                        context.RejectPrincipal();
                    }
                    else context.HttpContext.Features.Set(session);
                }
                catch (Exception)
                {
                    context.RejectPrincipal();
                }
            };
            options.Events.OnSigningIn = context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
                {
                    throw new InvalidOperationException("Live sign-in is disabled or transport is insecure.");
                }
                // Framework remote completion adds its internal scheme marker
                // after our handler returns. It is not product/session evidence.
                context.Properties.Items.Remove(".AuthScheme");
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        services.PostConfigure<OpenIdConnectOptions>(OidcScheme, options =>
        {
            options.TimeProvider = clock ?? TimeProvider.System;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuer = $"https://login.microsoftonline.com/{settings.TenantId:D}/v2.0";
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidAudience = settings.ClientId.ToString("D");
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
            options.ProtocolValidator.RequireNonce = true;
            options.UseSecurityTokenValidator = false;
            options.TokenHandler = new SignedJsonWebTokenHandler();
            options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
            var previousRedemption = options.Events.OnAuthorizationCodeReceived;
            options.Events.OnAuthorizationCodeReceived = async context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
                {
                    context.Fail("Live code redemption disabled.");
                    return;
                }
                await previousRedemption(context);
            };
            var previousValidation = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (previousValidation is not null)
                {
                    await previousValidation(context);
                }
                if (context.Result?.Failure is not null || !settings.LiveSignInEnabled ||
                    !context.Request.IsHttps || context.Principal is null ||
                    !BffIdentity.TryReadSubject(context.Principal, settings.TenantId, out var subject) ||
                    !ValidClient(context.Principal, settings.ClientId) ||
                    context.SecurityToken?.Issuer != $"https://login.microsoftonline.com/{settings.TenantId:D}/v2.0")
                {
                    context.Fail("Identity admission refused.");
                    return;
                }
                try
                {
                    // Microsoft.Identity.Web manually redeems the code. The
                    // framework skips this supported validator in that path;
                    // run it explicitly with the framework-read nonce before
                    // accepting evidence. Consumption remains after base success.
                    if (context.TokenEndpointResponse is null) throw new InvalidOperationException("Code response required.");
                    options.ProtocolValidator.ValidateTokenResponse(new OpenIdConnectProtocolValidationContext
                    {
                        ClientId = settings.ClientId.ToString("D"),
                        ProtocolMessage = context.TokenEndpointResponse,
                        ValidatedIdToken = context.SecurityToken,
                        Nonce = context.Nonce
                    });
                }
                catch (Exception)
                {
                    context.Fail("Protocol completion refused.");
                    return;
                }
                SubjectAdmission? admission;
                try
                {
                    admission = await authority.CheckAsync(subject, context.HttpContext.RequestAborted);
                }
                catch (Exception)
                {
                    context.Fail("Identity authority unavailable.");
                    return;
                }
                var now = (clock ?? TimeProvider.System).GetUtcNow();
                if (admission?.Subject != subject || !BffIdentity.IsAdmitted(admission, now) ||
                    !BffAuthenticationEvidence.OrganizationalOrigin(context.Principal, admission!, settings.TenantId) ||
                    context.Properties is null || !BffAuthenticationEvidence.TryReadAuthentication(context.Principal, context.Properties, now, out var authenticated) ||
                    authenticated < admission!.SignInValidFromUtc)
                {
                    context.Fail("Identity admission refused.");
                    return;
                }
                var claims = new List<Claim> { new("tid", subject.TenantId.ToString("D")), new("oid", subject.ObjectId.ToString("D")) };
                claims.AddRange(admission!.CoarseRoles.Select(role => new Claim("roles", role)));
                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme, "oid", "roles"));
                // Do not consume or discard protected protocol properties here:
                // framework nonce/token-response validation still follows this event.
                context.Properties.Items[SessionTicket.AuthenticatedUtc] = authenticated.ToString("O", CultureInfo.InvariantCulture);
                context.Properties.Items[SessionTicket.SecurityVersion] = admission.SecurityVersion.ToString(CultureInfo.InvariantCulture);
                context.Properties.Items[SessionTicket.ProviderCheckedUtc] = admission.ProviderCheckedUtc.ToString("O", CultureInfo.InvariantCulture);
                context.Properties.Items[SessionTicket.MfaCaVerified] = "false";
            };
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
                context.ProtocolMessage.MaxAge = "0";
                return Task.CompletedTask;
            };
            options.Events.OnRemoteSignOut = context =>
            {
                // Minimal product tickets deliberately have no supported sid/iss
                // binding; do not let the framework skip missing comparisons.
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            var previousSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
            options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                await previousSignOut(context);
            };
            options.Events.OnAuthenticationFailed = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Secure-IgaBffCsrf";
            options.Cookie.Path = settings.CookiePath;
            options.Cookie.Domain = null;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.HeaderName = "X-CSRF-Token";
            options.SuppressXFrameOptionsHeader = false;
        });
        return services;
    }

    private static bool ValidClient(ClaimsPrincipal principal, Guid clientId)
    {
        var clients = principal.FindAll("azp").ToArray();
        return clients.Length <= 1 && (clients.Length == 0 || clients[0].Value == clientId.ToString("D"));
    }
}
