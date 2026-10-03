# Local consultant artifact review — contract proposal

Status: Approved by repository owner on 2026-10-02 — local synthetic implementation only; no production authority
Owner: Repository owner with product, technical and security responsibility
Last updated: 2026-10-02
Source audited: `874c653a495921dbf20f628715b4ce70f3ecc030`

## Requested decision

The repository owner approved **AR13-01 through AR13-05 together for local synthetic implementation only** in direct response to the coordinator’s exact decision request. [The approval record](local-artifact-review-approval.md) binds the reviewed revision/digest and actual response. No amendments were supplied. No customer use, production permission, artifact execution, report publication, task creation or export follows.

| ID | Approved choice | Why an explicit human decision was required |
| --- | --- | --- |
| AR13-01 | Only the assigned synthetic Consultant can mark an individual artifact **Reviewed for planning** or withdraw that attestation. Qualified customer reviewers cannot mark artifacts reviewed in this slice; their existing finding actions remain unchanged. | The approved matrix allows customer reviewers to comment/review generally, while FR-HAS-37 names consultant-reviewed artifacts. The exact artifact authority is absent. This intentionally narrow local subset does not amend the production role matrix. |
| AR13-02 | Review is a digest-bound attestation to the exact artifact and complete source package. It acknowledges inspection for planning; it establishes neither supported One Identity remediation, correctness, execution safety, validated recovery nor remediation. Generated originals always retain **Unverified**. | The existing specifications do not define what removal of the unreviewed label means or how it relates to generated originals and finding decisions. |
| AR13-03 | When the latest event is a review, any change to its complete package source digest makes that attestation historical and the current artifact **Needs review**, even when text is unchanged. A latest withdrawal keeps the artifact **Unverified**, including after refresh. Current review never carries across runs. No earlier event means **Unverified**. All history is retained. | Source-sensitive invalidation is product behavior. Stable artifact IDs alone cannot establish that earlier review applies to new guidance/finding presentation. |
| AR13-04 | Store attributed review/withdrawal events append-only, with optimistic revision and idempotent client event IDs, in a new isolated synthetic review store. Require a reason for both actions. Reads never write invalidation events. | These are new collaboration semantics and durable derived data. Existing finding review is not an artifact review contract. |
| AR13-05 | Add one dedicated opt-in local profile and a distinct contract lock. Preserve all nine existing profiles, frozen bytes and always-Unverified fix-package behavior. Expose only review/withdraw and history in the new profile; no edit, bulk approval, task, export or execution controls. | This provides a reviewable rollout boundary and avoids silently changing the meaning of previously saved Cycle12 results. |

## Governing authority and missing decisions

