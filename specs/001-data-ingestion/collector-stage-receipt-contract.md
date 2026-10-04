# Collector authenticated stage receipt — RR-S04

Status: Selected pilot engineering contract; coordinator reviewed before implementation
Authority: Approved release-readiness execution and AGENTS.md pilot decision exception.
Review: Coordinator accepted the exact existing-format contract before code on 2026-10-03; independent implementation review remains required.
Scope: M03 / Phase 1A; feature-001 C1/C3, FR-ING-8–12/19/25, AC-ING-4–6/10/16; feature-003 IP-HAS-004 and TP-HAS-015/016.

## Governing requirements and fixed boundary

The approved [technical specification](technical-spec.md), [implementation plan](implementation-plan.md), [test plan](test-plan.md), [local service/checkpoint contract](collector-local-service-contract-proposal.md) and [database evidence contract](../003-health-assessment/database-evidence-contract.md) require durable minimized pages before a checkpoint may claim completion. The current coordinator authenticates checkpointed stages on restart, but a successful adapter task alone can currently advance a checkpoint whose stage was never persisted.

This packet implements an immediate local authenticated readback receipt before ledger advancement. The receipt is successful verification of existing IGS2 bytes, not a new transport message, evidence manifest, authority token or public API. Keep IGS2/IGC2/IGR1 formats, source adapter, service/CLI activation and retained-evidence policy unchanged. No provider dependency, customer query, package, installer or baseline activation follows.

## Exact readback admission

For the next page, the coordinator freezes its expected minimized field sequence, page boundary, row count, terminal marker and digest before invoking StagePageAsync. Keep the existing authenticated checkpoint context, approved run directory and key. The adapter receives a read-only field collection; it cannot mutate the coordinator's expected sequence through its collection API.

After StagePageAsync returns, before adding the next checkpoint or saving the ledger:

1. Check the existing linked page deadline/cancellation token. User cancellation returns Canceled; an elapsed page deadline returns LimitReached. Neither advances the ledger.
2. Load the expected page through EncryptedPageStageStore.Load from the validated approved-run directory, with the exact expected context, boundary and key. Existing path/ACL, format, AEAD, structural, prohibited-field and content-digest checks remain mandatory.
3. Require a page to exist. Compare its row count, terminal marker and digest to the pre-stage expectations; compare its entire ordered minimized field sequence with ordinal record equality. Counts alone or an adapter-supplied success flag are insufficient.
4. Check the linked cancellation/deadline token again after readback and immediately before checkpoint advancement. Independently recheck the configured local-retention boundary against the authenticated persisted extraction start: an expired result returns Expired without advancing the ledger, even for a valid final-page receipt. A synchronous bounded stage read is not interruptible mid-read; requested cancellation and retention expiry are honored before its result can advance the ledger.
5. Only then append/save the existing checkpoint under the held run lease.

Missing, unreadable, malformed, authentication-failed or substituted stages return StageFailed with only the existing outcome/count/warning metadata. Preserve prior checkpoint bytes and completed counts. Do not print SQL, field values, native identifiers, directory paths, keys, protected references or parser/exception details. The authenticated loader's approved exception categories are mapped to the existing failure result.

Run association means the validated approved-run directory/key/context supplied to the coordinator. Page sequence means the next append after the authenticated existing IGC2 prefix under its lease. IGS2 does not cryptographically embed a new extraction run ID or page ordinal; this packet makes no such claim. Customer run/key provisioning and full normalized row provenance remain separate gates.

## Retry, cancellation and immutability

An identical authenticated existing stage can satisfy readback without rewriting it. A changed stage cannot replace a completed stage: retain Stage's create-once/replay/conflict behavior. If staging succeeded but receipt/cancellation/checkpoint persistence prevented advancement, its orphan page remains for a compatible later idempotent retry. Do not delete it or silently claim completion. Existing restart reconciliation of all checkpointed stages remains intact.

Preserve existing synthetic empty-terminal handling and current boundary checks. Do not infer real source pagination, terminal state or continuation from the receipt. Cancellation may leave a staged orphan; it cannot add a checkpoint after the coordinator observes the linked token as canceled. This packet does not introduce a distributed atomic transaction with token state.

## Paired executable cases

- SR01: exact genuinely encrypted minimized stage succeeds, including existing two-page completion and restart paths.
- SR02: adapter returns successfully without persisting the expected stage; StageFailed and no checkpoint is created.
- SR03: ciphertext tampering or malformed bytes deny readback before checkpoint advancement.
- SR04: independently valid staged bytes with changed row count, terminal marker or ordered minimized content deny; wrong context/boundary/key cannot satisfy expected admission.
- SR05: caller cancellation after successful stage and deadline expiry after successful stage return the existing cancellation/limit outcomes; no checkpoint advances, and a compatible retry can reuse the orphan. A real-clock fixture with one-hour retention, an authenticated start immediately before expiry and a two-hour permitted run deadline must begin staging before and return after the retention boundary; the final authenticated receipt returns Expired without a checkpoint.
- SR06: a failure on a later page preserves exact prior checkpoint ciphertext and prior completed counts; authenticated orphan restage remains idempotent; changed-content restage still conflicts.
- SR07: adapter cannot change the expected field collection; the field sequence comparison preserves order and safe records. Existing prohibited-value and payload-free outcome behavior remain intact.
- SR08: preserve existing host recovery, empty-terminal, stage/checkpoint/key/ACL/lease/capacity tests; execute targeted pinned locked audited restore, format, Release build, owned secret/diff checks and independent review. Windows-specific behavior requires its existing Windows evidence; local execution does not establish G2.

## Deferred executor and baseline contracts

- First-page SQL family and typed initial boundary: strict v1 requires a non-null continuation, while the current coordinator's first read supplies null. No sentinel is selected.
- Full/empty/terminal source-page behavior and separate page identity versus native continuation key.
- SQL-consistent key type/collation/order, unique-key evidence and duplicate/conflict behavior.
- Integer local pack/policy revisions versus semantic artifact versions and exact immutable bindings.
- Native typed per-row identity/path/query/page/row provenance and normalized baseline assembly.
- Trusted signed pack/policy loader, protected connection descriptor, effective connection-bound permission probe, impact limits and source warning/audit.
- SqlClient execution, online/offline delivery, customer host/installer validation and G2 exact-build/source authority.

Worker owns this new contract, the bounded coordinator/stage change and focused CollectorHost fixtures. Coordinator owns shared test wiring, canonical plans/status/evidence, integration, independent review and private site. No production or irreversible operation is authorized by this local contract.
