# Product-Wide Functional Requirements

Status: Approved
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

These requirements express observable product behavior at discovery level. Feature specifications will refine them into detailed acceptance criteria. Priority and release assignment remain subject to product-owner approval.

## Initial delivery scope

- First migration source: SailPoint Identity Security Cloud
- First health-assessment pilot source: One Identity Manager 10.x on SQL Server through a dedicated read-only database account
- First destination: Veza
- Subsequent intended destination: One Identity Manager
- Initial use-case order: roles, access profiles, certifications, then Joiner/Mover/Leaver workflows
- Initial SailPoint evidence: configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions
- Operating models: professional-services-led managed delivery and customer-operated use
- Customer-operated emphasis: health assessment
- Migration execution modes: direct destination configuration and reviewed migration-package generation
- Equivalence criteria: approved business/governance behavior and meaningful configuration similarity
- Optional customer-side collection for engagements with stricter data boundaries
- Prohibited evidence: source credentials and passwords
- Protected evidence: Social Security numbers and government identifiers remain customer-side or are redacted
- Approval exceptions: read-only collection, documentation generation, health analysis, and migration-package generation only
- Source safety: SailPoint remains read-only during every phase
- Health-pilot operation: consultant-operated, with customer evidence authorization, review, acknowledgment, and residual-risk authority

## Engagement and governance

### FR-ENG-1 — Engagement boundaries

The product must allow authorized users to define an engagement's source, destination, environments, scope, owners, and applicable constraints.

### FR-ENG-2 — Roles and approvals

The product must distinguish who may collect, analyze, edit documentation, approve scope, approve destination changes, accept risk, execute migration, and accept validation results.

### FR-ENG-3 — Audit history

The product must retain an attributable history of material evidence, decisions, approvals, changes, executions, and validation results.

### FR-ENG-4 — Supported operating models

The product must support authorized use by consulting or destination-vendor professional services teams and customer-operated use without weakening engagement boundaries, approvals, or auditability.

### FR-ENG-5 — Suggestion, refinement, and approval chain

The product must distinguish AI-generated suggestions, consulting-team refinements, and customer decisions so that suggestions cannot be mistaken for approved scope or change authority.

### FR-ENG-6 — Customer-configured approval policy

The product must allow an authorized customer to omit per-action customer approval for read-only collection, documentation generation, health analysis, and migration-package generation. Any exception must identify its owner, scope, conditions, effective period, and audit history.

Direct destination configuration and data movement must continue to require customer approval.

### FR-ENG-7 — Partner and customer tenancy

The product must support multiple consulting partners and multiple customer organizations concurrently. Partner, customer, engagement, evidence, task, report, approval, and execution data must remain within their authorized boundaries.

### FR-ENG-8 — Customer data-policy configuration

Authorized customer owners must be able to configure applicable data residency, retention, deletion, and export policies, subject to supported service and legal constraints.

The retention policy must support a customer-configured start event. Initial supported data residency is the United States.

### FR-ENG-9 — Partner and customer administrator boundaries

A partner administrator may access multiple customer organizations only when explicitly assigned to them. A customer administrator must remain restricted to its own customer organization and authorized engagements.

## Collection

### FR-COL-1 — SailPoint source collection

The product must collect authorized, in-scope evidence from SailPoint Identity Security Cloud and identify the source and collection time of each item.

### FR-COL-2 — Coverage visibility

Before and after collection, the product must show which evidence categories are supported, collected, incomplete, inaccessible, or unsupported.

### FR-COL-3 — Relevant artifact inventory

The product must inventory supported source settings, governance artifacts, data, roles, access profiles, manually created artifacts, scripts or code snippets, and operational queue/event/log evidence relevant to the engagement.

### FR-COL-4 — Refresh and comparison

The product must distinguish separate collection runs and allow users to identify relevant changes or staleness in the source evidence.

### FR-COL-5 — Provenance

The product must preserve a traceable relationship between every interpreted finding and the source evidence that supports it.

### FR-COL-6 — Customer-side and redacted evidence

The product must support engagement rules that keep designated evidence categories customer-side or redact designated fields before evidence is made available outside the approved customer boundary.

### FR-COL-7 — Prohibited secret collection

The product must prevent source credentials and passwords from entering collected evidence, analysis datasets, generated documents, reports, or ordinary logs.

### FR-COL-8 — Protected government identifiers

