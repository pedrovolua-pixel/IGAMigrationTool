using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IdentitySessions;

// Metadata only. A well-formed subject is not proof of identity, authority, witness or durability.
public sealed record AuthenticationFailureJournalV1(SecurityAuditBindingV1 Binding, Guid EventId,
    Guid OperationId, Guid CorrelationId, DateTimeOffset OccurredAtUtc, SecurityAuditActorKind ActorKind,
    SessionSubject? Subject, SecurityAuditAction Action, SecurityAuditOutcome Outcome, SecurityAuditReason Reason,
    Guid? SessionReference, long? SecurityVersion);

public static class AuthenticationFailureJournalCodec
{
    public const string SchemaVersion = "authentication-failure-journal-v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] Fields = ["action", "actorKind", "applicationClientId", "correlationId", "environmentId",
        "eventId", "occurredAtUtc", "operationId", "outcome", "reason", "schemaVersion", "securityVersion",
        "sessionReference", "streamId", "subject", "writer", "writerBindingReference"];

    // Every representable value is bounded ASCII. Full subjects/session/version exceed null,
    // all GUIDs are 36 chars, time is 28, and the binding permits at most 48 environment chars.
    // This is a closed-representation bound, not an operational journal quota.
    public static int MaximumEncodedLength { get; } = DeriveMaximumEncodedLength();

    public static byte[] Serialize(AuthenticationFailureJournalV1 value, SecurityAuditBindingV1 expectedBinding,
        DateTimeOffset nowUtc)
    {
        Validate(value, expectedBinding, nowUtc);
        return Write(value);
    }

    public static string Digest(AuthenticationFailureJournalV1 value, SecurityAuditBindingV1 expectedBinding,
        DateTimeOffset nowUtc) => Convert.ToHexStringLower(SHA256.HashData(Serialize(value, expectedBinding, nowUtc)));

    public static AuthenticationFailureJournalV1 Parse(ReadOnlyMemory<byte> utf8, SecurityAuditBindingV1 expectedBinding,
        DateTimeOffset nowUtc)
    {
        try
        {
            Require(utf8.Length is > 0 && utf8.Length <= MaximumEncodedLength);
            StrictUtf8.GetCharCount(utf8.Span);
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 2 });
            var root = document.RootElement;
            Closed(root, Fields);
            Require(Text(root, "schemaVersion") == SchemaVersion);
            var binding = new SecurityAuditBindingV1(Id(root, "streamId"), Text(root, "environmentId"),
                Id(root, "writerBindingReference"), ReadSubject(root.GetProperty("writer")), Id(root, "applicationClientId"));
            var subject = root.GetProperty("subject");
            var value = new AuthenticationFailureJournalV1(binding, Id(root, "eventId"), Id(root, "operationId"),
                Id(root, "correlationId"), ReadTime(Text(root, "occurredAtUtc")),
                NamedEnum<SecurityAuditActorKind>(Text(root, "actorKind")),
                subject.ValueKind == JsonValueKind.Null ? null : ReadSubject(subject),
                NamedEnum<SecurityAuditAction>(Text(root, "action")), NamedEnum<SecurityAuditOutcome>(Text(root, "outcome")),
                NamedEnum<SecurityAuditReason>(Text(root, "reason")), OptionalId(root, "sessionReference"),
                Version(root.GetProperty("securityVersion")));
            var canonical = Serialize(value, expectedBinding, nowUtc);
            Require(utf8.Span.SequenceEqual(canonical)); // Reject whitespace, ordering, escapes and alternate spellings.
            return value;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw Denied(); // Do not carry parser messages or untrusted input in an inner exception.
        }
    }

    private static void Validate(AuthenticationFailureJournalV1 value, SecurityAuditBindingV1 expected, DateTimeOffset nowUtc)
    {
        if (value is null) throw Denied();
        ValidateBinding(expected);
        ValidateBinding(value.Binding);
        Require(value.Binding == expected && value.EventId != Guid.Empty && value.OperationId != Guid.Empty
            && value.CorrelationId != Guid.Empty && value.OccurredAtUtc.Offset == TimeSpan.Zero
            && nowUtc.Offset == TimeSpan.Zero && value.OccurredAtUtc <= nowUtc && Enum.IsDefined(value.ActorKind)
            && Enum.IsDefined(value.Reason) && value.SecurityVersion is null or > 0 && value.SessionReference != Guid.Empty);
        Require(value.Action == SecurityAuditAction.AuthenticationDenied && value.Outcome == SecurityAuditOutcome.Denied
            || value.Action == SecurityAuditAction.AuthenticationFailed && value.Outcome == SecurityAuditOutcome.Failed);
        Require((value.ActorKind == SecurityAuditActorKind.Anonymous) == (value.Subject is null));
        if (value.Subject is { } subject) ValidateSubject(subject);
        else Require(value.SessionReference is null && value.SecurityVersion is null);
        Require(value.SessionReference is null || value.Subject is not null && value.SecurityVersion is > 0);
    }

    internal static void ValidateBinding(SecurityAuditBindingV1 binding)
    {
        if (binding is null) throw Denied();
        Require(binding.StreamId != Guid.Empty && binding.WriterBindingReference != Guid.Empty
            && binding.ApplicationClientId != Guid.Empty && binding.EnvironmentId is not null
            && Regex.IsMatch(binding.EnvironmentId, "\\A[a-z][a-z0-9-]{0,47}\\z", RegexOptions.CultureInvariant));
        ValidateSubject(binding.Writer);
    }

    private static void ValidateSubject(SessionSubject subject)
        => Require(subject.TenantId != Guid.Empty && subject.ObjectId != Guid.Empty);

    private static byte[] Write(AuthenticationFailureJournalV1 value)
    {
        var binding = value.Binding;
        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = value.Action.ToString(),
            ["actorKind"] = value.ActorKind.ToString(),
            ["applicationClientId"] = binding.ApplicationClientId.ToString("D"),
            ["correlationId"] = value.CorrelationId.ToString("D"),
            ["environmentId"] = binding.EnvironmentId,
            ["eventId"] = value.EventId.ToString("D"),
            ["occurredAtUtc"] = value.OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
            ["operationId"] = value.OperationId.ToString("D"),
            ["outcome"] = value.Outcome.ToString(),
            ["reason"] = value.Reason.ToString(),
            ["schemaVersion"] = SchemaVersion,
            ["securityVersion"] = value.SecurityVersion?.ToString(CultureInfo.InvariantCulture),
            ["sessionReference"] = value.SessionReference?.ToString("D"),
            ["streamId"] = binding.StreamId.ToString("D"),
            ["subject"] = Subject(value.Subject),
            ["writer"] = Subject(binding.Writer),
            ["writerBindingReference"] = binding.WriterBindingReference.ToString("D")
        };
        return JsonSerializer.SerializeToUtf8Bytes(fields);
    }

    private static object? Subject(SessionSubject? subject) => subject is { } value
        ? new SortedDictionary<string, object?>(StringComparer.Ordinal)
        { ["objectId"] = value.ObjectId.ToString("D"), ["tenantId"] = value.TenantId.ToString("D") } : null;

    private static void Closed(JsonElement root, string[] fields)
        => Require(root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Select(p => p.Name)
            .Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal)));

    private static string Text(JsonElement root, string field)
    {
        var value = root.GetProperty(field);
        Require(value.ValueKind == JsonValueKind.String);
        return value.GetString()!;
    }

    private static Guid Id(JsonElement root, string field)
    {
        var text = Text(root, field);
        Require(Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty && text == value.ToString("D"));
        return value;
    }

    private static Guid? OptionalId(JsonElement root, string field)
        => root.GetProperty(field).ValueKind == JsonValueKind.Null ? null : Id(root, field);

    private static SessionSubject ReadSubject(JsonElement root)
    {
        Closed(root, ["objectId", "tenantId"]);
        return new(Id(root, "tenantId"), Id(root, "objectId"));
    }

    private static T NamedEnum<T>(string text) where T : struct, Enum
    {
        Require(Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(value) && value.ToString() == text);
        return value;
    }

    private static DateTimeOffset ReadTime(string text)
    {
        Require(DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            && value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture) == text);
        return value;
    }

    private static long? Version(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Null) return null;
        Require(root.ValueKind == JsonValueKind.String);
        var text = root.GetString()!;
        Require(long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value > 0 && text == value.ToString(CultureInfo.InvariantCulture));
        return value;
    }

    private static int DeriveMaximumEncodedLength()
    {
        var id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var binding = new SecurityAuditBindingV1(id, new string('a', 48), id, new(id, id), id);
        var action = new[] { SecurityAuditAction.AuthenticationDenied, SecurityAuditAction.AuthenticationFailed }
            .MaxBy(a => a.ToString().Length);
        var outcome = action == SecurityAuditAction.AuthenticationDenied ? SecurityAuditOutcome.Denied : SecurityAuditOutcome.Failed;
        var value = new AuthenticationFailureJournalV1(binding, id, id, id, DateTimeOffset.MaxValue,
            SecurityAuditActorKind.Workload, new(id, id), action, outcome,
            Enum.GetValues<SecurityAuditReason>().MaxBy(r => r.ToString().Length), id, long.MaxValue);
        return Write(value).Length;
    }

    private static void Require(bool condition) { if (!condition) throw Denied(); }
    private static InvalidOperationException Denied() => new("Authentication failure journal denied.");
}

