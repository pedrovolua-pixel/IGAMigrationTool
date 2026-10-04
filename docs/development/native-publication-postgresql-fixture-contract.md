# RR-P05: native publication PostgreSQL fixture contract

Status: DRAFT FOR COORDINATOR AND INDEPENDENT REVIEW — documentation only; persistence implementation not authorized by this document
Date: 2026-10-03
Owner: Native publication worker; coordinator owns solution, CI, canonical plans/status and deployment
Native core baseline: `8881c1cf4efe214afe4779221970070d4ecfc4db`

## Purpose and governing contracts

Implement the reviewed publication store against real PostgreSQL using fictional protected source and authority records. Prove atomic visibility, immutable content, exact receipts, restricted logins, races and the session-held emission fence. This supplies M09 / Phase 1C engineering evidence needed before M11 / Phase 1D integration. It does not establish an eligible One Identity baseline, terminal product source, actual identity/provider permission, central audit integration, customer encryption, production grants, recovery or G6/G7/G8/G9 acceptance.

Source of truth: [native contract](../../specs/003-health-assessment/native-publication-v1-contract.md), [literal oracle](../../specs/003-health-assessment/native-publication-v1-oracle-addendum.md), [test plan](../../specs/003-health-assessment/native-publication-v1-test-plan.md), [implementation plan](native-publication-v1-implementation-plan.md), [ADR0001](../../architecture/decisions/ADR-0001-pilot-application-shape.md), [ADR0002](../../architecture/decisions/ADR-0002-pilot-tenant-isolation.md), [ADR0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md), [authorization matrix](../security/health-assessment-authorization-matrix.md) and [audit policy](../security/health-assessment-audit-policy.md).

The frozen core has independently executed 157 codec and 58 scripted-flow checks at this baseline. These are not the 18 persisted cases below. Preserve all 31 original vectors from `51272d2`; a database implementation cannot regenerate them or weaken the synthetic ReportDrafts/AssessmentRuns/SyntheticMcp schemas. Any consequential amendment is documented and independently reviewed before dependent code under the pilot exception; no new human design-approval pause is implied.

## Bounded implementation and ownership proposal

| Owner | Proposed next-packet paths | Boundary |
|---|---|---|
| Persistence worker | `src/server/modules/ReportPublication.PostgreSql/**`; `migrations/report-publication/001-native-fixture.sql` | Separate assembly/namespace, actual Npgsql store/audit/blob/context implementation; additive explicit fixture schema. No core type or namespace overwrite. |
| Independent verifier | `tests/integration/ReportPublication.PostgreSql.Tests/**` | Actual restricted-connection fixtures, protected fake source/authority adapters, clocks, races, adversarial direct SQL and evidence. Assembly name `ReportPublication.Tests` retains the existing core test-only source-construction friendship. |
| Coordinator | Solution/CI/shared configuration/canonical records/private site; any required core friendship change | No ordinary-host registration, startup migration, HTTP/MCP/provider/cloud activation. |

This document is the author's sole owned RR-P05 design path. The earlier proposal `src/server/migrations/report-publication` is superseded by repository-root `/migrations/report-publication`. These are proposals until the coordinator freezes exact paths and reviewers close findings. No migration, role, database or product code is created now.

The adapter references the existing `ReportPublication` project and exactly pinned `Npgsql [10.0.3]` with locked dependencies matching the repository family. No additional package, extension, renderer, HTTP client or provider SDK. PostgreSQL built-in SHA-256 suffices; no `pgcrypto` provisioning. Coordinator selects and records the actual local/CI PostgreSQL binary/image version before tests, rather than inferring it from this design.

## Exact adapter API and composition

The core port signatures stay unchanged. New public adapter entry points are limited to:

```csharp
public sealed record PostgreSqlPublicationBindingV1(
    Guid CustomerId, Guid StreamId, Guid WriterBindingReference);

public sealed class PostgreSqlPublicationSessionV1 : IAsyncDisposable
{
    public PostgreSqlPublicationSessionV1(NpgsqlDataSource execution,
        PostgreSqlPublicationBindingV1 binding);
}
public sealed class PostgreSqlPublicationStoreV1 : IPublicationStoreV1
{
    public PostgreSqlPublicationStoreV1(PostgreSqlPublicationSessionV1 session);
}
public sealed class PostgreSqlImmutableFixtureBlobsV1 : IImmutablePublicationBlobsV1
{
    public PostgreSqlImmutableFixtureBlobsV1(PostgreSqlPublicationSessionV1 session,
        NpgsqlDataSource staging, PostgreSqlPublicationBindingV1 binding);
}
public sealed class PostgreSqlPublicationOutcomeAuditV1 : IPublicationOutcomeAuditV1
{
    public PostgreSqlPublicationOutcomeAuditV1(NpgsqlDataSource audit,
        PostgreSqlPublicationBindingV1 binding);
}
```

