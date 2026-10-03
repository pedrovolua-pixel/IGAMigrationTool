# Synthetic evaluation sampling v1 engineering contract

Status: settled engineering freeze; implementation and focused checks executed, nonauthor review pending
Date: 2026-10-03
Packet: S03. Authority: [exact EV02-S01–02 approval](local-evaluation-policy-approval.md), [approved policy](local-evaluation-sampling-contract-proposal.md), [test cases S001–S010/S020](local-evaluation-sampling-test-plan.md), [cycle03](../../plans/active/local-pilot-m08-policy-implementation-cycle03.md).

The pure internal general-AI fixture sampler changes no accuracy-v1 bytes, permissions, source mapping, storage or live authority. S03 excludes S03/S04 warning/regression behavior owned by G03. Synthetic metadata and eligibility declarations do not prove actual source correctness. A corrected evaluation supplies a new sampleId and predecessor digest; this module cannot enforce persisted uniqueness/history.

## Exact API

Namespace SyntheticEvaluation. Public declarations (positional field order fixed):

```csharp
enum SamplingSeverity { Critical, High, Medium, Low, Informational }
enum SamplingVersionKind { SourceBuild, EnvironmentEvidence, Capability, Baseline, ReassessmentBaseline,
    Collection, Query, Normalization,
    Catalog, Profile, Scoring, Maturity, AiProvider, AiModel, AiPrompt, AiSchema,
    AiSettings, Application, Reviewer, ReviewerEligibility, Instruction, Conflict, ReviewEvents }
enum SamplingIssue { InvalidInput, InvalidReference, InvalidVersion, InvalidMember,
    DuplicateMember, AmbiguousPrimary, InvalidBudget, InsufficientBudget }
record SamplingVersionBinding(SamplingVersionKind Kind, string Version);
record SamplingVersionManifest(IReadOnlyList<SamplingVersionBinding> Bindings,
    DateTimeOffset CorrectionCutoffUtc, DateTimeOffset EvaluationDateUtc,
    string? PredecessorSampleDigest = null);
record SamplingMember(string Id, string ScopeId, string EnvironmentId,
    string PrimaryModuleId, string CategoryId, string RuleVersion,
    string ModelPromptVersion, string ConfidenceBandId, SamplingSeverity Severity,
    bool PrimarySettled, bool Available, string? UnavailableReason,
    IReadOnlyList<string> SecondaryModuleIds);
record SamplingInput(string SampleId, string PopulationId, string ScopeId,
    string Seed, SamplingVersionManifest Versions, IReadOnlyList<SamplingMember> Members,
    int? HardReviewBudget = null);
record SamplingResult(SamplingIssue? Issue, SamplingProjection? Projection);
```

`EvaluationSampler.Build(SamplingInput? input)` returns one enum-only denial (Projection null) or immutable projection (Issue null). HasProjection is true only for success. Projection properties: SampleId, PopulationId, ScopeId, Seed, HardReviewBudget, Versions, Population, Strata, Ranks, Selected, VersionManifestJson, VersionManifestDigest, PopulationJson, PopulationDigest, CanonicalJson, ContentDigest. AlgorithmVersion constant is `synthetic-evaluation-sampling-v1`. SamplingStratum exposes EnvironmentId, PrimaryModuleId, CategoryId, Severity, PopulationCount, Allocation. SamplingRank exposes MemberId, RankDigest. SamplingSelection exposes Member (detached SamplingMember), Mandatory (bool), RankDigest (string?; null for mandatory). These output classes have internal constructors/get-only properties. Output collections, including nested secondary provenance/version bindings, are detached read-only views; record replacement cannot mutate an existing result.

## Admission and error mapping

Validation order: (1) null input/versions/collections or member count >100000 => InvalidInput; (2) manifest binding Count not exactly23 => InvalidVersion, without indexing/enumeration/copy; (3) detached null entries or invalid nested collection/resource bounds => InvalidInput; (4) sample/population/scope references => InvalidReference; (5) seed/manifest => InvalidVersion; (6) negative hard budget => InvalidBudget; (7) members, in supplied order; (8) insufficient budget after full validation. A malformed synthetic reference anywhere in member metadata/secondary provenance => InvalidReference. Every reference follows existing ASCII `synthetic-` plus 1–118 `[a-z0-9._-]`, total128 max. Empty reason/secondary references do not normalize.

