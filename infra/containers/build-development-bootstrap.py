#!/usr/bin/env python3
"""Build an inert local bootstrap image from the reviewed immutable base lock; no registry push."""
import argparse
import json
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tag', default='iga-azure-development-bootstrap:local')
parser.add_argument('--check-inputs-only', action='store_true')
args = parser.parse_args()
if not re.fullmatch(r'iga-azure-development-bootstrap:[a-z0-9][a-z0-9_.-]{0,80}', args.tag):
    parser.error('Use the local bootstrap repository and a bounded tag, not a remote registry.')
lock = json.loads((root / 'infra/containers/azure-development-bootstrap.images.lock.json').read_text())
expected = {'sdk': ('mcr.microsoft.com/dotnet/sdk', '10.0.401-noble'),
            'runtime': ('mcr.microsoft.com/dotnet/aspnet', '10.0.12-noble-chiseled-extra')}
if lock['schemaVersion'] != 1 or lock['targetPlatform'] != 'linux/amd64':
    raise SystemExit('Unsupported build input lock.')
digests = {}
for kind, (repository, tag) in expected.items():
    item = lock[kind]
    if (item['repository'], item['tag']) != (repository, tag):
        raise SystemExit('Build input differs from the approved platform/version.')
    digest = item['manifestDigest']
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', digest) or digest == 'sha256:' + '0' * 64:
        raise SystemExit('Require a real immutable base manifest digest.')
    digests[kind] = digest.removeprefix('sha256:')
if args.check_inputs_only:
    print('PASS locked Microsoft base image inputs and local target tag')
else:
    subprocess.run(['docker', 'build', '--platform', lock['targetPlatform'],
                    '--build-arg', 'SDK_IMAGE_DIGEST=' + digests['sdk'],
                    '--build-arg', 'RUNTIME_IMAGE_DIGEST=' + digests['runtime'],
                    '--tag', args.tag, '--file', 'infra/containers/azure-development-bootstrap.Dockerfile', '.'],
                   cwd=root, check=True)
