# Azure pilot development setup

Status: Initial Azure setup verified; paid platform spike pending  
Owner: Repository owner and platform engineer  
Last updated: 2026-10-01

This is the environment setup handoff for [Milestone 1 / G1](../../specs/003-health-assessment/implementation-plan.md). It follows accepted [ADR-0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md) and the [identity design](../security/health-assessment-identity-session-design.md). It does not change the approved platform, security boundaries or execution gates.

## Verified setup

The owner selected **Subscription 1** and a **USD 50 monthly Azure development limit** on 2026-10-01. The portal showed that subscription as Active and the signed-in owner as Owner. An empty resource group, `rg-iga-pilot-dev-eastus2`, was created and verified in East US 2. Its `iga-pilot-dev-monthly` budget was created and verified at USD 50, resetting monthly from 2026-10-01 and expiring on 2028-09-30. Actual-cost alerts are configured at 50%, 80% and 100% to the owner's supplied recipient. Subscription/tenant IDs and the recipient address remain in Azure/environment configuration, outside this document and the status site.

No application, network, broker, database, store, role assignment, app registration or credential was created in this setup. The portal showed zero resources and no deployments in the new group before budget creation. No resource provider, quota, Conditional Access, private DNS, workload federation or service capacity check has passed yet. G1–G9 remain `NOT VERIFIED`.

## Cost boundary

The USD 50 limit applies to pilot development resources. The existing subscription also contains unrelated resources; a resource-group budget does not include those or a later Container Apps managed resource group. Include every pilot-generated group in the cost estimate and monitoring before paid deployment.

Azure budgets alert on delayed cost data; they do not stop resources or enforce a hard spending cap. See [Microsoft's budget documentation](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets). The created budget has no automatic shutdown action.

Microsoft's public retail feed was queried on 2026-10-01 for East US 2, USD, Consumption prices. ACR Premium's `Premium Registry Unit` was USD 1.6666/day (USD 51.6646 over October's 31 days), before private endpoints, networking and the other services. ACR private endpoints require Premium. Service Bus Standard returned both an hourly base meter of USD 0.013441/hour and a monthly base meter of USD 10/month; confirm the applicable billing meter for this subscription rather than summing both. These are reference prices, not a subscription quote or a complete estimate. NAT Gateway pricing was not resolved in this session and stays unverified.

Sources: [Azure retail price API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices), [ACR pricing](https://azure.microsoft.com/en-us/pricing/details/container-registry/), [ACR private endpoints](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-private-endpoints), [Service Bus pricing](https://azure.microsoft.com/en-us/pricing/details/service-bus/), and [NAT Gateway pricing](https://azure.microsoft.com/en-us/pricing/details/azure-nat-gateway/).

Use synthetic fixtures and short, scheduled cloud test sessions until a complete estimate establishes an affordable persistent configuration. Before each paid session, record its exact services/SKUs, remaining monthly allowance, active hours, usage assumptions, maximum expected spend with contingency, cleanup operator/time, and residual charges. Teardown is a separately authorized operation: identify the exact disposable resources and verify that retained evidence is preserved before deletion. Scaling application replicas to zero does not remove broker, NAT, registry, private-endpoint or database storage charges. Any larger budget or architectural change needs its own owner decision.

## Next setup sequence

| Step | Requested role | Required input or work | Complete when |
|---|---|---|---|
| Subscription and spending | Repository owner | Select subscription, region and monthly amount; supply alert recipient | Initial group and budget are verified above. Notification delivery and total coverage of generated groups remain unverified. |
| Deployment session | Platform engineer with owner available | Use the existing portal session or authenticated Azure CLI; verify exact tenant/subscription and intended scope | Read-only account/group checks identify the selected environment; no token or credential is printed or committed. |
| Provider and capacity preflight | Azure administrator / platform engineer | Verify required provider registration, East US 2 SKUs, stable API versions, subscription policy and quota | Required providers and intended SKUs are available; any unsupported API or capacity is recorded and resolved before deployment. |
| Address and DNS allocation | Network owner / platform engineer | Allocate private VNet and disjoint Container Apps/private-endpoint subnets; confirm future database subnet/DNS needs and conflicts | Actual CIDRs pass repository address checks and network-owner review. Synthetic examples alone do not allocate an environment. |
| Costed test window | Repository owner / platform engineer | Complete the estimate above, including NAT, IP, broker, registry, database, private connectivity and telemetry; identify cleanup scope | A bounded session fits the remaining USD 50 allowance and its cleanup is reviewable. |
| First infrastructure test | Platform engineer | Compile/lint/policy-check and inspect a what-if for `pilot-broker-network-spike.bicep` with the allocated parameters | Exact change list is reviewed; deploy only this synthetic non-production scope, then record deployed configuration and denial tests. |
| Complete platform modules | Platform engineer | Add Container Apps workload profiles, diagnostics, ACR, non-HA PostgreSQL, private Blob/Key Vault paths and DNS; connect separate workload identities | Approved modules and tests exist and each deployed control has executed evidence. The current partial spike cannot close G1. |
| Human sign-in | Identity administrator / platform engineer | Confirm Entra administration and Conditional Access licensing; implement one single-tenant BFF registration, explicit app roles and dedicated managed-identity federation | The approved authorization-code/session, renewal, wrong-trust, revocation and leakage tests pass before sign-in is enabled. |

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
