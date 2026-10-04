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
const definitionPaths = [
  ...(await files(
    path.join(repo, "tests/integration/ReportDrafts.Tests"),
    (p) => !p.endsWith("execution.log"),
  )),
  ...(await files(own, (p) => p.endsWith(".mjs") || p.endsWith("README.md"))),
];
const integrationPaths = [];
for (const module of [
  "ReportDrafts",
  "AssessmentRuns",
  "FindingReview",
  "AssessmentMaturity",
  "AssessmentScoring",
  "DeterministicAnalysis",
])
  integrationPaths.push(
    ...(await files(
      path.join(repo, "src/server/modules", module),
      (p) => p.endsWith(".cs") || p.endsWith(".csproj"),
    )),
  );
const rootPaths = [];
for (const module of [
  "ReportDrafts",
  "AssessmentRuns",
  "FindingReview",
  "AssessmentMaturity",
  "AssessmentScoring",
  "DeterministicAnalysis",
])
  rootPaths.push(
    ...(await files(
      path.join(root, "src/server/modules", module),
      (p) => p.endsWith(".cs") || p.endsWith(".csproj"),
    )),
  );
rootPaths.push(
  ...(await files(
    path.join(root, "src/server/hosts/LocalConsultantDemo"),
    (p) => p.endsWith(".cs") || p.endsWith(".csproj"),
  )),
);
for (const p of [
  "src/web/src/AnalysisView.tsx",
  "src/web/src/DraftReportView.tsx",
  "src/web/src/MaturityView.tsx",
  "src/web/src/ReviewPanel.tsx",
  "src/web/src/api.ts",
  "src/web/src/styles.css",
  "src/web/src/demo-contract.generated.ts",
  "contracts/local-demo/demo-v1.schema.json",
])
  rootPaths.push(path.join(root, p));
rootPaths.push(...(await files(path.join(root, "src/web/dist"), () => true)));
const bindings = async (paths, base) =>
  Promise.all(
    paths.map(async (p) => ({
      path: path.relative(base, p),
      sha256: await digest(p),
    })),
  );
const execution = JSON.parse(
  await readFile(path.join(own, "execution.json"), "utf8"),
);
if (execution.status !== "PASS")
  throw new Error("Bind only final executed PASS evidence");
const evidencePaths = [
  "execution.json",
  "execution.log",
  "desktop-draft.png",
  "desktop-viewport.png",
  "mobile-draft.png",
  "reflow-draft.png",
  "reflow-viewport.png",
].map((p) => path.join(own, p));
const historical = [
  "tests/e2e/consultant-demo/artifact-digests.json",
  "tests/e2e/analysis-scoring/execution.json",
  "tests/e2e/review-maturity/artifacts.json",
  "tests/integration/ReviewMaturity.Tests/HistoricalGoldens.cs",
].map((p) => path.join(repo, p));
const value = {
  version: "draft-report-independent-evidence-v1",
  scope:
    "Synthetic unpublished read-only draft, no durable ReportVersion or production acceptance",
  definitionBinding: await bindings(definitionPaths, repo),
  rootSourceBinding: await bindings(rootPaths, root),
  integrationDependencyBinding: await bindings(integrationPaths, repo),
  integrationExecutionBinding: [
    {
      path: "tests/integration/ReportDrafts.Tests/execution.log",
      sha256: await digest(
        path.join(repo, "tests/integration/ReportDrafts.Tests/execution.log"),
      ),
    },
  ],
  hostBinding: [
    {
      path: "LocalConsultantDemo.dll",
      sha256: await digest(process.env.IGA_HOST_DLL),
    },
  ],
  executedEvidenceBinding: await bindings(evidencePaths, own),
  preservedHistoricalBinding: await bindings(historical, repo),
  dependencyVersions: {
    playwright: "1.62.1",
    axe: "4.13.0",
    postgres: "18.4",
    dotnetSdk: "10.0.401",
  },
  coreCommit: "343765a28a3227f42ed11240661f6d04be9bc16f",
  markdownCommit: "c7d8d3e5e8ac50e49d51d0b3857face1b3063ed9",
  execution,
};
await writeFile(
  path.join(own, "artifacts.json"),
  JSON.stringify(value, null, 2) + "\n",
);
console.log(
  "Bound independent complete definitions, executed host/assets/schema and preserved historical manifests.",
);
