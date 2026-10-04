# Local pilot consultant planning tasks — Cycle14

Status: COMPLETE — approved bounded local developer cycle and confirmed owner-private publication; full milestones/gates NOT VERIFIED
Owner: Coordinator
Last updated: 2026-10-03
Audited source: `711e38d84ac1c626d3695a7a442f36053c2c9729`
Product/technical basis: approved feature003 specifications and local synthetic build direction
Recorded decision: [TC14-01–06 approval](../../specs/003-health-assessment/local-planning-task-approval.md); original [TC14-01–06](../../specs/003-health-assessment/local-planning-task-contract-proposal.md#requested-decision)
Test plan: [bounded task verification](../../specs/003-health-assessment/local-planning-task-test-plan.md)

## Intended outcome and boundary

After exact approval, explicitly convert one currently reviewed fictional recommendation-option package into one durable Consultant planning task. The task retains original provenance, current source/attestation freshness, minimal workflow, append-only comments/history and exact-command replay. Completing planning work does not change finding disposition, artifact review, health/maturity or remediation validation. Ten historical profiles retain compatibility.

Preparation audits the committed requirements and constructs the reviewable missing decisions. It does not implement code, initialize a schema, alter a permission or claim runtime acceptance. TC14-06 proposes a future CSV authority correction to match the approved matrix; export implementation and its remaining contracts are deferred. Priority/effort, reassignment, attachments/mentions, arbitrary task edits, external task systems, source/provider access, execution and production/customer activation are outside this cycle.

## Requirement traceability

| Work | Requirements/criteria | Planned evidence |
| --- | --- | --- |
| Explicit grouped option conversion and complete source/provenance | FR-HLT-13/21; FR-HAS-35/37/42; FR-TSK-1/3; NFR-EVD-5; AC-HAS-18; IP-HAS-008 | TC14-T01/T03/T04/T08/T11 |
| Scope/actor/action/category/resource enforcement and payload-free telemetry | FR-HAS-50; NFR-SEC-5/10; approved matrix/audit policy; AC-HAS-14 | TC14-T02/T12 |
| Atomic durable task, source/attestation reconfirmation, lifecycle/comments/replay | FR-HAS-37; technical ConsultantTask/ADR0003; Milestone7 | TC14-T03–T07 |
| Inert controls, keyboard/late replies/current versus frozen originals | FR-HAS-40/41/42; AC-HAS-13/18 | TC14-T07/T09/T10 |
| Compatibility, opt-in and future CSV role reconciliation | FR-HAS-37; AC-HAS-14/18; TP-HAS-002/003/014/017/018 subsets | TC14-T08/T11; documentary TC14-06 approval, never export PASS |

## Preparation checkpoint

- [x] Read committed Cycle13 closure, approved requirements, task records, architecture and security policies.
- [x] Independent product and authority audits identify missing exact task semantics and the CSV-role contradiction.
- [x] Prepare explicit proposal, test matrix and bounded parallel ownership packets.
- [x] Documentary/source/link/whitespace/secret checks and independent concrete-proposal review complete with source-bound evidence.
- [x] Required human TC14-01–06 decision recorded against exact proposal revision/digest.

The canonical [preparation metadata](../../docs/development/evidence/local-pilot-cycle14-preparation.json) records documentary evidence only. No prior Cycle13 execution is relabeled as Cycle14 runtime evidence.

## Parallel implementation packets — frozen engineering contract

| Packet | Exact ownership after contract freeze | Acceptance/dependencies | State |
| --- | --- | --- | --- |
| A14 task domain/store | New `src/server/modules/SyntheticPlanningTasks/*.cs` and README; new additive `migrations/planning-tasks/001-initial.sql`; new `tests/unit/SyntheticPlanningTasks.Tests` except project/lock/config | Approved TC14 contract; frozen source/attestation callback, DTO, schema, identity and replay contract; atomic history/current/metadata receipt, policy/transition/integrity/recovery tests | VERIFIED — reviewed and executed bounded packet |
| B14 task presentation | New `src/web/src/PlanningTasksPanel.tsx/.css`; new `tests/unit/PlanningTasksComponent.Tests` except project/lock/config | Approved exact DTO and command forms; task status versus source freshness, original versus latest binding, inert full text/focus, frozen Retry and conflict behavior | VERIFIED — reviewed and executed bounded packet |
| V14 independent verification | New `tests/integration/LocalPlanningTasks.Tests` except project/lock/config; new `tests/e2e/planning-tasks` | Independent source/task/attestation/transition byte oracles authored before using author expectations; actual owned database/host/browser/concurrency/recovery; all historical regressions | VERIFIED — reviewed and executed bounded packet |
| Coordinator | Shared projects/locks/solution/CI; narrow owning-module transaction/capture APIs; profile/source/DTO/type integration; canonical records, integration/evidence/preservation and existing owner-private Site | Same known approved checkpoint; preserve concurrent root UI/BFF/research; scoped non-author review; executed combined checks and confirmed private publication | VERIFIED — reviewed integration/local/hosted checks; private publication recorded separately |

Writing workers receive isolated `codex/` worktrees, explicit path lists and traceable packets. Read-only audits may share the committed coordinator source. Use fewer workers for dependent contracts; no separate chats, schedules or unattended agents are started. Workers do not edit canonical records or operate the Site.

## Mandatory engineering checkpoint after approval

Before dependent implementation, freeze the exact new profile/application/task-contract digest and per-profile saved-input inventories, conversion/source/selected-attestation canonical recipes, task identity, action/receipt/Already exists schemas, bounded field/body/revision limits, event namespace, schema fingerprint, unavailable/history-read rules and transaction composition. Check source monotonicity, selected attestation changes without package changes and rollback without historical rewrite.

The existing artifact ReadAsync acquires its own fence on a separate transaction. Do not call it from a task transaction already holding that fence. Define an owning-module trusted read in the same supplied connection/transaction, without cross-module SQL or premature/cached review. Artifact/finding/assessment writers and task creation/reconfirmation must share one source-capture fence. New profile acceptance remains explicit; earlier source/contract/golden inventories do not widen generally.

If this checkpoint discovers a consequential product/permission/architecture departure, update the decision before coding. Accepted ADR0001/0002/0003/0004 remain in force; no new dependency or deployment topology is currently justified.

## Verification and closure after approval

- [x] TC14-T01–T12 exact executed PASS/FAIL/NOT VERIFIED per source/environment.
- [x] Pinned locked audited restore, formatting, type/lint/build, unit/portable/owned PostgreSQL, architecture, secret, collector and infrastructure checks.
- [x] Actual task browser flows plus all ten historical profiles and prior applicable browser suites; inspect desktop/mobile320 captures and separately report axe violations/incomplete.
- [x] Configured Linux, Windows2022/2025 and container results bound to exact code; no manual/deployed claim from automated tests.
- [x] Non-author findings closed without weakened assertions; original source/binaries/consumed fixtures/native logs/failures and reviews preserved and rehashed before clean checkout removal.
- [x] Canonical status/plan/evidence/operations current; matching owner-private Site status and human task board published and confirmed.

Full Milestone7, TP-HAS-018, local pilot completion, UAT readiness, supported manual accessibility, deployed ADR0001 isolation, default Mac startup and G1–G9 remain NOT VERIFIED. Do not restart shared PostgreSQL or change roles/HBA/another session's host/database.

## Migration, rollout and rollback

No schema is applied by preparation. After approval, use only a fresh dedicated Cycle14 synthetic database, additive task-owned migration and explicit task flag/profile. Preserve all old migration/input/golden bytes and append-only artifact/finding/task history. Rollback disables the new profile/actions while retaining compatible data/reference readers; no downmigration, purge or customer migration. Customer retention, identity and activation remain separate gated work.

## Resume instruction

The approved implementation, bounded developer evidence and owner-private publication are complete below. Resume from the canonical local-build plan for any next approved slice. Full pilot acceptance and eleven existing human dependencies remain open.


## Frozen engineering checkpoint

Contract SHA256 `f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2` at `specs/003-health-assessment/local-planning-task-implementation-contract.md` is frozen after A14/B14/V14 independent documentary reviews closed. Exact receipts are in `docs/development/evidence/local-pilot-cycle14-contract-freeze.json`. Approval board publication is confirmed owner-private version88; `local-pilot-cycle14-approval-site.json` records exact source/access. Four new project scaffolds restore with existing package versions only. At contract freeze, runtime implementation and migration checks were pending. The bounded executed closure below supersedes that historical preparation state; no historical test is relabeled.

## Executed bounded closure — 2026-10-03

The approved profile now explicitly converts one recommendation option into its single durable assigned Consultant task after all three current artifacts are reviewed. Source freshness remains separate from Planned/InProgress/Completed/Cancelled workflow. Reconfirmation, comments, terminal reopening and exact historical-receipt replay preserve original provenance and append-only history; completion does not validate remediation or alter findings, health, maturity, artifact review or reports. Missing current source reveals only verified task metadata and disables actions. The ten historical profiles retain their saved inputs/locks and omit the new task field. TC14-06 corrects future CSV actor wording only; export remains deferred.

The first hosted artifact-review replay exposed a test synchronization race: an already-visible Retry control did not prove the asynchronous finding save had refreshed the source. An independently reviewed verifier-only barrier now waits for the committed comment, advanced source digests and exact displayed source before Retry; production source-epoch protection is unchanged. The original failure is preserved separately from final hosted PASS.

Non-author review closed the authority-before-history/replay, full same-package binding consistency, retained historical artifact-prefix integrity, dependency-decoding recovery and controlled-barrier cleanup findings without weakening goldens. A14 passed 736 portable and 1249 inclusive fresh-owned-PG assertions; B14 passed 737 component assertions / 30 accepted / 209 denied; independent V14 passed 702 portable, 2542 inclusive dedicated-PG and 13626 actual macOS Chromium assertions. Counts overlap and are not summed. All eight task actions were exercised with Enter/Space; reasons, revisions, whole source/vector/history, current versus frozen plan, error/success focus, lost Retry, conflict/runswitch/source-epoch and actual owned-host restart were checked. Nine desktop/mobile390/320 captures were inspected through full viewport images and representative readable crops of tall regions; three axe records each reported zero violations and zero incomplete. Manual supported accessibility remains open.

The 62-project audited locked restore/current 127 dependency-byte agreement, default whole-solution/frontend formatting, zero-warning/error Release build, schema/type/frontend build, dependency and frontend vulnerability audits, architecture/current portable regressions, independent earlier frontend components, secret/infrastructure policies, Windows cross-publish and actual bootstrap process probes passed. [Configured Linux/Windows2022/2025](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37097534191) and [non-root container checks](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37097534187) passed on exact code`b10d7b28e10dccf379bd2922b04ac8a4eca8c0c2`, including all historical PostgreSQL/browser workflows and the new planning-task browser. No shared local cluster restart or historical database modification was performed.

[Canonical execution evidence](../../docs/development/evidence/local-pilot-cycle14-developer-checks.json) binds original author/non-author source, tool/runtime/compiled closures, consumed fixtures, failures, combined executions, hosted logs and independently rehashed archives. All 51 concurrent UI/BFF/research files were preserved; the two UI integration overlays have exact reverse proofs and their combined working UI build passed. That concurrent overlay remains outside the published pilot source. Clean temporary checkouts are removed only after preservation/review completes; private status publication is confirmed separately in the same cycle.

Only fresh dedicated synthetic databases receive the additive task migration (`synthetic_planning_tasks`, SQL SHA256`2ac4f0cebcf290dc08aefc809103a84bc493da6ab61bd6ce4b041c94ad2c4853`) through`--enable-synthetic-planning-tasks`. Rollback disables that profile/flag while retaining compatible readers and immutable records. No new dependency, customer/production migration, retention decision, CSV/external tasks/provider access, remediation execution, production release or gate approval. Default Mac startup, hard-kill/powerloss/shared-cluster recovery, full Milestone7/TP018/localpilot/UAT/G1–G9 and deployed isolation remain NOT VERIFIED.

The configured Linux replay separately passed 10,617 artifact-review and 13,654 planning-task browser assertions on the final code. Those are distinct from local macOS and controlled investigation counts; none are summed.

## Confirmed private publication

The matching status and eleven-task board was confirmed owner-private after executed desktop/mobile320 layout and completed task-navigation checks. The [publication receipt](../../docs/development/evidence/local-pilot-cycle14-completion-site.json) records exact saved source, deployment and QA. This closes the bounded developer cycle only; no full milestone, UAT or G1–G9 acceptance changed.
