# HTTPS production acceptance test packet

Status: Accepted local test design — executable subset and actual results tracked in local cycle plan; live cases remain unverified
Date: 2026-10-03 UTC
Owner: Technical/security, identity/platform and operations reviewers
Product: [Approved product](../../specs/003-health-assessment/product-spec.md)
Technical: [Production contract proposal](https-production-contract-proposal.md), [provider proposal](https-production-provider-contract-proposal.md), [key proposal](https-production-key-contract-proposal.md) and [ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md)

## Acceptance mapping and planned host cases

IP-HAS-003, AC-HAS-4/8–9/11/14/20 and SEC-PILOT-001/002/008/009 map to HO/PV/AU cases; ADR-0002/0004/0009 and G1/G3 to KEY/IN cases; AC-HAS-13/20 and HTTPS-T02–T08 to HO06/LV. This is supporting coverage; complete canonical acceptance and human gate review remain required.

| Test | Level and required evidence |
|---|---|
| HO01 | Composition/startup: no binding, wrong identity/store/discriminator, false activation, incomplete review evidence, synthetic demo/legacy store substitution → no public admission/provider activity. Diagnostic host remains permanently disabled. |
| HO02 | Actual HTTPS + restricted local PostgreSQL: correctly verified original protocol and current exact subject/session/provider/product assignment admit only existing approved session operations. No customer read before resource-context authorization; unknown resource scope and cross-customer locators deny. |
| HO03 | Negative matrix: personal/unknown-origin/external-without-home-proof, wrong tenant/resource/direct role, missing assignment, expired/revoked/suspended subject, exact session/security version/cutoff mismatch deny; no automatic enrollment or fallback. |
| HO04 | Races/time: revocation/assignment change during refresh/subject or audit-head waits; conservative observation15-minute boundary, idle30-minute/absolute8-hour and signed original-authentication limits rechecked immediately before commit; no renewed freshness from completion time. |
| HO05 | Unknown commit/crash: successful ticket/authority mutation always has same-transaction event/receipt; append failure rolls back; acknowledgment loss resolves metadata, never replays consumed authentication or exposes ticket bytes. |
| HO06 | Chromium + manual accessibility: unchanged native form/cookie/CSRF exact-session contract, no JavaScript tokens, no open redirect, generic errors, keyboard/focus/retry behavior. Privileged actions remain denied until approved original-session CA/MFA adapter; manual accessibility is separate evidence. |
| HO07 | Resource adapter: malicious requested scope/category/revision/projection/locator cannot populate a snapshot or open another customer's database; ambiguous authoritative catalogue/ownership denies. Every role/action/grant/state uses the existing authorization matrix. |
| HO08 | Resource races: resource moves, policy/assignment revoke, tombstone/hold or security-version update between preflight and mutation commit deny; protected read projection/resource binding cannot be swapped after authorization. |
| HO09 | Privileged verifier: signed exact-session/original-authentication/context mapping succeeds only with verified current required-strength tenant policy; wrong subject/session/action/version, stale/future auth, missing/removed CA policy, exclusion, context alone with no protecting policy and boolean/amr-only evidence deny. |
| HO10 | Sidecar/step-up: atomic ticket/audit/sidecar commit failure, unknown commit, rotation/revoke and context mapping change invalidate prior proof; replay/cancel/unapproved return/action targets deny. Product authorization is reevaluated after step-up. Exact public contract and sidecar lifecycle must be approved first. |

Provider PV and shared-key KEY stable cases are defined in their focused proposals. Execute all, including malformed/partial/hostile continuations, missing required properties, direct-role completeness, external Member home origin, concurrent ring writes, old-version unwrap, deletion/rebootstrap and two-replica recovery. A fake HTTP/store case never proves live Microsoft semantics or RBAC.

## Authentication audit tests — proposed AU

