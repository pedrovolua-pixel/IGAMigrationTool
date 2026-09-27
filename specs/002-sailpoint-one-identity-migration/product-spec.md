# Feature: SailPoint to One Identity Manager migration

Status: Draft

Owner: Product owner

Created: 2026-09-27

Last updated: 2026-09-27

## Summary

Provide a governed migration workflow from a read-only SailPoint Identity Security Cloud source into a One Identity Manager 10 destination. The initial release prepares and executes approved changes in a non-production One Identity environment, validates configuration and governance behavior, and produces a customer-accepted handoff package. Production promotion is a later, deprioritized capability.

Migration follows the established order of roles, Access Profiles, certifications, and Joiner/Mover/Leaver workflows. SailPoint Sources additionally produce One Identity target-system and synchronization implementation recipes and prerequisite tasks. Mappings preserve governance outcomes and source evidence rather than forcing literal object equivalence.

## Problem

SailPoint and One Identity Manager represent connected systems, access packages, roles, requestability, certification, and lifecycle automation differently. One Identity implementations also depend on installed modules, target-system connectors, synchronization projects, account definitions, manage levels, IT Shop structures, processes, scripts, Job servers, and custom schema. A literal translation can create incomplete provisioning, duplicate automation, or unsupported behavior.

Consultants need a reviewable recipe that explains how each approved SailPoint outcome becomes native One Identity configuration, identifies prerequisites and gaps, executes safely in non-production, and retains enough evidence for validation, rollback, customer acceptance, and later production planning.

## Goals

- Map approved SailPoint outcomes into native One Identity Manager 10 concepts.
- Generate implementation recipes for every selected SailPoint Source and dependent migration object.
- Support direct non-production configuration and reviewed native package generation.
- Make prerequisites, dependencies, semantic gaps, customizations, and manual work explicit.
- Validate both configuration similarity and equivalent governance behavior in non-production.
- Permit assigned consultants to approve non-production steps and non-fatal warnings while requiring formal customer acceptance of the completed non-production result.
- Preserve source evidence, destination before/after evidence, execution history, rollback information, and residual risks.
- Support configurable MCP access for customer-selected agent harnesses without bypassing identity, scope, approval, redaction, or audit controls.

## Non-goals

- Production execution or promotion in the initial release.
- One Identity Manager destination versions other than version 10.
- Copying SailPoint source credentials into One Identity target-system connections.
- Automatically deleting pre-existing One Identity objects.
- Migrating completed certification history or reviewer decisions.
- Reproducing source structure when a different native One Identity design better preserves the approved outcome.
- Allowing an MCP client to exceed the authority of its authenticated user or service identity.

## Users and preconditions

- The project has one SailPoint source and one One Identity Manager 10 non-production destination.
- SailPoint remains read-only throughout collection, mapping, execution, retry, recovery, and validation.
- The destination may be customer-managed on-premises, private-cloud, or hosted.
- The destination inventory identifies installed modules, enabled capabilities, custom schema, existing objects, supported authentication, and writable interfaces.
- Source collection and the approved migration baseline are complete enough to produce mappings or explicit gaps.
- Source and destination use separate credentials. Destination credentials are limited to the approved change scope.
- The customer has configured who may approve non-production work, final acceptance, MCP access, and evidence handling.

## User flows

### Primary flow

1. An authorized user selects a One Identity Manager 10 non-production destination.
2. The product inventories destination modules, features, schema, customizations, existing objects, authentication methods, and supported change mechanisms.
3. The product generates Source-to-target-system recipes and maps roles, Access Profiles, certifications, and lifecycle workflows into native One Identity concepts.
4. The product creates or proposes project tasks for prerequisites, credentials, connector and infrastructure preparation, gaps, decisions, validation, and manual work.
5. A consultant edits and approves the versioned recipe and previews the exact object- and field-level changes.
6. The product generates a native package or executes approved non-production changes through the safest applicable mechanism.
7. Generated target-system synchronization, processes, schedules, and provisioning triggers remain held until their prerequisite checks and approvals are satisfied.
8. The product runs configuration checks, simulation or dry-run validation, and approved non-production behavioral tests.
9. The product records warnings, results, before/after state, recovery evidence, and residual risks.
10. The customer formally accepts or rejects the completed non-production result.
11. The product produces a handoff package for later production planning.

### Alternative flows

