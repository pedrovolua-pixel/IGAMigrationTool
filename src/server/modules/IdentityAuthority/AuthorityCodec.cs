using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPolicy;
using IdentitySessions;

namespace IdentityAuthority;

public static class AuthorityCodec
{
    public static readonly Guid ConsumerTenant = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 24
        };
        options.Converters.Add(new StrictEnumConverterFactory());
        options.Converters.Add(new UtcConverter());
        return options;
    }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Parse<T>(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length is 0 or > 65536) throw new InvalidOperationException("Closed authority input exceeds bounds.");
        using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 24 });
        UniqueProperties(document.RootElement);
        CanonicalIds(document.RootElement);
        var value = JsonSerializer.Deserialize<T>(utf8, Options) ?? throw new InvalidOperationException("Authority input absent.");
        Validate(value);
        return value;
    }
    private static void CanonicalIds(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) CanonicalIds(property.Value);
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CanonicalIds(item);
        else if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var id))
            Require(element.GetString() == id.ToString("D"));
    }
    internal static void UniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name)); UniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) UniqueProperties(item);
    }
    public static string PayloadDigest(AuthorityCommandV1 command)
    {
        var source = Serialize(command with { Decision = command.Decision with { PayloadSha256 = new string('0', 64) } });
        using var document = JsonDocument.Parse(source);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Canonical(writer, document.RootElement);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    private static void Canonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(p.Name); Canonical(writer, p.Value); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var v in element.EnumerateArray()) Canonical(writer, v); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    public static void Validate<T>(T value)
    {
        switch (value)
        {
            case SubjectEnrollmentV1 e:
                Require(e.SchemaVersion == "subject-enrollment-v1" && Subject(e.Subject) && e.Revision > 0 && Enum.IsDefined(e.Lifecycle)
                    && Enum.IsDefined(e.Origin) && e.HomeTenantId != Guid.Empty && e.HomeTenantId != ConsumerTenant
                    && (e.Origin != OrganizationalOrigin.InternalOrganizational || e.HomeTenantId == e.Subject.TenantId)
                    && Utc(e.EnrolledAtUtc) && Attribution(e.Attribution)); break;
            case HumanAssignmentV1 a:
                Require(a.SchemaVersion == "human-assignment-v1" && a.AssignmentId != Guid.Empty && Subject(a.Subject)
                    && Scope(a.Scope) && Enum.IsDefined(a.Role) && a.Revision > 0 && Utc(a.StartsAtUtc) && Utc(a.ExpiresAtUtc)
                    && a.ExpiresAtUtc > a.StartsAtUtc && !a.EvidenceCategories.IsDefaultOrEmpty
                    && a.EvidenceCategories.All(Key) && a.EvidenceCategories.Distinct(StringComparer.Ordinal).Count() == a.EvidenceCategories.Length
                    && !a.Conditions.IsDefault && a.Conditions.All(c => Enum.IsDefined(c)) && a.Conditions.Distinct().Count() == a.Conditions.Length
                    && Attribution(a.Attribution)); break;
            case GuestLifecycleV1 g:
                Require(g.SchemaVersion == "guest-lifecycle-v1" && Subject(g.Subject) && Subject(g.Sponsor) && g.Subject != g.Sponsor
                    && Utc(g.AssignedAtUtc) && Utc(g.ExpiresAtUtc) && Utc(g.LastReviewedAtUtc) && g.ExpiresAtUtc > g.AssignedAtUtc
                    && g.ExpiresAtUtc - g.AssignedAtUtc <= TimeSpan.FromDays(90) && g.LastReviewedAtUtc >= g.AssignedAtUtc
                    && g.EngagementReference != Guid.Empty && g.EngagementRevision > 0 && Attribution(g.Attribution)); break;
            case ProviderRoleBindingV1 b:
                Require(b.SchemaVersion == "provider-role-binding-v1" && b.ResourceTenantId != Guid.Empty && b.ResourceTenantId != ConsumerTenant
                    && b.ClientId != Guid.Empty && b.ResourceServicePrincipalId != Guid.Empty && b.ManifestRevision > 0
                    && Sha(b.ManifestSha256) && b.ApprovedDecisionId != Guid.Empty && !b.AppRoles.IsDefault && b.AppRoles.Length == 4
                    && b.AppRoles.All(r => r is not null && r.AppRoleId != Guid.Empty && r.Enabled && Enum.IsDefined(r.Role))
                    && b.AppRoles.Select(r => r.AppRoleId).Distinct().Count() == 4 && b.AppRoles.Select(r => r.Role).Distinct().Count() == 4); break;
            case ProviderObservationV1 p:
                Require(p.SchemaVersion == "provider-observation-v1" && Subject(p.Subject) && p.ClientId != Guid.Empty
                    && p.ResourceServicePrincipalId != Guid.Empty && p.Sequence > 0 && Utc(p.StartedAtUtc) && Utc(p.CompletedAtUtc)
                    && p.StartedAtUtc <= p.CompletedAtUtc && p.EnrollmentRevision > 0 && p.SecurityVersion > 0
                    && p.ReturnedSubjectId == p.Subject.ObjectId && Enum.IsDefined(p.UserType)
                    && (p.InvitationState is null || Enum.IsDefined(p.InvitationState.Value)) && Utc(p.ResourceCutoffUtc)
                    && !p.AppRoleIds.IsDefault && p.AppRoleIds.All(x => x != Guid.Empty) && p.AppRoleIds.Distinct().Count() == p.AppRoleIds.Length
                    && p.AppRoleIds.SequenceEqual(p.AppRoleIds.Order()) && p.Complete && p.CorrelationId != Guid.Empty); break;
            case HomeStatusEvidenceV1 h:
                Require(Subject(h.Subject) && h.HomeTenantId != Guid.Empty && h.HomeTenantId != ConsumerTenant
                    && h.EnrollmentRevision > 0 && h.SecurityVersion > 0 && Utc(h.CheckedAtUtc) && Utc(h.CutoffUtc)); break;
            case AuthorityDecisionV1 d:
                Require(d.SchemaVersion == "authority-decision-v1" && d.CommandId != Guid.Empty && d.ExpectedRevision >= 0
                    && Subject(d.Subject) && (d.Scope is null || Scope(d.Scope)) && Enum.IsDefined(d.Operation)
                    && d.DecisionReference != Guid.Empty && Enum.IsDefined(d.Reason) && Subject(d.Administrator) && Sha(d.PayloadSha256)); break;
            case AuthorityCommandV1 c:
                Require(c.SchemaVersion == "authority-command-v1");
                if (c.Decision is null) throw new InvalidOperationException("Authority decision absent.");
                Validate(c.Decision);
                Require(c.Decision.PayloadSha256 == PayloadDigest(c));
                var operation = c.Decision!.Operation;
                Require((c.Enrollment is not null) == (operation is AuthorityOperation.EnrollPending or AuthorityOperation.ActivateEnrollment));
                Require((c.Assignment is not null) == (operation is AuthorityOperation.SetAssignment or AuthorityOperation.RevokeAssignment));
                Require((c.Guest is not null) == (operation == AuthorityOperation.ApproveExternalLifecycle));
                if (c.Enrollment is { } ce)
                {
                    Validate(ce); Require(ce.Subject == c.Decision.Subject && ce.Revision == c.Decision.ExpectedRevision + 1
                    && ce.Lifecycle == (operation == AuthorityOperation.EnrollPending ? EnrollmentLifecycle.Pending : EnrollmentLifecycle.Active));
                    if (operation == AuthorityOperation.EnrollPending) Match(ce.Attribution, c.Decision);
                }
                if (c.Assignment is { } ca)
                {
                    Validate(ca); Require(ca.Subject == c.Decision.Subject && ca.Scope == c.Decision.Scope
                    && (operation != AuthorityOperation.RevokeAssignment || !ca.Active)); Match(ca.Attribution, c.Decision);
                }
                if (c.Guest is { } cg) { Validate(cg); Require(cg.Subject == c.Decision.Subject && !cg.SponsorOrEngagementChanged); Match(cg.Attribution, c.Decision); }
                Require((c.Decision.Scope is not null) == (c.Assignment is not null)); break;
            default: throw new InvalidOperationException("Unsupported closed authority schema.");
        }
    }
    private static void Match(DecisionAttributionV1 a, AuthorityDecisionV1 d)
        => Require(a.ApprovedDecisionId == d.DecisionReference && a.ApprovedBySubject == d.Administrator);
    internal static bool Attribution(DecisionAttributionV1? a) => a is not null && a.ApprovedDecisionId != Guid.Empty && Subject(a.ApprovedBySubject) && Utc(a.ApprovedAtUtc);
    internal static bool Utc(DateTimeOffset t) => t.Offset == TimeSpan.Zero;
    internal static bool Subject(SessionSubject s) => s.TenantId != Guid.Empty && s.TenantId != ConsumerTenant && s.ObjectId != Guid.Empty;
    internal static bool Scope(HumanScope? s) => s is not null && s.CustomerId != Guid.Empty && s.ProjectId != Guid.Empty && s.EnvironmentId != Guid.Empty && s.AssessmentId != Guid.Empty;
    internal static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    internal static bool Sha(string s) => s is not null && s.Length == 64 && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Closed authority validation denied."); }
    private sealed class UtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !DateTimeOffset.TryParseExact(reader.GetString(), "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
                throw new JsonException("Canonical UTC instant required.");
            return value;
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        { if (!Utc(value)) throw new JsonException("UTC required."); writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)); }
    }
    private sealed class StrictEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;
    }
    private sealed class StrictEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !Enum.GetNames<T>().Contains(reader.GetString(), StringComparer.Ordinal))
                throw new JsonException("Exact enum literal required.");
            return Enum.Parse<T>(reader.GetString()!, false);
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        { if (!Enum.IsDefined(value)) throw new JsonException("Known enum required."); writer.WriteStringValue(value.ToString()); }
    }
}
