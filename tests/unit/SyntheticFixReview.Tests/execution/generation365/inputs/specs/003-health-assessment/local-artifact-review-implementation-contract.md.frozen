# Local artifact review: implementation contract v1

Status: Settled engineering contract for approved AR13-01–05; non-author A13/B13/V13 review complete
Owner: Coordinator
Authority: [source-bound owner approval](local-artifact-review-approval.md), [approved semantics](local-artifact-review-contract-proposal.md), [test plan](local-artifact-review-test-plan.md).

This document settles internal storage, capture and HTTP details within the approved local scope. It grants no additional product or security authority. Its exact UTF-8 bytes with LF line endings, including the final newline, are SHA-256 hashed into `FixReviewContractDigest`. The digest is recorded in the execution evidence and pinned in the new catalog and source validator; later changes require a new lock. This document never embeds its own digest.

## Dedicated profile and historical compatibility

Profile `synthetic-review-maturity-fix-review-equal-v1`, label `Synthetic consultant artifact review · equal weights`, application `synthetic-fix-review-app-v1`. All existing nine profile IDs, application versions, optional-field omission, canonical bytes and behavior remain unchanged. New frozen inputs contain exactly the previous fix-package twelve fields plus `fixReviewContractDigest`. New run display contains one corresponding additional contract lock. Analysis fixture is the existing equal-weight fixture; maturity and the three fixed fictional templates are unchanged. AI remains disabled. Historical analysis responses have null artifact-review detail and historical action routes are denied.

Generated `FixPackageSnapshot` stays byte-identical for its actual guidance source, including its original Unverified status and original unavailable-section text. A separately titled review overlay explains that those sections describe the generated original. Exact current label: **Reviewed for planning — fictional, review-only; correctness and remediation unverified**.

## Shared source fence and capture

A framework-only `SyntheticSourceFence` module contains a parameterized PostgreSQL transaction advisory-lock helper. Key bytes are SHA256 of UTF8 compact JSON with this fixed property order: `{"schemaVersion":"synthetic-source-fence-v1","customerId":...,"projectId":...,"environmentId":...,"runId":...}`. Strings use System.Text.Json default escaping; run ID is lower-case D-format UUID. Interpret the first eight digest bytes as signed big-endian Int64. Every caller passes trusted scope and the same guarded synthetic database, never client scope.

AssessmentRuns `MutateAsync`, FindingReview `SeedAsync` and `ApplyAsync` acquire this fence immediately after starting their transaction, before every row/global-seed lock. New artifact store reads and mutations acquire it before a trusted host-only asynchronous source callback, and hold it until their transaction ends. Source callback reads owning-module APIs without reacquiring the fence, then builds actual analysis, guidance and packages once. It contains no network, UI or unrelated work. No module queries another module's tables. Initial finding seeding occurs outside the artifact transaction. `DemoReviewService.ReadExistingAsync` only reads and compares the expected immutable seed; using seeding `ReadAsync` under the fence is forbidden.

The host reuses that exact captured run/review/guidance/package value for the entire analysis response and review overlay. A pre-fence cached capture is forbidden. Finding changes at unchanged run revision still create a different full package source digest. Assessment outbox delivery does not alter package source and does not join this fence. Migration locks remain separately scoped. Cancellation and existing bounded database command timeouts apply.

## Validated source and immutable identity

The store accepts only a source constructed from a real `FixPackageSnapshot` by the new pure source builder. It rebuilds through the actual package/guidance builders, compares canonical actual bytes and cached canonical JSON/content digest, requires the dedicated profile/application/thirteen frozen fields/exact contract and fixed scope, and verifies run/review/input/template bindings. Empty packages are valid reads with no actions. Forged or unavailable source is denied before event lookup.

Full source digest is the actual package `ContentDigest`; it binds complete upstream source, every finding revision and package artifact. Capture includes the ordered finding revision vector and each artifact's finding/category/package/scoped option/template/kind/text digest. Text digest is lower-case SHA256 of UTF8 original text. Fixed artifact IDs remain those emitted by the original builder. History is scoped by fixed scope/run/artifact, never carried to another run.

Recorded source versions are immutable, keyed by scope/run/full source digest. Same identity with different canonical binding is an integrity conflict. Reads create no seeds, source versions, events, receipts or invalidation entries. New successful commands register the validated source version and first artifact seed in the same atomic transaction. Existing source versions retain exact immutable bindings. Before every Ready read/overlay, replay or action, current captured run revision and every finding revision must be at least the greatest recorded revision, with identical immutable run input/profile/application/contract/template and artifact identity. Equal run revision AND complete finding revision vector with different full source digest are denied. A later run revision may legitimately change the package digest with unchanged finding revisions. Returning earlier presentation prose has a later finding revision and cannot resurrect an older attestation.

## Commands, outcomes and replay

Initial artifact revision is 0; every successful new event increments by exactly one, checked Int64 arithmetic. Maximum serialized HTTP revision is JavaScript safe integer 9007199254740991; values beyond it are denied. Event ID is a nonempty D-format UUID, namespaced by scope/run/artifact. Artifact ID is exactly 64 lower-case hex characters and is supplied only in the route.

