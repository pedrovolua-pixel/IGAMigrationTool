# Azure infrastructure bootstrap

Environment setup and the owner-selected USD 50 monthly development boundary are recorded in [Azure pilot development setup](../../docs/development/azure-pilot-development-setup.md). The dedicated East US 2 resource group and budget exist. The owner approved the isolated CIDRs and bounded partial spike; Azure provisioning and management configuration checks passed. Live workload controls and G1 remain unverified.

`environments/pilot-dev-spike.parameters.json` is the non-secret owner-approved isolated-development parameter set for the USD 15 / 24-hour test and exact cleanup scope in [the first-test proposal](../../docs/development/azure-pilot-development-setup.md#first-test-proposal-and-executed-preflight--2026-10-01). Its CIDRs passed the network policy runner. The compiled spike passed local policy, Azure what-if and Azure template validation, then the approved deployment and management configuration checks. The two role grants depend on new identity principal IDs and appeared as `Unsupported` in what-if; effective permissions still need deployed tests. This file is not approval to delete resources or evidence that G1 has passed.

`modules/engineering-evidence-store.bicep` is a partial Milestone 0 module for the separate engineering evidence store. It has no deployment entry point and has not been deployed. It fixes East US 2 and stable Storage and Network API versions. It disables public network access, blob public access and shared-key access; requires OAuth and HTTPS; and enables blob versioning with seven-day soft-delete windows. It takes non-secret subnet and Blob private DNS zone resource IDs, then defines the Blob private endpoint and DNS zone group. The compiled template outputs only resource IDs.

The module is not a deployable store yet. The supplied subnet and private DNS zone must exist in the approved network and have correct virtual-network DNS linkage. Managed-identity data roles, diagnostics, signed decision-bundle intake, restricted reviewer access, lifecycle purge, and restore tests are still missing. No time-based WORM policy is enabled: signed digests and versioning are the proposed tamper-evidence path, subject to end-to-end retrieval and purge tests. Blob versions and backups still need a deletion-safe lifecycle job before this store can carry real evidence. This draft is not G1 evidence or authorization to deploy.

`modules/engineering-evidence-roles.bicep` is a separate pilot draft containing three custom Blob data roles and assignments scoped to the existing `gate-evidence` container. The intake role can create new blobs; verification can read; purge can read and delete current and previous versions. Role definitions are available within the deployment resource group, while their assignments target only this container. It takes three non-secret managed-identity principal IDs. The repository owner directed pilot implementation to continue with review at each milestone, so this draft implements Option B in `architecture/decisions/ADR-0006-engineering-evidence-store-access.md` pending the Milestone 0 review. It has not been deployed or exercised against Azure. An integration spike must verify exact allowed/denied operations, propagation, revocation, version deletion and restore before using the store.

From the repository root, with Bicep CLI `0.47.16` and the pinned .NET SDK:

```sh
bicep build infra/bicep/modules/engineering-evidence-store.bicep --outfile /tmp/engineering-evidence-store.json
bicep lint infra/bicep/modules/engineering-evidence-store.bicep
bicep build infra/bicep/modules/engineering-evidence-roles.bicep --outfile /tmp/engineering-evidence-roles.json
bicep lint infra/bicep/modules/engineering-evidence-roles.bicep
dotnet run --project tests/infrastructure/EvidenceStorePolicy/EvidenceStorePolicy.csproj --configuration Release -- /tmp/engineering-evidence-store.json /tmp/engineering-evidence-roles.json
```

The policy test checks both compiled ARM templates and rejects eight store configuration drifts and seven role/scope drifts. It does not establish deployed network, identity, retention or recovery behavior.

## Pilot Service Bus namespace draft

`modules/pilot-service-bus-namespace.bicep` is a partial Milestone 1 module. It defines an East US 2 Standard namespace with local/SAS authentication disabled and TLS 1.2 minimum. Its network rule set denies traffic outside one IP rule. The rule resolves the address from an **existing public IP resource in the same resource group**, instead of accepting an arbitrary CIDR string. The supplied public IP name is non-secret; this module does not create or associate the address with a NAT gateway or Container Apps environment.

`modules/pilot-service-bus-work-roles.bicep` is a separate partial draft. It references one existing work queue and assigns the Azure Service Bus Data Sender and Data Receiver built-in roles to distinct managed-identity principal parameters, scoped to that queue. It does not create the queue or identities. The actual principal values must be checked for valid, distinct managed identities before deployment, then authorized and unauthorized send/receive attempts must be exercised after role propagation. Microsoft documents [the roles and queue scope](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-managed-service-identity) and [their built-in IDs](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/integration).

`modules/pilot-service-bus-work-queue.bicep` creates one work queue under an existing namespace, enables duplicate detection and dead-lettering on message expiration, and outputs only its resource ID. The application still needs an opaque message contract, idempotent processing, outbox/reconciliation, poison-work replay, expiry policy and operating thresholds. The [stable queue resource reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces/queues) describes those properties. The [duplicate-detection guidance](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection) does not replace application-level idempotency.

Before deployment, verify the referenced address is a provisioned static IPv4 address owned by the approved pilot egress path. After deployment, compare the namespace's effective IP rule with that address, prove app traffic uses it, and test denial from another network. Also verify Entra role grants and denials, local/SAS denial, diagnostics without message bodies, queue behavior and load. The spike now includes separate role-grant identities, but no workload attachment or live denial proof; passing template policy does not establish G1.

```sh
bicep build infra/bicep/modules/pilot-service-bus-namespace.bicep --outfile /tmp/pilot-service-bus-namespace.json
bicep lint infra/bicep/modules/pilot-service-bus-namespace.bicep
bicep build infra/bicep/modules/pilot-service-bus-work-roles.bicep --outfile /tmp/pilot-service-bus-work-roles.json
bicep lint infra/bicep/modules/pilot-service-bus-work-roles.bicep
bicep build infra/bicep/modules/pilot-service-bus-work-queue.bicep --outfile /tmp/pilot-service-bus-work-queue.json
bicep lint infra/bicep/modules/pilot-service-bus-work-queue.bicep
bicep build infra/bicep/pilot-broker-network-spike.bicep --outfile /tmp/pilot-broker-network-spike.json
bicep lint infra/bicep/pilot-broker-network-spike.bicep
dotnet run --project tests/infrastructure/ServiceBusPolicy/ServiceBusPolicy.csproj --configuration Release -- /tmp/pilot-service-bus-namespace.json /tmp/pilot-service-bus-work-roles.json /tmp/pilot-service-bus-work-queue.json /tmp/pilot-broker-network-spike.json
```

The compiled-template check rejects eleven namespace, seven role, five queue and thirteen spike-wiring drifts. Microsoft documents [Standard-tier IP filtering](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-ip-filtering), the [namespace resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces), the [network rule set](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces/networkrulesets), and [existing Bicep resource references](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/existing-resource).

## Pilot network and static egress draft

`modules/pilot-network-egress.bicep` creates a private VNet, a dedicated Container Apps workload-profile subnet, a separate private-endpoint subnet, one Standard static IPv4 public IP and one Standard NAT Gateway. The Container Apps subnet is delegated to `Microsoft.App/environments` and linked to that NAT Gateway. The Service Bus namespace module references the same public IP name. `pilot-broker-network-spike.bicep` composes network, broker namespace, work queue, two separately named managed identities and queue-scoped sender/receiver role assignments in dependency order for a partial non-production G1 spike. It does not create or attach a Container Apps workload, diagnostic or protected data service. Deployment and live authorization-denial tests remain required before use.

The non-secret VNet and subnet CIDRs must be allocated for the actual pilot environment. The policy runner can validate their canonical private IPv4 ranges, containment, disjointness, exclusion of Container Apps reserved `172.30.0.0/16` and `172.31.0.0/16` ranges, and the app subnet minimum `/27` size when passed as three additional arguments. CI runs fourteen synthetic address-plan checks, but cannot validate an actual plan until environment values are supplied. Before deployment, also verify subscription capacity and name consistency across modules. After deployment, prove all app egress uses the NAT address and that the Service Bus rule accepts only it. Compiled-template policy rejects nine infrastructure drifts. Azure documents [Container Apps NAT support and subnet requirements](https://learn.microsoft.com/en-us/azure/container-apps/networking), [NAT integration and reserved ranges](https://learn.microsoft.com/en-us/azure/container-apps/custom-virtual-networks), and the [NAT Gateway resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/2025-05-01/natgateways).

```sh
bicep build infra/bicep/modules/pilot-network-egress.bicep --outfile /tmp/pilot-network-egress.json
bicep lint infra/bicep/modules/pilot-network-egress.bicep
dotnet run --project tests/infrastructure/NetworkEgressPolicy/NetworkEgressPolicy.csproj --configuration Release -- /tmp/pilot-network-egress.json
```

For an allocated environment, append its VNet CIDR, Container Apps subnet CIDR and private-endpoint subnet CIDR to that policy command. Do not treat synthetic CI cases as validation of a future parameter file.

## Reusable workload identity draft

`modules/pilot-workload-identity.bicep` creates one East US 2 user-assigned managed identity and outputs its resource, principal and client IDs. It grants no role by itself. The G1 spike instantiates distinct sender and receiver identities from one non-secret name prefix and passes their principal IDs to the queue-scoped role module. A later application entry point must attach each identity only to its intended workload; the isolated renderer must remain without a platform managed identity. This module compiles and lints; actual separation, federated BFF credential behavior and role denial remain deployment tests. The resource uses the [stable 2024-11-30 API](https://learn.microsoft.com/en-us/azure/templates/microsoft.managedidentity/2024-11-30/userassignedidentities).

References: [Storage account 2025-06-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/2025-06-01/storageaccounts), [Blob service 2025-06-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/2025-06-01/storageaccounts/blobservices), [Blob container 2025-06-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/2025-06-01/storageaccounts/blobservices/containers), [private endpoint 2025-05-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/2025-05-01/privateendpoints), [private DNS zone group 2025-05-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/2025-05-01/privateendpoints/privatednszonegroups), [role definitions 2022-04-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.authorization/2022-04-01/roledefinitions), [role assignments 2022-04-01 resource](https://learn.microsoft.com/en-us/azure/templates/microsoft.authorization/2022-04-01/roleassignments).

## Private development foundation continuation

`pilot-development-foundation.bicep` composes the previously tested network/broker with private ACR Premium, closed RBAC Key Vault, Entra-only PostgreSQL 18, an empty engineering Blob container, eight private DNS links, an internal Consumption Container Apps environment and private monitoring. No workloads, images, sign-in, telemetry collection or customer evidence are enabled. Only the two disclosed synthetic queue grants and explicitly approved database administrator are included. Protected tenant/admin values belong in ephemeral Azure parameters. The root outputs no identifiers or credentials.

The [next-session proposal](../../docs/development/azure-foundation-session-proposal.md) records owner-approved USD 25 / maximum 24 hours, seven-day synthetic vault/DB recovery windows, thirty-day workspace retention with collection disabled, exact access and cleanup. Its approval does not close G1. Application Insights table retention requires separate verification before any collection. Delete the Container Apps environment through its provider; never directly modify its managed infrastructure group.

Build and lint the seven foundation modules plus the entry point with pinned Bicep 0.47.16. Run the five Python standard-library policy scripts in `tests/infrastructure/AzureFoundation/` with the corresponding compiled templates, as in `.github/workflows/bootstrap-checks.yml`. They reject private/public/authentication/API/output/connection drift, administrator/tenant/retention binding drift, missing DNS ordering and activation/consent in the inactive Entra manifest. These are developer checks; runtime and gate evidence remain required.

`infra/entra/pilot-bff.application.json` creates an inactive single-tenant application with the four approved coarse User roles. No redirect URI, implicit/public client, API consent, credentials, service principal or federation is configured. A hosted and tested BFF must supply actual endpoints and dedicated managed-identity trust before activation.

### Executed synthetic session and deployment repairs

The approved 2026-10-01 session passed provider validation/preview and 41 management checks after two scoped repairs; initial root failure and the non-replayed corrected root are recorded in [the execution cycle](../../plans/active/azure-development-foundation-cycle.md). `environments/pilot-dev-foundation.parameters.json` preserves only approved non-protected inputs; it is incomplete until ephemeral tenant/administrator parameters are supplied. Do not reuse the vault name while its seven-day soft-delete reservation remains.

A false required purge-protection input emits null, omitting the optional property: Azure rejects explicit false. This leaves protection disabled for a new vault; it cannot turn protection off on an existing protected vault. A true input still enables irreversible protection and requires separate authorization. PostgreSQL configures secure transport before minimum TLS, then creates its administrator only after TLS and private DNS dependencies. These changes preserve the approved policy and grant scope. Template checks cannot prove deployment timing under all provider conditions. Live management results, cleanup and runtime limitations are in [the sanitized record](../../docs/development/evidence/azure-pilot-foundation-20261001.json).

## Stored deployment sources

[Deployment template inventory](deployment-template-inventory.md) maps every composed source file to the two executed synthetic sessions, records original/repaired Git revisions and distinguishes repository parameters from protected ephemeral Azure inputs and portal bootstrap settings. The [detailed architecture](../../architecture/diagrams/05-azure-development-foundation.md) shows the tested configuration and future application paths.


## Optional private browser development access

The [Bastion handoff](../../docs/development/bastion-development-access.md) describes the accepted local template packet and protected preflight. The [separate access entry point](pilot-development-private-browser-access.bicep) composes a private Windows development desktop/Bastion Developer and default-domain browser DNS against an existing foundation; it does not recreate the disposed environment or change app ingress. [Inputs](environments/pilot-dev-private-browser.parameters.example.json) deliberately contain nulls and omit the owner-supplied secure password. Pinned build/lint, negative policies and provider-binding commands are in the [workstation tests](../../tests/infrastructure/PrivateBrowserAccess/README.md) and [DNS tests](../../tests/infrastructure/PrivateBrowserDns/README.md). Compilation/input consistency do not authorize a paid session or establish effective NSG/platform/TLS/login behavior.
