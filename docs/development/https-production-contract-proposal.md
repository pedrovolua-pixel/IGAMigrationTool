# HTTPS production composition contract proposal

Status: Proposed — human technical/security approval pending
Date: 2026-10-03 UTC
Product authority: [Approved health-assessment product](../../specs/003-health-assessment/product-spec.md)
Owner/reviewers: Technical/security, identity/platform, operations and data-governance owners
Preparation plan: [HTTPS-PC01–PC04](../../plans/active/https-production-contract-preparation.md)

## Overview and approval boundary

Specify the missing production adapters behind accepted local primitives. This is a review packet, not an approved implementation plan, permission request or deployment. [Local ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md) and [local ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) remain restricted to their accepted scope. “Proceed” authorizes preparation. No approved product behavior, retention, session lifetime or public API is changed by this document.

A future production host must be a separate executable; `BffDevelopmentHost` remains permanently disabled. The synthetic `LocalConsultantDemo` actor/data/permission providers cannot be attached to a public host. Completing this packet does not make customer assessment routes production-ready.

## Requirement and acceptance trace

| Existing authority | Proposed work and verification |
|---|---|
| IP-HAS-003; FR-HAS-13–15/29/32/45–46/55–56; AC-HAS-4/8–9/11/14/20 | Current Entra/product/session checks and server resource authorization; HO01–HO10 and PV cases |
| Approved identity/session design; SEC-PILOT-001/002/008 | Signed protocol/federation/CA, direct roles, exact customer assignment, no support evidence bypass; existing authentication suites plus proposed provider/live tests |
| Approved audit policy; SEC-PILOT-009; TP-HAS-009/020 | Atomic successful mutations and durable failure/denial composition; AU01–AU10 |
| ADR-0002/0004/0009; G1/G3 | Private protected backends, separate identities, shared keys/recovery and exact ingress evidence; KEY cases and IN01–IN06 |
| AC-HAS-13/20 and existing HTTPS-T02–T08 | Browser keyboard/error recovery, no JavaScript tokens, exact origins/redirects, cost/image/live acceptance; HO06 and LV01–LV04 |

These IDs inherit their canonical meanings. Tests below do not close complete ACs, SEC findings, G1–G9 or Milestone2.

## Current source and proposed components

| Boundary | Current implementation | Proposed composition / scope |
|---|---|---|
| Authentication | [BffRegistration](../../src/server/hosts/BffFoundation/BffRegistration.cs), guarded handler and public authentication endpoints | Preserve signed framework OIDC/code/PKCE/state/nonce validation and `/bff/v1` public contract; add typed internal failure observer composition after approval |
| Sessions/authority | [PostgreSqlBffSubjectAuthority](../../src/server/hosts/BffFoundation/PostgreSqlBffSubjectAuthority.cs), restricted audited ticket/authority stores | Keep original signed authentication distinct from current provider/product authority. No synthetic or legacy admission fallback; all successful ticket mutations use the atomic boundary |
| Provider | [SyntheticProviderReader](../../src/server/modules/IdentityAuthority/SyntheticProviderReader.cs) | [Proposed Graph adapter](https-production-provider-contract-proposal.md); a separate reader workload capability with reviewed consent, not browser delegated tokens |
| Keys | [BffHostingContracts](../../src/server/hosts/BffFoundation/BffHostingContracts.cs) | [Proposed supported Microsoft providers](https-production-key-contract-proposal.md), dedicated private ring/key/discriminator; no ephemeral or default credential fallback |
| Failure audit | Failure hooks currently return401; [PostgreSqlSecurityAudit](../../src/server/modules/IdentitySessions/PostgreSqlSecurityAudit.cs) appends typed events within a caller transaction | Compose available-store failures; [proposed ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md) compares durable outage paths. No selected production fallback/policy exception |
| Product actions | Existing policy/resource services are independently bounded local primitives | Production resource-context and privileged-authentication adapters require explicit contracts; generic authenticated middleware cannot authorize an assessment/evidence read |

