#!/usr/bin/env python3
"""Exercise the actual combined preflight CLI with isolated synthetic files."""
import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
from pathlib import Path

fixture_path = Path(__file__).resolve().parents[1] / 'PrivateBrowserAccess' / 'test-inputs.py'
spec = importlib.util.spec_from_file_location('access_fixture', fixture_path)
fixture_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture_module)


def fixture():
    document, review = fixture_module.fixture()
    document['parameters'].update({'environmentName': {'value': 'fixture-env'},
                                   'browserDnsLinkName': {'value': 'fixture-browser-link'}})
    group = '/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic'
    vid = group + '/providers/Microsoft.Network/virtualNetworks/fixture-vnet'
    sid = vid + '/subnets/fixture-apps-subnet'
    snapshot = {'resourceGroupId': group,
        'environmentResource': {'id': group + '/providers/Microsoft.App/managedEnvironments/fixture-env',
            'type': 'Microsoft.App/managedEnvironments', 'location': 'eastus2', 'properties': {
                'provisioningState': 'Succeeded', 'publicNetworkAccess': 'Disabled',
                'vnetConfiguration': {'internal': True, 'infrastructureSubnetId': sid},
                'defaultDomain': 'fixture.eastus2.azurecontainerapps.io', 'staticIp': '10.77.1.5'}},
        'virtualNetworkResource': {'id': vid, 'type': 'Microsoft.Network/virtualNetworks', 'location': 'eastus2',
            'properties': {'provisioningState': 'Succeeded', 'addressSpace': {'addressPrefixes': ['10.77.0.0/16']}, 'subnets': [
                {'id': sid, 'name': 'fixture-apps-subnet', 'properties': {'addressPrefix': '10.77.1.0/24',
                    'delegations': [{'properties': {'serviceName': 'Microsoft.App/environments'}}],
                    'natGateway': {'id': group + '/providers/Microsoft.Network/natGateways/fixture-nat'}}},
                {'id': vid + '/subnets/fixture-endpoints-subnet', 'name': 'fixture-endpoints-subnet',
                 'properties': {'addressPrefix': '10.77.2.0/27', 'delegations': []}}]}},
        'natGatewayResource': {'id': group + '/providers/Microsoft.Network/natGateways/fixture-nat',
            'type': 'Microsoft.Network/natGateways', 'location': 'eastus2', 'properties': {'provisioningState': 'Succeeded'}}}
    return document, {'resourceGroupId': group, 'accessReview': review}, snapshot


def run_cli(objects, directory):
    paths = [directory / f'synthetic-{i}.json' for i in range(3)]
    for path, value in zip(paths, objects): path.write_text(json.dumps(value))
    return subprocess.run([sys.executable, str(Path(__file__).with_name('validate_access_composition.py')),
                           *map(str, paths)], capture_output=True, text=True, check=False)


def main():
    baseline = fixture()
    cases = [
        (0, ['parameters', 'environmentName', 'value'], None),
        (0, ['parameters', 'environmentName', 'value'], 'other-env'),
        (0, ['parameters', 'browserDnsLinkName', 'value'], None),
        (0, ['parameters', 'adminPassword'], {'value': None}),
        (0, ['parameters', 'unknown'], {'value': 'extra'}),
        (0, ['parameters', 'developerRdpSourceAddress', 'value'], '*'),
        (1, ['accessReview', 'reviewRef'], None),
        (2, ['environmentResource', 'properties', 'publicNetworkAccess'], 'Enabled'),
        (2, ['environmentResource', 'properties', 'staticIp'], '10.77.1.6'),
        (2, ['virtualNetworkResource', 'id'], '/synthetic/foreign-vnet'),
        (1, ['accessReview', 'existingSubnetCidrs'], ['10.77.1.0/24', '10.77.2.0/28']),
        (1, ['accessReview', 'virtualNetworkAddressCidrs'], ['10.77.0.0/17']),
        (2, ['natGatewayResource', 'id'], '/synthetic/foreign-nat'),
        (2, ['virtualNetworkResource', 'properties', 'subnets', 0, 'properties', 'natGateway', 'id'], '/synthetic/other-nat'),
        (1, ['resourceGroupId'], '/subscriptions/00000000-0000-0000-0000-000000000002/resourceGroups/synthetic'),
        (2, ['virtualNetworkResource', 'properties', 'subnets'], baseline[2]['virtualNetworkResource']['properties']['subnets'] +
            [{'id': baseline[2]['virtualNetworkResource']['id'] + '/subnets/actual-extra', 'name': 'actual-extra',
              'properties': {'addressPrefix': '10.77.3.0/27', 'delegations': []}}]),
    ]
    with tempfile.TemporaryDirectory(prefix='iga-synthetic-access-') as temporary:
        directory = Path(temporary)
        result = run_cli(baseline, directory)
        if result.returncode != 0 or 'PASS:' not in result.stdout:
            raise AssertionError('Combined synthetic baseline failed')
        for which, path, value in cases:
            changed = copy.deepcopy(baseline)
            target = changed[which]
            for key in path[:-1]: target = target[key]
            target[path[-1]] = value
            result = run_cli(changed, directory)
            if result.returncode == 0 or result.stderr.strip() != \
                    'DENIED: incomplete or inconsistent protected access inputs; values suppressed':
                raise AssertionError('Unsafe composition input escaped or protected error output changed')
        empty = ({'parameters': {}}, {}, {})
        if run_cli(empty, directory).returncode == 0: raise AssertionError('Empty input escaped')
    print(f'PASS: actual combined validator CLI, {len(cases) + 1} denied cases with suppressed values')


if __name__ == '__main__':
    main()
