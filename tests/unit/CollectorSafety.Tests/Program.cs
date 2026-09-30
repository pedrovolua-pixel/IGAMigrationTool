using CollectorSafety;

var count = 0;
Check("minimum read", PermissionDecision.Eligible, [], false, false,
    new PermissionProbe(true, [SourceCapability.MinimumRead]));
Check("excess read-only", PermissionDecision.EligibleWithWarning, [], true, false,
    new PermissionProbe(true, [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly]));
Check("minimum read not satisfied", PermissionDecision.Blocked, [SourceCapability.Unknown], false, true,
    new PermissionProbe(false, [SourceCapability.MinimumRead]));
Check("missing minimum read proof", PermissionDecision.Blocked, [SourceCapability.Unknown], false, true,
    new PermissionProbe(true, []));
Check("missing probe", PermissionDecision.Blocked, [SourceCapability.Unknown], false, true, null);
Check("missing capability set", PermissionDecision.Blocked, [SourceCapability.Unknown], false, true,
    new PermissionProbe(true, null!));
Check("write overrides warning", PermissionDecision.Blocked, [SourceCapability.Write], true, true,
    new PermissionProbe(true, [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly, SourceCapability.Write]));

foreach (var capability in new[]
         {
             SourceCapability.Ddl,
             SourceCapability.Ownership,
             SourceCapability.Impersonation,
             SourceCapability.SecurityAdministration,
             SourceCapability.ServerAdministration,
             SourceCapability.AgentOrJobAdministration,
             SourceCapability.BackupOrRestore,
             SourceCapability.Unknown
         })
{
    Check($"blocking {capability}", PermissionDecision.Blocked, [capability], false, true,
        new PermissionProbe(true, [SourceCapability.MinimumRead, capability]));
}

Check("duplicate blocking category is deduplicated", PermissionDecision.Blocked,
    [SourceCapability.Write], false, true,
    new PermissionProbe(true, [SourceCapability.MinimumRead, SourceCapability.Write, SourceCapability.Write]));
Check("unrecognized capability fails closed", PermissionDecision.Blocked,
    [(SourceCapability)999], false, true,
    new PermissionProbe(true, [SourceCapability.MinimumRead, (SourceCapability)999]));

Console.WriteLine($"{count} collector permission-attestation cases passed.");
Console.WriteLine($"{PageCheckpointChecks.Run()} collector page-checkpoint cases passed.");
Console.WriteLine($"{QueryApplicabilityChecks.Run()} collector query-applicability cases passed.");
Console.WriteLine($"{PageBudgetChecks.Run()} collector page-budget cases passed.");
Console.WriteLine($"{StaticSqlShapeChecks.Run()} collector static-SQL shape cases passed.");
Console.WriteLine($"{await CollectorRunGateChecks.RunAsync()} collector run-gate cases passed.");
Console.WriteLine($"{OfflinePayloadEncryptionChecks.Run()} collector offline-encryption cases passed.");
Console.WriteLine($"{FieldMinimizerChecks.Run()} collector field-minimization cases passed.");

void Check(string name, PermissionDecision expectedDecision,
    SourceCapability[] expectedBlocking, bool expectedExcess, bool expectedBlocksQueries,
    PermissionProbe? probe)
{
    var actual = PermissionAttestation.Evaluate(probe);
    if (actual.Decision != expectedDecision ||
        actual.ExcessReadOnlyDetected != expectedExcess ||
        actual.BlocksEvidenceQueries != expectedBlocksQueries ||
        actual.RequiresWarningAndAudit != (expectedDecision == PermissionDecision.EligibleWithWarning) ||
        !actual.BlockingCategories.SequenceEqual(expectedBlocking))
    {
        throw new Exception($"{name}: unexpected permission decision.");
    }

    count++;
}
