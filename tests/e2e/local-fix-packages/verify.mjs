import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { readFile, writeFile, readdir, mkdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import { once } from "node:events";
import net from "node:net";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as expected from "./expected.mjs";
const directory = path.dirname(fileURLToPath(import.meta.url)),
  base = "http://127.0.0.1:5183";
const workdir = process.env.IGA_HOST_WORKING_DIRECTORY,
  hostDll = process.env.IGA_HOST_DLL;
assert.ok(path.isAbsolute(workdir ?? "") && path.isAbsolute(hostDll ?? ""));
assert.match(
  process.env.IGA_SYNTHETIC_DATABASE ?? "",
  /(?:^|;)Database=iga_synthetic_cycle12_v12(?:;|$)/,
);
assert.match(
  process.env.IGA_SYNTHETIC_DATABASE ?? "",
  /(?:^|;)Host=127\.0\.0\.1(?:;|$)/,
);
assert.match(
  process.env.IGA_SYNTHETIC_DATABASE ?? "",
  /(?:^|;)Port=55433(?:;|$)/,
);
assert.ok(path.isAbsolute(process.env.IGA_PLAYWRIGHT_MODULE ?? ""));
const { chromium, request } = await import(
  pathToFileURL(process.env.IGA_PLAYWRIGHT_MODULE).href
);
const axePath = path.resolve(
  path.dirname(process.env.IGA_PLAYWRIGHT_MODULE),
  "../axe-core/axe.min.js",
);
const axeSource = await readFile(axePath, "utf8");
let checks = 0,
  last = "initialize",
  host,
  browser,
  context,
  api,
  mutation = null,
  runMutation = null,
  hold = null,
  eventHold = null;
let actualBrowserVersion = "not-launched";
const pendingReleases = new Set();
const groups = [],
  blocked = [],
  faults = [],
  dialogs = [],
  screenshots = [],
  accessibility = [],
  known = new Set(),
  assets = new Set(["/"]);
function check(value, code) {
  last = code;
  assert.ok(value, code);
  checks++;
}
function equal(value, want, code) {
  last = code;
  assert.deepEqual(value, want, code);
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
    await new Promise((r) => setTimeout(r, 60));
  }
  throw new Error(code);
}
async function bounded(promise, code, milliseconds = 30000) {
  last = code;
  let timer;
  try {
    return await Promise.race([
      promise,
      new Promise(
        (_, reject) =>
          (timer = setTimeout(() => reject(new Error(code)), milliseconds)),
      ),
    ]);
  } finally {
    clearTimeout(timer);
  }
}
async function inventory(folder, relative = "") {
  for (const entry of await readdir(folder, { withFileTypes: true })) {
    const next = path.posix.join(relative, entry.name);
    if (entry.isDirectory())
      await inventory(path.join(folder, entry.name), next);
    else assets.add("/" + next);
  }
}
await inventory(path.join(workdir, "src/web/dist"));
async function freePort() {
  await new Promise((resolve, reject) => {
    const probe = net.createServer();
    probe.once("error", () =>
      reject(new Error("owned-host-port-already-listening")),
    );
    probe.listen(5183, "127.0.0.1", () => probe.close(resolve));
  });
  check(true, "host-port-free-before-launch");
}
async function launch() {
  await freePort();
  check(!host, "owned-single-host");
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
  host.stdout.on("data", () => {});
  host.stderr.on("data", () => {});
  await until(
    async () => {
      check(
        host.exitCode === null &&
          host.signalCode === null &&
          faults.length === 0,
        "owned-host-live",
      );
      try {
        return (await fetch(base + "/local-demo/v1/catalog")).status;
      } catch {
        return 0;
      }
    },
    (x) => x === 200,
    "owned-host-ready",
    60000,
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
const read = async (id) => {
  const response = await api.get("/local-demo/v1/runs/" + id);
  equal(response.status(), 200, "actual-run-read");
  return response.json();
};
const analysis = async (id) => {
  const response = await api.get("/local-demo/v1/runs/" + id + "/analysis");
  equal(response.status(), 200, "actual-analysis-read");
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
  equal(response.status(), 201, "actual-start");
  const run = await response.json();
  known.add(run.runId);
  return until(
    () => read(run.runId),
    (x) => x.state === "Scoring",
    "actual-worker-completes",
  );
}
const region = (page) =>
  page.getByRole("region", { name: "Fictional fix packages", exact: true });
async function select(page, id) {
  await page.evaluate(
    (value) => sessionStorage.setItem("iga.synthetic.selected-run", value),
    id,
  );
  await page.reload();
  await page
    .getByRole("button", { name: "Start synthetic run", exact: true })
    .waitFor();
}
async function shown(page, id) {
  await region(page).waitFor();
  await until(
    () => region(page).locator(".fix-package-source").textContent(),
    (text) => text?.includes(id),
    "current-run-source-visible",
  );
}
function payloadSnapshot(snapshot) {
  const p = structuredClone(snapshot);
  delete p.canonicalJson;
  delete p.contentDigest;
  return p;
}
function sourceRows(source) {
  const result = [];
  function visit(value, key) {
    if (Array.isArray(value))
      value.forEach((item, i) => visit(item, `${key}[${i}]`));
    else if (value !== null && typeof value === "object")
      Object.keys(value)
        .sort()
        .forEach((k) => visit(value[k], key ? key + "." + k : k));
    else result.push([key, value === null ? "Not supplied" : String(value)]);
  }
  visit(source, "");
  return result;
}
async function rows(locator) {
  return locator
    .locator("dl > div")
    .evaluateAll((nodes) =>
      nodes.map((n) => [
        n.querySelector("dt").textContent,
        n.querySelector("dd").textContent,
      ]),
    );
}
async function inert(page) {
  const r = region(page);
  equal(
    await r
      .locator(
        "a,button,input,iframe,script,img,svg,object,embed,form,audio,video,style,link",
      )
      .count(),
    0,
    "no-fix-actions-or-active-nodes",
  );
  equal(
    await r
      .locator("*")
      .evaluateAll(
        (nodes) =>
          nodes.filter((n) =>
            [...n.attributes].some(
              (a) =>
                /^on/i.test(a.name) ||
                ["href", "src", "srcdoc", "style"].includes(a.name),
            ),
          ).length,
      ),
    0,
    "no-injected-resource-or-event-attributes",
  );
  equal(
    await page.evaluate(() => globalThis.v12Injected),
    undefined,
    "hostile-script-never-executes",
  );
}
async function fullVisible(page, run, detail) {
  await shown(page, run.runId);
  const r = region(page),
    s = detail.fixPackages.snapshot,
    g = s.guidance;
  equal(
    s.canonicalJson,
    expected.canonical(expected.payload(g)),
    "full-independent-package-byte-recipe",
  );
  equal(
    s.contentDigest,
    expected.hash(s.canonicalJson),
    "independent-full-package-digest",
  );
  equal(
    expected.canonical(payloadSnapshot(s)),
    s.canonicalJson,
    "every-actual-package-property-canonical",
  );
  equal(
    g,
    detail.recommendationGuidance.snapshot,
    "same-complete-captured-guidance",
  );
  equal(s.templates, expected.templates, "three-exact-fixed-templates");
  equal(
    await r.locator(".fix-package-warning strong").textContent(),
    "Unverified · Review-only",
    "visible-unverified-review-only",
  );
  equal(
    await r.getByText(expected.disclaimer, { exact: true }).count(),
    1,
    "fixed-visible-disclaimer",
  );
  await r
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  await inert(page);
  equal(
    await r.locator(".fix-package-group").count(),
    s.packages.length,
    "complete-package-count",
  );
  for (let i = 0; i < s.packages.length; i++) {
    const p = s.packages[i],
      f = g.findings.find((f) => f.findingId === p.findingId),
      group = r.locator(".fix-package-group").nth(i);
    equal(
      await group.locator(":scope > summary").textContent(),
      `${f.presentationTitle} · ${f.currentState} · Unverified artifacts`,
      "current-summary-state",
    );
    const groupRows = await group
      .locator(":scope > dl")
      .locator("div")
      .evaluateAll((nodes) =>
        nodes.map((n) => [
          n.querySelector("dt").textContent,
          n.querySelector("dd").textContent,
        ]),
      );
    equal(
      groupRows,
      [
        ["Package ID", p.packageId],
        ["Finding ID", f.findingId],
        ["Current title", f.presentationTitle],
        ["Current finding state", f.currentState],
        ["Finding revision", String(f.findingRevision)],
        [
          "Business context",
          f.businessContext || "No business context supplied.",
        ],
        ["Original title", f.originalTitle],
        ["Original finding state", f.initialState],
        ["Rule ID", f.ruleId],
        ["Rule version", f.ruleVersion],
        ["Category", f.categoryId],
        ["Severity", f.severity],
        ["Root cause", f.rootCause],
      ],
      "all-current-and-original-finding-fields",
    );
    const provenance = group
      .locator("details")
      .filter({
        has: page.getByText("Original occurrences and evidence provenance", {
          exact: true,
        }),
      })
      .first();
    equal(
      await provenance.locator("article").count(),
      f.occurrences.length,
      "complete-occurrence-count",
    );
    for (let j = 0; j < f.occurrences.length; j++) {
      const o = f.occurrences[j];
      equal(
        await rows(provenance.locator("article").nth(j)),
        [
          ["Occurrence ID", o.occurrenceId],
          ["Object ID", o.objectId],
          ["Object type", o.objectType],
          ["Module", o.moduleId],
          ["Original digest", o.originalDigest],
          ["Evidence reference", o.evidenceReference],
        ],
        "all-immutable-occurrence-provenance-fields",
      );
    }
    for (let j = 0; j < p.options.length; j++) {
      const o = p.options[j],
        original = f.options.find((x) => x.scopedOptionId === o.scopedOptionId),
        option = group.locator(".fix-package-option").nth(j);
      equal(
        await option
          .locator(":scope > dl > div")
          .evaluateAll((nodes) =>
            nodes.map((n) => [
              n.querySelector("dt").textContent,
              n.querySelector("dd").textContent,
            ]),
          ),
        [
          ["Scoped option ID", original.scopedOptionId],
          ["Original option ID", original.optionId],
          ["Recommendation status", original.status],
          ["Original option guidance", original.text],
          ["Prerequisites", original.prerequisites],
          ["Risk", original.risk],
          ["Recovery guidance", original.recoveryGuidance],
        ],
        "all-original-option-text-and-identity-fields",
      );
      for (let k = 0; k < o.artifacts.length; k++) {
        const a = o.artifacts[k],
          article = option.locator(".fix-package-artifact").nth(k);
        equal(
          await rows(article),
          [
            ["Artifact ID", a.artifactId],
            ["Template ID", a.templateId],
            ["Artifact kind", a.kind],
            ["Artifact status", a.status],
          ],
          "all-artifact-identity-status-fields",
        );
        equal(
          await article.locator("code").textContent(),
          a.text,
          "exact-artifact-text",
        );
        equal(
          await article.locator("h5").textContent(),
          a.kind + " · Unverified",
          "every-artifact-unverified-heading",
        );
      }
    }
    const lists = group
      .locator("details")
      .filter({
        has: page.getByText(
          "Validation, references, assumptions and limitations",
          { exact: true },
        ),
      })
      .first();
    for (const [label, key] of [
      ["Validation guidance", "validationGuidance"],
      ["Guidance references", "guidanceReferences"],
      ["Assumptions", "assumptions"],
      ["Finding limitations", "limitations"],
    ]) {
      const heading = lists.getByRole("heading", { name: label, exact: true });
      const values = await heading.evaluate((n) => {
        const next = n.nextElementSibling;
        return next.tagName === "UL"
          ? [...next.children].map((c) => c.textContent)
          : [];
      });
      equal(values, f[key], "all-finding-" + key);
    }
  }
  const source = r.locator(".fix-package-source");
  equal(
    await rows(source),
    [
      ["Preview schema", s.schemaVersion],
      ["Preview status", s.status],
      ["Package content digest", s.contentDigest],
      ["Guidance content digest", g.contentDigest],
      ["Detail schema", detail.fixPackages.schemaVersion],
      ["Detail status", detail.fixPackages.status],
      ["Run ID", run.runId],
      ["Run revision", String(run.revision)],
      ["Complete frozen input digest", detail.fixPackages.runInputDigest],
      ["Baseline", run.selection.baselineId],
      ["Profile", run.selection.profileId],
      ...sourceRows(g.source),
    ],
    "all-source-version-lock-digest-fields",
  );
  for (const text of [
    ...s.warnings,
    ...s.unavailableSections,
    ...g.warnings,
    ...g.unavailableSections,
  ])
    check(
      (await r.getByText(text, { exact: true }).count()) >= 1,
      "all-current-and-historical-warning-text",
    );
  const fixedRegistry = r.locator("details").filter({
    has: page.getByText("Fixed fictional templates", { exact: true }),
  });
  equal(
    await rows(fixedRegistry),
    [
      ["Template version", expected.templateVersion],
      ["Template catalog digest", expected.templateDigest],
      ...expected.templates.flatMap((t) => [
        ["Template ID", t.templateId],
        ["Template kind", t.kind],
      ]),
    ],
    "all-fixed-registry-version-digest-template-identity-fields",
  );
  equal(
    await fixedRegistry.locator("h4").allTextContents(),
    expected.templates.map((t) => t.kind + " template · Unverified"),
    "all-fixed-template-native-kind-headings",
  );
  const historical = r.locator("details").filter({
    has: page.getByText("Historical upstream guidance boundary", {
      exact: true,
    }),
  });
  equal(
    await rows(historical),
    [
      ["Guidance schema", g.schemaVersion],
      ["Guidance status", g.status],
    ],
    "historical-upstream-schema-status-explicit",
  );
  for (const label of [
    "Current package warnings and unavailable capabilities",
    "Historical upstream guidance boundary",
    "Fixed fictional templates",
    "Captured source, versions and digests",
  ])
    equal(
      await r.locator("summary").filter({ hasText: label }).count(),
      1,
      "complete-native-boundary-summary",
    );
  for (const t of expected.templates) {
    check(
      (await r.locator("code").allTextContents()).includes(t.text),
      "visible-fixed-template-text",
    );
  }
  if (!s.packages.length)
    equal(
      await r.locator(".fix-package-empty").textContent(),
      "No findings were supplied; no fix packages or actions are available. Empty input does not establish healthy coverage or validated remediation.",
      "empty-no-health-or-remediation-claim",
    );
}
async function retryVisible(page) {
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .waitFor();
  equal(await region(page).count(), 0, "invalid-whole-analysis-no-old-package");
}
async function retry(page, run) {
  const replacedSelectedLocks = runMutation !== null;
  mutation = null;
  runMutation = null;
  if (replacedSelectedLocks)
    await until(
      () =>
        page
          .locator("dt")
          .filter({ hasText: "Fictional fix-package templates" })
          .count(),
      (count) => count === 1,
      "restored-selected-locks-poll-before-analysis-retry",
    );
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .click();
  await shown(page, run.runId);
  check(
    await page
      .getByRole("heading", {
        name: "Findings and reproducible health calculations",
        exact: true,
      })
      .evaluate((n) => n === document.activeElement),
    "retry-established-heading-focus",
  );
}
async function mutateCase(page, run, change, code) {
  mutation = change;
  await select(page, run.runId);
  await retryVisible(page);
  group(code);
  await retry(page, run);
}
const hostile =
  "<script>globalThis.v12Injected=true</script><img src='https://fixture.invalid/v12' onerror='globalThis.v12Injected=true'><form action='https://fixture.invalid/v12'><input autofocus></form><svg onload='globalThis.v12Injected=true'></svg> café 中文 😀 NUL[\0] CR[\r] CRLF[\r\n]";
function coherentHostile(d) {
  const g = d.recommendationGuidance.snapshot,
    f = g.findings[0],
    original = d.findings.find((x) => x.id === f.findingId),
    current = d.review.findings.find((x) => x.id === f.findingId);
  f.presentationTitle = hostile;
  f.businessContext = hostile;
  f.findingRevision = Math.max(1, f.findingRevision);
  original.title = hostile;
  current.title = hostile;
  current.businessContext = hostile;
  current.revision = f.findingRevision;
  const gp = structuredClone(g);
  delete gp.contentDigest;
  g.contentDigest = expected.hash(expected.canonical(gp));
  d.fixPackages.snapshot.guidance = structuredClone(g);
  expected.rehash(d.fixPackages.snapshot);
  return d;
}
async function routeHandler(route) {
  const req = route.request(),
    u = new URL(req.url());
  const match =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)(?:\/(?:analysis|review))?$/.exec(
      u.pathname,
    );
  const event =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)\/findings\/[a-f0-9]{64}\/events$/.exec(
      u.pathname,
    );
  let allowed =
    u.origin === base &&
    ((req.method() === "GET" &&
      (assets.has(u.pathname) ||
        u.pathname === "/local-demo/v1/catalog" ||
        u.pathname === "/local-demo/v1/runs" ||
        (match && known.has(match[1])))) ||
      (req.method() === "POST" && event && known.has(event[1])));
  if (!allowed) {
    blocked.push("unexpected-request");
    await route.abort();
    return;
  }
  if (eventHold && req.method() === "POST" && event) {
    const response = await route.fetch();
    const active = eventHold;
    active.captured();
    await active.release;
    try {
      await route.fulfill({ response });
    } finally {
      active.completed();
    }
    return;
  }
  if (u.pathname.endsWith("/analysis") && (mutation || hold)) {
    const response = await route.fetch();
    let data = await response.json();
    const capturedMutation = mutation,
      capturedHold = hold;
    if (capturedMutation) data = capturedMutation(structuredClone(data));
    if (capturedHold && data.runId === capturedHold.runId) {
      capturedHold.captured(data);
      await capturedHold.release;
    }
    try {
      await route.fulfill({ response, json: data });
    } finally {
      if (capturedHold && data.runId === capturedHold.runId)
        capturedHold.completed();
    }
    return;
  }
  if (runMutation && /^\/local-demo\/v1\/runs\/[a-f0-9-]+$/.test(u.pathname)) {
    const response = await route.fetch();
    await route.fulfill({ response, json: runMutation(await response.json()) });
    return;
  }
  await route.continue();
}
try {
  await launch();
  api = await request.newContext({ baseURL: base });
  const catalog = await (await api.get("/local-demo/v1/catalog")).json();
  const headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  browser = await chromium.launch({
    headless: true,
    ...(process.env.IGA_BROWSER_EXECUTABLE
      ? { executablePath: process.env.IGA_BROWSER_EXECUTABLE }
      : {}),
  });
  actualBrowserVersion = browser.version();
  context = await browser.newContext();
  await context.route("**/*", routeHandler);
  context.on("page", (p) => {
    p.on("pageerror", () => faults.push("pageerror"));
    p.on("dialog", (d) => {
      dialogs.push("unexpected-dialog");
      void d.dismiss();
    });
  });
  const page = await context.newPage();
  await page.goto(base);
  const runs = [];
  for (const baseline of [
    "synthetic-analysis-healthy-v1",
    "synthetic-analysis-gaps-v1",
    "synthetic-analysis-findings-v1",
    "synthetic-analysis-mixed-v1",
  ]) {
    const run = await start(baseline, expected.profile, headers);
    runs.push(run);
    const d = await analysis(run.runId);
    await select(page, run.runId);
    if (d.fixPackages.status === "Ready") await fullVisible(page, run, d);
    else {
      await region(page).getByRole("status").waitFor();
      check(
        (await region(page).textContent()).includes("unavailable"),
        "gaps-source-explicit-unavailable",
      );
      equal(
        await region(page).locator("article,code").count(),
        0,
        "unavailable-no-partial-artifact",
      );
    }
  }
  group(
    "V12-BROWSER actual four-presets/full-source/complete-fields/empty-unavailable",
  );
  const normal = runs[2],
    normalDetail = await analysis(normal.runId);
  await select(page, normal.runId);
  await fullVisible(page, normal, normalDetail);
  const original = normalDetail.fixPackages.snapshot.guidance.findings.map(
    (f) => ({
      id: f.findingId,
      title: f.originalTitle,
      occurrences: f.occurrences,
    }),
  );
  const artifacts = normalDetail.fixPackages.snapshot.packages;
  const finding = normalDetail.review.findings.find(
    (f) => f.state === "Proposed",
  );
  const event = {
    eventId: randomUUID(),
    expectedRevision: finding.revision,
    kind: "Comment",
    reason: null,
    text: "Fictional V12 browser review note",
    title: null,
    businessContext: null,
  };
  const posted = await api.post(
    `/local-demo/v1/runs/${normal.runId}/findings/${finding.id}/events`,
    { headers, data: event },
  );
  equal(posted.status(), 200, "actual-browser-review-event");
  const reviewed = await analysis(normal.runId);
  equal(
    reviewed.runRevision,
    normalDetail.runRevision,
    "actual-review-unchanged-run-revision",
  );
  check(
    reviewed.reviewSnapshotDigest !== normalDetail.reviewSnapshotDigest,
    "actual-new-review-digest",
  );
  equal(
    reviewed.provisional,
    normalDetail.provisional,
    "comment-preserves-provisional-health",
  );
  equal(
    reviewed.publishableCurrent,
    normalDetail.publishableCurrent,
    "comment-preserves-publishable-health",
  );
  equal(
    reviewed.categories,
    normalDetail.categories,
    "comment-preserves-category-calculations",
  );
  equal(
    reviewed.quality,
    normalDetail.quality,
    "comment-preserves-separate-quality",
  );
  equal(
    reviewed.reportDraft.status,
    "Ready",
    "new-profile-real-draft-remains-ready",
  );
  equal(
    reviewed.reportDraft.snapshot.status,
    "SyntheticDraft",
    "detached-draft-never-published",
  );
  equal(
    reviewed.reportDraft.snapshot.source.reviewSnapshotDigest,
    reviewed.reviewSnapshotDigest,
    "draft-and-package-current-review-capture",
  );
  equal(
    reviewed.fixPackages.snapshot.packages,
    artifacts,
    "actual-review-fixed-artifacts-invariant",
  );
  equal(
    reviewed.fixPackages.snapshot.guidance.findings.map((f) => ({
      id: f.findingId,
      title: f.originalTitle,
      occurrences: f.occurrences,
    })),
    original,
    "actual-review-originals-invariant",
  );
  await select(page, normal.runId);
  await fullVisible(page, normal, reviewed);
  group(
    "V12-BROWSER actual saved review/current detached source/original artifacts",
  );
  for (const [code, change] of [
    [
      "foreign-run",
      (d) => {
        d.fixPackages.runId = randomUUID();
        return d;
      },
    ],
    [
      "wrong-source-review",
      (d) => {
        d.fixPackages.snapshot.guidance.source.reviewSnapshotDigest =
          "0".repeat(64);
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
    [
      "changed-displayed-artifact",
      (d) => {
        d.fixPackages.snapshot.packages[0].options[0].artifacts[0].text =
          "changed";
        return d;
      },
    ],
    [
      "forged-coherent-template",
      (d) => {
        d.fixPackages.snapshot.templates[0].text = "changed";
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
    [
      "wrong-artifact-status",
      (d) => {
        d.fixPackages.snapshot.packages[0].options[0].artifacts[0].status =
          "Approved";
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
    [
      "missing-field",
      (d) => {
        delete d.fixPackages.snapshot.disclaimer;
        return d;
      },
    ],
    [
      "extra-field",
      (d) => {
        d.fixPackages.snapshot.extra = true;
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
    [
      "duplicate-canonical-key",
      (d) => {
        const s = d.fixPackages.snapshot;
        s.canonicalJson = s.canonicalJson.replace(
          "{",
          '{"status":"Unverified",',
        );
        s.contentDigest = expected.hash(s.canonicalJson);
        return d;
      },
    ],
    [
      "wrong-canonical-digest",
      (d) => {
        d.fixPackages.snapshot.contentDigest = "0".repeat(64);
        return d;
      },
    ],
    [
      "foreign-option",
      (d) => {
        d.fixPackages.snapshot.packages[0].options[0].scopedOptionId =
          "0".repeat(64);
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
    [
      "stale-finding-revision",
      (d) => {
        d.fixPackages.snapshot.guidance.findings[0].findingRevision++;
        expected.rehash(d.fixPackages.snapshot);
        return d;
      },
    ],
  ])
    await mutateCase(page, normal, change, "V12-BROWSER deny-" + code);
  runMutation = (d) => {
    d.lockedInputs = d.lockedInputs.filter(
      (i) => i.name !== "Fictional fix-package templates",
    );
    return d;
  };
  await select(page, normal.runId);
  await retryVisible(page);
  await retry(page, normal);
  group("V12-BROWSER missing-selected-template-lock");
  mutation = coherentHostile;
  await select(page, normal.runId);
  await fullVisible(page, normal, coherentHostile(structuredClone(reviewed)));
  equal(
    await region(page)
      .locator(".fix-package-group > dl")
      .first()
      .locator("dd")
      .nth(2)
      .textContent(),
    hostile,
    "actual-React-NUL-CR-CRLF-Unicode-hostile-text-fidelity",
  );
  await inert(page);
  mutation = null;
  group("V12-BROWSER coherent hostile/control text nodes");
  // Hold a saved old capture, select another existing run through the actual UI,
  // then release it: the old run cannot restore after the new identity selection.
  let release, priorCompleted;
  const priorCompletion = new Promise((r) => (priorCompleted = r));
  const captured = new Promise((resolve) => {
    hold = {
      runId: normal.runId,
      captured: resolve,
      release: new Promise((r) => (release = r)),
      completed: priorCompleted,
    };
  });
  await select(page, normal.runId);
  pendingReleases.add(release);
  await bounded(captured, "held-prior-run-captured");
  const button = page
    .getByRole("row")
    .filter({ hasText: runs[3].selection.baselineLabel })
    .getByRole("button", { name: /^Open / });
  await button.first().click();
  hold = null;
  release();
  await bounded(priorCompletion, "held-prior-response-completed");
  await page.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
  await shown(page, runs[3].runId);
  check(
    !(await region(page).locator(".fix-package-source").textContent()).includes(
      normal.runId,
    ),
    "late-prior-run-source-not-restored",
  );
  group("V12-BROWSER held-prior-run-selection-race");
  await select(page, normal.runId);
  await shown(page, normal.runId);
  // An actual UI save triggers analysis refresh at an unchanged run revision.
  // Hold that captured response, require old artifacts to clear immediately,
  // and supersede it through another selected identity before releasing it.
  await page
    .getByLabel("Review action", { exact: true })
    .first()
    .selectOption("Comment");
  await page
    .getByRole("textbox", { name: "Comment", exact: true })
    .first()
    .fill("Fictional held refresh comment");
  let releaseReview, reviewCompleted;
  const reviewCompletion = new Promise((r) => (reviewCompleted = r));
  const capturedReview = new Promise((resolve) => {
    hold = {
      runId: normal.runId,
      captured: resolve,
      release: new Promise((r) => (releaseReview = r)),
      completed: reviewCompleted,
    };
  });
  await page
    .getByRole("button", { name: "Add comment", exact: true })
    .first()
    .click();
  pendingReleases.add(releaseReview);
  const oldRefresh = await bounded(capturedReview, "held-refresh-captured");
  equal(
    oldRefresh.runRevision,
    reviewed.runRevision,
    "held-review-refresh-same-run-revision",
  );
  check(
    oldRefresh.reviewSnapshotDigest !== reviewed.reviewSnapshotDigest,
    "held-refresh-new-review-digest",
  );
  equal(
    await region(page).count(),
    0,
    "review-refresh-clears-old-packages-before-result",
  );
  const currentFinding = oldRefresh.review.findings.find(
    (f) => f.id === finding.id,
  );
  const second = await api.post(
    `/local-demo/v1/runs/${normal.runId}/findings/${finding.id}/events`,
    {
      headers,
      data: {
        eventId: randomUUID(),
        expectedRevision: currentFinding.revision,
        kind: "Comment",
        reason: null,
        text: "Fictional superseding review comment",
        title: null,
        businessContext: null,
      },
    },
  );
  equal(second.status(), 200, "actual-superseding-review-event");
  const latest = await analysis(normal.runId);
  check(
    latest.reviewSnapshotDigest !== oldRefresh.reviewSnapshotDigest,
    "superseding-review-current-digest",
  );
  hold = null;
  await page
    .getByRole("row")
    .filter({ hasText: runs[3].selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  await shown(page, runs[3].runId);
  await page
    .getByRole("row")
    .filter({ hasText: normal.selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  await shown(page, normal.runId);
  releaseReview();
  await bounded(reviewCompletion, "held-review-refresh-response-completed");
  await page.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
  await until(
    () => region(page).locator(".fix-package-source").textContent(),
    (t) => t.includes(latest.reviewSnapshotDigest),
    "held-old-review-cannot-restore-current-source",
  );
  await fullVisible(page, normal, latest);
  group("V12-BROWSER UI-review-refresh-clear and late superseded capture");
  await mutateCase(
    page,
    normal,
    (d) => {
      d.fixPackages = structuredClone(oldRefresh.fixPackages);
      d.recommendationGuidance = structuredClone(
        oldRefresh.recommendationGuidance,
      );
      return d;
    },
    "V12-BROWSER reject-real-old-guidance-packages-paired-with-fresh-review",
  );
  await page
    .getByLabel("Review action", { exact: true })
    .first()
    .selectOption("Comment");
  await page
    .getByRole("textbox", { name: "Comment", exact: true })
    .first()
    .fill("Fictional obsolete completion comment");
  let releaseEvent, eventCompleted;
  const eventCompletion = new Promise((r) => (eventCompleted = r));
  const capturedEvent = new Promise((resolve) => {
    eventHold = {
      captured: resolve,
      release: new Promise((r) => (releaseEvent = r)),
      completed: eventCompleted,
    };
  });
  await page
    .getByRole("button", { name: "Add comment", exact: true })
    .first()
    .click();
  pendingReleases.add(releaseEvent);
  await bounded(capturedEvent, "held-review-event-captured");
  const eventCurrent = await analysis(normal.runId);
  eventHold = null;
  await page
    .getByRole("row")
    .filter({ hasText: runs[3].selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  await shown(page, runs[3].runId);
  await page
    .getByRole("row")
    .filter({ hasText: normal.selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  await shown(page, normal.runId);
  releaseEvent();
  await bounded(eventCompletion, "held-review-event-response-completed");
  await page.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
  await until(
    () => region(page).locator(".fix-package-source").textContent(),
    (t) => t.includes(eventCurrent.reviewSnapshotDigest),
    "obsolete-review-completion-cannot-restore-old-source",
  );
  group("V12-BROWSER obsolete-review-completion");
  for (const [name, baseline, profile] of [
    ["coverage-standard", "baseline-complete", "profile-standard"],
    ["coverage-comparison", "baseline-complete", "profile-comparison"],
    [
      "analysis-equal",
      "synthetic-analysis-findings-v1",
      "synthetic-analysis-equal-v1",
    ],
    [
      "analysis-operations",
      "synthetic-analysis-findings-v1",
      "synthetic-analysis-operations-v1",
    ],
    [
      "review-equal",
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-equal-v1",
    ],
    [
      "review-operations",
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-operations-v1",
    ],
    ["AI-normal", "baseline-ai-configuration-v1", "profile-ai-preview-v1"],
    ["AI-empty", "baseline-ai-configuration-v1", "profile-ai-preview-empty-v1"],
  ]) {
    const old = await start(baseline, profile, headers);
    const d = await analysis(old.runId);
    equal(d.fixPackages, null, "historical-null-fix-" + name);
    await select(page, old.runId);
    await until(
      () =>
        page
          .getByText("Reading analysis from the saved run…", { exact: true })
          .count(),
      (n) => n === 0,
      "historical-analysis-read-completed",
    );
    equal(await region(page).count(), 0, "historical-no-fix-region-" + name);
  }
  group("V12-BROWSER historical flows remain available");
  for (const [name, size] of [
    ["desktop", { width: 1440, height: 1000 }],
    ["mobile", { width: 390, height: 844 }],
    ["narrow", { width: 320, height: 800 }],
  ]) {
    await page.setViewportSize(size);
    for (const [fixture, run] of [
      ["normal", normal],
      ["empty", runs[0]],
      ["hostile", normal],
    ]) {
      mutation = fixture === "hostile" ? coherentHostile : null;
      await select(page, run.runId);
      const dto = await analysis(run.runId);
      await fullVisible(
        page,
        run,
        fixture === "hostile" ? coherentHostile(dto) : dto,
      );
      const summary = region(page).getByText(
        "Captured source, versions and digests",
        { exact: true },
      );
      await summary.focus();
      await page.keyboard.press("Enter");
      check(
        await summary.evaluate((n) => !n.parentElement.open),
        "keyboard-native-summary-close",
      );
      await page.keyboard.press("Enter");
      check(
        await summary.evaluate((n) => n.parentElement.open),
        "keyboard-native-summary-open",
      );
      check(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
        "viewport-no-horizontal-reflow",
      );
      await inert(page);
      await page.evaluate(axeSource);
      const result = await page.evaluate(async () => {
        const r = await axe.run(
          document.querySelector('[aria-label="Fictional fix packages"]'),
          {
            runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21aa"] },
          },
        );
        return {
          violations: r.violations.map((v) => ({
            id: v.id,
            impact: v.impact,
            nodes: v.nodes.length,
          })),
          passes: r.passes.map((p) => p.id),
          incomplete: r.incomplete.map((v) => ({
            id: v.id,
            nodes: v.nodes.length,
          })),
        };
      });
      equal(
        result.violations,
        [],
        "axe-zero-violations-" + name + "-" + fixture,
      );
      accessibility.push({ viewport: name, fixture, ...result });
      await inert(page);
      if (fixture === "normal") {
        const shot = path.join(directory, "execution", name + ".png");
        await mkdir(path.dirname(shot), { recursive: true });
        await page.screenshot({ path: shot, fullPage: true });
        screenshots.push(shot);
        await region(page).evaluate((element) =>
          element.scrollIntoView({ block: "start" }),
        );
        const visibleShot = path.join(
          directory,
          "execution",
          name + "-visible.png",
        );
        await page.screenshot({ path: visibleShot, fullPage: false });
        screenshots.push(visibleShot);
      }
    }
  }
  mutation = null;
  group("V12-BROWSER desktop-mobile-320 keyboard/reflow/axe/full fields");
  equal(blocked, [], "zero-unexpected-request-attempts");
  equal(dialogs, [], "zero-hostile-dialogs");
  equal(faults, [], "zero-page-or-host-faults");
  await context.close();
  context = null;
  await browser.close();
  browser = null;
  await stop();
  await launch();
  const afterRestart = await analysis(normal.runId);
  equal(
    afterRestart.fixPackages.snapshot.contentDigest,
    eventCurrent.fixPackages.snapshot.contentDigest,
    "owned-host-restart-current-package-identical",
  );
  await stop();
  group("V12-BROWSER own-host-restart-detached-package");
  await writeFile(
    path.join(directory, "execution.json"),
    JSON.stringify(
      {
        schema: "v12-browser-execution-v1",
        status: "PASS",
        checks,
        last,
        groups,
        blockedRequests: blocked.length,
        pageFaults: faults.length,
        dialogs: dialogs.length,
        accessibility,
        screenshots,
        defaultMacStartup: "NOT VERIFIED",
        temporaryConfigWatcherOverride:
          process.env.DOTNET_hostBuilder__reloadConfigOnChange === "false",
        browserVersion: actualBrowserVersion,
        nodeVersion: process.versions.node,
        playwrightModule: process.env.IGA_PLAYWRIGHT_MODULE,
      },
      null,
      2,
    ) + "\n",
  );
  console.log("PASS V12 browser assertions=" + checks);
} catch (error) {
  await writeFile(
    path.join(directory, "execution.json"),
    JSON.stringify(
      {
        schema: "v12-browser-execution-v1",
        status: "FAIL",
        checks,
        last,
        errorType: error.constructor.name,
        groups,
        blockedRequests: blocked.length,
        pageFaults: faults.length,
        dialogs: dialogs.length,
        accessibility,
        screenshots,
      },
      null,
      2,
    ) + "\n",
  );
  console.error(
    "FAIL V12 browser code=" +
      last +
      " assertions=" +
      checks +
      " type=" +
      error.constructor.name,
  );
  process.exitCode = 1;
} finally {
  for (const release of pendingReleases) release();
  try {
    if (context) await context.close();
    if (browser) await browser.close();
  } finally {
    try {
      if (api) await api.dispose();
    } finally {
      await stop();
    }
  }
}
