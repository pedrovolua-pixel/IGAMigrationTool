import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { resolve, isAbsolute, join } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";

const options = new Map();
for (let i = 2; i < process.argv.length; i += 2) {
  const key = process.argv[i],
    value = process.argv[i + 1];
  if (
    ![
      "--preview-directory",
      "--playwright-module",
      "--browser-executable",
    ].includes(key) ||
    !value ||
    options.has(key)
  )
    throw new Error("V9 invalid CLI");
  options.set(key, value);
}
const directory = options.get("--preview-directory");
const modulePath = options.get("--playwright-module");
if (
  !directory ||
  !isAbsolute(directory) ||
  !modulePath ||
  !isAbsolute(modulePath)
)
  throw new Error("V9 absolute paths required");
const { chromium } = await import(
  pathToFileURL(join(modulePath, "index.mjs")).href
);
const manifest = JSON.parse(
  await readFile(join(directory, "preview-manifest.json"), "utf8"),
);
const names = ["benign.html", "hostile.html", "empty.html", "conflict.html"];
const contents = new Map();
const expectedDomHostileText = manifest.expectedHostileText
  .replace(/\r\n?/g, "\n")
  .replace(/\0/g, "");
let checks = 0,
  lastCode = "initialization";
const ok = (value, code) => {
  lastCode = code;
  assert.ok(value, code);
  checks++;
};
const eq = (actual, expected, code) => {
  lastCode = code;
  assert.deepEqual(actual, expected, code);
  checks++;
};
const sha = (value) => createHash("sha256").update(value).digest("hex");
for (const name of names) {
  const content = await readFile(join(directory, name));
  eq(
    sha(content),
    manifest.files.find((x) => x.name === name)?.sha256,
    "served-file-source-digest",
  );
  contents.set("/" + name, content);
  if (name === "hostile.html") {
    ok(
      content.includes(Buffer.from("\0NUL sentinel")),
      "serialized-hostile-NUL-byte-preserved",
    );
    ok(
      content.includes(Buffer.from("\r\nCRLF sentinel")),
      "serialized-hostile-CRLF-bytes-preserved",
    );
    ok(
      expectedDomHostileText !== manifest.expectedHostileText,
      "explicit-HTML-parser-normalization-expected",
    );
  }
}
const served = [],
  denied = [];
