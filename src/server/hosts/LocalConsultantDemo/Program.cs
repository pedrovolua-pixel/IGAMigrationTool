using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentScoring;
using System.Globalization;
using AssessmentRuns;
using FindingReview;
using SyntheticFixReview;
using SyntheticPlanningTasks;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;

const int port = 5183;
if (!args.Contains("--synthetic-local-demo", StringComparer.Ordinal))
    throw new InvalidOperationException("This host requires --synthetic-local-demo and supports synthetic loopback use only.");
var connection = Environment.GetEnvironmentVariable("IGA_SYNTHETIC_DATABASE")
    ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle03;Username=iga_synthetic";
var phase1bEnabled = args.Contains("--enable-synthetic-phase1b", StringComparer.Ordinal);
var phase1bAuditor = args.Contains("--synthetic-phase1b-auditor", StringComparer.Ordinal);
if (phase1bEnabled)
{
    var phaseDb = new Npgsql.NpgsqlConnectionStringBuilder(connection);
    if (phaseDb.Host is not ("127.0.0.1" or "localhost") || phaseDb.Port != 55433 || phaseDb.Username != "iga_synthetic" || phaseDb.Database?.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal) != true)
        throw new InvalidOperationException("Phase1B requires an explicitly selected dedicated fictional database before initialization.");
}
if (phase1bAuditor && !phase1bEnabled) throw new InvalidOperationException("Auditor fixture requires Phase1B opt-in.");
var planningTasksEnabled = args.Contains("--enable-synthetic-planning-tasks", StringComparer.Ordinal);
if (planningTasksEnabled && !new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.StartsWith("iga_synthetic_cycle14_", StringComparison.Ordinal))
    throw new InvalidOperationException("Planning tasks require an explicitly selected dedicated Cycle14 synthetic database.");
var artifactReviewEnabled = args.Contains("--enable-synthetic-artifact-review", StringComparer.Ordinal);
if (artifactReviewEnabled && !new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.StartsWith("iga_synthetic_cycle13_", StringComparison.Ordinal))
    throw new InvalidOperationException("Artifact review requires an explicitly selected dedicated Cycle13 synthetic database.");
var engine = new SyntheticDurableRunEngine(connection, DemoFixtureCatalog.Scope,
    new SyntheticRunPolicy(TimeSpan.FromSeconds(5), 2, 512));
await engine.InitializeAsync();
var reviewStore = new SyntheticReviewStore(connection, SyntheticReviewScope.Fixed);
await reviewStore.InitializeAsync();
var reviewService = new DemoReviewService(reviewStore);
var artifactReviewService = new DemoArtifactReviewService(connection, engine, reviewService);
if (artifactReviewEnabled || planningTasksEnabled) await artifactReviewService.InitializeAsync();
var planningTaskService = new DemoPlanningTaskService(connection, engine, reviewService);
if (planningTasksEnabled) await planningTaskService.InitializeAsync();
DemoPhase1BService? phase1b = phase1bEnabled ? new(connection, engine, reviewStore, phase1bAuditor) : null;
if (phase1b is not null) { await phase1b.InitializeAsync(); await phase1b.InitializeCsv(); }
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>(), EnvironmentName = "SyntheticLocalDemo" });
builder.WebHost.ConfigureKestrel(server =>
{
    server.Listen(IPAddress.Loopback, port);
    server.Limits.MaxRequestBodySize = 65536;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "iga-local-demo-csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
});
builder.Services.AddSingleton(engine);
if (!args.Contains("--pause-synthetic-worker", StringComparer.Ordinal))
    builder.Services.AddHostedService<SyntheticDemoWorker>();
