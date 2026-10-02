import { createHash } from "node:crypto";
import { readdir, readFile, writeFile } from "node:fs/promises";
import { dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packet = dirname(fileURLToPath(import.meta.url));
const repository = resolve(packet, "../../..");
const root = resolve(process.env.IGA_ARTIFACT_ROOT ?? repository);
const hash = async (path) =>
  createHash("sha256")
    .update(await readFile(path))
    .digest("hex");
async function files(directory, recursive = false) {
  const output = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isFile()) output.push(path);
    else if (recursive && entry.isDirectory())
      output.push(...(await files(path, true)));
  }
  return output;
}
async function binding(paths, base) {
  return Promise.all(
    [...new Set(paths)].sort().map(async (path) => ({
      path: relative(base, path),
      sha256: await hash(path),
    })),
  );
}
const host = resolve(root, "src/server/hosts/LocalConsultantDemo");
const binaries = resolve(host, "bin/Release/net10.0");
const artifactFiles = [
  ...(await files(host)).filter((path) => path.endsWith(".cs")),
  ...(await files(binaries)).filter(
    (path) => path.endsWith(".dll") || path.endsWith(".json"),
  ),
  ...(await files(resolve(root, "src/web/dist"), true)),
  ...["demo-v1.schema.json", "README.md"].map((name) =>
    resolve(root, "contracts/local-demo", name),
  ),
  ...[
    "AnalysisView.tsx",
    "ReviewPanel.tsx",
    "MaturityView.tsx",
    "App.tsx",
    "styles.css",
    "api.ts",
  ].map((name) => resolve(root, "src/web/src", name)),
  ...["package-lock.json", "package.json"].map((name) =>
    resolve(root, "tests/e2e/consultant-demo", name),
  ),
];
const execution = JSON.parse(
  await readFile(resolve(packet, "execution.json"), "utf8"),
);
const localFiles = await files(packet);
const data = {
  recordedAtUtc: new Date().toISOString(),
  execution,
  authoritativeArtifactSha256: await binding(artifactFiles, root),
  testDefinitionSha256: await binding(
    localFiles.filter((path) => path.endsWith(".mjs") || path.endsWith(".md")),
    repository,
  ),
  screenshotSha256: await binding(
    localFiles.filter((path) => path.endsWith(".png")),
    packet,
  ),
  executionSha256: await hash(resolve(packet, "execution.json")),
  method:
    "Complete suite against final primary assemblies, production assets and strict private schema; complete definitions include exact fault injection and legal-whitespace-padded valid transport commands, not summary pseudo-inputs",
  cleanup:
    "Own browser/API contexts closed and own host stopped; no PostgreSQL restart/drop/customer data",
  sourceReview:
    "Independent M5/R5/U5/bridge reviews closed; focused test-oracle findings corrected before final execution",
  runtime: {
    dotnetSdk: "10.0.401",
    postgres: "18.4",
    node: execution.node,
    playwright: "1.62.1",
    axe: "4.13.0",
    chromium: execution.browser,
  },
};
await writeFile(
  resolve(packet, "artifacts.json"),
  `${JSON.stringify(data, null, 2)}\n`,
);
console.log(
  `Bound ${data.authoritativeArtifactSha256.length} final artifacts and ${data.testDefinitionSha256.length} complete test definitions.`,
);
