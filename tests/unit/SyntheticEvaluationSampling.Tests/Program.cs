using System.Globalization;
using System.Security.Cryptography;
using SyntheticEvaluation;

var assertions = 0;
void Check(bool condition, string name)
{
    assertions++;
    if (!condition) { throw new InvalidOperationException(name); }
}
SamplingVersionManifest Versions() => new(Enum.GetValues<SamplingVersionKind>()
    .Select(kind => new SamplingVersionBinding(kind, "synthetic-" + kind.ToString().ToLowerInvariant())).ToArray(),
    new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero));
SamplingMember Member(int i, string environment = "synthetic-env", SamplingSeverity severity = SamplingSeverity.Medium) =>
    new($"synthetic-member-{i:D6}", "synthetic-scope", environment, "synthetic-module", "synthetic-category",
        "synthetic-rule", "synthetic-model-prompt", "synthetic-confidence", severity, true, true, null,
        new[] { "synthetic-secondary" });
SamplingInput Input(IEnumerable<SamplingMember> members, int? budget = null) =>
    new("synthetic-sample", "synthetic-population", "synthetic-scope", string.Concat(Enumerable.Repeat("01", 32)),
        Versions(), members.ToArray(), budget);
SamplingProjection Success(SamplingInput input)
{
    var result = EvaluationSampler.Build(input);
    Check(result.HasProjection && result.Issue is null, "expected successful sample: " + result.Issue);
    return result.Projection!;
}
void Denied(SamplingInput? input, SamplingIssue expected)
{
    var result = EvaluationSampler.Build(input);
    Check(result.Issue == expected && result.Projection is null && !result.HasProjection, "denial: " + expected);
}
SamplingMember[] Cohort(int mandatory, params int[] lowerSizes)
{
    var members = Enumerable.Range(0, mandatory).Select(i => Member(i, severity: i % 2 == 0 ? SamplingSeverity.Critical : SamplingSeverity.High)).ToList();
    var id = mandatory;
    for (var stratum = 0; stratum < lowerSizes.Length; stratum++)
    {
        for (var j = 0; j < lowerSizes[stratum]; j++) { members.Add(Member(id++, $"synthetic-env-{stratum:D3}")); }
    }

    return members.ToArray();
}

