# Scripted source-page checks

Standalone assertion executable for SP01–SP17 of the reviewed [contract](../../../specs/001-data-ingestion/collector-source-page-v1-contract.md). All SQL/schema/build/scope/value fixtures and native comparator/classification decisions are explicitly fictional. Tests verify orchestration; they do not establish vendor eligibility, source authority or production permissions.

Run the pinned repository SDK with locked audited restore, Release build and `dotnet run --no-build --no-restore -c Release --project tests/unit/CollectorSourcePages.Tests/CollectorSourcePages.Tests.csproj`. The assertion host catches failures and prints only fixed check names and numeric summaries. No raw exception/source payload is printed.

Checks include exact initial/continuation projections; closed first AST negatives; no minimum-key sentinel; stable identity/fresh generation; full/short/empty/capped pages; exact schema/native widths; caller collection/byte immutability; conflict occurrence references; bound classifiers/comparators; revocation/effective permission/warning/impact gates; cancellation at each operation; actual cancelable timeout; fake UTC/monotonic rollback and original-retention crossing; disposal failure and contention. Physical provider/SP19 and persisted typed codecs/SP18 remain separate checks. Existing historical suites remain applicable.
