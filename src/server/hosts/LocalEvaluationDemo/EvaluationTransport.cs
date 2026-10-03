using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SyntheticEvaluation;
using SyntheticEvaluationWorkflow;

internal static class EvaluationTransport
{
    internal const int BodyLimit = 256 * 1024;
    internal const int ResponseLimit = 16 * 1024 * 1024;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal static IResult Reply(object payload, string? csrfToken = null, bool? alreadyApplied = null) =>
        Buffered(new(1, true, csrfToken, null, payload, alreadyApplied), 200);

    internal static IResult Error(string issue)
    {
        var status = issue switch
        {
            "InvalidInput" => 400,
            "Denied" => 403,
            "NotFound" => 404,
            "RevisionConflict" or "RegistryConflict" or "SourceConflict" or "EventConflict" => 409,
            _ => 503
        };
        return Buffered(new(1, true, null, issue, null, null), status);
    }

    private static IResult Buffered(Envelope envelope, int status)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        if (bytes.Length > ResponseLimit)
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, true, null, "Unavailable", null, null), JsonOptions);
            status = 503;
        }
        return new BufferedJsonResult(bytes, status);
    }

    internal static async Task<EvaluationWorkflowCommand> ReadCommandAsync(HttpRequest request, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await request.Body.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + count > BodyLimit) throw new JsonException();
            await buffer.WriteAsync(chunk.AsMemory(0, count), token);
        }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        var body = document.RootElement;
        Keys(body, ["eventId", "memberId", "kind", "expectedAggregateRevision", "expectedMemberRevision", "expectedRegistryVersionId",
            "expectedSourceDigest", "expectedSampleDigest", "outcome", "originatingClassification", "reason", "evidenceReferenceIds", "correction"]);
        var eventText = Text(body.GetProperty("eventId"));
        if (!Guid.TryParseExact(eventText, "D", out var eventId) || eventId == Guid.Empty || eventId.ToString("D") != eventText)
            throw new JsonException();
        var kind = EnumValue<EvaluationWorkflowCommandKind>(body.GetProperty("kind"));
        var outcomeValue = body.GetProperty("outcome");
        var originValue = body.GetProperty("originatingClassification");
        EvaluationReviewOutcome? outcome = outcomeValue.ValueKind == JsonValueKind.Null ? null : EnumValue<EvaluationReviewOutcome>(outcomeValue);
        EvaluationOriginClassification? origin = originValue.ValueKind == JsonValueKind.Null ? null : EnumValue<EvaluationOriginClassification>(originValue);
        var references = body.GetProperty("evidenceReferenceIds");
        if (references.ValueKind != JsonValueKind.Array || references.GetArrayLength() > 16) throw new JsonException();
        var correctionValue = body.GetProperty("correction");
        EvaluationWorkflowCorrection? correction = null;
        if (correctionValue.ValueKind != JsonValueKind.Null)
        {
            Keys(correctionValue, ["severity", "category", "rootCause", "recommendation"]);
            string? Optional(string name) => correctionValue.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Text(correctionValue.GetProperty(name));
            correction = new(Optional("severity"), Optional("category"), Optional("rootCause"), Optional("recommendation"));
        }
        return new(eventId, Text(body.GetProperty("memberId")), kind,
            Revision(body.GetProperty("expectedAggregateRevision")), Revision(body.GetProperty("expectedMemberRevision")),
            Text(body.GetProperty("expectedRegistryVersionId")), Text(body.GetProperty("expectedSourceDigest")),
            Text(body.GetProperty("expectedSampleDigest")), outcome, origin, Text(body.GetProperty("reason")),
            Array.AsReadOnly(references.EnumerateArray().Select(Text).ToArray()), correction);
    }

    private static void Keys(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Length != expected.Length || names.Distinct(StringComparer.Ordinal).Count() != expected.Length ||
            names.Any(name => !expected.Contains(name, StringComparer.Ordinal))) throw new JsonException();
    }
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new JsonException();
    private static T EnumValue<T>(JsonElement value) where T : struct, Enum
    {
        var text = Text(value);
        return Enum.TryParse<T>(text, false, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == text ? parsed : throw new JsonException();
    }
    private static long Revision(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var revision)
        && revision is >= 0 and <= 9007199254740991 ? revision : throw new JsonException();
    internal static bool TryRevision(string? text, out long revision) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out revision)
        && revision is >= 0 and <= 9007199254740991 && revision.ToString(CultureInfo.InvariantCulture) == text;
    private sealed record Envelope(int SchemaVersion, bool DemoOnly, string? CsrfToken, string? Issue, object? Payload, bool? AlreadyApplied);
    private sealed class BufferedJsonResult(byte[] bytes, int status) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
        }
    }
}
