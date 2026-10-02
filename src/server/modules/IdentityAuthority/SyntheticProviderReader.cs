using System.Collections.Immutable;
using System.Text.Json;
using IdentitySessions;

namespace IdentityAuthority;

// Pure synthetic seam. This type has no HttpClient, credential or provider SDK.
public sealed class SyntheticProviderReader(ISyntheticProviderSource source, ProviderRoleBindingV1 binding,
    TimeProvider clock, IHomeStatusEvidenceSource? homeSource = null)
{
    public async ValueTask<ProviderReadResultV1?> ReadAsync(SubjectEnrollmentV1 enrollment, long securityVersion,
        long sequence, CancellationToken cancellationToken = default)
    {
        try
        {
            AuthorityCodec.Validate(enrollment); AuthorityCodec.Validate(binding);
            if (enrollment.Lifecycle != EnrollmentLifecycle.Active || enrollment.Subject.TenantId != binding.ResourceTenantId || securityVersion < 1 || sequence < 1) return null;
            var start = clock.GetUtcNow();
            var userBytes = await source.ReadUserAsync(enrollment.Subject, cancellationToken);
            if (userBytes is null || !Fresh(start, clock.GetUtcNow())) return null;
            using var user = Parse(userBytes.Value, ["id", "accountEnabled", "userType", "externalUserState", "signInSessionsValidFromDateTime"]);
            var u = user.RootElement;
            var id = GuidField(u, "id");
            if (id != enrollment.Subject.ObjectId || u.GetProperty("accountEnabled").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
            var enabled = u.GetProperty("accountEnabled").GetBoolean();
            if (!Enum.TryParse<ProviderUserType>(Text(u, "userType"), false, out var type) || !Enum.IsDefined(type) || type.ToString() != Text(u, "userType")) return null;
            var invitationValue = u.GetProperty("externalUserState");
            InvitationState? invitation = null;
            if (invitationValue.ValueKind != JsonValueKind.Null)
            {
                if (!Enum.TryParse<InvitationState>(Text(u, "externalUserState"), false, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != Text(u, "externalUserState")) return null;
                invitation = parsed;
            }
            var cutoffJson = JsonSerializer.Serialize(Text(u, "signInSessionsValidFromDateTime"));
            // Parse through the same strict canonical UTC codec as persisted records.
            var cutoff = AuthorityProviderTime.Parse(cutoffJson);
            if (cutoff > clock.GetUtcNow()) return null;
            var roles = ImmutableArray.CreateBuilder<Guid>();
            var assignments = new HashSet<string>(StringComparer.Ordinal);
            var query = RoleQuery(enrollment.Subject);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var pageNumber = 0; ; pageNumber++)
            {
                if (pageNumber >= 16 || !visited.Add(query.AbsoluteUri)) return null;
                var bytes = await source.ReadDirectRolesPageAsync(query, cancellationToken);
                if (bytes is null || !Fresh(start, clock.GetUtcNow())) return null;
                using var page = Parse(bytes.Value, ["value", "@odata.nextLink"], ["value"]);
                var rows = page.RootElement.GetProperty("value");
                if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 4) return null;
                foreach (var row in rows.EnumerateArray())
                {
                    Closed(row, ["id", "appRoleId", "principalId", "principalType", "resourceId"]);
                    var assignment = Text(row, "id");
                    var role = GuidField(row, "appRoleId");
                    if (assignment.Length is 0 or > 256 || !assignments.Add(assignment)
                        || Text(row, "principalType") != "User" || GuidField(row, "principalId") != enrollment.Subject.ObjectId
                        || GuidField(row, "resourceId") != binding.ResourceServicePrincipalId
                        || !binding.AppRoles.Any(r => r.Enabled && r.AppRoleId == role) || roles.Contains(role)) return null;
                    roles.Add(role);
                }
                if (!page.RootElement.TryGetProperty("@odata.nextLink", out var next) || next.ValueKind == JsonValueKind.Null) break;
                if (next.ValueKind != JsonValueKind.String || !ValidContinuation(next.GetString()!, enrollment.Subject, out var nextUri)) return null;
                query = nextUri!;
            }
            HomeStatusEvidenceV1? home = null;
            if (enrollment.Origin == OrganizationalOrigin.ExternalOrganizational)
            {
                if (invitation != InvitationState.Accepted || homeSource is null) return null;
                home = await homeSource.ReadAsync(enrollment, securityVersion, cancellationToken);
                if (!ValidHome(home, enrollment, securityVersion, clock.GetUtcNow())) return null;
            }
            var complete = clock.GetUtcNow();
            if (!Fresh(start, complete) || cutoff > complete) return null;
            var observation = new ProviderObservationV1("provider-observation-v1", enrollment.Subject, binding.ClientId,
                binding.ResourceServicePrincipalId, sequence, start, complete, enrollment.Revision, securityVersion,
                id, enabled, type, invitation, cutoff, roles.Order().ToImmutableArray(), true, Guid.NewGuid());
            AuthorityCodec.Validate(observation);
            return new(observation, home);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }
    public Uri RoleQuery(SessionSubject subject) => new($"https://graph.microsoft.com/v1.0/servicePrincipals/{binding.ResourceServicePrincipalId:D}/appRoleAssignedTo?$filter="
        + Uri.EscapeDataString($"principalId eq {subject.ObjectId:D}") + "&$select=id,appRoleId,principalId,principalType,resourceId");
    public bool ValidContinuation(string value, SessionSubject subject, out Uri? result)
    {
        result = null;
        if (value.Length > 8192 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "graph.microsoft.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != RoleQuery(subject).AbsolutePath || uri.AbsoluteUri.Contains('%') && uri.AbsolutePath.Contains('%')) return false;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in uri.Query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 || !parameters.TryAdd(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1]))) return false;
        }
        if (parameters.Count != 3 || !parameters.TryGetValue("$filter", out var filter) || filter != $"principalId eq {subject.ObjectId:D}"
            || !parameters.TryGetValue("$select", out var select) || select != "id,appRoleId,principalId,principalType,resourceId"
            || !parameters.TryGetValue("$skiptoken", out var token) || token.Length is 0 or > 4096) return false;
        result = uri; return true;
    }
    internal static bool Fresh(DateTimeOffset start, DateTimeOffset now) => start <= now && now - start < TimeSpan.FromMinutes(15);
    internal static bool ValidHome(HomeStatusEvidenceV1? home, SubjectEnrollmentV1 e, long version, DateTimeOffset now)
        => home is not null && home.Subject == e.Subject && home.HomeTenantId == e.HomeTenantId && home.EnrollmentRevision == e.Revision
            && home.SecurityVersion == version && home.Active && home.Complete && AuthorityCodec.Utc(home.CheckedAtUtc)
            && AuthorityCodec.Utc(home.CutoffUtc) && home.CutoffUtc <= now && Fresh(home.CheckedAtUtc, now);
    private static JsonDocument Parse(ReadOnlyMemory<byte> bytes, string[] fields, string[]? required = null)
    {
        if (bytes.Length is 0 or > 65536) throw new InvalidOperationException("Synthetic provider response exceeds bounds.");
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
        try { AuthorityCodec.UniqueProperties(document.RootElement); Closed(document.RootElement, fields, required); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void Closed(JsonElement root, string[] fields, string[]? required = null)
    {
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(p => !fields.Contains(p.Name, StringComparer.Ordinal))
            || (required ?? fields).Any(p => !root.TryGetProperty(p, out _))) throw new InvalidOperationException("Closed synthetic provider response required.");
    }
    private static string Text(JsonElement root, string field) => root.GetProperty(field).ValueKind == JsonValueKind.String
        ? root.GetProperty(field).GetString()! : throw new InvalidOperationException("Provider field type denied.");
    private static Guid GuidField(JsonElement root, string field) => Guid.TryParseExact(Text(root, field), "D", out var value) && value != Guid.Empty && Text(root, field) == value.ToString("D")
        ? value : throw new InvalidOperationException("Provider identifier denied.");
}

internal static class AuthorityProviderTime
{
    internal static DateTimeOffset Parse(string json)
    {
        var value = JsonSerializer.Deserialize<string>(json);
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var time))
            throw new JsonException("Canonical provider UTC instant required.");
        return time;
    }
}
