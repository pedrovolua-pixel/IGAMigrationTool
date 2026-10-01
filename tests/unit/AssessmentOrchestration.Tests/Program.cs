using AssessmentOrchestration;

var modules = new[] { new ModuleVersion("QBM", "10.0.0.287"), new ModuleVersion("QER", "10.0.0.247") };
var capability = new CapabilitySnapshot("matrix-v1", CapabilityLifecycleState.PilotValidated,
    "10.0.0.287", "schema-v1", "hotfix-digest", "16.0.1121.4", 160,
    modules, "pack-v1", "normalized-v1", "rules-v1");
var baseline = new BaselineCompatibility("10.0.0.287", "schema-v1", "hotfix-digest",
    "16.0.1121.4", 160, modules.Reverse().ToArray(), "pack-v1", "normalized-v1");

var passed = 0;
var accepted = CapabilityStartGuard.TryLock(capability, baseline);
Require(accepted.CanProceedToRemainingStartGates && accepted.Lock is not null, "matching exact tuple");
var locked = accepted.Lock ?? throw new Exception("Capability lock was not created.");
Require(locked.Modules[0].Id == "QBM", "canonical module order");
var firstDigest = locked.LockDigest;
modules[0] = new ModuleVersion("QBM", "changed-after-lock");
Require(locked.Modules[0].Version == "10.0.0.287", "frozen module list");
Require(CapabilityStartGuard.TryLock(capability with { Modules = baseline.Modules }, baseline).Lock?.LockDigest == firstDigest,
    "deterministic digest across module order");

Check(CapabilityLockIssue.Suspended, capability with { State = CapabilityLifecycleState.Suspended }, baseline);
Check(CapabilityLockIssue.Unsupported, capability with { State = CapabilityLifecycleState.Unsupported }, baseline);
Check(CapabilityLockIssue.ExactVersionMismatch, capability with { Modules = baseline.Modules },
    baseline with { DatabaseSchemaBuild = "schema-v2" });
Check(CapabilityLockIssue.ExactVersionMismatch, capability with { Modules = baseline.Modules },
    baseline with { QueryPackVersion = "pack-v2" });
Check(CapabilityLockIssue.ModuleInventoryMismatch, capability with { Modules = baseline.Modules },
    baseline with { Modules = [new ModuleVersion("QBM", "10.0.0.287")] });
Check(CapabilityLockIssue.InvalidInput, capability with { Modules = baseline.Modules },
    baseline with { Modules = [new ModuleVersion("QBM", "x"), new ModuleVersion("QBM", "y")] });
Check(CapabilityLockIssue.InvalidInput, capability with { Modules = baseline.Modules, RuleCatalogVersion = " " }, baseline);
Check(CapabilityLockIssue.InvalidInput, capability with { Modules = baseline.Modules, State = (CapabilityLifecycleState)999 }, baseline);
Check(CapabilityLockIssue.InvalidInput, null, baseline);

Console.WriteLine($"{passed} capability lock cases passed.");

void Check(CapabilityLockIssue issue, CapabilitySnapshot? candidate, BaselineCompatibility source)
{
    var result = CapabilityStartGuard.TryLock(candidate, source);
    Require(result.Issue == issue && result.Lock is null && !result.CanProceedToRemainingStartGates, issue.ToString());
}

void Require(bool condition, string name)
{
    if (!condition) throw new Exception($"Failed: {name}");
    passed++;
}