- A required One Identity module is absent. The product presents applicable choices: stop as unsupported, generate prerequisites for later installation, or select an outcome-preserving alternative mapping.
- A pre-existing destination object is found. An authorized reviewer chooses reuse, merge, update, replace through a separately approved step, or create separately.
- A supported API, native transport, or administrative mechanism cannot achieve the approved result. A direct database step may be proposed with enhanced preparation, approval, backup, validation, and rollback controls.
- A migration step partially succeeds. The operator may resume, retry safely, apply a compensating rollback, or accept the partial non-production state with an auditable decision.
- An agent harness uses MCP to read or, when enabled, update tasks, submit evidence, request approvals, provide credential references, generate packages, or execute already-authorized steps.

## Functional requirements

### FR-OIM-1: Destination scope

The initial migration release must target One Identity Manager version 10 in customer-managed on-premises, private-cloud, and hosted deployments. Each project targets exactly one One Identity environment and must complete non-production execution and validation before any later production promotion capability.

### FR-OIM-2: Migration sequence

Migration must proceed in this order: SailPoint Sources to One Identity target systems and synchronization recipes as prerequisites, roles, Access Profiles, certifications, then Joiner/Mover/Leaver workflows.

### FR-OIM-3: Execution modes

The product must support direct approved non-production configuration and reviewed migration-package generation. Generated packages must use native One Identity mechanisms where supported and include readable implementation documentation and manual steps for anything that cannot be packaged.

### FR-OIM-4: Destination discovery

Before mapping, the product must inventory installed modules, enabled features, custom schema, existing objects, authentication mechanisms, writable interfaces, connectors, Job or synchronization infrastructure, and destination customizations.

### FR-OIM-5: Missing-capability choices

When a required module or capability is unavailable, the product must present applicable choices to stop, prepare prerequisites for later installation, or select an alternative mapping that preserves the approved governance outcome. It must not silently approximate unsupported behavior.

### FR-OIM-6: Approval boundary

Assigned consultants may approve and execute non-production destination changes and accept documented non-fatal warnings. The customer must formally accept the completed non-production validation result. Any later production promotion requires explicit customer approval.

### FR-OIM-7: Destination credentials

Source and destination credentials must remain separate. SailPoint Source secrets must not be copied. An authorized user must enter or securely reference new One Identity target-system and migration credentials.

### FR-OIM-8: Change mechanism preference

Execution must prefer supported APIs, native transports, and approved administrative tools. Customer-executed scripts and direct database operations may be used when approved and necessary. Direct database changes require enhanced review and recovery controls.

### FR-OIM-9: Direct database preparation

Before a direct database change, a consultant task must identify exact tables and rows, vendor supportability, trigger and process implications, required temporary disabling, backup or restore point, validation query, re-enable steps, and rollback procedure. After consultant approval, the step may execute in non-production.

### FR-OIM-10: Source implementation recipe

Each selected SailPoint Source must produce a recipe covering source evidence and scope, recommended native connector or custom-target approach, modules and infrastructure, target-system and synchronization design, account definitions and manage levels, schemas and mappings, provisioning direction, schedules and triggers, credential-entry tasks, dependencies, execution order, validation, rollback, and recovery.

### FR-OIM-11: Native connector preference

When One Identity provides a suitable native connector, the recipe must require or recommend it. A custom target system is used only when a suitable native connector is unavailable or an approved product-specific reason requires it.

### FR-OIM-12: Prerequisite tasks and blocking

Recipes must identify module, connector, Job or synchronization server, schema, account-definition, manage-level, provisioning, schedule, connectivity, permission, and credential prerequisites. Unmet prerequisites must block dependent roles, Access Profiles, certifications, and workflows.

### FR-OIM-13: Task materialization

By project configuration, recipe steps may become in-product tasks with owner, dependency, status, evidence, approval requirement, and completion criteria. Tasks may be assigned to consultants, customer administrators, destination administrators, or platform operators and handed off through Jira, GitHub, ServiceNow, CSV, or MCP.

### FR-OIM-14: Recipe versioning

Consultants must be able to edit recipes. Every material edit must be versioned and attributable. Change sets and packages must be versioned, checksummed, previewable, reproducible, and immutable once used for accepted validation.

### FR-OIM-15: Source-to-target cardinality

Source-to-target mappings are expected to be one-to-one. Exceptional one-to-many decomposition is supported only with explicit rationale and human approval. The product must not automatically consolidate multiple SailPoint Sources into one target system.

