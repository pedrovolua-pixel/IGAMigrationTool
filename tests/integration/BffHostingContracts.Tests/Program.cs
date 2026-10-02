using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

var checks = 0;
void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
void Denies(Action action, string label)
{
    try { action(); } catch (Exception e) when (e is ArgumentException or CryptographicException or InvalidOperationException) { checks++; return; }
    throw new InvalidOperationException(label);
}
var environment = Guid.NewGuid();
var binding = new BffSharedProtectionContract(environment, Guid.NewGuid(),
    "https://syntheticcontrolplane.blob.core.windows.net/bff-data-protection/keyring.xml",
    "https://synthetic-vault.vault.azure.net/keys/bff-data-protection");
binding.Validate();
Check(binding.ApplicationDiscriminator == $"IGAMigrationTool.Bff.{environment:D}.v1", "stable explicit discriminator");
foreach (var bad in new[]
{
    binding with { EnvironmentId = Guid.Empty }, binding with { ManagedIdentityClientId = Guid.Empty },
    binding with { BlobRingUri = binding.BlobRingUri + "?sig=forbidden" },
    binding with { BlobRingUri = binding.BlobRingUri.Replace("https:", "http:") },
    binding with { BlobRingUri = binding.BlobRingUri.Replace("keyring.xml", "customer.xml") },
    binding with { BlobRingUri = binding.BlobRingUri.Replace(".windows.net", ".windows.net.attacker.test") },
    binding with { VersionlessWrappingKeyUri = binding.VersionlessWrappingKeyUri + "/version" },
    binding with { VersionlessWrappingKeyUri = binding.VersionlessWrappingKeyUri + "#fragment" },
    binding with { VersionlessWrappingKeyUri = binding.VersionlessWrappingKeyUri.Replace("https://", "https://user@") }
}) Denies(bad.Validate, "unsafe protection config accepted");

var ingress = new BffReviewedIngressContract("bff.synthetic.test", IPAddress.Loopback);
foreach (var bad in new[] { ingress with { CanonicalHost = "BFF.synthetic.test" }, ingress with { CanonicalHost = "bff.synthetic.test:443" },
    ingress with { CanonicalHost = "bff.synthetic.test/path" }, ingress with { ImmediatePeer = IPAddress.Any } })
    Denies(bad.Validate, "invalid ingress accepted");
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback);
request.CertificateExtensions.Add(san.Build());
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
var port = FreePort();
var backendPort = FreePort();
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Services.AddBffFoundation(new BffOptions { LiveSignInEnabled = true, TenantId = Guid.NewGuid(), ClientId = Guid.NewGuid(), ManagedIdentityClientId = Guid.NewGuid() }, new DenyingTickets(), new DenyingAuthority());
builder.Services.AddBffPublicAuthenticationTransport();
builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
builder.Services.PostConfigure<OpenIdConnectOptions>(BffRegistration.OidcScheme, options => options.ConfigurationManager =
    new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration()));