// One server-owned attempt; freeze is neither a successful write nor durable deduplication.
public sealed class AuthenticationFailureAttempt
{
    private readonly object gate = new();
    private readonly SecurityAuditBindingV1 binding;
    private readonly TimeProvider clock;
    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid operationId = Guid.NewGuid();
    private readonly Guid correlationId = Guid.NewGuid();
    private AuthenticationFailureJournalV1? frozen;

    public AuthenticationFailureAttempt(SecurityAuditBindingV1 binding, TimeProvider clock)
    {
        AuthenticationFailureJournalCodec.ValidateBinding(binding);
        ArgumentNullException.ThrowIfNull(clock);
        this.binding = binding;
        this.clock = clock;
    }

    public AuthenticationFailureJournalV1 Observe(SecurityAuditActorKind actorKind, SessionSubject? verifiedSubject,
        SecurityAuditAction action, SecurityAuditOutcome outcome, SecurityAuditReason reason,
        Guid? sessionReference = null, long? securityVersion = null)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            var candidate = new AuthenticationFailureJournalV1(binding, eventId, operationId, correlationId, now,
                actorKind, verifiedSubject, action, outcome, reason, sessionReference, securityVersion);
            _ = AuthenticationFailureJournalCodec.Serialize(candidate, binding, now); // Validate even a late observer before freeze/return.
            return frozen ??= candidate;
        }
    }
}