### FR-OIM-16: Access Profile mapping

A SailPoint Access Profile must be evaluated for mapping to a One Identity system role containing effective entitlements, an IT Shop product when requestable, approval policy and request properties when applicable, and account definitions or manage levels when it provisions or governs accounts.

### FR-OIM-17: Non-requestable access

A non-requestable Access Profile may map to a system role assignable through business roles, organizations, workflows, or explicitly approved direct assignments without being published in the IT Shop.

### FR-OIM-18: Role mapping

Each SailPoint role must be mapped by behavior to one or more native One Identity concepts: business role, system role, IT Shop product, dynamic role, or explicitly approved direct identity assignment. Users must review which native role categories participate before proposals execute.

### FR-OIM-19: Criteria-derived membership

Safely representable SailPoint membership criteria may become a One Identity dynamic role. Unsupported or ambiguous criteria remain health findings and consultant tasks. Membership already represented by a dynamic role must not produce duplicate lifecycle automation; the suppression must be logged and warned.

### FR-OIM-20: Effective access and identity matching

Nested roles and Access Profiles must preserve effective entitlements even if destination hierarchy differs. Identity matching uses email, employee ID, account ID, and configurable rules. Conflicting or ambiguous matches require consultant tasks and must not be silently selected.

### FR-OIM-21: Provisioning-policy mapping

SailPoint provisioning policies and attribute transforms must be evaluated for mapping to One Identity account definitions, manage levels, templates, IT operating data mappings, scripts, or processes according to behavior. Unsupported transforms, rules, or actions create proposed replacements, health findings, and blocking consultant tasks.

### FR-OIM-22: Existing destination objects

When a corresponding target system, synchronization project, system role, business role, service item, IT Shop product, account definition, attestation object, or process exists, an authorized reviewer must choose reuse, merge, update, replacement through a separately approved removal step, or separate creation before execution.

### FR-OIM-23: Customization preservation

Existing One Identity customizations are preserved by default. A conflict with proposed migration configuration must create a decision task rather than overwrite the customization.

### FR-OIM-24: Certification mapping

SailPoint certification definitions must map to applicable One Identity attestation policies, procedures, schedules, scope conditions, approval policies, approvers, reminders, escalation, decision options, and remediation behavior. Reusable templates and scheduled-but-not-started campaigns become reusable destination configuration.

### FR-OIM-25: Active and historical certifications

For an active SailPoint campaign, One Identity configuration must remain disabled or held until the source review completes. Completed history and reviewer decisions are not migrated.

### FR-OIM-26: Certification gaps and validation

Scope or reviewer behavior that cannot be reproduced must block acceptance until explicitly resolved. Certification validation uses 10% existence/type, 30% scope, 30% reviewers, 15% schedule/timing, and 15% decision/remediation; scope and reviewer mismatches are fatal.

### FR-OIM-27: Lifecycle mapping

SailPoint lifecycle workflows must map by behavior into One Identity events, processes, process steps, schedules, scripts, templates, and approval workflows. Custom workflows, scripts, and rules must produce a proposed implementation, consultant task, health finding, and explicit manual steps when automation is unsafe.

### FR-OIM-28: Workflow hold and prerequisites

Generated One Identity processes must remain disabled until compilation, consistency, dependency, permission, and non-production validation checks succeed. Target systems, Job servers, synchronization servers, modules, process tasks, scripts, and permissions are blocking dependencies.

### FR-OIM-29: In-flight source work

In-flight SailPoint certifications and lifecycle executions remain authoritative and read-only until completion. Corresponding One Identity attestation, process, schedule, synchronization, provisioning, and downstream triggers remain held.

### FR-OIM-30: Workflow validation

Workflow validation weights existence at 25%, triggers and criteria at 25%, and actions at 50%. Equivalent governance outcomes take priority over literal process structure. Unsupported referenced systems are fatal; consultants may accept other documented warnings for non-production execution.

### FR-OIM-31: Change preview and data minimization

Packages and direct change sets must be inspectable at object and field level and must exclude credentials. Identity and account movement is limited to approved static memberships, ownership references, and matching identifiers.

### FR-OIM-32: Before-images and safe rollback

The product must capture a before-image for every modified destination object. Rollback may remove only objects created by the change set after dependency verification. Automatic deletion of pre-existing objects is prohibited; replacement requires a separate explicit removal step.

