# Phase 1D cycle02 proposed publication conformance tests

Status: PROPOSED — NOT EXECUTED; actual contract and test approval pending
Owner: Quality owner with reporting, technical and security owners
Date: 2026-10-03
Contract: [readiness and gaps](phase1d-integration-readiness-cycle02.md)
Candidate: [isolated reader plan](phase1d-integration-implementation-candidate-cycle02.md)

These tests apply to the future approved actual publication/reader contract. They neither re-run nor replace the completed synthetic P1D-T01–12 matrix. Before writing the consumer, preserve independently authored native publication fixtures, exact bytes/commitments and denial vectors from the reviewed contract. Implementation-generated hashes alone are insufficient. Source/policy/audit spies and owning persistence footprints establish actual reads/nonmutation. Every case below remains NOT EXECUTED.

| ID | Proposed case and required evidence | Traceability / level |
| --- | --- | --- |
| P02-T01 | Only a committed exact published ReportVersion succeeds; Scoring, detached draft, saved review and evaluation capture deny without fallback or relabeling. | FR45/46/55; AC6/20; TP006/020; source contract |
| P02-T02 | Publisher expected-revision race, manifest/blob/frozen-score interruption and idempotency retry never expose a partial version; one visible version per approved key. Publisher-owned evidence may satisfy this prerequisite. | FR45; AC6; TP006; ADR0003; persistence |
| P02-T03 | Original manifest and frozen projection integrity match independently supplied bytes under the reviewed native scheme; per-item, response or grant commitments are tested only if selected and approved. Scope/report/schema/state/binding substitutions and altered bytes deny the requested result. | FR46; AC6/20; TP006/020; contract |
| P02-T04 | Later review/profile/score edits and new report creation leave old manifest/selected frozen projection/score/maturity/approval/warning bytes unchanged; old reads retain exact version. | FR45/46; AC6/17/20; TP006/017/020; history |
| P02-T05 | All six authorized resources reproduce independently authored native field/state/category mappings. Proposed AI remains provisional; approved warned publication and gaps retain explicit labels, publishable score and quality semantics. AutoConfirmed mapping is explicit. | FR45/55; AC17/20; TP017/020; golden |
| P02-T06 | Named-user/service authority is distinct from UI/source-read/share/worker roles. Wrong customer/project/environment/report, inactive identity, stale assignment, customer policy/action/category/field denial prevents protected loads; no business/raw capability. | AC9/14; TP009/014/020; policy/source |
| P02-T07 | Native prohibited values in title/summary/original/comment/provenance/nested recommendations/references never appear in response, error, audit, cursor or trace. Denied content is not fetched for redaction. | AC9; TP009/020; adversarial |
| P02-T08 | Cross-report/customer reference substitution and missing linkage deny under the approved mapping; filtering keeps original publisher digest and hides unauthorized counts. | FR46/55; AC9/20; TP009/020; linkage |
| P02-T09 | Frozen reference availability stays immutable; current evidence expiry/redaction produces authorized unavailable metadata only. No raw resolution/restoration; report expiry/deletion denies the whole read. | FR50; AC9/20; TP009/020; lifecycle |
| P02-T10 | Interleave blocked persisted reads with assignment/category/policy revocation, report deletion/evidence expiry and audit failure on both sides of the approved emission boundary. No stale result or leaked reservation; exact deadline semantics are tested rather than assumed. | AC9; TP009/020; race |
| P02-T11 | Approved audit completion/durability condition, safe outcome/schema/event/correlation, duplicate delivery vs retry and dependency/outage behavior; no exception text or protected value/hash/locator leakage. No content success on unmet audit condition. | AC9; TP009; audit policy; failure |
| P02-T12 | Exact authorized ordering/snapshot paging, foreign/changed scope/report/digest/identity/policy handle denial, expiry/rollback/restart/replica/key/replay behavior and bounded cleanup use approved real cursor semantics. Local numeric defaults are not imported. | FR55; AC20; TP020; boundary |
| P02-T13 | Approved workload/limits minus-one/exact/plus-one, atomic paired admission, denied budget/fairness, Unicode full-envelope byte edges, cancellation/outage/oversize cleanup and unrelated-customer progress. Values/topology must be supplied first. | FR55; TP020; stress |
| P02-T14 | Actual dashboard/Markdown/PDF/link/MCP bind the same supported publication/version/digest. Unavailable licensed renderer or link implementation stays individually NOT VERIFIED; a fictional fixture does not close parity. | FR46; AC20; TP017/020; delivery |
| P02-T15 | Approved actual public method/error/client/token matrix rejects all mutation/raw operations, unknown methods, injected authority/locators and invalid issuer/audience/client/consent/signature/expiry/key states. Requires the later external contract. | FR55/56; AC9/14/20; TP009/014/020; protocol/security |
| P02-T16 | Retention/recovery replay cannot resurrect expired reports/evidence, revoked authority or audit visibility. Non-read-blocking holds create no right and do not rewrite history; blocking state denies. | FR50; AC9; TP009/011/020; ADR0003; recovery |
| P02-T17 | Original fixture version/goldens, historical draft/run/public DTO/migration and host/UI bytes remain unchanged. Reader test host has no mutation/publisher/transport/registration activation; approved dependency/schema changes are exact and reversible. | AC14; TP014/020; source/architecture |
| P02-T18 | Execute pinned restore/format/build, unit/independent persisted integration, applicable security/dependency/CI and supported platform checks; native commands, failures, source/toolchain/fixture hashes and nonauthor findings retained. | Repository completion rules; evidence |

## Test ownership and sequencing

T02 publication atomicity remains with the Phase1C publisher owner. Reader cases execute after native source/mapping/authority/audit contracts are approved. T12/T13 operational and T15 external cases require their exact mechanisms/client inputs; T14 full parity and T16 deployed recovery cannot be marked passed by an isolated reader. The conformance packet must identify applicable cases and preserve every deferred case explicitly; no exception invents policy or scope.

No source/customer/provider/registration operation, test host, integration test or schema migration ran in this documentary cycle. Documentary link/traceability/source-preservation/secret checks and completed prior-cycle hosted observations are recorded separately. Structured label/non-color tests remain applicable; manual Windows/assistive-technology/PDF and performance/recovery acceptance require their own evidence.
