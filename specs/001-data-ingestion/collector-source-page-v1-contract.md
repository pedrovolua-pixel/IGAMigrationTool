# Collector source-page v1 — RR-S05 design

Status: Finite scripted-kernel design e22b8c83a464785ce23502cd10c545b13b272460 reviewed and accepted by coordinator; RR-S06 literal API amendment 3b143ed0d871a8ff6a5049c3174d6236749073ca reviewed and accepted by coordinator before implementation
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

The coordinator accepted the finite scripted-kernel scope after review of e22b8c83a464785ce23502cd10c545b13b272460. The worker owns only this contract and NEW `src/collector/CollectorSourcePages/**` and `tests/unit/CollectorSourcePages.Tests/**`, including their own csproj, package locks, README and standalone assertion-host Program. The coordinator owns solution/CI/shared configuration/canonical records. No existing CollectorSafety or CollectorHost source/test files are edited. CollectorSourcePages references CollectorSafety and uses its already pinned ScriptDom transitively; no new package is selected. The literal API and test-host wiring below must be reviewed before code. Do not add the physical provider, persistence codec or hosted baseline route by implication.

Remaining integrations are: signed registry/pack/policy loader; actual protected descriptor and TLS/auth provisioning; exact source/SME/DB-owner build/query/field/key/permission/impact evidence; physical permission and execution adapters; typed persistence/audit receipts and capacity/cleanup/key lifecycle; customer-controlled host/MSI/Server Core checks; online/offline receiving authorization and package inspection; immutable normalized baseline assembly; read-only health adapter; independent A/B validation. G2 remains NOT VERIFIED. Production or irreversible operations remain separately authorized.


## Literal scripted-kernel API amendment — review before code

Namespace `CollectorSourcePages`; net10.0 library plus a standalone net10.0 assertion executable with a project reference to that library. The implementation is one page per call, one query pair per extraction. Every trusted port is required in the constructor; there is no default production adapter, permissive fallback, request grant or boolean override. Opening a fresh connection generation on every call deliberately prevents permission-cache reuse; a transport disconnect ends that attempt rather than transparently reopening after a probe. Retry is a new call with the same trusted history and identity.

### Immutable protected construction

The following sealed classes expose getter-only properties, copy incoming arrays/collections and bytes, and override `ToString()` with the literal type name only. No generated record `PrintMembers`, interpolated values, raw exception text or protected identifiers appear in outcomes. Constructors do not attest trust. Access to protected getter properties is for the internal adapter pipeline; callers must not log them. Invalid candidate data is closed by preflight/kernel, and named trusted ports are the only sources of authority/history/permission/impact/value/native-order evidence.

