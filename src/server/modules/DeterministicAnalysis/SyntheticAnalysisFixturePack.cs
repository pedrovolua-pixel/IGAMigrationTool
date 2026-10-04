using System.Collections.Immutable;

namespace DeterministicAnalysis;

/// <summary>Fixed value-free engineering fixtures, not One Identity logic or a promoted production catalog.</summary>
public static class SyntheticAnalysisFixturePack
{
    public const string PackVersion = "synthetic-analysis-pack-v1";
    public const string CatalogVersion = "synthetic-analysis-catalog-v1";
    public const string RuleVersion = "synthetic-rule-v1";
    public const string EngineVersion = "synthetic-analysis-engine-v1";
    public static SyntheticAnalysisScope Scope { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
    public static SyntheticCompatibility Compatibility { get; } = new("SYNTHETIC-ONLY", "fixture-product-v1", "fixture-facts-v1", "count-predicate-v1");
    public static ImmutableArray<SyntheticRule> Rules { get; } =
    [
        Rule("SYN-GUARD", "Synthetic guard failure marker", "SECURITY", "SyntheticSecurity", SyntheticFactKind.GuardFailureCount, SyntheticSeverity.Critical),
        Rule("SYN-TRACE", "Synthetic trace failure marker", "SECURITY", "SyntheticSecurity", SyntheticFactKind.TraceFailureCount, SyntheticSeverity.High),
        Rule("SYN-PENDING", "Synthetic pending marker", "OPERATIONS", "SyntheticOperations", SyntheticFactKind.PendingMarkerCount, SyntheticSeverity.Medium),
        Rule("SYN-DUPLICATE", "Synthetic duplicate marker", "OPERATIONS", "SyntheticOperations", SyntheticFactKind.DuplicateMarkerCount, SyntheticSeverity.Low),
        Rule("SYN-UNUSED", "Synthetic unused marker opportunity", "OPERATIONS", "SyntheticOperations", SyntheticFactKind.UnusedMarkerCount, SyntheticSeverity.Informational)
    ];
    public static ImmutableArray<SyntheticAnalysisProfile> Profiles { get; } =
    [
        new("synthetic-analysis-equal-v1", "synthetic-profile-v1", "Synthetic equal category weights", "pilot-health-v1", "ExcludeFromHealth", [new("SECURITY", .5m), new("OPERATIONS", .5m)]),
        new("synthetic-analysis-operations-v1", "synthetic-profile-v1", "Synthetic operations comparison weights", "pilot-health-v1", "ExcludeFromHealth", [new("SECURITY", .25m), new("OPERATIONS", .75m)])
    ];
    public static ImmutableArray<SyntheticAnalysisPreset> Presets { get; } =
    [
        Preset("synthetic-analysis-healthy-v1", "Synthetic passing controls", "healthy"),
        Preset("synthetic-analysis-findings-v1", "Synthetic findings at every severity", "findings"),
        Preset("synthetic-analysis-gaps-v1", "Synthetic missing and excluded evidence", "gaps"),
        Preset("synthetic-analysis-mixed-v1", "Synthetic findings, passing controls and evidence gaps", "mixed")
    ];
    public static string CatalogDigest { get; } = SyntheticCanonicalDigest.Compute(new { CatalogVersion, Rules });
    public static string PackDigest { get; } = SyntheticCanonicalDigest.Compute(new
    {
        PackVersion,
        CatalogVersion,
        EngineVersion,
        Scope,
        Compatibility,
        Rules,
        Presets,
        Profiles,
        ConfidencePercent = 100m,
        ConfidenceBand = "Deterministic synthetic evidence",
        Boundary = "Local synthetic only; no authority, promotion, maturity, AI, mutation or publication"
    });

    public static SyntheticAnalysisPreset GetPreset(string id) => Presets.SingleOrDefault(preset => preset.Id == id)
        ?? throw new ArgumentException("Unknown fixed synthetic analysis preset.", nameof(id));
    public static SyntheticAnalysisProfile GetProfile(string id) => Profiles.SingleOrDefault(profile => profile.Id == id)
        ?? throw new ArgumentException("Unknown fixed synthetic analysis profile.", nameof(id));

    public static SyntheticAnalysisLock Freeze(SyntheticAnalysisScope scope, string presetId, string profileId)
    {
        if (scope != Scope) throw new ArgumentException("Only the fixed synthetic scope can be frozen.", nameof(scope));
        var preset = GetPreset(presetId);
        var profile = GetProfile(profileId);
        return new(scope, PackVersion, PackDigest, preset.Id, preset.Version,
            SyntheticCanonicalDigest.Compute(preset), CatalogVersion, CatalogDigest,
            profile.Id, profile.Version, SyntheticCanonicalDigest.Compute(profile), Compatibility);
    }

    private static SyntheticRule Rule(string id, string title, string category, string module,
        SyntheticFactKind fact, SyntheticSeverity severity) => new(
        id, RuleVersion, title, category, module, "SyntheticControl", fact, SyntheticPredicate.CountGreaterThan,
        0, severity, 1m, severity is not (SyntheticSeverity.Critical or SyntheticSeverity.High),
        "Demonstrate deterministic typed fixture evaluation; no vendor behavior is asserted.",
        "The fixed marker represents synthetic impact only.",
        $"Exact synthetic compatibility, module {module}, SyntheticControl object and known {fact} count.",
        "Count above zero emits a finding; zero passes; absent/conflicting/redacted/excluded/version-incompatible facts emit explained gaps.",
        $"fixture-guidance:{id}:v1", "Fictional control impact; no customer condition or remediation claim.",
        "Deterministically present when the fixed known count exceeds zero; no real-world likelihood estimated.",
        $"Shared synthetic {fact} condition",
        ["Facts are fixed synthetic markers, not collected One Identity evidence.", "Shared rule/root-cause grouping is a fixture premise, not real causal proof."],
        [new("inspect-fixture", "Review the synthetic marker and its typed fact before changing anything.", "Consultant review of the generated original.", "Unverified recommendation; do not execute.", "Keep the prior evidence baseline; compare a new fixture run."),
         new("compare-new-fixture", "Compare new passing synthetic evidence as a separate run.", "A new immutable fixture evidence baseline.", "A disposition alone cannot validate closure.", "Retain the original generated occurrence and comparison history.")],
        ["Check the exact rule/version, fixture baseline and evidence reference.", "Confirm the known marker count is zero in new passing evidence; never overwrite this original."],
        ["Engineering fixture only; no actual One Identity source, approved outcome, maturity or production validation.", "No fix execution or review permission is granted."],
        ["Fictional fixture marker semantics cannot establish a real-world defect."]);

    private static SyntheticAnalysisPreset Preset(string id, string label, string mode)
    {
        var objects = ImmutableArray.CreateBuilder<SyntheticEvidenceObject>();
        foreach (var module in new[] { "SyntheticSecurity", "SyntheticOperations" })
        {
            for (var number = 1; number <= 2; number++)
            {
                var objectId = $"{module}-OBJECT-{number}";
                var missing = mode == "gaps" && number == 1 || mode == "mixed" && module == "SyntheticOperations" && number == 1;
                var marker = mode == "findings" || mode == "mixed" && (module == "SyntheticSecurity" && number == 1 || module == "SyntheticOperations" && number == 2);
                objects.Add(new(objectId, "SyntheticControl", module, $"fixture-evidence:{id}:{objectId}",
                    Compatibility, mode == "gaps" && number == 2,
                    Rules.Where(rule => rule.ModuleId == module).Select(rule => new SyntheticFact(rule.RequiredFact,
                        missing ? SyntheticFactAvailability.Missing : SyntheticFactAvailability.Known,
                        missing ? null : marker ? 1 : 0)).ToImmutableArray()));
            }
        }
        return new(id, "synthetic-evidence-v1", label, objects.ToImmutable());
    }
}
