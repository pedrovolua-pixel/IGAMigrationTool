#!/usr/bin/env python3
"""Check compiled AZ-01 templates and prove unsafe configuration drift fails.

Usage: python3 private-services-policy.py REGISTRY.json VAULT.json DNS.json
Requires only Python's standard library. This validates deployment definitions;
it does not prove live DNS resolution, managed-identity access or denial.
"""
import copy
import json
import sys

REGISTRY = 'Microsoft.ContainerRegistry/registries'
VAULT = 'Microsoft.KeyVault/vaults'
ENDPOINT = 'Microsoft.Network/privateEndpoints'
DNS_GROUP = ENDPOINT + '/privateDnsZoneGroups'
ZONE = 'Microsoft.Network/privateDnsZones'
LINK = ZONE + '/virtualNetworkLinks'
ZONES = [
    'privatelink.azurecr.io',
    'privatelink.vaultcore.azure.net',
    'privatelink.blob.core.windows.net',
    'privatelink.postgres.database.azure.com',
    'privatelink.monitor.azure.com',
    'privatelink.oms.opinsights.azure.com',
    'privatelink.ods.opinsights.azure.com',
    'privatelink.agentsvc.azure-automation.net',
]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def resource(template, kind):
    matches = [r for r in template['resources'] if r['type'] == kind]
    require(len(matches) == 1, 'Exactly one ' + kind + ' required')
    return matches[0]


def get_path(value, path):
    for key in path:
        value = value[key]
    return value


def controls(kind):
    main = REGISTRY if kind == 'registry' else VAULT
    rules = [(('properties', 'publicNetworkAccess'), 'Disabled')]
    if kind == 'registry':
        rules += [
            (('apiVersion',), '2025-11-01'),
            (('sku', 'name'), 'Premium'),
            (('properties', 'adminUserEnabled'), False),
            (('properties', 'anonymousPullEnabled'), False),
            (('properties', 'networkRuleBypassOptions'), 'None'),
            (('properties', 'networkRuleBypassAllowedForTasks'), False),
            (('properties', 'networkRuleSet', 'defaultAction'), 'Deny'),
            (('properties', 'networkRuleSet', 'ipRules'), []),
            (('properties', 'dataEndpointEnabled'), True),
        ]
    else:
        rules += [
            (('apiVersion',), '2026-02-01'),
            (('properties', 'tenantId'), "[parameters('tenantId')]"),
            (('properties', 'enableRbacAuthorization'), True),
            (('properties', 'accessPolicies'), []),
            (('properties', 'enabledForDeployment'), False),
            (('properties', 'enabledForDiskEncryption'), False),
            (('properties', 'enabledForTemplateDeployment'), False),
            (('properties', 'enableSoftDelete'), True),
            (('properties', 'softDeleteRetentionInDays'), "[parameters('softDeleteRetentionInDays')]"),
            (('properties', 'enablePurgeProtection'), "[parameters('enablePurgeProtection')]"),
            (('properties', 'networkAcls', 'bypass'), 'None'),
            (('properties', 'networkAcls', 'defaultAction'), 'Deny'),
            (('properties', 'networkAcls', 'ipRules'), []),
            (('properties', 'networkAcls', 'virtualNetworkRules'), []),
        ]
    result = [(main, path, expected) for path, expected in rules]
    group = 'registry' if kind == 'registry' else 'vault'
    name = 'registryName' if kind == 'registry' else 'keyVaultName'
    dns = 'registryPrivateDnsZoneId' if kind == 'registry' else 'keyVaultPrivateDnsZoneId'
    result += [
        (main, ('location',), "[variables('location')]"),
        (ENDPOINT, ('apiVersion',), '2025-05-01'),
        (ENDPOINT, ('properties', 'subnet', 'id'), "[parameters('privateEndpointSubnetId')]"),
        (ENDPOINT, ('properties', 'privateLinkServiceConnections', 0, 'properties', 'privateLinkServiceId'),
         "[resourceId('" + main + "', parameters('" + name + "'))]"),
        (ENDPOINT, ('properties', 'privateLinkServiceConnections', 0, 'properties', 'groupIds'), [group]),
        (DNS_GROUP, ('apiVersion',), '2025-05-01'),
        (DNS_GROUP, ('properties', 'privateDnsZoneConfigs', 0, 'properties', 'privateDnsZoneId'),
         "[parameters('" + dns + "')]"),
    ]
    return result


