---
name: write-technical-spec
description: Design a feature from an approved product specification while preserving documented architecture and surfacing consequential decisions for approval.
---

# Write a technical specification

1. Read `AGENTS.md`, the approved product spec, relevant `/product` requirements, `/architecture`, accepted ADRs, and `/specs/templates/technical-spec.md`.
2. Trace every functional requirement and acceptance criterion to the proposed design. Identify contradictions or blockers before proceeding.
3. Define only the components actually needed. Address contracts, data lifecycle, authorization, trust boundaries, failure modes, concurrency, idempotency, compatibility, migrations, observability, deployment, testing, security, privacy, and operations as relevant.
4. Compare credible alternatives for consequential choices. If the best design conflicts with architecture or creates a durable constraint, create a **Proposed** ADR from the repository template and request human review.
5. Write or update the feature `technical-spec.md`; keep unresolved questions and risks explicit and update `status.md`.

Do not implement code, choose unresolved product behavior, silently revise architecture, or mark the design `Approved`.
