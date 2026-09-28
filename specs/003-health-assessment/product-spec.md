# Feature: IGA health assessment

Status: Draft

Owner: Product owner

Created: 2026-09-28

Last updated: 2026-09-28

## Summary

Provide standalone, recurring health assessment for SailPoint Identity Security Cloud and One Identity Manager. The product evaluates every supported in-scope object or records an explicit gap, combines deterministic and automatic AI analysis, measures adherence to customer-approved desired outcomes, and produces interactive technical and executive results from one canonical assessment.

Phase 1 delivers core assessment, automatic general AI, scoring and maturity, desired outcomes, recurring comparisons, projected ROI, interactive dashboards, governed reports, MCP access, and recommendations. Phase 2 adds on-demand AI deep analysis, realized-benefit tracking, fix packages and task conversion, and verified restore/recovery. Phase 3 adds custom-rule management and anonymous peer benchmarking. Phase 4 adds direct remediation and production migration execution. Licensing analysis and licensing ROI are excluded.

## Problem

IGA environments accumulate configuration drift, excessive access, ineffective governance, fragile customizations, operational failures, and undocumented behavior. Raw inventories and vendor reports do not explain whether the environment achieves its intended outcomes, where risks originate, or how evidence should translate into prioritized action.

Customers and consultants need an assessment that is technically deep, understandable to sponsors, traceable to evidence and authoritative guidance, repeatable over time, and safe for sensitive identity and configuration data. It must distinguish facts from inference, severity from confidence, findings from coverage gaps, and recommendations from approved remediation.

## Goals

- Assess SailPoint and One Identity environments independently of migration.
- Evaluate every supported in-scope object or record an explicit gap state.
- Serve technical practitioners and executives from one canonical assessment.
- Report risks, healthy controls, desired-outcome adherence, scores, maturity, evidence quality, and prioritized recommendations.
- Combine deterministic rules and governed AI analysis with evidence-level traceability.
- Support recurring assessment, immutable history, comparisons, collaboration, controlled publication, and customer acknowledgment.
- Enforce customer isolation, evidence authorization, redaction, retention, deletion, and audit controls across UI, exports, AI, MCP, and benchmarking.

## Non-goals

- Licensing analysis or licensing ROI.
- Direct remediation before Phase 4.
- Production migration execution before Phase 4.
- Custom-rule authoring or activation before Phase 3.
- Peer benchmarking before Phase 3.
- Directly connecting to downstream applications in Phase 1; connected systems are assessed through evidence available in the selected IGA source.
- Treating AI output as confirmed fact, customer risk acceptance, or approved remediation.

## Users and preconditions

- The project contains a supported SailPoint or One Identity evidence baseline produced by the ingestion capability.
- The project identifies authorized consultants, customer reviewers, executives, auditors, evidence viewers, and MCP identities.
- Collection scope, exclusions, redaction, retention, and evidence authorization are configured.
- The assessment profile identifies enabled rules, weights, severity choices, evidence-gap scoring, desired outcomes, AI settings, budgets, report preferences, and notification policy.
- Customer-approved desired outcomes are required for scored adherence; inferred or draft outcomes remain advisory.

## User flows

### Primary flow

1. An authorized project role selects an evidence baseline and assessment profile.
2. The product locks the evidence baseline, rule catalog, profile, weights, desired outcomes, scoring settings, and AI/model versions for the run.
3. Deterministic rules and automatic general AI analyze every supported in-scope object.
4. The run completes with successful results and explicit gaps even when some rules or evidence fail.
5. Consultants and qualified customer reviewers examine proposed findings, evidence, severity, confidence, root causes, and recommendations.
6. Findings are confirmed, rejected, deferred, accepted as risk by the customer, or progressed through remediation states.
7. The product calculates overall, maturity, category, object-type, module, and desired-outcome-adherence results.
8. Users explore interactive dashboards, graphs, evidence side panels, trends, recommendations, and the separate assessment-quality report.
9. A consultant publishes an immutable report version. The customer acknowledgment records delivery and awareness without implying agreement with every finding.
10. Later evidence collections produce new assessment runs, reopen recurring findings, and preserve the full timeline and reason for score changes.

