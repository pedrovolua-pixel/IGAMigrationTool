using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace ReportDrafts;

/// <summary>Read-only detached synthetic draft; no scope resolution, persistence, rendering or authority.</summary>
public static class DraftSnapshotBuilder
{
    public const string SchemaVersion = "synthetic-draft-report-v1";
    public const string Status = "SyntheticDraft";
    public const int MaximumContentBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly string[] ContentFields = ["provisional", "publishableCurrent", "categories", "objectTypes", "modules", "outcomes",
        "quality", "findings", "warnings", "maturity", "reviewHistory", "healthyControls", "limitations", "methodology", "unavailableSections"];

    public static DraftReportResult Build(DraftReportInput? input)
    {
        if (input is null || input.Source is null) return Deny(DraftReportIssue.InvalidInput);
        try
        {
            var source = input.Source;
            if (ValidateSource(source) is { } issue) return Deny(issue);
            var frozen = Canonical(source.FrozenVersions, "/frozenVersions");
            var capability = Canonical(source.CapabilityLock, "/capabilityLock");
            var analysis = Canonical(source.AnalysisLock, "/analysisLock");
            var content = Canonical(input.Content, "");
            if (ValidateContent(content, source) is { } contentIssue) return Deny(contentIssue);
            source = source with { FrozenVersions = frozen, CapabilityLock = capability, AnalysisLock = analysis };
            var snapshot = new DraftReportSnapshot(SchemaVersion, Status, source, "", content);
            return new(null, snapshot with { CanonicalContentDigest = Hash(CanonicalPayload(snapshot)) });
        }
        catch (ValidationException exception) { return Deny(exception.Issue); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ObjectDisposedException or ArgumentException or OverflowException)
        { return Deny(DraftReportIssue.InvalidInput); }
    }

    /// <summary>Full schema/status/source/content envelope. Digest excluded; returned bytes are caller-owned.</summary>
    public static byte[] CanonicalPayload(DraftReportSnapshot snapshot)
    {
        var element = JsonSerializer.SerializeToElement(new
        { schemaVersion = snapshot.SchemaVersion, status = snapshot.Status, source = snapshot.Source, content = snapshot.Content }, Options);
        return JsonSerializer.SerializeToUtf8Bytes(Canonical(element, "/envelope"));
    }

