# Implementation Plan: One Identity Manager Health-Assessment Pilot

Status: Approved — G0 passed; implementation authorized within this plan  
Product spec: `specs/003-health-assessment/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/003-health-assessment/technical-spec.md` (Approved 2026-09-28)  
Test plan: `specs/003-health-assessment/test-plan.md` (Approved 2026-09-28)  
Owner: Technical owner  
Reviewers: Product owner, security owner, operations owner, quality owner, One Identity SME, accessibility reviewer  
Last updated: 2026-09-29

## Planning result

The approved specifications are sufficient to create and review this plan. Missing pilot-environment and provider resource identifiers do not prevent planning or fixture-based development, but they do prevent capability promotion, provider-dependent enablement, pilot evaluation and pilot execution.

This plan was approved by the repository owner on 2026-09-29. The following implementation decisions were resolved and recorded before approval because they affect compatibility, security, operations or acceptance evidence:

| Decision ID | Required decision and evidence | Owner/reviewers | Blocks |
|---|---|---|---|
| IMP-DEC-001 | **RESOLVED 2026-09-29:** use the approved software baseline and servicing policy below | Repository owner; technical, security and operations review during implementation | — |
| IMP-DEC-002 | **RESOLVED 2026-09-29:** deploy the pilot only in Azure `eastus2`; no recovery region, cross-region standby or geo-redundant database backup; regional outage uses the manual best-effort business disaster plan | Repository owner; technical, security and operations review during implementation | — |
| IMP-DEC-003 | **RESOLVED 2026-09-29:** use the approved signed Windows Service/CLI collector profile below | Repository owner; technical, security, One Identity SME and customer database-owner review during implementation | — |
| IMP-DEC-004 | **RESOLVED 2026-09-29:** use Prince 17 as the acceptance-gated PDF renderer under the approved isolated profile and commercial-license prerequisite | Repository owner; accessibility, security and technical review during implementation | Phase 1C PDF remains disabled until spike evidence passes |
| IMP-DEC-005 | **RESOLVED 2026-09-29:** use the approved Windows-first browser, assistive-technology and PDF-reader matrix below | Repository owner; accessibility, product and quality review during implementation | — |
| IMP-DEC-006 | **RESOLVED 2026-09-29:** use a dedicated user-assigned managed identity federated to the confidential Entra web registration, subject to a successful end-to-end deployment spike | Repository owner; security and technical review during implementation | Entra sign-in remains disabled until the federation gate passes |

Concrete Entra registration IDs, managed-identity IDs, OpenAI project ID/ZDR evidence, Azure resource IDs, secrets, PILOT-ENV-A/B protected identifiers and exact One Identity build/module values are environment configuration or verification evidence. They are not architecture decisions and may be supplied after plan approval, but the dependent capability must remain disabled until its gate passes.

### Approved software baseline — IMP-DEC-001

| Component | Approved initial version |
|---|---|
| .NET SDK | `10.0.401` |
| .NET and ASP.NET Core runtime | `10.0.12` LTS |
| Entity Framework Core | `10.0.12` |
| Npgsql Entity Framework Core provider | `10.0.3` |
| Backend build image | `mcr.microsoft.com/dotnet/sdk:10.0.401-noble` |
| Backend runtime image | `mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra` |
| Node.js | `24.21.0` LTS |
| npm | `11.20.0` |
| React and React DOM | `19.3.0` |
| TypeScript | `7.0.2` |
| Vite | `8.3.1` |
| Azure Database for PostgreSQL Flexible Server | Major `18`; initial observed service minor `18.6` |
| Bicep CLI | `0.47.16` |
| OpenAI .NET client | `2.14.0` |
| OpenAI model and endpoint | `gpt-6-sol` through `/v1/responses` |

The application must commit exact dependency lock files and pin deployed container images by immutable digest. The PostgreSQL major version is an infrastructure input; Azure-managed minor servicing is accepted, and every deployment/evaluation record captures the observed engine minor. The OpenAI service currently exposes the approved `gpt-6-sol` alias rather than a dated snapshot; each run therefore locks the configured alias, prompt/policy/client versions and provider-returned model metadata. AI remains disabled until G5 verifies the selected project, US processing and ZDR.

Supported patch updates are reviewed at least monthly and before every release. A critical security update is expedited through the affected test and deployment gates. Patch updates within the approved major/minor family may update this baseline through ordinary technical/security review; framework minor/major, PostgreSQL major, container distribution, package-manager major, OpenAI client major or model changes require compatibility evidence and an approved plan change. Unsupported or end-of-life versions are prohibited.

Every Bicep resource declaration uses an explicit stable GA resource-provider API version; `latest` aliases and preview APIs are prohibited unless a separately approved documented requirement has no GA path. Exact resource API versions are recorded in the infrastructure module/version lock and verified for East US 2 before G1. The initially approved service APIs include `Microsoft.App@2026-01-01`, `Microsoft.DBforPostgreSQL@2025-08-01`, `Microsoft.ServiceBus@2026-01-01`, `Microsoft.ContainerRegistry@2025-11-01`, `Microsoft.KeyVault@2026-02-01`, and `Microsoft.Storage@2025-06-01`; any additional provider is added under the same stable-version rule.