### Alternative flows

- A partial evidence baseline is assessed with explicit gaps and configured score treatment.
- A consultant publishes despite incomplete mandatory reviews or coverage, with prominent warnings.
- A consultant initiates Phase 2 deep analysis for selected objects or categories after previewing scope, data classes, duration, and usage. Protected raw evidence requires separate customer authorization.
- A customer accepts residual risk with an owner, rationale, review date, compensating controls, and authorization.
- In Phase 2, findings become fix packages and tasks in the product or Jira, GitHub, ServiceNow, CSV, or MCP.
- In Phase 3, opted-in customers contribute aggregated metrics to a benchmark cohort containing at least five contributors.

## Functional requirements

### FR-HAS-1: Supported sources and modes

Phase 1 must support standalone consultant-led and customer-operated health assessment for SailPoint Identity Security Cloud and One Identity Manager.

### FR-HAS-2: Universal assessment coverage

Every supported, installed, in-scope object and evidence category must be assessed or carry an explicit state of not applicable, not assessed, insufficient evidence, excluded, inaccessible, redacted, unsupported, or error.

### FR-HAS-3: Assessment domains

Mandatory domains are security and excessive access; governance design; role and entitlement design; Joiner/Mover/Leaver lifecycle; certification or attestation; provisioning and synchronization; request and approval design; configuration and customization; scripts, rules, and code; reliability and operations; performance and scale; data quality and correlation; auditability and compliance. Licensing optimization is excluded.

### FR-HAS-4: SailPoint depth

Phase 1 must assess tenant and security configuration; Sources, connectors, aggregation, correlation, and provisioning; identity profiles, attributes, and transforms; entitlements, Access Profiles, roles, inheritance, and assignments; requests and approvals; certifications; lifecycle workflows; rules and custom logic; operational errors; and evidence completeness.

### FR-HAS-5: One Identity depth

Phase 1 must assess installed modules and configuration; target systems and synchronization; account definitions, manage levels, templates, and IT operating data; native role types and inheritance; IT Shop and approvals; attestation, compliance, separation of duties, risk, and mitigating controls; processes and scripts; schema and customization differences; authorization design; queues, synchronization, processes, audit and API errors; compilation, consistency, transport, and broken-reference health.

### FR-HAS-6: Evaluation context

Applicable objects must be evaluated against customer-approved desired outcomes, vendor-recommended configuration, out-of-the-box behavior, security and engineering guidance, customer policy references, and historical operational evidence.

### FR-HAS-7: Inferred outcomes

When no desired outcome is documented, the product may infer a candidate outcome, label it as inferred, and require consultant review. Desired outcomes use draft, consultant-reviewed, customer-approved, superseded, and retired states. Only customer-approved outcomes affect adherence scoring.

### FR-HAS-8: Finding and control states

Rules must produce pass, finding, not applicable, not assessed, insufficient evidence, excluded, or error. Assessments report healthy controls and strengths as well as actionable findings and informational improvement opportunities.

### FR-HAS-9: Finding content

Each finding must identify title, category, affected objects and outcomes, evidence baseline, exact evidence references, detection method, authoritative rule and version, observed facts, inference, assumptions, severity, confidence, impact, root cause, recommendation options, and validation guidance.

### FR-HAS-10: Severity and confidence

Severity uses Critical, High, Medium, Low, and Informational with documented likelihood and impact. Customers may override severity while retaining the product value. Confidence uses both a percentage and named band and reflects evidence completeness, rule determinism, ambiguity, and model certainty. Severity and confidence remain separately visible even when both affect scoring.

### FR-HAS-11: Deterministic and AI distinction

Deterministic and AI-inferred results must be visibly distinguished. Deterministic findings may auto-confirm except Critical and High findings, which remain proposed until reviewed. AI findings remain proposed until a consultant or qualified customer reviewer confirms them.

### FR-HAS-12: Finding lifecycle

Findings support proposed, confirmed, rejected/false positive, deferred, accepted risk, remediation planned, in progress, remediated pending validation, validated closed, and reopened states. Recurring issues reopen automatically and append to their timeline.

### FR-HAS-13: Review and disposition

