# Assessment workspace UI checks

UI-W1 checks the actual built React shell using read-only snapshots from the existing synthetic local host. Install the pinned browser/axe packages with `npm ci --ignore-scripts --audit --audit-level=low` in `tests/e2e/consultant-demo/`, build `src/web`, and run these commands from the repository root while a synthetic host is available:

```sh
node tests/e2e/assessment-workspace/preview.mjs
# In a separate terminal:
node tests/e2e/assessment-workspace/verify.mjs
```

The preview listens only on `127.0.0.1:5197`. It reads one completed review/maturity run and one completed coverage-only run through GET, replaces the fixture CSRF token, keeps data only in memory, and refuses every mutation. It forwards no request from the browser to the source. If those two presets have no saved completed run, create them in your own disposable synthetic demo database first. Do not run mutation/restart suites against another task's host or database.

Verification uses the installed Google Chrome by default; set `IGA_CHROMIUM` for another executable. It exercises the five destinations and focus, no-run/coverage-only availability, live notices, switched-run navigation, seven viewport sizes including a short desktop viewport and 320px, forced colors and axe. `/tmp/iga-assessment-workspace-evidence/` receives screenshots and results. Incomplete axe rules require manual review; zero violations does not establish full accessibility acceptance. Server/recovery, supported Windows/browser/screen-reader and live source/production gates are outside this read-only UI check.

This first shell uses section shortcuts in the same mounted assessment. UI-W2 now targets Generated findings directly; Evidence focuses selected finding provenance when open and coverage gaps otherwise. Individual finding provenance remains in the existing finding disclosures. Separate investigation views and additional settings are not implemented by UI-W1.
