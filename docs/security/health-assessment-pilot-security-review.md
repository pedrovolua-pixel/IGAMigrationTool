# Security Review: One Identity Manager Health-Assessment Pilot

Status: Design Approved — controls not implemented or verified  
Scope: Approved Phase 1A–1D pilot technical design  
Review owner: Security owner  
Last updated: 2026-09-29

## Executive result

Design review result: **CONDITIONALLY ACCEPTABLE FOR PLANNING; NOT VERIFIED FOR IMPLEMENTATION OR PILOT USE**.

The approved technical design establishes appropriate boundaries for read-only source interaction, separate customer data planes, immutable evidence/publication, server-side authorization, constrained AI, sandboxed rendering, controlled sharing, retention and read-only MCP. Microsoft Entra ID and OpenAI `gpt-6-sol` with required ZDR are selected, but there is no implementation, configured provider project/infrastructure, exact query pack, access-control test evidence, penetration evidence, backup/restore evidence or incident runbook to validate. No residual risk is accepted by this review.

Pilot use must remain blocked until every Critical/High design finding below is closed with executable evidence or explicitly accepted by its authorized human owner under approved policy.

## Reviewed evidence

- `AGENTS.md`
- `product/non-functional-requirements.md`
- `specs/001-data-ingestion/product-spec.md`
- `specs/003-health-assessment/product-spec.md`
- `specs/003-health-assessment/technical-spec.md`
- `specs/003-health-assessment/capability-matrix.md`
- `specs/003-health-assessment/database-evidence-contract.md`
- `architecture/decisions/ADR-0001-pilot-application-shape.md`
- `architecture/decisions/ADR-0002-pilot-tenant-isolation.md`
- `architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md`
- `specs/003-health-assessment/test-plan.md` when created by this design package

Implementation diff: none. Product code, deployment configuration and dependencies do not exist, so implementation controls are `NOT VERIFIED`.

## Assets and security objectives

| Asset | Confidentiality/integrity/availability objective |
|---|---|
| Source database credential | Never enters evidence, reports, logs, jobs or AI; only ingestion credential boundary may use it |
| Raw and normalized evidence | Customer/project/category authorization, encryption, integrity, retention and deletion |
| Protected identifiers and identity relationships | Minimized; government identifiers remain customer-side/redacted; field-level authorization |
| Rules, profiles, prompts and model versions | Approved provenance and immutable run locking; supply-chain integrity |
| Findings, risk decisions and customer comments | Attributable history; generated originals preserved; strict customer isolation |
| Scores, maturity and published reports | Reproducible and immutable; warnings cannot be removed silently |
| Fix-package SQL/scripts | Inert review-only content; never executable by the product |
| Share links and passcodes | Unpredictable, short lived, separately delivered, revocable and audited |
| Audit records | Append-only to application roles, payload-minimized, integrity monitored and recoverable |
| Tenant routing and assignments | Prevent confused-deputy and cross-customer access; immediate revocation |
| Encryption and signing keys | Least privilege, rotation, recovery and no ordinary logging |
| Backups and deletion ledger | Recoverability without resurrecting expired access or purged data |

## Actors

- Assigned consultant.
- Qualified customer reviewer.
- Customer evidence authorizer.
- Customer risk owner.
- Customer executive and auditor.
- Partner administrator and customer administrator where separately authorized.
- MCP named user or service identity in Phase 1D.
- Platform operator/support engineer.
- Application API, assessment worker, AI worker, renderer, purge worker and backup/restore operator identities.
- External AI provider and passcode-delivery provider, if approved.
- Unauthorized external actor, malicious or compromised authorized user, compromised worker, malicious evidence author and supply-chain attacker.

## Trust boundaries and entry points

1. Client/browser/MCP to authentication and public application boundary.
2. Shared control plane to one resolved customer data plane.
3. Interactive application through controlled public egress to Service Bus Standard, then to a short-lived worker identity. The broker carries opaque delivery signals only and PostgreSQL remains the work-state source of truth.
4. Application/worker to normalized evidence and protected-reference resolver.
5. AI gateway to external AI provider.
6. Canonical report projection to network-isolated renderer.
7. Published redacted artifact to passcode-protected link viewer.
8. Deployment pipeline to application, rules, prompts and configuration.
9. Active data plane to backups, restore environment, purge pipeline and audit storage.
10. Feature-001 collector to One Identity SQL Server through the approved query pack.

