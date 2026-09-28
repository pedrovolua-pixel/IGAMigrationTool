# Product Discovery Questions

Status: Open
Owner: Product owner
Discovery baseline approved: 2026-09-26
Health pilot decisions approved: 2026-09-28
Last updated: 2026-09-28

The product owner approved the current discovery baseline on 2026-09-26 with these questions explicitly open. Answers may materially change scope, requirements, security boundaries, or delivery order. Record decisions here and update the affected canonical documents through normal change review.

## Priority 1 — Product and customer model

1. **Resolved:** Initial buyers are consulting partners and Veza or One Identity professional services teams.
2. **Resolved:** Support both professional-services-led managed delivery and customer-operated use; customer use centers primarily on health assessment.
3. **Resolved:** Veza is the first destination for an end-to-end vertical slice.
4. **Resolved:** Initial use-case delivery order is roles, access profiles, certifications, then Joiner/Mover/Leaver workflows.
5. **Resolved:** Evaluate the same outcome through business/governance behavior and configuration similarity.

## Priority 1 — Scope and authority

6. **Resolved for initial scope:** Collect configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions. Exact fields and depth remain open.
7. **Resolved for the health pilot:** Source credentials and passwords are prohibited evidence. Social Security numbers and government identifiers remain customer-side or are redacted. Necessary ordinary business identity attributes may remain identifiable; unrelated personal fields are excluded. Broader product classification remains open.
8. **Resolved:** AI suggests, the consulting company refines, and the customer approves. Per-action customer approval may be omitted for read-only collection, documentation generation, health analysis, and migration-package generation. Direct configuration and data movement still require customer approval.
9. **Resolved for collection:** Customer-side collection is optional. Whether customer-side migration execution is required remains open.
10. **Resolved:** SailPoint must remain read-only during every phase, including migration and validation.

## Priority 1 — Destination and migration behavior

11. **Partially resolved:** The first roles slice produces a Veza Access Profile preserving effective approved entitlements, owners, and static members. Identity matching uses email, employee ID, account ID, and configurable rules. Ambiguous or unmatched references create warnings and consultant tasks with automated suggestions. Criteria-derived membership remains a health-assessment finding and is not migrated through Joiner/Mover/Leaver scope. Existing profiles require a customer decision. Non-fatal warnings may proceed with customer approval. Validation uses five equally weighted components and displays a percentage plus red/yellow/green status; no mismatch is automatically fatal. Red is below 50%, yellow is 50% through 80%, and green is above 80%.
12. Which concrete One Identity Manager capabilities must the first release configure or validate?
13. **Partially resolved:** SailPoint Roles and Access Profiles map to Veza Access Profiles; SailPoint Sources map to Veza Integrations using name, system type, and connection metadata. Access Profile coverage includes name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings. Existing profiles require a customer decision. Missing or ambiguous Integrations produce warnings, suggestions, and consultant tasks without blocking creation. Non-fatal warnings may proceed with customer approval; entitlement mismatch is fatal to acceptance. Exact direct-movement versus destination-discovery behavior remains open.
14. **Resolved:** Support both direct destination configuration and reviewed migration-package generation.
15. What rollback or recovery promise must be made if destination configuration partly succeeds?

Certification scope decision: support all campaign types available across the applicable source and destination, including identity, access-item, role, and application or Source-owner campaigns where supported. SailPoint campaigns map to Veza Access Reviews. Future scope includes templates and scheduled-but-not-started campaigns, with customer-selected exclusions that remain documented and health-assessed. Completed history and reviewer decisions do not migrate. For in-flight campaigns, the active round remains on SailPoint while settings migrate; warn not to trigger the destination review until the source round finishes, without creating a task. Existing Access Reviews require customer decisions. Validation uses standard thresholds with weights of 10% existence/type, 30% scope, 30% reviewers, 15% schedule/timing, and 15% decision/remediation; scope and reviewer mismatches are fatal.

Joiner/Mover/Leaver destination decision: map every SailPoint lifecycle workflow, including custom workflows, to Veza Workflows within the LCM module. Cover triggers, identity criteria, actions, approvals, and timing. Criteria-derived role membership remains health-assessment-only. In-flight settings may migrate while active executions finish in SailPoint; hold destination triggers. Unsupported referenced systems are the only fatal mismatch, are logged, and are included in health assessment. Validation weights Workflow existence at 25%, triggers/criteria at 25%, and actions at 50% with standard thresholds. Existing workflows require customer decisions. Custom scripts/rules receive suggestions, consultant tasks, and health findings. Excluded workflows remain documented and health-assessed. Every other mismatch produces a warning, explanation, and consultant task.

## Priority 2 — Documentation and visualization

