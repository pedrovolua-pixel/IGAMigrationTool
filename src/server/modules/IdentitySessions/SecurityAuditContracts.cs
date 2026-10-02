using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdentitySessions;

public enum SecurityAuditActorKind { Human, Workload, Anonymous }
public enum SecurityAuditAction { SessionIssued, AuthenticationDenied, AuthenticationFailed, SessionRevoked, SessionRotated, SubjectRevoked, AuthorityChanged }
public enum SecurityAuditOutcome { Succeeded, Denied, Failed }
public enum SecurityAuditReason { None, InvalidProtocol, AuthorityDenied, ProviderUnavailable, AuditUnavailable, Conflict }

public sealed record SecurityAuditBindingV1(Guid StreamId, string EnvironmentId, Guid WriterBindingReference,
    SessionSubject Writer, Guid ApplicationClientId);

public sealed record SecurityAuditEventV1(Guid EventId, Guid OperationId, DateTimeOffset EventAtUtc,
    SecurityAuditActorKind ActorKind, SessionSubject? Actor, Guid CorrelationId, SecurityAuditAction Action,
    SecurityAuditOutcome Outcome, SecurityAuditReason Reason, SessionSubject? Target, Guid? SessionReference,
    Guid? PreviousSessionReference, long? SecurityVersion, Guid? CustomerId = null, Guid? ProjectId = null);

public sealed record CommittedSecurityAuditEventV1(Guid EventId, Guid StreamId, long Sequence, string EventSha256);

public sealed record OperationReceiptRequestV1(Guid OperationId, SessionSubject Issuer, SecurityAuditActorKind ActorKind,
    SessionSubject? Actor, SecurityAuditAction OperationKind, SessionSubject Target, string CommandSha256,
    Guid? CustomerId = null, Guid? ProjectId = null, Guid? EnvironmentId = null, Guid? AssessmentId = null,
    Guid? OldSessionReference = null)
{ public string SchemaVersion => "operation-receipt-request-v1"; }

public sealed record OperationReceiptV1(OperationReceiptRequestV1 Request, SecurityAuditOutcome Outcome,
    DateTimeOffset CommittedAtUtc, long? ResultingRevision, long SecurityVersion, Guid? NewSessionReference,
    IReadOnlyList<Guid> EventIds)
{ public string SchemaVersion => "operation-receipt-v1"; }

public sealed record AuditCheckpointV1(Guid StreamId, string EnvironmentId, Guid WriterBindingReference,
    long Sequence, string EventSha256, DateTimeOffset CapturedAtUtc, Guid WitnessReference);

