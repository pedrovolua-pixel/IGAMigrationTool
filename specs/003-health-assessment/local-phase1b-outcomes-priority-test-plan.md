# Test plan proposal: local Phase 1B desired outcomes and priority/effort

Status: Proposed — NOT EXECUTED; awaiting OP1B-01–06 approval
Owner: Quality owner / independent verifier
Last updated: 2026-10-03
Contract: [exact decision proposal](local-phase1b-outcomes-priority-contract-proposal.md)
Authority: approved FR-HAS-6/7/9/35/36/37/42/47/50 and TP-HAS-002/005/009/013/014/017/018 subsets; no full feature acceptance

## Independent oracles and evidence

Before implementation, an independent verifier authors exact outcome version/event/lock fixtures, factor vectors, arithmetic expectations, ordering and historical envelope digests from the approved decision. Do not generate expected values with production helpers or regenerate old golden files. Freeze canonical bytes independently after the internal implementation contract is reviewed. Bind executed source, compiled closures, fixture/policy versions, native outputs and failures in evidence before cleanup. These cases are proposed coverage; no result is asserted here.

| Test ID | Positive and negative cases | Level | Approved requirement/acceptance mapping |
| --- | --- | --- | --- |
| OP1B-T01 | Exact immutable documented/inferred draft content; successor version preserves predecessor; review/approval never replaces inference labels; invalid/foreign/duplicate applicability references, unknown versions or altered digest fail | Unit + durable integration | FR-HAS-6/7/9/42; TP-HAS-002; AC-HAS-2 |
| OP1B-T02 | Every permitted transition and denied backward/self/direct-draft-approval/advisory-supersession transition; terminal retired/superseded cannot revive; reviewed successor leaves current approved predecessor until atomic approval; approval high-water survives retirement: approvev3→retirev3→approve reviewedv2 denied; later reviewedv4 may approve; retiring approved leaves none | State matrix + real PostgreSQL | FR-HAS-7/47; TP-HAS-005; AC-HAS-5 |
| OP1B-T03 | Consultant create/review/advisory-retire success; distinct server-bound fictional customer approver approval/approved-retire success; Consultant cannot customer-approve; general reviewer/risk-owner/Auditor/executive/support/MCP denied; untrusted role/actor/scope/approval/time denied | Policy + actual host | Authorization matrix; TP-HAS-009/014; AC-HAS-9/14 |
| OP1B-T04 | Exact approved version/content/event locks; explicit empty outcome set; draft/reviewed/current superseded/retired selection fails rather than silently omits; duplicate stable IDs, unavailable or foreign proof fail; no client approval flag | Composition + durable start | FR-HAS-6/7/47; TP-HAS-005/009; AC-HAS-5/9 |
| OP1B-T05 | Use existing pilot-health-v1 outcomes and unit formula; decimal confidence/severity/review boundaries; rejected/pass/gap/proposed/confirmed cases; approved zero-eligible units Not assessed; advisory has no scored row; multi-outcome unit does not duplicate overall/category contribution | Independent arithmetic + integration | FR-HAS-7/16–19; TP-HAS-005/017; AC-HAS-5/17 |
| OP1B-T06 | Retire/supersede/change outcome after start: old locked score/input/result digests unchanged; Approved at run lock versus current registry status shown separately; new explicit run uses new exact approved selection; corrupted locked outcome proof blocks the whole compound projection, no empty-outcome/core-digest fallback; separately verified historical metadata marked unavailable; unavailable current registry leaves verified locked history usable | Durable + host/browser | FR-HAS-7/23/47/50; TP-HAS-005/017; AC-HAS-5/17 |
| OP1B-T07 | Six factor formula independent literals; each factor varied alone has the specified contribution; distinct objects counted once, cap at100; objective max not sum, complete no-match0; every enum/weight/source/count invalid or missing fails with Priority unavailable; no runtime default fallback | Unit + independent composition | FR-HAS-35/36; TP-HAS-002/018; AC-HAS-2/18 bounded subset |
| OP1B-T08 | Raw boundary labels at40/60/80, below/above and rounding crossing; invariant decimal display; deterministic ties by ordinal scoped finding then option ID; unavailable rows last; exact High/Internal/10/Ordinary/S/access-governance vector51.75→51.8/Medium | Independent arithmetic + browser | FR-HAS-36; TP-HAS-002/013; AC-HAS-2/13 bounded subset |
| OP1B-T09 | Proposed fictional estimate visible until exact Consultant approval; XS/S/M/L/XL person-hour fixtures; approve original or approved reasoned replacement only; invalid ranges/sizes/source/revision deny; no inference of customer duration, due date or automatic approval | Unit + store + browser | FR-HAS-35/36; TP-HAS-002/018; AC-HAS-2/18 bounded subset |
| OP1B-T10 | Original factors/raw priority/size/range remain unchanged after approved effort replacement or priority label override; no hidden recalculation; original/effective/rationale visible; withdrawal after never-approved-original→approved replacement restores Proposed original; approved original→replacement→withdraw restores Approved original; no active override withdrawal denied; later source change→withdraw denied and no old approval current; exact replay retains original receipt | Store + host/browser | FR-HAS-36/42/50; TP-HAS-002/009; AC-HAS-2/9 |
| OP1B-T11 | Real approval/start fence race, two replacement approvals, concurrent overrides/revisions; one coherent winner, no double current-approved version or half supersession; injected faults before each commit leave all event/current/receipt/lock stores unchanged | Real PostgreSQL controlled barriers | FR-HAS-7/36/47; technical concurrency; TP-HAS-005/014 |
| OP1B-T12 | Exact accepted UUID replay yields original metadata after later lifecycle/override/source; changed actor/scope/version/payload/UUID reuse conflicts; stale revision/source new command denied without event or UUID reservation; gaps/corrupt projection/receipt fail closed; dropped response retry one event | Real PostgreSQL + host | FR-HAS-7/36/47; technical idempotency; TP-HAS-014 |
| OP1B-T13 | Eleven literal historical profile envelopes/digests, scoring outputs, task/artifact history bytes unchanged; exact coordinator-composed new Phase1B outcome/priority+automatic-AI+CSV inventory accepted only opt-in; unknown/partial/mismatched contracts denied; original source flag behavior preserved; additive migration repeated safely; dedicated-database guard and disabled routes deny | Compatibility + owned DB + host | FR-HAS-23/37/47; ADR0003; TP-HAS-005/014/018 |
| OP1B-T14 | New planning decisions do not change task creation/reconfirmation bindings/status/freshness, artifact review events/original Unverified content, finding dispositions, maturity, scoring or report bytes; no conversion/reconfirmation/execution/CSV permission is implied; task metadata link labeled separately | Cross-module + browser regression | FR-HAS-35/36/37/42; TP-HAS-018; AC-HAS-18 bounded subset |
| OP1B-T15 | Inert reasons/outcome labels and hostile markup; invalid Unicode round-trip, unknown fields, oversized strings/lists, integer/decimal overflow rejected before writes; category-filtered current/history/receipt, unavailable proof; audit/telemetry contains no text/protected values | Security + host | FR-HAS-9/42/50; TP-HAS-009/014/018; AC-HAS-9/14/18 |
| OP1B-T16 | Keyboard create→review→fictional approve→new run→inspect locked adherence; estimate approve→override→withdraw; status/original/effective labels, focus/error announcement/empty/unavailable states; late responses after selected run/version change cannot restore old view | Browser + axe + manual AT | TP-HAS-013; AC-HAS-13 bounded subset |

