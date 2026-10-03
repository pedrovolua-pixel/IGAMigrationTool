# Local synthetic durable run and consultant view

This local demo combines [cycle 03](../../plans/completed/local-pilot-durable-consultant-cycle-03.md) durable coverage/recovery with [cycle 04](../../plans/completed/local-pilot-analysis-scoring-cycle-04.md) synthetic analysis and health calculations, and [cycle05](../../plans/completed/local-pilot-review-maturity-cycle-05.md) opt-in consultant review/history and independent maturity. Historical presets retain their original behavior. Coverage readiness pauses at `Scoring`; local projections do not complete an assessment. Eligible customer baselines, Entra sign-in, source connections, AI and publication remain unavailable. Full live and accessibility gates remain open.

## Build and run

Use repository-pinned .NET SDK 10.0.401, Node 24.21.0 and npm 11.20.0. PostgreSQL must be major 18. Create a **new disposable synthetic** database whose name starts `iga_synthetic_` on a loopback listener. Do not point at customer or shared databases. The module refuses other hosts/database names; initialization applies one additive module-owned SQL migration and refuses an existing different digest.

From the repository root:

```sh
dotnet restore IgaMigrationTool.slnx --locked-mode
dotnet build IgaMigrationTool.slnx -c Release --no-restore
cd src/web
npm ci --ignore-scripts
npm run build
cd ../..
export IGA_SYNTHETIC_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle03;Username=iga_synthetic'
dotnet run --project src/server/hosts/LocalConsultantDemo -c Release --no-build -- --synthetic-local-demo
```

Open `http://127.0.0.1:5183`. The demo accepts only this loopback origin/Host (or localhost on the same port), same-origin antiforgery-protected JSON and opaque fixed catalog IDs. It has no deployment manifest and requires the explicit synthetic startup flag. The browser stores only a selected synthetic run ID. All run history, frozen plan/versions, results, attempts and progress are in PostgreSQL.

## Recovery and limits

Start the longer recovery preset. Stop the host process mid-run and restart it with the **same** database. Once the previous five-second synthetic lease expires, the resident worker resumes missing units with a new generation; successful results stay immutable. Restarting the database temporarily makes the screen unavailable; it must not reset saved runs or claim success. Cancel persists the request, stops new work and preserves completed results. Completed coverage explicitly shows whether there are gaps and that scoring is pending.

For a deterministic recovery test, add `--pause-synthetic-worker` when restarting the explicit synthetic host; it serves saved records without executing work. The browser test harness uses this startup-only fixture to verify expired-lease Resume, then restarts its own host normally. Antiforgery keys are process-scoped and never persisted by this demo.

Five-second leases, two attempts and scripted outcomes are demo fixtures, not production policy. The local outbox/inbox exercises atomic delivery bookkeeping, not live Service Bus. Server-side exact Entra permissions, customer-data-plane resolution, immutable source eligibility, production cancellation grant, full HTTP contracts and cloud restore acceptance require their existing reviews and gate evidence.

To stop, interrupt the host. Rollback stops this executable and removes only its disposable synthetic database/cluster after saving any wanted synthetic test evidence. Do not apply a destructive down migration or delete successful results from a customer database. Fresh local builds may remove ignored `src/web/dist` and `node_modules` and reinstall from their lockfile. Temporary database files/logs and screenshots stay outside Git; no credentials or customer payloads belong in this demo or the private status site.

## Production integration review

Requested from: technical owner and security owner, with the identity administrator for deployed evidence.

Review the fixed-demo [DTO source](../../contracts/local-demo/demo-v1.schema.json), the approved [logical interfaces](../../specs/003-health-assessment/technical-spec.md) and [authorization matrix](../security/health-assessment-authorization-matrix.md). Specify the production routes/schemas, consultant cancellation/recovery grant, assignment/revocation checks, server-resolved customer data plane, identity/session/CSRF boundary and version compatibility. The demo permits no adoption of these as a production public contract by default.

Done when: the exact production contract and cancellation authority have an approved decision record, and deployed Entra/session plus customer-isolation/revocation tests pass before live UI use. This dependency does not block the explicitly synthetic local demo and cannot be closed by its automated checks.

