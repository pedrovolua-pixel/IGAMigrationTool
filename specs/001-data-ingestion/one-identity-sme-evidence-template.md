# One Identity Manager pilot — SME evidence template

Status: Prefilled review template; no environment or query is verified
Use: Make one protected copy for `PILOT-ENV-A` and a separate copy for `PILOT-ENV-B`. The [partial Environment A response](one-identity-environment-a-sme-response.md) records what the supplied customization report supports.
Owner: One Identity Manager SME; customer database owner completes the database section.
Last updated: 2026-09-30

## How to complete this template

No field in this template blocks local development with synthetic data. Complete the **source-identification minimum** below when an actual pilot environment is available. Sections 5–8 are needed only when proposing an exact query pack and preparing supervised source tests; section 9 is later validation and signoff evidence. Unknown or unavailable facts should be marked **Unknown** with the gap, not guessed. Do not fill fields for an absent module or a category that will not be collected.

For applicable prefilled candidates, enter **Confirm**, **Correct**, **Not applicable**, or **Unknown** and explain corrections. Add rows for relevant missing modules, objects or fields. A public guide identifies possible product features; it does not prove that a module, table, column, permission, or behavior exists in the exact pilot build. Use protected artifact IDs and SHA-256 digests for evidence. Do not put credentials, customer records, raw SQL, host names, network topology, permission dumps, or unredacted exports in Git. Put detailed source artifacts in the approved protected review location and record only their references here.

An intake response is complete when the source-identification minimum has a value or an explicit gap for each environment. Later review records must identify authorized reviewers and decisions in the protected evidence system; this template needs only the decision reference, not SME or database-owner names. Neither intake completion nor the later template by itself authorizes source access, promotes a query pack, validates a capability row, or accepts G2. The governing requirements are the [database evidence contract](../003-health-assessment/database-evidence-contract.md), [capability matrix](../003-health-assessment/capability-matrix.md), and [collector implementation plan](implementation-plan.md).

## Source-identification minimum — per environment, when available

| Required for exact-source planning | Why it is needed |
|---|---|
| `PILOT-ENV-A` or `PILOT-ENV-B` protected evidence ID and independence reference | Distinguish the two independently administered sources and baselines. |
| Exact One Identity product and database schema build, relevant hotfixes, and evidence reference | Select compatible query and mapping candidates for that installed build. |
| Installed module IDs and exact versions, plus absent/unsupported/unknown states for proposed categories | Decide which evidence categories apply and which are gaps. |
| SQL Server build and compatibility level relevant to the proposed queries, with evidence reference | Check query behavior and database-owner execution plans. |

The source-identification minimum does not require a person's name, organization, contact details, review date, full field dictionary, SQL text, query approval or scale result. Those later artifacts are requested only for the applicable gate below.

## 1. Environment evidence — one copy per environment

| Source-identification datum | Prefill / response |
|---|---|
| Pilot environment evidence ID | `PILOT-ENV-A` **or** `PILOT-ENV-B`: [choose one] |
| Independent source evidence | [protected artifact ID showing separate administration and baseline; a clone/restore or second tenant of one source does not qualify] |
| Source evidence reference and version/digest | [protected artifact ID and SHA-256; capture provenance stays with the protected artifact] |

## 2. Confirm the public starting points

These are **candidate product facts**, not statements about this environment. At intake, review only those relevant to the installed modules and proposed categories. Before using a candidate in a query pack or capability claim, check it against the exact installed release and record a correction if behavior differs. Public references are listed at the end.