Exact new package versions, transitive locks, source audit and licenses must be reviewed before a dependency-bearing implementation packet. No dependency is installed here. New domain interfaces must not expose Microsoft SDK objects.

## Host startup and request contract — proposed HO boundary

1. Default admission disabled. Validate the exact reviewed origin/redirect, tenant/app/resource/role mappings, distinct workload identities, private SQL/key/ring endpoints, durable audit binding and signed deployment-review references before exposing authentication. Missing or incompatible binding blocks startup/admission; readiness is not pilot acceptance. Do not print binding values or provider diagnostics.
2. Configure shared protection before authentication handlers and challenge/session persistence. Install the exact ingress validation before HTTPS/authentication, authentication before the accepted public authentication endpoints, then generic request protection before host-owned product endpoints. Preserve existing CSRF session binding and native form navigation. Production routes cannot use the synthetic demo actor or accept client-selected connection/store locators.
3. Preserve immutable `tid/oid`, original signed authentication time and exact stored session/security version/cutoff. Per-request product assignment/resource/evidence checks run server-side before customer data reads. Coarse roles alone confer no resource access. Provider refresh cannot enroll a user, create an assignment, clear suspension or supply privileged MFA.
4. Keep privileged verification default-denying until a separate signed original-session MFA/CA/strength contract and tenant proof are accepted. Do not turn the current hardcoded false marker into trusted evidence. A basic consultant/customer sign-in test does not accept protected-evidence, publication, deletion or risk-acceptance actions.
5. Issuance/rotation/revocation preserve the same-transaction audit and receipt rules. Unknown commit never reports rollback or replays credentials; reconcile internal receipt metadata and remain denied until outcome is safe. Background work uses its own scope/identity. No migration or enrollment runs at startup.
6. Preserve existing v1 response/body/redirect semantics and no-token browser behavior. A proposed audit-dependency unavailable response must be reviewed as a public-contract addendum; no503/status/header change is silently adopted by this packet. Existing401/403 does not prove failure-audit durability.

## Resource authorization and privileged verifier — proposed exact adapters

Reuse [IdentityPolicy contracts](../../src/server/modules/IdentityPolicy/Contracts.cs); introduce no role/action/grant/projection value. `IHumanAuthoritySnapshotSource.ResolveAsync(subject, request, cancellationToken)` accepts the verified human subject and validated existing `HumanAuthorizationRequest` only as a target selector. Resolve the actual resource's customer/project/environment/assessment, revision, state/expiry, category, permitted actions/projection and customer policy from authoritative server-owned metadata. Reuse current subject security version, exact stored session, conservative provider/home evidence and current assignments/lifecycle. Never copy the requested scope/revision/category or browser claim into the authoritative result. Unresolvable/ambiguous metadata or a cross-customer selector denies before data-plane connection. Exact repository functions and ownership/catalogue adapter sources require approval before SQL implementation; no client-supplied locator or inferred permission.

Return only the existing `AuthoritativeHumanSnapshot`. Resolve anew on each call; any approved cache remains at most one minute and cannot mask a known product revocation. A modifying action must recheck the authoritative resource/subject revision and permission in the actual commit boundary; a preflight snapshot alone cannot protect against concurrent reassignment. A protected read must bind its authorized projection to the same server-resolved resource and revision; no second unguarded fetch.

Propose an action-to-tenant-CA-authentication-context mapping for `IPrivilegedAuthenticationVerifier.VerifyAsync(subject, sessionId, securityVersion, action, cancellationToken)`. Evidence comes only from the supported signed OIDC completion bound to the protected single-use transaction and original authentication time; never from presentation claims, Graph observation or another session. Preserve the full existing privileged action set. A closed internal sidecar binds the immutable subject, exact newly issued session/security version, original authentication UTC, required action/context mapping revision and verified tenant-policy evidence reference. It contains no raw token/claim bag. Store it atomically with the audited ticket, or deny; invalidate on rotation/version change and check current mapping on every verification. Sidecar cleanup/backup/retention needs an explicit lifecycle decision, not a new duration here.

