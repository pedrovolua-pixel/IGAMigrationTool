# Local synthetic evaluation workflow engineering contract v1

Status: ENGINEERING FREEZE CANDIDATE — coordinator and nonauthor review required before dependent code
Date: 2026-10-03
Authority: [cycle04 plan](../../plans/active/local-pilot-m08-evaluation-workflow-cycle04.md), [EV02 approval](local-evaluation-policy-approval.md), approved [product](product-spec.md), [technical specification](technical-spec.md), [reviewer policy](local-evaluation-reviewer-contract-proposal.md), [domain expectations](local-evaluation-workflow-domain-test-plan.md). Accepted ADR0001–0003 and existing authorization/history policy control. This bounded newly authorized engineering contract grants no customer/public API, new role, retention choice or production approval. Historical cycle03 no-persistence limits remain true for that completed packet.

## Components and frozen fixture

New SyntheticEvaluationWorkflow module, module-owned migrations/synthetic-evaluation-workflow/001-initial.sql and focused tests; coordinator owns project/configuration/authoritative seed construction and dedicated opt-in loopback host, frontend owns presentation. Reuse pinned Npgsql10.0.3 and SyntheticEvaluation only. No cross-module tables, source-fence adapter, raw resolver or provider. PostgreSQL18 loopback host exactly localhost/127.0.0.1/::1, database prefix iga_synthetic_evaluation_, schema synthetic_evaluation_workflow. Validate parsed connection options before opening, command timeout15s, parameterized values and qualified SQL; refuse unexpected server major.

Immutable existing120-fictional-member population and admitted100 general-AI members (40mandatory, lower allocations15/45). Existing composed oracle golden governs exact identities/order: population digest 069dd6f848250dfa46be775b382d563f7979e15e6349a29f59d4770a55955aa1, sample digest 5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110. Original23 sample bindings remain byte-identical after review/registry/cutoff changes. One server-seeded authenticated qualified Consultant synthetic-reviewer with two exact assignments synthetic-env-a-assignment/synthetic-env-b-assignment. Scope: synthetic-customer/synthetic-scope/synthetic-env-a or synthetic-env-b/synthetic-assessment/synthetic-evaluation; category synthetic-category. Seed validity2020-01-01→2100-01-01UTC is fictional development metadata, not actual human authority. Frozen originals contain only fictional text/references.

No wildcard or client authority/assignment-admin route; fixed read-only assignments. Single independent reviewer changing their own decision is attributable history, not a second vote/last-write adjudication. Multi-reviewer disagreement, source adapter, actual assignments, evidence retrieval, desired-outcome approval, finding dispositions/health scores, queue, risk, publication, retention and production remain deferred.

## Closed internal API and HTTP-shared DTO shape

Namespace SyntheticEvaluationWorkflow; immutable detached records/read-only lists. Trusted seed/actor/registry are never HTTP authority inputs. Shared JSON uses camelCase, exact string enums, explicit nullable fields; reject unknown/duplicate properties. References follow existing synthetic- ASCII128 grammar; digests lowercase SHA25664hex; UUID nonempty. Revisions integers0…9007199254740991. Reason/correction text1…2000UTF16, nonblank, no NUL; hostile text inert. EvidenceReferenceIds maximum16, unique ordinal ascending synthetic refs belonging to that original member's frozen permitted refs. Validate bounds before copies. HTTP body limit256KiB.

