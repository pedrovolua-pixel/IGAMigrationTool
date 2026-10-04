using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ReportPublication;

namespace ReportPublication.PostgreSql;

public sealed record PostgreSqlPublicationBindingV1(Guid CustomerId, Guid StreamId, Guid WriterBindingReference);
internal enum FixtureInvocationPurposeV1 { Publish, ReadExact }
internal sealed record FixtureInvocationAdmissionV1(PublicationActorV1 Actor, PublicationScopeV1 Scope,
    FixtureInvocationPurposeV1 Purpose, Guid ResourceId, Guid OriginalOperationOrInvocationId,
    DateTimeOffset OriginalAuthorityDeadlineUtc);

internal static class NativeFixtureWireV1
{
    internal static void Binding(PostgreSqlPublicationBindingV1 value)
    {
        if (value is null || value.CustomerId == Guid.Empty || value.StreamId == Guid.Empty
            || value.WriterBindingReference == Guid.Empty) throw Invalid();
    }
    internal static byte[] Scope(PublicationScopeV1 value)
    {
        if (!PublicationValidationV1.Scope(value)) throw Invalid();
        return Encode(w =>
        {
            w.WriteStartObject();
            w.WriteString("assessmentId", value.AssessmentId.ToString("D"));
            w.WriteString("customerId", value.CustomerId.ToString("D"));
            w.WriteString("environmentId", value.EnvironmentId.ToString("D"));
            w.WriteString("projectId", value.ProjectId.ToString("D"));
            w.WriteEndObject();
        });
    }
    internal static byte[] Actor(PublicationActorV1 value)
    {
        if (!PublicationValidationV1.Actor(value)) throw Invalid();
        return Encode(w =>
        {
            w.WriteStartObject();
            w.WriteString("objectId", value.ObjectId.ToString("D"));
            w.WriteString("securityVersion", value.SecurityVersion.ToString(CultureInfo.InvariantCulture));
            w.WriteString("sessionId", value.SessionId.ToString("D"));
            w.WriteString("tenantId", value.TenantId.ToString("D"));
            w.WriteEndObject();
        });
    }
    internal static byte[] Tokens(IEnumerable<string> values)
    {
        var owned = values?.ToArray() ?? throw Invalid();
        if (owned.Any(v => v is null || v.Length is < 1 or > 128 || !v.All(c => c is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-'))
            || owned.Distinct(StringComparer.Ordinal).Count() != owned.Length) throw Invalid();
        return Encode(w =>
        {
            w.WriteStartArray();
            foreach (var item in owned.Order(StringComparer.Ordinal)) w.WriteStringValue(item);
            w.WriteEndArray();
        });
    }
    internal static string Digest(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    internal static DateTimeOffset Clock(string utc7, long ticks)
    {
        var utc = NativeControlReaderV1.Time(utc7);
        if (utc.UtcTicks != ticks) throw new PublicationIntegrityException();
        return utc;
    }
    private static byte[] Encode(Action<Utf8JsonWriter> write)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) write(writer);
        return output.ToArray();
    }
    internal static ArgumentException Invalid() => new("Invalid native publication fixture binding.");
}

internal sealed class FixtureBoundOperationV1
{
    private readonly byte[] originalBytes;
    private FixtureBoundOperationV1(PublicationActorV1 actor, PublicationScopeV1 scope, FixtureInvocationPurposeV1 purpose,
        Guid resourceId, Guid operationOrInvocationId, byte[] bytes)
    {
        Actor = actor; Scope = scope; Purpose = purpose; ResourceId = resourceId;
        OperationOrInvocationId = operationOrInvocationId; originalBytes = bytes.ToArray();
        Digest = NativeFixtureWireV1.Digest(originalBytes);
    }
    internal PublicationActorV1 Actor { get; }
    internal PublicationScopeV1 Scope { get; }
    internal FixtureInvocationPurposeV1 Purpose { get; }
    internal Guid ResourceId { get; }
    internal Guid OperationOrInvocationId { get; }
    internal string Digest { get; }
    internal byte[] CopyBytes() => originalBytes.ToArray();
    internal bool Matches(PublicationActorV1 actor, PublishCommandV1 command) => Purpose == FixtureInvocationPurposeV1.Publish
        && PublicationValidationV1.Command(actor, command)
        && originalBytes.AsSpan().SequenceEqual(NativePublicationCanonicalV1.CommandBytes(actor, command));
    internal bool Matches(PublicationActorV1 actor, ExactReportRequestV1 request) => Purpose == FixtureInvocationPurposeV1.ReadExact
        && PublicationValidationV1.ExactRequest(actor, request)
        && originalBytes.AsSpan().SequenceEqual(NativePublicationCanonicalV1.ReadRequestBytes(actor, request));
    internal bool Matches(FixtureInvocationAdmissionV1 admission) => admission is not null && Actor == admission.Actor
        && Scope == admission.Scope && Purpose == admission.Purpose && ResourceId == admission.ResourceId
        && OperationOrInvocationId == admission.OriginalOperationOrInvocationId
        && admission.OriginalAuthorityDeadlineUtc.Offset == TimeSpan.Zero;
    internal static FixtureBoundOperationV1 Publish(PublicationActorV1 actor, PublishCommandV1 command)
    {
        if (!PublicationValidationV1.Command(actor, command)) throw NativeFixtureWireV1.Invalid();
        return new(actor, command.Scope, FixtureInvocationPurposeV1.Publish, command.RunId, command.OperationId,
            NativePublicationCanonicalV1.CommandBytes(actor, command));
    }
    internal static FixtureBoundOperationV1 Read(PublicationActorV1 actor, ExactReportRequestV1 request)
    {
        if (!PublicationValidationV1.ExactRequest(actor, request)) throw NativeFixtureWireV1.Invalid();
        return new(actor, request.Scope, FixtureInvocationPurposeV1.ReadExact, request.ReportVersionId, request.InvocationId,
            NativePublicationCanonicalV1.ReadRequestBytes(actor, request));
    }
}
