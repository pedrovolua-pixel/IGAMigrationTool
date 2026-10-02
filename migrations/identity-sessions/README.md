# Identity session migration

Apply `001-initial.sql` once through the approved control-plane migration runner
and ledger. The script creates only the `identity_sessions` schema; it is never a
customer database migration or an application-startup operation. Tables have no
passwords, tokens, connection strings or raw cookie identifiers. Tickets contain a
hashed lookup, protected minimal authentication ticket and expiry/revocation metadata.

The runtime data source and trusted authority writer need separately reviewed
permissions. No grant is created here. Keep every writer on the subject-row-first
lock protocol, increment the monotonic version on product security changes and use
subject revocation before disabling access. No retention/purge schedule is invented;
its operational approval and deletion-safe recovery remain open.
