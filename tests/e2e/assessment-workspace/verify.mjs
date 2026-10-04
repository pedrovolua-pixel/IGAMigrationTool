import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { chromium } from "../consultant-demo/node_modules/playwright/index.mjs";
import axe from "../consultant-demo/node_modules/axe-core/axe.js";

const base = "http://127.0.0.1:5197";
const output = "/tmp/iga-assessment-workspace-evidence";
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    process.env.IGA_CHROMIUM ??
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const faults = [];
page.on("pageerror", (error) => faults.push(error.message));
const checks = [];
const scans = [];
async function scan(label) {
  await page.evaluate(axe.source);
  const result = await page.evaluate(() =>
    window.axe.run(document, {
      runOnly: {
        type: "tag",
        values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
      },
    }),
  );
  scans.push({
    label,
    version: result.testEngine.version,
    violations: result.violations.map((item) => ({
      id: item.id,
      targets: item.nodes.map((node) => node.target),
    })),
    incomplete: result.incomplete.map((item) => item.id),
  });
  assert.deepEqual(scans.at(-1).violations, [], label);
}
const nav = (name) =>
  page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name, exact: true });
async function jump(name, selector) {
  await nav(name).focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(
    (target) => document.activeElement === document.querySelector(target),
    selector,
  );
  assert.equal(
    await page
      .locator(selector)
      .evaluate((element) => document.activeElement === element),
    true,
    `${name} moves keyboard focus`,
  );
}
try {
  await page.goto(base);
  await page
    .getByRole("heading", { name: "Assessment workspace", exact: true })
    .waitFor();
  await jump("Reports", "#run-heading");
  assert.match(
    await page.locator(".navigation-notice").textContent(),
    /Reports is not available/,
  );
  assert.equal(
    await nav("Assessments").getAttribute("aria-current"),
    "location",
  );
  await jump("Settings", "#configuration-heading");
  assert.equal(await page.locator(".navigation-notice").count(), 0);
  await page.getByLabel("Assessment profile", { exact: true }).waitFor();
  await scan("empty workspace");
  checks.push(
    "No-run availability notice, honest fallback, keyboard settings and live notice clearing",
  );

  const history = await (await fetch(`${base}/local-demo/v1/runs`)).json();
  const analysis = history.runs.find((run) =>
    run.selection.profileId.startsWith("synthetic-review-maturity-"),
  );
  const coverage = history.runs.find(
    (run) => run.selection.profileId === "profile-standard",
  );
  assert.ok(
    analysis && coverage,
    "Preview needs stable review-enabled and coverage-only snapshots",
  );
  await page.evaluate(
    (id) => sessionStorage.setItem("iga.synthetic.selected-run", id),
    analysis.runId,
  );
  await page.reload();
  await page.locator("#analysis-heading").waitFor();
  await page.locator(".draft-report").waitFor();
  await jump("Findings", "#findings-heading");
  await jump("Evidence", "#evidence-heading");
  await jump("Reports", ".draft-report");
  await jump("Settings", "#configuration-heading");
  await jump("Assessments", "#run-heading");
  assert.equal(await page.locator("#scope").count(), 1);
  assert.equal(await page.locator("#baseline").count(), 1);
  assert.equal(await page.locator("#profile").count(), 1);
  await scan("ready synthetic review and draft workspace");
  checks.push(
    "All five destinations focus real mounted sections; visible fixture inputs and existing review/report content",
  );
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: `${output}/workspace-desktop.png` });

  await jump("Reports", ".draft-report");
  await jump("Assessments", "#run-heading");
  const index = history.runs.findIndex((run) => run.runId === coverage.runId);
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .nth(index)
    .click();
  await page.waitForFunction(() =>
    document.activeElement?.classList.contains("run-detail"),
  );
  await page.locator("#analysis-heading").waitFor({ state: "detached" });
  assert.equal(
    await nav("Assessments").getAttribute("aria-current"),
    "location",
  );
  await jump("Reports", "#run-heading");
  assert.match(
    await page.locator(".navigation-notice").textContent(),
    /Reports is not available/,
  );
  checks.push(
    "Changing run clears previous section selection; coverage-only runs do not invent reports",
  );

  const layouts = [];
  for (const [width, height] of [
    [1440, 900],
    [1280, 720],
    [1200, 350],
    [900, 400],
    [760, 700],
    [390, 844],
    [320, 800],
  ]) {
    await page.setViewportSize({ width, height });
    await jump("Settings", "#configuration-heading");
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      `${width}px has no horizontal document overflow`,
    );
    for (const name of [
      "Assessments",
      "Findings",
      "Evidence",
      "Reports",
      "Settings",
    ]) {
      assert.ok(
        await nav(name).isVisible(),
        `${name} stays visible at ${width}px`,
      );
    }
    layouts.push({ width, height, settingsFocus: true, overflow: false });
  }
  await scan("320px coverage workspace");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: `${output}/workspace-narrow.png`,
    fullPage: true,
  });
  await page.emulateMedia({ forcedColors: "active" });
  await jump("Settings", "#configuration-heading");
  await scan("320px forced colors");
  checks.push(
    "Seven viewport sizes including 1200x350 and 320px; keyboard navigation and forced colors",
  );

  const denied = await fetch(`${base}/local-demo/v1/runs`, { method: "POST" });
  assert.equal(
    denied.status,
    503,
    "Read-only preview never forwards a mutation",
  );
  assert.deepEqual(faults, []);
  const report = {
    completedAt: new Date().toISOString(),
    browser: await browser.version(),
    checks,
    layouts,
    scans,
    pageErrors: faults,
    scope:
      "Final built React UI with read-only saved synthetic snapshots; no backend mutation/recovery regression or Windows/assistive-technology acceptance claimed.",
  };
  await writeFile(`${output}/checks.json`, JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
} finally {
  await browser.close();
}
