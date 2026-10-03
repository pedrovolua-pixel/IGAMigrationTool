# Bastion deployment session: provider preflight and VM decision

Status: READ-ONLY PREFLIGHT COMPLETE — VM decision and live deployment blocked
Recorded: 2026-10-03 UTC (2026-10-02 local)
Owner: Azure/BFF coordinator; repository owner decides SKU and quota request
Authority: [accepted ADR-0010](../../architecture/decisions/ADR-0010-private-pilot-browser-access.md), [PA03 plan](../../plans/active/private-pilot-browser-access-preparation.md), [operator handoff](bastion-development-access.md)
Evidence: [sanitized provider and price record](evidence/bastion-deployment-preflight-20261003.json)

## Executed next step

The owner requested “Move to the next step.” Read-only Azure portal and Cloud Shell checks refreshed the selected Subscription 1, its empty pilot resource group, current pilot costs, regional/family quotas, VM SKU restrictions and an exact official Windows image. Current public Microsoft retail meters were captured separately without credentials. No resource, quota request, support message, permission, password or template change occurred.

The pilot inventory remains empty with empty search, Type = all and Location = all. October pilot cost and the monthly budget's evaluated spend show USD 0.22. Both pilot monthly budgets remain USD 50; aggregate evaluated spend shows USD 0.00. Ingestion is delayed. No remaining allowance, hard cap, complete provider soft-delete inventory, updated subscription total or forecast is inferred.

Cloud Shell initially selected the other subscription. Those provider observations were discarded. Every retained provider command supplied the selected subscription explicitly, and its name/state were confirmed before interpreting results. Broad CLI image offer matching also returned a different offer; retained image results explicitly match `WindowsServer` and use the exact URN below. Protected subscription/account identifiers remain outside Git and the private board.

## Concrete VM blocker and options

| Item | Accepted B2s | Proposed B2s v2 |
|---|---|---|
| SKU | `Standard_B2s` | `Standard_B2s_v2`; not selected |
| Region | East US2 | East US2; no region change |
| Provider restriction | Location and zones 1/2/3: `NotAvailableForSubscription` | Zones1/2/3 restricted; no Location restriction reported |
| Family quota | `standardBSFamily`: 0 used / 10 limit | Exact provider family `standardBsv2Family`: 0 used / 0 limit |
| CPU/RAM | 2 vCPU / 4 GiB candidate | 2 vCPU / 8 GiB; x64, Gen1/2 compatible in principle |
| Windows Consumption retail | USD 0.0496/hour; USD 1.1904/24h | USD 0.0924/hour; USD 2.2176/24h |
| Result | Cannot provision despite spare quota | Cannot provision until family quota is approved; nonzonal candidate only, actual allocation still unproved |

Total regional quota is 0 used / 10 vCPU. This does not override SKU restrictions or establish the new family's quota. No availability-zone placement is proposed. A zone-only restriction is not proof of regional capacity. The alternative costs USD 1.0272 more for 24 compute hours. Neither option supports an always-on full foundation within USD 50/month.

**Recommended decision for owner review:** approve a bounded local ADR-0010 VM-size amendment to `Standard_B2s_v2`, and explicitly authorize submitting a request for **2 vCPU in `standardBsv2Family`, East US2, Subscription 1**. The request can require Microsoft quota/support review. No support request is sent without that authorization. The proposed quota request provisions no VM; paid resource creation would require the separately reviewed session. Requested capacity is for one synthetic operator desktop only. It does not select a paid Bastion tier, public VM IP, customer access or public portal.

Alternative: retain the accepted `Standard_B2s` and authorize asking Microsoft to resolve the exact Location restriction. Its existing 10 vCPU allowance makes a generic quota increase insufficient. The provider may not make this older SKU available; do not promise a resolution.

AGENTS.md requires owner approval for a consequential architecture/security decision. The existing template and compiled policy fix the B2s candidate; they remain unchanged while this amendment is pending. After attributed approval, update the accepted decision and small template/policy/document packet, build/lint, execute unsafe mutations and independent review. Do not add an unchecked fallback or claim allocation from a successful local test. Paid session/access approval remains separate after the remaining inputs and complete preview are ready.

## Image and networking evidence

Provider listing and exact image GET found `MicrosoftWindowsServer:WindowsServer:2022-datacenter-g2:20348.5622.260906` in East US2: x64, V2, Windows, Active, no purchase plan. This is a candidate version, not a deployed or approved guest. The GET returned no minimum disk size; 128 GiB compatibility still needs exact publisher/provider validation. Generic Windows guidance says Marketplace OS disks are usually 127 GiB, which cannot close this exact-image item. Desktop Experience, current Edge build, licensing, patches and actual browser readiness also remain unverified.

Non-author primary-source research confirms Developer's same-VNet/one-connection portal RDP capability and East US2 support. It did not establish an authoritative exact shared-service RDP source. Generic NSG guidance refers to AzureBastionSubnet, which Developer does not create. A Q&A/platform-address guess is insufficient. Keep the source input unbound and obtain provider-supported exact guidance before deployment.

Required DNS/platform/identity/certificate/update host/port flows also remain unbound. NSGs enforce IP/ports, not application purpose or FQDN; CDN address changes need refreshed evidence. WireServer guest-agent traffic has documented NSG exceptions. Removing an allow rule does not terminate an established connection. Preserve the accepted deny boundary and disconnect/revoke/deallocate during closure; no broad Internet rule, resolver/firewall, role or guest-agent policy is added by this preflight.