`PostgreSqlPublicationTransactionV1` implements the existing transaction port internally. No connection, SQL command, schema name, role name or storage locator is a request input or public result. The session is constructor-injected into store, fixture authority/source and availability adapters; there is no static/AsyncLocal/ambient actor, inline grant Boolean or request-selected connection. One fresh session permits exactly one publisher or reader invocation. Reuse/concurrent invocation refuses. The supplied data source resolves one customer fixture database; binding mismatch or missing schema identity denies before content.

Fixture adapters live in the independent integration test assembly: `FixturePublicationAuthorityV1`, `FixturePublicationSourceV1`, `FixtureReferenceAvailabilityV1` and a strict fixture source decoder. The PostgreSQL assembly exposes only internal typed context operations to its friend `ReportPublication.Tests` assembly. That friend may use the already internal core `SourceCaptureV1` constructor. No new public arbitrary-JSON source proof factory or core friend to an ordinary host is introduced.

The fixture authority receives the actor/request before opening its scoped execution backend, acquires the locks below, then verifies persisted fictional subject/session/assignment/customer/category/field/lifecycle facts. Publish fixtures preserve Consultant, warning acknowledgment, exact actor/session/security-version/action and original recent MFA/CA evidence; a role string alone is insufficient. Complete report read is an explicitly fictional complete-field fixture grant. Current IdentityPolicy supplies no production mapping of all 17 native fields; this packet must not infer such a mapping or relabel the fixture as that adapter.

`CaptureAsync(transaction,actor,command,fence,ct)` checks identical session/context/actor/scope/fence bindings. It reads only minimized required-set and revision metadata, invokes the supplied current fence for every required category and all 17 native fields, then loads protected fictional source bytes. It strictly decodes and reconstructs native SourceCapture, compares its exact canonical source/projection/score digests to seeded metadata, and returns defensively owned values. Unresolved classification, nonterminal state or mismatch denies before staging. Test spies distinguish metadata reads from protected source loads. The source is neither a client JSON endpoint nor an AssessmentRuns terminal-state extension.

## Database identity and finite schema

Use a fresh named loopback fixture database per fictional customer, each with fixed `report_publication` and `report_publication_fixture` schemas. One database identity row binds schema version, migration digest and CustomerId; every scoped function checks it and `SESSION_USER` bindings. A second database proves customer separation and progress. No global content deduplication or arbitrary schema/database selection. PostgreSQL storage is a local immutable blob fixture, not the production encrypted object store selected by ADR0003.

Notation: `S` is `(customer_id,project_id,environment_id,assessment_id)` with nonzero UUIDs. Digest `D` is exactly 64 lowercase hex characters. Native revisions/sequence are positive signed64; lengths/counts are nonnegative signed64 with the frozen operation-specific bounds. Canonical bytes are `bytea`, never JSONB storage. Each unique/FK lookup has its matching composite primary/unique index; no cascading delete.

