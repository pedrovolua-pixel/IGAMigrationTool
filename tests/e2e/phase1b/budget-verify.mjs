import assert from "node:assert/strict";
import { createHash, randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

// Coordinator supplies an already running dedicated fictional host with
// --pause-synthetic-worker. This test never manages hosts, databases or providers.
// Covers AI1B-T06/T14: real UI, owning budget receipt and explicit lost-response retry.
assert.equal(
  process.env.IGA_PHASE1B_PAUSED_HOST,
  "1",
  "explicit paused-host test window required",
);
const directory = dirname(fileURLToPath(import.meta.url));
const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5183";
const endpoint = new URL(base);
assert.equal(endpoint.protocol, "http:");
assert.equal(endpoint.hostname, "127.0.0.1");
assert.equal(endpoint.port, "5183");
assert.equal(endpoint.pathname, "/");
assert.equal(
  endpoint.search + endpoint.hash + endpoint.username + endpoint.password,
  "",
);
const dependencies =
  process.env.IGA_BROWSER_DEPENDENCIES ??
  resolve(directory, "../consultant-demo/node_modules");
const { chromium, request } = await import(
  pathToFileURL(
    process.env.IGA_PLAYWRIGHT_MODULE ??
      resolve(dependencies, "playwright/index.mjs"),
  )
);
const output =
  process.env.IGA_PHASE1B_E2E_OUTPUT ?? resolve(directory, "execution");
const prefix = "/local-demo/v1/phase1b";
const runId = randomUUID();
const reason =
  "Fictional pending work warrants the approved 900-unit allowance.";
const checks = [],
  errors = [],
  sent = [],
  observedBudgetRequests = [];
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
const api = await request.newContext({ baseURL: base });
page.on("pageerror", (error) => errors.push(error.message));
page.on("request", (request) => {
  if (
    request.method() === "POST" &&
    new URL(request.url()).pathname ===
      `${prefix}/runs/${runId}/ai/budget/events`
  )
    observedBudgetRequests.push(request.postDataJSON());
});
let headers, command, before, after;
async function read(path) {
  const response = await api.get(path);
  equal(
    response.status(),
    200,
    `actual host read ${path.replace(runId, "<run>")}`,
  );
  const value = await response.json();
  check(
    value.schemaVersion === 1 && value.demoOnly === true,
    "response remains explicitly versioned and fictional",
  );
  return value;
}
async function ledger() {
  return read(`${prefix}/runs/${runId}/ledger`);
}
async function waitFor(readValue, predicate, label) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    const value = await readValue();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw Error(label);
}
function pending(value, label) {
  equal(
    value.actor,
    "synthetic-consultant",
    `${label}: genuine server-bound Consultant`,
  );
  equal(value.runId, runId, `${label}: exact run binding`);
  check(
    value.canOverrideBudget === true,
    `${label}: pending run permits a reasoned override`,
  );
  equal(value.ai.works.length, 1, `${label}: exact frozen single AI work`);
  equal(
    value.ai.works[0].work.category,
    "OPERATIONS",
    `${label}: exact pending category`,
  );
  equal(
    value.ai.works[0].state,
    "Pending",
    `${label}: paused worker has not advanced work`,
  );
  equal(
    value.ai.works[0].attempts,
    [],
    `${label}: no provider attempt or reservation`,
  );
  equal(
    value.ai.works[0].outcomes,
    [],
    `${label}: no manufactured finding or gap`,
  );
  check(
    value.ai.budget.counters.every((c) => c.charged === 0 && c.held === 0),
    `${label}: every charge and hold stays zero`,
  );
}
function expectedCounters(value, allowance) {
  const locked = value.ai.runLock;
  return [
    {
      key: `category:${runId}:OPERATIONS`,
      charged: 0,
      held: 0,
      allowance,
      hardCeiling: 1200,
    },
    {
      key: `period:${locked.epoch}`,
      charged: 0,
      held: 0,
      allowance: 1200,
      hardCeiling: 1200,
    },
    { key: `run:${runId}`, charged: 0, held: 0, allowance, hardCeiling: 1200 },
    {
      key: `user:${locked.epoch}:${locked.initiatingConsultantId}`,
      charged: 0,
      held: 0,
      allowance: 1200,
      hardCeiling: 1200,
    },
  ].sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));
}
async function visibleCounters(value) {
  for (const counter of value.ai.budget.counters) {
    const row = page.getByRole("row").filter({
      has: page.getByRole("rowheader", { name: counter.key, exact: true }),
    });
    await waitFor(
      () => row.getByRole("cell").allTextContents(),
      (cells) =>
        JSON.stringify(cells) ===
        JSON.stringify(["0", "0", String(counter.allowance)]),
      "verified ledger did not refresh to exact allowance",
    );
    equal(
      await row.getByRole("cell").allTextContents(),
      ["0", "0", String(counter.allowance)],
      "actual built UI displays exact zero usage and scoped allowance",
    );
  }
}
try {
  const catalog = await read("/local-demo/v1/catalog");
  headers = { Origin: base, "X-CSRF-TOKEN": catalog.csrfToken };
  equal(
    (await read(`${prefix}/fixture`)).actor,
    "synthetic-consultant",
    "fixture is genuinely Consultant rather than caller-selected authority",
  );
  const created = await api.post(`${prefix}/runs`, {
    headers,
    data: { requestId: runId, selections: [], explicitlyNoOutcomes: true },
  });
  equal(
    created.status(),
    201,
    "actual host creates one explicitly empty-outcome combined run",
  );
  before = await ledger();
  pending(before, "before override");
  equal(
    before.ai.runLock.epoch,
    "synthetic-phase1b-fixture-epoch-v1",
    "shared counter keys use the literal frozen fixture epoch",
  );
  equal(
    before.ai.runLock.initiatingConsultantId,
    "synthetic-consultant",
    "user counter keys use the literal trusted initiating Consultant",
  );
  equal(
    before.outcomes.outcomes,
    [],
    "run explicitly locks an empty approved-outcome set",
  );
  equal(
    before.ai.budget.runAllowance,
    600,
    "original run allowance is literal 600",
  );
  equal(
    before.ai.budget.categoryAllowances,
    [{ key: "OPERATIONS", value: 600 }],
    "original category allowance is literal 600",
  );
  equal(
    before.ai.budget.revision,
    0,
    "original budget revision is literal zero before any override",
  );
  equal(before.ai.budget.history, [], "new budget has no manufactured history");
  equal(
    before.ai.budget.counters,
    expectedCounters(before, 600),
    "exact four original counters preserve shared period and Consultant 1200 ceilings",
  );
  equal(
    before.ai.runLock.policyVersion,
    "synthetic-automatic-ai-policy-v1",
    "original approved budget policy remains versioned",
  );
  await page.goto(`${base}/?run=${runId}`);
  await page
    .getByRole("heading", { name: "Combined Phase 1B workspace", exact: true })
    .waitFor();
  await visibleCounters(before);
  await page.getByLabel(/^Budget category/).selectOption("OPERATIONS");
  await page.getByLabel(/^Run allowance target/).selectOption("900");
  await page.getByLabel(/^Category allowance target/).selectOption("900");
  await page.getByLabel("Budget override reason", { exact: true }).fill(reason);
  const path = `${prefix}/runs/${runId}/ai/budget/events`;
  let first = true;
  const handler = async (route) => {
    const body = route.request().postDataJSON();
    sent.push(body);
    if (first) {
      first = false;
      const response = await route.fetch();
      equal(
        response.status(),
        200,
        "lost response was actually committed by the owning host",
      );
      const receipt = await response.json();
      equal(receipt.replayed, false, "first request is a new durable override");
      equal(
        receipt.value.eventId,
        body.eventId,
        "real first receipt binds exact submitted event UUID",
      );
      equal(
        receipt.value.revision,
        1,
        "first real commit advances budget once",
      );
      await response.dispose();
      await route.abort("failed");
    } else await route.continue();
  };
  await page.route(`**${path}`, handler);
  try {
    const apply = page.getByRole("button", {
      name: "Apply reasoned AI allowance override",
      exact: true,
    });
    await apply.focus();
    await page.keyboard.press("Enter");
    const retry = page.getByRole("button", {
      name: "Retry same event",
      exact: true,
    });
    await retry.waitFor();
    equal(
      sent.length,
      1,
      "uncertain response does not automatically duplicate the mutation",
    );
    command = sent[0];
    check(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/.test(
        command.eventId,
      ),
      "UI generates an explicit command UUID",
    );
    equal(
      command,
      {
        eventId: command.eventId,
        expectedRevision: 0,
        runTarget: 900,
        categoryTarget: 900,
        category: "OPERATIONS",
        reason,
      },
      "UI sends the exact reasoned approved target and original revision",
    );
    check(
      (await page
        .getByText(
          `Pending event: ${command.eventId}. Retry retains its exact run, source and revision.`,
          { exact: true },
        )
        .count()) === 1,
      "uncertain UI exposes the retained exact pending UUID",
    );
    const committedBeforeRetry = await ledger();
    equal(
      committedBeforeRetry.ai.budget.history.length,
      1,
      "lost response already has one durable history entry before retry",
    );
    equal(
      committedBeforeRetry.ai.budget.revision,
      1,
      "lost response already has one durable revision before retry",
    );
    const retryResponse = page.waitForResponse(
      (r) =>
        new URL(r.url()).pathname === path && r.request().method() === "POST",
    );
    await retry.focus();
    await page.keyboard.press("Enter");
    const replay = await retryResponse;
    equal(
      replay.status(),
      200,
      "explicit keyboard retry returns the accepted original receipt",
    );
    const receipt = await replay.json();
    equal(
      receipt.replayed,
      true,
      "second request is exact accepted replay rather than another override",
    );
    equal(
      receipt.value,
      committedBeforeRetry.ai.budget.history[0],
      "retry receipt preserves the original full attributed history record",
    );
    equal(
      sent.length,
      2,
      "exactly one new POST and one explicit retry occurred",
    );
    equal(
      sent[1],
      sent[0],
      "response-loss retry preserves the entire payload and exact UUID",
    );
  } finally {
    await page.unroute(`**${path}`, handler);
  }
  after = await ledger();
  pending(after, "after accepted retry");
  equal(
    after.ai.runLock,
    before.ai.runLock,
    "original immutable run and 600-unit policy inputs are unchanged",
  );
  equal(
    after.outcomes,
    before.outcomes,
    "budget sidecar does not rewrite locked outcome inputs",
  );
  equal(
    after.ai.budget.revision,
    1,
    "accepted retry leaves exactly one durable revision increment",
  );
  equal(
    after.ai.budget.runAllowance,
    900,
    "current run allowance is exactly 900",
  );
  equal(
    after.ai.budget.categoryAllowances,
    [{ key: "OPERATIONS", value: 900 }],
    "current OPERATIONS allowance is exactly 900",
  );
  equal(
    after.ai.budget.counters,
    expectedCounters(after, 900),
    "only run/category allowances change; period and initiating Consultant remain 1200 with zero usage",
  );
  equal(
    after.ai.budget.history.length,
    1,
    "exact replay creates no duplicate history",
  );
  const event = after.ai.budget.history[0];
  equal(
    {
      eventId: event.eventId,
      revision: event.revision,
      actorId: event.actorId,
      runTarget: event.runTarget,
      categoryTarget: event.categoryTarget,
      category: event.category,
      reason: event.reason,
    },
    {
      eventId: command.eventId,
      revision: 1,
      actorId: "synthetic-consultant",
      runTarget: 900,
      categoryTarget: 900,
      category: "OPERATIONS",
      reason,
    },
    "one durable event preserves exact trusted attribution and reason",
  );
  await visibleCounters(after);
  await waitFor(
    () =>
      page
        .getByRole("button", { name: "Retry same event", exact: true })
        .count(),
    (count) => count === 0,
    "verified success did not clear pending retry UUID",
  );
  const refreshedLedger = page.waitForResponse(
    (response) =>
      response.request().method() === "GET" &&
      new URL(response.url()).pathname === `${prefix}/runs/${runId}/ledger`,
  );
  await page
    .getByRole("button", { name: "Refresh combined workspace", exact: true })
    .click();
  equal(
    (await refreshedLedger).status(),
    200,
    "explicit refresh verifies the actual owning ledger",
  );
  await visibleCounters(after);
  equal(
    await page
      .getByRole("button", { name: "Retry same event", exact: true })
      .count(),
    0,
    "explicit verified refresh retains no pending UUID/retry action",
  );
  equal(
    (await ledger()).ai.budget,
    after.ai.budget,
    "refresh is read-only and preserves the one accepted budget event",
  );
  equal(
    observedBudgetRequests,
    [command, command],
    "refresh does not dispatch another override; entire browser request inventory contains only the original and exact retry",
  );
  equal(errors, [], "actual application has no browser runtime errors");
  await mkdir(output, { recursive: true });
  await page.screenshot({
    path: resolve(output, "budget-final.png"),
    fullPage: true,
  });
  await writeFile(
    resolve(output, "budget-report.json"),
    JSON.stringify(
      {
        status: "PASS",
        requirementIds: ["AI1B-T06", "AI1B-T14"],
        runId,
        checks,
        runtimeErrors: errors,
        sourceSha256: createHash("sha256")
          .update(await readFile(fileURLToPath(import.meta.url)))
          .digest("hex"),
        limits:
          "Already-running coordinator-paused fictional host only; no lifecycle/database reset/provider operation; no manual accessibility or production authority claim",
      },
      null,
      2,
    ) + "\n",
  );
  console.log(
    `PASS ${checks.length} Phase1B actual-host paused budget UI/retry checks; run ${runId}`,
  );
} catch (error) {
  await mkdir(output, { recursive: true });
  await writeFile(
    resolve(output, "budget-failure.json"),
    JSON.stringify(
      {
        status: "FAIL",
        runId,
        checks,
        runtimeErrors: errors,
        failure: String(error.message),
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
