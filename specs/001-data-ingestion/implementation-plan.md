# Implementation Plan: One Identity pilot ingestion slice

Status: Approved for local pilot implementation — external source and delivery gates remain open  
Product spec: `specs/001-data-ingestion/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/001-data-ingestion/technical-spec.md` (Approved for local pilot implementation)  
Test plan: `specs/001-data-ingestion/test-plan.md` (Approved for local pilot implementation)  
Owner: Technical owner  
Last updated: 2026-10-01

## Scope and constraints

This plan covers only the One Identity Manager 10.x SQL Server collector and immutable baseline acquisition needed by the approved health-assessment pilot. It follows the approved Windows Service/CLI profile in IMP-DEC-003 and accepted ADR-0004. SailPoint, generic uploads, hosted SaaS connectors, migration mappings, One Identity 8.x, and version 11+ remain outside this implementation slice. Feature 003 has no direct source-SQL path.

**Code gate:** The repository owner approved `technical-spec.md`, this plan and `test-plan.md` for local pilot implementation on 2026-09-29. The exact query pack, field dictionary, environment build/module rows, customer database-owner execution plans, enrollment/upload operations, offline envelope and MSI signing procedure each have their own later evidence/approval gate. The repository-owner pilot artifact-promotion exception cannot authorize source access, unsafe SQL, write/admin permissions or G2 acceptance.

Local implementation and synthetic fixtures can proceed while cloud and pilot environments are unavailable. No customer collector download, install, enrollment, SQL connection, online upload, offline import, baseline activation or production release is authorized by this approval alone.

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

