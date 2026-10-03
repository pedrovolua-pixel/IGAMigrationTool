# Synthetic warning/regression checks

G03 implements approved EV02-S03–04, TP-EV02-S011–020. Standalone host with independent rational integer boundary cases, literal canonical envelopes/digests, denominator/desired separation, metadata/compatibility/coverage/safety denial and immutability checks. Coordinator owns project wiring. No database, host, real safety assessment, reviewer authority or acceptance checks are implied. Pinned SDK10.0.401 focused audited locked restore, format verification and Release build with zero warnings/errors executed. Focused host passed6,144 assertions; unchanged accuracy host passed23,550. Empty canonical goldens were frozen before domain implementation; additional nonempty goldens were independently authored from the contract with Python JSON/hash logic (without product output), then compared to product bytes. Whitespace failure introduced in expanded tests was preserved and repaired with dotnet format; final checks rerun. Nonauthor implementation review and coordinator combined checks remain pending.

```sh
/private/tmp/iga-dotnet-10.0.401/dotnet restore tests/unit/SyntheticEvaluationRegression.Tests/SyntheticEvaluationRegression.Tests.csproj --locked-mode
/private/tmp/iga-dotnet-10.0.401/dotnet format tests/unit/SyntheticEvaluationRegression.Tests/SyntheticEvaluationRegression.Tests.csproj --no-restore --verify-no-changes
/private/tmp/iga-dotnet-10.0.401/dotnet build tests/unit/SyntheticEvaluationRegression.Tests/SyntheticEvaluationRegression.Tests.csproj -c Release --no-restore -warnaserror
/private/tmp/iga-dotnet-10.0.401/dotnet run --project tests/unit/SyntheticEvaluationRegression.Tests/SyntheticEvaluationRegression.Tests.csproj -c Release --no-build
```

Local native logs are in the coordinator evidence staging directory `regression/`. This packet has no dependency, migration, configuration or activation change; shared project/lock wiring is coordinator-owned.
