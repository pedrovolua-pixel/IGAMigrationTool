# Test Plan: One Identity Manager Health-Assessment Pilot

Status: Approved  
Product spec: `specs/003-health-assessment/product-spec.md` (Approved 2026-09-28)  
Technical spec: `specs/003-health-assessment/technical-spec.md` (Approved 2026-09-28)  
Owner: Quality owner  
Last updated: 2026-10-02

## Test strategy and evidence rules

Tests are traceable to immutable application, schema, query-pack, rule-catalog, prompt/model and environment versions. A pass requires executed evidence; an unimplemented or unexecuted test is `NOT VERIFIED`, never assumed. Test output must not contain credentials, protected evidence values, government identifiers, customer names or unrestricted report content.

The local pilot build checkpoint uses synthetic/fixture environments for product-path, destructive, adversarial and boundary testing. Its completion does not require Environment A/B validation or cloud resources. The later operational acceptance gate uses two independent eligible One Identity Manager 10.x SQL Server environments. No destructive test runs against a customer production source. Source interaction remains read-only in every environment.

The synthetic fixture catalog follows the families in `implementation-plan.md`: exact build/module compatibility; query/permission/baseline; the five required classes per deterministic rule; all coverage/scoring states; authorization/lifecycle; adversarial AI; renderer/export/client; work/migration/recovery; and independent 100,000-record scale generators. Every case has a stable ID, schema/build/module applicability, generator seed or input digest, expected typed result and requirement/test mapping. Golden-result changes require attributed review. Synthetic compatibility never substitutes for G8 evidence from two authorized independent pilot environments.

The human-executable companion, [human-uat-test-plan.md](human-uat-test-plan.md), maps the approved Phase 1A–1D use cases to observable UAT scenarios. It distinguishes local synthetic UAT from supervised operational UAT and does not amend the acceptance criteria or gates in this plan.

## Acceptance-criteria mapping

| Criterion | Test ID | Level | Required evidence location |
|---|---|---|---|
| AC-HAS-1 | TP-HAS-001 | Contract, integration, E2E | Inventory reconciliation artifact per run/environment |
| AC-HAS-2 | TP-HAS-002 | Unit, contract, UI/E2E | Finding schema/UI snapshots and provenance assertions |
| AC-HAS-3 | TP-HAS-003 | Unit, integration, E2E | State/score fixtures and dashboard/report warning evidence |
| AC-HAS-4 | TP-HAS-004 | Authorization, E2E, security | Denial/audit matrix and authorized customer-risk-owner success |
| AC-HAS-5 | TP-HAS-005 | Unit/property, integration | Golden score/maturity fixture bundle and digest |
| AC-HAS-6 | TP-HAS-006 | Concurrency, integration, E2E | Frozen manifest/artifact digest before and after edits |
| AC-HAS-7 | TP-HAS-007 | Integration, UI/report | Main/quality report comparison from partial baseline |
| AC-HAS-8 | TP-HAS-008 | AI security, integration | Adversarial packet/output suite and policy/audit evidence |
| AC-HAS-9 | TP-HAS-009 | Authorization/security | UI/API/worker/AI/export/share/MCP denial and audit evidence |
| AC-HAS-10 | TP-HAS-010 | Integration, E2E | Initial/reassessment occurrence and timeline records |
| AC-HAS-11 | TP-HAS-011 | Unit, integration, E2E | Acknowledgment record and unchanged finding/report digest |
| AC-HAS-12 | TP-HAS-012 | Unit/property, evaluation | Frozen sample manifest, outcome counts and denominator report |
| AC-HAS-13 | TP-HAS-013 | Accessibility, performance, E2E | WCAG results, manual AT notes and large-result evidence |
| AC-HAS-14 | TP-HAS-014 | Capability/authorization, E2E | Negative operation inventory for all deferred capabilities |
| AC-HAS-15 | TP-HAS-015 | Contract, integration, security | Exact build/module/permission/freshness record; topology absence scan |
| AC-HAS-16 | TP-HAS-016 | Contract, integration | Module/rule/coverage reconciliation and custom-module limitation |
| AC-HAS-17 | TP-HAS-017 | Unit, integration, report | Provisional/published/quality golden fixtures and rendered output |
| AC-HAS-18 | TP-HAS-018 | Security, E2E, export | Inert artifact sandbox, labels, review state and safe CSV evidence |
| AC-HAS-19 | TP-HAS-019 | Pilot evaluation, E2E | Two-environment initial/reassessment immutable evaluation report |
| AC-HAS-20 | TP-HAS-020 | Contract, E2E, security | Cross-surface manifest consistency, link controls and MCP deny list |

