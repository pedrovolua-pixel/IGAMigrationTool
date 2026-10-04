import { readFileSync, writeFileSync } from "node:fs";

const contract = JSON.parse(
  readFileSync(new URL("./bff-v1.openapi.json", import.meta.url), "utf8"),
);
const outputUrl = new URL(
  "../../src/web/src/bff-authentication-contract.generated.ts",
  import.meta.url,
);
function typeOf(node) {
  if ("const" in node) return JSON.stringify(node.const);
  switch (node.type) {
    case "string":
      return "string";
    case "boolean":
      return "boolean";
    case "object":
      if (node.maxProperties === 0) return "Readonly<Record<string, never>>";
      return `{\n${Object.entries(node.properties)
        .map(
          ([name, value]) =>
            `  readonly ${name}${node.required.includes(name) ? "" : "?"}: ${typeOf(value)};`,
        )
        .join("\n")}\n}`;
    default:
      throw new Error(`Unsupported authentication schema type: ${node.type}`);
  }
}
const output =
  "// Generated from contracts/bff-authentication/bff-v1.openapi.json. Do not edit.\n" +
  "// Approved local D01/D02 contract; no production host activation or demo UI adoption.\n\n" +
  Object.entries(contract.components.schemas)
    .map(([name, value]) => `export type ${name} = ${typeOf(value)};\n`)
    .join("\n");
if (process.argv.includes("--check")) {
  if (readFileSync(outputUrl, "utf8") !== output)
    throw new Error("Generated BFF authentication types have drifted.");
} else writeFileSync(outputUrl, output);