- `SourceScope(Guid CustomerId, Guid ProjectId, Guid EnvironmentId, Guid ExtractionId)`: four nonzero opaque identifiers.
- `SourceQueryPair(Guid PairId, QueryPackDescriptor Continuation, string FirstSql, string FirstSqlSha256, int LocalPackRevision, int LocalPolicyRevision, string? ApprovedUidField, string SchemaVersion, string NormalizationVersion, string RepeatabilityReviewReference)`: snapshots the existing descriptor, applicability arrays, fields and parameters. The existing descriptor is private/internal to the implementation, not exposed through a generated string. Public scalar/field getters needed by the transport remain protected. PairId is independent of pack/query identities. Exact registry mapping also binds optional UID field, schema/normalization versions and repeatability review.
- `SourcePageIdentity(SourceScope Scope, Guid PairId, long PageOrdinal)` and enum `SourceQueryPhase { First, Continuation }`.
- `SourcePageLimits(int MaximumPageSize, long MaximumRows, long MaximumFieldBytes, long MaximumPageBytes, long MaximumTotalBytes, TimeSpan MaximumDuration, TimeSpan CommandTimeout, TimeSpan Retention)`: all strictly positive, command timeout finite and no longer than maximum duration, field <= page <= total bytes. Concurrency is fixed to one in-flight call per kernel; a concurrent call is refused before opening. Page size/rows/duration use the narrower of these limits and query descriptor bounds. Retention uses the original trusted extraction start. No disk/queue admission is claimed by this memory-only kernel.
- `SourcePageRequest(SourceScope Scope, SourceQueryPair Pair, long PageOrdinal, SourceQueryPhase Phase, int RequestedPageSize, SourceNativeValue? Continuation, SourcePageLimits Limits)`. A nonterminal prior receipt supplies the exact typed continuation and next ordinal; an initial request has ordinal zero/First/null continuation. Requested size must be within the remaining row allowance, not silently clamped. `SourcePageIdentity` is derived once from frozen request inputs.
- `SourceNativeValue`: immutable native tag and exact SqlType plus one payload: `Integer(string sqlType, long value)`, `UniqueIdentifier(Guid value)`, `Text(string sqlType, string value, int NativeByteLength)`, `Binary(string sqlType, byte[] value)`, `SqlNull(string sqlType)`. Enum `SourceNativeKind { Integer, UniqueIdentifier, Text, Binary, SqlNull }`. No coercion. Binary inputs and outputs are copied. Integer bounds are tinyint 0..255, smallint Int16, int Int32, bigint Int64; uniqueidentifier is 16 native bytes. Text preserves exact native strings; transport reports actual encoded byte length for varchar, whose encoding is provider-owned, and nvarchar uses strict valid UTF-16 code-unit bytes. varchar byte length must be >= character count and <= declared width; nvarchar byte length must equal twice UTF-16 length and <= twice declared width. Empty text is zero bytes. Fixed binary requires exact declared width; varbinary permits <= width. Null uses zero payload bytes and descriptor nullability. A later physical adapter must supply independently verified varchar byte lengths and encoding. This kernel does not infer a code page.
- `SourceColumnMetadata(string Name, string SqlType, bool Nullable)` and `SourceReturnedSchema(IEnumerable<SourceColumnMetadata> Fields)` and `SourceReturnedRow(long RowOrdinal, IEnumerable<SourceNativeValue> Values)` freeze the reader's exact ordered schema/values. Schema names/types/nullability are compared to declared field metadata; classification is never taken from provider schema. The trusted locked dictionary supplies declared classification and the separately bound named policy port supplies actual returned-value disposition. This scripted shape does not pretend SQL schema discovery itself classifies data.
- `SourceProvenance`: constructed only by the kernel from frozen scope/pair, registry source build/modules, exact query phase, page/row/field ordinals and trusted timestamp. Getters include `Scope`, `PairId`, `PackId`, `PackVersion`, `QueryId`, `QueryVersion`, `Phase`, `PageOrdinal`, `RowOrdinal`, `Schema`, `Table`, `Field`, `SqlType`, `PolicyId`, `PolicyVersion`, `SchemaVersion`, `NormalizationVersion`, `ExactBuild`, immutable installed-module pairs, `ExtractedAtUtc`, `FieldDisposition`. It contains no raw field payload; its ToString is constant.
- `SourceMinimizedField(SourceProvenance Provenance, FieldDisposition Disposition, SourceNativeValue? Value)`, `SourceMinimizedRow(long RowOrdinal, IEnumerable<SourceMinimizedField> Fields)`, `SourceRowReference(SourcePageIdentity Page, long RowOrdinal)`, `SourceConflict(SourceConflictKind Kind, SourceRowReference First, SourceRowReference Second)` with `SourceConflictKind { PagingKeyTie, PagingOrder, ObjectUid }`. Conflict records expose both protected occurrence references but no key/UID/name/value. The continuation-boundary violation references the previous page's last row and the new row; no synthetic previous row is invented.
- `SourcePage` is kernel-created with `Identity`, `Phase`, `RequestedPageSize`, immutable `Rows`, immutable `Conflicts`, `Terminal`, optional `NextContinuation`, `ObservedBytes` and frozen `ExtractedAtUtc`. Terminal pages never supply next-work continuation. `SourcePageReceipt` is kernel-created with that page's identity/frozen binding fingerprint, optional next key, terminal/last-row metadata, original start, cumulative observed rows/bytes and trusted extraction start; this is an in-memory admission receipt, not an authenticated persistence claim. A history adapter may return this same receipt only after its own future durable contract is satisfied.