Critical and High findings require review, but a consultant may publish with explicit unreviewed warnings. Rejection requires a reason. Customer corrections and rejected findings feed quality metrics without rewriting historical results or automatically changing rules.

### FR-HAS-14: Risk acceptance and exceptions

Only an authorized customer role may accept residual risk. Acceptance requires owner, rationale, review/expiration date, compensating controls, and authorization. Rule suppressions and exceptions are scoped, expiring, attributable, and visible as coverage exceptions.

### FR-HAS-15: Finding editing and collaboration

Authorized reviewers may edit finding presentation and business context while preserving generated originals and history. Findings and recommendations support comments, mentions, owners, documents, links, screenshots, test evidence, policy references, and resolved discussion threads.

### FR-HAS-16: Root-cause grouping

Repeated evidence sharing a root cause must be represented as one finding with multiple affected objects where appropriate. Scoring impact grows linearly with affected objects. A finding may relate to and score against several desired outcomes without being duplicated.

### FR-HAS-17: Overall score and maturity

The product must calculate a 0–100 health score and maturity level: Initial, Developing, Defined, Managed, or Optimized. It must also calculate category, object-type, installed-module, and desired-outcome-adherence results.

### FR-HAS-18: Score configuration

The product provides default category weights that customers may override. Severity determines impact. Confidence may affect impact without changing severity. Passed controls contribute positively. Evidence-gap states use project-configured score treatment.

### FR-HAS-19: Score thresholds

Health status is red below 50, yellow from 50 to below 80, and green at 80 or above.

### FR-HAS-20: Score lifecycle

Proposed, auto-confirmed, confirmed, deferred, accepted-risk, remediation-pending, and reopened findings affect scoring. Rejected findings do not affect the current score but remain in accuracy and audit history. Accepted risk continues reducing health. Remediation changes scores only after new evidence or approved validation confirms it.

### FR-HAS-21: Score transparency and immutability

The product must explain scoring methodology but does not expose every individual mathematical contribution. Product or customer severity used for scoring is project-configurable. Published scores and maturity are frozen; later changes require a new assessment and report version.

### FR-HAS-22: Rule catalog

Vendor rules must be versioned by source product, product version, installed module, and evidence type. Each rule defines purpose, risk, applicability, evidence, logic, result behavior, default severity and weight, sources, recommendations, validation, limitations, and known false-positive conditions.

### FR-HAS-23: Rule history and replay

Rule updates affect new runs only. Historical assessments retain their exact rule version. Users may replay an old evidence baseline against a newer catalog as a separate comparison without replacing the original.

### FR-HAS-24: Vendor-rule governance

Customers may disable or override vendor rules only with reason, authorization, and a visible coverage exception. Conflicting customer and vendor results remain visible. Rules with material quality problems may be disabled for new runs while historical findings remain visible.

### FR-HAS-25: Rule quality

Every vendor rule requires positive, negative, insufficient-evidence, exclusion, and version-compatibility tests. The product owner approves rule-catalog and AI-analysis quality for release.

### FR-HAS-26: Custom-rule phase boundary

Custom-rule authoring, testing, publication, activation, lifecycle, packages, and organization-specific catalogs are deferred to Phase 3. Phase 1 may use customer policies as assessment references but does not publish executable customer rules.

### FR-HAS-27: Out-of-the-box comparison

Where applicable, the assessment must show whether supported out-of-the-box behavior could replace or satisfy customized behavior. It must flag unused, orphaned, duplicated, contradictory, obsolete, unreachable, or unnecessarily customized configuration.

### FR-HAS-28: Relationship, code, and operational analysis

The product must analyze excessive privilege, toxic combinations, circular dependencies, broken inheritance, unexpected indirect access, conflicting automation, security and correctness risks in code, maintainability, error handling, secrets, unsafe operations, unsupported APIs, version compatibility, recurring errors, backlogs, failures, slow work, retry loops, and configuration dependencies.

### FR-HAS-29: Downstream-system boundary

Phase 1 assesses configured target systems and connected applications only through metadata and operational evidence available in the selected IGA source. It does not separately connect to downstream systems.

### FR-HAS-30: General AI analysis

