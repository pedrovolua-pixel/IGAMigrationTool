# Phase1B source capture verification precision

Status: Engineering clarification of the frozen capture contract; no new behavior/authority
Date: 2026-10-03

The immutable [contract](local-phase1b-evaluation-source-contract.md) and [freeze](local-phase1b-evaluation-source-freeze.md) remain the reviewed source. The owner run engine's `ReadInTransactionAsync` emits `SyntheticRunIssue.InvalidState` when its scope/binding is not initialized; capture maps that specific owner result to `NotInitialized`. This is distinct from a saved Planned/Running/Failed/Cancelled or incomplete run, which capture rejects as `SourceUnavailable`. The contract's generic owner InvalidState/source-state description is interpreted with this exact existing run-owner meaning; AI/outcome InvalidState still maps to `SourceUnavailable`.

Supplied default/malformed role vectors, completed/disposed transactions and invalid caller fence order are input/authority denials with no capture. No exception detail, source identifiers or partial payload accompanies a typed denial. Infrastructure failure and cancellation remain distinct; the adapter does not turn an unavailable infrastructure dependency into a successful empty source.

Executed evidence, findings and unresolved cases are maintained separately in the cycle05 verification record. No source-backed sample/review, production identity, raw resolver, permission, storage migration or acceptance result follows from these internal input-closure details.
