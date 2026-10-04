# Native publication v1: internal engineering contract

Status: PROPOSED FREEZE — RR-P02 design; coordinator decision and nonauthor review pending
Date: 2026-10-03
Source baseline: `810a1eb0288d3af41758991c06c4287b7a75fe46`
Owner: Reporting module owner; coordinator owns integration and canonical records
Authority: Approved pilot execution and AGENTS.md pilot exception
Plan: [bounded implementation](../../docs/development/native-publication-v1-implementation-plan.md)
Tests: [native matrix](native-publication-v1-test-plan.md)

## Outcome and boundary

Implement one internal native publisher and an exact committed-version reader under accepted [ADR-0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md), ADR-0001 and ADR-0002. This supplies the owning publication seam missing from [P02-I01–I05](phase1d-integration-intake-cycle02.md). FR-HAS-21/44/45/46/50, AC-HAS-6/9/14/17/20 and TP-HAS-006/009/014/017/020 govern it.

The first executable packet uses fictional source records and isolated PostgreSQL plus a customer-scoped immutable blob test adapter. It executes the actual native publisher/store/reader, rather than pretending a saved draft is published. It does not integrate an actual completed assessment source, the BFF, UI, Prince, sharing, customer evidence, Azure or an external MCP transport. Those contracts/evidence remain separate. Existing ReportDrafts, SyntheticMcp and their schemas/fixtures/goldens stay unchanged.

No new product permission, retention duration, cloud provider, generalized event framework or dependency is selected. The implementation uses the existing pinned Npgsql family and .NET toolchain. PostgreSQL metadata and a blob interface follow the accepted architecture; an in-database immutable byte table is only the isolated test adapter, never a claim that production blobs are relational storage.

## Existing authority and audit conventions

`IdentityPolicy.HumanAction.PublishReport` already requires Consultant, `PublishWarningAcknowledged`, current exact assignment/resource/category/customer policy and privileged recent MFA/Conditional Access verification. Publication must preserve all of these; possessing an operation/report UUID or digest conveys no authority. Every requested category must pass; partial publication is not an authorization fallback.

`IHumanAuthoritySnapshotSource.ResolveAsync` and `HumanAuthorizer.AuthorizeAsync` alone provide no transaction/revocation fence across control-plane and customer databases. `IdentityAuthority` and `IdentitySessions` demonstrate same-transaction subject locking, immutable command-bound receipts, append-only restricted audit writers and conservative deadline rechecks. Reuse those conventions. Do not open a nested subject-locking connection or assert a distributed fence from a nontransactional snapshot.

`SecurityAuditEventV1`/`SecurityAuditAction` are closed session/authority contracts. Do not add report text/actions to them. This module has a separate closed `report-publication-audit-v1` event and transaction port. Actual central audit/authority/lifecycle adapters and their deployment bindings are follow-on work; fixture authority is explicitly identified as fixture authority.

## Internal API freeze

All records are server-created internal values, never HTTP DTOs. GUIDs must be nonempty. Mutable arrays/bytes/JSON cannot escape ownership: capture construction and returned results defensively copy. No arbitrary JSON content, storage locator, connection, inline authority booleans or client-created source capture is accepted.

