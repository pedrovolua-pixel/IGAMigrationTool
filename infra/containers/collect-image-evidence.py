#!/usr/bin/env python3
"""Collect private developer image inventories; never sign, publish or approve promotion."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
LOCK = ROOT / 'infra/containers/image-evidence-tools.lock.json'
DIGEST = re.compile(r'sha256:[0-9a-f]{64}')
COMMIT = re.compile(r'[0-9a-f]{40}')
MAX_JSON = 64 * 1024 * 1024


class EvidenceError(Exception):
    """Safe public failure category, without scanner findings or process output."""


def require(condition, category):
    if not condition:
        raise EvidenceError(category)


def digest(path):
    require(path.is_file() and not path.is_symlink(), 'missing-or-unsafe-file')
    hasher = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            hasher.update(block)
    return hasher.hexdigest()


def pairs_unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'duplicate-json-key')
        result[key] = value
    return result


def parse_json(raw):
    require(len(raw) <= MAX_JSON, 'oversized-json')
    try:
        return json.loads(raw, object_pairs_hook=pairs_unique,
                          parse_constant=lambda value: (_ for _ in ()).throw(
                              EvidenceError('non-finite-json-number')))
    except (ValueError, UnicodeError, RecursionError):
        raise EvidenceError('invalid-json') from None


def read_json(path):
    require(path.is_file() and not path.is_symlink(), 'missing-or-unsafe-json')
    require(path.stat().st_size <= MAX_JSON, 'oversized-json')
    return parse_json(path.read_bytes())


def utc(value):
    require(isinstance(value, str) and len(value) <= 40, 'invalid-time')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise EvidenceError('invalid-time') from None
    require(parsed.tzinfo is not None and parsed.utcoffset().total_seconds() == 0,
            'non-utc-time')
    return parsed


def process(command, cwd, env, log):
    """Keep protected diagnostics in private files, with generic public errors."""
    try:
        with log.open('ab') as errors:
            result = subprocess.run(command, cwd=cwd, env=env, stdout=subprocess.PIPE,
                                    stderr=errors, timeout=900, check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise EvidenceError('process-unavailable-or-timeout') from None
    require(result.returncode == 0, 'process-failed')
    require(len(result.stdout) <= MAX_JSON, 'oversized-process-output')
    return result.stdout


def source_inputs(root, source_sha, paths, runner):
    require(COMMIT.fullmatch(source_sha) is not None, 'invalid-source-commit')
    actual = runner(['git', 'rev-parse', 'HEAD']).decode().strip()
    require(actual == source_sha, 'source-commit-mismatch')
    require(not runner(['git', 'status', '--porcelain']).strip(), 'dirty-source-checkout')
    require(paths and len(paths) == len(set(paths)), 'missing-or-duplicate-inputs')
    required = {'global.json', 'Directory.Build.props',
                'infra/containers/azure-development-bootstrap.images.lock.json',
                'infra/containers/image-evidence-tools.lock.json'}
    require(required.issubset(paths) and any(p.endswith('.Dockerfile') for p in paths)
            and any(p.endswith('packages.lock.json') for p in paths), 'missing-build-locks')
    result = {}
    for name in paths:
        require(isinstance(name, str) and re.fullmatch(r'[A-Za-z0-9_./-]{1,200}', name),
                'unsafe-input-path')
        relative = Path(name)
        require(not relative.is_absolute() and '..' not in relative.parts,
                'unsafe-input-path')
        path = root / relative
        require(path.resolve().is_relative_to(root.resolve()), 'unsafe-input-path')
        require(not any(p.is_symlink() for p in [path, *path.parents] if p != root.parent),
                'symlink-input')
        runner(['git', 'ls-files', '--error-unmatch', '--', name])
        result[name] = digest(path)
    return result


def verify_tool(path, lock):
    require(lock['schemaVersion'] == 1 and lock['targetPlatform'] == 'linux/amd64',
            'unsupported-tool-lock')
    require(lock['trivy']['version'] == '0.73.0', 'unsupported-scanner-version')
    require(lock['trivy']['databaseRepository'] == 'ghcr.io/aquasecurity/trivy-db:2',
            'unsupported-database-source')
    require(path.is_absolute(), 'absolute-tool-path-required')
    require(digest(path) == lock['trivy']['binarySha256'], 'scanner-binary-mismatch')


def validate_image(data, expected):
    require(DIGEST.fullmatch(expected) is not None and expected != 'sha256:' + '0' * 64,
            'config-digest-required')
    require(isinstance(data, list) and len(data) == 1 and isinstance(data[0], dict),
            'invalid-image-inspect')
    image = data[0]
    require(image.get('Id') == expected, 'image-config-mismatch')
    require(image.get('Os') == 'linux' and image.get('Architecture') == 'amd64',
            'wrong-image-platform')
    refs = image.get('RepoDigests') or []
    require(isinstance(refs, list), 'invalid-repository-digests')
    observed = []
    for ref in refs:
        require(isinstance(ref, str) and len(ref) < 500 and '@' in ref,
                'invalid-repository-digests')
        value = ref.rsplit('@', 1)[1]
        require(DIGEST.fullmatch(value) is not None, 'invalid-repository-digests')
        observed.append(value)
    return {'dockerConfigDigest': expected, 'platform': 'linux/amd64',
            'observedRepositoryManifestDigests': sorted(set(observed)),
            'publishedManifestDigest': None, 'publication': 'NOT_VERIFIED'}


def validate_report(report, image_id, version, collected):
    require(isinstance(report, dict) and report.get('SchemaVersion') == 2,
            'unsupported-report-schema')
    require(report.get('ArtifactType') == 'container_image', 'wrong-report-artifact')
    require(report.get('Metadata', {}).get('ImageID') == image_id,
            'report-image-mismatch')
    require(report.get('Trivy', {}).get('Version') == version, 'report-tool-mismatch')
    require(utc(report.get('CreatedAt')) <= collected, 'future-report-time')
    results = report.get('Results')
    require(isinstance(results, list) and results, 'missing-image-inventory')
    counts = {'packages': 0, 'packagesWithoutLicenseMetadata': 0,
              'vulnerabilities': 0, 'licenseRecords': 0, 'resultTargets': len(results)}
    for result in results:
        require(isinstance(result, dict), 'invalid-inventory-result')
        for key in ['Packages', 'Vulnerabilities', 'Licenses']:
            values = result.get(key) or []
            require(isinstance(values, list) and all(isinstance(v, dict) for v in values),
                    'invalid-inventory-array')
        packages = result.get('Packages') or []
        counts['packages'] += len(packages)
        counts['packagesWithoutLicenseMetadata'] += sum(not p.get('Licenses') for p in packages)
        counts['vulnerabilities'] += len(result.get('Vulnerabilities') or [])
        counts['licenseRecords'] += len(result.get('Licenses') or [])
    require(counts['packages'] > 0, 'empty-package-inventory')
    return counts


def validate_database(version_info, cache, version, collected):
    require(isinstance(version_info, dict) and version_info.get('Version') == version,
            'scanner-version-mismatch')
    metadata = version_info.get('VulnerabilityDB')
    require(isinstance(metadata, dict) and metadata.get('Version') == 2,
            'missing-or-unsupported-database')
    updated = utc(metadata.get('UpdatedAt'))
    downloaded = utc(metadata.get('DownloadedAt'))
    next_update = utc(metadata.get('NextUpdate'))
    require(updated <= collected and downloaded <= collected, 'future-database-time')
    require(downloaded >= updated and next_update > updated, 'invalid-database-clock')
    stored = read_json(cache / 'db/metadata.json')
    require(isinstance(stored, dict) and stored.get('Version') == metadata['Version']
            and all(utc(stored.get(key)) == utc(metadata[key])
                    for key in ['UpdatedAt', 'DownloadedAt', 'NextUpdate']),
            'database-metadata-mismatch')
    require((cache / 'db/trivy.db').is_file() and (cache / 'db/trivy.db').stat().st_size > 0,
            'missing-or-empty-database')
    return {'repository': 'ghcr.io/aquasecurity/trivy-db:2',
            'databaseSha256': digest(cache / 'db/trivy.db'),
            'metadataSha256': digest(cache / 'db/metadata.json'),
            'metadata': metadata, 'freshnessPolicy': 'NOT_VERIFIED'}


def validate_spdx(sbom, version, collected):
    require(isinstance(sbom, dict) and sbom.get('spdxVersion') == 'SPDX-2.3'
            and sbom.get('SPDXID') == 'SPDXRef-DOCUMENT', 'unsupported-sbom-schema')
    creation = sbom.get('creationInfo', {})
    require('Tool: trivy-' + version in creation.get('creators', []), 'sbom-tool-mismatch')
    require(utc(creation.get('created')) <= collected, 'future-sbom-time')
    packages = sbom.get('packages')
    require(isinstance(packages, list) and packages and all(isinstance(p, dict) for p in packages),
            'empty-or-invalid-sbom')
    require(all(isinstance(p.get('SPDXID'), str) and p['SPDXID'].startswith('SPDXRef-')
                for p in packages), 'invalid-sbom-package')
    require(len({p['SPDXID'] for p in packages}) == len(packages), 'duplicate-sbom-package')
    return len(packages)


def write_json(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, sort_keys=True)
        stream.write('\n')
    path.chmod(0o600)


def collect(args):
    # A new external directory prevents accidental public Git inclusion and stale-result reuse.
    output = args.output.absolute()
    require(not output.exists() and not output.is_symlink(), 'output-must-be-new')
    require(not output.resolve().is_relative_to(ROOT.resolve()), 'output-outside-repository-required')
    output.mkdir(mode=0o700)
    output.chmod(0o700)
    previous_umask = os.umask(0o077)
    try:
        cache = output / 'scanner-cache'
        home = output / 'scanner-home'
        home.mkdir()
        config = output / 'scanner.yaml'
        config.write_text('{}\n')
        env = {'PATH': os.environ.get('PATH', '/usr/bin:/bin'), 'HOME': str(home),
               'TMPDIR': str(output), 'LANG': 'C.UTF-8'}
        log = output / 'private-process.log'
        run = lambda command: process(command, output, env, log)
        git_run = lambda command: process(command, ROOT, env, log)
        locks = source_inputs(ROOT, args.source_sha, args.input_lock, git_run)
        lock = read_json(LOCK)
        verify_tool(args.trivy, lock)
        version = lock['trivy']['version']
        image = validate_image(parse_json(run(['docker', 'image', 'inspect', args.image])), args.image)
        common = [str(args.trivy), '--quiet', '--config', str(config), '--cache-dir', str(cache)]
        scanner_version = parse_json(run(common + ['version', '--format', 'json']))
        require(scanner_version.get('Version') == version, 'scanner-version-mismatch')
        report_path = output / 'inventory.json'
        run(common + ['image', '--image-src', 'docker', '--scanners', 'vuln,license',
                      '--list-all-pkgs', '--license-full', '--skip-version-check',
                      '--db-repository', lock['trivy']['databaseRepository'],
                      '--format', 'json', '--output', str(report_path), args.image])
        collected = datetime.now(timezone.utc)
        counts = validate_report(read_json(report_path), args.image, version, collected)
        report_hash = digest(report_path)
        database = validate_database(parse_json(run(common + ['version', '--format', 'json'])),
                                     cache, version, collected)
        sbom_path = output / 'sbom.spdx.json'
        run(common + ['convert', '--format', 'spdx-json', '--output', str(sbom_path), str(report_path)])
        collected = datetime.now(timezone.utc)
        sbom_count = validate_spdx(read_json(sbom_path), version, collected)
        require(digest(report_path) == report_hash, 'inventory-changed-during-conversion')
        require(digest(cache / 'db/trivy.db') == database['databaseSha256']
                and digest(cache / 'db/metadata.json') == database['metadataSha256'],
                'database-changed-during-collection')
        verify_tool(args.trivy, lock)
        # Reinspect the immutable subject and source to detect collection-time drift.
        require(validate_image(parse_json(run(['docker', 'image', 'inspect', args.image])), args.image)
                == image, 'image-changed-during-collection')
        require(source_inputs(ROOT, args.source_sha, args.input_lock, git_run) == locks,
                'source-changed-during-collection')
        summary = {
            'schemaVersion': 1, 'kind': 'LOCAL_DEVELOPER_IMAGE_INVENTORY',
            'collectedUtc': collected.isoformat(), 'sourceCommit': args.source_sha,
            'inputSha256': locks, 'image': image,
            'scanner': {'version': version, 'binarySha256': digest(args.trivy),
                        'toolLockSha256': digest(LOCK), 'publisherSignatureTrust': 'NOT_VERIFIED'},
            'database': database, 'inventoryCounts': counts, 'spdxPackageCount': sbom_count,
            'privateReports': {'inventorySha256': report_hash, 'spdxSha256': digest(sbom_path)},
            'collection': 'PASS',
            'releaseChecks': {name: 'NOT_VERIFIED' for name in
                              ['vulnerabilityAcceptance', 'licenseAcceptance', 'signedProvenance',
                               'sourceToImageBuildBinding', 'restrictedEvidenceStore',
                               'requiredReviewers', 'compatibility', 'privateRegistryPublication',
                               'promotion', 'AzureDeployment']},
            'provenance': {'kind': 'UNSIGNED_COLLECTION_METADATA', 'result': 'NOT_VERIFIED'}
        }
        write_json(output / 'summary.json', summary)
        for path in [config, log, report_path, sbom_path]:
            path.chmod(0o600)
        print('PASS private image inventory collection; promotion and signed provenance NOT VERIFIED')
        return summary
    finally:
        os.umask(previous_umask)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True, help='Actual local Docker sha256 config ID; no tag/registry pull.')
    parser.add_argument('--trivy', type=Path, required=True, help='Absolute verified pinned Linux AMD64 binary.')
    parser.add_argument('--source-sha', required=True)
    parser.add_argument('--input-lock', action='append', required=True, help='Tracked repository build/lock input; repeat.')
    parser.add_argument('--output', type=Path, required=True, help='New private directory outside the repository.')
    args = parser.parse_args()
    try:
        collect(args)
    except (EvidenceError, OSError, KeyError, TypeError, AttributeError, UnicodeError) as error:
        category = str(error) if isinstance(error, EvidenceError) else 'invalid-or-unavailable-input'
        print('NOT VERIFIED image evidence collection: ' + category, file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
