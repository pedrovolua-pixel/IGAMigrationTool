# V14 independent preauthor oracle design

Status: independent semantic design; technical byte recipes and executable tests PENDING exact engineering freeze.

This design was authored before inspecting planning-task implementation, author expected output, runtime output or generated goldens. The checkout is `84501e539350eef5986690b75a786acd98a1287b` on `codex/cycle14-task-verification`. The exact owner-approved proposal is revision `2b565945be99c4c674cf00b08caa6fc581b99ffd`, SHA256 `fe8a1583de876b653ddef41fa105a0defe4679f612400f660721ca8234c2b839`; the coordinator's attributable approval record supersedes the proposal's unchanged pending header. No engineering DTO, digest recipe, issue code or migration API is invented here.

## Independent expected-value construction

Freeze literal fictional customer/project/environment/run/finding/option/actor/event identities, original option/guidance and the three fixed artifact templates separately from production helpers. Build expected canonical source, selected latest attestation vector, scoped task identity, semantic command bytes and metadata receipts with an independently authored ordinal recursive JSON implementation once the exact private recipes are frozen. Arrays retain declared significant order; text and Unicode escaping follow the frozen serializer recipe exactly. Do not copy actual output into expected files, import production canonical/identity helpers for expected values, or regenerate historical goldens. Preserve this initial design and every later supplement as distinct source generations.

Expected snapshots will explicitly contain creation binding, latest binding, workflow, freshness, current revision, continuous attributed events, original source references and closed receipt metadata. Compare complete bytes/digests and typed fields, not only success flags. Use deliberately different current and historical bindings so replay cannot accidentally return a new view. Observe actual saved rows and source captures only as execution evidence, never as expected fixtures.

## Identity and branch precedence

Stable identity includes customer/project/environment/run/finding/scoped option and excludes source digest, workflow and command UUID. Isolate each coordinate; same option in another run is distinct. Accepted UUIDs are unique across the scoped run/task module, actor/task/action/semantic-command bound. Wrong actor/resource/category authority denies before receipt/history/duplicate lookup. Closed-field, bounds and revision validation happens before UUID lookup.

After one shared run fence, verify current authority, current source and selected attestation proof, and stored history/projection/receipt integrity. Exact accepted UUID replay returns its original historical metadata receipt without reapplying fresh-command source/revision/transition eligibility; changed accepted reuse conflicts. UUID accepted for another task/action in the same namespace conflicts. SourceUnavailable or corruption denies even exact replay.

For fresh UUIDs, exact expected current full source and selected latest accepted event/revision vector precedes stable-task lookup. A stale expected vector conflicts even if a task already exists. Fresh Create always declares revision0. An existing task returns AlreadyExists without comparing that0 to its later revision and without rechecking absent-task creation eligibility. This remains true after explicitly inspected withdrawal or Rejected finding, but not unavailable proof. AlreadyExists reserves no UUID and writes no task/event/current/receipt; the same unused UUID may later become a valid different fresh command. It neither returns a creation receipt nor changes binding/state. An absent task then requires non-Rejected source and all three selected current ReviewedForPlanning attestations for the exact source.

## Workflow and independent freshness

The literal table in `semantic-primitives.json` is exhaustive. No self-transition, Completed→Cancelled or Cancelled→Completed is allowed. All status changes require an exact nonblank reason. Comments are separate append-only events in all four states and never alter workflow/binding.

CurrentPlan means the complete current source and selected latest accepted vector match the latest creation/reconfirmation binding. Verified later differences mean NeedsReconfirmation; unavailable/invalid proof means SourceUnavailable. Selected withdrawal/re-review changes freshness even when package bytes remain identical. Unselected attestation-only changes do not. Any complete package/finding-source change stales the task even if the selected option text remains identical. Returning to earlier text cannot restore a previous binding: newer revisions/events remain significant. Freshness never automatically changes terminal/nonterminal workflow.

Enter InProgress or Completed only with CurrentPlan and non-Rejected source. Explicit terminal reopening to Planned requires verifiable non-Rejected source but may leave NeedsReconfirmation. With stale or newly Rejected verifiable source, comments in every state, cancellation from Planned/InProgress, and InProgress→Planned are maintenance actions; exact current source/vector remains required. SourceUnavailable denies all writes. Reconfirm only Planned/InProgress, NeedsReconfirmation, still-existing original scoped option/finding, non-Rejected source and all three selected current reviewed attestations. A fresh CurrentPlan reconfirm is InvalidState/no-write; an already accepted reconfirm's exact replay still returns its historical receipt. Reconfirm preserves workflow and creation provenance.

## Database, concurrency and recovery oracles

Use only an exactly guarded, duplicate-key-free owned connection to a newly dedicated `iga_synthetic_cycle14_v14_` database on existing `127.0.0.1:55433` with the established synthetic role. Do not initialize until the exact additive schema, flag and same-transaction composition are frozen. Never restart cluster, change role/HBA, alter other databases, rewrite old migrations or remove history to recover.

