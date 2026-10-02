# Local cycle08 — synthetic AI packet and proposed-output validation

Status: VERIFIED engineering — matching owner-private publication pending.
Owner: Coordinator / repository owner
Source checkpoint: `f47f2717a9d3c0386297380112f2f270fb1d02c3`

The owner's “go to next cycle” on 2026-10-02 continues the approved [local build](one-identity-local-pilot-build.md). Implement the [settled internal fixture contract](../../specs/003-health-assessment/synthetic-ai-fixture-contract.md) under FR-HAS-30–33, AC-HAS-8, IP-HAS-007/Milestone6, TP-HAS-008 and relevant 002/003/009/017. This is a runnable isolated fake-provider foundation; no live AI activation, new public API, saved run/profile reinterpretation, scoring/review/publication change, migration, dependency or budget/permission decision.

## Ownership and sequence

| Packet | Owner/path | State | Acceptance and evidence |
|---|---|---|---|
| A8 packet builder | Assessment worker: `src/server/modules/SyntheticAiValidation/Packet*`, `tests/unit/SyntheticAiPackets.Tests/**` | VERIFIED | Closed minimized data-only packet, fixed fictional source, scoped immutable canonical digest; negative shape/allowlist/source/size/duplicate/inert-content cases and independent golden bytes |
| B8 proposal validator | Assessment worker: `src/server/modules/SyntheticAiValidation/Proposal*`, `tests/unit/SyntheticAiProposals.Tests/**` | VERIFIED | Exact run/packet/member citations; typed fact/inference/assumption/missing-context/suggestion; conflict preservation, closed failures, immutable Proposed only; no executable path |
| V8 independent verification | Verification worker: `tests/integration/SyntheticAiValidation.Tests/**` | VERIFIED | Separate authored fake provider and adversarial/golden/mutation fixtures; composed positive/negative workflow, exact reviewed source and executed checks; non-author review of A8/B8 |
| Coordinator | Shared module project/JSON helper, solution/CI/configuration, integration and canonical records/Site | RUNNING | Preserve concurrent BFF/UI work; integrate reviewed commits, all applicable checks on frozen combined source, source/evidence binding and private publication |

Each writing worker receives its own `codex/` worktree from the same contract checkpoint, explicit ownership and disabled paths. A8/B8 run independently against the settled contract; V8 composes the integrated result and independently reviews both. Coordinator or another non-author reviews V8. Review findings must close before VERIFIED. No persistent chats or automation are created.

## Completion checks

- [x] A8/B8 unit hosts execute their meaningful positive, denial, canonical and hostile-data cases.
- [x] V8 independently executes composed fixed fake-provider, malformed/foreign/run/conflict/oversize/duplicate/schema cases and independent golden digest checks.
- [x] Combined locked audited restore, formatting, zero-warning Release build, all unit hosts, architecture and relevant existing regression/integration checks pass on isolated exact source. New hosts join existing configured CI. Unavailable/platform/manual checks are stated, not claimed.
- [x] Secret/dependency checks and scoped source review show no provider/SDK/network/raw resolver/host/UI/run-policy integration or new dependency. No protected payload in execution metadata.
- [ ] Canonical plan/status/evidence and existing owner-private Site are synchronized; every human task retains its role/source/completion condition. Native deployment success is confirmed.
- [x] Worker source/execution evidence is preserved and clean temporary writing worktrees removed.

All full milestones, Phase1B/local-pilot completion, real authorization/security/provider acceptance and G1–G9 remain NOT VERIFIED. Model settings, token/cost/budget/profile decisions and US/ZDR provider evidence remain exact human dependencies. No production release or draft merge is authorized.

## Executed engineering checkpoint

Exact combined code `5662de8c27ba76050b53a2c5388b8f753ef57661`; [developer bindings](../../docs/development/evidence/local-pilot-cycle08-developer-checks.json). A8 `dc162124`, B8 `c4c12a0`, V8 `a6572028` are integrated with non-author review closed. Actual source/needed binaries are preserved in six verified archives; three temporary writing-worker checkouts removed. The coordinator exact-source proof checkout is retained. All local and hosted Linux/Windows2022/2025/package checks passed on the same code, including all five existing browser workflows. Matching private Site publication remains pending. No customer environment, live AI processing, cloud grant/deployment, draft merge or release occurred.
