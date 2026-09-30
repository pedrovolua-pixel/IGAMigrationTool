# Technical Specification: One Identity pilot ingestion slice

Status: Approved for local pilot implementation — exact source and delivery contracts remain gated  
Product spec: `specs/001-data-ingestion/product-spec.md` (Approved 2026-09-28)  
Author: Codex  
Reviewers: Technical owner, security owner, One Identity SME, customer database owner, operations owner  
Last updated: 2026-09-30

## Overview and scope

This specification covers the One Identity Manager 10.x on SQL Server acquisition path required by the approved health-assessment pilot. A customer-operated, signed .NET 10 Windows Service/CLI collector reads only through a reviewed query pack, applies customer field/category controls, and delivers an immutable, versioned baseline through outbound HTTPS or an authenticated encrypted offline package. Feature 003 consumes that baseline; it never connects to SQL Server. The product-wide SailPoint, generic upload, hosted connector, migration, and One Identity 8.x paths remain in feature 001 scope but are not designed or authorized for implementation by this pilot slice.

The pilot collector profile is already selected by accepted ADR-0004 and `specs/003-health-assessment/implementation-plan.md` IMP-DEC-003. This specification does not select exact SQL text, field dictionary, enrollment protocol, upload API, offline envelope serialization, MSI toolchain, or source-environment identifiers. Those are approval or evidence gates below.

## Relevant architecture and decisions

- ADR-0001: collector/import and assessment work remain separate bounded modules; the collector is a separate customer-side release artifact.
- ADR-0002: the hosted application resolves exactly one customer data plane from trusted assignment; the collector cannot select a platform data-plane locator.
- ADR-0003: normalized evidence and raw permitted payloads have separate lifecycle classes and immutable customer-scoped manifests.
- ADR-0004: customer-side One Identity collector, .NET 10 `win-x64`, Windows Server 2022/2025 including Server Core, signed MSI, unattended service and one-shot offline CLI, `Microsoft.Data.SqlClient` 7.1, outbound-only enrollment/delivery, and no inbound listener or remote self-update.
- `specs/003-health-assessment/database-evidence-contract.md` and `capability-matrix.md` govern the pilot baseline and exact-build eligibility. SEC-PILOT-003 remains open until environment evidence exists.

## Requirement traceability

| Product requirement | Pilot treatment |
|---|---|
| FR-ING-1–3 | One project/source scope; source interaction is read-only on every run and retry. |
| FR-ING-4–6 | Customer-side SQL collector only in this slice; broader hosted, API, export, upload and authentication paths need separate technical design. |
| FR-ING-7 | Attest effective capabilities before evidence queries; excess read-only warns and audits, any write/DDL/ownership/impersonation/admin capability blocks. |
| FR-ING-8–9 | Each completed extraction yields a new immutable baseline; reconciliation and tombstones belong to the hosted baseline assembler, with no mutation of prior baselines. |
| FR-ING-10–12 | Service schedule and one-shot mode use bounded checkpoints; overlapping runs skip, missed offline schedules do not catch up, and retries never duplicate completed pages. |
| FR-ING-13–16 | Generic uploads/mapping and multi-file import are deferred. Offline collector package inspection, digest, path and size controls must be designed with the receiving import boundary before enablement. |
| FR-ING-17–18 | Hosted baseline assembler exposes states, comparisons, provenance and conflicts; collector does not silently choose between acquisition methods. |
| FR-ING-19–25 | Field/category allowlists and exclusions precede transfer; prohibited values never become evidence; markers express redaction/gaps; raw/normalized retention stays separate. The health pilot's 90-day operational window specializes the product's broader one-year default. |
| FR-ING-26–27 | Customer-admin operated signed Windows collector, offline encrypted queue, outbound-only delivery, dedicated read-only SQL principal, bounded local retention. Pilot upgrades are manual signed MSI installs. |
| FR-ING-28–35 | Exact-build/module discovery and versioned query pack preserve native keys/types, customization and relationship provenance; unsupported and uninstalled states are explicit. Exact field coverage remains unverified until pilot environments and SME-approved packs exist. |
| FR-ING-36 | No migration destination mapping or source mutation in this slice. |

AC-ING-1 through AC-ING-16 are mapped to executable and environment tests in the companion draft test plan. AC-ING-7 and AC-ING-8 depend on the later generic-upload slice; AC-ING-11 requires the hosted multi-endpoint assembler; AC-ING-15's migration boundary is enforced by capability state outside the collector.

## Components affected

