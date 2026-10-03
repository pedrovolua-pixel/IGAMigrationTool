using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;

namespace SyntheticFixPackages;

/// <summary>Pure fixed fictional artifacts, preserving the complete verified upstream guidance value.</summary>
public static class FixPackageBuilder
{
    public const string SchemaVersion = "synthetic-fix-package-preview-v1";
    public const string TemplateVersion = "fictional-fix-templates-v1";
    public const string FixedStatus = "Unverified";
    public const string FixedDisclaimer = "Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.";
    public const int MaximumCanonicalBytes = 32 * 1024 * 1024;
    public const int MaximumRecords = 100_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ImmutableArray<FixTemplate> Templates =
    [
        new("fictional-config-v1", "Configuration", "{\n  \"fixtureOnly\": true,\n  \"reviewRequired\": true\n}"),
        new("fictional-script-v1", "Script", "# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),
        new("fictional-sql-v1", "Sql", "-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")
    ];
    private static readonly ImmutableArray<string> Warnings =
    [
        "Finding confirmation, rejection or deferral does not review artifacts or validate remediation.",
        "Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.",
        "Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection."
    ];
    private static readonly ImmutableArray<string> Unavailable =
    [
        "Consultant artifact review, approval history and content invalidation are unavailable.",
        "Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.",
        "Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable."
    ];
    private static readonly string TemplateDigest = Hash(CanonicalBytes(JsonSerializer.SerializeToElement(new
    { templateVersion = TemplateVersion, templates = Templates }, JsonOptions)));

    public static FixPackageResult Build(GuidanceSnapshot? guidance)
    {
        if (guidance is null) return Deny(FixPackageIssue.MissingGuidance);
        try
        {
            if (guidance.Source is null || guidance.Findings.IsDefault || guidance.Warnings.IsDefault || guidance.UnavailableSections.IsDefault)
                return Deny(FixPackageIssue.InvalidGuidance);
            long records = guidance.Findings.Length;
            var findings = ImmutableArray.CreateBuilder<GuidanceFindingInput>();
            foreach (var finding in guidance.Findings)
            {
                if (finding is null || finding.Options.IsDefault || finding.Options.Any(option => option is null))
                    return Deny(FixPackageIssue.InvalidGuidance);
                records += (long)finding.Options.Length * (1 + Templates.Length);
                if (records > MaximumRecords) return Deny(FixPackageIssue.OutputTooLarge);
                findings.Add(new(finding.FindingId, finding.RuleId, finding.RuleVersion, finding.CategoryId, finding.Severity,
                    finding.OriginalTitle, finding.PresentationTitle, finding.BusinessContext, finding.InitialState, finding.CurrentState,
                    finding.FindingRevision, finding.RootCause, finding.Occurrences,
                    finding.Options.Select(option => new GuidanceOptionInput(option.OptionId, option.Text,
                        option.Prerequisites, option.Risk, option.RecoveryGuidance)).ToImmutableArray(),
                    finding.ValidationGuidance, finding.GuidanceReferences, finding.Assumptions, finding.Limitations));
            }
            var rebuilt = RecommendationGuidanceBuilder.Build(new(guidance.Source, findings.ToImmutable()));
            if (!rebuilt.Succeeded) return Deny(FixPackageIssue.InvalidGuidance);
            var verified = rebuilt.Snapshot!;
            if (verified.ContentDigest != guidance.ContentDigest ||
                !RecommendationGuidanceBuilder.CanonicalPayload(verified).AsSpan().SequenceEqual(
                    RecommendationGuidanceBuilder.CanonicalPayload(guidance)))
                return Deny(FixPackageIssue.IntegrityMismatch);

            var packages = verified.Findings.Select(finding =>
            {
                var packageId = Hash(CanonicalBytes(JsonSerializer.SerializeToElement(new
                { findingId = finding.FindingId, runId = verified.Source.RunId.ToString("D"), scope = verified.Source.Scope }, JsonOptions)));
                var options = finding.Options.OrderBy(option => option.ScopedOptionId, StringComparer.Ordinal).Select(option =>
                    new FixOption(option.ScopedOptionId, Templates.Select(template => new FixArtifact(
                        Hash(CanonicalBytes(JsonSerializer.SerializeToElement(new
                        { packageId, scopedOptionId = option.ScopedOptionId, templateId = template.TemplateId, templateVersion = TemplateVersion }, JsonOptions))),
                        template.TemplateId, template.Kind, FixedStatus, template.Text)).ToImmutableArray())).ToImmutableArray();
                return new FixPackage(packageId, finding.FindingId, options);
            }).ToImmutableArray();
            var warnings = verified.Findings.IsEmpty
                ? Warnings.Add("No findings were supplied; no fix packages or actions are available.") : Warnings;
            var pending = new FixPackageSnapshot(SchemaVersion, FixedStatus, FixedDisclaimer, verified,
                TemplateVersion, TemplateDigest, Templates, packages, warnings, Unavailable, "", "");
            var bytes = CanonicalPayload(pending);
            return new(null, new FixPackageSnapshot(SchemaVersion, FixedStatus, FixedDisclaimer, verified,
                TemplateVersion, TemplateDigest, Templates, packages, warnings, Unavailable, Encoding.UTF8.GetString(bytes), Hash(bytes)));
        }
        catch (OutputLimitException) { return Deny(FixPackageIssue.OutputTooLarge); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ObjectDisposedException or ArgumentException or OverflowException or NullReferenceException)
        { return Deny(FixPackageIssue.InvalidGuidance); }
    }

    /// <summary>Serialize complete actual properties, independently of cached CanonicalJson and ContentDigest.</summary>
    public static byte[] CanonicalPayload(FixPackageSnapshot snapshot) => CanonicalBytes(JsonSerializer.SerializeToElement(new
    {
        snapshot.SchemaVersion,
        snapshot.Status,
        snapshot.Disclaimer,
        snapshot.Guidance,
        snapshot.TemplateVersion,
        snapshot.TemplateDigest,
        snapshot.Templates,
        snapshot.Packages,
        snapshot.Warnings,
        snapshot.UnavailableSections
    }, JsonOptions));

    private static byte[] CanonicalBytes(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value, 0);
        if (stream.Length > MaximumCanonicalBytes) throw new OutputLimitException();
        return stream.ToArray();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value, int depth)
    {
        if (depth > 32) throw new JsonException("Unsupported fixture nesting.");
        if (writer.BytesCommitted + writer.BytesPending > MaximumCanonicalBytes) throw new OutputLimitException();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().ToArray();
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new JsonException("Duplicate fixture property.");
                writer.WriteStartObject();
                foreach (var property in properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); Write(writer, property.Value, depth + 1); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item, depth + 1);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetDecimal().ToString("G29", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.String:
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                value.WriteTo(writer);
                break;
            default: throw new JsonException("Unsupported fixture value.");
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static FixPackageResult Deny(FixPackageIssue issue) => new(issue, null);
    internal sealed class OutputLimitException : Exception;
}
