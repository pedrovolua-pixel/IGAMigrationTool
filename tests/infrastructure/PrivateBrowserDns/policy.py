#!/usr/bin/env python3
"""Compiled DNS boundary and drift checks. No Azure deployment/runtime evidence."""
import copy
import json
import importlib.util
import sys
from collections import Counter
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def dns_policy(template):
    require(Counter(r['type'] for r in template['resources']) == Counter({
        'Microsoft.Network/privateDnsZones': 1,
        'Microsoft.Network/privateDnsZones/virtualNetworkLinks': 1,
        'Microsoft.Network/privateDnsZones/A': 2}), 'DNS-only closed resource inventory')
    require(all(r['apiVersion'] == '2024-06-01' for r in template['resources']), 'Pinned DNS APIs')
    require(set(template['parameters']) == {'environmentDefaultDomain', 'environmentStaticIp',
                                           'existingVnetName', 'virtualNetworkLinkName'}, 'Closed DNS inputs')
    require(all(p.get('type') == 'string' and p.get('minLength') == 1 and 'defaultValue' not in p
                for p in template['parameters'].values()), 'Required explicit DNS inputs')
    zone, link, wildcard, apex = template['resources']
    require(zone['type'] == 'Microsoft.Network/privateDnsZones' and
            zone['name'] == "[parameters('environmentDefaultDomain')]" and zone['location'] == 'global' and
            set(zone) == {'type', 'apiVersion', 'name', 'location'}, 'No public or arbitrary DNS zone')
    require(link['name'] == "[format('{0}/{1}', parameters('environmentDefaultDomain'), parameters('virtualNetworkLinkName'))]",
            'Exact link parent/name required')
    require(link['type'] == 'Microsoft.Network/privateDnsZones/virtualNetworkLinks' and
            link['properties'] == {'registrationEnabled': False, 'virtualNetwork': {
                'id': "[resourceId('Microsoft.Network/virtualNetworks', parameters('existingVnetName'))]"}},
            'Same VNet link without registration')
    for record, suffix in ((wildcard, '*'), (apex, '@')):
        require(record['type'] == 'Microsoft.Network/privateDnsZones/A' and
                record['name'] == f"[format('{{0}}/{{1}}', parameters('environmentDefaultDomain'), '{suffix}')]" and
                record['properties'] == {'ttl': 60, 'aRecords': [
                    {'ipv4Address': "[parameters('environmentStaticIp')]"}]}, 'Only observed ILB A records')
    expected_dependency = ["[resourceId('Microsoft.Network/privateDnsZones', parameters('environmentDefaultDomain'))]"]
    require(all(r['dependsOn'] == expected_dependency for r in (link, wildcard, apex)), 'Zone dependencies')
    require(set(template['outputs']) == {'privateDnsZoneId', 'virtualNetworkLinkId'}, 'IDs-only DNS outputs')
    require(template['outputs'] == {
        'privateDnsZoneId': {'type': 'string', 'value': "[resourceId('Microsoft.Network/privateDnsZones', parameters('environmentDefaultDomain'))]"},
        'virtualNetworkLinkId': {'type': 'string', 'value': "[resourceId('Microsoft.Network/privateDnsZones/virtualNetworkLinks', parameters('environmentDefaultDomain'), parameters('virtualNetworkLinkName'))]"}},
        'Exact non-secret ID outputs only')