Closed records and property order:

    enum EvaluationWorkflowCommandKind { Review, PresentationCorrection }
    enum EvaluationWorkflowIssue {
      InvalidInput, Denied, NotFound, RevisionConflict, RegistryConflict,
      SourceConflict, EventConflict, IntegrityMismatch, MigrationDrift,
      NotInitialized, SeedConflict, RevisionOverflow, ClockConflict, WorkflowLimit
    }
    record EvaluationWorkflowCorrection(string? Severity, string? Category,
      string? RootCause, string? Recommendation);
    record EvaluationWorkflowCommand(Guid EventId, string MemberId,
      EvaluationWorkflowCommandKind Kind, long ExpectedAggregateRevision,
      long ExpectedMemberRevision, string ExpectedRegistryVersionId,
      string ExpectedSourceDigest, string ExpectedSampleDigest,
      EvaluationReviewOutcome? Outcome,
      EvaluationOriginClassification? OriginatingClassification, string Reason,
      IReadOnlyList<string> EvidenceReferenceIds,
      EvaluationWorkflowCorrection? Correction);
    record EvaluationWorkflowOriginal(string MemberId, string Title,
      string Severity, string Category, string RootCause, string Recommendation,
      IReadOnlyList<string> EvidenceReferenceIds);
    record EvaluationWorkflowSeed(SamplingInput Sampling,
      EvaluationReviewerRegistryInput Registry,
      IReadOnlyList<EvaluationWorkflowOriginal> Originals);
    record EvaluationWorkflowActor(string IdentityId);
    record EvaluationWorkflowEvent(Guid EventId, long Sequence,
      long AggregateRevision, long MemberRevision, string MemberId,
      EvaluationWorkflowCommandKind Kind, string ActorId, string AssignmentId,
      string RegistryVersionId, string RegistryDigest, DateTimeOffset RecordedAtUtc,
      EvaluationReviewOutcome RecordedOutcome,
      EvaluationOriginClassification? OriginatingClassification,
      string Reason, IReadOnlyList<string> EvidenceReferenceIds,
      EvaluationWorkflowCorrection? Correction, string CommandDigest,
      string PreviousEventDigest, string ContentDigest);
    record EvaluationWorkflowReceipt(Guid EventId, string MemberId, string ActorId,
      long AggregateRevision, long MemberRevision, long Version,
      string SnapshotDigest, DateTimeOffset RecordedAtUtc);
    record EvaluationWorkflowMember(EvaluationWorkflowOriginal Original,
      EvaluationMemberMetadata OriginMetadata, string AssignmentId, long Revision,
      EvaluationReviewOutcome Outcome,
      EvaluationOriginClassification? OriginatingClassification,
      bool CanReview, bool CanCorrectPresentation, bool AuthorizedContextSufficient,
      EvaluationWorkflowCorrection? CurrentCorrection);
    record EvaluationWorkflowVersion(long Version,
      DateTimeOffset CorrectionCutoffUtc, long LastEventSequence,
      string RegistryVersionId, string RegistryDigest, string VersionManifestJson,
      string VersionManifestDigest, string? PredecessorDigest,
      string AccuracyCanonicalJson, string AccuracyDigest,
      string WarningCanonicalJson, string WarningDigest, string SnapshotCanonicalJson,
      string ContentDigest);
    record EvaluationWorkflowSnapshot(string SchemaVersion, string ActorId,
      long AggregateRevision, string RegistryVersionId, string SourceDigest,
      string PopulationDigest, string SampleDigest,
      DateTimeOffset SampleCorrectionCutoffUtc,
      IReadOnlyList<EvaluationWorkflowMember> Members,
      IReadOnlyList<EvaluationWorkflowVersionSummary> Versions);
    record EvaluationWorkflowVersionSummary(long Version,
      DateTimeOffset CorrectionCutoffUtc, long LastEventSequence,
      string RegistryVersionId, string ContentDigest, string? PredecessorDigest);
    record EvaluationWorkflowHistory(string MemberId, long AggregateRevision,
      long MemberRevision, string RegistryVersionId,
      IReadOnlyList<EvaluationWorkflowEvent> Events, long? NextAfterSequence);
    record EvaluationWorkflowHistoryReadResult(EvaluationWorkflowIssue? Issue,
      EvaluationWorkflowHistory? History);
    record EvaluationWorkflowReadResult(EvaluationWorkflowIssue? Issue,
      EvaluationWorkflowSnapshot? Snapshot);
    record EvaluationWorkflowApplyResult(EvaluationWorkflowIssue? Issue,
      EvaluationWorkflowReceipt? Receipt, bool AlreadyApplied=false);
    record EvaluationWorkflowSeedResult(EvaluationWorkflowIssue? Issue,
      bool AlreadySeeded=false);
    record EvaluationWorkflowMemberReadResult(EvaluationWorkflowIssue? Issue,
      EvaluationWorkflowMember? Member, long? AggregateRevision,
      string? RegistryVersionId, string? SourceDigest, string? SampleDigest);
    record EvaluationWorkflowVersionReadResult(EvaluationWorkflowIssue? Issue,
      EvaluationWorkflowVersion? Version);

