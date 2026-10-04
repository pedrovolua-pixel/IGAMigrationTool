# Private browser DNS and environment binding checks

These standard-library checks verify the optional DNS template and protected provider metadata consistency. They do not deploy Azure, activate the BFF or prove DNS/TLS/NSG behavior.

With pinned Bicep0.47.16:

```sh
bicep build infra/bicep/modules/pilot-development-browser-dns.bicep --outfile /tmp/pilot-development-browser-dns.json
bicep lint infra/bicep/modules/pilot-development-browser-dns.bicep
bicep build infra/bicep/pilot-development-private-browser-access.bicep --outfile /tmp/pilot-development-private-browser-access.json
bicep lint infra/bicep/pilot-development-private-browser-access.bicep
python3 tests/infrastructure/PrivateBrowserDns/policy.py /tmp/pilot-development-browser-dns.json /tmp/pilot-development-private-browser-access.json
python3 tests/infrastructure/PrivateBrowserDns/environment_inputs.py --self-test
python3 tests/infrastructure/PrivateBrowserDns/test_composition_inputs.py
```

For a future independently reviewed session, run `validate_access_composition.py <protected ARM parameters> <protected network review> <protected provider snapshot>`; it invokes both the workstation validator and environment binding check. The module-only validator is described in [its README](../PrivateBrowserAccess/README.md). Do not pass or print credentials; the owner supplies the required secure OS parameter separately. Both scripts suppress invalid protected values in their terminal error messages.

The combined protected network review is an object with `resourceGroupId` (the exact reviewed subscription/group scope) and `accessReview` (the module review schema in the workstation README). Names alone cannot bind a review to a different same-named environment.

The provider snapshot contains `resourceGroupId`, `environmentResource`, `virtualNetworkResource` and `natGatewayResource`, using actual Azure resource JSON. The environment must be Succeeded, EastUS2, internal/publicNetworkAccessDisabled, bound to the same VNet's dedicated Container Apps subnet with a private staticIP matching `appIlbAddress`, and have its exact default domain. The workstation subnet must be new. The existing NAT must be ready in the same scope and attached to the environment subnet. The combined validator binds the complete reviewed VNet address and existing subnet inventories to the captured provider metadata. Missing fields, public/foreign resources, wrong domain/IP/subnet or incomplete snapshots deny. Refresh immediately before live provider validate/what-if; stale/captured JSON is not live provider evidence. The workstation validator additionally requires complete reviewed subnet/protected-destination/flow inventories.

Compiled tests reject extra resources/roles, public or broad DNS zones, wrong link/IP, auto-registration, missing dependencies and outputs/defaults that could expose secrets. Composed tests require DNS values derived from the exact current environment, Incremental mode and only the separately reviewed access/DNS modules. There is no root deployment replay, app ingress change, certificate bypass, private-endpoint bill or customer grant.
