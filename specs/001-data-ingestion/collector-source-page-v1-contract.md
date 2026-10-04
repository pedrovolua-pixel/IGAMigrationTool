# Collector source-page v1 — RR-S05 design

Status: Proposed finite internal engineering contract; coordinator review required before implementation
Authority: Approved release-readiness execution and AGENTS.md pilot decision exception.
Scope: M03 / Phase 1A; feature-001 C1/C3, FR-ING-2/7–12/19/25/27–35, AC-ING-2/4–6/10/13–16; feature-003 IP-HAS-004, TP-HAS-015/016.

## Governing boundary and finite next packet

The approved [technical specification](technical-spec.md), [implementation plan](implementation-plan.md), [test plan](test-plan.md), [database evidence contract](../003-health-assessment/database-evidence-contract.md), [query preflight](query-pack-preflight-v1-contract.md) and [authenticated receipt contract](collector-stage-receipt-contract.md) remain authoritative. This design resolves the missing first/continuation/terminal and typed-return semantics. It does not identify an actual One Identity table, field, query, installed build, permission grant, source principal, execution plan or impact threshold.

After this design is frozen, the next implementation is one isolated source-page kernel against scripted transport, trusted-authority, native-key-order and warning/audit ports. It handles one immutable query pair per extraction, produces a protected immutable minimized page or a metadata-only refusal, and verifies call ordering. It creates no SqlConnection, file, checkpoint, package, baseline, service/CLI activation or production grant. No SqlClient dependency is added for that packet. Multi-query pack scheduling, physical-provider adapters, exact persistence byte codecs and source-baseline assembly remain subsequent bounded work.

The shipped PendingCollectorRunAdapter remains selected. Existing synthetic CollectorPage, IGS2, IGC2 and IGR1 APIs and readers remain unchanged; they are not relabeled as native typed-source evidence.

## Frozen query pair and exact version namespaces

One protected immutable query-pair descriptor binds opaque pack/query identity, exact source/build/module compatibility, policy and minimum-read-set bindings, ordered projected native fields, one non-null unique reviewed native key, bounds and both exact strict-UTF-8 SQL digests. SQL remains in protected artifacts; no executable source SQL belongs in this document or status site.

- **First query:** one single-table SELECT with exactly the declared ordered native projection, ascending native key order, and bound TOP page-size parameter. No WHERE, boundary parameter, aliases on projections, DISTINCT/ALL, additional clauses, expressions, functions, joins, hints or nested statements. Exactly one non-null int parameter, the declared page-size parameter.
- **Continuation query:** the same exact schema/table, ordered projection, native key, native key type and ascending order. Its WHERE is exactly key greater than the bound non-null typed boundary, with syntax-only parentheses allowed. Exactly the page-size and boundary parameters. Preserve all strict-v1 rejections; this is a distinct descriptor family, not a widening of historical validation.
- No null boundary, zero/empty/minimum sentinel, interpolated value, fabricated GUID ordering or implicit first-page rewrite is permitted. The first family has no lower bound and can return the true minimum key.
- Field and parameter declarations retain the bounded type, identifier, classification and case-duplicate rules of query-preflight v1. Both AST projections must match the ordered descriptor field sequence exactly; a positional or reordered mismatch refuses the pair. Implement a separate first-family validator using the pinned AST parser: the historical read-only/strict guards require a WHERE boundary and cannot be widened or treated as first-family validators.

Keep namespaces explicit. The local protected config's positive integer pack/policy revisions remain local revisions. Pack and query artifact semantic versions remain exact three-component versions; normalization, field-policy and minimum-read-set versions remain their explicitly declared exact labels. A trusted immutable registry record binds the local pack ID/revision/digest and local policy ID/revision/digest to the artifact identities/versions/digests. No integer-to-semver conversion, stripping of components, candidate self-registration or wildcard alias is allowed. Missing or inconsistent registry bindings refuse before source execution. The scripted port represents this trusted record; production signed loading and registry authority remain integration gates.

## Request identity, bounded paging and terminal meaning

A frozen request contains opaque authorized scope, a nonzero immutable ExtractionId, exact query-pair/pack/policy/compatibility bindings, a zero-based PageOrdinal, the query phase, requested page size and, for continuation only, a typed prior native key. Page identity is the tuple of ExtractionId, query-pair identity and PageOrdinal. It is distinct from the native continuation and remains identical on retry while the prior committed prefix is unchanged. It is not derived from a row value or newly randomized on each retry.

