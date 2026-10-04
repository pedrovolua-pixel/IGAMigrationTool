# Approved graphical platform UI — UI-W4

Status: COMPLETE — bounded local presentation implementation and private checkpoint verified. Owner approval: 2026-10-03, directly following review of the complete navigable mockup at `work/ui-review/2026-10-03/platform-mockup/`. This supersedes UI-W3's assumption about the preferred landing design: use the recovered chart-led overview and unblurred contextual relationship overlays.

## Scope and decisions before implementation

Implement the approved visual composition in the existing React/TypeScript synthetic pilot. All 18 navigation areas are reachable. Present existing verified run/catalog/history/analysis/review/AI-preview/guidance/task/report records through those areas; never substitute the mockup's fictional numeric constants for host data. Unknown scores, missing categories, historical health and unprovided topology remain explicitly unavailable. The canonical backend calculations and immutable content stay unchanged.

Preserve existing start/cancel/resume, coherent-analysis admission, CSRF, review, artifact and task controllers. Maintain mounted forms when navigating the same run. Changing run clears prior protected results and selections. Keep protected-reference routes unchanged. Local appearance preferences may change presentation only and persist no evidence. AI chat/deep analysis, customer access/policy, external integrations, publication, portfolio and migration functions without existing admitted adapters remain unavailable or explicitly separate design previews. This approval does not activate future phases or authorize production release.

Requirement trace: FR-HAS-2/3/9–21/30–37/40–50/55; AC-HAS-2/3/5/7/8/9/13/14/17/18/20; TP-HAS-002/003/005/007/008/009/013/014/017/018/020. Existing product/technical specifications, test plan and accepted ADR0001–0004 apply. No backend/public-contract/permission/architecture/dependency changes. Future phase screen designs remain review artifacts, not implemented product capabilities.

Visual thesis: the recovered light workspace, a single blue action accent, chart-led hierarchy, and context-preserving overlays without blur. Content plan: overview → operational navigation → progressively disclosed evidence and controls. Interaction thesis: restrained page entrance, selected navigation feedback, and overlay transition; respect reduced motion.

## Ownership and packets

- Coordinator UIW4-C: App.tsx, AnalysisView.tsx, WorkspaceNavigation.tsx, workspace.css, FindingsExplorer.tsx/CSS, public/design-review; integration, preserved prototype files, plans/specification notes/status/evidence, checks, private status site.
- Worker UIW4-O: isolated `codex/ui-w4-overview` worktree from recorded HEAD plus current UI baseline; only new `src/web/src/PlatformOverview.tsx` and `PlatformOverview.css`. Pure rendering of admitted AnalysisDetail/RunDetail; score gauge, category chart, severity chart, hotspots, quality; no requests or constants posing as results.
- Worker UIW4-A: isolated `codex/ui-w4-areas` worktree from the same baseline; only new `src/web/src/PlatformAreas.tsx` and `PlatformAreas.css`. Read-only catalog/run/history areas, existing analysis-derived rule/outcome/audit views, local appearance settings, and honest future/unavailable presentation. No network, HTML injection, mock persisted policy or actor authority.
- Independent review UIW4-R: read-only integrated inspection by a nonauthor worker after handoff; correctness, unavailable states, no fabricated topology/health, focus/dialog/mobile, stale-state and permission preservation. Agent review is not human acceptance.

## Preimplementation test plan

1. Pinned contract generation check, frontend typecheck, Prettier, Vite build and dependency audit; no dependency additions. Inspect scoped diff and preserve unrelated dirty work.
2. Browser checks with a task-owned synthetic fixture preview: all areas reachable; overview score/category/severity derives from exact current admitted records; empty/unavailable/coverage-only cases never show a sample score; same-run navigation retains editors; run changes clear previous results.
3. Existing typed controllers stay mounted; finding/review/report original digests remain unchanged by navigation. AI and future actions do not produce provider or destination traffic. Untrusted titles and references remain inert React text.
4. Unblurred finding inspector: keyboard open/close, Escape, focus return and containment; actual supplied object/reference graph only, with table alternative and explicit unavailable relationship edges. No implied inherited role chain unless the host supplies it.
5. Desktop/390/320 layout and visible focus; document overflow checks and automated accessibility engineering checks where available. Manual supported Windows/browser/screen-reader and complete operational acceptance remain NOT VERIFIED.
6. Capture source/asset checksums, executed checks and unavailable portions in `docs/development/evidence/approved-platform-ui-20261003.json`; keep canonical implementation/status/evidence current and publish the existing owner-private status site. Gates stay unchanged.