// Literal independently frozen Python bytes; no production helper creates these expectations.
const string versionJson = """
{"schema":"synthetic-evaluation-sampling-versions-v1","bindings":[{"kind":"SourceBuild","version":"synthetic-sourcebuild"},{"kind":"EnvironmentEvidence","version":"synthetic-environmentevidence"},{"kind":"Capability","version":"synthetic-capability"},{"kind":"Baseline","version":"synthetic-baseline"},{"kind":"ReassessmentBaseline","version":"synthetic-reassessmentbaseline"},{"kind":"Collection","version":"synthetic-collection"},{"kind":"Query","version":"synthetic-query"},{"kind":"Normalization","version":"synthetic-normalization"},{"kind":"Catalog","version":"synthetic-catalog"},{"kind":"Profile","version":"synthetic-profile"},{"kind":"Scoring","version":"synthetic-scoring"},{"kind":"Maturity","version":"synthetic-maturity"},{"kind":"AiProvider","version":"synthetic-aiprovider"},{"kind":"AiModel","version":"synthetic-aimodel"},{"kind":"AiPrompt","version":"synthetic-aiprompt"},{"kind":"AiSchema","version":"synthetic-aischema"},{"kind":"AiSettings","version":"synthetic-aisettings"},{"kind":"Application","version":"synthetic-application"},{"kind":"Reviewer","version":"synthetic-reviewer"},{"kind":"ReviewerEligibility","version":"synthetic-reviewereligibility"},{"kind":"Instruction","version":"synthetic-instruction"},{"kind":"Conflict","version":"synthetic-conflict"},{"kind":"ReviewEvents","version":"synthetic-reviewevents"}],"correctionCutoffUtc":"2026-10-03T00:00:00.0000000\u002B00:00","evaluationDateUtc":"2026-10-03T01:00:00.0000000\u002B00:00","predecessorSampleDigest":null}
""";
const string versionDigest = """
a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef
""";
const string populationJson = """
{"schema":"synthetic-evaluation-sampling-population-v1","populationId":"synthetic-population","scopeId":"synthetic-scope","versionManifestDigest":"a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef","members":[{"id":"synthetic-member-000001","scopeId":"synthetic-scope","environmentId":"synthetic-env","primaryModuleId":"synthetic-module","categoryId":"synthetic-category","ruleVersion":"synthetic-rule","modelPromptVersion":"synthetic-model-prompt","confidenceBandId":"synthetic-confidence","severity":"Medium","primarySettled":true,"available":true,"unavailableReason":null,"secondaryModuleIds":["synthetic-secondary"]},{"id":"synthetic-member-000002","scopeId":"synthetic-scope","environmentId":"synthetic-env-b","primaryModuleId":"synthetic-module","categoryId":"synthetic-category","ruleVersion":"synthetic-rule","modelPromptVersion":"synthetic-model-prompt","confidenceBandId":"synthetic-confidence","severity":"Low","primarySettled":true,"available":true,"unavailableReason":null,"secondaryModuleIds":["synthetic-secondary"]}]}
""";
const string populationDigest = """
d7c420295459227adf0e10f02689ef4ef9857ea497d8992075ec5c00063c8cfe
""";
const string sampleJson = """
{"schema":"synthetic-evaluation-sampling-v1","sampleId":"synthetic-sample","populationId":"synthetic-population","scopeId":"synthetic-scope","populationDigest":"d7c420295459227adf0e10f02689ef4ef9857ea497d8992075ec5c00063c8cfe","versionManifestDigest":"a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef","seed":"0101010101010101010101010101010101010101010101010101010101010101","hardReviewBudget":null,"strata":[{"environmentId":"synthetic-env","primaryModuleId":"synthetic-module","categoryId":"synthetic-category","severity":"Medium","populationCount":1,"allocation":1},{"environmentId":"synthetic-env-b","primaryModuleId":"synthetic-module","categoryId":"synthetic-category","severity":"Low","populationCount":1,"allocation":1}],"ranks":[{"memberId":"synthetic-member-000001","rankDigest":"43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44"},{"memberId":"synthetic-member-000002","rankDigest":"458c9f7276d0bc0328e17bcf9a1eacaa08d62e9e17398004de20a97af2103fc3"}],"selected":[{"memberId":"synthetic-member-000001","mandatory":false,"rankDigest":"43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44"},{"memberId":"synthetic-member-000002","mandatory":false,"rankDigest":"458c9f7276d0bc0328e17bcf9a1eacaa08d62e9e17398004de20a97af2103fc3"}]}
""";
const string sampleDigest = """
5c33882ce142ae63a52ff6149cb45ee9aeb3eb8fff434dd40941a10f5ce92f02
""";
const string rankHex = """
6967612e73796e7468657469632d6576616c756174696f6e2e73616d706c652d72616e6b2e76310001010101010101010101010101010101010101010101010101010101010101010000000f73796e7468657469632d73636f706500000040643763343230323935343539323237616466306531306630323638396566346566393835376561343937643839393230373565633563303030363363386366650000000d73796e7468657469632d656e760000001073796e7468657469632d6d6f64756c650000001273796e7468657469632d63617465676f7279000000064d656469756d0000001773796e7468657469632d6d656d6265722d303030303031
""";
const string rankDigest = """
43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44
""";
string[] excludedGolden = ["synthetic-member-000000", "synthetic-member-000003", "synthetic-member-000010", "synthetic-member-000011", "synthetic-member-000014", "synthetic-member-000021", "synthetic-member-000037", "synthetic-member-000042", "synthetic-member-000044", "synthetic-member-000045", "synthetic-member-000050", "synthetic-member-000057", "synthetic-member-000062", "synthetic-member-000067", "synthetic-member-000082", "synthetic-member-000085", "synthetic-member-000100", "synthetic-member-000102", "synthetic-member-000115", "synthetic-member-000119"];
var literalInput = Input(new[] { Member(1), Member(2, "synthetic-env-b", SamplingSeverity.Low) });
var literal = Success(literalInput);
Check(literal.VersionManifestJson == versionJson && literal.VersionManifestDigest == versionDigest, "version byte golden");
Check(literal.PopulationJson == populationJson && literal.PopulationDigest == populationDigest, "population byte golden");
Check(literal.CanonicalJson == sampleJson && literal.ContentDigest == sampleDigest, "sample byte golden");
Check(literal.Ranks.Single(rank => rank.MemberId == Member(1).Id).RankDigest == rankDigest, "rank byte golden");
Check(Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(rankHex))) == rankDigest, "literal complete rank bytes");
Check(literal.Selected.Select(selected => selected.Member.Id).SequenceEqual(new[] { Member(1).Id, Member(2).Id }), "small golden selection");
var bigInput = Input(Enumerable.Range(0, 120).Select(i => Member(i)));
var big = Success(bigInput);
Check(big.Population.Select(member => member.Id).Except(big.Selected.Select(selection => selection.Member.Id))
    .SequenceEqual(excludedGolden), "independent120-member excluded IDs");

