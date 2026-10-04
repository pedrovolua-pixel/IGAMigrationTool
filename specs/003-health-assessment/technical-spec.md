# Technical Specification: One Identity Manager health-assessment pilot

Status: Approved  
Product spec: `specs/003-health-assessment/product-spec.md` (Approved 2026-09-28)  
Author: Codex, for technical-owner review  
Reviewers: Technical owner, security owner, One Identity subject-matter expert, product owner, accessibility reviewer, operations owner  
Last updated: 2026-10-01

## Overview

The pilot is a consultant-operated, read-only assessment pipeline for One Identity Manager 10.x on SQL Server. It consumes an immutable normalized evidence baseline from feature 001, locks all assessment inputs, executes versioned deterministic rules and constrained automatic AI analysis, creates traceable coverage results and findings, supports governed human review, computes reproducible health and quality results, and publishes immutable dashboard, PDF, Markdown, expiring-link, and later read-only MCP representations of the same canonical assessment.

Delivery is divided into four deployable sub-phases:

1. **1A — Evidence foundation:** accept an eligible immutable baseline, verify build/module/capability metadata, materialize the assessment inventory, and produce an explicit coverage state for every supported in-scope object and evidence category.
2. **1B — Analysis:** execute approved deterministic rules and automatic AI, group finding occurrences, calculate provisional and publishable scores, classify maturity, manage desired outcomes, create recommendations/fix packages, and create in-product consultant tasks with CSV export.
3. **1C — Review and publication:** support dispositions and risk acceptance, reassessment and comparison, governed immutable publication, dashboard, accessible alternatives, PDF/Markdown, expiring links, and pilot-quality evaluation.
4. **1D — MCP:** expose read-only, authorization-filtered status, coverage, scores, findings, recommendations, and protected evidence references. No raw evidence or consequential operations are exposed.

This specification's approved behavior does not depend on a particular programming language or infrastructure product. Microsoft Entra ID and the OpenAI API are approved external providers. Accepted ADR-0004 selects the pilot application and infrastructure platform while preserving the contracts and boundaries below. Prince 17 is selected for PDF/UA output, but PDF remains disabled until the commercial license and accessibility/security/determinism spike pass.

## Relevant architecture and decisions

- Architecture documents: `architecture/README.md` and accepted ADR-0001 through ADR-0004.
- Accepted ADR-0001: modular application with separately scalable asynchronous workers.
- Accepted ADR-0002: shared control plane and isolated customer data planes.
- Accepted ADR-0003: versioned relational metadata and customer-scoped immutable blobs.
- Accepted ADR-0004: Azure managed technology platform with ASP.NET Core, React, Container Apps, non-HA PostgreSQL Flexible Server, Service Bus Standard, Blob Storage, Key Vault, and managed identities.

Exact versions, the single pilot Azure region, resource identifiers, credentials, capacity, and environment parameters belong in the implementation and deployment plans and must conform to the accepted decisions. Regional disaster recovery is outside the pilot service architecture and is governed by the manual business disaster plan.

## Scope and requirement traceability

### Pilot capability matrix

| Capability | Pilot support | Eligibility or limitation |
|---|---|---|
| Source | One Identity Manager 10.x | Exact major/minor/build/hotfix is recorded; each validated build requires capability evidence |
| Database | Microsoft SQL Server | Dedicated read-only account; approved query pack; database topology is neither collected nor classified |
| Acquisition | Immutable normalized baseline from feature 001 | A One Identity-specific customer-side collector performs pilot SQL acquisition through outbound-only HTTPS or encrypted offline package; direct health-assessment queries are prohibited; ingestion owns all source access |
| Foundation | Identity Management Base; Target System Base and synchronization; configuration/schema; authorization; processes/scripts/templates/custom code; Job Queue; DBQueue; compilation; consistency; audit; synchronization/error health | Installed and accessible capabilities are assessed; otherwise explicit gap state |
| Modules | Business Roles, System Roles, IT Shop, Attestation, Compliance Rules/SoD, Company Policies, Risk Assessment, Application Governance, reporting/data archiving, password management, AD, Entra ID/Azure AD, Exchange/Exchange Online | A module/build pair becomes `validated` only after approved query, mapping, and rule fixtures pass |
| Customer connectors/modules | Inventory plus approved generic security, code, configuration, dependency, and operational checks | No customer-specific semantic rules in the pilot |
| Operational history | Default 90-day lookback, bounded by availability and configurable | Scope and freshness are visible; absent history becomes an explicit quality state |
| General AI | Automatic analysis of normalized, redacted evidence and protected references | No protected raw-evidence retrieval; no tool execution from evidence content |
| Tasks | In-product consultant tasks and CSV export | No Jira, GitHub, ServiceNow, or MCP task creation |
| Remediation | Review-only recommendations, SQL/scripts/examples, validation and recovery guidance | Never executed; unverified until consultant review |
| Reports | Dashboard, PDF, structured Markdown, revocable passcode link expiring within 24 hours | Links contain redacted published reports and no raw evidence; authenticated download is deferred |
| MCP | Phase 1D read-only results and protected references | No raw evidence, analysis start, disposition, risk acceptance, publication, or task creation |

The capability-matrix registry uses these lifecycle states: `declared`, `fixture-verified`, `environment-verified`, `pilot-validated`, `suspended`, and `unsupported`. Only `pilot-validated` build/module/acquisition combinations may satisfy pilot acceptance. A combination requires a recorded exact build, module versions, query-pack version, normalized-schema version, rule-catalog version, fixture evidence, environment test evidence, SME approval, and known limitations. The two required independent pilot environments must be separate rows; their exact values cannot be filled in until customer evidence exists.

### Functional traceability

| Requirement | Design coverage |
|---|---|
| FR-HAS-1 | Capability registry limits pilot eligibility to validated One Identity Manager 10.x/SQL Server rows while retaining extensible source keys |
| FR-HAS-2 | Inventory projector creates exactly one terminal `CoverageItem` per supported in-scope object and evidence category |
| FR-HAS-3 | Rule metadata requires one or more approved assessment-domain tags and coverage reconciliation for every mandatory domain |
| FR-HAS-4 | SailPoint remains outside pilot eligibility; the source adapter and catalog model can add its approved depth later without changing historical runs |
| FR-HAS-5 | One Identity capability rows and rule applicability cover the approved foundation/modules and explicit unsupported states |
| FR-HAS-6 | Rule inputs reference locked desired outcomes, vendor baselines, guidance, customer policies, and operational evidence |
| FR-HAS-7 | Versioned desired outcomes implement draft, consultant-reviewed, customer-approved, superseded, and retired states; only approved outcomes score |
| FR-HAS-8 | Typed rule results and `CoverageItem` states represent passes, findings, gaps, healthy controls, and informational improvement opportunities |
| FR-HAS-9 | Finding schema preserves every required content field and typed fact/inference/assumption/evidence references |
| FR-HAS-10 | Severity and confidence are separate typed fields; named confidence bands derive from stored percentages without changing severity |
| FR-HAS-11 | Detection method and lifecycle policy distinguish deterministic from AI; Critical/High and AI review gates are enforced in scoring/state |
| FR-HAS-12 | Append-only disposition state machine and recurrence correlation implement the complete finding lifecycle |
| FR-HAS-13 | Review permissions, rejection reason, generated-original preservation, warned publication, and evaluation outcomes implement disposition governance |
| FR-HAS-14 | Customer-only `RiskAcceptance`, expiring scoped exceptions, and coverage-exception records enforce risk authority |
| FR-HAS-15 | Finding revisions, owners, comments, mentions, references, validation evidence, and resolved-thread events support pilot collaboration without attachments |
| FR-HAS-16 | Root-cause correlation groups presentation while per-object occurrences preserve linear score impact and many-to-many outcomes |
| FR-HAS-17 | `pilot-health-v1` and independent `pilot-maturity-v1` produce overall, category, object, module, and outcome views |
| FR-HAS-18 | Immutable profile versions hold category weights, severity policy, confidence handling, and explicit gap policy |
| FR-HAS-19 | Score status derives from the approved red/yellow/green thresholds |
| FR-HAS-20 | Lifecycle-to-score mapping separates provisional AI from publishable results and requires new evidence/approved validation for remediation benefit |
| FR-HAS-21 | Versioned algorithms and frozen publication snapshots provide explanation and immutability |
| FR-HAS-22 | Immutable `RuleVersion` records contain source/build/module/evidence applicability, logic, behavior, severity/weight, sources, advice, validation, and limitations |
| FR-HAS-23 | Run locks retain exact rule versions; replay creates a new comparison run rather than rewriting history |
| FR-HAS-24 | Audited catalog/profile governance records disablement, override reason/authority, conflicts, and visible coverage exceptions |
| FR-HAS-25 | Normal catalog promotion requires five fixture classes, One Identity SME review, security-owner review where applicable, and product-owner quality approval; an explicit repository-owner pilot exception may activate an unverified artifact without counting it as validated or satisfying acceptance |
| FR-HAS-26 | No custom-rule authoring or activation operation is deployed in the pilot |
| FR-HAS-27 | Vendor-baseline and rule outputs type supported out-of-the-box replacements and unused/orphaned/duplicated/contradictory/obsolete/unreachable/customized conditions |
| FR-HAS-28 | Relationship, code, dependency, authorization, queue, synchronization, retry, backlog, and error analyzers are rule families over normalized evidence |
| FR-HAS-29 | Assessment has no credential or connector path to downstream applications; only source-baseline metadata is readable |
| FR-HAS-30 | AI gateway receives bounded normalized/redacted evidence packets for the approved general-analysis tasks and cannot retrieve raw evidence |
| FR-HAS-31 | AI schema requires citations and typed fact/inference/assumption/missing-context/suggestion fields; conflicts produce uncertainty |
| FR-HAS-32 | AI trust boundary, minimization, residency/training policy, and instruction/tool isolation govern all calls |
| FR-HAS-33 | Versioned AI policy and budget ledger support disablement, exhaustion gaps, and audited consultant overrides |
| FR-HAS-34 | Deep-analysis operations and raw retrieval are absent and capability-denied in Phase 1 |
| FR-HAS-35 | Recommendation schema covers options, outcomes, scope, prerequisites, changes, roles, risk, validation, recovery, effort, and references |
| FR-HAS-36 | Versioned prioritization inputs and consultant override events preserve automated and human priority/effort rationale |
| FR-HAS-37 | Grouped fix packages and in-product tasks support CSV and inert review-only artifacts; no executor or external-task connector exists |
| FR-HAS-38 | No projected/realized ROI model, field, calculation, or report section is present in the pilot |
| FR-HAS-39 | No realized-benefit tracker is present in the pilot |
| FR-HAS-40 | Canonical report projection drives progressively disclosed interactive executive/practitioner views |
| FR-HAS-41 | Snapshot-bound filters, saved views, stable cursors, lazy loading, and role defaults preserve canonical data |
| FR-HAS-42 | Relationship projections link findings, objects, rules, outcomes, tasks, and fixes; evidence resolution applies field authorization |
| FR-HAS-43 | `pilot-quality-v1` drives a separate quality report and compact main-report limitation warning |
| FR-HAS-44 | Audience projections render every required report section from one frozen canonical record |
| FR-HAS-45 | Atomic immutable publication, warned override record, and append-only acknowledgment implement governance |
| FR-HAS-46 | One report manifest identifies baseline/catalog/profile/state across dashboard, PDF, Markdown, link, and MCP; links enforce pilot controls |
| FR-HAS-47 | Baseline-triggered orchestration locks inputs, checkpoints work, preserves successes, and applies the configurable 90-day operational window |
| FR-HAS-48 | Comparison and correlation services classify changes by evidence/configuration/rules/profile/outcomes/dispositions and notify Critical/High worsening |
| FR-HAS-49 | In-product notification events cover completion, failure, reviews, worsening, and new catalogs; no external channels are deployed |
| FR-HAS-50 | Assessment lifecycle uses customer policy and protected references retain explicit evidence-unavailable state after expiry |
| FR-HAS-51 | Locked stratified evaluation cohort and denominator logic implement the complete sampling and >80% confirmation gate |
| FR-HAS-52 | Evaluation records preserve all review/correction outcomes and originating rule/model/prompt versions; release comparison detects material regression |
| FR-HAS-53 | Desired-outcome adherence evaluation is a distinct result set and denominator |
| FR-HAS-54 | Benchmarking is not deployed; feature capability denies the operation |
| FR-HAS-55 | Phase 1D read-only MCP resource projection applies granular authorization, separation, concurrency, revocation, redaction, and audit |
| FR-HAS-56 | No customer-system mutation credential, operation, worker identity, or route exists in the pilot |

