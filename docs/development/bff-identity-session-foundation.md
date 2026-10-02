# BFF identity and session foundation

Status: Independently verified local foundation on `codex/bff-integration`; published draft PR #2 with passing configured hosted checks; Azure sign-in disabled
Scope: Milestone 2 / IP-HAS-003

## Components

The [approved identity design](../security/health-assessment-identity-session-design.md) and [authorization matrix](../security/health-assessment-authorization-matrix.md) govern these internal components. They introduce no public product route or request schema.

| Component | Responsibility |
|---|---|
| `src/server/hosts/BffFoundation` | Microsoft.Identity.Web 4.15.0 registration; tenant-specific code flow with PKCE and managed-identity client assertion; no token-bearing browser cookie; request authentication, authority freshness and CSRF enforcement |
| `src/server/modules/IdentitySessions` | Shared control-plane PostgreSQL ticket store, protected minimal tickets, random opaque identifiers, approved deadlines, session rotation and authoritative revocation/security version |
| `src/server/modules/IdentityPolicy` | Deny-by-default human action/role/projection policy using trusted current assignment/resource/customer snapshots and separately verified privileged authentication |
| `tests/integration/BffSessionFlow.Tests` | Two real HTTPS server instances sharing a disposable PostgreSQL store and test key ring; synthetic session fixtures only |

