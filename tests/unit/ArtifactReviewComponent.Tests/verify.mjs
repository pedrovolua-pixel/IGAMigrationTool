import { readFile, writeFile, mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import assert from "node:assert/strict";
const here = dirname(fileURLToPath(import.meta.url)),
  root = resolve(here, "../../.."),
  frontend = resolve(root, "src/web"),
  out = resolve(here, "artifacts");
const require = createRequire(resolve(frontend, "package.json")),
  React = require("react"),
  { renderToStaticMarkup } = require("react-dom/server");
const { transformWithOxc } = await import(
  pathToFileURL(resolve(frontend, "node_modules/vite/dist/node/index.js")).href
);
await mkdir(out, { recursive: true });
for (const name of ["FixPackagePreview", "ArtifactReviewPanel"]) {
  const path = resolve(frontend, "src", name + ".tsx");
  const result = await transformWithOxc(await readFile(path, "utf8"), path, {
    lang: "tsx",
    jsx: { runtime: "automatic" },
    target: "es2022",
  });
  const code = result.code
    .replace(new RegExp(`import ['"]\\./${name}\\.css['"];?\\n`, "g"), "")
    .replaceAll(
      '"react/jsx-runtime"',
      JSON.stringify(pathToFileURL(require.resolve("react/jsx-runtime")).href),
    )
    .replaceAll('"./FixPackagePreview"', '"./FixPackagePreview.mjs"');
  await writeFile(
    resolve(out, name + ".mjs"),
    code +
      (name === "ArtifactReviewPanel"
        ? "\nexport { structural as testOnlyStructural };\n"
        : ""),
  );
}
const { ArtifactReviewPanel, coherentArtifactReview, testOnlyStructural } =
  await import(pathToFileURL(resolve(out, "ArtifactReviewPanel.mjs")).href);
const complete = JSON.parse(
    await readFile(resolve(here, "complete-fixture.json"), "utf8"),
  ),
  empty = JSON.parse(
    await readFile(resolve(here, "empty-fixture.json"), "utf8"),
  );
let count = 0,
  accepted = 0,
  denied = 0;
const check = (v, label) => {
    assert.ok(v, label);
    count++;
  },
  equal = (a, b, label) => {
    assert.deepEqual(a, b, label);
    count++;
  };
const clone = () => structuredClone(complete),
  hash = (s) => createHash("sha256").update(s).digest("hex"),
  artifact = (x) => x.analysis.artifactReview.artifacts[0],
  source = (x) => x.analysis.artifactReview.source;
const noop = () => {},
  props = (x, drafts = {}) => ({
    analysis: x.analysis,
    run: x.run,
    drafts,
    onReasonChange: noop,
    onAction: noop,
    onRetry: noop,
    onRefresh: noop,
  });
const render = (x, drafts = {}) =>
  renderToStaticMarkup(
    React.createElement(ArtifactReviewPanel, props(x, drafts)),
  );
async function accept(x, label) {
  check(await coherentArtifactReview(x.analysis, x.run), label);
  accepted++;
}
async function deny(x, label, sync = true) {
  check(!(await coherentArtifactReview(x.analysis, x.run)), label);
  denied++;
  if (sync) {
    const html = render(x);
    check(
      html.includes("Artifact review data is inconsistent."),
      label + " no partial panel",
    );
    check(
      !html.includes("<textarea") &&
        !html.includes("Original fictional artifact text"),
      label + " no controls/content",
    );
  }
}
const pristine = JSON.stringify(complete);
for (const [name, x] of [
  ["complete", complete],
  ["empty", empty],
]) {
  await accept(x, name);
  const actual = render(x),
    expected = await readFile(resolve(here, name + "-expected.html"), "utf8");
  equal(actual, expected, name + " independent complete HTML bytes");
  await writeFile(resolve(out, name + "-actual.html"), actual);
}
const html = render(complete);
for (const e of complete.analysis.artifactReview.artifacts) {
  for (const field of [
    "findingId",
    "packageId",
    "scopedOptionId",
    "artifactId",
    "categoryId",
    "templateId",
    "kind",
    "artifactTextDigest",
  ])
    check(html.includes(e[field]), "actual original field " + field);
  check(html.includes("artifact-review-" + e.artifactId), "safe focus heading");
}
check(
  !/<script|<iframe|<a\b|<img|\son\w+=|href=|src=/i.test(html),
  "inert markup no supplied resource/actions",
);
check(
  html.includes("Generated originals stay Unverified.") &&
    html.includes("correctness") === false,
  "original/current fixed boundary",
);
const reason =
  "  Review \0\r\n<script>window.injected=1</script> https://invalid.example/ 😀 e\u0301  ";
function event(x, n = 1, kind = "ReviewForPlanning", s = source(x)) {
  return {
    eventId: `10000000-2222-4333-8444-${String(n).padStart(12, "0")}`,
    revision: n,
    kind,
    actorId: "synthetic-consultant",
    actorRoles: ["Consultant"],
    recordedAtUtc: "2026-10-02T12:34:56.1234567Z",
    reason,
    source: structuredClone(s),
    recordedState:
      kind === "ReviewForPlanning" ? "ReviewedForPlanning" : "Unverified",
  };
}
function history(x, events, state) {
  const e = artifact(x);
  e.history = events;
  e.revision = events.length;
  e.state = state;
  e.canReview = state !== "ReviewedForPlanning";
  e.canWithdraw = state === "ReviewedForPlanning";
  return x;
}
const reviewed = history(clone(), [], "Unverified");
history(reviewed, [event(reviewed)], "ReviewedForPlanning");
await accept(reviewed, "reviewed exact current");
check(
  render(reviewed).includes(
    "Reviewed for planning — fictional, review-only; correctness and remediation unverified",
  ),
  "exact current planning label",
);
const withdrawn = clone();
history(
  withdrawn,
  [event(withdrawn), event(withdrawn, 2, "WithdrawReview")],
  "Unverified",
);
await accept(withdrawn, "withdrawal current");
const rereview = structuredClone(withdrawn);
artifact(rereview).history.push(event(rereview, 3));
artifact(rereview).revision = 3;
artifact(rereview).state = "ReviewedForPlanning";
artifact(rereview).canReview = false;
artifact(rereview).canWithdraw = true;
await accept(rereview, "review withdrawal rereview");
const needs = clone(),
  old = structuredClone(source(needs));
old.runRevision--;
old.sourceDigest = "b".repeat(64);
old.guidanceDigest = "c".repeat(64);
old.findingReviewDigest = "d".repeat(64);
history(needs, [event(needs, 1, "ReviewForPlanning", old)], "NeedsReview");
await accept(needs, "old source needs review");
const afterWithdrawal = clone();
history(
  afterWithdrawal,
  [
    event(afterWithdrawal, 1, "ReviewForPlanning", old),
    event(afterWithdrawal, 2, "WithdrawReview", old),
  ],
  "Unverified",
);
await accept(
  afterWithdrawal,
  "withdrawn remains unverified after source refresh",
);
const changedReview = clone();
history(
  changedReview,
  [event(changedReview, 1, "ReviewForPlanning", old), event(changedReview, 2)],
  "ReviewedForPlanning",
);
await accept(changedReview, "fresh review after source change");
const sameRun = clone(),
  sameOld = structuredClone(source(sameRun));
sameOld.findingRevisions[0].revision--;
sameOld.sourceDigest = "b".repeat(64);
sameOld.guidanceDigest = "c".repeat(64);
sameOld.findingReviewDigest = "d".repeat(64);
history(
  sameRun,
  [event(sameRun, 1, "ReviewForPlanning", sameOld)],
  "NeedsReview",
);
await accept(sameRun, "finding refresh unchanged run revision invalidates");
const hostile = render(reviewed);
check(
  hostile.includes("&lt;script&gt;window.injected=1&lt;/script&gt;") &&
    !hostile.includes("<script>"),
  "hostile history remains inert",
);
check(
  hostile.includes("\0\r\n") &&
    hostile.includes("😀") &&
    hostile.includes("e\u0301"),
  "SSR retains UTF16/control text exact; no parser inference",
);
for (const reason of [
  "\0",
  "\r\nX\t",
  "😀".repeat(1000),
  "a".repeat(2000),
  " e\u0301 ",
]) {
  const x = structuredClone(reviewed);
  artifact(x).history[0].reason = reason;
  await accept(x, "exact reason boundary");
}
for (const reason of [
  "",
  " \r\n\t\u0085",
  "a".repeat(2001),
  "😀".repeat(1001),
  "\ud800",
]) {
  const x = structuredClone(reviewed);
  artifact(x).history[0].reason = reason;
  await deny(x, "invalid reason");
}
for (const token of [
  "artifact_review_source_unavailable",
  "artifact_review_integrity_denied",
  "artifact_review_denied",
]) {
  const x = clone();
  x.analysis.artifactReview = {
    schemaVersion: 1,
    demoOnly: true,
    status: "Unavailable",
    reasonCode: token,
    source: null,
    actorId: null,
    artifacts: [],
  };
  await accept(x, "closed unavailable " + token);
  check(
    render(x).includes(token) && !render(x).includes("textarea"),
    "safe unavailable text",
  );
}
for (const mutation of [
  (x) => (x.analysis.artifactReview = null),
  (x) => (x.analysis.artifactReview.schemaVersion = 2),
  (x) => (x.analysis.artifactReview.demoOnly = false),
  (x) => (x.analysis.artifactReview.status = "ready"),
  (x) => (x.analysis.artifactReview.reasonCode = "foreign"),
  (x) => (x.analysis.artifactReview.actorId = "foreign"),
  (x) => (x.analysis.runId = "foreign"),
  (x) => x.analysis.runRevision--,
  (x) => x.run.revision++,
  (x) => (x.run.selection.profileId = "unknown"),
  (x) => x.run.lockedInputs.pop(),
  (x) => (x.run.lockedInputs[0] = x.run.lockedInputs[1]),
  (x) => (x.run.lockedInputs.at(-1).sha256 = "b".repeat(64)),
  (x) => (x.run.lockedInputs.at(-1).version = "foreign"),
  (x) => (x.analysis.review.actor = "other"),
  (x) => (x.analysis.review.snapshotDigest = "b".repeat(64)),
  (x) => x.analysis.review.findings[0].revision++,
  (x) => x.analysis.artifactReview.artifacts.reverse(),
  (x) => x.analysis.artifactReview.artifacts.pop(),
  (x) => x.analysis.artifactReview.artifacts.push(artifact(x)),
  (x) => source(x).findingRevisions[0].revision++,
  (x) => source(x).findingRevisions.push(source(x).findingRevisions[0]),
]) {
  const x = clone();
  mutation(x);
  await deny(x, "whole capture/closed source denial");
}
for (const part of ["detail", "source", "scope", "findingRevision", "entry"]) {
  for (const mode of ["extra", "missing"]) {
    const base = clone(),
      v =
        part === "detail"
          ? base.analysis.artifactReview
          : part === "source"
            ? source(base)
            : part === "scope"
              ? source(base).scope
              : part === "findingRevision"
                ? source(base).findingRevisions[0]
                : artifact(base);
    if (mode === "extra") {
      v.foreign = true;
      await deny(base, part + " unknown");
    } else
      for (const key of Object.keys(v)) {
        const x = clone(),
          target =
            part === "detail"
              ? x.analysis.artifactReview
              : part === "source"
                ? source(x)
                : part === "scope"
                  ? source(x).scope
                  : part === "findingRevision"
                    ? source(x).findingRevisions[0]
                    : artifact(x);
        delete target[key];
        await deny(x, part + " missing " + key);
      }
  }
}
for (const field of Object.keys(source(complete))) {
  const x = clone();
  source(x)[field] = null;
  await deny(x, "source null " + field);
}
for (const field of [
  "findingId",
  "categoryId",
  "packageId",
  "scopedOptionId",
  "artifactId",
  "templateId",
  "kind",
  "revision",
  "state",
  "canReview",
  "canWithdraw",
  "history",
]) {
  const x = clone();
  artifact(x)[field] = null;
  await deny(x, "entry invalid " + field);
}
for (const field of [
  "findingId",
  "packageId",
  "scopedOptionId",
  "artifactId",
]) {
  const x = clone();
  artifact(x)[field] = "e".repeat(64);
  await deny(x, "foreign actual identity " + field);
}
for (const field of Object.keys(event(reviewed))) {
  const x = structuredClone(reviewed);
  delete artifact(x).history[0][field];
  await deny(x, "history missing " + field);
}
for (const mutation of [
  (e) => (e.foreign = true),
  (e) => (e.eventId = "00000000-0000-0000-0000-000000000000"),
  (e) => (e.eventId = "foreign"),
  (e) => (e.revision = 2),
  (e) => (e.kind = "Review"),
  (e) => (e.actorId = ""),
  (e) => (e.actorRoles = ["QualifiedReviewer"]),
  (e) => (e.actorRoles = ["Consultant", "Consultant"]),
  (e) => (e.recordedAtUtc = "2026-02-30T12:34:56Z"),
  (e) => (e.recordedAtUtc = "2026-10-02T12:34:56+00:00"),
  (e) => (e.recordedState = "NeedsReview"),
  (e) => (e.source.runRevision = 9007199254740992),
  (e) => e.source.findingRevisions[0].revision++,
  (e) => (e.source.sourceDigest = "b".repeat(64)),
  (e) => (e.source.guidanceDigest = "b".repeat(64)),
  (e) => (e.source.contractDigest = "b".repeat(64)),
  (e) => (e.source.scope.customerId = "foreign"),
]) {
  const x = structuredClone(reviewed);
  mutation(artifact(x).history[0]);
  await deny(x, "invalid history/immutable binding");
}
for (const mutation of [
  (x) => (artifact(x).state = "Unverified"),
  (x) => (artifact(x).canReview = true),
  (x) => (artifact(x).canWithdraw = false),
  (x) => (artifact(x).revision = 0),
  (x) => artifact(x).history.push({ ...artifact(x).history[0], revision: 2 }),
  (x) => history(x, [event(x, 1, "WithdrawReview")], "Unverified"),
  (x) => history(x, [event(x), event(x, 2)], "ReviewedForPlanning"),
  (x) =>
    history(
      x,
      [event(x, 1, "ReviewForPlanning", old), event(x, 2, "WithdrawReview")],
      "Unverified",
    ),
  (x) =>
    history(
      x,
      [event(x), event(x, 2, "ReviewForPlanning", old)],
      "NeedsReview",
    ),
]) {
  const x = structuredClone(reviewed);
  mutation(x);
  await deny(x, "transition/derived denial");
}
const badText = clone();
artifact(badText).artifactTextDigest = "b".repeat(64);
await deny(badText, "actual text SHA mismatch", false);
const badPackage = clone();
badPackage.analysis.fixPackages.snapshot.canonicalJson += " ";
await deny(badPackage, "delegated complete canonical byte proof", false);
const historical = JSON.parse(
  await readFile(
    resolve(root, "tests/unit/FixPackagePreview.Tests/complete-fixture.json"),
    "utf8",
  ),
);
historical.analysis.artifactReview = null;
await accept(historical, "historical package null overlay");
equal(render(historical), "", "historical panel absent");
historical.analysis.artifactReview = complete.analysis.artifactReview;
await deny(historical, "historical foreign overlay");
const frozen = clone();
function freeze(v) {
  if (v && typeof v === "object") {
    Object.values(v).forEach(freeze);
    Object.freeze(v);
  }
}
freeze(frozen);
await accept(frozen, "deep frozen readonly capture");
equal(JSON.stringify(complete), pristine, "component/verifier nonmutation");
const race = clone(),
  promise = coherentArtifactReview(race.analysis, race.run);
race.analysis.artifactReview.actorId = "late foreign";
check(!(await promise), "mutation across async rejected");
denied++;
function elements(v) {
  if (!v || typeof v !== "object") return [];
  if (Array.isArray(v)) return v.flatMap(elements);
  return [v, ...elements(v.props?.children)];
}
const id = artifact(complete).artifactId,
  calls = [],
  p = props(complete, {
    [id]: {
      reason,
      pending: null,
      busy: false,
      error: null,
      requiresRefresh: false,
    },
  });
p.onReasonChange = (...args) => calls.push(["reason", ...args]);
p.onAction = (...args) => calls.push(["action", ...args]);
let tree = elements(ArtifactReviewPanel(p));
let area = tree.find(
  (e) => e.type === "textarea" && e.props.id === "artifact-review-reason-" + id,
);
area.props.onChange({ target: { value: reason } });
equal(calls.pop(), ["reason", id, reason], "controlled exact reason callback");
const article = tree.find((e) => e.type === "article" && e.key === id);
let button = elements(article).find((e) => e.type === "button");
equal(button.props.disabled, false, "nonblank reason enables explicit action");
button.props.onClick();
equal(
  calls.pop(),
  ["action", id, "ReviewForPlanning"],
  "proposal callback no command generation",
);
for (const scenario of ["pending", "busy", "refresh", "error"]) {
  const draft = {
    reason,
    pending: null,
    busy: false,
    error: null,
    requiresRefresh: false,
  };
  if (scenario === "pending")
    draft.pending = {
      eventId: "10000000-2222-4333-8444-000000000001",
      kind: "ReviewForPlanning",
      expectedRevision: 0,
      expectedSourceDigest: source(complete).sourceDigest,
      reason,
    };
  if (scenario === "busy") draft.busy = true;
  if (scenario === "refresh") draft.requiresRefresh = true;
  if (scenario === "error")
    draft.error = "<img src=x onerror=attack()> unavailable";
  const markup = render(complete, { [id]: draft });
  check(markup.includes("textarea"), "controlled state " + scenario);
  if (scenario !== "error")
    check(
      markup.includes("Current attestation withheld"),
      "pending state withholds attestation " + scenario,
    );
  if (scenario === "pending")
    check(
      markup.includes("Retry same artifact command") &&
        !markup.includes("eventId"),
      "uncertain exact retry text",
    );
  if (scenario === "error")
    check(
      markup.includes("&lt;img") && !markup.includes("<img"),
      "error inert text",
    );
}

for (const profile of [
  "profile-standard",
  "profile-comparison",
  "synthetic-analysis-equal-v1",
  "synthetic-analysis-operations-v1",
  "synthetic-review-maturity-equal-v1",
  "synthetic-review-maturity-operations-v1",
  "profile-ai-preview-v1",
  "profile-ai-preview-empty-v1",
]) {
  const x = clone();
  x.run.selection.profileId = profile;
  x.analysis.artifactReview = null;
  x.analysis.fixPackages = null;
  x.run.lockedInputs = x.run.lockedInputs.filter(
    (l) =>
      !["Artifact review contract", "Fictional fix-package templates"].includes(
        l.name,
      ),
  );
  await accept(x, "historical null " + profile);
  equal(render(x), "", "historical no panel " + profile);
}
const unknown = clone();
unknown.run.selection.profileId = "unknown";
unknown.analysis.artifactReview = null;
unknown.analysis.fixPackages = null;
unknown.run.lockedInputs = unknown.run.lockedInputs.filter(
  (l) =>
    !["Artifact review contract", "Fictional fix-package templates"].includes(
      l.name,
    ),
);
await deny(unknown, "unknown profile null denied");
const equivocation = clone(),
  equivOld = structuredClone(old);
equivOld.sourceDigest = source(equivocation).sourceDigest;
history(
  equivocation,
  [event(equivocation, 1, "ReviewForPlanning", equivOld)],
  "ReviewedForPlanning",
);
await deny(
  equivocation,
  "same source key cannot name different immutable binding",
);
for (const [draft, buttonText, callback] of [
  [
    {
      reason,
      pending: {
        eventId: "10000000-2222-4333-8444-000000000001",
        kind: "ReviewForPlanning",
        expectedRevision: 0,
        expectedSourceDigest: source(complete).sourceDigest,
        reason,
      },
      busy: false,
      error: null,
      requiresRefresh: false,
    },
    "Retry same artifact command",
    "retry",
  ],
  [
    { reason, pending: null, busy: false, error: null, requiresRefresh: true },
    "Refresh artifact source",
    "refresh",
  ],
]) {
  const pr = props(complete, { [id]: draft });
  pr.onRetry = (a) => calls.push(["retry", a]);
  pr.onRefresh = (a) => calls.push(["refresh", a]);
  const nodes = elements(ArtifactReviewPanel(pr));
  const target = nodes.find(
    (e) => e.type === "button" && e.props.children === buttonText,
  );
  target.props.onClick();
  equal(
    calls.pop(),
    [callback, id],
    "explicit " + callback + " controlled callback",
  );
  check(
    nodes.find(
      (e) =>
        e.type === "textarea" && e.props.id === "artifact-review-reason-" + id,
    ).props.disabled,
    "pending reason frozen " + callback,
  );
}
const withdrawProps = props(reviewed, {
  [id]: {
    reason,
    pending: null,
    busy: false,
    error: null,
    requiresRefresh: false,
  },
});
withdrawProps.onAction = (...a) => calls.push(a);
const withdrawArticle = elements(ArtifactReviewPanel(withdrawProps)).find(
  (e) => e.type === "article" && e.key === id,
);
const withdrawButton = elements(withdrawArticle).find(
  (e) => e.type === "button",
);
equal(
  withdrawButton.props.children,
  "Withdraw planning review",
  "current review gets withdrawal only",
);
withdrawButton.props.onClick();
equal(calls.pop(), [id, "WithdrawReview"], "controlled withdrawal proposal");
for (const reason of ["", "  \r\n\t", "a".repeat(2001)]) {
  const pr = props(complete, {
    [id]: {
      reason,
      pending: null,
      busy: false,
      error: null,
      requiresRefresh: false,
    },
  });
  check(
    elements(ArtifactReviewPanel(pr))
      .find((e) => e.type === "article" && e.key === id)
      .props.children.at(-1).props.disabled,
    "invalid draft never enables action",
  );
}

// Approved history has no event-count cap. Exercise the actual private guard via
// a test-only compiled export, avoiding enormous SSR/output and async JSON copies.
const longHistory = clone(),
  longEntry = artifact(longHistory),
  currentSource = source(longHistory);
longEntry.history = Array.from({ length: 100001 }, (_, i) => ({
  ...event(
    longHistory,
    i + 1,
    i % 2 === 0 ? "ReviewForPlanning" : "WithdrawReview",
  ),
  source: currentSource,
}));
longEntry.revision = 100001;
longEntry.state = "ReviewedForPlanning";
longEntry.canReview = false;
longEntry.canWithdraw = true;
check(
  testOnlyStructural(longHistory.analysis, longHistory.run),
  "full valid100001 history no invented cap",
);
accepted++;
// Source record limits remain strict, independently of durable history length.
const overSource = clone();
overSource.analysis.artifactReview.artifacts = Array(100001).fill(
  artifact(overSource),
);
await deny(overSource, "documented source artifact cap");
for (const e of reviewed.analysis.artifactReview.artifacts[0].history) {
  for (const field of [
    "eventId",
    "kind",
    "actorId",
    "recordedAtUtc",
    "reason",
    "recordedState",
  ]) {
    const expected = String(e[field])
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#x27;");
    check(
      hostile.includes(expected),
      "readable complete historical field " + field,
    );
  }
  for (const [key, value] of Object.entries(e.source)) {
    if (typeof value === "string")
      check(hostile.includes(value), "historical source metadata " + key);
  }
}
for (const value of [
  "10000000-2222-4333-8444-000000000001\n",
  "10000000-2222-4333-8444-000000000001\r\n",
]) {
  const x = structuredClone(reviewed);
  artifact(x).history[0].eventId = value;
  await deny(x, "UUID requires exact36bytes");
}
const newlineDigest = clone(),
  newlineOld = structuredClone(old);
newlineOld.sourceDigest = "b".repeat(64) + "\n";
history(
  newlineDigest,
  [event(newlineDigest, 1, "ReviewForPlanning", newlineOld)],
  "NeedsReview",
);
await deny(newlineDigest, "historical digest requires exact64hex");
await writeFile(
  resolve(out, "summary.json"),
  JSON.stringify(
    { assertions: count, accepted, denied, completeHtmlSha256: hash(html) },
    null,
    2,
  ) + "\n",
);
console.log(
  `B13 component PASS: ${count} assertions, ${accepted} accepted, ${denied} denials`,
);
