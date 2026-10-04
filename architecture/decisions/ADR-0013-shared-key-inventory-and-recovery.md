# ADR-0013: Shared-key inventory authority and recovery

Status: Proposed — no architecture, authority, protocol or policy selected
Date: 2026-10-03 UTC
Decision owners: Technical/security, platform, operations/recovery and independent integrity authority; repository owner approves consequential choice
Preparation authority: [bounded inventory review plan](../../plans/active/https-key-inventory-review-preparation.md), `4be13b53a1eb69c9478bab5b54bcc5a9e53aed8e`; source baseline `bc65859b62a9e7441f7188abe88adb085ee557a1`

## Context

[ADR-0004](ADR-0004-azure-pilot-technology-platform.md) selects Azure Container Apps, PostgreSQL, private Blob/Key Vault and managed identities. [ADR-0009 P04](ADR-0009-production-bff-authority-and-audit.md) and [the accepted local key strategy](../../docs/development/https-production-key-contract-proposal.md) prefer Microsoft Data Protection providers with narrow guards. The ring/discriminator/wrapping bindings are fixed configuration seams; protected inventory, rollback witness and recovery authority remain unresolved.

The [37-archive Linux signature review](../../docs/development/https-key-provider-linux-run-result.md#supported-linux-artifact-review-complete--2026-10-03-utc) and [27-case actual SDK dispatch diagnostic](../../docs/development/https-key-provider-dispatch-evidence.md) establish bounded artifact/mechanics evidence. They promote no production dependency, inventory or lifecycle policy. [Resolver403/diagnostics](../../plans/active/https-key-resolver-pipeline-diagnostic.md) runs separately; this ADR neither supplies nor infers its result.

Blob ETag/If-Match prevents a stale conditional write relative to the currently read object. It does not establish that a privileged operator's deleted/recreated/restored valid ring contains the newest authorized keys/revocations. A valid ring at a fresh ETag can omit a required key or revocation. A ring-write principal can insert entries unless another reviewed authority rejects them. An existence probe or second writable object controlled by the same rollback actor is insufficient independent evidence.

Separate identities can limit ordinary writers; a common privileged owner able to restore/rewrite ring, authority and purported witness can defeat all three. Independence must identify which actors/trust roots cannot rewrite historical accepted state together. An operator's authorized recovery action is not permission to reset inventory history or resurrect protected values/access. No proposal here guarantees integrity against every administrator or trust-root compromise.

## Decision drivers

- Preserve supported Microsoft provider/cryptographic formats, exact environment/purposes and all required keys/revocations/concrete wrapping versions.
- Detect an older but valid ring, missing required entries and unauthorized binding/inventory changes before dependent operations become ready.
- Resolve concurrent append and unknown acknowledgment without unconditional overwrite, invented rollback or duplicate authority transitions.
- Preserve independent recovery evidence and authoritative product revocation; key decryptability never grants admission.
- Use existing platform components where feasible, with explicit privilege/failure/cost boundaries and separately reviewed freshness/limits/lifecycle.

## Options considered

### Option A: Existing PostgreSQL inventory authority plus independent witness

Use a proposed module-owned inventory authority in the existing control plane, bound to the exact environment/ring/purposes and required key/revocation/wrapping-version inventory. An independently governed durable witness anchors accepted authority transitions. Exact records, verification, identities and publication protocol remain to be specified.

- Advantages: fits the selected relational control plane; transactional authority comparison/update can arbitrate concurrent publishers; existing SQL ownership/recovery practices provide a starting point for review.
- Disadvantages: Blob persistence, SQL authority and independent witness are separate durable commits. Partial progress and authority outages need exact reconciliation/readiness semantics. No SQL transaction makes a ring write or witness publication atomic.
- Risks: restoring both ring and SQL to an older mutually consistent state evades local comparison without the independent witness. Same-owner witness control supplies no stronger privileged-rollback boundary. A new schema/role or witness destination is not approved by selecting this candidate for review.

### Option B: Separately protected inventory manifest plus independent witness

Use a proposed dedicated protected manifest, conditionally updated and bound to ring contents/history, with an independently governed witness for accepted transitions. Its location/store/format and update/verification authority remain open; this option does not select another Azure service or container.

- Advantages: can separate inventory access/recovery from control-plane availability and keep cryptographic persistence concerns together.
- Disadvantages: multiple storage objects plus witness have no atomic commit. Conditional manifest updates alone do not prove monotonic history after delete/recreate. More object/version/access/recovery boundaries need complete evidence and pricing.
- Risks: ring and manifest under one privileged restore owner can roll back together. Signatures authenticate a signer/content, not freshness, if an older signed manifest can be replayed without an independently trusted latest-state anchor.

### Option C: Separate authorized writer with read-only replicas

Disable automatic generation on replicas and use a separately reviewed writer for bootstrap/generation/rotation/publication. This is an operational alternative that still needs an inventory authority such as A or B and independent witness; it does not replace the rollback solution.

- Advantages: ordinary replica capability can exclude ring writes; inventory transitions can be centrally reviewed/published.
- Disadvantages: adds writer identity/role, availability, scheduling and rotation protocol. Generation disabled alone can permit expired-key selection; new protection readiness needs its explicit policy.
- Risks: compromised/privileged writer or recovery operator remains within the authority threat model. No writer topology, schedule or broader role is accepted. This alternative requires architecture review beyond the currently preferred ordinary-provider strategy.

ETag-only/current-ring acceptance is not a viable alternative for the required inventory rollback checks KEY-014/016. Replacing the Microsoft repository with a custom protocol is not selected; that durable maintenance constraint requires its separate Proposed ADR and supported-format proof.

## Decision

**Unresolved.** Recommend evaluating Option A first because the approved platform already has a transactional control plane. This is a design-study preference, not selection of SQL schema, role, independent witness store/service or persistence protocol. Adopt/refine an option only after the [technical inputs](../../docs/development/https-key-production-input-checklist.md#before-production-key-guard-implementation) and protected authority/recovery review are concrete. Human approval must reference the immutable ADR/technical/test revision and chosen boundaries.

## Rationale

Every feasible option needs a trusted inventory acceptance/update authority and a latest-state/recovery anchor the modeled rollback actor cannot rewrite. Available current-ring bytes/ETag, same-owner replicas or local signed metadata cannot supply that property alone. Option A minimizes speculative new platform components, while its shared database availability/recovery boundary and external witness requirements remain real trade-offs. A complete quote and independence proof may change the preference.

## Consequences

### Positive

A reviewed authority/witness could make ring/revocation rollback distinguishable from ordinary conditional-write conflict and could govern recovery/reopen decisions without changing cookie/ticket purposes or product authorization.

### Negative and trade-offs

Multiple durable stores introduce intermediate/UNKNOWN states. A witness detects only the events/content/absence covered by its reviewed protocol; it does not prove all legitimate generation or honest writers. Missing evidence may block readiness. Cache behavior, witness availability, publication lag and costs need explicit decisions; no availability exception is inferred.

## Security, operations, and cost impact

| Boundary | Exact question a later technical packet must resolve |
|---|---|
| Authority vs storage | Who approves inventory changes, who persists wrapped ring bytes, which immutable binding/history each compares, and what a compromised authorized writer can still do. No authority comes from a client header or XML claim. |
| Independent witness | Named governance/trust root, durable destination, authenticated latest-state/completeness protocol and writer-denial proof. A second store/identity sharing unrestricted restore control is not automatically independent. |
| Concurrency | Every candidate preserves prior required entries/revocations; stale ETags reread safely. Authority comparison and competing transitions cannot lose a winner or reset monotonic state. Exact mechanism/order remains open. |
| Partial/unknown acknowledgment | Specify every ordering/failure point across ring, authority and witness: unchanged ring, persisted unaccepted key, accepted authority with missing witness, or uncertain write. Resolve exact attempted entry/inventory and receipt/history before reporting readiness; absence alone never proves rollback. |
| Freshness/outage | Define trusted current-state checks and cache lifetime/refresh/invalidations, along with cold/warm allowed-operation matrix. No deadline/poll/lag/retry value is selected; cached decryptability supplies no inventory or audit freshness. |
| Recovery | Isolated restore verifies current authoritative inventory/witness, all required concrete wrapping versions, purpose/discriminator/format, SQL protected snapshots and revocations/tombstones before reopening. Missing/regressed/conflicting evidence denies; recovery cannot self-authorize bootstrap. |
| Lifecycle/cost | Inventory all live values and recoverable backups before key/marker/witness retirement. Key generation/lifetime/activation, Blob/vault recovery windows, minimum witness contents/horizon/operator and additional service/storage/operations cost remain unspecified. |

Ordinary replicas, inventory approver/publisher, independent verifier/witness and recovery/lifecycle operators need exact separately reviewed capabilities; this document creates none. Permissions, protected proof, incident/drain and audit producers belong in the technical packet. The [approved audit policy](../../docs/security/health-assessment-audit-policy.md) still requires durable required events; no log fallback, mandatory-event sampling or total-audit-outage exception is selected.

The approved [identity design](../../docs/security/health-assessment-identity-session-design.md) distinguishes ordinary session 30-minute idle/8-hour absolute limits, signed original authentication no older than 15 minutes for single-use authentication transaction admission, protected-action authentication no older than 15 minutes with required MFA/Conditional Access context, and provider evidence under 15 minutes. These limits, audit12months/30additional days and backup35-day maximum/RPO24hours/RTO one business day do not become key/witness retention or freshness values. Prior disposable vault/database7-day and disabled-monitoring30-day settings are unrelated. New services, roles or retained copies need exact review and fresh complete cost/session authorization.

## Migration and reversibility

After human architecture/technical/test approval, propose additive inventory metadata/guards and exact protected bindings; never rewrite historical migrations, import synthetic XML or make startup bootstrap automatic. Production dependency/format compatibility remains a separate gate. No implementation or deployment follows from this Proposed ADR.

Rollback denies/drains incompatible admission and preserves ring, required wrapping versions, authority history, witnesses and product revocations. Reconcile partial writes and restore only under the approved recovery protocol; do not erase inventory to make an older host ready. Replacing an option must preserve the already accepted transition/recovery evidence and protected values.

## Validation

A separately approved implementation must test actual supported provider/authority/witness composition: older valid ring at fresh ETag; same-owner ring+authority restore control; independent witness mismatch/missing history; inserted/removed keys/revocations/foreign bindings; concurrent append and publication; each partial/UNKNOWN commit; cold/warm freshness/outage; isolated restore and marker/key retirement. Independently prove the modeled privilege boundary rather than treating fixtures or a local checkpoint as witness trust. Map to [KEY-002/006–017](../../docs/development/https-production-key-contract-proposal.md#planned-key-evidence), NFR-REL-3/4 and SEC-PILOT-009. These are planned requirements, not new executed KEY evidence.

This packet executes only UTF-8/links/whitespace/protected-prose/secret checks and non-author documentary review under INV-P01–05. Existing signature/27-case results remain historical bounded evidence; no runtime/cloud check is rerun here.

## References

- [Precise implementation/deployment input checklist](../../docs/development/https-key-production-input-checklist.md).
- [Existing protected intake](../../docs/development/bff-production-bindings-template.json) and [current human dependencies](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions).
- [Source-time/audit lifecycle proposal](../../docs/development/https-production-audit-receipt-lifecycle-proposal.md), explicitly proposed production decisions.

## Approval

Accepted by: Not accepted
Date: Not supplied
Required next decision: attributed technical/security/platform/operations and independent integrity-owner review plus repository-owner architecture approval of a concrete option/contract. Local strategy/continuation approval does not select this architecture, grant permissions, authorize spending or release production.
