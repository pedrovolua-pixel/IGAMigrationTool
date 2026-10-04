# Synthetic consultant workspace

React `19.3.0`, React DOM `19.3.0`, TypeScript `7.0.2`, Vite `8.3.1`, Node `24.21.0`, npm `11.20.0`. Exact dependency lock is committed. React/DOM render the requested UI; TypeScript and matching React declaration packages provide static type checks; Vite emits local same-origin static assets. No router, UI library, remote fonts/assets or development listener is needed.

From this directory, run `npm ci`, `npm run build` and `npm run audit`. `build` verifies generated private-demo types, checks TypeScript and writes `dist/`. The coordinator's ASP.NET loopback host serves that directory and the fixed [demo contract](../../contracts/local-demo/README.md). Do not open the HTML as a file or run an external deployment; API calls use relative same-origin paths.

The single screen selects named server fixtures, starts an idempotent run, displays durable history/locked inputs/progress/gaps and requests cancel or expired-lease resume only when server actions allow. `Scoring` is labeled coverage ready / scoring pending. No health score, AI, source access, publication, review or full assessment completion is claimed. Browser session storage preserves only selected run ID; the host/engine persist run facts across restart.

Prettier `3.9.9` provides a pinned formatting check for the new JSX, CSS and contract source. `npm run format` formats those owned sources; `npm run format:check` verifies them. Generated TypeScript is checked by its generator rather than hand-formatted.

Native forms, buttons, tables and progress elements support keyboard navigation. The screen keeps focused controls and selected run stable during polling, announces state transitions through a polite live region, and uses text plus color for state/warnings. Browser E2E/transport recovery checks are owned by V3; full supported Windows NVDA/Narrator/browser/zoom/contrast acceptance remains required and is not established by a build.
