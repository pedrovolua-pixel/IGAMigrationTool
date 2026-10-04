import { showAssessments } from "../consultant-demo/navigation.mjs";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { until, valid } from "./browser-support.mjs";

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
let host;
let report;
const equal = (actual, expected, message) => {
  assert.deepEqual(actual, expected, message);
  assertions++;
};
const check = (value, message) => {
  assert.ok(value, message);
  assertions++;
};
const group = (name) => {
  groups.push(name);
  console.log(`PASS ${name}`);
};
async function launch() {
  if (!process.env.IGA_HOST_DLL || !process.env.IGA_HOST_WORKING_DIRECTORY)
    return;
  let occupied = false;
  try {
    await fetch(`${base}/local-demo/v1/catalog`);
    occupied = true;
  } catch {}
  assert.equal(
    occupied,
    false,
    "Owned host requires free port; never reuse another host implicitly",
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
  host.stdout.on("data", () => {});
  host.stderr.on("data", () => {});
  await until(
    async () => {
      if (host.exitCode !== null)
        throw new Error(`Owned host exited:${host.exitCode}`);
      try {
        return (await fetch(`${base}/local-demo/v1/catalog`)).status;
      } catch {
        return 0;
      }
    },
    (status) => status === 200,
    "synthetic host did not start",
  );
}
async function stop() {
  if (!host || host.exitCode !== null) return;
  const stopped = once(host, "exit");
  host.kill("SIGTERM");
  let timer;
  const timeout = new Promise((resolve) => {
    timer = setTimeout(() => {
      host.kill("SIGKILL");
      resolve();
    }, 10000);
  });
  await Promise.race([stopped, timeout]);
  clearTimeout(timer);
  if (host.exitCode === null) await stopped;
}
process.once("exit", () => {
  if (host?.exitCode === null) host.kill("SIGTERM");
});
await launch();
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
const dialogs = [];
page.on("pageerror", (error) => errors.push(error.message));
page.on("dialog", async (dialog) => {
  dialogs.push(dialog.message());
  await dialog.dismiss();
});
const api = await request.newContext({ baseURL: base });
try {
  const catalog = await (await api.get("/local-demo/v1/catalog")).json();
  const headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const command = (kind, expectedRevision = 0, fields = {}) => ({
    eventId: randomUUID(),
    expectedRevision,
    kind,
    reason: null,
    text: null,
    title: null,
    businessContext: null,
    ...fields,
  });
  async function start(
    baselineId = "synthetic-analysis-findings-v1",
    profileId = "synthetic-review-maturity-equal-v1",
  ) {
    const response = await api.post("/local-demo/v1/runs", {
      headers,
      data: {
        scopeId: "demo-scope",
        baselineId,
        profileId,
        requestId: randomUUID(),
      },
    });
    equal(response.status(), 201, "fixed opt-in run starts");
    return response.json();
  }
  async function analysis(id) {
    const response = await api.get(`/local-demo/v1/runs/${id}/analysis`);
    equal(response.status(), 200, "actual saved analysis readable");
    const data = await response.json();
    check(
      valid(data, schema.$defs.AnalysisDetail, schema),
      "strict authoritative analysis/review/maturity schema",
    );
    equal(data.runId, id, "analysis binds requested run");
    return data;
  }
  async function review(id) {
    const response = await api.get(`/local-demo/v1/runs/${id}/review`);
    equal(response.status(), 200, "actual saved review readable");
    const data = await response.json();
    check(
      valid(data, schema.$defs.ReviewDetail, schema),
      "strict authoritative review schema",
    );
    equal(data.runId, id, "review binds requested run");
    return data;
  }
  const ready = (id) =>
    until(
      () => analysis(id),
      (data) => data.status === "Ready",
      "opt-in analysis not ready",
    );
  const path = (id, finding) =>
    `/local-demo/v1/runs/${id}/findings/${finding}/events`;
  async function post(id, finding, data, status = 200, extra = {}) {
    const response = await api.post(path(id, finding), {
      headers,
      data,
      ...extra,
    });
    equal(response.status(), status, "review transport expected status");
    return response;
  }
  function scores(data, provisional, publishable, pending) {
    equal(
      [
        data.provisional.display,
        data.publishableCurrent.display,
        data.quality.proposedReviewUnits,
      ],
      [provisional, publishable, pending],
      "independent health/review-count goldens",
    );
    equal(
      data.reviewSnapshotDigest,
      data.review.snapshotDigest,
      "health and review from same coherent durable snapshot",
    );
  }
  async function visibleScores(provisional, publishable, pending) {
    await until(
      () => page.locator(".analysis-score strong").allTextContents(),
      (values) =>
        JSON.stringify(values) === JSON.stringify([provisional, publishable]),
      "visible score cards stale",
    );
    equal(
      await page.locator(".analysis-score strong").allTextContents(),
      [provisional, publishable],
      "rendered health goldens",
    );
    const warning = page
      .locator(".warning-note li")
      .filter({ hasText: "await mandatory review" });
    if (pending)
      equal(
        await warning.textContent(),
        `${pending} Critical/High finding occurrences await mandatory review and are excluded from publishable-current health.`,
        "rendered mandatory review count coherent",
      );
    else
      equal(
        await warning.count(),
        0,
        "no stale rendered mandatory review warning",
      );
  }
  async function visibleHistory(title, count, text) {
    const editor = card(title);
    equal(
      await editor.locator(".review-history summary").textContent(),
      `Append-only history (${count} events)`,
      "rendered append-only history count",
    );
    await editor
      .locator(".review-history")
      .evaluate((node) => (node.open = true));
    equal(
      await editor.locator(".review-history li").count(),
      count,
      "actual rendered event rows",
    );
    if (text)
      check(
        (await editor.locator(".review-history").textContent()).includes(text),
        "rendered history retains exact text",
      );
  }
  async function refreshHistory() {
    const response = page.waitForResponse(
      (value) =>
        value.url() === `${base}/local-demo/v1/runs` &&
        value.request().method() === "GET",
    );
    await page
      .getByRole("button", { name: "Refresh history", exact: true })
      .click();
    const data = await (await response).json();
    await until(
      () =>
        page
          .getByRole("region", { name: "Saved run history table" })
          .locator("tbody tr")
          .first()
          .locator("time")
          .getAttribute("datetime"),
      (value) => value === data.runs[0].createdAtUtc,
      "fresh history first row not rendered",
    );
    await until(
      () =>
        page
          .getByRole("button", { name: "Refresh history", exact: true })
          .isEnabled(),
      (value) => value,
      "history refresh still pending",
    );
    return data;
  }
  const run = await start();
  const original = await ready(run.runId);
  scores(original, "44.2", "78.3", 4);
  const originalReview = await review(run.runId);
  equal(
    originalReview.snapshotDigest,
    original.reviewSnapshotDigest,
    "embedded versus standalone review same snapshot",
  );
  equal(
    originalReview.actor,
    "synthetic-consultant",
    "only fixed disclosed server actor",
  );
  equal(
    [
      original.maturity.level,
      original.maturity.mandatoryDomains,
      original.maturity.insufficientIndicators,
      original.maturity.improvementMissingDistinctAssessments,
    ],
    ["Managed", 5, 0, 5],
    "literal independent frozen maturity with single-assessment limitations",
  );
  const criticalId = original.findings.find(
    (item) => item.severity === "Critical",
  ).id;
  const highId = original.findings.find((item) => item.severity === "High").id;
  check(
    originalReview.findings.filter((item) => item.state === "Proposed")
      .length === 2 &&
      originalReview.findings.every(
        (item) =>
          item.occurrenceIds.length === 2 && item.originalDigests.length === 2,
      ),
    "five groups preserve two occurrences and original refs each",
  );
  const originals = originalReview.findings.map((item) => [
    item.id,
    item.originalTitle,
    item.originalDigests,
    item.occurrenceIds,
  ]);
  await post(run.runId, criticalId, command("Confirm"));
  const confirmed = await analysis(run.runId);
  scores(confirmed, "44.2", "39.2", 2);
  const hostile =
    '<script>window.syntheticInjected=true</script><img src=x onerror=alert(1)> & "quoted"';
  const comment = command("Comment", 1, { text: hostile });
  await post(run.runId, criticalId, comment);
  await post(
    run.runId,
    criticalId,
    command("EditPresentation", 2, {
      title: hostile,
      businessContext: hostile,
    }),
  );
  const beforeReplay = await review(run.runId);
  const replay = await (await post(run.runId, criticalId, comment)).json();
  equal(
    replay.findings.find((item) => item.id === criticalId).revision,
    3,
    "HTTP replay returns fresh current snapshot rather than original event outcome",
  );
  equal(
    replay.snapshotDigest,
    beforeReplay.snapshotDigest,
    "identical replay adds no event",
  );
  await post(
    run.runId,
    criticalId,
    { ...comment, text: "changed replay" },
    409,
  );
  await post(run.runId, criticalId, command("Defer", 0), 409);
  await post(run.runId, criticalId, command("Defer", 3), 409);
  equal(
    (await review(run.runId)).snapshotDigest,
    beforeReplay.snapshotDigest,
    "changed replay/stale/invalid transition have no writes",
  );
  await post(
    run.runId,
    highId,
    command("Reject", 0, {
      reason: "Independent synthetic false-positive explanation",
    }),
  );
  const rejected = await analysis(run.runId);
  scores(rejected, "64.2", "64.2", 0);
  equal(
    rejected.review.findings.map((item) => [
      item.id,
      item.originalTitle,
      item.originalDigests,
      item.occurrenceIds,
    ]),
    originals,
    "all original references unchanged through review/comment/edit",
  );
  equal(
    rejected.maturity,
    original.maturity,
    "current health changes cannot rewrite frozen maturity",
  );
  equal(
    rejected.runRevision,
    original.runRevision,
    "review revisions separate from immutable completed run revision",
  );
  check(
    rejected.review.findings.find((item) => item.id === criticalId).history
      .length === 3 &&
      rejected.review.findings.every((item) =>
        item.history.every(
          (event) =>
            event.actorId === "synthetic-consultant" &&
            event.actorRoles.includes("Consultant") &&
            Date.parse(event.recordedAtUtc),
        ),
      ),
    "attributed persisted history with server time",
  );
  group(
    "RM-API-001 stored originals/current health/coherent snapshot/history/replay/maturity goldens",
  );

  const deferredRun = await start();
  const deferredOriginal = await ready(deferredRun.runId);
  const deferredHigh = deferredOriginal.findings.find(
    (item) => item.severity === "High",
  ).id;
  await post(deferredRun.runId, deferredHigh, command("Defer"));
  scores(await analysis(deferredRun.runId), "44.2", "49.2", 2);
  const healthyRun = await start("synthetic-analysis-healthy-v1");
  const healthy = await ready(healthyRun.runId);
  equal(
    [
      healthy.provisional.display,
      healthy.maturity.level,
      healthy.maturity.insufficientIndicators,
    ],
    ["100.0", "Initial", 25],
    "high health never promotes independent maturity",
  );
  const mixedRun = await start("synthetic-analysis-mixed-v1");
  const mixed = await ready(mixedRun.runId);
  equal(
    [mixed.maturity.level, mixed.maturity.insufficientIndicators],
    ["Developing", 19],
    "fixed mixed maturity/gaps",
  );
  const gapsRun = await start("synthetic-analysis-gaps-v1");
  const gaps = await ready(gapsRun.runId);
  equal(
    [
      gaps.provisional.status,
      gaps.maturity.level,
      gaps.maturity.insufficientIndicators,
    ],
    ["Unavailable", "Initial", 25],
    "all gaps cannot infer healthy or mature state",
  );
  const legacyRun = await start(
    "synthetic-analysis-findings-v1",
    "synthetic-analysis-equal-v1",
  );
  const legacy = await ready(legacyRun.runId);
  equal(
    [legacy.review, legacy.maturity, legacy.reviewSnapshotDigest],
    [null, null, null],
    "older analysis profile remains read-only without new metadata",
  );
  const deniedOld = await review(legacyRun.runId);
  equal(
    [deniedOld.status, deniedOld.reasonCode],
    ["Unavailable", "review_profile_required"],
    "historical write grant absent",
  );
  await post(
    legacyRun.runId,
    legacy.findings.find((item) => item.severity === "Critical").id,
    command("Confirm"),
    403,
  );
  group(
    "RM-API-002 defer penalty/health independence/mixed/gaps/historical read-only profile",
  );

  const currentCritical = (await review(run.runId)).findings.find(
    (item) => item.id === criticalId,
  );
  const goodComment = command("Comment", currentCritical.revision, {
    text: "bounded synthetic comment",
  });
  for (const extra of [
    { actor: "attacker" },
    { role: "CustomerRiskOwner" },
    { scope: "wrong" },
    { category: "OTHER" },
    { weights: {} },
    { originalDigests: [] },
  ])
    await post(run.runId, criticalId, { ...goodComment, ...extra }, 400);
  for (const kind of [
    "AcceptRisk",
    "ValidatedClosed",
    "Reopened",
    "RemediationPlanned",
  ])
    await post(run.runId, criticalId, { ...goodComment, kind }, 400);
  await post(
    run.runId,
    criticalId,
    command("Reject", currentCritical.revision, { reason: "  " }),
    400,
  );
  await post(
    run.runId,
    criticalId,
    command("Comment", currentCritical.revision, { text: "x".repeat(2001) }),
    400,
  );
  await post(
    run.runId,
    criticalId,
    command("EditPresentation", currentCritical.revision, {
      title: "x".repeat(251),
    }),
    400,
  );
  await post(
    run.runId,
    criticalId,
    command("Comment", currentCritical.revision, { text: "x".repeat(5000) }),
    400,
  );
  // All command fields are valid: legal JSON whitespace alone breaches transport4KiB.
  const paddedCommand = " ".repeat(5000) + JSON.stringify(goodComment);
  equal(
    JSON.parse(paddedCommand),
    goodComment,
    "padded body is syntactically valid exact seven-field bounded command",
  );
  check(
    Buffer.byteLength(paddedCommand, "utf8") > 4096,
    "only legal padding exceeds transport byte limit",
  );
  const beforePadded = (await review(run.runId)).snapshotDigest;
  const paddedResponse = await api.post(path(run.runId, criticalId), {
    headers: { ...headers, "Content-Type": "application/json" },
    data: paddedCommand,
  });
  equal(
    paddedResponse.status(),
    400,
    "otherwise valid padded command denied by4KiB transport guard",
  );
  equal(
    (await review(run.runId)).snapshotDigest,
    beforePadded,
    "oversized bounded-field command appends no event or current-state write",
  );
  await post(run.runId, criticalId, goodComment, 403, {
    headers: { Origin: base },
  });
  await post(run.runId, criticalId, goodComment, 403, {
    headers: { ...headers, Origin: "https://attacker.invalid" },
  });
  await post(run.runId, criticalId, goodComment, 403, {
    headers: { ...headers, Host: "attacker.invalid:5183" },
  });
  await post(run.runId, "0".repeat(64), goodComment, 404);
  equal(
    (await api.get(`/local-demo/v1/runs/${randomUUID()}/review`)).status(),
    404,
    "unknown review resource denied",
  );
  equal(
    (await review(run.runId)).snapshotDigest,
    rejected.reviewSnapshotDigest,
    "all hostile transport/payload cases leave current snapshot unchanged",
  );
  group(
    "RM-API-003 server-owned actor/scope/originals unsupported actions/body4KiB CSRF/Host/Origin",
  );

  await page.goto(base);
  await showAssessments(page);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption("synthetic-analysis-findings-v1");
  await page
    .getByLabel("Assessment profile", { exact: true })
    .selectOption("synthetic-review-maturity-equal-v1");
  const startedResponse = page.waitForResponse(
    (response) =>
      response.url() === `${base}/local-demo/v1/runs` &&
      response.request().method() === "POST",
  );
  await page.getByLabel("Assessment profile", { exact: true }).focus();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  const uiRun = await (await startedResponse).json();
  await page
    .getByRole("heading", {
      name: "Consultant review and history",
      exact: true,
    })
    .waitFor();
  let uiOriginal = await analysis(uiRun.runId);
  const uiCritical = uiOriginal.review.findings[0];
  const uiHigh = uiOriginal.review.findings[1];
  const card = (title) =>
    page
      .locator(".review-finding")
      .filter({ has: page.getByRole("heading", { name: title, exact: true }) });
  async function keyboardAction(title, kind, fields = {}) {
    const editor = card(title);
    await editor
      .getByLabel("Review action", { exact: true })
      .selectOption(kind);
    if ("reason" in fields)
      await editor.getByLabel(/Review reason/).fill(fields.reason);
    if ("text" in fields)
      await editor.getByLabel("Comment", { exact: true }).fill(fields.text);
    if ("title" in fields)
      await editor
        .getByLabel("Presentation title", { exact: true })
        .fill(fields.title);
    if ("businessContext" in fields)
      await editor
        .getByLabel("Business context", { exact: true })
        .fill(fields.businessContext);
    const label = {
      Confirm: "Confirm",
      Reject: "Reject",
      Defer: "Defer",
      Comment: "Add comment",
      EditPresentation: "Save presentation",
    }[kind];
    const response = page.waitForResponse(
      (value) =>
        value.url().includes(`/runs/${uiRun.runId}/findings/`) &&
        value.request().method() === "POST",
    );
    await editor.getByRole("button", { name: label, exact: true }).focus();
    await page.keyboard.press("Enter");
    const actual = await response;
    equal(actual.status(), 200, "keyboard review action persisted");
    await page
      .getByRole("heading", {
        name: "Consultant review and history",
        exact: true,
      })
      .waitFor();
    await until(
      () => page.evaluate(() => document.activeElement?.id),
      (value) => value === "analysis-heading",
      "successful review refresh heading focus",
    );
  }
  await visibleScores("44.2", "78.3", 4);
  await keyboardAction(uiCritical.title, "Confirm");
  scores(await analysis(uiRun.runId), "44.2", "39.2", 2);
  await visibleScores("44.2", "39.2", 2);
  await visibleHistory(uiCritical.title, 1);
  const highEditor = card(uiHigh.title);
  await highEditor
    .getByLabel("Review action", { exact: true })
    .selectOption("Reject");
  check(
    await highEditor
      .getByRole("button", { name: "Reject", exact: true })
      .isDisabled(),
    "reject disabled until required reason",
  );
  await keyboardAction(uiHigh.title, "Reject", {
    reason: "Keyboard synthetic rejection reason",
  });
  scores(await analysis(uiRun.runId), "64.2", "64.2", 0);
  await visibleScores("64.2", "64.2", 0);
  await visibleHistory(uiHigh.title, 1, "Keyboard synthetic rejection reason");
  await keyboardAction(uiCritical.title, "Comment", { text: hostile });
  await keyboardAction(uiCritical.title, "EditPresentation", {
    title: hostile,
    businessContext: hostile,
  });
  await visibleHistory(hostile, 3, hostile);
  const editedCard = card(hostile);
  await editedCard
    .locator(".review-history")
    .evaluate((node) => (node.open = true));
  await editedCard.getByText("Generated original", { exact: true }).click();
  check(
    await editedCard
      .getByRole("heading", { name: hostile, exact: true, level: 5 })
      .isVisible(),
    "hostile title rendered as text",
  );
  equal(
    await editedCard.locator("script,img").count(),
    0,
    "no injected markup elements",
  );
  equal(
    await page.evaluate(() => window.syntheticInjected ?? false),
    false,
    "no synthetic script execution",
  );
  equal(dialogs, [], "no injected alert/dialog");
  const uiAfter = await analysis(uiRun.runId);
  equal(
    uiAfter.maturity,
    uiOriginal.maturity,
    "browser reviews leave frozen maturity unchanged",
  );
  equal(
    uiAfter.review.findings.find((item) => item.id === uiCritical.id)
      .originalTitle,
    uiCritical.originalTitle,
    "original title preserved after edited presentation",
  );
  await page.reload();
  await showAssessments(page);
  await page
    .getByLabel("Consultant review and history", { exact: true })
    .getByRole("heading", { name: hostile, exact: true, level: 5 })
    .waitFor();
  equal(
    (await analysis(uiRun.runId)).reviewSnapshotDigest,
    uiAfter.reviewSnapshotDigest,
    "reload retains exact review history snapshot",
  );
  await refreshHistory();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .focus();
  await page.keyboard.press("Enter");
  await page
    .getByLabel("Consultant review and history", { exact: true })
    .getByRole("heading", { name: hostile, exact: true, level: 5 })
    .waitFor();
  group(
    "RM-UI-001 actual keyboard confirm/reject/comment/edit inert text/originals/history/reload",
  );

  const staleEditor = card(hostile);
  await staleEditor
    .getByLabel("Review action", { exact: true })
    .selectOption("Comment");
  await staleEditor
    .getByLabel("Comment", { exact: true })
    .fill("must conflict, not overwrite");
  const latest = await review(uiRun.runId);
  const latestCritical = latest.findings.find(
    (item) => item.id === uiCritical.id,
  );
  await post(
    uiRun.runId,
    uiCritical.id,
    command("Comment", latestCritical.revision, {
      text: "Independent competing writer",
    }),
  );
  const conflicted = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/findings/${uiCritical.id}/events`) &&
      response.request().method() === "POST",
  );
  await staleEditor
    .getByRole("button", { name: "Add comment", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  equal(
    (await conflicted).status(),
    409,
    "actual stale finding revision refused",
  );
  const conflictAlert = staleEditor.getByRole("alert");
  await conflictAlert.waitFor();
  check(
    (await conflictAlert.isVisible()) &&
      (await conflictAlert.textContent()).includes("Refresh"),
    "conflict explanation remains visible",
  );
  check(
    await conflictAlert.evaluate(
      (element) => element === document.activeElement,
    ),
    "conflict alert receives focus",
  );
  check(
    await staleEditor
      .getByRole("button", { name: "Add comment", exact: true })
      .isDisabled(),
    "stale form cannot blindly repeat write",
  );
  equal(
    await conflictAlert
      .getByRole("button", { name: "Retry same review event", exact: true })
      .count(),
    0,
    "deterministic conflict has no ambiguous retry",
  );
  await conflictAlert
    .getByRole("button", { name: "Refresh saved analysis", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page
    .getByRole("heading", {
      name: "Consultant review and history",
      exact: true,
    })
    .waitFor();
  equal(
    (await review(uiRun.runId)).findings.find(
      (item) => item.id === uiCritical.id,
    ).history.length,
    latestCritical.history.length + 1,
    "one competing event only, stale command never appended",
  );
  const responseRoute = `**/local-demo/v1/runs/${uiRun.runId}/analysis`;
  await page.route(responseRoute, async (route) => {
    const actual = await route.fetch();
    const data = await actual.json();
    await route.fulfill({
      response: actual,
      json: { ...data, review: { ...data.review, runId: healthyRun.runId } },
    });
  });
  await page.reload();
  await showAssessments(page);
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .waitFor();
  equal(
    await page.locator(".review-finding").count(),
    0,
    "cross-run review response cannot populate active actions",
  );
  await page.unroute(responseRoute);
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .click();
  await page
    .getByRole("heading", {
      name: "Consultant review and history",
      exact: true,
    })
    .waitFor();
  group(
    "RM-UI-002 actual stale CAS focused recovery and cross-run response denial",
  );

  // Commit on server, deliberately lose HTTP response, then exercise captured-event UI retry.
  const lostPath = `**/local-demo/v1/runs/${uiRun.runId}/findings/${uiCritical.id}/events`;
  let capturedCommand;
  let lostCalls = 0;
  await page.route(lostPath, async (route) => {
    lostCalls++;
    if (lostCalls === 1) {
      capturedCommand = route.request().postDataJSON();
      const committed = await route.fetch();
      equal(
        committed.status(),
        200,
        "lost response command actually committed",
      );
      await route.abort("failed");
    } else {
      equal(
        route.request().postDataJSON(),
        capturedCommand,
        "ambiguous retry reuses exact UUID and seven-field payload",
      );
      await route.continue();
    }
  });
  const lossEditor = card(hostile);
  await lossEditor
    .getByLabel("Review action", { exact: true })
    .selectOption("Comment");
  await lossEditor
    .getByLabel("Comment", { exact: true })
    .fill("Committed response lost; explicit same event retry");
  await lossEditor
    .getByRole("button", { name: "Add comment", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  const retry = lossEditor.getByRole("button", {
    name: "Retry same review event",
    exact: true,
  });
  await retry.waitFor();
  equal(lostCalls, 1, "no automatic ambiguous write retry");
  const beforeRetry = await review(uiRun.runId);
  equal(
    beforeRetry.findings.find((f) => f.id === uiCritical.id).history.length,
    5,
    "lost response appended exactly one server event",
  );
  await retry.focus();
  await page.keyboard.press("Enter");
  await until(
    () => page.evaluate(() => document.activeElement?.id),
    (id) => id === "analysis-heading",
    "retry success focus",
  );
  equal(lostCalls, 2, "one explicit retry only");
  equal(
    (await review(uiRun.runId)).snapshotDigest,
    beforeRetry.snapshotDigest,
    "UI identical replay appends no second history event",
  );
  await page.unroute(lostPath);
  await visibleScores("64.2", "64.2", 0);
  await visibleHistory(hostile, 5, capturedCommand.text);

  // Hold actual old-run GET while selecting a different newest saved run.
  const switched = await start("synthetic-analysis-healthy-v1");
  await ready(switched.runId);
  let releaseGet, heldGetReady;
  const getHeld = new Promise((resolve) => (heldGetReady = resolve));
  const getGate = new Promise((resolve) => (releaseGet = resolve));
  await page.route(responseRoute, async (route) => {
    const actual = await route.fetch();
    heldGetReady();
    await getGate;
    try {
      await route.fulfill({ response: actual });
    } catch {
      /* browser aborts obsolete selection */
    }
  });
  await page.reload();
  await showAssessments(page);
  await getHeld;
  await refreshHistory();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .click();
  await page
    .getByRole("heading", { name: "Maturity: Initial", exact: true })
    .waitFor();
  await visibleScores("100.0", "100.0", 0);
  const selectionFocus = await page.evaluate(() => document.activeElement?.id);
  releaseGet();
  await page.unroute(responseRoute);
  await page.waitForTimeout(150);
  await visibleScores("100.0", "100.0", 0);
  equal(
    await page.locator(".review-finding").count(),
    0,
    "delayed prior-run GET cannot restore prior actions",
  );
  equal(
    await page.evaluate(() => document.activeElement?.id),
    selectionFocus,
    "obsolete GET cannot steal new selection focus",
  );

  // Hold committed old-run POST while selecting another saved run.
  const delayedRun = await start();
  const delayedData = await ready(delayedRun.runId);
  await refreshHistory();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .click();
  await visibleScores("44.2", "78.3", 4);
  const newer = await start("synthetic-analysis-healthy-v1");
  await ready(newer.runId);
  await refreshHistory();
  let releasePost, heldPostReady;
  const postHeld = new Promise((resolve) => (heldPostReady = resolve));
  const postGate = new Promise((resolve) => (releasePost = resolve));
  const delayedPost = `**/local-demo/v1/runs/${delayedRun.runId}/findings/*/events`;
  await page.route(delayedPost, async (route) => {
    const actual = await route.fetch();
    equal(actual.status(), 200, "old-run held POST committed");
    heldPostReady();
    await postGate;
    try {
      await route.fulfill({ response: actual });
    } catch {}
  });
  const delayedCard = card(delayedData.review.findings[0].title);
  await delayedCard
    .getByLabel("Review action", { exact: true })
    .selectOption("Confirm");
  await delayedCard
    .getByRole("button", { name: "Confirm", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await postHeld;
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .click();
  await page
    .getByRole("heading", { name: "Maturity: Initial", exact: true })
    .waitFor();
  await visibleScores("100.0", "100.0", 0);
  const postFocus = await page.evaluate(() => document.activeElement?.id);
  releasePost();
  await page.unroute(delayedPost);
  await page.waitForTimeout(150);
  await visibleScores("100.0", "100.0", 0);
  equal(
    await page.locator(".review-finding").count(),
    0,
    "delayed old-run POST cannot restore old actions",
  );
  equal(
    await page.evaluate(() => document.activeElement?.id),
    postFocus,
    "delayed POST cannot steal new-run focus",
  );
  equal(
    (await review(delayedRun.runId)).findings[0].history.length,
    1,
    "old-run commit retained solely on its own run",
  );
  group(
    "RM-UI-005 committed-response-loss exact-event retry and delayed GET/POST selection races with visible score/history goldens",
  );

  const deferredUI = await start();
  await ready(deferredUI.runId);
  await refreshHistory();
  await page
    .getByRole("region", { name: "Saved run history table" })
    .getByRole("button")
    .first()
    .click();
  await page
    .getByRole("heading", {
      name: "Consultant review and history",
      exact: true,
    })
    .waitFor();
  const deferCard = page.locator(".review-finding").nth(1);
  await deferCard
    .getByLabel("Review action", { exact: true })
    .selectOption("Defer");
  const deferResponse = page.waitForResponse(
    (response) =>
      response.url().includes(`/runs/${deferredUI.runId}/findings/`) &&
      response.request().method() === "POST",
  );
  await deferCard.getByRole("button", { name: "Defer", exact: true }).focus();
  await page.keyboard.press("Enter");
  equal((await deferResponse).status(), 200, "keyboard Defer persisted");
  await page
    .getByRole("heading", {
      name: "Consultant review and history",
      exact: true,
    })
    .waitFor();
  scores(await analysis(deferredUI.runId), "44.2", "49.2", 2);
  await visibleScores("44.2", "49.2", 2);
  await visibleHistory((await review(deferredUI.runId)).findings[1].title, 1);
  if (host) {
    const saved = await analysis(deferredUI.runId);
    await stop();
    await launch();
    await page.reload();
    await showAssessments(page);
    await page
      .getByRole("heading", {
        name: "Consultant review and history",
        exact: true,
      })
      .waitFor();
    equal(
      (await analysis(deferredUI.runId)).reviewSnapshotDigest,
      saved.reviewSnapshotDigest,
      "actual host restart preserves review snapshot and maturity",
    );
  }
  group(
    "RM-UI-003 keyboard Defer and actual owned-host restart preserves durable history",
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
    equal(result.violations, [], `axe ${label} confirmed violations`);
  }
  await page.locator(".review-history summary").nth(1).click();
  await page.locator(".maturity-domain summary").first().click();
  await scan("desktop review/history/maturity");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./desktop-review.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./mobile-review.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 800 });
  check(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
    "320px expanded history/maturity no page overflow",
  );
  await scan("320px review/history/maturity");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: new URL("./reflow-review.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  check(
    await page
      .getByRole("heading", { name: "Maturity: Managed", exact: true })
      .isVisible(),
    "independent maturity label visible in forced colors",
  );
  equal(errors, [], "no uncaught browser application exceptions");
  group(
    "RM-UI-004 desktop390320 expanded history/evidence/axe/forced-color engineering smoke",
  );
  report = {
    status: "PASS",
    assertions,
    groups,
    accessibility,
    browser: browser.version(),
    node: process.version,
    ownedHostRestartExecuted: !!host,
    limitations: [
      "Fixed synthetic reviewer identity, fictional maturity indicators only; no customer risk acceptance/publication/production authority",
      "Full supported Windows/NVDA/Narrator acceptance NOT VERIFIED",
      ...accessibility
        .filter((item) => item.incomplete.length)
        .map(
          (item) =>
            `Axe ${item.label} incomplete:${item.incomplete.join(",")}; NOT VERIFIED`,
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
    visibleScores: await page
      .locator(".analysis-score strong")
      .allTextContents(),
    selectedHistory: await page.locator(".selected-row").allTextContents(),
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
  await stop();
  console.log(JSON.stringify(report));
}
