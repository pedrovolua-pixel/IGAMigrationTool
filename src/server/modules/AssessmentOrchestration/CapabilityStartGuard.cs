using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace AssessmentOrchestration;

public enum CapabilityLifecycleState
{
    Declared,
    FixtureVerified,
    EnvironmentVerified,
    PilotValidated,
    Suspended,
    Unsupported
}

public sealed record ModuleVersion(string Id, string Version);

/// <summary>A trusted registry projection, not an approval or source-access grant.</summary>
public sealed record CapabilitySnapshot(
    string MatrixVersion,
    CapabilityLifecycleState State,
    string ProductBuild,
    string DatabaseSchemaBuild,
    string HotfixSetDigest,
    string SqlServerBuild,
    int CompatibilityLevel,
    IReadOnlyCollection<ModuleVersion> Modules,
    string QueryPackVersion,
    string NormalizationSchemaVersion,
    string RuleCatalogVersion);

/// <summary>Fields already validated by an immutable baseline adapter.</summary>
public sealed record BaselineCompatibility(
    string ProductBuild,
    string DatabaseSchemaBuild,
    string HotfixSetDigest,
    string SqlServerBuild,
    int CompatibilityLevel,
    IReadOnlyCollection<ModuleVersion> Modules,
    string QueryPackVersion,
    string NormalizationSchemaVersion);

public enum CapabilityLockIssue
{
    InvalidInput,
    Suspended,
    Unsupported,
    ExactVersionMismatch,
    ModuleInventoryMismatch
}

public sealed record CapabilityVersionLock(
    string MatrixVersion,
    CapabilityLifecycleState StateAtLock,
    string ProductBuild,
    string DatabaseSchemaBuild,
    string HotfixSetDigest,
    string SqlServerBuild,
    int CompatibilityLevel,
    IReadOnlyList<ModuleVersion> Modules,
    string QueryPackVersion,
    string NormalizationSchemaVersion,
    string RuleCatalogVersion,
    string LockDigest);

public sealed record CapabilityLockResult(CapabilityVersionLock? Lock, CapabilityLockIssue? Issue)
{
    public bool CanProceedToRemainingStartGates => Lock is not null && Issue is null;
}

/// <summary>
/// Checks a trusted registry snapshot against a trusted baseline descriptor and
/// freezes the exact version tuple. Authorization, freshness, baseline integrity,
/// lifecycle promotion, and durable run start remain separate required gates.
/// </summary>
public static class CapabilityStartGuard
{
    public static CapabilityLockResult TryLock(CapabilitySnapshot? capability, BaselineCompatibility? baseline)
    {
        if (capability is null || baseline is null || !Valid(capability, baseline))
        {
            return new(null, CapabilityLockIssue.InvalidInput);
        }

        if (capability.State == CapabilityLifecycleState.Suspended)
        {
            return new(null, CapabilityLockIssue.Suspended);
        }

        if (capability.State == CapabilityLifecycleState.Unsupported)
        {
            return new(null, CapabilityLockIssue.Unsupported);
        }

        if (!Equal(capability.ProductBuild, baseline.ProductBuild) ||
            !Equal(capability.DatabaseSchemaBuild, baseline.DatabaseSchemaBuild) ||
            !Equal(capability.HotfixSetDigest, baseline.HotfixSetDigest) ||
            !Equal(capability.SqlServerBuild, baseline.SqlServerBuild) ||
            capability.CompatibilityLevel != baseline.CompatibilityLevel ||
            !Equal(capability.QueryPackVersion, baseline.QueryPackVersion) ||
            !Equal(capability.NormalizationSchemaVersion, baseline.NormalizationSchemaVersion))
        {
            return new(null, CapabilityLockIssue.ExactVersionMismatch);
        }

        var expected = SortedModules(capability.Modules);
        var actual = SortedModules(baseline.Modules);
        if (expected.Count != actual.Count || !expected.SequenceEqual(actual))
        {
            return new(null, CapabilityLockIssue.ModuleInventoryMismatch);
        }

        var frozen = new ReadOnlyCollection<ModuleVersion>(expected);
        var digest = Digest(capability, expected);
        return new(new CapabilityVersionLock(
            capability.MatrixVersion, capability.State, capability.ProductBuild,
            capability.DatabaseSchemaBuild, capability.HotfixSetDigest,
            capability.SqlServerBuild, capability.CompatibilityLevel, frozen,
            capability.QueryPackVersion, capability.NormalizationSchemaVersion,
            capability.RuleCatalogVersion, digest), null);
    }

    private static bool Valid(CapabilitySnapshot capability, BaselineCompatibility baseline) =>
        Enum.IsDefined(capability.State) &&
        capability.CompatibilityLevel > 0 && baseline.CompatibilityLevel > 0 &&
        ValidText(capability.MatrixVersion) && ValidText(capability.ProductBuild) &&
        ValidText(capability.DatabaseSchemaBuild) && ValidText(capability.HotfixSetDigest) &&
        ValidText(capability.SqlServerBuild) && ValidText(capability.QueryPackVersion) &&
        ValidText(capability.NormalizationSchemaVersion) && ValidText(capability.RuleCatalogVersion) &&
        ValidText(baseline.ProductBuild) && ValidText(baseline.DatabaseSchemaBuild) &&
        ValidText(baseline.HotfixSetDigest) && ValidText(baseline.SqlServerBuild) &&
        ValidText(baseline.QueryPackVersion) && ValidText(baseline.NormalizationSchemaVersion) &&
        ValidModules(capability.Modules) && ValidModules(baseline.Modules);

    private static bool ValidModules(IReadOnlyCollection<ModuleVersion>? modules)
    {
        if (modules is null) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            if (module is null || !ValidText(module.Id) || !ValidText(module.Version) || !ids.Add(module.Id))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Equal(string left, string right) => StringComparer.Ordinal.Equals(left, right);

    private static List<ModuleVersion> SortedModules(IReadOnlyCollection<ModuleVersion> modules) =>
        modules.OrderBy(module => module.Id, StringComparer.Ordinal).ToList();

    private static string Digest(CapabilitySnapshot capability, IReadOnlyList<ModuleVersion> modules)
    {
        var values = new List<string>
        {
            capability.MatrixVersion, capability.State.ToString(), capability.ProductBuild,
            capability.DatabaseSchemaBuild, capability.HotfixSetDigest,
            capability.SqlServerBuild, capability.CompatibilityLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
            capability.QueryPackVersion, capability.NormalizationSchemaVersion, capability.RuleCatalogVersion
        };
        foreach (var module in modules)
        {
            values.Add(module.Id);
            values.Add(module.Version);
        }

        var canonical = new StringBuilder();
        foreach (var value in values)
        {
            canonical.Append(value.Length).Append(':').Append(value);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}
