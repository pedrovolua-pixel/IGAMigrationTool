# Actual browser verification of structured synthetic guidance

The harness launches and stops only its own supplied host, refuses an occupied fixed loopback port5183 and requires dedicated disposable PostgreSQL database `iga_synthetic_v7`. Obtain coordinator port handoff first. Actual database integration checks finish before browser checks; no shared cluster restart/drop, historical demo deletion or other host termination occurs. Existing locked Playwright1.62.1/axe4.13.0 dependencies are reused, with no new package/version.

```sh
IGA_HOST_DLL=/absolute/repository/src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll \
IGA_HOST_WORKING_DIRECTORY=/absolute/repository \
IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v7;Username=iga_synthetic' \
node tests/e2e/recommendation-guidance/verify.mjs
node tests/e2e/recommendation-guidance/bind-artifacts.mjs
```

The binder requires the same host/root environment. Optional existing-runtime selectors are `IGA_DOTNET`, `IGA_PLAYWRIGHT_MODULE`, `IGA_AXE_MODULE`, `IGA_CHROMIUM` and `IGA_DEMO_SCHEMA`. `IGA_COORDINATOR_WORKING_DIRECTORY` additionally verifies each executed borrowed source file matches coordinator source. Never bind a pending/failing execution or changed source.

Independent literal complete catalog fields, full canonical snapshot/scoped-option hashes, existing score/maturity literals, current versus immutable-original values, all four older-profile nulls, healthy/gap-only zero advice and independent weighted/mixed values are checked on actual saved runs. API reads and existing finding review actions are the only requests. There is no guidance mutation endpoint.

Actual browser cases inspect every visible option field, occurrence row, ordered validation/reference/assumption/limitation, keyboard disclosure and current finding Confirm refresh/focus; source/content/scoped-ID/paired-provenance/option-ID payload substitutions must fail before display and recover with focused same-revision retry. Typed unavailable shows no stale/inferred advice. Two selection races independently hold a fetched old GET and the browser's post-GET WebCrypto digest await; neither may replace the new selected run or steal focus. The harness performs an actual owned host process restart and reacquires ephemeral transport values through reload.

Hostile title/business context are actual saved R5 text. Hostile structured option fields are coherently substituted through the real read/coherence guard together with matching original flattened strings; the approved fictional catalog is unchanged. HTML/image/script/link/fence/shell/formula/bidi text creates no active nodes, dialogs or external loads. Guidance has native disclosure controls and inert table scrollers; no advice review/approval/task/export/download/execution control appears. Desktop390320 reflow, fully expanded content, forced colors and axe confirmed violations are engineering checks.

`execution.json`/`execution.log`, five screenshots and `artifacts.json` bind complete definitions and literal fixtures, exact executed borrowed source/schema/assets and host/test binaries with `{path,sha256}` arrays. Earlier recovery/analysis/review/draft evidence remains unchanged and separately hashed. The verifier authors only these test directories; borrowed source copies are excluded from its commit.

Axe incomplete results, full Windows/NVDA/Narrator/manual WCAG, CSV/PDF, live/customer pilot and full milestones remain explicitly NOT VERIFIED. Finding disposition cannot review guidance or validate remediation. The value is current and detached; no durable recommendation history is claimed.

Final executed evidence: 2941 browser assertions/eight groups; 44 source/content substitutions; actual owned process restart; Chromium145.0.7632.6/Node24.21.0. Integration:112 portable assertions;257 with actual PostgreSQL18.4. Locked audited restore, project formatting and Release0warnings/0errors passed. Final executed assets `index-CafamDP9.css`/`index-DaT-ukfi.js`. Axe4.13.0:desktop31passes/zero confirmed violations;320px33passes/zero confirmed violations. `aria-prohibited-attr` remains incomplete in both and `color-contrast` incomplete at320px; full manual acceptance NOT VERIFIED. Desktop and320px viewport screenshots were visually inspected; narrow provenance labels wrap heavily and require manual accessibility acceptance.