All pilot AC-HAS-1 through AC-HAS-20 map to named verification scenarios in **Test implications**. This pilot technical specification intentionally excludes post-pilot FR-HAS-57 and AC-HAS-21; the vendor health-portfolio dashboard requires its own approved technical design, security review, authorization model and test-plan extension before implementation.

## Design

### Logical flow

1. An authorized consultant selects an eligible immutable evidence baseline and a versioned assessment profile.
2. The orchestration transaction creates an `AssessmentRun`, stores an idempotency key, and locks exact references to baseline manifest, capability entry, rule catalog, profile, desired outcomes, scoring algorithm, AI policy, prompt, model, and application versions.
3. The inventory projector creates one `CoverageItem` for every supported installed in-scope object and every required evidence category. Exclusions and gaps are records, not absence.
4. The planner evaluates rule applicability from the locked build/module/capability data and creates deterministic work units. Work units read authorized normalized evidence through a narrow evidence-reader interface.
5. Deterministic rules emit typed results. Eligible Critical/High findings remain proposed; other deterministic findings may auto-confirm according to the locked catalog policy.
6. The AI gateway receives only a bounded, normalized, redacted evidence packet, authoritative references, and a data-only envelope. It returns a schema-validated proposal with citations and fact/inference/assumption labels. Missing citations, invalid references, policy violations, or exhausted budgets become explicit gaps or rejected analysis results.
7. The correlation stage groups repeated occurrences by stable rule, root-cause key, and affected relationship while retaining per-object occurrences for linear scoring.
8. Scoring produces a provisional snapshot and a publishable-current snapshot. Maturity is calculated separately from approved capability indicators.
9. Authorized reviewers append edits, comments, dispositions, validation evidence, and risk decisions. Every consequential change recalculates the mutable current snapshots without changing the run inputs or any published version.
10. Publication validates warnings, freezes scores and report data in one manifest, and renders all outputs from that manifest. Acknowledgment is a later event and does not alter findings.
11. A reassessment correlates occurrences using stable source-native object keys, rule family, and root-cause signature, then records new, resolved, persistent, reopened, or uncorrelated state with an explanation.

### Components affected

| Area | Change |
|---|---|
| User interface | Consultant run configuration; progress; coverage and quality; review; findings; outcomes; scoring/maturity; graphs plus table equivalents; recommendations; fix packages; tasks; comparisons; publication; acknowledgment; evaluation |
| Application/service | Authorization; orchestration; inventory/coverage; rule registry and execution; AI gateway; correlation; scoring; maturity; review; reporting; sharing; notifications; evaluation; Phase 1D MCP projection |
| Data | Versioned profiles/catalogs/outcomes; immutable run inputs and evidence references; coverage, results, findings, events, score snapshots, reports, tasks, audit and retention metadata |
| Infrastructure | Durable work queue, sandboxed worker pools, transactional metadata storage, encrypted immutable blob storage, credential/key services, observability, backup/restore, PDF/Markdown rendering |
| External services | Enterprise identity provider, approved AI provider, passcode delivery mechanism if separately approved; no downstream IGA connections |

### Module boundaries

- **Identity and policy:** authenticates people/service identities, resolves partner/customer assignments, evaluates resource/action/evidence-category policy, and issues short-lived job scopes.
- **Project and evidence:** resolves immutable baseline manifests, protected references, redaction markers, retention state, and authorized normalized projections. It never exposes source credentials.
- **Assessment orchestration:** owns run state, input locks, work plans, checkpoints, cancellation, retry, and completion classification.
- **Rule catalog and execution:** owns rule schemas, applicability, approval state, deterministic execution, result validation, and rule-quality evidence.
- **AI analysis:** owns prompt/model versions, evidence packets, injection defenses, schema validation, citations, budgets, and provider isolation.
- **Findings and review:** owns finding identity/occurrences, generated originals, editable presentation revisions, collaboration, dispositions, exceptions, risk acceptance, and recurrence.
- **Scoring and maturity:** owns versioned algorithms and mutable/published snapshots; it does not infer missing gap data as healthy or unhealthy under the default profile.
- **Recommendations and work:** owns recommendations, fix packages, review-only artifacts, consultant tasks, and CSV exports; it has no executor for customer-system changes.
- **Reporting and sharing:** owns canonical report projections, rendering, immutable manifests, acknowledgments, and expiring links.
- **Evaluation:** owns sampling cohorts, review outcomes, accuracy denominators, regression comparisons, and the pilot acceptance record.
- **Audit and notifications:** owns append-only security/business audit events and in-product notifications.
- **MCP projection:** owns read-only Phase 1D resources and applies the same policy engine and field redaction as the UI.

## Data model and lifecycle

Local implementation note (cycle 02): a value-free `synthetic-baseline-inventory-v1` internal fixture projection checks matching opaque scope, declared permission and exact compatibility before freezing sorted coverage keys and caller-declared applicability/gap items. It is an executable engineering boundary, not the production evidence manifest/transport schema, trusted baseline adapter, eligibility/authorization grant, semantic applicability engine or durable run model. See [local coverage guidance](../../docs/development/assessment-coverage.md) and [cycle 02](../../plans/completed/local-pilot-parallel-cycle-02.md) for its exact scope and evidence.

### Core records

