#!/usr/bin/env python3
"""Reject infrastructure wiring that opens runtime paths or drops DNS ordering."""
import copy
import json
import sys


def check(t):
    rs = {r['name']: r for r in t['resources']}
    assert len(rs) == 9
    assert all(r['type'] == 'Microsoft.Resources/deployments' for r in rs.values())
    assert not t.get('outputs')
    assert not any('defaultValue' in t['parameters'][p] for p in (
        'databaseAdministratorId', 'databaseBackupRetentionDays',
        'vaultSoftDeleteRetentionDays', 'vaultPurgeProtection', 'telemetryRetentionDays'))
    def params(n):
        return {k: v['value'] for k,v in rs[n]['properties']['parameters'].items()}
    n = params('foundation-network')
    assert n['virtualNetworkAddressPrefix'] == '10.64.0.0/16'
    assert n['containerAppsSubnetAddressPrefix'] == '10.64.0.0/23'
    assert n['privateEndpointSubnetAddressPrefix'] == '10.64.2.0/24'
    assert n['workQueueName'] == 'synthetic-work'
    assert n['workloadIdentityPrefix'] == 'id-iga-pilot-dev'
    d = rs["[format('foundation-dns-{0}', copyIndex())]"]
    assert d['copy']['count'] == "[length(variables('zones'))]"
    assert t['variables']['zones'] == [
        'privatelink.azurecr.io','privatelink.vaultcore.azure.net',
        'privatelink.blob.core.windows.net','privatelink.postgres.database.azure.com',
        'privatelink.monitor.azure.com','privatelink.oms.opinsights.azure.com',
        'privatelink.ods.opinsights.azure.com','privatelink.agentsvc.azure-automation.net']
    assert any('foundation-network' in x for x in d['dependsOn'])
    for name, index, key in (
        ('foundation-registry',0,'registryPrivateDnsZoneId'),
        ('foundation-vault',1,'keyVaultPrivateDnsZoneId'),
        ('foundation-empty-storage',2,'blobPrivateDnsZoneId'),
        ('foundation-database',3,'privateDnsZoneId')):
        v=params(name)[key]
        assert "format('foundation-dns-{0}', " + str(index) + ')' in v
        assert 'privateDnsZoneId' in v
        assert 'containerApps' not in v
    m = rs['foundation-monitor-private-link']
    assert any('foundation-observability' in x for x in m['dependsOn'])
    assert 'dns' in m['dependsOn']
    assert any('foundation-network' in x for x in rs['foundation-container-apps']['dependsOn'])
    bindings = {
        'foundation-database': {'administratorPrincipalId':'databaseAdministratorId',
            'administratorPrincipalName':'databaseAdministratorName',
            'administratorPrincipalType':'databaseAdministratorType',
            'entraTenantId':'tenantId','backupRetentionDays':'databaseBackupRetentionDays'},
        'foundation-vault': {'tenantId':'tenantId',
            'softDeleteRetentionInDays':'vaultSoftDeleteRetentionDays',
            'enablePurgeProtection':'vaultPurgeProtection'},
        'foundation-observability': {'telemetryRetentionDays':'telemetryRetentionDays',
            'dailyIngestionCapGb':'dailyIngestionCapGb'},
        'foundation-container-apps': {'infrastructureResourceGroupName':'infrastructureResourceGroupName'},
    }
    for module, mapping in bindings.items():
        for target, source in mapping.items():
            assert params(module)[target] == "[parameters('" + source + "')]"
    db = params('foundation-database')
    assert db['skuName']=='Standard_B1ms' and db['storageSizeGB']==32
    assert db['storageAutoGrow']=='Disabled'


def deny(t, mutate):
    x=copy.deepcopy(t);mutate(x)
    try: check(x)
    except (AssertionError, KeyError): return
    raise AssertionError('unsafe composition mutation accepted')


t=json.load(open(sys.argv[1]));check(t)
mutations=[
    lambda x:x.update(outputs={'secret':{'type':'string','value':'secret'}}),
    lambda x:x['parameters']['vaultPurgeProtection'].update(defaultValue=True),
    lambda x:x['variables']['zones'].pop(),
    lambda x:x['variables']['zones'].reverse(),
    lambda x:x['resources'][0]['properties']['parameters']['workQueueName'].update(value='customer-work'),
]
for module, key in [('foundation-database','entraTenantId'), ('foundation-database','administratorPrincipalId'), ('foundation-vault','enablePurgeProtection'), ('foundation-observability','telemetryRetentionDays')]:
    def mutate(x, module=module, key=key):
        next(r for r in x['resources'] if r['name']==module)['properties']['parameters'][key]['value']='unapproved'
    mutations.append(mutate)
for f in mutations: deny(t,f)
print(f'PASS composition baseline and {len(mutations)} unsafe mutations')
