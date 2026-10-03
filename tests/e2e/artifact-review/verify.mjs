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
  database: "iga_synthetic_cycle13_v13_v2",
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
      ...(enabled ? ["--enable-synthetic-artifact-review"] : []),
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
    /^\/local-demo\/v1\/runs\/([a-f0-9-]+)(?:\/(?:analysis|review))?$/.exec(
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
  const allowed =
    u.origin === base &&
    ((req.method() === "GET" &&
      (assets.has(u.pathname) ||
        u.pathname === "/local-demo/v1/catalog" ||
        u.pathname === "/local-demo/v1/runs" ||
        (readRoute && known.has(readRoute[1])))) ||
      (req.method() === "POST" &&
        ((event && known.has(event[1])) ||
          (action && known.has(action[1]) && artifactIds.has(action[2])))));
  if (!allowed) {
    blocked.push("unexpected-request");
    await route.abort();
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
try {
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
    "new-opt-in-start-denied-without-explicit-host-migration-activation",
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
    for (const entry of d.artifactReview?.artifacts ?? [])
      artifactIds.add(entry.artifactId);
    await select(page, run.runId);
    if (d.fixPackages.status === "Ready") await fullVisible(page, run, d);
    else {
      await region(page).getByRole("status").waitFor();
      equal(
        await region(page).locator("code,article").count(),
        0,
        "gaps-unavailable-no-partial-originals",
      );
    }
    await visibleOverlay(page, run, d);
  }
  group(
    "AR13-T01/T03/T07 four-actual-presets-full-original-current-source-empty-boundaries",
  );
  const normal = runs[2];
  let detail = await analysis(normal.runId),
    target = detail.artifactReview.artifacts[0].artifactId;
  const original = structuredClone(detail.fixPackages.snapshot.packages),
    scores = structuredClone({
      provisional: detail.provisional,
      publishableCurrent: detail.publishableCurrent,
      categories: detail.categories,
      quality: detail.quality,
    }),
    originalFindings = detail.fixPackages.snapshot.guidance.findings.map(
      (f) => ({
        findingId: f.findingId,
        originalTitle: f.originalTitle,
        occurrences: f.occurrences,
      }),
    );
  await select(page, normal.runId);
  await visibleOverlay(page, normal, detail);
  const a = article(page, target);
  await a
    .locator("textarea")
    .fill("Fictional explicit browser planning review");
  await a
    .getByRole("button", { name: "Review for planning", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await waitCurrent(page, normal, target, "ReviewedForPlanning", 1);
  detail = await analysis(normal.runId);
  await fullVisible(page, normal, detail);
  await visibleOverlay(page, normal, detail);
  const firstPost = artifactPosts.at(-1);
  equal(
    Object.keys(firstPost.command).sort(),
    [
      "eventId",
      "expectedRevision",
      "expectedSourceDigest",
      "kind",
      "reason",
    ].sort(),
    "exact-five-field-browser-command-no-client-authority",
  );
  equal(
    firstPost.command.kind,
    "ReviewForPlanning",
    "explicit-review-command-kind",
  );
  equal(
    firstPost.command.expectedRevision,
    0,
    "explicit-first-artifact-revision-zero",
  );
  equal(
    firstPost.command.expectedSourceDigest,
    detail.artifactReview.artifacts.find((e) => e.artifactId === target)
      .history[0].source.sourceDigest,
    "command-attests-inspected-source",
  );
  check(
    await page
      .locator("#artifact-review-" + target)
      .evaluate((n) => n === document.activeElement),
    "successful-refresh-targeted-artifact-heading-focus",
  );
  await article(page, target)
    .locator("textarea")
    .fill("Fictional explicit browser withdrawal");
  await article(page, target)
    .getByRole("button", { name: "Withdraw planning review", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await waitCurrent(page, normal, target, "Unverified", 2);
  detail = await analysis(normal.runId);
  await visibleOverlay(page, normal, detail);
  equal(
    detail.fixPackages.snapshot.packages,
    original,
    "review-withdraw-never-mutates-original-artifact-content",
  );
  equal(
    {
      provisional: detail.provisional,
      publishableCurrent: detail.publishableCurrent,
      categories: detail.categories,
      quality: detail.quality,
    },
    scores,
    "artifact-events-never-change-health-quality",
  );
  const hostile =
    "<script>globalThis.v13Injected=true</script><img src='https://fixture.invalid/v13' onerror='globalThis.v13Injected=true'><form action='https://fixture.invalid/v13'></form><svg onload='globalThis.v13Injected=true'></svg> café 中文 😀 NUL[\0] CR[\r] CRLF[\r\n]";
  const command = {
    eventId: randomUUID(),
    kind: "ReviewForPlanning",
    expectedRevision: 2,
    expectedSourceDigest: detail.artifactReview.source.sourceDigest,
    reason: "  " + hostile + "  ",
  };
  const posted = await artifactPost(normal, target, command, headers);
  equal(posted.status(), 200, "actual-host-control-Unicode-reason-review");
  const receipt = (await posted.json()).receipt;
  equal(
    Object.keys(receipt).sort(),
    [
      "schemaVersion",
      "eventId",
      "runId",
      "artifactId",
      "kind",
      "revision",
      "actorId",
      "recordedAtUtc",
      "sourceDigest",
    ].sort(),
    "metadata-only-HTTP-receipt-closed",
  );
  const replay = await artifactPost(normal, target, command, headers);
  equal(replay.status(), 200, "exact-command-host-replay");
  const replayed = await replay.json();
  check(
    replayed.alreadyApplied,
    "replay-indicator-original-historical-receipt",
  );
  equal(
    replayed.receipt,
    receipt,
    "host-original-receipt-time-actor-preserved",
  );
  await select(page, normal.runId);
  detail = await analysis(normal.runId);
  await visibleOverlay(page, normal, detail);
  equal(
    await article(page, target)
      .locator(".artifact-review-reason")
      .last()
      .textContent(),
    "Reason: " + command.reason,
    "actual-React-text-node-NUL-CR-CRLF-original-reason-fidelity",
  );
  await inertOverlay(page);
  group(
    "AR13-T03/T04/T09 actual-review-withdraw-replay-control-reason-original-health-invariants",
  );
  const finding = detail.review.findings.find((f) => f.state === "Proposed");
  const update = {
    eventId: randomUUID(),
    expectedRevision: finding.revision,
    kind: "Comment",
    reason: null,
    text: "Fictional source refresh",
    title: null,
    businessContext: null,
  };
  const findingPost = await api.post(
    `/local-demo/v1/runs/${normal.runId}/findings/${finding.id}/events`,
    { headers, data: update },
  );
  equal(findingPost.status(), 200, "actual-finding-source-change");
  const changed = await analysis(normal.runId);
  equal(
    changed.runRevision,
    detail.runRevision,
    "review-source-change-same-run-revision",
  );
  check(
    changed.artifactReview.source.sourceDigest !==
      detail.artifactReview.source.sourceDigest,
    "complete-current-source-digest-changed",
  );
  equal(
    changed.artifactReview.artifacts.find((e) => e.artifactId === target).state,
    "NeedsReview",
    "finding-comment-invalidates-artifact-attestation-without-new-artifact-event",
  );
  equal(
    changed.artifactReview.artifacts.find((e) => e.artifactId === target)
      .history.length,
    3,
    "finding-comment-does-not-append-artifact-event",
  );
  await select(page, normal.runId);
  await visibleOverlay(page, normal, changed);
  equal(
    changed.fixPackages.snapshot.packages,
    original,
    "source-refresh-original-artifact-bytes-andIDs-unchanged",
  );
  equal(
    changed.fixPackages.snapshot.guidance.findings.map((f) => ({
      findingId: f.findingId,
      originalTitle: f.originalTitle,
      occurrences: f.occurrences,
    })),
    originalFindings,
    "original-findings-occurrences-never-rewritten",
  );
  const staleWithdraw = await artifactPost(
    normal,
    target,
    {
      ...command,
      eventId: randomUUID(),
      kind: "WithdrawReview",
      expectedRevision: 3,
      expectedSourceDigest: changed.artifactReview.source.sourceDigest,
    },
    headers,
  );
  check(staleWithdraw.status() >= 400, "stale-history-withdraw-route-denied");
  const freshStale = await artifactPost(
    normal,
    target,
    { ...command, eventId: randomUUID(), expectedRevision: 3 },
    headers,
  );
  check(freshStale.status() >= 400, "new-event-old-source-conflict-denied");
  const altered = await artifactPost(
    normal,
    target,
    { ...command, reason: "changed reuse" },
    headers,
  );
  check(altered.status() >= 400, "changed-event-reuse-conflict-denied");
  for (const data of [
    { ...command, actorId: "forged" },
    { ...command, roles: ["Consultant"] },
    { ...command, scope: { customerId: "foreign" } },
    { ...command, recordedAtUtc: new Date().toISOString() },
    { ...command, reason: " " },
    { ...command, reason: "a".repeat(2001) },
    { ...command, expectedRevision: 9007199254740992 },
    { ...command, kind: "Execute" },
    { ...command, extra: true },
  ]) {
    const bad = await artifactPost(normal, target, data, headers);
    check(bad.status() >= 400, "closed-host-command-negative-denied");
  }
  const missingCsrf = await artifactPost(
    normal,
    target,
    { ...command, eventId: randomUUID() },
    {},
  );
  check(missingCsrf.status() >= 400, "artifact-mutation-no-CSRF-denied");
  const foreignOrigin = await artifactPost(
    normal,
    target,
    { ...command, eventId: randomUUID() },
    { ...headers, Origin: "https://fixture.invalid" },
  );
  check(
    foreignOrigin.status() >= 400,
    "artifact-mutation-foreign-origin-denied",
  );
  const duplicate = JSON.stringify({
    ...command,
    eventId: randomUUID(),
  }).replace("{", '{"kind":"WithdrawReview",');
  const dup = await api.post(
    `/local-demo/v1/runs/${normal.runId}/artifacts/${target}/review`,
    {
      headers: { ...headers, "Content-Type": "application/json" },
      data: duplicate,
    },
  );
  check(dup.status() >= 400, "duplicate-JSON-command-field-denied");
  const afterDenied = await analysis(normal.runId);
  equal(
    afterDenied.artifactReview,
    changed.artifactReview,
    "all-host-denied-commands-zero-event-state-effects",
  );
  group(
    "AR13-T02/T03/T04/T11 host-exact-input-origin-CSRF-namespace-source-replay-boundaries",
  );
  for (const [code, change] of [
    [
      "missing-field",
      (d) => {
        delete d.artifactReview.actorId;
        return d;
      },
    ],
    [
      "extra-field",
      (d) => {
        d.artifactReview.extra = true;
        return d;
      },
    ],
    [
      "foreign-run",
      (d) => {
        d.artifactReview.source.runId = randomUUID();
        return d;
      },
    ],
    [
      "wrong-current-source",
      (d) => {
        d.artifactReview.source.sourceDigest = "0".repeat(64);
        return d;
      },
    ],
    [
      "wrong-current-vector",
      (d) => {
        d.artifactReview.source.findingRevisions[0].revision++;
        return d;
      },
    ],
    [
      "foreign-artifact",
      (d) => {
        d.artifactReview.artifacts[0].artifactId = "0".repeat(64);
        return d;
      },
    ],
    [
      "wrong-original-text-hash",
      (d) => {
        d.artifactReview.artifacts[0].artifactTextDigest = "0".repeat(64);
        return d;
      },
    ],
    [
      "duplicate-artifact",
      (d) => {
        d.artifactReview.artifacts.push(
          structuredClone(d.artifactReview.artifacts[0]),
        );
        return d;
      },
    ],
    [
      "changed-history-state",
      (d) => {
        d.artifactReview.artifacts.find(
          (e) => e.history.length,
        ).history[0].recordedState = "ReviewedForPlanning";
        d.artifactReview.artifacts.find(
          (e) => e.history.length,
        ).history[0].kind = "WithdrawReview";
        return d;
      },
    ],
    [
      "history-gap",
      (d) => {
        d.artifactReview.artifacts.find(
          (e) => e.history.length,
        ).history[0].revision = 9;
        return d;
      },
    ],
    [
      "history-role",
      (d) => {
        d.artifactReview.artifacts.find(
          (e) => e.history.length,
        ).history[0].actorRoles = ["QualifiedReviewer"];
        return d;
      },
    ],
    [
      "unsafe-revision",
      (d) => {
        d.artifactReview.artifacts[0].revision = 9007199254740992;
        return d;
      },
    ],
    [
      "forged-current-capability",
      (d) => {
        d.artifactReview.artifacts.find((e) => e.history.length).canWithdraw =
          true;
        return d;
      },
    ],
    [
      "corrupt-full-package-canonical",
      (d) => {
        d.fixPackages.snapshot.canonicalJson = "{}";
        return d;
      },
    ],
  ])
    await badCase(page, normal, change, "AR13-T07 client-deny-" + code);
  runMutation = (d) => {
    d.lockedInputs = d.lockedInputs.filter(
      (l) => l.name !== "Artifact review contract",
    );
    return d;
  };
  await select(page, normal.runId);
  await retryVisible(page);
  runMutation = null;
  await until(
    () =>
      page
        .locator("dt")
        .filter({ hasText: "Artifact review contract" })
        .count(),
    (n) => n === 1,
    "selected-contract-lock-restored-before-retry",
  );
  await retry(page, normal);
  group(
    "AR13-T07 complete-source-original-canonical-history-schema-client-denials",
  );
  // Malform the receipt of a real committed second-artifact command. It must be
  // uncertain, retaining its exact identity until explicit actor-bound replay.
  const secondary = detail.artifactReview.artifacts.find(
    (e) => e.artifactId !== target,
  ).artifactId;
  await article(page, secondary)
    .locator("textarea")
    .fill("Fictional malformed committed receipt");
  receiptMutation = (value) => {
    value.receipt.recordedAtUtc = "2026-10-02";
    return value;
  };
  await article(page, secondary)
    .getByRole("button", { name: "Review for planning", exact: true })
    .click();
  await article(page, secondary)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .waitFor();
  const malformedCommand = structuredClone(artifactPosts.at(-1));
  check(
    await page
      .locator("#artifact-review-error-" + secondary)
      .evaluate((n) => n === document.activeElement),
    "malformed-committed-receipt-error-focus",
  );
  equal(
    await article(page, secondary).locator("textarea").inputValue(),
    "Fictional malformed committed receipt",
    "malformed-receipt-original-reason-retained",
  );
  equal(
    (await analysis(normal.runId)).artifactReview.artifacts.find(
      (e) => e.artifactId === secondary,
    ).history.length,
    1,
    "malformed-receipt-real-command-committed-once",
  );
  await article(page, secondary)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .click();
  await waitCurrent(page, normal, secondary, "ReviewedForPlanning", 1);
  equal(
    artifactPosts.at(-1),
    malformedCommand,
    "malformed-receipt-explicit-exact-retry-no-new-command",
  );
  detail = await analysis(normal.runId);
  equal(
    detail.artifactReview.artifacts.find((e) => e.artifactId === secondary)
      .history.length,
    1,
    "malformed-receipt-retry-no-duplicate-event",
  );
  await visibleOverlay(page, normal, detail);
  group("AR13-T04/T06/T07 real-malformed-committed-receipt-explicit-replay");
  // A committed response is lost: retain exactly one frozen command and reason.
  await select(page, normal.runId);
  await shown(page, normal.runId);
  const savedReason = "Fictional uncertain original reason";
  await article(page, target).locator("textarea").fill(savedReason);
  dropArtifact = true;
  await article(page, target)
    .getByRole("button", { name: "Review for planning", exact: true })
    .click();
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .waitFor();
  const uncertain = structuredClone(artifactPosts.at(-1)),
    postCount = artifactPosts.length;
  equal(
    await article(page, target).getByRole("status").first().textContent(),
    "Current attestation withheld while this command or refresh is unresolved.",
    "uncertain-command-suppresses-current-attestation",
  );
  equal(
    await article(page, target).locator("textarea").inputValue(),
    savedReason,
    "lost-response-preserves-entered-reason",
  );
  check(
    await page
      .locator("#artifact-review-error-" + target)
      .evaluate((n) => n === document.activeElement),
    "lost-response-actionable-error-focus",
  );
  const commitObserved = await analysis(normal.runId);
  equal(
    commitObserved.artifactReview.artifacts.find((e) => e.artifactId === target)
      .revision,
    4,
    "lost-response-event-actually-committed-once",
  );
  // ReviewPanel remains reachable after the uncertain whole-source reread; its save
  // triggers an actual child unmount/reload at unchanged run revision.
  await page
    .getByLabel("Review action", { exact: true })
    .first()
    .selectOption("Comment");
  await page
    .getByRole("textbox", { name: "Comment", exact: true })
    .first()
    .fill(
      "Fictional refresh while original artifact command remains uncertain",
    );
  await page
    .getByRole("button", { name: "Add comment", exact: true })
    .first()
    .click();
  // The pending retry was already visible before the asynchronous finding save.
  // Wait for the actual saved source and its coherent DOM replacement, so retry
  // deliberately uses the new source epoch rather than racing that replacement.
  const refreshedUncertain = await until(
    () => analysis(normal.runId),
    (d) =>
      d.reviewSnapshotDigest !== commitObserved.reviewSnapshotDigest &&
      d.artifactReview.source.sourceDigest !==
        commitObserved.artifactReview.source.sourceDigest &&
      d.review.findings.some((f) =>
        f.history.some(
          (e) =>
            e.kind === "Comment" &&
            e.text ===
              "Fictional refresh while original artifact command remains uncertain",
        ),
      ),
    "finding-save-before-original-command-retry",
  );
  equal(
    refreshedUncertain.runRevision,
    commitObserved.runRevision,
    "finding-refresh-keeps-same-run-revision",
  );
  await until(
    () =>
      rows(
        overlay(page)
          .locator("details")
          .filter({
            has: page.getByText("Current review source", { exact: true }),
          })
          .first(),
      ),
    (value) =>
      JSON.stringify(value) ===
      JSON.stringify(sourceValues(refreshedUncertain.artifactReview.source)),
    "saved-finding-source-visible-before-original-command-retry",
  );
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .waitFor();
  equal(
    await article(page, target).locator("textarea").inputValue(),
    savedReason,
    "reason-survives-analysis-child-unmount",
  );
  equal(
    artifactPosts.length,
    postCount,
    "source-refresh-never-auto-retries-artifact-command",
  );
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .click();
  await waitCurrent(page, normal, target, "NeedsReview", 4);
  equal(
    artifactPosts.at(-1),
    uncertain,
    "explicit-retry-exact-event-revision-source-reason-no-rebind",
  );
  detail = await analysis(normal.runId);
  equal(
    detail.artifactReview.artifacts.find((e) => e.artifactId === target).history
      .length,
    4,
    "historical-exact-retry-no-duplicate-event",
  );
  await visibleOverlay(page, normal, detail);
  group(
    "AR13-T04/T06/T07 actual-committed-response-loss-refresh-original-command-retry",
  );
  // A competing accepted command produces a real revision conflict; preserve reason,
  // require an explicit source refresh and a fresh explicit action.
  const externallyReviewed = {
    eventId: randomUUID(),
    kind: "ReviewForPlanning",
    expectedRevision: 4,
    expectedSourceDigest: detail.artifactReview.source.sourceDigest,
    reason: "Fictional competing review",
  };
  equal(
    (await artifactPost(normal, target, externallyReviewed, headers)).status(),
    200,
    "competing-actual-current-review-commits",
  );
  await article(page, target)
    .locator("textarea")
    .fill("Fictional preserved conflict reason");
  await article(page, target)
    .getByRole("button", { name: "Review for planning", exact: true })
    .click();
  await article(page, target)
    .getByRole("button", { name: "Refresh artifact source", exact: true })
    .waitFor();
  const conflictPosts = artifactPosts.length;
  equal(
    await article(page, target).locator("textarea").inputValue(),
    "Fictional preserved conflict reason",
    "conflict-preserves-entered-reason",
  );
  equal(
    await article(page, target)
      .getByRole("button", { name: "Retry same artifact command", exact: true })
      .count(),
    0,
    "conflict-clears-uncertain-command",
  );
  await article(page, target)
    .getByRole("button", { name: "Refresh artifact source", exact: true })
    .click();
  await article(page, target)
    .getByRole("button", { name: "Withdraw planning review", exact: true })
    .waitFor();
  equal(
    artifactPosts.length,
    conflictPosts,
    "explicit-source-refresh-never-applies-rebound-action",
  );
  await article(page, target)
    .getByRole("button", { name: "Withdraw planning review", exact: true })
    .click();
  await waitCurrent(page, normal, target, "Unverified", 6);
  check(
    artifactPosts.at(-1).command.eventId !== externallyReviewed.eventId,
    "post-conflict-new-explicit-event-ID",
  );
  group(
    "AR13-T07 actual-competing-revision-conflict-refresh-reason-new-inspection",
  );
  // Hold old analysis, change selected run through its real recent-run action,
  // complete the old route before asserting the final current identity.
  let releaseOld, completeOld;
  const oldDone = new Promise((r) => (completeOld = r)),
    capturedOld = new Promise((resolve) => {
      hold = {
        runId: normal.runId,
        captured: resolve,
        release: new Promise((r) => (releaseOld = r)),
        completed: completeOld,
      };
    });
  await select(page, normal.runId);
  pendingReleases.add(releaseOld);
  await bounded(capturedOld, "held-old-run-analysis-captured");
  await page
    .getByRole("row")
    .filter({ hasText: runs[3].selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  hold = null;
  releaseOld();
  await bounded(oldDone, "held-old-analysis-route-completed");
  await tick(page);
  await shown(page, runs[3].runId);
  check(
    !(await overlay(page).textContent()).includes(normal.runId),
    "late-prior-run-source-never-restored",
  );
  group("AR13-T07 held-old-analysis-selection-race-completed");
  // Hold an accepted old mutation receipt, select another run, then release it.
  await select(page, normal.runId);
  await shown(page, normal.runId);
  await article(page, target)
    .locator("textarea")
    .fill("Fictional held old-run artifact review");
  let releaseArtifact, completeArtifact;
  const artifactDone = new Promise((r) => (completeArtifact = r)),
    capturedArtifact = new Promise((resolve) => {
      artifactHold = {
        captured: resolve,
        release: new Promise((r) => (releaseArtifact = r)),
        completed: completeArtifact,
      };
    });
  await article(page, target)
    .getByRole("button", { name: "Review for planning", exact: true })
    .click();
  pendingReleases.add(releaseArtifact);
  await bounded(capturedArtifact, "held-old-run-mutation-receipt-captured");
  await page
    .getByRole("row")
    .filter({ hasText: runs[3].selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  artifactHold = null;
  releaseArtifact();
  await bounded(artifactDone, "held-old-run-mutation-route-completed");
  await tick(page);
  await shown(page, runs[3].runId);
  check(
    !(await overlay(page).textContent()).includes(normal.runId),
    "late-old-mutation-never-restores-prior-overlay",
  );
  group("AR13-T07 late-prior-run-mutation-completion-fence");
  const heldCommand = structuredClone(artifactPosts.at(-1));
  await page
    .getByRole("row")
    .filter({ hasText: normal.selection.baselineLabel })
    .getByRole("button", { name: /^Open / })
    .first()
    .click();
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .waitFor();
  equal(
    await article(page, target).locator("textarea").inputValue(),
    "Fictional held old-run artifact review",
    "run-switch-retains-reason-keyed-by-original-run-artifact",
  );
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .click();
  await waitCurrent(page, normal, target, "ReviewedForPlanning", 7);
  equal(
    artifactPosts.at(-1),
    heldCommand,
    "run-return-explicit-retry-retains-exact-original-command",
  );
  // Real finding save starts a held old analysis response; once hidden, a second
  // source change uses the separate owned API, not an unreachable hidden editor.
  await page
    .getByLabel("Review action", { exact: true })
    .first()
    .selectOption("Comment");
  await page
    .getByRole("textbox", { name: "Comment", exact: true })
    .first()
    .fill("Fictional held current-source refresh");
  let releaseRefresh, completeRefresh;
  const refreshDone = new Promise((r) => (completeRefresh = r)),
    capturedRefresh = new Promise((resolve) => {
      hold = {
        runId: normal.runId,
        captured: resolve,
        release: new Promise((r) => (releaseRefresh = r)),
        completed: completeRefresh,
      };
    });
  await page
    .getByRole("button", { name: "Add comment", exact: true })
    .first()
    .click();
  pendingReleases.add(releaseRefresh);
  const oldRefresh = await bounded(
    capturedRefresh,
    "saved-comment-old-refresh-captured",
  );
  equal(
    await overlay(page).count(),
    0,
    "actual-finding-save-clears-old-attestation-before-held-read",
  );
  const refreshedFinding = oldRefresh.review.findings.find(
    (f) => f.id === finding.id,
  );
  equal(
    (
      await api.post(
        `/local-demo/v1/runs/${normal.runId}/findings/${finding.id}/events`,
        {
          headers,
          data: {
            ...update,
            eventId: randomUUID(),
            expectedRevision: refreshedFinding.revision,
            text: "Fictional independently superseding source",
          },
        },
      )
    ).status(),
    200,
    "separate-owner-client-supersedes-held-source",
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
  const fresh = await analysis(normal.runId);
  check(
    fresh.artifactReview.source.sourceDigest !==
      oldRefresh.artifactReview.source.sourceDigest,
    "superseded-read-current-full-source-digest",
  );
  releaseRefresh();
  await bounded(refreshDone, "saved-comment-old-refresh-route-completed");
  await tick(page);
  await visibleOverlay(page, normal, fresh);
  check(
    !(await overlay(page).locator("details").first().textContent()).includes(
      oldRefresh.artifactReview.source.sourceDigest,
    ),
    "late-same-run-revision-old-source-never-restored",
  );
  await badCase(
    page,
    normal,
    (d) => {
      d.artifactReview = structuredClone(oldRefresh.artifactReview);
      d.fixPackages = structuredClone(oldRefresh.fixPackages);
      d.recommendationGuidance = structuredClone(
        oldRefresh.recommendationGuidance,
      );
      return d;
    },
    "AR13-T07 forged-old-package-guidance-overlay-with-fresh-review-denied",
  );

  // Hold a committed artifact receipt across a reachable finding save and refresh
  // at the same selected run/revision. No run/generation switch masks this fence.
  const sameRunReason = "Fictional same-run pending planning review";
  await article(page, target).locator("textarea").fill(sameRunReason);
  let releaseSame, completeSame;
  const sameDone = new Promise((r) => (completeSame = r)),
    capturedSame = new Promise((resolve) => {
      artifactHold = {
        captured: resolve,
        release: new Promise((r) => (releaseSame = r)),
        completed: completeSame,
      };
    });
  await article(page, target)
    .getByRole("button", { name: "Review for planning", exact: true })
    .click();
  pendingReleases.add(releaseSame);
  await bounded(capturedSame, "held-same-run-committed-artifact-receipt");
  const sameCommand = structuredClone(artifactPosts.at(-1));
  await page
    .getByLabel("Review action", { exact: true })
    .first()
    .selectOption("Comment");
  await page
    .getByRole("textbox", { name: "Comment", exact: true })
    .first()
    .fill("Fictional same-run source change while artifact response held");
  await page
    .getByRole("button", { name: "Add comment", exact: true })
    .first()
    .click();
  await overlay(page).waitFor();

  const sameFresh = await until(
    () => analysis(normal.runId),
    (d) =>
      d.artifactReview.source.sourceDigest !==
        sameCommand.command.expectedSourceDigest &&
      d.review.findings.some(
        (f) =>
          f.id === finding.id &&
          f.revision >
            fresh.review.findings.find((f) => f.id === finding.id).revision,
      ),
    "same-run-finding-save-committed-new-source-before-release",
  );
  equal(
    sameFresh.runRevision,
    fresh.runRevision,
    "held-mutation-review-refresh-unchanged-run-revision",
  );
  equal(
    sameFresh.artifactReview.artifacts.find((e) => e.artifactId === target)
      .state,
    "NeedsReview",
    "same-run-current-source-changed-after-committed-receipt",
  );
  equal(
    await article(page, target).locator("textarea").inputValue(),
    sameRunReason,
    "same-run-refresh-retains-frozen-command-reason",
  );
  await until(
    () => overlay(page).textContent(),
    (text) => text.includes(sameFresh.artifactReview.source.sourceDigest),
    "same-run-fresh-source-visible-before-held-receipt-release",
  );
  equal(
    await article(page, target).getByRole("status").first().textContent(),
    "Current attestation withheld while this command or refresh is unresolved.",
    "same-run-held-command-withholds-current-attestation",
  );
  artifactHold = null;
  releaseSame();
  await bounded(sameDone, "held-same-run-artifact-route-completed");
  await tick(page);
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .waitFor();
  check(
    await page
      .locator("#artifact-review-error-" + target)
      .evaluate((n) => n === document.activeElement),
    "late-same-run-uncertain-response-error-focus",
  );
  equal(
    await article(page, target).locator("textarea").inputValue(),
    sameRunReason,
    "late-same-run-receipt-cannot-discard-original-reason",
  );
  await article(page, target)
    .getByRole("button", { name: "Retry same artifact command", exact: true })
    .click();
  await waitCurrent(page, normal, target, "NeedsReview", 8);
  equal(
    artifactPosts.at(-1),
    sameCommand,
    "late-same-run-receipt-explicit-retry-exact-unrebound-command",
  );
  const sameFinal = await analysis(normal.runId);
  equal(
    sameFinal.artifactReview,
    sameFresh.artifactReview,
    "late-same-run-receipt-no-stale-current-source-or-duplicate-event",
  );
  await visibleOverlay(page, normal, sameFinal);
  group(
    "AR13-T07 unchanged-run-held-artifact-receipt-review-source-epoch-fence",
  );

  // Execute only the dedicated fixture helper against the owned host/database.
  const schemaLog = [];
  const schemaChild = spawn(
    process.env.IGA_DOTNET,
    [
      path.join(
        workdir,
        "tests/integration/LocalArtifactReview.Tests/bin/Release/net10.0/LocalArtifactReview.IntegrationTests.dll",
      ),
      "--host-schema-denial",
      normal.runId,
    ],
    {
      cwd: workdir,
      env: {
        ...process.env,
        IGA_ARTIFACT_REVIEW_TEST_DATABASE: process.env.IGA_SYNTHETIC_DATABASE,
      },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  schemaChild.stdout.on("data", (data) => schemaLog.push(data.toString()));
  schemaChild.stderr.on("data", (data) => schemaLog.push(data.toString()));
  schemaChild.on("error", () => faults.push("owned-schema-helper-spawn-error"));
  try {
    equal(
      (
        await bounded(
          once(schemaChild, "exit"),
          "owned-schema-helper-completed",
        )
      )[0],
      0,
      "owned-schema-helper-success",
    );
  } finally {
    if (
      schemaChild.exitCode === null &&
      schemaChild.signalCode === null &&
      schemaChild.pid
    ) {
      schemaChild.kill("SIGKILL");
      await once(schemaChild, "exit");
    }
    // A killed child cannot run its own finally. Always recover through a new,
    // independently guarded process before releasing the owned host/database.
    const recovery = spawn(
      process.env.IGA_DOTNET,
      [
        path.join(
          workdir,
          "tests/integration/LocalArtifactReview.Tests/bin/Release/net10.0/LocalArtifactReview.IntegrationTests.dll",
        ),
        "--restore-host-schema",
      ],
      {
        cwd: workdir,
        env: {
          ...process.env,
          IGA_ARTIFACT_REVIEW_TEST_DATABASE: process.env.IGA_SYNTHETIC_DATABASE,
        },
        stdio: ["ignore", "pipe", "pipe"],
      },
    );
    recovery.stdout.on("data", (data) => schemaLog.push(data.toString()));
    recovery.stderr.on("data", (data) => schemaLog.push(data.toString()));
    recovery.on("error", () =>
      faults.push("owned-schema-recovery-spawn-error"),
    );
    equal(
      (await once(recovery, "exit"))[0],
      0,
      "independent-outer-schema-recovery-verified",
    );
  }
  await mkdir(path.join(directory, ".host"), { recursive: true });
  await writeFile(
    path.join(directory, ".host/schema-denial.log"),
    schemaLog.join(""),
  );
  group("AR13-T12 actual-host-pre-capture-schema-denial-and-restoration");

  // Restart only the owned process: full durable source/history and exact original
  // receipt remain available under fresh anti-forgery startup state.
  const preRestart = await analysis(normal.runId);
  await stop();
  await launch();
  catalog = await (await api.get("/local-demo/v1/catalog")).json();
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const afterRestart = await analysis(normal.runId);
  equal(
    afterRestart.artifactReview,
    preRestart.artifactReview,
    "owned-host-restart-durable-complete-history-identical",
  );
  const restartReplay = await artifactPost(normal, target, command, headers);
  equal(
    restartReplay.status(),
    200,
    "owned-host-restart-original-replay-allowed",
  );
  equal(
    (await restartReplay.json()).receipt,
    receipt,
    "owned-host-restart-receipt-exact-original",
  );
  await select(page, normal.runId);
  detail = await analysis(normal.runId);
  await fullVisible(page, normal, detail);
  await visibleOverlay(page, normal, detail);
  group("AR13-T06 owned-host-stop-restart-exact-history-receipt");
  for (const historical of [
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
  ]) {
    const run = await start(historical[0], historical[1], headers),
      d = await analysis(run.runId);
    equal(d.artifactReview, null, "historical-nine-required-null-overlay");
    check(
      !run.lockedInputs.some((l) => l.name === "Artifact review contract"),
      "historical-nine-no-new-contract-lock",
    );
    const denied = await artifactPost(run, target, command, headers);
    check(
      denied.status() >= 400,
      "historical-nine-direct-artifact-route-denied",
    );
    await select(page, run.runId);
    await until(
      () =>
        page
          .getByText("Reading analysis from the saved run…", { exact: true })
          .count(),
      (n) => n === 0,
      "historical-analysis-read-completes",
    );
    equal(
      await overlay(page).count(),
      0,
      "historical-nine-no-artifact-control-promotion",
    );
  }
  for (const suffix of ["tasks", "export", "execute", "publish", "download"]) {
    const response = await api.post(
      `/local-demo/v1/runs/${normal.runId}/artifacts/${target}/${suffix}`,
      { headers, data: {} },
    );
    check(
      response.status() >= 400,
      "deferred-operation-no-artifact-route-enabled",
    );
  }
  group(
    "AR13-T08/T11 actual-historical-nine-no-authority-or-operation-promotion",
  );
  await select(page, normal.runId);
  detail = await analysis(normal.runId);
  await fullVisible(page, normal, detail);
  await visibleOverlay(page, normal, detail);
  await mkdir(path.join(directory, ".host/observed"), { recursive: true });
  await writeFile(
    path.join(directory, ".host/observed/final-saved-source-and-replay.json"),
    JSON.stringify(
      {
        schema: "v13-observed-browser-synthetic-values-v1",
        savedRun: normal,
        finalAnalysis: detail,
        originalCommand: command,
        originalReceipt: receipt,
        preRestartAnalysis: preRestart,
        afterRestartAnalysis: afterRestart,
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
    await visibleOverlay(page, normal, detail);
    await fullVisible(page, normal, detail);
    const r = overlay(page);
    await r.scrollIntoViewIfNeeded();
    check(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      "page-" + name + "-reflow-no-horizontal-overflow",
    );
    const firstSummary = r.locator("summary").first();
    await firstSummary.focus();
    await page.keyboard.press("Enter");
    check(
      !(await firstSummary.evaluate((n) => n.parentElement.open)),
      "native-disclosure-keyboard-toggle",
    );
    await page.keyboard.press("Enter");
    const reason = article(page, target).locator("textarea");
    await reason.fill("Fictional keyboard inspection reason");
    await reason.focus();
    await page.keyboard.press("Tab");
    check(
      await article(page, target)
        .getByRole("button")
        .evaluate((n) => n === document.activeElement),
      "reason-to-action-keyboard-order",
    );
    await inertOverlay(page);
    await page.evaluate(axeSource);
    const axe = await page.evaluate(async () =>
      globalThis.axe.run(".artifact-review", {
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
    equal(
      axe.violations.length,
      0,
      "axe-" + name + "-WCAG-engineering-subset-zero-violations",
    );
    await inertOverlay(page);
    const targetPath = path.join(directory, `${name}.png`);
    await r.screenshot({ path: targetPath, caret: "initial" });
    screenshots.push(targetPath);
    await page.locator("#artifact-review-heading").scrollIntoViewIfNeeded();
    const viewportPath = path.join(directory, `${name}-viewport.png`);
    await page.screenshot({ path: viewportPath, caret: "initial" });
    screenshots.push(viewportPath);
  }
  const actualHostLogs = ownedHostLogChunks.join("");
  check(
    ![
      command.reason,
      savedReason,
      sameRunReason,
      "Fictional malformed committed receipt",
    ].some((value) => actualHostLogs.includes(value)),
    "actual-host-logs-no-fixture-reason-payloads",
  );
  await writeFile(
    path.join(directory, ".host/host-runtime.log"),
    actualHostLogs,
  );
  equal(blocked.length, 0, "zero-unexpected-or-external-request-attempts");
  equal(dialogs.length, 0, "zero-injected-dialogs");
  equal(faults.length, 0, "zero-browser-or-owned-host-faults");
  group(
    "AR13-T09/T10 actual-inert-history-keyboard-focus-reflow-axe-three-viewports",
  );
  console.log("PASS V13 artifact-review browser; checks=" + checks);
} catch (error) {
  console.error(
    "FAIL V13 browser; code=" +
      last +
      "; checks=" +
      checks +
      "; exception=" +
      error.constructor.name +
      "; payload-suppressed",
  );
  process.exitCode = 1;
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
  await writeFile(
    path.join(directory, "execution.json"),
    JSON.stringify(
      {
        schema: "v13-browser-execution-v1",
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
          "closed allowlist applied before fixture/browser loading; no marker bypass",
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
          "Test-only axe injection is isolated from product inert-node checks.",
          "Historical test counts remain original evidence, not new assertions.",
        ],
      },
      null,
      2,
    ) + "\n",
  );
}
