import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "../consultant-demo/node_modules/playwright/index.mjs";

const root = fileURLToPath(new URL("../../../", import.meta.url));
const directory = await mkdtemp(resolve(tmpdir(), "iga-auth-navigation-"));
const compiler = spawn(
  process.execPath,
  [
    resolve(root, "src/web/node_modules/typescript/bin/tsc"),
    "--ignoreConfig",
    "--target",
    "es2022",
    "--module",
    "esnext",
    "--lib",
    "es2022,dom",
    "--outDir",
    directory,
    resolve(root, "src/web/src/bff-authentication-navigation.ts"),
  ],
  { cwd: root, stdio: "inherit" },
);
assert.equal(
  (await once(compiler, "exit"))[0],
  0,
  "compile exact repository helper",
);
const dotnet = process.env.IGA_DOTNET ?? "dotnet";
const fixture = spawn(
  dotnet,
  [
    resolve(
      root,
      "tests/integration/BffAuthenticationTransport.Tests/bin/Release/net10.0/BffAuthenticationTransport.Tests.dll",
    ),
    "--browser-fixture",
  ],
  {
    cwd: root,
    env: {
      ...process.env,
      IGA_BFF_AUTH_NAVIGATION_HELPER: resolve(
        directory,
        "bff-authentication-navigation.js",
      ),
    },
    stdio: ["ignore", "pipe", "pipe"],
  },
);
let browser;
try {
  const origin = await new Promise((resolveOrigin, reject) => {
    let output = "";
    const timer = setTimeout(
      () => reject(new Error("Synthetic HTTPS fixture startup timed out.")),
      30000,
    );
    fixture.stdout.on("data", (chunk) => {
      output += chunk.toString();
      const match = output.match(
        /BFF-PUBLIC-BROWSER-FIXTURE (https:\/\/127\.0\.0\.1:\d+)/,
      );
      if (match) {
        clearTimeout(timer);
        resolveOrigin(match[1]);
      }
    });
    fixture.once("exit", () => {
      clearTimeout(timer);
      reject(new Error("Synthetic HTTPS fixture exited before ready."));
    });
  });
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const signIns = [];
  let providerRequests = 0;
  await context.route("**/*", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.origin === origin) {
      if (url.pathname === "/bff/v1/sign-in") signIns.push(request);
      return route.continue();
    }
    assert.equal(
      request.url(),
      "https://provider.invalid/authorize",
      "no real provider/network request",
    );
    assert.equal(request.isNavigationRequest(), true);
    providerRequests++;
    await route.fulfill({
      status: 200,
      contentType: "text/html",
      body: "<!doctype html><title>Intercepted synthetic provider navigation</title>",
    });
  });
  const page = await context.newPage();
  await page.goto(origin + "/fixture");
  await Promise.all([
    page.waitForURL("https://provider.invalid/authorize"),
    page.locator("#sign-in").click(),
  ]);
  assert.equal(signIns.length, 1);
  const request = signIns[0];
  assert.equal(
    request.isNavigationRequest(),
    true,
    "sign-in uses top-level native navigation",
  );
  assert.equal(
    request.resourceType(),
    "document",
    "provider redirect is not fetch",
  );
  assert.equal(request.method(), "POST");
  const headers = await request.allHeaders();
  assert.equal(headers.origin, origin);
  assert.match(headers["content-type"], /^application\/x-www-form-urlencoded/);
  const form = new URLSearchParams(request.postData());
  assert.deepEqual([...form.keys()], ["__RequestVerificationToken"]);
  assert.ok(form.get("__RequestVerificationToken").length > 0);
  assert.equal(providerRequests, 1);
  const cookies = await context.cookies(origin + "/bff/v1/session");
  assert.ok(
    cookies.some(
      (cookie) =>
        cookie.name === "__Secure-IgaBffCsrf" &&
        cookie.httpOnly &&
        cookie.secure &&
        cookie.path === "/bff",
    ),
  );
  await context.close();
  console.log(
    "PASS exact repository helper: real Chromium HTTPS session CSRF -> native form POST -> intercepted synthetic provider document navigation; no real provider activity.",
  );
} finally {
  if (browser) await browser.close();
  if (fixture.exitCode === null) {
    const exited = once(fixture, "exit");
    fixture.kill("SIGTERM");
    await exited;
  }
  await rm(directory, { recursive: true, force: true });
}