| Record | Key constraints and ownership | Mutability/lifecycle |
|---|---|---|
| `Organization`, `PartnerAssignment` | Control-plane ID; dated assignment and role | Versioned; revocation immediately affects access |
| `Project`, `Environment` | Exactly one customer; environment belongs to project | Soft delete then purge under customer policy |
| `EvidenceBaselineRef` | Unique immutable ingestion baseline ID and manifest digest | Reference is immutable; availability state may change |
| `CapabilityEntry` | Unique source product/build/database/acquisition/module tuple and version | Versioned approval lifecycle |
| `AssessmentProfileVersion` | Unique profile/version; category weights sum to 1.0; explicit gap and severity policy | Immutable once used |
| `RuleCatalogVersion`, `RuleVersion` | Content digest; source/build/module applicability; approval evidence | Immutable; suspension affects new plans only |
| `DesiredOutcomeVersion` | Stable outcome ID plus version and lifecycle | Immutable version; only customer-approved affects adherence |
| `AssessmentRun` | Unique customer/project/run; idempotency key unique in project | State machine; input references immutable after lock |
| `RunInputLock` | One per run; exact versions and digests | Immutable |
| `CoverageItem` | Unique run + inventory/evidence-category key | Terminal state per run; append correction only before publication |
| `RuleResult` | Unique run + rule + subject + attempt-success key | Successful result immutable; failed attempts retained |
| `FindingIdentity` | Stable correlation identity within project | Persists across runs while evidence may expire |
| `FindingOccurrence` | Unique finding + run + affected subject | Immutable generated fact; recurrence links occurrences |
| `FindingRevision` | Original and human presentation/business context | Append-only; original cannot be overwritten |
| `DispositionEvent` | Actor, authority, from/to state, reason, time | Append-only |
| `RiskAcceptance` | Customer risk owner, rationale, controls, authorization, expiry/review date <= 1 year | Versioned; expiry produces required-review state |
| `ScoreSnapshot` | Algorithm/profile/input digest and multidimensional scores | Mutable-current snapshots replaced; published snapshots immutable |
| `MaturityResult` | Indicator evidence and level per scope | Frozen on publication |
| `Recommendation`, `FixPackage` | Generated original plus consultant review; source findings | Versioned; cannot execute |
| `ConsultantTask` | In-product owner/status/comments and originating links | Audited state machine |
| `ReportVersion` | Immutable report manifest and frozen projection digest | Permanent until retention/deletion policy applies |
| `ShareLink` | Random token digest, passcode-verifier digest, report ID, expiry <= 24h | Revocable; access audited; token never stored plaintext |
| `EvaluationCohort` | Locked run/finding IDs and stratification | Immutable for a pilot evaluation |
| `AuditEvent` | Actor/workload, action, customer/resource, outcome, correlation, reason | Append-only and separately retained |

Every customer-data record is structurally located in one customer data plane and includes project/environment scope where applicable. Foreign keys prohibit cross-project relationships unless an explicitly approved control-plane reference is used. Stable public identifiers are opaque and non-sequential.

### Coverage states

An inventory or evidence category has exactly one terminal run state: `pass`, `finding`, `not_applicable`, `not_assessed`, `insufficient_evidence`, `excluded`, `inaccessible`, `redacted`, `unsupported`, or `error`. `Pass` and `finding` mean applicable execution occurred. All other states include a typed reason, responsible stage, and evidence/provenance reference when permitted. A run can be `completed`, `completed_with_gaps`, or `failed`; it cannot be `completed` while any planned item lacks a terminal state.

### Evidence references

Findings store minimal excerpts only when allowed and otherwise store a protected reference containing baseline ID, normalized object key, field/path, value provenance, and classification. References are resolved server-side after current authorization and retention checks. If evidence is deleted or becomes unauthorized, the finding keeps the reference metadata and receives `unavailable`, `deleted`, or `redacted` without restoring the value.

### Retention and deletion

- Customer policy assigns separate retention classes to raw evidence, normalized evidence, derived assessments/findings/tasks, published artifacts, share artifacts, and audit records.
- Default retention is 30 days from the customer-selected supported clock event.
- Expiry soft-deletes records, removes them from ordinary reads and processing, revokes links, cancels unstarted work, and schedules active-system purge within 30 additional days.
- Recovery during the soft-delete window requires authorized operational action, reason, and audit evidence.
- Purge follows dependency order: access tokens/links, exports, raw evidence, normalized evidence, derived artifacts, then customer data-plane resources as policy permits. Historical records that law or approved audit policy requires to survive contain no deleted payload.
- Backup expiry and restoration handling must be documented and verified before pilot execution; they are not silently inferred from active-system purge.

## Scoring and maturity design

### Health score

The algorithm is versioned as `pilot-health-v1`.

1. Expand every grouped result to rule-subject **assessment units** so affected-object impact remains linear. Root-cause grouping changes presentation, not unit count.
2. Exclude all gap states from health scoring under the default profile. Include them in quality scoring. `Informational` findings have zero health penalty.
3. Give each applicable unit a catalog weight `w` greater than zero; default `w = 1`. A passed unit earns its full weight.
4. A score-affecting finding earns `w * (1 - S * C)` where severity factor `S` is Critical `1.00`, High `0.80`, Medium `0.45`, Low `0.20`, Informational `0.00`.
5. Confidence factor `C` is `1.00` for deterministic results. For confirmed AI results it is the locked confidence percentage constrained to `[0.50, 1.00]`. For deferred, accepted-risk, remediation-planned, in-progress, remediated-pending-validation, or reopened findings it is the same factor used when confirmed. Rejected findings earn full weight. Proposed AI findings are omitted from the publishable score and included in the provisional score using their constrained confidence. Critical/High deterministic findings that await mandatory review are included only in the provisional score until confirmed or otherwise disposed.
6. Calculate a category score as `100 * sum(earned weight) / sum(applicable weight)`, rounded to one decimal only for display. Empty categories show `not assessed`, not 100.
7. Overall health is the weighted arithmetic mean of assessed category scores. The default profile assigns equal weight to every assessed mandatory category. A versioned profile may override weights; weights for unassessed categories are removed and the remainder renormalized. The UI discloses this coverage effect.
8. Object-type, installed-module, and approved desired-outcome scores use the same unit formula over their respective unit sets. One unit may contribute to multiple views but only once to the overall category calculation.
9. Status is red below 50, yellow from 50 to below 80, and green at 80 or above.

Severity always remains visible and is never rewritten by confidence. A Critical or High item remains prominently listed even if confidence reduces its numeric penalty. The report explains the algorithm, factors, included states, assessed category weights, and quality exclusions without requiring exposure of every row-level arithmetic contribution.

### Assessment quality

`pilot-quality-v1` reports, but does not collapse into one health score:

- inventory representation: represented inventory keys / expected inventory keys;
- executable coverage: `pass` or `finding` units / all applicable planned units;
- evidence availability by terminal gap reason;
- rule execution success and failure;
- AI coverage and budget exhaustion;
- confidence distribution;
- capability-matrix limitations and unsupported generic/semantic checks;
- review completion for Critical/High and the pilot evaluation sample.

The main report presents a compact limitation warning and links to the separate quality report. It never describes a gap as a healthy control.

### Capability maturity

`pilot-maturity-v1` is evidence-based and not converted from the health score. Each mandatory domain contains approved indicators for documented design, consistent implementation, measured operation, governed review, and demonstrated improvement. An indicator is `met`, `partially_met`, `not_met`, or `insufficient_evidence` with evidence.

- `Initial`: no higher level is fully evidenced.
- `Developing`: documented design and repeatable implementation indicators are met for at least 60% of assessed mandatory domains.
- `Defined`: those indicators are met for at least 80% of assessed domains and governance ownership is evidenced.
- `Managed`: Defined plus measured operation and regular review are met for at least 80% of assessed domains.
- `Optimized`: Managed plus evidence of validated improvement across at least two assessments is met for at least 80% of assessed domains.

Insufficient-evidence domains do not count as met and are prominently disclosed. A higher level requires every preceding level. Indicator definitions and thresholds are part of the approved rule catalog and locked per run.

## Interfaces and contracts

Local implementation note (2026-10-01): the internal `CapabilityBoundCoverageProjector` composes the existing exact-version guard and terminal coverage projectors. It accepts only caller-supplied trusted descriptors and an already-authorized plan/results, returns no combined projection on any lock or coverage denial, and otherwise returns the frozen lock and existing coverage measures. This is a pure module composition with synthetic fixtures, not a new public operation or a baseline/inventory/authorization/durable-run implementation. See [development coverage guidance](../../docs/development/assessment-coverage.md) and [parallel cycle 01](../../plans/completed/local-pilot-parallel-cycle-01.md).

These are logical operation contracts. Concrete transport, paths, and public schemas require technical-owner approval during implementation planning.

### Start assessment

- Input: project/environment, baseline ID, profile version, desired-outcome set, enabled catalog, AI policy/budget, client idempotency key.
- Authorization: assigned consultant with `assessment.start`; customer policy must permit analysis and AI settings.
- Validation: one eligible baseline; exact capability row; immutable inputs available; profile weights valid; no duplicate active start key.
- Output: run ID, locked-input summary, planned stages, initial warnings.
- Idempotency: the same project and key with the same input digest returns the original run; a different digest conflicts.
- Errors: unauthorized, ineligible build, unavailable input, incompatible catalog, invalid profile, duplicate conflict.

### Read assessment/results

- Input: run or published version, filters, stable cursor, requested fields.
- Authorization: resource/action/evidence-category policy; field filtering occurs before serialization.
- Output: canonical typed projection plus version/digest and redaction markers.
- Pagination: cursor is bound to customer, query digest, sort key, and snapshot version; it expires and cannot cross scopes.
- Compatibility: additive fields are allowed within a schema version; breaking changes require a new media/schema version.

### Review finding

- Input: finding ID, expected revision, presentation changes or disposition, reason, optional policy/test references.
- Authorization: consultant or qualified customer reviewer by action; risk acceptance only by customer risk owner.
- Validation: permitted transition, required reason/fields, customer risk expiry no more than one year, no change to generated original.
- Concurrency: optimistic revision check; conflicts return current revision and no partial write.
- Idempotency: client event ID unique per finding.

### Publish report

- Input: run ID, expected run revision, audience views, explicit acknowledgment of any mandatory-review or coverage warnings, idempotency key.
- Authorization: assigned consultant with `report.publish`.
- Validation: run terminal; score snapshot current; render inputs available; warnings explicitly captured; no deletion hold.
- Output: immutable report version and render status.
- Atomicity: report manifest and frozen snapshots commit once; rendering may retry from that manifest.