| Candidate to check | Public basis | SME decision / correction | Environment evidence ID |
|---|---|---|---|
| One Identity Manager has a 10.0 LTS documentation set; the pilot targets an exact 10.x build rather than the broad `10.x` label. | [V1]; repository capability matrix | [Confirm/Correct/Unknown] | [enter] |
| The One Identity Manager schema has table and column definitions; Designer's Schema Editor and documentation reports are candidate ways to inspect definitions and customizations. | [V2] is the public 9.3 guide; verify 10.0 behavior in the installed tools | [Confirm/Correct/Unknown] | [enter] |
| `DialogTable` and `DialogColumn` are candidate schema-metadata objects. Their exact columns, availability, and permitted read path must be confirmed for this build. | [V2] | [Confirm/Correct/Unknown] | [enter] |
| IT Shop uses concepts including shelves, service items/products, approval policies and workflows. No table or field mapping is assumed. | [V3] is the public 9.2 guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Attestation policies are candidate governance objects with an attestation procedure, approval policy and schedule. | [V7] is the public 9.3 guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Compliance rule checks are a candidate operational category. Checks may be scheduled, automatic, or ad hoc; disabled rules are not checked. | [V4] 10.0 LTS | [Confirm/Correct/Not applicable] | [enter] |
| Assignment of system roles to business roles depends on the Business Roles Module being installed. | [V5] is a public earlier-version guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Job Queue and DBQueue are distinct process/queue concepts to consider for source-contained operational evidence. No queue payload is approved by this statement. | [V6] vendor support article applicable to 10.0 LTS | [Confirm/Correct/Not applicable] | [enter] |

## 3. Exact product and database compatibility

| Source-identification datum | SME response | Provenance / protected evidence ID |
|---|---|---|
| Exact One Identity Manager product version | **A:** report cover states **Edition version 10.0.0.287**; current installed product/build still to confirm. **B:** [enter full value]. | **A:** [partial response](one-identity-environment-a-sme-response.md), report p. 1 and SHA-256; current-source confirmation pending. **B:** [product metadata source and capture time]. |
| Exact database schema build/version | [enter full value; explain any difference from client version] | [enter] |
| Installed hotfixes/cumulative updates/transport packages relevant to schema or behavior | [list exact identifiers and order, or none verified] | [enter] |
| SQL Server product/build and database compatibility level | **A:** owner supplied SQL Server 2022 Database Engine build **16.0.1121.4**; database compatibility level [enter]. **B:** [enter]. | **A:** [partial response](one-identity-environment-a-sme-response.md); protected source/database-owner evidence pending. **B:** [enter]. |
| Query-relevant collation or compatibility characteristic, if applicable | [enter only if needed for exact query behavior; no topology] | [enter or not applicable] |
| Known version/hotfix limitations, if any | [enter known limitation or unknown; no research required for intake] | [enter if available] |

## 4. Installed module and category inventory

For each proposed category, record **Installed**, **Not installed**, **Unsupported**, or **Unknown**, with exact module identifier and version where installed. Add relevant custom modules and connectors. An uninstalled module is `not_applicable`; an installed but unreadable module is a gap. These families come from the approved [capability matrix](../003-health-assessment/capability-matrix.md); the list is a scope prompt, not proof of installation. Unrelated families need no detailed row at intake.

| Candidate family | State | Exact module ID/version or reason | Discovery evidence ID | Expected evidence categories / gaps |
|---|---|---|---|---|
| Identity Management Base, Target System Base, schema/configuration, authorization | [enter] | [enter] | [enter] | [enter] |
| Business Roles and System Roles | [enter] | [enter] | [enter] | [enter] |
| IT Shop and approval workflows | [enter] | [enter] | [enter] | [enter] |
| Attestation | [enter] | [enter] | [enter] | [enter] |
| Compliance/SoD, company policies, risk and mitigations | [enter] | [enter] | [enter] | [enter] |
| Application Governance | [enter] | [enter] | [enter] | [enter] |
| Account definitions, manage levels and templates | [enter] | [enter] | [enter] | [enter] |
| Synchronization and target-system metadata; installed AD, Entra, Exchange/Exchange Online connectors | [enter] | [enter] | [enter] | [enter] |
| Processes, scripts, custom code, compilation and consistency | [enter] | [enter] | [enter] | [enter] |
| Job Queue, DBQueue, audit and operational health | [enter] | [enter] | [enter] | [enter] |
| Reporting, archiving and password-management **configuration only** | [enter] | [enter] | [enter] | [enter] |
| Custom modules and connectors — add one row per item | [enter] | [enter] | [enter] | [enter] |

## 5. Native object and field dictionary — before exact query-pack approval