### FR-OIM-33: Partial execution

For partial success, authorized operators may resume from the failed step, retry safely, apply compensating rollback, or accept the partial non-production state with an attributable decision.

### FR-OIM-34: Idempotency and restoration

Operations must use stable migration identifiers and be idempotent where One Identity permits. Temporarily disabled triggers, processes, schedules, and synchronization configurations must be restored to their original state after completion or failure, with prominent consultant warnings when restoration needs attention.

### FR-OIM-35: Downstream-impact hold

Any action capable of downstream provisioning or deprovisioning must remain disabled until an impact preview and applicable approval are complete. Non-production tests must not reach production-connected downstream systems unless separately approved.

### FR-OIM-36: Non-production validation

Validation must verify target systems and synchronization projects, modules and infrastructure, schemas and mappings, connections, simulations or dry runs, unresolved references, provisioning direction, and hold states. Controlled non-production synchronization may run after successful simulation using customer-provided or synthetic test identities selected per project.

### FR-OIM-37: Behavioral validation

Where supported, non-production validation must exercise dry-run-style request, approval, provisioning, revocation, attestation, and lifecycle behavior rather than relying only on configuration comparison.

### FR-OIM-38: Role and Access Profile validation

Role validation equally weights destination role/package existence, effective entitlements or company resources, owner, static members, and effective inheritance outcome. Access Profile validation equally weights existence, target system/account definition, entitlements, owner, and request/provisioning settings. Access Profile entitlement mismatch is fatal.

### FR-OIM-39: Final non-production acceptance

After consultant execution and warning disposition, the customer must accept or reject the complete non-production validation result. The evidence package must include source evidence, approved mapping, before/after state, executed steps, test results, warnings, unresolved tasks, rollback evidence, and residual risks.

### FR-OIM-40: Handoff package

A completed non-production engagement must produce a handoff package containing accepted configuration, unresolved work, operational prerequisites, production considerations, rollback guidance, and validation evidence.

### FR-OIM-41: Production boundary

Production promotion is outside the initial release. A later capability must require successful non-production validation, an immutable validated package, explicit customer approval, a fresh destination drift check, and a separate downstream-impact review.

### FR-OIM-42: MCP integration

The product must expose an MCP server so any standards-compatible, validly authorized customer-selected agent harness may access project capabilities. The in-product record remains canonical when tasks or evidence are copied or synchronized through MCP or another integration.

### FR-OIM-43: MCP modes and identity

MCP access defaults to read-only. A customer administrator may enable mutations per project. Clients authenticate through named user identities or scoped service identities and may act only with that identity's permissions.

### FR-OIM-44: MCP authorization

MCP permissions must be independently configurable for reads, task management, package generation, approvals, risk acceptance, credential-reference submission, and execution. Permissions are scoped by customer, project, environment, action, and evidence category.

### FR-OIM-45: MCP authority parity

MCP may approve non-production work, approve production work in a later release, accept residual risk, or execute migration steps only when its authenticated identity has the same authority required in the product UI and all prerequisite approvals exist.

### FR-OIM-46: MCP secret handling

MCP must not read or return plaintext credentials. An appropriately authorized client may submit a secret reference or initiate an OAuth flow. MCP responses must apply the same redaction, retention, and customer-boundary rules as the UI and exports.

### FR-OIM-47: MCP separation, concurrency, and revocation

The product must prevent one service identity from proposing and approving the same consequential change unless the customer explicitly configures an exception. Every mutation returns a version or concurrency token. Customers may revoke a client or identity immediately without deleting its audit history or submitted evidence.

## Permissions

| Actor | Allowed action | Constraints |
|---|---|---|
| Assigned consultant | Edit recipes, approve and execute non-production steps, accept non-fatal warnings, validate results | Cannot provide final customer acceptance or approve production |
| Customer administrator or authorized customer approver | Configure authority, approve exceptions, accept non-production results, approve later production promotion | Restricted to its organization and approved environments |
| Destination administrator | Complete assigned prerequisites and destination tasks | Limited to assigned tasks and destination permissions |
| Platform operator | Support authorized execution and recovery | Must preserve isolation and cannot assume customer approval authority |
| Named MCP user | Perform enabled actions | Limited to the named user's product permissions |
| MCP service identity | Perform explicitly granted actions | Granular scopes, separation-of-duties policy, revocable access, and full audit apply |