## Acceptance scenarios

### TP-HAS-001 — Complete assessment representation

- Build expected inventory keys from the locked baseline, capability row, installed modules, scope and rule applicability.
- Reject suspended/unsupported capability snapshots, malformed module inventories and exact product/schema/hotfix/SQL/module/query-pack/normalization mismatches before continuing to run-start gates; freeze a deterministic version lock. A local pure guard test does not establish the trusted registry, baseline adapter, authorization or durable start path.
- Assert exactly one terminal coverage state for every key and no unknown/duplicate active key.
- During a partial run, count only valid terminal results against the trusted plan. Missing keys remain outstanding; duplicate, unexpected, malformed or unexplained results deny the progress projection.
- A nonempty reconciled terminal plan with only `pass`, `finding` and explained `not_applicable` results classifies as complete; any explained limitation state classifies as complete with gaps. Empty, missing, duplicate or unexplained results deny classification. This pure classification does not start or complete a durable run.
- The internal capability-bound composition must return no combined projection when exact-version locking fails or terminal coverage is invalid/incomplete. Its synthetic fixtures bind the frozen lock to completion kind, state counts, executable numerator/denominator and grouped limitations, including zero applicable and 100,000-unit inputs. The separate versioned `SyntheticPilotFlow.Tests` host verifies independently expected primitive/composition results. These checks cover only a local subset; they do not establish baseline eligibility, trusted inventory generation, authorization, durable execution, UI or full TP-HAS-001/007 completion.
- Cycle 02's internal `synthetic-baseline-inventory-v1` descriptor must produce deterministic immutable keys only from already-authorized scope, exact-compatible fixture metadata and caller-supplied category applicability. Preserve declared gaps and permission warnings; reject malformed/unknown versions, wrong scope, blocked permission, duplicate object/native identities, conflicting category metadata and empty inventory. Independently expected fixtures must feed those actual planned keys into progress and terminal summaries, proving missing results do not complete. Reordering, mutation after planning and 100,000-key cases cover this synthetic projection only; the production manifest/eligibility reader, applicability engine, authorization, durable execution and full TP-HAS-001/007/015/016 remain unverified.
- Exercise pass, finding, not-applicable, not-assessed, insufficient, excluded, inaccessible, redacted, unsupported and error.
- Fail the test if absence is used instead of an explicit state.

### TP-HAS-002 — Evidence-backed finding

- Generate deterministic and AI findings with multiple objects/outcomes.
- Assert generated original preserves title/category, affected objects/outcomes, baseline/reference, method, rule/version, facts, inference, assumptions, severity, confidence, impact, root cause, recommendations and validation guidance.
- Verify UI, PDF and Markdown distinguish each field and resolve only authorized evidence.

### TP-HAS-003 — Critical/High review state

- Run deterministic and AI Critical/High fixtures.
- Assert state remains proposed before human review, excluded from publishable score as designed, included only in provisional score and prominently warned in dashboard/publication.
- Verify lower-severity deterministic auto-confirm behavior follows the locked catalog.

### TP-HAS-004 — Risk acceptance authority

- Attempt acceptance as consultant, reviewer, executive, auditor, MCP and wrong-customer risk owner; assert denial without resource leakage and with audit.
- Succeed only as scoped customer risk owner with owner, rationale, compensating controls, authorization and <=1-year review/expiry.
- Test missing fields, >1-year expiry, concurrent change, expiry and recurrence.

### TP-HAS-005 — Scoring and maturity

- Golden fixtures cover every severity, confidence boundary, lifecycle state, pass, informational result and gap state.
- Property tests assert score in `[0,100]`, linear per-object impact, no gap effect under default policy, equal default assessed-category weights, empty category `not assessed`, and red/yellow/green boundaries at 50/80.
- Verify overall, category, object, module and approved-outcome views and independent cumulative maturity levels.
- Recalculate from locked inputs and require identical unrounded results/digest.

### TP-HAS-006 — Immutable publication

- Publish while concurrent reviewer tries to edit; only the expected revision commits.
- Change findings/profile after publication and verify score/report manifest and rendered artifact digests remain unchanged.
- Retry render and publication idempotency keys; assert one version and deterministic content or visible failure.

### TP-HAS-007 — Quality separation