The SME must supply the exact dictionary; public documentation is insufficient to preapprove SQL projections or field classifications. Start with `DialogTable` and `DialogColumn` as **candidate metadata objects only** if confirmed in section 2. For each row, attach protected evidence of the exact schema and any customization. Do not enter example customer values here.

| Category | Native table/view and object type | Module/build | Native UID/key and stable order | Proposed column/path | Meaning and assessment purpose | Classification and value rule | Relation target/key | Null/duplicate/custom behavior | Evidence ID |
|---|---|---|---|---|---|---|---|---|---|
| Schema inventory candidate | `DialogTable` **SME confirm** | [enter] | [enter] | [enter] | [enter] | [approve/reference, marker-only, excluded, prohibited] | [enter] | [enter] | [enter] |
| Schema inventory candidate | `DialogColumn` **SME confirm** | [enter] | [enter] | [enter] | [enter] | [approve/reference, marker-only, excluded, prohibited] | [enter] | [enter] | [enter] |
| [next category] | [enter] | [enter] | [enter] | [enter] | [enter] | [enter] | [enter] | [enter] | [enter] |

For each object, also record: expected row count/range; whether it is a vendor default, modified default, customer-created object, or unknown; the method and evidence for comparing default with actual; supported relationship edges; unresolved-reference behavior; and any fields whose classification cannot be established. Mark unknown or prohibited values as no-value reason markers. General identity/account profiles and secret/government-identifier values are outside the approved payload.

## 6. Exact read-only query-pack review — before exact query-pack approval

**Do not paste executable SQL in this template.** Each query needs a separately protected artifact, static validation, SME review, and customer database-owner plan/permission approval before it can enter a promoted pack. A public example query or an unbounded `SELECT` is not an approved collector query.

| Required query datum | SME proposal / artifact reference |
|---|---|
| Query ID, version and content SHA-256 | [enter] |
| Category/object and exact source build/hotfix | [enter] |
| Required installed module ID and exact version, or core-only | [enter] |
| Native tables/views and approved projected columns | [enter; map every output to section 5] |
| Stable ordering key and keyset page boundary | [enter; show null/tie behavior] |
| Bound parameter names, types and purpose | [enter; no text interpolation] |
| Count/empty-page behavior and repeatability assumption | [enter] |
| Minimum-read object/permission set identifier | [enter; database owner confirms effective principal] |
| Maximum page size, rows, duration, concurrency and local staging estimate | [enter; may narrow approved maxima] |
| Timeout, cancel, disconnect, drift and impact-stop behavior | [enter] |
| Exact expected result schema, native types, nullability and mapping version | [enter] |
| Static SQL safety result and test evidence ID | [enter] |
| SME decision, date and limitation | [Confirm/Correct/Reject/Pending; enter] |

## 7. Defaults, customizations, relationships and fixtures — before applicable rule/gate review

Provide **sanitized synthetic fixtures**, never customer records, for every applicable category. State whether each outcome is expected to be assessed, a typed gap, or not applicable. Record fixture IDs and expected native keys/relationships without sensitive values.

| Required case | Expected behavior / SME correction | Fixture ID |
|---|---|---|
| Vendor default with matching actual value | [enter exact provenance and comparison rule] | [enter] |
| Customer-modified vendor default | [enter actual/default linkage and precedence] | [enter] |
| Customer-created object or extension | [enter classification and semantic-analysis limit] | [enter] |
| Missing vendor baseline / unverifiable default | [explicit incomplete evidence] | [enter] |
| Null, empty, malformed, duplicate native UID | [enter separate expected outcomes] | [enter] |
| Broken, dynamic or multi-target relation | [enter unresolved-reference marker and expected count] | [enter] |
| Prohibited, redacted and unknown-classification field | [no value staged; enter marker/quarantine expectation] | [enter] |
| Uninstalled, inaccessible, unsupported or partially collected module/category | [distinct state and user-visible gap] | [enter] |
| Timeout, cancellation, row cap, changed page digest, schema drift | [checkpoint/gap/conflict outcome] | [enter] |

## 8. Customer database-owner approval — before supervised source testing

