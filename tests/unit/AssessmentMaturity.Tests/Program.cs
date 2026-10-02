using AssessmentMaturity;

var checks = 0;
var allKinds = Enum.GetValues<MaturityIndicatorKind>();
const string sourceDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
foreach (var (baseline, level, missing, baseMet, missingAssessments) in new[]
{
    ("healthy", MaturityLevel.Initial, 25, 0, 0),
    ("findings", MaturityLevel.Managed, 0, 5, 5),
    ("mixed", MaturityLevel.Developing, 19, 3, 0),
    ("gaps", MaturityLevel.Initial, 25, 0, 0)
})
{
    var fixture = SyntheticMaturityFixturePack.Freeze($"synthetic-analysis-{baseline}-v1", sourceDigest);
    var projection = Project(fixture.Input);
    Check($"{baseline} independent maturity", projection.Level == level && projection.Counts.InsufficientIndicators == missing &&
        projection.Gates.Developing.MetDomains == baseMet && projection.Counts.ImprovementMissingDistinctAssessments == missingAssessments);
    Check($"{baseline} all mandatory domains retained", projection.Counts.MandatoryDomains == 5 && projection.Counts.Indicators == 25);
    Check($"{baseline} frozen full digest", fixture.ContentDigest == projection.InputDigest &&
        SyntheticMaturityFixturePack.Freeze($"synthetic-analysis-{baseline}-v1", sourceDigest).ContentDigest == fixture.ContentDigest);
    Check($"{baseline} missing prominently referenced", projection.Input.Indicators.Where(item => item.State == MaturityIndicatorState.InsufficientEvidence)
        .All(item => item.ReasonCode == "SYNTHETIC-MATURITY-EVIDENCE-MISSING"));
}

foreach (var (count, expected) in new[] { (0, MaturityLevel.Initial), (5, MaturityLevel.Initial),
    (6, MaturityLevel.Developing), (7, MaturityLevel.Developing), (8, MaturityLevel.Defined), (10, MaturityLevel.Defined) })
    Check($"base threshold {count}/10", Project(Input(10, count)).Level == expected);
