import fs from "node:fs/promises";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const web = process.argv[2] ?? process.cwd();
const require = createRequire(`${web}/package.json`);
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const { build } = await import(`${web}/node_modules/vite/dist/node/index.js`);
const result = await build({
  root: web,
  configFile: false,
  logLevel: "silent",
  build: {
    ssr: `${web}/src/AiWorkspace.tsx`,
    write: false,
    minify: false,
    rollupOptions: { external: ["react", "react/jsx-runtime"] },
  },
});
const compiled = result.output
  .find((chunk) => chunk.type === "chunk")
  .code.replace(
    /from "react\/jsx-runtime"/g,
    `from '${web}/node_modules/react/jsx-runtime.js'`,
  )
  .replace(/from "react"/g, `from '${web}/node_modules/react/index.js'`);
const modulePath = `/private/tmp/iga-ui-w5-ai-render-${process.pid}.mjs`;
await fs.writeFile(modulePath, compiled);
const { AiWorkspace } = await import(modulePath);
let assertions = 0;
function ok(condition, message) {
  assert.ok(condition, message);
  assertions++;
}
const run = Object.freeze({
  runId: "actual-run",
  revision: 9,
  createdAtUtc: "2026-10-03T14:00:00Z",
  selection: Object.freeze({
    scopeLabel: "<script>customer</script>",
    baselineLabel: "Actual baseline",
    profileLabel: "Actual profile",
    baselineId: "baseline",
    profileId: "profile",
  }),
});
const findings = Object.freeze([
  { id: "one", state: "Proposed" },
  { id: "two", state: "Proposed" },
  { id: "three", state: "Confirmed" },
]);
const analysis = Object.freeze({
  runId: run.runId,
  runRevision: run.revision,
  status: "Ready",
  findings,
  review: { status: "Ready", findings: [{ id: "one", state: "Confirmed" }] },
  aiPreview: null,
});
const preview = React.createElement(
  "div",
  { "data-preview-marker": "mounted" },
  "Existing proposal controller",
);
function render(run, analysis, onSettings) {
  return renderToStaticMarkup(
    React.createElement(AiWorkspace, { run, analysis, preview, onSettings }),
  );
}
for (const scenario of [
  { name: "matching", run, analysis, count: true },
  { name: "no run", run: null, analysis, count: false },
  { name: "no analysis", run, analysis: null, count: false },
  {
    name: "other run",
    run,
    analysis: { ...analysis, runId: "stale" },
    count: false,
  },
  {
    name: "other revision",
    run,
    analysis: { ...analysis, runRevision: 8 },
    count: false,
  },
  {
    name: "unavailable",
    run,
    analysis: { ...analysis, status: "Unavailable" },
    count: false,
  },
  {
    name: "empty findings",
    run,
    analysis: { ...analysis, findings: [] },
    count: true,
  },
]) {
  const html = render(scenario.run, scenario.analysis);
  ok(
    (html.match(/role="tab"/g) || []).length === 4,
    `${scenario.name} 4 exact tabs`,
  );
  for (const tab of [
    "Analysis",
    "Proposed findings",
    "Analysis history",
    "Deep analysis",
  ])
    ok(html.includes(`>${tab}</button>`), `${scenario.name} ${tab}`);
  ok(
    (html.match(/role="tabpanel"/g) || []).length === 4,
    `${scenario.name} 4 mounted panels`,
  );
  ok(
    (html.match(/role="tabpanel" tabindex="0"/g) || []).length === 4,
    `${scenario.name} tabpanels keyboard accessible`,
  );
  ok(
    (html.match(/hidden=""/g) || []).length === 3,
    `${scenario.name} default Analysis visible only`,
  );
  ok(
    html.includes('data-preview-marker="mounted"'),
    `${scenario.name} existing preview mounted`,
  );
  ok(
    html.includes(
      "Interactive AI is unavailable in this pilot. No provider request can be sent.",
    ),
    `${scenario.name} provider unavailability explicit`,
  );
  ok(
    /<textarea[^>]*disabled=""/.test(html),
    `${scenario.name} composer disabled`,
  );
  ok(!html.includes("<form"), `${scenario.name} no transport form`);
  ok(
    !html.includes("<script>"),
    `${scenario.name} hostile scope remains inert`,
  );
  ok(
    !html.includes("42%") &&
      !html.includes("NaN") &&
      !html.includes("Infinity"),
    `${scenario.name} no invented or nonfinite usage`,
  );
  ok(
    html.includes("Provider usage and budget limits are not supplied"),
    `${scenario.name} budget unavailable`,
  );
  if (scenario.count)
    ok(
      html.includes(
        `${scenario.analysis.findings.length} assessment findings · ${scenario.analysis.findings.length ? 1 : 0} currently proposed.`,
      ),
      `${scenario.name} review state controls actual counts`,
    );
  else
    ok(
      html.includes("Admitted assessment findings unavailable."),
      `${scenario.name} stale counts suppressed`,
    );
}
const countPreview = {
  status: "Ready",
  snapshot: { proposals: [{}, {}] },
  runId: run.runId,
  runRevision: run.revision,
  baselineId: "baseline",
  profileId: "profile",
};
ok(
  render(run, { ...analysis, aiPreview: countPreview }, () => {}).includes(
    "2 offline AI proposals; separate from deterministic findings.",
  ),
  "exact offline proposal count",
);
const unavailableWithPreview = render(run, {
  ...analysis,
  status: "Unavailable",
  aiPreview: countPreview,
});
ok(
  unavailableWithPreview.includes(
    "2 offline AI proposals; separate from deterministic findings.",
  ),
  "health unavailable retains independently bound ready offline preview count",
);
ok(
  unavailableWithPreview.includes("Admitted assessment findings unavailable."),
  "health unavailable does not invent admitted finding counts",
);
ok(
  render(run, {
    ...analysis,
    status: "Unavailable",
    runRevision: 8,
    aiPreview: countPreview,
  }).includes("Offline AI proposal count unavailable."),
  "unavailable health stale response suppresses preview count",
);
ok(
  render(run, {
    ...analysis,
    aiPreview: { ...countPreview, baselineId: "wrong" },
  }).includes("Offline AI proposal count unavailable."),
  "mismatched baseline suppressed",
);
ok(
  render(run, {
    ...analysis,
    aiPreview: { ...countPreview, profileId: "wrong" },
  }).includes("Offline AI proposal count unavailable."),
  "mismatched profile suppressed",
);
ok(
  render(run, analysis).includes("03 Oct 2026"),
  "date comes from actual run UTC",
);
ok(
  render(run, analysis, () => {}).includes(">Open settings →</button>"),
  "routing button accurately describes general settings",
);
ok(
  render(
    { ...run, createdAtUtc: "<script>bad date</script>" },
    analysis,
  ).includes("&lt;script&gt;bad date&lt;/script&gt;"),
  "invalid date escaped without crash",
);
const css = await fs.readFile(`${web}/src/AiWorkspace.css`, "utf8");
ok(
  css.includes("minmax(0, 1.5fr) minmax(260px, 1fr)"),
  "approved assistant/context proportions",
);
ok(
  css.includes("border-bottom-color: var(--platform-blue"),
  "active underline tabs",
);
ok(css.includes("@media (max-width: 760px)"), "mobile stacked panels");
ok(css.includes("@media (max-width: 480px)"), "mobile stacked composer");
console.log(
  JSON.stringify(
    {
      result: "pass",
      assertions,
      scenarios: [
        "matching",
        "null run/analysis",
        "stale ID/revision",
        "unavailable/empty",
        "review state counts",
        "proposal baseline/profile admission",
        "preview remains mounted",
        "disabled composer",
        "escaping",
        "real date",
        "no fabricated usage",
        "approved split/mobile CSS",
      ],
    },
    null,
    2,
  ),
);

await fs.unlink(modulePath);
