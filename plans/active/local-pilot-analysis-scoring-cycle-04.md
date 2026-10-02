# Local pilot cycle 04 — deterministic analysis and scoring

Status: RUNNING, 2026-10-01.

Authority: owner directed the next phase after closed cycle 03; standing [local pilot build](one-identity-local-pilot-build.md), approved feature-003 [product](../../specs/003-health-assessment/product-spec.md), [technical](../../specs/003-health-assessment/technical-spec.md), [implementation](../../specs/003-health-assessment/implementation-plan.md#milestone-5--deliver-deterministic-phase-1b-analysis-and-reproducible-scoring) and [tests](../../specs/003-health-assessment/test-plan.md). Accepted ADR-0001–0004 remain governing. This is the first bounded Phase 1B vertical slice, not full Milestone 5 or Phase 1B acceptance.

## Runnable outcome and contract

Add fixed value-free synthetic evidence/rules to the existing loopback-only consultant host. Rules execute explicit conditions over typed fixture facts, emit pass/finding/explained gaps, and retain exact catalog, rule, evidence and generated-original provenance. Root-cause grouping retains per-object occurrences. Critical/High deterministic findings remain Proposed; eligible lower severity fixtures explicitly auto-confirm under their catalog. No actual One Identity rule is invented, promoted or activated.

Calculate the approved `pilot-health-v1` with decimal arithmetic: positive catalog unit weight, severity factors 1/.8/.45/.2/0, deterministic confidence factor 1; AI confidence clamp .5–1 is checked in pure fixtures only. Gaps and not-applicable units never improve or reduce default health; pass earns full weight. Proposed AI and mandatory-review deterministic findings participate only in provisional health. Accepted risk/remediation-pending retain the confirmed penalty; rejected findings earn full weight. A validated closure requires new passing evidence rather than a disposition alone erasing an occurrence. Category ratios remain unrounded; default assessed categories have equal weight, selected fixed comparison profile has explicit immutable category weights with unassessed-category renormalization. Empty eligible score sets show unavailable, not 100. One-decimal display and red/yellow/green boundaries follow the technical spec. Module/object-type/approved-outcome projections reuse the same units without overall duplication. Quality stays separate and reports execution/gap/review counts.

Only newly named analysis baselines/profiles opt into this behavior. Original cycle-03 presets, version locks and coverage-only behavior remain reproducible. Freeze the entire analysis fixture/catalog/profile content digest in optional run-input metadata (omitted when absent for historical serialization compatibility); refuse analysis if digest/version/results mismatch. Derive reproducible read-only analysis from canonical complete saved coverage, not partial/in-memory worker state; bind its content digest to frozen inputs and canonical result facts. No new schema migration is needed for the existing JSON input envelope. No lifecycle-completion or publication transition is added.

Extend only the private local-demo DTO/read projection. No production HTTP contract, Entra/customer authority, mutating review/risk acceptance, collaboration, actual outcome approval, maturity indicator catalog, AI/provider, tasks/fix execution, report publication or source/cloud access is granted. Display scores as synthetic provisional and publishable-current calculations, never a published report. Full review/history, maturity and remaining Phase 1B paths are subsequent bounded packets. Current user authorization permits these reversible internal demo changes; human live-contract/catalog/gate decisions remain open.

## Ownership and sequencing

Workers get isolated worktrees from one baseline and own only these paths. Coordinator owns all shared project references, solution/CI, run metadata/catalog adapter, host, frontend/private DTOs, canonical records and private Site. Workers settle their pure APIs before dependent verification; independent verification reviews math and rule provenance and tests the final integrated browser.

| Packet | Owner / checkout | Exclusive writing paths | Required evidence |
|---|---|---|---|
| S4 | assessment02 / /private/tmp/iga-cycle04-scoring | src/server/modules/AssessmentScoring/, tests/unit/AssessmentScoring.Tests/ | IP-HAS-006 / TP-HAS-003/005/007/017; independently expected severity/lifecycle/confidence/weights/gap/empty/boundary/linear/grouping/version digests |
| D4 | contracts02 / /private/tmp/iga-cycle04-analysis | src/server/modules/DeterministicAnalysis/, tests/unit/DeterministicAnalysis.Tests/ | FR-HAS-8–11/16/22–25 / TP-HAS-002/003/016; typed fixture rules, immutable originals/provenance, five rule classes, root-cause occurrences, catalog-content digest and compatibility denial |
| V4 | collector / /private/tmp/iga-cycle04-verification | tests/integration/AnalysisScoring.Tests/, tests/e2e/analysis-scoring/ | Independent golden/adversarial, durable/reload historical lock, missing/incomplete denial, actual browser scores/findings/quality/a11y/transport negatives; review other workers |
| C4 | coordinator / primary checkout | Shared integration/configuration, host/UI/DTOs, canonical records and private status Site | Integrate reviewed packets and run all applicable checks; no concurrent Azure file edits |

## Checks and exit criteria

Pinned audited locked .NET restore, formatting, full Release build and every current unit/integration/architecture host; focused actual PostgreSQL persistence/negative cases and existing portable recovery suite; pinned frontend DTO drift/type/format/build/audit; new final built browser flow and prior recovery regression; axe/reflow/keyboard checks with incomplete/manual items explicit; Gitleaks and whitespace; existing infrastructure policies and Linux/Windows partial CI. Snapshot/digest execution evidence and worker commits, review findings and resolutions, configuration/migration/rollback implications must be recorded before closure. No check is presumed passed. Full supported Windows assistive-technology/manual acceptance and production performance/live G1–G9 remain NOT VERIFIED.

Close this packet only after its local paths run, independent review is resolved, applicable checks and remote partial CI are recorded, clean temporary worktrees are removed after preserving commits, and the existing owner-private Site publication confirms a matching canonical snapshot and open human dependencies. The broader pilot and Milestone 5 remain incomplete.

## Execution record

Pending; packet APIs, review and executed results will be appended here.
