# Bastion B2s v2 amendment and quota request

Status: SUPERSEDED FOR ACTIVE ACCESS — historical evidence retained; HTTPS preparation replaces the desktop path
Date: 2026-10-03 UTC
Owner: Azure/BFF coordinator

## Attributed owner decision

The repository owner answered “Do it” immediately after the exact reviewed [preflight proposal](bastion-deployment-session-preflight.md), commit `40f7306ffdb6787068419b385609f8453efb9cad`, and confirmation request: change the private development desktop from `Standard_B2s` to `Standard_B2s_v2` (2 vCPU/8 GiB; Windows compute USD 0.0924/hour, USD 2.2176 per 24 hours) and submit a 2-vCPU `standardBsv2Family` quota request to Microsoft Azure for the owner-selected Subscription 1, East US2.

This approves that bounded ADR-0010/template amendment and that request only. It does not approve paid provisioning, broader access, a paid support plan, guest credentials or public exposure. Provider approval and actual nonzonal allocation remain unverified. The original preflight and its pending checkpoint remain historical evidence. All image/network/BFF/key/audit/identity/complete-cost prerequisites remain open.

## Execution packet

| Packet | Owner and exact paths | Checks and closure |
|---|---|---|
| VM amendment | Isolated platform worker: `infra/bicep/modules/pilot-development-private-browser-access.bicep`, `tests/infrastructure/PrivateBrowserAccess/template-policy.py`, `tests/infrastructure/PrivateBrowserAccess/README.md` only | Fix the approved SKU to B2s v2; reject the former B2s and other unsafe SKU mutations; pinned Bicep 0.47.16 build/lint, structural and input negatives. No fallback or other configuration change. |
| Azure request | Coordinator in the selected Azure portal subscription | Request limit 2 for exact `standardBsv2Family`, East US2; preserve protected request/reference outside Git. Read back submitted/approved/pending/failed state. Do not purchase support or assert capacity. |
| Integration and records | Coordinator owns ADR-0010, access handoff/preflight, plans/status/evidence and shared composition | Review the immutable worker change independently, integrate, build/lint affected composition, run affected policies, JSON/local links, diff and whole-checkout secrets. Publish to existing draft PR; no merge or promotion. |

Requirement mapping: ADR-0010, PA01/PA03, PA-T01/PA-T08, IP-HAS-002, G1/G3/G7. Existing exact template boundary remains: private static NIC, no public VM IP, Developer only, explicit protected network inputs, single-writer 128 GiB E10 LRS candidate disk, no role/extension/credential output. The approval does not establish exact-image disk compatibility or guest/browser readiness.

## Lifecycle and reporting

Workers use isolated Git worktrees from `40f7306`; coordinator owns canonical records and cloud actions. Non-author review is engineering evidence, not human gate acceptance. Preserve the clean integrated worker commit before removing its temporary checkout. Record executed source/checks/request result here and in the canonical evidence index before closure.

The existing private board remains stale for BFF/access updates because automatic approval review rejected the official publishing helper credential/network action for bypassing the managed network proxy. Its separately requested approval remains pending. Do not retry through an indirect path. Canonical records remain authoritative; no publication is claimed.


## Executed checkpoint

The isolated worker commit `f321f81a7cdb0d31d5aafb28fd1c6e177b9d814b` was independently reviewed by `auth_transport` and integrated as `a5093d673a3b20873ce2d96760d86eda7c2ae8cd`. The only module change fixes the VM size to B2s v2. The policy explicitly rejects the former B2s; all other template and input boundaries remain unchanged.

Coordinator executed pinned Bicep 0.47.16 format comparison and build/lint of the module, DNS and optional composition with zero diagnostics. Positive baselines, 50 workstation mutations, 48 input negatives, 16 DNS mutations, 16 composition mutations, 19 provider-metadata negatives and 17 actual combined CLI denials passed (166 negative cases). Independent worker review separately reran module build/lint, 50 mutations and 48 input negatives. These are local structural checks; no live allocation or access proof follows.

The authorized Azure Quotas request was submitted for exactly 2 vCPUs in the selected Bsv2 family/region/subscription. Azure returned Unsuccessful (one request; zero successful/partial), and the refreshed quota remains 0 used / 0 limit. The next [prepared support action](bastion-bsv2-support-request.md) was blocked by automatic approval review because it is a separate external support workflow requiring explicit authorization. No ticket was opened or sent; no VM, role grant, credentials or paid resource was created. The provider failure is preserved as FAIL, not partial capacity acceptance.

[Sanitized execution evidence](evidence/bastion-b2sv2-amendment-20261003.json) binds exact sources, checks, provider result and remaining human tasks. Historical preflight and earlier hosted baseline results remain preserved. New hosted checks are recorded against the published exact source when available; no pending run is claimed PASS.


### Prior preflight hosted result

On exact source `40f7306`, [Azure package 37086070370](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37086070370) passed; [bootstrap 37086070371](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37086070371) failed the Linux fictional fix-package browser step with `keyboard-fragment-navigation` / `AssertionError` after 2811 checks. Later infrastructure/Bastion steps were skipped; both Windows collector jobs passed. This remains a failed hosted result, separate from the executed local amendment checks. The unrelated browser implementation was not modified, no root cause or flaky-test diagnosis is asserted, and no new hosted pass is claimed before execution.

## Owner switches the active access path to HTTPS — 2026-10-03 UTC

The repository owner now requests “Let’s move to the https instead of bastion.” The [active HTTPS preparation plan](../../plans/active/https-pilot-portal-preparation.md), [Proposed ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [technical/test proposal](https-pilot-portal-proposal.md) replace the Bastion browser path and post-pilot timing preference. Prior template approvals and failed quota evidence remain historical. VM quota/support/image/desktop/RDP prerequisites are SUPERSEDED for this path, not technically resolved. No VM/Bastion was deployed or removed; no support ticket is pursued.

The recommended candidate is a new external VNet-integrated Consumption-profile Container Apps environment exposing only the portal/BFF through built-in HTTPS, with Entra organizational login and private protected services. Exact architecture/local disabled-template approval, real BFF/provider/key/audit composition, reviewed users/roles/CA, full priced public session and live verification remain required. No public ingress, resource, invitation, grant, migration, live sign-in or new spending was performed. G1–G9/Milestone2 remain NOT VERIFIED. Private-board publication remains blocked by the separately rejected publisher credential/network action; its BFF/access snapshot is stale. Historical sections below or above retain their original time-bound state.
