# Local cycle 05 — consultant review/history and maturity

Status: VERIFIED — local developer packet; matching private publication pending — owner explicitly approved this exact proposal on 2026-10-01 EDT / 2026-10-02 UTC.

Requested from: Repository owner acting as product, technical and security owner.

Done when: the owner explicitly approves the bounded synthetic behavior, private transport, server-side test identity, new schema and compatibility contract below. Record that decision before implementation. Production release and live gates remain separate.

## Purpose and existing authority

The owner requested the next phase after completed cycle04. Approved feature003 product requirements FR-HAS-12/13/15/17/20, the technical review/maturity contracts, Milestone5, TP-HAS-003/004/005/017 and the authorization matrix already describe review/history and independent maturity. The standing local build permits synthetic implementation. Automatic approval review nevertheless rejected starting this particular cycle because the new mutation surface, synthetic reviewer identity and persistence schema need explicit approval. This proposal supplies those exact boundaries; it does not reinterpret the rejection as authorization.

## Proposed runnable result

New opt-in profiles `synthetic-review-maturity-equal-v1` and `synthetic-review-maturity-operations-v1` use the existing fixed analysis baselines. The consultant can confirm, reject or defer a proposed synthetic finding, add a plain-text comment, and edit its presentation title/business context. Generated originals, per-object occurrences, saved results and run inputs remain immutable. Current scores and mandatory-review counts recalculate from one durable review snapshot; Confirm/Defer retain the penalty and Reject removes the current penalty. An append-only history shows the original, every action, synthetic actor, server time and revision after reload or host restart.

Maturity appears separately from health, using only frozen fictional indicator fixtures. High health cannot promote maturity. Developing requires documented design and repeatable implementation in at least 60% of mandatory domains; Defined requires at least 80% and evidenced governance ownership; Managed also requires measured operation/regular review in at least 80%; Optimized also requires validated improvement across at least two distinct assessments in at least 80%. Every preceding level is required. Partial or insufficient evidence never counts as met. The proposed fixture denominator includes every declared mandatory domain, with missing evidence prominently disclosed; invalid or empty catalogs expose no maturity projection. This denominator convention is included in the requested approval.

## Private transport and permission boundary

- Only the existing loopback port 5183 host with its explicit synthetic startup flag may expose the new routes. Existing Host/Origin, same-origin antiforgery, strict JSON, 4 KiB body limit and browser security headers remain enforced.
- `GET /local-demo/v1/runs/{runId}/review` returns current review projections, original references, history, per-finding revisions and a review-snapshot digest. Existing analysis reads incorporate the same reviewed snapshot for new opt-in profiles only.
- `POST /local-demo/v1/runs/{runId}/findings/{findingId}/events` accepts exactly an event UUID, expected finding revision, event kind and bounded event fields. Disposition targets are only Confirmed, Rejected or Deferred from Proposed; rejection requires a nonblank reason. Comments/business context are limited to 2000 characters and presentation titles to 250. All text is inert, encoded text in the UI. Unsupported transitions and extra fields are refused.
- The server supplies one fixed active synthetic consultant identity assigned to the fixed synthetic customer/project/environment and fixture categories. Clients cannot choose actor, role, assignment, scope, category, originals, weights or permissions. The policy module tests denial of wrong/inactive/revoked identities, assignments, roles, scope, categories and resource states. This is a disclosed local test double and supplies no Entra/customer authority.
- Identical per-finding event-ID/payload replay returns the original outcome; changed replay or stale revisions conflict without partial writes. An append and its current projection update commit in one transaction.
- Customer risk acceptance, remediation/validated closure, recurrence/cross-run correlation, mentions/notifications/attachments, production identity, AI, tasks, fix execution and publication stay disabled or deferred. This slice does not complete Phase1B/1C or a live gate.

## Schema, versions and rollback

A separate module-owned `synthetic_review` PostgreSQL schema stores immutable run/finding seeds, original digests and occurrence references, current finding projections and append-only events. Seeds bind the complete analysis/input digests. Scope accompanies resource keys; transaction locks/revisions prevent competing updates. Migration content/schema drift is refused. This schema never changes the existing assessment migration or rewrites saved assessment rows. Its connection guard accepts only loopback `iga_synthetic_` databases.

The new profiles explicitly map to the unchanged cycle04 analysis profiles and add a whole maturity catalog/indicator-evidence digest to optional run-input metadata. Missing optional fields remain omitted for historical serialization compatibility. Historical cycle03/04 profiles stay read-only and retain their exact inputs, catalog/script/migration digests and recovery behavior. Maturity definitions/evidence are frozen per new run. No actual One Identity catalog is invented or promoted. Npgsql 10.0.3 is already pinned transitively; a direct reference has the concrete transactional-history need and introduces no new package version.

Rollback disables the new profiles/write routes or uses a compatible reader; it never deletes review events or earlier evidence. No Azure deployment, customer migration or production configuration is requested.

