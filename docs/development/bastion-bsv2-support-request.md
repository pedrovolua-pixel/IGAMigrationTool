# Azure Bsv2 quota support request: prepared scope

Status: SUPERSEDED FOR ACTIVE ACCESS — historical evidence retained; HTTPS preparation replaces the desktop path
Date: 2026-10-03 UTC
Requested role: Repository owner / selected subscription owner
Canonical authority: [approved SKU and quota amendment](bastion-b2sv2-amendment.md)

## Executed result

The coordinator submitted the authorized automatic increase from zero to **2 vCPUs** for **Standard Bsv2 Family**, exact provider family `standardBsv2Family`, in **Subscription 1 / East US2**. Azure returned **Unsuccessful: 1; Successful: 0; Partial increase: 0** and directed a support request. A refreshed quota row remains **0 used / 0 limit**. The receipt's “0 of 2” display is a requested limit, not proof of granted capacity.

Automatic approval review rejected clicking **Create a support request** because it opens a separate external support workflow beyond the approved automatic quota submission. No support form was opened and no ticket was sent. Do not use another browser, CLI, API or indirect path to bypass that rejection. The owner must explicitly authorize this support action before it is attempted again.

## Exact proposed request text

**Title:** Request 2-vCPU Standard Bsv2 Family quota in East US2

**Description:**

> Please review a quota increase to a total limit of 2 vCPUs for Standard Bsv2 Family (`standardBsv2Family`) in East US2 on the selected Subscription 1. Current family usage is 0 and the limit is 0. The Azure Quotas automatic adjustment to 2 was unsuccessful and directed us to support. The intended use is one `Standard_B2s_v2` nonzonal private Windows development desktop, 2 vCPU / 8 GiB, for bounded synthetic development tests. No VM has been deployed by this request. Please confirm whether this quota can be approved and whether any subscription or regional availability restriction applies. We are requesting no additional families, regions, public VM access or paid support-plan purchase.

The selected subscription identifier is supplied by the authenticated Azure portal context, not committed here. The support form and its mandatory contact/diagnostic fields have not been observed; no contact details, phone, attachments, consent or urgency assertion are invented. Opening and preparing this specific quota-support workflow is the pending action. Any extra disclosure, access, paid commitment or legal agreement outside its reviewed scope must be examined before submission. Customer evidence, credentials, private browser screenshots and repository logs are not proposed attachments.

## Completion and remaining limits

This human dependency closes when the owner explicitly authorizes the separate Azure support action and the coordinator records the actual submitted ticket/result, or the owner supplies a protected submitted-ticket reference. A sent ticket does not prove quota approval. A granted quota does not prove VM allocation, exact-image disk/browser compatibility or effective network isolation. Full priced deployment and the image/network/BFF/key/audit/identity prerequisites remain open.

## Owner switches the active access path to HTTPS — 2026-10-03 UTC

The repository owner now requests “Let’s move to the https instead of bastion.” The [active HTTPS preparation plan](../../plans/active/https-pilot-portal-preparation.md), [Proposed ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [technical/test proposal](https-pilot-portal-proposal.md) replace the Bastion browser path and post-pilot timing preference. Prior template approvals and failed quota evidence remain historical. VM quota/support/image/desktop/RDP prerequisites are SUPERSEDED for this path, not technically resolved. No VM/Bastion was deployed or removed; no support ticket is pursued.

The recommended candidate is a new external VNet-integrated Consumption-profile Container Apps environment exposing only the portal/BFF through built-in HTTPS, with Entra organizational login and private protected services. Exact architecture/local disabled-template approval, real BFF/provider/key/audit composition, reviewed users/roles/CA, full priced public session and live verification remain required. No public ingress, resource, invitation, grant, migration, live sign-in or new spending was performed. G1–G9/Milestone2 remain NOT VERIFIED. Private-board publication remains blocked by the separately rejected publisher credential/network action; its BFF/access snapshot is stale. Historical sections below or above retain their original time-bound state.
