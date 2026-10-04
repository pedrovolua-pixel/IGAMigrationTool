# RR-P02: native publication design and execution packet

Status: IMPLEMENTING — RR-P02 design reviewed at4391d27; bounded RR-P03/RR-P04 authorized
Date: 2026-10-03
Owner: Pilot coordinator with bounded reporting author and independent verifier
Base: `810a1eb0288d3af41758991c06c4287b7a75fe46`
Worktree: `/private/tmp/iga-release-native-publication`
Branch: `codex/release-native-publication`
Canonical execution: [release-readiness plan](../../plans/active/pilot-release-readiness-execution.md)
Contract: [native v1](../../specs/003-health-assessment/native-publication-v1-contract.md)
Tests: [NPV-T01–18](../../specs/003-health-assessment/native-publication-v1-test-plan.md)

## Scope and source finding

M09 / Phase1C supplies the missing immutable native publication owner before M11 / Phase1D can consume one. ReportDrafts is detached SyntheticDraft/Scoring; AssessmentRuns has no terminal-completed source; SyntheticMcp validates a deliberately closed fictional fixture. Relabeling them is prohibited. The native publisher uses an owning terminal-source capture seam, original canonical commitments, staged immutable blobs and atomic metadata/receipt/audit visibility. Its exact reader uses current authority/lifecycle through the documented emission fence.

Merged M02 code already supplies HumanAction.PublishReport policy and recent privileged verification, subject/assignment revision conventions, restricted same-transaction audit/receipts and authoritative UUID scopes. It does not supply a publication event schema, a terminal assessment capture or a control/customer-plane commit fence. Record these integrations as missing; do not wrap nontransactional authorizer booleans to claim them.

## Ownership and freeze

This design author owns only the contract/test document links above and this plan. Coordinator owns product/technical/canonical plans/status/evidence/site/shared configuration. RR-P02 completes when these three documents receive nonauthor review and the coordinator records the exact approved commit/digests. AGENTS.md's pilot exception permits that coordinator decision without a new human design approval pause; it does not waive external evidence/customer access/spending or independent acceptance.

No product code is written in RR-P02. The following code paths are proposals for subsequent explicit writing packets, not permission to modify shared paths now:

| Packet | Proposed owned paths | Exit evidence |
|---|---|---|
| RR-P03 oracle | `tests/unit/ReportPublication.Tests/`, `specs/003-health-assessment/native-publication-v1-oracle-addendum.md` | Independent native canonical bytes/digests, denial/failure corpus and fictional owning source records committed before production code. |
| RR-P04 core | `src/server/modules/ReportPublication/{Contracts,Canonical,NativeReportPublisher,NativePublishedReportReader}.cs`, module README/project/lock | Strong native types, source/authority/blob/transaction ports, exact source/state/warning/integrity guards; NPV-T01–05/10–12/16. Coordinator owns solution/CI wiring. |
| RR-P05 persisted core | `src/server/modules/ReportPublication/Persistence/`, `src/server/migrations/report-publication/`; bounded explicit test store fixture | Actual PostgreSQL immutable versions/receipts/audit, customer-scoped blob staging adapter and transaction visibility/fence; NPV-T06–09/13–15. Migration paths require coordinator confirmation first. |
| RR-P06 independent verification | `tests/unit/ReportPublication.Tests/`, `tests/integration/ReportPublication.Tests/` | Independently authored persisted races/oracles, authority/raw/write/load/emission spies, actual login restrictions, byte compatibility and evidence. |
| RR-P07 integration | Coordinator shared/canonical/evidence/site paths | Reviewed combined source; required checks; exact intake completion/deferred ledger; confirmed existing private deployment or explicit stale-publication reason. |

Use at most three workers and isolate writing worktrees with explicit paths under the approved parallel workflow. Core and persistence are dependent; do not split shared types among simultaneous authors. Independent verifier may author fixtures/tests while the coordinator freezes contracts. No worker modifies another owner's files. All changes remain local, reversible and disabled in ordinary hosts.

## Sequencing and verification

1. Author/reviewer inspect governing product/technical/implementation/test/status documents, ADR0001/2/3 and authorization/audit/retention rules. Freeze this exact design, resolving reviewer findings before code.
2. Commit independent oracle first. Implement strict typed core without source-host integration; retain explicit fixture authority labels.
3. Implement additive fixture-tested persistence, atomic immutable visibility/receipt/audit and source/read emission fence. Run real isolated PostgreSQL tests with distinct restricted logins. Unknown commit outcomes reconcile original receipts.
4. Independent review checks native schema/integrity, authority/lock order, warning/scoring and immutable history, byte minimization, audit failure/cancellation and unsupported source denial. Correct findings and repeat only affected checks.
5. Coordinator integrates and runs pinned locked restore, formatting, Release build, applicable unit/integration/security/architecture/dependency checks and affected historical draft/run/review/MCP suites. Record executed checks, original failures, hashes, migrations/configuration implications and limitations. No E2E/public protocol/renderer claim without implementation.
6. Coordinator updates canonical M09/Phase1C and M11 intake records, then publishes the existing private site and human tasks. Report site current only after confirmed deployment. Do not close M09/M11/G6/G7 or real source/identity/audit/operational human tasks on fixture evidence.

## Integration dependencies and next steps

Actual runtime source owner must supply terminal run/review/current score/maturity/coverage/capability/input capture and revision ordering. M02 must supply an exact control/customer-plane authority/revocation fence and central audit/lifecycle adapters; existing APIs are references, not the missing guarantees. Reporting must later add audience/field partitions and dashboard/Markdown/render contracts before incomplete grants can serve a partial report. M11 requires separate native resource mapping and MCP grants, followed by concrete protocol/client/token/distributed limits and deployed verification. M10 supplies lifecycle/purge/recovery/link/acknowledgment orchestration. None is implied by the publisher port.

Human next steps remain evidence-based: SME/database owners supply each eligible exact-build baseline; platform owner supplies fresh priced deployment/access authority; API project owner supplies processing/ZDR/access/quota proof; pilot/accessibility owners supply Prince license and supported manual review environment; evaluation/operations owners supply qualified independent reviewers and authorized reassessment/recovery evidence. These are canonical-role tasks; RR-P02 introduces no source/customer request or cloud action.

Rollback removes only unactivated new wiring while preserving canonical versions/receipts/audit/lifecycle and historical bytes. Schema/provider migration must verify customer-scoped manifest/blob digests before switching references. Production release, actual destructive data cleanup and irreversible operations still require explicit human authorization.

## RR-P02 documentary evidence

Before handoff execute whitespace/diff ownership/link existence checks and a scoped prohibited-data inspection. Record actual results in the handoff with the exact commit. Runtime tests have NOT EXECUTED in this design packet. Independent review closed at4391d27d6760e3c457b9c9b61a6fcaf4527ab949. Coordinator integrated the reviewed design atb6c806e and approved bounded RR-P03/RR-P04 implementation on2026-10-03. Exact literal canonical envelopes are completed by the independent oracle addendum before hash implementation.
