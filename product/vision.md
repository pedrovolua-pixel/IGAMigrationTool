# Product Vision

Status: Approved
Owner: Product owner
Source: Product owner concept supplied 2026-09-26
Approved by: Product owner
Approved: 2026-09-28
Last updated: 2026-09-28

## Vision

Create a web application that helps organizations understand, assess, document, migrate, and validate Identity Governance and Administration (IGA) capabilities when moving from one IGA product to another.

Initial product coverage is intended to support SailPoint Identity Security Cloud as a source. Veza is the first destination for an end-to-end migration slice; One Identity Manager remains an intended subsequent destination.

## Problem

IGA migrations require teams to reconstruct what the current platform actually does before they can decide what should move. Relevant behavior may be distributed across use cases, settings, data, roles, access profiles, scripts, manually created artifacts, queues, events, and operational knowledge. This makes migration scope difficult to understand, review, reproduce, and validate.

A migration that copies artifacts without understanding their intended outcomes can preserve obsolete design, omit important behavior, or recreate security and operational weaknesses in the destination.

## Product proposition

The product will create a traceable path from the observed source environment to:

1. an inventory of existing capabilities and artifacts;
2. graphical and written documentation of current use cases;
3. functional requirements, non-functional requirements, acceptance criteria, definitions of done, and delivery stories;
4. a product-owner-reviewed migration scope;
5. health and security findings with suggested remediation;
6. configuration and necessary data movement for approved destination capabilities; and
7. evidence that the destination produces the approved outcomes.

## Intended users, customers, and delivery model

The initial buyers and service-delivery users are consulting partners and the professional services teams of Veza or One Identity. Likely day-to-day users include migration product owners, IGA analysts or consultants, security reviewers, and engineers or administrators responsible for source and destination platforms.

The product must support both managed-service use by those teams and customer-operated use. Direct customer use is expected to center primarily on health assessment, while the broader workflow supports professional-services-led discovery, documentation, migration, and validation.

Health assessment is independent of migration selection. Every collected object and supported evidence category is assessed even when the customer excludes it from migration.

The health-assessment output is a complete, sponsor-readable report of what exists in the current IGA environment, what was assessed, identified risks and inefficiencies, recommendations, and proposed fix packages. The pilot excludes ROI; later evidence-based ROI may cover labor/time savings, licensing optimization, reduced audit effort, risk reduction, avoided incidents, migration cost, and remediation cost where evidence supports them. Assessment draws from all applicable authoritative sources, including vendor guidance, recognized security guidance, software-development practices, and customer rules. Findings include severity and AI confidence. For a health-assessment-only engagement, fix packages can be converted into consultant tasks when configured.

Health results are available through interactive dashboards and consistent PDF and Markdown exports. Markdown supports agent and automation integrations. AI proposes findings, consultants confirm or refine them, and customers accept residual risk. Initial phases do not execute remediation directly; customer-approved direct remediation is a later roadmap capability. Queue, event, audit, and error-log history depth is configurable per engagement.

The product is the canonical home for maintained discovery, health, migration, and validation documentation; PDF and Markdown are generated exports. Visualizations include process flows, relationship graphs, data-flow diagrams, coverage maps, dependency views, and migration-status dashboards.

The service supports multiple consulting partners and customer organizations concurrently with strict isolation. Data residency, retention, deletion, and export policies are configurable per customer.

Partner administrators may access multiple customer organizations only when assigned; customer administrators are restricted to their own organization. The default data-retention period is 30 days unless a customer configures another supported policy, and the event that starts the retention clock is configurable. At retention expiry, data is soft-deleted; pilot active-system data is permanently purged within 30 additional days. Initial scale targets at least 100,000 identities, accounts, entitlements, roles, workflows, and records within the configurable 90-day operational-evidence window per customer in each category, measured independently.

Pilot support operates 9:00 a.m. to 5:00 p.m. Eastern Time on United States business days, with response targets defined in the non-functional requirements. Initial data residency is limited to the United States. The pilot applies baseline security controls and customer contractual requirements without claiming formal compliance certification.

