using System.Collections.Immutable;
using System.Text.Json;
using AssessmentCoverage;
using SyntheticAiExecution;

public static class AiFixtures
{
    public static string Digest => new('a', 64);
    public static AiAuthority Worker => new("synthetic-worker", AiScope.Fixed, [AiRole.Worker], [AiAction.Read, AiAction.Dispatch, AiAction.Reconcile], ["SECURITY", "OPERATIONS"]);
    public static AiAuthority Consultant => new("synthetic-consultant", AiScope.Fixed, [AiRole.Consultant], [AiAction.Read, AiAction.Override], ["SECURITY", "OPERATIONS"]);
    public static (AiRunLock Locked, ImmutableArray<AiWork> Works) Create(Guid runId, string epoch, AiScenario scenario = AiScenario.Benign, int count = 1)
    {
        var packet = JsonSerializer.Serialize(new
        {
            schemaVersion = "synthetic-ai-fixture-input-v1",
            source = new { customerId = "synthetic-customer", projectId = "synthetic-project", environmentId = "synthetic-environment", runId = runId.ToString("D"), baselineDigest = Digest, profileDigest = Digest, normalizationVersion = "fixture-normalization-v1", redactionVersion = "fixture-redaction-v1", promptVersion = "fixture-prompt-v1" },
            evidence = new[] { new { evidenceId = "ev-" + new string('1', 64), classification = "NormalizedRedactedConfiguration", redactionCount = 0, configurationKey = "scheduleEnabled", configurationValue = "false" }, new { evidenceId = "ev-" + new string('2', 64), classification = "NormalizedRedactedConfiguration", redactionCount = 0, configurationKey = "retryPolicy", configurationValue = "three attempts" } },
            ruleIds = new[] { "fixture-rule-schedule-v1", "fixture-rule-conflict-v1" }
        });
        var works = Enumerable.Range(0, count).Select(i => new AiWork("work-" + i, "OPERATIONS", packet,
            [new("proposal-01", new("synthetic-ai-schedule-" + i, "configuration"), "SyntheticConfiguration", "SyntheticOperations", "fixture-rule-schedule-v1", "synthetic-rule-v1", "Fictional AI schedule", AiSeverity.High, "Fictional impact", "Fictional likelihood", "Fictional schedule cause", ["ev-" + new string('1', 64)]),
             new("proposal-02", new("synthetic-ai-retry-" + i, "configuration"), "SyntheticConfiguration", "SyntheticOperations", "fixture-rule-conflict-v1", "synthetic-rule-v1", "Fictional AI retry", AiSeverity.Medium, "Fictional impact", "Fictional likelihood", "Fictional retry cause", ["ev-" + new string('2', 64)])], scenario)).ToImmutableArray();
        var locked = new AiRunLock(runId, AiScope.Fixed, "synthetic-phase1b-combined-v1", "synthetic-phase1b-app-v1", Digest, Digest, Digest, Digest,
            AiExecutionPolicy.PolicyVersion, AiExecutionPolicy.ProviderVersion, AiExecutionPolicy.PromptVersion, AiExecutionPolicy.FixtureVersion,
            AiExecutionPolicy.MappingDigest(works), epoch, "synthetic-consultant");
        return (locked, works);
    }
}
