---
name: implement-feature
description: Implement an approved feature from its canonical specifications and maintained plans, with scoped changes and executable verification evidence.
---

# Implement an approved feature

1. Read `AGENTS.md`, the feature's approved product and technical specs, implementation plan, test plan, status, relevant architecture, and accepted ADRs.
2. Stop for material contradictions, unresolved blocking decisions, destructive requirements, or scope beyond the approval given.
3. Work milestone by milestone. Keep changes small and in scope, add tests with behavior, update affected documentation, and keep the implementation plan and status current.
4. Do not reinterpret product behavior, permissions, public contracts, architecture, or security assumptions. Propose the appropriate spec or ADR change when needed.
5. Run every applicable repository check and inspect the final diff for unintended changes.
6. Report requirements completed, files/subsystems affected, exact checks and results, migrations, configuration changes, deviations, and unresolved risks.

Do not claim unexecuted checks passed. Implementation completion does not grant acceptance or production approval.