The commercial packaging, licensing, and boundary between partner and customer responsibilities remain open.

## Primary jobs to be done

1. Understand and explain what the current IGA environment does.
2. Decide which existing outcomes should be preserved, improved, replaced, or retired.
3. Convert selected outcomes into reviewable migration requirements and delivery work.
4. Detect unhealthy, insecure, erroneous, or non-optimal implementation patterns before reproducing them.
5. Configure the destination and move only the data needed to achieve the approved outcomes.
6. Demonstrate that the migrated destination behaves as intended.

## Initial end-to-end scope

The first destination is Veza. The initial migration scenario covers:

- roles;
- access profiles;
- certifications; and
- Joiner, Mover, and Leaver workflows.

The intended delivery order is roles, access profiles, certifications, then Joiner/Mover/Leaver workflows.

For this scope, destination equivalence means preserving the approved business and governance behavior while achieving appropriate configuration similarity. Literal object-for-object copying is not required when product concepts differ.

Initial collection evidence includes SailPoint configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions. The exact fields, sensitive-data classification, Veza mappings, execution coverage, and validation behavior still require detailed feature discovery.

Source credentials and passwords are prohibited evidence and must not be collected into product datasets or reports. Social Security numbers and other government identifiers must remain within the customer-controlled boundary or be redacted before approved evidence leaves that boundary. Customers may optionally run collection within their own environment.

For the first roles slice, the target outcome is a Veza Access Profile that preserves the role's effective approved entitlements, owners, and static SailPoint role members. The visible inheritance structure does not need to match when the effective entitlement outcome is preserved. Identities may be matched using email, employee ID, account ID, and configurable matching rules.

When an owner, member, entitlement, or source cannot be matched—or matching signals conflict—the Access Profile is created with a warning and a consultant task containing an automated suggested resolution. Membership derived from SailPoint role criteria is not migrated through the Joiner/Mover/Leaver capability; it is documented as a health-assessment finding only. If a matching Veza Access Profile already exists, the product requires a customer decision before changing it.

Consultant tasks are managed in the product and can also be handed off through supported integrations or export formats, initially including Jira, GitHub, ServiceNow, and CSV. Non-fatal unresolved warnings do not prevent direct configuration when the customer explicitly approves them.

Role validation checks the destination Access Profile, effective entitlements, owner, static members, and effective inheritance outcome. The five components carry equal weight. Results are presented as a percentage plus an easy-to-understand red/yellow/green status with component evidence. Red is below 50%, yellow is 50% through 80%, and green is above 80%. No individual role mismatch is automatically fatal; the customer can accept or reject the result through an explicit recorded decision.

For the subsequent access-profile slice, a SailPoint Access Profile maps to a Veza Access Profile. The migration covers name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings. A SailPoint Source maps to a Veza Integration using name, system type, and connection metadata. Missing or ambiguous Integration matches do not block profile creation; they produce warnings, automated suggestions, and consultant tasks. Existing matching Veza Access Profiles require a customer decision before change. Non-fatal warnings may proceed with customer approval, but an entitlement mismatch is fatal to Access Profile acceptance.

Certification discovery and migration must cover all campaign types supported by the source and destination, including identity, access-item, role, and application or Source-owner campaigns where supported. A SailPoint certification campaign maps to a Veza Access Review. Migration covers the campaign's name, description, type, scope, reviewers, schedule, deadlines, reminders, escalation, decision options, and remediation settings. Future campaigns include reusable templates and scheduled-but-not-started campaigns; customers can exclude selected objects from migration while retaining their documentation and health assessment. Completed history and reviewer decisions are not migrated. For an in-flight campaign, the active review round and decisions remain in SailPoint, but its campaign settings are migrated. A warning prevents triggering the corresponding Veza Access Review until the SailPoint round finishes; the warning does not create a consultant task. Existing matching Veza Access Reviews require a customer decision before change.

