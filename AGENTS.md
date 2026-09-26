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