```csharp
record PublicationScopeV1(Guid CustomerId, Guid ProjectId, Guid EnvironmentId, Guid AssessmentId);
record PublicationActorV1(Guid TenantId, Guid ObjectId, Guid SessionId, long SecurityVersion);
record PublishCommandV1(Guid OperationId, Guid CorrelationId, PublicationScopeV1 Scope,
    Guid RunId, long ExpectedRunRevision, string ExpectedSourceDigest,
    IReadOnlyList<PublicationWarningV1> AcknowledgedWarnings);
record ExactReportRequestV1(PublicationScopeV1 Scope, Guid ReportVersionId,
    string ExpectedManifestDigest, Guid InvocationId, Guid CorrelationId);
enum PublicationIssueV1 { InvalidInput, Unavailable, RevisionConflict,
    IdempotencyConflict, IntegrityMismatch, DependencyUnavailable }

// SourceCaptureV1 construction is assembly-internal, available to the owning
// source adapter. Test-only friend assembly may build fictional source captures.
// It contains typed bindings/content, not arbitrary DraftReportSnapshot/JSON.
interface IPublicationSourceV1 {
    ValueTask<SourceCaptureV1?> CaptureAsync(IPublicationTransactionV1 transaction,
        PublishCommandV1 command, CancellationToken cancellationToken);
}
interface IPublicationAuthorityV1 {
    ValueTask<PublicationFenceV1?> EnterPublishAsync(PublicationActorV1 actor,
        PublishCommandV1 command, CancellationToken cancellationToken);
    ValueTask<PublicationFenceV1?> EnterExactReadAsync(PublicationActorV1 actor,
        ExactReportRequestV1 request, CancellationToken cancellationToken);
}
interface IImmutablePublicationBlobsV1 {
    ValueTask<StagedBlobV1> PutIfAbsentAsync(PublicationScopeV1 scope,
        PublicationBlobKindV1 kind, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedAsync(PublicationScopeV1 scope,
        PublicationBlobKindV1 kind, string expectedDigest,
        CancellationToken cancellationToken);
}
// NativeReportPublisherV1.PublishAsync(actor,command,ct) returns an immutable
// metadata receipt or a typed issue. NativePublishedReportReaderV1.ReadExactAsync
// (actor,request,emit,ct) returns metadata-only outcome; its trusted callback
// receives the exact verified committed projection while the fence is held.
```

The above names/entry signatures are frozen. `PublicationFenceV1` is an opaque lease owned by a trusted authority adapter, not a public record with caller-controlled `Allowed=true`. Its actor/session/security binding must be revalidated, including the privileged proof for PublishReport. Publication authorization targets the mutable current run resource; an existing Published report is read separately and cannot authorize another PublishReport action. `IPublicationTransactionV1` exposes only owning capture/store/audit operations and lifetime; its concrete PostgreSQL implementation keeps its connection/transaction internal. A capture adapter may access them through a separate internal friend seam, never an ordinary caller cast. Fence acquisition returns null on denial or unavailable authority; neither condition permits source/blob loads. Infrastructure exception detail is not returned. Requested cancellation propagates `OperationCanceledException`; commit uncertainty is handled below.

First reader scope is deliberately narrow: return the complete minimized canonical native report only when its entire frozen required category/field set is authorized. There is no partial-count or field-filtering fallback. Executive/support projections and MCP grants cannot obtain this complete value by reusing a broader role. Later audience/MCP consumers receive their separately authorized projection/partition contract; this packet implements no service-identity or MCP authentication. An exact-version reader is a source capability, not permission to serve all its bytes.

## Closed native schemas

`health-report-manifest-v1`, `health-report-projection-v1`, `health-report-score-v1`, `report-publication-receipt-v1` and `report-publication-audit-v1` are independent native versions. Unknown/missing/duplicate fields, unknown versions/enums and invalid bindings deny. No `syn-*`, `SyntheticApproved`, fictional prose atom or Scoring alias is admitted as native publication state.

The manifest has exactly these fields:

| Field | Shape / binding |
|---|---|
| `schemaVersion`, `projectionSchemaVersion`, `scoreSchemaVersion` | Exact native versions above |
| `scope`, `assessmentId` | Scope object has customer/project/environment UUIDs; assessment UUID repeats exact scope assessment |
| `reportVersionId`, `runId`, `runRevision` | Server allocated report UUID; owning run UUID and positive signed64 revision |
| `createdAtUtc`, `createdBy` | Database trusted timestamp; actor tenant/object UUIDs; no session cookie or token |
| `assessmentState`, `approvalState` | `Completed` or `CompletedWithGaps`; `Published` or `PublishedWithWarnings` |
| `inputs` | Typed source bindings described below |
| `projectionDigest`, `scoreDigest`, `sourceDigest` | Exact original SHA-256 commitments; no transformed digest is relabeled original |
| `requiredCategories`, `requiredFields` | Sorted unique canonical category keys and typed projection field-path names; metadata is protected, not an anonymous catalogue |
| `classification`, `redactionMarkers` | `MinimizedDerivedReport`; sorted explicit typed markers `(section,id,field,reason)` |
| `retention` | Owning policy UUID/version, class `PublishedArtifact`, clock-start UTC, expiry UTC, optional hold reference; does not invent duration or clock policy |
| `provenance` | Sorted `(kind,opaqueRecordId,digest)` input references, no URL/path/native object name/raw resolver |
| `artifactInputs` | Frozen projection/score blob descriptors `(kind,digest,byteLength)`; no locator, precreated PDF, secret-bearing link or presumed successful renderer |

