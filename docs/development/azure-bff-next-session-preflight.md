# Next private Azure BFF session: preflight

Status: READ-ONLY PREFLIGHT COMPLETE — live deployment not ready; no new paid session authorized or started
Date: 2026-10-02
Owner: Azure/platform coordinator; repository owner approves exact session and access
Related: [BFF execution plan](../../plans/active/bff-azure-deployment-preparation.md), [authority/audit handoff](bff-local-authority-audit-handoff.md), [binding intake](bff-production-bindings-template.json), [application package](azure-application-package.md), [previous disposable session](azure-foundation-session-proposal.md), [sanitized preflight evidence](evidence/azure-bff-next-preflight-20261002.json)

## Refreshed read-only Azure observations

Authenticated portal readbacks on 2026-10-02 UTC show the owner-selected Subscription 1, October accumulated **subscription** cost USD0.35 and forecast USD5.09. Pilot-group accumulated cost and its USD50 monthly budget evaluated spend both show **USD0.22**. The separate aggregate pilot subscription budget remains USD50 monthly but evaluated spend shows USD0.00. Evaluation and usage ingestion are delayed; subscription totals also include unrelated workloads. These readings are not a final invoice, available allowance, hard spending cap or authorization to reserve another USD25. No budget or unrelated resource was changed.

The pilot group shows **zero resources**, with Type and Location set to all and empty search. The platform-managed Container Apps group is absent from the displayed subscription scope list. This verifies the visible inventory, not every provider soft-delete state. Historical vault-name recovery must still be reviewed before reuse.

The prior USD25/24-hour authorization covered the disposed synthetic session and is not renewed. Regional capacity, quota and exact deployment what-if remain **NOT VERIFIED** because the production composition and protected parameters do not yet exist.

## Current public rates and budget constraint

The [Microsoft Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices) was queried on 2026-10-02 at 21:02 UTC for East US 2 Consumption rates in USD. Exact candidate meters and their effective dates are bound in the sanitized evidence.

| Candidate meter | Public retail rate | Pricing limit |
|---|---:|---|
| ACR Premium Registry Unit | USD1.6666/day | October31-day registry alone = USD51.6646; no replication selected |
| PostgreSQL Flexible Server B1MS compute | USD0.017/hour | Compute only; storage/backup/operations are separate |
| PostgreSQL Flexible Server LRS backup | USD0.095/GB-month | Billable quantity depends on actual included backup/usage |
| Standard IPv4 static public IP | USD0.005/hour | Candidate foundation NAT IP; does not price NAT processing |
| Container Apps Standard active vCPU | USD0.000024/vCPU-second | Quantity, free grants and billing offer must be bound |
| Container Apps Standard active memory | USD0.000003/GiB-second | Quantity, free grants and billing offer must be bound |
| Service Bus Standard base | USD0.013441/hour or USD10/month catalog rows | Resolve applicable billing meter; never add both |

This is a partial rate refresh, **not a complete deployment quote**. Networking/private endpoints/DNS, database and Blob storage, key operations, monitoring, private builder, image publication and charge-lag reserve still require exact quantities and offer checks. No always-on full foundation fits the USD50 allowance with the currently selected private registry. Do not replace private networking or lower a SKU to bypass the accepted architecture. Form an exact bounded session only after the remaining bindings exist. [Azure budgets alert rather than stop resources](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets).

## What is verified and what must be composed

D01/D02 local authentication and P01–P05 local authority/audit are approved and COMPLETE. Accepted ADR-0009 and the [local handoff](bff-local-authority-audit-handoff.md) define their exact limits. Independent read-only review of `d51349a0bedc5299f990ba2b759dc4bd3eb29242` confirmed these production gaps:

| Area | Stored implementation/template | Remaining executable prerequisite |
|---|---|---|
| BFF process | Permanently disabled diagnostic host; reviewed reusable local BFF components | Separately reviewed production composition with real adapters and exact protected configuration; current container cannot activate |
| Azure app deployment | `infra/bicep/modules/pilot-development-bootstrap-apps.bicep` launches bootstrap | BFF-specific module, exact identity/runtime inputs and access scopes; bootstrap is not the BFF deployment |
| Provider authority | Synthetic provider/direct-role/home-origin contracts and denial tests | Actual supported provider reader, least-permission property/role/home proofs and named identity review |
| Shared keys | Reviewed configuration seams and HTTPS/framework tests | Exactly pinned SDK adapters, conditional-write/conflict/recovery proof, dedicated private Blob ring and wrapping key |
| Browser path | One-hop ingress contract accepts one exact IP | Reviewed private reachability, DNS/certificate/fixed HTTPS origin and actual exact trusted proxy peer; CIDR input is unsupported |
| Database and audit | Manual session001/002/003 then authority001 templates | Exact migration/administration/runtime/function scopes and reviewed outage preservation/lifecycle/restore bindings |
| Entra | Inactive application/federation templates | Named licensed users, actual role mapping/CA/authentication evidence, reviewed exact dedicated trust; no live grants yet |
| Image | Disabled image and actual local inventory checks | Published manifest digest, accepted vulnerability/license policy, signed retained provenance, private build/push/pull and restricted evidence |

Foundation empty evidence storage/vault are not dedicated BFF ring/key resources. Earlier seven-day synthetic backup/soft-delete and no-purge choices do not define production retention or recovery policy. No actual lifecycle/access decision is inferred from local approval.

Final metadata source `d51349a` now passed configured [bootstrap37056853245](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37056853245) and [package37056853227](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37056853227). Earlier pending documentation CI is historical. This does not dispose the six historical GitGuardian digest alerts or pass live G1/Milestone2.

## Ordered next execution

1. **Protected intake:** the owner supplied a private local destination and two organizational account requests, recorded as `LOCAL-INTAKE-20261002-01`. The customer detailed role and actual directory/licensing/CA proof remain unresolved. The personal external request is superseded by an organizational account request, and private hosting is retained. Work/school home-origin and detailed customer role remain unverified. [The private browser access proposal](../../plans/active/private-pilot-browser-access-preparation.md) recommends an owner-operated Bastion Developer test desktop; Proposed ADR-0010 and exact security/network/price decisions precede templates or access. Earlier [consumer/public options](bff-browser-identity-options.md) are historical. The protected binding template remains incomplete; every null is unresolved, and actual identifiers, addresses and user details stay out of Git and the board.
2. **Concrete composition packet:** use accepted local contracts to prepare a separately bounded technical/implementation/test packet for production host, supported provider/key adapters, BFF-specific Bicep and manual SQL identities. Map to IP-HAS-001/002/003, FR-HAS-13–15/29/32/45–46/55–56, SEC-PILOT-001/002/008/009 and TP-HAS-009/014/020. Resolve missing production choices through attributed review before implementation.
3. **Named reviews:** identity/platform, network/database, technical/security and operations owners review exact CA/licensing/role/home proof, proxy/key scopes, audit-outage preservation and key/authority/receipt/backup lifecycle. Incident and enrollment owners must be attributable.
4. **Artifact and deployment preflight:** build/review the concrete host, bind the registry manifest and accepted signed retained evidence, then refresh delayed charges, quota/capacity, all applicable rates and exact provider validate/what-if. Specify duration, maximum additional charge, reserve and cleanup inventory within the remaining allowance.
5. **Concrete session approval and execution:** obtain exact priced session and required access approval; run private build/push/pull/startup/provider plus negative trust/network/revocation/two-replica/recovery tests. Keep live sign-in disabled until actual federation and deployment evidence are accepted. Preserve required sanitized and restricted evidence before disposing only the approved session resources.

A local test, successful deployment or board publication does not authorize production release, draft merge, G1 or Milestone2. No Graph permission, federation grant, app-role assignment, database administrator, key grant, public ingress, resource or retention default was changed during this preflight.

## Status publication

The private board update is blocked: automatic approval review rejected sending the official Sites helper's short-lived repository credential through an unsandboxed terminal because it bypasses the managed network proxy. Exact approval was requested. The last confirmed BFF snapshot is `4346e0f`, published at 2026-10-02T19:50:45.775370+00:00; it does not include this preflight. The canonical repository record remains authoritative. No site sharing or access changed.
