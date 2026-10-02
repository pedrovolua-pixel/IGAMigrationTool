# Local synthetic identity authority

This module implements the owner-approved P01/P02 internal authority packet and ADR0009. It has no controller, Graph HTTP client, credential, Azure grant, startup migration or production activation. The permanently disabled executable remains unchanged. Synthetic evidence cannot verify real Entra property permissions, role replication, guests/home revocation, Conditional Access or enrollment transport.

## Contracts and authority

Closed v1 records and `AuthorityCodec` reject missing/extra/duplicate JSON members, noncanonical GUID/UTC values, numeric or unknown enums, invalid revisions, empty identifiers and conflicting grants. Authority commands carry exact trusted administrator, decision reference, operation and target. The command digest is recomputed internally over its full canonicalized payload with the digest field zeroed; caller-supplied hash text is not accepted on its own.

The enrollment revision is the subject aggregate revision. Every product assignment, guest or subject mutation increments it and the one existing `identity_sessions.subjects.security_version` once. Assignments have a separately checked target revision. Initial enrollment attribution and origin/home binding remain immutable through activation; a new activation decision is retained in the atomic command record/event receipt. Revoked enrollment is terminal. Sign-in and provider observations cannot enroll, activate or clear suspension/revocation.

`PostgreSqlIdentityAuthority.ExecuteAsync` provides only closed internal administration commands. It locks advisory subject, subject row and owned target before the audit stream. Authority mutation, actual ticket revokes, attributable event and immutable receipt commit together. Exact retry resolves a receipt before checking the old revision. Binding/digest conflicts deny. Unknown commit results are not reported as rollback; trusted callers reconcile the original operation receipt rather than inventing a new command.

## Provider seam

`SyntheticProviderReader` uses explicit synthetic byte/page and home-status interfaces. It permits only direct user assignments to the reviewed resource, exact known role IDs and validated same-query Graph-shaped continuations. Group/unknown/default-zero/duplicate roles and missing or hostile pages deny. There is no network implementation. Bounds (64KiB closed response,16 pages,4 unique roles) limit this internal fixture parser; actual provider compatibility remains unverified.

Provider time is sampled before the first await. Publication checks captured aggregate revision/security version and monotonic sequence/start/resource cutoff/home cutoff under the same subject transaction, then rechecks the strictly-under15-minute conservative timestamp after audit waits and immediately before commit. Deferred database triggers repeat deadlines at commit. A late read cannot create a new freshness window. Account/role changes invalidate old sessions; Member/Guest classification never changes explicit product origin.

External origin applies its sponsor/review/expiry and exact organizational home evidence regardless of provider `userType`. Missing home evidence defaults to denial. `EffectiveCutoff` is the later of home/resource cutoffs. Production home trust is absent. Synthetic home evidence cannot become a claim of home-tenant Graph permission or live revocation.

## Composition

Reference IdentityPolicy and IdentitySessions. Apply identity-sessions001,002,003 then identity-authority001 explicitly using the migration identity. Create reviewed scope ownership and exact role bindings through trusted migration/administration intake, never browser JSON. No real input is seeded by this module.

The production candidate uses `ITransactionalSessionAdmissionPolicy`: audited ticket operations pass their already locked connection/transaction and original protected authentication time. The authority checks the exact snapshot and composite cutoff there. Opening another subject-locking connection from ticket admission would deadlock and is not a supported composition. The legacy unaudited ticket constructor is an older fixture seam and is not this boundary.

`ReadAsync`, `Eligible`, `CoarseRoles`, `EffectiveCutoff` and `ExactAssignments` are trusted internal projections. Exact assignment filtering is performed before any customer lookup; it does not grant a public operation or replace the existing action/resource/customer-policy engine. A host bridge must map ExternalOrganizational to the existing guest boundary even for provider Members, use the earlier resource/home check time, validate the exact requested resource revision and preserve default-denying privileged verification.

SQL functions have fixed schema-qualified objects/search paths, PUBLIC execution revoked, bound SESSION_USER and separate administrator/provider/reader privileges. A controlled operator assigns nonlogin function ownership and narrowly grants approved functions. Runtime/reader/provider cannot directly edit authority/events/receipts; provider cannot call administration. Authority owner needs restricted reads of audit events/receipts for deferred integrity checks, not append/update/delete permission. The local integration harness proves distinct real login restrictions; it creates only fictional roles in an isolated loopback database.

## Verification and limits

Unit tests cover closed wire/schema/digest/assignment/guest and direct role/page/home denial. Real PostgreSQL tests cover optimistic revision races, exact receipt replay, cross-scope FKs, restricted login bypass denial, audited ticket composition, product revocation, composite home cutoff, audit rollback and observed subject/audit-head waits crossing the conservative deadline. Coordinator owns final whole-repository checks, independent review, canonical evidence and private board publication.

No new authority/challenge/receipt cleanup duration, live grants, production migration, retained witness trust, outage fallback, human gate, merge or release follows. Preserve revisions, revocation and audit/tombstone/key state during rollback; never restore authority by decreasing a version or dropping durable rows.