Check("three/five exact Developing", Project(Input(5, 3)).Level == MaturityLevel.Developing);
Check("four/five exact Defined", Project(Input(5, 4)).Level == MaturityLevel.Defined);
Check("missing ownership cannot Defined", Project(Input(10, 10) with
{
    GovernanceOwnership = new(MaturityIndicatorState.InsufficientEvidence,
    null, [], "NO-OWNER-EVIDENCE")
}).Level == MaturityLevel.Developing);
Check("partial ownership cannot Defined", Project(Input(10, 10) with
{
    GovernanceOwnership = new(MaturityIndicatorState.PartiallyMet,
    "fixture-owner", ["fixture-owner:partial"])
}).Level == MaturityLevel.Developing);
Check("negative ownership cannot Defined", Project(Input(10, 10) with
{
    GovernanceOwnership = new(MaturityIndicatorState.NotMet,
    null, ["fixture-owner:not-met"])
}).Level == MaturityLevel.Developing);
Check("seven/ten operation remains Defined", Project(Input(10, 8, 7)).Level == MaturityLevel.Defined);
Check("eight/ten operation Managed", Project(Input(10, 8, 8)).Level == MaturityLevel.Managed);
Check("seven/ten improvement remains Managed", Project(Input(10, 8, 8, 7)).Level == MaturityLevel.Managed);
Check("eight/ten improvement Optimized", Project(Input(10, 8, 8, 8)).Level == MaturityLevel.Optimized);
Check("operation cannot skip Developing", Project(Input(10, 5, 10, 10)).Level == MaturityLevel.Initial);
Check("operation cannot skip Defined", Project(Input(10, 6, 10, 10)).Level == MaturityLevel.Developing);
Check("improvement cannot skip Managed", Project(Input(10, 8, 7, 10)).Level == MaturityLevel.Defined);
var fullyMet = Input(10, 10, 10, 10);
var disjoint = fullyMet with
{
    Indicators = fullyMet.Indicators.Select(item => item with
    {
        State = (item.Kind is MaturityIndicatorKind.Design or MaturityIndicatorKind.Implementation ? Number(item.DomainId) <= 8 :
            item.Kind is MaturityIndicatorKind.MeasuredOperation or MaturityIndicatorKind.RegularReview ? Number(item.DomainId) >= 3 :
            Number(item.DomainId) <= 6 || Number(item.DomainId) >= 9) ? MaturityIndicatorState.Met : MaturityIndicatorState.NotMet,
        HasValidatedImprovementEvidence = item.Kind == MaturityIndicatorKind.ValidatedImprovement && (Number(item.DomainId) <= 6 || Number(item.DomainId) >= 9)
    }).ToArray()
};
Check("global gates permit disjoint eighty-percent domain sets", Project(disjoint).Level == MaturityLevel.Optimized);
var unpairedBase = fullyMet with
{
    Indicators = fullyMet.Indicators.Select(item => item with
    {
        State = item.Kind == MaturityIndicatorKind.Design && Number(item.DomainId) > 5 ||
        item.Kind == MaturityIndicatorKind.Implementation && Number(item.DomainId) <= 5 ? MaturityIndicatorState.NotMet : item.State
    }).ToArray()
};
Check("base design and implementation must pair", Project(unpairedBase).Level == MaturityLevel.Initial);
var unpairedOperation = fullyMet with
{
    Indicators = fullyMet.Indicators.Select(item => item with
    {
        State = item.Kind == MaturityIndicatorKind.MeasuredOperation && Number(item.DomainId) > 5 ||
        item.Kind == MaturityIndicatorKind.RegularReview && Number(item.DomainId) <= 5 ? MaturityIndicatorState.NotMet : item.State
    }).ToArray()
};
Check("operation and review must pair", Project(unpairedOperation).Level == MaturityLevel.Defined);
foreach (var references in new string[][] { [], ["assessment:one"], ["assessment:one", "assessment:one"] })
{
    var single = fullyMet with
    {
        Indicators = fullyMet.Indicators.Select(item => item.Kind == MaturityIndicatorKind.ValidatedImprovement
        ? item with { AssessmentReferences = references } : item).ToArray()
    };
    var projection = Project(single);
    Check("zero/one/duplicate assessments cannot optimize", projection.Level == MaturityLevel.Managed &&
        projection.Counts.ImprovementMissingDistinctAssessments == 10 && projection.Gates.Optimized.MetDomains == 0);
}
foreach (var state in new[] { MaturityIndicatorState.PartiallyMet, MaturityIndicatorState.NotMet, MaturityIndicatorState.InsufficientEvidence })
{
    var partial = fullyMet with
    {
        Indicators = fullyMet.Indicators.Select(item => item with
        { State = state, HasValidatedImprovementEvidence = false, ReasonCode = state == MaturityIndicatorState.InsufficientEvidence ? "FIXTURE-INSUFFICIENT" : null }).ToArray()
    };
    Check($"{state} never counts met", Project(partial).Level == MaturityLevel.Initial && Project(partial).Gates.Developing.MetDomains == 0);
}
var allMissing = Project(fullyMet with { Indicators = [] });
Check("all missing indicators are explicit insufficient", allMissing.Level == MaturityLevel.Initial && allMissing.Counts.InsufficientIndicators == 50 &&
    allMissing.Counts.InsufficientDomains == 10 && allMissing.Input.Indicators.All(item => item.ReasonCode == "MISSING-SYNTHETIC-INDICATOR"));
var missingOne = Project(Input(5, 3) with { Indicators = Input(5, 3).Indicators.Where(item => item.DomainId != "DOMAIN-3").ToArray() });
Check("missing base domain remains denominator", missingOne.Level == MaturityLevel.Initial && missingOne.Gates.Developing.MandatoryDomains == 5 &&
    missingOne.Gates.Developing.MetDomains == 2);

