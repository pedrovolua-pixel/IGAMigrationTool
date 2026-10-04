# ADR-0008: Signed pilot-owner override evidence

Status: Pilot implementation choice — milestone review pending  
Date: 2026-09-29  
Decision owners: Repository owner, technical owner, security owner, operations owner

## Context

The approved implementation plan permits the repository owner to waive any internal artifact-promotion review or evidence gate for one pilot activation. A waiver must keep failed and missing checks visible, bind the exact artifact and target, carry a reason and known risk, expire no later than the pilot, and never change customer-source permission or runtime safety boundaries. The normal reviewer approval bundle cannot represent this exception.

## Decision drivers

- Verify one owner's explicit decision independently from CI and reviewer approval.
- Bind the exact digest, opaque customer/environment/capability scope and precise waived checks.
- Keep free-text rationale and known-risk detail out of the metadata-only evidence index.
- Bound the decision by a trusted pilot end time and a per-decision expiry.
- Make the exception a separate operator-reviewed path with no persistent bypass switch.

## Options considered

### Option A: Mutable override flag

A database flag is simple but does not bind one signed decision to the artifact, target, waived evidence and expiry. It risks becoming a persistent global bypass.

### Option B: Detached signed decision and restricted rationale (pilot choice)

Use a strict versioned metadata bundle signed with RSA-PSS/SHA-256. It names the exact artifact digest, owner, opaque scope and target IDs, sorted waived-check IDs, suspension target, decision/expiry times and SHA-256 digest of a separately protected rationale. That rationale records the reason, known risk and compensating controls, if any. The protected operator retrieves both and compares the digest. The evidence index records only references and factual `FAIL`/`NOT VERIFIED` results.

## Pilot implementation choice

Proceed with Option B as a local draft under the repository owner's instruction to continue development through milestone review. `PilotOwnerOverrideBundle` creates and verifies the signed metadata and binds the rationale bytes. `OwnerOverrideTrustRegistry` checks a protected caller-supplied key snapshot for owner ID, allowed scope, signing window and revocation. The verifier requires exact expected values from a trusted caller and a trusted pilot end time. Its `ValidForOperatorReview` result is only evidence for an operator; it does not activate an artifact, create an audit event or establish production identity by itself.

The signer key must be bound to the current repository owner through protected identity and key governance, including revocation. The actual rationale format, sanitizer, restricted storage, owner identity lookup, operator authorization, customer-visible exception, product audit, expiry shutdown and rollback/suspension execution are not implemented. No waiver can bypass runtime security or pilot acceptance controls.

## Consequences and validation

- Each exception has a separate signed bundle and a concrete expiration.
- The operator must compare the actual failed/missing check set with the signed waiver set; an unlisted failure continues to block normal promotion.
- Tests must reject wrong artifact/target/owner, missing or changed rationale, changed waiver set, wrong signer, schema drift, future or expired decision and expiry beyond the pilot end.
- Live identity, storage, audit and activation tests are required before this path is enabled.

## References

- `specs/003-health-assessment/implementation-plan.md` — pilot owner override
- `docs/development/pilot-owner-overrides.md`
- `architecture/decisions/ADR-0005-gate-evidence-signing.md`