Primary references: [Bsv2 specifications](https://learn.microsoft.com/en-us/azure/virtual-machines/sizes/general-purpose/bsv2-series), [quota/capacity](https://learn.microsoft.com/en-us/azure/virtual-machines/quotas), [SKU restrictions](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-sku-not-available), [Bastion SKUs](https://learn.microsoft.com/en-us/azure/bastion/bastion-sku-comparison), [Developer connection](https://learn.microsoft.com/en-us/azure/bastion/quickstart-host-portal), [Bastion NSGs](https://learn.microsoft.com/en-us/azure/bastion/bastion-nsg), [image GET schema](https://learn.microsoft.com/en-us/rest/api/compute/virtual-machine-images/get?view=rest-compute-2025-04-01), [Windows disk guidance](https://learn.microsoft.com/en-us/azure/virtual-machines/windows/expand-disks), [Server 2022 features](https://learn.microsoft.com/en-us/windows-server/get-started/whats-new-in-windows-server-2022), [Edge OS support](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-supported-operating-systems), [Edge endpoints](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-security-endpoints), [platform tags](https://learn.microsoft.com/en-us/azure/virtual-network/service-tags-overview), [platform IP exceptions](https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16), [stateful NSGs](https://learn.microsoft.com/en-us/azure/virtual-network/network-security-groups-overview).

## Preliminary cost worksheet — not a complete session quote

Public [Microsoft retail API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices) reads bind 21 exact product/meter/SKU/region/unit/tier records in the evidence. A public network read initially failed under the local sandbox, then succeeded with scoped read permission. Private Link pricing was rate limited once; a later backed-off retry succeeded. Meter IDs alone are not a unique SKU/tier selection; the worksheet uses composite records.

| Candidate 24h component/reserve | USD |
|---|---:|
| One ACR Premium registry/day |1.6666|
| NAT 24h, static IPv4 and 1 GB processing |1.245|
| Service Bus monthly alternative plus operations reserve |10.80|
| Five private endpoints 24h plus traffic reserve |1.40|
| Nine private DNS zones, full first-tier monthly reserve |4.50|
| At most 1M DNS queries reserve |0.40|
| PostgreSQL B1ms 24h, 32 GB storage/backup reserve |0.80|
| Managed load balancer and possible platform IPv4 |0.72|
| Vault/empty Blob/disabled monitoring/bandwidth/contingency |4.36|
| Candidate Windows B2s v2 compute 24h |2.2176|
| E10 LRS OS disk, full-month conservative reserve |9.60|
| OS disk operation reserve |0.20|
| **Preliminary fixed infrastructure subtotal** |**37.9092**|

These are proposed synthetic quantities and conservative reserves, not an offer-specific quote or paid authorization. Service Bus hourly and monthly base rows are alternatives, never summed. PostgreSQL storage/backup reserve assumes 24h proration; OS disk and DNS reserve full months. No discount/free grant is assumed. Actual disk/month billing and the subscription offer need verification. Workstation disk persists after deallocation. Nine zones means the eight foundation zones plus browser default-domain zone, not nine public zones.

The subtotal excludes unbound deployable BFF sizing, private image-builder resources, dedicated live key/audit operations and workload usage. The absent foundation must be recreated before browser DNS can bind observed environment metadata. Exact protected names/inventory/what-if, delayed charges, complete cost and cleanup ownership are missing. No new USD 25 session, USD 40/45 allowance, always-on environment or spend decision is requested from this subtotal.

## Ordered dependencies and human tasks

| Task | Requested role | Completion condition |
|---|---|---|
| Choose VM/capacity route | Repository owner | Attributed decision on the exact B2s v2 amendment and 2 vCPU quota request, or original B2s availability request; only that chosen request is submitted. |
| Establish Developer RDP source and egress | Platform/network reviewer | Provider-supported exact source, fresh destinations and reviewed effective/guest boundary; no guessed address or broader rules. |
| Verify image and routine OS use | Platform/operator | Exact image minimum disk and patch/browser proof; owner-controlled credential handoff and distinct routine nonadministrator account. |
| Complete BFF/foundation binding | Engineering/platform/identity reviewers | Deployable host/provider/key/audit/private ingress/SQL and private build composition, protected app/identity inputs and observed environment metadata. The disabled diagnostic image cannot become the live portal. |
| Approve exact paid session | Repository owner/operations reviewer | Complete priced preview, current delayed costs, duration/reserve, exact creation/removal inventory and operator-enforced disposal; approve required access at action time. |

No capacity or architecture amendment resolves the other rows. [The post-pilot HTTPS portal](../../plans/active/post-pilot-https-portal.md) remains deferred after attributed pilot completion. PA03/live G1–G9/Milestone2 remain NOT VERIFIED.

Configured bootstrap 37082060723 and package 37082060721 passed on source 1468303. This documentation/preflight packet changes no runtime, template, migration or Azure policy; no new runtime test is claimed. Scoped document/JSON/link/price arithmetic/secret checks and non-author review are recorded in the evidence before closure.

The private BFF status board remains stale for this update: automatic approval review rejected the specific official publishing helper credential/network action for managed-proxy bypass. Its separately requested approval is pending. Last confirmed BFF snapshot 4346e0f is historical; unrelated newer pilot publications remain preserved. Canonical records are authoritative.

## Current amended candidate and request outcome — 2026-10-03 UTC

[The owner-approved B2s v2 amendment](bastion-b2sv2-amendment.md) supersedes the prior pending SKU decision. The template is locally verified; the automatic quota request failed and refreshed quota remains zero. [The prepared separate support action](bastion-bsv2-support-request.md) was blocked by automatic approval review and needs explicit authorization. No ticket or paid resource was created. Other exact image/network/BFF/access/cost prerequisites remain open; earlier observations above are historical.
