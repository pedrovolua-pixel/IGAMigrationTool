# Operations and Recovery Plan: Health-Assessment Pilot

Status: Approved — implementation and recovery evidence pending  
Owner: Operations owner  
Reviewers: Technical owner, security owner, data-governance owner  
Last updated: 2026-09-29

## Scope

This plan covers product-managed pilot configuration, normalized evidence, assessment runs, findings, tasks, reports, audit history, immutable blobs, queues and customer data-plane routing. It does not cover rollback or restoration of a customer One Identity environment because the pilot never changes that environment.

Pilot objectives are a recovery-point objective of 24 hours and a recovery-time objective of one business day for recoverable incidents within the selected single Azure region. The pilot has no recovery region, cross-region standby, geo-redundant database backup or regional-outage RTO. A region-wide Azure outage invokes the manual best-effort business disaster plan. Support operates 9:00 a.m.–5:00 p.m. Eastern Time on United States business days, with initial-response targets of four business hours for critical incidents, one business day for high-priority incidents, and three business days for normal incidents.

## Service and job states

Operators must see API/UI availability, control-plane health, per-customer data-plane reachability, queue age/depth, worker saturation, run/checkpoint age, rule/AI/render failures, storage integrity, link failures, deletion/purge backlog, backup success and restore-drill evidence without seeing customer payload values.

Customer-visible run states and gap explanations remain canonical. Operators may retry idempotent work, pause new work, drain workers, revoke links, or invoke approved recovery procedures. Investigation does not authorize evidence access, data mutation outside the procedure, retention override, risk acceptance or customer-system changes.

## Service Bus Standard operating profile

Service Bus Standard is a delivery mechanism, not the work-state source of truth. Canonical work state, authorization scope, checkpoint and outcome remain in PostgreSQL. Messages contain opaque identifiers and versions only. A transactional outbox publishes committed work, and a reconciliation process safely re-publishes missing delivery signals after broker or dispatcher failure.

The Standard-tier public endpoint is allowed only with local/SAS authentication disabled, Entra managed-identity data roles, approved TLS, controlled static application egress and denial of developer-workstation or broad-network access. Operations must alert on authentication/network denial, unusual connection source, queue age/depth, expiry, lock loss, retry, dead-letter and outbox-reconciliation lag. Message bodies are excluded from logs and diagnostic capture.

Runbooks must cover broker outage, outbox backlog, duplicate delivery, lock loss, expired work, poison/dead-letter replay, unauthorized connection attempts and Standard shared-capacity saturation. Replay revalidates current work authorization/state and must not create duplicate domain results.

## Non-HA PostgreSQL operating profile

The pilot uses one single-primary Azure Database for PostgreSQL Flexible Server with high availability disabled in one Azure region. A recoverable database or zone outage may interrupt all pilot customers; this is an approved pilot cost/availability trade-off and does not alter the same-region 24-hour RPO or one-business-day RTO. APIs and workers must fail safely, stop acknowledging new work that cannot be checkpointed, preserve queued delivery signals and resume idempotently after database recovery. A region-wide outage is outside those objectives.

Automated backup and point-in-time recovery remain mandatory. Readiness requires an isolated restore drill, customer-database extraction, integrity reconciliation, deletion/access tombstone replay and restoration of queue/outbox consistency. Production release or a tighter availability objective requires a new capacity/availability review and may enable zone-redundant HA or customer-dedicated servers.

## Deployment sequence

1. Verify approved change, artifact digests, dependency/SBOM and secret scan.
2. Confirm current backup and last successful restore evidence.
3. Pause new work types affected by incompatible changes; preserve interactive reads where safe.
4. Apply expand-only storage changes to control and customer data planes.
5. Deploy readers/policy service compatible with old and new shapes.
6. Deploy workers; route only compatible job versions and drain old workers.
7. Deploy write/orchestration paths and UI.
8. Enable sub-phase capability flags for an internal validation customer only.
9. Run smoke, authorization, queue/checkpoint, render and observability checks.
10. Expand rollout by approved customer/environment and monitor the defined signals.

