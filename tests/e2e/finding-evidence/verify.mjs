import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { chromium } from "../consultant-demo/node_modules/playwright/index.mjs";
import axe from "../consultant-demo/node_modules/axe-core/axe.js";
import { until } from "../draft-reports/browser-support.mjs";
import { base, launchHost, ownsHost, stopHost } from "./host-harness.mjs";

const output = process.env.IGA_UI_EVIDENCE ?? "/tmp/iga-uiw2-evidence";
await mkdir(output, { recursive: true });
await launchHost();
assert.ok(ownsHost(), "Only a task-owned disposable synthetic host qualifies");
const browser = await chromium.launch({
  executablePath: process.env.IGA_CHROMIUM,
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const faults = [];
page.on("pageerror", (error) => faults.push(error.message));
page.on("console", (message) => {
  if (/same key|Encountered two children/.test(message.text()))
    faults.push(message.text());
});
const groups = [];
const scans = [];
const result = (text) => {
  groups.push(text);
  console.log(text);
};
const explorer = page.locator(".findings-explorer");
const detail = page.locator(".finding-detail");
const search = page.getByLabel("Search findings", { exact: true });
const severity = page.getByLabel("Finding severity", { exact: true });
const category = page.getByLabel("Finding category", { exact: true });
const state = page.getByLabel("Finding state", { exact: true });
const confidence = page.getByLabel("Confidence band", { exact: true });
const clear = () =>
  page
    .getByRole("button", { name: "Clear finding filters", exact: true })
    .click();
async function inspect(index = 0) {
  const button = explorer.locator(".inspect-finding").nth(index);
  const id = await button.getAttribute("id");
  await button.focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(
    () =>
      document.activeElement ===
      document.querySelector(".finding-detail-title h4"),
  );
  return id;
}
async function focusIs(selector) {
  await page.waitForFunction(
    (selector) => document.activeElement === document.querySelector(selector),
    selector,
  );
}
async function scan(label) {
  await page.evaluate(axe.source);
  const scan = await page.evaluate(() =>
    window.axe.run(document, {
      runOnly: {
        type: "tag",
        values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
      },
    }),
  );
  const record = {
    label,
    version: scan.testEngine.version,
    violations: scan.violations.map((v) => ({
      id: v.id,
      targets: v.nodes.map((n) => n.target),
    })),
    incomplete: scan.incomplete.map((v) => v.id),
  };
  scans.push(record);
  assert.deepEqual(record.violations, [], label);
}
let runId;
let original;
try {
  await page.goto(base);
  await until(
    () =>
      page
        .getByRole("button", { name: "Start synthetic run", exact: true })
        .isEnabled(),
    Boolean,
    "Catalog ready",
  );
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
  runId = (await (await accepted).json()).runId;
  await explorer.waitFor();
  assert.equal(await explorer.locator(".analysis-finding").count(), 5);
  original = await (
    await page.request.get(`${base}/local-demo/v1/runs/${runId}/analysis`)
  ).json();
  assert.match(
    original.reportDraft.snapshot.canonicalContentDigest,
    /^[a-f0-9]{64}$/,
  );
  const counts = async () => explorer.locator(".analysis-finding").count();
  await severity.selectOption("High");
  assert.equal(await counts(), 1);
  await category.selectOption(
    original.findings.find((f) => f.severity === "High").category,
  );
  assert.equal(await counts(), 1);
  await state.selectOption("Proposed");
  assert.equal(await counts(), 1);
  await confidence.selectOption(
    original.findings.find((f) => f.severity === "High").confidenceBand,
  );
  assert.equal(await counts(), 1);
  await search.fill(
    original.findings.find((f) => f.severity === "High").ruleId,
  );
  assert.equal(await counts(), 1);
  await search.fill("nonexistent synthetic finding");
  assert.equal(await counts(), 0);
  assert.match(await explorer.textContent(), /No findings match/);
  await clear();
  assert.equal(await counts(), 5);
  await page
    .getByRole("button", { name: "Mandatory review pending", exact: true })
    .click();
  assert.equal(await counts(), 2);
  await clear();
  const unchanged = await (
    await page.request.get(`${base}/local-demo/v1/runs/${runId}/analysis`)
  ).json();
  assert.equal(unchanged.contentDigest, original.contentDigest);
  assert.equal(unchanged.reviewSnapshotDigest, original.reviewSnapshotDigest);
  assert.equal(
    unchanged.reportDraft.snapshot.canonicalContentDigest,
    original.reportDraft.snapshot.canonicalContentDigest,
  );
  result(
    "FE-001 combined search/severity/category/state/confidence, mandatory-review, no matches and clear; canonical digests unchanged",
  );

  const first = original.findings[0];
  let invokingId = await inspect();
  assert.ok(
    await detail.getByText(first.originalTitle, { exact: true }).count(),
  );
  for (const ref of first.evidenceReferences)
    assert.ok(await detail.getByText(ref, { exact: true }).count());
  assert.match(
    await detail.textContent(),
    /Protected raw evidence is not retrieved/,
  );
  assert.match(
    await detail.textContent(),
    /Synthetic · Review-only · Unverified/,
  );
  await page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name: "Evidence", exact: true })
    .click();
  await focusIs("#finding-evidence-heading");
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  assert.equal(
    await page.evaluate(() => document.activeElement.id),
    invokingId,
  );
  invokingId = await inspect();
  await page.keyboard.press("Escape");
  assert.equal(
    await page.evaluate(() => document.activeElement.id),
    invokingId,
  );
  await inspect();
  await severity.selectOption("Low");
  assert.match(await detail.textContent(), /outside the current filters/);
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  await focusIs("#findings-heading");
  await clear();
  await inspect();
  await page.locator("#findings-heading").scrollIntoViewIfNeeded();
  await page.screenshot({ path: `${output}/finding-evidence-desktop.png` });
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  result(
    "FE-002 adjacent original/current explanation and inert evidence/options; focus on open/close/Escape; selection retained outside filters with fallback focus",
  );

  await state.selectOption("Proposed");
  invokingId = await inspect();
  await detail
    .getByRole("button", { name: "Review this finding", exact: true })
    .click();
  assert.equal(
    await page.evaluate(() => document.activeElement.id),
    `review-finding-${runId}-${first.id}`,
  );
  const editor = page.locator(".review-finding").filter({
    has: page.locator(`[id="review-finding-${runId}-${first.id}"]`),
  });
  await editor
    .getByLabel("Review reason (optional)", { exact: true })
    .fill("Synthetic UI-W2 focus and snapshot check");
  await editor.getByRole("button", { name: "Confirm", exact: true }).click();
  await until(
    () => detail.textContent(),
    (text) => text?.includes("Confirmed"),
    "Selected finding retained after actual review refresh",
  );
  assert.equal(await state.inputValue(), "Proposed");
  assert.match(await detail.textContent(), /outside the current filters/);
  assert.ok(
    await detail.getByText(first.originalTitle, { exact: true }).count(),
  );
  assert.match(await detail.textContent(), /Unverified/);
  const afterReview = await (
    await page.request.get(`${base}/local-demo/v1/runs/${runId}/analysis`)
  ).json();
  assert.deepEqual(
    afterReview.findings[0].originalDigests,
    first.originalDigests,
  );
  await clear();
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  assert.equal(
    await page.evaluate(() => document.activeElement.id),
    invokingId,
  );
  // Draft text in an existing mounted editor survives filtering and panel operations.
  const high = original.findings.find((f) => f.severity === "High");
  const highEditor = page
    .locator(".review-finding")
    .filter({ has: page.locator(`[id="review-finding-${runId}-${high.id}"]`) });
  await highEditor
    .getByLabel("Review reason (optional)", { exact: true })
    .fill("Unsaved synthetic editor text");
  await severity.selectOption("Medium");
  await clear();
  await inspect();
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  assert.equal(
    await highEditor
      .getByLabel("Review reason (optional)", { exact: true })
      .inputValue(),
    "Unsaved synthetic editor text",
  );
  await state.selectOption("Proposed");
  await inspect();
  await highEditor
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await until(
    () => counts(),
    (count) => count === 0,
    "No Proposed findings remain",
  );
  assert.equal(await state.inputValue(), "Proposed");
  assert.equal(await state.locator('option[value="Proposed"]').count(), 1);
  assert.match(await detail.textContent(), /Confirmed/);
  await detail
    .getByRole("button", { name: "Close finding", exact: true })
    .click();
  await focusIs("#findings-heading");
  await clear();
  result(
    "FE-003 actual Confirm and coherent refresh preserve selected identity/filters/originals; retained zero-match state option; unsaved editor text survives filters",
  );

  // Saved hostile presentation remains plain text in the inspector and search results.
  const hostile =
    "<img src=x onerror=window.__uiInjected=1> <script>window.__uiInjected=2</script> https://example.invalid =SUM(1,1)";
  const savedEditor = page.locator(".review-finding").filter({
    has: page.locator(`[id="review-finding-${runId}-${first.id}"]`),
  });
  await savedEditor
    .getByLabel("Review action", { exact: true })
    .selectOption("EditPresentation");
  await savedEditor
    .getByLabel("Presentation title", { exact: true })
    .fill(hostile);
  await savedEditor
    .getByLabel("Business context", { exact: true })
    .fill(hostile);
  await savedEditor
    .getByRole("button", { name: "Save presentation", exact: true })
    .click();
  await until(
    () => explorer.locator(".inspect-finding").first().textContent(),
    (text) => text?.includes(hostile),
    "Saved presentation refresh",
  );
  await inspect();
  assert.match(await detail.textContent(), /Original title/);
  assert.ok(
    await detail.getByText(first.originalTitle, { exact: true }).count(),
  );
  assert.equal(await detail.locator("img,script,a,iframe").count(), 0);
  assert.equal(await page.evaluate(() => window.__uiInjected), undefined);
  await search.fill("SUM(1,1)");
  assert.equal(await counts(), 1);
  await clear();
  result(
    "FE-004 actually saved hostile presentation and context stay inert; immutable original title and provenance preserved",
  );

  // Same-run refresh with identical revision keeps presentation state; run switches reset it.
  await severity.selectOption("Critical");
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  const layouts = [];
  for (const [width, height] of [
    [1440, 1000],
    [1200, 350],
    [900, 400],
    [390, 844],
    [320, 800],
  ]) {
    await page.setViewportSize({ width, height });
    await explorer.locator(".inspect-finding").first().click();
    await focusIs(".finding-detail-title h4");
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await detail
      .getByRole("button", {
        name: width <= 1100 ? "Back to findings" : "Close finding",
        exact: true,
      })
      .click();
    layouts.push({ width, height, overflow: false });
  }
  await clear();
  await inspect();
  await scan("320px selected finding");
  await page.emulateMedia({ forcedColors: "active" });
  await scan("320px forced colors");
  await page.emulateMedia({ forcedColors: "none" });
  await page.screenshot({
    path: `${output}/finding-evidence-narrow.png`,
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await scan("desktop selected finding");
  await page.locator("#findings-heading").scrollIntoViewIfNeeded();
  await page.screenshot({
    path: `${output}/finding-evidence-hostile-desktop.png`,
  });

  await page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name: "Settings", exact: true })
    .click();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("baseline-complete");
  await page
    .getByLabel("Assessment profile", { exact: true })
    .selectOption("profile-standard");
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .click();
  await explorer.waitFor({ state: "detached" });
  await page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name: "Evidence", exact: true })
    .click();
  await focusIs("#evidence-heading");
  await page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name: "Assessments", exact: true })
    .click();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .nth(1)
    .click();
  await explorer.waitFor();
  assert.equal(await severity.inputValue(), "");
  assert.equal(await search.inputValue(), "");
  assert.equal(
    await detail
      .getByRole("button", { name: "Close finding", exact: true })
      .count(),
    0,
  );
  assert.deepEqual(faults, []);
  result(
    "FE-005 desktop/short390320, forced colors and axe; historical coverage-only Evidence fallback; changed run resets panel and filters",
  );
  const report = {
    completedAt: new Date().toISOString(),
    browser: await browser.version(),
    groups,
    scans,
    layouts,
    pageErrors: faults,
    scope:
      "Actual task-owned synthetic PostgreSQL-backed host, final built React assets; Windows/manual/large-scale acceptance not claimed.",
  };
  await writeFile(`${output}/execution.json`, JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
  if (process.env.IGA_UI_REVIEW_HOLD === "1") {
    console.log(
      "Review hold: owned host remains at http://127.0.0.1:5183 until SIGINT",
    );
    await new Promise((resolve) => process.once("SIGINT", resolve));
  }
} finally {
  await browser.close();
  await stopHost();
}
