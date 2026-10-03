#!/usr/bin/env python3
"""Compiled PA01 boundary policy and negative drift tests, pinned Bicep 0.47.16."""
import copy
import json
import sys
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def parameter(name):
    return f"[parameters('{name}')]"


def resource_id(kind, *names):
    args = ', '.join(f"parameters('{name}')" for name in names)
    return f"[resourceId('{kind}', {args})]"


def policy(template):
    require(template['metadata']['_generator']['version'] == '0.47.16.16243', 'Pinned compiler required')
    require(template['languageVersion'] == '2.0', 'Typed sealed array compilation required')
    resources = template['resources']
    expected = {'existingVnet': ('Microsoft.Network/virtualNetworks', '2025-05-01'),
                'existingNat': ('Microsoft.Network/natGateways', '2025-05-01'),
                'workstationNsg': ('Microsoft.Network/networkSecurityGroups', '2025-05-01'),
                'workstationSubnet': ('Microsoft.Network/virtualNetworks/subnets', '2025-05-01'),
                'workstationNic': ('Microsoft.Network/networkInterfaces', '2025-05-01'),
                'workstation': ('Microsoft.Compute/virtualMachines', '2024-11-01'),
                'bastion': ('Microsoft.Network/bastionHosts', '2024-05-01')}
    require(set(resources) == set(expected), 'Closed resource scope; no public IP, grant, extension or route')
    names = {'existingVnet': parameter('existingVnetName'), 'existingNat': parameter('existingNatGatewayName'),
             'workstationNsg': parameter('workstationNsgName'),
             'workstationSubnet': "[format('{0}/{1}', parameters('existingVnetName'), parameters('workstationSubnetName'))]",
             'workstationNic': parameter('workstationNicName'), 'workstation': parameter('workstationVmName'),
             'bastion': parameter('bastionName')}
    dependencies = {'workstationSubnet': ['workstationNsg'],
                    'workstationNic': ['workstationNsg', 'workstationSubnet'],
                    'workstation': ['workstationNic']}
    require(template['variables']['location'] == 'eastus2', 'East US 2 only')
    require(set(template['variables']) == {'location', 'dnsServerAddresses', 'copy'}, 'Closed network variable scope')
    for name, (kind, version) in expected.items():
        resource = resources[name]
        require((resource['type'], resource['apiVersion']) == (kind, version), 'Resource/API pin drift')
        require(resource['name'] == names[name], 'Resource name/parent must bind validated scope')
        if name in dependencies:
            require(resource['dependsOn'] == dependencies[name], 'Boundary resource dependency drift')
        if name in {'existingVnet', 'existingNat'}:
            require(resource.get('existing') is True and set(resource) == {'type', 'apiVersion', 'existing', 'name'},
                    'Existing foundation only; no VNet/NAT reconfiguration')
        else:
            require('existing' not in resource, 'Created boundary resource required')
            keys = {'type', 'apiVersion', 'name', 'properties'}
            if name != 'workstationSubnet':
                keys.add('location')
            if name in {'workstationSubnet', 'workstationNic', 'workstation'}:
                keys.add('dependsOn')
            if name == 'bastion':
                keys.add('sku')
            require(set(resource) == keys, 'No additional resource identity, scope or configuration')
            if name != 'workstationSubnet':
                require(resource['location'] == "[variables('location')]", 'Region binding drift')
    require(set(template['parameters']) == {'existingVnetName', 'existingNatGatewayName', 'workstationSubnetName',
            'workstationSubnetCidr', 'workstationPrivateAddress', 'workstationVmName', 'workstationNicName',
            'workstationNsgName', 'bastionName', 'appIlbAddress', 'developerRdpSourceAddress',
            'windowsImageVersion', 'adminUsername', 'adminPassword', 'egressRules'}, 'Closed required parameter set')
    for name, definition in template['parameters'].items():
        require('defaultValue' not in definition, 'Every binding explicit; no defaults')
        require(definition['type'] == ('securestring' if name == 'adminPassword' else
                                      'array' if name == 'egressRules' else 'string'), 'Parameter type drift')
    require(template['parameters']['adminPassword']['minLength'] == 12, 'Secure credential floor')
    egress = template['definitions']['EgressRule']
    require(egress['additionalProperties'] is False and set(egress['properties']) ==
            {'name', 'category', 'destinationAddress', 'protocol', 'destinationPort'}, 'Closed typed egress')
    require(set(egress['properties']['category']['allowedValues']) ==
            {'dns', 'platform', 'identity', 'certificate', 'update'}, 'No arbitrary egress purpose')
    require(egress['properties']['protocol']['allowedValues'] == ['Tcp', 'Udp'], 'Exact protocols')
    require(template['parameters']['egressRules']['minLength'] == 1 and
            template['parameters']['egressRules']['maxLength'] == 100, 'Bounded explicit rules')
    subnet = resources['workstationSubnet']['properties']
    require(subnet == {'addressPrefix': parameter('workstationSubnetCidr'), 'defaultOutboundAccess': False,
                       'delegations': [], 'serviceEndpoints': [], 'privateEndpointNetworkPolicies': 'Enabled',
                       'privateLinkServiceNetworkPolicies': 'Enabled',
                       'networkSecurityGroup': {'id': resource_id('Microsoft.Network/networkSecurityGroups', 'workstationNsgName')},
                       'natGateway': {'id': resource_id('Microsoft.Network/natGateways', 'existingNatGatewayName')}},
            'Dedicated nondelegated NSG/NAT subnet only')
    nic = resources['workstationNic']['properties']
    require(nic == {'enableIPForwarding': False, 'enableAcceleratedNetworking': False,
                    'networkSecurityGroup': {'id': resource_id('Microsoft.Network/networkSecurityGroups', 'workstationNsgName')},
                    'dnsSettings': {'dnsServers': "[variables('dnsServerAddresses')]"},
                    'ipConfigurations': [{'name': 'private-workstation', 'properties': {'primary': True,
                        'privateIPAddressVersion': 'IPv4', 'privateIPAllocationMethod': 'Static',
                        'privateIPAddress': parameter('workstationPrivateAddress'),
                        'subnet': {'id': resource_id('Microsoft.Network/virtualNetworks/subnets', 'existingVnetName', 'workstationSubnetName')}}}]},
            'One private bound NIC; no public IP, forwarding or extra configurations')
    require(template['variables']['dnsServerAddresses'] ==
            "[union(map(filter(parameters('egressRules'), lambda('rule', equals(lambdaVariables('rule').category, 'dns'))), lambda('rule', lambdaVariables('rule').destinationAddress)), createArray())]",
            'DNS must use exact reviewed hosts')
    vm = resources['workstation']['properties']
    require(set(vm) == {'hardwareProfile', 'storageProfile', 'osProfile', 'networkProfile', 'diagnosticsProfile'},
            'No additional identity, provider, diagnostics or billing options')
    require(vm['hardwareProfile'] == {'vmSize': 'Standard_B2s_v2'}, 'Approved B2s v2 compute SKU only')
    require(vm['storageProfile'] == {'imageReference': {'publisher': 'MicrosoftWindowsServer', 'offer': 'WindowsServer',
             'sku': '2022-datacenter-g2', 'version': parameter('windowsImageVersion')},
             'osDisk': {'createOption': 'FromImage', 'diskSizeGB': 128, 'caching': 'ReadWrite', 'deleteOption': 'Delete',
                        'managedDisk': {'storageAccountType': 'StandardSSD_LRS'}}, 'dataDisks': []},
            'Pinned official image and single writer E10-compatible disk only')
    require(vm['osProfile'] == {'computerName': parameter('workstationVmName'), 'adminUsername': parameter('adminUsername'),
             'adminPassword': parameter('adminPassword'), 'allowExtensionOperations': False},
            'Owner OS setup handoff only; no extensions or patch-policy selection')
    require(vm['networkProfile'] == {'networkInterfaces': [{'id': resource_id('Microsoft.Network/networkInterfaces', 'workstationNicName'),
             'properties': {'primary': True, 'deleteOption': 'Delete'}}]}, 'One owned NIC')
    require(vm['diagnosticsProfile'] == {'bootDiagnostics': {'enabled': False}}, 'No unreviewed diagnostic storage')
    bastion = resources['bastion']
    require(bastion['sku'] == {'name': 'Developer'} and bastion['properties'] ==
            {'virtualNetwork': {'id': resource_id('Microsoft.Network/virtualNetworks', 'existingVnetName')}},
            'Same-VNet Developer only; no paid fallback or dedicated IP/features')
    rules = resources['workstationNsg']['properties']
    require(set(rules) == {'securityRules'}, 'No alternative NSG configuration')
    expected_rules = "[concat(createArray(createObject('name', 'owner-developer-rdp', 'properties', createObject('priority', 100, 'direction', 'Inbound', 'access', 'Allow', 'protocol', 'Tcp', 'sourcePortRange', '*', 'destinationPortRange', '3389', 'sourceAddressPrefix', parameters('developerRdpSourceAddress'), 'destinationAddressPrefix', parameters('workstationPrivateAddress'))), createObject('name', 'private-app-https', 'properties', createObject('priority', 100, 'direction', 'Outbound', 'access', 'Allow', 'protocol', 'Tcp', 'sourcePortRange', '*', 'destinationPortRange', '443', 'sourceAddressPrefix', parameters('workstationPrivateAddress'), 'destinationAddressPrefix', parameters('appIlbAddress')))), variables('essentialRules'), variables('platformDenies'), variables('defaultDenies'))]"
    require(rules['securityRules'] == expected_rules, 'Exact RDP/app plus essential and deny rules required')
    loops = template['variables']['copy']
    require(len(loops) == 3 and [loop['name'] for loop in loops] == ['essentialRules', 'platformDenies', 'defaultDenies'],
            'No hidden rule arrays')
    essential = loops[0]
    require(essential['count'] == "[length(parameters('egressRules'))]" and essential['input'] == {
      'name': "[format('essential-{0}', parameters('egressRules')[copyIndex('essentialRules')].name)]",
      'properties': {'priority': "[add(200, copyIndex('essentialRules'))]", 'direction': 'Outbound', 'access': 'Allow',
       'protocol': "[parameters('egressRules')[copyIndex('essentialRules')].protocol]", 'sourcePortRange': '*',
       'destinationPortRange': "[string(parameters('egressRules')[copyIndex('essentialRules')].destinationPort)]",
       'sourceAddressPrefix': parameter('workstationPrivateAddress'),
       'destinationAddressPrefix': "[parameters('egressRules')[copyIndex('essentialRules')].destinationAddress]"}},
       'Each essential flow must bind exact input host/port and one workstation')
    for loop, names, priority, direction, destination in (
        (loops[1], "createArray('AzurePlatformDNS', 'AzurePlatformIMDS', 'AzurePlatformLKM')", "[add(3800, copyIndex('platformDenies'))]", 'Outbound',
         "[createArray('AzurePlatformDNS', 'AzurePlatformIMDS', 'AzurePlatformLKM')[copyIndex('platformDenies')]]"),
        (loops[2], "createArray('Inbound', 'Outbound')", 4000,
         "[createArray('Inbound', 'Outbound')[copyIndex('defaultDenies')]]", '*')):
        require(loop['count'] == f'[length({names})]' and loop['input']['properties'] == {
          'priority': priority, 'direction': direction, 'access': 'Deny', 'protocol': '*', 'sourcePortRange': '*',
          'destinationPortRange': '*', 'sourceAddressPrefix': '*', 'destinationAddressPrefix': destination},
          'Platform and default lateral/Internet deny must supersede provider defaults')
    require(set(template['outputs']) == {'workstationResourceId', 'workstationNicResourceId', 'workstationNsgResourceId',
                                        'workstationSubnetResourceId', 'bastionResourceId'}, 'Only boundary inventory outputs')
    outputs = {
      'workstationResourceId': resource_id('Microsoft.Compute/virtualMachines', 'workstationVmName'),
      'workstationNicResourceId': resource_id('Microsoft.Network/networkInterfaces', 'workstationNicName'),
      'workstationNsgResourceId': resource_id('Microsoft.Network/networkSecurityGroups', 'workstationNsgName'),
      'workstationSubnetResourceId': resource_id('Microsoft.Network/virtualNetworks/subnets', 'existingVnetName', 'workstationSubnetName'),
      'bastionResourceId': resource_id('Microsoft.Network/bastionHosts', 'bastionName'),
    }
    require(template['outputs'] == {name: {'type': 'string', 'value': value} for name, value in outputs.items()},
            'Exact owned inventory IDs only; no wrapped secret or unbound resource outputs')


