// Independent guarded-host consumer. Expectations were committed at b184dbc.
import assert from "node:assert/strict";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { createHash, randomUUID } from "node:crypto";
import {
  chromium,
  request,
} from "/Users/pedrovolu/Documents/ChatGPT/IGAMigrationTool/tests/e2e/consultant-demo/node_modules/playwright/index.mjs";

const url = process.env.WF04_URL ?? "http://127.0.0.1:5184";
const evidence =
  process.env.WF04_EVIDENCE ??
  "/private/tmp/iga-m08-cycle04-evidence/verification";
await mkdir(evidence, { recursive: true });
const expected = JSON.parse(
  await readFile(
    new URL(
      "../../integration/SyntheticEvaluationWorkflowIndependent.Tests/expected-v1.json",
      import.meta.url,
    ),
    "utf8",
  ),
);
let checks = 0;
const check = (actual, wanted, name) => {
  checks++;
  assert.deepEqual(actual, wanted, name);
};
const sha = (text) => createHash("sha256").update(text, "utf8").digest("hex");
const api = await request.newContext({ baseURL: url });
const keys = (value) => Object.keys(value).sort();
async function wrapper(response, status, issue = null) {
  check(response.status(), status, "HTTP status");
  check(response.headers()["cache-control"], "no-store", "no cache");
  check(response.headers()["x-content-type-options"], "nosniff", "nosniff");
  const body = await response.json();
  check(
    keys(body),
    [
      "alreadyApplied",
      "csrfToken",
      "demoOnly",
      "issue",
      "payload",
      "schemaVersion",
    ],
    "closed six-key response",
  );
  check(body.schemaVersion, 1, "schema");
  check(body.demoOnly, true, "fictional");
  check(body.issue, issue, "typed issue");
  if (issue) {
    check(body.payload, null, "failure no payload");
    check(body.csrfToken, null, "failure no token");
    check(body.alreadyApplied, null, "failure no replay data");
  }
  return body;
}
try {
  const workspace = await wrapper(
    await api.get("/local-evaluation/v1/workspace"),
    200,
  );
  const s = workspace.payload;
  check(s.populationDigest, expected.populationDigest, "pre-code population");
  check(s.sampleDigest, expected.sampleDigest, "pre-code sample");
  check(
    s.members.map((x) => x.original.memberId),
    expected.expectedSelectedIds,
    "pre-code selected IDs",
  );
  check(typeof workspace.csrfToken, "string", "server CSRF token");
  const initialVersion = await wrapper(
    await api.get(`/local-evaluation/v1/versions/${s.aggregateRevision}`),
    200,
  );
  const v = initialVersion.payload;
  check(
    sha(v.snapshotCanonicalJson),
    v.contentDigest,
    "independent raw snapshot SHA",
  );
  for (const [body, digest] of [
    ["versionManifestJson", "versionManifestDigest"],
    ["accuracyCanonicalJson", "accuracyDigest"],
    ["warningCanonicalJson", "warningDigest"],
  ])
    check(sha(v[body]), v[digest], "independent nested raw SHA");
  const originalVersionJson = JSON.stringify(v);
  const member = s.members[0];
  const command = {
    eventId: randomUUID(),
    memberId: member.original.memberId,
    kind: "Review",
    expectedAggregateRevision: s.aggregateRevision,
    expectedMemberRevision: member.revision,
    expectedRegistryVersionId: s.registryVersionId,
    expectedSourceDigest: s.sourceDigest,
    expectedSampleDigest: s.sampleDigest,
    outcome: "Confirmed",
    originatingClassification: null,
    reason: "Independent HTTP fictional review <script>inert text</script>",
    evidenceReferenceIds: [member.original.evidenceReferenceIds[0]],
    correction: null,
  };
  const headers = {
    Origin: url,
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": workspace.csrfToken,
  };
  const post = (data, extra = {}) =>
    api.post("/local-evaluation/v1/events", {
      data,
      headers: { ...headers, ...extra },
    });
  await wrapper(
    await api.get("/local-evaluation/v1/workspace?unexpected=1"),
    400,
    "InvalidInput",
  );
  await wrapper(
    await api.get("/local-evaluation/v1/versions/not-a-number"),
    400,
    "InvalidInput",
  );
  await wrapper(
    await api.delete("/local-evaluation/v1/workspace"),
    400,
    "InvalidInput",
  );
  await wrapper(
    await api.get("/local-evaluation/v1/workspace", {
      headers: { Host: "spoof.invalid" },
    }),
    403,
    "Denied",
  );
  await wrapper(
    await post(command, { Origin: "https://untrusted.invalid" }),
    403,
    "Denied",
  );
  await wrapper(
    await post(command, { "X-CSRF-TOKEN": "not-valid" }),
    403,
    "Denied",
  );
  await wrapper(
    await post(command, { "Content-Type": "text/plain" }),
    403,
    "Denied",
  );
  await wrapper(
    await post({ ...command, actorId: "synthetic-forged" }),
    400,
    "InvalidInput",
  );
  await wrapper(await post({ ...command, outcome: 0 }), 400, "InvalidInput");
  await wrapper(
    await post({ ...command, reason: "\u0000" }),
    400,
    "InvalidInput",
  );
  await wrapper(
    await post(
      JSON.stringify(command).replace("{", `{"eventId":"${command.eventId}",`),
    ),
    400,
    "InvalidInput",
  );
  await wrapper(
    await post(JSON.stringify(command) + " null"),
    400,
    "InvalidInput",
  );
  await wrapper(
    await post({ ...command, reason: "x".repeat(256 * 1024 + 1) }),
    400,
    "InvalidInput",
  );
  check(
    (await wrapper(await api.get("/local-evaluation/v1/workspace"), 200))
      .payload.aggregateRevision,
    s.aggregateRevision,
    "all HTTP denials no writes",
  );
  const accepted = await wrapper(await post(command), 200);
  check(accepted.alreadyApplied, false, "first command new event");
  check(
    accepted.payload.version,
    s.aggregateRevision + 1,
    "one immutable new outcome version",
  );
  const replay = await wrapper(await post(command), 200);
  check(replay.alreadyApplied, true, "exact replay");
  check(replay.payload, accepted.payload, "exact original replay receipt");
  await wrapper(
    await post({ ...command, reason: "changed reuse" }),
    409,
    "EventConflict",
  );
  check(
    JSON.stringify(
      (
        await wrapper(
          await api.get(`/local-evaluation/v1/versions/${v.version}`),
          200,
        )
      ).payload,
    ),
    originalVersionJson,
    "old raw version unchanged",
  );
  const history = (
    await wrapper(
      await api.get(
        `/local-evaluation/v1/members/${command.memberId}/history?afterSequence=0`,
      ),
      200,
    )
  ).payload;
  check(
    keys(history),
    [
      "aggregateRevision",
      "events",
      "memberId",
      "memberRevision",
      "nextAfterSequence",
      "registryVersionId",
    ],
    "history no original/controls/whole cohort",
  );
  check(
    history.events.filter((e) => e.eventId === command.eventId).length,
    1,
    "history exactly one accepted event",
  );

  const browser = await chromium.launch({
    executablePath:
      "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    headless: true,
  });
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.name));
  for (const width of [1440, 390, 320]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.goto(url);
    await page
      .getByRole("heading", { name: "Evaluation review", exact: true })
      .waitFor();
    await page.getByText("Current workspace loaded", { exact: true }).waitFor();
    check(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      true,
      "viewport reflow " + width,
    );
    check(
      await page
        .getByRole("button", {
          name: /publish|freeze|resample|export|download/i,
        })
        .count(),
      0,
      "no broader action",
    );
    await page.screenshot({
      path: `${evidence}/workspace-${width}.png`,
      fullPage: true,
    });
  }
  const beforeKeyboard = (
    await wrapper(await api.get("/local-evaluation/v1/workspace"), 200)
  ).payload.aggregateRevision;
  const memberSelector = page.getByLabel("Choose one selected member");
  await memberSelector.focus();
  await page.keyboard.press("End");
  await page.keyboard.press("Enter");
  const rationale =
    "Independent keyboard review <script>window.injected = true</script>";
  await page.getByLabel("Rationale", { exact: true }).focus();
  await page.keyboard.insertText(rationale);
  await page.getByRole("checkbox").first().focus();
  await page.keyboard.press("Space");
  await page
    .getByRole("button", { name: "Prepare review", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page
    .getByRole("heading", { name: "Prepared review", exact: true })
    .waitFor();
  const bodies = [];
  let lostResponse = true;
  await page.route("**/local-evaluation/v1/events", async (route) => {
    bodies.push(route.request().postData());
    if (lostResponse) {
      lostResponse = false;
      const response = await route.fetch();
      check(response.status(), 200, "intercepted review really committed");
      await route.abort("failed");
    } else await route.continue();
  });
  await page
    .getByRole("button", { name: "Record review", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page
    .getByRole("button", { name: "Retry same review", exact: true })
    .waitFor();
  await page
    .getByRole("button", { name: "Retry same review", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page.getByText("Current workspace loaded", { exact: true }).waitFor();
  check(bodies.length, 2, "two explicit attempts");
  check(bodies[0], bodies[1], "uncertain retry exact bytes and UUID");
  check(
    (await wrapper(await api.get("/local-evaluation/v1/workspace"), 200))
      .payload.aggregateRevision,
    beforeKeyboard + 1,
    "uncertain keyboard retry one outcome version",
  );
  await memberSelector.focus();
  await page.keyboard.press("End");
  await page.keyboard.press("Enter");
  await page
    .getByRole("button", { name: "Open related history", exact: true })
    .focus();
  await page.keyboard.press("Enter");
  await page.getByText(rationale, { exact: true }).waitFor();
  check(
    await page.evaluate(() => window.injected === undefined),
    true,
    "hostile rationale remains inert",
  );
  const versionSelect = page.getByLabel(/outcome version/i);
  await versionSelect.focus();
  await page.keyboard.press("Home");
  await page.keyboard.press("Enter");
  await page
    .getByText("Historical outcome version — review actions are unavailable.", {
      exact: true,
    })
    .waitFor();
  check(
    await page
      .getByRole("button", { name: "Prepare review", exact: true })
      .count(),
    0,
    "historical version read-only",
  );
  await page.screenshot({
    path: `${evidence}/keyboard-history-old-version.png`,
    fullPage: true,
  });
  check(errors, [], "no browser runtime errors");
  await browser.close();
  await writeFile(
    `${evidence}/host-browser-result.json`,
    JSON.stringify(
      {
        result: "PASS",
        checks,
        fixtureExpectation: sha(
          await readFile(
            new URL(
              "../../integration/SyntheticEvaluationWorkflowIndependent.Tests/expected-v1.json",
              import.meta.url,
            ),
            "utf8",
          ),
        ),
        operationalLimits: expected.unexecutedOperational,
      },
      null,
      2,
    ),
  );
  process.stdout.write(
    `Independent guarded HTTP/browser checks passed ${checks} assertions.\n`,
  );
} finally {
  await api.dispose();
}
