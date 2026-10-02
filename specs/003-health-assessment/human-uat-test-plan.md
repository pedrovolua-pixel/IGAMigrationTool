# Human UAT Test Plan: One Identity Manager Health-Assessment Pilot

Status: Draft — execution starts only when the entry criteria for the selected checkpoint are met
Derived from: [product specification](product-spec.md), [approved technical test plan](test-plan.md), [evaluation plan](evaluation-plan.md), [capability matrix](capability-matrix.md), and [local build plan](../../plans/active/one-identity-local-pilot-build.md)
Owner: Quality owner
Human roles: Pilot UAT lead, consultant, One Identity SME, customer risk owner, database owner, accessibility reviewer, security reviewer
Last updated: 2026-10-02

## Purpose and checkpoints

This is the human-facing execution runbook for the approved pilot use cases. It records observations; it does not approve a source query, grant access, accept risk, change a customer environment, or convert a failed check into a pass.

There are two distinct test checkpoints.

| Checkpoint | What a human may test | Earliest entry | What it cannot establish |
|---|---|---|---|
| Local synthetic UAT | The complete Phase 1A–1D experience with fictional, versioned fixtures | The local-build completion checklist is fully checked and its applicable automated/manual evidence is recorded | Customer-source safety, customer data handling, cloud operation, capability validation, G8 or G9 |
| Supervised operational UAT | Read-only assessment and reassessment in approved pilot environments | Both capability-matrix rows are `pilot-validated`, the exact source/query/field/permission contracts are approved, and G1–G8 prerequisites are evidenced | Production release beyond the approved pilot; G9 passes only after this plan and the evaluation gate pass |

Do not begin a human session against a customer source merely because a screen is available. Until the second checkpoint, use only the supplied synthetic profile. Do not try source writes, DDL, remediation, external task creation, raw-evidence retrieval, custom-rule management, benchmarking, migration, or any other deferred capability.

## Readiness decision

The UAT lead records one result before inviting testers.

| Decision | Required condition | Action |
|---|---|---|
| `READY — local synthetic UAT` | Every local-build completion item is checked, the selected build and fixture digests are recorded, applicable tests pass, and the synthetic environment is available | Run UAT-00 through UAT-09 with fictional fixtures only |
| `READY — supervised operational UAT` | Local UAT exit is met; both environments are `pilot-validated`; source/read-only permission, query-pack, field-policy and customer authorization records are approved; relevant G1–G8 evidence is current | Run UAT-00 through UAT-14 in both environments |
| `NOT READY` | Any required evidence is absent, failed, expired, or is `NOT VERIFIED` | Do not schedule the affected checkpoint; log the blocker and link the canonical record |

At this revision the decision is **`NOT READY`**. The local build plan says the repository has no complete end-to-end pilot application and none of its completion boxes is earned. The capability matrix has no `pilot-validated` environment; both rows are `declared` and their required evidence is `NOT VERIFIED`. No delivery date can be responsibly inferred from those open implementation and human-dependency gates.

## Test preparation

Before every session, the UAT lead must record:

- test date, tester names and roles, tested commit/build and fixture or environment evidence ID;
- selected checkpoint and proof that its entry criteria passed;
- rule catalog, profile, scoring/maturity, report-renderer and (where enabled) AI prompt/model/packet versions;
- for operational UAT only: opaque environment ID, approved query-pack/normalization versions, capability-matrix row, database-owner authorization reference, and the stop contact;
- known limitations, including unsupported/uninstalled/inaccessible categories and any disabled AI, publication, link, PDF or MCP feature.

Use synthetic data that contains no customer information for local UAT. Operational test notes and exports must use opaque IDs and protected evidence references only. Do not paste credentials, raw SQL, protected evidence values, customer names, production screenshots containing protected data, or security-test details into this file or ordinary issue trackers.

## Execution rules

