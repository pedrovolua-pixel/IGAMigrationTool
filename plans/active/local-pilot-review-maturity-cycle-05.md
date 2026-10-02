# Proposed local cycle 05 — consultant review/history and maturity

Status: DRAFT — explicit owner approval pending. No implementation, new permission, migration or worktree is authorized by this draft.

Requested from: Repository owner acting as product, technical and security owner.

Done when: the owner explicitly approves the bounded synthetic behavior, private transport, server-side test identity, new schema and compatibility contract below. Record that decision before implementation. Production release and live gates remain separate.

## Purpose and existing authority

The owner requested the next phase after completed cycle04. Approved feature003 product requirements FR-HAS-12/13/15/17/20, the technical review/maturity contracts, Milestone5, TP-HAS-003/004/005/017 and the authorization matrix already describe review/history and independent maturity. The standing local build permits synthetic implementation. Automatic approval review nevertheless rejected starting this particular cycle because the new mutation surface, synthetic reviewer identity and persistence schema need explicit approval. This proposal supplies those exact boundaries; it does not reinterpret the rejection as authorization.

## Proposed runnable result

New opt-in profiles `synthetic-review-maturity-equal-v1` and `synthetic-review-maturity-operations-v1` use the existing fixed analysis baselines. The consultant can confirm, reject or defer a proposed synthetic finding, add a plain-text comment, and edit its presentation title/business context. Generated originals, per-object occurrences, saved results and run inputs remain immutable. Current scores and mandatory-review counts recalculate from one durable review snapshot; Confirm/Defer retain the penalty and Reject removes the current penalty. An append-only history shows the original, every action, synthetic actor, server time and revision after reload or host restart.

Maturity appears separately from health, using only frozen fictional indicator fixtures. High health cannot promote maturity. Developing requires documented design and repeatable implementation in at least 60% of mandatory domains; Defined requires at least 80% and evidenced governance ownership; Managed also requires measured operation/regular review in at least 80%; Optimized also requires validated improvement across at least two distinct assessments in at least 80%. Every preceding level is required. Partial or insufficient evidence never counts as met. The proposed fixture denominator includes every declared mandatory domain, with missing evidence prominently disclosed; invalid or empty catalogs expose no maturity projection. This denominator convention is included in the requested approval.

## Private transport and permission boundary

- Only the existing loopback port 5183 host with its explicit synthetic startup flag may expose the new routes. Existing Host/Origin, same-origin antiforgery, strict JSON, 4 KiB body limit and browser security headers remain enforced.
- `GET /local-demo/v1/runs/{runId}/review` returns current review projections, original references, history, per-finding revisions and a review-snapshot digest. Existing analysis reads incorporate the same reviewed snapshot for new opt-in profiles only.
- `POST /local-demo/v1/runs/{runId}/findings/{findingId}/events` accepts exactly an event UUID, expected finding revision, event kind and bounded event fields. Disposition targets are only Confirmed, Rejected or Deferred from Proposed; rejection requires a nonblank reason. Comments/business context are limited to 2000 characters and presentation titles to 250. All text is inert, encoded text in the UI. Unsupported transitions and extra fields are refused.
- The server supplies one fixed active synthetic consultant identity assigned to the fixed synthetic customer/project/environment and fixture categories. Clients cannot choose actor, role, assignment, scope, category, originals, weights or permissions. The policy module tests denial of wrong/inactive/revoked identities, assignments, roles, scope, categories and resource states. This is a disclosed local test double and supplies no Entra/customer authority.
- Identical per-finding event-ID/payload replay returns the original outcome; changed replay or stale revisions conflict without partial writes. An append and its current projection update commit in one transaction.
- Customer risk acceptance, remediation/validated closure, recurrence/cross-run correlation, mentions/notifications/attachments, production identity, AI, tasks, fix execution and publication stay disabled or deferred. This slice does not complete Phase1B/1C or a live gate.

## Schema, versions and rollback

A separate module-owned `synthetic_review` PostgreSQL schema stores immutable run/finding seeds, original digests and occurrence references, current finding projections and append-only events. Seeds bind the complete analysis/input digests. Scope accompanies resource keys; transaction locks/revisions prevent competing updates. Migration content/schema drift is refused. This schema never changes the existing assessment migration or rewrites saved assessment rows. Its connection guard accepts only loopback `iga_synthetic_` databases.

The new profiles explicitly map to the unchanged cycle04 analysis profiles and add a whole maturity catalog/indicator-evidence digest to optional run-input metadata. Missing optional fields remain omitted for historical serialization compatibility. Historical cycle03/04 profiles stay read-only and retain their exact inputs, catalog/script/migration digests and recovery behavior. Maturity definitions/evidence are frozen per new run. No actual One Identity catalog is invented or promoted. Npgsql 10.0.3 is already pinned transitively; a direct reference has the concrete transactional-history need and introduces no new package version.

Rollback disables the new profiles/write routes or uses a compatible reader; it never deletes review events or earlier evidence. No Azure deployment, customer migration or production configuration is requested.

## Planned ownership and checks after approval

| Packet | Exclusive writing paths | Required outcome |
|---|---|---|
| M5 maturity worker | src/server/modules/AssessmentMaturity/, tests/unit/AssessmentMaturity.Tests/ | Pure cumulative thresholds, evidence/ownership guards, distinct-assessment improvement, immutable digests and fixed fixtures |
| R5 review worker | src/server/modules/FindingReview/, migrations/finding-review/, tests/unit/FindingReview.Tests/ | Policy test doubles, immutable originals, append-only history, bounded text, revision/idempotency/concurrency and actual synthetic PostgreSQL checks |
| V5 independent verification | tests/integration/ReviewMaturity.Tests/, tests/e2e/review-maturity/ | Independent golden/adversarial cases, database reload/concurrency/rollback and final browser keyboard actions/history/current scores/maturity/a11y/hostile transport |
| Coordinator | Shared contracts/configuration, run/profile bridge, host/UI, canonical records and existing private Site | Integrate author-independent reviews and execute every applicable combined check |

Writing workers will use isolated worktrees from one known cycle-start commit only after approval. Run pinned audited locked restore, full format/build and all existing unit/integration/architecture checks; actual synthetic PostgreSQL tests; generated private-contract drift, frontend type/format/build/audit; new browser plus historical coverage/analysis regressions; existing infrastructure policies, secret/whitespace checks and Linux/Windows partial CI. Record migrations, versions, source/artifact digests, review findings and unavailable manual/Windows/live evidence. Preserve evidence/commits, clean temporary worktrees and publish the matching owner-private board. Agent review does not replace required human gate approval.

## Decision record

Pending explicit owner approval. The earlier action was rejected before execution; no cycle05 implementation or worker worktree was created.