| Area | Pilot change |
|---|---|
| Customer-side application | Windows Service scheduler and one-shot CLI share a collector core with query-pack validation, permission attestation, bounded extraction, local checkpointing, minimization and delivery adapters. |
| Hosted ingestion module | Authenticated enrollment/upload or offline import, package inspection, immutable baseline assembly, coverage/provenance and lifecycle enforcement. Exact operation shapes remain pending technical approval. |
| Data | Versioned query-pack and mapping records; derived permission result; page/chunk manifest; normalized evidence, provenance, gaps, conflicts and baseline manifest. |
| Infrastructure | Customer-managed Windows Server and SQL principal; platform customer-scoped storage and ingestion workers under accepted ADRs. No source database access from Azure. |

## Collector boundaries and execution

1. Installation, service-account setup, enrollment, schedule and removal are customer-administrator actions. The service has no inbound listener or interactive browser surface.
2. A run fixes the customer-approved project/environment scope, exact product build/module claim, query-pack digest, field/category policy and local limits before opening a source connection. Any drift pauses the run for an attributed decision; it does not broaden collection.
3. The collector establishes the dedicated SQL connection using Windows Integrated Security where possible. A dedicated SQL account fallback reads its secret only from DPAPI/ACL-protected local storage. The secret never enters an artifact, platform upload, ordinary log, crash report or checkpoint.
4. A separate permission-attestation step tests the effective dedicated principal against the approved minimum and blocking capability categories. Any blocking category stops before evidence queries. Excess read-only produces an explicit warning/audit result and may proceed only under the approved pilot exception.
5. The collector accepts only a promoted, digest-bound pack applicable to the exact build and installed module. Static validation rejects unsafe statement classes and unbounded/prohibited fields. Bound parameters, page limits, timeouts, cancellation and per-query impact budgets are mandatory. No generic locking hint is selected; exact query/build execution plans require customer database-owner review.
6. Each completed page is minimized and classified before it leaves the customer boundary. A checkpoint binds the exact pack/build/scope/policy, stable ordering key, boundary and digest. A resumed page with the same key/digest is idempotent; a changed digest is a visible conflict.
7. Online delivery uses outbound-only HTTPS, a non-exportable device certificate created during one-time enrollment and short-lived scoped upload authorization. A revoked device cannot create new uploads. The collector has no Azure identity or customer database locator.
8. Offline mode produces the approved authenticated encrypted package using a random AES-256-GCM key wrapped to the platform's approved RSA public key with RSA-OAEP-SHA256. The exact signed manifest, envelope serialization, key identity/rotation and vectors require approval before G2; this draft authorizes no exchange format.
9. Local staging and offline queue contents are encrypted and ACL-restricted. Only the latest extraction is retained until the next successful extraction, subject to the configured expiry/capacity. Successful handoff removes completed clear working material; cleanup is idempotent and payload-free in audit.

## Data model and lifecycle

- `QueryPackVersion`: immutable pack ID/version/digest, supported exact builds and modules, approved query metadata, field classification, checkpoint contract, impact limits and reviewer evidence. Exact SQL is unavailable until source validation.
- `PermissionAttestation`: opaque tested-principal reference, minimum-read-set version, excess-read-only and blocking-category results, decision, method version and time. Raw grant listings stay protected customer evidence.
- `ExtractionRun`: opaque scope, pack/build/policy locks, start/end, state, correlation, encrypted checkpoint reference, per-category coverage and warnings. No credential or topology field.
- `PageManifest`: query/page key, ordered digest, row count, provenance, classification/redaction summary and immutable completion marker. A content conflict cannot overwrite the prior page.
- `BaselineManifest`: immutable baseline identity, scope, source/build/modules, extraction and schema versions, acquisition path, policy, permission result, ordered page digests, coverage, gaps/conflicts, counts and retention references under the approved evidence contract.
- Raw permitted values, normalized values and source-specific unmapped values have separate classifications and retention/deletion states. Deleting one class leaves an explicit unavailable marker and does not rewrite the baseline's historical provenance.

## Interfaces and authorization

The collector's local CLI and service share the same policy engine. The CLI accepts only a customer-admin supplied protected local configuration reference and one-shot collection action; it must not accept plaintext SQL credentials or arbitrary SQL/query text. The service accepts only protected local configuration changes made through customer administrator control. The pilot-local command, configuration, schedule and shell status contract is approved in `collector-local-service-contract-proposal.md` under the repository owner's standing pilot preapproval. Signed configuration provisioning, enrollment request/response, upload/import operations and collection-phase error schema remain separate contracts before enablement.

The hosted side authorizes enrollment, upload, import, cancellation and baseline activation using customer/project/environment assignment and resource state. A device certificate proves collector identity, not customer role or source-database authority. The server resolves the customer data plane from trusted assignment; it does not trust scope or storage locators supplied by the collector. Revocation blocks new use while preserving the independently governed choice to delete prior evidence.