| Test | Required result / evidence |
|---|---|
| AU01 | Each terminal protocol/authority failure with available PostgreSQL appends exactly one existing typed AuthenticationDenied/Failed event and additive outcome receipt atomically. Run every declared producer-map subcase, including cookie RejectPrincipal/authority exceptions, early challenge/callback-store refusal and late challenge consumption/expiry paths that bypass failure hooks. Hook overlap creates no duplicate; separate attempts stay distinct. |
| AU02 | Invalid/unverified input remains Anonymous with null actor/target/customer/project/session; signed evidence alone may bind immutable subject. Token/claim/header/body/URL/email/exception/evidence corpus cannot reach event, journal, telemetry or public error. Test actual serialized bytes. |
| AU03 | Append/receipt/stream-head failure, cancellation and client disconnect: no session or successful action admitted; required event persistence is not inferred from401/403. Unknown append commit resolves exact receipt, conflicting descriptor/writer/operation denies. |
| AU04 | Selected journal candidate: database down + durable journal available produces one validated conditional-create descriptor with no canonical stream sequence; same ID/same descriptor resolves, altered descriptor conflicts; uncertain write is UNKNOWN. |
| AU05 | Reconciliation: duplicate workers, crash before/after PostgreSQL commit and replay of the same storage receipt produce one source receipt and one canonical event. Never issue session, change authority or renew provider evidence. Poisoned/altered/unwitnessed object does not import. |
| AU06 | Stream time vs source time: recovered event is appended at fresh post-lock time; source occurrence retained in separate metadata; the proposed original-age linked lifecycle is asserted only after PC-D03 data-governance approval resolves its difference from canonical reconciliation-time event age; no policy-compliance inference before that decision. |
| AU07 | Least privilege: actual database roles and, separately, actual cloud writer capability cannot modify/delete canonical/journal history, change binding or read customer evidence. Conditional application code alone is not a compromised-writer test. Distinct reader/reconciler/witness/lifecycle identities tested. |
| AU08 | Both durable paths unavailable/shared identity-network failure: all admission denied, no false durability or audit-compliance assertion. Evidence identifies the unpreserved event and pending exact human outage-policy decision; no sampled/drop-success fallback. |
| AU09 | Lifecycle/restore:12-month soft-delete, purge within30additional days, approved holds, max35-day rolling backup and tombstone replay across canonical/source/receipt/witness material; no recovered access, revived revoked session or restart of original retention. |
| AU10 | Capacity/abuse: bounded anonymous bursts, lock waits, retry storms and full journal. Use human-reviewed limits/deadlines/lag thresholds and required-event policy; no arbitrary number, silent drop or quota-based audit exemption. Cost/overload/runbook approval precedes live execution. |

## Ingress acceptance — proposed IN

| Test | Required result / evidence |
|---|---|
| IN01 | Current exact-peer middleware: correct canonical host/scheme/peer fixture passes; wrong peer, alternate Host, prefix/forwarded Host/original headers, multiple protocol values and unsupported chain deny. |
| IN02 | Actual disposable ACA: record exact benign socket-peer/header shapes without payload. Compare with current default and one-address mode; any mismatch blocks activation and requires a reviewed amendment. Microsoft documentation alone cannot supply a stable immediate peer. |
| IN03 | Actual injected left-hand/multiple/malformed X-Forwarded-For, X-Forwarded-Proto, Forwarded, Host/prefix/original headers cannot spoof effective client/host/scheme. Prove ACA normalization and application handling on the same topology. |
| IN04 | Replica/revision/scale changes and cold starts retain compatible peer contract; changing addresses do not automatically widen trusted ranges. Prove new peer-review lifecycle or deny. |
| IN05 | Private SQL/Blob/Key Vault/registry stay unreachable publicly; only approved portal/BFF ingress, no worker ingress/environment route exposure/extra TCP/debug/product management endpoint. Service Bus retains its exact reviewed exception. |
| IN06 | Same-origin state-changing requests preserve canonical origin, CSRF and callback/logout bindings after forwarding; no forwarded-header environment switch, VNet blanket trust or alternate browser authority. |

## Live prerequisites — LV, separately authorized

| Test | Protected environment/evidence needed |
|---|---|
| LV01 | Explicit tenant/app/resource/role/user/home-state approvals; approved dedicated identities and federation trust; actual signed OIDC redemption, assertion renewal, wrong trust/tenant/audience denial and provider property/permission/replication proof. Requested emails are not immutable identity evidence. |
| LV02 | Named privileged action + licensed CA/phishing-resistant strength and original-session context, exact product assignment/resource/category; real revoked identities denied; no break-glass or support evidence bypass. |
| LV03 | Actual private-path SQL/key/Blob roles, two replicas, rotation/old-version recovery, independent witness/restore/tombstone proof and accepted digest-bound images/provenance/private build/pull. No diagnostic image promotion. |
| LV04 | Fresh complete East US2 quote, exact disposable inventory/duration/spend/reservation/cleanup authorization and overload cost controls. A budget alert is not a spending cap; past24-hour approval does not authorize this topology. |

## Execution order and evidence

