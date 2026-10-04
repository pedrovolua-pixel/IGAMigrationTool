using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPolicy;
using IdentitySessions;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("IGA_BFF_FLOW_DATABASE")
    ?? throw new InvalidOperationException("A disposable synthetic PostgreSQL connection is required.");
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (parsed.Host != "127.0.0.1" || parsed.Database is null ||
    !parsed.Database.StartsWith("iga_synthetic_bff_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only the disposable loopback BFF test database is allowed.");

await using var dataSource = NpgsqlDataSource.Create(connection);
var root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "IgaMigrationTool.slnx")))
    root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Repository root missing.");
await using (var migration = dataSource.CreateCommand(await File.ReadAllTextAsync(
                 Path.Combine(root, "migrations/identity-sessions/001-initial.sql"))))
    await migration.ExecuteNonQueryAsync();
await using (var migration = dataSource.CreateCommand(await File.ReadAllTextAsync(
                 Path.Combine(root, "migrations/identity-sessions/002-authentication-context.sql"))))
    await migration.ExecuteNonQueryAsync();

var keys = Path.Combine(Path.GetTempPath(), "iga-bff-flow-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(keys);
using var rsa = RSA.Create(2048);
var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var names = new SubjectAlternativeNameBuilder();
names.AddIpAddress(IPAddress.Loopback);
certificateRequest.CertificateExtensions.Add(names.Build());
using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
var clock = new FixtureClock();
var fixture = new AuthorityFixture(clock);
var sessions = new PostgreSqlSessionAuthority(dataSource, clock);
var proof = DataProtectionProvider.Create(new DirectoryInfo(keys), builder => builder.SetApplicationName("iga-bff-flow-tests"));
var storeA = new PostgreSqlTicketStore(dataSource, proof, clock, fixture);
var storeB = new PostgreSqlTicketStore(dataSource, proof, clock, fixture);
var settings = new BffOptions
{
    // Test composition only: no OIDC challenge/callback is exercised or enabled in Azure.
    LiveSignInEnabled = true,
    TenantId = fixture.Subject.TenantId,
    ClientId = Guid.Parse("00000000-0000-4000-8000-000000000002"),
    ManagedIdentityClientId = Guid.Parse("00000000-0000-4000-8000-000000000003")
};
await sessions.ProvisionAsync(fixture.SessionSubject, true, clock.GetUtcNow(), DateTimeOffset.UnixEpoch);
await sessions.ProvisionAsync(new(fixture.OtherSubject.TenantId, fixture.OtherSubject.ObjectId), true, clock.GetUtcNow(), DateTimeOffset.UnixEpoch);
await using var serverA = BuildServer(storeA);
await using var serverB = BuildServer(storeB);
try
{
    await serverA.StartAsync();
    await serverB.StartAsync();
    var addressA = Address(serverA);
    var addressB = Address(serverB);
    using var handler = new HttpClientHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        // The exception is confined to this loopback test certificate, not product code.
        ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == certificate.Thumbprint
    };
    using var client = new HttpClient(handler);
    var cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    var cookieOptions = serverA.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffRegistration.CookieScheme);
    var wrapper = cookieOptions.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=') + 1)..]);
    Require(wrapper is not null && wrapper.Principal.Claims.Count() == 1 &&
        wrapper.Principal.Claims.Single().Type == "Microsoft.AspNetCore.Authentication.Cookies-SessionId" &&
        !wrapper.Principal.HasClaim(claim => claim.Type is "oid" or "tid" or "roles"), "opaque cookie contains only store reference");
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.OK, "cookie accepted by second server");
    var otherCookie = await SeedCookie(serverB, fixture.AuthenticationProperties(), fixture.OtherSubject);
    Require(otherCookie != cookie && fixture.Subject != fixture.OtherSubject, "distinct admitted subject fixture");
    var otherWrapper = cookieOptions.TicketDataFormat.Unprotect(otherCookie[(otherCookie.IndexOf('=') + 1)..])!;
    var otherTicket = await storeA.RetrieveAsync(otherWrapper.Principal.Claims.Single().Value);
    Require(otherTicket is not null && BffIdentity.TryReadSubject(otherTicket.Principal, settings.TenantId, out var otherIdentity) &&
        otherIdentity == fixture.OtherSubject, "second cookie resolves actual second subject");
    await Status(addressA, "/bff/fixture/read", otherCookie, HttpStatusCode.Forbidden,
        "different admitted subject cannot consume primary customer resource");
    await Status(addressA, "/bff/fixture/read", null, HttpStatusCode.Unauthorized, "anonymous denied");
    await Status(addressA, "/bff/fixture/read", cookie + "x", HttpStatusCode.Unauthorized, "altered cookie denied");
    await Status(addressB, "/bff/fixture/read?customer=" + Guid.NewGuid(), cookie, HttpStatusCode.Forbidden, "cross-customer substitution denied");
    await Status(addressB, "/bff/fixture/read?project=" + Guid.NewGuid(), cookie, HttpStatusCode.Forbidden, "cross-project substitution denied");
    await Status(addressB, "/bff/fixture/read?resource=" + Guid.NewGuid(), cookie, HttpStatusCode.Forbidden, "direct resource substitution denied");
    await Status(addressB, "/bff/fixture/read?revision=2", cookie, HttpStatusCode.Forbidden, "stale revision denied");
    var (csrfCookie, csrfToken) = await Csrf(addressA, cookie);
    await Mutation(addressB, otherCookie + "; " + csrfCookie, csrfToken, HttpStatusCode.Forbidden, "CSRF token from different subject denied");
    await Mutation(addressB, cookie + "; " + csrfCookie, null, HttpStatusCode.Forbidden, "missing CSRF denied");
    await Mutation(addressB, cookie + "; " + csrfCookie, csrfToken + "x", HttpStatusCode.Forbidden, "altered CSRF denied");
    await Mutation(addressB, cookie, csrfToken, HttpStatusCode.Forbidden, "missing CSRF cookie denied");
    await Mutation(addressB, cookie + "; " + csrfCookie, csrfToken, HttpStatusCode.OK, "valid CSRF on second server");
    Require(fixture.Mutations == 1, "only authorized mutation executed");
    fixture.Assigned = false;
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "assignment removal on next request");
    fixture.Assigned = true;
    using (var logout = new HttpRequestMessage(HttpMethod.Post, addressA + "/bff/fixture/logout"))
    {
        logout.Headers.Add("Cookie", cookie + "; " + csrfCookie);
        logout.Headers.Add("X-CSRF-Token", csrfToken);
        using var response = await client.SendAsync(logout);
        Require(response.StatusCode == HttpStatusCode.OK &&
            response.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith("__Secure-IgaBff=;", StringComparison.Ordinal)),
            "logout clears local cookie");
    }
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "logout revokes shared session");
    cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    var all = await sessions.ListSessionsAsync(fixture.SessionSubject);
    var active = all.Single(item => !item.Revoked);
    Require(await sessions.RevokeSessionAsync(fixture.SessionSubject, active.Reference), "individual session revoked");
    await Status(addressA, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "individual revocation across server");

    cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    var concurrentCookie = await SeedCookie(serverB, fixture.AuthenticationProperties());
    fixture.Version = await sessions.RevokeSubjectAsync(fixture.SessionSubject);
    await Status(addressA, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "subject revocation server A");
    await Status(addressB, "/bff/fixture/read", concurrentCookie, HttpStatusCode.Unauthorized, "subject revocation server B");
    cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    clock.Advance(TimeSpan.FromMinutes(15));
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "provider status boundary denied");

    fixture.Authenticated = clock.GetUtcNow();
    await ConfirmProvider();
    cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    clock.Advance(TimeSpan.FromMinutes(31));
    await ConfirmProvider();
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "idle deadline denied even with fresh provider");

    fixture.Authenticated = clock.GetUtcNow();
    await ConfirmProvider();
    cookie = await SeedCookie(serverA, fixture.AuthenticationProperties());
    for (var minute = 10; minute < 480; minute += 10)
    {
        clock.Advance(TimeSpan.FromMinutes(10));
        await ConfirmProvider();
        await Status(minute % 20 == 0 ? addressA : addressB, "/bff/fixture/read", cookie,
            HttpStatusCode.OK, "active idle renewal at minute " + minute);
    }
    clock.Advance(TimeSpan.FromMinutes(10));
    await ConfirmProvider();
    await Status(addressB, "/bff/fixture/read", cookie, HttpStatusCode.Unauthorized, "eight-hour absolute deadline remains fixed");
    Console.WriteLine($"BFF-FLOW: {fixture.Checks} actual HTTPS/shared PostgreSQL checks passed; Entra/FIC NOT VERIFIED.");

    async Task ConfirmProvider()
    {
        fixture.ProviderChecked = clock.GetUtcNow();
        await sessions.ConfirmProviderAsync(fixture.SessionSubject, fixture.ProviderChecked, DateTimeOffset.UnixEpoch);
    }
    async Task Status(string address, string path, string? sessionCookie, HttpStatusCode expected, string description)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address + path);
        if (sessionCookie is not null) request.Headers.Add("Cookie", sessionCookie);
        using var response = await client.SendAsync(request);
        Require(response.StatusCode == expected, description + ": " + response.StatusCode);
    }
    async Task Mutation(string address, string cookies, string? token, HttpStatusCode expected, string description)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address + "/bff/fixture/mutate");
        request.Headers.Add("Cookie", cookies);
        if (token is not null) request.Headers.Add("X-CSRF-Token", token);
        using var response = await client.SendAsync(request);
        Require(response.StatusCode == expected, description + ": " + response.StatusCode);
    }
    async Task<(string Cookie, string Token)> Csrf(string address, string sessionCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address + "/bff/fixture/csrf");
        request.Headers.Add("Cookie", sessionCookie);
        using var response = await client.SendAsync(request);
        Require(response.StatusCode == HttpStatusCode.OK, "CSRF issuance authorized");
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Require(setCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            setCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            setCookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) &&
            setCookie.Contains("path=/bff", StringComparison.OrdinalIgnoreCase) &&
            !setCookie.Contains("domain=", StringComparison.OrdinalIgnoreCase), "secure narrow CSRF cookie");
        return (setCookie.Split(';')[0], await response.Content.ReadAsStringAsync());
    }
}
finally
{
    await serverA.StopAsync();
    await serverB.StopAsync();
    Directory.Delete(keys, true);
}

