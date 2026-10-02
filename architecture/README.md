# Architecture documentation

This directory describes how the approved product is constructed: system boundaries, data, interfaces, security, observability, infrastructure, deployment, and other cross-cutting concerns.

The health-assessment pilot architecture and Azure technology platform are selected in [`decisions`](./decisions/). Consequential changes require a proposed ADR and human approval; they must not emerge silently during feature implementation.

## Architecture diagrams

- [`diagrams/01-pilot-system-context.svg`](./diagrams/01-pilot-system-context.svg) — users, source environments, platform and external-provider context.
- [`diagrams/02-azure-deployment-and-network.svg`](./diagrams/02-azure-deployment-and-network.svg) — Azure services, network paths, data stores and deployment boundaries.
- [`diagrams/03-assessment-processing-flow.svg`](./diagrams/03-assessment-processing-flow.svg) — durable assessment, AI, review, scoring and publication flow.
- [`diagrams/04-identity-and-tenant-isolation.svg`](./diagrams/04-identity-and-tenant-isolation.svg) — human identity, workload identity and Pilot A/Pilot B data separation.
- [`diagrams/README.md`](./diagrams/README.md) — diagram conventions, scope and official icon sources.

- [`diagrams/05-azure-development-foundation.md`](./diagrams/05-azure-development-foundation.md) — detailed executed development foundation, editable official-icon diagram and deployment-template mapping.
