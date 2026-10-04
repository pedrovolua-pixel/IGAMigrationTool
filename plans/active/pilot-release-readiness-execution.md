# One Identity pilot release-readiness execution

Status: RUNNING — execution plan approved by repository owner on 2026-10-03
Owner: Pilot coordinator
Authority: Owner approved the Milestones 1–12 / G1–G9 execution plan in this session after amending AGENTS.md's pilot approval rule.
Source of truth: [approved implementation plan](../../specs/003-health-assessment/implementation-plan.md), [product](../../specs/003-health-assessment/product-spec.md), [technical](../../specs/003-health-assessment/technical-spec.md), [tests](../../specs/003-health-assessment/test-plan.md), [status](../../specs/003-health-assessment/status.md) and [evidence](../../specs/003-health-assessment/evidence-index.md).

## Direction and authority

Execute the approved scope continuously, using milestones and gates for work control and Phase 1A–1D for product reporting. Priority: platform/identity, eligible real One Identity baseline, real deterministic assessment, Phase 1C evaluation/reporting/operations, Phase 1D integration, then two-environment validation. Source intake and independent local preparation may run alongside the critical path; final A/B validation follows implementation.

The owner's pilot exception permits the coordinator to resolve and document product, architecture and security decisions without further human approval pauses. Update exact specifications, ADRs, implementation and test contracts before dependent code. Preserve accepted architecture unless a documented pilot decision changes it. Missing protected configuration, customer/source authority, spending authority, human qualification or verification evidence cannot be invented. Production release and irreversible operations retain explicit authorization. This direction does not claim any gate has passed.

## Milestone execution and exit evidence

| Milestone | Product grouping | Outcome and gate |
|---|---|---|
| M01 | Shared foundation | Deployed secure East US 2 platform, workload/network/queue/observability and failure evidence; G1. |
| M02 | Shared foundation | BFF, current identity/assignment authority, sessions, keys/audit and per-customer routing/isolation; G3. |
| M03 | Phase 1A | Exact-build approved query/field/permission/impact contract, signed collector and immutable baseline; G2 for each build. |
| M04 | Phase 1A | Real-baseline capability locks, inventory/coverage reconciliation and durable resumable runs. |
| M05 | Phase 1B | Real reviewed One Identity rules, five fixture classes, exact module reconciliation and reproducible scoring/maturity; G4. |
| M06 | Phase 1B | Minimized constrained provider, budget/citation/output/failure controls; G5 before real-provider activation. |
| M07 | Phase 1B | Governed recommendations, inert fix packages, consultant tasks and safe CSV. |
| M08 | Phase 1C | Actual source provenance, reviewer authority/history, risk, recurrence/reassessment and frozen evaluation. |
| M09 | Phase 1C | Native immutable publisher/manifest, dashboard/Markdown/PDF parity, licensed isolated renderer and accessibility; G6 with M10 share controls. |
| M10 | Phase 1C | Expiring/revocable links, acknowledgment, retention/purge, deployment/rollback and deletion-safe same-region restore; G7. |
| M11 | Phase 1D | Actual-publication reader, separate MCP identity/grants/client, current revocation, minimization, durable audit and version parity. |
| M12 | Phase 1A–1D | Independent A/B pilot validation, initial/later assessments, acceptance/security/accessibility/operations review and frozen accuracy strictly greater than80%; G8/G9. |

All milestones are OPEN for full acceptance until their stated evidence exists. Existing bounded local verification remains credited at its exact source and scope. G0 is PASS; G1–G9 remain NOT VERIFIED at entry.

## Initial bounded packets

Entry root: f947d380c1e5c05127d4d4fde3c315671077d886, codex/pilot-foundation, with unrelated existing working changes preserved.

