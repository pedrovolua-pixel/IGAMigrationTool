# Audit Policy: Health-Assessment Pilot

Status: Approved  
Owner: Security, data-governance and operations owners  
Last updated: 2026-09-28

## Approved retention

Pilot audit records are retained for 12 months from the event timestamp. Audit records are payload-free: they may identify actors, workloads, opaque customer/project/resource IDs, action, policy decision, outcome, reason category, correlation, artifact/version identifiers and security-relevant metadata, but must not contain evidence values, prompts, model responses, comments, report contents, credentials, tokens, passcodes, keys, connection strings, government identifiers or other protected payload.

At 12 months, records are soft-deleted and unavailable to ordinary reads. Active-system purge completes within 30 additional days unless an approved contractual, legal or security hold applies. Backup expiration follows the approved 35-day maximum rolling policy, and restore replays audit-deletion tombstones before access.

## Event scope

- Authentication/session creation, failure, revocation and privileged reauthentication.
- Partner/customer/project/environment assignment and role changes.
- Policy decisions for evidence, AI, export, sharing, deletion, MCP and support access.
- Connection/permission tests and the excess-read-only warning or write/admin block.
- Assessment configuration, locked inputs, start/cancel/retry/checkpoint/completion and budget override.
- Rule/profile/prompt/model/catalog promotion, suspension and use.
- Finding edits, dispositions, risk acceptance, comments and validation state changes.
- Fix packages, tasks, CSV exports, report publication, acknowledgment and link creation/access/revocation/expiry.
- Retention, soft deletion, purge, backup, restore and recovery decisions.
- Security control failures, integrity mismatch and operator actions.

## Integrity and minimization

- Application roles can append only through the audit interface and cannot update/delete existing events.
- Each event has a schema version, trusted timestamp, unique event ID, actor/workload identity, scope, correlation and outcome.
- Integrity monitoring detects gaps, unexpected writers, ordering/time anomalies and unauthorized lifecycle changes.
- Ordinary application logs are not the audit system of record.
- Payload-schema negative tests fail builds when prohibited fields or values reach audit serialization.
- Free-text reasons are stored only where product requirements demand them, are length/classification constrained and are never copied into security telemetry.

## Access

- Customer auditors may read customer-scoped business audit history for approved reports, rules, provenance, dispositions, risk decisions, acknowledgments and exports.
- Consultants and customer reviewers see only the project-scoped history required for actions they are authorized to perform.
- Security audit detail, detection logic, cross-customer operational metadata and internal incident records are restricted to authorized security/operations roles.
- A customer receives its scoped security-event disclosure through the approved incident or contractual process; no view may expose another customer or detection secrets.
- Platform support has no protected-evidence access, and audit access does not grant evidence access.

## Holds and deletion

A hold requires authorized owner, legal/security basis, exact customer/resource/event categories, start, review/expiry and audit trail. Holds do not make records visible to otherwise unauthorized roles. Release of a hold resumes the original lifecycle rather than resetting retention.

## Verification

- Retention boundary, soft deletion, 30-day active purge and backup/tombstone replay.
- Append-only enforcement, restricted writer/reader identities and integrity alerts.
- Customer/project isolation and business/security field filtering.
- Prohibited-payload corpus across every event producer.
- Clock/time-zone consistency and correlation through API, queue, worker, AI, renderer, share and MCP paths.
- Restore proves expired audit records and revoked access are not resurrected.

## Approval

Approved by: Repository owner  
Date: 2026-09-28
