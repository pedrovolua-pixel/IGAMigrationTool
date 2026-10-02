import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { spawn } from "node:child_process";
import { once } from "node:events";

const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5183";
const parsed = new URL(base);
assert.equal(parsed.hostname, "127.0.0.1");
assert.equal(parsed.protocol, "http:");
assert.equal(parsed.port, "5183");
const { chromium, request } = process.env.IGA_PLAYWRIGHT_MODULE
  ? await import(pathToFileURL(process.env.IGA_PLAYWRIGHT_MODULE).href)
  : await import("playwright");
const { default: axe } = await import(
  new URL("./node_modules/axe-core/axe.js", import.meta.url)
);
const accessibility = [];
async function scanAccessibility(label) {
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
      impact: item.impact,
      nodes: item.nodes.map((node) => node.target),
    })),
    incomplete: result.incomplete.map((item) => item.id),
  });
  equal(
    result.violations.map((item) => ({
      id: item.id,
      impact: item.impact,
      targets: item.nodes.map((node) => node.target),
    })),
    [],
    `axe ${label} has no confirmed WCAG violations`,
  );
}
let checks = 0;
const groups = [];
function recordGroup(message) {
  groups.push(message);
  console.log(message);
}
function check(condition, message) {
  assert.ok(condition, message);
  checks++;
}
function equal(actual, expected, message) {
  assert.deepEqual(actual, expected, message);
  checks++;
}
async function waitFor(read, predicate, message, timeout = 30000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(message);
}
let ownedHost;
let hostLog = "";
async function launchHost(paused = false) {
  if (!process.env.IGA_HOST_DLL || !process.env.IGA_HOST_WORKING_DIRECTORY)
    return;
  ownedHost = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [
      process.env.IGA_HOST_DLL,
      "--synthetic-local-demo",
      ...(paused ? ["--pause-synthetic-worker"] : []),
    ],
    {
      cwd: process.env.IGA_HOST_WORKING_DIRECTORY,
      env: { ...process.env },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  ownedHost.stdout.on("data", (value) => {
    hostLog += value;
  });
  ownedHost.stderr.on("data", (value) => {
    hostLog += value;
  });
  await waitFor(
    async () => {
      if (ownedHost.exitCode !== null)
        throw new Error(
          `Owned demo host exited with code ${ownedHost.exitCode}`,
        );
      try {
        return (await fetch(`${base}/local-demo/v1/catalog`)).status;
      } catch {
        return 0;
      }
    },
    (value) => value === 200,
    "Owned demo host did not become ready",
  );
}
async function stopHost(signal = "SIGTERM") {
  if (!ownedHost || ownedHost.exitCode !== null) return;
  const exited = once(ownedHost, "exit");
  ownedHost.kill(signal);
  await Promise.race([
    exited,
    new Promise((_, reject) =>
      setTimeout(() => reject(new Error("Owned host did not stop")), 10000),
    ),
  ]);
  ownedHost = null;
}
await launchHost();
const browser = await chromium.launch({
  ...(process.env.IGA_CHROMIUM
    ? { executablePath: process.env.IGA_CHROMIUM }
    : {}),
  headless: true,
});
const context = await browser.newContext({
  viewport: { width: 1280, height: 900 },
});
const page = await context.newPage();
const api = await request.newContext({ baseURL: base });
const faults = [];
page.on("pageerror", (error) => faults.push(error.message));
const read = async (id) => {
  const response = await api.get(`/local-demo/v1/runs/${id}`);
  equal(response.status(), 200, "saved run readable");
  return response.json();
};
let report;
try {
  const catalogResponse = await api.get("/local-demo/v1/catalog");
  equal(catalogResponse.status(), 200, "catalog available");
  const catalog = await catalogResponse.json();
  check(
    catalog.schemaVersion === 1 && catalog.demoOnly === true,
    "versioned explicitly synthetic response",
  );
  const headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const startBody = {
    scopeId: "demo-scope",
    baselineId: "baseline-gaps",
    profileId: "profile-standard",
    requestId: randomUUID(),
  };
  async function deny(label, options, expected = 403) {
    const response = await api.post("/local-demo/v1/runs", {
      data: startBody,
      headers,
      ...options,
    });
    equal(response.status(), expected, label);
  }
  equal(
    (
      await api.get("/local-demo/v1/catalog", {
        headers: { Host: "attacker.invalid:5183" },
      })
    ).status(),
    403,
    "unknown Host refused",
  );
  equal(
    (
      await api.get("/local-demo/v1/catalog", {
        headers: {
          Host: "attacker.invalid:5183",
          "X-Forwarded-Host": "127.0.0.1:5183",
          "X-Forwarded-For": "127.0.0.1",
        },
      })
    ).status(),
    403,
    "proxy headers cannot authorize hostile Host",
  );
  await deny("foreign origin refused", {
    headers: { ...headers, Origin: "https://attacker.invalid" },
  });
  await deny("missing origin refused", {
    headers: { "X-CSRF-TOKEN": catalog.csrfToken },
  });
  await deny("missing antiforgery token refused", {
    headers: { Origin: base },
  });
  await deny(
    "unknown properties refused",
    { data: { ...startBody, arbitrarySql: "synthetic-rejected-field" } },
    400,
  );
  await deny(
    "wrong scope/catalog selection refused",
    { data: { ...startBody, scopeId: "synthetic-wrong-scope" } },
    400,
  );
  await deny(
    "unknown baseline refused",
    { data: { ...startBody, baselineId: "synthetic-unknown" } },
    400,
  );
  await deny(
    "wrong JSON value kind refused",
    { data: { ...startBody, profileId: 7 } },
    400,
  );
  await deny(
    "oversized request refused",
    { data: { ...startBody, baselineId: "x".repeat(5000) } },
    400,
  );
  check(
    catalogResponse.headers()["cache-control"].includes("no-store"),
    "API cannot cache saved facts",
  );
  equal(
    catalogResponse.headers()["x-content-type-options"],
    "nosniff",
    "nosniff response",
  );
  check(
    catalogResponse
      .headers()
      ["content-security-policy"].includes("frame-ancestors 'none'"),
    "no framing policy",
  );
  check(
    !catalogResponse.headers()["access-control-allow-origin"],
    "no cross-origin authorization",
  );
  const startedResponse = await api.post("/local-demo/v1/runs", {
    data: startBody,
    headers,
  });
  equal(startedResponse.status(), 201, "new start created");
  const first = await startedResponse.json();
  const retry = await api.post("/local-demo/v1/runs", {
    data: startBody,
    headers,
  });
  equal(retry.status(), 200, "same operation id retry accepted");
  equal(
    (await retry.json()).runId,
    first.runId,
    "duplicate start has original identity",
  );
  equal(
    (
      await api.post("/local-demo/v1/runs", {
        data: { ...startBody, profileId: "profile-comparison" },
        headers,
      })
    ).status(),
    409,
    "same operation changed input refused",
  );
  const complete = await waitFor(
    () => read(first.runId),
    (value) => value.state === "Scoring",
    "coverage did not advance to paused scoring",
  );
  equal(
    complete.progress,
    { plannedUnits: 4, terminalUnits: 4, remainingUnits: 0, allTerminal: true },
    "independent gap-fixture progress",
  );
  equal(
    complete.coverageCompletionKind,
    "CompleteWithGaps",
    "gaps cannot become healthy completion",
  );
  equal(
    complete.executableCoverage,
    { numerator: 2, denominator: 4, hasApplicableUnits: true },
    "independent two-of-four measure",
  );
  equal(
    complete.limitations
      .map((item) => [
        item.state,
        item.reasonCode,
        item.responsibleStage,
        item.count,
      ])
      .sort(),
    [
      ["NotAssessed", "synthetic-history-not-assessed", "synthetic-planner", 1],
      [
        "Unsupported",
        "synthetic-semantic-analysis-unavailable",
        "synthetic-planner",
        1,
      ],
    ].sort(),
    "literal gap provenance",
  );
  equal(
    complete.stateCounts
      .filter((item) => item.count > 0)
      .map((item) => [item.state, item.count])
      .sort(),
    [
      ["Pass", 2],
      ["NotAssessed", 1],
      ["Unsupported", 1],
    ].sort(),
    "exact terminal state counts",
  );
  equal(
    complete.lockedInputs,
    first.lockedInputs,
    "complete freeze persists during processing",
  );
  check(complete.warnings.length === 1, "permission warning remains explicit");
  equal(
    (
      await api.post(`/local-demo/v1/runs/${first.runId}/cancel`, {
        data: { expectedRevision: 0, requestId: randomUUID() },
        headers,
      })
    ).status(),
    409,
    "stale mutation revision refused",
  );
  recordGroup(
    "DEMO-001 strict transport and independent durable coverage golden",
  );

  await page.goto(base);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("baseline-gaps");
  await page.getByLabel("Assessment profile", { exact: true }).focus();
  await page.keyboard.press("Tab");
  equal(
    await page.evaluate(() => document.activeElement?.textContent),
    "Start synthetic run",
    "keyboard reaches start control",
  );
  const newRunResponse = page.waitForResponse(
    (response) =>
      response.url() === `${base}/local-demo/v1/runs` &&
      response.request().method() === "POST",
  );
  await page.keyboard.press("Enter");
  const uiStart = await (await newRunResponse).json();
  await waitFor(
    () => page.evaluate(() => document.activeElement?.className),
    (value) => value === "panel run-detail",
    "start did not focus run detail",
  );
  check(
    (await page
      .getByRole("progressbar", { name: "Terminal coverage results" })
      .count()) === 1,
    "named native progress control",
  );
  const uiComplete = await waitFor(
    () => read(uiStart.runId),
    (value) => value.state === "Scoring",
    "UI run did not finish coverage",
  );
  await page
    .getByText("Coverage ready. Scoring is pending.", { exact: true })
    .waitFor();
  await page
    .getByRole("table", {
      name: "Grouped coverage limitations for selected run",
    })
    .waitFor();
  check(
    (await page
      .getByRole("columnheader", { name: "Reason", exact: true })
      .count()) === 1,
    "limitations table has semantic headers",
  );
  check(
    (await page
      .getByRole("status")
      .filter({ hasText: "Run state:" })
      .getAttribute("aria-live")) === "polite",
    "status announcements are noninterrupting",
  );
  await scanAccessibility("desktop gap coverage");
  const details = page.getByText("Locked input versions and digests", {
    exact: true,
  });
  await details.focus();
  await page.keyboard.press("Enter");
  check(
    await details.evaluate((element) => element.parentElement.open),
    "keyboard expands full input freeze",
  );
  await page.getByLabel("Evidence baseline", { exact: true }).focus();
  await new Promise((resolve) => setTimeout(resolve, 1500));
  equal(
    await page.evaluate(() => document.activeElement?.id),
    "baseline",
    "polling preserves keyboard focus",
  );
  await page.reload();
  await page.getByText(uiStart.runId, { exact: true }).waitFor();
  equal(
    await page.evaluate(() =>
      sessionStorage.getItem("iga.synthetic.selected-run"),
    ),
    uiStart.runId,
    "reload retains only opaque selected identity",
  );
  const keys = await page.evaluate(() => Object.keys(sessionStorage));
  equal(
    keys,
    ["iga.synthetic.selected-run"],
    "browser stores no fixture payload",
  );
  equal(
    (await read(uiStart.runId)).lockedInputs,
    uiComplete.lockedInputs,
    "reload retains exact full lock references",
  );
  await page
    .getByRole("button", { name: "Refresh history", exact: true })
    .click();
  const firstHistory = page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first();
  await firstHistory.focus();
  await page.keyboard.press("Enter");
  await waitFor(
    () => page.evaluate(() => document.activeElement?.className),
    (value) => value === "panel run-detail",
    "history open did not focus detail",
  );
  recordGroup(
    "DEMO-002 actual keyboard start/progress/locks/reload/history/focus",
  );

  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("baseline-recovery");
  const recoveryResponse = page.waitForResponse(
    (response) =>
      response.url() === `${base}/local-demo/v1/runs` &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  const recovery = await (await recoveryResponse).json();
  const inProgress = await waitFor(
    () => read(recovery.runId),
    (value) =>
      value.progress.terminalUnits > 0 && value.progress.terminalUnits < 40,
    "no observable recovery work",
  );
  let cancelAccepted = false;
  for (let attempt = 0; attempt < 5 && !cancelAccepted; attempt++) {
    await page.getByText(recovery.runId, { exact: true }).waitFor();
    const cancelButton = page.getByRole("button", {
      name: "Cancel run",
      exact: true,
    });
    await waitFor(
      () => cancelButton.isEnabled(),
      (value) => value,
      "cancel control did not become enabled",
    );
    const cancelResponse = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/runs/${recovery.runId}/cancel`) &&
        response.request().method() === "POST",
    );
    await cancelButton.focus();
    await page.keyboard.press("Enter");
    const response = await cancelResponse;
    if (response.status() === 409) {
      await page.getByRole("alert").waitFor();
      check(
        await page
          .getByRole("button", { name: "Refresh connection", exact: true })
          .isVisible(),
        "stale live revision surfaces recovery action",
      );
      await page
        .getByRole("button", { name: "Refresh connection", exact: true })
        .click();
      await new Promise((resolve) => setTimeout(resolve, 100));
    } else {
      equal(response.status(), 200, "keyboard cancellation accepted");
      cancelAccepted = true;
    }
  }
  check(cancelAccepted, "cancel can be retried after stale revision");
  const cancelled = await waitFor(
    () => read(recovery.runId),
    (value) => value.state === "Cancelled",
    "cancel did not settle",
  );
  check(
    cancelled.progress.terminalUnits >= inProgress.progress.terminalUnits &&
      cancelled.progress.terminalUnits < 40,
    "cancel preserves saved work and stops new work",
  );
  check(
    cancelled.progress.remainingUnits > 0 &&
      cancelled.coverageCompletionKind === null,
    "cancel cannot fabricate complete assessment",
  );
  equal(
    cancelled.lockedInputs,
    recovery.lockedInputs,
    "cancel retains frozen inputs",
  );
  await page.getByText("Run cancelled.", { exact: true }).waitFor();
  check(
    await page
      .getByRole("button", { name: "Cancel run", exact: true })
      .isDisabled(),
    "server cancellation gates UI control",
  );
  check(
    await page
      .getByRole("button", { name: "Resume expired work", exact: true })
      .isDisabled(),
    "cancelled work cannot resume",
  );
  await page.reload();
  await page.getByText(recovery.runId, { exact: true }).waitFor();
  equal(
    (await read(recovery.runId)).progress,
    cancelled.progress,
    "reload never resurrects cancelled work",
  );
  recordGroup(
    "DEMO-003 keyboard cancellation preserves incomplete durable results",
  );

  await page.screenshot({
    path: new URL("./desktop-synthetic.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: new URL("./mobile-synthetic.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 800 });
  await page
    .getByText("Locked input versions and digests", { exact: true })
    .click();
  check(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
    "320px reflow has no page horizontal overflow",
  );
  const undersized = await page
    .locator("button:not([disabled]),select:not([disabled])")
    .evaluateAll(
      (elements) =>
        elements.filter(
          (element) => element.getBoundingClientRect().height < 24,
        ).length,
    );
  equal(undersized, 0, "interactive targets meet24px engineering minimum");
  const unlabeled = await page
    .locator("select")
    .evaluateAll(
      (elements) =>
        elements.filter((element) => element.labels.length === 0).length,
    );
  equal(unlabeled, 0, "all native selection controls have associated labels");
  const unnamedTable = await page
    .locator("table")
    .evaluateAll(
      (elements) => elements.filter((element) => !element.caption).length,
    );
  equal(unnamedTable, 0, "data tables retain captions");
  await scanAccessibility("320px cancellation with expanded locks");
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  check(
    await page
      .getByRole("button", { name: "Refresh history", exact: true })
      .isVisible(),
    "controls remain exposed in forced colors/reduced motion",
  );
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./reflow-synthetic.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.emulateMedia({
    forcedColors: "none",
    reducedMotion: "no-preference",
  });
  await page.setViewportSize({ width: 1280, height: 900 });
  recordGroup(
    "DEMO-004 semantic/target/reflow/forced-colors engineering smoke",
  );

  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("baseline-not-applicable");
  const unavailableResponse = page.waitForResponse(
    (response) =>
      response.url() === `${base}/local-demo/v1/runs` &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .click();
  const unavailable = await (await unavailableResponse).json();
  const zero = await waitFor(
    () => read(unavailable.runId),
    (value) => value.state === "Scoring",
    "zero-applicable fixture did not finish",
  );
  equal(
    zero.executableCoverage,
    { numerator: 0, denominator: 0, hasApplicableUnits: false },
    "zero applicable units unavailable",
  );
  await page
    .getByText("Coverage ready. Scoring is pending.", { exact: true })
    .waitFor();
  equal(
    await page.locator(".coverage-measure").textContent(),
    "Unavailable",
    "zero denominator never displayed100percent",
  );
  if (process.env.IGA_HOST_DLL && process.env.IGA_HOST_WORKING_DIRECTORY) {
    await page
      .getByLabel("Evidence baseline", { exact: true })
      .selectOption("baseline-recovery");
    const restartResponse = page.waitForResponse(
      (response) =>
        response.url() === `${base}/local-demo/v1/runs` &&
        response.request().method() === "POST",
    );
    await page
      .getByRole("button", { name: "Start synthetic run", exact: true })
      .focus();
    await page.keyboard.press("Enter");
    const restartStart = await (await restartResponse).json();
    const saved = await waitFor(
      () => read(restartStart.runId),
      (value) =>
        value.progress.terminalUnits > 0 && value.progress.terminalUnits < 40,
      "restart preset has no observable committed work",
    );
    await stopHost("SIGKILL");
    await launchHost(true);
    await page.reload();
    await page.getByText(restartStart.runId, { exact: true }).waitFor();
    const paused = await read(restartStart.runId);
    check(
      paused.progress.terminalUnits >= saved.progress.terminalUnits &&
        paused.progress.terminalUnits < 40,
      "host crash preserves completed work without completing missing units",
    );
    equal(
      paused.lockedInputs,
      restartStart.lockedInputs,
      "host restart retains full locks",
    );
    await waitFor(
      () => read(restartStart.runId),
      (value) => value.actions.canResume,
      "server did not authorize expired lease recovery",
    );
    const resume = page.getByRole("button", {
      name: "Resume expired work",
      exact: true,
    });
    await waitFor(
      () => resume.isEnabled(),
      (value) => value,
      "resume UI did not follow server authorization",
    );
    await resume.focus();
    const resumedResponse = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/runs/${restartStart.runId}/resume`) &&
        response.request().method() === "POST",
    );
    await page.keyboard.press("Enter");
    equal(
      (await resumedResponse).status(),
      200,
      "keyboard expired-lease resume accepted",
    );
    const resumed = await read(restartStart.runId);
    equal(
      resumed.progress,
      paused.progress,
      "resume operation does not repeat terminal results",
    );
    equal(
      resumed.lockedInputs,
      paused.lockedInputs,
      "resume preserves same frozen inputs",
    );
    await stopHost();
    await launchHost();
    await page.reload();
    const finished = await waitFor(
      () => read(restartStart.runId),
      (value) => value.state === "Scoring",
      "restarted worker did not resume missing units",
      90000,
    );
    equal(
      finished.progress,
      {
        plannedUnits: 40,
        terminalUnits: 40,
        remainingUnits: 0,
        allTerminal: true,
      },
      "recovered40keyplan closes once",
    );
    equal(
      finished.stateCounts
        .filter((item) => item.count > 0)
        .map((item) => [item.state, item.count])
        .sort(),
      [
        ["Pass", 39],
        ["Finding", 1],
      ].sort(),
      "independent39pass1findinggolden after recovery",
    );
    equal(
      finished.lockedInputs,
      restartStart.lockedInputs,
      "final recovered run retains exact full freeze",
    );
    equal(
      finished.coverageCompletionKind,
      "Complete",
      "complete coverage remains paused scoring",
    );
    await page
      .getByText("Coverage ready. Scoring is pending.", { exact: true })
      .waitFor();
    recordGroup(
      "DEMO-006 actual host SIGKILL/reload/expired-lease keyboard resume/missing-only completion",
    );
  }
  const deniedRoute = "**/local-demo/v1/runs";
  await page.route(deniedRoute, async (route) => {
    if (route.request().method() === "POST")
      await route.fulfill({
        status: 403,
        contentType: "application/json",
        body: JSON.stringify({
          schemaVersion: 1,
          code: "Denied",
          message: "Synthetic denied action. Reload before retrying.",
          correlationId: null,
          currentRevision: null,
        }),
      });
    else await route.continue();
  });
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page.getByRole("alert").waitFor();
  await waitFor(
    () => page.evaluate(() => document.activeElement?.className),
    (value) => value === "error-summary",
    "denied action error not focused",
  );
  check(
    await page
      .getByRole("button", { name: "Refresh connection", exact: true })
      .isVisible(),
    "action error exposes recovery control",
  );
  await page.unroute(deniedRoute);
  await page
    .getByRole("button", { name: "Refresh connection", exact: true })
    .click();
  equal(faults, [], "actual application has no uncaught browser exceptions");
  recordGroup(
    "DEMO-005 unavailable coverage and focused denied-action error recovery",
  );
  report = {
    status: "PASS",
    checks,
    groups,
    accessibility,
    browser: browser.version(),
    node: process.version,
    base,
    limitations: [
      "Automated Chromium engineering smoke only; supported Windows browser and NVDA/Narrator acceptance NOT VERIFIED",
      "Production identity/customer isolation, live source, scoring, AI and ServiceBus NOT VERIFIED",
      ...(groups.some((group) => group.startsWith("DEMO-006"))
        ? []
        : [
            "Expired-lease browser resume NOT VERIFIED without explicitly owned host process",
          ]),
    ],
  };
} catch (error) {
  report = {
    status: "FAIL",
    checks,
    groups,
    error: error.message,
    accessibility,
    browser: browser.version(),
    node: process.version,
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
  await stopHost();
  console.log(JSON.stringify(report));
}