### Create expiring link

- Input: published redacted report version, expiry no greater than 24 hours, delivery metadata, passcode policy.
- Authorization: assigned consultant with `report.share` and customer export permission.
- Output: secret-bearing URL returned once and separate passcode delivery material; stored state contains only verifiers/digests.
- Controls: revoke, rate limit, failed-attempt lockout, generic errors, no indexing/caching, access audit, field-level redaction at artifact creation.

### Export tasks CSV

- Input: project and task filter, published/current version selection.
- Authorization: assigned Consultant with `task.export` and customer export policy; Auditor only with an explicit scoped export grant and the same applicable customer policy, category and resource checks. This wording is reconciled with the approved role matrix under [TC14-06](local-planning-task-approval.md); Cycle14 implements no CSV export.
- Output: formula-injection-safe UTF-8 CSV containing stable IDs and protected links, not raw evidence.
- Idempotency: an export request digest may reuse an unexpired identical artifact; creation/access is audited.

### Phase 1D MCP

- Authentication: established named-user or service identity; no anonymous or share-link identity.
- Authorization: granular read permission plus customer/project/environment and field scope; revocation is checked on every request.
- Resources: assessment status, coverage summary, score/maturity snapshots, findings, recommendations, and protected evidence-reference metadata.
- Prohibited: raw evidence values, running analysis, comments/edits, dispositions, risk acceptance, publication, link/export generation, task creation, and any customer-system action.
- Concurrency/pagination: per-identity and per-customer limits; opaque snapshot-bound cursors.
- Audit: identity, tool/resource, scope, fields returned/redacted, outcome, correlation, and latency without payload values.

## Authorization and trust boundaries

- The internet/client boundary terminates authentication before customer-data-plane resolution. Client-supplied organization or storage locators are never trusted.
- The control plane resolves active partner/customer assignment and exactly one customer data plane.
- Every service and worker calls the central policy module with actor/workload, customer, project, environment, resource, action, evidence category, and record state. UI visibility is not an authorization control.
- Worker jobs contain opaque identifiers and a short-lived one-customer job scope. They cannot broaden their scope or use interactive-user credentials.
- The evidence boundary exposes normalized/redacted projections and protected references. Raw evidence access is a separate permission and is unavailable to pilot AI and MCP.
- The AI provider is an external processing boundary. Packets are minimized, redacted, residency-compatible, non-training by contract/configuration, bounded, and tied to a run/model/prompt version. Provider output is untrusted.
- Rule packages, evidence text, scripts, logs, comments, generated code, uploads, and model output are untrusted data. None can add tools, modify prompts/policy, retrieve secrets, or execute commands.
- Rendering runs without network access, credentials, or customer-data-plane query permission; it receives one signed render manifest and writes only its declared artifact.
- Share-link access is limited to a pre-rendered redacted artifact and cannot call canonical APIs.
- MCP uses the same policy decisions as the UI but has an explicit deny list for consequential actions and raw evidence.

## State transitions, concurrency, and idempotency

### Assessment run

`draft -> locking -> planned -> running -> scoring -> review_ready -> publishing -> published`

Terminal alternatives are `completed_with_gaps`, `failed`, `cancelled`, and `deleted`. A run may resume from durable checkpoints in `planned`, `running`, `scoring`, or `publishing`. Cancellation stops new work and lets in-flight read-only work finish or time out; completed results remain auditable. Retry creates a new attempt under the same work unit and never replaces a successful result.

Stage completion uses compare-and-swap on run revision plus an outbox event in the same transaction. At-least-once delivery is assumed; consumers deduplicate by event ID and work-unit idempotency key. A lease has an expiry and heartbeat; an expired worker may be replaced, while successful result uniqueness prevents duplicate active effects.

### Finding lifecycle

`proposed -> confirmed | rejected | deferred | accepted_risk | remediation_planned`

`remediation_planned -> in_progress -> remediated_pending_validation -> validated_closed`

`confirmed`, `rejected`, `deferred`, `accepted_risk`, or `validated_closed` may transition to `reopened` when a correlated issue recurs. `reopened` then follows the normal review/remediation states. Risk acceptance requires customer authority and expiry/review metadata. Expiry creates a review-required event and notification; it does not silently resolve or delete the finding.

### Publication

A run may have multiple immutable `ReportVersion` records, each created from a specific run revision and score snapshot. The same publication idempotency key cannot create multiple versions. Once its manifest is committed, later rendering retries must reproduce the same digest or fail visibly. Acknowledgment and link access append events but never alter the version.

### Reassessment correlation

Correlation first uses source product + environment + native table/type + stable UID. It then matches rule family and root-cause signature. Missing or changed native keys may use an explicit, confidence-bearing proposed correlation that requires consultant confirmation; the system never silently merges ambiguous objects. One new occurrence can reopen an existing finding identity while retaining every prior disposition.

## Failure modes

| Failure | Expected behavior | Recovery/mitigation |
|---|---|---|
| Baseline missing, deleted, or digest mismatch | Start fails, or active run pauses before affected reads; no fabricated coverage | Restore if policy permits or start a new run on a valid baseline; audit integrity event |
| Unsupported build/module/acquisition | Explicit unsupported coverage; pilot eligibility denied where required | Add and approve capability evidence in a new matrix version |
| Rule crashes or times out | Other work continues; affected units become `error`; run completes with gaps | Bounded retry; suspend defective rule for new runs; preserve version and attempts |
| Queue duplicate or worker loss | At-most-one successful result per work unit; lease expires | Resume from checkpoint; deduplicate by work key |
| AI disabled or budget exhausted | Deterministic assessment completes; explicit AI gap | Audited consultant override or new run/profile; never imply AI coverage |
| AI output cites missing/unauthorized evidence | Proposal rejected or marked insufficient; security event if policy violation suspected | Do not retry with broader data; inspect packet/prompt/model safely |
| Embedded prompt/tool instructions | Treated as evidence data; no tools or policy changes available | Injection tests, structural envelopes, output validation, provider/tool isolation |
| Conflicting evidence | Preserve each provenance and emit uncertainty/gap or finding | Human review; never silently choose a value |
| Concurrent finding edit | Stale writer receives revision conflict | Refresh and reapply intentionally |
| Publish with incomplete review/coverage | Allowed only after explicit warning acknowledgment; report prominently identifies limitations | New report version after review; old version remains immutable |
| Renderer failure or nondeterminism | Canonical version remains; artifact is unavailable, never partially published | Retry sandbox; digest comparison; operational alert |
| Share-link guessing or repeated passcode failure | Generic denial, rate limit/lock, audit | Revoke and recreate; security review on suspicious activity |
| Evidence expires after publication | Report/findings retain provenance and show evidence unavailable | No resurrection outside authorized recovery window |
| Retention purge fails | Data remains inaccessible; purge backlog alerts | Idempotent purge retry and incident handling |
| Cross-customer scope mismatch | Deny before query and emit security audit | Alert on repeated attempts; no resource existence leakage |
| Backup/restore misses target | Pilot readiness fails | Correct and repeat documented restore drill before pilot execution |
| MCP requests prohibited action/raw value | Typed authorization/capability denial | Audit without exposing the resource |

## Security and privacy

- Prepare a threat model before technical approval covering cross-tenant access, confused deputy, privilege escalation, stale assignment, evidence exfiltration, prompt injection, malicious scripts/configuration, rule-supply-chain compromise, CSV formula injection, report-rendering attacks, share-link theft/brute force, MCP abuse, deletion bypass, audit tampering, and backup exposure.
- Source database credentials remain solely in the ingestion credential boundary. Assessment components receive only baseline references.
- The ingestion permission-attestation result is a locked assessment input. Excess read-only permission produces the approved prominent warning/audit; any write, DDL, ownership, or administrative capability makes the baseline ineligible.
- Encrypt all customer data in transit and at rest with customer-specific encryption context. Keys, credentials, link secrets, and passcodes never enter reports or ordinary logs.
- Enforce protected-field minimization before storage and again before AI, render, export, share, or MCP serialization. Government identifiers and source secrets have no allowed pilot representation except explicit redaction markers.
- Normal rule-bundle and application promotion requires provenance, review status, integrity digest, dependency scan, security-owner approval for security rules and One Identity SME approval for all pilot rules. A repository-owner pilot exception may waive internal promotion review/evidence for an exact artifact, while retaining factual unverified status and the runtime security boundaries in the approved product and security specifications.
- Fix-package content is inert text in a sandboxed viewer/renderer. It cannot be executed, connected to a database, or copied into an automatic executor. The UI and export label it review-only and unverified until consultant review.
- Free-text fields show a sensitive-data warning, apply output encoding, and are excluded from AI unless specifically authorized by profile and classification.
- Audit storage is append-only to application roles, integrity-monitored, and excluded from customer-controlled editing. Audit payloads contain identifiers and outcomes, not evidence values.
- Customer data is not used for shared-model training without a separately approved explicit opt-in; the pilot default is no training.

## Observability

