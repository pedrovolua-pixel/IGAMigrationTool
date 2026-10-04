import { readFileSync, writeFileSync } from "node:fs";

const schemaUrl = new URL("./demo-v1.schema.json", import.meta.url);
const outputUrl = new URL(
  "../../src/web/src/demo-contract.generated.ts",
  import.meta.url,
);
const schema = JSON.parse(readFileSync(schemaUrl, "utf8"));

function typeOf(node) {
  if (node.$ref) return node.$ref.split("/").at(-1);
  if ("const" in node) return JSON.stringify(node.const);
  if (node.enum)
    return node.enum.map((value) => JSON.stringify(value)).join(" | ");
  if (node.anyOf) return node.anyOf.map(typeOf).join(" | ");
  switch (node.type) {
    case "string":
      return "string";
    case "integer":
      return "number";
    case "boolean":
      return "boolean";
    case "null":
      return "null";
    case "array":
      return `ReadonlyArray<${typeOf(node.items)}>`;
    case "object":
      return `{\n${Object.entries(node.properties)
        .map(
          ([name, value]) =>
            `  readonly ${name}${node.required.includes(name) ? "" : "?"}: ${typeOf(value)};`,
        )
        .join("\n")}\n}`;
    default:
      throw new Error(`Unsupported schema type: ${node.type}`);
  }
}

const output =
  "// Generated from contracts/local-demo/demo-v1.schema.json. Do not edit.\n" +
  "// Private synthetic demo only; does not establish production HTTP approval.\n\n" +
  Object.entries(schema.$defs)
    .map(([name, value]) => `export type ${name} = ${typeOf(value)};\n`)
    .join("\n");
if (process.argv.includes("--check")) {
  if (readFileSync(outputUrl, "utf8") !== output)
    throw new Error("Generated demo contract types have drifted.");
} else {
  writeFileSync(outputUrl, output);
}
