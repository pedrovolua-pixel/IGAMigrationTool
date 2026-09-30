# Feature Status: Multi-source data ingestion

State: IMPLEMENTING

Owner: Product owner

Last updated: 2026-09-29

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for acquisition methods, authentication lifecycle, snapshots and deltas, schedules, uploads, retries, rate limits, checkpoints, logs, retention, normalization, raw evidence, object depth, customer-side collection, and One Identity Manager source coverage.
- Draft product specification created with stable `FR-ING-N` and `AC-ING-N` identifiers.
- SailPoint and One Identity Manager established as health-assessment sources, one logical source per project.
- Migration sequencing recorded with SailPoint to Veza first.
- Product specification and the excess-read-only pilot exception approved by the repository owner acting as product and security owner on 2026-09-28.
- Repository owner approved the pilot-slice technical specification, implementation plan and test plan for local development on 2026-09-29. Exact query-pack, customer source and delivery gates remain separate.

## In progress

- A local `CollectorSafety` module classifies typed permission-probe outcomes, checks page replay versus conflict, compares trusted exact-build/module claims against query metadata, bounds the next page by row/time limits, rejects unsafe T-SQL structures with a pinned parser, skips in-process overlapping scope runs and encrypts a minimized payload using the approved primitive. It has no SQL probe, credential, Windows Service/CLI, query execution, encrypted checkpoint store, package envelope, enrollment or upload path; it cannot authorize collection or produce an importable package. C0's exact-contract decisions and G2 remain open.
- The approved pilot-slice technical specification, implementation plan and test plan for the One Identity Manager 10.x Windows collector and immutable baseline boundary trace the approved feature-001 requirements, accepted ADRs, IMP-DEC-003 and the health-pilot evidence contract. Exact query packs, environment evidence, enrollment/upload operations, offline envelope serialization and MSI provenance remain open; technical/security/operations review of those contracts, SME and database-owner review are pending.
- A concrete but unapproved local service/CLI contract proposal now covers command syntax, protected configuration, schedule and status behavior. It is a review artifact, not a running Windows service.
- The broader SailPoint, hosted connector, generic upload and migration ingestion technical design remains open.

## Blocked

- None recorded for local C1 work. The unresolved exact source and delivery contracts block their corresponding integration paths.

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
| Technical design | APPROVED FOR LOCAL PILOT IMPLEMENTATION | Repository owner approved the pilot collector slice in `technical-spec.md` on 2026-09-29; exact source/delivery contracts and full feature design remain open |
| Implementation/test plans | APPROVED FOR LOCAL PILOT IMPLEMENTATION | Repository owner approved `implementation-plan.md` and `test-plan.md` on 2026-09-29; C0 exact-contract decisions remain open |
| Implementation and tests | LOCAL FOUNDATION / G2 NOT VERIFIED | `CollectorSafety` permission, checkpoint, query-applicability, page-budget, static T-SQL shape, run-gate and offline-encryption checks have 17, 12, 14, 12, 28, 8 and 10 synthetic cases locally. The prior [partial remote bootstrap run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36658938566) passed on `129d08e`; the new cases await remote verification. No source interaction, service, importable package, delivery or G2 execution evidence exists. |

## Remaining work and verification

- Enumerate supported source versions, modules, patches, endpoints, authentication mechanisms, and exact field contracts during technical design.
- Conduct separate destination-mapping discovery for SailPoint to One Identity Manager, One Identity Manager to Veza, and later One Identity Manager to SailPoint.
- Review the proposed local service/CLI contract, then resolve enrollment/upload, offline envelope and MSI contracts. Obtain One Identity SME and customer database-owner approval for exact query packs and source execution plans. C0 and G2 remain open beyond the local-plan approval.

## Next transition

- Target state: CODE REVIEW for the bounded pilot slice.
- Entry conditions: Finish an implementation slice and its applicable checks without treating local synthetic results as G2 evidence.
- Required human approval: Exact source, delivery and release decisions retain their separate gates.