- **Logs:** structured event name, time, deployment/software version, correlation/trace ID, customer data-plane pseudonym, project/run/job IDs, stage, attempt, result/error class, duration, and redaction count. No credentials, passcodes, tokens, evidence values, prompts containing evidence, model responses, comments, or report content.
- **Metrics:** API latency/error/denial; queue age and depth; worker saturation; run/stage duration; checkpoint age; work completion/retry/error; coverage/gap counts; rule failure by version; AI token/cost/budget and schema/citation rejection; render time/digest mismatch; publication/share/MCP activity; retention/purge backlog; backup/restore objective evidence.
- **Alerts:** cross-scope denials above threshold; integrity mismatch; write-capable source permission; rule or AI systemic failure; queue/checkpoint stall threatening eight-hour target; renderer sandbox violation; unusual share-link failures; purge overdue; backup or restore failure; service health affecting support targets.
- **Traces/correlation:** one correlation chain from user operation through outbox, job attempts, evidence reads, AI call metadata, scoring, rendering, and audit. Trace baggage carries opaque scope IDs only.
- Customer/environment labels in operator views are access-controlled and pseudonymized where full identity is unnecessary. Metrics must not create cross-customer evidence channels.

## Data migration and compatibility

- **Migration strategy:** no existing application data exists. Introduce schema and manifest versions from the first pilot deployment. Database changes use expand/migrate/contract sequencing; destructive contraction waits until all supported application/worker versions and restoration tests no longer require the old shape.
- **Existing data:** feature-001 baselines are accepted only through a versioned adapter that validates manifest schema, normalized schema, capability metadata, provenance, and classification. An adapter never mutates the source baseline.
- **Rule compatibility:** a catalog declares compatible evidence-schema, source-build, module, and executor versions. Incompatible rules become explicit unsupported/not-assessed coverage.
- **Work compatibility:** queued payloads have a schema and minimum worker version. Deployments drain or route old work; unknown versions fail safely.
- **Report compatibility:** published manifests are immutable. Historical readers either support their schema or use a tested versioned projection; they do not rewrite old manifests.
- **Rollback strategy:** deploy the prior application/worker version only while its schema and job contracts remain supported. Pause new runs/publication during rollback, let compatible read-only work drain, and never roll back by deleting new customer data. Restore is for verified data loss/corruption, not ordinary release rollback.

## Deployment and configuration

- **Deployment ordering:** storage expansion -> capability/rule/profile seed validation -> policy service -> read APIs -> worker pools -> write/orchestration paths -> UI -> render/share -> Phase 1D MCP. Contract checks run before each activation.
- **Configuration:** identity provider, customer data-plane map, encryption/key references, retention classes, worker concurrency, timeouts/retries, AI provider/model allowlist, no-training/residency controls, budgets, render sandbox, link expiry/rate limits, support routing, and observability thresholds. Secrets use the approved secret store only.
- **Feature flags:** separate flags for 1A, deterministic analysis, AI analysis, scoring, tasks/CSV, review/publication, expiring links, reassessment/evaluation, and MCP. Flags are server-enforced capabilities, scoped by customer, and audited. Removal occurs only after the sub-phase is accepted, rollback window closes, and operations confirms no customer requires disablement.
- **Pilot readiness:** accepted ADRs; threat model; access-control matrix; exact validated capability rows for two independent environments; approved query/rule catalogs; verified backup/restore; security and accessibility review; load evidence; incident/support runbooks; and implementation/test plans approved.

## Test implications

The later test plan must include unit, property, contract, integration, end-to-end, security, accessibility, performance, recovery, compatibility, and evaluation suites. At minimum:

| Acceptance criterion | Required verification |
|---|---|
| AC-HAS-1 | Inventory reconciliation proves every expected key has exactly one terminal coverage state |
| AC-HAS-2 | Finding contract and UI test distinguish fact/inference/assumption, evidence, rule/version, guidance, severity, confidence, outcomes, and recommendations |
| AC-HAS-3 | Deterministic and AI Critical/High fixtures remain proposed and visibly warned before review |
| AC-HAS-4 | Authorization matrix denies consultant risk acceptance and validates all required customer fields |
| AC-HAS-5 | Golden scoring fixtures cover weights, severity selection, confidence, every lifecycle state, passes, gaps, module/object/outcome views, and rounding |
| AC-HAS-6 | Concurrent edit/publication test proves prior score and artifacts remain byte-identical |
| AC-HAS-7 | Partial baseline fixture separates health narrative from complete quality limitations |
| AC-HAS-8 | Adversarial evidence-instruction suite proves bounded packets, citations, budget, and no instruction/tool following |
| AC-HAS-9 | UI, worker, export, share, AI, and MCP tests deny unauthorized protected/raw evidence and verify audit |
| AC-HAS-10 | Reassessment fixtures reopen stable correlations and require review for ambiguous identity changes |
| AC-HAS-11 | Acknowledgment records exact metadata without changing dispositions |
| AC-HAS-12 | Evaluation fixtures test >80% boundary, indeterminate exclusion, denominator, <100 findings, and low-sample warning |
| AC-HAS-13 | WCAG 2.2 AA automated/manual checks, keyboard/screen-reader flows, focus restoration, virtualized-list access, and graph/table equivalence |
| AC-HAS-14 | Capability tests deny every deferred Phase 2–4 action and show no false completion state |
| AC-HAS-15 | Eligible and ineligible SQL permission/build/module fixtures; topology fields absent from collection and outputs |
| AC-HAS-16 | Matrix/rule reconciliation covers every approved module and generic custom-module limitation |
| AC-HAS-17 | Golden publication test separates provisional score, publishable score, and quality measure |
| AC-HAS-18 | Generated SQL/script remains inert, labeled, reviewed, CSV-safe, and unavailable to execution paths |
| AC-HAS-19 | Two-environment initial/reassessment pilot evaluation with exact builds, recurrence, score-change explanation, sample, and accuracy gate |
| AC-HAS-20 | Cross-surface manifest/digest consistency; link expiry/revocation/passcode/redaction; MCP deny-list and read-only contract |

Every pilot rule has positive, negative, insufficient-evidence, exclusion, and version-compatibility fixtures. Security rules require security-owner test evidence. The rule catalog cannot enter a pilot-eligible state while a selected mandatory domain/module lacks approved rules or an explicit unsupported declaration. A repository-owner pilot promotion exception can enable limited trial use with explicit unverified status, scope, expiry and disclosure; it never supplies missing fixture/reviewer evidence or changes the acceptance gate.

Performance tests use independently measured categories up to 100,000 identities, accounts, entitlements, roles, workflows, and 90-day operational records. They verify common interactions at two seconds p95, evidence views at three seconds p95, assessment progress/checkpoints, and the eight-hour target under documented infrastructure and concurrency. Results are evidence, not guarantees beyond the approved scale profile.

Same-region recovery tests prove the 24-hour RPO and one-business-day RTO for recoverable product-managed pilot data and cover database metadata, blobs, manifests, rule/profile configuration, findings, reports, tasks, and audit history. They also verify that restore does not resurrect expired share links or bypass deletion state. A region-wide Azure outage is exercised by business-disaster tabletop, not treated as an RTO test.

## Alternatives considered

### Microservices for each assessment capability

- Advantages: independent deployment and scaling; strong process isolation.
- Disadvantages: distributed authorization, audit, consistency, and versioning before pilot workloads justify them.
- Reason not selected: accepted ADR-0001 uses a modular application plus isolated workers and preserves later extraction.

### Shared tables with application-only tenant filters

- Advantages: simplest storage and analytics.
- Disadvantages: insufficient defense in depth and difficult customer-specific deletion/restore.
- Reason not selected: accepted ADR-0002 requires a separate customer data plane.

### Event-source all state

- Advantages: complete append-only history.
- Disadvantages: privacy erasure, projection compatibility, and operational complexity are disproportionate.
- Reason not selected: accepted ADR-0003 uses immutable manifests and append-only events only where history is required.

### AI-only assessment

- Advantages: fast expansion of qualitative checks.
- Disadvantages: lower reproducibility, weak coverage accounting, and unacceptable governance for deterministic controls.
- Reason not selected: the approved product explicitly requires deterministic and AI distinction and rule governance.

### Synchronous assessment and report generation

- Advantages: smaller infrastructure footprint.
- Disadvantages: incompatible with eight-hour work, checkpoints, partial success, and reliable render retries.
- Reason not selected: durable asynchronous work is required by the reliability objectives.

### Store evidence excerpts directly in every finding

- Advantages: simple report rendering.
- Disadvantages: multiplies sensitive data and frustrates redaction, authorization, retention, and deletion.
- Reason not selected: findings prefer protected references and minimal authorized excerpts.

## Risks, open questions, and required decisions

### Resolved technical-approval decisions

- ADR-0004, including Service Bus Standard and non-HA PostgreSQL, was accepted by the repository owner on 2026-09-29.
- The application-registration, BFF session, lifetime, privileged reauthentication, guest-review, revocation and no-local-break-glass defaults in `docs/security/health-assessment-identity-session-design.md` were approved by the repository owner on 2026-09-29.

The exact pilot environment/build rows, Entra registration IDs, OpenAI project ID/ZDR evidence, cloud resource IDs, secrets and regional deployment parameters are required before the relevant pilot workload is enabled, but they are provisioning and verification evidence rather than unresolved product or architecture decisions.

### Risks to validate

- One Identity database schemas and safe query behavior can vary by exact build, hotfix, installed modules, and customization; a broad `10.x` label is insufficient.
- Vendor-default comparison depends on legally usable and exact matching vendor baselines. Missing baselines must reduce quality and limit conclusions.
- Rule-subject expansion can create high work-unit volume; planning, batching, indexing, and correlation must be load-tested at category scale.
- The proposed confidence multiplier could make uncertain severe AI findings appear numerically small despite warnings; usability research and golden examples must validate comprehension.
- PDF/Markdown parity can drift from interactive results unless every output is rendered from the frozen canonical projection and checked by digest/contract tests.
- The customer-data-plane model increases migration and restore overhead; operations evidence must confirm it is viable before acceptance.
- WCAG conformance for dense graphs, virtualized lists, side panels, and PDF output requires specialist review, not automated checks alone.
- Accuracy above 80% is not guaranteed by architecture. Release must fail the pilot gate if the locked evaluation cohort does not meet it.