var valid = Input(5, 5, 5, 5);
var first = valid.Indicators.First();
var improvement = valid.Indicators.First(item => item.Kind == MaturityIndicatorKind.ValidatedImprovement);
var negatives = new (string Name, MaturityInput? Input, MaturityIssue Issue)[]
{
    ("null", null, MaturityIssue.InvalidInput),
    ("missing versions", valid with { Versions = null! }, MaturityIssue.InvalidInput),
    ("missing catalog", valid with { Catalog = null! }, MaturityIssue.InvalidInput),
    ("missing indicators", valid with { Indicators = null! }, MaturityIssue.InvalidInput),
    ("missing ownership", valid with { GovernanceOwnership = null! }, MaturityIssue.InvalidInput),
    ("unknown algorithm", valid with { Versions = valid.Versions with { AlgorithmVersion = "pilot-maturity-v2" } }, MaturityIssue.UnknownVersion),
    ("unknown schema", valid with { Versions = valid.Versions with { InputSchemaVersion = "other" } }, MaturityIssue.UnknownVersion),
    ("empty baseline", valid with { Versions = valid.Versions with { BaselineId = " " } }, MaturityIssue.InvalidInput),
    ("bad source digest", valid with { Versions = valid.Versions with { SourceAnalysisFixtureDigest = "not-a-digest" } }, MaturityIssue.InvalidInput),
    ("uppercase source digest", valid with { Versions = valid.Versions with { SourceAnalysisFixtureDigest = sourceDigest.ToUpperInvariant() } }, MaturityIssue.InvalidInput),
    ("empty catalog version", valid with { Catalog = valid.Catalog with { Version = " " } }, MaturityIssue.InvalidCatalog),
    ("wrong threshold", valid with { Catalog = valid.Catalog with { DevelopingPercent = 59 } }, MaturityIssue.InvalidCatalog),
    ("wrong eighty threshold", valid with { Catalog = valid.Catalog with { DefinedPercent = 81 } }, MaturityIssue.InvalidCatalog),
    ("wrong measured threshold", valid with { Catalog = valid.Catalog with { ManagedPercent = 79 } }, MaturityIssue.InvalidCatalog),
    ("wrong improvement threshold", valid with { Catalog = valid.Catalog with { OptimizedPercent = 79 } }, MaturityIssue.InvalidCatalog),
    ("wrong assessment minimum", valid with { Catalog = valid.Catalog with { RequiredDistinctAssessmentCount = 1 } }, MaturityIssue.InvalidCatalog),
    ("authority boundary changed", valid with { Catalog = valid.Catalog with { AuthorityBoundary = "One Identity approved" } }, MaturityIssue.InvalidCatalog),
    ("empty catalog", valid with { Catalog = valid.Catalog with { MandatoryDomains = [] } }, MaturityIssue.InvalidCatalog),
    ("null domains", valid with { Catalog = valid.Catalog with { MandatoryDomains = null! } }, MaturityIssue.InvalidInput),
    ("null domain", valid with { Catalog = valid.Catalog with { MandatoryDomains = [null!] } }, MaturityIssue.InvalidCatalog),
    ("malformed domain", valid with { Catalog = valid.Catalog with { MandatoryDomains = [new(" ", "fictional")] } }, MaturityIssue.InvalidCatalog),
    ("duplicate domains", valid with { Catalog = valid.Catalog with { MandatoryDomains = valid.Catalog.MandatoryDomains.Concat([valid.Catalog.MandatoryDomains.First()]).ToArray() } }, MaturityIssue.DuplicateDomain),
    ("unknown domain evidence", Replace(valid, first, first with { DomainId = "unknown" }), MaturityIssue.UnknownDomain),
    ("duplicate indicator", valid with { Indicators = valid.Indicators.Concat([first]).ToArray() }, MaturityIssue.DuplicateIndicator),
    ("null indicator", valid with { Indicators = [null!] }, MaturityIssue.InvalidEvidence),
    ("unknown kind", Replace(valid, first, first with { Kind = (MaturityIndicatorKind)99 }), MaturityIssue.InvalidEvidence),
    ("unknown state", Replace(valid, first, first with { State = (MaturityIndicatorState)99 }), MaturityIssue.InvalidEvidence),
    ("met without evidence", Replace(valid, first, first with { EvidenceReferences = [] }), MaturityIssue.InvalidEvidence),
    ("null evidence", Replace(valid, first, first with { EvidenceReferences = null! }), MaturityIssue.InvalidEvidence),
    ("blank evidence", Replace(valid, first, first with { EvidenceReferences = [" "] }), MaturityIssue.InvalidEvidence),
    ("null assessment references", Replace(valid, improvement, improvement with { AssessmentReferences = null! }), MaturityIssue.InvalidEvidence),
    ("blank assessment", Replace(valid, improvement, improvement with { AssessmentReferences = [" "] }), MaturityIssue.InvalidEvidence),
    ("unexplained insufficient", Replace(valid, first, first with { State = MaturityIndicatorState.InsufficientEvidence, ReasonCode = null }), MaturityIssue.InvalidEvidence),
    ("malformed reason", Replace(valid, first, first with { ReasonCode = " " }), MaturityIssue.InvalidEvidence),
    ("unvalidated met improvement", Replace(valid, improvement, improvement with { HasValidatedImprovementEvidence = false }), MaturityIssue.InvalidEvidence),
    ("partial is not validated met", Replace(valid, improvement, improvement with { State = MaturityIndicatorState.PartiallyMet }), MaturityIssue.InvalidEvidence),
    ("non-improvement validation flag", Replace(valid, first, first with { HasValidatedImprovementEvidence = true }), MaturityIssue.InvalidEvidence),
    ("non-improvement assessment refs", Replace(valid, first, first with { AssessmentReferences = ["fixture-assessment:x"] }), MaturityIssue.InvalidEvidence),
    ("unknown ownership state", valid with { GovernanceOwnership = valid.GovernanceOwnership with { State = (MaturityIndicatorState)99 } }, MaturityIssue.InvalidOwnership),
    ("ownership without owner", valid with { GovernanceOwnership = valid.GovernanceOwnership with { OwnerId = null } }, MaturityIssue.InvalidOwnership),
    ("ownership blank owner", valid with { GovernanceOwnership = valid.GovernanceOwnership with { OwnerId = " " } }, MaturityIssue.InvalidOwnership),
    ("ownership without evidence", valid with { GovernanceOwnership = valid.GovernanceOwnership with { EvidenceReferences = [] } }, MaturityIssue.InvalidOwnership),
    ("ownership null evidence", valid with { GovernanceOwnership = valid.GovernanceOwnership with { EvidenceReferences = null! } }, MaturityIssue.InvalidOwnership),
    ("ownership blank evidence", valid with { GovernanceOwnership = valid.GovernanceOwnership with { EvidenceReferences = [" "] } }, MaturityIssue.InvalidOwnership),
    ("ownership insufficient needs reason", valid with { GovernanceOwnership = new(MaturityIndicatorState.InsufficientEvidence, null, []) }, MaturityIssue.InvalidOwnership)
};
foreach (var (name, input, issue) in negatives)
{
    var denied = PilotMaturityProjector.Project(input);
    Check($"typed {name}", denied.Issue == issue && denied.Projection is null && !denied.HasProjection);
}

