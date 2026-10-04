import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { writeFile, unlink } from "node:fs/promises";
const web = process.argv[2] ?? process.cwd();
const require = createRequire(`${web}/package.json`);
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const { build } = await import(`${web}/node_modules/vite/dist/node/index.js`);
const output = await build({
  root: web,
  configFile: false,
  logLevel: "silent",
  build: {
    ssr: `${web}/src/PlatformAreas.tsx`,
    write: false,
    minify: false,
    rollupOptions: { external: ["react", "react/jsx-runtime"] },
  },
});
const compiled = output.output
  .find((chunk) => chunk.type === "chunk")
  .code.replace(
    /from "react\/jsx-runtime"/g,
    `from '${web}/node_modules/react/jsx-runtime.js'`,
  )
  .replace(/from "react"/g, `from '${web}/node_modules/react/index.js'`);
const modulePath = `/private/tmp/iga-ui-w5-areas-render-${process.pid}.mjs`;
await writeFile(modulePath, compiled);
const { PlatformAreas } = await import(modulePath);
const root =
  process.argv[3] ??
  new URL("../../", `file://${web}/`).pathname.replace(/\/$/, "");
const fixture = new Map(
  JSON.parse(
    readFileSync(
      `${root}/work/ui-review/2026-10-03/approved-platform-implementation/preview-snapshot.json`,
      "utf8",
    ),
  ),
);
const catalog = fixture.get("/local-demo/v1/catalog"),
  history = fixture.get("/local-demo/v1/runs");
const runId = history.runs[0].runId;
const run = fixture.get(`/local-demo/v1/runs/${runId}`),
  analysis = fixture.get(`/local-demo/v1/runs/${runId}/analysis`);
let checks = 0;
const check = (condition, message) => {
  assert.ok(condition, message);
  checks++;
};
const render = (view, override = {}) =>
  renderToStaticMarkup(
    React.createElement(PlatformAreas, {
      view,
      run,
      catalog,
      history,
      analysis,
      onNavigate: () => {},
      onSelectRun: () => {},
      ...override,
    }),
  );
for (const view of [
  "Projects",
  "Sources & baselines",
  "Rule catalog",
  "Migration",
  "Portfolio",
  "Audit history",
  "Settings",
  "Design archive",
  "Compare runs",
]) {
  const html = render(view);
  check(
    html.includes(view.replaceAll("&", "&amp;")),
    "area is rendered: " + view,
  );
  check(!html.includes("<script"), "no executable input: " + view);
}
const settings = render("Settings");
for (const label of [
  "General",
  "Assessment",
  "AI",
  "Access",
  "Data policy",
  "Notifications",
  "Integrations",
  "Appearance",
])
  check(settings.includes(label), "setting tab " + label);
check(settings.includes("Project name"), "aligned General form");
check(
  settings.includes("Save settings · unavailable"),
  "settings cannot mutate server",
);
check(settings.includes('disabled=""'), "unknown configuration disabled");
const sources = render("Sources & baselines");
for (const label of [
  "Connection",
  "Object inventory",
  "Baselines",
  "Collection history",
])
  check(sources.includes(label), "source tab " + label);
check(
  sources.includes("Synthetic local host"),
  "source cannot imply customer connection",
);
const rules = render("Rule catalog");
for (const label of ["Catalog", "Version history", "Custom rules"])
  check(rules.includes(label), "rule tab " + label);
check(rules.includes(analysis.findings[0].ruleId), "actual rule included");
const stale = render("Rule catalog", {
  analysis: { ...analysis, runRevision: analysis.runRevision + 1 },
});
check(!stale.includes(analysis.findings[0].ruleId), "stale rule excluded");
check(
  stale.includes("Verified analysis is unavailable"),
  "stale state visible",
);
const cross = render("Rule catalog", {
  analysis: { ...analysis, runId: "other-run" },
});
check(!cross.includes(analysis.findings[0].ruleId), "cross run excluded");
const migration = render("Migration");
for (const label of ["Readiness", "Mappings", "Scope decisions", "Validation"])
  check(migration.includes(label), "migration tab " + label);
check(migration.includes("No migration engine"), "migration remains inactive");
const portfolio = render("Portfolio");
check(
  (portfolio.match(/<strong>—<\/strong>/g) || []).length === 4,
  "four unknown portfolio stats",
);
check(
  portfolio.includes("not a customer portfolio entry"),
  "no fake customer inference",
);
check(!portfolio.includes("72 / 100"), "no approved sample score copied");
const audit = render("Audit history");
for (const label of ["Time", "Actor", "Event", "Reference", "Details"])
  check(audit.includes(`<th>${label}</th>`), "audit column " + label);
check(
  audit.includes(
    analysis.review.findings.find((f) => f.history.length).history[0].eventId,
  ),
  "actual audit event preserved",
);
check(
  !audit.includes("platform-areas-timeline"),
  "audit matches compact table",
);
const archive = render("Design archive");
check(
  (archive.match(/<img /g) || []).length === 10,
  "all ten actual screenshot cards",
);
for (const match of archive.matchAll(/src="([^"]+)"/g))
  check(
    readFileSync(`${root}/src/web/public${match[1]}`).length > 0,
    "real screenshot asset " + match[1],
  );
const hostile = render("Settings", {
  run: {
    ...run,
    selection: {
      ...run.selection,
      scopeLabel: '<script>alert("bad")</script>',
    },
  },
});
check(hostile.includes("&lt;script&gt;"), "untrusted scope escapes");
check(!hostile.includes("<script>"), "escaped scope cannot execute");
for (const view of [
  "Sources & baselines",
  "Settings",
  "Rule catalog",
  "Migration",
  "Portfolio",
  "Audit history",
])
  check(
    render(view, { run: null, analysis: null, catalog: null, history: null })
      .length > 0,
    "null records handled " + view,
  );
console.log(
  JSON.stringify(
    {
      status: "PASS",
      checks,
      scope: "SSR only; UI interaction pending coordinator",
    },
    null,
    2,
  ),
);

await unlink(modulePath);
