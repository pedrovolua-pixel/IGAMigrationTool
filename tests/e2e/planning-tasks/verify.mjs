import { showAssessments } from "../consultant-demo/navigation.mjs";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { readFile, writeFile, readdir, mkdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import { once } from "node:events";
import net from "node:net";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
// Sanitize before loading fixtures, browser tools, or launching the owned host.
// No inherited sentinel grants permission to bypass this closed allowlist.
const allowedEnvironment = new Set([
  "PATH",
  "HOME",
  "TMPDIR",
  "TEMP",
  "TMP",
  "LANG",
  "LC_ALL",
  "LC_CTYPE",
  "SYSTEMROOT",
  "SystemRoot",
  "WINDIR",
  "COMSPEC",
  "PATHEXT",
  "DOTNET_ROOT",
  "DOTNET_CLI_HOME",
  "DOTNET_hostBuilder__reloadConfigOnChange",
  "IGA_HOST_WORKING_DIRECTORY",
  "IGA_HOST_DLL",
  "IGA_DOTNET",
  "IGA_SYNTHETIC_DATABASE",
  "IGA_PLAYWRIGHT_MODULE",
  "IGA_BROWSER_EXECUTABLE",
]);
let removedEnvironmentKeys = 0;
for (const key of Object.keys(process.env))
  if (!allowedEnvironment.has(key)) {
    delete process.env[key];
    removedEnvironmentKeys++;
  }
assert.ok(Object.keys(process.env).every((key) => allowedEnvironment.has(key)));
const expected = await import("./expected.mjs");
const directory = path.dirname(fileURLToPath(import.meta.url)),
  base = "http://127.0.0.1:5183";
const workdir = process.env.IGA_HOST_WORKING_DIRECTORY,
  hostDll = process.env.IGA_HOST_DLL;
assert.ok(path.isAbsolute(workdir ?? "") && path.isAbsolute(hostDll ?? ""));
const connectionEntries = (process.env.IGA_SYNTHETIC_DATABASE ?? "")
  .split(";")
  .filter(Boolean)
  .map((part) => {
    const split = part.indexOf("=");
    assert.ok(split > 0, "owned-connection-syntax");
    return [
      part.slice(0, split).trim().toLowerCase(),
      part.slice(split + 1).trim(),
    ];
  });
assert.equal(
  new Set(connectionEntries.map(([key]) => key)).size,
  connectionEntries.length,
  "owned-connection-no-duplicates",
);
const connection = Object.fromEntries(connectionEntries);
assert.deepEqual(Object.keys(connection).sort(), [
  "database",
  "host",
  "port",
  "username",
]);
assert.deepEqual(connection, {
  host: "127.0.0.1",
  port: "55433",
  database: "iga_synthetic_cycle14_v14_browser",
  username: "iga_synthetic",
});
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
  eventHold = null,
  artifactHold = null,
  dropArtifact = false,
  receiptMutation = null;
const ownedHostLogChunks = [];
const taskPosts = [],
  taskIds = new Set();
let dropTask = false,
  taskHold = null,
  taskReceiptMutation = null,
  denyAnalysis = false,
  standaloneMutation = null;
const artifactPosts = [],
  artifactIds = new Set();
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
async function launch(enabled = true) {
  await freePort();
  check(!host, "owned-single-host");
  host = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [
      hostDll,
      "--synthetic-local-demo",
      ...(enabled ? ["--enable-synthetic-planning-tasks"] : []),
    ],
    {
      cwd: workdir,
      env: { ...process.env },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  host.on("error", () => faults.push("owned-host-spawn-error"));
  host.stdout.on("data", (data) => ownedHostLogChunks.push(data.toString()));
  host.stderr.on("data", (data) => ownedHostLogChunks.push(data.toString()));
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
  await showAssessments(page);
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
const overlay = (page) =>
  page.getByRole("region", { name: "Consultant artifact review", exact: true });
const article = (page, id) =>
  overlay(page)
    .locator("article")
    .filter({ has: page.locator("#artifact-review-" + id) });
const stateLabel = (state) =>
  state === "ReviewedForPlanning"
    ? expected.currentLabel
    : state === "NeedsReview"
      ? "Needs review — source changed"
      : "Unverified — no current planning attestation";
const tick = (page) =>
  page.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
function sourceValues(s) {
  return [
    ...Object.entries(s)
      .filter(([k]) => k !== "scope" && k !== "findingRevisions")
      .map(([k, v]) => [k, String(v)]),
    ...Object.entries(s.scope).map(([k, v]) => [k, String(v)]),
    [
      "Finding revisions",
      s.findingRevisions.map((f) => `${f.findingId}: ${f.revision}`).join(""),
    ],
  ];
}
async function visibleOverlay(page, run, d) {
  await shown(page, run.runId);
  const r = overlay(page),
    review = d.artifactReview;
  await r.waitFor();
  if (review.status === "Unavailable") {
    equal(
      await r.locator("article,textarea,button").count(),
      0,
      "unavailable-overlay-no-partial-history-actions",
    );
    check(
      (await r.getByRole("status").textContent()).includes(review.reasonCode),
      "bounded-unavailable-code-visible",
    );
    return;
  }
  equal(
    review.source,
    expected.binding(d.fixPackages.snapshot),
    "complete-independent-current-source-binding",
  );
  equal(
    review.artifacts.map(
      ({ revision, state, canReview, canWithdraw, history, ...a }) => a,
    ),
    expected
      .artifacts(d.fixPackages.snapshot)
      .sort((a, b) => (a.artifactId < b.artifactId ? -1 : 1)),
    "complete-independent-HTTP-artifact-global-order-bindings",
  );
  equal(
    await r.locator("article").count(),
    review.artifacts.length,
    "all-current-artifacts-visible",
  );
  await r
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  const currentSource = r
    .locator("details")
    .filter({ has: page.getByText("Current review source", { exact: true }) })
    .first();
  equal(
    await rows(currentSource),
    sourceValues(review.source),
    "every-current-source-field-vector-visible",
  );
  check(
    (await r.textContent()).includes("Current reviewer: " + review.actorId),
    "current-trusted-reviewer-visible",
  );
  for (const entry of review.artifacts) {
    artifactIds.add(entry.artifactId);
    const a = article(page, entry.artifactId);
    await a.waitFor();
    equal(
      await a.locator("h3").textContent(),
      `${entry.kind} artifact · ${entry.artifactId}`,
      "individual-kind-id-heading",
    );
    equal(
      await a.getByRole("status").first().textContent(),
      stateLabel(expected.state(entry.history, review.source.sourceDigest)),
      "current-source-latest-event-state-independent",
    );
    equal(
      entry.state,
      expected.state(entry.history, review.source.sourceDigest),
      "DTO-state-independent-history-oracle",
    );
    equal(
      entry.canReview,
      entry.state !== "ReviewedForPlanning",
      "DTO-current-review-flag",
    );
    equal(
      entry.canWithdraw,
      entry.state === "ReviewedForPlanning",
      "DTO-current-withdraw-flag",
    );
    equal(
      await a
        .locator(":scope > dl")
        .evaluate((n) =>
          Array.from(n.children).map((row) => [
            row.querySelector("dt").textContent,
            row.querySelector("dd").textContent,
          ]),
        ),
      Object.entries(entry)
        .filter(([k]) => k !== "history" && k !== "state")
        .map(([k, v]) => [k, String(v)]),
      "every-artifact-identity-revision-capability-field-visible",
    );
    equal(
      await a
        .getByText("Original generated status: Unverified", { exact: true })
        .count(),
      1,
      "current-overlay-never-relabels-original",
    );
    const original = d.fixPackages.snapshot.packages
      .flatMap((p) => p.options.flatMap((o) => o.artifacts))
      .find((x) => x.artifactId === entry.artifactId);
    equal(
      await a.locator("code").textContent(),
      original.text,
      "overlay-original-artifact-literal-text",
    );
    equal(
      await a.locator(":scope > details > summary").allTextContents(),
      [
        "Original fictional artifact text",
        `Attributed review history (${entry.history.length})`,
      ],
      "native-original-and-history-disclosure-labels",
    );
    const history = a.locator("ol > li");
    equal(
      await history.count(),
      entry.history.length,
      "full-ordered-attributed-event-history-visible",
    );
    equal(
      entry.history.map((e) => e.revision),
      Array.from({ length: entry.revision }, (_, i) => i + 1),
      "continuous-client-history-revisions",
    );
    equal(
      new Set(entry.history.map((e) => e.eventId)).size,
      entry.history.length,
      "unique-event-IDs",
    );
    for (let i = 0; i < entry.history.length; i++) {
      const e = entry.history[i],
        li = history.nth(i);
      equal(
        await li.locator("h4").textContent(),
        `Revision ${e.revision} · ${e.kind}`,
        "event-kind-outcome-historical-never-relabelled",
      );
      equal(
        await li.locator(":scope > p").allTextContents(),
        [
          `Event: ${e.eventId}`,
          `Actor: ${e.actorId} · Roles: ${e.actorRoles.join(", ")}`,
          `Recorded: ${e.recordedAtUtc}`,
          `Recorded outcome: ${e.recordedState}`,
          `Reason: ${e.reason}`,
        ],
        "every-literal-event-attribution-time-reason-outcome",
      );
      equal(
        await li.locator("time").textContent(),
        e.recordedAtUtc,
        "trusted-recorded-time-visible",
      );
      equal(
        await rows(li.locator("details")),
        sourceValues(e.source),
        "complete-server-verified-historical-source-reference-visible",
      );
      equal(
        await li.locator("summary").textContent(),
        "Recorded source · server-verified historical binding",
        "historical-binding-trust-boundary-visible",
      );
    }
    equal(
      await a.locator("textarea").getAttribute("maxlength"),
      "2000",
      "reason-UTF16-bound-visible-input",
    );
    equal(
      await a
        .getByRole("button", {
          name: entry.canWithdraw
            ? "Withdraw planning review"
            : "Review for planning",
          exact: true,
        })
        .count(),
      1,
      "only-current-review-or-withdraw-action",
    );
    const reason = await a.locator("textarea").inputValue();
    equal(
      await a.getByRole("button").isDisabled(),
      /^\p{White_Space}*$/u.test(reason),
      "reason-validity-controls-action-without-forgetting-draft",
    );
  }
  if (!review.artifacts.length)
    equal(
      await r
        .getByText(
          "No artifacts are available to review. Empty results do not establish health or remediation.",
          { exact: true },
        )
        .count(),
      1,
      "empty-not-health-or-remediation",
    );
  await inertOverlay(page);
}
async function inertOverlay(page) {
  const r = overlay(page);
  equal(
    await r
      .locator(
        "a,iframe,script,img,svg,object,embed,form,audio,video,style,link,input",
      )
      .count(),
    0,
    "no-supplied-links-executors-downloads-injected-active-nodes",
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
                [
                  "href",
                  "src",
                  "srcdoc",
                  "style",
                  "action",
                  "formaction",
                ].includes(a.name),
            ),
          ).length,
      ),
    0,
    "no-injected-event-resource-action-attributes",
  );
  equal(
    await page.evaluate(() => globalThis.v13Injected),
    undefined,
    "hostile-review-reason-never-executes",
  );
}
async function retryVisible(page) {
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .waitFor();
  equal(
    await overlay(page).count(),
    0,
    "invalid-analysis-no-current-artifact-attestation",
  );
}
async function retry(page, run) {
  mutation = null;
  runMutation = null;
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .click();
  await shown(page, run.runId);
}
async function badCase(page, run, change, code) {
  mutation = change;
  await select(page, run.runId);
  await retryVisible(page);
  group(code);
  await retry(page, run);
}
async function artifactPost(run, id, command, headers) {
  return api.post(`/local-demo/v1/runs/${run.runId}/artifacts/${id}/review`, {
    headers,
    data: command,
  });
}
async function waitCurrent(page, run, id, state, revision) {
  await until(
    async () => {
      const d = await analysis(run.runId);
      return d;
    },
    (d) =>
      d.artifactReview?.artifacts?.find((e) => e.artifactId === id)
        ?.revision === revision &&
      d.artifactReview.artifacts.find((e) => e.artifactId === id).state ===
        state,
    "actual-current-artifact-revision-state",
  );
  await until(
    () => article(page, id).getByRole("status").first().textContent(),
    (x) => x === stateLabel(state),
    "coherent-current-artifact-label-refreshed",
  );
}
async function routeHandler(route) {
  const req = route.request(),
    u = new URL(req.url());
  const readRoute =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)(?:\/(?:analysis|review|planning-tasks))?$/.exec(
      u.pathname,
    );
  const event =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)\/findings\/[a-f0-9]{64}\/events$/.exec(
      u.pathname,
    );
  const action =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)\/artifacts\/([a-f0-9]{64})\/review$/.exec(
      u.pathname,
    );
  const taskRoute =
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)\/planning-tasks\/([a-f0-9]{64})\/events$/.exec(
      u.pathname,
    );
  const allowed =
    u.origin === base &&
    ((req.method() === "GET" &&
      (assets.has(u.pathname) ||
        u.pathname === "/local-demo/v1/catalog" ||
        u.pathname === "/local-demo/v1/runs" ||
        (readRoute && known.has(readRoute[1])))) ||
      (req.method() === "POST" &&
        ((event && known.has(event[1])) ||
          (action && known.has(action[1]) && artifactIds.has(action[2])) ||
          (taskRoute &&
            known.has(taskRoute[1]) &&
            taskIds.has(taskRoute[2])))));
  if (!allowed) {
    blocked.push("unexpected-request");
    await route.abort();
    return;
  }
  if (u.pathname.endsWith("/analysis") && denyAnalysis) {
    await route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        schemaVersion: 1,
        demoOnly: true,
        status: "Unavailable",
        reasonCode: "planning_task_source_unavailable",
      }),
    });
    return;
  }
  if (
    req.method() === "GET" &&
    u.pathname.endsWith("/planning-tasks") &&
    standaloneMutation
  ) {
    const response = await route.fetch();
    await route.fulfill({
      response,
      json: standaloneMutation(await response.json()),
    });
    return;
  }
  if (taskRoute) {
    taskPosts.push({
      runId: taskRoute[1],
      taskId: taskRoute[2],
      command: JSON.parse(req.postData()),
    });
    const response = await route.fetch();
    if (dropTask) {
      dropTask = false;
      await route.abort();
      return;
    }
    if (taskReceiptMutation) {
      const change = taskReceiptMutation;
      taskReceiptMutation = null;
      await route.fulfill({ response, json: change(await response.json()) });
      return;
    }
    if (taskHold) {
      const active = taskHold;
      active.captured(await response.json());
      await active.release;
      try {
        await route.fulfill({ response });
      } finally {
        active.completed();
      }
      return;
    }
    await route.fulfill({ response });
    return;
  }
  if (action) {
    const command = JSON.parse(req.postData());
    artifactPosts.push({ runId: action[1], artifactId: action[2], command });
    if (dropArtifact) {
      dropArtifact = false;
      await route.fetch();
      await route.abort();
      return;
    }
    if (receiptMutation) {
      const change = receiptMutation;
      receiptMutation = null;
      const response = await route.fetch();
      await route.fulfill({ response, json: change(await response.json()) });
      return;
    }
    if (artifactHold) {
      const active = artifactHold,
        response = await route.fetch();
      active.captured(await response.json());
      await active.release;
      try {
        await route.fulfill({ response });
      } finally {
        active.completed();
      }
      return;
    }
  }
  if (eventHold && event) {
    const active = eventHold,
      response = await route.fetch();
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
    let d = await response.json(),
      m = mutation,
      h = hold;
    if (m) d = m(structuredClone(d));
    if (h && d.runId === h.runId) {
      h.captured(d);
      await h.release;
    }
    try {
      await route.fulfill({ response, json: d });
    } finally {
      if (h && d.runId === h.runId) h.completed();
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
const planning = (page) => page.locator(".planning-tasks");
const taskArticle = (page, id) =>
  planning(page)
    .locator("article")
    .filter({ has: page.locator("#planning-task-" + id) });
const labels = {
  Create: "Create planning task",
  ReconfirmPlan: "Reconfirm plan",
  StartProgress: "Start work",
  ReturnToPlanned: "Return to planned",
  Complete: "Complete planning work",
  Cancel: "Cancel task",
  Reopen: "Reopen task",
  Comment: "Add comment",
};
const statuses = {
  Planned: "Planned",
  InProgress: "In progress",
  Completed: "Completed",
  Cancelled: "Cancelled",
};
const freshLabels = {
  CurrentPlan: "Current plan",
  NeedsReconfirmation: "Needs reconfirmation",
  SourceUnavailable: "Source unavailable",
};
const kindLabels = {
  Create: "Create planning task",
  ReconfirmPlan: "Reconfirm plan",
  StartProgress: "Start work",
  ReturnToPlanned: "Return to planned",
  Complete: "Complete",
  Cancel: "Cancel",
  Reopen: "Reopen",
  Comment: "Comment",
};
const identityLabels = {
  taskId: "Task",
  findingId: "Finding",
  categoryId: "Category",
  packageId: "Package",
  scopedOptionId: "Recommendation option",
};
const fields = (locator) =>
  locator.evaluate((n) =>
    Array.from(n.children).map((row) => [
      row.querySelector("dt").textContent,
      row.querySelector("dd").textContent,
    ]),
  );
function taskSourceRows(source) {
  const { scope, findingRevisions, ...rest } = source.artifactSource;
  return [
    ...Object.entries(rest).map(([k, v]) => [k, String(v)]),
    ...Object.entries(scope),
    ["Planning task contract", source.planningTaskContractDigest],
    [
      "Finding revisions",
      findingRevisions.map((f) => `${f.findingId}: ${f.revision}`).join(""),
    ],
  ];
}
function vectorText(a) {
  return `${a.artifactId} · Revision ${a.revision} · ${a.state} · Event ${a.eventId ?? "None"} · ${a.kind ?? "None"} · Source ${a.sourceDigest ?? "None"}`;
}
function verifyTasks(d) {
  const p = d.planningTasks;
  equal(
    Object.keys(p).sort(),
    [...expected.detailKeys].sort(),
    "closed-nine-task-detail-fields",
  );
  equal(
    p.source,
    expected.taskBinding(d.fixPackages.snapshot),
    "complete-independent-task-current-binding",
  );
  equal(
    p.options,
    expected.taskOptions(d.fixPackages.snapshot, d.artifactReview, p.entries),
    "complete-independent-option-identity-vector-current-capabilities",
  );
  equal(p.unavailableEntries, [], "ready-no-unavailable-metadata");
  equal(
    p.entries.map((e) => e.identity.taskId),
    [...p.entries.map((e) => e.identity.taskId)].sort(),
    "ordinal-task-entry-order",
  );
  const ids = new Set();
  for (const e of p.entries) {
    const opt = p.options.find((o) => o.identity.taskId === e.identity.taskId);
    equal(e.identity, opt.identity, "existing-task-complete-original-identity");
    equal(e.assigneeId, p.actorId, "trusted-assigned-consultant");
    equal(e.history.length, e.revision, "continuous-task-history-count");
    let state = null,
      plan = null;
    for (let i = 0; i < e.history.length; i++) {
      const h = e.history[i];
      equal(h.revision, i + 1, "continuous-ordered-task-revisions");
      check(!ids.has(h.eventId), "module-wide-accepted-taskUUID-unique");
      ids.add(h.eventId);
      equal(
        h.actorRoles,
        ["Consultant"],
        "exact-trusted-event-consultant-role",
      );
      equal(h.actorId, e.assigneeId, "attributed-task-event-owner");
      check(
        /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(
          h.recordedAtUtc,
        ),
        "task-event-exactUTCZ",
      );
      state = expected.recordedStatus(state, h.kind);
      equal(h.recordedStatus, state, "literal-event-workflow-oracle");
      if (h.kind === "Create" || h.kind === "ReconfirmPlan") plan = h;
      equal(
        h.planningEventId,
        plan.eventId,
        "every-event-immutable-latest-plan-reference",
      );
      equal(
        h.source.planningTaskContractDigest,
        expected.taskContractDigest,
        "historical-task-contract-fixed",
      );
      equal(
        h.attestations.map((a) => a.artifactId),
        e.identity.artifactIds,
        "historical-exact-three-artifact-reference",
      );
    }
    equal(e.creation, e.history[0], "immutable-first-creation-reference");
    equal(e.plan, plan, "immutable-latest-plan-reference");
    equal(e.status, state, "current-status-from-events");
    const current = expected.freshness(e, p.source, opt.currentAttestations);
    equal(e.freshness, current, "freshness-independent-of-workflow");
    const flags = expected.capabilities(
      state,
      current,
      opt.findingState,
      opt.currentAttestations.every((a) => a.state === "ReviewedForPlanning"),
    );
    for (const [key, value] of Object.entries(flags))
      equal(e[key], value, "independent-current-task-capability-" + key);
  }
}
async function inertTasks(page) {
  const r = planning(page);
  equal(
    await r
      .locator(
        "a,iframe,script,img,svg,object,embed,form,audio,video,style,link",
      )
      .count(),
    0,
    "task-no-active-injected-nodes-or-actions",
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
                ["href", "src", "srcdoc", "style", "action"].includes(a.name),
            ),
          ).length,
      ),
    0,
    "task-no-dynamic-resource-event-style-attributes",
  );
  equal(
    await page.evaluate(() => globalThis.v14Injected),
    undefined,
    "task-hostile-content-never-executes",
  );
}
async function historyVisible(locator, event) {
  equal(
    await locator.locator("h4").textContent(),
    `Revision ${event.revision} · ${kindLabels[event.kind]}`,
    "full-history-kind-revision-visible",
  );
  equal(
    await locator.locator(":scope > p").allTextContents(),
    [
      `Event: ${event.eventId}`,
      `Actor: ${event.actorId} · Roles: ${event.actorRoles.join(", ")}`,
      `Recorded: ${event.recordedAtUtc}`,
      `Recorded status: ${statuses[event.recordedStatus]}`,
      `Planning event: ${event.planningEventId}`,
      `Reason or comment: ${event.reason}`,
    ],
    "full-history-literal-attribution-time-reason-state-visible",
  );
  equal(
    await rows(locator.locator("details")),
    taskSourceRows(event.source),
    "full-history-all-source-version-vector-fields-visible",
  );
  equal(
    await locator.locator("details > ul > li").allTextContents(),
    event.attestations.map(vectorText),
    "full-history-all-selected-attestation-fields-visible",
  );
}
async function visibleTasks(page, run, d, { pending = false } = {}) {
  verifyTasks(d);
  const r = planning(page);
  await r.waitFor();
  await until(
    () => r.textContent(),
    (t) => t.includes(d.planningTasks.source.artifactSource.sourceDigest),
    "current-task-source-visible",
  );
  await r
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  equal(
    await rows(r.locator(":scope > details")),
    taskSourceRows(d.planningTasks.source),
    "all-current-task-source-fields-visible",
  );
  equal(
    await r.locator("article").count(),
    d.planningTasks.options.length,
    "every-task-option-visible",
  );
  if (!d.planningTasks.options.length)
    check(
      (await r.textContent()).includes(
        "No recommendation options are available. Empty results do not establish health or remediation.",
      ),
      "task-empty-not-health-or-remediation",
    );
  for (const opt of d.planningTasks.options) {
    taskIds.add(opt.identity.taskId);
    const a = taskArticle(page, opt.identity.taskId),
      e = d.planningTasks.entries.find(
        (e) => e.identity.taskId === opt.identity.taskId,
      );
    equal(
      await a.locator("h3").textContent(),
      `${e ? "Planning task" : "Recommendation option"} · ${opt.identity.taskId}`,
      "complete-option-task-heading",
    );
    equal(
      await fields(a.locator(":scope > dl")),
      [
        ...Object.entries(opt.identity)
          .filter(([k]) => k !== "artifactIds")
          .map(([k, v]) => [identityLabels[k], v]),
        ["Selected artifacts", opt.identity.artifactIds.join("")],
      ],
      "all-stable-task-coordinate-fields-visible",
    );
    const finding = d.fixPackages.snapshot.guidance.findings.find(
        (f) => f.findingId === opt.identity.findingId,
      ),
      original = finding.options.find(
        (o) => o.scopedOptionId === opt.identity.scopedOptionId,
      ),
      packageOption = d.fixPackages.snapshot.packages
        .flatMap((p) => p.options)
        .find((o) => o.scopedOptionId === opt.identity.scopedOptionId);
    const disclosure = a.locator(":scope > details").filter({
      has: page.getByText(
        "Current original recommendation and fictional artifacts",
        { exact: true },
      ),
    });
    equal(
      await rows(disclosure),
      [
        ["Original finding title", finding.originalTitle],
        ["Rule", `${finding.ruleId} · ${finding.ruleVersion}`],
        ["Recommendation", original.text],
        ["Prerequisites", original.prerequisites],
        ["Risk", original.risk],
        ["Recovery guidance", original.recoveryGuidance],
      ],
      "every-original-recommendation-guidance-field-visible",
    );
    equal(
      await disclosure.locator(":scope > ul > li").allTextContents(),
      finding.validationGuidance,
      "all-original-validation-guidance-visible",
    );
    equal(
      await disclosure.locator("code").allTextContents(),
      packageOption.artifacts.map((x) => x.text),
      "every-original-artifact-text-inert-visible",
    );
    equal(
      await disclosure.locator("h4").allTextContents(),
      packageOption.artifacts.map((x) => `${x.kind} · ${x.artifactId}`),
      "every-original-artifact-kind-identity",
    );
    equal(
      await disclosure.locator(":scope > div > p").allTextContents(),
      packageOption.artifacts.map(
        (x) =>
          `Original generated status: Unverified · Template ${x.templateId}`,
      ),
      "every-original-artifact-status-template",
    );
    const vector = a.locator(":scope > details").filter({
      has: page.getByText("Current selected planning attestations", {
        exact: true,
      }),
    });
    equal(
      await vector.locator("ul > li").allTextContents(),
      opt.currentAttestations.map(vectorText),
      "all-current-selected-latest-event-vector-fields-visible",
    );
    if (e) {
      check(
        (await a.textContent()).includes(
          `Assignee: ${e.assigneeId} · Revision ${e.revision}`,
        ),
        "current-task-assignee-revision-visible",
      );
      if (!pending)
        equal(
          await a.getByRole("status").textContent(),
          `Status: ${statuses[e.status]} · ${freshLabels[e.freshness]}`,
          "current-workflow-freshness-separate-readable",
        );
      for (const [prefix, event] of [
        ["Creation binding", e.creation],
        ["Latest plan binding", e.plan],
      ]) {
        const detail = a.locator(":scope > details").filter({
          has: page.getByText(`${prefix} · Event ${event.eventId}`, {
            exact: true,
          }),
        });
        equal(
          await rows(detail),
          taskSourceRows(event.source),
          "all-immutable-" + prefix + "-source-fields-visible",
        );
        equal(
          await detail.locator(":scope > ul > li").allTextContents(),
          event.attestations.map(vectorText),
          "all-immutable-" + prefix + "-attestation-fields-visible",
        );
      }
      const history = a
        .locator(":scope > details")
        .filter({
          has: page.getByText(`Attributed task history (${e.history.length})`, {
            exact: true,
          }),
        })
        .locator(":scope > ol > li");
      equal(
        await history.count(),
        e.history.length,
        "all-attributed-task-events-visible",
      );
      for (let i = 0; i < e.history.length; i++)
        await historyVisible(history.nth(i), e.history[i]);
    }
    equal(
      await a.locator("textarea").getAttribute("maxlength"),
      "2000",
      "bounded-reason-input-visible",
    );
  }
  await inertTasks(page);
}
async function taskRead(run) {
  const r = await api.get(`/local-demo/v1/runs/${run.runId}/planning-tasks`);
  equal(r.status(), 200, "actual-standalone-task-read");
  return r.json();
}
async function taskPost(run, id, command, headers) {
  return api.post(
    `/local-demo/v1/runs/${run.runId}/planning-tasks/${id}/events`,
    { headers, data: command },
  );
}
function commandFor(
  d,
  id,
  kind = "Create",
  reason = "Fictional V14 API planning reason",
) {
  const opt = d.planningTasks.options.find((o) => o.identity.taskId === id),
    e = d.planningTasks.entries.find((e) => e.identity.taskId === id);
  return {
    eventId: randomUUID(),
    kind,
    expectedRevision: kind === "Create" ? 0 : e.revision,
    expectedSourceDigest: d.planningTasks.source.artifactSource.sourceDigest,
    expectedAttestations: opt.currentAttestations,
    reason,
  };
}
async function freshPage(page, run) {
  await select(page, run.runId);
  await shown(page, run.runId);
  await planning(page).waitFor();
  return analysis(run.runId);
}
async function waitTask(page, run, id, revision, status) {
  const d = await until(
    () => analysis(run.runId),
    (d) =>
      d.planningTasks.entries.some(
        (e) =>
          e.identity.taskId === id &&
          e.revision === revision &&
          (!status || e.status === status),
      ),
    "actual-task-history-next-revision",
  );
  await until(
    () => taskArticle(page, id).textContent(),
    (t) => t?.includes(`Revision ${revision}`),
    "actual-saved-task-revision-visible",
  );
  return d;
}
async function act(
  page,
  run,
  id,
  kind,
  reason = "Fictional V14 explicit " + kind,
) {
  const before = await analysis(run.runId),
    old =
      before.planningTasks.entries.find((e) => e.identity.taskId === id)
        ?.revision ?? 0;
  const a = taskArticle(page, id);
  await a.locator("textarea").fill(reason);
  await a.getByRole("button", { name: labels[kind], exact: true }).click();
  const d = await waitTask(
    page,
    run,
    id,
    old + 1,
    expected.recordedStatus(
      before.planningTasks.entries.find((e) => e.identity.taskId === id)
        ?.status,
      kind,
    ),
  );
  await visibleTasks(page, run, d);
  return d;
}
async function keyboardAct(page, run, id, kind, key) {
  check(key === "Enter" || key === "Space", "closed-keyboard-activation-key");
  const before = await analysis(run.runId),
    entry = before.planningTasks.entries.find((e) => e.identity.taskId === id),
    old = entry?.revision ?? 0,
    reason = "Fictional V14 keyboard " + kind + " " + key;
  const article = taskArticle(page, id),
    button = article.getByRole("button", { name: labels[kind], exact: true });
  await article.locator("textarea").fill(reason);
  await button.focus();
  check(
    await button.evaluate((n) => n === document.activeElement),
    "keyboard-exact-action-focused-" + kind,
  );
  await page.keyboard.press(key);
  const after = await waitTask(
    page,
    run,
    id,
    old + 1,
    expected.recordedStatus(entry?.status, kind),
  );
  const saved = after.planningTasks.entries.find(
      (e) => e.identity.taskId === id,
    ),
    event = saved.history.at(-1);
  equal(event.kind, kind, "keyboard-actual-accepted-kind-" + kind);
  equal(event.reason, reason, "keyboard-actual-exact-reason-" + kind);
  equal(event.revision, old + 1, "keyboard-exact-one-new-revision-" + kind);
  equal(
    event.attestations,
    before.planningTasks.options.find((o) => o.identity.taskId === id)
      .currentAttestations,
    "keyboard-command-observes-exact-current-three-vector-" + kind,
  );
  equal(
    event.source,
    before.planningTasks.source,
    "keyboard-command-observes-exact-current-full-source-" + kind,
  );
  if (kind === "Create" || kind === "ReconfirmPlan")
    equal(
      saved.plan.eventId,
      event.eventId,
      "keyboard-explicit-plan-event-" + kind,
    );
  else
    equal(saved.plan, entry.plan, "keyboard-workflow-preserves-plan-" + kind);
  equal(
    sansTasks(after),
    sansTasks(before),
    "keyboard-no-original-product-or-artifact-effects-" + kind,
  );
  await until(
    () =>
      page
        .locator("#planning-task-" + id)
        .evaluate((n) => n === document.activeElement),
    Boolean,
    "keyboard-saved-task-heading-focus-" + kind,
  );
  await visibleTasks(page, run, after);
  return after;
}
function sansTasks(d) {
  const c = structuredClone(d);
  delete c.planningTasks;
  return c;
}
function gate() {
  let release, captured, completed;
  const value = {
    release: new Promise((r) => (release = r)),
    captured: (x) => captured(x),
    completed: () => completed(),
  };
  const seen = new Promise((r) => (captured = r)),
    done = new Promise((r) => (completed = r));
  pendingReleases.add(release);
  return { value, seen, done, release };
}
async function reviewOption(run, d, opt, headers) {
  for (const id of opt.identity.artifactIds) {
    const fresh = await analysis(run.runId),
      e = fresh.artifactReview.artifacts.find((a) => a.artifactId === id);
    if (e.state === "ReviewedForPlanning") continue;
    const r = await artifactPost(
      run,
      id,
      {
        eventId: randomUUID(),
        kind: "ReviewForPlanning",
        expectedRevision: e.revision,
        expectedSourceDigest: fresh.artifactReview.source.sourceDigest,
        reason: "Fictional selected artifact review",
      },
      headers,
    );
    equal(r.status(), 200, "actual-current-selected-three-review");
  }
}
async function prepareHistoricalBrowserFixture() {
  await mkdir(path.join(directory, ".host"), { recursive: true });
  const output = path.join(directory, ".host/historical-artifact-run.json");
  const child = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [
      path.join(
        workdir,
        "tests/integration/LocalPlanningTasks.Tests/bin/Release/net10.0/LocalPlanningTasks.IntegrationTests.dll",
      ),
      "--prepare-browser-fixture",
      output,
    ],
    {
      cwd: workdir,
      env: {
        ...process.env,
        IGA_PLANNING_TASK_TEST_DATABASE: process.env.IGA_SYNTHETIC_DATABASE,
      },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  const logs = [];
  child.stdout.on("data", (data) => logs.push(data.toString()));
  child.stderr.on("data", (data) => logs.push(data.toString()));
  const ended = once(child, "exit");
  try {
    const [code] = await bounded(
      ended,
      "owned-historical-fixture-process-completed",
    );
    equal(code, 0, "actual-historical-fixture-process-success");
  } finally {
    if (child.exitCode === null && child.signalCode === null && child.pid) {
      child.kill("SIGKILL");
      try {
        await ended;
      } catch {}
    }
    await writeFile(
      path.join(directory, ".host/historical-fixture.log"),
      logs.join(""),
    );
  }
  return JSON.parse(await readFile(output, "utf8"));
}
try {
  const historicalArtifactRun = await prepareHistoricalBrowserFixture();
  known.add(historicalArtifactRun.runId);
  await launch(false);
  api = await request.newContext({ baseURL: base });
  let catalog = await (await api.get("/local-demo/v1/catalog")).json();
  let headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const disabled = await api.post("/local-demo/v1/runs", {
    headers,
    data: {
      scopeId: "demo-scope",
      baselineId: "synthetic-analysis-findings-v1",
      profileId: expected.profile,
      requestId: randomUUID(),
    },
  });
  check(
    disabled.status() >= 400,
    "new-task-start-denied-without-explicit-host-activation",
  );
  await api.dispose();
  api = null;
  await stop();
  await launch();
  api = await request.newContext({ baseURL: base });
  catalog = await (await api.get("/local-demo/v1/catalog")).json();
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
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
  await showAssessments(page);
  const runs = [];
  for (const baseline of [
    "synthetic-analysis-healthy-v1",
    "synthetic-analysis-gaps-v1",
    "synthetic-analysis-findings-v1",
    "synthetic-analysis-mixed-v1",
  ]) {
    const run = await start(baseline, expected.profile, headers);
    runs.push(run);
    const d = await freshPage(page, run);
    await fullVisible(page, run, d);
    await visibleOverlay(page, run, d);
    await visibleTasks(page, run, d);
    equal(
      d.planningTasks.entries,
      [],
      "no-auto-created-tasks-on-any-saved-preset",
    );
    equal(
      await taskRead(run),
      d.planningTasks,
      "actual-analysis-and-standalone-taskDTO-complete-parity",
    );
  }
  group(
    "TC14-T01/T03/T08 four-real-saved-presets-full-source-and-visible-options",
  );
  const normal = runs[2],
    secondary = runs[3];
  let detail = await freshPage(page, normal);
  const target = detail.planningTasks.options[0].identity.taskId;
  let option = detail.planningTasks.options[0];
  await reviewOption(normal, detail, option, headers);
  detail = await freshPage(page, normal);
  await visibleTasks(page, normal, detail);
  const invariant = sansTasks(detail);
  const originalCreate = commandFor(detail, target);
  let response = await taskPost(normal, target, originalCreate, headers);
  equal(response.status(), 200, "actual-explicit-create");
  const originalReceipt = (await response.json()).receipt;
  detail = await freshPage(page, normal);
  await visibleTasks(page, normal, detail);
  equal(
    sansTasks(detail),
    invariant,
    "task-conversion-no-health-maturity-finding-review-guidance-artifact-report-effect",
  );
  detail = await act(page, normal, target, "StartProgress");
  const currentRev = detail.planningTasks.entries.find(
    (e) => e.identity.taskId === target,
  ).revision;
  const duplicate = commandFor(detail, target);
  response = await taskPost(normal, target, duplicate, headers);
  equal(
    await response.json(),
    {
      schemaVersion: 1,
      demoOnly: true,
      issue: null,
      alreadyApplied: false,
      receipt: null,
      alreadyExistsTaskId: target,
    },
    "typed-fresh-duplicate-revision0-after-progress-identity-only",
  );
  detail = await freshPage(page, normal);
  equal(
    detail.planningTasks.entries.find((e) => e.identity.taskId === target)
      .revision,
    currentRev,
    "duplicate-no-second-task-event",
  );
  response = await taskPost(normal, target, originalCreate, headers);
  const replay = await response.json();
  check(
    replay.alreadyApplied && replay.receipt.revision === 1,
    "exact-create-replay-original-historical-metadata",
  );
  equal(
    replay.receipt,
    originalReceipt,
    "original-receipt-exact-byte-value-replay",
  );
  detail = await act(page, normal, target, "ReturnToPlanned");
  detail = await act(page, normal, target, "StartProgress");
  detail = await act(page, normal, target, "Complete");
  detail = await act(page, normal, target, "Comment");
  detail = await act(page, normal, target, "Reopen");
  detail = await act(page, normal, target, "Cancel");
  detail = await act(page, normal, target, "Comment");
  detail = await act(page, normal, target, "Reopen");
  equal(
    sansTasks(detail),
    invariant,
    "all-workflow-comments-completion-reopen-no-original-health-or-artifact-review-effect",
  );
  group(
    "TC14-T01/T03/T04/T11 explicit-workflow-duplicate-historical-replay-noeffects",
  );
  // Malformed committed receipt is uncertain; retry must preserve the exact event and bytes.
  const malformedReason = "Fictional malformed receipt reason";
  taskReceiptMutation = (r) => ({
    ...r,
    receipt: { ...r.receipt, recordedAtUtc: "2026-10-03" },
  });
  const a = taskArticle(page, target);
  await a.locator("textarea").fill(malformedReason);
  await a.getByRole("button", { name: "Add comment", exact: true }).click();
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .waitFor();
  const posted = taskPosts.at(-1);
  const uncertain = await analysis(normal.runId),
    uncertainRev = uncertain.planningTasks.entries.find(
      (e) => e.identity.taskId === target,
    ).revision;
  await until(
    () =>
      page
        .locator("#planning-task-error-" + target)
        .evaluate((n) => n === document.activeElement),
    Boolean,
    "malformed-receipt-error-focused",
  );
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .click();
  await until(
    () => taskPosts.length,
    (n) => n >= 2 && taskPosts.at(-1) !== posted,
    "identical-retry-dispatched",
  );
  equal(
    taskPosts.at(-1),
    posted,
    "uncertain-retry-identical-original-command-eventUUID-source-vector-reason",
  );
  await until(
    () =>
      a
        .getByRole("button", {
          name: "Retry same planning command",
          exact: true,
        })
        .count(),
    (n) => n === 0,
    "verified-exact-replay-clears-uncertainty",
  );
  detail = await analysis(normal.runId);
  equal(
    detail.planningTasks.entries.find((e) => e.identity.taskId === target)
      .revision,
    uncertainRev,
    "committed-malformed-receipt-retry-never-duplicates-event",
  );
  await visibleTasks(page, normal, detail);
  // A truly lost committed response follows the same uncertain flow.
  dropTask = true;
  await a.locator("textarea").fill("Fictional lost committed comment");
  await a.getByRole("button", { name: "Add comment", exact: true }).click();
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .waitFor();
  const lost = taskPosts.at(-1);
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .click();
  await until(
    () =>
      a
        .getByRole("button", {
          name: "Retry same planning command",
          exact: true,
        })
        .count(),
    (n) => n === 0,
    "lost-receipt-exact-retry-completes",
  );
  equal(taskPosts.at(-1), lost, "lost-receipt-retry-exact-frozen-command");
  group(
    "TC14-T04/T06/T10 committed-malformed-and-lost-response-exact-retry-focus",
  );
  // Same-run review epoch race: comment commits while a task receipt is withheld.
  detail = await analysis(normal.runId);
  const sameRunRevision = normal.revision;
  const held = gate();
  taskHold = held.value;
  const heldReason = "Fictional held same-run planning comment";
  await a.locator("textarea").fill(heldReason);
  await a.getByRole("button", { name: "Add comment", exact: true }).click();
  await held.seen;
  const frozen = taskPosts.at(-1);
  const findingId = detail.planningTasks.options.find(
    (o) => o.identity.taskId === target,
  ).identity.findingId;
  const finding = page
    .locator(".finding-review")
    .filter({ has: page.locator("#review-" + findingId + "-action") });
  // Locate the actual existing review form by its closed action control suffix.
  const reviewSelect = page
    .locator('select[id$="-action"]')
    .filter({ has: page.locator('option[value="Comment"]') })
    .first();
  await reviewSelect.selectOption("Comment");
  const reviewForm = reviewSelect.locator("xpath=ancestor::form");
  await reviewForm
    .getByLabel("Comment", { exact: true })
    .fill("Fictional same-run source refresh");
  await reviewForm
    .getByRole("button", { name: "Add comment", exact: true })
    .click();
  const newer = await until(
    () => analysis(normal.runId),
    (d) =>
      d.planningTasks.source.artifactSource.sourceDigest !==
      frozen.command.expectedSourceDigest,
    "finding-comment-committed-new-source-at-unchanged-runrevision",
  );
  equal(
    newer.runRevision,
    sameRunRevision,
    "same-run-review-epoch-no-run-revision-change",
  );
  await until(
    () => planning(page).textContent(),
    (t) => t.includes(newer.planningTasks.source.artifactSource.sourceDigest),
    "new-source-visible-before-old-task-receipt-release",
  );
  held.release();
  await held.done;
  taskHold = null;
  await tick(page);
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .waitFor();
  equal(
    await a.locator("textarea").inputValue(),
    heldReason,
    "same-run-epoch-preserves-entered-reason",
  );
  check(
    (await a.getByRole("status").textContent()).includes("withheld"),
    "same-run-stale-receipt-never-restores-current-plan-label",
  );
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .click();
  await until(
    () =>
      a
        .getByRole("button", {
          name: "Retry same planning command",
          exact: true,
        })
        .count(),
    (n) => n === 0,
    "same-run-old-receipt-explicit-exact-retry-completes",
  );
  equal(
    taskPosts.at(-1),
    frozen,
    "same-run-explicit-retry-never-rebinds-old-command",
  );
  detail = await analysis(normal.runId);
  equal(
    detail.planningTasks.entries.find((e) => e.identity.taskId === target)
      .freshness,
    "NeedsReconfirmation",
    "same-run-old-replay-remains-stale",
  );
  await visibleTasks(page, normal, detail);
  // A stale task can comment and cancel; start/complete remain absent.
  equal(
    await a.getByRole("button", { name: "Start work", exact: true }).count(),
    0,
    "stale-no-start-action",
  );
  await act(page, normal, target, "Comment");
  detail = await act(page, normal, target, "Cancel");
  detail = await act(page, normal, target, "Reopen");
  await reviewOption(
    normal,
    detail,
    detail.planningTasks.options.find((o) => o.identity.taskId === target),
    headers,
  );
  detail = await freshPage(page, normal);
  detail = await act(page, normal, target, "ReconfirmPlan");
  equal(
    detail.planningTasks.entries.find((e) => e.identity.taskId === target)
      .freshness,
    "CurrentPlan",
    "explicit-reconfirmation-only-restores-current-plan",
  );
  group(
    "TC14-T03/T06/T10 same-run-held-receipt-review-epoch-stale-maintenance-reconfirm",
  );
  // Retain reason and uncertain exact command across a real run switch; late response fenced.
  const switched = gate();
  taskHold = switched.value;
  await a.locator("textarea").fill("Fictional run-switched pending comment");
  await a.getByRole("button", { name: "Add comment", exact: true }).click();
  await switched.seen;
  const switchedCommand = taskPosts.at(-1);
  const runButton = page.getByRole("button", {
    name: "Select run " + secondary.runId,
    exact: true,
  });
  const actualSelect = page
    .locator("tr")
    .filter({
      has: page.locator(
        "time[datetime=" + JSON.stringify(secondary.createdAtUtc) + "]",
      ),
    })
    .getByRole("button");
  await actualSelect.click();
  await shown(page, secondary.runId);
  switched.release();
  await switched.done;
  taskHold = null;
  await tick(page);
  await shown(page, secondary.runId);
  check(
    !(await planning(page).textContent()).includes(target),
    "late-other-run-receipt-never-restores-old-task",
  );
  await page
    .locator("tr")
    .filter({
      has: page.locator(
        "time[datetime=" + JSON.stringify(normal.createdAtUtc) + "]",
      ),
    })
    .getByRole("button")
    .click();
  await shown(page, normal.runId);
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .waitFor();
  equal(
    await a.locator("textarea").inputValue(),
    switchedCommand.command.reason,
    "volatile-parent-draft-survives-run-child-unmount",
  );
  await a
    .getByRole("button", { name: "Retry same planning command", exact: true })
    .click();
  await until(
    () =>
      a
        .getByRole("button", {
          name: "Retry same planning command",
          exact: true,
        })
        .count(),
    (n) => n === 0,
    "run-switched-identical-retry-completes",
  );
  equal(
    taskPosts.at(-1),
    switchedCommand,
    "run-switched-retry-exact-original-command",
  );
  group(
    "TC14-T06/T10 run-switch-late-receipt-fence-and-retained-volatile-draft",
  );
  // Closed host body, authority and exact route body bounds; these cannot append history.
  detail = await analysis(normal.runId);
  const beforeNegative = detail.planningTasks.entries;
  const valid = commandFor(detail, target, "Comment");
  for (const [code, data, heads, status] of [
    ["missing-csrf", valid, { Origin: base }, 403],
    [
      "foreign-origin",
      valid,
      { ...headers, Origin: "https://fixture.invalid" },
      403,
    ],
    ["client-actor", { ...valid, actorId: "foreign-consultant" }, headers, 400],
    ["unknown-kind", { ...valid, kind: "Execute" }, headers, 400],
    [
      "duplicate-vector",
      {
        ...valid,
        expectedAttestations: [
          valid.expectedAttestations[0],
          valid.expectedAttestations[0],
          valid.expectedAttestations[2],
        ],
      },
      headers,
      400,
    ],
    [
      "foreign-source",
      { ...valid, expectedSourceDigest: "f".repeat(64) },
      headers,
      409,
    ],
    [
      "unsafe-revision",
      { ...valid, expectedRevision: 9007199254740992 },
      headers,
      400,
    ],
  ]) {
    response = await taskPost(normal, target, data, heads);
    equal(response.status(), status, "host-negative-" + code);
  }
  const raw = JSON.stringify(valid);
  for (const [code, body] of [
    [
      "reason-lone-surrogate",
      raw.replace(JSON.stringify(valid.reason), '"\\ud800"'),
    ],
    [
      "nested-value-lone-surrogate",
      raw.replace('"ReviewedForPlanning"', '"\\ud800"'),
    ],
    ["nested-name-lone-surrogate", raw.replace('"artifactId"', '"\\ud800"')],
    [
      "invalid-UTF8",
      Buffer.concat([
        Buffer.from(raw.slice(0, -1)),
        Buffer.from([0xc0, 0xaf]),
        Buffer.from("}"),
      ]),
    ],
  ]) {
    response = await api.post(
      `/local-demo/v1/runs/${normal.runId}/planning-tasks/${target}/events`,
      {
        headers: { ...headers, "Content-Type": "application/json" },
        data: body,
      },
    );
    equal(response.status(), 400, "host-malformed-string-" + code);
  }
  response = await api.post(
    `/local-demo/v1/runs/${normal.runId}/planning-tasks/${target}/events`,
    {
      headers: { ...headers, "Content-Type": "application/json" },
      data: raw + " ".repeat(16384),
    },
  );
  equal(response.status(), 400, "task-route-exact16KiB-envelope-bound");
  response = await api.post(
    `/local-demo/v1/runs/${normal.runId}/findings/${findingId}/events`,
    {
      headers: { ...headers, "Content-Type": "application/json" },
      data:
        JSON.stringify({
          eventId: randomUUID(),
          expectedRevision: detail.review.findings.find(
            (f) => f.id === findingId,
          ).revision,
          kind: "Comment",
          reason: null,
          text: "Fictional otherwise valid oversized old-route comment",
          title: null,
          businessContext: null,
        }) + " ".repeat(4097),
    },
  );
  equal(response.status(), 400, "old-finding-route-retains4096-byte-bound");
  equal(
    (await analysis(normal.runId)).planningTasks.entries,
    beforeNegative,
    "all-host-negative-corpus-no-accepted-history",
  );

  detail = await analysis(normal.runId);
  const largeReason = "F" + "\0".repeat(1999),
    large = commandFor(detail, target, "Comment", largeReason);
  check(
    Buffer.byteLength(JSON.stringify(large)) > 4096 &&
      Buffer.byteLength(JSON.stringify(large)) <= 16384,
    "independent-large-valid-task-wire-envelope",
  );
  response = await taskPost(normal, target, large, headers);
  equal(
    response.status(),
    200,
    "actual-new-task-route-allows-valid-16KiB-bound-beyond-old4096",
  );
  equal(
    (await analysis(normal.runId)).planningTasks.entries
      .find((e) => e.identity.taskId === target)
      .history.at(-1).reason,
    largeReason,
    "exact2000UTF16-control-reason-saved",
  );

  group(
    "TC14-T02/T07/T12 actualHTTP-closed-input-authority-UTF16-UTF8-routebounds-noeffects",
  );
  // Real trusted persisted control/hostile reason is React text, never parsed HTML.
  detail = await analysis(normal.runId);
  const hostileReason =
    'Fictional <script>globalThis.v14Injected=1</script><img src="https://fixture.invalid/x" onerror="globalThis.v14Injected=2"> café 中文 😀\0NUL\r\nCRLF\rCR';
  response = await taskPost(
    normal,
    target,
    commandFor(detail, target, "Comment", hostileReason),
    headers,
  );
  equal(response.status(), 200, "actual-hostile-control-comment-saved");
  detail = await freshPage(page, normal);
  await visibleTasks(page, normal, detail);
  check(
    (await a.locator(".planning-task-reason").allTextContents()).some(
      (t) => t === "Reason or comment: " + hostileReason,
    ),
    "React-text-originalNUL-CR-CRLF-Unicode-exact-fidelity",
  );
  await inertTasks(page);

  // Closed unavailable transport metadata is intentionally intercepted; native
  // HostRecoveryCases separately verifies the actual persisted-corruption path.
  const availableDetail = detail;
  standaloneMutation = (value) => ({
    schemaVersion: 1,
    demoOnly: true,
    status: "SourceUnavailable",
    reasonCode: "planning_task_source_unavailable",
    source: null,
    actorId: value.actorId,
    options: [],
    entries: [],
    unavailableEntries: value.entries.map((e) => ({
      identity: e.identity,
      assigneeId: e.assigneeId,
      revision: e.revision,
      status: e.status,
      freshness: "SourceUnavailable",
      history: e.history.map((h) => ({
        eventId: h.eventId,
        revision: h.revision,
        kind: h.kind,
        actorId: h.actorId,
        actorRoles: h.actorRoles,
        recordedAtUtc: h.recordedAtUtc,
        recordedStatus: h.recordedStatus,
        planningEventId: h.planningEventId,
      })),
    })),
  });
  denyAnalysis = true;
  await select(page, normal.runId);
  await until(
    () => planning(page).textContent(),
    (t) =>
      t?.includes(
        "Current source unavailable. Historical task metadata is shown without content or actions.",
      ),
    "intercepted-unavailable-metadata-visible",
  );
  equal(
    await planning(page).locator("textarea,button,code").count(),
    0,
    "unavailable-source-no-reason-actions-or-artifact-text",
  );
  await planning(page)
    .locator("details")
    .evaluateAll((nodes) => nodes.forEach((n) => (n.open = true)));
  for (const e of availableDetail.planningTasks.entries) {
    const art = taskArticle(page, e.identity.taskId);
    check(
      (await art.textContent()).includes(
        `Assignee: ${e.assigneeId} · Revision ${e.revision}`,
      ),
      "unavailable-verified-owner-revision-readable",
    );
    const history = art.locator("ol > li");
    equal(
      await history.count(),
      e.history.length,
      "unavailable-complete-metadata-history",
    );
    for (let i = 0; i < e.history.length; i++) {
      const h = e.history[i];
      equal(
        await history.nth(i).locator("p").allTextContents(),
        [
          `Event: ${h.eventId}`,
          `Actor: ${h.actorId} · Roles: ${h.actorRoles.join(", ")}`,
          `Recorded: ${h.recordedAtUtc}`,
          `Recorded status: ${statuses[h.recordedStatus]}`,
          `Planning event: ${h.planningEventId}`,
        ],
        "unavailable-only-approved-metadata-readable",
      );
      check(
        !(await history.nth(i).textContent()).includes(h.reason),
        "unavailable-reason-suppressed",
      );
    }
  }
  denyAnalysis = false;
  standaloneMutation = null;
  await page
    .getByRole("button", { name: "Retry analysis", exact: true })
    .click();
  await shown(page, normal.runId);
  detail = await analysis(normal.runId);
  await visibleTasks(page, normal, detail);
  group(
    "TC14-T07/T10 intercepted-closed-unavailable-metadata-no-content-or-actions",
  );

  // Full-context guard negatives; expected byte fixtures are unchanged.
  for (const [code, change] of [
    [
      "extra-detail",
      (d) => {
        d.planningTasks.extra = true;
        return d;
      },
    ],
    [
      "foreign-run-source",
      (d) => {
        d.planningTasks.source.artifactSource.runId = secondary.runId;
        return d;
      },
    ],
    [
      "contract-lock",
      (d) => {
        d.planningTasks.source.planningTaskContractDigest = "f".repeat(64);
        return d;
      },
    ],
    [
      "missing-vector",
      (d) => {
        d.planningTasks.options[0].currentAttestations.pop();
        return d;
      },
    ],
    [
      "task-identity",
      (d) => {
        d.planningTasks.options[0].identity.taskId = "f".repeat(64);
        return d;
      },
    ],
    [
      "unsafe-history",
      (d) => {
        d.planningTasks.entries[0].history[0].revision = 9007199254740992;
        return d;
      },
    ],
    [
      "history-role",
      (d) => {
        d.planningTasks.entries[0].history[0].actorRoles = ["Auditor"];
        return d;
      },
    ],
    [
      "fake-current",
      (d) => {
        d.planningTasks.entries[0].status = "Completed";
        return d;
      },
    ],
    [
      "fullcanonical",
      (d) => {
        d.fixPackages.snapshot.canonicalJson = "{}";
        return d;
      },
    ],
    [
      "current-capability",
      (d) => {
        d.planningTasks.options[0].canCreate =
          !d.planningTasks.options[0].canCreate;
        return d;
      },
    ],
    [
      "history-nonUTC",
      (d) => {
        d.planningTasks.entries[0].history[0].recordedAtUtc = "2026-10-03";
        return d;
      },
    ],
  ]) {
    await badCase(page, normal, change, "TC14-T07 client-denied-" + code);
  }
  detail = await freshPage(page, normal);
  await visibleTasks(page, normal, detail);
  group(
    "TC14-T07/T09 literal-hostile-controls-and-closed-current-history-digest-guards",
  );
  // All ten historical profiles remain unchanged and never receive the new analysis member.
  for (const [baseline, profile] of [
    ["baseline-complete", "profile-standard"],
    ["baseline-complete", "profile-comparison"],
    ["synthetic-analysis-findings-v1", "synthetic-analysis-equal-v1"],
    ["synthetic-analysis-findings-v1", "synthetic-analysis-operations-v1"],
    ["synthetic-analysis-findings-v1", "synthetic-review-maturity-equal-v1"],
    [
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-operations-v1",
    ],
    ["baseline-ai-configuration-v1", "profile-ai-preview-v1"],
    ["baseline-ai-configuration-v1", "profile-ai-preview-empty-v1"],
    [
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-fix-packages-equal-v1",
    ],
    [
      "synthetic-analysis-findings-v1",
      "synthetic-review-maturity-fix-review-equal-v1",
    ],
  ]) {
    const old =
      profile === "synthetic-review-maturity-fix-review-equal-v1"
        ? historicalArtifactRun
        : await start(baseline, profile, headers);
    const oldD = await analysis(old.runId);
    check(
      !Object.hasOwn(oldD, "planningTasks"),
      "old-ten-analysis-new-task-property-omitted",
    );
    check(
      !old.lockedInputs.some((l) => l.name === "Planning task contract"),
      "old-ten-new-task-lock-omitted",
    );
  }
  group(
    "TC14-T08 ten-historical-profiles-additive-field-omission-no-promotion",
  );
  // Every workflow action is activated through actual native keyboard behavior.
  let keyboardDetail = await freshPage(page, secondary);
  const keyboardOption = keyboardDetail.planningTasks.options.find(
    (o) =>
      o.canCreate ||
      !keyboardDetail.planningTasks.entries.some(
        (e) => e.identity.taskId === o.identity.taskId,
      ),
  );
  check(
    Boolean(keyboardOption),
    "independent-secondary-keyboard-option-present",
  );
  const keyboardTask = keyboardOption.identity.taskId;
  await reviewOption(secondary, keyboardDetail, keyboardOption, headers);
  keyboardDetail = await freshPage(page, secondary);
  await visibleTasks(page, secondary, keyboardDetail);
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Create",
    "Space",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "StartProgress",
    "Enter",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Complete",
    "Space",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Reopen",
    "Enter",
  );
  // An actual saved finding comment stales the plan without changing run revision.
  const keyboardOldSource =
      keyboardDetail.planningTasks.source.artifactSource.sourceDigest,
    keyboardRunRevision = keyboardDetail.runRevision;
  const keyboardReviewSelect = page
    .locator('select[id$="-action"]')
    .filter({ has: page.locator('option[value="Comment"]') })
    .first();
  await keyboardReviewSelect.selectOption("Comment");
  const keyboardReviewForm = keyboardReviewSelect.locator(
    "xpath=ancestor::form",
  );
  await keyboardReviewForm
    .getByLabel("Comment", { exact: true })
    .fill("Fictional keyboard source refresh");
  const keyboardReviewButton = keyboardReviewForm.getByRole("button", {
    name: "Add comment",
    exact: true,
  });
  await keyboardReviewButton.focus();
  await page.keyboard.press("Enter");
  keyboardDetail = await until(
    () => analysis(secondary.runId),
    (d) =>
      d.planningTasks.source.artifactSource.sourceDigest !== keyboardOldSource,
    "keyboard-finding-comment-real-source-refresh",
  );
  equal(
    keyboardDetail.runRevision,
    keyboardRunRevision,
    "keyboard-stale-source-unchanged-runrevision",
  );
  await until(
    () => planning(page).textContent(),
    (t) =>
      t.includes(
        keyboardDetail.planningTasks.source.artifactSource.sourceDigest,
      ),
    "keyboard-fresh-source-visible-before-stale-action",
  );
  equal(
    keyboardDetail.planningTasks.entries.find(
      (e) => e.identity.taskId === keyboardTask,
    ).freshness,
    "NeedsReconfirmation",
    "keyboard-task-actually-stale-before-maintenance",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Comment",
    "Space",
  );
  await reviewOption(
    secondary,
    keyboardDetail,
    keyboardDetail.planningTasks.options.find(
      (o) => o.identity.taskId === keyboardTask,
    ),
    headers,
  );
  keyboardDetail = await freshPage(page, secondary);
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "ReconfirmPlan",
    "Space",
  );
  equal(
    keyboardDetail.planningTasks.entries.find(
      (e) => e.identity.taskId === keyboardTask,
    ).freshness,
    "CurrentPlan",
    "keyboard-explicit-reconfirm-restores-current-only",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "StartProgress",
    "Space",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "ReturnToPlanned",
    "Enter",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Cancel",
    "Space",
  );
  keyboardDetail = await keyboardAct(
    page,
    secondary,
    keyboardTask,
    "Reopen",
    "Space",
  );
  group(
    "TC14-T03/T10 actual-Enter-Space-eight-workflow-actions-stale-comment-explicit-reconfirm-focus-noeffects",
  );
  const beforeRestart = await analysis(normal.runId);
  await api.dispose();
  api = null;
  await stop();
  await launch();
  api = await request.newContext({ baseURL: base });
  catalog = await (await api.get("/local-demo/v1/catalog")).json();
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const afterRestart = await analysis(normal.runId);
  equal(
    afterRestart.planningTasks,
    beforeRestart.planningTasks,
    "actual-own-host-restart-durable-full-task-source-history-byte-values",
  );
  response = await taskPost(normal, target, originalCreate, headers);
  equal(
    (await response.json()).receipt,
    originalReceipt,
    "own-host-restart-original-historical-receipt",
  );
  detail = await freshPage(page, normal);
  await visibleTasks(page, normal, detail);
  await mkdir(path.join(directory, ".host/observed"), { recursive: true });
  await writeFile(
    path.join(directory, ".host/observed/final-saved-task-source.json"),
    JSON.stringify(
      {
        schema: "v14-observed-browser-values-v1",
        savedRun: normal,
        originalCreate,
        originalReceipt,
        finalAnalysis: detail,
        beforeRestart,
        afterRestart,
        taskPosts,
      },
      null,
      2,
    ) + "\n",
  );
  for (const [name, width, height] of [
    ["desktop", 1440, 1000],
    ["mobile", 390, 844],
    ["narrow", 320, 740],
  ]) {
    await page.setViewportSize({ width, height });
    await fullVisible(page, normal, detail);
    await visibleOverlay(page, normal, detail);
    await visibleTasks(page, normal, detail);
    const r = planning(page);
    await r.scrollIntoViewIfNeeded();
    check(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      "page-" + name + "-reflow",
    );
    const summary = r.locator("summary").first();
    await summary.focus();
    await page.keyboard.press("Enter");
    check(
      !(await summary.evaluate((n) => n.parentElement.open)),
      "task-native-keyboard-disclosure-toggle",
    );
    await page.keyboard.press("Enter");
    const reason = taskArticle(page, target).locator("textarea");
    await reason.fill("Fictional " + name + " keyboard comment");
    await reason.focus();
    await page.keyboard.press("Tab");
    check(
      await taskArticle(page, target)
        .getByRole("button")
        .first()
        .evaluate((n) => n === document.activeElement),
      "task-reason-to-action-keyboard-order",
    );
    const actionsViewport = path.join(directory, name + "-task-viewport.png");
    await page.screenshot({ path: actionsViewport, caret: "initial" });
    screenshots.push(actionsViewport);
    await inertTasks(page);
    await page.evaluate(axeSource);
    const axe = await page.evaluate(async () =>
      globalThis.axe.run(".planning-tasks", {
        runOnly: {
          type: "tag",
          values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
        },
      }),
    );
    accessibility.push({
      viewport: name,
      violations: axe.violations.map((v) => ({
        id: v.id,
        count: v.nodes.length,
      })),
      incomplete: axe.incomplete.map((v) => ({
        id: v.id,
        count: v.nodes.length,
      })),
    });
    equal(axe.violations.length, 0, "axe-" + name + "-engineering-subset-zero");
    await inertTasks(page);
    const full = path.join(directory, name + ".png");
    await r.screenshot({ path: full, caret: "initial" });
    screenshots.push(full);
    await page.locator("#planning-tasks-heading").scrollIntoViewIfNeeded();
    const viewport = path.join(directory, name + "-viewport.png");
    await page.screenshot({ path: viewport, caret: "initial" });
    screenshots.push(viewport);
  }
  const logs = ownedHostLogChunks.join("");
  check(
    !taskPosts.some((p) => logs.includes(p.command.reason)) &&
      !logs.includes(hostileReason),
    "actual-host-telemetry-no-planning-reason-comment-artifact-payload",
  );
  await writeFile(path.join(directory, ".host/host-runtime.log"), logs);
  equal(blocked.length, 0, "zero-unexpected-or-external-request-attempts");
  equal(dialogs.length, 0, "zero-injected-dialogs");
  equal(faults.length, 0, "zero-browser-or-owned-host-faults");
  group(
    "TC14-T06/T09/T10 actual-own-restart-inert-keyboard-reflow-axe-three-viewports",
  );
  console.log("PASS V14 planning-tasks browser; checks=" + checks);
} catch (error) {
  console.error(
    "FAIL V14 browser; code=" +
      last +
      "; checks=" +
      checks +
      "; exception=" +
      error.constructor.name +
      "; payload-suppressed",
  );
  process.exitCode = 1;
  console.error(
    "DIAGNOSTIC browser-error; strict-selector=" +
      String(error.message.includes("strict mode violation")) +
      "; timeout=" +
      String(error.message.includes("Timeout")),
  );
} finally {
  for (const release of pendingReleases) release();
  try {
    if (context) await context.close();
  } finally {
    try {
      if (browser) await browser.close();
    } finally {
      try {
        if (api) await api.dispose();
      } finally {
        await stop();
      }
    }
  }
  await mkdir(path.join(directory, ".host"), { recursive: true });
  await writeFile(
    path.join(directory, ".host/host-runtime.log"),
    ownedHostLogChunks.join(""),
  );
  await writeFile(
    path.join(directory, "execution.json"),
    JSON.stringify(
      {
        schema: "v14-browser-execution-v1",
        status: process.exitCode ? "FAIL" : "PASS",
        checks,
        lastCode: last,
        browserVersion: actualBrowserVersion,
        groups,
        blockedAttemptCodes: blocked,
        pageErrorCodes: faults,
        dialogCodes: dialogs,
        screenshots,
        accessibility,
        environmentStrategy:
          "closed allowlist before fixture/browser/host loading; no inherited marker bypass",
        removedEnvironmentKeys,
        sourceHash: expected.hash(
          await readFile(fileURLToPath(import.meta.url)),
        ),
        defaultMacStartupVerified:
          process.env.DOTNET_hostBuilder__reloadConfigOnChange === "false"
            ? false
            : "not-established",
        temporaryConfigurationWatchOverride:
          process.env.DOTNET_hostBuilder__reloadConfigOnChange === "false",
        limitations: [
          "Chromium macOS automated subset only; Windows/manual screen reader/deployed sandbox/production identity/full product gates NOT VERIFIED.",
          "Test-only axe injection isolated from product inert-node assertions.",
          "Observed synthetic captures never used as expected goldens; old counts not relabeled.",
        ],
      },
      null,
      2,
    ) + "\n",
  );
}
