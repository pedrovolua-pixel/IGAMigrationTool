# Product Capability Map

Status: Approved
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

This map organizes intended capabilities by the five phases named by the product owner. It is not a delivery commitment or technical decomposition.

## Cross-cutting foundation

- Engagement, environment, and scope management
- Multi-partner and multi-customer organization tenancy
- Strict partner/customer/engagement/environment isolation
- Assigned-customer access for partner administrators and single-organization scope for customer administrators
- Customer-configurable residency, retention, deletion, and export policies
- 30-day default retention policy
- Configurable retention-clock start event
- Soft deletion at retention expiry with controlled recovery and later permanent purge
- United States data residency for the initial product
- Users, roles, approvals, and separation of duties
- Secure connection/access management
- Optional customer-side collection mode
- Credential/password exclusion and protected-identifier redaction
- Enforced read-only SailPoint interaction
- Evidence provenance and version history
- Audit trail
- Traceability across phases
- Progress, decisions, blockers, and exception handling
- Reporting and controlled export

## Phase 1 — Collection

- SailPoint Identity Security Cloud evidence collection
- Collection-scope preview and authorization
- Inventory of supported settings and governance artifacts
- SailPoint configuration and Sources inventory
- Entitlement inventory
- Transforms, rules, and script collection
- Workflow-definition collection
- Evidence-category and field redaction/customer-side handling
- Coverage, permission, completeness, freshness, and error reporting
- Repeat collection and change comparison

## Phase 2 — Documentation and review

- Current-state use-case reconstruction
- Object, dependency, and relationship exploration
- Graphical visualizations with accessible alternatives
- Process-flow, relationship-graph, data-flow, coverage-map, dependency, and migration-status views
- Canonical in-product documentation with versioned PDF and Markdown exports
- Draft functional and non-functional requirements
- Draft acceptance criteria and definitions of done
- Draft delivery stories
- Analyst correction and enrichment
- Explicit AI-suggestion and consulting-refinement states
- Product-owner disposition: preserve, improve, replace, retire, or defer
- Customer-configured approval rules and exceptions
- Reviewed and approved migration baseline

## Phase 3 — Health assessment

- Pilot sub-phase 1A: consultant-operated One Identity Manager 10.x SQL Server evidence foundation using approved read-only database queries
- Pilot sub-phase 1B: deterministic and automatic AI assessment, findings, scoring, maturity, recommendations, grouped fix packages, and in-product consultant tasks with CSV export
- Pilot sub-phase 1C: review, reassessment, governed publication, dashboard, PDF, Markdown, accessibility, and accuracy evaluation
- Pilot sub-phase 1D: read-only MCP access to authorized assessment results and protected evidence references
- Pilot exclusions: ROI, direct remediation, production migration, custom-rule authoring, benchmarking, and external task creation
- Customer-operated standalone health-assessment workflow
- Assessment of every collected object and supported evidence category regardless of migration selection
- Findings retained for excluded, deferred, and non-migrated objects
- Software-development-practice checks for supported scripts/configuration
- Security-risk checks
- Non-optimal use-case and configuration detection
- Queue, event, and log error analysis
- Applicable vendor, security, software-development, and customer rule sources
- Evidence, severity/impact, confidence, and remediation advice
- Reviewer disposition and risk ownership
- Approved remediation reflected in future-state requirements
- Sponsor-readable current-state and health report
- Evidence-based ROI with transparent assumptions and confidence after the pilot
- Later ROI across labor/time, licensing, audit effort, risk, avoided incidents, migration cost, and remediation cost
- Interactive dashboards plus PDF and Markdown exports
- Stable Markdown identifiers for agent integrations
- Reviewable fix packages
- Configurable conversion of fix packages into consultant tasks for health-only engagements
- AI proposal, consultant confirmation/refinement, and customer residual-risk acceptance
- Configurable queue, event, audit, and error-log history
- Direct remediation deferred to a later roadmap phase

## Phase 4 — Migration

