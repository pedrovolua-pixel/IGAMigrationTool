using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReportDrafts;

public sealed record StructuredDraftMarkdownResult(string Version, string CanonicalContentDigest,
    string MarkdownText, string MarkdownSha256);

/// <summary>Inert text projection of one validated synthetic value; no renderer, storage or authority.</summary>
public static class StructuredDraftMarkdown
{
    public const string Version = "synthetic-draft-markdown-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly (string Id, string Label)[] Sections =
    [
        ("categories", "Category health"), ("objectTypes", "Object-type health"),
        ("modules", "Module health"), ("outcomes", "Outcome adherence"),
        ("findings", "Current findings and generated originals"),
        ("healthyControls", "Healthy synthetic controls"), ("quality", "Separate assessment quality"),
        ("limitations", "Coverage limitations"), ("maturity", "Independent capability maturity"),
        ("reviewHistory", "Captured review history"), ("methodology", "Methodology"),
        ("unavailableSections", "Unavailable sections")
    ];

    public static StructuredDraftMarkdownResult Render(DraftReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!DraftSnapshotBuilder.ValidateSnapshot(snapshot))
            throw new ArgumentException("A validated, unchanged synthetic draft snapshot is required.", nameof(snapshot));

        var output = new StringBuilder();
        output.Append("# Synthetic draft report — unpublished\n\n");
        output.Append("This is a read-only SYNTHETIC draft, not a validated One Identity assessment or a published report. ");
        output.Append("The run remains Scoring. No publication, customer approval, risk acceptance or remediation execution is established.\n\n");
        output.Append("| Binding | Value |\n| --- | --- |\n");
        Row(output, "Markdown version", Version);
        Row(output, "Draft schema", snapshot.SchemaVersion);
        Row(output, "Draft status", snapshot.Status);
        Row(output, "Canonical content digest", snapshot.CanonicalContentDigest);
        output.Append('\n');

        Heading(output, "source", "Source bindings");
        Table(output, JsonSerializer.SerializeToElement(snapshot.Source, JsonOptions));
        Heading(output, "summary", "Health summary");
        output.Append("Health and quality are separate. Gaps never imply healthy controls. Maturity is independently evidence-based.\n\n");
        output.Append("### provisional — Provisional health\n\n");
        Table(output, snapshot.Content.GetProperty("provisional"));
        output.Append("### publishableCurrent — Publishable-current health (unpublished)\n\n");
        Table(output, snapshot.Content.GetProperty("publishableCurrent"));
        output.Append("### warnings — Interpretation warnings\n\n");
        Table(output, snapshot.Content.GetProperty("warnings"));

        foreach (var (id, label) in Sections)
        {
            Heading(output, id, label);
            var value = snapshot.Content.GetProperty(id);
            if (id is "findings" or "reviewHistory")
            {
                if (value.GetArrayLength() == 0) output.Append("No records in this synthetic fixture.\n\n");
                foreach (var record in value.EnumerateArray())
                {
                    var identifier = record.GetProperty(id == "findings" ? "id" : "findingId").GetString()!;
                    output.Append("### ").Append(id == "findings" ? "finding:" : "review:")
                        .Append(Escape(identifier)).Append('\n').Append('\n');
                    Table(output, record);
                }
            }
            else Table(output, value);
        }

        var text = output.ToString();
        return new(Version, snapshot.CanonicalContentDigest, text,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }

    private static void Heading(StringBuilder output, string id, string label)
        => output.Append("## ").Append(id).Append(" — ").Append(label).Append("\n\n");

    private static void Table(StringBuilder output, JsonElement value)
    {
        output.Append("| Field | Value |\n| --- | --- |\n");
        Leaves(output, value, "value");
        output.Append('\n');
    }

    private static void Leaves(StringBuilder output, JsonElement value, string path)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (!value.EnumerateObject().Any()) Row(output, path, "(empty record)");
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                    Leaves(output, property.Value, path == "value" ? property.Name : path + "." + property.Name);
                break;
            case JsonValueKind.Array:
                if (value.GetArrayLength() == 0) Row(output, path, "(empty list)");
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    Leaves(output, item, path + "[" + (index++).ToString(CultureInfo.InvariantCulture) + "]");
                break;
            case JsonValueKind.String: Row(output, path, value.GetString()!); break;
            case JsonValueKind.Null: Row(output, path, "null (not supplied)"); break;
            default: Row(output, path, value.GetRawText()); break;
        }
    }

    private static void Row(StringBuilder output, string name, string value)
        => output.Append("| ").Append(Escape(name)).Append(" | ").Append(Escape(value)).Append(" |\n");

    // Escape source punctuation before any Markdown processor can interpret it.
    // Dot, colon, slash and @ escaping also prevents GFM URL/email autolinks.
    // Source newlines never create headings, table rows, fences or list items.
    private static string Escape(string value)
    {
        var output = new StringBuilder();
        foreach (var character in value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
        {
            if (character == '\n') output.Append(" ↵ ");
            else if (character == '\t') output.Append(" [tab] ");
            else if (char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
                output.Append("[U+").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture)).Append(']');
            else if (character == '<') output.Append("&lt;");
            else if (character == '>') output.Append("&gt;");
            else if (character == '&') output.Append("&amp;");
            else if (character is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~')
                output.Append('\\').Append(character);
            else output.Append(character);
        }
        return output.ToString();
    }
}
