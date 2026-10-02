using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentitySessions;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

internal static class BffProtocolProof
{
    internal static async Task<int> RunAsync()
    {
        var checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
        using var signing = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(signing) { KeyId = "synthetic-only" };
        using var tls = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", tls, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback);
        certificateRequest.CertificateExtensions.Add(san.Build());
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        var settings = new BffOptions { LiveSignInEnabled = true, TenantId = Guid.NewGuid(), ClientId = Guid.NewGuid(), ManagedIdentityClientId = Guid.NewGuid() };
        var clock = new ProtocolClock();
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var subject = new HumanSubject(settings.TenantId, Guid.NewGuid());
        var authority = new Authority(new(subject, true, true, true, true, clock.GetUtcNow(), 1, ["PilotConsultant"], DateTimeOffset.UnixEpoch));
        var challenges = new MemoryChallenges();
        var tokens = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        var codeCalls = 0;
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IPendingAuthenticationChallengeStore>(challenges);
        builder.Services.AddBffFoundation(settings, new MemoryTickets(), authority, clock);
        builder.Services.PostConfigure<OpenIdConnectOptions>(BffRegistration.OidcScheme, options =>
        {
            options.TimeProvider = clock;
            var issuer = $"https://login.microsoftonline.com/{settings.TenantId:D}/v2.0";
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = issuer,
                AuthorizationEndpoint = "https://synthetic.invalid/authorize",
                TokenEndpoint = "https://synthetic.invalid/token"
            };
            options.Configuration.SigningKeys.Add(signingKey);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            options.Backchannel = new HttpClient(new RejectingBackchannel());
            options.Events.OnAuthorizationCodeReceived = context =>
            {
                codeCalls++;
                context.HandleCodeRedemption("synthetic-unused-access", tokens[context.ProtocolMessage.Code]);
                return Task.CompletedTask;
            };
        });
        await using var server = builder.Build();
        server.UseAuthentication();
        server.Run(async context =>
        {
            if (context.Request.Path == "/synthetic-challenge")
            {
                await context.ChallengeAsync(BffRegistration.OidcScheme, new AuthenticationProperties { RedirectUri = "https://untrusted.invalid/" });
                return;
            }
            var authentication = await context.AuthenticateAsync(BffRegistration.CookieScheme);
            context.Response.StatusCode = authentication.Succeeded ? 204 : 401;
        });
        await server.StartAsync();
        try
        {
            var address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var origin = new Uri(address);
            HttpClient Client(CookieContainer? cookies = null) => new(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = cookies ?? new CookieContainer(),
                ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == certificate.Thumbprint
            })
            { BaseAddress = origin };
            async Task<(HttpClient Client, string State, string Nonce)> Challenge()
            {
                authority.State = authority.State! with { ProviderCheckedUtc = clock.GetUtcNow() };
                var client = Client();
                var response = await client.GetAsync("/synthetic-challenge");
                Check(response.StatusCode == HttpStatusCode.Redirect, "supported synthetic challenge redirect: " + (int)response.StatusCode);
                var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
                Check(query["max_age"] == "0" && query["code_challenge_method"] == "S256" && !string.IsNullOrEmpty(query["code_challenge"]), "fresh authentication plus framework PKCE");
                var options = server.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenIdConnectOptions>>().Get(BffRegistration.OidcScheme);
                var state = options.StateDataFormat.Unprotect(query["state"]!)!;
                Check(state.RedirectUri == "/bff/v1/session" && state.Items.ContainsKey("bff.pendingReference"), "fixed redirect with protected pending state");
                return (client, query["state"]!, query["nonce"]!);
            }
            string Token(string nonce, string? authTime = null, IEnumerable<Claim>? extras = null, bool unsigned = false, bool omitTime = false, bool stringTime = false, string? issuer = null, string? audience = null, SecurityKey? key = null)
            {
                var claims = new List<Claim> { new("tid", settings.TenantId.ToString("D")), new("oid", subject.ObjectId.ToString("D")), new("nonce", nonce), new("sub", "synthetic-subject"), new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) };
                if (!omitTime) claims.Add(new("auth_time", authTime ?? clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), stringTime ? ClaimValueTypes.String : ClaimValueTypes.Integer64));
                if (extras is not null) claims.AddRange(extras);
                var jwt = new JwtSecurityToken(
                    issuer ?? $"https://login.microsoftonline.com/{settings.TenantId:D}/v2.0", audience ?? settings.ClientId.ToString("D"), claims,
                    DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), unsigned ? null : new SigningCredentials(key ?? signingKey, SecurityAlgorithms.RsaSha256));
                if (extras?.Any(c => c.Type == "auth_time") == true)
                    jwt.Payload["auth_time"] = new long[] { clock.GetUtcNow().ToUnixTimeSeconds(), clock.GetUtcNow().ToUnixTimeSeconds() };
                return new JwtSecurityTokenHandler().WriteToken(jwt);
            }
            async Task<HttpResponseMessage> Callback(HttpClient client, string state, string token)
            {
                var code = Guid.NewGuid().ToString("N"); tokens[code] = token;
                return await client.PostAsync(settings.CallbackPath, new FormUrlEncodedContent(new Dictionary<string, string> { ["state"] = state, ["code"] = code }));
            }
            var good = await Challenge();
            using (good.Client)
            {
                var token = Token(good.Nonce);
                var completed = await Callback(good.Client, good.State, token);
                Check(completed.StatusCode == HttpStatusCode.Redirect && completed.Headers.Location?.OriginalString == "/bff/v1/session", "complete signature/state/nonce/correlation success");
                Check(challenges.Consumes == 1, "single consumption only after protocol success");
                Check((await good.Client.GetAsync("/bff/v1/session")).StatusCode == HttpStatusCode.NoContent, "completed cookie uses exact session evidence");
                var replay = await Callback(good.Client, good.State, token);
                Check(replay.StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == 1, "replayed callback cannot consume or create session");
                Check((await good.Client.GetAsync("/bff/signout-oidc")).StatusCode == HttpStatusCode.Forbidden, "unbound remote signout refuses");
                Check((await good.Client.GetAsync("/bff/v1/session")).StatusCode == HttpStatusCode.NoContent, "unbound remote signout cannot clear current cookie");
            }
            var invalidNonce = await Challenge();
            using (invalidNonce.Client)
            {
                var before = challenges.Consumes;
                var failed = await Callback(invalidNonce.Client, invalidNonce.State, Token("incorrect-nonce"));
                Check(failed.StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before, "nonce failure after token validation never consumes: " + (int)failed.StatusCode + ", before=" + before + ", after=" + challenges.Consumes);
            }
            var noCorrelation = await Challenge();
            using (noCorrelation.Client)
            using (var other = Client())
            {
                var calls = codeCalls; var before = challenges.Consumes;
                Check((await Callback(other, noCorrelation.State, Token(noCorrelation.Nonce))).StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before && codeCalls == calls, "missing correlation refused before code redemption/consume");
                Check((await Callback(other, "altered-state", Token(noCorrelation.Nonce))).StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before && codeCalls == calls, "altered protected state refused before redemption/consume");
            }
            foreach (var kind in new[] { "unsigned", "missing", "malformed", "future", "stale", "older-than-challenge", "personal", "personal-tenant", "unknown-guest", "string-time", "duplicate-time", "wrong-issuer", "wrong-audience", "wrong-signature" })
            {
                var value = await Challenge();
                using (value.Client)
                {
                    var before = challenges.Consumes;
                    var time = kind switch
                    {
                        "malformed" => "-1",
                        "future" => clock.GetUtcNow().AddSeconds(1).ToUnixTimeSeconds().ToString(),
                        "stale" => clock.GetUtcNow().AddMinutes(-15).ToUnixTimeSeconds().ToString(),
                        "older-than-challenge" => clock.GetUtcNow().AddSeconds(-1).ToUnixTimeSeconds().ToString(),
                        _ => null
                    };
                    Claim[] extra = kind switch
                    {
                        "personal" => [new("idp", "live.com")],
                        "personal-tenant" => [new("idp", "https://sts.windows.net/9188040d-6c67-4c5b-b112-36a304b66dad/")],
                        "unknown-guest" => [new("acct", "1"), new("idp", $"https://sts.windows.net/{Guid.NewGuid():D}/")],
                        "duplicate-time" => [new("auth_time", clock.GetUtcNow().ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)],
                        _ => []
                    };
                    using var wrongSigning = RSA.Create(2048);
                    var token = Token(value.Nonce, time, extra, kind == "unsigned", kind == "missing", kind == "string-time",
                        kind == "wrong-issuer" ? $"https://sts.windows.net/{settings.TenantId:D}/" : null,
                        kind == "wrong-audience" ? Guid.NewGuid().ToString("D") : null,
                        kind == "wrong-signature" ? new RsaSecurityKey(wrongSigning) { KeyId = "synthetic-only" } : null);
                    var refused = await Callback(value.Client, value.State, token);
                    Check(refused.StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before, kind + " token fails without consumption: " + (int)refused.StatusCode + ", consumed=" + (challenges.Consumes - before));
                }
            }
            var reviewedHome = Guid.NewGuid();
            authority.State = authority.State! with { IsGuest = true, OrganizationalGuestOriginVerified = true, OrganizationalGuestHomeTenantId = reviewedHome };
            foreach (var provider in new[] { "https://unreviewed.invalid/", $"https://sts.windows.net/{Guid.NewGuid():D}/", "live.com", "https://sts.windows.net/9188040d-6c67-4c5b-b112-36a304b66dad/", $"https://sts.windows.net/{reviewedHome:D}/" })
            {
                var guest = await Challenge();
                using (guest.Client)
                {
                    var before = challenges.Consumes;
                    var response = await Callback(guest.Client, guest.State, Token(guest.Nonce, extras: [new("acct", "1"), new("idp", provider)]));
                    var expected = provider == $"https://sts.windows.net/{reviewedHome:D}/";
                    Check(response.StatusCode == (expected ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized) && challenges.Consumes == before + (expected ? 1 : 0), "signed guest origin matches exact trusted organizational home");
                }
            }
            authority.State = authority.State! with { IsGuest = false, OrganizationalGuestOriginVerified = false, OrganizationalGuestHomeTenantId = null };
            var authenticationDeadline = await Challenge();
            using (authenticationDeadline.Client)
            {
                var before = challenges.Consumes;
                var originalAuthentication = clock.GetUtcNow().ToUnixTimeSeconds().ToString();
                clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromMilliseconds(500)));
                authority.State = authority.State! with { ProviderCheckedUtc = clock.GetUtcNow() };
                Check((await Callback(authenticationDeadline.Client, authenticationDeadline.State, Token(authenticationDeadline.Nonce, originalAuthentication))).StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before, "auth_time exact fifteen-minute age denies while pending is younger");
                clock.Advance(-TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromMilliseconds(500)));
            }
            var delayed = await Challenge();
            using (delayed.Client)
            {
                var before = challenges.Consumes;
                challenges.AfterConsume = () => clock.Advance(TimeSpan.FromMinutes(15));
                var failed = await Callback(delayed.Client, delayed.State, Token(delayed.Nonce));
                Check(failed.StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before + 1, "deadline crossing during async consume burns transaction but refuses completion");
                Check(!failed.Headers.TryGetValues("Set-Cookie", out var headers) || !headers.Any(h => h.StartsWith("__Secure-IgaBff=", StringComparison.Ordinal)), "failed late completion issues no product cookie");
                challenges.AfterConsume = null;
                clock.Advance(TimeSpan.FromMinutes(-15));
            }
            var expired = await Challenge();
            using (expired.Client)
            {
                var before = challenges.Consumes;
                clock.Advance(TimeSpan.FromMinutes(15)); authority.State = authority.State! with { ProviderCheckedUtc = clock.GetUtcNow() };
                Check((await Callback(expired.Client, expired.State, Token(expired.Nonce))).StatusCode == HttpStatusCode.Unauthorized && challenges.Consumes == before, "exact pending deadline refused");
                clock.Advance(TimeSpan.FromMinutes(-15));
            }
            var race = await Challenge();
            using (race.Client)
            {
                var before = challenges.Consumes;
                var token = Token(race.Nonce);
                var callbacks = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Callback(race.Client, race.State, token)));
                Check(callbacks.Count(c => c.StatusCode == HttpStatusCode.Redirect) == 1 && challenges.Consumes == before + 1, "eight concurrent validated callbacks admit exactly once");
            }
            // Subject eligibility deliberately carries no authentication/MFA.
            var state = authority.State!;
            AuthenticationTicket Session(DateTimeOffset authenticated, Guid reference) => new(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tid", subject.TenantId.ToString("D")), new Claim("oid", subject.ObjectId.ToString("D")), new Claim("roles", "PilotConsultant")], BffRegistration.CookieScheme)),
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [SessionTicket.AuthenticatedUtc] = authenticated.ToString("O"),
                    [SessionTicket.ProviderCheckedUtc] = clock.GetUtcNow().ToString("O"),
                    [SessionTicket.SecurityVersion] = "1",
                    [SessionTicket.MfaCaVerified] = "false",
                    [SessionTicket.SessionReference] = reference.ToString("D")
                }), BffRegistration.CookieScheme);
            var old = Session(clock.GetUtcNow().AddHours(-8), Guid.NewGuid());
            var recent = Session(clock.GetUtcNow(), Guid.NewGuid());
            Check(!BffSessionContext.IsCurrent(old, state, clock.GetUtcNow()) && BffSessionContext.IsCurrent(recent, state, clock.GetUtcNow()), "same subject recent session cannot refresh old authentication");
            Check(!BffSessionContext.IsCurrent(recent, state with { SignInValidFromUtc = clock.GetUtcNow().AddTicks(1) }, clock.GetUtcNow()), "provider cutoff invalidates exact original authentication");
            Check(!BffSessionContext.IsCurrent(recent, state with { CoarseRoles = ["PilotAuditor"] }, clock.GetUtcNow()), "current role change rejects old session");
            Check(!BffSessionContext.IsCurrent(recent, state with { SecurityVersion = 2 }, clock.GetUtcNow()), "current version change rejects old session");
            recent.Properties.Items.Remove(SessionTicket.SessionReference);
            Check(!BffSessionContext.IsCurrent(recent, state, clock.GetUtcNow()), "missing exact store reference denies");
        }
        finally { await server.StopAsync(); }
        Console.WriteLine($"PASS {checks} actual HTTPS synthetic signed protocol assertions; no Entra/Graph/FIC proof");
        return checks;
    }

    private sealed class RejectingBackchannel : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Synthetic fixture refuses every provider HTTP request.");
    }

    private sealed class ProtocolClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
    }
}