foreach (var (mandatory, sizes, expected) in new[]
{
    (20, new[] { 40, 40, 40, 10 }, 100), (120, new[] { 40, 40, 40, 10 }, 220),
    (120, Enumerable.Range(0, 150).Select(i => i < 130 ? 2 : 1).ToArray(), 270),
    (120, new[] { 15, 15, 15, 15 }, 180), (99, new[] { 120 }, 100),
    (100, new[] { 120 }, 200), (101, new[] { 120 }, 201), (105, new[] { 8, 3, 1 }, 117),
    (90, Enumerable.Repeat(20, 20).ToArray(), 110), (101, Array.Empty<int>(), 101),
    (0, new[] { 80, 30, 10 }, 100), (100, new[] { 500, 500 }, 200)
})
{
    var fixture = Input(Cohort(mandatory, sizes));
    var projection = Success(fixture);
    Check(projection.Selected.Count == expected, "explicit quota example");
    Check(projection.Selected.Count(selection => selection.Mandatory) == mandatory, "all mandatory included");
    Check(projection.Strata.Count == sizes.Length && projection.Strata.All(stratum => stratum.Allocation >= 1), "all nonempty lower strata");
    Check(projection.Selected.All(selection => selection.Mandatory == (selection.Member.Severity is SamplingSeverity.Critical or SamplingSeverity.High)), "mandatory marker");
    Check(projection.Selected.All(selection => selection.Mandatory == (selection.RankDigest is null)), "mandatory rank null");
    Denied(fixture with { HardReviewBudget = expected - 1 }, SamplingIssue.InsufficientBudget);
    var exact = Success(fixture with { HardReviewBudget = expected });
    var generous = Success(fixture with { HardReviewBudget = int.MaxValue });
    Check(exact.Selected.Select(selection => selection.Member.Id).SequenceEqual(generous.Selected.Select(selection => selection.Member.Id)), "larger budget does not enlarge membership");
}

foreach (var severity in Enum.GetValues<SamplingSeverity>())
{
    foreach (var n in new[] { 0, 1, 99, 100 })
    {
        var projection = Success(Input(Enumerable.Range(0, n).Select(i => Member(i, severity: severity)), n));
        Check(projection.Selected.Count == n, "0/1/99/100 all selected for each severity");
    }
}
Denied(Input(new[] { Member(0) }, 0), SamplingIssue.InsufficientBudget);
Check(Success(Input(Array.Empty<SamplingMember>(), 0)).Selected.Count == 0, "empty cap zero");
Denied(Input(Array.Empty<SamplingMember>(), -1), SamplingIssue.InvalidBudget);

// Independent decimal-fraction quota allocation oracle, including Hamilton remainder ties.
int[] Expected(int[] sizes, int quota)
{
    if (quota == sizes.Sum()) { return sizes.ToArray(); }
    var r = quota - sizes.Length;
    if (r == 0) { return Enumerable.Repeat(1, sizes.Length).ToArray(); }
    var fractions = sizes.Select(size => (decimal)r * (size - 1) / (sizes.Sum() - sizes.Length)).ToArray();
    var floors = fractions.Select(value => (int)decimal.Floor(value)).ToArray();
    var extras = r - floors.Sum();
    var winners = Enumerable.Range(0, sizes.Length).OrderByDescending(i => fractions[i] - floors[i]).ThenBy(i => i).Take(extras).ToHashSet();
    return floors.Select((floor, i) => 1 + floor + (winners.Contains(i) ? 1 : 0)).ToArray();
}
for (var a = 1; a <= 6; a++)
{
    for (var b = 1; b <= 6; b++)
    {
        for (var c = 1; c <= 6; c++)
        {
            var sizes = new[] { a, b, c };
            for (var k = 3; k <= sizes.Sum(); k++)
            {
                var projection = Success(Input(Cohort(100 - k, sizes)));
                var actual = projection.Strata.Select(stratum => stratum.Allocation).ToArray();
                Check(actual.SequenceEqual(Expected(sizes, k)), "exact independent allocation");
                Check(actual.Sum() == k && actual.Zip(sizes).All(pair => pair.First >= 1 && pair.First <= pair.Second), "allocation capacity invariant");
                Check(projection.Selected.Count == 100 && projection.Selected.Select(selection => selection.Member.Id).Distinct().Count() == 100, "selected population invariant");
            }
        }
    }
}
Check(Success(Input(Cohort(93, 2, 4, 5))).Strata.Select(stratum => stratum.Allocation).SequenceEqual(new[] { 2, 2, 3 }), "explicit residual weighting golden");
var saturation = Success(Input(Cohort(100, 1, 1, 1)));
Check(saturation.Strata.All(stratum => stratum.Allocation == 1), "saturation/W0");

