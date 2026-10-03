# Azure Bsv2 quota support request: prepared scope

Status: PREPARED — opening the separate support workflow awaits explicit owner authorization
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