Seed is exactly64 lowercase hexadecimal characters; predecessor digest is null or same grammar. Manifest requires exactly one binding for every declared kind, valid reference values, no extra/unknown/duplicate kinds, UTC-zero offsets for both times => InvalidVersion. No chronology rule is invented. Null binding collection/entry is InvalidInput. Unknown severity, wrong member scope, inconsistent availability (available requires null reason; unavailable requires a valid reason), duplicate secondary IDs or secondary equal primary => InvalidMember (malformed reasons remain InvalidReference). PrimarySettled=false => AmbiguousPrimary; invalid/missing primary reference => InvalidReference. Repeated memberId => DuplicateMember even across tuples; no suppression or tuple guessing. Secondary provenance is at most100000 entries per member, matching the admitted reference-vector bound; greater counts/null entries => InvalidInput. Total admitted secondary references across population is capped100000, InvalidInput beyond that, to bound detachment. This is an internal synthetic resource bound, not a product sampling quota. Empty secondary collection is allowed. Member confidence/rule/model metadata are opaque references and never filter membership. Enum values are declared only.

All Critical/High selected. N<100 selects all; otherwise lower quota k=min(L,max(S,max(0,100-H),H>=100?100:0)). N0 succeeds empty. Optional hard budget is upper bound; if below required selection => InsufficientBudget and no partial sample. Larger budget leaves membership unchanged. Allocation reserves one each lower tuple; residual Hamilton weights n-1 use Int64 exact arithmetic and descending remainder then component ordinal environment/module/category/severity-name tie. k=L allocation is each size. Exact ties in SHA256 ranks use ordinal memberId; SHA256 always used, no injectable hash bypass. Selected unavailable remains selected and the composer must record Unreviewed unless separately authorized review evidence supports an outcome.

## Canonical bytes

Utf8JsonWriter default encoder, compact UTF8, no BOM/newline. Only ASCII restricted references/enums/hex and ISO O dates occur. The default encoder escapes the plus sign in UTC offsets as literal `\u002B`; Python compact JSON explicitly applies this transformation before hashing. No other admitted reference/date character requires escaping. All optional values written as JSON null; booleans native; integers decimal. Date format invariant `O`, UTC `+00:00`. SHA256 lowercase hex of exact UTF8. No floating point.

1. Version manifest JSON: schema=`synthetic-evaluation-sampling-versions-v1`, bindings (enum declaration order; each kind,version), correctionCutoffUtc, evaluationDateUtc, predecessorSampleDigest.
2. Population JSON: schema=`synthetic-evaluation-sampling-population-v1`, populationId, scopeId, versionManifestDigest, members (ordinal memberId order). Each member: id,scopeId,environmentId,primaryModuleId,categoryId,ruleVersion,modelPromptVersion,confidenceBandId,severity,primarySettled,available,unavailableReason,secondaryModuleIds (ordinal sorted).
3. Sample JSON: schema=`synthetic-evaluation-sampling-v1`, sampleId,populationId,scopeId,populationDigest,versionManifestDigest,seed,hardReviewBudget,strata,ranks,selected. Strata in component ordinal order: environmentId,primaryModuleId,categoryId,severity,populationCount,allocation. Ranks contains every lower member in ordinal memberId order: memberId,rankDigest. Selected in ordinal memberId order: memberId,mandatory,rankDigest. Full population/member snapshots remain bound by populationDigest and immutable values.

Rank bytes exactly approved proposal: ASCII domain `iga.synthetic-evaluation.sample-rank.v1`, zero byte,32 decoded seed bytes; then scopeId,populationDigest,environmentId,primaryModuleId,categoryId,severity-name,memberId, each uint32 big-endian UTF8 byte length then bytes. Within each tuple rank unsigned digest bytes ascending; digest lowercase hex ordinal sort is equivalent. Admission scope is fixed and memberId unique, so ordinal scoped-ID ties reduce to memberId.

## Independent expectations and applicability