Closed HTTP body has exactly `eventId`, `kind`, `expectedRevision`, `expectedSourceDigest`, `reason`. Kind is exactly `ReviewForPlanning` or `WithdrawReview`; expected source digest is 64 lower-case hex. Reason must be nonblank and at most 2000 UTF-16 units. Preserve its original string exactly, without trimming, Unicode normalization or control removal. Store it in JSON text so escaped NUL is representable. Unknown/duplicate properties, actor/roles/scope/time/grants and invalid types are denied before mutation. Existing origin, anti-forgery, loopback and body-size boundaries apply.

Command semantic identity hashes canonical ordinal-property JSON binding private version, trusted scope/run/artifact/actor and all five exact command fields. Canonical digest uses System.Text.Json web property names/default escaping, ordinal recursive object keys, significant array order, no newline. Same command and actor returns the original metadata-only receipt plus `AlreadyApplied`; changed event reuse returns EventConflict. Current trusted source, exact category/resource visibility and action authority are checked before event lookup. Expected revision/source and transition checks occur only for a new event. Replay never restores the current overlay and is allowed after source refresh or later actions if current visibility/source remain valid.

Current state derives only from latest event: none → Unverified; latest withdrawal → Unverified even after source refresh; latest review with current full source digest → ReviewedForPlanning; latest review with another digest → NeedsReview. A fresh review is allowed from Unverified/NeedsReview. Fresh review of an already current attestation is InvalidState. Withdrawal requires the latest exact-current attestation; withdrawing stale history or no current attestation is InvalidState. New stale expected revision/source returns RevisionConflict/SourceConflict before transition validation. Invalid new commands never write. Finding actions cannot create artifact events.

## Authority and atomic audit

Only a trusted server-supplied authenticated/active/nonrevoked synthetic Consultant with active exact assignment, explicit Read and Review action grants, current artifact category access and Mutable resource state may read history or perform these actions. QualifiedReviewer and all other roles alone are denied. HTTP never supplies authority. The entire source is visible or the coherent detail is denied; no partial source/history disclosure. Existing finding authority remains unchanged.

One additive `synthetic_fix_review` migration001 owns fixed-scope metadata, immutable source/artifact seeds, per-artifact current revision, append-only events and append-only metadata receipts. Events include trusted actor/roles, database UTC timestamp, exact reason, semantic command and immutable source binding. Receipt contains only schema/event/run/artifact/action/result revision/actor/time/source digest, never reason, text, evidence, code, comments or credentials. No reason/content is logged. Event/current/receipt/source registration commit atomically. Observer `BeforeCommitAsync` supports controlled rollback/concurrency verification without changing production semantics.

Triggers prohibit updates/deletes/truncation of immutable rows, events and receipts; current advances one revision matching an appended event. Every read reconstructs ordered continuous history and verifies source/command/event/receipt digests, immutable identities and current revision. Migration initialization and operations verify exact embedded SQL digest and schema fingerprint including columns/constraints/indexes/trigger state/functions. Unknown or drifted schema and foreign/nonloopback/non-`iga_synthetic_` database are denied. No existing migration bytes or schema are changed. Rollback disables new actions/profile and preserves append-only data; no purge/down migration or customer retention decision.

## Closed presentation and refresh contract

The analysis `artifactReview` detail is separate from `fixPackages`. It carries schemaVersion1/demoOnlytrue, Ready/Unavailable with a bounded reason code, run ID/revision/input/profile/contract, current package/guidance/finding-review digests, trusted actor, and ordered artifact entries. Entry carries exact original artifact identity/kind/text/template digests, continuous revision, derived state, server canReview/canWithdraw and complete ordered attributable history. History carries exact immutable source binding, event/action/revision/actor/time/reason and recorded outcome. The client independently checks the complete current package; historical bindings are strictly checked server-verified references, not an independent client proof of historical full-package bytes. Server reconstruction verifies the exact durable historical source versions. Current flags must agree with derived state and current authority. New generated schema types define exact property names before B13 coding.

Mutation result carries an optional typed issue, `alreadyApplied`, and only the original metadata receipt. It is never merged into current state. The parent rereads whole analysis after every successful/replayed command. All async source validation and mutation/read completion are fenced by run/artifact/source/operation epoch. Full schema, actual current artifact membership, original text/template digests, history uniqueness/order/derived state and all source locks must agree before controls appear. Historical profiles remain null.

Entered reason and a frozen uncertain command survive refresh/unmount through parent-owned draft state keyed by run/artifact. No automatic retry or source rebinding. Explicit retry uses the exact original event command. Conflict clears uncertain command, preserves entered reason, refreshes source and requires another explicit inspection/action with a fresh event ID. Changed run clears visible old overlay; late completions cannot restore it. Failure focuses an actionable error summary; coherent refresh restores the targeted artifact heading. Hostile strings render only as inert React text; no downloads, supplied links, editors or execution controls.

## Evidence

