using AssessmentMaturity;

internal static class MaturityFixtures
{
    internal static MaturityInput Ten(int bases = 0, int operation = 0, int improvement = 0, bool owner = false, int distinct = 2)
        => Sets(Enumerable.Range(1, bases).ToHashSet(), Enumerable.Range(1, operation).ToHashSet(), Enumerable.Range(1, improvement).ToHashSet(), owner, distinct);

    internal static MaturityInput Sets(IReadOnlySet<int> bases, IReadOnlySet<int> operation, IReadOnlySet<int> improvement, bool owner = true, int distinct = 2)
    {
        var domains = Enumerable.Range(1, 10).Select(index => new MaturityDomainDefinition($"INDEPENDENT-DOMAIN-{index:D2}", $"Independent fictional domain {index}")).ToArray();
        var indicators = new List<MaturityIndicatorEvidence>();
        foreach (var (domain, index) in domains.Select((domain, index) => (domain, index + 1)))
        {
            foreach (var kind in Enum.GetValues<MaturityIndicatorKind>())
            {
                var met = kind switch
                {
                    MaturityIndicatorKind.Design or MaturityIndicatorKind.Implementation => bases.Contains(index),
                    MaturityIndicatorKind.MeasuredOperation or MaturityIndicatorKind.RegularReview => operation.Contains(index),
                    _ => improvement.Contains(index)
                };
                indicators.Add(new(domain.Id, kind, met ? MaturityIndicatorState.Met : MaturityIndicatorState.NotMet,
                    [$"fixture-independent:{domain.Id}:{kind}:{(met ? "met" : "not-met")}"],
                    kind == MaturityIndicatorKind.ValidatedImprovement && met ? Enumerable.Range(1, distinct).Select(number => $"fixture-assessment:{number}").ToArray() : [],
                    kind == MaturityIndicatorKind.ValidatedImprovement && met));
            }
        }
        return new(new("pilot-maturity-v1", "synthetic-maturity-input-v1", "independent-maturity-baseline", new string('a', 64)),
            new("independent-fictional-catalog-v1", 60, 80, 80, 80, 2, PilotMaturityProjector.AuthorityBoundary, domains),
            indicators.ToArray(), new(owner ? MaturityIndicatorState.Met : MaturityIndicatorState.InsufficientEvidence,
                owner ? "fixture-independent-governance-owner" : null, owner ? ["fixture-independent:governance"] : [],
                owner ? null : "INDEPENDENT-OWNER-INSUFFICIENT"));
    }
}