Phase 1 automatic general AI may interpret configuration, infer candidate use cases and outcomes, identify patterns, correlate evidence, explain root causes, draft findings and recommendations, compare customizations with out-of-the-box behavior, and identify missing controls or evidence within project settings and budgets.

### FR-HAS-31: AI evidence and explanations

AI conclusions must cite exact evidence, rules, desired outcomes, and authoritative sources and distinguish fact, inference, assumption, missing context, and suggestion. Conflicting evidence produces uncertainty rather than silent selection.

### FR-HAS-32: AI safety and data policy

Evidence, scripts, logs, comments, rules, and imported content are untrusted input and cannot direct AI tools. AI obeys customer residency, retention, exclusion, redaction, deletion, and authorization policy. Customer evidence is not used to train shared models without explicit opt-in under an approved policy.

### FR-HAS-33: AI budgets and gaps

Projects can configure or disable AI by run, user, category, or period. When a budget is exhausted, analysis completes with an explicit AI-coverage gap. A consultant may override the budget through an audited action.

### FR-HAS-34: Deep-analysis phase boundary

Phase 2 adds consultant-initiated, scoped deep analysis with previewed scope, data classes, cost/usage, and expected duration. Raw evidence is retrieved only when necessary and after separate customer authorization. Deep-analysis output follows the same review and approval workflow.

### FR-HAS-35: Recommendations

Phase 1 findings may include multiple remediation options with outcome, affected scope, prerequisites, proposed changes, roles and approvals, risks, validation, rollback, effort, and authoritative references. Recommendations prefer supported out-of-the-box behavior when it meets the desired outcome.

### FR-HAS-36: Prioritization and effort

Recommendations are prioritized using severity, exposure, affected-object count, dependency criticality, effort, and customer objectives. Consultants may override priority while preserving the original and rationale. Effort uses relative sizing and time ranges with consultant approval.

### FR-HAS-37: Fix-package phase boundary

Phase 2 adds grouped fix packages, quick wins, prerequisites, compensating controls, redesign options, generated review-only artifacts, and configurable conversion to in-product, Jira, GitHub, ServiceNow, CSV, or MCP tasks. A finding becomes remediated only after validation; accepted-risk findings retain their recommendations.

### FR-HAS-38: ROI

Phase 1 projected ROI may use labor, administration, audit, incident, delay, failure/support, remediation/migration, avoided infrastructure, and customer-defined cost or benefit inputs. It excludes licensing. ROI provides conservative, expected, and optimistic scenarios with currency, horizon, evidence, formulas, assumptions, uncertainty, and exclusions. Approved customer inputs preserve original estimates and history.

### FR-HAS-39: Realized benefits

Phase 2 tracks realized benefits against projected outcomes using later assessment evidence.

### FR-HAS-40: Canonical interactive experience

The dashboard must be interactive, uncluttered, and progressively disclosed, with critical KPIs, overall health and maturity, adherence, high-priority risk, category hotspots, recommendations, and changes since prior runs. Side panels preserve navigation context.

### FR-HAS-41: Navigation and customization

Users can navigate and filter by category, outcome, use case, object, module, target system, root cause, fix, severity, confidence, status, rule, owner, reviewer, evidence state, risk, and remediation. Saved views support role-specific defaults, custom graphs, and filters without changing the canonical assessment.

### FR-HAS-42: Relationship and evidence exploration

Pleasant, navigable graphs trace findings through objects, rules, outcomes, tasks, and fixes. Authorized users may open protected raw evidence in a side panel; unauthorized users receive a redacted excerpt or provenance reference.

### FR-HAS-43: Assessment-quality report

Coverage limitations do not dominate the main health narrative. A separate assessment-quality report shows inaccessible, excluded, redacted, unsupported, or errored evidence; rule failures; confidence distribution; unsupported checks; and other assessment limitations. Formal sampling metrics are not required in Phase 1.

### FR-HAS-44: Report contents and audiences

Reports include executive summary, environment and scope, overall score and maturity, category/object/module/adherence results, Critical/High findings, all findings and healthy controls, root causes, recommendations, accepted risks, projected ROI, methodology, rule versions, and technical appendices. Executive, practitioner, auditor, and remediation views derive from the same canonical record.

