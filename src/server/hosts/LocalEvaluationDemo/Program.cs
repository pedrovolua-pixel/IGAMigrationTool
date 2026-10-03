using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using SyntheticEvaluationWorkflow;
using SyntheticEvaluationWorkflowFixtures;

const int port = 5184;
if (args.Length != 1 || args[0] != "--enable-synthetic-evaluation-workflow")
    throw new InvalidOperationException("Use the explicit local synthetic evaluation workflow flag.");
var connection = Environment.GetEnvironmentVariable("IGA_SYNTHETIC_EVALUATION_DATABASE")
    ?? throw new InvalidOperationException("An explicit dedicated synthetic evaluation database is required.");
var assetsSetting = Environment.GetEnvironmentVariable("IGA_SYNTHETIC_EVALUATION_ASSETS")
    ?? throw new InvalidOperationException("An explicit dedicated evaluation asset directory is required.");
var assetRoot = Path.GetFullPath(assetsSetting);
if (!assetRoot.EndsWith(Path.Combine("src", "web", "evaluation", "dist"), StringComparison.Ordinal) ||
    !Directory.Exists(assetRoot) || !File.Exists(Path.Combine(assetRoot, "index.html")) || new DirectoryInfo(assetRoot).LinkTarget is not null)
    throw new InvalidOperationException("Build the dedicated local evaluation workspace first.");
var assets = new Dictionary<string, (string Path, string Type)>(StringComparer.Ordinal);
foreach (var path in Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories))
{
    var info = new FileInfo(path);
    if (info.LinkTarget is not null) throw new InvalidOperationException("Dedicated assets cannot be symbolic links.");
    var parent = info.Directory;
    while (parent is not null && parent.FullName.StartsWith(assetRoot, StringComparison.Ordinal))
    {
        if (parent.LinkTarget is not null) throw new InvalidOperationException("Dedicated assets cannot contain symbolic directories.");
        parent = parent.Parent;
    }
    var type = Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", _ => null
    };
    if (type is not null) assets.Add(Path.GetRelativePath(assetRoot, path).Replace(Path.DirectorySeparatorChar, '/'), (path, type));
}
SyntheticEvaluationWorkflowStore store;
try
{
    store = new SyntheticEvaluationWorkflowStore(connection);
    await store.InitializeAsync();
    var seed = await store.SeedAsync(FictionalEvaluationFixture.BuildSeed());
    if (seed.Issue is not null) throw new InvalidOperationException();
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    // Startup also suppresses database/provider exception details; no inner payload is retained.
    throw new InvalidOperationException("The dedicated synthetic evaluation store could not be verified.");
}
var actor = new EvaluationWorkflowActor("synthetic-reviewer");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "SyntheticEvaluationWorkflow" });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(server =>
{
    server.Listen(IPAddress.Loopback, port);
    server.Limits.MaxRequestBodySize = EvaluationTransport.BodyLimit;
});
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "iga-local-evaluation-csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    var host = context.Request.Host.Value;
    if (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
        host != $"127.0.0.1:{port}" && host != $"localhost:{port}")
    {
        await EvaluationTransport.Error("Denied").ExecuteAsync(context);
        return;
    }
    var path = context.Request.Path.Value ?? "";
    var history = path.StartsWith("/local-evaluation/v1/members/", StringComparison.Ordinal) && path.EndsWith("/history", StringComparison.Ordinal);
    if (context.Request.Query.Count != 0 && (!history || context.Request.Query.Count != 1 ||
        !context.Request.Query.TryGetValue("afterSequence", out var cursor) || cursor.Count != 1 ||
        !EvaluationTransport.TryRevision(cursor[0], out _)))
    {
        await EvaluationTransport.Error("InvalidInput").ExecuteAsync(context);
        return;
    }
    if (context.Request.Method is not ("GET" or "POST"))
    {
        await EvaluationTransport.Error("InvalidInput").ExecuteAsync(context);
        return;
    }
    try
    {
        if (context.Request.Method == "POST")
        {
            if (path != "/local-evaluation/v1/events")
            {
                await EvaluationTransport.Error("NotFound").ExecuteAsync(context);
                return;
            }
            if (context.Request.Headers.Origin.Count != 1 || context.Request.Headers.Origin != $"http://{host}" ||
                !string.Equals(context.Request.ContentType, "application/json", StringComparison.Ordinal))
            {
                await EvaluationTransport.Error("Denied").ExecuteAsync(context);
                return;
            }
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        await next();
    }
    catch (AntiforgeryValidationException)
    {
        if (!context.Response.HasStarted) await EvaluationTransport.Error("Denied").ExecuteAsync(context);
    }
    catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
    {
        if (!context.Response.HasStarted) await EvaluationTransport.Error("InvalidInput").ExecuteAsync(context);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        if (!context.Response.HasStarted) await EvaluationTransport.Error("Unavailable").ExecuteAsync(context);
    }
});
app.MapGet("/local-evaluation/v1/workspace", async (HttpContext context, IAntiforgery antiforgery) =>
{
    var result = await store.ReadAsync(actor, context.RequestAborted);
    return result.Issue is not null ? EvaluationTransport.Error(result.Issue.Value.ToString()) :
        EvaluationTransport.Reply(result.Snapshot!, antiforgery.GetAndStoreTokens(context).RequestToken!);
});
app.MapGet("/local-evaluation/v1/members/{memberId}", async (HttpContext context, string memberId) =>
{
    var result = await store.ReadMemberAsync(memberId, actor, context.RequestAborted);
    return result.Issue is not null ? EvaluationTransport.Error(result.Issue.Value.ToString()) : EvaluationTransport.Reply(result);
});
app.MapGet("/local-evaluation/v1/versions/{version}", async (HttpContext context, string version) =>
{
    if (!EvaluationTransport.TryRevision(version, out var number)) return EvaluationTransport.Error("InvalidInput");
    var result = await store.ReadVersionAsync(number, actor, context.RequestAborted);
    return result.Issue is not null ? EvaluationTransport.Error(result.Issue.Value.ToString()) : EvaluationTransport.Reply(result.Version!);
});
app.MapGet("/local-evaluation/v1/members/{memberId}/history", async (HttpContext context, string memberId) =>
{
    var after = context.Request.Query.TryGetValue("afterSequence", out var value) ? long.Parse(value[0]!, System.Globalization.CultureInfo.InvariantCulture) : 0;
    var result = await store.ReadHistoryAsync(memberId, after, actor, context.RequestAborted);
    return result.Issue is not null ? EvaluationTransport.Error(result.Issue.Value.ToString()) : EvaluationTransport.Reply(result.History!);
});
app.MapPost("/local-evaluation/v1/events", async (HttpContext context) =>
{
    var command = await EvaluationTransport.ReadCommandAsync(context.Request, context.RequestAborted);
    var result = await store.ApplyAsync(command, actor, context.RequestAborted);
    return result.Issue is not null ? EvaluationTransport.Error(result.Issue.Value.ToString()) : EvaluationTransport.Reply(result.Receipt!, alreadyApplied: result.AlreadyApplied);
});
app.MapGet("/{**assetPath}", (string? assetPath) =>
{
    var name = string.IsNullOrEmpty(assetPath) ? "index.html" : assetPath;
    if (!assets.TryGetValue(name, out var asset)) return EvaluationTransport.Error("NotFound");
    return Results.File(asset.Path, asset.Type);
});
await app.RunAsync();