Social Security numbers and other government identifiers must remain within the customer-controlled boundary or be redacted before approved evidence leaves that boundary.

### FR-COL-9 — Optional customer-side collection

The product must allow a customer to choose a customer-side collection mode so protected source evidence can be processed within the customer-controlled environment.

### FR-COL-10 — Read-only SailPoint access

The product must not create, update, or delete SailPoint data or configuration during collection, analysis, migration, validation, retry, or recovery activity.

## Documentation and visualization

### FR-DOC-1 — Current-state model

The product must organize collected evidence into understandable current-state capabilities, use cases, relationships, dependencies, and exceptions.

### FR-DOC-2 — Graphical visualization

The product must provide graphical views that help users understand relevant IGA use cases and relationships without requiring them to interpret raw source exports.

### FR-DOC-3 — Generated delivery artifacts

The product must be able to draft functional requirements, non-functional requirements, acceptance criteria, definitions of done, and delivery stories from supported findings.

### FR-DOC-4 — Human refinement

Authorized users must be able to correct, enrich, approve, reject, or defer generated interpretations and artifacts without losing their source traceability.

### FR-DOC-5 — Export or handoff

The product must provide a usable way to hand reviewed documentation and delivery artifacts to stakeholders or their established work-management process. The required formats and integrations remain open.

### FR-DOC-6 — Canonical product documentation

The product must be the canonical maintained home for discovery, health, migration, and validation documentation. PDF and Markdown outputs must be generated from the canonical product record and must identify their source version and generation time.

### FR-DOC-7 — Required visualization views

The product must provide process flows, relationship graphs, data-flow diagrams, coverage maps, dependency views, and migration-status dashboards, with traceability to the underlying canonical objects and evidence.

## Scope decisions

### FR-SCP-1 — Disposition decisions

For each candidate capability or use case, the product must allow an authorized product owner to record whether it will be preserved, improved, replaced, retired, or deferred.

### FR-SCP-2 — Decision rationale

The product must retain the owner, rationale, date, dependencies, and unresolved conditions for scope decisions.

### FR-SCP-3 — Approved migration baseline

The product must distinguish draft findings from the approved set of future-state outcomes used to prepare migration.

## Health assessment

### FR-HLT-1 — Assessment checks

The product must evaluate supported evidence against identifiable software-development, security, configuration, and operational checks.

### FR-HLT-2 — Finding quality

Each health finding must include supporting evidence, affected scope, category, severity or impact, confidence, and a recommended correction or investigation path.

### FR-HLT-3 — Operational error analysis

Where supported and authorized, the product must identify and summarize relevant errors or unhealthy patterns in queue, event, or log evidence.

### FR-HLT-4 — Finding disposition

Qualified users must be able to confirm, reject, defer, remediate, or accept the risk of a finding, subject to their authority.

### FR-HLT-5 — Findings influence future state

Confirmed findings must not silently change migration scope; approved remediations must be reflected in the reviewed future-state requirements.

### FR-HLT-6 — Standalone customer health assessment

The product must allow an authorized customer to complete and review a health assessment without requiring that customer to initiate or purchase a full migration workflow.

### FR-HLT-7 — Universal collected-object assessment

The product must include every supported, in-scope object and evidence category in the health assessment regardless of whether it is selected, excluded, deferred, or otherwise outside migration scope. Each item must be assessed or carry an explicit incomplete, inaccessible, unsupported, excluded, or not-yet-assessed state. Migration disposition must not suppress applicable findings.

### FR-HLT-8 — Applicable assessment sources

The product must evaluate evidence against all applicable authoritative sources, including relevant SailPoint and Veza guidance, recognized security guidance such as OWASP or CIS where applicable, software-development best practices, and customer-supplied rules or standards. Each finding must identify the source or rule that supports it.

### FR-HLT-9 — Finding severity and confidence

Each finding must include a severity of Critical, High, Medium, Low, or Informational and an AI confidence score or band. Confidence must not replace evidence or human review.

### FR-HLT-10 — Complete current-state report

The health assessment must produce a sponsor-readable report describing the current IGA environment, inventory and relationships, assessment coverage and gaps, findings, recommendations, dependencies, residual risks, and prioritized next actions.

### FR-HLT-11 — ROI analysis

Later health releases must include evidence-based ROI analysis for relevant recommendations, remediation, or migration options. ROI must expose its inputs, assumptions, time horizon, confidence, and material exclusions rather than presenting unsupported precision. ROI is not a health-pilot exit requirement.