- [x] Repository owner approved the pilot technical spec, this plan and test plan for local development, including the explicit deferred product paths.
- [x] Resolve the pilot-local configuration/CLI shell contract under repository-owner standing preapproval; source and release gates remain separate.
- [ ] Resolve enrollment/upload and offline envelope contracts with security and operations review.
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
| 2026-09-29 | A side-effect-free permission-decision prototype was added under the approved health-pilot source-safety contract. | No SQL probe, connection, credential or collection capability exists. This local check cannot satisfy C0, G2 or customer database-owner approval. |
| 2026-09-29 | A pure page-checkpoint verifier was added for the approved query/build/scope/policy/order/boundary/digest contract. | It distinguishes new, idempotent, conflicting and incompatible pages without storage or source access. Encrypted durable storage and atomic resume remain open. |
| 2026-09-29 | Repository owner approved the technical specification, implementation plan and test plan for local pilot development. | C1 implementation may proceed. Exact query pack, environment/DB-owner evidence, delivery and installer contracts, and G2 remain gated. |
| 2026-09-29 | Added a pure exact-build/module applicability decision with fourteen synthetic cases. | Metadata matching cannot approve a pack or enable SQL; source discovery provenance and actual build rows remain unavailable. |
| 2026-09-29 | Added a pure next-page budget decision with twelve synthetic cases. | The eventual executor must still bind paging parameters and enforce command timeout, cancellation, impact and gap reporting during SQL execution. |
| 2026-09-29 | Pinned Microsoft's T-SQL ScriptDom parser and added a conservative single-table/static-page shape check with synthetic adversarial cases. | Parser preflight is necessary but insufficient; exact SQL, approved fields, pack promotion and customer database-owner execution plans remain unavailable. |
| 2026-09-29 | Added an in-process per-scope run gate for overlap skip, failure release and cancellation release. | The future service/CLI needs a reviewed local configuration contract, durable cross-process coordination and restart recovery; this primitive cannot start collection. |
| 2026-09-29 | Added the approved AES-256-GCM/RSA-OAEP-SHA256 encryption primitive with ten synthetic tamper and wrong-key cases. | No envelope serialization, signature, key identity, expiry, package-size bound, recipient import or delivery path is implemented; the reviewed offline contract remains a separate gate. |
| 2026-09-29 | Repository owner gave standing preapproval for all pilot-local development; the local service/CLI shell contract in `collector-local-service-contract-proposal.md` is approved. | Local host work proceeds without review pauses; exact source, envelope, installer and cloud dependencies remain separate gates. |
| 2026-09-30 | Added `CollectorHost` with fixed CLI verbs, strict versioned config, Windows ACL/reparse checks, daylight-saving schedule and a Windows Service loop that stays blocked from collection. | No SQL connection, package output or customer install exists. Synthetic contract cases and cross-publish validate the local shell only; Windows service identity/ACL and MSI evidence remain open. |
| 2026-09-30 | Added a local exclusive-file run lease keyed by scope in the protected configuration directory. | The primitive rejects overlapping opens and releases after disposal; service/CLI integration, crash/restart and protected Windows directory evidence remain open. |
| 2026-09-30 | Added an authenticated encrypted, append-only local checkpoint ledger with bounded page count and file size. | Synthetic wrong-key/context, tamper and rewrite tests pass; protected key provisioning, service/CLI integration, Windows atomicity/crash and retention checks remain open. |
| 2026-09-30 | Added a Windows DPAPI `LocalMachine` checkpoint-key reader with scope-specific entropy and protected-file ACL checks; added Windows 2022/2025 CI host checks. | Key provisioning/rotation and service/CLI integration remain open. The CI runners exercise API/ACL behavior, not MSI, Server Core or customer-controlled hosts. |
| 2026-09-30 | Added an ephemeral Windows Service start/stop and blocked one-shot smoke script to the Windows 2022/2025 CI jobs. | This tests the self-contained executable with synthetic protected config under Service Control Manager; signed MSI, dedicated customer service identity and source access remain open. |
| 2026-09-30 | Added a pure field/category minimizer that requires an explicit field allowlist and drops values for excluded, redacted, prohibited and unclassified fields. | Twelve synthetic cases pass. Signed customer policy verification, exact field dictionary and pre-staging integration remain open; the pure snapshot is not an authorization source. |
| 2026-09-30 | Connected one-shot and scheduled service entry points to a common fail-closed local run coordinator. A synthetic adapter exercises exact pack/build/policy/permission admission, bounded pages, minimization, durable staging order, lease and encrypted checkpoint resume. | The shipped adapter returns pending and cannot read SQL or stage a package. Signed pack/policy loading, SQL permission probe, durable evidence sink, customer source tests and Windows crash/ACL validation remain open. See the SME handoff in `docs/operations/one-identity-sme-pilot-handoff.md`. |
| 2026-09-30 | Added `one-identity-sme-evidence-template.md` with publicly documented product concepts prefilled as candidates and exact per-environment query, field, module, fixture and DBA evidence slots. | Public documentation cannot verify either pilot environment or approve a query. The SME must confirm or correct every candidate, supply protected exact-build artifacts, and obtain customer database-owner review before source work. |
| 2026-09-30 | Applied the collector's configured run deadline to synthetic page staging as well as reading. | A timed-out staging adapter returns a partial limit outcome without advancing the checkpoint. The eventual durable sink must honor cancellation and stage idempotently before source activation. |
| 2026-09-30 | Classified adapter-originated cancellation during approved-material loading, page reading and staging separately from run cancellation or timeout. | Synthetic negative cases return source/stage failure without advancing a checkpoint; the shipped adapter still cannot read SQL or stage evidence. Controlled source and durable-sink checks remain open. |
| 2026-10-01 | The Environment A database owner authorized a one-off bounded metadata diagnostic. A dedicated principal with selected column-level reads passed a local blocking-capability probe, and a prohibited connection-string column was denied. Live SQL and 14 active module rows matched the owner-supplied inventory, but report-cover and main-database edition/build fields differ. | Provisional owner-only artifacts and the discrepancy are recorded in `one-identity-environment-a-sme-response.md`. SME reconciliation, trusted SQL certificate, formal permissions/query-plan/impact review, exact schema build/hotfix and protected source binding remain open. No collector source adapter or G2 evidence was produced. |
| 2026-10-01 | Added synthetic recovery checks for a staged page whose checkpoint write fails, a changed-content restage conflict, and cross-process lease release after forced termination. Extended the Windows runner smoke script to restart the blocked service. Prepared `collector-delivery-and-installer-contract-proposal.md` for later review. | A new coordinator invocation can retry the synthetic page idempotently, and the local lease can be reacquired after a killed test process. [Partial bootstrap run 36868281712](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36868281712) passed on commit `028d864`, including both Windows jobs and the restart smoke step. No durable production sink, approved delivery protocol/envelope, signed MSI, source access or G2 evidence follows. |
| 2026-10-01 | Repository owner approved the candidate collector delivery/installer direction for continued pilot-local work. Added an isolated AES-256-GCM page-stage primitive with context-bound opaque filenames, content verification, atomic create, replay/conflict checks and per-page size bound. | Exact online/offline/MSI contracts remain draft. The primitive is not wired into the shipped adapter and has no validated directory ACL, key lifecycle, aggregate queue/retention or customer-host recovery; source interaction, package exchange and installation stay disabled. |
| 2026-10-01 | [Partial bootstrap run 36870871323](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36870871323) passed on implementation commit `840f3d3`: Linux format/build/unit/architecture/secret/Bicep checks and both Windows Server runner collector/DPAPI/ACL/publish/restart smoke jobs. | The first stage-store push had a missing import and failed CI; `840f3d3` corrected it. The passing runner evidence verifies this local slice only, not protected customer staging, signed MSI, source access, package import or G2. |
| 2026-10-01 | Switched the synthetic coordinator adapter from an in-memory stage map to the encrypted page-stage primitive. A fresh adapter instance reuses a previously staged page after a simulated checkpoint-write failure; changed content conflicts. | This exercises durable synthetic staging across adapter instances. Shipped collection stays blocked pending a reviewed adapter, protected-directory ACL and key lifecycle, aggregate queue/retention and customer-host recovery evidence. |
| 2026-10-01 | [Partial bootstrap run 36871748202](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36871748202) passed on implementation commit `3b69ca9`, including Linux format/build/unit/architecture/secret/Bicep checks and both Windows Server collector/DPAPI/ACL/publish/restart smoke jobs. | The synthetic encrypted-stage recovery is verified on ephemeral runners; protected customer staging, signed MSI, source access, package import and G2 remain open. |
| 2026-10-01 | Added Windows stage-directory and file ACL/path checks before encrypted stage reads and writes, plus synthetic broad-ACE rejection cases for both levels. | Customer service-identity directory provisioning, key rotation, aggregate storage/retention and installation remain gated; local non-Windows tests cannot verify the new ACL behavior. |
| 2026-10-01 | [Partial bootstrap run 36872704294](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36872704294) passed on implementation commit `358914d`, including both Windows Server stage ACL negative cases plus Linux format/build/unit/architecture/secret/Bicep and Windows publish/restart checks. | The runner guards do not prove customer service-identity provisioning, installer behavior, aggregate retention, source access, delivery or G2. |
| 2026-10-01 | Upgraded the internal encrypted checkpoint to `IGC2` with completed row counts and terminal markers and the isolated stage file to `IGS2` with terminal state in its digest. Resume now applies cumulative row limits and skips a completed terminal page. New staged pages require available capacity under `maxLocalBytes`, counting every run-directory file. | Older count-less local files fail closed; no customer-deployed format migration exists. Expiry, cleanup, cross-run queue limits, protected run-directory/key provisioning and controlled source evidence remain open. |