| Table | Exact logical columns and constraints |
|---|---|
| `report_publication.database_identity` | Singleton schema version `native-publication-postgresql-fixture-v1`, migration digest D, CustomerId UUID. Inserted only by controlled migration/seed; no runtime alteration. |
| `report_publication.login_bindings` | `(login_name name,S,kind,stream_id,writer_binding_reference)` composite PK; kinds Publisher/Reader/Audit/BlobStager/FixtureController. One explicit row per allowed scope; the separately configured platform-outcome binding permits null event scope only for Audit. No scope JSON array, runtime writes or role-name input. |
| `report_publication.blobs` | `(CustomerId,kind,digest)` PK; kind Projection/Score/Manifest; `bytes bytea`, byte_length bigint; check length = octet_length(bytes), digest = SHA256(bytes), positive length within native kind bound. Immutable; committed independently before visibility. |
| `report_publication.versions` | `(S,report_version_id)` PK; unique `(CustomerId,report_version_id)`; run_id/revision, original manifest_bytes/digest, projection_digest, score_digest, source_digest, created_at_utc exact text plus ticks; native schema constants. No raw source/prose columns; immutable manifest holds closed minimized metadata. |
| `report_publication.artifact_inputs` | `(S,report_version_id,kind)` PK; two rows Projection/Score with digest/length; FK to version deferred, FK to exact customer-scoped blob; matching manifest descriptor validation. Manifest blob linked by version.manifest_digest. Immutable. |
| `report_publication.publication_receipts` | `(CustomerId,operation_id)` PK; S, exact actor four-field bytes, command_digest, original canonical receipt bytes/digest, report_version_id, event_id/digest; exact deferred version/event links. A changed scope within the same customer operation conflicts without revealing the other scope's receipt. Separate customer databases have independent operation namespaces. |
| `report_publication.read_receipts` | `(CustomerId,invocation_id)` PK; S, exact actor bytes, request_digest, report_version_id, manifest_digest, canonical receipt bytes/digest, event_id/digest; deferred exact read-event/version links. Conflict returns only Conflict, no other receipt. |
| `report_publication.audit_streams` | StreamId PK, CustomerId, writer binding UUID, head_sequence >=0, head_digest D (all-zero only genesis), last_event_ticks. Mutable head only through restricted audit functions. |
| `report_publication.audit_events` | EventId PK; StreamId/sequence unique; original canonical event bytes/digest, previous digest, exact event time text/ticks, operation_id nullable, invocation_id, action/outcome. Native closed event; immutable. S/actor/resource fields remain inside the payload-free canonical event with exact typed projections for deferred checks. |
| `report_publication.lifecycle_events` | `(S,report_version_id,revision)` PK; prior revision, Active/SoftDeleted/Expired, read_blocked, supplied expiry text/ticks, hold_reference nullable; fixture authority/control reference and event ID. Append-only; current projection `report_lifecycle` updated only by fixture-controller function. A hold neither resets expiry nor grants access. |
| `report_publication_fixture.source_revisions` | `(S,run_id,revision)` PK; terminal state, source/projection/score digests, required category/field metadata, source_bytes bytea, provenance/retention/input commitments. Seeded fake source only; immutable. `source_current` pointer is controller-only and revision-checked. |
| `report_publication_fixture.authority` | Exact actor four fields + S key; positive authority revision, source-owned role/condition facts, full category/field sets, active/revoked flags, original provider/session/assignment/customer-policy/MFA/action cutoff text/ticks, approved fixture provenance. Controlled fictional facts; never request-bound grant bits. |
| `report_publication_fixture.references` | `(S,reference_id,revision)` PK; current availability/reason + original exact expiry text/ticks, controller decision reference; append-only history and controller-only current pointer. No raw evidence or locator. |
| `report_publication_fixture.fence_contexts` | One row per `(backend_pid,backend_start)` plus random ContextId, SESSION_USER binding, exact actor/S/mode/resource, original deadline text/ticks, Admission/ActiveTransaction/CommittedReadOnly state. Created by verified open_fence before the business transaction, never caller-written. Cleanup deletes only this ephemeral row; no immutable business row deletion. |
| `report_publication_fixture.clock` | Singleton exact UTC7 text/ticks plus monotonic generation; fixture-controller only. Trusted deterministic fixture clock for database sampling and corresponding fixture TimeProvider. Production database/system-clock integration is unproved. Clock advancement deliberately takes no authority/emission lock: locks cannot stop time. |

The implementation must render the compressed logical keys above into explicit SQL columns, FKs and indexes; no JSON array of composite scopes or generic entity registry. Fixture authority/source/reference schemas are local tests and are not production domain schemas. Native real retention/audit purge/tombstone/restore jobs remain absent; versions/blobs/events/receipts cannot be runtime deleted.

## Restricted logins, owners and migration

Provision separate actual local logins per fixture database: `rp_publisher`, `rp_reader`, `rp_audit`, `rp_blob_stager`, `rp_fixture_controller`, and explicit `rp_migration`. Tests may suffix these names to isolate concurrently owned databases; accepted names are seeded bindings, never runtime parameters. All execution logins are NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION/NOBYPASSRLS and have no membership in migration/function-owner roles. Credentials remain ephemeral local test inputs and never enter commits, receipts, logs or site.

Separate NONLOGIN owners own publication functions/tables and fixture-control functions/tables. Owners have only their exact DML requirements: immutable tables SELECT/INSERT, mutable heads/current projections SELECT/INSERT/UPDATE; no application owner DELETE on immutable tables. The fence-context function owner may DELETE only its ephemeral backend context rows for cleanup. A controlled migration identity owns the migration boundary and may assign ownership to these NONLOGIN owners. Only the isolated test provisioner may create fixture database/logins; migration001 itself creates no login, password, grant to an arbitrary existing application or startup execution.

Revoke PUBLIC schema usage, table/sequence access and function execution. Functions use `SECURITY DEFINER SET search_path=pg_catalog`; schema-qualified names only; fixed owner, explicit grants by full signature, no caller SQL or dynamic identifier. Revoke CREATE in PUBLIC, fixture and publication schemas from execution logins. Explicit default privileges for the owners deny PUBLIC on later functions/tables. SQL checks `SESSION_USER`, not `CURRENT_USER`, so SET ROLE cannot impersonate an approved login. Wrong binding, alternate login, missing ownership, schema drift or missing grant fails closed with fixed safe error categories.

