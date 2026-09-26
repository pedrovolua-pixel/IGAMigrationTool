---
name: create-implementation-plan
description: Turn approved product and technical specifications into a traceable, executable implementation plan and test plan without implementing the feature.
---

# Create implementation and test plans

1. Read `AGENTS.md`, the feature's approved product and technical specs, relevant architecture/ADRs, and the implementation-plan and test-plan templates under `/specs/templates`.
2. Stop if unresolved questions materially change scope, architecture, security, data safety, or public contracts.
3. Decompose work into small vertical, reviewable milestones. Keep unrelated refactoring and speculative infrastructure out of scope.
4. Map work and tests to stable requirement and acceptance-criterion IDs. Include positive, negative, authorization/isolation, failure/recovery, compatibility/migration, accessibility, performance, and security coverage where relevant.
5. Give each milestone concrete verification evidence and identify approvals, dependencies, rollout, documentation, and rollback obligations.
6. Write or update `implementation-plan.md`, `test-plan.md`, and `status.md` in the feature directory.

Do not write product code. A plan is a maintained execution record, not disposable prose.