## Required authority/isolation matrix

For each allowed action in OP1B-T03, execute authenticated fixture success and unauthenticated/wrong actor, wrong customer/project/environment/run/outcome/option, wrong category, inactive/suspended/revoked fixture, stale assignment/revision, deleted resource and direct identifier substitution cases. Enforce denial before lookup/serialization and before persistence in the actual host, not just UI hiding or a pure policy helper. Test approval-only actor denial for Consultant review and Consultant-only effort/priority operations. Show both local fixture personas explicitly as simulations; real customer identity and production approval routes stay absent/disabled.

## Independently computed arithmetic vectors

The verifier should freeze literal expected numbers after approval, including these proposed examples:

| Vector | Expected raw `P` | Display / label |
| --- | --- | --- |
| High1-factor mapping0.8; Internal0.25; count10→0.1; Ordinary0.25; S→0.75; access-governance1 | 51.75 | 51.8 / Medium |
| Critical1; Public1; count100→1; Essential1; XS→1; objective1 | 100 | 100.0 / Immediate |
| Informational0; Isolated0; count0; None0; XL→0; complete objective map/no matches0 | 0 | 0.0 / Low |
| High0.8; Internal0.25; count10; Ordinary0.25; S→0.75; objective max0.75 | 49.25 | 49.3 / Medium |