var original = Project(valid);
var reordered = valid with
{
    Catalog = valid.Catalog with { MandatoryDomains = valid.Catalog.MandatoryDomains.Reverse().ToArray() },
    Indicators = valid.Indicators.Reverse().Select(item => item with
    {
        EvidenceReferences = item.EvidenceReferences.Concat(item.EvidenceReferences).ToArray(),
        AssessmentReferences = item.AssessmentReferences.Reverse().Concat(item.AssessmentReferences).ToArray()
    }).ToArray()
};
Check("canonical reordering and exact duplicate references", Project(reordered).InputDigest == original.InputDigest && Project(reordered).ContentDigest == original.ContentDigest);
var refs = new[] { "fixture-evidence:immutable" };
var ownerRefs = new[] { "fixture-owner:immutable" };
var domainsArray = valid.Catalog.MandatoryDomains.ToArray();
var indicatorsArray = valid.Indicators.Select(item => item with { EvidenceReferences = refs }).ToArray();
var mutable = valid with
{
    Catalog = valid.Catalog with { MandatoryDomains = domainsArray },
    Indicators = indicatorsArray,
    GovernanceOwnership = valid.GovernanceOwnership with { EvidenceReferences = ownerRefs }
};
var snapshot = Project(mutable);
refs[0] = "fixture-evidence:changed"; ownerRefs[0] = "fixture-owner:changed";
domainsArray[0] = new("mutated", "mutated"); indicatorsArray[0] = first with { DomainId = "mutated" };
Check("nested mutable input isolated", snapshot.Input.Indicators.All(item => item.EvidenceReferences.Single() == "fixture-evidence:immutable") &&
    snapshot.Input.Catalog.MandatoryDomains.All(domain => domain.Id != "mutated") &&
    snapshot.Input.GovernanceOwnership.EvidenceReferences.Single() == "fixture-owner:immutable" && snapshot.Level == MaturityLevel.Optimized);
Check("snapshot reprojects identically", Project(snapshot.Input).ContentDigest == snapshot.ContentDigest);
Check("collections are read-only", IsReadOnly(snapshot.Input.Indicators) && IsReadOnly(snapshot.Input.Catalog.MandatoryDomains) &&
    IsReadOnly(snapshot.Input.Indicators.First().EvidenceReferences) && IsReadOnly(snapshot.Domains));
foreach (var changed in new[]
{
    valid with { Versions = valid.Versions with { SourceAnalysisFixtureDigest = new string('a', 64) } },
    valid with { Catalog = valid.Catalog with { Version = "fictional-catalog-v2" } },
    valid with { Catalog = valid.Catalog with { MandatoryDomains = valid.Catalog.MandatoryDomains.Select(domain => domain with { Name = domain.Name + " changed" }).ToArray() } },
    Replace(valid, first, first with { EvidenceReferences = ["fixture-evidence:changed"] }),
    Replace(valid, first, first with { ReasonCode = "EXPLICIT-SYNTHETIC-REASON" }),
    Replace(valid, improvement, improvement with { AssessmentReferences = ["fixture-assessment:c", "fixture-assessment:d"] }),
    valid with { GovernanceOwnership = valid.GovernanceOwnership with { OwnerId = "other-fixture-owner" } }
}) Check("digest binds all frozen metadata", Project(changed).InputDigest != original.InputDigest && Project(changed).ContentDigest != original.ContentDigest);