- Assess a baseline containing every gap type, rule failure and AI-budget exhaustion.
- Reconcile terminal coverage before projecting reason-level limitations; missing results or unexplained gaps must not produce a limitation summary. Keep `pass`, `finding`, and `not_applicable` out of that summary.
- Reconcile before projecting executable coverage; count `pass`/`finding` in the numerator, remove only explicit `not_applicable` units from the applicable planned denominator, retain gap states in that denominator, and report zero applicable units as unavailable rather than 100%.
- Verify health narrative remains health-focused, gap units do not improve or reduce default health, and the quality report contains complete limitations/counts/reasons.
- Verify main report contains a prominent compact limitation and link/reference to quality details.

### TP-HAS-008 — General AI boundary

- Dispatch only normalized/redacted authorized packet records and verify citations are packet members.
- Exercise embedded/encoded prompt overrides, fake roles/system messages, malicious SQL/script comments, cross-record extraction, secret/raw requests, conflicting evidence, oversized input and invalid output schema.
- Assert no tool/network/raw resolver exists, budget is atomic, output is proposed, invalid citations/output are rejected and all outcomes are audited without payload logging.

### TP-HAS-009 — Protected evidence

- For UI/API, queued worker, AI, renderer, CSV/PDF/Markdown, share link and MCP, attempt protected/raw access without category permission and customer authorization.
- Assert deny-before-read/serialization, redaction/protected-reference behavior and payload-free audit.
- Revoke permission during a session/job and assert the next access fails.

### TP-HAS-010 — Recurring finding

- Initial run creates confirmed, rejected, accepted-risk and validated-closed examples.
- Reassessment repeats the issue with stable key; assert same `FindingIdentity`, new occurrence and `reopened` timeline event.
- Exercise ambiguous/missing/changed UID; assert proposed correlation requires consultant review and no silent merge.

### TP-HAS-011 — Report acknowledgment

- Record recipient, exact version, time, delivery method and comments.
- Assert no finding disposition, risk acceptance, score or report content changes.
- Exercise duplicate client event, wrong version/scope and unauthorized actor.

### TP-HAS-012 — Reviewed-finding accuracy

- Test all Critical/High plus deterministic seeded lower-severity stratification, at least 100 or all when fewer.
- Assert formula `confirmed/(confirmed+rejected)`, strict `>80%`, exactly 80% failure, denominator display and separate indeterminate/unreviewed counts.
- Exercise 0 denominator, 1 review, <100 population, >100 Critical/High, empty strata and unavailable selected item with low-sample warnings.

### TP-HAS-013 — Accessible interactive dashboard

- Execute every workflow in `accessibility-plan.md` with keyboard and manual screen-reader review.
- Compare graph data to table/text alternative and preserve filter/focus/context across side panels and lazy loading.
- Validate PDF tags/reading order and structured Markdown stable IDs.
- Run approved-scale lists while meeting interaction targets.

### TP-HAS-014 — Phase boundaries

- Enumerate deep analysis, raw-AI retrieval, external task creation, custom-rule management, benchmarking, direct remediation and production migration operations.
- Assert no enabled UI action, API/MCP operation, worker job, credential or completion state exists in Phase 1.
- Attempt direct invocation and assert typed capability denial/audit.

### TP-HAS-015 — One Identity pilot baseline

- Validate exact One Identity build/hotfix, installed modules, query/acquisition capabilities, per-category freshness and explicit unsupported/inaccessible scope.
- Test `eligible`, warned excess-read-only and blocked write/DDL/owner/admin permission states.
- Scan baseline schema/values/metadata for prohibited topology fields and fail on any occurrence.

### TP-HAS-016 — Pilot module coverage

- Reconcile every installed approved module/foundation category against approved rule results or gaps.
- Mark uninstalled modules not-applicable.
- For a customer module/connector, verify inventory plus generic checks and explicit unsupported semantic-analysis gap.

### TP-HAS-017 — Pilot scoring publication

- Use proposed AI, proposed Critical/High deterministic, confirmed/auto-confirmed results and every evidence gap.
- Assert provisional includes eligible proposals, publishable uses reviewed/deterministic auto-confirmed states, accepted risk still reduces health, and gaps affect only quality by default.
- Verify labels and numbers match across dashboard/PDF/Markdown/link/MCP.

### TP-HAS-018 — Fix-package safety

- Generate malicious SQL/script/configuration examples containing markup, CSV formulas, shell-like text and network references.
- Assert inert encoded rendering in a network/credential-free sandbox, `unverified` label before consultant review, no execution route, and formula-safe CSV.
- Verify task/fix traces back to finding/evidence without embedding protected values.

