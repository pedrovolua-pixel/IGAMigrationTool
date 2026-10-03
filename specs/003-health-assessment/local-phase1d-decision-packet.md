# Phase 1D local preparation decision packet

Status: PREPARED — exact local decision pending
Requested from: Repository owner acting as technical and security owner; Phase 1C reporting owner for the publication handoff
Last updated: 2026-10-03

## Concrete requested decision

Approve P1D-D01–D03 in the [contract proposal](local-phase1d-contract-proposal.md) together with its [paired test matrix](local-phase1d-test-plan.md), for bounded local synthetic implementation only:

| Decision | Recommended exact local choice | What approval enables |
| --- | --- | --- |
| P1D-D01 | Dependency-free in-process harness; explicit synthetic publication fixture with immutable manifest/payload binding; frozen published status, separate future current-status read | Internal source/projection implementation and authored fictional fixtures |
| P1D-D02 | Six closed resource kinds; separate MCP fixture grants; minimize before serialization; explicit total mutation/raw denial; bound opaque registry cursors; payload-free audited completion and generic failure | Synthetic policy, dispatch, cursor and audit composition tests |
| P1D-D03 | Proposed page/byte/deadline/rate/concurrency/cursor budgets as local-only parameters; cancellation/restart/expiry behavior | Bounded local reliability and independent adversarial verification |

These choices are proposals. The owner's “Do it” approved preparation, not these exact previously unrecorded semantics. Review both documents and record approval or requested amendments against their exact commit and SHA-256 digests. Do not treat an agent review as human approval. No protocol, SDK, public path/schema, real token/grant, registration, live provider/source, retention change, production budget or release is authorized. Later routine internal engineering may be frozen and independently reviewed within an approved packet; a consequential change returns for review.

## Phase 1C dependency and handoff

Current `src/server/modules/ReportDrafts/ReportDraftContracts.cs` expressly returns a detached draft without durable ReportVersion/publication. It cannot serve as a published source. Phase 1C owns publication semantics/persistence/approval and delivery parity; Phase 1D owns reads, policy application and prohibited-operation tests. Agree the immutable reader schema, scope/version/digest validation and source availability before writing its adapter. Full Phase 1C/PDF/share acceptance need not block independent fictional harness work, but Phase 1D integration requires actual reviewed published inputs. Never overwrite another worker's source or canonical records.

## Completion condition for the human task

An attributable technical/security-owner decision references exact proposal/test bytes and approves or amends all three choices for the local scope. The reporting owner acknowledges the synthetic-vs-real handoff and schedules its separate exact publication adapter review. Amendments are reviewed and rehashed before code. Actual publication integration and live MCP client approval remain separately visible dependencies after this local task closes.

## Verification and source binding

The [preparation plan](../../plans/active/local-pilot-phase1d-preparation.md) records documentary validation, independent review, proposal/test hashes and private-board publication. See its evidence links for the exact reviewable snapshot. Approval: PENDING. No Milestone 11 checkbox or G1–G9 state changes.
