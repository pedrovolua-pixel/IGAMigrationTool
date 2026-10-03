# Synthetic reviewer eligibility tests

Independent literal expected decisions frozen in [reviewer-v1 contract](../../../specs/003-health-assessment/synthetic-evaluation-reviewer-v1-contract.md) before implementation. Pure trusted-fixture capture/eligibility tests cover EV02-RT01–08/10 and bounded current snapshot checks for RT14/15; they do not execute actual human authorization, transaction/queue/history storage or RT20 audit. RT11–13/17–18 reuse the accuracy host/composed coordinator checks. No payload, provider or customer evidence is used.

Coordinator owns the test project and locked dependency configuration. Executed with pinned .NET10.0.401: locked audited focused restore, formatting verification, zero-warning Release build, and Release test host (300626 independent assertions, including100000 repeated current member/conflict decisions with requested category last in100000 allowed references and detached inputs) passed. Gitleaks8.30.1 module/test scans and git whitespace check passed. Native logs are handed to the coordinator under `/private/tmp/iga-m08-cycle03-evidence/reviewer`; these local pure-fixture results do not complete the deferred operational tests.

```sh
dotnet restore tests/unit/SyntheticEvaluationReviewer.Tests/SyntheticEvaluationReviewer.Tests.csproj --locked-mode
dotnet format tests/unit/SyntheticEvaluationReviewer.Tests/SyntheticEvaluationReviewer.Tests.csproj --no-restore --verify-no-changes
dotnet build tests/unit/SyntheticEvaluationReviewer.Tests/SyntheticEvaluationReviewer.Tests.csproj -c Release --no-restore
dotnet run --project tests/unit/SyntheticEvaluationReviewer.Tests/SyntheticEvaluationReviewer.Tests.csproj -c Release --no-build
```