## Recovery and completion

Preserve the preimplementation dirty UI in a dated work snapshot before integration. Rollback restores only UIW4-owned presentation changes, preserving existing dirty work and immutable backend records. Keep the approved standalone mockup and recovered original available for comparison. Scope does not include production release or source collection. Record exact outcomes below before claiming local verification complete.

## Executed local evidence

Configured frontend formatting, contract generation, typecheck, production build and dependency audit passed. Gitleaks found no frontend leaks; whitespace check passed. Rendering checks passed66 assertions for unavailable/stale records, current review and escaped hostile labels. Nonauthor reviews have no outstanding findings. Browser checks covered all18 areas, eight settings tabs, same-run unsaved review/filter preservation, cross-run score clearing, native overlay keyboard/focus/no blur, theme persistence and390/320 reflow. No dependency, backend contract, migration or permission change.

The preimplementation snapshot and original/mockup are preserved. The implemented preview uses frozen synthetic records and rejects writes. Full mutating workflow regression, hosted CI and supported Windows/screen-reader/manual operational acceptance are NOT VERIFIED. G1–G9 and existing human dependencies remain open. Private publication is confirmed on exact site source406b9495096074345fa1198ce8e673f535a45472; the fourteen human task cards are byte-preserved. The dated evidence record gives exact source hashes and scope.

## Closure

Application source, approved complete mockup, recovered original and preimplementation UI are saved locally in Git d8eeee830ac3ca1a9c94f534b6de620a80d22f81; no GitHub push or production application release. Private status publication succeeded; see approved-platform-ui-site-20261003.json. The source update preserves a concurrent release-readiness update and all fourteen existing human tasks. Full pilot and independent operational/manual acceptance remain open.

## Release-readiness integration correction and regression plan

The combined checkout preserves all24approved incoming web paths byte-for-byte before correction. Integrated browser control demonstrated that the old UI-W3 driver expects a visible Assessments start form on entry; UI-W4 intentionally lands on Overview. Update functional drivers to navigate through the real Workspace sections control on entry/reload instead of changing their transport/semantic/authorization assertions. Starting or explicitly opening a saved run must select Assessments and focus its visible run detail, preserving the existing keyboard/mutation/recovery contract. Remove the selected-run effect's unconditional switch to Overview, which hides that focus target after successful start/open. Category inspection clears on a run change; run-bound component state/admission still clears through existing IDs/revisions. Overview stays the initial landing, and all other areas remain reachable.

Execute the existing real synthetic PostgreSQL keyboard/start/cancel/reload/recovery and changed-source browser tests, new18-area navigation/current-score/overlay/state-retention checks, automated accessibility, pinned contract/type/format/build/audit and independent review. Keep the original driver failure and exact code/log evidence. No permission/backend/public contract/provider changes. Host environmental workaround and unavailable supported manual acceptance remain explicit.

The first real integrated browser replay passed strict transport/coverage, keyboard start/reload/focus and cancellation, then its target-size assertion counted23non-rendered controls (closed mobile navigation and hidden areas) as zero-height targets. Scope the size assertion to rendered visible controls and enforce both width and height >=24px; retain the failure and existing axe/focus/reflow checks. Hidden navigation must remain absent from keyboard focus until opened. This aligns the engineering check with actual interactive targets without changing product style or reducing the threshold.

The real navigation helper waits for its documented animation-frame focus target before drivers focus workflow controls and send keyboard input. The first analysis run timed out at keyboard retry (703 checks); an unchanged application diagnostic replay passed708 checks. The original failure is retained. This driver synchronization does not change analysis or retry authority, route assertions, server revision admission or expected scoring.
