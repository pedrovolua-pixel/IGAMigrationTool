using AssessmentCoverage;

namespace AssessmentOrchestration;

public enum SyntheticBaselinePermission
{
    Eligible,
    EligibleWithWarning,
    Blocked
}

public enum SyntheticApplicability
{
    RequiresAssessment,
    NotApplicable,
    NotAssessed,
    Unsupported
}

/// <summary>Opaque scope supplied by a trusted, already-authorized caller; not an authorization grant.</summary>
public sealed record SyntheticAuthorizedScope(string CustomerId, string ProjectId, string EnvironmentId);

/// <summary>Trusted applicability intent, not an inferred rule result.</summary>
public sealed record SyntheticCategoryDescriptor(
    string CategoryId,
    SyntheticApplicability Applicability,
    string? ReasonCode = null,
    string? ResponsibleStage = null);

/// <summary>Value-free, baseline-local synthetic identity metadata. Native identity may be unavailable.</summary>
public sealed record SyntheticInventoryObject(
    string InventoryId,
    string NativeType,
    string? NativeIdentity,
    string ModuleId,
    IReadOnlyCollection<SyntheticCategoryDescriptor> Categories);

/// <summary>
/// An internal SYNTHETIC fixture, not a production ingestion manifest or wire schema.
/// It contains no evidence values, credentials, source locators or integrity attestation.
/// </summary>
public sealed record SyntheticBaselineInventory(
    string FixtureVersion,
    string BaselineId,
    SyntheticAuthorizedScope Scope,
    SyntheticBaselinePermission Permission,
    BaselineCompatibility Compatibility,
    IReadOnlyCollection<SyntheticInventoryObject> Objects);

public enum SyntheticInventoryIssue
{
    InvalidInput,
    UnknownFixtureVersion,
    WrongScope,
    PermissionBlocked,
    EmptyInventory,
    InvalidObject,
    DuplicateObjectId,
    DuplicateNativeIdentity,
    InvalidModule,
    InvalidCategory,
    ConflictingCategoryMetadata
}

public sealed record SyntheticInventoryPlan(
    string FixtureVersion,
    string BaselineId,
    SyntheticAuthorizedScope Scope,
    SyntheticBaselinePermission Permission,
    CapabilityVersionLock CapabilityLock,
    IReadOnlyList<SyntheticInventoryObject> Objects,
    IReadOnlyList<CoverageKey> ExpectedKeys,
    IReadOnlyList<CoverageItem> DeclaredItems)
{
    public bool HasPermissionWarning => Permission == SyntheticBaselinePermission.EligibleWithWarning;
}

public sealed record SyntheticInventoryPlanResult(
    SyntheticInventoryIssue? InventoryIssue,
    CapabilityLockIssue? CapabilityIssue,
    SyntheticInventoryPlan? Plan)
{
    public bool HasPlan => Plan is not null;
}

/// <summary>
/// Projects a versioned SYNTHETIC inventory and trusted applicability metadata to
/// immutable coverage keys after exact-version locking. Duplicate native identities
/// are refused visibly, never merged or claimed to be a reconciled source baseline.
/// This does not validate a full ingestion manifest, digests, freshness or authorization,
/// generate applicability, emit an audit event, execute rules or transition a durable run.
/// </summary>
public static class SyntheticBaselineInventoryPlanner
{
    public const string FixtureVersion = "synthetic-baseline-inventory-v1";

