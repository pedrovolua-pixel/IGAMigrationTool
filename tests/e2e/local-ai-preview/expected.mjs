import { createHash } from "node:crypto";
export const baseline = "baseline-ai-configuration-v1";
export const normal = "profile-ai-preview-v1";
export const empty = "profile-ai-preview-empty-v1";
export const A = "ev-" + "1".repeat(64);
export const B = "ev-" + "2".repeat(64);
export const schedule = "fixture-rule-schedule-v1";
export const conflict = "fixture-rule-conflict-v1";
export const disclaimer =
  "Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.";
export const suggestion =
  "Review these fictional settings; <script>window.fixtureExecuted=true</script> is data only.";
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
const statement = (text, evidenceIds, ruleIds) => ({
  text,
  evidenceIds: [...evidenceIds].sort(),
  ruleIds: [...ruleIds].sort(),
});
export function proposals(isEmpty) {
  return isEmpty
    ? []
    : [
        {
          proposalId: "proposal-01",
          facts: [
            statement(
              "Fictional scheduleEnabled is disabled.",
              [A],
              [schedule],
            ),
            statement(
              "Fictional retryPolicy describes three attempts.",
              [B],
              [conflict],
            ),
          ],
          inferences: [
            statement(
              "Fictional schedule and retry evidence may conflict.",
              [B, A],
              [schedule, conflict],
            ),
          ],
          assumptions: [
            statement("Fictional context: café, 漢字, 😀.", [A], [schedule]),
          ],
          missingContext: [
            "Consultant confirmation of the fictional fixture assumptions.",
            "No actual provider or customer evidence was used.",
          ],
          suggestions: [statement(suggestion, [B, A], [conflict])],
          uncertainty:
            "Fictional conflict remains unresolved; no setting is selected.",
          conflictingEvidenceIds: [A, B],
        },
      ];
}
export function full(run, templateDigest) {
  const source = {
    customerId: "synthetic-customer",
    projectId: "synthetic-project",
    environmentId: "synthetic-environment",
    runId: run.runId,
    baselineDigest: templateDigest,
    profileDigest: run.lockedInputs.find(
      (x) => x.name === "Complete frozen input",
    ).sha256,
    normalizationVersion: "fixture-normalization-v1",
    redactionVersion: "fixture-redaction-v1",
    promptVersion: "fixture-prompt-v1",
  };
  const packet = canonical({
    schemaVersion: "synthetic-ai-fixture-packet-v1",
    status: "SyntheticDataOnly",
    source,
    evidence: [
      {
        evidenceId: A,
        classification: "NormalizedRedactedConfiguration",
        redactionCount: 0,
        configurationKey: "scheduleEnabled",
        configurationValue: "false",
      },
      {
        evidenceId: B,
        classification: "NormalizedRedactedConfiguration",
        redactionCount: 0,
        configurationKey: "retryPolicy",
        configurationValue: "three attempts",
      },
    ],
    ruleIds: [conflict, schedule],
  });
  const values = proposals(run.selection.profileId === empty);
  const proposal = canonical({
    schemaVersion: "synthetic-ai-proposal-snapshot-v1",
    status: "Proposed",
    runId: run.runId,
    packetDigest: hash(packet),
    proposals: values,
  });
  const preview = canonical({
    schemaVersion: "synthetic-ai-preview-v1",
    status: "Proposed",
    source,
    packetDigest: hash(packet),
    proposalDigest: hash(proposal),
    proposals: values,
    disclaimer,
  });
  return { source, packet, proposal, preview, proposals: values };
}
