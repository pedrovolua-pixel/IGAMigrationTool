# Development deployment template inventory

All Bicep source templates used for the two approved synthetic Azure sessions are stored in this repository. The current foundation includes the scoped deployment repairs; original source remains recoverable from Git. This inventory does not make the environment an active deployment or provide a full replay of the corrected root.

## Entry points and inputs

| Repository file | Purpose / executed scope |
|---|---|
| [`pilot-broker-network-spike.bicep`](pilot-broker-network-spike.bicep) | First bounded network/broker deployment; also nested by the foundation. |
| [`environments/pilot-dev-spike.parameters.json`](environments/pilot-dev-spike.parameters.json) | Non-secret approved network, broker and identity naming inputs for the first session. |
| [`pilot-development-foundation.bicep`](pilot-development-foundation.bicep) | Composed network/broker, private data services, DNS, monitoring and empty compute environment. |
| [`environments/pilot-dev-foundation.parameters.json`](environments/pilot-dev-foundation.parameters.json) | Non-protected subset of executed naming/retention/cap inputs; intentionally incomplete alone. |
| [`../entra/pilot-bff.application.json`](../entra/pilot-bff.application.json) | Inactive Entra application manifest; separate from the ARM deployment. |

The foundation requires the approved tenant and database administrator ID/name as ephemeral Azure inputs; those protected identifiers are not committed. The administrator type, retention windows, purge-protection choice and ingestion cap are stored. A rerun must account for the soft-deleted vault name reservation and approved spending/session scope. Never purge or silently change names/policies to bypass it.

The retained resource group and budget/alert configuration were configured separately in the Azure portal; there is no committed deployment template for them. Alert recipients and protected subscription settings are deliberately excluded. The repo therefore contains the templates **used** to build the disposable foundation, but does not claim a single-command recreation of every portal/bootstrap setting.

## Composed module inventory

All paths below are relative to `infra/bicep/modules/`.

| Source file | Resources / controls |
|---|---|
| `pilot-network-egress.bicep` | VNet, delegated app subnet, endpoint subnet, static IPv4, NAT binding. |
| `pilot-service-bus-namespace.bicep` | Standard namespace, Entra-only auth, TLS, default-deny / existing NAT IP rule. |
| `pilot-service-bus-work-queue.bicep` | Synthetic queue, duplicate detection, expiry dead lettering. |
| `pilot-workload-identity.bicep` | Instantiated twice for distinct sender/receiver identities. |
| `pilot-service-bus-work-roles.bicep` | Two queue-scoped built-in data role assignments. |
| `pilot-private-dns.bicep` | Instantiated for eight private zones and their VNet links. |
| `pilot-container-registry.bicep` | Private Premium registry, private endpoint / DNS group. |
| `pilot-key-vault.bicep` | Private RBAC vault, approved recovery policy, private endpoint / DNS group. |
| `pilot-postgresql.bicep` | Private Entra-only Flexible Server, approved owner admin, TLS, backup, endpoint / DNS group. |
| `engineering-evidence-store.bicep` | Empty engineering Blob scaffold, versioning/recovery, endpoint / DNS group. |
| `pilot-container-apps-environment.bicep` | Internal Consumption environment and provider-owned infrastructure group. |
| `pilot-observability.bicep` | Private Log Analytics and Application Insights; collection not activated. |
| `pilot-monitor-private-link.bicep` | AMPLS, two scoped resources, monitoring endpoint / five-zone DNS group. |

`engineering-evidence-roles.bicep` is also stored, but was **not** included in the foundation deployment. Application workloads, images, BFF federation, customer database/container scopes and monitoring exports are not deployed by these entry points.

## Exact executed source and repairs

- Initial composed source revision: `59df7590a4cb7139d668f94624350a3df59d7848`. The initial root failed; do not label it a successful full deployment.
- Repaired source revision: `db7decb0ec4b64709e53f94a5c7e713fdca98392`. Key Vault now omits the optional purge flag for the approved false input. PostgreSQL orders TLS configuration and private DNS attachment before administrator creation.
- Key Vault was repaired with its scoped module; the exact approved PostgreSQL administrator operation was retried after the server became Ready. Both operations and their limits are in the execution evidence.
- The corrected full root compiled and passed policy checks but has **not** been replayed as a full Azure deployment.

For exact compiled-template SHA-256 fingerprints, input-subset fingerprint, repair operations, 41 checks and cleanup readback, use the [sanitized foundation record](../../docs/development/evidence/azure-pilot-foundation-20261001.json). First-session evidence is in the [broker spike record](../../docs/development/evidence/azure-pilot-spike-20261001.json). Historic source can be recovered with `git show <revision>:<repository-path>`; generated ARM JSON can be regenerated using pinned Bicep 0.47.16. Compiled temporary artifacts and ephemeral protected parameters are not repository source files.

## Verification and related architecture

