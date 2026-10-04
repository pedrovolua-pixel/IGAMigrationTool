# Complete navigable platform mockup

Open **http://127.0.0.1:5211/**. The recovered original remains separately available at **http://127.0.0.1:5210/**.

The preview uses the recovered chart-led overview, the original relationship inspector, and overlay panels without blur. Light is the default appearance; Settings → Appearance includes Light, Dark, and System modes.

## Navigate

Use the left navigation; scroll its middle section to reach all areas. On mobile, open the navigation menu. The workspace contains 18 primary areas:

- Overview, Assessments, Risk analysis, Evidence, Outcomes & maturity
- AI workspace, Recommendations, Tasks & reviews, Reports, Compare runs
- Projects, Sources & baselines, Rule catalog, Settings, Audit history
- Migration, Portfolio, Design archive

Settings contains General, Assessment, AI, Access, Data policy, Notifications, Integrations, and Appearance. The tabbed areas collectively expose 35 secondary screens, in addition to finding overlays and sample-flow dialogs.

Design archive lets you browse and enlarge all 10 original PNG screenshots from `work/ui-review/visual-concepts/`. Its comparison link opens the recovered original prototype.

AI has a scripted sample conversation, evidence citations, proposed findings, analysis history, and a Phase 2 deep-analysis preview. Other interactions include severity/category filtering, graph-node selection, evidence search, a three-step assessment wizard, session-only task movement, locally saved preview settings, sample CSV/Markdown downloads, and publication/share previews.

## Review boundary

This is a fictional-data mockup. It does not call a backend, run AI, connect to a source system, change permissions, publish reports, or execute migration. Future-phase screens are explicitly labeled. The production application and canonical pilot status are untouched by this artifact.

Settings are saved only in this browser's local storage under `iga-platform-mock-settings`; Reset sample settings clears those preview preferences. Other interactions reset on reload. Nothing is sent to a remote service.

The source is saved in this repository's work directory. **No Git commit or push was made for this mockup.**

## Run / rebuild

The current preview runs as a detached local Node process. No Codex restart is needed. To restart later with Node installed:

```sh
cd work/ui-review/2026-10-03/platform-mockup
node preview.mjs
```

`index.html`, `platform.css`, and `screenshots/` form the portable preview. `platform.js` contains the extension source. `build.py` assembles it with the preserved original source in the neighboring `recovered-graphical-prototype/` directory:

```sh
python3 build.py
```

After rebuilding, format the generated HTML with the repository's existing Prettier installation if desired. The server reads files per request; reload the page to see changes.

## Executed verification

- Navigated all 18 primary areas and all 35 secondary screens in the browser.
- Submitted a sample AI query and followed its evidence citation into the relationship overlay.
- Verified no backdrop blur and Escape dismissal of the finding overlay.
- Saved an AI run budget, reloaded, and verified persistence; reset the test preference afterward.
- Completed the three-step sample assessment wizard and moved a task to In progress.
- Verified Light / Dark appearance switching and restored the default Light state.
- Tested mobile navigation and document width at 390 × 844 across 10 main areas; no horizontal document overflow.
- Inspected the mobile relationship overlay and enlarged an original screenshot successfully.
- Checked generated JavaScript and server syntax with `node --check`, and parsed the Python build script.
- Ran Prettier checks on generated HTML, extension JavaScript, CSS, and preview server; all passed.
- Confirmed HTTP 200 for the preview, CSS, and sampled image assets; served HTML matched the saved file.
- Confirmed POST, PUT, PATCH, and DELETE return 405; the preview has no backend operations.
- Browser error/warning logs were empty during verification.
- Confirmed the recovered source remains byte-identical to its original; SHA-256 `c7c6e233a70ec821fd7f4d7ee74bf1ab9220def4eb9793b0ba02e4225488a3d3`.

Production application builds, integration tests, and end-to-end suites were not run: this change is isolated to a standalone review artifact, with no application-code changes or dependencies.

See `DESIGN.md` for the visual thesis and repository specification mapping. Saved desktop and mobile captures are included beside the source.
