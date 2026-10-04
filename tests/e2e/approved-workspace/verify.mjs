import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { chromium } from "../consultant-demo/node_modules/playwright/index.mjs";
import axe from "../consultant-demo/node_modules/axe-core/axe.js";

// Run only against a coordinator-owned disposable synthetic host.
const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5198";
const url = new URL(base);
assert.equal(url.hostname, "127.0.0.1");
assert.equal(url.protocol, "http:");
assert.equal(
  process.env.IGA_UI_OWNED_SYNTHETIC,
  "1",
  "Requires coordinator-owned disposable synthetic host",
);
const output = process.env.IGA_UI_EVIDENCE ?? "/tmp/iga-ui-w3-independent";
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath: process.env.IGA_CHROMIUM,
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(30000);
const groups = [],
  scans = [],
  faults = [];
page.on("pageerror", (error) => faults.push(error.message));
const record = (text) => {
  groups.push(text);
  console.log(text);
};
const nav = (name) =>
  page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name, exact: true });
const explorer = page.locator(".findings-explorer");
const detail = page.locator(".finding-detail");
const search = page.getByLabel("Search findings", { exact: true });
const focusIs = (selector) =>
  page.waitForFunction(
    (selector) => document.activeElement === document.querySelector(selector),
    selector,
  );
const headingIs = (title) =>
  page.getByRole("heading", { level: 1, name: title, exact: true }).waitFor();