Build/lint instructions and limits: [Bicep README](README.md). The [bootstrap workflow](../../.github/workflows/bootstrap-checks.yml) runs the infrastructure policy suites, including the five foundation Python checks. Compiled-template checks do not establish live networking, authorization, recovery, workload behavior or G1 acceptance.

[Detailed architecture diagram and technical notes](../../architecture/diagrams/05-azure-development-foundation.md) maps this source to the tested topology. This documentation adds no deployment, access grant or new policy.

## Subsequent application-package templates (not deployed)

[`pilot-development-bootstrap-apps.bicep`](pilot-development-bootstrap-apps.bicep),its[private hosting module](modules/pilot-development-bootstrap-apps.bicep) and [intentionally unresolved example](environments/pilot-dev-bootstrap.parameters.example.json) prepare inert diagnostic web/worker hosting. They were not used in the earlier foundation session and add no access grants. See[package handoff](../../docs/development/azure-application-package.md) for concrete image/private-pull/product dependencies.

## BFF federation input template (not provisioned)

[`../entra/pilot-bff.federated-credential.template.json`](../entra/pilot-bff.federated-credential.template.json) records the approved exact tenant issuer, dedicated BFF managed-identity principal subject and token-exchange audience. Its placeholders must be replaced in protected ephemeral inputs after deployment review. No federated credential, redirect endpoint or consent was created by the local [BFF foundation cycle](../../plans/active/bff-identity-session-cycle.md).

## Disabled BFF container recipe and inventory tools (not deployed)

The reviewed [`../containers/bff-development.Dockerfile`](../containers/bff-development.Dockerfile), [allowlisted context](../containers/bff-development.Dockerfile.dockerignore), [immutable image lock](../containers/bff-development.images.lock.json) and [build helper](../containers/build-bff-development.py) compose a permanently disabled diagnostic BFF executable. The [pinned collector](../containers/collect-image-evidence.py) and [tool lock](../containers/image-evidence-tools.lock.json) collect developer image inventories/SPDX; collection does not approve licenses/findings, establish signed build provenance or promote an image. No BFF container, image pull grant or registry artifact was deployed to Azure. [The bounded execution record](../../docs/development/evidence/bff-deployment-preparation-20261002.json) distinguishes actual CI collection from pending live controls.


## Optional Bastion development access sources (not deployed)

[Access entry point](pilot-development-private-browser-access.bicep), [desktop/Bastion module](modules/pilot-development-private-browser-access.bicep), [environment browser DNS module](modules/pilot-development-browser-dns.bicep) and [deliberately incomplete inputs](environments/pilot-dev-private-browser.parameters.example.json) implement the bounded local [ADR-0010](../../architecture/decisions/ADR-0010-private-pilot-browser-access.md) packet. They reference an existing approved VNet/NAT/internal environment and add only owner-operated synthetic development access. They were not used for either historical Azure session. No public application, role grant, production BFF, password, exact protected network binding or resource deployment is supplied. [Handoff and live prerequisites](../../docs/development/bastion-development-access.md) distinguish local source verification from future provider/live proof. The [post-pilot portal plan](../../plans/active/post-pilot-https-portal.md) is deferred and creates no resources.

## HTTPS replacement direction — 2026-10-03 UTC

The owner now selects HTTPS instead of Bastion. [Accepted local ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [the active plan](../../plans/active/https-pilot-portal-preparation.md) replace the desktop deployment path. Existing optional Bastion templates remain stored, locally verified and undeployed. The current internal environment module is not convertible to public ingress. The approved local HTTPS-P01 sources are now stored separately. The required false-only root guard prevents deployment; public network admission is hard disabled in its prospective module. No application is included. All environment-building templates remain repository sources with protected bindings outside Git.


## HTTPS-P01 stored environment scaffold (not deployed)

| Source | Purpose |
|---|---|
| [Guarded composition](pilot-https-portal.bicep) | Required false-only `deployEnvironment`; no app or outputs. |
| [External environment module](modules/pilot-https-container-apps-environment.bicep) | East US 2, external Consumption profile, public network access disabled, existing exact dedicated subnet and Standard NAT guards, no log export. |
| [Incomplete protected-input example](environments/pilot-dev-https-portal.parameters.example.json) | Six null values; intentionally rejected by preflight. |
| [Compiled and input checks](../../tests/infrastructure/HttpsPortal/README.md) | Negative template mutations and value-suppressed protected provider/input CLI; no Azure calls. |

These files were not used in either historical Azure session. No app, identity, secret, grant or resource is deployed. The historical foundation is disposed; existing reviewed network prerequisites are not restored by this scaffold. [Current execution plan](../../plans/active/https-pilot-portal-preparation.md) and [production dependency handoff](../../docs/development/https-pilot-portal-next-packet.md) retain the real BFF, provider/key/audit, ingress proof and separately priced/public session prerequisites. A direct module invocation is not authorized by local verification.
