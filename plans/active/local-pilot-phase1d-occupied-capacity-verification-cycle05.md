# Phase 1D cycle05 occupied cursor capacity verification

Status: COMPLETED — approved synthetic verification only
Owner: Phase1D coordinator
Date: 2026-10-03
Baseline: 1a7ce60d2114c690c83193e0ee731060121d9cab
Authority: owner's “Next cycle”, [exact local approval](../../specs/003-health-assessment/local-phase1d-approval.md), [frozen contract](../../specs/003-health-assessment/local-phase1d-implementation-contract.md), [P1D-T06/08/09/10/12](../../specs/003-health-assessment/local-phase1d-test-plan.md), [verification skill](../../skills/verify-feature/SKILL.md) and [parallel workflow](../../docs/development/parallel-agent-workflow.md).

## Bounded outcome

Independent gap review found no composed proof combining occupied cursor state, terminal audit failure, exact quota recovery and final-page progress. Existing suites test steady ceilings and audit failures from empty registries separately. This packet verifies the frozen local behavior; it changes no runtime or public contract. Actual immutable publication/client handoffs remain absent and cycle02 intake stays open.

## Packets and ownership

C05-A writing verifier owns only new `tests/integration/SyntheticMcp.Tests/OccupiedCursorCapacity.cs` and adjacent `.oracle.md` in isolated `codex/p1d-cycle05-capacity` worktree from the stated baseline. Preserve an independently derived contract/fixture oracle before inspecting implementation. No original fixture/golden, runtime, existing guard, configuration, project, lock, workflow, canonical or site edits.

C05-V nonauthor read-only reviewer owns gap selection and independent review/execution. Review source-call/policy/audit observations and quota/rate/expiry separation. Agent review does not replace human gate approval. Coordinator owns additive Program call, integration/checks/evidence, canonical records and existing owner-private board. One writing worker suffices because later integration/review depend on its handoff.

## Acceptance cases

- [x] Sixteen scenarios: NamedUser/Service × Findings/Recommendations × identity 128 / customer 512 ceilings × audit false/throw, original three-item groups, page size1, explicit partial fields. Expected typed values and original scalar bindings/digest derive from original bytes without production projection helpers.
- [x] Occupy exactly cap−1 using successful original-fixture pages, below unrelated rate/concurrency/expiry limits. Fail continuation of an existing handle while its successor would occupy the final slot: DependencyUnavailable, no envelope/new cursor, one attempted Success audit, no accepted completion and safe AuditUnavailable.
- [x] Retry the same original handle with unchanged authority before original expiry. It returns exact item b and a new successor, filling the ceiling. The next cursor-producing call returns Limited with an accepted Limited audit; expected source reads are observed rather than forbidden because capacity admission occurs during terminal commit.
- [x] Final-page successor returns exact item c/null cursor at full registry. Preserve occupied handles and bindings; verify spare identity quota in the same customer for identity ceiling and another identity’s retained final-page handle for customer ceiling. This is quota fairness/handle preservation, not new cross-customer isolation proof. Original fixture files stay unchanged.
- [x] Independent review/execution closes and exact combined source passes applicable locked restore/format/build, original/new MCP, oracle/architecture, source-secret/dependency and configured hosted checks. Original failures and skipped scope remain recorded.
- [x] Preserve unrelated working changes and original fixture/production/host/UI/contract bytes. Update canonical plan/status/evidence and publish the existing private board with native source/audience confirmation.

## Limits and escalation

No global4096 quota duplication, exhaustive cleanup proof, production scale/SLO, durable/distributed cursor/audit, expiry-during-emission policy, reentrant audit or cancellation-after-terminal-audit behavior is added. Logical clock pacing only separates approved local quota dimensions. No native publisher/reader, real identity/grant, SDK/listener/transport/client selection, migration/dependency/settings or live activation. Report any contract defect for separately scoped correction; this packet authorizes no runtime fix. Full Milestone11/Phase1D/UAT/G1–G9 and actual-source P02 cases remain NOT VERIFIED.

## Local checkpoint (historical)

The [executed verification](../../specs/003-health-assessment/phase1d-occupied-capacity-verification-cycle05.md) passes16 scenarios / 5,200 invocations / 88,562 assertions on coordinator e0d6a67, equivalent pushed 413bd13. Nonauthor review independently executed the final DLL. Full local restore/format/zero-warning build, original/new MCP, oracle/architecture, dependency/source-secret checks pass. Initial coordinator CS8803 and author oracle arithmetic corrections are preserved. Exactlythree test paths change;332 source pins / 16 fixtures / prior guards unchanged. Both own clean worktrees removed after preservation. Package 140 passed; full hosted bootstrap 230 and private publication remain pending.


## Cycle closure

Native exact-source [bootstrap 230](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37162503717) and [package 140](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37162503644) on 413bd137210a23f301ef53643a5093c4f074e1d6 completed successfully. Linux and both Windows jobs passed, including the ordinary new guard, whole-solution checks, historical PostgreSQL/browser and configured infrastructure checks. These are partial repository gates, not full pilot acceptance. [Private checkpoint 114](../../docs/development/evidence/phase1d-occupied-capacity-cycle05-site-20261003.json) is confirmed owner-only; final completed-cycle publication is recorded separately in the same cycle. Original failures remain preserved, source/fixtures/prior guards remain unchanged and both clean temporary worktrees are removed. No actual publication/client contract was supplied.
