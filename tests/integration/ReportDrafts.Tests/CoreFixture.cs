using System.Text.Json;
using System.Text.Json.Nodes;
using ReportDrafts;

internal static class CoreFixture
{
    internal static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    internal const string Baseline = "synthetic-analysis-findings-v1";
    internal const string FindingId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    internal const string Hostile = "## stolen\n<script>alert(1)</script> & [link](https://evil.invalid/a) ![img](https://evil.invalid/x) ```sh\n$(touch /tmp/x)\n=SUM(A1:A2)\t\u202e";
    internal static string Hex(char value) => new(value, 64);
    internal static JsonElement Json(object value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.SerializeToElement(value, options);
    }
    internal static JsonElement Change(JsonElement original, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(original.GetRawText())!.AsObject(); change(node); return Json(node);
    }
    internal static DraftReportInput Create()
    {
        var scope = new DraftScope("synthetic-customer", "synthetic-project", "synthetic-environment");
        var versions = Json(new
        {
            profileVersion = "synthetic-profile-v1",
            desiredOutcomeVersion = (string?)null,
            scoringAlgorithmVersion = "pilot-health-v1",
            aiPolicyVersion = "synthetic-ai-disabled-v1",
            promptVersion = "synthetic-prompt-disabled-v1",
            modelVersion = "synthetic-model-disabled-v1",
            applicationVersion = "synthetic-review-maturity-app-v1",
            workSchemaVersion = "synthetic-run-work-v1",
            scriptedResultsDigest = Hex('1'),
            analysisFixtureDigest = Hex('2'),
            maturityFixtureDigest = Hex('8')
        });
        var capability = Json(new
        {
            matrixVersion = "synthetic-analysis-matrix-v1",
            stateAtLock = "FixtureVerified",
            productBuild = "fixture-product-v1",
            databaseSchemaBuild = "fixture-facts-v1",
            hotfixSetDigest = "synthetic-hotfix-digest",
            sqlServerBuild = "synthetic-sql-build",
            compatibilityLevel = 160,
            modules = new[] { new { id = "SyntheticSecurity", version = "synthetic-module-v1" }, new { id = "SyntheticOperations", version = "synthetic-module-v1" } },
            queryPackVersion = "synthetic-query-pack-v1",
            normalizationSchemaVersion = "synthetic-normalization-v1",
            ruleCatalogVersion = "synthetic-analysis-catalog-v1",
            lockDigest = Hex('a')
        });
        var analysis = Json(new
        {
            scope,
            packVersion = "synthetic-analysis-pack-v1",
            packDigest = Hex('b'),
            presetId = Baseline,
            presetVersion = "synthetic-evidence-v1",
            evidenceDigest = Hex('c'),
            catalogVersion = "synthetic-analysis-catalog-v1",
            catalogDigest = Hex('d'),
            profileId = "synthetic-analysis-equal-v1",
            profileVersion = "synthetic-profile-v1",
            profileDigest = Hex('e'),
            compatibility = new { sourceProduct = "SYNTHETIC-ONLY", productVersion = "fixture-product-v1", evidenceSchemaVersion = "fixture-facts-v1", ruleLanguageVersion = "count-predicate-v1" }
        });
        var source = new DraftSourceBinding(scope, RunId, 13, "Scoring", Hex('0'), Baseline, "synthetic-review-maturity-equal-v1", versions, capability, analysis,
            Hex('2'), Hex('3'), Hex('4'), Hex('5'), RunId, 13, Hex('f'), Hex('8'), Hex('6'), Hex('7'));
        var provisional = new { raw = "50", display = "50.0", status = "Yellow", eligibleUnits = 2 };
        var current = new { raw = "100", display = "100.0", status = "Green", eligibleUnits = 1 };
        var failing = new { raw = "0", display = "0.0", status = "Red", eligibleUnits = 1 };
        var unavailable = new { raw = (string?)null, display = (string?)null, status = "Unavailable", eligibleUnits = 0 };
        var finding = new
        {
            id = FindingId,
            title = "Original synthetic title",
            originalTitle = "Original synthetic title",
            initialState = "Proposed",
            category = "SECURITY",
            severity = "Critical",
            confidencePercent = "100",
            state = "Proposed",
            reviewRequired = true,
            ruleId = "SYN-GUARD",
            ruleVersion = "synthetic-rule-v1",
            baselineId = Baseline,
            rootCause = "Synthetic root cause",
            rootCauseKey = FindingId,
            objectIds = new[] { "SYN-OBJECT-A" },
            evidenceReferences = new[] { "fixture:reference" },
            facts = new[] { "A fictional marker was observed." },
            inferences = new[] { "A synthetic inference only." },
            assumptions = new[] { "Fictional fixture." },
            impact = "Synthetic impact",
            recommendations = new[] { "Unverified guidance; do not execute." },
            validationGuidance = "Fresh evidence required.",
            sources = new[] { "fixture:guidance" },
            confidenceBand = "Deterministic synthetic evidence",
            method = "Deterministic",
            likelihood = "Synthetic likelihood",
            limitations = new[] { "No customer authority." },
            originalDigests = new[] { Hex('9') },
            outcomeIds = Array.Empty<string>(),
            revision = 0,
            businessContext = "",
            occurrenceIds = new[] { Hex('0') }
        };
        var kinds = new[] { "Design", "Implementation", "MeasuredOperation", "RegularReview", "ValidatedImprovement" };
        var maturity = new
        {
            status = "Ready",
            reasonCode = (string?)null,
            level = "Initial",
            algorithmVersion = "pilot-maturity-v1",
            catalogVersion = "synthetic-maturity-catalog-v1",
            inputDigest = Hex('6'),
            contentDigest = Hex('7'),
            authorityBoundary = "Fictional synthetic indicators only; no One Identity catalog, promotion, validation or customer authority.",
            mandatoryDomains = 2,
            insufficientIndicators = 10,
            insufficientDomains = 2,
            improvementMissingDistinctAssessments = 0,
            governanceOwnershipEvidenced = false,
            gates = new[] { "Developing", "Defined", "Managed", "Optimized" }.Select(level => new { level, metDomains = 0, mandatoryDomains = 2, requiredPercent = level == "Developing" ? 60 : 80, isMet = false }).ToArray(),
            domains = new[] { "SYN-DOMAIN-Z", "SYN-DOMAIN-A" }.Select(id => new
            {
                id,
                name = "Fictional domain " + id,
                baseMet = false,
                operationAndReviewMet = false,
                improvementMet = false,
                insufficientIndicators = 5,
                indicators = kinds.Select(kind => new
                {
                    kind,
                    state = "InsufficientEvidence",
                    reasonCode = "SYN-MISSING",
                    evidenceReferences = Array.Empty<string>(),
                    assessmentReferences = Array.Empty<string>(),
                    hasValidatedImprovementEvidence = false
                }).ToArray()
            }).ToArray(),
            ownership = new { state = "InsufficientEvidence", ownerId = (string?)null, evidenceReferences = Array.Empty<string>(), reasonCode = "SYN-OWNER-MISSING" }
        };
        var content = Json(new
        {
            provisional,
            publishableCurrent = current,
            categories = new object[] { new { id = "SECURITY", provisional = failing, publishableCurrent = unavailable, provisionalWeight = "0.5", publishableWeight = (string?)null },
                new { id = "OPERATIONS", provisional = current, publishableCurrent = current, provisionalWeight = "0.5", publishableWeight = "1" } },
            objectTypes = new[] { new { id = "SyntheticControl", provisional, publishableCurrent = current } },
            modules = new object[] { new { id = "SyntheticSecurity", provisional = failing, publishableCurrent = unavailable }, new { id = "SyntheticOperations", provisional = current, publishableCurrent = current } },
            outcomes = Array.Empty<object>(),
            quality = new { plannedUnits = 2, executedUnits = 2, gapUnits = 0, notApplicableUnits = 0, proposedReviewUnits = 1, totalFindingUnits = 1 },
            findings = new[] { finding },
            warnings = new[] { "Synthetic draft, unpublished.", "One proposed Critical occurrence awaits review." },
            maturity,
            reviewHistory = new[] { new { findingId = FindingId, revision = 0, originalTitle = finding.originalTitle, businessContext = "", events = Array.Empty<object>() } },
            healthyControls = new[] { new { objectId = "SYN-OBJECT-B", ruleId = "SYN-TRACE", ruleVersion = "synthetic-rule-v1", state = "Pass" } },
            limitations = Array.Empty<object>(),
            methodology = new[] { "Health and maturity are independent.", "Default gaps affect quality." },
            unavailableSections = new[] { "Risk acceptance", "Reassessment", "AI", "Tasks", "Publication" }
        });
        return new(source, content);
    }
    internal static DraftReportInput WithHistory(DraftReportInput input, bool duplicate = false)
    {
        var content = Change(input.Content, node =>
        {
            node["findings"]![0]!["revision"] = 2;
            node["reviewHistory"]![0]!["revision"] = 2;
            var events = new JsonArray();
            for (var revision = 1; revision <= 2; revision++) events.Add(JsonNode.Parse(Json(new
            {
                eventId = revision == 1 || duplicate ? "aaaaaaaa-1111-2222-3333-444444444444" : "bbbbbbbb-1111-2222-3333-444444444444",
                actorId = "synthetic-consultant",
                actorRoles = new[] { "Consultant" },
                kind = "Comment",
                recordedAtUtc = "2026-10-02T00:00:00+00:00",
                revision,
                state = "Proposed",
                reason = (string?)null,
                text = "Synthetic comment " + revision,
                title = (string?)null,
                businessContext = (string?)null
            }).GetRawText()));
            node["reviewHistory"]![0]!["events"] = events;
        });
        return input with { Content = content };
    }
}
