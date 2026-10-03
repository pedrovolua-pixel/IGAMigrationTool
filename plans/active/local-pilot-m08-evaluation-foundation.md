# Milestone08 synthetic evaluation accuracy foundation

Status: RUNNING — approved local implementation, bounded arithmetic subset
Owner: Milestone08 coordinator
Started: 2026-10-03
Known base: `0185a6c`
Authority: approved feature003 product/technical/implementation/test/evaluation plans and repository owner's “Start 08” direction.

## Outcome and dependencies

Implement a standalone, dependency-free synthetic evaluation module. Capture an explicitly supplied sample and its exact outcomes in detached immutable values; preserve correction classifications and separate general AI accuracy from customer-approved desired-outcome accuracy. Bind canonical bytes/digest to synthetic source/version references. No sample selection or acceptance record is created.

This slice advances FR-HAS-51–53, AC-HAS-12 and the arithmetic/frozen-outcome subset of TP-HAS-012. It can proceed independently of Cycle14 task integration and BFF deployment. Cycle14 is now locally closed at the known base; its source remains unchanged. Customer risk authority, review integration, recurrence/comparison, sampling policy, reviewer eligibility/conflicts, persistence, notifications, live environment/provider and G8/G9 remain outside this slice.

The approved evaluation plan leaves exact insufficient-budget stratum allocation, lower sample size when mandatory reviews exceed100, low-sample thresholds and regression edge rules unresolved. This implementation does not choose them. A supplied synthetic sample digest is a fixture binding, not proof of correct sampling or authority.

## Bounded packets and ownership

| Packet | Permitted paths | Outcome/state |
| --- | --- | --- |
| E08 domain worker | New `src/server/modules/SyntheticEvaluation/*.cs`, module README only | Exact internal contract; immutable capture, membership validation, canonicalization and arithmetic; RUNNING after contract freeze |
| V08 verifier | New `tests/unit/SyntheticEvaluation.Tests/Program.cs` and test README only | Independent arithmetic/property/byte goldens, denial and mutation tests; RUNNING after contract freeze |
| Coordinator | New project/lock files, solution/CI additions, integration, canonical records/evidence, private Site | Freeze contract, isolate workers from same commit, obtain non-author reviews, execute applicable combined checks |

Each writer uses a separate `codex/` worktree. Workers cannot edit shared files, canonical records, active BFF/task/UI work or operate the Site. Domain and verification workers independently review the other packet before integration. Source/evidence is preserved before clean worktree removal.

## Verification and limits

- [x] Exact count/outcome correction, zero/one denominator, exactly80%, below/above80%, and desired/general separation.
- [x] Independent complete canonical-byte/digest golden, input-order invariance and detached output mutation resistance.
- [x] Missing/extra/duplicate members/reviews, invalid enum/reference/source locks and unapproved desired outcomes deny without payload.
- [x] Pinned locked audited restore, formatting, zero-warning Release build, focused tests and architecture checks; applicable secret/dependency/license checks.
- [x] Existing unchanged modules compile; no host/browser/DB/source paths change. Runtime integration, browser, database and live checks are not applicable to this pure module; no prior check is relabeled.
- [ ] Non-author reviews closed; executed evidence/current records and confirmed owner-private publication.

No dependency, migration, runtime flag, production permission or deployment topology is introduced. Rollback removes the unused module/test registrations while preserving source/evidence. Full Milestone08, TP-HAS-012/019, Phase1C, UAT and G1–G9 remain NOT VERIFIED.

## Next human dependency — evaluation policy and reviewer procedure

Requested role: product/evaluation owner, with technical and security review of reviewer eligibility/conflicts.

Before complete sampling, regression promotion or live evaluation implementation, record the exact lower-severity review budget when Critical/High alone reaches/exceeds100; how insufficient review budget is allocated across strata; the numerical low-sample warning rule; regression denominator/zero-baseline handling; and qualified reviewer eligibility/conflict procedure. Sources: [approved evaluation plan](../../specs/003-health-assessment/evaluation-plan.md#review-population-and-sample), its Material quality regression and Current blockers sections.

Completion condition: an attributable approved decision and amended exact evaluation/test contracts settle these choices without changing frozen prior records. This dependency does not block this bounded arithmetic module. It does not authorize real source/provider use or satisfy G8/G9. The private board must carry this role, source link and completion condition alongside the eleven existing open tasks.

## Local executed checkpoint

Final 64-project locked audited restore/format/Release build passed with zero warnings/errors; 23,550 independently authored assertions, 14 portable regression hosts and architecture checks passed. Non-author domain/test/integration reviews closed. Initial test indentation and scanner prose false-positive failures are preserved; final checks pass without changed assertions/scanner policy. [Exact evidence](../../docs/development/evidence/m08-evaluation-foundation-20261003.json) records limitations. Hosted checks remain NOT VERIFIED. Private publication remains pending.
