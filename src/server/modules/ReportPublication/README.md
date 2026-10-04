# Native publication core preparation

RR-P04 prepares the internal types and ports for the independently reviewed
[native v1 contract](../../../../specs/003-health-assessment/native-publication-v1-contract.md).
This module has no ordinary-host reference, database/HTTP/MCP/source adapter,
provider integration, migration or live activation.

The typed input stores only minimized report content. Collection constructors
copy inputs; source proof construction remains internal to an owning adapter.
Structural types do not establish minimization, eligibility, authority, terminal
completion or publication. Canonical commitments follow the independently
frozen RR-P03 literal envelopes and original vectors committed at `51272d2`
before this codec. Historical synthetic contracts remain unchanged.

The coordinator selected finite support ports before dependent flows:
`IPublicationStoreV1.BeginAsync` supplies one scoped transaction;
`ResolvePublicationReceiptAsync` and `ResolveReadReceiptAsync` distinguish
NotFound/Found/Conflict rather than silently treating a changed actor/hash as
a missing receipt. The transaction owns the database clock, exact-version
metadata, source revalidation, audit append and immutable metadata/receipt
writes. Commit exceptions/cancellation are conservatively uncertain unless
the adapter proves not committed; reconcile the original receipt. Actual
PostgreSQL/control-plane adapters remain absent.

`IPublicationOutcomeAuditV1` records safe known denial/failure after rollback
and fence release; unavailable/unknown audit emits only the closed operational
signal. The audit writer owns stream/sequence/time, never caller-selected heads.
`IReferenceAvailabilityV1` returns only linked metadata. The constructor-only
trusted sink honors the original earliest deadline at every bounded protected
write. The callback view exposes no bytes and permits no background delivery.
The authority/lifecycle/reference fence must remain held after audit commit
through valid emission; transaction row locks released at commit cannot provide
that guarantee.

The project uses the existing repository net10.0 settings and has no package
dependencies. A scoped locked restore/build will be recorded once preparation
is complete; no whole-repository build or acceptance evidence is implied.

A successful `CommitAsync` ends the database write transaction. Until disposal,
the scoped context may retain only read operations for current report/reference
metadata under the independent, still-held fence; Append/Add/Commit after success
must refuse. Unknown commit outcome also forbids new writes or retrying Commit;
reconcile the original scoped receipt through a fresh read-only context and fresh
current authority. Disposal invalidates retained context.

`NativeReportPublisherV1.PublishAsync` and `NativePublishedReportReaderV1.ReadExactAsync`
are finite core flows. Exact read authorizes the entire declared field/category
set before loading blobs/references, verifies original canonical bytes and all
source commitments, then commits read audit/receipt before the callback. The
callback receives only an opaque delivery view. A UTC plus monotonic original
deadline lease cancels paused callbacks and invalidates delivery before releasing
fences. Every protected delivery rechecks authority, lifecycle and reference
metadata. The configured transport must honor linked cancellation on every
bounded write; production adapters remain a separate packet.