The first request has ordinal zero and no continuation. A later request must follow the committed previous receipt, use the next ordinal and its exact typed continuation, and retain every frozen binding. A terminal receipt permits no later request. A missing/untrusted prior receipt refuses continuation. Reconnection does not reset the page identity, budgets or original extraction start.

The executor binds the requested size as int and the continuation using the exact declared native SQL type/width. It never uses AddWithValue-style inference, text substitution, SQL appending or an extra lookahead row. Bound rows, bytes, time and concurrency cannot exceed the narrower of approved artifact and configured limits.

- Fewer returned rows than requested, including zero, is an explicit terminal page for this bounded query execution.
- Exactly the requested row count is conservatively nonterminal. Its next continuation is the last returned native key; a later bounded query is needed to prove the end.
- An empty terminal page has a valid new page identity and zero rows. It needs no fabricated native key or changed continuation. A short nonempty terminal page may preserve its last key as provenance, but supplies no next-work continuation.
- Exhausting a row/byte/time/impact budget before proving the end produces a partial/gap outcome. A full page at the row cap cannot authorize another query or be labeled complete, even if the unseen source happens to end there.
- Cancellation or failed transport invalidates the in-flight page for completion; prior committed immutable pages remain intact. No partially read page becomes a terminal success.

These terminal statements describe the individual SQL executions, not a transactionally consistent source snapshot. A query pair must declare its reviewed source-repeatability limitation. Concurrent source change, reconciliation/tombstones, multi-query collection completeness and baseline activation are governed separately; this packet does not claim snapshot isolation or select locking hints.

## Native schema, rows, ordering and minimization

Before reading values, the transport supplies its actual returned schema: exact ordered names, native SQL types/widths and nullability. Compare it to the frozen descriptor; reject missing/extra/duplicate/reordered columns, type/nullability mismatch or ambiguous provider metadata. No positional coercion, parsing of string UIDs as GUIDs, loss of native types or fallback field mapping is allowed.

Each protected row keeps its zero-based row ordinal, exact native key/type and an ordered field array. Values are typed: signed SQL integer widths, uniqueidentifier value, bounded native character value, bounded binary bytes, or explicit SQL null. Copy caller byte buffers and collections before admission. Native values remain distinct from no-value excluded/redacted/prohibited/unknown markers. Descriptor nullability governs SQL null; the stable key cannot be null. Every projected field and category must be explicitly classified and authorized by the locked field policy before a query is allowed. Preserve query-preflight v1's denial of an excluded/redacted/unknown/prohibited projected declaration; do not locally rewrite the exact projection. A narrower reviewed pair or an explicit no-value planned coverage gap is required.

Apply classification/minimization before producing a page for persistence. Prohibited/unclassified source values quarantine the page from successful staging; excluded/redacted values never enter the minimized value representation. No-value markers preserve planned excluded/redacted scope and any later reviewed classification decision without retaining its value. General identity/account profiles and prohibited topology/secret/government-identifier content remain excluded by the governing evidence contract. A field's declared type alone does not prove its classification. The physical adapter needs the separately reviewed classification dictionary and permitted-value detector; scripted fixtures can verify orchestration without establishing those facts.

Each admitted row/field preserves opaque scope/extraction identity, exact product/module and pack/query versions, both applicable query identity and phase, page and row ordinals, native table/object/column references, native type, trusted extraction timestamp, field/policy/schema version and its minimization disposition. Source-native UID and relationship references are retained only when separately approved fields define them. This is an internal provenance record, not a complete baseline or a generated vendor-default comparison. Protected references and values must not appear in normal strings, logs or refusal results.

Source ordering belongs to the trusted transport/native-key-semantics port. Preserve server result order. Require strictly increasing keys within each page and the first continuation-row key to be greater than the requested native boundary under the reviewed native semantics. Do not compare GUIDs or strings using generic .NET/ordinal ordering. Unsupported or unverified type/collation semantics refuse before a page can be accepted. The scripted packet uses explicitly fictional integer-key semantics; it cannot establish a native provider comparator.

