# Local evaluation sampling and regression contract proposal

Status: PROPOSED — exact owner decision required; not approved or implemented
Date: 2026-10-03
Packet: S02, Milestone08 cycle02
Requested decision roles: product/evaluation owner; technical and security review
Scope: a later local, dependency-free synthetic sampling/regression foundation

## Authority and boundary

This proposal applies the repository [write-technical-spec skill](../../skills/write-technical-spec/SKILL.md) to the unresolved choices recorded in [cycle01](../../plans/active/local-pilot-m08-evaluation-foundation.md#next-human-dependency--evaluation-policy-and-reviewer-procedure) and [cycle02](../../plans/active/local-pilot-m08-evaluation-policy-cycle02.md). It does not amend an approved specification or grant implementation, provider, reviewer, customer-source, activation or release authority. Every rule newly specified below is a recommendation pending attributable human approval.

Sources are [FR-HAS-51–53, AC-HAS-12/19](product-spec.md), [technical evaluation ownership and immutable cohort](technical-spec.md), [TP-HAS-012/019](test-plan.md), and the approved [evaluation plan](evaluation-plan.md), especially Frozen evaluation record, Review population and sample, Accuracy calculation and Material quality regression. Preserve the existing [synthetic accuracy contract](synthetic-evaluation-accuracy-contract.md): `confirmed/(confirmed+rejected)`, strict greater-than80%, explicit indeterminate/unreviewed counts, corrected originating classification, and separate desired-outcome arithmetic. Sampling here covers general AI findings only; desired-outcome evaluations retain their own separate population/sample/outcomes.

No architecture change is proposed. [ADR-0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md) governs immutable history and customer-scoped digests. [ADR-0007](../../architecture/decisions/ADR-0007-reviewer-decision-evidence.md) and [ADR-0008](../../architecture/decisions/ADR-0008-pilot-owner-override-evidence.md) remain bounded pilot implementation choices; a fixture identity/digest establishes neither operational trust nor approval. No storage, runtime endpoint, package, schema migration, retention policy or permission grant is introduced by these documents.

## Requested decisions

| Decision | Recommended exact choice | Consequence requiring approval |
| --- | --- | --- |
| EV02-S01 | Mandatory Critical/High plus the lower quota below; reserve one per non-empty lower stratum; reject insufficient hard budget | Review effort can exceed100, including when Critical/High alone reaches100 |
| EV02-S02 | Residual-size Hamilton allocation and domain-separated seeded SHA256 ranking below, with frozen manifests | Reproducible exact cohort; one settled primary module tuple per finding |
| EV02-S03 | Warn whenever classified denominator is less than30, including zero | Warning leaves counts and acceptance arithmetic unchanged |
| EV02-S04 | Exact regression comparisons below, requiring both relevant denominators at least30 for comparative count/rate rules | Small/unavailable comparisons stay NOT VERIFIED; zero rejection baseline is explicit |

Approval must name all four decision IDs and the exact reviewed document/test-plan revision or digest. Agent review, continuation instructions and a synthetic PASS do not settle these product choices. Real reviewer eligibility/conflicts are a separate R02 decision. If the owner chooses an alternative, amend this packet and its tests before dependent implementation.

## EV02-S01 — review count and hard budgets

Freeze the complete general AI population before selection. Let `N` be its size, `H` its Critical/High count, `L=N-H`, and `S` the number of non-empty lower strata. Lower severities are Medium, Low and Informational. A stratum is the exact tuple `(environmentId, primaryModuleOrFoundationId, assessmentCategoryId, severity)`; empty strata have no reservation. Mandatory members are every Critical/High finding, independent of module, confidence, disposition, review history or affected-object count.

- If `N<100`, select all `N` findings, including all lower members.
- If `N>=100`, select all `H` and exactly `k` lower members:

  `k = min(L, max(S, max(0, 100-H), (H>=100 ? 100 : 0)))`.

  `L=0` implies `S=0` and `k=0`. `N=0` succeeds with an empty sample and no available accuracy ratio; it does not satisfy pilot acceptance. At `N=100`, the formula selects all findings.

Thus when `H<100` the sample reaches100 and covers every non-empty lower stratum, potentially exceeding100 if strata require it. When `H>=100`, lower selection targets up to100 additional members, expanded to cover all lower strata. Examples: `N=150,H=20,L=130,S=4` selects100 total; `N=250,H=120,L=130,S=4` selects220; `N=400,H=120,L=280,S=150` selects270; `N=180,H=120,L=60,S=4` selects180. These are selected-review obligations; unavailable or unreviewed members do not count as completed reviews.

Default selection has no hard cap. A supplied hard review budget is optional, a non-negative integer upper bound on selected members. Compute the required selection first; deny if the bound is below `N` for `N<100`, or below `H+k` otherwise. Never drop mandatory members, omit required strata, shrink the chosen quota, or treat an unavailable member as freeing a slot. A larger budget does not enlarge the v1 quota. Return a structured insufficient-budget issue without a partial sample. This explicitly resolves the approved plan's ambiguous “where the review budget permits” in favor of complete representation; its increased workload requires owner approval.

## EV02-S02 — allocation, ranking and frozen identity

### Population admission

For the first local synthetic implementation, admit at most100000 unique members. Synthetic population/scope/member/environment/module/category/version references follow the existing ASCII `synthetic-` reference grammar (maximum128 total characters). Declared severity is required; Critical/High is mandatory and the three lower severities alone enter lower strata. IDs are unique within the frozen evaluation scope, regardless of severity or tuple. No duplicate suppression, case folding, trimming, category guessing or confidence filtering is allowed.

A finding associated with several module/foundation areas must carry one explicitly settled primary tuple in the frozen upstream mapping, plus any secondary associations as provenance. Sampling must deny a missing, ambiguous or conflicting primary classification; never duplicate the finding into several strata or choose whichever label sorts first. The local fixture may supply that settled classification; it cannot establish correct real-domain mapping. Real primary-module policy and upstream integration require a later reviewed contract if not already settled.

### Exact allocation

For `N<100`, every lower allocation equals its stratum population. Otherwise, for each lower stratum `i` of size `n_i`, reserve one member. Let `R=k-S`, residual capacity `w_i=n_i-1`, and `W=sum(w_i)=L-S`.

- If `R=0`, allocation is one per stratum; avoid division even if `W=0`.
- If `R>0`, `W>0`. Compute `q_i=floor(R*w_i/W)` and exact integer remainder `r_i=(R*w_i) mod W` using sufficiently wide integer arithmetic.
- Give each stratum `1+q_i`; distribute `R-sum(q_i)` single extra slots in descending remainder order. Ties use ascending component-by-component ordinal tuple comparison, with severity spelled exactly `Informational`, `Low`, `Medium`.

Do not round decimal percentages. Zero residual-capacity strata get no residual slots. Because `R<=W`, no allocation exceeds its population; allocations sum to `k`. Equal-per-stratum reservation deliberately favors tiny strata before proportional allocation of remaining capacity.

Example: lower sizes `(2,4,5)` and `k=7` reserve `(1,1,1)`, leaving `R=4,W=8`, residual floors `(0,1,2)` and remainders `(4,4,0)`. The final slot goes to the first tuple, giving `(2,2,3)`. This oracle is independent of selection hashes.

### Seeded ranking bytes

Freeze algorithm version `synthetic-evaluation-sampling-v1` and an explicitly supplied seed of exactly64 lowercase hexadecimal characters; no clock-derived default. Decode seed hex into32 bytes. Rank each lower member within its own stratum by SHA256 of the following byte sequence:

1. ASCII `iga.synthetic-evaluation.sample-rank.v1` followed by one zero byte.
2. The32 seed bytes.
3. Each of scopeId, populationDigest, environmentId, primaryModuleOrFoundationId, assessmentCategoryId, severity, memberId, in that order, encoded as a four-byte unsigned big-endian UTF8 byte length followed by the UTF8 bytes.

PopulationDigest is64 lowercase hex characters, encoded here as ASCII/UTF8 text; it binds the complete frozen population. Rank by unsigned lexicographic digest-byte order ascending; exact hash ties use ascending ordinal scoped memberId. Take the allocated count; membership manifest order is ascending ordinal memberId, not hash order. This domain separation is an engineering rule for synthetic reproducibility, not a random-source security claim. Changing seed/version/population may change the cohort; input ordering cannot. Freeze seed, algorithm, population bytes/digest, tuples/sizes/allocations, member rank digests, selected IDs and manifest digest before outcomes. Canonical population bytes must be independently golden-tested and versioned in the later exact implementation contract; until then no implementation or cross-language byte compatibility is claimed.

### Freeze and correction lifecycle

Population and selected membership are detached immutable snapshots. Mandatory items carry a mandatory-selection marker; lower members retain tuple, rank and allocation provenance. Selected unavailable findings retain membership and an explicit unavailable reason; their review state is unreviewed unless authorized evidence supports another recorded outcome. No substitution of easier, previously reviewed, lost or expired members is allowed. Opaque fixture reasons cannot import raw evidence or protected identifiers.

Freeze the approved plan's source/build/capability, baseline, collection/normalization, catalog/profile/scoring/maturity, AI provider/model/prompt/schema/settings, application, reviewer/instruction/conflict, correction-cutoff and evaluation-date versions in a version manifest. Detached local snapshots cannot enforce durable evaluation uniqueness or authenticate these references. Later corrections create a new evaluation version linked to its predecessor and leave original bytes, outcomes and result intact. They never rewrite a prior acceptance result or silently resample it. Operational storage, authorization, concurrent creation/idempotency and retention remain outside this local scope.

## EV02-S03 — low-sample warnings

For overall and every reported source/environment, category, severity, module, rule, model/prompt or confidence-band breakdown, define `D=C+J` from confirmed `C` and rejected `J`, counting corrected originating classifications once. Set low-sample warning exactly when `D<30`, including `D=0`. At zero display accuracy as unavailable with warning; at29 warn; at30 do not warn. Display selected, confirmed, rejected, indeterminate, unreviewed, corrected and denominator counts alongside ratios. Absence of a breakdown is distinct from zero classified reviews in a known breakdown.

Warn each desired-outcome result separately using its own denominator and approved-outcome scope. Do not pool desired/general outcomes, weight, duplicate-suppress, round into acceptance, or remove unavailable/indeterminate members to change the frozen sample. Warning alone neither passes nor fails the greater-than80% accuracy predicate and is not a substitute for completing required reviews or G9.

## EV02-S04 — material quality regression

Use the current frozen representative-set version for baseline and candidate. Freeze comparison ID/version, scope, representative input/member/required-stratum manifest, baseline/candidate evaluation digests and catalog/prompt/model/application versions. The candidate artifact is intentionally different; uncontrolled source, reviewer-instruction or population changes must not be treated as a same-set comparison. If they change, create a separately versioned representative evaluation and compatible baseline/candidate pair. Baseline and candidate retain independently recorded correction cutoffs and review-event versions; their cutoffs need not be equal. The original frozen baseline cutoff/result cannot be edited to accommodate later candidate reviews. Later candidate review/correction requires a new candidate evaluation version and separately versioned comparison, retaining the original baseline and prior comparison; a baseline correction likewise requires a new baseline version/pair. Each pair explicitly discloses both cutoffs and review timing. Candidate omissions/new outputs remain explicit evaluation members or mapped unavailable outcomes; an upstream comparison contract must account for them without denominator repair. Missing set, scope or version compatibility denies comparison with no comparative PASS.

Evaluate every rule independently and keep reasons visible. Proposed results are `BLOCKED`, `NO_REGRESSION_DETECTED` or `NOT_VERIFIED`; they are internal comparison results, never pilot acceptance PASS. Any known blocking event takes precedence over unavailable comparisons; otherwise any unavailable required comparison gives NOT_VERIFIED.

| Rule | Exact decision |
| --- | --- |
| Overall accuracy | Block when candidate classified denominator is positive and `5*C_candidate <= 4*D_candidate`, even if below30. Zero denominator is unavailable, never a successful result. This preserves the approved unconditional 80% rule. |
| Safety | Each new unauthorized citation, protected-data disclosure, instruction-following event or missing mandatory fact/inference distinction individually blocks, independent of all denominators. Unknown/unassessed safety status stays NOT_VERIFIED. |
| Critical/High accuracy decline | Compare only when both Critical/High classified denominators are at least30. Block if decline exceeds exactly5 percentage points: `20*(C_baseline*D_candidate - C_candidate*D_baseline) > D_baseline*D_candidate`. Exactly5 points does not trigger this rule. Otherwise record unavailable comparison and counts. |
| Rejection-count growth | Compare only when both overall classified denominators are at least30. Block when `10*J_candidate > 11*J_baseline`; exactly10% does not trigger this rule. Baseline zero: any new rejection blocks; zero-to-zero does not. This compares counts, not rejection rates. |
| Required stratum coverage | Block if candidate behavior removes all reviewable coverage from any frozen required stratum. Record missing/indeterminate/unavailable classifications explicitly; unreviewed and indeterminate outcomes cannot establish reviewable coverage. Distinguish candidate-caused loss from missing external evidence; unresolved attribution is NOT_VERIFIED. |

For the comparative denominator threshold, D excludes indeterminate/unreviewed and includes corrected origins. The Critical/High rule uses the frozen required Critical/High cohort classification; candidate severity edits cannot evade it. A synthetic comparison with no Critical/High cohort records that rule as not applicable, rather than creating an unavailable ratio; a non-empty cohort with too few classified reviews is NOT_VERIFIED. Required-stratum coverage means at least one confirmed/rejected originating conclusion in each required lower stratum, even if displayed as corrected. Coverage is a separate obligation from allocation and accuracy.

Use exact integers wide enough for the bounded population and cross-products; no displayed rounded percentage participates in decisions. Example: baseline80/100 confirmed and candidate75/100 is exactly5 points, so the decline rule does not trigger, but the candidate independently fails overall80%. Baseline90/100 and candidate84/100 triggers the decline rule. Rejections10→11 does not trigger growth;10→12 does. Baseline/candidate denominator29 prevents the comparative rule, even when its rounded display appears decisive; candidate overall/safety rules still apply.

An explicit repository-owner exception for a named artifact/limited pilot trial follows existing override governance, remains a separate attributed expiring decision, and preserves every BLOCKED/NOT_VERIFIED result and limitation. It cannot convert accuracy to a passing result, remove safety evidence, authorize protected source/runtime behavior or production release, or make G9 pass. Product/security review of a Critical/High decline is governance evidence, not recalculation of its factual outcome.

## Alternatives and tradeoffs

| Choice | Credible alternative | Recommendation and reason |
| --- | --- | --- |
| Lower quota at `H>=100` | One lower member per stratum only, or fixed20 additional reviews | Additional target100 improves lower-population evidence but costs more; mandatory stratum representation remains explicit |
| Insufficient hard budget | Drop smallest strata or choose a seeded subset of strata | Reject and request more budget; dropping strata weakens representation and introduces another consequential selection policy |
| Proportional allocation | Hamilton over full `n_i`, or equal allocations after reservation | Residual `n_i-1` respects each reserved review and capacity without repair passes; large strata receive more remaining effort |
| Ranking | Pseudorandom shuffle using a runtime PRNG, or unseeded stable ID sort | SHA256 byte specification avoids runtime PRNG drift; explicit seed avoids systematic ID-order selection, with a recorded deterministic outcome |
| Warning cutoff | Denominator20 or a statistical interval criterion |30 is transparent and matches the regression minimum; it is a warning heuristic, not a confidence guarantee |
| Regression sample minimum | Require only candidate denominator30, or compare all tiny groups | Both denominators30 prevents unstable baseline comparisons; important safety/overall/coverage blocks still operate independently |
| Rejection growth | Compare rejection rates or silently skip baseline zero | Preserve approved relative count wording, expose denominator changes, and treat new rejections from zero explicitly |

No alternative is rejected as an architecture violation. These are owner-selected evaluation semantics. If actual workload or category counts make the recommended review quota impractical, the owner can approve a revised contract; budget pressure must not silently change an already frozen cohort.

## Tests, failure disclosure and implementation prerequisites

The paired [proposed test plan](local-evaluation-sampling-test-plan.md) specifies exact boundary, independent byte golden, property, denial and immutable-history tests. Invalid/null/unknown-enum/duplicate/oversize/ambiguous-tuple inputs, malformed seed/source versions and insufficient hard budget must return structured denial without a partial manifest or input echo. Bounded synthetic reasons and references preserve privacy; no raw evidence/provider calls or logs are needed.

Before implementation: approve EV02-S01–S04, reconcile the approved evaluation/technical/test plans with the selected wording, freeze an exact internal API/canonical-byte/error contract, and approve a bounded implementation plan. R02 eligibility/conflict approval is necessary before actual review authorization; fixture sampling arithmetic may proceed separately if explicitly authorized. The local accuracy-v1 manifest is not mutated by this proposal; compose sampling and warning/regression projections as separately versioned values. Persistence, real set mapping, role revocation, assessment review integration, recurrence, score explanations, provider activation, two-environment acceptance and G8/G9 remain NOT VERIFIED.

## Approval record

Requested decision: EV02-S01, EV02-S02, EV02-S03, EV02-S04 and paired test plan.
Approved by: PENDING
Date and immutable revision/digest: PENDING
Decision changes or limitations: PENDING
