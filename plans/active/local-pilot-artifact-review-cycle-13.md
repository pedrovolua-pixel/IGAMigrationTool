# Local pilot consultant artifact review — Cycle13

Status: COMPLETE — approved bounded local synthetic implementation; broader pilot acceptance remains open
Owner: Coordinator
Last updated: 2026-10-02
Executed code checkpoint: `804d71a3e27aa5dbc8da48fe6d3732b76f6c6bc1`
Product/technical authority: approved feature003 specifications and approved local synthetic build
Approved decision: [AR13-01 through AR13-05](../../specs/003-health-assessment/local-artifact-review-contract-proposal.md#requested-decision)
Test plan: [bounded artifact-review tests](../../specs/003-health-assessment/local-artifact-review-test-plan.md)

## Intended outcome and boundary

After approval, a consultant can explicitly review or withdraw review of individual fixed fictional artifacts in one new opt-in localhost profile, with exact-source attestation, durable attributable history, stale-source invalidation and unchanged generated originals. Review means Reviewed for planning; it does not establish correctness, supported remediation or execution safety. Existing nine profiles remain unchanged.

The owner explicitly approved the exact AR13-01–05 proposal; [the source-bound decision](../../specs/003-health-assessment/local-artifact-review-approval.md) closes its human dependency. Cycle13 now implements that bounded local behavior. It does not declare the broader pilot ready. Task conversion/workflow/CSV/priority/effort/customer objectives, artifact editing/execution, publication, real source/provider, production identity, production/customer migration activation and release are outside this cycle.

## Requirement traceability

| Work | Requirements/criteria | Planned evidence |
| --- | --- | --- |
| Exact-source original/attestation separation | FR-HAS-35/37/42; AC-HAS-18; IP-HAS-008 | AR13-T01/T03/T08/T11, immutable originals and source denials |
| Explicit local authority and attributed history | FR-HAS-37/50; AC-HAS-14/18; approved authorization/audit policies | AR13-T02/T04/T05/T06/T12 |
| Safe source refresh and inert consultant actions | FR-HAS-40/41/42; AC-HAS-13/18 | AR13-T07/T09/T10; actual browser and automated accessibility subset |
| Compatibility and no execution/export | AC-HAS-14; TP-HAS-002/003/014/017/018 subset | AR13-T08/T11/T12 plus existing regression suites |

## Preparation checkpoint

- [x] Audited committed feature specifications and Cycle11/12 boundaries, separately from concurrent BFF/UI proposals.
- [x] Identified missing artifact authority/attestation/invalidation and durable workflow decisions; task-export role discrepancy remains separately open.
- [x] Prepared exact AR13 proposal, traceable implementation packets and bounded positive/negative test plan.
- [x] Recorded authorized human approval of the exact AR13 contract revision.
- [x] Human decision recorded before implementation; engineering review cannot replace it.

Preparation evidence is [metadata only](../../docs/development/evidence/local-pilot-cycle13-preparation.json). It records documentary checks and independent reviews, never runtime PASS.

## Parallel implementation packets — internal contract checkpoint

All writing packets start in separate `codex/` worktrees from the same coordinator-approved contract/configuration checkpoint. Worker count may be reduced for dependencies. No worker writes shared canonical records or operates the private Site. Each returns immutable commit/patch, touched paths, requirement coverage, actual commands/results, migrations/config implications and unresolved cases. A worker other than the author reviews each packet.

| Packet | Exact intended ownership | Dependency and acceptance | State |
| --- | --- | --- | --- |
| A13 domain/store | New `src/server/modules/SyntheticFixReview/*.cs`, module README, `migrations/fix-review/001-initial.sql`; new `tests/unit/SyntheticFixReview.Tests` except project/lock; no shared configuration | Approved AR13 contract, settled schema/source concurrency fence; immutable originals, authority, source binding, append-only event/current/receipt transaction, replay/conflict/restart; T01–T06/T12 | COMPLETE — executed evidence and non-author review closed |
| B13 presentation | New `src/web/src/ArtifactReviewPanel.tsx/.css`; new `tests/unit/ArtifactReviewComponent.Tests` except project/lock | Approved closed overlay DTO and exact action schemas; independent component states/text/hostile content/source fences/focus/reflow; no host/backend edits | COMPLETE — executed evidence and non-author review closed |
| V13 verification | New `tests/integration/LocalArtifactReview.Tests` except project/lock; new `tests/e2e/artifact-review` | Independent literal/source/authority/state/concurrency oracles before reading author conclusions; later actual owned PG/host/browser composition, historical regressions, non-author reviews | COMPLETE — executed evidence and non-author review closed |
| Coordinator | Projects/locks/solution/workflow; AssessmentRuns/LocalConsultantDemo private integration; narrow `RecommendationGuidanceBuilder.cs`/`DraftSnapshotBuilder.cs` source validators; `contracts/local-demo`, type generation, `api.ts`/`App.tsx`/`AnalysisView.tsx`/`useArtifactReview.ts`/`FixPackagePreview.tsx` strict profile/lock/coherence guards, canonical records, integration and existing private Site | Preserve unrelated workspace edits; separate new profile/input locks; combined applicable checks, exact evidence, reviews, cleanup and confirmed owner-private publication | COMPLETE — combined local/hosted evidence, canonical closure and private publication |

Before coding, record an exact compatibility checkpoint for the new profile/application/contract lock through saved inputs, source validators, captured guidance/draft/package reads and strict web guards. New-profile acceptance is explicit; historical nine-profile byte checks and old eleven/twelve-field locks remain strict. Shared source-concurrency/schema/API contracts are settled before dependent code, never through competing worker assumptions. Workers request cross-owner edits from the coordinator. No new third-party dependency is currently justified.

## Verification and closure after approval

- [x] Execute AR13-T01 through T12 and record exact PASS/FAIL/NOT VERIFIED per source.
- [x] Pinned full-solution locked audited restore, formatting, lint/type checks, zero-warning Release build, new and current unit/portable/PostgreSQL integrations, architecture/secret/infrastructure/collector checks run.
- [x] New actual-browser scenarios and all prior applicable owned-host browser suites run against frozen combined source; inspect desktop/mobile/320 capture and focus behavior.
- [x] Configured Linux, Windows2022/2025 and container checks run; distinguish their exact source/environment/counts from local evidence.
- [x] Non-author findings close without weakened assertions; original executed worker source, binary closures, consumed fixtures, logs and review proofs are retained and rehashed before clean checkout removal.
- [ ] Canonical implementation/status/evidence/operations handoff updated; existing owner-private Site status and human board published and confirmed.

No check is checked because it is listed here. Full Milestone7, TP-HAS-018, local build completion, UAT, deployed sandbox/identity, supported manual accessibility and G1–G9 remain NOT VERIFIED. The known default-Mac startup limitation remains open; any temporary test-only workaround is separately disclosed.

## Migration, rollout and rollback proposal

Recorded AR13 approval allows one new isolated synthetic schema and new opt-in profile, not migration of a customer data plane. Before application, validate exact schema digest, dedicated fictional DB ownership and additive compatibility. Preserve existing review/history/assessment schemas and protected fixtures. Rollback disables new profile/actions while retaining compatible read access and append-only data; never delete events to restore an older binary. Production retention, real identity and deployment need their own existing gates.

## Resume instruction

This bounded cycle is closed. Read its exact evidence and remaining limits before selecting new work. Task/conversion/priority/effort and CSV authority require an approved later contract; do not restart completed packets or infer new permissions from this closure. No recurring job or unattended agent was started.

## Frozen implementation checkpoint

The [reviewed internal contract](../../specs/003-health-assessment/local-artifact-review-implementation-contract.md) has SHA256 `a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f` (exact UTF8/LF/final-newline bytes). A13/B13/V13 reviewed it before implementation. V13 preserved its independent oracle first. Shared framework-only run fence and project configuration belong to the coordinator; A13 owns only its additive new migration. All current source writers join the same fence before row locks; old migration/input bytes remain unchanged. Writing worktrees branch from the coordinator checkpoint containing this exact contract/configuration.

## Executed bounded closure — 2026-10-02

The approved localhost Consultant can review or withdraw review of an individual fictional artifact, with exact-source attribution and continuous append-only history. Generated originals remain byte-identical and Unverified. A source change gives Needs review; current withdrawal remains Unverified after refresh. Exact command replay returns its original historical metadata-only receipt. All current source writers share a run transaction fence, and unavailable or inconsistent source suppresses actions.

[Exact developer evidence](../../docs/development/evidence/local-pilot-cycle13-developer-checks.json) binds code804d71a, original worker generations, retained failures/corrections, native logs, independent reviews and preserved archives. A13 passed212 portable and370 combined PostgreSQL assertions; coordinator369 on a fresh dedicated database omits only the author's separate older-original-database case. B13 passed572 assertions. V13 passed218 portable,911 combined PostgreSQL and10,564 actual-browser checks, with33 independent byte oracles. Three new viewport axe audits had0violations/0incomplete; all six captures were visually inspected. Counts from repeated/overlapping executions are not additive. All nine prior browser flows and applicable58-project local checks passed; configured hosted results are recorded on the exact code commit.

The only additive migration is `migrations/fix-review/001-initial.sql` (SHA256 `def3557d73879f4fc199d8152b1ff6c404f2e2477a99f7e50b031cccf5d92ab8`). Activation needs the explicit artifact-review flag and dedicated Cycle13 synthetic database prefix. Existing nine profiles remain separate. Rollback disables new actions/profile and preserves append-only records; no data deletion or customer migration. No third-party dependency was added.

Original source/runtime/consumed fixtures/logs were preserved and rehashed. A13 original265 bindings were independently checked before a whitespace rebuild;254 were retained afterward, with11 generated obj intermediates unavailable and explicitly recorded. Original executed source and Release runtime remain available. Initial A13/B13 failed-generation provenance limitations remain disclosed. Root UI/BFF/research changes were preserved; their separate composed build is bounded evidence.

The next substantial slice needs an exact task/conversion/priority/effort contract and resolution of the Auditor CSV-role discrepancy. This cycle does not authorize those choices. Full Milestone7/TP-HAS-018, local build completion, UAT, G1–G9, real source/provider and customer/production activation remain NOT VERIFIED. Supported manual accessibility, default Mac startup and shared-cluster restart are also unverified. Local host tests used the disclosed test-only configuration-watch override. No hard-kill mid-commit result is claimed.
