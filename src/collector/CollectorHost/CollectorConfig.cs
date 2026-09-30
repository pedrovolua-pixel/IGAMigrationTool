using System.Globalization;
using System.Text.Json;

namespace CollectorHost;

public sealed record CollectorConfig(
    Guid ScopeId,
    string ExactBuild,
    Guid QueryPackId,
    int QueryPackVersion,
    string QueryPackSha256,
    Guid FieldPolicyId,
    int FieldPolicyVersion,
    string FieldPolicySha256,
    string SqlDescriptorRef,
    string TimeZoneId,
    TimeOnly LocalRunTime,
    bool Enabled,
    int MaxPageSize,
    long MaxRows,
    int MaxDurationSeconds,
    long MaxLocalBytes,
    int RetentionHours,
    Guid OfflineRecipientKeyId)
{
    private static readonly HashSet<string> Properties =
    [
        "schemaVersion", "scopeId", "exactBuild", "queryPackId", "queryPackVersion", "queryPackSha256",
        "fieldPolicyId", "fieldPolicyVersion", "fieldPolicySha256", "sqlDescriptorRef", "timeZoneId",
        "localRunTime", "enabled", "maxPageSize", "maxRows", "maxDurationSeconds", "maxLocalBytes",
        "retentionHours", "offlineRecipientKeyId"
    ];

    public static CollectorConfig Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid();
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!Properties.Contains(property.Name) || !names.Add(property.Name))
                {
                    throw Invalid();
                }
            }

            if (names.Count != Properties.Count || Int(root, "schemaVersion") != 1)
            {
                throw Invalid();
            }

            var timeText = String(root, "localRunTime", 5);
            if (!TimeOnly.TryParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var localTime))
            {
                throw Invalid();
            }

            var zoneId = String(root, "timeZoneId", 128);
            _ = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            var sqlDescriptor = String(root, "sqlDescriptorRef", 1024);
            if (!LocalPath.IsValid(sqlDescriptor))
            {
                throw Invalid();
            }

            var retentionHours = Int(root, "retentionHours");
            if (retentionHours is < 1 or > 720)
            {
                throw Invalid();
            }

            return new CollectorConfig(
                Id(root, "scopeId"),
                String(root, "exactBuild", 128),
                Id(root, "queryPackId"),
                Positive(Int(root, "queryPackVersion")),
                Digest(root, "queryPackSha256"),
                Id(root, "fieldPolicyId"),
                Positive(Int(root, "fieldPolicyVersion")),
                Digest(root, "fieldPolicySha256"),
                sqlDescriptor,
                zoneId,
                localTime,
                Bool(root, "enabled"),
                Positive(Int(root, "maxPageSize")),
                Positive(Long(root, "maxRows")),
                Positive(Int(root, "maxDurationSeconds")),
                Positive(Long(root, "maxLocalBytes")),
                retentionHours,
                Id(root, "offlineRecipientKeyId"));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or
               TimeZoneNotFoundException or InvalidTimeZoneException or FormatException or OverflowException)
        {
            throw Invalid();
        }
    }

    private static string String(JsonElement root, string name, int maxLength)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid();
        }

        var text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength || text.Any(char.IsControl))
        {
            throw Invalid();
        }

        return text;
    }

    private static Guid Id(JsonElement root, string name)
    {
        var text = String(root, name, 36);
        return Guid.TryParseExact(text, "D", out var id) && id != Guid.Empty ? id : throw Invalid();
    }

    private static string Digest(JsonElement root, string name)
    {
        var text = String(root, name, 64);
        return text.Length == 64 && text.All(Uri.IsHexDigit) ? text.ToLowerInvariant() : throw Invalid();
    }

    private static int Int(JsonElement root, string name) => root.GetProperty(name).GetInt32();
    private static long Long(JsonElement root, string name) => root.GetProperty(name).GetInt64();
    private static bool Bool(JsonElement root, string name) => root.GetProperty(name).GetBoolean();
    private static int Positive(int value) => value > 0 ? value : throw Invalid();
    private static long Positive(long value) => value > 0 ? value : throw Invalid();
    private static FormatException Invalid() => new("Invalid collector configuration.");
}
