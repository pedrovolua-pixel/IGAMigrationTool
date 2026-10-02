import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { until } from "./browser-support.mjs";

export const base = process.env.IGA_DEMO_BASE_URL ?? "http://127.0.0.1:5183";
assert.equal(base, "http://127.0.0.1:5183");
let child;
export const ownsHost = () => !!child;
export async function launchHost() {
  if (!process.env.IGA_HOST_DLL || !process.env.IGA_HOST_WORKING_DIRECTORY)
    return;
  const connection = process.env.IGA_SYNTHETIC_DATABASE ?? "";
  assert.match(
    connection,
    /(?:^|;)Database=iga_synthetic_v7(?:;|$)/i,
    "Owned guidance host requires dedicated disposable v7 database",
  );
  assert.match(
    connection,
    /(?:^|;)Host=127\.0\.0\.1(?:;|$)/i,
    "Only loopback synthetic database allowed",
  );
  let occupied = false;
  try {
    await fetch(`${base}/local-demo/v1/catalog`);
    occupied = true;
  } catch {}
  assert.equal(
    occupied,
    false,
    "Host port must be free; do not reuse another process",
  );
  child = spawn(
    process.env.IGA_DOTNET ?? "dotnet",
    [process.env.IGA_HOST_DLL, "--synthetic-local-demo"],
    {
      cwd: process.env.IGA_HOST_WORKING_DIRECTORY,
      env: { ...process.env },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  // Diagnostic host output is never echoed into evidence; no environment/secret logging.
  child.stdout.on("data", () => {});
  child.stderr.on("data", () => {});
  await until(
    async () => {
      if (child.exitCode !== null)
        throw new Error(`Owned synthetic host exited:${child.exitCode}`);
      try {
        return (await fetch(`${base}/local-demo/v1/catalog`)).status;
      } catch {
        return 0;
      }
    },
    (status) => status === 200,
    "Synthetic guidance host did not start",
  );
}
export async function stopHost() {
  if (!child || child.exitCode !== null) return;
  const stopped = once(child, "exit");
  child.kill("SIGTERM");
  let timer;
  await Promise.race([
    stopped,
    new Promise((resolve) => {
      timer = setTimeout(() => {
        child.kill("SIGKILL");
        resolve();
      }, 10000);
    }),
  ]);
  clearTimeout(timer);
  if (child.exitCode === null) await stopped;
}
process.once("exit", () => {
  if (child?.exitCode === null) child.kill("SIGTERM");
});
