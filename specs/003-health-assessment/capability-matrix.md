# One Identity Manager Pilot Capability Matrix

Status: Approved — environment evidence required  
Owner: Technical owner  
Reviewers: One Identity SME, security owner, product owner  
Last updated: 2026-10-01

## Purpose

This registry determines whether an exact One Identity Manager 10.x on SQL Server combination is eligible for the health-assessment pilot. A broad `10.x` designation is never sufficient for eligibility. Each environment row locks the exact source build and hotfix, installed modules, ingestion query pack, normalization schema, rule catalog, and validation evidence used by an assessment.

The matrix records database engine product and supported compatibility information needed to validate queries. It must not collect or classify database topology, host layout, clustering, replication, network paths, instance inventory, or infrastructure diagrams.

## Lifecycle

| State | Meaning | Required evidence |
|---|---|---|
| `declared` | Combination is expected but has not passed fixtures | Product/version/module claim and evidence owner |
| `fixture-verified` | Query, mapping, and rule fixtures pass outside a customer environment | Five rule fixture classes, query safety analysis, normalization contract tests |
| `environment-verified` | Read-only extraction and assessment passed in one exact environment | Exact build/modules, permission attestation, performance, coverage and SME review |
| `pilot-validated` | Combination is approved as pilot evidence | Environment verification plus technical, security, SME, and product approval |
| `suspended` | Previously eligible combination is disabled for new runs | Reason, owner, date, affected versions, notification record |
| `unsupported` | Combination cannot be assessed under the approved pilot contract | Explicit limitation and safe user-facing behavior |

Historical assessments retain the locked matrix version even if a row is later suspended.

## Required independent pilot environments

| Environment evidence ID | Independence evidence | One Identity exact version/build/hotfix | SQL Server product/compatibility | Installed validated modules | Query pack | Normalized schema | Rule catalog | State | Evidence owner |
|---|---|---|---|---|---|---|---|---|---|
| PILOT-ENV-A | SME asserted report/current-source binding and A/B independence; protected proof pending | Report cover `CCC`/`10.0.0.287` differs from live main database `STE`/`10.0`; modules `CCC`/`QBM`/`DPR` report `10.0.0.287`. Exact product/schema build and hotfix `NOT VERIFIED` | Provisional live metadata matches SQL Server 2022 build `16.0.1121.4`, source compatibility `160`, collation and KB `5040936`; formal evidence pending | [14 supplied rows match active live module inventory](../001-data-ingestion/one-identity-environment-a-sme-response.md); installed-category interpretation and validation pending | `NOT VERIFIED` | `NOT VERIFIED` | `NOT VERIFIED` | `declared` | Repository owner (planned) |
| PILOT-ENV-B | Repository owner committed to create as an environment distinct from PILOT-ENV-A; evidence pending | `NOT VERIFIED` | `NOT VERIFIED` | `NOT VERIFIED` | `NOT VERIFIED` | `NOT VERIFIED` | `NOT VERIFIED` | `declared` | Repository owner (planned) |

Independence means separately administered source environments with distinct evidence baselines. A cloned database, restore, repeated run, or second tenant backed by the same source environment does not count as the second environment. Customer identity may remain confidential in this document; the protected validation record must establish independence.

## Approved pilot capability declarations

These rows declare product scope only. They do not become `pilot-validated` until at least one exact environment row supplies all required evidence.

| Capability family | Declared scope | Validation required per exact build | Current state |
|---|---|---|---|
| Product foundation | Identity Management Base; Target System Base and synchronization; configuration/schema; authorization; processes, scripts, templates, custom code; Job Queue; DBQueue; compilation; consistency; audit; synchronization and error health | Inventory reconciliation, query fixtures, rule fixtures, scale/performance, SME review | `declared` |
| Governance structure | Business Roles and System Roles | Native-type fidelity, hierarchy/assignment relationships, broken-reference and excessive-access fixtures | `declared` |
| Request and approval | IT Shop | Shelf/product/request/approval configuration and relationship coverage | `declared` |
| Attestation and compliance | Attestation, Compliance Rules and SoD, Company Policies, Risk Assessment | Rule/policy/mitigation applicability, configuration and operational evidence | `declared` |
| Application governance | Application Governance | Installed-feature discovery, configuration, ownership and operational coverage | `declared` |
| Reporting and archival | Report subscriptions and data archiving | Definitions/configuration only unless separately permitted; no generated report contents by default | `declared` |
| Password management | Password-management configuration | Secret/password values prohibited; configuration and redaction tests required | `declared` |
| Target systems | Active Directory, Microsoft Entra ID/Azure AD, Exchange and Exchange Online | Metadata/configuration and source-contained operational evidence only; no downstream connection | `declared` |
| Customer extensions | Customer-specific connectors and custom modules | Inventory plus generic security/code/configuration/dependency/operations checks; semantic-analysis limitation | `declared` |