builder.WebHost.ConfigureKestrel(options => { options.Listen(IPAddress.Loopback, port, endpoint => endpoint.UseHttps(certificate)); options.Listen(IPAddress.Loopback, backendPort); });
await using var app = builder.Build();
app.UseBffReviewedIngress(ingress);
app.UseAuthentication();
app.UseBffPublicAuthenticationEndpoints(new() { FixedOrigin = ingress.FixedOrigin });
app.Run(async context => { context.Response.StatusCode = 204; await Task.CompletedTask; });
await app.StartAsync();
using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == certificate.Thumbprint };
using var client = new HttpClient(handler);
async Task<int> Send(string host, params (string Name, string Value)[] headers)
{
    using var message = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{port}/ingress-check");
    message.Headers.Host = host;
    foreach (var (name, value) in headers) message.Headers.TryAddWithoutValidation(name, value);
    using var response = await client.SendAsync(message); return (int)response.StatusCode;
}
Check(await Send(ingress.CanonicalHost, ("X-Forwarded-Proto", "https")) == 204, "real HTTPS verified hop");
using (var offload = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{backendPort}/ingress-check"))
{
    offload.Headers.Host = ingress.CanonicalHost; offload.Headers.Add("X-Forwarded-Proto", "https");
    using var response = await client.SendAsync(offload);
    Check((int)response.StatusCode == 204, "trusted TLS offload makes real backend HTTP effective HTTPS before authentication");
}
Check(await Send(ingress.CanonicalHost, ("X-Forwarded-Proto", "https"), ("Origin", ingress.FixedOrigin)) == 204, "fixed origin");
Check(await Send("attacker.test", ("X-Forwarded-Proto", "https")) == 403, "original host spoof");
Check(await Send(ingress.CanonicalHost) == 403, "absent proxy evidence");
foreach (var value in new[] { "http", "HTTPS", "https,http", "http,https", "https,https" })
    Check(await Send(ingress.CanonicalHost, ("X-Forwarded-Proto", value)) == 403, "scheme/hop denial");
foreach (var name in new[] { "X-Forwarded-Host", "X-Forwarded-Prefix", "Forwarded", "X-Original-Proto", "X-Original-Host", "X-Original-For", "X-Original-Prefix", "X-Forwarded-For" })
    Check(await Send(ingress.CanonicalHost, ("X-Forwarded-Proto", "https"), (name, "forbidden")) == 403, "unsupported forwarded header");
Check(await Send(ingress.CanonicalHost, ("X-Forwarded-Proto", "https"), ("Origin", "https://attacker.test")) == 204, "ingress leaves application Origin policy to route handler");
using (var callback = new HttpRequestMessage(HttpMethod.Post, $"https://127.0.0.1:{port}/bff/signin-oidc"))
{
    callback.Headers.Host = ingress.CanonicalHost;
    callback.Headers.Add("X-Forwarded-Proto", "https");
    callback.Headers.Add("Origin", "https://login.microsoftonline.com");
    callback.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "synthetic-invalid", ["state"] = "invalid" });
    using var response = await client.SendAsync(callback);
    Check((int)response.StatusCode == 401, "provider-Origin callback reaches supported handler and invalid protocol denies");
}
using (var signIn = new HttpRequestMessage(HttpMethod.Post, $"https://127.0.0.1:{port}/bff/v1/sign-in"))
{
    signIn.Headers.Host = ingress.CanonicalHost; signIn.Headers.Add("X-Forwarded-Proto", "https");
    signIn.Headers.Add("Origin", "https://attacker.test"); signIn.Content = new FormUrlEncodedContent([]);
    using var response = await client.SendAsync(signIn);
    Check((int)response.StatusCode == 403, "wrong application Origin still denied by public D01 handler");
}
await app.StopAsync();
foreach (var candidate in new[] { ingress with { ForwardClientAddress = true }, ingress with { ImmediatePeer = IPAddress.Parse("192.0.2.9") } })
{
    var testPort = FreePort();
    var forwardedBuilder = WebApplication.CreateBuilder(); forwardedBuilder.Logging.ClearProviders();
    forwardedBuilder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, testPort, endpoint => endpoint.UseHttps(certificate)));
    await using var forwardedApp = forwardedBuilder.Build();
    forwardedApp.Use(async (context, next) => { if (context.Request.Path == "/base") context.Request.PathBase = "/base"; await next(context); });
    forwardedApp.UseBffReviewedIngress(candidate);
    forwardedApp.Run(context => context.Response.WriteAsync(context.Connection.RemoteIpAddress!.ToString()));
    await forwardedApp.StartAsync();
    foreach (var address in new[] { "192.0.2.11", "", "invalid", "192.0.2.11,192.0.2.12" })
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{testPort}/check");
        message.Headers.Host = ingress.CanonicalHost; message.Headers.Add("X-Forwarded-Proto", "https");
        if (address.Length != 0) message.Headers.TryAddWithoutValidation("X-Forwarded-For", address);
        using var response = await client.SendAsync(message);
        Check(candidate.ForwardClientAddress && address == "192.0.2.11"
            ? response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync() == address
            : (int)response.StatusCode == 403, "actual HTTPS optional client hop or untrusted peer");
    }
    using var prefix = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{testPort}/base");
    prefix.Headers.Host = ingress.CanonicalHost; prefix.Headers.Add("X-Forwarded-Proto", "https");
    if (candidate.ForwardClientAddress) prefix.Headers.Add("X-Forwarded-For", "192.0.2.11");
    using var prefixResponse = await client.SendAsync(prefix);
    Check((int)prefixResponse.StatusCode == 403, "actual HTTPS path-base denied");
    await forwardedApp.StopAsync();
}
var services = new ServiceCollection().AddLogging().BuildServiceProvider();
using var testServices = services;
var pipeline = new ApplicationBuilder(services);
pipeline.UseBffReviewedIngress(ingress); pipeline.Run(_ => Task.CompletedTask);
var wrongPeer = new DefaultHttpContext { RequestServices = services };
wrongPeer.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.3"); wrongPeer.Request.Host = new(ingress.CanonicalHost);
wrongPeer.Request.Headers["X-Forwarded-Proto"] = "https";
await pipeline.Build()(wrongPeer); Check(wrongPeer.Response.StatusCode == 403, "untrusted actual peer");

