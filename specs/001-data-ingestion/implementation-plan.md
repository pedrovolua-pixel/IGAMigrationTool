# Implementation Plan: One Identity pilot ingestion slice

Status: Draft — contingent on technical, security, operations, SME and customer database-owner approval  
Product spec: `specs/001-data-ingestion/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/001-data-ingestion/technical-spec.md` (Draft)  
Test plan: `specs/001-data-ingestion/test-plan.md` (Draft)  
Owner: Technical owner  
Last updated: 2026-09-29

## Scope and constraints

This plan covers only the One Identity Manager 10.x SQL Server collector and immutable baseline acquisition needed by the approved health-assessment pilot. It follows the approved Windows Service/CLI profile in IMP-DEC-003 and accepted ADR-0004. SailPoint, generic uploads, hosted SaaS connectors, migration mappings, One Identity 8.x, and version 11+ remain outside this implementation slice. Feature 003 has no direct source-SQL path.

**Code gate:** `technical-spec.md`, this plan and `test-plan.md` must be approved before substantial collector product behavior is implemented. The exact query pack, field dictionary, environment build/module rows, customer database-owner execution plans, enrollment/upload operations, offline envelope and MSI signing procedure each have their own later evidence/approval gate. The repository-owner pilot artifact-promotion exception cannot authorize source access, unsafe SQL, write/admin permissions or G2 acceptance.

Local design and synthetic fixtures can proceed while cloud and pilot environments are unavailable. No collector download, install, enrollment, SQL connection, online upload, offline import, baseline activation or production release is authorized by this draft.

## Requirement traceability

| Work item | Product requirements / criteria | Verification |
|---|---|---|
| ING-PILOT-001 — scope, capability and policy locks | FR-ING-1–3, 7, 19, 25, 27–29, 35; AC-ING-1–3, 10, 13–16 | Version/module/permission and field-policy fixtures; explicit unsupported/gap states |
| ING-PILOT-002 — collector host and source reads | FR-ING-10–12, 26–28, 33; AC-ING-2, 4, 16 | Windows service/CLI, static SQL, bound parameters, cancellation, impact and checkpoint tests |
| ING-PILOT-003 — secure delivery and retention | FR-ING-23–27; AC-ING-9–10 | Enrollment/revocation, package crypto, local queue/expiry, payload-free audit and redaction tests |
| ING-PILOT-004 — immutable baseline | FR-ING-8–9, 16–18, 23–25, 29–35; AC-ING-5–6, 10–14 | Page replay/conflict, manifest/digest, coverage/provenance, partial activation and lifecycle tests |
| ING-PILOT-005 — pilot validation | FR-ING-27–35; AC-ING-2–6, 10–16 | Two independent exact builds, owner-approved query plans, SME review, performance and G2 records |

AC-ING-7–8 require the separately designed generic-upload slice. AC-ING-11 requires the hosted multi-endpoint assembler; the collector must preserve value-level provenance but cannot alone establish multi-method correlation. AC-ING-15's migration rejection remains a capability/policy obligation outside the collector.

## Milestones

### C0 — Approve the executable collector boundary

- [ ] Review and approve the pilot technical spec, this plan and test plan, including the explicit deferred product paths.
- [ ] Resolve exact local configuration/CLI, enrollment/upload and offline envelope contracts with security and operations review.
- [ ] Record query-pack/field-dictionary and package-signing approval authorities and evidence locations.

Verification:

- [ ] All consequential contracts and negative paths have named owners, versions and review decisions; no query or deployment is enabled by draft status.

### C1 — Build local source-safety and checkpoint core

- [ ] Create the separate `src/collector/` .NET 10 code boundary and architecture checks; no reference to assessment stores or hosted data-plane locators.
- [ ] Implement exact-build/query-pack applicability and static SQL safety, permission attestation, bounded parameterized pages, cancellation and impact stop.
- [ ] Implement policy-bound encrypted local checkpoints, page-digest replay/conflict and explicit coverage/gap outcomes.
- [ ] Add synthetic positive, negative, malformed, unsupported and duplicate fixtures without customer data.

Verification:

- [ ] Static SQL and blocking-permission tests reject before any evidence query; no source-write path exists.
- [ ] Repeated and changed page boundaries yield idempotent result or visible conflict; cancellation preserves completed immutable pages.

### C2 — Package and operate the customer-side service

- [ ] Use the same approved collector core from an unattended Windows Service and one-shot offline CLI.
- [ ] Implement customer-admin controlled schedule/configuration, overlap skip, restart recovery, local encrypted staging/queue and bounded retention.
- [ ] Build self-contained `win-x64` release and signed MSI with publisher/digest/version verification and manual upgrade/rollback procedure.

Verification:

- [ ] Windows Server 2022/2025 including Server Core installation, service restart, Integrated Security, DPAPI/ACL SQL fallback and no-listener tests pass.
- [ ] Signed package provenance, no self-update path and payload-free status/audit are verified.

### C3 — Deliver authorized evidence and assemble an immutable baseline

- [ ] Implement reviewed device-certificate enrollment, revocation and short-lived scoped upload without an inbound listener.
- [ ] Implement the approved authenticated encrypted offline package and receiving inspection/import path.
- [ ] Implement customer-scoped normalized baseline assembly, ordered manifest/digests, provenance, conflicts, gaps and independent raw/normalized lifecycle.
- [ ] Integrate the health feature's read-only baseline adapter after G2 eligibility evidence.

Verification:

- [ ] Wrong/revoked identity or scope, corrupted/replayed/expired package, prohibited field and digest mismatch are denied before activation.
- [ ] Offline and online delivery of the same pages cannot create duplicate active evidence.

### C4 — Validate two independent pilot environments

- [ ] Record exact build/hotfix/module and SQL compatibility rows for PILOT-ENV-A and PILOT-ENV-B without database topology.
- [ ] Obtain customer database-owner permission and execution-plan/impact evidence per exact query/build; obtain One Identity SME, technical and security review.
- [ ] Run category-specific 100,000-record scale cases, permission and minimization tests, end-to-end collection/resume and baseline eligibility.

Verification:

- [ ] G2 passes independently for each exact build before activation; SEC-PILOT-003 is closed only with executed evidence.

## Hardening, rollout and rollback

- [ ] Run formatting, linting, locked restore, type/build, unit, contract, integration, architecture, secret, dependency, license, packaging/signature and vulnerability checks applicable to collector changes.
- [ ] No customer payload, SQL credential or topology enters CI, source control, logs or test artifacts.
- [ ] Release is disabled by default; activation is scoped to a verified exact build and customer-authorized source path.
- [ ] Rollback stops collection and revokes enrollment, preserves already completed immutable baselines, and uses only a signed compatible previous collector after a recorded recovery decision.
- [ ] Update the canonical health-pilot status, capability matrix, database evidence contract, operations and security evidence with executed results.

## Completion gate

- [ ] C0 through C4 and applicable AC-ING outcomes pass with immutable evidence.
- [ ] Feature-001 and feature-003 status, query/mapping/schema versions and documentation are current.
- [ ] Technical, security, operations, One Identity SME and customer database-owner reviews are recorded.
- [ ] Production or irreversible operation has separate explicit authorization.

## Discoveries and plan changes

| Date | Discovery | Impact/evidence |
|---|---|---|
| 2026-09-29 | Product approval exists but feature-001 technical/test/implementation approval does not. | Drafted a bounded pilot plan; collector behavior remains behind C0. |