### FR-HAS-45: Report publication and acknowledgment

Draft reports are visibly marked. A consultant may publish an immutable version, including with incomplete reviews or coverage when warnings are explicit. Customer acknowledgment records recipient, version, time, delivery method, and comments without implying acceptance of every finding.

### FR-HAS-46: Consistent outputs

Dashboard, PDF, Markdown, MCP, and shared views must identify the same assessment version, evidence baseline, rule catalog, scoring profile, and approval state. Exports use requesting-identity redaction and controlled, audited download or expiring/revocable links.

### FR-HAS-47: Recurring assessment

Assessment follows the evidence extraction cadence rather than a separate schedule. Export review and analysis may run on demand. Each run locks all inputs for reproducibility, supports checkpoints and partial completion, and retains successful rule results when other rules fail.

### FR-HAS-48: Reassessment and comparison

Users choose immediate-prior and approved comparison baselines. The product explains changes caused by source configuration, evidence, rules, scoring profiles, desired outcomes, and dispositions. New or worsened Critical/High findings alert early. Recurrence reopens the finding and logs its timeline without a separate regression alert.

### FR-HAS-49: Notifications

Notifications are configurable by severity, category, environment, state, owner, and channel. New rule catalogs generate notifications but do not automatically reassess old evidence.

### FR-HAS-50: Historical evidence availability

Assessments use the general retention policy rather than a separate policy. Historical assessments may remain after underlying evidence expires and must clearly mark unavailable source evidence.

### FR-HAS-51: Accuracy reporting

The pilot target is greater than 80% confirmed among reviewed AI findings. Accuracy is reported overall and, where data exists, by source, category, severity, and rule; deterministic and AI results are not separated. A percentage may display after one reviewed result without a low-sample warning. Indeterminate results are excluded from the confirmation denominator.

### FR-HAS-52: Quality history

The product retains confirmed, rejected, indeterminate, unreviewed, and correction outcomes with originating rule and model versions. It does not require a formal Phase 1 sampling program, AI prompt/model evaluation against a frozen set, an immediate special review for Critical quality failures, or a user-facing quality dashboard.

### FR-HAS-53: Desired-outcome quality

Desired-outcome adherence accuracy is evaluated separately from general finding accuracy.

### FR-HAS-54: Benchmark phase boundary

Phase 3 adds opt-in anonymous benchmarking using aggregated metrics only. Customers opt in separately to contribute and view. Cohorts use source product, target systems, and use cases, contain at least five contributors, and cannot be filtered below five. Customer names, raw evidence, object identifiers, exact configurations, and individual findings are prohibited. Contributions leave future calculations after opt-out; published historical benchmarks remain immutable. Cohort definition, sample size, age, and limitations are not displayed.

### FR-HAS-55: MCP health access

MCP uses the established named-user/service-identity, read-only-default, granular-permission, separation, concurrency, revocation, redaction, and audit model. Permissions separately govern running analysis, reviewing findings, accepting risk, changing rules when Phase 3 enables them, publishing reports, accessing evidence, and converting work to tasks.

### FR-HAS-56: Phase 4 boundary

Direct remediation execution and production migration execution are Phase 4 capabilities requiring separate approved specifications, security review, execution controls, validation, and recovery.

## Permissions

| Actor | Allowed action | Constraints |
|---|---|---|
| Authorized project role | Start a general assessment | Limited to assigned customer/project/environment |
| Consultant | Configure profiles, scoring, rules, outcomes, AI budgets; initiate deep analysis in Phase 2; review findings; publish reports; delete within policy | Cannot accept residual risk; protected raw access still needs customer authorization |
| Qualified customer reviewer | Confirm or refine AI findings | Optional participation; limited to granted scope |
| Customer evidence authorizer | Authorize protected raw-evidence access | Customer role only |
| Customer risk owner | Accept residual risk and acknowledge reports | Risk authority is not delegated to consultants |
| Executive | View summaries and authorized excerpts | No implicit raw-evidence access |
| Auditor | Read approved reports, rules, provenance, dispositions, risk decisions, and audit history | Read-only |
| MCP identity | Perform explicitly granted health actions | Same authority as equivalent UI identity and granular scopes |

