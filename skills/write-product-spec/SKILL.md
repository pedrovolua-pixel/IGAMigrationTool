---
name: write-product-spec
description: Create or refine a substantial feature's product specification from explicit product-owner input before technical design or implementation.
---

# Write a product specification

Use this workflow for meaningful new or changed product behavior. Do not use it to make architecture or stack decisions.

1. Read `AGENTS.md`, approved material under `/product`, related feature specs, and `/specs/templates/product-spec.md`.
2. Establish the user problem, users, evidence, goals, non-goals, observable flows, permissions, validation, failure states, edge cases, accessibility, privacy, security, analytics, audit needs, dependencies, and acceptance criteria.
3. Reconcile contradictions with existing requirements; do not silently choose among them.
4. Assign stable `FR-N` and `AC-N` identifiers. Make criteria observable and testable.
5. Write `/specs/NNN-feature-name/product-spec.md` from the template. Record assumptions and unresolved questions explicitly.
6. Create or update the feature `status.md`, but stop before technical design when product approval is absent.

Do not invent requirements to fill sections. Do not mark the specification `Approved`; only the product owner can approve scope.
