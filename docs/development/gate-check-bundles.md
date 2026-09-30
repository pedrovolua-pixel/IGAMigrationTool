# Internal gate-check bundle (implementation draft)

The `EvidenceGovernance` module verifies one signed, metadata-only bundle of machine check results. It does not read customer evidence or authorize a gate, artifact promotion, pilot use, or production release. The signing key's trust source, reviewer attestations, restricted-store access, upload sanitization, and retention/purge jobs must be implemented and reviewed before this is used for a real gate.

The initial internal schema is version `1`. It contains exactly these JSON fields:

| Field | Constraint |
|---|---|
| `schemaVersion` | Integer `1` |
| `gate` | `G1` through `G9` |
| `commitSha` | 40 hexadecimal characters |
| `artifactSha256` | 64 hexadecimal characters |
| `decisionAt` | Explicit UTC gate-decision time (`Z` or `+00:00`); the bundle becomes stale 12 months later |
| `checks` | Array of unique objects containing only `id` and `result` |
| `checks[].id` | Uppercase letters, digits and hyphens, 2–64 characters; must match one of the caller's required check IDs |
| `checks[].result` | `PASS`, `FAIL`, or `NOT_VERIFIED` |

`GateCheckBundleWriter` creates this shape from a trusted required-check list and typed outcomes. It sorts check IDs so the same inputs produce the same bytes. Missing outcomes are written as `NOT_VERIFIED`; unexpected outcome IDs and free-text identifiers are rejected. The writer does not sign or upload the bytes. The decision time must come from the trusted gate-decision process before signing.

The verifier rejects unknown or duplicate JSON fields, extra checks, missing checks, non-passing checks, wrong gate/commit/artifact/decision time, evidence at or beyond 12 months from decision, future decisions, malformed dates, unsigned bundles, digest mismatches, wrong signing keys, and bundles over 64 KiB. It verifies an RSA-PSS/SHA-256 signature over the exact JSON bytes using a public key supplied by the trusted caller, never by the bundle. The caller must supply the expected decision time and required check set from approved gate configuration; the bundle cannot choose its own requirements.

`GateSigningTrustRegistry` is a local, in-memory trust snapshot supplied by a protected caller. It validates RSA public keys and unique key identity/material, binds keys to allowed gates and signing windows, permits verification of historical signatures after rotation, and rejects revoked keys. Its key ID comes from trusted retrieval metadata, not the bundle. This does not provide a production key registry, signing authority, revocation feed or store integration; those must be verified before a gate can rely on it.

`PromotionMachineEvidencePreflight` checks a signed artifact and a signed gate bundle together against one caller-supplied expected artifact digest. Its result is `ReadyForReview` only when both integrity checks pass; it never activates an artifact. Synthetic threat cases cover unsigned or tampered artifacts/bundles, wrong artifact, commit, signer or key, failed checks and expired evidence. The future promotion service must still authenticate reviewer attestations, validate compatibility and owner exceptions, store and retrieve evidence, record audit, and require human-authorized activation. The preflight result alone cannot grant any of those steps.

`Valid` means only that this machine-check bundle is authentic, in scope, current, and reports every required check as passing. It is **not** a gate `PASS`. The approved implementation plan still requires an accessible signed/digested bundle in the restricted East US 2 store, reviewer decisions, applicable manual and security checks, and the metadata-only evidence-index entry. No store or promotion integration exists yet. The RSA-PSS trust/key lifecycle and this internal schema need technical and security review before production wiring.

`GateEvidenceRetention` calculates the default clock shared with bundle verification. Ordinary access ends at the decision time plus 12 calendar months; the active-store purge deadline is 30 days later. The boundary instants are expired/due. Its synthetic checks include a leap-day decision and both boundary instants. This pure calculator does not delete or hide objects, approve a hold, verify a purge, or grant a gate. The separate access and purge design is recorded as a pilot implementation choice in `architecture/decisions/ADR-0006-engineering-evidence-store-access.md` and awaits milestone review.
