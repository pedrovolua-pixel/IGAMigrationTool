# Assessment workspace presentation packet

Status: VERIFIED — bounded local presentation packet completed · 2026-10-02 · coordinator-owned UI-W1

The owner selected the recommended Assessment workspace direction after reviewing the [UI research and mockups](../../product/research/ui-reference-review-2026-10-01.md). This packet applies that direction to the existing synthetic consultant view under the approved [local pilot build](../active/one-identity-local-pilot-build.md), health-assessment product/technical specifications, implementation plan and test/accessibility plans. It is a presentation and navigation slice; it does not approve additional product scope or close G1–G9.

## Bounded implementation

UI-W1 owns `src/web/src/App.tsx`, new `WorkspaceNavigation.tsx` and `workspace.css`, its own browser check under `tests/e2e/assessment-workspace/`, and this packet. The coordinator appends narrowly scoped evidence to feature records and maintains the existing private status Site. Preserve concurrent cycle07, BFF and research changes. A non-author reviewer reads this exact slice without writing shared files.

- Keep five plainly labeled destinations visible: Assessments, Findings, Evidence, Reports and Settings. They move focus to existing sections of the same mounted workspace; no router, API, permission, persistence, dependency or invented settings is introduced.
- Put scope, baseline and profile choices in a visible run-settings panel. Frozen selections still change only by starting a new synthetic run.
- Keep coverage, gaps, immutable evidence, finding review and draft-report warnings intact. Missing analysis/report sections explain their availability and focus the selected-run region rather than silently jumping nowhere.
- Apply the reviewed light slate/blue workspace shell, readable sans-serif typography and responsive navigation. All existing run callbacks, request validation, polling and child components retain their behavior.
- Keep narrow screens usable without hiding navigation behind a menu. Preserve keyboard focus, skip link, live announcements, textual states and forced-colors support.

## Acceptance and evidence

Run pinned frontend contract generation check, formatting, type checking, production build and dependency audit. Execute actual-browser section navigation/focus, no-run/unavailable behavior, existing run/review/report regression checks where the local host is available, desktop and 320px reflow, forced colors and axe. Record exact executed checks and limitations. Full supported Windows/browser/assistive-technology acceptance remains NOT VERIFIED. No server change or database migration is part of this packet; disposable synthetic test infrastructure is isolated.

## Execution record

The first shell is implemented. All existing assessment sections remain mounted; the five shortcuts focus existing content. No new route, permission, API, settings policy, migration or dependency was added. Settings remain reachable at short desktop heights through a scrollable sidebar, and navigation resets on run changes.

Pinned contract/format/type/build/audit checks passed. The actual synthetic host Release build passed with zero warnings/errors. Final Chrome154 read-only snapshot verification passed all navigation/focus/availability assertions, seven viewport sizes, forced colors and four axe4.13.0 scans with zero confirmed violations. Manual contrast/ARIA checks remain incomplete. A non-author reviewer found no remaining issues after the two navigation fixes and independently rechecked empty/ready states and four viewport sizes. [Exact evidence](../../docs/development/evidence/assessment-workspace-ui-20261002.json) binds the source hashes, results, corrections and limitations.

The shared fixed-port host was occupied by other pilot work. A legacy regression launch was interrupted, an isolated host launch refused the occupied port, and neither is counted as completed regression evidence. Final UI verification used frozen GET-only synthetic snapshots in memory; its preview refuses all mutations. The task-owned disposable cluster was stopped. Full backend mutation/recovery regression and supported Windows/manual accessibility acceptance were not run by this packet. G1–G9 remain NOT VERIFIED. The original publication attempt was blocked by a temporarily missing Sites helper; the unpublished separate checkout remains preserved at `/private/tmp/iga-ui-status-site`. UI-W2 subsequently executed all five existing actual-host mutation/recovery/browser suites against the integrated workspace (8198 assertions/checks), with exact evidence in its completed packet. Private UI-W1/UI-W2 board publication is confirmed: version64, source `fa36bf5f6a747e3087099028fb7087c25f4ed25b`, deployment `appgdep_6abfc33d3a048191a7b9cc4eddc39ba8` succeeded at 2026-10-02T14:44:20Z. The existing owner-private audience and eleven human tasks are preserved. The earlier missing-helper blocker is resolved. Full Windows/manual accessibility and G1–G9 acceptance remains NOT VERIFIED.
