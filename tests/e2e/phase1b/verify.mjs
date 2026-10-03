import assert from "node:assert/strict";
import { createHash, randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

// Executes only when the coordinator supplies an already running fictional host.
// Never starts/stops a host, reads credentials, resets a database, or regenerates goldens.
// Consultant mode needs one complete 240-unit run available in the fixed shared fixture epoch.
const directory = dirname(fileURLToPath(import.meta.url));
const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5183";
const endpoint = new URL(base);
assert.equal(endpoint.protocol, "http:");
assert.equal(endpoint.hostname, "127.0.0.1");
assert.equal(endpoint.port, "5183");
assert.equal(endpoint.pathname, "/");
assert.equal(endpoint.search + endpoint.hash, "");
const role = process.env.IGA_PHASE1B_ROLE ?? "consultant";
assert.ok(["consultant", "auditor"].includes(role));
const dependencies =
  process.env.IGA_BROWSER_DEPENDENCIES ??
  resolve(directory, "../consultant-demo/node_modules");
const { chromium, request } = await import(
  pathToFileURL(
    process.env.IGA_PLAYWRIGHT_MODULE ??
      resolve(dependencies, "playwright/index.mjs"),
  )
);
const axeSource = await readFile(
  resolve(dependencies, "axe-core/axe.min.js"),
  "utf8",
);
const output =
  process.env.IGA_PHASE1B_E2E_OUTPUT ?? resolve(directory, "execution");
const prefix = "/local-demo/v1/phase1b";
const profile = "synthetic-phase1b-combined-v1",
  baseline = "synthetic-phase1b-baseline-v1";
const checks = [],
  scans = [],
  errors = [];
const check = (condition, label) => {
  assert.ok(condition, label);
  checks.push(label);
  console.log(`PASS ${label}`);
};
const equal = (actual, expected, label) => {
  assert.deepEqual(actual, expected, label);
  checks.push(label);
  console.log(`PASS ${label}`);
};
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");
const digest = (value) =>
  typeof value === "string" && /^[0-9a-f]{64}$/.test(value);
const browser = await chromium.launch({
  headless: true,
  ...(process.env.IGA_CHROMIUM
    ? { executablePath: process.env.IGA_CHROMIUM }
    : {}),
});
const context = await browser.newContext({
  acceptDownloads: true,
  viewport: { width: 1280, height: 900 },
});
const page = await context.newPage(),
  api = await request.newContext({ baseURL: base });
page.on("pageerror", (error) => errors.push(error.message));
let headers, runId, downloadedSha;
async function read(path) {
  const response = await api.get(path);
  equal(
    response.status(),
    200,
    `read ${path.replace(/[0-9a-f-]{36}/g, "<run>")}`,
  );
  if (path === "/local-demo/v1/catalog") {
    const policy = response.headers()["content-security-policy"] ?? "";
    check(
      policy.includes("script-src 'self'") &&
        !policy.includes("'unsafe-inline'") &&
        !policy.includes("'unsafe-eval'"),
      "real host CSP permits only same-origin scripts without inline or eval allowances",
    );
  }
  const value = await response.json();
  check(
    value.schemaVersion === 1 && value.demoOnly === true,
    "read response is versioned and explicitly fictional",
  );
  return value;
}
async function post(path, body, expected = 200) {
  const response = await api.post(path, { data: body, headers });
  equal(
    response.status(),
    expected,
    `mutation/denial ${path.replace(/[0-9a-f-]{36}/g, "<run>")}`,
  );
  return response;
}
async function waitFor(readValue, predicate, label, timeout = 45000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const value = await readValue();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 150));
  }
  throw Error(label);
}
async function workspace() {
  return read(`${prefix}/runs/${runId}/workspace`);
}
async function scan(label) {
  // Playwright evaluates the test scanner through its automation execution context.
  // The application CSP remains active; this does not add an inline script element.
  await page.evaluate(axeSource);
  const result = await page.evaluate(async () =>
    window.axe.run(document, {
      runOnly: {
        type: "tag",
        values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
      },
    }),
  );
  const violations = result.violations.map((value) => ({
    id: value.id,
    impact: value.impact,
    targets: value.nodes.map((node) => node.target),
  }));
  scans.push({ label, version: result.testEngine.version, violations });
  equal(violations, [], `WCAG scan ${label}`);
}
// One real commit is intentionally delivered as a lost response. Retry must use the same UUID/body.
async function loseOnce(pattern, click, retryLabel) {
  const sent = [];
  let first = true;
  const handler = async (route) => {
    const body = route.request().postDataJSON();
    sent.push(body);
    if (first) {
      first = false;
      const response = await route.fetch();
      equal(response.status(), 200, "lost response was committed by real host");
      await response.dispose();
      await route.abort("failed");
    } else await route.continue();
  };
  await page.route(pattern, handler);
  try {
    await click();
    const retry = page.getByRole("button", { name: retryLabel, exact: true });
    await retry.waitFor();
    await retry.click();
    await waitFor(
      async () => sent.length,
      (count) => count === 2,
      "exact response-loss retry was not sent",
    );
    equal(
      sent[1],
      sent[0],
      "real response-loss retry retains exact event UUID and payload",
    );
    return sent[0];
  } finally {
    await page.unroute(pattern, handler);
  }
}
const headersLiteral = [
  "csv_contract_version",
  "run_id",
  "task_id",
  "finding_id",
  "package_id",
  "scoped_option_id",
  "assignee_id",
  "task_revision",
  "task_status",
  "plan_freshness",
  "created_at_utc",
  "planned_at_utc",
  "current_source_digest",
  "planned_source_digest",
  "export_snapshot_digest",
  "task_link",
  "finding_link",
];
const rowKeys = [
  "csvContractVersion",
  "runId",
  "taskId",
  "findingId",
  "packageId",
  "scopedOptionId",
  "assigneeId",
  "taskRevision",
  "taskStatus",
  "planFreshness",
  "createdAtUtc",
  "plannedAtUtc",
  "currentSourceDigest",
  "plannedSourceDigest",
  "exportSnapshotDigest",
  "taskLink",
  "findingLink",
];
const quote = (value) => {
  const text = String(value),
    inert = /^\p{White_Space}*[=+\-@]/u.test(text) ? `'${text}` : text;
  return `"${inert.replaceAll('"', '""')}"`;
};
const literalHeader = headersLiteral.map(quote).join(",") + "\r\n";
equal(
  Buffer.byteLength(literalHeader),
  283,
  "independent exact seventeen-column quoted header is 283 bytes",
);
equal(
  hash(literalHeader),
  "2e1c6fedadd38cb870c4d6ef25dbd44f806fdc16f6c10ed3c20293c8a9f4b603",
  "independent frozen CSV header digest",
);
function verifyLinks(row) {
  equal(
    row.taskLink,
    `http://localhost:5183/?run=${runId}&view=tasks&task=${row.taskId}`,
    "task link uses exact trusted origin and scoped identity",
  );
  equal(
    row.findingLink,
    `http://localhost:5183/?run=${runId}&view=findings&finding=${row.findingId}`,
    "finding link uses exact trusted origin and scoped identity",
  );
  check(
    digest(row.taskId) && digest(row.findingId),
    "protected target IDs are lowercase SHA256 identities",
  );
}
async function navigation(capture) {
  const row = capture.rows[0];
  check(
    row !== undefined,
    "saved task metadata exists for protected navigation",
  );
  verifyLinks(row);
  await page.goto(row.taskLink);
  await page
    .getByRole("heading", { name: "Protected fictional planning reference" })
    .waitFor();
  await page.getByText(row.taskId, { exact: true }).waitFor();
  check(
    (await page
      .getByText("Auditor · metadata only", { exact: false })
      .count()) === (role === "auditor" ? 1 : 0),
    "protected navigation reflects genuine current role",
  );
  if (role === "auditor")
    check(
      (await page
        .getByRole("link", { name: "Open combined assessment workspace" })
        .count()) === 0,
      "Auditor navigation has metadata only",
    );
  await scan("protected-task");
  await page.goto(row.findingLink);
  await page.getByText(row.findingId, { exact: true }).waitFor();
  check(true, "finding link performs fresh authorized lookup");
  const denied = await api.get(
    `${prefix}/runs/${runId}/navigation?view=tasks&id=${"0".repeat(64)}`,
  );
  equal(denied.status(), 403, "foreign protected target fails closed");
  const duplicate = await api.get(
    `${prefix}/runs/${runId}/navigation?view=tasks&id=${row.taskId}&id=${row.taskId}`,
  );
  equal(
    duplicate.status(),
    403,
    "duplicate protected lookup parameters denied",
  );
  await page.goto(row.taskLink + "&task=" + row.taskId);
  await page.getByRole("alert").filter({ hasText: "unavailable" }).waitFor();
  check(
    (await page.getByText(row.taskId, { exact: true }).count()) === 0,
    "invalid UI protected query reveals no prior target metadata",
  );
}
try {
  const catalog = await read("/local-demo/v1/catalog");
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  const fixture = await read(`${prefix}/fixture`);
  if (role === "auditor") {
    equal(
      fixture.actor,
      "synthetic-auditor",
      "genuine Auditor fixture is active",
    );
    runId = process.env.IGA_PHASE1B_AUDITOR_RUN;
    assert.match(runId ?? "", /^[0-9a-f-]{36}$/);
    for (const path of [
      `${prefix}/registry`,
      `${prefix}/runs/${runId}/workspace`,
      `${prefix}/runs/${runId}/ledger`,
    ])
      equal(
        (await api.get(path)).status(),
        403,
        "Auditor cannot read ordinary source content",
      );
    await post(`${prefix}/outcomes/events`, {}, 403);
    await post(`${prefix}/runs/${runId}/planning/events`, {}, 403);
    const capture = await read(`${prefix}/runs/${runId}/csv`);
    capture.rows.forEach(verifyLinks);
    check(
      !JSON.stringify(capture).includes("behavior") &&
        !JSON.stringify(capture).includes("packetInputJson"),
      "Auditor export inspection omits outcome and AI source text",
    );
    await navigation(capture);
  } else {
    equal(
      fixture.actor,
      "synthetic-consultant",
      "genuine local Consultant fixture is active",
    );
    equal(
      fixture.availableCoverageKeys.length,
      12,
      "fixed combined plan has twelve exact keys",
    );
    const retryKey = fixture.availableCoverageKeys.find(
      (key) =>
        key.inventoryId === "synthetic-ai-retry" &&
        key.evidenceCategory === "configuration",
    );
    const guardKey = fixture.availableCoverageKeys.find(
      (key) => key.evidenceCategory === "SYN-GUARD",
    );
    check(
      retryKey &&
        guardKey &&
        retryKey.categoryId === "OPERATIONS" &&
        guardKey.categoryId === "SECURITY",
      "trusted fixture maps exact evidence keys separately from health categories",
    );
    const before = await read(`${prefix}/registry`),
      outcomeId = "access-governance";
    const version =
      Math.max(
        0,
        ...before.entries
          .filter((entry) => entry.content.outcomeId === outcomeId)
          .map((entry) => entry.content.version),
      ) + 1;
    const title = `Fictional E2E access governance v${version}`,
      behavior =
        '<img src=x onerror="window.phase1bInjected=true"> Fictional desired behavior';
    await page.goto(base);
    await page.getByLabel("Evidence baseline").selectOption(baseline);
    await page.getByLabel("Assessment profile").selectOption(profile);
    await page
      .getByRole("heading", { name: "Phase 1B outcome setup" })
      .waitFor();
    await page
      .getByLabel("Reason for the next lifecycle action")
      .fill("Explicit fictional E2E lifecycle decision");
    await page
      .getByText("Create an immutable draft version", { exact: true })
      .click();
    await page.getByLabel("Stable outcome ID").fill(outcomeId);
    await page.getByLabel("Title", { exact: true }).fill(title);
    await page.getByLabel("Desired behavior").fill(behavior);
    await page.getByLabel(/^Origin/).selectOption("Inferred");
    await page
      .getByLabel("Applicability display filter")
      .selectOption("SECURITY");
    await page
      .getByRole("checkbox", {
        name: `${guardKey.inventoryId} · ${guardKey.evidenceCategory} · SECURITY`,
        exact: true,
      })
      .check();
    await page
      .getByLabel("Applicability display filter")
      .selectOption("OPERATIONS");
    await page
      .getByRole("checkbox", {
        name: "synthetic-ai-retry · configuration · OPERATIONS",
        exact: true,
      })
      .check();
    await page
      .getByLabel("Reference IDs (one per line)")
      .fill("fictional-e2e-reference");
    await page
      .getByLabel("Assumptions (one per line)")
      .fill("Explicit fictional scope prerequisite");
    await page
      .getByRole("button", { name: "Create draft version", exact: true })
      .click();
    await page
      .getByRole("button", {
        name: `Consultant review ${outcomeId} v${version}`,
        exact: true,
      })
      .waitFor();
    const reviewEvent = await loseOnce(
      `**${prefix}/outcomes/events`,
      () =>
        page
          .getByRole("button", {
            name: `Consultant review ${outcomeId} v${version}`,
            exact: true,
          })
          .click(),
      "Retry same event",
    );
    await page
      .getByRole("button", {
        name: `Fictional customer approval ${outcomeId} v${version}`,
        exact: true,
      })
      .waitFor();
    await page
      .getByRole("button", {
        name: `Fictional customer approval ${outcomeId} v${version}`,
        exact: true,
      })
      .click();
    const registry = await waitFor(
      () => read(`${prefix}/registry`),
      (value) =>
        value.entries.some(
          (entry) =>
            entry.content.outcomeId === outcomeId &&
            entry.content.version === version &&
            entry.state === "CustomerApproved",
        ),
      "fictional approval not saved",
    );
    const approved = registry.entries.find(
      (entry) =>
        entry.content.outcomeId === outcomeId &&
        entry.content.version === version,
    );
    equal(
      approved.content.origin,
      "Inferred",
      "fictional approval preserves inferred origin",
    );
    equal(
      approved.content.unitLinks,
      [guardKey, retryKey]
        .map(({ inventoryId, evidenceCategory }) => ({
          inventoryId,
          evidenceCategory,
        }))
        .sort((a, b) =>
          a.inventoryId < b.inventoryId
            ? -1
            : a.inventoryId > b.inventoryId
              ? 1
              : a.evidenceCategory < b.evidenceCategory
                ? -1
                : a.evidenceCategory > b.evidenceCategory
                  ? 1
                  : 0,
        ),
      "exact cross-category applicability is sealed without health metadata",
    );
    equal(
      approved.history.filter((event) => event.kind === "Review").length,
      1,
      "response-loss review retry creates one immutable event",
    );
    check(
      approved.history.some((event) => event.eventId === reviewEvent.eventId),
      "lost review UUID has its exact saved event",
    );
    equal(
      approved.history.at(-1).actorId,
      "synthetic-customer-outcome-approver-v1",
      "approval records distinct fictional customer authority",
    );
    check(
      (await page.locator("img").count()) === 0 &&
        !(await page.evaluate(() => window.phase1bInjected)),
      "desired behavior and model text stay inert in the actual application",
    );
    const selection = {
      outcomeId,
      version,
      contentDigest: approved.content.contentDigest,
      revision: approved.revision,
      approvalEventId: approved.history.at(-1).eventId,
    };
    const selectionBox = page.getByRole("checkbox", {
      name: new RegExp(`${outcomeId} v${version} · approval`),
    });
    check(
      !(await selectionBox.isChecked()),
      "new approved version is explicitly unselected until human selection",
    );
    await selectionBox.check();
    const started = page.waitForResponse(
      (response) =>
        response.url().endsWith(`${prefix}/runs`) &&
        response.request().method() === "POST",
    );
    await page
      .getByRole("button", {
        name: "Start combined Phase 1B run with selected versions",
        exact: true,
      })
      .click();
    const startResponse = await started;
    equal(startResponse.status(), 201, "explicit selected combined run saved");
    const run = await startResponse.json();
    runId = run.runId;
    equal(
      run.selection.profileId,
      profile,
      "new run locks exactly the combined profile",
    );
    const ready = await waitFor(
      async () => {
        const response = await api.get(`${prefix}/runs/${runId}/workspace`);
        return response.ok() ? response.json() : null;
      },
      (value) => value?.analysis.status === "Ready",
      "combined automatic workload did not become ready",
    );
    equal(
      ready.analysis.quality.plannedUnits,
      12,
      "literal twelve terminal scoring keys including every planned category",
    );
    equal(
      ready.analysis.findings.length,
      7,
      "literal five deterministic plus two fictional AI findings",
    );
    const aiFindings = ready.analysis.findings.filter(
      (finding) => finding.method === "AI",
    );
    equal(
      aiFindings.length,
      2,
      "two AI findings are distinguishable from deterministic findings",
    );
    check(
      aiFindings.every(
        (finding) =>
          finding.state === "Proposed" &&
          Number(finding.confidencePercent) === 80,
      ),
      "AI findings remain proposed with trusted fixed eighty percent confidence",
    );
    equal(
      ready.ai.works.flatMap((work) => work.attempts).length,
      1,
      "one benign provider dispatch returns the two frozen AI proposals",
    );
    equal(
      ready.ai.works
        .flatMap((work) => work.attempts)
        .reduce(
          (sum, attempt) =>
            sum + attempt.receipt.inputUse + attempt.receipt.outputUse,
          0,
        ),
      240,
      "one frozen benign call uses literal eighty input plus one hundred sixty output units",
    );
    check(
      ready.ai.works.every((work) =>
        work.attempts.every((attempt) => !attempt.held),
      ),
      "known success releases all reservations",
    );
    equal(
      ready.outcomes.outcomes[0].content.contentDigest,
      selection.contentDigest,
      "saved run retains exact approved content lock",
    );
    await page
      .getByRole("heading", { name: "Combined fictional health assessment" })
      .waitFor();
    await scan("combined-ready");
    // Confirm one AI through the real finding-review API; original provenance and run locks remain immutable.
    const target = ready.analysis.review.findings.find(
      (finding) => finding.id === aiFindings[0].id,
    );
    check(
      target && target.actions.confirm,
      "AI review is available to the assigned fictional Consultant",
    );
    const originalDigests = [...target.originalDigests],
      inputLockDigest = ready.outcomes.contentDigest;
    const reviewArticle = page.locator(".review-finding").filter({
      has: page.getByRole("heading", { name: target.title, exact: true }),
    });
    await reviewArticle.getByLabel("Review action").selectOption("Confirm");
    await reviewArticle
      .getByLabel("Review reason (optional)")
      .fill("Explicit fictional confidence review");
    const confirmation = await loseOnce(
      `**/local-demo/v1/runs/${runId}/findings/${target.id}/events`,
      () =>
        reviewArticle
          .getByRole("button", { name: "Confirm", exact: true })
          .click(),
      "Retry same review event",
    );
    const confirmed = await workspace();
    equal(
      confirmed.analysis.review.findings.find((f) => f.id === target.id).state,
      "Confirmed",
      "explicit review is required to confirm AI",
    );
    equal(
      confirmed.analysis.review.findings.find((f) => f.id === target.id)
        .originalDigests,
      originalDigests,
      "review preserves generated original provenance",
    );
    equal(
      confirmed.outcomes.contentDigest,
      inputLockDigest,
      "finding review preserves exact outcome run lock",
    );
    const confirmedAi = confirmed.analysis.findings.find(
      (f) => f.id === target.id,
    );
    check(
      confirmedAi.method === "AI" &&
        confirmedAi.initialState === "Proposed" &&
        confirmedAi.state === "Confirmed" &&
        !confirmedAi.reviewRequired &&
        Number(confirmedAi.confidencePercent) === 80,
      "explicit AI confirmation retains proposed original state and confidence while resolving required review",
    );
    equal(
      confirmed.analysis.review.findings
        .find((f) => f.id === target.id)
        .history.filter((event) => event.eventId === confirmation.eventId)
        .length,
      1,
      "response-loss confirmation retains exactly one saved finding event",
    );
    await page.reload();
    await page
      .getByRole("heading", { name: "Combined fictional health assessment" })
      .waitFor();
    const entry = confirmed.priority.entries[0];
    const confirmedReason = page.getByLabel(
      `Reason for ${entry.original.optionId}`,
      { exact: true },
    );
    await confirmedReason.waitFor();
    check(
      await confirmedReason.isVisible(),
      "combined decoder accepts confirmed AI and exposes verified planning controls after reload",
    );
    check(
      entry && digest(confirmed.priority.source.sourceDigest),
      "verified planning source and option are available",
    );
    const taskSourceBefore =
      confirmed.analysis.planningTasks.source.artifactSource.sourceDigest;
    await page
      .getByLabel(`Reason for ${entry.original.optionId}`, { exact: true })
      .fill("Explicit fictional priority sidecar decision");
    const priorityEvent = await loseOnce(
      `**${prefix}/runs/${runId}/planning/events`,
      () =>
        page
          .getByRole("button", {
            name: `Override priority ${entry.original.optionId}`,
            exact: true,
          })
          .click(),
      "Retry same event",
    );
    const planned = await workspace(),
      current = planned.priority.entries.find(
        (value) => value.original.optionId === entry.original.optionId,
      );
    equal(
      current.history.filter((event) => event.eventId === priorityEvent.eventId)
        .length,
      1,
      "lost planning response creates one immutable sidecar event",
    );
    equal(
      current.originalPriority,
      entry.originalPriority,
      "priority override preserves literal original calculation",
    );
    equal(
      planned.analysis.planningTasks.source.artifactSource.sourceDigest,
      taskSourceBefore,
      "priority sidecar preserves task source freshness",
    );
    // Attest the exact relevant artifacts; create a task through the real UI with a lost acknowledgement.
    const option = planned.analysis.planningTasks.options.find(
      (value) => value.findingState === "Confirmed",
    );
    check(option, "confirmed finding has a concrete planning option");
    const relevant = planned.analysis.artifactReview.artifacts.filter(
      (artifact) => artifact.scopedOptionId === option.identity.scopedOptionId,
    );
    for (const artifact of relevant.filter((value) => value.canReview))
      await post(
        `${prefix}/runs/${runId}/artifacts/${artifact.artifactId}/events`,
        {
          eventId: randomUUID(),
          kind: "ReviewForPlanning",
          expectedRevision: artifact.revision,
          expectedSourceDigest:
            planned.analysis.artifactReview.source.sourceDigest,
          reason: "Explicit fictional planning attestation",
        },
      );
    const attested = await workspace(),
      available = attested.analysis.planningTasks.options.find(
        (value) => value.identity.taskId === option.identity.taskId,
      );
    check(
      available.canCreate,
      "all exact artifact attestations make task creation eligible",
    );
    await page.reload();
    await page
      .getByRole("heading", {
        name: "Artifact attestations and planning tasks",
      })
      .waitFor();
    await page
      .getByLabel("Reason for artifact or task event")
      .fill("Explicit fictional planning task");
    const optionArticle = page.locator("article").filter({
      has: page.getByRole("heading", {
        name: `Task option · ${option.identity.scopedOptionId}`,
        exact: true,
      }),
    });
    const taskEvent = await loseOnce(
      `**${prefix}/runs/${runId}/tasks/${option.identity.taskId}/events`,
      () =>
        optionArticle
          .getByRole("button", {
            name: "Create fictional planning task",
            exact: true,
          })
          .click(),
      "Retry the same planning event",
    );
    const taskRead = await workspace(),
      task = taskRead.analysis.planningTasks.entries.find(
        (value) => value.identity.taskId === option.identity.taskId,
      );
    check(
      task && task.freshness === "CurrentPlan" && task.status === "Planned",
      "explicit task is saved as a current planning record",
    );
    equal(
      task.history.filter((event) => event.eventId === taskEvent.eventId)
        .length,
      1,
      "lost task acknowledgement preserves exact UUID with one event",
    );
    await page
      .getByRole("button", {
        name: "Inspect current CSV snapshot",
        exact: true,
      })
      .click();
    await page
      .getByRole("checkbox", {
        name: "I understand downloaded-copy handling and the protected-link warning.",
        exact: true,
      })
      .waitFor();
    check(
      await page
        .getByRole("button", {
          name: "Download this CSV snapshot",
          exact: true,
        })
        .isDisabled(),
      "download requires explicit protected-copy warning acknowledgement",
    );
    const capture = await read(`${prefix}/runs/${runId}/csv`);
    equal(
      capture.rows.length,
      1,
      "only explicitly created task metadata is exported",
    );
    capture.rows.forEach(verifyLinks);
    const currentPriority = taskRead.priority.entries.find(
      (value) => value.original.optionId === entry.original.optionId,
    );
    await post(`${prefix}/runs/${runId}/planning/events`, {
      eventId: randomUUID(),
      kind: "OverridePriority",
      optionId: currentPriority.original.optionId,
      expectedRevision: currentPriority.revision,
      expectedSourceDigest: taskRead.priority.source.sourceDigest,
      priorityOverride: "Immediate",
      replacementSize: null,
      assumptions: [],
      reason: "Fictional sidecar change after task creation",
    });
    const taskAfterSidecar = await workspace();
    const preservedTask = taskAfterSidecar.analysis.planningTasks.entries.find(
      (value) => value.identity.taskId === task.identity.taskId,
    );
    equal(
      preservedTask,
      task,
      "priority sidecar does not rewrite the saved task or its history",
    );
    equal(
      (await read(`${prefix}/runs/${runId}/csv`)).snapshotDigest,
      capture.snapshotDigest,
      "priority sidecar preserves the inspected task export snapshot",
    );
    await page
      .getByRole("checkbox", {
        name: "I understand downloaded-copy handling and the protected-link warning.",
        exact: true,
      })
      .check();
    const downloadPromise = page.waitForEvent("download");
    await page
      .getByRole("button", { name: "Download this CSV snapshot", exact: true })
      .click();
    const download = await downloadPromise;
    const stream = await download.createReadStream(),
      chunks = [];
    for await (const chunk of stream) chunks.push(chunk);
    const bytes = Buffer.concat(chunks);
    check(
      bytes.length <= 4 * 1024 * 1024 &&
        !bytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf])),
      "download is bounded exact UTF8 without BOM",
    );
    const expectedCsv =
      literalHeader +
      capture.rows
        .map((row) => rowKeys.map((key) => quote(row[key])).join(",") + "\r\n")
        .join("");
    equal(
      bytes,
      Buffer.from(expectedCsv, "utf8"),
      "full downloaded bytes/cells match independently authored seventeen-column snapshot oracle",
    );
    downloadedSha = hash(bytes);
    check(
      !bytes.toString("utf8").includes("<img") &&
        !bytes.toString("utf8").includes("Fictional desired behavior"),
      "CSV includes task metadata and excludes outcome/model text",
    );
    // A changed task revision invalidates an inspected snapshot without reinterpreting its source.
    await page
      .getByRole("button", {
        name: "Inspect current CSV snapshot",
        exact: true,
      })
      .click();
    await page
      .getByRole("checkbox", {
        name: "I understand downloaded-copy handling and the protected-link warning.",
        exact: true,
      })
      .waitFor();
    const inspected = await read(`${prefix}/runs/${runId}/csv`),
      latestTask = taskAfterSidecar.analysis.planningTasks.entries.find(
        (value) => value.identity.taskId === task.identity.taskId,
      );
    await post(`${prefix}/runs/${runId}/tasks/${task.identity.taskId}/events`, {
      eventId: randomUUID(),
      kind: "Comment",
      expectedRevision: latestTask.revision,
      expectedSourceDigest:
        taskAfterSidecar.analysis.planningTasks.source.artifactSource
          .sourceDigest,
      expectedAttestations:
        taskAfterSidecar.analysis.planningTasks.options.find(
          (value) => value.identity.taskId === task.identity.taskId,
        ).currentAttestations,
      reason: "Fictional snapshot revision change",
    });
    await post(
      `${prefix}/runs/${runId}/csv`,
      { requestId: randomUUID(), snapshotDigest: inspected.snapshotDigest },
      409,
    );
    const changed = await read(`${prefix}/runs/${runId}/csv`);
    check(
      changed.snapshotDigest !== inspected.snapshotDigest &&
        changed.rows[0].planFreshness === "CurrentPlan",
      "changed revision invalidates old export snapshot while task stays current",
    );
    await page
      .getByRole("checkbox", {
        name: "I understand downloaded-copy handling and the protected-link warning.",
        exact: true,
      })
      .check();
    await page
      .getByRole("button", { name: "Download this CSV snapshot", exact: true })
      .click();
    await page
      .getByText(
        "The snapshot changed or export was denied. Inspect again before another download.",
        { exact: true },
      )
      .waitFor();
    check(
      (await page
        .getByRole("button", {
          name: "Download this CSV snapshot",
          exact: true,
        })
        .count()) === 0,
      "stale delivery clears inspection and warning acknowledgement",
    );
    await navigation(changed);
    // Approved retirement keeps old run readable, while new locking of its old proof fails closed.
    await page.goto(base);
    await page.getByLabel("Evidence baseline").selectOption(baseline);
    await page.getByLabel("Assessment profile").selectOption(profile);
    await page
      .getByLabel("Reason for the next lifecycle action")
      .fill("Explicit fictional approved retirement");
    const retirement = await loseOnce(
      `**${prefix}/outcomes/events`,
      () =>
        page
          .getByRole("button", {
            name: `Retire ${outcomeId} v${version}`,
            exact: true,
          })
          .click(),
      "Retry same event",
    );
    const retiredRegistry = await read(`${prefix}/registry`),
      retired = retiredRegistry.entries.find(
        (value) =>
          value.content.outcomeId === outcomeId &&
          value.content.version === version,
      );
    equal(
      retired.state,
      "Retired",
      "approved retirement records distinct authorized lifecycle transition",
    );
    equal(
      retired.history.filter((event) => event.eventId === retirement.eventId)
        .length,
      1,
      "retirement retry preserves original fictional approval capability",
    );
    equal(
      (await workspace()).outcomes.contentDigest,
      inputLockDigest,
      "retirement does not rewrite the historical run outcome lock",
    );
    const deniedRun = randomUUID();
    await post(
      `${prefix}/runs`,
      {
        requestId: deniedRun,
        selections: [selection],
        explicitlyNoOutcomes: false,
      },
      409,
    );
    equal(
      (await api.get(`/local-demo/v1/runs/${deniedRun}`)).status(),
      404,
      "denied stale approved proof creates no run",
    );
    for (const width of [390, 320]) {
      await page.setViewportSize({ width, height: 844 });
      check(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
        `actual app ${width}px viewport has no horizontal page overflow`,
      );
      await scan(`registry-retired-mobile-${width}`);
    }
  }
  equal(errors, [], "actual application has no browser runtime errors");
  await mkdir(output, { recursive: true });
  await page.screenshot({
    path: resolve(output, `${role}-final.png`),
    fullPage: true,
  });
  await writeFile(
    resolve(output, `${role}-report.json`),
    JSON.stringify(
      {
        role,
        runId,
        checks,
        accessibility: scans,
        downloadedSha: downloadedSha ?? null,
        runtimeErrors: errors,
      },
      null,
      2,
    ) + "\n",
  );
  console.log(
    `PASS ${checks.length} Phase1B real-host ${role} checks; run ${runId}`,
  );
} catch (error) {
  await mkdir(output, { recursive: true });
  await writeFile(
    resolve(output, `${role}-failure.json`),
    JSON.stringify(
      {
        role,
        runId: runId ?? null,
        checks,
        accessibility: scans,
        runtimeErrors: errors,
        failure: error.message,
      },
      null,
      2,
    ) + "\n",
  );
  throw error;
} finally {
  await api.dispose();
  await context.close();
  await browser.close();
}
