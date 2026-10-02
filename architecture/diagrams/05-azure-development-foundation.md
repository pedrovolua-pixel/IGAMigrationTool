# Detailed Azure development foundation

[Editable, self-contained SVG](05-azure-development-foundation.svg) · [High-resolution PNG](previews/05-azure-development-foundation.png)

## Scope and evidence

This view documents the approved East US 2 synthetic development foundation, its configured network and identity boundaries, and the separately identified future application layer. It adds deployment detail to the accepted design views 01–04 without changing ADR-0004 or authorizing additional services.

The session produced 41 passing **management configuration** checks after scoped Key Vault and PostgreSQL repairs. Disposable resources were subsequently removed; the resource group, USD 50 monthly budget/alerts, inactive BFF application registration and deployment history remain. The vault remains recoverable under seven-day soft delete. The diagram is an executed configuration record, not a current inventory of running services. G1 remains NOT VERIFIED.

Canonical execution and cleanup: [foundation cycle](../../plans/active/azure-development-foundation-cycle.md). Machine-readable provenance and results: [sanitized execution evidence](../../docs/development/evidence/azure-pilot-foundation-20261001.json). Stored source and configuration mapping: [template inventory](../../infra/bicep/deployment-template-inventory.md).

## Connectivity and trust boundaries

- Source collector: inside the source network, near One Identity Manager SQL Server, using dedicated read-only access. The planned Azure intake uses outbound HTTPS; offline encrypted delivery is separately approved. There is no VPN/peering or Azure access to the SQL credential. Collector delivery does not go directly to the Azure database or engineering Blob container.
- Application subnet: `10.64.0.0/23`, delegated to `Microsoft.App/environments`, inside `10.64.0.0/16`. Internal Consumption Container Apps environment; public networking disabled. No application ingress, images, apps or jobs were deployed. Public browser/collector ingress still needs implementation and testing under the accepted design.
- Endpoint subnet: `10.64.2.0/24`. Five service private endpoints and their NICs are here; the managed PaaS services themselves are outside the subnet. The green connector bus expresses private endpoint associations, not service-to-service traffic.
- Service Bus Standard: explicit public-endpoint exception. Default-deny network rules allow the single approved NAT IPv4. Entra-only authorization, no SAS/local authentication and TLS 1.2 minimum. Two distinct managed identities hold sender/receiver grants on the synthetic queue only. They were not attached to running workloads.
- Future workload access requires separate identities per privilege boundary, tested data scopes and effective denial checks. Human app roles do not replace product customer/project/action authorization. The inactive single-tenant BFF registration has no redirects, credentials, service principal, federation or API consent.

## Private DNS mapping

Each zone has one VNet link with automatic registration disabled. These are configuration checks; live resolution was not exercised.

| Private endpoint | Target / subresource | Private DNS zone(s) |
|---|---|---|
| Registry | Azure Container Registry / `registry` | `privatelink.azurecr.io` |
| Vault | Azure Key Vault / `vault` | `privatelink.vaultcore.azure.net` |
| Database | PostgreSQL Flexible Server / `postgresqlServer` | `privatelink.postgres.database.azure.com` |
| Engineering storage | Azure Blob Storage / `blob` | `privatelink.blob.core.windows.net` |
| Monitoring | Azure Monitor Private Link Scope / `azuremonitor` | `privatelink.monitor.azure.com`; `privatelink.oms.opinsights.azure.com`; `privatelink.ods.opinsights.azure.com`; `privatelink.agentsvc.azure-automation.net`; `privatelink.blob.core.windows.net` |

The Blob zone is shared by the engineering Blob endpoint and the monitoring endpoint DNS group; there are eight distinct zones overall.

## Service controls and exclusions

- PostgreSQL 18: Standard_B1ms, 32 GiB, Entra-only, sole approved development owner administrator, seven-day backups, TLS required/minimum 1.2. No high availability, geo-redundant backup, storage autogrow, app migrations or customer databases. Customer database/role isolation and restore drills are future work.
- Key Vault: private RBAC vault, no access policies, seven-day soft delete, purge protection disabled for this disposable session. The template omits the optional purge-protection property when false; it does not disable protection on an existing protected vault.
- ACR Premium: private, admin/anonymous access disabled, network bypass None, no images. Image build/pull authorization has not been tested.
- Engineering storage: empty `gate-evidence` container; OAuth/HTTPS, no shared-key or public Blob access, versioning and seven-day recovery windows. Custom evidence roles were not deployed. This scaffold is not the customer artifact store or a signed evidence intake.
- Monitoring: private ingestion/query and disabled local authentication; no SDK collection or diagnostic exports. Workspace retention is 30 days and daily ingestion cap 1 GB. Application Insights table retention must be checked before collection; the cap and budget are not hard spending stops.
- Platform-managed Container Apps resource group: created/removed through the environment provider; never directly managed as application-owned infrastructure.

## Iconography and reproduction

Icons are unmodified Microsoft SVG assets from the [official Azure package](https://learn.microsoft.com/en-us/azure/architecture/icons/) and [official Entra package](https://learn.microsoft.com/en-us/entra/architecture/architecture-icons/), retained under [icons](icons/). Product names appear beside the icons; neutral shapes represent logical components. This is an architecture diagram using official Microsoft assets, not a Microsoft endorsement of this solution.

The new SVG embeds the exact repository icon bytes as data URLs, so it renders without external files or network access. It is editable vector source with an accessible title/description. Render the PNG from SVG with an SVG-capable renderer at 1.5× (2850 × 2475); the preview was rendered using Sharp and visually inspected. No customer payloads, credentials, tenant/subscription/principal identifiers, actual egress IP or budget-recipient email are included.

## Verification for this documentation change

Executed on 2026-10-01:

- Rendered 2850 × 2475 PNG and visually inspected the final layout.
- Parsed SVG XML; all ten embedded icon instances exactly match original repository SVG bytes.
- Checked relative links in both new documents and the three updated README files.
- Confirmed all 15 recursively composed Bicep files are already Git-tracked and recoverable at both listed source revisions.
- Built and linted the current foundation with Bicep 0.47.16; no errors or warnings. Its compiled SHA-256 and parameter-file SHA-256 match the existing execution evidence.
- Ran foundation composition policy: baseline passed and all nine unsafe mutations rejected.
- Secret scans of `architecture/diagrams` and `infra`: no leaks found. `git diff --check`: passed.

Application tests and cloud deployment were not rerun for this documentation-only change. No canonical gate, phase, human dependency or feature status changed; the private status board did not require publication in this cycle.
