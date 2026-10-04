# ADR-0009: Production BFF authority and atomic security audit

Status: Accepted for local synthetic implementation only
Date: 2026-10-02
Decision owners: Technical/security owner; operations and identity/platform owners

## Context

Accepted ADR-0002/0004 establish a shared PostgreSQL control plane, product-scoped authorization, Entra BFF identity and Key Vault/managed identities. Approved local D01/D02 implements transport and exact-session authentication without production enrollment/provider adapters or durable audit. Production composition needs durable authority records, shared protection keys and an append-only event in the same commit as session/authority mutation.

## Decision drivers

- Preserve no automatic enrollment, per-request authoritative product checks and exact security-version/session binding.
- Enforce payload-free audit with the existing 12-month lifecycle and separate reader/administration identities.
- Avoid admitted sessions after required audit failure, and preserve revocation during uncertain commits or retries.
- Retain protected-data network isolation and the approved managed-identity platform without another distributed service.

## Options considered

### Option A: Module-owned control-plane authority and atomic audit

Use explicit module-owned versioned authority rows and narrowly callable PostgreSQL transaction functions for subject/assignment/session changes plus append-only security events. Bind shared Blob/Data Protection keys to one reviewed environment and wrap them using a dedicated Key Vault key.

- Advantages: one durable commit and recovery boundary for authority, ticket and required audit event; enforceable transaction invariants; uses accepted PostgreSQL platform.
- Disadvantages: security-event identifiers share the control-plane recovery domain; roles/functions and12-month audit restore/deletion controls need independent tests. The BFF transaction identity has limited audit-insert capability via trusted functions rather than an independently deployed writer.
- Risks: a privileged database operator can bypass application append restrictions; integrity monitoring, recovery and separation of lifecycle identities remain required.

### Option B: Separate durable audit service with compensation

- Advantages: distinct writer/storage boundary and independent operational isolation.
- Disadvantages: there is no single transaction across session and audit stores; uncertain outcome, compensation and retries need another reviewed durable protocol.
- Risks: logging before ticket commit can report nonexistent sessions; logging after commit can leave an unaudited admitted ticket. Best-effort logs or queue publish cannot meet the approved audit requirement.

### Option C: Independent cached authority and ordinary application logs

- Advantages: fewer implementation steps.
- Disadvantages: cannot provide atomic audited lifecycle changes or authoritative immediate product revocation; ordinary logs are not the approved audit system of record.
- Risks: contradicts approved controls; not a viable production option.

## Decision

**Accepted for local synthetic implementation:** Option A, with exact schemas, boundaries and tests in [the D03/D04 proposal](../../docs/development/bff-production-authority-hosting-proposal.md). The repository owner accepted P01–P05 and this ADR on 2026-10-02 against immutable packet `cacbe372ff8a9bc448033e6918308c5f820b4bfd`. Actual provider/Azure/SQL grants, production activation, audit-outage preservation and paid deployment remain separate.

## Rationale

Option A can satisfy the accepted audit/authority invariants using the existing control-plane database. Its constrained functions are a consequential new boundary requiring exact approval and independent database-role/transaction/recovery proof. This proposal does not claim that audit insertion permissions have already been granted or that the current session methods are atomic with audit.

## Consequences

### Positive

- Session issuance, explicit revoke and authority revision have durable event binding.
- Typed internal commands and idempotency keys permit reconciliation without inventing new public routes.
- Shared keys survive replica changes; product revocation remains independent of cached cryptographic keys.

### Negative and trade-offs

- New additive schemas, role/function ownership and audit lifecycle/restore implementation are required after approval.
- Provider data remains eventually consistent; product authority must handle known changes immediately. Provider reads cannot establish a stronger revocation guarantee than Microsoft supplies.
- Production admission still needs actual property/role/guest/CA/federation and trusted-ingress proof. Local synthetic results cannot replace it.

## Security, operations, and cost impact

- No new grants, Graph consent, database operations, Azure resources or paid session are part of acceptance for local implementation.
- Shared key retention must cover all protected stored data and its recoverable backups, not only browser cookie lifetime. No unapproved key deletion schedule is selected.
- Live network topology, key/blob/SQL scopes, image acceptance, named users and priced session require exact protected deployment bindings and their own approval.
- The existing audit lifecycle remains12-month retention, soft delete, active purge within30additional days, holds and maximum35-day rolling backup/tombstone replay. Restore must not resurrect authority or revoked sessions.

## Migration and reversibility

Prepare additive local migrations only after decision acceptance; do not run them at host startup. Keep existing 001/002 history intact. Missing authority, audit, key or proxy evidence denies. Rollback first disables admission and drains production replicas, preserves durable audit/authority records and keys, then uses reviewed schema compatibility. Do not drop audit data or revert security versions to restore an older application.

## Validation

Use the proposal's exact local test matrix, non-author review and scoped role-negative tests. Live activation separately requires deployed two-replica key continuity, trusted-header spoofing denial, current least-permission provider/guest evidence, managed-identity code redemption and audit/recovery/incident proof. Milestone 2/G1–G9 remain open.

## References

- [Approved identity design](../../docs/security/health-assessment-identity-session-design.md)
- [Approved audit policy](../../docs/security/health-assessment-audit-policy.md)
- [Accepted Azure platform](ADR-0004-azure-pilot-technology-platform.md)
- [Protected binding intake](../../docs/development/bff-production-bindings-template.json)

## Approval

Accepted by: Repository owner acting as technical/security owner, explicit approval in the active session
Date: 2026-10-02
Scope: P01–P05 local synthetic implementation only; [execution plan](../../plans/active/bff-local-authority-audit-implementation.md).
