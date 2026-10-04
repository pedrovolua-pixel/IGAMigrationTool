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

## Hosted253 visible-target correction packet

Both Windows jobs passed; Linux passed through draft-report and recommendation browser checks, then the AI preview driver failed after125checks at24px-enabled-targets. Its predicate counts hidden closed navigation/panel controls as zero-height targets. Preserve that exact failed receipt and change only the owned `tests/e2e/local-ai-preview/verify.mjs` measurement to visible enabled controls using actual client rectangles/computed visibility, with both width and height at least24px, matching the already reviewed consultant check. Retain every semantic/authority/source/transport assertion and dedicated database guard; archived execution source stays historical. Execute affected owned PostgreSQL/browser replay and independent diff review before the corrected hosted run. This is engineering target-size evidence, not manual accessibility acceptance; remaining hosted steps stay NOT VERIFIED.


Independent target review reproduced a LOW edge: Chromium retains client rectangles and visible CSS for a tiny child inside closed native details, although rendering visibility is false. Before dependent test code, refine the visible-target definition to require the browser's actual `checkVisibility()` as well as client rectangles/computed visibility. Include an undersized hidden-details negative control and an opened-details positive refusal; retain the same24px width/height requirement and original full guarded driver expectations. Original failed and intermediate control receipts remain retained.

## RR-P05 persisted implementation packets — 2026-10-04

The coordinator accepts immutable designfdefd8e79fa108bb099e5922e9a49b87a6a1a50e (SHA256a2e81c399cfbb04aecf84c4f6e4e1a09df38bdca7fc0a27abc02e64bc6896d50), integrated5f3e9b6, after nonauthor primary review closes every stated finding. Supplemental read-only reviewad95947 found no additional design defect. [Exact fixture contract](../../docs/development/native-publication-postgresql-fixture-contract.md) and governing615e608 freeze actor-first locks, original operation-byte/warning equality, server-owned audit reservations, same-xid write/finalization and committed readonly proof, UTC/monotonic expiry, separate readonly reconciliation and historical replay branches. Original31vectors/core8881c1 remain unchanged. This authorizes bounded local engineering, not gate acceptance.

| Packet | Owner / exact paths | Exit and dependency |
|---|---|---|
| RR-P05B independent fixture | Identity verifier in `/private/tmp/iga-rr-native-pg-verifier`, branch`codex/release-native-pg-verifier`; only`tests/integration/ReportPublication.PostgreSql.Tests/**` | Freeze18case expectations and exact signature/grant matrix before dependent adapter code; own test source/authority/reference adapters and deterministic clocks; execute actual restricted-login PostgreSQL races/negative cases and retain original failures. Assembly`ReportPublication.Tests`; pinnedNpgsql10.0.3. No module/migration/shared/canonical edits. |
| RR-P05A persisted adapter | Publication author in `/private/tmp/iga-rr-native-pg-adapter`, branch`codex/release-native-pg-adapter`; only`src/server/modules/ReportPublication.PostgreSql/**` and`migrations/report-publication/001-native-fixture.sql` | After P05B frozen case commit, implement exact35signatures/owners/types and reviewed session/store/blob/audit/internal-reader/reconciler APIs. New isolated module references existing core and exactNpgsql10.0.3. No new core/public envelope/ordinary-host/source/provider/MCP/cloud/renderer activation. Report shared friendship/config needs to coordinator. |
| RR-P05C integration | Coordinator shared solution/CI/core test-only friendship and canonical/evidence/private site | Integrate only independently reviewed owned commits, locked restore/format/build/architecture/default secret checks, original157+58 and required unchanged draft/run/review/MCP regressions, then confirmed owner-private publication. Physical18remain planned until executed. |

