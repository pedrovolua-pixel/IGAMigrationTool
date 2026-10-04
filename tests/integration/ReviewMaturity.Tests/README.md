# Independent synthetic review/maturity verification

Own packet: V5, owner-approved cycle05. The test host has no customer/provider connection and never grants risk acceptance, closure, recurrence, publication or production authority. Independent expectations and approved requirement/test mappings are in `golden-design.md`.

## Run

Use the repository-pinned SDK10.0.401. Locked audited restore, Release build and focused formatting are required before execution:

```sh
dotnet restore tests/integration/ReviewMaturity.Tests/ReviewMaturity.Tests.csproj --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet format tests/integration/ReviewMaturity.Tests/ReviewMaturity.Tests.csproj --no-restore --verify-no-changes
dotnet build tests/integration/ReviewMaturity.Tests/ReviewMaturity.Tests.csproj -c Release --no-restore --disable-build-servers /p:UseSharedCompilation=false
dotnet run --project tests/integration/ReviewMaturity.Tests/ReviewMaturity.Tests.csproj -c Release --no-build -- --postgres
```

Without `--postgres`, the host explicitly prints persistence and bridge checks NOT VERIFIED. For actual PostgreSQL18.4 checks, provision ONLY disposable database `iga_synthetic_v5` at127.0.0.1. `IGA_REVIEW_TEST_DATABASE` optionally supplies its connection string; default port55433/useriga_synthetic. The explicit exact-host/database guard refuses another database. CI must create that database in its disposable loopback PostgreSQL18.4 service; trust authentication is confined to that isolated synthetic service.

Schema probes briefly modify ONLY owned `synthetic_review` metadata/content and restore it in `finally`; never execute this test concurrently with a browser/demo host using the same database. No cluster restart, database deletion, production migration or external service is performed. Observer exceptions exercise actual transaction rollback; concurrent independent connections test one-winner revision compare-and-swap. Tampered seeds, migration ledger and actual schema refuse access. Immutable originals, append-only events, replay outcomes and all occurrence overlays are separately checked.

Historical compatibility uses four literal frozen JSON envelopes and complete saved-input digests produced from exported baseline commit8f75eb49af13cb1ba36af9bac88ca8699a072a85. `historical-baseline-goldens.json` records each baseline source hash and baseline-only oracle source; `HistoricalGoldens.cs` embeds the literal values. Current implementation never computes its own expected compatibility values. Temporary baseline source export is excluded from the packet and removed after obtaining the oracles.

Complete definition hashes, exact executed results, source commits and limitations are in `evidence.json`. Full Windows/NVDA/Narrator acceptance, real catalogs/evidence/identities, release/publication and customer gates remain NOT VERIFIED.
