# Phase 1D cycle02 publication integration readiness

Status: PREPARED — actual-reader prerequisites open; no integration code approved
Date: 2026-10-03
Source inventory: committed7030a64b4953958ef631c820b8e247ed562f4f32
Authority: owner’s “Next cycle”; [approved local scope](local-phase1d-approval.md) and [follow-on checklist](phase1d-integration-contract-checklist.md)

## Outcome and governing requirements

The next implementation outcome is an isolated persisted-publication reader conformance adapter, conditional on the Phase1C reporting owner delivering a reviewed immutable publication contract. This cycle prepares its handoff, candidate tests and entry conditions. FR-HAS-45/46/50/55, AC-HAS-6/9/14/17/20, TP-HAS-006/009/014/017/020 and accepted ADR-0001–0003 govern it. Phase1C owns publication and delivery; Phase1D owns authorized reads and mutation denial. The completed P1D-D01–03 fixture approval is preserved.

Prior combined MCP source8e9ada7 now has complete bootstrap37150129304 success on Linux and Windows2022/2025, and package37150129349 success. The intermediate1ce75da failure remains historical. These are configured engineering checks, not full milestone or live gate acceptance.

## Committed-source inventory

| Source and exact entry point | Existing guarantee | Missing publication guarantee |
| --- | --- | --- |
| [SyntheticDurableRunEngine](../../src/server/modules/AssessmentRuns/SyntheticDurableRunEngine.cs), ReadAsync/ReadInTransactionAsync; [run contracts](../../src/server/modules/AssessmentRuns/SyntheticRunContracts.cs) | Saved scoped run, revision, inputs, coverage/results and work history | Coverage completion pauses at Scoring. Run states contain no Completed/CompletedWithGaps. No immutable ReportVersion/manifest entity. |
| [DraftReportSnapshot](../../src/server/modules/ReportDrafts/ReportDraftContracts.cs), Build/ValidateSnapshot in [DraftSnapshotBuilder](../../src/server/modules/ReportDrafts/DraftSnapshotBuilder.cs) | Detached, validated SyntheticDraft value and canonical draft digest | Unpublished; no durable ReportVersion. Inputs must remain Scoring. Immutable returned bytes are not a publication transition. |
| [DemoReportDraftProjection.Detail](../../src/server/hosts/LocalConsultantDemo/DemoReportDraftProjection.cs) | Current saved run/review/scoring/maturity inputs compose a draft; source/digest mismatches deny | The draft itself is not persisted. Refresh can observe later review data; no publisher commitment or approval. The host profile selector excludes Phase1B draft serving. |
| [StructuredDraftMarkdown.Render](../../src/server/modules/ReportDrafts/StructuredDraftMarkdown.cs) | Inert text bound to a validated draft and separate Markdown byte hash | Draft consistency does not establish published dashboard/PDF/Markdown/link/MCP parity. |
| [SyntheticReviewStore](../../src/server/modules/FindingReview/SyntheticReviewStore.cs), ReadAsync/ReadInTransactionAsync/ApplyAsync | Attributed current review and append-only history with revisions | Mutable collaboration input; review authority does not supply report.publish or a frozen report. |
| [Phase1BEvaluationSourceAdapter.CaptureAsync](../../src/server/modules/SyntheticEvaluationSourceIntegration/Phase1BEvaluationSourceAdapter.cs) | Guarded coherent observation of saved fictional AI/outcome source within caller-owned transaction | Transient Scoring capture; original/generated AI JSON and observation timestamp are not a minimized publication. Consultant source-read authority grants no MCP serving right. |
| [SyntheticMcp.IPublicationReader](../../src/server/modules/SyntheticMcp/Contracts.cs) and [PublicationCodec](../../src/server/modules/SyntheticMcp/PublicationCodec.cs) | Exact synthetic fixture bytes, original/item hashes, closed fictional IDs/text and test authority | Only a synthetic port and fixtures exist. No actual publication persistence or production reader is composed. |