SyntheticEvaluationWorkflowStore(connectionString, observer=null) exposes InitializeAsync(), SeedAsync(seed), ReadAsync(actor), ReadMemberAsync(memberId,actor), ReadVersionAsync(version,actor), ReadHistoryAsync(memberId,afterSequence,actor), ApplyAsync(command,actor), all optional CancellationToken. Exact filenames anticipated: EvaluationWorkflowContracts.cs, EvaluationWorkflowCanonical.cs, EvaluationWorkflowPolicy.cs, SyntheticEvaluationWorkflowStore.cs, SyntheticEvaluationWorkflowMigration.cs. Coordinator trusted actor fixed server-side, never supplied in body/header/query.

Current source/member presentation requires CURRENT ScoredReview OR PresentationCorrection eligibility for each included member, in addition to current RelatedHistory where versions/history are included. ReadAsync returns all100 current original/member projections and version summaries only when ALL100 have both source-presentation eligibility and separate RelatedHistory permission; otherwise whole Denied, no partial aggregate leakage. ReadMemberAsync requires exact member source presentation plus RelatedHistory, no other member content. Action flags are current separate decisions, context=true only if current ScoredReview permits sufficient context. Workflow member revision differs from registry resource revisions.

ReadHistoryAsync is a distinct MINIMAL member endpoint requiring current RelatedHistory only, no source-presentation action. It returns memberId, current aggregate/member revision, registryVersionId and ordered attributed event metadata/rationale/correction/refs; NEVER generated original/source text, OriginMetadata, selected cohort, other members, score aggregates or mutation flags. Ref metadata does not grant source/evidence retrieval. ReadVersionAsync requires current RelatedHistory for ALL100 but no current source grant because it contains only frozen derived accuracy/warning canonical JSON and workflow version metadata, no source originals or rationale. Former review permission never grants historical access.

Reads have no effects. History page fixed maximum50 events, afterSequence safe-JS >=0, NextAfterSequence last returned sequence if later records exist else null; no gaps/duplication, no metadata of unauthorized members. Workspace never embeds history or full version canonical bytes. Local workflow maximum1000 aggregate revisions after initial0 (including registry updates), maximum1000 review events,1001 snapshots/registry records, then WorkflowLimit denies without writes; this engineering bound is not retention or deletion. Response serialized UTF8 maximum16MiB, coordinator enforces closed error if exceeded; bound JSON text and fixture originals enough to fit100 current members (each original field≤2000, refs≤16),50historyevents and one full accuracy/warning version. No silent truncation. All canonical hashes verified on full raw bytes before parsing, historical/frozen accuracy computed with unchanged originals. All denial results issue+null substantive DTO/metadata, no input echo. Host status issue mapping closed.


## Command and correction semantics

Review allows Confirmed/Rejected/Indeterminate/Corrected only, never actor-written Unreviewed. Current ScoredReview eligibility mandatory. Confirmed/Rejected need context=true, reason and at least1 permitted evidence ref. Indeterminate needs context=false and reason, optional refs, forbids origin/correction. Corrected needs context=true, explicit Confirmed/Rejected origin, reason, refs, at least1 correction dimension. Other Review outcomes forbid origin/correction. Frozen original classification is reviewer-provided explicit assessment of original conclusion; severity disagreement does not infer rejection.

PresentationCorrection requires separately granted action; null submitted outcome/origin, reason, at least1 dimension, optional refs. Existing Confirmed/Rejected becomes Corrected with EXACT prior originating classification; prior Corrected keeps origin; Unreviewed/Indeterminate stays excluded. This cannot invent independent scored outcome. Correction severity/category are free bounded PRESENTATION text, not taxonomy/permission changes. All4 dimensions stored individually, originals and origin metadata unchanged. Evidence refs are frozen permitted fictional metadata, no payload copied/resolved. Every first/later event attributed with exact actor/assignment/registry/time/origin/version. Later review may replace own current decision through new event, never erase history. No outcome record impersonates denied reviewer. Registry revoke creates later limitation version but does not retroactively erase/relabel valid prior independent events.

## Transactions, replay and snapshot versions

One advisory transaction lock734021014 covers migration, seed, all reads/current-policy/integrity, writes and server-only registry updates. ReadCommitted transactions acquire lock BEFORE mutable reads; all operations same ordering. No registry cache. Registry stored in same database/transaction, no cross-database source-fence claim.

