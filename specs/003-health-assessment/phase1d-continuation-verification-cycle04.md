# Phase 1D cycle04 continuation recovery verification

Status: LOCAL VERIFIED — hosted/private closure pending
Date: 2026-10-03
Authority: [exact local approval](local-phase1d-approval.md), [frozen contract](local-phase1d-implementation-contract.md), [P1D-T06/07/09/10/12](local-phase1d-test-plan.md), owner's “Next cycle”.
Tested coordinator: 1899ca0e970df4cd1a3cab24b563767370719edc.
Equivalent pushed code: be5a82ed5593c12590af27108f1b8dcc266f8b85.

## Executed outcome

The ordinary integration console now executes [ContinuationRecovery](../../tests/integration/SyntheticMcp.Tests/ContinuationRecovery.cs): 24 deterministic scenarios, 112 invocations and 1,011 assertions. Both NamedUser and Service identities exercise Findings and Recommendations, the original three-item fixture collections, at page size 1. Explicit partial field grants, exact typed values and scalar bindings come directly from original fixture bytes and [the independently pinned oracle](../../tests/integration/SyntheticMcp.Tests/ContinuationRecovery.oracle.md). Expected output uses no production projection/codec helper.

| Scenario per identity/resource | Evidence |
| --- | --- |
| Two overlapping continuations of one handle | Both reach observable selected-item barriers before audit; same ordinal item/bindings, distinct opaque successors, separate policy/audit attempts. Each successor returns the final item; the original handle remains repeatable. |
| Cancellation, exact five-second request deadline, audit false, audit throw | No content or new handle. Cancelled or DependencyUnavailable as specified. Distinct authorized retry before original cursor expiry returns the same item, then the final item. Audit outages record an attempt and safe failure signal, never an accepted spy event. |
| Revision change during selected-item loading | The trusted emission fence denies stale authority with scope-free generic audit. Revoked/regranted retries make zero manifest/item calls; a fresh page uses the new field grant. |

The source materializes immutable payload bytes before signalling the selected-item barrier; it delays return to the harness. This observes a reader-port loading race, not a post-validation barrier. Barriers await actual call arrivals, use bounded waits and release in finally; no sleeps create the overlap. Exact manifest/item scope/report/kind/IDs, policy evaluation/revision/commit calls and audit attempts/accepted in-memory events are independently checked. Caller correlations are distinct server-created fixture values. No durable audit or exhaustive cursor-capacity cleanup proof is claimed. Cursor expiry during emission remains outside this packet; retries stay below the original 300-second fixture expiry.

## Checks, review and preservation

Author and nonauthor executions passed before formatting; coordinator and nonauthor final executions passed on frozen formatted source. Original integration 2,849 assertions and cycle03 10,060 requests/92,904 assertions also pass, reported separately. Assertion counts overlap and do not represent unique requirements.

Local locked audited restores/builds for the MCP integration, publication, boundary and architecture projects pass; scoped MCP format verification and Release build pass with zero warnings/errors. Original Python oracle, publication 260, boundary 10,444, architecture 7 policy/4 project-scan cases and dependency audit of the framework-only MCP test project pass. A clean source checkout excluding stored image/PDF artifacts passed Gitleaks. Full local solution restore failed on disk allocation; whole-solution compilation/formatting, other historical PostgreSQL/browser/Windows/infrastructure checks are bound to the configured exact-source hosted workflows, with pending results remaining NOT VERIFIED.

Nonauthor review closed with no unresolved findings, independently ran the final DLL, matched all four final check-log hashes and verified oracle provenance. Initial wording overstated in-memory audit events as durable; the author corrected it and retained the original note. Hashes establish preserved bytes, not independent proof of the model's chronological inspection. Six formatting diagnostics were corrected only in the new class. The coordinator pushed an intermediate checkpoint before collecting that formatter result; native Linux formatting failure on e065747 remains historical. Final formatted source has its own fresh hosted runs. Original checkout/restore/tempfile failures from disk exhaustion remain preserved; only this cycle's reproducible checkout/generated artifacts were reduced.

All 332 pinned existing working source/contract/migration paths, original 16 fixture/oracle files and both cycle03 files remain unchanged. Exactly three test paths change: new class, oracle note and six Program lines. No runtime, public/native contract, host/UI, package/project/lock/CI configuration, migration or setting changes. [Execution receipt](../../docs/development/evidence/phase1d-continuation-cycle04-20261003.json) binds exact source, commands, logs, review and preservation. Private publication provenance is recorded separately.

## Remaining acceptance

The committed source still has no actual immutable publisher or publication reader. [Cycle02 I01–I11](phase1d-integration-intake-cycle02.md) remain open; P02-T01–18 remain proposed/unexecuted. No native integrity scheme, free-text sanitizer, client/version, protocol/SDK/registration, real grant, transport, distributed limits/cursors/audit, retention/recovery, production deadline or live release is supplied. The synchronous terminal audit limitation is unchanged. Full Milestone11/Phase1D/UAT/G1–G9 remain NOT VERIFIED.
