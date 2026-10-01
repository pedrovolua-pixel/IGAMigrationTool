# Pilot Evaluation Plan: One Identity Manager Health Assessment

Status: Approved  
Owner: Product owner  
Reviewers: One Identity SME, technical owner, security owner  
Last updated: 2026-10-01

## Objective and acceptance gate

This evaluation follows the local synthetic pilot build. Environment eligibility is not a prerequisite to completing local implementation; this plan governs later live operational acceptance.

Evaluate discovery representation, assessment behavior, reassessment, score explanations and AI finding accuracy across at least two independent eligible One Identity Manager 10.x SQL Server environments. Pilot acceptance requires:

- every supported in-scope object/evidence category represented by an assessment result or explicit gap state;
- exact product builds and capability rows recorded;
- an initial assessment and later reassessment;
- review of every Critical/High AI finding plus a module/category/severity-stratified lower-severity sample;
- at least 100 reviewed AI findings, or all AI findings when fewer than 100 exist;
- confirmed AI-finding accuracy greater than 80% among reviewed non-indeterminate findings;
- recurrence and score-change explanations demonstrated;
- desired-outcome adherence evaluated separately where approved outcomes exist.

No discovery coverage percentage target is imposed. Representation completeness is an inventory-reconciliation gate, while gaps are analyzed by reason.

## Environment eligibility

Each environment must reach `pilot-validated` in `capability-matrix.md`. Independence must be evidenced; clones/restores of one source do not count twice. Environment selection should include different supported patch/hotfix levels where available and should collectively exercise the approved foundation/modules. Missing modules are not failures if accurately marked `not_applicable`; the evaluation report states which capabilities were and were not exercised.

Customer permission, data-handling authorization and consultant/reviewer assignments must be recorded before evidence use. Evaluation exports use opaque environment identifiers.

## Frozen evaluation record

The evaluation locks:

- environment evidence IDs and exact capability rows;
- baseline, reassessment baseline and collection/query/normalization versions;
- rule catalog, profile, scoring/maturity algorithms;
- AI provider/model/prompt/packet schema and relevant settings;
- application/software version;
- complete AI finding population before sampling;
- sampling algorithm/version and random seed where random selection is used;
- reviewer identity/eligibility, review instructions and conflict disclosures;
- correction cutoff and evaluation date.

Later corrections remain history but do not silently change a frozen acceptance result; a new evaluation version is required.

## Review population and sample

1. Include every AI-generated Critical and High finding.
2. Partition remaining AI findings by environment, installed module/foundation area, assessment category and severity.
3. Allocate the remaining reviews toward a total of at least 100 using proportional stratification with a minimum of one item from every non-empty stratum where the review budget permits.
4. If fewer than 100 AI findings exist, review all.
5. Use deterministic seeded selection within each stratum and retain the population/sample manifest.
6. Do not substitute easy or already-reviewed findings after selection. An unavailable item remains in the sample with its reason.

If the Critical/High population already exceeds 100, review all of it; lower-severity stratified sampling is still required unless no lower-severity AI findings exist. The evaluation report discloses population, sample, strata, selection, unavailable/unreviewed and indeterminate counts.

## Review outcomes

- `confirmed`: material conclusion is supported and useful without a change that reverses its meaning.
- `rejected`: conclusion is false, unsupported, materially misleading or cites evidence that does not establish it.
- `indeterminate`: authorized evidence/context is insufficient for a qualified decision.
- `unreviewed`: required review did not occur; never included in the accuracy denominator.
- `corrected`: reviewer changes material content; accuracy classification also records whether the originating conclusion was confirmed or rejected.

Reviewers record rationale, evidence references and any category/severity/root-cause/recommendation correction. Product severity disagreement alone does not necessarily reject the finding; the reviewer records each dimension separately.

## Accuracy calculation

`confirmed_accuracy = confirmed / (confirmed + rejected)`.

