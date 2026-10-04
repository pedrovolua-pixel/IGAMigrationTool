import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// Independent accepted D01 shape. Runtime checks use actual HTTPS separately.
const contract = JSON.parse(
  readFileSync(new URL("./bff-v1.openapi.json", import.meta.url), "utf8"),
);
assert.equal(contract.openapi, "3.1.0");
assert.deepEqual(Object.keys(contract.paths).sort(), [
  "/bff/v1/session",
  "/bff/v1/sign-in",
  "/bff/v1/sign-out",
]);
for (const [path, method, statuses, media] of [
  ["/bff/v1/session", "get", ["200", "400", "403", "405"], null],
  [
    "/bff/v1/sign-in",
    "post",
    ["302", "400", "403", "405", "409"],
    "application/x-www-form-urlencoded",
  ],
  [
    "/bff/v1/sign-out",
    "post",
    ["204", "400", "401", "403", "405"],
    "application/json",
  ],
]) {
  assert.deepEqual(Object.keys(contract.paths[path]), [method]);
  const operation = contract.paths[path][method];
  assert.deepEqual(Object.keys(operation.responses).sort(), statuses);
  assert.deepEqual(operation["x-query-fields"], []);
  for (const response of Object.values(operation.responses)) {
    assert.equal(response.headers["Cache-Control"].schema.const, "no-store");
  }
  if (media) {
    assert.deepEqual(Object.keys(operation.requestBody.content), [media]);
    assert.equal(operation.requestBody.required, true);
    assert.equal(operation["x-body-limit-bytes"], 8192);
  }
}
const schemas = contract.components.schemas;
assert.deepEqual(schemas.BffSession.required, [
  "schemaVersion",
  "authenticated",
  "csrfToken",
]);
assert.deepEqual(
  Object.keys(schemas.BffSession.properties),
  schemas.BffSession.required,
);
assert.equal(
  schemas.BffSession.properties.schemaVersion.const,
  "bff-session-v1",
);
assert.equal(schemas.BffSession.properties.authenticated.type, "boolean");
assert.equal(schemas.BffSession.properties.csrfToken.type, "string");
assert.equal(schemas.BffSession.additionalProperties, false);
assert.deepEqual(schemas.BffSignInForm.required, [
  "__RequestVerificationToken",
]);
assert.deepEqual(
  Object.keys(schemas.BffSignInForm.properties),
  schemas.BffSignInForm.required,
);
assert.equal(schemas.BffSignInForm.additionalProperties, false);
assert.equal(
  schemas.BffSignInForm.properties.__RequestVerificationToken.minLength,
  1,
);
assert.equal(
  schemas.BffSignInForm.properties.__RequestVerificationToken.maxLength,
  4096,
);
assert.equal(schemas.BffSignOutBody.maxProperties, 0);
assert.equal(schemas.BffSignOutBody.additionalProperties, false);
assert.equal(
  contract.paths["/bff/v1/sign-in"].post["x-fixed-return-path"],
  "/bff/v1/session",
);
assert.deepEqual(contract["x-framework-callbacks"], [
  "/bff/signin-oidc",
  "/bff/signout-callback-oidc",
  "/bff/signout-oidc",
]);
console.log(
  "PASS frozen accepted D01 OpenAPI route/status/schema/body/callback contract.",
);
