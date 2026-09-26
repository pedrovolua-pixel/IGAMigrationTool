---
name: security-review
description: Perform a scoped, evidence-based security review of consequential changes and document risks without silently changing policy or accepting residual risk.
---

# Review security-sensitive changes

Use this workflow for authentication, authorization, payments, secrets, uploads, personal data, administration, tenant isolation, cryptography, webhooks, or user-generated content.

1. Read `AGENTS.md`, the threat model and security documentation, approved specs, relevant architecture/ADRs, test plan, and implementation diff.
2. Identify assets, actors, trust boundaries, entry points, abuse cases, assumptions, and affected controls.
3. Inspect authentication/session behavior, authorization and isolation, input/output handling, injection risks, request forgery, file handling, callbacks, replay/idempotency, rate limits, secrets, sensitive logs, dependency exposure, auditability, retention/deletion, recovery paths, and administrative privilege as relevant.
4. Validate controls with executable evidence where practical. Record missing evidence as `NOT VERIFIED`.
5. Report findings by severity with evidence, exploit/impact reasoning, location, and recommended remediation. Record residual risks and the human owner needed to accept them.

Do not disclose real secrets, perform destructive testing against production, change security policy, or accept risk on the owner's behalf.
