# Non-Functional Requirements

Status: Approved
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

Quantitative targets require validation against customer expectations, data volumes, deployment model, and contractual obligations. Items marked `TBD` must be resolved before technical approval for affected features.

## Security and least privilege

- NFR-SEC-1: Source and destination access must use the minimum privileges required for the approved operation. For the One Identity health pilot only, detected excess read-only database permissions may proceed with a prominent warning and audit event; write, DDL, ownership, or administrative capability must block collection. The product and security owner approved this narrowly scoped exception.
- NFR-SEC-2: Collection privileges and destination-changing privileges must be separable.
- NFR-SEC-3: Secrets must not be stored in source control, reports, ordinary logs, or generated delivery artifacts.
- NFR-SEC-4: Sensitive data must be protected in transit and at rest using approved controls.
- NFR-SEC-5: Authorization must be enforced for every engagement-scoped read, decision, approval, export, and execution operation.
- NFR-SEC-6: Customer or engagement data must not be exposed across isolation boundaries.
- NFR-SEC-7: Security-relevant operations and approvals must be auditable.
- NFR-SEC-8: The product must support safe redaction or minimization of sensitive values in evidence and reports.
- NFR-SEC-9: Source credentials and passwords must not be ingested as evidence or emitted in analysis data, reports, generated artifacts, or ordinary logs.
- NFR-SEC-10: Authorization and isolation must account for partner, customer organization, engagement, and environment boundaries; access through a consulting partner must not imply access to its other customers.

## Privacy and data governance

- NFR-PRV-1: Collection must be limited to evidence needed for the approved engagement scope.
- NFR-PRV-2: Data classes, residency constraints, retention periods, deletion expectations, and export rules must be defined before production use. Targets: `TBD`.
- NFR-PRV-3: Reports and visualizations must avoid exposing personal or privileged information beyond the viewer's authorization.
- NFR-PRV-4: The product must distinguish raw evidence from derived findings so retention and deletion policies can be applied appropriately.
- NFR-PRV-5: Evidence-handling policy must be enforceable by category and field so designated information can remain customer-side or be redacted before crossing the approved customer boundary.
- NFR-PRV-6: The system must make the applied redaction or customer-side retention rule visible without revealing the protected value.
- NFR-PRV-7: Social Security numbers and government identifiers must remain inside the customer-controlled boundary or be redacted before approved evidence leaves it.
- NFR-PRV-8: Customers must be able to select a customer-side collection mode; choosing hosted collection must not weaken the configured exclusion and redaction rules.
- NFR-PRV-9: Supported data residency, retention, deletion, and export controls must be configurable per customer and enforceable across canonical data, evidence, tasks, generated reports, and backups where applicable.
- NFR-PRV-10: The default customer-data retention period is 30 days unless an authorized customer configures another supported policy. The event that starts the retention clock must be customer-configurable.
- NFR-PRV-11: At retention expiry, customer data must be soft-deleted and become unavailable to ordinary users and processing. For the pilot, active-system data must be permanently purged within 30 additional days; backup expiration is documented separately. Authorized recovery and audit visibility must remain controlled and documented.

## Evidence integrity and traceability

- NFR-EVD-1: Evidence must retain provenance, collection time, engagement, and source context.
- NFR-EVD-2: Generated conclusions must be distinguishable from user-confirmed facts and approved decisions.
- NFR-EVD-3: Material edits, approvals, executions, and validation results must retain attributable history.
- NFR-EVD-4: Reports must indicate incomplete, stale, inaccessible, or unsupported evidence.
- NFR-EVD-5: Internal and external consultant tasks must remain traceable to their originating evidence, warning, migration object, automated suggestion, and human resolution.
- NFR-EVD-6: In-flight campaign warnings and customer-selected certification exclusions must remain visible in migration reports without being misrepresented as consultant tasks or completed migration work.
- NFR-EVD-7: Migration exclusions must not remove collected objects, applicable checks, findings, or evidence from health-assessment reporting.
- NFR-EVD-8: ROI calculations must expose inputs, assumptions, time horizon, uncertainty, excluded costs/benefits, and evidence sources so sponsors can evaluate the analysis.
- NFR-EVD-9: Dashboard, PDF, and Markdown outputs for the same assessment must identify the same evidence baseline, generation time, approval state, and material findings.
- NFR-EVD-10: The canonical in-product record must identify the source version of every generated PDF or Markdown export and must remain authoritative when an export becomes stale.

## Safety and change control

- NFR-SAF-1: Read-only discovery must be the default until destination-changing scope is explicitly approved.
- NFR-SAF-2: Destination changes must be previewable and tied to an approved, versioned change set.
- NFR-SAF-3: Operations must expose partial failure and must not report overall success when required work is incomplete.
- NFR-SAF-4: Retried or resumed operations must avoid silent duplicate effects where the destination permits.
- NFR-SAF-5: Irreversible or destructive operations require explicit identification and separate authorization.
- NFR-SAF-6: Before any remediation or migration operation that changes a customer system is enabled, its rollback or compensating-recovery behavior, limitations, approval requirements, failure handling, and verification evidence must be defined and tested for the supported execution path.
- NFR-SAF-7: A configured approval exception must be narrowly scoped to read-only collection, documentation generation, health analysis, or migration-package generation; it must be attributable to an authorized customer decision, reviewable, revocable, and auditable.
- NFR-SAF-8: Direct destination configuration and data movement must require customer approval and cannot inherit an approval exception for read-only or generative activities.
- NFR-SAF-9: All SailPoint access must be technically constrained to read-only behavior; migration, validation, retry, and recovery must not introduce a source-write path.
- NFR-SAF-10: Identity matching must expose the rule and evidence used, surface ambiguous matches, and avoid silently selecting among competing identities.
- NFR-SAF-11: Access Profile acceptance must not be permitted while an approved entitlement mismatch remains unresolved, regardless of aggregate validation score.
- NFR-SAF-12: In-flight SailPoint certification rounds must remain read-only and must not be interrupted or represented as migrated. Their campaign settings may be migrated, but the corresponding destination Access Review must not be triggered until the source round finishes.
- NFR-SAF-13: Access Review acceptance must not be permitted while an approved scope or reviewer mismatch remains unresolved, regardless of aggregate validation score.
- NFR-SAF-14: In-flight SailPoint lifecycle executions must remain read-only and authoritative until completion; corresponding Veza Workflows must not be triggered while the source execution is active.
- NFR-SAF-15: A workflow that references a system unsupported by Veza must not be accepted as successfully migrated, regardless of aggregate validation score.
- NFR-SAF-16: A recommendation or fix package that does not execute a customer-system change must document applicable rollback, recovery, and validation guidance without representing that guidance as an executed or verified restore of the customer system.