// Unavailability and every declared lower-severity tuple remain factual, no substitution.
var unavailableInput = literalInput with { Members = new[] { Member(1) with { Available = false, UnavailableReason = "synthetic-expired" }, Member(2, "synthetic-env-b", SamplingSeverity.Low) } };
var unavailable = Success(unavailableInput);
Check(unavailable.Selected.Any(selection => !selection.Member.Available && selection.Member.UnavailableReason == "synthetic-expired"), "unavailable retained");
Check(unavailable.Selected.Select(selection => selection.Member.Id).SequenceEqual(literal.Selected.Select(selection => selection.Member.Id)), "small selected membership unchanged");
Check(unavailable.PopulationDigest != literal.PopulationDigest && unavailable.ContentDigest != literal.ContentDigest, "availability version binding");
var mixedTuple = Success(Input(new[] { SamplingSeverity.Medium, SamplingSeverity.Informational, SamplingSeverity.Low }.Select((severity, i) => Member(i, severity: severity))));
Check(mixedTuple.Strata.Select(stratum => stratum.Severity).SequenceEqual(new[] { SamplingSeverity.Informational, SamplingSeverity.Low, SamplingSeverity.Medium }), "severity ordinal names");

// Member, binding and secondary permutations preserve complete bytes across cultures.
var priorCulture = CultureInfo.CurrentCulture;
var priorUiCulture = CultureInfo.CurrentUICulture;
try
{
    foreach (var culture in new[] { "en-US", "tr-TR", "fr-FR", "ar-SA" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var permuted = Success(literalInput with
        {
            Members = literalInput.Members.Reverse().ToArray(),
            Versions = literalInput.Versions with { Bindings = literalInput.Versions.Bindings.Reverse().ToArray() }
        });
        Check(permuted.CanonicalJson == sampleJson && permuted.PopulationJson == populationJson && permuted.VersionManifestJson == versionJson, "permutation/culture byte invariance");
    }
}
finally { CultureInfo.CurrentCulture = priorCulture; CultureInfo.CurrentUICulture = priorUiCulture; }
var provenanceInput = Input(new[] { Member(0) with { SecondaryModuleIds = new[] { "synthetic-z", "synthetic-a" } } });
Check(Success(provenanceInput).ContentDigest == Success(provenanceInput with { Members = new[] { provenanceInput.Members[0] with { SecondaryModuleIds = provenanceInput.Members[0].SecondaryModuleIds.Reverse().ToArray() } } }).ContentDigest, "secondary provenance ordering");

var mutableSecondary = new[] { "synthetic-secondary" };
var mutableBindings = Versions().Bindings.ToArray();
var mutableMembers = new[] { Member(0) with { SecondaryModuleIds = mutableSecondary } };
var mutableInput = Input(mutableMembers) with { Versions = Versions() with { Bindings = mutableBindings } };
var detached = Success(mutableInput);
var savedBytes = detached.CanonicalJson;
mutableSecondary[0] = "synthetic-changed";
mutableBindings[0] = mutableBindings[0] with { Version = "synthetic-changed" };
mutableMembers[0] = Member(50);
Check(detached.Population[0].Id == Member(0).Id && detached.Population[0].SecondaryModuleIds[0] == "synthetic-secondary", "nested detached member");
Check(detached.Versions.Bindings[0].Version == "synthetic-sourcebuild" && detached.CanonicalJson == savedBytes, "detached bindings/bytes");
Check(detached.Population is not SamplingMember[] && detached.Versions.Bindings is not SamplingVersionBinding[] && detached.Population[0].SecondaryModuleIds is not string[], "read-only surfaces");
var corrected = Success(literalInput with { SampleId = "synthetic-sample-corrected", Versions = literalInput.Versions with { PredecessorSampleDigest = literal.ContentDigest } });
Check(corrected.ContentDigest != literal.ContentDigest && corrected.Versions.PredecessorSampleDigest == literal.ContentDigest && literal.ContentDigest == sampleDigest, "predecessor new version preserves old");
Check(Success(bigInput with { Seed = new string('a', 64) }).Selected.Select(selection => selection.Member.Id).Except(big.Selected.Select(selection => selection.Member.Id)).Any(), "specific seed fixture differs");
Check(Success(literalInput with { ScopeId = "synthetic-other", Members = literalInput.Members.Select(member => member with { ScopeId = "synthetic-other" }).ToArray() }).PopulationDigest != literal.PopulationDigest, "scope binds population");
Check(Success(literalInput with { Versions = literalInput.Versions with { Bindings = literalInput.Versions.Bindings.Select(binding => binding.Kind == SamplingVersionKind.AiPrompt ? binding with { Version = "synthetic-new-prompt" } : binding).ToArray() } }).PopulationDigest != literal.PopulationDigest, "version binds population");

// Enum-only denials cover malformed and ambiguous input; no returned partial manifest/input echoes.
Denied(null, SamplingIssue.InvalidInput);
Denied(literalInput with { Versions = null! }, SamplingIssue.InvalidInput);
Denied(literalInput with { Members = null! }, SamplingIssue.InvalidInput);
Denied(literalInput with { Members = new SamplingMember[] { null! } }, SamplingIssue.InvalidInput);
Denied(literalInput with { Versions = Versions() with { Bindings = null! } }, SamplingIssue.InvalidInput);
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Select((binding, i) => i == 0 ? null! : binding).ToArray() } }, SamplingIssue.InvalidInput);
Denied(literalInput with { Members = new[] { Member(0) with { SecondaryModuleIds = null! } } }, SamplingIssue.InvalidInput);
Denied(literalInput with { Members = new[] { Member(0) with { SecondaryModuleIds = new string[] { null! } } } }, SamplingIssue.InvalidInput);
foreach (var reference in new[] { "", "synthetic-", "Synthetic-a", "synthetic-A", "synthetic-a/b", "synthetic-a b", "synthetic-é", "synthetic-a\n", "synthetic-" + new string('a', 119) })
{
    Denied(literalInput with { SampleId = reference }, SamplingIssue.InvalidReference);
    Denied(literalInput with { PopulationId = reference }, SamplingIssue.InvalidReference);
    Denied(literalInput with { ScopeId = reference }, SamplingIssue.InvalidReference);
    foreach (var member in new[]
    {
        Member(0) with { Id = reference }, Member(0) with { ScopeId = reference }, Member(0) with { EnvironmentId = reference },
        Member(0) with { PrimaryModuleId = reference }, Member(0) with { CategoryId = reference }, Member(0) with { RuleVersion = reference },
        Member(0) with { ModelPromptVersion = reference }, Member(0) with { ConfidenceBandId = reference },
        Member(0) with { Available = false, UnavailableReason = reference }, Member(0) with { SecondaryModuleIds = new[] { reference } }
    }) { Denied(Input(new[] { member }), SamplingIssue.InvalidReference); }
}
Check(Success(literalInput with { SampleId = "synthetic-" + new string('a', 118) }).SampleId.Length == 128, "maximum valid reference");
foreach (var seed in new[] { "", new string('a', 63), new string('a', 65), new string('A', 64), new string('g', 64), " " + new string('a', 63) })
{
    Denied(literalInput with { Seed = seed }, SamplingIssue.InvalidVersion);
    Denied(literalInput with { Versions = Versions() with { PredecessorSampleDigest = seed } }, SamplingIssue.InvalidVersion);
}
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Take(22).ToArray() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { Bindings = new NonEnumeratingOversizeBindings() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Append(new(SamplingVersionKind.SourceBuild, "synthetic-other")).ToArray() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Select(binding => binding.Kind == SamplingVersionKind.SourceBuild ? binding with { Kind = (SamplingVersionKind)999 } : binding).ToArray() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Select(binding => binding with { Kind = SamplingVersionKind.SourceBuild }).ToArray() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { Bindings = Versions().Bindings.Select(binding => binding with { Version = "invalid" }).ToArray() } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { CorrectionCutoffUtc = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.FromHours(1)) } }, SamplingIssue.InvalidVersion);
Denied(literalInput with { Versions = Versions() with { EvaluationDateUtc = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.FromHours(1)) } }, SamplingIssue.InvalidVersion);
foreach (var member in new[]
{
    Member(0) with { Severity = (SamplingSeverity)999 }, Member(0) with { ScopeId = "synthetic-other" },
    Member(0) with { Available = false }, Member(0) with { UnavailableReason = "synthetic-reason" },
    Member(0) with { SecondaryModuleIds = new[] { "synthetic-secondary", "synthetic-secondary" } },
    Member(0) with { SecondaryModuleIds = new[] { "synthetic-module" } }
}) { Denied(Input(new[] { member }), SamplingIssue.InvalidMember); }
Denied(Input(new[] { Member(0) with { PrimarySettled = false } }), SamplingIssue.AmbiguousPrimary);
Denied(Input(new[] { Member(0), Member(0) with { EnvironmentId = "synthetic-other", Severity = SamplingSeverity.High } }), SamplingIssue.DuplicateMember);
var manySecondary = Enumerable.Range(0, 100001).Select(i => "synthetic-module-" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
Denied(Input(new[] { Member(0) with { SecondaryModuleIds = manySecondary } }), SamplingIssue.InvalidInput);
Denied(Input(new[] { Member(0) with { SecondaryModuleIds = manySecondary.Take(50001).ToArray() }, Member(1) with { SecondaryModuleIds = manySecondary.Take(50000).ToArray() } }), SamplingIssue.InvalidInput);
Denied(Input(Enumerable.Range(0, 100001).Select(i => Member(i) with { SecondaryModuleIds = Array.Empty<string>() })), SamplingIssue.InvalidInput);
var max = Success(Input(Enumerable.Range(0, 100000).Select(i => Member(i, "synthetic-env-" + (i % 3), i < 100 ? SamplingSeverity.High : SamplingSeverity.Medium) with { SecondaryModuleIds = Array.Empty<string>() })));
Check(max.Population.Count == 100000 && max.Selected.Count == 200 && max.Strata.Sum(stratum => stratum.Allocation) == 100, "100000 admitted maximum");
Check(max.Selected.Count(selection => selection.Mandatory) == 100 && max.Ranks.Count == 99900, "maximum all ranks/mandatory");
// Tuple ordering is component-wise, not a concatenated delimiter key or enum integer.
var tupleMembers = Cohort(93, 2, 4, 5).Select(member => member.Severity == SamplingSeverity.High || member.Severity == SamplingSeverity.Critical ? member : member with
{
    EnvironmentId = "synthetic-env",
    PrimaryModuleId = member.EnvironmentId.EndsWith("002", StringComparison.Ordinal) ? "synthetic-module-b" : "synthetic-module-a",
    CategoryId = member.EnvironmentId.EndsWith("001", StringComparison.Ordinal) ? "synthetic-category-b" : "synthetic-category-a"
}).Reverse().ToArray();
var tupleProjection = Success(Input(tupleMembers));
Check(tupleProjection.Strata.Select(stratum => stratum.Allocation).SequenceEqual(new[] { 2, 2, 3 }), "module/category component ordering and exact tie");
var maximumProvenance = Success(Input(new[] { Member(0) with { SecondaryModuleIds = manySecondary.Take(100000).Reverse().ToArray() } }));
Check(maximumProvenance.Population[0].SecondaryModuleIds.Count == 100000, "maximum secondary vector admitted");
var immutableThrown = false;
try { ((IList<SamplingMember>)literal.Population)[0] = Member(99); }
catch (NotSupportedException) { immutableThrown = true; }
Check(immutableThrown && literal.ContentDigest == sampleDigest, "population cannot be changed through IList");
immutableThrown = false;
try { ((IList<string>)literal.Population[0].SecondaryModuleIds)[0] = "synthetic-changed"; }
catch (NotSupportedException) { immutableThrown = true; }
Check(immutableThrown && literal.Population[0].SecondaryModuleIds[0] == "synthetic-secondary", "secondary cannot be changed through IList");
Console.WriteLine($"Synthetic evaluation sampling passed {assertions} independent assertions.");


// A malformed externally supplied list must be rejected using Count alone, before enumeration.
internal sealed class NonEnumeratingOversizeBindings : IReadOnlyList<SamplingVersionBinding>
{
    public int Count => int.MaxValue;
    public SamplingVersionBinding this[int index] => throw new InvalidOperationException("oversize list indexed");
    public IEnumerator<SamplingVersionBinding> GetEnumerator() => throw new InvalidOperationException("oversize list enumerated");
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