### FR-HLT-12 — Fix packages

The product must prepare reviewable fix packages for supported findings. Each package must identify the finding addressed, proposed change, expected benefit, prerequisites, risks, approval needs, validation, rollback or recovery implications, and items requiring human execution.

### FR-HLT-13 — Health-only consultant tasks

When an engagement is configured as health-assessment-only, authorized users must be able to convert selected fix packages into traceable consultant tasks without enabling migration or direct destination changes.

### FR-HLT-14 — ROI dimensions

When later ROI capability is enabled and evidence is available, analysis must address labor/time savings, licensing optimization, reduced audit effort, risk reduction, avoided incidents, migration cost, and remediation cost. Unsupported dimensions must be identified rather than estimated without a basis. ROI is excluded from the pilot.

### FR-HLT-15 — Health-report outputs

The product must provide consistent health-assessment results through interactive dashboards, PDF exports, and Markdown exports suitable for agent and automation integrations.

### FR-HLT-16 — Finding confirmation and risk acceptance

The product must distinguish AI-proposed findings, consultant-confirmed or refined findings, and customer residual-risk decisions. AI output alone must not constitute finding confirmation or risk acceptance.

### FR-HLT-17 — No initial direct remediation

Initial health-assessment phases must not execute remediation directly. They may prepare recommendations, fix packages, and consultant tasks. Customer-approved direct remediation remains a later roadmap capability requiring separate specification and approval.

### FR-HLT-18 — Configurable evidence history

The customer or authorized engagement owner must be able to configure the queue, event, audit, and error-log lookback period used for health assessment, subject to source availability and data-boundary policy.

### FR-HLT-19 — One Identity pilot boundary

The health pilot must validate consultant-operated One Identity Manager 10.x on SQL Server using a dedicated read-only database account and a versioned capability matrix. It must cover the approved foundation and module set, inventory other installed modules, and represent unsupported semantic analysis as explicit gaps.

### FR-HLT-20 — Pilot sub-phases

Health Phase 1 is delivered through evidence foundation, rules/findings/scoring, review/reporting/evaluation, and read-only MCP sub-phases. MCP must not run assessments, disposition findings, accept risk, publish reports, access raw evidence, or create tasks during the pilot.

### FR-HLT-21 — Pilot fix packages and tasks

The pilot must support grouped fix packages, in-product consultant tasks, and CSV export. Review-only SQL, scripts, configuration examples, and validation steps must never be executed by the product. Jira, GitHub, ServiceNow, and MCP task creation are deferred.

### FR-HLT-22 — Pilot scoring and quality

Proposed AI findings affect only a provisional score. Published scores use reviewed findings and deterministic auto-confirmed results. Evidence gaps reduce a separate assessment-quality measure rather than the default health score. Maturity uses a separate evidence-based rubric.

### FR-HLT-23 — Pilot evaluation

Pilot acceptance requires at least two independent eligible One Identity Manager 10.x environments, an initial assessment and reassessment, review of every Critical and High AI finding, and a module/category/severity-stratified sample totaling at least 100 reviewed AI findings or all findings when fewer exist. More than 80% of reviewed non-indeterminate AI findings must be confirmed.

## Mapping and migration

### FR-MAP-1 — Destination-specific mapping

The product must map approved source outcomes to supported Veza or One Identity Manager capabilities without implying equivalence where none exists.

### FR-MAP-2 — Gap and ambiguity handling

The product must surface unsupported mappings, one-to-many mappings, loss of behavior, conflicts, and decisions requiring expert review.

### FR-MAP-3 — Outcome-equivalence assessment

The product must evaluate proposed destination mappings against approved business/governance behavior and meaningful configuration similarity, and must report where either form of equivalence cannot be demonstrated.

### FR-MAP-4 — SailPoint role coverage

For the initial roles slice, the product must collect and represent supported role definitions, descriptions, owners, membership criteria, access profiles, entitlements, hierarchy or inheritance, assignments, and approval/governance settings.

### FR-MAP-5 — Veza Access Profile proposal

The product must propose a Veza Access Profile that preserves the approved SailPoint role's effective entitlements and mapped owners. The visible inheritance structure may differ when the effective entitlement outcome is preserved. Unsupported or ambiguous differences must remain visible.

### FR-MAP-6 — Static role membership

