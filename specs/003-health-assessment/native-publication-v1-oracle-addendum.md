# Native publication v1 independent oracle addendum

Status: FROZEN — coordinator reviewed d52189f; original vector checkpoint51272d2 precedes core hashing
Date: 2026-10-03
Baseline: integrated `b6c806e`, reviewed RR-P02 `4391d27d6760e3c457b9c9b61a6fcaf4527ab949`
Ownership: independent oracle author; this addendum and `tests/unit/ReportPublication.Tests/` only
Authority: approved pilot execution and pilot decision exception

This addendum supplies literal canonical envelope keys and nested shapes for the [reviewed native contract](native-publication-v1-contract.md) and [NPV test plan](native-publication-v1-test-plan.md). It changes no permission, retention duration, source eligibility, public route, session contract, host activation or accepted storage architecture. It fixes interpretation before independent original byte fixtures and production code exist. All eventual fixtures are fictional local source data; their native schema labels cannot establish a real completed assessment or pilot gate.

## Scalar and ordering freeze

Use `native-report-canonical-v1` exactly: UTF-8 without BOM or trailing newline, object keys sorted by ASCII/ordinal name, no insignificant whitespace, GUIDs lowercase hyphenated D, lowercase64 SHA-256, UTC timestamps with exactly seven fractional digits and Z. Counters/revisions/byte lengths are canonical signed64 decimal strings; counters permit zero, revisions/byte lengths are positive. Health/quality/confidence values are invariant decimal strings0..100 with no exponent, sign on zero, leading zero, or insignificant trailing fractional zero; at most28 fractional digits. JSON booleans and null retain their own types. No float conversion or rounding occurs in canonicalization. Reject malformed UTF-8, unpaired UTF-16 surrogates, duplicate keys/IDs, unknown fields/schema/enum, numeric instead of string counters, and noncanonical values.

Text is at most4096 Unicode scalar values, without normalization. Category/version/rule/confidence-band/provenance-kind tokens are nonempty ASCII `[A-Za-z0-9._-]` with at most128 characters. This grammar constrains representation; every meaning and classification still comes from its owning source. Canonical string escaping uses quote/backslash, standard short JSON control escapes, and lowercase `\\u00xx` for remaining controls; other valid Unicode scalars remain UTF-8. Reserved source facts such as rule IDs are source-owned tokens, not evidence locators or raw native identifiers.

Arrays declared sets are sorted as follows: category/field tokens ordinal; UUID ID collections by UUID D ordinal; dimensions/warnings/provenance by `(kind,id)` with warning `recordId` and provenance `opaqueRecordId`; redaction markers by `(section,id,field,reason)`. When an extra discriminator is present it sorts last ordinal. Duplicate set identities refuse, rather than being discarded; a conflicting digest for the same provenance `(kind,opaqueRecordId)` refuses. Display-record arrays and their source prose order are preserved; every display ID remains unique. Findings, root causes, recommendations, accepted risks, healthy controls, coverage items and protected references sort by their `id`. Linked UUID arrays sort ordinal and reject duplicates. Empty arrays are actual arrays, never null.

## Hash envelopes

`sourceDigest` is SHA-256 of the canonical UTF-8 object with exactly these12 fields:

```text
scope, runId, runRevision, assessmentState, inputs, projection, score,
warnings, retention, requiredCategories, requiredFields, provenance
```

`scope` is the four-field scope below. `runId` is UUID, `runRevision` a positive revision, `assessmentState` Completed/CompletedWithGaps. `projection` and `score` are complete closed objects below. All repeated source/run/input/scope/score/warning/category/field/provenance commitments must agree exactly. This envelope contains no observation time, actor, report UUID, sourceDigest or manifestDigest and has no additional outer schemaVersion; its inner projection/score are explicitly versioned. No prefix/domain string is prepended.

`commandDigest` is SHA-256 of the canonical object with exactly:

```text
actor, operationId, scope, runId, expectedRunRevision,
expectedSourceDigest, acknowledgedWarnings
```

