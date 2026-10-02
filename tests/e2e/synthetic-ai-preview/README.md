# Actual browser checks of fictional standalone previews

First execute `SyntheticAiPreview.Tests --write-previews <absolute-directory>`. Then run this harness with the existing pinned toolchain:

```
node verify.mjs --preview-directory <absolute-directory> --playwright-module <absolute-installed-playwright-package-directory> [--browser-executable <absolute-browser-path>]
```

The harness installs no package and changes no shared dependency. It owns an ephemeral loopback HTTP server that serves only four exact generated HTML filenames, rejects other paths/methods, verifies file digests and stops its browser/server in `finally`. No existing application/host/database is used or restarted.

Actual Chromium checks cover desktop 1440x1000, mobile390x844 and narrow320x844. They verify exact complete source/status/digests, ordered proposals and typed explanation/citation/conflict/missing-context/uncertainty display, explicit empty coverage warning, hostile Unicode text fidelity, fixed CSP, no active nodes/events/resource attributes or script side effect, exactly one document request/no external or other resource dispatch, no dialog/page error, keyboard skip/source/proposal navigation and no horizontal overflow. Screenshots contain only fictional fixed fixtures. Execution metadata prints codes/counts/hashes/paths rather than fixture/provider content.

Bounded TP-HAS-008/009/013/014 evidence only. Chromium does not establish supported Windows/browser/manual screen-reader acceptance, WCAG conformance, a deployed production sandbox or actual provider/authorization/semantic-redaction/model-quality acceptance. Full milestones and gates remain open.

HTML display has standard parser normalization: in Chromium HTML body text, NUL is removed and CR/CRLF becomes LF. V9 separately checks original typed values/serialized HTML bytes and the normalized DOM text. It does not claim lossless browser text for controls or introduce an escaped-control display policy.
