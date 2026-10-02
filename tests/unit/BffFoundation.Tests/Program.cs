using System.Security.Claims;
using IdentitySessions;
using System.Globalization;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Microsoft.Identity.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Identity.Client;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

var checks = 0;
void Check(bool success, string label)
{
    if (!success) throw new InvalidOperationException(label);
    checks++;
}
var tenant = Guid.NewGuid();
var client = Guid.NewGuid();
var subject = new HumanSubject(tenant, Guid.NewGuid());
var settings = new BffOptions { TenantId = tenant, ClientId = client, ManagedIdentityClientId = Guid.NewGuid(), LiveSignInEnabled = true };
var authority = new Authority(new SubjectAdmission(subject, true, true, true, true,
    DateTimeOffset.UtcNow, 1, ["PilotConsultant"], DateTimeOffset.UnixEpoch));
ClaimsPrincipal Principal(params Claim[] extras) => new(new ClaimsIdentity(
    new Claim[] { new("tid", tenant.ToString("D")), new("oid", subject.ObjectId.ToString("D")) }.Concat(extras),
    BffRegistration.CookieScheme, "oid", "roles"));
var valid = Principal(new Claim("roles", "PilotConsultant"));
Check(BffIdentity.TryReadSubject(valid, tenant, out var read) && read == subject, "immutable subject");
Check(!BffIdentity.TryReadSubject(Principal(new Claim("tid", tenant.ToString())), tenant, out _), "duplicate tenant");
Check(!BffIdentity.TryReadSubject(Principal(new Claim("oid", Guid.NewGuid().ToString())), tenant, out _), "duplicate object");
Check(!BffIdentity.TryReadSubject(valid, Guid.NewGuid(), out _), "wrong tenant");
Check(!BffIdentity.TryReadSubject(new ClaimsPrincipal(new ClaimsIdentity(valid.Claims)), tenant, out _), "unauthenticated");
Check(!BffIdentity.TryReadSubject(new ClaimsPrincipal(new[] { (ClaimsIdentity)valid.Identity!, new ClaimsIdentity() }), tenant, out _), "multiple identities");
var now = DateTimeOffset.UtcNow;
Check(!BffIdentity.IsAdmitted(authority.State! with { ProviderCheckedUtc = now.AddMinutes(-15) }, now), "provider stale boundary");
Check(!BffIdentity.IsAdmitted(authority.State! with { SignInValidFromUtc = null }, now), "missing provider cutoff");
Check(!BffIdentity.IsAdmitted(authority.State! with { SecurityVersion = 0 }, now), "invalid version");
Check(!BffIdentity.IsAdmitted(authority.State! with { CoarseRoles = ["Administrator"] }, now), "unknown coarse role");
Check(!BffIdentity.IsAdmitted(authority.State! with { GuestOnboardingValid = false }, now), "guest onboarding");
foreach (var unsafeSettings in new[] { settings with { TenantId = Guid.Empty }, settings with { ManagedIdentityClientId = client }, settings with { CookiePath = "/" }, settings with { CallbackPath = "/external" }, settings with { CookiePath = "/bff/../" } })
{
    try { new ServiceCollection().AddBffFoundation(unsafeSettings, new MemoryTickets(), authority); throw new Exception("accepted unsafe settings"); }
    catch (ArgumentException) { checks++; }
}
ServiceProvider Provider(BffOptions options, CountingConfiguration? configuration = null)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    services.AddSingleton<IPendingAuthenticationChallengeStore, MemoryChallenges>();
    services.AddBffFoundation(options, new MemoryTickets(), authority);
    if (configuration is not null)
    {
        services.PostConfigure<OpenIdConnectOptions>(BffRegistration.OidcScheme, oidc => oidc.ConfigurationManager = configuration);
    }
    return services.BuildServiceProvider();
}
using var provider = Provider(settings);
var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffRegistration.CookieScheme);
Check(cookie.Cookie.SecurePolicy == CookieSecurePolicy.Always && cookie.Cookie.HttpOnly && cookie.Cookie.SameSite == SameSiteMode.Lax && cookie.Cookie.Domain is null && cookie.Cookie.Path == "/bff" && cookie.SessionStore is not null, "opaque secure scoped cookie");
var oidc = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(BffRegistration.OidcScheme);
Check(oidc.ResponseType == "code" && oidc.UsePkce && !oidc.SaveTokens && !oidc.MapInboundClaims && oidc.RequireHttpsMetadata && !oidc.GetClaimsFromUserInfoEndpoint, "OIDC hardening");
Check(oidc.Scope.ToHashSet().SetEquals(["openid", "profile"]), "no requested downstream/offline scopes");
Check(oidc.RemoteSignOutPath == settings.CookiePath + "/signout-oidc", "remote logout callback stays inside narrow cookie path");
Check(oidc.TokenValidationParameters.ValidIssuer == $"https://login.microsoftonline.com/{tenant:D}/v2.0" && oidc.TokenValidationParameters.ValidAudience == client.ToString("D") && oidc.TokenValidationParameters.ClockSkew == TimeSpan.Zero, "exact validation inputs");
var microsoft = provider.GetRequiredService<IOptionsMonitor<MicrosoftIdentityOptions>>().Get(BffRegistration.OidcScheme);
Check(microsoft.ClientCredentials!.Count() == 1 && microsoft.ClientCredentials!.Single().SourceType == CredentialSource.SignedAssertionFromManagedIdentity && microsoft.ClientCredentials!.Single().ManagedIdentityClientId == settings.ManagedIdentityClientId.ToString("D") && string.IsNullOrEmpty(microsoft.ClientSecret), "MI only credential");
DefaultHttpContext Context(IServiceProvider services, string method = "GET", string? browserCookie = null)
{
    var context = new DefaultHttpContext { RequestServices = services.CreateScope().ServiceProvider };
    context.Request.Scheme = "https";
    context.Request.Host = new HostString("localhost");
    context.Request.Path = "/bff/internal-test";
    context.Request.Method = method;
    context.Response.Body = new MemoryStream();
    if (browserCookie is not null) context.Request.Headers.Cookie = browserCookie;
    return context;
}
RequestDelegate Pipeline(IServiceProvider services)
{
    var app = new ApplicationBuilder(services);
    app.UseAuthentication();
    app.UseBffRequestProtection();
    app.Run(context => { context.Response.StatusCode = 204; return Task.CompletedTask; });
    return app.Build();
}
var properties = new AuthenticationProperties();
properties.Items["bff.securityVersion"] = "1";
properties.Items[SessionTicket.AuthenticatedUtc] = DateTimeOffset.UtcNow.ToString("O");
properties.Items[SessionTicket.ProviderCheckedUtc] = DateTimeOffset.UtcNow.ToString("O");
properties.Items[SessionTicket.MfaCaVerified] = "false";
var signIn = Context(provider);
await signIn.SignInAsync(BffRegistration.CookieScheme, valid, properties);
var setCookie = signIn.Response.Headers.SetCookie.ToString();
Check(setCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && setCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && setCookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) && !setCookie.Contains(subject.ObjectId.ToString()), "real cookie attributes and opaque contents");
var browserCookie = setCookie.Split(';')[0];
var run = Pipeline(provider);
var anonymous = Context(provider);
await run(anonymous);
Check(anonymous.Response.StatusCode == 401 && !anonymous.Response.Headers.ContainsKey("Location"), "anonymous refusal without redirect");
var authenticated = Context(provider, browserCookie: browserCookie);
await run(authenticated);
Check(authenticated.Response.StatusCode == 204, "real cookie authentication and authority");
var mutation = Context(provider, "POST", browserCookie);
await run(mutation);
Check(mutation.Response.StatusCode == 403, "missing CSRF refused");
var tokenContext = Context(provider, browserCookie: browserCookie);
await tokenContext.AuthenticateAsync(BffRegistration.CookieScheme);
tokenContext.User = valid;
var tokens = provider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(tokenContext);
var csrfCookie = tokenContext.Response.Headers.SetCookie.ToString().Split(';')[0];
var permitted = Context(provider, "POST", browserCookie + "; " + csrfCookie);
permitted.Request.Headers["X-CSRF-Token"] = tokens.RequestToken;
await run(permitted);
Check(permitted.Response.StatusCode == 204, "real synchronizer token accepted");
var tampered = Context(provider, "DELETE", browserCookie + "; " + csrfCookie);
tampered.Request.Headers["X-CSRF-Token"] = "tampered";
await run(tampered);
Check(tampered.Response.StatusCode == 403, "tampered CSRF refused");
var insecure = Context(provider, browserCookie: browserCookie);
insecure.Request.Scheme = "http";
await run(insecure);
Check(insecure.Response.StatusCode == 403, "insecure transport refused");
authority.State = authority.State! with { SecurityVersion = 2 };
var revoked = Context(provider, browserCookie: browserCookie);
await run(revoked);
Check(revoked.Response.StatusCode == 401, "real cookie rejects authoritative version change");
authority.State = authority.State! with { SecurityVersion = 1, Subject = new HumanSubject(tenant, Guid.NewGuid()) };
var mismatched = Context(provider, browserCookie: browserCookie);
await run(mismatched);
Check(mismatched.Response.StatusCode == 401, "wrong authoritative subject denied");
using var disabledProvider = Provider(settings with { LiveSignInEnabled = false });
var disabled = Context(disabledProvider);
await Pipeline(disabledProvider)(disabled);
Check(disabled.Response.StatusCode == 403, "default disabled gate");
try { await Context(disabledProvider).SignInAsync(BffRegistration.CookieScheme, valid); throw new Exception("disabled sign-in accepted"); }
catch (InvalidOperationException) { checks++; }
authority.State = new SubjectAdmission(subject, true, true, true, true, DateTimeOffset.UtcNow, 1, ["PilotConsultant"], DateTimeOffset.UnixEpoch);
async Task<TokenValidatedContext> Admit(ClaimsPrincipal identity, string? issuer = null, string? returnPath = null)
{
    var eventNonce = oidc.ProtocolValidator.GenerateNonce();
    var context = new TokenValidatedContext(Context(provider),
        new AuthenticationScheme(BffRegistration.OidcScheme, null, typeof(OpenIdConnectHandler)), oidc, identity, new AuthenticationProperties())
    {
        Principal = new ClaimsPrincipal(new ClaimsIdentity(identity.Claims.Append(new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))), BffRegistration.CookieScheme, "oid", "roles")),
        Properties = new AuthenticationProperties(new Dictionary<string, string?>
        {
            ["bff.pendingReference"] = SessionTicket.NewKey(),
            ["bff.pendingIssuedUtc"] = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString("O")
        })
        { RedirectUri = returnPath },
        Nonce = eventNonce,
        TokenEndpointResponse = new OpenIdConnectMessage { IdToken = "synthetic-event-only", AccessToken = "synthetic-unused-access", TokenType = "Bearer" },
        SecurityToken = new JwtSecurityToken(issuer ?? $"https://login.microsoftonline.com/{tenant:D}/v2.0", client.ToString("D"), [new Claim("nonce", eventNonce), new Claim("sub", "synthetic-event-subject"), new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10))
    };
    await oidc.Events.TokenValidated(context);
    return context;
}
var admitted = await Admit(Principal(new Claim("amr", "mfa"), new Claim("roles", "Administrator"), new Claim("email", "synthetic")));
Check(admitted.Result?.Failure is null && admitted.Principal!.Claims.All(claim => claim.Type is "tid" or "oid" or "roles") &&
    admitted.Properties!.Items["bff.mfaCaVerified"] == "false" && admitted.Principal.IsInRole("PilotConsultant") && !admitted.Principal.IsInRole("Administrator"), "trusted minimal admission ignores presentation/MFA/unknown role claims");