// RFC8785 subset: closed ASCII field names sorted ordinally, no JSON numbers,
// no untrusted text, UTF8 without insignificant whitespace. Counters use strings.
public static class SecurityAuditCanonical
{
    public const string EmptyDigest = "0000000000000000000000000000000000000000000000000000000000000000";
    private static readonly JsonSerializerOptions Strict = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new UtcTimestampConverter() }
    };

    public static string Event(SecurityAuditBindingV1 binding, SecurityAuditEventV1 value, long sequence, string previous)
    {
        Validate(binding); Validate(value);
        if (sequence < 1 || !Digest(previous)) throw new InvalidOperationException("Invalid audit ordering.");
        return Write(new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = value.Action.ToString(),
            ["actor"] = Subject(value.Actor),
            ["actorKind"] = value.ActorKind.ToString(),
            ["applicationClientId"] = Id(binding.ApplicationClientId),
            ["authenticationEvidence"] = "None",
            ["correlationId"] = Id(value.CorrelationId),
            ["customerId"] = OptionalId(value.CustomerId),
            ["environmentId"] = binding.EnvironmentId,
            ["eventAtUtc"] = Time(value.EventAtUtc),
            ["eventId"] = Id(value.EventId),
            ["operationId"] = Id(value.OperationId),
            ["outcome"] = value.Outcome.ToString(),
            ["previousEventSha256"] = previous,
            ["previousSessionReference"] = OptionalId(value.PreviousSessionReference),
            ["projectId"] = OptionalId(value.ProjectId),
            ["reason"] = value.Reason.ToString(),
            ["schemaVersion"] = "security-audit-session-v1",
            ["scope"] = value.CustomerId is null ? "Platform" : "CustomerProject",
            ["securityVersion"] = Number(value.SecurityVersion),
            ["sequence"] = Number(sequence),
            ["sessionReference"] = OptionalId(value.SessionReference),
            ["streamId"] = Id(binding.StreamId),
            ["target"] = Subject(value.Target),
            ["writer"] = Subject(binding.Writer),
            ["writerBindingReference"] = Id(binding.WriterBindingReference)
        });
    }

    public static string Request(OperationReceiptRequestV1 value)
    {
        Validate(value);
        return Write(new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["actor"] = Subject(value.Actor),
            ["actorKind"] = value.ActorKind.ToString(),
            ["assessmentId"] = OptionalId(value.AssessmentId),
            ["commandSha256"] = value.CommandSha256,
            ["customerId"] = OptionalId(value.CustomerId),
            ["environmentId"] = OptionalId(value.EnvironmentId),
            ["issuer"] = Subject(value.Issuer),
            ["oldSessionReference"] = OptionalId(value.OldSessionReference),
            ["operationId"] = Id(value.OperationId),
            ["operationKind"] = value.OperationKind.ToString(),
            ["projectId"] = OptionalId(value.ProjectId),
            ["schemaVersion"] = "operation-receipt-request-v1",
            ["target"] = Subject(value.Target)
        });
    }

    public static string Receipt(OperationReceiptV1 value)
    {
        Validate(value.Request);
        if (!Enum.IsDefined(value.Outcome) || value.SecurityVersion < 1 || value.ResultingRevision is <= 0 ||
            value.NewSessionReference == Guid.Empty || value.EventIds.Count == 0 || value.EventIds.Any(i => i == Guid.Empty) ||
            value.EventIds.Distinct().Count() != value.EventIds.Count) throw new InvalidOperationException("Invalid receipt.");
        // A fixed DTO JSON representation is only used for internal reconstruction;
        // the independently canonical request is the identity/idempotency boundary.
        _ = Time(value.CommittedAtUtc);
        var issuing = value.Request.OperationKind is SecurityAuditAction.SessionIssued or SecurityAuditAction.SessionRotated;
        if (issuing && value.NewSessionReference is null || !issuing && value.NewSessionReference is not null ||
            value.Request.OperationKind == SecurityAuditAction.SessionRotated && value.Request.OldSessionReference is null ||
            value.Request.OperationKind == SecurityAuditAction.SessionRevoked && value.Request.OldSessionReference is null ||
            value.Request.OperationKind is SecurityAuditAction.SessionIssued or SecurityAuditAction.SessionRevoked or SecurityAuditAction.SessionRotated or SecurityAuditAction.SubjectRevoked or SecurityAuditAction.AuthorityChanged && value.Outcome != SecurityAuditOutcome.Succeeded)
            throw new InvalidOperationException("Invalid committed receipt action.");
        var element = JsonSerializer.SerializeToElement(value, Strict);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteElement(writer, element);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static OperationReceiptV1 ReadReceipt(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        RejectDuplicates(parsed.RootElement);
        RequireFields(parsed.RootElement, ["schemaVersion", "request", "outcome", "committedAtUtc", "resultingRevision", "securityVersion", "newSessionReference", "eventIds"]);
        if (parsed.RootElement.GetProperty("schemaVersion").GetString() != "operation-receipt-v1" ||
            parsed.RootElement.GetProperty("securityVersion").ValueKind != JsonValueKind.String ||
            parsed.RootElement.GetProperty("resultingRevision").ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
            !System.Text.RegularExpressions.Regex.IsMatch(parsed.RootElement.GetProperty("securityVersion").GetString()!, "^[1-9][0-9]*$") ||
            parsed.RootElement.GetProperty("resultingRevision") is { ValueKind: JsonValueKind.String } revision && !System.Text.RegularExpressions.Regex.IsMatch(revision.GetString()!, "^[1-9][0-9]*$"))
            throw new InvalidOperationException("Invalid closed receipt version/counter.");
        var request = parsed.RootElement.GetProperty("request");
        RequireFields(request, ["schemaVersion", "operationId", "issuer", "actorKind", "actor", "operationKind", "target", "commandSha256", "customerId", "projectId", "environmentId", "assessmentId", "oldSessionReference"]);
        if (request.GetProperty("schemaVersion").GetString() != "operation-receipt-request-v1") throw new InvalidOperationException("Invalid receipt request version.");
        var value = JsonSerializer.Deserialize<OperationReceiptV1>(json, Strict) ?? throw new InvalidOperationException("Missing receipt.");
        if (Receipt(value) != json) throw new InvalidOperationException("Noncanonical closed receipt.");
        return value;
    }

    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static SecurityAuditEventV1 ReadEvent(SecurityAuditBindingV1 binding, string canonical, long sequence, string previous)
    {
        using var document = JsonDocument.Parse(canonical); var j = document.RootElement;
        RejectDuplicates(j);
        RequireFields(j, ["action", "actor", "actorKind", "applicationClientId", "authenticationEvidence", "correlationId", "customerId", "environmentId", "eventAtUtc", "eventId", "operationId", "outcome", "previousEventSha256", "previousSessionReference", "projectId", "reason", "schemaVersion", "scope", "securityVersion", "sequence", "sessionReference", "streamId", "target", "writer", "writerBindingReference"]);
        var evt = new SecurityAuditEventV1(j.GetProperty("eventId").GetGuid(), j.GetProperty("operationId").GetGuid(),
            JsonSerializer.Deserialize<DateTimeOffset>(j.GetProperty("eventAtUtc"), Strict),
            Enum.Parse<SecurityAuditActorKind>(j.GetProperty("actorKind").GetString()!), ReadSubject(j.GetProperty("actor")),
            j.GetProperty("correlationId").GetGuid(), Enum.Parse<SecurityAuditAction>(j.GetProperty("action").GetString()!),
            Enum.Parse<SecurityAuditOutcome>(j.GetProperty("outcome").GetString()!), Enum.Parse<SecurityAuditReason>(j.GetProperty("reason").GetString()!),
            ReadSubject(j.GetProperty("target")), ReadId(j.GetProperty("sessionReference")), ReadId(j.GetProperty("previousSessionReference")),
            j.GetProperty("securityVersion").ValueKind == JsonValueKind.Null ? null : long.Parse(j.GetProperty("securityVersion").GetString()!, CultureInfo.InvariantCulture),
            ReadId(j.GetProperty("customerId")), ReadId(j.GetProperty("projectId")));
        if (Event(binding, evt, sequence, previous) != canonical) throw new InvalidOperationException("Noncanonical or inconsistent closed audit event.");
        return evt;
    }
    private static Guid? ReadId(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();
    private static SessionSubject? ReadSubject(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        RequireFields(value, ["objectId", "tenantId"]);
        return new(value.GetProperty("tenantId").GetGuid(), value.GetProperty("objectId").GetGuid());
    }
    public static bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static void Validate(SecurityAuditBindingV1 value)
    {
        if (value.StreamId == Guid.Empty || value.WriterBindingReference == Guid.Empty || value.ApplicationClientId == Guid.Empty ||
            !System.Text.RegularExpressions.Regex.IsMatch(value.EnvironmentId, "^[a-z][a-z0-9-]{0,47}$")) throw new InvalidOperationException("Invalid trusted audit binding.");
        ValidateSubject(value.Writer);
    }
    public static void Validate(SecurityAuditEventV1 value)
    {
        if (value.EventId == Guid.Empty || value.OperationId == Guid.Empty || value.CorrelationId == Guid.Empty ||
            !Enum.IsDefined(value.Action) || !Enum.IsDefined(value.Outcome) || !Enum.IsDefined(value.Reason) || value.SecurityVersion is <= 0 ||
            value.SessionReference == Guid.Empty || value.PreviousSessionReference == Guid.Empty || value.CustomerId == Guid.Empty || value.ProjectId == Guid.Empty ||
            (value.CustomerId is null) != (value.ProjectId is null)) throw new InvalidOperationException("Invalid closed audit event.");
        ValidateActor(value.ActorKind, value.Actor); if (value.Target is { } target) ValidateSubject(target);
        _ = Time(value.EventAtUtc);
        var success = value.Action is SecurityAuditAction.SessionIssued or SecurityAuditAction.SessionRevoked or SecurityAuditAction.SessionRotated or SecurityAuditAction.SubjectRevoked or SecurityAuditAction.AuthorityChanged;
        if (success && (value.Target is null || value.SecurityVersion is null || value.Outcome != SecurityAuditOutcome.Succeeded || value.Reason != SecurityAuditReason.None) ||
            value.Action == SecurityAuditAction.AuthenticationDenied && value.Outcome != SecurityAuditOutcome.Denied ||
            value.Action == SecurityAuditAction.AuthenticationFailed && value.Outcome != SecurityAuditOutcome.Failed ||
            value.Action is SecurityAuditAction.SessionIssued or SecurityAuditAction.SessionRevoked or SecurityAuditAction.SessionRotated && value.SessionReference is null ||
            value.Action == SecurityAuditAction.SessionRotated && value.PreviousSessionReference is null ||
            value.Action != SecurityAuditAction.SessionRotated && value.PreviousSessionReference is not null ||
            value.Action is not SecurityAuditAction.AuthorityChanged && value.CustomerId is not null)
            throw new InvalidOperationException("Invalid action/event combination.");
    }
    public static void Validate(OperationReceiptRequestV1 value)
    {
        if (value.OperationId == Guid.Empty || !Enum.IsDefined(value.OperationKind) || !Digest(value.CommandSha256) ||
            value.CustomerId == Guid.Empty || value.ProjectId == Guid.Empty || value.EnvironmentId == Guid.Empty ||
            value.AssessmentId == Guid.Empty || value.OldSessionReference == Guid.Empty) throw new InvalidOperationException("Invalid receipt request.");
        ValidateSubject(value.Issuer); ValidateSubject(value.Target); ValidateActor(value.ActorKind, value.Actor);
        if ((value.CustomerId is null) != (value.ProjectId is null) || (value.EnvironmentId is null) != (value.AssessmentId is null) ||
            value.EnvironmentId is not null && value.CustomerId is null) throw new InvalidOperationException("Invalid exact receipt scope.");
    }
    private static void ValidateActor(SecurityAuditActorKind kind, SessionSubject? subject)
    {
        if (!Enum.IsDefined(kind) || (kind == SecurityAuditActorKind.Anonymous) != (subject is null)) throw new InvalidOperationException("Invalid verified actor.");
        if (subject is { } value) ValidateSubject(value);
    }
    private static void ValidateSubject(SessionSubject subject)
    { if (subject.TenantId == Guid.Empty || subject.ObjectId == Guid.Empty) throw new InvalidOperationException("Empty immutable subject."); }
    private static object? Subject(SessionSubject? value) => value is { } s ? new SortedDictionary<string, object?>(StringComparer.Ordinal)
    { ["objectId"] = Id(s.ObjectId), ["tenantId"] = Id(s.TenantId) } : null;
    private static string Id(Guid value) => value.ToString("D");
    private static string? OptionalId(Guid? value) => value?.ToString("D");
    private static string? Number(long? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static string Time(DateTimeOffset value)
    { if (value.Offset != TimeSpan.Zero) throw new InvalidOperationException("UTC required."); return value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture); }
    private static string Write(SortedDictionary<string, object?> value)
    {
        using var buffer = new MemoryStream(); using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        { JsonSerializer.Serialize(writer, value); }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        { writer.WriteStartObject(); foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteElement(writer, property.Value); } writer.WriteEndObject(); }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteElement(writer, item); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var p in element.EnumerateObject()) { if (!names.Add(p.Name)) throw new InvalidOperationException("Duplicate receipt field."); RejectDuplicates(p.Value); } }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicates(value);
    }
    private static void RequireFields(JsonElement element, string[] fields)
    { if (element.ValueKind != JsonValueKind.Object || !element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal))) throw new InvalidOperationException("Incomplete closed receipt."); }
    private sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            DateTimeOffset.TryParseExact(reader.GetString(), "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value : throw new JsonException("Exact UTC timestamp required.");
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(Time(value));
    }
}