All consequential permissions are scoped by customer, project, environment, assessment, action, and evidence category.

## Validation and failure behavior

- Partial evidence and failed rules produce explicit gaps and warned completion rather than false completeness.
- Missing evidence produces an assessment-quality gap, not an unsupported finding.
- Conflicting evidence produces uncertainty and preserves each source.
- AI budget exhaustion completes with an AI-coverage gap unless a consultant overrides the limit.
- Unauthorized raw-evidence access is denied even when deep analysis was consultant-initiated.
- Critical/High proposed findings remain visibly unreviewed; publication is allowed only with explicit warnings.
- Published reports and scores remain immutable.
- A benchmark query that would produce fewer than five contributors is denied.
- Remediation does not change health until validated in a subsequent run or approved test.
- Large lists lazy-load and preserve navigation and filters.

## Edge cases

- A proposed AI finding affects score before human confirmation.
- A report is published with unreviewed Critical findings and incomplete coverage.
- Customer and product severity differ, and project scoring selects either value.
- Accepted risk expires after its source evidence has been deleted.
- A rule is disabled after prior assessments used it.
- One root cause affects many objects and several desired outcomes.
- A customization is safe but unnecessary because out-of-the-box behavior now exists.
- An assessment runs against partial evidence and later receives additional evidence.
- A benchmark cohort falls below five after opt-out.
- A one-review accuracy result displays as 0% or 100% without a low-sample warning.

## User-facing errors

- Evidence baseline, rule catalog, or profile unavailable.
- Rule execution failed or required evidence is insufficient.
- AI budget exhausted or AI disabled for the project.
- Customer raw-evidence authorization required.
- Finding requires review or contains conflicting evidence.
- Report contains incomplete mandatory review or coverage.
- Protected evidence or export denied by authorization/redaction policy.
- Benchmark cohort too small.
- Published report cannot be modified; create a new version.

## Analytics and audit requirements

- Audit assessment runs, locked inputs, rule/profile/scoring changes, AI use, budget overrides, raw authorization, edits, dispositions, risk acceptance, recommendations, task conversion, publication, acknowledgment, exports, sharing, deletion, benchmark opt-in/out, and MCP activity.
- Track assessment coverage, finding states, severity, confidence, desired-outcome adherence, recurring findings, recommendation progress, report publication, and customer acknowledgment.
- Track confirmed, rejected, indeterminate, unreviewed, and correction outcomes and the reviewed-finding denominator.
- Track projected and Phase 2 realized benefits with evidence and approved input changes.

## Accessibility requirements

- All graphs and visualizations require accessible table or text equivalents.
- Dashboards, side panels, filters, dialogs, findings, evidence, reports, and lazy-loaded lists support keyboard and screen-reader operation.
- Severity, status, confidence, and score do not rely on color alone.
- Navigation preserves user context and focus when side panels open or close.

## Security and privacy considerations

- All artifacts inherit customer/project isolation, retention, deletion, residency, and export policy.
- Attachments and future rule packages receive malware and archive-safety checks.
- Findings minimize copied evidence and prefer protected references.
- Users receive warnings before adding sensitive information to free-text fields or attachments.
- Exports and MCP responses apply field-level authorization and redaction at generation time.
- Customer data is not used for shared-model training without explicit approved opt-in.
- Benchmarking requires opt-in, aggregation, a minimum cohort of five, filter enforcement, and prohibition of customer-specific payloads.
- All source content remains untrusted for AI and tool execution.

## Acceptance criteria

### AC-HAS-1: Complete assessment representation

Given a completed supported-source assessment

When the inventory is reviewed

Then every supported in-scope object is assessed or has an explicit gap state.

### AC-HAS-2: Evidence-backed finding

Given a proposed finding

When a reviewer opens it

Then facts, inference, evidence, rule/version, source guidance, severity, confidence, affected outcomes, and recommendations are distinguishable and traceable.

### AC-HAS-3: Critical review state

Given a deterministic or AI-generated Critical or High finding

When it has not received human review

Then it remains proposed and is prominently marked in dashboards and published reports.

