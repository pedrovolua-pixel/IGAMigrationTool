# Local cycle06 — canonical synthetic draft-report preparation

Status: APPROVED LOCAL SCOPE / RUNNING — owner's “Move to the next cycle” on2026-10-02 continues the approved local build.
Owner: Coordinator / repository owner
Source checkpoint: `0a58364de8a60dea5d9ef64d9e6b3b2c381eae9f`

## Authority and runnable outcome

Implement a bounded read-only slice of the approved [feature product](../../specs/003-health-assessment/product-spec.md) FR-HAS-21/40/43/44/45/46, [technical design](../../specs/003-health-assessment/technical-spec.md) reporting module/canonical projections, Milestone9, TP-HAS-002/003/005/007/009/013/017/020 and a pure-value subset of006. [The approved local build](one-identity-local-pilot-build.md) authorizes synthetic implementations before live integration. Accepted ADR-0001/0002/0003/0004 govern modular ownership, scoped reads, immutable projection values and renderer separation. The two cycle05 review/maturity profiles already supply the complete saved analysis, current review snapshot and independent maturity inputs needed for this read-only draft.

The consultant sees a progressively disclosed summary and technical draft, separate quality details, stable original/current finding references, available healthy controls and explicit unavailable later sections. A structured Markdown **text preview** derives from the exact same canonical draft value. Scope/run/input/catalog/profile/application/scoring/review/maturity bindings and a deterministic content digest identify that value. Refresh after a review action creates a new current draft value; a previously returned value remains immutable. No durable draft history is claimed.

Current synthetic runs remain `Scoring`; coverage readiness is not assessment completion. Every view prominently says synthetic draft/unpublished. No `ReportVersion`, new run state, persistence schema, warning acknowledgment, publish/share/download/export route, PDF renderer, customer audience grant or BFF integration is introduced. Markdown is encoded inert text in the existing consultant screen. Actual publication needs a terminal run, current-score/review fencing, explicit warning acknowledgment, `report.publish`, approved exact write contract and its own verification. Existing six profiles, migrations, review commands and historical run inputs retain their semantics.

## Private contract and module boundary

Extend only the existing private `GET .../analysis` read projection with nullable `reportDraft`, populated only for the two already opted-in review/maturity profiles after their existing engine/review/maturity validation. Earlier profiles receive null. No new transport route or mutation is added. The existing loopback/start flag, fixed server synthetic identity, scope checks, Host/Origin/antiforgery/CSP and strict generated DTO schema remain in force.

The internal `ReportDrafts` module receives already-authorized, engine-validated inputs; it cannot resolve a scope, database, raw reference, network location, renderer output or authority. Its versioned input binds customer/project/environment, run ID/revision/Scoring state, original run input digest and versions, analysis fixture/content digest, review snapshot digest, maturity fixture/input/content digest and application version. It receives a bounded JSON data-only projection of the current health/category/object/module/outcome/quality/findings/coverage strengths/review history and maturity details. JSON is detached/cloned, properties canonicalized, ordered set-like lists normalized by declared stable keys and record arrays preserve documented history order. Dynamic read observation time, actions/buttons and transport tokens are excluded. Unknown/missing/invalid versions, bindings, duplicate IDs, invalid required data or cross-run review linkage return an unavailable draft; no stale or simpler fallback is allowed.

One immutable draft value is the source for the summary/technical UI and Markdown; its digest binds all authorized canonical content and all versions. Digests are identifiers, never grants. Plain text stays plain text, including reviewer-controlled titles/comments/business context and fictional guidance; Markdown must not introduce raw HTML, executable snippets, active links/images or network resolution from source text. Report sections absent from this bounded fixture (risk acceptance, reassessment, full recommendations/tasks, AI and actual publication) are explicitly unavailable, not zero, approved or silently omitted as complete. Fixed fictional recommendations may be shown only as the existing unverified guidance.

## Isolated work packets

[Parallel workflow](../../docs/development/parallel-agent-workflow.md): coordinator plus three bounded workers, same recorded plan checkpoint, distinct `codex/` branches and worktrees. Coordinator owns shared solution/host/schema/frontend/CI/canonical records and private site. Settle the internal input/result signature before dependent worker edits.

