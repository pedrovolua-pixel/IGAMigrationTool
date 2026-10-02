# Local synthetic durable run and consultant view

This local demo is part of [cycle 03](../../plans/active/local-pilot-durable-consultant-cycle-03.md). It uses only fixed, value-free synthetic baseline/profile presets and explicitly scripted outcomes. Coverage readiness pauses at `Scoring`; there is no health score, eligible customer baseline, Entra sign-in, source connection, AI, finding review or publication. Full live and accessibility gates remain open.

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
