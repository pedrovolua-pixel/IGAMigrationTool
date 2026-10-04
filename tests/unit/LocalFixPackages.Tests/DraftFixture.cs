using System.Text.Json;
using ReportDrafts;

public static class DraftFixture
{
    public const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string FindingId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string Baseline = "synthetic-analysis-findings-v1";
    public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public static DraftReportInput Create()
    {
        var run = Guid.Parse("7eedaf91-6e83-4e82-8b2b-deefc910b27b");
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
            scriptedResultsDigest = Digest,
            analysisFixtureDigest = Digest,
            maturityFixtureDigest = Digest
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
            lockDigest = Digest
        });
        var analysis = Json(new
        {
            scope,
            packVersion = "synthetic-analysis-pack-v1",
            packDigest = Digest,
            presetId = Baseline,
            presetVersion = "synthetic-evidence-v1",
            evidenceDigest = Digest,
            catalogVersion = "synthetic-analysis-catalog-v1",
            catalogDigest = Digest,
            profileId = "synthetic-analysis-equal-v1",
            profileVersion = "synthetic-profile-v1",
            profileDigest = Digest,
            compatibility = new
            {
                sourceProduct = "SYNTHETIC-ONLY",
                productVersion = "fixture-product-v1",
                evidenceSchemaVersion = "fixture-facts-v1",
                ruleLanguageVersion = "count-predicate-v1"
            }
        });
        var source = new DraftSourceBinding(scope, run, 13, "Scoring", Digest, Baseline, "synthetic-review-maturity-equal-v1", versions, capability, analysis,
            Digest, Digest, Digest, Digest, run, 13, Digest, Digest, Digest, Digest);
        var provisional = new { raw = "66.666666666666666666666666667", display = "66.7", status = "Yellow", eligibleUnits = 3 };
        var current = new { raw = "100", display = "100.0", status = "Green", eligibleUnits = 2 };
        var finding = new
        {
            id = FindingId,
            title = "Current fictional title",
            originalTitle = "Original fictional title",
            initialState = "Proposed",
            category = "SECURITY",
            severity = "Critical",
            confidencePercent = "100",
            state = "Proposed",
            reviewRequired = true,
            ruleId = "SYN-GUARD",
            ruleVersion = "synthetic-rule-v1",
            baselineId = Baseline,
            rootCause = "Fictional root cause",
            rootCauseKey = FindingId,
            objectIds = new[] { "OBJECT-1" },
            evidenceReferences = new[] { "fixture:evidence-1" },
            facts = new[] { "A fixed fictional marker was observed." },
            inferences = new[] { "No real vendor defect is inferred." },
            assumptions = new[] { "Synthetic fixture premise." },
            impact = "Fictional impact",
            recommendations = new[] { "Unverified fixed fictional guidance; do not execute." },
            validationGuidance = "New evidence required.",
            sources = new[] { "fixture-guidance:SYN-GUARD:v1" },
            confidenceBand = "Deterministic synthetic evidence",
            method = "Deterministic",
            likelihood = "Fictional likelihood",
            limitations = new[] { "No customer validation." },
            originalDigests = new[] { Digest },
            outcomeIds = Array.Empty<string>(),
            revision = 1,
            businessContext = "Fictional business context",
            occurrenceIds = new[] { Digest }
        };
        var maturity = new
        {
            status = "Ready",
            reasonCode = (string?)null,
            level = "Initial",
            algorithmVersion = "pilot-maturity-v1",
            catalogVersion = "synthetic-maturity-catalog-v1",
            inputDigest = Digest,
            contentDigest = Digest,
            authorityBoundary = "Fictional synthetic indicators only.",
            mandatoryDomains = 1,
            insufficientIndicators = 5,
            insufficientDomains = 1,
            improvementMissingDistinctAssessments = 0,
            governanceOwnershipEvidenced = false,
            gates = new[] { new { level = "Developing", metDomains = 0, mandatoryDomains = 1, requiredPercent = 60, isMet = false },
                new { level = "Defined", metDomains = 0, mandatoryDomains = 1, requiredPercent = 80, isMet = false },
                new { level = "Managed", metDomains = 0, mandatoryDomains = 1, requiredPercent = 80, isMet = false },
                new { level = "Optimized", metDomains = 0, mandatoryDomains = 1, requiredPercent = 80, isMet = false } },
            domains = new[] { new { id = "DOMAIN-1", name = "Fictional mandatory domain", baseMet = false, operationAndReviewMet = false, improvementMet = false,
                insufficientIndicators = 5, indicators = new[] { "Design", "Implementation", "MeasuredOperation", "RegularReview", "ValidatedImprovement" }.Select(kind => new
                { kind, state = "InsufficientEvidence", reasonCode = "FIXTURE-MISSING", evidenceReferences = Array.Empty<string>(), assessmentReferences = Array.Empty<string>(), hasValidatedImprovementEvidence = false }).ToArray() } },
            ownership = new { state = "InsufficientEvidence", ownerId = (string?)null, evidenceReferences = Array.Empty<string>(), reasonCode = "FIXTURE-OWNER-MISSING" }
        };
        var content = Json(new
        {
            provisional,
            publishableCurrent = current,
            categories = new[] { new { id = "SECURITY", provisional, publishableCurrent = current, provisionalWeight = "1", publishableWeight = "1" } },
            objectTypes = new[] { new { id = "SyntheticControl", provisional, publishableCurrent = current } },
            modules = new[] { new { id = "SyntheticSecurity", provisional, publishableCurrent = current } },
            outcomes = Array.Empty<object>(),
            quality = new { plannedUnits = 3, executedUnits = 3, gapUnits = 0, notApplicableUnits = 0, proposedReviewUnits = 1, totalFindingUnits = 1 },
            findings = new[] { finding },
            warnings = new[] { "Synthetic draft, not published.", "One Critical occurrence awaits review." },
            maturity,
            reviewHistory = new[] { new { findingId = FindingId, revision = 1, originalTitle = finding.originalTitle, businessContext = finding.businessContext,
                events = new[] { new { eventId = "7d28b421-a838-4146-9837-e5c9ab9d0ed1", actorId = "synthetic-consultant", actorRoles = new[] { "Consultant" },
                    kind = "EditPresentation", recordedAtUtc = "2026-10-02T01:00:00Z", revision = 1, state = "Proposed", reason = (string?)null,
                    text = (string?)null, title = finding.title, businessContext = finding.businessContext } } } },
            healthyControls = new[] { new { objectId = "OBJECT-3", ruleId = "SYN-TRACE", ruleVersion = "synthetic-rule-v1", state = "Pass" },
                new { objectId = "OBJECT-2", ruleId = "SYN-TRACE", ruleVersion = "synthetic-rule-v1", state = "Pass" } },
            limitations = Array.Empty<object>(),
            methodology = new[] { "pilot-health-v1; gaps excluded from default health.", "Maturity is independent of health." },
            unavailableSections = new[] { "Risk acceptance", "Reassessment", "AI", "Tasks", "Publication" }
        });
        return new(source, content);
    }
}