if (phase1b is not null && !args.Contains("--pause-synthetic-worker", StringComparer.Ordinal))
{ builder.Services.AddSingleton(phase1b); builder.Services.AddHostedService<DemoPhase1BWorker>(); }
var app = builder.Build();
app.Use(async (context, next) =>
{
    var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = context.Request.Path.StartsWithSegments("/local-demo/v1/phase1b") ? 65536 : context.Request.Path.Value?.Contains("/planning-tasks/", StringComparison.Ordinal) == true ? 16384 : 4096;
    var host = context.Request.Host.Value;
    if (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
        (host != $"127.0.0.1:{port}" && host != $"localhost:{port}"))
    {
        context.Response.StatusCode = 403;
        return;
    }
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/local-demo")) context.Response.Headers.CacheControl = "no-store";
    try
    {
        if (phase1bAuditor && context.Request.Path.StartsWithSegments("/local-demo") &&
            !(context.Request.Method == "GET" && (context.Request.Path == "/local-demo/v1/catalog" || context.Request.Path == "/local-demo/v1/phase1b/fixture" || context.Request.Path.Value!.EndsWith("/csv", StringComparison.Ordinal) || context.Request.Path.Value.EndsWith("/navigation", StringComparison.Ordinal)) ||
              context.Request.Method == "POST" && context.Request.Path.Value!.StartsWith("/local-demo/v1/phase1b/runs/", StringComparison.Ordinal) && context.Request.Path.Value.EndsWith("/csv", StringComparison.Ordinal)))
            throw new Phase1BDeniedException("Denied");
        if (context.Request.Method == "POST")
        {
            if (context.Request.Headers.Origin != $"http://{host}" ||
                !string.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                await DemoProjection.Error("Denied", "Use the same-origin demo screen to submit this operation.", 403).ExecuteAsync(context);
                return;
            }
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(context);
        }
        await next();
    }
    catch (Phase1BDeniedException error)
    {
        var status = error.Message == "InvalidInput" ? 400 : error.Message is "Denied" or "WrongScope" or "NotFound" ? 403 : 409;
        await DemoProjection.Error(error.Message, "This fictional operation is unavailable or its saved source changed. Refresh before a new action.", status).ExecuteAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        await DemoProjection.Error("Denied", "Reload the demo screen before retrying.", 403).ExecuteAsync(context);
    }
    catch (JsonException)
    {
        await DemoProjection.Error("InvalidInput", "The request must contain exactly the expected fields.", 400).ExecuteAsync(context);
    }
    catch (BadHttpRequestException)
    {
        await DemoProjection.Error("InvalidInput", "The request exceeds the demo limit or is malformed.", 400).ExecuteAsync(context);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        app.Logger.LogWarning("Synthetic demo dependency failure: {Kind}", exception.GetType().Name);
        if (!context.Response.HasStarted)
            await DemoProjection.Error("Unavailable", "The local database is unavailable. Saved runs have not been reset.", 503).ExecuteAsync(context);
    }
});
app.MapGet("/local-demo/v1/catalog", (HttpContext context, IAntiforgery antiforgery) =>
    Results.Json(DemoProjection.Catalog(antiforgery.GetAndStoreTokens(context).RequestToken!, phase1bEnabled)));
app.MapGet("/local-demo/v1/runs", async () => Results.Json(new
{
    schemaVersion = 1,
    demoOnly = true,
    runs = (await engine.ListAsync(DemoFixtureCatalog.Scope)).OrderByDescending(run => run.CreatedAt)
        .Take(200).Select(DemoProjection.Summary)
}));
app.MapGet("/local-demo/v1/runs/{runId:guid}", async (Guid runId) =>
    DemoProjection.Result(await engine.ReadAsync(DemoFixtureCatalog.Scope, runId)));