| Login | Exact allowed surface | Must refuse |
|---|---|---|
| Publisher | Open/recheck fixture publish fence, minimized source metadata, authorized fake source load, receipt resolution, exact version metadata, successful Publish audit append, add publication/receipt; staged blob verification via BlobStager adapter. | Direct table INSERT/UPDATE/DELETE, source/authority/lifecycle mutation, read audit/history expansion, migration/DDL, foreign binding/scope. |
| Reader | Open/recheck exact fixture read fence, exact version/lifecycle/receipt metadata, authorized committed blob/reference reads, successful ReadExact audit and read receipt append. | Publication/source capture, blob staging or staged blob reads, publication receipt append, source/lifecycle mutation, direct DML/DDL. |
| Audit | Separate payload-free Denied/Failed/Cancelled outcome append on its configured stream; safe schema/actor-null/scope-null rules. | Successful publication/read event, report/source/blob content, receipt/version writes, lifecycle or binding changes. |
| BlobStager | Create-if-absent and verify immutable customer-scoped staged blobs, bound to configured scope. | Version/receipt/audit/source/authority data, replace/update/delete content, cross-customer existence/bytes. |
| FixtureController | Explicit fixture seed/mutate/clock helpers with shared mutation lock order except clock advancement. | Serving report content as a user; unregistered ordinary application activation. |
| Migration | Apply exact additive migration and controlled owner/binding seed in the new owned fixture database. | No use as publisher/reader/audit credential; no connection to existing customer or another worker's database. |

Successful audit writes occur on the publisher/reader's *same* transaction through tightly bound append functions; the separate Audit login writes only known failures after rollback/fence release. It is not a second transaction for successful audit. Native audit is its own event schema/stream; do not extend IdentitySessions' session event enum or emit AuthorityChanged for publication.

## Exact SQL function surface

All names are in `report_publication` unless prefixed `report_publication_fixture`. `scope json` and `actor json` below are the closed four-UUID/four-field native objects; SQL typed argument shape validation precedes casts. Functions receive parameterized values, not SQL fragments. No request supplies a role/schema/time/head. Private validators and trigger functions receive no execution-login grants.

| Function signature | Port mapping and required behavior |
|---|---|
| `read_identity() -> (version, migration_digest,customer_id)` | Adapter admission; exact compile-time migration/version expectation and bound customer. |
| `fixture.open_fence(actor json,scope json,mode text,resource_id uuid) -> metadata` / `fixture.recheck_fence(actor json,scope json,mode text,resource_id uuid,categories json,fields json) -> metadata` | Fixture authority only; mode exact Publish/ReadExact; persistently resolve original trusted authority facts and deadline, no inline grant result from caller. Acquires known shared session locks, records backend-owned context in autocommit before the business transaction, and verifies login/mode/actor/scope/original cutoff on each use. |
| `fixture.begin_invocation() -> context_id` / `fixture.finalize_invocation() -> void` / `fixture.close_fence() -> void` | Internal adapter calls only. Bind existing backend context to the one active write transaction; finalization rechecks current authority/lifecycle/source/deadline and sets CommittedReadOnly inside the same pending COMMIT. Close removes only the exact backend context and releases its known locks in reverse order after invalidation. No arbitrary PID/key/context input. |
| `fixture.read_source_metadata(scope json,run_id uuid,revision bigint) -> metadata` / `fixture.read_source_bytes(scope json,run_id uuid,revision bigint) -> bytea` | Same transaction/context. Only Publisher. Source adapter authorizes full metadata set through exact fence before protected bytes; SQL independently rechecks current fixture facts/revision/deadline. |
| `lock_publication_receipt(scope json,operation_id uuid,actor json,command_digest text) -> (status,receipt_bytes)` | Transaction-scoped operation lock, then NotFound/Found/Conflict; changed actor/hash/scope Conflict with NULL bytes. Publish retry never performs a fresh source load first. |
| `lock_read_receipt(scope json,invocation_id uuid,actor json,request_digest text) -> (status,receipt_bytes)` | Reader/publisher scoped lookup; exact same conflict semantics. New read uses a new invocation. |
| `read_version(scope json,report_id uuid) -> metadata+manifest_bytes` | Exact committed version in ReadCommitted; no latest/draft fallback; independent lifecycle projection. |
| `put_blob(scope json,kind text,bytes bytea,digest text) -> (kind,digest,length)` / `read_staged_blob(scope json,kind text,digest text) -> bytea` | Only BlobStager; independent autocommit connection. Existing key compares complete bytes/length/hash; conflict cannot replace. |
| `read_committed_blob(scope json,report_id uuid,kind text,digest text) -> bytea` | Only exact authorized read context; digest must be linked to that active exact report; no global blob search or staged visibility. Adapter uses the execution backend/token/fence, not BlobStager credentials. |
| `read_reference_availability(scope json,report_id uuid,reference_ids uuid[]) -> linked rows` | Exact bounded set only after full field/category revalidation; no raw resolver. Core copies before validating/delivering the current overlay. |
| `lock_audit_head(stream_id uuid,binding_id uuid) -> (next_sequence,previous_digest,event_at_utc)` / `append_audit(stream_id uuid,binding_id uuid,event_bytes bytea,event_digest text) -> (event_id,event_digest,event_at_utc)` | Same owning transaction, exact SESSION_USER/action/outcome binding. Writer samples fixture database clock *after* head wait, constructs typed native event and preserves supplied intent only; event has no protected text. Revalidate original deadlines after wait. |
| `add_publication(scope json,manifest_bytes bytea,manifest_digest text,receipt_bytes bytea,receipt_digest text)` | Only Publisher; closed exact metadata, source revision/projection/score commitments, all existing blob descriptors and same-transaction audit link; inserts immutable version/descriptors/lifecycle-init/receipt once. No caller-selected event/time/head. |
| `add_read_receipt(scope json,receipt_bytes bytea,receipt_digest text)` | Only Reader; exact successful same-transaction read-event/report/digest/actor/request link; no publication writes. |
| `recheck_source(scope json,run_id uuid,revision bigint,source_digest text) -> boolean` | Owning source equality under existing fence immediately before commit, no source payload read. A Boolean is a result of authoritative persisted equality, not an authority grant input. |
| `read_database_utc() -> exact UTC7` | Store-owned trusted fixture clock sample; no caller time argument. Read-only after commit. |
| `fixture.mutate_authority(authority_bytes bytea,digest text,expected_revision bigint)` | Controller only; closed AuthorityControlV1 below; zero expected revision creates its initial fixture row, positive exact revision appends and projects the next revision. |
| `fixture.append_source_revision(scope json,run_id uuid,expected_revision bigint,metadata_bytes bytea,source_bytes bytea)` | Controller only; closed SourceMetadataV1 below, independently seeded source/projection/score digests and raw canonical source bytes; next revision is expected+1. No runtime source writes. |
| `fixture.append_report_lifecycle(scope json,report_id uuid,expected_revision bigint,state text,read_blocked boolean,expires_utc7 text,hold_reference uuid,decision_reference uuid)` | Controller only; nullable hold; original expiry may only stay or shorten; no state restoration. Appends event and updates current projection with exact previous revision. |
| `fixture.append_reference_lifecycle(scope json,reference_id uuid,expected_revision bigint,availability text,reason text,expires_utc7 text,decision_reference uuid)` | Controller only; frozen enum/reason pairing, exact revision; expiry is source-supplied and cannot be extended; no raw restoration. |
| `fixture.advance_clock(utc7 text,ticks bigint,generation bigint)` | Controller only; text/ticks exact equality and next generation. Controlled UTC rollback is test-only; corresponding fixture TimeProvider elapsed time never rolls back. |


