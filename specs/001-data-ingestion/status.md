# Feature Status: Multi-source data ingestion

State: PRODUCT APPROVED

Owner: Product owner

Last updated: 2026-09-28

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for acquisition methods, authentication lifecycle, snapshots and deltas, schedules, uploads, retries, rate limits, checkpoints, logs, retention, normalization, raw evidence, object depth, customer-side collection, and One Identity Manager source coverage.
- Draft product specification created with stable `FR-ING-N` and `AC-ING-N` identifiers.
- SailPoint and One Identity Manager established as health-assessment sources, one logical source per project.
- Migration sequencing recorded with SailPoint to Veza first.
- Product specification and the excess-read-only pilot exception approved by the repository owner acting as product and security owner on 2026-09-28.

## In progress

- Technical specification and capability-matrix design.

## Blocked

- None recorded. The repository owner acting as product and security owner approved the narrowly scoped excess-read-only pilot exception; broader product and technical approval gates remain outstanding.

## Decisions made

- One logical source system per project, with multiple acquisition endpoints allowed: `product-spec.md` FR-ING-1 and FR-ING-18.
- SailPoint and One Identity Manager health-assessment support: `product-spec.md` FR-ING-2.
- Read-only behavior for every source: `product-spec.md` FR-ING-3.
- Excess read-only permission warns and audits without blocking the pilot; write, DDL, ownership, or administrative capability blocks collection: `product-spec.md` FR-ING-7 and AC-ING-16.
- Hosted, collector, report/export, database where applicable, and upload acquisition: `product-spec.md` FR-ING-4 and FR-ING-27.
- Immutable incremental baselines and seven-day reconciliation: `product-spec.md` FR-ING-8 and FR-ING-9.
- One Identity 8.x through 10.x native and customization coverage: `product-spec.md` FR-ING-28 through FR-ING-34.
- Migration sequence and later-specification boundary: `product-spec.md` FR-ING-36.

## Discoveries

- One Identity Designer reports and directly collected metadata complement each other and must preserve both default and actual values for modified defaults.
- Multiple evidence paths may disagree; value-level provenance and visible conflicts are required.
- General identity/account data is unnecessary; minimal matching and relationship references are sufficient for the approved ingestion scope.
- Temporary hosted normalization may retain raw evidence for up to 30 days even when permanent raw retention is disabled.

## Evidence

| Requirement / gate | Result | Evidence |
|---|---|---|
| Product-owner discovery input captured | PASS | `product-spec.md` |
| Product specification approved | PASS | Product owner approval recorded in `product-spec.md` on 2026-09-28 |
| Least-privilege consistency | PASS WITH APPROVED EXCEPTION | NFR-SEC-1, FR-ING-7, and AC-ING-16 allow warned excess read-only scope for the pilot and block write, DDL, ownership, or administrative capability |
| Technical design | NOT VERIFIED | Not started; product approval required first |
| Implementation and tests | NOT VERIFIED | Not authorized or started |

## Remaining work and verification

- Enumerate supported source versions, modules, patches, endpoints, authentication mechanisms, and exact field contracts during technical design.
- Conduct separate destination-mapping discovery for SailPoint to One Identity Manager, One Identity Manager to Veza, and later One Identity Manager to SailPoint.
- Create technical specification, implementation plan, and test plan only after product approval.

## Next transition

- Target state: TECHNICAL DESIGN
- Entry conditions: Approved product specification and reconciled product-wide requirements.
- Required human approval: Technical owner approval after technical specification, architecture, security, and test design are complete.
