#!/usr/bin/env python3
"""HTTPS-T01 closed compiled-template policies and unsafe mutation checks."""
import copy
import functools
import json
import sys
from pathlib import Path

NAMES = {'environmentName', 'virtualNetworkName', 'containerAppsSubnetName', 'natGatewayName',
         'infrastructureResourceGroupName'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def param(name):
    return f"parameters('{name}')"


def resource_id(kind, *names):
    return f"resourceId('{kind}', {', '.join(param(name) for name in names)})"


def network_guard():
    vnet = f"reference({resource_id('Microsoft.Network/virtualNetworks', 'virtualNetworkName')}, '2025-05-01', 'full')"
    nat_id = resource_id('Microsoft.Network/natGateways', 'natGatewayName')
    nat = f"reference({nat_id}, '2025-05-01', 'full')"
    nat_properties = f"reference({nat_id}, '2025-05-01')"
    subnet_id = resource_id('Microsoft.Network/virtualNetworks/subnets', 'virtualNetworkName', 'containerAppsSubnetName')
    subnet = f"reference({subnet_id}, '2025-05-01')"
    delegation = f"coalesce(tryGet({subnet}, 'delegations'), createArray())"
    conditions = [f"equals(toLower({vnet}.location), 'eastus2')", f"equals(toLower({nat}.location), 'eastus2')",
                  f"equals({nat}.sku.name, 'Standard')",
                  f"equals(length(coalesce(tryGet({nat_properties}, 'publicIpAddresses'), createArray())), 1)",
                  f"equals(length(coalesce(tryGet({nat_properties}, 'publicIpPrefixes'), createArray())), 0)",
                  f"equals(tryGet(tryGet({subnet}, 'natGateway'), 'id'), {nat_id})",
                  f"equals(length({delegation}), 1)",
                  f"equals(length(filter({delegation}, lambda('delegation', equals(lambdaVariables('delegation').properties.serviceName, 'Microsoft.App/environments')))), 1)"]
    conditions += [f"equals(length(coalesce(tryGet({subnet}, '{key}'), createArray())), 0)"
                   for key in ('ipConfigurations', 'privateEndpoints', 'serviceAssociationLinks')]
    checks = functools.reduce(lambda left, right: f'and({left}, {right})', conditions)
    return f"[if({checks}, {subnet_id}, fail('Require an unused dedicated East US 2 Microsoft.App/environments subnet with the exact existing Standard NAT attachment.'))]"


def parameters_policy(parameters, root=False):
    require(set(parameters) == NAMES | ({'deployEnvironment'} if root else set()), 'Closed parameter scope')
    for name in NAMES:
        require(parameters[name] == {'type': 'string', 'minLength': 1}, 'Required explicit name; no default')
    if root:
        require(parameters['deployEnvironment'] == {'type': 'bool', 'allowedValues': [False]}, 'False-only required deployment guard')


def module_policy(template):
    require(template['metadata']['_generator']['version'] == '0.47.16.16243', 'Pinned compiler')
    parameters_policy(template['parameters'])
    require(set(template) == {'$schema', 'contentVersion', 'metadata', 'parameters', 'variables', 'resources', 'outputs'}, 'Closed environment template')
    require(template['variables'] == {'managedGroupName': "[if(not(equals(toLower(parameters('infrastructureResourceGroupName')), toLower(resourceGroup().name))), parameters('infrastructureResourceGroupName'), fail('Require a distinct explicitly reviewed platform-managed resource group.'))]"}, 'Explicit separate managed group fail guard')
    require(isinstance(template['resources'], list) and len(template['resources']) == 1, 'Only one new managed environment; no data/grant/app/network edits')
    environment = template['resources'][0]
    require(set(environment) == {'type', 'apiVersion', 'name', 'location', 'properties'}, 'No extra environment identity/scope/config')
    require(environment['type'] == 'Microsoft.App/managedEnvironments' and environment['apiVersion'] == '2026-01-01'
            and environment['name'] == "[parameters('environmentName')]" and environment['location'] == 'eastus2', 'Exact environment name/API/region')
    require(environment['properties'] == {'infrastructureResourceGroup': "[variables('managedGroupName')]",
            'publicNetworkAccess': 'Disabled', 'vnetConfiguration': {'infrastructureSubnetId': network_guard(), 'internal': False},
            'workloadProfiles': [{'name': 'Consumption', 'workloadProfileType': 'Consumption'}],
            'appLogsConfiguration': {'destination': 'none'}}, 'Exact external VNet shell; no admission, routes, dedicated capacity or export')
    require(template['outputs'] == {'environmentResourceId': {'type': 'string',
            'value': "[resourceId('Microsoft.App/managedEnvironments', parameters('environmentName'))]"}}, 'Exact inventory ID only; no generated origin/secret output')


def composition_policy(template):
    require(set(template) == {'$schema', 'contentVersion', 'metadata', 'parameters', 'resources'}, 'Closed inactive composition; no origin/output inference')
    require(template['metadata']['_generator']['version'] == '0.47.16.16243', 'Pinned compiler')
    parameters_policy(template['parameters'], root=True)
    require(isinstance(template['resources'], list) and len(template['resources']) == 1, 'One optional environment module only')
    module = template['resources'][0]
    require(set(module) == {'condition', 'type', 'apiVersion', 'name', 'properties'}, 'No nested deployment scope/copy drift')
    require(module['condition'] == "[parameters('deployEnvironment')]" and module['type'] == 'Microsoft.Resources/deployments'
            and module['apiVersion'] == '2025-04-01' and module['name'] == 'disabled-https-environment-preparation', 'Exact false-only environment deployment condition')
    properties = module['properties']
    require(set(properties) == {'expressionEvaluationOptions', 'mode', 'parameters', 'template'} and
            properties['expressionEvaluationOptions'] == {'scope': 'inner'} and properties['mode'] == 'Incremental', 'No linked/external/complete deployment mode')
    require(properties['parameters'] == {name: {'value': f'[{param(name)}]'} for name in NAMES}, 'Bind exact reviewed names to module; no literal override')
    module_policy(properties['template'])


def mutations(template, policy, cases):
    policy(template)
    for label, path, value in cases:
        candidate = copy.deepcopy(template)
        target = candidate
        for part in path[:-1]:
            target = target[part]
        target[path[-1]] = value
        try:
            policy(candidate)
        except (ValueError, KeyError, TypeError):
            continue
        raise AssertionError(f'Unsafe configuration escaped: {label}')
    return len(cases)


def run(environment, composition):
    base = ['resources', 0]
    properties = base + ['properties']
    environment_cases = [
      ('public access', properties + ['publicNetworkAccess'], 'Enabled'),
      ('internal conversion', properties + ['vnetConfiguration', 'internal'], True),
      ('unguarded subnet', properties + ['vnetConfiguration', 'infrastructureSubnetId'], '[resourceId(\'Microsoft.Network/virtualNetworks/subnets\', parameters(\'virtualNetworkName\'), parameters(\'containerAppsSubnetName\'))]'),
      ('wrong region', base + ['location'], 'westus'), ('wrong environment', base + ['name'], 'existing-internal'),
      ('preview API', base + ['apiVersion'], '2025-10-02-preview'),
      ('managed group bypass', properties + ['infrastructureResourceGroup'], 'existing-main-group'),
      ('paid profile', properties + ['workloadProfiles'], [{'name': 'D4', 'workloadProfileType': 'D4'}]),
      ('profile min replicas', properties + ['workloadProfiles', 0, 'minimumCount'], 1),
      ('log export', properties + ['appLogsConfiguration'], {'destination': 'log-analytics'}),
      ('custom domain', properties + ['customDomainConfiguration'], {'dnsSuffix': 'example.invalid'}),
      ('ingress tuning', properties + ['ingressConfiguration'], {'workloadProfileName': 'Consumption'}),
      ('route', ['resources'], environment['resources'] + [{'type': 'Microsoft.App/managedEnvironments/httpRouteConfigs'}]),
      ('app', ['resources'], environment['resources'] + [{'type': 'Microsoft.App/containerApps'}]),
      ('grant', ['resources'], environment['resources'] + [{'type': 'Microsoft.Authorization/roleAssignments'}]),
      ('public data service', ['resources'], environment['resources'] + [{'type': 'Microsoft.DBforPostgreSQL/flexibleServers'}]),
      ('edge', ['resources'], environment['resources'] + [{'type': 'Microsoft.Cdn/profiles'}]),
      ('secret output', ['outputs', 'environmentResourceId', 'value'], '[listKeys(\'example\', \'2025-01-01\')]'),
      ('name default', ['parameters', 'virtualNetworkName', 'defaultValue'], 'fixture-vnet'),
      ('additional activation input', ['parameters', 'enableSignIn'], {'type': 'bool'}),
    ]
    environment_count = mutations(environment, module_policy, environment_cases)
    root_properties = ['resources', 0, 'properties']
    composition_cases = [
      ('true allowed', ['parameters', 'deployEnvironment', 'allowedValues'], [False, True]),
      ('implicit deployment', ['parameters', 'deployEnvironment', 'defaultValue'], False),
      ('deployment true condition', ['resources', 0, 'condition'], True),
      ('deployment condition bypass', ['resources', 0, 'condition'], '[true()]'),
      ('wrong nested name', ['resources', 0, 'name'], 'unreviewed-module'),
      ('other resource group', ['resources', 0, 'resourceGroup'], 'other-group'),
      ('complete deletion mode', root_properties + ['mode'], 'Complete'),
      ('outer expression scope', root_properties + ['expressionEvaluationOptions', 'scope'], 'outer'),
      ('literal environment overwrite', root_properties + ['parameters', 'environmentName', 'value'], 'existing-env'),
      ('different subnet', root_properties + ['parameters', 'containerAppsSubnetName', 'value'], 'endpoint-subnet'),
      ('linked template', root_properties + ['templateLink'], {'uri': 'https://example.invalid/template.json'}),
      ('nested public access', root_properties + ['template', 'resources', 0, 'properties', 'publicNetworkAccess'], 'Enabled'),
      ('inferred hostname output', ['outputs'], {'origin': {'value': 'https://unobserved.invalid'}}),
      ('additional deployment', ['resources'], composition['resources'] + [{'type': 'Microsoft.Resources/deployments'}]),
      ('Bastion', ['resources'], composition['resources'] + [{'type': 'Microsoft.Network/bastionHosts'}]),
      ('diagnostic app', ['resources'], composition['resources'] + [{'type': 'Microsoft.App/containerApps'}]),
      ('private service rewrite', ['resources'], composition['resources'] + [{'type': 'Microsoft.KeyVault/vaults'}]),
      ('public ingress input', ['parameters', 'publicNetworkAccess'], {'type': 'string'}),
    ]
    composition_count = mutations(composition, composition_policy, composition_cases)
    print(f'PASS: HTTPS environment baseline/{environment_count} unsafe mutations; inactive composition baseline/{composition_count} unsafe mutations')
    print('Local environment scaffold only; no runnable portal, production BFF, provider/live/public readiness or gate acceptance')


if __name__ == '__main__':
    require(len(sys.argv) == 3, 'Supply compiled HTTPS environment and composition JSON')
    run(*(json.loads(Path(path).read_text()) for path in sys.argv[1:]))
