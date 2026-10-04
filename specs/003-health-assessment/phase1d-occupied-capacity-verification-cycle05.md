# Phase 1D cycle05 occupied cursor capacity verification

Status: VERIFIED — approved synthetic cycle completed
Date: 2026-10-03
Authority: [exact local approval](local-phase1d-approval.md), [frozen contract](local-phase1d-implementation-contract.md), [P1D-T06/08/09/10/12](local-phase1d-test-plan.md), owner's “Next cycle”.
Tested coordinator: e0d6a67023c01d6d964ce246c560d403fcde23f7.
Equivalent pushed code: 413bd137210a23f301ef53643a5093c4f074e1d6.

## Executed outcome

The ordinary integration console executes [OccupiedCursorCapacity](../../tests/integration/SyntheticMcp.Tests/OccupiedCursorCapacity.cs):16 scenarios, 5,200 invocations, 88,562 assertions. NamedUser/Service × Findings/Recommendations × identity 128 / customer 512 × audit false / throw run in separate harnesses. The [independent oracle](../../tests/integration/SyntheticMcp.Tests/OccupiedCursorCapacity.oracle.md) pins original fixture bytes, explicit partial fields and typed values,14 original scalar bindings and original digest. No production projection/codec helper determines expectations.

| Case | Result | Executed evidence |
| --- | --- | --- |
| Occupied quota minus one | PASS | A ledger counts distinct successfully returned handles, including retained original/final-page chains. Customer fill rotates five identities so identity128 cannot mask customer512. Explicit active-window ledgers stay below 60 identity / 300 customer calls; all proof ends at 120 logical seconds before original 300-second expiry. |
| Final-slot terminal audit false/throw | PASS | Continuation itemb has one exact manifest/item read and policy/commit. DependencyUnavailable exposes no envelope/cursor; one attempted Success event is rejected, no accepted spy completion, and one safe AuditUnavailable signal. |
| Same-handle authorized retry | PASS | Unchanged authority returns exact b / original bindings/digest and one new opaque successor, filling the ceiling. Failed reservation did not consume the final available slot. |
| Full registry | PASS | Another allocating continuation returns Limited with an accepted Limited audit, after expected source reads. New and representative retained terminal handles return exact c / null without allocating. Another same-customer identity progresses under the identity ceiling; another identity's retained terminal handle survives customer occupancy. |

Every invocation checks exact reader arguments, grant/request/caller/commit, audit context/schema names/correlations and attempts versus accepted in-memory events. The handle ledger is external evidence, not introspection of private registry state. Representative retained chains are replayed; not every occupied handle is individually resolved again. This is quota fairness/handle preservation, not additional cross-customer or exhaustive cleanup proof.

## Requirement mapping

| Requirement | Result in this bounded cycle | Evidence / remaining limit |
| --- | --- | --- |
| P1D-T06/T11 | PASS for tested pages | Exact original b/c values, partial fields, fourteen scalar bindings and original manifest digest on recovered and representative retained pages. |
| P1D-T08 | PASS for tested quotas | Identity128/customer512 final-slot recovery and nonallocating final pages; five-identity and request-window ledgers separate quota dimensions. Global4096 and exhaustive cleanup add no new proof here. |
| P1D-T09 | PASS for clock separation | All calls finish at 120 logical seconds before original 300-second expiry; no expiry-during-emission or production deadline decision. |
| P1D-T10 | PASS for local audit port | Exact rejected Success attempt versus accepted Limited event, no denied payload/cursor and safe failure signals. Durable audit remains NOT VERIFIED. |
| P1D-T12 | PASS for source scope | Exactlythree test paths change, original fixtures/guards and332source pins preserved, architecture checks pass; no production activation. |
| Actual-source P02-T01–18 / full Milestone11 / UAT / G1–G9 | NOT VERIFIED | Required native publication/client inputs and their own evidence/approval are still absent. |

## Checks and review

Full-solution locked restore, final full format verification and Release build pass locally, with0 warnings / 0 errors. Original integration 2,849, cycle03 10,060 requests / 92,904 assertions, cycle04 24 scenarios / 112 invocations / 1,011 assertions, publication 260 and boundary 10,444 pass separately. Python oracle 14 byte/hash artifacts/47 denial requests, architecture 7 policy / 4 project cases, dependency audit98projects/no reported vulnerabilities and clean sparse source-secret scan pass. These are overlapping verification counts, not unique requirements.

Nonauthor source review and independent final DLL execution close with no unresolved findings. The [execution receipt](../../docs/development/evidence/phase1d-occupied-capacity-cycle05-20261003.json) binds commands, native logs, source/runtime hashes, preserved failures and limitations. Initial coordinator c53ff61 appended the call outside Main; full build failed CS8803. Final e0d6a67 moves six additive lines inside Main and passes. The failed checkpoint was not pushed. Initial oracle pacing and scalar-count arithmetic errors are retained transparently and corrected from original contract/fixture bytes; optional author Perl hashing locale failure is retained.

All 332 pinned existing source paths,16 original fixtures and both prior guards remain unchanged. Exactlythree test paths change; no runtime, host/UI, contract, configuration, dependency or migration change. Both clean temporary worktrees were removed after preserving needed source/runtime/log/review evidence; branches/Git objects remain.

## Hosted closure and limits

Exact-source [bootstrap 230](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37162503717) and [package 140](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37162503644) on 413bd137210a23f301ef53643a5093c4f074e1d6 completed successfully. Linux and Windows 2022/2025 jobs passed the ordinary new guard, whole-solution restore/format/build, historical PostgreSQL/browser and configured infrastructure checks. Those broader checks were executed hosted; they were not executed locally in this cycle. [Private publication receipt](../../docs/development/evidence/phase1d-occupied-capacity-cycle05-site-20261003.json) preserves local checkpoint 114 and final completed-cycle native provenance. These configured partial gates do not establish full pilot acceptance.

No actual immutable publication/client input is supplied. Global4096, expiry-during-emission, durable/distributed audit/cursor/limit, production scale and frozen synchronous terminal audit deadline limits remain outside this packet. Full Milestone11/Phase1D/UAT/G1–G9 and actual-source P02-T01–18 remain NOT VERIFIED. All 13 human tasks remain open.

## Concurrent integration qualification

After the frozen Phase1D checks, another approved M08 cycle07 coordinator integrated its source and canonical records through2b76e3a. Its change to SyntheticAiExecutionStore.cs is preserved. The332 original source pins bind the frozen e0d6a67/413bd13 snapshots and the Phase1D patch; they do not claim the later combined root is byte-identical. All three tested MCP paths still match. Full hosted evidence here validates413bd13; the later M08 integration has its own source-bound evidence.