## Phase 1B synthetic analysis presets

[Cycle 04](../../plans/completed/local-pilot-analysis-scoring-cycle-04.md) adds four named typed-fact baselines (passing controls, findings at every severity, gaps only, mixed facts) and two fixed analysis profiles (equal categories or operations-weighted comparison). These opt into the versioned synthetic analysis pack; original coverage-only presets and saved input serialization stay compatible. Old/new profile families cannot be mixed. No One Identity rule, vendor catalog, customer outcome or live source is activated.

The worker executes fixed rule predicates on immutable typed fixture facts and saves their results through the existing checkpoint path. Completed canonical coverage drives a read-only exact-input projection. New runs freeze a digest of scope, evidence, catalog and profile contents plus their complete version tuple in optional JSON run metadata. A changed lock, unsupported/missing result, wrong scope, cancelled/incomplete run or changed evidence/result fact exposes no analysis. `AnalysisFixtureDigest` is omitted when absent, preserving older request serialization/idempotency. The same existing additive SQL migration remains in use; no new table or destructive data migration is introduced.

`GET /local-demo/v1/runs/{runId}/analysis` extends only the private demo read contract. It returns explicit availability, locked content digests, original grouped findings with per-object provenance, decimal scores and separate quality. Decimal values are strings to retain exact precision. The adapter accepts engine-validated snapshots; storage reads already verify the canonical persisted JSON digest and data-plane marker. The pure scoring module does not grant source/actor authorization. Strict loopback/Host/CSRF/CSP boundaries and disabled production deployment remain unchanged.

Health follows `pilot-health-v1`: per-object weights and severity, deterministic confidence factor 1, gaps excluded from health and kept in quality, proposed mandatory-review findings excluded from publishable-current health, equal/default or selected immutable category weights renormalized over each calculation's assessed categories. Display alone rounds to one decimal, midpoint away from zero; thresholds use unrounded results. Severe findings and unreviewed warnings stay prominent even when publishable-current health appears better. Root-cause grouping never collapses scored occurrences. No eligible units show unavailable, never 100. Module/object-type/approved-fixture-outcome views reuse the same units; a fixture approval flag is not a customer approval.

The screen exposes provisional and publishable-current calculations **without publishing a report**. Run lifecycle stays at `Scoring`; full collaboration/risk authority, tasks, AI, immutable report publication and live acceptance remain later packets. Historical originals and saved results are read-only. Rollback stops the new host; it must not delete or rewrite saved coverage. The earlier host does not understand newly named analysis presets, so retain this compatible reader for the disposable database or use a separate fresh synthetic database when running an older demo binary. Never point either version at customer data.

## Cycle05 opt-in review and independent maturity

The two `synthetic-review-maturity-…-v1` profiles reuse the same four fictional analysis baselines. Only these profiles enable the approved fixed synthetic consultant identity and review writes. The older four profiles remain read-only and preserve their original locks/serialization. A new nullable maturity input digest is omitted from historical requests.

The host applies separate additive `synthetic_review` migration001 alongside the unchanged assessment migration. It refuses schema/content drift and accepts only loopback PostgreSQL18 synthetic databases. Immutable seeds bind the full validated run/analysis originals and per-object occurrence references. Confirm, Reject and Defer apply only to Proposed; Reject requires a reason. Comments and presentation edits append attributed server-clock history without overwriting generated originals. Text is encoded plain text; reason/comment/context max2000, title max250. The server fixes scope/actor/categories/grants. No risk acceptance, closure, recurrence, notification or production permissions are enabled.

`GET /local-demo/v1/runs/{runId}/review` reads one coherent current/history snapshot. `POST /local-demo/v1/runs/{runId}/findings/{findingId}/events` accepts exactly `eventId`, `expectedRevision`, `kind`, `reason`, `text`, `title`, `businessContext`, with unused text fields null. The strict private schema, same-origin antiforgery and 4KiB limit apply. Identical UUID/payload replay returns its original stored outcome; the host rereads fresh current history before returning to the UI. Changed replay/stale revisions fail atomically. After an uncertain network response, retry the same action identifier and payload; do not invent a second comment/event automatically.

