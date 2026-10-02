using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using IdentitySessions;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;

var checks = 0;
using var rsa = RSA.Create(2048);
var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var names = new SubjectAlternativeNameBuilder();
names.AddIpAddress(IPAddress.Loopback);
certificateRequest.CertificateExtensions.Add(names.Build());
using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
var keys = Path.Combine(Path.GetTempPath(), "iga-public-auth-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(keys);
var fixture = new Fixture();
var store = new MemoryTickets();
var clock = TimeProvider.System;
var tenant = fixture.Subject.TenantId;
await using var enabled = Build(true);
await using var disabled = Build(false);
using var handler = new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == certificate.Thumbprint
};
using var client = new HttpClient(handler);
try
{
    await enabled.App.StartAsync();
    await disabled.App.StartAsync();
    if (args.Contains("--browser-fixture", StringComparer.Ordinal))
    {
        Console.WriteLine("BFF-PUBLIC-BROWSER-FIXTURE " + enabled.Origin);
        await enabled.App.WaitForShutdownAsync();
        return;
    }
    foreach (var invalidOrigin in new[] { "http://localhost", "https://localhost/", "https://localhost/path", "https://user@localhost", "https://localhost?x=1", "https://localhost#fragment" })
    {
        try
        {
            enabled.App.UseBffPublicAuthenticationEndpoints(new() { FixedOrigin = invalidOrigin });
            throw new InvalidOperationException("Unsafe origin accepted.");
        }
        catch (ArgumentException) { checks++; }
    }
    var anonymous = await Session(enabled);
    Check(!anonymous.Authenticated, "anonymous exact session JSON");
    var firstCookie = await Seed(enabled);
    var secondCookie = await Seed(enabled);
    var otherCookie = await Seed(enabled, fixture.OtherSubject);
    var first = await Session(enabled, firstCookie);
    var second = await Session(enabled, secondCookie);
    var other = await Session(enabled, otherCookie);
    Check(first.Authenticated && second.Authenticated && other.Authenticated, "three actual opaque authenticated sessions");
    foreach (var path in new[] { "/bff/v1/unknown", "/BFF/v1/session", "/bff/V1/session", "/bff/v1/session/", "/bff/v1/session/extra", "/bff/v1/%73ession", "/%62ff/v1/session", "/bff/v1/sign%2Din" })
        await Expect(enabled, HttpMethod.Get, path, null, null, null, 404, "unknown/case/encoded route " + path);
    foreach (var path in new[] { "/bff/v1/session", "/bff/v1/sign-in", "/bff/v1/sign-out" })
    {
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Head, HttpMethod.Options, new HttpMethod("TRACE") })
            await Expect(disabled, method, path, null, null, null, 405, "method before activation/auth/body");
    }
    await Expect(disabled, HttpMethod.Post, "/bff/v1/sign-in", "malformed", "text/plain", null, 403, "disabled before parsing");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "malformed", "text/plain", null, 401, "missing authentication before origin/body");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", "malformed", "text/plain", firstCookie, 409, "authenticated sign-in before origin/body");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", "malformed", "text/plain", null, 403, "origin before parsing");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", Form(anonymous.Token), "application/x-www-form-urlencoded", anonymous.Cookie, 403, "wrong origin", "https://example.invalid");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in?returnUrl=https://example.invalid", Form(anonymous.Token), "application/x-www-form-urlencoded", anonymous.Cookie, 400, "query returnURL forbidden", enabled.Origin);
    await Expect(enabled, HttpMethod.Get, "/bff/v1/session?scope=x", null, null, null, 400, "session query fields forbidden");
    var formNegatives = new[]
    {
        "", "__RequestVerificationToken", "__RequestVerificationToken=", "__RequestVerificationToken=x&__RequestVerificationToken=y",
        "__RequestVerificationToken=x&scope=openid", "__RequestVerificationToken=x&", "other=x", "__RequestVerificationToken=%",
        "__RequestVerificationToken=%GG", "__RequestVerificationToken=% A", "__RequestVerificationToken=%C0%AF", "__RequestVerificationToken=%ED%A0%80",
        "__RequestVerificationToken=" + new string('a', 4097), "__RequestVerificationToken=" + new string('a', 9000)
    };
    foreach (var body in formNegatives)
        await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", body, "application/x-www-form-urlencoded", anonymous.Cookie, 400, "strict form rejection", enabled.Origin);
    foreach (var type in new[] { "text/plain", "application/json", "application/x-www-form-urlencoded; charset=iso-8859-1", "application/x-www-form-urlencoded; charset=utf-8; charset=utf-8", "application/x-www-form-urlencoded; boundary=x" })
        await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", Form(anonymous.Token), type, anonymous.Cookie, 400, "media type/charset rejection", enabled.Origin);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", "__RequestVerificationToken=" + new string('a', 4096), "application/x-www-form-urlencoded", anonymous.Cookie, 403, "4096 token reaches CSRF", enabled.Origin);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", Form(anonymous.Token), "application/x-www-form-urlencoded", null, 403, "missing CSRF cookie", enabled.Origin);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", Form(first.Token), "application/x-www-form-urlencoded", anonymous.Cookie, 403, "authenticated token cannot become anonymous", enabled.Origin);
    foreach (var type in new[] { "application/x-www-form-urlencoded", "application/x-www-form-urlencoded; charset=utf-8", "application/x-www-form-urlencoded; charset=\"UTF-8\"" })
        await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-in", Form(anonymous.Token), type, anonymous.Cookie, 302, "supported form navigation redirect", enabled.Origin);
    Check(fixture.Challenges == 3, "only valid anonymous forms challenge provider");
    foreach (var body in new[] { "", "null", "[]", "{\"a\":1}", "{\"a\":1,\"a\":2}", "{}{}", "{}x", "{/*x*/}", "{", "{}" + new string(' ', 8191) })
        await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", body, "application/json", firstCookie + "; " + first.Cookie, 400, "strict empty JSON rejection", enabled.Origin, first.Token);
    foreach (var type in new[] { "text/plain", "application/json; charset=latin1", "application/json; charset=utf-8; charset=utf-8", "application/json; extra=x" })
        await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", type, firstCookie + "; " + first.Cookie, 400, "JSON media rejection", enabled.Origin, first.Token);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", firstCookie + "; " + first.Cookie, 403, "missing CSRF header", enabled.Origin);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", secondCookie + "; " + first.Cookie, 403, "same subject distinct session CSRF isolation", enabled.Origin, first.Token);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", otherCookie + "; " + first.Cookie, 403, "other subject CSRF isolation", enabled.Origin, first.Token);
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", firstCookie + "; " + anonymous.Cookie, 403, "anonymous token cannot become authenticated", enabled.Origin, anonymous.Token);
    foreach (var path in new[] { "/bff/v1/sign-in", "/bff/v1/sign-out" })
    {
        using var request = Request(enabled, HttpMethod.Post, path, "{}", "application/json", path.EndsWith("sign-out", StringComparison.Ordinal) ? firstCookie + "; " + first.Cookie : anonymous.Cookie, enabled.Origin, first.Token);
        request.Headers.Remove("Origin");
        request.Headers.TryAddWithoutValidation("Origin", [enabled.Origin, enabled.Origin]);
        using var response = await client.SendAsync(request);
        Check((int)response.StatusCode == 403, "multiple Origin denial");
    }
    using (var request = Request(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", firstCookie + "; " + first.Cookie, enabled.Origin, first.Token))
    {
        request.Headers.Remove("X-CSRF-Token");
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", [first.Token, first.Token]);
        using var response = await client.SendAsync(request);
        Check((int)response.StatusCode == 403, "multiple CSRF header denial");
    }
    foreach (var path in new[] { "/bff/v1/sign-in", "/bff/v1/sign-out" })
    {
        using var request = Request(enabled, HttpMethod.Post, path, null, null, path.EndsWith("sign-out", StringComparison.Ordinal) ? firstCookie + "; " + first.Cookie : anonymous.Cookie, enabled.Origin, first.Token);
        request.Content = new ByteArrayContent([0xff, 0xfe]);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", path.EndsWith("sign-out", StringComparison.Ordinal) ? "application/json" : "application/x-www-form-urlencoded");
        using var response = await client.SendAsync(request);
        Check((int)response.StatusCode == 400, "raw malformed UTF8 rejected");
    }
    using (var request = Request(enabled, HttpMethod.Post, "/bff/v1/sign-in", null, null, anonymous.Cookie, enabled.Origin))
    {
        request.Content = new UnknownLengthContent(Encoding.UTF8.GetBytes("__RequestVerificationToken=" + new string('a', 8192)));
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
        using var response = await client.SendAsync(request);
        Check((int)response.StatusCode == 400, "chunked body bound enforced without ContentLength");
    }
    using (var request = Request(enabled, HttpMethod.Get, "/bff/v1/session", null, null, null))
    {
        request.Headers.Host = "attacker.invalid";
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", new Uri(enabled.Origin).Authority);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Check((int)response.StatusCode == 403, "forwarded host cannot bypass fixed actual transport host");
    }
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}" + new string(' ', 8190), "application/json", firstCookie + "; " + first.Cookie, 403, "8192 byte JSON parsed before invalid CSRF", enabled.Origin, "invalid");
    await TruncatedForm();
    fixture.RemainingChecks = 1;
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json", firstCookie + "; " + first.Cookie, 403, "current authority failure after valid CSRF denies without removal", enabled.Origin, first.Token);
    Check(store.RemoveAttempts == 0, "authority failure never touches removal");
    fixture.RemainingChecks = null;
    await Expect(enabled, HttpMethod.Post, "/bff/fixture/mutate", "{}", "application/json", firstCookie + "; " + first.Cookie, 204, "public token compatible with generic protected mutation", enabled.Origin, first.Token);
    await Expect(enabled, HttpMethod.Post, "/bff/fixture/mutate", "{}", "application/json", secondCookie + "; " + first.Cookie, 403, "generic mutation preserves exact session CSRF isolation", enabled.Origin, first.Token);
    Check(fixture.Mutations == 1, "only exact session generic mutation admitted");
    store.FailRemove = true;
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", "{}", "application/json; charset=utf-8", firstCookie + "; " + first.Cookie, 403, "store failure denies without cookie clearing", enabled.Origin, first.Token);
    Check(store.RemoveAttempts == 1 && store.Removed == 0, "failed removal not false revocation");
    store.FailRemove = false;
    var remains = await Session(enabled, firstCookie);
    Check(remains.Authenticated, "failed signout session remains admitted");
    await Expect(enabled, HttpMethod.Post, "/bff/v1/sign-out", " {} \n", "application/json", firstCookie + "; " + first.Cookie, 204, "exact local revocation before cookie clear", enabled.Origin, first.Token);
    Check(store.Removed == 1, "only current session removed");
    var ended = await Session(enabled, firstCookie);
    var otherSession = await Session(enabled, secondCookie);
    Check(!ended.Authenticated && otherSession.Authenticated, "revoked cookie denied other same-subject session preserved");
    Check(fixture.Challenges == 3, "no sign-out provider activity");
    Console.WriteLine($"BFF-PUBLIC-AUTH: {checks} actual HTTPS checks passed; production/OIDC acceptance NOT VERIFIED.");
}
finally
{
    await enabled.App.StopAsync();
    await disabled.App.StopAsync();
    Directory.Delete(keys, true);
}

