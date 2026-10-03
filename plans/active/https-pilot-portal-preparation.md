# HTTPS pilot portal: active preparation

Status: PREPARATION — owner selects HTTPS now; exact ADR/local implementation approval pending
Date: 2026-10-03 UTC
Owner: Azure/BFF coordinator
Product/technical basis: approved feature003 and existing identity/session controls; [proposed technical/test addendum](../../docs/development/https-pilot-portal-proposal.md); [Proposed ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md)

## Owner direction and supersession

“Let’s move to the https instead of bastion” replaces the Bastion customer-browser path and the post-pilot scheduling preference. HTTPS preparation begins now; pilot completion is not claimed. The owner has not selected a concrete public architecture or approved a new paid/public session. Retain accepted ADR-0010/local templates and failed automatic quota evidence as historical optional work. Do not pursue the pending separate VM support request or re-request its approval while this direction stands. No VM/Bastion exists to remove.

## Conditional packets and ordering

These are proposal packets, not authorization to code through unresolved boundaries. The coordinator owns canonical/shared files and integration; writing workers receive isolated worktrees after exact path/contract approval. Use the approved [parallel workflow](../../docs/development/parallel-agent-workflow.md), one bounded platform worker and one non-author reviewer when work is independent.

| Packet | Owner / scope | Entry condition and completion evidence | State |
|---|---|---|---|
| HTTPS-P00 | Coordinator plus read-only architecture/review worker; ADR/proposal/diagram/current records | Current primary Microsoft research, exact proposal, documentary checks and non-author review; attributable owner direction | VERIFIED — preparation only |
| HTTPS-P01 | Platform writer: proposed `infra/bicep/modules/pilot-https-container-apps-environment.bicep`, `pilot-https-portal.bicep`, protected-input template and `tests/infrastructure/HttpsPortal/`; coordinator owns shared inventory | Exact ADR-0011/local technical/test approval before authoring; fixed separate external environment, disabled public app ingress/live sign-in, private backends and exact protected inputs; pinned compile/lint/format and negative/composition evidence | BLOCKED — exact local decision pending |
| HTTPS-P02 | Coordinator + separately scoped BFF implementation workers | Approved production provider/key/audit contracts and isolated packets; actual production composition, SQL role negatives, shared keys, provider observation/federation and durable audit-outage proof; reviewed image/private build/signing acceptance | BLOCKED — contracts/live prerequisites |
| HTTPS-P03 | Coordinator/platform/identity/operations, protected inputs only | Full fresh price/spend/capacity/what-if, fixed generated origin/proxy/private paths, users/role/CA/federation proof, exact public synthetic session and preservation/disposal authorization | NOT READY |
| HTTPS-P04 | Non-author verifier + coordinator, approved actual environment | HTTPS-T01–T08 actual evidence, affected configured checks/CI and safe closure; distinguish engineering results from G1–G9/human acceptance | NOT VERIFIED |
| HTTPS-P05 | Named human release/security/operations owners | Existing pilot/release gates and explicit production/customer-evidence release approval | NOT VERIFIED |

## Immediate human tasks

| Task | Requested role | Source / completion |
|---|---|---|
| Choose direct managed HTTPS topology for local preparation | Repository technical/security owner | [ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md): explicit Option A local disabled-template decision; no paid/public grant |
| Complete exact user admission | Identity/product owner | [Protected binding template](../../docs/development/bff-production-bindings-template.json), [approved role matrix](../../docs/security/health-assessment-authorization-matrix.md): immutable organizational/B2B proof, selected customer role/scope and CA/licensing; no email-only authority |
| Resolve production audit/provider/key authority | Technical/security/platform owners | [Existing production proposal](../../docs/development/bff-production-authority-hosting-proposal.md): real adapter contracts, durable audit-outage preservation, scoped key/SQL/provider evidence |
| Review exact Azure/public session | Repository/operations owner, after local preparation | [HTTPS proposal](../../docs/development/https-pilot-portal-proposal.md#cost-and-operations): current complete quote, full reserve under allowance, exact resources/ownership/public boundary and preservation/disposal |
| Restore private board publication | Repository owner | [Site maintenance](../../docs/development/pilot-status-site.md): separately rejected publisher credential/network action still needs its exact authorization and confirmed deployment |

Bsv2 support authorization, VM quota/image/disk/browser/RDP/desktop egress tasks are **SUPERSEDED for the active path**. Do not mark their technical results successful. Keep original evidence and publisher block separate. User grants, network exposure and production release remain disabled.

## Verification and completion limits

P00 has no runtime or IaC changes; applicable checks are source/link/JSON/SVG validity, diagram render/visual inspection, protected-identifier/secret scan, diff checks and independent scope review. P01–P04 require the full affected checks listed in the proposed test packet when authorized and executed. Planning cannot satisfy a runtime gate or repair historical CI failures.

Canonical feature implementation/status/evidence records are updated in this cycle. Private board publication remains unavailable: last confirmed BFF snapshot `4346e0f` predates these changes; preserve unrelated later pilot publications. Report the stale BFF/access summary, not a current site.

## Executed preparation checkpoint

The coordinator rendered and visually inspected the new diagram at3200×2000, validated source/link/JSON/SVG/heading targets and six unmodified official icon embeds, ran added-prose protected-identifier checks, whole-checkout Gitleaks and `git diff --check`. Non-author final review passed with no remaining material findings after adding ADR-0010's current-direction notice and the evidence record. [Sanitized evidence](../../docs/development/evidence/https-pilot-portal-preparation-20261003.json) binds exact changed sources and check results. No runtime/IaC change occurred; runtime/live cases and gates remain NOT VERIFIED. Exact local architecture approval remains the next dependency.
