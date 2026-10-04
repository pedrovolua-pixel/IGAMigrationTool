import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";

const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5183";
assert.equal(base, "http://127.0.0.1:5183");
const { chromium, request } = await import(
  process.env.IGA_PLAYWRIGHT_MODULE
    ? pathToFileURL(process.env.IGA_PLAYWRIGHT_MODULE).href
    : new URL(
        "../consultant-demo/node_modules/playwright/index.mjs",
        import.meta.url,
      )
);
const { default: axe } = await import(
  process.env.IGA_AXE_MODULE
    ? pathToFileURL(process.env.IGA_AXE_MODULE).href
    : new URL(
        "../consultant-demo/node_modules/axe-core/axe.js",
        import.meta.url,
      )
);
const schema = JSON.parse(
  await readFile(
    process.env.IGA_DEMO_SCHEMA ??
      new URL(
        "../../../contracts/local-demo/demo-v1.schema.json",
        import.meta.url,
      ),
    "utf8",
  ),
);
let assertions = 0;
const groups = [];
const accessibility = [];
function check(value, message) {
  assert.ok(value, message);
  assertions++;
}
function equal(actual, expected, message) {
  assert.deepEqual(actual, expected, message);
  assertions++;
}
function group(name) {
  groups.push(name);
  console.log(`PASS ${name}`);
}
async function until(read, predicate, name, milliseconds = 40000) {
  const end = Date.now() + milliseconds;
  while (Date.now() < end) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(name);
}
// This bounded schema traversal consumes the authoritative strict DTO schema. It
// checks the features used there; it is not a general JSON Schema implementation.
function valid(value, rule) {
  if (rule.$ref) return valid(value, schema.$defs[rule.$ref.split("/").at(-1)]);
  if (rule.anyOf) return rule.anyOf.some((child) => valid(value, child));
  if ("const" in rule && value !== rule.const) return false;
  if (rule.enum && !rule.enum.includes(value)) return false;
  if (rule.type === "null") return value === null;
  if (rule.type === "string")
    return (
      typeof value === "string" &&
      (!rule.minLength || value.length >= rule.minLength) &&
      (!rule.maxLength || value.length <= rule.maxLength) &&
      (!rule.pattern || new RegExp(rule.pattern).test(value))
    );
  if (rule.type === "integer")
    return (
      Number.isInteger(value) &&
      (rule.minimum === undefined || value >= rule.minimum)
    );
  if (rule.type === "boolean") return typeof value === "boolean";
  if (rule.type === "array")
    return (
      Array.isArray(value) && value.every((item) => valid(item, rule.items))
    );
  if (rule.type === "object")
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      (rule.required ?? []).every((key) => key in value) &&
      (rule.additionalProperties !== false ||
        Object.keys(value).every((key) => key in rule.properties)) &&
      Object.entries(value).every(
        ([key, item]) =>
          !rule.properties[key] || valid(item, rule.properties[key]),
      )
    );
  return true;
}
let host;
if (process.env.IGA_HOST_DLL && process.env.IGA_HOST_WORKING_DIRECTORY) {
  let occupied = false;
  try {
    await fetch(`${base}/local-demo/v1/catalog`);
    occupied = true;
  } catch {}
  assert.equal(
    occupied,
    false,
    "Owned host requires a free port; another host is never reused implicitly",
  );
  host = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [process.env.IGA_HOST_DLL, "--synthetic-local-demo"],
    {
      cwd: process.env.IGA_HOST_WORKING_DIRECTORY,
      env: { ...process.env },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  process.once("exit", () => {
    if (host.exitCode === null) host.kill("SIGTERM");
  });
  host.stdout.on("data", () => {});
  host.stderr.on("data", () => {});
  await until(
    async () => {
      if (host.exitCode !== null)
        throw new Error(`Owned synthetic host exited:${host.exitCode}`);
      try {
        return (await fetch(`${base}/local-demo/v1/catalog`)).status;
      } catch {
        return 0;
      }
    },
    (value) => value === 200,
    "owned synthetic host did not start",
  );
}
const browser = await chromium.launch({
  headless: true,
  ...(process.env.IGA_CHROMIUM
    ? { executablePath: process.env.IGA_CHROMIUM }
    : {}),
});
const context = await browser.newContext({
  viewport: { width: 1280, height: 900 },
});
const page = await context.newPage();
const errors = [];
page.on("pageerror", (error) => errors.push(error.message));
const api = await request.newContext({ baseURL: base });
let report;
try {
  const catalog = await (await api.get("/local-demo/v1/catalog")).json();
  const headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  async function start(
    baselineId,
    profileId = "synthetic-analysis-equal-v1",
    extra = {},
  ) {
    const response = await api.post("/local-demo/v1/runs", {
      headers,
      data: {
        scopeId: "demo-scope",
        baselineId,
        profileId,
        requestId: randomUUID(),
        ...extra,
      },
    });
    equal(response.status(), 201, "new fixed catalog run starts");
    return response.json();
  }
  async function analysis(runId) {
    const response = await api.get(`/local-demo/v1/runs/${runId}/analysis`);
    equal(response.status(), 200, "saved analysis projection readable");
    const data = await response.json();
    check(
      valid(data, schema.$defs.AnalysisDetail),
      "actual private analysis DTO matches authoritative strict shape/types",
    );
    return data;
  }
  const current = async (id) =>
    (await api.get(`/local-demo/v1/runs/${id}`)).json();
  const ready = async (id) =>
    until(
      () => analysis(id),
      (value) => value.status === "Ready",
      "saved analysis did not become Ready",
    );
  const findingsStart = await start("synthetic-analysis-findings-v1");
  const early = await analysis(findingsStart.runId);
  equal(early.status, "Unavailable", "partial planned/run state cannot score");
  check(
    early.provisional === null &&
      early.publishableCurrent === null &&
      early.contentDigest === null,
    "unavailable projection contains no fabricated scores",
  );
  const findings = await ready(findingsStart.runId);
  equal(
    findings.algorithmVersion,
    "pilot-health-v1",
    "exact algorithm version",
  );
  equal(
    [
      findings.provisional.display,
      findings.provisional.status,
      findings.provisional.eligibleUnits,
    ],
    ["44.2", "Red", 10],
    "independent all-findings provisional golden",
  );
  equal(
    [
      findings.publishableCurrent.display,
      findings.publishableCurrent.status,
      findings.publishableCurrent.eligibleUnits,
    ],
    ["78.3", "Yellow", 6],
    "independent publishable excludes four mandatory proposals",
  );
  check(
    typeof findings.provisional.raw === "string" &&
      Number(findings.provisional.raw) > 44.16 &&
      Number(findings.provisional.raw) < 44.17 &&
      findings.provisional.raw.length > 20,
    "raw decimal precision preserved as string",
  );
  equal(
    findings.quality,
    {
      plannedUnits: 10,
      executedUnits: 10,
      gapUnits: 0,
      notApplicableUnits: 0,
      proposedReviewUnits: 4,
      totalFindingUnits: 10,
    },
    "literal separate quality counts",
  );
  equal(findings.findings.length, 5, "five root-cause presentation groups");
  check(
    findings.findings.every(
      (item) =>
        item.objectIds.length === 2 &&
        item.originalDigests.length === 2 &&
        item.evidenceReferences.length === 2 &&
        item.facts.length === 2,
    ),
    "groups retain two actual per-object originals",
  );
  equal(
    findings.findings
      .filter((item) => item.reviewRequired)
      .map((item) => [item.severity, item.state])
      .sort(),
    [
      ["Critical", "Proposed"],
      ["High", "Proposed"],
    ],
    "mandatory severe groups remain Proposed",
  );
  check(
    findings.findings.every(
      (item) =>
        item.ruleVersion === "synthetic-rule-v1" &&
        item.baselineId === "synthetic-analysis-findings-v1" &&
        item.method === "Deterministic" &&
        item.confidencePercent === "100" &&
        item.confidenceBand &&
        item.rootCause &&
        item.recommendations.length === 2 &&
        item.validationGuidance &&
        item.sources.length === 1,
    ),
    "actual typed provenance/generated originals remain readable and distinct",
  );
  check(
    findings.warnings.some((item) =>
      item.includes("4 Critical/High finding occurrences"),
    ),
    "four mandatory-review warning remains prominent",
  );
  const security = findings.categories.find((item) => item.id === "SECURITY");
  const operations = findings.categories.find(
    (item) => item.id === "OPERATIONS",
  );
  equal(
    [
      Number(security.provisional.raw),
      Number(security.provisionalWeight),
      security.publishableCurrent.status,
      security.publishableWeight,
    ],
    [10, 0.5, "Unavailable", null],
    "category omitted independently in publishable weights",
  );
  equal(
    operations.publishableWeight,
    "1",
    "single assessed category renormalizes publishable weight",
  );
  const saved = await current(findingsStart.runId);
  equal(
    findings.runRevision,
    saved.revision,
    "analysis binds exact saved terminal revision",
  );
  check(
    saved.lockedInputs.some(
      (item) =>
        item.name === "Frozen analysis contents" &&
        item.sha256 === findings.fixtureDigest,
    ),
    "analysis content lock shown in saved full inputs",
  );
  group(
    "AS-API-001 real stored findings/provisional/publishable/quality/provenance precision goldens",
  );

  const comparisonStart = await start(
    "synthetic-analysis-findings-v1",
    "synthetic-analysis-operations-v1",
  );
  const comparison = await ready(comparisonStart.runId);
  equal(
    Number(comparison.provisional.raw),
    61.25,
    "literal operations-weighted provisional61.25",
  );
  equal(
    comparison.publishableCurrent.display,
    "78.3",
    "publishable only assessed operations remains78.3",
  );
  equal(
    comparison.categories.map((item) => [item.id, item.provisionalWeight]),
    [
      ["OPERATIONS", "0.75"],
      ["SECURITY", "0.25"],
    ],
    "explicit fixed weights differ from equal profile",
  );
  check(
    comparison.fixtureDigest !== findings.fixtureDigest &&
      comparison.contentDigest !== findings.contentDigest,
    "new profile produces separate frozen comparison",
  );
  equal(
    (await analysis(findingsStart.runId)).contentDigest,
    findings.contentDigest,
    "comparison does not rewrite original saved analysis",
  );
  const mixedStart = await start("synthetic-analysis-mixed-v1");
  const mixed = await ready(mixedStart.runId);
  equal(
    [mixed.provisional.display, mixed.publishableCurrent.display],
    ["66.7", "89.2"],
    "independent mixed profile goldens",
  );
  equal(
    mixed.quality,
    {
      plannedUnits: 10,
      executedUnits: 7,
      gapUnits: 3,
      notApplicableUnits: 0,
      proposedReviewUnits: 2,
      totalFindingUnits: 5,
    },
    "mixed quality independent of health",
  );
  const gapsStart = await start("synthetic-analysis-gaps-v1");
  const gaps = await ready(gapsStart.runId);
  equal(
    [gaps.provisional, gaps.publishableCurrent],
    [
      { raw: null, display: null, status: "Unavailable", eligibleUnits: 0 },
      { raw: null, display: null, status: "Unavailable", eligibleUnits: 0 },
    ],
    "all gaps cannot become100",
  );
  equal(
    gaps.quality,
    {
      plannedUnits: 10,
      executedUnits: 0,
      gapUnits: 10,
      notApplicableUnits: 0,
      proposedReviewUnits: 0,
      totalFindingUnits: 0,
    },
    "allgap quality remains10gaps no healthy controls",
  );
  const healthyStart = await start("synthetic-analysis-healthy-v1");
  const healthy = await ready(healthyStart.runId);
  equal(
    [
      healthy.provisional.display,
      healthy.publishableCurrent.display,
      healthy.findings.length,
    ],
    ["100.0", "100.0", 0],
    "actual explicit zero marker facts yield100 and no findings",
  );
  const oldStart = await start("baseline-complete", "profile-standard");
  await until(
    () => current(oldStart.runId),
    (value) => value.state === "Scoring",
    "coverage-only legacy run did not reach saved coverage",
  );
  const old = await analysis(oldStart.runId);
  equal(
    old.status,
    "Unavailable",
    "historical coverage-only behavior preserved",
  );
  equal(
    old.reasonCode,
    "coverage_only_fixture",
    "old fixture never acquires new analysis",
  );
  const cancelStart = await start("synthetic-analysis-mixed-v1");
  const cancelCurrent = await current(cancelStart.runId);
  const cancelResponse = await api.post(
    `/local-demo/v1/runs/${cancelStart.runId}/cancel`,
    {
      headers,
      data: {
        expectedRevision: cancelCurrent.revision,
        requestId: randomUUID(),
      },
    },
  );
  equal(
    cancelResponse.status(),
    200,
    "synthetic cancel accepted before remaining work",
  );
  const cancelled = await until(
    () => current(cancelStart.runId),
    (value) => value.state === "Cancelled",
    "analysis run cancel did not settle",
  );
  const deniedCancelled = await analysis(cancelStart.runId);
  equal(
    deniedCancelled.status,
    "Unavailable",
    "cancelled run cannot acquire score",
  );
  check(
    cancelled.progress.remainingUnits > 0,
    "cancelled run missing work not falsely complete",
  );
  group(
    "AS-API-002 comparisons/mixed/gaps/explicit healthy/historical/cancelled",
  );

  equal(
    (await api.get(`/local-demo/v1/runs/${randomUUID()}/analysis`)).status(),
    404,
    "unknown analysis resource denied",
  );
  const hostileBody = {
    scopeId: "demo-scope",
    baselineId: "synthetic-analysis-findings-v1",
    profileId: "synthetic-analysis-equal-v1",
    requestId: randomUUID(),
  };
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        headers,
        data: { ...hostileBody, weights: { SECURITY: 0 } },
      })
    ).status(),
    400,
    "arbitrary weights not admitted",
  );
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        headers,
        data: { ...hostileBody, analysisFixtureDigest: "0".repeat(64) },
      })
    ).status(),
    400,
    "client cannot provide frozen analysis contents",
  );
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        headers,
        data: { ...hostileBody, scopeId: "wrong-scope" },
      })
    ).status(),
    400,
    "wrong opaque scope denied",
  );
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        headers,
        data: { ...hostileBody, profileId: "profile-standard" },
      })
    ).status(),
    400,
    "incompatible analysis/coverage-only profile denied",
  );
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        headers: { ...headers, Origin: "https://attacker.invalid" },
        data: hostileBody,
      })
    ).status(),
    403,
    "foreign origin denied",
  );
  equal(
    (
      await api.get(`/local-demo/v1/runs/${findingsStart.runId}/analysis`, {
        headers: { Host: "attacker.invalid:5183" },
      })
    ).status(),
    403,
    "hostile Host denied for analysis route",
  );
  group(
    "AS-API-003 hostile scope/weights/frozen lock/profile/origin/Host/unknown-resource",
  );

  await page.goto(base);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("synthetic-analysis-findings-v1");
  await page
    .getByLabel("Assessment profile", { exact: true })
    .selectOption("synthetic-analysis-equal-v1");
  const startedResponse = page.waitForResponse(
    (response) =>
      response.url() === `${base}/local-demo/v1/runs` &&
      response.request().method() === "POST",
  );
  await page.getByLabel("Assessment profile", { exact: true }).focus();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  const startedUI = await (await startedResponse).json();
  await page
    .getByRole("heading", {
      name: "Findings and reproducible health calculations",
      exact: true,
    })
    .waitFor();
  equal(
    await page
      .locator(".analysis-score")
      .first()
      .locator("strong")
      .textContent(),
    "44.2",
    "keyboard-started UI provisional44.2",
  );
  equal(
    await page
      .locator(".analysis-score")
      .nth(1)
      .locator("strong")
      .textContent(),
    "78.3",
    "UI publishable-current78.3 distinct label",
  );
  check(
    (await page
      .getByText(/publishable-current calculation is not a published report/)
      .count()) === 1,
    "UI cannot claim published report",
  );
  equal(
    await page.locator(".analysis-finding").count(),
    5,
    "UI grouped findings retain five root causes",
  );
  equal(
    await page
      .locator(".analysis-finding > p > strong")
      .evaluateAll((elements) =>
        elements.slice(0, 2).map((element) => element.textContent),
      ),
    ["Critical", "High"],
    "mandatory severe findings remain first and prominent",
  );
  const disclosure = page
    .getByText("Read generated original and provenance", { exact: true })
    .first();
  await disclosure.focus();
  await page.keyboard.press("Enter");
  check(
    await disclosure.evaluate((element) => element.parentElement.open),
    "keyboard opens generated original",
  );
  check(
    (await page
      .getByText("Observed facts", { exact: true })
      .first()
      .isVisible()) &&
      (await page
        .getByText("Inference", { exact: true })
        .first()
        .isVisible()) &&
      (await page
        .getByText("Assumptions", { exact: true })
        .first()
        .isVisible()),
    "facts/inference/assumptions separately visible",
  );
  const beforeReload = await analysis(startedUI.runId);
  await page.reload();
  await page.getByText(startedUI.runId, { exact: true }).waitFor();
  await page
    .getByRole("heading", {
      name: "Findings and reproducible health calculations",
      exact: true,
    })
    .waitFor();
  equal(
    (await analysis(startedUI.runId)).contentDigest,
    beforeReload.contentDigest,
    "browser reload preserves same canonical score digest",
  );
  await page
    .getByRole("button", { name: "Refresh history", exact: true })
    .click();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .focus();
  await page.keyboard.press("Enter");
  await page
    .getByRole("heading", {
      name: "Findings and reproducible health calculations",
      exact: true,
    })
    .waitFor();
  group(
    "AS-UI-001 actual keyboard start/grouped original/reload/history immutable score",
  );

  const retryRoute = `**/local-demo/v1/runs/${startedUI.runId}/analysis`;
  let failures = 1;
  await page.route(retryRoute, async (route) => {
    if (failures-- > 0)
      await route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({
          schemaVersion: 1,
          code: "Unavailable",
          message: "Synthetic transient unavailable.",
          correlationId: null,
          currentRevision: null,
        }),
      });
    else await route.continue();
  });
  await page.reload();
  const retry = page.getByRole("button", {
    name: "Retry analysis",
    exact: true,
  });
  await retry.waitFor();
  await retry.focus();
  await page.keyboard.press("Enter");
  await page
    .getByRole("heading", {
      name: "Findings and reproducible health calculations",
      exact: true,
    })
    .waitFor();
  await until(
    () => page.evaluate(() => document.activeElement?.id),
    (value) => value === "analysis-heading",
    "successful same-revision retry did not focus analysis heading",
  );
  equal(
    (await analysis(startedUI.runId)).runRevision,
    beforeReload.runRevision,
    "read-only retry at unchanged terminal revision succeeds",
  );
  await page.unroute(retryRoute);
  await page.route(retryRoute, async (route) => {
    const actual = await route.fetch();
    const data = await actual.json();
    await route.fulfill({
      response: actual,
      json: { ...data, runRevision: data.runRevision + 1 },
    });
  });
  await page.reload();
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .waitFor();
  equal(
    await page.locator(".analysis-score").count(),
    0,
    "mismatched response revision is not displayed as verified score",
  );
  await page.unroute(retryRoute);
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .click();
  await page
    .getByRole("heading", {
      name: "Findings and reproducible health calculations",
      exact: true,
    })
    .waitFor();
  group(
    "AS-UI-002 actual transient failure/keyboard read-only same-revision retry/stale projection denial",
  );

  async function scan(label) {
    await page.evaluate(axe.source);
    const result = await page.evaluate(async () =>
      window.axe.run(document, {
        runOnly: {
          type: "tag",
          values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
        },
      }),
    );
    accessibility.push({
      label,
      version: result.testEngine.version,
      passes: result.passes.length,
      violations: result.violations.map((item) => ({
        id: item.id,
        targets: item.nodes.map((node) => node.target),
      })),
      incomplete: result.incomplete.map((item) => item.id),
    });
    equal(
      result.violations.map((item) => ({
        id: item.id,
        targets: item.nodes.map((node) => node.target),
      })),
      [],
      `axe ${label} confirmed violations`,
    );
  }
  const original = page
    .getByText("Read generated original and provenance", { exact: true })
    .first();
  await original.click();
  await scan("desktop findings/original");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./desktop-analysis.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./mobile-analysis.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 800 });
  check(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
    "320px expanded-provenance reflow no page overflow",
  );
  await scan("320px findings/original");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./reflow-analysis.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  check(
    await page
      .getByRole("heading", { name: "Provisional health", exact: true })
      .isVisible(),
    "meaningful health label remains visible in forced colors",
  );
  equal(errors, [], "no uncaught application browser exceptions");
  group(
    "AS-UI-003 actual findings provenance/desktop390320/axe/forced-colors engineering smoke",
  );
  report = {
    status: "PASS",
    assertions,
    groups,
    accessibility,
    browser: browser.version(),
    node: process.version,
    limitations: [
      "Synthetic internal scoring only; no actual rule promotion, review/risk grant, publication, maturity or AI enabled",
      "Full supported Windows browser/NVDA/Narrator acceptance NOT VERIFIED",
      ...accessibility
        .filter((item) => item.incomplete.length)
        .map(
          (item) =>
            `Axe ${item.label} incomplete:${item.incomplete.join(",")}; those rules are NOT VERIFIED`,
        ),
    ],
  };
} catch (error) {
  report = {
    status: "FAIL",
    assertions,
    groups,
    accessibility,
    error: error.message,
  };
  throw error;
} finally {
  await writeFile(
    new URL("./execution.json", import.meta.url),
    `${JSON.stringify(report, null, 2)}\n`,
  );
  await api.dispose();
  await context.close();
  await browser.close();
  if (host && host.exitCode === null) {
    const stopped = once(host, "exit");
    host.kill("SIGTERM");
    let timeout;
    const forced = new Promise((resolve) => {
      timeout = setTimeout(() => {
        host.kill("SIGKILL");
        resolve();
      }, 10000);
    });
    await Promise.race([stopped, forced]);
    clearTimeout(timeout);
    if (host.exitCode === null) await stopped;
  }
  console.log(JSON.stringify(report));
}
