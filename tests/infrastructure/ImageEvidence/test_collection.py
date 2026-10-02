#!/usr/bin/env python3
"""Independent negative cases for private metadata collection, with synthetic reports only."""
import contextlib
from datetime import datetime, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('collector', ROOT / 'infra/containers/collect-image-evidence.py')
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)
NOW = datetime(2026, 10, 2, 12, 0, tzinfo=timezone.utc)
ID = 'sha256:' + '1' * 64
SHA = '2' * 40
VERSION = '0.73.0'


def report():
    return {'SchemaVersion': 2, 'ArtifactType': 'container_image',
            'CreatedAt': '2026-10-02T11:00:00Z', 'Trivy': {'Version': VERSION},
            'Metadata': {'ImageID': ID}, 'Results': [
                {'Packages': [{'Name': 'fictional-lib', 'Licenses': ['MIT']},
                              {'Name': 'fictional-unknown'}],
                 'Vulnerabilities': [{'VulnerabilityID': 'SYNTHETIC-NOT-A-CVE',
                                      'Severity': 'UNKNOWN', 'Title': 'PRIVATE-FINDING'}],
                 'Licenses': [{'Name': 'MIT'}]}]}


def spdx():
    return {'spdxVersion': 'SPDX-2.3', 'SPDXID': 'SPDXRef-DOCUMENT',
            'creationInfo': {'created': '2026-10-02T11:00:00Z',
                             'creators': ['Tool: trivy-' + VERSION]},
            'packages': [{'SPDXID': 'SPDXRef-SYNTHETIC',
                          'primaryPackagePurpose': 'CONTAINER',
                          'annotations': [{'comment': 'ImageID: ' + ID}]}],
            'relationships': [{'spdxElementId': 'SPDXRef-DOCUMENT',
                               'relationshipType': 'DESCRIBES',
                               'relatedSpdxElement': 'SPDXRef-SYNTHETIC'}]}


def metadata():
    return {'Version': 2, 'UpdatedAt': '2026-10-02T10:00:00Z',
            'DownloadedAt': '2026-10-02T11:00:00Z', 'NextUpdate': '2026-10-02T16:00:00Z'}


