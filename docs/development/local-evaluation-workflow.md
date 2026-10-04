# Local evaluation review workflow

This explicitly enabled fictional workspace implements the [cycle04 scope](../../plans/active/local-pilot-m08-evaluation-workflow-cycle04.md) and [frozen engineering contract](../../specs/003-health-assessment/local-evaluation-workflow-engineering-contract.md). It preserves120 original population members and100 selected members across saved reviews, corrections, current registry checks and immutable outcome versions. It uses its own host/database/frontend; the Phase1B source adapter and actual human evaluation remain later work.

## Build and run

Use the repository-pinned .NET10.0.401, Node24.21.0 and npm11.20.0. Existing web dependencies and Npgsql10.0.3 are reused; no new package version is selected.

```sh
dotnet restore IgaMigrationTool.slnx --locked-mode
dotnet build IgaMigrationTool.slnx --configuration Release --no-restore
cd src/web
npm ci --ignore-scripts
node node_modules/typescript/bin/tsc -p evaluation/tsconfig.json
node node_modules/vite/bin/vite.js build --config evaluation/vite.config.ts
```

From the repository root, select an explicitly provisioned local PostgreSQL18 database whose name begins `iga_synthetic_evaluation_`; the host never creates a database or changes a role/server configuration. Set the absolute dedicated built asset directory:

```sh
export IGA_SYNTHETIC_EVALUATION_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_evaluation_demo;Username=iga_synthetic'
export IGA_SYNTHETIC_EVALUATION_ASSETS='/absolute/repository/src/web/evaluation/dist'
dotnet run --project src/server/hosts/LocalEvaluationDemo --configuration Release --no-build -- --enable-synthetic-evaluation-workflow
```

Open `http://127.0.0.1:5184/`. Startup requires the explicit flag, connection and assets, verifies database/schema/source, and preserves an already seeded current registry and all saved history. The additive migration `migrations/synthetic-evaluation-workflow/001-initial.sql` owns only the guarded `synthetic_evaluation_workflow` schema and its six tables/immutability guards. An incompatible migration/schema or failed integrity check fails closed; there is no automatic repair, reset or down migration.

## Review and history

The fixed server-seeded fictional Consultant has two exact environment assignments. The browser cannot select an actor or administer assignments. Select a member, assess its original conclusion, choose a permitted outcome and supply the rationale/permitted fictional reference. A material correction retains the explicit originating Confirmed/Rejected classification. Severity/category corrections are presentation text and never alter source taxonomy or access rights.

Prepare review freezes its UUID, exact command and expected revisions; Record review is a separate action. A successful new command appends one event and one immutable outcome version atomically. Result unknown permits explicit refresh or retry of the exact prepared body. A conflicting revision requires refreshing and preparing a new action. The server rechecks current policy on every request, including retries. Current denial clears browser disclosure without erasing previous stored outcomes.

Related history requires independent current history permission; source visibility does not imply history or raw evidence access. Earlier outcome versions remain read-only. The original sample cutoff and evolving outcome cutoffs are distinct; registry/events never cause resampling. The server's exact Confirmed/denominator fraction, excluded counts and correction overlap remain visible with sample warnings. These values do not mark pilot acceptance.

## Verification and rollback

Focused unit, PostgreSQL and nonauthor consumer projects are `SyntheticEvaluationWorkflow.Tests`, `SyntheticEvaluationWorkflowStore.Tests` and `SyntheticEvaluationWorkflowIndependent.Tests`. Their READMEs specify fresh guarded fixtures and preserve deliberate tamper/drift databases. UI checks live in `src/web/evaluation`; independent browser/transport checks in `tests/e2e/local-evaluation-workflow`. Executed source-bound results and initial failures belong in the canonical evidence index.

Disable the host/flag to roll back availability; preserve stored immutable records and a compatible reader. No raw evidence/provider/production session, queue cancellation, adjudication, actual reviewer assignment, customer acceptance, risk disposition, reassessment, report publication or retention policy is added. Live/manual operational portions, fullM08/Phase1C/TP-HAS-012/019 and G1–G9 remain unverified.

Engineering representation and migration corrections are recorded in the [validation addendum](../../specs/003-health-assessment/local-evaluation-workflow-engineering-addendum.md); the original freeze remains unchanged. Hosted CI has new isolated Linux store/browser and Windows unit steps; final current hosted execution is tracked separately from local proof.
