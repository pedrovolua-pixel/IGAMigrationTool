# Internal synthetic evaluation reviewer eligibility v1

Status: Engineering freeze reviewed before code; local implementation awaiting nonauthor source review
Date: 2026-10-03
Authority: [exact policy approval](local-evaluation-policy-approval.md), [cycle03 plan](../../plans/active/local-pilot-m08-policy-implementation-cycle03.md), [reviewer policy](local-evaluation-reviewer-contract-proposal.md), [test design](local-evaluation-reviewer-test-plan.md).

This dependency-free internal `SyntheticEvaluation` value policy consumes supplied trusted fixture authority. It is not authentication, a customer permission store, a transaction, queue cancellation, history store, source resolver, audit sink or retention implementation. It cannot write outcomes, grant real authority, alter selected members or qualify a real person. Actual human qualifications/assignments, current real policy, RT14–16 transaction/queue/history enforcement and RT20 audit remain later NOT VERIFIED. RT11–13/17–18 arithmetic/frozen projections reuse accuracy-v1 and composed checks; this packet does not duplicate them.

## Exact API

Closed enums:

- `EvaluationReviewerRole`: Consultant, QualifiedCustomerReviewer, CustomerEvidenceAuthorizer, CustomerRiskOwner, Executive, Auditor, PlatformSupport, Mcp, ShareViewer.
- `EvaluationReviewerAction`: ScoredReview, PresentationCorrection, RelatedHistory.
- `EvaluationFixtureState`: Active, Revoked, Suspended, Deleted, Expired.
- `EvaluationConflictAnswer`: No, Yes, Unknown.
- `EvaluationReviewerIssue`: InvalidInput, InvalidReference, InvalidVersion, InvalidRegistry, DuplicateEntry, UnknownIdentity, UnknownAssignment, UnknownMember, StaleVersion, IdentityDenied, AssignmentDenied, ScopeDenied, RoleActionDenied, CategoryDenied, ResourceDenied, CustomerPolicyDenied, LifecycleDenied, QualificationDenied, ConflictDenied, HistoryDenied.

Positional immutable records (property order shown):

```csharp
EvaluationReviewerScope(string CustomerId, string ProjectId, string EnvironmentId,
    string AssessmentId, string EvaluationId);
EvaluationFixtureIdentity(string Id, long Revision, bool Authenticated,
    EvaluationFixtureState State);
EvaluationFixtureAssignment(string Id, long Revision, string IdentityId,
    EvaluationReviewerRole Role, EvaluationReviewerScope Scope,
    DateTimeOffset ValidFromUtc, DateTimeOffset ValidUntilUtc, EvaluationFixtureState State,
    bool Qualified, string? QualificationBasisId, string AssignmentAuthorityId,
    bool ScoredReviewGranted, bool PresentationCorrectionGranted, bool RelatedHistoryGranted,
    IReadOnlyList<string> ReviewCategoryIds,
    IReadOnlyList<string> HistoryCategoryIds,
    bool CustomerPolicyPermits, bool LifecyclePermits);
EvaluationFixtureMember(string Id, long Revision, EvaluationReviewerScope Scope,
    string CategoryId, EvaluationFixtureState State,
    bool ResourcePermits, bool CustomerPolicyPermits, bool LifecyclePermits,
    bool AuthorizedContextSufficient);
EvaluationConflictClearance(string ReasonId, string AuthorityId,
    bool OwnerApproved, bool SmeReviewed, bool SecurityReviewed);
EvaluationFixtureConflict(string AssignmentId, string MemberId, long Revision,
    EvaluationConflictAnswer FindingOrAnswerOrCorrectionAuthor,
    EvaluationConflictAnswer RuleOrCatalogContributor,
    EvaluationConflictAnswer AssessmentOrSourceContributor,
    EvaluationConflictAnswer ModelPromptOrProviderContributor,
    EvaluationConflictAnswer CustomerConclusionOrRemediationResponsible,
    EvaluationConflictAnswer OtherMaterialContributor,
    EvaluationConflictAnswer GeneralRelationship, EvaluationConflictClearance? Clearance);
EvaluationReviewerRegistryInput(string VersionId, string PolicyVersionId,
    string InstructionVersionId, IReadOnlyList<EvaluationFixtureIdentity> Identities,
    IReadOnlyList<EvaluationFixtureAssignment> Assignments,
    IReadOnlyList<EvaluationFixtureMember> Members,
    IReadOnlyList<EvaluationFixtureConflict> Conflicts);
EvaluationReviewerRequest(string IdentityId, string AssignmentId, string MemberId,
    EvaluationReviewerScope Scope, EvaluationReviewerAction Action, DateTimeOffset AtUtc,
    string ExpectedRegistryVersionId, long ExpectedIdentityRevision,
    long ExpectedAssignmentRevision, long ExpectedMemberRevision,
    long? ExpectedConflictRevision);
EvaluationReviewerCaptureResult(EvaluationReviewerIssue? Issue,
    EvaluationReviewerRegistry? Registry); // HasRegistry iff success
EvaluationReviewerDecision(EvaluationReviewerIssue? Issue,
    bool? AuthorizedContextSufficient); // IsAuthorized iff Issue == null
```