TestServer Build(bool live)
{
    using var reserve = new TcpListener(IPAddress.Loopback, 0);
    reserve.Start();
    var port = ((IPEndPoint)reserve.LocalEndpoint).Port;
    reserve.Stop();
    var origin = "https://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
    var builder = WebApplication.CreateSlimBuilder();
    builder.Configuration.Sources.Clear();
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port, listener => listener.UseHttps(certificate)));
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("public-auth-synthetic");
    builder.Services.AddSingleton<IPendingAuthenticationChallengeStore, PendingFixture>();
    builder.Services.AddBffFoundation(new() { TenantId = tenant, ClientId = Guid.NewGuid(), ManagedIdentityClientId = Guid.NewGuid(), LiveSignInEnabled = live }, store, fixture, clock);
    builder.Services.AddBffPublicAuthenticationTransport();
    builder.Services.PostConfigure<OpenIdConnectOptions>(BffRegistration.OidcScheme, options =>
    {
        // Supported handler still runs its challenge; external metadata/backchannel is absent.
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { AuthorizationEndpoint = "https://provider.invalid/authorize", Issuer = "https://provider.invalid" });
        options.Backchannel = new HttpClient(new NoNetworkHandler());
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            Check(context.ProtocolMessage.MaxAge == "0" && context.Properties.RedirectUri == "/bff/v1/session", "challenge fresh auth fixed return");
            fixture.Challenges++;
            context.Response.StatusCode = 302;
            context.Response.Headers.Location = "https://provider.invalid/authorize";
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
    var app = builder.Build();
    app.UseAuthentication();
    app.UseBffPublicAuthenticationEndpoints(new() { FixedOrigin = origin });
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/bff/fixture"), branch => branch.UseBffRequestProtection());
    app.Run(async context =>
    {
        if (args.Contains("--browser-fixture", StringComparer.Ordinal) && context.Request.Path == "/fixture")
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("""
                <!doctype html><html lang="en"><title>Synthetic BFF form transport fixture</title>
                <button id="sign-in">Sign in synthetic fixture</button>
                <script type="module">
                import { navigateBffSignIn } from '/fixture/helper.js';
                document.querySelector('#sign-in').onclick = navigateBffSignIn;
                </script></html>
                """);
            return;
        }
        if (args.Contains("--browser-fixture", StringComparer.Ordinal) && context.Request.Path == "/fixture/helper.js")
        {
            var helper = Environment.GetEnvironmentVariable("IGA_BFF_AUTH_NAVIGATION_HELPER")
                ?? throw new InvalidOperationException("Explicit compiled coordinator navigation helper required.");
            if (!Path.IsPathFullyQualified(helper) || Path.GetFileName(helper) != "bff-authentication-navigation.js")
                throw new InvalidOperationException("Bounded coordinator helper file required.");
            context.Response.ContentType = "text/javascript; charset=utf-8";
            await context.Response.WriteAsync(await File.ReadAllTextAsync(helper));
            return;
        }
        if (context.Request.Path == "/bff/fixture/mutate" && context.Request.Method == "POST")
        {
            fixture.Mutations++;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = 204;
            return;
        }
        context.Response.StatusCode = 404;
    });
    return new(app, origin);
}