Changing count from10 to20 adds1.5 points; changing Internal to Public adds15; changing Ordinary to Essential adds11.25; changing S to XS adds2.5. More than100 distinct objects does not add points. Matching objectives1 and0.75 still yields `O=1`. Any missing map/exposure/dependency/size produces no raw priority. Construct additional valid factor vectors for exactly40/60/80 and values whose one-decimal display crosses a boundary while raw label does not. A Consultant label override has no manufactured raw score.

For outcome scoring, independently build at least one pass, confirmed High deterministic finding, proposed High deterministic finding and proposed/confirmed AI finding linked to two approved outcome versions and one advisory outcome. Verify exact eligible sets under provisional/publishable policy, no extra overall unit, empty approved outcome unavailable and advisory exclusion. Freeze original and later run locks/digests before lifecycle operations; do not use the latest registry as the historical oracle.

## Failure, recovery and compatibility execution

Use explicitly owned local synthetic PostgreSQL fixtures and dedicated database guards; do not restart or mutate another agent's database. Exercise crash/fault boundaries with independent barrier controls, real transaction composition and source-owner readers. Record denied-operation no-write checks for all owned stores, including run locks and supersession counterparts. After uncertain response, repeat the exact command, verify the original receipt and refresh current view; changed retry must conflict. Corrupt/missing durable proof remains an explicit unavailable/denied state, never a fixture regeneration path.

Save eleven old-profile golden envelopes/digests before code. Re-run existing analysis/scoring, review/maturity, recommendation/fix/artifact and task suites on combined source. New profile enablement never promotes old runs, widens old DTO inventories or rewrites old migrations. Disable the new flag and demonstrate historic authorized metadata remains intact; no destructive downmigration. New-profile task/export coupling requires the coordinator's separately approved/frozen contract before related tests become implementation authority.

## Evidence required before a bounded developer completion claim

- Attributable OP1B-01–06 approval and independent internal-contract review; exact source/fixture/policy digests and literal old-profile goldens.
- Executed .NET locked restore with package auditing, format, Release build, affected unit/integration/architecture hosts, frontend lock/lint/type/build, applicable security scan and existing regression suites.
- Independent real-PostgreSQL authorization/concurrency/replay/failure results; actual loopback host/browser workflow and automated accessibility output.
- Explicit manual accessibility status, unmet cases, migration/configuration implications, rollback proof and known limits. Do not report unexecuted manual AT, Windows or customer tests as passing.
- Coordinator reconciliation of the canonical Phase 1B exit checklist and confirmed private status-site publication. These tests alone do not close Phase 1B, Phase 1C, UAT or G1–G9.

No new performance SLO is proposed. Run the approved bounded fixture/load checks applicable to the new profile and report actual shape, timing and memory; the synthetic slice cannot substitute for approved 100,000-object/multi-environment acceptance. Production customer approval, real objective weights/effort estimates, retention, deployed identity, source evidence and manual supported-browser/AT checks remain separate evidence dependencies.
