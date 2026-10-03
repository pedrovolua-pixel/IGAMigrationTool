# Bastion development access handoff

Status: LOCAL TEMPLATES VERIFIED — live access not deployed or verified
Date: 2026-10-02
Authority: [accepted bounded ADR-0010](../../architecture/decisions/ADR-0010-private-pilot-browser-access.md) and [execution/test plan](../../plans/active/private-pilot-browser-access-preparation.md).

## Intended pilot access

The operator uses a Mac browser to open the Azure portal, selects the private Windows development VM and connects using Bastion Developer. Inside that desktop, the operator opens the private portal's canonical HTTPS URL and signs in to the application separately. Azure portal access, the protected local Windows login and the application's Entra login are three distinct authorities. No customer receives the VM password or an Azure infrastructure role.

One owner-operated session is for sequential synthetic development tests. It is not a shared customer workstation or remote desktop hosting service. Developer is the accepted candidate; no automatic upgrade to paid Bastion, VPN, public RDP or public app ingress is permitted on connection failure. [Microsoft SKU documentation](https://learn.microsoft.com/en-us/azure/bastion/bastion-sku-comparison) and [Developer connection instructions](https://learn.microsoft.com/en-us/azure/bastion/quickstart-host-portal) establish intended capability, not proof of this environment.

After the pilot is accepted as complete, the [deferred HTTPS portal plan](../../plans/active/post-pilot-https-portal.md) prepares customer access through a normal browser URL and organizational login. It requires its own reviewed public boundary and release decision.

## Repository templates

- [Optional access entry point](../../infra/bicep/pilot-development-private-browser-access.bicep): composes only the development desktop and private browser DNS against an existing foundation.
- [Workstation/Bastion module](../../infra/bicep/modules/pilot-development-private-browser-access.bicep): one private VM/NIC/NSG/new nondelegated subnet, existing NAT attachment, Bastion Developer. Required exact inputs have no defaults.
- [Browser DNS module](../../infra/bicep/modules/pilot-development-browser-dns.bicep): environment default-domain zone, nonregistering same-VNet link and wildcard/apex A records. The entry point derives domain/IP from actual environment metadata; no public zone or Container Apps private endpoint is added.
- [Incomplete parameter example](../../infra/bicep/environments/pilot-dev-private-browser.parameters.example.json): null bindings deliberately prevent use. Password is omitted for owner secure runtime handoff; never put it in JSON/Git/output.
- [Workstation policy/input tests](../../tests/infrastructure/PrivateBrowserAccess/README.md) and [DNS/binding checks](../../tests/infrastructure/PrivateBrowserDns/README.md).

The entry point does not recreate the disposed foundation, create an application, change bootstrap ingress, install a BFF, assign roles or configure product login. Existing bootstrap ingress `external:false` cannot serve the workstation. The future BFF needs reviewed VNet-scope ingress inside the internal environment and a proven exact immediate ingress peer/header contract. The diagnostic BFF image remains permanently disabled.

## Protected preflight, in order

1. Refresh exact resource group/subscription, VNet/subnet/NAT/internal-environment metadata, provider readiness and quota. Bind the environment's actual defaultDomain/staticIp and same-VNet infrastructure subnet. Use the protected intake; no account/address identifiers in Git or the board.
2. Review a fresh, distinct nonoverlapping workstation subnet and a static NIC address excluding Azure-reserved addresses. Verify the Developer RDP source with authoritative/provider evidence, not a guessed shared-service address. Allow only that source to the target NIC TCP3389.
3. Bind the reviewed app ILB TCP443 and exact necessary DNS/platform/identity/certificate/update hosts/ports. Keep default-deny lateral/Internet rules and exclude protected data, broker, registry and control-plane destinations. NAT supplies connectivity, not filtering. NSG platform-tag rules and explicit DNS settings are a candidate boundary: prove effective Windows licensing, DNS, platform traffic and guest/firewall behavior before a live session. Azure host traffic has special semantics; do not claim all platform traffic is filtered by ordinary NSG rules. Shared IP hosting is not FQDN filtering; review destinations accordingly. No custom resolver/firewall is provisioned by this packet.
4. Verify exact official Windows image version and minimum OS disk requirements, regional B2s availability/capacity and browser/patch readiness. `latest` is rejected. The E10-sized Standard SSD OS disk and Windows compute price are only candidate quantities until the actual image/provider accepts them.
5. Owner handles protected OS setup credentials outside tools/Git and creates a distinct routine nonadministrator RDP account before any app test; the template only provisions the required setup administrator. Verify that routine account has RDP rights and cannot administer the VM. No extension creates customer accounts. Synthetic data only; sign out and clear each test persona's browser state, respect clipboard/storage limitations and preserve sanitized test evidence before disposal.
6. Run both protected input validators before provider validate/what-if. Validators check captured consistency and deny unsafe inputs; review references are not proof of live behavior. Refresh snapshots immediately before execution. Do not deploy this addition into an absent foundation or treat compile success as provider evidence.
7. Prepare the complete composed foundation/session preview, current offer prices, delayed charges, duration/reserve, exact ownership/removal inventory and operator-enforced deallocation/disposal. Obtain the separately required priced-session and credential/access approval. Historical USD25/24hour authorization is not renewed.
8. Run PA-T01–PA-T08 on the exact deployed artifacts with independent review. Application sign-in remains disabled until production host/provider/key/audit and identity/network requirements pass. No G1/Milestone2 acceptance follows from a working desktop.

## Cleanup and recovery

Disconnect and clear persona state; deallocate the Azure VM through the control plane (Windows shutdown alone does not stop compute billing). Track continued OS disk charges. Preserve required sanitized/restricted evidence, then remove only session-owned VM/NIC/disk/NSG/workstation subnet/Bastion/browser zone/link/records after checking dependencies. Do not delete budgets, the entire resource group, existing foundation resources, shared data/keys or unrelated workloads. Cleanup requires an exact inventory and operator; this template does not enforce a timed shutdown or authorize disposal by itself.

## Completion limits

Current executed local evidence is recorded in the [access plan](../../plans/active/private-pilot-browser-access-preparation.md). No live VM, Bastion connection, Windows routine account, app HTTPS/TLS, effective firewall, role grant, provider what-if, priced session or gate verification is claimed. The private board publication remains blocked by automatic approval review of the official helper credential/network action; its last confirmed BFF snapshot is stale and repository records are authoritative.

## Current provider discovery — 2026-10-03 UTC

[The next Bastion preflight](bastion-deployment-session-preflight.md) records actual selected-subscription VM availability/quota restrictions, an exact image candidate and current public cost worksheet. It requires a VM/capacity decision before local template amendment or a quota/support request. No live deployment or paid-session approval follows; the existing accepted template remains unchanged.
