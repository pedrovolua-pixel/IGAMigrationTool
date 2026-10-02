import { readFile, writeFile, mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, "../../..");
const frontend = resolve(root, "src/web");
const require = createRequire(resolve(frontend, "package.json"));
const { transformWithOxc } = await import(
  pathToFileURL(resolve(frontend, "node_modules/vite/dist/node/index.js")).href
);
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const sourcePath = resolve(frontend, "src/AiProposalPreview.tsx");
const transformed = await transformWithOxc(
  await readFile(sourcePath, "utf8"),
  sourcePath,
  { lang: "tsx", jsx: { runtime: "automatic" }, target: "es2022" },
);
const moduleDirectory = resolve(directory, "artifacts");
await mkdir(moduleDirectory, { recursive: true });
const compiled = transformed.code
  .replace(/import ['"]\.\/AiProposalPreview\.css['"];?\n/g, "")
  .replaceAll(
    '"react/jsx-runtime"',
    JSON.stringify(pathToFileURL(require.resolve("react/jsx-runtime")).href),
  );
const compiledPath = resolve(moduleDirectory, "component.mjs");
await writeFile(compiledPath, compiled);
const { AiProposalPreview, coherentAiPreview } = await import(
  pathToFileURL(compiledPath).href
);
const fixture = JSON.parse(
  await readFile(resolve(directory, "fixture.json"), "utf8"),
);
const original = () => structuredClone(fixture);
const hash = (value) => createHash("sha256").update(value).digest("hex");
const stringify = (value) => {
  if (typeof value === "string")
    return (
      '"' +
      JSON.stringify(value)
        .slice(1, -1)
        .replace(/\\"/g, "\\u0022")
        .replace(
          /[<>&'+`\u007f-\uffff]/g,
          (unit) =>
            "\\u" +
            unit.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0"),
        ) +
      '"'
    );
  if (Array.isArray(value)) return "[" + value.map(stringify).join(",") + "]";
  return (
    "{" +
    Object.keys(value)
      .sort()
      .map((key) => stringify(key) + ":" + stringify(value[key]))
      .join(",") +
    "}"
  );
};
// Rebinding uses the independent Python fixture's parsed canonical object and
// Node's separate JSON string serializer; never the component's private helper.
function refresh(f) {
  const s = f.analysis.aiPreview.snapshot;
  const payload = Object.fromEntries(
    Object.entries(s).filter(
      ([key]) => key !== "contentDigest" && key !== "canonicalJson",
    ),
  );
  s.canonicalJson = stringify(payload);
  s.contentDigest = hash(s.canonicalJson);
}
let checks = 0;
let denied = 0;
let accepted = 0;
const check = (code, condition) => {
  assert.ok(condition, code);
  checks++;
};
const render = (f) =>
  renderToStaticMarkup(
    React.createElement(AiProposalPreview, {
      preview: f.analysis.aiPreview,
      run: f.run,
    }),
  );
async function accept(code, f) {
  check(code, await coherentAiPreview(f.analysis, f.run));
  accepted++;
}
async function reject(code, mutate) {
  const f = original();
  mutate(f);
  check(code, !(await coherentAiPreview(f.analysis, f.run)));
  denied++;
}
await accept("independent literal fixture accepted", original());
const golden = await readFile(
  resolve(directory, "golden-component.html"),
  "utf8",
);
const goldenDigest = await readFile(
  resolve(directory, "golden-component.sha256"),
  "utf8",
);
const actual = render(original());
check("independent complete component UTF-8 bytes", actual === golden);
check("literal independent full markup SHA256", hash(actual) === goldenDigest);
check("render deterministic", render(original()) === golden);
check(
  "React script text encoded",
  actual.includes(
    "&lt;script&gt;alert(&#x27;x&#x27; &amp; &quot;y&quot;)&lt;/script&gt;",
  ),
);
check("Unicode text retained", actual.includes("Unicode café 中文 😀"));
for (const forbidden of [
  "<script",
  "<img",
  "<svg",
  "<iframe",
  "<form",
  "<button",
  "<a ",
  " onclick=",
  "dangerouslySetInnerHTML",
])
  check(
    "no active element or action " + forbidden,
    !actual.includes(forbidden),
  );
for (const label of [
  "Facts",
  "Inferences",
  "Assumptions",
  "Suggestions",
  "Missing context",
  "Uncertainty",
  "Conflicting evidence IDs",
])
  check("typed category " + label, actual.includes("<h5>" + label + "</h5>"));
for (const field of [
  "Customer",
  "Project",
  "Environment",
  "Run ID",
  "Run revision",
  "Baseline",
  "Profile",
  "Complete frozen input digest",
  "Complete offline fixture digest",
  "Configuration template digest",
  "Source profile digest",
  "Normalization version",
  "Redaction version",
  "Prompt version",
  "Packet digest",
  "Proposal digest",
  "Canonical preview content digest",
  "Preview schema",
  "Preview status",
])
  check("source field " + field, actual.includes("<dt>" + field + "</dt>"));
check(
  "progressive keyboard disclosure",
  actual.includes(
    '<details class="ai-preview-source"><summary>Captured preview source and digests</summary>',
  ),
);
check(
  "fixed retry focus heading",
  actual.includes('id="ai-proposal-preview-heading" tabindex="-1"'),
);
check("source no raw canonical data", !actual.includes("canonicalJson"));
check(
  "supplied statement order",
  actual.indexOf("&lt;script&gt;") < actual.indexOf("Unicode café"),
);
check(
  "supplied context order",
  actual.indexOf("First missing context") <
    actual.indexOf("Second missing context"),
);
const historical = original();
historical.run.selection.profileId = "profile-standard";
historical.run.selection.baselineId = "baseline-complete";
historical.analysis.aiPreview = null;
await accept("historical additive null preserves old contract", historical);
check("historical component silent", render(historical) === "");
await reject("historical undefined denied", (f) => {
  f.run.selection.profileId = "profile-standard";
  f.run.selection.baselineId = "baseline-complete";
  delete f.analysis.aiPreview;
});
await reject("historical profile cannot use dedicated baseline", (f) => {
  f.run.selection.profileId = "profile-standard";
  f.analysis.aiPreview = null;
});
await reject("historical profile cannot return preview", (f) => {
  f.run.selection.profileId = "profile-standard";
  f.run.selection.baselineId = "baseline-complete";
});
await reject("dedicated profile requires detail", (f) => {
  f.analysis.aiPreview = null;
});
const empty = original();
empty.run.selection.profileId = "profile-ai-preview-empty-v1";
empty.analysis.aiPreview.profileId = empty.run.selection.profileId;
empty.run.lockedInputs.find((i) => i.name === "Profile").version =
  "synthetic-ai-preview-empty-profile-v1";
empty.analysis.aiPreview.snapshot.proposals = [];
refresh(empty);
await accept("empty opt-in exact fixture", empty);
check(
  "empty never implies health",
  render(empty).includes(
    "No proposals were returned. This does not establish healthy or complete assessment coverage.",
  ),
);
await reject("empty profile rejects supplied proposals", (f) => {
  f.run.selection.profileId = "profile-ai-preview-empty-v1";
  f.analysis.aiPreview.profileId = f.run.selection.profileId;
  f.run.lockedInputs.find((i) => i.name === "Profile").version =
    "synthetic-ai-preview-empty-profile-v1";
});
const unavailable = original();
unavailable.analysis.aiPreview.status = "Unavailable";
unavailable.analysis.aiPreview.reasonCode = "fixture_mismatch";
unavailable.analysis.aiPreview.snapshot = null;
await accept(
  "typed unavailable accepted without substituted snapshot",
  unavailable,
);
check(
  "unavailable no partial field display",
  render(unavailable).includes("No proposal or health result is inferred.") &&
    !render(unavailable).includes("Unicode café"),
);
for (const field of ["runId", "runRevision", "schemaVersion", "demoOnly"])
  await reject("outer analysis fence " + field, (f) => {
    f.analysis[field] = typeof f.analysis[field] === "number" ? 9 : "foreign";
  });
for (const field of [
  "schemaVersion",
  "runId",
  "runRevision",
  "runInputDigest",
  "baselineId",
  "profileId",
  "fixtureDigest",
  "status",
  "reasonCode",
])
  await reject("detail fence " + field, (f) => {
    f.analysis.aiPreview[field] =
      typeof f.analysis.aiPreview[field] === "number" ? 6 : "foreign";
  });
for (const field of Object.keys(fixture.analysis.aiPreview)) {
  await reject("detail missing " + field, (f) => {
    delete f.analysis.aiPreview[field];
  });
  if (field !== "reasonCode")
    await reject("detail null " + field, (f) => {
      f.analysis.aiPreview[field] = null;
    });
}
await reject("detail unknown field", (f) => {
  f.analysis.aiPreview.action = "approve";
});
await reject("detail unavailable must have null snapshot", (f) => {
  f.analysis.aiPreview.status = "Unavailable";
  f.analysis.aiPreview.reasonCode = "fixture_mismatch";
});
await reject("detail unavailable requires typed reason", (f) => {
  f.analysis.aiPreview.status = "Unavailable";
  f.analysis.aiPreview.reasonCode = null;
  f.analysis.aiPreview.snapshot = null;
});
for (const field of [
  "state",
  "cancelRequested",
  "coverageCompletionKind",
  "revision",
  "runId",
])
  await reject("selected run fence " + field, (f) => {
    f.run[field] =
      field === "cancelRequested" ? true : field === "revision" ? 6 : "foreign";
  });
for (const field of [
  "allTerminal",
  "plannedUnits",
  "terminalUnits",
  "remainingUnits",
])
  await reject("complete coverage fence " + field, (f) => {
    f.run.progress[field] =
      field === "allTerminal" ? false : field === "remainingUnits" ? 1 : 0;
  });
for (const field of ["plannedUnits", "terminalUnits", "remainingUnits"])
  await reject("typed coverage count " + field, (f) => {
    f.run.progress[field] = String(f.run.progress[field]);
  });
await reject("coverage extra field", (f) => {
  f.run.progress.extra = true;
});
for (const input of fixture.run.lockedInputs) {
  await reject("frozen lock version " + input.name, (f) => {
    f.run.lockedInputs.find((i) => i.name === input.name).version = "foreign";
  });
  await reject("frozen lock missing " + input.name, (f) => {
    f.run.lockedInputs = f.run.lockedInputs.filter(
      (i) => i.name !== input.name,
    );
  });
}
await reject("duplicate frozen input", (f) => {
  f.run.lockedInputs.push(f.run.lockedInputs[0]);
});
await reject("unknown frozen input", (f) => {
  f.run.lockedInputs.push({
    name: "foreign",
    version: "foreign",
    sha256: "a".repeat(64),
  });
});
for (const name of [
  "Complete frozen input",
  "Frozen offline AI contents",
  "Offline AI configuration template",
])
  await reject("frozen digest " + name, (f) => {
    f.run.lockedInputs.find((i) => i.name === name).sha256 = "9".repeat(64);
  });
for (const field of Object.keys(fixture.analysis.aiPreview.snapshot)) {
  await reject("snapshot missing " + field, (f) => {
    delete f.analysis.aiPreview.snapshot[field];
  });
  await reject("snapshot null " + field, (f) => {
    f.analysis.aiPreview.snapshot[field] = null;
  });
}
await reject("snapshot unknown field", (f) => {
  f.analysis.aiPreview.snapshot.action = "download";
});
await reject("snapshot noncanonical whitespace", (f) => {
  const s = f.analysis.aiPreview.snapshot;
  s.canonicalJson += " ";
  s.contentDigest = hash(s.canonicalJson);
});
await reject("snapshot duplicate canonical member", (f) => {
  const s = f.analysis.aiPreview.snapshot;
  s.canonicalJson = s.canonicalJson.replace(
    '"status":"Proposed"',
    '"status":"Proposed","status":"Proposed"',
  );
  s.contentDigest = hash(s.canonicalJson);
});
await reject("snapshot extra canonical field", (f) => {
  const s = f.analysis.aiPreview.snapshot;
  s.canonicalJson = s.canonicalJson.replace("{", '{"action":"approve",');
  s.contentDigest = hash(s.canonicalJson);
});
await reject("snapshot digest corrupted", (f) => {
  f.analysis.aiPreview.snapshot.contentDigest = "0".repeat(64);
});
await reject("snapshot UTF8 byte guard", (f) => {
  f.analysis.aiPreview.snapshot.canonicalJson = "中".repeat(1400000);
});
for (const field of Object.keys(fixture.analysis.aiPreview.snapshot.source)) {
  await reject("source value or typed canonical fence " + field, (f) => {
    f.analysis.aiPreview.snapshot.source[field] = "foreign";
  });
  await reject("source wrong even after canonical rehash " + field, (f) => {
    f.analysis.aiPreview.snapshot.source[field] = "foreign";
    refresh(f);
  });
}
for (const field of ["facts", "inferences", "assumptions", "suggestions"]) {
  for (const member of ["text", "evidenceIds", "ruleIds"])
    await reject(
      "complete statement canonical fence " + field + "/" + member,
      (f) => {
        const s = f.analysis.aiPreview.snapshot.proposals[0][field][0];
        s[member] = member === "text" ? "changed" : ["changed"];
      },
    );
  for (const bad of [
    null,
    {},
    { text: "text", evidenceIds: [], ruleIds: [] },
    { text: "text", evidenceIds: ["foreign"], ruleIds: ["foreign"] },
  ])
    await reject("closed typed statement shape " + field, (f) => {
      f.analysis.aiPreview.snapshot.proposals[0][field] = [bad];
    });
}
for (const field of [
  "proposalId",
  "missingContext",
  "uncertainty",
  "conflictingEvidenceIds",
])
  await reject("complete proposal canonical fence " + field, (f) => {
    f.analysis.aiPreview.snapshot.proposals[0][field] =
      typeof f.analysis.aiPreview.snapshot.proposals[0][field] === "string"
        ? "changed"
        : ["changed"];
  });
for (const mutation of [
  (p) => {
    p.proposalId = "<script>";
  },
  (p) => {
    p.conflictingEvidenceIds = [p.conflictingEvidenceIds[0]];
  },
  (p) => {
    p.uncertainty = "";
  },
  (p) => {
    p.missingContext = [];
  },
  (p) => {
    p.facts[0].text = " ";
  },
  (p) => {
    p.facts[0].text = "\u0085";
  },
  (p) => {
    p.facts[0].text = "\ud800";
  },
  (p) => {
    p.facts[0].text = "x".repeat(4097);
  },
  (p) => {
    p.facts[0].evidenceIds.reverse();
  },
  (p) => {
    p.facts[0].ruleIds.push(p.facts[0].ruleIds[0]);
  },
  (p) => {
    p.extra = "action";
  },
  (p) => {
    p.facts[0].extra = "download";
  },
])
  await reject("invalid typed graph despite recomputed canonical", (f) => {
    mutation(f.analysis.aiPreview.snapshot.proposals[0]);
    refresh(f);
  });
for (const supplied of [
  "<svg onload=alert(1)>",
  '<iframe src="https://example.invalid">',
  "javascript:alert(1)",
  "<style>@import url(https://example.invalid)</style>",
  "é Ω 中文 😀",
  "\uFEFF",
  "\0embedded",
  "line\r\nnext",
  "  spaces preserved  ",
  "x".repeat(4096),
  "漢".repeat(4096),
  "😀".repeat(2048),
]) {
  const f = original();
  f.analysis.aiPreview.snapshot.proposals[0].facts[0].text = supplied;
  refresh(f);
  await accept("inert supplied text or exact boundary", f);
  const result = render(f);
  check(
    "no hostile node",
    !result.includes("<svg") &&
      !result.includes("<iframe") &&
      !result.includes("<style>"),
  );
}
const copied = original();
const before = JSON.stringify(copied);
await coherentAiPreview(copied.analysis, copied.run);
render(copied);
check("input DTO not mutated", JSON.stringify(copied) === before);
const malformed = original();
malformed.analysis.aiPreview.snapshot.source = null;
check(
  "component malformed graph fails closed",
  !render(malformed).includes("Unicode café") &&
    render(malformed).includes("unavailable"),
);
const css = await readFile(
  resolve(frontend, "src/AiProposalPreview.css"),
  "utf8",
);
check(
  "wrapping CSS",
  css.includes("overflow-wrap: anywhere") && css.includes("minmax(0, 1fr)"),
);
check(
  "visible keyboard focus CSS",
  css.includes("summary:focus-visible") && css.includes("outline: 3px"),
);
check("mobile breakpoint CSS", css.includes("@media (max-width: 480px)"));
await writeFile(resolve(moduleDirectory, "actual-component.html"), actual);
console.log(
  JSON.stringify({
    suite: "ai-proposal-preview-component-v1",
    checks,
    accepted,
    denied,
    goldenDigest,
    compiledDigest: hash(compiled),
    result: "PASS",
    limitations:
      "Literal isolated frontend fixtures, ReactDOMServer and exact SHA/DTO fences; actual host/browser/deployed/manual checks belong to V10/coordinator.",
  }),
);