Execute AR13-T01–T12 against exact combined source; preserve independent oracle, worker and coordinator evidence separately. Historical literal goldens are never regenerated to conceal regression. Report actual source/environment/check outcomes, migration implications and unverified cases. The private status site follows canonical records; publication confirmation does not pass product gates.

## Exact v1 API and DTO names

All arrays are immutable in .NET and readonly in generated TypeScript. JSON uses web camelCase names; enum fields are the strings below, never numeric enum values. Every object is closed. Nullable properties remain explicitly present in the new HTTP envelope. Revision range is 0 through 9007199254740991. Source findings/artifacts are sorted ordinal by findingId/artifactId; history is ascending continuous revision beginning at1. Actor roles are exactly `["Consultant"]` on accepted events. Maximum source artifacts/findings is100000 and canonical source bytes32MiB, inheriting the original builder's bounds; reasons retain their separate2000-unit bound. Empty history is a valid revision0 entry. No arbitrary unavailable reason or issue text reaches the UI.

```text
ArtifactReviewScope { customerId, projectId, environmentId }
ArtifactReviewFindingRevision { findingId, revision }
ArtifactReviewSourceBinding {
  scope, runId, runRevision, runInputDigest, baselineId, profileId,
  applicationVersion, contractDigest, sourceDigest, guidanceDigest,
  findingReviewDigest, templateVersion, templateDigest, findingRevisions[]
}
ArtifactReviewArtifact {
  findingId, categoryId, packageId, scopedOptionId, artifactId,
  templateId, kind, artifactTextDigest
}
ArtifactReviewCommand { eventId, kind, expectedRevision, expectedSourceDigest, reason }
ArtifactReviewEvent {
  eventId, revision, kind, actorId, actorRoles[], recordedAtUtc,
  reason, source: ArtifactReviewSourceBinding, recordedState
}
ArtifactReviewEntry {
  [all ArtifactReviewArtifact fields], revision, state,
  canReview, canWithdraw, history: ArtifactReviewEvent[]
}
ArtifactReviewReceipt {
  schemaVersion: "synthetic-fix-review-receipt-v1", eventId, runId,
  artifactId, kind, revision, actorId, recordedAtUtc, sourceDigest
}
ArtifactReviewDetail {
  schemaVersion: 1, demoOnly: true, status: "Ready"|"Unavailable",
  reasonCode: null|"artifact_review_source_unavailable"|"artifact_review_integrity_denied"|"artifact_review_denied",
  source: null|ArtifactReviewSourceBinding, actorId: null|string,
  artifacts: ArtifactReviewEntry[]
}
ArtifactReviewResult {
  schemaVersion: 1, demoOnly: true, issue: null|ArtifactReviewIssue,
  alreadyApplied: boolean, receipt: null|ArtifactReviewReceipt
}
```

`kind` is ReviewForPlanning/WithdrawReview; current `state` is Unverified/ReviewedForPlanning/NeedsReview; event `recordedState` is ReviewedForPlanning/Unverified. Artifact kind is Configuration/Script/Sql. Issue enum is InvalidInput, Denied, WrongScope, InvalidState, NotFound, RevisionConflict, SourceConflict, EventConflict, SeedConflict, IntegrityMismatch, MigrationDrift, NotInitialized, SourceUnavailable, RevisionOverflow. Unavailable detail has null source/actor and empty artifacts. A denied route returns the existing metadata-only demo error envelope; successful mutation returns ArtifactReviewResult with null issue and original receipt.

`ArtifactReviewAuthority` is a server-only record of ActorId, Authenticated, Active, Revoked, AssignmentActive, AssignedScope, Roles, Categories, Actions and ResourceState. Roles/actions/state use closed string values matching the approved synthetic policy; accepted authority is Consultant with Read/Review and Mutable. Source binding contains no mutable client authority. Trusted source builder `ArtifactReviewSourceBuilder.Build(FixPackageSnapshot?)` returns `ArtifactReviewSourceResult(Issue,Source)`. Get-only `ArtifactReviewSource` exposes Binding and Artifacts and internally retains exact validated package/canonical bytes for persistence verification.

Store constructor is `SyntheticFixReviewStore(connectionString,trustedScope,ArtifactReviewSourceReader,observer?)`; reader is `Task<ArtifactReviewSourceResult>(Guid runId,CancellationToken)`. InitializeAsync verifies migration. ReadAsync(runId,authority,ct) returns ArtifactReviewReadResult(Issue,Snapshot), where snapshot carries Source binding and Entries. ApplyAsync(runId,artifactId,authority,command,ct) returns ArtifactReviewApplyResult(Issue,Receipt,AlreadyApplied). The host creates a request-scoped store/capture callback, avoiding shared mutable capture state between concurrent requests. Callback executes exactly once under the fence. Observer is BeforeCommitAsync(operation,runId,artifactId,eventId,ct), with operation `apply`; read has no commit observer or writes.

Route is `POST /local-demo/v1/runs/{runId:guid}/artifacts/{artifactId}/review`; whole detail is `analysis.artifactReview`. Existing anti-forgery/error behavior and metadata-only HTTP conflicts apply. No additional routes.