var random = new Random(51);
for (var iteration = 0; iteration < 200; iteration++)
{
    var count = random.Next(1, 101);
    var b = random.Next(count + 1); var o = random.Next(count + 1); var i = random.Next(count + 1);
    var input = Input(count, b, o, i);
    var current = Project(input);
    var better = Project(Input(count, Math.Min(count, b + 1), Math.Min(count, o + 1), Math.Min(count, i + 1)));
    var largerCatalog = input with { Catalog = input.Catalog with { MandatoryDomains = input.Catalog.MandatoryDomains.Concat([new("NEW-MISSING-DOMAIN", "Fictional missing domain")]).ToArray() } };
    var withMissing = Project(largerCatalog);
    if (better.Level < current.Level || withMissing.Level > current.Level || current.Counts.MandatoryDomains != count ||
        Project(input with { Indicators = input.Indicators.Reverse().ToArray() }).ContentDigest != current.ContentDigest)
        throw new Exception($"property iteration {iteration}");
}
Check("two hundred fixed-seed monotonicity/denominator/order properties", true);
var large = Project(Input(100_000, 100_000, 100_000, 100_000));
Check("one hundred thousand mandatory domains", large.Level == MaturityLevel.Optimized && large.Counts.MandatoryDomains == 100_000 &&
    large.Counts.Indicators == 500_000 && large.Counts.MetIndicators == 500_000 && large.Counts.InsufficientIndicators == 0);
Check("large immutable reprojection", Project(large.Input).ContentDigest == large.ContentDigest);
Console.WriteLine($"{checks} pure maturity checks passed, including 200 fixed-seed iterations and 100,000 domains / 500,000 indicators; no health conversion or catalog authority.");

MaturityInput Input(int count, int baseMet, int operationMet = 0, int improvementMet = 0)
{
    var domains = Enumerable.Range(1, count).Select(number => new MaturityDomainDefinition($"DOMAIN-{number}", $"Fictional domain {number}")).ToArray();
    var indicators = domains.SelectMany(domain => allKinds.Select(kind =>
    {
        var number = Number(domain.Id);
        var met = kind is MaturityIndicatorKind.Design or MaturityIndicatorKind.Implementation ? number <= baseMet :
            kind is MaturityIndicatorKind.MeasuredOperation or MaturityIndicatorKind.RegularReview ? number <= operationMet : number <= improvementMet;
        return new MaturityIndicatorEvidence(domain.Id, kind, met ? MaturityIndicatorState.Met : MaturityIndicatorState.NotMet,
            [$"fixture-evidence:{domain.Id}:{kind}"], kind == MaturityIndicatorKind.ValidatedImprovement ? ["fixture-assessment:a", "fixture-assessment:b"] : [],
            met && kind == MaturityIndicatorKind.ValidatedImprovement);
    })).ToArray();
    return new(new(PilotMaturityProjector.AlgorithmVersion, PilotMaturityProjector.InputSchemaVersion, "fixture-baseline", sourceDigest),
        new(SyntheticMaturityFixturePack.CatalogVersion, 60, 80, 80, 80, 2, PilotMaturityProjector.AuthorityBoundary, domains),
        indicators, new(MaturityIndicatorState.Met, "fixture-owner", ["fixture-governance:owner"]));
}
static int Number(string id) => int.Parse(id.AsSpan("DOMAIN-".Length));
static MaturityInput Replace(MaturityInput input, MaturityIndicatorEvidence original, MaturityIndicatorEvidence replacement) =>
    input with { Indicators = input.Indicators.Select(item => item == original ? replacement : item).ToArray() };
static MaturityProjection Project(MaturityInput input) => PilotMaturityProjector.Project(input).Projection ??
    throw new Exception($"unexpected denial: {PilotMaturityProjector.Project(input).Issue}");
static bool IsReadOnly<T>(IReadOnlyCollection<T> values)
{
    if (values is not IList<T> list || !list.IsReadOnly) return false;
    try { list[0] = list[0]; return false; } catch (NotSupportedException) { return true; }
}
void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    checks++;
}
