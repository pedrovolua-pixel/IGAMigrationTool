# Environment A — partial One Identity SME response

Status: Report and live metadata reconciled where possible; exact build, source eligibility and query approval NOT VERIFIED

Source: One Identity Manager Customization Documentation, generated 2026-07-08, 40 pages

Source SHA-256: `0e021547b83ac8a60c4dcabb2338176359d0698bd4d7e08585f61f6bca39df5d`

The mounted 40-page PDF was re-opened on 2026-10-01. Its first-page text identifies `CCC` edition version `10.0.0.287`, Main Database, and Development system; the current file digest matches the value above. This verifies which supplied PDF was read, not that its July snapshot equals the current database.

Last updated: 2026-10-01

This response applies the [SME evidence template](one-identity-sme-evidence-template.md) to the report supplied for `PILOT-ENV-A` and a bounded live metadata read on 2026-10-01. The report remains outside this public repository. On 2026-09-30, the SME confirmed that the report is bound to the current Environment A source and that A is independent from B. The protected report ID and independence evidence reference were not included. The live metadata raises an edition/build discrepancy that requires SME reconciliation before this source can be eligible. Page numbers below refer to the supplied report, not to an approved collection query pack. Customer identifiers, connection details, configuration values, source code, and record contents are not reproduced here.

The owner-only local diagnostic artifacts are `PILOT-ENV-A-LAB-METADATA-20261001` (SHA-256 `7ea3e77998b2ea5dc2c05692f8285b471a91f9baa84e1ce066ba4b7cdc96d8c5`) and `PILOT-ENV-A-LAB-PERMISSION-20261001` (SHA-256 `4f5988e4b993a8a9bc7f0f58dd872d8bc8959fd2be2f69f49795d666a194592c`). They are provisional local evidence, not immutable restricted-store or G2/G8 acceptance artifacts.

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
| One Identity Manager version | The July report cover calls `CCC` edition version **10.0.0.287**, and the SME confirmed that as the installed product/build on 2026-09-30. The live main `DialogDatabase` row instead reports edition `STE`, edition version `10.0`, and customer prefix `CCC`. The live `CCC`, `DPR`, and `QBM` module versions are **10.0.0.287**. | The cover and database fields have different meanings or snapshots; the exact installed product/build remains **NOT VERIFIED** until the SME identifies the authoritative build source and reconciles them. |
| Database schema build/version | The live main `DialogDatabase` row reports `EditionVersion=10.0` and a migration time on 2026-10-01; the 14 active module rows have migration version `2025.0012.0001.0000`. Neither value was established as a separate database schema build. | Exact database schema build and its authoritative metadata source still required. |
| Relevant hotfixes/cumulative updates/transport packages | The live bounded read found 15 `QBMTransportHistory` rows, all of type `Migration`, and no separate hotfix identifier in the permitted fields. The SME supplied no hotfix identifiers or verified “none” statement. | Hotfix/package inventory and applicability remain **Unknown**; absence is not inferred from this history slice. |
| SQL Server product and Database Engine build | Repository owner supplied **SQL Server 2022**, build **16.0.1121.4**, and `sqlservr.exe` file version **2022.160.1121.4**. The live Database Engine reports **16.0.1121.4** and Developer Edition. | Engine build directly matched; file version remains owner-supplied and unverified by this database read. |
| SQL Server servicing reference | Repository owner supplied GDR name **GDR** and Knowledge Base number **KB5040936**. The live server reports update reference **KB5040936**, product level `RTM`, and no `ProductUpdateLevel` value. | KB reference directly matched; the GDR label requires separate servicing evidence. |
| Database compatibility level | Repository owner supplied **SQL Server 2022 (160)**; the live source database reports level **160**. | Directly matched in provisional local metadata evidence. |
| Analysis Services version (supplemental) | Repository owner supplied build **16.0.42.216** and `msmdsrv.exe` file version **2022.160.42.216** on 2026-09-30. | Preserved as context; it does not supply the Database Engine compatibility level. |
| Query-relevant collation/compatibility | Repository owner supplied default case-insensitive sort/collation **`SQL_Latin1_General_CP1_CI_AS`**; the live source database reports the same collation. | Directly matched in provisional local metadata evidence. |

