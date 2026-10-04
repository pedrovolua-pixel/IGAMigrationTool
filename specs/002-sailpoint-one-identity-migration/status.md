# Feature Status: SailPoint to One Identity Manager migration

State: PRODUCT SPEC

Owner: Product owner

Last updated: 2026-09-27

Valid states: `IDEA`, `DISCOVERY`, `PRODUCT SPEC`, `PRODUCT APPROVED`, `TECHNICAL DESIGN`, `TECHNICAL APPROVED`, `PLANNED`, `IMPLEMENTING`, `CODE REVIEW`, `VERIFICATION`, `STAGING`, `ACCEPTANCE`, `RELEASE READY`, `RELEASED`, `OBSERVED`, `BLOCKED`.

## Completed

- Product discovery for SailPoint Sources, roles, Access Profiles, certifications, lifecycle workflows, One Identity recipes, non-production execution, validation, rollback, handoff, and MCP integration.
- Draft product specification created with stable `FR-OIM-N` and `AC-OIM-N` identifiers.
- Initial release boundary established at customer-accepted non-production execution and validation.

## In progress

- Product-owner review of the draft specification.
- Product-wide policy reconciliation for non-production consultant approval.

## Blocked

- Blocker: Approved `NFR-SAF-8` requires customer approval for every direct destination change, while this specification authorizes assigned-consultant approval and execution in non-production.
- Owner/decision needed: Product owner and security owner must approve the revised non-production/production authority boundary.

## Decisions made

- One Identity Manager 10 across on-premises, private-cloud, and hosted deployments: `product-spec.md` FR-OIM-1.
- SailPoint Source implementation recipes precede roles, Access Profiles, certifications, and lifecycle workflows: `product-spec.md` FR-OIM-2 and FR-OIM-10.
- Consultants approve and execute non-production work; customers accept the completed non-production result: `product-spec.md` FR-OIM-6 and FR-OIM-39.
- Production execution is deferred: `product-spec.md` FR-OIM-41.
- Native conditional mappings and complete prerequisite dependency graph: `product-spec.md` FR-OIM-11 through FR-OIM-30.
- Versioned, recoverable, idempotent execution and validation: `product-spec.md` FR-OIM-31 through FR-OIM-40.
- Configurable MCP access with granular permission and UI-equivalent authority: `product-spec.md` FR-OIM-42 through FR-OIM-47.

## Discoveries

- SailPoint Source mapping is an implementation recipe, not only an object mapping.
- Access Profiles may require system roles, IT Shop products, approval policy, account definitions, and manage levels.
- Safely representable membership criteria may use native dynamic roles, with duplicate workflow generation suppressed and warned.
- Direct database changes require enhanced preparation and recovery but remain allowed in non-production after consultant approval.
- Customers may use any standards-compatible MCP harness when validly authorized.

## Evidence

| Requirement / gate | Result | Evidence |
|---|---|---|
| Product discovery captured | PASS | `product-spec.md` |
| Product specification approved | NOT VERIFIED | Approval section is blank and status remains Draft |
| Product-wide approval policy consistent | FAIL | Conflict with `product/non-functional-requirements.md` NFR-SAF-8 |
| Destination interface feasibility | NOT VERIFIED | Technical validation has not started |
| Implementation and tests | NOT VERIFIED | Not authorized or started |

## Remaining work and verification

- Obtain product-owner review and approval or requested revisions.
- Reconcile and approve the non-production consultant authority change.
- Update product-wide vision, journeys, requirements, non-functional requirements, feature map, roadmap, principles, and open-question decision log through normal review.
- Validate One Identity 10 APIs, transports, supported administrative interfaces, package formats, and database-operation supportability.
- Create technical specification, implementation plan, test plan, threat model, and rollback design after product approval.
- Run separate destination discovery for One Identity Manager to Veza and later One Identity Manager to SailPoint.

## Next transition

- Target state: PRODUCT APPROVED
- Entry conditions: Product owner approves scope; security owner approves the revised environment-specific authority boundary; destination capability assumptions are accepted for technical validation.
- Required human approval: Product owner and security owner.
