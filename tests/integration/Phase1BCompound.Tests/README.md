# Phase1B compound integration evidence

This executable uses the actual `LocalConsultantDemo` host, owning modules and PostgreSQL on the existing local cluster. It adds no product implementation and copies no host behavior as an oracle. It is bounded by the approved combined Phase1B packet and frozen engineering contract. The parent coordinator owns the shared host, solution/configuration, canonical status, browser/hosted evidence and Phase1C separation.

The only writable database is `iga_synthetic_phase1b_compound_tests` at `127.0.0.1:55433`, role `iga_synthetic`. The coordinator explicitly authorized repeatable fixture cleanup. Startup checks its exact connection identity, creates only that absent database, and resets only its seven known synthetic fixture schemas. It does not change cluster settings, roles or any other database. This is disposable test setup, separate from runtime append-only history and retention.

Executed cases exercise the actual host start with injected owning engine and AI ensure observers (the existing approved private observer is set only on the test instance via reflection); all-or-nothing outcome lock/run/outbox/AI lock visibility; accepted start replay; actual worker result and unknown-gap checkpoint rollback; exact accepted module/checkpoint replay; lease expiry after a pending write and recovery under a fresh generation; terminal identity-only billing with unchanged error coverage; actual finding review, three artifact attestations and explicit task/status commands; coherent current and stale completed task metadata; real owning proof corruption denial for outcomes, AI, finding review, artifact receipts and task receipts, with controlled rollback restoration; genuinely server-bound Auditor metadata capture/navigation and denied Consultant workspace/task mutation.

CSV cases call the actual host `IResult.ExecuteAsync` with controlled response streams. A real source change after rendering and before Execute must yield zero bytes and a durable Denial. A blocked write holds the source session fence after authorization audit commit: the test observes the actual advisory-lock waiter in PostgreSQL while an actual Review mutation attempts to enter, verifies exact CSV bytes, then verifies mutation completion only after write release. A cancelled response stream triggers the actual host WriteAsync catch and one bounded TransferInterrupted marker bound to the original snapshot/output/count. These tests do not claim a Kestrel socket-abort, browser, hosted Linux container or production authentication test.

Run with the pinned SDK from the final repository root:

```sh
PATH=/private/tmp/iga-dotnet-10.0.401:$PATH /private/tmp/iga-dotnet-10.0.401/dotnet run --project tests/integration/Phase1BCompound.Tests/Phase1BCompound.Tests.csproj -c Release
```

During bounded writer integration, pass `-p:Phase1BHostProject=/private/tmp/iga-phase1b-coordinator/src/server/hosts/LocalConsultantDemo/LocalConsultantDemo.csproj`; the default reference remains repository-relative. The configured renderer defaults to the built repository worker closure. An explicit `PHASE1B_COMPOUND_RENDERER` may point to that same approved built closure when the host/test worktrees differ.

Actual counts, commands, source/binary bindings and remaining checks are recorded alongside `execution.log` and `execution.json`. Assertions are executed checks, not a claim that all Phase1B gates or Phase1C are closed.
