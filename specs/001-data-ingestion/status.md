# Feature Status: Multi-source data ingestion

State: IMPLEMENTING

Owner: Product owner

Last updated: 2026-10-01

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for acquisition methods, authentication lifecycle, snapshots and deltas, schedules, uploads, retries, rate limits, checkpoints, logs, retention, normalization, raw evidence, object depth, customer-side collection, and One Identity Manager source coverage.
- Draft product specification created with stable `FR-ING-N` and `AC-ING-N` identifiers.
- SailPoint and One Identity Manager established as health-assessment sources, one logical source per project.
- Migration sequencing recorded with SailPoint to Veza first.
- Product specification and the excess-read-only pilot exception approved by the repository owner acting as product and security owner on 2026-09-28.
- Repository owner approved the pilot-slice technical specification, implementation plan and test plan for local development on 2026-09-29. Exact query-pack, customer source and delivery gates remain separate.

## In progress

- A local `CollectorSafety` module classifies typed permission-probe outcomes, checks page replay versus conflict, compares trusted exact-build/module claims against query metadata, bounds the next page by row/time limits, rejects unsafe T-SQL structures with a pinned parser, skips in-process overlapping scope runs and encrypts a minimized payload using the approved primitive. It has no SQL probe, credential, query execution, encrypted checkpoint store, package envelope, enrollment or upload path; it cannot authorize collection or produce an importable package. G2 remains open.
- `CollectorHost` now has fixed Windows Service/CLI verbs, versioned strict JSON configuration, protected-path ACL checks, a daylight-saving-aware daily schedule, a local exclusive-file run-lease primitive, an authenticated encrypted append-only checkpoint ledger, a Windows DPAPI protected-key reader and payload-free blocked-state codes. The service never opens SQL and the one-shot command never writes a package until the separately gated contracts are implemented. Key provisioning/rotation and run integration remain open; Windows installation and customer-host ACL behavior are unverified.
- `CollectorSafety` now has a pure explicit-allowlist field minimizer that omits values for excluded, redacted, prohibited and unknown fields. It does not verify a customer signature or exact field dictionary and cannot authorize staging alone.
- The approved pilot-slice technical specification, implementation plan and test plan for the One Identity Manager 10.x Windows collector and immutable baseline boundary trace the approved feature-001 requirements, accepted ADRs, IMP-DEC-003 and the health-pilot evidence contract. Exact query packs, environment evidence, enrollment/upload operations, offline envelope serialization and MSI provenance remain open; technical/security/operations review of those contracts, SME and database-owner review are pending.
- The pilot-local service/CLI shell contract is approved under the repository owner's standing preapproval. The host implementation remains a blocked shell pending query-pack, source, offline-envelope and installer evidence.
- One-shot CLI and scheduled service now enter a shared local coordinator. A synthetic adapter exercises trusted-material matching, exact build/module applicability, permission blocking, lease, bounded pages, minimization, stage-before-checkpoint ordering and encrypted resume. The shipped adapter always returns `SOURCE_CONTRACT_PENDING`; it has no SQL reader or package sink. The [SME handoff](../../docs/operations/one-identity-sme-pilot-handoff.md) lists the exact source and database-owner evidence needed for the next milestone.
- The local coordinator now applies the configured run deadline to page staging as well as page reading. A synthetic adapter that waits in staging must stop at the deadline without advancing the encrypted checkpoint; durable sink cancellation and recovery still need a reviewed adapter and environment evidence.
- Adapter-originated cancellation without a run or deadline cancellation now returns a typed source/staging failure instead of escaping the coordinator. Three synthetic negative cases leave the checkpoint unchanged; no source or delivery adapter has been enabled.
- The coordinator's synthetic staging adapter now demonstrates the stage-before-checkpoint recovery window: a failed ledger write leaves the page incomplete, changed restaging conflicts, and a new coordinator invocation restages the same digest idempotently. A separate child process demonstrates local lease reacquisition after forced termination. [Partial bootstrap run 36868281712](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36868281712) passed the Windows 2022/2025 restart smoke checks. These tests do not establish a durable production sink or customer-host crash recovery.
- The repository owner approved the direction in the [delivery and installer proposal](collector-delivery-and-installer-contract-proposal.md) for pilot-local work. Exact online/offline schemas, signer trust, MSI toolchain and operations decisions remain draft; no delivery adapter, package or installer is enabled.
- An isolated encrypted page-stage primitive now binds minimized values, row count and digest to the exact checkpoint context and boundary. It uses opaque keyed filenames, an atomic encrypted write, idempotent same-content replay, conflict detection and a per-page size bound. Synthetic restart, tamper, prohibited-field and excluded-value cases pass locally. It is not connected to the shipped adapter; protected-directory ACLs, key lifecycle, aggregate queue/retention and customer-host recovery remain open.
- The [SME evidence template](one-identity-sme-evidence-template.md) now gives the reviewer separate environment records, public-documentation candidates to confirm or correct, exact source/query/field/fixture slots, a database-owner section and signoff fields. Both environment rows remain `NOT VERIFIED` until protected evidence is supplied and reviewed.
- A bounded Environment A live metadata read on 2026-10-01 matched the supplied SQL Server build **16.0.1121.4**, KB **5040936**, database compatibility **160**, collation **`SQL_Latin1_General_CP1_CI_AS`**, and all **14 active module/version/migration-version rows**. The [partial SME response](one-identity-environment-a-sme-response.md) records provisional owner-only artifacts. The July report cover's `CCC`/`10.0.0.287` and live main database's `STE`/`10.0` edition fields conflict as exact product-build evidence, although `CCC`/`QBM`/`DPR` modules report `10.0.0.287`. Exact product and schema build, hotfixes, report/source binding, unlisted-category classifications and approved query pack remain `NOT VERIFIED`.
- The Environment A database owner authorized bounded metadata discovery and creation of a dedicated SQL login. The new account has selected metadata-column reads, no broad reader role, and no blocking capability detected by the local diagnostic probe; a prohibited connection-string column was denied. The one-off lab read relaxed certificate trust. Formal permission, TLS, query-plan, impact and reviewer evidence remain open; no collector source adapter or G2 approval exists.
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
| Implementation/test plans | APPROVED FOR LOCAL PILOT IMPLEMENTATION | Repository owner approved `implementation-plan.md` and `test-plan.md` on 2026-09-29 and preapproved pilot-local shell decisions; external C0 contracts remain open |
| Implementation and tests | LOCAL FOUNDATION / G2 NOT VERIFIED | On 2026-10-01, pinned .NET `10.0.401` locked restore, format verification, Release build, all local unit/architecture suites and self-contained `win-x64` publish passed. `CollectorHost` passed 29 command/configuration/schedule/lease, 11 encrypted ledger, 14 isolated encrypted page-stage, 26 shared coordinator and 2 cross-process lease recovery cases. Gitleaks and Bicep were unavailable locally and no infrastructure files changed. [Partial bootstrap run 36870871323](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36870871323) passed on implementation commit `840f3d3`, including the pinned secret scan, Linux checks, Bicep policies, and Windows 2022/2025 DPAPI/ACL, publish and service restart/blocked-CLI smoke checks. The earlier `40691f5` run failed on a missing coordinator import, corrected in `840f3d3`. No source interaction, customer-installed Windows service, signed MSI, importable package, delivery or G2 execution evidence exists. |

## Remaining work and verification

- Enumerate supported source versions, modules, patches, endpoints, authentication mechanisms, and exact field contracts during technical design.
- Conduct separate destination-mapping discovery for SailPoint to One Identity Manager, One Identity Manager to Veza, and later One Identity Manager to SailPoint.
- Review and complete the draft enrollment/upload, offline envelope and MSI contracts. Obtain One Identity SME and customer database-owner approval for exact query packs and source execution plans. C0 and G2 remain open beyond the local-plan approval.

## Next transition

- Target state: CODE REVIEW for the bounded pilot slice.
- Entry conditions: Finish an implementation slice and its applicable checks without treating local synthetic results as G2 evidence.
- Required human approval: Exact source, delivery and release decisions retain their separate gates.