| Packet | Owner / permitted activity | Exit |
|---|---|---|
| RR-I01 | Identity worker: read-only inventory of existing BFF branch/worktrees, contracts, source and executed evidence. No writes to existing owner worktrees. | Identify already implemented D01–D04, traceable safe integration and smallest remaining M01/M02 gap. |
| RR-S01 | Collector worker: read-only M03–M05 inventory and exact protected source-contract/preflight proposal. No customer source reads. | Distinguish implementable input validation from external exact-build/query/field/permission/impact evidence; specify paths, requirement/test IDs. |
| RR-P01 | Assessment worker: read-only M08/M09/M11 publication inventory and exact native owning publisher packet. | Freeze source-capture, native manifest/visibility/store/reader/policy/audit contract and independent negative/concurrency test matrix before code. |
| RR-C01 | Coordinator: approval record, preservation, shared configuration/integration, canonical documents, toolchain checks and private site. | Current traceable execution record, exact combined checks and confirmed private publication or explicit stale-site blocker. |

Writing packets are assigned only after their exact contract/test plan is documented, with separate codex/ worktrees from a known baseline and explicit path ownership. Each receives nonauthor engineering review. Workers do not edit canonical records, shared configuration or the site unless ownership is specifically reassigned.

## Checks and completion

Every change runs applicable formatting/lint/type/unit/contract/integration/security/build and document checks. Every integrated milestone runs affected E2E, authorization/isolation, accessibility, compatibility/migration, provenance, failure/recovery and performance checks. Failed/skipped/unavailable evidence remains visible. Preserve customer data, credentials, raw SQL and protected identifiers outside repository/CI/site.

Keep implementation-plan.md, status.md and evidence-index.md current, then publish the existing owner-private status site with role/document/done-condition human tasks. Confirm deployment before calling the site current. Final readiness requires AC-HAS-1–20, security closure, G1–G9, two independent pilot-validated rows, initial/reassessment evidence, approved sampling/qualified review and strictly greater than80% confirmed accuracy. Prepare the release-readiness decision; production remains separately authorized.

## External work to progress alongside engineering