app.MapGet("/local-demo/v1/runs/{runId:guid}/analysis", async (HttpContext context, Guid runId) =>
{
    var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId);
    if (read.Succeeded && DemoPhase1BCatalog.IsProfile(read.Snapshot!.ProfileCatalogId))
        return phase1b is null ? DemoProjection.Error("Denied", "Phase1B requires its dedicated fictional host.", 403) : Results.Json((await phase1b.Workspace(runId, context.RequestAborted)), DemoPhase1BRoutes.Json);
    if (read.Succeeded && DemoPlanningTaskCatalog.IsProfile(read.Snapshot!.ProfileCatalogId))
    {
        if (!planningTasksEnabled) return DemoProjection.Error("Denied", "Start the dedicated Cycle14 synthetic host to use planning tasks.", 403);
        var capture = await planningTaskService.ReadAnalysisAsync(read.Snapshot!, context.RequestAborted);
        return capture is null ? DemoProjection.Error("Unavailable", "The current planning source could not be verified. Refresh the run.", 503) : Results.Json(capture);
    }
    if (read.Succeeded && DemoArtifactReviewCatalog.IsProfile(read.Snapshot!.ProfileCatalogId))
    {
        if (artifactReviewEnabled)
        {
            var captured = await artifactReviewService.ReadAnalysisAsync(read.Snapshot!, context.RequestAborted);
            return captured is null ? DemoProjection.Error("Unavailable", "The current artifact source could not be verified. Refresh the run.", 503)
                : Results.Json(captured);
        }
        var disabled = DemoAnalysisProjection.Capture(read.Snapshot!, await reviewService.ReadAsync(read.Snapshot!));
        disabled.Analysis["artifactReview"] = JsonSerializer.SerializeToNode(DemoArtifactReviewService.Detail(new(ArtifactReviewIssue.SourceUnavailable, null)), DemoReportDraftProjection.JsonOptions);
        return Results.Json(disabled.Analysis);
    }
    return read.Succeeded ? Results.Json(DemoAnalysisProjection.Detail(read.Snapshot!,
        DemoAnalysisCatalog.IsReviewMaturityProfile(read.Snapshot!.ProfileCatalogId) ? await reviewService.ReadAsync(read.Snapshot!) : null))
        : DemoProjection.Result(read);
});
app.MapGet("/local-demo/v1/runs/{runId:guid}/review", async (Guid runId) =>
{
    var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId);
    return read.Succeeded ? Results.Json(DemoReviewService.Detail(read.Snapshot!, await reviewService.ReadAsync(read.Snapshot!)))
        : DemoProjection.Result(read);
});
app.MapPost("/local-demo/v1/runs/{runId:guid}/findings/{findingId}/events", async (HttpContext context, Guid runId, string findingId) =>
{
    using var document = await DemoProjection.ReviewBody(context);
    var body = document.RootElement;
    var kindText = body.GetProperty("kind").GetString();
    if (!Guid.TryParse(body.GetProperty("eventId").GetString(), out var eventId) || eventId == Guid.Empty ||
        !body.GetProperty("expectedRevision").TryGetInt64(out var revision) || revision < 0 ||
        !Enum.TryParse<SyntheticReviewEventKind>(kindText, false, out var kind) || !Enum.IsDefined(kind) || kind.ToString() != kindText)
        return DemoProjection.Error("InvalidInput", "Use a valid event, action and current finding revision.", 400);
    var command = new SyntheticReviewCommand(eventId, revision, kind, body.GetProperty("reason").GetString(),
        body.GetProperty("text").GetString(), body.GetProperty("title").GetString(), body.GetProperty("businessContext").GetString());
    if (SyntheticReviewPolicy.ValidateCommand(command) is not null)
        return DemoProjection.Error("InvalidInput", "Supply only the bounded fields for the selected review action.", 400);
    var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId);
    if (!read.Succeeded) return DemoProjection.Result(read);
    if (DemoPhase1BCatalog.IsProfile(read.Snapshot!.ProfileCatalogId))
        return phase1b is null ? DemoProjection.Error("Denied", "Phase1B is disabled.", 403)
            : Results.Json(await phase1b.Review(runId, findingId, command, context.RequestAborted), DemoPhase1BRoutes.Json);
    var review = await reviewService.ReadAsync(read.Snapshot!);
    if (review.Snapshot is null) return DemoProjection.Error("Denied", "Review is unavailable for this saved run.", 403);
    var applied = await reviewService.ApplyAsync(runId, findingId, command);
    if (!applied.Succeeded)
        return DemoProjection.Error(applied.Issue.ToString()!, "The finding changed or the action is unavailable. Refresh before a new action.",
            applied.Issue == SyntheticReviewIssue.NotFound ? 404 : applied.Issue is SyntheticReviewIssue.Denied or SyntheticReviewIssue.WrongScope ? 403 : 409);
    // Replayed events preserve their original outcome in storage; the screen always receives fresh current history.
    var current = await reviewService.ReadAsync(read.Snapshot!);
    return current.Snapshot is null ? DemoProjection.Error("Unavailable", "The saved review could not be verified. Refresh the run.", 503)
        : Results.Json(DemoReviewService.Detail(read.Snapshot!, current));
});
app.MapPost("/local-demo/v1/runs/{runId:guid}/artifacts/{artifactId}/review", async (HttpContext context, Guid runId, string artifactId) =>
{
    var selected = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId, context.RequestAborted);
    if (!selected.Succeeded || !(artifactReviewEnabled && DemoArtifactReviewCatalog.IsProfile(selected.Snapshot!.ProfileCatalogId) || planningTasksEnabled && DemoPlanningTaskCatalog.IsProfile(selected.Snapshot!.ProfileCatalogId)))
        return DemoProjection.Error("Denied", "Artifact review is unavailable for this host and profile.", 403);
    if (!artifactReviewEnabled && !planningTasksEnabled) return DemoProjection.Error("Denied", "Artifact review is unavailable in this local host.", 403);
    using var document = await DemoProjection.Body(context, ["eventId", "kind", "expectedRevision", "expectedSourceDigest", "reason"]);
    var body = document.RootElement;
    if (!Guid.TryParseExact(body.GetProperty("eventId").GetString(), "D", out var eventId) || eventId == Guid.Empty ||
        !body.GetProperty("expectedRevision").TryGetInt64(out var revision) || revision < 0 || revision > 9007199254740991 ||
        body.GetProperty("kind").GetString() is not ("ReviewForPlanning" or "WithdrawReview"))
        return DemoProjection.Error("InvalidInput", "Use a valid artifact action and current source/revision.", 400);
    var command = new ArtifactReviewCommand(eventId, Enum.Parse<ArtifactReviewKind>(body.GetProperty("kind").GetString()!), revision,
        body.GetProperty("expectedSourceDigest").GetString()!, body.GetProperty("reason").GetString()!);
    var result = await artifactReviewService.ApplyAsync(runId, artifactId, command, context.RequestAborted);
    if (!result.Succeeded)
    {
        var status = result.Issue switch
        {
            ArtifactReviewIssue.Denied or ArtifactReviewIssue.WrongScope => 403,
            ArtifactReviewIssue.NotFound => 404,
            ArtifactReviewIssue.InvalidInput => 400,
            _ => 409
        };
        return DemoProjection.Error(result.Issue.ToString()!, "The artifact source changed or this operation is unavailable. Refresh before a new action.", status);
    }
    return Results.Json(new
    {
        schemaVersion = 1,
        demoOnly = true,
        issue = (string?)null,
        result.AlreadyApplied,
        receipt = DemoArtifactReviewService.Receipt(result.Receipt!)
    });
});
app.MapGet("/local-demo/v1/runs/{runId:guid}/planning-tasks", async (HttpContext context, Guid runId) =>
{
    if (!planningTasksEnabled) return DemoProjection.Error("Denied", "Planning tasks are disabled on this host.", 403);
    var read = await planningTaskService.ReadAsync(runId, context.RequestAborted);
    return Results.Json(DemoPlanningTaskService.Detail(read));
});
app.MapPost("/local-demo/v1/runs/{runId:guid}/planning-tasks/{taskId}/events", async (HttpContext context, Guid runId, string taskId) =>
{
    if (!planningTasksEnabled) return DemoProjection.Error("Denied", "Planning tasks are disabled on this host.", 403);
    using var body = await DemoProjection.TaskBody(context);
    var root = body.RootElement;
    var kindText = root.GetProperty("kind").GetString();
    if (!Guid.TryParseExact(root.GetProperty("eventId").GetString(), "D", out var eventId) || eventId == Guid.Empty ||
        !root.GetProperty("expectedRevision").TryGetInt64(out var revision) || revision < 0 ||
        !Enum.TryParse<PlanningTaskKind>(kindText, false, out var kind) || !Enum.IsDefined(kind) || kind.ToString() != kindText)
        return DemoProjection.Error("InvalidInput", "Use the current planning task revision and a valid action.", 400);
    var vectors = root.GetProperty("expectedAttestations");
    if (vectors.ValueKind != JsonValueKind.Array || vectors.GetArrayLength() != 3) throw new JsonException();
    var attestations = ImmutableArray.CreateBuilder<PlanningTaskAttestation>();
    foreach (var item in vectors.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 6 ||
            item.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 6 ||
            !new[] { "artifactId", "revision", "eventId", "kind", "state", "sourceDigest" }.All(name => item.TryGetProperty(name, out _))) throw new JsonException();
        if (item.GetProperty("artifactId").ValueKind != JsonValueKind.String || item.GetProperty("revision").ValueKind != JsonValueKind.Number ||
            !item.GetProperty("revision").TryGetInt64(out var artifactRevision) || item.GetProperty("state").ValueKind != JsonValueKind.String ||
            item.GetProperty("eventId").ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
            item.GetProperty("kind").ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
            item.GetProperty("sourceDigest").ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException();
        var artifactKind = item.GetProperty("kind");
        ArtifactReviewKind? acceptedKind = null;
        if (artifactKind.ValueKind != JsonValueKind.Null)
        { if (!Enum.TryParse<ArtifactReviewKind>(artifactKind.GetString(), false, out var value) || !Enum.IsDefined(value) || value.ToString() != artifactKind.GetString()) throw new JsonException(); acceptedKind = value; }
        if (!Enum.TryParse<ArtifactReviewState>(item.GetProperty("state").GetString(), false, out var state) || !Enum.IsDefined(state) || state.ToString() != item.GetProperty("state").GetString()) throw new JsonException();
        var acceptedEvent = item.GetProperty("eventId");
        Guid? acceptedId = null;
        if (acceptedEvent.ValueKind != JsonValueKind.Null)
        { if (!Guid.TryParseExact(acceptedEvent.GetString(), "D", out var id) || id == Guid.Empty) throw new JsonException(); acceptedId = id; }
        attestations.Add(new(item.GetProperty("artifactId").GetString()!, artifactRevision, acceptedId, acceptedKind, state, item.GetProperty("sourceDigest").GetString()));
    }
    var command = new PlanningTaskCommand(eventId, kind, revision, root.GetProperty("expectedSourceDigest").GetString()!, attestations.ToImmutable(), root.GetProperty("reason").GetString()!);
    var result = await planningTaskService.ApplyAsync(runId, taskId, command, context.RequestAborted);
    if (!result.Succeeded)
        return DemoProjection.Error(result.Issue.ToString()!, "The planning source changed or this action is unavailable. Refresh before a new action.", result.Issue switch
        { PlanningTaskIssue.InvalidInput => 400, PlanningTaskIssue.Denied or PlanningTaskIssue.WrongScope => 403, PlanningTaskIssue.NotFound => 404, _ => 409 });
    return Results.Json(new
    {
        schemaVersion = 1,
        demoOnly = true,
        issue = (string?)null,
        result.AlreadyApplied,
        receipt = result.Receipt is null ? null : DemoPlanningTaskService.Receipt(result.Receipt),
        result.AlreadyExistsTaskId
    });
});
app.MapPost("/local-demo/v1/runs", async (HttpContext context) =>
{
    using var body = await DemoProjection.Body(context, ["scopeId", "baselineId", "profileId", "requestId"]);
    var root = body.RootElement;
    var requestId = root.GetProperty("requestId").GetString();
    if (!Guid.TryParse(requestId, out _) || root.GetProperty("scopeId").GetString() != "demo-scope")
        return DemoProjection.Error("InvalidInput", "Choose a scope and presets from the demo catalog.", 400);
    var baselineId = root.GetProperty("baselineId").GetString();
    var profileId = root.GetProperty("profileId").GetString();
    if (!DemoFixtureCatalog.Baselines.Any(item => item.Id == baselineId) || !DemoFixtureCatalog.Profiles.Any(item => item.Id == profileId) ||
        !DemoAnalysisCatalog.Compatible(baselineId!, profileId!)) return DemoProjection.Error("InvalidInput", "The selected demo presets are unavailable.", 400);
    if (DemoPlanningTaskCatalog.IsProfile(profileId) && !planningTasksEnabled)
        return DemoProjection.Error("Denied", "Start the dedicated Cycle14 synthetic host to use planning tasks.", 403);
    if (DemoArtifactReviewCatalog.IsProfile(profileId) && !artifactReviewEnabled)
        return DemoProjection.Error("Denied", "Start the dedicated Cycle13 synthetic host to use artifact review.", 403);
    var request = DemoFixtureCatalog.CreateStartRequest(baselineId!, profileId!, requestId!);
    var result = await engine.StartAsync(request);
    return DemoProjection.Result(result, result.AlreadyApplied ? 200 : 201);
});
app.MapPost("/local-demo/v1/runs/{runId:guid}/cancel", async (HttpContext context, Guid runId) =>
{
    using var body = await DemoProjection.Body(context, ["expectedRevision", "requestId"]);
    if (!Guid.TryParse(body.RootElement.GetProperty("requestId").GetString(), out _) ||
        !body.RootElement.GetProperty("expectedRevision").TryGetInt64(out var revision) || revision < 0)
        return DemoProjection.Error("InvalidInput", "Use the current run revision and a valid request ID.", 400);
    return DemoProjection.Result(await engine.RequestCancelAsync(DemoFixtureCatalog.Scope, runId, revision));
});
app.MapPost("/local-demo/v1/runs/{runId:guid}/resume", async (HttpContext context, Guid runId) =>
{
    using var body = await DemoProjection.Body(context, ["expectedRevision", "requestId"]);
    if (!Guid.TryParse(body.RootElement.GetProperty("requestId").GetString(), out _) ||
        !body.RootElement.GetProperty("expectedRevision").TryGetInt64(out var revision) || revision < 0)
        return DemoProjection.Error("InvalidInput", "Use the current run revision and a valid request ID.", 400);
    // A release makes it immediately available to the resident recovery worker; expired owners stay fenced.
    var acquired = await engine.AcquireLeaseAsync(DemoFixtureCatalog.Scope, runId, "manual-recovery", revision);
    if (!acquired.Succeeded) return DemoProjection.Result(acquired);
    return DemoProjection.Result(await engine.ReleaseLeaseAsync(DemoFixtureCatalog.Scope, runId,
        acquired.Snapshot!.Lease!.Generation));
});
var webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/web/dist"));
if (!Directory.Exists(webRoot)) throw new InvalidOperationException("Build src/web before starting the local demo from the repository root.");
var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
if (phase1b is not null) DemoPhase1BRoutes.Map(app, phase1b);
await app.RunAsync();