WebApplication BuildServer(ITicketStore store)
{
    var builder = WebApplication.CreateSlimBuilder();
    builder.Logging.ClearProviders();
    builder.Configuration.Sources.Clear();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificate)));
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("iga-bff-flow-tests");
    builder.Services.AddBffFoundation(settings, store, fixture, clock);
    var app = builder.Build();
    app.UseAuthentication();
    app.UseBffRequestProtection();
    app.MapGet("/bff/fixture/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        antiforgery.GetAndStoreTokens(context).RequestToken!);
    // Typed route handlers select the Minimal API Delegate overload. A raw
    // HttpContext -> Task lambda can select RequestDelegate and discard IResult.
    Func<HttpContext, Task<IResult>> read = context => ReadOrMutate(context, false);
    Func<HttpContext, Task<IResult>> mutate = context => ReadOrMutate(context, true);
    app.MapGet("/bff/fixture/read", read);
    app.MapPost("/bff/fixture/mutate", mutate);
    Func<HttpContext, Task<IResult>> logout = async context =>
    {
        await context.SignOutAsync(BffRegistration.CookieScheme);
        return Results.Ok();
    };
    app.MapPost("/bff/fixture/logout", logout);
    return app;
}

async Task<IResult> ReadOrMutate(HttpContext context, bool mutate)
{
    var authentication = await context.AuthenticateAsync(BffRegistration.CookieScheme);
    if (authentication.Principal is null ||
        !BffIdentity.TryReadSubject(authentication.Principal, fixture.Subject.TenantId, out var actualSubject))
        return Results.StatusCode(403);
    string? referenceText = null;
    authentication.Properties?.Items.TryGetValue(SessionTicket.SessionReference, out referenceText);
    if (!Guid.TryParse(referenceText, out var reference))
        return Results.StatusCode(403);
    fixture.SessionReference = reference;
    var scope = fixture.Scope;
    if (context.Request.Query.TryGetValue("customer", out var customer)) scope = scope with { CustomerId = Guid.Parse(customer.ToString()) };
    if (context.Request.Query.TryGetValue("project", out var project)) scope = scope with { ProjectId = Guid.Parse(project.ToString()) };
    var resource = context.Request.Query.TryGetValue("resource", out var value) ? Guid.Parse(value.ToString()) : fixture.Resource;
    var revision = context.Request.Query.TryGetValue("revision", out var revisionValue) ? long.Parse(revisionValue.ToString(), CultureInfo.InvariantCulture) : 1;
    var request = new HumanAuthorizationRequest(scope, resource, revision,
        mutate ? HumanAction.ConfigureStartAssessment : HumanAction.ViewDashboard, "synthetic-summary",
        mutate ? ReadProjection.None : ReadProjection.Dashboard);
    var subject = new HumanSubjectKey(actualSubject.TenantId, actualSubject.ObjectId, ActorKind.Human);
    var decision = await new HumanAuthorizer(fixture, null, clock).AuthorizeAsync(subject, request, context.RequestAborted);
    if (!decision.Allowed) return Results.StatusCode(403);
    if (mutate) fixture.Mutations++;
    return Results.Ok();
}