Both writing worktrees start at the same committed packet checkpoint. Local fixture uses healthy coordinator-owned PostgreSQL18.4/127.0.0.1:56284; hosted image is exact existingpostgres18.4 digest in bootstrap workflow. Provision only fresh owner-receipted`iga_synthetic_native_publication_*` databases and globally unique suffixed fixture roles, six actual execution/setup logins plus two NONLOGIN owners, with no permissions to another database/owner/worker fixture. Verifier validates loopback database identity, absence and owner receipt before setup, and reports setup/cleanup; no arbitrary existing database reset/drop. Credentials stay ephemeral and never enter files/logs/site. Normal nonpooled backend close/discard or narrowly owner-bound PID/backend_start cleanup only; no broadpg_signal_backend/other-fixture termination. Preserve old56283stuck cluster and other55433owner.

Before dependent implementation, independent verifier commits `fixtures/persisted-cases.json` and exact SQL-surface/grant expectations, preserving all31original goldens. Author may prepare project/docs only until this freeze. All18NPV-I01–18 require actual observations (including pg_locks wait/progress and canonical row/hash linkage), not scripted-port substitutes. Where a case is not executable it remains NOT VERIFIED. These are explicit fixture roles/facts; eligible One Identity A/B source, real identity/central audit/lifecycle, actual Phase1D deployment, licensed PDF/manual evaluation/operations acceptance and fullM01–M12/G1–G9 remain open.


AI target correction8dea8be independently closes LOWUI-TARGET-001: real rendering visibility excludes the undersized closed-details descendant, while reopening it still refuses below24px. Five exact-source root and five independent Chromium151 controls PASS; selector/threshold/semantic checks/DBguards unchanged. Original125check hosted failure and intermediate4PASS/1FAIL review are retained. Full guarded hosted replay remains pending; no gate/manual accessibility credit. RR-P05 author/verifier worktrees share packet checkpointa3e7c5d; fresh2fictional databases/16role absence and owner receipt are recorded before controlled setup. Database setup and18persisted cases have not executed.


The owner-private packet snapshot is confirmed at exactsitecf5b2e49/canonicald02cb0d: [deployment receipt](../../docs/development/evidence/pilot-native-postgresql-packet-site-20261004.json). Actual desktop/mobile layout checks pass, all14taskcards remain byte-identical, and native design/packet progress retains18persistedcases/fullgate limits. This publication introduces no source/identity/customer access or task closure.


## RR-P05 subject/version and trusted error refinement