Snapshot task/event/current/receipt/source-registration counts and complete owned source-store bytes before each denial. For accepted commands check one continuous revision/event/current/receipt change and exact unchanged finding/assessment/artifact-review/source originals. For replay/AlreadyExists compare complete pre/post store state, not only task count. Concurrent same-option conversion produces one task; a fresh loser is AlreadyExists or a stale precondition conflict as specified by its actual captured vector. Concurrent commands at one expected task revision have one accepted revision winner. Real owning-module artifact withdrawal/re-review, finding review/comment and assessment writers share the same connection/transaction run fence with conversion/reconfirmation. Controlled barriers prove no inconsistent source/vector commit; no nested transaction or cross-module SQL shortcut.

Place release/cancel/finally around each spawned held operation before waiting for entry. Fault/cancellation must roll back event/current/receipt and any source registration atomically. Verify recovery on a fresh connection, then controlled owned host stop/restart preserving history/replay. Bound schema drift/fingerprint denial with protected restoration and externally verified cleanup if any child can be killed. Hard-kill durability is a separate NOT VERIFIED claim unless actually executed. Source high-water rollback cannot expose or accept older historical proof.

## Host and browser oracles

Validate literal closed DTOs, metadata-only receipts, exact UTC time, integer/revision limits, complete source/vector/task/artifact/history parity and every displayed original warning/reference. Test unknown/missing/foreign fields, actor/assignee/roles/time injection, malformed canonical hashes and semantically inconsistent but rehashed DTOs. Read authorization and unavailable-content boundaries precede history exposure.

Serve only owned loopback product host after port ownership handoff. Sanitize an explicit allowlisted environment before loading Playwright or spawning children; no inherited-marker bypass. Intercept requests before navigation, allow only exact owned host/asset/read/mutation routes and methods, abort and record every unexpected/injected request. Cleanup handles exited/signalled/spawn-error children and never kills pre-existing listeners. Use existing pinned tools; no dependencies or shared configuration changes.

Test actual explicit conversion, reconfirmation, status/reopen/comment, AlreadyExists reread, exact uncertain Retry and refreshed-source conflict. Preserve entered text and frozen uncertain command across run/option switching. Held replies require completion barriers after fulfillment plus a browser processing turn. Include unchanged-run source epoch changes that cannot be masked by a generation/run switch, and malformed committed receipt→uncertain identical retry→no duplicate event. Do not invent an unreachable interaction while a panel is unmounted.

Hostile markup/form/resource/formula/Unicode/control text is literal inert React text; do not carry HTML parser normalization rules over to text-node insertion. Assert original textContent for the actual client path, zero active injected nodes/actions/network, immutable original code and absent export/executor routes. Keep strict DOM checks before/after screenshots; use `caret: 'initial'` so Playwright does not leave style attributes on textareas. Test keyboard action activation, native history/details, mandatory labels/reasons, success/error focus, desktop/mobile/320 reflow and axe violations/incomplete separately. Manual screen reader/Windows/zoom and deployed isolation remain NOT VERIFIED.

## Evidence and compatibility

Bind exact original source, independent recipes, consumed fixtures and full executed Release/front-end closure including runtime tools; preserve native commands, counts/codes, failures, original source of each failed generation, observed synthetic captures and screenshots. Capture outputs only in ignored evidence, never expected fixtures. Telemetry negative corpus excludes all reason/comment/artifact/source values; product task history is separately authorized domain content.

All ten historical profiles retain exact input/source/lock/DTO/golden bytes and prior finding/artifact review/guidance/AI/draft/health/maturity behavior. One eleventh profile alone gains the exact opt-in task authority. Historical Cycle13 counts are historical evidence, not V14 PASS. TC14-06 is documentary future CSV reconciliation only; no export PASS, broader permission, production migration/release, customer evidence/provider or full gate acceptance follows from this design.

## Freeze questions that block dependent implementation

1. Exact eleventh profile/application/source versions, task lock and per-profile inventory; older-profile task-field representation and explicit startup flag/database guard.
2. Exact source/selected-vector DTOs, all verifiable ineligible attestation states, canonical/identity/semantic-command byte recipes, ordering and high-water source/selected-vector rollback checks.
3. Closed command/receipt/read shapes, UTC encoding, revision maxima, text round-trip/body limits, issue/result precedence, AlreadyExists metadata and accepted UUID namespace coordinates.
4. Module-owned same-connection/transaction capture API and lock ordering; task/source/event/current/receipt schema/integrity fingerprint and source registration semantics.
5. Authorized SourceUnavailable read boundary: which metadata/history/source content is provable and which is withheld, with no unfenced fallback.

No runtime, host, database migration, security audit or task implementation has been executed by this preauthor design.
