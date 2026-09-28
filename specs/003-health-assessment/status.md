# Feature Status: IGA health assessment

State: PRODUCT APPROVED

Owner: Product owner

Last updated: 2026-09-28

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for assessment scope, rules, findings, scoring, desired outcomes, AI, recommendations, ROI, reports, collaboration, reassessment, quality, permissions, MCP, and phased delivery.
- Draft product specification created with stable `FR-HAS-N` and `AC-HAS-N` identifiers.
- Phase 1 through Phase 4 boundaries recorded.
- Platform restore, recommendation guidance, and executed rollback concerns separated into distinct NFRs.
- Phase 1 pilot boundary refined for consultant-operated One Identity Manager 10.x on SQL Server with read-only database ingestion.
- Pilot sub-phases 1A through 1D, validated modules, reassessment, evaluation sampling, report delivery, fix packages, consultant tasks, and read-only MCP boundaries recorded.
- Pilot operational, accessibility, performance, scale, retention, recovery, and support targets recorded in the draft specification.
- Product specification and reconciled pilot scope approved by the repository owner acting as product and security owner on 2026-09-28.

## In progress

- Technical specification, architecture, capability matrix, scoring design, rule-catalog design, and evaluation design.

## Blocked

- No unresolved product-scope or security-owner decision is recorded. Technical design and later gates remain outstanding.

## Decisions made

- General SailPoint and One Identity product coverage remains intended, while the pilot validates One Identity Manager 10.x on SQL Server first: `product-spec.md` Phase 1 pilot boundary and FR-HAS-1 through FR-HAS-6.
- Finding governance, scoring, and desired outcomes: `product-spec.md` FR-HAS-7 through FR-HAS-21.
- Rule catalog and product-specific assessment: `product-spec.md` FR-HAS-22 through FR-HAS-29.
- Phase 1 automatic AI and Phase 2 deep analysis: `product-spec.md` FR-HAS-30 through FR-HAS-34.
- Pilot recommendations, grouped fix packages, in-product consultant tasks, CSV export, and ROI exclusion: `product-spec.md` FR-HAS-35 through FR-HAS-39.
- Interactive reporting, reassessment, accuracy sampling, benchmarking boundary, read-only MCP sub-phase, and Phase 4: `product-spec.md` FR-HAS-40 through FR-HAS-56.

## Discoveries

- Proposed AI findings affect only a provisional score; published scores use reviewed and deterministic auto-confirmed results. Accepted risk continues to reduce health.
- Critical/High findings remain proposed until reviewed, but consultants may publish warned reports with incomplete review.
- General AI is automatic in Phase 1 and limited to normalized, redacted evidence; AI raw-evidence retrieval is deferred.
- Custom-rule management and anonymous peer benchmarking are Phase 3.
- Direct remediation and production migration are Phase 4.
- Pilot acceptance requires at least two independent eligible environments, an initial assessment and reassessment, review of every Critical/High AI finding plus the defined stratified sample, and greater than 80% confirmed accuracy.

## Evidence

| Requirement / gate | Result | Evidence |
|---|---|---|
| Product discovery captured | PASS | `product-spec.md` |
| Product specification approved | PASS | Product owner approval recorded in `product-spec.md` on 2026-09-28 |
| Pilot scope decisions captured | PASS | `product-spec.md` Phase 1 pilot boundary; FR-HAS-1 through FR-HAS-56; AC-HAS-15 through AC-HAS-20 |
| Product-wide reconciliation | PASS | Vision, journeys, requirements, feature map, roadmap, open questions, and NFRs distinguish pilot scope from later product scope |
| Recovery concerns separated | PASS | Platform restore: NFR-REL-3/4; recommendation guidance: NFR-SAF-16; executed rollback: NFR-SAF-6 |
| Rule catalog and AI quality validated | NOT VERIFIED | Technical and test design have not started |
| Implementation and tests | NOT VERIFIED | Not authorized or started |

## Remaining work and verification

- Define the One Identity 10.x SQL Server capability matrix, database evidence contract, source/version/module rule catalogs, maturity rubric, and exact scoring formula.
- Create technical specification, implementation plan, test plan, security review, accessibility plan, and evaluation plan after product approval.
- Specify Phase 2, Phase 3, and Phase 4 capabilities separately before implementation.

## Next transition

- Target state: TECHNICAL DESIGN
- Entry conditions: Approved product specification and reconciled product-wide requirements.
- Required human approval: Technical owner approval after technical specification, architecture, security, accessibility, evaluation, and test design are complete.