Return existing `VerifiedPrivilegedAuthentication` only for that same subject/session/version/action with authentication strictly within the existing15-minute bound and required MFA/CA positively proved. A signed matching `acrs` value alone does not prove required strength: Microsoft permits a requested context when no CA policy protects it. The mapping must have separately verified current enabled tenant policy, required approved phishing-resistant strength, applicable user/guest coverage and reviewed exclusions. Missing/removed/changed/unverified policy mapping denies; do not substitute `amr=mfa`, a boolean cookie marker or token age for that proof. [Microsoft authentication-context guidance](https://learn.microsoft.com/en-us/entra/identity-platform/developer-guide-conditional-access-authentication-context), checked2026-10-03 UTC.

Until that exact mapping/sidecar/lifecycle and supported BFF step-up protocol are approved, all privileged actions stay denied. Preserve existing sign-in/session public contract; do not copy a browser bearer-token challenge example into this BFF, add delegated API scope or new return/action parameters implicitly. A separate exact public-contract addendum must define any future step-up initiation, allowed pending action reference, native navigation/cancellation/replay and retry-after-authorization behavior. The application must evaluate product authorization again after step-up. This packet does not install CA, invite users, create an admin UI or grant those actions.

## Available-store authentication failure audit — proposed AU boundary

Attach a typed, server-owned attempt context at the authentication boundary. It generates one operation/event/correlation identity and allowlisted phase/reason, never from a request header. At each terminal authentication denial/failure, emit at most one durable outcome through a dedicated append-only adapter. Framework callback failure and `OnAuthenticationFailed`/`OnRemoteFailure`/authority denial may overlap: one context arbitrates the terminal event. Internal observer exceptions do not convert refusal into success.

Proposed executable producer map, all sharing terminal-attempt deduplication:

| Existing producer | Failure/denial to cover |
|---|---|
| `BffRegistration.OnValidatePrincipal` | RejectPrincipal for disabled/insecure transport, invalid subject, unavailable authority and stale/mismatched exact-session evidence; cookie validation does not necessarily invoke OIDC failure hooks |
| `GuardedOpenIdConnectHandler.HandleChallengeAsync` | Disabled/insecure admission and unavailable/refused pending-challenge creation before any provider redirect |
| `GuardedOpenIdConnectHandler.HandleRequestAsync` | Earliest callback/signed-out callback/remote-signout refusal before remote authentication, including disabled/insecure transport |
| `GuardedOpenIdConnectHandler.HandleRemoteAuthenticateAsync` | Framework terminal failure; missing/invalid transaction evidence; late consumed/expired/refused challenge after supported framework validation |
| OIDC `OnTokenValidated` and `OnAuthorizationCodeReceived` | Protocol/client/issuer/authority/origin/authentication/cutoff denial and disabled redemption; no fabricated validated actor |
| `OnAuthenticationFailed` / `OnRemoteFailure` | Terminal exception/protocol outcome, including overlaps with earlier observers |
| `GuardedOpenIdConnectHandler.SignOutAsync` / OIDC `OnRemoteSignOut` | Exact local logout refusal and unsupported remote sign-out; no global provider-logout guarantee or unaudited terminal refusal |
| Public authentication endpoints / generic request protection | Sign-in/logout authentication/CSRF/origin and missing/current-session refusals within these declared operations; ordinary unrelated404/static requests are not silently categorized as login attempts |

Resource/action authorization denials also require approved scoped business/security audit. Do not mislabel them as an anonymous authentication failure or add customer fields to authentication canonical v1; their exact producer/schema contract is part of PC-D05 and remains blocked before production product routes. Record distinct producer-coverage tests as AU01 subcases, not a blanket failure-hook assertion.

Reuse existing `SecurityAuditEventV1` action/outcome/reason values. Before validated subject evidence, actor kind is Anonymous, actor/target/session/security version and customer/project scope are null. A client `oid`, cookie lookup hash, route parameter, email, unsigned token or claimed customer cannot populate audit identity/scope. After signed validated identity, use only trusted immutable subject and server-resolved scope where the closed action permits it. Existing authentication events prohibit customer/project fields; do not widen canonical v1 to attach them.

Append denial/failure in an independent short PostgreSQL transaction after any failed mutation transaction rolls back. It cannot borrow a poisoned transaction or successful issuance event. Cancellation/client disconnect cannot be treated as proof that an append never committed: use a bounded independent cancellation/deadline policy whose values are separately reviewed. No request is admitted while mandatory audit is uncertain. A repeated observer invocation resolves the same exact binding; distinct attempts must remain distinct events.

Compatibility gap: existing `OperationReceiptRequestV1` requires a non-null target and `OperationReceiptV1` requires a positive non-null security version; existing receipt v1 already supports denial/failure outcomes but cannot safely bind an unknown anonymous subject, so it cannot represent an unknown anonymous failure by inventing a subject. A proposed additive internal authentication-outcome receipt is keyed by exact trusted environment/writer/operation plus event descriptor digest; stored outcome is terminal event ID and canonical stream sequence/digest only. Unique binding, conflicting digest denial and receipt/event atomicity need actual restricted-role tests. Do not modify existing receipt v1 or use a synthetic target.

## Outage journal/reconciliation candidate — not selected

Under ADR-0012 Option A, proposed `AuthenticationFailureJournalV1` is a closed internal object, not a public endpoint:

| Field group | Proposed invariants |
|---|---|
| Version and binding | Fixed schema version; exact approved environment/writer/stream references; server-created nonempty event/operation/correlation IDs; object locator derived only from the reviewed store binding and event ID |
| Event descriptor | Original trusted UTC occurrence time; existing AuthenticationDenied/Denied or AuthenticationFailed/Failed action/outcome; existing reason enum; Anonymous/null subject unless already verified; nullable exact subject/session/version only when valid; no customer/project fields for these existing authentication actions |
| Integrity | Canonical closed UTF-8 descriptor digest; rejects duplicate/unknown fields, noncanonical counters/times and conflicting IDs; no externally assigned canonical audit sequence/head |
| Prohibited data | No token/assertion/code/nonce/state/cookie/ticket, claim bag, headers/IP/user agent, raw request/URL/query, body, email/display name, exception/free text, evidence, or connection/key values |

Require conditional create and content match reconciliation. A timed-out write is UNKNOWN, never “not persisted.” Runtime cannot overwrite/delete existing objects; read/list/reconcile and lifecycle capabilities remain separate reviewed identities. An ordinary built-in broad write role plus `If-None-Match` application logic alone is insufficient enforcement. Exact platform grant definition, witness protocol/destination/cadence and quotas are unresolved bindings, so this candidate is not live-ready.

A worker validates descriptor/binding/digest and independent witness, then atomically inserts a unique journal-source receipt with one canonical audit event. Source occurrence time, source digest, storage receipt reference and reconciliation time remain in additive metadata. The existing ordered appender stamps post-lock trusted time: never backdate it or silently alter canonical v1. PC-D03 proposes lifecycle eligibility from the original occurrence time for source and linked disclosure, rather than restarting12months on reconciliation. This is an explicit data-governance interpretation requiring approval: the existing policy names the event timestamp, while canonical stream events use the reconciliation append timestamp. Until that interpretation and required metadata/read filtering are approved, do not claim production retention compliance or implement lifecycle actions. Receipt conflict denies; crash after canonical commit resolves receipt without another event. Reconciliation cannot create/rotate tickets, change subject version, revalidate a provider or grant any role.

Both stores unavailable, identity/network outage, unwitnessed gaps, exceeded capacity and attempted bypass remain incident/activation blockers. Even correct refusal has an unpreserved event in a total outage; do not label it policy-compliant. This exact handling, overload budget and any policy exception require human decision before live use. No sampling or silent drop is proposed.

## Ingress compatibility and live proof

Current `BffReviewedIngressContract` requires one exact immediate peer, original canonical Host, one HTTPS forwarded-protocol value and no forwarded Host/prefix/original-header inputs. Default denies any `X-Forwarded-For`; enabled mode accepts one parseable address. Microsoft documents ACA adding that header and appending caller values, so neither existing mode is assumed compatible with real public ingress. The rightmost provided address is not proof of the immediate socket peer. [Official ingress behavior](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview), checked2026-10-03 UTC.

Collect benign and adversarial exact peer/header observations in an approved disposable deployment without retaining request payloads. If benign traffic fails the current contract, activation stays blocked and a separately reviewed ingress amendment specifies exact peer lifecycle, trusted rightmost chain normalization and allowed header shape. Do not trust all VNet addresses, clear known-proxy protections, infer fixed addresses, accept caller forwarded host, or use a blanket forwarded-header switch. No proposed amendment is implemented here.

## Data lifecycle, migration and observability

Authority/session cleanup, key-version deletion eligibility, journal quotas/holds/witness schedule and deployed proxy topology remain protected operations decisions. Approved audit12months/soft-delete/purge30days/backup35days and session30minutes/8hours/provider-under15minutes remain unchanged. No production migration/grant or tombstone purge occurs. Proposed additive anonymous outcome/journal-source receipts require schema/role/retention review; preserve historical migration files and denied legacy rows.

Observe only allowlisted outcome/reason counters and opaque correlation references. Exceptions/provider bodies are never exported. Audit-lag and missing-witness thresholds are undecided operations values, not invented defaults. Incident response first denies admission/updates authoritative revocation when available and drains affected replicas; cached keys cannot guarantee instant cryptographic revocation. Rollback preserves audit, authority versions, journals/witnesses/tombstones and historical keys.

## Review decisions and conditional implementation sequence

| Decision | Requested role and concrete acceptance | Implementation boundary |
|---|---|---|
| PC-D01 | Identity/security: accept exact provider/direct-role protocol and required-property handling; external home proof stays denied until separately supplied | Local fake-HTTP adapter/strict parser cases only; real Graph consent/calls require additional authorization |
| PC-D02 | Platform/security: accept supported shared-key provider integration strategy after exact pinned dependency/lock audit | Local SDK-bound concurrency/recovery tests; real key/blob grants remain separate |
| PC-D03 | Technical/security/data-governance: select ADR-0012 candidate and exact anonymous outcome receipt/observer semantics; settle total-audit outage and retention/reconciliation boundary | Only separately approved bounded synthetic prototype; no live policy exception implied |
| PC-D04 | Platform/security: prove actual ingress compatibility or review exact amendment | Real peer observations need a priced/disposable session; no guessed permissive middleware |
| PC-D05 | Technical/security: accept proposed existing-type resource snapshot and exact-session CA-context verifier boundary; settle sidecar lifecycle/tenant proof and exact step-up/audit-unavailable public response/deadline/abuse controls | No production assessment/privileged route implementation until contract approved |

Conditional order: approve applicable exact contracts/test cases and dependency locks → record bounded isolated implementation packets → available-store outcome audit and fake-provider integration → supported key-provider tests and journal candidate if selected → independent review and complete configured regression checks → resource/privileged/ingress closure → fresh fully priced and explicitly approved live synthetic session → protected identity/role/CA/key/SQL/image/recovery evidence → human gate/release review. No implementation packet is READY merely because this review packet exists.

## Approval

Approved by: Pending
Date: Pending
[Acceptance tests and human tasks](https-production-test-packet.md) are proposed; no tests in that packet have run.
