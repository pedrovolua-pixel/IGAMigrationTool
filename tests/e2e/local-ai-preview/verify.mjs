import { showAssessments } from "../consultant-demo/navigation.mjs";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { readFile, writeFile, readdir, mkdir } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import { spawn } from "node:child_process";
import { once } from "node:events";
import * as expected from "./expected.mjs";

const directory = path.dirname(fileURLToPath(import.meta.url));
const base = "http://127.0.0.1:5183";
const workdir = process.env.IGA_HOST_WORKING_DIRECTORY;
const hostDll = process.env.IGA_HOST_DLL;
assert.ok(path.isAbsolute(workdir ?? "") && path.isAbsolute(hostDll ?? ""));
assert.match(
  process.env.IGA_SYNTHETIC_DATABASE ?? "",
  /Database=iga_synthetic_v10(?:;|$)/,
);
assert.match(
  process.env.IGA_SYNTHETIC_DATABASE ?? "",
  /Host=127\.0\.0\.1(?:;|$)/,
);
assert.match(process.env.IGA_SYNTHETIC_DATABASE ?? "", /Port=55433(?:;|$)/);
const playwrightModule = process.env.IGA_PLAYWRIGHT_MODULE;
assert.ok(path.isAbsolute(playwrightModule ?? ""));
const { chromium, request } = await import(
  pathToFileURL(playwrightModule).href
);
let checks = 0,
  last = "initialize",
  host,
  browser,
  context,
  api,
  report;
const groups = [],
  blocked = [],
  faults = [],
  dialogs = [],
  shots = [],
  known = new Set();
let mutation = null,
  runMutation = null,
  delay = null;
