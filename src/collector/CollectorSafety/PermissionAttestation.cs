namespace CollectorSafety;

public enum SourceCapability
{
    MinimumRead,
    ExcessReadOnly,
    Write,
    Ddl,
    Ownership,
    Impersonation,
    SecurityAdministration,
    ServerAdministration,
    AgentOrJobAdministration,
    BackupOrRestore,
    Unknown
}

public enum PermissionDecision
{
    Eligible,
    EligibleWithWarning,
    Blocked
}

public sealed record PermissionProbe(
    bool MinimumReadSetSatisfied,
    IReadOnlyCollection<SourceCapability> ObservedCapabilities);

public sealed record PermissionAttestationResult(
    PermissionDecision Decision,
    IReadOnlyList<SourceCapability> BlockingCategories,
    bool ExcessReadOnlyDetected)
{
    public bool BlocksEvidenceQueries => Decision == PermissionDecision.Blocked;
    public bool RequiresWarningAndAudit => Decision == PermissionDecision.EligibleWithWarning;
}

/// <summary>
/// Pure decision step after an approved, trusted SQL permission probe. No
/// source connection or query execution is exposed by this component.
/// </summary>
public static class PermissionAttestation
{
    public static PermissionAttestationResult Evaluate(PermissionProbe? probe)
    {
        if (probe?.ObservedCapabilities is null)
        {
            return new PermissionAttestationResult(PermissionDecision.Blocked,
                [SourceCapability.Unknown], false);
        }

        var observed = probe.ObservedCapabilities;
        var blocking = observed
            .Where(capability => capability is not SourceCapability.MinimumRead and not SourceCapability.ExcessReadOnly)
            .Distinct()
            .OrderBy(capability => capability)
            .ToArray();
        if (!probe.MinimumReadSetSatisfied || !observed.Contains(SourceCapability.MinimumRead))
        {
            blocking = blocking.Append(SourceCapability.Unknown).Distinct().OrderBy(capability => capability).ToArray();
        }

        var excess = observed.Contains(SourceCapability.ExcessReadOnly);
        var decision = blocking.Length > 0
            ? PermissionDecision.Blocked
            : excess ? PermissionDecision.EligibleWithWarning : PermissionDecision.Eligible;
        return new PermissionAttestationResult(decision, blocking, excess);
    }
}