def validate_service(template, kind):
    main = REGISTRY if kind == 'registry' else VAULT
    require(sorted(r['type'] for r in template['resources']) == sorted([main, ENDPOINT, DNS_GROUP]),
            'Unexpected resource: secrets, grants, tasks and additional services prohibited')
    require(template['variables']['location'] == 'eastus2', 'Single approved US region required')
    for res_type, path, expected in controls(kind):
        require(get_path(resource(template, res_type), path) == expected, res_type + '/' + str(path))
    require(len(resource(template, ENDPOINT)['properties']['privateLinkServiceConnections']) == 1,
            'One private service connection required')
    require(len(resource(template, DNS_GROUP)['properties']['privateDnsZoneConfigs']) == 1,
            'One DNS zone required')
    name = 'registryName' if kind == 'registry' else 'keyVaultName'
    main_id = "[resourceId('" + main + "', parameters('" + name + "'))]"
    endpoint_id = "[resourceId('Microsoft.Network/privateEndpoints', format('{0}-pe', parameters('" + name + "')))]"
    outputs = {
        'registryId' if kind == 'registry' else 'keyVaultId': {'type': 'string', 'value': main_id},
        'privateEndpointId': {'type': 'string', 'value': endpoint_id},
    }
    if kind == 'registry':
        outputs['loginServer'] = {'type': 'string', 'value':
            "[reference(resourceId('Microsoft.ContainerRegistry/registries', parameters('registryName')), '2025-11-01').loginServer]"}
    require(template['outputs'] == outputs, 'Only reviewed non-secret output expressions permitted')
    endpoint_props = resource(template, ENDPOINT)['properties']
    require(set(endpoint_props) == {'subnet', 'privateLinkServiceConnections'},
            'Unsupported/manual private endpoint connections prohibited')
    require(set(endpoint_props['subnet']) == {'id'}, 'Unsupported subnet settings prohibited')
    require(set(endpoint_props['privateLinkServiceConnections'][0]['properties']) == {'privateLinkServiceId', 'groupIds'},
            'Unsupported private service connection settings prohibited')
    require('policies' not in resource(template, main)['properties'], 'No unapproved retention policy')
    if kind == 'vault':
        for name in ['tenantId', 'softDeleteRetentionInDays', 'enablePurgeProtection']:
            require('defaultValue' not in template['parameters'][name], 'Owner input required: ' + name)
        retention = template['parameters']['softDeleteRetentionInDays']
        require(retention['type'] == 'int' and retention['minValue'] == 7 and retention['maxValue'] == 90,
                'Service retention bounds required')
        require(template['parameters']['enablePurgeProtection']['type'] == 'bool', 'Explicit purge decision required')


def validate_dns(template):
    require(sorted(r['type'] for r in template['resources']) == sorted([ZONE, LINK]), 'Only zone and VNet link permitted')
    require(template['parameters']['zoneName']['allowedValues'] == ZONES, 'Exact approved private DNS zones required')
    for kind in [ZONE, LINK]:
        item = resource(template, kind)
        require(item['apiVersion'] == '2024-06-01' and item['location'] == 'global', 'Stable global DNS API required')
    props = resource(template, LINK)['properties']
    require(props['registrationEnabled'] is False, 'Auto-registration prohibited')
    require(props['virtualNetwork']['id'] == "[parameters('virtualNetworkId')]", 'Existing VNet link required')
    require(template['outputs'] == {
        'privateDnsZoneId': {'type': 'string', 'value': "[resourceId('Microsoft.Network/privateDnsZones', parameters('zoneName'))]"},
        'virtualNetworkLinkId': {'type': 'string', 'value':
            "[resourceId('Microsoft.Network/privateDnsZones/virtualNetworkLinks', parameters('zoneName'), parameters('virtualNetworkLinkName'))]"},
    }, 'Only reviewed non-secret DNS output expressions permitted')


