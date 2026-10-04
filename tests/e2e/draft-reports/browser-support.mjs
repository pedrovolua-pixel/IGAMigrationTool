export async function until(
  read,
  predicate,
  description,
  milliseconds = 40000,
) {
  const end = Date.now() + milliseconds;
  while (Date.now() < end) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(description);
}
// Bounded traversal for the authoritative private DTO schema; not a general
// JSON Schema validator. Resource/revision/digest bindings are checked separately.
export function valid(value, rule, schema) {
  if (rule.$ref)
    return valid(value, schema.$defs[rule.$ref.split("/").at(-1)], schema);
  if (rule.anyOf)
    return rule.anyOf.some((child) => valid(value, child, schema));
  if ("const" in rule && value !== rule.const) return false;
  if (rule.enum && !rule.enum.includes(value)) return false;
  if (rule.type === "null") return value === null;
  if (rule.type === "string")
    return (
      typeof value === "string" &&
      (!rule.minLength || value.length >= rule.minLength) &&
      (!rule.maxLength || value.length <= rule.maxLength) &&
      (!rule.pattern || new RegExp(rule.pattern).test(value))
    );
  if (rule.type === "integer")
    return (
      Number.isInteger(value) &&
      (rule.minimum === undefined || value >= rule.minimum)
    );
  if (rule.type === "boolean") return typeof value === "boolean";
  if (rule.type === "array")
    return (
      Array.isArray(value) &&
      value.every((item) => valid(item, rule.items, schema))
    );
  if (rule.type === "object")
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      (rule.required ?? []).every((key) => key in value) &&
      (rule.additionalProperties !== false ||
        Object.keys(value).every((key) => key in rule.properties)) &&
      Object.entries(value).every(
        ([key, item]) =>
          !rule.properties[key] || valid(item, rule.properties[key], schema),
      )
    );
  return true;
}