internal static class DemoProjection
{
    internal static async Task<JsonDocument> ReviewBody(HttpContext context)
    {
        var document = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 4 });
        var fields = new[] { "eventId", "expectedRevision", "kind", "reason", "text", "title", "businessContext" };
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(fields.Order(StringComparer.Ordinal)) || fields.Any(field => root.GetProperty(field).ValueKind !=
                (field == "expectedRevision" ? JsonValueKind.Number : JsonValueKind.String) &&
                !(field is "reason" or "text" or "title" or "businessContext" && root.GetProperty(field).ValueKind == JsonValueKind.Null)))
        {
            document.Dispose();
            throw new JsonException("Unexpected review fields or values.");
        }
        return document;
    }

    internal static async Task<JsonDocument> TaskBody(HttpContext context)
    {
        var document = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 4 });
        try
        {
            var root = document.RootElement;
            string[] fields = ["eventId", "kind", "expectedRevision", "expectedSourceDigest", "expectedAttestations", "reason"];
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal)))
            { document.Dispose(); throw new JsonException(); }
            foreach (var field in fields)
            {
                var expected = field == "expectedRevision" ? JsonValueKind.Number : field == "expectedAttestations" ? JsonValueKind.Array : JsonValueKind.String;
                if (root.GetProperty(field).ValueKind != expected) { document.Dispose(); throw new JsonException(); }
            }
            ValidateTaskStrings(document.RootElement);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            document.Dispose();
            throw new JsonException("Invalid task string encoding.");
        }
        return document;
    }

    private static void ValidateTaskStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(text)) != text)
                throw new JsonException("Invalid task string encoding.");
        }
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                var name = property.Name;
                if (JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(name)) != name)
                    throw new JsonException("Invalid task field encoding.");
                ValidateTaskStrings(property.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) ValidateTaskStrings(item);
    }

    internal static async Task<JsonDocument> Body(HttpContext context, string[] fields)
    {
        var document = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(fields.Order(StringComparer.Ordinal)) is false)
        {
            document.Dispose();
            throw new JsonException("Unexpected request fields.");
        }
        foreach (var field in fields)
            if (root.GetProperty(field).ValueKind != (field == "expectedRevision" ? JsonValueKind.Number : JsonValueKind.String))
            {
                document.Dispose();
                throw new JsonException("Unexpected request value kind.");
            }
        return document;
    }

    internal static IResult Error(string code, string message, int status, long? revision = null) =>
        Results.Json(new { schemaVersion = 1, code, message, correlationId = (string?)null, currentRevision = revision }, statusCode: status);

    internal static IResult Result(SyntheticRunCommandResult result, int success = 200)
    {
        if (result.Succeeded) return Results.Json(Detail(result.Snapshot!), statusCode: success);
        var status = result.Issue switch
        {
            SyntheticRunIssue.NotFound => 404,
            SyntheticRunIssue.WrongScope => 403,
            SyntheticRunIssue.InvalidInput or SyntheticRunIssue.InvalidPlan => 400,
            _ => 409
        };
        return Error(result.Issue.ToString()!, "The run changed or the requested operation is unavailable. Refresh before retrying.", status, result.Snapshot?.Revision);
    }

    internal static object Catalog(string csrfToken, bool phase1b = false) => new
    {
        schemaVersion = 1,
        demoOnly = true,
        scopes = new[] { new { id = "demo-scope", label = "Synthetic One Identity environment" } },
        baselines = (phase1b ? DemoFixtureCatalog.Baselines.Concat([DemoPhase1BCatalog.CreateBaseline()]) : DemoFixtureCatalog.Baselines).Select(item => new
        {
            id = item.Id,
            label = item.Name,
            scopeId = "demo-scope",
            version = SyntheticBaselineVersion,
            warnings = Warnings(item.Id)
        }),
        profiles = (phase1b ? DemoFixtureCatalog.Profiles.Concat([DemoPhase1BCatalog.CreateProfile()]) : DemoFixtureCatalog.Profiles).Select(item => new { id = item.Id, label = item.Name, version = item.Versions.ProfileVersion, baselineIds = (phase1b ? DemoFixtureCatalog.Baselines.Concat([DemoPhase1BCatalog.CreateBaseline()]) : DemoFixtureCatalog.Baselines).Where(baseline => DemoAnalysisCatalog.Compatible(baseline.Id, item.Id)).Select(baseline => baseline.Id) }),
        csrfToken
    };
    private const string SyntheticBaselineVersion = "synthetic-baseline-inventory-v1";
    private static object Selection(SyntheticRunSnapshot run) => new
    {
        scopeId = "demo-scope",
        scopeLabel = "Synthetic One Identity environment",
        baselineId = run.BaselineCatalogId,
        baselineLabel = run.BaselineCatalogId == DemoPhase1BCatalog.BaselineId ? DemoPhase1BCatalog.CreateBaseline().Name : DemoFixtureCatalog.Baselines.Single(item => item.Id == run.BaselineCatalogId).Name,
        profileId = run.ProfileCatalogId,
        profileLabel = DemoPhase1BCatalog.IsProfile(run.ProfileCatalogId) ? DemoPhase1BCatalog.CreateProfile().Name : DemoFixtureCatalog.Profiles.Single(item => item.Id == run.ProfileCatalogId).Name
    };
    private static object Progress(SyntheticRunSnapshot run) => new
    {
        run.Progress.PlannedUnits,
        run.Progress.TerminalUnits,
        run.Progress.RemainingUnits,
        run.Progress.AllTerminal
    };
    internal static object Summary(SyntheticRunSnapshot run) => new
    {
        runId = run.RunId,
        run.Revision,
        state = run.State.ToString(),
        run.CancelRequested,
        createdAtUtc = run.CreatedAt,
        updatedAtUtc = run.UpdatedAt,
        selection = Selection(run),
        progress = Progress(run),
        coverageCompletionKind = run.CoverageSummary?.Kind.ToString()
    };
    internal static object Detail(SyntheticRunSnapshot run)
    {
        var active = run.State is SyntheticRunState.Planned or SyntheticRunState.Running;
        var available = run.Lease is null || run.Lease.ExpiresAt <= run.ObservedAtDatabaseUtc;
        var fixtureMatches = DemoAnalysisCatalog.MatchesFrozenFixture(run);
        var limits = run.Results.Where(item => item.ReasonCode is not null && item.State != CoverageState.NotApplicable)
            .GroupBy(item => new { item.State, item.ReasonCode, item.ResponsibleStage })
            .Select(group => new { state = group.Key.State.ToString(), group.Key.ReasonCode, group.Key.ResponsibleStage, count = group.Count() });
        var versions = run.FrozenInputs;
        var refs = new (string Name, string Version)[]
        {
            ("Baseline", run.Plan.BaselineId), ("Profile", versions.ProfileVersion),
            ("Rule catalog", run.Plan.CapabilityLock.RuleCatalogVersion), ("Capability", run.Plan.CapabilityLock.MatrixVersion),
            ("Desired outcomes", versions.DesiredOutcomeVersion ?? "disabled"), ("Scoring algorithm", versions.ScoringAlgorithmVersion),
            ("AI policy", versions.AiPolicyVersion), ("Prompt", versions.PromptVersion), ("Model", versions.ModelVersion),
            ("Application", versions.ApplicationVersion), ("Work schema", versions.WorkSchemaVersion)
        };
        return new
        {
            schemaVersion = 1,
            demoOnly = true,
            runId = run.RunId,
            run.Revision,
            state = run.State.ToString(),
            run.CancelRequested,
            createdAtUtc = run.CreatedAt,
            updatedAtUtc = run.UpdatedAt,
            selection = Selection(run),
            progress = Progress(run),
            coverageCompletionKind = run.CoverageSummary?.Kind.ToString(),
            lockedInputs = refs.Select(item => new { name = item.Name, version = item.Version, sha256 = Digest(item.Version) })
                .Append(new { name = "Complete frozen input", version = "synthetic-input-lock-v1", sha256 = run.InputDigest })
                .Append(new { name = "Exact capability tuple", version = run.Plan.CapabilityLock.MatrixVersion, sha256 = run.Plan.CapabilityLock.LockDigest })
                .Append(new { name = "Scripted result fixture", version = "synthetic-outcomes-v1", sha256 = versions.ScriptedResultsDigest })
                .Concat(versions.PlanningTaskContractDigest is null ? [] : new[] { new { name = "Planning task contract", version = "synthetic-planning-task-contract-v1", sha256 = versions.PlanningTaskContractDigest } })
                .Concat(versions.FixReviewContractDigest is null ? [] : new[] { new { name = "Artifact review contract", version = "synthetic-fix-review-contract-v1", sha256 = versions.FixReviewContractDigest } })
                .Concat(versions.AnalysisFixtureDigest is null ? [] : new[] { new { name = "Frozen analysis contents", version = "synthetic-analysis-lock-v1", sha256 = versions.AnalysisFixtureDigest } })
                .Concat(!DemoAiPreviewCatalog.IsProfile(run.ProfileCatalogId) || versions.AiPreviewFixtureDigest is null ? [] : new[]
                {
                    new { name = "Frozen offline AI contents", version = "synthetic-ai-demo-fixture-v1", sha256 = versions.AiPreviewFixtureDigest },
                    new { name = "Offline AI configuration template", version = "synthetic-ai-configuration-v1", sha256 = DemoAiPreviewCatalog.PacketTemplateDigest }
                })
                .Concat(!(DemoFixPackageCatalog.MatchesFrozenFixture(run) || DemoArtifactReviewCatalog.MatchesFrozenFixture(run) || DemoPlanningTaskCatalog.MatchesFrozenFixture(run)) || versions.FixPackageTemplateDigest is null ? [] : new[]
                {
                    new { name = "Fictional fix-package templates", version = DemoFixPackageCatalog.TemplateVersion, sha256 = versions.FixPackageTemplateDigest }
                }),
            warnings = PermissionWarnings(run.Plan.HasPermissionWarning),
            stateCounts = run.Progress.TerminalStateCounts.Select(item => new { state = item.State.ToString(), item.Count }),
            executableCoverage = run.CoverageSummary is null ? null : new
            {
                numerator = run.CoverageSummary.ExecutableCoverage.ExecutedUnits,
                denominator = run.CoverageSummary.ExecutableCoverage.ApplicablePlannedUnits,
                run.CoverageSummary.ExecutableCoverage.HasApplicableUnits
            },
            limitations = limits,
            actions = new
            {
                canCancel = active && !run.CancelRequested,
                canResume = active && !run.CancelRequested && available && fixtureMatches,
                resumeAvailableAtUtc = active && !run.CancelRequested && run.Lease is not null ? run.Lease.ExpiresAt : (DateTimeOffset?)null,
                reasonCode = !fixtureMatches ? "frozen_fixture_mismatch" : run.ScoringPaused ? "scoring_not_implemented" : run.CancelRequested ? "cancellation_requested" : !available ? "worker_lease_active" : null
            }
        };
    }
    private static object[] Warnings(string baselineId) => PermissionWarnings((baselineId == DemoPhase1BCatalog.BaselineId ? DemoPhase1BCatalog.CreateBaseline() : DemoFixtureCatalog.Baselines.Single(item => item.Id == baselineId)).Inventory.Permission == AssessmentOrchestration.SyntheticBaselinePermission.EligibleWithWarning);
    private static object[] PermissionWarnings(bool warning) => warning
        ? [new { code = "synthetic_permission_warning", message = "This synthetic preset carries an explicit baseline permission warning." }]
        : [];
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