The approved [product specification](product-spec.md) FR-HAS-35/37/42 and AC-HAS-18, [technical specification](technical-spec.md) core records, state/concurrency and safety requirements, [implementation plan](implementation-plan.md#milestone-7--deliver-recommendations-fix-packages-tasks-and-safe-csv) IP-HAS-008, and [test plan](test-plan.md#tp-has-018--fix-package-safety) TP-HAS-018 permit eventual consultant-reviewed inert artifacts. They do not specify an artifact-review action, attestation meaning, invalidation rule or history schema. The [Cycle11 contract](../../docs/development/cycle11-fictional-fix-package-contract.md#authority-and-outcome) and [Cycle12 contract](local-fix-package-integration-contract.md#authority-and-boundary) explicitly exclude those behaviors. The [approved local build](../../plans/active/one-identity-local-pilot-build.md) requires undecided contracts to be made reviewable rather than assumed.

[AGENTS.md](../../AGENTS.md) controls 1, 2 and 8 prohibit unapproved substantial behavior, invented permissions and un-escalated material ambiguity. Its rule that human approval is required for product scope and consequential security decisions applies to these exact choices. Agent review establishes proposal quality, not approval. Existing ADR-0001/0002/0003/0004 remain accepted and unchanged.

## Approved internal implementation contract

The human decision is recorded; these details govern the bounded implementation. They are neither public endpoints nor production schemas.

### Exact source and immutable originals

Consume the actual Cycle11 immutable `FixPackageSnapshot`, rebuilt and validated from the same captured guidance and current finding-review source as the analysis response. Each event binds fixed synthetic customer/project/environment, run ID, run-input digest, finding ID, package ID, scoped option ID, artifact ID, kind, artifact-text digest, template version/digest, complete package content digest, guidance digest and captured finding-review digest. IDs and digests are references, never authorization tokens. Reject mismatched source, foreign members, malformed snapshots and unavailable results before mutation; return no protected payload on denial.

Review does not modify `FixPackageSnapshot`, its canonical bytes, any generated text, recommendation option, finding disposition, score, maturity, current draft, existing lock or saved coverage. Preserve its **Unverified** status and disclaimer as the generated-original layer. A separate typed review overlay and attributable history show the consultant attestation. Label the overlay **Reviewed for planning — fictional, review-only; correctness and remediation unverified**. Never use a bare Approved, Safe, Verified, Fixed or Remediated label.

### Actions, state and authority

Only `ReviewForPlanning` and `WithdrawReview` mutate the overlay. Both require an active server-supplied synthetic named Consultant authority with exact assignment, category and action grant, mutable nondeleted resource, valid source and bounded nonblank reason (maximum 2,000 UTF-16 code units). No actor, roles, scope grants or timestamp supplied by an HTTP payload is trusted. A qualified reviewer, auditor, executive, support actor, revoked/inactive/wrong-scope actor and missing action/category grant are denied. Production identity and permissions are not implemented by this fixture authority.

Review may apply to an Unverified or Needs review artifact; repeating review with a new event ID against an already-current attestation is denied as an invalid transition. Withdrawal requires a current exact-source attestation; withdrawal of a stale historical attestation is denied. Replay precedence is explicit: first recheck current actor/resource/category visibility and validate the current trusted source; then look up an existing event ID in its scoped run/artifact namespace. Exact semantic replay returns only the original historical receipt with AlreadyApplied, even after source refresh or later actions. It never restores an old current overlay and is clearly labeled historical. Changed command reuse conflicts. Expected-current source/revision and transition checks apply only to a new event. Thus a stale new command is denied, while an authorized identical historical replay is inert. Missing or unavailable current source denies even replay; no history is disclosed without current visibility.

Current labels derive from the latest accepted event in the continuous scoped artifact history: latest ReviewForPlanning matching the current exact source => Reviewed for planning; latest WithdrawReview => Unverified even after a later source change; latest ReviewForPlanning for a different source => Needs review; no accepted event => Unverified. History reports the source each action actually attested and the original outcome, never retroactively relabeled. Source refresh makes old review historical immediately, without writes on read or automatic approval of unchanged text. Corruption/unavailable source suppresses the overlay and actions, rather than treating missing proof as a valid attestation.

| Sequence (A/B are complete source bindings) | Current result |
| --- | --- |
| No events; source A | Unverified |
| Review A; source A | Reviewed for planning |
| Review A; source B | Needs review; new review B permitted |
| Review A; withdraw A; source B | Unverified; withdrawal remains latest event |
| Review A; source B; review B; withdraw B | Unverified; A and B history retained |
| Review A; source B; attempt withdrawal of A | Denied; current remains Needs review |
| Review A; source A; new review A event | Denied; already-current attestation |
| Review A; source B; replay identical review-A event | Historical original receipt only; current remains Needs review |
| Review A; source B; new command claiming old A | Source conflict; no write |

The trusted current source includes the actual monotonically revised finding-review snapshot. Returning displayed prose to its earlier text creates a later revision and a different complete binding; it cannot revive A. A caller supplying old A or a tampered/backward source is denied. Test this explicitly; matching artifact text or stable IDs never resurrects review. Current source validation must use saved monotonic provenance, not a caller's hash assertion.

### Durable storage, concurrency and recovery

Separate `synthetic_fix_review` migration001 is additive and applies only to a dedicated fictional database selected by existing ownership guards. Its reviewed implementation must enforce scoped run/artifact foreign identity, immutable versioned source seeds keyed by complete source binding, continuous per-artifact revision, event uniqueness, append-only event enforcement and metadata-only audit receipts. Reasons/history are collaboration data and are never copied into security telemetry; approved audit policy remains unchanged. No customer retention default, purge policy or production migration is created.

Each mutation checks expected artifact revision and expected full source digest. Capture and validate current saved assessment/finding-review source under one transaction-compatible concurrency fence through commit; a concurrent source change cannot approve an obsolete snapshot. Event, current revision and payload-free receipt commit atomically, or all roll back. No accepted command exists without durable history. Reject overflow, conflicting seed, migration drift and integrity mismatch. A legitimate new source creates a new immutable seed; it does not reset artifact revision/history or conflict with the prior seed. Conflicting content for the same seed identity is denied. Exact relational definitions and the integration fence receive technical review before dependent UI work; this is not authority to weaken an existing boundary.

Owned-host/database reconnect and process restart must preserve event identity/current revision/history. A cancelled or failed transaction leaves no partial result. Tests may restart only their owned host; no shared PostgreSQL cluster restart, role changes or other database alterations.

### Opt-in and presentation

Approved profile ID `synthetic-review-maturity-fix-review-equal-v1`, label `Synthetic consultant artifact review · equal weights`, application version `synthetic-fix-review-app-v1`. It reuses the exact approved fixed fictional baseline/presets and templates; AI remains disabled. Add nullable `FixReviewContractDigest` only for this profile, omitted from every historical frozen input. The digest binds the finalized approved implementation contract bytes; technical review records the exact recipe and value before coding. Existing nine profiles and their application versions/locks/projections remain byte-compatible. Old profile reads have no artifact-review overlay and keep current Unverified artifacts.

A closed internal overlay DTO includes availability, full source/version bindings, current per-artifact review revision/label, and ordered immutable attributable history. It does not put mutable review state into generated-original canonical payloads. Transport/body-size/schema limits and exact synthetic paths are reviewed internal details under the existing loopback-only host; no production operation path or BFF capability is added.

The new profile offers individual Review for planning and Withdraw review with reason, full source-bound history, visible generated Unverified originals and current-source warning. No bulk approval, supplied hyperlink, artifact edit/download/copy-to-executor or execution action. Pending/failed/stale requests clear obsolete overlays, return established focus and require fresh coherent reads. A stale source or revision causes a conflict and no write; retries never silently apply to refreshed content. Keyboard and narrow-screen navigation retain existing inert preview behavior.

## Next contracts remain separate

Task conversion/ownership/status/comments, priority/effort overrides and CSV are excluded. Task completion must not imply validated remediation; exact states, assignment authority, conversion configuration and duplicate behavior still require decisions. There is also a recorded specification discrepancy: [authorization matrix](../../docs/security/health-assessment-authorization-matrix.md#human-role-matrix) permits Auditor CSV with an explicit export grant, while technical-spec's Export tasks CSV names an assigned Consultant. Product/security/technical owners must reconcile it before any task-export authorization is implemented. This proposal neither chooses nor silently changes that policy.

## Approval record

Decision: **APPROVED** for AR13-01 through AR13-05, without amendments.
Authorized human: Repository owner; date: 2026-10-02. [Exact revision/digest and response](local-artifact-review-approval.md#exact-decision) are recorded.
The [Cycle13 plan](../../plans/active/local-pilot-artifact-review-cycle-13.md) and [specific test plan](local-artifact-review-test-plan.md) may proceed. No requirement, milestone, UAT readiness or G1–G9 gate is accepted by contract approval.
