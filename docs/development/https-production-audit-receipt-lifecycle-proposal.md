# Production authentication audit receipts and linked lifecycle

Status: PROPOSED — document review only; no production contract or lifecycle decision accepted
Date: 2026-10-03 UTC
Owners/reviewers: Technical/security, identity/platform, data-governance and operations owners
Source baseline: `b2099783820123726579a113489d811fe682562f`; writing authority: [cycle04](../../plans/active/https-production-local-cycle04.md), frozen plan `4f74b056feac5b20c1de8827cd740f614059a3a0`
Product: [approved pilot](../../specs/003-health-assessment/product-spec.md); policy: [approved audit policy](../security/health-assessment-audit-policy.md); architecture: [ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md), [ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md)
Validation: [ARL01–ARL18 planned packet](https-production-audit-receipt-lifecycle-test-packet.md); parent [AU01–AU10](https-production-test-packet.md#authentication-audit-tests--proposed-au)

## Authority and evidence boundaries

The approved policy requires 12 months from event timestamp, ordinary invisibility at expiry, active purge within 30 additional days unless an approved hold applies, maximum 35-day rolling backups and deletion-tombstone replay before restore access. Holds confer no ordinary visibility or new retention clock. These requirements are unchanged.

ADR-0012 accepts Option A and source-occurrence-based linked retention **for the local prototype only**. Production metadata/read filtering, independent witness, lifecycle authority and total-outage handling remain open. The uniform descriptor-occurrence anchor proposed below additionally covers NEW available-store outcomes; that consequential production interpretation is ARL-D01, not an existing approval. No new interval, sampling exemption or indefinite metadata exemption is supplied here.

[Cycle02](../../plans/active/https-production-local-cycle02.md) verifies the unused AP01 codec/first-terminal freeze; [cycle03](../../plans/active/https-production-local-cycle03.md) verifies a test-only anonymous PostgreSQL model: 60 new checks plus 21 independent SQL oracles. Those tests prove neither known-identity production provenance nor Blob delivery, witness trust, production receipt schema, linked retention, backup/restore or hooks. All ARL cases remain PLANNED.

## Candidate records — additive, internal and closed

The names/fields below are proposed internal contracts, not public routes, deployed tables or a change to canonical audit/operation-receipt v1. Every accepted encoding has a fixed version, closed exact keys and canonical lowercase UUID/digest, decimal-string positive Int64 and seven-digit Z UTC rules. Unknown/duplicate/missing fields, numeric coercions, alternate spellings, invalid UTF-8 and prohibited payload deny. Recompute every digest at the database boundary; do not trust a supplied hash alone.

| Candidate | Exact proposed content and constraints |
|---|---|
| Authentication outcome receipt v1 | Full immutable `SecurityAuditBindingV1` snapshot; operation/event/correlation IDs; exact AP01 canonical descriptor bytes and SHA256; original `occurredAtUtc`; canonical event ID, positive sequence, event SHA256 and post-lock `eventAtUtc`. Unique `(streamId,operationId)` and canonical event ID; frozen descriptor IDs equal canonical IDs. No mutation target/security version fabricated to fit receipt v1. |
| Journal-source receipt v1 | Reviewed journal binding reference, source event ID, server-resolved opaque storage receipt/version reference, source descriptor SHA256, exact outcome key, original occurrence text, canonical append-time text, protected storage-proof and independent provenance/witness references. Unique `(journalBinding,sourceEventId)` and `(journalBinding,storageReceiptReference)`; exact source event/digest links to one outcome. Multiple accepted source bindings cannot create another canonical event. |
| Linked lifecycle record | Immutable reviewed anchor and decision/version reference; original expiry and purge deadline; attributable soft-delete/purge transitions; exact linked hold references and lifecycle authority. Mutable lifecycle state is separate from immutable outcome/source records. Purge removes retained descriptor/identity content rather than rewriting an immutable receipt in place. |
| Minimal resolution/chain anchor | Proposed separate record retaining only the explicitly reviewed operation/event/stream, descriptor/event digests, sequence/previous digest, purge authority/time and deletion linkage needed for conflict resolution/chain recovery. Exact minimum contents, access, horizon and retirement evidence are ARL-D06; no indefinite default. |

Store exact descriptor/time text independently of any PostgreSQL timestamp projection. Never reconstruct the seventh fractional digit from Npgsql `timestamptz`. Derive representational bounds from the closed schema as AP01 does; operational capacity/deadlines are separately reviewed inputs, not codec limits. Storage proof references resolve protected evidence outside these payload-free records; never store SAS URLs, tokens, raw certificates, provider bodies or arbitrary free-text proof bags.

### Identity and projection

Use existing `AuthenticationDenied/Denied` or `AuthenticationFailed/Failed` and existing reason enums, including `None`. No new MFA, privilege, eligibility or authentication-evidence claim is introduced; canonical `authenticationEvidence` remains its existing `None`.

Anonymous has null subject, actor/target, session and security version; customer/project and previous-session fields are always null. A nonanonymous Human or Workload requires independent producer-specific immutable identity proof before terminal freeze. Proposed projection maps the already verified descriptor subject to canonical actor and the same target; it grants no eligibility or admission. A known subject may precede a session and version; any session requires verified subject and positive version. Exact current session/version evidence must come from its trusted producer, never a cookie lookup hash, client claim or guessed row.

Parsing establishes shape only. Receipt lookup/reconciliation cannot revalidate a user, renew provider freshness, create/rotate/revoke a session, change product authority or upgrade an anonymous first-terminal descriptor. A syntactically valid signed-looking identity or journal object is insufficient. Producer identity/proof mechanisms and the proposed known-subject target mapping require ARL-D02 review. Preserve unknown evidence as unverified; do not manufacture a verified record or mutate a frozen one.

### Source and witness trust

Resolve an object only from the protected approved journal-store binding plus server-owned event identity through an exact reviewed locator/version algorithm. No caller URL/path, redirect, traversal, alternate container, key/evidence store or client-selected credential is accepted. The production algorithm and receipt encoding are ARL-D03; the fixture's event-derived UUID is not a Blob storage acknowledgment or version.

Before import require matching canonical bytes/digest, exact producer/environment/writer binding, exact storage object/version proof and independently authenticated witness/provenance for that same source identity/digest. A witness retained in the database being restored or a Boolean supplied by the writer is insufficient. Proof protocol, verifier trust, destination, cadence, missing-object detection and permissible lag remain ARL-D03/D08; do not infer completeness from a successful list or one matching object. Missing/altered/unwitnessed evidence blocks import and requires an integrity decision, without ordinary-log fallback.

## Transactions, retries and failures

Proposed available-store failure append occurs in a new transaction after the failed mutation transaction has rolled back. It uses no poisoned transaction and never borrows an issuance event. Journal persistence remains a separate conditional-create boundary; no cross-store atomicity is claimed.

1. Verify producer/descriptor/binding and, for import, exact source proofs outside database locks. Capture trusted current UTC; future occurrence or untrusted clock/proof denies.
2. Lock the exact outcome operation before the canonical stream head. Resolve receipt or reviewed purge marker before attempting an append. Exact full binding/descriptor/digest returns only committed metadata; any changed ID, correlation, identity, occurrence, action, reason or content conflicts.
3. For a new outcome append through the existing canonical appender, preserving descriptor IDs/projection. Ordered canonical time is sampled after head contention; it must not precede original occurrence. Allocate sequence only inside this transaction; no backdating or head rollback.
4. Atomically record outcome, optional source receipt and necessary reviewed lifecycle/visibility state. A direct outcome can acquire its exact source link later without another event or changed anchor. Deferred obligations cover every authentication event and exact source projection, not merely foreign-key existence.
5. Recheck trusted time, authorization/proof validity, hold/lifecycle state and reviewed operation limits after asynchronous work/lock waits and immediately before commit. Commit precedes persistence acknowledgment; no admission follows this receipt.

All paths acquire the same operation lock before stream-head locking. Failure paths need no subject mutation lock; any eventual composition with existing mutation transactions must preserve their subject/target-before-head order and must not introduce an inverse lock path. Whole protocol composition/concurrency proof remains required.

Cancellation, disconnect, timeout or lost commit acknowledgment is UNKNOWN until exact receipt/marker resolution. A missing row alone never proves rollback or permits a second event. Integrity conflicts fail closed and remain attributable; retry uses the same frozen descriptor and reviewed binding. Bounded cancellation/deadline/recovery policy and public error behavior remain ARL-D08, without invented values. Both stores unavailable remains an unpreserved required event and production blocker under ARL-D09; safe refusal is not audit compliance.

## Proposed linked lifecycle and read contract

ARL-D01 recommends one immutable logical occurrence anchor for NEW direct and journal outcomes, bound to the frozen descriptor before initial commit. This prevents a later matching source attachment from changing expiry. Canonical stream time remains fresh append time for ordering. Credible alternative: canonical append age for direct outcomes and original source age for imports; it needs an explicit cross-path precedence rule and can otherwise change age when source arrives. Neither alternative is selected for production by this document.

After approval, calculate expiry as anchor plus 12 UTC calendar months, preserving time/tick precision and documenting end-of-month/leap-year behavior; purge deadline is expiry plus 30 elapsed UTC days. A delayed soft-delete/job/import never starts another 30-day window. The same original age governs descriptor, source, linked canonical disclosure and ordinary receipt reads. Historical events without this new reviewed descriptor/lifecycle contract retain their existing policy; no invented historical occurrence or retroactive timestamp rewrite.

Every ordinary read enforces current scope and trusted-time expiry, not only a mutable soft-delete flag. Filter canonical events, descriptor-bearing receipts, source metadata, searches, exports and caches before disclosure. Expired imports enter with ordinary visibility already denied in the same transaction; a reconciliation-time timestamp cannot reopen access. A hold preserves authorized material without restoring ordinary visibility. A separate protected hold-review access capability, if required, needs explicit scope/approval; it is not supplied here.

Exact internal retry resolution may need a reviewed capability to compare caller-held trusted digest with a minimal committed/purged marker after descriptor disclosure expires. It returns only the approved committed tuple/status, never the retained descriptor, subject, source locator or renewed authority. It cannot list/search another operation. Ordinary unavailable/not-found behavior must not reveal cross-scope existence. ARL-D04/D06 must settle this capability and marker lifecycle before implementation.

| Arrival / hold state | Required boundary; unresolved procedure |
|---|---|
| Before expiry | Verified new outcome/source commit normally; matching existing outcome only gains source linkage. |
| Expired, before purge deadline | Already invisible at commit. Preserve original expiry/deadline; authorized lifecycle completes remaining purge without another retention window. |
| Beyond purge deadline, no hold | No default import. ARL-D05 must select/refine atomic append plus immediate attributable tombstone/minimized linkage, or refusal pending authorized source lifecycle handling. Either option blocks ordinary disclosure; neither erases prior lateness or proves timely purge. Existing appender/lifecycle functions alone cannot perform the proposed linked procedure. |
| Exact active hold | Preserve only the reviewed linked categories/copies and original age; no ordinary visibility. Hold expiry/release follows the approved hold record, not a guessed auto-release schedule. |
| Missing/stale/conflicting hold or source proof | Deny disclosure and destructive actions; preserve for owner resolution and record the compliance/operational gap. No silent retention extension is approved. |

Hold scope/basis/owner/start/review-expiry lives in a protected authorized record referenced by closed metadata. Release resumes original lifecycle; if its deadline has passed, no fresh 30-day grace is inferred. Placement/release/purge races serialize on the reviewed linked lifecycle target; exact lock order and atomic cross-store deletion receipts need ARL-D04/D05. Reconciler has no general delete/hold/purge capability. A special expired-import transaction, if selected, must have independently reviewed narrow authority, not borrowed broad lifecycle grants.

### Purge, backups and chain preservation

Purge accounts for journal objects/versions, full descriptors, identity-bearing receipts, linked canonical content and governed witness copies under the same reviewed age/hold decision. Do not keep equivalent personal metadata in a convenient receipt or proof indefinitely. Cross-store deletion is not one SQL commit: record exact authorized attempts/unknown outcomes, resolve exact version/deletion receipts, and finish within the original deadline; no inferred success from a missing ordinary read.

Retain attributable original stream sequence/previous digest/event digest through a reviewed minimal chain anchor/tombstone so authorized deletion differs from unexplained gaps. Live-event-only foreign keys must not block legitimate purge or cascade-delete the evidence; propose an additive reference to a durable logical event/chain anchor. Exact referential migration and marker retirement are ARL-D06, not a patch to historical migration003.

Rolling backups may cover at most 35 days; this is not a new post-purge retention allowance. Inventory every recoverable SQL/journal/witness/marker copy, encryption dependency and replay boundary. Before any restored ordinary read, import or authority operation, verify independently trusted head/provenance, apply deletion/hold/source/deduplication markers and preserve security versions/revocations. A stale backup or source cannot resurrect an expired descriptor or create another outcome. Missing authoritative markers/proofs block restored access.

Retiring minimal anchors/witnesses needs an exact owner-approved horizon and evidence that every still-supported restore/source replay is covered by authoritative deletion/deduplication state and trusted chain checkpoints. Do not invent a horizon from cookie lifetime or 35 days alone; held/unreconciled sources and surviving recovery copies may differ. ARL-D06/D07 must define admissible recovery inventory, minimum metadata and lawful deletion conditions.

## Current compatibility gaps and recovery

- [AP01 codec/context](../../src/server/modules/IdentitySessions/AuthenticationFailureJournal.cs) proves canonical metadata/freeze only; it has no producer hooks or identity/witness verifier.
- [Appender](../../src/server/modules/IdentitySessions/PostgreSqlSecurityAudit.cs) stamps post-lock time; [receipt v1](../../src/server/modules/IdentitySessions/SecurityAuditContracts.cs) requires target and security version. Keep both unchanged; additive outcome receipts must not fabricate values.
- [Historical migration003](../../migrations/identity-sessions/003-atomic-audit.sql): `visible_events` filters soft-delete state, not linked-time job lag; `soft_delete_event` uses canonical event age and blocks holds; `purge_event` checks actual soft-delete state/time and provides no linked source original-deadline/read/proof contract; existing tombstones retain stream, sequence, previous/event digests and lifecycle authority but have no accepted new linked-metadata horizon. Existing mutation receipt obligations exclude authentication outcomes. These are explicit gaps, not silently remediated by this proposal.
- [AP-PG test helper](../../tests/integration/SecurityAudit.Tests/AnonymousOutcomeReceiptChecks.cs) is anonymous-only; its live-event foreign key blocks event purge, source receipts have fixture-only references, and its reader has no production expiry filter.
- [Integrity verifier](../../src/server/modules/IdentitySessions/AuditIntegrityVerifier.cs) verifies canonical chain/receipt v1, not additive outcome/source lifecycle trust; a future additive verifier is needed after contract approval.

Any future implementation needs separately approved additive technical/implementation/test plans, role/schema review and migration compatibility proof. No startup migration, destructive backfill, direct production grant or public receipt route is authorized. Rollback disables admission/reconciliation/lifecycle jobs and preserves evidence, keys, holds, markers and authority versions; no schema downgrade or data deletion to make an older host work. Telemetry is allowlisted categories/counters/opaque correlation only; thresholds, deadlines and overload controls require review.

## Decision register and protected intake

Every row is OPEN/PROPOSED. Approval must identify exact proposal/test revision, chosen alternative, named approving role and affected protected binding; owner approval of a local prototype does not complete these rows or authorize execution.

| Decision | Required authorizer | Exact completion condition |
|---|---|---|
| ARL-D01 — anchor/calendar | Data-governance + technical/security; repository owner for consequential policy interpretation | Accept/refine new direct/source anchor and calendar behavior against canonical-time alternative; historical compatibility and boundary examples reviewed. |
| ARL-D02 — identity/projection | Identity/platform + security | Bind each Human/Workload producer proof to immutable identity and frozen descriptor; approve known-target/session/version mapping and invalid-proof handling. |
| ARL-D03 — locator/source/witness | Platform + security + independent witness owner | Exact private store/object/version/receipt locator, create-only capability proof, producer trust and independent witness protocol/destination/completeness binding; no writer-controlled acceptance flag. |
| ARL-D04 — visibility/holds | Data-governance + security + lifecycle owner | All linked read surfaces, post-expiry resolution capability, exact hold scope/review/release authority and lock/race ordering accepted. |
| ARL-D05 — late import/deletion | Data-governance + technical/security + lifecycle/operations | Select past-deadline procedure and narrow authority, exact cross-store deletion/unknown receipt protocol and original-deadline incident handling. |
| ARL-D06 — markers/witness retention | Data-governance + security + independent witness/operations | Exact minimal contents, lawful horizon, access and demonstrable retirement conditions; additive referential compatibility approved. |
| ARL-D07 — backups/restore | Operations/recovery + data-governance/security | Complete recoverable-copy inventory, maximum35-day enforcement, trusted replay order and isolated restore proof with no resurrection. |
| ARL-D08 — limits/errors | Operations + security/technical; product owner for public behavior | Exact deadlines/capacity/lag/retry/overload and generic response contract; mandatory events neither sampled nor silently dropped. |
| ARL-D09 — total outage | Security/data-governance/operations + repository owner | Exact correlated-outage decision or separately approved policy amendment; no exception inferred from refusal or Blob availability. |

Complete an owner-only copy of [binding intake](bff-production-bindings-template.json), linking database scopes under `authorityAndAudit.controlPlaneDatabaseAndRoleFunctionScopeReference`, lifecycle identity under `trustedAdministrationMigrationAndLifecycleIdentityReference`, atomicity under `atomicAuditAndCommitUnknownProofReference`, restore under `restoreTombstoneAndSecurityVersionProofReference` and ARL source/witness/lifecycle evidence under `protectedAuditIntegrityAndLifecycleEvidenceReference`. Its existing generic fields do not themselves supply the missing journal/witness contracts.

Required protected review attachment (template; actual values remain outside Git/site): exact proposal/test digest and decision record; immutable full audit binding/transition map; producer/verifier identity proof bindings; journal store/locator/version/create-only role proof; separate writer/reconciler/reader/witness/lifecycle/migration identities and function/object scopes; independent witness protocol/destination/trust; linked lifecycle/hold/deletion manifest; minimal marker/witness content+horizon; complete backup/restore inventory; reviewed limits/errors/outage decision; named approving owners and source-bound executed evidence references. Any missing attachment keeps dependent implementation/live activation blocked.

## Approval and execution status

No production decision approved; no ARL test executed. This two-document packet can be reviewed and revised. Human contract approval precedes dependent implementation; actual protected deployment, paid session and production release retain their separate gates. No new resource, permission, dependency, SQL migration, job or customer behavior follows from document completion.