The pair's fingerprint compares all frozen descriptor/query/registry/minimization/budget identities and exact native boundary payloads without stringification. It is a protected canonical in-memory equality value, not a new persisted digest/codec. Fingerprinting does not reorder fields/modules or normalize SQL/version strings. For API clarity the receipt stores the exact frozen pair and scope, and equality is explicit structural comparison. An identical retry must see the same prior receipt; changed candidate bindings refuse. Limits can narrow on continuation but cannot widen prior extraction caps, duration or retention.

### Closed outcomes, counters and trusted ports

`SourcePageOutcome { PageReady, Partial, Refused, Quarantined, Canceled, TimedOut, Expired, Disconnected }` and `SourcePageReason { None, InvalidInput, ConcurrentCall, AuthorityMissing, AuthorityRevoked, RegistryMismatch, InvalidQueryPair, HistoryMismatch, PermissionBlocked, WarningAuditMissing, ImpactUnknown, ImpactStopped, RowCap, FieldByteCap, PageByteCap, TotalByteCap, SchemaMismatch, NativeValueInvalid, NativeOrderUnsupported, PagingKeyConflict, PagingOrderViolation, ClassifiedContent, TransportFailure, PortFailure, Deadline, Retention }` are closed enums. `SourcePageResult` contains Outcome/Reason, an optional protected Page/Receipt, immutable no-value Conflicts/planned field-disposition gaps and `SourcePageCounters`. Page/Receipt exist only for PageReady and complete full-page row-cap Partial (valid page admitted with Terminal=false and no next-work continuation); every interrupted/invalid/quarantined page is discarded. A row-cap partial receipt cannot authorize a next request. Planned declaration gaps retain disposition and opaque field ordinal only, never a rejected SQL name or value. Ordinary result/counter ToString can show only enum names and numbers; SourcePageResult ToString is the literal type name.

`SourcePageCounters` contains only long counts: `AuthorityResolutions`, `AuthorityRevalidations`, `HistoryLoads`, `ConnectionOpens`, `PermissionProbes`, `WarningAudits`, `ImpactChecks`, `Executions`, `ReadCalls`, `RowsObserved`, `BytesObserved`, `ValueClassifications`. Counts mean attempted calls, actually received rows and measurable native payload-byte totals for this attempt, including rejected values. Prior receipt totals are separately trusted. Checked counter overflow closes Refused/InvalidInput rather than wrapping or returning a page. SQL rows and native payload bytes are bounded; CLR overhead/provenance metadata and process memory are not presented as a disk-staging byte quota.

Required named interfaces (all async operations receive CancellationToken; protected returned classes have constant ToString):

```csharp
public interface ITrustedSourceAuthority
{
    ValueTask<SourceAuthorityResolution> ResolveAsync(SourcePageRequest request, CancellationToken cancellationToken);
    ValueTask<SourceAuthorityState> RevalidateAsync(SourceRegistryBinding binding, Guid connectionGeneration, CancellationToken cancellationToken);
}
public interface ITrustedPageHistory
{
    ValueTask<SourceHistoryResolution> LoadAsync(SourcePageIdentity identity, CancellationToken cancellationToken);
}
public interface ISourcePageTransport
{
    ValueTask<ISourcePageConnection> OpenAsync(SourceRegistryBinding binding, CancellationToken cancellationToken);
}
public interface ISourcePageConnection : IAsyncDisposable
{
    Guid Generation { get; }
    ValueTask<ISourcePageReader> ExecuteAsync(SourceBoundCommand command, CancellationToken cancellationToken);
}
public interface ISourcePageReader : IAsyncDisposable
{
    SourceReturnedSchema Schema { get; }
    ValueTask<SourceReturnedRow?> ReadAsync(CancellationToken cancellationToken); // null is explicit end
}
public interface ITrustedConnectionPermission
{
    ValueTask<SourcePermissionReceipt> ProbeAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken);
}
public interface IWarningAuditReceiptWriter
{
    ValueTask<SourceWarningReceipt?> CommitAsync(SourceWarningIdentity identity, CancellationToken cancellationToken);
}
public interface ITrustedImpactGate
{
    ValueTask<SourceImpactState> CheckAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken);
}
public interface INativeKeySemantics
{
    SourceNativeComparisonReceipt Compare(SourceRegistryBinding binding, SourceNativePurpose purpose, int fieldOrdinal, QueryPackField field, SourceNativeValue left, SourceNativeValue right);
}
public interface ITrustedReturnedValuePolicy
{
    SourceValueClassificationReceipt Classify(SourceRegistryBinding binding, int fieldOrdinal, QueryPackField field, SourceNativeValue value);
}
public sealed class SourcePageKernel
{
    public SourcePageKernel(ITrustedSourceAuthority authority, ITrustedPageHistory history,
        ISourcePageTransport transport, ITrustedConnectionPermission permissions,
        IWarningAuditReceiptWriter warningAudit, ITrustedImpactGate impact,
        INativeKeySemantics nativeKeys, ITrustedReturnedValuePolicy valuePolicy, TimeProvider timeProvider);
    public ValueTask<SourcePageResult> RunPageAsync(SourcePageRequest? request, CancellationToken cancellationToken);
}
```