`EvaluationReviewerPolicy.Capture(EvaluationReviewerRegistryInput? input)` produces the validated detached sealed registry. Its public properties mirror input; constructor internal. `Decide(EvaluationReviewerRegistry? registry, EvaluationReviewerRequest? request)` checks the current supplied registry on every call; no stored decisions/caches/global authority. A denied result has only a closed issue and null context, never supplied identifiers, qualification, records or payload. Successful eligibility contains only the nullable context indicator: a boolean for ScoredReview, null for other actions. It does not record Confirmed, Rejected, Indeterminate or Unreviewed.

## Capture admission

All references including versions, authority/basis/reason IDs use accuracy-v1 ASCII `synthetic-` plus1–118 `[a-z0-9._-]`, maximum128. No normalization, URLs, free text or protected payload. Revisions positive. Each registry collection maximum100000; each nested category list maximum100000, with a maximum100000 category references across the registry. Category IDs are opaque supplied synthetic references, not a newly defined permission taxonomy. Null records/collections/entries, duplicate identity/assignment/member IDs or assignment/member conflict pairs, duplicate categories, undefined enums and invalid references deny. Validate every record even when unused. Assignment identity and conflict assignment/member references must exist; conflict assignment/member scope must match. All five scope IDs valid. Time offsets zero, end strictly after start; validity interval `[from,until)`. Capture clones each record/scope/clearance and deeply copies each collection into read-only views. Qualified requires a basis reference; unqualified forbids it. Optional clearance, when present, requires valid references even without a relationship; it never overrides material involvement. Empty registry is valid. Concurrent mutation of caller-owned inputs while Capture executes is outside this pure value API; caller must provide a stable input.

## Decision order and action bounds

Validate request refs/scope/enums/positive revisions/UTC and positive optional conflict revision. Expected registry version must exactly match captured version; identity/assignment/member revisions must exactly match trusted records before substantive policy. Identity must exist, authenticated and Active. Assignment must exist, belong to identity, be Active and currently valid; member must exist. Resolved assignment/member/request scope must all match exactly. Wrong scope/IDs never yields substantive content. Flags represent the six approved current-fixture policy conditions, not caller authority.

Member Active with ResourcePermits; assignment/member CustomerPolicyPermits and LifecyclePermits required for every action. Category authorization evaluated per action. ScoredReview and PresentationCorrection allow only Consultant or QualifiedCustomerReviewer with their separate action grant and matching ReviewCategoryIds. Both roles require explicit PresentationCorrectionGranted for that action; this flag represents the existing Consultant allowance/customer separate grant, not a new permission. ScoredReview additionally requires Qualified and a complete matching conflict declaration with exact ExpectedConflictRevision. Missing/Unknown material/relationship answers deny; any material Yes denies regardless of clearance. GeneralRelationship Yes requires reasoned clearance with OwnerApproved, SmeReviewed and SecurityReviewed all true; GeneralRelationship No needs no clearance. Scored review authorized with insufficient permitted context succeeds with context=false, allowing a separately authorized consumer to classify Indeterminate; denial never becomes Indeterminate. Presentation correction can supply context even when the actor is a material contributor; it cannot create independent scored review.

