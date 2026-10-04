# Azure pilot development setup

Status: Approved partial spike provisioned and configuration verified; approved cleanup verified
Owner: Repository owner and platform engineer  
Last updated: 2026-10-01

This is the environment setup handoff for [Milestone 1 / G1](../../specs/003-health-assessment/implementation-plan.md). It follows accepted [ADR-0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md) and the [identity design](../security/health-assessment-identity-session-design.md). It does not change the approved platform, security boundaries or execution gates.

## Verified setup

The owner selected **Subscription 1** and a **USD 50 monthly Azure development limit** on 2026-10-01. The portal showed that subscription as Active and the signed-in owner as Owner. An empty resource group, `rg-iga-pilot-dev-eastus2`, was created and verified in East US 2. Its `iga-pilot-dev-monthly` budget was created and verified at USD 50, resetting monthly from 2026-10-01 and expiring on 2028-09-30. Actual-cost alerts are configured at 50%, 80% and 100% to the owner's supplied recipient. Subscription/tenant IDs and the recipient address remain in Azure/environment configuration, outside this document and the status site.

The initial setup created no application, network, broker, database, store, role assignment, app registration or credential. The later approved partial spike is recorded below. The portal showed zero resources and no deployments in the new group before budget creation. The continuation below records provider, network-quota and deployment-preview checks; Conditional Access, private DNS, workload federation and deployed service capacity remain unverified. G1–G9 remain `NOT VERIFIED`.

## Cost boundary

The USD 50 limit applies to pilot development resources. The existing subscription also contains unrelated resources; a resource-group budget does not include those or a later Container Apps managed resource group. Include every pilot-generated group in the cost estimate and monitoring before paid deployment.

