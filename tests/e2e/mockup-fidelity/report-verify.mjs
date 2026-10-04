import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import { createHash } from "node:crypto";
const webRoot = process.argv[2] ?? process.cwd();
const require = createRequire(path.join(webRoot, "package.json"));
const { build } = await import(pathToFileURL(require.resolve("vite")));
const output = fs.mkdtempSync(path.join(os.tmpdir(), "iga-report-w5-"));
try {
  await build({
    configFile: false,
    root: webRoot,
    plugins: [
      {
        name: "private-projection-review-export",
        transform(code, id) {
          if (id.endsWith("/DraftReportView.tsx"))
            return code + "\nexport { DedicatedProjection };";
        },
      },
    ],
    build: {
      ssr: "src/DraftReportView.tsx",
      outDir: output,
      emptyOutDir: true,
      rollupOptions: { output: { entryFileNames: "report.mjs" } },
    },
  });
  fs.symlinkSync(
    path.join(webRoot, "node_modules"),
    path.join(output, "node_modules"),
  );
  const React = require("react");
  const { renderToStaticMarkup } = require("react-dom/server");
  const { DraftReportView, DedicatedProjection } = await import(
    pathToFileURL(path.join(output, "report.mjs"))
  );
  const routes = JSON.parse(
    fs.readFileSync(
      path.resolve(
        webRoot,
        "../../work/ui-review/2026-10-03/approved-platform-implementation/preview-snapshot.json",
      ),
      "utf8",
    ),
  );
  const analysis = routes.find(
    ([route, value]) =>
      route.endsWith("/analysis") && value.reportDraft?.status === "Ready",
  )[1];
  const report = analysis.reportDraft;
  const before = JSON.stringify(report);
  const render = (props) =>
    renderToStaticMarkup(React.createElement(DraftReportView, props));
  const projection = (audience, snapshot = report.snapshot) =>
    renderToStaticMarkup(
      React.createElement(DedicatedProjection, { audience, snapshot }),
    );
  let assertions = 0;
  const has = (markup, re) => {
    assert.match(markup, re);
    assertions++;
  };
  const lacks = (markup, re) => {
    assert.doesNotMatch(markup, re);
    assertions++;
  };
  const dedicated = render({ report, dedicated: true });
  for (const tab of [
    "Executive",
    "Practitioner",
    "Auditor",
    "Assessment quality",
    "Publication history",
  ])
    has(dedicated, new RegExp(">" + tab + "<"));
  has(dedicated, /aria-pressed="true">Executive/);
  has(dedicated, /class="report-paper"/);
  has(dedicated, /Draft format · inert Markdown preview/);
  lacks(dedicated, />Summary draft<|>Technical draft<|>Markdown preview</);
  const inline = render({ report });
  has(inline, />Summary draft</);
  has(inline, />Technical draft</);
  has(inline, />Markdown preview</);
  lacks(inline, /Report audiences/);
  for (const unavailable of [
    render({ report: null, dedicated: true }),
    render({ report: { ...report, status: "Unavailable" }, dedicated: true }),
    render({
      report: {
        ...report,
        markdown: { ...report.markdown, canonicalContentDigest: "mismatch" },
      },
      dedicated: true,
    }),
  ]) {
    has(unavailable, /Report audiences/);
    lacks(unavailable, /Current reviewed health/);
  }
  assert.equal(render({ report: null }), "");
  assertions++;
  const executive = projection("Executive");
  for (const finding of report.snapshot.content.findings) {
    if (["Critical", "High"].includes(finding.severity))
      has(executive, new RegExp(finding.title));
    else lacks(executive, new RegExp(finding.title));
  }
  const practitioner = projection("Practitioner");
  for (const finding of report.snapshot.content.findings)
    has(practitioner, new RegExp(finding.title));
  const auditor = projection("Auditor");
  has(auditor, /Locked methodology/);
  has(auditor, new RegExp(report.snapshot.source.reviewSnapshotDigest));
  for (const finding of report.snapshot.content.reviewHistory)
    for (const event of finding.events) has(auditor, new RegExp(event.actorId));
  const quality = projection("Assessment quality");
  has(quality, /Assessment quality and limitations/);
  has(
    quality,
    new RegExp(
      "Gap units<\\/dt><dd>" + report.snapshot.content.quality.gapUnits + "<",
    ),
  );
  const history = projection("Publication history");
  has(history, /Publication history is unavailable/);
  has(history, /Publication unavailable/);
  lacks(
    history,
    /Published · immutable|Acknowledged ·|Sample consultant|25 Sep/,
  );
  const hostile = structuredClone(report);
  hostile.snapshot.content.findings.find(
    (item) => item.severity === "Critical",
  ).title = "<script>unsafe title</script>";
  hostile.markdown.markdownText =
    "<img src=x onerror=alert(1)> [evil](javascript:alert(1))";
  const escaped = render({ report: hostile, dedicated: true });
  has(escaped, /&lt;script&gt;unsafe title&lt;\/script&gt;/);
  has(escaped, /&lt;img src=x onerror=alert\(1\)&gt;/);
  lacks(escaped, /<script>|<img |javascript:.*href/);
  assert.equal(JSON.stringify(report), before);
  assertions++;
  const file = path.join(webRoot, "src/DraftReportView.tsx");
  const evidence = {
    result: "pass",
    assertions,
    scope:
      "Pure SSR of actual frozen canonical report and all five internal audience projections; private helper exported only by test build",
    notVerified: [
      "browser tab interactions",
      "integrated application styling",
      "live publication",
    ],
    sha256: createHash("sha256").update(fs.readFileSync(file)).digest("hex"),
  };
  fs.writeFileSync(
    process.argv[3] ?? "/private/tmp/iga-ui-w5-report-evidence.json",
    JSON.stringify(evidence, null, 2) + "\n",
  );
  console.log(JSON.stringify(evidence));
} finally {
  fs.rmSync(output, { recursive: true, force: true });
}
