# Phase 1D bounded local test matrix

Status: PROPOSED — paired contract approval pending; tests not implemented or executed
Owner: Quality owner
Last updated: 2026-10-03
Contract: [local-phase1d-contract-proposal.md](local-phase1d-contract-proposal.md)
Governing plan: [approved test plan](test-plan.md), TP-HAS-009/014/020

## Acceptance mapping and independent oracle

All data is fictional and versioned. The verifier preserves an independently authored manifest/payload golden and denial corpus before reading the implementation. An implementation-generated hash or a mock repeating production code is insufficient. Use source-read, raw-resolver and business-write spies so forbidden access is observed, not inferred from empty responses. Compare exact typed values, field sets and immutable version/digest bindings. Test clocks/identity changes come from trusted fixture ports only.

| Test ID | Cases and expected evidence | Requirements / decisions | Level |
| --- | --- | --- | --- |
| P1D-T01 | All six resource kinds succeed for independently scoped synthetic named-user and service identities; exact fields/labels/decimal/unavailable states; repeated reads cause zero business writes. | FR55; AC9/20; D01/02 | Unit + composed host |
| P1D-T02 | Verify manifest and all payload goldens; reject draft/Scoring, unknown fixture/schema, missing binding, invalid state, altered bytes/hash, duplicate IDs and wrong scope/version. No fallback source read. | FR46/55; AC20; TP020; D01 | Contract + host |
| P1D-T03 | Separate MCP grant required despite UI roles; anonymous/share/support/unsupported identity; wrong customer/project/environment/assessment/category; missing customer policy; suspended/revoked identity; stale assignment; deleted/expired resource and read-blocking hold. All deny without protected reads or existence clues. A non-read-blocking retention hold preserves the otherwise authorized read; it creates no right. | AC9; TP009; D02 | Policy + host |
| P1D-T04 | Complete business/raw deny list including cancel/resume/acknowledgment, all unknown and fuzzed method/field spellings, duplicate properties, hostile identifier/path/URL/storage selectors and injected authority. Zero raw resolution/business writes/queued jobs/exports/credentials. | FR55/56; AC14; TP014/020; D02 | Property/adversarial + host |
| P1D-T05 | Protected sentinels in nested title/summary/recommendation/provenance/reference data absent from output, error, cursor, audit and trace. Category denial precedes loading; minimized authorized labels remain. | AC9; TP009; D02 | Unit + serialization + host |
| P1D-T06 | Ordered stable pages of size1/default/max over an authorized snapshot, no duplicates/skips; zero collection; unauthorized rows never counted. Newer report/current run changes do not alter old pages. Mutated/foreign/expired/restarted handles and changed size/resource/scope/identity/report/digest/security revision fail generically. | FR46/55; AC20; D01/02/03 | Contract + composed host |
| P1D-T07 | Revoke assignment/category/policy or delete/expire source between pages and before emission; revoke/regrant cannot reuse old cursor. No stale authorization or historical-publication bypass. Underlying evidence expiry after publication produces only authorized current-unavailable metadata; historical provenance/payload hash and canonical manifest digest stay fixed, with zero raw resolution/restoration. Expired/deleted report source denies entirely. | AC9; TP009/020; D02 | Race + host |
| P1D-T08 | Identity/customer concurrent/rate limits at minus-one/exact/plus-one; paired atomic acquisition; denied operations consume admission budget; arbitrary client customer IDs cannot create limiter buckets. Capacity, cancellation, deadline, exception and oversize release leases; unrelated customer progresses. | FR55; TP020; D03 | Deterministic concurrency + stress |
| P1D-T09 | Exact expiry/window edges, restart, monotonic clock rollback, default/invalid numeric input, UTF-8 envelope byte cap including multibyte text; no partial/truncated content or unbounded cursor registry. | FR55; D02/03 | Boundary + failure |
| P1D-T10 | Success/denial/limited/cancel/dependency event fields, safe UnknownOperation and redacted-field names; hostile request never echoed. Audit unavailable/failure denies content, records only safe operational failure signal; one completed event per admitted invocation, with no duplicate on internal delivery replay; a separately retried read rechecks policy and records its own event. | AC9; TP009; audit policy; D02 | Audit port + failure host |
| P1D-T11 | Published status remains frozen; fixture retains baseline/catalog/profiles/application/approval state and original digest on every resource/page. Response filtering never re-labels its digest as canonical; independent golden remains unchanged. | FR46; AC20; TP020; D01 | Cross-projection contract |
| P1D-T12 | Historical draft/run/input/migration/public DTO bytes unchanged; new module disabled/absent in ordinary host; no SDK/listener/token/key/env setting, database migration or new dependency. | AC14; TP014; ADR0001/4; D01 | Source/architecture + historical regression |

## Integration and operational follow-on

P1D-T01–12 prove only the bounded local harness. Before Phase 1C integration, replace fixture input with its reviewed immutable publication reader and repeat source binding/digest/state races against persisted report versions. The dashboard/Markdown/PDF/link parity suite must use the same actual manifest; matching a fictional golden alone does not satisfy full TP-HAS-020.

Before live MCP exposure, test the approved real protocol/client matrix and token issuer/tenant/audience/client/expiry/signature/consent/identity-kind denials, key rollover, every-request production revocation and cross-customer routing, field minimization, durable audit, distributed rate/concurrency/cursor behavior and retention/purge replay. UAT-13/14 execute only at their operational checkpoint. Supported client versions, transport mapping, status codes, consent/scopes and production numerical thresholds remain undecided, not implied by this matrix.

Accessibility: verify structured labels/gap reasons and non-color status semantics. No visual interface is added; existing manual Windows/screen-reader/PDF acceptance stays open. Performance/reliability is required for bounded limit/cleanup behavior; local fixture numbers are test parameters, not pilot scale/SLO proof.

## Executed evidence and completion

No runtime test above has run in this preparation cycle. Future evidence records exact source/toolchain/fixture digests, host commands, actual assertions and original failures, independent review, skipped cases and limitations. Run applicable locked restore, format, build, source boundaries, unit/contract/integration, historical host/browser regressions, secret/dependency checks and any changed packaging/CI checks once code exists. Do not run unrelated migrations or cloud/provider operations for this documentation packet.

Preparation validation is document link/traceability/whitespace/secret inspection and independent review. Its result belongs in the [active preparation plan](../../plans/active/local-pilot-phase1d-preparation.md); it cannot check off Milestone 11 or any runtime acceptance criterion.
