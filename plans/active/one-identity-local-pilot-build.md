# One Identity local pilot build

Status: Local implementation direction approved by repository owner on 2026-10-01; implementation in progress
Owner: Technical owner
Governing specifications: [health product](../../specs/003-health-assessment/product-spec.md), [health technical](../../specs/003-health-assessment/technical-spec.md), [health implementation](../../specs/003-health-assessment/implementation-plan.md), [health tests](../../specs/003-health-assessment/test-plan.md), [collector product](../../specs/001-data-ingestion/product-spec.md), [collector technical](../../specs/001-data-ingestion/technical-spec.md), [collector implementation](../../specs/001-data-ingestion/implementation-plan.md), and [collector tests](../../specs/001-data-ingestion/test-plan.md).

## Objective and boundary

Finish the pilot's locally executable product paths with versioned **synthetic** evidence, without waiting for the Environment A/B SME response, exact customer build, protected source artifact, customer SQL query review, or cloud resource provisioning. Those external facts are integration and live-use inputs, not prerequisites to implement and test their contracts locally.

Local completion is a separate engineering result. It does not mark G1–G9 `PASS`, turn a declared capability into `pilot-validated`, accept a customer baseline, enable an unreviewed source query, or authorize protected data processing. Keep runtime source access closed until the exact query pack, minimum-read principal, field policy, customer database-owner approval and applicable gates are verified. An environment-specific unknown is represented as a typed gap or blocked activation, never guessed from synthetic fixtures.

The local pilot build is a distinct checkpoint before live validation. The product owner directed Environment A/B validation to follow local pilot development. G8/G9 remain the gates for live evaluation and release readiness, and local completion does not satisfy either gate.

## Local completion slices

The owner approved a coordinator and three parallel workers on 2026-10-01. [The development workflow](../../docs/development/parallel-agent-workflow.md) assigns isolated implementation and independent verification; [cycle 01](../completed/local-pilot-parallel-cycle-01.md) records the first checkpoint-retry, assessment-composition and synthetic-integration packets. The coordinator maintains canonical records and publishes the private site after integration. Parallel execution changes delivery organization, not completion or acceptance criteria. [Cycle 02](../completed/local-pilot-parallel-cycle-02.md) now connects an internal value-free synthetic inventory to coverage planning and summaries, with a separate draft offline-receiving contract for human review. Cycle 02 does not implement or validate those paths. [Cycle 03](../completed/local-pilot-durable-consultant-cycle-03.md) now implements the synthetic PostgreSQL run/checkpoint/recovery path and actual local consultant view, with frozen inputs and explicit coverage readiness. Production baseline, identity, full scoring/maturity/review/report/AI and full local-pilot completion remain open.

| Slice | Locally reviewable outcome | External evidence deferred |
|---|---|---|
| Collector host and recovery | A Windows Service and one-shot CLI share the bounded collector core; protected configuration, lease, encrypted run directory/key, restart, retention and failure outcomes pass synthetic Windows tests. No arbitrary SQL or inbound listener. | Customer installation, real SQL credential, certificate trust, exact query pack, per-query impact approval, signed MSI. |
| Evidence transport and baseline | Versioned online/offline contracts and receiving inspection are implemented with synthetic packages, tamper/replay/size/prohibited-field tests, immutable manifest assembly and a read-only health adapter. The contract design receives its required technical/security/operations decision before code activation. | Live enrollment, cloud endpoint, signing/wrapping keys, customer package exchange, protected store and G2. |
| Phase 1A | Synthetic immutable baseline and capability snapshots drive inventory planning, exact lock, terminal coverage, gaps, quality measures, durable run/checkpoint behavior and a local consultant view. | A/B build validation, eligible customer baseline, Entra and Azure deployment. |
| Phase 1B | Versioned deterministic rule fixtures, finding provenance and lifecycle, desired outcomes, health/quality/maturity calculations, recommendations, inert fix packages and local task/CSV paths run end to end on synthetic data. AI packets and output validation use a fake provider with adversarial fixtures. | SME promotion of real build/module rules, OpenAI project/ZDR and provider execution. |
| Phase 1C | Local review and reassessment, immutable report manifest, dashboard/Markdown/PDF renderer boundary, accessibility and export tests operate on synthetic data. | Prince license and final manual PDF matrix, customer publication/share, live identity/retention/restore evidence. |
| Phase 1D | Read-only MCP projection and mutation denial run against synthetic published results and current authorization test doubles. | Deployed identity, customer isolation and external MCP exposure. |
| Platform and supply chain | ASP.NET/BFF/worker/data-plane boundaries, Bicep, schema migrations, API/client contracts, packaging, scans and synthetic integration/E2E checks are reproducible locally or in repository CI. | Azure resource IDs/capacity, deployed routing/RBAC/backup/observability, production signatures and release authorization. |

Each slice follows its approved feature plan and test plan. Do not fill an undecided enrollment, offline envelope, MSI, UI/API or security contract by assumption; make its exact proposal and decision record reviewable, then implement under the owner's standing pilot-local authorization.

## Completion accounting

