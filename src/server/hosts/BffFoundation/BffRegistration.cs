using System.Globalization;
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
                    context.Properties.Items.TryGetValue("bff.securityVersion", out var versionText);
                    if (admission?.Subject != subject || !BffIdentity.IsAdmitted(admission, (clock ?? TimeProvider.System).GetUtcNow()) ||
                        !long.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
                        version != admission!.SecurityVersion ||
                        context.Principal.Claims.Any(claim => claim.Type is not ("tid" or "oid" or "roles")) ||
                        !context.Principal.FindAll("roles").Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal).SetEquals(admission.CoarseRoles))
                    {
                        context.RejectPrincipal();
                    }
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
                if (admission?.Subject != subject || !BffIdentity.IsAdmitted(admission, (clock ?? TimeProvider.System).GetUtcNow()))
                {
                    context.Fail("Identity admission refused.");
                    return;
                }
                var claims = new List<Claim> { new("tid", subject.TenantId.ToString("D")), new("oid", subject.ObjectId.ToString("D")) };
                claims.AddRange(admission!.CoarseRoles.Select(role => new Claim("roles", role)));
                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme, "oid", "roles"));
                var intendedPath = context.Properties?.RedirectUri;
                context.Properties = new AuthenticationProperties();
                if (intendedPath is not null && intendedPath.StartsWith(settings.CookiePath + "/", StringComparison.Ordinal) &&
                    System.Text.RegularExpressions.Regex.IsMatch(intendedPath, "^/[a-zA-Z0-9_-]+(?:/[a-zA-Z0-9_-]+)*$"))
                {
                    // Only a narrow local path survives protocol completion. Product
                    // authorization is evaluated again when that host action runs.
                    context.Properties.RedirectUri = intendedPath;
                }
                context.Properties.Items["bff.authenticatedUtc"] = admission.AuthenticatedUtc.ToString("O", CultureInfo.InvariantCulture);
                context.Properties.Items["bff.securityVersion"] = admission.SecurityVersion.ToString(CultureInfo.InvariantCulture);
                context.Properties.Items["bff.providerCheckedUtc"] = admission.ProviderCheckedUtc.ToString("O", CultureInfo.InvariantCulture);
                context.Properties.Items["bff.mfaCaVerified"] = admission.MfaCaVerified ? "true" : "false";
            };
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
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
