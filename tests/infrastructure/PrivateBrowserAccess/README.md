# Private browser access local checks

PA01 implements the bounded local template packet under [ADR-0010](../../../architecture/decisions/ADR-0010-private-pilot-browser-access.md) and its [approved implementation/test plan](../../../plans/active/private-pilot-browser-access-preparation.md). It creates no Azure resources during these checks. PA-T01/PA-T03/PA-T04/PA-T08 receive local structural and negative-input evidence only; every provider, browser, credential, identity, effective-network and disposal case remains **NOT VERIFIED**.

## Exact local checks

Use the repository-pinned Bicep **0.47.16**. Replace the executable path with the matching verified installation when required. The extraction/output directories are temporary, outside the repository.

```sh
mkdir -p /tmp/iga-bicep-worker-extract
DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/iga-bicep-worker-extract /tmp/iga-bicep-0.47.16 --version
DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/iga-bicep-worker-extract /tmp/iga-bicep-0.47.16 build infra/bicep/modules/pilot-development-private-browser-access.bicep --outfile /tmp/iga-private-browser-module.json
python3 tests/infrastructure/PrivateBrowserAccess/template-policy.py /tmp/iga-private-browser-module.json
python3 tests/infrastructure/PrivateBrowserAccess/test-inputs.py
```

The compiled-template policy checks the exact closed resource set, East US 2, Developer same-VNet attachment, one private static NIC, dedicated nondelegated subnet and existing NAT/NSG attachment, Standard_B2s_v2, official image publisher/offer/SKU with an explicit version, 128GiB StandardSSD_LRS single-writer OS disk, secure required administrator parameter, no extensions/grants/identity/public IP, and resource-ID-only inventory outputs. It rejects unsafe mutations, including the formerly selected Standard_B2s and other compute SKUs. Typed sealed egress inputs compile to ARM languageVersion2.0 with symbolic resource names; the checker supports that shape explicitly.

The owner-approved amendment on2026-10-03 fixes the candidate to Standard_B2s_v2 (2vCPU/8GiB); it supplies no fallback. The coordinator owns the exact standardBsv2Family quota request, provider readback and canonical approval/cost records. This local template change does not establish quota approval, allocation capacity, image/disk/browser readiness or a complete paid-session approval. Every other workstation/Bastion boundary remains unchanged.

The synthetic input tests contain RFC1918/documentation addresses and a nonexistent example image version. They are test fixtures, **never recommended deployment bindings or proof that an official image/version exists**. No fixture file includes a password or real identity. No dependencies beyond Python's standard library are added.

## Mandatory protected input check

Before Azure validation/what-if/deployment, run the validator on owner-only files outside Git. Invoke this check again whenever addresses, reviewed destinations, image version, scope or environment metadata change. The secure password must be **absent** from the file: the owner supplies it through a separately reviewed secure runtime handoff. Missing, null, empty, plaintext or vault-reference password entries are rejected if present. The Bicep module itself still requires a secure `adminPassword` with no default; the validator does not create/retrieve that credential.

```sh
python3 tests/infrastructure/PrivateBrowserAccess/validate-inputs.py "$PRIVATE_MODULE_PARAMETERS" "$PRIVATE_NETWORK_REVIEW"
```

The first file is an ARM parameters object with a `parameters` member. Each noncredential module input is a closed `{ "value": ... }` object. All inputs are required, including exact source/ILB/NIC IPv4 hosts and the official image version; there are no deployable defaults. The root composition must bind its environment-derived ILB to the same reviewed `appIlbAddress`; adaptation to that composition belongs to the coordinator.

The second file has exactly these fields:

| Field | Required binding |
|---|---|
| `reviewRef`, `credentialHandoffRef`, `networkEvidenceRefs` | Opaque references to the actual private review/owner handoff and network evidence. A reference's shape is checked; its existence, freshness and provider content require human/operator verification. |
| `scope` | Exact mapping of `existingVnetName`, `existingNatGatewayName`, `workstationSubnetName`, `workstationVmName`, `workstationNicName`, `workstationNsgName`, `bastionName`. |
| `virtualNetworkAddressCidrs` | Complete observed approved VNet address ranges. |
| `existingSubnetNames`, `existingSubnetCidrs` | Paired complete current inventory. New workstation subnet must not reuse an existing name or overlap a prefix. |
| `protectedDestinationCidrs` | Complete observed protected database/key/evidence/registry/broker/control-plane/worker endpoint ranges, including public endpoints when relevant. Empty inventory is rejected. |
| `approvedAppIlbAddress`, `approvedDeveloperRdpSourceAddress` | Exact reviewed hosts matching module inputs. Developer source must be supported by provider evidence; no guessed source or whole-VNet permission. |
| `approvedEgressRules` | Exact reviewed list matching module `egressRules`, with single host/port/protocol and purpose. Every change requires renewed review. |
| `approvedWindowsImageVersion` | Exact reviewed non-latest version for MicrosoftWindowsServer/WindowsServer/2022-datacenter-g2. |

An egress rule contains only `name`, `category` (`dns`, `platform`, `identity`, `certificate`, `update`), `destinationAddress` (one IPv4 host), `protocol` (`Tcp`/`Udp`), and `destinationPort` (one integer). No wildcard, CIDR, broad service tag, port range or arbitrary purpose is accepted as an allow. Each DNS host must have reviewed TCP and UDP53; the NIC binds exactly those addresses. Required reviewed platform, TLS identity, certificate and update flows must be explicit. These purpose labels do not prove that an IP belongs to its claimed service. The review inventory and effective negative probes must prove that before use. Protected endpoints and workstation lateral destinations are forbidden even when their flows appear in the approval list.

## Platform and runtime limits

The NSG puts narrow source/destination allow rules ahead of explicit platform-tag denies and inbound/outbound deny-all. Ordinary provider default VNet/Internet allows are below those denies. [Azure platform tags](https://learn.microsoft.com/en-us/azure/virtual-network/service-tags-overview) matter because DNS/metadata/licensing traffic can bypass ordinary IP-based NSG treatment. These tags are used **only to deny**, never as broad allow rules. Exact explicitly reviewed host allows may still fail against platform-tag semantics; matching must be proved through effective rules and actual probes. NIC DNS binds only reviewed supplied hosts, with no new resolver, forwarding service or platform-source default selected by this packet.

[WireServer traffic](https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16) is not filtered by configured NSGs. The template does not prove full guest-level platform isolation. Official image guest-agent, licensing, DNS, update/certificate requirements, firewall state and browser availability require separate evidence. No OS patch/update policy is selected by this template. Image defaults and 128GiB minimum-disk compatibility remain unverified. No VM extension, command execution, backup, telemetry store or provider repair action is included.

The module requires setup-administrator credentials; it **does not create the routine nonadministrator RDP account**. That distinct account, owner-only portal/OS authorization, password handling/rotation, patch readiness, browser/persona sign-out and clipboard cleanup are owner handoff tasks. No customer OS credential or Azure role grant is inferred.

Bastion uses the [Developer virtualNetwork schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/2024-05-01/bastionhosts), with no AzureBastionSubnet, Bastion public IP or unsupported feature flags. Actual Developer RDP source/authentication behavior remains unverified. No automatic paid fallback is implemented. NAT attachment supplies explicit connectivity, not filtering or proof of support for an identity/update endpoint.

These checks cannot authorize paid provisioning. The disposed foundation, actual internal environment/BFF composition, quota/capacity, exact image/offer, full session cost, approved cleanup inventory, credential handoff and identity/key/audit gates remain prerequisites. Existing bootstrap ingress and the permanently disabled diagnostic BFF are unchanged. VM disk/NIC delete options describe ownership behavior when the VM is deliberately removed; they do not deallocate, delete, schedule disposal or stop billing by themselves. No cleanup or Azure write executes here.