- Record each scenario as `PASS`, `FAIL`, `BLOCKED`, or `NOT VERIFIED`; attach the evidence reference, not sensitive payloads.
- Stop immediately and notify the designated security/operations contact for any suspected cross-customer access, source-write capability, protected-data exposure, integrity mismatch, unsafe rendered output, or deletion/access-control bypass.
- A `BLOCKED` or `NOT VERIFIED` result does not become a pass by waiver. A retest uses a new execution record and tested build.
- Human exploration is valuable, but a scenario passes only when the expected observable outcome below is met.

## Phase 1A — baseline, coverage and quality

| ID | Role | Steps | Expected outcome | Test-plan trace |
|---|---|---|---|---|
| UAT-00 | UAT lead | Confirm the readiness decision, build/fixture lock and visible environment label. | Correct checkpoint label is visible; no live-source claim is implied for synthetic UAT. | TP-HAS-001/015 |
| UAT-01 | Consultant | Start an approved synthetic baseline with pass, finding, not-applicable and each declared gap state. Observe progress and final coverage. | Every planned item has exactly one explicit terminal state; missing, duplicate or unexplained results do not appear complete. | TP-HAS-001/007 |
| UAT-02 | Consultant + SME | Inspect supported modules/categories and an uninstalled, inaccessible and unsupported example. | Installed scope is represented; uninstalled is `not_applicable`; inaccessible/unsupported categories remain explicit gaps rather than disappearing. | TP-HAS-015/016 |
| UAT-03 | Consultant | Compare health summary with assessment-quality details for a partial baseline. | Health remains separate from quality; gap reasons/counts are visible and no gap silently improves or reduces default health. | TP-HAS-007/017 |
| UAT-04 | Consultant | Cancel or interrupt a synthetic run at a safe fixture checkpoint, then resume. | Resume is traceable and idempotent; it neither duplicates results nor falsely reports completion. | TP-HAS-001; recovery tests |

## Phase 1B — findings, review, scoring, AI and guidance

| ID | Role | Steps | Expected outcome | Test-plan trace |
|---|---|---|---|---|
| UAT-05 | Consultant | Open deterministic findings with multiple affected objects and a proposed Critical/High example. | Original/provenance, facts, inference, severity, confidence and guidance are distinguishable; proposed Critical/High results are visibly warned and excluded from publishable score until reviewed. | TP-HAS-002/003/017 |
| UAT-06 | Consultant + reviewer | Confirm, reject and present an inconclusive finding; refresh the view and compare score/maturity. | Attributed immutable history is retained; current presentation is coherent; score behavior follows the shown review state without altering originals. | TP-HAS-005/010/011 |
| UAT-07 | Consultant | Open recommendations/fix package and CSV fixture containing hostile markup/formula-like text. Attempt to activate an unavailable action. | Guidance is inert, clearly unverified until consultant review, CSV is safe, and no execution or external task-creation path is enabled. | TP-HAS-018/014 |
| UAT-08 | Consultant + reviewer | Where an approved test profile enables the fake AI path, inspect proposed AI output, citations and an adversarial/invalid-output fixture. | Output remains proposed; citations resolve only to allowed packet members; invalid or unsafe output is rejected with a payload-free failure. Do not use a real provider unless the operational checkpoint explicitly authorizes it. | TP-HAS-008/009 |

## Phase 1C — publication, delivery, reassessment and accessibility

