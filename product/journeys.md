# Product Journeys

Status: Approved
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

These journeys describe intended user outcomes. They do not prescribe implementation or integration mechanisms.

## Journey 1 — Establish an engagement

1. An authorized user defines the customer or migration engagement.
2. A partner administrator can work across explicitly assigned customer organizations; a customer administrator remains within its own organization.
3. The user identifies SailPoint Identity Security Cloud as the source and the supported destination. Veza is the first destination; One Identity Manager follows in later scope.
4. The user records scope boundaries, environments, owners, and applicable data/security constraints.
5. Authorized access is established for the agreed discovery scope.
6. The product displays what it can and cannot collect before collection begins.

Outcome: the migration has explicit ownership, boundaries, and authorized evidence sources.

## Journey 2 — Collect and understand the current state

1. The analyst initiates collection for approved source areas.
2. The customer chooses hosted collection or the optional customer-side collection mode according to its data-boundary requirements.
3. For the initial scope, the product records evidence and provenance for SailPoint configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions.
4. Source credentials and passwords are excluded. Social Security numbers and government identifiers remain customer-side or are redacted before evidence crosses the approved customer boundary.
5. The product identifies inaccessible, incomplete, conflicting, redacted, customer-retained, or unsupported evidence.
6. The product organizes findings into an explorable inventory and candidate relationships.
7. The analyst reviews and corrects interpretation where needed.

Outcome: stakeholders have a qualified, traceable representation of the current state rather than an unexplained export.

## Journey 3 — Document and refine desired outcomes

1. The product produces graphical and written views of current use cases and relationships.
2. It drafts functional requirements, non-functional requirements, acceptance criteria, definitions of done, and delivery stories from supported findings.
3. AI-generated suggestions remain drafts until the consulting team reviews and refines them, resolves ambiguity, and identifies missing context.
4. The customer decides which behaviors should be preserved, changed, replaced, or retired, subject to any explicit approval policy the customer has configured.
5. Approved items form the migration scope; rejected or deferred items retain their disposition and rationale.

Outcome: migration intent is explicit, reviewable, and traceable to current-state evidence.

## Journey 4 — Assess health and risk

This journey must be usable as a customer-centered health assessment without requiring the customer to proceed through a full migration.

1. The product includes every supported, in-scope object in the health assessment, either as assessed or with an explicit gap state, regardless of migration selection.
2. It identifies potentially non-optimal use cases, security risks, suspicious code or scripts, and relevant queue/event/log errors.
3. Each finding includes evidence, affected objects, confidence, impact, and a suggested correction or next diagnostic step.
4. Qualified reviewers confirm, reject, defer, or accept the risk of findings.
5. Approved remediations are reflected in future-state requirements rather than silently applied.
6. The product produces a sponsor-readable report describing the current environment, assessment coverage, findings, recommendations, and proposed fix packages. ROI is excluded from the pilot and may be added in a later health release.
7. Findings show severity, AI confidence, evidence, and applicable authoritative sources.
8. In a health-assessment-only engagement, configured fix packages can be converted into traceable consultant tasks.
9. AI proposes findings; consultants confirm or refine them; customers accept residual risk.
10. Users consume consistent results through interactive dashboards, PDF reports, and Markdown exports for agent integrations.
11. Users explore process flows, relationship graphs, data-flow diagrams, coverage maps, dependency views, and migration-status dashboards from the canonical product record.
12. After the pilot, ROI may include labor/time savings, licensing optimization, reduced audit effort, risk reduction, avoided incidents, migration cost, and remediation cost, with transparent evidence and assumptions.
13. The engagement configures the queue, event, audit, and error-log lookback period.
14. Initial health-assessment phases do not execute remediation directly.

Outcome: the migration avoids blindly reproducing known or newly discovered weaknesses.

## Journey 5 — Prepare and execute migration

1. The product maps each approved outcome to destination capabilities and identifies gaps or required human decisions.
2. The AI prepares proposed destination changes; the consulting team reviews and refines the proposal.
3. The customer approves the proposed change set unless an authorized customer configuration explicitly permits that approval step to be skipped for the applicable scope.
4. The user chooses an approved execution mode: direct configuration by the product or generation of a reviewed migration package for separate execution.
5. In direct mode, the system configures approved capabilities and moves approved data that cannot be sourced directly by the destination, where supported.
6. In package mode, the system produces a versioned, reviewable set of migration instructions or artifacts and records its approved handoff.
7. It records every approval, configured approval exception, attempted direct change, or reported package outcome and its relationship to the approved scope.
8. Failures or partial completion produce actionable diagnostics and safe recovery options.

### Initial role mapping behavior

