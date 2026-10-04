# Proposed local evaluation sampling and regression test plan

Status: PROPOSED — tests are specified, not implemented or executed
Date: 2026-10-03
Packet: S02, Milestone08 cycle02
Contract: [exact sampling/regression proposal](local-evaluation-sampling-contract-proposal.md)

## Scope and authority

This is the proposed test contract for EV02-S01–S04. It extends the local synthetic subset of FR-HAS-51–53, AC-HAS-12/19 and TP-HAS-012/019 without claiming review authorization, reassessment integration, live environment/provider correctness or G9 acceptance. [Existing accuracy-v1 tests](synthetic-evaluation-accuracy-contract.md#executable-acceptance) remain separate and unchanged. Tests below are future obligations; any documentary arithmetic check reported separately is not product verification.

Implementation begins only after the exact owner decision and bounded implementation/test plan are approved. A non-author verifier supplies independent arithmetic/byte oracles; a non-author reviewer inspects final source/evidence. The coordinator owns shared projects/CI, integration, canonical records and the private Site under [parallel workflow](../../docs/development/parallel-agent-workflow.md). Tests use only synthetic IDs and fabricated outcomes, no network, credentials, customer evidence or database.

## Traceability and exact expected cases

| Test ID | Decision and source | Cases and required oracle |
| --- | --- | --- |
| TP-EV02-S001 | S01; FR-HAS-51, TP-HAS-012 | N0 gives empty unavailable result; N1/99 review all; N100 reviews all; each declared severity and arbitrary H partition; no confidence/disposition/affected-object filtering |
| TP-EV02-S002 | S01; FR-HAS-51, AC-HAS-19 | N150/H20/L130/S4 gives100 total; N250/H120/L130/S4 gives220; N400/H120/L280/S150 gives270; N180/H120/L60/S4 gives180; H99/100/101 boundaries; L0, S0; all mandatory and at least one each lower stratum |
| TP-EV02-S003 | S01; TP-HAS-012 | Omitted hard cap succeeds; cap required-1 denies without partial sample; cap exactly required and required+1 give identical membership; cap0 valid only for empty population; N99 cap98 denies; huge cap cannot increase quota; malformed/negative caps deny |
| TP-EV02-S004 | S02; evaluation allocation | Sizes2/4/5, k7 yield2/2/3 in tuple order; equal remainders use literal ordinal tuple tie; zero residual weights get none; R0/W0 and saturated k=L avoid division errors; deliberately distinguish residual weights from full-size weighting |
| TP-EV02-S005 | S01/S02; TP-HAS-012 | Enumerate feasible small strata/counts independently: allocations sumk, each1..n_i, total selectedH+k, every mandatory included, unique IDs, no omitted strata; sample never exceeds population; population permutations preserve bytes/IDs |
| TP-EV02-S006 | S02; Frozen evaluation record | Independent literal complete ranking-byte golden, SHA256 digest and selected-ID golden; verify domain terminator, decoded32-byte seed, field order, big-endian lengths, tuple strings and lowercase population-digest text; use offline independently computed golden, not implementation helper |
| TP-EV02-S007 | S02; deterministic selection | Reordering members/strata, changing process culture and repeated builds preserves canonical bytes/selection; targeted seed/scope/population/member/version changes produce expected independently computed fixture differences; a changed seed is not required to change membership for all populations |
| TP-EV02-S008 | S02; tie and input identity | Independently force a digest-tie comparator unit fixture to confirm ordinal scoped-ID order without weakening production SHA256; same member with multiple tuples denies; primary tuple absent/ambiguous denies; secondary module provenance cannot alter primary membership; no silent duplicate suppression |
| TP-EV02-S009 | S02; validation/privacy | Null records/collections, unknown severity, duplicate ID, wrong scope, invalid synthetic reference grammar, over128 chars, malformed/non-lowercase/non64-hex seed or digest, missing version, oversize100001 and checked arithmetic errors deny without input echo/partial projection; exactly100000 exercises maximum products |
| TP-EV02-S010 | S02; FR-HAS-52, frozen history | Mutating caller member/stratum/version arrays after success cannot change saved bytes/digests; unavailable selected member remains present and explicitly unreviewed; no substitution for prior-reviewed/easier/lost/expired items; correction produces new version with predecessor and preserves original result |
| TP-EV02-S011 | S03; AC-HAS-12, TP-HAS-012 | D0 unavailable+warning, D1/29 warning, D30/31 no warning; selected count100 with99 indeterminate still warns atD1; corrected counts once by origin; warning uses D, not selected/reviewed total; every existing breakdown and separate desired scope covered |
| TP-EV02-S012 | S03; FR-HAS-51/53 | Exactly80% remains failing despite warning/no-warning; >80% predicate unchanged; general and desired never pool; desired result has no general80% acceptance predicate; no weighting/rounding/deduplication or affected-object substitution |
| TP-EV02-S013 | S04; overall rule | Candidate4/5 blocks at80%;5/6 does not trigger overall rule; D0 unavailable; D1 rejected blocks despite low sample; below30 overall rule independent of comparative minimum; display rounding never decides |
| TP-EV02-S014 | S04; Critical/High decline | Both CH denominators>=30: baseline90/100 versus85/100 no decline block,84/100 blocks; unequal denominators with exactly5 points and nearest feasible values on both sides; negative decline does not block; eitherD29 unavailable, D30 enables; frozen CH classification survives candidate downgrade; empty CH cohort not applicable, non-empty unclassified cohort unavailable |
| TP-EV02-S015 | S04; rejection growth | Both overallD>=30: rejections10→11 no growth block,10→12 block;0→0 no growth,0→1 block; unequal denominators retain count semantics; eitherD29 unavailable; independent overall/safety outcomes retained |
| TP-EV02-S016 | S04; safety | Each of the four safety events individually blocks even with D0, D1 or missing comparison; combinations report every reason; unknown safety NOT_VERIFIED; absence of review cannot be reported as safe; exception metadata cannot erase event |
| TP-EV02-S017 | S04; required strata | Every frozen required lower tuple has confirmed/rejected coverage; corrected origin establishes coverage once; candidate-caused loss to unavailable/unreviewed/indeterminate blocks; disappeared output cannot silently remove stratum; unresolved cause gives NOT_VERIFIED; source changes require new compatible evaluation set |
| TP-EV02-S018 | S04; freeze/comparability | Different representative-set/scope/input/required-strata/reviewer-instruction versions deny comparison; changed candidate artifact version is intentional and retained; independently recorded baseline/candidate cutoffs may differ; candidate reviews after its frozen cutoff require a new candidate version/comparison without editing the original baseline; baseline/candidate digests/locks immutable; correction cannot overwrite prior pair; missing/new output mapping cannot repair denominator |
| TP-EV02-S019 | S04; TP-HAS-019/override | BLOCKED takes precedence over unavailable reasons; otherwise unavailable required comparison gives NOT_VERIFIED; NO_REGRESSION_DETECTED is never acceptance PASS; named owner exception keeps factual failures and cannot satisfy G9, authorize production/source/runtime or convert accuracy result |
| TP-EV02-S020 | S01–S04; full limits | No live API, host route, durable authority, role grant, database migration, release activation or mutation of accuracy-v1; run prior accuracy suite to prove denominator/desired-origin/canonical bytes remain compatible |

## Independent oracle detail

For quota properties, calculate expected `k` from literal fixture counts and directly enumerate selected all/mandatory/stratum obligations. For allocation, use an independent exact-rational implementation or hand-checked table, not a copy of the product allocator. Include singleton-heavy populations, one huge stratum, all equal sizes, k=S, k=L, H far above100 and S above100. Verify the capacity bound `1+floor(R*w_i/W)+extra <= n_i` and that Hamilton extras go only to the largest exact remainders with the specified ties.

For rank/canonical goldens, freeze at least two lower strata and several eligible members per stratum; include literal expected bytes and digest, not generated-at-runtime expected values. Fixture identifiers obey the strict synthetic grammar. The later exact API/canonical population contract must be settled before completing these goldens; ranking is already byte-specified in this proposal. Byte tests must detect seed-as-ASCII, missing domain terminator, little-endian lengths, locale comparisons and hashing only memberId. Hash collision resistance is not established by tests; comparator tie behavior is tested independently without an injectable production selection bypass.

For regression boundaries, generate rational cases independently and verify exact cross-products. Include denominators30 and100000, exactly5 percentage points, immediately feasible greater/smaller declines, exactly10% growth and integer rounding edges. Never use formatted percentages as the oracle. Confirm all event flags independently; an unavailable comparison may coexist with a known block and does not hide it.

## Future executed evidence and completion

The later implementation cycle must record final source/fixture/reviewer revisions, original failures and corrections, exact test command/output, golden digests and non-author review. Run applicable pinned locked audited restore, formatting, zero-warning build, focused unit/property tests, architecture checks and secret/dependency/license checks, plus unchanged-module regressions justified by integration. Browser/database/live/security-boundary E2E checks are not applicable to a pure synthetic module; their supported product obligations remain NOT VERIFIED. No check is marked PASS here.

A successful local packet would verify only supplied synthetic cohort/count/projection semantics. It cannot establish installed modules, qualified reviewers, actual permission, live provider behavior, representative-set adequacy, required completed live reviews, recurrence/score explanations, or two eligible environments. Milestone08, Phase1C, UAT and G8/G9 remain open until their independent evidence passes.

## Approval record

Product/evaluation owner decision on EV02-S01–S04 and this test plan: PENDING.
Technical/security review and immutable revision/digest: PENDING.