Actor/scope are exact records below; UUIDs/revision/digest are canonical. AcknowledgedWarnings uses the exact warning set. Both invocationId and correlationId are excluded, as reviewed RR-P02 specifies. Neither field may accidentally enter through general record serialization. Changing actor/session/securityVersion, operation, scope, run, expected revision/digest or acknowledgment changes the command commitment. No prefix/newline or extra schemaVersion occurs.

Projection, score and manifest digests hash their entire respective canonical objects. A manifest contains no manifestDigest. Audit eventDigest hashes the entire closed audit object including previousEventDigest, with no eventDigest field. Publication/read receipt bytes have no self-digest. Read-request digest hashes the reviewed six-field `report-exact-read-request-v1` object; correlation is excluded. These distinct commitments never substitute for each other.

## Closed reusable records

| Name | Exact fields/types |
|---|---|
| Four-field scope | `customerId`, `projectId`, `environmentId`, `assessmentId`: UUID. Manifest/projection `scope` has only the first three; its sibling `assessmentId` must equal the source/request four-field assessment. |
| Actor | `tenantId`, `objectId`, `sessionId`: UUID; `securityVersion`: positive revision. These are verified server bindings; no caller record grants authority. |
| Metric | `availability`: Available/Unavailable; `value`: canonical decimal-string0..100 or null; `reason`: reviewed availability reason. Available requires nonnull value/None; Unavailable requires null/other reason. |
| Score | `schemaVersion`: health-report-score-v1; `provisional`, `publishable`, `quality`: Metric. Projection `scores` is this exact four-field score; source `score` is identical. Quality remains the owning quality result, never a health or coverage ratio inferred by the publisher. |
| Retention | `policyId`: owning policy UUID; `policyVersion`: source-owned token; `class`: PublishedArtifact; `clockStartUtc`, `expiresAtUtc`: UTC; `holdReference`: UUID or null. Clock start is no later than expiry. These literal names represent only RR-P02's already required owning-policy UUID/version, class, supplied clock/expiry and optional hold reference. They confer no retention/hold authority, choose no duration, and do not prove a hold exists. Only the trusted lifecycle/source adapter can resolve current policy/hold state. |
| Provenance | `kind`: source-owned token; `opaqueRecordId`: UUID; `digest`: SHA-256. No URI/path/native object name. |
| Warning | `kind`: MandatoryReviewIncomplete/CoverageIncomplete/SourceLimitation; `recordId`: UUID; `category`: token. Exact tuple identity, source-owned occurrence and acknowledgment. |
| Redaction marker | `section`: exact requiredFields token; `id`: UUID; `field`: bounded source-owned field token; `reason`: Redacted/Expired/Deleted/Inaccessible/Unsupported/InsufficientEvidence/Excluded/NotAssessed/Error. No original value. |
| Display record | `id`: UUID; `category`: token; `title`: text; `text`: text or null; `availability`, `reason`: reviewed availability pair. Available requires nonnull text/None; Unavailable requires null text/other reason. Title is minimized section metadata, not the unavailable original. |
| Priority/effort | `availability`, `value`: classified display text or null, `reason`, `sourceReference`: UUID or null. Available requires value/reference/None and reference present in provenance; Unavailable requires both null and other reason. No invented taxonomy or missing estimate. |

Availability reasons are exactly None/NotApplicable/NotAssessed/InsufficientEvidence/Excluded/Inaccessible/Redacted/Unsupported/Error/Expired/Deleted. General availability is Available/Unavailable. Protected-reference frozen availability additionally permits Redacted paired with Redacted. In the source projection no missing/unknown reason is mapped to a convenient enum.

## Closed owning inputs

The input object contains exactly:

