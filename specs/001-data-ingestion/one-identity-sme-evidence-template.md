# One Identity Manager pilot — SME evidence template

Status: Prefilled review template; no environment or query is verified
Use: Make one protected copy for `PILOT-ENV-A` and a separate copy for `PILOT-ENV-B`. The [partial Environment A response](one-identity-environment-a-sme-response.md) records what the supplied customization report supports.
Owner: One Identity Manager SME; customer database owner completes the database section.
Last updated: 2026-10-01

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

**A best-effort prefill:** The verified customization report confirms that the installed reporting tool can list configuration parameters, customization sections and schedules at its July snapshot. The live module rows support the A module classifications in section 4. Neither source confirms the exact `DialogTable`/`DialogColumn` columns, IT Shop activation, rule execution, or a safe collector read. Keep the candidate decisions below open until an SME checks the current source.

| Candidate to check | Public basis | SME decision / correction | Environment evidence ID |
|---|---|---|---|
| One Identity Manager has a 10.0 LTS documentation set; the pilot targets an exact 10.x build rather than the broad `10.x` label. | [V1]; repository capability matrix | [Confirm/Correct/Unknown] | [enter] |
| The One Identity Manager schema has table and column definitions; Designer's Schema Editor and documentation reports are candidate ways to inspect definitions and customizations. | [V2] is the public 9.3 guide; verify 10.0 behavior in the installed tools | [Confirm/Correct/Unknown] | [enter] |
| `DialogTable` and `DialogColumn` are candidate schema-metadata objects. Their exact columns, availability, and permitted read path must be confirmed for this build. | [V2] | **A:** Object/column-name existence confirmed by owner catalog read; meaning, classification and safe path still Unknown. **B:** Unknown. | [A provisional schema diagnostic](one-identity-environment-a-sme-response.md); B pending |
| IT Shop uses concepts including shelves, service items/products, approval policies and workflows. No table or field mapping is assumed. | [V3] is the public 9.2 guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Attestation policies are candidate governance objects with an attestation procedure, approval policy and schedule. | [V7] is the public 9.3 guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Compliance rule checks are a candidate operational category. Checks may be scheduled, automatic, or ad hoc; disabled rules are not checked. | [V4] 10.0 LTS | [Confirm/Correct/Not applicable] | [enter] |
| Assignment of system roles to business roles depends on the Business Roles Module being installed. | [V5] is a public earlier-version guide; verify 10.0 behavior | [Confirm/Correct/Not applicable] | [enter] |
| Job Queue and DBQueue are distinct process/queue concepts to consider for source-contained operational evidence. No queue payload is approved by this statement. | [V6] vendor support article applicable to 10.0 LTS | [Confirm/Correct/Not applicable] | [enter] |

## 3. Exact product and database compatibility

