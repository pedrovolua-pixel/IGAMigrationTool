# Independent analysis and scoring verification

This executable uses literal expectations from the approved feature003 health algorithm and cycle04 plan. It independently verifies the five fixed synthetic predicate classes, original finding/run provenance, per-object grouping linearity, mandatory review boundaries, health versus quality, lifecycle fixture penalties, category weights, raw thresholds, immutable canonical projection and 100,000 unique keys. It does not calculate expected goldens by calling implementation arithmetic.

Run from the repository with the pinned SDK:

```sh
dotnet restore tests/integration/AnalysisScoring.Tests/AnalysisScoring.Tests.csproj --locked-mode /p:NuGetAudit=true /p:NuGetAuditMode=all /p:NuGetAuditLevel=low
dotnet build tests/integration/AnalysisScoring.Tests/AnalysisScoring.Tests.csproj --configuration Release --no-restore
IGA_ANALYSIS_TEST_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v4;Username=iga_synthetic' dotnet run --project tests/integration/AnalysisScoring.Tests/AnalysisScoring.Tests.csproj --configuration Release --no-build -- --postgres
```

Precreate only the disposable synthetic database `iga_synthetic_v4` using PostgreSQL18.4, UTF8 and the dedicated synthetic role. The integration guard accepts that exact database on `127.0.0.1`. The runner initializes the current approved synthetic migration and adds synthetic runs. It never restarts the cluster or drops databases. Its deliberate raw digest corruption negative disables the immutable input trigger only in its own database, restores both data and trigger in `finally`, and revalidates migration drift afterward. Run it before a browser host uses the same database. Without `--postgres`, actual database cases explicitly report NOT VERIFIED.

Executed 2026-10-01 with SDK10.0.401: Release build0 warnings/errors and **399 assertions PASS**, including actual PostgreSQL18.4 saved complete/partial/cancelled/legacy runs, fresh engine reload, exact historical JSON, full version tuple refusal, same-key plan metadata refusal, saved result provenance refusal and raw stored input corruption denial/restoration. Audited locked restore and focused formatting verification passed. `evidence.json` binds the complete owned fixture definitions, copied module sources, coordinator private schema/host/frontend artifacts and browser results to SHA-256 values.

Read-only review: the full frozen version tuple and whole saved plan canonical binding initially needed correction; the coordinator corrected both before this execution, and explicit negative fixtures now cover them. No remaining actionable finding was observed in the scoped final S4/D4 review. Test-only dependency copies were excluded from this packet. The adapter is invoked with engine-validated snapshots; raw persisted digest integrity is tested through actual engine reads.

Limits: fixed internal synthetic scope and fixture rules only. No customer evidence, actual One Identity rule promotion, production permissions, AI provider, mutable finding review, risk acceptance grant, published report or maturity result. Full Windows/manual assistive technology acceptance remains NOT VERIFIED.
