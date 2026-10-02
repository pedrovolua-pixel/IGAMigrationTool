using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;

// This explicit development executable exposes no product, identity or data operation.
if (args.Length != 3 ||
    args[0] != "--azure-development-bootstrap" ||
    args[1] != "--role" ||
    (args[2] != "web" && args[2] != "worker"))
{
    Console.Error.WriteLine("Explicit Azure development bootstrap role is required.");
    return 2;
}

if (args[2] == "worker")
{
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, signal) =>
    {
        signal.Cancel = true;
        shutdown.Cancel();
    };
    using var termination = OperatingSystem.IsWindows() ? null :
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, signal =>
        {
            signal.Cancel = true;
            shutdown.Cancel();
        });
    try
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
        // Graceful process drain only: there is no work to receive or acknowledge.
    }
    return 0;
}

// Empty configuration prevents ASPNETCORE_URLS, command-line configuration and
// repository settings from changing this internal HTTP-only probe listener.
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
var app = builder.Build();
app.Run(async context =>
{
    context.Response.Headers.CacheControl = "no-store";
    var target = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
    if (HttpMethods.IsGet(context.Request.Method) && string.Equals(target, "/health/live", StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"bootstrapOnly\":true}");
    }
    else if (HttpMethods.IsGet(context.Request.Method) && string.Equals(target, "/health/ready", StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"code\":\"product_not_enabled\",\"bootstrapOnly\":true}");
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }
});
await app.RunAsync();
return 0;