## Approval

Approved by: Repository owner  
Date: 2026-09-28  

Only an authorized human reviewer may change `Status` to `Approved`.

## Accepted local BFF authentication contract — 2026-10-02

The repository owner accepted exact D01/D02 in [the attributed decision](../../docs/development/bff-production-contract-proposal.md). [BFF authentication v1 OpenAPI](../../contracts/bff-authentication/bff-v1.openapi.json) governs only session/CSRF, native browser form sign-in and exact local sign-out. Framework callbacks retain supported code/PKCE/state/nonce/correlation and signed token validation. Authentication time is signed `auth_time` bound to a protected single-use challenge; current subject admission cannot supply another session's time or MFA. Each request checks the original authentication, exact stored session, current cutoff, roles and security version. Missing evidence denies; privileged verification stays default-denying.

[The bounded implementation/test packet](../../plans/completed/bff-authentication-contract-cycle.md) records executed proof and limits. Additive control-plane migration002 introduces the explicit provider cutoff and pending challenges; existing null-cutoff rows deny until trusted administration supplies evidence. It is not a startup migration, enrollment permission or selected cleanup/retention policy. Reusable web types/helpers are unused by the synthetic consultant UI. The diagnostic executable remains permanently disabled. D03/D04 production authority, audit/key/proxy composition and D05 image/live activation remain separately reviewed dependencies; this local approval does not enable a provider or authorize a release.

## Proposed production BFF addendum — 2026-10-02

The [P01–P05 production authority/hosting proposal](../../docs/development/bff-production-authority-hosting-proposal.md) and [ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md) supply candidate closed enrollment/assignment/external lifecycle/provider observations, one subject security version, direct-only initial role boundary, atomic session/audit functions, explicit proxy trust and shared protection keys. This was proposed at the preparation checkpoint. The following attributed owner acceptance now governs the exact local synthetic implementation; real bindings, audit-outage preservation, live provider/access and acceptance remain separate.

## Approved BFF local authority/audit cycle — 2026-10-02

The repository owner approved P01–P05 and ADR-0009 for local synthetic implementation against immutable reviewed packet `cacbe372ff8a9bc448033e6918308c5f820b4bfd`. [Execution plan](../../plans/active/bff-local-authority-audit-implementation.md) owns implementation, exact P01–P05 verification and independent review. Real provider/Azure/SQL grants, production activation, audit-outage preservation, paid deployment and G1–G9 remain separate. The approved local authority/audit/hosting seams are implemented and independently reviewed; exact executed results, immutable sources and remaining live limits are bound in [the execution evidence](../../docs/development/evidence/bff-local-authority-audit-20261002.json). Configured hosted verification/private publication close this bounded cycle; real production composition and gates remain separate.

## Proposed HTTPS access addendum — 2026-10-03 UTC

