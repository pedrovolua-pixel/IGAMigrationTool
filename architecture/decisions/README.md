# Architecture Decision Records

Use an ADR for a consequential decision that constrains future engineering choices. Copy [`ADR-template.md`](./ADR-template.md), assign the next four-digit sequence, and use `ADR-NNNN-short-title.md`.

ADRs are append-only decision history. Supersede an accepted ADR with a new ADR rather than erasing the original context. Only an authorized human reviewer accepts consequential architectural decisions.

## Accepted decisions

- [`ADR-0001-pilot-application-shape.md`](./ADR-0001-pilot-application-shape.md) — modular pilot application with independently scalable asynchronous workers.
- [`ADR-0002-pilot-tenant-isolation.md`](./ADR-0002-pilot-tenant-isolation.md) — shared control plane with isolated customer data planes.
- [`ADR-0003-immutable-evidence-and-publication-storage.md`](./ADR-0003-immutable-evidence-and-publication-storage.md) — versioned relational metadata plus customer-scoped immutable blobs.
- [`ADR-0004-azure-pilot-technology-platform.md`](./ADR-0004-azure-pilot-technology-platform.md) — Azure managed pilot platform using ASP.NET Core, React, Container Apps, non-HA PostgreSQL, Service Bus Standard, Blob Storage, Key Vault, and managed identities.
- [`ADR-0005-gate-evidence-signing.md`](./ADR-0005-gate-evidence-signing.md) — RSA-PSS/SHA-256 signing profile for metadata-only gate-check bundles; production key and reviewer trust remain gated.

## Pilot choices pending milestone review

- [`ADR-0006-engineering-evidence-store-access.md`](./ADR-0006-engineering-evidence-store-access.md) — separate create, read and purge data identities implemented as a pilot draft; Milestone 0 review pending.
- [`ADR-0007-reviewer-decision-evidence.md`](./ADR-0007-reviewer-decision-evidence.md) — signed, metadata-only reviewer decisions and trusted role coverage implemented as a pilot draft; Milestone 0 review pending.
- [`ADR-0008-pilot-owner-override-evidence.md`](./ADR-0008-pilot-owner-override-evidence.md) — signed, exact-scope pilot-owner waiver evidence implemented as a local draft; Milestone 0 review pending.

## Accepted local BFF design; production composition pending

- [`ADR-0009-production-bff-authority-and-audit.md`](./ADR-0009-production-bff-authority-and-audit.md) — versioned product/provider authority and atomic payload-free session/authority audit; accepted for local P01–P05 synthetic implementation on2026-10-02. No live access or deployment authorization.
