# Phase 1D approved local implementation cycle

Status: RUNNING — exact owner approval recorded; internal contract freeze first
Owner: Phase1D coordinator
Date: 2026-10-03
Approval: [P1D-D01–03](../../specs/003-health-assessment/local-phase1d-approval.md)
Specifications: [approved proposal](../../specs/003-health-assessment/local-phase1d-contract-proposal.md), [paired matrix](../../specs/003-health-assessment/local-phase1d-test-plan.md), [internal contract](../../specs/003-health-assessment/local-phase1d-implementation-contract.md)

## Authorized outcome

A dependency-free in-process fixture host validates exact synthetic immutable publication bindings, projects six closed resources, enforces distinct current grants/minimization/denials, opaque paged snapshots, bounded admission/deadline/cursor state and payload-free audit. No ordinary host path, listener, public/client contract, provider/resource registration, source adapter, real grant, database migration, retention or production configuration change. Full Milestone11/Phase1D/UAT/G1–G9 remain NOT VERIFIED.

## Packets and ownership

| Packet | Exclusive paths | Checks / state |
| --- | --- | --- |
| Coordinator | Approval/internal contract/shared Contracts.cs, module/test csproj+locks, solution/CI, canonical docs, integration/operations and existing private Site | RUNNING |
| A-D1 | SyntheticMcp/PublicationCodec.cs and Projection.cs; tests/unit/SyntheticMcpPublication.Tests source/fixtures only | P1D-T01/02/05/11; waiting for freeze |
| B-D1 | SyntheticMcp/McpHarness.cs, RequestParser.cs, LocalLimits.cs, CursorRegistry.cs; tests/unit/SyntheticMcpBoundary.Tests source/fixtures only | P1D-T03/04/06–10; waiting for freeze |
| V-D2 | tests/integration/SyntheticMcp.Tests source/fixtures/oracle only; read-only non-author code review | Independent P1D-T01–12; prerequisite/internal review |

Writers receive isolated codex/ worktrees from one exact checkpoint after signatures and contract review freeze. Coordinator owns shared configuration. Workers return commits, actual commands/counts/failures and limitations; receive non-author review before integration. Verifier preserves authored oracle/goldens before inspecting implementation. No separate persistent task or automation is created.

## Execution and completion

- [x] Exact approval recorded; internal contract and shared signatures non-author reviewed/frozen.
- [ ] Independent fixture golden and denial corpus preserved before implementation reads.
- [ ] A publication/resource tests pass with exact manifest/payload and hostile/protected text checks.
- [ ] B boundary/race/limits/cursor/audit tests pass; zero raw/source/business mutation access under denial.
- [ ] V independent integration/adversarial tests pass; actionable review findings resolved.
- [ ] Combined locked restore, format/build, architecture, all applicable local unit/portable regression and dependency/secret/CI checks executed. Unavailable checks recorded separately.
- [ ] Exact prior source/DTO/migration/normal-host/UI bytes preserved; no new dependency or runtime flag.
- [ ] Documentation, canonical status/evidence/plan and operations updated; private Site closes approved local decision, shows separate remaining human dependencies, preserves concurrent work/cards and confirms exact owner-only deployment.
- [ ] Native source/commands/original failures/reviews archived; clean own worker worktrees removed with branches/evidence retained.

## Integration, rollout and rollback

Add module and three test projects to solution and portable CI execution only. No host reference activates MCP. Rollback removes additive module/test projects and portable step; no database down migration. Phase1C immutable reader and real protocol/client/identity/distributed controls stay separate integration/activation packets. Exact approval supersedes original proposal pending labels only within the local scope; historical preparation bytes stay unchanged.
