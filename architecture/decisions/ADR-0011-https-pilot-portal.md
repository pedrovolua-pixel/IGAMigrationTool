# ADR-0011: HTTPS pilot portal with organizational login

Status: Proposed
Date: 2026-10-03 UTC
Decision owners: Repository owner acting as technical/security owner; platform and operations owners

## Context

The owner now requests: “Let’s move to the https instead of bastion.” This supersedes the earlier preference to use Bastion during the pilot and defer HTTPS until pilot completion. It authorizes preparation of this alternative, not an unspecified public deployment, paid session or change to identity policy. No VM or Bastion was deployed; the unsuccessful VM quota request is historical and its support follow-up is no longer an active dependency.

The current environment template is internal (`internal: true`, public network access disabled). Microsoft documents that an internal environment cannot be changed to accept public traffic. Setting only an app's `external` ingress flag is insufficient. A direct public portal requires a new external environment, while protected data services can retain private connectivity.

## Decision drivers

- Customers open an HTTPS URL and sign in from an ordinary browser without a VPN, Bastion or Azure VM account.
- Preserve accepted ADR-0002/0004/0009, organizational Entra/B2B admission, MFA/Conditional Access, server sessions and authoritative customer/project permissions.
- Keep PostgreSQL, Blob evidence, shared keys, Key Vault and registry private; preserve the reviewed Service Bus Standard public endpoint exception and static egress.
- Prefer the fewest additional services. The USD50 monthly development allowance does not fund an assumed always-on customer service.

## Options considered

### Option A: Direct Container Apps HTTPS ingress — recommended for a bounded development portal

Create an external, VNet-integrated workload-profiles environment with only the Consumption profile. Publish only the reviewed portal/BFF through built-in HTTP ingress with `allowInsecure: false`. Use its generated Azure hostname and TLS endpoint initially; require one exact canonical HTTPS origin and exact Entra web callback/logout URIs. Retain private backend connectivity and NAT-controlled broker egress. Workers have no ingress or environment-level route targets.

Advantages: fits the selected application platform; no desktop or separate edge appliance; Azure supplies the hostname and TLS termination. Disadvantages: internet traffic reaches the BFF directly; no WAF is supplied by this choice. Rate/abuse controls, safe error responses, proxy trust and denial evidence must be reviewed before activation. Scale-to-zero does not make PostgreSQL, NAT, private endpoints or ACR free.

### Option B: Front Door Premium and Private Link to an internal Container Apps origin

Advantages: retains a private origin and provides an edge suitable for a separately reviewed WAF/production design. Disadvantages: additional paid edge/private-link charges and proxy hops; the current one-hop BFF contract cannot be reused without analysis. No complete affordable quote exists. Not recommended for the current development allowance.

### Option C: Continue the Bastion development desktop

Preserves accepted local ADR-0010 templates, but fails the owner's new customer-browser preference and retains failed VM quota/image/desktop prerequisites. Removed from the active path; stored templates and executed evidence are retained.

## Proposed decision

Approve **Option A for local, disabled-by-default preparation only**, under [the exact technical proposal](../../docs/development/https-pilot-portal-proposal.md) and [conditional work plan](../../plans/active/https-pilot-portal-preparation.md). This is a proposal, not acceptance. Public reachability, real sign-in, user grants, paid resources and production release require their separately bound review and authorization. No new data access, roles, enrollment, consumer identity, audit retention or key deletion policy is proposed.

## Rationale

Direct managed ingress supplies the desired browser experience with the existing Container Apps platform. Private backend connectivity remains distinct from public browser reachability. A separate environment avoids falsely treating the internal environment as convertible or widening its accepted template.

## Consequences

### Positive

- The active browser path has no VM-family quota, Windows image, RDP password or Bastion prerequisite.
- Infrastructure and policy tests remain versioned in the repository; no portal-only configuration is the source of truth.
- Backend isolation and server-side product authority retain their existing requirements.

### Negative and trade-offs

- Public ingress adds an internet threat boundary and an operational abuse/cost risk. Login alone does not prevent resource consumption by anonymous traffic.
- The permanently disabled diagnostic image cannot become the portal. Production BFF/provider/key/audit composition remains incomplete.
- Generated hostnames require exact binding after environment creation; no wildcard redirects or inferred proxy addresses.

## Security, operations, and cost impact

HTTP port80 redirects to HTTPS when insecure ingress is disallowed; this is not a claim that port80 disappears. Microsoft documents TLS1.2/1.3 ingress termination. The BFF must accept forwarded protocol only from proved immediate ingress peers; do not clear known-proxy checks, trust the whole VNet, trust client-supplied forwarded host, or enable the blanket forwarded-header environment switch. Microsoft overwrites `X-Forwarded-Proto` but appends `X-Forwarded-For`; deployed negative tests must bind the exact topology. An unprovable peer contract blocks activation and requires a reviewed amendment, not a guessed range.

Public BFF is the only application entry point; no environment HTTP routes to internal apps, extra public TCP ports, public database/key/blob/registry or debug console product route. Azure management access remains subject to its separate Azure RBAC. No replacement of the approved BFF by platform authentication alone.

A complete time-bounded quote must include the managed infrastructure group, compute/request charges, private registry/build, NAT/IP, database/backup/storage, private endpoints/DNS, broker, keys and monitoring. ACR Premium alone was previously quoted above USD50 for a full October; that historical observation is not a current complete quote. Do not assume free grants remain available or allow an always-on deployment under the USD50 alert budget. No spend limit or deletion policy is changed by this proposal.

## Migration and reversibility

Keep existing internal foundation/Bastion templates intact. After approval, create separate optional HTTPS modules/composition and negative checks, defaulting public ingress and live sign-in to disabled. No in-place environment conversion, automatic migration, enrollment or grants. Later deployment uses a fresh priced resource scope and manually approved SQL migrations; preserve audit, authority and key records. Rollback disables admission and public ingress, drains replicas and preserves durable state. Disposal is limited to an explicitly approved disposable inventory.

## Validation

Execute the proposal's HTTPS-T01–T08 matrix on the exact source and, separately, the approved live deployment. Non-author review is engineering evidence, not human gate acceptance. G1–G9/Milestone2 remain NOT VERIFIED.

## References

- [Container Apps environment networking](https://learn.microsoft.com/en-us/azure/container-apps/networking)
- [Ingress, TLS and forwarded headers](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)
- [Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing)
- [Front Door with Private Link](https://learn.microsoft.com/en-us/azure/container-apps/how-to-integrate-with-azure-front-door)
- [Accepted private development access](ADR-0010-private-pilot-browser-access.md)
- [Approved identity/session design](../../docs/security/health-assessment-identity-session-design.md)

## Approval

Accepted by: Pending
Date: Pending
Requested scope: Option A local disabled templates and review preparation. Actual public exposure, identity/grants, spending and release remain separate.