def expect_denied(template, validator, mutate, label):
    changed = copy.deepcopy(template)
    mutate(changed)
    try:
        validator(changed)
    except (ValueError, KeyError, TypeError, IndexError):
        return
    raise RuntimeError('Unsafe mutation accepted: ' + label)


def set_path(value, path, replacement):
    for key in path[:-1]:
        value = value[key]
    value[path[-1]] = replacement


def main():
    require(len(sys.argv) == 4, __doc__)
    registry, vault, dns = [json.load(open(path, encoding='utf-8')) for path in sys.argv[1:]]
    count = 0
    for template, kind in [(registry, 'registry'), (vault, 'vault')]:
        validator = lambda value, k=kind: validate_service(value, k)
        validator(template)
        for res_type, path, expected in controls(kind):
            bad = not expected if isinstance(expected, bool) else ['unsafe'] if isinstance(expected, list) else 'unsafe'
            expect_denied(template, validator,
                          lambda t, r=res_type, p=path, b=bad: set_path(resource(t, r), p, b), str(path))
            count += 1
        expect_denied(template, validator, lambda t: t['resources'].append({'type': 'Microsoft.Authorization/roleAssignments'}), 'extra grant')
        expect_denied(template, validator, lambda t: t['outputs'].update({'secret': {'type': 'string', 'value': 'unsafe'}}), 'secret output')
        expect_denied(template, validator, lambda t: t['variables'].update({'location': 'westeurope'}), 'non-US region')
        count += 3
        for expression in ["[listCredentials(resourceId('Microsoft.ContainerRegistry/registries', 'unsafe'), '2025-11-01')]",
                           "[listKeys(resourceId('Microsoft.Storage/storageAccounts', 'unsafe'), '2025-06-01')]",
                           "[reference(resourceId('Microsoft.KeyVault/vaults/secrets', 'unsafe', 'secret'), '2026-02-01').value]"]:
            output_name = 'loginServer' if kind == 'registry' else 'keyVaultId'
            expect_denied(template, validator,
                          lambda t, n=output_name, e=expression: t['outputs'][n].update({'value': e}), 'secret output expression')
            count += 1
        expect_denied(template, validator,
                      lambda t: resource(t, ENDPOINT)['properties'].update({'manualPrivateLinkServiceConnections': []}),
                      'manual private connection')
        expect_denied(template, validator,
                      lambda t: resource(t, ENDPOINT)['properties']['privateLinkServiceConnections'].append({'properties': {}}),
                      'additional private connection')
        count += 2
    for name in ['tenantId', 'softDeleteRetentionInDays', 'enablePurgeProtection']:
        expect_denied(vault, lambda t: validate_service(t, 'vault'),
                      lambda t, n=name: t['parameters'][n].update({'defaultValue': 'unsafe'}), 'unapproved default ' + name)
        count += 1
    for bound, value in [('minValue', 1), ('maxValue', 365)]:
        expect_denied(vault, lambda t: validate_service(t, 'vault'),
                      lambda t, b=bound, v=value: t['parameters']['softDeleteRetentionInDays'].update({b: v}), 'retention bounds')
        count += 1
    validate_dns(dns)
    for path, value in [(('properties', 'registrationEnabled'), True), (('properties', 'virtualNetwork', 'id'), 'unsafe'),
                        (('apiVersion',), '2024-06-01-preview'), (('location',), 'eastus2')]:
        expect_denied(dns, validate_dns, lambda t, p=path, v=value: set_path(resource(t, LINK), p, v), str(path))
        count += 1
    expect_denied(dns, validate_dns, lambda t: t['parameters']['zoneName']['allowedValues'].append('example.com'), 'unapproved zone')
    count += 1
    expect_denied(dns, validate_dns,
                  lambda t: t['outputs']['privateDnsZoneId'].update({'value': "[listKeys('unsafe', '2025-06-01')]"}),
                  'secret DNS output expression')
    count += 1
    print(f'PASS: 3 compiled module baselines; {count} unsafe drift mutations denied. Live connectivity/access not verified.')


if __name__ == '__main__':
    main()
