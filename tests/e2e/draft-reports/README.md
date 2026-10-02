# Actual draft browser verification

The harness uses the locked Playwright1.62.1 and axe4.13.0 dependencies already declared by `tests/e2e/consultant-demo`. It launches and terminates only its own explicitly supplied synthetic host, requires free loopback port5183 and the dedicated `iga_synthetic_v6` database, and cleans up browser/API/host subprocesses on success or failure. PostgreSQL integration probes must finish before launch; do not share this database/port with another active harness.

```sh
npm ci --prefix tests/e2e/consultant-demo
npm exec --prefix tests/e2e/consultant-demo -- playwright install chromium
IGA_HOST_DLL=/absolute/repository/src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll \
IGA_HOST_WORKING_DIRECTORY=/absolute/repository \
IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v6;Username=iga_synthetic' \
node tests/e2e/draft-reports/verify.mjs
node tests/e2e/draft-reports/bind-artifacts.mjs
```

The artifact binder requires the same host/root environment. `IGA_DOTNET` optionally selects the dotnet executable; `DOTNET_ROOT` is inherited. `IGA_PLAYWRIGHT_MODULE`/`IGA_AXE_MODULE` may point to existing locked module files, `IGA_CHROMIUM` to a preinstalled browser executable, and `IGA_DEMO_SCHEMA` to the current private contract. Default imports reuse the consultant-demo dependency directory. No new package is added.

Goldens include initial44.2/78.3/four proposals, Critical confirm44.2/39.2/two, High reject64.2/64.2/zero and independent Managed maturity; healthy100/Initial, gapsUnavailable/Initial, mixed66.7/89.2/Developing and operations61.3/78.3. API parity, exact visible distinct category/object/module columns, quality/gap reasons, captured maturity thresholds, original/current finding text and attributed history are checked independently. Keyboard view/review actions, same-revision retry, 17 actual response source substitutions, old-response selection/focus race, actual owned process restart, hostile HTML/link/image/fence/control data, reflow and axe are exercised. Existing historical manifests are hashed and preserved.

`execution.json`/`execution.log` are executed results. `artifacts.json` binds complete test definitions, fixture bytes, source/modules, actual DLL/frontend/schema, execution and screenshots using separate `{path,sha256}` records. It deliberately excludes itself to avoid recursion. Summary/technical/Markdown identify one current immutable returned value; no durable ReportVersion/history, published/exported document or new public API is claimed.

Automated accessibility is engineering evidence only. Axe incomplete findings are disclosed as NOT VERIFIED; full Windows/NVDA/Narrator/manual WCAG acceptance remains NOT VERIFIED.

Final executed evidence:2137 assertions/seven groups; actual owned process restart; Chromium145.0.7632.6/Node24.21.0; final host and frontend `index-DtzxviFB.js` bound in `artifacts.json`. Axe4.13.0:desktop31passes/zero confirmed violations;320px33passes/zero confirmed violations. `aria-prohibited-attr` incomplete in both views and `color-contrast` incomplete at320px remain explicitly NOT VERIFIED. A strict320px whole-page overflow failure identified ordinary technical/history IDs and timestamps; coordinator repaired text wrapping and the same unchanged assertion then passed. The object-type golden51.0 uses ten equal units earning5.1; equal-category overall44.2 uses distinct category denominators. No implementation arithmetic helper generates expected scores.