// In-memory repository and wrapping service are deliberately synthetic seams.
// Real framework DP interoperability is exercised; no Azure SDK/ETag proof is claimed.
var ring = new SyntheticRing();
var wrapping = new SyntheticWrapping();
using var replica1 = Replica(binding, ring, wrapping);
replica1.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(2));
var first = replica1.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1");
var protectedValue = first.Protect("synthetic-ticket");
using var replica2 = Replica(binding, ring, wrapping);
Check(replica2.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue) == "synthetic-ticket", "cold replica shared encrypted ring");
Check(ring.GetAllElements().All(x => !x.ToString().Contains("<masterKey", StringComparison.Ordinal)), "no plaintext master keys stored");
using var otherEnvironment = Replica(binding with { EnvironmentId = Guid.NewGuid() }, ring, wrapping);
Denies(() => otherEnvironment.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "cross-environment protected data");
using var wrongIdentity = Replica(binding with { ManagedIdentityClientId = Guid.NewGuid() }, ring, wrapping, allowedIdentity: binding.ManagedIdentityClientId);
Denies(() => wrongIdentity.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "wrong managed identity seam");
var previousVersion = wrapping.Version;
wrapping.Rotate();
replica1.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(3));
using var afterRotation = Replica(binding, ring, wrapping);
Check(afterRotation.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue) == "synthetic-ticket", "historical wrapping version retained");
wrapping.Unavailable.Add(previousVersion);
Check(first.Unprotect(protectedValue) == "synthetic-ticket", "warm cache retains key despite external wrapping revocation");
using var drained = Replica(binding, ring, wrapping);
Denies(() => drained.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "drained cold replica missing wrapping key fails");
wrapping.Unavailable.Clear();
wrapping.Disabled = true;
using var disabled = Replica(binding, ring, wrapping);
Denies(() => disabled.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "disabled wrapping source fails");
Denies(() => disabled.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)), "disabled wrapping prevents key persistence");
wrapping.Disabled = false;
Denies(() => wrongIdentity.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)), "wrong identity cannot persist new key");
var missingRing = new SyntheticRing { ReadUnavailable = true };
using var unavailableRepository = Replica(binding, missingRing, wrapping);
Denies(() => unavailableRepository.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "repository unavailable no ephemeral fallback");
var emptyRing = new SyntheticRing { Conflict = true };
using var conflict = Replica(binding, emptyRing, wrapping);
Denies(() => conflict.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)), "conditional write conflict fails without fallback");
Check(emptyRing.GetAllElements().Count == 0, "conflict leaves no plaintext/ephemeral persisted key");
var corruptRing = new SyntheticRing();
corruptRing.Replace(ring.GetAllElements().Select(x => new XElement(x)));
corruptRing.Corrupt();
using var corrupt = Replica(binding, corruptRing, wrapping);
Denies(() => corrupt.GetRequiredService<IDataProtectionProvider>().CreateProtector("fixture.v1").Unprotect(protectedValue), "corrupt encrypted ring fails");
Console.WriteLine($"PASS BFF hosting contracts: {checks} checks; actual HTTPS/framework DP, synthetic wrapping/repository only.");