The product must propose adding members found as static SailPoint role members to the mapped Veza Access Profile, subject to identity matching, consulting refinement, and customer approval policy.

### FR-MAP-7 — Criteria-based role membership

When SailPoint role membership is derived from membership criteria rather than static assignment, the product must preserve the criteria and relevant evidence as a health-assessment finding. It must not silently flatten dynamic membership into a static member list or convert the criteria into a Joiner/Mover/Leaver migration requirement.

### FR-MAP-8 — Configurable identity matching

The product must support identity matching using email, employee ID, account ID, and engagement-configured matching rules. It must expose the rule and evidence used for each proposed match.

When matching signals conflict or produce ambiguity, the product must create a consultant task with an automated suggested resolution and must not select a match silently.

### FR-MAP-9 — Unmatched-reference tasks

When an owner, member, entitlement, or source cannot be matched, the product must create the Veza Access Profile with a visible warning and create a task with an automated suggested resolution for consultant review. The warning and task must retain the unmatched source evidence and remain traceable to the created profile.

### FR-MAP-10 — Existing destination profile decision

When a matching Veza Access Profile already exists, the product must require a customer decision before updating, merging, replacing, or otherwise changing it.

### FR-MAP-11 — Access Profile mapping

For the access-profile slice, the product must map a SailPoint Access Profile to a Veza Access Profile while retaining traceability to its approved source evidence and effective entitlements.

The mapping must cover name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings, while making unsupported or non-equivalent fields visible.

### FR-MAP-12 — Source-to-Integration mapping

When a SailPoint Access Profile references a Source, the product must map the Source to the corresponding Veza Integration using name, system type, and connection metadata. Missing, ambiguous, or conflicting Integration matches must remain visible for consultant review.

### FR-MAP-13 — Existing Access Profile decision

When a matching Veza Access Profile already exists for a SailPoint Access Profile, the product must require a customer decision before updating, merging, replacing, or otherwise changing it.

### FR-MAP-14 — Missing or ambiguous Integration

When a Veza Integration match is missing, ambiguous, or conflicting, the product must still create the approved Access Profile with a visible warning, automated suggested resolution, and consultant task linked to the source evidence and destination profile.

## Consultant task management

### FR-TSK-1 — In-product consultant tasks

The product must maintain consultant tasks with status, assignee, source evidence, affected migration object, automated suggestion, human decision, and resolution history.

### FR-TSK-2 — Task handoff

The product must support both in-product task management and external handoff. Initial handoff targets include Jira, GitHub, ServiceNow, and CSV; exact integration depth is defined per supported connector or export.

### FR-TSK-3 — Task traceability

External and internal task representations must retain a stable relationship to the originating warning, evidence, migration object, and later resolution.

### FR-MIG-1 — Change preview

Before destination modification, authorized users must be able to review the proposed configuration and data changes, dependencies, expected outcomes, and known risks.

### FR-MIG-2 — Explicit execution approval

The product must not perform direct destination configuration or data movement without an authorized customer approval tied to the defined change set. Approval exceptions do not authorize these operations.

The product must record the customer approval that permitted execution; absence of required approval must block execution.

### FR-MIG-3 — Destination configuration

The product must configure approved, supported capabilities in Veza or One Identity Manager and record the result of each attempted change.

### FR-MIG-4 — Controlled data movement

Where approved outcomes require data that the destination cannot obtain directly, the product must support authorized movement of supported data from the source or reviewed migration dataset.

### FR-MIG-5 — Safe repetition and recovery

The product must make repeated, resumed, failed, and partially completed migration activity visible and prevent silent duplicate or conflicting outcomes.

### FR-MIG-6 — Manual-action support

When an operation cannot be automated, the product must identify the required manual action and allow resulting evidence or completion to be recorded.

### FR-MIG-7 — Direct and packaged execution

For supported changes, the product must allow an authorized user to choose between direct destination configuration and generation of a versioned, reviewable migration package for separate execution.

### FR-MIG-8 — Package outcome reconciliation

When a migration package is used, the product must preserve its relationship to the approved change set and allow actual execution results and resulting destination state to be reconciled with it.

### FR-MIG-9 — Customer-approved warnings

Non-fatal unresolved mapping warnings must not prevent direct configuration when the customer explicitly approves proceeding with the warnings. The approval must identify the warnings accepted and remain part of the migration evidence.

### FR-MIG-10 — Access Profile warning approval

