# Health-assessment pilot diagrams

These diagrams explain the accepted pilot architecture before implementation planning. They are descriptive views of the approved product specification, ADR-0001 through ADR-0004, identity/session design, authorization matrix, AI controls and operations plan. They do not claim that any resource has been provisioned or verified.

## Views

1. [`01-pilot-system-context.svg`](./01-pilot-system-context.svg) ([PNG preview](./previews/01-pilot-system-context.png)) — who uses the platform, how the One Identity-specific customer-side collectors deliver evidence outbound, and which external services participate.
2. [`02-azure-deployment-and-network.svg`](./02-azure-deployment-and-network.svg) ([PNG preview](./previews/02-azure-deployment-and-network.png)) — the accepted Azure services and their public/private network paths.
3. [`03-assessment-processing-flow.svg`](./03-assessment-processing-flow.svg) ([PNG preview](./previews/03-assessment-processing-flow.png)) — how an immutable baseline becomes reviewed and published assessment results.
4. [`04-identity-and-tenant-isolation.svg`](./04-identity-and-tenant-isolation.svg) ([PNG preview](./previews/04-identity-and-tenant-isolation.png)) — how Entra authentication, product authorization and customer data-plane isolation work together.

## Conventions

- Solid blue arrows represent application or data flow.
- Dashed purple arrows represent authentication, workload identity or policy decisions.
- Dashed red paths identify an explicit public or external trust boundary.
- Azure service icons are used only for the Azure product named next to the icon.
- Microsoft Entra icons are used only for Microsoft Entra ID.
- Neutral shapes represent the IGA Migration Tool, One Identity Manager, OpenAI and logical application modules. Microsoft icons do not represent non-Microsoft products.
- “Private” means an Azure private endpoint or private application path is required by the accepted design. Service Bus Standard is the documented exception: it uses a controlled public Azure endpoint with Entra-only authorization and opaque delivery messages.
- PostgreSQL is deliberately non-HA for the pilot. Automated backup, point-in-time recovery and the approved recovery drill remain required.
- The customer-side collector is specific to the One Identity Manager pilot and sits outside the Azure boundary. Future SaaS-to-SaaS product connectors are hosted acquisition components and require separate product-specific designs.

## Official Microsoft icon sources

The selected icon files in [`icons`](./icons/) are unmodified copies from Microsoft's official architecture-icon packages, downloaded on 2026-09-29 for use in architecture documentation:

- [Azure Architecture Icons](https://learn.microsoft.com/en-us/azure/architecture/icons/) — `Azure_Public_Service_Icons_V24.zip`, updated July 2026.
- [Microsoft Entra Architecture Icons](https://learn.microsoft.com/en-us/entra/architecture/architecture-icons/) — October 2023 package.

Microsoft permits these icons in architecture diagrams, training materials and documentation. The diagrams follow Microsoft's published guidance: icons are not cropped, flipped, rotated, distorted or used to represent another product. The original product name appears near every Microsoft icon.

| Repository icon | Official source file |
|---|---|
| `azure-container-apps.svg` | `02989-icon-service-Container-Apps-Environments.svg` |
| `azure-container-registry.svg` | `10105-icon-service-Container-Registries.svg` |
| `azure-key-vault.svg` | `10245-icon-service-Key-Vaults.svg` |
| `azure-managed-identity.svg` | `10227-icon-service-Entra-Managed-Identities.svg` |
| `azure-monitor.svg` | `00001-icon-service-Monitor.svg` |
| `azure-postgresql.svg` | `10131-icon-service-Azure-Database-PostgreSQL-Server.svg` |
| `azure-service-bus.svg` | `10836-icon-service-Azure-Service-Bus.svg` |
| `azure-storage.svg` | `10086-icon-service-Storage-Accounts.svg` |
| `azure-virtual-network.svg` | `10061-icon-service-Virtual-Networks.svg` |
| `microsoft-entra-id.svg` | `Microsoft Entra ID color icon.svg` |

## Review status

- Architecture content: accepted design; implementation not started.
- Icon source and usage: official Microsoft packages; repository copies are unmodified.
- Visual verification: all four SVGs were rendered at 2× resolution to aspect-ratio-preserving PNG previews and inspected on 2026-09-29.
