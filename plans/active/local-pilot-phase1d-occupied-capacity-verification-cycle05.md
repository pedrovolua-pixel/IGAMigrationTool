# Phase 1D cycle05 occupied cursor capacity verification

Status: RUNNING — approved synthetic verification only
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

- [ ] Eight scenarios: NamedUser/Service × identity128/customer512 ceilings × audit false/throw, original three-item Findings, page size1, explicit partial fields. Expected typed values and original scalar bindings/digest derive from original bytes without production projection helpers.
- [ ] Occupy exactly cap−1 using successful original-fixture pages, below unrelated rate/concurrency/expiry limits. Fail continuation of an existing handle while its successor would occupy the final slot: DependencyUnavailable, no envelope/new cursor, one attempted Success audit, no accepted completion and safe AuditUnavailable.
- [ ] Retry the same original handle with unchanged authority before original expiry. It returns exact item b and a new successor, filling the ceiling. The next cursor-producing call returns Limited with an accepted Limited audit; expected source reads are observed rather than forbidden because capacity admission occurs during terminal commit.
- [ ] Final-page successor returns exact item c/null cursor at full registry. Preserve occupied handles and bindings; verify spare identity quota in the same customer for identity ceiling, and a distinct identity/customer for customer ceiling. Any alternate fictional customer manifest derives independently from original canonical bytes and has its own source/grant commitment; original fixture files stay unchanged.
- [ ] Independent review/execution closes and exact combined source passes applicable locked restore/format/build, original/new MCP, oracle/architecture, source-secret/dependency and configured hosted checks. Original failures and skipped scope remain recorded.
- [ ] Preserve unrelated working changes and original fixture/production/host/UI/contract bytes. Update canonical plan/status/evidence and publish the existing private board with native source/audience confirmation.

## Limits and escalation

No global4096 quota duplication, exhaustive cleanup proof, production scale/SLO, durable/distributed cursor/audit, expiry-during-emission policy, reentrant audit or cancellation-after-terminal-audit behavior is added. Logical clock pacing only separates approved local quota dimensions. No native publisher/reader, real identity/grant, SDK/listener/transport/client selection, migration/dependency/settings or live activation. Report any contract defect for separately scoped correction; this packet authorizes no runtime fix. Full Milestone11/Phase1D/UAT/G1–G9 and actual-source P02 cases remain NOT VERIFIED.

## Evidence

Execution/review/remaining checks will be recorded in this plan and a source-bound verification receipt.