Check((await Admit(valid, returnPath: "/bff/review")).Properties?.Items.ContainsKey("bff.pendingReference") == true, "pending evidence preserved until full protocol completion");
foreach (var unsafeReturn in new[] { "https://example.invalid/", "//example.invalid/", "/bff/../external", "/bff/%2fexternal", "/bff/review?token=synthetic", "/external", "/bff/\\external" })
{
    Check((await Admit(valid, returnPath: unsafeReturn)).Properties?.Items.ContainsKey("bff.pendingReference") == true, "protocol event does not finish redirect/transaction before validation");
}
Check((await Admit(valid, $"https://sts.windows.net/{tenant:D}/")).Result?.Failure is not null, "exact v2 issuer callback gate");
Check((await Admit(Principal(new Claim("azp", Guid.NewGuid().ToString("D"))))).Result?.Failure is not null, "wrong authorized party callback gate");
Check((await Admit(Principal(new Claim("tid", tenant.ToString("D"))))).Result?.Failure is not null, "duplicate immutable identity callback gate");
authority.State = authority.State with { ProviderCheckedUtc = DateTimeOffset.UtcNow.AddMinutes(-16) };
Check((await Admit(valid)).Result?.Failure is not null, "stale provider callback refusal");
authority.State = authority.State with { ProviderCheckedUtc = DateTimeOffset.UtcNow, Assigned = false };
Check((await Admit(valid)).Result?.Failure is not null, "assignment callback refusal");
var cacheClient = PublicClientApplicationBuilder.Create(Guid.NewGuid().ToString("D")).Build();
var seed = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
{
    ["Account"] = new Dictionary<string, object>
    {
        ["synthetic.login.microsoftonline.com.synthetic"] = new
        {
            home_account_id = "synthetic.synthetic",
            environment = "login.microsoftonline.com",
            realm = "synthetic",
            local_account_id = "synthetic",
            username = "synthetic",
            authority_type = "MSSTS"
        }
    }
});
((ITokenCacheSerializer)cacheClient.UserTokenCache).DeserializeMsalV3(seed, true);
Check(System.Text.Encoding.UTF8.GetString(((ITokenCacheSerializer)cacheClient.UserTokenCache).SerializeMsalV3()).Contains("synthetic.synthetic"), "MSAL synthetic cache fixture admitted");
new DiscardTokenCacheProvider().Initialize(cacheClient.UserTokenCache);
Check(!(await cacheClient.GetAccountsAsync()).Any(), "discard provider clears real MSAL cache on access");
Check(!System.Text.Encoding.UTF8.GetString(((ITokenCacheSerializer)cacheClient.UserTokenCache).SerializeMsalV3()).Contains("synthetic.synthetic"), "no retained synthetic cache account");
foreach (var (enabled, secure) in new[] { (false, true), (false, false), (true, false) })
{
    var configuration = new CountingConfiguration();
    using var guardedProvider = Provider(settings with { LiveSignInEnabled = enabled }, configuration);
    var scheme = await guardedProvider.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(BffRegistration.OidcScheme);
    Check(scheme?.HandlerType == typeof(GuardedOpenIdConnectHandler), "guarded supported OIDC handler registered");
    var challenge = Context(guardedProvider);
    challenge.Request.Scheme = secure ? "https" : "http";
    await challenge.ChallengeAsync(BffRegistration.OidcScheme);
    Check(challenge.Response.StatusCode == 403 && !challenge.Response.Headers.ContainsKey("Location"), "pre-metadata challenge gate");
    foreach (var path in new[] { settings.CallbackPath, settings.SignedOutCallbackPath, settings.CookiePath + "/signout-oidc" })
    {
        var callback = Context(guardedProvider, "POST");
        callback.Request.Scheme = secure ? "https" : "http";
        callback.Request.Path = path;
        await Pipeline(guardedProvider)(callback);
        Check(callback.Response.StatusCode == 403, "pre-metadata callback gate");
    }
    var signOut = Context(guardedProvider);
    signOut.Request.Scheme = secure ? "https" : "http";
    await signOut.SignOutAsync(BffRegistration.OidcScheme);
    Check(signOut.Response.StatusCode == 403, "pre-metadata signout gate");
    Check(configuration.Calls == 0 && configuration.Refreshes == 0, "no provider metadata or refresh activity while disabled/insecure");
}
var enabledConfiguration = new CountingConfiguration();
using (var enabledProvider = Provider(settings, enabledConfiguration))
{
    try
    {
        await Context(enabledProvider).ChallengeAsync(BffRegistration.OidcScheme);
        throw new Exception("Synthetic metadata sentinel was not reached.");
    }
    catch (InvalidOperationException)
    {
        Check(enabledConfiguration.Calls == 1, "enabled secure challenge delegates to supported metadata path");
    }
}
checks += await BffProtocolProof.RunAsync();
Console.WriteLine($"PASS {checks} BFF foundation assertions; local middleware/options only, live OIDC/FIC unverified");