### AC-HAS-4: Risk acceptance authority

Given a finding selected for risk acceptance

When a consultant or unauthorized role attempts acceptance

Then the action is denied; an authorized customer role must provide the required ownership and rationale.

### AC-HAS-5: Scoring and maturity

Given a locked assessment run

When scoring completes

Then overall, maturity, category, object, module, and customer-approved desired-outcome results use the saved weights, severity choice, confidence, finding states, passed controls, and gap policy.

### AC-HAS-6: Immutable publication

Given a consultant-published report

When findings or profiles later change

Then the published score and report remain unchanged and a new assessment/report version is required.

### AC-HAS-7: Assessment-quality separation

Given incomplete, inaccessible, excluded, redacted, unsupported, or errored evidence

When the main report and quality report are viewed

Then the main narrative remains health-focused while the separate quality report fully discloses assessment limitations.

### AC-HAS-8: General AI boundary

Given Phase 1 automatic AI analysis

When it creates a conclusion

Then it cites authorized evidence, distinguishes inference from fact, obeys the project budget, and cannot follow embedded evidence instructions.

### AC-HAS-9: Protected evidence

Given a user or AI request for protected raw evidence

When customer authorization or dedicated evidence permission is absent

Then access is denied and the event is audited.

### AC-HAS-10: Recurring finding

Given a validated, rejected, or accepted-risk finding whose issue appears in new evidence

When reassessment completes

Then the finding reopens and records a new timeline event rather than creating an unlinked regression alert.

### AC-HAS-11: Report acknowledgment

Given a published report delivered to a customer

When acknowledgment is recorded

Then recipient, version, time, method, and comments are stored without implying acceptance of each finding.

### AC-HAS-12: Reviewed-finding accuracy

Given at least one reviewed finding

When accuracy is displayed

Then confirmed accuracy uses reviewed non-indeterminate outcomes and may display without a low-sample warning.

### AC-HAS-13: Accessible interactive dashboard

Given an authorized user navigating assessment results

When they open graphs, filters, side panels, evidence, and findings

Then context is preserved, large results lazy-load, and equivalent keyboard-accessible text or tables expose the same material information.

### AC-HAS-14: Phase boundaries

Given a Phase 1 deployment

When a user requests deep analysis, fix-package task conversion, custom-rule management, anonymous benchmarking, direct remediation, or production migration

Then only capabilities enabled for the deployed phase are available and deferred actions are not represented as completed.

## Dependencies

- Approved multi-source ingestion capability and immutable evidence baselines.
- Product/version/module-specific vendor rule catalogs and authoritative-source management.
- Desired-outcome, finding, scoring, report, collaboration, notification, audit, and evidence-authorization models.
- Automatic AI analysis with prompt-injection resistance, budget enforcement, provenance, redaction, and model/version recording.
- Interactive dashboard, graph, side-panel, export, controlled sharing, and accessibility capability.
- MCP identity and granular authorization model.
- Phase 2 deep-analysis, task integration, and restore/recovery designs.
- Phase 3 rule-authoring and benchmark privacy/statistical designs.
- Separate Phase 4 remediation and production-migration specifications.

## Open questions and assumptions

- **Blocking production-readiness conflict:** approved `NFR-COM-2` requires a verified restore procedure before production readiness, while discovery defers verified restore/recovery to Phase 2. Phase 1 cannot be declared production-ready unless this is reconciled.
- Displaying accuracy after one review without a low-sample warning can imply unsupported statistical confidence; this remains an accepted product decision requiring report-language review.
- Confidence affects score while severity remains separately visible; the exact formula belongs to technical/scoring design and must avoid concealing severe findings.
- Cohort metadata is hidden even though benchmark quality depends on cohort context; Phase 3 requires privacy and statistical review before approval.
- General AI prompt/model changes do not require evaluation against a frozen test set under current discovery decisions; release governance must still satisfy the product-owner quality gate.
- Phase 1 projected ROI is included; only realized-benefit tracking is deferred to Phase 2.
- Phase 1 recommendations do not include Phase 2 fix-package grouping or task conversion.

## Approval

Approved by:

Date:

Only the product owner may change `Status` to `Approved`.
