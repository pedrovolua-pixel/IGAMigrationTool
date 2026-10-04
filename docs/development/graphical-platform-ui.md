# Approved graphical platform workspace

The owner approved the complete recovered-design mockup on 2026-10-03. [UI-W4](../../plans/active/local-pilot-approved-platform-ui.md) implements its presentation in the existing synthetic React pilot. It replaces the earlier UI-W3 interpretation of the preferred landing; immutable host contracts and accepted ADRs are unchanged.

## Rendering contract

The Overview uses only an admitted analysis matching the selected run ID and revision. Health and categories come from saved DTOs, severity counts come from actual finding rows, and current review states override original states where present. Gaps remain separate quality values. Missing health, historical health, category scores and topology do not receive sample defaults. Category graphs have text/table equivalents; relationships visualize only explicit finding-to-object/reference associations, never an inferred object inheritance chain.

All navigation areas are reachable, with explicit empty or unavailable states. Existing configuration/review/artifact/task/report components stay mounted across same-run area changes. Analysis admission retains all prior source, revision, snapshot and digest checks. Protected-reference routing and mutation transports are unchanged. Appearance preferences persist only non-evidence display choices; server configuration and customer authority cannot be edited locally.

The approved static design is available separately under `/design-review/`. That surface is explicitly fictional, uses scripted AI responses and never operates the backend. Future-phase page links point to this design context; navigation approval does not enable interactive AI, provider access, publication, customer policy, integrations, cross-customer portfolio or migration execution.

## Verification boundaries

See UI-W4's preimplementation tests and dated execution evidence. Automated/local browser results do not establish manual supported-browser/screen-reader acceptance or G1–G9. A read-only preview can verify visual/navigation/state retention but cannot establish mutation/recovery success. No migration, configuration or dependency changes are required for this presentation packet.

Release-readiness integration preserves Overview as the initial landing. Starting or explicitly opening a saved run selects Assessments so the existing run-detail focus and controls stay visible. Browser regression drivers use the real navigation control before interacting with an initial/reloaded Assessments form; semantic/transport/authentication checks remain intact.

Existing server-generated run/task/finding deep links retain their Assessments admission and focus path on initial navigation. A query identifier remains a selection hint only; the owning server still resolves exact scope and authority. Ordinary entry, including remembered browser selection, lands on Overview.