RelatedHistory requires its own current RelatedHistoryGranted, matching HistoryCategoryIds and Consultant, QualifiedCustomerReviewer or Auditor role. Qualification or past review/conflict clearance does not grant history. No conflict declaration required for history/presentation; if ExpectedConflictRevision is supplied, it must match an existing current declaration (stale supplied metadata always denies). No operation provides raw evidence, security audit, risk acceptance, tasks, publishing, protected resolver authority or broader category access. Lifecycle flags deny restricted actions without implementing actual retention/hold semantics. Private indexes over validated detached identity/assignment/member IDs and conflict pairs plus per-assignment category membership provide bounded constant-time lookups per decision; no mutable map is exposed. No assignment/no eligible reviewer produces policy denial; a separately authorized coordinator retains the same selected member and records Unreviewed through its own path.

Policy checks order: input admission; registry version; known records; revision checks; identity; assignment ownership/state/time; scope; resource; customer policy; lifecycle; role/action grant; action category; history grant; qualification; conflict. Typed failures are stable internal categories, not a public error contract. Identity state/authentication always denies before any resource-context projection.

## Independent expected cases frozen before implementation

Expected values are literal decisions, not generated by this policy. Base fixture: UTC2026-10-03T12:00, validity2026-10-01→2026-11-01, all revisions1, exact synthetic scope, authenticated Active Consultant, Qualified with basis, all three grants, synthetic-category in review/history category-ID lists, member Active/all flags true/context true, all seven conflicts No, no clearance. Base expected authorized score/context=true, presentation/history/context=null. QualifiedCustomerReviewer same; Auditor only history. Every other listed role denied for scored review.

| Case | Independent expected |
| --- | --- |
| RT01/04 role/action table | Consultant/customer: three authorized grants; Auditor history only; six other roles all denied |
| RT02 stale/fake | unknown identity/assignment/member respective Unknown issue; altered registry or any expected revision StaleVersion; mismatched identity/assignment AssignmentDenied |
| RT03 current six conditions | unauthenticated/inactive identity IdentityDenied; inactive/expired/not-yet-valid assignment AssignmentDenied; any scope coordinate ScopeDenied; resource/state ResourceDenied; either policy false CustomerPolicyDenied; either lifecycle false LifecycleDenied; action grant false RoleActionDenied (history HistoryDenied); denied category CategoryDenied |
| RT05 unknown/tie | absent declaration, any Unknown or missing required clearance ConflictDenied; general Yes with three approvals true authorized; each omitted approval ConflictDenied |
| RT06 material independence | each of six material answers Yes independently ConflictDenied, with/without full clearance; same actor presentation authorized within separate grant |
| RT07 reassignment | conflicted assignment score denied, independent assignment score allowed for same member; registry snapshots do not mutate supplied sample |
| RT08 no reviewer | absent assignment UnknownAssignment; no generated outcome or replacement |
| RT10 context | authorized context=false eligibility allowed; denied actor context=null |
| RT14/15 bounded revocation/history | recapture changed version/revoked identity or assignment: old expected version StaleVersion, current request denied; prior snapshot stable; former scored authority without history grant HistoryDenied; independent scoped current history authorized |
| Admission | null/oversize/duplicates/cross-reference/invalid enums/UTC/refs/revisions/category duplicates/qualification inconsistencies denied;128 reference accepted129 denied; end boundary denied start accepted |
| Detachment | mutate every original list and category array after capture: decisions/registry retain frozen content; output collection mutation throws; no successful projection in denial |

Full assignment workflow, resolved disagreements, persistent correction history, transaction recheck, queued cancellation, real source redaction/category filtering, UI/API and audit lifecycle are NOT VERIFIED by these eligibility tests. No migration/configuration/dependency/runtime host or actual human authority changes.
