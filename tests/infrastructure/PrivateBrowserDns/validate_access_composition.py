#!/usr/bin/env python3
"""Read-only adapter for the access entry point; never emits protected values."""
import importlib.util
import json
import ipaddress
import sys
from pathlib import Path


def main():
    if len(sys.argv) != 4:
        raise SystemExit('Supply protected entry-point parameters, network review and provider snapshot JSON')
    path = Path(__file__).resolve().parents[1] / 'PrivateBrowserAccess' / 'validate-inputs.py'
    spec = importlib.util.spec_from_file_location('access_inputs', path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    from environment_inputs import validate as validate_environment
    try:
        document, reviewed_scope, snapshot = [json.loads(Path(p).read_text()) for p in sys.argv[1:]]
        worker.require(set(reviewed_scope) == {'resourceGroupId', 'accessReview'} and
                       reviewed_scope['resourceGroupId'].lower() == snapshot['resourceGroupId'].lower(),
                       'Protected review resource group mismatch')
        review = reviewed_scope['accessReview']
        worker.require(set(document['parameters']) == worker.REQUIRED | {'environmentName', 'browserDnsLinkName'},
                       'Closed entry-point inputs; OS password stays owner handoff')
        for name in ('environmentName', 'browserDnsLinkName'):
            entry = document['parameters'][name]
            worker.require(isinstance(entry, dict) and set(entry) == {'value'} and
                           isinstance(entry['value'], str) and len(entry['value']) > 0, 'Required root name')
        module = dict(document, parameters={name: document['parameters'][name] for name in worker.REQUIRED})
        worker.validate(module, review)
        actual = snapshot['virtualNetworkResource']['properties']
        worker.require(sorted(review['virtualNetworkAddressCidrs']) == sorted(actual['addressSpace']['addressPrefixes']),
                       'VNet address inventory mismatch')
        captured_subnets = {(item['name'].lower(), item['properties']['addressPrefix']) for item in actual['subnets']}
        reviewed_subnets = set(zip(map(str.lower, review['existingSubnetNames']), review['existingSubnetCidrs']))
        worker.require(captured_subnets == reviewed_subnets and len(captured_subnets) == len(actual['subnets']),
                       'Complete subnet inventory mismatch')
        proposed = ipaddress.IPv4Network(document['parameters']['workstationSubnetCidr']['value'], strict=True)
        worker.require(any(proposed.subnet_of(ipaddress.IPv4Network(cidr, strict=True))
                           for cidr in actual['addressSpace']['addressPrefixes']), 'Subnet outside captured VNet')
        worker.require(not any(proposed.overlaps(ipaddress.IPv4Network(item['properties']['addressPrefix'], strict=True))
                               for item in actual['subnets']), 'Subnet overlaps captured provider inventory')
        validate_environment(document, snapshot)
    except (ValueError, KeyError, TypeError, OSError, AttributeError):
        raise SystemExit('DENIED: incomplete or inconsistent protected access inputs; values suppressed')
    print('PASS: captured access/environment consistency only; live provider, network, credentials and cost gates remain')


if __name__ == '__main__':
    main()
