# HTTPS-PC01: production provider contract proposal

Status: Accepted local design — bounded response projection implementation approved; consent/live admission and exact unresolved inputs remain blocked
Date: 2026-10-03 UTC
Reviewers: Identity/platform and technical/security owners; independent non-author reviewer
Baseline: `b065b51ad97eda9cd96dc08504802b974e819cd1`

The owner's instruction to prepare production specifications authorizes this review document. It does not approve its consequential choices. Basis: [next packet](https-pilot-portal-next-packet.md), [P01–P05 proposal](bff-production-authority-hosting-proposal.md), [ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md), [ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md), [identity design](../security/health-assessment-identity-session-design.md), [authorization matrix](../security/health-assessment-authorization-matrix.md) and [audit policy](../security/health-assessment-audit-policy.md). Those documents remain authoritative. Proposed adapter rules below require attributed review; no policy, schema, route, dependency, grant or runtime is added here.

## Scope and existing compatibility

| Existing boundary | Exact consequence for a future adapter |
|---|---|
| [Closed authority records](../../src/server/modules/IdentityAuthority/Contracts.cs) | Reuse `ProviderRoleBindingV1`, `ProviderObservationV1`, `ProviderReadResultV1` and `HomeStatusEvidenceV1`; no provider-created enrollment, assignment, actor or original authentication time. |
| [Synthetic reader](../../src/server/modules/IdentityAuthority/SyntheticProviderReader.cs) | No HTTP/token implementation exists. Its closed JSON, canonical GUID/time parsing and continuation rules cannot consume arbitrary Graph responses unchanged. |
| [Authority publication](../../src/server/modules/IdentityAuthority/PostgreSqlIdentityAuthority.cs) | Reuse atomic publication, current product revision/security version, monotonic sequence/cutoffs and exact operation receipt. Remote reads must occur outside SQL locks. |
| [Transaction-bound admission](../../src/server/modules/IdentitySessions/ITransactionalSessionAdmissionPolicy.cs) | Use the existing session transaction and protected ticket's original authentication time; separate connections must not reacquire its subject lock. |
| [BFF registration](../../src/server/hosts/BffFoundation/BffRegistration.cs) | Current privilege marker remains false/default-denying. Provider observations cannot verify MFA/CA, privileged access, detailed customer permission or resource ownership. |

Recommended first reviewed production composition: direct assignments for explicitly enrolled internal organizational subjects; external admission remains denied. This is a narrower proposed initial choice, not removal of approved future group/B2B support. The permanently disabled diagnostic host remains disabled. Full production authority, HTTPS-T03/T04/T06 and Milestone 2 are not verified by this packet.

## Trusted binding and refresh command candidate

Protected operator intake uses the [binding template](bff-production-bindings-template.json), outside Git and the status board. Require exact resource tenant, BFF client, resource service-principal object, four enabled nonzero app-role IDs mapped bijectively to existing coarse roles, manifest revision/SHA256/decision, dedicated Graph reader identity and reviewed consent/property-proof references. Email, UPN, display name, group name, invitation recipient and mutable aliases are never keys or authorization evidence.

Proposed internal refresh invocation carries an already validated `SubjectEnrollmentV1`, captured positive signed 64-bit subject security version, candidate positive sequence, pinned `ProviderRoleBindingV1`, server-generated publication operation ID and correlation. It is not an HTTP contract and cannot bind browser claims, headers, arbitrary URLs or caller timestamps. Aggregate enrollment revision and independently checked assignment revisions remain distinct; assignments remain product authority.

**Manifest gap:** `ProviderObservationV1` carries client/resource/role IDs, not manifest revision/hash. Existing publication checks do not prove an operator's manifest hash. Before implementation approval, specify how the immutable reader binding and SQL role seed are attested and held consistent through publication; either a reviewed configuration invariant or an explicitly reviewed schema amendment. Do not silently add fields or claim the current schema checks that hash.