async Task<string> Seed(TestServer server, HumanSubject? subject = null)
{
    using var scope = server.App.Services.CreateScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    context.Request.Scheme = "https";
    context.Request.Host = HostString.FromUriComponent(new Uri(server.Origin));
    context.Request.Path = "/bff/v1/session";
    var actual = subject ?? fixture.Subject;
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new("tid", actual.TenantId.ToString("D")), new("oid", actual.ObjectId.ToString("D")), new("roles", "PilotConsultant")], BffRegistration.CookieScheme, "oid", "roles"));
    var properties = new AuthenticationProperties();
    properties.Items[SessionTicket.AuthenticatedUtc] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    properties.Items[SessionTicket.ProviderCheckedUtc] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    properties.Items[SessionTicket.SecurityVersion] = "1";
    properties.Items[SessionTicket.MfaCaVerified] = "false";
    await context.SignInAsync(BffRegistration.CookieScheme, principal, properties);
    return context.Response.Headers.SetCookie.Single()!.Split(';')[0];
}

async Task<SessionResult> Session(TestServer server, string? cookie = null)
{
    using var request = Request(server, HttpMethod.Get, "/bff/v1/session", null, null, cookie);
    using var response = await client.SendAsync(request);
    Check((int)response.StatusCode == 200 && response.Headers.CacheControl?.NoStore == true, "session200 no-store");
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Check(json.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "authenticated", "csrfToken", "schemaVersion" }) &&
        json.RootElement.GetProperty("schemaVersion").GetString() == "bff-session-v1" && json.RootElement.GetProperty("csrfToken").ValueKind == JsonValueKind.String, "strict minimal JSON no protected identity fields");
    var csrfCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Secure-IgaBffCsrf=", StringComparison.Ordinal));
    Check(csrfCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && csrfCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && csrfCookie.Contains("path=/bff", StringComparison.OrdinalIgnoreCase), "narrow secure CSRF cookie");
    return new(json.RootElement.GetProperty("authenticated").GetBoolean(), json.RootElement.GetProperty("csrfToken").GetString()!, csrfCookie.Split(';')[0]);
}

