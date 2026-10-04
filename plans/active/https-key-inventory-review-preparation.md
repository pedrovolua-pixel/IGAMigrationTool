# Shared-key inventory decision and production input preparation

Status: VERIFIED — documentary preparation only; architecture review remains open
Date: 2026-10-03 UTC
Owner: Coordinator
Baseline: bc65859
Authority: owner “approved, next step”; repository rule3 requires a proposed ADR for consequential architecture. [Accepted local PC-D02](https-production-local-cycle01.md) leaves protected monotonic inventory/witness, lifecycle/limits and actual bindings unresolved. The [key contract](../../docs/development/https-production-key-contract-proposal.md) states these prerequisites; [binding intake](../../docs/development/bff-production-bindings-template.json) remains protected and unfilled.

## Scope

Prepare a concrete reviewable architectural decision and precise production input checklist while the independent [resolver pipeline diagnostic](https-key-resolver-pipeline-diagnostic.md) runs. No architecture is accepted or implemented by this packet. Do not turn unspecified values into defaults or treat general continuation approval as acceptance of a new key policy.

Isolated writing worker owns only architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md and docs/development/https-key-production-input-checklist.md. Coordinator owns ADR index, canonical plan/status/test/evidence and six-task board delta. A different worker reviews the immutable documents against approved policy. No code/configuration/dependency/template/schema/grant/cloud/publisher/source export change.

The proposed ADR must explain why Blob ETags cannot establish monotonic inventory after privileged restore; distinguish authority, durable storage, independent witness and lifecycle owners; compare bounded feasible alternatives with rollback/concurrency/unknown-acknowledgment/restore failure behavior and costs/permissions left for review. It may recommend an explicitly proposed approach only when supported by the existing platform constraints. No new service/role, persistence protocol or cryptographic format is accepted by writing the ADR. Do not invent witness identity, polling/retry/deadline/size/key-generation/retention/deletion numbers. Mark unresolved exact mechanics/authorities and necessary human decisions explicitly. Preserve existing audit/session/backup policies and no total-outage exception.

The input checklist must group exact technical/security/operations decisions needed before production guards separately from protected deployment bindings/live evidence. Give each input the requested role, canonical source or existing template field, and completion condition. State existing approved quantities separately; seven-day disposable vault/database and30-day disabled-monitoring settings are not shared-key policy. Never include actual identifiers, users, network ranges, credentials, customer data or raw SQL. Reuse the current protected intake rather than adding an unapproved public contract.

## Documentary verification

INV-P01: demonstrate accepted architecture/policy alignment and proposed-only status. INV-P02: trace inventory/witness/restore/acknowledgment boundaries and unresolved assumptions without an invented guarantee. INV-P03: distinguish all unspecified key quantities and named owners from already approved unrelated policies. INV-P04: every checklist row links a canonical requirement/template field and identifies role/completion. INV-P05: UTF-8, Markdown links/anchors, whitespace, protected prose/secret scan and independent non-author review. These are preparation checks, not implemented inventory or production KEY evidence. No runtime/full-product regressions apply to documents-only changes.

Coordinator integrates only independently reviewed documents and updates canonical records and private human-task delta. All live/gate obligations remain open. Private BFF/HTTPS snapshot4346e0f is stale under the separate automatic-review-rejected publisher proxy-bypass; no retry/bypass or credential request. Preserve unrelated Cycle14 publication. Exact new architecture decisions require attributed human approval after this reviewable packet exists.

## Documentary checkpoint — 2026-10-04 UTC

Corrected immutable candidate66ca09a9d4438bcf4fca429a1bf15daaecef4e8d (initial8bd16ab retained) passed non-author INV-P01–05 review and was integrated as42b7891/2b312fe with identical two-document hashes. The review checked41 local links/13 heading anchors,42 exact existing null intake fields, seven technical/nine live input rows, strict UTF-8/newlines, proposed-only and protected prose, committed whitespace and Gitleaks. Original15-minute wording was corrected against the canonical identity design to distinguish ordinary sessions, transaction admission, protected-action authentication and provider freshness; this changes no policy. [Source-bound documentary receipt](../../docs/development/evidence/https-key-inventory-preparation-20261004.json) retains initial/corrected sources, reviewer evidence hashes and limits.

[ADR-0013](../../architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md) remains Proposed/unselected. [Checklist](../../docs/development/https-key-production-input-checklist.md) is a technical review/input aid, not supplied values or architecture approval. No runtime/product test, new KEY case, grant, spend, Azure operation, production dependency or schema was executed by this packet. All six human tasks and G1–G9/Milestone2 remain open; private BFF/HTTPS publisher block persists.
