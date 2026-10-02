# Offline synthetic AI proposal preview

Cycle09's separate `SyntheticAiPreview` module projects immutable values from the real Cycle08 packet builder and proposal validator into a complete typed Proposed preview and encoded inert HTML. The [internal fixture contract](../../specs/003-health-assessment/synthetic-ai-preview-contract.md) specifies exact bindings, canonical bytes, fields, disclaimer and rendering constraints. The current application does not call this module; historical disabled-AI profiles and saved findings/scores/review/drafts/guidance retain their contracts.

After the pinned locked restore and Release build, run the portable hosts:

```sh
dotnet run --project tests/unit/SyntheticAiPreview.Tests --configuration Release --no-build
dotnet run --project tests/unit/SyntheticAiPreviewHtml.Tests --configuration Release --no-build
dotnet run --project tests/integration/SyntheticAiPreview.Tests --configuration Release --no-build
```

The independent integration host can write four fixed fictional preview documents with `--write-previews <absolute-owned-directory>`. The browser verifier at `tests/e2e/synthetic-ai-preview/verify.mjs` accepts that directory through `--preview-directory` and the existing pinned Playwright package through `--playwright-module`; an optional `--browser-executable` selects the approved local Chromium. It owns and closes its ephemeral loopback server. No arbitrary files, application routes, provider access or real evidence are exposed.

The document displays typed statements, plain citation IDs, missing context and declared uncertainty. The persistent disclaimer states Proposed/untrusted status. Empty proposals do not establish healthy coverage. Source digests identify a value and never grant access. No evidence reference is resolved. Dynamic strings cannot supply markup, attributes, script, URLs, styles, event handlers or action controls. The fixed CSP is a local preview guard; this does not prove production sandbox/header enforcement or real-model injection resistance.

The module receives only immutable Cycle08 factory values, whose nonpublic constructors are an internal trusted-value boundary. Its digest/binding checks do not prove factual accuracy, semantic redaction or real authorization. Canonical JSON escaping can expand legitimate factory output beyond the original wire byte limit; the projection uses its separate documented guard and preserves valid Unicode values.

Execution summaries contain codes/counts/digests and exclude supplied provider/evidence strings. All fixtures and screenshots are fictional. Actual provider US/ZDR, evaluated model/token/budget policy, storage/recovery/deletion/retention, customer scope enforcement, production renderer integration and supported Windows/screen-reader/manual acceptance remain separate. Full Milestone6/7/9, TP-HAS-008/009/013/014 and G1–G9 remain open.

There is no database migration, additional external dependency or runtime setting. Rollback removes this standalone module/hosts and solution/CI entries without interpreting saved data differently. A future application integration must settle its exact opt-in source/profile/projection and authority contracts before activation.
