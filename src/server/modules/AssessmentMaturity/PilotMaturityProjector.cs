using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AssessmentMaturity;

/// <summary>Pure cumulative synthetic maturity, independent of health, review mutations and publication.</summary>
public static class PilotMaturityProjector
{
    public const string AlgorithmVersion = "pilot-maturity-v1";
    public const string InputSchemaVersion = "synthetic-maturity-input-v1";
    public const string AuthorityBoundary = "Fictional synthetic indicators only; no One Identity catalog, promotion, validation or customer authority.";
    private static readonly MaturityIndicatorKind[] Kinds = Enum.GetValues<MaturityIndicatorKind>();

    public static MaturityResult Project(MaturityInput? input)
    {
        if (input is null || input.Versions is null || input.Catalog is null || input.Indicators is null ||
            input.GovernanceOwnership is null || input.Catalog.MandatoryDomains is null)
            return Deny(MaturityIssue.InvalidInput);
        var versions = input.Versions;
        if (!Text(versions.BaselineId) || !Digest(versions.SourceAnalysisFixtureDigest)) return Deny(MaturityIssue.InvalidInput);
        if (versions.AlgorithmVersion != AlgorithmVersion || versions.InputSchemaVersion != InputSchemaVersion)
            return Deny(MaturityIssue.UnknownVersion);
        var catalog = input.Catalog;
        if (!Text(catalog.Version) || catalog.DevelopingPercent != 60 || catalog.DefinedPercent != 80 ||
            catalog.ManagedPercent != 80 || catalog.OptimizedPercent != 80 || catalog.RequiredDistinctAssessmentCount != 2 ||
            catalog.AuthorityBoundary != AuthorityBoundary)
            return Deny(MaturityIssue.InvalidCatalog);
        var domains = catalog.MandatoryDomains.ToArray();
        if (domains.Length == 0 || domains.Any(domain => domain is null || !Text(domain.Id) || !Text(domain.Name)))
            return Deny(MaturityIssue.InvalidCatalog);
        if (domains.Select(domain => domain.Id).Distinct(StringComparer.Ordinal).Count() != domains.Length)
            return Deny(MaturityIssue.DuplicateDomain);
        domains = domains.OrderBy(domain => domain.Id, StringComparer.Ordinal).ToArray();
        var domainIds = domains.Select(domain => domain.Id).ToHashSet(StringComparer.Ordinal);
        var indicators = new Dictionary<(string, MaturityIndicatorKind), MaturityIndicatorEvidence>();
        foreach (var indicator in input.Indicators.ToArray())
        {
            if (indicator is null || !Text(indicator.DomainId) || !Enum.IsDefined(indicator.Kind) || !Enum.IsDefined(indicator.State) ||
                !ValidReferences(indicator.EvidenceReferences) || !ValidReferences(indicator.AssessmentReferences) ||
                indicator.ReasonCode is not null && !Text(indicator.ReasonCode))
                return Deny(MaturityIssue.InvalidEvidence);
            if (!domainIds.Contains(indicator.DomainId)) return Deny(MaturityIssue.UnknownDomain);
            var evidence = Canonical(indicator.EvidenceReferences);
            var assessments = Canonical(indicator.AssessmentReferences);
            if (indicator.State == MaturityIndicatorState.InsufficientEvidence ? !Text(indicator.ReasonCode) : evidence.Count == 0)
                return Deny(MaturityIssue.InvalidEvidence);
            if (indicator.Kind != MaturityIndicatorKind.ValidatedImprovement &&
                (assessments.Count != 0 || indicator.HasValidatedImprovementEvidence)) return Deny(MaturityIssue.InvalidEvidence);
            if (indicator.Kind == MaturityIndicatorKind.ValidatedImprovement &&
                (indicator.State == MaturityIndicatorState.Met && !indicator.HasValidatedImprovementEvidence ||
                indicator.State != MaturityIndicatorState.Met && indicator.HasValidatedImprovementEvidence))
                return Deny(MaturityIssue.InvalidEvidence);
            if (!indicators.TryAdd((indicator.DomainId, indicator.Kind), indicator with
            { EvidenceReferences = evidence, AssessmentReferences = assessments })) return Deny(MaturityIssue.DuplicateIndicator);
        }
        var owner = input.GovernanceOwnership;
        if (!Enum.IsDefined(owner.State) || !ValidReferences(owner.EvidenceReferences) ||
            owner.OwnerId is not null && !Text(owner.OwnerId) || owner.ReasonCode is not null && !Text(owner.ReasonCode))
            return Deny(MaturityIssue.InvalidOwnership);
        var ownerEvidence = Canonical(owner.EvidenceReferences);
        if (owner.State == MaturityIndicatorState.InsufficientEvidence ? !Text(owner.ReasonCode) : ownerEvidence.Count == 0)
            return Deny(MaturityIssue.InvalidOwnership);
        if (owner.State == MaturityIndicatorState.Met && !Text(owner.OwnerId)) return Deny(MaturityIssue.InvalidOwnership);
        owner = owner with { EvidenceReferences = ownerEvidence };

        var frozenIndicators = new List<MaturityIndicatorEvidence>();
        var achievements = new List<MaturityDomainAchievement>();
        foreach (var domain in domains)
        {
            var domainIndicators = new Dictionary<MaturityIndicatorKind, MaturityIndicatorEvidence>();
            foreach (var kind in Kinds)
            {
                var indicator = indicators.GetValueOrDefault((domain.Id, kind)) ?? new(domain.Id, kind,
                    MaturityIndicatorState.InsufficientEvidence, Array.Empty<string>(), Array.Empty<string>(),
                    ReasonCode: "MISSING-SYNTHETIC-INDICATOR");
                // Missing entries are explicit immutable evidence gaps in the frozen normalized input.
                indicator = indicator with { EvidenceReferences = Canonical(indicator.EvidenceReferences), AssessmentReferences = Canonical(indicator.AssessmentReferences) };
                domainIndicators.Add(kind, indicator);
                frozenIndicators.Add(indicator);
            }
            bool Met(MaturityIndicatorKind kind) => domainIndicators[kind].State == MaturityIndicatorState.Met;
            achievements.Add(new(domain.Id, Met(MaturityIndicatorKind.Design) && Met(MaturityIndicatorKind.Implementation),
                Met(MaturityIndicatorKind.MeasuredOperation) && Met(MaturityIndicatorKind.RegularReview),
                Met(MaturityIndicatorKind.ValidatedImprovement) && domainIndicators[MaturityIndicatorKind.ValidatedImprovement].HasValidatedImprovementEvidence &&
                domainIndicators[MaturityIndicatorKind.ValidatedImprovement].AssessmentReferences.Count >= catalog.RequiredDistinctAssessmentCount,
                domainIndicators.Values.Count(indicator => indicator.State == MaturityIndicatorState.InsufficientEvidence)));
        }
        var frozenInput = new MaturityInput(versions, catalog with { MandatoryDomains = Array.AsReadOnly(domains) },
            Array.AsReadOnly(frozenIndicators.ToArray()), owner);
        MaturityThresholdMeasure Threshold(int count, int percent) => new(count, domains.Length, percent, (long)count * 100 >= (long)domains.Length * percent);
        var baseMet = achievements.Count(domain => domain.BaseMet);
        var gates = new MaturityGates(Threshold(baseMet, catalog.DevelopingPercent), Threshold(baseMet, catalog.DefinedPercent),
            Threshold(achievements.Count(domain => domain.OperationAndReviewMet), catalog.ManagedPercent),
            Threshold(achievements.Count(domain => domain.ImprovementMet), catalog.OptimizedPercent), owner.State == MaturityIndicatorState.Met);
        var level = MaturityLevel.Initial;
        if (gates.Developing.IsMet)
        {
            level = MaturityLevel.Developing;
            if (gates.Defined.IsMet && gates.GovernanceOwnershipEvidenced)
            {
                level = MaturityLevel.Defined;
                if (gates.Managed.IsMet)
                {
                    level = MaturityLevel.Managed;
                    if (gates.Optimized.IsMet) level = MaturityLevel.Optimized;
                }
            }
        }
        var counts = new MaturityCounts(domains.Length, frozenIndicators.Count,
            frozenIndicators.Count(indicator => indicator.State == MaturityIndicatorState.Met),
            frozenIndicators.Count(indicator => indicator.State == MaturityIndicatorState.PartiallyMet),
            frozenIndicators.Count(indicator => indicator.State == MaturityIndicatorState.NotMet),
            frozenIndicators.Count(indicator => indicator.State == MaturityIndicatorState.InsufficientEvidence),
            achievements.Count(domain => domain.InsufficientIndicators > 0), frozenIndicators.Count(indicator =>
                indicator.Kind == MaturityIndicatorKind.ValidatedImprovement && indicator.State == MaturityIndicatorState.Met &&
                indicator.AssessmentReferences.Count < catalog.RequiredDistinctAssessmentCount));
        var inputDigest = ComputeInputDigest(frozenInput);
        using var hash = new CanonicalHash();
        hash.Add("synthetic-maturity-projection-v1"); hash.Add(inputDigest); hash.Add(level.ToString());
        foreach (var domain in achievements)
        {
            hash.Add(domain.DomainId); hash.Add(domain.BaseMet); hash.Add(domain.OperationAndReviewMet);
            hash.Add(domain.ImprovementMet); hash.Add(domain.InsufficientIndicators);
        }
        foreach (var threshold in new[] { gates.Developing, gates.Defined, gates.Managed, gates.Optimized })
        {
            hash.Add(threshold.MetDomains); hash.Add(threshold.MandatoryDomains); hash.Add(threshold.RequiredPercent); hash.Add(threshold.IsMet);
        }
        hash.Add(gates.GovernanceOwnershipEvidenced);
        foreach (var value in new[] { counts.MandatoryDomains, counts.Indicators, counts.MetIndicators, counts.PartiallyMetIndicators,
            counts.NotMetIndicators, counts.InsufficientIndicators, counts.InsufficientDomains, counts.ImprovementMissingDistinctAssessments }) hash.Add(value);
        return new(null, new(frozenInput, level, Array.AsReadOnly(achievements.ToArray()), gates, counts, inputDigest, hash.Finish()));
    }

