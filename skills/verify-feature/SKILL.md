---
name: verify-feature
description: Independently verify an implemented feature against its approved requirements and test plan using executable evidence, without fixing it unless separately asked.
---

# Verify a feature

Begin from the feature's approved product spec, acceptance criteria, technical spec, and test plan—not the implementation agent's summary.

1. Read relevant architecture and ADRs, then inspect the implementation and change set.
2. For every requirement and acceptance criterion record `PASS`, `FAIL`, or `NOT VERIFIED` with evidence.
3. Run applicable formatting, lint, type, unit, integration, end-to-end, security, migration, and build checks. Exercise negative and failure paths where practical.
4. Check authorization, isolation, validation, concurrency, idempotency, compatibility, data integrity, observability, accessibility, and rollback implications when relevant.
5. Update the evidence and remaining-verification sections of `status.md`. Report exact commands/results, environment limitations, and uncovered risk.

Do not manufacture findings or claim unexecuted checks passed. Do not modify implementation during an independent verification pass unless the user separately authorizes fixes.