Typed `inputs` has exactly `baselineId`, `baselineDigest`, `capabilityLockDigest`, `ruleCatalogVersion`, `ruleCatalogDigest`, `scoringProfileVersion`, `scoringProfileDigest`, `maturityProfileVersion`, `maturityProfileDigest`, `desiredOutcomeVersion` (nullable), `desiredOutcomeDigest` (nullable paired with version), `scoringAlgorithmVersion`, `maturityAlgorithmVersion`, `applicationVersion`, `reviewSnapshotDigest`, `coverageSnapshotDigest`, `runInputDigest`, `aiPolicyVersion`, `modelVersion` (nullable), `promptVersion` (nullable). Version tokens are nonempty bounded tokens from the owning source; they are not eligibility approvals. A source adapter must verify their actual owning records. Model/prompt versions alone do not prove ZDR/provider configuration or AI acceptance.

The projection contains exactly `schemaVersion`, `scope`, `assessmentId`, `runId`, `runRevision`, `inputs`, `executiveSummary`, `environmentScope`, `scores`, `maturity`, `dimensions`, `coverage`, `findings`, `healthyControls`, `recommendations`, `acceptedRisks`, `warnings`, `methodology`, `technicalAppendices`, `redactionMarkers`. It has no generated originals, free-text review comments, object/native identifiers, raw evidence, SQL/script/configuration examples, credential, storage locator, executable markup or customer topology. Required report sections are present even when unavailable; unavailable status/reason is explicit and never presented as an empty successful result.

| Typed section | Required fields and semantics |
|---|---|
| `executiveSummary`, `environmentScope`, `methodology`, `technicalAppendices` | Arrays of `(id,category,title,text,availability,reason)` minimized display records. Plain bounded text is inert, classified source output; the core is not a free-text sanitizer. Appendix includes methodology/version explanations, never raw evidence/code. |
| `scores` | Exact score schema: `provisional`, `publishable`, `quality`; each `(availability,value,reason)`. Decimal health/quality values in 0..100 or null with explicit unavailable reason. Does not recompute scoring or infer quality from health. |
| `maturity` | `(availability,level,algorithmVersion,inputDigest,contentDigest,reason)`; level is null or Initial/Developing/Defined/Managed/Optimized. It never derives maturity from health. |
| `dimensions` | Sorted `(kind,id,category,provisional,publishable)`; kind Category/ObjectType/Module/DesiredOutcome. Approved customer outcome state comes from source, never from report publication. |
| `coverage` | Sorted `(id,category,state,reason)` with Pass/Finding/NotApplicable/NotAssessed/InsufficientEvidence/Excluded/Inaccessible/Redacted/Unsupported/Error; counts `(planned,executed,gap,notApplicable)` must reconcile to source's exact terminal coverage. A zero-applicable denominator stays unavailable. |
| `findings` | Sorted `(id,category,title,summary,severity,state,method,confidencePercent,mandatoryReview,referenceIds,originalDigest,reviewRevision)`. Severity Critical/High/Medium/Low/Informational; method Deterministic/AI; state Proposed/AutoConfirmed/Confirmed/Rejected/Deferred/AcceptedRisk/RemediationPlanned/InProgress/RemediatedPendingValidation/ValidatedClosed/Reopened. No silent AutoConfirmed-to-Confirmed translation. |
| `recommendations` | Sorted `(id,findingId,category,summary,priority,effort,reviewState)`; bounded classified inert text; reviewState Unverified/Reviewed. Finding linkage must exist in same scope/projection. No executor/task creation. |
| `acceptedRisks` | Sorted `(id,findingId,category,decisionReference,reviewAtUtc,status)` with source-backed customer-risk authority and Current/ReviewRequired status at capture. No report action mints risk acceptance. |
| `healthyControls` | Sorted `(id,category,ruleId,ruleVersion,summary)`; omitted/prohibited content uses explicit marker, not an invented healthy control. |
| `warnings` | Sorted `(kind,recordId,category)`; MandatoryReviewIncomplete/CoverageIncomplete/SourceLimitation. Warnings include every unreviewed Critical/High finding and applicable coverage gap. |
| reference metadata | Each finding `referenceIds` links to sorted appendix-independent `ProtectedReference` records in `technicalAppendices` with `(id,category,availability,reason)` instead of prose fields. Only opaque reference UUIDs; no raw lookup. Frozen availability is historical; current availability overlay is separate. |

