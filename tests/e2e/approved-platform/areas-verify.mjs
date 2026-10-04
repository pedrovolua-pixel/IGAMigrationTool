import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";

// Usage: pinned-node iga-ui-w4-areas-review-verify.mjs /absolute/path/to/src/web [evidence.json]
const root = path.resolve(process.argv[2] ?? process.cwd());
const require = createRequire(path.join(root, "package.json"));
const { build } = await import(pathToFileURL(require.resolve("vite")));
const output = fs.mkdtempSync(path.join(os.tmpdir(), "iga-areas-review-"));
try {
  await build({
    configFile: false,
    root,
    build: {
      ssr: "src/PlatformAreas.tsx",
      outDir: output,
      emptyOutDir: true,
      rollupOptions: { output: { entryFileNames: "areas.mjs" } },
    },
  });
  fs.symlinkSync(
    path.join(root, "node_modules"),
    path.join(output, "node_modules"),
  );
  const React = require("react");
  const { renderToStaticMarkup } = require("react-dom/server");
  const { PlatformAreas } = await import(
    pathToFileURL(path.join(output, "areas.mjs"))
  );
  const run = {
    runId: "current",
    revision: 2,
    selection: {
      scopeId: "scope",
      scopeLabel: "<script>scope-title</script>",
      baselineLabel: "baseline",
      profileLabel: "profile",
    },
    stateCounts: [],
    lockedInputs: [],
    progress: { terminalUnits: 2, plannedUnits: 3 },
    coverageCompletionKind: null,
    createdAtUtc: "2026-10-03T12:00:00Z",
  };
  const analysis = {
    status: "Ready",
    runId: "current",
    runRevision: 2,
    findings: [
      {
        category: "SECURITY",
        objectIds: [],
        ruleId: "<img src=x onerror=alert(1)>",
        ruleVersion: "1",
      },
    ],
    review: {
      status: "Ready",
      findings: [
        {
          id: "finding",
          title: "<script>review-title</script>",
          history: [
            {
              eventId: "event",
              kind: "Comment",
              recordedAtUtc: "2026-10-03T12:30:00Z",
              actorId: "actor",
              revision: 1,
              state: "Proposed",
              text: "<img src=x onerror=alert(1)>",
              reason: null,
              title: null,
              businessContext: null,
            },
          ],
        },
      ],
    },
  };
  const render = (view, suppliedAnalysis = analysis, suppliedRun = run) =>
    renderToStaticMarkup(
      React.createElement(PlatformAreas, {
        view,
        analysis: suppliedAnalysis,
        run: suppliedRun,
        catalog: null,
        history: null,
        onNavigate: () => {},
        onSelectRun: () => {},
      }),
    );
  let assertions = 0;
  const has = (html, re) => {
    assert.match(html, re);
    assertions++;
  };
  const lacks = (html, re) => {
    assert.doesNotMatch(html, re);
    assertions++;
  };
  const views = [
    "Projects",
    "Sources & baselines",
    "Compare runs",
    "Rule catalog",
    "Audit history",
    "Settings",
    "Migration",
    "Portfolio",
    "Design archive",
  ];
  for (const view of views) {
    has(render(view), /platform-area-heading/);
    has(render(view, null, null), /platform-area-heading/);
  }
  has(render("Rule catalog"), /&lt;img src=x onerror=alert\(1\)&gt;/);
  lacks(render("Rule catalog"), /<img /);
  has(render("Audit history"), /&lt;script&gt;review-title&lt;\/script&gt;/);
  lacks(render("Audit history"), /<script>/);
  lacks(render("Rule catalog", { ...analysis, runRevision: 1 }), /onerror/);
  lacks(
    render("Audit history", { ...analysis, runId: "prior" }),
    /review-title/,
  );
  has(render("Settings"), /&lt;script&gt;scope-title&lt;\/script&gt;/);
  has(render("Portfolio"), /No authorized customer portfolio feed/);
  has(render("Migration"), /No migration engine or execution controls/);
  const evidence = {
    result: "pass",
    assertions,
    scope:
      "Independent read-only PlatformAreas SSR smoke and stale-admission/inert-text checks",
    views,
    scenarios: [
      "nine views with supplied and null records",
      "cross-revision analysis excluded",
      "cross-run analysis excluded",
      "untrusted rule IDs escaped",
      "untrusted review titles and text escaped",
      "untrusted scope label escaped",
      "future portfolio and migration actions unavailable",
    ],
    notVerified: [
      "tab interactions",
      "browser appearance preferences",
      "mobile overflow",
    ],
    files: ["PlatformAreas.tsx", "PlatformAreas.css"].map((name) => ({
      name,
      sha256: createHash("sha256")
        .update(fs.readFileSync(path.join(root, "src", name)))
        .digest("hex"),
    })),
  };
  if (process.argv[3])
    fs.writeFileSync(process.argv[3], JSON.stringify(evidence, null, 2) + "\n");
  console.log(JSON.stringify(evidence));
} finally {
  fs.rmSync(output, { recursive: true, force: true });
}
