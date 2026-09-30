# Security documentation

This directory will hold the threat model, security policies, secrets handling, access-control rules, dependency policy, and security runbooks when the product and architecture make them concrete.

Security documentation must identify assets, actors, trust boundaries, abuse cases, controls, residual risks, and owners. Security-policy changes and risk acceptance require human approval. Never add real credentials or secret values here.

## Health-assessment pilot

- [`health-assessment-pilot-security-review.md`](./health-assessment-pilot-security-review.md) — scoped threat model, design review, open findings and required verification.
- [`health-assessment-authorization-matrix.md`](./health-assessment-authorization-matrix.md) — deny-by-default human and workload permissions.
- [`health-assessment-audit-policy.md`](./health-assessment-audit-policy.md) — approved 12-month payload-free audit lifecycle, integrity and access policy.
- [`health-assessment-identity-session-design.md`](./health-assessment-identity-session-design.md) — approved Microsoft Entra ID human, session, workload and lifecycle controls; implementation not verified.
- [`health-assessment-ai-data-controls.md`](./health-assessment-ai-data-controls.md) — AI data eligibility, provider controls, injection defenses, budgets and evaluation requirements.