Controller inputs are closed fixture records, never product request schemas. `AuthorityControlV1` has exactly these ordinal-key names: `schemaVersion` (`native-publication-fixture-authority-v1`), `actor` (native four fields), `scope` (native four UUIDs), `revision` (positive counter string), `role` (existing HumanRole token), `conditions` (unique ordinal-sorted existing GrantCondition tokens), `active`/`revoked`/`mfaCaVerified` (JSON bool), `categories` (unique sorted bounded source tokens), `fields` (unique sorted subset of the existing 17-field registry), `sessionExpiresAtUtc`, `providerExpiresAtUtc`, `assignmentExpiresAtUtc`, `customerPolicyExpiresAtUtc`, `privilegedAuthenticatedAtUtc`, `privilegedExpiresAtUtc` (exact UTC7 strings), `privilegedAction` (existing HumanAction token) and `fixtureDecisionReference` (nonzero UUID). The fixture's complete native read permits only explicitly seeded current Consultant/full-field facts; all other roles/partial fields deny in this finite fixture. Publication also requires exact PublishReport/PublishWarningAcknowledged/current MFA/CA facts. These bounds prove fictional port conditions only, not native production field mapping.

`SourceMetadataV1` has exactly `schemaVersion` (`native-publication-fixture-source-metadata-v1`), `scope`, `runId`, `runRevision`, `assessmentState`, `sourceDigest`, `projectionDigest`, `scoreDigest`, `requiredCategories`, `requiredFields`, `inputs`, `retention`, `provenance`. Nested shapes and values are the frozen native oracle; there is no prose/comment/source-object/locator field. Source bytes are the original 12-key native source envelope and hash to sourceDigest; their owned typed decoder proves the exact projection/score/inputs/retention/category/provenance equality after full preload authorization. Initial fixture seed is performed by the controlled provisioner before races; all later helpers acquire the shared lock order and enforce expected revisions. Direct malformed controller input is a negative test, never an eligible source.

Reference overlay pairing is Available/None, Redacted/Redacted or Unavailable with a declared non-None native reason. A controlled evidence expiry produces Unavailable/Expired with the current revision; deleted evidence produces Unavailable/Deleted. Lifecycle helpers cannot mutate frozen bytes or expand access. Full typed calendar, token/size, duplicate/set and revision checks apply before SQL casts.


