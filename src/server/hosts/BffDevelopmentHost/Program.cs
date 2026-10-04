using IdentitySessions;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Npgsql;

// This executable can never enable sign-in. Production composition must replace
// this diagnostic executable, its refusing adapters and its ephemeral key ring.
if (args.Length != 1 || args[0] != "--bff-development-disabled" || ActivationRequested())
{
    Console.Error.WriteLine("Explicit disabled BFF diagnostic mode is required; activation is unavailable.");
    return 2;
}

BffOptions settings;
NpgsqlDataSource dataSource;
try
{
    settings = new BffOptions
    {
        LiveSignInEnabled = false,
        TenantId = ReadGuid("IGA_BFF_TENANT_ID"),
        ClientId = ReadGuid("IGA_BFF_CLIENT_ID"),
        ManagedIdentityClientId = ReadGuid("IGA_BFF_MANAGED_IDENTITY_CLIENT_ID")
    };
    var connection = Environment.GetEnvironmentVariable("IGA_BFF_CONTROL_PLANE_CONNECTION");
    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new ArgumentException("Required control-plane configuration is unavailable.");
    }
    dataSource = NpgsqlDataSource.Create(connection); // Builds configuration; never opens a connection.
}
catch (Exception)
{
    Console.Error.WriteLine("Required BFF diagnostic configuration is unavailable or invalid.");
    return 2;
}

await using (dataSource)
{
    try
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options =>
        {
            options.AddServerHeader = false;
            options.ListenAnyIP(8080, listener => listener.Protocols = HttpProtocols.Http1);
        });
        builder.Host.UseConsoleLifetime();
        var refusal = new DisabledAuthority();
        // NO production key ring fallback: this process has no issuance route,
        // every ticket operation refuses, and live activation is unavailable.
        var protection = new EphemeralDataProtectionProvider();
        var realStore = new PostgreSqlTicketStore(dataSource, protection, TimeProvider.System, refusal);
        builder.Services.AddSingleton(realStore);
        builder.Services.AddBffFoundation(settings, new DisabledTicketStore(realStore), refusal);
        builder.Services.AddSingleton<IDataProtectionProvider>(protection);
        builder.Services.PostConfigure<OpenIdConnectOptions>(BffRegistration.OidcScheme,
            options => options.Backchannel = new HttpClient(new DisabledProviderHandler()));
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var target = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
            if (HttpMethods.IsGet(context.Request.Method) && target == "/health/live")
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"bootstrapOnly\":true}");
            }
            else if (HttpMethods.IsGet(context.Request.Method) && target == "/health/ready")
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"code\":\"product_not_enabled\",\"bootstrapOnly\":true}");
            }
            else
            {
                await next(context);
            }
        });
        // No forwarded-header middleware: caller headers cannot make HTTP HTTPS.
        app.UseAuthentication();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        await app.RunAsync();
        return 0;
    }
    catch (Exception)
    {
        // Provider/connection/configuration exceptions can contain identifiers or
        // secrets; refuse without serializing them or dumping the environment.
        Console.Error.WriteLine("BFF diagnostic startup or execution failed.");
        return 2;
    }
}

static Guid ReadGuid(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty
        ? id : throw new ArgumentException("Explicit immutable identity configuration is required.");
}

static bool ActivationRequested()
{
    foreach (System.Collections.DictionaryEntry item in Environment.GetEnvironmentVariables())
    {
        var key = ((string)item.Key).Replace("_", "", StringComparison.Ordinal)
            .Replace(":", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        if (key.Contains("LiveSignInEnabled", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("IGABFFACTIVATION", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("ASPNETCOREFORWARDEDHEADERSENABLED", StringComparison.OrdinalIgnoreCase))
        {
            return true; // An activation/proxy flag is unsupported even if its value says false.
        }
    }
    return false;
}

sealed class DisabledAuthority : IBffSubjectAuthority, ISessionAdmissionPolicy
{
    public ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken)
        => ValueTask.FromResult<SubjectAdmission?>(null);
    public ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken)
        => ValueTask.FromResult(false);
}

// Preserve actual PostgreSQL composition without allowing any diagnostic path
// to reach it. This decorator can never create, retrieve, renew or remove a ticket.
sealed class DisabledTicketStore(PostgreSqlTicketStore composedStore) : ITicketStore
{
    private readonly PostgreSqlTicketStore store = composedStore ?? throw new ArgumentNullException(nameof(composedStore));
    public Task<string> StoreAsync(AuthenticationTicket ticket) => Refuse<string>();
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Refuse<object>();
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Refuse<AuthenticationTicket?>();
    public Task RemoveAsync(string key) => Refuse<object>();
    private Task<T> Refuse<T>()
    {
        GC.KeepAlive(store);
        return Task.FromException<T>(new InvalidOperationException("Ticket operations are unavailable in disabled diagnostic mode."));
    }
}

sealed class DisabledProviderHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(new InvalidOperationException("Provider transport is unavailable in disabled diagnostic mode."));
}