Associated exact shapes: `SourceAuthorityState { Current, Missing, Revoked, Drifted, Unsupported }`; `SourceAuthorityResolution(State, SourceRegistryBinding?)`; `SourceRegistryBinding(Scope, PairId, QueryPackExpectedBindings expected, string trustedFirstSqlSha256, int trustedLocalPackRevision, int trustedLocalPolicyRevision, string? trustedUidField, string trustedSchemaVersion, string trustedNormalizationVersion, string trustedRepeatabilityReviewReference)` freezes expected source/modules/policy sets and exposes them only internally. A Current resolution is insufficient until its exact binding is matched to the candidate. `SourceHistoryState { Initial, Previous, Unavailable }`; `SourceHistoryResolution(State, DateTimeOffset OriginalStartedAtUtc, SourcePageReceipt? Previous)` supplies original start even for Initial, never replaced by request time. The named history adapter stands for later durable receipts; it is scripted in this packet.

`SourcePermissionReceipt(Scope, PairId, Guid Generation, string MinimumReadSetId, string MinimumReadSetVersion, PermissionProbe Probe)` snapshots capabilities; equality of scope/pair/generation/minimum-read bindings is required before existing PermissionAttestation classifies it. `SourceWarningIdentity(Scope, PairId, PageOrdinal, Generation, MinimumReadSetId, MinimumReadSetVersion)` plus `SourceWarningReceipt(Identity)` is an exact protected durable-audit acknowledgment supplied by the named writer, not a caller flag. No receipt/mismatched receipt refuses. Each fresh generation has a new warning identity; retries within an adapter preserve exact identity. `SourceImpactState { Continue, Unknown, Stop }`; `SourceNativePurpose { Paging, ObjectIdentity }`; `SourceNativeOrder { Less, Equal, Greater, Unsupported }`. `SourceNativeComparisonReceipt(Binding, Purpose, FieldOrdinal, Order)` and `SourceValueClassificationReceipt(Binding, FieldOrdinal, Disposition)` contain the exact frozen registry binding, including source/build, pack/query/policy semantic identities/digests and local revisions. The kernel matches that binding and requested purpose/field ordinal before using either decision. Missing, substituted or stale policy/comparator binding refuses with no typed values in the result. These protected receipts use constant ToString; they contain no compared/classified value. No built-in native comparator, content detector or success-returning production implementation is supplied. `SourceBoundCommand` holds the exact protected SQL, phase, typed PageSize parameter, optional typed boundary parameter, request identity, finite timeout and generation; no parameter inference or string interpolation.

### Required call order and finite deadlines