async Task<string> SeedCookie(WebApplication app, AuthenticationProperties properties, HumanSubject? subject = null)
{
    using var scope = app.Services.CreateScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    context.Request.Scheme = "https";
    context.Request.Path = "/bff/fixture/read";
    context.Request.Host = new HostString("127.0.0.1");
    var actualSubject = subject ?? fixture.Subject;
    var identity = new ClaimsIdentity([new("tid", actualSubject.TenantId.ToString("D")),
        new("oid", actualSubject.ObjectId.ToString("D")), new("roles", "PilotConsultant")], BffRegistration.CookieScheme, "oid", "roles");
    await context.SignInAsync(BffRegistration.CookieScheme, new ClaimsPrincipal(identity), properties);
    var header = context.Response.Headers.SetCookie.Single()!;
    Require(header.Contains("secure", StringComparison.OrdinalIgnoreCase) && header.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
        header.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) && header.Contains("path=/bff", StringComparison.OrdinalIgnoreCase) &&
        !header.Contains("domain=", StringComparison.OrdinalIgnoreCase), "secure opaque session cookie");
    return header.Split(';')[0];
}

string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("BFF-FLOW failed: " + description);
    fixture.Checks++;
    Console.WriteLine("PASS " + description);
}

sealed class FixtureClock : TimeProvider
{
    private DateTimeOffset utc = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => utc;
    public void Advance(TimeSpan elapsed) => utc += elapsed;
}