- SME/database owners: each [environment's authoritative build/module/query/field/minimum-read/impact evidence](../../docs/operations/one-identity-sme-pilot-handoff.md). A's full application/schema build is still unresolved; a clone cannot supply B independence.
- Platform owner: fresh priced capacity/what-if/access/evidence/disposal session; prior disposed-session authority is not renewed by this plan.
- API project owner: verified project processing/ZDR/access/quota under the existing AI data controls.
- Accessibility/pilot owners: Prince license and supported manual UI/PDF review environment.
- Evaluation/operations owners: actual qualified independent reviewers, conflicts/scope and authorized later baselines, support/recovery ownership.

Continue available approved engineering while these inputs remain outstanding. Never convert a fictional fixture, document approval exception or deployment into live evidence acceptance.

## Execution checkpoint — integrated1996866

- RR-I01/RR-S01/RR-P01 inventories completed; existing BFF source and approved UI integrated without activation.
- RR-S02 strictquerypreflight closed with335cases after independent malformedUTF8 correction; G2 staysopen.
- RR-S04 authenticated stage receipts/retention correction independently verified with166hostcases; Windows/hosted checks pending.
- RR-P02 native design reviewed/frozen;RR-P03 independent31originalgoldens/19adversarialmethods/14Unicodecases closed. RR-P04 typedcore/codecs/flow and independentcompiledtests in progress; actual persistedfixture races follow.
- RR-S05 first/final-page,typedrow/version/provenance/permission/executor contract design in progress; no sourceactivation or sentinel assumptions.
- Reviewable draftPR4 targets currentpilotfoundation; shared118-projectbuild,architecture,23Bicep templates andGitleaks passed. Original failures/hoststorage-process limits retained.
- Existing private board confirmed at checkpoint2e129a3; this new source checkpoint is scheduled for same-cycle publication. AllM01–M12fullacceptance/G1–G9remainopen.

## Native source authority contract amendment — RR-P04/RR-P06

Freeze the explicit actor/command/fence source-port binding in the native contract/test plan before the dependent integration and independent flow tests. The 31 original goldens remain immutable; 157 compiled byte/hash/shape/ownership checks passed against the frozen codec source. RR-P06 tests actual publisher/reader calls with scripted trusted ports, including current preload authority, receipt/commit/audit/deadline and late-delivery paths. Persisted PostgreSQL races and actual-source/control/customer integrations remain separate and open.

## Integrated native codec checkpoint — 2026-10-04

RR-P04code integrated;RR-P03compiled157andoriginal31/negative19PASS. Shared120-projectlockedrestore/format/Releasebuild0warnings passed. Independentpublisher/readflow reviewactive;RR-P05persistencedesign preparation;RR-S06scriptedexecutor implementationactive. ExistingWindows2022/2025bootstrap250passed;Linuxpending atinspection. BFFpackage160staleallowlistcorrectedwith2exactpaths;localfullinputsPASS,hostedcorrectionpending. See [nativecoreevidence](../../docs/development/evidence/pilot-native-core-20261004.json). Allfullacceptancegatesremainopen.

## Combined release-readiness checkpoint — 2026-10-04

[Executed combined evidence](../../docs/development/evidence/pilot-integrated-checkpoint-20261004.json) records123-project locked restore, formatting and zero-warning Release build;7architecture policies/4scans;284authored collector SQL-family/scripted cases and55independent cases;157native codec and58scripted flow cases. Both independently reproduced source cancellation/accounting defects and native flow defects are closed. These bounded ports remain unactivated; physical source execution/signed registry/native history and18persisted native cases remain NOT VERIFIED. Native persistence design receives independent review before code, including exact grants, COMMIT-time guarantees, lock order and deadline ownership.

The approved graphical UI is integrated with independently reviewed visible start/open focus and the original protected-link guard intact. Actual owned PostgreSQL browser checks pass385consultant,718final scoring/retry,2180review/maturity,2095draft-report and2923guidance checks, including the exact recorded restart/race/recovery controls. Zero confirmed automated accessibility violations; color-contrast remains incomplete and supported Windows/NVDA/Narrator/PDF/manual acceptance is open. Remaining current-head AI/artifact/task/Phase1B drivers await their unchanged dedicated hosted fixture guards. Current frontend contract/type/build/format passes; source and backend authority remain separate from presentation.

Hosted checkpoint60ebff3 passed complete bootstrap251 (Linux/Windows2022/2025), package161 and HTTPS signature6. These are exact older-source results; the newer combined head requires new hosted runs. Ten secret-scan matches were five independently recomputed fictional root-cause hashes in one UI snapshot. The exact path/rule/five-value correction passes the default scan and retains changed-digest/other-path detection; nonauthor review closed the LOW nested-path allowance finding with an exact repository-relative anchor and six controls plus a full scan. Original failures, correction attempts and host limitations are retained.

Phase1A still needs eligible exact-build A/B source contracts; Phase1B needs real reviewed rules/provider evidence; Phase1C needs actual provenance/qualified evaluation, persisted publication, licensed PDF and operations evidence; Phase1D needs actual publisher/identity/audit/MCP integration. FullM01–M12/G1–G9 remain OPEN/NOT VERIFIED;G0PASS. Fourteen human tasks retain roles, repository instructions and done conditions. The last confirmed private snapshoted1c155 is [recorded](../../docs/development/evidence/pilot-native-status-site-20261004.json); this combined checkpoint receives same-cycle publication before being called current.

Hosted combined9c9dadf results: Windows2022/2025bootstrap252, package162 and HTTPSsignature7 PASS. Linux stopped at one generic-api-key match in the new evidence review-status string, which contained a verified Git commit hash immediately after the secretScan field. Exact tracked Git archive reproduces it. Restructure only the metadata into scanner status/sourceCommit fields, retaining default detection and all original evidence. No runtime/configuration/allowance change; corrected-head Linux/full browser verification remains pending.

The combined owner-private snapshot is confirmed on exact site source893d194, bound to canonical8a6037a: [publication receipt](../../docs/development/evidence/pilot-integrated-status-site-20261004.json). All14task cards are byte-preserved; desktop/mobile local rendering has no horizontal overflow. Chromium151was selected for status-page QA after the previously used141cache disappeared; pilot test receipts remain explicitly bound to141. Newhead253Linux has passed the corrected scan, formatting and build and continues; Windows2022PASS at last inspection, Windows2025inprogress. Package163/HTTPSsignature8PASS. These partial observations do not complete gates or the full Linux/browser run.