The cover and live module values are in the approved pilot's **10.x** family, but they do not establish the exact installed product build. The different `CCC`/`STE` edition labels and `10.0.0.287`/`10.0` version values must be explained by the SME against the current source and report generation method. The live migration timestamp is later than the report generation date, so the report cannot by itself prove the current source state. These claims do not establish G2/G8 eligibility. One Identity's [configuration guide](https://support.oneidentity.com/technical-documents/identity-manager/9.1.2/configuration-guide/customizing-the-one-identity-manager-base-configuration/changing-database-connection-data) describes “Edition version” as the edition's version number. The SQL, compatibility, collation and Analysis Services values above were supplied separately from the customization report.

One Identity's [version-identification article](https://support.oneidentity.com/identity-manager/kb/4257755/how-to-identify-the-version-of-one-identity-manager) directs operators to **Help → Info → System information** and identifies `QBMVSystemOverview` as a version/database information source. A bounded, field-reviewed read of that view or a protected export from System information is the next candidate to reconcile the current product and database version. The view has not yet been queried under the dedicated metadata principal, and the article does not prove which of the conflicting A values is authoritative. Its fields and any export must be reviewed for prohibited topology or connection data before retention.

## Section 4 — installed module and category inventory

The repository owner supplied the following 14 Environment A module rows on 2026-09-30. The SME confirmed they were queried directly from the current A database, including their migration versions. A bounded live read of active `QBMModuleDef` rows on 2026-10-01 returned exactly these 14 rows under a 100-row cap, with no additional active row at that snapshot. All have migration version `2025.0012.0001.0000`. The table's authoritative installed-module semantics and classifications for unlisted capability families still need SME review; an absent family is not inferred to be uninstalled.

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

### Best-effort category classification from the 14 live rows

`Installed` below means only that an active module row with that ID and version appeared in the bounded snapshot. It does **not** imply that every feature, connector, configuration item or health rule in that category is active or approved for collection. `Unknown` is retained where no direct row or configuration proof is available. These are evidence-based draft classifications for SME correction, not SME signoff.

| Candidate category | Current A classification | Basis / remaining distinction |
|---|---|---|
| Identity Management Base, Target System Base, configuration, synchronization | Installed module rows | `QER`, `TSB`, `QBM`, `DPR`; specific objects and source permissions unverified. |
| Business Roles and System Roles | Installed module rows | `RMB`, `RMS`; role usage and relationships unverified. |
| Attestation, compliance rules, company policies | Installed module rows | `ATT`, `CPL`, `POL`; policies, operational use, and risk/mitigation configuration unverified. |
| Active Directory and Active Roles | Installed module rows | `ADS`, `ARS`; connector configuration and synchronization state unverified. |
| Reporting subscriptions | Installed module row | `RPS`; generated report contents remain outside the approved payload. |
| Customer configured content | Installed module row | `CCC`; report records customization categories, while custom connector/module semantics remain unknown. |
| Governance Base | Installed module row | `CAP`; no inference about individual governance features. |
| IT Shop, application governance, Entra, Exchange/Exchange Online, archiving, password-management configuration | Unknown at feature/category level | A present base module or absence of a separately named row does not establish activation, absence, or safe collection. |

The 14 rows do not justify a `Not installed` or `Unsupported` classification for an unlisted family. Public documentation describes product possibilities, not this database's enabled features.

## Sections 5–9 — later source and gate review

Native object/field dictionary, default-to-actual comparison, sanitized fixtures, approved collection query pack, formal effective-permission attestation, execution plans, source impact, coverage, scale, and reviewer decisions remain pending. The bounded metadata diagnostic is not a collector run or a G2/G8 acceptance artifact.

## SME items needed next

1. Reconcile the July report's `CCC`/`10.0.0.287` cover with the current main database's `STE`/`10.0` edition fields and `CCC`/`QBM`/`DPR` module builds; identify the authoritative current product/build and schema-build source. Prefer a protected System information export or a field-reviewed, bounded `QBMVSystemOverview` projection following the vendor version-identification article.
2. Provide protected evidence binding the report and live database to the same Environment A source, and evidence for A/B independence. The current local metadata artifact does not prove either relationship.
3. Provide relevant hotfix/package identifiers or an explicit verified “none” statement. Confirm whether the 14 active `QBMModuleDef` rows are the complete installed inventory and classify applicable unlisted categories.
4. Continue exact-build query-pack, field, permission and database-owner reviews only through their later gates; the confirmations above do not authorize source collection.

## Source-access preparation

On 2026-09-30, the repository owner, acting as the Environment A database owner, authorized bounded read-only metadata discovery. On 2026-10-01, the owner authorized creation of a dedicated SQL login when the existing account's effective capabilities did not satisfy the source-safety boundary. Database-owner provisioning created a new login, removed broad `db_datareader` membership, and granted `SELECT` only on chosen metadata columns in three tables. The subsequent diagnostic probe found no blocking server role, database role, database permission, applicable object permission, or schema permission. A `TOP (0)` check confirmed that the prohibited connection-string column was denied. The live read used encrypted SQL with lab certificate trust relaxed; certificate validation and a formal signed permission/impact review remain open before G2. The administrator credential was used for account and grant provisioning, never as the collector evidence principal. Credentials, account names, host and database details remain outside this repository.
