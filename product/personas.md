# Personas and Stakeholders

Status: Approved
Approved by: Product owner
Approved: 2026-09-26
Last updated: 2026-09-26

No user interviews have been completed. These approved role hypotheses remain subject to validation through product use and customer research.

## Migration Product Owner

Owns the business outcome and migration scope.

Needs to:

- understand current IGA use cases without reading raw configuration;
- review visualizations, requirements, findings, and gaps;
- choose what to migrate, improve, replace, or retire;
- approve acceptance criteria and definitions of done;
- see migration progress, exceptions, risks, and validation evidence.

Must control:

- approved future-state behavior;
- finding disposition and accepted risk;
- migration scope and acceptance.
- customer approval policy, including any explicitly configured situations in which per-change approval may be omitted.

## IGA Migration Analyst or Consultant

Investigates the source, interprets IGA semantics, prepares mappings, and supports destination design.

Needs to:

- collect and classify source evidence;
- correct or enrich inferred use cases;
- identify unsupported or ambiguous mappings;
- turn reviewed findings into requirements and delivery work;
- explain proposed destination outcomes to stakeholders.

Acts as the human refinement layer between AI-generated suggestions and customer approval.

## Destination Engineer or Administrator

Configures Veza or One Identity Manager and resolves execution issues.

Needs to:

- review proposed changes before execution;
- supply or authorize destination access;
- understand prerequisites, order, dependencies, and exceptions;
- retry or recover safely from partial failure;
- prove what was configured and why.

## Security or Governance Reviewer

Evaluates health findings, risky scripts/configuration, permissions, data handling, and control effectiveness.

Needs to:

- distinguish evidence from inference;
- assess severity, exposure, and remediation advice;
- approve or reject security-relevant changes and residual risk;
- confirm that risky source behavior is not reproduced unnoticed.

## Customer Health Assessment Owner

Uses the customer-operated product primarily to understand the quality, risk, and operational health of the existing IGA environment, whether or not a full migration immediately follows.

Needs to:

- run or authorize a bounded health assessment;
- understand collection coverage and data-handling implications;
- review findings with evidence, confidence, and remediation guidance;
- assign or disposition remediation work;
- compare assessment results over time where recurring assessment is supported.

## Migration Program or Delivery Lead

Coordinates scope, sequencing, readiness, dependencies, and delivery evidence across stakeholders.

Needs to:

- understand progress across the five phases;
- see blockers and decisions awaiting owners;
- export or integrate delivery artifacts into the team's work-management process;
- demonstrate readiness and completion.

## Executive Sponsor

Consumes the health-assessment and migration case to decide whether and how to fund remediation or migration.

Needs to:

- understand the current IGA estate without reading raw platform configuration;
- see material risks, inefficiencies, dependencies, and limitations;
- understand recommendation priority, expected outcomes, assumptions, and confidence;
- compare remediation and migration options using transparent ROI evidence;
- see which decisions, costs, and residual risks require sponsorship.
- consume the same canonical findings through an interactive dashboard or a portable PDF report.

## Platform or Service Operator

Operates the migration service and supports customer engagements.

Needs to:

- establish engagements and authorized connections safely;
- monitor collection, analysis, migration, and validation jobs;
- troubleshoot without unnecessary access to customer secrets or data;
- preserve audit evidence and enforce isolation and retention rules.

Must support configurable queue, event, audit, and error-log lookback periods without weakening customer data-boundary rules.

## Stakeholders requiring clarification

- How are responsibilities divided among consulting partners, destination-vendor professional services, and customer teams?
- Which roles are expected to use the application directly versus consume exported deliverables in each operating model?
- Who is authorized to approve destination changes and risk acceptance in each engagement?
- Are auditors, application owners, identity owners, or access reviewers direct users?
