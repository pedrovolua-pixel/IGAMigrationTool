#!/usr/bin/env python3
"""Read-only preflight binding check; feed protected files, never log their values."""
import copy
import ipaddress
import json
import re
import sys
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(parameters, snapshot):
    values = {name: item['value'] for name, item in parameters['parameters'].items()}
    env, vnet, nat = snapshot['environmentResource'], snapshot['virtualNetworkResource'], snapshot['natGatewayResource']
    group = snapshot['resourceGroupId']
    require(isinstance(group, str) and re.fullmatch(
        r'/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[A-Za-z0-9_.()-]+', group), 'Exact resource group ID required')
    require(env['id'].lower() == (group + '/providers/Microsoft.App/managedEnvironments/' +
            values['environmentName']).lower(), 'Environment scope/name mismatch')
    require(vnet['id'].lower() == (group + '/providers/Microsoft.Network/virtualNetworks/' +
            values['existingVnetName']).lower(), 'VNet scope/name mismatch')
    require(env['type'].lower() == 'microsoft.app/managedenvironments' and
            vnet['type'].lower() == 'microsoft.network/virtualnetworks', 'Wrong provider resource type')
    require(env['location'].lower() == vnet['location'].lower() == 'eastus2', 'East US 2 required')
    nat_id = group + '/providers/Microsoft.Network/natGateways/' + values['existingNatGatewayName']
    require(nat['id'].lower() == nat_id.lower() and nat['type'].lower() == 'microsoft.network/natgateways' and
            nat['location'].lower() == 'eastus2' and nat['properties']['provisioningState'] == 'Succeeded',
            'Existing same-scope ready NAT required')
    ep = env['properties']
    require(ep['provisioningState'] == vnet['properties']['provisioningState'] == 'Succeeded', 'Provider readiness required')
    require(ep['publicNetworkAccess'] == 'Disabled' and ep['vnetConfiguration']['internal'] is True,
            'Internal environment with public network disabled required')
    subnet_id = ep['vnetConfiguration']['infrastructureSubnetId']
    require(subnet_id.lower().startswith(vnet['id'].lower() + '/subnets/'), 'Environment must use same VNet')
    matching = [s for s in vnet['properties']['subnets'] if s['id'].lower() == subnet_id.lower()]
    require(len(matching) == 1, 'Exact environment subnet inventory required')
    infra = matching[0]['properties']
    require(any(d['properties']['serviceName'] == 'Microsoft.App/environments' for d in infra['delegations']),
            'Existing dedicated Container Apps subnet required')
    require(values['workstationSubnetName'].lower() not in {
        s['name'].lower() for s in vnet['properties']['subnets']}, 'New dedicated workstation subnet only')
    require(infra['natGateway']['id'].lower() == nat_id.lower(), 'Environment subnet NAT mismatch')
    domain = ep['defaultDomain']
    require(isinstance(domain, str) and re.fullmatch(
        r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.eastus2\.azurecontainerapps\.io', domain), 'Exact environment domain required')
    ilb = ipaddress.IPv4Address(ep['staticIp'])
    require(any(ilb in ipaddress.IPv4Network(r) for r in ('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16')),
            'Private environment ILB required')
    require(str(ilb) == values['appIlbAddress'], 'Reviewed app ILB disagrees with provider metadata')
    require(ilb in ipaddress.IPv4Network(infra['addressPrefix']), 'ILB outside environment infrastructure subnet')
    return True


def self_test():
    group = '/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic'
    vid = group + '/providers/Microsoft.Network/virtualNetworks/synthetic-vnet'
    sid = vid + '/subnets/synthetic-apps'
    params = {'parameters': {k: {'value': v} for k, v in {
        'environmentName': 'synthetic-env', 'existingVnetName': 'synthetic-vnet',
        'workstationSubnetName': 'synthetic-desktop', 'appIlbAddress': '10.42.0.5',
        'existingNatGatewayName': 'synthetic-nat'}.items()}}
    snapshot = {'resourceGroupId': group,
        'environmentResource': {'id': group + '/providers/Microsoft.App/managedEnvironments/synthetic-env',
            'type': 'Microsoft.App/managedEnvironments', 'location': 'eastus2', 'properties': {
                'provisioningState': 'Succeeded', 'publicNetworkAccess': 'Disabled',
                'vnetConfiguration': {'internal': True, 'infrastructureSubnetId': sid},
                'defaultDomain': 'synthetic.eastus2.azurecontainerapps.io', 'staticIp': '10.42.0.5'}},
        'virtualNetworkResource': {'id': vid, 'type': 'Microsoft.Network/virtualNetworks', 'location': 'eastus2',
            'properties': {'provisioningState': 'Succeeded', 'subnets': [
                {'id': sid, 'name': 'synthetic-apps', 'properties': {'addressPrefix': '10.42.0.0/27',
                    'delegations': [{'properties': {'serviceName': 'Microsoft.App/environments'}}],
                    'natGateway': {'id': group + '/providers/Microsoft.Network/natGateways/synthetic-nat'}}}]}},
        'natGatewayResource': {'id': group + '/providers/Microsoft.Network/natGateways/synthetic-nat',
            'type': 'Microsoft.Network/natGateways', 'location': 'eastus2', 'properties': {'provisioningState': 'Succeeded'}}}
    validate(params, snapshot)
    cases = [
        (['environmentResource', 'properties', 'publicNetworkAccess'], 'Enabled'),
        (['environmentResource', 'properties', 'vnetConfiguration', 'internal'], False),
        (['environmentResource', 'properties', 'vnetConfiguration', 'infrastructureSubnetId'], vid + '/subnets/other'),
        (['environmentResource', 'properties', 'staticIp'], '203.0.113.10'),
        (['environmentResource', 'properties', 'staticIp'], '10.42.0.6'),
        (['environmentResource', 'properties', 'defaultDomain'], 'azurecontainerapps.io'),
        (['environmentResource', 'properties', 'defaultDomain'], 'synthetic.westus.azurecontainerapps.io'),
        (['environmentResource', 'properties', 'provisioningState'], 'Failed'),
        (['environmentResource', 'location'], 'westus'),
        (['environmentResource', 'id'], group + '/providers/Microsoft.App/managedEnvironments/other'),
        (['virtualNetworkResource', 'id'], group + '/providers/Microsoft.Network/virtualNetworks/other'),
        (['virtualNetworkResource', 'type'], 'Microsoft.Network/publicIPAddresses'),
        (['virtualNetworkResource', 'properties', 'subnets'], []),
        (['virtualNetworkResource', 'properties', 'subnets', 0, 'properties', 'delegations'], []),
        (['virtualNetworkResource', 'properties', 'subnets', 0, 'properties', 'addressPrefix'], '10.42.1.0/27'),
        (['virtualNetworkResource', 'properties', 'subnets', 0, 'name'], 'synthetic-desktop'),
        (['natGatewayResource', 'id'], '/synthetic/foreign-nat'),
        (['natGatewayResource', 'properties', 'provisioningState'], 'Failed'),
        (['virtualNetworkResource', 'properties', 'subnets', 0, 'properties', 'natGateway', 'id'], '/synthetic/other-nat'),
    ]
    for path, value in cases:
        changed = copy.deepcopy(snapshot)
        target = changed
        for key in path[:-1]: target = target[key]
        target[path[-1]] = value
        try: validate(params, changed)
        except (ValueError, KeyError, TypeError): continue
        raise AssertionError('Unsafe provider metadata mutation escaped')
    print(f'PASS: synthetic provider binding and {len(cases)} negative cases')


def main():
    if sys.argv[1:] == ['--self-test']:
        self_test()
        return
    if len(sys.argv) != 3:
        raise SystemExit('Supply protected ARM parameter file and protected environment/VNet snapshot')
    try:
        validate(json.loads(Path(sys.argv[1]).read_text()), json.loads(Path(sys.argv[2]).read_text()))
    except (ValueError, KeyError, TypeError, OSError):
        raise SystemExit('DENIED: private environment/VNet inputs incomplete or inconsistent; values suppressed')
    print('PASS: captured input consistency only; refresh provider/what-if and prove live DNS/TLS/network before deployment')


if __name__ == '__main__':
    main()
