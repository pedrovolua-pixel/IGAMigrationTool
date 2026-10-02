import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { isAbsolute, join, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";

const script = fileURLToPath(import.meta.url);
const allowedEnvironmentNames = [
  "PATH",
  "HOME",
  "TMPDIR",
  "TEMP",
  "TMP",
  "TZ",
  "SystemRoot",
  "WINDIR",
];
const cleanEnvironment = Object.fromEntries(
  allowedEnvironmentNames
    .filter((key) => process.env[key] !== undefined)
    .map((key) => [key, process.env[key]]),
);
// CI may call this directly. Re-enter before loading Playwright/fixtures or starting
// the renderer host; inherited application/provider/PG variables are not forwarded.
if (process.env.V11_ISOLATED_FIXTURE_PROCESS !== "1") {
  const result = spawnSync(
    process.execPath,
    [script, ...process.argv.slice(2)],
    {
      env: { ...cleanEnvironment, V11_ISOLATED_FIXTURE_PROCESS: "1" },
      stdio: "inherit",
    },
  );
  process.exit(result.status ?? 1);
}
// An inherited marker is not authority. Enforce the same environment allowlist
// after entry even when an external caller supplied that marker itself.
let removedEnvironmentCount = 0;
for (const key of Object.keys(process.env)) {
  if (
    !allowedEnvironmentNames.includes(key) &&
    key !== "V11_ISOLATED_FIXTURE_PROCESS"
  ) {
    delete process.env[key];
    removedEnvironmentCount++;
  }
}

const options = new Map();
for (let i = 2; i < process.argv.length; i += 2) {
  const key = process.argv[i],
    value = process.argv[i + 1];
  if (
    ![
      "--preview-directory",
      "--playwright-module",
      "--browser-executable",
      "--axe-module",
    ].includes(key) ||
    !value ||
    options.has(key) ||
    !isAbsolute(value)
  )
    throw new Error("V11 invalid absolute CLI");
  options.set(key, value);
}
const directory = options.get("--preview-directory");
const packagePath = options.get("--playwright-module");
if (!directory || !packagePath) throw new Error("V11 required CLI missing");
const modulePath = packagePath.endsWith(".mjs")
  ? packagePath
  : join(packagePath, "index.mjs");
const { chromium } = await import(pathToFileURL(modulePath).href);
const playwrightPackage = JSON.parse(
  await readFile(
    join(
      packagePath.endsWith(".mjs") ? resolve(packagePath, "..") : packagePath,
      "package.json",
    ),
    "utf8",
  ),
);
const axePath =
  options.get("--axe-module") ?? join(resolve(modulePath, "../.."), "axe-core");
const axePackage = JSON.parse(
  await readFile(join(axePath, "package.json"), "utf8"),
);
const axeSource = await readFile(join(axePath, "axe.min.js"), "utf8");
const manifestBytes = await readFile(join(directory, "preview-manifest.json"));
const manifest = JSON.parse(manifestBytes);
const names = ["normal.html", "hostile.html", "empty.html"];
const contents = new Map();
const sha = (value) => createHash("sha256").update(value).digest("hex");
let checks = 0,
  lastCode = "initialization",
  browserVersion = "NOT VERIFIED";
const equal = (actual, expected, code) => {
  lastCode = code;
  assert.deepEqual(actual, expected, code);
  checks++;
};
const require = (value, code) => {
  lastCode = code;
  assert.ok(value, code);
  checks++;
};
// HtmlEncoder emits control characters as numeric references. A zero reference
// becomes U+FFFD; referenced CR/LF survive tokenizer preprocessing unchanged.
const domText = (value) => String(value).replace(/\0/g, "\uFFFD");
const expectedCases = manifest.expected.cases;
equal(
  manifest.schemaVersion,
  "v11-generated-preview-manifest-v1",
  "fixed-generated-manifest-schema",
);
equal(
  manifest.expected.schemaVersion,
  "v11-independent-expectations-v1",
  "independent-manifest-schema",
);
equal(playwrightPackage.version, "1.62.1", "pinned-playwright-version");
equal(axePackage.version, "4.13.0", "pinned-axe-version");
require(Object.keys(process.env).every(
  (key) =>
    allowedEnvironmentNames.includes(key) ||
    key === "V11_ISOLATED_FIXTURE_PROCESS",
), "post-entry-environment-allowlist-enforced");
equal(
  process.env.IGA_V11_ENV_CANARY,
  undefined,
  "fictional-inherited-canary-not-forwarded",
);
for (const name of names) {
  const content = await readFile(join(directory, name));
  const entry = manifest.files.find((value) => value.name === name);
  require(entry !== undefined, "exact-fixture-manifest-membership");
  equal(sha(content), entry.sha256, "served-html-byte-digest");
  equal(
    entry.packageDigest,
    expectedCases[name.slice(0, -5)].packageDigest,
    "served-source-package-digest",
  );
  contents.set("/" + name, content);
}
const served = [],
  denied = [],
  requests = [],
  blocked = [],
  errors = [],
  dialogs = [],
  screenshots = [],
  groups = [],
  accessibility = [];
const server = createServer((request, response) => {
  if (request.method !== "GET" || !contents.has(request.url)) {
    denied.push("unexpected-host-request");
    response.writeHead(404, {
      "Content-Type": "text/plain",
      "Cache-Control": "no-store",
    });
    response.end("Unavailable");
    return;
  }
  served.push(request.url);
  response.writeHead(200, {
    "Content-Type": "text/html; charset=utf-8",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  response.end(contents.get(request.url));
});
let browser;
const output = fileURLToPath(new URL(".", import.meta.url));

// Independently compare the renderer's visible recursive tree, using property
// names and array membership from the literal expected value, not runtime bytes.
async function verifyTree(locator, expected, code) {
  const tag = await locator.evaluate(
    (node) => node.firstElementChild?.tagName ?? null,
  );
  if (
    expected !== null &&
    typeof expected === "object" &&
    !Array.isArray(expected)
  ) {
    equal(tag, "DL", code + "-object-kind");
    const dl = locator.locator(":scope > dl");
    const terms = await dl.locator(":scope > dt").allTextContents();
    equal(
      [...terms].sort(),
      Object.keys(expected).sort(),
      code + "-complete-properties",
    );
    equal(new Set(terms).size, terms.length, code + "-no-duplicate-properties");
    const definitions = dl.locator(":scope > dd");
    equal(
      await definitions.count(),
      terms.length,
      code + "-complete-definitions",
    );
    for (let i = 0; i < terms.length; i++)
      await verifyTree(definitions.nth(i), expected[terms[i]], code);
  } else if (Array.isArray(expected)) {
    equal(tag, "OL", code + "-array-kind");
    const items = locator.locator(":scope > ol > li");
    equal(await items.count(), expected.length, code + "-complete-array");
    for (let i = 0; i < expected.length; i++)
      await verifyTree(items.nth(i), expected[i], code);
  } else {
    equal(tag, null, code + "-inert-text-leaf");
    equal(
      await locator.textContent(),
      expected === null ? "Not supplied" : domText(expected),
      code + "-exact-text",
    );
    require(await locator.isVisible(), code + "-visible-text");
  }
}

async function fields(locator, values, code) {
  const names = await locator.locator(":scope > dl > dt").allTextContents();
  equal(names, Object.keys(values), code + "-labels");
  equal(
    await locator.locator(":scope > dl > dd").allTextContents(),
    Object.values(values).map(domText),
    code + "-values",
  );
}

try {
  lastCode = "owned-loopback-server-start";
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const base = `http://127.0.0.1:${server.address().port}`;
  lastCode = "owned-native-browser-start";
  browser = await chromium.launch({
    headless: true,
    env: cleanEnvironment,
    ...(options.get("--browser-executable")
      ? { executablePath: options.get("--browser-executable") }
      : {}),
  });
  browserVersion = browser.version();
  for (const viewport of [
    { name: "desktop", width: 1440, height: 1000 },
    { name: "mobile", width: 390, height: 844 },
    { name: "reflow", width: 320, height: 844 },
  ]) {
    const context = await browser.newContext({
      viewport: { width: viewport.width, height: viewport.height },
      serviceWorkers: "block",
    });
    await context.route("**/*", async (route) => {
      const request = route.request();
      if (
        request.method() === "GET" &&
        request.resourceType() === "document" &&
        names.some((name) => request.url() === base + "/" + name)
      )
        await route.continue();
      else {
        blocked.push("unexpected-browser-request");
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
      requests.push({
        method: request.method(),
        type: request.resourceType(),
        owned: names.some((name) => request.url() === base + "/" + name),
      }),
    );
    for (const name of names) {
      const before = requests.length;
      const expected = expectedCases[name.slice(0, -5)],
        snapshot = expected.snapshot;
      const response = await page.goto(base + "/" + name, {
        waitUntil: "networkidle",
      });
      equal(response.status(), 200, "actual-owned-rendered-html");
      equal(await page.title(), "Fictional fix-package preview", "fixed-title");
      equal(
        await page.locator("html").getAttribute("lang"),
        "en",
        "fixed-language",
      );
      equal(
        await page.locator("h1").allTextContents(),
        ["Fictional fix-package preview"],
        "single-semantic-title",
      );
      equal(
        await page.locator("header .warning").textContent(),
        manifest.expected.disclaimer,
        "exact-visible-disclaimer",
      );
      require(await page
        .locator("header .warning")
        .isVisible(), "disclaimer-visible-before-disclosure");
      equal(
        await page.locator("header > p").last().textContent(),
        "Status: Unverified",
        "always-unverified-visible-status",
      );
      equal(
        await page
          .locator('meta[http-equiv="Content-Security-Policy"]')
          .getAttribute("content"),
        "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'",
        "fixed-CSP",
      );
      equal(
        await page
          .locator(
            "script,form,iframe,object,embed,svg,img,video,audio,link,input,button,textarea,select,canvas",
          )
          .count(),
        0,
        "no-active-resource-or-action-nodes",
      );
      equal(
        await page
          .locator("*")
          .evaluateAll((nodes) =>
            nodes.flatMap((node) =>
              [...node.attributes]
                .filter(
                  (attribute) =>
                    /^on/i.test(attribute.name) ||
                    [
                      "src",
                      "srcset",
                      "action",
                      "formaction",
                      "download",
                    ].includes(attribute.name),
                )
                .map((attribute) => attribute.name),
            ),
          ),
        [],
        "no-event-resource-action-attributes",
      );
      equal(
        await page.evaluate(() => globalThis.v11Injected),
        undefined,
        "hostile-script-never-executed",
      );
      equal(
        await page
          .locator("a")
          .evaluateAll((nodes) =>
            nodes.map((node) => node.getAttribute("href")),
          ),
        ["#main", "#packages", "#historical", "#provenance"],
        "fixed-local-fragments-only",
      );
      // Native keyboard-only skip and section navigation before programmatic
      // disclosure expansion, so closed content cannot mask missing navigation.
      await page.keyboard.press("Tab");
      equal(
        await page.locator(":focus").getAttribute("href"),
        "#main",
        "keyboard-first-stop-skip",
      );
      await page.keyboard.press("Enter");
      equal(
        await page.locator(":focus").getAttribute("id"),
        "main",
        "keyboard-skip-focus-main",
      );
      await page.locator("nav a").first().focus();
      await page.keyboard.press("Enter");
      equal(
        new URL(page.url()).hash,
        "#packages",
        "keyboard-fragment-navigation",
      );
      const firstSummary = page.locator("details summary").first();
      await firstSummary.focus();
      await page.keyboard.press("Enter");
      equal(
        await firstSummary.evaluate((node) => node.parentElement.open),
        true,
        "native-disclosure-keyboard-open",
      );
      await page.keyboard.press("Space");
      equal(
        await firstSummary.evaluate((node) => node.parentElement.open),
        false,
        "native-disclosure-keyboard-close",
      );
      await page.locator("details").evaluateAll((nodes) =>
        nodes.forEach((node) => {
          node.open = true;
        }),
      );
      const boundary = page.locator("main > section").first();
      equal(
        await boundary.locator("ul").nth(0).locator("li").allTextContents(),
        snapshot.warnings,
        "complete-current-warnings",
      );
      equal(
        await boundary.locator("ul").nth(1).locator("li").allTextContents(),
        snapshot.unavailableSections,
        "complete-current-unavailable-boundary",
      );
      const packages = page.locator("#packages > details");
      equal(
        await packages.count(),
        snapshot.packages.length,
        "complete-unchanged-groups",
      );
      for (let i = 0; i < snapshot.packages.length; i++) {
        const actual = packages.nth(i),
          package_ = snapshot.packages[i];
        equal(
          await actual.locator(":scope > summary").textContent(),
          "Package for finding " + package_.findingId,
          "finding-membership-summary",
        );
        await fields(
          actual,
          {
            "Package ID": package_.packageId,
            "Finding ID": package_.findingId,
          },
          "package-provenance",
        );
        const options = actual.locator(":scope > details");
        equal(
          await options.count(),
          package_.options.length,
          "complete-scoped-options",
        );
        for (let j = 0; j < package_.options.length; j++) {
          const option = package_.options[j],
            optionNode = options.nth(j);
          await fields(
            optionNode,
            { "Scoped option ID": option.scopedOptionId },
            "option-provenance",
          );
          const articles = optionNode.locator(":scope > article");
          equal(await articles.count(), 3, "three-fixed-artifacts-per-option");
          for (let k = 0; k < option.artifacts.length; k++) {
            const artifact = option.artifacts[k],
              article = articles.nth(k);
            equal(
              await article.locator("h3").textContent(),
              artifact.kind + " — Unverified",
              "artifact-kind-unverified-title",
            );
            await fields(
              article,
              {
                "Artifact ID": artifact.artifactId,
                "Template ID": artifact.templateId,
                Kind: artifact.kind,
                "Artifact status": "Unverified",
              },
              "complete-artifact-provenance",
            );
            equal(
              await article.locator("pre > code").textContent(),
              artifact.text,
              "exact-encoded-fixed-code-visible",
            );
            require(await article
              .locator("pre > code")
              .isVisible(), "fixed-code-visible");
          }
        }
      }
      if (snapshot.packages.length === 0) {
        equal(
          await page.locator("#packages .warning").textContent(),
          "No findings were supplied; no fix packages or actions are available. Empty input does not establish a healthy environment.",
          "empty-explicit-no-health-or-action-claim",
        );
        equal(
          await page.locator("#historical > details").count(),
          1,
          "empty-no-invented-finding-guidance",
        );
      }
      const templates = page
        .locator("main > section")
        .nth(2)
        .locator(":scope > details");
      equal(await templates.count(), 3, "complete-fixed-template-registry");
      for (let i = 0; i < 3; i++) {
        await fields(
          templates.nth(i),
          {
            "Template ID": snapshot.templates[i].templateId,
            Kind: snapshot.templates[i].kind,
          },
          "fixed-template-fields",
        );
        equal(
          await templates.nth(i).locator("pre > code").textContent(),
          snapshot.templates[i].text,
          "exact-original-template-text",
        );
      }
      const historical = page.locator("#historical");
      require((await historical.locator(":scope > p").textContent()).includes(
        "earlier guidance projection",
      ), "upstream-boundary-clearly-historical");
      await fields(
        historical,
        {
          "Guidance schema": snapshot.guidance.schemaVersion,
          "Guidance status": snapshot.guidance.status,
        },
        "historical-schema-status",
      );
      const historicalDetails = historical.locator(":scope > details");
      equal(
        await historicalDetails
          .nth(0)
          .locator("ul")
          .nth(0)
          .locator("li")
          .allTextContents(),
        snapshot.guidance.warnings,
        "complete-historical-guidance-warnings",
      );
      equal(
        await historicalDetails
          .nth(0)
          .locator("ul")
          .nth(1)
          .locator("li")
          .allTextContents(),
        snapshot.guidance.unavailableSections,
        "complete-historical-unavailable-sections",
      );
      equal(
        await historicalDetails.count(),
        snapshot.guidance.findings.length + 1,
        "complete-historical-findings",
      );
      for (let i = 0; i < snapshot.guidance.findings.length; i++) {
        const finding = snapshot.guidance.findings[i],
          details = historicalDetails.nth(i + 1);
        equal(
          await details.locator(":scope > summary").textContent(),
          domText(
            finding.presentationTitle +
              " — finding state: " +
              finding.currentState,
          ),
          "separate-presentation-and-current-state",
        );
        // Wrapper's first child is summary. Verify only its direct full finding DL.
        const wrapper = details.locator(":scope > dl");
        const terms = await wrapper.locator(":scope > dt").allTextContents();
        equal(
          [...terms].sort(),
          Object.keys(finding).sort(),
          "complete-finding-properties",
        );
        for (let j = 0; j < terms.length; j++)
          await verifyTree(
            wrapper.locator(":scope > dd").nth(j),
            finding[terms[j]],
            "finding-full-field",
          );
      }
      const sourceDetails = page.locator("#provenance > details").first();
      const source = sourceDetails.locator(":scope > dl"),
        sourceTerms = await source.locator(":scope > dt").allTextContents();
      equal(
        [...sourceTerms].sort(),
        Object.keys(snapshot.guidance.source).sort(),
        "complete-source-binding-properties",
      );
      for (let i = 0; i < sourceTerms.length; i++)
        await verifyTree(
          source.locator(":scope > dd").nth(i),
          snapshot.guidance.source[sourceTerms[i]],
          "source-complete-field",
        );
      await fields(
        page.locator("#provenance > details").last(),
        {
          "Preview schema": snapshot.schemaVersion,
          "Template version": snapshot.templateVersion,
          "Template digest": snapshot.templateDigest,
          "Guidance digest": expected.guidanceDigest,
          "Package digest": expected.packageDigest,
        },
        "complete-frozen-version-and-digests",
      );
      if (name === "hostile.html") {
        const original = snapshot.guidance.findings[0].originalTitle;
        require(original.includes("\0") &&
          original.includes("\r\n") &&
          original.includes("中文") &&
          original.includes(
            "😀",
          ), "independent-hostile-control-and-Unicode-source");
        const leaves = await historical
          .locator("dd")
          .evaluateAll((nodes) =>
            nodes
              .filter((node) => node.childElementCount === 0)
              .map((node) => node.textContent),
          );
        require(leaves.includes(
          domText(original),
        ), "visible-hostile-original-exact-reference-normalization");
        require(leaves.some(
          (value) =>
            value.includes("\uFFFDNUL sentinel") &&
            value.includes("\r\nCRLF sentinel") &&
            value.includes("\rCR sentinel"),
        ), "explicit-numeric-reference-NUL-replacement-and-CR-retention");
      }
      const bounds = await page.evaluate(() => ({
        viewport: innerWidth,
        document: document.documentElement.scrollWidth,
        body: document.body.scrollWidth,
      }));
      require(bounds.document <= bounds.viewport + 1 &&
        bounds.body <= bounds.viewport + 1, "no-horizontal-viewport-overflow");
      // Test-only evaluation injects axe into the JS context, never a product
      // script node/resource. Inspect actual product safety before and after.
      await page.evaluate(axeSource);
      const audit = await page.evaluate(async () => {
        const result = await globalThis.axe.run(document, {
          runOnly: {
            type: "tag",
            values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
          },
        });
        const safe = (entries) =>
          entries.map((value) => ({
            id: value.id,
            impact: value.impact,
            nodeCount: value.nodes.length,
          }));
        return {
          violations: safe(result.violations),
          incomplete: safe(result.incomplete),
          passes: result.passes.length,
        };
      });
      equal(audit.violations, [], "axe-open-disclosure-WCAG-no-violations");
      accessibility.push({ viewport: viewport.name, fixture: name, ...audit });
      equal(
        await page
          .locator(
            "script,form,iframe,object,embed,svg,img,video,audio,link,input,button",
          )
          .count(),
        0,
        "no-product-active-nodes-after-test-only-axe",
      );
      equal(
        await page.locator("style").count(),
        1,
        "fixed-style-count-after-axe",
      );
      equal(
        await page
          .locator('meta[http-equiv="Content-Security-Policy"]')
          .getAttribute("content"),
        "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'",
        "unchanged-product-CSP-after-axe",
      );
      equal(
        await page.evaluate(() => globalThis.v11Injected),
        undefined,
        "hostile-script-remains-inert-after-axe",
      );
      equal(
        requests.length - before,
        1,
        "one-owned-document-only-per-navigation",
      );
      const focused = page.locator("header .warning");
      await focused.scrollIntoViewIfNeeded();
      if (name === "normal.html") {
        const viewportPath = join(output, viewport.name + "-viewport.png");
        await page.screenshot({ path: viewportPath });
        const fullPath = join(output, viewport.name + "-preview.png");
        await page.screenshot({ path: fullPath, fullPage: true });
        screenshots.push({ viewport: viewport.name, viewportPath, fullPath });
      }
    }
    await context.close();
    groups.push("V11-BROWSER-" + viewport.name);
  }
  equal(blocked.length, 0, "zero-unexpected-browser-request-attempts");
  equal(denied.length, 0, "zero-unexpected-host-request-attempts");
  equal(errors, [], "zero-page-errors");
  equal(dialogs, [], "zero-dialogs");
  require(requests.every(
    (request) =>
      request.method === "GET" && request.type === "document" && request.owned,
  ), "all-browser-requests-owned-GET-documents");
  equal(served.length, 9, "exact-three-fixtures-three-viewports");
  const report = {
    schemaVersion: "v11-browser-execution-v1",
    result: "PASS",
    checks,
    groups,
    browserVersion,
    nodeVersion: process.version,
    playwrightVersion: playwrightPackage.version,
    harnessSha256: sha(await readFile(script)),
    inputManifestSha256: sha(manifestBytes),
    axeVersion: axePackage.version,
    axeSourceSha256: sha(axeSource),
    accessibility,
    environmentStrategy:
      "Self-reexec plus enforced post-entry OS-path environment allowlist before fixture/server/Playwright load; inherited marker cannot bypass removal; browser uses same whitelist; no application/PG/provider setting forwarded",
    forwardedEnvironmentNames: Object.keys(cleanEnvironment).sort(),
    removedEnvironmentCount,
    servedCount: served.length,
    blockedCount: blocked.length,
    deniedCount: denied.length,
    normalization:
      "HtmlEncoder numeric references: NUL becomes U+FFFD; encoded CR and CRLF retained",
    screenshots,
    limitations: [
      "Deployed ADR sandbox/customer authorization NOT VERIFIED",
      "Supported Windows/manual screenreader/contrast/full accessibility NOT VERIFIED",
      "Task/CSV/review/persistence/publication/execution absent",
    ],
  };
  await writeFile(
    join(output, "execution.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
  console.log(
    `PASS V11 actual fixture browser; checks=${checks}; served=${served.length}; blocked=${blocked.length}; screenshots=${screenshots.length * 2}`,
  );
} catch (error) {
  await writeFile(
    join(output, "execution.json"),
    JSON.stringify(
      {
        schemaVersion: "v11-browser-execution-v1",
        result: "FAIL",
        checks,
        lastCode,
        exception: error.name,
        systemCode: /^[A-Z_]+$/.test(error.code ?? "") ? error.code : null,
        blockedCount: blocked.length,
        deniedCount: denied.length,
      },
      null,
      2,
    ) + "\n",
  );
  console.error(
    `FAIL V11 browser; code=${lastCode}; checks=${checks}; exception=${error.name}`,
  );
  process.exitCode = 1;
} finally {
  try {
    if (browser) await browser.close();
  } finally {
    if (server.listening)
      await new Promise((resolve, reject) =>
        server.close((error) => (error ? reject(error) : resolve())),
      );
  }
}