Entry points include authentication callbacks, resource identifiers, filters/cursors, assessment configuration, finding edits/comments, desired outcomes and policy references, evidence payloads, rule bundles, AI responses, CSV/PDF/Markdown rendering, share-link tokens/passcodes, MCP inputs, job payloads, operator tooling, deletion/recovery actions and restore procedures.

## Abuse cases and required controls

| Abuse case | Required preventive/detective controls | Verification status |
|---|---|---|
| Substitute another customer/project/resource ID | Resolve customer data plane from trusted assignment; opaque IDs; deny before query; cross-tenant tests | `NOT VERIFIED` |
| Partner accesses an unassigned customer | Dated assignment policy on every request/job; cache invalidation and revocation tests | `NOT VERIFIED` |
| Worker reuses interactive authority or broadens scope | One-customer, one-job short-lived workload identity; narrow module permissions | `NOT VERIFIED` |
| SQL account can write/administer | Preflight effective-permission attestation; block before queries; approved pilot warning only for excess read | `NOT VERIFIED` |
| Malicious evidence injects AI/tool instructions | Data-only envelope, no tool access, system policy isolation, schema/citation validation, adversarial suite | `NOT VERIFIED` |
| AI provider retains/trains on evidence | Approved contract/configuration, US residency, retention/deletion evidence, no-training default | `NOT VERIFIED` |
| Evidence or comments trigger XSS/template/code execution | Contextual encoding, sanitization where markup allowed, CSP, sandbox renderer, inert fix artifacts | `NOT VERIFIED` |
| CSV cells execute formulas | Prefix/escape dangerous leading characters; contract tests for all exported text fields | `NOT VERIFIED` |
| Link/passcode theft exposes report | 128-bit-or-greater token entropy, verifier-only storage, separate passcode delivery, <=24h expiry, rate limit, revoke, no cache/index | `NOT VERIFIED` |
| Report renderer exfiltrates evidence | No network, credentials or database access; signed bounded manifest; resource and file restrictions | `NOT VERIFIED` |
| Rule/prompt supply-chain compromise | Signed/digested approved artifacts, separate review, immutable versions, deployment provenance, rollback/suspension | `NOT VERIFIED` |
| Historical run is silently reinterpreted | Immutable input locks, versioned readers and manifests, digest verification | Design approved; implementation `NOT VERIFIED` |
| Retention deletion is bypassed or restore resurrects data/link | Deny soft-deleted reads, purge ledger, backup expiry, restore tombstone replay, link invalidation | `NOT VERIFIED` |
| Platform support bypasses customer evidence authorization | Approved pilot policy denies protected-evidence access; any future elevation needs separate design | Implementation `NOT VERIFIED` |
| MCP exposes raw evidence or consequential methods | Explicit allowlist, common policy engine, response-field filter and contract/negative tests | `NOT VERIFIED` |
| Audit trail is altered or contains evidence | Append-only writer, restricted readers, integrity monitoring, payload schema and negative log tests | `NOT VERIFIED` |
| High-volume user or job denies service | Per-identity/customer concurrency and quotas, bounded pages/budgets, queue fairness and alerts | `NOT VERIFIED` |
| Public Service Bus endpoint is probed, reached from an unapproved network or accessed with a leaked shared key | Standard-tier broker contains no evidence payload; local/SAS auth disabled; Entra managed identities and least-privilege data roles; TLS; approved static-egress restriction; connection/denial alerts; deployment drift tests | Design selected; implementation `NOT VERIFIED` |
| SSRF or unsafe callback reaches internal services | No user-supplied fetch URLs in pilot; egress allowlists for approved providers; callback state/nonce validation | `NOT VERIFIED` |
| Authentication session replay/fixation | Entra OIDC/OAuth profile, BFF-managed secure sessions, nonce/state/PKCE and revocation | Design approved; implementation `NOT VERIFIED` |

## Security findings

### SEC-PILOT-001 — Entra identity/session design approved; implementation unverified