async function scan(label) {
  await page.evaluate(axe.source);
  const data = await page.evaluate(() =>
    window.axe.run(document, {
      runOnly: {
        type: "tag",
        values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
      },
    }),
  );
  const summary = {
    label,
    version: data.testEngine.version,
    violations: data.violations.map((v) => ({
      id: v.id,
      targets: v.nodes.map((n) => n.target),
    })),
    incomplete: data.incomplete.map((v) => ({
      id: v.id,
      targets: v.nodes.map((n) => n.target),
    })),
  };
  scans.push(summary);
  assert.deepEqual(summary.violations, [], label);
}
async function startRun() {
  await nav("Assessments").click();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("synthetic-analysis-findings-v1");
  await page
    .getByLabel("Assessment profile", { exact: true })
    .selectOption("synthetic-review-maturity-equal-v1");
  const accepted = page.waitForResponse(
    (r) =>
      r.url() === `${base}/local-demo/v1/runs` &&
      r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .click();
  const runId = (await (await accepted).json()).runId;
  await explorer.waitFor();
  return runId;
}
async function inspect() {
  const row = explorer.locator(".inspect-finding").first();
  const id = await row.getAttribute("id");
  await row.focus();
  await page.keyboard.press("Enter");
  await focusIs(".finding-detail-title h4");
  return id;
}
try {
  await page.goto(base);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
  await page.waitForFunction(
    () => !document.querySelector("button.primary")?.disabled,
  );
  await nav("Findings").click();
  await headingIs("Assessment workspace");
  assert.match(
    await page.locator(".navigation-notice").textContent(),
    /Findings is not available/,
  );
  await focusIs("#run-heading");
  await nav("Reports").click();
  await headingIs("Assessment workspace");
  await nav("Settings").click();
  await headingIs("Settings");
  await focusIs("#configuration-heading");
  assert.equal(
    await page
      .getByRole("button", { name: "Start synthetic run", exact: true })
      .isVisible(),
    true,
  );
  record(
    "UIW3-001 empty-run Findings/Reports fallback; Settings existing form focus; original landing title",
  );
  const runId = await startRun();
  const original = await (
    await page.request.get(`${base}/local-demo/v1/runs/${runId}/analysis`)
  ).json();
  await nav("Findings").click();
  await headingIs("Findings");
  await focusIs("#findings-heading");
  assert.equal(await page.locator(".configuration").isVisible(), false);
  assert.equal(await page.locator(".history").isVisible(), false);
  assert.equal(await page.locator(".draft-report").isVisible(), false);
  assert.equal(
    await explorer.locator(".analysis-finding").count(),
    original.findings.length,
  );
  await search.fill(original.findings[0].ruleId);
  const invoking = await inspect();
  const selectedId = invoking.substring(`inspect-finding-${runId}-`.length);
  const editor = page.locator(".review-finding").filter({
    has: page.locator(`[id="review-finding-${runId}-${selectedId}"]`),
  });
  await editor
    .getByLabel("Review action", { exact: true })
    .selectOption("EditPresentation");
  const title = editor.getByLabel("Presentation title", { exact: true });
  await title.fill("Unsaved independent UI review title");
  for (const view of ["Reports", "Settings", "Assessments", "Findings"]) {
    await nav(view).click();
    await headingIs(view === "Assessments" ? "Assessment workspace" : view);
    assert.equal(
      await title.inputValue(),
      "Unsaved independent UI review title",
    );
  }
  assert.equal(await search.inputValue(), original.findings[0].ruleId);
  assert.equal(
    await explorer
      .locator('.inspect-finding[aria-pressed="true"]')
      .getAttribute("id"),
    invoking,
  );
  await detail
    .getByRole("button", { name: "Recommendations", exact: true })
    .click();
  assert.equal(
    await detail
      .getByRole("heading", {
        name: "Original recommendation options",
        exact: true,
      })
      .isVisible(),
    true,
  );
  assert.match(
    await detail.textContent(),
    /Synthetic · Review-only · Unverified/,
  );
  await nav("Evidence").click();
  await headingIs("Evidence");
  await focusIs("#finding-evidence-heading");
  assert.equal(
    await detail
      .getByRole("heading", { name: "Evidence and provenance", exact: true })
      .isVisible(),
    true,
  );
  assert.equal(
    await detail
      .getByRole("button", { name: "Evidence", exact: true })
      .getAttribute("aria-pressed"),
    "true",
  );
  await scan("desktop dedicated Evidence");
  record(
    "UIW3-002 dedicated navigation hides irrelevant sections; mounted unsaved editor and filters/selection retained; Evidence restores Evidence disclosure from Recommendations",
  );
  await nav("Reports").click();
  await focusIs(".draft-report");
  assert.equal(await page.locator(".draft-report").isVisible(), true);
  assert.equal(await explorer.isVisible(), false);
  assert.match(
    await page.locator(".draft-report").textContent(),
    /Unpublished|unpublished/,
  );
  await page
    .getByRole("button", { name: "Technical draft", exact: true })
    .click();
  await scan("desktop dedicated Reports");
  await nav("Settings").click();
  await focusIs("#configuration-heading");
  assert.equal(await page.locator(".run-inputs").getAttribute("open"), "");
  assert.equal(await page.locator(".run-analysis").isVisible(), false);
  await scan("desktop dedicated Settings");
  const after = await (
    await page.request.get(`${base}/local-demo/v1/runs/${runId}/analysis`)
  ).json();
  for (const key of ["contentDigest", "reviewSnapshotDigest"])
    assert.equal(after[key], original[key]);
  assert.equal(
    after.reportDraft.snapshot.canonicalContentDigest,
    original.reportDraft.snapshot.canonicalContentDigest,
  );
  record(
    "UIW3-003 canonical Reports and immutable Settings display; all UI navigation retains scoring/review/report digests",
  );
  await nav("Findings").click();
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  await page.setViewportSize({ width: 320, height: 740 });
  assert.equal(await search.isVisible(), true);
  assert.equal(await detail.isVisible(), false);
  const mobileInvoking = await inspect();
  assert.equal(await search.isVisible(), false);
  assert.equal(await explorer.locator(".finding-results").isVisible(), false);
  await detail
    .getByRole("button", { name: "Back to findings", exact: true })
    .click();
  await focusIs(`[id="${mobileInvoking}"]`);
  assert.equal(await search.isVisible(), true);
  assert.equal(await search.inputValue(), original.findings[0].ruleId);
  await inspect();
  await page.keyboard.press("Escape");
  await focusIs(`[id="${mobileInvoking}"]`);
  await scan("320px mobile Findings list");
  record(
    "UIW3-004 320px list/detail visibility, Back and Escape focus, retained filter",
  );
  for (const width of [320, 390, 760, 1024, 1440]) {
    await page.setViewportSize({ width, height: 740 });
    for (const view of [
      "Findings",
      "Evidence",
      "Reports",
      "Settings",
      "Assessments",
    ]) {
      await nav(view).click();
      const dimensions = await page.evaluate(() => ({
        width: document.documentElement.clientWidth,
        scroll: document.documentElement.scrollWidth,
      }));
      assert.ok(
        dimensions.scroll <= dimensions.width + 1,
        `${view} overflow at ${width}: ${JSON.stringify(dimensions)}`,
      );
    }
  }
  record(
    "UIW3-005 all five views avoid document overflow at 320/390/760/1024/1440px",
  );
  await page.setViewportSize({ width: 1440, height: 1000 });
  const secondRun = await startRun();
  assert.notEqual(secondRun, runId);
  await headingIs("Assessment workspace");
  await nav("Findings").click();
  assert.equal(await search.inputValue(), "");
  assert.equal(
    await explorer.locator('.inspect-finding[aria-pressed="true"]').count(),
    0,
  );
  assert.deepEqual(faults, []);
  record(
    "UIW3-006 changing run returns to retained landing and clears prior finding filters/selection; no browser runtime errors",
  );
  await inspect();
  await nav("Evidence").click();
  await focusIs("#finding-evidence-heading");
  const secondFirst = (
    await (
      await page.request.get(`${base}/local-demo/v1/runs/${secondRun}/analysis`)
    ).json()
  ).findings[0];
  const secondEditor = page.locator(".review-finding").filter({
    has: page.locator(`[id="review-finding-${secondRun}-${secondFirst.id}"]`),
  });
  await secondEditor
    .getByLabel("Review action", { exact: true })
    .selectOption("Confirm");
  await secondEditor
    .getByLabel("Review reason (optional)", { exact: true })
    .fill("Fictional independent UI-W3 saved-feedback focus check");
  await secondEditor
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await page.waitForFunction(() =>
    document
      .querySelector(".finding-detail")
      ?.textContent?.includes("Confirmed"),
  );
  await focusIs("#findings-heading");
  const status = page.locator('.analysis-view > p[role="status"]');
  assert.equal(await status.isVisible(), true);
  assert.match(await status.textContent(), /Review event saved/);
  const reviewed = await (
    await page.request.get(`${base}/local-demo/v1/runs/${secondRun}/analysis`)
  ).json();
  assert.deepEqual(
    reviewed.findings[0].originalDigests,
    secondFirst.originalDigests,
  );
  record(
    "UIW3-007 actual fictional Confirm in Evidence preserves original digests, restores visible findings focus, and announces saved feedback",
  );
  await writeFile(
    `${output}/validation.json`,
    JSON.stringify(
      { base, runId, secondRun, groups, scans, faults, result: "PASS" },
      null,
      2,
    ),
  );
} catch (error) {
  await writeFile(
    `${output}/validation.json`,
    JSON.stringify(
      { base, groups, scans, faults, result: "FAIL", error: String(error) },
      null,
      2,
    ),
  );
  await page.screenshot({ path: `${output}/failure.png`, fullPage: true });
  throw error;
} finally {
  await browser.close();
}