16. **Resolved:** Provide process flows, relationship graphs, data-flow diagrams, coverage maps, dependency views, and migration-status dashboards.
17. **Partially resolved for consultant tasks:** Support in-product tasks and handoff through Jira, GitHub, ServiceNow, CSV, and later integrations. Delivery-story schemas and integration depth remain open.
18. **Resolved at canonical-source level:** Maintain documentation canonically in the product and generate PDF and Markdown exports. External synchronization beyond defined task integrations remains open.
19. What level of human editing and approval workflow is required?

## Priority 2 — Health assessment

20. **Resolved at source-policy and pilot-governance level:** Use all applicable authoritative sources, including vendor guidance, OWASP/CIS where applicable, software-development best practices, and customer rules. Every applicable pilot domain/module requires an approved rule or explicit gap. Exact rules remain technical/catalog work.
21. **Resolved:** AI proposes findings, consultants confirm or refine them, and customers accept residual risk.
22. **Resolved for initial scope:** Health assessment produces recommendations and reviewable fix packages. In health-only mode, configured packages can become consultant tasks. Initial phases do not execute remediation directly; approved remediation execution is a later roadmap capability.
23. **Resolved:** Health assessment must support customer-operated use independently of a full migration workflow, but the first pilot is consultant-operated and focuses on One Identity Manager 10.x on SQL Server. Every collected object and supported evidence category remains represented; commercial packaging remains open.
24. **Resolved for pilot:** Queue, DBQueue, synchronization, event, audit, and error-log lookback defaults to 90 days, is configurable per engagement, and remains subject to source availability and customer data-boundary rules.

## Priority 2 — Validation and success

25. Which outcomes can be tested automatically, and which require customer acceptance or expert review?
26. Is validation limited to configuration equivalence, or must it test live governance workflows and downstream effects?
27. **Resolved for pilot:** Prioritize discovery coverage and finding accuracy. Every supported, in-scope object must appear with an assessment or explicit gap; no coverage percentage target is set. Review every Critical/High AI finding plus a stratified lower-severity sample totaling at least 100 reviewed AI findings or all when fewer exist. More than 80% of reviewed non-indeterminate AI findings must be confirmed.
28. What error rate or confidence threshold is acceptable for generated use cases and findings?

## Priority 3 — Service operations and commercial constraints

29. **Resolved:** Support multiple consulting partners and customer organizations concurrently with strict isolation. Partner administrators may access assigned customers; customer administrators remain in their own organization.
30. **Partially resolved:** Data residency, retention, deletion, and export policies are configurable per customer. Default retention is 30 days with a configurable start event. Expiry causes soft deletion; pilot active-system purge occurs within 30 additional days. Initial residency is United States only. Backup expiration remains technical/operational work.
31. **Resolved for pilot scale:** Support at least 100,000 identities, accounts, entitlements, roles, workflows, and records within the 90-day operational-evidence window, measured independently by category. Other category/engagement targets remain open.
32. **Resolved for pilot:** Support hours are 9:00 a.m.–5:00 p.m. Eastern Time on United States business days. Response targets are four business hours for critical incidents, one business day for high-priority issues, and three business days for normal issues. Production maintenance and after-hours behavior remain open.
33. Are vendor API licensing, rate limits, certification, partnership, or deployment restrictions already known?

## Decision log

