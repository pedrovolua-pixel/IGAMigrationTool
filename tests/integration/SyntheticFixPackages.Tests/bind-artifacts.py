"""Bind and independently recompute the final worker execution closure."""
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
TARGET = HERE / 'artifacts.json'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

if sys.argv[1:] == ['--verify']:
    data = json.loads(TARGET.read_text())
    for item in data['files']:
        path = REPO / item['path']
        if not path.is_file() or path.stat().st_size != item['bytes'] or sha(path) != item['sha256']:
            raise SystemExit('FAIL V11 binding: ' + item['path'])
    print('PASS V11 exact closure bindings; files=' + str(len(data['files'])))
    raise SystemExit(0)
if sys.argv[1:]: raise SystemExit('FAIL invalid binding arguments')
if 'PASS V11 independent composition; checks=338' not in (HERE / 'execution.log').read_text():
    raise SystemExit('FAIL final composition transcript required')
browser = REPO / 'tests/e2e/synthetic-fix-packages'
report = json.loads((browser / 'execution.json').read_text())
if report['result'] != 'PASS' or report['checks'] != 4578 or report['harnessSha256'] != sha(browser / 'verify.mjs'):
    raise SystemExit('FAIL final browser source execution required')
if report['inputManifestSha256'] != sha(HERE / 'generated/preview-manifest.json'):
    raise SystemExit('FAIL generated fixture manifest changed')
files = [p for p in HERE.rglob('*') if p.is_file() and not any(x in p.parts for x in ['bin', 'obj', '__pycache__']) and p != TARGET]
files += [p for p in browser.rglob('*') if p.is_file()]
binary = HERE / 'bin/Release/net10.0'
for name in ['normal', 'hostile', 'empty']:
    for suffix in ['-input.json', '-guidance-golden.json', '-fix-golden.json', '-html-golden.html']:
        source = HERE / (name + suffix)
        consumed = binary / (name + suffix)
        if source.read_bytes() != consumed.read_bytes():
            raise SystemExit('FAIL consumed fixture copy changed: ' + name + suffix)
if (HERE / 'expected-manifest.json').read_bytes() != (binary / 'expected-manifest.json').read_bytes():
    raise SystemExit('FAIL consumed expected manifest changed')
files += [p for p in binary.rglob('*') if p.is_file()]
for name in ['RecommendationGuidance', 'SyntheticFixPackages']:
    module = REPO / 'src/server/modules' / name
    files += [p for p in module.iterdir() if p.is_file()]
files += [REPO / x for x in ['global.json', 'Directory.Build.props', 'NuGet.config', '.gitattributes', 'docs/development/cycle11-fictional-fix-package-contract.md', 'plans/active/local-pilot-fix-packages-cycle-11.md', 'skills/verify-feature/SKILL.md', 'architecture/decisions/ADR-0001-pilot-application-shape.md'] if (REPO / x).is_file()]
files = sorted(set(files), key=str)
TARGET.write_text(json.dumps(dict(schemaVersion='v11-execution-bindings-v1', status='PASS',
    sourceAuthority='Worker commit enclosing this metadata; external implementation copies bound by exact source hash and A/B immutable seal',
    files=[dict(path=str(p.relative_to(REPO)), bytes=p.stat().st_size, sha256=sha(p)) for p in files]), indent=2) + '\n')
print('PASS V11 exact closure bindings; files=' + str(len(files)))
