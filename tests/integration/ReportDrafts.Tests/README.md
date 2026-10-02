# Independent draft verification

This executable consumes the sealed `ReportDrafts` core/Markdown modules and existing saved run/review/maturity modules. Its own manually specified fixture, literal complete canonical bytes and independent SHA-256 golden are separate from author tests. `canonical-golden-provenance.json` records the fixture export and standalone JSON canonicalization method. Runtime assertions never regenerate expected bytes using the implementation.

Portable cases exercise scores, separate maturity/quality, complete source identity, object/set reordering, culture, disposal and detached bytes, hostile Markdown, unknown nested properties, duplicate IDs/events, history/current coherence and fail-closed versions. Actual PostgreSQL cases consume saved engine/review records and assert literal initial/reviewed health, source/time independence, preserved generated originals and maturity, a new current value versus an unchanged prior returned value, fresh instance reload, wrong scope and baseline8f75eb4 immutable historical envelope/digest compatibility. No report persistence or migration is introduced.

Run from the repository root with the repository-pinned SDK:

```sh
dotnet restore tests/integration/ReportDrafts.Tests/ReportDrafts.Tests.csproj --locked-mode
dotnet build tests/integration/ReportDrafts.Tests/ReportDrafts.Tests.csproj -c Release --no-restore
dotnet run --project tests/integration/ReportDrafts.Tests/ReportDrafts.Tests.csproj -c Release --no-build
dotnet run --project tests/integration/ReportDrafts.Tests/ReportDrafts.Tests.csproj -c Release --no-build -- --postgres
```

The PostgreSQL opt-in uses only `Host=127.0.0.1;Port=55433;Database=iga_synthetic_v6;Username=iga_synthetic`, or the exact loopback/database equivalent in `IGA_DRAFT_TEST_DATABASE`. Provision that disposable UTF-8 database first. The executable refuses other host/database names. It initializes existing approved run/review schemas only, never restarts/drops the cluster or touches another database. Execute persistence before the browser host uses the same database. Without `--postgres`, persistence prints NOT VERIFIED.

`execution.log` contains the final executed assertion count. Full supported Windows/NVDA/Narrator/manual WCAG, PDF/export/publication, production/customer authority and durable report history remain NOT VERIFIED.

Final executed evidence:201 assertions with PostgreSQL18.4,146 portable-only assertions, SDK10.0.401 audited locked restore, full focused format verification and Release build with zero warnings/errors passed. Exact consumed core343765a and Markdownc7d8d3e. Browser integration is separately attributed in its own evidence.
