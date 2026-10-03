# Phase 1D cycle03 independent minimization verification

Status: LOCAL VERIFIED — approved synthetic regression only
Date: 2026-10-03
Authority: owner’s “Next cycle”; [exact local approval](local-phase1d-approval.md), frozen [internal contract](local-phase1d-implementation-contract.md) and [P1D-T03/05/11/12](local-phase1d-test-plan.md).
Tested coordinator source: ab0459ca45bd53793582236ec7c21675baba2cfd.
Pushed equivalent test source: cb38bb69483e3983a1ccb84734001915cdbadb6f.

## Executed result

[The independent probe](../../tests/integration/SyntheticMcp.Tests/MinimizationCrossProduct.cs) is now called by the ordinary integration console and its existing portable CI entry point. It executes10,060 requests and92,904 assertions separately from the original2849 integration checks. Expected canonical envelopes, typed values, linkage, audit schema names, frozen scalar bindings and original manifest digest derive from unchanged Python-authored fixtures and [explicit oracle rules/provenance](../../tests/integration/SyntheticMcp.Tests/MinimizationCrossProduct.oracle.md), not production Projection helpers.

| Partition | Executed calls | Contract coverage |
| --- | --- | --- |
| All optional field subsets × all16 category subsets × NamedUser/Service across six resources | 8,448 | P1D-T03/05/11: category selection before item reads, exact granted fields/values, immutable bindings/digest |
| Full optional fields with neither/itemId-only/category-only mandatory grant | 576 | Authorized rows always retain itemId/category under the frozen fixture contract |
| Additional Unavailable/Expired and Redacted/Redacted reference overlays | 1,024 | Current authorized overlay fields separate from frozen availability/reason |
| Unknown field grant for each resource/identity | 12 | Dependency denial before source reads; no raw field echo or scoped audit-field disclosure |

Read spies record exact scope/report/kind/item order; overlay spies record only selected reference IDs. Every unselected payload is poisoned in memory, while original fixture files remain unchanged. Findings’ reference arrays omit denied-category referents without reading their contents; recommendation findingId disappears for a denied referent. Exact audit unions include referenceIds in both returned and redacted lists when its contents are filtered. This is source-read instrumentation, not newly instrumented raw/business-write ports or deployed customer-isolation proof.

The fixed fixture has descriptors in every resource group. Empty category grants deny before manifest reads; nonempty grants selecting no descriptors may read the manifest but no items and yield generic Unavailable. This does not change genuinely empty collection semantics exercised by the original suite. No success/partial content/count/cursor is returned for those denied cases.

## Combined checks and preservation

On the exact combined source:94-project locked audited restore, whole-solution format and zero-warning/error Release build; original Python oracle (14 byte/hash artifacts and47 denial vectors); publication260, boundary10,444 and original integration2849 assertions; the new10,060/92,904 probe; architecture7 policy/4 project-scan checks; whole clean-source secret scan and94-project dependency audit all passed. Assertion counts overlap and do not measure unique requirements.

All294 initially pinned server/web/contract/migration working paths match their cycle03 hashes; all323 prior whole-source working hashes were independently reverified. Clean production/host/UI/DTO/migration bytes and16 original fixture/oracle files remain unchanged. Exactly three test paths changed: the new class, its oracle note and the six-line Program entry-point call. No project, lockfile, CI configuration, dependency, migration, product setting or runtime behavior changed.

Nonauthor C03-V review is closed with no unresolved actionable findings. Original formatting exit2/four whitespace diagnostics and incidental host-locale shasum exit9 are retained; only the new class was formatted and Python supplied subsequent hashes. No production defect or unintended runtime failure was observed. The initial pre-production oracle snapshot is preserved; its partial-reference audit interpretation was amended after inspection and independently confirmed against the frozen contract. The normal entry-point wrapper reports probe failures and sets exit code1. An isolated external test-only compile substitution deliberately throws in the new guard: the actual Program entry point still passes its original2849 checks, then returns exit1 with the new failure label and no new-probe PASS. No tracked source/runtime file changes for this control.

[The execution receipt](../../docs/development/evidence/phase1d-minimization-cycle03-20261003.json) binds exact commands/source/oracle/probe/review/log hashes and native hosted observations. Private board publication is proven separately; no publication success follows from local preparation alone.

## Remaining acceptance

[Cycle02 I01–I11](phase1d-integration-intake-cycle02.md) are still open: no actual immutable publication contract/client handoff was provided. This cycle executes none of proposed P02-T01–18 and does not approve a reader, publisher, native integrity scheme, free-text sanitizer, client/version/protocol/SDK, registration or live grant. Durable/distributed authority/limits/audit, real publication parity, retention/recovery, manual accessibility, full Milestone11/Phase1D/UAT/G1–G9 remain NOT VERIFIED. The existing terminal synchronous audit deadline/cancellation limitation is unchanged.

## Source-bound hosted closure

Native GitHub observations confirm [bootstrap run218](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37154105088) and [package run128](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37154105078) completed successfully on exact pushed source cb38bb69483e3983a1ccb84734001915cdbadb6f. Linux and both Windows jobs passed their MCP steps. The final deliberate-failure control constrains substitution to SyntheticMcp.Tests and checks by reflection that the production assembly contains no substituted class. A nonauthor independently executed it and observed expected exit1; initial control artifacts remain qualified historical evidence. [Private publication receipt](../../docs/development/evidence/phase1d-minimization-cycle03-site-20261003.json) preserves native deployment, source/archive, audience and layout evidence. Full feature acceptance and cycle02 inputs remain open.