## Reliability and recoverability

- NFR-REL-1: Long-running collection, assessment, migration, and validation work must be resumable or safely restartable where feasible.
- NFR-REL-2: The system must preserve completed evidence and actionable error context when one item or dependency fails.
- NFR-REL-3: Product-managed pilot data has a 24-hour recovery-point objective and one-business-day recovery-time objective. Later production targets require validation against contractual obligations.
- NFR-REL-4: Before any product phase operates as a production service, backup and restoration procedures for product-managed customer data, configuration, evidence, findings, reports, tasks, and audit history must be documented and verified against approved recovery targets. This requirement concerns recovery of the product service and does not verify rollback of customer-system changes.
- NFR-REL-5: Pilot support operates 9:00 a.m. to 5:00 p.m. Eastern Time on United States business days. Initial response targets are four business hours for critical incidents, one business day for high-priority issues, and three business days for normal issues. Planned-maintenance and after-hours behavior remain to be defined before production release.

## Performance and scale

- NFR-PER-1: Initial per-customer scale must support at least 100,000 identities, 100,000 accounts, 100,000 entitlements, 100,000 roles, 100,000 workflows, and 100,000 records within the configurable 90-day operational-evidence window. Each category is measured independently; a pilot customer need not contain every category at that volume. Separate Access Profile, policy, event, and engagement-count targets remain to be defined.
- NFR-PER-2: Collection and migration must respect supported source and destination service limits.
- NFR-PER-3: For the pilot, common dashboard and filter interactions target two seconds at p95, detailed evidence views target three seconds, and asynchronous assessments must expose progress and checkpoints and target completion within eight hours at approved scale.
- NFR-PER-4: Large inventories must remain navigable without requiring users to load or inspect all raw evidence at once.

## Explainability and usability

- NFR-EXP-1: Findings, mappings, generated requirements, and validation conclusions must expose evidence and reasoning sufficient for qualified human review.
- NFR-EXP-2: Confidence or uncertainty must be visible where the system infers meaning.
- NFR-EXP-3: The product must use language and representations understandable to the intended IGA practitioner and product-owner audiences.
- NFR-EXP-4: Graphical views must have an accessible non-graphical representation of the same material information.
- NFR-EXP-5: Validation grades must expose their weighted component results, evidence, missing information, percentage, red/yellow/green status, and scoring basis; a summary must not conceal a failed or unverified component.
- NFR-EXP-6: Color must not be the only means of conveying validation status.
- NFR-EXP-7: Health reports must provide an executive summary and progressively disclose technical evidence so sponsors and practitioners can use the same canonical report.
- NFR-EXP-8: Severity and AI confidence must be shown separately; confidence must not reduce the stated severity of a supported risk.
- NFR-EXP-9: Markdown exports must use stable identifiers and structured, machine-readable conventions sufficient for agent integrations without making the Markdown a second source of truth.

## Accessibility

- NFR-ACC-1: The pilot accessibility target is WCAG 2.2 AA.
- NFR-ACC-2: Keyboard operation, focus behavior, labels, contrast, and alternatives for graphical content must be verified for user-facing workflows.

## Observability and supportability

- NFR-OBS-1: Operators must be able to determine the state and outcome of collection, analysis, migration, and validation work without exposing sensitive payloads.
- NFR-OBS-2: Failures must include correlation context and actionable diagnostics suitable for authorized support staff.
- NFR-OBS-3: Health, error, throughput, backlog, dependency, and change-outcome signals must be available where relevant.
- NFR-OBS-4: Monitoring and alerts must distinguish customer/environment context while preserving isolation and privacy.

## Compatibility and maintainability

- NFR-CMP-1: Supported source and destination product versions or service editions must be explicit.
- NFR-CMP-2: Product-specific mappings and checks must be versioned so changing vendor behavior does not silently reinterpret prior engagements.
- NFR-CMP-3: Unsupported concepts or versions must fail visibly rather than be approximated without disclosure.
- NFR-CMP-4: Common workflow concepts must not erase meaningful destination-specific semantics.

## Compliance and assurance

- NFR-COM-1: The pilot applies baseline security controls plus documented customer contractual requirements and must not claim formal regulatory or compliance certification. Later certification targets remain to be defined.
- NFR-COM-2: Production readiness must include a threat model, access-control design, data-flow inventory, retention policy, and incident process. Platform backup and restoration are governed separately by NFR-REL-3 and NFR-REL-4; recommendation guidance is governed by NFR-SAF-16; executed remediation and migration rollback are governed by NFR-SAF-6.
- NFR-COM-3: Initial customer-data residency is limited to the United States. Additional regions require explicit product, legal, security, and operational review.
