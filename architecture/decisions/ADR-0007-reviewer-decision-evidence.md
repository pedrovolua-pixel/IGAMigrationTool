# ADR-0007: Signed reviewer decision evidence for pilot promotion

Status: Pilot implementation choice — milestone review pending  
Date: 2026-09-29  
Decision owners: Technical owner, security owner, operations owner

## Context

The approved implementation plan requires an artifact author not to be the sole normal promotion approver. Required reviewers vary by artifact and, for a query pack, by exact customer environment. Reviewer decisions must be digest-bound and available as restricted engineering evidence. The Milestone 0 machine-evidence preflight cannot establish reviewer identity or role.

## Decision drivers

- Bind a reviewer decision to one artifact digest and opaque scope without customer names or evidence payloads.
- Preserve explicit approval and rejection outcomes, decision time, reviewer identity and role.
- Reject a decision signed by an unknown, revoked, wrong-role or wrong-scope key.
- Keep reviewer trust outside bundle content and recheck it for each decision use.
- Leave activation and owner override to separately governed paths.

## Options considered

### Option A: Unsigned application review rows

Application records could be protected by database authorization and audit. This is simpler but does not give an independently verifiable reviewer evidence bundle for the restricted engineering store.

### Option B: Detached signed metadata-only decisions (pilot choice)

Use a strict versioned JSON bundle with only artifact digest, opaque scope/reviewer/role IDs, UTC decision time and `APPROVE` or `REJECT`. Sign its exact bytes with RSA-PSS/SHA-256. A protected reviewer-key registry binds the key to one reviewer, role, allowed scopes, validity window and revocation state. A review-coverage check consumes only freshly verified approvals for the same artifact and scope and requires every configured role plus at least one non-author approver.

## Pilot implementation choice

Proceed with Option B as an internal pilot draft under the repository owner's direction to develop through milestone review. The writer, verifier, in-memory trust snapshot, role-coverage check and combined normal-path evidence preflight are implemented with synthetic negative cases. Their results do not activate an artifact. The exact reviewer role requirements still come from trusted artifact policy, not from submitted bundles. The proof is valid only for the evaluation instant and must be reverified after trust changes.

The trusted registry is not yet backed by Entra assignments, protected key storage, revocation propagation or the restricted evidence store. The reviewer signature authenticates a key only when those operational mappings are verified. The owner override remains a separate explicit signed decision and is not represented as a reviewer approval.

## Consequences and validation

- A reviewer can sign an approval or rejection without placing free text, customer names or payloads in the bundle.
- Key issuance, identity proofing, role changes, rotation, compromise response and lost-key recovery become operations work.
- Before use, prove authorized and wrong-role/wrong-scope decisions against real identities; test revocation, key rotation, tampering, stale evidence and store retrieval.
- Normal promotion must still verify machine evidence, compatibility, all required approvals and a human-authorized activation event; repository merge or CI pass alone is insufficient.

## References

- `specs/003-health-assessment/implementation-plan.md` — artifact promotion and reviewer matrix
- `specs/003-health-assessment/test-plan.md` — promotion negative cases
- `docs/development/reviewer-decisions.md`
- `architecture/decisions/ADR-0005-gate-evidence-signing.md`
