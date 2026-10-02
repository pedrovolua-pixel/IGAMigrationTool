using AssessmentMaturity;
using AssessmentRuns;

internal static class DemoMaturityProjection
{
    internal static MaturityResult Project(SyntheticRunSnapshot run)
    {
        var fixture = DemoAnalysisCatalog.FreezeMaturity(run.BaselineCatalogId, run.ProfileCatalogId);
        return fixture.ContentDigest == run.FrozenInputs.MaturityFixtureDigest
            ? PilotMaturityProjector.Project(fixture.Input) : new MaturityResult(MaturityIssue.InvalidInput, null);
    }

    internal static object Detail(SyntheticRunSnapshot run) => Detail(Project(run));

    internal static object Detail(MaturityResult response)
    {
        var data = response.Projection;
        var gates = data is null ? [] : new[]
        {
            (MaturityLevel.Developing, data.Gates.Developing), (MaturityLevel.Defined, data.Gates.Defined),
            (MaturityLevel.Managed, data.Gates.Managed), (MaturityLevel.Optimized, data.Gates.Optimized)
        }.Select(item => new
        {
            level = item.Item1.ToString(),
            item.Item2.MetDomains,
            item.Item2.MandatoryDomains,
            item.Item2.RequiredPercent,
            item.Item2.IsMet
        }).ToArray();
        return new
        {
            status = data is null ? "Unavailable" : "Ready",
            reasonCode = response.Issue?.ToString(),
            level = data?.Level.ToString(),
            algorithmVersion = data?.Input.Versions.AlgorithmVersion,
            catalogVersion = data?.Input.Catalog.Version,
            inputDigest = data?.InputDigest,
            contentDigest = data?.ContentDigest,
            authorityBoundary = data?.Input.Catalog.AuthorityBoundary,
            mandatoryDomains = data?.Counts.MandatoryDomains ?? 0,
            insufficientIndicators = data?.Counts.InsufficientIndicators ?? 0,
            insufficientDomains = data?.Counts.InsufficientDomains ?? 0,
            improvementMissingDistinctAssessments = data?.Counts.ImprovementMissingDistinctAssessments ?? 0,
            governanceOwnershipEvidenced = data?.Gates.GovernanceOwnershipEvidenced ?? false,
            gates,
            domains = data is null ? [] : data.Domains.Select(domain => new
            {
                id = domain.DomainId,
                name = data.Input.Catalog.MandatoryDomains.Single(item => item.Id == domain.DomainId).Name,
                domain.BaseMet,
                domain.OperationAndReviewMet,
                domain.ImprovementMet,
                domain.InsufficientIndicators,
                indicators = data.Input.Indicators.Where(indicator => indicator.DomainId == domain.DomainId).Select(indicator => new
                {
                    kind = indicator.Kind.ToString(),
                    state = indicator.State.ToString(),
                    indicator.ReasonCode,
                    indicator.EvidenceReferences,
                    indicator.AssessmentReferences,
                    indicator.HasValidatedImprovementEvidence
                }).ToArray()
            }).ToArray(),
            ownership = data is null ? null : new
            {
                state = data.Input.GovernanceOwnership.State.ToString(),
                data.Input.GovernanceOwnership.OwnerId,
                data.Input.GovernanceOwnership.EvidenceReferences,
                data.Input.GovernanceOwnership.ReasonCode
            }
        };
    }
}