sealed class Authority(SubjectAdmission? state) : IBffSubjectAuthority
{
    public SubjectAdmission? State { get; set; } = state;
    public ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken) => ValueTask.FromResult(State);
}
sealed class MemoryTickets : ITicketStore
{
    private readonly Dictionary<string, AuthenticationTicket> tickets = [];
    public Task<string> StoreAsync(AuthenticationTicket ticket) { var key = Guid.NewGuid().ToString(); ticket.Properties.Items[SessionTicket.SessionReference] = Guid.NewGuid().ToString("D"); tickets[key] = ticket; return Task.FromResult(key); }
    public Task RenewAsync(string key, AuthenticationTicket ticket) { tickets[key] = ticket; return Task.CompletedTask; }
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult(tickets.GetValueOrDefault(key));
    public Task RemoveAsync(string key) { tickets.Remove(key); return Task.CompletedTask; }
}

sealed class CountingConfiguration : IConfigurationManager<OpenIdConnectConfiguration>
{
    public int Calls { get; private set; }
    public int Refreshes { get; private set; }
    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        Calls++;
        throw new InvalidOperationException("Unexpected synthetic metadata access.");
    }
    public void RequestRefresh() => Refreshes++;
}

sealed class MemoryChallenges : IPendingAuthenticationChallengeStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> pending = new();
    public int Consumes { get; private set; }
    public ValueTask<PendingAuthenticationChallenge?> CreateAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reference = SessionTicket.NewKey(); pending[reference] = now;
        return ValueTask.FromResult<PendingAuthenticationChallenge?>(new(reference, now));
    }
    public ValueTask<bool> TryConsumeAsync(PendingAuthenticationChallenge challenge, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ok = challenge.IssuedUtc <= now && now - challenge.IssuedUtc < TimeSpan.FromMinutes(15) &&
            pending.TryGetValue(challenge.Reference, out var issued) && issued == challenge.IssuedUtc && pending.TryRemove(challenge.Reference, out _);
        if (ok) Consumes++;
        return ValueTask.FromResult(ok);
    }
}
