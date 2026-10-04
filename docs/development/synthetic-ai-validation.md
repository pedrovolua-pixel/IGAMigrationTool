# Offline synthetic AI validation

The cycle08 `SyntheticAiValidation` module validates fictional, already-authorized normalized/redacted configuration fixtures and fixed fake-provider proposed output. The [internal fixture contract](../../specs/003-health-assessment/synthetic-ai-fixture-contract.md) specifies exact closed shapes, local parser bounds, canonical bytes, citation membership and limits. The existing application does not call this module; no assessment profile, saved run, public transport or live AI setting changes.

Run the portable hosts after a locked solution restore and Release build:

```sh
dotnet run --project tests/unit/SyntheticAiPackets.Tests --configuration Release --no-build
dotnet run --project tests/unit/SyntheticAiProposals.Tests --configuration Release --no-build
dotnet run --project tests/integration/SyntheticAiValidation.Tests --configuration Release --no-build
```

The integration host constructs explicit fictional records, calls the real builder, obtains literal responses from a test-only fake provider and calls the real proposed-output validator. Independent goldens and denial cases are test evidence, not real-model quality or prompt-injection acceptance. Failure summaries contain codes/counts rather than evidence or response text. Production logs must never include prompts, responses, normalized/raw evidence or consultant comments.

Successful output is immutable `Proposed` data. It has no persistence, review, scoring, publication, executor, raw resolver or network side effect. Scope/classification declarations and hashes cannot authorize an actor, prove redaction or establish real evidence semantics. A schema/member validator cannot establish factual correctness or detect undeclared contradictions. Hostile strings remain inert data; no renderer is included and future consumers must encode them.

No migration, external package, credential, service or runtime setting is introduced. Rollback removes the standalone module/hosts and their solution/CI entries; existing saved runs and consultant views remain compatible. Real provider processing stays disabled pending approved policy/budget/model settings and verified US/ZDR controls. Full TP-HAS-008/012, Milestone6, SEC-PILOT-004 and G1–G9 remain open.