1. Human approval resolves the exact applicable PC-D decisions and package pins/locks; conditional local packets receive isolated worktrees/path ownership and non-author review. Unresolved security choices block dependent code.
2. Local fake-provider/actual restricted PostgreSQL/actual HTTPS/SDK-backed test doubles are individually labeled. For each test record source commit, case, tool/provider version, expected/actual outcome and limitation. Never put protected payload/identifiers/raw SQL on Git or the board.
3. Execute every applicable configured restore/audit/format/build/unit/integration/database/browser/type/security/infrastructure/package/Windows check for the eventual combined code patch. No test is PASS until actually run.
4. Separate exact cloud authorization, deployment proof, named human reviews and live gates follow; source-only success cannot satisfy them. Manual MFA/federation/CA/accessibility/recovery/customer acceptance remains explicit.

## Regression and rollback obligations

Preserve existing authentication v1 and canonical audit/receipt v1 contracts, diagnostic disablement, direct-role boundary and all existing negative suites. New metadata is additive only after schema/lifecycle approval; older unknown rows remain denied. Rollback disables admission, drains replicas and preserves audit/authority/journal/witness/tombstone/historical-key state; no version rollback, destructive purge or permission expansion.

## Human tasks and completion conditions

| Task | Requested role | Needed information / completion source |
|---|---|---|
| Provider contract | Identity/security | Owner accepted the local PC-D01 protocol; close exact refresh/manifest inputs and provide required-property and external-home trust evidence in an owner-only copy of [binding intake](bff-production-bindings-template.json). External admission remains denied without it. |
| Shared-key contract | Repository owner + platform/security/operations | Owner accepted the preferred PC-D02 strategy; approve the [exact seven-file public CI packet](https-key-provider-public-export-approval.md), then execute the prepared [supported-platform37-archive verifier](https-key-provider-platform-verification.md) with approved host/SDK/trust provenance (prior macOS36PASS/1FAIL stays unresolved), then close actual guarded dispatch/inventory/lifecycle evidence, and complete private ring/key/historical-version/lifecycle inputs using [key proposal](https-production-key-contract-proposal.md). |
| Failure audit | Technical/security/data-governance | Owner accepted local Option A/[ADR-0012](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md) and the linked source-time interpretation; metadata/freeze and test-only PostgreSQL outcome/source receipts now have bounded local evidence only; review the [production receipt/lifecycle proposal](https-production-audit-receipt-lifecycle-proposal.md) and [ARL01–ARL18 planned cases](https-production-audit-receipt-lifecycle-test-packet.md), close its exact production decisions and witness/capacity/deadline and both-store outage handling; name distinct witness/lifecycle owners. |
| Host/resource and privileged proof | Technical/identity/security | Owner accepted the PC-D05 local boundary; supply exact resource/CA evidence and close sidecar lifecycle, step-up/public error/abuse/deadline contracts; no inferred permissions. [Production proposal](https-production-contract-proposal.md) defines completion. |
| Ingress/live deployment | Platform/operations/security | PC-D04 IN evidence and LV01–LV04 complete: exact approved proxy contract, image/private pull acceptance and fresh priced session. [Protected intake](bff-production-bindings-template.json) links evidence. |
| Private status publisher | Repository owner | Separate exact credential/network publisher approval remains unresolved after automatic-review rejection; source publication approval does not authorize it. [Status-site maintenance](pilot-status-site.md) defines private readback completion. |

The prepared private-board delta is these six linked tasks plus the verified local cycle01/cycle02/cycle03 implementation and cycle04 preparation status; exact unresolved implementation dependencies, live activation and gates remain blocked. Publication is unavailable under the existing publisher block, so the BFF/HTTPS snapshot remains stale. Unrelated confirmed Cycle14 board changes remain preserved.


## Owner approval and current implementation scope — 2026-10-03 UTC

Repository owner replied “Approved” after exact review packet `d518d25` and PC-D01–PC-D05 were presented. This accepts the recommended local design and supporting tests; the earlier proposed-state descriptions are the preparation record. [Local cycle01](../../plans/active/https-production-local-cycle01.md) records the exact accepted scope, implementation/test sequence and dependencies. No unspecified quantity, inventory authority, public step-up/outage response, actual tenant/role/ingress proof, cloud deployment, spending or production release is supplied by this approval. External users and privileged routes remain denied until their independent evidence/contracts close. Only explicitly executed cases may be marked verified.


## Cycle01 executed subset

[Cycle01 evidence](evidence/https-production-local-cycle01-20261003.json) verifies125 new local projector checks within212 authority assertions,29 independent hostile cases and12 applicable regressions. This contributes partial local T03/T05/T07/T08 coverage only; no complete production case or live gate is marked passed. The key-provider artifact audit records five verifier failures and source-only guard feasibility.
