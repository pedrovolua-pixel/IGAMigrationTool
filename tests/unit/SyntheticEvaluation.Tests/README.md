# Independent synthetic evaluation verification

V08 authors only `Program.cs` and this file. The coordinator owns project/lock/solution wiring. The worker used isolated branch `codex/m08-evaluation-verifier` from `2890b6c261bfe8ecbb2b55318a96f4391c02f2f9`.

The assertions implement the frozen internal accuracy contract independently of domain code. The entire canonical JSON literal and its SHA256 were authored before inspecting the implementation. Python hashlib independently hashed the literal before integration. The default System.Text.Json encoder escapes the plus in the invariant `O` UTC timestamp; the escaped literal has 1660 UTF8 bytes and digest `0f3488d40029bc7073e03073b20f0f570f0a3fd3e19528dc80d56f56e7a1dfdc`. A separate framework-encoder assertion checks that prerequisite. No expected JSON, count or acceptance value is created by calling the builder.

Coverage includes complete canonical bytes, source-lock sensitivity, detachment and mutable-interface refusal; every declared outcome/origin combination for both tracks; exhaustive confirmed/rejected ratios for denominators1–60; exact100000-member80% boundary and both neighboring counts; zero denominator and explicit excluded counts; missing/extra/duplicate/null membership; closed enum admission; opaque ASCII reference length/character limits; exact lowercase source digests; UTC cutoff; culture/order invariance; customer-approved desired-outcome lifecycle admission, separate denominators and absent desired-outcome threshold; size limits. Typed-denial assertions use the coordinator/domain single-invalid mapping and do not prescribe multi-invalid priority.

Focused execution evidence and non-author code-review results are recorded below once the coordinator supplies the module. This host has no provider, database, browser, UI, sampling, reviewer eligibility, live customer approval or promotion/acceptance authority. No migration, dependency or runtime flag is added by this packet. Full TP-HAS-012/019, Milestone08 and G1–G9 remain unverified.

## Executed evidence

Executed against domain commit `9e58994` using `/private/tmp/iga-dotnet-10.0.401/dotnet`:

- Initial restore and locked audited restore passed, with native logs `/private/tmp/iga-v08-restore-initial.log` and `/private/tmp/iga-v08-restore-locked.log`.
- Release build with `--no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false` passed with zero warnings/errors. Final log: `/private/tmp/iga-v08-build-final.log`.
- Direct DLL execution passed 23,550 independent assertions. Final log: `/private/tmp/iga-v08-run-final.log`.
- Initial format verification failed only on nested-loop indentation. Owned formatting was applied without assertion changes; final scoped `format --no-restore --include tests/unit/SyntheticEvaluation.Tests/Program.cs --verify-no-changes` passed. Initial, apply and final native logs are preserved under `/private/tmp/iga-v08-format-*.log`.
- Python independently rehashed the committed literal and passed its 1660-byte/exact-digest assertions. Native log: `/private/tmp/iga-v08-golden-oracle.log`.
- `git diff --check` passed. Only worker-owned Program/README are committed by V08; generated coordinator-owned project/locks remain excluded from the worker commits.

The initial unformatted independent expectations were committed as `fbfe7b8` before the worker inspected domain source. Formatting did not change the expected data or assertion count.

Non-author review inspected both domain source files and README against the exact frozen contract, FR-HAS-51–53, the evaluation/test/implementation plans and accepted ADR0001/0003. No confirmed scoped findings remain. The review checked admission/closed enum boundaries, whole-member matching, outcome conservation/correction arithmetic, zero denominator, general-only threshold, stable canonical locks/order/digest and detached read-only values. It inspected no provider, persistence, authorization or host implementation because this module introduces none. Fixture digests and approval classifications remain opaque inputs; no complete sampling cohort, real customer/reviewer authority, immutable durable version uniqueness or pilot acceptance is verified.

Whole-solution, architecture/security/secret/dependency/license checks, hosted OS execution and the exact combined-source evidence are coordinator-owned and not claimed by this worker's focused evidence. No production activation, migration, customer database change or runtime configuration occurred.
