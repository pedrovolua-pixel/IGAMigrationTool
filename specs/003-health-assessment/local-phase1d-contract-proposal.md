# Phase 1D local projection contract proposal

Status: PROPOSED — exact local contract approval pending; no runtime implemented
Owner: Technical owner
Reviewers: Technical owner, security owner, Phase 1C reporting owner, quality owner
Last updated: 2026-10-03

## Authority and scope

The owner's “Do it” authorizes the preparation recommended in the readiness assessment: contract, bounded implementation packet and test matrix. It does not approve the new choices below. The approved [product](product-spec.md) FR-HAS-46/55/56, AC-HAS-9/14/20, [technical specification](technical-spec.md#phase-1d-mcp), [authorization matrix](../../docs/security/health-assessment-authorization-matrix.md) and [local pilot plan](../../plans/active/one-identity-local-pilot-build.md) govern this proposal.

Preserve accepted ADRs [0001](../../architecture/decisions/ADR-0001-pilot-application-shape.md), [0002](../../architecture/decisions/ADR-0002-pilot-tenant-isolation.md), [0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md) and [0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md). This is an internal modular projection and test host, not a new service or public API. It adds no registration, SDK, listener, provider token validation, database, migration, customer grant, retention policy or production setting. No new dependency is proposed.

## P1D-D01 — Internal projection boundary and publication handshake

Recommend a dependency-free, in-process test host and typed projection module. A server-created fixture context supplies the identity and resolved scope; request arguments never supply authority or storage locators. A narrow read port returns a single immutable synthetic publication. Policy, field minimization, clock, limiter and audit ports have explicitly labeled test doubles; the future application adapters must use the central implementations. Do not extend the current human-only BFF policy to grant service/MCP authority by inference.

Use contract version `synthetic-published-health-read-v1`. Authored fixtures carry an explicit `SyntheticPublishedFixture` marker. The marker is accepted only by this isolated test host and is never evidence of actual publication, customer approval or a production baseline. Current `ReportDrafts` snapshots and `Scoring` runs are rejected. Do not turn a draft into a publication by renaming its state. The Phase 1C adapter remains unimplemented until its exact immutable manifest contract is reviewed.

Proposed fixture source bindings:

| Field | Type / constraint |
| --- | --- |
| contractVersion, fixtureKind | Exact strings above; unknown versions fail closed |
| customerId, projectId, environmentId | Nonempty fictional opaque IDs resolved by the host, identical to trusted scope |
| assessmentId, reportVersionId | Nonempty fictional opaque IDs; report version is immutable and unique within scope |
| baselineVersion, catalogVersion, scoringProfileVersion, maturityProfileVersion, applicationVersion | Nonempty explicit frozen version identifiers; no fallback to latest |
| assessmentState, approvalState | Frozen publication values, present in the authored fixture; no implicit approval |
| manifestBytes, manifestDigest | Original immutable fixture manifest bytes and lowercase SHA-256 of those exact bytes; independently authored golden digest |
| projectionContent | Closed typed resource payloads, with unique scoped item IDs and all frozen bindings validated |

The exact manifest bytes also bind a digest of each typed resource payload. Freeze UTF-8 serialization, property order, optional fields and golden vectors in an independently reviewed internal implementation contract before coding; do not invent a production canonicalization scheme. Conflicting metadata, source bytes, payload hashes or unknown fields reject the entire source. Preserve decimal scores and explicit unavailable/gap states; no recomputation of health, maturity, severity or recommendations.

Every successful resource result carries the same authorized frozen bindings and original manifest digest. It does not claim that a filtered response's byte digest equals the canonical manifest digest. Never expose bindings or a digest for a denied resource. Current underlying evidence availability is a separately labeled authorized overlay (`currentAvailability`: Available, Unavailable or Redacted; `availabilityReason`: a frozen allowlisted safe reason code). It is not part of the frozen publication payload hash and cannot rewrite historical availability, provenance or manifest digest. Underlying evidence expiry after publication permits only authorized unavailable metadata, with no raw resolution or restoration. Expiry/deletion of the report source itself denies the whole read. References never resolve raw content.