- Severity: High
- Evidence: single-tenant Microsoft Entra ID, explicit B2B onboarding, MFA/Conditional Access, BFF registration with dedicated federated user-assigned managed-identity credential, coarse app roles, workload identities, session lifetimes, privileged reauthentication, guest review, revocation and no-local-break-glass policy are approved. The design has not been implemented. Concrete registration and managed-identity IDs are provisioning evidence, not design prerequisites.
- Exploit/impact: weak or inconsistent identity/session handling could permit account takeover, replay or stale access across sensitive customer evidence.
- Required remediation: implement the approved authentication flows, MFA policy, session/token lifetimes, callback protections, federated BFF credential, workload identity, emergency access and revocation; prove end-to-end redemption, renewal, negative trust and leakage cases in deployed Container Apps; add executable tests and provider-configuration evidence.
- Owner: Security owner and technical owner.
- Status: Design decision closed 2026-09-29; implementation remains open and `NOT VERIFIED`.

### SEC-PILOT-002 — Authorization policy and data-plane routing are unimplemented

- Severity: Critical
- Evidence: ADR-0002 and the authorization matrix are design artifacts only.
- Exploit/impact: identifier substitution, routing error or privileged-path bypass could disclose or alter another customer's evidence/findings/reports.
- Required remediation: implement one policy decision path for UI, API, worker, render, export, share and MCP; prove deny-before-query behavior with cross-tenant tests and independent penetration review.
- Owner: Technical owner and security owner.
- Status: Open, `NOT VERIFIED`.

### SEC-PILOT-003 — Exact SQL query pack and permission attestation lack evidence

- Severity: Critical
- Evidence: capability rows, query text, field dictionaries and two environments are `NOT VERIFIED`.
- Exploit/impact: unsafe queries may affect production or collect prohibited data; an overprivileged account could create a source-write path.
- Required remediation: approve exact-build query packs; execute permission and production-impact gates in two independent environments; block every write/admin category before evidence reads.
- Owner: One Identity SME, customer database owner and security owner.
- Status: Open, `NOT VERIFIED`.

### SEC-PILOT-004 — OpenAI GPT-6 Sol and ZDR policy approved; API project controls unverified

- Severity: High
- Evidence: OpenAI API model `gpt-6-sol`, Responses API, US regional processing and required Zero Data Retention are approved. API organization/project, ZDR eligibility/configuration, deletion verification, access identity, quotas and incident configuration are absent.
- Exploit/impact: customer evidence could leave approved residency/retention boundaries or be retained/trained on contrary to policy.
- Required remediation: obtain and verify ZDR on the selected OpenAI project; approve remaining account controls; complete provider due diligence and run injection, leakage, budget and deletion tests.
- Owner: Security owner, privacy/data owner and technical owner.
- Status: Open, `NOT VERIFIED`.

### SEC-PILOT-005 — Backup policy approved; deletion-safe restore is unverified

- Severity: High
- Evidence: rolling backup retention no longer than 35 days is approved, but deletion/tombstone replay and restore procedures have not been implemented or tested.
- Exploit/impact: deleted evidence or revoked links could be restored beyond the promised lifecycle.
- Required remediation: implement deletion ledger/tombstone replay and verify a restore drill meets RPO/RTO without resurrecting access.
- Owner: Data-governance owner, operations owner and security owner.
- Status: Policy decision closed 2026-09-28; implementation remains open and `NOT VERIFIED`.

### SEC-PILOT-006 — Rendering, export and share-link controls lack implementation evidence

- Severity: High
- Evidence: Prince 17 is selected as the gated PDF/UA renderer with a network/database/platform-credential-free Container Apps Job profile. Its read-only vendor license file is the only credential-like exception and cannot authorize platform services. Sandboxing, license handling, token/passcode generation, delivery, rate limiting, revocation and CSV safety remain design only.
- Exploit/impact: malicious content may execute, report data may be exfiltrated, or bearer links may be brute-forced/reused.
- Required remediation: license and implement the approved isolated Prince renderer and delivery mechanism; execute accessibility, deterministic-output, license non-disclosure, sandbox, export/share and adversarial tests; independently review before enabling PDF or sharing.
- Owner: Technical owner and security owner.
- Status: Open, `NOT VERIFIED`.

### SEC-PILOT-007 — Rule, prompt and deployment supply chain is undefined