Command order: cheap closed admission; open/lock/migration verify; reconstruct persisted seed/registry/events/projections/versions; resolve trusted actor/member/exact assignment; CURRENT action authorization; look up EventId. Exact actor+canonical semantic command match returns original receipt AlreadyApplied=true without writing even if supplied original revision/registry expectations now old, PROVIDED CURRENT action still authorized. Altered payload/actor EventConflict with null receipt. Revoked actor cannot receive former receipt. No prior event: compare frozen source/sample, expected current registry version, expected aggregate/member revisions, overflow/capacity; derive event DB UTC, member projection, new immutable snapshot; observer; recheck registry authorization/integrity before commit; commit. Typed denial/exception/cancellation/timeout rollback all pending rows. Lost response may leave committed state; exact retry reconciles under current policy. No transport token included in semantic digest; every command field including expected revisions IS included.

Trusted server/test-only UpdateRegistryAsync(expectedRegistryVersionId,replacementRegistry) has no HTTP route. Validate capture and frozen exact identity/assignment/member IDs/scopes/categories/role/qualification/grant structure, while permitting fixture version/revision/status/context/resource/customer/lifecycle/conflict changes. Existing action grants may be revoked/restored only within original seeded grant ceiling; category lists may narrow/restore only seeded category set, qualification may become false (basis then null) or restore original basis. No role/scope/identity/member/category addition or broader grant follows. Append registry version, increment aggregate, keep workflow member revisions/eventsequence, append1 outcome version. Revision starts1, increments1; stale expected version conflict. Same lock linearizes revoke/write: review committed before revoke stays history; revoke before review denies. ISyntheticEvaluationWorkflowCommitObserver has exact signature Task BeforeCommitAsync(string operation, Guid? eventId, CancellationToken cancellationToken); it injects failure only, no SQL/authority/registry. Internal test-only method ApplyWithRegistryReplacementForTestAsync(command,actor,EvaluationReviewerRegistryInput replacement,CancellationToken) is visible ONLY to the module's designated test assemblies through InternalsVisibleTo, never host/router/frontend. After pending event/member/snapshot writes and observer callback, this method validates trusted replacement under the same seeded ceiling, appends replacement registry under the HELD transaction/lock, then rechecks current authorization immediately before commit. Replacement must revoke current requested action/identity/assignment/category/context (not grant new authority). Expected outcome is Denied and rollback of ALL pending writes INCLUDING injected registry replacement; this is a deterministic final-policy simulation, not a claim that an independent external revocation was durably committed. Real durable concurrency is separately proven through UpdateRegistryAsync lock ordering. Normal ApplyAsync has no replacement input, re-reads current transaction registry before commit. Observer must never call another store operation while holding the lock. Queue cancellation is unimplemented.

Seed rebuilds sample once, verifies exact independent golden population/sample and100selected/120population; originals exactly selected100, unique frozen permitted refs, exact trusted two-assignment registry. SourceDigest hashes the exact fixed-order source envelope: schemaVersion=synthetic-evaluation-workflow-source-v1, scopeId=synthetic-scope, sampling=SamplingInput in declared record property order, originals=Original records in ordinal MemberId order. The sampling nested Versions/Bindings/Members preserve existing contract record property order with versionbindings enumorder and population ordinal MemberId, secondaryrefs ordinal. Mutable registry is EXCLUDED. Client receives only server-verified source/population/sample digest anchors and relationships, not raw120-source canonical bytes or20nonselected member data; it must not claim to independently hash omitted bytes. Same source+ORIGINAL registry seed digest AlreadySeeded even after valid registry updates; changed seed SeedConflict. Restart never reseeds current revoked registry to defaults. Initial version0: selected100 Unreviewed, memberrevision0, eventsequence0, DB UTCcutoff. Every successful NEW Review/PresentationCorrection and server registry update creates exactly1 immutable version automatically through trusted store orchestration; no reviewer freeze/publication grant. Version=aggregate revision.

Fixed-order workflow manifest: schemaVersion=synthetic-evaluation-workflow-version-v1, version, sourceDigest, populationDigest, sampleDigest, originalSampleVersionManifestDigest, originalSampleCorrectionCutoffUtc, registryVersionId, registryDigest, lastEventSequence, eventsDigest, correctionCutoffUtc, predecessorDigest. This new manifest binds evolving registry/events/cutoff; it does NOT resample/update original23bindings. First predecessor null, later previous snapshot digest. DB clock_timestamp UTC microsecond cutoff must be >= prior or ClockConflict rollback; equal cutoff allowed because version differentiates. Snapshot includes exactly included eventsequence, not later events.