    private static string ComputeInputDigest(MaturityInput input)
    {
        using var hash = new CanonicalHash();
        hash.Add("synthetic-maturity-frozen-input-v1"); hash.Add(input.Versions.AlgorithmVersion); hash.Add(input.Versions.InputSchemaVersion);
        hash.Add(input.Versions.BaselineId); hash.Add(input.Versions.SourceAnalysisFixtureDigest);
        var catalog = input.Catalog;
        hash.Add(catalog.Version); hash.Add(catalog.AuthorityBoundary); hash.Add(catalog.DevelopingPercent); hash.Add(catalog.DefinedPercent);
        hash.Add(catalog.ManagedPercent); hash.Add(catalog.OptimizedPercent); hash.Add(catalog.RequiredDistinctAssessmentCount);
        hash.Add(catalog.MandatoryDomains.Count);
        foreach (var domain in catalog.MandatoryDomains) { hash.Add(domain.Id); hash.Add(domain.Name); }
        hash.Add(input.Indicators.Count);
        foreach (var indicator in input.Indicators)
        {
            hash.Add(indicator.DomainId); hash.Add(indicator.Kind.ToString()); hash.Add(indicator.State.ToString());
            hash.Add(indicator.HasValidatedImprovementEvidence); hash.Add(indicator.ReasonCode);
            hash.Add(indicator.EvidenceReferences.Count); foreach (var reference in indicator.EvidenceReferences) hash.Add(reference);
            hash.Add(indicator.AssessmentReferences.Count); foreach (var reference in indicator.AssessmentReferences) hash.Add(reference);
        }
        hash.Add(input.GovernanceOwnership.State.ToString()); hash.Add(input.GovernanceOwnership.OwnerId);
        hash.Add(input.GovernanceOwnership.ReasonCode); hash.Add(input.GovernanceOwnership.EvidenceReferences.Count);
        foreach (var reference in input.GovernanceOwnership.EvidenceReferences) hash.Add(reference);
        return hash.Finish();
    }

    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidReferences(IReadOnlyCollection<string>? references) => references is not null && references.All(Text);
    private static IReadOnlyList<string> Canonical(IReadOnlyCollection<string> references) =>
        Array.AsReadOnly(references.Distinct(StringComparer.Ordinal).OrderBy(reference => reference, StringComparer.Ordinal).ToArray());
    private static MaturityResult Deny(MaturityIssue issue) => new(issue, null);

    private sealed class CanonicalHash : IDisposable
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public void Add(string? value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value is null ? "-1:" : value.Length.ToString(CultureInfo.InvariantCulture) + ":"));
            if (value is not null) hash.AppendData(Encoding.UTF8.GetBytes(value));
        }
        public void Add(int value) => Add(value.ToString(CultureInfo.InvariantCulture));
        public void Add(bool value) => Add(value ? "true" : "false");
        public string Finish() => Convert.ToHexStringLower(hash.GetHashAndReset());
        public void Dispose() => hash.Dispose();
    }
}