Duplicate paging keys or native-equivalent ties invalidate keyset progress and prevent checkpointing that page. Preserve a no-value conflict record for both row-provenance occurrences; never merge them or silently select one. Duplicate separately approved object UIDs with distinct valid paging keys retain both minimized occurrences and an explicit conflict marker for later baseline reconciliation. Actual UID semantics and permissible relationship/deduplication rules require the approved field mapping; they are not inferred from a key's spelling.

## Connection authority, permissions and source limits

Opening/reopening a source connection uses a protected local descriptor and dedicated principal. The physical provider is the selected Microsoft.Data.SqlClient 7.1.0, with Windows Integrated Security preferred; SQL-auth fallback remains DPAPI/ACL-protected. Validate TLS certificate/hostname; no lab TrustServerCertificate exception, cloud-held SQL credential, direct hosted SQL path or topology logging is introduced. Read-only connection intent is defense in depth and does not establish authority.

A newly opened connection receives an opaque connection-generation identity. Before any evidence query on that generation, revalidate source/customer authorization, exact eligibility and trusted pack/policy bindings, then run its separately approved bounded effective-permission probe on that same connection. Missing minimum-read proof, unknown categories or any write/DDL/ownership/impersonation/admin capability refuse. A reconnect requires fresh authority/permission checks; a previous generation's proof cannot be reused. Pack/policy/source drift, revocation or suspension stops new work without broadening the frozen extraction.

Excess read-only requires a prominent warning and a durable payload-free audit receipt bound to the generation/extraction/minimum-read-set decision before evidence execution. Failure to commit that warning/audit refuses the evidence query. A supplied boolean, candidate reference or collector device identity cannot replace source-access authority or a durable receipt. Local/cloud audit transport and receipt serialization remain separate integrations.

Every probe, open, execute and row read receives the caller cancellation/deadline signal. The physical adapter sets a finite command timeout constrained by the approved per-query/remaining run budget, disposes failed commands/readers/connections and returns closed outcomes rather than exception text. No source writes, transactions/control changes, stored-procedure execution or arbitrary metadata introspection are added.

Approved bounds cover maximum page/total rows, maximum returned field/page bytes, total local staging/queue bytes, duration, concurrency and an independently supplied source-impact stop. The physical adapter must enforce field/page bounds during sequential reading, before unbounded materialization. The scripted kernel requires positive finite configured bounds and can verify stop propagation; it does not invent production defaults or source-impact acceptance thresholds. Check cancellation, persisted local retention, time/row/byte limits and current impact permission before another query and again before a page is eligible for durable checkpointing. Unknown impact status refuses new evidence work. Retries do not silently renew the original extraction start or widen caps.

## Durable typed receipt and format separation — follow-on proposal

The next scripted kernel returns protected immutable typed pages and metadata-only refusals without persistence. Its later durable integration must retain the [RR-S04 readback requirement](collector-stage-receipt-contract.md): authenticate actual exact staged content, compare frozen bindings and ordered minimized rows, recheck cancellation/deadline and original retention, then advance the ordered ledger. Adapter success alone is insufficient. Same identity/content is idempotent; changed content is a conflict. A stage whose checkpoint did not commit is an orphan available only to compatible retry.

Native typed pages cannot be written as IGS2/IGC2 by erasing row/type/provenance or converting page identity back into a native boundary string. A separate reviewed persistence packet should define new IGS3 stage and IGC3 ledger formats, with:

- authenticated scope, ExtractionId, query-pair/artifact/policy/normalization bindings, PageOrdinal and exact requested continuation;
- canonical typed key/value encodings and ordered minimized row/field/provenance/conflict content;
- row count, terminal marker, optional next continuation and content digest;
- authenticated ordered-prefix/previous-receipt bindings, original run-start identity and configured capacity checks;
- exact versioned byte framing/canonicalization/AAD, test vectors, key/directory ACLs, create-once replay/conflict and tamper/failure/cancellation behavior.

This document does not freeze those byte codecs or silently change current storage. Retain existing IGS2/IGC2/IGR1 readers/writers for their historical synthetic module; new native typed readers reject legacy files and unknown future versions. Do not migrate or reinterpret legacy flattened pages as native evidence. A separately selected run-start/ledger codec must persist ExtractionId and the original start for the typed module; it must not reset age on migration or restart. There are no customer-deployed formats or migration operations authorized by this proposal.

## Exact paired test plan

