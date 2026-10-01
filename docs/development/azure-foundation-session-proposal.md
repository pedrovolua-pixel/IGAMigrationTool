# Next Azure foundation session

Status: APPROVED — owner approved the exact synthetic session on 2026-10-01; Azure validation/create pending
Date: 2026-10-01
Scope: isolated synthetic development only, East US 2, Subscription 1

This is the concrete next session under [the Azure setup runbook](azure-pilot-development-setup.md), accepted ADR-0004 and G1. The owner asked to finish Azure configuration. AGENTS.md prohibits inventing retention/security policy; the browser confirmation policy requires confirmation immediately before creating new security-sensitive access. Approval of this proposal supplies the currently missing choices and exact access scope, not a G1 acceptance decision.

## Proposed configuration and access

- Existing pilot group plus platform-managed `rg-iga-pilot-dev-aca-eastus2`; existing approved isolated addresses 10.64.0.0/16, app subnet 10.64.0.0/23 and endpoint subnet 10.64.2.0/24. Source collector stays outbound HTTPS only.
- Internal Consumption Container Apps environment, static NAT egress; no applications or public listener.
- Private ACR Premium, closed RBAC Key Vault, PostgreSQL 18 Burstable Standard_B1ms / 32 GiB, empty private Blob engineering container, eight linked DNS zones, Log Analytics/Application Insights and private Monitor scope. No HA, geo backup, auto storage growth, database migrations, secrets or telemetry collection.
- **Proposed synthetic-session policy:** vault soft delete seven days, purge protection disabled; database rolling backups seven days; workspace operational telemetry retention thirty days, daily cap 1 GB. These are not customer/evidence retention policies. Application Insights tables may independently retain ninety days; collection remains disabled until table retention/redaction is approved and verified.
- **New access requiring confirmation:** the currently signed-in development owner becomes this disposable PostgreSQL server's Entra administrator; no other database/data principals. Recreate only the two prior distinct synthetic-work queue sender/receiver identities and their queue-scoped Data Sender/Data Receiver assignments. No app consent, federation, sign-in, registry/vault/blob data grants or Conditional Access policy changes.
- Globally unique development names receive a reviewed non-secret session suffix. Protected tenant and administrator identifiers are supplied only in ephemeral Azure parameters, never committed or published.

## Spending and time boundary

Request **USD 25 reserved from the remaining USD 50 monthly allowance**, at most **24 hours from first paid resource creation**, with exact cleanup as soon as configuration checks finish. Refresh delayed actual costs before starting; reserve previous unreported charges. If the available monthly allowance cannot cover this entire session, stop before creation. Budget alerts and ingestion caps do not stop spending.

| Component | Conservative 24-hour estimate/reserve (USD) |
|---|---:|
| ACR Premium, one region, no replication | 1.67 |
| NAT + static IPv4 + at most 1 GB processing | 1.25 |
| Standard Service Bus base + operations | 10.80 |
| Five private endpoints + small traffic reserve | 1.40 |
| Eight private DNS zones, reserve full monthly zone amount | 4.00 |
| PostgreSQL B1ms compute + 32 GiB disk and backup reserve | 0.80 |
| Managed Standard load balancer and possible platform static IP: 24h reserve | 0.72 |
| Empty Blob/Key Vault, monitoring, bandwidth and contingency | 4.36 |
| **Session envelope** | **25.00** |

Official retail reads on 2026-10-01 returned ACR Premium 1.6666/day, PostgreSQL B1MS 0.017/hour and storage 0.115/GB-month. NAT/IP/broker rates are recorded in the prior approved proposal. DNS retail reads confirmed USD 0.50 per private zone-month for the first 25 zones. Private endpoint and managed load-balancer/IP estimates require meter reconciliation before creation. This is an estimate and reserve, not a quote or guaranteed cap; no load or telemetry collection is included. ACR alone is about USD 51.66 for October, so an always-on deployment cannot fit the selected USD 50 limit.

Primary sources: [Azure retail API](https://learn.microsoft.com/rest/api/cost-management/retail-prices/azure-retail-prices), [Private Link pricing](https://azure.microsoft.com/pricing/details/private-link/), [DNS pricing](https://azure.microsoft.com/pricing/details/dns/), [Container Apps managed resources](https://learn.microsoft.com/azure/container-apps/custom-virtual-networks).

## Exact cleanup and recovery constraints

After preserving sanitized checks, delete only resources created by this session: delete the Container Apps environment through its provider (which owns cleanup of its managed infrastructure; do not directly modify/delete managed group resources), Monitor private scope/links/endpoint, workspace/Application Insights, empty storage and its endpoint, registry and endpoint, database/admin/configurations/endpoint, vault and endpoint, eight session DNS zones/links, broker/queue/grants, the two session identities, VNet/subnets, NAT and static IP. Keep the existing pilot group, both budgets, inactive Entra registration and deployment history. Inventory ownership must match the reviewed what-if before deletion; never delete the entire pilot group.

The vault will remain recoverable in Azure soft delete for seven days; **do not purge it**. Its name cannot immediately be reused. PostgreSQL removal deletes the disposable server and rolling backups; no customer data or evidence may enter it. Preserve results outside all disposable resources before cleanup. If preservation/ownership is uncertain, stop cleanup and escalate promptly while resource hours continue. No automatic future monitoring is installed by this proposal.

## Completion limits

Provider validation and what-if precede creation. After approval, provisioning/configuration/private DNS reads can proceed. Actual allowed/denied workload traffic, BFF authorization-code/session/federation, customer/project authorization, private image build/pull, monitoring redaction, restore, evidence-store intake/purge and signed G1 evidence remain implementation/test dependencies. Repository `src/server` has local prototype libraries, not deployable application images or a tested BFF host. Do not enable or invent those runtime paths to claim setup complete.
