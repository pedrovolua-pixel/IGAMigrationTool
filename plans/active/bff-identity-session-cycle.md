# BFF identity and session foundation

Status: LOCAL VERIFICATION PASSED — reviewable branch; public publication approval pending; live sign-in disabled
Owner: Coordinator / repository owner  
Started: 2026-10-01

## Authority and outcome

The owner's “Work on it” follows the BFF explanation and next-step proposal. Implement the already approved identity/session and human authorization foundation under Milestone 2 / IP-HAS-003. Sources: [identity design](../../docs/security/health-assessment-identity-session-design.md), [authorization matrix](../../docs/security/health-assessment-authorization-matrix.md), [technical specification](../../specs/003-health-assessment/technical-spec.md), [test plan](../../specs/003-health-assessment/test-plan.md), ADR-0004 and the [parallel workflow](../../docs/development/parallel-agent-workflow.md).

Deliver internally composable .NET modules with executable synthetic tests: supported Microsoft OIDC configuration using the approved federated managed identity; opaque server-managed sessions and per-request revocation; secure cookies/CSRF; deny-by-default human action policy. This cycle introduces no product endpoint schema, permission outside the matrix, guest auto-enrollment, local authentication bypass, customer database routing, worker grant or Azure deployment. Existing assessment-engine edits belong to another work cycle and must be preserved.

## Packets and ownership

Workers start isolated branches/worktrees from one recorded source commit. Shared solution, CI, canonical documents, integration and private status site belong to the coordinator. Workers own only their packet paths and report exact checks, dependency needs, limitations and commit references. Each packet receives review by a different worker before integration.

| Packet | Owner | Paths | Outcome / acceptance |
|---|---|---|---|
| BFF-S1 | Session worker | `src/server/modules/IdentitySessions/`, `tests/unit/IdentitySessions.Tests/`, `tests/integration/IdentitySessions.Tests/`, `migrations/identity-sessions/` | Shared PostgreSQL ticket/session store; cryptographic opaque identifiers; approved 30-minute idle / 8-hour absolute / 15-minute provider-status bounds; rotation, individual/subject revoke, monotonic version, race tests; no tokens/browser claims |
| BFF-S2 | Microsoft/transport worker | `src/server/hosts/BffFoundation/`, `tests/unit/BffFoundation.Tests/` | Composable disabled-by-default Microsoft.Identity.Web OIDC registration; single tenant, code+PKCE, managed-identity assertion only, minimal immutable identity, fail closed missing provider/assignment/context adapters; secure cookie and CSRF configuration; exact dependency lock; adversarial configuration/middleware tests |
| BFF-S3 | Policy worker | `src/server/modules/IdentityPolicy/`, `tests/unit/IdentityPolicy.Tests/` | Pure internal human policy contract covering approved role/action/condition matrix with scope/resource/category/customer-policy/recent-authentication denial and independent synthetic cases; no inferred worker/share/MCP authority |
| BFF-I1 | Coordinator | shared configuration, focused integration tests, docs/evidence/site | Integrate reviewed modules; test real transport plus shared store where runnable; run repository checks; record unverified provider/deployment cases and publish owner-private status |

Internal module contracts are implementation details. Authority lookup and privileged MFA/Conditional Access verification are injected trusted server adapters, never request fields or automatically inferred from presentation claims. Missing adapters deny access. Provider status older than 15 minutes denies access; no synthetic revalidation or silent lifetime extension is permitted. Guest onboarding remains authoritative product state with sponsor, expiry and review. No live sign-in activation follows from passing local checks.

## Verification and handoff

Execute locked restore, formatting, warning-free build, bounded module tests, real synthetic PostgreSQL persistence/revocation/concurrency, middleware refusal/CSRF/cookie tests, architectural checks, dependency audit and secret scan. Extend partial CI with applicable cases. Record exact executed evidence; do not equate helper tests with the entire identity verification matrix.

Before live sign-in: accepted Container Apps authorization-code redemption with federated identity, assertion renewal/multiple replicas/key rollover, wrong issuer/subject/audience/tenant and trust removal, reviewed Conditional Access strength/scope, complete product assignment/routing/audit integration and full cross-path authorization tests remain required. G1–G9 remain NOT VERIFIED. A new paid Azure window requires its own priced scope and cleanup approval.

## Execution record

- Planning: approved specifications read; repository implementation and scoped security-review workflows applied. Writing packets used isolated worktrees from `131eaa6` and received independent review.
- BFF-S1 accepted after the expired-rotation correction: session author `90fb073f294a0b37ae3088ce044d4650b22a3982`; coordinator 27 unit / 100 PostgreSQL checks; independent reviewer 27 unit / 103 PostgreSQL checks.
- BFF-S2 accepted after pre-metadata challenge/callback/sign-out guards: transport author `bba44cbd95169ea11cc84457c9ac6337d02b220d`; independent reviewer 71 assertions plus a separate zero-fetch probe. Coordinator scoped remote sign-out under the cookie path and passed the final 72 assertions.
- BFF-S3 accepted: policy author `f5c7c39a2a044255f504a2726c1416814ebcbbdc`; author and independent reviewer each passed 2546 matrix/boundary/overlap cases.
- BFF-I1 implementation `3781b3b464102062795185fd812818555259aa20`: 84 actual HTTPS/shared-store checks passed, independently repeated against that immutable head. Actual subject binding and denial HTTP results were corrected before review acceptance. Final source contains no public authentication bypass or deployed product routes.
- Pinned 33-project restore/format/Release build, all 11 unit executables, architecture and existing synthetic/database regressions, advisory scan, secret scan, 18 Bicep compile/lint files and infrastructure policies passed. Exact results and limits: [evidence](../../docs/development/evidence/bff-identity-session-20261002.json), [scoped security review](../../docs/development/evidence/bff-foundation-review-20261002.md), [composition handoff](../../docs/development/bff-identity-session-foundation.md).
- The coordinator integrated on `codex/bff-integration` at `/tmp/iga-bff-integration` because another approved cycle owns unfinished shared-checkout edits. Those edits are preserved; the BFF modules are not yet integrated into that dirty checkout.
- Automatic approval review blocked source publication. The authenticated owner and public visibility of `pedrovolua-pixel/IGAMigrationTool` were verified; explicit public branch/draft-PR approval is pending. Updated hosted CI and Windows/container execution remain NOT VERIFIED. No gate or production approval is inferred.
- Private pilot board update/publication is pending. This cycle is not administratively closed until its documentation and board reflect the verified handoff/publication state.
