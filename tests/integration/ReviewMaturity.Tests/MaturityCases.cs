using System.Text.Json;
using AssessmentMaturity;

internal static class MaturityCases
{
    internal static void Run()
    {
        foreach (var (bases, expected) in new[] { (5, MaturityLevel.Initial), (6, MaturityLevel.Developing), (7, MaturityLevel.Developing), (8, MaturityLevel.Defined) })
        {
            var actual = Accept(MaturityFixtures.Ten(bases, owner: true));
            Check.Equal(actual.Level, expected, $"literal {bases}/10 base maturity threshold");
            Check.That(actual.Counts.MandatoryDomains == 10 && actual.Counts.Indicators == 50 && actual.Gates.Defined.MetDomains == bases,
                "all ten declared domains and fifty indicators remain in denominator");
        }
        Check.Equal(Accept(MaturityFixtures.Ten(8, owner: false)).Level, MaturityLevel.Developing, "ownership required at Defined");
        Check.Equal(Accept(MaturityFixtures.Ten(5, 10, 10, true)).Level, MaturityLevel.Initial, "advanced evidence cannot skip Developing");
        Check.Group("RM-MAT-001 inclusive60/80 and cumulative governance prerequisite");

        var full = MaturityFixtures.Ten(6, owner: true);
        foreach (var state in new[] { MaturityIndicatorState.PartiallyMet, MaturityIndicatorState.NotMet, MaturityIndicatorState.InsufficientEvidence })
        {
            var changed = full with
            {
                Indicators = full.Indicators.Select(item => item.DomainId == "INDEPENDENT-DOMAIN-06" && item.Kind == MaturityIndicatorKind.Implementation
                ? item with { State = state, ReasonCode = state == MaturityIndicatorState.InsufficientEvidence ? "INDEPENDENT-INSUFFICIENT" : null } : item).ToArray()
            };
            var actual = Accept(changed);
            Check.Equal(actual.Level, MaturityLevel.Initial, "partial/notmet/insufficient implementation never counts as full met");
            Check.That(actual.Gates.Developing is { MetDomains: 5, MandatoryDomains: 10, IsMet: false }, "nonmet domain cannot shrink denominator");
        }
        var missing = Accept(full with { Indicators = full.Indicators.Where(item => !(item.DomainId == "INDEPENDENT-DOMAIN-06" && item.Kind == MaturityIndicatorKind.Design)).ToArray() });
        Check.That(missing.Level == MaturityLevel.Initial && missing.Counts is { Indicators: 50, InsufficientIndicators: 1, InsufficientDomains: 1 } &&
            missing.Input.Indicators.Single(item => item.DomainId == "INDEPENDENT-DOMAIN-06" && item.Kind == MaturityIndicatorKind.Design).ReasonCode == "MISSING-SYNTHETIC-INDICATOR",
            "missing indicator becomes explicit gap with all domains preserved");
        var allMissing = Accept(full with { Indicators = [] });
        Check.That(allMissing.Level == MaturityLevel.Initial && allMissing.Counts is { MandatoryDomains: 10, Indicators: 50, InsufficientIndicators: 50, InsufficientDomains: 10 },
            "missing every indicator preserves ten domains, fifty gaps and Initial");
        Deny(full with { Catalog = full.Catalog with { MandatoryDomains = [] } }, MaturityIssue.InvalidCatalog, "empty catalog yields no maturity projection");
        Check.Group("RM-MAT-002 partial/missing/insufficient denominator and disclosure");

        Check.Equal(Accept(MaturityFixtures.Ten(8, 7, owner: true)).Level, MaturityLevel.Defined, "seven operation/review domains insufficient for Managed");
        Check.Equal(Accept(MaturityFixtures.Ten(8, 8, owner: true)).Level, MaturityLevel.Managed, "exact8/10 operation/review reaches Managed");
        var operationUnpaired = MaturityFixtures.Ten(8, 8, owner: true);
        operationUnpaired = operationUnpaired with
        {
            Indicators = operationUnpaired.Indicators.Select(item => item.DomainId == "INDEPENDENT-DOMAIN-08" && item.Kind == MaturityIndicatorKind.RegularReview
            ? item with { State = MaturityIndicatorState.NotMet } : item).ToArray()
        };
        Check.Equal(Accept(operationUnpaired).Level, MaturityLevel.Defined, "measurement alone does not satisfy same-domain operation/review pair");
        Check.Equal(Accept(MaturityFixtures.Ten(8, 8, 7, true)).Level, MaturityLevel.Managed, "seven improvements insufficient for Optimized");
        Check.Equal(Accept(MaturityFixtures.Ten(8, 8, 8, true)).Level, MaturityLevel.Optimized, "eight two-assessment validated improvements reach Optimized");
        var oneAssessment = Accept(MaturityFixtures.Ten(8, 8, 8, true, 1));
        Check.That(oneAssessment.Level == MaturityLevel.Managed && oneAssessment.Counts.ImprovementMissingDistinctAssessments == 8 && oneAssessment.Gates.Optimized.MetDomains == 0,
            "one assessment does not become validated cross-assessment improvement");
        var duplicate = MaturityFixtures.Ten(8, 8, 8, true, 1);
        duplicate = duplicate with
        {
            Indicators = duplicate.Indicators.Select(item => item.Kind == MaturityIndicatorKind.ValidatedImprovement && item.State == MaturityIndicatorState.Met
            ? item with { AssessmentReferences = ["fixture-assessment:1", "fixture-assessment:1"] } : item).ToArray()
        };
        Check.That(Accept(duplicate).Level == MaturityLevel.Managed && Accept(duplicate).Counts.ImprovementMissingDistinctAssessments == 8,
            "duplicate references do not fabricate two distinct assessments");
        Check.Equal(Accept(MaturityFixtures.Ten(8, 7, 10, true)).Level, MaturityLevel.Defined, "Optimized never skips Managed");
        Check.Equal(Accept(MaturityFixtures.Ten(8, 10, 10, false)).Level, MaturityLevel.Developing, "advanced indicators never bypass evidenced ownership");
        var global = Accept(MaturityFixtures.Sets(Enumerable.Range(1, 8).ToHashSet(), Enumerable.Range(3, 8).ToHashSet(), new[] { 1, 2, 3, 4, 5, 6, 9, 10 }.ToHashSet()));
        Check.That(global.Level == MaturityLevel.Optimized && global.Gates is { Defined.MetDomains: 8, Managed.MetDomains: 8, Optimized.MetDomains: 8 },
            "global cumulative prerequisites do not invent cross-level domain intersections");
        Check.Group("RM-MAT-003/004/006 operation pairs, distinct validation and global prerequisites");

        Deny(null, MaturityIssue.InvalidInput, "null input");
        Deny(full with { Versions = full.Versions with { AlgorithmVersion = "unknown" } }, MaturityIssue.UnknownVersion, "unknown algorithm");
        Deny(full with { Versions = full.Versions with { InputSchemaVersion = "unknown" } }, MaturityIssue.UnknownVersion, "unknown schema");
        Deny(full with { Versions = full.Versions with { SourceAnalysisFixtureDigest = "not-digest" } }, MaturityIssue.InvalidInput, "invalid source lock digest");
        Deny(full with { Catalog = full.Catalog with { DevelopingPercent = 59 } }, MaturityIssue.InvalidCatalog, "changed fixed threshold");
        Deny(full with { Catalog = full.Catalog with { MandatoryDomains = [.. full.Catalog.MandatoryDomains, full.Catalog.MandatoryDomains.First()] } }, MaturityIssue.DuplicateDomain, "duplicate catalog domain");
        Deny(full with { Indicators = [.. full.Indicators, full.Indicators.First()] }, MaturityIssue.DuplicateIndicator, "duplicate indicator");
        Deny(full with { Indicators = [full.Indicators.First() with { DomainId = "unknown-domain" }] }, MaturityIssue.UnknownDomain, "unknown domain indicator");
        Deny(full with { Indicators = [full.Indicators.First() with { EvidenceReferences = [] }] }, MaturityIssue.InvalidEvidence, "Met needs explicit evidence");
        Deny(full with { Indicators = [full.Indicators.First() with { AssessmentReferences = ["fixture-assessment:1"] }] }, MaturityIssue.InvalidEvidence, "assessment refs cannot authorize ordinary indicator");
        Deny(full with { GovernanceOwnership = full.GovernanceOwnership with { OwnerId = null } }, MaturityIssue.InvalidOwnership, "Met governance needs owner");
        var improvementUnvalidated = MaturityFixtures.Ten(8, 8, 8, true);
        Deny(improvementUnvalidated with
        {
            Indicators = improvementUnvalidated.Indicators.Select(item => item.Kind == MaturityIndicatorKind.ValidatedImprovement && item.State == MaturityIndicatorState.Met
            ? item with { HasValidatedImprovementEvidence = false } : item).ToArray()
        }, MaturityIssue.InvalidEvidence, "Met improvement needs validation premise");
        var projected = Accept(full);
        var encoded = JsonSerializer.Serialize(projected);
        var reordered = Accept(full with { Catalog = full.Catalog with { MandatoryDomains = full.Catalog.MandatoryDomains.Reverse().ToArray() }, Indicators = full.Indicators.Reverse().ToArray() });
        Check.That(reordered.ContentDigest == projected.ContentDigest && JsonSerializer.Serialize(reordered) == encoded, "order-independent canonical evidence projection");
        ((MaturityIndicatorEvidence[])full.Indicators)[0] = full.Indicators.First() with { State = MaturityIndicatorState.NotMet };
        Check.Equal(JsonSerializer.Serialize(projected), encoded, "caller mutation cannot alter original frozen maturity");
        Check.That(!typeof(MaturityInput).GetProperties().Any(property => property.Name.Contains("Health", StringComparison.OrdinalIgnoreCase)), "maturity has no health input coupling");
        Check.Group("RM-MAT-005 adversarial versions/evidence/catalog/ownership and immutable health-independent input");

        foreach (var (baseline, level, missingCount, validations) in new[]
        {
            ("synthetic-analysis-healthy-v1", MaturityLevel.Initial, 25, 0),
            ("synthetic-analysis-findings-v1", MaturityLevel.Managed, 0, 5),
            ("synthetic-analysis-mixed-v1", MaturityLevel.Developing, 19, 0),
            ("synthetic-analysis-gaps-v1", MaturityLevel.Initial, 25, 0)
        })
        {
            var frozen = SyntheticMaturityFixturePack.Freeze(baseline, new string('b', 64));
            var actual = Accept(frozen.Input);
            Check.That(actual.Level == level && actual.Counts.MandatoryDomains == 5 && actual.Counts.Indicators == 25 &&
                actual.Counts.InsufficientIndicators == missingCount && actual.Counts.ImprovementMissingDistinctAssessments == validations && frozen.ContentDigest == actual.InputDigest,
                $"literal {baseline} fixture level/counts/immutable lock");
        }
        Check.Group("RM-MAT-007 fixed baseline fixtures are independent of health");
    }
    internal static MaturityProjection Accept(MaturityInput input)
    {
        var result = PilotMaturityProjector.Project(input);
        Check.That(result.HasProjection && result.Projection is not null, $"maturity accepted, actual {result.Issue}");
        return result.Projection!;
    }
    private static void Deny(MaturityInput? input, MaturityIssue expected, string description)
    {
        var result = PilotMaturityProjector.Project(input);
        Check.That(result.Issue == expected && result.Projection is null && !result.HasProjection, $"{description}; expected {expected}, actual {result.Issue}");
    }
}