| Source-identification datum | SME response | Provenance / protected evidence ID |
|---|---|---|
| Exact One Identity Manager product version | **A:** The verified July report cover says `CCC` edition **10.0.0.287**. Current main database and a bounded version-view Installed Edition diagnostic report `STE` / `10.0`; its `CCC`/`QBM`/`DPR` modules report **10.0.0.287**. Exact installed application/build **NOT VERIFIED** pending interpretation and protected System information evidence. **B:** [enter full value]. | **A:** [partial response and provisional live metadata](one-identity-environment-a-sme-response.md); [vendor version-identification method](https://support.oneidentity.com/identity-manager/kb/4257755/how-to-identify-the-version-of-one-identity-manager). **B:** [product metadata source and capture time]. |
| Exact database schema build/version | **A:** `DialogDatabase.EditionVersion=10.0` and active module migration version `2025.0012.0001.0000` were observed; neither is yet confirmed as the schema build. **B:** [enter]. | **A:** [partial response](one-identity-environment-a-sme-response.md); authoritative schema-build source pending. **B:** [enter]. |
| Installed hotfixes/cumulative updates/transport packages relevant to schema or behavior | **A:** 15 permitted transport-history rows were all `Migration`; hotfix identifiers or verified none remain **Unknown**. **B:** [enter]. | **A:** [partial response](one-identity-environment-a-sme-response.md); SME statement pending. **B:** [enter]. |
| SQL Server product/build and database compatibility level | **A:** live Database Engine build **16.0.1121.4** and source compatibility **160** match the supplied values. **B:** [enter]. | **A:** [partial response and provisional live metadata](one-identity-environment-a-sme-response.md); formal database-owner evidence pending. **B:** [enter]. |
| Query-relevant collation or compatibility characteristic, if applicable | **A:** live source database collation **`SQL_Latin1_General_CP1_CI_AS`** matches the supplied value. **B:** [enter only if needed]. | **A:** [partial response and provisional live metadata](one-identity-environment-a-sme-response.md). **B:** [enter or not applicable]. |
| Known version/hotfix limitations, if any | [enter known limitation or unknown; no research required for intake] | [enter if available] |

## 4. Installed module and category inventory

For each proposed category, record **Installed**, **Not installed**, **Unsupported**, or **Unknown**, with exact module identifier and version where installed. Add relevant custom modules and connectors. An uninstalled module is `not_applicable`; an installed but unreadable module is a gap. These families come from the approved [capability matrix](../003-health-assessment/capability-matrix.md); the list is a scope prompt, not proof of installation. Unrelated families need no detailed row at intake.

For Environment A, the [partial response](one-identity-environment-a-sme-response.md) records 14 module IDs, display values, exact module versions, and migration versions. A bounded live read returned the same 14 active `QBMModuleDef` rows under a 100-row cap. The draft classifications below identify directly observed module rows only; feature activation, installed-inventory semantics, and relevant unlisted families remain open to SME correction. Environment B remains unfilled.

| Candidate family | State | Exact module ID/version or reason | Discovery evidence ID | Expected evidence categories / gaps |
|---|---|---|---|---|
| Identity Management Base, Target System Base, schema/configuration, authorization | **A:** installed module rows; authorization features unknown. **B:** unknown. | **A:** `QER 10.0.0.247`, `TSB 10.0.0.247`, `QBM 10.0.0.287`. | [A live module artifact in partial response](one-identity-environment-a-sme-response.md); B pending. | Exact objects, authorization configuration and read path unknown. |
| Business Roles and System Roles | **A:** installed module rows. **B:** unknown. | **A:** `RMB 10.0.0.245`, `RMS 10.0.0.245`. | [A live module artifact](one-identity-environment-a-sme-response.md); B pending. | Hierarchy and assignment objects unknown. |
| IT Shop and approval workflows | **A/B:** unknown at feature level. | A's `QER` row does not prove IT Shop activation. | [A partial response](one-identity-environment-a-sme-response.md); B pending. | Confirm active configuration and safe evidence path. |
| Attestation | **A:** installed module row. **B:** unknown. | **A:** `ATT 10.0.0.246`. | [A live module artifact](one-identity-environment-a-sme-response.md); B pending. | Policy and operational usage unknown. |
| Compliance/SoD, company policies, risk and mitigations | **A:** installed compliance/policy module rows; risk/mitigation feature details unknown. **B:** unknown. | **A:** `CPL 10.0.0.246`, `POL 10.0.0.246`. | [A live module artifact](one-identity-environment-a-sme-response.md); B pending. | Exact rules, policies and mitigations unknown. |
| Application Governance | **A/B:** unknown. | No direct A module-to-feature proof in the bounded inventory. | [A partial response](one-identity-environment-a-sme-response.md); B pending. | Confirm module/feature state before planning collection. |
| Account definitions, manage levels and templates | **A/B:** unknown at feature level. | A's `QER` and customization report do not establish complete usage. | [A partial response](one-identity-environment-a-sme-response.md); B pending. | Confirm exact objects, default/customized semantics and exclusions. |
| Synchronization and target-system metadata; installed AD, Entra, Exchange/Exchange Online connectors | **A:** `DPR`, `ADS`, `ARS` rows installed; Entra and Exchange family status unknown. **B:** unknown. | **A:** `DPR 10.0.0.287`, `ADS 10.0.0.247`, `ARS 10.0.0.247`. | [A live module artifact](one-identity-environment-a-sme-response.md); B pending. | Connector configuration, sync state and other connectors unknown. |
| Processes, scripts, custom code, compilation and consistency | **A:** customization report covers scripts, templates and process sections; live feature/health state unknown. **B:** unknown. | **A:** `CCC`/`QBM` rows; report pp. 19, 21, 40. | [A report and live module artifact](one-identity-environment-a-sme-response.md); B pending. | Exact code/definition fields, compilation status and safe projection unknown. |
| Job Queue, DBQueue, audit and operational health | **A/B:** unknown at operational-evidence level. | A's `QBM` row does not establish safe queue/audit reads. | [A partial response](one-identity-environment-a-sme-response.md); B pending. | Confirm bounded metadata, no payload values. |
| Reporting, archiving and password-management **configuration only** | **A:** report-subscription row installed; archiving/password-management status unknown. **B:** unknown. | **A:** `RPS 10.0.0.245`. | [A live module artifact](one-identity-environment-a-sme-response.md); B pending. | Definitions only; no generated report or secret values. |
| Custom modules and connectors — add one row per item | **A:** customer-configured-content row installed; specific custom module/connector status unknown. **B:** unknown. | **A:** `CCC 10.0.0.287`. | [A report and live module artifact](one-identity-environment-a-sme-response.md); B pending. | The report shows customization categories, not complete custom-connector semantics. |

## 5. Native object and field dictionary — before exact query-pack approval

The SME must supply the exact dictionary; public documentation and a column-name catalog are insufficient to preapprove SQL projections or field classifications. The Environment A database-owner catalog read confirmed `dbo.DialogTable` (60 columns) and `dbo.DialogColumn` (81 columns). The draft rows below name only a few observed columns and native types. Their keys, ordering, meaning, allowed values and relationship semantics require SME/owner review; no source row values were read for this prefill. For each eventual query row, attach protected evidence of the exact schema and any customization. Do not enter example customer values here.

| Category | Native table/view and object type | Module/build | Native UID/key and stable order | Proposed column/path | Meaning and assessment purpose | Classification and value rule | Relation target/key | Null/duplicate/custom behavior | Evidence ID |
|---|---|---|---|---|---|---|---|---|---|
| Schema inventory candidate | `dbo.DialogTable` observed in A; SME confirm safe view/query | A: `QBM 10.0.0.287` module row; object ownership unverified | `UID_DialogTable` candidate key; ordering unverified | `UID_DialogTable` (`varchar`) | Candidate table definition identifier; SME confirm | Unclassified; no value approved | [SME confirm] | Null, duplicates, customization unknown | [A provisional schema diagnostic](one-identity-environment-a-sme-response.md) |
| Schema inventory candidate | `dbo.DialogTable` observed in A; SME confirm safe view/query | A: `QBM` row; ownership unverified | Same candidate key | `TableName` (`varchar`) | Candidate native table name; SME confirm | Unclassified; no value approved | [SME confirm] | Null, duplicates, customization unknown | [A provisional schema diagnostic](one-identity-environment-a-sme-response.md) |
| Schema inventory candidate | `dbo.DialogColumn` observed in A; SME confirm safe view/query | A: `QBM` row; ownership unverified | `UID_DialogColumn` candidate key; ordering unverified | `UID_DialogColumn` (`varchar`) | Candidate column definition identifier; SME confirm | Unclassified; no value approved | [SME confirm] | Null, duplicates, customization unknown | [A provisional schema diagnostic](one-identity-environment-a-sme-response.md) |
| Schema inventory candidate | `dbo.DialogColumn` observed in A; SME confirm safe view/query | A: `QBM` row; ownership unverified | Same candidate key | `ColumnName` (`varchar`), `UID_DialogTable` (`varchar`), `UID_BaseColumn` (`varchar`) observed | Candidate name, table relation and base-column relation; SME confirm each | Unclassified; no value approved | Candidate `DialogTable`/base-column links; unverified | Null, broken, dynamic, custom behavior unknown | [A provisional schema diagnostic](one-identity-environment-a-sme-response.md) |
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
- **[V8]** [One Identity Support — how to identify the version of One Identity Manager](https://support.oneidentity.com/identity-manager/kb/4257755/how-to-identify-the-version-of-one-identity-manager). Identifies System information and `QBMVSystemOverview` as version-information sources. The view exists in A, but its generic Value field spans server/database categories and is not an approved restricted-principal projection.

If a vendor page changes or is inaccessible, the SME should record the exact guide version and protected copy/reference used. New public material may add candidate rows; it never substitutes for exact-build and customer database-owner evidence.
