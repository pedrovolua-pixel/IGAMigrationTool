# Collector query-pack preflight v1

Status: Selected pilot engineering contract — 2026-10-03
Authority: Owner-approved release-readiness execution and AGENTS.md pilot decision exception.
Scope: M03 / Phase 1A; FR-HAS-1–6/28–29/47/50; AC-HAS-15/16; TP-HAS-015/016 and the approved [database evidence contract](../003-health-assessment/database-evidence-contract.md).

## Boundary

Add a pure protected in-memory preflight for an immutable query descriptor and trusted expected pack/policy/build bindings. This does not load a signed pack, connect to SQL, authorize source access, establish uniqueness or query impact, promote an artifact or make G2 pass. Keep the shipped collector adapter disabled. Preserve the historical StaticSqlShapeValidator contract; a separate stricter keyset validator adds the missing checkpoint-shape check.

## Metadata admission

Each candidate declares immutable pack ID/version, query ID/version, exact SQL UTF-8 SHA256, exact-build/module applicability, schema/table/category, explicit projected native fields with SQL type/nullability and existing FieldClassification, policy ID/version/digest, minimum-read-set ID/version, one stable checkpoint key with non-null type and opaque uniqueness-review evidence reference, page-size/boundary parameter names and typed non-null parameter declarations, positive page/total-row/duration budgets and an opaque exact-query review reference. Trusted expected bindings are separate inputs, not authority derived from the candidate. Reject missing/duplicate/malformed entries, wildcard builds/versions, digest/pack/policy mismatch, incompatible applicability, excluded/unclassified/prohibited/redacted projected values, extra or missing parameters, invalid types/budgets and an unprojected/nullable key. The strict v1 family has exactly two parameters: integer page size and a boundary whose SQL type matches the key.

Use the current field policy allowlist and exact-build matcher. Identifier/type syntax is conservatively bounded; unsupported type or query shapes return typed refusal rather than guessing. Structural key metadata and an opaque review reference do not prove live uniqueness, field semantics, reviewed grants, signature trust or safe source impact. Require the later source evidence gate to establish those facts.

## Strict keyset family

First require the historical single-table parameterized read-only shape. Then independently parse the AST and accept only explicit native column projection from the declared table, no projection aliases, DISTINCT, nested queries or extra clauses. Qualifiers must refer to the declared table or its single declared alias. All projected columns correspond one-to-one to the descriptor fields; the stable key occurs exactly once.

WHERE is exactly the stable key greater than the bound boundary parameter, permitting syntax-only parentheses. ORDER BY is exactly the same stable key ascending (explicit ASC or SQL's unspecified ascending default). TOP is exactly the bound page-size parameter. Reject inclusive/reversed/different-key predicates, OR/AND/literal additions, DESC, multiple/mismatched order keys, unknown qualifiers and missing/duplicated keys. This conservative family is intentionally narrower than the existing read-only shape validator; additional safe query families require separately documented contracts.

## Result and data handling

Return a closed metadata-only decision and success predicate that means structural/metadata preflight only. Do not return SQL, fields/values, native table/key names, protected references or parser diagnostics. No static/global mutable state, persistence, environment defaults, clock reads or side effects. Inputs are not mutated; descriptor types containing SQL must have safe ToString behavior. Snapshot caller collections before validating when required for consistent decisions.

## Paired test plan and evidence

- QP01 valid explicit and implicit ascending keyset, alias/qualified/bracketed identifiers and parentheses within the specified family.
- QP02 all historical static-SQL negative families and preserved historical tests.
- QP03 OR tautology, AND filter, wrong key/qualifier/order, DESC, multiple order keys, inclusive/reversed predicate, absent/duplicate/aliased key, DISTINCT and unsupported query family deny.
- QP04 missing/unknown/null/prohibited/redacted/excluded fields, duplicate descriptors, type/nullability and parameter mismatches deny before any executor is reachable.
- QP05 independently calculated exact SQL digest; changed SQL/digest/pack/policy/build/module/version/refuse cases; valid reference does not imply authority.
- QP06 zero/negative/inconsistent budgets and malformed metadata deny; exact boundary values are finite configured inputs, not invented production defaults.
- QP07 source collections/input bytes remain unchanged; malformed inputs return closed decisions without SQL/values in strings/errors.
- QP08 CollectorSafety historical tests, CollectorHost/architecture regressions, locked audited restore, formatting, Release build, scoped secret/diff/document checks.

Worker ownership: new QueryPackPreflight.cs and StrictKeysetSqlValidator.cs in CollectorSafety, new QueryPackPreflightChecks.cs and StrictKeysetSqlChecks.cs in CollectorSafety.Tests. Coordinator owns test Program wiring, shared configuration, this contract, canonical records and site. No dependency/version/configuration/migration change. Nonauthor review and coordinator combined execution are required before claiming this packet verified. G2/source/package/environment acceptance remain NOT VERIFIED.
