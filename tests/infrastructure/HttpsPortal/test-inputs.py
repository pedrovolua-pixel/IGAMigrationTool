#!/usr/bin/env python3
"""Synthetic HTTPS-T01 binding denials and actual value-suppressed CLI checks."""
import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
from pathlib import Path

spec = importlib.util.spec_from_file_location('https_inputs', Path(__file__).with_name('validate-inputs.py'))
inputs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inputs)


def fixture():
    values = {'deployEnvironment': False, 'environmentName': 'fixture-https-env', 'virtualNetworkName': 'fixture-vnet',
              'containerAppsSubnetName': 'fixture-apps-subnet', 'natGatewayName': 'fixture-nat',
              'infrastructureResourceGroupName': 'fixture-managed-group'}
    scope = {'subscriptionId': '00000000-0000-0000-0000-000000000001', 'resourceGroupName': 'fixture-group'}
    subnet_id = inputs.resource_id(scope, 'Microsoft.Network/virtualNetworks/subnets', 'fixture-vnet/fixture-apps-subnet')
    nat_id = inputs.resource_id(scope, 'Microsoft.Network/natGateways', 'fixture-nat')
    public_ip_id = inputs.resource_id(scope, 'Microsoft.Network/publicIPAddresses', 'fixture-nat-public-ip')
    document = {'parameters': {name: {'value': value} for name, value in values.items()}}
    snapshot = {'scope': copy.deepcopy(scope), 'observedAtUtc': '2026-10-03T00:00:00Z',
      'virtualNetwork': {'id': inputs.resource_id(scope, 'Microsoft.Network/virtualNetworks', 'fixture-vnet'),
        'name': 'fixture-vnet', 'type': 'Microsoft.Network/virtualNetworks', 'location': 'eastus2',
        'properties': {'addressSpace': {'addressPrefixes': ['10.77.0.0/16']}, 'subnets': [
          {'id': subnet_id, 'name': 'fixture-apps-subnet', 'properties': {'addressPrefix': '10.77.1.0/27',
            'natGateway': {'id': nat_id}, 'delegations': [{'name': 'fixture-delegation', 'properties': {'serviceName': 'Microsoft.App/environments'}}]}}
        ]}},
      'natGateway': {'id': nat_id, 'name': 'fixture-nat', 'type': 'Microsoft.Network/natGateways', 'location': 'eastus2',
                     'sku': {'name': 'Standard'}, 'properties': {'publicIpAddresses': [{'id': public_ip_id}]}},
      'natPublicIp': {'id': public_ip_id, 'name': 'fixture-nat-public-ip', 'type': 'Microsoft.Network/publicIPAddresses',
                      'location': 'eastus2', 'sku': {'name': 'Standard', 'tier': 'Regional'},
                      'properties': {'publicIPAllocationMethod': 'Static', 'publicIPAddressVersion': 'IPv4',
                                     'ipAddress': '203.0.113.10'}},
      'managedEnvironmentInventory': {'value': [], 'nextLink': None},
      'resourceGroupInventory': {'value': [{'id': f"/subscriptions/{scope['subscriptionId']}/resourceGroups/fixture-group"}], 'nextLink': None}}
    review = {'scope': copy.deepcopy(scope), 'bindings': copy.deepcopy(values), 'reviewRef': 'SYNTHETIC-01',
              'providerInventoryRef': 'SYNTHETIC-INVENTORY-01'}
    return document, snapshot, review


def set_path(target, path, value):
    for part in path[:-1]:
        target = target[part]
    target[path[-1]] = value


def rejected(candidate):
    try:
        inputs.validate(*candidate)
    except (ValueError, KeyError, TypeError, AttributeError):
        return True
    return False


def main():
    baseline = fixture()
    inputs.validate(*baseline)
    subnet = ['virtualNetwork', 'properties', 'subnets', 0, 'properties']
    scope = baseline[2]['scope']
    env_id = inputs.resource_id(scope, 'Microsoft.App/managedEnvironments', 'fixture-https-env')
    subnet_id = baseline[1]['virtualNetwork']['properties']['subnets'][0]['id']
    cases = [
      ('null environment', 0, ['parameters', 'environmentName', 'value'], None),
      ('blank network', 0, ['parameters', 'virtualNetworkName', 'value'], ''),
      ('public activation', 0, ['parameters', 'deployEnvironment', 'value'], True),
      ('wrong guard type', 0, ['parameters', 'deployEnvironment', 'value'], 0),
      ('unknown origin input', 0, ['parameters', 'canonicalOrigin'], {'value': 'https://unobserved.invalid'}),
      ('credential input', 0, ['parameters', 'credential'], {'value': 'fixture-not-a-credential'}),
      ('scope mismatch', 1, ['scope', 'subscriptionId'], '00000000-0000-0000-0000-000000000002'),
      ('unbound observation', 1, ['observedAtUtc'], None),
      ('unbound review', 2, ['reviewRef'], 'UNBOUND'),
      ('unbound inventory', 2, ['providerInventoryRef'], None),
      ('binding mismatch', 2, ['bindings', 'environmentName'], 'other-env'),
      ('fake approval flag', 2, ['approved'], True),
      ('VNet location', 1, ['virtualNetwork', 'location'], 'westus'),
      ('VNet ID', 1, ['virtualNetwork', 'id'], '/unreviewed-vnet'),
      ('NAT location', 1, ['natGateway', 'location'], 'westus'),
      ('NAT StandardV2', 1, ['natGateway', 'sku', 'name'], 'StandardV2'),
      ('NAT ID', 1, ['natGateway', 'id'], '/other-nat'),
      ('NAT missing public IP', 1, ['natGateway', 'properties', 'publicIpAddresses'], []),
      ('NAT prefix fallback', 1, ['natGateway', 'properties', 'publicIpPrefixes'], [{'id': '/prefix'}]),
      ('NAT IP mismatch', 1, ['natGateway', 'properties', 'publicIpAddresses'], [{'id': '/other-ip'}]),
      ('public IP wrong tier', 1, ['natPublicIp', 'sku', 'tier'], 'Global'),
      ('public IP wrong region', 1, ['natPublicIp', 'location'], 'westus'),
      ('public IP dynamic', 1, ['natPublicIp', 'properties', 'publicIPAllocationMethod'], 'Dynamic'),
      ('public IP IPv6', 1, ['natPublicIp', 'properties', 'publicIPAddressVersion'], 'IPv6'),
      ('public IP unassigned', 1, ['natPublicIp', 'properties', 'ipAddress'], ''),
      ('subnet parent', 1, ['virtualNetwork', 'properties', 'subnets', 0, 'id'], '/other-vnet/subnet'),
      ('subnet too small', 1, subnet + ['addressPrefix'], '10.77.1.0/28'),
      ('subnet host bits', 1, subnet + ['addressPrefix'], '10.77.1.4/27'),
      ('subnet outside VNet', 1, subnet + ['addressPrefix'], '10.78.1.0/27'),
      ('subnet wrong NAT', 1, subnet + ['natGateway', 'id'], '/other-nat'),
      ('subnet wrong delegation', 1, subnet + ['delegations', 0, 'properties', 'serviceName'], 'Microsoft.Web/serverFarms'),
      ('subnet no delegation', 1, subnet + ['delegations'], []),
      ('subnet multiple delegation', 1, subnet + ['delegations'], [{'properties': {'serviceName': 'Microsoft.App/environments'}}] * 2),
      ('subnet IP occupied', 1, subnet + ['ipConfigurations'], [{'id': '/occupied'}]),
      ('subnet private endpoint', 1, subnet + ['privateEndpoints'], [{'id': '/protected-endpoint'}]),
      ('subnet service association', 1, subnet + ['serviceAssociationLinks'], [{'id': '/existing-env-link'}]),
      ('environment overwrite', 1, ['managedEnvironmentInventory', 'value'], [{'id': env_id, 'properties': {}}]),
      ('environment subnet reuse', 1, ['managedEnvironmentInventory', 'value'], [{'id': '/other-env', 'properties': {'vnetConfiguration': {'infrastructureSubnetId': subnet_id}}}]),
      ('unfinished environment inventory', 1, ['managedEnvironmentInventory', 'nextLink'], 'https://example.invalid/next'),
      ('unfinished group inventory', 1, ['resourceGroupInventory', 'nextLink'], 'https://example.invalid/next'),
      ('managed group exists', 1, ['resourceGroupInventory', 'value'], [{'id': f"/subscriptions/{scope['subscriptionId']}/resourceGroups/fixture-managed-group"}]),
    ]
    candidates = []
    for label, which, path, value in cases:
        candidate = copy.deepcopy(baseline)
        set_path(candidate[which], path, value)
        inputs.require(rejected(candidate), f'Unsafe input escaped: {label}')
        candidates.append(candidate)
    for reserved in inputs.ACA_RESERVED:
        candidate = copy.deepcopy(baseline)
        prefix = str(next(reserved.subnets(new_prefix=27)))
        candidate[1]['virtualNetwork']['properties']['addressSpace']['addressPrefixes'] = [str(reserved)]
        candidate[1]['virtualNetwork']['properties']['subnets'][0]['properties']['addressPrefix'] = prefix
        inputs.require(rejected(candidate), 'ACA documented reserved range escaped')
        candidates.append(candidate)
    # Duplicated inventory entries must not make an existing subnet look unused.
    candidate = copy.deepcopy(baseline)
    candidate[1]['virtualNetwork']['properties']['subnets'] *= 2
    inputs.require(rejected(candidate), 'Duplicate subnet inventory escaped')
    candidates.append(candidate)
    cli_cases = [baseline] + candidates
    with tempfile.TemporaryDirectory(prefix='iga-https-inputs-') as directory:
        paths = [Path(directory) / f'input-{index}.json' for index in range(3)]
        for index, candidate in enumerate(cli_cases):
            for path, document in zip(paths, candidate):
                path.write_text(json.dumps(document))
            result = subprocess.run([sys.executable, str(Path(__file__).with_name('validate-inputs.py')), *map(str, paths)],
                                    capture_output=True, text=True, check=False)
            inputs.require(result.returncode == (0 if index == 0 else 1), 'Actual CLI exit did not match input denial')
            output = result.stdout + result.stderr
            inputs.require(not any(value in output for value in ('fixture', '203.0.113.10', scope['subscriptionId'], 'Traceback')),
                           'CLI exposed protected values or traceback')
        example = Path(__file__).resolve().parents[3] / 'infra/bicep/environments/pilot-dev-https-portal.parameters.example.json'
        result = subprocess.run([sys.executable, str(Path(__file__).with_name('validate-inputs.py')), str(example), str(paths[1]), str(paths[2])],
                                capture_output=True, text=True, check=False)
        inputs.require(result.returncode == 1 and 'FAIL:' in result.stderr, 'Incomplete repository example must fail')
    print(f'PASS: synthetic binding baseline and {len(candidates)} unsafe input/provider cases; actual CLI baseline/{len(candidates) + 1} denials with values suppressed')
    print('No provider collection, paid session, production BFF, origin/proxy or live gate evidence')


if __name__ == '__main__':
    main()
