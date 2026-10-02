# Shared identity session foundation

Internal Milestone 2 / IP-HAS-003 composition only. No endpoint, guest onboarding,
sign-in activation, customer permission, audit export or data-plane access exists
here. The approved profile is in `docs/security/health-assessment-identity-session-design.md`.

## Composition

Supply `NpgsqlDataSource` for the **control-plane** database, a production-approved
shared `IDataProtectionProvider`, a trusted synchronized `TimeProvider`, and a
required `ISessionAdmissionPolicy` adapter. The adapter reads current authoritative
product eligibility, including guest sponsor/expiry/review and subject assignment;
it cannot derive eligibility from request fields or coarse claims. Missing adapters
fail closed. Exceptions never restore access. Do not enable parameter/value logging
on the datasource or record cookie keys, protected ticket bytes or token material.

Assign `PostgreSqlTicketStore` to ASP.NET cookie `SessionStore`. The framework sends
only its protected store-key envelope; the store's returned key has 32 cryptographic
random bytes, while PostgreSQL retains only a SHA256 lookup hash. Tickets are
purpose-separated and protected using the supplied shared key ring. The store does
not configure an ephemeral or filesystem production key fallback. Shared durable
key persistence, key encryption/access/rotation and failover remain deployment gates.

The trusted transport supplies raw `tid`, `oid` and only the four approved `roles`.
Allowed trusted properties are `bff.authenticatedUtc`, `bff.securityVersion`,
`bff.providerCheckedUtc` and `bff.mfaCaVerified`; timestamps use round-trip UTC form.
Tokens, extra claims, token properties and arbitrary parameter payloads are rejected.
Issuance requires unexpired trusted authentication (younger than eight hours).
The absolute deadline is that original authentication time plus eight hours, so
storing or rotating an older context cannot reset its deadline. Privileged actions
retain the separate 15-minute authentication requirement in the policy module.

`PostgreSqlSessionAuthority` is for trusted server operations after product/provider
authority has been verified. Provisioning does not automatically onboard a guest.
Provider confirmation cannot come from a ticket or browser. Subject changes call
`RevokeSubjectAsync`; it increments the security version and revokes all sessions in
one transaction. Disable uses its explicit flag. Re-enablement is not provided.
Listing returns metadata/reference GUIDs without cookie keys or ticket bytes;
reference revocation is bound to the supplied trusted subject. These methods expose
no user-facing route or authorization policy themselves.

Every store/retrieve/renew/rotate/revoke operation uses the same subject row lock;
session rows are then locked in that order. Each retrieval checks active/version,
provider freshness and eligibility without a grant cache. Thirty-minute inactivity,
eight-hour absolute expiry and 15-minute provider status are strict boundaries.
Timestamps are rechecked after potentially slow eligibility calls. Ordinary renewal
cannot change claims, authentication/MFA context, reference or absolute expiry.
Fresh sign-in/privilege change/reauthentication replaces the cookie with a newly
stored key; explicit `RotateAsync` invalidates its old unrevoked key atomically.
The server-generated `bff.sessionReference` in retrieved ticket properties binds
trusted privileged decisions to an exact current session; Store/Rotate reject an
incoming reference and generate a new one. The reference is not a cookie identity.

## Migration and verification

`migrations/identity-sessions/001-initial.sql` creates only a control-plane schema.
Apply once with the approved migration identity and migration ledger; the host does
not migrate on startup. No SQL privileges or secrets are created. Runtime grants,
data-protection key management, expired/revoked-record cleanup/retention, payload-free
audit integration and complete production assignment/provider adapters are unresolved
deployment/product integrations. This module does not select a ticket-retention policy.

Unit tests exercise cryptographic identifiers, context refusals and minimal tickets.
Integration tests require `IGA_SESSION_TEST_CONNECTION` naming a loopback database
whose name starts `iga_synthetic_`, plus `--reset-synthetic-schema`. They reset only
the session schema in that explicit disposable database and exercise real PostgreSQL,
two store/data-protection instances, exact lifetimes, tamper denial and concurrent
renew/store/rotate versus revoke. No in-memory store substitutes for persistence.