1. Freeze/validate request without ports. Acquire single-call ownership; concurrent call refuses. Build identity. Cancellation closes before any port.
2. Resolve named authority, load trusted history, compare exact registry/history/request bindings and original start. Query-pair structural preflight: existing QueryPackPreflight unchanged for continuation; NEW first-family AST validator plus NEW ordered projection verification for both. Refused projected declarations produce no-value ordinal gaps and no open/query.
3. Derive effective limits, original-start wall-clock run deadline and retention boundary; persisted elapsed time is never reset on retry. Negative/future starts, malformed counters and widened previous limits refuse. If no remaining rows/bytes/time/retention, close before open. Create a linked cancellation deadline for the narrower remaining run duration/CommandTimeout; timeout aborts the entire attempt including open/probe/read, not just execute.
4. Open a fresh generation; reject empty generation. Revalidate authority on that generation. Probe permissions on that generation; require exact receipt binding and existing minimum-read/no-write decision. If excess read-only, commit and match warning identity before execute. Check current impact/cancel/time/retention before execute. Construct exact protected bound command; no query on any failed gate.
5. Execute once. Check exact ordered returned schema before values. Read at most RequestedPageSize row results. Check cancel/time/retention/impact before each read. Validate row ordinal, actual native payload type/width/nullability/field bytes and cumulative page/total bytes before retention in a minimized row. Classify via the named value policy; prohibited/unclassified refuse/quarantine, excluded/redacted create no-value markers. A redacted/excluded key cannot continue and is quarantined. Validate server key order/boundary only via native-key port; preserve both row refs on conflicts. Retain optional approved UID conflicts with both rows. No speculative read follows a full page. A short page has one explicit End read and Terminal=true.
6. Before admission revalidate current authority on the same generation and check impact, cancellation, original deadline and retention again. A full page at cumulative row cap is Partial/RowCap; valid rows remain protected with no next-work continuation. Otherwise PageReady with explicit terminal/full semantics and next key only for nonterminal. Produce in-memory receipt; no ledger/files written. Dispose reader/connection on every opened path. Cleanup exceptions close a result rather than leaking details or claiming successful admission. SourcePageKernel does not catch process-fatal exceptions.

A trusted-port exception produces Refused/PortFailure; a transport exception produces Disconnected/TransportFailure. OperationCanceledException with an actually canceled caller/deadline maps to caller Canceled, original-retention Expired, or TimedOut/Deadline in that precedence. An unsolicited dependency cancellation while those signals remain current maps to Refused/PortFailure or Disconnected/TransportFailure according to the throwing port; it must not invent a caller cancellation or elapsed deadline. Returned source bytes never enter exception text, stderr or ToString. Result reasons are fixed enums. No cancellation path returns an admitted page. Actual retention-crossing tests use trusted TimeProvider advancement between source operations, plus the existing RR-S04 real-clock persisted-retention regression remains applicable.

### Literal implementation/test ownership

Owned NEW source files are `CollectorSourcePages.csproj`, `packages.lock.json`, `README.md`, `SourcePageContracts.cs`, `SourcePagePorts.cs`, `SourceQueryPairPreflight.cs`, `FirstPageSqlValidator.cs`, `SourcePageKernel.cs` under `src/collector/CollectorSourcePages/`. Owned NEW test files are `CollectorSourcePages.Tests.csproj`, `packages.lock.json`, `README.md`, `Program.cs`, `FirstPageSqlChecks.cs`, `SourcePageChecks.cs`, `ScriptedSourcePorts.cs` under `tests/unit/CollectorSourcePages.Tests/`. Smaller internal helpers may be kept in these same files; no unowned/shared file changes. The own assertion Program catches failures, prints metadata-only check names and exits nonzero, avoiding unhandled-crash evidence loss. Test fixtures use explicit fictional SQL/schema/build/identifiers/integers and port names; none establishes vendor eligibility or permissions.

SP01–SP17 are executable in this finite packet, including substituted/stale source, history, permission, native-comparison and value-policy receipt bindings. Missing/revoked authority and projected declarations are rejected before opening; failed connection-bound gates are rejected before execute. Add exact trace spies for initial authority/history -> open -> revalidate -> probe -> warning when required -> impact -> execute -> schema -> read gate/rows -> final revalidate/impact -> disposal. Every refused pre-execute fixture asserts Executions=0, no bound command, and no caller-provided grant path. Trace cancellation at open/probe/execute/read and independent fake-TimeProvider advances verify linked-token propagation and final gate checks; actual cancelable-operation timeout verifies the finite deadline path. SP18/SP19 remain unimplemented external integration gates.


### Coordinator review addendum before implementation

