# Local pilot consultant artifact review — Cycle13

Status: AWAITING HUMAN CONTRACT DECISION — preparation reviewed; implementation not started
Owner: Coordinator
Last updated: 2026-10-02
Source checkpoint: `874c653a495921dbf20f628715b4ce70f3ecc030`
Product/technical authority: approved feature003 specifications and approved local synthetic build
Required decision: [AR13-01 through AR13-05](../../specs/003-health-assessment/local-artifact-review-contract-proposal.md#requested-decision)
Test plan: [bounded artifact-review tests](../../specs/003-health-assessment/local-artifact-review-test-plan.md)

## Intended outcome and boundary

After approval, a consultant can explicitly review or withdraw review of individual fixed fictional artifacts in one new opt-in localhost profile, with exact-source attestation, durable attributable history, stale-source invalidation and unchanged generated originals. Review means Reviewed for planning; it does not establish correctness, supported remediation or execution safety. Existing nine profiles remain unchanged.

Cycle13 currently prepares the missing contract, work packets, acceptance tests and independent review. It does not implement artifact approval/history, change a permission or declare the broader pilot ready. Material decisions cannot be inferred from Next cycle. Task conversion/workflow/CSV/priority/effort/customer objectives, artifact editing/execution, publication, real source/provider, production identity, production/customer migration activation and release are outside this cycle.

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
- [ ] Recorded authorized human approval of the exact AR13 contract revision.
- [ ] Implementation begins only after that decision; engineering review cannot replace it.

Preparation evidence is [metadata only](../../docs/development/evidence/local-pilot-cycle13-preparation.json). It records documentary checks and independent reviews, never runtime PASS.

## Parallel implementation packets — blocked pending approval

All writing packets start in separate `codex/` worktrees from the same coordinator-approved contract/configuration checkpoint. Worker count may be reduced for dependencies. No worker writes shared canonical records or operates the private Site. Each returns immutable commit/patch, touched paths, requirement coverage, actual commands/results, migrations/config implications and unresolved cases. A worker other than the author reviews each packet.

| Packet | Exact intended ownership | Dependency and acceptance | State |
| --- | --- | --- | --- |
| A13 domain/store | New `src/server/modules/SyntheticFixReview/*.cs`, module README; new `tests/unit/SyntheticFixReview.Tests` except project/lock; no shared configuration | Approved AR13 contract, settled schema/source concurrency fence; immutable originals, authority, source binding, append-only event/current/receipt transaction, replay/conflict/restart; T01–T06/T12 | BLOCKED — human decision |
| B13 presentation | New `src/web/src/ArtifactReviewPanel.tsx/.css`; new `tests/unit/ArtifactReviewComponent.Tests` except project/lock | Approved closed overlay DTO and exact action schemas; independent component states/text/hostile content/source fences/focus/reflow; no host/backend edits | BLOCKED — human decision, then DTO |
| V13 verification | New `tests/integration/LocalArtifactReview.Tests` except project/lock; new `tests/e2e/artifact-review` | Independent literal/source/authority/state/concurrency oracles before reading author conclusions; later actual owned PG/host/browser composition, historical regressions, non-author reviews | BLOCKED — human decision |
| Coordinator | Projects/locks/solution/workflow; AssessmentRuns/LocalConsultantDemo private integration; narrow `RecommendationGuidanceBuilder.cs`/`DraftSnapshotBuilder.cs` source validators; `contracts/local-demo`, type generation, `api.ts`/`AnalysisView.tsx`/`FixPackagePreview.tsx` strict profile/lock/coherence guards, canonical records, integration and existing private Site | Preserve unrelated workspace edits; separate new profile/input locks; combined applicable checks, exact evidence, reviews, cleanup and confirmed owner-private publication | PREPARATION READY — implementation blocked |

Before coding, record an exact compatibility checkpoint for the new profile/application/contract lock through saved inputs, source validators, captured guidance/draft/package reads and strict web guards. New-profile acceptance is explicit; historical nine-profile byte checks and old eleven/twelve-field locks remain strict. Shared source-concurrency/schema/API contracts are settled before dependent code, never through competing worker assumptions. Workers request cross-owner edits from the coordinator. No new third-party dependency is currently justified.

## Verification and closure after approval

- [ ] Execute AR13-T01 through T12 and record exact PASS/FAIL/NOT VERIFIED per source.
- [ ] Pinned full-solution locked audited restore, formatting, lint/type checks, zero-warning Release build, new and current unit/portable/PostgreSQL integrations, architecture/secret/infrastructure/collector checks run.
- [ ] New actual-browser scenarios and all prior applicable owned-host browser suites run against frozen combined source; inspect desktop/mobile/320 capture and focus behavior.
- [ ] Configured Linux, Windows2022/2025 and container checks run; distinguish their exact source/environment/counts from local evidence.
- [ ] Non-author findings close without weakened assertions; original executed worker source, binary closures, consumed fixtures, logs and review proofs are retained and rehashed before clean checkout removal.
- [ ] Canonical implementation/status/evidence/operations handoff updated; existing owner-private Site status and human board published and confirmed.

No check is checked because it is listed here. Full Milestone7, TP-HAS-018, local build completion, UAT, deployed sandbox/identity, supported manual accessibility and G1–G9 remain NOT VERIFIED. The known default-Mac startup limitation remains open; any temporary test-only workaround is separately disclosed.

## Migration, rollout and rollback proposal

AR13 approval would allow one new isolated synthetic schema and new opt-in profile, not migration of a customer data plane. Before application, validate exact schema digest, dedicated fictional DB ownership and additive compatibility. Preserve existing review/history/assessment schemas and protected fixtures. Rollback disables new profile/actions while retaining compatible read access and append-only data; never delete events to restore an older binary. Production retention, real identity and deployment need their own existing gates.

## Resume instruction

Read the actual human decision and finalized contract first. If AR13 choices are amended, update this plan/test/DTO/schema before dispatch. Start A13 and independent V13 oracle design together; B13 can implement the approved component contract once settled. Coordinator integrates after non-author review, runs the combined checks and publishes the existing private board. No recurring job or unattended agent was started by this preparation checkpoint.