| ID | Required scripted/source-contract result |
|---|---|
| SP01 | Valid first query has no boundary/WHERE; includes negative/zero/minimum fictional integer keys without a sentinel. Valid continuation shares the exact ordered projection/source/key and binds the typed prior key. |
| SP02 | First-query WHERE/boundary, missing TOP, literal TOP, extra parameter, aliases, DISTINCT/ALL, join/function/hint/nested/write/multiple statements refuse. Preserve all strict continuation negatives. |
| SP03 | Projection/source/key/type/digest/semantic-version/policy/local-revision-registry mismatch refuses before execute; malformed Unicode is rejected without replacement bytes. |
| SP04 | First/continuation phase, ordinal, extraction identity or prior terminal/receipt mismatch refuses. Retry retains the exact page identity and typed prior key. |
| SP05 | Empty and short pages are terminal; a full page remains nonterminal, including exact multiples of page size followed by an explicit empty final page. No lookahead/sentinel query occurs. |
| SP06 | Full page at row cap yields partial/gap and no additional query; cancellation/disconnect during a page cannot produce terminal success. |
| SP07 | Actual schema missing/extra/reordered/duplicate fields, native type/width/nullability mismatch and non-null key violation refuse without positional coercion. |
| SP08 | Native integer widths, string UID, uniqueidentifier, bounded bytes and SQL null preserve type; caller buffers/collections cannot alter frozen rows or provenance. Unsupported native ordering refuses. |
| SP09 | Key ties, reversed/native-nonincreasing order or a continuation row at/below the prior boundary refuse; both conflicting row references survive as no-value conflict records. |
| SP10 | Duplicate separately approved UIDs with distinct paging keys preserve both occurrences plus conflict; no inferred UID or relationship semantics. |
| SP11 | Excluded/redacted projected declarations refuse before execute and retain no-value planned gap markers; no projection rewrite occurs. Prohibited/unknown classification or forbidden returned content quarantines the page; no value/name/reference appears in ToString or errors. |
| SP12 | Exact per-row/field source/query/phase/page/row/policy/type/time provenance survives minimization; a caller-invented provenance/binding refuses. |
| SP13 | Missing/revoked source authority, unsupported source, unpromoted/mismatching pack or policy, blocked/unknown permissions deny before the scripted evidence-execute counter advances. |
| SP14 | Excess read-only warning/audit must commit before execute; unavailable/conflicting audit receipt denies; retry uses its exact idempotent identity. |
| SP15 | Reconnect creates a new generation and reruns authority/permission/warning gates; no cached proof from the prior generation permits a query. |
| SP16 | Open/probe/execute/read cancellation, finite timeout, row/field/page-byte/concurrency caps, unknown/tripped impact guard and actual persisted-retention crossing yield explicit stopped/partial results with no page completion. |
| SP17 | Kernel emits no file/checkpoint/package/source connection and leaves the shipped adapter disabled. Existing historical CollectorSafety/CollectorHost tests stay applicable. |
| SP18 | Later codec tests independently verify typed byte vectors, AAD/run/ordinal/previous-receipt binding, replay/conflict, orphan recovery, unchanged prior ledger, tamper, narrow caps and legacy/future-format refusal. Not executed by the scripted packet. |
| SP19 | Later authorized provider tests verify exact SqlClient bindings, TLS, connection-bound effective SQL permissions, reader schema/native semantics, source mutation denial, impact plans and Windows/auth/recovery. Not replaced by scripted tests or public vendor examples. |

## Implementation ownership and remaining source-adapter gates

After coordinator review, a finite scripted implementation packet can own new source-page kernel/contracts/tests under the collector boundary, with coordinator ownership of solution/project/lock wiring and canonical records. It must use immutable fictional fixtures and independently expected transport call traces, page identities, terminal outcomes and provenance. The exact scoped work packet/test-host wiring is frozen before code. Do not add the physical provider, persistence codec or hosted baseline route by implication.

Remaining integrations are: signed registry/pack/policy loader; actual protected descriptor and TLS/auth provisioning; exact source/SME/DB-owner build/query/field/key/permission/impact evidence; physical permission and execution adapters; typed persistence/audit receipts and capacity/cleanup/key lifecycle; customer-controlled host/MSI/Server Core checks; online/offline receiving authorization and package inspection; immutable normalized baseline assembly; read-only health adapter; independent A/B validation. G2 remains NOT VERIFIED. Production or irreversible operations remain separately authorized.
