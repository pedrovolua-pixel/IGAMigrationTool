import fs from "node:fs/promises";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const web = process.argv[2] ?? process.cwd();
const require = createRequire(`${web}/package.json`);
const { build } = await import(`${web}/node_modules/vite/dist/node/index.js`);
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const output = await build({
  root: web,
  configFile: false,
  logLevel: "silent",
  build: {
    ssr: `${web}/src/RiskDistribution.tsx`,
    write: false,
    minify: false,
    rollupOptions: { external: ["react", "react/jsx-runtime"] },
  },
});
let compiled = output.output.find((chunk) => chunk.type === "chunk").code;
compiled = compiled
  .replace(
    /from "react\/jsx-runtime"/g,
    `from '${web}/node_modules/react/jsx-runtime.js'`,
  )
  .replace(/from "react"/g, `from '${web}/node_modules/react/index.js'`);
const modulePath = `/private/tmp/iga-ui-w5-risk-render-${process.pid}.mjs`;
await fs.writeFile(modulePath, compiled);
const { RiskDistribution } = await import(modulePath);
let assertions = 0;
const ok = (condition, description) => {
  assert.ok(condition, description);
  assertions++;
};
const rows = Object.freeze(
  [
    ["Security", "Critical"],
    ["Security", "Critical"],
    ["Security", "High"],
    ["Security", "Informational"],
    ["Operations", "Medium"],
    ["Operations", "Medium"],
    ["Operations", "Low"],
    ['<script>alert("x")</script>', "High"],
  ].map(([category, severity], i) =>
    Object.freeze({ id: String(i), category, severity }),
  ),
);
const levels = ["Critical", "High", "Medium", "Low", "Informational"];
function render(findings, category = "", severity = "") {
  return renderToStaticMarkup(
    React.createElement(RiskDistribution, {
      findings,
      category,
      severity,
      onFilter: () => {
        throw new Error("Render invoked mutation callback");
      },
    }),
  );
}
for (const category of [
  "",
  "All",
  "Security",
  "Operations",
  "Missing category",
]) {
  const html = render(rows, category, "Low");
  for (const level of levels) {
    const count = rows.filter(
      (row) =>
        (!category || category === "All" || row.category === category) &&
        row.severity === level,
    ).length;
    const escapedCategory = category
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
    ok(
      html.includes(
        `aria-label="${level} findings, ${count}${category && category !== "All" ? ` in ${escapedCategory}` : ""}"`,
      ),
      `${category || "all"} ${level} count ${count}`,
    );
  }
  ok(
    !html.includes("NaN") && !html.includes("Infinity"),
    `${category || "all"} finite scale`,
  );
  for (const match of html.matchAll(/width:([\d.]+)%/g))
    ok(Number(match[1]) >= 0 && Number(match[1]) <= 100, "bars bounded 0-100");
  ok(
    html.includes('aria-label="Operations, Medium, 2 findings"'),
    "category matrix independent of selected category",
  );
  ok(
    html.includes('aria-label="Security, Critical, 2 findings"'),
    "duplicate rows counted independently",
  );
  ok(!html.includes("<script>"), "hostile category rendered as inert text");
  ok(
    html.includes("&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;"),
    "hostile category preserved escaped",
  );
  ok(
    html.includes('tabindex="0" role="region"'),
    "heatmap scroll has keyboard region",
  );
  ok(
    (html.match(/class="risk-distribution-bar /g) || []).length === 5,
    "all five severity bars",
  );
  ok(
    (html.match(/scope="col"/g) || []).length === 6,
    "heatmap labels category plus five severity columns",
  );
}
for (const level of levels) {
  const html = render(rows, "Security", level);
  const bar = html.match(
    new RegExp(
      `<button[^>]*class="risk-distribution-bar risk-severity-${level.toLowerCase()}"[^>]*>`,
    ),
  )?.[0];
  ok(bar?.includes('aria-pressed="true"'), `${level} selected bar announced`);
  const activeCells =
    html.match(
      /<button[^>]*class="risk-distribution-cell [^"]+"[^>]*aria-pressed="true"[^>]*>/g,
    ) || [];
  ok(
    activeCells.length === 1,
    "selected category + severity marks exactly one matrix cell",
  );
}
const empty = render([]);
ok(
  empty.includes("No findings in this assessment."),
  "empty assessment explicitly stated",
);
ok(
  empty.includes("No category hotspots to display."),
  "empty matrix explicitly stated",
);
ok(empty.includes("scale 0–0"), "empty scale zero");
ok((empty.match(/width:0%/g) || []).length === 5, "empty bars zero width");
ok(
  !empty.includes("NaN") && !empty.includes("Infinity"),
  "empty finite values",
);
ok(
  rows.length === 8 && rows[0].category === "Security",
  "frozen data unchanged",
);
const css = await fs.readFile(`${web}/src/RiskDistribution.css`, "utf8");
ok(css.includes("repeat(2, minmax(0, 1fr))"), "equal desktop panels");
ok(css.includes("@media (max-width: 760px)"), "mobile single column");
ok(css.includes("overflow-x: auto"), "heatmap panel-contained overflow");
console.log(
  JSON.stringify(
    {
      assertions,
      result: "pass",
      scenarios: [
        "all",
        "All alias",
        "selected category",
        "missing selected category",
        "empty findings",
        "all five selected severities",
        "duplicate rows",
        "hostile category escaping",
        "frozen input",
        "accessible scroll region",
        "bounded widths",
        "equal panels/mobile CSS",
      ],
    },
    null,
    2,
  ),
);

await fs.unlink(modulePath);