Private `require_fenced_backend` resolves the registered `(pg_backend_pid(), backend_start)` context, compares exact SESSION_USER/customer/mode/resource/actor bindings and original deadlines, and verifies every required session advisory lock is still granted in `pg_locks` before protected reads or writes. Registration is not itself a grant: each operation rechecks current persisted fixture facts and complete required set. Direct calls after manual unlock, forged context/PID, missing begin/finalization or expired context deny. SQL grants never expose registration table DML or bypass helpers. A recycled backend PID cannot inherit a prior context because backend_start and physical-session lifetime differ. Staged BlobStager operations are explicitly scoped workload staging, not protected source access or exact committed report serving; they do not claim the execution backend's authority.

Receipt lookup functions use only their operation advisory lock while inside the active transaction. Post-commit or reconciliation read-only contexts use separate `read_publication_receipt(...)` / `read_read_receipt(...)` functions with identical safe equality/status output and no operation lock or write. They do not retry COMMIT or append an event. Deferred constraint triggers validate source/manifest/blob descriptor/receipt/audit/lifecycle links at actual COMMIT; successful events must have exactly one corresponding version+publication receipt or read receipt. Orphan successful events/receipts/versions and wrong actor/hash/operation/invocation links cannot commit even through direct function calls. Denial/failure events have no business receipt obligation.

## Byte admission, Unicode, hashes and time precision

Keep original source/projection/score/manifest/audit/receipt bytes. PostgreSQL SHA-256 checks the actual `bytea`, not reserialized JSON. C# strict native codecs/readers perform full closed, duplicate, depth/size/link/set/Unicode validation. SQL repeats closed scalar/nested control schema, exact enums/UUID/counter/decimal-string/null meanings, sorted required sets, hash/length/binding and same-transaction linkage for command/manifest/score/audit/receipts before accepting restricted writes. Unknown schemas/fields, duplicate keys and canonical byte changes deny; JSONB equality alone cannot prove original bytes.