The `technicalAppendices` field is a closed object with exactly `notes` (display records) and `protectedReferences` (the reference metadata above); it is not a union determined by arbitrary JSON. Every record category is a canonical source-owned key and must be present in requiredCategories. Record IDs are opaque UUIDs, stable within the source snapshot, not newly generated to conceal missing linkage. Strings are at most 4096 Unicode scalar values; version/category/field-path tokens at most128 ASCII characters, allow letters/digits/period/underscore/hyphen only, case-sensitive. Reference linkage, unique IDs and required section coverage are validated before staging. Structural validity cannot establish that source text is minimized; the trusted capture adapter is responsible and tests include prohibited sentinels.

Availability is `Available` or `Unavailable`, with reasons `None`, `NotApplicable`, `NotAssessed`, `InsufficientEvidence`, `Excluded`, `Inaccessible`, `Redacted`, `Unsupported`, `Error`, `Expired`, `Deleted`; available requires None, unavailable requires another reason and no prohibited value. Frozen reference availability may also be `Redacted` paired with Redacted. Redaction markers do not include original values. Scores/maturity must preserve the owning frozen computations and Proposed/AutoConfirmed semantics, not recompute from this minimized text. Risk lifecycle changes after publication do not rewrite historical Current labels; serving must distinguish a current authorized overlay when later implemented.

## Canonical bytes and boundedness

Use `native-report-canonical-v1`: UTF-8 without BOM/whitespace, ordinal sorted closed ASCII object keys, preserved explicitly ordered arrays, canonical GUID lowercase D strings, lowercase64 SHA-256, UTC `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`, enum names as exact strings. Revisions/counts/byte lengths use nonnegative canonical base10 strings (no plus/leading zero except0; revisions positive; signed64 bounds). Decimal values use invariant .NET decimal plain strings, no exponent/negative zero/insignificant trailing zero; maximum28 fractional digits and exact0..100 range. Null is JSON null, not an empty string. Reject invalid UTF-8, unpaired surrogates, duplicate fields, controls except escaped text controls, NaN/Infinity, numeric enum tokens, numeric instead of string counters and unrecognized fields at any depth. Escape quote/backslash, JSON standard control escapes, remaining U+0000..001F as lowercase `\u00xx`; preserve other valid Unicode scalar values without normalization. Source text order is preserved; declared sets above sort by kind then UUID D ordinal; reject duplicates rather than dropping them.

The digest is SHA-256 of exactly these canonical bytes. A manifest does not contain its own digest; its external metadata descriptor/receipt holds that digest. The sourceDigest commits the source capture's scope/run/revision/assessmentState/inputs/projection/score/warnings/retention/category/field/provenance envelope, excluding database observation time and any report UUID. Projection/score digest is computed before creating the manifest. Command digest commits actor tenant/object/session/securityVersion plus the complete closed command including warning set; it excludes correlationId so an uncertain retry with a new correlation retains exact operation meaning. It includes expected source/revision and uses the same canonical scheme. A source digest, projection digest and manifest digest identify different objects.