| ID | Role | Steps | Expected outcome | Test-plan trace |
|---|---|---|---|---|
| UAT-09 | Consultant | Produce the currently supported draft output and compare dashboard and Markdown views. | Version, health/quality and limitations agree across supported surfaces; if publication/PDF/link is unavailable, record `NOT VERIFIED`, not a failure workaround. | TP-HAS-006/017/020 |
| UAT-10 | Consultant + customer risk owner | At the operational checkpoint only, test scoped risk acceptance and report acknowledgment; try the same action as consultant/wrong customer. | Only the scoped risk owner can accept risk with required metadata/expiry; acknowledgment records delivery without changing findings or score; denied roles reveal nothing. | TP-HAS-004/011 |
| UAT-11 | Consultant + SME | At the operational checkpoint only, run a later approved baseline that includes safely available unchanged, resolved/new-or-worsened and changed-evidence/rule/profile cases. | Stable issues correlate with a timeline; ambiguous keys require review; score change explanations are visible; prior published versions remain immutable. | TP-HAS-010/019 |
| UAT-12 | Accessibility reviewer | Complete the selected essential workflow using keyboard, screen reader, 200% text/reflow and graph table/text alternatives; inspect supported PDF only when it exists. | No essential-flow WCAG 2.2 A/AA defect; focus/context are preserved; tables/text convey graph meaning; record tool/browser/AT and defects. | TP-HAS-013; accessibility plan |

## Phase 1D — read-only MCP boundary

| ID | Role | Steps | Expected outcome | Test-plan trace |
|---|---|---|---|---|
| UAT-13 | Authorized MCP identity | At the operational checkpoint only, read an authorized published health summary. | Read result identifies the same canonical version/digest as the supported delivery surfaces and omits raw evidence. | TP-HAS-020 |
| UAT-14 | MCP identity + security reviewer | Attempt raw-evidence, run/start, edit, disposition, risk, publication, export/link/task and all mutation operations. | Each is denied and audited without leaking resource existence or payload. | TP-HAS-009/014/020 |

## Operational evaluation gate

Run this section only after UAT-00 through UAT-14 are eligible in **both** independent `pilot-validated` environments.

1. Freeze the evaluation record: exact environments/builds, baselines, query/normalization/rule/profile/scoring/AI/application versions, AI population, sample seed, reviewers and correction cutoff.
2. Run initial assessment and later reassessment in each environment; reconcile all expected inventory keys and explicit gaps.
3. Review every AI Critical/High finding plus a deterministic module/category/severity-stratified sample totaling at least 100 AI findings, or all when fewer than 100 exist.
4. Record confirmed, rejected, indeterminate and unreviewed outcomes. Calculate `confirmed / (confirmed + rejected)`; it must be strictly greater than 80%. Exactly 80% fails.
5. Record recurrence, score-change explanations, desired-outcome evaluation where approved, limitations and reviewer conflicts in the immutable evaluation report.

## Exit criteria and defect handling

Local synthetic UAT exits when all eligible UAT-00–UAT-09 scenarios pass or have a documented non-essential limitation accepted through the approved defect process, with no unresolved critical security/integrity/accessibility issue. It is evidence for local implementation only; it does not close G8/G9.

Operational UAT exits when all applicable UAT-00–UAT-14 scenarios pass in both environments, the complete approved test plan passes, essential accessibility flows pass, recovery/restore evidence meets its objective, each capability row is `pilot-validated`, and the evaluation gate above passes. Only then may the owners assess G9 operational acceptance.

For every failed or blocked scenario, record the test ID, checkpoint, build/fixture/environment reference, concise expected versus actual outcome, severity, safe reproduction reference, owner, and retest result. Link the issue to the relevant acceptance criterion and the canonical evidence index; retain sensitive material only in the approved restricted evidence location.

## Human result record

| Session | Checkpoint | Test ID | Result | Build / fixture / environment reference | Evidence reference | Defect / retest |
|---|---|---|---|---|---|---|
| [enter] | [local synthetic / operational] | [UAT-NN] | [PASS / FAIL / BLOCKED / NOT VERIFIED] | [enter] | [enter] | [enter or n/a] |

## Required links

- [Local build completion checklist](../../plans/active/one-identity-local-pilot-build.md#completion-accounting)
- [Approved technical test plan](test-plan.md)
- [Accessibility plan](accessibility-plan.md)
- [Capability matrix and environment evidence](capability-matrix.md)
- [Pilot evaluation plan](evaluation-plan.md)
- [One Identity SME and database-owner evidence template](../001-data-ingestion/one-identity-sme-evidence-template.md)