Primary support evidence: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Node.js releases](https://nodejs.org/en/about/previous-releases), [React 19.3](https://react.dev/blog/2026/09/09/react-19-3), [TypeScript](https://www.typescriptlang.org/), [Azure PostgreSQL supported versions](https://learn.microsoft.com/en-us/azure/postgresql/configure-maintain/concepts-supported-versions), [PostgreSQL version policy](https://www.postgresql.org/support/versioning/), [OpenAI GPT-6 Sol](https://developers.openai.com/api/docs/models/gpt-6-sol), and [OpenAI SDKs](https://developers.openai.com/api/docs/libraries).

### Approved pilot region — IMP-DEC-002

The pilot deploys only in Azure East US 2 (`eastus2`). The deployment must verify subscription capacity, quotas, selected SKUs and current availability for Container Apps/Jobs, PostgreSQL Flexible Server, Service Bus Standard, Blob Storage, Key Vault, ACR and Azure Monitor before G1. Environment parameters remain non-secret and independently deployable; resource identifiers and secrets remain environment configuration.

There is no pilot recovery region, cross-region standby or geo-redundant PostgreSQL backup. The 24-hour RPO and one-business-day RTO apply only to recoverable incidents and isolated restores within East US 2. A region-wide Azure outage invokes the separately maintained manual business disaster plan and has no pilot RPO/RTO. The plan may select an alternate United States region during an actual disaster, subject to then-current capacity, data availability, security review and manual validation, but no such region is pre-approved as a standby.

### Approved pilot collector — IMP-DEC-003

The One Identity pilot collector is an Authenticode-signed, self-contained .NET 10 `win-x64` package delivered as an MSI. It installs an unattended Windows Service and exposes a one-shot command-line mode from the same signed release for encrypted offline collection. Supported collector hosts are Windows Server 2022 and Windows Server 2025, including Server Core. The pilot does not support a collector container, browser UI, inbound listener or automatic self-update.

The SQL provider is `Microsoft.Data.SqlClient` `7.1.0`. The preferred connection uses Windows Integrated Security under a dedicated customer-managed gMSA or domain service account. A dedicated SQL-authentication account is an allowed fallback where Windows authentication is unavailable. Its secret is protected locally with Windows DPAPI and filesystem/service-identity ACLs, never stored in ordinary configuration or logs, and never sent to the Azure platform. Both modes remain subject to the approved permission attestation: excess read-only access warns/audits, while write, DDL, ownership, impersonation or administrative capability blocks collection before evidence queries.

Online delivery uses outbound-only HTTPS. A one-time enrollment authorizes the collector to generate and register a non-exportable local device certificate and obtain short-lived, scope-bound upload authorization. No inbound firewall rule or interactive user credential is required for normal service operation. Revocation prevents subsequent upload/enrollment use without granting any SQL authority.

Offline delivery creates a versioned authenticated encrypted package. Evidence is encrypted with a random per-package AES-256-GCM key; that key is wrapped to the platform's approved RSA public key using RSA-OAEP-SHA256. The signed manifest binds collector/query-pack/schema versions, opaque scope, chunk digests, redaction policy and creation/expiry metadata without credentials or prohibited topology. Security review and test vectors must approve the exact envelope format before G2.

Local staging is encrypted and ACL-restricted to the service identity. Verified online upload or confirmed offline handoff removes completed clear working material; encrypted checkpoints may remain only until their configured expiry so interrupted collection can resume. Removal is idempotent and audited without payload values.

Pilot updates are manual installation of a signed MSI after Authenticode chain, publisher, version and published digest verification. The collector has no remote self-update channel. Downgrade is blocked unless an approved recovery procedure identifies the exact signed version and compatibility evidence. Customer administrators retain installation, service-account, enrollment, upgrade and removal control.

### Approved PDF renderer — IMP-DEC-004

Prince 17 for Linux x86-64 is the selected pilot PDF renderer, subject to a commercial cloud/site license and successful accessibility/security spike. The exact Prince artifact checksum, renderer image digest, fonts, CSS, wrapper and license terms are locked in the release manifest. A Prince upgrade is a render-engine change requiring parity, determinism, accessibility and sandbox regression evidence.

The renderer runs only as a finite isolated Azure Container Apps Job. It receives a signed frozen render manifest plus allowlisted local static assets and writes one declared PDF artifact. It runs non-root with a read-only container filesystem, a bounded ephemeral workspace and explicit CPU, memory, output-size and execution-time limits. It has no network, database, canonical API, customer-data-plane, arbitrary filesystem, managed-identity, Key Vault or general secret access.

The only credential-like material permitted in the renderer is the Prince vendor license file, mounted read-only by the job platform and inaccessible to render content. It is not a platform/customer credential, is excluded from the image, output, logs and crash artifacts, and cannot authorize any external service. This narrow exception does not permit access to Key Vault from the renderer or weaken the prohibition on customer/platform credentials.

Prince must render in PDF/UA-1/tagged-PDF mode with document title/language, logical reading order, headings, lists, tables, links, bookmarks, meaningful alternative text and graph/table equivalents. Rendering fails closed rather than publishing when the engine reports a PDF/UA fault, required font/asset is missing, the output digest is nondeterministic, the manifest signature/scope is invalid or automated accessibility validation fails.

The spike must use representative executive/practitioner reports, dense tables, long findings, links, SVG/graph alternatives and malicious content. Evidence includes PDF/UA validation, tag-tree inspection, deterministic digest retries, network/filesystem/resource escape attempts, and manual keyboard/screen-reader review of reading order, headings, tables, links and alternative text. Markdown is generated independently and remains available if PDF is disabled or a render fails. Phase 1C PDF remains off until the accessibility, security and technical reviewers approve the evidence and the commercial license is in place.

### Approved browser and assistive-technology matrix — IMP-DEC-005

The pilot interactive UI supports managed, fully patched Windows 11 Enterprise x64 on a Microsoft-supported General Availability release. Every release candidate records and freezes the exact Windows edition, version, build and installed updates used for acceptance; an operating-system release that has left Microsoft support is not supported.

The supported browser channels are the current fully patched Microsoft Edge Stable, Google Chrome Stable and Firefox Extended Support Release (ESR). Exact browser versions are recorded at release freeze because the vendor channels service independently. Outdated, Beta, Developer, Canary, preview and Internet Explorer-mode builds are outside the pilot support boundary.

The complete accessibility workflow in `accessibility-plan.md` runs with the current stable NVDA release and Edge Stable. Targeted compatibility verification runs with NVDA and Firefox ESR and with the Windows Narrator version included in the frozen Windows build and Edge Stable. Keyboard-only and 200%/400% zoom/reflow verification runs in all three supported browsers; Windows contrast-theme verification runs in Edge and Firefox ESR. Automated browser coverage supplements but does not replace these manual checks.

Published PDF verification uses the current patched Adobe Acrobat Reader with NVDA and covers logical reading order, headings, lists, tables, links, bookmarks, alternative text and graph equivalents. Browser-integrated PDF viewers may receive smoke testing but are not the pilot's authoritative PDF accessibility reader.

Mobile and tablet browsers, Safari/macOS, ChromeOS and Linux desktop are not supported by the pilot. Responsive/reflow and touch-target WCAG requirements still apply to the supported Windows desktop matrix; this exclusion does not waive any WCAG 2.2 A/AA requirement on an essential pilot flow. A browser, operating-system or assistive-technology update that causes an essential-flow accessibility regression blocks release until fixed or until the affected version leaves the supported matrix through an approved plan change.

### Approved Entra confidential-client credential — IMP-DEC-006

The Azure Container Apps BFF uses a dedicated user-assigned managed identity as a federated credential for its single-tenant confidential Entra web application registration. The app registration trusts only the assigned identity's exact tenant issuer, principal ID subject and `api://AzureADTokenExchange` audience. The BFF obtains a short-lived managed-identity assertion through the supported Microsoft .NET identity library and exchanges it as its confidential-client credential. Its managed identity is not shared with workers, the renderer or other application roles. Neither a client secret nor a client-certificate private key is configured for the pilot BFF.

The G1 platform and Milestone 2 identity deployment spikes must prove this credential works for the complete OIDC authorization-code redemption and session creation path in Container Apps, including multiple BFF replicas, assertion renewal and cache behavior. They must also prove wrong identity, issuer, subject, audience and tenant denial; removal of the federated trust or managed-identity assignment blocks new sign-ins; existing product sessions can be revoked; and assertions and tokens are absent from browser JavaScript, deployment output and logs. Entra sign-in remains disabled until the technical and security reviewers accept that evidence. If the supported library or platform cannot complete this path, changing to a Key Vault-backed certificate requires an explicit plan and security review before enablement; a client secret requires separate explicit security approval.

Primary support evidence: [Microsoft Entra application trust for a managed identity](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation-config-app-trust-managed-identity), [MSAL.NET workload identity federation](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/web-apps-apis/workload-identity-federation), and [Azure Container Apps managed identities](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity).

### Approved repository, contract and migration structure

Use one versioned repository and .NET codebase for the modular application, deployed as separate interactive BFF/API and asynchronous worker roles. The isolated report-rendering job consumes only its signed render contract and has no dependency on canonical data access. The customer-side Windows collector is a separate release artifact. The planned directory ownership is:

| Directory | Ownership and boundary |
|---|---|
| `src/server/hosts/` | BFF/API, worker and isolated renderer entry points; composition and deployment wiring only |
| `src/server/modules/` | The bounded modules in `technical-spec.md` (identity/policy through MCP projection); each owns domain rules and persistence access behind explicit module interfaces |
| `src/web/` | React/TypeScript UI; only generated HTTP contract types and approved UI projections cross the server boundary |
| `src/collector/` | Signed Windows Service/CLI collector and source-specific query execution; no direct dependency on health-assessment internals |
| `contracts/` | Versioned HTTP/OpenAPI, durable-work, evidence-baseline and report/render schemas plus compatibility fixtures |
| `infra/bicep/` | Azure infrastructure modules and non-secret environment parameters |
| `tests/` | Unit/property, architecture, contract, integration, end-to-end, security, accessibility, performance and recovery suites with synthetic fixtures |

The accepted module list in `technical-spec.md` is authoritative. Architecture tests reject direct persistence access across modules, UI access to server internals, collector access to assessment stores, renderer access to canonical APIs/data stores, and worker use of browser-delegated authority. Modules communicate through explicit in-process interfaces and versioned work messages; a service split would require an ADR.

The HTTP API publishes a versioned OpenAPI contract. The server implementation is checked against a frozen contract artifact; the frontend's request/response types are generated from that artifact, and CI rejects unreviewed generated diffs or hand-edited generated files. Durable-work, evidence-baseline and report/render schemas are language-neutral, committed, versioned and validated at both producer and consumer boundaries. Additive fields may preserve a major version only when older readers safely ignore them; a breaking shape or meaning requires a new major schema/media version and an explicit reader/worker compatibility window. Exact routes, operation shapes and public schemas require technical-owner approval before implementation; this structure does not define them by implication.

Each module owns its table/schema changes. Separate control-plane and customer-plane migration sets compose those changes in a documented order, and one controlled migration runner records the applied version and digest for every database. Deployment uses expand/migrate/contract; it stops on schema drift or an incompatible customer database and never silently skips one. Contract and migration tests exercise old/new BFF and worker overlap, all customer databases, historical manifests, rollback compatibility and an isolated restore. Destructive contraction waits until old readers/jobs and approved restore paths no longer need the prior shape. The collector cannot run platform database migrations.

### Approved CI and verification gates

The pipeline has three cumulative gates. A required check must report `PASS` with an inspectable result; failure, skip, unavailable infrastructure, absent evidence or an unexecuted check is `NOT VERIFIED` and cannot satisfy its gate. Changes to a disabled feature still run its relevant contract, authorization and negative-capability checks.

| Gate | Required checks and evidence | Blocks |
|---|---|---|
| Change/merge | Formatting, linting, static type checking and build for affected .NET, TypeScript, collector and infrastructure components; architecture/module-dependency checks; unit/property and focused contract/integration tests on synthetic data; OpenAPI/generated-client and versioned-schema drift checks; Bicep compile/lint/policy validation; secret, dependency, vulnerability and license scans; affected OCI image build/scan, SBOM and provenance attestation; affected documentation/traceability checks | Merge when any applicable result is not `PASS` |
| Milestone | Full affected integration and end-to-end suites, complete authorization/isolation matrix for touched paths, automated and required manual accessibility checks, forward/rollback migration and mixed-version compatibility, adversarial/security tests, signed artifact provenance and recorded reviewer evidence | Milestone completion and capability promotion |
| Pilot readiness | Entire approved `test-plan.md`, frozen Windows/browser/assistive-technology and PDF matrix, synthetic-scale load/soak/outage tests, deployment/rollback, deletion/purge and isolated restore drills, independent security/accessibility/operations review, verified build/container/collector provenance, and two-environment initial/reassessment evaluation | G8/G9 acceptance and release-readiness review |

The build pipeline uses the pinned toolchain and immutable dependency locks in IMP-DEC-001. Test and scan artifacts contain only synthetic or opaque scope data and link to the exact commit, dependency lock, container/collector digest, contract/catalog versions, environment and check version. Manual evidence records reviewer, time, tested versions, result and defects. CI never substitutes a green scanner for required manual accessibility, security or operations review, and no CI result authorizes production release.

The repository-owner pilot exception below may authorize a specific merge or pilot trial activation despite failed or absent internal checks. The check remains `FAIL` or `NOT VERIFIED`, the milestone is not marked complete, and pilot acceptance and production release remain blocked by their separate criteria. This exception requires the same exact commit/artifact identity and visible waiver record as artifact promotion.

### Approved implementation evidence storage and retention

`evidence-index.md` is the feature's metadata-only index for planning and implementation gates. Each entry identifies the gate/work item, commit, CI run or manual check, tested version and artifact digest where applicable, `PASS`/`FAIL`/`NOT VERIFIED` result, reviewer/decision authority, date, and a restricted-store reference for execution evidence. It also indexes deployment attestations and catalog/query/prompt promotion decisions. G0 plan approval is evidenced by this repository's approved plan and human decision record; it does not depend on the engineering artifact store that Milestone 0 will build. The index contains no customer payload, credential, token, protected identifier, report content or detailed security finding. For G1–G9 and later execution decisions, a missing or expired required bundle changes the dependent gate to `NOT VERIFIED` until evidence is rerun and linked.

Sanitized test reports, scan results, signed build/collector/container attestations and reviewer records are stored outside Git in a separate access-restricted East US 2 engineering artifact store. The store is not a customer data plane. It uses managed identities, least-privilege reviewer access, encryption, versioning, write-once decision bundles or equivalent tamper evidence, and digest/signature verification on retrieval. A sanitizer rejects customer payloads, names, credentials, tokens, prompts/responses containing evidence and unrestricted reports before upload; failed sanitization blocks the gate. CI and developer fixtures remain synthetic. Provider configuration and protected pilot-environment identifiers remain in their approved secret/configuration boundary and are represented here only by opaque references and verification outcomes.

Each frozen gate evidence bundle is retained for 12 months from its decision date, then removed from ordinary access and purged from the active store within 30 days, unless a separately approved hold identifies authority, scope and expiry. If evidence expires before pilot acceptance, the relevant gate must be rerun; a stale index entry never establishes a pass. This is an internal engineering-evidence lifecycle, distinct from the product audit policy in `docs/security/health-assessment-audit-policy.md`. The store, access controls, integrity checks, purge job and restore behavior remain implementation tasks and `NOT VERIFIED` until exercised.

### Approved artifact-promotion workflow and pilot owner override

The normal path is `draft -> validated -> reviewed -> staged -> active`, with `suspended` available from every enabled state. A protected repository and CI build produce immutable, signed/digested artifacts and a dependency/SBOM, test and provenance bundle. The promotion service verifies the exact artifact digest, required passing gate evidence, reviewer identities and compatibility manifest before a human-authorized, audited activation. Repository merge, image deployment or a CI pass alone never activates a query pack, rule catalog, prompt/model policy or customer capability row. The artifact author cannot be the sole approver under the normal path.

| Artifact | Normal required review before pilot activation |
|---|---|
| Exact-build One Identity query pack | One Identity SME, security owner and the customer database owner for each exact environment; static SQL, minimum-permission, bounded-impact and prohibited-field evidence must pass |
| Rule catalog/version | One Identity SME for every pilot rule; security owner for security rules; product owner for catalog and AI-analysis quality; all five fixture classes and mandatory-domain/module reconciliation must pass |
| Prompt/model/AI policy | Product owner and security owner; packet/schema, injection, citation, data-minimization, cost and representative regression evidence must pass the approved `evaluation-plan.md` thresholds and G5 provider controls |
| Application/collector/renderer build | Technical, security, quality and operations review of the exact signed/digested build, dependencies/SBOM, relevant CI/milestone gates, migration/rollback compatibility and capability boundaries; production release remains a separate human authorization |

A security or operations owner may immediately suspend a suspect artifact for new work, with reason, scope, time and follow-up review recorded in the product audit and engineering evidence index. Suspension does not rewrite a historical run, rule result, assessment input lock or published report. Returning to a prior version means activating only a previously approved compatible digest after current schema, query-pack, prompt, capability and work-message checks. If compatibility fails, affected new work remains paused until a forward repair is approved.

During the pilot, the repository owner may override **any internal artifact-promotion review or evidence gate**, including reviewer separation, SME/security/product/quality sign-off, missing or failed tests, scan/provenance findings and the material-quality thresholds in `evaluation-plan.md`. The owner may be the artifact author and sole promotion approver under this exception. Each use is a separate explicit signed decision identifying the exact artifact/digest, affected gate failures or missing evidence, target customer/environment and capability, reason, known risk, compensating controls if any, rollback/suspension target, decision time and expiry no later than the pilot end. A protected operator path verifies that the decision came from the repository owner, records it in the engineering evidence index and product audit, and exposes the exception and its unverified areas to authorized pilot users and reports. There is no persistent global bypass switch. Expiry or owner revocation disables new work on that exception; historical run locks and publications remain intact.

An owner override authorizes a limited pilot activation; it does not convert a failed, missing or unexecuted check into `PASS`, make an unvalidated capability row `pilot-validated`, or satisfy G8/G9 acceptance or production-release criteria. Customer database-owner permission to access a customer source and the runtime read-only, authorization, tenant-isolation, data-minimization, provider-ZDR, secret-protection and artifact-identity controls remain separate from artifact promotion and are still enforced. A desire to change those product or security boundaries requires a separate explicit amendment to their governing specifications and any customer authority required for that environment.

### Approved synthetic fixture taxonomy

All developer and CI fixtures are synthetic and versioned. Each fixture records a stable case ID, generator or input digest and seed, evidence/contract schema versions, source-build/module compatibility descriptor where applicable, expected typed output or denial, governing requirement/test IDs and reviewer. Exact customer builds are added to compatibility fixtures only after authorized environment evidence exists; a synthetic match alone never advances a capability row to `environment-verified` or `pilot-validated`.

| Fixture family | Required cases and expected evidence |
|---|---|
| Exact build and module compatibility | Supported and unsupported build/hotfix descriptors; installed, uninstalled, unavailable and customer-specific modules; native-key and modified-vendor-default distinctions; explicit applicability or gap outcome |
| Query, permission and baseline | Minimum read permission, warned excess read-only and blocked write/DDL/owner/admin; static SQL rejection, timeout/cancellation, bounded pages, checkpoint/resume, corrupt/tampered offline package, malformed/null/duplicate/conflicting/truncated values; prohibited topology, secret and government-identifier detection |
| Deterministic rule quality | Positive, negative, insufficient-evidence, exclusion and version-compatibility cases for every pilot rule, each with expected result, coverage and finding fields; mandatory-domain/module reconciliation |
| Assessment state and scoring | Every terminal coverage state; severity/confidence boundaries, proposed versus confirmed and accepted-risk states, weights, score/maturity/outcome calculations, recurrence and immutable publication digests |
| Authorization and lifecycle | Actor/role and customer/project/environment/evidence-category matrix, direct-ID substitution, stale/revoked assignment, soft deletion, link expiry, purge and restore tombstones |
| AI and untrusted content | Encoded/role-spoofing prompt injection, forbidden raw/secret data, unauthorized or missing citations, invalid structured output, conflicting facts, provider error/rate limit, budget race and disabled/ZDR-unverified provider |
| Render, export and client access | Malicious HTML/Markdown/SVG/script/CSV cells, dense accessible PDF tables and graph equivalents, share-link/passcode abuse, MCP mutation attempts, keyboard/focus/zoom cases across the approved browser matrix |
| Work, migration and recovery | Duplicate/expired/poison work, outbox reconciliation, mixed worker/message/schema versions, partial migration, isolated restore, integrity mismatch and no resurrection of deleted or revoked resources |
| Independent scale generators | Deterministic separately generated sets up to 100,000 identities, accounts, entitlements, roles, workflows and 90-day operational records, with recorded seed, shape, concurrency and expected count/performance outcomes; one environment need not hold every maximum simultaneously |

Quality owns the expected-result catalog and fixture-to-test traceability; the One Identity SME reviews source/module and rule cases; security reviews permission, prohibited-data, authorization and adversarial cases; operations reviews queue, migration, scale and recovery cases. Fixture generators, seeds and expected-result digests are immutable for a test run. Changed expectations require an attributed review rather than silently updating a golden file. Real pilot environments remain separate G8 evidence and are never copied into CI fixtures.

## Scope and constraints

### In scope

- Phase 1A through 1D of the approved One Identity Manager 10.x on SQL Server health-assessment pilot: evidence foundation, deterministic and constrained AI analysis, governed review/scoring/tasks, reassessment, immutable publication, dashboard/PDF/Markdown/expiring link, evaluation and read-only MCP.
- The Azure platform and application shape accepted in ADR-0001 through ADR-0004: ASP.NET Core modular application, React/TypeScript BFF UI, separately scalable workers, Container Apps/Jobs, PostgreSQL Flexible Server, Service Bus Standard, Blob Storage, Key Vault/managed identities, ACR, Bicep and OpenTelemetry/Azure Monitor.
- The One Identity-specific customer-side collector and feature-001 baseline adapter needed by this pilot, governed by `database-evidence-contract.md` and the approved ingestion specification.
- Separate control-plane and customer-database/blob data planes, payload-free work messages and audit, immutable versioned catalogs/manifests, retention/deletion/restore controls, and the approved Entra and OpenAI boundaries.
- All automated and manual evidence in `test-plan.md`, including security findings SEC-PILOT-001 through SEC-PILOT-010, accessibility, performance, recovery and two-environment evaluation.

### Out of scope

- FR-HAS-57/AC-HAS-21 vendor health portfolio, SailPoint assessment depth, hosted SaaS-to-SaaS connectors, customer-specific semantic rules, custom-rule authoring, deep AI/raw-evidence retrieval, external task creation, benchmarking, ROI/benefit tracking, direct remediation, production migration and every Phase 2–4 capability.
- Direct assessment access to One Identity SQL Server, downstream-system credentials/connections, customer-system mutation, and production release.
- Platform-support access to protected evidence and any unapproved local authentication or authorization bypass.
- Kubernetes, Redis, external search/graph/workflow products, service mesh and unrelated platform refactoring.

### Delivery controls

- Each merge must be reviewable, keep disabled capabilities server-enforced, and include applicable tests, documentation and evidence references.
- No customer evidence is used in developer or CI tests. Synthetic, minimized fixtures cover destructive, adversarial and boundary cases.
- No rule, query pack, prompt/model, capability row or report renderer is promoted by code deployment alone. Promotion is a separately authorized, immutable, digest-bound event with required reviewers.
- Database changes use expand/migrate/contract. No rollback deletes new customer data or rewrites immutable historical records.
- A milestone is complete only when its stated verification has run and its evidence is recorded; implementation presence is not verification.

## Dependencies and gates

| Gate | Entry requirement | Enables | Current state |
|---|---|---|---|
| G0 — Plan approval | IMP-DEC-001 through IMP-DEC-006 resolved; technical approval plus security and operations review | Product-code implementation | `PASS` — repository owner approved 2026-09-29 |
| G1 — Platform foundation | Bicep spike, version lock, East US 2 non-production environment, identity/managed identities, private protected-data paths, Service Bus controls and observability verified | Shared application deployment | `NOT VERIFIED` |
| G2 — Evidence contract | Approved query pack, field dictionary, normalization schema, collector package and fixtures for an exact build | Environment collection and Phase 1A eligibility | `NOT VERIFIED` |
| G3 — Customer isolation | Policy/data-plane routing, per-customer DB/blob roles, worker scope, audit and negative tests pass | Any protected pilot data | `NOT VERIFIED` |
| G4 — Rule catalog | Mandatory domain/module reconciliation and five fixture classes pass with SME/product/security approvals | Deterministic analysis | `NOT VERIFIED` |
| G5 — AI provider | Selected OpenAI project has verified US processing, ZDR, access, quotas, incident configuration and adversarial tests | AI flag | `NOT VERIFIED` |
| G6 — Publication | Canonical manifest, renderer/Markdown accessibility, sandbox, link/export controls and immutable digest tests pass | Phase 1C publication/share | `NOT VERIFIED` |
| G7 — Operations | Deployment/rollback rehearsal, monitoring/runbooks, daily backup and isolated deletion-safe restore satisfy RPO/RTO | Pilot execution | `NOT VERIFIED` |
| G8 — Environment eligibility | PILOT-ENV-A and PILOT-ENV-B independently reach `pilot-validated` | Pilot evaluation | `NOT VERIFIED` |
| G9 — Acceptance | AC-HAS-1 through AC-HAS-20 pass; SEC-PILOT-001 through 010 closed or validly accepted; WCAG gate passes; evaluation accuracy is strictly greater than 80% | Release-readiness review | `NOT VERIFIED` |

## Work-item traceability

| Work item | Outcome | Requirements / criteria | Primary verification |
|---|---|---|---|
| IP-HAS-001 | Delivery and supply-chain foundation | FR-HAS-22–25; AC-HAS-14, 16; SEC-PILOT-007 | Signed/digested build, SBOM/dependency/secret scans, catalog promotion/suspension tests |
| IP-HAS-002 | Azure substrate and workload execution | FR-HAS-47, 50; AC-HAS-6, 9, 14, 20; SEC-PILOT-005, 009, 010 | Bicep policy/network tests, outbox/queue/retry tests, observability and restore evidence |
| IP-HAS-003 | Identity, authorization and tenant isolation | FR-HAS-13–15, 29, 32, 45–46, 55–56; AC-HAS-4, 8–9, 11, 14, 20; SEC-PILOT-001, 002, 008, 009 | Full authorization matrix, cross-tenant property/E2E tests, independent penetration review |
| IP-HAS-004 | Collector and immutable normalized baseline | FR-HAS-1–6, 28–29, 47, 50; AC-HAS-1, 7, 9, 15–16; SEC-PILOT-003 | Query safety/permissions, schema contract, resume/digest, prohibited-field and environment tests |
| IP-HAS-005 | Phase 1A inventory, coverage and orchestration | FR-HAS-1–6, 8, 22–25, 27–29, 43, 47, 50; AC-HAS-1, 7, 15–16 | TP-HAS-001, 007, 015, 016 plus queue/checkpoint failure tests |
| IP-HAS-006 | Deterministic analysis, findings, scoring and maturity | FR-HAS-7–13, 16–25, 27–28; AC-HAS-2–3, 5, 7, 16–17 | Golden/property fixtures, lifecycle/concurrency tests, rule-quality and reconciliation evidence |
| IP-HAS-007 | Constrained AI analysis | FR-HAS-10–13, 30–34, 51–53; AC-HAS-2–3, 8, 12, 17, 19; SEC-PILOT-004, 007 | TP-HAS-008/012, provider controls, injection/leakage/citation/budget tests |
| IP-HAS-008 | Recommendations, fix packages and consultant tasks | FR-HAS-35–39; AC-HAS-14, 18 | Inert-artifact, no-executor, safe CSV, authorization and traceability tests |
| IP-HAS-009 | Governed review, risk and reassessment | FR-HAS-12–15, 20–21, 47–53; AC-HAS-3–4, 6, 10–12, 17, 19 | State/authority/concurrency tests, correlation fixtures and frozen evaluation record |
| IP-HAS-010 | Accessible canonical reporting and publication | FR-HAS-40–46, 49–50; AC-HAS-2–3, 6–7, 11, 13, 17, 20; SEC-PILOT-006 | Canonical digest parity, WCAG/manual AT, tagged PDF/Markdown, render sandbox tests |
| IP-HAS-011 | Expiring report links and acknowledgment | FR-HAS-45–46, 50; AC-HAS-6, 9, 11, 20; SEC-PILOT-006 | Link entropy/expiry/revocation/lockout/redaction and immutable acknowledgment tests |
| IP-HAS-012 | Phase 1D read-only MCP | FR-HAS-46, 55–56; AC-HAS-9, 14, 20 | MCP allowlist/deny-list, revocation, rate/concurrency, redaction and audit tests |
| IP-HAS-013 | Retention, purge, backup and recovery | FR-HAS-21, 45–47, 50; AC-HAS-6, 9–11, 20; SEC-PILOT-005, 009 | Soft-delete/purge rehearsal, isolated restore, tombstone replay, RPO/RTO evidence |
| IP-HAS-014 | Two-environment pilot validation | FR-HAS-1–56 excluding deferred behavior; AC-HAS-1–20 | Approved test plan, exact capability rows, initial/reassessment runs, immutable evaluation report |

## Milestones

### Milestone 0 — Resolve implementation decisions and bootstrap evidence governance

Implementation started 2026-09-29: the approved .NET SDK is pinned, internal artifact integrity and signed gate-check bundle verification plus a deterministic metadata-only bundle writer have synthetic negative checks. A local public-key trust snapshot checks gate scope, signing window, rotation and revocation; its protected production source is absent. The normal-path evidence preflight verifies the artifact, gate bundle and signed reviewer decisions against one digest and scope, but has no identity-backed registry, compatibility or activation path. A separate signed owner-override bundle binds exact target, waived checks, rationale digest and expiry for operator review; owner identity and activation are not implemented. A default 12-month access and 30-day purge-deadline calculator has boundary checks but no store effects. A project-reference architecture check covers the initial collector, renderer and server boundaries. Closed-by-default engineering evidence-store and container-scoped role Bicep drafts compile, lint and pass compiled-template drift checks; they have no deployment entry point. The partial GitHub bootstrap workflow runs these initial checks. This is not the complete change/merge gate. Production signing-key, owner and reviewer trust, network/DNS integration, deployed RBAC/lifecycle verification, the first remote CI run, full configured-gate evidence and complete promotion denial tests remain open. ADR-0005 accepts the gate signing profile; ADR-0006 through ADR-0008 record pilot choices pending milestone review.

- [x] Resolve IMP-DEC-001 and record exact software selections, support windows and servicing policy.
- [x] Resolve IMP-DEC-002 and record the single-region pilot and business-disaster boundary.
- [x] Resolve IMP-DEC-003 and record collector packaging, host support, identity, SQL authentication, secret protection, enrollment, offline encryption and update policy.
- [x] Resolve IMP-DEC-004 and record Prince 17, licensing, sandbox and accessibility enablement gates.
- [x] Resolve IMP-DEC-005 and record the Windows, browser, assistive-technology and PDF-reader support matrix and version-freeze policy.
- [x] Resolve IMP-DEC-006 and record the federated confidential-client credential, isolation and deployment evidence gate.
- [x] Define repository layout, bounded modules, API/schema versioning, database migration ownership and generated-contract policy without changing ADR boundaries.
- [x] Define CI gates for formatting, linting, type checking, unit/integration/contract/E2E tests, accessibility checks, security scans, Bicep validation, container scanning, SBOM and artifact provenance.
- [x] Define immutable evidence storage conventions for test runs, deployment attestations, catalog/query/prompt promotion and approvals; customer payloads are prohibited.
- [x] Define rule/query/prompt/application promotion, suspension, rollback, reviewer separation and the repository-owner pilot override. SEC-PILOT-007 implementation and tests remain open.
- [x] Define the synthetic fixture taxonomy covering exact-build compatibility, all coverage states, all rule fixture classes, permission outcomes, malicious content and scale categories. Fixture data and executed results remain implementation work.
- [x] Obtain technical-owner approval with security and operations review; change this plan to `Approved` and feature state to `PLANNED` only after all approval conditions pass.

Verification:

- [x] Version/region/renderer/collector/browser/credential decision record is complete and internally consistent.
- [ ] CI on an empty/bootstrap change produces payload-free evidence for every configured gate.
- [ ] Supply-chain threat cases demonstrate unsigned/unreviewed/tampered artifacts cannot promote.

### Milestone 1 — Deploy the secure, observable Azure foundation

The Service Bus Standard namespace, work queue and queue-scoped sender/receiver role drafts compile with the pinned Bicep CLI and pass compiled-template policy that rejects twenty-three unsafe drifts. A separate VNet/NAT draft creates the static IPv4 address used by the namespace rule and rejects nine infrastructure drifts. A partial non-production spike entry point composes network, broker, queue, two distinct managed identities and queue-scoped role grants in dependency order, with thirteen wiring-drift checks. Fourteen synthetic address-plan checks exercise private containment, reserved-range exclusion, disjointness and subnet size; actual environment CIDRs remain unallocated. The reusable identity module has no workload attachment. Container Apps, diagnostics, Azure deployment and live network/auth tests are still open. G1 remains `NOT VERIFIED`.

- [ ] Implement reusable Bicep modules and non-secret environment parameters for Container Apps/Jobs, ACR, PostgreSQL, Service Bus, Blob, Key Vault, Azure Monitor/Log Analytics, networking, private protected-data access and controlled static egress.
- [ ] Create separate managed identities and least-privilege roles per interactive API, worker class, renderer/export, purge and operations boundary.
- [ ] Configure PostgreSQL as one non-HA pilot server with separate control-plane database and database/role per customer; configure customer-scoped Blob containers and encryption context.
- [ ] Configure Service Bus Standard with local/SAS auth disabled, Entra-only least privilege, TLS, approved egress restriction, opaque message schema, dead-letter queues and diagnostics without bodies.
- [ ] Implement shared transactional outbox, reconciliation, delivery deduplication, leases, checkpoints, poison-work handling and versioned work envelopes.
- [ ] Add payload-filtered OpenTelemetry logs, metrics, traces and alerts required by the operations plan.

Verification:

- [ ] G1 platform spike deploys and tears down a non-production environment from Bicep without secrets in repository, parameters, state output or logs.
- [ ] Wrong identity/network/customer role and local/SAS attempts are denied; duplicate, lock-loss, commit-before-publish, outage, expiry and dead-letter tests preserve one domain result.
- [ ] Non-HA database outage produces safe API/worker failure and idempotent resume.

### Milestone 2 — Establish identity, policy, isolation, audit and lifecycle primitives

- [ ] Implement the Entra BFF flow with the approved federated managed-identity credential, server-managed sessions, CSRF protection, recent-authentication rules, session/security-version revocation and explicit B2B/product assignments.
- [ ] Implement one deny-by-default policy decision contract across UI/API/workers/render/export/share/MCP with actor/workload, customer, project, environment, resource, action, evidence category and state inputs.
- [ ] Implement trusted control-plane routing to exactly one customer data plane; prohibit client-supplied database/storage locators and cross-plane foreign references.
- [ ] Implement opaque identifiers, customer/project/environment invariants, field-level filtering before serialization and one-customer short-lived worker scopes.
- [ ] Implement the approved payload-free append-only audit schema, integrity monitoring, 12-month retention/soft-delete/purge lifecycle and time/correlation controls.
- [ ] Implement retention clocks, deletion ledger, soft-delete deny behavior, link/job revocation and idempotent dependency-ordered purge primitives.

Verification:

- [ ] Execute all identity/session verification cases and the complete authorization matrix on API and workload paths.
- [ ] Prove full authorization-code redemption and session creation with the federated BFF identity in Container Apps; exercise renewal, multiple replicas, wrong trust/identity/tenant denial, credential removal, session revocation and token/log leakage checks before Entra sign-in is enabled.
- [ ] Cross-tenant generated-ID and confused-deputy tests deny before customer query; audit/log/metric negative tests show no evidence payload.
- [ ] Independent security review closes SEC-PILOT-001, 002, 008 and 009 or records an authorized blocking result.

### Milestone 3 — Produce an eligible immutable One Identity baseline

- [ ] Implement the approved Windows Service/CLI collector only after feature-001 plan dependencies are approved.
- [ ] Implement query-pack static validation, exact-build/module applicability, parameter binding, bounded pages, checkpoints, cancellation, impact guardrails and permission attestation.
- [ ] Block write/DDL/ownership/impersonation/admin capability before evidence queries; warn/audit excess read-only capability.
- [ ] Implement outbound-only HTTPS enrollment/delivery and encrypted offline-package path without topology, credentials, secrets, government identifiers or prohibited profile data.
- [ ] Implement versioned manifest, normalized envelopes, provenance, relationship/conflict markers, ordered content digests and immutable resumption.
- [ ] Implement the health feature's read-only baseline adapter and compatibility rejection without mutating the source baseline.

Verification:

- [ ] Static SQL, permission, contract, prohibited-field, minimization, checkpoint/idempotency, corruption and 100,000-record category fixtures pass.
- [ ] Customer database owner approves execution plans and impact evidence per exact query/build; SME/security reviewers approve query pack and mapping.
- [ ] G2 passes separately for each exact build before its collector/query pack is enabled; SEC-PILOT-003 remains open until both pilot environments pass.

### Milestone 4 — Deliver Phase 1A evidence foundation

- [ ] Implement capability-registry lifecycle and immutable exact-version locks; suspended/unsupported combinations cannot start new work.
- [ ] Implement assessment start/idempotency, durable state machine, input locking, planning, cancellation, checkpoint/resume and terminal completion classification.
- [ ] Build the expected inventory from baseline, capability, scope and rule applicability; create exactly one explicit terminal `CoverageItem` per supported key/category.
- [ ] Implement coverage/quality projections, gaps/conflicts/freshness, progress and failure explanations without scoring gaps as health.
- [ ] Deliver the consultant configuration/progress/coverage UI with keyboard, focus, live-region and non-color status behavior.
- [ ] Keep deterministic, AI, task, publication/share and MCP flags disabled.

Verification:

- [ ] TP-HAS-001, 007, 014, 015 and 016 pass using eligible, partial, incompatible, duplicate, conflict and failure fixtures.
- [ ] Queue duplicate/worker-loss/stale-checkpoint/cancellation tests prove resumability and no false completion.
- [ ] Phase 1A can deploy, roll back and be disabled independently without destructive data rollback.

### Milestone 5 — Deliver deterministic Phase 1B analysis and reproducible scoring

- [ ] Implement immutable desired outcomes, assessment profiles, catalogs/rules and the required promotion/reviewer evidence.
- [ ] Implement rule applicability/execution, typed results, original findings/revisions, provenance, fact/inference/assumption labels, severity/confidence and root-cause grouping with per-object occurrences.
- [ ] Implement finding/disposition state machines, collaboration/comments/mentions/references without attachments, concurrency and append-only history.
- [ ] Implement `pilot-health-v1`, `pilot-quality-v1` and `pilot-maturity-v1` with immutable algorithms, golden fixtures and multidimensional projections.
- [ ] Implement mandatory Critical/High review gates, lower-severity auto-confirm policy and provisional/publishable separation.
- [ ] Promote a catalog only when every mandatory installed domain/module has approved rules or an explicit unsupported state and all five fixture classes pass.

Verification:

- [ ] TP-HAS-002, 003, 005, 007, 016 and 017 plus property, concurrency, replay and exact-version compatibility tests pass.
- [ ] A new catalog/profile creates a new run/comparison and never changes prior results.
- [ ] One Identity SME, product and applicable security approvals are digest-bound and independently attributable.

### Milestone 6 — Add constrained automatic AI analysis

- [ ] Implement disabled-by-default AI policy/budget records, atomic reservation/reconciliation and audited consultant override.
- [ ] Implement minimized normalized/redacted evidence packets, data-only structural envelope, approved authoritative references and no raw resolver/tool/arbitrary network path.
- [ ] Call only the approved Responses API model through the pinned client after G5; bind provider/model/prompt/packet/policy versions to the run.
- [ ] Validate schemas, citations and typed fact/inference/assumption/missing-context/suggestion fields; reject unauthorized/missing citations and preserve conflict/uncertainty.
- [ ] Treat output as untrusted proposed findings; keep Critical/High and all AI review/scoring gates intact.
- [ ] Implement provider outage, timeout/rate-limit/unknown-result and deletion/retention evidence behavior without broadening data.

Verification:

- [ ] TP-HAS-008, 012 and relevant 002/003/017 cases pass against fake provider and verified provider configuration.
- [ ] Adversarial injection/leakage/tool-use/cross-record/oversize/invalid-output tests pass with payload-free audit.
- [ ] Security/privacy verification closes SEC-PILOT-004 before AI is enabled for pilot data.

### Milestone 7 — Deliver recommendations, fix packages, tasks and safe CSV

- [ ] Implement recommendation, priority/effort override, grouped fix-package and consultant-task models with complete finding/evidence traceability.
- [ ] Render SQL/script/configuration examples as encoded, inert, review-only text; preserve generated originals and explicit unverified/reviewed state.
- [ ] Implement in-product task workflow/comments and formula-injection-safe UTF-8 CSV using stable IDs/protected links rather than evidence values.
- [ ] Prohibit all execution, database connection, external task connector, ROI and realized-benefit routes at UI, API, worker and policy layers.

Verification:

- [ ] TP-HAS-014 and 018 pass including malicious markup/formula/shell/network content and direct-operation attempts.
- [ ] Task/fix/CSV authorization and customer-isolation tests pass with no protected-value leakage.

### Milestone 8 — Deliver governed review, risk, reassessment and evaluation

- [ ] Complete consultant/qualified-reviewer workflows, customer-risk-owner-only acceptance, expiring exceptions, validation evidence and review-required notifications.
- [ ] Implement recurrence correlation using stable keys/rule/root-cause signature and reviewer-required ambiguous correlations.
- [ ] Implement snapshot comparison explaining evidence/configuration/rule/profile/outcome/disposition causes and worsening notifications.
- [ ] Implement deterministic stratified sampling, frozen cohort/version record, outcome/denominator rules, desired-outcome separation and regression gates.
- [ ] Assign reviewer eligibility/conflict procedure and preserve immutable correction/evaluation history.

Verification:

- [ ] TP-HAS-004, 010, 012 and 019 pass on fixtures before customer-environment use.
- [ ] Exactly 80% fails, indeterminate/unreviewed are excluded and visible, and no role can relabel a failed frozen evaluation.

### Milestone 9 — Deliver accessible canonical reporting and immutable publication

- [ ] Implement one frozen canonical report projection/manifest and audience projections for dashboard, Markdown and the renderer; bind baseline/catalog/profile/state/application versions and digests.
- [ ] Implement optimistic/atomic publication, explicit warning acknowledgment, immutable `ReportVersion`, deterministic retry and visible render failure.
- [ ] Build progressively disclosed executive/practitioner dashboard, stable snapshot cursors, saved views, lazy loading and graph/table/text equivalents.
- [ ] Implement structured Markdown with stable IDs and content parity.
- [ ] Implement Prince 17 only after the commercial license is available; isolate network, platform/customer credentials, database, arbitrary filesystem and canonical writes, allowing only the read-only vendor license file.
- [ ] Apply the approved accessibility plan to every essential flow and report output.

Verification:

- [ ] TP-HAS-006, 007, 011, 013, 017 and report portions of 020 pass; dashboard/PDF/Markdown values and canonical digest agree.
- [ ] Manual keyboard, screen-reader, 200%/400% zoom/reflow, graph equivalence and PDF reading-order/table/tag checks pass on the approved matrix.
- [ ] Any essential WCAG 2.2 A/AA failure blocks G6 and pilot acceptance.

### Milestone 10 — Add expiring links and complete operations/recovery

- [ ] Implement pre-rendered redacted report links with verifier-only token/passcode storage, separate passcode delivery, <=24-hour expiry, revoke, attempt lockout, generic denial, no cache/index/referrer leakage and access audit.
- [ ] Implement acknowledgment as an append-only event that cannot change findings, scores or reports.
- [ ] Complete soft-delete/purge jobs, backup inventory/integrity, isolated server restore, authorized customer extraction, tombstone/access/link/capability replay and reconciliation.
- [ ] Implement deployment, rollback, outage, authorization, integrity, broker/outbox, AI, render/share, purge, key and incident runbooks with named owners/escalation.
- [ ] Rehearse feature-flag rollout by sub-phase and customer, mixed worker/schema versions, safe drain and forward repair.

Verification:

- [ ] Link/acknowledgment portions of TP-HAS-009, 011 and 020 pass, including revoke/expiry during access.
- [ ] Daily backup evidence and same-region isolated restore meet the 24-hour RPO and one-business-day RTO without resurrecting deleted/revoked state.
- [ ] A region-wide Azure outage tabletop validates the manual business disaster plan without claiming a pilot regional RPO/RTO.
- [ ] Deployment/rollback, retention/purge and non-HA outage rehearsals close SEC-PILOT-005 and 006 and satisfy G7.

### Milestone 11 — Deliver Phase 1D read-only MCP

- [ ] Provision the separate approved API/resource and client registration; do not infer MCP rights from the UI registration.
- [ ] Implement named-user/service authentication and read-only resources for status, coverage, score/maturity, findings, recommendations and protected-reference metadata.
- [ ] Reuse the central policy/field filter with separate MCP grants, per-request revocation, opaque snapshot cursors and per-identity/customer concurrency limits.
- [ ] Enforce an explicit deny list for raw values, run/start, comments/edits, dispositions, risk, publication, exports/links/tasks and all mutation/customer-system operations.
- [ ] Emit payload-free audit with identity, resource/tool, scope, returned/redacted fields, outcome, correlation and latency.

Verification:

- [ ] MCP portions of TP-HAS-009, 014 and 020 pass, including fuzzed prohibited methods, identifier substitution, stale/revoked grants and rate/concurrency abuse.
- [ ] MCP result versions/digest match the canonical published manifest.

### Milestone 12 — Validate two environments and prepare release readiness

- [ ] Create protected records for PILOT-ENV-A/B and prove distinct administration/evidence baselines; clones/restores/repeated runs do not satisfy independence.
- [ ] Populate exact builds/hotfixes, SQL compatibility, modules, query pack, normalization schema, rule catalog, permissions, performance and evidence owners.
- [ ] Promote both rows through fixture/environment verification to `pilot-validated` with required independent approvals.
- [ ] Run initial and later reassessment in both environments without using product-driven source changes to manufacture scenarios.
- [ ] Execute every applicable repository/test-plan gate, cross-tenant penetration review, accessibility review, security verification and operations readiness review.
- [ ] Freeze and execute the evaluation cohort; require complete representation and strictly greater than 80% confirmed accuracy.
- [ ] Update feature status and all affected product/architecture/security/operations documentation; prepare the release-readiness review without performing a production release.

Verification:

- [ ] AC-HAS-1 through AC-HAS-20 have immutable evidence links and no untriaged acceptance failure.
- [ ] SEC-PILOT-001 through SEC-PILOT-010 are closed with evidence or explicitly block readiness; Critical/High findings are not silently accepted.
- [ ] G8 and G9 pass; release remains subject to explicit human approval.

## Test execution sequence

1. Every change: formatting, linting, type checking, unit/property tests, focused contract/integration tests, secret/dependency/license/SBOM scans, Bicep validation and affected documentation checks.
2. Every milestone: full affected integration/E2E/authorization/accessibility suite, migration-forward/rollback compatibility, artifact provenance and evidence capture.
3. Before enabling AI/render/share/MCP: the named gate's provider, sandbox, adversarial, authorization and negative-capability suites.
4. Before staging: full test plan on synthetic scale data; load/soak/outage, deployment/rollback, purge and isolated restore drills.
5. Before pilot acceptance: exact two-environment TP-HAS-015/016/019 evidence, full AC-HAS-1–20 regression, independent security/accessibility/operations review and frozen evaluation.

Failed, skipped, unimplemented or unexecuted checks are recorded as `NOT VERIFIED`. Test artifacts use opaque scope identifiers and must not contain credentials, tokens, customer names, evidence payloads, prompts/responses with evidence or unrestricted reports.

## Rollout and rollback

Roll out in dependency order: storage expansion; catalog/profile seed validation; policy/read paths; workers; orchestration/write paths; UI; deterministic analysis; AI; tasks/CSV; review/publication; links/reassessment/evaluation; MCP. Each capability is server-enforced, customer-scoped, disabled by default and first enabled for an internal synthetic validation customer.

Rollback disables the affected flag, stops new work, revokes suspect links, drains/cancels at safe checkpoints, and deploys a prior image only when current schema/job contracts remain compatible. Otherwise writes remain paused while a forward repair is deployed. Successful results and immutable publications are never deleted or rewritten. Database restore is reserved for verified loss/corruption and must use the approved isolated procedure.

Rollback triggers include cross-customer access, source-write capability, integrity/digest mismatch, uncontrolled disclosure, deletion bypass, systemic scoring/rule error, unsafe render/share behavior and material availability regression. Trigger handling and verification follow `docs/operations/health-assessment-pilot-operations-plan.md`.

## Documentation obligations

- Keep this plan and `status.md` current at each milestone and record material discoveries below.
- Update API/schema/message/catalog/query/prompt compatibility documentation with the implementing change.
- Update security findings with executed evidence, not implementation claims.
- Update infrastructure inventory, data-flow diagrams, configuration reference, runbooks, support ownership and recovery evidence before staging.
- Record exact capability rows, rule/query/prompt versions and evaluation artifacts in their canonical approved records.

## Completion gate

- [x] This plan is approved and all plan decisions are resolved.
- [ ] Phase 1A–1D implementation satisfies AC-HAS-1 through AC-HAS-20 with immutable evidence.
- [ ] All applicable formatting, linting, type, unit, property, contract, integration, E2E, security, accessibility, performance, recovery, compatibility and build checks pass.
- [ ] G0 through G9 pass; any exception has named authority, scope, expiry and evidence and does not contradict a mandatory gate.
- [ ] SEC-PILOT-001 through SEC-PILOT-010 are closed or pilot execution remains blocked.
- [ ] Both independent exact capability rows are `pilot-validated`; initial/reassessment and >80% evaluation gates pass.
- [ ] Feature status and affected product, architecture, security and operations documents are current.
- [ ] Technical, security, operations, quality, SME and accessibility reviews are complete.
- [ ] Human release-readiness approval is recorded; production release remains separately authorized.

## Discoveries and plan changes

Record material discoveries and approved deviations without rewriting historical decisions.

| Date | Discovery/change | Impact | Approval/evidence |
|---|---|---|---|
| 2026-09-29 | Initial canonical draft created. Environment/provider identifiers are execution gates, not planning prerequisites. | Planning may continue; implementation remains unauthorized. | Pending technical, security and operations review |
| 2026-09-29 | IMP-DEC-001 approved with .NET 10 LTS, Node 24 LTS, React 19.3, TypeScript 7, PostgreSQL 18, Bicep 0.47.16, OpenAI .NET 2.14.0 and the documented patch/API/container locking policy. | Software baseline resolved; five implementation decisions remain. | Repository owner approval in planning review |
| 2026-09-29 | IMP-DEC-002 approved with East US 2 as the only pilot region. No recovery region or geo-redundant standby is in pilot scope; regional outage follows a manual best-effort business disaster plan and is excluded from the pilot RPO/RTO. | Region decision resolved; four implementation decisions remain. Governing recovery documents amended. | Repository owner approval in planning review |
| 2026-09-29 | IMP-DEC-003 approved with signed self-contained .NET 10 Windows Service/CLI packaging, Server 2022/2025 support, SqlClient 7.1, integrated-auth preference, protected SQL-auth fallback, outbound enrollment, authenticated encrypted offline packages and manual signed updates. | Collector decision resolved; three implementation decisions remain. | Repository owner approval in planning review |
| 2026-09-29 | IMP-DEC-004 approved with Prince 17 as the isolated PDF/UA renderer, subject to commercial licensing and successful accessibility/security/determinism spike evidence. | Renderer decision resolved; PDF remains disabled until G6. Two implementation decisions remain. | Repository owner approval in planning review |
| 2026-09-29 | IMP-DEC-005 approved with a Windows 11 Enterprise desktop matrix covering Edge Stable, Chrome Stable, Firefox ESR, NVDA, Narrator and Acrobat Reader. | Accessibility support boundary and release-freeze evidence are explicit. One implementation decision remains. | Repository owner approval in planning review |
| 2026-09-29 | IMP-DEC-006 approved with a dedicated BFF user-assigned managed identity federated to the confidential Entra web registration. | All six implementation decisions are resolved. Entra sign-in remains disabled until the end-to-end federation spike passes; G0 still requires plan completion and review. | Repository owner approval in planning review |
| 2026-09-29 | Repository, contract and migration structure approved: separate BFF/worker roles, isolated renderer, Windows collector, bounded domain modules, versioned contracts and control/customer migration sets. | Bootstrap ownership and compatibility policy are explicit; exact public operation shapes still require technical approval before implementation. | Repository owner approval in planning review |
| 2026-09-29 | Three cumulative CI gates approved for change/merge, milestone completion and pilot readiness; required checks without executed passing evidence are `NOT VERIFIED`. | Check selection and acceptance are explicit. Pipeline implementation and first green run remain Milestone 0 verification work. | Repository owner approval in planning review |
| 2026-09-29 | Metadata-only evidence index and restricted East US 2 engineering artifact store approved, with sanitized digest-bound bundles retained 12 months from gate decision and purged from active storage within 30 additional days. | Evidence location, integrity, access and expiration rules are explicit; store implementation and tests remain `NOT VERIFIED`. | Repository owner approval in planning review |
| 2026-09-29 | Normal promotion workflow approved; repository owner explicitly requested authority to override every internal artifact-promotion review/evidence gate during the pilot. | Per-artifact owner exception can activate unverified work for limited pilot use but does not falsify check results, customer authorization, runtime safety or acceptance status. | Repository owner approval in planning review |
| 2026-09-29 | Synthetic fixture taxonomy approved across build/module compatibility, source permissions, rule classes, coverage/scoring, authorization, AI, rendering, work/recovery and independent 100,000-record scale categories. | Fixture ownership, version/digest and expected-result rules are explicit; actual fixtures and executed evidence remain `NOT VERIFIED`. | Repository owner approval in planning review |
| 2026-09-29 | Final G0 implementation plan approved after consistency review of the approved product/technical/test plans and accepted ADRs. | Feature advances to `PLANNED`; implementation may begin within the approved scope. G1–G9 and milestone execution checks remain `NOT VERIFIED`. | Repository owner approval in this planning review, covering technical approval and security/operations review |
| 2026-09-29 | Milestone 0 prototype verifies artifact signatures and strict, signed, metadata-only gate-check bundles with synthetic negative cases. ADR-0005 proposes the signer trust and key lifecycle decision. | No gate or promotion is activated; store, reviewer, CI, key-governance and promotion controls remain unverified. | Local build/tests; technical/security decision pending |
| 2026-09-29 | Repository owner accepted the RSA-PSS/SHA-256 signing profile and trusted-caller boundary in ADR-0005. | Production signing authority, key registry, rotation/revocation, reviewer attestation and store integration remain implementation/review gates; G1–G9 stay `NOT VERIFIED`. | Repository owner approval of ADR-0005 signing direction |
| 2026-09-29 | A partial engineering evidence-store Bicep module was compiled and linted with the pinned CLI. It defines East US 2 private Blob access and closed authentication/network defaults, with compiled-template drift tests. | No Azure deployment or gate evidence exists; network/DNS resources, identities, diagnostics, lifecycle purge and recovery remain open. | Local Bicep build/lint and synthetic infrastructure policy checks |
| 2026-09-29 | Partial Service Bus Standard namespace and queue-scoped sender/receiver role modules were added for Milestone 1. The IP rule derives from an existing public IP resource; eighteen unsafe template drifts are rejected. | No NAT linkage, actual role grant, queue, diagnostic resource, Azure deployment or live denial test exists. G1 remains `NOT VERIFIED`. | Local Bicep build/lint and compiled-template policy checks |
| 2026-09-29 | A VNet/subnet/static-IP/NAT draft for Container Apps egress was added; compiled-template policy rejects nine network drifts and fourteen synthetic address-plan cases pass. | Actual environment CIDRs, capacity, effective egress, private endpoint linkage and Azure deployment remain unverified; the earlier Service Bus draft now has a matching static-IP resource but no deployed linkage. | Local Bicep build/lint, compiled-template policy and synthetic address-plan checks |
| 2026-09-29 | A reusable user-assigned managed-identity module was added for separate workload instantiation. | No workload attachment, federated BFF credential, live role assignment or identity-denial proof exists. | Local Bicep build/lint |
| 2026-09-29 | A partial non-production G1 spike entry point composes the network/NAT and broker namespace modules in dependency order; five wiring drifts are rejected. | No Azure deployment was attempted; CIDR allocation, capacity, live network/auth denial, queue, roles, Container Apps and diagnostics are still pending. | Local Bicep build/lint and compiled-template wiring policy |
| 2026-09-29 | A separate signed owner-override metadata bundle binds exact target IDs, waiver set, protected rationale digest, suspension target and expiry under ADR-0008. A local key registry checks owner/scope, signing window and revocation. | This is operator-review evidence only. Production owner identity/key source, rationale storage, activation, audit, disclosure and expiry shutdown remain unimplemented; no internal gate is waived. | 24 bundle and 13 key-trust synthetic checks |
| 2026-09-29 | A pure default gate-evidence retention clock and boundary checks were added; ADR-0006 proposes separate intake, verification and purge Blob identities. | The clock does not control storage or holds, and ADR-0006 is not approved. No RBAC grant or purge job was deployed. | Local synthetic clock checks; technical/security/operations review pending |
| 2026-09-29 | Repository owner directed continued pilot implementation without interim security-review pauses and will review at the end of each milestone. ADR-0006 Option B is implemented as a draft role module with compiled-template drift checks. | Interim design choices remain visible for milestone review. No Azure role grant, deployment or execution gate is claimed; applicable checks and runtime safety boundaries remain in force. | Repository-owner direction in development conversation; local Bicep and policy checks |
| 2026-09-29 | Gate-bundle verification now binds the expected decision time and uses a local trust registry for key ID, gate scope, signing window and revocation checks. | The trust source and signer are still absent; no gate or promotion path is enabled. | Synthetic wrong-time, wrong-key, wrong-scope, rotation and revocation checks |
| 2026-09-29 | A pinned Gitleaks directory scan was added to the partial bootstrap workflow and run locally on the checkout. | No leak was found locally; remote CI and full change/merge security gates remain unverified. | Gitleaks `8.30.1` local checkout scan |
| 2026-09-29 | The normal promotion machine-evidence preflight now binds artifact and gate signatures to one digest; 16 synthetic threat cases pass. NuGet Audit is explicitly enabled for direct/transitive .NET packages and treats advisories as restore errors. | Reviewer authority, compatibility, owner override, actual promotion/audit, remote CI and store retrieval remain unimplemented; this preflight cannot activate anything. | Local unit cases, locked restore and inspected MSBuild audit properties |
| 2026-09-29 | Signed metadata-only reviewer decisions, a reviewer key/role/scope trust snapshot and required-role coverage were added as an internal pilot draft under ADR-0007. | Identity-backed trust, live role/revocation lookup, compatibility, owner override, activation/audit and store retrieval remain open. Local proofs cannot authorize promotion. | Synthetic reviewer bundle, wrong-key/scope/role, revocation, self-approval and stale-proof checks |
| 2026-09-29 | Normal-path preflight now combines machine and reviewer evidence for the same artifact digest and opaque scope. | It returns only evidence readiness for compatibility review; no promotion state changes, owner override, audit or activation exist. | 12 combined negative and success cases |
| 2026-09-29 | Draft PR #1 was pushed with the approved pilot foundation; the partial bootstrap workflow passed on commit `ea27cc6` (run `36651093133`). | Remote verification covers the configured partial checks only. Full change/merge gates, signed execution evidence and G1–G9 remain open. | [GitHub Actions run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36651093133) |
| 2026-09-29 | A Service Bus work-queue draft now enables duplicate detection and dead-lettering on expiration; the G1 spike composes it after the namespace. | No deployed queue, message contract, roles, diagnostics, expiry policy or live delivery proof exists. | Local Bicep build/lint and five queue plus eight wiring drift checks; remote run pending for this addition |
| 2026-09-29 | The work-queue draft passed the partial remote bootstrap workflow on commit `4658776` (run `36651579793`). The spike now instantiates separately named sender/receiver identities and binds their principal IDs to queue-scoped roles. | No application workload is attached; Azure deployment, role propagation, unauthorized access denial and effective egress remain unverified. G1 stays `NOT VERIFIED`. | [GitHub Actions run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36651579793); local Bicep build and thirteen wiring drift checks for identity/role addition |

## Approval

Technical owner: Repository owner — approved 2026-09-29  
Security review: Repository owner — reviewed and approved 2026-09-29  
Operations review: Repository owner — reviewed and approved 2026-09-29  
Product scope confirmation: Repository owner — approved pilot scope unchanged  
Date: 2026-09-29

Only authorized human reviewers may change this plan to `Approved` and advance the feature to `PLANNED`.
