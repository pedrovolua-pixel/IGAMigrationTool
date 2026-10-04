# ADR-0012: Authentication failure audit preservation

Status: Accepted for bounded local Option A protocol prototype only; production outage decision remains open
Date: 2026-10-03 UTC
Decision owners: Technical/security, data-governance and operations owners
Scope: HTTPS production dependency review; no production implementation, deployment, grant or total-outage policy amendment

## Context

[The approved audit policy](../../docs/security/health-assessment-audit-policy.md) includes authentication failures and denials. [Local ADR-0009](ADR-0009-production-bff-authority-and-audit.md) preserves successful session/authority changes and their audit events in one PostgreSQL transaction. That transaction cannot persist failure events when its own store is unavailable. The current BFF authentication failure hooks return401 without durable product audit. Returning a safe refusal, stderr, telemetry and an ephemeral container file do not close this requirement.

A separate durable path can cover a PostgreSQL outage while it is available. It cannot guarantee recording every request when both durable stores, their identity provider or networking are unavailable. That correlated-failure case needs an explicit operational/security decision; no option below silently creates an audit-policy exception.

## Decision drivers

- Admission and successful mutations still require the existing atomic PostgreSQL audit transaction.
- Preserve failure/denial metadata without tokens, cookies, protocol bodies, claims, raw URL/query, evidence or free text.
- Separate writer, reconciliation, witness and lifecycle privileges; prove crash recovery, uncertain commits, duplicates and tampering.
- Preserve12-month event-time retention, soft deletion, purge within30additional days, approved holds, maximum35-day rolling backup and tombstone replay.
- Avoid accidental public storage, broad data roles, irreversible retention locks and an assumed affordable always-on service.

## Options considered

### Option A: Dedicated private Blob failure journal — recommended for a local prototype

A separately bound private journal stores one closed metadata object per server-generated attempt/event ID. Write only after schema validation; use conditional create, resolve an uncertain outcome by reading the exact object through a separately authorized reconciliation path, and treat a conflicting digest as integrity failure. Runtime journal capability is create-only under the reviewed contract; platform permissions must actually enforce the required boundary. Application code checking a condition does not establish protection against a compromised writer. No shared evidence/key container.

A reviewed reconciliation worker links each verified journal event to exactly one new canonical audit event and source receipt in a PostgreSQL transaction, without issuing a session, renewing provider evidence or replaying an OIDC callback. An independently retained witness detects missing/altered objects. Proposed detailed schema and failure tests are in [the production contract](../../docs/development/https-production-contract-proposal.md).

- Advantages: uses the selected Blob platform and avoids a new message broker/audit service; PostgreSQL outage and journal writes have distinct persistence paths.
- Disadvantages: new journal/witness/lifecycle access boundaries; non-atomic cross-store reconciliation; potentially large anonymous-event volume; both paths still share Azure/identity/network failure risks.
- Risks: create-only enforcement, immutable-object recovery, deletion/tombstone semantics and cost need proof. WORM is not selected: its duration/lock/hold can prevent required lifecycle actions.

### Option B: Independent durable audit service/store

- Advantages: can isolate writers and provide a dedicated admission receipt protocol and operational failure domain.
- Disadvantages: introduces another service, deployment/cost/identity/recovery boundary and a reviewed distributed protocol.
- Risks: no cross-service transaction; no guarantee during total audit-service failure. Full architecture and quote required before implementation.

### Option C: Exact outage policy amendment

An authorized human could define a precise, time-bounded exception and compensating incident controls for unauditable refusals when every durable path is unavailable. This would change approved policy.

- Advantages: makes the unsatisfied correlated-outage case explicit.
- Disadvantages: required events may be absent; existing policy cannot be described as satisfied.
- Risks: scope, detection, duration, authority and recovery evidence need separate approval. This proposal neither supplies nor accepts an exception.

Ordinary logs, memory queues, queue publish without durable receipt and container-local files are insufficient alternatives. Journal receipts never authorize an unaudited successful login or privileged action.

## Decision

Repository owner accepted the recommended Option A for a bounded local protocol prototype on 2026-10-03 UTC after packet `d518d25`. This acceptance permits local synthetic persistence/concurrency/reconciliation tests, not public admission or a policy amendment. Decide correlated total-audit outage handling separately before any live activation. Do not claim private Blob alone meets every production audit requirement.

## Security, operations and cost impact

Exact create/read/list/delete/hold/signing permissions, private endpoints, writer identities, witness destination and lifecycle operator are protected review inputs. No role or retention lock is installed. Anonymous volume and quotas must have a reviewed bound and overload behavior; dropping or sampling mandatory events is not assumed. All stores remain private and must be included in a fresh complete quote. No payload or actual environment identifier belongs in Git or the status board.

## Migration and reversibility

After separately approved local contracts, propose additive journal-source receipt metadata without rewriting existing migrations/canonical event v1. Reconciliation preserves source occurrence time separately because the existing audit appender stamps its ordered event after the stream lock. Do not backdate the canonical stream. Source-occurrence-based linked retention is the accepted local PC-D03 interpretation; canonical event timestamps remain the reconciliation time. Actual lifecycle metadata, read filtering, data-governance/live evidence and total-outage handling still need closure before lifecycle execution or a production compliance claim. Disable admission and preserve journal/canonical/witness/tombstone evidence during rollback. No destructive cleanup or key deletion is authorized.

## Validation

[AU01–AU10](../../docs/development/https-production-test-packet.md) cover available-store hook composition, anonymous minimization, late callbacks, uncertain outcomes, races, crash/recovery, retention/holds/restore, writer bypass and both stores unavailable. Actual cloud writer permissions/witness/restore/cost and human policy acceptance remain required before production.

## References

Microsoft sources checked2026-10-03 UTC; provider facts do not prove this application design:

- [Blob conditional headers](https://learn.microsoft.com/en-us/rest/api/storageservices/specifying-conditional-headers-for-blob-service-operations): exact ETag preconditions and conditional creation.
- [Immutable Blob storage](https://learn.microsoft.com/en-us/azure/storage/blobs/immutable-storage-overview): active time policies/holds prevent deletion; do not infer lifecycle compatibility.
- [Production handoff](../../docs/development/https-pilot-portal-next-packet.md).

## Approval

Accepted by: Repository owner, reply “Approved” after PC-D01–PC-D05 review packet
Date: 2026-10-03 UTC
Scope: Local Option A prototype and proposed linked source-occurrence interpretation only; no total-outage policy exception, live compliance, grants or release. [Approval and dependencies](../../plans/active/https-production-local-cycle01.md).