For these profiles, the analysis response binds current health, quality and review history to one review-snapshot digest. Confirm/Defer keep penalties; Reject removes current penalties. Original coverage, rule facts and analysis originals remain fixed. A denied or unverifiable review suppresses the current analysis projection. The screen refreshes the whole coherent analysis after an action.

Maturity comes from separately frozen fictional indicators, never health or review dispositions. Every mandatory domain remains in the denominator, including missing indicators. Developing requires Design+Implementation in60%; Defined also80% and evidenced ownership; Managed also MeasuredOperation+RegularReview in80%; Optimized also validated improvement across two distinct assessments in80%. Earlier levels are prerequisites; separate threshold populations are global and need not be the same domains. Partial/insufficient evidence never counts as met. The screen shows domains, evidence/assessment references, reasons and ownership. Empty/invalid catalogs expose no projection. These fixture statements grant no vendor/customer evidence-validation authority.

Rollback disables the new host/profiles or retains a compatible reader. Preserve saved assessment and review records; no destructive down migration or event deletion is part of rollback.

## Cycle06 read-only canonical draft preview

[Cycle06](../../plans/completed/local-pilot-draft-report-cycle-06.md) extends the existing analysis read for the two review/maturity profiles with one detached synthetic draft. The Summary draft, Technical draft and structured Markdown text preview use its exact canonical value and digest. Health, quality, original/current finding presentation, captured attributed review history and independently frozen maturity come from the same verified source read. Technical maturity retains evidence gates and limitations. Current Scoring runs remain at coverage readiness; every draft is unpublished.

The pure ReportDrafts module clones/canonicalizes data-only inputs, binds full run/scope/version/fixture/analysis/scoring/review/maturity locks, rejects inconsistent or unsupported inputs, and has no database, route or authority dependency. Changing current review returns a new digest; an already returned value is immutable. This does not retain durable report history. Markdown is rendered as inert encoded text, including hostile reviewer text; supplied links, HTML and code never become active content. The UI rejects mismatched run/revision/source bindings and exposes retry after an unverifiable read. Historical profiles return null and preserve their earlier behavior.

Customer risk acceptance, reassessment, full recommendations/tasks/AI and actual publication/PDF/download/sharing remain explicitly unavailable. No ReportVersion, run transition, schema migration, dependency version, permission or transport operation is added. Rollback disables the field/view while preserving a compatible analysis reader and all existing saved runs/review events. Full manual accessibility, PDF and live gates remain NOT VERIFIED.

## Cycle07 structured recommendation guidance

[Cycle07](../../plans/completed/local-pilot-recommendation-guidance-cycle-07.md) adds a separate read-only current guidance value for the two review/maturity profiles. Existing frozen fictional options keep their individual IDs, text, prerequisites, risk, recovery and validation guidance. Current finding title/context/state/revision is captured separately from immutable originals and occurrence/evidence/digest references. Every option is synthetic, review-only and unverified; Confirm/Reject/Defer cannot review guidance, validate remediation or remove original options. Healthy and gap-only snapshots have no finding advice. Priority/effort/overrides, approved objectives/roles, fix artifacts/review, tasks/CSV, actual publication and execution remain unavailable.

The additive private analysis sibling binds full saved scope/run/version/analysis/review sources and a canonical content digest. Scoped option IDs include scope/run/finding/option, so repeated source option IDs across findings are distinct and presentation edits do not rename them. Refresh returns a new current detached value; no durable guidance history is claimed. Existing draft-v1 content/Markdown and historical input envelopes remain unchanged. Unknown/mismatched guidance is unavailable with no substitution; the client rejects conflicting fields/source and preserves existing read-retry/selection fences. No new route, permission, migration, package version, network/raw resolver or customer-system executor is introduced. Rollback removes the added field/view and retains compatible readers and saved history.

## Cycle10 offline preview in the localhost workspace

