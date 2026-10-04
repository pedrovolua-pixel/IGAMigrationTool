#!/usr/bin/env python3
"""Reject unsafe/incomplete PA01 bindings; never performs deployment or prints inputs."""
import ipaddress
import json
import re
import sys
from pathlib import Path

NAMES = ('existingVnetName', 'existingNatGatewayName', 'workstationSubnetName',
         'workstationVmName', 'workstationNicName', 'workstationNsgName', 'bastionName')
REQUIRED = set(NAMES) | {'workstationSubnetCidr', 'workstationPrivateAddress',
                       'appIlbAddress', 'developerRdpSourceAddress',
                       'windowsImageVersion', 'adminUsername', 'egressRules'}
PRIVATE = tuple(ipaddress.ip_network(c) for c in ('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16'))
REF = re.compile(r'[A-Za-z0-9][A-Za-z0-9._/-]{2,127}\Z')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def host(value):
    require(isinstance(value, str) and '/' not in value, 'Exact IPv4 host required')
    try:
        address = ipaddress.IPv4Address(value)
    except ipaddress.AddressValueError:
        raise ValueError('Invalid IPv4 host') from None
    require(not (address.is_unspecified or address.is_loopback or address.is_multicast
                 or address.is_link_local or address.is_reserved), 'Unusable IPv4 host')
    return address


def private(value):
    address = host(value)
    require(any(address in network for network in PRIVATE), 'RFC1918 private host required')
    return address


def network(value):
    try:
        result = ipaddress.IPv4Network(value, strict=True)
    except (ValueError, TypeError):
        raise ValueError('Canonical IPv4 CIDR required') from None
    require(any(result.subnet_of(parent) for parent in PRIVATE), 'Private IPv4 CIDR required')
    return result


def opaque_ref(value):
    require(isinstance(value, str) and REF.fullmatch(value) and
            not any(term in value.lower() for term in ('placeholder', 'unbound', 'todo', 'pending')),
            'Bound opaque review reference required')