P1D-D01 also selects **frozen published assessment status only for this first local slice**. Current run status is a distinct deferred read with its own approved schema/permission; it cannot overwrite frozen approval/state/version fields. This bounds the slice, not the complete FR-HAS-55 implementation.

## P1D-D02 — Closed resources, policy, cursors and audit

Internal request fields are `contractVersion`, `resourceKind`, `assessmentId`, `reportVersionId`, and, for collections, optional `pageSize` and `cursor`. Reject duplicate/unknown fields, malformed IDs, unknown resource kinds and extra selectors. No free-text filters, caller URLs, source paths, raw identifiers, inline role claims or arbitrary tool invocation. Resource names below are local dispatch enum values, not registered MCP methods, OAuth scopes or public paths.

| Resource kind | Closed allowed content after policy/minimization | Local action identifier |
| --- | --- | --- |
| PublishedStatus | Frozen assessment state, approval state, declared limitations | published-status.read |
| Coverage | Existing authorized assessed/gap/unavailable counts and category summaries, explicit labels/limitations | coverage.read |
| Scores | Frozen health, quality and cumulative maturity values/labels, unavailable reasons and permitted profile provenance | scores.read |
| Findings | Scoped finding ID, authorized title/summary, severity, review state, confidence/mandatory-review labels and permitted protected-reference IDs | findings.read |
| Recommendations | Scoped recommendation/finding IDs, permitted reviewed summary/options, priority/effort and unverified/review labels present in the source | recommendations.read |
| ProtectedReferences | Opaque scoped reference ID, permitted category, availability and redaction labels | protected-references.read |

A separate synthetic MCP action/category grant is required even when the actor has UI read rights. Synthetic named-user and service identities have different identity kinds; both require active scope assignment, active identity, permitted action/category, customer MCP policy, resource state and current retention/deletion/hold decisions. Anonymous, share-link, unsupported workload and support identities receive no inferred right. Multiple human roles cannot convert a prohibited MCP operation into an allowed one.

Authenticate/resolve scope and deny access before customer source reads; the reader may accept only a server-resolved scope. Evaluate category/state policy before protected content loading and filter before serialization. A serializer allowlist alone cannot sanitize protected values embedded in titles, summaries or nested recommendation prose: permitted text must come from a trusted minimized projection and prohibited sentinel values must not enter results, cursors, errors, traces or audit. Do not fetch raw evidence in order to redact it. No locator, token, script/SQL, file path, credential, external URL or raw object value is part of protected-reference metadata.

Business mutation and raw-value denial is total: start/run/cancel/resume analysis; comments/presentation edits; finding dispositions; risk acceptance; publication/acknowledgment; export/link/task generation or mutation; source-system operations; remediation/migration; arbitrary tools and every unknown/fuzzed operation. Internal audit/limiter bookkeeping confers no business mutation capability.

Collections use ascending ordinal item-ID order after authorization, with no unauthorized total/count or cursor clues. First-page selection pins one report version; later versions do not change it. The in-process cursor is a cryptographically random 32-byte handle into a bounded host-owned registry. Bind it to identity kind/ID, security revision, resolved scope, resource kind, report version/digest, page size, position and expiry. It contains no serialized payload/authority. Every page rechecks current grants, policy, deletion/retention and availability; a grant revision change invalidates the cursor instead of silently skipping data. Malformed, missing, expired, foreign or mismatched handles give generic unavailable. Host restart loses handles; retry begins a new first page. This is a local test strategy, not a production durable cursor decision.

Typed local outcomes: `Success`, `InvalidRequest`, `Unavailable`, `Limited`, `Cancelled`, `DependencyUnavailable`. Unauthenticated, unauthorized, absent, wrong-scope, revoked and unavailable source share `Unavailable` without existence detail. `InvalidRequest` describes only public input shape, never resource state. Unknown methods have no echo in the result. No partial data on failure; no fallback to drafts, stale authority or another customer. Transport status codes remain deferred.