def run(template):
    policy(template)
    cases = [
      ('region', ['variables', 'location'], 'westus'),
      ('paid Bastion', ['resources', 'bastion', 'sku', 'name'], 'Standard'),
      ('peered Bastion', ['resources', 'bastion', 'properties', 'virtualNetwork', 'id'], '/other/vnet'),
      ('wrong existing VNet', ['resources', 'existingVnet', 'name'], 'other-vnet'),
      ('wrong existing NAT', ['resources', 'existingNat', 'name'], 'other-nat'),
      ('overwrite different subnet', ['resources', 'workstationSubnet', 'name'],
       "[format('{0}/{1}', parameters('existingVnetName'), 'existing-protected-subnet')]"),
      ('wrong subnet parent', ['resources', 'workstationSubnet', 'name'],
       "[format('{0}/{1}', 'other-vnet', parameters('workstationSubnetName'))]"),
      ('wrong owned VM name', ['resources', 'workstation', 'name'], 'unreviewed-vm'),
      ('wrong owned NIC name', ['resources', 'workstationNic', 'name'], 'unreviewed-nic'),
      ('wrong owned NSG name', ['resources', 'workstationNsg', 'name'], 'unreviewed-nsg'),
      ('wrong owned Bastion name', ['resources', 'bastion', 'name'], 'unreviewed-bastion'),
      ('missing subnet dependency', ['resources', 'workstationSubnet', 'dependsOn'], []),
      ('wrong NIC dependency', ['resources', 'workstationNic', 'dependsOn'], ['otherSubnet']),
      ('wrong VM dependency', ['resources', 'workstation', 'dependsOn'], ['otherNic']),
      ('native tunnel', ['resources', 'bastion', 'properties', 'enableTunneling'], True),
      ('public IP resource', ['resources', 'publicIp'], {'type': 'Microsoft.Network/publicIPAddresses'}),
      ('role grant', ['resources', 'role'], {'type': 'Microsoft.Authorization/roleAssignments'}),
      ('extension', ['resources', 'extension'], {'type': 'Microsoft.Compute/virtualMachines/extensions'}),
      ('subnet delegation', ['resources', 'workstationSubnet', 'properties', 'delegations'], [{'name': 'apps'}]),
      ('implicit outbound', ['resources', 'workstationSubnet', 'properties', 'defaultOutboundAccess'], True),
      ('no NAT', ['resources', 'workstationSubnet', 'properties', 'natGateway'], {}),
      ('no subnet NSG', ['resources', 'workstationSubnet', 'properties', 'networkSecurityGroup'], {}),
      ('NIC public IP', ['resources', 'workstationNic', 'properties', 'ipConfigurations', 0, 'properties', 'publicIPAddress'], {'id': '/public'}),
      ('NIC forwarding', ['resources', 'workstationNic', 'properties', 'enableIPForwarding'], True),
      ('NIC second address', ['resources', 'workstationNic', 'properties', 'ipConfigurations'], []),
      ('NIC unbound DNS', ['resources', 'workstationNic', 'properties', 'dnsSettings'], {'dnsServers': []}),
      ('larger compute', ['resources', 'workstation', 'properties', 'hardwareProfile', 'vmSize'], 'Standard_D4s_v3'),
      ('former restricted compute', ['resources', 'workstation', 'properties', 'hardwareProfile', 'vmSize'], 'Standard_B2s'),
      ('premium disk', ['resources', 'workstation', 'properties', 'storageProfile', 'osDisk', 'managedDisk', 'storageAccountType'], 'Premium_LRS'),
      ('shared disk', ['resources', 'workstation', 'properties', 'storageProfile', 'osDisk', 'managedDisk', 'maxShares'], 2),
      ('unapproved image', ['resources', 'workstation', 'properties', 'storageProfile', 'imageReference', 'publisher'], 'Other'),
      ('latest image', ['resources', 'workstation', 'properties', 'storageProfile', 'imageReference', 'version'], 'latest'),
      ('automated extensions', ['resources', 'workstation', 'properties', 'osProfile', 'allowExtensionOperations'], True),
      ('identity assigned', ['resources', 'workstation', 'properties', 'identity'], {'type': 'SystemAssigned'}),
      ('actual VM identity', ['resources', 'workstation', 'identity'], {'type': 'SystemAssigned'}),
      ('broad rule', ['resources', 'workstationNsg', 'properties', 'securityRules'], "[concat(variables('essentialRules'))]"),
      ('wide egress source', ['variables', 'copy', 0, 'input', 'properties', 'sourceAddressPrefix'], 'VirtualNetwork'),
      ('wide egress destination', ['variables', 'copy', 0, 'input', 'properties', 'destinationAddressPrefix'], 'Internet'),
      ('wide egress port', ['variables', 'copy', 0, 'input', 'properties', 'destinationPortRange'], '*'),
      ('platform allow', ['variables', 'copy', 1, 'input', 'properties', 'access'], 'Allow'),
      ('default lateral allow', ['variables', 'copy', 2, 'input', 'properties', 'access'], 'Allow'),
      ('late default deny', ['variables', 'copy', 2, 'input', 'properties', 'priority'], 65535),
      ('credential default', ['parameters', 'adminPassword', 'defaultValue'], ''),
      ('plain credential type', ['parameters', 'adminPassword', 'type'], 'string'),
      ('implicit source', ['parameters', 'developerRdpSourceAddress', 'defaultValue'], '168.63.129.16'),
      ('unsealed flow', ['definitions', 'EgressRule', 'additionalProperties'], True),
      ('unreviewed parameter', ['parameters', 'unreviewed'], {'type': 'string'}),
      ('credential output', ['outputs', 'workstationResourceId', 'value'], parameter('adminPassword')),
      ('wrapped credential output', ['outputs', 'workstationResourceId', 'value'],
       "[resourceId('Microsoft.Compute/virtualMachines', concat(parameters('workstationVmName'), parameters('adminPassword')))]"),
      ('other resource output', ['outputs', 'workstationResourceId', 'value'],
       "[resourceId('Microsoft.Compute/virtualMachines', 'unreviewed-vm')]"),
    ]
    for label, path, value in cases:
        mutated = copy.deepcopy(template)
        target = mutated
        for part in path[:-1]:
            target = target[part]
        target[path[-1]] = value
        try:
            policy(mutated)
        except (ValueError, KeyError, TypeError):
            continue
        raise AssertionError(f'Unsafe mutation escaped: {label}')
    print(f'PASS: compiled private browser baseline and {len(cases)} unsafe template mutations')
    print('Local policy evidence only; provider, image compatibility, network, TLS, identity and spending tests remain NOT VERIFIED')


if __name__ == '__main__':
    require(len(sys.argv) == 2, 'Supply compiled private browser module JSON')
    run(json.loads(Path(sys.argv[1]).read_text()))
