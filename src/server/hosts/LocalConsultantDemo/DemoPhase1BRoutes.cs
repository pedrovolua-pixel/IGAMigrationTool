using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssessmentRuns;
using FindingReview;
using SyntheticAiExecution;
using SyntheticFixReview;
using SyntheticOutcomePriority;
using SyntheticPlanningTasks;

internal static class DemoPhase1BRoutes
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }, MaxDepth = 32 };
    private sealed record StartBody(Guid RequestId, ImmutableArray<OutcomeSelection> Selections, bool ExplicitlyNoOutcomes);
    private sealed record ExportBody(Guid RequestId, string SnapshotDigest);
    internal static void Map(WebApplication app, DemoPhase1BService service)
    {
        const string prefix = "/local-demo/v1/phase1b";
        app.MapGet(prefix + "/registry", async (HttpContext context) => { service.Mutable(); return Reply(await service.Registry(context.RequestAborted), Json); });
        app.MapGet(prefix + "/fixture", () => Reply(new
        {
            schemaVersion = 1,
            demoOnly = true,
            availableCoverageKeys = DemoPhase1BCatalog.DeterministicPlan.Units.Select(u => new { u.Key.InventoryId, u.Key.EvidenceCategory, u.CategoryId }).Concat(DemoPhase1BCatalog.AiKeys.Select(k => new { k.InventoryId, k.EvidenceCategory, CategoryId = "OPERATIONS" })).OrderBy(k => k.InventoryId).ThenBy(k => k.EvidenceCategory),
            actor = service.Auditor ? "synthetic-auditor" : "synthetic-consultant",
            canManageOutcomes = !service.Auditor,
            canSimulateCustomerApproval = !service.Auditor,
            canStart = !service.Auditor,
            fictionalCustomerApprover = "synthetic-customer-outcome-approver-v1",
            customerApprovalLabel = "Simulate fictional customer approval"
        }, Json));
        app.MapPost(prefix + "/outcomes/events", async (HttpContext context) =>
        {
            var command = await Body<OutcomeCommand>(context);
            if (command.Kind == OutcomeKind.CreateDraft && command.Content is { } content)
            { var sealedContent = OutcomePriorityCanonical.Seal(content); if (content.ContentDigest != "" && content.ContentDigest != sealedContent.ContentDigest) throw new Phase1BDeniedException("InvalidInput"); command = command with { Content = sealedContent, ExpectedContentDigest = sealedContent.ContentDigest }; }
            return Reply(await service.Outcome(command, context.RequestAborted), Json);
        });
        app.MapPost(prefix + "/runs", async (HttpContext context) =>
        {
            var body = await Body<StartBody>(context);
            if (body.Selections.IsDefault || body.Selections.Length > 64 || body.ExplicitlyNoOutcomes != body.Selections.IsEmpty) throw new Phase1BDeniedException("InvalidInput");
            var result = await service.Start(body.RequestId, body.Selections, context.RequestAborted); return DemoProjection.Result(result, result.AlreadyApplied ? 200 : 201);
        });
        app.MapGet(prefix + "/runs/{runId:guid}/ledger", async (HttpContext context, Guid runId) => Reply(await service.Ledger(runId, context.RequestAborted), Json));
        app.MapGet(prefix + "/runs/{runId:guid}/workspace", async (HttpContext context, Guid runId) => Reply(await service.Workspace(runId, context.RequestAborted), Json));
        app.MapPost(prefix + "/runs/{runId:guid}/findings/{findingId}/events", async (HttpContext context, Guid runId, string findingId) =>
            Reply(await service.Review(runId, findingId, await Body<SyntheticReviewCommand>(context), context.RequestAborted), Json));
        app.MapPost(prefix + "/runs/{runId:guid}/artifacts/{artifactId}/events", async (HttpContext context, Guid runId, string artifactId) =>
            Reply(await service.Artifact(runId, artifactId, await Body<ArtifactReviewCommand>(context), context.RequestAborted), Json));
        app.MapPost(prefix + "/runs/{runId:guid}/tasks/{taskId}/events", async (HttpContext context, Guid runId, string taskId) =>
            Reply(await service.TaskEvent(runId, taskId, await Body<PlanningTaskCommand>(context), context.RequestAborted), Json));
        app.MapPost(prefix + "/runs/{runId:guid}/planning/events", async (HttpContext context, Guid runId) =>
            Reply(await service.Planning(runId, await Body<PlanningCommand>(context), context.RequestAborted), Json));
        app.MapPost(prefix + "/runs/{runId:guid}/ai/budget/events", async (HttpContext context, Guid runId) =>
        {
            var result = await service.Override(runId, await Body<AiOverrideCommand>(context), context.RequestAborted);
            return Reply(new { runId, result.Issue, result.Value, result.Replayed }, Json);
        });
        app.MapGet(prefix + "/runs/{runId:guid}/csv", async (HttpContext context, Guid runId) => Reply(await service.Inspect(runId, context.RequestAborted), Json));
        app.MapGet(prefix + "/runs/{runId:guid}/navigation", async (HttpContext context, Guid runId) =>
        {
            if (context.Request.Query.Count != 2 || !context.Request.Query.TryGetValue("view", out var view) || !context.Request.Query.TryGetValue("id", out var id) || view.Count != 1 || id.Count != 1)
                throw new Phase1BDeniedException("Denied");
            return Reply(await service.Navigate(runId, view[0]!, id[0]!, context.RequestAborted), Json);
        });
        app.MapPost(prefix + "/runs/{runId:guid}/csv", async (HttpContext context, Guid runId) =>
        {
            var body = await Body<ExportBody>(context);
            var directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/server/workers/SyntheticCsvRenderer/bin/Release/net10.0"));
            return await service.Export(runId, body.RequestId, body.SnapshotDigest, directory, context.RequestAborted);
        });
    }
    private static IResult Reply(object value, JsonSerializerOptions? options = null)
    {
        var node = JsonSerializer.SerializeToNode(value, Json)!.AsObject(); node["schemaVersion"] = 1; node["demoOnly"] = true;
        return Results.Json(node, Json);
    }
    private static async Task<T> Body<T>(HttpContext context)
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 32 }, context.RequestAborted);
        Validate(doc.RootElement);
        ValidateShape(doc.RootElement, typeof(T));
        return doc.RootElement.Deserialize<T>(Json) ?? throw new JsonException();
    }
    private static void ValidateShape(JsonElement value, Type type)
    {
        if (value.ValueKind == JsonValueKind.Null) return; // Domain validators decide explicitly permitted nulls.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        { if (value.ValueKind != JsonValueKind.Array) throw new JsonException(); foreach (var child in value.EnumerateArray()) ValidateShape(child, type.GetGenericArguments()[0]); return; }
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || Nullable.GetUnderlyingType(type) is not null) return;
        var properties = type.GetProperties(); var names = properties.Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names)) throw new JsonException();
        foreach (var property in properties) ValidateShape(value.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(property.Name)), property.PropertyType);
    }
    private static void Validate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var field in element.EnumerateObject()) { if (!names.Add(field.Name)) throw new JsonException(); Validate(field.Value); } }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Validate(child);
        else if (element.ValueKind == JsonValueKind.String)
        { string text; try { text = element.GetString()!; } catch (InvalidOperationException ex) { throw new JsonException("Invalid Unicode string.", ex); } for (var i = 0; i < text.Length; i++) if (char.IsSurrogate(text[i])) { if (!char.IsHighSurrogate(text[i]) || ++i >= text.Length || !char.IsLowSurrogate(text[i])) throw new JsonException(); } }
    }
}