1. The product identifies the SailPoint role definition, description, owner, membership criteria, access profiles, entitlements, hierarchy/inheritance, assignments, and approval/governance settings where available.
2. It proposes a Veza Access Profile that preserves the approved role's effective entitlements and mapped owners; the visible hierarchy may differ when the effective entitlement outcome is preserved.
3. Identities are matched using email, employee ID, account ID, and engagement-configured matching rules. Conflicting signals create a consultant task with an automated suggested resolution rather than an automatic selection.
4. Static SailPoint role members are proposed as members of the destination Access Profile.
5. If an owner, member, entitlement, or source cannot be matched, the product still creates the Access Profile with a visible warning and a consultant task containing an automated suggested resolution.
6. Membership determined by SailPoint role criteria is documented as a health-assessment finding only and is not converted into a Joiner/Mover/Leaver migration requirement.
7. If a matching Veza Access Profile exists, the product requests a customer decision and does not change the existing profile until that decision is recorded.
8. All SailPoint interaction remains read-only throughout migration and validation.
9. Non-fatal unresolved warnings may proceed to direct configuration only when the customer explicitly approves them.
10. Consultant tasks remain available in the product and may also be exported or synchronized through supported Jira, GitHub, ServiceNow, CSV, or later task integrations.

Outcome: the destination is configured through an approved, auditable, and controlled process.

## Journey 6 — Validate and accept outcomes

1. The product derives validation work from approved acceptance criteria and destination expectations.
2. Automated and human checks evaluate the configured destination.
3. Results are classified as passed, failed, or not verified with evidence.
4. Gaps return to remediation or an explicit owner decision.
5. The product owner accepts the migrated scope only after required evidence is complete.

For migrated roles, validation includes the destination Access Profile, effective entitlements, owner, static members, and effective inheritance outcome. Each component has equal weight. The product provides a percentage plus red/yellow/green status, exposes the component evidence, and records whether the customer accepts or rejects the result. Red is below 50%, yellow is 50% through 80%, and green is above 80%. No role mismatch is automatically fatal.

## Initial Access Profile mapping behavior

1. A SailPoint Access Profile is proposed as a Veza Access Profile.
2. The proposed mapping covers name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings.
3. A referenced SailPoint Source is proposed as a Veza Integration using name, system type, and connection metadata.
4. Missing, ambiguous, or conflicting Integration matches do not block profile creation; they create visible warnings, automated suggestions, and consultant tasks.
5. A matching existing Veza Access Profile requires a customer decision before change.
6. Validation gives equal weight to Access Profile existence, Integration, entitlements, owner, and configuration settings.
7. Non-fatal warnings may proceed when the customer explicitly approves them.
8. An entitlement mismatch is fatal to acceptance and must be resolved or removed through an approved scope change.

## Initial certification mapping behavior

1. The product discovers every campaign type supported by the applicable SailPoint and Veza environments.
2. A SailPoint certification campaign is proposed as a Veza Access Review.
3. The proposal covers name, description, type, scope, reviewers, schedule, deadlines, reminders, escalation, decision options, and remediation settings.
4. Future campaigns include reusable templates and scheduled-but-not-started campaigns. The customer may exclude selected campaign objects from the approved migration scope; excluded objects remain documented and health-assessed.
5. Completed campaign history and reviewer decisions are not migrated.
6. For an in-flight campaign, the active round and decisions remain in SailPoint and are not modified, but the campaign settings are included in migration.
7. The corresponding Veza Access Review must not be triggered until the SailPoint round finishes. This condition appears as a warning and does not create a consultant task.
8. A matching existing Veza Access Review requires a customer decision before change.
9. Validation weights existence/type at 10%, scope at 30%, reviewers at 30%, schedule/timing at 15%, and decision/remediation settings at 15%. It uses the standard red/yellow/green thresholds; scope and reviewer mismatches are fatal to acceptance.

## Initial Joiner/Mover/Leaver mapping behavior

1. Every SailPoint lifecycle workflow, including custom workflows, is considered for mapping to a Veza Workflow within the LCM module.
2. Mapping covers triggers, identity criteria, actions, approvals, and timing.
3. Criteria-derived role membership is excluded from workflow migration and remains a health-assessment finding.
4. For an in-flight workflow, definitions and settings may migrate while the active execution remains in SailPoint.
5. The corresponding Veza Workflow must not be triggered until the SailPoint execution finishes; the condition appears in health assessment and migration reporting.
6. A referenced system unsupported by Veza is fatal to workflow acceptance, is logged with evidence, and appears in the health assessment.
7. Validation weights workflow existence at 25%, triggers/identity criteria at 25%, and actions at 50%.
8. A matching existing Veza Workflow requires a customer decision before change.
9. Custom scripts or rules produce an automated suggested Veza replacement, a consultant task, and a health finding.
10. Customers may exclude selected workflows from migration; excluded workflows remain documented and health-assessed.
11. The grade uses the standard red/yellow/green thresholds.
12. A referenced system unsupported by Veza is the only automatically fatal mismatch; every other mismatch creates a warning, explanation, and consultant task.

Outcome: completion means approved outcomes were demonstrated, not merely that configuration commands ran.

## Cross-journey exception paths

- credentials or permissions are insufficient;
- evidence is incomplete, stale, inconsistent, or unsupported;
- a source concept has no destination equivalent;
- a finding requires expert judgment or changes approved scope;
- destination configuration partly succeeds;
- validation contradicts the expected outcome;
- collected information exceeds the engagement's approved data boundary.