Contract changes use expand/migrate/contract. Contraction waits until all data planes, workers, reports and queued payloads no longer depend on the old shape and a verified restore can read the retained version.

## Rollback

Rollback triggers include cross-customer authorization failure, integrity/digest failure, data loss/corruption, source-write capability, systemic rule/score error, uncontrolled evidence disclosure, unsafe render/share behavior, deletion bypass, or material availability regression.

Rollback steps:

1. Disable affected capability flags and stop new affected jobs.
2. Revoke newly created share links if their controls are suspect.
3. Drain or cancel at the next safe checkpoint; do not delete successful evidence/results.
4. Revert application/workers only if their prior versions support the current schemas and job payloads.
5. If not compatible, keep writes paused and deploy a forward repair; never force a destructive schema rollback.
6. Validate authorization, integrity, checkpoints and customer-visible state.
7. Notify affected owners under the incident process and preserve audit evidence.

Database restore is not an ordinary application rollback. It is used only for verified loss/corruption under the recovery procedure.

## Backup design

Backups must include control-plane routing/assignments, each customer data plane, immutable blob manifests/content, configuration/rule/profile/prompt artifacts, audit records, deletion ledger and encryption-key references needed for restoration. Secrets and keys follow their managed recovery process and are never copied into ordinary backups or runbooks.

Required controls:

- encrypted, access-controlled and customer-bound backup sets in the approved US region;
- no geo-redundant pilot backup or cross-region standby; regional-disaster recovery is a manual business-plan concern;
- at least daily recovery points to meet the 24-hour RPO;
- integrity checks and inventory reconciliation for relational metadata and blobs;
- protected deletion/tombstone ledger included in every recovery set;
- immutable backup-job audit and failure alerting;
- quarterly pilot restore drills before and during operation, plus a drill after material storage/schema change;
- recovery into an isolated environment before cutover, with customer isolation and authorization checks.

### Approved backup-expiration policy

Active-system data is soft-deleted at retention expiry and purged within 30 additional days. The approved pilot backup policy is:

- rolling backup retention no longer than 35 days unless an approved contractual/legal hold requires otherwise;
- deleted-data tombstones retained long enough to cover the longest backup plus restore validation window;
- any restore replays tombstones, revoked assignments, expired/revoked links and purge decisions before customer access or background processing is enabled;
- backup media expiration is cryptographically and operationally verified, with exceptions attributed and reviewed.

The repository owner approved this backup-expiration policy on 2026-09-28. A contractual or legal hold is an explicit exception requiring its own authority, scope, expiry and audit record.

## Restore procedure

Prerequisites: declared incident, recovery owner, exact customer/data-plane scope, approved recovery point, integrity evidence, key availability and authorization to restore.

1. Provision an isolated recovery target in the selected pilot region.
2. Restore control metadata needed to resolve only the affected customer, then restore its data plane and blob namespace.
3. Verify database and blob digests/manifests, schema versions, audit continuity and rule/profile/report artifacts.
4. Replay deletion/tombstone ledger, revoked assignments, expired/revoked links, customer policy changes and capability suspensions newer than the recovery point.
5. Run cross-customer, deleted-resource, link-revocation, protected-evidence and worker-scope tests.
6. Reconcile runs, checkpoints, findings, reports, tasks, audits and blob references; quarantine any mismatch.
7. Measure data loss against the 24-hour RPO and elapsed recovery against the one-business-day RTO for the same-region drill.
8. Obtain recovery-owner and security verification before traffic/jobs resume.
9. Record evidence, customer impact, unrecovered data and follow-up actions.

The procedure must prove that a restore does not resurrect an expired share link, deleted evidence, revoked user assignment, suspended rule/capability or cancelled job.

## Business disaster plan boundary

