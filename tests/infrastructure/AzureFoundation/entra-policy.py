#!/usr/bin/env python3
"""Reject activation, consent or client credentials in the inert BFF scaffold."""
import copy
import json
import sys


def check(a):
    assert a['signInAudience']=='AzureADMyOrg'
    assert a['isFallbackPublicClient'] is False
    assert a['passwordCredentials']==a['keyCredentials']==a['requiredResourceAccess']==[]
    assert a['web']['redirectUris']==[]
    assert not any(a['web']['implicitGrantSettings'].values())
    assert a['api']['oauth2PermissionScopes']==[]
    assert not a['api'].get('knownClientApplications')
    assert not a['api'].get('preAuthorizedApplications')
    assert {r['value'] for r in a['appRoles']} == {
        'PilotConsultant','PilotCustomerUser','PilotAuditor','PilotPlatformOperator'}
    assert len(a['appRoles']) == 4
    assert len({r['id'] for r in a['appRoles']}) == 4
    assert all(r['allowedMemberTypes']==['User'] and r['isEnabled'] for r in a['appRoles'])
    assert not a.get('publicClient',{}).get('redirectUris')
    assert not a.get('spa',{}).get('redirectUris')


a=json.load(open(sys.argv[1]));check(a)
mutations=[
    lambda x:x.update(signInAudience='AzureADMultipleOrgs'),
    lambda x:x.update(isFallbackPublicClient=True),
    lambda x:x.update(passwordCredentials=[{'secretText':'synthetic-not-a-secret'}]),
    lambda x:x.update(requiredResourceAccess=[{'resourceAppId':'synthetic'}]),
    lambda x:x['web']['redirectUris'].append('https://example.invalid/callback'),
    lambda x:x['web']['implicitGrantSettings'].update(enableIdTokenIssuance=True),
    lambda x:x['appRoles'][0].update(allowedMemberTypes=['Application']),
    lambda x:x['api'].update(knownClientApplications=['unreviewed-client']),
    lambda x:x['api'].update(preAuthorizedApplications=[{'appId':'unreviewed-client'}]),
]
for mutate in mutations:
    x=copy.deepcopy(a);mutate(x)
    try:check(x)
    except AssertionError:continue
    raise AssertionError('unsafe registration mutation accepted')
print(f'PASS inert Entra registration baseline and {len(mutations)} unsafe mutations')
