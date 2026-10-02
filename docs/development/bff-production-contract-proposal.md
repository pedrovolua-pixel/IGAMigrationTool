# Production BFF integration contract proposal

Status: PROPOSED — exact public contract and consequential security decisions require owner review
Owner/reviewer: Technical and security owner; identity/platform owner for actual environment bindings
Date: 2026-10-02
Related: [execution plan](../../plans/active/bff-azure-deployment-preparation.md), [approved identity design](../security/health-assessment-identity-session-design.md), [foundation handoff](bff-identity-session-foundation.md)

This supplies concrete implementation decisions requested by implementation-plan.md's public-contract approval rule. The owner's “Do it” authorizes preparation and already approved independent engineering work; it is not acceptance of an unseen route/schema or new live permissions. Decisions below are proposed for local implementation. Live activation, actual users/CA policies, grants and priced Azure execution have separate checks/approval.

## D01 — Minimal versioned authentication transport

| Operation | Proposed exact behavior |
|---|---|
| `GET /bff/v1/session` | JSON `{schemaVersion:"bff-session-v1",authenticated:boolean,csrfToken:string}`; no identity claims, customer assignments, tokens, session reference or grant list. The opaque synchronizer token binds to the current anonymous/authenticated context. Only transport CSRF cookie issuance occurs; no product/store mutation. Response always no-store. |
| `POST /bff/v1/sign-in` | Require same-origin request and valid synchronizer token, strict empty JSON object, no caller return URL or scope. When configured activation is disabled, 403 and no provider activity. Otherwise supported single-tenant code+PKCE challenge; request fresh authentication with `max_age=0`; completion returns only to fixed `/bff/v1/session`. No scope/admission inferred from claims. |
| `POST /bff/v1/sign-out` | Require authenticated session, same origin and CSRF; strict empty JSON object. Revoke this store session before clearing cookies; return 204. This ends the local product session; it makes no global/provider logout claim. Store/authority failure denies without reporting successful revocation. |
| Existing `/bff/signin-oidc`, `/bff/signout-callback-oidc`, `/bff/signout-oidc` | Framework-owned protocol callbacks only; retain supported state/nonce/PKCE/correlation guards, exact tenant/client validation and narrow cookie path. No generic callback forwarding. Remote sign-out never supplies product customer authority. |

Unsupported methods: 405; unknown application paths: 404; missing authentication:401; policy, disabled activation, stale authority or CSRF:403; malformed supported JSON:400. Errors carry no provider/identity detail. No CORS credentials, browser bearer scope, session-list/revoke-other/reauthentication/customer-data operation, UI adoption of the local-demo contract or public admin route is introduced. Health endpoints keep the already approved diagnostic contract. Login/session endpoints are absent from the independent disabled host until D01 is accepted.

## D02 — Authentication/session-bound authority

Split current subject-only admission into (a) current provider/product subject eligibility and (b) authentication evidence bound to the particular protocol completion or opaque store session. The initial timestamp comes from the signature/issuer/audience/state-validated OIDC `auth_time` response to the fresh-authentication request; missing, future, malformed or stale time denies. Never substitute current server time or the newest authentication of another session.

Session checks bind immutable tenant/object identity, exact session reference, original authentication, provider-status cutoff and monotonic security version. Security version/assignment revocation applies on the next request. Role changes revoke old sessions; no subject-wide MFA boolean or timestamp can admit another session. Existing idle30min/absolute8h/provider<15min limits and per-request product-state lookup remain unchanged. Every missing binding/evidence source denies.

For the first integration, privileged authentication verifier remains deny-by-default unless the separately reviewed Entra authentication-context and phishing-resistant strength evidence is bound to the exact subject/session/security version/action. Do not treat `amr`, a coarse app role, a local flag, email or a different session as sufficient. No privileged operation is enabled by accepting D01/D02.

## D03 — Product-side enrollment and provider retrieval boundary

Persist explicit product enrollment/assignment/guest lifecycle only in module-owned control-plane tables, via a controlled migration and trusted administration path. No automatic enrollment at sign-in and no public administration operation. Read adapters enforce current active assignment, scope, guest sponsor/expiry/review, customer policy and resource state before any customer-store lookup. Missing or contradictory state denies. Existing product role/action/evidence rules remain authoritative; test fixtures cannot become runtime assignments.

