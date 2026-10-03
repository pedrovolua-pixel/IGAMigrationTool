# HTTPS-P02A: production dependency closure handoff

Status: PREPARATION ONLY — implementation and live activation not approved
Date: 2026-10-03 UTC
Basis: [accepted local ADR-0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md), [active HTTPS plan](../../plans/active/https-pilot-portal-preparation.md) and [production authority/hosting proposal](bff-production-authority-hosting-proposal.md).

HTTPS-P01 supplies an inactive environment scaffold. It creates no application and cannot establish a customer portal. The existing diagnostic BFF executable permanently refuses activation. Complete the exact production specification, implementation and test packet before adding the production executable or adapters. This handoff introduces no new permission, route, dependency, policy or deployment authorization.

## Required reviewed contracts

| Dependency | Existing implementation | Information or decision needed / completion |
|---|---|---|
| Production host | [Diagnostic executable](../../src/server/hosts/BffDevelopmentHost/Program.cs) and [BFF registration](../../src/server/hosts/BffFoundation/BffRegistration.cs) | Technical owner: separate production composition using the existing authoritative stores; approved resource/action/customer policy adapter and privileged verifier contracts. Admission alone must not grant resource permission. |
| Provider authority | [Synthetic reader](../../src/server/modules/IdentityAuthority/SyntheticProviderReader.cs) and closed publication/session contracts | Identity/security owner: exact Graph properties, direct roles, permissions, complete pagination, replication and external organizational home-state evidence. Real HTTP/token adapter requires an approved packet; synthetic data cannot establish tenant authority. |
| Shared keys | [Hosting contracts](../../src/server/hosts/BffFoundation/BffHostingContracts.cs) | Platform/security owner: exact dedicated Blob/key/managed identity scopes, proposed Microsoft SDK dependencies, conditional writes, historical unwrap and recovery tests. Local synthetic key seams do not prove Azure SDK behavior. |
| Authentication audit | [Registration failure hooks](../../src/server/hosts/BffFoundation/BffRegistration.cs) and [approved audit policy](../security/health-assessment-audit-policy.md) | Technical/security owner: available-store denial/failure audit composition; separately review durable preservation/reconciliation during audit-store outages. Returning401 and ordinary logs do not satisfy durable audit policy. Do not select a fallback or retention amendment implicitly. |
| Ingress trust | [Exact peer and canonical host contract](../../src/server/hosts/BffFoundation/BffHostingContracts.cs) | Platform/security owner: actual ACA peer/header evidence, benign and injected chain tests, and exact reviewed amendment if needed. Current default rejects all forwarded client addresses; enabled mode accepts one address and one exact immediate peer. No subnet-wide trust, blanket forwarded-header switch or inferred proxy address. |
| Recovery and images | Existing lifecycle/tombstones and [image package handoff](azure-application-package.md) | Operations/security owner: actual restore, independent retained witness, lifecycle authorization, accepted immutable image/provenance and private build/pull path. |

## Protected and live prerequisites

Use the [production binding template](bff-production-bindings-template.json) in the owner-only intake for exact tenant/app/role/user/customer, federation, CA/licensing, SQL, key, canonical origin/redirect and proxy bindings. Selected email addresses are requests, not identity or home-organization proof. Keep protected identifiers and evidence outside Git and the status board.

After local production contracts and implementation are reviewed, prepare the complete refreshed Azure quote and exact bounded public synthetic session. Include all network/platform-managed, registry, database, storage, backup and private-endpoint costs. The historical disposable session approval does not authorize this topology. Public exposure, paid resources, user invitations/enrollment, provider consent, SQL/key grants and production release need their separate exact authorization. HTTPS-T02–T08, G1–G9 and Milestone2 remain NOT VERIFIED.

The immediate next engineering action is to prepare the production dependency specifications and test packet for human review. Do not turn on the environment-only template, promote the diagnostic image, or grant access from this document.
