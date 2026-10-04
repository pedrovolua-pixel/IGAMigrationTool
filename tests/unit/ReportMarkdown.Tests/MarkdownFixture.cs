using System.Text.Json;
using System.Text.Json.Nodes;
using ReportDrafts;

internal static class MarkdownFixture
{
    internal static readonly string FindingId = new('a', 64);
    internal static JsonElement Json(string text) { using var document = JsonDocument.Parse(text); return document.RootElement.Clone(); }
    internal static DraftReportInput Input()
    {
        var a = new string('a', 64);
        var b = new string('b', 64);
        var source = new DraftSourceBinding(new("synthetic-customer", "synthetic-project", "synthetic-environment"),
            Guid.Parse("10000000-0000-4000-8000-000000000001"), 4, "Scoring", a,
            "synthetic-analysis-findings-v1", "synthetic-review-maturity-equal-v1",
            Json($$$"""
            {"profileVersion":"synthetic-profile-v1","desiredOutcomeVersion":null,"scoringAlgorithmVersion":"pilot-health-v1",
             "aiPolicyVersion":"synthetic-ai-disabled-v1","promptVersion":"synthetic-prompt-disabled-v1","modelVersion":"synthetic-model-disabled-v1",
             "applicationVersion":"synthetic-review-maturity-app-v1","workSchemaVersion":"synthetic-run-work-v1",
             "scriptedResultsDigest":"{{{a}}}","analysisFixtureDigest":"{{{a}}}","maturityFixtureDigest":"{{{a}}}"}
            """),
            Json($$$"""
            {"matrixVersion":"fixture-matrix-v1","stateAtLock":"FixtureVerified","productBuild":"fixture-product-v1",
             "databaseSchemaBuild":"fixture-facts-v1","hotfixSetDigest":"fixture-hotfix","sqlServerBuild":"fixture-sql",
             "compatibilityLevel":160,"modules":[{"id":"SYNTHETIC-MODULE","version":"fixture-module-v1"}],
             "queryPackVersion":"fixture-query-v1","normalizationSchemaVersion":"fixture-normalization-v1",
             "ruleCatalogVersion":"synthetic-analysis-catalog-v1","lockDigest":"{{{a}}}"}
            """),
            Json($$$"""
            {"scope":{"customerId":"synthetic-customer","projectId":"synthetic-project","environmentId":"synthetic-environment"},
             "packVersion":"synthetic-analysis-pack-v1","packDigest":"{{{a}}}","presetId":"synthetic-analysis-findings-v1",
             "presetVersion":"synthetic-evidence-v1","evidenceDigest":"{{{a}}}","catalogVersion":"synthetic-analysis-catalog-v1",
             "catalogDigest":"{{{a}}}","profileId":"synthetic-analysis-equal-v1","profileVersion":"synthetic-profile-v1","profileDigest":"{{{a}}}",
             "compatibility":{"sourceProduct":"SYNTHETIC-ONLY","productVersion":"fixture-product-v1","evidenceSchemaVersion":"fixture-facts-v1","ruleLanguageVersion":"count-predicate-v1"}}
            """), a, a, b, a, Guid.Parse("10000000-0000-4000-8000-000000000001"), 4, b, a, a, b);
        return new(source, Json($$$"""
        {
          "provisional":{"raw":"44.2","display":"44.2","status":"Red","eligibleUnits":2},
          "publishableCurrent":{"raw":"78.3","display":"78.3","status":"Yellow","eligibleUnits":1},
          "categories":[{"id":"SECURITY","provisional":{"raw":"44.2","display":"44.2","status":"Red","eligibleUnits":2},"publishableCurrent":{"raw":"78.3","display":"78.3","status":"Yellow","eligibleUnits":1},"provisionalWeight":"1","publishableWeight":"1"}],
          "objectTypes":[],"modules":[],"outcomes":[],
          "quality":{"plannedUnits":3,"executedUnits":2,"gapUnits":1,"notApplicableUnits":0,"proposedReviewUnits":1,"totalFindingUnits":1},
          "findings":[{"id":"{{{a}}}","revision":1,"title":"Current synthetic title","originalTitle":"Original synthetic title","businessContext":"Fictional context",
            "state":"Proposed","initialState":"Proposed","severity":"Critical","confidencePercent":"100","confidenceBand":"Deterministic synthetic evidence","category":"SECURITY","baselineId":"synthetic-analysis-findings-v1","rootCauseKey":"{{{a}}}","reviewRequired":true,"method":"Deterministic","impact":"Fictional impact","likelihood":"Fictional likelihood","limitations":["Synthetic only"],"outcomeIds":[],"validationGuidance":"New evidence required","sources":["fixture-guidance:SYN-GUARD"],
            "rootCause":"Fictional root cause","objectIds":["object-a"],"occurrenceIds":["{{{b}}}"],"originalDigests":["{{{b}}}"],
            "facts":["Explicit fixture count is one"],"inferences":["Fictional inference"],"assumptions":["Fixture assumptions only"],
            "recommendations":["Unverified fictional guidance"],"evidenceReferences":["fixture:object-a:SYN-GUARD"],"ruleId":"SYN-GUARD","ruleVersion":"synthetic-rule-v1"}],
          "warnings":["Critical review pending","Synthetic draft only"],
          "maturity":{"status":"Ready","algorithmVersion":"pilot-maturity-v1","catalogVersion":"synthetic-maturity-catalog-v1","level":"Managed",
            "inputDigest":"{{{a}}}","contentDigest":"{{{b}}}","mandatoryDomains":1,"insufficientIndicators":0,"insufficientDomains":0,"improvementMissingDistinctAssessments":1,
            "reasonCode":null,"authorityBoundary":"Fictional synthetic indicators only","governanceOwnershipEvidenced":true,"ownership":{"state":"Met","ownerId":"fixture-owner","evidenceReferences":["fixture:owner"],"reasonCode":null},"domains":[{"id":"D1","name":"Fictional governance","baseMet":true,"operationAndReviewMet":true,"improvementMet":false,"insufficientIndicators":0,"indicators":[
              {"kind":"Design","state":"Met","reasonCode":null,"evidenceReferences":["fixture:Design"],"assessmentReferences":[],"hasValidatedImprovementEvidence":false},{"kind":"Implementation","state":"Met","reasonCode":null,"evidenceReferences":["fixture:Implementation"],"assessmentReferences":[],"hasValidatedImprovementEvidence":false},{"kind":"MeasuredOperation","state":"Met","reasonCode":null,"evidenceReferences":["fixture:MeasuredOperation"],"assessmentReferences":[],"hasValidatedImprovementEvidence":false},{"kind":"RegularReview","state":"Met","reasonCode":null,"evidenceReferences":["fixture:RegularReview"],"assessmentReferences":[],"hasValidatedImprovementEvidence":false},{"kind":"ValidatedImprovement","state":"Met","reasonCode":null,"evidenceReferences":["fixture:improvement"],"assessmentReferences":["fixture-assessment:1"],"hasValidatedImprovementEvidence":true}]}],
            "gates":[{"level":"Developing","metDomains":1,"mandatoryDomains":1,"requiredPercent":60,"isMet":true},{"level":"Defined","metDomains":1,"mandatoryDomains":1,"requiredPercent":80,"isMet":true},{"level":"Managed","metDomains":1,"mandatoryDomains":1,"requiredPercent":80,"isMet":true},{"level":"Optimized","metDomains":0,"mandatoryDomains":1,"requiredPercent":80,"isMet":false}]},
          "reviewHistory":[{"findingId":"{{{a}}}","revision":1,"originalTitle":"Original synthetic title","businessContext":"Fictional context","events":[{"eventId":"20000000-0000-4000-8000-000000000003","actorId":"synthetic-edit-actor","actorRoles":["Consultant"],"kind":"EditPresentation","recordedAtUtc":"2026-10-02T11:00:00Z","revision":1,"state":"Proposed","reason":null,"text":null,"title":"Current synthetic title","businessContext":"Fictional context"}]}],
          "healthyControls":[{"objectId":"object-pass","ruleId":"SYN-TRACE","ruleVersion":"synthetic-rule-v1","state":"Pass"}],
          "limitations":[{"objectId":"object-gap","ruleId":"SYN-GUARD","state":"InsufficientEvidence","reasonCode":"fixture-facts-missing"}],
          "methodology":["pilot-health-v1; gap units excluded from default health","pilot-maturity-v1; independent evidence thresholds"],
          "unavailableSections":["Risk acceptance is unavailable","Reassessment is unavailable","Publication is unavailable"]
        }
        """));
    }
    internal static DraftReportInput Change(DraftReportInput input, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(input.Content.GetRawText())!;
        edit(node);
        return input with { Content = Json(node.ToJsonString()) };
    }
    internal static DraftReportSnapshot Build(DraftReportInput input)
    {
        var result = DraftSnapshotBuilder.Build(input);
        if (!result.Succeeded) throw new InvalidOperationException($"Independent Markdown fixture invalid: {result.Issue}");
        return result.Snapshot!;
    }
}
