import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import * as expected from "./expected.mjs";
const root = new URL(
  "../../integration/LocalPlanningTasks.Tests/",
  import.meta.url,
);
const load = async (name) =>
  JSON.parse(await readFile(new URL(name, root), "utf8"));
const manifest = await load("expected-manifest.json");
let checks = 0;
const equal = (a, b) => {
  assert.deepEqual(a, b);
  checks++;
};
equal(expected.taskContractDigest, manifest.contractDigest);
equal(expected.templateDigest, manifest.templateDigest);
for (const [name, value] of Object.entries(manifest.cases)) {
  const bytes = await readFile(
    new URL(name + "-fix-golden.json", root),
    "utf8",
  );
  equal(expected.canonical(expected.payload(value.snapshot.guidance)), bytes);
  equal(expected.hash(bytes), value.packageDigest);
  equal(expected.taskBinding(value.snapshot), value.binding);
  equal(expected.artifacts(value.snapshot), value.artifacts);
}
for (const value of await load("identity-golden.json")) {
  equal(
    expected.canonical({
      schemaVersion: "synthetic-planning-task-identity-v1",
      scope: value.scope,
      runId: value.runId,
      findingId: value.findingId,
      scopedOptionId: value.scopedOptionId,
    }),
    value.canonical,
  );
  equal(
    expected.taskId(
      value.scope,
      value.runId,
      value.findingId,
      value.scopedOptionId,
    ),
    value.taskId,
  );
}
for (const value of await load("command-golden.json")) {
  equal(expected.canonical(value.payload), value.canonical);
  equal(
    expected.commandDigest(
      { scope: value.payload.scope, runId: value.payload.runId },
      value.payload.taskId,
      value.payload.actorId,
      value.payload.command,
    ),
    value.digest,
  );
}
console.log(
  "PASS V14 independent cross-language full source/identity/command oracle; checks=" +
    checks +
    "; no runtime acceptance claim",
);