Proposed provider reader: a distinct managed-identity application-only Microsoft Graph read adapter for **only already enrolled tenant/object IDs**, using `GET /v1.0/users/{objectId}?$select=id,accountEnabled,userType,externalUserState,signInSessionsValidFromDateTime`. No user listing, profile/default fields, group/member graph, sign-in log ingestion or Graph write method. The proposed consent is `User.Read.All` only, which nevertheless permits tenant-wide profile reads and requires separate exact access approval before provisioning. Do not request Directory.Read.All, User.ReadWrite.All or User.EnableDisableAccount.All. If the approved read permission cannot supply required properties, fail closed and return to security review; never expand permission automatically.

Microsoft describes `User.Read.All` as the least application permission for Get user, and separately identifies a read/write combination for accountEnabled. That is not proof that the selected restricted runtime query succeeds. Actual least-permission property access, cutoff semantics and unavailable/ambiguous responses require provider-spike evidence before activation. Provider account state alone does not verify CA or all grant changes. Product-role updates remain attributable, security-versioned authority changes; current provider-app-role revalidation needs its own accepted source/contract before live use.

References: [Get user permissions](https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0), [user cutoff property](https://learn.microsoft.com/en-us/graph/api/resources/user?view=graph-rest-1.0). This is an implementation proposal, not live Graph consent.

## D04 — Proxy, keys and audit integration

Development tests use direct real HTTPS. Production uses an explicitly configured trusted proxy network/addresses and fixed allowed host/origin, never blanket forwarded-header trust. Only the reviewed Container Apps ingress path may supply original HTTPS/host data. The actual trust configuration is a deployment input requiring negative spoofing and deployed-network proof.

The shared replica Data Protection ring is persisted in a dedicated private control-plane Blob object and protected using the approved Key Vault managed-identity pattern. Narrow key/blob grants, key version/rotation/recovery and database roles remain separately reviewed deployment inputs; no storage or key grant is made by local implementation. No startup migration or fallback plaintext/ephemeral production key ring.

Write payload-free append-only product security audit under the approved audit schema/lifecycle before reporting consequential operations successful. Failure cannot return successful sign-in/session revocation or a privileged action. Concrete writer/store identity and cleanup/12-month preservation follow the existing audit policy; no new retention default is selected. Operator, migration, ordinary ticket and customer roles stay distinct.

## D05 — Image evidence and Azure activation remain separate

Independent disabled executable/OCI inventory work may proceed under the existing health contract. Image scan findings and license inventories are observations; acceptance thresholds, exception handling, reviewed signer/trust, restricted engineering evidence and private builder/publication permissions require their own concrete decisions. An unsigned local provenance record is not accepted signed build provenance. Keep real registry manifest digest distinct from local Docker config ID.

Before any paid session: refresh delayed spending and regional capacity, price the exact resources and private builder path, bind actual image/users/proxy/key/SQL/registry inputs, review what-if and exact grants, obtain session/access approval, preserve runtime evidence outside disposable resources, then remove only exact session-owned resources. Existing $50 budget alerts do not stop spending. No paid window, new trust, Graph consent or production release is authorized by this proposal.

## Required approval and verification

Technical/security owner: accept/replace/reject D01/D02 and the bounded internal D03/D04 implementation design; return corrections for Graph/proxy/key/audit decisions. Environment-specific access and activation stay disabled pending separate exact review. No code implementing unapproved public routes or new access is permitted.

Tests after acceptance include anonymous/authenticated CSRF binding, wrong/missing subject/session/security version, independent sessions with different original authentication/MFA, exact deadline boundaries, database revocation/failure/concurrency, no auto-enrollment, account/cutoff/guest denial, no provider activity while disabled, callback/proxy spoofing, no credentials in client/logs, key/audit failure and all affected authorization paths. Entra/Graph/CA/Container Apps claims stay NOT VERIFIED until actual deployed tests run and human evidence review accepts them.
