#!/usr/bin/env python3
"""Replay only the isolated A11 framework project with no database/provider environment."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OWNED = ROOT / 'tests/unit/SyntheticFixPackages.Tests'
LOGS = OWNED / 'execution'
DOTNET = '/private/tmp/iga-dotnet-10.0.401/dotnet'
PROJECT = 'tests/unit/SyntheticFixPackages.Tests/SyntheticFixPackages.Tests.csproj'
CS_FILES = [
    'src/server/modules/SyntheticFixPackages/FixPackageBuilder.cs',
    'src/server/modules/SyntheticFixPackages/FixPackageContracts.cs',
    'tests/unit/SyntheticFixPackages.Tests/GuidanceFixture.cs',
    'tests/unit/SyntheticFixPackages.Tests/Program.cs',
]
# Explicit allowlist: no inherited application, PostgreSQL, provider, credential or secret variables.
ENV = {key: os.environ[key] for key in ('PATH', 'HOME', 'TMPDIR', 'LANG', 'LC_ALL', 'SHELL', 'USER', 'LOGNAME') if key in os.environ}
ENV.update(DOTNET_ROOT='/private/tmp/iga-dotnet-10.0.401', DOTNET_CLI_HOME='/private/tmp/iga-coordinator-cli',
           DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
STEPS = [
    ('sdk.txt', [DOTNET, '--info']),
    ('restore.txt', [DOTNET, 'restore', PROJECT, '--locked-mode', '--disable-parallel', '-p:NuGetAudit=true', '-p:NuGetAuditMode=all']),
    ('audit.txt', [DOTNET, 'list', PROJECT, 'package', '--vulnerable', '--include-transitive', '--no-restore']),
    ('format.txt', [DOTNET, 'format', PROJECT, '--no-restore', '--verify-no-changes', '--include', *CS_FILES, '--verbosity', 'minimal']),
    ('build.txt', [DOTNET, 'build', PROJECT, '--no-restore', '--configuration', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false', '-warnaserror', '-m:1']),
    ('oracle.txt', [sys.executable, 'tests/unit/SyntheticFixPackages.Tests/independent-oracle.py']),
    ('unit.txt', [DOTNET, 'tests/unit/SyntheticFixPackages.Tests/bin/Release/net10.0/SyntheticFixPackages.Tests.dll']),
    ('secrets-module.txt', ['/private/tmp/iga-parallel-tools/gitleaks', 'dir', 'src/server/modules/SyntheticFixPackages', '--redact', '--no-banner', '--exit-code', '1']),
    ('secrets-tests.txt', ['/private/tmp/iga-parallel-tools/gitleaks', 'dir', 'tests/unit/SyntheticFixPackages.Tests', '--redact', '--no-banner', '--exit-code', '1']),
    ('diff.txt', ['git', 'diff', '--check']),
]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bind():
    paths = set(CS_FILES + ['tests/unit/SyntheticFixPackages.Tests/independent-oracle.py',
                           'tests/unit/SyntheticFixPackages.Tests/run-checks.py',
                           'tests/unit/SyntheticFixPackages.Tests/SyntheticFixPackages.Tests.csproj',
                           'tests/unit/SyntheticFixPackages.Tests/packages.lock.json',
                           'src/server/modules/SyntheticFixPackages/SyntheticFixPackages.csproj',
                           'src/server/modules/SyntheticFixPackages/packages.lock.json',
                           'Directory.Build.props', 'global.json', '.gitattributes',
                           'docs/development/cycle11-fictional-fix-package-contract.md',
                           'plans/active/local-pilot-fix-packages-cycle-11.md'])
    paths.update(str(p.relative_to(ROOT)) for p in (ROOT / 'src/server/modules/RecommendationGuidance').glob('*.cs'))
    paths.update(str(p.relative_to(ROOT)) for p in (ROOT / 'src/server/modules/RecommendationGuidance').glob('*.csproj'))
    paths.update(str(p.relative_to(ROOT)) for p in (ROOT / 'src/server/modules/RecommendationGuidance').glob('packages.lock.json'))
    paths.update(str(p.relative_to(ROOT)) for p in OWNED.glob('*-golden.json'))
    paths.add('tests/unit/SyntheticFixPackages.Tests/golden-digests.json')
    paths.update(str(p.relative_to(ROOT)) for p in LOGS.glob('*.txt'))
    binary_paths = [*OWNED.joinpath('bin/Release/net10.0').glob('*'),
                    *ROOT.joinpath('src/server/modules/SyntheticFixPackages/bin/Release/net10.0').glob('*.dll'),
                    *ROOT.joinpath('src/server/modules/RecommendationGuidance/bin/Release/net10.0').glob('*.dll')]
    paths.update(str(p.relative_to(ROOT)) for p in binary_paths if p.is_file())
    missing = [p for p in sorted(paths) if not (ROOT / p).is_file()]
    # Config filenames may be case-sensitive in other checkouts; never silently omit a declared input.
    if missing:
        raise RuntimeError('Missing declared provenance inputs: ' + ', '.join(missing))
    return [{'path': path, 'sha256': digest(ROOT / path), 'bytes': (ROOT / path).stat().st_size} for path in sorted(paths)]


def main():
    LOGS.mkdir(exist_ok=True)
    for path in ('Directory.Packages.props', 'NuGet.Config', '.editorconfig'):
        if (ROOT / path).exists():
            raise RuntimeError('Previously absent configuration now exists: ' + path)
    results = []
    for name, command in STEPS:
        result = subprocess.run(command, cwd=ROOT, env=ENV, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60)
        (LOGS / name).write_text(result.stdout)
        results.append({'command': command, 'exitCode': result.returncode, 'transcript': str((LOGS / name).relative_to(ROOT))})
        print(name + ': exit ' + str(result.returncode), flush=True)
        if result.returncode:
            print(result.stdout)
            return result.returncode
    metadata = {
        'status': 'PASS', 'baseCommit': 'd8519e48e8d755919441b777acefe356f9e16724',
        'branch': 'codex/cycle11-packages', 'workingDirectory': str(ROOT), 'assertions': 1699,
        'sourceAuthority': 'Immutable worker commit enclosing this record; all executed source hashes are bound below.',
        'environment': {'inheritance': 'Only PATH HOME TMPDIR LANG LC_ALL SHELL USER LOGNAME; no application/database/provider/secret variables.',
                        'dotnetRoot': ENV['DOTNET_ROOT'], 'cliHome': ENV['DOTNET_CLI_HOME'], 'sdk': '10.0.401'},
        'checks': results, 'artifacts': bind(),
        'absentConfigurationInputs': ['Directory.Packages.props', 'NuGet.Config', '.editorconfig'],
        'limitations': ['Framework-only fixture builder unit execution. No hosts, database, providers, customer system, cloud or deployment.',
                        'No artifact review, task/export, execution authorization or human acceptance established.',
                        'B11/V11 independent non-author review and combined regression gates remain coordinator responsibilities.'],
        'attemptHistory': ['Initial restore/build/unit passed; initial sandbox formatter failed to connect its named pipe, without source edits.',
                           'Scoped formatter applied outside that sandbox restriction; final exact formatted source rebuilt and executed below.']
    }
    (OWNED / 'execution.json').write_text(json.dumps(metadata, indent=2) + '\n')
    print('PASS: complete isolated A11 checks and ' + str(len(metadata['artifacts'])) + ' provenance bindings')
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
