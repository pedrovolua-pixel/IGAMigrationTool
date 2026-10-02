# Shared identity session foundation

Internal Milestone 2 / IP-HAS-003 composition only. No endpoint, guest onboarding,
sign-in activation, customer permission, audit export or data-plane access exists
here. The approved profile is in `docs/security/health-assessment-identity-session-design.md`.

## Accepted P03 local atomic audit

The five-argument `PostgreSqlTicketStore` constructor explicitly requires
`PostgreSqlSecurityAudit` and `ITransactionalSessionAdmissionPolicy`. Authority
checks use the **same** connection and transaction with the original signed
authentication time; no nested subject-locking connection can deadlock against
the store. The four-argument constructor and `PostgreSqlSessionAuthority` remain
legacy synthetic fixture seams, not the new restricted audited boundary. No
production composition may claim atomic audit by selecting those seams.

Apply additive `003-atomic-audit.sql` after `001`/`002`, before identity-authority
migrations, using a controlled migration executor. It creates no LOGIN role,
grants, stream, trusted binding, schedule or real enrollment. Its SQL functions
are schema-qualified, use `search_path=pg_catalog`, and revoke PUBLIC execution.
The explicit synthetic fixture assigns a distinct NONLOGIN nonsuperuser function
owner and separate runtime, administration, audit reader, lifecycle and witness
LOGIN roles. Actual production owners and grants remain protected review inputs.

The audited runtime uses only `lookup_ticket_subject`, `lock_subject`,
`lock_ticket`, `touch_ticket`, `issue_ticket`, `revoke_ticket`, `lock_head`,
`append_event`, `read_receipt` and `append_receipt` functions in `security_audit`;
it requires no direct ticket-table privileges. Administration uses the separate
`revoke_reference`/`revoke_subject` functions through
`PostgreSqlAuditedSessionAuthority`. Direct writes are not a recovery fallback.
All callers take the same subject advisory lock, then subject row, target row,
and finally stream head. The advisory expression/seed matches IdentityAuthority.

Issuance/rotation/revocation, closed event and metadata-only receipt share one
transaction. Deferred constraints bind tickets, subject mutations and mutation
events to exact receipts; null operation IDs cannot bypass the restricted
functions. Audited issuance and rotation recheck authentication/provider and
transaction-bound eligibility after the stream wait immediately before commit.
Exact local logout rechecks its authenticated session deadline and authority.
The event timestamp is sampled from the trusted clock after the head lock.
Store/rotate receipts never preserve or replay a raw cookie key, lookup hash,
protected ticket or credential. Lost commit acknowledgment is an uncertain
outcome, not evidence of rollback; trusted internal commands reconcile a closed
issuer/actor/kind/target/digest-bound receipt before retry. A revoked browser
cookie has no receipt route and remains unauthenticated under D01.

`writer_roles` binds the actual `SESSION_USER` to one configured stream,
writer-binding reference and explicit allowed actions; knowing a binding GUID
does not confer writer authority. Receipt creation verifies that same login,
stream and allowed action for every linked event. `reader_scopes` restricts the visible view to
configured stream/platform or exact customer/project and optional actor scope.
`lifecycle_bindings` and `witness_bindings` authorize distinct actors. These
tables are migration/operator-owned configuration, never caller-populated.

Event hashing uses the approved closed RFC8785 subset: sorted ASCII keys, exact
canonical GUIDs/digests, seven-fraction UTC timestamps and decimal-string 64-bit
counters. Closed reconstruction rejects duplicate/missing/unknown fields,
versions, prohibited payload fields, inconsistent combinations and writer,
receipt or stream bindings. The stream head and event allocate together.
`AuditIntegrityVerifier` uses an independently supplied checkpoint, the complete
ordered event/tombstone chain and receipts; it rejects gaps, regressed heads,
wrong writers, time anomalies and altered receipt links. Database-local hashes
are not independent protection against a privileged operator.

Explicit synthetic lifecycle tests exercise the already approved 12-month
soft-delete boundary, holds, release, 30-day active purge and tombstone replay
before restored records become visible. Lifecycle functions reject null or
future caller time against a fresh database clock after row locks; tests seed
past events instead of advancing the lifecycle operator's clock. No automatic cleanup, hold creation,
retention default, backup execution, checkpoint schedule or live witness is
introduced. Challenge/ticket/receipt cleanup and retained independent witnesses
remain reviewed production inputs. The audit-unavailable failure-preservation
decision remains unresolved; rollback/denial tests do not claim that logging is
a durable outage audit fallback. Live sign-in and the diagnostic host remain
disabled.

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

## Additive authentication context migration

Apply `002-authentication-context.sql` only through the controlled migration identity after `001`; startup performs no migration. Existing subject rows have a null provider cutoff and fail closed until trusted administration supplies explicit evidence. `ProvisionAsync` and `ConfirmProviderAsync` require that cutoff; refresh cannot move it backward. Retrieval uses the stored session's original authentication, so a newer session for the same subject cannot refresh the older one.

`PostgreSqlAuthenticationChallengeStore` stores only a hash of the opaque state reference and its microsecond-precise issuance timestamp. A row lock, fresh injected server clock and conditional update allow one successful consume across replicas within the accepted strict 15-minute validity. The handler owns protected OIDC state and consumes only after all supported protocol checks. Missing composition defaults to `DenyingAuthenticationChallengeStore`. Consumed or expired records remain until a separately reviewed cleanup/retention decision; this migration chooses no deletion schedule and grants no role. PostgreSQL retrieval errors fail closed with no returned ticket. Production migration permissions, compatible rollback, provider cutoff retrieval, origin semantics and deployed replica/key behavior remain unverified.