[Python oracle](../../tests/unit/SyntheticEvaluationSampling.Tests/oracle.py) imports no product code. It uses exact Fraction Hamilton arithmetic, explicit field bytes and SHA256. Its committed literal goldens below bind two-member population/one rank and120-member 100-selected fixture. C# tests compare literal JSON/digests/rank bytes/selected-exclusion values; generated expectations use independent fractional capacity reasoning, never product allocator output. Exhaustive feasible small allocation invariants, quota boundary cases, hard budgets, unavailable retention, all denied forms, input permutation/culture, caller mutation and exactly100000-member arithmetic are required. Tie ordering tested via observable Hamilton ties; actual cryptographic hash collision cannot be manufactured and must remain a documented unexecuted SHA tie branch rather than weaken production hashing. Actual source mapping, unavailable outcome authorization, persisted predecessor uniqueness/corrections and live gate acceptance remain NOT VERIFIED. Focused project config is coordinator-owned.

Coordinator reviewed the final pre-code freeze at f9a5ffb, corrected the initial UTC-plus JSON golden defect before dependent implementation, and approved engineering implementation. Focused locked audited restore, full formatting verification for module/test projects, Release build with zero warnings/errors,7,717 independent assertions and scoped gitleaks8.30.1 scan executed successfully. Nonauthor review subsequently found and corrected early version-binding cardinality admission and missing committed-golden oracle comparison; binding shape now denies before copying, and the offline oracle asserts every computed envelope/hash/rank/selection/allocation against the literal contract before printing. Initial evidence remains preserved. Native evidence is under `/private/tmp/iga-m08-cycle03-evidence/sampling`; nonauthor review and combined architecture/accuracy/regression checks remain coordinator completion dependencies. No migrations/dependencies/runtime configuration changed.

### Literal Python goldens

