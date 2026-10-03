# Implementation Plan: One Identity Manager Health-Assessment Pilot

Status: Approved — G0 passed; implementation authorized within this plan  
Product spec: `specs/003-health-assessment/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/003-health-assessment/technical-spec.md` (Approved 2026-09-28)  
Test plan: `specs/003-health-assessment/test-plan.md` (Approved 2026-09-28)  
Owner: Technical owner  
Reviewers: Product owner, security owner, operations owner, quality owner, One Identity SME, accessibility reviewer  
Last updated: 2026-10-03

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

The [local pilot build track](../../plans/active/one-identity-local-pilot-build.md), approved by the repository owner on 2026-10-01, completes implementation and synthetic verification without making A/B source validation a development prerequisite. It is a separate checkpoint before live evaluation. External gate evidence remains separate from local code completion. The G2/G8 rows below constrain live source collection and environment-based evaluation, while G1/G3/G5/G6/G7 constrain their respective deployed capabilities. A missing environment value is a typed gap or blocked activation, not a reason to stop unrelated local work.

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

Initial environment setup was verified on 2026-10-01 with an East US 2 group and USD 50 monthly budget. The owner then approved the isolated CIDRs and USD 15 / 24-hour test plus exact disposal. The reviewed partial Bicep spike deployed successfully; management reads verified NAT linkage, broker closed defaults, queue settings and distinct queue-scoped sender/receiver grants. Sanitized results were preserved before deleting the six disposable resources; the group is empty and the budget remains. PostgreSQL registration, remaining planned provider regions/APIs and Entra Global Administrator/Premium P2 prerequisites were verified. [The setup handoff](../../docs/development/azure-pilot-development-setup.md#approved-deployment-execution--2026-10-01) records results and open full-platform, workload, private-connectivity, identity-policy and live denial/recovery tests. G1 remains `NOT VERIFIED`; this partial configuration exercise does not close its deployment/teardown acceptance item. Budgets alert without stopping spending and final charges remain delayed.

The Service Bus Standard namespace, work queue and queue-scoped sender/receiver role drafts compile with the pinned Bicep CLI and pass compiled-template policy that rejects twenty-three unsafe drifts. A separate VNet/NAT draft creates the static IPv4 address used by the namespace rule and rejects nine infrastructure drifts. A partial non-production spike entry point composes network, broker, queue, two distinct managed identities and queue-scoped role grants in dependency order, with thirteen wiring-drift checks. Fourteen synthetic address-plan checks exercise private containment, reserved-range exclusion, disjointness and subnet size; the isolated test CIDRs were owner-approved and exercised against Azure. The reusable identity module has no workload attachment. Container Apps, diagnostics, full platform deployment and live network/auth tests are still open. G1 remains `NOT VERIFIED`.

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

- [ ] Complete the approved Windows Service/CLI collector; the pilot-local host shell is implemented under standing preapproval, while source, installer and delivery dependencies remain gated.
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

A local, side-effect-free coverage reconciler checks one terminal result per trusted planned inventory/category key and explanations for gap states. Unweighted state counts, reason-level limitation projections, executable-coverage numerator/denominator and a complete/complete-with-gaps classification are denied unless that reconciliation succeeds. The executable measure excludes explicit `not_applicable` units from its denominator but retains gaps; it does not score health or label zero applicable units as perfect coverage. A separate progress projection accepts missing items while rejecting other malformed results, including on a 100,000-key synthetic plan. The classification is not a durable run transition. None has a baseline reader, applicability planner, run state, authorization, persistence, UI or complete quality/scoring path; TP-HAS-001 and all execution gates remain `NOT VERIFIED`.

A local capability-start guard now compares trusted registry and baseline descriptors across exact product, database schema, hotfix-set, SQL build/compatibility, module inventory, query-pack and normalization versions, and freezes a deterministic version lock that includes the rule catalog and matrix version. It denies suspended/unsupported rows and mismatches. It does not load or validate the source manifest, promote a registry row, authorize an actor or customer, persist a lock, or start work. The capability-registry lifecycle item below remains open until those integration paths exist.

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
| 2026-09-29 | The work-queue draft passed the partial remote bootstrap workflow on commit `4658776` (run `36651579793`). The spike now instantiates separately named sender/receiver identities and binds their principal IDs to queue-scoped roles; this addition passed the partial remote workflow on commit `8d6c02b` (run `36651897279`). | No application workload is attached; Azure deployment, role propagation, unauthorized access denial and effective egress remain unverified. G1 stays `NOT VERIFIED`. | [Queue run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36651579793); [identity/role run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36651897279); local Bicep build and thirteen wiring drift checks |
| 2026-09-29 | Internal Phase 1A coverage reconciliation was added with thirteen synthetic cases and wired to the partial bootstrap workflow. | This checks terminal-result completeness only. Expected inventory generation, baseline eligibility, persistence, scoring and end-to-end TP-HAS-001 remain open. | Local Release build and synthetic cases; [partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36657204155) passed on `44915de` |
| 2026-09-29 | A feature-001 collector safety prototype now classifies typed permission-probe outcomes, blocking missing minimum-read proof and all reported write/admin categories while marking excess read-only for warning. | It opens no SQL connection and cannot start collection; the approved query pack, trusted probe, warning/audit path, feature-001 C0 and G2 remain open. | Seventeen local synthetic permission cases; [partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36657204155) passed on `44915de` |
| 2026-09-29 | A local page-checkpoint verifier now binds query/build/scope/policy/order/boundary and digest, distinguishing replay from conflict. | It performs no source read, durable storage or atomic checkpoint write; G2 and feature-001 C0 remain open. | Twelve local synthetic cases; [partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36657204155) passed on `44915de` |
| 2026-09-29 | An unweighted coverage-state count projection now requires complete reconciliation and includes a 100,000-item synthetic case. | No score, applicability or end-to-end scale claim follows from this local count. | Local cases and remote run pending |
| 2026-09-29 | Repository owner approved the feature-001 pilot collector technical specification, implementation plan and test plan for local development; exact query packs and delivery contracts retain separate gates. Exact-build/module, page-budget and static T-SQL shape guards were added locally. | A Windows service, source query, signed package, cloud upload and G2 evidence remain unavailable until their corresponding contracts and environments are validated. | Local synthetic guard cases; remote run pending |
| 2026-09-30 | Under repository-owner standing pilot-local preapproval, added a Windows Service/CLI host shell with strict protected config, fixed verbs and a daylight-saving schedule. | The shell reports blocked state and performs no SQL read or package export. Windows install, exact query pack, signed MSI, delivery, two-environment and G2 evidence remain open. | CollectorHost contract cases and local build; remote run pending |
| 2026-09-30 | Added a local cross-process file lease and authenticated encrypted append-only page checkpoint ledger. | These are isolated primitives; protected key provisioning and actual collector run integration remain open. | Synthetic overlap, restart-release, wrong-key/context, tamper and immutable-prefix cases |
| 2026-09-30 | Added a Windows DPAPI `LocalMachine` protected-key reader and Windows 2022/2025 host test jobs. | CI checks local crypto/ACL behavior only; customer MSI/service/Server Core and G2 remain open. | [Partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36702668583) passed on both Windows runner versions |
| 2026-09-30 | Added a Service Control Manager start/stop and blocked one-shot smoke script to the Windows runner jobs. | This adds synthetic service-host evidence; MSI provenance, customer identity, Server Core, source and G2 remain open. | [Partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36703803467) passed on Windows 2022 and 2025 after correcting the smoke harness's stderr capture |
| 2026-09-30 | Added an explicit-allowlist field minimizer to the collector safety core. | It can return permitted values or value-free reason markers, but signed field policy, exact dictionary and real pre-staging enforcement remain open. | Twelve local synthetic cases and [partial remote run](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36704790822) passed |
| 2026-10-01 | Expanded the feature-001 local collector's recovery fixtures to cover a staged page with failed checkpoint persistence, changed-content restage and force-terminated lease holder. Added a blocked-service restart step to the Windows smoke script and drafted delivery/installer review questions. | The pinned SDK local suite and `win-x64` publish passed; [partial bootstrap run 36868281712](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36868281712) passed on Linux and both Windows runner versions. Production durable staging remains open. Enrollment/upload, offline package and MSI contracts are not approved; G2 remains `NOT VERIFIED`. | Feature-001 synthetic coordinator and cross-process tests, local format/build/publish, remote Windows service restart smoke |
| 2026-10-01 | Added a reconciled executable-coverage numerator and applicable planned denominator for the Phase 1A quality foundation. | Pass/finding units count as executed; explicit not-applicable units leave the denominator; gaps remain. No health score, run completion, trusted plan or full quality report follows. | Six local synthetic cases, including 100,000 planned units; remote checks pending |
| 2026-10-01 | [Partial bootstrap run 36901896983](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36901896983) passed on `8788c05` across Linux and Windows 2022/2025. | The executable-coverage measure passed the repository's current partial CI. Baseline eligibility, end-to-end quality reporting and G2/G4 remain unverified. | Linux format/build/unit/architecture/secret/Bicep checks and Windows collector publish/service smoke |
| 2026-10-01 | Added a pure exact-version capability lock guard and 13 synthetic checks, and verified the supplied Environment A report digest and cover. Public vendor guidance identifies a version-information view for the next protected source read. | The guard is not wired to a trusted registry, baseline adapter, authorization or durable run start. The A edition/build discrepancy and G2/G8 remain open. | Local pinned SDK locked restore, format, Release build, unit and architecture checks; [partial bootstrap run 36918795336](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36918795336) passed on `f9ae962`; [A partial SME response](../001-data-ingestion/one-identity-environment-a-sme-response.md) |
| 2026-10-01 | The Environment A dedicated metadata principal reconnected. A bounded owner diagnostic found `STE`/`10.0` in the current version view's Installed Edition entry; a system-catalog read confirmed schema-column candidates. No broad view-value grant or source collection followed. | The July report and active module builds still do not establish the authoritative current application/schema build. A protected System information export and SME interpretation, hotfix/status evidence, source binding and reviewed field/query plans remain open. G2/G8 remain `NOT VERIFIED`. | [A partial SME response](../001-data-ingestion/one-identity-environment-a-sme-response.md) and provisional owner-only diagnostics; no acceptance evidence |
| 2026-10-01 | Added a pure coverage completion projection that classifies a reconciled plan as complete or complete with gaps, reusing the limitation grouping so unexplained, missing or duplicate results cannot be classified. | This does not create a run state, accept an empty plan as eligible, authorize a baseline, score health or persist completion. Trusted plan generation and durable start/completion remain open. | Twelve synthetic completion cases; local Release build and format check |
| 2026-10-01 | [Partial bootstrap run 36923312658](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36923312658) passed on `2840bec` across Linux and Windows 2022/2025. | The pure completion classification passed the current partial CI. Trusted run planning, durable state and G2/G4 remain unverified. | Linux format/build/unit/architecture/secret/Bicep checks and Windows collector publish/service smoke |
| 2026-10-01 | Repository owner directed local pilot completion before further Environment A/B validation. | [The cross-feature local build plan](../../plans/active/one-identity-local-pilot-build.md) and amended product/test plans separate synthetic build completion from later live operational acceptance. G1–G9 retain their factual `NOT VERIFIED` status. | Owner direction; canonical feature, collector and product plans updated |
| 2026-10-01 | Owner approved the USD 15 / 24-hour isolated spike and cleanup. Exact reviewed Bicep deployment succeeded; management configuration and queue scopes verified; six disposable resources removed after preserving results. PostgreSQL registration and remaining provider/Entra prerequisites verified. | Configuration-only progress; group and USD 50 budget retained; G1 remains NOT VERIFIED. | [Sanitized execution record](../../docs/development/evidence/azure-pilot-spike-20261001.json); authenticated Azure/Entra session |

## Approval

Implementation update 2026-10-01 (cycle 02): [the second parallel cycle](../../plans/completed/local-pilot-parallel-cycle-02.md) adds a versioned value-free synthetic baseline-to-inventory planner, 62 focused planner cases and 11 independent baseline-to-progress/coverage fixtures with 65 assertions. The combined local pinned audited locked restore, solution formatting, Release build, current unit/integration/architecture hosts and self-contained Windows cross-publish passed. The integration host now has 17 fixtures and 143 assertions. Scope remains a synthetic projection using caller-supplied applicability and already-authorized scope; the production manifest/eligibility adapter, semantic applicability, durable start/recovery, authorization and UI remain unimplemented. The new collector receiving contract is an unapproved disabled review candidate with planned, unexecuted crypto/import/installer vectors. No Milestone 3/4 or G1–G9 checkbox is completed by these packets. Cycle 02 [partial bootstrap run 36940634611](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36940634611) passed on `918d48e` across Linux and Windows 2022/2025, including integration, pinned secret/Bicep checks, protected-key/ACL and collector service smoke. These are partial engineering checks; full-feature and live gates remain NOT VERIFIED.

Implementation update 2026-10-01: the owner approved [parallel cycle 01](../../plans/completed/local-pilot-parallel-cycle-01.md), delivered by a coordinator and three isolated workers. A pure capability-bound terminal coverage composition and six versioned independent integration fixtures are now executable; the collector checkpoint retry preserves ciphertext at the existing aggregate cap. Pinned audited locked restore, solution formatting, Release build, all current unit/integration/architecture hosts and Windows cross-publish passed locally. The assessment host passed 13 guard and 25 composition cases; integration passed 78 assertions. These are developer evidence subsets, not completion of Milestone 4 or TP-HAS-001/007; trusted baseline/planning, authorization, durable work and UI remain open. [partial bootstrap run 36935964103](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36935964103) passed on `ed3a77b`, including Linux integration, secret/Bicep checks and both Windows collector jobs. The private site and human tasks board were published with confirmed native deployment; cycle 01 is complete.

Technical owner: Repository owner — approved 2026-09-29  
Security review: Repository owner — reviewed and approved 2026-09-29  
Operations review: Repository owner — reviewed and approved 2026-09-29  
Product scope confirmation: Repository owner — approved pilot scope unchanged  
Date: 2026-09-29

Only authorized human reviewers may change this plan to `Approved` and advance the feature to `PLANNED`.

Implementation update 2026-10-01 (cycle 03 started): the owner requested durable assessment runs/recovery followed by consultant view development. [The exact local packet](../../plans/completed/local-pilot-durable-consultant-cycle-03.md) bounds the synthetic PostgreSQL engine, private loopback-only demo transport and actual consultant UI; isolated workers own persistence, frontend and independent failure/browser verification. Production public contracts, Entra/customer trust, scoring/publication and G1–G9 are unchanged. Checks and Site publication are pending.

### Azure foundation continuation execution note — 2026-10-01

The [Azure continuation cycle](../../plans/active/azure-development-foundation-cycle.md) executed the [approved session](../../docs/development/azure-foundation-session-proposal.md) after Mac unlock. Provider validation/preview succeeded; 41 management checks passed after vault serialization repair and exact database administrator retry. Initial root deployment failed; the corrected root has not been replayed live. Sanitized checks were saved before exact cleanup, now verified. No G1 prerequisite is waived. Remaining hosted application images/tested BFF endpoints/trust, actual licensed users/reviewed Conditional Access scope and live gate tests require implementation/evidence before acceptance.

Implementation update 2026-10-01 (cycle 03 local packets): [the durable/consultant packet](../../plans/completed/local-pilot-durable-consultant-cycle-03.md) now provides a PostgreSQL-backed internal synthetic run engine and an actual loopback-only ASP.NET/React view. Inputs, scripted outcomes and successful results remain immutable across crash/replay; database-clock lease/revision fences and atomic checkpoint/outbox writes have real process/database tests. The screen selects fixed presets, starts/cancels/resumes, shows persisted history, locks, progress and gaps, and pauses at coverage readiness without inventing scoring completion. D3 passed 14 pure and 18 PostgreSQL groups; independently authored final-source V3 passed 86 PostgreSQL assertions and six browser groups / 392 checks. Coordinator repeated the current repository/frontend/infrastructure/secret checks and 80 PostgreSQL assertions without another cluster restart. Axe found no tested-view violations; the 320px contrast rule was incomplete and full Windows/manual acceptance remains NOT VERIFIED. New exact EFCore/Relational 10.0.12, Npgsql provider 10.0.3, React 19.3/TS 7.0.2/Vite 8.3.1 pins and locks, synthetic-only additive migration and private demo DTO generator are documented. The Linux and Windows 2022/2025 [partial CI run 36945923151](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36945923151) passed; owner-private publication and cycle closure are confirmed. No full Milestone 4, local pilot completion or G1–G9 acceptance follows.

Implementation update 2026-10-01 (cycle 04 started): the owner directed the next phase. [The bounded Phase 1B packet](../../plans/completed/local-pilot-analysis-scoring-cycle-04.md) assigns isolated deterministic-rule, scoring and independent verification workers; coordinator integrates the actual consultant demo and exact frozen fixture inputs. This implements approved internal synthetic behavior, not a live rule promotion or completion of Milestone 5. Checks and private publication are pending.

Implementation update 2026-10-01 (cycle 04 local slice): [the Phase 1B packet](../../plans/completed/local-pilot-analysis-scoring-cycle-04.md) now connects fixed typed synthetic rules, immutable grouped findings and exact frozen catalog/profile versions to decimal health/quality projections in the existing consultant screen. New presets opt in while historical coverage runs retain compatibility. Independently reviewed pure checks (175 assertions / 92 groups), 399 actual PostgreSQL assertions and the prior 392-check recovery regression passed; final browser verification passed 712 checks. The Linux and Windows2022/2025 [partial CI run36951595057](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36951595057) passed, and owner-private publication is confirmed; the bounded cycle is closed as developer evidence. No production catalog, review permission, AI, maturity/task/publication behavior or G1–G9 acceptance follows. The [catalog promotion task](../../docs/development/deterministic-catalog-review.md) states required attributable reviewers and exact fixture/build/digest evidence for later real use.

### Azure application package preparation — 2026-10-01

The owner requested the next Azure preparation step. [The bounded package cycle](../../plans/active/azure-application-package-cycle.md) adds an explicit inert web/worker bootstrap executable, immutable Microsoft base-image lock, non-root allowlisted container recipe, private Consumption hosting templates and independently authored refusal/shutdown/drift checks. Local build/format/publish/process and 44 negative hosting checks passed; container CI and real BFF/worker integration are separate. The existing synthetic consultant host retains loopback-only protection and is not deployed. No public product contract, identity grant, sign-in, data migration, telemetry collection or paid Azure operation follows. G1 remains NOT VERIFIED;[package handoff](../../docs/development/azure-application-package.md) defines the actual workload, image promotion, pull, identity and priced-redeployment dependencies.

Package execution update: [partial run 36953664310](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36953664310) passed actual Linux AMD64 image build and non-root/read-only web/worker smoke on `ae785af`; image supply-chain acceptance, published private registry pulls, real BFF/worker integration and G1 remain NOT VERIFIED.

Implementation update 2026-10-01 (cycle05 approval): repository owner explicitly approved [the exact review/maturity proposal](../../plans/completed/local-pilot-review-maturity-cycle-05.md), including new opt-in private routes, server-supplied synthetic consultant test identity, separate append-only review schema and fixed maturity denominator. Isolated maturity/review/verification workers implement this bounded local packet; coordinator owns shared integration and private status Site. No production customer authority, risk acceptance, public contract or live gate is granted.

Implementation update2026-10-02 (cycle05 local verification): [the approved packet](../../plans/completed/local-pilot-review-maturity-cycle-05.md) now implements append-only synthetic review/comments/presentation, immutable originals, same-snapshot health/quality and independent evidence-based maturity in two new opt-in profiles. Independent review findings are closed; final local342 integration/2,162 browser assertions and historical391/712 browser regressions passed, alongside full restore/format/build/current suites/frontend/infrastructure checks. Four literal old-profile envelopes/digests remain compatible. Configured Linux/Windows/Azure package CI passed; owner-private publication is confirmed. These partial Milestone5 behaviors do not check off full milestone, Phase1B/1C or G1–G9 acceptance.

## BFF identity/session local foundation — 2026-10-02

The owner's “Work on it” implemented the approved Milestone 2 identity/session and human policy foundation in a separate reviewable `codex/bff-integration` branch. Live sign-in defaults to disabled. Reviewed shared PostgreSQL sessions, supported Microsoft OIDC registration/guards, secure cookies/CSRF and deny-by-default human action policy now compose in actual HTTPS tests. Final results: 72 transport assertions, 27 session unit cases, 100 PostgreSQL checks, 2546 policy cases and 84 two-server HTTPS checks, the last independently repeated at `3781b3b`. Pinned 33-project restore/format/Release build (zero warnings/errors), all current local unit/architecture checks, existing assessment database regressions and infrastructure policies passed. [Evidence](../../docs/development/evidence/bff-identity-session-20261002.json), [review](../../docs/development/evidence/bff-foundation-review-20261002.md) and [handoff](../../docs/development/bff-identity-session-foundation.md) record limits. An additive control-plane session migration and inert federation input template are stored in Git; no Azure schema/grant/trust or customer data was created.

The owner explicitly approved public branch publication and a draft pull request on 2026-10-02. [Draft PR #2](https://github.com/pedrovolua-pixel/IGAMigrationTool/pull/2) is published and remains unmerged. The combined code snapshot `145de02af6537c2532c5cdd2ca0bd3e4117e21d4`, based on completed pilot source `0a58364`, passed local locked restore, formatting and the 38-project Release build with zero warnings/errors; the BFF runtime/test source is unchanged from independently reviewed `3781b3b`. [Hosted bootstrap checks](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37001243915) passed Linux and Windows 2022/2025, including actual shared PostgreSQL BFF sessions/HTTPS, existing database/browser regressions, dependency boundaries, secret and infrastructure checks. [Hosted package checks](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37001244041) passed the inert non-root container and hosting-policy checks. The earlier publishing blocker is resolved and the human publication dependency is closed. Production host/adapters, provider/federation/Conditional Access proofs, key lifecycle, field filtering, routing/audit and full cross-path authorization remain open. Milestone 2 and G1–G9 remain NOT VERIFIED; publication does not authorize draft merge, live sign-in or new Azure resources. Owner-private board version57 publication is confirmed on 2026-10-02; it closes the public-publication task while retaining production identity and live gate dependencies.

Implementation update2026-10-02 (cycle06 start): owner requested the next local cycle. [The bounded draft-report packet](../../plans/completed/local-pilot-draft-report-cycle-06.md) maps approved FR-HAS-21/40/43–46 and report-parity tests to read-only canonical synthetic draft values, summary/technical views and inert Markdown preview in the existing private analysis response. No publication/run-state/persistence/authority change is introduced; current Scoring runs stay draft-only. Isolated projection, Markdown and independent verification packets receive non-author review; checks/private publication are pending.

Implementation update2026-10-02 (cycle06 local verification): [canonical draft preparation](../../plans/completed/local-pilot-draft-report-cycle-06.md) is runnable through the existing private analysis read with detached values, strict frozen bindings and same-value summary/technical/inert Markdown. Non-author findings are closed. Local180 draft/114 Markdown/201 independent source-database/2137 new browser assertions and historical regressions passed, as did current full-solution/frontend/infrastructure checks. Configured Linux/Windows/container partial CI passed on f4c7f82; owner-private publication is confirmed and the bounded cycle is closed. This prepares a bounded Milestone9 value/projection subset; no milestone, report publication/history, PDF or live gate is accepted.

Implementation update2026-10-02 (cycle07 local verification): [structured recommendation guidance](../../plans/completed/local-pilot-recommendation-guidance-cycle-07.md) is runnable through the existing private analysis read with complete detached original options/provenance and captured current finding presentation. Finding review cannot approve advice. Independent review and287 module/47 rendering/257 source-database/2980 final guidance browser assertions, four historical regressions and37-project/frontend/infrastructure checks passed. Configured Linux/Windows/container partial CI passed on6c53e66; owner-private closure publication is confirmed and the bounded cycle is closed. This is a bounded FR-HAS-35–37/IP-HAS-008/Milestone7 subset; no milestone, fix review/tasks/CSV or live gate acceptance.

## Cycle08 bounded Milestone6 foundation — 2026-10-02

[Cycle08](../../plans/completed/local-pilot-synthetic-ai-cycle-08.md) advances IP-HAS-007/FR-HAS-30–33 and the local schema/citation/data-only/proposed subset of AC-HAS-8/TP-HAS-008 through the [settled internal fixture contract](synthetic-ai-fixture-contract.md). Its real builder/fixed fake provider/real output validator portable path passed 1573 new assertions across three hosts, independent full canonical goldens and source/member/conflict/hostile/oversize/duplicate denials. Non-author packet reviews and combined 41-project local verification passed; hosted Linux, both Windows and package checks passed, including all five browser workflows; owner-private publication succeeded; the bounded cycle is COMPLETE. [Exact evidence](../../docs/development/evidence/local-pilot-cycle08-developer-checks.json) records actual source/checks and unverified cases. All full Milestone6 checkboxes remain open: this module cannot authenticate a caller, prove semantic redaction or factual accuracy, detect undeclared contradictions, reserve production budgets, invoke a real model or change saved findings/scoring/review/publication. No architecture, production permission, public API, migration, dependency or historical disabled-AI profile decision changes.

## Cycle09 bounded offline presentation subset — 2026-10-02

[Cycle09](../../plans/completed/local-pilot-ai-preview-cycle-09.md) advances the local FR-HAS-30–32/40–42, AC-HAS-8/13 and IP-HAS-007 presentation subset under the [internal test-only preview contract](synthetic-ai-preview-contract.md). Actual validated immutable fictional input produces a complete Proposed projection and framework-encoded deterministic inert HTML; no evidence resolution, action, application route, persistence, scoring/review promotion or live provider. Independently authored full byte/digest goldens and substantive negative cases passed 921 portable assertions; 440 actual browser checks passed with explicit control-token normalization limits, empty semantics, keyboard/reflow and negative network/action evidence. Non-author reviews, combined 45-project local verification and configured hosted Linux/Windows/container checks passed; [exact evidence](../../docs/development/evidence/local-pilot-cycle09-developer-checks.json) records the initial Windows fixture checkout failure and unchanged-assertion fix. Worker source/needed output archives are verified and writing checkouts removed. Matching owner-private publication succeeded; the bounded cycle is COMPLETE. All full milestone/TP checkboxes remain open; supported manual accessibility, provider eligibility/US/ZDR/quality/budgets/recovery/retention, exact task/CSV contracts and real report publication remain later work. No approval or architecture/security boundary is inferred from agent review.


## Cycle10 offline AI workspace integration — 2026-10-02

[Cycle10](../../plans/completed/local-pilot-ai-workspace-cycle-10.md) connects the actual Cycle08/09 offline builder, validator and Proposed preview to the localhost consultant workspace. A dedicated fictional configuration baseline and two opt-in fixed-response profiles support complete proposed explanations or an explicit empty response. Immutable run/input/template/fixture bindings and complete canonical consistency checks deny mismatched reads; historical six-profile locks and deterministic scores remain unchanged. Proposed text stays separate from findings, scores, review, guidance and reports. No real provider, AI persistence, production migration, package, public API or permanent runtime setting was added.

Non-author reviews found no unresolved issues. The fixture unit suite passed485 assertions, independent component/coherence checks257, independent actual PostgreSQL replay282 (including78 portable composition checks), and final formatted actual-browser replay637. All five prior browser flows plus the standalone Cycle09 preview passed; readiness-poll counts vary. Combined pinned47-project audited locked restore, whole formatting, zero-warning/error Release build, current unit/database/architecture/frontend/infrastructure/secret checks passed. [Exact evidence](../../docs/development/evidence/local-pilot-cycle10-developer-checks.json) distinguishes original worker outputs, coordinator executions, hosted checks and limits. Original476 worker bindings and2,907 preserved file entries were independently rechecked before removing the three writing checkouts. Concurrent BFF/UI/research edits and committed draft UAT guidance were preserved.

A local macOS draft-host restart stalled before listening; the native trace strongly supports .NET file-watcher startup waiting in a global filesystem flush. Final draft/guidance/Cycle10 browser replays used temporary `DOTNET_hostBuilder__reloadConfigOnChange=false`; product configuration was unchanged. Default macOS startup reliability remains NOT VERIFIED. Hosted Linux exercises default startup separately. Local shared-cluster restart DUR-PG-008 was not rerun. Manual supported accessibility, real-provider/source/identity/security, full milestones and G1–G9 remain NOT VERIFIED. The new human UAT plan remains draft; full local pilot completion and UAT readiness are not claimed.

Hosted [Linux/Windows bootstrap](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37049034667) and [container package](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37049034557) checks passed on code `0b95ca6`, including default Linux startup and all seven browser flows. Matching owner-private publication succeeded; the bounded Cycle10 is COMPLETE. Full pilot/UAT/milestones/G1–G9 acceptance remains open.

## Cycle11 fictional fix-package preview — complete

[Cycle11](../../plans/completed/local-pilot-fix-packages-cycle-11.md) delivers a separate fixture-only builder and inert complete HTML preview of generic fictional SQL/script/configuration artifacts bound to existing guidance group/option identities and full source provenance. Every artifact stays Unverified; no supported One Identity remediation, application integration, consultant artifact approval, task/CSV/export, execution, persistence/migration or customer/provider activation. FR-HAS-35/37/42, IP-HAS-008, AC-HAS-18/14 and TP-HAS-018/014/002/003/017/009/013 are bounded engineering subsets only.

Independent reviews closed without scoped findings.1699 builder,311 original renderer (314 coordinator permitted-environment assertions),338 composition and4578 actual browser checks passed;9 new axe audits had zero violations/incomplete.51-project local restore/format/zero-warning build and all applicable current local regressions passed, including seven prior browser suites. Configured Linux, Windows2022/2025 and container checks passed on code `265c3ed`; the initial Windows build-environment harness failure and unchanged-source direct-DLL correction are retained in [exact evidence](../../docs/development/evidence/local-pilot-cycle11-developer-checks.json). All210 original bindings,2415 worker archive entries and1946 coordinator entries rehashed; clean writing checkouts removed after preservation. Concurrent BFF/UI/research edits and shared PostgreSQL were preserved.

Matching owner-private publication succeeded (version78, exact Site source `cb326730a83d0406bcca8b8ab472496148ec8fa8`, deployment `appgdep_6ac0154a30e08191a9ee73df190b31d8`); the bounded cycle is COMPLETE. The final docs-only COMPLETE snapshot is republished in this same cycle. Full Milestone7/local pilot/UAT readiness/TP018/G1–G9, deployed ADR-0001 renderer isolation, supported manual accessibility and default macOS startup remain NOT VERIFIED. Historical Mac app browser replay used a temporary test-only configuration-watch override; shared-cluster restart DUR-PG008 was not rerun. No new dependency, migration or permanent runtime setting.

## Cycle12 local fictional fix-package integration — complete

[Cycle12](../../plans/completed/local-pilot-fix-workspace-cycle-12.md) integrates the actual immutable fictional package builder into the localhost consultant workspace through one exact opt-in profile. Complete original/current findings, guidance, artifact contents and source/template locks remain inspectable as inert React text. Every artifact remains Unverified. Saved review changes refresh one captured guidance/current-review source; stale, malformed, foreign or mismatched reads are refused. Eight historical profiles retain their prior inputs and behavior. This is the approved bounded FR-HAS-35/37/42 and IP-HAS-008 engineering subset; artifact approval/history, task/CSV/export/execution and full TP018 acceptance remain open.

Independent A12/B12/V12 and coordinator reviews closed without scoped findings. 553 source, 519 component, 691 independent saved-source checks (including 137 portable) and 3,538 actual browser assertions passed. Nine new axe subset audits reported zero violations/incomplete; readable desktop/mobile/320 captures were inspected. Final 53-project restore/format/zero-warning Release build, current unit/PostgreSQL/architecture/frontend/infrastructure/secret/collector checks, Cycle11 standalone checks and seven earlier browser suites passed. Configured Linux, Windows 2022/2025 and container checks passed on code `0569c3c88cbd2c856e9d8cb39c8ddf4534e8e66a`; [exact evidence](../../docs/development/evidence/local-pilot-cycle12-developer-checks.json) binds 591 source entries with SHA256 `e8196ab8b80bdabdc45765e45a110e17ec84ed62a3e4a1753ded8b9341dd2005` and preserves test/tool invocation failures and corrections separately.

All 1,903 original worker bindings were reverified in 2,843 preserved archive entries before clean writing-worker checkout removal; original consumed snapshots and compiled runtime closures are retained. The coordinator's final formatting-only harness correction has distinct replay evidence. All 51 concurrent workspace changes were preserved, including a reversible integration with the uncommitted findings UI; that concurrent UI is not covered by the isolated Cycle12 results. The worker's duplicate public-registry audit was rejected by automatic approval review; previously executed coordinator audits cover exactly unchanged dependency locks. Generated capture scanner matches were independently classified as fictional finding hashes; source checks passed without ignore/policy changes.

Matching owner-private publication succeeded; the final docs-only COMPLETE snapshot is republished in this same cycle. All11 open human task roles, instructions and completion criteria remain supported by their canonical records. The bounded cycle is COMPLETE. Full Milestone7/local pilot/UAT/G1–G9, supported Windows/manual accessibility, deployed ADR0001 renderer isolation and default Mac startup remain NOT VERIFIED. Historical Mac host/browser checks used a temporary test-only configuration-watch override. Shared-cluster restart DUR-PG008 was not rerun. No new dependency, migration, permanent runtime setting or customer/provider/production activation.

## Cycle13 exact artifact-review decision packet — 2026-10-02

[Cycle13](../../plans/active/local-pilot-artifact-review-cycle-13.md) prepares the next IP-HAS-008/Milestone7 subset under FR-HAS-35/37/42, AC-HAS-18/14 and bounded TP-HAS-018/014/009/002/003/017/013. [AR13-01–05](local-artifact-review-contract-proposal.md) proposes explicit consultant-only local attestation, immutable Unverified originals, complete-source invalidation, append-only attributed event/replay/concurrency and one opt-in compatibility boundary. The [test plan](local-artifact-review-test-plan.md) makes twelve positive/negative implementation evidence groups explicit. Contract approval is PENDING; A13/B13/V13 implementation packets are BLOCKED at the human decision, not running. Agent documentary review does not grant product/security authority.

No new runtime or migration has been implemented or verified. Task/CSV contracts, including the Auditor-versus-Consultant export discrepancy between the authorization matrix and technical operation, remain separately unresolved. All full milestone/local completion/UAT/G1–G9 checkboxes remain open. Canonical preparation evidence and the existing private board must preserve this pending status and source-backed requested role/completion condition.

## Cycle13 AR13-01–05 approved — bounded implementation running

[The exact owner decision](local-artifact-review-approval.md) authorizes the previously prepared individual artifact-review/history/invalidation packet. [The plan](../../plans/active/local-pilot-artifact-review-cycle-13.md) now permits execution after the coordinator settles its internal module/source-fence/schema/DTO checkpoint. Preserve historical nine-profile bytes and Unverified originals; review never changes scoring or validates remediation. Required tests and review/publication completion are not checked until executed. No production permission/customer migration/task/CSV or full milestone/G1–G9 acceptance follows.

## Cycle13 approved artifact review — bounded developer closure

The [approved Cycle13 slice](../../plans/active/local-pilot-artifact-review-cycle-13.md) is implemented and independently reviewed: individual local Consultant review/withdrawal, source invalidation, append-only attributed history and exact historical receipt replay. Generated originals stay byte-identical and Unverified; Reviewed for planning does not establish correctness or safe remediation. One explicit opt-in fictional profile enables these actions, with an additive synthetic migration and shared transaction fence for source writers. Earlier nine profiles retain their compatibility boundary.

[Source-bound executed evidence](../../docs/development/evidence/local-pilot-cycle13-developer-checks.json) records212/370 author domain assertions (coordinator212/369 with a disclosed optional-case difference),572 component assertions,218/911 independent portable/PostgreSQL assertions,33 independent oracles and10,564 actual-browser checks. All nine earlier browser flows and applicable58-project checks passed. New desktop/mobile320 axe records have0violations/0incomplete; six captures were inspected. Non-author reviews are closed; original generations, failures/corrections and archive limitations remain visible. Configured hosted results bind code804d71a separately from documentation.

This closes only the approved local development cycle. Full Milestone7/TP-HAS-018, local pilot completion, UAT readiness and G1–G9 remain open. Task/conversion/priority/effort and the Auditor CSV-role discrepancy need an exact later decision. Supported manual accessibility, default Mac startup, shared PostgreSQL restart and deployed isolation remain NOT VERIFIED. Existing human dependencies are unchanged. Private board publication is confirmed separately.

## Cycle14 exact planning-task decision packet — 2026-10-02

[Cycle14](../../plans/active/local-pilot-planning-tasks-cycle-14.md) prepares the next IP-HAS-008/Milestone7 subset under FR-HLT-13/21, FR-HAS-35/37/42/50, FR-TSK-1/3, NFR-EVD-5, AC-HAS-13/14/18 and bounded TP-HAS-002/003/009/013/014/017/018. [TC14-01–06](local-planning-task-contract-proposal.md) makes grouped conversion eligibility, frozen source/selected-attestation vector, minimal ownership/lifecycle/comments, optimistic duplicate/replay, isolated opt-in/schema and future CSV role reconciliation explicit for approval.

A14/B14/V14 writing packets are BLOCKED at the human decision. The coordinator can finish documentary/source/traceability checks, independent review, canonical records and the matching private board. No implementation/migration/permission/architecture change or runtime/build/implementation-CI PASS follows. The future CSV-role wording remains proposed until TC14-06 approval; fields/export-policy/lifecycle/worker/encoding need a separate exact export contract. All full milestone/UAT/local completion/G1–G9 checkboxes remain open.


## Cycle14 owner approval — 2026-10-02

The repository owner explicitly approved TC14-01–06 against proposal revision `2b565945be99c4c674cf00b08caa6fc581b99ffd` and SHA256 `fe8a1583de876b653ddef41fa105a0defe4679f612400f660721ca8234c2b839`. Decision record: `specs/003-health-assessment/local-planning-task-approval.md`. The bounded Cycle14 plan/test plan are approved for local synthetic implementation. At this approval checkpoint, engineering preparation and implementation were RUNNING and runtime/migration verification was pending. The executed bounded closure below records the later result; full milestone and live gate acceptance remain open. Future CSV wording is reconciled under TC14-06; export remains deferred. The planning-task decision dependency is closed; eleven prior human tasks remain open. Private board publication will follow the committed approval snapshot and be recorded separately.


## Cycle14 consultant planning tasks — bounded developer closure — 2026-10-03

[The approved Cycle14 slice](../../plans/active/local-pilot-planning-tasks-cycle-14.md) implements explicit conversion of a currently reviewed fictional recommendation option into one durable assigned Consultant planning task. Workflow status is separate from source/selected-review freshness; reconfirmation, reasoned status changes, terminal reopening, comments and immutable historical-receipt retry preserve provenance and continuous history. Completing planning work does not validate remediation or change findings, artifact review, scores, maturity or reports. Unavailable source reveals verified task metadata only. Ten historical profiles retain their saved inputs/locks and old analysis shapes. Future CSV role wording is reconciled; exports remain deferred.

Independent reviews closed with no remaining scoped findings. 736 portable/1249 inclusive fresh-PG domain assertions,737 component assertions,702 portable/2542 inclusive-PG independent checks and 13626 final actual-browser assertions passed. Counts overlap. Actual Enter/Space covered all eight task actions; nine desktop/mobile390/320 captures were inspected through full viewport images and representative readable crops of tall regions and all three axe records reported zero violations/incomplete. Final 62-project/current local checks and configured [Linux/Windows](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37097534191)/[container](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37097534187) checks passed on exact code`b10d7b28e10dccf379bd2922b04ac8a4eca8c0c2`, including historical database/browser regressions. [Exact evidence](../../docs/development/evidence/local-pilot-cycle14-developer-checks.json) records corrections, original compiled/source/fixture/log closures, non-author reviews, rehashed archives and limitations. All 51 concurrent UI/BFF/research files were preserved through reversible integration; their composed working UI build passed separately from published pilot source.

The additive synthetic task schema initializes only with the explicit flag in a fresh dedicated Cycle14 loopback database; no customer/production migration or dependency was added. Rollback disables new actions while preserving compatible readers/history. Full Milestone7/TP-HAS-018/localpilot completion/UAT/G1–G9, manual supported accessibility, deployed isolation, default Mac startup and hard-kill/shared-cluster recovery remain NOT VERIFIED. Eleven prior human dependencies remain open. Matching owner-private publication is confirmed in this same cycle; its exact source, deployment and QA are recorded in docs/development/evidence/local-pilot-cycle14-completion-site.json.

The configured Linux replay separately passed 10,617 artifact-review and 13,654 planning-task browser assertions on the final code. Those are distinct from local macOS and controlled investigation counts; none are summed.


## Milestone08 evaluation accuracy foundation — 2026-10-03

The owner directed “Start 08.” The [bounded evaluation foundation](../../plans/active/local-pilot-m08-evaluation-foundation.md) starts Phase1C independently of task and BFF work. The [internal synthetic contract](synthetic-evaluation-accuracy-contract.md) captures caller-supplied sample membership and outcomes with detached canonical bytes/source locks; exact general AI denominator math and customer-approved desired-outcome arithmetic stay separate. No sampling, reviewer authority, risk acceptance, recurrence, persistence, application route or live evaluation is introduced. Remaining sampling/regression edge policy and reviewer eligibility/conflict procedure require the source-backed decision described in the bounded plan. Full Milestone08/TP-HAS-012/019/Phase1C/UAT/G1–G9 remain NOT VERIFIED. Local 64-project locked audited restore/format/zero-warning Release build, 23,550 independent assertions, 14 portable regression hosts and architecture/secret checks passed. Non-author reviews closed; original failures/corrections are retained in [exact evidence](../../docs/development/evidence/m08-evaluation-foundation-20261003.json). Hosted checks remain NOT VERIFIED. Matching owner-private publication is confirmed at version 91/source 37240557936bb0b17bdd1be39a33b80959f56706 with the twelve source-backed human tasks; [exact receipt](../../docs/development/evidence/m08-evaluation-site-publication-20261003.json) records deployment/access/QA separately.


## Phase 1B closure decision preparation — 2026-10-03

The owner directed continuous Phase1B closure work, with Phase1C owned by another agent. [The maintained closure plan](../../plans/active/local-pilot-phase1b-closure.md) maps current foundations and missing outcome/priority/effort/automatic fake-AI/CSV paths. [The exact combined decision](local-phase1b-closure-decision-packet.md) is Proposed: OP1B-01–06, AI1B-01–07, CSV1B-01–07, plus P1B-SCOPE-01 requesting explicit local allocation of residual FR-HAS-12/15 collaboration/dispositions to Phase1C. Existing canonical Milestone5 criteria remain open; absent allocation approval those paths stay in Phase1B.

Non-author documentary review closed authority/high-water/effort-withdrawal/compound-scoring, AI coverage/fenced-read/attempt accounting, and CSV Auditor capture/link-origin/input-output parity issues. The one coordinator-composed profile and eleven-profile compatibility are proposed, not implemented. [Preparation evidence](../../docs/development/evidence/phase1b-closure-preparation-20261003.json) separates old-source baseline checks from64-project composition, invocation corrections, documentary review and unexecuted runtime closure. No new dependency/configuration/migration, production permission, real provider/customer processing or milestone/gate acceptance. Phase1B remains IN PROGRESS; exact owner decision precedes dependent implementation. The owner-private board and source-linked human task must reflect this pending decision after confirmed publication.


## Phase 1B exact closure packet approved — 2026-10-03

The repository owner stated “Approved 1B packet”, approving OP1B-01–06, AI1B-01–07, CSV1B-01–07 and P1B-SCOPE-01 without amendment. [Attributable approval and exact SHA256 bindings](local-phase1b-approval.md) supersede the immutable proposals’ pending labels. All seven reviewed files were hash-verified unchanged.

[Closure execution](../../plans/active/local-pilot-phase1b-closure.md) is RUNNING: bounded isolated writers prepare the shared engineering contract and remaining desired-outcome/priority-effort, automatic fictional AI and safe task CSV implementation. Approval grants no runtime PASS or closure. Residual FR-HAS-12/15 collaboration beyond existing Confirm/Reject/Defer/comment/presentation is allocated to Phase 1C; requirements/full Milestone5 stay open. Preserve the separate Phase1C agent’s work. Human UAT, live G1–G9, production/provider/source authority and full milestones remain separately gated. The combined packet owner task is closed by this approval; twelve earlier human dependencies remain open.


## Milestone08 cycle02 — exact policy decision ready — 2026-10-03

The owner's “Next cycle” advanced [decision preparation](../../plans/active/local-pilot-m08-evaluation-policy-cycle02.md). [The combined packet](local-evaluation-policy-decision-packet.md) proposes EV02-S01–S04 sampling/quota/allocation/warning/regression and EV02-R01–R03 qualified scoped reviewer/conflict/correction-history semantics. Four exact proposal/test files are digest-bound. Non-author reviews closed the independent-cutoff issue; the combined allocation example explicitly includes its stratum count. Documentary links, whitespace, traceability, four independent numeric examples and secret scan passed. [Exact evidence](../../docs/development/evidence/m08-evaluation-policy-cycle02-20261003.json) distinguishes these checks from all40 unexecuted future behavioral cases.

Exact owner policy approval remains PENDING and blocks dependent implementation. No code, dependency, migration, runtime setting, role, real provider/source or acceptance change. The existing M08 foundation remains verified only to its original boundary. Phase1B's [exact approval](local-phase1b-approval.md) and separate implementation are preserved; residual FR-HAS-12/15 collaboration allocated to Phase1C remains a later separate contract. Full M08/Phase1C/UAT/G1–G9 remain NOT VERIFIED. The existing evaluation human task is refined to the exact packet, not duplicated or closed. Private-site publication is tracked separately and must be confirmed before this snapshot is reported current.


### Milestone08 cycle02 private publication confirmed

[Exact publication receipt](../../docs/development/evidence/m08-evaluation-policy-site-cycle02-20261003.json) confirms owner-private version94/source000e400547775bb8202cc2cfbedc1d619a865261 and succeeded deployment. Final1440/390/320 browser checks and viewport inspection passed;12 open tasks remain, the evaluation decision card is refined and Phase1B approval stays closed. Source/native artifacts are preserved and rehashed before clean worker removal. Exact evaluation policy approval is still PENDING; no runtime, full milestone or gate acceptance follows.


## Milestone08 cycle03 — approved evaluation policy foundation — 2026-10-03

The repository owner’s explicit “Approved” closes EV02-S01–S04/R01–R03 at the exact immutable [decision packet](local-evaluation-policy-decision-packet.md); [the attributable receipt](local-evaluation-policy-approval.md) preserves proposal digests. The [bounded cycle03 plan](../../plans/active/local-pilot-m08-policy-implementation-cycle03.md) implements deterministic immutable synthetic sampling, independent warning/regression decisions and trusted-fixture scoped reviewer/conflict eligibility. Current fixture decisions do not establish actual identity, reviewer qualification, safety detection or server authority. Original accuracy-v1 source and reviewed proposal bytes are unchanged. No package, database migration, public route, UI registration, runtime flag or production/provider/source activation.

[Source-bound executed evidence](../../docs/development/evidence/m08-evaluation-policy-cycle03-20261003.json) records pinned10.0.401 audited locked68-project restore, formatting, zero-warning/error Release build,7717 sampling/6144 warning-regression/300626 reviewer/23550 original-accuracy/128 composed assertions, independent Python oracles,14 portable regression hosts and architecture checks. Host assertion counts overlap. Nonauthor reviews closed all scoped findings and independently replayed corrected sampling/regression/reviewer/composed source; original failures and corrections remain visible. Secret/link/traceability checks and matching private publication are recorded with their actual outcomes, separately from runtime checks. Hosted checks remain NOT VERIFIED until exact configured runs are observed.

The local policy approval task is closed. Actual qualified independent reviewers remain a human dependency before live M08/G8/G9 evaluation: pilot owner, One Identity SME, Security and relevant customer owners must identify eligible reviewers, attest qualification/scoped permission and reasoned conflicts/clearance, and retain attributable protected evidence under the [approved evaluation procedure](evaluation-plan.md) and [reviewer policy](local-evaluation-reviewer-contract-proposal.md). Completion requires actual documented assignments/attestations and independently reviewed outcomes, not fixture declarations. Other human tasks and Phase1B implementation remain separately owned.

All40 proposed operational cases, real authorization/redaction, durable correction/history/audit/queue/atomic recheck, real reviewer/live two-environment evidence, recurrence/reassessment/score explanations and residual FR-HAS-12/15 remain NOT VERIFIED or later bounded work. This closes only the approved local foundation implementation after its publication/archive receipt; full M08/Phase1C/TP-HAS-012/019/UAT/G1–G9 remain open. Private site publication must match the committed canonical snapshot before it is reported current.


### Milestone08 cycle03 bounded developer closure confirmed

[The exact closure receipt](../../docs/development/evidence/m08-evaluation-policy-cycle03-closure-20261003.json) records approved local implementation COMPLETE at published code8b44a27, whose committed tree exactly matches the independently checked coordinator. Original source/runtime/log/review/fixture closures were copied and rehashed; four clean own worktrees were removed, with branches and unrelated work preserved. Both Windows2022/2025 jobs and the new Linux evaluation-policy step passed on this code; the remaining historical Linux browser/infrastructure job was still running, so whole-job success is not claimed. Container checks passed separately.

Matching owner-private version95/sourceeba7bd85 and succeeded deployment are confirmed;1440/390/320 local asset QA and viewport inspection passed, with all eleven prior task cards byte-preserved. The policy decision is closed and actual qualified independent reviewer assignments remain open, keeping twelve human tasks. The final docs-complete canonical snapshot is reflected in the board in this same cycle with a separate native receipt. Full M08/Phase1C/TP-HAS-012/019/UAT/G1–G9 and all actual reviewer/live/durable operational boundaries remain NOT VERIFIED; other phases remain separately owned.


### Milestone08 hosted replay and bounded test synchronization correction

The original full Linux job on code8b44a27 subsequently FAILED in unchanged Cycle09 offline-preview mobile keyboard-proposals-navigation; the new M08 checks had passed. [Exact failure and corrective evidence](../../docs/development/evidence/m08-policy-hosted-navigation-correction-20261003.json) retain that result and later skipped checks. Two test-only bounded waits now observe exact keyboard-triggered URL changes before the unchanged assertions. Navigation timing is a supported inference, not conclusively proven root cause. Original and corrected local browser replays each passed440 checks; pinned fixture restore/build and192 composed checks, syntax/format/secrets and independent review passed. Product/renderer/fixture and evaluation module bytes are unchanged. Corrected whole Linux execution remains NOT VERIFIED until an exact run is observed. Local approved foundation closure, twelve human dependencies and all full milestone/live gate limits are unchanged; the private board reflects the failure and this correction.


### Milestone08 cycle03 final private publication provenance

[Final native receipt](../../docs/development/evidence/m08-evaluation-policy-site-cycle03-20261003.json) confirms owner-private version96/source8fea2c2a and succeeded deployment reflecting canonicaldd1db9e, including the original Linux failure and locally verified test-only correction. All eleven prior task cards remain byte-identical; twelve open tasks include actual independent reviewer assignments.1440/390/320 asset QA, exact pushed archive content, original concurrent-file preservation and final artifact rehash passed. Temporary own worktrees are removed after archived closure. Corrected full hosted Linux remains RUNNING/NOT VERIFIED at this checkpoint. This is publication provenance only; implementation, milestone, human and gate statuses are unchanged from the published snapshot.


## Phase 1B local engineering verified — 2026-10-03

The exact approved OP1B/AI1B/CSV1B packet is implemented on frozen code `6b7a5f25d9d726af2ebbc3f45480dd1e23476d24`. One guarded fictional run connects exact approved outcome locks, ten deterministic and two automatic fake-AI units, coherent health/quality/independent maturity, original-preserving priority/effort, inert reviewed packages, explicit Consultant tasks and genuine Consultant/Auditor protected CSV. [Requirement/test dispositions](local-phase1b-closure-verification.md), [source-bound native evidence](../../docs/development/evidence/local-pilot-phase1b-closure-20261003.json) and [operations handoff](../../docs/operations/local-phase1b-synthetic-handoff.md) govern this local engineering boundary.

All applicable configured checks passed:81-project pinned locked audited restore/format/zero-warning Release build,32 unit hosts, actual PostgreSQL OP158/AI463/CSV81/compound131 assertions, frontend contract/type/build/format/audit, architecture, tracked-source/document secret checks, Chrome component50 and actual Consultant121/Auditor40/paused-budget81 checks. Counts overlap. Final Linux/Windows2022/2025 bootstrap37142354919 and container/package37142354891 passed every job, including historical browser/recovery, collector and Bicep checks. Eighteen native Linux isolation probes verify the pure renderer; the actual localhost CSV host is Mac-only. All scoped independent reviews are closed. Six additive synthetic migrations preserve four historical migration bytes; no new package or live activation. Original failures, exact retry, archive/source hashes and raw workspace scanner digest false positives remain disclosed.

Residual FR-HAS-12/15 collaboration is allocated to the separately owned Phase1C under approved P1B-SCOPE-01. FR-HAS-53 accuracy, supported manual accessibility (including two retained narrow contrast incomplete groups), default Mac startup, real identities/provider/customer/source, deployed durability/isolation/retention, full milestones/UAT/G1–G9 and production release remain NOT VERIFIED. Twelve human dependencies and separate Phase1C/M08 records are preserved. Local runtime verification is complete; matching owner-private publication is the remaining closure criterion in [the canonical plan](../../plans/active/local-pilot-phase1b-closure.md).


### Phase 1B local checkpoint CLOSED — publication and cleanup confirmed

[The final native closure receipt](../../docs/development/evidence/phase1b-local-closure-site-20261003.json) confirms owner-private version98/sourcecb3e893 and succeeded deployment reflecting canonical283a293 and exact frozen runtime/tests6b7a5f2. Reconciliation preserved the newer shared site/source97, all twelve human cards and sixteen unowned panels.1440/390/320 QA and exact HTML/archive inspection passed. Four finished own worktrees were removed after native/source/runtime/log evidence was copied and rehashed; retained branches, databases and unrelated/concurrent work are preserved. All scoped [Phase1B completion criteria](../../plans/active/local-pilot-phase1b-closure.md) are fulfilled. Separate Phase1C, FR-HAS-53 accuracy, manual accessibility/default Mac startup, full milestones/UAT/G1–G9 and production release remain open.


### Phase 1B final closed-snapshot publication provenance

[The final native private receipt](../../docs/development/evidence/phase1b-final-closed-site-20261003.json) confirms succeeded owner-private version99/source8f2b5a5 reflecting the completed canonical closure commit0f78830. The board explicitly marks the local engineering checkpoint closed and links its confirmed receipt. Repeated1440/390/320 QA, exact pushed HTML/archive inspection and preservation of all twelve human cards/sixteen unowned panels passed. This records publication provenance only: scoped closure, separate Phase1C, all human dependencies and NOT VERIFIED/live/production boundaries are unchanged.


## Milestone08 cycle04 — bounded review workflow executed

[The active cycle plan](../../plans/active/local-pilot-m08-evaluation-workflow-cycle04.md) has verified the guarded synthetic storage/host/workspace bridge for fixed sample membership, current fixture eligibility, original-preserving reviews/corrections and immutable outcome/history versions. [Executed evidence](../../docs/development/evidence/m08-evaluation-workflow-cycle04-20261003.json) and [dispositions](local-evaluation-workflow-verification.md) retain exact source, nonauthor reviews, initial failures, migration/flag and unexecuted operational portions. Publication and cleanup close in a separate receipt. Full milestone/phase and live gates remain open. Next work requires exact approved real Phase1B source/reviewer/evidence authority contracts; residual FR-HAS-12/15 collaboration remains separately in Phase1C.


### Cycle04 closure confirmed

[The exact closure receipt](../../docs/development/evidence/m08-evaluation-workflow-cycle04-closure-20261003.json) closes the bounded plan after private version100 deployment, source/runtime/native preservation and removal of four own worktrees. The next real source/authority contract and all operational/full acceptance limits remain open.


## Phase 1D preparation checkpoint — 2026-10-03

[The bounded preparation plan](../../plans/active/local-pilot-phase1d-preparation.md) and [exact decision packet](local-phase1d-decision-packet.md) make IP-HAS-012/Milestone11's first local contract reviewable. The approved local track permits fictional published fixtures and authorization doubles; source drafts cannot be relabeled published. P1D-D01–03 remain proposed and dependent code is blocked on exact local technical/security-owner approval. This checkpoint changes no Milestone11 checkbox, public/client contract, production authorization or G1–G9 state. Actual Phase1C immutable reader and live MCP controls remain later integration/activation inputs.


Cycle04 hosted follow-up: [all configured jobs/steps passed](../../docs/development/evidence/m08-evaluation-workflow-cycle04-hosted-20261003.json) on integrated implementation16fac34. The local/hosted engineering slice is closed; actual source/reviewer and full operational acceptance remain open.


[Matching hosted-check private publication](../../docs/development/evidence/m08-evaluation-workflow-cycle04-hosted-site-20261003.json) is confirmed at version103, preserving the separately owned Phase1D proposal and thirteen current human tasks. Cycle04 engineering/hosted/publication/preservation is complete within the bounded scope.


## Phase 1D approved local execution — 2026-10-03

[Exact owner approval](local-phase1d-approval.md) authorizes the [bounded implementation cycle](../../plans/active/local-pilot-phase1d-implementation.md) for P1D-D01–03. Non-author-reviewed internal interfaces, independent preserved fixtures and isolated writers govern publication projection and request-boundary work. No Milestone11 checkbox, public/client contract or G1–G9 state changes until its own required evidence exists; real Phase1C publication/production adapters remain deferred.


## Phase 1D bounded local implementation verified — 2026-10-03

[The canonical local plan](../../plans/active/local-pilot-phase1d-implementation.md) and [requirement/test disposition](local-phase1d-implementation-verification.md) close the executable P1D-T01–12 local slice with independent fixtures/reviews and exact source-bound checks. This does not check off full IP-HAS-012/Milestone11: actual immutable publication parity, reviewed client/identity/transport and durable/distributed controls remain open in the [follow-on checklist](phase1d-integration-contract-checklist.md). The local decision is approved; the private board retains twelve prior tasks plus that separate integration dependency. Native publication closure follows separately.


## Milestone08 cycle05 saved Phase1B capture verified — 2026-10-03

The owner's next-cycle instruction and existing approved Phase1B/EV02 scope now have a reviewed frozen internal source-capture adapter. It captures actual saved fictional AI groups, original bytes/digests, desired-outcome locks and terminal gaps under one caller-owned guarded transaction and exact Consultant authorities. Normal2-group, zero2-gap and mixed1/1 cases pass; deterministic units remain excluded. [Verification and limits](local-phase1b-evaluation-source-verification.md), [source-bound execution receipt](../../docs/development/evidence/m08-source-capture-cycle05-20261003.json), [run instructions](../../docs/development/local-phase1b-evaluation-source.md) and [canonical cycle plan](../../plans/active/local-pilot-m08-source-integration-cycle05.md) govern the bounded result.

Unit168, author PostgreSQL95 and independent PostgreSQL208 assertions pass. Locked audited restore, whole solution format/zero-warning Release build, relevant historical evaluation/AI/outcome/CSV compatibility, three Python oracles, architecture, diff and secret checks pass. Exact90 original owning files are unchanged;19 capture files match the tested coordinator source. Nonauthor review closed all scoped findings. Counts overlap; exhaustive semantic/corruption branches and Confirm/Defer/comment cases are not all newly executed. The separate Phase1D failed expectation was corrected by its owner and the final actual entry point passes2849 through an exception-catching native wrapper. Earlier failed dotnet processes remain in macOS crash handling despite targeted termination; unrelated processes are preserved.

No migration, new package/version, host/UI/API activation, source-backed sampling23-bindings, reviewer votes, actual identities/customer/provider access or production release. Source-backed sampling/provenance and reviewer integration are the next bounded cycle; the historical120/100 workspace remains unchanged. Full M08/Phase1C/TP-HAS-012/019/UAT/G1–G9 and live/manual/capacity acceptance remain open. Current-cycle hosted observation and matching private publication/preservation are recorded separately; do not infer them from local execution.


The bounded Phase1D plan closes after executed local checks, independent findings/T12 proof review and owner-private104 deployment. The final completed-plan Site source is confirmed by its separate native receipt. Full Milestone11 and actual publication/client/identity/distributed acceptance remain open.


### Cycle05 bounded capture publication and preservation confirmed

[The native closure receipt](../../docs/development/evidence/m08-source-capture-cycle05-site-20261003.json) confirms owner-private version105/source979dbaf, succeeded deployment reflecting canonicalc02b945, exact pushed archive bytes and1440/390/320 QA. All22 previous panels and13 open human cards are preserved.158 native/source/runtime/log files and two large native archives were rehashed before removing three own clean worktrees; branches/evidence databases remain. Two failed earlier dotnet processes still show macOS crash handling despite targeted termination; their cleanup is NOT CONFIRMED. Container/package CI passed; bootstrap is pending at this historical observation. The bounded capture checkpoint is closed with this explicit OS limitation; actual source-backed sampling/reviewer integration, full M08/Phase1C and live/manual gates remain open.


### Cycle05 complete hosted verification observed

[The final hosted receipt](../../docs/development/evidence/m08-source-capture-cycle05-hosted-20261003.json) confirms complete bootstrap37150936195/Linux/Windows2022/2025 and package37150936031 success associated withc02b945. All new capture and historical configured checks passed; native job logs are preserved. Later shared changes through7030a64 are documentation only, with exact runtime/test/CI equivalence;58 historical evaluation/approval files remain unchanged. Final nonauthor consistency review passed. Prior pending/failure records and failed Mac-process cleanup remain disclosed. Matching final private snapshot follows separately. Source-backed sampling/reviewer provenance is next; actual assignments/customer accuracy/full M08/Phase1C/live/manual gates remain open.


## Phase 1D cycle02 conditional integration handoff — 2026-10-03

[Cycle02 preparation](../../plans/active/local-pilot-phase1d-integration-readiness-cycle02.md) closes the read-only source/authority inventories and independent documentary review. [The reader candidate](phase1d-integration-implementation-candidate-cycle02.md) remains CONDITIONAL / NOT READY: the Phase1C owner must supply an actual immutable publication contract, and technical/security/operations owners must supply reviewed source mapping, lifecycle, current authority and audit semantics. [Intake I01–I07](phase1d-integration-intake-cycle02.md) precedes approved specifications, an exact implementation/test plan and independent internal freeze. Later I08–I11 governs external client/identity/configuration/activation separately. [P02-T01–18](phase1d-publication-conformance-test-plan-cycle02.md) remain proposed and unexecuted. No publisher/reader/client/schema/SDK implementation or production operation is authorized by this preparation.

[Prior-source complete hosted checks](../../docs/development/evidence/phase1d-hosted-20261003.json) passed on8e9ada7. The existing fixture approval and bytes are preserved. Full IP-HAS-012/Milestone11, reporting parity, live/manual/UAT/G1–G9 checkboxes stay open. Source-backed human tasks and the existing private board are reconciled in this work cycle, with exact publication closure recorded separately.


Cycle05 final private closed-snapshot publication is confirmed by [native version107/sourcef6e3390](../../docs/development/evidence/m08-source-capture-cycle05-final-site-20261003.json), reflecting canonical643458c and complete passing CIc02b945. The newer separate Phase1D closure,22 unowned panels and13 human cards are preserved; repeated1440/390/320 task/link/reflow and exact pushed archive checks pass. The bounded capture is closed; next source-backed sampling/reviewer integration and all actual/full/live/manual dependencies remain open. Failed earlier Mac-process cleanup remains unconfirmed.
