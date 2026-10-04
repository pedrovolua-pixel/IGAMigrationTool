# Phase 1D cycle04 continuation recovery verification

Status: COMPLETED — approved synthetic verification only
Owner: Phase1D coordinator
Date: 2026-10-03
Baseline: 3551fe5693196ab371282c38bae53d2f9ed23f19
Authority: owner's “Next cycle”, [exact local approval](../../specs/003-health-assessment/local-phase1d-approval.md), [P1D-T06/07/09/10/12](../../specs/003-health-assessment/local-phase1d-test-plan.md), [frozen contract](../../specs/003-health-assessment/local-phase1d-implementation-contract.md), and repository verification/parallel workflow.

## Bounded outcome

Independent review identified a composed-test gap: existing replay is sequential; blocked revocation uses a fresh manifest read; failed-audit reservation recovery uses new first pages. This cycle verifies an already-resolved continuation through selected-item loading, terminal failure, unchanged-authority retry, concurrent duplicate replay and revision changes. It supplies no actual publication or external MCP behavior. Committed source still has no native immutable publication reader/publisher; cycle02 intake remains open.

## Work packets and ownership

- C04-A writing verifier owns only new `tests/integration/SyntheticMcp.Tests/ContinuationRecovery.cs` and `ContinuationRecovery.oracle.md` in isolated `codex/p1d-cycle04-continuation` worktree. Pin expected cases from approved contract/original fixtures before implementation inspection. No runtime, fixture/golden, original Program, project, lock, workflow, canonical or site edits.
- C04-V nonauthor read-only reviewer validates expected rules, source-call/audit instrumentation, determinism, claims and source boundaries. Return findings and executed evidence; agent review does not replace human approval.
- Coordinator owns this plan, the additive existing Program entry-point call, combined checks/source preservation, canonical records/evidence and existing owner-private board. With limited local disk, reuse the same clean worker checkout for coordinator integration after author handoff; no simultaneous writers. Preserve unrelated dirty root files.

## Required cases and completion

- [x] Original page-one cursor continues to the exact ordinal item with unchanged manifest digest/scalar bindings; expected item data derives from original fixture bytes and explicit field policy, not production projection helpers.
- [x] Selected-item barriers deterministically overlap two invocations of the same handle. Both succeed with the same item/bindings, distinct opaque successor handles, separate policy evaluations and completion attempts. Compare stable content rather than random envelope bytes.
- [x] Continuation cancellation, exact request deadline, audit false and audit throw expose no content/new handle. Before original expiry, unchanged-authority retry of the original cursor succeeds. Failed audit is an attempt, not a durable completed event.
- [x] Revision change after cursor resolution/item load denies at the trusted emission fence with generic scope-free stale audit. Regrant cannot restore the old cursor; a fresh initial page uses current authority. Exercise both approved identity kinds and the resource groups that actually have successor pages in the original fixture, with exact source-call observations.
- [x] Nonauthor review closes; applicable pinned restore/format/build/oracle/MCP/architecture/security/dependency checks execute on frozen combined source. Hosted results are separately source-bound; pending checks remain NOT VERIFIED.
- [x] Original production/host/UI/contracts/migrations, original fixtures and cycle03 guard remain unchanged. Preserve original failures/logs/runtime evidence; update canonical records and confirm the existing private board deployment in this cycle.

## Limits

No expiry-during-emission policy is chosen; the test retry stays before original cursor expiry. No production free-text sanitizer, real identity/grant, native publisher/reader, SDK, transport, client selection, durable/distributed limits/cursors/audit, migration, dependency, setting or live release is added. The frozen synchronous terminal audit deadline limitation is unchanged. A verification finding is reported for separately scoped correction; no runtime fix is authorized by this packet. Full Milestone11/Phase1D/UAT/G1–G9 and actual-reader P02 cases remain NOT VERIFIED.

## Local result

The [executed verification](../../specs/003-health-assessment/phase1d-continuation-verification-cycle04.md) passes 24 scenarios/112 invocations/1,011 assertions on coordinator1899ca0, equivalent pushed codebe5a82e. Nonauthor final review independently executed the DLL and closed all findings. Scoped local checks pass; original full-solution restore/checkout/tempfile failures and six corrected format diagnostics are retained. The intermediate pushed checkpoint e065747 failed Linux formatting; final exact source has fresh hosted runs. Full hosted/private closure remains pending; no production behavior or actual integration is added.

## Cycle closure

Native exact-source bootstrap226 (Linux/Windows2022/2025) and package136 on be5a82ed5593c12590af27108f1b8dcc266f8b85 completed successfully, including whole-solution restore/format/Release build, historical database/browser and infrastructure checks. These remain configured partial repository gates, not full pilot acceptance. [Initial private publication](../../docs/development/evidence/phase1d-continuation-cycle04-site-20261003.json) confirms owner-private version112 with13 open human tasks; final completed-plan/hosted-success snapshot is published in the same cycle with native provenance recorded separately. All needed source/runtime/log/review evidence was preserved before removing the clean cycle04 worktree; all332 pinned working-source paths and16 original fixtures are unchanged. No actual publication/client contract was supplied.