const server = createServer((req, res) => {
  if (req.method !== "GET" || !contents.has(req.url)) {
    denied.push(req.url);
    res.writeHead(404, {
      "Content-Type": "text/plain",
      "Cache-Control": "no-store",
    });
    res.end("Unavailable");
    return;
  }
  served.push(req.url);
  res.writeHead(200, {
    "Content-Type": "text/html; charset=utf-8",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  res.end(contents.get(req.url));
});
let browser;
const groups = [],
  screenshots = [],
  errors = [],
  dialogs = [],
  requests = [],
  blockedRequests = [];
const output = resolve(fileURLToPath(new URL(".", import.meta.url)));
try {
  await new Promise((res, rej) => {
    server.once("error", rej);
    server.listen(0, "127.0.0.1", res);
  });
  const base = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({
    headless: true,
    ...(options.get("--browser-executable")
      ? { executablePath: options.get("--browser-executable") }
      : {}),
  });
  for (const viewport of [
    { name: "desktop", width: 1440, height: 1000 },
    { name: "mobile", width: 390, height: 844 },
    { name: "reflow", width: 320, height: 844 },
  ]) {
    const context = await browser.newContext({
      viewport: { width: viewport.width, height: viewport.height },
    });
    await context.route("**/*", async (route) => {
      const request = route.request();
      const allowed =
        request.method() === "GET" &&
        request.resourceType() === "document" &&
        names.some((name) => request.url() === base + "/" + name);
      if (allowed) await route.continue();
      else {
        blockedRequests.push("unexpected-request");
        await route.abort();
      }
    });
    const page = await context.newPage();
    page.on("pageerror", () => errors.push("pageerror"));
    page.on("dialog", async (dialog) => {
      dialogs.push("dialog");
      await dialog.dismiss();
    });
    page.on("request", (request) =>
      requests.push({ url: request.url(), type: request.resourceType() }),
    );
    for (const name of names) {
      const before = requests.length;
      const response = await page.goto(base + "/" + name, {
        waitUntil: "networkidle",
      });
      eq(response.status(), 200, "actual-loopback-html-200");
      eq(await page.title(), "Synthetic AI proposal preview", "title");
      eq(await page.locator("html").getAttribute("lang"), "en", "language");
      eq(await page.locator("h1").count(), 1, "one-title");
      eq(
        await page.locator("h1").textContent(),
        "Synthetic AI proposal preview",
        "semantic-title",
      );
      eq(
        await page.locator(".disclaimer").textContent(),
        manifest.disclaimer,
        "visible-fixed-disclaimer",
      );
      ok(await page.locator(".disclaimer").isVisible(), "disclaimer-visible");
      eq(
        await page
          .locator('meta[http-equiv="Content-Security-Policy"]')
          .getAttribute("content"),
        "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'",
        "unchanged-fixed-policy",
      );
      eq(
        await page
          .locator(
            "script,form,iframe,object,embed,svg,img,video,audio,link,input,button",
          )
          .count(),
        0,
        "no-active-or-external-elements",
      );
      const attributes = await page
        .locator("*")
        .evaluateAll((nodes) =>
          nodes.flatMap((node) =>
            [...node.attributes]
              .filter(
                (attribute) =>
                  /^on/i.test(attribute.name) ||
                  ["src", "srcset", "action", "formaction"].includes(
                    attribute.name,
                  ),
              )
              .map((attribute) => attribute.name),
          ),
        );
      eq(attributes, [], "no-executable-event-resource-attributes");
      eq(
        await page.evaluate(() => globalThis.v9Injected),
        undefined,
        "no-injected-script-effect",
      );
      const anchors = await page
        .locator("a")
        .evaluateAll((nodes) => nodes.map((node) => node.getAttribute("href")));
      eq(
        anchors,
        ["#preview-content", "#source", "#proposals"],
        "fixed-literal-in-document-anchors-only",
      );
      const bounds = await page.evaluate(() => ({
        viewport: innerWidth,
        document: document.documentElement.scrollWidth,
        body: document.body.scrollWidth,
      }));
      ok(
        bounds.document <= bounds.viewport + 1 &&
          bounds.body <= bounds.viewport + 1,
        "no-horizontal-overflow",
      );
      const source = await page.locator("#source dd").allTextContents();
      const s = manifest.source;
      const file = manifest.files.find((value) => value.name === name);
      eq(
        source.slice(0, 10),
        [
          s.customerId,
          s.projectId,
          s.environmentId,
          s.runId,
          s.baselineDigest,
          s.profileDigest,
          s.normalizationVersion,
          s.redactionVersion,
          s.promptVersion,
          manifest.packetDigest,
        ],
        "all-source-fields-exact",
      );
      eq(source[11], file.previewDigest, "displayed-preview-digest-exact");
      if (name === "benign.html" || name === "conflict.html") {
        eq(
          source[10],
          manifest.proposalDigest,
          "displayed-proposal-digest-exact",
        );
        eq(
          await page.locator("article h3").allTextContents(),
          ["Proposal proposal-03", "Proposal proposal-09"],
          "ordered-complete-proposals",
        );
        eq(await page.locator("article").count(), 2, "complete-proposals");
        const second = page.locator("article").nth(1);
        eq(
          await second.locator("h4").allTextContents(),
          [
            "Facts",
            "Inferences",
            "Assumptions",
            "Suggestions",
            "Missing context",
            "Uncertainty",
            "Conflicting evidence IDs",
          ],
          "typed-categories-distinguished",
        );
        const texts = await second
          .locator("section")
          .evaluateAll((nodes) => nodes.map((node) => node.textContent));
        ok(
          texts[0].includes("Fictional schedule observation") &&
            texts[1].includes("Fictional timing inference") &&
            texts[2].includes("Fictional operating assumption") &&
            texts[3].includes("Fictional review suggestion"),
          "every-typed-statement-visible",
        );
        ok(
          texts[4].includes("Fictional context one") &&
            texts[4].indexOf("Fictional context one") <
              texts[4].indexOf("Fictional context two"),
          "ordered-missing-context-visible",
        );
        ok(
          texts[5].includes("Fictional uncertainty remains"),
          "uncertainty-visible",
        );
        const citations = await second
          .locator("section")
          .last()
          .locator("li")
          .allTextContents();
        eq(
          citations,
          ["ev-" + "c".repeat(64), "ev-" + "d".repeat(64)],
          "both-conflicting-provenances-visible",
        );
        const evA = "ev-" + "c".repeat(64),
          evB = "ev-" + "d".repeat(64);
        const ruleA = "fixture-rule-schedule-v1",
          ruleB = "fixture-rule-conflict-v1";
        const expectedCitations = [
          [[[evB], [ruleB]], null, null, null],
          [
            [[evA], [ruleA]],
            [
              [evA, evB],
              [ruleB, ruleA],
            ],
            [[evB], [ruleB]],
            [
              [evA, evB],
              [ruleB, ruleA],
            ],
          ],
        ];
        for (let proposal = 0; proposal < 2; proposal++) {
          for (let category = 0; category < 4; category++) {
            const section = page
              .locator("article")
              .nth(proposal)
              .locator("section")
              .nth(category);
            const expected = expectedCitations[proposal][category];
            if (expected === null)
              eq(
                await section.locator("ol > li").count(),
                0,
                "empty-category-no-invented-statements",
              );
            else {
              eq(
                await section.locator("ol > li").count(),
                1,
                "complete-statement-per-category",
              );
              eq(
                await section
                  .locator("ol > li > ul")
                  .nth(0)
                  .locator("li")
                  .allTextContents(),
                expected[0],
                "every-statement-evidence-citations",
              );
              eq(
                await section
                  .locator("ol > li > ul")
                  .nth(1)
                  .locator("li")
                  .allTextContents(),
                expected[1],
                "every-statement-rule-citations",
              );
            }
          }
        }
        const disclaimer = await page.locator("article > p").allTextContents();
        eq(
          disclaimer,
          [
            "Proposed and untrusted. Cited statements are not verified facts.",
            "Proposed and untrusted. Cited statements are not verified facts.",
          ],
          "per-proposal-untrusted-warning",
        );
      } else if (name === "hostile.html") {
        const direct = await page
          .locator("article section ol > li > p:first-child")
          .allTextContents();
        eq(direct.length, 5, "all-hostile-statement-locations");
        for (const text of direct)
          eq(
            text,
            expectedDomHostileText,
            "hostile-unicode-text-with-HTML-normalization",
          );
        const contextText = await page
          .locator("article section")
          .filter({ has: page.locator("h4", { hasText: /^Missing context$/ }) })
          .locator("li")
          .allTextContents();
        eq(
          contextText.filter((text) => text === expectedDomHostileText).length,
          2,
          "hostile-context-text-fidelity",
        );
        const uncertain = await page
          .locator("article section")
          .filter({ has: page.locator("h4", { hasText: /^Uncertainty$/ }) })
          .locator("p")
          .allTextContents();
        eq(
          uncertain,
          [expectedDomHostileText, expectedDomHostileText],
          "hostile-uncertainty-text-fidelity",
        );
      } else {
        eq(
          await page.locator("article").count(),
          0,
          "empty-no-invented-proposal",
        );
        ok(
          (await page.locator("#proposals").textContent()).includes(
            "No proposals were returned. This does not establish healthy or complete assessment coverage.",
          ),
          "empty-coverage-warning-visible",
        );
      }
      eq(
        requests.slice(before),
        [{ url: base + "/" + name, type: "document" }],
        "one-document-request-no-external-resource-dispatch",
      );
      eq(errors.length, 0, "no-pageerror");
      eq(dialogs.length, 0, "no-dialog");
    }
    await page.goto(base + "/hostile.html", { waitUntil: "networkidle" });
    await page.keyboard.press("Tab");
    eq(
      await page.evaluate(() => document.activeElement?.textContent),
      "Skip to preview content",
      "keyboard-skip-first",
    );
    ok(await page.locator(".skip").isVisible(), "keyboard-visible-skip");
    await page.keyboard.press("Enter");
    eq(
      await page.evaluate(() => document.activeElement?.id),
      "preview-content",
      "keyboard-skip-focus",
    );
    await page.keyboard.press("Tab");
    eq(
      await page.evaluate(() => document.activeElement?.getAttribute("href")),
      "#source",
      "keyboard-source-anchor",
    );
    await page.keyboard.press("Enter");
    eq(new URL(page.url()).hash, "#source", "keyboard-source-navigation");
    // Source hash navigation changes the browser's sequential focus starting point.
    // Begin a separate keyboard-only path from document start for Proposals.
    await page.goto(base + "/hostile.html", { waitUntil: "networkidle" });
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await page.keyboard.press("Tab");
    await page.keyboard.press("Tab");
    eq(
      await page.evaluate(() => document.activeElement?.getAttribute("href")),
      "#proposals",
      "keyboard-proposals-anchor",
    );
    await page.keyboard.press("Enter");
    eq(new URL(page.url()).hash, "#proposals", "keyboard-proposals-navigation");
    await page.locator("article").first().scrollIntoViewIfNeeded();
    const viewportPath = join(output, viewport.name + "-viewport.png");
    const fullPath = join(output, viewport.name + "-preview.png");
    await page.screenshot({ path: viewportPath });
    await page.screenshot({ path: fullPath, fullPage: true });
    screenshots.push({
      viewport: viewport.name,
      viewportPath,
      fullPath,
      width: viewport.width,
      height: viewport.height,
      sha256: sha(await readFile(viewportPath)),
      fullSha256: sha(await readFile(fullPath)),
    });
    groups.push("V9-BROWSER-" + viewport.name);
    console.log(
      "PASS V9-BROWSER-" +
        viewport.name +
        " inert text/source/categories/empty/conflict/network/keyboard/reflow",
    );
    await context.close();
  }
  eq(denied.length, 0, "no-unexpected-server-request");
  eq(
    blockedRequests.length,
    0,
    "no-blocked-external-or-unexpected-request-attempt",
  );
  eq(errors.length, 0, "no-page-errors-total");
  eq(dialogs.length, 0, "no-dialogs-total");
  const metadata = {
    schemaVersion: "v9-browser-execution-v1",
    result: "PASS",
    checks,
    groups,
    browserVersion: browser.version(),
    nodeVersion: process.version,
    playwrightVersion: JSON.parse(
      await readFile(join(modulePath, "package.json"), "utf8"),
    ).version,
    harnessSha256: sha(await readFile(fileURLToPath(import.meta.url))),
    controlNormalization: {
      context: "HTML body text",
      sourceNulCount: [...manifest.expectedHostileText].filter(
        (c) => c === "\0",
      ).length,
      expectedDomNulCount: [...expectedDomHostileText].filter((c) => c === "\0")
        .length,
      sourceCrCount: [...manifest.expectedHostileText].filter((c) => c === "\r")
        .length,
      expectedDomCrCount: [...expectedDomHostileText].filter((c) => c === "\r")
        .length,
    },
    inputManifestSha256: sha(
      await readFile(join(directory, "preview-manifest.json")),
    ),
    viewports: screenshots,
    sourceFiles: manifest.files.map((value) => ({
      name: value.name,
      sha256: value.sha256,
      previewDigest: value.previewDigest,
    })),
    requests: {
      documents: requests.filter((value) => value.type === "document").length,
      externalOrNonDocument: requests.filter(
        (value) =>
          value.type !== "document" || !value.url.startsWith(base + "/"),
      ).length,
    },
    blockedRequests: blockedRequests.length,
    errors: errors.length,
    dialogs: dialogs.length,
    limits: [
      "Chromium only; supported Windows browser and manual screen-reader acceptance NOT VERIFIED",
      "Fictional local preview; production sandbox/auth/provider/semantic redaction/model quality NOT VERIFIED",
      "Chromium HTML body-text parsing removes serialized NUL and normalizes CR/CRLF to LF; canonical typed values and serialized source bytes retain originals",
    ],
  };
  await writeFile(
    join(output, "execution.json"),
    JSON.stringify(metadata, null, 2) + "\n",
  );
  console.log(
    checks +
      " independent actual-browser checks passed. No supplied payload logged.",
  );
} catch {
  console.error(
    "FAIL V9 actual-browser code=" + lastCode + "; payload suppressed.",
  );
  process.exitCode = 1;
  await writeFile(
    join(output, "execution.json"),
    JSON.stringify(
      {
        schemaVersion: "v9-browser-execution-v1",
        result: "FAIL",
        checks,
        lastCode,
        limits: [
          "No completion claim; supplied exception and payload suppressed",
        ],
      },
      null,
      2,
    ) + "\n",
  );
} finally {
  try {
    if (browser) await browser.close();
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}
