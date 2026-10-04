import { createHash } from "node:crypto";
export const hash = (value) => createHash("sha256").update(value).digest("hex");
// Independent wire-byte oracle, no app/component canonicalization import.
function quoted(value) {
  let result = '"';
  const shortcuts = new Map([
    [92, "\\\\"],
    [8, "\\b"],
    [12, "\\f"],
    [10, "\\n"],
    [13, "\\r"],
    [9, "\\t"],
  ]);
  for (let i = 0; i < value.length; i++) {
    const unit = value.charCodeAt(i);
    result += shortcuts.has(unit)
      ? shortcuts.get(unit)
      : unit < 32 || unit >= 127 || [34, 38, 39, 43, 60, 62, 96].includes(unit)
        ? "\\u" + unit.toString(16).toUpperCase().padStart(4, "0")
        : value[i];
  }
  return result + '"';
}
export function canonical(value) {
  if (typeof value === "string") return quoted(value);
  if (Array.isArray(value)) return "[" + value.map(canonical).join(",") + "]";
  if (value && typeof value === "object")
    return (
      "{" +
      Object.keys(value)
        .sort()
        .map((key) => quoted(key) + ":" + canonical(value[key]))
        .join(",") +
      "}"
    );
  if (value === null || typeof value === "number" || typeof value === "boolean")
    return JSON.stringify(value);
  throw new Error("oracle-type");
}

export const profile = "synthetic-review-maturity-fix-review-equal-v1";
export const templateVersion = "fictional-fix-templates-v1";
export const templateDigest =
  "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
export const disclaimer =
  "Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.";
export const templates = [
  {
    templateId: "fictional-config-v1",
    kind: "Configuration",
    text: '{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}',
  },
  {
    templateId: "fictional-script-v1",
    kind: "Script",
    text: "# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'",
  },
  {
    templateId: "fictional-sql-v1",
    kind: "Sql",
    text: "-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;",
  },
];
export const packageId = (findingId, runId, scope) =>
  hash(canonical({ findingId, runId, scope }));
export const optionId = (findingId, optionId, runId, scope) =>
  hash(canonical({ findingId, optionId, runId, scope }));
export const artifactId = (packageId, scopedOptionId, templateId) =>
  hash(canonical({ packageId, scopedOptionId, templateId, templateVersion }));
export function payload(guidance) {
  const { runId, scope } = guidance.source;
  const packages = [...guidance.findings]
    .sort((a, b) =>
      a.findingId < b.findingId ? -1 : a.findingId > b.findingId ? 1 : 0,
    )
    .map((f) => {
      const id = packageId(f.findingId, runId, scope);
      return {
        packageId: id,
        findingId: f.findingId,
        options: [...f.options]
          .sort((a, b) =>
            a.scopedOptionId < b.scopedOptionId
              ? -1
              : a.scopedOptionId > b.scopedOptionId
                ? 1
                : 0,
          )
          .map((o) => ({
            scopedOptionId: optionId(f.findingId, o.optionId, runId, scope),
            artifacts: templates.map((t) => ({
              artifactId: artifactId(id, o.scopedOptionId, t.templateId),
              templateId: t.templateId,
              kind: t.kind,
              status: "Unverified",
              text: t.text,
            })),
          })),
      };
    });
  const warnings = [
    "Finding confirmation, rejection or deferral does not review artifacts or validate remediation.",
    "Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.",
    "Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.",
  ];
  if (!packages.length)
    warnings.push(
      "No findings were supplied; no fix packages or actions are available.",
    );
  return {
    schemaVersion: "synthetic-fix-package-preview-v1",
    status: "Unverified",
    disclaimer,
    guidance: structuredClone(guidance),
    templateVersion,
    templateDigest,
    templates: structuredClone(templates),
    packages,
    warnings,
    unavailableSections: [
      "Consultant artifact review, approval history and content invalidation are unavailable.",
      "Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.",
      "Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.",
    ],
  };
}
export function rehash(snapshot) {
  const p = structuredClone(snapshot);
  delete p.canonicalJson;
  delete p.contentDigest;
  snapshot.canonicalJson = canonical(p);
  snapshot.contentDigest = hash(snapshot.canonicalJson);
  return snapshot;
}
export function textLeaves(value) {
  if (value === null) return [];
  if (typeof value !== "object") return [String(value)];
  return Object.values(value).flatMap(textLeaves);
}

export const contractDigest =
  "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f";
export const currentLabel =
  "Reviewed for planning — fictional, review-only; correctness and remediation unverified";
export function binding(snapshot) {
  const g = snapshot.guidance,
    s = g.source;
  return {
    scope: structuredClone(s.scope),
    runId: s.runId,
    runRevision: s.runRevision,
    runInputDigest: s.runInputDigest,
    baselineId: s.baselineId,
    profileId: s.profileId,
    applicationVersion: s.frozenVersions.applicationVersion,
    contractDigest,
    sourceDigest: hash(canonical(payload(g))),
    guidanceDigest: g.contentDigest,
    findingReviewDigest: s.reviewSnapshotDigest,
    templateVersion,
    templateDigest,
    findingRevisions: g.findings.map((f) => ({
      findingId: f.findingId,
      revision: f.findingRevision,
    })),
  };
}
export function artifacts(snapshot) {
  const cats = new Map(
    snapshot.guidance.findings.map((f) => [f.findingId, f.categoryId]),
  );
  return snapshot.packages
    .flatMap((p) =>
      p.options.flatMap((o) =>
        o.artifacts.map((a) => ({
          findingId: p.findingId,
          categoryId: cats.get(p.findingId),
          packageId: p.packageId,
          scopedOptionId: o.scopedOptionId,
          artifactId: a.artifactId,
          templateId: a.templateId,
          kind: a.kind,
          artifactTextDigest: hash(a.text),
        })),
      ),
    )
    .sort((a, b) =>
      a.findingId < b.findingId
        ? -1
        : a.findingId > b.findingId
          ? 1
          : a.artifactId < b.artifactId
            ? -1
            : a.artifactId > b.artifactId
              ? 1
              : 0,
    );
}
export function state(history, sourceDigest) {
  const last = history.at(-1);
  return !last || last.kind === "WithdrawReview"
    ? "Unverified"
    : last.source.sourceDigest === sourceDigest
      ? "ReviewedForPlanning"
      : "NeedsReview";
}
export const commandDigest = (source, artifactId, actorId, command) =>
  hash(
    canonical({
      schemaVersion: "synthetic-fix-review-command-v1",
      scope: source.scope,
      runId: source.runId,
      artifactId,
      actorId,
      command,
    }),
  );
