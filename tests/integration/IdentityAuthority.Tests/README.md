# Synthetic PostgreSQL authority proof

This executable exercises the approved P01/P02 local boundary against PostgreSQL18. It uses distinct fictional LOGIN identities and a nonlogin function owner, actual audit/session functions and transaction-bound admission. It never uses Graph HTTP, Azure or actual users.

Run the Release build with the repository-pinned .NET SDK, then supply `IGA_AUTHORITY_TEST_CONNECTION` for a dedicated loopback database whose name begins `iga_synthetic_`. Execute the built `IdentityAuthority.Tests.dll --reset-synthetic-schema`. The explicit argument authorizes reset of only `identity_authority`, `security_audit` and `identity_sessions` in that isolated database. Do not point it at any retained/shared environment. Synthetic roles are newly generated for each run; dispose of the isolated cluster afterwards.

The executable covers:

- Closed attributed enrollment, activation, suspension, explicit resumption and terminal revocation; separate aggregate and assignment revisions; exact receipt retry and conflicting digest denial.
- Exact owned scope/category assignments, duplicate/FK rejection and assignment revocation; actual restricted login denial of direct table edits and cross-writer functions.
- Captured provider versions, ordered publications, role changes, conservative deadlines and rollback after observed subject/stream-head lock waits.
- External Members under the same sponsor/expiry/review/home boundary; known engagement change, attributed renewal and home-cutoff monotonicity.
- Actual audited ticket issue/read, transaction-bound admission without self-lock, original authentication cutoff and atomic authority ticket revocation.
- Audit failure rollback, direct unaudited mutation denial, and missing/null/Pascal receipt fields failing deferred integrity checks.

Synthetic success is not live property-permission, role-replication, guest-home trust, unknown network commit, resource-policy endpoint or production proof. Independent combined tests and canonical evidence are coordinator-owned.
