# Saved-source accepted-schema checks

Run with the pinned .NET SDK and local synthetic PostgreSQL service. `IGA_ACCEPTED_SCHEMA_TEST_DATABASE` selects an absent database with prefix `iga_synthetic_phase1b_schema07_author_`, ASCII letters/digits/underscore only and total length at most63. Default appends20 lowercase hex characters. The executable self-creates only absent databases and refuses reuse; no reset/drop/truncate. Failed databases are retained.

Fixtures use existing owning write APIs solely to prepare saved fictional source. Runtime capture uses existing caller-owned transactions and metadata-only owning reads. Checks include committed table fingerprints, normal/mixed/accepted-empty/no-accepted/retry/unknown states, current authority, invalid input, actual writer exclusion, rollback-scoped corruption and migration drift. No provider network/host/reviewer/sampler activation.
