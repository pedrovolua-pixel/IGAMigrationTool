# Test Plan: One Identity pilot ingestion slice

Status: Approved for local pilot implementation — executable source fixtures pending  
Product spec: `specs/001-data-ingestion/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/001-data-ingestion/technical-spec.md` (Approved for local pilot implementation)  
Owner: Quality owner  
Last updated: 2026-09-30

Only the local synthetic cases recorded in the feature status are claimed executed. CI fixtures must be synthetic and payload-free. Exact SQL, modules, field dictionaries, scale data and production-impact plans are approved separately for each eligible build/environment.

## Acceptance-criteria mapping

| Criterion | Test ID and level | Pilot expectation |
|---|---|---|
| AC-ING-1 | TP-ING-001 contract | One logical One Identity source; cross-source endpoint rejected. |
| AC-ING-2 | TP-ING-002 static/integration | No write path, read-only intent and blocking permission categories. |
| AC-ING-3 | TP-ING-003 contract/UI | Exact-build capability, policy, gap and estimate preview before run. |
| AC-ING-4 | TP-ING-004 unit/integration | Timeout/disconnect resumes from compatible checkpoint without duplicate page. |
| AC-ING-5 | TP-ING-005 integration | New immutable baseline, tombstones and untouched timestamps. |
| AC-ING-6 | TP-ING-006 integration/UI | Partial baseline has category gaps and prominent warning. |
| AC-ING-7 | TP-ING-007 later upload slice | Generic upload/archive safety is deferred; offline collector package has separate corruption/inspection tests. |
| AC-ING-8 | TP-ING-008 later upload slice | Generic CSV/JSON mapping is deferred. |
| AC-ING-9 | TP-ING-009 lifecycle | Temporary raw expiry no later than 30 days; normalized provenance survives. |
| AC-ING-10 | TP-ING-010 minimization | General identity/account profiles excluded; approved references only. |
| AC-ING-11 | TP-ING-011 hosted assembler | Value-level provenance/conflict across methods; collector contributes its own provenance. |
| AC-ING-12 | TP-ING-012 mapping | Matching vendor default and actual value retained; missing baseline labeled incomplete. |
| AC-ING-13 | TP-ING-013 coverage | Uninstalled module is not applicable; inaccessible installed module is a gap. |
| AC-ING-14 | TP-ING-014 coverage | Every supported in-scope category/object has terminal assessed or gap state. |
| AC-ING-15 | TP-ING-015 capability | Unsupported/unverified version cannot migrate; reduced-trust upload is a later slice. |
| AC-ING-16 | TP-ING-016 permission | Excess read-only warns/audits; any write/DDL/ownership/admin blocks before query. |

## Unit and static tests

- Static SQL validator rejects multiple statements, dynamic SQL, write/DDL/control/permission/impersonation/administrative statements, unbounded fields and unsupported query-pack metadata. Bound parameter values cannot change SQL structure.
- Exact-build/module applicability rejects unknown, mismatched, suspended and future versions; no broad `10.x` wildcard is pilot-validated.
- Permission classifier tests minimum read, excess read-only, write, DDL, owner, impersonation, security/server administration, agent/job and backup/restore categories; block occurs before evidence-read mock invocation.
- Field/category policy excludes prohibited values and general profiles, preserves allowed native type/UID/path/provenance and emits explicit redaction/gap markers.
- Checkpoints bind pack/build/scope/policy/order/boundary/digest; replay same digest is idempotent, changed digest is conflict, incompatible checkpoint stops.
- Package vectors cover AES-256-GCM authentication, RSA-OAEP-SHA256 wrap/unwrap, wrong key, altered nonce/tag/ciphertext/manifest, expiry, wrong scope and duplicate chunk. Exact serialized vectors await envelope approval.
- Collector-host contract cases reject unknown/duplicate/future configuration fields, bad digests and limits, unapproved CLI switches, UNC/device/traversal/alternate-stream paths, and exercise spring gap, fall ambiguity and missed-run schedule behavior. The one-shot command must remain blocked without an approved query pack and offline envelope.
- The local run-lease primitive must reject an overlapping scope, allow a different scope and permit reacquisition after release. Windows service/CLI contention and crash recovery require controlled host tests before collection.
- The local encrypted checkpoint ledger must authenticate exact context, reject wrong keys, ciphertext tampering, duplicate pages, removal or rewriting of completed pages, and enforce file/page bounds. Protected key provisioning and crash recovery require Windows integration tests.
- Windows 2022/2025 CI cases must reload the same DPAPI-protected key, reject a wrong scope and tampered blob, and reject a key file with a write-capable broad ACL. These checks do not substitute for customer service-identity, Server Core, installer or crash-recovery validation.
- Ephemeral Windows 2022/2025 runners must start and stop the self-contained collector under Service Control Manager with a synthetic protected config, observe payload-free disabled status, and confirm one-shot collection exits blocked without a package. This smoke check does not validate a signed MSI or customer service identity.