Uninstalled modules are `not_applicable`. Installed but inaccessible modules are `inaccessible`. Installed modules outside the approved generic or semantic scope are `unsupported` or `not_assessed`; they are never silently omitted.

## Per-environment evidence checklist

| Gate | Required result | Evidence status |
|---|---|---|
| Exact product build and hotfix | Captured from an approved product metadata source with provenance | `NOT VERIFIED` |
| Installed module/version inventory | Reconciled to supported module identifiers | `NOT VERIFIED` |
| Acquisition path | Dedicated SQL Server read-only account and approved query pack | `NOT VERIFIED` |
| Permission attestation | Excess read-only warned/audited; write, DDL, ownership or administrative capability blocked | `NOT VERIFIED` |
| Query safety | Static SQL validation, DBA review, bounded execution evidence, cancellation and production-impact evidence | `NOT VERIFIED` |
| Normalization | Contract tests preserve native table/type, column/path, UID when available, module, version, extraction method and provenance | `NOT VERIFIED` |
| Inventory representation | Every expected supported in-scope object/category has one terminal assessment state | `NOT VERIFIED` |
| Rule quality | Positive, negative, insufficient-evidence, exclusion and version-compatibility fixtures pass | `NOT VERIFIED` |
| Generic extension analysis | Custom connectors/modules show generic checks and semantic limitation | `NOT VERIFIED` |
| Performance | Approved-scale extraction and assessment evidence meets documented targets or records an approved limitation | `NOT VERIFIED` |
| Reassessment | Later baseline demonstrates recurrence and score-change explanations | `NOT VERIFIED` |
| Reviews | One Identity SME, security owner for security rules, technical owner and product quality approval | `NOT VERIFIED` |

## Promotion and suspension controls

Normal promotion requires an immutable evidence bundle containing exact versions, executed test identifiers, results, limitations, reviewers, dates, and artifact digests. No single reviewer may both author and grant every required approval under the normal path. During the pilot, the repository owner may make an explicit artifact-specific exception to any internal promotion review or evidence gate, including sole approval of their own work. The row retains its factual lifecycle state and carries visible override scope, missing/failed evidence, owner, decision time and expiry; the override never changes it to `pilot-validated` without the required executed evidence and cannot satisfy pilot acceptance. Customer database access authority and runtime safety remain independent requirements.

Suspend a row for unsafe queries, vendor-schema incompatibility, material rule-quality regression, incomplete permission enforcement, unexplained data loss, or security control failure. Suspension prevents new runs and publication from new runs but does not rewrite historical assessments. Affected active work pauses at its next safe checkpoint and produces an operator/customer notification without exposing evidence.

## Open evidence requests

- Create PILOT-ENV-A and PILOT-ENV-B, assign their protected identifiers, and record evidence proving they are independently administered distinct environments.
- Reconcile A's report-cover and live main-database edition/build fields with the One Identity SME; identify the authoritative exact product/schema build, hotfix status, and report/source binding. The verified PDF cover and provisional local SQL/module artifacts do not establish G8 eligibility. [Vendor version guidance](https://support.oneidentity.com/identity-manager/kb/4257755/how-to-identify-the-version-of-one-identity-manager) points to System information or `QBMVSystemOverview` for a protected, field-reviewed next read.
- Obtain protected A/B independence evidence, A's unlisted-category classifications and B's source-identification records through the approved ingestion path.
- Establish whether the environments exercise different supported patch/hotfix levels, where available.
- Record the eligible SQL Server product/compatibility information without collecting database topology.
- Approve the first query pack, normalization schema, rule catalog and fixture bundle.

## Approval

Approved by: Repository owner  
Date: 2026-09-28  

Only an authorized human reviewer may approve capability rows or this matrix.
