# Synthetic Phase 1D local harness

Status: LOCAL ENGINEERING VERIFIED — bounded synthetic scope
Authority: [exact local approval](../../specs/003-health-assessment/local-phase1d-approval.md)
Contract: [frozen internal contract](../../specs/003-health-assessment/local-phase1d-implementation-contract.md)
Test matrix: [P1D-T01–12](../../specs/003-health-assessment/local-phase1d-test-plan.md)

This framework-only module has no ordinary host reference, listener, external MCP transport, provider token validator, customer evidence source or real grant. The independent console host is an executable synthetic verification entry point. It reads only versioned fictional fixture files and prints assertion counts and safe outcome labels; it opens no network port and requires no provider/customer/environment configuration.

## Reproduce the local checks

Use the exact SDK in global.json. Restore locked dependencies and build the solution before running:

```sh
dotnet restore IgaMigrationTool.slnx --locked-mode
dotnet format IgaMigrationTool.slnx --verify-no-changes --no-restore
dotnet build IgaMigrationTool.slnx -c Release --no-restore
python3 tests/integration/SyntheticMcp.Tests/fixtures/author_oracle.py --verify
dotnet run --project tests/unit/SyntheticMcpPublication.Tests -c Release --no-build
dotnet run --project tests/unit/SyntheticMcpBoundary.Tests -c Release --no-build
dotnet run --project tests/integration/SyntheticMcp.Tests -c Release --no-build
dotnet run --project tests/architecture/BoundaryChecks -c Release --no-build
```

The [source-bound verification](../../specs/003-health-assessment/local-phase1d-implementation-verification.md) records commands actually run, original failures, review fixes and unexecuted follow-on cases. A matching fictional golden proves fixture compatibility, not a persisted ReportVersion, live policy or protocol implementation.

## Local state and failure handling

Source metadata and minimized item reads are separate. Policy decides resource/scope/category before content reads. Original canonical manifest and per-item bytes are checked against independently pinned hashes; draft/Scoring and unknown versions are unavailable. Current evidence availability is a labeled authorized overlay; it cannot change immutable historical provenance or resolve raw evidence.

Requests carry only a closed fictional resource selector and optional collection page/cursor. The test host supplies all identity/grant/source/clock state. An unknown operation has no source or business mutation capability. Result denials are generic and audit records contain enums, schema names and safe scoped metadata only.

Local cursor/rate/concurrency registries are bounded and ephemeral. Each new harness starts without cursors. Continuation rechecks current authority and immutable publication identity; source/authority/audit failures return no partial content. A5second monotonic deadline and caller cancellation stop processing before the terminal synchronous audit/emission commit and release concurrency/cursor reservations. The final check occurs before append-only completion begins; later cancellation or elapsed time cannot retract that successful atom. A slow synchronous audit can exceed5seconds and hold the fence, so this is not an end-to-end wall-clock bound. Production audit/deadline/reliability integration remains NOT VERIFIED. Separate retries consume admission budget, reauthorize and emit their own completion event. Local numeric budgets apply only inside this fixture harness and cannot configure production.

## Rollback and future integration

Rollback removes the additive SyntheticMcp module, its three console projects and portable CI steps. There is no migration or persisted business state and no normal-host capability flag to toggle. Keep original oracle/golden bytes and evidence after cleanup.

The [follow-on checklist](../../specs/003-health-assessment/phase1d-integration-contract-checklist.md) asks the reporting/technical/security owners for the actual immutable publication reader and external client/identity contract. Actual production policy/field filtering, distributed cursor/limit/audit/retention and deployed cross-customer isolation remain unverified. Full Milestone11/Phase1D/UAT/G1–G9 stay open.

## Cycle03 minimization regression

The ordinary `dotnet run --project tests/integration/SyntheticMcp.Tests -c Release --no-build` command now also runs the independent field/category/identity cross-product. [Cycle03 verification](../../specs/003-health-assessment/phase1d-minimization-verification-cycle03.md) and the [pinned oracle](../../tests/integration/SyntheticMcp.Tests/MinimizationCrossProduct.oracle.md) describe10,060 requests/92,904 assertions separately from the original2849 checks. The guard uses the unchanged fictional fixture only, verifies exact read IDs/canonical responses/audit schema names, and exits nonzero on failure. It adds no host, option, package or runtime capability. Actual integration and all prior operational limits remain open.

## Cycle04 continuation recovery regression

The same ordinary integration command also executes [cycle04 continuation verification](../../specs/003-health-assessment/phase1d-continuation-verification-cycle04.md): 24 deterministic scenarios, 112 invocations and 1,011 assertions. Its [fixture-derived oracle](../../tests/integration/SyntheticMcp.Tests/ContinuationRecovery.oracle.md) verifies overlapping repeatable pages, cancellation/deadline/audit-failure retry and stale-revision/regrant denial. Barrier and audit records are in-memory test observations; they do not prove durable audit or real publication. No additional option, dependency, migration, configuration or listener is introduced.