## Integration and end-to-end tests

- Dedicated synthetic SQL Server fixture exercises Integrated Security and SQL-account fallback, pre-query permission attestation, bounded pages, cancellation, timeout, impact stop, schema/column mismatch and no source mutation.
- Windows Service schedule and one-shot CLI use the same core; overlap is skipped, missed offline schedule does not catch up, restart resumes compatible encrypted checkpoint, local storage/retention limit fails safely.
- Authorized enrollment produces a non-exportable device key/certificate; wrong, revoked, expired or cross-scope device/upload authorization fails. Online path uses outbound HTTPS only.
- Offline handoff imports an approved package only after inspection and digest/signature verification; replay and corruption never create duplicate active evidence.
- Baseline assembler compares multiple pages/endpoints, retains value-level provenance/conflicts, creates explicit gaps, and never mutates prior manifests. Raw/normalized deletion and surviving unavailable markers are verified independently.
- Health assessment accepts only an eligible immutable feature-001 baseline and never opens a source SQL connection.

## Authorization and isolation tests

- Customer administrator can install/enroll/configure/remove only its collector; consultant, partner administrator and platform operator cannot administer it.
- Hosted enrollment/upload/import/activation denies unauthenticated, wrong role, customer, project, environment, suspended/deleted scope, revoked device and direct locator substitution.
- Possession of a package, digest, checkpoint or device certificate alone never authorizes another customer data plane or source SQL principal.

## Failure, compatibility and recovery tests

- SQL disconnect, credential expiry, cancellation, service crash, upload outage, local queue full/expired, interrupted cleanup, malformed page and checksum mismatch preserve completed work and expose the correct gap/error.
- Collector/query-pack/manifest schema version skew fails closed or follows an explicitly approved compatibility matrix; old baselines remain readable without reinterpretation.
- Installer upgrade/downgrade and removal verify publisher/chain/digest, supported host, no remote self-update, enrollment revocation and customer-controlled local cleanup.

## Performance and security evidence

Performance testing is required. Independently exercise 100,000 records in each applicable named scale category under the approved query/build plan, bounded page size/concurrency/timeouts, source impact and local storage caps. Record duration, source resource impact, retries, checkpoint lag and partial outcomes without values. Customer database owner approves each exact query/build execution plan and material-impact threshold before a pilot source run.

Security testing includes static SQL adversarial inputs; effective permission denial; field minimization and prohibited value detection; DPAPI/ACL inspection; device key non-exportability/revocation; TLS-only outbound connectivity; package crypto tamper/expiry; payload-free logs/status/audit; secret scan; signed MSI provenance; and no inbound listener. Security findings remain open until executed evidence exists.

## Manual environment evidence and gaps

- Windows Server 2022 and 2025, including Server Core, MSI install/service restart/upgrade/removal and Integrated/SQL-auth flows need controlled Windows hosts.
- PILOT-ENV-A and PILOT-ENV-B must be independent exact One Identity 10.x/SQL Server environments with protected identifiers and customer database-owner authorization. Build/module/query/mapping/permission/impact/SME records are not available locally.
- Enrollment/upload, hosted baseline activation, customer data-plane isolation and lifecycle checks need approved cloud resources. Offline package serialization and API operation shapes still need technical/security review.
- AC-ING-7/8 and the broader multi-source paths remain later feature-001 design, not a pass/fail claim for this pilot slice.

## Approval

Approved for local pilot implementation by: Repository owner  
Date: 2026-09-29

The exact query pack, customer source tests, offline exchange format, hosted operations and production release require their separate gates.
