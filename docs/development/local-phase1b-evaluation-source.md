# Local saved Phase1B evaluation source capture

This is an internal library under the [cycle05 frozen contract](../../specs/003-health-assessment/local-phase1b-evaluation-source-freeze.md), not an enabled evaluation workspace. Reference SyntheticEvaluationSourceIntegration explicitly and construct Phase1BEvaluationSourceAdapter with existing run, AI and outcome stores. Pass an open compatible guarded Phase1B connection and its active transaction, existing same-actor exact Consultant read authorities and run UUID. Enter before acquiring other owning locks. Caller owns commit/rollback/disposal; capture holds registry→run→AI fences until release. The owning AI integrity read uses FOR UPDATE: use an ordinary non-mutating read-write transaction, not a SQL READ ONLY transaction.

The capture guard permits localhost/127.0.0.1, port55433, iga_synthetic and database prefix iga_synthetic_phase1b_; store configuration must exactly match the supplied connection. It reads only the complete fixed12-key scoring source with terminal AI and locked outcomes. Typed denial has null content; cancellation/infrastructure errors remain distinct. Full AI content is already authorized fictional Consultant source and adds no serving/export/storage grant. All original/native IDs and embedded owning canonical encoders remain exact. Observation time is bound by SHA256; a later capture may have a different digest.

Use pinned .NET10.0.401/Npgsql10.0.3. No new package/version is selected:

```sh
dotnet restore IgaMigrationTool.slnx --locked-mode
dotnet build IgaMigrationTool.slnx -c Release --no-restore
dotnet run --project tests/unit/SyntheticEvaluationSourceIntegration.Tests -c Release --no-build
```

For author and independent PostgreSQL consumers, follow their respective READMEs under tests/integration. Set IGA_EVALUATION_SOURCE_TEST_DATABASE or IGA_EVALUATION_SOURCE_INDEPENDENT_DATABASE to a new dedicated fixture name under their documented prefixes at127.0.0.1:55433/iga_synthetic. Each fixture creates its own fresh database and refuses existing ownership; do not precreate/reset a shared database. Preserve deliberate tamper/drift databases as evidence. No server, role or existing database configuration changes are needed.

Rollback removes the explicit new library reference; there is no stored-data migration to reverse or source repair to perform. No host, UI, HTTP, sampler, reviewer vote, source23-version mapping, customer/provider access or production deployment is activated. [Verification](../../specs/003-health-assessment/local-phase1b-evaluation-source-verification.md) distinguishes executed cases and unresolved acceptance.
