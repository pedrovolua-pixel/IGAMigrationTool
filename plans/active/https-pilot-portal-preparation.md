# HTTPS pilot portal: active preparation

Status: APPROVED LOCAL IMPLEMENTATION — HTTPS-P01 LOCAL VERIFIED; paid/public session and live gates remain separate
Date: 2026-10-03 UTC
Owner: Azure/BFF coordinator
Product/technical basis: approved feature003 and existing identity/session controls; [approved local technical/test addendum](../../docs/development/https-pilot-portal-proposal.md); [Accepted local ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md)

## Owner direction and supersession

“Let’s move to the https instead of bastion” replaces the Bastion customer-browser path and the post-pilot scheduling preference. HTTPS preparation begins now; pilot completion is not claimed. The owner accepted Option A for bounded local disabled templates/tests against `50cf4fd`; no new paid/public session is approved. Retain accepted ADR-0010/local templates and failed automatic quota evidence as historical optional work. Do not pursue the pending separate VM support request or re-request its approval while this direction stands. No VM/Bastion exists to remove.

## Conditional packets and ordering

HTTPS-P01 is approved under [the frozen packet](../../docs/development/https-pilot-portal-approval.md); other unresolved boundaries remain gated. The coordinator owns canonical/shared files and integration; writing workers receive isolated worktrees after exact path/contract approval. Use the approved [parallel workflow](../../docs/development/parallel-agent-workflow.md), one bounded platform worker and one non-author reviewer when work is independent.

| Packet | Owner / scope | Entry condition and completion evidence | State |
|---|---|---|---|
| HTTPS-P00 | Coordinator plus read-only architecture/review worker; ADR/proposal/diagram/current records | Current primary Microsoft research, exact proposal, documentary checks and non-author review; attributable owner direction | VERIFIED — preparation only |
| HTTPS-P01 | Platform writer: proposed `infra/bicep/modules/pilot-https-container-apps-environment.bicep`, `pilot-https-portal.bicep`, protected-input template and `tests/infrastructure/HttpsPortal/`; coordinator owns shared inventory | Exact ADR-0011/local technical/test approval before authoring; fixed separate external environment, disabled public app ingress/live sign-in, private backends and exact protected inputs; pinned compile/lint/format and negative/composition evidence | LOCAL VERIFIED — environment/input subset; no app or live proof |
| HTTPS-P02 | Coordinator + separately scoped BFF implementation workers | Approved production provider/key/audit contracts and isolated packets; actual production composition, SQL role negatives, shared keys, provider observation/federation and durable audit-outage proof; reviewed image/private build/signing acceptance | BLOCKED — contracts/live prerequisites |
| HTTPS-P03 | Coordinator/platform/identity/operations, protected inputs only | Full fresh price/spend/capacity/what-if, fixed generated origin/proxy/private paths, users/role/CA/federation proof, exact public synthetic session and preservation/disposal authorization | NOT READY |
| HTTPS-P04 | Non-author verifier + coordinator, approved actual environment | HTTPS-T01–T08 actual evidence, affected configured checks/CI and safe closure; distinguish engineering results from G1–G9/human acceptance | NOT VERIFIED |
| HTTPS-P05 | Named human release/security/operations owners | Existing pilot/release gates and explicit production/customer-evidence release approval | NOT VERIFIED |

## Immediate human tasks

