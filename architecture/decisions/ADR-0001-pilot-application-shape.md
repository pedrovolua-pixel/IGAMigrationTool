# ADR-0001: Pilot application and work-execution shape

Status: Accepted  
Date: 2026-09-28  
Decision owners: Technical owner, security owner

## Context

The approved health-assessment pilot requires an interactive application, long-running and resumable assessment work, deterministic and AI analysis, immutable publication, exports, and later read-only MCP access. No application architecture or technology stack has been selected. The pilot must support strict authorization, complete auditability, an eight-hour assessment target at approved scale, and small, reversible delivery increments.

## Decision drivers

- Keep authorization, audit, state transitions, and scoring behavior consistent.
- Isolate long-running and failure-prone work from interactive requests.
- Support checkpoints, partial completion, safe retry, and bounded concurrency.
- Avoid premature distributed-system complexity during the pilot.
- Preserve a clear path to split independently scaling or higher-risk workloads later.

## Options considered

### Option A: Modular application with separate worker processes

- Advantages: one versioned domain model and policy layer; transactional state changes; simple deployment topology; asynchronous workers can scale independently; module boundaries can later become services.
- Disadvantages: requires discipline to preserve module boundaries; a poor worker implementation could contend with interactive workloads.
- Risks: process-level separation alone is insufficient for hostile-content and report-rendering isolation.

### Option B: Independent microservices from the first pilot release

- Advantages: strong deploy-time separation and independent scaling.
- Disadvantages: distributed authorization, data consistency, observability, and version compatibility add substantial pilot complexity.
- Risks: service boundaries would be selected before workload and ownership evidence exists.

### Option C: Single synchronous web application

- Advantages: smallest initial deployment surface.
- Disadvantages: unsuitable for assessments lasting hours, reliable checkpoints, bounded AI work, and report generation.
- Risks: request timeouts and retries could duplicate work or lose progress.

## Decision

Use a modular application for the pilot, with the same versioned codebase deployed as interactive API/UI processes and independently scalable asynchronous worker processes. Durable work items and checkpoints separate interactive operations from collection import, rule execution, AI analysis, scoring, report generation, export, deletion, and evaluation jobs.

Run untrusted-content processing, generated-code rendering, and export rendering in separately sandboxed worker execution contexts even though they remain part of the same application architecture. Define module boundaries around identity/authorization, projects and evidence, assessment orchestration, rules, AI analysis, findings/review, scoring, reporting/sharing, tasks, audit, and MCP.

The programming language, web framework, database product, queue product, object-store product, and hosting platform remain unselected and require implementation planning after this ADR is accepted.

## Rationale

This option provides the asynchronous behavior and operational isolation the pilot needs without committing the product to a distributed service topology before validated scale and team boundaries exist. It centralizes policy enforcement and immutable publication while retaining a migration path for workloads that later require independent security or scaling controls.

## Consequences

### Positive

- One policy and audit implementation governs UI, exports, workers, and MCP.
- Worker pools can be separately bounded by customer, job class, and AI budget.
- Transactional state transitions remain practical.
- Pilot deployment and recovery have fewer independently versioned components.

### Negative and trade-offs

- Module dependency rules need automated enforcement.
- Resource isolation requires separate process pools and sandbox controls.
- A later service extraction may require data ownership changes.

## Security, operations, and cost impact

- Interactive, assessment, AI, and rendering workloads use distinct worker identities and least-privilege permissions.
- Queued work carries opaque tenant and resource identifiers, never credentials or raw evidence payloads.
- Per-customer concurrency and rate limits prevent one assessment from exhausting shared capacity.
- Operations must monitor queue age, checkpoint age, retries, poison work, worker saturation, and customer-scoped failures without logging evidence.
- This shape favors lower pilot operating complexity than a microservice topology.

## Migration and reversibility

Start with explicit in-process module APIs and module-owned tables or schemas. A module can later be extracted behind the same operation contract and event/outbox boundary. Rollback deploys the previous compatible application version while workers use versioned work payloads and refuse unsupported payload versions.

## Validation

- Architecture tests reject prohibited module dependencies.
- Load tests demonstrate UI p95 targets while an approved-scale assessment is running.
- Recovery tests restart workers from checkpoints without duplicate active results.
- Security tests show rendering and AI workers cannot obtain unauthorized raw evidence.
- Deployment tests demonstrate mixed-version safe draining and rollback.

## References

- `specs/003-health-assessment/product-spec.md`
- `product/non-functional-requirements.md` NFR-REL-1 through NFR-REL-5, NFR-PER-1 through NFR-PER-4, and NFR-OBS-1 through NFR-OBS-4

## Approval

Accepted by: Repository owner  
Date: 2026-09-28