## Planned ownership and checks after approval

| Packet | Exclusive writing paths | Required outcome |
|---|---|---|
| M5 maturity worker | src/server/modules/AssessmentMaturity/, tests/unit/AssessmentMaturity.Tests/ | Pure cumulative thresholds, evidence/ownership guards, distinct-assessment improvement, immutable digests and fixed fixtures |
| R5 review worker | src/server/modules/FindingReview/, migrations/finding-review/, tests/unit/FindingReview.Tests/ | Policy test doubles, immutable originals, append-only history, bounded text, revision/idempotency/concurrency and actual synthetic PostgreSQL checks |
| V5 independent verification | tests/integration/ReviewMaturity.Tests/, tests/e2e/review-maturity/ | Independent golden/adversarial cases, database reload/concurrency/rollback and final browser keyboard actions/history/current scores/maturity/a11y/hostile transport |
| U5 supplemental UI packet, reassigned to completed M5 worker in its isolated worktree | contracts/local-demo/demo-v1.schema.json; src/web/src/AnalysisView.tsx, ReviewPanel.tsx, MaturityView.tsx, api.ts, App.tsx, styles.css, demo-contract.generated.ts | Bounded approved review/history and independent maturity screen; generated private DTOs; same-snapshot refresh and explicit identical-event retry |
| Coordinator | Shared configuration, run/profile bridge, host, canonical records and existing private Site | Integrate author-independent reviews, review U5 contracts/UI and execute every applicable combined check |

Writing workers will use isolated worktrees from one known cycle-start commit only after approval. Run pinned audited locked restore, full format/build and all existing unit/integration/architecture checks; actual synthetic PostgreSQL tests; generated private-contract drift, frontend type/format/build/audit; new browser plus historical coverage/analysis regressions; existing infrastructure policies, secret/whitespace checks and Linux/Windows partial CI. Record migrations, versions, source/artifact digests, review findings and unavailable manual/Windows/live evidence. Preserve evidence/commits, clean temporary worktrees and publish the matching owner-private board. Agent review does not replace required human gate approval.

## Decision record

Repository owner responded “Approved” after reviewing the explicit bounded proposal, including private write routes, fixed synthetic consultant identity, separate review schema and maturity denominator convention. Product/technical/security approval is recorded for this local packet. The earlier rejected action did not execute; this subsequent approval authorizes isolated workers and implementation. Production/live gates are unchanged.

## Execution record

Implementation, independent review, local verification and configured remote partial CI are complete. Matching owner-private publication remains the last cycle closure dependency.

M5 `ebdb15e0db021b6088f9c18850a7dda4802e9377`, R5 `964333be841ef1fa6535570aed85b80c66940544`, supplemental U5 `730869f5b723673e07f9dd492bd8f4ebcce50898` and V5 `e883ef066b75b0fbc202928697584dc754a1c31e` were integrated from their exact isolated commits. M5/R5 reviewed each other's module; R5 independently reviewed the coordinator host/run bridge; V5 independently reviewed the UI and R5 reviewed the independent verification packet. The coordinator inspected the final desktop/mobile synthetic review/maturity screenshots and executed combined regressions. No remaining actionable reviewed issue exists in this bounded packet. Agent reviews do not accept a milestone or live gate.

The new opt-in profiles show durable attributed Confirm/Reject/Defer, comments and presentation history, coherent current health/quality, and separately frozen maturity. The server fixes the synthetic actor and grants. Original generated findings, occurrence references, saved coverage and run inputs remain unchanged. All four earlier profiles retain independently pinned baseline8f75eb4 envelopes/input hashes, omission of optional maturity metadata and their original read-only behavior.

### Executed local evidence — 2026-10-02 UTC

- Pinned SDK10.0.401 final audited locked restore, whole-solution formatting and Release build passed with zero warnings/errors. Existing unit/composition/architecture suites, standalone review144 portable plus42 actual PostgreSQL assertions, pure maturity104 checks (200 properties;100,000 domains/500,000 indicators), and self-contained Windows collector cross-publish passed.
- Actual PostgreSQL18.4 final integrated V5 suite passed342 assertions (199 portable included, not additive). This covers independent thresholds/denominators, original binding, policy denials, exact/changed replay, stale/concurrent writes, precommit rollback, immutable SQL/schema/content corruption denial/restoration, both occurrence overlays and all four literal historical locks.
- Existing actual durable PostgreSQL suite passed80 assertions, including worker loss and lease fencing. Cluster crash-restart was not repeated; its explicit opt-in remained absent. Existing analysis/scoring suite passed399 assertions. No customer database, cluster restart/drop or cloud deployment was performed by this cycle.
- Final V5 browser passed2,162 assertions across eight groups, including actual keyboard decisions/comment/edit, reason enforcement, visible scores/review counts/history, inert hostile text, conflicts with focused explicit refresh, committed response-loss exact-event retry, delayed old GET/POST during selecting another run, reload and actual owned-host restart. Counts include ready polling. Historical final-built coverage/recovery browser passed391 checks; historical analysis browser passed712 assertions. Earlier evidence files were preserved; new reports are separately bound in coordinator metadata.
- Pinned Node24.21.0/npm11.20.0 generated DTO drift, type checking, formatting, production build and dependency audit passed with zero reported vulnerabilities. Playwright1.62.1/Chromium145.0.7632.6 and axe4.13.0 performed engineering browser checks. Desktop axe31 rules passed with zero violations/incomplete;320px33 passed with zero confirmed violations and color-contrast INCOMPLETE. Full supported Windows/NVDA/Narrator and WCAG conformance remain NOT VERIFIED.
- Bicep0.47.16 build/lint of18 existing templates and current infrastructure policies passed, including44 unsafe bootstrap-hosting drifts. Existing inert Azure bootstrap actual-process startup/refusal/shutdown/log probes passed. This does not prove container supply-chain promotion, Azure runtime controls or G1.
- Initial complete tracked/non-ignored checkout scan passed zero leaks; final records/whitespace scan and exact source/artifact/transcript bindings are in the coordinator metadata before closure.