- Severity: High
- Evidence: normal artifact promotion and suspension requirements, reviewer matrix, signing/provenance, dependency checks, evidence storage and the explicit repository-owner pilot exception are documented in the implementation plan. The exception may waive internal promotion reviews/evidence but cannot supply a `PASS`, customer access authority or pilot acceptance. Promotion and override controls have not been implemented or tested.
- Exploit/impact: compromised rules/prompts could misclassify evidence, leak data to AI or corrupt published results.
- Required remediation: implement protected repository/pipeline, reviewer and owner-override enforcement, signing/digests, dependency/SBOM scanning, promotion, suspension and compatibility-checked rollback; execute normal and override negative/provenance tests, including visible unverified status and expiry.
- Owner: Technical owner and security owner.
- Status: Open, `NOT VERIFIED`.

### SEC-PILOT-008 — Platform support protected-evidence policy

- Severity: Medium
- Evidence: technical specification defaults to no access without separately authorized, time-bounded customer action.
- Exploit/impact: unclear incident access may lead either to unauthorized evidence disclosure or inability to resolve a critical incident.
- Required remediation: the approved pilot policy is default deny. Any future customer-authorized elevation requires a separate approved design defining purpose, duration, fields, approval, monitoring and revocation.
- Owner: Product owner and security owner.
- Status: Design decision closed 2026-09-28; implementation remains `NOT VERIFIED`; no risk accepted.

### SEC-PILOT-009 — Audit policy approved; integrity implementation unverified

- Severity: Medium
- Evidence: 12-month payload-free audit retention, access separation and deletion lifecycle are approved in `health-assessment-audit-policy.md`; storage protection, clock assurance and integrity implementation are absent.
- Exploit/impact: consequential actions may be unattributable or evidence values may leak through logs.
- Required remediation: implement the approved schema/lifecycle with restricted append-only writes, time synchronization, integrity alerts and negative payload tests.
- Owner: Security owner, data-governance owner and operations owner.
- Status: Policy decision closed 2026-09-28; implementation remains open and `NOT VERIFIED`.

### SEC-PILOT-010 — Service Bus Standard public-endpoint controls unverified

- Severity: Medium
- Evidence: the repository owner selected Service Bus Standard on 2026-09-29. Standard lacks Service Bus Private Link and virtual-network integration. ADR-0004 limits messages to opaque delivery signals and proposes Entra-only authorization, controlled static egress, TLS, an outbox and broker reconciliation; no infrastructure or tests exist.
- Exploit/impact: configuration drift, broad network access or shared-key use could permit unauthorized message injection, consumption or denial of service. Messages contain no customer evidence, but forged or lost delivery could delay, duplicate or misroute work if application authorization and idempotency fail.
- Required remediation: implement the ADR-0004 controls in Bicep; disable local/SAS authentication; restrict network access to approved egress; use distinct managed identities and least-privilege data roles; validate scope again before resolving work state; alert on denied/unusual connections; execute network, cross-customer, duplicate, outbox, dead-letter and load tests.
- Owner: Technical owner, security owner and operations owner.
- Status: Design choice approved; implementation remains open and `NOT VERIFIED`; no residual risk accepted.

## Residual risks requiring human ownership

- The pilot exception permits excess read-only source permissions after warning/audit. The product and security owner already approved that narrow policy; each occurrence still requires visible evidence and does not authorize any write/admin capability.
- Published reports may contain unreviewed Critical/High or incomplete coverage after a consultant explicitly acknowledges warnings. Product policy permits this; usability and audit tests must prove the limitation cannot be overlooked.
- AI accuracy may not exceed 80%. The pilot must fail its quality gate rather than accept the shortfall silently.
- Customer-specific modules receive generic analysis and explicit semantic gaps; reports must not imply full semantic assurance.

This review does not accept these risks or any finding. Acceptance remains with the owners named by approved policy.

## Required verification before pilot execution

- Close SEC-PILOT-001 through SEC-PILOT-007 with executable evidence.
- Implement and verify the approved SEC-PILOT-008 default-deny and SEC-PILOT-009 audit policies.
- Implement and verify SEC-PILOT-010 Service Bus Standard public-endpoint and delivery-integrity controls.
- Complete authorization matrix tests, cross-tenant penetration testing and MCP negative tests.
- Complete AI provider/data-control verification and prompt-injection/leakage evaluation.
- Complete two-environment query/permission/data-minimization validation.
- Complete renderer, CSV and expiring-link adversarial testing.
- Complete deletion, purge, backup and restore drill.
- Complete dependency/SBOM, secret scanning and artifact-provenance checks after technology selection.

## Approval

Security design approved by: Repository owner  
Date: 2026-09-28  

Pilot security verification approved by:  
Date:
