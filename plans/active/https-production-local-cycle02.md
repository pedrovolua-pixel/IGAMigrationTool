# HTTPS production local cycle 02

Status: RUNNING — approved local protocol implementation and isolated dependency evidence
Date: 2026-10-03 UTC
Owner: Coordinator
Baseline: `b4232bb0b18e8292db5de5cfd7cc488362e205c6`
Approval basis: Owner PC-D01–PC-D05 approval against `d518d25`, accepted [ADR-0012 local Option A](../../architecture/decisions/ADR-0012-authentication-failure-audit-preservation.md), [cycle01 scope](https-production-local-cycle01.md), and owner “Next step”. [Approved product](../../specs/003-health-assessment/product-spec.md), [technical boundary](../../docs/development/https-production-contract-proposal.md) and [test design](../../docs/development/https-production-test-packet.md) remain authoritative.

## Scope and settled internal representation

AP01 implements the accepted failure journal's closed metadata representation and per-attempt freezing only. It does not attach a hook, persist a real journal/event/receipt, acknowledge durable storage, admit a session or reconcile an audit stream. Real anonymous receipt SQL, witness/retention filtering, operation deadlines/limits, live create-only enforcement and host composition still need their exact approved implementation contracts.

A journal descriptor uses one version plus the existing trusted `SecurityAuditBindingV1` (stream/environment/writer binding/writer/client), server-created event/operation/correlation IDs, original terminal occurrence UTC, existing actor/action/outcome/reason enums, nullable verified immutable subject, nullable exact session reference and positive security version. It contains no target chosen separately from the verified subject, previous session, customer/project scope, request/protocol/claim payload or free text. Anonymous subject/session/version are null. A session reference cannot exist without a verified subject and positive security version; a known subject may precede a session. Preserve existing actor enum meanings, but parsing structure never establishes signed human identity, eligibility, authority or witness trust. Production producers must independently prove every nonanonymous identity; no client input can populate it.

The exact serializer/parser is an implementation of the approved field groups: fixed `authentication-failure-journal-v1`, ordinal closed keys, canonical lowercase D GUIDs, exact seven-digit Z UTC, positive canonical decimal-string version or null, exact named existing enums, no JSON numbers or unknown/duplicate/omitted properties, strict UTF-8. Bind every parsed descriptor to an expected trusted full audit binding and trusted current UTC; future occurrence denies. Digest is SHA256 over exact canonical UTF-8. Parsing must reject noncanonical byte spellings/whitespace/escapes, prohibited field/value corpus and conflicting trusted binding. A size bound may be derived from the maximum representable closed field lengths already enforced by existing types/binding regex, not an arbitrary operational quota. A journal cannot carry canonical audit sequence/head or pretend that an occurrence timestamp is the ordered stream's append time.

One attempt context allocates its own nonempty event/operation/correlation IDs, takes its occurrence time at the first terminal failure, and freezes one validated descriptor under concurrent observer calls. All later observations return the same metadata; verified evidence or timestamps must not be upgraded after the terminal outcome. Input validation runs before freeze. Distinct contexts produce distinct IDs. First-terminal freezing is not persistence deduplication or acknowledgment; storage must still resolve the same operation/digest receipt and UNKNOWN writes. No once-written latch, swallowed persistence error, request header IDs, elapsed-time policy or ordinary-log fallback.

## Work packets and ownership

| Packet | Isolated worker and exact permitted writes | Evidence / state |
|---|---|---|
| AP01 | Audit worker: only `src/server/modules/IdentitySessions/AuthenticationFailureJournal.cs`, `tests/unit/IdentitySessions.Tests/AuthenticationFailureJournalChecks.cs`, and one count/call in its existing `Program.cs` | Codec byte/negative/binding corpus, anonymous/coherent verified shape, original-time freeze, concurrent hook overlap, immutable IDs/hash; RUNNING |
| AP02 | SDK worker: only `docs/development/https-key-provider-resolved-graph-evidence.md` | Restore an isolated immutable source copy with the two previously audited exact provider references, retain actual resolved graph/locks, audited restore/build/format facts, signature results for selected artifacts, supported API dispatch proof only if safe/available; RUNNING |
| AP03 | Coordinator: canonical records/integration/full applicable checks | Independent non-author review, scoped checks and exact completion evidence; pending |

AP02 may modify a disposable `/tmp` source copy for experimental restore; it must not install/update dependencies, locks, config or code in the repository or call Azure. Exact candidates remain Blobs1.5.4/Keys1.6.4. Do not invent a host-resolution algorithm, silently upgrade dependencies to avoid signature failure, alter trust stores, disable verification or infer signature/binary success from metadata. A successful restore without explicit verification is not proof of all signatures. Docker/Podman CLIs are unavailable locally; do not install a container runtime or claim Linux checks. No live credential, blob/vault/tenant call. Any actual SDK harness must use synthetic transport/resolver only, retained artifacts and normal verification; stop if unresolved integrity blocks it. This packet may finish with a documented unresolved verifier/SDK proof gap.

## Test mapping and checks

| Test | Required local evidence | Parent trace |
|---|---|---|
| AP-T01 | Exact anonymous/verified closed roundtrip and digest; no sequence/head or prohibited field; trusted binding required | AU02, SEC-PILOT-009, TP-HAS-020 |
| AP-T02 | Duplicate/unknown/missing/numeric/alias/noncanonical/invalid UTF-8/deep/oversized/future/timezone/empty ID/binding swap corpus rejected; serialized bytes inspected | AU02/AU03, NFR-SEC-1/3/6 |
| AP-T03 | Hook overlap/concurrent terminal observations preserve one original descriptor/IDs/time/digest; late identity does not upgrade anonymous; separate attempts remain distinct | AU01/AU03 |
| AP-T04 | SDK actually resolved graph vs candidate-minimum inventory, existing host compatibility, exact locks/source hashes and selected artifact verification with every failure retained | KEY-001/002/005 source prerequisite only; not complete cases |

Workers execute pinned SDK10.0.401 locked restore, actual unfiltered project format verification and warning-free Release build for changed code plus meaningful focused cases/Gitleaks/whitespace. Coordinator executes full-solution locked audited restore/format/build and applicable identity/session/audit/authority/BFF/hosting/architecture regression suites, with a new owned PostgreSQL18.4 cluster for database/HTTPS cases. Repeat only failed or source-affected checks. Independent review follows repository review-feature/security-review and labels all synthetic/metadata-only evidence. No full production AU/KEY case or gate is accepted by this slice.

## Data/configuration/recovery and human tasks

No migration, schema, permission, resource, production dependency, public route, hook or lifecycle job is changed. Codec/context have no durable state; rollback removes unused additive files/call. Journal durability, anonymous SQL receipts, reconciliation/witness/expiry/restore and all actual tenant/key/ingress/CA/host tests remain NOT VERIFIED. Source-only protocol tests cannot meet mandatory failure-event preservation during any outage.

Owner design approval remains recorded; do not ask it again. Keep the six exact human tasks linked in [the acceptance packet](../../docs/development/https-production-test-packet.md); change completion only for evidence this cycle supplies. Private BFF/HTTPS board remains stale under separately rejected credential/network publisher proxy-bypass action, last confirmed snapshot4346e0f,2026-10-02T19:50:45.775370+00:00; preserve unrelated Cycle14 publication and prepare a linked delta in canonical records. This cycle grants no retry/bypass of that publisher.

## Completion evidence

Candidate commits, independent reviews, executed results/failures, source bindings and exact limits: pending.