### Review findings and disclosed corrections

R5's malformed-existing-seed replay originally lacked a typed integrity failure; it was fixed with an actual PostgreSQL regression before freeze. U5's conflict message could disappear during automatic refresh; it now stays focused and disables a stale form until explicit refresh, with actual409 browser evidence. Independent V5 review strengthened the historical oracle to fixed baseline-only literals and added real response-loss, delayed selection and visible DOM goldens. Preliminary browser runs corrected a malformed-body status expectation (400), duplicate-title selector scope, and asynchronous history-refresh selection timing; the final complete run passed. No failing or preliminary run is treated as passed.

The coordinator's early host build ran before restore completed, and an initial frontend command used the wrong working directory; corrected prerequisite/order and final checks passed. Sandbox named-pipe restrictions prevented the first formatter run, and the Bicep bundle needed an explicit temporary extraction path. Exact owned-file formatting corrected whitespace; the final whole-solution verification passed. All these limitations/corrections are retained in temporary transcript bindings rather than hidden as success.

### Configuration and remaining scope

Only separate additive `synthetic_review` migration001 is new (SHA256 `f919f6a16de9a579573e52046ca1d756f691dfed5f79c1d6eda5ecc7b370180d`). Existing assessment migration001 remains `67bad3b18091babdda2abb898666932b7dc09df767c4e6a3ed49265590e34f88`. Direct Npgsql10.0.3 and Logging.Abstractions10.0.12 alignment use existing pinned versions; no novel package version was added. Optional `MaturityFixtureDigest` is omitted for earlier envelopes. New private schema/CI references are additive; no production configuration or permissions changed.

Rollback disables new profiles/routes or retains a compatible reader; never delete review history or rewrite previous coverage to run an older binary. [Demo operations](../../docs/development/local-consultant-demo.md) and [private DTO contract](../../contracts/local-demo/README.md) describe the exact local transport and retry limits.

Full Phase1B/1C, Milestone5, desired-outcome approval, production identity/customer isolation, risk acceptance, validated closure, recurrence, AI, recommendations/fix/task/export/report/MCP and G1–G9 remain open. This bounded synthetic cycle cannot accept any of them.

Final independent test review found that an oversized text field could also fail the field-length limit. V5 supplement `8b60ae88a92ae3baba537be0b726ccb5c94347c9` now sends an otherwise-valid bounded seven-field command padded with legal JSON whitespace beyond4096UTF8 bytes, proves400 and unchanged history. Full actual browser rerun passed2,162 assertions/eight groups with owned host restart. Source-digest metadata maps produced a generic API-key scanner false positive beside api.ts; the reproducible binding generator separates path and digest into object fields without scanner allowlists. No product behavior changed.

### Configured remote verification and preserved handoff

Integrated source `97f7a890df03d630f6fd10e2c5f6d0152eeb5231` passed [Linux and Windows2022/2025 bootstrap run36964164461](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36964164461) and [Azure package run36964164350](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36964164350). Linux executed full restore/format/Release/current suites, actual PostgreSQL review/maturity and historical regressions, all three browser suites, frontend audit, scanner and infrastructure policies. Windows protected-key/ACL, cross-publish and service smoke passed. Azure verified actual inert non-root container and private-hosting checks. These configured workflows are partial engineering evidence, not full feature or live gate acceptance. Final checkout scanner passed440 tracked/non-ignored files without allowlists. R5 reread the exact V5 supplement and found no actionable issue. Three completed worker worktrees were removed only after commits, exact source archives and changes were preserved; other pilot work was retained. The loopback consultant demo was reopened against its existing synthetic database and returned all six profiles.

The existing private board preserves the concurrent BFF handoff and its pending owner-publication task. Full milestones, Phase1B/1C, production role/customer authority, report acceptance and G1–G9 remain open.
