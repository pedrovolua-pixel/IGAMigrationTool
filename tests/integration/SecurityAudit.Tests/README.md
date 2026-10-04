# Local atomic security audit verification

Requires real PostgreSQL and the pinned repository .NET SDK. Supply
`IGA_AUDIT_TEST_CONNECTION` for a loopback database whose name begins
`iga_synthetic_`, then execute this project with `--reset-synthetic-schema`.
The explicit test fixture resets only its session/audit schemas and creates
named disposable synthetic roles. Never point it at a retained/live database.

The fixture uses distinct nonsuperuser runtime, administration, reader,
lifecycle and witness LOGINs plus a NONLOGIN function owner. Ordinary runtime
has no direct ticket-table privileges. Tests exercise atomic issue/revoke/rotate,
real audit failure rollback, null-operation and direct-write denial, exact
stream/action/reader bindings for events and receipts, closed SQL payload and
signed64-bit overflow refusal, concurrent idempotent
commands, subject version revocation and actual ordered-head contention crossing
the provider deadline. Sixteen different-subject writers share the same stream.

An actual PostgreSQL commit followed by deterministic simulated acknowledgment
loss verifies metadata-only reconciliation and untouched other sessions. It does
not simulate an actual TCP interruption. Chain/witness tests include holds,
12-month soft deletion, active purge at 30 days, deletion tombstones and replay
before restored events become readable. Lifecycle tests seed past events and
prove null/future operator times are refused against the actual database clock.
Restricted receipt and witness calls likewise reject null/future timestamps.
The fixture deliberately uses a seven-fraction instant that rounds differently
between PostgreSQL JSON timestamp parsing and Npgsql timestamp parameters.
Exact retention boundaries and restored timestamps derive from the persisted
PostgreSQL event instant; the one-microsecond early denial remains explicit.
Tests use synthetic clock/bindings and
an independently supplied synthetic witness, not a production signer/destination.

Live SQL grants, retained independent witnesses, audit-outage preservation,
retention/backup scheduling, real Azure keys/proxies/provider behavior and gate
acceptance remain unverified.