class CollectionTests(unittest.TestCase):
    def deny(self, function, *args):
        with self.assertRaises(collector.EvidenceError):
            function(*args)

    def test_digest_is_not_published_manifest(self):
        result = collector.validate_image([{'Id': ID, 'Os': 'linux', 'Architecture': 'amd64',
                                            'RepoDigests': ['example.invalid/a@' + ID]}], ID)
        self.assertIsNone(result['publishedManifestDigest'])
        self.assertEqual(result['publication'], 'NOT_VERIFIED')
        self.assertEqual(result['observedRepositoryManifestDigests'], [ID])

    def test_image_subject_and_platform_denials(self):
        base = {'Id': ID, 'Os': 'linux', 'Architecture': 'amd64'}
        for expected, delta in [('local:tag', {}), ('sha256:' + '0' * 64, {}),
                                (ID, {'Id': 'sha256:' + '3' * 64}),
                                (ID, {'Os': 'windows'}), (ID, {'Architecture': 'arm64'}),
                                (ID, {'RepoDigests': ['unbound-tag']})]:
            with self.subTest(expected=expected, delta=delta):
                self.deny(collector.validate_image, [{**base, **delta}], expected)
        self.deny(collector.validate_image, [], ID)

    def test_inventory_counts_keep_unknown_findings_and_licenses(self):
        counts = collector.validate_report(report(), ID, VERSION, NOW)
        self.assertEqual(counts['vulnerabilities'], 1)
        self.assertEqual(counts['packagesWithoutLicenseMetadata'], 1)
        self.assertEqual(counts['packages'], 2)

    def test_inventory_wrong_subject_version_schema_future_and_missing(self):
        for delta in [{'SchemaVersion': 1}, {'ArtifactType': 'sbom'},
                      {'Metadata': {'ImageID': 'sha256:' + '4' * 64}},
                      {'Trivy': {'Version': '0.69.4'}}, {'CreatedAt': '2027-01-01T00:00:00Z'},
                      {'Results': []}, {'Results': [{'Packages': []}]},
                      {'Results': [{'Packages': 'not-an-array'}]}]:
            with self.subTest(delta=delta):
                self.deny(collector.validate_report, {**report(), **delta}, ID, VERSION, NOW)

    def test_strict_json_denies_duplicate_and_truncated(self):
        for value in [b'{"a":1,"a":2}', b'{', b'\xff', b'{"value":NaN}']:
            self.deny(collector.parse_json, value)

    def test_time_requires_explicit_utc(self):
        for value in [None, '2026-10-02T10:00:00', '2026-10-02T11:00:00+01:00', 'tomorrow']:
            self.deny(collector.utc, value)

    def test_database_identity_clock_and_no_freshness_policy(self):
        with tempfile.TemporaryDirectory() as temp:
            cache = Path(temp)
            (cache / 'db').mkdir()
            (cache / 'db/trivy.db').write_bytes(b'SYNTHETIC-DATABASE')
            (cache / 'db/metadata.json').write_text(json.dumps(metadata()))
            data = {'Version': VERSION, 'VulnerabilityDB': metadata()}
            result = collector.validate_database(data, cache, VERSION, NOW)
            self.assertEqual(result['freshnessPolicy'], 'NOT_VERIFIED')
            self.assertEqual(len(result['databaseSha256']), 64)
            for delta in [{'Version': 1}, {'UpdatedAt': '2027-01-01T00:00:00Z'},
                          {'DownloadedAt': '2027-01-01T00:00:00Z'},
                          {'NextUpdate': '2026-10-01T00:00:00Z'},
                          {'DownloadedAt': '2026-10-01T00:00:00Z'}]:
                with self.subTest(delta=delta):
                    self.deny(collector.validate_database,
                              {'Version': VERSION, 'VulnerabilityDB': {**metadata(), **delta}},
                              cache, VERSION, NOW)
            self.deny(collector.validate_database, {'Version': VERSION}, cache, VERSION, NOW)
            (cache / 'db/metadata.json').write_text('{}')
            self.deny(collector.validate_database, data, cache, VERSION, NOW)
            (cache / 'db/metadata.json').write_text(json.dumps(metadata()))
            (cache / 'db/trivy.db').write_bytes(b'')
            self.deny(collector.validate_database, data, cache, VERSION, NOW)
            (cache / 'db/trivy.db').unlink()
            self.deny(collector.validate_database, data, cache, VERSION, NOW)

    def test_spdx_invalid_empty_wrong_tool_future(self):
        self.assertEqual(collector.validate_spdx(spdx(), VERSION, NOW, ID), 1)
        for delta in [{'spdxVersion': 'SPDX-1.0'}, {'packages': []},
                      {'packages': [{'SPDXID': '../escape'}]},
                      {'packages': [{'SPDXID': 'SPDXRef-DUP'}, {'SPDXID': 'SPDXRef-DUP'}]},
                      {'creationInfo': {'created': '2027-01-01T00:00:00Z',
                                        'creators': ['Tool: trivy-' + VERSION]}},
                      {'creationInfo': {'created': '2026-10-02T11:00:00Z', 'creators': []}}]:
            self.deny(collector.validate_spdx, {**spdx(), **delta}, VERSION, NOW, ID)

    def test_spdx_rejects_unrelated_missing_wrong_and_duplicate_image_roots(self):
        import copy
        valid = spdx()
        cases = []
        unrelated = copy.deepcopy(valid)
        unrelated['packages'][0]['annotations'][0]['comment'] = 'ImageID: sha256:' + '4' * 64
        cases.append(unrelated)
        missing_root = copy.deepcopy(valid)
        missing_root.pop('relationships')
        cases.append(missing_root)
        missing_describe = copy.deepcopy(valid)
        missing_describe['relationships'] = []
        cases.append(missing_describe)
        wrong_describe = copy.deepcopy(valid)
        wrong_describe['relationships'][0]['relatedSpdxElement'] = 'SPDXRef-UNRELATED'
        cases.append(wrong_describe)
        wrong_source = copy.deepcopy(valid)
        wrong_source['relationships'][0]['spdxElementId'] = 'SPDXRef-UNRELATED'
        cases.append(wrong_source)
        wrong_purpose = copy.deepcopy(valid)
        wrong_purpose['packages'][0]['primaryPackagePurpose'] = 'LIBRARY'
        cases.append(wrong_purpose)
        duplicate_describe = copy.deepcopy(valid)
        duplicate_describe['relationships'] *= 2
        cases.append(duplicate_describe)
        duplicate_container = copy.deepcopy(valid)
        duplicate_container['packages'].append({**duplicate_container['packages'][0],
                                                'SPDXID': 'SPDXRef-SECOND'})
        cases.append(duplicate_container)
        missing_id = copy.deepcopy(valid)
        missing_id['packages'][0]['annotations'] = []
        cases.append(missing_id)
        duplicate_id = copy.deepcopy(valid)
        duplicate_id['packages'][0]['annotations'] *= 2
        cases.append(duplicate_id)
        ambiguous_id = copy.deepcopy(valid)
        ambiguous_id['packages'][0]['annotations'].append({'comment': 'ImageID: sha256:' + '4' * 64})
        cases.append(ambiguous_id)
        for index, bad in enumerate(cases):
            with self.subTest(case=index):
                self.deny(collector.validate_spdx, bad, VERSION, NOW, ID)

    def test_binary_digest_rejects_fake_scanner_and_symlink(self):
        lock = collector.read_json(collector.LOCK)
        with tempfile.TemporaryDirectory() as temp:
            fake = Path(temp) / 'trivy'
            fake.write_bytes(b'NOT-THE-PINNED-SCANNER')
            self.deny(collector.verify_tool, fake, lock)
            link = Path(temp) / 'link'
            link.symlink_to(fake)
            self.deny(collector.digest, link)

    def test_process_failure_does_not_disclose_diagnostics(self):
        with tempfile.TemporaryDirectory() as temp:
            log = Path(temp) / 'private.log'
            with patch.object(collector.subprocess, 'run', return_value=subprocess.CompletedProcess(
                    [], 1, stdout=b'PRIVATE-FINDING', stderr=b'PRIVATE-SECRET')):
                with self.assertRaises(collector.EvidenceError) as error:
                    collector.process(['synthetic'], Path(temp), {}, log)
            self.assertEqual(str(error.exception), 'process-failed')
            with patch.object(collector.subprocess, 'run', side_effect=OSError('PRIVATE-PATH')):
                self.deny(collector.process, ['synthetic'], Path(temp), {}, log)

    def test_sources_reject_bad_commit_dirty_checkout_unsafe_paths_missing_build_locks(self):
        paths = ['global.json', 'Directory.Build.props',
                 'infra/containers/azure-development-bootstrap.images.lock.json',
                 'infra/containers/image-evidence-tools.lock.json',
                 'infra/containers/azure-development-bootstrap.Dockerfile',
                 'src/server/hosts/AzureDevelopmentBootstrap/packages.lock.json']
        def runner(command):
            return SHA.encode() if command[1] == 'rev-parse' else b''
        self.deny(collector.source_inputs, ROOT, 'bad-sha', paths, runner)
        self.deny(collector.source_inputs, ROOT, '3' * 40, paths, runner)
        self.deny(collector.source_inputs, ROOT, SHA, [], runner)
        self.deny(collector.source_inputs, ROOT, SHA, ['global.json'], runner)
        self.deny(collector.source_inputs, ROOT, SHA, paths + ['../outside'], runner)
        self.deny(collector.source_inputs, ROOT, SHA, paths + ['/absolute'], runner)
        dirty = lambda command: SHA.encode() if command[1] == 'rev-parse' else b'M tracked'
        self.deny(collector.source_inputs, ROOT, SHA, paths, dirty)

    def test_output_must_be_new_and_outside_repository(self):
        class Args:
            output = ROOT / 'tests'
        self.deny(collector.collect, Args())
        Args.output = ROOT / 'new-private-reports'
        self.deny(collector.collect, Args())

    def test_full_collection_is_private_unsigned_and_policy_unverified(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / 'new-output'
            class Args:
                trivy = Path(temp) / 'synthetic-trivy'
                source_sha = SHA
                input_lock = ['synthetic-lock']
                image = ID
            Args.output = output
            Args.trivy.write_bytes(b'SYNTHETIC')
            def fake_process(command, cwd, env, log):
                self.assertFalse(any(key.startswith('TRIVY') or key.endswith('TOKEN') for key in env))
                if command[0] == 'docker':
                    return json.dumps([{'Id': ID, 'Os': 'linux', 'Architecture': 'amd64'}]).encode()
                if 'version' in command:
                    data = {'Version': VERSION}
                    if (output / 'scanner-cache/db/trivy.db').exists():
                        data['VulnerabilityDB'] = metadata()
                    return json.dumps(data).encode()
                target = Path(command[command.index('--output') + 1])
                if 'image' in command:
                    self.assertIn('vuln,license', command)
                    self.assertIn('--license-full', command)
                    self.assertIn('--list-all-pkgs', command)
                    self.assertNotIn('--severity', command)
                    self.assertNotIn('--ignore-unfixed', command)
                    cache = output / 'scanner-cache/db'
                    cache.mkdir(parents=True)
                    (cache / 'metadata.json').write_text(json.dumps(metadata()))
                    (cache / 'trivy.db').write_bytes(b'SYNTHETIC')
                    target.write_text(json.dumps(report()))
                    log.write_text('PRIVATE-DIAGNOSTIC')
                else:
                    self.assertIn('convert', command)
                    target.write_text(json.dumps(spdx()))
                return b''
            printed = io.StringIO()
            with patch.object(collector, 'verify_tool'), \
                    patch.object(collector, 'source_inputs', return_value={'synthetic-lock': '5' * 64}), \
                    patch.object(collector, 'process', side_effect=fake_process), \
                    contextlib.redirect_stdout(printed):
                summary = collector.collect(Args())
            self.assertEqual(summary['collection'], 'PASS')
            self.assertTrue(all(value == 'NOT_VERIFIED' for value in summary['releaseChecks'].values()))
            self.assertEqual(summary['provenance']['result'], 'NOT_VERIFIED')
            self.assertIsNone(summary['image']['publishedManifestDigest'])
            self.assertNotIn('PRIVATE', printed.getvalue())
            self.assertNotIn('PRIVATE', (output / 'summary.json').read_text())
            self.assertEqual(output.stat().st_mode & 0o777, 0o700)
            for name in ['summary.json', 'inventory.json', 'sbom.spdx.json', 'private-process.log']:
                self.assertEqual((output / name).stat().st_mode & 0o777, 0o600)


if __name__ == '__main__':
    unittest.main()