def composition_policy(template):
    resources = template['resources']
    require(isinstance(resources, dict) and set(resources) == {'environment', 'desktop', 'browserDns'},
            'Exact optional composition resource inventory')
    require(resources['environment'] == {'type': 'Microsoft.App/managedEnvironments',
            'apiVersion': '2026-01-01', 'existing': True, 'name': "[parameters('environmentName')]"},
            'Existing environment only; no ingress or public-state change')
    modules = {r['name']: r for r in resources.values() if r['type'] == 'Microsoft.Resources/deployments'}
    require(len(modules) == 2, 'Only separate access and DNS deployments')
    require(set(modules) == {'private-development-desktop', 'private-development-browser-dns'}, 'Exact module scope')
    require(all(r['properties']['mode'] == 'Incremental' for r in modules.values()), 'No Complete-mode deletion')
    for key, dependencies in (('desktop', None), ('browserDns', ['environment'])):
        deployment = resources[key]
        expected_keys = {'type', 'apiVersion', 'name', 'properties'} | ({'dependsOn'} if dependencies else set())
        require(set(deployment) == expected_keys and deployment['type'] == 'Microsoft.Resources/deployments' and
                deployment['apiVersion'] == '2025-04-01', 'Closed same-scope deployments only')
        require(deployment.get('dependsOn') == dependencies, 'Exact observed-environment dependency')
        require(set(deployment['properties']) == {'expressionEvaluationOptions', 'mode', 'parameters', 'template'} and
                deployment['properties']['expressionEvaluationOptions'] == {'scope': 'inner'},
                'No scope, template-link, credential or deployment-mode overrides')
    dns = modules['private-development-browser-dns']['properties']
    ref = "reference('environment')"
    require(dns['parameters']['environmentDefaultDomain']['value'] == f'[{ref}.defaultDomain]' and
            dns['parameters']['environmentStaticIp']['value'] == f'[{ref}.staticIp]', 'DNS from current environment')
    require(dns['parameters'] == {
        'environmentDefaultDomain': {'value': f'[{ref}.defaultDomain]'},
        'environmentStaticIp': {'value': f'[{ref}.staticIp]'},
        'existingVnetName': {'value': "[parameters('existingVnetName')]"},
        'virtualNetworkLinkName': {'value': "[parameters('browserDnsLinkName')]"}},
        'Exact reviewed DNS link and environment bindings only')
    dns_policy(dns['template'])
    require(dns['parameters']['existingVnetName']['value'] == "[parameters('existingVnetName')]", 'Same VNet DNS')
    require(all('defaultValue' not in p for p in template['parameters'].values()), 'No deployable default inputs')
    require(template['parameters']['adminPassword']['type'] == 'securestring', 'Protected OS secret')
    require(not template.get('outputs'), 'Composition cannot expose credentials or addresses')
    access = modules['private-development-desktop']['properties']
    policy_path = Path(__file__).resolve().parents[1] / 'PrivateBrowserAccess' / 'template-policy.py'
    spec = importlib.util.spec_from_file_location('access_policy', policy_path)
    access_policy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(access_policy)
    access_policy.policy(access['template'])
    expected_parameters = set(access['template']['parameters'])
    require(set(access['parameters']) == expected_parameters, 'Exact access module bindings')
    require(set(template['parameters']) == expected_parameters | {'environmentName', 'browserDnsLinkName'},
            'Closed root parameter scope')
    require(all(entry == {'value': f"[parameters('{name}')]"}
                for name, entry in access['parameters'].items()), 'No source/IP/password or flow substitution')


def set_path(target, path, value):
    for p in path[:-1]:
        target = target[p]
    target[path[-1]] = value


def mutations(template, policy, cases):
    policy(template)
    for label, path, value in cases:
        changed = copy.deepcopy(template)
        set_path(changed, path, value)
        try:
            policy(changed)
        except (ValueError, KeyError, TypeError):
            continue
        raise AssertionError(f'Unsafe mutation escaped: {label}')
    print(f'PASS: baseline and {len(cases)} unsafe mutations rejected')