Indeterminate and unreviewed findings are excluded from the denominator and shown separately. The result must be strictly greater than 80.0%; exactly 80.0% fails. Counts and denominator are always displayed. Overall accuracy is the acceptance gate. Source/environment, category, severity, module, rule, prompt/model and confidence-band breakdowns are reported only where data exists and always include sample sizes; tiny groups receive a low-sample warning.

No weighting, confidence adjustment or duplicate suppression changes the acceptance denominator after the sample is frozen. Root-cause-grouped findings count as the reviewed finding presented to the reviewer; the evaluation also records affected-object count.

## Desired-outcome evaluation

Where customer-approved desired outcomes exist, a separate labeled evaluation checks whether evidence-to-outcome mapping and adherence conclusions are supported. These results never inflate or replace general finding accuracy. Draft/inferred/consultant-reviewed outcomes may be studied qualitatively but are excluded from scored adherence accuracy.

## Discovery and coverage evaluation

- Reconcile source inventory/category expectations to exactly one terminal coverage state.
- Review all unexplained missing keys, duplicate native IDs, unresolved relationships and `error` states.
- Report counts and rates for pass/finding/not-applicable/not-assessed/insufficient/excluded/inaccessible/redacted/unsupported/error.
- Confirm customer-specific modules receive generic checks and explicit semantic limitations.
- Confirm evidence gaps reduce quality, not default health.
- Confirm database topology and prohibited fields are absent.

## Initial-to-reassessment evaluation

For each environment, obtain a later authorized baseline with at least one known unchanged issue, resolved issue, new/worsened issue and evidence/rule/profile difference where safely available. Verify:

- stable issues correlate and preserve timeline;
- validated/rejected/accepted-risk recurrence reopens rather than creating an unlinked alert;
- ambiguous native-key changes require review;
- score differences identify source configuration/evidence/rule/profile/outcome/disposition causes;
- published prior results remain immutable;
- new/worsened Critical/High notifications occur.

No customer configuration change is performed by this product to manufacture test data. Safe fixtures or naturally occurring authorized changes may supply scenarios.

## Material quality regression

A candidate rule catalog, prompt or model is blocked from pilot promotion if any of the following occurs against the current representative evaluation set:

- overall confirmed accuracy falls to 80% or below;
- any new unauthorized citation, protected-data disclosure, instruction-following or missing mandatory fact/inference distinction occurs;
- Critical/High confirmed accuracy falls by more than 5 percentage points without product-owner and security review;
- rejection count increases by more than 10% relative while the denominator is at least 30;
- required strata lose all reviewable coverage because of candidate behavior.

These are release-governance thresholds, not a product promise that one frozen set is sufficient. The representative set is versioned as new exact builds/modules are validated.

The repository owner may explicitly override these internal promotion thresholds for a named artifact and limited pilot trial. The candidate remains marked unverified or regressed, the failed result remains in the evaluation record and user-facing pilot limitations, and the exception cannot make the pilot acceptance accuracy gate pass or authorize production release.

## Evaluation report

The immutable report includes environments/builds/capabilities, locked versions, inventory representation, gaps, AI population/sample/strata, outcomes, counts/denominators, overall and breakdown accuracy, low-sample warnings, desired-outcome evaluation, recurrence/score explanation evidence, quality regressions, reviewer conflicts/limitations and pass/fail for every gate. It contains no customer-identifying evidence beyond the authorized audience.

## Current blockers

- PILOT-ENV-A and PILOT-ENV-B are unidentified and `NOT VERIFIED`.
- No approved query pack, normalized schema, rule catalog, OpenAI API project/model configuration, prompt or implemented product exists; `gpt-6-sol` is selected but not yet configured or evaluated.
- Reviewer eligibility and conflict-management procedure are not assigned.
- Reassessment timing depends on authorized later evidence collection.

## Approval

Product owner: Repository owner  
One Identity SME: Repository owner  
Security owner: Repository owner  
Date: 2026-09-28