### TP-HAS-019 — Pilot reassessment and quality gate

- Execute initial and reassessment against PILOT-ENV-A and PILOT-ENV-B after both are `pilot-validated`.
- Record exact builds/versions, inventory representation, recurrence and score-change explanations.
- Freeze/evaluate sample and assert all Critical/High, required strata/count and strictly >80% overall confirmation.
- A failed gate produces `FAIL`; no authorized role can relabel it pass without a new evaluation version.

### TP-HAS-020 — Delivery and MCP boundary

- Assert dashboard, PDF, Markdown, link and MCP identify identical report/baseline/catalog/profile/state versions and canonical digest.
- Test link entropy/verifier-only storage, separate passcode, <=24h expiry, rate limiting/lockout, revocation, no cache/index and no raw evidence.
- Enumerate MCP allowlist and deny raw values, run/start, edit/disposition, risk, publication, export/link/task and all mutation.

## Unit and property tests

- Coverage-state totality and uniqueness.
- Rule applicability across source build/module/evidence state.
- Finding transition and required-field state machine.
- Risk-acceptance expiry bounds.
- Scoring/maturity golden and algebraic properties.
- Stable root-cause/correlation keys and ambiguity behavior.
- Permission decision inputs and deny precedence.
- Cursor scope/query/snapshot binding and expiry.
- Budget reservation/reconciliation under concurrency.
- CSV formula neutralization and output encoding.
- Report manifest/digest construction and immutable version IDs.
- Retention clock, soft-delete, purge eligibility and tombstone replay.
- Evaluation sampling determinism, strata and denominator edge cases.

## Integration and contract tests

- Feature-001 baseline manifest/normalized-schema adapter, including old/new compatible versions and unsupported rejection.
- Query-pack static validation, permission attestation and checkpoint/idempotency in non-production fixtures.
- Transactional outbox, at-least-once delivery, lease expiry, duplicate work and poison jobs.
- Database/blob manifest atomicity, orphan reconciliation and digest mismatch quarantine.
- Policy engine across interactive and workload identities.
- AI provider adapter with fake provider for timeout, retry, duplicate, invalid schema/citation, retention/delete response and rate limits.
- Renderer sandbox input/output/network/filesystem/resource limits.
- Identity provider callback/session/revocation contract after selection.
- Backup inventory and isolated restore with tombstone/link revocation replay.

## End-to-end tests

- Phase 1A eligible baseline to complete coverage/quality result.
- Phase 1B deterministic plus AI assessment, scoring, recommendations, fix package, task and CSV.
- Phase 1C review, customer risk acceptance, warned publication, dashboard/PDF/Markdown/link, acknowledgment, later baseline, reassessment and evaluation.
- Phase 1D named-user and service-identity MCP reads plus complete deny list.
- Partial baseline, rule failure, AI disabled/exhausted, renderer failure and later recovery without false success.
- Soft deletion through active purge, including denied reads/jobs/links.

## Authorization and isolation tests

Execute `docs/security/health-assessment-authorization-matrix.md` for authenticated success and unauthenticated, wrong-role, wrong-customer, wrong-project, wrong-environment, wrong-assessment, wrong-evidence-category, suspended/deleted actor, revoked assignment, stale session/cache, deleted resource, identifier substitution and concurrency cases.

Add cross-tenant property tests that generate arbitrary valid resource IDs from another customer and require denial before customer-data query. Independently test operator tooling, exports, caches, queue payloads, traces, metrics, backup/restore and error messages for cross-customer leakage.

## Failure and recovery tests

- Invalid/missing/deleted baseline; incompatible catalog/profile/schema.
- Rule crash/timeout, partial result and suspended catalog version.
- Worker death before/after result commit, expired lease, duplicate event and stale checkpoint.
- AI timeout/rate limit/unknown outcome/budget race/provider deletion failure.
- Concurrent review, publication and deletion revisions.
- Renderer crash/nondeterminism/resource violation.
- Link brute force, passcode lock, clock boundary, revoke during access and customer-policy change.
- Purge partial failure/backlog and idempotent resume.
- Backup failure alert and same-region restore drill meeting the 24-hour RPO/one-business-day RTO.
- Region-wide Azure outage business-disaster tabletop validating escalation, authority, communications, dependency inventory and manual best-effort recovery without claiming a pilot regional RTO.
- Recovery proves no resurrection of deleted evidence, expired/revoked link, revoked assignment, suspended rule or cancelled job.

