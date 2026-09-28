# Outcome-Based Roadmap

Status: Approved
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

The phases reflect the sequence supplied by the product owner. Veza is the first destination. Initial end-to-end use-case delivery proceeds through roles, access profiles, certifications, then Joiner/Mover/Leaver workflows. Release boundaries and dates remain undecided.

## Phase 1 — Collection

Goal: create a trustworthy, scoped, and traceable representation of relevant SailPoint Identity Security Cloud evidence.

Exit outcome:

- users can see what was collected, from where, when, and with what gaps;
- initial evidence covers configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions;
- customers can choose hosted or customer-side collection;
- source credentials and passwords are excluded, while Social Security numbers and government identifiers remain customer-side or are redacted;
- SailPoint access is read-only throughout every phase;
- the inventory is sufficient to begin use-case interpretation;
- security and data-handling boundaries are validated.

## Phase 2 — Documentation and review

Goal: convert collected evidence into understandable current-state use cases and reviewable future-state requirements.

Exit outcome:

- graphical and written representations are available;
- process, relationship, data-flow, coverage, dependency, and migration-status views are available from the canonical product record;
- PDF and Markdown exports identify their canonical source version;
- functional/non-functional requirements, acceptance criteria, definitions of done, and stories can be drafted and refined;
- AI suggestions, consulting refinements, and customer decisions remain distinguishable;
- the product owner can approve a migration baseline and disposition exclusions.

## Phase 3 — Health assessment

Goal: provide a customer-centered assessment of material design, security, code/script, configuration, queue, event, and log concerns. This phase must also work independently of a complete migration engagement.

Pilot delivery sub-phases:

- 1A — One Identity Manager 10.x SQL Server read-only evidence foundation and immutable baselines;
- 1B — rules, automatic AI, findings, scoring, maturity, recommendations, grouped fix packages, and in-product consultant tasks with CSV export;
- 1C — review, reassessment, governed publication, dashboard, PDF, Markdown, accessibility, and pilot evaluation;
- 1D — read-only MCP access to authorized assessment results and protected evidence references.

The pilot is consultant-operated. ROI, direct remediation, production migration, custom-rule authoring, anonymous benchmarking, authenticated report download, and external task creation are not pilot exit requirements.

Exit outcome:

- supported findings contain evidence and recommended next action;
- every collected object and supported evidence category is assessed regardless of migration selection;
- excluded, deferred, and non-migrated objects retain their documentation and findings;
- all applicable vendor, security, software-development, and customer rules are traceable to findings;
- sponsors receive a complete current-state report with severity, AI confidence, recommendations, and fix packages;
- transparent ROI is added after the pilot and may later cover labor/time, licensing, audit effort, risk reduction, avoided incidents, migration cost, and remediation cost where supported;
- interactive dashboards, PDF, and agent-friendly Markdown expose the same canonical assessment baseline;
- AI proposes, consultants confirm/refine, and customers accept residual risk;
- engagement owners configure log and event history depth;
- health-only engagements can convert configured fix packages into consultant tasks;
- initial phases do not execute remediation directly.
- qualified reviewers can disposition findings;
- approved remediations are incorporated into future-state requirements.
- customers can complete a health-assessment engagement without proceeding to migration.

## Phase 4 — Migration

Goal: configure an approved destination or produce a reviewed migration package, and move required approved data through a controlled, auditable process.

Exit outcome:

- supported approved outcomes are mapped to the selected destination;
- the first roles slice proposes Veza Access Profiles that preserve effective approved entitlements and owners, plus static members matched using configurable identity rules;
- ambiguous or unmatched references create warnings and consultant tasks with automated suggestions without blocking profile creation;
- consultant tasks can remain in-product or be handed off through Jira, GitHub, ServiceNow, CSV, and later integrations;
- criteria-derived role membership remains a health-assessment finding and is not migrated through Joiner/Mover/Leaver scope;
- existing matching Veza Access Profiles require customer decisions before change;
- non-fatal unresolved warnings may proceed with explicit customer approval;
- the subsequent access-profile slice maps SailPoint Access Profiles—including name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings—to Veza Access Profiles;
- SailPoint Sources match Veza Integrations by name, system type, and connection metadata;
- missing or ambiguous Integration matches create warnings, automated suggestions, and consultant tasks without blocking Access Profile creation;
- existing matching Veza Access Profiles require a customer decision before change;
- non-fatal Access Profile warnings may proceed with customer approval, while entitlement mismatches remain fatal to acceptance;
- certification coverage includes every campaign type supported by the source and destination;
- SailPoint certification campaigns map to Veza Access Reviews with settings for name, description, type, scope, reviewers, schedule, deadlines, reminders, escalation, decision options, and remediation;
- future campaigns include reusable templates and scheduled-but-not-started campaigns, with customer-selected object exclusions that remain documented and health-assessed;
- completed history and reviewer decisions do not migrate;
- in-flight rounds and decisions remain on SailPoint while their campaign settings migrate;
- warnings prevent triggering corresponding Veza Access Reviews until source rounds finish, without creating consultant tasks;
- existing matching Veza Access Reviews require customer decisions before change;
- changes are previewed and explicitly approved;
- users can select direct configuration or reviewed package generation for supported changes;
- results, exceptions, manual actions, and recovery state are visible.

## Phase 5 — Validation

Goal: demonstrate that the destination produces the approved outcomes.

Exit outcome:

- acceptance criteria have passed, failed, or not-verified evidence;
- migrated roles receive an equally weighted percentage and accessible red/yellow/green grade covering Access Profile existence, effective entitlements, owner, static members, and effective inheritance outcome;
- migrated Access Profiles receive an equally weighted grade covering existence, Integration, entitlements, owner, and configuration settings;
- Access Profile entitlement mismatch blocks acceptance regardless of aggregate grade;
- Access Review scope or reviewer mismatch blocks acceptance regardless of aggregate grade;
- certification grades use the standard thresholds and weight existence/type at 10%, scope at 30%, reviewers at 30%, schedule/timing at 15%, and decision/remediation at 15%;

Joiner/Mover/Leaver scope maps every SailPoint lifecycle workflow, including custom workflows, to Veza Workflows within the LCM module. Mapping covers triggers, identity criteria, actions, approvals, and timing. In-flight definitions/settings may migrate while active executions finish in SailPoint; destination triggering remains blocked until completion. Systems unsupported by Veza are fatal, logged, and included in health assessment. Validation weights Workflow existence at 25%, triggers/criteria at 25%, and actions at 50% and uses standard thresholds. Existing workflows require customer decisions. Custom scripts/rules receive suggested replacements, consultant tasks, and health findings. Customer-excluded workflows remain documented and health-assessed. Every non-fatal mismatch produces a warning, explanation, and consultant task.
- no individual mismatch is automatically fatal; the customer accepts or rejects the graded outcome with an auditable decision;
- differences and residual risks are explicitly resolved or owned;
- the product owner can make an informed acceptance decision.

## Sequencing decisions still required

- whether initial releases serve one customer engagement at a time or a multi-customer service;
- the smallest vertical slice that can prove collection-to-validation traceability;
- which evidence and object categories are essential for each initial use case;
- the commercial packaging of standalone health assessment versus full migration service.

## Pilot success gate

The pilot will prioritize discovery coverage and finding accuracy. Every supported, in-scope object must be represented as assessed or with an explicit gap state; no percentage target is set. More than 80% of reviewed non-indeterminate AI findings must be confirmed. Review covers every Critical and High AI finding plus a module/category/severity-stratified sample of lower-severity findings, totaling at least 100 reviewed AI findings or all findings when fewer exist. Acceptance also requires at least two independent eligible One Identity Manager 10.x SQL Server environments and an initial assessment plus reassessment. The pilot decisions for permanent purge, support hours and response targets, 90-day operational-evidence window, recovery targets, accessibility, performance, scale, and the excess-read-only permission exception are recorded in the health specification and non-functional requirements.

## Later roadmap — Approved remediation execution

Direct execution of customer-approved health remediations may be considered only after the initial assessment, documentation, task, migration, and validation controls are proven. It requires a separate product specification, technical design, risk review, approval model, and rollback strategy.