## Validation and failure behavior

- Unsupported One Identity versions, missing modules, unavailable connectors, and incompatible customizations fail visibly or produce explicit decision paths.
- Failed prerequisites block dependent migration objects.
- A direct database step cannot run without the required preparation task and consultant approval.
- Package or destination drift invalidates the affected validation and requires review.
- A failed or partial operation preserves completed evidence and offers resume, retry, compensation, or explicit partial acceptance.
- Pre-existing objects are never automatically deleted.
- Disabled destination automation remains held until validation and approval conditions are satisfied.
- A failed attempt to restore temporarily disabled configuration produces a prominent warning and blocking recovery task.
- MCP stale-write attempts fail using the concurrency token and return the newer record version.
- Revoked MCP clients lose access immediately while their previous actions remain auditable.

## Edge cases

- One SailPoint Source requires exceptional decomposition into multiple One Identity target systems.
- A native One Identity connector exists but cannot represent required source-specific behavior.
- A destination customization modifies the same object or process the recipe proposes to change.
- A dynamic-role proposal overlaps an existing lifecycle workflow or static assignment.
- A process compiles successfully but retains non-fatal warnings accepted for non-production.
- Test identities would cause a path to a production-connected downstream system.
- An object created by the migration gains a new external dependency before rollback.
- A consultant-approved partial state cannot satisfy final customer acceptance.
- An MCP service identity proposes a change and then attempts to approve it.

## User-facing errors

- Required One Identity module, connector, server, permission, or schema is unavailable.
- Existing destination object requires disposition.
- Source-to-target recipe is incomplete or has blocked prerequisites.
- Direct database preparation or recovery evidence is missing.
- Package checksum, destination state, or validation baseline has drifted.
- Synchronization simulation, compilation, consistency check, connection test, or behavioral validation failed.
- A fatal entitlement, scope, reviewer, or referenced-system mismatch remains unresolved.
- Destination automation is still held.
- MCP client is unauthorized, revoked, out of scope, or using a stale concurrency token.

## Analytics and audit requirements

- Audit recipe generation and edits, task changes, approvals, exceptions, package generation, preview, execution, retry, rollback, partial acceptance, validation, customer acceptance, and handoff.
- Record source baseline, destination environment, destination before/after state, package checksum, migration identifier, actor, approval, timestamp, result, and correlation context.
- Audit every MCP read and mutation with client identity, user or service identity, project, environment, scope, action, result, and version token without recording plaintext secrets.
- Measure recipe coverage, prerequisite completion, automation versus manual steps, warnings, fatal gaps, execution outcomes, rollback use, validation outcomes, and time to customer acceptance.

## Accessibility requirements

- Recipe dependency graphs, change previews, validation results, and migration status must have keyboard-accessible and non-graphical equivalents.
- Fatal and non-fatal conditions must not rely on color alone.
- Object- and field-level diffs must identify additions, changes, removals, held automation, and unresolved decisions in text.

## Security and privacy considerations

- SailPoint source access remains technically read-only.
- Source and destination credentials are separate and excluded from packages, evidence, reports, logs, and MCP responses.
- Non-production execution is limited to explicitly authorized destination scope.
- Direct database operations require enhanced review, backup, verification, and restoration controls.
- Downstream provisioning remains held until impact review and approval.
- Production-connected systems are excluded from non-production tests unless separately approved.
- MCP uses granular authorization, identity parity, separation of duties, redaction, audit, revocation, and optimistic concurrency.
- The approved product-wide rule requiring customer approval for every direct destination change must be revised to allow assigned-consultant approval in non-production while retaining customer approval for production.

## Acceptance criteria

### AC-OIM-1: Destination preflight

Given a One Identity Manager 10 non-production destination

When migration discovery runs

Then installed modules, capabilities, custom schema, existing objects, customizations, authentication, connectors, infrastructure, and writable mechanisms are inventoried before mappings execute.

### AC-OIM-2: Complete Source recipe

Given a selected SailPoint Source

When its migration recipe is generated

Then the recipe contains every implementation, prerequisite, dependency, credential, validation, rollback, and recovery category required by FR-OIM-10.

### AC-OIM-3: Blocked dependency

Given an unmet target-system prerequisite

When a dependent role, Access Profile, certification, or workflow is selected for execution