Read-only inventories found no committed publisher or actual publication-reader implementation in src/server, contracts or migrations at the stated source. This is a scoped committed-source conclusion; it makes no claim about another owner’s uncommitted or future work. Saved draft *inputs* must not be described as a persisted draft/report. Source locations and independent review are bound by the cycle receipt.

## Compatibility and trust gaps

1. Native run UUIDs, Scoring states and existing version labels cannot be renamed into syn-* / Completed / SyntheticApproved to manufacture publication. The approved fictional version and goldens stay unchanged.
2. Draft/evaluation canonical digests identify different objects. Rehashing a transformed response cannot create the original publisher commitment. Record the reviewed native integrity scheme and explicit original-versus-serving mapping with historical compatibility. Per-item, serving-response or grant commitments apply only if selected and approved in that native contract; fixture mechanisms are not prerequisites by inheritance.
3. ADR-0003’s actual manifest includes provenance, classification/redaction, creation time, retention class and inputs. The fixture’s closed manifest is not that schema; adding fields to it silently is prohibited. Review a distinct native schema and explicit serving projection before implementation.
4. Real descriptions, originals, comments, business context, provenance and nested recommendations need approved category/field/text minimization. Closed fictional atoms are not a free-text sanitizer. Do not read denied content to redact it or expose it through errors, audit or cursors.
5. Real source reads, UI roles and Consultant evaluation-capture permissions cannot mint separate MCP identity/action/category grants. A caller URL, locator, inline role or draft digest cannot supply server authority.
6. Original publication bytes and frozen score/maturity/approval/warnings remain fixed. Current identity/resource/evidence availability is a separate authoritative revision/overlay. Expired report denies entirely; expired evidence can yield only authorized unavailable metadata. No raw resolution or restoration is added.
7. The trusted synthetic TryCommit fence is not a production atomicity mechanism. Its five-second check precedes synchronous append-only completion; slow audit may exceed it. Real audit durability, failure/duplicate/retry and deadline/emission semantics need explicit reviewed composition.
8. Local page/byte/rate/concurrency budgets and ephemeral random cursor registry are not production defaults. Decide topology, authenticated cursor/key/replay and distributed admission from actual client/workload inputs.

## Smallest candidate and alternatives

Recommended: finish the [owner intake](phase1d-integration-intake-cycle02.md), then review the [isolated reader candidate](phase1d-integration-implementation-candidate-cycle02.md) and [conformance matrix](phase1d-publication-conformance-test-plan-cycle02.md). It reads already-published fictional test data from the actual approved publisher contract; it creates no publication or transport. This requires a publisher and owner handoff that do not yet exist in committed source.

Two independent alternatives remain useful: prepare concrete external client/security contracts in parallel once client inputs are supplied, or continue bounded fixture regressions without claiming persisted integration. Implementing Phase1C publication belongs to its owner; duplicating it here or relabeling saved inputs is not an alternative.

## Policy already approved; implementation inputs still missing

The approved authorization matrix, identity design, audit policy, retention and recovery rules remain authoritative. Customer-data default30days and configurable clock-start, soft deletion/read denial and active purge within30additionaldays are existing policy. Audit12month retention and separate backup/replay rules are unchanged. Request exact publication lifecycle class, current-state/read-block and recovery wiring; do not reopen or invent generic retention policy.

Registration identifiers and deployment values are later protected provisioning inputs, not prerequisites for drafting contract-level schemas. However supported clients, protocol/transport/SDK, public resource/error schemas, named-user/service token validation and distinct product grants still require attributable technical/security approval before their implementation or activation. No client or version is selected in this cycle; no external protocol-conformance claim is made.

## Completion and human handoff

The intake lists exact roles, source/contract inputs and done conditions. Reader readiness closes only after required publication and policy/minimization/audit inputs have source-bound decisions, an approved implementation/test plan and an independently reviewed internal freeze. External exposure closes separately after concrete client/identity/operations decisions, verified configuration/isolation and explicit activation authority. A generic approval of this readiness document cannot supply absent source bytes or client facts. Full Milestone11/Phase1D/UAT/G1–G9 remain NOT VERIFIED.
