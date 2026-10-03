# Local Phase 1B synthetic operations handoff

Authority: the [exact Phase 1B owner approval](../../specs/003-health-assessment/local-phase1b-approval.md) and [frozen internal engineering contract](../../specs/003-health-assessment/local-phase1b-engineering-contract.md). This handoff covers the approved fictional localhost implementation. The [closure plan](../../plans/active/local-pilot-phase1b-closure.md) and its final evidence record determine verification status.

## Activation and inspection

Use pinned .NET10.0.401/runtime10.0.12, Node24.21.0 and npm11.20.0, audited locked restore and Release builds. Follow the existing [local demo](../development/local-consultant-demo.md) build commands. Select a separately created disposable PostgreSQL18 database with prefix `iga_synthetic_phase1b_`, loopback host127.0.0.1/localhost, port55433 and role `iga_synthetic`. Never restart the shared cluster or change its roles, configuration or another agent's databases.

Set the existing `IGA_SYNTHETIC_DATABASE` environment variable to that connection, then launch:

```sh
dotnet src/server/hosts/LocalConsultantDemo/bin/Release/net10.0/LocalConsultantDemo.dll --synthetic-local-demo --enable-synthetic-phase1b
```

Open `http://127.0.0.1:5183`. Choose baseline `synthetic-phase1b-baseline-v1` and profile `synthetic-phase1b-combined-v1`. Create or review fictional outcomes; the distinct server-bound fictional customer approver approves exact versions. Explicitly select approved versions, or acknowledge an explicitly empty outcome set, before starting. A new run freezes all ten Phase1B locks. Historical eleven profiles retain their prior envelopes and behavior.

For the genuine read-only fictional Auditor fixture, stop only the owned host, retain the same database and add `--synthetic-phase1b-auditor`. Auditor ordinary workspace, registry, ledger and mutations are denied. A freshly authorized protected task/finding reference exposes only metadata and scoped export. This fixture does not grant production permissions.

Local Mac execution used process-only `DOTNET_hostBuilder__reloadConfigOnChange=false` to avoid the previously recorded configuration-watcher startup stall. Default Mac startup reliability remains NOT VERIFIED. Do not convert this test override into product configuration.

## Durable behavior and recovery

One combined run schedules ten deterministic units and one fixed fake-provider work item producing two AI units. The twelve planned object/category keys remain disjoint; seven findings include two Proposed AI findings with trusted80% confidence. One benign call reserves300, charges240 and releases60. Base run/category allowances are600. A reasoned Consultant command may raise them to900/1200 while the selected work is pending or retryable. Shared project and initiating-Consultant fixture-epoch limits remain1200. Counter periods never reset on restart, override or a new run.

A reservation commits before the dispatch marker, and the marker commits before the pure callback. After interruption, recovery uses the exact logical key, packet, dispatched attempt ID and ordinal. It never blindly redispatches or substitutes an earlier receipt. Unknown usage retains300 through a terminal gap. Only a valid exact usage receipt can reconcile billing; terminal coverage and findings remain fixed. At most three known retryable attempts are permitted. Do not clear holds or edit history to resume work.

Registry, run and budget fences serialize compound operations. Outcome approval/start locks and AI accounting/coverage checkpoints commit atomically. Current scores, review, guidance, inert packages and tasks use one verified source. Missing or corrupt required proof denies the whole current projection. Retired/superseded outcomes retain their approved-at-run-lock history; new starts require current exact approved proof.

After uncertain command delivery, use the UI's explicit Retry with its original UUID and payload. Changed UUID payload, actor, scope, revision or source conflicts. A source change requires fresh inspection and explicit action. Priority/effort decisions preserve original factors and estimates; they do not rewrite task/artifact history or validate a fix. Generated packages and code stay fictional, Unverified and inert.

## Additive migrations and rollback

Activation initializes existing assessment/review/artifact/task stores plus six new module-owned SQL files:

- `migrations/synthetic-outcome-priority/001-initial.sql`;
- `migrations/synthetic-ai-execution/001-initial.sql`, `002-integrity-proof.sql`, `003-history-truncate-guards.sql`;
- `migrations/synthetic-task-csv/001-initial.sql`, `002-transfer-interrupted.sql`.

Initialization verifies exact migration digests, schema fingerprints and fixed data-plane binding. Repeated initialization is supported; drift is refused. Historical migration files are unchanged. AI002 refuses a nonempty unbound pre-proof journal: preserve the rejected database for investigation, never regenerate or bless its history. The initial experimental combined test database with incorrectly cased lock serialization is intentionally preserved and fails closed; final tests used separate fresh owned databases.

Rollback stops the owned host or disables Phase1B activation, retaining the database, saved outcomes/proposals/results, held counters, receipts and audit. The eleven historical profile readers remain compatible. Disabling the flag removes dedicated routes and combined-analysis access; it does not offer a full combined reader with mutations disabled. A later inspection must use a compatible Phase1B binary under its explicit guarded activation. `--pause-synthetic-worker` stops dispatch only and does not disable Consultant mutations; the Auditor fixture offers authorized metadata-only navigation/export. An older binary cannot understand the new profile. No destructive down migration, history rewrite, counter reset or customer migration is authorized.

## Export and downloaded copies

CSV is an exact17-column metadata-only snapshot of all saved task states and current freshness. Source-owning captures evaluate genuine Consultant/Auditor authorization and complete required proofs before minimization. Auditor needs its scoped export grant; Read/Review alone is insufficient. Fixed trusted links use `http://localhost:5183` and fresh current authorization on opening.

The renderer receives bounded minimized stdin and returns disposable bytes. Limits are1000 rows,2048 UTF8 bytes/cell,256-byte links,1MiB input,4MiB output,depth32,10s wall,256MiB managed heap,384MiB measured RSS and16KiB stderr. The current localhost CSV host runner is Mac-only and requires `/private/tmp/iga-dotnet-10.0.401/dotnet` and `/usr/bin/sandbox-exec`; it uses a nonroot sandbox. Hosted Linux verification separately exercised the pure renderer in a nonroot, read-only, network-disabled container. Linux/Windows host export remains unavailable. Full independently specified header/cell/byte parity is verified, with distinct canonical input and output hashes.

Metadata audit records Request, Dispatch, Render and Delivery or Denial. The same registry/run session fences protect the final source/authority recheck through response writing. Changed or revoked authority/source denies before the first byte. A failed write after Delivery adds a bounded exact-binding TransferInterrupted marker. Delivery means authorized access, not proof of client receipt. No durable CSV cache, retrieval token or reusable server download exists.

A user must acknowledge protected-copy handling before download. Deletion, revocation and expiry cannot recall a downloaded copy; apply the existing approved handling policy. The export contains no reasons, comments, outcome/model text, raw evidence or executable artifacts. Never put customer evidence, credentials or raw SQL on the private progress site.

## Remaining acceptance

The local checkpoint grants no human UAT, supported Windows/screen-reader/manual contrast/zoom acceptance, real model accuracy/cost/latency, customer source/build/permission verification, deployed identity/queue/worker/audit guarantees, production retention/deletion or G1–G9. No live provider, API credential, customer connection, new public contract, production migration or production release is enabled.

Approved P1B-SCOPE-01 allocates residual finding collaboration beyond Confirm/Reject/Defer/comment/presentation to Phase1C. Risk acceptance, validated remediation closure, recurrence/reassessment, evaluation, immutable publication, report/PDF/sharing and Phase1D MCP remain separately governed. Preserve full milestone criteria and the other agent's work. See the final closure evidence for exact executed checks, original failures, independent reviews and private-site deployment confirmation.
