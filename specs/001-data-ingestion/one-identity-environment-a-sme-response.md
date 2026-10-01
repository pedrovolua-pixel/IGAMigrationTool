# Environment A — partial One Identity SME response

Status: Report-backed, owner-supplied and SME-confirmed intake; exact source eligibility and query approval NOT VERIFIED

Source: One Identity Manager Customization Documentation, generated 2026-07-08, 40 pages

Source SHA-256: `0e021547b83ac8a60c4dcabb2338176359d0698bd4d7e08585f61f6bca39df5d`

Last updated: 2026-09-30

This response applies the [SME evidence template](one-identity-sme-evidence-template.md) to the report supplied for `PILOT-ENV-A`. The report remains outside this public repository. On 2026-09-30, the SME confirmed that the report is bound to the current Environment A source and that A is independent from B. The protected artifact ID and independence evidence reference were not included in the response. Page numbers below refer to the supplied report, not to an approved query pack. Customer identifiers, configuration values, source code, and record contents are not reproduced here.

## Section 1 — environment evidence

| Datum | Response | Remaining confirmation |
|---|---|---|
| Pilot environment | `PILOT-ENV-A`, as designated by the repository owner; SME confirmed the report is bound to the current A source on 2026-09-30. | Protected source/report artifact ID and capture reference pending. |
| Reported staging level | Development system (cover, p. 1) | SME confirmed this is the current A source; source evidence reference pending. |
| Independence | SME confirmed A is independent from B on 2026-09-30. | Protected evidence showing separate administration and distinct baselines pending. |
| Report provenance | Generated 2026-07-08; digest above | Protected artifact ID and capture method pending. |

## Section 2 — public starting points and report scope

The report includes activated configuration parameters (pp. 2–17), a custom tables/columns section (p. 18), custom templates (p. 19), formatting rules (p. 20), custom scripts (p. 21), SQL extensions (p. 22), activated schedules (pp. 23–39), and a custom processes section (p. 40). At the report snapshot, it explicitly reports **no custom tables or columns** (p. 18), **no custom formatting rules** (p. 20), **no custom SQL extensions** (p. 22), and **no custom processes** (p. 40). It shows a customized template and lab scripts (pp. 19, 21). These are report findings, not proof that every possible customization was captured or that the source has not changed since generation.

The report supports customization-inventory review and category discovery. It does not establish a complete installed-module inventory, a vendor-default comparison baseline, a safe read path, or approval of any source query. The SME still confirms or corrects the applicable public candidates in the template against the installed build.

## Section 3 — exact product and database compatibility

| Source-identification datum | Report-backed response | Status / next evidence |
|---|---|---|
| One Identity Manager version | **10.0.0.287** is printed as **“Edition version”** on the cover (p. 1); SME confirmed it is the current installed product/build on 2026-09-30. | Product/build claim SME-confirmed; protected current-source metadata reference pending. |
| Database schema build/version | The SME replied “confirm” to the combined product/schema request but did not provide a separate schema-build value or state that it equals `10.0.0.287`. | Exact database schema build and metadata source still required. |
| Relevant hotfixes/cumulative updates/transport packages | The SME replied “confirm” to the combined request but supplied no hotfix identifiers or explicit “none” statement. | Exact identifiers or a verified none statement still required. |
| SQL Server product and Database Engine build | Repository owner supplied **SQL Server 2022**, build **16.0.1121.4**, and `sqlservr.exe` file version **2022.160.1121.4**; SME confirmed these apply to A on 2026-09-30. | SME-confirmed; database-owner/source evidence reference pending. |
| SQL Server servicing reference | Repository owner supplied GDR name **GDR** and Knowledge Base number **KB5040936**; SME confirmed the supplied servicing reference for A on 2026-09-30. | SME-confirmed; servicing evidence reference pending. |
| Database compatibility level | Repository owner supplied **SQL Server 2022 (160)**; SME confirmed level **160** applies to the One Identity source database on 2026-09-30. | SME-confirmed; protected database-setting evidence reference pending. |
| Analysis Services version (supplemental) | Repository owner supplied build **16.0.42.216** and `msmdsrv.exe` file version **2022.160.42.216** on 2026-09-30. | Preserved as context; it does not supply the Database Engine compatibility level. |
| Query-relevant collation/compatibility | Repository owner supplied default case-insensitive sort/collation **`SQL_Latin1_General_CP1_CI_AS`**; SME confirmed it applies to the One Identity source database on 2026-09-30. | SME-confirmed; protected database-setting evidence reference pending. |