## Migration and compatibility tests

- Expand/migrate/contract schemas across control and multiple customer data planes.
- Mixed application/worker versions and unsupported job-payload refusal.
- Historical rule/profile/prompt/model/score/report readers.
- Blob-provider migration by customer-scoped digest.
- Old baseline adapter compatibility and explicit unsupported behavior.
- Deployment rollback without destructive schema/data rollback.

## Accessibility tests

Execute the complete `accessibility-plan.md`. Automated scanners supplement but do not replace keyboard, screen-reader, zoom/reflow, graph-equivalence and tagged-PDF manual verification. Any essential-flow WCAG 2.2 A/AA failure blocks acceptance.

- Freeze and record the exact supported Windows 11 Enterprise build, updates, Edge Stable, Chrome Stable, Firefox ESR, NVDA, Narrator and Adobe Acrobat Reader versions for each release candidate.
- Execute all workflow scenarios with NVDA/Edge; execute targeted compatibility scenarios with NVDA/Firefox ESR and Narrator/Edge.
- Execute keyboard and 200%/400% zoom/reflow coverage in all three browsers and Windows contrast-theme coverage in Edge and Firefox ESR.
- Verify the representative Prince PDF in Acrobat Reader with NVDA plus the approved automated PDF/UA checker and manual tag-tree, reading-order, table, link, heading, bookmark, alternative-text and graph-equivalence inspection.
- Fail release on any essential-flow regression in a supported combination; do not substitute browser-PDF-viewer or automated-scanner success for the approved manual matrix.

## CI execution and evidence policy

The implementation plan's change/merge gate runs applicable format, lint, type, architecture, build, unit/property, focused contract/integration, generated-contract drift, Bicep, secret/dependency/license/vulnerability, image-scan, SBOM and provenance checks. Each milestone adds the affected full integration, end-to-end, authorization, accessibility, migration/compatibility and adversarial suites. Pilot readiness runs this entire test plan, the frozen assistive-technology/PDF matrix, scale/recovery exercises and the two-environment evaluation with independent reviews. An explicit repository-owner pilot exception can authorize a specific merge or trial activation with failed internal checks; their results remain `FAIL`/`NOT VERIFIED` and do not satisfy milestone completion or acceptance.

Every required result links to the tested commit, dependency and artifact digests, contract/catalog versions, environment and test-tool versions. Manual results also record reviewer, date, defects and disposition. Failed, skipped, unavailable and unexecuted checks are `NOT VERIFIED`; no such result is reported as passing or used to satisfy a gate. Developer and CI fixtures are synthetic and evidence artifacts exclude customer payloads, credentials and tokens.

For execution gates G1–G9, verify the metadata-only `evidence-index.md` entry resolves to the exact signed/digested bundle in the restricted East US 2 engineering artifact store. G0 plan approval instead links to the approved repository plan and human decision record because the store is built after G0. Test tampering, wrong-reader denial, missing/expired bundle handling, sanitizer rejection of prohibited content, 12-month retention and active-store purge within 30 additional days. Execution evidence that has expired before pilot acceptance requires a rerun; an index row alone does not count as passing evidence. This engineering-evidence policy is separate from product audit retention.

Normal promotion tests must reject unsigned or digest-mismatched artifacts, failed or absent required evidence, self-approval, wrong reviewer role, unapproved customer/build compatibility, skipped source-safety or AI data-protection checks, and direct activation through deployment alone. Test immediate audited suspension, preservation of historical run/report versions, and reactivation of only a previously approved compatible digest. The repository-owner pilot exception tests must prove that only an explicit signed, artifact-specific, scoped, expiring owner decision can bypass any internal promotion reviewer or evidence gate; all skipped/failed checks stay factual, the exception is disclosed, expiry/revocation stops new work, and customer access authority, runtime safety, G8/G9 acceptance and production-release gates are not implicitly granted. Reject a global bypass flag, forged owner decision, wrong digest or cross-customer reuse.

## Performance and reliability tests

Required: Yes. The test data independently exercises up to 100,000 identities, accounts, entitlements, roles, workflows and records in the 90-day operational window; one environment need not contain every maximum simultaneously.

- Common dashboard/filter interactions: p95 <=2 seconds under the documented pilot concurrency/load profile.
- Detailed evidence views: p95 <=3 seconds.
- Asynchronous assessment: progress/checkpoints visible and target <=8 hours at approved scale.
- Verify per-customer fairness, queue back-pressure, cancellation, bounded AI/render work and no interactive starvation.
- Run soak/restart tests long enough to expire leases and cross scheduled backup/retention work.
- Record infrastructure, data shape, concurrency, warm/cold cache, result distribution and errors; do not generalize beyond the tested profile.

