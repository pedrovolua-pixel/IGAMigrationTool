# Environment A — partial One Identity SME response

Status: Report-backed and owner-supplied intake; exact source eligibility and query approval NOT VERIFIED

Source: One Identity Manager Customization Documentation, generated 2026-07-08, 40 pages

Source SHA-256: `0e021547b83ac8a60c4dcabb2338176359d0698bd4d7e08585f61f6bca39df5d`

Last updated: 2026-09-30

This response applies the [SME evidence template](one-identity-sme-evidence-template.md) to the report supplied for `PILOT-ENV-A`. The report remains outside this public repository. Its protected artifact ID and confirmation that it represents the current, independently administered pilot source are still needed. Page numbers below refer to the supplied report, not to an approved query pack. Customer identifiers, configuration values, source code, and record contents are not reproduced here.

## Section 1 — environment evidence

| Datum | Response | Remaining confirmation |
|---|---|---|
| Pilot environment | `PILOT-ENV-A`, as designated by the repository owner | Protected source identifier and report-to-source binding pending. |
| Reported staging level | Development system (cover, p. 1) | Confirm current source and whether this is the intended independently administered pilot environment. |
| Independence | Unknown | Evidence that A and B are independently administered sources with distinct baselines. |
| Report provenance | Generated 2026-07-08; digest above | Protected artifact ID, capture method, and confirmation the report reflects the current source. |

## Section 2 — public starting points and report scope

The report includes activated configuration parameters (pp. 2–17), a custom tables/columns section (p. 18), custom templates (p. 19), formatting rules (p. 20), custom scripts (p. 21), SQL extensions (p. 22), activated schedules (pp. 23–39), and a custom processes section (p. 40). At the report snapshot, it explicitly reports **no custom tables or columns** (p. 18), **no custom formatting rules** (p. 20), **no custom SQL extensions** (p. 22), and **no custom processes** (p. 40). It shows a customized template and lab scripts (pp. 19, 21). These are report findings, not proof that every possible customization was captured or that the source has not changed since generation.

The report supports customization-inventory review and category discovery. It does not establish a complete installed-module inventory, a vendor-default comparison baseline, a safe read path, or approval of any source query. The SME still confirms or corrects the applicable public candidates in the template against the installed build.

## Section 3 — exact product and database compatibility

| Source-identification datum | Report-backed response | Status / next evidence |
|---|---|---|
| One Identity Manager version | **10.0.0.287** is printed as **“Edition version”** on the cover (p. 1). | Report-stated version recorded. SME confirms it is the current installed product/build for A. |
| Database schema build/version | Not separately established by this customization report. | Exact database schema build and its metadata source required. |
| Relevant hotfixes/cumulative updates/transport packages | Not established by this report. | Exact identifiers or a verified none statement required. |
| SQL Server product and Database Engine build | Repository owner supplied **SQL Server 2022**, build **16.0.1121.4**, and `sqlservr.exe` file version **2022.160.1121.4** on 2026-09-30. | Supplied value recorded; database-owner/source evidence reference pending. |
| SQL Server servicing reference | Repository owner supplied GDR name **GDR** and Knowledge Base number **KB5040936** on 2026-09-30. | Record as supplied; servicing applicability/evidence pending. |
| Database compatibility level | Repository owner supplied **SQL Server 2022 (160)** for Environment A databases on 2026-09-30. | Confirm the One Identity source database specifically reports level **160**; protected evidence reference pending. |
| Analysis Services version (supplemental) | Repository owner supplied build **16.0.42.216** and `msmdsrv.exe` file version **2022.160.42.216** on 2026-09-30. | Preserved as context; it does not supply the Database Engine compatibility level. |
| Query-relevant collation/compatibility | Repository owner supplied default case-insensitive sort/collation **`SQL_Latin1_General_CP1_CI_AS`** on 2026-09-30. | Confirm whether this is the source database's collation or only a server/default setting before using it for exact queries. |

The cover value is in the approved pilot's **10.x** family. It does not by itself prove current source identity, exact schema compatibility, module versions, or G2/G8 eligibility. One Identity's [configuration guide](https://support.oneidentity.com/technical-documents/identity-manager/9.1.2/configuration-guide/customizing-the-one-identity-manager-base-configuration/changing-database-connection-data) describes “Edition version” as the edition's version number. The SQL, compatibility, collation and Analysis Services values above are owner-supplied facts, not values extracted from the customization report; their source-database binding still needs evidence.

## Section 4 — installed module and category inventory

The repository owner supplied the following 14 Environment A module rows on 2026-09-30. They are **reported installed**, not yet reconciled to a protected source inventory. All rows have the supplied migration version `2025.0012.0001.0000`. The list's completeness and the state of modules not listed remain **Unknown**; absence must not be inferred.

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

1. Confirm the report's protected artifact ID, its binding to the current `PILOT-ENV-A` source, and independence from `PILOT-ENV-B`.
2. Confirm **10.0.0.287** as the current installed product/build and supply the exact database schema build and relevant hotfixes.
3. Confirm the 14 supplied module rows against a current protected source inventory, including their migration versions, and classify relevant unlisted modules/categories as installed, not installed, unsupported, or unknown.
4. Confirm that compatibility level **160** and collation **`SQL_Latin1_General_CP1_CI_AS`** apply to the One Identity source database, and supply protected evidence for those values, the supplied SQL Server build and its servicing reference. Query and database-owner evidence follows for the confirmed exact build and applicable modules.
