# HTTPS pilot portal: local approval and implementation packet

Status: APPROVED — bounded local HTTPS-P01 templates/tests only
Date: 2026-10-03 UTC
Approved by: Repository owner acting as technical/security owner, explicit “Approved” response to the reviewed ADR-0011 Option A request in the active session
Approved immutable source: `50cf4fdfdabf83fd9aa5e92db7196ae26f5437fc`

## Exact approved scope

A new VNet-integrated external Azure Container Apps workload-profiles environment with only the Consumption profile; one future HTTPS portal on its generated Azure hostname; the existing organizational Entra/B2B, MFA/CA, server session and product authorization policy; private database/storage/key/registry services. Implement separate optional repository templates, protected incomplete inputs and local compiled-template/input/composition tests. Public network admission, app public ingress and live sign-in remain disabled. Preserve the internal/Bastion templates and historical evidence. No new dependencies, product routes, grants, paid deployment, audit/key policy, production activation or user enrollment are approved.

The real production BFF remains a separately gated implementation. Any diagnostic app shell must be named/documented as permanently disabled, preserve its explicit disabled command, contain no real identity/SQL/secret configuration and never claim product readiness. A protected input template cannot be deployable while required proof or runtime composition is absent. Unknown canonical origin/proxy/provider/key/audit bindings remain unresolved, not invented.

## Frozen local work packet

| Role | Checkout / paths | Contract and evidence |
|---|---|---|
| Platform author | Isolated `/tmp/iga-https-template-worker`, branch `codex/https-template-worker`; owns only `infra/bicep/modules/pilot-https-container-apps-environment.bicep`, `infra/bicep/pilot-https-portal.bicep`, `infra/bicep/environments/pilot-dev-https-portal.parameters.example.json`, and new `tests/infrastructure/HttpsPortal/` files | HTTPS-T01; G1 configuration only. New external environment with public access hard disabled, reviewed existing dedicated delegated/NAT subnet and explicit managed group. Closed composition; no private-service changes, grants, routes/TCP/custom domains/telemetry exports, broad proxy trust or live activation. Existing resource metadata must be bound through a value-suppressed local preflight; examples have nulls. Compile/lint/format with Bicep0.47.16; real unsafe mutation/input tests; report exact paths/commit and limits. |
| Non-author reviewer | Read-only isolated author source, then coordinator source; no writes or Azure | Independently execute compiled and input/composition denial policies; review actual ARM conditions/references and BFF diagnostic constraints against approved packet. No human gate acceptance. |
| Coordinator | `/tmp/iga-bff-integration`; shared inventory/README/workflow, integration, canonical plans/spec status/evidence and existing private board | Integrate reviewed bounded author commit, run all affected infrastructure checks, source/link/secret checks and configured CI if available; preserve unrelated pilot work. No workaround for previously rejected board publisher. |

## Approval boundaries

Exact paid/public session, what-if/capacity, user/role/CA/federation/provider/SQL grants, deployed proxy/header proof, key/audit preservation and production/customer-evidence release remain separate. G1–G9/Milestone2 are NOT VERIFIED. The current USD50 alert budget is unchanged and is not a hard cap.

## Source binding

Original approved SHA256 values are in [approval metadata](evidence/https-pilot-portal-approval-20261003.json). This attribution is human decision evidence, not an executed runtime test or signed gate bundle.