```text
baselineId, baselineDigest, capabilityLockDigest,
ruleCatalogVersion, ruleCatalogDigest,
scoringProfileVersion, scoringProfileDigest,
maturityProfileVersion, maturityProfileDigest,
desiredOutcomeVersion, desiredOutcomeDigest,
scoringAlgorithmVersion, maturityAlgorithmVersion, applicationVersion,
reviewSnapshotDigest, coverageSnapshotDigest, runInputDigest,
aiPolicyVersion, modelVersion, promptVersion
```

baselineId is UUID. Every `*Digest` is SHA-256; desiredOutcomeDigest may be null only with null desiredOutcomeVersion. All version fields are source-owned tokens, except modelVersion/promptVersion and the paired desiredOutcomeVersion may be null. Other versions/digests are required. Scoring/maturity algorithm tokens and computations must be the owning approved versions; the oracle does not promote a catalog/provider/model. Input object repeats exactly in source, projection and manifest. Provenance references bind real records only in the future actual adapter, not from syntactic validity.

## Closed projection sections

The outer projection fields are exactly the RR-P0221 fields: schemaVersion, scope, assessmentId, runId, runRevision, inputs, executiveSummary, environmentScope, scores, maturity, dimensions, coverage, findings, rootCauses, healthyControls, recommendations, acceptedRisks, warnings, methodology, technicalAppendices, redactionMarkers. schemaVersion is health-report-projection-v1. Scope/run/input bindings equal the source envelope.

| Section | Exact nested shape |
|---|---|
| executiveSummary/environmentScope/methodology | Ordered arrays of Display record. Missing required section is not an empty successful result: include its explicit unavailable Display record where appropriate. |
| maturity | `availability`, `level`, `algorithmVersion`, `inputDigest`, `contentDigest`, `reason`. Level Initial/Developing/Defined/Managed/Optimized or null; availability/reason rules apply. Algorithm and digests are source-owned commitments even when level unavailable; Available requires nonnull level, Unavailable null. |
| dimensions | Array of `{kind,id,category,provisional,publishable}`. kind Category/ObjectType/Module/DesiredOutcome; id UUID; category token; both metrics exact Metric. |
| coverage | Object with exactly `items`, `counts`. Items array of `{id,category,state,reason}`: id UUID/category token, state Pass/Finding/NotApplicable/NotAssessed/InsufficientEvidence/Excluded/Inaccessible/Redacted/Unsupported/Error; reason the same source-owned availability-reason enum (None for Pass/Finding). Counts exact `{planned,executed,gap,notApplicable}` canonical nonnegative strings. Planned equals item count and executed+gap+notApplicable; Pass/Finding are executed, NotApplicable is notApplicable, remaining states gap. Do not derive score/quality from this arithmetic. |
| findings | Array of `{id,category,title,summary,severity,state,method,confidencePercent,confidenceBand,confidenceAvailability,confidenceReason,mandatoryReview,referenceIds,rootCauseIds,originalDigest,reviewRevision}`. ID UUID; title/summary minimized text; source-reviewed enums exactly RR-P02; confidence percent decimal-string0..100 or null/band token or null with exact available rules; mandatoryReview boolean; link arrays UUID; originalDigest SHA-256; reviewRevision nonnegative counter (initial review revision may be0). |
| rootCauses | Array of `{id,category,summary,findingIds,availability,reason}`. Summary text/null; exact availability; linked UUIDs agree bidirectionally with findings, including explicit unavailable cause. |
| healthyControls | Array of `{id,category,ruleId,ruleVersion,summary}`. id UUID; category/rule/version tokens; minimized summary text. No omitted control is inferred healthy. |
| recommendations | Array of `{id,findingId,category,summary,priority,effort,reviewState}`. IDs UUID, same-projection finding; category token/summary text; priority/effort exact records above; reviewState Unverified/Reviewed. |
| acceptedRisks | Array of `{id,findingId,category,decisionReference,reviewAtUtc,status}`. IDs/references UUID; linked finding; category token; UTC; status Current/ReviewRequired. Source supplies customer-risk authority; reference syntactic validity grants none. |
| warnings | Exact Warning set; source warnings and command acknowledgments match. Every unreviewed Critical/High in Proposed/AutoConfirmed requires mandatoryReview=true and its matching warning; false cannot bypass the approved mandatory-review requirement. Every applicable gap has its matching warning. Terminal Completed/CompletedWithGaps remains the owning source state, without a gap-count iff rule; SourceLimitation may require warned publication with zero numeric coverage gaps. |
| technicalAppendices | Object exactly `{notes,protectedReferences}`; notes ordered Display records; references array exactly `{id,category,availability,reason}` with UUID/token/availability, linked from findings. No raw resolver. |
| redactionMarkers | Exact Redaction marker set; referenced records/sections exist, markers carry no denied original values. |

