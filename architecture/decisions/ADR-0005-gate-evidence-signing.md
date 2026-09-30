# ADR-0005: Gate-evidence signing and trust source

Status: Accepted  
Date: 2026-09-29  
Decision owners: Technical owner, security owner

## Context

The approved implementation plan requires signed, digest-bound gate evidence in a restricted East US 2 engineering store. The Milestone 0 prototype verifies a detached RSA-PSS/SHA-256 signature over a strict metadata-only bundle and now has a local in-memory trusted-public-key snapshot with gate scope, validity-window and revocation checks. It is not wired to a production signer, protected trust source, evidence store, reviewer workflow, or artifact promotion path. Key ownership, rotation operations and revocation propagation still need a reviewed design before production integration.

## Decision drivers

- Detect tampering and bind check results to one gate, commit, and artifact digest.
- Keep signing authority separate from untrusted bundle content and customer data.
- Verify evidence after key rotation without accepting a revoked or wrong-scope signer.
- Preserve the approved 12-month gate-decision evidence lifecycle.
- Avoid putting signing private keys or customer payloads in Git or ordinary CI output.

## Options considered

### Option A: RSA-PSS/SHA-256 detached signatures

- Advantages: matches the executable prototype and supports verification with a public key that can be distributed to authorized reviewers.
- Disadvantages: requires an explicit key registry, signer identity, rotation policy, and trust-removal behavior.
- Risks: accepting a key supplied by the bundle, stale trust entry, or overly broad signing authority would defeat the gate boundary.

### Option B: ECDSA P-256/SHA-256 detached signatures

- Advantages: asymmetric verification with smaller signatures.
- Disadvantages: changes the prototype and still requires the same key governance and scope controls.
- Risks: inconsistent signature encoding across producers and consumers could cause failed verification or unsafe fallbacks.

## Decision

Select Option A: RSA-PSS/SHA-256 detached signatures over the exact bytes of a versioned, metadata-only gate-check bundle. The verifier obtains the public key from a trusted caller, never from bundle content, and binds the signed fields to the expected gate, commit, artifact digest, decision time and required check set. This approval covers the signing profile and trust-boundary direction represented by the internal prototype.

The signing authority, protected public-key source, key identity and scope binding, rotation/revocation operations, reviewer attestations, and restricted-store integration require concrete technical and security review before production use. No G1–G9 result or artifact promotion may rely on the prototype before those controls and their tests pass.

## Rationale

The repository already requires signed evidence but does not select the trust source or signing profile. Recording this choice before wiring a production signer prevents a local test key or bundle-provided key from becoming an implicit trust anchor.

## Consequences

### Positive

- The eventual gate verifier can bind evidence to the exact commit and artifact it reviews.
- Trust and rotation requirements become explicit and testable.

### Negative and trade-offs

- Key governance and reviewer attestations add implementation and operations work before a gate can pass.
- The current prototype may need replacement if another signing profile is accepted.

## Security, operations, and cost impact

- Private signing capability must be limited to the approved producer and kept in the managed secret/key boundary.
- Verification must obtain the authorized key from trusted configuration, check key scope and validity, and reject unknown or revoked keys.
- Decision bundles remain metadata-only and follow the separate engineering-evidence retention policy.

## Migration and reversibility

Keep the prototype disconnected from production gate decisions. If a different option is accepted, replace the verifier behind its internal module boundary and rerun negative/tamper tests. Historical bundles must retain enough algorithm and key identity metadata for authorized verification without trusting those fields as authority.

## Validation

- Reject unsigned, altered, wrong-key, wrong-gate, wrong-commit, wrong-artifact, expired and missing-check bundles.
- Test key rotation and revocation, wrong-scope signer denial, signer outage and verified retrieval from the restricted store.
- Confirm no customer payload or secret appears in bundle, CI output, deployment output or the metadata index.

## References

- `specs/003-health-assessment/implementation-plan.md`
- `specs/003-health-assessment/test-plan.md`
- `docs/development/gate-check-bundles.md`
- `docs/security/health-assessment-pilot-security-review.md` (SEC-PILOT-007)

## Approval

Accepted by: Repository owner — signing profile and trust-boundary direction  
Date: 2026-09-29
