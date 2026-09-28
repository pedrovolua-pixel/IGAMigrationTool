# Feature Status: IGA health assessment

State: PRODUCT SPEC

Owner: Product owner

Last updated: 2026-09-28

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for assessment scope, rules, findings, scoring, desired outcomes, AI, recommendations, ROI, reports, collaboration, reassessment, quality, permissions, MCP, and phased delivery.
- Draft product specification created with stable `FR-HAS-N` and `AC-HAS-N` identifiers.
- Phase 1 through Phase 4 boundaries recorded.

## In progress

- Product-owner review of the draft product specification.
- Reconciliation with approved production-readiness requirements.

## Blocked

- Blocker: Verified restore/recovery is deferred to Phase 2, while approved `NFR-COM-2` requires verified restore before production readiness.
- Owner/decision needed: Product owner, security owner, and operations owner must reconcile whether Phase 1 is non-production/pilot-only or must include restore verification.

## Decisions made

- SailPoint and One Identity core assessment coverage: `product-spec.md` FR-HAS-1 through FR-HAS-6.
- Finding governance, scoring, and desired outcomes: `product-spec.md` FR-HAS-7 through FR-HAS-21.
- Rule catalog and product-specific assessment: `product-spec.md` FR-HAS-22 through FR-HAS-29.
- Phase 1 automatic AI and Phase 2 deep analysis: `product-spec.md` FR-HAS-30 through FR-HAS-34.
- Recommendations, ROI, and phase boundaries for fix packages: `product-spec.md` FR-HAS-35 through FR-HAS-39.
- Interactive reporting, reassessment, accuracy, benchmarking, MCP, and Phase 4: `product-spec.md` FR-HAS-40 through FR-HAS-56.

## Discoveries

- Health scoring includes proposed and accepted-risk findings and uses confidence as an input without changing displayed severity.
- Critical/High findings remain proposed until reviewed, but consultants may publish warned reports with incomplete review.
- General AI is automatic in Phase 1; raw-evidence access remains customer-authorized.
- Custom-rule management and anonymous peer benchmarking are Phase 3.
- Direct remediation and production migration are Phase 4.

## Evidence

| Requirement / gate | Result | Evidence |
|---|---|---|
| Product discovery captured | PASS | `product-spec.md` |
| Product specification approved | NOT VERIFIED | Approval section is blank and status remains Draft |
| Production restore/recovery gate | FAIL | Conflict with `product/non-functional-requirements.md` NFR-COM-2 |
| Rule catalog and AI quality validated | NOT VERIFIED | Technical and test design have not started |
| Implementation and tests | NOT VERIFIED | Not authorized or started |

## Remaining work and verification

- Obtain product-owner review and approval or requested revisions.
- Resolve Phase 1 restore/recovery and production-readiness conflict.
- Update product-wide documents through normal change review.
- Define source/version/module rule catalogs and exact scoring formula.
- Create technical specification, implementation plan, test plan, security review, accessibility plan, and evaluation plan after product approval.
- Specify Phase 2, Phase 3, and Phase 4 capabilities separately before implementation.

## Next transition

- Target state: PRODUCT APPROVED
- Entry conditions: Product owner approves scope and phased delivery; production-readiness conflict is resolved.
- Required human approval: Product owner, security owner, and operations owner for the restore/recovery decision.
