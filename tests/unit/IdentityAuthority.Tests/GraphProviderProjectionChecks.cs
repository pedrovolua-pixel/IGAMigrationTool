using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IdentityAuthority;
using IdentityPolicy;
using IdentitySessions;

internal static class GraphProviderProjectionChecks
{
    public static async Task<int> RunAsync()
    {
        var checks = 0;
        void Check(bool result, string label)
        {
            if (!result) throw new InvalidOperationException("Graph projection test: " + label);
            checks++;
        }
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var subject = new SessionSubject(Guid.NewGuid(), Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        var roles = Enum.GetValues<CoarseAppRole>().Select(role => new ProviderAppRoleV1(Guid.NewGuid(), role, true)).ToImmutableArray();
        var binding = new ProviderRoleBindingV1("provider-role-binding-v1", subject.TenantId, Guid.NewGuid(), Guid.NewGuid(),
            roles, 1, new string('a', 64), Guid.NewGuid());
        var clock = new ProjectionClock(now);
        var enrollment = new SubjectEnrollmentV1("subject-enrollment-v1", subject, 1, EnrollmentLifecycle.Active,
            OrganizationalOrigin.InternalOrganizational, subject.TenantId, now,
            new(Guid.NewGuid(), new(subject.TenantId, Guid.NewGuid()), now));
        byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
        string User(string cutoff = "2026-10-03T11:00:00Z") => JsonSerializer.Serialize(new
        {
            id = subject.ObjectId.ToString("D"), accountEnabled = true, userType = "Member", externalUserState = (string?)null,
            signInSessionsValidFromDateTime = cutoff
        });
        string Metadata(string json, string value = "https://graph.microsoft.com/v1.0/$metadata")
            => json[..^1] + ",\"@odata.context\":" + JsonSerializer.Serialize(value) + "}";
        string Row(int index = 0, string? assignment = null) => JsonSerializer.Serialize(new
        {
            id = assignment ?? "opaque_not_a_guid/" + index, appRoleId = roles[index].AppRoleId.ToString("D"),
            principalId = subject.ObjectId.ToString("D"), principalType = "User", resourceId = binding.ResourceServicePrincipalId.ToString("D")
        });
        string Page(string[] rows, string? next = null) => "{\"value\":[" + string.Join(',', rows) + "]"
            + (next is null ? "}" : ",\"@odata.nextLink\":" + JsonSerializer.Serialize(next) + "}");
        byte[]? ProjectUser(string json) => GraphProviderResponseProjection.ProjectUser(Utf8(json), subject, binding, now);
        byte[]? ProjectPage(string json) => GraphProviderResponseProjection.ProjectDirectRolesPage(Utf8(json), subject, binding);
        var source = new ProjectedFixtureSource();
        var reader = new SyntheticProviderReader(source, binding, clock);
        var next = reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=opaque%2Btoken%3D";

        // HTTPS-PROV-T08: every approved precision preserves ticks and uses the existing strict reader format.
        for (var precision = 0; precision <= 7; precision++)
        {
            var fraction = precision == 0 ? "" : "." + "1234567"[..precision];
            var projected = ProjectUser(Metadata(User("2026-10-03T11:00:00" + fraction + "Z")));
            Check(projected is not null, "accepted UTC precision " + precision);
            using var user = JsonDocument.Parse(projected!);
            var expected = "2026-10-03T11:00:00." + (precision == 0 ? "0000000" : "1234567"[..precision].PadRight(7, '0')) + "Z";
            Check(user.RootElement.GetProperty("signInSessionsValidFromDateTime").GetString() == expected
                && !user.RootElement.TryGetProperty("@odata.context", out _), "exact normalization/metadata discard " + precision);
            source.User = projected; source.Pages = [ProjectPage(Metadata(Page([Row()])))!];
            var result = await reader.ReadAsync(enrollment, 1, 1);
            var instant = DateTimeOffset.ParseExact(expected, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            Check(result is not null && result.Observation.ResourceCutoffUtc == instant, "reader compatibility without rounding " + precision);
        }

        // HTTPS-PROV-T03/T08: no coercion, unknown metadata, alias, malformed UTF-8 or invented cutoff.
        var validUser = User();
        foreach (var json in new[]
        {
            "{}", "[]", "null", validUser[..^1], validUser + "{}", validUser.Replace("true", "\"true\""),
            validUser.Replace("true", "null"), validUser.Replace("\"Member\"", "\"member\""),
            validUser.Replace("\"Member\"", "\"0\""), validUser.Replace("\"Member\"", "0"),
            validUser.Replace(subject.ObjectId.ToString("D"), subject.ObjectId.ToString("D").ToUpperInvariant()),
            validUser.Replace(subject.ObjectId.ToString("D"), Guid.NewGuid().ToString("D")),
            validUser.Replace("\"externalUserState\":null", "\"externalUserState\":\"accepted\""),
            validUser.Replace("\"externalUserState\":null", "\"externalUserState\":\"0\""),
            validUser.Replace("\"externalUserState\":null,", ""),
            validUser.Replace("\"accountEnabled\":true", "\"accountEnabled\":true,\"accountEnabled\":true"),
            validUser.Replace("\"accountEnabled\":true", "\"accountEnabled\":true,\"accountEn\\u0061bled\":true"),
            validUser[..^1] + ",\"@odata.type\":\"user\"}", validUser[..^1] + ",\"@odata.context\":null}",
            Metadata(validUser)[..^1] + ",\"@odata.context\":\"duplicate\"}",
            validUser[..^1] + ",\"@odata.context\":{\"x\":1}}", Metadata(validUser, new string('x', 65536)),
            validUser[..^1] + ",\"@odata.context\":\"\\ud800\"}",
            validUser[..^1] + ",\"extra\":" + new string('[', 13) + "0" + new string(']', 13) + "}",
            validUser.Replace("null", "\"\\ud800\"")
        }) Check(ProjectUser(json) is null, "closed user hostile corpus");
        foreach (var cutoff in new[]
        {
            "2026-10-03T11:00:00.12345678Z", "2026-10-03T11:00:00.Z", "2026-10-03T12:00:00.0000001Z",
            "2026-10-03T11:00:00+00:00", "2026-10-03T11:00:00.1+01:00", "2026-10-03t11:00:00Z",
            "2026-10-03T11:00:00z", "2026-10-03 11:00:00Z", "2026-02-30T11:00:00Z",
            "2026-10-03T24:00:00Z", "2026-10-03T11:00:60Z", "2026-10-03T11:00:00", "bad"
        }) Check(ProjectUser(User(cutoff)) is null, "invalid/future/non-UTC cutoff");
        Check(ProjectUser(User(now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture))) is not null,
            "cutoff equal to trusted now is valid");
        Check(ProjectUser(validUser.Replace("\"signInSessionsValidFromDateTime\":\"2026-10-03T11:00:00Z\"",
            "\"signInSessionsValidFromDateTime\":null")) is null, "null cutoff denied");
        Check(ProjectUser(validUser.Replace("true", "false")) is not null, "complete negative account status retained");
        Check(ProjectUser(validUser.Replace("\"Member\"", "\"Guest\"").Replace("null", "\"PendingAcceptance\"")) is not null,
            "projection parses known invitation without granting external admission");
        Check(GraphProviderResponseProjection.ProjectUser(Utf8(validUser), subject, binding, now.ToOffset(TimeSpan.FromHours(1))) is null,
            "trusted non-UTC clock denied");
        var invalidUtf8 = Utf8(Metadata(validUser, "sentinel"));
        var marker = Encoding.UTF8.GetString(invalidUtf8).IndexOf("sentinel", StringComparison.Ordinal);
        invalidUtf8[marker] = 0xc0; invalidUtf8[marker + 1] = 0xaf;
        Check(GraphProviderResponseProjection.ProjectUser(invalidUtf8, subject, binding, now) is null, "invalid UTF-8 even in discarded metadata");
        Check(GraphProviderResponseProjection.ProjectUser(ReadOnlyMemory<byte>.Empty, subject, binding, now) is null, "empty bytes denied");
        var atBound = Utf8(Metadata(validUser)).Concat(Enumerable.Repeat((byte)' ', 65536 - Utf8(Metadata(validUser)).Length)).ToArray();
        Check(GraphProviderResponseProjection.ProjectUser(atBound, subject, binding, now) is not null, "whole raw body exact bound accepted");
        Check(GraphProviderResponseProjection.ProjectUser(atBound.Append((byte)' ').ToArray(), subject, binding, now) is null, "raw body over bound denied");
        Check(GraphProviderResponseProjection.ProjectUser(Utf8(validUser), subject with { TenantId = Guid.NewGuid() }, binding, now) is null,
            "wrong enrolled tenant denied");
        Check(GraphProviderResponseProjection.ProjectUser(Utf8(validUser), default, binding, now) is null, "empty subject denied");
        Check(GraphProviderResponseProjection.ProjectUser(Utf8(validUser), subject, binding with { ManifestRevision = 0 }, now) is null,
            "invalid trusted binding denied");

        // HTTPS-PROV-T05/T07: page-local validation, opaque IDs and preservation of the exact safe continuation.
        var projectedPage = ProjectPage(Metadata(Page([Row()], next)));
        Check(projectedPage is not null, "opaque assignment and fixed query accepted");
        using (var page = JsonDocument.Parse(projectedPage!))
            Check(page.RootElement.GetProperty("@odata.nextLink").GetString() == next
                && page.RootElement.GetProperty("value")[0].GetProperty("id").GetString() == "opaque_not_a_guid/0"
                && !page.RootElement.TryGetProperty("@odata.context", out _), "continuation unchanged and no envelope metadata");
        Check(ProjectPage(Page(Enumerable.Range(0, 4).Select(i => Row(i)).ToArray())) is not null, "four bound roles accepted");
        Check(ProjectPage(Page([])) is not null && ProjectPage("{\"value\":[],\"@odata.nextLink\":null}") is not null,
            "empty page or null continuation does not itself assert all-page completeness");
        var row = Row();
        foreach (var badRow in new[]
        {
            row.Replace("\"User\"", "\"Group\""), row.Replace("\"User\"", "\"ServicePrincipal\""),
            row.Replace(subject.ObjectId.ToString("D"), Guid.NewGuid().ToString("D")),
            row.Replace(subject.ObjectId.ToString("D"), subject.ObjectId.ToString("D").ToUpperInvariant()),
            row.Replace(binding.ResourceServicePrincipalId.ToString("D"), Guid.NewGuid().ToString("D")),
            row.Replace(roles[0].AppRoleId.ToString("D"), Guid.NewGuid().ToString("D")),
            row.Replace(roles[0].AppRoleId.ToString("D"), Guid.Empty.ToString("D")),
            row[..^1] + ",\"@odata.context\":\"unexpected-row-annotation\"}",
            row.Replace("\"id\":", "\"id\":\"duplicate\",\"id\":"), Row(assignment: ""), Row(assignment: new string('x', 257))
        }) Check(ProjectPage(Page([badRow])) is null, "wrong or ambiguous role row denied");
        foreach (var badPage in new[]
        {
            "{}", "[]", "{\"value\":null}", Page([row, row]), Page([row, Row(0, "differentOpaqueAssignment")]),
            Page([row, Row(1, "opaque_not_a_guid/0")]),
            Page([Row(), Row(1), Row(2), Row(3), Row()]), Page([])[..^1] + ",\"@odata.count\":0}",
            Page([])[..^1] + ",\"@odata.nextLink\":1}", Page([])[..^1] + ",\"value\":[]}",
            Metadata(Page([]))[..^1] + ",\"@odata.context\":\"duplicate\"}", Page([])[..^1] + ",\"@odata.context\":false}"
        }) Check(ProjectPage(badPage) is null, "closed page hostile corpus");
        foreach (var badNext in new[]
        {
            next.Replace("https://", "http://"), next.Replace("graph.microsoft.com", "evil.invalid"),
            next.Replace("graph.microsoft.com", "graph.microsoft.com:444"), next.Replace("https://", "https://user@"),
            next + "#fragment", next.Replace("appRoleAssignedTo", "users"), next.Replace("appRoleAssignedTo", "%61ppRoleAssignedTo"),
            next.Replace("/v1.0/", "/removed/../v1.0/"),
            reader.RoleQuery(subject).AbsoluteUri, next + "&$filter=x", next.Replace("$skiptoken=", "$skip="),
            next.Replace("principalId", "wrongPrincipal"), next.Replace("resourceId", "otherId"),
            reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=", reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=" + new string('x', 4097),
            reader.RoleQuery(subject).AbsoluteUri + "&$skiptoken=" + new string('x', 8192)
        }) Check(ProjectPage(Page([], badNext)) is null, "hostile query or continuation bound denied");
        Check(GraphProviderResponseProjection.ProjectDirectRolesPage(Utf8(Page([row])), subject,
            binding with { AppRoles = roles.SetItem(0, roles[0] with { Enabled = false }) }) is null, "disabled binding role denied");
        var invalidPageUtf8 = Utf8(Metadata(Page([]), "sentinel"));
        var pageMarker = Encoding.UTF8.GetString(invalidPageUtf8).IndexOf("sentinel", StringComparison.Ordinal);
        invalidPageUtf8[pageMarker] = 0xc0; invalidPageUtf8[pageMarker + 1] = 0xaf;
        Check(GraphProviderResponseProjection.ProjectDirectRolesPage(invalidPageUtf8, subject, binding) is null, "page invalid UTF-8 denied");
        var pageAtBound = Utf8(Metadata(Page([]))).Concat(Enumerable.Repeat((byte)' ', 65536 - Utf8(Metadata(Page([]))).Length)).ToArray();
        Check(GraphProviderResponseProjection.ProjectDirectRolesPage(pageAtBound, subject, binding) is not null, "page exact raw bound accepted");
        Check(GraphProviderResponseProjection.ProjectDirectRolesPage(pageAtBound.Append((byte)' ').ToArray(), subject, binding) is null,
            "page oversized bytes denied");

        // Projection is page-local: the existing reader rejects stateful replay/cycles and enforces home denial.
        source.User = ProjectUser(validUser);
        source.Pages = [projectedPage!, ProjectPage(Page([Row(1)]))!];
        Check(await reader.ReadAsync(enrollment, 1, 1) is not null, "normalized two-page reader compatibility");
        source.Pages = [projectedPage!, ProjectPage(Page([Row()]))!];
        Check(await reader.ReadAsync(enrollment, 1, 1) is null, "reader rejects role replay across projected pages");
        source.Pages = [ProjectPage(Page([], next))!, ProjectPage(Page([], next))!];
        Check(source.Pages.All(p => p is not null) && await reader.ReadAsync(enrollment, 1, 1) is null,
            "safe individual URL does not imply cycle-free complete pagination");
        source.User = ProjectUser(validUser.Replace("null", "\"Accepted\"")); source.Pages = [ProjectPage(Page([Row()]))!];
        Check(await reader.ReadAsync(enrollment with { Origin = OrganizationalOrigin.ExternalOrganizational, HomeTenantId = Guid.NewGuid() }, 1, 1) is null,
            "external Member remains denied without independent home proof");
        return checks;
    }

    private sealed class ProjectionClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ProjectedFixtureSource : ISyntheticProviderSource
    {
        public byte[]? User { get; set; }
        public byte[][] Pages { get; set; } = [];
        private int page;
        public ValueTask<ReadOnlyMemory<byte>?> ReadUserAsync(SessionSubject subject, CancellationToken cancellationToken)
        {
            page = 0;
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(User is null ? null : User);
        }
        public ValueTask<ReadOnlyMemory<byte>?> ReadDirectRolesPageAsync(Uri exactQuery, CancellationToken cancellationToken)
            => ValueTask.FromResult<ReadOnlyMemory<byte>?>(page < Pages.Length ? Pages[page++] : null);
    }
}
