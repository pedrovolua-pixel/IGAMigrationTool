# Milestone08 cycle02 — evaluation policy decision packet

Status: PROPOSED — owner approval pending; dependent behavior not implemented
Date: 2026-10-03
Requested from: repository owner acting as product/evaluation owner, with technical, One Identity SME and security review

## Decision requested

Approve or amend **EV02-S01–S04 and EV02-R01–R03** against the exact four document digests below for bounded local synthetic implementation. The repository owner's “Next cycle” authorizes preparation; it does not settle these previously unspecified evaluation choices. Agent review is documentary evidence, not human approval. An approval must be attributable and identify this packet/revision; record it in a separate approval receipt before dependent implementation.

| Decision | Recommended choice and practical effect |
| --- | --- |
| EV02-S01 — sample size | Review every Critical/High finding. Below100 total, review all. Otherwise reach100 total and cover every lower stratum; when Critical/High alone reaches100, add up to100 lower reviews and expand for all strata. Deny a hard budget below the required sample. Example:120 mandatory plus130 lower findings across four lower strata yields220 selected reviews. |
| EV02-S02 — deterministic selection | Reserve one lower finding per nonempty environment/module/category/severity stratum, allocate remaining slots proportionally to remaining capacity with exact Hamilton remainders, then seeded SHA256 ranking. Freeze seed, complete population, tuples, allocation and selected members. Missing primary classification denies selection; unavailable selected findings stay selected. Local population cap100000 and exact synthetic identifier/rank bytes are part of this choice. |
| EV02-S03 — warning | Warn for every classified denominator below30, including zero. Keep accuracy arithmetic and the approved strict >80% general threshold unchanged. Desired-outcome accuracy remains separate. |
| EV02-S04 — regression | Both relevant classified denominators must reach30 for comparative Critical/High decline and rejection growth. Decline >5 points and rejection count growth >10% block; zero baseline plus any rejection blocks. Overall≤80%, safety and required coverage remain independent blocks. Missing comparison evidence stays NOT VERIFIED. Preserve baseline cutoff and separately freeze candidate cutoff. |
| EV02-R01 — eligibility | Explicit qualified Consultant/customer-reviewer assignment for the exact scope, action and categories under existing permissions. Local tests use a trusted versioned fixture registry; submitted claims cannot grant authority. Actual people, qualifications and customer access remain later evidence. |
| EV02-R02 — independence | Material authors/contributors abstain from their affected output. Disclose general ties and obtain reasoned assignment clearance. Reassign the same selected item; absent independent or unresolved substantive review stays Unreviewed. No automatic majority/tie-breaker or owner waiver manufactures independence. |
| EV02-R03 — corrections/history | Retain attributed originating Confirmed/Rejected classification and count once when corrected. Freeze each result/cutoff; later changes produce a new version. Recheck current permissions/revocation for new access or writes; history does not grant evidence access. |

The linked proposals contain the exact formula, byte ordering, qualification/conflict boundaries, alternatives, increased-workload tradeoffs and test assertions. This summary cannot override those documents. The30-review warning is a transparent heuristic, not a statistical confidence guarantee. No desired-outcome >80% acceptance threshold is proposed.

## Exact review set

| Document | SHA256 of UTF8 file bytes |
| --- | --- |
| [local-evaluation-sampling-contract-proposal.md](local-evaluation-sampling-contract-proposal.md) | `fbcc7125f80a411f4c964dfb10afc84d9829f725113404ed7455edc33ec87d35` |
| [local-evaluation-sampling-test-plan.md](local-evaluation-sampling-test-plan.md) | `ac6ce96f4bfc7ff9b5d0046fbd85a1e307f1f40282b962d954f1273acf0b2487` |
| [local-evaluation-reviewer-contract-proposal.md](local-evaluation-reviewer-contract-proposal.md) | `15c0189e921bf76248360eb5aa5d0cc06bd1ae7c6fdd1a9ba3add266d6019494` |
| [local-evaluation-reviewer-test-plan.md](local-evaluation-reviewer-test-plan.md) | `0afcbcd8143e221c1a9028beb89eb5c6c14a93c288a4d85d5e2ea5cda6771363` |

Independent reviewer reports and executed documentary checks are in [cycle02 evidence](../../docs/development/evidence/m08-evaluation-policy-cycle02-20261003.json); the [implementation preparation plan](../../plans/active/local-pilot-m08-evaluation-policy-cycle02.md) records ownership, findings and next steps. The [approved evaluation plan](evaluation-plan.md), [accuracy foundation](synthetic-evaluation-accuracy-contract.md), accepted ADRs and existing authorization/audit policies remain authoritative.

## Scope after approval

Approval settles evaluation semantics and the paired bounded test designs. The next coordinator cycle must freeze a small engineering implementation contract and plan consistent with these choices, then build independent synthetic sampling/regression checks and a trusted-fixture reviewer-policy foundation in separately scoped steps. Routine internal API, canonical JSON byte grammar and fixture engineering may be resolved and independently reviewed without another product decision if they preserve the approved choices. Material scope, authority or architecture changes return for approval.

No live provider/source, real reviewer assignment, role grant, storage migration, public endpoint, customer authorization, retention change, rule/model activation, production release or pilot acceptance follows. There is no claim of server authorization enforcement from pure local fixtures. Operational UI/API/queue/persistence integrations require their full approved tests and separate evidence. Phase1B approved implementation remains with its coordinator; residual FR-HAS-12/15 finding collaboration assigned to Phase1C is a separate future contract. Risk acceptance, recurrence/reassessment and reports remain open.

All40 proposed behavioral cases are NOT EXECUTED. This cycle changes documents only. Full Milestone08, Phase1C/UAT and G1–G9 remain NOT VERIFIED. An owner internal-promotion exception cannot relabel acceptance, change the cohort or manufacture qualified independent review.

## Approval record

Decision: PENDING
Approved by/date/reviewed packet revision: PENDING
SME/technical/security review or stated limitations: PENDING
Alternatives or amendments: PENDING