Then execution is blocked and the missing prerequisite is shown as an actionable task.

### AC-OIM-4: Conditional native mapping

Given a SailPoint role or Access Profile

When a destination proposal is generated

Then the proposal selects native One Identity concepts based on inheritance, entitlements, requestability, approvals, provisioning, and membership behavior and retains the source evidence and rationale.

### AC-OIM-5: Criteria-derived membership

Given safely representable SailPoint membership criteria

When mapping occurs

Then the product proposes a dynamic role and suppresses duplicate workflow generation with a visible warning and audit record.

### AC-OIM-6: Existing object decision

Given a matching destination object

When execution is prepared

Then no change occurs until an authorized reviewer records reuse, merge, update, separately approved replacement, or separate creation.

### AC-OIM-7: Direct database gate

Given a proposed direct database operation

When the preparation task or consultant approval is missing

Then the operation cannot execute.

### AC-OIM-8: Recoverable partial execution

Given a change set that partially succeeds

When the operator reviews the result

Then the product preserves before/after evidence and offers resume, retry, compensation, or auditable partial acceptance without deleting pre-existing objects automatically.

### AC-OIM-9: Held downstream effects

Given generated synchronization, process, schedule, or provisioning configuration

When prerequisite validation or approval is incomplete

Then downstream execution remains disabled and the hold reason is visible.

### AC-OIM-10: Non-production behavioral validation

Given successful configuration checks and simulation

When approved test identities are used

Then the product can validate applicable request, approval, provisioning, revocation, attestation, and lifecycle outcomes without reaching production-connected systems unless separately approved.

### AC-OIM-11: Fatal validation mismatch

Given an unresolved Access Profile entitlement mismatch, certification scope or reviewer mismatch, or unsupported workflow referenced system

When final validation is calculated

Then customer acceptance is blocked regardless of aggregate score.

### AC-OIM-12: Customer non-production acceptance

Given completed consultant execution and validation

When the customer reviews the evidence package

Then the customer can accept or reject the non-production result with an attributable decision.

### AC-OIM-13: Production boundary

Given an accepted non-production result

When a user attempts production execution in the initial release

Then execution is unavailable and the product produces the approved handoff and production-planning evidence instead.

### AC-OIM-14: MCP read-only default

Given a newly authorized MCP client

When it connects to a project

Then it has read-only access until a customer administrator explicitly grants granular mutation permissions.

### AC-OIM-15: MCP authority parity

Given an MCP request to approve, accept risk, submit a credential reference, or execute

When the authenticated identity lacks the equivalent product permission or prerequisite approval

Then the request is denied and audited.

### AC-OIM-16: MCP separation and concurrency

Given a service identity that proposed a consequential change or holds an outdated version token

When it attempts a prohibited approval or stale mutation

Then the product rejects the operation unless an applicable customer-configured separation exception exists.

## Dependencies

- Approved multi-source data-ingestion product specification.
- One Identity Manager 10 destination capability and change-mechanism matrix.
- One Identity native package/transport, API, administrative, and database-operation validation.
- Approved destination credential, secret-reference, and OAuth design.
- Versioned canonical mapping model and recipe/task model.
- Non-production test-data, simulation, and downstream-isolation design.
- Package signing/checksum, drift-detection, before-image, rollback, and recovery design.
- MCP server, identity, authorization, audit, redaction, and concurrency design.
- Jira, GitHub, ServiceNow, and CSV task handoff contracts.

## Open questions and assumptions

- **Required policy revision:** approved `NFR-SAF-8` currently requires customer approval for all direct destination changes. Discovery authorizes assigned consultants to approve and execute non-production changes while preserving customer approval for production.
- Direct database writes are in scope after consultant approval, but the exact supported and supportable operations must be established in technical design and security review.
- The initial release ends at non-production acceptance. Production requirements are retained only as later design constraints and are deprioritized.
- One-to-one Source-to-target mapping is expected; one-to-many is an approved exception path.
- Native One Identity package format, transport dependencies, and package installation workflow require technical validation.
- “Dry run” means the closest safe simulation or controlled non-production behavior supported by each One Identity capability; it must not imply that every operation has a vendor-native no-effect mode.
- MCP may expose consequential actions when configured, but it cannot weaken the approval, role, separation, credential, or environment boundaries defined for the same UI action.

## Approval

Approved by:

Date:

Only the product owner may change `Status` to `Approved`.