sealed class AuthorityFixture(FixtureClock clock) : IBffSubjectAuthority, ISessionAdmissionPolicy, IHumanAuthoritySnapshotSource
{
    public HumanSubject Subject { get; } = new(Guid.Parse("00000000-0000-4000-8000-000000000001"), Guid.NewGuid());
    public HumanSubject OtherSubject { get; } = new(Guid.Parse("00000000-0000-4000-8000-000000000001"), Guid.NewGuid());
    public SessionSubject SessionSubject => new(Subject.TenantId, Subject.ObjectId);
    public HumanSubjectKey PolicySubject => new(Subject.TenantId, Subject.ObjectId, ActorKind.Human);
    public HumanScope Scope { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    public HumanScope OtherScope { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    public Guid Resource { get; } = Guid.NewGuid();
    public bool Assigned { get; set; } = true;
    public long Version { get; set; } = 1;
    public DateTimeOffset Authenticated { get; set; } = clock.GetUtcNow();
    public DateTimeOffset ProviderChecked { get; set; } = clock.GetUtcNow();
    public Guid SessionReference { get; set; }
    public HumanSubjectKey? LastResolvedSubject { get; set; }
    public int Mutations { get; set; }
    public int Checks { get; set; }
    public ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(subject == SessionSubject && Assigned || subject == new SessionSubject(OtherSubject.TenantId, OtherSubject.ObjectId));
    public ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult<SubjectAdmission?>(subject == Subject ? new(Subject, true, Assigned, true, true,
            ProviderChecked, Version, ["PilotConsultant"], DateTimeOffset.UnixEpoch) : subject == OtherSubject ?
            new(OtherSubject, true, true, true, true, ProviderChecked, 1, ["PilotConsultant"], DateTimeOffset.UnixEpoch) : null);
    public AuthenticationProperties AuthenticationProperties()
    {
        var properties = new AuthenticationProperties();
        properties.Items[SessionTicket.AuthenticatedUtc] = Authenticated.ToString("O", CultureInfo.InvariantCulture);
        properties.Items[SessionTicket.ProviderCheckedUtc] = ProviderChecked.ToString("O", CultureInfo.InvariantCulture);
        properties.Items[SessionTicket.SecurityVersion] = Version.ToString(CultureInfo.InvariantCulture);
        properties.Items[SessionTicket.MfaCaVerified] = "false";
        return properties;
    }
    public ValueTask<AuthoritativeHumanSnapshot?> ResolveAsync(HumanSubjectKey subject, HumanAuthorizationRequest request, CancellationToken cancellationToken)
    {
        LastResolvedSubject = subject;
        var categories = ImmutableHashSet.Create("synthetic-summary");
        if (subject != PolicySubject && subject != new HumanSubjectKey(OtherSubject.TenantId, OtherSubject.ObjectId, ActorKind.Human))
            return ValueTask.FromResult<AuthoritativeHumanSnapshot?>(null);
        var primary = subject == PolicySubject;
        var version = primary ? Version : 1;
        return ValueTask.FromResult<AuthoritativeHumanSnapshot?>(new(subject, true, true, true, version, version,
            SessionReference, clock.GetUtcNow(), ProviderChecked, ImmutableHashSet.Create(CoarseAppRole.PilotConsultant), false, null,
            [new(HumanRole.Consultant, primary ? Scope : OtherScope, !primary || Assigned, clock.GetUtcNow().AddDays(1), categories, ImmutableHashSet<GrantCondition>.Empty)],
            Scope, Resource, 1, ResourceState.Active, null,
            request.Action == HumanAction.ConfigureStartAssessment ? ReadProjection.None : ReadProjection.Dashboard, "synthetic-summary",
            ImmutableHashSet.Create(HumanAction.ViewDashboard, HumanAction.ConfigureStartAssessment), false, false, false, false,
            new(true, categories, false, false, false, false, false)));
    }
}