function check(condition, code) {
  last = code;
  assert.ok(condition, code);
  checks++;
}
function equal(actual, want, code) {
  last = code;
  assert.deepEqual(actual, want, code);
  checks++;
}
function group(code) {
  groups.push(code);
  console.log("PASS " + code);
}
async function until(read, predicate, code, timeout = 30000) {
  last = code;
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 80));
  }
  throw new Error(code);
}
async function launch() {
  check(!host, "own-host-single-process");
  host = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [hostDll, "--synthetic-local-demo"],
    {
      cwd: workdir,
      env: { ...process.env },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  host.on("error", () => faults.push("owned-host-spawn-error"));
  // Never retain host logs: exceptions may contain fixture fields.
  host.stdout.on("data", () => {});
  host.stderr.on("data", () => {});
  await until(
    async () => {
      check(
        host.exitCode === null &&
          host.signalCode === null &&
          faults.length === 0,
        "own-host-live",
      );
      try {
        return (await fetch(base + "/local-demo/v1/catalog")).status;
      } catch {
        return 0;
      }
    },
    (x) => x === 200,
    "own-host-ready",
  );
}
async function stop() {
  if (!host) return;
  const child = host;
  host = null;
  if (child.exitCode !== null || child.signalCode !== null || !child.pid)
    return;
  const ended = once(child, "exit");
  child.kill("SIGTERM");
  const timer = setTimeout(() => child.kill("SIGKILL"), 8000);
  try {
    await ended;
  } finally {
    clearTimeout(timer);
  }
}
const assets = new Set(["/"]);
async function inventory(folder, relative = "") {
  for (const entry of await readdir(folder, { withFileTypes: true })) {
    const next = path.posix.join(relative, entry.name);
    if (entry.isDirectory())
      await inventory(path.join(folder, entry.name), next);
    else assets.add("/" + next);
  }
}
await inventory(path.join(workdir, "src/web/dist"));
async function screenshot(page, name) {
  const absolute = path.join(directory, name + ".png");
  await page.screenshot({ path: absolute, fullPage: true });
  shots.push(absolute);
}
const read = async (id) => {
  const response = await api.get("/local-demo/v1/runs/" + id);
  equal(response.status(), 200, "actual-saved-run-read");
  return response.json();
};
const analysis = async (id) => {
  const response = await api.get("/local-demo/v1/runs/" + id + "/analysis");
  equal(response.status(), 200, "actual-saved-analysis-read");
  return response.json();
};
async function start(baselineId, profileId, headers) {
  const response = await api.post("/local-demo/v1/runs", {
    headers,
    data: {
      scopeId: "demo-scope",
      baselineId,
      profileId,
      requestId: randomUUID(),
    },
  });
  equal(response.status(), 201, "actual-owned-API-start");
  const run = await response.json();
  known.add(run.runId);
  return until(
    () => read(run.runId),
    (x) => x.state === "Scoring",
    "actual-worker-complete",
  );
}
const region = (page) =>
  page.getByRole("region", {
    name: "Offline simulated configuration response",
    exact: true,
    includeHidden: true,
  });
async function select(page, id) {
  await page.evaluate(
    (value) => sessionStorage.setItem("iga.synthetic.selected-run", value),
    id,
  );
  await page.reload();
  await showAssessments(page);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
}
async function showProposedFindings(page) {
  last = "open-proposed-findings-tab";
  await page
    .getByRole("tablist", { name: "AI workspace sections", exact: true })
    .getByRole("tab", { name: "Proposed findings", exact: true })
    .click();
}
async function shown(page, id) {
  await showProposedFindings(page);
  last = "await-visible-preview-region";
  await region(page).waitFor();
  await until(
    () => region(page).locator("details dd").first().textContent(),
    (value) => value === id,
    "actual-selected-source-visible",
  );
}
async function fullVisible(page, run, detail) {
  const preview = region(page),
    snapshot = detail.aiPreview.snapshot;
  await shown(page, run.runId);
  equal(
    await preview.locator(".ai-preview-warning strong").textContent(),
    "Proposed · Untrusted",
    "visible-proposed-untrusted",
  );
  equal(
    await preview.getByText(expected.disclaimer, { exact: true }).count(),
    1,
    "visible-fixed-disclaimer",
  );
  equal(
    await preview
      .locator(
        "a,button,input,iframe,script,img,svg,object,embed,form,audio,video,style",
      )
      .count(),
    0,
    "no-preview-actions-or-active-nodes",
  );
  equal(
    await preview
      .locator("*")
      .evaluateAll(
        (nodes) =>
          nodes.filter((node) =>
            Array.from(node.attributes).some(
              (attribute) =>
                /^on/i.test(attribute.name) ||
                ["src", "href", "srcdoc", "style"].includes(attribute.name),
            ),
          ).length,
      ),
    0,
    "no-preview-injected-attributes",
  );
  equal(
    await page.evaluate(() => window.fixtureExecuted),
    undefined,
    "hostile-script-inert",
  );
  const summary = preview.getByText("Captured preview source and digests", {
    exact: true,
  });
  await summary.focus();
  await page.keyboard.press("Enter");
  check(
    await summary.evaluate((node) => node.parentElement.open),
    "keyboard-source-disclosure",
  );
  const oracle = expected.full(run, snapshot.source.baselineDigest);
  const rows = [
    ["Run ID", run.runId],
    ["Run revision", String(run.revision)],
    ["Baseline", expected.baseline],
    ["Profile", run.selection.profileId],
    ["Complete frozen input digest", detail.aiPreview.runInputDigest],
    ["Complete offline fixture digest", detail.aiPreview.fixtureDigest],
    ["Customer", "synthetic-customer"],
    ["Project", "synthetic-project"],
    ["Environment", "synthetic-environment"],
    ["Configuration template digest", snapshot.source.baselineDigest],
    ["Source profile digest", detail.aiPreview.runInputDigest],
    ["Normalization version", "fixture-normalization-v1"],
    ["Redaction version", "fixture-redaction-v1"],
    ["Prompt version", "fixture-prompt-v1"],
    ["Packet digest", expected.hash(oracle.packet)],
    ["Proposal digest", expected.hash(oracle.proposal)],
    ["Canonical preview content digest", expected.hash(oracle.preview)],
    ["Preview schema", "synthetic-ai-preview-v1"],
    ["Preview status", "Proposed"],
  ];
  equal(
    await preview
      .locator("dl > div")
      .evaluateAll((nodes) =>
        nodes.map((node) => [
          node.querySelector("dt").textContent,
          node.querySelector("dd").textContent,
        ]),
      ),
    rows,
    "every-exact-visible-source-field",
  );
  const values = expected.proposals(run.selection.profileId === expected.empty);
  equal(
    await preview.locator("article").count(),
    values.length,
    "visible-full-proposal-count",
  );
  if (!values.length) {
    equal(
      await preview.getByRole("status").textContent(),
      "No proposals were returned. This does not establish healthy or complete assessment coverage.",
      "empty-output-explicit-no-health",
    );
    return;
  }
  const article = preview.locator("article").first();
  equal(
    await article.locator("h4").textContent(),
    "Proposal proposal-01",
    "visible-proposal-id",
  );
  for (const [label, category] of [
    ["Facts", "facts"],
    ["Inferences", "inferences"],
    ["Assumptions", "assumptions"],
    ["Suggestions", "suggestions"],
  ]) {
    const section = article
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: label, exact: true }) });
    const actual = await section.locator("ol > li").evaluateAll((nodes) =>
      nodes.map((node) => ({
        text: node.querySelector("p").textContent,
        evidenceIds: Array.from(node.querySelectorAll("ul")[0].children).map(
          (x) => x.textContent,
        ),
        ruleIds: Array.from(node.querySelectorAll("ul")[1].children).map(
          (x) => x.textContent,
        ),
      })),
    );
    equal(
      actual,
      values[0][category],
      "every-visible-" + category + "-ordered-text-citations",
    );
  }
  for (const [label, key] of [
    ["Missing context", "missingContext"],
    ["Conflicting evidence IDs", "conflictingEvidenceIds"],
  ]) {
    const section = article
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: label, exact: true }) });
    equal(
      await section.locator("li").allTextContents(),
      values[0][key],
      "every-visible-" + key,
    );
  }
  equal(
    await article
      .locator("section")
      .filter({
        has: page.getByRole("heading", { name: "Uncertainty", exact: true }),
      })
      .locator("p")
      .textContent(),
    values[0].uncertainty,
    "visible-unresolved-uncertainty",
  );
}
try {
  let listening = false;
  try {
    await fetch(base);
    listening = true;
  } catch {}
  check(!listening, "refuse-existing-shared-host");
  await launch();
  browser = await chromium.launch({
    headless: true,
    ...(process.env.IGA_CHROMIUM
      ? { executablePath: process.env.IGA_CHROMIUM }
      : {}),
  });
  context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    serviceWorkers: "block",
  });
  // Interception is installed before any navigation. Only owned literal assets/read routes and starts are allowed.
  await context.route("**/*", async (route) => {
    const request = route.request(),
      url = new URL(request.url()),
      method = request.method();
    const routeMatch = url.pathname.match(
      /^\/local-demo\/v1\/runs\/([a-f0-9-]{36})(?:\/(analysis|review))?$/,
    );
    const allowed =
      url.origin === base &&
      !url.search &&
      ((method === "GET" &&
        (assets.has(url.pathname) ||
          ["/local-demo/v1/catalog", "/local-demo/v1/runs"].includes(
            url.pathname,
          ) ||
          (routeMatch && known.has(routeMatch[1])))) ||
        (method === "POST" && url.pathname === "/local-demo/v1/runs"));
    if (!allowed) {
      blocked.push("unexpected-request");
      await route.abort();
      return;
    }
    if (
      routeMatch &&
      !routeMatch[2] &&
      runMutation &&
      routeMatch[1] === runMutation.id
    ) {
      const response = await route.fetch();
      const value = await response.json();
      runMutation.change(value);
      await route.fulfill({ response, json: value });
      return;
    }
    if (routeMatch?.[2] === "analysis") {
      if (delay && routeMatch[1] === delay.id) {
        const gate = delay;
        const response = await route.fetch();
        gate.entered();
        await gate.promise;
        try {
          await route.fulfill({ response });
        } catch {}
        return;
      }
      if (mutation && routeMatch[1] === mutation.id) {
        const response = await route.fetch();
        const value = await response.json();
        mutation.change(value);
        await route.fulfill({ response, json: value });
        return;
      }
    }
    await route.continue();
  });
  const page = await context.newPage();
  page.on("pageerror", () => faults.push("page-error"));
  page.on("dialog", async (dialog) => {
    dialogs.push("dialog");
    await dialog.dismiss();
  });
  api = await request.newContext({ baseURL: base });
  const catalogResponse = await api.get("/local-demo/v1/catalog");
  equal(catalogResponse.status(), 200, "actual-catalog-ready");
  const catalog = await catalogResponse.json();
  equal(catalog.profiles.length, 11, "actual-catalog-eleven-profiles");
  equal(
    catalog.profiles.filter(
      (p) => p.id !== "synthetic-review-maturity-planning-tasks-equal-v1",
    ).length,
    10,
    "actual-historical-catalog-ten-profiles",
  );
  equal(catalog.baselines.length, 9, "actual-catalog-nine-baselines");
  const headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const normal = await start(expected.baseline, expected.normal, headers),
    empty = await start(expected.baseline, expected.empty, headers);
  for (const run of [normal, empty]) {
    const detail = await analysis(run.runId),
      snapshot = detail.aiPreview.snapshot,
      oracle = expected.full(run, snapshot.source.baselineDigest);
    equal(
      detail.status,
      "Unavailable",
      "new-offline-preview-no-deterministic-health",
    );
    equal(detail.aiPreview.status, "Ready", "actual-composed-preview-ready");
    equal(
      snapshot.canonicalJson,
      oracle.preview,
      "independent-entire-actual-API-canonical",
    );
    equal(
      snapshot.contentDigest,
      expected.hash(oracle.preview),
      "independent-entire-actual-API-digest",
    );
    equal(
      snapshot.packetDigest,
      expected.hash(oracle.packet),
      "independent-entire-actual-API-packet",
    );
    equal(
      snapshot.proposalDigest,
      expected.hash(oracle.proposal),
      "independent-entire-actual-API-proposal",
    );
    equal(
      snapshot.proposals,
      oracle.proposals,
      "independent-entire-actual-API-DTO",
    );
    equal(run.lockedInputs.length, 16, "dedicated-sixteen-frozen-locks");
    equal(
      run.progress,
      {
        plannedUnits: 2,
        terminalUnits: 2,
        remainingUnits: 0,
        allTerminal: true,
      },
      "actual-two-unit-coverage",
    );
    equal(run.coverageCompletionKind, "Complete", "coverage-only-complete");
  }
  group(
    "V10-BROWSER-001 actual host/PG/catalog/normal-empty/full independent bytes/digests/no-health",
  );
  await page.goto(base);
  await showAssessments(page);
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
  await page
    .getByLabel("Evidence baseline", { exact: true })
    .selectOption(expected.baseline);
  await page
    .getByLabel("Assessment profile", { exact: true })
    .selectOption(expected.normal);
  const started = page.waitForResponse(
    (response) =>
      response.url() === base + "/local-demo/v1/runs" &&
      response.request().method() === "POST",
  );
  await page.getByLabel("Assessment profile", { exact: true }).focus();
  await page.keyboard.press("Tab");
  equal(
    await page.evaluate(() => document.activeElement.textContent),
    "Start synthetic run",
    "keyboard-start-reachable",
  );
  await page.keyboard.press("Enter");
  const uiRun = await (await started).json();
  known.add(uiRun.runId);
  const completed = await until(
    () => read(uiRun.runId),
    (x) => x.state === "Scoring",
    "actual-UI-worker-complete",
  );
  await fullVisible(page, completed, await analysis(uiRun.runId));
  equal(
    await page.locator(".analysis-score-grid").count(),
    0,
    "preview-has-no-health-score-grid",
  );
  await screenshot(page, "desktop-preview");
  await page.setViewportSize({ width: 390, height: 844 });
  await screenshot(page, "mobile-preview");
  await page.setViewportSize({ width: 320, height: 800 });
  check(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "320px-full-page-reflow",
  );
  check(
    await region(page).evaluate(
      (node) => node.scrollWidth <= node.clientWidth + 1,
    ),
    "320px-full-preview-reflow",
  );
  equal(
    await page
      .locator("button:not([disabled]),summary,select:not([disabled])")
      .evaluateAll(
        (nodes) =>
          nodes.filter((node) => {
            if (
              !node.getClientRects().length ||
              !node.checkVisibility() ||
              getComputedStyle(node).visibility === "hidden"
            )
              return false;
            const bounds = node.getBoundingClientRect();
            return bounds.height < 24 || bounds.width < 24;
          }).length,
      ),
    0,
    "24px-enabled-targets",
  );
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  check(await region(page).isVisible(), "forced-colors-preview-visible");
  await screenshot(page, "reflow-preview");
  await page.emulateMedia({
    forcedColors: "none",
    reducedMotion: "no-preference",
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await select(page, empty.runId);
  await fullVisible(page, empty, await analysis(empty.runId));
  await screenshot(page, "empty-preview");
  equal(
    await page.evaluate(() => Object.keys(sessionStorage)),
    ["iga.synthetic.selected-run"],
    "browser-stores-opaque-run-id-only",
  );
  group(
    "V10-BROWSER-002 keyboard start/source/all fields/citations/hostile-Unicode/empty/reflow",
  );
  const beforeNormal = await analysis(normal.runId),
    beforeEmpty = await analysis(empty.runId);
  await stop();
  await launch();
  headers["X-CSRF-TOKEN"] = (
    await (await api.get("/local-demo/v1/catalog")).json()
  ).csrfToken;
  equal(
    (await analysis(normal.runId)).aiPreview,
    beforeNormal.aiPreview,
    "owned-host-restart-same-normal-preview",
  );
  equal(
    (await analysis(empty.runId)).aiPreview,
    beforeEmpty.aiPreview,
    "owned-host-restart-same-empty-preview",
  );
  equal(
    (await read(normal.runId)).lockedInputs,
    normal.lockedInputs,
    "owned-host-restart-same-locks",
  );
  group(
    "V10-BROWSER-003 actual own-host restart/persisted normal-empty/frozen locks",
  );
  for (const profile of [
    "profile-standard",
    "profile-comparison",
    "synthetic-analysis-equal-v1",
    "synthetic-analysis-operations-v1",
    "synthetic-review-maturity-equal-v1",
    "synthetic-review-maturity-operations-v1",
  ]) {
    const run = await start(
        profile.startsWith("profile-")
          ? "baseline-complete"
          : "synthetic-analysis-findings-v1",
        profile,
        headers,
      ),
      detail = await analysis(run.runId);
    equal(detail.aiPreview, null, "six-historical-API-null-preview");
    check(
      !run.lockedInputs.some((x) =>
        [
          "Frozen offline AI contents",
          "Offline AI configuration template",
        ].includes(x.name),
      ),
      "six-historical-no-new-locks",
    );
    await select(page, run.runId);
    if (profile.startsWith("synthetic-"))
      await page
        .getByRole("heading", {
          name: "Findings and reproducible health calculations",
          exact: true,
        })
        .waitFor();
    else
      await page
        .getByText(
          "This saved fixture demonstrates coverage only; health scoring remains unavailable.",
          { exact: true },
        )
        .waitFor();
    equal(await region(page).count(), 0, "six-historical-UI-no-preview");
    if (profile.startsWith("synthetic-")) {
      equal(detail.status, "Ready", "historical-deterministic-health-ready");
      equal(
        detail.provisional.display,
        profile.endsWith("operations-v1") ? "61.3" : "44.2",
        "historical-literal-provisional-health",
      );
      equal(
        detail.publishableCurrent.display,
        "78.3",
        "historical-literal-publishable-health",
      );
    }
  }
  group(
    "V10-BROWSER-004 all six historical API/UI/null/locks/health regressions",
  );
  const zero = "0".repeat(64),
    foreign = "22222222-3333-4444-5555-666666666666";
  const negative = [
    ["outer-run", (v) => (v.runId = foreign)],
    ["outer-revision", (v) => v.runRevision++],
    ["preview-run", (v) => (v.aiPreview.runId = foreign)],
    ["preview-revision", (v) => v.aiPreview.runRevision++],
    ["input-digest", (v) => (v.aiPreview.runInputDigest = zero)],
    ["baseline", (v) => (v.aiPreview.baselineId = "baseline-complete")],
    ["profile", (v) => (v.aiPreview.profileId = expected.empty)],
    ["fixture", (v) => (v.aiPreview.fixtureDigest = zero)],
    ["detail-schema", (v) => (v.aiPreview.schemaVersion = "different")],
    ["detail-status", (v) => (v.aiPreview.status = "Published")],
    ["detail-extra", (v) => (v.aiPreview.extra = "unexpected")],
    ["detail-null", (v) => (v.aiPreview = null)],
    ["snapshot-null", (v) => (v.aiPreview.snapshot = null)],
    ["snapshot-status", (v) => (v.aiPreview.snapshot.status = "Verified")],
    ["disclaimer", (v) => (v.aiPreview.snapshot.disclaimer = "Trusted")],
    ["digest", (v) => (v.aiPreview.snapshot.contentDigest = zero)],
    ["canonical", (v) => (v.aiPreview.snapshot.canonicalJson += " ")],
    [
      "duplicate-canonical",
      (v) =>
        (v.aiPreview.snapshot.canonicalJson =
          v.aiPreview.snapshot.canonicalJson.replace(
            "{",
            '{"status":"Proposed",',
          )),
    ],
    ["source-run", (v) => (v.aiPreview.snapshot.source.runId = foreign)],
    ["source-input", (v) => (v.aiPreview.snapshot.source.profileDigest = zero)],
    [
      "source-template",
      (v) => (v.aiPreview.snapshot.source.baselineDigest = zero),
    ],
    [
      "source-scope",
      (v) => (v.aiPreview.snapshot.source.customerId = "foreign"),
    ],
    ["source-extra", (v) => (v.aiPreview.snapshot.source.extra = "unexpected")],
    [
      "displayed-text",
      (v) => (v.aiPreview.snapshot.proposals[0].facts[0].text = "changed"),
    ],
    [
      "displayed-citation",
      (v) =>
        (v.aiPreview.snapshot.proposals[0].facts[0].evidenceIds = [expected.B]),
    ],
    [
      "displayed-context",
      (v) => (v.aiPreview.snapshot.proposals[0].missingContext = []),
    ],
    [
      "displayed-conflict",
      (v) => (v.aiPreview.snapshot.proposals[0].conflictingEvidenceIds = []),
    ],
    ["snapshot-extra", (v) => (v.aiPreview.snapshot.extra = "unexpected")],
    ["ready-reason", (v) => (v.aiPreview.reasonCode = "unexpected")],
  ];
  for (const [code, change] of negative) {
    mutation = { id: normal.runId, change };
    await select(page, normal.runId);
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .waitFor();
    equal(
      await region(page).count(),
      0,
      "corrupt-" + code + "-no-partial-preview",
    );
    mutation = null;
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .focus();
    await page.keyboard.press("Enter");
    await shown(page, normal.runId);
    equal(
      await page.evaluate(() => document.activeElement.id),
      "ai-proposal-preview-heading",
      "corrupt-" + code + "-keyboard-retry-focus",
    );
  }
  mutation = {
    id: normal.runId,
    change: (v) => {
      v.aiPreview.status = "Unavailable";
      v.aiPreview.reasonCode = "ai_preview_source_unavailable";
      v.aiPreview.snapshot = null;
    },
  };
  await select(page, normal.runId);
  await showProposedFindings(page);
  await region(page).getByRole("status").waitFor();
  equal(
    await region(page).locator("article,details").count(),
    0,
    "valid-unavailable-no-partial-or-stale-preview",
  );
  check(
    (await region(page).getByRole("status").textContent()).includes(
      "No proposal or health result is inferred.",
    ),
    "valid-unavailable-explicit-no-health",
  );
  mutation = null;
  group(
    "V10-BROWSER-005 twenty-nine corrupt response guards/full source/canonical/DTO/keyboard retry/unavailable",
  );
  const badLocks = [
    [
      "missing-fixture",
      (value) =>
        (value.lockedInputs = value.lockedInputs.filter(
          (item) => item.name !== "Frozen offline AI contents",
        )),
    ],
    [
      "missing-template",
      (value) =>
        (value.lockedInputs = value.lockedInputs.filter(
          (item) => item.name !== "Offline AI configuration template",
        )),
    ],
    [
      "duplicate-lock",
      (value) => value.lockedInputs.push(value.lockedInputs[0]),
    ],
    [
      "changed-policy-version",
      (value) =>
        (value.lockedInputs.find((item) => item.name === "AI policy").version =
          "different"),
    ],
    [
      "changed-template-digest",
      (value) =>
        (value.lockedInputs.find(
          (item) => item.name === "Offline AI configuration template",
        ).sha256 = zero),
    ],
  ];
  for (const [code, change] of badLocks) {
    runMutation = { id: normal.runId, change };
    await select(page, normal.runId);
    await page
      .getByRole("button", { name: "Retry analysis", exact: true })
      .waitFor();
    equal(
      await region(page).count(),
      0,
      "invalid-selected-lock-" + code + "-fails-closed",
    );
    runMutation = null;
  }
  group(
    "V10-BROWSER-008 five selected frozen lock omissions/mismatches/duplicates fail closed",
  );

  const controls =
    expected.suggestion +
    '\u0000NUL\rCR\r\nCRLF <img src="https://fixture.invalid/inert" onerror="window.fixtureExecuted=true">';
  mutation = {
    id: normal.runId,
    change: (value) => {
      const snapshot = value.aiPreview.snapshot;
      snapshot.proposals[0].suggestions[0].text = controls;
      const {
        canonicalJson: ignoredBytes,
        contentDigest: ignoredDigest,
        ...payload
      } = snapshot;
      snapshot.canonicalJson = expected.canonical(payload);
      snapshot.contentDigest = expected.hash(snapshot.canonicalJson);
    },
  };
  await select(page, normal.runId);
  await shown(page, normal.runId);
  equal(
    await region(page)
      .locator("article section")
      .filter({
        has: page.getByRole("heading", { name: "Suggestions", exact: true }),
      })
      .locator("ol > li > p")
      .textContent(),
    controls,
    "React-client-exact-NUL-CR-CRLF-inert-resource-text-node",
  );
  equal(
    await region(page).locator("script,img,[onerror],[src],[href]").count(),
    0,
    "React-control-text-no-active-node-or-resource",
  );
  equal(
    await page.evaluate(() => window.fixtureExecuted),
    undefined,
    "React-control-text-no-execution",
  );
  mutation = null;
  group(
    "V10-BROWSER-007 intercepted coherent presentation-only control text retains original React DOM units",
  );

  let entered, release;
  const observed = new Promise((resolve) => (entered = resolve)),
    gate = new Promise((resolve) => (release = resolve));
  delay = { id: normal.runId, entered, promise: gate };
  await select(page, normal.runId);
  await observed;
  equal(await region(page).count(), 0, "delayed-preview-cleared-before-result");
  const row = page.locator(".history tbody tr").filter({
    has: page.getByText("Fictional configuration · empty offline AI response", {
      exact: true,
    }),
  });
  await row.first().getByRole("button").focus();
  await page.keyboard.press("Enter");
  await shown(page, empty.runId);
  release();
  delay = null;
  await page.waitForTimeout(150);
  equal(
    await region(page).locator("details dd").first().textContent(),
    empty.runId,
    "late-old-result-cannot-replace-selected-preview",
  );
  equal(
    await region(page).locator("article").count(),
    0,
    "late-old-result-cannot-repopulate-proposals",
  );
  equal(faults, [], "no-uncaught-browser-error");
  equal(dialogs, [], "no-hostile-dialog");
  equal(blocked, [], "zero-external-or-unexpected-request-attempts");
  group(
    "V10-BROWSER-006 actual delayed old response/keyboard history/abort/stale-clear/zero injected effects",
  );
  report = {
    status: "PASS",
    checks,
    groups,
    browser: browser.version(),
    node: process.version,
    screenshots: shots,
    blockedRequestCount: blocked.length,
    pageErrorCount: faults.length,
    dialogCount: dialogs.length,
    negativeCases: negative.map((x) => x[0]),
    invalidSelectedLocks: badLocks.map((x) => x[0]),
    hostRestart: true,
    limitations: [
      "Fictional local fixed responses only; no model/provider processing",
      "Chromium automated engineering only; Windows/manual screenreader acceptance NOT VERIFIED",
      "Production identity/security/budgets/retention/report publication/gates NOT VERIFIED",
    ],
  };
} catch {
  report = {
    status: "FAIL",
    checks,
    groups,
    lastCode: last,
    node: process.version,
    blockedRequestCount: blocked.length,
    pageErrorCount: faults.length,
    dialogCount: dialogs.length,
    screenshots: shots,
  };
  process.exitCode = 1;
} finally {
  try {
    if (delay) delay = null;
    await api?.dispose();
  } finally {
    try {
      await context?.close();
    } finally {
      try {
        await browser?.close();
      } finally {
        await stop();
      }
    }
  }
  await writeFile(
    path.join(directory, "execution.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
  console.log(JSON.stringify(report));
}