def validate(document, review):
    require(isinstance(document, dict) and isinstance(review, dict), 'Objects required')
    raw = document.get('parameters')
    require(isinstance(raw, dict) and REQUIRED <= set(raw) <= REQUIRED | {'adminPassword'},
            'Exact module parameter keys required')
    parameters = {}
    for name in REQUIRED:
        entry = raw[name]
        require(isinstance(entry, dict) and set(entry) == {'value'}, 'Explicit value entry required')
        parameters[name] = entry['value']
        require(entry['value'] is not None, 'Unbound parameter')
    # OS credential creation/input stays an owner handoff. No plaintext password
    # belongs in any file consumed by this checker (including its output/logs).
    require('adminPassword' not in raw, 'Omit credential; supply through owner secure handoff')
    require(set(review) == {'reviewRef', 'credentialHandoffRef', 'scope',
                          'virtualNetworkAddressCidrs', 'existingSubnetCidrs', 'existingSubnetNames',
                          'protectedDestinationCidrs', 'approvedAppIlbAddress',
                          'approvedDeveloperRdpSourceAddress', 'approvedEgressRules',
                          'approvedWindowsImageVersion', 'networkEvidenceRefs'},
            'Exact protected review metadata required')
    opaque_ref(review['reviewRef'])
    opaque_ref(review['credentialHandoffRef'])
    require(isinstance(review['networkEvidenceRefs'], list) and len(review['networkEvidenceRefs']) >= 1,
            'Actual network review references required')
    for reference in review['networkEvidenceRefs']:
        opaque_ref(reference)
    require(review['scope'] == {name: parameters[name] for name in NAMES}, 'Review scope mismatch')
    for name in NAMES:
        value = parameters[name]
        require(isinstance(value, str) and re.fullmatch(r'[A-Za-z][A-Za-z0-9-]{0,63}', value),
                'Invalid resource name')
    require(len(parameters['workstationVmName']) <= 15, 'Windows computer name exceeds 15 characters')
    require(parameters['workstationSubnetName'].lower() != 'azurebastionsubnet',
            'Developer requires dedicated workstation subnet, not AzureBastionSubnet')
    require(len({parameters[name].lower() for name in NAMES}) == len(NAMES), 'Distinct names required')
    require(isinstance(parameters['adminUsername'], str) and
            re.fullmatch(r'[A-Za-z][A-Za-z0-9_-]{0,19}', parameters['adminUsername']) and
            parameters['adminUsername'].lower() not in {'administrator', 'admin', 'guest', 'root', 'user'},
            'Explicit supported setup administrator required')
    image = parameters['windowsImageVersion']
    require(isinstance(image, str) and re.fullmatch(r'\d+\.\d+\.\d+', image),
            'Exact image version required; latest/wildcards forbidden')
    require(image == review['approvedWindowsImageVersion'], 'Image review mismatch')
    subnet = network(parameters['workstationSubnetCidr'])
    require(subnet.prefixlen <= 29, 'Azure subnet must have room for reserved addresses and NIC')
    for key in ('virtualNetworkAddressCidrs', 'existingSubnetCidrs', 'protectedDestinationCidrs'):
        require(isinstance(review[key], list) and len(review[key]) > 0, 'Complete network inventory required')
    vnet_ranges = [network(cidr) for cidr in review['virtualNetworkAddressCidrs']]
    require(any(subnet.subnet_of(cidr) for cidr in vnet_ranges), 'Subnet outside approved VNet')
    existing = [network(cidr) for cidr in review['existingSubnetCidrs']]
    require(isinstance(review['existingSubnetNames'], list) and
            len(review['existingSubnetNames']) == len(existing) and
            all(isinstance(name, str) and name for name in review['existingSubnetNames']),
            'Existing subnet names and prefixes must be paired')
    require(parameters['workstationSubnetName'].lower() not in
            {name.lower() for name in review['existingSubnetNames']}, 'Cannot overwrite an existing subnet')
    require(all(any(cidr.subnet_of(parent) for parent in vnet_ranges) for cidr in existing),
            'Existing subnet inventory outside VNet')
    require(not any(subnet.overlaps(cidr) for cidr in existing), 'Subnet overlaps existing service subnet')
    protected = [ipaddress.IPv4Network(cidr, strict=True) for cidr in review['protectedDestinationCidrs']]
    workstation = private(parameters['workstationPrivateAddress'])
    require(workstation in subnet and int(workstation) - int(subnet.network_address) >= 4
            and workstation != subnet.broadcast_address, 'NIC address outside subnet or Azure reserved')
    app = private(parameters['appIlbAddress'])
    require(str(app) == review['approvedAppIlbAddress'] and app != workstation,
            'App ILB does not match reviewed environment')
    require(any(app in cidr for cidr in vnet_ranges) and app not in subnet, 'App ILB outside separate VNet service subnet')
    source = host(parameters['developerRdpSourceAddress'])
    require(str(source) == review['approvedDeveloperRdpSourceAddress'] and source != workstation,
            'Developer source does not match review')
    require(not any(app in cidr for cidr in protected), 'App target is a protected data endpoint')
    rules = parameters['egressRules']
    require(isinstance(rules, list) and 1 <= len(rules) <= 100 and rules == review['approvedEgressRules'],
            'Exact approved egress flow set required')
    names, flows, categories, dns = set(), set(), set(), {}
    for rule in rules:
        require(isinstance(rule, dict) and set(rule) == {'name', 'category', 'destinationAddress',
                                                       'protocol', 'destinationPort'}, 'Closed egress shape required')
        require(isinstance(rule['name'], str) and re.fullmatch(r'[a-z][a-z0-9-]{0,50}', rule['name'])
                and rule['name'] not in names, 'Unique bounded rule name required')
        names.add(rule['name'])
        category, protocol, port = rule['category'], rule['protocol'], rule['destinationPort']
        require(category in {'dns', 'platform', 'identity', 'certificate', 'update'}, 'Unapproved egress purpose')
        require(protocol in {'Tcp', 'Udp'} and type(port) is int and 1 <= port <= 65535,
                'Exact supported protocol and single port required')
        destination = host(rule['destinationAddress'])
        require(destination != workstation and destination != app and destination not in subnet,
                'No workstation/lateral/app port bypass')
        require(not any(destination in cidr for cidr in protected), 'Protected endpoint egress forbidden')
        flow = (str(destination), protocol, port)
        require(flow not in flows, 'Duplicate egress flow')
        flows.add(flow)
        categories.add(category)
        if category == 'dns':
            require(protocol in {'Tcp', 'Udp'} and port == 53, 'DNS must use exact TCP/UDP53')
            dns.setdefault(str(destination), set()).add(protocol)
        elif category in {'identity', 'certificate', 'update'}:
            require(protocol == 'Tcp' and port in {80, 443}, 'Web purpose requires TCP80/443')
            if category == 'identity':
                require(port == 443, 'Identity flow must use TLS')
        else:
            require((protocol == 'Tcp' and port in {80, 443, 1688, 32526}) or
                    (protocol == 'Udp' and port == 123), 'Unsupported platform port')
    require(categories == {'dns', 'platform', 'identity', 'certificate', 'update'},
            'Each necessary traffic purpose must be reviewed explicitly')
    require(dns and all(protocols == {'Tcp', 'Udp'} for protocols in dns.values()),
            'Each selected DNS host requires TCP and UDP53')


if __name__ == '__main__':
    try:
        require(len(sys.argv) == 3, 'Supply module ARM parameters and protected review JSON')
        validate(json.loads(Path(sys.argv[1]).read_text()), json.loads(Path(sys.argv[2]).read_text()))
    except (ValueError, KeyError, TypeError, OSError):
        print('FAIL: private browser bindings rejected; inspect protected files locally', file=sys.stderr)
        sys.exit(1)
    print('PASS: local binding shape/scope checks; provider, effective network, credential and spending gates remain unverified')
