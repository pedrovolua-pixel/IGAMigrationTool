# HTTPS pilot portal: technical and test proposal

Status: APPROVED for bounded local disabled templates/tests only — Azure/public session and live production composition remain gated
Date: 2026-10-03 UTC
Product basis: [approved feature003](../../specs/003-health-assessment/product-spec.md), [technical spec](../../specs/003-health-assessment/technical-spec.md), [test plan](../../specs/003-health-assessment/test-plan.md)
Owner: Azure/BFF coordinator; technical/security owner accepted local [ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md)

## Customer experience and scope

The owner chooses HTTPS instead of Bastion now. The proposed customer opens one HTTPS portal URL, completes the existing organizational Entra sign-in with reviewed MFA/Conditional Access, and sees only authorized customer/project content. No Azure subscription role, VPN or desktop login is needed for portal use. External customers use the already selected organizational B2B admission path, with exact home-organization proof, sponsorship and explicit product assignment; an invitation or successful login alone grants no product authority. Named requests remain in protected intake `LOCAL-INTAKE-20261002-01`, unenrolled.

This changes the proposed access topology and scheduling, not feature scope. Existing FR-HAS-40–46 presentation/publication, FR-HAS-50 lifecycle, AC-HAS-9 isolation and TP-HAS-009/013/015 plus approved BFF identity/session controls remain authoritative. It does not add consumer login, self-registration, public reports, new roles or customer-system connectivity.

## Accepted local design / gated target topology

1. A **new** external, VNet-integrated Azure Container Apps workload-profiles environment in the selected East US2 pilot development region, using only its Consumption profile and a separately bound dedicated delegated subnet. Verify regional/subscription capacity before deployment. Do not convert the current internal module.
2. One portal/BFF app receives built-in HTTP ingress, `allowInsecure: false`, no public TCP/additional ports, exact generated Azure HTTPS hostname initially, and one fixed canonical origin. No custom domain purchase or extra edge service is selected. Do not create environment-level HTTP routes to internal apps. Workers remain without ingress.
3. TLS terminates at Azure ingress; HTTP redirects to HTTPS. Bind observed ingress peer(s) and the exact header behavior before the BFF trusts forwarded protocol. Keep D01's fixed canonical Host/origin, reject untrusted/ambiguous headers, and retain approved cookies, CSRF, OIDC protocol and server-side session authority. No blanket trust fallback. A mismatch with the existing exact-IP seam is an unresolved architectural input, not permission to weaken it.
4. Private PostgreSQL, Blob evidence/control-plane keyring, Key Vault and registry stay behind their reviewed VNet/private DNS boundaries. Dedicated workload identities retain separately reviewed exact scopes. NAT/static egress and Entra-only opaque work signals preserve Service Bus Standard's accepted restricted public-endpoint exception; no SKU or broker trust amendment.
5. Real provider observation, managed-identity confidential-client redemption, authority/audit functions and shared Blob/Key Vault Data Protection adapters must be deployable and independently verified. The current permanently disabled diagnostic host cannot serve customer UI or real login. Audit-outage preservation still needs its separate reviewed durable contract/policy decision; ordinary logs are insufficient.

See the [proposed architecture view](../../architecture/diagrams/06-proposed-https-pilot-portal.svg). Official Microsoft icons identify only their respective services; this is a proposal, not deployed topology.

## Interfaces, data and failure behavior

Reuse the approved [BFF authentication contract](../../contracts/bff-authentication/bff-v1.openapi.json); do not create new authentication routes, browser-visible tokens, wildcard redirects, cross-origin APIs or anonymous protected-data access. Pin exact observed callback/logout URLs in protected configuration and Entra before activation. Keep authority enrollment/assignment, per-request role/scope/security-version checks and exact session revocation. A customer role must be chosen explicitly from the approved matrix before assignment.

No new schema, startup migration, retention, deletion, session lifetime or key-destruction rule is selected. Preserve existing audit retention, recovery/tombstone and old wrapping-key requirements. Missing provider, proxy, key, SQL authority/audit or trust evidence keeps admission disabled. Dependency failure denies protected requests; unknown commits reconcile using the existing durable protocol. Abuse/rate controls and responses must be specified and reviewed before public activation; do not invent thresholds here.

## Components and repository sources

| Area | Proposed change after exact local approval |
|---|---|
| IaC | Separate optional external environment module, disabled ingress composition and deliberately incomplete protected parameter template under `infra/bicep/`; retain internal/Bastion history |
| Infrastructure checks | Compiled-template/input/composition negative policies under a new `tests/infrastructure/HttpsPortal/` packet |
| BFF | Complete separately approved production composition before live sign-in; do not relabel diagnostic image |
| Entra | Later exact HTTPS callback/logout and named enrollment/role/CA/federation review; no grants in local packet |
| Canonical records | Proposed ADR, topology, current plans/status/evidence and template inventory |

Every deployed setting must be reproducible from reviewed repo templates and protected inputs. Portal actions may verify or execute approved templates; they must not create undocumented configuration. Build/registry/private-pull and exact signing/image acceptance still need their independent proof.

## Cost and operations

Retain the existing USD50 monthly development allowance. No new paid session is approved. Direct ingress avoids a separate edge SKU, but the full private foundation is not demonstrated affordable as an always-on service. Consumption free grants are shared at subscription level and cannot be assumed unused. Scale-to-zero does not stop database, NAT, IP, private endpoints, registry or retained storage charges. Container Apps private endpoint/planned-maintenance features can introduce management charges; do not add them casually to this direct public candidate.

