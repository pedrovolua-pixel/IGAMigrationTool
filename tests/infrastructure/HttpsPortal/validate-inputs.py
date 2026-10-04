#!/usr/bin/env python3
"""Value-suppressed HTTPS-P01 protected binding checks. No Azure/network/write actions."""
import datetime
import ipaddress
import json
import re
import sys
import uuid
from pathlib import Path

NAMES = {'environmentName', 'virtualNetworkName', 'containerAppsSubnetName', 'natGatewayName',
         'infrastructureResourceGroupName'}
PARAMETERS = NAMES | {'deployEnvironment'}
PRIVATE_RANGES = tuple(ipaddress.ip_network(value) for value in ('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16'))
ACA_RESERVED = tuple(ipaddress.ip_network(value) for value in (
    '169.254.0.0/16', '172.30.0.0/16', '172.31.0.0/16', '192.0.2.0/24',
    '100.100.0.0/17', '100.100.128.0/19', '100.100.160.0/19', '100.100.192.0/19'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def resource_id(scope, kind, name):
    return f"/subscriptions/{scope['subscriptionId']}/resourceGroups/{scope['resourceGroupName']}/providers/{kind}/{name}"


def same(left, right):
    return isinstance(left, str) and isinstance(right, str) and left.lower() == right.lower()


def private_network(value):
    network = ipaddress.IPv4Network(value, strict=True)
    require(any(network.subnet_of(parent) for parent in PRIVATE_RANGES), 'Private IPv4 range required')
    return network


def reference(value):
    require(isinstance(value, str) and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._/-]{2,127}', value)
            and not any(part in value.lower() for part in ('unbound', 'placeholder', 'pending', 'todo')),
            'Bound review reference required')


def validate(document, snapshot, review):
    require(isinstance(document, dict) and isinstance(snapshot, dict) and isinstance(review, dict), 'Objects required')
    raw = document.get('parameters')
    require(isinstance(raw, dict) and set(raw) == PARAMETERS, 'Exact inactive composition parameters required')
    values = {}
    for name, entry in raw.items():
        require(isinstance(entry, dict) and set(entry) == {'value'} and entry['value'] is not None,
                'Every nonsecret input must be explicitly bound')
        values[name] = entry['value']
    require(values['deployEnvironment'] is False, 'Deployment stays permanently false in P01')
    for name in NAMES:
        require(isinstance(values[name], str) and re.fullmatch(r'[A-Za-z][A-Za-z0-9-]{0,62}', values[name]),
                'Supported explicit resource name required')
    require(len({value.lower() for name, value in values.items() if name in NAMES}) == len(NAMES),
            'Distinct scope names required')
    require(set(review) == {'scope', 'bindings', 'reviewRef', 'providerInventoryRef'}, 'Closed private review metadata')
    require(review['bindings'] == values, 'Review/input binding mismatch')
    reference(review['reviewRef'])
    reference(review['providerInventoryRef'])
    scope = review['scope']
    require(isinstance(scope, dict) and set(scope) == {'subscriptionId', 'resourceGroupName'}, 'Exact Azure scope required')
    require(isinstance(scope['subscriptionId'], str) and str(uuid.UUID(scope['subscriptionId'])) == scope['subscriptionId'].lower(),
            'Subscription UUID required')
    require(isinstance(scope['resourceGroupName'], str) and
            re.fullmatch(r'[A-Za-z][A-Za-z0-9-]{0,89}', scope['resourceGroupName']), 'Resource group binding required')
    require(not same(values['infrastructureResourceGroupName'], scope['resourceGroupName']), 'Managed group must be separate')
    require(set(snapshot) == {'scope', 'observedAtUtc', 'virtualNetwork', 'natGateway', 'natPublicIp',
                              'managedEnvironmentInventory', 'resourceGroupInventory'}, 'Closed provider snapshot required')
    require(snapshot['scope'] == scope, 'Provider subscription/resource group differs from review')
    observed = datetime.datetime.fromisoformat(snapshot['observedAtUtc'].replace('Z', '+00:00'))
    require(observed.utcoffset() == datetime.timedelta(0), 'Provider observation needs UTC attribution')
    # No arbitrary freshness duration is invented. A human must establish actual
    # collection scope/completeness/currentness before a separately approved apply.
    for key in ('managedEnvironmentInventory', 'resourceGroupInventory'):
        inventory = snapshot[key]
        require(isinstance(inventory, dict) and set(inventory) == {'value', 'nextLink'} and
                isinstance(inventory['value'], list) and inventory['nextLink'] is None,
                'Complete reviewed subscription inventory required; unfinished pagination rejects')
    vnet, nat = snapshot['virtualNetwork'], snapshot['natGateway']
    require(vnet['type'] == 'Microsoft.Network/virtualNetworks' and same(vnet['name'], values['virtualNetworkName']) and
            same(vnet['id'], resource_id(scope, 'Microsoft.Network/virtualNetworks', values['virtualNetworkName'])) and
            same(vnet['location'], 'eastus2'), 'VNet metadata mismatch')
    require(nat['type'] == 'Microsoft.Network/natGateways' and same(nat['name'], values['natGatewayName']) and
            same(nat['id'], resource_id(scope, 'Microsoft.Network/natGateways', values['natGatewayName'])) and
            same(nat['location'], 'eastus2') and nat['sku'] == {'name': 'Standard'}, 'Standard NAT metadata mismatch')
    public_ip = snapshot['natPublicIp']
    require(public_ip['type'] == 'Microsoft.Network/publicIPAddresses' and
            same(public_ip['location'], 'eastus2') and public_ip['sku'] == {'name': 'Standard', 'tier': 'Regional'} and
            same(public_ip['id'], resource_id(scope, 'Microsoft.Network/publicIPAddresses', public_ip['name'])),
            'Exact existing Standard Regional NAT public IP metadata required')
    require(nat['properties'].get('publicIpAddresses') == [{'id': public_ip['id']}] and
            nat['properties'].get('publicIpPrefixes', []) == [], 'NAT requires one exact public IP and no prefix fallback')
    ip_properties = public_ip['properties']
    require(ip_properties['publicIPAllocationMethod'] == 'Static' and ip_properties['publicIPAddressVersion'] == 'IPv4',
            'Static IPv4 NAT address required')
    address = ipaddress.IPv4Address(ip_properties['ipAddress'])
    require(not any(address in parent for parent in PRIVATE_RANGES) and not
            (address.is_unspecified or address.is_loopback or address.is_link_local or address.is_multicast or address.is_reserved),
            'Assigned NAT public IPv4 host required')
    prefixes = vnet['properties']['addressSpace']['addressPrefixes']
    require(isinstance(prefixes, list) and prefixes, 'Observed VNet address space required')
    vnet_ranges = [private_network(prefix) for prefix in prefixes]
    subnets = vnet['properties']['subnets']
    require(isinstance(subnets, list) and subnets, 'Complete VNet subnet inventory required')
    names, ranges, matches = set(), [], []
    for subnet in subnets:
        name = subnet['name']
        require(isinstance(name, str) and name.lower() not in names, 'Unique observed subnets required')
        names.add(name.lower())
        require(same(subnet['id'], resource_id(scope, 'Microsoft.Network/virtualNetworks/subnets',
                                             f"{values['virtualNetworkName']}/{name}")), 'Subnet parent/scope mismatch')
        properties = subnet['properties']
        require('addressPrefixes' not in properties, 'One observed IPv4 subnet prefix required')
        address = private_network(properties['addressPrefix'])
        require(any(address.subnet_of(parent) for parent in vnet_ranges) and
                not any(address.overlaps(other) for other in ranges), 'Subnet outside VNet or overlapping')
        ranges.append(address)
        if same(name, values['containerAppsSubnetName']):
            require(address.prefixlen <= 27, 'Workload-profiles subnet must be /27 or larger')
            require(not any(address.overlaps(reserved) for reserved in ACA_RESERVED), 'ACA-reserved address overlap')
            require(name.lower() != 'azurebastionsubnet', 'Dedicated Container Apps subnet required')
            require(same(properties.get('natGateway', {}).get('id'), nat['id']), 'Exact NAT attachment required')
            delegations = properties.get('delegations')
            require(isinstance(delegations, list) and len(delegations) == 1 and
                    delegations[0]['properties']['serviceName'] == 'Microsoft.App/environments', 'Exact app delegation required')
            for key in ('ipConfigurations', 'privateEndpoints', 'serviceAssociationLinks'):
                require(properties.get(key, []) == [], 'Dedicated subnet must be unused')
            matches.append(subnet)
    require(len(matches) == 1, 'One exact dedicated subnet match required')
    subnet_id = matches[0]['id']
    env_id = resource_id(scope, 'Microsoft.App/managedEnvironments', values['environmentName'])
    for environment in snapshot['managedEnvironmentInventory']['value']:
        require(isinstance(environment, dict) and isinstance(environment.get('id'), str)
                and isinstance(environment.get('properties'), dict), 'Full environment inventory entry required')
        require(not same(environment['id'], env_id), 'Do not convert/overwrite an existing environment')
        require(not same(environment['properties'].get('vnetConfiguration', {}).get('infrastructureSubnetId'), subnet_id),
                'Subnet is already assigned to an environment')
    managed_group_id = f"/subscriptions/{scope['subscriptionId']}/resourceGroups/{values['infrastructureResourceGroupName']}"
    for group in snapshot['resourceGroupInventory']['value']:
        require(isinstance(group, dict) and isinstance(group.get('id'), str), 'Full resource group inventory entry required')
        require(not same(group['id'], managed_group_id), 'Managed group must be newly reviewed, not an existing group')


def main():
    try:
        require(len(sys.argv) == 4, 'Supply ARM parameters, provider snapshot and private review JSON')
        validate(*(json.loads(Path(path).read_text()) for path in sys.argv[1:]))
    except (ValueError, TypeError, KeyError, AttributeError, OSError):
        print('FAIL: HTTPS scaffold bindings rejected; inspect protected inputs locally', file=sys.stderr)
        return 1
    print('PASS: local disabled-scaffold shape/provider bindings; no deployment/public admission or live readiness established')
    return 0


if __name__ == '__main__':
    sys.exit(main())
