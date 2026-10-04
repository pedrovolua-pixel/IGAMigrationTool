# HTTPS deployment readiness completion

Status: RUNNING — local development and tests; consequential contracts pending exact review
Date: 2026-10-04 UTC
Owner: Coordinator
Baseline: 102658a245cfbb084124f65a301487aa79e1e572
Authority: owner “Approved, finish development and test our way down to be ready for deployment.” Continue all authorized local engineering through [PC-D01–05](https-production-local-cycle01.md). Human approval of consequential new architecture/security policy, protected bindings, grants, paid sessions, publication and release remains separate under AGENTS.md. Do not reinterpret continuation as supplying absent values or identities.

## Finite dependency order

1. Close remaining local actual-SDK transport evidence, rather than repeat the completed45-case resolver/dispatch diagnostic.
2. Produce concrete recommended key inventory/recovery and lifecycle/limits contracts, implementation and test plans, refining Proposed ADR-0013. Produce a concrete host/provider/audit decision addendum identifying every prerequisite and exact recommended alternative/value. Independent review precedes any request for consequential human approval.
3. After exact architecture/technical/test approval, implement production adapters and additive metadata, compose the actual BFF and run complete applicable local regression/negative/recovery/browser/build/security checks. Do not write dependent product code against unresolved security contracts.
4. Prepare digest-bound production image/build evidence, reviewed protected environment inputs and full priced validation/what-if. Then obtain exact Azure test-session/access authorization and execute live identity/RBAC/ingress/two-replica/recovery cases. Live-only evidence cannot be supplied by a local fixture. Public login and production release require their own acceptance.

This plan is a maintained completion map, not a claim that all phases are approved or verified. The existing [production test packet](../../docs/development/https-production-test-packet.md), [key input checklist](../../docs/development/https-key-production-input-checklist.md) and feature specs own acceptance and six human tasks. Existing local passes remain closed; each new packet targets a concrete missing risk.

## Initial isolated work packets

| Packet | Writing ownership | Deliverable / boundary |
|---|---|---|
| RD-K | Key design worker: architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md; docs/development/https-key-inventory-production-contract.md; https-key-inventory-implementation-test-plan.md; https-key-lifecycle-limits-proposal.md | Concrete recommended Option A protocol, closed records/state/failure ordering/independence and implementable local test plan. Numeric limits must be explicit justified proposals with units, never silently accepted policy. Exact protected owners/IDs stay external; common-owner limitations remain explicit. No SQL/code/grants/retention locks. |
| RD-H | Host/audit design worker: docs/development/https-production-readiness-decision-addendum.md; https-production-readiness-implementation-test-plan.md | Concrete remaining provider, audit receipt/lifecycle/outage, host/resource/CA/step-up/abuse/deadline decisions and critical path. Preserve approved public contracts and policy; proposed changes explicitly require review. No arbitrary new access, outage exemption or durable-storage guarantees. No code/schema/permission changes. |
| RD-B | SDK transport worker: tests/infrastructure/HttpsKeyProviderDispatch/BlobPipelineCases.cs; Program.cs; run-experimental.py; README.md | Actual BlobClient pipeline and supported Microsoft repository under strictly scripted no-network HTTP. Preserve prior27+18 cases and exact37/74 package/source verification. Test-only fixed guards and constants do not become production protocol or policy. |

Writing workers use separate codex/ branches/worktrees from one frozen plan commit, only owned paths, immutable handoff and non-author review. Coordinator owns canonical records, solution/dependencies/workflow/configuration, integration, approvals and private board. Documentary authors can independently review each other's packets and the test packet after freezing their own work.

## RD-B required bounded cases

Read-only non-author scope review precedes writing. Actual archive-derived Blobs1.5.4/Keys1.6.4, Blob SDK12.26.0/Core1.61.0 and installed DataProtection10.0.12, SDK10.0.401 remain experimental; no new package restore graph or promotion. Runner keeps empty-source scratch build and all source/assembly/log hash checks.

- BP01: actual public Microsoft registration reaches guarded legacy overloads that delegate to actual BlobClient HTTP pipeline; small existing XML read and conditional append round-trip, concrete ETag from actual response.
- BP02: actual ranged/multipart download and single/staged upload controls with deterministic scripted server state and explicit fixture transfer sizes. Guard validates successful full content before provider use; report actual paths/counters, do not assume transfer API dispatch.
- BP03: actual scripted404/empty/malformed/truncated content and denied read errors refuse existing-mode fallback. Guarded conditions deny absent/empty/wildcard If-Match and absent-object creation before unsafe commit.
- BP04: actual scripted412 followed by deleted/missing reread refuses recreation; concurrent preserved entries and terminal/conflict exhaustion behave as observed. No production retry count adopted.
- BP05: scripted commit acceptance followed by lost acknowledgment demonstrates durable unused entry and caller uncertainty; reread validates preserved bytes/inventory. This is simulation, not TCP fault or a complete UNKNOWN/witness reconciliation contract.
- BP06: selected actual SDK retry/error path remains bounded by fixture options and script; unexpected method/path/query/headers/body mismatch denies, transport never calls socket/base HTTP send, no real credential or authentication fallback.
- BP07: preserve all previous45 cases; build0 warnings/errors, whole scratch C# formatting with meaningful control for the new source, Python AST, links/UTF-8/whitespace/scoped secrets and independent actual execution. Extend source-copy/hash guards for the third C# source; expose only safe labels/counts/booleans/digests, not raw XML, key/token/URI/body/protected payload.

Fixture failure/SDK incompatibility is evidence, not permission to replace internal Microsoft implementation, widen production format, assume recovery authority or invent a success. If supported guards cannot uphold the needed path, retain the failure and bring the finding into the concrete architecture review. No full KEY row, real Azure role/network/challenge, composed host or live readiness follows from this packet alone.

## Approval and completion

RD-K/RD-H become REVIEW_READY only after independent documentary/source/links/secret/whitespace checks. They are reviewable proposed policy/architecture, not product implementation approval. Present one consolidated exact revision and explicit choices rather than ask the owner to invent technical numbers. Continue independent approved work while awaiting required answers. Do not implement dependent behavior without applicable approved spec/technical/implementation/test packet.

RD-B becomes VERIFIED only after non-author immutable review and coordinator repeat on integrated source. Run all checks affected by actual changes; unchanged product suites are historical until real product composition changes require them. Record every failure/correction and source/log/assembly binding.

Update feature plan/status/evidence and six linked human tasks in the same cycle. Private BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the separate automatic-review-rejected credential/network publisher proxy-bypass; no retry/bypass, new credentials or alternate publication route. Preserve unrelated Cycle14 publication. No Azure resources/spend/grants/public source export or live customer admission in these initial packets.