The coordinator reviewed and accepted the literal RR-S06 API and SP01–SP17 scope on 2026-10-03 before code. The transport and permission adapters must share an internal active-generation registry: the permission port resolves the exact currently open connection for the supplied generation and performs its approved probe on that connection. A UUID alone is not permission evidence; a separately opened probe connection, stale generation or unregistered generation refuses. The scripted transport/permission fixtures share such a registry and test its identity; the physical registry/probe adapter remains SP19.

At attempt entry the kernel records UTC plus a monotonic TimeProvider timestamp. It derives a monotonic admission ceiling from the narrower original UTC remaining run duration, remaining retention and configured command timeout. UTC checks and elapsed-monotonic checks both apply before protected loads/admission. A wall-clock rollback during open/probe/read cannot renew the attempt or the original extraction age; wall-clock advance can expire sooner. The linked timeout uses that same bounded remaining duration. Future monotonic clock behavior and production TimeProvider use must satisfy these invariants.

The page/receipt are provisional until reader and connection disposal complete. Any disposal failure discards provisional typed outputs and returns only the closed metadata refusal/error; cleanup never converts a failed page into success. These clarifications preserve the frozen interface signatures and introduce no physical adapter or persistence behavior.


### Finite source-authority and UID reconciliation scope

The coordinator selected explicit current-page UID conflict scope for this finite kernel. Native paging comparisons cover the prior receipt's actual last key and every key in the current page. Optional approved UID conflict comparisons retain both current-page occurrences only. The admission receipt does not contain a prior-page UID inventory. Cross-page UID/relationship conflict discovery belongs to subsequent persisted history and baseline reconciliation, which requires a separately frozen inventory/semantics contract; current-page success must not be presented as extraction-wide deduplication or absence of conflicts.

`ITrustedSourceAuthority.ResolveAsync(request, token)` must validate the entire requested query pair against the currently promoted, signed immutable registry definition before returning Current. That obligation includes exact ordered fields/native types/nullability/classification, parameter declarations, artifact bounds, key/impact/repeatability review bindings and both SQL digests, local revisions, policy/normalization/schema and source compatibility. `QueryPackExpectedBindings` contains only a subset of that complete definition. The kernel separately checks returned registry identity/digests, structural SQL and receipt bindings; those checks cannot transform candidate metadata or a scripted Current result into signed artifact/source-access proof. A physical authority implementation must perform the complete signed-definition comparison and current customer/source authorization. No such implementation ships in this packet.


### Independent-review correction contract — RR-S06

Independent immutable-source review of 18311aebbbdb9bf8bbfb3f14d56d3f2f0fe15298 reproduced two incorrect metadata outcomes: an unsolicited reader OperationCanceledException was labeled user Canceled, and a received oversized varchar payload was excluded from BytesObserved before width refusal. The coordinator authorized the following exact corrections before code.

An OperationCanceledException alone is not proof that the user canceled or time expired. Recheck actual caller cancellation, original retention, UTC/monotonic deadline and linked timer. If none is signaled, return the same closed transport/dependency failure used for that port's other exceptions. Preserve caller cancellation first, true original-retention expiry second and true deadline third; no exception message/token/source value appears in the result.

After receiving a row, count its measurable native payload bytes before refusing native schema/type/width/content. Measurement is independent of admission: known SQL integer native widths are 1/2/4/8 bytes, uniqueidentifier 16, actual binary array length, nonnegative reported native text byte length, and SQL null zero. A reported text length does not make a malformed/oversized value eligible. Never infer a negative, absent or unsupported native length. A row with any unmeasurable payload closes Refused/NativeValueInvalid; the counter preserves only independently measurable portions already received, and no typed page/receipt is returned. Checked accumulation overflow closes Refused/InvalidInput, preserves the prior finite counter prefix and never wraps. No accepted payload retention/classification occurs until row shape, native value validity and all field/page/total-byte guards pass.

For the independently scripted two-column row int plus returned varchar(16) with 17 actual reported bytes, RowsObserved=1 and BytesObserved=21 before NativeValueInvalid refusal. The later invalid field must not disappear from accounting merely because its width guard failed. This is measurable native payload accounting, not a claim about CLR allocations, encoded provenance or physical staging quota. Source transport still owns sequential bounded physical loading; no extra reader call occurs to calculate counters.
