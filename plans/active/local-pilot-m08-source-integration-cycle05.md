# Milestone08 cycle05 — Phase1B evaluation source capture

Status: RUNNING — bounded local capture contract preparation; implementation requires coordinator engineering freeze
Owner: Phase1C coordinator
Date: 2026-10-03
Stable source: a6c3375a8ccdf562333d4b9487811c3cd8ec0f09
Authority: repository owner's “Move to next cycle”; cycle04's explicit next dependency is a concrete approved source/reviewer/evidence integration contract. Existing approved product, EV02 policies and ADR0001–0004 govern the proposal. Existing EV02 approval permits routine internal API/canonical JSON/fixture engineering to be frozen without another product decision. This cycle does not select unresolved product or security behavior.

## Bounded outcome and ownership

Implement a narrow read-only capture for actual saved synthetic Phase1B source after a reviewed exact contract and paired test matrix. Inventory the actual saved Phase1B source and required version/authority bindings before proposing engineering adapters. Preserve historical EV02/freeze/golden bytes, the independent120/100 fixture and all other owners' current work. Customer sources, actual reviewer assignments, OpenAI/provider execution, production identity, report publication and Phase1D remain outside this packet.

| Packet | Permitted paths / source | Owner | State |
| --- | --- | --- | --- |
| Source audit | Read-only committed Phase1B source at stable SHA; native audit only | cycle05_source_audit | VERIFIED documentary |
| Policy audit | Read-only committed approved evaluation/ADR records at stable SHA; native audit only | cycle05_policy_audit | RUNNING |
| Coordinator contracts/configuration | /private/tmp/iga-m08-cycle05-coordinator; cycle05 contracts/freeze/test/shared configuration and this plan | Coordinator | RUNNING |
| Independent capture review | Read-only exact draft/runtime commits; native review only | cycle05_policy_audit | REVIEW |
| Source implementation | NEW SyntheticEvaluationSourceIntegration module and focused tests, only after freeze | cycle05_source_audit | READY |
| Independent verification | Separate worktree, new independent consumer tests only, assigned after freeze | cycle05_independent | READY |
| Canonical integration/site | Owned additive records only; existing owner-private site | Coordinator | READY |

No worker writes shared source/configuration or another owner's paths. Dependent capture code waits for coordinator engineering freeze and nonauthor review. Source-backed sampling/review commands remain outside this cycle until their provenance/authority integration is settled. The approved parallel workflow permits these read-only independent audits and nonauthor review.

## Completion evidence

- [ ] Actual source/policy/architecture inventories identify valid source content and deferred integration bindings.
- [ ] Exact capture contract and paired tests trace approved requirements; coordinator freeze recorded before code.
- [ ] Capture implementation and nonauthor review complete; applicable formatting/build/type/unit/PostgreSQL/architecture/security/compatibility checks executed and retained.
- [ ] Canonical plan/status/evidence updated with exact pending dependency.
- [ ] Existing private site status/human board published and confirmed; native evidence retained and own temporary worktree cleaned after preservation.

Source-backed sampling/reviewer commits, actual source/reviewer security boundaries and actual end-to-end evaluation remain NOT VERIFIED. This preparation cannot close M08, Phase1C, TP-HAS-012/019 or G1–G9.

## Engineering execution trace

FR-HAS-11/13/50–53, bounded AC-HAS-12/19 and TP-HAS-012/019 trace to EV05-S001–S014 in the paired source-capture test plan. Build only the read-only internal capture and focused author/independent consumers, after the exact engineering freeze. Reuse public owning APIs under one supplied guarded transaction with registry→run ordering. Two actual AI finding groups and terminal gaps retain native identities/provenance; no sampler aliases/23-binding substitutes, reviewer writes, HTTP/UI activation or new authority. New source/sample/reviewer integration is the later outcome, not an unfinished prerequisite to this capture.

Rollout is a library reference and explicit focused test invocation only. No existing host references or activates the adapter. Rollback removes the new consumer reference; no stored data rollback, destructive migration or new configuration default. Coordinator owns the solution and CI additions. No new package versions/dependencies are introduced. Author and independent workers own only their new assigned projects; frozen contract amendments require nonauthor review before implementation.