## State, concurrency and recovery

Collector run states are queued, validating, collecting, waiting for reconnection, partially complete, complete with warnings, complete, failed, canceled and expired, mapped to product-visible states. One active run per configured source/scope is allowed; an overlapping schedule is skipped. A missed offline schedule waits for the next cycle. Cancellation stops new pages and preserves completed immutable pages. Retry resumes only if pack/build/scope/policy/checkpoint compatibility still holds. A failed endpoint or category creates a gap, never false completion. The hosted assembler activates a partial baseline only with explicit warnings and gap records.

| Failure | Required behavior |
|---|---|
| Unsupported exact build/module | Stop native query collection; expose unsupported state. Reduced-trust upload is a later separately designed path. |
| Write/admin effective capability | Block before evidence query; emit category-only audit. |
| SQL timeout, cancellation or impact guardrail | Stop or pause affected query, preserve completed pages, mark explicit gap. |
| Schema/column mismatch | Reject affected mapping without positional coercion; require a new reviewed mapping version. |
| Offline/revoked upload credential | Preserve encrypted bounded queue; resume only after authorized renewal. |
| Replayed page or chunk | Same key/digest is idempotent; different digest is a conflict. |
| Prohibited value, corrupt digest or unsafe package | Quarantine affected unit from activation; retain only approved forensic metadata. |
| Local storage limit or expiry | Stop collection safely, expose status, and follow customer-configured cleanup; never silently evict an incomplete baseline as complete. |

## Security, privacy and observability

No source write path, dynamic SQL, cloud-held SQL credential, inbound listener, auto-update channel or general identity-profile collection is permitted. Logs and status contain opaque scope/correlation, state, durations, counts, category-level gaps, permission decision and error codes, without SQL text, query results, topology, credentials, government identifiers or protected values. Metrics cover duration, pages, bytes, retries, checkpoints, queue depth/age, impact stops, gaps and upload failures. Alerts cover blocking permissions, repeated integrity failures, expired offline queue, stopped service and prolonged delivery failure. Every audit event is payload-free and follows the approved pilot audit lifecycle.

## Deployment, compatibility and rollback

The customer installs a signed self-contained `win-x64` MSI on supported Windows Server and controls its service identity, SQL principal, enrollment and local storage. The pilot has manual signed upgrades only; publisher, chain, version and published digest are checked before install. Downgrade requires an approved recovery record with exact version and compatibility evidence. A collector release declares supported pack/manifest/schema versions; unknown future versions fail safely. Rollback does not rewrite completed baselines or checkpoints. Removal revokes enrollment, stops schedules and handles local retained material under the customer-configured policy.

## Test implications

Synthetic tests must cover SQL static rejection, permission categories, build/module applicability, schema and field minimization, exact checkpoint replay/conflict, cancellation/timeout/impact stops, package corruption, enrollment/revocation, local expiry, version compatibility and no payload in logs. Windows Server 2022/2025 installation, service restart, DPAPI/ACL, Integrated/SQL-auth fallback, certificate lifecycle, Authenticode/MSI verification and actual SQL execution plans require controlled environment evidence. Two independent exact One Identity 10.x environments are required for G2/pilot acceptance.

## Alternatives considered

- Hosted direct SQL access would require a cloud-to-customer source path and conflicts with the approved customer-side pilot boundary; it is not selected.
- A generic on-premises connector for every product would erase One Identity-specific query and permission controls; future SaaS integrations use separately designed hosted connectors.
- A remotely self-updating collector conflicts with the approved pilot manual signed-update boundary.

## Risks, open questions and required decisions

- Exact build/module rows, query text, columns, field dictionary, minimum permission set, impact budgets and production-safe execution plans require pilot-environment/SME/database-owner evidence. No query pack can be promoted from this specification alone.
- The enrollment/upload protocol, offline package envelope serialization and test vectors, MSI toolchain and signature/provenance process require concrete reviewed proposals before their implementation. The pilot-local configuration/CLI shell contract is approved, but its Windows ACL/service behavior needs controlled host verification before customer installation.
- Feature 001's full multi-source technical design, implementation plan and test plan remain separate work. This pilot slice cannot be used to infer hosted SailPoint, generic upload or migration behavior.
- The repository owner approved local pilot implementation against this specification on 2026-09-29 and gave standing approval for pilot-local work. Exact query-pack/source access still needs One Identity SME and customer database-owner review; unresolved delivery, envelope and MSI contracts need concrete evidence before those surfaces are enabled. This approval does not establish G2.

## Approval

Approved for local pilot implementation by: Repository owner  
Date: 2026-09-29

This approval does not approve exact environment rows, query packs, delivery operations or production release.