| Task | Requested role | Source / completion |
|---|---|---|
| Direct managed HTTPS local topology | Repository technical/security owner — CLOSED | [Exact approval](../../docs/development/https-pilot-portal-approval.md): Option A local disabled templates/tests accepted against50cf4fd; no paid/public grant |
| Complete exact user admission | Identity/product owner | [Protected binding template](../../docs/development/bff-production-bindings-template.json), [approved role matrix](../../docs/security/health-assessment-authorization-matrix.md): immutable organizational/B2B proof, selected customer role/scope and CA/licensing; no email-only authority |
| Resolve production audit/provider/key authority | Technical/security/platform owners | [Existing production proposal](../../docs/development/bff-production-authority-hosting-proposal.md): real adapter contracts, durable audit-outage preservation, scoped key/SQL/provider evidence |
| Review exact Azure/public session | Repository/operations owner, after local preparation | [HTTPS proposal](../../docs/development/https-pilot-portal-proposal.md#cost-and-operations): current complete quote, full reserve under allowance, exact resources/ownership/public boundary and preservation/disposal |
| Publish approved local packet to GitHub — CLOSED | Repository owner | [Publication dependency](#publication-dependencies): Exact owner public publication approval and successful upload of af31cba confirmed; fresh CI tracked separately |
| Restore private board publication | Repository owner | [Site maintenance](../../docs/development/pilot-status-site.md): separately rejected publisher credential/network action still needs its exact authorization and confirmed deployment |

Bsv2 support authorization, VM quota/image/disk/browser/RDP/desktop egress tasks are **SUPERSEDED for the active path**. Do not mark their technical results successful. Keep original evidence and publisher block separate. User grants, network exposure and production release remain disabled.

## Verification and completion limits

P00 has no runtime or IaC changes; applicable checks are source/link/JSON/SVG validity, diagram render/visual inspection, protected-identifier/secret scan, diff checks and independent scope review. P01–P04 require the full affected checks listed in the proposed test packet when authorized and executed. Planning cannot satisfy a runtime gate or repair historical CI failures.

Canonical feature implementation/status/evidence records are updated in this cycle. Private board publication remains unavailable: last confirmed BFF snapshot `4346e0f` predates these changes; preserve unrelated later pilot publications. Report the stale BFF/access summary, not a current site.

## Executed preparation checkpoint

The coordinator rendered and visually inspected the new diagram at3200×2000, validated source/link/JSON/SVG/heading targets and six unmodified official icon embeds, ran added-prose protected-identifier checks, whole-checkout Gitleaks and `git diff --check`. Non-author final review passed with no remaining material findings after adding ADR-0010's current-direction notice and the evidence record. [Sanitized evidence](../../docs/development/evidence/https-pilot-portal-preparation-20261003.json) binds exact changed sources and check results. No runtime/IaC change occurred; runtime/live cases and gates remain NOT VERIFIED. This is the historical preparation checkpoint; the owner subsequently accepted the local packet as recorded below.

## Approved execution packet — 2026-10-03 UTC

The repository owner explicitly approved the reviewed request. [Approval and exact source/path ownership](../../docs/development/https-pilot-portal-approval.md), [original-source metadata](../../docs/development/evidence/https-pilot-portal-approval-20261003.json) and the approved local tests close the local decision dependency. HTTPS-P01 completed its bounded environment/input scaffold with non-author review. Public admission/live sign-in/grants/spending remain disabled; other contracts and G1–G9/Milestone2 are unchanged.


## HTTPS-P01 executed local closure

The environment-only packet is stored and independently reviewed at worker `e796c36`, integrated as `6690955`. Pinned Bicep0.47.16 build/lint passed for all23 templates with zero diagnostics; both new templates match formatter output. New tests passed38 compiled unsafe mutations,50 invalid provider/input cases and51 actual CLI denials with values suppressed. The reviewer independently repeated these checks and denied four additional probes. The root remains false-only; no app, identity, secret, grant or public admission is present.

Combined74-project locked restore, formatting and Release build passed with zero build warnings/errors. All28 unit/architecture projects and three portable integration projects passed, including308 actual synthetic HTTPS transport checks and60 local hosting/key-seam checks. Thirteen configured existing infrastructure policy commands passed. These runs do not re-execute PostgreSQL integration, frontend/browser end-to-end, Windows collector or container-package checks; fresh hosted CI is unavailable while upload is blocked. Historical results remain source-bound and are not relabeled as current combined execution. HTTPS-T01 has local environment/input evidence only; HTTPS-T02–T08, G1–G9/Milestone2 remain NOT VERIFIED.

[Execution metadata](../../docs/development/evidence/https-pilot-portal-implementation-20261003.json) records source bindings and limits. [HTTPS-P02A handoff](../../docs/development/https-pilot-portal-next-packet.md) is preparation for the next separately approved production specification/test packet, not runtime implementation authority.

### Publication dependencies

Automatic approval review rejected uploading the combined source to GitHub because destination ownership/privacy and exact export authorization were not established. Subsequent read-only metadata confirms the signed-in account owns/administers the existing repository, whose visibility is **public**. The approved local source is committed in the isolated coordinator checkout; uploading it to the existing draft branch requires explicit owner approval for public publication. Do not bypass the rejection through a different publisher. No new hosted CI run or remote PR update is claimed.

Private status-board publication remains separately blocked by the earlier rejected credential/network publisher action. Its last confirmed BFF/access snapshot4346e0f predates this local packet; preserve unrelated later Cycle13 publications. The board is stale for HTTPS approval/implementation. Prepared board delta: close local topology approval; mark HTTPS-P01 local environment/input verified; keep production BFF/audit/provider/key, exact user/role/CA proof, ingress evidence, fresh priced public session and publisher authorization open. The canonical repository remains authoritative.


## Approved public source publication — 2026-10-03 UTC

The owner explicitly approved uploading reviewed `af31cba` to the existing owned **public** GitHub repository and updating draft PR2. The exact commit was pushed successfully without force; the previous automatic-review export block is closed. [Publication metadata](../../docs/development/evidence/https-pilot-portal-publication-20261003.json) preserves the prior rejection and attributable approval. Earlier blocked-publication statements above describe their original checkpoints.

The public target advanced to `cf8b320` with17 Cycle14 documentary changes only. Its three canonical append conflicts were resolved additively in `3fec237`, preserving both histories. Approved application/infrastructure/test/project/workflow source is unchanged. Fresh hosted CI is tracked separately; unfinished or absent jobs are not passing evidence. No target merge, release, paid Azure session, public application, login, enrollment, grants or migration follows from source publication.

The private BFF/HTTPS board remains stale under its separate rejected publisher credential/network action. Public GitHub approval does not authorize that private publisher or change its audience. Preserve the independently confirmed Cycle14 board update.


## Fresh hosted HTTPS verification — 2026-10-03 UTC

Public source `33f4b47` triggered [bootstrap37090777603](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37090777603) and [package37090777612](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37090777612). Both package jobs and Windows2022/2025 passed. Linux bootstrap failed at the first synthetic audited BFF composition ticket issuance after120 authority and81 audit assertions passed. Later PostgreSQL/session/frontend/browser/infrastructure steps were skipped and are NOT VERIFIED by this run. [CI record](../../docs/development/evidence/https-pilot-portal-ci-20261003.json) retains exact source/job evidence and confirmed fixture diagnosis and pending hosted rerun. No fresh complete CI success is claimed; prior local checks retain their actual limited scope. Public admission/login and all live gates remain disabled/unverified. The private BFF/HTTPS board remains stale under its separate publisher block.


### Synthetic precision repair verified locally

Worker `31bb30b` reproduced the original failure with a nine-tick frozen timestamp; PostgreSQL rounded provider state one tick into its future and the production guard correctly refused issuance. The fixture now captures wall time at PostgreSQL microsecond precision while retaining deliberate raw-tick injection. Nine real provider-publication remainder cases prove rounded-future denials and separate aligned successful issuance across stores. All prior cutoff/expiry/lock-contention tests remain present. Author643/644 and non-author644 real PostgreSQL assertions passed; the half-microsecond conditional assertion explains the one-count variation. Locked restore/Release build/format/diff passed; no production code, SQL, dependency, permission or retention policy changed. Hosted rerun remains separately pending. Original failure evidence is retained in the CI record.


## Hosted fixture repair closure — 2026-10-03 UTC

Exact published test source `ba2f44740fc96eacf98f1635d64574633de35e47` passed [bootstrap37091900841](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37091900841) and [package37091900865](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37091900865). Linux completed all configured build/unit/integration/PostgreSQL/browser/dependency/infrastructure checks, including disabled HTTPS validation; the repaired actual PostgreSQL composition passed643 assertions (the documented half-microsecond conditional explains643/644). Authority120, audit81 and real HTTPS/shared PostgreSQL84 passed. Windows2022/2025 and both package jobs passed. This supersedes the pending hosted-rerun state above; the original33f4b47 failure remains historical evidence.

The source-bound [CI record](../../docs/development/evidence/https-pilot-portal-ci-20261003.json) retains both checkpoints. Later receipt-only records do not imply their own CI execution; production/runtime/test/workflow content must remain identical to ba2f447. Configured workflows remain partial pilot gates: no Azure operation, public portal/login, grant, live provider/CA/federation proof, image promotion or production release occurred. Historical GitGuardian findings still need human disposition before merge; the separate private BFF/HTTPS board publisher block remains and unrelated confirmed Cycle14 publication is preserved. The production dependency specification/test packet remains the next engineering action.