- [ ] Every local slice above has a running entry point or test host with synthetic fixtures and explicit disabled live paths.
- [ ] Applicable formatting, linting, locked restore, type/build, unit, contract, integration, end-to-end, architecture, secret, dependency, license, vulnerability, package and accessibility checks run and their exact results are recorded. An unavailable check stays `NOT VERIFIED`.
- [ ] The collector can be packaged and exercised on supported Windows runners without a customer environment; production signing and customer install remain separate.
- [ ] A synthetic baseline can travel through the approved local phases without direct assessment SQL access, with gaps, provenance and immutable version locks visible.
- [ ] Failure, retry, cancellation, tampering, out-of-scope input and unauthorized operations fail closed in local fixtures.
- [ ] Feature plans, status, evidence index, operations handoff and the owner-private pilot status site reflect the same local completion state and deferred external tasks.

The current repository is **not** locally complete: it contains collector and coverage/governance primitives, a blocked collector host, partial CI, and no complete end-to-end pilot application. The remaining checkboxes must be earned by implementation and executed checks. Do not treat this plan as evidence that they have passed.

## Deferred integration and acceptance

The [SME template](../../specs/001-data-ingestion/one-identity-sme-evidence-template.md) and [Environment A response](../../specs/001-data-ingestion/one-identity-environment-a-sme-response.md) remain available for later exact-source validation. Cloud inputs and G1/G3/G5/G6/G7 execution evidence remain later deployment work. The capability matrix, source-safety controls and evidence index remain authoritative for any live customer assessment or release decision.

## First Phase 1B synthetic slice — cycle 04

[Cycle 04](../completed/local-pilot-analysis-scoring-cycle-04.md) connects saved complete synthetic coverage to fixed typed fictional rules, immutable grouped findings and reproducible decimal health/quality in the consultant view. Independent checks passed 175 rule assertions, 92 scoring groups, 399 actual PostgreSQL assertions and 712 final analysis browser checks; the historical recovery harness passed 396 checks against final assets. Exact versions/full plan/facts deny mismatched projection, and original presets retain coverage-only behavior. The matching private board publication succeeded; Linux and Windows2022/2025 partial CI passed on run36951595057 after a non-secret metadata-field scanner correction. The bounded cycle is closed as developer evidence. This bounded slice does not complete the full local pilot, Milestone 5, review/maturity/AI/task/publication paths or G1–G9.

## Consultant review/history and independent maturity — cycle05

[Cycle05](../completed/local-pilot-review-maturity-cycle-05.md) adds approved opt-in synthetic review decisions, comments, presentation edits, durable append-only history and separately frozen cumulative maturity. Coherent current health/quality uses a verified saved review snapshot, while originals and earlier four-profile input envelopes remain fixed. Local342 integrated/2,162 browser assertions and historical391/712 regressions passed; configured Linux/Windows and Azure package CI passed; owner-private publication is confirmed and the bounded cycle is closed. This is another bounded Phase1B slice. The full completion checklist, production identity/risk/closure, AI/recommendations/fix/tasks/report/MCP and live gates remain open.

## Canonical synthetic draft-report preparation — cycle06

[Cycle06](../completed/local-pilot-draft-report-cycle-06.md) prepares a read-only current draft from one coherent saved review/analysis/maturity snapshot, with summary/technical views and inert Markdown parity. Current Scoring runs do not become terminal or published. This bounded Milestone9 preparation slice adds no durable ReportVersion, publication/share/download/PDF permission or live gate. Implementation and independent local verification passed180 draft/114 Markdown/201 source-database/2137 browser assertions and historical regressions. Configured Linux/Windows/container partial CI passed on f4c7f82; owner-private publication is confirmed and the bounded cycle is closed.

## Structured synthetic recommendation guidance — cycle07

[Cycle07](../completed/local-pilot-recommendation-guidance-cycle-07.md) starts a read-only current guidance projection preserving all frozen fictional option IDs, prerequisites, risks, recovery/validation steps and original references. Captured finding status is separate from recommendation review; every option remains unverified. The additive private field does not change existing draft-v1 or historical run inputs. Three isolated implementation/UI/independent verification packets passed review and287 module/47 rendering/257 source-database/2980 final guidance browser assertions, four historical regressions and37-project/frontend/infrastructure checks. Configured Linux/Windows/container partial CI passed on6c53e66; owner-private closure publication is confirmed and the bounded cycle is closed; full Milestone7, task/CSV/fix review and live gates remain open.

## Offline synthetic AI packet and proposal validation — cycle08

[Cycle08](local-pilot-synthetic-ai-cycle-08.md) delivers the approved standalone fake-provider packet/output foundation. Closed minimized fictional packets and exact run/packet/member citations produce immutable Proposed values, with typed uncertainty/missing context and payload-free failures. Independent literal goldens and adversarial portable composition passed 233+362+978 assertions, non-author reviews closed and the combined 41-project/current local regression suite passed. Hosted Linux/Windows/package checks and all five browser workflows passed; matching owner-private publication is pending. Existing application and saved AI-disabled profiles remain unchanged. Production policy/model/token/budgets, real provider US/ZDR, actual eligibility/redaction/quality/injection/recovery/rendering and all full milestones/local completion/G1–G9 remain open. No migration, package or live activation was introduced.