async Task Expect(TestServer server, HttpMethod method, string path, string? body, string? type, string? cookie, int status, string label, string? origin = null, string? token = null)
{
    using var request = Request(server, method, path, body, type, cookie, origin, token);
    using var response = await client.SendAsync(request);
    Check((int)response.StatusCode == status, label + ": expected " + status + ", actual " + (int)response.StatusCode);
    if (path.StartsWith("/bff/v1", StringComparison.Ordinal)) Check(response.Headers.CacheControl?.NoStore == true, label + " no-store");
    Check((await response.Content.ReadAsByteArrayAsync()).Length == 0, label + " no echoed error/body details");
    if (path.StartsWith("/bff/v1/sign-out", StringComparison.Ordinal))
    {
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var setCookies) ? setCookies.ToArray() : [];
        Check(status == 204 ? cookies.Any(value => value.StartsWith("__Secure-IgaBff=;", StringComparison.Ordinal)) :
            !cookies.Any(value => value.StartsWith("__Secure-IgaBff=;", StringComparison.Ordinal)), label + " cookie clearing follows successful revocation");
    }
}

HttpRequestMessage Request(TestServer server, HttpMethod method, string path, string? body, string? type, string? cookie, string? origin = null, string? token = null)
{
    var request = new HttpRequestMessage(method, new Uri(server.Origin + path, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true }));
    if (body is not null)
    {
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        if (type is not null) request.Content.Headers.TryAddWithoutValidation("Content-Type", type);
    }
    if (cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", cookie);
    if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
    if (token is not null) request.Headers.TryAddWithoutValidation("X-CSRF-Token", token);
    return request;
}
async Task TruncatedForm()
{
    using var socket = new TcpClient();
    var origin = new Uri(enabled.Origin);
    await socket.ConnectAsync(IPAddress.Loopback, origin.Port);
    await using var ssl = new SslStream(socket.GetStream(), false, (_, presented, _, _) => presented?.GetCertHashString() == certificate.Thumbprint);
    await ssl.AuthenticateAsClientAsync("127.0.0.1");
    var text = $"POST /bff/v1/sign-in HTTP/1.1\r\nHost: {origin.Authority}\r\nOrigin: {enabled.Origin}\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: 100\r\nConnection: close\r\n\r\nx=";
    await ssl.WriteAsync(Encoding.ASCII.GetBytes(text));
    await ssl.FlushAsync();
    socket.Client.Shutdown(SocketShutdown.Send);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var bytes = new byte[4096];
    var count = 0;
    try { count = await ssl.ReadAsync(bytes, timeout.Token); }
    catch (IOException exception) when (exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset })
    {
        // Kestrel may reject truncated framing by resetting its TLS connection.
    }
    Check((count == 0 || Encoding.ASCII.GetString(bytes, 0, count).StartsWith("HTTP/1.1 400", StringComparison.Ordinal)) && fixture.Challenges == 3, "truncated form connection closed or400 without challenge");
}