Azure budgets alert on delayed cost data; they do not stop resources or enforce a hard spending cap. See [Microsoft's budget documentation](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets). The created budget has no automatic shutdown action.

Microsoft's public retail feed was queried on 2026-10-01 for East US 2, USD, Consumption prices. ACR Premium's `Premium Registry Unit` was USD 1.6666/day (USD 51.6646 over October's 31 days), before private endpoints, networking and the other services. ACR private endpoints require Premium. Service Bus Standard returned both an hourly base meter of USD 0.013441/hour and a monthly base meter of USD 10/month; confirm the applicable billing meter for this subscription rather than summing both. These are reference prices, not a subscription quote. The continuation resolved NAT through the official pricing page's East US 2 price data: USD 0.045/hour and USD 0.045/GB processed, with partial hours rounded up.

Sources: [Azure retail price API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices), [ACR pricing](https://azure.microsoft.com/en-us/pricing/details/container-registry/), [ACR private endpoints](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-private-endpoints), [Service Bus pricing](https://azure.microsoft.com/en-us/pricing/details/service-bus/), and [NAT Gateway pricing](https://azure.microsoft.com/en-us/pricing/details/azure-nat-gateway/).

Use synthetic fixtures and short, scheduled cloud test sessions until a complete estimate establishes an affordable persistent configuration. Before each paid session, record its exact services/SKUs, remaining monthly allowance, active hours, usage assumptions, maximum expected spend with contingency, cleanup operator/time, and residual charges. Teardown is a separately authorized operation: identify the exact disposable resources and verify that retained evidence is preserved before deletion. Scaling application replicas to zero does not remove broker, NAT, registry, private-endpoint or database storage charges. Any larger budget or architectural change needs its own owner decision.

## Setup sequence and current disposition

| Step | Requested role | Required input or work | Complete when |
|---|---|---|---|
| Subscription and spending | Repository owner | Select subscription, region and monthly amount; supply alert recipient | Group and both alert budgets are verified; the aggregate budget covers both explicit pilot/managed groups as one USD 50 allowance. Notification delivery and final delayed charges remain unverified. |
| Deployment session | Platform engineer with owner available | Use the existing portal session or authenticated Azure CLI; verify exact tenant/subscription and intended scope | Selected scope was verified; authorized session executed and 41 management checks were preserved. Exact cleanup is verified; no token or credential is printed or committed. |
| Provider and capacity preflight | Azure administrator / platform engineer | Verify required provider registration, East US 2 SKUs, stable API versions, subscription policy and quota | Required providers/region/APIs and the approved SKU provisioned in this session. Recheck quota, capacity and APIs before another deployment. |
| Address and DNS allocation | Network owner / platform engineer | Allocate private VNet and disjoint Container Apps/private-endpoint subnets; confirm future database subnet/DNS needs and conflicts | Approved isolated CIDRs and actual VNet/delegation/NAT/DNS bindings passed management checks. Source collector remains outbound HTTPS; no network peering or VPN was enabled. |
| Costed test window | Repository owner / platform engineer | Complete the estimate above, including NAT, IP, broker, registry, database, private connectivity and telemetry; identify cleanup scope | Owner approved the complete USD 25 / maximum 24-hour foundation window and exact disposal; creation started 2026-10-02 00:11:27Z. Reconcile delayed charges before another paid window. |
| First infrastructure test | Platform engineer | Compile/lint/policy-check and inspect a what-if for `pilot-broker-network-spike.bicep` with the allocated parameters | The prior spike deployed/verified/cleaned up. Foundation validation/preview and 41 management checks passed after scoped repairs. Live allowed/denied traffic remains unrun. |
| Complete platform modules | Platform engineer | Add Container Apps workload profiles, diagnostics, ACR, non-HA PostgreSQL, private Blob/Key Vault paths and DNS; connect separate workload identities | Private foundation modules compile/lint and have deployed management evidence. Actual approved cloud application images, tested endpoints/trust/grants and live private control tests remain required; G1 stays NOT VERIFIED. |
| Human sign-in | Identity administrator / platform engineer | Confirm Entra administration and Conditional Access licensing; implement one single-tenant BFF registration, explicit app roles and dedicated managed-identity federation | Inactive four-role registration and directory administration/tenant P2 prerequisites are verified. Actual licensed users, reviewed Conditional Access scope and tested BFF/federation inputs remain required. Run the complete approved session/trust/denial tests before activation. |

For provider preflight, inspect `Microsoft.App`, `Microsoft.Network`, `Microsoft.ManagedIdentity`, `Microsoft.ServiceBus`, `Microsoft.ContainerRegistry`, `Microsoft.DBforPostgreSQL`, `Microsoft.Storage`, `Microsoft.KeyVault`, `Microsoft.OperationalInsights`, `Microsoft.Insights`, and `Microsoft.Authorization`. Register only providers needed by the selected test. Owner access to Azure resources does not establish Entra directory permissions or Conditional Access licensing.

The first spike uses **Service Bus Standard**, local/SAS auth disabled, TLS 1.2, one allowed NAT address, distinct queue-scoped sender/receiver identities and opaque synthetic messages. [Microsoft documents Standard IP filtering](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-ip-filtering); its portal Networking tab is Premium-only, so deploy and inspect the Standard rules through Bicep/management APIs. Do not add a workstation IP or broad allow rule to make testing convenient. Successful traffic must originate from the intended Azure workload path; an outside client is a denial test.

## Preflight entry points

Azure CLI is not installed on the current developer machine as of this setup. The portal's [ephemeral Cloud Shell](https://learn.microsoft.com/en-us/azure/cloud-shell/get-started/ephemeral) provides a session without creating a persistent shell storage account; its temporary files are lost at session end. It is an administration path, not an allowed data-plane client for the private services or broker. Use an authorized repository source copy and the pinned toolchain; retain only sanitized execution evidence through the approved evidence-store workflow once available.

Read-only checks, after explicitly selecting the subscription by its protected ID:

```sh
az account show --query '{name:name,state:state}' --output json
az group show --name rg-iga-pilot-dev-eastus2 --query '{name:name,location:location,provisioningState:properties.provisioningState}' --output json
az resource list --resource-group rg-iga-pilot-dev-eastus2 --query '[].{name:name,type:type,location:location}' --output table
az provider show --namespace Microsoft.App --query '{state:registrationState,resources:resourceTypes[].{type:resourceType,locations:locations,apiVersions:apiVersions}}' --output json
```

Repeat the provider check for the intended services. Follow [the infrastructure README](../../infra/bicep/README.md) for pinned Bicep compile/lint and policy checks; supply allocated CIDRs to its network-policy check. A reviewed parameter file must contain only non-secret names/CIDRs/configuration. Keep credentials out of parameters and outputs. What-if requires permissions for the intended resources and deployment; deployments that grant roles also require role-assignment authority. See [Microsoft's Bicep deployment guidance](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-cli).

## Evidence and gate accounting

Initial setup is a portal verification, not a deployed platform control test. Record subsequent exact commit/template/parameter digests, tested API/SKU/runtime versions, cost window, sanitized results and reviewer decisions in the [evidence index](../../specs/003-health-assessment/evidence-index.md). Runtime identity/network/customer denial, observability and recovery evidence are still required by the approved test plan. Customer evidence, AI provider activation, PDF publication and operational acceptance retain their existing gates. Local synthetic pilot implementation continues under the [local build plan](../../plans/active/one-identity-local-pilot-build.md).

## First test proposal and executed preflight — 2026-10-01

State: **Owner approved the exact isolated addresses, USD 15 / 24-hour test, scoped grants and disposable-resource cleanup on 2026-10-01. Deployment and management configuration checks passed; approved cleanup returned an empty resource inventory. G1 remains NOT VERIFIED.**

The owner clarified that the source collector will send to Azure over outbound HTTPS. No Azure-to-source VPN, peering, inbound collector listener or SQL route is required or approved. The approved test Azure VNet was isolated. The selected subscription's VNet inventory returned empty; this does not verify address compatibility with any future connected network.

The authenticated ephemeral Cloud Shell selected the protected subscription ID and returned `Subscription 1`, `Enabled`. Registration checks found App, Network, ManagedIdentity, ContainerRegistry, Storage, KeyVault, OperationalInsights, Insights and Authorization registered. ServiceBus was unregistered; its registration was started in the portal and the subsequent CLI result returned `Registered`. DBforPostgreSQL remains unregistered and was not enabled for this partial test.

East US 2 network usage returned 0/1000 VNets, 0/20 public IPs and 0/100 NAT gateways. Provider metadata advertised the planned region and Network `2025-05-01`, ServiceBus namespace/queue `2026-01-01`, and ManagedIdentity `2024-11-30` APIs. Child-resource availability and policy were additionally exercised by Azure template validation. These checks do not reserve capacity or prove live workload networking.

The [approved parameter file](../../infra/bicep/environments/pilot-dev-spike.parameters.json) selects VNet `10.64.0.0/16`, Container Apps subnet `10.64.0.0/23`, and private-endpoint subnet `10.64.2.0/24`. The owner approved these addresses for this isolated synthetic environment on 2026-10-01; a future network connection still requires its own reviewed address plan. The current template creates no private endpoint or DNS zone; later protected-service modules must add their approved DNS paths.

### Exact proposed scope

Within the existing `rg-iga-pilot-dev-eastus2` only:

- `vnet-iga-pilot-dev`, with `snet-container-apps` and `snet-private-endpoints`.
- Standard static IPv4 `pip-iga-pilot-dev-egress` and Standard `nat-iga-pilot-dev`.
- Standard namespace `sb-iga-pilot-dev-1001-pv`, its deny-by-default NAT-IP rule set and `synthetic-work` queue.
- Distinct `id-iga-pilot-dev-sb-sender` and `id-iga-pilot-dev-sb-receiver` identities, with only their respective queue-scoped Data Sender / Data Receiver assignments.

There is no workload attachment, app registration, customer evidence, database, ACR, Key Vault, private endpoint, telemetry workspace or source connection in this scope. A successful deployment proves resource provisioning/configuration only; live allowed/denied workload tests and full G1 remain later work.

### 24-hour cost envelope and cleanup

The active window begins only after deployment is authorized. The platform operator executing the test must preserve sanitized configuration/results outside disposable resources, inspect actual costs and delete only the above test resources and their child grants within **24 hours of creation**, sooner when checks finish. Keep the existing resource group and budget. Do not use whole-group deletion. Stop before cleanup if ownership or evidence preservation is uncertain; notify the owner promptly because continuing resource hours still incur charges. This document does not itself authorize deletion.

| Item | Reference rate / conservative allowance | 24-hour estimate or reserve |
|---|---|---:|
| Standard NAT resource hours | USD 0.045/hour, 24 billed hours | USD 1.08 |
| Standard static IPv4 | USD 0.005/hour, 24 hours | USD 0.12 |
| NAT processed traffic | At most 1 GB synthetic allowance, USD 0.045/GB | USD 0.045 |
| Service Bus Standard base | Reserve the entire USD 10 monthly reference amount pending subscription meter reconciliation; do not also add hourly base | USD 10.00 |
| Service Bus operations | Reserve USD 0.80 for up to 1 million operations, without relying on included allowance | USD 0.80 |
| Bandwidth, price variation and contingency | Remaining reserve; no workload/load test in this scope | USD 2.955 |
| **Requested test envelope** | Within the USD 50 monthly limit, subject to a fresh cost check | **USD 15.00** |

The network plus hourly broker reference would be approximately USD 1.52 before traffic; the USD 15 proposal uses the conservative monthly broker reserve. This is an estimate, not a hard cap or contract quote. No new session starts unless the remaining monthly pilot allowance covers the full envelope. Future sessions and added services require a new estimate. The initial Azure budget showed USD 0 evaluated spend, with delayed cost reporting; refresh it before creation. NAT billing begins at creation even without workloads.

### Executed checks and preview limitations

- Bicep `0.47.16` macOS ARM64 vendor SHA-256 verified, then build and lint passed for the spike, network, namespace, queue and role modules. SDK `10.0.401` was used for policy runners.
- Network policy passed, rejected nine unsafe drifts, passed fourteen synthetic address cases, and accepted the exact proposed three CIDRs.
- Service Bus policy passed and rejected eleven namespace, seven role, five queue and thirteen spike-wiring drifts.
- Compiled spike SHA-256: `8bdffd48ffc62c893233a944b6b773cb362228819d236a9e2a64a466383a8265`. Uploaded Cloud Shell file returned the same digest.
- Parameter-file SHA-256: `383962763a66b861182aa96d76c3fbae6b28d07ed7b86468e9eded34251c8492`.
- Azure group what-if returned `Succeeded`, no error, ten `Create` changes and two `Unsupported` role-assignment previews. It showed no modification or deletion of existing resources. The unsupported grants depend on newly created managed-identity principal IDs; their exact queue scopes and role definitions were reviewed in the compiled source and local policy, but propagation and effective access remain live tests.
- Azure group template validation returned `Succeeded`, no error, for the same proposed names/CIDRs. It creates no paid resources and is not evidence of deployed authorization, networking, observability or recovery.

Chrome's automated upload required an extension file-access setting. The normal native file picker successfully uploaded only the compiled non-secret template; the setting was not changed. Cloud Shell files are ephemeral. No credentials, subscription/tenant IDs or user contact data are in the parameter file, this handoff or the private board.

## Approved deployment execution — 2026-10-01

The owner approved the concrete proposal and requested continued setup of development prerequisites. A fresh scope check returned the selected subscription enabled, the intended group empty, the uploaded compiled digest unchanged, and the monthly USD 50 budget with USD 0 reported spend. Reporting is delayed and this does not guarantee final charges.

The exact reviewed spike deployment returned `Succeeded` and no error. Management reads verified the allocated VNet/subnets and Container Apps delegation/NAT attachment; Standard static IPv4 and its NAT association; Service Bus Standard, TLS 1.2, local/SAS disabled, default network Deny, no trusted-service bypass, and exactly the NAT IP allowed; active work queue, duplicate detection and expiration dead-lettering. Distinct identities have exactly two queue-scoped assignments, Data Sender and Data Receiver. The initial role read incorrectly combined `--scope` and `--all`; correcting it to scope-only returned the verified grants. These are configuration checks, not effective data-plane authorization proof.

The [sanitized execution record](evidence/azure-pilot-spike-20261001.json) preserves template/parameter digests and actual results outside the disposable resources. It is developer evidence, not a signed gate bundle or restricted engineering-store artifact. No workload, customer evidence, message load or source connection was created.

PostgreSQL's provider was enabled and returned `Registered`; its metadata lists East US 2. This administrative prerequisite creates no database. The full platform still lacks deployable Container Apps, database, ACR/private connectivity, Key Vault, diagnostics and complete evidence-store integration. Entra registration, exact redirect/logout inputs, user-license assignment, pilot Conditional Access configuration and live BFF federation proof remain open; directory administration and the tenant Premium P2 plan were verified below. Additional paid tests need complete modules, reviewed parameters, an estimate within the remaining monthly allowance and an exact evidence-preserving cleanup scope. Persistent ACR Premium alone exceeds this development limit.

### Cleanup and identity prerequisites

After preserving the configuration results, inventory showed exactly the six approved top-level test resources. The operator deleted the broker namespace/children, VNet/subnets, NAT, static IP and the two managed identities by exact name. The subsequent resource inventory returned `[]`, within the approved 24-hour window. No whole-group deletion occurred. Deployment history was retained. Final invoiced spend and email delivery remain unverified.

The Entra admin center showed the signed-in account as Global Administrator and the tenant plan as Entra ID Premium P2. The Conditional Access overview is accessible. These administrative/licensing checks do not prove assigned-user licensing, pilot-specific MFA/authentication strength, policy scope, exclusions or emergency-account behavior. The pilot has no deployed BFF registration or approved actual web redirect/logout endpoints yet. Implement and validate those inputs and the dedicated federation path before enabling sign-in.

### Remaining service preflight

Authenticated management metadata returned registered providers, East US 2 availability and the approved stable API for Container Apps managed environments (`2026-01-01`), ACR (`2025-11-01`), PostgreSQL Flexible Server (`2025-08-01`), Key Vault (`2026-02-01`) and Storage (`2025-06-01`). Metadata is not capacity reservation or a deployed service-control test. PostgreSQL capabilities advertise Burstable including `Standard_B1ms` and 32 GiB managed disk; major 18 is listed among supported versions and the regional status/restriction/reason fields were null. No SKU or database was provisioned. These observations are candidate inputs for the next priced test, not a selected production configuration.

After cleanup the retained group returned `Succeeded` in East US 2, and the retained budget returned USD 50 with USD 0 reported spend. Reconcile delayed billed charges before another paid test.

## Historical foundation preparation before Mac unlock — 2026-10-01

Three isolated infrastructure packets completed and received independent review. Private registry/vault/DNS, PostgreSQL 18, Container Apps environment and private monitoring modules now compile/lint with pinned Bicep. Review found and corrected an ACA API pin and configuration-policy omissions. The composed root includes the previously tested broker/network and empty engineering storage; no application images or live data paths are present. See [the approved USD 25 / 24-hour session](azure-foundation-session-proposal.md) and [cycle record](../../plans/active/azure-development-foundation-cycle.md).

The owner approved the exact new session, synthetic retention choices, disposable database administrator and two queue-scoped grants on 2026-10-01. Azure create is not yet executed: uploading the reviewed template through the normal native file picker is blocked because the Mac is locked. The owner was asked to unlock it. No paid foundation resource has been created in this continuation; full Azure validation, preview and deployment remain pending.

The inactive single-tenant BFF application was created and management readback confirmed four approved User roles, no service principal, federation, credential, redirect or API consent. The registration does not enable sign-in. The aggregate subscription budget now covers both the pilot group and explicitly named Container Apps managed group with one combined USD 50 monthly boundary and preserved 50%/80%/100% alert recipients. Fresh management readback passed; reported spend was zero, with delayed charges and alert delivery unverified. The original group budget is retained; the two budgets do not create two spending allowances.

Development still needs deployable BFF/application/worker images and tested endpoints, actual licensed pilot users and reviewed Conditional Access scope, private image build/pull and managed-identity workload grants, evidence intake/purge and all required live denial, monitoring, retention and restore tests. Infrastructure preparation is complete for this proposed session; those product/security/runtime dependencies cannot be invented through portal configuration. G1 remains NOT VERIFIED.

## Full foundation execution — 2026-10-01 EDT

After the Mac was unlocked, original provider validation and what-if succeeded. The approved synthetic session provisioned the private foundation. Initial root deployment failed on vault purge-protection serialization and database administrator ordering; scoped repair and exact administrator retry succeeded. Both reusable templates and negative policies were corrected and independently reviewed. All 41 management configuration/access/DNS-group checks passed and were [saved outside the disposable resources](evidence/azure-pilot-foundation-20261001.json). See the [cycle execution detail](../../plans/active/azure-development-foundation-cycle.md#approved-foundation-execution--2026-10-01-edt--2026-10-02-utc). The repaired root has compiled but has not been replayed as a full Azure deployment.

Exact approved cleanup is verified. The existing group, budgets, inactive application and history are retained. No customer data, application collection or live workload was enabled. Reconcile delayed spend before another session. Azure configurable prerequisites in this approved foundation have management evidence; remaining work requires approved cloud application images, real tested BFF endpoints/trust, actual licensed pilot users/Conditional Access scope and the live identity/network/observability/restore/evidence-store tests. G1 remains NOT VERIFIED.

Final disposal confirmed at 2026-10-02 01:16:45Z: primary inventory empty, provider-managed group removed, queue grants zero, vault recoverable and not purged. No active paid resource from this session remains. The existing group, both budgets, inactive application and deployment history are retained. This session is complete within the approved maximum duration. Final billed charges remain delayed/unverified. Subsequent paid deployment requires a fresh allowance/capacity check and new suffix while the vault name is reserved in soft delete.

## Application package preparation — 2026-10-01

[The next package](azure-application-package.md) now has an explicit inert web/worker bootstrap,immutable Microsoft base-image lock,private hosting templates and local process/drift checks. It is not the authenticated product BFF or real durable worker;those reviewed contracts still require implementation. No paid redeployment or sign-in/data-grant activation occurred. The preparation adds a container-capable partialCI check;actual registry publication,pulls and supply-chain/G1 evidence must be recorded before promotion.
