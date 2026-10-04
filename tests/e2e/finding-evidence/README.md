# Finding/evidence workspace UI-W2 verification

This suite starts **only its own** synthetic host at `127.0.0.1:5183` and requires a task-owned, disposable PostgreSQL18 database named `iga_synthetic_uiw2`. It refuses an occupied host port. It starts fixed fixtures, filters the actual built React view, appends synthetic review/presentation events through the existing UI, and stops its host in finally. No customer evidence, new API or authorization contract is involved.

Use pinned Node24.21.0, SDK10.0.401 and the locked Playwright1.62.1/axe4.13.0 packages in `tests/e2e/consultant-demo`. Build the frontend and synthetic host first. From the repository root:

```sh
IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55459;Database=iga_synthetic_uiw2;Username=iga_synthetic' \
IGA_HOST_DLL='/absolute/task-owned/host/bin/LocalConsultantDemo.dll' \
IGA_HOST_WORKING_DIRECTORY='/absolute/task-owned/host' \
IGA_CHROMIUM='/absolute/path/to/chromium' \
node tests/e2e/finding-evidence/verify.mjs
```

`IGA_DOTNET` optionally selects the pinned dotnet executable; `IGA_UI_EVIDENCE` selects the output directory (default `/tmp/iga-uiw2-evidence`). The task-owned host working directory must contain `src/web/dist`. `IGA_UI_REVIEW_HOLD=1` retains the owned host after successful checks until SIGINT for bounded developer review; it is not a background automation.

- FE001: combined query/severity/category/state/confidence, mandatory-review shortcut, zero matches and clear; real analysis/review/canonical draft digests do not change from filtering.
- FE002: original/current distinction, supplied facts and references, original unverified options, keyboard open/Close/Escape, selected item outside filters, return-focus fallback.
- FE003: actual Confirm and coherent same-run refresh preserve selection and filters; disappeared state options remain visibly selected; filtering does not unmount or erase unsaved text in existing review editors.
- FE004: actually saved hostile title/context stay inert and searchable; immutable original/provenance remain intact.
- FE005: desktop, short desktop,390/320px, forced colors, axe and run-change reset; historical coverage-only Evidence falls back to gaps.

Existing coverage/recovery, analysis/scoring, review/maturity, draft and structured-guidance suites remain separate regressions. Run them sequentially with their dedicated synthetic databases and owned host variables. To preserve historical evidence, UI-W2 executes byte-identical copies of their definitions/schema in a task-owned shadow directory and records new outputs separately. No historical passing result substitutes for a current execution.

Engineering verification on macOS Chrome does not establish the approved Windows11/Edge/Chrome/Firefox ESR/NVDA/Narrator, manual contrast/ARIA, zoom or approved-scale acceptance. Snapshot filters are local presentation state; persistent saved views, role defaults, new source fields and protected raw-evidence resolution remain later work.
