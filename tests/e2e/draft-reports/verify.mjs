import assert from "node:assert/strict";
import { createHash, randomUUID } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { until, valid } from "./browser-support.mjs";
import { base, launchHost, stopHost, ownsHost } from "./host-harness.mjs";
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
const groups = [],
  accessibility = [];
const eq = (a, b, m) => {
  assert.deepEqual(a, b, m);
  assertions++;
};
const ok = (a, m) => {
  assert.ok(a, m);
  assertions++;
};
const group = (m) => {
  groups.push(m);
  console.log(`PASS ${m}`);
};
const sha = (s) => createHash("sha256").update(s, "utf8").digest("hex");
const hostile =
  "## stolen\n<script>alert(1)</script> & [link](https://evil.invalid/a) ![img](https://evil.invalid/x) ```sh\n$(touch /tmp/x)\n=SUM(A1:A2)\t\u202e";
let browser, context, page, api, report, headers;
const errors = [],
  dialogs = [],
  external = [];
try {
  await launchHost();
  ok(ownsHost(), "owned host required for process-restart evidence");
  browser = await chromium.launch({
    headless: true,
    ...(process.env.IGA_CHROMIUM
      ? { executablePath: process.env.IGA_CHROMIUM }
      : {}),
  });
  context = await browser.newContext({
    viewport: { width: 1280, height: 900 },
  });
  page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("dialog", async (d) => {
    dialogs.push(d.message());
    await d.dismiss();
  });
  page.on("request", (r) => {
    if (!r.url().startsWith(base) && !r.url().startsWith("data:"))
      external.push(r.url());
  });
  api = await request.newContext({ baseURL: base });
  const catalog = await (await api.get("/local-demo/v1/catalog")).json();
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  async function start(
    baseline = "findings",
    profile = "synthetic-review-maturity-equal-v1",
  ) {
    const response = await api.post("/local-demo/v1/runs", {
      headers,
      data: {
        scopeId: "demo-scope",
        baselineId: `synthetic-analysis-${baseline}-v1`,
        profileId: profile,
        requestId: randomUUID(),
      },
    });
    eq(response.status(), 201, "actual synthetic run starts");
    return response.json();
  }
  async function analysis(id) {
    const r = await api.get(`/local-demo/v1/runs/${id}/analysis`);
    eq(r.status(), 200, "existing analysis route");
    const d = await r.json();
    ok(
      valid(d, schema.$defs.AnalysisDetail, schema),
      "exact private analysis schema",
    );
    eq(d.runId, id, "requested run binding");
    return d;
  }
  const ready = (id) =>
    until(
      () => analysis(id),
      (d) => d.status === "Ready",
      "saved synthetic analysis never ready",
    );
  async function event(run, finding, kind, fields = {}) {
    const c = {
      eventId: randomUUID(),
      expectedRevision: finding.revision,
      kind,
      reason: null,
      text: null,
      title: null,
      businessContext: null,
      ...fields,
    };
    const r = await api.post(
      `/local-demo/v1/runs/${run}/findings/${finding.id}/events`,
      { headers, data: c },
    );
    eq(r.status(), 200, "approved existing review action");
    return ready(run);
  }
  function draft(d, provisional, current, pending, level) {
    eq(d.reportDraft.status, "Ready", "verified synthetic draft available");
    const { snapshot: s, markdown: m } = d.reportDraft,
      c = s.content,
      b = s.source;
    eq(
      [s.schemaVersion, s.status],
      ["synthetic-draft-report-v1", "SyntheticDraft"],
      "schema/status are synthetic and unpublished",
    );
    eq(
      [
        c.provisional.display,
        c.publishableCurrent.display,
        c.quality.proposedReviewUnits,
        c.maturity.level,
      ],
      [provisional, current, pending, level],
      "independent score/maturity/pending literals",
    );
    eq(
      [b.runId, b.runRevision, b.runState, b.reviewRunId, b.reviewRunRevision],
      [d.runId, d.runRevision, "Scoring", d.runId, d.runRevision],
      "same saved source and review revision",
    );
    eq(
      b.reviewSnapshotDigest,
      d.reviewSnapshotDigest,
      "same coherent review snapshot",
    );
    eq(b.scoringContentDigest, d.contentDigest, "same current scoring content");
    eq(
      [b.maturityInputDigest, b.maturityContentDigest],
      [d.maturity.inputDigest, d.maturity.contentDigest],
      "same immutable maturity",
    );
    eq(c.maturity, d.maturity, "complete maturity snapshot retained");
    for (const k of [
      "provisional",
      "publishableCurrent",
      "categories",
      "objectTypes",
      "modules",
      "outcomes",
      "quality",
      "warnings",
    ])
      eq(c[k], d[k], `complete ${k} parity`);
    eq(m.version, "synthetic-draft-markdown-v1", "structured renderer version");
    eq(
      m.canonicalContentDigest,
      s.canonicalContentDigest,
      "same canonical draft digest",
    );
    eq(sha(m.markdownText), m.markdownSha256, "independent actual UTF8 hash");
    for (const section of [
      "Source binding",
      "Summary",
      "Findings",
      "Maturity",
      "Review history",
      "Methodology",
      "Unavailable sections",
    ])
      ok(
        m.markdownText.toLowerCase().includes(section.toLowerCase()),
        `literal Markdown section ${section}`,
      );
    eq(
      c.unavailableSections.length,
      5,
      "five later sections explicitly unavailable",
    );
    ok(
      !JSON.stringify(s).includes("csrfToken") &&
        !JSON.stringify(s).includes("observedAtDatabaseUtc"),
      "draft excludes token/observation time",
    );
    return d.reportDraft;
  }
  const run = await start();
  let data = await ready(run.runId);
  const original = draft(data, "44.2", "78.3", 4, "Managed");
  const originalBytes = JSON.stringify(original);
  const maturity = JSON.stringify(data.maturity);
  const originalRefs = data.findings.map((f) => [
    f.id,
    f.originalTitle,
    f.originalDigests,
    f.objectIds,
  ]);
  eq(
    data.reportDraft.snapshot.content.findings.length,
    5,
    "five root-cause groups retained",
  );
  eq(
    data.maturity.improvementMissingDistinctAssessments,
    5,
    "five improvement domains lack two assessments",
  );
  data = await event(
    run.runId,
    data.review.findings.find(
      (f) =>
        f.category === "SECURITY" &&
        f.originalTitle ===
          data.findings.find((x) => x.severity === "Critical").originalTitle,
    ),
    "Confirm",
  );
  draft(data, "44.2", "39.2", 2, "Managed");
  const intermediateDigest = data.reportDraft.snapshot.canonicalContentDigest;
  data = await event(
    run.runId,
    data.review.findings.find(
      (f) =>
        f.originalTitle ===
        data.findings.find((x) => x.severity === "High").originalTitle,
    ),
    "Reject",
    { reason: "Independent synthetic rejection fixture" },
  );
  draft(data, "64.2", "64.2", 0, "Managed");
  ok(
    intermediateDigest !== data.reportDraft.snapshot.canonicalContentDigest,
    "review changes canonical current value",
  );
  eq(
    JSON.stringify(original),
    originalBytes,
    "old returned draft remains byte immutable",
  );
  eq(
    JSON.stringify(data.maturity),
    maturity,
    "health/review never rewrite maturity",
  );
  eq(
    data.findings.map((f) => [
      f.id,
      f.originalTitle,
      f.originalDigests,
      f.objectIds,
    ]),
    originalRefs,
    "generated original identities preserved",
  );
  const criticalId = data.findings.find((f) => f.severity === "Critical").id;
  data = await event(
    run.runId,
    data.review.findings.find((f) => f.id === criticalId),
    "Comment",
    { text: hostile },
  );
  data = await event(
    run.runId,
    data.review.findings.find((f) => f.id === criticalId),
    "EditPresentation",
    {
      title: "<img src=x onerror=alert(2)> Edited synthetic title",
      businessContext: hostile,
    },
  );
  const hostileDraft = draft(data, "64.2", "64.2", 0, "Managed");
  ok(
    hostileDraft.markdown.markdownText.includes(
      "&lt;script&gt;alert\\(1\\)&lt;\\/script&gt;",
    ),
    "hostile HTML entity and punctuation literal",
  );
  ok(
    !hostileDraft.markdown.markdownText.includes("https://evil.invalid") &&
      !hostileDraft.markdown.markdownText.includes("```sh"),
    "hostile URLs/fences escaped",
  );
  const history = hostileDraft.snapshot.content.reviewHistory.find(
    (f) => f.findingId === criticalId,
  );
  eq(
    history.events.length,
    3,
    "confirm/comment/edit append attributed history",
  );
  eq(
    history.events.at(-1).businessContext,
    hostile,
    "exact hostile text captured as data",
  );
  group(
    "DR-API-001 actual saved coherent draft, review change, immutable originals and hostile Markdown",
  );
  let gapsRun;
  for (const [baseline, p, c, n, level] of [
    ["healthy", "100.0", "100.0", 0, "Initial"],
    ["gaps", null, null, 0, "Initial"],
    ["mixed", "66.7", "89.2", 2, "Developing"],
  ]) {
    const r = await start(baseline),
      d = await ready(r.runId);
    draft(d, p, c, n, level);
    if (baseline === "gaps") {
      gapsRun = r;
      eq(
        d.reportDraft.snapshot.content.limitations.length,
        10,
        "ten actual gap explanations",
      );
      eq(d.quality.gapUnits, 10, "all planned gaps disclosed");
    }
  }
  const operations = await start(
    "findings",
    "synthetic-review-maturity-operations-v1",
  );
  draft(await ready(operations.runId), "61.3", "78.3", 4, "Managed");
  for (const profile of [
    "synthetic-analysis-equal-v1",
    "synthetic-analysis-operations-v1",
  ]) {
    const r = await start("healthy", profile);
    const d = await ready(r.runId);
    eq(
      d.reportDraft,
      null,
      "old analysis profiles remain read-only without draft",
    );
  }
  group(
    "DR-API-002 independent healthy/gap/mixed/weighted and historical profile literals",
  );
  const uiRun = await start();
  let uiData = await ready(uiRun.runId);
  await page.goto(base);
  const draftSection = page.locator(".draft-report");
  async function refresh() {
    const response = page.waitForResponse(
      (r) =>
        r.url() === `${base}/local-demo/v1/runs` &&
        r.request().method() === "GET",
    );
    await page
      .getByRole("button", { name: "Refresh history", exact: true })
      .click();
    const d = await (await response).json();
    await until(
      () =>
        page
          .getByRole("region", { name: "Saved run history table" })
          .locator("tbody tr")
          .first()
          .locator("time")
          .getAttribute("datetime"),
      (v) => v === d.runs[0].createdAtUtc,
      "history not freshly rendered",
    );
    return d;
  }
  async function select(id) {
    const d = await refresh();
    const index = d.runs.findIndex((r) => r.runId === id);
    ok(index >= 0, "requested saved history row exists");
    await page
      .getByRole("region", { name: "Saved run history table" })
      .locator("tbody tr")
      .nth(index)
      .getByRole("button")
      .click();
    await until(
      () => draftSection.locator(".draft-digest code").first().textContent(),
      (v) => v?.length === 64,
      "selected draft not rendered",
    );
  }
  async function visible(p, c, n, level = "Managed") {
    await until(
      () => draftSection.locator(".draft-kpis dd").allTextContents(),
      (v) =>
        JSON.stringify(v) ===
        JSON.stringify([
          `${c} / 100 · ${Number(c) >= 80 ? "Green" : Number(c) >= 50 ? "Yellow" : "Red"}`,
          `${p} / 100 · ${Number(p) >= 80 ? "Green" : Number(p) >= 50 ? "Yellow" : "Red"}`,
          level,
          `${n} finding occurrences`,
        ]),
      "visible draft score/maturity/pending stale",
    );
    eq(
      await draftSection.locator(".draft-digest code").first().textContent(),
      uiData.reportDraft.snapshot.canonicalContentDigest,
      "visible current canonical digest",
    );
  }
  async function keyboard(label) {
    const b = draftSection.getByRole("button", { name: label, exact: true });
    await b.focus();
    await page.keyboard.press("Enter");
    eq(
      await b.getAttribute("aria-pressed"),
      "true",
      "native keyboard selected view",
    );
    ok(
      await b.evaluate((n) => n === document.activeElement),
      "view switch preserves keyboard focus",
    );
  }
  await select(uiRun.runId);
  draft(uiData, "44.2", "78.3", 4, "Managed");
  await visible("44.2", "78.3", 4);
  eq(
    await draftSection.locator(".draft-finding").count(),
    2,
    "summary promotes Critical/High only",
  );
  await keyboard("Technical draft");
  eq(
    await draftSection.locator(".draft-finding").count(),
    5,
    "technical view preserves all five groups",
  );
  await draftSection
    .getByText("Captured source and reproducibility", { exact: true })
    .click();
  await draftSection.locator("#draft-maturity-heading").waitFor();
  eq(
    await page.locator("#draft-maturity-heading").count(),
    1,
    "captured maturity has unique heading id",
  );
  eq(
    await draftSection.locator(".draft-digest code").first().textContent(),
    uiData.reportDraft.snapshot.canonicalContentDigest,
    "technical canonical source unchanged",
  );
  eq(
    await draftSection
      .getByRole("region", { name: "Category health", exact: true })
      .locator("tbody tr")
      .allTextContents(),
    [
      "OPERATIONS78.3 / 100 · Yellow78.3 / 100 · Yellow",
      "SECURITYUnavailable10.0 / 100 · Red",
    ],
    "visible independent category rows and distinct current/provisional columns",
  );
  eq(
    await draftSection
      .getByRole("region", { name: "Object-type health", exact: true })
      .locator("tbody tr")
      .allTextContents(),
    ["SyntheticControl78.3 / 100 · Yellow51.0 / 100 · Yellow"],
    "visible independent object-type columns (five equal rule weights earn 0+0.2+0.6+0.8+0.95, distinct from equal category overall)",
  );
  eq(
    await draftSection
      .getByRole("region", { name: "Module health", exact: true })
      .locator("tbody tr")
      .allTextContents(),
    [
      "SyntheticOperations78.3 / 100 · Yellow78.3 / 100 · Yellow",
      "SyntheticSecurityUnavailable10.0 / 100 · Red",
    ],
    "visible independent module columns",
  );
  eq(
    await draftSection
      .getByRole("region", { name: "Maturity thresholds", exact: true })
      .locator("tbody tr")
      .allTextContents(),
    [
      "Developing5 / 560%Met",
      "Defined5 / 580%Met",
      "Managed5 / 580%Met",
      "Optimized0 / 580%Not met",
    ],
    "captured maturity gates preserve independent literal counts",
  );
  await draftSection.locator(".draft-quality").evaluate((n) => (n.open = true));
  eq(
    await draftSection.locator(".draft-quality dd").allTextContents(),
    ["10", "10", "0", "0", "10"],
    "visible separate planned/executed/gap/not-applicable/finding counts",
  );
  await keyboard("Markdown preview");
  eq(
    await draftSection
      .getByLabel("Structured Markdown text", { exact: true })
      .textContent(),
    uiData.reportDraft.markdown.markdownText,
    "inert UI Markdown is exact byte-hashed source text",
  );
  eq(
    await draftSection.locator("a,img,script").count(),
    0,
    "draft does not render link/image/script nodes",
  );
  eq(
    await draftSection.locator(".draft-digest code").nth(1).textContent(),
    uiData.reportDraft.markdown.markdownSha256,
    "visible Markdown hash matches source",
  );
  await keyboard("Summary draft");
  const editor = page.locator(".review-finding").filter({
    has: page.getByRole("heading", {
      name: uiData.findings.find((f) => f.severity === "Critical").title,
      exact: true,
    }),
  });
  await editor
    .getByLabel("Review action", { exact: true })
    .selectOption({ label: "Confirm" });
  await editor.getByRole("button", { name: "Confirm", exact: true }).focus();
  await page.keyboard.press("Enter");
  uiData = await until(
    () => ready(uiRun.runId),
    (d) => d.quality.proposedReviewUnits === 2,
    "keyboard confirm not persisted",
  );
  await visible("44.2", "39.2", 2);
  eq(
    await page.evaluate(() => document.activeElement?.id),
    "analysis-heading",
    "review success returns focus to analysis",
  );
  group(
    "DR-UI-001 native summary/technical/inert Markdown, unique maturity, keyboard review refresh",
  );
  await select(gapsRun.runId);
  await keyboard("Technical draft");
  eq(
    await draftSection.locator(".draft-kpis dd").allTextContents(),
    ["Unavailable", "Unavailable", "Initial", "0 finding occurrences"],
    "visible all-gap draft never infers zero or healthy100",
  );
  await draftSection.locator(".draft-quality").evaluate((n) => (n.open = true));
  eq(
    await draftSection.locator(".draft-quality dd").allTextContents(),
    ["10", "0", "10", "0", "0"],
    "visible all-gap quality denominator literal",
  );
  eq(
    await draftSection.locator(".draft-quality li").count(),
    10,
    "ten visible captured limitations",
  );
  eq(
    (await draftSection.locator(".draft-quality li").allTextContents()).filter(
      (t) => t.includes("SYNTHETIC-FACT-MISSING"),
    ).length,
    5,
    "five actual missing-data reasons visible",
  );
  eq(
    (await draftSection.locator(".draft-quality li").allTextContents()).filter(
      (t) => t.includes("SYNTHETIC-FIXTURE-EXCLUSION"),
    ).length,
    5,
    "five explicit exclusion reasons visible",
  );
  // Select actual hostile saved value; no test injects markup into the DOM.
  await select(run.runId);
  uiData = data;
  await visible("64.2", "64.2", 0);
  await keyboard("Technical draft");
  const hostileCard = draftSection
    .locator(".draft-finding")
    .filter({ hasText: "Edited synthetic title" });
  ok(
    (await hostileCard.textContent()).includes("<img src=x onerror=alert(2)>"),
    "edited title is literal visible text",
  );
  await hostileCard.locator("details").evaluate((n) => (n.open = true));
  ok(
    (await hostileCard.textContent()).includes(
      "Generated original: " +
        data.findings.find((f) => f.id === criticalId).originalTitle +
        " · Proposed",
    ),
    "captured original remains distinct from current edited title",
  );
  const capturedHistory = draftSection.locator("details").filter({
    has: page.getByText("Captured finding review history", { exact: true }),
  });
  await capturedHistory.evaluate((n) => (n.open = true));
  const capturedGroup = capturedHistory.locator("article").filter({
    has: page.getByRole("heading", {
      name: data.findings.find((f) => f.id === criticalId).originalTitle,
      exact: true,
    }),
  });
  eq(
    await capturedGroup.locator("li").count(),
    3,
    "three visible captured attributed events",
  );
  const eventText = await capturedGroup.locator("li").allTextContents();
  for (const [i, kind] of ["Confirm", "Comment", "EditPresentation"].entries())
    ok(
      eventText[i].startsWith(
        `${kind} · Confirmed · revision ${i + 1} · synthetic-consultant`,
      ),
      "visible attributed kind/state/revision/actor literal",
    );
  ok(
    eventText[1].includes("Comment: " + hostile) &&
      eventText[2].includes("Business context: " + hostile),
    "captured history preserves inert original multiline text",
  );
  await keyboard("Markdown preview");
  eq(
    await draftSection
      .getByLabel("Structured Markdown text", { exact: true })
      .textContent(),
    hostileDraft.markdown.markdownText,
    "hostile structured Markdown rendered verbatim",
  );
  eq(
    await draftSection.locator("a,img,script").count(),
    0,
    "hostile values create no executable nodes",
  );
  eq(dialogs, [], "hostile text triggers no dialogs");
  eq(external, [], "no hostile outbound loads");
  // Each substitution exercises actual request -> UI coherence guard, then same-revision keyboard retry.
  const target = `**/local-demo/v1/runs/${run.runId}/analysis`;
  const substitutions = [
    ["schema", (d) => (d.reportDraft.snapshot.schemaVersion = "unknown")],
    ["published", (d) => (d.reportDraft.snapshot.status = "Published")],
    ["Markdown version", (d) => (d.reportDraft.markdown.version = "unknown")],
    [
      "canonical digest",
      (d) => (d.reportDraft.markdown.canonicalContentDigest = "0".repeat(64)),
    ],
    ["run", (d) => (d.reportDraft.snapshot.source.runId = randomUUID())],
    [
      "review revision",
      (d) => d.reportDraft.snapshot.source.reviewRunRevision++,
    ],
    ["baseline", (d) => (d.reportDraft.snapshot.source.baselineId = "other")],
    [
      "profile",
      (d) =>
        (d.reportDraft.snapshot.source.profileId =
          "synthetic-analysis-equal-v1"),
    ],
    [
      "input digest",
      (d) => (d.reportDraft.snapshot.source.runInputDigest = "0".repeat(64)),
    ],
    [
      "capability tuple",
      (d) =>
        (d.reportDraft.snapshot.source.capabilityLock.lockDigest = "0".repeat(
          64,
        )),
    ],
    [
      "scripted digest",
      (d) =>
        (d.reportDraft.snapshot.source.frozenVersions.scriptedResultsDigest =
          "0".repeat(64)),
    ],
    [
      "application lock",
      (d) =>
        (d.reportDraft.snapshot.source.frozenVersions.applicationVersion =
          "other"),
    ],
    [
      "desired outcomes",
      (d) =>
        (d.reportDraft.snapshot.source.frozenVersions.desiredOutcomeVersion =
          "other"),
    ],
    [
      "analysis fixture",
      (d) =>
        (d.reportDraft.snapshot.source.analysisFixtureDigest = "0".repeat(64)),
    ],
    [
      "scoring digest",
      (d) =>
        (d.reportDraft.snapshot.source.scoringContentDigest = "0".repeat(64)),
    ],
    [
      "maturity digest",
      (d) =>
        (d.reportDraft.snapshot.source.maturityInputDigest = "0".repeat(64)),
    ],
    [
      "wrong scope",
      (d) => (d.reportDraft.snapshot.source.scope.environmentId = "wrong"),
    ],
  ];
  for (const [name, mutate] of substitutions) {
    await page.route(target, async (route) => {
      const r = await route.fetch(),
        d = await r.json();
      mutate(d);
      await route.fulfill({ response: r, json: d });
    });
    await page.reload();
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .waitFor();
    eq(await draftSection.count(), 0, `fail closed substituted ${name}`);
    await page.unroute(target);
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .focus();
    await page.keyboard.press("Enter");
    await draftSection.waitFor();
    await visible("64.2", "64.2", 0);
    await until(
      () => page.evaluate(() => document.activeElement?.id),
      (v) => v === "analysis-heading",
      "retry did not restore intentional focus",
    );
  }
  group(
    "DR-UI-002 source/version/digest/scope substitution refusal and same-revision retry",
  );
  // A held fetched old response cannot replace the newly selected saved run.
  let release, fetched;
  const gate = new Promise((r) => (release = r)),
    seen = new Promise((r) => (fetched = r));
  await page.route(target, async (route) => {
    const r = await route.fetch();
    fetched();
    await gate;
    try {
      await route.fulfill({ response: r });
    } catch {}
  });
  await page.reload();
  await seen;
  const healthy = await start("healthy");
  const healthyData = await ready(healthy.runId);
  await select(healthy.runId);
  const focusBeforeRelease = await page.evaluate(() => ({
    id: document.activeElement?.id,
    text: document.activeElement?.textContent,
  }));
  release();
  await page.unroute(target);
  uiData = healthyData;
  await visible("100.0", "100.0", 0, "Initial");
  await page.waitForTimeout(150);
  eq(
    await draftSection.locator(".draft-digest code").first().textContent(),
    healthyData.reportDraft.snapshot.canonicalContentDigest,
    "late old response does not replace selected draft",
  );
  eq(
    await page.evaluate(() => ({
      id: document.activeElement?.id,
      text: document.activeElement?.textContent,
    })),
    focusBeforeRelease,
    "late old response does not transfer current selection focus",
  );
  group("DR-UI-003 late old GET selection race remains bound to new run");
  // Own process restart; ephemeral anti-forgery values are reacquired by reload.
  await select(run.runId);
  uiData = data;
  await visible("64.2", "64.2", 0);
  const savedDigest = data.reportDraft.snapshot.canonicalContentDigest;
  if (ownsHost()) {
    await stopHost();
    await launchHost();
    await page.reload();
    await draftSection.waitFor();
    await visible("64.2", "64.2", 0);
    eq(
      (await analysis(run.runId)).reportDraft.snapshot.canonicalContentDigest,
      savedDigest,
      "actual process restart preserves saved source-derived current draft",
    );
  }
  group("DR-UI-004 actual process restart/reload reproducibility");
  await keyboard("Technical draft");
  await draftSection
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  async function scan(label) {
    await page.evaluate(axe.source);
    const r = await page.evaluate(() =>
      window.axe.run(document, {
        runOnly: {
          type: "tag",
          values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
        },
      }),
    );
    accessibility.push({
      label,
      version: r.testEngine.version,
      passes: r.passes.length,
      violations: r.violations.map((v) => v.id),
      incomplete: r.incomplete.map((v) => v.id),
    });
    eq(r.violations, [], `axe confirmed ${label} violations`);
  }
  await scan("desktop expanded technical draft");
  await page.screenshot({
    path: new URL("./desktop-viewport.png", import.meta.url).pathname,
  });
  await page.screenshot({
    path: new URL("./desktop-draft.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: new URL("./mobile-draft.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 800 });
  ok(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "320px expanded draft no whole-page overflow",
  );
  await scan("320px expanded technical draft");
  await page.screenshot({
    path: new URL("./reflow-viewport.png", import.meta.url).pathname,
  });
  await page.screenshot({
    path: new URL("./reflow-draft.png", import.meta.url).pathname,
    fullPage: true,
  });
  await keyboard("Markdown preview");
  await draftSection
    .getByLabel("Structured Markdown text", { exact: true })
    .focus();
  ok(
    await draftSection
      .getByLabel("Structured Markdown text", { exact: true })
      .evaluate((n) => n === document.activeElement),
    "large inert Markdown is keyboard focusable",
  );
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  ok(
    await draftSection
      .getByRole("heading", {
        name: "Structured Markdown text preview",
        exact: true,
      })
      .isVisible(),
    "forced-colors native preview available",
  );
  eq(errors, [], "no uncaught browser errors");
  eq(dialogs, [], "no code executed");
  eq(external, [], "no source-value external resource loads");
  eq(
    await draftSection
      .getByRole("button", { name: /publish|export|download|share/i })
      .count(),
    0,
    "bounded preview exposes no publication/export controls",
  );
  group(
    "DR-UI-005 desktop390320, expanded captured evidence, axe and native focus engineering checks",
  );
  report = {
    status: "PASS",
    assertions,
    groups,
    accessibility,
    browser: browser.version(),
    node: process.version,
    ownedHostRestartExecuted: ownsHost(),
    substitutionCases: substitutions.map(([name]) => name),
    limitations: [
      "Fixed synthetic scope only; no durable ReportVersion, publication/export/customer authority",
      "Full supported Windows/NVDA/Narrator/manual WCAG acceptance NOT VERIFIED",
      ...accessibility
        .filter((a) => a.incomplete.length)
        .map(
          (a) =>
            `axe ${a.label} incomplete:${a.incomplete.join(",")}; NOT VERIFIED`,
        ),
    ],
  };
} catch (e) {
  report = {
    status: "FAIL",
    assertions,
    groups,
    error: e.stack,
    accessibility,
  };
  console.error(e.stack);
  process.exitCode = 1;
} finally {
  await page?.close();
  await context?.close();
  await browser?.close();
  await api?.dispose();
  await stopHost();
  await writeFile(
    new URL("./execution.json", import.meta.url),
    JSON.stringify(report, null, 2) + "\n",
  );
}
