# Environment A — partial One Identity SME response

Status: Report-backed intake; exact source eligibility and query approval NOT VERIFIED

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
| SQL Server build and database compatibility level | Not established by this report. | Database-owner evidence required before query review. |
| Query-relevant collation/compatibility | Not established by this report. | Supply only if needed for exact queries. |

The cover value is in the approved pilot's **10.x** family. It does not by itself prove current source identity, exact schema compatibility, module versions, or G2/G8 eligibility. One Identity's [configuration guide](https://support.oneidentity.com/technical-documents/identity-manager/9.1.2/configuration-guide/customizing-the-one-identity-manager-base-configuration/changing-database-connection-data) describes “Edition version” as the edition's version number; the separate database and SQL Server facts still need evidence.

## Section 4 — installed module and category inventory

Activated configuration and schedules identify candidate categories for SME review, including governance and operational behavior. They do **not** provide exact installed module IDs and versions. Every proposed family remains **Unknown** until the SME supplies a current module inventory and explicitly marks absent or unsupported families. The report's customization names and code remain in protected evidence; any proposed collector field still needs classification and an exact-build query review.

## Sections 5–9 — later source and gate review

Native object/field dictionary, default-to-actual comparison, sanitized fixtures, bounded read-only query pack, effective database permissions, execution plans, source impact, coverage, scale, and reviewer decisions remain pending. This report is an input to those reviews, not an authorization to query or a G2/G8 acceptance artifact.

## SME items needed next

1. Confirm the report's protected artifact ID, its binding to the current `PILOT-ENV-A` source, and independence from `PILOT-ENV-B`.
2. Confirm **10.0.0.287** as the current installed product/build and supply the exact database schema build and relevant hotfixes.
3. Supply installed module IDs and exact versions, with absent/unsupported/unknown states for proposed categories.
4. Supply SQL Server build and database compatibility level. Query and database-owner evidence follows for the confirmed exact build and applicable modules.
