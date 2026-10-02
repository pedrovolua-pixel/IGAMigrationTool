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
  accessibility = [],
  errors = [],
  dialogs = [],
  external = [];
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
// Independent standard JSON canonical writer. Match the settled web-JSON escaping,
// never call the server builder/helper or use returned hashes as expected values.
function string(s) {
  return (
    '"' +
    JSON.stringify(s)
      .slice(1, -1)
      .replace(/\\"/g, "\\u0022")
      .replace(
        /['<>&+`\u007f-\uffff]/g,
        (c) =>
          "\\u" + c.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0"),
      ) +
    '"'
  );
}
function canonical(v) {
  if (typeof v === "string") return string(v);
  if (Array.isArray(v)) return "[" + v.map(canonical).join(",") + "]";
  if (v !== null && typeof v === "object")
    return (
      "{" +
      Object.keys(v)
        .sort()
        .map((k) => string(k) + ":" + canonical(v[k]))
        .join(",") +
      "}"
    );
  return JSON.stringify(v);
}
const hostile =
  "## stolen\n<script>alert(1)</script> & [link](https://evil.invalid/a) ![img](https://evil.invalid/x) ```sh\n$(touch /tmp/x)\n=SUM(A1:A2)\t\u202e";
let browser, context, page, api, report, headers;
try {
  await launchHost();
  ok(ownsHost(), "only owned host qualifies for restart evidence");
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
    baseline = "synthetic-analysis-findings-v1",
    profile = "synthetic-review-maturity-equal-v1",
  ) {
    const r = await api.post("/local-demo/v1/runs", {
      headers,
      data: {
        scopeId: "demo-scope",
        baselineId: baseline,
        profileId: profile,
        requestId: randomUUID(),
      },
    });
    eq(r.status(), 201, "actual fixed synthetic run starts");
    return r.json();
  }
  async function analysis(id) {
    const r = await api.get(`/local-demo/v1/runs/${id}/analysis`);
    eq(r.status(), 200, "existing read route only");
    const d = await r.json();
    ok(
      valid(d, schema.$defs.AnalysisDetail, schema),
      "exact strict private DTO shape",
    );
    eq(d.runId, id, "exact requested run binding");
    return d;
  }
  const ready = (id) =>
    until(
      () => analysis(id),
      (d) => d.status === "Ready",
      "saved analysis not Ready",
    );
  async function event(id, finding, kind, fields = {}) {
    const r = await api.post(
      `/local-demo/v1/runs/${id}/findings/${finding.id}/events`,
      {
        headers,
        data: {
          eventId: randomUUID(),
          expectedRevision: finding.revision,
          kind,
          reason: null,
          text: null,
          title: null,
          businessContext: null,
          ...fields,
        },
      },
    );
    eq(r.status(), 200, "approved existing finding review action");
    return ready(id);
  }
  function guidance(d, count = 5, occurrences = 2) {
    const g = d.recommendationGuidance;
    eq(g.status, "Ready", "captured guidance ready");
    eq(g.reasonCode, null, "no inferred fallback");
    const s = g.snapshot,
      b = s.source;
    eq(
      [s.schemaVersion, s.status],
      ["synthetic-recommendation-guidance-v1", "SyntheticUnverified"],
      "literal schema/unverified authority",
    );
    eq(s.findings.length, count, "literal grouped finding count");
    eq(
      [b.runId, b.runRevision, b.runState, b.reviewRunId, b.reviewRunRevision],
      [d.runId, d.runRevision, "Scoring", d.runId, d.runRevision],
      "exact saved and captured review run/revision/state",
    );
    eq(
      b.reviewSnapshotDigest,
      d.reviewSnapshotDigest,
      "single captured review snapshot",
    );
    eq(
      b.scope,
      {
        customerId: "synthetic-customer",
        environmentId: "synthetic-environment",
        projectId: "synthetic-project",
      },
      "fixed synthetic scope",
    );
    const ds = d.reportDraft.snapshot.source;
    for (const k of [
      "runInputDigest",
      "analysisFixtureDigest",
      "analysisContentDigest",
      "savedCoverageDigest",
      "frozenVersions",
      "capabilityLock",
      "analysisLock",
    ])
      eq(b[k], ds[k], `exact shared ${k}`);
    eq(
      s.findings.map((f) => f.findingId),
      s.findings.map((f) => f.findingId).sort(),
      "stable identity order never priority rank",
    );
    const identity = [];
    for (const f of s.findings) {
      const original = d.findings.find((o) => o.id === f.findingId),
        current = d.review.findings.find((o) => o.id === f.findingId);
      eq(
        [
          f.originalTitle,
          f.presentationTitle,
          f.currentState,
          f.businessContext,
          f.findingRevision,
        ],
        [
          original.originalTitle,
          current.title,
          current.state,
          current.businessContext,
          current.revision,
        ],
        "captured original/current presentation/state/context/revision parity",
      );
      eq(
        f.occurrences.length,
        occurrences,
        "literal fixture grouped occurrence count",
      );
      eq(
        f.occurrences.map((o) => o.occurrenceId),
        current.occurrenceIds.slice().sort(),
        "original occurrence links complete",
      );
      eq(
        f.occurrences.map((o) => o.originalDigest).sort(),
        original.originalDigests.slice().sort(),
        "original per-occurrence digest links",
      );
      eq(
        f.options.map((o) => o.optionId),
        ["compare-new-fixture", "inspect-fixture"],
        "literal original complete alternatives in stable identity order",
      );
      eq(
        f.options[0].text,
        "Compare new passing synthetic evidence as a separate run.",
        "literal comparison guidance text",
      );
      eq(
        f.options[0].prerequisites,
        "A new immutable fixture evidence baseline.",
        "literal comparison prerequisites",
      );
      eq(
        f.options[0].risk,
        "A disposition alone cannot validate closure.",
        "literal comparison risk",
      );
      eq(
        f.options[0].recoveryGuidance,
        "Retain the original generated occurrence and comparison history.",
        "literal comparison recovery",
      );
      eq(
        f.options[1].text,
        "Review the synthetic marker and its typed fact before changing anything.",
        "literal inspection guidance text",
      );
      eq(
        f.options[1].prerequisites,
        "Consultant review of the generated original.",
        "literal inspection prerequisites",
      );
      eq(
        f.options[1].risk,
        "Unverified recommendation; do not execute.",
        "literal inspection risk",
      );
      eq(
        f.options[1].recoveryGuidance,
        "Keep the prior evidence baseline; compare a new fixture run.",
        "literal inspection recovery",
      );
      eq(
        f.validationGuidance,
        [
          "Check the exact rule/version, fixture baseline and evidence reference.",
          "Confirm the known marker count is zero in new passing evidence; never overwrite this original.",
        ],
        "literal ordered full validation guidance",
      );
      eq(
        f.guidanceReferences,
        [`fixture-guidance:${f.ruleId}:v1`],
        "literal authoritative fixture references",
      );
      for (const o of f.options) {
        eq(o.status, "Unverified", "no finding disposition validates advice");
        const bytes = `{"findingId":"${f.findingId}","optionId":"${o.optionId}","runId":"${d.runId}","scope":{"customerId":"synthetic-customer","environmentId":"synthetic-environment","projectId":"synthetic-project"}}`;
        eq(
          o.scopedOptionId,
          sha(bytes),
          "independent literal scope/run/group/option canonical ID hash",
        );
        identity.push(o.scopedOptionId);
      }
    }
    eq(
      new Set(identity).size,
      count * 2,
      "repeated catalog IDs distinct across groups",
    );
    const { contentDigest, ...envelope } = s;
    eq(
      contentDigest,
      sha(canonical(envelope)),
      "independent entire source/schema/status/content canonical SHA",
    );
    eq(s.warnings.length, 4, "four explicit warnings");
    eq(
      s.unavailableSections.length,
      5,
      "five explicit unavailable capabilities",
    );
    ok(
      !JSON.stringify(s).includes("csrfToken") &&
        !JSON.stringify(s).includes("observedAtDatabaseUtc"),
      "transport token/database observation excluded",
    );
    return s;
  }
  const run = await start();
  let data = await ready(run.runId);
  const initial = guidance(data),
    initialBytes = JSON.stringify(initial);
  eq(
    [
      data.provisional.display,
      data.publishableCurrent.display,
      data.quality.proposedReviewUnits,
      data.maturity.level,
    ],
    ["44.2", "78.3", 4, "Managed"],
    "unchanged independent health/quality/maturity literals",
  );
  const originals = initial.findings.map((f) => [
    f.findingId,
    f.originalTitle,
    f.occurrences,
    f.options,
  ]);
  const critical = data.findings.find((f) => f.severity === "Critical").id,
    high = data.findings.find((f) => f.severity === "High").id;
  data = await event(
    run.runId,
    data.review.findings.find((f) => f.id === critical),
    "Confirm",
  );
  const confirmed = guidance(data);
  eq(
    [
      data.provisional.display,
      data.publishableCurrent.display,
      data.quality.proposedReviewUnits,
    ],
    ["44.2", "39.2", 2],
    "unchanged independent Confirm score literals",
  );
  data = await event(
    run.runId,
    data.review.findings.find((f) => f.id === high),
    "Reject",
    { reason: "Independent synthetic rejection explanation" },
  );
  const rejected = guidance(data);
  eq(
    [
      data.provisional.display,
      data.publishableCurrent.display,
      data.quality.proposedReviewUnits,
    ],
    ["64.2", "64.2", 0],
    "unchanged independent Reject score literals",
  );
  data = await event(
    run.runId,
    data.review.findings.find((f) => f.id === critical),
    "EditPresentation",
    {
      title: "<img src=x onerror=alert(2)> Edited synthetic title",
      businessContext: hostile,
    },
  );
  const edited = guidance(data);
  eq(
    edited.findings.map((f) => [
      f.findingId,
      f.originalTitle,
      f.occurrences,
      f.options,
    ]),
    originals,
    "actual saved dispositions/presentation preserve all originals and unverified scoped options",
  );
  eq(
    new Set([
      initial.contentDigest,
      confirmed.contentDigest,
      rejected.contentDigest,
      edited.contentDigest,
    ]).size,
    4,
    "current review creates new complete guidance value",
  );
  eq(
    JSON.stringify(initial),
    initialBytes,
    "earlier returned value remains immutable",
  );
  group(
    "RG-API-001 literal structured options/scoped IDs/canonical digest/captured review/immutable originals",
  );
  let gapsRun, healthyRun;
  for (const [name, p, c, count, level] of [
    ["healthy", "100.0", "100.0", 0, "Initial"],
    ["gaps", null, null, 0, "Initial"],
    ["mixed", "66.7", "89.2", 5, "Developing"],
  ]) {
    const r = await start(`synthetic-analysis-${name}-v1`),
      d = await ready(r.runId);
    guidance(d, count, name === "mixed" ? 1 : 2);
    eq(
      [d.provisional.display, d.publishableCurrent.display, d.maturity.level],
      [p, c, level],
      "healthy/gaps/mixed independent unchanged score/maturity literals",
    );
    if (name === "gaps") {
      gapsRun = r;
      eq(d.quality.gapUnits, 10, "ten genuine gaps never healthy/advice");
    }
    if (name === "healthy") healthyRun = r;
  }
  const weighted = await start(
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-operations-v1",
    ),
    w = await ready(weighted.runId);
  guidance(w);
  eq(
    [w.provisional.display, w.publishableCurrent.display],
    ["61.3", "78.3"],
    "weighted independent old score literals",
  );
  for (const profile of [
    "profile-standard",
    "profile-comparison",
    "synthetic-analysis-equal-v1",
    "synthetic-analysis-operations-v1",
  ]) {
    const r = await start(
      profile.startsWith("profile-")
        ? "baseline-complete"
        : "synthetic-analysis-findings-v1",
      profile,
    );
    const d = await until(
      () => analysis(r.runId),
      (d) => d.status === "Ready" || d.reasonCode === "coverage_only_fixture",
      "historical response never settled",
    );
    eq(
      d.recommendationGuidance,
      null,
      "all four older profiles opt out of guidance",
    );
    eq(d.reportDraft, null, "all four older profiles remain draft-v1 null");
  }
  group(
    "RG-API-002 healthy/gaps/mixed/weighted and all four older-profile null compatibility",
  );
  const uiRun = await start();
  let uiData = await ready(uiRun.runId);
  guidance(uiData);
  await page.goto(base);
  const section = page.locator(".recommendation-guidance");
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
      "fresh history not rendered",
    );
    return d;
  }
  async function select(id) {
    const d = await refresh(),
      i = d.runs.findIndex((r) => r.runId === id);
    ok(i >= 0, "saved row exists");
    await page
      .getByRole("region", { name: "Saved run history table" })
      .locator("tbody tr")
      .nth(i)
      .getByRole("button")
      .click();
    await section.waitFor();
    await until(
      () => section.locator(".guidance-bindings dt").allTextContents(),
      (v) => v.length > 0,
      "source binding not rendered",
    );
    await until(
      () => section.locator(".guidance-bindings").textContent(),
      (v) => v.includes(id),
      "selected source remains stale",
    );
  }
  async function disclose(locator) {
    const summary = locator.locator(":scope > summary");
    await summary.focus();
    await page.keyboard.press("Enter");
    eq(
      await locator.getAttribute("open"),
      "",
      "native keyboard disclosure opens",
    );
    ok(
      await summary.evaluate((n) => n === document.activeElement),
      "native disclosure retains focus",
    );
  }
  async function visible(d) {
    eq(
      await section.locator(".guidance-count").textContent(),
      `${d.recommendationGuidance.snapshot.findings.length * 2} unverified options across ${d.recommendationGuidance.snapshot.findings.length} findings. Options follow stable identity order; this is not a priority or effort ranking.`,
      "visible exact count and boundary",
    );
    for (const [i, f] of d.recommendationGuidance.snapshot.findings.entries()) {
      const card = section.locator(".guidance-finding").nth(i);
      await card.evaluate((n) => (n.open = true));
      eq(
        await card.locator(".guidance-current dd").allTextContents(),
        [
          f.presentationTitle,
          f.currentState,
          String(f.findingRevision),
          f.businessContext || "No business context supplied.",
        ],
        "visible exact current title/state/revision/context",
      );
      eq(
        await card.locator(".guidance-original > dl dd").allTextContents(),
        [
          f.originalTitle,
          f.findingId,
          f.initialState,
          `${f.ruleId} · ${f.ruleVersion}`,
          f.categoryId,
          f.severity,
          f.rootCause,
        ],
        "visible exact immutable original/root/rule/category",
      );
      await card
        .locator(".guidance-original details")
        .evaluate((n) => (n.open = true));
      eq(
        await card.locator("tbody tr").count(),
        f.occurrences.length,
        "all original occurrence rows visible",
      );
      for (const [j, o] of f.occurrences.entries()) {
        const row = card.locator("tbody tr").nth(j);
        eq(
          await row.locator("th code").textContent(),
          o.occurrenceId,
          "visible stable original occurrence",
        );
        eq(
          await row.locator("dd").allTextContents(),
          [
            o.objectId,
            o.objectType,
            o.moduleId,
            o.originalDigest,
            o.evidenceReference,
          ],
          "visible exact full provenance/affected scope",
        );
      }
      for (const [j, o] of f.options.entries()) {
        const option = card.locator(".guidance-option").nth(j);
        await option.evaluate((n) => (n.open = true));
        eq(
          await option.locator("dd").allTextContents(),
          [
            o.scopedOptionId,
            o.optionId,
            "Unverified",
            o.text,
            o.prerequisites,
            o.risk,
            o.recoveryGuidance,
          ],
          "visible complete original unverified option fields",
        );
      }
      await card
        .locator(":scope > details")
        .last()
        .evaluate((n) => (n.open = true));
      eq(
        await card
          .locator(":scope > details")
          .last()
          .locator("li")
          .allTextContents(),
        [
          ...f.validationGuidance,
          ...f.guidanceReferences,
          ...f.assumptions,
          ...f.limitations,
        ],
        "visible complete ordered validation/fixture references/assumptions/limitations",
      );
    }
  }
  await select(uiRun.runId);
  await disclose(section.locator(".guidance-finding").first());
  await disclose(section.locator(".guidance-option").first());
  await visible(uiData);
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
    "keyboard finding Confirm not persisted",
  );
  await until(
    () => section.locator(".guidance-current dd").allTextContents(),
    (v) => v.includes("Confirmed"),
    "captured guidance not refreshed after keyboard Confirm",
  );
  eq(
    await page.evaluate(() => document.activeElement?.id),
    "analysis-heading",
    "review refresh preserves intentional focus",
  );
  await visible(uiData);
  eq(
    await section.getByRole("button").count(),
    0,
    "guidance has no advice review/approval/execution/task/export buttons",
  );
  group(
    "RG-UI-001 keyboard disclosure/actual finding review/focus/current-state/unverified complete visible options",
  );
  await select(gapsRun.runId);
  eq(
    await section.locator(".guidance-finding").count(),
    0,
    "all-gap visible guidance empty",
  );
  ok(
    (await section.locator(".guidance-empty").textContent()).includes(
      "absent evidence is not proof of health",
    ),
    "gap absence explicitly does not infer health/advice",
  );
  await select(run.runId);
  await visible(data);
  eq(
    await section.locator("a,img,script,iframe,form").count(),
    0,
    "actual hostile saved title/context produces only inert text",
  );
  eq(dialogs, [], "no hostile dialogs");
  eq(external, [], "no hostile source-value resource loads");
  const target = `**/local-demo/v1/runs/${run.runId}/analysis`;
  const substitutions = [
    [
      "schema",
      (d) => (d.recommendationGuidance.snapshot.schemaVersion = "unknown"),
    ],
    ["status", (d) => (d.recommendationGuidance.snapshot.status = "Reviewed")],
    [
      "content digest shape",
      (d) => (d.recommendationGuidance.snapshot.contentDigest = "bad"),
    ],
    [
      "run",
      (d) => (d.recommendationGuidance.snapshot.source.runId = randomUUID()),
    ],
    [
      "run revision",
      (d) => d.recommendationGuidance.snapshot.source.runRevision++,
    ],
    [
      "state",
      (d) => (d.recommendationGuidance.snapshot.source.runState = "Completed"),
    ],
    [
      "review run",
      (d) =>
        (d.recommendationGuidance.snapshot.source.reviewRunId = randomUUID()),
    ],
    [
      "review revision",
      (d) => d.recommendationGuidance.snapshot.source.reviewRunRevision++,
    ],
    [
      "review digest",
      (d) =>
        (d.recommendationGuidance.snapshot.source.reviewSnapshotDigest =
          "0".repeat(64)),
    ],
    [
      "scope",
      (d) =>
        (d.recommendationGuidance.snapshot.source.scope.customerId = "other"),
    ],
    [
      "baseline",
      (d) => (d.recommendationGuidance.snapshot.source.baselineId = "other"),
    ],
    [
      "profile",
      (d) =>
        (d.recommendationGuidance.snapshot.source.profileId =
          "synthetic-analysis-equal-v1"),
    ],
    [
      "input",
      (d) =>
        (d.recommendationGuidance.snapshot.source.runInputDigest = "0".repeat(
          64,
        )),
    ],
    [
      "analysis fixture",
      (d) =>
        (d.recommendationGuidance.snapshot.source.analysisFixtureDigest =
          "0".repeat(64)),
    ],
    [
      "analysis content",
      (d) =>
        (d.recommendationGuidance.snapshot.source.analysisContentDigest =
          "0".repeat(64)),
    ],
    [
      "coverage",
      (d) =>
        (d.recommendationGuidance.snapshot.source.savedCoverageDigest =
          "0".repeat(64)),
    ],
    [
      "capability",
      (d) =>
        (d.recommendationGuidance.snapshot.source.capabilityLock.lockDigest =
          "0".repeat(64)),
    ],
    [
      "frozen version",
      (d) =>
        (d.recommendationGuidance.snapshot.source.frozenVersions.applicationVersion =
          "other"),
    ],
    [
      "analysis lock",
      (d) =>
        (d.recommendationGuidance.snapshot.source.analysisLock.catalogDigest =
          "0".repeat(64)),
    ],
    [
      "finding group duplicate",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[1] = structuredClone(
          d.recommendationGuidance.snapshot.findings[0],
        )),
    ],
    [
      "original title",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].originalTitle = "other"),
    ],
    [
      "current title",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].presentationTitle =
          "other"),
    ],
    [
      "current state",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].currentState =
          d.recommendationGuidance.snapshot.findings[0].currentState ===
          "Confirmed"
            ? "Rejected"
            : "Confirmed"),
    ],
    [
      "context",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].businessContext =
          "other"),
    ],
    [
      "finding revision",
      (d) => d.recommendationGuidance.snapshot.findings[0].findingRevision++,
    ],
    [
      "original digest",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].occurrences[0].originalDigest =
          "0".repeat(64)),
    ],
    [
      "evidence reference",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].occurrences[0].evidenceReference =
          "other"),
    ],
    [
      "object",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].occurrences[0].objectId =
          "other"),
    ],
    [
      "object type",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].occurrences[0].objectType =
          "other"),
    ],
    [
      "module",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].occurrences[0].moduleId =
          "other"),
    ],
    [
      "option reviewed",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[0].status =
          "Reviewed"),
    ],
    [
      "duplicate option",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[1] =
          structuredClone(
            d.recommendationGuidance.snapshot.findings[0].options[0],
          )),
    ],
    [
      "option text",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[0].text =
          "other"),
    ],
    [
      "recovery",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[0].recoveryGuidance =
          "other"),
    ],
    [
      "validation",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].validationGuidance[0] =
          "other"),
    ],
    [
      "references",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].guidanceReferences[0] =
          "other"),
    ],
    [
      "assumptions",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].assumptions[0] =
          "other"),
    ],
    [
      "limitations",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].limitations[0] =
          "other"),
    ],
  ];
  substitutions.push(
    [
      "scoped option identity",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[0].scopedOptionId =
          "0".repeat(64)),
    ],
    [
      "unknown original option identity",
      (d) =>
        (d.recommendationGuidance.snapshot.findings[0].options[0].optionId =
          "other"),
    ],
    [
      "paired original digests",
      (d) => {
        const [a, b] =
          d.recommendationGuidance.snapshot.findings[0].occurrences;
        [a.originalDigest, b.originalDigest] = [
          b.originalDigest,
          a.originalDigest,
        ];
      },
    ],
    [
      "paired evidence references",
      (d) => {
        const [a, b] =
          d.recommendationGuidance.snapshot.findings[0].occurrences;
        [a.evidenceReference, b.evidenceReference] = [
          b.evidenceReference,
          a.evidenceReference,
        ];
      },
    ],
    [
      "paired occurrence identities",
      (d) => {
        const [a, b] =
          d.recommendationGuidance.snapshot.findings[0].occurrences;
        [a.occurrenceId, b.occurrenceId] = [b.occurrenceId, a.occurrenceId];
      },
    ],
    [
      "option payload identity pairing",
      (d) => {
        const [a, b] = d.recommendationGuidance.snapshot.findings[0].options;
        for (const key of ["text", "prerequisites", "risk", "recoveryGuidance"])
          [a[key], b[key]] = [b[key], a[key]];
      },
    ],
  );
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
    eq(await section.count(), 0, `fail closed ${name}`);
    await page.unroute(target);
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .focus();
    await page.keyboard.press("Enter");
    await section.waitFor();
    await until(
      () => page.evaluate(() => document.activeElement?.id),
      (v) => v === "analysis-heading",
      "same-revision retry focus not restored",
    );
    eq(
      (await analysis(run.runId)).recommendationGuidance.snapshot.contentDigest,
      edited.contentDigest,
      "retry reloads exact coherent current guidance",
    );
  }
  await page.route(target, async (route) => {
    const r = await route.fetch(),
      d = await r.json();
    d.recommendationGuidance = {
      status: "Unavailable",
      reasonCode: "guidance_source_unavailable",
      snapshot: null,
    };
    await route.fulfill({ response: r, json: d });
  });
  await page.reload();
  await section.waitFor();
  eq(
    await section.locator(".guidance-finding").count(),
    0,
    "typed unavailable guidance has no stale fallback",
  );
  ok(
    (await section.textContent()).includes(
      "No advice is inferred or substituted",
    ),
    "typed unavailable reason disclosed",
  );
  await page.unroute(target);
  await page.reload();
  await section.waitFor();
  group(
    "RG-UI-002 source/content substitution refusal, focused same-revision retry and typed unavailable no fallback",
  );
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
  await select(healthyRun.runId);
  const focus = await page.evaluate(() => ({
    id: document.activeElement?.id,
    text: document.activeElement?.textContent,
  }));
  release();
  await page.unroute(target);
  await page.waitForTimeout(150);
  eq(
    await section.locator(".guidance-finding").count(),
    0,
    "late old GET cannot replace new empty healthy guidance",
  );
  eq(
    await page.evaluate(() => ({
      id: document.activeElement?.id,
      text: document.activeElement?.textContent,
    })),
    focus,
    "late old GET cannot steal selection focus",
  );
  group("RG-UI-003 held old response selection/abort/focus fence");
  // Independently hold the browser digest await after the GET already arrived.
  // Selecting the empty healthy run must win even when old async work finishes.
  await page.addInitScript(() => {
    if (sessionStorage.getItem("iga-v7-delay-digest") !== "once") return;
    sessionStorage.removeItem("iga-v7-delay-digest");
    window.__igaV7DigestWaiters = [];
    window.__igaV7HoldDigest = true;
    const originalDigest = SubtleCrypto.prototype.digest;
    Object.defineProperty(SubtleCrypto.prototype, "digest", {
      configurable: true,
      value: async function (...args) {
        if (window.__igaV7HoldDigest)
          await new Promise((resolve) =>
            window.__igaV7DigestWaiters.push(resolve),
          );
        return originalDigest.apply(this, args);
      },
    });
  });
  await select(run.runId);
  await page.evaluate(() =>
    sessionStorage.setItem("iga-v7-delay-digest", "once"),
  );
  await page.reload();
  await until(
    () => page.evaluate(() => window.__igaV7DigestWaiters?.length ?? 0),
    (n) => n > 0,
    "guidance async digest await was not held",
  );
  await select(healthyRun.runId);
  const digestFocus = await page.evaluate(() => ({
    id: document.activeElement?.id,
    text: document.activeElement?.textContent,
  }));
  await page.evaluate(() => {
    window.__igaV7HoldDigest = false;
    window.__igaV7DigestWaiters.splice(0).forEach((resolve) => resolve());
  });
  await page.waitForTimeout(150);
  eq(
    await section.locator(".guidance-finding").count(),
    0,
    "completed old async digest cannot replace selected healthy guidance",
  );
  eq(
    await page.evaluate(() => ({
      id: document.activeElement?.id,
      text: document.activeElement?.textContent,
    })),
    digestFocus,
    "completed old async digest cannot steal current selection focus",
  );
  group("RG-UI-003B post-GET async digest selection/abort/focus fence");
  await select(run.runId);
  await stopHost();
  await launchHost();
  await page.reload();
  await section.waitFor();
  await visible(data);
  eq(
    (await analysis(run.runId)).recommendationGuidance.snapshot.contentDigest,
    edited.contentDigest,
    "actual owned process restart reproduces current saved-source guidance",
  );
  group(
    "RG-UI-004 actual owned host process restart and saved read reproducibility",
  );
  // Exercise hostile option text through the real coherence guard without changing
  // the fixed fixture catalog: both structured text and existing flattened view
  // are consistently substituted, while all actual bindings remain unchanged.
  await page.route(target, async (route) => {
    const r = await route.fetch(),
      d = await r.json(),
      f = d.recommendationGuidance.snapshot.findings[0],
      original = d.findings.find((x) => x.id === f.findingId);
    for (const o of f.options) {
      o.text = hostile;
      o.prerequisites = hostile;
      o.risk = hostile;
      o.recoveryGuidance = hostile;
    }
    original.recommendations = f.options.map(
      (o) =>
        `${o.text} Prerequisites: ${o.prerequisites} Risk: ${o.risk} Recovery: ${o.recoveryGuidance}`,
    );
    const { contentDigest, ...envelope } = d.recommendationGuidance.snapshot;
    d.recommendationGuidance.snapshot.contentDigest = sha(canonical(envelope));
    await route.fulfill({ response: r, json: d });
  });
  await page.reload();
  await section.waitFor();
  await section
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  ok(
    (await section.locator(".guidance-option").first().textContent()).includes(
      hostile,
    ),
    "hostile structured option text remains exact inert visible data",
  );
  eq(
    await section.locator("a,img,script,iframe,form").count(),
    0,
    "consistent hostile option input creates no active nodes",
  );
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
  await scan("desktop expanded guidance");
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({
    path: new URL("./desktop-viewport.png", import.meta.url).pathname,
  });
  await page.screenshot({
    path: new URL("./desktop-guidance.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  ok(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "390px expanded guidance reflow",
  );
  await page.screenshot({
    path: new URL("./mobile-guidance.png", import.meta.url).pathname,
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 800 });
  ok(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "320px expanded guidance reflow",
  );
  await scan("320px expanded guidance");
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({
    path: new URL("./reflow-viewport.png", import.meta.url).pathname,
  });
  await page.screenshot({
    path: new URL("./reflow-guidance.png", import.meta.url).pathname,
    fullPage: true,
  });
  const scroller = section.locator(".guidance-table-scroll").first();
  await scroller.focus();
  ok(
    await scroller.evaluate((n) => n === document.activeElement),
    "original provenance table scroll region keyboard focusable",
  );
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  ok(
    await section
      .getByRole("heading", { name: "Recommendation guidance", exact: true })
      .isVisible(),
    "forced colors semantic guidance remains available",
  );
  eq(errors, [], "no uncaught browser errors");
  eq(dialogs, [], "no text execution dialogs");
  eq(external, [], "no hostile external loads");
  eq(
    await section.getByRole("button").count(),
    0,
    "no review approval execution task conversion export control introduced",
  );
  group(
    "RG-UI-005 hostile structured text, desktop390320, native table focus, forced colors and axe engineering evidence",
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
      "Fixed synthetic read-only unverified guidance; no advice approval, task/export, execution or customer authority",
      "Full supported Windows/NVDA/Narrator/manual WCAG/CSV/PDF acceptance NOT VERIFIED",
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
