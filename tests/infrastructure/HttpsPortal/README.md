# HTTPS-P01: disabled environment scaffold checks

This packet implements only the bounded local environment/input portion of [ADR-0011](../../../architecture/decisions/ADR-0011-https-pilot-portal.md), the [approved technical/test addendum](../../../docs/development/https-pilot-portal-proposal.md) and [exact owner approval](../../../docs/development/https-pilot-portal-approval.md). HTTPS-T01 receives local structural and negative-input evidence. HTTPS-T02–T08, G1–G9 and Milestone2 remain **NOT VERIFIED**.

## What is reviewable

The new environment module describes an East US2 **external**, VNet-integrated workload-profiles environment with only Consumption, public network access hard disabled and no log collection/export. It references an existing dedicated delegated subnet and an existing Standard NAT. Direct ARM guards deny wrong region/SKU/NAT attachment, missing or additional delegation, occupied subnet and a platform-managed group equal to the deployment group. It does not create or modify the VNet, subnet, NAT, public IP or protected data services.

The optional root composition has a required `deployEnvironment` parameter whose **only allowed value is false**, and the module condition binds exactly that parameter. There is no default. No app is described or created: no diagnostic image, registry identity, public app ingress, production BFF, live sign-in, proxy address, generated origin, custom domain, edge, TCP route, secret, grant or environment HTTP route is included. Root outputs are absent; the direct environment module exposes only its exact resource ID for inventory. That ID is a proposed name, not evidence of a resource existing.

**This is not a runnable portal.** HTTPS-P02 must complete the independently approved production BFF/provider/key/audit and app composition. Exact generated host/redirect/proxy evidence, identity assignments, abuse controls, private backend proof and the complete paid/public session remain separate prerequisites. The permanently disabled BFF diagnostic image is unchanged. Internal and Bastion templates/history are unchanged. No new runtime dependency, schema, migration, retention, key deletion or user-enrollment policy is introduced.

## Executable local checks

Use verified Bicep **0.47.16**. The commands below compile/lint the prospective module and its permanently inactive composition; they make no Azure calls.

```sh
mkdir -p /tmp/iga-https-template-extract
DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/iga-https-template-extract /tmp/iga-bicep-0.47.16 --version
DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/iga-https-template-extract /tmp/iga-bicep-0.47.16 build infra/bicep/modules/pilot-https-container-apps-environment.bicep --outfile /tmp/iga-https-environment.json
DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/iga-https-template-extract /tmp/iga-bicep-0.47.16 build infra/bicep/pilot-https-portal.bicep --outfile /tmp/iga-https-composition.json
python3 tests/infrastructure/HttpsPortal/template-policy.py /tmp/iga-https-environment.json /tmp/iga-https-composition.json
python3 tests/infrastructure/HttpsPortal/test-inputs.py
```

The compiled policy checks exact resource/name/parameter/property/output scopes, direct network fail guards, stable API pins, public-admission disablement and the nested false-only condition. Unsafe mutation cases cover public admission, internal environment conversion, wrong region/name/subnet, dedicated capacity, telemetry export, extra app/route/edge/grant/data resources, secret/hostname output, deployment-condition/default/parameter bypass and complete deletion mode. Python tests independently exercise unsafe provider/input bindings and the actual value-suppressed CLI, including rejection of the repository's incomplete null example. Synthetic fixtures are test data, not approved names/addresses, Azure observations, capacity evidence or authority.

## Protected input/provider bindings

Keep all three actual inputs outside Git in the owner-only intake. The command prints a generic result and never prints bound values, IDs, IPs, paths or exception text.

```sh
python3 tests/infrastructure/HttpsPortal/validate-inputs.py "$PRIVATE_HTTPS_PARAMETERS" "$PRIVATE_PROVIDER_SNAPSHOT" "$PRIVATE_REVIEW"
```

The ARM parameters object requires precisely `deployEnvironment`, `environmentName`, `virtualNetworkName`, `containerAppsSubnetName`, `natGatewayName` and `infrastructureResourceGroupName`. Every entry is a closed `{ "value": ... }` object; required names cannot be null. `deployEnvironment` must be boolean false. The checked-in example intentionally gives **all values null** and must fail. No hostname, proxy, credentials or activation parameter may be added to this packet.

Private review metadata has exactly `scope`, `bindings`, `reviewRef`, `providerInventoryRef`. `scope` contains the selected immutable `subscriptionId` and `resourceGroupName`; `bindings` matches all composition values. References are opaque links to the actual private review and provider collection. Checking a reference's shape does not prove its existence, authority, freshness or content.

The provider snapshot has exactly these keys:

| Key | Required evidence shape |
|---|---|
| `scope`, `observedAtUtc` | Same selected subscription/group and an attributed UTC observation. No arbitrary freshness interval is invented; actual collection currentness requires operator review. |
| `virtualNetwork` | Observed ID/name/type/location, full address-space and subnet inventory. Exact selected subnet must be unused, private IPv4, contained/disjoint, /27 or larger, delegated solely to Microsoft.App/environments and attached to the exact NAT. |
| `natGateway` | Observed exact ID/name/location, Standard SKU, one attached public IP and no public-IP-prefix fallback. StandardV2 is rejected. |
| `natPublicIp` | Observed exact ID/name/location, Standard Regional SKU, assigned static IPv4 matching the NAT attachment. No IP is inferred or selected by this packet. |
| `managedEnvironmentInventory` | Complete reviewed subscription collection `{ "value": [...], "nextLink": null }`: no target environment overwrite/internal conversion or existing environment use of the selected subnet. |
| `resourceGroupInventory` | Complete reviewed subscription collection `{ "value": [...], "nextLink": null }`: the proposed platform-managed group must be absent and distinct from the deployment group. |

Collections with unfinished pagination are rejected. An empty array in synthetic fixtures is not real absence evidence. Only an actual reviewed complete provider collection can support eventual absence assertions. The module cannot read a nonexistent environment to prove that it is absent; that obligation belongs to the protected provider check/operator. The historical foundation is disposed, so these existing network prerequisites are currently not established by the templates.

[Microsoft's subnet compatibility restrictions](https://learn.microsoft.com/en-us/azure/container-apps/custom-virtual-networks) prohibit overlap with 169.254.0.0/16, 172.30.0.0/16, 172.31.0.0/16, 192.0.2.0/24 and the documented workload-profile reservations 100.100.0.0/17, 100.100.128.0/19, 100.100.160.0/19, 100.100.192.0/19. The input corpus rejects every listed range; no customer/pilot CIDR allocation is invented. The same documentation specifies /27 minimum and Microsoft.App/environments delegation for workload-profiles environments, distinct from the legacy Consumption-only environment's /23/nondelegated requirements. No custom service CIDR is selected. Actual peered/custom-service conflicts, effective routing/DNS/firewall/egress and provider capacity still require complete network review.

## Completion limits

The false-only root guard cannot be turned on through a protected parameter value. Changing it requires a separately approved source change and exact complete priced deployment packet; direct invocation of the environment module is not authorized by successful local checks. A later external environment may create billable platform-managed infrastructure even while public traffic is disabled; no free or complete cost claim follows from this scaffold.

There is no registry/image/private-pull, SQL/provider/key/audit adapter or browser test in this packet because no application exists here. No Azure resource, public endpoint, credential, invitation, user grant, SQL migration or cleanup action executes. Public admission and live sign-in remain disabled by hard property/absence. Private board/shared canonical records and configured integration/CI are coordinator responsibilities; this worker does not publish the site or claim its currency.