| Date | Question | Decision | Owner | Documents updated |
|---|---|---|---|---|
| 2026-09-26 | 1 | Initial buyers are consulting partners and Veza or One Identity professional services teams. | Product owner | vision, personas |
| 2026-09-26 | 2, 23 | Support managed and customer-operated modes; direct customer use centers on standalone health assessment. | Product owner | vision, personas, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 3 | Veza is the first destination. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 4 | Initial delivery order is roles, access profiles, certifications, then Joiner/Mover/Leaver workflows. | Product owner | vision, requirements, feature map, roadmap |
| 2026-09-26 | 14 | Support direct configuration and reviewed migration-package generation. | Product owner | journeys, requirements, feature map, roadmap |
| 2026-09-26 | 5 | Assess equivalence using business/governance behavior and configuration similarity. | Product owner | vision, principles, requirements, feature map |
| 2026-09-26 | 6 | Initial evidence covers configuration, Sources, entitlements, transforms/rules/scripts, and workflow definitions. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 7 | Credentials and passwords are prohibited; Social Security numbers and government identifiers remain customer-side or are redacted. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 8 | AI suggests, consulting refines, customer approves; approval may be skipped only for read-only collection, documentation, health analysis, and migration-package generation. | Product owner | principles, personas, journeys, requirements, non-functional requirements, feature map |
| 2026-09-26 | 9 | Customer-side collection is optional. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Map SailPoint roles to Veza Access Profiles preserving effective entitlements, owners, and static members; retain criteria-derived membership as a health-assessment finding only. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 10 | SailPoint remains read-only during collection, migration, validation, retry, and recovery. | Product owner | vision, principles, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Match identities using email, employee ID, account ID, and configurable rules; unmatched references create warnings and consultant tasks. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Do not turn criteria-derived role membership into LCM migration requirements; retain it in health assessment. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 11 | Require a customer decision before changing an existing matching Veza Access Profile. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 11 | Conflicting identity matches create consultant tasks with automated suggestions rather than automatic selection. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Preserve effective entitlement outcomes; the visible inheritance structure may differ. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 17 | Support in-product consultant tasks plus Jira, GitHub, ServiceNow, CSV, and later integrations. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Non-fatal unresolved warnings may proceed with explicit customer approval. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 11 | Validate Access Profile existence, effective entitlements, owner, static members, and effective inheritance; provide an explainable customer-accepted grade for mismatches. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | Weight the five role-validation components equally and present a percentage plus red/yellow/green status: red below 50%, yellow from 50% through 80%, green above 80%. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | 11 | No role-validation mismatch is automatically fatal; the customer accepts or rejects the graded outcome. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Map SailPoint Access Profiles to Veza Access Profiles and SailPoint Sources to Veza Integrations. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Cover name, description, owner, Source, entitlements, requestability, approval settings, and revoke settings; match Integrations by name, system type, and connection metadata. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Require a customer decision before changing an existing matching Veza Access Profile. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Validate Access Profiles with equal weight for existence, Integration, entitlements, owner, and configuration settings. | Product owner | journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Missing or ambiguous Integrations create warnings, automated suggestions, and consultant tasks; non-fatal warnings may proceed with customer approval. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | 13 | Access Profile entitlement mismatch is fatal to acceptance regardless of aggregate grade. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Certification scope | Support every campaign type available across the applicable source and destination. | Product owner | vision, requirements, feature map, roadmap |
| 2026-09-26 | Certification mapping | Map SailPoint certification campaigns to Veza Access Reviews, covering name, description, type, scope, reviewers, schedule, deadlines, reminders, escalation, decision options, and remediation settings. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification history | Migrate settings and future campaigns; completed history and reviewer decisions do not migrate. For in-flight campaigns, migrate settings but keep the active round on SailPoint. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Certification validation | Scope and reviewer mismatches are fatal to Access Review acceptance. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Certification validation | Validate existence/type, scope, reviewers, schedule/timing, and decision/remediation settings; weight scope and reviewers more heavily. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification conflict | Require a customer decision before changing an existing matching Veza Access Review. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification future scope | Include templates and scheduled-but-not-started campaigns, with customer-selected object exclusions. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification in flight | Report in-flight campaigns with a warning to finish them in SailPoint; do not create consultant tasks. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Certification validation | Use standard grade thresholds and weights of 10% existence/type, 30% scope, 30% reviewers, 15% schedule/timing, and 15% decision/remediation. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification exclusions | Excluded campaign objects remain documented and health-assessed. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Certification in flight | Migrate campaign settings, but warn not to trigger the destination review until the SailPoint round finishes. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Map SailPoint lifecycle behavior to Veza Workflows within the LCM module. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Cover all lifecycle workflows, including custom workflows, and map triggers, identity criteria, actions, approvals, and timing. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Migrate in-flight workflow definitions/settings while active executions finish in SailPoint; hold destination triggering until completion. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Treat systems unsupported by Veza as fatal; log them and include them in health assessment. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Validate Workflow existence at 25%, triggers/identity criteria at 25%, and actions at 50%. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Require a customer decision before changing an existing matching Veza Workflow. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | For custom scripts/rules, generate a suggested replacement, create a consultant task, and include a health finding. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Allow workflow exclusions while retaining documentation and health assessment. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Health assessment | Assess every collected object and supported evidence category regardless of migration selection. | Product owner | vision, principles, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Joiner/Mover/Leaver | Use standard grade thresholds; unsupported systems are the only fatal mismatch, while all others create warnings, explanations, and consultant tasks. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Health assessment | Use all applicable authoritative sources and provide severity plus AI confidence for findings. | Product owner | vision, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Health report | Produce a sponsor-readable current-state report with recommendations, fix packages, and transparent ROI. | Product owner | vision, personas, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Health-only workflow | Allow configured fix packages to become consultant tasks without enabling migration. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Health ROI | Include labor/time, licensing, audit effort, risk reduction, avoided incidents, migration cost, and remediation cost when evidence supports them. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Health outputs | Provide interactive dashboards, PDF, and agent-friendly Markdown from the same canonical assessment. | Product owner | vision, personas, journeys, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Finding governance | AI proposes, consultants confirm/refine, and customers accept residual risk. | Product owner | vision, journeys, requirements, open questions |
| 2026-09-26 | Health remediation | Do not execute remediation directly in initial phases; retain it as a separately governed roadmap capability. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Health evidence history | Configure queue, event, audit, and error-log lookback per engagement. | Product owner | vision, personas, journeys, requirements, open questions |
| 2026-09-26 | Visualization | Provide process, relationship, data-flow, coverage, dependency, and migration-status views. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Documentation source | Maintain documents canonically in-product and generate PDF/Markdown exports. | Product owner | vision, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Tenancy | Support concurrent consulting partners and customer organizations with strict isolation. | Product owner | vision, requirements, non-functional requirements, feature map |
| 2026-09-26 | Customer data policy | Make residency, retention, deletion, and export policies configurable per customer. | Product owner | vision, requirements, non-functional requirements, feature map |
| 2026-09-26 | Pilot metrics | Prioritize discovery coverage and finding accuracy. | Product owner | vision, requirements, feature map, roadmap |
| 2026-09-26 | Discovery coverage | Include every supported, in-scope object in health assessment with an assessment or explicit gap state; do not set a percentage target yet. | Product owner | vision, journeys, requirements, feature map, roadmap |
| 2026-09-26 | Data retention | Use a 30-day default retention period; retention-clock start and deletion target remain open. | Product owner | vision, requirements, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Administrator scope | Partner administrators access assigned customers; customer administrators remain within their own organization. | Product owner | vision, journeys, requirements, feature map |
| 2026-09-26 | Initial scale | Support at least 100,000 identities, accounts, entitlements, roles, workflows, and log records per customer. | Product owner | vision, non-functional requirements, feature map, open questions |
| 2026-09-26 | Finding accuracy | Pilot target is greater than 80% confirmed among consultant-reviewed AI findings. | Product owner | vision, requirements, feature map, roadmap |
| 2026-09-26 | Retention start | Make the event that starts the retention clock configurable. | Product owner | vision, requirements, non-functional requirements, feature map |
| 2026-09-26 | Deletion | Soft-delete data at retention expiry; permanent-purge timing remains open. | Product owner | vision, non-functional requirements, feature map, roadmap |
| 2026-09-26 | Availability | Initial availability is business hours. | Product owner | vision, non-functional requirements, open questions |
| 2026-09-26 | Residency | Initial data residency is United States only. | Product owner | vision, requirements, non-functional requirements, feature map |
| 2026-09-28 | Health pilot source | Validate One Identity Manager 10.x on SQL Server through a dedicated read-only database account before other source/version combinations. | Product and security owner | requirements, feature map, roadmap, health and ingestion specs |
| 2026-09-28 | Health pilot modules | Validate the approved foundation plus Business Roles, System Roles, IT Shop, Attestation, Compliance Rules, Company Policies, Risk Assessment, Application Governance, reporting/data archiving, password management, AD, Entra ID, Exchange, and generic custom-module checks. | Product owner | health spec, requirements |
| 2026-09-28 | Health pilot phases | Deliver evidence foundation, assessment/scoring/tasks, review/reporting/evaluation, then read-only MCP as pilot sub-phases. | Product owner | feature map, roadmap, health spec |
| 2026-09-28 | Health pilot operating model | Use consultant-operated assessment with customer evidence authorization, review, acknowledgment, and residual-risk authority. | Product owner | requirements, roadmap, health spec |
| 2026-09-28 | Health pilot ROI and tasks | Exclude ROI; include grouped fix packages, in-product consultant tasks, and CSV export; defer external task creation. | Product owner | vision, journeys, requirements, feature map, roadmap, health spec |
| 2026-09-28 | Pilot AI and scoring | Run deterministic rules and automatic AI; proposed AI affects only a provisional score; published scores use reviewed and deterministic auto-confirmed results; gaps reduce a separate quality measure. | Product owner | requirements, health spec |
| 2026-09-28 | Pilot evaluation | Require two independent eligible environments, an initial assessment and reassessment, all Critical/High AI findings plus the defined stratified sample, and greater than 80% confirmed accuracy. | Product owner | vision, requirements, feature map, roadmap, health spec |
| 2026-09-28 | Pilot access exception | Warn and audit excess read-only permissions without blocking; block write, DDL, ownership, and administrative capability. | Product and security owner | non-functional requirements, ingestion and health specs |
| 2026-09-28 | Pilot operations | Set 90-day operational lookback, 30-day post-soft-delete active purge, 24-hour RPO, one-business-day RTO, Eastern business-hours support targets, WCAG 2.2 AA, and pilot performance/scale targets. | Product and security owner | vision, non-functional requirements, health spec |
