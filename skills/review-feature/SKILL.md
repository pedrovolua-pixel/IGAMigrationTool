---
name: review-feature
description: Independently review a feature implementation for requirement gaps, correctness, architecture, security, operations, and test adequacy before fixes begin.
---

# Review a feature independently

Read the approved product and technical specs, test plan, architecture, ADRs, and implementation diff. Do not begin from the implementer's conclusions.

Review for missing or misread requirements, authorization and isolation failures, validation gaps, races, transactions, idempotency, error handling, information leakage, migration/compatibility risk, observability gaps, weak tests, unnecessary complexity, and architectural violations.

Classify actionable findings as `BLOCKER`, `HIGH`, `MEDIUM`, or `LOW`. For each finding provide evidence, impact, a precise location, and a recommended correction. Distinguish confirmed defects from questions or residual risks. Map requirement gaps back to stable identifiers where possible.

Do not modify code during the initial review unless separately instructed. Do not manufacture findings merely to populate a report. State the review scope and anything not inspected or executed.
