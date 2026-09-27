# Feature Status: Multi-source data ingestion

State: PRODUCT SPEC

Owner: Product owner

Last updated: 2026-09-27

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for acquisition methods, authentication lifecycle, snapshots and deltas, schedules, uploads, retries, rate limits, checkpoints, logs, retention, normalization, raw evidence, object depth, customer-side collection, and One Identity Manager source coverage.
- Draft product specification created with stable `FR-ING-N` and `AC-ING-N` identifiers.
- SailPoint and One Identity Manager established as health-assessment sources, one logical source per project.
- Migration sequencing recorded with SailPoint to Veza first.

## In progress

- Product-owner review of the draft product specification.
- Reconciliation with approved product-wide source assumptions and security requirements.

## Blocked

- Blocker: Discovery allows warned overprivileged credentials to proceed, while approved `NFR-SEC-1` requires minimum privileges.
- Owner/decision needed: Product owner and security owner must decide whether excess privileges block collection or revise the approved least-privilege requirement through normal review.

## Decisions made

- One logical source system per project, with multiple acquisition endpoints allowed: `product-spec.md` FR-ING-1 and FR-ING-18.
- SailPoint and One Identity Manager health-assessment support: `product-spec.md` FR-ING-2.
- Read-only behavior for every source: `product-spec.md` FR-ING-3.
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
| Product specification approved | NOT VERIFIED | Approval section is blank and status remains Draft |
| Least-privilege consistency | FAIL | `product-spec.md` open questions; conflict with `product/non-functional-requirements.md` NFR-SEC-1 |
| Technical design | NOT VERIFIED | Not started; product approval required first |
| Implementation and tests | NOT VERIFIED | Not authorized or started |

## Remaining work and verification

- Obtain product-owner review and approval or requested revisions.
- Resolve the least-privilege conflict with security ownership.
- Update approved product-wide documents through normal change review after the specification is accepted.
- Enumerate supported source versions, modules, patches, endpoints, authentication mechanisms, and exact field contracts during technical design.
- Conduct separate destination-mapping discovery for SailPoint to One Identity Manager, One Identity Manager to Veza, and later One Identity Manager to SailPoint.
- Create technical specification, implementation plan, and test plan only after product approval.

## Next transition

- Target state: PRODUCT APPROVED
- Entry conditions: Product owner approves scope; security conflict is resolved; product-wide documents are reconciled.
- Required human approval: Product owner, plus security-owner concurrence for the privilege decision.