## Proposed Graph requests and consent alternatives

Only the global Graph `v1.0` origin below is proposed. Other clouds, beta, enumeration, `/me`, UPN resolution, group traversal, sign-in logs and write/revocation commands are outside this packet. Identifiers below are symbolic placeholders for protected immutable IDs, never values from a request.

```http
GET https://graph.microsoft.com/v1.0/users/{enrolledObjectId}?$select=id,accountEnabled,userType,externalUserState,signInSessionsValidFromDateTime
GET https://graph.microsoft.com/v1.0/servicePrincipals/{resourceServicePrincipalId}/appRoleAssignedTo?$filter=principalId%20eq%20{enrolledObjectId}&$select=id,appRoleId,principalId,principalType,resourceId
Authorization: Bearer {serverOnlyGraphToken}
Accept: application/json
```

No request body. Construct URLs only from trusted GUIDs; accept complete HTTP 200 JSON only. The service-principal API documents `$filter`/`$select`, but the exact `principalId eq` filter, selected-property completeness and returned continuation shape still need authorized live proof. A rejected/ignored filter must not trigger broader enumeration or another permission automatically. [Service-principal API](https://learn.microsoft.com/en-us/graph/api/serviceprincipal-list-approleassignedto?view=graph-rest-1.0), [Get user](https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0).

| Consent candidate, not a grant | Documented API minimum and unresolved boundary |
|---|---|
| Dedicated app-only reader: user GET plus resource direct roles | `User.Read.All` and `Application.Read.All` are the respective application minima. These are tenant-wide capabilities; an exact filtered URL does not scope consent to one user/application. Human review must accept that breadth and exact identity. |
| Required `accountEnabled` read | Microsoft names `User.EnableDisableAccount.All` plus `User.Read.All` for **read and write**. This does not establish that extra write consent is necessary for this GET. Prove a trustworthy boolean under the accepted read-only consent; unavailable/missing property blocks admission. No automatic write grant. |
| User-centric/effective assignment alternative | `GET /v1.0/users/{id}/appRoleAssignments` requires application `Directory.Read.All`; includes direct assignments and assignments through direct group membership. Microsoft calls for eventual consistency plus count for completeness in large indirect-assignment cases. Broader consent, group/effective semantics and pagination need separate approval. |
| Delegated reader alternative | User GET can use delegated `User.Read` for the signed-in profile; resource assignment listing uses delegated `Application.Read.All` plus a supported directory role. It changes credential/operator availability and cannot replace the same mandatory properties or privileged proof. Not selected. |

[User assignment API](https://learn.microsoft.com/en-us/graph/api/user-list-approleassignments?view=graph-rest-1.0) and [permissions reference](https://learn.microsoft.com/en-us/graph/permissions-reference) support this comparison. No `ReadWrite` permission, assignment-write permission or provider-session revocation permission is proposed. Group-based application assignment also has licensing and nesting limits; review those before selecting that alternative. [Group assignment](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal).

Proposed credential alternatives are an explicitly bound workload identity or reviewed certificate/federated application credential using a supported Microsoft authentication library. Exact identity/tenant, credential mechanism, library version, lock/audit evidence and renewal/incident owner remain approval inputs; no SDK version is guessed. For app-only Graph, target `https://graph.microsoft.com/.default`; the flow supplies no refresh token. Keep tokens server-side in the supported credential cache, never in browser state, tickets, receipts or logs. Treat Graph access tokens as opaque service credentials, not product/MFA authority; send them only to validated Graph URLs. Token reuse does not renew provider freshness. The BFF code-redemption identity and Graph reader are separate proposed permission boundaries; delegated login scopes must not acquire tenant-wide Graph authority implicitly. [Client credentials](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow).

## Response projection and complete pagination candidate

Proposed production adapter validates bounded raw JSON before projecting only the existing closed records. Do not pass raw Graph JSON directly to the synthetic codec or let an SDK silently drop malformed/duplicate selected fields. Proposed envelope allowance: optional bounded string `@odata.context`, discarded without dereferencing; all other unexpected properties/annotations deny until explicitly reviewed. Existing internal codec stays closed. Graph metadata and DateTimeOffset normalization are new proposed adapter behavior, not existing implementation claims.

| Selected value | Required interpretation |
|---|---|
| User `id` | Exact enrolled nonempty canonical lowercase GUID; no UPN fallback. If live casing differs, return for reviewed normalization, never alter immutable subject matching. |
| `accountEnabled` | Actual boolean, never string/null/missing. False is a complete negative eligibility observation, not a transport failure. |
| `userType`, `externalUserState` | Exact `Member`/`Guest`; invitation exact `Accepted`/`PendingAcceptance` or explicit null. External origin requires Accepted. Case aliases, numeric strings and unknown states deny. Internal origin does not become external solely from this field; incompatible origin binding denies. |
| `signInSessionsValidFromDateTime` | Required non-null nonfuture UTC cutoff. Proposed wire normalization accepts ISO date/time ending `Z`, zero through seven fractional digits, and emits internal `yyyy-MM-ddTHH:mm:ss.fffffffZ` without rounding. Other forms require review; never substitute epoch/now. Preserve its instant and monotonicity. |
| Role row `id` | Opaque distinct nonempty string, at most 256 characters; Graph assignment IDs are not necessarily GUIDs. |
| Role row identifiers/type | Exact enrolled principal, configured resource, `principalType=User`, known enabled nonzero role ID; duplicate assignment/role, default-zero, disabled, unknown, Group or ServicePrincipal row denies the whole result. Sort distinct accepted roles for the existing record. |
| `value`, `@odata.nextLink` | Required array; only missing/null nextLink terminates validated paging. Empty complete result is role absence; malformed or unconsumed paging never means absence. |

The cutoff is provider revocation evidence, not MFA, original login time or product enrollment. [User properties](https://learn.microsoft.com/en-us/graph/api/resources/user?view=graph-rest-1.0) and [assignment properties](https://learn.microsoft.com/en-us/graph/api/resources/approleassignment?view=graph-rest-1.0).

Carry forward the synthetic bounds as proposed initial limits: response 1–65536 bytes including metadata; depth 12; at most 16 pages, 4 rows/page and 4 unique roles; continuation 8192 characters and skiptoken 4096 characters. Prove compatibility; exceeding bounds denies rather than truncates. Quantitative network timeout/retry limits still require review and must fit the existing strict 15-minute freshness bound.

Validate every complete nextLink before using it unchanged: HTTPS `graph.microsoft.com`, default port 443, no userinfo/fragment, exact resource path, exact decoded filter/select, no duplicate query names, exactly one nonempty `$skiptoken`, no encoded path tricks or cycles. Disable automatic redirects and cross-origin credential forwarding; never fetch metadata or provider-supplied arbitrary URLs. Microsoft says follow the entire nextLink and allows generic `$skip` or `$skiptoken`; the current seam accepts only the latter with exact filter/select. An otherwise legitimate incompatible continuation is a denied compatibility gap requiring review, not permission to rewrite/drop query parameters or widen the validator. [Paging](https://learn.microsoft.com/en-us/graph/paging).

## Observation, transaction and failure semantics

1. Capture current active product enrollment/revision, security version and pinned manifest; allocate a candidate sequence from trusted state. Sample trusted `startedAtUtc` **before the first credential/provider await**, including credential acquisition, retries, user/role/home reads. All remote waits belong to this observation window; snapshot capture does not reset it. Do not hold a database transaction during remote calls.
2. Read the user, every validated direct-role page and required home evidence using that exact subject/resource binding. Require start<=completion<=current trusted UTC and age strictly less than 15 minutes after every wait. Completion/token refresh/final page must not reset start. Graph user and paged-role reads are not a single provider transaction.
3. Publish only a complete validated `ProviderReadResultV1`. Under the existing subject advisory/row lock, resolve the identical receipt first; otherwise compare captured enrollment revision/security version and current binding, monotonic sequence/start/resource cutoff/home cutoff and home checked time. A concurrent assignment/revoke/newer publication invalidates stale capture; reread with a new observation, never relabel the old response.
4. Existing publication atomically persists authority, linked `AuthorityChanged` event and canonical-digest receipt; provider eligibility changes advance the single `identity_sessions.subjects.security_version` and revoke tickets. Recheck resource/home deadlines after subject and audit-stream waits and immediately before commit, including deferred SQL checks. Failure rolls back all mutation/event/receipt. Unknown commit resolves the original server operation/digest metadata; no credential replay or new timestamps.
5. Every request still checks current product/session authority and original authentication against the maximum resource/home cutoff. Composite freshness uses the earlier resource start/home check. Observation never clears Suspended/Revoked, approves assignments, or changes origin. Explicit known changes must disable/increment authority in the attributed product transaction before next access; Graph replication may lag even a new GET. [Replication warning](https://learn.microsoft.com/en-us/graph/api/serviceprincipal-list-approleassignedto?view=graph-rest-1.0).

**Refresh cadence decision:** mandatory every-request current authority checks are not a promise of every-request linearizable Graph reads. Review fresh provider reads before issuance/rotation and a bounded foreground refresh contract, versus reuse of an existing published observation within the approved strict 15-minute bound. Neither scheduling choice, background fallback nor stale-positive reuse after a required read fails is selected here. Required-read failure denies that attempted operation without advancing freshness.

| Failure | Closed result and recovery boundary |
|---|---|
| Complete valid account false or zero direct roles | Publish complete negative eligibility through existing atomic contract; no inferred product suspension/reenrollment. |
| HTTP 202/401/403/404/429/5xx, absent field, malformed/truncated body, cancellation, timeout, invalid continuation or unknown home | No complete observation and no manufactured account-disabled/empty-role result. Generic denial for the attempted operation; no freshness advance, payload leakage or elevated permission retry. Caller cancellation propagates internally. |
| Throttle/transient retry | Respect supported Retry-After guidance within reviewed attempt/deadline bounds and original observation start; abandon if freshness expires. A later new read uses a new observation/operation, while uncertain publication uses its identical receipt. [Throttling](https://learn.microsoft.com/en-us/graph/throttling). |
| Audit unavailable | Existing transactional denial/rollback is necessary but does not durably preserve required failure events during outage. Production audit preservation/reconciliation remains a separate unresolved reviewed contract. No ordinary-log fallback is selected. |

Use existing closed failure/action enums and safe correlation, not raw Graph errors, tokens, response bodies, claims, URLs, identities or customer evidence in ordinary logs/status output. Preserve approved audit retention; this proposal chooses no provider-cache, operational receipt or enrollment deletion duration. No startup migration/backfill. Rollback disables admission and preserves security versions, receipts, cutoffs and tombstones; it must not resurrect access.

## External organizational home-proof limit

`OrganizationalOrigin` controls the external lifecycle independently of Graph `userType`; external Members and Guests require the same sponsor/engagement, expiry/review and exact home checks. Invitation acceptance is not product onboarding or current home account proof. `idp` is an authentication-provider claim, not a home object ID or evidence that a home account is still enabled; personal/consumer and unknown origins deny. [B2B properties](https://learn.microsoft.com/en-us/entra/external-id/user-properties), [ID-token claims](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference).

The current default home source denies. A future binder must independently establish immutable nonconsumer home tenant/object correspondence to the resource subject, approved trust/consent and publisher, current active status, complete evidence, conservative check start, original-authentication cutoff, freshness and monotonicity; bind it to the captured resource subject/enrollment/security version. `HomeStatusEvidenceV1` does not contain home object ID, trust method or publisher evidence: exact binder/storage/verification must be separately reviewed, not inferred from resource properties or email. Resource-tenant `revokeSignInSessions` does not revoke externally authenticated sessions; it cannot substitute home evidence and is not an adapter command. [Revocation limitation](https://learn.microsoft.com/en-us/graph/api/user-revokesigninsessions?view=graph-rest-1.0).

## Stable acceptance cases for the future approved implementation

All cases below are PLANNED, not executed here. LOCAL means deterministic fake transport plus actual local SQL/session composition; LIVE means separate authorized protected tenant evidence. Preserve exact response/operation digests in protected test evidence, never provider payloads in Git. Trace A=[AC-HAS-9](../../specs/003-health-assessment/product-spec.md)/[TP-HAS-009](../../specs/003-health-assessment/test-plan.md)/[IP-HAS-003](../../specs/003-health-assessment/implementation-plan.md); S=identity design/HTTPS-T03; R=ADR-0009/HTTPS-T06; P=[NFR-SEC-1/3/5/6 and NFR-PER-2](../../product/non-functional-requirements.md). These cases contribute to broad requirements, not complete gate acceptance.

| Stable ID | Procedure and expected evidence | Trace / proof |
|---|---|---|
| HTTPS-PROV-T01 | Exact internal enrolled subject, accepted read-only properties, complete direct roles: publish once; detailed resource authorization remains independently checked. | A,S / LOCAL+LIVE |
| HTTPS-PROV-T02 | Unenrolled/Pending/Suspended/Revoked, wrong tenant/user/client/resource/manifest or email alias: no reader-created authority or admission. | A,P / LOCAL |
| HTTPS-PROV-T03 | Missing/null/string account status; absent/unknown/case/numeric enum; duplicate properties/unknown metadata: fail closed before publication. | A,P / LOCAL+LIVE property proof |
| HTTPS-PROV-T04 | Valid zero-role/account-disabled results versus malformed/404/403: complete negatives atomically revoke; unavailable never fabricates negative data or fresh time. | A,R / LOCAL+LIVE |
| HTTPS-PROV-T05 | Opaque assignment ID succeeds; duplicate, unknown/zero/disabled role, wrong principal/resource or group/workload row denies the whole result. | A,P / LOCAL+LIVE |
| HTTPS-PROV-T06 | Two valid pages succeed unchanged; missing/unconsumed final page, oversized/deep/truncated/duplicate/cyclic pages and a 17th page deny with no partial publish. | A,P / LOCAL+LIVE paging proof |
| HTTPS-PROV-T07 | Redirect, foreign host/cloud/path, port/userinfo/fragment, encoded path, changed filter/select, duplicate query or `$skip` incompatible shape: no request/token forwarded. | A,P / LOCAL+LIVE compatibility |
| HTTPS-PROV-T08 | Approved UTC wire variants preserve the same cutoff instant; null/future/regressing/unreviewed forms deny, never epoch/now substitution. | S,P / LOCAL+LIVE format proof |
| HTTPS-PROV-T09 | Token acquisition, retry, final-page or home wait crosses 15 minutes; start/future/completion anomaly: no refreshed observation. | S,R / LOCAL |
| HTTPS-PROV-T10 | Subject-lock/audit-head/commit wait crosses resource or home deadline: no mutation/event/receipt commit; exact original auth cutoff still enforced. | S,R / LOCAL actual SQL |
| HTTPS-PROV-T11 | Concurrent revoke/assignment or later publication wins: stale capture/sequence/replay denies; cannot relabel the old response with new revision/start. | A,R / LOCAL actual SQL |
| HTTPS-PROV-T12 | Known removal while Graph still returns old role: product version/disable denies next access; record lag without claiming instantaneous provider removal. | A,S,R / LOCAL+LIVE |
| HTTPS-PROV-T13 | Same operation/digest after unknown commit: exactly one event/version/receipt; changed target/digest/issuer denies without reauthentication. | R,P / LOCAL actual SQL |
| HTTPS-PROV-T14 | External Member and Guest with missing/personal/unknown home, accepted invitation only, inactive sponsor or changed engagement: both deny. | A,S / LOCAL+LIVE |
| HTTPS-PROV-T15 | Home subject/tenant/revision/version mismatch, future/stale/inactive/incomplete/regressing home evidence: deny; advancing home cutoff invalidates old original authentication. | A,S,R / LOCAL; LIVE binder blocked |
| HTTPS-PROV-T16 | Resource observation/token claims try to supply MFA/original auth or customer grant: privilege/resource policy stays denied by independent verifier. | A,S / LOCAL |
| HTTPS-PROV-T17 | Wrong reader identity/tenant/consent, token failure or unavailable required property: deny with no delegated/development/write-permission fallback. | P / LOCAL+LIVE exact grants |
| HTTPS-PROV-T18 | 429 Retry-After, timeout/cancellation and bounded retry: preserve first start, never busy-loop or publish partial last-known data; no protected logs. | P,S / LOCAL |
| HTTPS-PROV-T19 | Audit failure before/after append and lost commit: rollback or exact reconciliation; outage event preservation tested separately under the accepted future audit contract. | R,P / LOCAL; LIVE outage blocked |
| HTTPS-PROV-T20 | Restore/rollback and manifest drift: no decreased version/cutoff or old-session resurrection; deny mismatched seeded binding. | A,R / LOCAL; LIVE recovery blocked |

## Decisions and completion inputs

| Decision | Required attributed owner outcome before implementation/live proof |
|---|---|
| PROVIDER-D01 | Identity/security: accept direct-only internal initial admission or separately specify group/effective reader; external stays denied until D05. |
| PROVIDER-D02 | Identity/platform: accept exact app-only/delegated credential/consent alternative and tenant-wide breadth; authorize a protected property/filter/paging proof session. No consent granted by this file. |
| PROVIDER-D03 | Technical/security: accept normalization/envelope/continuation/bounds and exact dependency pins; resolve live format incompatibilities explicitly. |
| PROVIDER-D04 | Technical/security: accept refresh triggering, network retry/deadline limits and manifest/SQL seed consistency contract; preserve strict existing freshness and known-change revocation. |
| PROVIDER-D05 | Identity/security: either keep external denied or review exact organizational home-object/trust/publisher/cutoff binder and consent. Resource Graph alone cannot close this proof. |
| PROVIDER-D06 | Coordinator/operations/security: production host/resource policy/privileged verifier, durable audit outage decision, shared-key recovery, ingress peer, immutable image and priced live session must close independently. |

Implementation packet after approval: bounded provider HTTP/credential/projection seam, deterministic transport negatives, existing real SQL/session composition and independent review; coordinator owns solution/dependencies/canonical records. Protected live proof requires exact immutable bindings, least-permission consent and property/filter/pagination evidence, named test personas/operators and approved spending/access. No API was called against a tenant, no dependency was added and no runtime test was executed for this preparation. All Microsoft links above were checked against primary documentation on 2026-10-03 UTC; published examples are not proof of this tenant's actual access or response shape.


## Owner approval and current implementation scope — 2026-10-03 UTC

Repository owner replied “Approved” after exact review packet `d518d25` and PC-D01–PC-D05 were presented. This accepts the recommended local design and supporting tests; the earlier proposed-state descriptions are the preparation record. [Local cycle01](../../plans/active/https-production-local-cycle01.md) records the exact accepted scope, implementation/test sequence and dependencies. No unspecified quantity, inventory authority, public step-up/outage response, actual tenant/role/ingress proof, cloud deployment, spending or production release is supplied by this approval. External users and privileged routes remain denied until their independent evidence/contracts close. Only explicitly executed cases may be marked verified.
