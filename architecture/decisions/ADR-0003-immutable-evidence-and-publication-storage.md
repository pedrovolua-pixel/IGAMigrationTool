# ADR-0003: Immutable evidence and publication storage

Status: Accepted  
Date: 2026-09-28  
Decision owners: Technical owner, security owner

## Context

The pilot locks evidence, rules, profiles, desired outcomes, models, and scoring settings for each assessment. It must preserve historical interpretation, support reassessment without rewriting prior results, publish immutable reports, delete raw and normalized evidence independently, and continue showing provenance when underlying evidence expires.

## Decision drivers

- Reproduce a run from its recorded inputs and software versions.
- Prevent edits from changing a published score or report.
- Preserve exact evidence references without copying sensitive payloads into findings.
- Apply independent retention and deletion to raw evidence, normalized evidence, derived records, and export artifacts.
- Support large evidence sets and efficient interactive queries.

## Options considered

### Option A: Mutable relational records only

- Advantages: simple query model and transactions.
- Disadvantages: large payload storage is inefficient; immutability depends on application behavior; independent evidence deletion is awkward.
- Risks: later edits could silently reinterpret historical results.

### Option B: Event sourcing for all domain state

- Advantages: complete history and replayability.
- Disadvantages: substantial projection, migration, privacy-erasure, and operational complexity.
- Risks: replay code changes can produce a different historical view unless every projection implementation is retained.

### Option C: Versioned relational metadata plus content-addressed immutable blobs

- Advantages: transactional domain state and efficient querying; immutable large payloads; explicit manifests; independent lifecycle policies.
- Disadvantages: consistency between metadata and blob storage requires controlled commit and garbage collection.
- Risks: content hashes can become cross-tenant correlation signals if globally deduplicated.

## Decision

Use versioned relational metadata for canonical domain entities and immutable, content-addressed blobs for permitted evidence payloads and generated artifacts. Content addressing and deduplication are scoped to one customer data plane; hashes are never exposed as authorization tokens.

Each evidence baseline and published report has an immutable manifest containing stable identifiers, customer/project/environment scope, schema versions, content digests, provenance, data classifications, redaction markers, creation time, retention class, and references to inputs. Assessment run-input locks reference exact immutable versions. Mutable collaboration and current lifecycle state are recorded as append-only events plus current projections; generated originals remain unchanged.

Publication writes a frozen score snapshot and report manifest before the version becomes visible. Raw evidence, normalized evidence, findings, report artifacts, and audit records have distinct lifecycle classes. Deletion replaces references with an unavailable/deleted marker and reason; it does not falsify historical provenance. Cryptographic erasure and physical object removal follow approved retention policy, with legal or security holds represented explicitly.

## Rationale

This hybrid fits the product's query and reporting needs while making large evidence and report artifacts immutable. It avoids the cost of event-sourcing every entity and supports independent evidence lifecycle controls.

## Consequences

### Positive

- Historical assessments retain exact version references.
- Published reports cannot change when findings or profiles change.
- Evidence expiry remains visible without retaining prohibited payloads.
- Large payloads do not burden interactive relational queries.

### Negative and trade-offs

- Two storage systems require transactional coordination and reconciliation.
- Garbage collection must respect manifests, holds, soft deletion, backup policy, and shared references within a customer.
- Schema readers must remain compatible with historical manifests or provide versioned migration views.

## Security, operations, and cost impact

- Blobs are encrypted under the customer encryption context and accessed only through authorized server-side references.
- Malware/archive inspection precedes a blob becoming eligible evidence.
- Reconciliation detects orphan metadata, orphan blobs, digest mismatch, lifecycle drift, and unauthorized retention.
- Object-version and database backups must meet the pilot RPO/RTO; backup expiration remains an operations approval item.

## Migration and reversibility

Introduce manifests with explicit schema versions from the first pilot baseline. Storage providers remain behind metadata and blob interfaces. A provider migration copies and verifies blobs by customer-scoped digest before switching the manifest locator. Rollback keeps the prior provider read-only until verification and retention policy allow removal.

## Validation

- Tamper tests detect changed evidence or report content.
- Publication concurrency tests produce one immutable version per idempotency key.
- Retention tests independently expire raw, normalized, derived, and export classes.
- Restore tests re-establish manifest-to-blob integrity within approved recovery targets.
- Authorization tests show possession of a locator or digest does not grant access.

## References

- `specs/001-data-ingestion/product-spec.md` FR-ING-8 through FR-ING-10 and FR-ING-23 through FR-ING-25
- `specs/003-health-assessment/product-spec.md` FR-HAS-21, FR-HAS-23, FR-HAS-45, FR-HAS-46, and FR-HAS-50
- `product/non-functional-requirements.md` NFR-PRV-4, NFR-PRV-9 through NFR-PRV-11, NFR-EVD-1 through NFR-EVD-4, and NFR-REL-3 through NFR-REL-4

## Approval

Accepted by: Repository owner  
Date: 2026-09-28