Reuse unchanged EvaluationAccuracyBuilder with original100 GeneralAi members, latest per-member outcomes through cutoff, EvaluationId synthetic-workflow-version-{version}, original scope/population/sample and new workflow-manifestdigest/DBcutoff. Reuse WarningBuilder with unchanged original severity/category/environment/module/rule/model/confidence. No new arithmetic. Store/reconstruct exact canonical bytes/digests. SnapshotCanonicalJson is the exact raw fixed-order JSON envelope schemaVersion=synthetic-evaluation-workflow-snapshot-v1, versionManifestJson, accuracyCanonicalJson, warningCanonicalJson. ContentDigest is SHA256 of these raw UTF8 bytes; client can verify full envelope and nested string relations without recreating encoder escaping. Canonical source follows seed Sampling then Originals ordinalmember; registry follows input record fields with ordinalrecords/categories; command follows record fieldorder; event follows record without ContentDigest. Event previousdigest first64zeroes; eventsDigest SHA256 canonical array of included event digests. Utf8JsonWriter default encoder/no whitespace, invariant O zero-offsetUTC, UTF8 lowercase SHA256. DTOs detached readonly, members ordinal/history sequence/versions increasing.

## Module-owned schema, drift and recovery

Six tables:
- schema_migrations(migration_id PK,script_digest,schema_fingerprint,applied_at).
- workspace(singleton=true PK,source_json,source_digest,initial_registry_digest,aggregate_revision,last_event_sequence,current_registry_version_id).
- registries(version_id PK,revision UNIQUE,canonical_json,digest,recorded_at).
- members(member_id PK,revision,current_json).
- events(event_id UUID PK,sequence UNIQUE,aggregate_revision UNIQUE,member_id FK,member_revision,actor_id,command_json,command_digest,event_json,event_digest,receipt_json).
- versions(version bigint PK,registry_version_id FK,last_event_sequence,manifest_json,manifest_digest,accuracy_json,accuracy_digest,warning_json,warning_digest,snapshot_digest UNIQUE,recorded_at).

Migration only this schema/tables/constraints/indexes plus metadata guard trigger refusing UPDATE/DELETE of registries/events/versions and frozen workspace source columns. Current member/workspace projections update transactionally. Embedded script SHA256 and recorded fingerprint cover columns/order/types/defaults, relations, constraints/indexes/triggers/functions/extraobjects/RLS. Initialization transactional/idempotent, refuses untracked/partial/preexisting unknown objects/history; no destructive cleanup. Every operation verifies script/history/schema/scope. Drift MigrationDrift; missing init NotInitialized. Read and independently replay all events/registries/version relations, canonical digests/receipt/sequence/predecessor/current pointers/memberprojection/source. Extra/missing/tampered rows/digests/links/projections IntegrityMismatch; no repair/read write. Source compiled fixed fixture/golden remains anchor. Stored canonical sourceJSON and its exactdigest are retained module-side and rebuilt, never sent to history-only or client source panels. Unkeyed hashes do not resist privileged attacker rewriting all rows/code/anchors; detection is incomplete/direct drift, not cryptographic authorization/production tamper resistance.

Valid unknown member/version yields NotFound after current identity/scope admission. Cancellation/SQL errors provide no protected exception text. Logs operation/closedissue/correlation only, no rationale/correction/evidencerefs/connectionstring/SQL. Disable new host/flag for rollback, retain data/compatible reader; no down migration/DROP/retention clock/sharedDBrestart. Corrupt state preserved for inspection; replacing fictional database is separately authorized, not automatic repair. Backup/RPO/RTO/productionauth/manual/live review remain NOT VERIFIED.

Mutable latest-only history rejected under EV02-R03; event sourcing all product state rejected under ADR0003. Resampling on events rejected for immutablecohort. Revision-before-exactretry rejected for lostresponse durability; CURRENT authorization still precedes retry. Partial aggregatecounts rejected for hiddenenvironment leak; narrow memberhistory provides scoped alternative. Original policy source/goldens/historical host/UI untouched. Full M08/Phase1C/TP012/019/G1–G9 remain open.
