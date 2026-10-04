# One Identity Manager SQL Server Evidence Contract

Status: Approved  
Owner: Technical owner  
Reviewers: One Identity SME, security owner, customer database owner  
Last updated: 2026-09-28

## Purpose and boundary

This contract defines how feature 001 may produce the immutable One Identity Manager 10.x SQL Server baseline consumed by the health pilot. The health-assessment feature never connects directly to the source database. It reads only an immutable baseline that conforms to this contract.

Source interaction is read-only. A dedicated database account and a versioned query pack are mandatory. Normal collection uses an approved pack; a repository-owner promotion override may activate a named unverified pack for limited pilot trial under the exception below. Excess read-only permissions create a prominent warning and audit event under the approved pilot exception. Any effective write, DDL, ownership, impersonation, security-administration, server-administration, agent/job administration, backup/restore, or equivalent administrative capability blocks collection.

Database topology is prohibited evidence. The contract must not collect or infer server names, instance layout, clustering, availability groups, replication topology, network addresses, file paths, storage layout, backup locations, or infrastructure credentials.

## Query-pack contract

Every query has immutable metadata:

- query ID and semantic version;
- supported exact product build ranges and module applicability;
- purpose, evidence categories and assessment domains;
- SQL digest and reviewer-approved source artifact;
- expected columns, types, nullability and classification;
- stable ordering and checkpoint key;
- maximum page size, row estimate, duration budget and cancellation behavior;
- source-native keys and relationship keys;
- required minimum permission and prohibited returned fields;
- known limitations, version hazards and safe fallback;
- One Identity SME, security and database-owner review evidence.

Before promotion, automated static validation must reject query-pack content containing multiple statements, dynamic SQL, data modification, DDL, transaction/control changes, role or permission changes, ownership/impersonation, stored procedure execution not individually approved, external access, server/database configuration inspection outside the allowed version/permission contract, or unbounded payload fields. Parameter values are bound, never interpolated.

The repository owner may waive internal query-pack promotion reviewers or evidence gates for a named pilot artifact under the health-assessment implementation plan's owner exception. Such a waiver is not customer database-owner permission to access the source. It does not disable the collector's runtime query validation or source permission attestation; unsafe SQL or a write/admin-capable account remains blocked before collection. An exception-active pack stays visibly unverified and cannot establish a `pilot-validated` capability row or pilot acceptance.

Runtime protections include connection/read-only intent where supported, statement timeout, cancellation, bounded pages, stable checkpoints, configured concurrency, resource/back-pressure limits, and query correlation. The collector must stop when its impact guardrail is reached and preserve completed pages. Locking/isolation hints are not selected generically: each exact query/build combination needs database-owner review and execution-plan evidence showing it avoids locks or material production impact.

## Permission-attestation result

The baseline includes a derived permission-attestation record, not credential data:

| Field | Meaning |
|---|---|
| `principal_evidence_id` | Opaque reference to the tested dedicated account; not its credential |
| `query_pack_version` | Pack used for minimum-permission comparison |
| `minimum_read_set` | Versioned identifier for required read capabilities |
| `excess_read_only_detected` | Boolean plus permission-category identifiers |
| `blocking_capability_detected` | Boolean plus write/DDL/ownership/admin category identifiers |
| `decision` | `eligible`, `eligible_with_warning`, or `blocked` |
| `tested_at` | Timestamp and collection correlation |
| `method_version` | Permission-evaluation implementation version |

The assessment refuses baselines with `blocked`. It surfaces and audits `eligible_with_warning`. Raw permission listings remain protected evidence and follow customer policy.

## Baseline manifest

| Field | Requirement |
|---|---|
| Baseline identity | Immutable baseline ID, manifest schema, content digest and creation time |
| Scope | Customer, project and environment opaque IDs; collection scope version |
| Source | Product name, exact One Identity version/build/hotfix evidence reference, installed module/version list |
| Database compatibility | Approved product/compatibility descriptor needed for query validation; no topology |
| Acquisition | Query-pack version, collector version, extraction method and endpoint evidence ID |
| Policy | Redaction/exclusion policy version, raw/normalized retention class and temporary-processing disclosure |
| Permission | Derived permission-attestation record and warning/block state |
| Coverage | Per category/object-type requested, completed, partial, inaccessible, excluded, redacted, unsupported, malformed and error counts |
| Freshness | Collection start/end and per-category source/evidence timestamps |
| Integrity | Ordered page/chunk digests, normalized object counts, relationship counts and unresolved-reference counts |
| Gaps/conflicts | Typed reason, affected category/scope, provenance and user-visible explanation |

## Normalized evidence envelope

Every normalized object and evidence record contains:

