# Product Principles

Status: Approved
Approved by: Product owner
Approved: 2026-09-26
Last updated: 2026-09-26

These principles were derived from and approved with the product discovery baseline.

## Outcomes over artifact copying

Preserve or improve the approved governance outcome. Do not assume source objects should be copied literally into a destination with different concepts.

## Evidence before conclusions

Every discovered use case, requirement, health finding, migration action, and validation result should identify its evidence and confidence. Missing or inaccessible evidence must remain visible.

## Assessment independent of migration

Health assessment applies to every collected object and supported evidence category regardless of whether the customer selects it for migration. Excluding an object from migration must not hide its health findings.

## Human-controlled intent and risk

The product may collect, analyze, draft, recommend, and prepare changes. AI suggestions are refined by the consulting team and approved by the customer unless the customer has explicitly configured an allowed approval exception. The system must never interpret a skipped approval as AI authority to approve its own recommendation.

Configured approval exceptions must identify their owner, scope, conditions, and audit history. Per-action customer approval may be omitted for read-only collection, documentation generation, health analysis, and migration-package generation when authorized by customer policy. Direct destination configuration and data movement continue to require customer approval.

## Traceability end to end

Maintain a navigable relationship among source evidence, documented current behavior, approved future behavior, migration work, destination configuration, and validation evidence.

## Secure by default

Minimize collected data and privileges, protect secrets and sensitive identity information, separate customers and environments, and avoid exposing sensitive values in reports or logs.

## Reversible and resumable execution

Migration actions should be reviewable, idempotent or safely retryable where possible, and designed around partial failure, recovery, and clear operator control.

## Source remains read-only

Collection, analysis, migration, and validation must never modify SailPoint. All source interaction is read-only.

## Do not automate uncertainty away

Surface ambiguity, unsupported mappings, conflicting evidence, and confidence levels. Route consequential uncertainty to a qualified reviewer rather than silently guessing.

## Product-specific fidelity

Use a common workflow where it adds clarity, but preserve meaningful differences among SailPoint, Veza, and One Identity Manager instead of forcing false equivalence.

Evaluate migration fidelity using approved business/governance behavior and meaningful configuration similarity, not literal artifact identity.
