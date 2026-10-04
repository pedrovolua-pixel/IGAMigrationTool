# Internal synthetic evaluation accuracy contract

Status: Settled engineering contract for the approved Milestone08 arithmetic subset
Date: 2026-10-03
Basis: FR-HAS-51–53; AC-HAS-12; TP-HAS-012; approved evaluation-plan sections Frozen evaluation record, Review outcomes, Accuracy calculation and Desired-outcome evaluation.

This value contract is confined to internal synthetic tests. Public operations, sampling defaults, reviewer permissions, live acceptance and storage authority remain outside its scope.

## Exact API

Namespace `SyntheticEvaluation`; dependency-free net10 module. Public enums:

- `EvaluationTrack`: `GeneralAi`, `ApprovedDesiredOutcome`.
- `EvaluationReviewOutcome`: `Confirmed`, `Rejected`, `Indeterminate`, `Unreviewed`, `Corrected`.
- `EvaluationOriginClassification`: `Confirmed`, `Rejected`.
- `EvaluationOutcomeApproval`: `CustomerApproved`, `Draft`, `Inferred`, `ConsultantReviewed`.
- `EvaluationIssue`: `InvalidInput`, `InvalidReference`, `InvalidVersion`, `InvalidMember`, `DuplicateMember`, `MissingReview`, `UnexpectedReview`, `DuplicateReview`, `InvalidReview`, `InvalidDesiredOutcome`.

Input records, positional properties in order:

```csharp
EvaluationLocks(string EvaluationId, string ScopeId, string PopulationDigest,
    string SampleDigest, string VersionManifestDigest, DateTimeOffset CorrectionCutoffUtc);
EvaluationMember(string Id, EvaluationTrack Track, string? DesiredOutcomeVersion = null,
    EvaluationOutcomeApproval? DesiredOutcomeApproval = null);
EvaluationReview(string MemberId, EvaluationReviewOutcome Outcome,
    EvaluationOriginClassification? OriginatingClassification = null);
EvaluationInput(EvaluationLocks Locks, IReadOnlyList<EvaluationMember> Members,
    IReadOnlyList<EvaluationReview> Reviews);
```

`EvaluationAccuracyBuilder.Build(EvaluationInput? input)` returns `EvaluationResult(EvaluationIssue? Issue, EvaluationProjection? Projection)`, with `HasProjection` true only for success. Any denial has no projection or echo of input, including IDs. Result contains no full acceptance PASS/FAIL.

`EvaluationProjection` exposes detached immutable Locks/Members/Reviews, `GeneralAi`, `DesiredOutcome`, `CanonicalJson`, `ContentDigest`. `EvaluationAccuracy` has `Selected`, `Confirmed`, `Rejected`, `Indeterminate`, `Unreviewed`, `Corrected`, `Denominator` (int), `ConfirmedAccuracyPercent` (decimal?), `ExceedsGeneralAiThreshold` (bool?). Constructor(s) of projection/accuracy need not be public.

## Admission and arithmetic

At most100000 members/reviews each. Null input/collections/entries deny. EvaluationId, ScopeId, Id, MemberId and any DesiredOutcomeVersion must be ASCII `synthetic-` followed by 1–118 characters `[a-z0-9._-]` (total maximum128). No normalization, whitespace, URL or payload accepted. Three source digests are exactly64 lowercase hexadecimal characters. Cutoff must have UTC offset zero; source/version bindings are opaque fixture metadata, not integrity/authorization proofs.

Member IDs unique across both tracks. Exactly one explicit review for each member, including unavailable members represented explicitly as Unreviewed; missing/extra/duplicate reviews deny. All enum values must be declared. GeneralAi forbids both desired fields. ApprovedDesiredOutcome requires an opaque version plus CustomerApproved fixture classification; other classifications deny. These fixture fields cannot establish real customer approval.

Corrected requires Confirmed/Rejected originating classification; every other outcome forbids it. Corrected increments the original confirmed/rejected classification exactly once, and separately increments Corrected as an overlapping subset. Indeterminate/Unreviewed remain visible and do not enter denominator. Each track is counted separately, with no weighting, confidence, affected-object counts, deduplication or substitution. Empty inputs succeed with zero counts and unavailable ratios.

Percent =100m*Confirmed/(Confirmed+Rejected), unrounded decimal. For GeneralAi, strict threshold uses exact integer comparison `5L*Confirmed > 4L*Denominator`; denominator zero yields null ratio/threshold. DesiredOutcome always has null threshold; no80% desired-outcome policy is approved. No low-sample warning cutoff is chosen here.

## Canonical bytes and immutability

Snapshot input collections before processing, never return mutable arrays/dictionaries. Sort members and reviews by member ID using ordinal comparison. Use System.Text.Json Utf8JsonWriter, no indentation/newline, default encoder. Serialize exact property order:

1. `schema`: literal `synthetic-evaluation-accuracy-v1`.
2. `locks`: evaluationId, scopeId, populationDigest, sampleDigest, versionManifestDigest, correctionCutoffUtc (format `O` with invariant culture).
3. `members`: each id, track (enum name), desiredOutcomeVersion (string/null), desiredOutcomeApproval (enum name/null).
4. `reviews`: each memberId, outcome (enum name), originatingClassification (enum name/null).

Digest is lowercase SHA256 of exact canonical UTF8 bytes. Counts are derived, not separately hashed. Reordering inputs preserves bytes. Changed outcome/lock/membership changes bytes/digest; prior detached projection remains unchanged. This pure module cannot enforce evaluation-ID uniqueness or immutable persisted history. A later durable integration must create a new evaluation version for corrections under the approved plan.

## Executable acceptance

Independent literal JSON and SHA256 golden; reordered and mutated original collections; every outcome/origin combination; zero/one denominator; exact80% and near-boundary counts; complete counts; split desired/general; duplicate/missing/extra/invalid/null/oversize inputs; exact reference/digest/cutoff boundaries and customer-approval lifecycle denial. Non-author review and coordinator execution bind the tests to final source. Sampling/reviewer/low-sample/regression/integration/live gates remain unverified.