## Security tests

- Close every abuse case and SEC-PILOT finding in `docs/security/health-assessment-pilot-security-review.md` with executed evidence.
- Authentication/session/MFA/callback/revocation after provider selection.
- In a deployed Container Apps BFF, complete Entra authorization-code redemption and server-session creation using the dedicated federated user-assigned managed identity; test multiple replicas, assertion renewal, wrong issuer/subject/audience/tenant, removal of federated trust or identity assignment, session revocation and absence of assertions/tokens from browser, deployment output and logs.
- Cross-tenant penetration test and confused-deputy/workload-scope attempts.
- SQL static/permission/production-impact/data-minimization tests in two environments.
- Prompt injection, data leakage, unauthorized citation, provider policy and deletion tests.
- XSS/template injection, CSP, report/PDF/Markdown sanitization and renderer sandbox escape attempts.
- CSV formula injection across every user/model/evidence-derived text column.
- Share-link entropy, brute force, passcode delivery, cache/index/referrer and revoke/expiry.
- Secret scan, dependency/SBOM/license/vulnerability scan and artifact provenance after stack selection.
- Audit payload negative tests and integrity alerts.
- Retention/purge/restore bypass attempts.
- MCP allowlist/deny-list fuzzing and rate/concurrency abuse.

## Manual exploratory scenarios

- One Identity SME examines unusual custom module/connector and verifies generic checks do not imply semantic coverage.
- Consultant and executive assess whether severe low-confidence findings remain prominent despite numeric scoring.
- Consultant publishes with incomplete Critical/High review and gaps; reviewer verifies warnings cannot be overlooked.
- Accessibility reviewer uses a dense relationship graph and equivalent table at scale.
- Operations performs isolated restore and incident tabletop for cross-customer disclosure and source-write capability.
- Security reviewer inspects selected provider/account configuration and renderer isolation.

## Regression risks and test gaps

- Exact One Identity schemas/query text/builds remain `NOT VERIFIED` until PILOT-ENV-A/B evidence exists.
- Microsoft Entra ID, OpenAI `gpt-6-sol` and Service Bus Standard are selected, and accepted ADR-0004 defines the remaining Azure stack. Provider-specific suites remain `NOT VERIFIED` until test infrastructure is provisioned and the suites execute. Service Bus coverage must include Entra-only access, network allowlisting, cross-customer denial, duplicate delivery, outbox reconciliation, lock loss, expiry, dead-letter replay and shared-capacity load behavior.
- Vendor-default comparison fixtures depend on legally usable exact baselines.
- The pilot's non-HA, single-region PostgreSQL server creates a shared availability boundary; recoverable outage, safe failure, same-region point-in-time restore, customer extraction, tombstone replay and idempotent resume require executed evidence against the approved RPO/RTO. Region-wide Azure outage recovery is best-effort under the business disaster plan and is not represented as meeting that RTO.
- Customer production-impact acceptance cannot be simulated fully; each database owner must review actual plans/telemetry.
- Automatic accessibility tooling cannot verify complete usability or PDF reading order.
- The >80% AI gate is an outcome, not guaranteed by passing software tests.

## Test data and environment needs

- PILOT-ENV-A and PILOT-ENV-B, independently administered and authorized, with exact builds/modules and later baselines.
- Synthetic non-production SQL fixtures for every supported module, permissions, gaps, malformed/duplicate/conflict/prohibited data, query timeouts and scale.
- Approved vendor-default baselines where legally available.
- Versioned rule fixtures: positive, negative, insufficient, exclusion and compatibility for every pilot rule.
- Adversarial AI corpus containing prompt injection, conflicting context, unsupported claims, secrets/government identifiers and citation traps; prohibited values remain synthetic.
- Multiple customer/project/environment fixtures for isolation.
- Approved Windows 11 Enterprise browser/assistive-technology matrix, current Acrobat Reader and accessible PDF checker.
- Isolated backup/restore target and disposable renderer/worker sandboxes.

## Entry and exit criteria

Entry to implementation verification requires accepted architecture, approved implementation plan, selected technologies/providers, approved security/accessibility/operations designs and available test environments. Individual tests may be implemented earlier against fakes/fixtures.