[The bounded Cycle10 integration](../../plans/completed/local-pilot-ai-workspace-cycle-10.md) is available from the existing catalog. Select `baseline-ai-configuration-v1` and either `profile-ai-preview-v1` or `profile-ai-preview-empty-v1`, then start the fictional assessment. Those profiles match only that baseline. Coverage reconciles its two fixed configuration outcomes; the run remains Scoring and the health analysis is Unavailable. The separate AI proposal panel displays Proposed/untrusted text, supplied citations, missing context, conflicts, declared uncertainty and frozen source digests. The empty profile explicitly has no proposals and makes no healthy-coverage claim.

The existing analysis GET supplies required nullable `aiPreview`; historical six profiles return null. New saved inputs include an omitted-when-null `AiPreviewFixtureDigest`. Valid new run details add frozen fixture and configuration-template locks; a missing saved fixture lock is never replaced. The server uses actual saved source with the fixed offline templates and real pure builder/validator/preview modules. The client checks complete closed DTO shapes, selected source locks and whole canonical/displayed agreement before exposing text. Failed coherence clears the response; keyboard retry and run selection cannot resurrect stale content. Supplied strings are React text children, with no HTML insertion, dynamic links, evidence resolver or action controls.

No new route, real provider/API key, AI persistence, dependency, production migration or permanent runtime setting. Preview reads cannot change findings, health, review, guidance, reports or publication. Keep a compatible reader for saved opt-in runs during rollback and preserve the fictional database; no destructive down migration is prescribed.

During final local verification on this Mac, an owned draft-host restart stalled before listening. A native trace ended in global `sync()`, consistent with the pinned .NET configuration watcher flushing filesystems before starting its macOS watcher thread. The exact managed caller is inferred. A temporary launch-only setting `DOTNET_hostBuilder__reloadConfigOnChange=false` allowed final draft, guidance and Cycle10 owned-host browser replays, including restarts, to pass. This disables configuration-file reload for that process only; it is not required product configuration or proof of reliable default Mac startup. Do not restart the shared PostgreSQL cluster or alter unrelated filesystems to address this issue. [Exact evidence](evidence/local-pilot-cycle10-developer-checks.json) records successful checks, failed attempts and limits.


## Cycle12 fictional fix packages in the localhost workspace

[The bounded Cycle12 integration](../../plans/completed/local-pilot-fix-workspace-cycle-12.md) adds one profile, `synthetic-review-maturity-fix-packages-equal-v1`, for the existing four fictional analysis presets. Select its **Synthetic consultant review + fictional fix packages · equal weights** label to inspect actual Cycle11 package values in the consultant workspace. It uses the existing equal analysis and independent maturity fixtures; findings/mixed presets contain fictional packages, while passing/gap-only presets contain no finding packages. Empty packages never imply healthy coverage.

One captured current recommendation-guidance value and the same captured review context feed the pure package builder. Exact saved scope, complete reconciled coverage, plan/capability/input/application/template locks and current per-finding review revisions must match. Every package and artifact remains **Unverified**. Finding confirmation cannot approve guidance or validate a fix. Current and original finding text, complete original options, prerequisites, risk/recovery, provenance/evidence references, artifact templates and digest identities appear as ordinary React text. No renderer runs in this database host.

The existing analysis GET has required nullable `fixPackages`; historical profiles receive null and omit `FixPackageTemplateDigest` from saved input JSON. The new profile freezes the unchanged `fictional-fix-templates-v1` digest and `synthetic-fix-packages-app-v1`. Full closed DTO, canonical-byte/digest, current-guidance/review and artifact-identity checks precede display. A mismatched response clears the whole analysis and offers Retry. Review refresh and Retry invalidate pending reads even if the run revision remains unchanged.

No new route, dependency, SQL migration, real provider/customer connection, package persistence, artifact approval, tasks, CSV/export/download, publication or execution. Keep a compatible reader for saved opt-in runs and preserve assessment/review data on rollback. Full sandbox, production permissions, manual accessibility, customer acceptance and Milestone7 remain open. The exact [internal contract](../../specs/003-health-assessment/local-fix-package-integration-contract.md) governs this private fixture integration.
