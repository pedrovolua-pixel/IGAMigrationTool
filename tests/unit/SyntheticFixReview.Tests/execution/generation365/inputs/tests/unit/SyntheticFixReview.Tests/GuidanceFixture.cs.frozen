using System.Collections.Immutable;
using System.Text.Json;
using RecommendationGuidance;

internal static class GuidanceFixture
{
    internal const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string FindingId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    internal const string ExpectedOptionId = "39132c2bfb9e8b3d13a6badd1c9569f989867264039cd258bacfe97f21dc328d";
    internal const string ExpectedContentDigest = "d141e78843b8df1307b98f4d3f80724574c8c7177a6e87fb9c70035b789d6b46";
    internal static JsonElement Json(string text) { using var document = JsonDocument.Parse(text); return document.RootElement.Clone(); }
    internal static GuidanceInput Create()
    {
        var source = JsonSerializer.Deserialize<GuidanceSourceBinding>(SourceJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var finding = new GuidanceFindingInput(FindingId, "SYN-GUARD", "synthetic-rule-v1", "SECURITY", "Critical", "Original fictional title", "Current fictional title", "Synthetic business context", "Proposed", "Confirmed", 1, "Fictional root cause",
            [new(new string('c', 64), "OBJECT-1", "SyntheticControl", "SyntheticSecurity", Digest, "fixture-evidence:OBJECT-1")],
            [new("inspect-fixture", "Inspect the original fixture.", "Consultant review required.", "Unverified; do not execute.", "Retain the prior baseline.")],
            ["Verify exact rule and baseline.", "New evidence is required."], ["fixture-guidance:SYN-GUARD:v1"], ["Fictional fixture premise."], ["No customer validation."]);
        return new(source, [finding]);
    }
    private const string SourceJson = """
{
  "scope": {
    "customerId": "synthetic-customer",
    "projectId": "synthetic-project",
    "environmentId": "synthetic-environment"
  },
  "runId": "7eedaf91-6e83-4e82-8b2b-deefc910b27b",
  "runRevision": 13,
  "runState": "Scoring",
  "runInputDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "baselineId": "synthetic-analysis-findings-v1",
  "profileId": "synthetic-review-maturity-equal-v1",
  "frozenVersions": {
    "profileVersion": "synthetic-profile-v1",
    "desiredOutcomeVersion": null,
    "scoringAlgorithmVersion": "pilot-health-v1",
    "aiPolicyVersion": "synthetic-ai-disabled-v1",
    "promptVersion": "synthetic-prompt-disabled-v1",
    "modelVersion": "synthetic-model-disabled-v1",
    "applicationVersion": "synthetic-review-maturity-app-v1",
    "workSchemaVersion": "synthetic-run-work-v1",
    "scriptedResultsDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "analysisFixtureDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "maturityFixtureDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
  },
  "capabilityLock": {
    "matrixVersion": "synthetic-analysis-matrix-v1",
    "stateAtLock": "FixtureVerified",
    "productBuild": "fixture-product-v1",
    "databaseSchemaBuild": "fixture-facts-v1",
    "hotfixSetDigest": "synthetic-hotfix-digest",
    "sqlServerBuild": "synthetic-sql-build",
    "compatibilityLevel": 160,
    "modules": [
      {
        "id": "SyntheticOperations",
        "version": "synthetic-module-v1"
      },
      {
        "id": "SyntheticSecurity",
        "version": "synthetic-module-v1"
      }
    ],
    "queryPackVersion": "synthetic-query-pack-v1",
    "normalizationSchemaVersion": "synthetic-normalization-v1",
    "ruleCatalogVersion": "synthetic-analysis-catalog-v1",
    "lockDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
  },
  "analysisLock": {
    "scope": {
      "customerId": "synthetic-customer",
      "projectId": "synthetic-project",
      "environmentId": "synthetic-environment"
    },
    "packVersion": "synthetic-analysis-pack-v1",
    "packDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "presetId": "synthetic-analysis-findings-v1",
    "presetVersion": "synthetic-evidence-v1",
    "evidenceDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "catalogVersion": "synthetic-analysis-catalog-v1",
    "catalogDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "profileId": "synthetic-analysis-equal-v1",
    "profileVersion": "synthetic-profile-v1",
    "profileDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "compatibility": {
      "sourceProduct": "SYNTHETIC-ONLY",
      "productVersion": "fixture-product-v1",
      "evidenceSchemaVersion": "fixture-facts-v1",
      "ruleLanguageVersion": "count-predicate-v1"
    }
  },
  "analysisFixtureDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "analysisContentDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "savedCoverageDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "reviewRunId": "7eedaf91-6e83-4e82-8b2b-deefc910b27b",
  "reviewRunRevision": 13,
  "reviewSnapshotDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
}
""";
}