Pilot verification exits only when every AC-HAS criterion is PASS, Critical/High security findings are closed or validly accepted by authorized owners, accessibility essential flows pass, RPO/RTO restore succeeds, capability rows for both environments are `pilot-validated`, and the evaluation gate passes. Any unavailable required evidence remains `NOT VERIFIED` and blocks acceptance.

## Approval

Quality owner: Repository owner  
Technical owner: Repository owner  
Security owner: Repository owner  
Accessibility reviewer: Repository owner  
Product owner: Repository owner  
Date: 2026-09-28

## Accepted D01/D02 local authentication verification — 2026-10-02

Exact public operations follow [the accepted OpenAPI contract](../../contracts/bff-authentication/bff-v1.openapi.json) and [the attributed owner decision](../../docs/development/bff-production-contract-proposal.md). The [bounded cycle](../../plans/completed/bff-authentication-contract-cycle.md) requires actual HTTPS status/body/header and strict UTF-8/field/byte-limit checks; anonymous and distinct same-subject session CSRF isolation; native top-level form navigation; removal failure before cookie clearing and other-session preservation. Supported signed synthetic OIDC proofs must check fixed issuer/client, state/correlation/nonce/code+PKCE, unsigned/missing/stale/future `auth_time`, exact 15-minute bounds, and single challenge completion after all protocol checks. Shared PostgreSQL proofs must cover replay/races, cutoff/version/role changes and failure denial. Synthetic provider tokens do not verify real Entra issuance, Conditional Access, managed-identity redemption, production guests or deployed activation. Record each executed source/run separately; unsupported or unexecuted provider, production audit/key/proxy and image promotion checks remain NOT VERIFIED.

## Planned production BFF contract tests — 2026-10-02