Before an Azure execution proposal, refresh complete rates and current spend, cap replica/session scope, include the platform-managed group and cleanup/residual storage, and name deployment/incident/cleanup owners. An alert budget is not a hard spending cap. Present a bounded synthetic test session and its complete reserve for explicit approval; do not carry the expired USD25/24-hour approval into this topology. Key/audit preservation cannot be replaced by disposable vault deletion defaults.

## Approved local test plan (all runtime/live cases NOT VERIFIED)

| Case | Mapping | Required evidence |
|---|---|---|
| HTTPS-T01 — configuration | G1, ADR-0004, public boundary | Compile/lint/format new modules; negative mutations deny public data services, wrong environment/subnet, added edge/TCP/routes, missing exact origin, live sign-in default, unbound inputs and drift from approved limits |
| HTTPS-T02 — TLS/host/proxy | D01, TP-HAS-009 | Deployed HTTPS trust/TLS, HTTP redirect without protected content, canonical host denial, raw-path denial, forwarded protocol/host/for spoof and ambiguous-chain denial; exact immediate peer proof, not guessed IPs |
| HTTPS-T03 — login/session | Approved identity design | Real federated code redemption/PKCE/state/nonce/signature/issuer/audience tests, correct secure cookies/CSRF/logout, no browser tokens; two-replica key continuity and renewal/revocation |
| HTTPS-T04 — authorization | AC-HAS-9, TP-HAS-009 | Anonymous, unenrolled, missing-role, wrong-customer/project, suspended/revoked and consumer-origin denials; named organizational and separately proved B2B successes |
| HTTPS-T05 — private services | ADR-0002/0004, G1 | Internet denied for DB/blob/vault/registry; correct private DNS/workload identity; exact SQL/Blob/key scope negatives; no worker/route/debug exposure; restricted broker outside-egress/wrong-identity denials |
| HTTPS-T06 — audit/recovery | ADR-0009, TP-HAS-015 | Atomic audit/authority, outage preservation, unknown-commit reconciliation, immediate product revoke, restore without resurrection; retained key versions and safe rollback |
| HTTPS-T07 — browser/abuse | TP-HAS-013, approved performance targets | Actual deployed UI/login/logout, manual accessibility, reviewed abuse/rate behavior, no protected log/error content and exact versioned performance evidence |
| HTTPS-T08 — session closure | G1, operations | Complete current quote/owner approval, exact what-if and inventory, measured charges, admission/ingress disable/drain and preservation/approved disposal proof |

Local fixtures do not prove provider, TLS, live networking, CA, browser accessibility, performance or release gates. Run affected runtime/SQL/frontend/infrastructure/package/secret/CI checks after implementation, obtaining non-author review. The earlier browser keyboard-fragment CI failure remains unresolved; a documentation proposal does not fix it.

## Decisions and inputs to complete

- **Technical/security owner — closed:** accepted ADR-0011 Option A for local disabled templates/tests in [the exact approval](https-pilot-portal-approval.md); no paid deployment or public activation is included.
- **Engineering/platform — next:** approved local templates plus production BFF/provider/key/audit composition, observed proxy/host/network/capacity and private builder/image proof. Some production authority decisions are separately unresolved; no implementation authority is manufactured by this proposal.
- **Identity/product owner — before real access:** protected immutable users/home-organization proof, exact customer roles/scopes, licensing/CA, provider consent and federated trust. Existing intake is reused; account details remain outside Git/site.
- **Repository/operations owner — before Azure/public access:** exact full priced synthetic session, disposal/preservation, operations/abuse controls and public exposure authorization. Production/customer-evidence release retains all existing gates.

## Current execution

Only architecture research and documentation/diagram preparation were performed for this change. No resource, public endpoint, invitation, role, credential, DNS purchase, SQL migration, live sign-in or support ticket was created. Bastion quota/support/desktop prerequisites are removed from the active HTTPS path, not solved. Canonical status/evidence will record executed documentary checks and independent review. The private board remains stale because automatic approval review rejected its publisher credential/network action; the exact publisher approval is still pending.

## Research constraint: actual ingress seam

The current `src/server/hosts/BffFoundation/BffHostingContracts.cs` contract defaults `ForwardClientAddress` to false and rejects any `X-Forwarded-For` header in that mode. Azure ingress supplies that header. Its reviewed true mode accepts one IP and one exact immediate peer; benign ACA traffic and client-injected multi-address chains must be tested against the actual deployed configuration. Do not claim compatibility from TLS reachability or a public IP readback. Public ingress traverses platform infrastructure, so an app-subnet NSG alone cannot enforce the internet application boundary. Sources: [ingress headers](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview), [custom VNet infrastructure](https://learn.microsoft.com/en-us/azure/container-apps/custom-virtual-networks).

## Attributed local approval and execution

The owner explicitly approved the presented ADR-0011 Option A local template/test request against `50cf4fd`. [The frozen packet](https-pilot-portal-approval.md) authorizes HTTPS-P01 in an isolated author checkout with non-author verification. The target topology does not authorize public admission, a production host, grants or spending. Earlier preparation-only observations above retain their original checkpoint; implementation evidence is recorded after execution.