def main():
    require(len(sys.argv) in (2, 3), 'Supply compiled DNS JSON and optional composition JSON')
    dns = json.loads(Path(sys.argv[1]).read_text())
    mutations(dns, dns_policy, [
        ('public zone', ['resources', 0, 'type'], 'Microsoft.Network/dnsZones'),
        ('arbitrary zone', ['resources', 0, 'name'], 'azurecontainerapps.io'),
        ('wrong VNet', ['resources', 1, 'properties', 'virtualNetwork', 'id'], '/synthetic/other'),
        ('auto registration', ['resources', 1, 'properties', 'registrationEnabled'], True),
        ('wrong apex IP', ['resources', 3, 'properties', 'aRecords', 0, 'ipv4Address'], '203.0.113.10'),
        ('wrong wildcard IP', ['resources', 2, 'properties', 'aRecords', 0, 'ipv4Address'], '203.0.113.10'),
        ('extra record', ['resources', 2, 'properties', 'aRecords'], []),
        ('wide wildcard', ['resources', 2, 'name'], '*.azurecontainerapps.io'),
        ('secret output', ['outputs', 'privateDnsZoneId', 'value'], "[listKeys('synthetic', '2025-01-01')]"),
        ('unreviewed TTL', ['resources', 2, 'properties', 'ttl'], 86400),
        ('missing dependency', ['resources', 1, 'dependsOn'], []),
        ('default public address', ['parameters', 'environmentStaticIp', 'defaultValue'], '203.0.113.10'),
        ('API drift', ['resources', 0, 'apiVersion'], '2025-01-01'),
        ('foreign link parent', ['resources', 1, 'name'], "[format('{0}/{1}', 'other', parameters('virtualNetworkLinkName'))]"),
        ('ID wrapped secret', ['outputs', 'privateDnsZoneId', 'value'], "[resourceId('Microsoft.Network/privateDnsZones', parameters('adminPassword'))]"),
        ('grant', ['resources'], dns['resources'] + [{'type': 'Microsoft.Authorization/roleAssignments'}]),
    ])
    if len(sys.argv) == 3:
        composed = json.loads(Path(sys.argv[2]).read_text())
        dp = ['resources', 'browserDns', 'properties']
        mutations(composed, composition_policy, [
            ('complete deletion', dp + ['mode'], 'Complete'),
            ('fixed domain', dp + ['parameters', 'environmentDefaultDomain', 'value'], 'synthetic.eastus2.azurecontainerapps.io'),
            ('fixed ILB', dp + ['parameters', 'environmentStaticIp', 'value'], '10.0.0.4'),
            ('different VNet', dp + ['parameters', 'existingVnetName', 'value'], 'other'),
            ('public workload', ['resources'], dict(composed['resources'], publicApp={'type': 'Microsoft.App/containerApps'})),
            ('password output', ['outputs'], {'password': {'type': 'string', 'value': "[parameters('adminPassword')]"}}),
            ('default password', ['parameters', 'adminPassword', 'defaultValue'], 'fake-synthetic-placeholder'),
            ('insecure password type', ['parameters', 'adminPassword', 'type'], 'string'),
            ('RDP source substitution', ['resources', 'desktop', 'properties', 'parameters', 'developerRdpSourceAddress', 'value'], '*'),
            ('child paid Bastion', ['resources', 'desktop', 'properties', 'template', 'resources', 'bastion', 'sku', 'name'], 'Standard'),
            ('wrong DNS link', dp + ['parameters', 'virtualNetworkLinkName', 'value'], 'existing-unreviewed-link'),
            ('foreign deployment group', ['resources', 'desktop', 'resourceGroup'], 'other-group'),
            ('foreign subscription', ['resources', 'browserDns', 'subscriptionId'], '/synthetic/foreign'),
            ('conditional omission', ['resources', 'desktop', 'condition'], False),
            ('missing environment dependency', ['resources', 'browserDns', 'dependsOn'], []),
            ('child public address', ['resources', 'desktop', 'properties', 'template', 'resources', 'workstationNic', 'properties', 'ipConfigurations', 0, 'properties', 'publicIPAddress'], {'id': '/synthetic/public'}),
        ])
    print('Local structural checks only; metadata freshness, TLS and effective network behavior NOT VERIFIED.')


if __name__ == '__main__':
    main()