The owner changes the access preference and preparation sequence to HTTPS now. [Proposed ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [the proposed topology/test packet](../../docs/development/https-pilot-portal-proposal.md) require exact review before local implementation or public exposure. This addendum records the proposal only; it does not amend the approved technical/security baseline or authorize production sign-in, new roles, grants, migrations or spending. Approved product scope and G1–G9 remain unchanged.

## HTTPS local implementation approved — 2026-10-03 UTC

The repository owner explicitly approved [ADR-0011 Option A](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [the exact local HTTPS-P01 packet](../../docs/development/https-pilot-portal-approval.md) against immutable proposal50cf4fd. [Approval metadata](../../docs/development/evidence/https-pilot-portal-approval-20261003.json) binds the original design/technical/test/plan source. Separate optional disabled IaC and negative/input/composition checks are now authorized; the local approval task is CLOSED and implementation begins in an isolated worktree with non-author review. Public activation, real identity/permission/SQL/key/audit changes, new paid session and production release remain separately gated. No implementation pass or live evidence is claimed by approval; G1–G9/Milestone2 remain NOT VERIFIED. Private BFF/access board publication is still blocked by the separately rejected publisher credential/network action; its snapshot remains stale.


## HTTPS-P01 environment scaffold — executed local closure, 2026-10-03 UTC

The [approved bounded packet](../../docs/development/https-pilot-portal-approval.md) now stores separate optional HTTPS environment templates and input/compiled policies. Worker `e796c36` passed non-author review and is integrated as `6690955`; no application is supplied. Required false-only composition and hard-disabled public network access prevent activation. No Azure resources, public endpoint, identity/grant, migration, invitation or new paid session were created.

[Executed evidence](../../docs/development/evidence/https-pilot-portal-implementation-20261003.json): all23 Bicep build/lint checks (zero diagnostics), both new format comparisons,38 unsafe template mutations,50 unsafe input cases and51 actual CLI denials passed. Combined74-project locked restore/format/Release build (zero warnings/errors),28 unit/architecture projects, three portable integration projects and13 existing infrastructure policy commands passed. Database/frontend/browser/Windows/package checks and fresh hosted CI were not rerun in this packet; their historical evidence is not current combined verification. No public runtime or gate acceptance follows.

HTTPS-T01 has local environment/input evidence; HTTPS-T02–T08 and G1–G9/Milestone2 remain NOT VERIFIED. [The next production dependency handoff](../../docs/development/https-pilot-portal-next-packet.md) records unresolved contracts and protected proof. GitHub upload is blocked by automatic approval review pending exact public-repository publication approval; the private BFF/access board remains stale under its separate publisher block. [Active plan](../../plans/active/https-pilot-portal-preparation.md#publication-dependencies) records both dependencies and prepared board changes. Preserve unrelated Cycle13 canonical/publication evidence.


## Proposed production HTTPS contracts — 2026-10-03 UTC

The owner authorized production specification/test preparation after the published HTTPS scaffold. [The production composition proposal](../../docs/development/https-production-contract-proposal.md), [provider proposal](../../docs/development/https-production-provider-contract-proposal.md), [shared-key proposal](../../docs/development/https-production-key-contract-proposal.md), [proposed ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md) and [acceptance packet](../../docs/development/https-production-test-packet.md) are PROPOSED, not amendments to the approved product/security baseline. [The preparation plan](../../plans/active/https-production-contract-preparation.md) records bounded ownership and non-author reviews.

Sixty-eight planned host/resource/privilege, provider, shared-key, failure-audit, ingress and live-prerequisite cases are unexecuted. Available-store failure producers, anonymous receipt compatibility, total-audit-outage limitations, source-occurrence retention interpretation, real Graph normalization/paging/home limitations, missing-ring rebootstrap/rollback guards and exact-session CA-context proof are explicit. PC-D01–PC-D05 and the focused provider/key decisions require attributed technical/security/identity/platform/operations review before dependent implementation. SDK pins, manifest consistency, inventory/witness/lifecycle and exact step-up/unavailable-response contracts remain prerequisites. No code, dependency, grant, migration, actual identifier, Azure operation, public admission or spending changed.

Prior exact receipt source b065b51 separately passed [bootstrap37093087397](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37093087397) and [package37093087429](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37093087429); these completed repeat results supersede its historical pending state and are not execution of the new proposed cases. G1–G9/Milestone2 and production release remain NOT VERIFIED. Historical GitGuardian findings remain for human disposition. The private BFF/HTTPS snapshot remains stale under its separate rejected publisher action; the linked human-task delta is prepared in the acceptance packet and unrelated confirmed Cycle14 publication is preserved.


## HTTPS production local cycle 01 — owner approval, 2026-10-03 UTC

The owner approved the recommended local PC-D01–PC-D05 design after exact packet `d518d25`. [The bounded implementation/test plan](../../plans/active/https-production-local-cycle01.md) records accepted choices and unresolved exact inputs. Strict Graph response projection and Microsoft provider artifact audit are RUNNING; no cloud activation or grants. Source-only/local tests cannot satisfy live gates. The private BFF/HTTPS board remains stale under the separate publisher rejection.


### HTTPS local cycle01 verified checkpoint — 2026-10-03 UTC

[Cycle01](../../plans/active/https-production-local-cycle01.md) now has a strict unused Graph response projector,125 new parser checks (212 authority suite total), independent29-case review and a reviewed32-archive Microsoft key-provider artifact audit. Full locked audited restore, full formatting, warning-free Release build and12 applicable unit/architecture/PostgreSQL/HTTPS regression suites passed. [Exact execution evidence](../../docs/development/evidence/https-production-local-cycle01-20261003.json) records failures/corrections and limits. Only partial local HTTPS-PROV-T03/05/07/08 coverage is verified;68 proposed production cases and actual provider/CA/keys/ingress/recovery/live gates are not accepted. No new dependencies, schema/migration, production wiring, permission or Azure operation. SDK actual graph/guards/signature evidence, audit persistence/witness/outage and exact remaining contracts/bindings remain open. Private BFF/HTTPS board stays stale under separate publisher rejection; linked task delta reflects local design approval without closing live tasks.


## HTTPS local cycle02 — 2026-10-03 UTC

Owner “Next step” continues the accepted PC-D03 local protocol and PC-D02 artifact strategy. [Cycle02](../../plans/active/https-production-local-cycle02.md) implements closed failure-journal metadata and per-attempt freezing, and checks an actual isolated key-provider dependency graph. No producer wiring, durable audit acknowledgment, schema/dependency/permission/Azure/public-admission change. Actual persistence/receipts/witness/limits/key/tenant/ingress/live gates stay open; board remains stale under its separate publisher blocker.


### HTTPS local cycle02 verified checkpoint — 2026-10-03 UTC

[Cycle02](../../plans/active/https-production-local-cycle02.md) closes the unused failure-journal metadata codec and first-terminal attempt freeze, with212 new assertions (281 session/audit suite total) and146 independent hostile/concurrent checks. Exact trusted binding, canonical UTF-8, original terminal UTC and coherent optional subject/session/version are checked; parsing grants no identity trust or durable acknowledgment. Full locked audited restore, unfiltered solution formatting, zero-warning Release build and all12 applicable unit/architecture/PostgreSQL/HTTPS suites passed on integrated runtime source `ffd2278`. [Source-bound execution evidence](../../docs/development/evidence/https-production-local-cycle02-20261003.json) retains the bounded review and results.

[Actual isolated provider graph](../../docs/development/https-key-provider-resolved-graph-evidence.md) resolves37 packages; normal explicit signature verification passes36 and fails existing ProtectedData4.5.0 with certificate trust/expiry errors (exit1). Experimental SDK build/format/dispatch remains NOT EXECUTED; no tampering claim or integrity exception. AP-T01–03 provide partial AU01–03 only; AP-T04 is a KEY prerequisite, not full production case/gate acceptance. No producer hook, persistence/schema/migration, dependency/lock, permission or Azure/public admission change. Exact receipts/witness/lifecycle/limits/outage and key/tenant/CA/ingress/live proof remain open. The six linked human tasks persist; private BFF/HTTPS snapshot stays stale under the separate rejected publisher action, and unrelated Cycle14 publication is preserved.


## HTTPS local cycle03 — 2026-10-03 UTC

Owner “Approved, next cycle” continues the accepted ADR-0012 local prototype. [Exact cycle03 fixture/test plan](../../plans/active/https-production-local-cycle03.md) prepares actual isolated PostgreSQL anonymous outcome/source receipts and supported-platform signature proof. Production receipt-v1, schema/grants/hooks/lifecycle/keys remain unchanged; exact production decisions/live gates stay open. No new test/runtime PASS yet. The six linked human tasks persist and private BFF/HTTPS board remains stale under its separate publisher block.


### HTTPS local cycle03 verified checkpoint — 2026-10-03 UTC

[Cycle03](../../plans/active/https-production-local-cycle03.md) now has a test-only actual PostgreSQL anonymous outcome/source-receipt prototype:60 new checks (141 audit suite total) and21 independent direct SQL oracles passed. Exact descriptor/binding/digest conflicts, concurrent direct/reconciliation retries, rollback/deferred obligations, restricted LOGIN roles, seven-digit original occurrence and fresh canonical post-lock time are verified in owned loopback fixtures. This installs no production receipt schema/grant/hook or Azure journal/witness; synthetic source references prove no Blob delivery or independent witness trust.

[Supported-platform proof preparation](../../docs/development/https-key-provider-platform-verification.md) stores an exact37-archive manifest and reusable Windows/Linux verifier. Eighteen mocked safety tests plus8 independent mocked probes passed; all37 retained hashes match. Microsoft documents macOS verification as unsupported; the observed timestamp-chain failure is consistent with its documented limitation, with full root attribution unobserved. Actual Windows/Linux signature execution, SDK dispatch and production package acceptance remain NOT VERIFIED.

Full locked audited solution restore, unfiltered formatting, zero-warning Release build and all12 applicable unit/architecture/PostgreSQL/HTTPS regression suites passed on integrated source `e2ed26f`. Composition passed643 assertions; the existing documented half-microsecond conditional accounts for643/644 variation without skipping original scenarios. [Exact source/execution receipt](../../docs/development/evidence/https-production-local-cycle03-20261003.json) preserves failures/corrections and limits. No production source/migration/dependency/lock/config/IaC/workflow, permission, Azure resource, public admission or paid session changed. Partial AU/KEY prerequisite evidence closes only this local packet; G1–G9/Milestone2/full production cases stay open. Six source-linked human tasks remain; private BFF/HTTPS snapshot is stale under the separately rejected publisher action and unrelated Cycle14 publication is preserved.

## HTTPS local cycle04 — 2026-10-03 UTC

Owner “Approved” continues from the completed cycle03 checkpoint. [Exact cycle04 plan](../../plans/active/https-production-local-cycle04.md) prepares a minimal Linux signature CI/export packet and a documents-only production audit receipt/lifecycle review. No actual supported-platform signatures, public export, production schema/lifecycle acceptance or Azure activation is claimed. Independent scope review precedes isolated writing workers. The six linked human tasks remain open; the BFF/HTTPS private board remains stale under its separate rejected publisher action, preserving unrelated Cycle14 publication.

### HTTPS local cycle04 reviewed preparation — 2026-10-03 UTC

[Cycle04](../../plans/active/https-production-local-cycle04.md) completed its bounded local preparation. The [minimal public CI approval packet](../../docs/development/https-key-provider-public-export-approval.md) binds a separate seven-file candidate `4fadf7ced8b6c6b978e326e2fae6d925001fdb26`, sole public parent31c61ea and exact file/patch digests. No coordinator implementation ancestry is included. Author, independent reviewer and coordinator each executed16 downloader+18 verifier mocked safety tests; reviewer additionally executed10 independent hostile HTTP/output cases. YAML/static permissions/actions/upload controls, five bash bodies, two inline Python ASTs, four Python source ASTs, documentation/source bindings, whitespace and exact-export Gitleaks passed. These are diagnostic preparation checks; no real HTTP acquisition, SDK setup, supported-platform signature or remote workflow/publication occurred. Exact public export/run approval remains pending.

The [production audit receipt/lifecycle proposal](../../docs/development/https-production-audit-receipt-lifecycle-proposal.md) and [ARL01–ARL18 planned packet](../../docs/development/https-production-audit-receipt-lifecycle-test-packet.md) passed non-author source/policy review. Twelve-month/30-day/35-day policy remains unchanged. Uniform NEW direct/source occurrence age, identity/projection/source trust, linked read/hold/deletion rules and minimal marker/witness horizon are explicitly proposed; nine production decisions/inputs remain open. Historical view/hold/purge clock and fixture live-event FK gaps are disclosed. No ARL production case or schema/grant/lifecycle behavior was implemented or executed.

[Source-bound cycle04 receipt](../../docs/development/evidence/https-production-local-cycle04-20261003.json) records checks/review and scope. Unchanged C#/PostgreSQL source was not rebuilt/retested; actual cycle03 build/format/restore and12-suite evidence remains historical. No Azure resource, paid session, role, key call, dependency lock, portal admission or release changed; G1–G9/Milestone2 stay open. All six source-linked [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) persist with concrete CI/ARL review links. The prepared private-board delta is this checkpoint and the six tasks; publication remains unavailable under the separately rejected credential/network publisher proxy-bypass action. Last confirmed BFF/HTTPS snapshot4346e0f,2026-10-02T19:50:45.775370+00:00 is stale; unrelated Cycle14 publications are preserved. No publisher retry/bypass occurred.

## HTTPS exact Linux proof execution — 2026-10-03 UTC

Owner approved the exact seven-file4fadf7c public packet. [Draft PR3](https://github.com/pedrovolua-pixel/IGAMigrationTool/pull/3) now exists and is attached; public main31c61ea/old pilot branchb065 remain unchanged. [Run37139568768](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37139568768) failed before SDK setup/download/signatures:18 verifier mocks passed, downloader15/16 passed, with the single failure assuming HTTPSHandler's normal verified context must be None. No diagnostic artifact exists. Actual runner2.337.0/Ubuntu24.04.5/image20260927.320.1 is setup evidence only. Supported-platform signature proof remains NOT VERIFIED; no package-integrity acceptance or trust exception.

[Execution plan](../../plans/active/https-key-provider-linux-proof-run.md), [failure and exact correction approval](../../docs/development/https-key-provider-linux-run-result.md) and [source-bound receipt](../../docs/development/evidence/https-key-provider-linux-proof-run-20261003.json) record this superseding checkpoint. A local one-file test-only correction checks effective CERT_REQUIRED+hostname validation without sockets;17 downloader+18 unchanged verifier mocks and independent17 downloader+6 secure/unsafe controls passed. Candidatecefd8354d69e00eb372f8efff457e9b175d3a6eb is unpublished and needs exact changed-byte approval before updating PR3/rerunning. Workflow/downloader/verifier/manifest/SDK and six other public files remain unchanged. No application/dependency/schema/IaC/production wiring, Azure session/resource/grant or public admission changed. Unchanged C#/PostgreSQL regression evidence is historical, not rerun.

All six linked [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) persist; the shared-key task now asks for this exact correction publication/run followed by actual supported-platform evidence. Production audit decisions/ARL cases and G1–G9/Milestone2 stay open. Prepared private-board delta reflects this failure/local fix; BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale because automatic approval review rejected the separate credential/network publisher proxy-bypass action. No retry/bypass; unrelated confirmed Cycle14 changes remain preserved.

## HTTPS corrected Linux run — 2026-10-03 UTC

Owner “Run it” authorized publishing exact one-file correctioncefd835 and repeating the sole Linux diagnostic job. PR3 stays draft/2commits/7files/base31c61ea; main and the old pilot branch remain unchanged. [Run37145824111](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37145824111)/job111269428858 completed SUCCESS with all12steps and35mocked safety tests passing. Setup log binds runner2.337.0/Ubuntu24.04.5/image20260927.320.1/readContents/actionpins and exact synthetic mergee7e5f70. Artifact11281169753/85352bytes/digestfbf6baee29e156edcfac7e4f0ca4454bc188ae5fedd88cd5f071b65687e3b395 exists in GitHub metadata.

[Current result and manual artifact intake](../../docs/development/https-key-provider-linux-run-result.md), [plan](../../plans/active/https-key-provider-linux-proof-run.md) and [source-bound rerun metadata](../../docs/development/evidence/https-key-provider-linux-proof-rerun-20261003.json) supersede correction-publication approval pending. Actual execution PASS does not independently accept all37archive hashes/direct exits/raw logs/config/SDK/source/normal selected trust. ZIP retrieval returned connectorHTTP403; automatic approval review rejected the alternate browser download as a policy workaround. No further route/bypass; owner was asked to attach the exact diagnostic ZIP. Receipt/platform acceptance stays NOT VERIFIED until safe ZIP/digest and individual receipt review. Prior failed run remains historical.

The six linked [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) remain open; shared-key task now requests that manual artifact/review followed by actual provider dispatch/lifecycle and protected inputs. No Azure resources/grants/spending/session, dependency integration/harness, merge, admission or release. No unchanged C#/PG checks rerun. Production decisions/ARL/G1–G9/Milestone2 remain open. Prepared private-board delta reflects successful remote execution with incomplete proof; BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the separate rejected credential/network publisher proxy-bypass action; unrelated Cycle14 updates preserved and no publisher retry/bypass.

## HTTPS supported Linux artifact prerequisite verified — 2026-10-03 UTC

Owner supplied the original diagnostic ZIP/folder for run37145824111. [Exact review](../../docs/development/https-key-provider-linux-run-result.md) and [source-bound manual receipt](../../docs/development/evidence/https-key-provider-linux-proof-manual-20261003.json) close the previous manual-artifact dependency. ZIP SHA256fbf6baee29e156edcfac7e4f0ca4454bc188ae5fedd88cd5f071b65687e3b395 matches GitHub;85 safe member bytes,37 original official acquisition/hash receipts,37 direct normal verifier exits0,78raw log byte/hash checks,7sources,SDK10.0.401/runtime10.0.12/Linux-x64/config/pins,runner2.337.0/Ubuntu24.04.5/image20260927.320.1 and selected normal SDK trust bundles independently passed. Unchanged ProtectedData4.5.0 now passes on Linux; historical macOS failure preserved with no trust or version change. Finder .DS_Store outside ZIP excluded; initial disk-pressure shell failure resolved through argument-based checks without deleting evidence.

Owner “continue” starts the [bounded local Microsoft provider dispatch diagnostic](../../plans/active/https-key-provider-dispatch-diagnostic.md). It uses only the verified experimental graph with synthetic clients/resolvers/no-network traps; no production package/lock/config/IaC/permission/resource/admission or paid session change. Signature evidence clears only this artifact prerequisite; current SDK dispatch/harness, guard/inventory/witness/lifecycle/403/transport/diagnostics, production/ARL/G1–G9/Milestone2 remain open. All six linked [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) persist, with signature/manual-artifact substep closed. Prepared private-board delta follows; BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the separate automatically rejected credential/network publisher proxy-bypass action; no retry/bypass and unrelated Cycle14 updates preserved.

## HTTPS bounded Microsoft provider dispatch verified — 2026-10-03 UTC

[DISP01–08](../../plans/active/https-key-provider-dispatch-diagnostic.md) is VERIFIED as a local test-only diagnostic after the [Linux artifact prerequisite](../../docs/development/https-key-provider-linux-run-result.md). Exact candidate4ad7597 was independently reviewed and integrated as8db45d7 with identical three-file hashes. Author, non-author reviewer and coordinator each executed27 actual Microsoft provider cases,37 raw archive hashes/74 selected DLL entries, empty-source scratch restore, Release build0 warnings/errors and whole-project format. Author seven/reviewer nine runner negatives deny before subprocess. Separate meaningful formatter control rejects misformatted C# with exit2 and passes clean/restored copies; original project-file and CSSM diagnostics retained. [Detailed result](../../docs/development/https-key-provider-dispatch-evidence.md) and [source-bound receipt](../../docs/development/evidence/https-key-provider-dispatch-20261003.json) record initial failures, corrections, logs, hashes and precise scope.

This proves actual public registration, legacy Blob overload interception, synthetic existing-mode/CAS/delete-conflict denial and concrete historical unwrap selection with different synthetic keys. Fixtures do not define production parser/crypto/lifecycle policy or provide403/real HTTP/diagnostics/inventory/witness/rollback/composition/fullKEY/ARL/production/live acceptance. No production dependency/lock/configuration/schema/IaC/grant/Azure/spending/admission/public publication changed. Unchanged product regressions were not rerun; this scratch build is separately labeled. G1–G9/Milestone2 and all six linked [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) remain open with the signature and diagnostic substeps complete. Prepared private-board delta follows; BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the separately rejected credential/network publisher proxy-bypass action; no retry/bypass and unrelated Cycle14 updates preserved.

## HTTPS resolver pipeline next step — 2026-10-03 UTC

Owner “approved, next step” continues the accepted PC-D02 local SDK scope after65f2608. The [RP01–08 plan](../../plans/active/https-key-resolver-pipeline-diagnostic.md) passed non-author scope review before an isolated writing worker starts. It tests actual KeyResolver metadata403/force-remote interception and SDK diagnostics with deterministic no-network responses and fixed synthetic guards. Implementation/execution/review are RUNNING; no new RP PASS or production policy is inferred. Existing27-case dispatch and37 Linux signature proof remain historical verified checkpoints. Production inventory/witness/lifecycle/limits and protectedbindings/live/gates remain open. No production source/dependency/config/schema/grant/Azure/spending/publish change; six linked human tasks persist and the private BFF/HTTPS publisher block remains.


## HTTPS shared-key input preparation reviewed — 2026-10-04 UTC

[Proposed ADR-0013](../../architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md) and the [production input checklist](../../docs/development/https-key-production-input-checklist.md) passed independent INV-P01–05 documentary review at exact candidate66ca09a and were integrated with identical source hashes. The checklist maps seven technical decisions and nine protected/live evidence groups to existing canonical requirements and42 null intake fields. It preserves approved session/authentication/audit/backup values separately from unspecified key policy. [Source-bound documentary evidence](../../docs/development/evidence/https-key-inventory-preparation-20261004.json) records41 local links/13 anchors, UTF-8/newlines, proposed-only/protected prose, whitespace and secret checks plus the retained wording correction.

ADR-0013 remains Proposed and no inventory store, independent witness, protocol, lifecycle quantity or production policy is selected. Technical reviewers must prepare justified concrete contracts/values for attributed human review; the owner need not invent engineering values. This packet executes no runtime/new KEY test or cloud operation. Actual resolver/diagnostics is separately RUNNING under the [source-backed synthetic challenge clarification](../../plans/active/https-key-resolver-pipeline-diagnostic.md#supported-synthetic-challenge-clarification--2026-10-04-utc). Six human tasks, full KEY/ARL/live, G1–G9/Milestone2 and the private BFF/HTTPS publisher block remain open.


## HTTPS actual resolver pipeline verified — 2026-10-04 UTC

[RP01–08](../../plans/active/https-key-resolver-pipeline-diagnostic.md) is VERIFIED as a bounded local diagnostic: exact candidatefb6e837 passed non-author review and integrated as35a3175 with identical four-file hashes. Author, independent reviewer and coordinator each executed45 cases (27 preserved plus18 new),37 archive/74 selected-entry checks, six loaded assembly digests, empty-source framework restore, Release0 warnings/errors and whole scratch formatting. Reviewer13 runner/source negatives and clean0/misformatted2/restored0 new-file format control passed; Python/docs/whitespace/secrets and command/source/log bindings passed. [Detailed result](../../docs/development/https-key-resolver-pipeline-evidence.md) and [source-bound receipt](../../docs/development/evidence/https-key-resolver-pipeline-20261004.json) retain initial failures and source-backed corrections.

Actual KeyResolver metadata403 fallback and non-RequestFailed public-policy interception were executed. Cold guarded403 cases made zero credential/crypto calls before fixed-authority SDK cache priming; supported synthetic401 and later warm positive cases used narrowly expected synthetic credentials with ordinary resource validation. This does not establish production warm-cache failure behavior or real Azure RBAC/credentials/network. Normal diagnostics4/8/4 exposed protected binding fields in memory; off0/0/0 and the fixture sink admitted only closed harmless counts. No production exporter/redaction or universal absence claim follows.

[Proposed ADR-0013](../../architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md) and [reviewed input checklist](../../docs/development/https-key-production-input-checklist.md) remain unselected preparation. Production graph/guards/parser/limits/inventory/witness/lifecycle/composition, protected bindings, full KEY/ARL/live, new priced session and G1–G9/Milestone2 remain open. No production configuration/schema/IaC, Azure resource/grant/spending, public source publication or admission changed. Unchanged product/PostgreSQL regressions were not rerun. All six [human tasks](../../docs/development/https-production-test-packet.md#human-tasks-and-completion-conditions) remain open; prepared board delta reflects completed local substeps. BFF/HTTPS snapshot4346e0f remains stale under the separately rejected publisher proxy-bypass; no retry/bypass and unrelated Cycle14 publication preserved.