```json
{
  "sampleJson": "{\"schema\":\"synthetic-evaluation-sampling-v1\",\"sampleId\":\"synthetic-sample\",\"populationId\":\"synthetic-population\",\"scopeId\":\"synthetic-scope\",\"populationDigest\":\"d7c420295459227adf0e10f02689ef4ef9857ea497d8992075ec5c00063c8cfe\",\"versionManifestDigest\":\"a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef\",\"seed\":\"0101010101010101010101010101010101010101010101010101010101010101\",\"hardReviewBudget\":null,\"strata\":[{\"environmentId\":\"synthetic-env\",\"primaryModuleId\":\"synthetic-module\",\"categoryId\":\"synthetic-category\",\"severity\":\"Medium\",\"populationCount\":1,\"allocation\":1},{\"environmentId\":\"synthetic-env-b\",\"primaryModuleId\":\"synthetic-module\",\"categoryId\":\"synthetic-category\",\"severity\":\"Low\",\"populationCount\":1,\"allocation\":1}],\"ranks\":[{\"memberId\":\"synthetic-member-000001\",\"rankDigest\":\"43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44\"},{\"memberId\":\"synthetic-member-000002\",\"rankDigest\":\"458c9f7276d0bc0328e17bcf9a1eacaa08d62e9e17398004de20a97af2103fc3\"}],\"selected\":[{\"memberId\":\"synthetic-member-000001\",\"mandatory\":false,\"rankDigest\":\"43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44\"},{\"memberId\":\"synthetic-member-000002\",\"mandatory\":false,\"rankDigest\":\"458c9f7276d0bc0328e17bcf9a1eacaa08d62e9e17398004de20a97af2103fc3\"}]}",
  "sampleDigest": "5c33882ce142ae63a52ff6149cb45ee9aeb3eb8fff434dd40941a10f5ce92f02",
  "versionJson": "{\"schema\":\"synthetic-evaluation-sampling-versions-v1\",\"bindings\":[{\"kind\":\"SourceBuild\",\"version\":\"synthetic-sourcebuild\"},{\"kind\":\"EnvironmentEvidence\",\"version\":\"synthetic-environmentevidence\"},{\"kind\":\"Capability\",\"version\":\"synthetic-capability\"},{\"kind\":\"Baseline\",\"version\":\"synthetic-baseline\"},{\"kind\":\"ReassessmentBaseline\",\"version\":\"synthetic-reassessmentbaseline\"},{\"kind\":\"Collection\",\"version\":\"synthetic-collection\"},{\"kind\":\"Query\",\"version\":\"synthetic-query\"},{\"kind\":\"Normalization\",\"version\":\"synthetic-normalization\"},{\"kind\":\"Catalog\",\"version\":\"synthetic-catalog\"},{\"kind\":\"Profile\",\"version\":\"synthetic-profile\"},{\"kind\":\"Scoring\",\"version\":\"synthetic-scoring\"},{\"kind\":\"Maturity\",\"version\":\"synthetic-maturity\"},{\"kind\":\"AiProvider\",\"version\":\"synthetic-aiprovider\"},{\"kind\":\"AiModel\",\"version\":\"synthetic-aimodel\"},{\"kind\":\"AiPrompt\",\"version\":\"synthetic-aiprompt\"},{\"kind\":\"AiSchema\",\"version\":\"synthetic-aischema\"},{\"kind\":\"AiSettings\",\"version\":\"synthetic-aisettings\"},{\"kind\":\"Application\",\"version\":\"synthetic-application\"},{\"kind\":\"Reviewer\",\"version\":\"synthetic-reviewer\"},{\"kind\":\"ReviewerEligibility\",\"version\":\"synthetic-reviewereligibility\"},{\"kind\":\"Instruction\",\"version\":\"synthetic-instruction\"},{\"kind\":\"Conflict\",\"version\":\"synthetic-conflict\"},{\"kind\":\"ReviewEvents\",\"version\":\"synthetic-reviewevents\"}],\"correctionCutoffUtc\":\"2026-10-03T00:00:00.0000000\\u002B00:00\",\"evaluationDateUtc\":\"2026-10-03T01:00:00.0000000\\u002B00:00\",\"predecessorSampleDigest\":null}",
  "versionDigest": "a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef",
  "populationJson": "{\"schema\":\"synthetic-evaluation-sampling-population-v1\",\"populationId\":\"synthetic-population\",\"scopeId\":\"synthetic-scope\",\"versionManifestDigest\":\"a64bf1accd2ecca3b0c089076e7cfb665100cac86024c9448def6fed487420ef\",\"members\":[{\"id\":\"synthetic-member-000001\",\"scopeId\":\"synthetic-scope\",\"environmentId\":\"synthetic-env\",\"primaryModuleId\":\"synthetic-module\",\"categoryId\":\"synthetic-category\",\"ruleVersion\":\"synthetic-rule\",\"modelPromptVersion\":\"synthetic-model-prompt\",\"confidenceBandId\":\"synthetic-confidence\",\"severity\":\"Medium\",\"primarySettled\":true,\"available\":true,\"unavailableReason\":null,\"secondaryModuleIds\":[\"synthetic-secondary\"]},{\"id\":\"synthetic-member-000002\",\"scopeId\":\"synthetic-scope\",\"environmentId\":\"synthetic-env-b\",\"primaryModuleId\":\"synthetic-module\",\"categoryId\":\"synthetic-category\",\"ruleVersion\":\"synthetic-rule\",\"modelPromptVersion\":\"synthetic-model-prompt\",\"confidenceBandId\":\"synthetic-confidence\",\"severity\":\"Low\",\"primarySettled\":true,\"available\":true,\"unavailableReason\":null,\"secondaryModuleIds\":[\"synthetic-secondary\"]}]}",
  "populationDigest": "d7c420295459227adf0e10f02689ef4ef9857ea497d8992075ec5c00063c8cfe",
  "rankHex": "6967612e73796e7468657469632d6576616c756174696f6e2e73616d706c652d72616e6b2e76310001010101010101010101010101010101010101010101010101010101010101010000000f73796e7468657469632d73636f706500000040643763343230323935343539323237616466306531306630323638396566346566393835376561343937643839393230373565633563303030363363386366650000000d73796e7468657469632d656e760000001073796e7468657469632d6d6f64756c650000001273796e7468657469632d63617465676f7279000000064d656469756d0000001773796e7468657469632d6d656d6265722d303030303031",
  "rankDigest": "43091c9fcac3c62e99ec80a38f740554af161088a4c2d36c2790d9880d291f44",
  "selectedExcludedIds": [
    "synthetic-member-000000",
    "synthetic-member-000003",
    "synthetic-member-000010",
    "synthetic-member-000011",
    "synthetic-member-000014",
    "synthetic-member-000021",
    "synthetic-member-000037",
    "synthetic-member-000042",
    "synthetic-member-000044",
    "synthetic-member-000045",
    "synthetic-member-000050",
    "synthetic-member-000057",
    "synthetic-member-000062",
    "synthetic-member-000067",
    "synthetic-member-000082",
    "synthetic-member-000085",
    "synthetic-member-000100",
    "synthetic-member-000102",
    "synthetic-member-000115",
    "synthetic-member-000119"
  ],
  "allocation2345": [
    2,
    2,
    3
  ]
}
```
