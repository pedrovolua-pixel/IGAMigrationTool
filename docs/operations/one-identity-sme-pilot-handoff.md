# One Identity Manager pilot: SME evidence handoff

Status: Request for evidence; no source collection authorized
Owner: One Identity SME with customer database owner for database execution approval
Last updated: 2026-09-30

This handoff closes the source-knowledge gaps in the approved [database evidence contract](../../specs/003-health-assessment/database-evidence-contract.md) and [pilot capability matrix](../../specs/003-health-assessment/capability-matrix.md). It applies separately to `PILOT-ENV-A` and `PILOT-ENV-B`. The environments must be independently administered sources with distinct evidence baselines; a clone, restore or second tenant of one source does not count as the second environment.

## What the One Identity SME needs to provide

1. **Exact source identity.** For each environment, provide a protected evidence reference for the exact One Identity Manager product version, build and hotfix, plus the installed module identifiers and exact module versions. Identify the approved product metadata source and when it was read. Mark each declared capability as installed, uninstalled, unsupported or still unknown. Record the SQL Server product and compatibility information needed to validate queries; do not put host names, network layout or credentials in this repository.
2. **Reviewed query-pack candidates.** For each exact build/module combination, identify the read-only metadata queries needed for the in-scope categories in the database evidence contract. Provide query ID/version, exact applicability, stable keyset ordering and page boundary, bound parameters, expected schema and native column names, maximum page/row/time bounds, and a versioned minimum-read permission set. Provide SQL text only through the approved protected review channel. Explain category gaps where no safe query is known. Do not treat a broad `10.x` query as verified.
3. **Field and relationship dictionary.** Map every proposed field to native table/object type, column/path, native UID where available, module, classification and approved purpose. Identify the smallest matching, ownership, approval, assignment, membership and relationship references needed. Mark general identity/account profiles, passwords, secret values, private keys, connection strings, government identifiers and other prohibited values for exclusion or reason-only markers. Show native relationship keys and how unresolved references are identified.
4. **Default and customization semantics.** For vendor defaults, customer-modified defaults and customer-created objects, identify the authoritative comparison and the actual/default fields. Supply sanitized fixtures for nulls, missing defaults, duplicate UIDs, broken references, unsupported customizations and changed values. State where the distinction cannot be established so the assessment can show incomplete evidence instead of guessing.
5. **Coverage and interpretation.** Review the declared capability families in the matrix, including installed custom connectors/modules. Confirm which categories are expected for each environment, which are not applicable because a module is absent, and which are inaccessible or unsupported. Review synthetic positive, negative, insufficient-evidence, exclusion and version-compatibility fixtures for the first rule set. Record any known schema or semantic differences across the two builds.

## What the customer database owner needs to provide

1. Written authorization for the dedicated read-only principal and the exact environment/query pack. Supply the effective permission evidence through a protected channel so the collector can distinguish minimum read, excess read-only and blocking write/DDL/ownership/administrative capabilities. No credential is required in this handoff.
2. An execution plan and impact review for each exact query/build combination, including approved isolation/locking behavior, page size, timeout, row cap, concurrency and material-impact stop threshold. A generic isolation hint or a local synthetic test is not approval for source execution.
3. An approved window and supervised test plan for bounded source validation, including cancellation, timeout, schema mismatch and impact-stop observation. Record the independently measured source impact and any query that must remain disabled.

## Handoff record and completion

Use protected artifact identifiers and SHA-256 digests in the capability row and review record. Keep customer data, SQL credentials, raw permission listings, SQL text and source topology out of Git and CI fixtures. For each environment, record the SME and database-owner names/roles, review dates, exact artifact versions, accepted gaps and limitations. The technical/security reviewers can then lock a query pack, field policy, normalization schema and fixture bundle to that exact environment.

This handoff is complete only when the two environment rows contain the required factual evidence and the exact query/field/permission/impact artifacts have been reviewed. The current local collector has no production source adapter, signed policy/pack loader, approved offline package or customer installer. Source testing, package exchange and G2 acceptance remain separate steps under the [feature implementation plan](../../specs/001-data-ingestion/implementation-plan.md).