A region-wide Azure outage invokes a separately maintained manual business disaster plan. The plan identifies decision authority, customer and owner communications, service/dependency inventory, last known backup and artifact evidence, source-repository and infrastructure reconstruction inputs, secrets/key recovery dependencies, alternate-US-region selection criteria, security review and validation before any recovered service is exposed. It is exercised as a tabletop before pilot execution and after a material platform change.

The business disaster plan is best-effort for the pilot. It does not promise a recovery region, pre-provisioned capacity, cross-region data availability, a 24-hour RPO or a one-business-day RTO. Any future contractual regional recovery objective requires a new approved architecture, data-protection design, cost model and recovery test plan.

## Retention, soft deletion and purge

- The configured customer event starts each lifecycle clock; the default is 30 days.
- Soft deletion atomically denies ordinary reads/processing, revokes links, prevents new work and writes a deletion-ledger event.
- Purge work is idempotent, dependency ordered and separately authorized. It removes exports/links, raw evidence, normalized evidence, derived artifacts and customer resources as policy permits.
- A failed purge leaves data inaccessible and alerts before the 30-day active-purge deadline.
- Holds are explicit, narrowly scoped, authorized, expiring and auditable; they do not silently change customer policy.
- Metrics expose counts/ages by opaque customer data-plane identifier without payload or customer-sensitive labels.

## Monitoring and alerts

| Signal | Alert condition | Initial response |
|---|---|---|
| Cross-scope authorization denial | Baseline anomaly or repeated direct-ID attempts | Security triage; do not reveal resource existence |
| Data-plane routing mismatch | Any invariant failure | Critical: disable affected reads/writes and investigate |
| Manifest/blob digest mismatch | Any | Quarantine artifact/baseline; integrity incident |
| Source permission block | Any write/DDL/owner/admin capability | Stop collection; security/customer notification |
| Queue/checkpoint age | Threatens eight-hour assessment target or configured SLO | Scale/drain/retry after dependency check |
| Rule/AI failure rate | Systemic version-specific threshold | Suspend affected version for new work; preserve history |
| Render digest mismatch/sandbox event | Any | Disable publication/share renderer; security review |
| Share-link failure anomaly | Brute-force/rate threshold | Lock/revoke and investigate |
| Backup failure | Missed recovery point threatens 24-hour RPO | Critical operations response |
| Restore drill failure | Any objective/integrity/auth failure | Pilot readiness fails until corrected and repeated |
| Purge backlog | Any item risks 30-day post-soft-delete deadline | Escalate to operations/security/data owner |

Threshold values depend on selected infrastructure and load evidence and must be recorded before staging.

## Incident priorities

- **Critical:** confirmed/suspected cross-customer exposure, source-write path, credential/key exposure, material integrity loss, backup gap beyond RPO, active deletion bypass, or widespread unavailability.
- **High:** single-customer protected-data exposure, failed restore drill, systemic incorrect assessment/publication, share/render security failure, or assessment fleet unable to meet pilot operation.
- **Normal:** isolated retryable job/rule/render issue, non-security UI defect, documentation or low-impact alert.

Incident records include correlation IDs, affected opaque scopes, time, detection, classification, containment, evidence, customer communication owner, recovery, verification and follow-up. They exclude customer payload values unless separately authorized in the incident evidence store.

## Readiness evidence

- Concrete infrastructure inventory and data-flow diagram.
- Successful deployment/rollback rehearsal.
- Daily backup evidence and isolated restore drill meeting RPO/RTO.
- Business-disaster tabletop for a region-wide Azure outage, explicitly reported without a regional RPO/RTO claim.
- Retention/backup-expiration approval and purge rehearsal.
- Monitoring dashboards/alerts with synthetic evidence.
- On-call/support ownership and escalation contacts.
- Runbooks for authorization incident, integrity mismatch, non-HA database outage/restore, Service Bus outage/public-endpoint anomaly/outbox or queue stall, AI provider outage, renderer/share failure, purge failure and key/credential incident.
- Capacity evidence at approved independent category scale.

## Approval

Operations owner: Repository owner  
Security owner: Repository owner  
Data-governance owner: Repository owner  
Date: 2026-09-28
