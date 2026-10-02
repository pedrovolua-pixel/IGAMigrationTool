# Identity session migration order

Execute `001-initial.sql`, then `002-authentication-context.sql`, then
`003-atomic-audit.sql` through an explicitly controlled migration identity before
the additive identity-authority migrations. No host executes a startup migration.
Existing null-cutoff subjects deny; no synthetic fixture backfills production.

Migration `003` creates closed audit/event/receipt/integrity/lifecycle schemas and
restricted functions. It creates no LOGIN role, trust binding, stream, data
grant, retention schedule or production resource. PUBLIC execution is revoked.
Production function ownership and exact role privileges require a separately
reviewed protected binding and deployed negative tests. The local synthetic
SecurityAudit.Tests fixture establishes only disposable loopback test roles.

Use a distinct NONLOGIN function owner, migration executor, administration,
provider publisher, ordinary ticket runtime, scoped reader, lifecycle and witness
identities. Populate only explicitly reviewed `writer_roles`, `reader_scopes`,
`lifecycle_bindings` and `witness_bindings`; missing configuration denies.
Runtime writers need exact allowed action sets, not a shared broad grant.

Rollback disables admission and preserves version, audit, receipt, deletion
tombstone, checkpoint and key state. Do not drop records, reduce a security
version or restore a revoked session to make an old reader run. Actual live
cleanup/backup/restore and audit-outage preservation remain unverified.

The initial schema is control-plane only, never a customer database. Ticket rows
contain a hashed lookup, protected minimal ticket and expiration/revocation
metadata, without raw cookie identifiers, passwords, tokens or connection strings.
