---
name: release-review
description: Prepare an evidence-based release readiness review and identify unmet production gates without approving or deploying the release.
---

# Review release readiness

1. Read `AGENTS.md`, the release scope, canonical feature specifications/status files, operations and security documentation, accepted ADRs, and the release-readiness template.
2. Inspect CI and acceptance evidence independently. Treat missing evidence as `NOT VERIFIED`, not a pass.
3. Inventory migrations, configuration/secrets, infrastructure, external dependencies, security/privacy changes, documentation, deployment ordering, and compatibility constraints.
4. Evaluate staging verification, rollback feasibility, observability, alerts, backup/restore implications, runbooks, known issues, and post-deployment checks.
5. Create the review from `/docs/operations/release-readiness-review-template.md` at a release-specific path chosen by existing repository convention. If no convention exists, propose one rather than multiplying structures.
6. Classify readiness as `READY`, `NOT READY`, or `CONDITIONALLY READY`, with evidence and named conditions.

This workflow prepares a decision. It does not authorize production deployment, destructive operations, secret changes, or risk acceptance; those remain human-controlled.
