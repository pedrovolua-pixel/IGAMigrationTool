# ADR-0002: Pilot tenant isolation model

Status: Accepted  
Date: 2026-09-28  
Decision owners: Technical owner, security owner

## Context

The product must support multiple consulting partners and customer organizations concurrently while preventing cross-customer disclosure. Authorization is scoped by partner assignment, customer organization, project, environment, assessment, action, and evidence category. Evidence, findings, reports, tasks, exports, audit history, AI processing, and MCP responses inherit these boundaries.

## Decision drivers

- Make customer isolation a structural control rather than a UI convention.
- Permit assigned-partner access without granting access to the partner's other customers.
- Apply identical authorization semantics to UI, workers, exports, sharing, and MCP.
- Support customer-specific retention, deletion, residency, and encryption policy.
- Keep pilot operations feasible with at least two independent customer environments.

## Options considered

### Option A: Shared database and shared object namespace with application filters

- Advantages: lowest operational overhead and easiest cross-tenant product analytics.
- Disadvantages: a missing filter can expose another customer; customer-specific purge and restore are difficult.
- Risks: does not provide sufficient defense in depth for the approved isolation requirement.

### Option B: Shared database with database row policies and tenant-prefixed objects

- Advantages: stronger enforcement than application filtering; moderate operating cost.
- Disadvantages: privileged roles and maintenance paths can bypass row policies; customer-specific restore and encryption remain coupled.
- Risks: policy drift across interactive, worker, export, and support paths.

### Option C: Shared control plane with a separate customer data plane

- Advantages: strong customer boundary; customer-specific encryption, retention, purge, and restore; smaller blast radius.
- Disadvantages: provisioning, migrations, support, and aggregate operations are more complex and costly.
- Risks: configuration drift between customer data planes if migrations are not centrally governed.

## Decision

Use a shared control plane for organizations, assignments, resource locators, deployment metadata, and non-sensitive service health. Place customer evidence and derived engagement data in a logically separate customer data plane with a dedicated database namespace or database, dedicated object-store namespace, and customer-specific encryption context. The implementation may pool infrastructure, but a request must resolve exactly one customer data plane before any customer-data access occurs.

Within a customer data plane, every resource is also scoped by project and environment. Authorization is deny-by-default and evaluated server-side from actor, customer assignment, project/environment scope, action, evidence category, and resource state. Background jobs use short-lived workload identities constrained to one customer data plane and job. Platform support access is time-bounded, reasoned, audited, and does not bypass evidence-category authorization.

## Rationale

The separate customer data plane materially reduces cross-customer disclosure and makes customer-specific retention, purge, encryption, and recovery testable. Keeping a shared control plane avoids duplicating global partner assignments and deployment coordination.

## Consequences

### Positive

- Cross-customer query paths are absent from normal customer-data operations.
- Customer deletion and restore can be tested without selecting rows from a global data set.
- Encryption and evidence policy can vary by customer.
- Worker and MCP scopes resolve to the same structural boundary.

### Negative and trade-offs

- Schema migrations must be orchestrated across all customer data planes.
- Cross-customer operational reporting must use privacy-safe control-plane metrics rather than customer records.
- Connection and resource counts increase with customers.

## Security, operations, and cost impact

- Customer data-plane identifiers are opaque and cannot be supplied directly by clients.
- Authorization tests include confused-deputy, identifier substitution, partner reassignment, revoked access, export, share-link, worker, support, and MCP cases.
- Backups, recovery, deletion, and monitoring preserve the customer boundary.
- The operating-cost increase is accepted provisionally in exchange for reduced pilot isolation risk; actual cost is measured before production architecture approval.

## Migration and reversibility

Provisioning and data access use an abstraction that can map a customer to a dedicated database or an isolated namespace. A future move to shared row-policy storage requires a new security review and migration ADR. A move from pooled isolated namespaces to dedicated customer infrastructure preserves the same logical contract.

## Validation

- Automated authorization matrices cover every actor and consequential operation.
- Cross-tenant penetration tests attempt direct identifiers, stale links, queued work, cached data, exports, and MCP access.
- Deletion and restore drills operate on one customer without exposing or modifying another.
- Schema migration tests detect and stop on customer data-plane drift.

## References

- `product/feature-map.md` cross-cutting foundation
- `product/non-functional-requirements.md` NFR-SEC-5, NFR-SEC-6, NFR-SEC-10, NFR-PRV-9, and NFR-OBS-4
- `specs/003-health-assessment/product-spec.md` permissions and security considerations

## Approval

Accepted by: Repository owner  
Date: 2026-09-28
