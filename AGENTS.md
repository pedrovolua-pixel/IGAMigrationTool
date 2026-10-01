# Repository operating instructions

This repository is the source of truth. Do not rely on prior chat history when repository documentation exists.

## Navigate the repository

- Product intent and product-wide requirements: `/product`
- Feature specifications and templates: `/specs`
- Architecture and architectural decisions: `/architecture`
- Development guidance: `/docs/development`
- Operations guidance: `/docs/operations`
- Security guidance: `/docs/security`
- Cross-feature work plans: `/plans`
- Repeatable Codex workflows: `/skills`

Read the relevant documents before substantial work. Use a matching repository Skill when one exists.

## Control rules

1. Do not implement substantial product behavior without an approved product specification, technical specification, implementation plan, and test plan.
2. Do not invent product behavior, permissions, architecture, security policy, retention policy, or public contracts.
3. Follow accepted ADRs. Propose an ADR instead of silently changing architecture.
4. Prefer small, scoped, reversible changes; avoid unrelated refactoring.
5. Add dependencies only for a concrete, documented need.
6. Never commit credentials or secrets. Validate external input and enforce authorization server-side.
7. Update affected documentation in the same change as behavior, architecture, configuration, API, security, or operations changes.
8. Stop and escalate material ambiguity, contradictory specifications, possible data loss, destructive changes, security-boundary changes, or scope beyond the authorization given.

Human approval is required for product scope, consequential architecture or security decisions, and production release. Production or otherwise irreversible operations require explicit authorization.

## Evidence and completion

Run every applicable repository check before claiming completion: formatting, linting, type checking, unit tests, integration tests, end-to-end tests, security checks, and build. Report what ran, its result, what could not run, migrations/configuration changes, and unresolved risks. Never claim a check passed unless it was executed.

For substantial work, keep the canonical implementation plan and feature status current.

## Parallel pilot development

The repository owner approved a coordinator and up to three bounded parallel workers on 2026-10-01. For approved local pilot implementation, delegate independent work through [the parallel development workflow](docs/development/parallel-agent-workflow.md). Use isolated worktrees for writing workers, explicit path ownership and traceable work packets. The coordinator owns shared configuration, integration, canonical records, human reporting and the existing private site. Workers return executed evidence and receive independent review; agent review does not replace required human approval. Use fewer workers when tasks are dependent or cannot be safely isolated.

## Pilot status site

For One Identity health-assessment pilot work, also maintain the existing private [pilot status site](docs/development/pilot-status-site.md). The repository's approved plans, feature status, and evidence index remain authoritative; the site summarizes them for human review.

Whenever pilot implementation progress changes a canonical plan or feature status, or a gate, phase, milestone, or human dependency changes, update and publish the site's status and human tasks board in the same work cycle. Keep each open human task linked to the repository document or template that states the needed information, the requested role, and what completes the task. Close or change a task only when the canonical records support it. Do not report the site as current until the private deployment is confirmed; if publication is unavailable, report the stale snapshot and the reason. Never put customer evidence, credentials, protected identifiers, or raw SQL on the site.