string Form(string token) => "__RequestVerificationToken=" + Uri.EscapeDataString(token);
void Check(bool success, string label)
{
    if (!success) throw new InvalidOperationException(label);
    checks++;
}
sealed record TestServer(WebApplication App, string Origin) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => App.DisposeAsync();
}
sealed record SessionResult(bool Authenticated, string Token, string Cookie);
sealed class Fixture : IBffSubjectAuthority
{
    public HumanSubject Subject { get; } = new(Guid.NewGuid(), Guid.NewGuid());
    public HumanSubject OtherSubject => new(Subject.TenantId, Guid.Parse("00000000-0000-4000-8000-000000000001"));
    public int Challenges { get; set; }
    public int Mutations { get; set; }
    public int? RemainingChecks { get; set; }
    public ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken)
    {
        if (RemainingChecks == 0) throw new InvalidOperationException("Synthetic authority unavailable.");
        if (RemainingChecks is not null) RemainingChecks--;
        return ValueTask.FromResult<SubjectAdmission?>(subject == Subject || subject == OtherSubject ?
            new(subject, true, true, true, true, DateTimeOffset.UtcNow, 1, ["PilotConsultant"], DateTimeOffset.UnixEpoch) : null);
    }
}
sealed class MemoryTickets : ITicketStore
{
    private readonly Dictionary<string, AuthenticationTicket> tickets = new(StringComparer.Ordinal);
    public bool FailRemove { get; set; }
    public int Removed { get; private set; }
    public int RemoveAttempts { get; private set; }
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ticket.Properties.Items[SessionTicket.SessionReference] = Guid.NewGuid().ToString("D");
        var key = Guid.NewGuid().ToString("N");
        tickets.Add(key, ticket);
        return Task.FromResult(key);
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Task.CompletedTask;
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult(tickets.GetValueOrDefault(key));
    public Task RemoveAsync(string key)
    {
        RemoveAttempts++;
        if (FailRemove) throw new InvalidOperationException("Synthetic store unavailable.");
        if (tickets.Remove(key)) Removed++;
        return Task.CompletedTask;
    }
}
sealed class PendingFixture : IPendingAuthenticationChallengeStore
{
    public ValueTask<PendingAuthenticationChallenge?> CreateAsync(DateTimeOffset issuedUtc, CancellationToken cancellationToken) =>
        ValueTask.FromResult<PendingAuthenticationChallenge?>(new(SessionTicket.NewKey(), issuedUtc));
    public ValueTask<bool> TryConsumeAsync(PendingAuthenticationChallenge challenge, DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
sealed class UnknownLengthContent(byte[] bytes) : HttpContent
{
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
}

sealed class NoNetworkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Synthetic transport cannot contact any external provider.");
}