    public static bool ValidateSnapshot(DraftReportSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.SchemaVersion != SchemaVersion || snapshot.Status != Status) return false;
        var result = Build(new(snapshot.Source, snapshot.Content));
        return result.Succeeded && result.Snapshot!.CanonicalContentDigest == snapshot.CanonicalContentDigest &&
            Hash(CanonicalPayload(snapshot)) == snapshot.CanonicalContentDigest;
    }

    private static DraftReportIssue? ValidateSource(DraftSourceBinding source)
    {
        if (source.Scope != new DraftScope("synthetic-customer", "synthetic-project", "synthetic-environment")) return DraftReportIssue.WrongScope;
        if (source.RunId == Guid.Empty || source.RunRevision < 0 || source.RunState != "Scoring" ||
            !Text(source.BaselineId) || !Text(source.ProfileId)) return DraftReportIssue.InvalidSource;
        if (source.ProfileId is not ("synthetic-review-maturity-equal-v1" or "synthetic-review-maturity-operations-v1" or "synthetic-review-maturity-fix-packages-equal-v1" or "synthetic-review-maturity-fix-review-equal-v1")) return DraftReportIssue.UnknownVersion;
        if (source.BaselineId is not ("synthetic-analysis-healthy-v1" or "synthetic-analysis-findings-v1" or "synthetic-analysis-mixed-v1" or "synthetic-analysis-gaps-v1")) return DraftReportIssue.UnknownVersion;
        if (source.ReviewRunId != source.RunId || source.ReviewRunRevision != source.RunRevision) return DraftReportIssue.SourceMismatch;
        foreach (var digest in new[] { source.RunInputDigest, source.AnalysisFixtureDigest, source.AnalysisContentDigest,
            source.ScoringContentDigest, source.SavedCoverageDigest, source.ReviewSnapshotDigest, source.MaturityFixtureDigest,
            source.MaturityInputDigest, source.MaturityContentDigest }) if (!Digest(digest)) return DraftReportIssue.InvalidSource;
        var artifactReview = source.ProfileId == "synthetic-review-maturity-fix-review-equal-v1";
        var fixPackages = artifactReview || source.ProfileId == "synthetic-review-maturity-fix-packages-equal-v1";
        var versions = source.FrozenVersions;
        if (artifactReview && String(versions, "fixReviewContractDigest") != "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f") return DraftReportIssue.UnknownVersion;
        string[] versionFields = ["profileVersion", "desiredOutcomeVersion", "scoringAlgorithmVersion", "aiPolicyVersion", "promptVersion", "modelVersion", "applicationVersion", "workSchemaVersion", "scriptedResultsDigest", "analysisFixtureDigest", "maturityFixtureDigest"];
        if (!Object(versions, artifactReview ? [.. versionFields, "fixPackageTemplateDigest", "fixReviewContractDigest"] : fixPackages ? [.. versionFields, "fixPackageTemplateDigest"] : versionFields)) return DraftReportIssue.InvalidSource;
        if (fixPackages && String(versions, "fixPackageTemplateDigest") != "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669") return DraftReportIssue.UnknownVersion;
        var expected = new Dictionary<string, string>
        {
            ["profileVersion"] = "synthetic-profile-v1",
            ["scoringAlgorithmVersion"] = "pilot-health-v1",
            ["aiPolicyVersion"] = "synthetic-ai-disabled-v1",
            ["promptVersion"] = "synthetic-prompt-disabled-v1",
            ["modelVersion"] = "synthetic-model-disabled-v1",
            ["applicationVersion"] = artifactReview ? "synthetic-fix-review-app-v1" : fixPackages ? "synthetic-fix-packages-app-v1" : "synthetic-review-maturity-app-v1",
            ["workSchemaVersion"] = "synthetic-run-work-v1"
        };
        if (expected.Any(item => String(versions, item.Key) != item.Value) || versions.GetProperty("desiredOutcomeVersion").ValueKind != JsonValueKind.Null)
            return DraftReportIssue.UnknownVersion;
        if (!Digest(String(versions, "scriptedResultsDigest")) || String(versions, "analysisFixtureDigest") != source.AnalysisFixtureDigest ||
            String(versions, "maturityFixtureDigest") != source.MaturityFixtureDigest) return DraftReportIssue.SourceMismatch;
        var capability = source.CapabilityLock;
        if (!Object(capability, ["matrixVersion", "stateAtLock", "productBuild", "databaseSchemaBuild", "hotfixSetDigest", "sqlServerBuild",
            "compatibilityLevel", "modules", "queryPackVersion", "normalizationSchemaVersion", "ruleCatalogVersion", "lockDigest"]) ||
            !Positive(capability, "compatibilityLevel") || !Digest(String(capability, "lockDigest"))) return DraftReportIssue.InvalidSource;
        if (String(capability, "stateAtLock") != "FixtureVerified") return DraftReportIssue.UnknownVersion;
        foreach (var name in new[] { "matrixVersion", "productBuild", "databaseSchemaBuild", "hotfixSetDigest", "sqlServerBuild", "queryPackVersion", "normalizationSchemaVersion", "ruleCatalogVersion" })
            if (!Text(String(capability, name))) return DraftReportIssue.InvalidSource;
        if (!Array(capability, "modules") || !capability.GetProperty("modules").EnumerateArray().Any() ||
            capability.GetProperty("modules").EnumerateArray().Any(module => !Object(module, ["id", "version"]) || !Text(String(module, "id")) || !Text(String(module, "version"))))
            return DraftReportIssue.InvalidSource;
        var analysis = source.AnalysisLock;
        if (!Object(analysis, ["scope", "packVersion", "packDigest", "presetId", "presetVersion", "evidenceDigest", "catalogVersion", "catalogDigest",
            "profileId", "profileVersion", "profileDigest", "compatibility"])) return DraftReportIssue.InvalidSource;
        if (!Object(analysis.GetProperty("scope"), ["customerId", "projectId", "environmentId"]) ||
            String(analysis.GetProperty("scope"), "customerId") != source.Scope.CustomerId ||
            String(analysis.GetProperty("scope"), "projectId") != source.Scope.ProjectId ||
            String(analysis.GetProperty("scope"), "environmentId") != source.Scope.EnvironmentId || String(analysis, "presetId") != source.BaselineId ||
            String(analysis, "profileVersion") != String(versions, "profileVersion") ||
            String(analysis, "catalogVersion") != String(capability, "ruleCatalogVersion")) return DraftReportIssue.SourceMismatch;
        if (String(analysis, "packVersion") != "synthetic-analysis-pack-v1" || String(analysis, "catalogVersion") != "synthetic-analysis-catalog-v1" ||
            String(analysis, "presetVersion") != "synthetic-evidence-v1" || String(analysis, "profileId") != (fixPackages ? "synthetic-analysis-equal-v1" : source.ProfileId.Replace("synthetic-review-maturity-", "synthetic-analysis-", StringComparison.Ordinal)))
            return DraftReportIssue.UnknownVersion;
        foreach (var name in new[] { "packDigest", "evidenceDigest", "catalogDigest", "profileDigest" }) if (!Digest(String(analysis, name))) return DraftReportIssue.InvalidSource;
        var compatibility = analysis.GetProperty("compatibility");
        if (!Object(compatibility, ["sourceProduct", "productVersion", "evidenceSchemaVersion", "ruleLanguageVersion"]) ||
            String(compatibility, "sourceProduct") != "SYNTHETIC-ONLY" || String(compatibility, "productVersion") != "fixture-product-v1" ||
            String(compatibility, "evidenceSchemaVersion") != "fixture-facts-v1" || String(compatibility, "ruleLanguageVersion") != "count-predicate-v1") return DraftReportIssue.UnknownVersion;
        if (String(capability, "productBuild") != String(compatibility, "productVersion") ||
            String(capability, "databaseSchemaBuild") != String(compatibility, "evidenceSchemaVersion")) return DraftReportIssue.SourceMismatch;
        return null;
    }

    private static DraftReportIssue? ValidateContent(JsonElement content, DraftSourceBinding source)
    {
        if (!Object(content, ContentFields) || !Score(content.GetProperty("provisional")) || !Score(content.GetProperty("publishableCurrent"))) return DraftReportIssue.InvalidContent;
        foreach (var field in new[] { "categories", "objectTypes", "modules", "outcomes" })
        {
            if (!Array(content, field) || content.GetProperty(field).EnumerateArray().Any(row => !Object(row, field == "categories" ? ["id", "provisional", "publishableCurrent", "provisionalWeight", "publishableWeight"] : ["id", "provisional", "publishableCurrent"]) || !Text(String(row, "id")) ||
                !row.TryGetProperty("provisional", out var p) || !Score(p) || !row.TryGetProperty("publishableCurrent", out var c) || !Score(c) || (field == "categories" && (!Weight(row, "provisionalWeight") || !Weight(row, "publishableWeight"))))) return DraftReportIssue.InvalidContent;
        }
        foreach (var field in new[] { "warnings", "methodology", "unavailableSections" })
            if (!Array(content, field) || content.GetProperty(field).EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || !Text(item.GetString()))) return DraftReportIssue.InvalidContent;
        if (!content.GetProperty("methodology").EnumerateArray().Any() || !content.GetProperty("unavailableSections").EnumerateArray().Any()) return DraftReportIssue.InvalidContent;
        var quality = content.GetProperty("quality");
        if (!Object(quality, ["plannedUnits", "executedUnits", "gapUnits", "notApplicableUnits", "proposedReviewUnits", "totalFindingUnits"])) return DraftReportIssue.InvalidContent;
        foreach (var count in new[] { "plannedUnits", "executedUnits", "gapUnits", "notApplicableUnits", "proposedReviewUnits", "totalFindingUnits" })
            if (!Count(quality, count)) return DraftReportIssue.InvalidContent;
        if ((long)Int(quality, "executedUnits") + Int(quality, "gapUnits") + Int(quality, "notApplicableUnits") != Int(quality, "plannedUnits")) return DraftReportIssue.InvalidContent;
        var maturity = content.GetProperty("maturity");
        if (String(maturity, "status") != "Ready" || String(maturity, "algorithmVersion") != "pilot-maturity-v1" ||
            String(maturity, "catalogVersion") != "synthetic-maturity-catalog-v1" ||
            String(maturity, "level") is not ("Initial" or "Developing" or "Defined" or "Managed" or "Optimized") ||
            String(maturity, "inputDigest") != source.MaturityInputDigest || String(maturity, "contentDigest") != source.MaturityContentDigest ||
            !Positive(maturity, "mandatoryDomains") || !Count(maturity, "insufficientIndicators") || !Count(maturity, "insufficientDomains") ||
            !Count(maturity, "improvementMissingDistinctAssessments") || !Array(maturity, "domains") || !Array(maturity, "gates")) return DraftReportIssue.SourceMismatch;
        if (maturity.GetProperty("domains").GetArrayLength() != Int(maturity, "mandatoryDomains") || maturity.GetProperty("domains").EnumerateArray().Any(domain =>
            !Text(String(domain, "id")) || !Text(String(domain, "name")) || !Array(domain, "indicators") || domain.GetProperty("indicators").GetArrayLength() != 5)) return DraftReportIssue.InvalidContent;
        if (ValidateMaturity(maturity) is { } maturityIssue) return maturityIssue;
        foreach (var field in new[] { "findings", "reviewHistory", "healthyControls", "limitations" }) if (!Array(content, field)) return DraftReportIssue.InvalidContent;
        if (content.GetProperty("reviewHistory").EnumerateArray().Any(row => !Object(row, ["findingId", "revision", "originalTitle", "businessContext", "events"]))) return DraftReportIssue.InvalidContent;
        var reviews = content.GetProperty("reviewHistory").EnumerateArray().ToDictionary(row => String(row, "findingId") ?? "");
        var findings = content.GetProperty("findings").EnumerateArray().ToArray();
        if (reviews.Count != findings.Length) return DraftReportIssue.SourceMismatch;
        foreach (var finding in findings)
        {
            if (!Object(finding, ["id", "title", "originalTitle", "initialState", "category", "severity", "confidencePercent", "state", "reviewRequired",
                "ruleId", "ruleVersion", "baselineId", "rootCause", "rootCauseKey", "objectIds", "evidenceReferences", "facts", "inferences", "assumptions",
                "impact", "recommendations", "validationGuidance", "sources", "confidenceBand", "method", "likelihood", "limitations", "originalDigests",
                "outcomeIds", "revision", "businessContext", "occurrenceIds"]) || !Text(String(finding, "id")) || !Count(finding, "revision") || !Text(String(finding, "title")) || !Text(String(finding, "originalTitle")) ||
                String(finding, "state") is not ("Proposed" or "AutoConfirmed" or "Confirmed" or "Rejected" or "Deferred") ||
                String(finding, "initialState") is not ("Proposed" or "AutoConfirmed") || !Array(finding, "occurrenceIds") || !Array(finding, "originalDigests") ||
                !finding.GetProperty("occurrenceIds").EnumerateArray().Any() || !finding.GetProperty("originalDigests").EnumerateArray().Any()) return DraftReportIssue.InvalidContent;
            if (String(finding, "severity") is not ("Critical" or "High" or "Medium" or "Low" or "Informational") || String(finding, "method") != "Deterministic" ||
                !decimal.TryParse(String(finding, "confidencePercent"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var confidence) || confidence is < 0 or > 100 ||
                String(finding, "baselineId") != source.BaselineId || String(finding, "businessContext") is null || !Boolean(finding, "reviewRequired")) return DraftReportIssue.InvalidContent;
            foreach (var field in new[] { "category", "ruleId", "ruleVersion", "rootCauseKey", "confidenceBand", "rootCause", "impact", "validationGuidance", "likelihood" }) if (!Text(String(finding, field))) return DraftReportIssue.InvalidContent;
            foreach (var field in new[] { "objectIds", "evidenceReferences", "facts", "inferences", "assumptions", "recommendations", "sources", "limitations", "originalDigests", "outcomeIds", "occurrenceIds" })
                if (!Array(finding, field) || finding.GetProperty(field).EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String || !Text(value.GetString()))) return DraftReportIssue.InvalidContent;
            if (finding.GetProperty("originalDigests").EnumerateArray().Any(value => !Digest(value.GetString()))) return DraftReportIssue.InvalidContent;
            if (!reviews.TryGetValue(String(finding, "id")!, out var review) || !Count(review, "revision") || Int(review, "revision") != Int(finding, "revision") ||
                String(review, "originalTitle") != String(finding, "originalTitle") || String(review, "businessContext") != String(finding, "businessContext") || !Array(review, "events")) return DraftReportIssue.SourceMismatch;
            var revision = 0;
            var eventIds = new HashSet<Guid>();
            var state = String(finding, "initialState");
            var title = String(finding, "originalTitle");
            var context = "";
            foreach (var history in review.GetProperty("events").EnumerateArray())
            {
                if (!Count(history, "revision") || Int(history, "revision") != ++revision || !Text(String(history, "actorId")) ||
                    !Guid.TryParse(String(history, "eventId"), out var eventId) || eventId == Guid.Empty ||
                    !DateTimeOffset.TryParse(String(history, "recordedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) ||
                    String(history, "kind") is not ("Confirm" or "Reject" or "Defer" or "Comment" or "EditPresentation")) return DraftReportIssue.InvalidContent;
                if (!eventIds.Add(eventId)) return DraftReportIssue.DuplicateId;
                if (!Object(history, ["eventId", "actorId", "actorRoles", "kind", "recordedAtUtc", "revision", "state", "reason", "text", "title", "businessContext"]) ||
                    !StringArray(history, "actorRoles") || !history.GetProperty("actorRoles").EnumerateArray().Any() ||
                    String(history, "state") is not ("Proposed" or "AutoConfirmed" or "Confirmed" or "Rejected" or "Deferred")) return DraftReportIssue.InvalidContent;
                foreach (var field in new[] { "reason", "text", "title", "businessContext" }) if (!NullableString(history, field)) return DraftReportIssue.InvalidContent;
                state = String(history, "state");
                title = String(history, "title") ?? title;
                context = String(history, "businessContext") ?? context;
            }
            if (revision != Int(review, "revision")) return DraftReportIssue.SourceMismatch;
            if (state != String(finding, "state") || title != String(finding, "title") || context != String(finding, "businessContext")) return DraftReportIssue.SourceMismatch;
        }
        foreach (var healthy in content.GetProperty("healthyControls").EnumerateArray()) if (!Object(healthy, ["objectId", "ruleId", "ruleVersion", "state"]) || !Text(String(healthy, "objectId")) ||
            !Text(String(healthy, "ruleId")) || !Text(String(healthy, "ruleVersion")) || String(healthy, "state") != "Pass") return DraftReportIssue.InvalidContent;
        foreach (var gap in content.GetProperty("limitations").EnumerateArray()) if (!Object(gap, ["objectId", "ruleId", "state", "reasonCode"]) || !Text(String(gap, "objectId")) || !Text(String(gap, "ruleId")) ||
            !Text(String(gap, "reasonCode")) || String(gap, "state") is not ("NotAssessed" or "InsufficientEvidence" or "Excluded" or "Inaccessible" or "Redacted" or "Unsupported" or "Error")) return DraftReportIssue.InvalidContent;
        return null;
    }

    private static DraftReportIssue? ValidateMaturity(JsonElement maturity)
    {
        if (!Object(maturity, ["status", "reasonCode", "level", "algorithmVersion", "catalogVersion", "inputDigest", "contentDigest", "authorityBoundary",
            "mandatoryDomains", "insufficientIndicators", "insufficientDomains", "improvementMissingDistinctAssessments", "governanceOwnershipEvidenced", "gates", "domains", "ownership"]) ||
            !NullableString(maturity, "reasonCode") || !Text(String(maturity, "authorityBoundary")) || !Boolean(maturity, "governanceOwnershipEvidenced")) return DraftReportIssue.InvalidContent;
        var gateLevels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gate in maturity.GetProperty("gates").EnumerateArray())
        {
            var level = String(gate, "level");
            if (level is not ("Developing" or "Defined" or "Managed" or "Optimized") ||
                !Object(gate, ["level", "metDomains", "mandatoryDomains", "requiredPercent", "isMet"]) || !Count(gate, "metDomains") ||
                !Count(gate, "mandatoryDomains") || Int(gate, "mandatoryDomains") != Int(maturity, "mandatoryDomains") ||
                !Count(gate, "requiredPercent") || Int(gate, "requiredPercent") != (level == "Developing" ? 60 : 80) || !Boolean(gate, "isMet") ||
                Int(gate, "metDomains") > Int(gate, "mandatoryDomains")) return DraftReportIssue.InvalidContent;
            if (!gateLevels.Add(level)) return DraftReportIssue.DuplicateId;
        }
        if (gateLevels.Count != 4) return DraftReportIssue.InvalidContent;
        foreach (var domain in maturity.GetProperty("domains").EnumerateArray())
        {
            if (!Object(domain, ["id", "name", "baseMet", "operationAndReviewMet", "improvementMet", "insufficientIndicators", "indicators"]) ||
                !Boolean(domain, "baseMet") || !Boolean(domain, "operationAndReviewMet") || !Boolean(domain, "improvementMet") || !Count(domain, "insufficientIndicators")) return DraftReportIssue.InvalidContent;
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var indicator in domain.GetProperty("indicators").EnumerateArray())
            {
                var kind = String(indicator, "kind");
                if (kind is not ("Design" or "Implementation" or "MeasuredOperation" or "RegularReview" or "ValidatedImprovement") ||
                    !Object(indicator, ["kind", "state", "reasonCode", "evidenceReferences", "assessmentReferences", "hasValidatedImprovementEvidence"]) ||
                    !IndicatorState(String(indicator, "state")) || !NullableString(indicator, "reasonCode") || !StringArray(indicator, "evidenceReferences") ||
                    !StringArray(indicator, "assessmentReferences") || !Boolean(indicator, "hasValidatedImprovementEvidence")) return DraftReportIssue.InvalidContent;
                if (!kinds.Add(kind)) return DraftReportIssue.DuplicateId;
                if (String(indicator, "state") == "InsufficientEvidence" ? !Text(String(indicator, "reasonCode")) : !indicator.GetProperty("evidenceReferences").EnumerateArray().Any())
                    return DraftReportIssue.InvalidContent;
                if (kind == "ValidatedImprovement" && String(indicator, "state") == "Met" && !indicator.GetProperty("hasValidatedImprovementEvidence").GetBoolean()) return DraftReportIssue.InvalidContent;
            }
            if (kinds.Count != 5) return DraftReportIssue.InvalidContent;
        }
        var ownership = maturity.GetProperty("ownership");
        if (!Object(ownership, ["state", "ownerId", "evidenceReferences", "reasonCode"]) || !IndicatorState(String(ownership, "state")) ||
            !NullableString(ownership, "ownerId") || !NullableString(ownership, "reasonCode") || !StringArray(ownership, "evidenceReferences")) return DraftReportIssue.InvalidContent;
        if (String(ownership, "state") == "InsufficientEvidence" ? !Text(String(ownership, "reasonCode")) : !ownership.GetProperty("evidenceReferences").EnumerateArray().Any()) return DraftReportIssue.InvalidContent;
        if (String(ownership, "state") == "Met" && !Text(String(ownership, "ownerId"))) return DraftReportIssue.InvalidContent;
        return null;
    }

    private static JsonElement Canonical(JsonElement element, string path)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element, path, 0);
        if (stream.Length > MaximumContentBytes) throw new ValidationException(DraftReportIssue.InvalidContent);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element, string path, int depth)
    {
        if (depth > 32) throw new ValidationException(DraftReportIssue.InvalidContent);
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new ValidationException(DraftReportIssue.DuplicateId);
            writer.WriteStartObject();
            foreach (var property in properties.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (property.Name is "actions" or "allowedActions" or "csrfToken" or "observedAtDatabaseUtc" or "reportDraft") throw new ValidationException(DraftReportIssue.InvalidContent);
                writer.WritePropertyName(property.Name); Write(writer, property.Value, path + "/" + property.Name, depth + 1);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var array = element.EnumerateArray().ToArray();
            string? key = path switch
            {
                "/categories" or "/objectTypes" or "/modules" or "/outcomes" or "/findings" or "/maturity/domains" or "/capabilityLock/modules" => "id",
                "/reviewHistory" => "findingId",
                "/healthyControls" or "/limitations" => "objectId",
                _ => null
            };
            if (key is not null)
            {
                string Identity(JsonElement value)
                {
                    var id = String(value, key);
                    if (!Text(id)) throw new ValidationException(DraftReportIssue.InvalidContent);
                    if (path is "/healthyControls" or "/limitations")
                    {
                        var rule = String(value, "ruleId");
                        if (!Text(rule)) throw new ValidationException(DraftReportIssue.InvalidContent);
                        return id!.Length.ToString(CultureInfo.InvariantCulture) + ":" + id + rule;
                    }
                    return id!;
                }
                if (array.Select(Identity).Distinct(StringComparer.Ordinal).Count() != array.Length) throw new ValidationException(DraftReportIssue.DuplicateId);
                array = array.OrderBy(Identity, StringComparer.Ordinal).ToArray();
            }
            writer.WriteStartArray(); foreach (var item in array) Write(writer, item, path + "/*", depth + 1); writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Number) writer.WriteRawValue(element.GetDecimal().ToString("G29", CultureInfo.InvariantCulture));
        else if (element.ValueKind is JsonValueKind.String or JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False) element.WriteTo(writer);
        else throw new ValidationException(DraftReportIssue.InvalidContent);
    }

    private static bool Weight(JsonElement row, string name) => row.GetProperty(name).ValueKind == JsonValueKind.Null ||
        decimal.TryParse(String(row, name), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var weight) && weight is > 0 and <= 1;

    private static bool Score(JsonElement score)
    {
        if (!Object(score, ["raw", "display", "status", "eligibleUnits"]) || !Count(score, "eligibleUnits")) return false;
        if (score.GetProperty("raw").ValueKind == JsonValueKind.Null) return score.GetProperty("display").ValueKind == JsonValueKind.Null && String(score, "status") == "Unavailable" && Int(score, "eligibleUnits") == 0;
        return decimal.TryParse(String(score, "raw"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var raw) && raw is >= 0 and <= 100 &&
            decimal.TryParse(String(score, "display"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var display) && display == decimal.Round(raw, 1, MidpointRounding.AwayFromZero) &&
            String(score, "status") == (raw < 50 ? "Red" : raw < 80 ? "Yellow" : "Green") && Int(score, "eligibleUnits") > 0;
    }
    private static bool Object(JsonElement value, string[] fields) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == fields.Length && fields.All(field => value.TryGetProperty(field, out _));
    private static bool Array(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array;
    private static bool StringArray(JsonElement value, string name) => Array(value, name) && value.GetProperty(name).EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && Text(item.GetString()));
    private static bool Boolean(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False;
    private static bool NullableString(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.Null or JsonValueKind.String;
    private static bool IndicatorState(string? value) => value is "Met" or "PartiallyMet" or "NotMet" or "InsufficientEvidence";
    private static string? String(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Count(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number) && number >= 0;
    private static bool Positive(JsonElement value, string name) => Count(value, name) && Int(value, name) > 0;
    private static int Int(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static DraftReportResult Deny(DraftReportIssue issue) => new(issue, null);
    private sealed class ValidationException(DraftReportIssue issue) : Exception { public DraftReportIssue Issue { get; } = issue; }
}