static int FreePort() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
static ServiceProvider Replica(BffSharedProtectionContract config, SyntheticRing ring, SyntheticWrapping wrapping, Guid? allowedIdentity = null)
{
    config.Validate();
    var services = new ServiceCollection(); services.AddLogging();
    services.AddSingleton(new SyntheticAccess(wrapping, config.ManagedIdentityClientId == (allowedIdentity ?? config.ManagedIdentityClientId)));
    services.AddDataProtection().SetApplicationName(config.ApplicationDiscriminator).AddKeyManagementOptions(options =>
    {
        options.XmlRepository = ring; options.XmlEncryptor = new SyntheticEncryptor(wrapping, config.ManagedIdentityClientId == (allowedIdentity ?? config.ManagedIdentityClientId));
        options.AutoGenerateKeys = false;
    });
    return services.BuildServiceProvider();
}
sealed class SyntheticRing : IXmlRepository
{
    private readonly List<XElement> elements = [];
    public bool Conflict { get; set; }
    public bool ReadUnavailable { get; set; }
    public IReadOnlyCollection<XElement> GetAllElements() { lock (elements) { if (ReadUnavailable) throw new InvalidOperationException("Synthetic repository unavailable."); return elements.Select(x => new XElement(x)).ToArray(); } }
    public void StoreElement(XElement element, string friendlyName) { lock (elements) { if (Conflict) throw new InvalidOperationException("Synthetic ETag conflict."); elements.Add(new(element)); } }
    public void Replace(IEnumerable<XElement> values) { lock (elements) { elements.Clear(); elements.AddRange(values); } }
    public void Corrupt() { lock (elements) foreach (var element in elements.SelectMany(x => x.Descendants("wrapped"))) element.Value = "invalid"; }
}
sealed class SyntheticWrapping
{
    public string Version { get; private set; } = Guid.NewGuid().ToString("D");
    public Dictionary<string, byte[]> Keys { get; } = [];
    public HashSet<string> Unavailable { get; } = [];
    public bool Disabled { get; set; }
    public SyntheticWrapping() => Keys.Add(Version, RandomNumberGenerator.GetBytes(32));
    public void Rotate() { Version = Guid.NewGuid().ToString("D"); Keys.Add(Version, RandomNumberGenerator.GetBytes(32)); }
}
sealed record SyntheticAccess(SyntheticWrapping Wrapping, bool IdentityAllowed);
sealed class SyntheticEncryptor(SyntheticWrapping wrapping, bool identityAllowed) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (!identityAllowed || wrapping.Disabled) throw new CryptographicException();
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plaintext.Length]; var tag = new byte[16];
        using var aes = new AesGcm(wrapping.Keys[wrapping.Version], 16); aes.Encrypt(nonce, plaintext, cipher, tag);
        return new(new XElement("wrapped", new XAttribute("version", wrapping.Version),
            Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray())), typeof(SyntheticDecryptor));
    }
}
sealed class SyntheticDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var access = services.GetRequiredService<SyntheticAccess>();
        var version = (string?)encryptedElement.Attribute("version");
        if (!access.IdentityAllowed || access.Wrapping.Disabled || version is null || access.Wrapping.Unavailable.Contains(version) ||
            !access.Wrapping.Keys.TryGetValue(version, out var key)) throw new CryptographicException();
        var bytes = Convert.FromBase64String(encryptedElement.Value);
        if (bytes.Length < 28) throw new CryptographicException();
        var plaintext = new byte[bytes.Length - 28]; using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plaintext);
        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }
}

sealed class DenyingAuthority : IBffSubjectAuthority
{
    public ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken) => ValueTask.FromResult<SubjectAdmission?>(null);
}
sealed class DenyingTickets : ITicketStore
{
    public Task<string> StoreAsync(AuthenticationTicket ticket) => Task.FromException<string>(new InvalidOperationException());
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Task.CompletedTask;
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult<AuthenticationTicket?>(null);
    public Task RemoveAsync(string key) => Task.CompletedTask;
}
