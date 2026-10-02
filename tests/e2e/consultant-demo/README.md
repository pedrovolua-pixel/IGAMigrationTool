# Independent consultant demo browser checks

V3 exercises the actual built React interface and local ASP.NET host with PostgreSQL-backed saved runs. Playwright 1.62.1 and axe-core 4.13.0 are exact development dependencies here; no browser/scanner dependency enters the product. Install the locked test packages with `npm ci --ignore-scripts --audit --audit-level=low`, then install the pinned Playwright Chromium with `npx playwright install chromium`. Run `node verify.mjs` against an already running synthetic host on `http://127.0.0.1:5183`.

For deterministic crash/recovery, provide `IGA_HOST_DLL` and `IGA_HOST_WORKING_DIRECTORY` pointing to the built local demo DLL and repository checkout containing `src/web/dist`. The test runner spawns, kills and restarts **only its own process**; it never kills an existing host or restarts PostgreSQL. The port must be unused initially. `IGA_DOTNET` selects the pinned SDK executable; inherited `DOTNET_ROOT` is preserved. `IGA_CHROMIUM` can select an already installed Chromium, otherwise Playwright uses its installed browser. `IGA_PLAYWRIGHT_MODULE` is an optional absolute module override for an existing bundled runtime; the default uses the locked local package.

```sh
IGA_DOTNET="$(command -v dotnet)" \
IGA_HOST_DLL="$PWD/src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll" \
IGA_HOST_WORKING_DIRECTORY="$PWD" \
node tests/e2e/consultant-demo/verify.mjs
```

The host is always started with `--synthetic-local-demo`. A recovery restart additionally uses the explicitly authorized startup-only `--pause-synthetic-worker` fixture flag. The test stops its own host in finally. These tests create only opaque synthetic catalog runs in the disposable demo database.

- DEMO-001: hostile Host/proxy headers, foreign/missing origin, missing antiforgery, extra properties/wrong kinds/oversize/unknown catalog and scope, response security headers, duplicate/conflicting start, stale CAS; independent 4/4 gap progress, literal 2/4 executable measure, state counts, reasons/stages and frozen references.
- DEMO-002: keyboard start, named native progress, polite status, detail focus, keyboard locked-input disclosure, preserved focus during polling, reload/opaque selected identity and keyboard history selection.
- DEMO-003: keyboard cancellation, focused stale-CAS recovery if raced, results retained and outstanding work remains, no fabricated completion or cancelled-run resume.
- DEMO-004: desktop plus390 px/320 px captures, no page overflow at320 px, labeled controls/table captions/target size, forced colors/reduced motion; axe WCAG 2A/AA,2.1 AA,2.2 AA scans on desktop gap view and320 px cancelled run with expanded input references.
- DEMO-005:0/0 applicable coverage displays Unavailable; deliberately injected 403 action error is focused, offers recovery and does not mutate the saved record; no uncaught application exceptions.
- DEMO-006: actual owned host SIGKILL, paused restart, reload, server-authorized database lease expiry, keyboard Resume expired work, normal restart, missing-only completion of40 keys with independent39 Pass / 1 Finding, unchanged frozen references and coverage completion remaining Scoring.

`execution.json` records the actual browser run, group names, check count and axe pass/violation/incomplete counts. Network polling contributes to the count, so it can vary. Screenshots contain only synthetic local data. These automated engineering checks do not establish complete WCAG conformance or manual Windows 11 Edge/Chrome/Firefox ESR with NVDA/Narrator acceptance. Production identity/customer isolation, live source, ServiceBus, scoring/AI and live gates remain NOT VERIFIED.
