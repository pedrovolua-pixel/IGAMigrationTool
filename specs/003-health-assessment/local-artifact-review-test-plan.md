# Test plan: bounded local consultant artifact review

Status: Executed bounded synthetic subset — exact results/limits in Cycle13 developer evidence
Owner: Quality owner / independent verifier
Last updated: 2026-10-02
Contract: [exact local proposal](local-artifact-review-contract-proposal.md)
Plan: [Cycle13](../../plans/active/local-pilot-artifact-review-cycle-13.md)
Authority: approved TP-HAS-002/003/009/013/014/017/018 subsets; no full test-plan acceptance

## Acceptance mapping and independent oracles

Expected source identities, canonical bytes, state labels and event outcomes must be authored independently from approved contract primitives before consuming author-produced expected data. Do not call implementation helpers to produce expected values or rewrite historical goldens to hide failures. Actual executed source, tool versions, native logs/outputs and immutable bindings are retained separately from this planned matrix.

| ID | Required positive and negative cases | Level | Requirement |
| --- | --- | --- | --- |
| AR13-T01 | Real rebuilt package with all three fictional artifact kinds; empty package; malformed/forged/canonical mismatch; foreign finding/option/artifact/template; full source/run/input/template/guidance/review agreement | Unit + portable composition | FR-HAS-35/37/42; AC-HAS-18 |
| AR13-T02 | Exact server-supplied Consultant granted success; unauthenticated/inactive/revoked/stale assignment, qualified reviewer/auditor/executive/support, wrong customer/project/environment/run/category/action/resource state; actor/roles supplied in command denied | Policy matrix + actual host | FR-HAS-37/50; AC-HAS-14 |
| AR13-T03 | Generated originals always Unverified/byte-identical; separate Reviewed for planning overlay; confirm/reject/defer/comment/presentation change never reviews artifact; changed full package digest gives Needs review and permits new review; current withdrawal gives Unverified, including after later source refresh; every exact sequence table row (including withdrawn A then B, review B then withdraw B, return to earlier prose without resurrecting A); stale/no-current withdrawal and fresh-ID duplicate-current review denied; no cross-run carryover | Unit + saved-source integration | AC-HAS-18; TP-HAS-002/003/017 |
| AR13-T04 | Atomic event/revision/metadata-only receipt; same command/event replay for each action returns original historical receipt after later changes/source refresh, never restores old overlay; denied current visibility/unavailable source blocks replay; stale new commands conflict; changed reuse conflicts; invalid reason/unknown fields/oversize/overflow fail before write; reads never append | Real PostgreSQL | FR-HAS-37/50; technical concurrency |
| AR13-T05 | Two simultaneous commands on one revision produce exactly one commit; concurrent current finding/source update cannot attest obsolete source; expected source/revision conflict leaves all stores unchanged; fresh retry requires explicit new source inspection | PostgreSQL + controlled commit barriers | AC-HAS-18; source integrity |
| AR13-T06 | Owned-host kill/restart, cancelled/failed transaction and DB reconnect preserve history/revision/idempotency; failed commit produces no partial current/history/receipt; strict migration digest/foreign DB refusal | Real owned processes/database | ADR-0003; recovery |
| AR13-T07 | Source-coherent full overlay/history display; pending failure/late response/run switch/review reload clears stale state; Retry and action conflicts preserve focus and entered reason without auto-applying to changed source | Component + actual browser | FR-HAS-40/41/42; AC-HAS-13 |
| AR13-T08 | Every earlier profile retains exact input bytes, lock count, source application version and historical null overlay; old guidance/fix/AI/draft/score/review canonical goldens unchanged | Portable + saved DB + previous browser flows | TP-HAS-002/003/017; compatibility |
| AR13-T09 | Hostile markup/script/network/formula/control/Unicode strings render as inert text; no injected resource requests, dialogs, executors, supplied hyperlinks or artifact edits/downloads; frozen original remains visible | Actual browser + negative route inventory | AC-HAS-18/14; TP-HAS-018/014 |
| AR13-T10 | Keyboard review/reason/withdraw/history, actionable labels and current-vs-original warning; focus restoration; desktop/mobile/320 reflow and contrast; axe results report violations and incomplete separately | Actual browser + visual inspection | AC-HAS-13; TP-HAS-013 subset |
| AR13-T11 | Only the dedicated new profile enables new actions; direct operation attempts denied for all historical profiles and foreign/missing sources; no task/CSV/export/execution/report route enabled; health/maturity/draft unaffected | Host + architecture + regression | AC-HAS-14/18; TP-HAS-014/017 |
| AR13-T12 | Append-only enforcement and scoped history/receipt; prohibited evidence/code/reason/comment/secret payloads absent from audit/logs; scope denials reveal neither history nor resource contents; authoritative trusted timestamp and event attribution | PostgreSQL + negative telemetry corpus | Approved audit/authorization policies |

## Data, environment and migration limits

Use only existing versioned generic fictional artifacts plus bounded hostile fixture strings. No actual One Identity remediation, customer evidence, credentials, source SQL access or provider. Create a new dedicated fictional database only after its ownership guard and additive schema are reviewed. Do not restart shared PostgreSQL, modify roles/HBA or operate another session's hosts. Concurrent UI/BFF/research changes must be byte-preserved and excluded from claims unless explicitly integrated and tested.

Measure actual source/record bounds, deterministic ordering, complete captured source validation and immutable snapshots; do not infer product-scale performance from fixture checks. Manual supported Windows/screen-reader workflow and deployed ADR-0001 sandbox remain separate NOT VERIFIED cases. Default macOS startup is not verified by a run using the disclosed temporary configuration-watch override.

## Applicable repository verification

Run current pinned locked restore/dependency audit, solution formatting/Release build, frontend lint/type/format/build/audit, new and historical unit/portable/PG integrations, module architecture/secret/infrastructure/collector checks and actual-browser regressions. Configured hosted Linux/Windows/container checks bind exact code commits. Existing dependency audit evidence is reusable only when exact dependency inputs and required freshness remain unchanged; do not claim a new audit that was blocked or unrun.

The preparation-only Cycle13 change runs documentary link/decision/traceability/whitespace/source-scope/secret checks and independent proposal review. At the preparation checkpoint runtime tests, migrations, build and hosted implementation checks were NOT RUN. The [owner approval](local-artifact-review-approval.md) now permits implementation and planned execution; actual results will be recorded separately. historical Cycle12 evidence is not relabeled as Cycle13 evidence.

## Evidence and completion

Record each ID PASS/FAIL/NOT VERIFIED with exact executed commands, source digest, native environment and output bindings. Preserve failures/corrections, original author/independent/coordinator outputs and any unverified environment. An engineering reviewer cannot approve AR13 choices, Milestone7, UAT readiness or G1–G9. Publish status/dependency changes to the existing owner-private board in the same work cycle.

## Executed result

All twelve groups have source-bound bounded execution evidence in [the developer record](../../docs/development/evidence/local-pilot-cycle13-developer-checks.json) and [independent native matrix](../../tests/integration/LocalArtifactReview.Tests/execution.json). PASS_BOUNDED_EXECUTED_SUBSET does not accept the full product test plan. Supported manual accessibility, deployed isolation, default Mac startup and full milestones/gates remain NOT VERIFIED. Actual controlled rollback/cancellation, reconnect and owned stop/restart were executed; no hard-kill mid-commit claim. Original failed harness runs and unavailable earlier provenance remain retained/disclosed.