Non-fatal Access Profile warnings, including unresolved Integration matches, may proceed to configuration when explicitly approved by the customer. Fatal entitlement mismatches cannot be accepted as a completed migration outcome.

## Validation

### FR-VAL-1 — Criteria-based validation

The product must relate destination validation to approved acceptance criteria and intended outcomes.

### FR-VAL-2 — Evidence status

Each validation must be reported as passed, failed, or not verified, with evidence and execution context.

### FR-VAL-3 — Reconciliation

The product must identify material differences among approved outcomes, attempted destination changes, observed destination state, and validation results.

### FR-VAL-4 — Acceptance control

Only an authorized owner may accept migration scope as complete; incomplete or failed validation must remain visible.

### FR-VAL-5 — Role validation evidence

Role validation must evaluate the destination Access Profile's existence, effective entitlements, owner, static members, and effective inheritance outcome against the approved migration baseline.

### FR-VAL-6 — Explainable role grade

The product must calculate an explainable role grade from five equally weighted components: destination Access Profile existence, effective entitlements, owner, static members, and effective inheritance outcome.

The grade must be displayed as a percentage plus a red/yellow/green status and must identify component results and missing evidence. Red is below 50%, yellow is 50% through 80%, and green is above 80%.

### FR-VAL-7 — Customer acceptance of graded mismatch

No individual role-validation mismatch is automatically fatal. The customer must be able to accept or reject any graded outcome, and the decision, score, component mismatches, rationale, and owner must remain auditable.

### FR-VAL-8 — Access Profile validation

Access Profile validation must evaluate five equally weighted components: destination Access Profile existence, mapped Veza Integration, entitlements, owner, and configuration settings.

### FR-VAL-9 — Access Profile grade

The Access Profile validation result must use the same explainable percentage, red/yellow/green status, component evidence, and customer acceptance record as role validation.

### FR-VAL-10 — Fatal Access Profile entitlement mismatch

An Access Profile entitlement mismatch is fatal to acceptance regardless of the aggregate grade. The product must identify the mismatched entitlements and prevent final acceptance until they match or an authorized product-scope change removes them from the approved baseline.

## Certification coverage

### FR-CER-1 — Supported campaign types

The certification capability must discover, document, migrate, and validate every certification campaign type supported by the applicable source and destination, including identity, access-item, role, and application or Source-owner campaigns where supported. Unsupported types or semantics must remain explicit.

### FR-CER-2 — Access Review mapping

The product must map a SailPoint certification campaign to a Veza Access Review.

### FR-CER-3 — Campaign settings coverage

The Access Review proposal must cover the source campaign's name, description, type, scope, reviewers, schedule, deadlines, reminders, escalation, decision options, and remediation settings, while identifying unsupported or non-equivalent behavior.

### FR-CER-4 — Future campaigns only

Migration must include campaign settings and future campaign definitions. Completed campaign history and prior reviewer decisions must not be migrated as destination Access Review history.

### FR-CER-5 — In-flight campaign round remains in SailPoint

An active or otherwise in-flight SailPoint campaign round and its decisions must not be migrated, interrupted, or modified. The active round must remain on SailPoint for completion, while its campaign settings remain eligible for migration.

### FR-CER-6 — Fatal scope and reviewer mismatch

A mismatch in Access Review scope or reviewers is fatal to acceptance regardless of aggregate validation score. Final acceptance must remain blocked until the mismatch is resolved or an authorized product-scope change updates the approved baseline.

### FR-CER-7 — Certification validation components

Certification validation must evaluate Access Review existence/type, scope, reviewers, schedule/timing, and decision/remediation settings.

### FR-CER-8 — Certification validation weighting

Certification validation must weight existence/type at 10%, scope at 30%, reviewers at 30%, schedule/timing at 15%, and decision/remediation settings at 15%. The weights and component evidence must be visible in the grade explanation.

### FR-CER-9 — Existing Access Review decision

When a matching Veza Access Review already exists, the product must require a customer decision before updating, merging, replacing, or otherwise changing it.

### FR-CER-10 — Future campaign selection

Future campaign migration must support reusable campaign templates and scheduled-but-not-started campaigns. The customer must be able to exclude selected campaign objects from the approved migration scope, with the exclusion and owner recorded. Excluded objects must remain documented and included in applicable health assessment.

### FR-CER-11 — In-flight campaign warning

