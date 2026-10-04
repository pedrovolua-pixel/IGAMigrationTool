# UI-W5 rendering checks

Use pinned Node24.21.0. Each script takes the absolute `src/web` path as its first argument. Run `risk-verify.mjs`, `ai-verify.mjs`, `areas-verify.mjs`, `overview-verify.mjs` and `report-verify.mjs`. Areas additionally accepts the repository root as its second argument. Overview/report optionally accept an output evidence file.

These use real React SSR and independently computed expectations;402 assertions cover counts, finite bar widths, selected states, stale/missing inputs, inert hostile text, unavailable capabilities, exact report snapshot projections and preserved inline reporting. The unchanged frozen GET-only preview snapshot supplies actual synthetic area/report data. The report test exposes a private pure helper through a test-only Vite transform; production exports are unchanged.

These are rendering checks. Browser interaction evidence is recorded separately, and full mutating/backend/Windows/manual acceptance remains open.