First internal safety bounds: depth32; projection32MiB; score1MiB; manifest1MiB; maximum100000 aggregate typed collection records and maximum100000 linked references; command64KiB. These are refusal bounds for the core, not public page defaults, workload quotas or pilot performance acceptance. Oversize returns a typed issue with no partial projection; boundary tests include Unicode complete-envelope bytes. Production load limits/client budgets are separately decided and measured. No lossy truncation.

## Transaction, commit and exact retry

1. Validate command/actor before touching protected storage. Acquire the authority fence; it must serialize current identity/session/assignment/customer/category/field revocation and retain the observed policy/security revisions through publication commit or exact-read emission. Acquire locks in one documented order: actor subject, exact assignment/customer policy, assessment source, operation receipt, report lifecycle, audit stream. Sorted UUID ordering applies when a class has multiple locks. The fixture adapters implement this same fence; the real cross-plane mechanism is deferred and cannot be replaced by booleans or freshness caching.
2. Begin the customer-owned PostgreSQL transaction. Resolve an existing actor/scope/operation/command-digest receipt under current authority before rejecting a stale expected run revision. Exact success replay returns the same version/manifest digest and never produces another publication event. Another command/actor/scope with the same operation conflicts. A revoked actor cannot use receipts to bypass denial.
3. Capture the terminal source in that transaction under its owning source fence; verify run/review/score/maturity/coverage/inputs/digests and warning set. CompletedWithGaps is permitted with exact coverage warnings; source can include unreviewed findings with exact mandatory warnings. Expected source/revision mismatch denies. Source ports may not relabel Scoring/drafts/evaluation captures as terminal. Publication does not perform a run-state transition in this packet.
4. Required acknowledged warning tuples must equal the computed source warning tuples, including empty when none; both are committed. ApprovalState is PublishedWithWarnings iff warnings are nonempty. Warned publication is allowed; there is no gate requiring all findings confirmed. Critical/High deterministic mandatory unreviewed and Proposed AI stay only in source provisional score as required by the scoring owner.
5. Build and verify score/projection bytes and stage customer-scoped PutIfAbsent blobs. Existing bytes under a digest must match exactly or fail; cross-customer deduplication is prohibited. Re-read/hash staged bytes. Build/stage the manifest likewise. Uncommitted blobs cannot be found through the reader, digest possession or a caller locator. GC/reconciliation is deferred; failed stages remain quarantined/invisible until owned reconciliation can remove safely.
6. In the same metadata transaction insert immutable version, exact frozen blob descriptors, operation receipt and append-only audit event. Audit failure aborts visibility. The receipt includes actor binding, operation/command digest, report/scope, manifest/projection/score digests, committed timestamp, event ID and event digest. No report text, session secret, raw reference, warning prose or source payload enters audit.
7. After audit-head waits, revalidate source/fence/lifecycle/deadline immediately before commit. A deferred fixture database check verifies the exact revision/fence at commit. Authority deadlines use conservative original validity; a wait cannot manufacture a fresh window. No arbitrary five-second total guarantee is inherited from SyntheticMcp. Actual caller cancellation before commit rolls back metadata; immutable staged blobs remain invisible.
8. Metadata/version/receipt/audit commit once. On ambiguous commit acknowledgment return/throw a distinct internal `PublicationCommitUncertainException` containing only the opaque operation reference; never claim rollback or retry with a new operation. Reconcile the original receipt under current authority using the same command. Unknown outcome cannot return content or successful publication.

The first persisted fixture fence uses connection-session advisory locks retained through emission, while metadata/source/audit row locks use the owned transaction. Every fixture authority/source/lifecycle mutator takes the same documented advisory key/order; transaction commit must not release the emission fence. Disposal releases session locks and returns the connection only afterward, including failed callbacks/cancellation. This is an executable single-database fixture mechanism, not the future control/customer-plane solution. The production fence adapter must supply equivalent guarantees before host composition; unsupported adapters deny rather than substituting a snapshot. The core itself has no database-specific locking dependency.