In-flight SailPoint campaigns must appear in migration reporting with a warning that the active round must finish in SailPoint before the corresponding Veza Access Review is triggered. The warning must not create a consultant task unless a separate issue is identified.

### FR-CER-12 — Certification grade presentation

Certification validation must use the standard percentage and red/yellow/green thresholds: red below 50%, yellow from 50% through 80%, and green above 80%.

## Joiner/Mover/Leaver coverage

### FR-JML-1 — Veza LCM Workflow mapping

The product must map approved SailPoint Joiner, Mover, and Leaver behavior to Veza Workflows within the LCM module while preserving traceability to the source definitions and approved lifecycle outcomes.

### FR-JML-2 — All lifecycle workflows

The product must discover and assess every SailPoint lifecycle workflow, including customized workflows, for supported mapping to Veza LCM Workflows.

### FR-JML-3 — Workflow mapping coverage

The workflow mapping must cover triggers, identity criteria, actions, approvals, and timing while identifying unsupported or non-equivalent behavior.

### FR-JML-4 — Role criteria remain health-assessment findings

Criteria-derived SailPoint role membership must remain in health-assessment scope and must not become a Veza LCM Workflow migration requirement solely because the role uses membership criteria.

### FR-JML-5 — In-flight workflow execution

For an in-flight SailPoint lifecycle workflow, definitions and settings may migrate while the active execution remains in SailPoint for completion. The corresponding Veza Workflow must not be triggered until the SailPoint execution finishes, and the condition must appear in health assessment and migration reporting.

### FR-JML-6 — Unsupported referenced system

If a lifecycle workflow references a system unsupported by Veza, the mismatch is fatal to workflow acceptance. The product must log the unsupported system, preserve the affected workflow evidence, and include the issue in the health assessment.

### FR-JML-7 — Workflow validation grade

LCM workflow validation must weight destination Workflow existence at 25%, triggers/identity criteria at 25%, and actions at 50%. Component evidence and missing information must remain visible.

### FR-JML-8 — Existing Workflow decision

When a matching Veza Workflow already exists, the product must require a customer decision before updating, merging, replacing, or otherwise changing it.

### FR-JML-9 — Custom script and rule treatment

For each custom script or rule used by a SailPoint lifecycle workflow, the product must generate an automated suggested Veza replacement, create a traceable consultant task, and include the item and its evidence in the health assessment.

### FR-JML-10 — Customer workflow exclusions

The customer must be able to exclude selected lifecycle workflows from migration. Excluded workflows must remain documented and included in health assessment, with the exclusion and owner recorded.

### FR-JML-11 — Workflow grade thresholds

LCM workflow validation must use the standard percentage and red/yellow/green thresholds: red below 50%, yellow from 50% through 80%, and green above 80%.

### FR-JML-12 — Non-fatal workflow mismatches

A referenced system unsupported by Veza is the only automatically fatal JML mismatch. Every other mismatch must produce a visible warning, explanation, and traceable consultant task with relevant evidence and an automated suggested resolution where supported.

## Reporting and traceability

### FR-TRC-1 — End-to-end traceability

The product must support navigation from source evidence through current-state interpretation, approved requirement, migration action, destination state, and validation evidence.

### FR-TRC-2 — Progress and exceptions

Authorized users must be able to view progress, blockers, decisions awaiting action, exceptions, and remaining validation across the five product phases.

### FR-TRC-3 — Reproducible reports

Generated reports must identify their engagement, scope, source evidence version, generation time, and review/approval state.

## Product success measurement

### FR-MET-1 — Discovery coverage

The product must measure and report discovery coverage against the approved collection scope, distinguishing assessed, incomplete, inaccessible, unsupported, excluded, and not-yet-assessed items. Every supported, in-scope object must be represented; no pilot percentage target is currently defined.

### FR-MET-2 — Finding accuracy

The product must measure finding accuracy using consultant-reviewed dispositions and must report confirmed, rejected, indeterminate, and unreviewed findings separately. The pilot target is greater than 80% confirmed among consultant-reviewed AI findings; unreviewed findings must not be counted as confirmed or omitted from reporting.

## Unresolved scope

The following are not yet requirements because product decisions are missing:

- tenancy and commercial account model;
- exact SailPoint evidence categories available in the first release;
- exact Veza and One Identity Manager configuration coverage;
- supported export and work-management integrations;
- remediation automation beyond migration configuration;
- ongoing assessment after migration;
- source/destination version support and deployment models.