Microsoft documents the [managed-identity assertion credential](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificateless). The package is [pinned to 4.15.0](https://www.nuget.org/packages/Microsoft.Identity.Web/4.15.0). The Microsoft token acquisition handler is needed to redeem the code with that credential; declaring the credential alone is insufficient in this version. No delegated downstream API operation is added. Token cache persistence is deliberately absent; response credentials are never copied into a session, browser response or log.

## Host composition and remaining adapters

The production host must supply a shared `ITicketStore`, trusted `IBffSubjectAuthority` and `ISessionAdmissionPolicy`, and the policy's authoritative snapshot and privileged-authentication adapters. These adapters read current product/provider state, bind immutable tenant/object identity, customer/project/environment/assessment/resource, security version and session, and deny on missing or unavailable authority. Boolean fixture adapters in test projects are not production identity, onboarding or Conditional Access implementations.

Install request protection after authentication and before every cookie-authenticated product endpoint. Generate synchronizer tokens through ASP.NET `IAntiforgery` at an approved host-owned read operation; state-changing requests require its token. Safe GET/HEAD/OPTIONS handlers must have no mutation side effects. Apply product authorization and field projection before every data-plane lookup/serialization; transport authentication alone grants no customer access.

The cookie has a narrow `/bff` path by default, no domain, `Secure`, `HttpOnly` and `SameSite=Lax`. The shared store owns idle (30 minutes), absolute (8 hours) and provider-status (15 minutes) bounds. Its maximum absolute deadline is anchored to the trusted original authentication time, so ordinary activity or rotation of unchanged authentication cannot restart eight hours; an older admitted authentication has less remaining time. Rotation after idle/absolute expiry requires authentication at or after that expiry boundary. Privileged actions additionally require the independent server verifier's current MFA/Conditional Access context and authentication no older than 15 minutes. No claim or local session flag proves that context. Transport/store reject provider age at exactly 15 minutes; the pure policy permits the inclusive specification bound, and cannot broaden transport admission.

Use one shared application-discriminated Data Protection key ring for replicas, with production keys protected and lifecycle-managed under the approved Key Vault/managed-identity design. Temporary local test keys and certificates are synthetic-only. Production key persistence, rotation, permissions and recovery are NOT VERIFIED.

## Migration and registration inputs

[`migrations/identity-sessions/001-initial.sql`](../../migrations/identity-sessions/001-initial.sql) creates only the module-owned control-plane schema. Apply it with the reviewed migration identity, using the repository expand/migrate/contract procedure; startup does not automatically provision subjects or migrate customer databases. Effective database permissions, migration drift and compatible deployment/rollback require the later platform integration checks. Production roles must separate trusted subject administration from ordinary ticket operations; this cycle does not grant database access.

[`infra/entra/pilot-bff.federated-credential.template.json`](../../infra/entra/pilot-bff.federated-credential.template.json) preserves the approved exact tenant issuer, dedicated managed-identity **principal/object** subject and token-exchange audience. It is an inert template; replace placeholders only in an ephemeral protected provisioning input. The runtime credential uses the managed identity's **client** ID. Do not confuse those identifiers. Registration/client/tenant/identity IDs and HTTPS redirect/logout URIs remain environment inputs outside Git. The existing inactive registration template is unchanged.

## Verification and activation

Executed checks, reviews and remaining cases are recorded in the [cycle plan](../../plans/active/bff-identity-session-cycle.md) and the [executed evidence record](evidence/bff-identity-session-20261002.json), with an [independent scoped review](evidence/bff-foundation-review-20261002.md). Local fixtures do not prove OIDC signature/lifetime/nonce/state/PKCE/key rollover, Entra status retrieval, actual managed-identity assertion renewal, Container Apps multi-replica sign-in, wrong trust/removal, licensed users or tenant Conditional Access.

`LiveSignInEnabled` defaults to false. Production composition and enabling sign-in remain blocked until the approved provider spike, full authorization matrix across API/workload/render/export/share/MCP paths, trusted routing/assignment/audit adapters and human review are accepted. The present human-only policy denies non-human identity kinds. It does not issue worker scopes, authorize share viewers, perform field redaction itself, record append-only audit or cancel queued work. Unknown/unsupported operations deny. Complete product integration must provide those boundaries before live use.

No Azure resources, trust, consent, redirect endpoints, customer assignments, customer data or paid session were created by this local cycle. G1–G9 remain NOT VERIFIED.

## Local handoff — 2026-10-02

The immutable implementation `3781b3b464102062795185fd812818555259aa20` passed 72 transport assertions, 27 session unit cases, 100 shared-store checks, 2546 human policy cases and 84 actual two-server HTTPS checks. A different reviewer repeated the 84 HTTPS checks against that head. The 33-project solution passed audited locked restore, formatting and Release build with zero warnings/errors. Existing assessment database regressions and compiled infrastructure policies also passed. The configured hosted Linux, Windows2022/2025 and container checks now passed on the combined 38-project code snapshot; exact runs and limits follow below.

The source is published in the separate `codex/bff-integration` draft branch/worktree, preserving the other coordinator's ongoing local pilot work. Start composition from that branch; do not assume the original checkout already contains these modules. Test runner safety conditions and executed commands are in each test README. There is no production sign-in endpoint, browser login screen or deployable BFF image from this cycle. The next technical packet is production host/adapters plus the accepted deployed Entra federation proof, followed by cross-path authorization/key/audit integration. A newly priced Azure resource window and its cleanup scope need their own approval.

## Approved publication and hosted evidence — 2026-10-02

The owner explicitly approved public branch publication and a draft pull request on 2026-10-02. [Draft PR #2](https://github.com/pedrovolua-pixel/IGAMigrationTool/pull/2) is published and remains unmerged. The combined code snapshot `145de02af6537c2532c5cdd2ca0bd3e4117e21d4`, based on completed pilot source `0a58364`, passed local locked restore, formatting and the 38-project Release build with zero warnings/errors; the BFF runtime/test source is unchanged from independently reviewed `3781b3b`. [Hosted bootstrap checks](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37001243915) passed Linux and Windows 2022/2025, including actual shared PostgreSQL BFF sessions/HTTPS, existing database/browser regressions, dependency boundaries, secret and infrastructure checks. [Hosted package checks](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37001244041) passed the inert non-root container and hosting-policy checks. The earlier publishing blocker is resolved and the human publication dependency is closed. Production host/adapters, provider/federation/Conditional Access proofs, key lifecycle, field filtering, routing/audit and full cross-path authorization remain open. Milestone 2 and G1–G9 remain NOT VERIFIED; publication does not authorize draft merge, live sign-in or new Azure resources. Owner-private board version57 publication is confirmed on 2026-10-02; it closes the public-publication task while retaining production identity and live gate dependencies.
