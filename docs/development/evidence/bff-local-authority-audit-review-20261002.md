# Independent local authority, audit and hosting review — 2026-10-02

Status: corrected-source non-author review complete; no remaining validated local source finding. Configured hosted gates and private publication are coordinator closure evidence.

The AUTH and AUDIT implementations were written in isolated checkouts. Each author independently reviewed the other module; a third reviewer authored and executed the real PostgreSQL composition fixture and reviewed the coordinator's hosting/authority projection. Agent review authorizes no production release or human gate.

## Scope and evidence

AUTH review traced closed schemas, immutable enrollment identity, attributed lifecycle transitions, exact ownership foreign keys, independent assignment revisions, terminal revocation, direct-user role paging, explicit external organizational origin, monotonic resource/home cutoff, captured versions and same-transaction post-lock freshness. AUDIT review traced restricted SESSION_USER/stream/action bindings, canonical event and receipt bytes, deferred mutation/event/receipt obligations, lock ordering, failure rollback, receipt-only reconciliation, lifecycle timing and chain continuity. HOST review covers exact one-hop/host inputs, supported provider callback compatibility, dedicated key configuration and opt-in subject projection.

The independent fixture uses real PostgreSQL with separate nonsuperuser administrator, provider and runtime LOGIN roles, plus a distinct NONLOGIN function owner. It does not substitute an always-eligible authority. Direct malformed-function tests capture rejection before an explicit rollback, so a deferred missing-receipt error cannot disguise acceptance of a malformed input. Its discarded successful COMMIT response followed by a thrown exception is simulated acknowledgment loss, not an actual network interruption.

## Required corrections and calibration

- Same-transaction authority admission avoids reacquiring a subject lock on a second connection; all writers share subject advisory/row, target and stream lock ordering.
- Audit timestamps and admission deadlines are resampled after stream-head contention. Provider/home evidence cannot renew freshness or regress cutoff.
- Closed strict enum parsing rejects case aliases and numeric strings. SQL and C# receipt field names, bounds, duplicate/unknown-field denial and canonical bytes now agree.
- Restricted SQL audit writes bind the actual login to the configured stream and allowed action; receipts require corresponding writer/stream/action authority.
- Audit security versions require signed64 bounds. Retention functions reject NULL/future time, preserving the accepted 12-month/30-day policy.
- Authority command/provider digests are recomputed at the SQL boundary and must match the exact existing serialization protocol.
- A fractional-rounding concern was **withdrawn** after actual PostgreSQL showed text-to-bigint casts already reject fractional strings. Explicit canonical integer guards and direct fraction/overflow tests document the closed contract.
- Initial standalone formatting checks used ineffective include filters; the whole-solution check exposed the drift. Authors corrected formatting and the final solution check must pass.
- The global ingress Origin guard was removed after independent review showed it would reject legitimate Entra form callbacks; existing application endpoints retain their exact Origin policy.

## Practical limits

Synthetic provider pages and framework key fixtures prove local seams. They do not prove live Graph emission/permissions/home-state semantics, Microsoft Azure SDK conditional storage, managed identities, historical Azure wrapping keys, deployed proxy/reachability, licensed Conditional Access or federation. The opt-in projection creates neither authentication time nor MFA. The disabled diagnostic host activates none of these adapters.

A supplied checkpoint establishes tested ordered chain/head/receipt continuity. Independently retained witness and authorized tombstone/lifecycle trust against a privileged database operator remain NOT VERIFIED; a nonempty lifecycle reference alone is not independent deletion approval. No operational retention schedule, new receipt/authority/key retention policy, real backup/restore or durable audit-store-outage failure preservation is implemented. Milestone 2, G1–G9, image acceptance/promotion, live activation and production release remain open.

Final source review: AUTH `1e5ecb4` + `0a7da148`; AUDIT `64716f6` + `d9173e9` + `970a1ab`; bridge `4202ab8`. Independent PostgreSQL composition passed258 checks; HOST60 actual HTTPS/framework checks passed. AUTH87unit/120PG and AUDIT69unit/81PG are separately executed author evidence, not independently rerun author suites.

Final immutable source, executed counts, configured CI runs and private publication are bound in [the execution evidence](bff-local-authority-audit-20261002.json).


## Additive pilot integration

GitHub initially held checks because the approved pilot base had advanced. Non-author review of merge `2e97739` confirms all113 scoped BFF blobs from `fd2285e` unchanged and all550 scoped pilot runtime/frontend/test blobs from `7b2a741` retained, apart from the documented additive BFF frontend commands/ignore entry. Dependencies/engines remain unchanged. The63-project solution is the exact union of the57-project BFF branch and47-project pilot base, with no duplicates or missing references; both workflows preserve parent steps. Both canonical histories are retained. The coordinator repeated locked audited restore, whole formatting, zero-warning/error build, all portable unit/architecture checks and Gitleaks. Actual configured hosted runs are bound separately in the evidence.


## Hosted precision fixture correction

Linux CI37052401010 passed authority120 but failed the exact positive audit lifecycle boundary. A deterministic seventh fractional digit7 reproduces PostgreSQL canonical timestamp rounding versus Npgsql parameter truncation. Fixture-only `a320876` now derives exact twelve-month ±one-microsecond and stale-restoration time from the persisted PostgreSQL event timestamp. Author81PG/build/format/secrets passed; non-author review confirms all exact early/positive, holds, thirty-day purge, tombstone and restore/replay assertions remain. No SQL/runtime/retention policy changed. Windows and both container jobs passed on the first combined source; the corrected full hosted run is separately bound in execution evidence.
