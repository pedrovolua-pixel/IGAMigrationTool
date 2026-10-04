# Approved internal workspace UI — UI-W3

Status: VERIFIED — bounded local engineering checks and private checkpoint publication confirmed. Human approved the reviewed UI changes on 2026-10-03, after requesting retention of the existing landing page. This packet implements the internal Findings, Evidence, Reports and Settings mockups in `work/ui-review/2026-10-03/index.html`. It is presentation and local navigation within the approved synthetic pilot, under FR-HAS-40–44 and TP-HAS-002/003/013/014/017. Product and technical specifications, accepted ADRs and the existing UI-W1/UI-W2 contracts remain authoritative.

## Scope and acceptance

Retain the existing Assessments landing layout and run controls. Give the five existing destinations dedicated mounted views; keep review drafts, filters, selection and polling intact across navigation. Improve findings list/detail composition, provide evidence/recommendation/original disclosure, and support mobile list/detail return and keyboard focus. Reports render the existing canonical unpublished draft. Settings uses the existing new-run form and immutable selected-run inputs. Empty/unavailable destinations return to Assessments with an explicit notice. Preserve Phase 1B controllers and their existing protections. No new endpoints, permissions, dependencies, live sources, publication, saved views, scoring policy or remediation authority.

## Ownership and execution

- Coordinator: App.tsx, WorkspaceNavigation.tsx, AnalysisView.tsx, workspace.css; integration, canonical records, checks, private site.
- Writing worker UI-W3-F: FindingsExplorer.tsx and FindingsExplorer.css only, in an isolated `codex/ui-w3-findings` worktree from HEAD `5fdb19e`, overlaid with the current uncommitted baseline. Compact rows and detail disclosure/mobile return without changing supplied evidence or review contracts.
- Independent verification worker UI-W3-R: read-only review of integrated UI scope, state retention and accessibility; report findings and executed evidence. Agent review does not replace manual human acceptance.

## Verification and rollback

Run frontend contract, format, type, build and dependency audit checks; execute applicable owned-host synthetic browser regression checks; inspect actual desktop/mobile rendering and keyboard navigation. Record unavailable checks explicitly. Supported Windows/browser/screen-reader acceptance remains NOT VERIFIED. Verify landing remains structurally unchanged, unavailable destinations, same-run filter/editor retention, run-change reset, canonical report coherence and inert hostile content. Rollback consists of reverting this packet's presentation/navigation changes without touching prior dirty files or host data. No migrations or deployment configuration changes are expected.

## Completion record

Implemented approved dedicated views with the existing landing retained. Pinned frontend checks, seven new browser groups, existing workspace/finding checks and five historical suites passed their executed assertions. Two independent focus/feedback findings were corrected and replayed. Exact source/assets, executed checks, temporary port substitutions, skipped review/maturity restart and manual limits are in [evidence](../../docs/development/evidence/approved-workspace-ui-20261003.json). Automated scans have zero confirmed violations with incomplete contrast retained. No migrations, configuration or contracts changed. Owner-private UI checkpoint publication confirmed as version97/source `d4d3bd1d22e710fce3ee9db6dcc00ea49a5a5371`; all twelve task cards byte-preserved and gates unchanged.
