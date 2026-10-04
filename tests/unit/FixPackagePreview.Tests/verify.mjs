import { readFile, writeFile, mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import assert from "node:assert/strict";

const directory = dirname(fileURLToPath(import.meta.url)),
  root = resolve(directory, "../../.."),
  frontend = resolve(root, "src/web");
const require = createRequire(resolve(frontend, "package.json"));
const React = require("react"),
  { renderToStaticMarkup } = require("react-dom/server");
const { transformWithOxc } = await import(
  pathToFileURL(resolve(frontend, "node_modules/vite/dist/node/index.js")).href
);
const sourcePath = resolve(frontend, "src/FixPackagePreview.tsx");
const result = await transformWithOxc(
  await readFile(sourcePath, "utf8"),
  sourcePath,
  { lang: "tsx", jsx: { runtime: "automatic" }, target: "es2022" },
);
const compiled = result.code
  .replace(/import ['"]\.\/FixPackagePreview\.css['"];?\n/g, "")
  .replaceAll(
    '"react/jsx-runtime"',
    JSON.stringify(pathToFileURL(require.resolve("react/jsx-runtime")).href),
  );
const artifacts = resolve(directory, "artifacts");
await mkdir(artifacts, { recursive: true });
await writeFile(resolve(artifacts, "component.mjs"), compiled);
const { FixPackagePreview, coherentFixPackages } = await import(
  pathToFileURL(resolve(artifacts, "component.mjs")).href
);
const complete = JSON.parse(
  await readFile(resolve(directory, "complete-fixture.json"), "utf8"),
);
const empty = JSON.parse(
  await readFile(resolve(directory, "empty-fixture.json"), "utf8"),
);
const oracle = JSON.parse(
  await readFile(resolve(directory, "oracle-hashes.json"), "utf8"),
);
const clone = () => structuredClone(complete),
  hash = (s) => createHash("sha256").update(s).digest("hex");
// Independent Node serializer for adversarial rebinding. JSON.stringify supplies
// basic string escaping; a token regex maps remaining framework-default escapes.
function canonical(v) {
  if (typeof v === "string")
    return JSON.stringify(v).replace(
      /\\\\|\\"|\\u([a-fA-F0-9]{4})|[<>&'+`\u007f-\uffff]/g,
      (token, hex) => {
        if (token === "\\\\") return token;
        if (token === '\\"') return "\\u0022";
        if (hex) return "\\u" + hex.toUpperCase();
        return (
          "\\u" +
          token.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0")
        );
      },
    );
  if (v === null || typeof v === "number" || typeof v === "boolean")
    return JSON.stringify(v);
  if (Array.isArray(v)) return "[" + v.map(canonical).join(",") + "]";
  return (
    "{" +
    Object.keys(v)
      .sort()
      .map((k) => canonical(k) + ":" + canonical(v[k]))
      .join(",") +
    "}"
  );
}
function refresh(f, updateGuidance = true) {
  const s = f.analysis.fixPackages.snapshot;
  if (updateGuidance) {
    const { contentDigest, ...payload } = s.guidance;
    s.guidance.contentDigest = hash(canonical(payload));
    f.analysis.recommendationGuidance.snapshot = structuredClone(s.guidance);
  }
  const { canonicalJson, contentDigest, ...payload } = s;
  s.canonicalJson = canonical(payload);
  s.contentDigest = hash(s.canonicalJson);
}
let checks = 0,
  accepted = 0,
  denied = 0;
const check = (name, condition) => {
  assert.ok(condition, name);
  checks++;
};
const render = (f) =>
  renderToStaticMarkup(
    React.createElement(FixPackagePreview, {
      preview: f.analysis.fixPackages,
      run: f.run,
      analysis: f.analysis,
    }),
  );
async function accept(name, f) {
  check(name, await coherentFixPackages(f.analysis, f.run));
  accepted++;
}
async function reject(name, mutate, refreshCache = false) {
  const f = clone();
  mutate(f);
  if (refreshCache) refresh(f);
  check(name, !(await coherentFixPackages(f.analysis, f.run)));
  denied++;
  return f;
}
for (const [name, f] of [
  ["complete", complete],
  ["empty", empty],
]) {
  await accept("independent " + name + " fixture", f);
  const html = render(f),
    golden = await readFile(
      resolve(directory, name + "-component.html"),
      "utf8",
    );
  check(name + " full independent markup bytes", html === golden);
  check(
    name + " full independent markup SHA256",
    hash(html) === oracle[name].html,
  );
  check(
    name + " independent canonical serializer",
    canonical(
      Object.fromEntries(
        Object.entries(f.analysis.fixPackages.snapshot).filter(
          ([k]) => !["contentDigest", "canonicalJson"].includes(k),
        ),
      ),
    ) === f.analysis.fixPackages.snapshot.canonicalJson,
  );
  check(
    name + " independent source digests",
    f.analysis.fixPackages.snapshot.contentDigest === oracle[name].package &&
      f.analysis.fixPackages.snapshot.guidance.contentDigest ===
        oracle[name].guidance,
  );
  check(name + " repeat deterministic", render(f) === golden);
}
const actual = render(complete);
await writeFile(resolve(artifacts, "actual-complete.html"), actual);
check(
  "empty explicit no healthy inference",
  render(empty).includes(
    "Empty input does not establish healthy coverage or validated remediation.",
  ),
);
check(
  "current and original distinction",
  actual.includes("<h4>Current finding and immutable original</h4>") &&
    actual.includes("Original &lt;script&gt;never()&lt;/script&gt;") &&
    actual.includes("Current café Ω 中文 😀"),
);
check(
  "React hostile text encoding",
  actual.includes("Review &lt;img src=x onerror=alert(1)&gt; inert text."),
);
check(
  "code inert escaped quotes",
  actual.includes("Write-Output &#x27;Fixture review required&#x27;"),
);
for (const forbidden of [
  "<script",
  "<img",
  "<svg",
  "<form",
  "<iframe",
  "<button",
  "<a ",
  "<input",
  "<link",
  " onclick=",
  "dangerouslySetInnerHTML",
])
  check("no active element " + forbidden, !actual.includes(forbidden));
for (const label of [
  "Package ID",
  "Finding ID",
  "Current title",
  "Current finding state",
  "Finding revision",
  "Business context",
  "Original title",
  "Original finding state",
  "Rule ID",
  "Rule version",
  "Category",
  "Severity",
  "Root cause",
  "Occurrence ID",
  "Object ID",
  "Object type",
  "Module",
  "Original digest",
  "Evidence reference",
  "Scoped option ID",
  "Original option ID",
  "Recommendation status",
  "Original option guidance",
  "Prerequisites",
  "Risk",
  "Recovery guidance",
  "Artifact ID",
  "Template ID",
  "Artifact kind",
  "Artifact status",
  "Template version",
  "Template catalog digest",
  "Preview schema",
  "Preview status",
  "Package content digest",
  "Guidance content digest",
  "Detail schema",
  "Detail status",
  "Run ID",
  "Run revision",
  "Complete frozen input digest",
  "Baseline",
  "Profile",
])
  check("readable field " + label, actual.includes("<dt>" + label + "</dt>"));
for (const heading of [
  "Validation guidance",
  "Guidance references",
  "Assumptions",
  "Finding limitations",
  "Current warnings",
  "Current unavailable capabilities",
  "Historical guidance warnings",
  "Historical upstream unavailable capabilities",
])
  check(
    "complete contextual section " + heading,
    actual.includes(">" + heading + "</h"),
  );
const sourceFields = [];
function collect(v, p = "") {
  if (Array.isArray(v)) v.forEach((x, i) => collect(x, `${p}[${i}]`));
  else if (v !== null && typeof v === "object")
    Object.entries(v).forEach(([k, x]) => collect(x, p ? `${p}.${k}` : k));
  else sourceFields.push(p);
}
collect(complete.analysis.fixPackages.snapshot.guidance.source);
for (const field of sourceFields)
  check(
    "complete source primitive " + field,
    actual.includes("<dt>" + field + "</dt>"),
  );
check(
  "fixed keyboard heading",
  actual.includes('id="fix-package-preview-heading" tabindex="-1"'),
);
check(
  "native disclosure",
  actual.includes('<details class="fix-package-group"><summary>') &&
    actual.includes('<details class="fix-package-source"><summary>'),
);
check(
  "all three kinds and all six artifacts",
  actual.split('class="fix-package-artifact"').length === 7 &&
    ["Configuration", "Script", "Sql"].every((k) =>
      actual.includes("<h5>" + k + " · Unverified</h5>"),
    ),
);
for (const reason of [
  "fix_packages_source_unavailable",
  "fix_packages_capture_mismatch",
  "fix_packages_integrity_denied",
]) {
  const f = clone();
  f.analysis.fixPackages.status = "Unavailable";
  f.analysis.fixPackages.reasonCode = reason;
  f.analysis.fixPackages.snapshot = null;
  await accept("typed unavailable " + reason, f);
  const html = render(f);
  check(
    "safe reason visible " + reason,
    html.includes("<code>" + reason + "</code>"),
  );
  check(
    "unavailable never partial",
    !html.includes('class="fix-package-artifact"') &&
      !html.includes("Current café"),
  );
}
for (const reason of [
  "",
  null,
  "other_reason",
  "<script>",
  "private customer evidence",
])
  await reject("invalid unavailable reason", (f) => {
    f.analysis.fixPackages.status = "Unavailable";
    f.analysis.fixPackages.reasonCode = reason;
    f.analysis.fixPackages.snapshot = null;
  });
for (const historicalProfile of [
  "profile-standard",
  "synthetic-analysis-equal-v1",
  "synthetic-review-maturity-equal-v1",
  "synthetic-review-maturity-operations-v1",
  "profile-ai-preview-v1",
]) {
  const f = clone();
  f.run.selection.profileId = historicalProfile;
  f.analysis.fixPackages = null;
  f.run.lockedInputs = f.run.lockedInputs.filter(
    (i) => i.name !== "Fictional fix-package templates",
  );
  await accept("historical null " + historicalProfile, f);
  check("historical component silent", render(f) === "");
  await reject("historical cannot return package " + historicalProfile, (g) => {
    g.run.selection.profileId = historicalProfile;
  });
}
await reject("new profile requires detail", (f) => {
  f.analysis.fixPackages = null;
});
await reject("historical undefined rejected", (f) => {
  f.run.selection.profileId = "profile-standard";
  delete f.analysis.fixPackages;
});
for (const field of Object.keys(complete.analysis.fixPackages)) {
  if (field === "snapshot") continue;
  await reject("detail member changed " + field, (f) => {
    f.analysis.fixPackages[field] =
      typeof f.analysis.fixPackages[field] === "number" ? 999 : "changed";
  });
  await reject("detail member missing " + field, (f) => {
    delete f.analysis.fixPackages[field];
  });
}
await reject("extra detail field", (f) => {
  f.analysis.fixPackages.execute = true;
});
for (const mutation of [
  (f) => (f.run.runId = "22222222-2222-4222-8222-222222222222"),
  (f) => f.run.revision++,
  (f) => (f.run.schemaVersion = 2),
  (f) => (f.run.demoOnly = false),
  (f) => (f.run.selection.scopeId = "other"),
  (f) => (f.run.selection.baselineId = "baseline-complete"),
  (f) => (f.run.state = "Running"),
  (f) => (f.run.cancelRequested = true),
  (f) => (f.run.progress.allTerminal = false),
  (f) => (f.run.progress.remainingUnits = 1),
  (f) => (f.run.progress.plannedUnits = 0),
  (f) => (f.run.progress.extra = 1),
  (f) => (f.run.coverageCompletionKind = null),
  (f) => (f.run.stateCounts[0].count = 2),
  (f) => (f.run.stateCounts[0].state = "Unknown"),
  (f) => (f.analysis.runId = "other"),
  (f) => f.analysis.runRevision++,
  (f) => (f.analysis.schemaVersion = 2),
  (f) => (f.analysis.demoOnly = false),
  (f) => (f.analysis.status = "Unavailable"),
  (f) => (f.analysis.reasonCode = "wrong"),
  (f) => (f.analysis.reviewSnapshotDigest = "0".repeat(64)),
  (f) => (f.analysis.review.snapshotDigest = "0".repeat(64)),
  (f) => (f.analysis.review.runId = "other"),
  (f) => f.analysis.review.runRevision++,
  (f) => f.analysis.review.findings[0].revision++,
  (f) => (f.analysis.review.findings[0].title = "stale"),
  (f) => (f.analysis.review.findings[0].businessContext = "stale"),
  (f) => (f.analysis.findings[0].originalTitle = "changed"),
  (f) => (f.analysis.findings[0].recommendations[0] = "changed"),
  (f) =>
    (f.analysis.recommendationGuidance.snapshot.contentDigest = "0".repeat(64)),
])
  await reject("run/current review/source response fence", mutation);
for (const lock of complete.run.lockedInputs) {
  for (const field of ["name", "version", "sha256"])
    await reject("selected lock " + lock.name + " " + field, (f) => {
      f.run.lockedInputs.find((i) => i.name === lock.name)[field] = "wrong";
    });
  await reject("selected lock missing " + lock.name, (f) => {
    f.run.lockedInputs = f.run.lockedInputs.filter((i) => i.name !== lock.name);
  });
}
await reject("duplicate lock", (f) => {
  f.run.lockedInputs.push(f.run.lockedInputs[0]);
});
await reject("foreign lock", (f) => {
  f.run.lockedInputs.push({
    name: "Execute",
    version: "v1",
    sha256: "0".repeat(64),
  });
});
for (const field of Object.keys(complete.analysis.fixPackages.snapshot)) {
  await reject("snapshot actual member missing " + field, (f) => {
    delete f.analysis.fixPackages.snapshot[field];
  });
  if (
    [
      "canonicalJson",
      "contentDigest",
      "guidance",
      "templates",
      "packages",
      "warnings",
      "unavailableSections",
    ].includes(field)
  )
    continue;
  await reject(
    "snapshot actual forged " + field,
    (f) => {
      f.analysis.fixPackages.snapshot[field] = "changed";
    },
    true,
  );
}
const groups = [
  ["guidance", complete.analysis.fixPackages.snapshot.guidance],
  ["source", complete.analysis.fixPackages.snapshot.guidance.source],
  [
    "versions",
    complete.analysis.fixPackages.snapshot.guidance.source.frozenVersions,
  ],
  [
    "capability",
    complete.analysis.fixPackages.snapshot.guidance.source.capabilityLock,
  ],
  [
    "analysisLock",
    complete.analysis.fixPackages.snapshot.guidance.source.analysisLock,
  ],
  ["finding", complete.analysis.fixPackages.snapshot.guidance.findings[0]],
  [
    "option",
    complete.analysis.fixPackages.snapshot.guidance.findings[0].options[0],
  ],
  [
    "occurrence",
    complete.analysis.fixPackages.snapshot.guidance.findings[0].occurrences[0],
  ],
  ["package", complete.analysis.fixPackages.snapshot.packages[0]],
  ["fixOption", complete.analysis.fixPackages.snapshot.packages[0].options[0]],
  [
    "artifact",
    complete.analysis.fixPackages.snapshot.packages[0].options[0].artifacts[0],
  ],
  ["template", complete.analysis.fixPackages.snapshot.templates[0]],
];
function group(f, name) {
  const s = f.analysis.fixPackages.snapshot;
  return {
    guidance: s.guidance,
    source: s.guidance.source,
    versions: s.guidance.source.frozenVersions,
    capability: s.guidance.source.capabilityLock,
    analysisLock: s.guidance.source.analysisLock,
    finding: s.guidance.findings[0],
    option: s.guidance.findings[0].options[0],
    occurrence: s.guidance.findings[0].occurrences[0],
    package: s.packages[0],
    fixOption: s.packages[0].options[0],
    artifact: s.packages[0].options[0].artifacts[0],
    template: s.templates[0],
  }[name];
}
for (const [name, value] of groups) {
  for (const key of Object.keys(value))
    await reject("closed " + name + " missing " + key, (f) => {
      delete group(f, name)[key];
    });
  await reject("closed " + name + " extra", (f) => {
    group(f, name).execute = true;
  });
}
for (const mutation of [
  (s) => s.guidance.warnings.reverse(),
  (s) => s.guidance.unavailableSections.pop(),
  (s) => (s.guidance.status = "Reviewed"),
  (s) => (s.guidance.source.profileId = "synthetic-review-maturity-equal-v1"),
  (s) =>
    (s.guidance.source.frozenVersions.applicationVersion =
      "synthetic-review-maturity-app-v1"),
  (s) => delete s.guidance.source.frozenVersions.fixPackageTemplateDigest,
  (s) =>
    (s.guidance.source.frozenVersions.fixPackageTemplateDigest = "0".repeat(
      64,
    )),
  (s) => (s.guidance.source.capabilityLock.modules[0].id = "other"),
  (s) => (s.guidance.source.capabilityLock.compatibilityLevel = 150),
  (s) =>
    (s.guidance.source.analysisLock.profileId =
      "synthetic-analysis-operations-v1"),
  (s) => (s.guidance.source.analysisLock.compatibility.sourceProduct = "real"),
  (s) => (s.guidance.findings[0].currentState = "Reviewed"),
  (s) => (s.guidance.findings[0].options[0].status = "Reviewed"),
  (s) => (s.guidance.findings[0].options[0].optionId = "foreign"),
  (s) => (s.guidance.findings[0].options[0].scopedOptionId = "0".repeat(64)),
  (s) => (s.guidance.findings[0].occurrences[0].occurrenceId = "0".repeat(64)),
  (s) => (s.packages[0].packageId = "0".repeat(64)),
  (s) => (s.packages[0].findingId = "0".repeat(64)),
  (s) => (s.packages[0].options[0].scopedOptionId = "0".repeat(64)),
  (s) => (s.packages[0].options[0].artifacts[0].artifactId = "0".repeat(64)),
  (s) => (s.packages[0].options[0].artifacts[0].status = "Reviewed"),
  (s) =>
    (s.packages[0].options[0].artifacts[0].text = "<script>execute()</script>"),
  (s) => (s.templates[0].text = "changed"),
  (s) => (s.templates[0].kind = "Script"),
  (s) => (s.warnings = []),
  (s) => (s.unavailableSections = []),
  (s) => s.packages[0].options.reverse(),
  (s) => s.packages[0].options[0].artifacts.reverse(),
  (s) => s.templates.reverse(),
  (s) => s.guidance.findings[0].options.reverse(),
  (s) => s.packages[0].options.push(s.packages[0].options[0]),
  (s) => s.guidance.findings.push(s.guidance.findings[0]),
])
  await reject(
    "forged nested actual graph with recomputed caches",
    (f) => mutation(f.analysis.fixPackages.snapshot),
    true,
  );
for (const modify of [
  (s) => (s.canonicalJson = "{}"),
  (s) => (s.canonicalJson += " "),
  (s) =>
    (s.canonicalJson = s.canonicalJson.replace("{", '{"status":"Unverified",')),
  (s) => (s.canonicalJson = s.canonicalJson.replace("{", '{"execute":true,')),
  (s) =>
    (s.canonicalJson = s.canonicalJson
      .replace("\\u0022", "\\u0022")
      .replace("Unverified", "Reviewed")),
  (s) => (s.canonicalJson = s.canonicalJson.replace("\\u00E9", "é")),
  (s) => (s.canonicalJson = "x".repeat(32 * 1024 * 1024 + 1)),
  (s) => (s.canonicalJson = "中".repeat(12 * 1024 * 1024)),
  (s) => (s.contentDigest = "0".repeat(64)),
  (s) => (s.contentDigest = s.contentDigest.toUpperCase()),
])
  await reject("canonical bytes or hash fence", (f) =>
    modify(f.analysis.fixPackages.snapshot),
  );
for (const member of [
  "originalTitle",
  "presentationTitle",
  "businessContext",
  "rootCause",
  "validationGuidance",
  "guidanceReferences",
  "assumptions",
  "limitations",
])
  await reject("mutated displayed finding " + member, (f) => {
    const finding = f.analysis.fixPackages.snapshot.guidance.findings[0];
    finding[member] = Array.isArray(finding[member]) ? ["changed"] : "changed";
  });
for (const hostile of [
  "<svg onload=alert(1)>",
  '<iframe src="https://example.invalid">',
  '<form action="https://example.invalid"><input></form>',
  "javascript:alert(1)",
  '$(curl https://example.invalid); =HYPERLINK("https://example.invalid")',
  "é e\u0301 Ω 中文 😀",
  "\0embedded\r\nnext\t",
  "literal\\u00ff + `",
  "x".repeat(16384),
  "漢".repeat(16384),
  "😀".repeat(8192),
]) {
  const f = clone(),
    s = f.analysis.fixPackages.snapshot,
    finding = s.guidance.findings[0];
  finding.originalTitle = hostile;
  finding.presentationTitle = hostile;
  finding.businessContext = hostile;
  f.analysis.findings[0].originalTitle = hostile;
  f.analysis.findings[0].title = hostile;
  f.analysis.review.findings[0].originalTitle = hostile;
  f.analysis.review.findings[0].title = hostile;
  f.analysis.review.findings[0].businessContext = hostile;
  refresh(f);
  await accept("hostile coherent text or exact boundary", f);
  check(
    "hostile no injected node",
    !["<svg", "<iframe", "<form"].some((tag) => render(f).includes(tag)),
  );
}
for (const text of ["x".repeat(16385), "\ud800", "\udc00", "\u0085"])
  await reject(
    "invalid text boundary or scalar",
    (f) => {
      f.analysis.fixPackages.snapshot.guidance.findings[0].presentationTitle =
        text;
    },
    true,
  );
for (const baseline of [
  "synthetic-analysis-healthy-v1",
  "synthetic-analysis-findings-v1",
  "synthetic-analysis-mixed-v1",
  "synthetic-analysis-gaps-v1",
]) {
  const f = structuredClone(
      baseline === "synthetic-analysis-healthy-v1" ? empty : complete,
    ),
    g = f.analysis.fixPackages.snapshot.guidance;
  f.run.selection.baselineId = baseline;
  f.analysis.fixPackages.baselineId = baseline;
  g.source.baselineId = baseline;
  g.source.analysisLock.presetId = baseline;
  const lock = f.run.lockedInputs.find((i) => i.name === "Baseline");
  lock.version = baseline;
  lock.sha256 = hash(baseline);
  for (const finding of g.findings) {
    f.analysis.findings.find((i) => i.id === finding.findingId).baselineId =
      baseline;
    for (const o of finding.occurrences)
      o.occurrenceId = hash(
        canonical({
          EvidenceDigest: g.source.analysisLock.evidenceDigest,
          Id: finding.ruleId,
          ObjectId: o.objectId,
          PresetId: baseline,
          Scope: {
            CustomerId: g.source.scope.customerId,
            EnvironmentId: g.source.scope.environmentId,
            ProjectId: g.source.scope.projectId,
          },
          Version: finding.ruleVersion,
          runId: g.source.runId,
        }),
      );
    f.analysis.review.findings.find(
      (i) => i.id === finding.findingId,
    ).occurrenceIds = finding.occurrences.map((o) => o.occurrenceId);
  }
  if (baseline === "synthetic-analysis-gaps-v1") {
    f.run.progress.plannedUnits = 2;
    f.run.progress.terminalUnits = 2;
    f.run.stateCounts.push({ state: "NotAssessed", count: 1 });
    f.run.coverageCompletionKind = "CompleteWithGaps";
  }
  refresh(f);
  await accept("new profile explicit preset " + baseline, f);
}
await reject("gap completion disagrees with actual counts", (f) => {
  f.run.coverageCompletionKind = "CompleteWithGaps";
});
await reject("claimed complete despite gap counts", (f) => {
  f.run.stateCounts[0].state = "NotAssessed";
});
for (const state of ["Proposed", "Confirmed", "Rejected", "Deferred"]) {
  const f = clone(),
    snapshot = f.analysis.fixPackages.snapshot;
  snapshot.guidance.findings[0].currentState = state;
  f.analysis.findings[0].state = state;
  f.analysis.review.findings[0].state = state;
  refresh(f);
  await accept("finding decision preserves unverified artifacts " + state, f);
  check(
    "artifact identities/content invariant " + state,
    JSON.stringify(snapshot.packages) ===
      JSON.stringify(complete.analysis.fixPackages.snapshot.packages),
  );
  check(
    "explicit current state " + state,
    render(f).includes("<dt>Current finding state</dt><dd>" + state + "</dd>"),
  );
}
const pendingSource = clone();
const pending = coherentFixPackages(pendingSource.analysis, pendingSource.run);
pendingSource.run.revision++;
check("async verification repeats source fence", !(await pending));
denied++;
const frozen = clone();
function freeze(v) {
  if (v && typeof v === "object") {
    Object.values(v).forEach(freeze);
    Object.freeze(v);
  }
}
freeze(frozen);
await accept("deep frozen readonly DTO", frozen);
check("deep frozen component display", render(frozen) === actual);
const snapshotBefore = JSON.stringify(complete);
await coherentFixPackages(complete.analysis, complete.run);
render(complete);
check("input not mutated", JSON.stringify(complete) === snapshotBefore);
const malformed = clone();
malformed.analysis.fixPackages.snapshot.guidance.source = null;
check(
  "component malformed fail closed",
  render(malformed).includes("unavailable") &&
    !render(malformed).includes('class="fix-package-artifact"'),
);
const css = await readFile(
  resolve(frontend, "src/FixPackagePreview.css"),
  "utf8",
);
check(
  "wrapping CSS",
  /overflow-wrap:\s*anywhere/.test(css) &&
    /white-space:\s*pre-wrap/.test(css) &&
    css.includes("minmax(0, 1fr)"),
);
check(
  "visible keyboard focus",
  css.includes("summary:focus-visible") && /outline:\s*3px/.test(css),
);
check("mobile breakpoint", css.includes("max-width: 480px"));
console.log(
  JSON.stringify({
    suite: "fix-package-preview-component-v1",
    checks,
    accepted,
    denied,
    completeMarkupSha256: hash(actual),
    compiledSha256: hash(compiled),
    result: "PASS",
    limitations:
      "Independent literal UI primitives and ReactDOMServer; actual saved input/host/browser/async request lifecycle and production/manual acceptance are outside this component host.",
  }),
);
