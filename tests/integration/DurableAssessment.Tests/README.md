# Independent durable synthetic verification

Authorized packet V3 in `plans/active/local-pilot-durable-consultant-cycle-03.md`. This executable uses actual PostgreSQL and independently authored five-key synthetic input. It does not mock database transactions, clocks, process exits, or recovery. It accepts only loopback `127.0.0.1` and database name **iga_synthetic_v3**. Provision that disposable database with UTF8 encoding before execution. It must not share a database with the demo host because each database binds one immutable synthetic scope.

Use the repository-pinned SDK. Set `IGA_DOTNET` to its absolute executable for child processes, and inherit its correct `DOTNET_ROOT`.

```sh
dotnet restore tests/integration/DurableAssessment.Tests/DurableAssessment.Tests.csproj --locked-mode /p:NuGetAudit=true /p:NuGetAuditMode=all /p:NuGetAuditLevel=low
dotnet build tests/integration/DurableAssessment.Tests/DurableAssessment.Tests.csproj --no-restore --configuration Release --disable-build-servers /p:UseSharedCompilation=false
IGA_DOTNET="$(command -v dotnet)" dotnet tests/integration/DurableAssessment.Tests/bin/Release/net10.0/DurableAssessment.Tests.dll
```

The default connection is a disposable PostgreSQL 18 service on 127.0.0.1:55433, database iga_synthetic_v3, user iga_synthetic. `IGA_SYNTHETIC_TEST_DATABASE` can override connection settings but the host/database safety checks remain mandatory. Local trust authentication is for this disposable fixture service only.

| Case       | Independent acceptance                                                                                                                                                                                                                                          |
| ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DUR-PG-001 | Concurrent/reordered identical start returns one run; changed version lock conflicts; initial5 planned / 3 terminal / 2 outstanding and full version locks are literal expectations.                                                                            |
| DUR-PG-002 | Wrong customer/project/environment refuses reads/mutation/history; constructing a differently scoped engine without initialization cannot bypass the persisted scope marker.                                                                                    |
| DUR-PG-003 | Live lease excludes overlapping worker, revision CAS rejects stale writes, terminal replay has no repeated result/checkpoint, conflicting result refuses, missing result prevents coverage completion.                                                          |
| DUR-PG-004 | Observer exception and separate process hard kill before commit each leave result/attempt/revision/checkpoint/outbox absent together.                                                                                                                           |
| DUR-PG-005 | Separate process commits then loses its response by hard kill; fresh engine reads preserved locks/results; retry idempotent; actual SQL result/plan/successful-attempt counts independent; exact2/4coverage and literal gap groups; inbox receipt deduplicates. |
| DUR-PG-006 | Cancellation refuses new work, permits begun leased work, retains completed results and locks, and cannot resurrect cancelled work.                                                                                                                             |
| DUR-PG-007 | Actual database-clock lease expiry/replacement changes fence; stale heartbeat/write/release refused; failed attempt remains distinct from retry success.                                                                                                        |
| DUR-PG-008 | Opt-in **actual PostgreSQL immediate crash/restart** retains original run and cancelled state, full locks and results.                                                                                                                                          |
| DUR-PG-009 | Immutable SQL guards; digest/column/disabled-trigger drift refuses initialization; corrupted frozen input refuses read; different trusted scope cannot rebind database. All deliberately changed test metadata is restored in finally blocks.                   |
| DUR-PG-010 | Lease expires while staged transaction is held by observer; immediately-before-commit fence refuses and rolls back result/attempt/revision/checkpoint/outbox together.                                                                                          |

DUR-PG-008 is deliberately opt-in and prints NOT VERIFIED when absent. Its local safeguard accepts only cluster directory `/private/tmp/iga-cycle03-pg`, explicit port 55433, loopback host, and socket directory /private/tmp. Set `IGA_ALLOW_TEST_CLUSTER_RESTART=1`, `IGA_SYNTHETIC_CLUSTER_DATA=/private/tmp/iga-cycle03-pg`, and `IGA_PG_CTL` to the actual executable **only after coordinating exclusive cluster use**. This stops every database in that disposable cluster; it must never target a customer or shared PostgreSQL installation. CI normally runs the real transaction/process tests without cluster restart.

The packet adds one test executable/lock file and owns no module source or migration. Module and migration copies used in the isolated checkout are excluded from its commit. PostgreSQL 18.4 local execution does not verify Azure patch 18.6, production routing/permissions, ServiceBus, backups/RPO/RTO, live assessment rules/scoring/AI or G1–G9. See `execution.md` for exact executed evidence and limitations.
