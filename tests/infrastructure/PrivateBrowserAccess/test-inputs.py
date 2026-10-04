#!/usr/bin/env python3
"""Synthetic validation tests. Fixture names/addresses/image version are not deployment recommendations."""
import copy
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('inputs', Path(__file__).with_name('validate-inputs.py'))
inputs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inputs)


def fixture():
    values = {
      'existingVnetName': 'fixture-vnet', 'existingNatGatewayName': 'fixture-nat',
      'workstationSubnetName': 'fixture-desktop-subnet', 'workstationVmName': 'fixture-vm',
      'workstationNicName': 'fixture-nic', 'workstationNsgName': 'fixture-nsg', 'bastionName': 'fixture-bastion',
      'workstationSubnetCidr': '10.77.3.0/27', 'workstationPrivateAddress': '10.77.3.4',
      'appIlbAddress': '10.77.1.5', 'developerRdpSourceAddress': '198.51.100.9',
      'windowsImageVersion': '2022.0.12345', 'adminUsername': 'fixtureSetup',
      'egressRules': [
        {'name': 'dns-tcp', 'category': 'dns', 'destinationAddress': '10.77.1.6', 'protocol': 'Tcp', 'destinationPort': 53},
        {'name': 'dns-udp', 'category': 'dns', 'destinationAddress': '10.77.1.6', 'protocol': 'Udp', 'destinationPort': 53},
        {'name': 'platform-time', 'category': 'platform', 'destinationAddress': '198.51.100.10', 'protocol': 'Udp', 'destinationPort': 123},
        {'name': 'identity-tls', 'category': 'identity', 'destinationAddress': '198.51.100.11', 'protocol': 'Tcp', 'destinationPort': 443},
        {'name': 'certificate-http', 'category': 'certificate', 'destinationAddress': '198.51.100.12', 'protocol': 'Tcp', 'destinationPort': 80},
        {'name': 'update-tls', 'category': 'update', 'destinationAddress': '198.51.100.13', 'protocol': 'Tcp', 'destinationPort': 443},
      ]}
    document = {'parameters': {name: {'value': value} for name, value in values.items()}}
    review = {'reviewRef': 'SYNTHETIC-NETWORK-REVIEW-01', 'credentialHandoffRef': 'FIXTURE-01',
              'scope': {name: values[name] for name in inputs.NAMES},
              'virtualNetworkAddressCidrs': ['10.77.0.0/16'], 'existingSubnetCidrs': ['10.77.1.0/24', '10.77.2.0/27'],
              'existingSubnetNames': ['fixture-apps-subnet', 'fixture-endpoints-subnet'],
              'protectedDestinationCidrs': ['10.77.2.0/27', '198.51.100.200/32'],
              'approvedAppIlbAddress': values['appIlbAddress'],
              'approvedDeveloperRdpSourceAddress': values['developerRdpSourceAddress'],
              'approvedEgressRules': copy.deepcopy(values['egressRules']),
              'approvedWindowsImageVersion': values['windowsImageVersion'],
              'networkEvidenceRefs': ['SYNTHETIC-EFFECTIVE-POLICY-01']}
    return document, review


def set_path(obj, path, value):
    target = obj
    for part in path[:-1]:
        target = target[part]
    target[path[-1]] = value