Record one completion event before returning content, using the approved [audit policy](../../docs/security/health-assessment-audit-policy.md): safe server identity/reference, resolved scope when authorized, allowlisted resource/action and field-name/redaction sets, typed outcome, correlation and elapsed time. Unknown input maps to `UnknownOperation`, never arbitrary text. Denial metadata does not echo untrusted locators or identifiers. Audit unavailable means no content; synthetic records remain test evidence, not durable audit compliance. Audit/security-event failures need a separate operational signal with no request payload. Production audit integration and 12-month lifecycle remain unverified.

## P1D-D03 — Explicit synthetic limits and lifecycle

Recommend these reproducible **local fixture parameters only**; none becomes an approved production default:

| Parameter | Local proposed value |
| --- | --- |
| Page size | Default 25; accepted integer range 1–100; invalid values rejected |
| Serialized result | Maximum 262,144 UTF-8 bytes including envelope; oversize fails without partial data |
| Concurrent reads | 4 per identity and 16 per resolved customer across all resource kinds in one host |
| Rolling request budget | 60 per identity and 300 per customer in 60 monotonic seconds; admitted requests include denied operations |
| Cursor lifetime / capacity | 300 monotonic seconds, non-sliding; 128 handles per identity / 512 per customer |
| Execution deadline | 5 monotonic seconds per admitted read; cancellation/dependency failure releases concurrency leases |

Acquire identity/customer limits atomically; do not consume one counter when the paired acquisition fails. Never evict another customer's cursor to admit a request. Registry capacity gives `Limited`; expiry/identity invalidation removes entries. Unknown/unresolved identities use a host-wide bounded ingress guard rather than a client-created customer bucket; exact guard algorithm/limits are an internal freeze prerequisite. Clock rollback cannot extend expiry or restore budget. No limiter payload or authoritative grant cache; current policy is checked again before emission. Stress tests must cover failure/cancellation and isolated-customer progress, not just boundary constants.

Removing the local module/test host is the rollback. No database down migration or changes to historical draft/run DTOs are needed. Future production grants, registrations, token claims/consent, protocol version/transport/SDK, routes, numerical budgets, authenticated cursor mechanism, durable audit/rate stores and retention integration require an exact follow-on client contract and technical/security review before activation.

## Alternatives and boundaries

| Choice | Alternative / tradeoff | Recommendation |
| --- | --- | --- |
| Isolated projection harness | Implement live MCP transport immediately; exercises client integration but chooses unresolved public/authentication contracts | Harness first; full protocol conformance is deferred |
| Synthetic publication input | Wait for all Phase 1C delivery/PDF work; reduces fixture divergence but blocks independent local boundary tests | Versioned fictional publication fixture, paired later with exact Phase 1C adapter proof |
| Frozen status | Mix current run status into a frozen report; convenient but ambiguous version/state provenance | Frozen status for this slice; distinct current-status contract later |
| Ephemeral cursor registry | Signed stateless production cursors; durable across restarts but requires key/rotation/replay decisions | Bounded local registry with explicit restart loss |

Principal risks: fixture/publication divergence; treating test policy as production authority; text leaking protected values; limits proven only within one process; failure of audit/revocation between read and emission. These risks are acceptance cases in the [test matrix](local-phase1d-test-plan.md), not accepted live residual risks. MCP structured labels preserve non-color meaning; no new UI/PDF or manual accessibility claim follows.

## Approval

P1D-D01, P1D-D02 and P1D-D03: PENDING. See the [decision packet](local-phase1d-decision-packet.md). Only an authorized human may approve the exact source. Full Milestone 11, Phase 1D, UAT-13/14, TP-HAS-009/014/020 and G1–G9 remain NOT VERIFIED.