Certification validation evaluates Access Review existence/type at 10%, scope at 30%, reviewers at 30%, schedule/timing at 15%, and decision/remediation settings at 15%. It uses the same red/yellow/green thresholds as the other migration grades. Scope and reviewer mismatches remain fatal to acceptance.

Every SailPoint lifecycle workflow, including customized workflows, is in discovery scope for mapping to Veza Workflows within the LCM module. Migration mapping covers triggers, identity criteria, actions, approvals, and timing. Criteria-derived role membership is excluded from this mapping and remains a health-assessment concern.

For in-flight lifecycle workflows, definitions and settings may migrate while active executions finish in SailPoint. The corresponding Veza Workflow must not be triggered until the source execution finishes. A referenced system unsupported by Veza is fatal to workflow acceptance, must be logged, and must appear in the health assessment.

LCM validation weights workflow existence at 25%, triggers/identity criteria at 25%, and actions at 50%. Existing matching Veza Workflows require a customer decision before change. Custom scripts or rules receive an automated suggested replacement, a consultant task, and a health finding. Customers may exclude selected workflows from migration, but excluded workflows remain documented and health-assessed.

LCM uses the standard red/yellow/green thresholds. Unsupported referenced systems are the only automatically fatal JML mismatch. All other mismatches produce warnings, explanations, and consultant tasks.

SailPoint is always a read-only source, including during migration and validation.

## Desired outcomes

- Migration decisions are based on evidence rather than incomplete institutional knowledge.
- Product owners retain control over what is migrated.
- Source behavior, approved requirements, destination configuration, and validation evidence remain traceable.
- Avoidable security, reliability, and design problems are identified instead of blindly copied.
- Repeatable migration work can be delivered as a service for the named source and destinations.

## Non-goals for the current discovery scope

- Supporting every IGA source or destination in the first release.
- Selecting the application architecture or technology stack.
- Automatically approving migration scope, risk acceptance, or production changes.
- Assuming every source artifact should be reproduced in the destination.
- Defining exact connector capabilities before source and destination APIs and export mechanisms are validated.

## Candidate success measures

The primary pilot measures are discovery coverage and finding accuracy. Every supported, in-scope object must appear in the health assessment as assessed or with an explicit incomplete, inaccessible, unsupported, excluded, or not-yet-assessed state; no percentage target is currently set. More than 80% of reviewed non-indeterminate AI findings must be confirmed. Review includes every Critical and High AI finding plus a module/category/severity-stratified lower-severity sample, totaling at least 100 reviewed AI findings or all findings when fewer exist. Pilot acceptance uses at least two independent eligible One Identity Manager 10.x SQL Server environments and includes an initial assessment and reassessment. Rejected, indeterminate, and unreviewed findings are reported separately. Additional measures are hypotheses:

- percentage of in-scope source capabilities inventoried with provenance;
- percentage of reviewed findings confirmed as accurate, with rejected and indeterminate findings reported separately;
- percentage of approved migration requirements traceable to source evidence and destination validation;
- reduction in manual discovery and documentation effort;
- migration findings reviewed and dispositioned before execution;
- approved outcomes successfully validated in the destination;
- security, configuration, and operational issues identified before migration;
- manual intervention and failed/repeated migration operations.

## Key risks

- Source or destination products may not expose all required artifacts or operations.
- Product semantics may not map one-to-one between IGA platforms.
- Collected data may contain credentials, personal data, entitlements, or other sensitive information.
- Automated configuration or data movement could cause unintended access or governance changes.
- Generated documentation may appear authoritative despite incomplete evidence.
- Health recommendations could be incorrect without sufficient context or expert review.
- Destination-vendor differences may create separate migration products rather than one uniform workflow.

## Assumptions requiring validation

- Customers can grant sufficient authorized access to inspect their SailPoint environment.
- A meaningful portion of Veza and One Identity Manager configuration can be created or updated through supported interfaces.
- Business/governance behavior and meaningful configuration similarity can be evaluated even when source and destination concepts differ.
- Human review will gate migration scope, consequential remediation, execution, and acceptance.
- Findings can be stored and processed in a manner acceptable for customer security and compliance needs.
