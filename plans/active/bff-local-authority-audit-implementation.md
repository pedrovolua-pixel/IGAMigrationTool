# BFF local authority and atomic audit implementation

Status: COMPLETE — bounded local synthetic implementation
Owner: BFF coordinator
Date: 2026-10-02

## Approval and scope

The repository owner explicitly approved P01–P05 and ADR-0009 for local synthetic implementation against [immutable reviewed packet cacbe37](https://github.com/pedrovolua-pixel/IGAMigrationTool/blob/cacbe372ff8a9bc448033e6918308c5f820b4bfd/docs/development/bff-production-authority-hosting-proposal.md). [Accepted ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md) and the packet define exact internal shapes and tests. Existing feature product/technical/test approval remains the requirement authority; this approval adds the exact internal schema/security boundary only.

No actual user enrollment, Graph HTTP adapter/consent, Azure/SQL grants, production host activation, paid deployment, merge/release, or outage fallback is authorized. The diagnostic Program remains permanently disabled. No new retention/permission policy is selected.

## Work packets

| Packet | Owner / isolated checkout | Paths | Acceptance | State |
|---|---|---|---|---|
| AUTH | production_identity_proposal / /tmp/iga-bff-authority | src/server/modules/IdentityAuthority/**; migrations/identity-authority/**; tests/unit/IdentityAuthority.Tests/**; tests/integration/IdentityAuthority.Tests/** | P01/P02 closed versioned records, exact scope, attributed revision/idempotency, conservative provider publication, direct-role paging and external-origin denial; actual local PG tests | VERIFIED |
| AUDIT | production_hosting_proposal / /tmp/iga-bff-audit | src/server/modules/IdentitySessions/**; migrations/identity-sessions/003-atomic-audit.sql; tests/unit/IdentitySessions.Tests/**; tests/integration/IdentitySessions.Tests/**; tests/integration/SecurityAudit.Tests/** | P03 atomic ticket/revoke/rotate/authority event receipts, stream chain, lifecycle/restore and negative role tests, real PG rollback/concurrency | VERIFIED |
| HOST | coordinator / /tmp/iga-bff-integration | src/server/hosts/BffFoundation/BffHostingContracts.cs; src/server/hosts/BffFoundation/PostgreSqlBffSubjectAuthority.cs; tests/unit/BffHostingContracts.Tests/**; tests/integration/BffHostingContracts.Tests/**; shared project/CI/package config; canonical docs/evidence | P04 one verified hop and canonical host, fixed key/environment config, replica/historical-key/failure seams; P01 opt-in authority-to-BFF projection with external-origin/composite-cutoff binding; no Azure provider attachment | VERIFIED |
| REVIEW | auth_transport / read-only then coordinator-owned verification paths if explicitly assigned | Independent review of AUTH/AUDIT/HOST, integrated proof and applicable regressions | P05 non-author review and real local PG/HTTPS; no gate claim | VERIFIED |

## Shared contract and ordering

AUTH references IdentityPolicy and IdentitySessions; IdentitySessions never references IdentityAuthority. Audited session composition requires `ITransactionalSessionAdmissionPolicy`, using the existing connection/transaction and original authentication time; a second subject-locking connection is forbidden. AUDIT exposes a closed typed `SecurityAuditEventV1` and transaction-bound `PostgreSqlSecurityAudit.AppendAsync(NpgsqlConnection, NpgsqlTransaction, event, cancellationToken)` for AUTH. Writers settle exact constructor/receipt/event fields before dependent use. Each authority mutation and its event/receipt shares one existing PG transaction; AUTH uses the existing subject security_version. Audit append obtains stream head lock after subject/target locks; caller rechecks fresh clock immediately before commit. Schema order: identity-sessions 001,002,003, then identity-authority additive migrations. No startup migration or real grants.

Coordinator owns solution/CI/container allowlists and canonical records. Authors return executed evidence and immutable commits; non-author review precedes completion. Legacy synthetic fixture methods must not be represented as the new restricted production boundary.

## Verification and completion

Map every approved P01–P05 case to executed tests or explicit NOT VERIFIED. Run all applicable pinned restore/audit/format/build/unit/integration/architecture/frontend/infrastructure/security/package checks. Preserve failures and corrections. Update feature status/evidence and parent plan, publish the existing owner-private board and confirm deployment. Actual provider/key/proxy bindings, audit outage preservation, live retention inputs, human gates and production release remain open.

## Execution evidence

Approval recorded in `487f6f7`. HOST initial independent checkpoint `8c697c7` passed59 actual HTTPS/framework Data Protection checks and warning-free build. Coordinator added a60th positive HTTP-backend/TLS-offload check, executed successfully; final integrated review/checks remain pending. Independent review found and corrected a global Origin guard that would have rejected legitimate provider callbacks; actual supported invalid-callback401 and application wrong-Origin403 cases are included. Coordinator integration review found the second-connection subject lock deadlock; AUTH/AUDIT now use a same-transaction interface, awaiting real composed proof. Sandbox NuGet restore failure was resolved by a successful network-enabled audited restore. Initial test-harness SDK analyzer/registration mistakes were corrected before freezing HOST. No full-feature or live result follows.


## Integrated local verification — 2026-10-02

AUTH source `1e5ecb4` plus correction `0a7da148` passed87 unit and120 actual PostgreSQL assertions. AUDIT `64716f6` plus `d9173e9`/`970a1ab` passed69 unit and81 actual PostgreSQL assertions; existing126 PostgreSQL session checks were repeated by its author. Independent corrected-source composition passed258 actual PostgreSQL checks against separate restricted administrator/provider/runtime logins, including five opt-in BFF projection cases. HOST passed60 actual HTTPS/framework Data Protection checks, independently repeated before transitive integration and repeated by the coordinator afterward. The57-project solution passed locked audited restore, whole-solution formatting and Release build with zero warnings/errors. All integrated portable unit suites, architecture boundaries, exact restricted package inputs,14 image-evidence fixtures,254 actual hard-disabled process assertions and whole-checkout secret scan passed. Source changes after those earlier unchanged-component checks are bounded SQL/format/fixture corrections and will also run in configured CI.

Non-author AUTH/AUDIT/bridge reviews closed on the corrected immutable sources. Required direct SQL digest, signed64, writer/action, trusted timestamp and canonical-byte fixes were implemented and executed. The claimed fractional-rounding concern was disproven by actual PostgreSQL text casts; explicit fraction/overflow tests remain. Earlier filtered formatting commands excluded files; whole-solution validation exposed and closed the drift. Independent review and [execution bindings](../../docs/development/evidence/bff-local-authority-audit-20261002.json) distinguish author execution, independently repeated evidence and limits. Full hosted Linux/Windows/container gates and confirmed private-board publication remain the final closure work.


## Combined configured checks

The approved pilot base advanced to `7b2a741` while this work was running. Additive merge `2e97739` preserves both canonical histories and the exact63-project union. Non-author review verifies113 BFF blobs and550 scoped pilot runtime/frontend/test blobs retained with only documented additive BFF frontend commands/ignore differences. The63-project solution passed locked audited restore, whole formatting, warning-free/error-free Release build, every portable unit/architecture suite and Gitleaks.

First hosted Linux run37052401010 passed authority120 but failed the positive exact12-month audit fixture boundary due to seven-fraction canonical timestamp rounding versus Npgsql microsecond parameter truncation. Test-only `a320876` deterministically reproduces and repairs the fixture using the persisted event time, retaining the one-microsecond early refusal, exact positive boundary, holds, purge and restoration assertions. No runtime/SQL/retention policy changed.

Final corrected code `ba8de419f867487abd56d0e6a9c4519131fe37f8` passed [full configured bootstrap37053078322](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37053078322) and [actual package37053078545](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37053078545):Linux,Windows2022/2025, every configured PG/HTTPS/browser/frontend/component/architecture/secret/infrastructure gate and actual nonroot/read-only Docker/process/template/inventory checks. Raw inventories remain ephemeral and do not accept findings/licenses/provenance or promotion. Clean completed worker checkouts were removed with commits/evidence preserved; owned disposable PG clusters stopped, shared cluster untouched. Matching owner-private publication succeeded; the bounded cycle is COMPLETE.


## Confirmed bounded cycle closure — 2026-10-02

All approved local packets and non-author reviews are VERIFIED. The63-project solution and every configured affected hosted check passed on immutable tested code `ba8de419f867487abd56d0e6a9c4519131fe37f8`. The existing owner-private board deployed successfully at `2026-10-02T19:38:18.614757+00:00`, Site source `6c91a022eb2f0fbaca3bcaef483729aece79b9c4`, saved version `appgprj_6abd3ea5f20081918f3f6e81099c2a32~appgver_ca08531ec7b08191b400edc816cd7b98`, native deployment `appgdep_6ac00823db5c8191a6a63debb43e28f2`. It summarizes canonical checkpoint `02dc25b0898e5145802dc288b5e8fad9cb9b58ee`;71 immutable link targets and11task cards were checked, with desktop/mobile/320px reflow and no horizontal overflow. Concurrent Cycle11 content was preserved after a rejected initial push and fresh-source reconciliation. Only the BFF panel/task and snapshot text changed. The BFF local-approval task is closed; its replacement requests protected exact live bindings, named identity/database/network/security/operations reviews and a fresh bounded priced session.

The bounded local cycle is COMPLETE. New manual migration templates are stored as session003 then authority001 after session001/002; none were applied to Azure. No actual enrollment/provider/key/network/SQL grant, production host activation, durable audit-outage fallback, paid deployment, image acceptance, milestone/G1–G9 acceptance or merge/release follows. Earlier pending/initial execution entries above are historical; final exact evidence and unverified inputs remain authoritative. Completed worker checkouts and owned PG clusters are cleaned up; the coordinator checkout is retained for human review.


### Metadata publication checks

Metadata checkpoint02dc25b failed Linux run37054510685 at the secrets step (later Linux steps skipped); Windows and package37054510809 passed. All six locations are exact Git blob SHA256 evidence digests beside `secrets.log` filenames, independently reproduced; they are not credentials. Closure64d497d represents the same223 bindings as separate path/digest records, without a scanner exception. Local whole-checkout Gitleaks passes. Its package37055694798 and Windows checks passed; Linux repeat37055694762 is in progress. GitGuardian historical incident statuses were not suppressed. Final tested runtime ba8de41 and its full passing configured gates remain unchanged.

GitGuardian check110999704920 still flags those six proven historical digests. Their technical/security disposition remains a future pre-merge review item; no check was bypassed, provider incident suppressed or history rewritten. The private board will show that exact review item with its evidence link.


Final owner-private handoff publication succeeded at `2026-10-02T19:50:45.775370+00:00`, Site source `3c4a4f20cfbc699afab0eaf80cc1eeb8d95079b6`, version `appgprj_6abd3ea5f20081918f3f6e81099c2a32~appgver_67de8bf82c28819180ac7c66b6a14e09`, deployment `appgdep_6ac00b0f39c08191b27245c26abc03b4`. It summarizes immutable record4346e0f, with72 immutable source links,11cards and concurrent Cycle11 content preserved. The BFF task includes the exact six proven historical digest alerts for technical/security disposition before any future merge. Current runtime proof and63-project configured checks remain PASS on ba8de41; extra documentation CI on64d497d is still running (Windows/package PASS). No success is claimed for an unfinished repeat or the external GitGuardian historical check. Owned preview tab/server58561 are closed and the viewport restored.
