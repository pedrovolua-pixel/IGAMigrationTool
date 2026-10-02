# Local synthetic durable run and consultant view

This local demo combines [cycle 03](../../plans/completed/local-pilot-durable-consultant-cycle-03.md) durable coverage/recovery with [cycle 04](../../plans/completed/local-pilot-analysis-scoring-cycle-04.md) read-only synthetic analysis and health calculations. Historical presets retain scripted coverage-only behavior; new analysis presets execute fixed fictional predicates over typed synthetic facts. Coverage readiness pauses at `Scoring`; new presets expose local health/quality projections without completing an assessment. Eligible customer baselines, Entra sign-in, source connections, AI, mutating finding review and publication remain unavailable. Full live and accessibility gates remain open.

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

The screen exposes provisional and publishable-current calculations **without publishing a report**. Run lifecycle stays at `Scoring`; full finding review/collaboration/risk authority, maturity indicators, tasks, AI, immutable report publication and live acceptance remain later packets. Historical originals and saved results are read-only. Rollback stops the new host; it must not delete or rewrite saved coverage. The earlier host does not understand newly named analysis presets, so retain this compatible reader for the disposable database or use a separate fresh synthetic database when running an older demo binary. Never point either version at customer data.
