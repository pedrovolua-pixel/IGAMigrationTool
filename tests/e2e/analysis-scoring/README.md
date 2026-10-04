# Independent private analysis browser verification

The script reuses the existing exact locked Playwright1.62.1 and axe-core4.13.0 dependencies from `tests/e2e/consultant-demo`; it introduces no package. It checks actual API outputs against the authoritative private DTO schema and literal score/count expectations, then uses the built browser view for keyboard start, generated original disclosure, history/reload, unchanged-revision transient-failure retry, stale response rejection and reflow. Arithmetic expected values are literal specification-derived goldens.

Install the existing dependencies and Chromium, then run from the repository:

```sh
npm ci --ignore-scripts --prefix tests/e2e/consultant-demo
npm exec --prefix tests/e2e/consultant-demo -- playwright install chromium
IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v4;Username=iga_synthetic' IGA_HOST_DLL="$(pwd)/src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll" IGA_HOST_WORKING_DIRECTORY="$(pwd)" node tests/e2e/analysis-scoring/verify.mjs
```

Use only the disposable synthetic database already initialized by the independent integration runner. Port5183 must be free. Providing both host variables lets the harness launch and terminate only its own host process; otherwise it uses an already authorized synthetic host at `http://127.0.0.1:5183`. `IGA_DOTNET` optionally sets the dotnet command. The child inherits `DOTNET_ROOT`. `IGA_PLAYWRIGHT_MODULE`, `IGA_AXE_MODULE` and `IGA_CHROMIUM` optionally select existing local runtime files; otherwise the parent test dependencies and installed Chromium are used. `IGA_DEMO_SCHEMA` optionally supplies an absolute authoritative private schema path. No personal runtime path is committed as a default.

Executed 2026-10-01 against the frozen coordinator host/frontend: **712 assertions PASS** across six groups, including actual persisted findings/equal-versus-operations comparison/mixed/gaps/healthy/legacy/cancelled outputs, hostile extra properties/scope/profile/origin/Host, strict DTO shapes, raw decimal precision, keyboard start and original disclosure, reload/history, transient analysis503 then keyboard read-only Retry at unchanged revision with heading focus, and stale revision response hidden before retry, and Critical/High-first finding cards. Polling contributes to the executed assertion count, so a later successful count may differ.

Chromium145.0.7632.6, Node24.21.0, axe-core4.13.0. Desktop scan:30 passing rules,0 violations,0 incomplete. 320px expanded original:31 passing rules,0 violations, **color-contrast incomplete and NOT VERIFIED**. Screenshots include desktop,390px and320px. Forced-color labels and no horizontal page overflow at320px passed. The harness stopped its owned host normally. `execution.json` and the integration `evidence.json` contain exact results and artifact binding. The existing consultant recovery harness was unchanged and remains a separate coordinator regression check.

These are engineering checks of an internal synthetic view. Full supported Windows browser/NVDA/Narrator acceptance and full WCAG conformance are NOT VERIFIED. Live rules/AI, review/risk permissions, publication/maturity and production gates remain disabled.