Immutable version rows prohibit UPDATE/DELETE by runtime. Lifecycle lives separately; application read checks Active/unexpired/not read-blocked under the same fence. Hold preserves retention only; a nonblocking hold creates no access and does not rewrite bytes. Soft-deleted/expired report denies regardless of stored blobs or receipt. Audit events/receipts have unique IDs and append-only restricted writer functions, stream ordering/hash predecessor and trusted timestamps, following the M02 convention. Fixture database roles separate migration owner/runtime/read/audit; no startup migration or live grant is added. Actual audit retention12months/deletion+30days and customer report retention follow existing policies; this packet records/enforces supplied expiry but does not implement lifecycle/purge/recovery jobs.

## Exact read and current availability

Authenticate/resolve current authority before manifest/lifecycle/content loads; exact request never accepts storage locators or searches latest. Acquire read fence, find exact committed `(scope,reportVersionId)` and compare expected manifest digest. Verify committed manifest bytes and exact bindings, lifecycle/retention, every frozen descriptor and complete category/field authorization before protected blob loads. Verify score/projection bytes and source linkages. No partial/draft/newest/empty fallback. Recheck authority/lifecycle after load and audit wait, persist one payload-free successful read event per InvocationId and then emit while the fence remains held. Release after the caller-owned emission callback returns; an implementation returning detached content before emission cannot claim a final revocation fence. Therefore the frozen reader signature is `ReadExactAsync(actor,request,Func<VerifiedPublishedReportV1,CancellationToken,ValueTask> emit,ct)` returning metadata-only read outcome. No bytes may be emitted on failed audit/authority/integrity. The emission callback is trusted in-process and must neither mutate canonical data nor invoke a raw resolver.

Read-audit event deduplicates internal delivery retry by InvocationId plus actor/exact request digest. A new requested read uses a new InvocationId, rechecks authority and records its own event. Retry cannot replay protected content merely from an audit receipt; reload/integrity/current policy checks still apply. Unknown audit commit outcome emits nothing until original audit receipt is reconciled. Transport disconnect after durable successful audit cannot erase that audit; it does not imply bytes were delivered.

Evidence/reference availability after publication is read separately under the same fence from an authoritative lifecycle port, returning only `(referenceId,currentAvailability,reason,revision)` for linked authorized IDs. It does not rewrite original projection/manifest digests or frozen availability. Exact reader returns original bytes and a separately labeled `currentReferenceAvailability` overlay to its trusted emission callback; it verifies the overlay is linked and bounded. Expired/deleted evidence produces unavailable metadata, never raw load/restoration. Report expiry/deletion denies the complete read. No generic reference search, source database lookup or locator exists.

Render state/artifact completion and acknowledgment/link access are separate append-only projections, never manifest edits. This packet creates no PDF success, renderer workload authorization, sharing token, acknowledgment or audience-specific report. Native original manifest digest must accompany later dashboard/Markdown/PDF/link/MCP representations; filtered serving hashes, if introduced separately, are never labeled that digest.

## Deferred acceptance and change process

Production source capture, control/customer-plane fence, central audit/lifecycle adapters, actual reviewer/risk provenance, renderer/license/sandbox, audience/field partitions, supported MCP clients/tokens/paging/distributed budgets and recovery/tombstone replay remain NOT VERIFIED. A local fixture port cannot complete P02-I06/I07 production integration, any external intake row, G6/G7 or M09/M11 acceptance. Reviewer/gate source evidence remains independent; agent design approval cannot supply customer authority or qualified acceptance review.

This doc is a design freeze proposal. Coordinator records the exact approved contract/test/plan commit and independent reviewer outcome before assigning code. Contract amendments precede dependent implementation. Rollback preserves immutable committed versions/receipts/audit/lifecycle state and historical synthetic bytes; production or irreversible changes retain explicit authorization.