Findings severity/state/method preserve RR-P02 exact enums without aliases. Confidence-band semantics, recommendation estimates, risk authority, classification and actual source score consistency remain adapter responsibilities. Independent fixture literals declare their source facts; they are not silently promoted from synthetic draft states.

## Manifest, event and receipt linkage

Manifest keys/types remain exactly RR-P02's manifest table. `artifactInputs` is an array of `{kind,digest,byteLength}` with kind Projection/Score, exactly one of each and exact independently measured canonical lengths/digests. Manifest `provenance`, `retention`, `requiredCategories`, `requiredFields`, `inputs`, `sourceDigest`, `projectionDigest`, `scoreDigest` repeat their original commitments. Classification is MinimizedDerivedReport. `createdBy` is exactly `{tenantId,objectId}` from the verified actor; createdAtUtc is trusted transaction time, not fixture authority in production. ApprovalState is PublishedWithWarnings iff warning set nonempty. ReportVersionId is server-owned UUID. No report UUID/created time is inserted back into original sourceDigest.

Audit/event/publication/read receipt and read-request field sets are the reviewed RR-P02 Closed audit and receipt bytes section, copied literally by independent vectors. Audit `scope` uses four-field scope or null with null resourceKind/resourceId. Verified Human actor or Anonymous/null; sequence canonical positive string; previousEventDigest all-zero only at genesis. Successful publication targets Run; successful exact read targets ReportVersion. The native audited failure stream is not manufactured authority when scope/identity is unresolved. Receipt links are checked against original event bytes/hash, request/command hash and the exact committed report. Unknown commit is never converted into a known Failed event.

## RR-P04 structural linkage clarification

The coordinator-selected linkage clarification in the native contract applies before codec guards: mandatory severe review flags/warnings, exact finding/coverage category and kind matching, provenance-bound SourceLimitation, record-section-specific markers and no standalone metadata marker IDs. IDs are unique within declared collection identities, not globally. Owning terminal-state labels are preserved independently of numeric coverage gaps. Original source-limitation golden bytes remain valid and unchanged.

## Independent evidence and scope

RR-P03 will commit original literal JSON bytes and SHA-256 values before core code. A stdlib reference oracle validates closed shapes, canonical scalars/order/links and independently checks every original commitment. It uses no production module import, package installation, network, real source, database credential, platform identity or permission. Negative vectors include duplicate keys, malformed Unicode, revised binding, unknown fields, noncanonical numbers, mismatched receipts, missing warnings and expired/uncertain emission scenarios. Persisted authorization, transaction races, read delivery, audit and deadline cases remain fixture designs for NPV-T06–15; a reference-script case cannot claim their runtime implementation passed.

The coordinator records this exact addendum revision before core hash implementation. Any new literal shape or authority meaning requires an explicit amendment before dependent code, rather than regenerating oracle goldens from the production codec. Migration/configuration changes: none. Runtime or gate acceptance: none.

## RR-P04 source port amendment

Explicit verified actor and acquired fence now accompany the transaction/command/cancellation source capture call. The adapter revalidates complete minimized classification metadata before protected source loading. This amendment changes no canonical field or literal golden byte. Independent compiled codec checks and in-process publication/read flow checks remain separately reported; actual persisted races remain open.
