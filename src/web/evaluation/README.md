# Local synthetic evaluation workspace

This isolated React workspace consumes the frozen cycle04 loopback host only. It does not change the existing consultant app or package/lock. All source/configuration/tests are beneath this directory. Use pinned Node24.21.0/npm11.20.0 and the unchanged `../package-lock.json` dependencies. No new package was added. The dedicated Vite config uses Node’s builtin fileURLToPath for platform paths; its local build-only declaration avoids adding Node browser types. Native Windows execution remains a separate hosted check.

From repository root, with `src/web/node_modules` installed from the parent lock:

```sh
node src/web/node_modules/typescript/bin/tsc --project src/web/evaluation/tsconfig.json --noEmit
node --test src/web/evaluation/workflow.test.ts
node src/web/node_modules/prettier/bin/prettier.cjs --check src/web/evaluation --ignore-path src/web/evaluation/.prettierignore
node src/web/node_modules/vite/bin/vite.js build --config src/web/evaluation/vite.config.ts
```

Run the parent dependency audit using the pinned npm. The dedicated build writes `evaluation/dist` and is served by the coordinator-owned explicitly enabled LocalEvaluationDemo at127.0.0.1:5184; do not open the HTML as a file or publish this fixture workspace. Same-origin `/local-evaluation/v1` APIs enforce current server authority, source locks and antiforgery. No default existing host/UI activation follows.

The browser admits closed bounded responses (16MiB streamed UTF8;256KiB commands), rejects duplicate JSON keys, validates the fixed selected-ID/metadata golden, hashes four supplied raw outcome canonical envelopes and checks nested locks/relationships. Source/population/sample digests are server-verified anchors; omitted120-source bytes are not independently hashed by this browser. Event hashes shown as metadata do not confer authority. Accuracy uses exact server-supplied confirmed/denominator fractions, no client percent or gate calculation.

Review drafts require explicit prepare and record. Prepared UUID/body is immutable; transport uncertainty offers an explicit byte-identical retry, never automatic resubmit/rebase. Invalid/denied/stale responses clear protected cached source/history/outcome counts; current denial does not change persisted cohort or historical outcomes. Version/history reads reauthorize the current whole workspace before exposing existing source presentation. Historical versions are read-only; source original/correction presentation remain distinct. Text is inert React text, with no payload logging/persistence/download/execution.

Focused schema/state tests use separately constructed literal policy fixtures and independent Node SHA256, not host output. Executed checks/native evidence are coordinator-indexed; browser/HTTP/database integration is separately owned. Full live reviewer authority, actual Phase1B source adapter, queue/adjudication, evidence lifecycle, risk/report/reassessment, manual supported accessibility and fullM08/G1–G9 remain unverified. Disabling the local host/profile/assets rolls back availability without deleting immutable store records.
