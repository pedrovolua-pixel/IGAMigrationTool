# Phase 1D cycle05 occupied cursor capacity verification

Status: LOCAL VERIFIED — full hosted/private closure pending
Date: 2026-10-03
Authority: [exact local approval](local-phase1d-approval.md), [frozen contract](local-phase1d-implementation-contract.md), [P1D-T06/08/09/10/12](local-phase1d-test-plan.md), owner's “Next cycle”.
Tested coordinator: e0d6a67023c01d6d964ce246c560d403fcde23f7.
Equivalent pushed code: 413bd137210a23f301ef53643a5093c4f074e1d6.

## Executed outcome

The ordinary integration console executes [OccupiedCursorCapacity](../../tests/integration/SyntheticMcp.Tests/OccupiedCursorCapacity.cs):16 scenarios,5,200 invocations,88,562 assertions. NamedUser/Service × Findings/Recommendations × identity128/customer512 × auditfalse/throw run in separate harnesses. The [independent oracle](../../tests/integration/SyntheticMcp.Tests/OccupiedCursorCapacity.oracle.md) pins original fixture bytes, explicit partial fields and typed values,14 original scalar bindings and original digest. No production projection/codec helper determines expectations.

| Case | Executed evidence |
| --- | --- |
| Occupied quota minus one | A ledger counts distinct successfully returned handles, including retained original/final-page chains. Customer fill rotates5 identities so identity128 cannot mask customer512. Explicit active-window ledgers stay below60identity/300customer calls; all proof ends at120logical seconds before original300second expiry. |
| Final-slot terminal audit false/throw | Continuation itemb has one exact manifest/item read and policy/commit. DependencyUnavailable exposes no envelope/cursor; one attempted Success event is rejected, no accepted spy completion, and one safe AuditUnavailable signal. |
| Same-handle authorized retry | Unchanged authority returns exactb/originalbindings/digest and one new opaque successor, filling the ceiling. Failed reservation did not consume the final available slot. |
| Full registry | Another allocating continuation returns Limited with an accepted Limited audit, after expected source reads. New and representative retained terminal handles return exactc/null without allocating. Another same-customer identity progresses under the identity ceiling; another identity's retained terminal handle survives customer occupancy. |

Every invocation checks exact reader arguments, grant/request/caller/commit, audit context/schema names/correlations and attempts versus accepted in-memory events. The handle ledger is external evidence, not introspection of private registry state. Representative retained chains are replayed; not every occupied handle is individually resolved again. This is quota fairness/handle preservation, not additional cross-customer or exhaustive cleanup proof.

## Checks and review

Full-solution locked restore, final full format verification and Release build pass locally, with0warnings/0errors. Original integration2849, cycle03 10060/92904, cycle04 24/112/1011, publication260 and boundary10444 pass separately. Python oracle14 byte/hash artifacts/47denial requests, architecture7policy/4project cases, dependency audit98projects/no reported vulnerabilities and clean sparse source-secret scan pass. These are overlapping verification counts, not unique requirements.

Nonauthor source review and independent final DLL execution close with no unresolved findings. The [execution receipt](../../docs/development/evidence/phase1d-occupied-capacity-cycle05-20261003.json) binds commands, native logs, source/runtime hashes, preserved failures and limitations. Initial coordinatorc53ff61 appended the call outside Main; full build failedCS8803. Finale0d6a67 moves six additive lines inside Main and passes. The failed checkpoint was not pushed. Initial oracle pacing and scalar-count arithmetic errors are retained transparently and corrected from original contract/fixture bytes; optional author Perl hashing locale failure is retained.

All332 pinned existing source paths,16 original fixtures and both prior guards remain unchanged. Exactly3 test paths change; no runtime, host/UI, contract, configuration, dependency or migration change. Both clean temporary worktrees were removed after preserving needed source/runtime/log/review evidence; branches/Git objects remain.

## Pending scope and limits

Exact-source hosted bootstrap230/package140 started on413bd13; package job has passed, broader Linux/Windows jobs remain pending at this snapshot. PostgreSQL/browser/container/infrastructure/Windows checks were not executed locally in this cycle. [Private publication receipt](../../docs/development/evidence/phase1d-occupied-capacity-cycle05-site-20261003.json) records native publication separately.

No actual immutable publication/client input is supplied. Global4096, expiry-during-emission, durable/distributed audit/cursor/limit, production scale and frozen synchronous terminal audit deadline limits remain outside this packet. Full Milestone11/Phase1D/UAT/G1–G9 and actual-source P02-T01–18 remain NOT VERIFIED. All13 human tasks remain open.
