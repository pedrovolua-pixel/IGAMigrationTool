import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
const own = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(own, "../../..");
const root = process.env.IGA_HOST_WORKING_DIRECTORY;
if (!root || !process.env.IGA_HOST_DLL)
  throw new Error("Actual executed host root and DLL required");
const digest = async (p) =>
  createHash("sha256")
    .update(await readFile(p))
    .digest("hex");
async function files(dir, predicate) {
  const result = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    if (["bin", "obj", "node_modules"].includes(entry.name)) continue;
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) result.push(...(await files(p, predicate)));
    else if (predicate(p)) result.push(p);
  }
  return result.sort();
}
const bindings = (paths, base) =>
  Promise.all(
    paths.map(async (p) => ({
      path: path.relative(base, p),
      sha256: await digest(p),
    })),
  );
const definitions = [
  ...(await files(
    path.join(repo, "tests/integration/RecommendationGuidance.Tests"),
    (p) => !p.endsWith("execution.log") && !p.endsWith("verification.log"),
  )),
  ...(await files(own, (p) => p.endsWith(".mjs") || p.endsWith("README.md"))),
];
const linked = [
  "tests/integration/ReportDrafts.Tests/CoreFixture.cs",
  "tests/integration/ReportDrafts.Tests/SavedDraftFixture.cs",
  "tests/integration/ReportDrafts.Tests/canonical-golden.json",
  "tests/integration/ReviewMaturity.Tests/HistoricalGoldens.cs",
].map((p) => path.join(repo, p));
const source = [];
for (const module of [
  "RecommendationGuidance",
  "ReportDrafts",
  "AssessmentRuns",
  "AssessmentOrchestration",
  "AssessmentCoverage",
  "FindingReview",
  "AssessmentMaturity",
  "AssessmentScoring",
  "DeterministicAnalysis",
])
  source.push(
    ...(await files(
      path.join(root, "src/server/modules", module),
      (p) =>
        p.endsWith(".cs") ||
        p.endsWith(".csproj") ||
        p.endsWith("packages.lock.json"),
    )),
  );
source.push(
  ...(await files(
    path.join(root, "src/server/hosts/LocalConsultantDemo"),
    (p) =>
      p.endsWith(".cs") ||
      p.endsWith(".csproj") ||
      p.endsWith("packages.lock.json"),
  )),
);
source.push(
  ...[
    "Directory.Build.props",
    "global.json",
    "src/web/src/AnalysisView.tsx",
    "src/web/src/RecommendationGuidanceView.tsx",
    "src/web/src/RecommendationGuidanceView.css",
    "src/web/src/DraftReportView.tsx",
    "src/web/src/MaturityView.tsx",
    "src/web/src/ReviewPanel.tsx",
    "src/web/src/api.ts",
    "src/web/src/App.tsx",
    "src/web/src/main.tsx",
    "src/web/src/styles.css",
    "src/web/src/demo-contract.generated.ts",
    "src/web/package.json",
    "src/web/package-lock.json",
    "contracts/local-demo/demo-v1.schema.json",
  ].map((p) => path.join(root, p)),
);
source.push(...(await files(path.join(root, "src/web/dist"), () => true)));
const borrowed = await bindings(source, root);
const comparison = [];
if (process.env.IGA_COORDINATOR_WORKING_DIRECTORY) {
  for (const b of borrowed) {
    const actual = await digest(
      path.join(process.env.IGA_COORDINATOR_WORKING_DIRECTORY, b.path),
    );
    comparison.push({
      path: b.path,
      executedSha256: b.sha256,
      coordinatorSha256: actual,
      matches: actual === b.sha256,
    });
  }
  if (comparison.some((b) => !b.matches))
    throw new Error(
      "Executed borrowed source no longer matches final coordinator source",
    );
}
const execution = JSON.parse(
  await readFile(path.join(own, "execution.json"), "utf8"),
);
if (execution.status !== "PASS")
  throw new Error("Bind only final executed PASS evidence");
const evidence = [
  "execution.json",
  "execution.log",
  "desktop-guidance.png",
  "desktop-viewport.png",
  "mobile-guidance.png",
  "reflow-guidance.png",
  "reflow-viewport.png",
].map((p) => path.join(own, p));
const historical = [
  "tests/e2e/consultant-demo/artifact-digests.json",
  "tests/e2e/analysis-scoring/execution.json",
  "tests/e2e/review-maturity/artifacts.json",
  "tests/e2e/draft-reports/artifacts.json",
].map((p) => path.join(repo, p));
const hostDir = path.dirname(process.env.IGA_HOST_DLL),
  testDir = path.join(
    repo,
    "tests/integration/RecommendationGuidance.Tests/bin/Release/net10.0",
  );
async function binaries(dir) {
  return (await readdir(dir))
    .filter(
      (n) =>
        n.endsWith(".dll") ||
        n.endsWith(".deps.json") ||
        n.endsWith(".runtimeconfig.json"),
    )
    .map((n) => path.join(dir, n));
}
const value = {
  version: "guidance-independent-evidence-v1",
  scope:
    "Current detached synthetic unverified read-only guidance; no recommendation review/approval/task/export/execution or full pilot acceptance",
  definitionBinding: await bindings(definitions, repo),
  linkedIndependentFixtureBinding: await bindings(linked, repo),
  executedBorrowedSourceBinding: borrowed,
  coordinatorSourceComparison: comparison,
  hostBinaryBinding: await bindings(await binaries(hostDir), hostDir),
  integrationBinaryBinding: await bindings(await binaries(testDir), testDir),
  integrationExecutionBinding: await bindings(
    ["execution.log", "verification.log"].map((p) =>
      path.join(repo, "tests/integration/RecommendationGuidance.Tests", p),
    ),
    repo,
  ),
  executedEvidenceBinding: await bindings(evidence, own),
  preservedHistoricalBinding: await bindings(historical, repo),
  dependencyVersions: {
    playwright: "1.62.1",
    axe: "4.13.0",
    postgres: "18.4 (Homebrew)",
    dotnetSdk: "10.0.401",
    node: "24.21.0",
    npm: "11.20.0",
    prettier: "3.9.9",
  },
  guidanceCommit: "720d1dbd8018ebcdf037a05aecaba7d0e293c853",
  uiCommit: "4f42ed6cff89b705072017034852fce6f0116380",
  execution,
};
await writeFile(
  path.join(own, "artifacts.json"),
  JSON.stringify(value, null, 2) + "\n",
);
console.log(
  `Bound complete independent definitions, ${borrowed.length} executed borrowed source files, host/test binaries and preserved historical evidence; ${comparison.filter((b) => !b.matches).length} source mismatches.`,
);
