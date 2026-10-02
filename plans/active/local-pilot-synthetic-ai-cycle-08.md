# Local cycle08 — synthetic AI packet and proposed-output validation

Status: RUNNING — bounded approved local developer cycle.
Owner: Coordinator / repository owner
Source checkpoint: `f47f2717a9d3c0386297380112f2f270fb1d02c3`

The owner's “go to next cycle” on 2026-10-02 continues the approved [local build](one-identity-local-pilot-build.md). Implement the [settled internal fixture contract](../../specs/003-health-assessment/synthetic-ai-fixture-contract.md) under FR-HAS-30–33, AC-HAS-8, IP-HAS-007/Milestone6, TP-HAS-008 and relevant 002/003/009/017. This is a runnable isolated fake-provider foundation; no live AI activation, new public API, saved run/profile reinterpretation, scoring/review/publication change, migration, dependency or budget/permission decision.

## Ownership and sequence

| Packet | Owner/path | State | Acceptance and evidence |
|---|---|---|---|
| A8 packet builder | Assessment worker: `src/server/modules/SyntheticAiValidation/Packet*`, `tests/unit/SyntheticAiPackets.Tests/**` | READY | Closed minimized data-only packet, fixed fictional source, scoped immutable canonical digest; negative shape/allowlist/source/size/duplicate/inert-content cases and independent golden bytes |
| B8 proposal validator | Assessment worker: `src/server/modules/SyntheticAiValidation/Proposal*`, `tests/unit/SyntheticAiProposals.Tests/**` | READY | Exact run/packet/member citations; typed fact/inference/assumption/missing-context/suggestion; conflict preservation, closed failures, immutable Proposed only; no executable path |
| V8 independent verification | Verification worker: `tests/integration/SyntheticAiValidation.Tests/**` | READY | Separate authored fake provider and adversarial/golden/mutation fixtures; composed positive/negative workflow, exact reviewed source and executed checks; non-author review of A8/B8 |
| Coordinator | Shared module project/JSON helper, solution/CI/configuration, integration and canonical records/Site | RUNNING | Preserve concurrent BFF/UI work; integrate reviewed commits, all applicable checks on frozen combined source, source/evidence binding and private publication |

Each writing worker receives its own `codex/` worktree from the same contract checkpoint, explicit ownership and disabled paths. A8/B8 run independently against the settled contract; V8 composes the integrated result and independently reviews both. Coordinator or another non-author reviews V8. Review findings must close before VERIFIED. No persistent chats or automation are created.

## Completion checks

- [ ] A8/B8 unit hosts execute their meaningful positive, denial, canonical and hostile-data cases.
- [ ] V8 independently executes composed fixed fake-provider, malformed/foreign/run/conflict/oversize/duplicate/schema cases and independent golden digest checks.
- [ ] Combined locked audited restore, formatting, zero-warning Release build, all unit hosts, architecture and relevant existing regression/integration checks pass on isolated exact source. New hosts join existing configured CI. Unavailable/platform/manual checks are stated, not claimed.
- [ ] Secret/dependency checks and scoped source review show no provider/SDK/network/raw resolver/host/UI/run-policy integration or new dependency. No protected payload in execution metadata.
- [ ] Canonical plan/status/evidence and existing owner-private Site are synchronized; every human task retains its role/source/completion condition. Native deployment success is confirmed.
- [ ] Worker source/execution evidence is preserved and clean temporary writing worktrees removed.

All full milestones, Phase1B/local-pilot completion, real authorization/security/provider acceptance and G1–G9 remain NOT VERIFIED. Model settings, token/cost/budget/profile decisions and US/ZDR provider evidence remain exact human dependencies. No production release or draft merge is authorized.