| Packet | Owner | Exact owned paths | Acceptance |
|---|---|---|---|
| D6 | Draft projection worker | `src/server/modules/ReportDrafts/ReportDraftContracts.cs`, `DraftSnapshotBuilder.cs`, `ReportDrafts.csproj`, `packages.lock.json`, `README.md`; `tests/unit/ReportDrafts.Tests/**` | Pure versioned frozen draft, deterministic digest/order/deep immutability, invalid-input denial; no storage/authority dependency |
| M6 | Markdown worker | `src/server/modules/ReportDrafts/StructuredDraftMarkdown.cs`; `tests/unit/ReportMarkdown.Tests/**` | Structured inert Markdown from the exact D6 value; stable IDs/digests/scores/findings/warnings/methodology/quality parity; hostile text and deterministic bytes |
| V6 | Independent verifier | `tests/integration/ReportDrafts.Tests/**`, `tests/e2e/draft-reports/**` | Independent input/digest/parity/compatibility goldens, actual saved-review bridge and browser checks, failure/selection-race/inert-text/keyboard/reflow tests; exact source/artifact/transcript evidence |
| I6 | Coordinator | host `DemoReportDraftProjection.cs`/existing analysis bridge, solution/project references, strict private schema/generated types, `src/web/src/DraftReportView.tsx`/owned integration/style files, workflow, canonical docs/evidence/site | One coherent source read; bounded UI; old-profile compatibility; independent review of integration; all applicable checks; confirmed private board |

Every packet gets a non-author review. Worker reviews supply engineering evidence and cannot accept milestones, production permissions or accessibility/live gates. Worktree additions are local/temporary; preserve committed packets before cleanup. BFF integration and unrelated research/work files remain outside this packet.

## Concrete verification plan

- D6-001: identical bytes/digest for identical frozen input, culture-independent decimal display, stable ordering, duplicate/missing/unknown/mismatched input denial and no mutation after caller collections/document disposal or changed current review. Pure returned-value immutability is a subset ofTP006; no durable publication is claimed.
- D6-002: bind every source version/digest/scope/revision and finding original/current/review revision; altered original/review/maturity/content changes the draft digest or denies input. Never accept a client digest as permission. Preserve source quality/gaps and unavailable results.
- M6-001: same scores, status, maturity, stable finding IDs/current/original presentation, quality/warnings, source lock and digest in UI and Markdown. Fixed reference literals/independent expected section checks; do not derive expected content solely with the implementation helper.
- M6-002: multiline/backticks/fences/HTML/Markdown links/images/script/shell/formula text remain inert with no active URL or resource load; stable text output and heading/table semantics. No recommendation execution or report export/download capability.
- V6-001: actual PostgreSQL saved run + review actions produce a coherent new draft; old returned value stays unchanged; restart/reload reproduce the current value; historical profiles stay null/read-only. Core and integration tests use dedicated disposable synthetic DBs, sequential schema probes before browser, never restart/drop the shared cluster.
- V6-002: actual keyboard summary/technical/Markdown preview, correct focus/retry, correct quality separation, transient read denial, aborted/delayed cross-run reads, visible DOM/Markdown parity, no raw HTML/network navigation; desktop/390/320 reflow and axe engineering checks. Full Windows/NVDA/Narrator/manualWCAG and PDF acceptance remain NOT VERIFIED unless actually run.
- Coordinator: pinned audited locked restore, whole-solution format/Release build, current unit/integration/architecture suites, collector Windows cross-publish/current hosted runners, frontend generated contract/type/format/build/dependency audit, applicable Bicep/infrastructure/secret scans, historical recovery/analysis/review browser regressions on final assets, new configured partial CI and exact metadata bindings. Report all unavailable checks and disclosed corrections. No milestone/G1–G9 advancement follows.

## Rollback and completion

Disable/remove the read-only draft field/view while retaining a compatible private reader. No new DB migration, package version, retention rule or durable history is required. A previous compatible host retains all existing saved runs/reviews. Canonical implementation/status/evidence and the existing owner-private status site/human board update in this cycle; preserve its audience and BFF owner dependency. Close only after independent findings, applicable executed checks, immutable evidence and matching private publication are confirmed.

## Execution record

Read-only D6/R5 contract assessments selected draft preparation before reassessment because current runs are still Scoring and recurrence lacks durable native-identity/lifecycle inputs. Implementation and verification packets are pending. No check is claimed passed for cycle06.
