using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ReportPublication;

/// <summary>Strict persisted projection decoder. It does not create source authority or admit a publication.</summary>
public static class NativePublicationProjectionReaderV1
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryRead(ReadOnlyMemory<byte> bytes, out PublicationProjectionV1? projection)
    {
        projection = null;
        if (bytes.IsEmpty || bytes.Length > NativePublicationCanonicalV1.MaximumProjectionBytes) return false;
        try
        {
            var owned = bytes.ToArray(); _ = StrictUtf8.GetString(owned);
            using var document = JsonDocument.Parse(owned, new JsonDocumentOptions { MaxDepth = 32 });
            Duplicates(document.RootElement);
            var candidate = ReadProjection(document.RootElement);
            if (!PublicationValidationV1.Projection(candidate) || !NativePublicationCanonicalV1.ProjectionBytes(candidate).AsSpan().SequenceEqual(owned)) return false;
            projection = candidate; return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { return false; }
    }

    private static PublicationProjectionV1 ReadProjection(JsonElement p)
    {
        Closed(p, "schemaVersion", "scope", "assessmentId", "runId", "runRevision", "inputs", "executiveSummary", "environmentScope", "scores",
            "maturity", "dimensions", "coverage", "findings", "rootCauses", "healthyControls", "recommendations", "acceptedRisks", "warnings",
            "methodology", "technicalAppendices", "redactionMarkers");
        Require(String(p, "schemaVersion") == "health-report-projection-v1");
        var s = p.GetProperty("scope"); Closed(s, "customerId", "projectId", "environmentId");
        var scope = new PublicationScopeV1(Id(s, "customerId"), Id(s, "projectId"), Id(s, "environmentId"), Id(p, "assessmentId"));
        return new(scope, Id(p, "runId"), Counter(p, "runRevision"), Inputs(p.GetProperty("inputs")),
            Array(p, "executiveSummary", Display), Array(p, "environmentScope", Display), Score(p.GetProperty("scores")),
            Maturity(p.GetProperty("maturity")), Array(p, "dimensions", Dimension), Coverage(p.GetProperty("coverage")),
            Array(p, "findings", Finding), Array(p, "rootCauses", RootCause), Array(p, "healthyControls", HealthyControl),
            Array(p, "recommendations", Recommendation), Array(p, "acceptedRisks", Risk), Array(p, "warnings", Warning),
            Array(p, "methodology", Display), Appendices(p.GetProperty("technicalAppendices")), Array(p, "redactionMarkers", Marker));
    }

    private static PublicationInputsV1 Inputs(JsonElement j)
    {
        Closed(j, "baselineId", "baselineDigest", "capabilityLockDigest", "ruleCatalogVersion", "ruleCatalogDigest", "scoringProfileVersion",
            "scoringProfileDigest", "maturityProfileVersion", "maturityProfileDigest", "desiredOutcomeVersion", "desiredOutcomeDigest",
            "scoringAlgorithmVersion", "maturityAlgorithmVersion", "applicationVersion", "reviewSnapshotDigest", "coverageSnapshotDigest",
            "runInputDigest", "aiPolicyVersion", "modelVersion", "promptVersion");
        return new(Id(j, "baselineId"), String(j, "baselineDigest"), String(j, "capabilityLockDigest"), String(j, "ruleCatalogVersion"),
            String(j, "ruleCatalogDigest"), String(j, "scoringProfileVersion"), String(j, "scoringProfileDigest"), String(j, "maturityProfileVersion"),
            String(j, "maturityProfileDigest"), NullableString(j, "desiredOutcomeVersion"), NullableString(j, "desiredOutcomeDigest"),
            String(j, "scoringAlgorithmVersion"), String(j, "maturityAlgorithmVersion"), String(j, "applicationVersion"),
            String(j, "reviewSnapshotDigest"), String(j, "coverageSnapshotDigest"), String(j, "runInputDigest"), String(j, "aiPolicyVersion"),
            NullableString(j, "modelVersion"), NullableString(j, "promptVersion"));
    }
    private static PublicationDisplayRecordV1 Display(JsonElement j)
    {
        Closed(j, "id", "category", "title", "text", "availability", "reason");
        return new(Id(j, "id"), String(j, "category"), String(j, "title"), NullableString(j, "text"),
            EnumValue<PublicationAvailabilityV1>(j, "availability"), EnumValue<PublicationReasonV1>(j, "reason"));
    }
    private static PublicationScoreV1 Score(JsonElement j)
    {
        Closed(j, "schemaVersion", "provisional", "publishable", "quality"); Require(String(j, "schemaVersion") == "health-report-score-v1");
        return new(Metric(j.GetProperty("provisional")), Metric(j.GetProperty("publishable")), Metric(j.GetProperty("quality")));
    }
    private static PublicationScoreValueV1 Metric(JsonElement j)
    {
        Closed(j, "availability", "value", "reason");
        return new(EnumValue<PublicationAvailabilityV1>(j, "availability"), Decimal(j, "value"), EnumValue<PublicationReasonV1>(j, "reason"));
    }
    private static PublicationMaturityV1 Maturity(JsonElement j)
    {
        Closed(j, "availability", "level", "algorithmVersion", "inputDigest", "contentDigest", "reason");
        return new(EnumValue<PublicationAvailabilityV1>(j, "availability"), j.GetProperty("level").ValueKind == JsonValueKind.Null
            ? null : EnumValue<PublicationMaturityLevelV1>(j, "level"), String(j, "algorithmVersion"), String(j, "inputDigest"),
            String(j, "contentDigest"), EnumValue<PublicationReasonV1>(j, "reason"));
    }
    private static PublicationDimensionV1 Dimension(JsonElement j)
    {
        Closed(j, "kind", "id", "category", "provisional", "publishable");
        return new(EnumValue<PublicationDimensionKindV1>(j, "kind"), Id(j, "id"), String(j, "category"),
            Metric(j.GetProperty("provisional")), Metric(j.GetProperty("publishable")));
    }
    private static PublicationCoverageV1 Coverage(JsonElement j)
    {
        Closed(j, "items", "counts"); var c = j.GetProperty("counts"); Closed(c, "planned", "executed", "gap", "notApplicable");
        return new(Array(j, "items", i =>
        {
            Closed(i, "id", "category", "state", "reason");
            return new PublicationCoverageItemV1(Id(i, "id"), String(i, "category"), EnumValue<PublicationCoverageStateV1>(i, "state"), EnumValue<PublicationReasonV1>(i, "reason"));
        }), new(Counter(c, "planned"), Counter(c, "executed"), Counter(c, "gap"), Counter(c, "notApplicable")));
    }
    private static PublicationFindingV1 Finding(JsonElement j)
    {
        Closed(j, "id", "category", "title", "summary", "severity", "state", "method", "confidencePercent", "confidenceBand", "confidenceAvailability",
            "confidenceReason", "mandatoryReview", "referenceIds", "rootCauseIds", "originalDigest", "reviewRevision");
        var mandatory = j.GetProperty("mandatoryReview"); Require(mandatory.ValueKind is JsonValueKind.True or JsonValueKind.False);
        return new(Id(j, "id"), String(j, "category"), String(j, "title"), String(j, "summary"), EnumValue<PublicationSeverityV1>(j, "severity"),
            EnumValue<PublicationFindingStateV1>(j, "state"), EnumValue<PublicationFindingMethodV1>(j, "method"), Decimal(j, "confidencePercent"),
            NullableString(j, "confidenceBand"), EnumValue<PublicationAvailabilityV1>(j, "confidenceAvailability"),
            EnumValue<PublicationReasonV1>(j, "confidenceReason"), mandatory.GetBoolean(), IdArray(j, "referenceIds"), IdArray(j, "rootCauseIds"),
            String(j, "originalDigest"), Counter(j, "reviewRevision"));
    }
    private static PublicationRootCauseV1 RootCause(JsonElement j)
    {
        Closed(j, "id", "category", "summary", "findingIds", "availability", "reason");
        return new(Id(j, "id"), String(j, "category"), NullableString(j, "summary"), IdArray(j, "findingIds"),
            EnumValue<PublicationAvailabilityV1>(j, "availability"), EnumValue<PublicationReasonV1>(j, "reason"));
    }
    private static PublicationHealthyControlV1 HealthyControl(JsonElement j)
    {
        Closed(j, "id", "category", "ruleId", "ruleVersion", "summary");
        return new(Id(j, "id"), String(j, "category"), String(j, "ruleId"), String(j, "ruleVersion"), String(j, "summary"));
    }
    private static PublicationRecommendationV1 Recommendation(JsonElement j)
    {
        Closed(j, "id", "findingId", "category", "summary", "priority", "effort", "reviewState");
        return new(Id(j, "id"), Id(j, "findingId"), String(j, "category"), String(j, "summary"), SourceDisplay(j.GetProperty("priority")),
            SourceDisplay(j.GetProperty("effort")), EnumValue<PublicationReviewStateV1>(j, "reviewState"));
    }
    private static PublicationSourceDisplayValueV1 SourceDisplay(JsonElement j)
    {
        Closed(j, "availability", "value", "reason", "sourceReference");
        return new(EnumValue<PublicationAvailabilityV1>(j, "availability"), NullableString(j, "value"), EnumValue<PublicationReasonV1>(j, "reason"),
            j.GetProperty("sourceReference").ValueKind == JsonValueKind.Null ? null : Id(j, "sourceReference"));
    }
    private static PublicationAcceptedRiskV1 Risk(JsonElement j)
    {
        Closed(j, "id", "findingId", "category", "decisionReference", "reviewAtUtc", "status");
        var text = String(j, "reviewAtUtc"); Require(DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc));
        return new(Id(j, "id"), Id(j, "findingId"), String(j, "category"), Id(j, "decisionReference"), utc, EnumValue<PublicationRiskStateV1>(j, "status"));
    }
    private static PublicationWarningV1 Warning(JsonElement j)
    {
        Closed(j, "kind", "recordId", "category");
        return new(EnumValue<PublicationWarningKindV1>(j, "kind"), Id(j, "recordId"), String(j, "category"));
    }
    private static PublicationAppendicesV1 Appendices(JsonElement j)
    {
        Closed(j, "notes", "protectedReferences");
        return new(Array(j, "notes", Display), Array(j, "protectedReferences", r =>
        {
            Closed(r, "id", "category", "availability", "reason");
            return new PublicationProtectedReferenceV1(Id(r, "id"), String(r, "category"), EnumValue<PublicationAvailabilityV1>(r, "availability"), EnumValue<PublicationReasonV1>(r, "reason"));
        }));
    }
    private static PublicationRedactionMarkerV1 Marker(JsonElement j)
    {
        Closed(j, "section", "id", "field", "reason");
        return new(String(j, "section"), Id(j, "id"), String(j, "field"), EnumValue<PublicationReasonV1>(j, "reason"));
    }

    private static T[] Array<T>(JsonElement owner, string field, Func<JsonElement, T> read)
    {
        var array = owner.GetProperty(field); Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() <= 100000);
        return array.EnumerateArray().Select(read).ToArray();
    }
    private static Guid[] IdArray(JsonElement j, string field) => Array(j, field, ReadId);
    private static Guid Id(JsonElement j, string field) => ReadId(j.GetProperty(field));
    private static Guid ReadId(JsonElement value)
    {
        var text = ReadString(value); Require(Guid.TryParseExact(text, "D", out var id) && id != Guid.Empty && id.ToString("D") == text); return id;
    }
    private static T EnumValue<T>(JsonElement j, string field) where T : struct, Enum
    {
        var text = String(j, field); Require(Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(value) && value.ToString() == text); return value;
    }
    private static string String(JsonElement j, string field) => ReadString(j.GetProperty(field));
    private static string ReadString(JsonElement value) { Require(value.ValueKind == JsonValueKind.String); return value.GetString()!; }
    private static string? NullableString(JsonElement j, string field) => j.GetProperty(field).ValueKind == JsonValueKind.Null ? null : String(j, field);
    private static long Counter(JsonElement j, string field)
    {
        var text = String(j, field); Require(long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value >= 0 && value.ToString(CultureInfo.InvariantCulture) == text); return value;
    }
    private static decimal? Decimal(JsonElement j, string field)
    {
        if (j.GetProperty(field).ValueKind == JsonValueKind.Null) return null;
        var text = String(j, field); Require(decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            && value is >= 0 and <= 100 && value.ToString("0.############################", CultureInfo.InvariantCulture) == text); return value;
    }
    private static void Closed(JsonElement j, params string[] fields) => Require(j.ValueKind == JsonValueKind.Object
        && j.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal)));
    private static void Duplicates(JsonElement j)
    {
        if (j.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in j.EnumerateObject()) { Require(names.Add(property.Name)); Duplicates(property.Value); }
        }
        else if (j.ValueKind == JsonValueKind.Array) foreach (var item in j.EnumerateArray()) Duplicates(item);
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Invalid native projection."); }
}
