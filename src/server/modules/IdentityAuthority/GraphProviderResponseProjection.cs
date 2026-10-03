using System.Globalization;
using System.Text;
using System.Text.Json;
using IdentitySessions;

namespace IdentityAuthority;

// Pure projection only: output is an existing synthetic reader document, never authority
// or evidence of complete pagination. No transport, credential, clock or admission exists here.
public static class GraphProviderResponseProjection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] UserFields = ["id", "accountEnabled", "userType", "externalUserState", "signInSessionsValidFromDateTime"];
    private static readonly string[] RoleFields = ["id", "appRoleId", "principalId", "principalType", "resourceId"];

    public static byte[]? ProjectUser(ReadOnlyMemory<byte> utf8, SessionSubject subject,
        ProviderRoleBindingV1 binding, DateTimeOffset nowUtc)
    {
        try
        {
            ValidateBinding(subject, binding);
            Require(AuthorityCodec.Utc(nowUtc));
            using var document = Parse(utf8);
            var user = document.RootElement;
            Closed(user, UserFields, UserFields, metadata: true);
            Require(GuidField(user, "id") == subject.ObjectId);
            Require(user.GetProperty("accountEnabled").ValueKind is JsonValueKind.True or JsonValueKind.False);
            Require(Text(user, "userType") is "Member" or "Guest");
            var invitation = user.GetProperty("externalUserState");
            Require(invitation.ValueKind == JsonValueKind.Null ||
                invitation.ValueKind == JsonValueKind.String && invitation.GetString() is "Accepted" or "PendingAcceptance");
            var cutoff = CanonicalTime(Text(user, "signInSessionsValidFromDateTime"), nowUtc);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                foreach (var field in UserFields)
                {
                    writer.WritePropertyName(field);
                    if (field == "signInSessionsValidFromDateTime") writer.WriteStringValue(cutoff);
                    else user.GetProperty(field).WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return output.ToArray();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    public static byte[]? ProjectDirectRolesPage(ReadOnlyMemory<byte> utf8, SessionSubject subject,
        ProviderRoleBindingV1 binding)
    {
        try
        {
            ValidateBinding(subject, binding);
            using var document = Parse(utf8);
            var page = document.RootElement;
            Closed(page, ["value", "@odata.nextLink"], ["value"], metadata: true);
            var rows = page.GetProperty("value");
            Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() <= 4);
            var assignments = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<Guid>();
            foreach (var row in rows.EnumerateArray())
            {
                Closed(row, RoleFields, RoleFields);
                var assignment = Text(row, "id");
                var role = GuidField(row, "appRoleId");
                Require(assignment.Length is > 0 and <= 256 && assignments.Add(assignment)
                    && Text(row, "principalType") == "User" && GuidField(row, "principalId") == subject.ObjectId
                    && GuidField(row, "resourceId") == binding.ResourceServicePrincipalId
                    && binding.AppRoles.Any(r => r.Enabled && r.AppRoleId == role) && roles.Add(role));
            }
            if (page.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind != JsonValueKind.Null)
                Require(next.ValueKind == JsonValueKind.String && ValidContinuation(next.GetString()!, subject, binding));
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                rows.WriteTo(writer);
                if (page.TryGetProperty("@odata.nextLink", out next))
                {
                    writer.WritePropertyName("@odata.nextLink");
                    next.WriteTo(writer); // Preserve the decoded string exactly; do not rebuild the URL.
                }
                writer.WriteEndObject();
            }
            return output.ToArray();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static void ValidateBinding(SessionSubject subject, ProviderRoleBindingV1 binding)
    {
        AuthorityCodec.Validate(binding);
        Require(AuthorityCodec.Subject(subject) && subject.TenantId == binding.ResourceTenantId);
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> utf8)
    {
        Require(utf8.Length is > 0 and <= 65536);
        StrictUtf8.GetCharCount(utf8.Span);
        var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 12 });
        try { AuthorityCodec.UniqueProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void Closed(JsonElement root, string[] fields, string[] required, bool metadata = false)
    {
        Require(root.ValueKind == JsonValueKind.Object);
        foreach (var property in root.EnumerateObject())
        {
            if (metadata && property.Name == "@odata.context")
                Require(property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is not null);
            else Require(fields.Contains(property.Name, StringComparer.Ordinal));
        }
        Require(required.All(field => root.TryGetProperty(field, out _)));
    }

    private static string Text(JsonElement root, string field)
    {
        var value = root.GetProperty(field);
        Require(value.ValueKind == JsonValueKind.String);
        return value.GetString()!;
    }

    private static Guid GuidField(JsonElement root, string field)
    {
        var text = Text(root, field);
        Require(Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty && text == value.ToString("D"));
        return value;
    }

    private static string CanonicalTime(string text, DateTimeOffset nowUtc)
    {
        Require(text.Length == 20 || text.Length is >= 22 and <= 28);
        Require(text[10] == 'T' && text[^1] == 'Z');
        if (text.Length != 20)
        {
            Require(text[19] == '.');
            foreach (var c in text.AsSpan(20, text.Length - 21)) Require(c is >= '0' and <= '9');
        }
        var canonical = text.Length == 20 ? text[..^1] + ".0000000Z" : text[..^1].PadRight(27, '0') + "Z";
        Require(DateTimeOffset.TryParseExact(canonical, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var cutoff) && cutoff <= nowUtc);
        return canonical;
    }

    // Preserve the v1 reader query boundary, checking the original path before Uri normalization.
    // Stateful page cycles/duplicates/16-page completeness remain the reader's responsibility.
    private static bool ValidContinuation(string text, SessionSubject subject, ProviderRoleBindingV1 binding)
    {
        if (text.Length > 8192 || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "graph.microsoft.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != $"/v1.0/servicePrincipals/{binding.ResourceServicePrincipalId:D}/appRoleAssignedTo"
            || uri.AbsolutePath.Contains('%')) return false;
        var pathStart = text.IndexOf('/', text.IndexOf("://", StringComparison.Ordinal) + 3);
        if (pathStart < 0) return false;
        var queryStart = text.IndexOf('?', pathStart);
        if (queryStart < 0 || text[pathStart..queryStart] != uri.AbsolutePath) return false;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in uri.Query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 || !parameters.TryAdd(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1]))) return false;
        }
        return parameters.Count == 3 && parameters.TryGetValue("$filter", out var filter) && filter == $"principalId eq {subject.ObjectId:D}"
            && parameters.TryGetValue("$select", out var select) && select == "id,appRoleId,principalId,principalType,resourceId"
            && parameters.TryGetValue("$skiptoken", out var token) && token.Length is > 0 and <= 4096;
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Provider projection denied.");
    }
}