The cover value is in the approved pilot's **10.x** family. SME confirmation resolves the conversational source binding for the supplied product and SQL values, but protected references and the separate schema/hotfix facts remain open. These claims do not establish G2/G8 eligibility. One Identity's [configuration guide](https://support.oneidentity.com/technical-documents/identity-manager/9.1.2/configuration-guide/customizing-the-one-identity-manager-base-configuration/changing-database-connection-data) describes “Edition version” as the edition's version number. The SQL, compatibility, collation and Analysis Services values above were supplied separately from the customization report.

## Section 4 — installed module and category inventory

The repository owner supplied the following 14 Environment A module rows on 2026-09-30. The SME confirmed they were queried directly from the current A database, including their migration versions. The protected query result/artifact reference was not supplied. All rows have migration version `2025.0012.0001.0000`. The list's completeness and the state of modules not listed remain **Unknown**; absence must not be inferred.

| Module ID | Display value supplied | Module version | Migration version |
|---|---|---|---|
| `ADS` | Active Directory Module | `10.0.0.247` | `2025.0012.0001.0000` |
| `ARS` | Active Roles Module | `10.0.0.247` | `2025.0012.0001.0000` |
| `ATT` | Attestation Module | `10.0.0.246` | `2025.0012.0001.0000` |
| `CAP` | Governance Base Module | `10.0.0.247` | `2025.0012.0001.0000` |
| `CCC` | Customer configured content | `10.0.0.287` | `2025.0012.0001.0000` |
| `CPL` | Compliance Rules Module | `10.0.0.246` | `2025.0012.0001.0000` |
| `DPR` | Target System Synchronization Module | `10.0.0.287` | `2025.0012.0001.0000` |
| `POL` | Company Policies Module | `10.0.0.246` | `2025.0012.0001.0000` |
| `QBM` | Configuration Module | `10.0.0.287` | `2025.0012.0001.0000` |
| `QER` | Identity Management Base Module | `10.0.0.247` | `2025.0012.0001.0000` |
| `RMB` | Business Roles Module | `10.0.0.245` | `2025.0012.0001.0000` |
| `RMS` | System Roles Module | `10.0.0.245` | `2025.0012.0001.0000` |
| `RPS` | Report Subscription Module | `10.0.0.245` | `2025.0012.0001.0000` |
| `TSB` | Target System Base Module | `10.0.0.247` | `2025.0012.0001.0000` |

These rows give candidate module-to-capability leads for governance, roles, attestation, compliance, target synchronization, Active Directory/Active Roles, report subscriptions, and customer configuration. They do not confirm that every feature within those families is enabled, readable, or in scope. IT Shop, Application Governance, Entra, Exchange, archival, password-management and other unlisted families remain to be classified by the SME as installed, not installed, unsupported, or unknown. The report's customization names and code remain in protected evidence; any proposed collector field still needs classification and an exact-build query review.

## Sections 5–9 — later source and gate review

Native object/field dictionary, default-to-actual comparison, sanitized fixtures, bounded read-only query pack, effective database permissions, execution plans, source impact, coverage, scale, and reviewer decisions remain pending. This report is an input to those reviews, not an authorization to query or a G2/G8 acceptance artifact.

## SME items needed next

1. Provide protected artifact references for the report/current-source binding, A/B independence, direct database module query, and confirmed SQL product/settings/servicing values.
2. Provide the exact database schema build and relevant hotfix identifiers, or an explicit verified “none” for hotfixes; these values were not supplied in the SME's confirmation.
3. Classify relevant unlisted modules/categories as installed, not installed, unsupported, or unknown, and state whether the 14-row query is a complete installed-module inventory.
4. Continue exact-build query-pack, field, permission and database-owner reviews only through their later gates; the confirmations above do not authorize source collection.

## Source-access preparation

On 2026-09-30, the repository owner, acting as the Environment A database owner, authorized bounded read-only metadata discovery for the named lab database. Network reachability was confirmed, but the SQL principal offered for access has administrative authority. The approved collector boundary blocks administrative principals before source queries. No database authentication or source query was attempted. A dedicated read-only principal, its effective-permission evidence, and a protected record of the approved discovery scope are needed before metadata discovery can begin. Connection details and credentials remain outside this repository.