Report/source prose may include the frozen valid Unicode/control/NUL corpus. Never cast its payload bytes through PostgreSQL JSONB, text-unescape functions or a generic SQL canonicalizer: JSONB rejects escaped NUL, and text cannot store decoded NUL. Preserve it as bytea; source controller seeds its original independent commitments, source adapter strictly decodes under authorization, publication SQL checks all source-owned expected hashes, and exact reader verifies bytes again. A BlobStager SQL login can stage arbitrary opaque bytes only within bounds/hash integrity; that cannot create a visible report unless restricted publication/source/descriptor constraints match, and cannot authorize a read. Native metadata contains closed ASCII UUID/digest/version/category/field values; SQL control admission may use JSON/JSONB *after* duplicate and ASCII shape checks. Do not copy a session JSON canonicalizer over full report payloads. [PostgreSQL JSON documentation](https://www.postgresql.org/docs/current/datatype-json.html) explains the NUL and duplicate-key limitations.

For SQL control JSON, compare original UTF8 bytes to an independently checked ordinal-key encoder constrained to the frozen shapes; source/projection byte commitments never change. SQL validators must preserve bool/null/string distinctions, integer counters as strings, decimal strings and exact UTC7 values. Structured binary parameters plus strict UTF8 decoding refuse malformed encodings. Exact full content/manifest limits are the native bounds; engineering refusals cannot be labeled SLO acceptance.

PostgreSQL timestamps have microsecond precision, while native UTC7 has 100ns precision. Store canonical time text and exact signed64 .NET UTC ticks alongside a comparison timestamp; never round-trip canonical input timestamps through timestamptz. Parse the first six fractional digits into PostgreSQL time and add the seventh digit as ticks, validating the exact calendar representation. UTC tick origin is .NET0001-01-01, and input comparison ticks must equal the canonical text. Fixture clock returns its trusted text/ticks from SQL; no caller-selected audit timestamp. The deterministic fixture TimeProvider advances with that same controller-owned clock and independently preserves monotonic elapsed time; tests may simulate UTC rollback without rolling back elapsed time. Clock controls are fiction, not production time-sync evidence.

## Lock order and context lifetime

Execution connections use `Pooling=false` and remain pinned until fence disposal. Session and store share that one explicit backend. No nested second subject-lock connection. Blob staging and known-failure audit use separate bound logins/connections only where stated; neither acquires the authority/emission lock chain or waits on a lock owned by the caller.

Every fixture runtime/controller path acquires the following order:

1. Shared session advisory customer key for runtime; exclusive for customer-policy mutation.
2. Shared session advisory full S key for runtime; exclusive for source/report/reference/assignment/category mutation within S.
3. Shared session advisory actor/session key for runtime; exclusive for actor/provider/session revocation. A mutation spanning scopes acquires sorted ordinal S keys, then sorted actor keys; never reverse the chain.
4. Transaction advisory operation/invocation key; unique receipt lookup serialized.
5. Source/current/version/lifecycle/reference rows required by that operation in fixed table order and UUID order.
6. Audit-head row FOR UPDATE last; append/event/receipt/version rows afterward do not reacquire an earlier lock.

Keys are signed64 hashes of fixed ASCII domain prefixes plus lowercase UUID tuple, using `hashtextextended(...,0)`; key domains/customer/full scope/actor/operation are recorded once in the implementation. Hash collisions serialize conservatively and confer no grant. `pg_advisory_lock_shared`/exclusive **session** locks protect explicit mutations through emission; transaction-level advisory locks cannot be substituted. The invocation owns only its known key set, releases in reverse order and never calls unlock-all on shared connections. Controller mutators must use the same keys even in setup helpers after initial seed; tests prove a competing actual connection waits, rather than trusting an in-process Boolean. [PostgreSQL locking documentation](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS) distinguishes their lifetimes.

Customer-only policy mutation takes customer exclusive and never attempts lower locks while waiting for an operation/audit head. Actor revocation affecting multiple scopes takes customer shared plus the sorted scope chain before actor exclusive. Fixture clock advancement has no authority locks because time must advance during emission. No externally supplied key or operation can create a cross-customer lock lookup. Schema/seed setup occurs before concurrent runtime tests; thereafter all mutators follow the chain.

Contexts: Created -> Fenced -> ActiveTransaction -> CommittedReadOnly or Unknown -> Disposed. Failed/known rollback ends the active transaction before outcome audit. Only one Commit attempt. In CommittedReadOnly, ReadDatabaseUtc/read exact version/current references/read receipt methods are permitted; SQL finalization projected that state in the successful business COMMIT; Append/Add/Commit/recheck-source-for-writing refuse. Each post-commit read runs with server `READ ONLY` transaction settings and current-token/fence/lifecycle checks; database writer locks ended at COMMIT, while session locks remain held. Unknown forbids new writes or COMMIT retry, and its connection is discarded after cleanup. Dispose invalidates every retained context and view before releasing known session locks and closing the physical backend. Cleanup cannot claim success until backend/locks are confirmed released; uncertain cleanup closes/discards rather than returning a pooled connection.

## Visibility, retry and commit uncertainty

Staged blobs commit through BlobStager before any version is visible. Publisher verifies original staged bytes. Version/descriptors/lifecycle-init/publication receipt/successful event/head move together in the owning metadata transaction; deferred checks verify source revision/current authority/clock/lifecycle immediately at COMMIT. Independent readers see either zero version or the complete verified version and linked successful receipt/event. No runtime reader can enumerate or retrieve orphan staged content.

Same customer operation and exact actor/command retry returns the original receipt after current complete authority/lifecycle checks, before stale new-source rejection. Changed actor/security version/scope/hash conflicts. Different new operations may publish distinct immutable versions from the same terminal source. Old source changes do not modify historical report content. New read invocations append new historical read events; exact retry invocation deduplicates audit but reloads/integrity/current authorization/deadlines each time.

`CommitAsync` throws `PublicationCommitNotAppliedException` only when server noncommit/rollback is positively established. Any ambiguous network/cancellation/disposal result after commit attempt becomes `PublicationCommitUncertainException` with only the original opaque reference; no successful return, no false rollback/failure event and no delivery. Tests intentionally discard acknowledgment after a real COMMIT, plus abort before COMMIT; they are not proof of real TCP failure. Reconcile the original receipt through a fresh session, current authority and read-only function; unavailable/conflict/tamper stays no-success. The frozen core's existing scoped Begin/receipt lookup can perform this read-only reconciliation without a new client command API.

Reader acquires the original authority lease before store admission. Exact minimized version metadata can only narrow it to report/lifecycle/retention expiry. Full categories/fields revalidate before any blob/reference load; every dependency uses the original linked UTC/monotonic token. Current metadata and overlay rechecks remain under the session fence after durable read-audit COMMIT and immediately before each protected sink write. The core owns immutable overlay copies; null/duplicate/unlinked rows are IntegrityMismatch. Proven noncommit is Failed/DependencyUnavailable after certain rollback; unknown audit commit emits zero callback. Original expiry or ignored/late callback cannot retain locks indefinitely or acquire/deliver buffers after invalidation.

## Persisted 18-case mapping and evidence

The verifier owns the frozen `fixtures/persisted-cases.json` cases; this packet implements their mechanisms rather than rewriting expected cases. All are initially PLANNED_NOT_EXECUTED.

| Case | Required real PostgreSQL fixture observation |
|---|---|
| NPV-I01 | Separate restricted login and persisted revoked/current category/field facts; metadata/source/blob spies prove denial before protected loads and zero delivery. |
| NPV-I02 | Interruption at each blob/metadata/event/receipt boundary; another actual connection sees no partial version/event/receipt. Staged byte rows may remain but exact read cannot fetch them. |
| NPV-I03 | Failed required audit and cancelled known precommit; atomic rollback; separate known-failure audit only after lock release. |
| NPV-I04 | Two actual Publisher connections, same operation and command; observe operation wait and one committed version/event/receipt. |
| NPV-I05 | Altered actor/session/scope/digest conflicts; changed source pointer after commit still permits exact original freshly authorized replay. |
| NPV-I06 | Actual COMMIT then acknowledgment discard, plus positive rollback control; fresh read-only original receipt reconciliation; no new operation/event. |
| NPV-I07 | Actual controller connection waits on shared session fence; reversal is refused by fixture helper ordering; winner behavior and independent customer database progress. |
| NPV-I08 | Append source score/review/profile revision and second operation; original three blobs/manifest/approval/warnings remain exact. |
| NPV-I09 | Wrong exact scope/version/digest and partial grants; direct SQL/blob/receipt substitution; no fallback or protected load on category/field denial. |
| NPV-I10 | Reference expiry/deletion and nonblocking/blocking hold; linked unavailable overlay, original bytes preserved; report expiry/deletion denies all. |
| NPV-I11 | Actual audit-head row wait; clock crosses original cutoff, append fails or commit unknown; zero callback until valid durable receipt. |
| NPV-I12 | Controller advances trusted DB clock and fixture TimeProvider to minus-one/exact/plus-one; actual session locks remain through valid callback, expiry cancels every paused dependency/write. |
| NPV-I13 | UTC rollback with monotonic advance; ignored cancellation/late callback view cannot deliver; all owned backend locks released and immutable buffers cleared. |
| NPV-I14 | Same read invocation exact request deduplication, altered meaning conflict and new invocation separate event; each retry current authority/lifecycle/integrity. |
| NPV-I15 | Separate real Publisher/Reader/Audit/BlobStager/Controller/Migration logins; direct DML/DDL/SET ROLE/PUBLIC/helper/wrong binding/customer/stream attempts fail; complete grant inventory and owner/search_path checks. |
| NPV-I16 | Protected original/comment/native-object/credential/report sentinels never enter allowed report/error/audit/receipt/trace; no raw resolver or source payload telemetry. |
| NPV-I17 | Existing digest altered bytes via controlled tamper fixture; immutable stage collision refuses; full byte limits and Unicode/NUL originals preserved; no cross-customer dedup. |
| NPV-I18 | Coordinator runs unchanged historical draft/run/review/MCP checks with no ordinary-host reference; architecture/locked dependency/secret/build/format checks and exact evidence hashes. |

Evidence records source/core/migration/oracle/tool/login configuration identities, every executed command, assertion count, original failures and corrected findings, actual pg_locks/pg_stat_activity waiting backend observations, committed row counts and canonical byte hashes. Sanitized evidence contains only fictional UUIDs, categories and safe outcomes. No connection string, protected bytes, SQL customer evidence or credential on the private site. A Boolean grant fixture cannot substitute for actual login restrictions or actual waiting connections.

## Local execution, recovery and deferred acceptance

The coordinator-owned macOS PostgreSQL cluster on port52699 has shutdown requested but a checkpointer remains stuck after storage exhaustion. Do not connect to, delete, restart, reconfigure or claim cleanup of that cluster. Use a separately owned disposable loopback cluster/database or isolated CI service only after its exact local provisioning ownership, free space and startup/cleanup commands are recorded. This document launches nothing. No customer/source/cloud/spending operation is authorized here.

Explicit fixture setup validates loopback host and a fresh `iga_synthetic_native_publication_*` database, owner receipt and schema identity before migration/seed. No schema DROP/CASCADE against an existing worker database, startup migration, production grant, actual provider token or arbitrary connection in a product request. Keep the cluster/data directory and final logs if cleanup cannot be confirmed; don't erase evidence to free space. Local test reset/drop applies only to the independently owned disposable fixture after completion and explicit owner checks.

Rollback removes unactivated adapter references only; committed immutable versions/blobs/receipts/events remain intact. Fixture lifecycle/purge/recovery and encryption are explicitly unimplemented; production restore/tombstone/retention targets and real central control/customer-plane revocation require later adapters and evidence. The single-database advisory mechanism is a fixture fence, not a distributed fence. Production release remains separately authorized.

## Alternatives and review decisions

Selected: immutable bytea fixture blobs plus relational metadata, restricted native audit functions and pinned shared session fence. It exercises ADR0003 port semantics locally without a new cloud/object-store dependency. Production retains customer-encrypted immutable object storage behind the same port. Rejected: changing to JSONB canonical report storage (breaks NUL/original commitments); mutable version rows (breaks history); separate successful audit transaction (breaks visibility); transaction-only advisory locks (end at audit COMMIT); pooled/fresh subject connection during emission (breaks shared lock ownership); session audit schema reuse (wrong event contract); real customer/provider wiring during fixture work (outside packet).

Coordinator/independent review must freeze: exact SQL expansion and per-signature grants against the closed records above; the customer-scoped operation/invocation uniqueness above; test-only context friendship and owning source adapter placement; controlled SQL clock/tick precision; exact hash/key order and unknown-state cleanup. Any unresolved item blocks only its dependent persistence code; the pilot proceeds with independent work. This draft makes no new product permission/retention choice and claims no runtime persistence test success.