The [P05 test matrix](../../docs/development/bff-production-authority-hosting-proposal.md#p05--planned-tests-and-ordered-local-work) maps the proposed next local packet to IP-HAS-002/003, identity/audit policies and TP-HAS-009/014/020. Planned cases cover cross-scope/external-Member denial, command revisions/idempotency, provider refresh/revoke races, monotonic cutoffs, exact resource app roles, durable audited session mutations/unknown commits, SQL-role bypass, payload minimization, lifecycle/tombstone restore and trusted proxy/shared-key failure. At the initial proposal checkpoint none of these new tests had executed. The following owner acceptance and current execution bindings supersede that historical observation. Existing D01/D02 results remain historical verified evidence; production outage/provider/replica/cloud proofs stay NOT VERIFIED.

## Approved BFF local authority/audit cycle — 2026-10-02

The repository owner approved P01–P05 and ADR-0009 for local synthetic implementation against immutable reviewed packet `cacbe372ff8a9bc448033e6918308c5f820b4bfd`. [Execution plan](../../plans/active/bff-local-authority-audit-implementation.md) owns implementation, exact P01–P05 verification and independent review. Real provider/Azure/SQL grants, production activation, audit-outage preservation, paid deployment and G1–G9 remain separate. Executed bounded P01–P05 coverage is recorded in [the exact execution bindings](../../docs/development/evidence/bff-local-authority-audit-20261002.json) and [non-author review](../../docs/development/evidence/bff-local-authority-audit-review-20261002.md):87 authority unit/120PG,69 audit unit/81PG,258 independent restricted-role composition and60 realHTTPS/framework key seam checks passed. Host/key tests use synthetic storage/wrapping and commit-loss tests discard a successful response; actual Azure SDK/provider/witness/outage/network/backup/retention proofs remain NOT VERIFIED. Whole configured hosted gates and private publication are separately bound by the cycle; no full TP/gate checkbox is accepted from these developer results.

## Proposed HTTPS access verification — 2026-10-03 UTC

The [HTTPS-T01–T08 proposal](../../docs/development/https-pilot-portal-proposal.md#approved-local-test-plan-all-runtimelive-cases-not-verified) maps the proposed access topology to existing identity controls, TP-HAS-009/013/015 and G1. Exact packet approval precedes implementation. All runtime/live cases remain NOT VERIFIED; documentary research/review does not count as TLS, login, networking, accessibility, recovery, abuse or gate evidence. Existing approved tests and acceptance criteria remain authoritative.

## HTTPS local implementation approved — 2026-10-03 UTC

The repository owner explicitly approved [ADR-0011 Option A](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [the exact local HTTPS-P01 packet](../../docs/development/https-pilot-portal-approval.md) against immutable proposal50cf4fd. [Approval metadata](../../docs/development/evidence/https-pilot-portal-approval-20261003.json) binds the original design/technical/test/plan source. Separate optional disabled IaC and negative/input/composition checks are now authorized; the local approval task is CLOSED and implementation begins in an isolated worktree with non-author review. Public activation, real identity/permission/SQL/key/audit changes, new paid session and production release remain separately gated. No implementation pass or live evidence is claimed by approval; G1–G9/Milestone2 remain NOT VERIFIED. Private BFF/access board publication is still blocked by the separately rejected publisher credential/network action; its snapshot remains stale.


## HTTPS-P01 environment scaffold — executed local closure, 2026-10-03 UTC

The [approved bounded packet](../../docs/development/https-pilot-portal-approval.md) now stores separate optional HTTPS environment templates and input/compiled policies. Worker `e796c36` passed non-author review and is integrated as `6690955`; no application is supplied. Required false-only composition and hard-disabled public network access prevent activation. No Azure resources, public endpoint, identity/grant, migration, invitation or new paid session were created.

[Executed evidence](../../docs/development/evidence/https-pilot-portal-implementation-20261003.json): all23 Bicep build/lint checks (zero diagnostics), both new format comparisons,38 unsafe template mutations,50 unsafe input cases and51 actual CLI denials passed. Combined74-project locked restore/format/Release build (zero warnings/errors),28 unit/architecture projects, three portable integration projects and13 existing infrastructure policy commands passed. Database/frontend/browser/Windows/package checks and fresh hosted CI were not rerun in this packet; their historical evidence is not current combined verification. No public runtime or gate acceptance follows.

HTTPS-T01 has local environment/input evidence; HTTPS-T02–T08 and G1–G9/Milestone2 remain NOT VERIFIED. [The next production dependency handoff](../../docs/development/https-pilot-portal-next-packet.md) records unresolved contracts and protected proof. GitHub upload is blocked by automatic approval review pending exact public-repository publication approval; the private BFF/access board remains stale under its separate publisher block. [Active plan](../../plans/active/https-pilot-portal-preparation.md#publication-dependencies) records both dependencies and prepared board changes. Preserve unrelated Cycle13 canonical/publication evidence.


## Proposed production HTTPS contracts — 2026-10-03 UTC

The owner authorized production specification/test preparation after the published HTTPS scaffold. [The production composition proposal](../../docs/development/https-production-contract-proposal.md), [provider proposal](../../docs/development/https-production-provider-contract-proposal.md), [shared-key proposal](../../docs/development/https-production-key-contract-proposal.md), [proposed ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md) and [acceptance packet](../../docs/development/https-production-test-packet.md) are PROPOSED, not amendments to the approved product/security baseline. [The preparation plan](../../plans/active/https-production-contract-preparation.md) records bounded ownership and non-author reviews.

Sixty-eight planned host/resource/privilege, provider, shared-key, failure-audit, ingress and live-prerequisite cases are unexecuted. Available-store failure producers, anonymous receipt compatibility, total-audit-outage limitations, source-occurrence retention interpretation, real Graph normalization/paging/home limitations, missing-ring rebootstrap/rollback guards and exact-session CA-context proof are explicit. PC-D01–PC-D05 and the focused provider/key decisions require attributed technical/security/identity/platform/operations review before dependent implementation. SDK pins, manifest consistency, inventory/witness/lifecycle and exact step-up/unavailable-response contracts remain prerequisites. No code, dependency, grant, migration, actual identifier, Azure operation, public admission or spending changed.

Prior exact receipt source b065b51 separately passed [bootstrap37093087397](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37093087397) and [package37093087429](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37093087429); these completed repeat results supersede its historical pending state and are not execution of the new proposed cases. G1–G9/Milestone2 and production release remain NOT VERIFIED. Historical GitGuardian findings remain for human disposition. The private BFF/HTTPS snapshot remains stale under its separate rejected publisher action; the linked human-task delta is prepared in the acceptance packet and unrelated confirmed Cycle14 publication is preserved.


## HTTPS production local cycle 01 — owner approval, 2026-10-03 UTC

The owner approved the recommended local PC-D01–PC-D05 design after exact packet `d518d25`. [The bounded implementation/test plan](../../plans/active/https-production-local-cycle01.md) records accepted choices and unresolved exact inputs. Strict Graph response projection and Microsoft provider artifact audit are RUNNING; no cloud activation or grants. Source-only/local tests cannot satisfy live gates. The private BFF/HTTPS board remains stale under the separate publisher rejection.
