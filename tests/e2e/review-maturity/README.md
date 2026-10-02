# Actual local review/maturity browser verification

This independent packet uses the exact locked Playwright1.62.1 and axe-core4.13.0 dependencies of `tests/e2e/consultant-demo`; no new package/configuration is needed. Run `npm ci --ignore-scripts` in that parent directory, then provision its Chromium browser (`npx playwright install chromium --with-deps` on disposable Linux CI). Node24.21.0/npm11.20.0 were used locally.

Run from the repository root after a final Release host build and final production frontend build:

```sh
IGA_DOTNET=dotnet \
IGA_HOST_DLL="${PWD}/src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll" \
IGA_HOST_WORKING_DIRECTORY="${PWD}" \
IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v5;Username=iga_synthetic' \
node tests/e2e/review-maturity/verify.mjs
```

Only own disposable iga_synthetic_v5 is authorized. Port5183 must be free before owned host launch. The script gracefully stops only its own child process, performs an actual owned-host restart, and cleans up browser/API contexts. It never restarts PostgreSQL or drops a database. Run database drift/probe tests first, then this browser test sequentially. Setting no host DLL means use an explicitly already running synthetic host and skips owned-host restart evidence; final local evidence did launch/restart its own host.

Optional overrides: `IGA_CHROMIUM` absolute executable, `IGA_PLAYWRIGHT_MODULE`/`IGA_AXE_MODULE` module file, `IGA_DEMO_SCHEMA` authoritative schema path. `IGA_DEMO_BASE_URL` is guarded to http://127.0.0.1:5183. The child inherits DOTNET_ROOT from the environment; there are no personal/private default runtime paths. The schema defaults to the repository private contract.

Assertions cover independent health/maturity literals, old profile denial, immutable originals and separate run/review revisions, strict7-field bodies and4KiB malformed-body denial isolated by an otherwise valid bounded command with legal JSON whitespace padding, server actor/scope, CSRF/Host/Origin, keyboard Confirm/Reject/Defer/comment/edit, required reason, inert hostile text, rendered score/warning/history goldens, stale CAS focused/manual recovery, actual committed-response-loss same-event retry without duplicate history, held old GET/POST during selecting another saved run, and actual reload/owned-host restart. Axe is injected into test execution context via DevTools evaluate without weakening product CSP. Desktop/390px/320px screenshots, expanded history/evidence, forced-color smoke and confirmed/incomplete axe results are recorded.

`execution.json` records exact executed groups/assertions and scanner outcomes; assertion count includes actual ready polling and can vary. `artifacts.json` binds complete test definitions, authoritative schema, final host assemblies and served frontend/source. Screenshots contain synthetic fixture data only. Full supported Windows/NVDA/Narrator acceptance and WCAG conformance are NOT VERIFIED. Any scanner incomplete result remains explicitly NOT VERIFIED.

After successful execution, run `node tests/e2e/review-maturity/bind-artifacts.mjs` to bind the final built artifacts. If the built host/frontend live in another checkout, set `IGA_ARTIFACT_ROOT` to its absolute repository path. Paths and SHA-256 values are separate record fields; no scanner allowlist is used.