    public static SyntheticInventoryPlanResult Plan(
        CapabilitySnapshot? capability,
        SyntheticBaselineInventory? baseline,
        SyntheticAuthorizedScope? authorizedScope)
    {
        if (baseline is null || !ValidText(baseline.FixtureVersion) || !ValidText(baseline.BaselineId) ||
            !ValidScope(baseline.Scope) || !ValidScope(authorizedScope) ||
            !Enum.IsDefined(baseline.Permission) || baseline.Objects is null)
        {
            return Refuse(SyntheticInventoryIssue.InvalidInput);
        }

        if (!StringComparer.Ordinal.Equals(baseline.FixtureVersion, FixtureVersion))
        {
            return Refuse(SyntheticInventoryIssue.UnknownFixtureVersion);
        }

        if (baseline.Scope != authorizedScope)
        {
            return Refuse(SyntheticInventoryIssue.WrongScope);
        }

        if (baseline.Permission == SyntheticBaselinePermission.Blocked)
        {
            return Refuse(SyntheticInventoryIssue.PermissionBlocked);
        }

        var capabilityResult = CapabilityStartGuard.TryLock(capability, baseline.Compatibility);
        if (!capabilityResult.CanProceedToRemainingStartGates)
        {
            return new(null, capabilityResult.Issue, null);
        }

        var sourceObjects = baseline.Objects.ToArray();
        if (sourceObjects.Length == 0)
        {
            return Refuse(SyntheticInventoryIssue.EmptyInventory);
        }

        var installedModules = capabilityResult.Lock!.Modules.Select(module => module.Id)
            .ToHashSet(StringComparer.Ordinal);
        var inventoryIds = new HashSet<string>(StringComparer.Ordinal);
        var nativeIdentities = new HashSet<(string ModuleId, string NativeType, string NativeIdentity)>();
        var frozenObjects = new List<SyntheticInventoryObject>(sourceObjects.Length);
        foreach (var item in sourceObjects)
        {
            if (item is null || !ValidText(item.InventoryId) || !ValidText(item.NativeType) ||
                (item.NativeIdentity is not null && !ValidText(item.NativeIdentity)))
            {
                return Refuse(SyntheticInventoryIssue.InvalidObject);
            }

            if (!inventoryIds.Add(item.InventoryId))
            {
                return Refuse(SyntheticInventoryIssue.DuplicateObjectId);
            }

            if (!ValidText(item.ModuleId) || !installedModules.Contains(item.ModuleId))
            {
                return Refuse(SyntheticInventoryIssue.InvalidModule);
            }

            if (item.NativeIdentity is not null &&
                !nativeIdentities.Add((item.ModuleId, item.NativeType, item.NativeIdentity)))
            {
                return Refuse(SyntheticInventoryIssue.DuplicateNativeIdentity);
            }

            if (item.Categories is null)
            {
                return Refuse(SyntheticInventoryIssue.InvalidCategory);
            }

            var categories = new Dictionary<string, SyntheticCategoryDescriptor>(StringComparer.Ordinal);
            foreach (var category in item.Categories.ToArray())
            {
                if (!ValidCategory(category))
                {
                    return Refuse(SyntheticInventoryIssue.InvalidCategory);
                }

                if (categories.TryGetValue(category.CategoryId, out var previous) && previous != category)
                {
                    return Refuse(SyntheticInventoryIssue.ConflictingCategoryMetadata);
                }

                categories[category.CategoryId] = category;
            }

            if (categories.Count == 0)
            {
                return Refuse(SyntheticInventoryIssue.InvalidCategory);
            }

            frozenObjects.Add(item with
            {
                Categories = Array.AsReadOnly(categories.Values
                    .OrderBy(category => category.CategoryId, StringComparer.Ordinal).ToArray())
            });
        }

        var orderedObjects = frozenObjects.OrderBy(item => item.InventoryId, StringComparer.Ordinal).ToArray();
        var keys = new List<CoverageKey>();
        var declarations = new List<CoverageItem>();
        foreach (var item in orderedObjects)
        {
            foreach (var category in item.Categories)
            {
                var key = new CoverageKey(item.InventoryId, category.CategoryId);
                keys.Add(key);
                if (category.Applicability != SyntheticApplicability.RequiresAssessment)
                {
                    declarations.Add(new CoverageItem(key, DeclaredState(category.Applicability),
                        category.ReasonCode, category.ResponsibleStage));
                }
            }
        }

        return new(null, null, new SyntheticInventoryPlan(
            FixtureVersion, baseline.BaselineId, baseline.Scope, baseline.Permission,
            capabilityResult.Lock, Array.AsReadOnly(orderedObjects),
            Array.AsReadOnly(keys.ToArray()), Array.AsReadOnly(declarations.ToArray())));
    }

    private static SyntheticInventoryPlanResult Refuse(SyntheticInventoryIssue issue) => new(issue, null, null);

    private static bool ValidScope(SyntheticAuthorizedScope? scope) => scope is not null &&
        ValidText(scope.CustomerId) && ValidText(scope.ProjectId) && ValidText(scope.EnvironmentId);

    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool ValidCategory(SyntheticCategoryDescriptor? category)
    {
        if (category is null || !ValidText(category.CategoryId) || !Enum.IsDefined(category.Applicability))
        {
            return false;
        }

        return category.Applicability == SyntheticApplicability.RequiresAssessment
            ? category.ReasonCode is null && category.ResponsibleStage is null
            : ValidText(category.ReasonCode) && ValidText(category.ResponsibleStage);
    }

    private static CoverageState DeclaredState(SyntheticApplicability applicability) => applicability switch
    {
        SyntheticApplicability.NotApplicable => CoverageState.NotApplicable,
        SyntheticApplicability.NotAssessed => CoverageState.NotAssessed,
        SyntheticApplicability.Unsupported => CoverageState.Unsupported,
        _ => throw new ArgumentOutOfRangeException(nameof(applicability))
    };
}