P05B independent18case/35signature/15composite expectations froze at6232bbf before dependent adapter code. Preparation50207b7 adds only isolated module project/README/pinned dependency, with empty-assembly build; no runtime credit. Author preparation identified old exactactor4+S current rows could survive a security-version change. Current identity policy invalidates ALL subject sessions: select the [finite customer-local subject projection and subject-before-session lock](../../docs/development/native-publication-postgresql-fixture-contract.md#coordinator-subjectversion-and-error-marker-refinement--2026-10-04), preserving35signatures/bytes/scoped flags and immutable history. Verifier appends two-scope/two-session/lower-version/wait controls before affected authority code. Nonauthor review precedes that dependent implementation. Real cross-plane invalidation remains open.

Coordinator also selects public accessibility of the existing sealed parameterless fixed-message PublicationIntegrityException rather than broad adapter SourceCapture friendship. Record native contract/test expectations first, obtain engineering review, then make only that minimal core accessibility edit and rerun existing codec/flows. Public source constructor and current test friendship remain unchanged; unknown errors stay dependency failures. No new request route, grant, activation or golden-byte change.


The selected subject/error refinement is independently accepted after literal3-field subject/4-field session correctioncab65d1; verifier expectations70a1dc froze before affectedcode. Root157codec+58flow replay passes after the minimal existing-marker accessibility edit; physical18remain unexecuted. Pre-setup [actualMigration-login clarification](../../docs/development/native-publication-postgresql-fixture-contract.md#finite-actual-migration-login-setup-clarification) selects newDBowner Migration DDL plus exact provisioner post-DDL transfers/ACLs/seed, no role memberships and no runtime until verified ownership/grants. Record both execution identities; no existingDB/role/HBA mutation or production authority.

## Hosted254 fix-package failure isolation — 2026-10-04

Before dependent P05 session/source code, the coordinator records the [finite owning-fence registry and protected-byte validation boundary](../../docs/development/native-publication-postgresql-fixture-contract.md#finite-owning-fence-and-protected-byte-validation-clarification--2026-10-04). Verifier freezes same-session/transaction/reference-identity/command/lease negatives before implementation. This clarifies existing internal ownership checks without expanding35SQL functions, SourceMetadataV1, public core APIs or SQL's prohibited protected-prose parsing. Callback, cleanup, pre-cancel admission and before-copy size failures from the first portable adapter checkpoint remain retained pending exact correction review/replay; no physical18-case credit.

Exact checkpointd02cb0d bootstrap254 passes Windows2022/2025, package164 and HTTPSsignature9. Linux passes the corrected offline AI preview and subsequent inert preview checks, then local fix-package step59 fails after2672assertions with TimeoutError. Its last completed assertion is hostile-script-never-executes; this label alone does not locate the timed-out browser operation. Retain original failed logs. No full Linux success or accessibility acceptance is claimed.

Coordinator will run an explicitly diagnostic copy of the unchanged assertion driver against a new absence-checked owned database `iga_synthetic_rr_v12_20261004` on the healthy coordinator cluster127.0.0.1:56284, preserving original source/hash and documenting only the two fixed database/port literal substitutions. This diagnostic copy is outside tracked source and does not replace the original guarded hosted test. Do not use or modify other owner's55433 service, broaden repository database guards, remove security/semantic assertions, or count this diagnostic as the original hosted pass. Capture safe operation-stage identifiers and viewport/fixture before waits and screenshots; isolate the concrete failing operation before selecting a reviewed correction. The source driver and CI continue to require the original dedicated fictional database/port.

The original diagnostic stops at a30s worker poll during concurrent local checks; a separate fresh a2 fixture uses a diagnostic60s poll and retains that limit explicitly. It reaches desktop screenshots, then reproduces locator.waitFor Timeout waiting for the Assessments button inside a closed mobile Workspace sections navigation. The navigation helper's role locators exclude hidden elements before its visibility check can open Menu. Before the minimal shared helper correction, freeze closed-menu/visible-menu/desktop/focus-settling controls. Locate attached hidden navigation/button with explicit includeHidden, retain visibility-based Menu opening, real target click and next-frame workspace-title focus wait. Add only safe stage labels for subsequent viewport/fixture selection and screenshot waits. Do not change application behavior, screenshot coverage, assertion thresholds, dedicated database guards or historical receipts. Independent browser replay plus new exact hosted run remains required; the diagnostic is not hosted/manual acceptance.

The first includeHidden button correctionfd60168 passes four simplified DOM controls but fails the actual owned a3 diagnostic at initial desktop navigation: the real button has an aria-hidden decorative glyph, and including hidden descendants changes its computed accessible name. Retain this intermediate failed correction. Strengthen the controls with the exact glyph structure before editing again. Select and wait for the attached navigation with includeHidden only; inspect navigation visibility and open Menu before resolving the normal visible exact-name Assessments button. Keep the button's ordinary accessible-name calculation and RAF focus wait. No application/guard/assertion change; original and intermediate failures remain distinct evidence.


## Preparation and hosted failure checkpoint — 2026-10-04

125-project locked restore, corrected whole-solution format, zero-warning Release build and7architecture policies/4scans PASS. Standalone preparation confirms18planned/35signatures/15returnshapes/31originalhashes without SQL execution. Core fixed-marker157+58 and independent external17 checks PASS. Hosted254 Windows2022/2025, package164, HTTPSsignature9 and real guarded AI651 PASS; Linux stops at localfix2672 Timeout. Owned diagnostic identifies closed mobile navigation; original and intermediate correction failures retained, exact2f1a534 glyph controls4PASS, independent/full hosted correction pending. Native adapter portable failures remain under correction/review; no physical18/gate completion.


Navigation correction2f1a534 is independently accepted with four exact-source real-glyph Chromium151 controls. Owned diagnostic passes3487 plus actual host restart; the explicit56284/database/60s configuration remains diagnostic only. Original hosted254 and intermediate failures are retained; corrected full hosted validation remains pending. NoG1–G9/manual/real-environment credit.

## Foundation ancestry reconciliation — 2026-10-04

DraftPR4 now targets foundatione83e234 instead of earlier4c95b734 and reports conflicts; no new exact4698c45 hosted runs are scheduled. Merge the already reviewed graphical foundation ancestry into the isolated integration branch. Preserve its approved UI files/evidence and the integrated exact source-authority contracts, canonical execution/evidence history, BFF behavior and reviewed assessment selection/navigation corrections. Inspect each conflict and retain both evidence histories rather than reverting integrated functionality or changing another checkout. Rerun checks affected by the resulting source, confirm PR mergeability and new hosted scheduling. This is a reversible engineering branch merge, not PR merge, production release or gate acceptance.

Fetch resolves the reviewed foundation to exact0e44eb0152fd043896cf351956f88fb6808a8fa0, including UI-W5dff88945 and its prior independent402+27 rendering evidence. Preserve that owner-approved fidelity correction and all five parallel documentary histories. The App merge keeps visible Assessments start/open and protected routing, while adopting presentation-only props/styles and the corrected scroll behavior. Re-execute current frontend and affected browser/rendering checks; prior source receipts do not establish this combined source.

RR-P05 portable correction415bc14 passes seven independent reader/lease regressions, but the verifier's pre-frozen broader corpus records256PASS/1FAIL: an already pending ignored dependency waits while original-caller LIFO dispatch is stalled. Before dependent code, select the [existing-runtime cancellation-state waiter](../../docs/development/native-publication-postgresql-fixture-contract.md#cancellation-state-observation-independent-of-callback-dispatch), with owned unregister/no caller-handle disposal and unchanged original validity. Original failures remain preserved; integrate only after exact corrected independent replay. PostgreSQL backend_start under NONLOGIN definer owners also needs an independently reviewed finite privilege mechanism before guards; no broad stats/runtime membership is silently added.

Before affected SQL guards or setup grants, select the [seven reverse own-customer owner memberships](../../docs/development/native-publication-postgresql-fixture-contract.md#finite-own-customer-backend-statistics-membership-refinement--2026-10-04) for PostgreSQL server backend_start. This replaces broad statistics privilege with owner→own-execution INHERITtrue/SETfalse/ADMINfalse, zero runtime/Migration owner edges, explicit30/33effective routine matrices, no runtime-owned objects/TEMP and actual SESSION_USER guards. Exact-document nonauthor review and independent negative-case freeze precede implementation. No roles, databases, memberships or SQL guards are changed by this decision record.

Independent exact-owner refinement review identifies LOW P05-ROLE-DOC-001: Migration as fresh database owner implicitly has pg_database_owner, so do not require pg_has_role for that implicit role to be false. Correct the literal no-membership statement to zero explicit/transitive LOGIN pg_auth_members edges, preserving the already selected privileged M database ownership. No new role grant or content authority; verifier freezes this distinction before affected code.


## Approved AI tab retry correction — 2026-10-04

Hosted256 exact589 Linux passes backend and earlier browser suites then stops at AI106; both Windows jobs, package166 and HTTPSsignature10 pass. Owned diagnostic of unchanged driver preserves92checks/TimeoutError with hidden proposal region; real-tab correction reaches537checks before the original keyboard-retry focus oracle fails. Nonauthor six-control review confirms LOWUI-AI-HELPER-FOCUS-0015PASS/1FAIL. Before UI correction, select [the existing retry focus invariant](../../specs/003-health-assessment/local-ai-preview-integration-contract.md#approved-tab-integration-and-retry-focus-correction--2026-10-04): no redundant driver click, and remounted admitted offline proposal panel opens for keyboard retry before its heading receives focus. Original data/threshold/negative/DB guards remain unchanged. Corrected full hosted replay remains NOT VERIFIED; no gate or human task closes.