- stable baseline-local object ID;
- native table/object type and native UID when available;
- native column/path for each value or explicit derived-field lineage;
- module and exact source build reference;
- extraction query/page/row provenance and extraction time;
- evidence category, data classification and authorization category;
- vendor-default, customer-modified default, customer-created or unknown classification where supported;
- actual and matching vendor-default references when an overwritten default comparison is valid;
- normalized common type without erasing the native type;
- relationship edges with native keys and unresolved-reference markers;
- redacted, prohibited, customer-retained, permission-denied, unsupported, malformed and inaccessible reason markers;
- content/schema version and customer-scoped integrity digest.

General identity/account profiles are excluded. Only approved matching, ownership, approval, assignment, membership and relationship references may be represented. Source credentials, passwords, private keys, connection strings, encrypted secret values, Social Security numbers and government identifiers have no allowed value representation outside the customer boundary; only reason markers are permitted.

## Evidence categories

The contract supports versioned, module-aware categories for product/module discovery; schema/configuration; authorization; native roles and inheritance; IT Shop; attestation; compliance/SoD; company policies; risk and mitigations; application governance; account definitions/manage levels/templates; target-system and synchronization metadata; processes/scripts/custom code; compilation and consistency; Job Queue; DBQueue; audit; synchronization/process/error health; reporting configuration; data-archiving configuration; password-management configuration; and generic custom connector/module metadata.

Operational categories default to a configurable 90-day lookback bounded by source availability. Record caps and truncation produce explicit gap states and never appear as complete.

## Checkpointing, duplicates and conflicts

- A page checkpoint binds query ID/version, exact build, scope/policy version, stable ordering key, page boundary and content digest.
- Replayed pages with the same key and digest are idempotent. Same key with a different digest is a conflict requiring reconciliation.
- Duplicate native UIDs are preserved as a data-quality conflict, not merged silently.
- Conflicting values from query/report/export paths preserve value-level provenance and uncertainty.
- A resumed extraction never changes completed immutable pages. A completed incremental or reconciliation run creates a new baseline manifest.

## Failure behavior

| Condition | Required result |
|---|---|
| Unsupported exact build | Stop native query collection unless a separately approved reduced-trust upload applies; record unsupported capability |
| Write/admin capability | Block collection before evidence queries and audit the category without exposing credentials |
| Excess read-only permission | Continue only under pilot exception, warn prominently and audit |
| Query timeout/cancellation | Preserve completed pages; record explicit category gap; bounded retry only |
| Schema/column mismatch | Stop affected query; no positional coercion; mark unsupported/error and require a new mapping version |
| Row cap/truncation | Mark partial with boundary/count evidence |
| Prohibited value detected | Quarantine affected page from assessment, record redaction/security event and follow incident process |
| Digest mismatch | Baseline ineligible; preserve forensic metadata without serving payload |

## Validation gates

- Static SQL safety and prohibited-operation tests.
- Least-privilege and blocking-permission fixtures.
- Positive, empty, null, malformed, duplicate, conflict, truncation and unsupported-version contract fixtures.
- Exact-build schema compatibility and One Identity SME review.
- Execution-plan and bounded production-impact evidence approved by the customer database owner.
- Resume/idempotency and immutable-baseline tests.
- Data-minimization, secret/government-identifier exclusion and field-authorization tests.
- At least 100,000 records independently in each applicable named scale category, without requiring one environment to contain every category.

## Implementation decisions and open evidence

- Exact query text and field dictionaries are `NOT VERIFIED` until the two pilot environments are authorized and inspected through the approved workflow.
- No locking/isolation strategy is approved globally; it must be evidenced per query/build combination.
- Customer database-owner acceptance criteria for material production impact must be recorded for each pilot environment.
- IMP-DEC-003 selects an Authenticode-signed self-contained .NET 10 `win-x64` MSI for Windows Server 2022/2025, an unattended Windows Service plus one-shot offline CLI, and `Microsoft.Data.SqlClient` 7.1. Windows Integrated Security under a dedicated customer-managed gMSA/domain account is preferred; a dedicated SQL-authentication account is allowed only with DPAPI/ACL-protected local storage. Online delivery uses outbound-only device-certificate enrollment and short-lived upload scope; offline delivery uses the approved signed AES-256-GCM/RSA-OAEP-SHA256 package. Updates are manual signed MSI installs with no self-update channel. Exact identities, credentials and environment compatibility remain verification evidence.

## Approval

Approved by: Repository owner  
Date: 2026-09-28  

The normal approval path requires authorized technical, security, One Identity SME and customer database reviewers for an exact environment. The repository owner may override internal promotion review during the pilot as documented above; customer source-access authority and runtime source-safety controls still apply.
