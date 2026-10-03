# Local evaluation reviewer procedure — decision proposal

Status: PROPOSED — exact owner decision pending; no reviewer authority or implementation granted
Owner: Product/evaluation owner, with One Identity SME and security-owner review
Date: 2026-10-03
Packet: R02, Milestone08 cycle02

## Basis and boundary

This packet recommends EV02-R01–03 for the unresolved reviewer eligibility/conflict procedure in the [approved evaluation plan](evaluation-plan.md#current-blockers). It advances FR-HAS-51–53, AC-HAS-12/19 and bounded TP-HAS-012/019; FR-HAS-11/13/15/50 constrain review and history. The [cycle02 plan](../../plans/active/local-pilot-m08-evaluation-policy-cycle02.md) authorizes decision preparation only. The [test proposal](local-evaluation-reviewer-test-plan.md) supplies exact positive and negative cases.

Existing [authorization](../../docs/security/health-assessment-authorization-matrix.md), [identity/session](../../docs/security/health-assessment-identity-session-design.md) and [audit](../../docs/security/health-assessment-audit-policy.md) policies remain authoritative. EV02-R01–03 add no role, grant, raw-evidence resolver, customer risk authority, retention rule, public API or storage design. Accepted [ADR-0001](../../architecture/decisions/ADR-0001-pilot-application-shape.md), [ADR-0002](../../architecture/decisions/ADR-0002-pilot-tenant-isolation.md) and [ADR-0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md) remain unchanged. Architecture departures require a separate proposed ADR.

The [approved Phase1B allocation](local-phase1b-approval.md) leaves collaboration beyond existing Confirm/Reject/Defer/comment/presentation to Phase1C. This proposal does not implement that collaboration, reopen Phase1B work, or imply findings review equals evaluation review. Evaluation accuracy records do not mutate finding dispositions, scores, customer outcome approval, reports or task/artifact reviews.

## EV02-R01 — scoped eligibility and trusted assignment

**Recommended decision:** require an explicitly assigned qualified human, scoped to the exact evaluation and evidence categories, for each scored finding review. Retain the existing Consultant and qualified customer reviewer roles and their action bounds. Evaluation ownership, coarse Entra role, repository access, provider employment, platform support, customer evidence authorization or customer risk ownership alone does not qualify a reviewer.

An assignment records the opaque named identity, existing role, exact customer/project/environment/assessment/evaluation scope, authorized review action and evidence categories, effective period, qualification basis, assignment authority, policy/assignment version and applicable conflict decision. Qualification evidence states competence in the applicable One Identity build/module/category and understanding of the review instructions; the product/evaluation owner records the assignment with SME/security oversight. This record supports qualification; it cannot grant customer access. The existing customer assignment and policy mechanisms must independently authorize every access and action.

Before substantive review, verify all six authorization-model conditions server-side against current trusted policy: active authenticated identity, assignment scope, allowed role/action, resource state/category, customer data policy, and absence of lifecycle/capability denial. Finding visibility does not grant evidence visibility. Reject submitted identity, qualification, role or category declarations as authority. Both Consultant and qualified customer reviewer can confirm/reject within scope; customer reviewers may edit presentation/context only when separately granted. Neither gains task/package creation, publication, configuration, raw evidence or residual-risk acceptance from this procedure. Risk acceptance requires a separately assigned customer risk role and its existing independent conditions; EV02-R01 adds none.

Use a trusted, versioned synthetic fixture registry for a later local slice. It supplies explicit named fixture identities, qualifications, role/action assignments, customer/project/environment/assessment/evaluation scopes, categories, validity/revocation state and conflict declarations. The review input refers to a registry entry; its own claims cannot alter the registry or establish eligibility. Fixture-only evidence/source IDs and CustomerApproved outcome declarations remain synthetic. A passing test proves behavior against supplied fixture authority, never actual human qualification, identity proof, customer approval, real policy enforcement or acceptance.

Before actual human evidence use, the product/evaluation owner must record named assignments and qualification/conflict decisions with SME/security oversight; authorized customer assignments, permissions and category policy must be evidenced. Real identity mapping, revocation, policy/evidence access and instructions must be verified against those exact people and environments. No fictional identity, solo owner acting as two people or agent review satisfies independent human review. These operational proofs are separate later prerequisites and remain NOT VERIFIED.

## EV02-R02 — conflicts, independent review and missing reviewers

**Recommended decision:** an author or materially involved contributor cannot supply the independent scored review of their own affected output. Require a conflict declaration before assignment and refresh it when relevant facts or locked artifact versions change. The declaration records explicit yes/no/unknown and scope for:

- authorship or editing of the finding, correction or expected evaluation answer;
- rule/catalog development, assessment configuration/execution or source evidence preparation;
- prompt/model selection, tuning, candidate development or provider involvement;
- customer employment, consulting engagement, ownership of the assessed configuration or remediation, and commercial/personal ties;
- any other interest that could influence the conclusion.

Material authorship/involvement in the evaluated output requires abstention from its independent scored review. Provider involvement affecting the tested model/prompt or customer responsibility for the reviewed conclusion has the same consequence. A general customer or consulting relationship is disclosed rather than automatically disqualifying every qualified customer reviewer or Consultant: the owner, with SME/security oversight, records a reasoned assignment decision that preserves nonauthor independence for the exact output. An unresolved/unknown conflict, missing declaration or missing reasoned decision prevents assignment. Do not infer clearance from silence, reviewer availability or an existing role.

The author may supply authorized factual context, identify limitations or propose a correction; that contribution is attributed and cannot replace the independent review. No blanket owner override makes a conflicted review independent. If a conflict emerges before the cutoff, abstain and reassign the same selected finding to a different eligible, authorized, nonauthor reviewer. Preserve the former assignment and any contributions as history. The reassigned reviewer independently assesses the originating conclusion from the frozen source; a prior answer is not authority. A discovered conflict after freezing is recorded as a limitation and addressed by a new evaluation version rather than rewriting the old result.

If no eligible independent reviewer is available by the cutoff, keep the selected finding, record a structured unavailable-reviewer reason and `unreviewed`; count it separately and exclude it from accuracy. Do not substitute an easier, already-reviewed or different item. Do not relabel missing review `indeterminate` or `confirmed`, or manufacture fixture eligibility to close a live dependency. The original sample remains frozen; whether count/stratum and other acceptance gates pass is evaluated separately under the approved plan and exact sampling decision.

The recommended local scope uses one independently assigned scored reviewer per item and preserves all contributions. It does not introduce majority vote or an automatic tie-breaker. Divergent substantive human conclusions require the owner to obtain an independently qualified reassessment and record the disagreement; when no resolved, supported conclusion is available at the cutoff, preserve `unreviewed` with an unresolved-review reason. Approving this fallback is part of EV02-R02, not a claim that existing specs already select it.

## EV02-R03 — outcomes, correction history and revocation

**Recommended decision:** evaluate the originating conclusion against the exact frozen source/context and instruction versions; retain immutable attribution and correction history. Review records bind evaluation/sample/member, reviewer assignment/declaration versions, originating finding/rule/model/prompt/source locks, review time, rationale, authorized evidence references and each correction dimension. Rationale and review content belong only in authorized derived records; the security audit remains payload-free under its approved lifecycle. No new retention period follows.

Apply the approved outcome semantics:

| Condition | Recorded outcome and effect |
| --- | --- |
| Authorized qualified reviewer supports the useful material original conclusion | `confirmed`; one confirmed count |
| Authorized qualified reviewer finds false/unsupported/materially misleading original or non-supporting citation | `rejected`; reason and evidence references; one rejected count |
| Authorized qualified reviewer can review the finding, but authorized context is insufficient for a qualified decision | `indeterminate`; explicit context limitation; no denominator contribution |
| No required authorized independent review occurred | `unreviewed`; reason, no denominator contribution |
| Reviewer materially changes content | `corrected`, plus originating `confirmed` or `rejected` classification; exactly one originating denominator contribution and overlapping correction count |

A scope/category/identity denial produces no substantive review write, no outcome write by the denied actor and no protected content in the response. An authorized coordinator can retain an unavailable-item reason through its own permitted path; it cannot impersonate the denied reviewer. Authorized access to a redacted finding with insufficient permitted source context can yield `indeterminate`; denial of review itself cannot. Preserve authorized redaction markers without disclosing hidden values or granting a broader resolver. Protected raw evidence remains under its dedicated permission and customer authorization, outside this packet.

Record severity, category, root cause and recommendation corrections separately. Severity disagreement alone does not automatically reject the finding. A corrected unsupported original stays an originating rejection even if the corrected text is useful; a supported original with presentation/context correction stays originating confirmed. A correction cannot add another denominator member, change the frozen selected population, auto-tune a rule/model or turn risk acceptance into confirmation. General AI and customer-approved desired-outcome quality remain separate tracks; draft/inferred/consultant-reviewed desired outcomes never enter scored adherence accuracy.

Freeze the outcome events selected at the stated correction cutoff and all locked versions. Later outcomes/corrections, reassignment, conflict discoveries or changed source evidence require a distinct evaluation version with new locks/cutoff/digest and a link to the prior record. Preserve prior bytes, counts, rationale attribution and disclosed limitations. A new result does not relabel a failed historical acceptance gate. Historical source expiry or removal is shown as unavailable under FR-HAS-50; history is not proof that deleted evidence is still available.

Recheck active authorization at the next request and before queued review work begins or commits. Revoked/suspended identities or assignments cannot read, append review/correction, reuse a cached decision, or run unstarted work authorized solely by the revoked assignment. A stale revision cannot overwrite another review. Trusted versions must be rechecked atomically with the later integration's commit boundary; exact transaction/API design is an implementation decision after approval.

Revocation prevents new access; it does not erase historical authorized events or grant anyone access to them. Historical readers need their current independently valid scoped history permission and current category/customer/lifecycle policy. A former reviewer has no continuing right by authorship. Consultants/customer reviewers see only authorized project/related review history; auditors see only their approved scope; audit access never grants evidence access. Normalized or protected evidence behind historical references is separately authorized on retrieval; retain only permitted metadata/redaction or unavailability markers when values are denied. No protected historical payload becomes public because the evaluation is frozen.

## Alternatives and requested decisions

| Decision | Recommended choice | Credible alternative and tradeoff |
| --- | --- | --- |
| EV02-R01 | Explicit qualified assignment and trusted fixtures; actual people/proofs later | Role-only self-declared eligibility is simpler but fails scope/category and qualification evidence. A new reviewer role would require separate authority design and is unnecessary here. |
| EV02-R02 | Nonauthor review; reasoned disclosed general ties; abstain/reassign same item; unresolved review remains unreviewed | Require no customer/consulting ties at all: stronger organizational separation but may exclude the approved customer-reviewer role and needs external access/qualification. Allow material author self-review: easier staffing, defeats independence. Require two reviewers and adjudicator: stronger disagreement handling, greater capacity and separately specified authority/process. |
| EV02-R03 | Append attributed corrections, preserve original classification, new frozen version, current-policy history reads | Mutate the old result or give former reviewers perpetual history access: simpler workflow but contradicts frozen records/revocation. Store copied raw evidence with each review: aids reproduction but conflicts with minimization and separate evidence permissions/lifecycle. |

Owner approval must name EV02-R01–03 and their exact reviewed bytes; SME/security review must cover the qualification, material-conflict and current-policy history boundaries. Actual reviewer names, customer grants, qualification proof, conflict records, independent staffing and live enforcement evidence are not decided by this document and remain later human dependencies. Local synthetic implementation may start only after exact policy approval plus an approved bounded implementation/test plan. Any alternative changing accepted architecture, authorization or retention needs amended specifications and, where applicable, an ADR before implementation.

The existing explicit pilot-owner exception can waive named **internal artifact-promotion** checks for a limited trial while preserving failed/unverified/regressed results. It cannot deem a conflicted author independent, manufacture a reviewed outcome, change the dataset, relax customer authorization, relabel the >80% acceptance gate, satisfy G8/G9 or authorize production. This reviewer proposal creates no additional waiver path.

No code, package, configuration, schema, migration, public contract or deployment is changed. Documentary review and source checks can verify this preparation; all proposed behavioral, identity, persistence, UI/API, live and acceptance tests remain NOT VERIFIED until executed. Full Milestone08, Phase1C/UAT and G1–G9 remain open.

## Approval

Requested: attributable product/evaluation-owner decision on EV02-R01–03, with SME/security review.
Approved by: PENDING
Date: PENDING