internal sealed class SyntheticDemoWorker(SyntheticDurableRunEngine engine, ILogger<SyntheticDemoWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, Guid> owned = [];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var listed in await engine.ListAsync(DemoFixtureCatalog.Scope, stoppingToken))
                {
                    if (DemoPhase1BCatalog.IsProfile(listed.ProfileCatalogId) || listed.State is not (SyntheticRunState.Planned or SyntheticRunState.Running)) continue;
                    var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, listed.RunId, stoppingToken);
                    if (!read.Succeeded) continue;
                    var run = read.Snapshot!;
                    if (run.CancelRequested)
                    {
                        var final = await engine.FinalizeCancellationAsync(run.Scope, run.RunId, run.Revision,
                            owned.TryGetValue(run.RunId, out var token) ? token : null, stoppingToken);
                        if (final.Succeeded) owned.Remove(run.RunId);
                        continue;
                    }
                    if (!DemoAnalysisCatalog.MatchesFrozenFixture(run)) continue;
                    if (!owned.TryGetValue(run.RunId, out var generation))
                    {
                        var acquire = await engine.AcquireLeaseAsync(run.Scope, run.RunId, "local-consultant-worker", null, stoppingToken);
                        if (!acquire.Succeeded) continue;
                        run = acquire.Snapshot!;
                        generation = run.Lease!.Generation;
                        owned[run.RunId] = generation;
                    }
                    if (run.Lease is null || run.Lease.Generation != generation)
                    {
                        owned.Remove(run.RunId);
                        continue;
                    }
                    if (run.Lease.ExpiresAt <= run.ObservedAtDatabaseUtc.AddSeconds(2))
                    {
                        var heartbeat = await engine.HeartbeatAsync(run.Scope, run.RunId, generation, stoppingToken);
                        if (!heartbeat.Succeeded) { owned.Remove(run.RunId); continue; }
                        run = heartbeat.Snapshot!;
                    }
                    var existing = run.Results.Select(item => item.Key).ToHashSet();
                    var next = DemoAnalysisCatalog.WorkResults(run).FirstOrDefault(item => !existing.Contains(item.Key));
                    SyntheticRunCommandResult result;
                    if (next is null)
                        result = await engine.CompleteCoverageAsync(run.Scope, run.RunId, generation, run.Revision, stoppingToken);
                    else
                    {
                        var begin = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, [next.Key], stoppingToken);
                        if (!begin.Succeeded) continue;
                        await Task.Delay(300, stoppingToken); // Explicit simulated read-only fixture work, not a source read.
                        var latest = await engine.ReadAsync(run.Scope, run.RunId, stoppingToken);
                        if (!latest.Succeeded) continue;
                        result = await engine.CheckpointAsync(run.Scope, run.RunId, generation, latest.Snapshot!.Revision, [next], stoppingToken);
                    }
                    if (!result.Succeeded || result.Snapshot!.ScoringPaused) owned.Remove(run.RunId);
                }
                foreach (var notification in await engine.ReadOutboxAsync(DemoFixtureCatalog.Scope, null, stoppingToken))
                    if (notification.DispatchedAt is null)
                        await engine.DeliverOutboxAsync(DemoFixtureCatalog.Scope, notification.EventId, "local-demo-notifications", stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Synthetic recovery is waiting after dependency failure: {Kind}", exception.GetType().Name);
                owned.Clear();
            }
            await Task.Delay(300, stoppingToken);
        }
    }
}