| Required datum | Owner response / protected evidence ID |
|---|---|
| Written authorization for this exact environment, principal and query-pack version | [enter decision, date, scope and expiry] |
| Dedicated principal evidence ID and authentication mode | [reference only; no credential] |
| Effective minimum-read capabilities by query/object | [enter versioned set and evidence ID] |
| Excess read-only capability, if any | [categories, warning/audit decision; no raw grant dump here] |
| Effective write, DDL, ownership, impersonation or administrative capability | [none verified / blocking category; any present blocks collection] |
| Execution plan and index/access-path evidence per query/build | [one evidence ID per query] |
| Reviewed isolation/locking behavior | [per query; no generic hint approval] |
| Approved page/row/time/concurrency limits | [per query and aggregate run] |
| Material-impact threshold and stop signal | [numeric/observable threshold, method and owner] |
| Supervised source-test window and rollback/stop contact | [enter] |
| Query approval or rejection with date | [enter] |

## 9. Coverage, scale and signoff — later G2/G8 evidence

| Required result | Response / evidence ID |
|---|---|
| Every supported in-scope category/object has a requested, completed, partial, inaccessible, excluded, redacted, unsupported, malformed or error outcome | [coverage manifest version and gaps] |
| Applicable named scale categories independently exercised at 100,000 records with bounded source impact, or explicit limitation | [test IDs, counts, duration and impact; no values] |
| Exact build/module/query/field-policy/normalization/rule versions locked together | [versioned manifest/digests] |
| SME summary of corrected public assumptions and newly discovered categories | [enter] |
| SME review decision and unresolved items | [protected decision artifact ID; reviewer identity and time stay in that record] |
| Database-owner review decision and unresolved items | [protected decision artifact ID; reviewer identity and time stay in that record] |
| Technical/security review decision and capability-matrix row update | [pending or protected decision artifact ID] |

## Public documentation used for the prefill

- **[V1]** [One Identity Manager 10.0 LTS technical documentation index](https://support.oneidentity.com/identity-manager/10.0%20lts/technical-documents). This establishes the published documentation set, not an environment's installed build.
- **[V2]** [One Identity Manager 9.3 Configuration Guide — table definitions](https://docs.oneidentity.com/bundle/one-identity-manager_configuration_9.3/page/sources/config/schema/dbmdialogtableintro.html) and [9.3 column definitions](https://support.oneidentity.com/technical-documents/identity-manager/9.3/configuration-guide/10). These establish public schema concepts and candidate metadata names; the SME must verify the exact 10.x build.
- **[V3]** [One Identity Manager 9.2 IT Shop Administration Guide](https://support.oneidentity.com/technical-documents/identity-manager/9.2/it-shop-administration-guide). Used only to seed IT Shop concepts; exact 10.x objects and columns remain open.
- **[V4]** [One Identity Manager 10.0 LTS — checking compliance rules](https://docs.oneidentity.com/bundle/one-identity-manager_compliance-rules_10.0/page/sources/cpl/cplrulecheck.html). Used only to seed the compliance/rule-check category.
- **[V5]** [One Identity Manager system roles — assigning system roles to business roles](https://docs.oneidentity.com/bundle/one-identity-manager_system-roles_9.2/page/sources/rms/esetassignbusinessroles.htm). This public earlier-version page states the Business Roles Module condition; confirm for the installed build.
- **[V6]** [One Identity support — Job Queue and DBQueue upgrade check](https://support.oneidentity.com/identity-manager/kb/4342771/compilation-error-the-job-queue-and-or-the-dbqueue-is-not-empty). Used only to seed distinct operational categories; it does not identify safe collector tables or values.
- **[V7]** [One Identity Manager 9.3 Attestation Administration Guide — attestation policies](https://support.oneidentity.com/technical-documents/identity-manager/9.3/attestation-administration-guide/10). Used to seed policy concepts; the installed 10.x build and exact fields remain open.

If a vendor page changes or is inaccessible, the SME should record the exact guide version and protected copy/reference used. New public material may add candidate rows; it never substitutes for exact-build and customer database-owner evidence.