- Approved-outcome mapping to Veza as the first destination
- Ordered initial coverage: roles, access profiles, certifications, then Joiner/Mover/Leaver workflows
- Business/governance behavior and configuration-similarity comparison
- SailPoint role definition, ownership, criteria, hierarchy, assignments, and governance-setting analysis
- Veza Access Profile proposal preserving effective approved entitlements and mapped owners
- Static-member mapping to the destination Access Profile
- Identity matching by email, employee ID, account ID, and configurable rules
- Warning and consultant-task creation for unmatched owners, members, entitlements, or sources
- Consultant tasks with automated suggestions for ambiguous or conflicting identity matches
- In-product task management and Jira, GitHub, ServiceNow, and CSV handoff
- Health-assessment-only treatment for criteria-derived role membership
- Customer decision gate for existing matching Veza Access Profiles
- Customer-approved continuation with non-fatal unresolved warnings
- SailPoint Access Profile to Veza Access Profile mapping
- Access Profile name, description, owner, Source, entitlement, requestability, approval-setting, and revoke-setting coverage
- SailPoint Source to Veza Integration matching by name, system type, and connection metadata
- Warning, automated suggestion, and consultant task for missing or ambiguous Integrations
- Customer decision gate for existing Access Profiles
- Customer-approved continuation for non-fatal Access Profile warnings
- Approved-outcome mapping to One Identity Manager in subsequent scope
- Gap, ambiguity, and unsupported-concept review
- Proposed destination-change preview
- Dependency and execution sequencing
- Explicit customer approval for direct configuration and data movement
- Choice of direct configuration or reviewed migration-package generation
- Supported direct destination configuration
- Versioned migration package and handoff evidence
- Controlled movement of approved data where required
- Manual-action guidance for unsupported automation
- Execution audit, partial-failure handling, safe retry, and recovery

## Phase 5 — Validation

- Acceptance-criteria-derived validation plan
- Destination-state inspection
- Behavioral/outcome validation where supported
- Source-to-approved-to-destination reconciliation
- Passed, failed, and not-verified evidence
- Equally weighted role grade across Access Profile existence, effective entitlements, owner, static members, and effective inheritance outcome
- Percentage plus accessible red/yellow/green status and component evidence
- Access Profile grade across existence, Integration, entitlements, owner, and configuration settings
- Fatal acceptance gate for Access Profile entitlement mismatches
- Customer acceptance or rejection of graded outcomes
- No automatically fatal role-validation mismatch

## Certification coverage

- Identity certification campaigns
- Access-item certification campaigns
- Role certification campaigns
- Application or Source-owner certification campaigns
- Additional campaign types supported by the source and destination
- Explicit unsupported-type and semantic-gap reporting
- SailPoint certification campaign to Veza Access Review mapping
- Name, description, type, scope, reviewer, schedule, deadline, reminder, escalation, decision-option, and remediation-setting coverage
- Future campaign and settings migration only
- Reusable template and scheduled-but-not-started campaign support
- Customer-selected campaign-object exclusions that remain documented and health-assessed
- Explicit exclusion of completed campaign history and reviewer decisions
- In-flight rounds retained on SailPoint while their settings migrate
- Warning that blocks destination triggering until the SailPoint round finishes, without creating a consultant task
- Customer decision gate for existing Veza Access Reviews
- Weighted certification grade across existence/type, scope, reviewers, schedule/timing, and decision/remediation settings
- Certification weights: 10% existence/type, 30% scope, 30% reviewers, 15% schedule/timing, 15% decision/remediation
- Standard red/yellow/green grade thresholds
- Fatal acceptance gates for Access Review scope and reviewer mismatches

## Joiner/Mover/Leaver coverage

- All SailPoint lifecycle workflows, including custom workflows
- SailPoint lifecycle behavior to Veza Workflow mapping
- Veza LCM module as the destination capability
- Trigger, identity-criteria, action, approval, and timing coverage
- Role membership criteria excluded from LCM migration and retained in health assessment
- In-flight definitions/settings migration while active executions finish in SailPoint
- Destination trigger hold until the source execution finishes
- Fatal acceptance gate, logging, and health finding for systems unsupported by Veza
- Validation weights: 25% Workflow existence, 25% triggers/identity criteria, 50% actions
- Standard red/yellow/green thresholds
- Warning, explanation, and consultant task for every non-fatal mismatch
- Customer decision gate for existing Veza Workflows
- Automated replacement suggestion, consultant task, and health finding for custom scripts/rules
- Customer-selected workflow exclusions that remain documented and health-assessed
- Traceability from source definitions to approved lifecycle outcomes
- Detailed validation scoring to be discovered
- Remediation loop and retesting
- Product-owner acceptance and completion record

## Potential later capabilities

These are hypotheses, not current commitments:

- additional IGA sources and destinations;
- continuous or recurring health assessment;
- reusable mapping and assessment rule libraries;
- portfolio-level migration reporting;
- work-management and documentation-platform integrations;
- partner or vendor service-delivery administration.

## Pilot measurement

- Representation of every supported, in-scope object with an assessment or explicit gap state
- Discovery coverage by assessment status and evidence category, without a current percentage target
- Finding accuracy by confirmed, rejected, indeterminate, and unreviewed disposition
- Greater than 80% confirmed accuracy among consultant-reviewed AI findings
- Every Critical and High AI finding plus a stratified lower-severity sample, totaling at least 100 reviewed AI findings or all findings when fewer exist
- At least two independent One Identity Manager 10.x SQL Server environments and an initial assessment plus reassessment
- Initial scale of at least 100,000 records/objects in each named category per customer