def main():
    document, review = fixture()
    inputs.validate(document, review)
    cases = [
      ('null name', 'parameters', ['parameters', 'workstationVmName', 'value'], None),
      ('blank source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], ''),
      ('CIDR source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], '10.77.0.0/16'),
      ('tag source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], 'VirtualNetwork'),
      ('IPv6 source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], '::1'),
      ('wildcard source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], '*'),
      ('guessed source', 'parameters', ['parameters', 'developerRdpSourceAddress', 'value'], '168.63.129.16'),
      ('public app', 'parameters', ['parameters', 'appIlbAddress', 'value'], '8.8.8.8'),
      ('same NIC app', 'parameters', ['parameters', 'appIlbAddress', 'value'], '10.77.3.4'),
      ('app mismatch', 'parameters', ['parameters', 'appIlbAddress', 'value'], '10.77.1.7'),
      ('public subnet', 'parameters', ['parameters', 'workstationSubnetCidr', 'value'], '8.8.8.0/27'),
      ('overlap subnet', 'parameters', ['parameters', 'workstationSubnetCidr', 'value'], '10.77.2.0/27'),
      ('outside VNet', 'parameters', ['parameters', 'workstationSubnetCidr', 'value'], '10.78.3.0/27'),
      ('host bits subnet', 'parameters', ['parameters', 'workstationSubnetCidr', 'value'], '10.77.3.4/27'),
      ('reserved NIC', 'parameters', ['parameters', 'workstationPrivateAddress', 'value'], '10.77.3.3'),
      ('outside NIC', 'parameters', ['parameters', 'workstationPrivateAddress', 'value'], '10.77.4.4'),
      ('broadcast NIC', 'parameters', ['parameters', 'workstationPrivateAddress', 'value'], '10.77.3.31'),
      ('latest image', 'parameters', ['parameters', 'windowsImageVersion', 'value'], 'latest'),
      ('image mismatch', 'parameters', ['parameters', 'windowsImageVersion', 'value'], '2022.0.12346'),
      ('long computer name', 'parameters', ['parameters', 'workstationVmName', 'value'], 'fixture-computer-name-too-long'),
      ('plaintext credential', 'parameters', ['parameters', 'adminPassword'], {'value': ''}),
      ('null credential', 'parameters', ['parameters', 'adminPassword'], {'value': None}),
      ('credential reference', 'parameters', ['parameters', 'adminPassword'], {'reference': {}}),
      ('unbound review', 'review', ['reviewRef'], 'UNBOUND-NETWORK'),
      ('no handoff', 'review', ['credentialHandoffRef'], None),
      ('no network evidence', 'review', ['networkEvidenceRefs'], []),
      ('unknown review bool', 'review', ['approved'], True),
      ('empty service inventory', 'review', ['existingSubnetCidrs'], []),
      ('overwrite existing subnet', 'review', ['existingSubnetNames'], ['fixture-desktop-subnet', 'fixture-endpoints-subnet']),
      ('missing subnet names', 'review', ['existingSubnetNames'], []),
      ('empty protected inventory', 'review', ['protectedDestinationCidrs'], []),
      ('review scope drift', 'review', ['scope', 'existingVnetName'], 'different-vnet'),
      ('unbound rules', 'parameters', ['parameters', 'egressRules', 'value'], []),
    ]
    rejected = 0
    for label, which, path, value in cases:
        candidate, metadata = copy.deepcopy(document), copy.deepcopy(review)
        set_path(candidate if which == 'parameters' else metadata, path, value)
        try:
            inputs.validate(candidate, metadata)
        except (ValueError, KeyError, TypeError):
            rejected += 1
            continue
        raise AssertionError(f'Unsafe binding escaped: {label}')
    # Mutate BOTH reviewed and deployed flows to ensure approval strings don't
    # override structural/protected-boundary denial checks.
    flow_cases = [('Internet tag', 'destinationAddress', 'Internet'), ('whole CIDR', 'destinationAddress', '10.77.0.0/16'),
                  ('wildcard destination', 'destinationAddress', '*'), ('protected data', 'destinationAddress', '10.77.2.4'),
                  ('protected ARM', 'destinationAddress', '198.51.100.200'), ('lateral NIC', 'destinationAddress', '10.77.3.5'),
                  ('app alternate port', 'destinationAddress', '10.77.1.5'), ('metadata host', 'destinationAddress', '169.254.169.254'),
                  ('all ports', 'destinationPort', '*'), ('port range', 'destinationPort', '1-65535'),
                  ('wrong protocol', 'protocol', '*'), ('arbitrary purpose', 'category', 'database'),
                  ('false integer', 'destinationPort', True), ('identity plaintext', 'destinationPort', 80),
                  ('control port', 'destinationPort', 3389)]
    for label, key, value in flow_cases:
        candidate, metadata = copy.deepcopy(document), copy.deepcopy(review)
        candidate['parameters']['egressRules']['value'][3][key] = value
        metadata['approvedEgressRules'][3][key] = value
        try:
            inputs.validate(candidate, metadata)
        except (ValueError, KeyError, TypeError):
            rejected += 1
            continue
        raise AssertionError(f'Unsafe approved flow escaped: {label}')
    # Azure DNS may be explicitly reviewed but matching against platform-tag deny
    # still requires actual provider evidence; this test grants no live exception.
    candidate, metadata = copy.deepcopy(document), copy.deepcopy(review)
    for rule in candidate['parameters']['egressRules']['value'][:2]:
        rule['destinationAddress'] = '168.63.129.16'
    metadata['approvedEgressRules'] = copy.deepcopy(candidate['parameters']['egressRules']['value'])
    inputs.validate(candidate, metadata)
    print(f'PASS: 2 synthetic baselines and {rejected} unsafe parameter/review/flow cases rejected')
    print('Synthetic fixtures are not deployable bindings or real network/image evidence')


if __name__ == '__main__':
    main()
