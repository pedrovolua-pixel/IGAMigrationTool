namespace AssessmentMaturity;

/// <summary>Five fictional mandatory domains; no real One Identity catalog or approval claim.</summary>
public static class SyntheticMaturityFixturePack
{
    public const string CatalogVersion = "synthetic-maturity-catalog-v1";
    public static SyntheticMaturityFrozenFixture Freeze(string baselineId, string sourceAnalysisFixtureDigest)
    {
        if (baselineId is not ("synthetic-analysis-healthy-v1" or "synthetic-analysis-findings-v1" or
            "synthetic-analysis-mixed-v1" or "synthetic-analysis-gaps-v1"))
            throw new ArgumentException("Unknown fixed synthetic maturity baseline.", nameof(baselineId));
        var domains = Enumerable.Range(1, 5).Select(number => new MaturityDomainDefinition($"SYNTHETIC-DOMAIN-{number}",
            $"Fictional capability domain {number}")).ToArray();
        var indicators = new List<MaturityIndicatorEvidence>();
        foreach (var domain in domains)
        {
            foreach (var kind in Enum.GetValues<MaturityIndicatorKind>())
            {
                var baseMet = baselineId == "synthetic-analysis-findings-v1" ||
                    baselineId == "synthetic-analysis-mixed-v1" && Array.IndexOf(domains, domain) < 3;
                var met = kind is MaturityIndicatorKind.Design or MaturityIndicatorKind.Implementation ? baseMet :
                    baselineId == "synthetic-analysis-findings-v1";
                indicators.Add(new(domain.Id, kind, met ? MaturityIndicatorState.Met : MaturityIndicatorState.InsufficientEvidence,
                    met ? new[] { $"fixture-maturity:{baselineId}:{domain.Id}:{kind}" } : Array.Empty<string>(),
                    met && kind == MaturityIndicatorKind.ValidatedImprovement ? new[] { "fixture-assessment:single-v1" } : Array.Empty<string>(),
                    met && kind == MaturityIndicatorKind.ValidatedImprovement,
                    met ? null : "SYNTHETIC-MATURITY-EVIDENCE-MISSING"));
            }
        }
        var ownershipMet = baselineId == "synthetic-analysis-findings-v1";
        var input = new MaturityInput(new(PilotMaturityProjector.AlgorithmVersion, PilotMaturityProjector.InputSchemaVersion,
            baselineId, sourceAnalysisFixtureDigest), new(CatalogVersion, 60, 80, 80, 80, 2,
            PilotMaturityProjector.AuthorityBoundary, domains), indicators,
            new(ownershipMet ? MaturityIndicatorState.Met : MaturityIndicatorState.InsufficientEvidence,
                ownershipMet ? "synthetic-governance-owner" : null,
                ownershipMet ? new[] { "fixture-maturity:governance-owner-v1" } : Array.Empty<string>(),
                ownershipMet ? null : "SYNTHETIC-OWNERSHIP-EVIDENCE-MISSING"));
        var result = PilotMaturityProjector.Project(input);
        if (!result.HasProjection) throw new ArgumentException($"Invalid synthetic maturity freeze: {result.Issue}.", nameof(sourceAnalysisFixtureDigest));
        return new(result.Projection!.Input, result.Projection.InputDigest);
    }
}
