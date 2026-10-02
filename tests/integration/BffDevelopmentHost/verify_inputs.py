#!/usr/bin/env python3
"""Verify the fixed Dockerfile-specific allowlist and immutable reviewed base input."""
import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
EXPECTED = {'global.json', 'Directory.Build.props', 'migrations/identity-sessions/001-initial.sql', 'migrations/identity-sessions/002-authentication-context.sql', 'migrations/identity-sessions/003-atomic-audit.sql', 'migrations/identity-authority/001-authority.sql'}
for directory in ('src/server/hosts/BffFoundation', 'src/server/hosts/BffDevelopmentHost', 'src/server/modules/IdentitySessions', 'src/server/modules/IdentityAuthority', 'src/server/modules/IdentityPolicy'):
    EXPECTED.update(str(p.relative_to(ROOT)) for p in (ROOT / directory).iterdir()
                    if p.is_file() and (p.suffix in ('.cs', '.csproj') or p.name == 'packages.lock.json'))
ignore = (ROOT / 'infra/containers/bff-development.Dockerfile.dockerignore').read_text().splitlines()
assert ignore[0] == '**'
assert all(line.startswith('!') for line in ignore[1:])
allowed = {line[1:] for line in ignore[1:] if not line.endswith('/')}
assert allowed == EXPECTED, 'Only exact required sources/locks/migration may enter context'
assert all('*' not in line for line in ignore[1:]), 'No wildcard source or secret inclusion'
assert len(ignore[1:]) == len(set(ignore[1:])), 'No duplicate allowlist entries'
lock_path = ROOT / 'infra/containers/bff-development.images.lock.json'
lock = json.loads(lock_path.read_text())
assert lock == json.loads((ROOT / 'infra/containers/azure-development-bootstrap.images.lock.json').read_text()), 'Reuse exact verified MCR bases'
helper = ROOT / 'infra/containers/build-bff-development.py'
assert subprocess.run(['python3', str(helper), '--check-inputs-only'], check=True).returncode == 0
for tag in ('remote.azurecr.io/image:tag', 'iga-bff-development:../bad', 'iga-bff-development:UPPER'):
    assert subprocess.run(['python3', str(helper), '--check-inputs-only', '--tag', tag], capture_output=True).returncode != 0
recipe = (ROOT / 'infra/containers/bff-development.Dockerfile').read_text()
# Every embedded module resource must be explicitly present at compile time.
for project in [ROOT / f'src/server/modules/{module}/{module}.csproj' for module in ('IdentitySessions', 'IdentityAuthority', 'IdentityPolicy')]:
    for resource in ET.parse(project).getroot().iter('EmbeddedResource'):
        relative = str((project.parent / resource.attrib['Include']).resolve().relative_to(ROOT))
        assert relative in allowed and f'COPY {relative} {relative}' in recipe, 'Embedded resource must reach the image compiler'

assert 'USER 1654' in recipe and '--locked-mode' in recipe and '--no-restore' in recipe
assert 'sha256:${SDK_IMAGE_DIGEST}' in recipe and 'sha256:${RUNTIME_IMAGE_DIGEST}' in recipe
assert 'ENV ' not in recipe and 'IGA_BFF_' not in recipe and 'COPY . ' not in recipe
with tempfile.TemporaryDirectory(prefix='iga-bff-context-fixture-') as directory:
    # The exact positive allowlist excludes these fixtures by construction.
    for forbidden in ('.env', '.git/config', 'work/customer.json', 'src/server/hosts/BffDevelopmentHost/appsettings.json',
                      'src/server/hosts/BffDevelopmentHost/bin/Release/secrets.json'):
        path = Path(directory, forbidden)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('synthetic fixture only')
        assert forbidden not in allowed
print('PASS exact restricted BFF container context, unchanged verified MCR locks, local-only build target/refused unsafe tags')
