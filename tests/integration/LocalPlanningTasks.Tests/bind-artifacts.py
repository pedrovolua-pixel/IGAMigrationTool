#!/usr/bin/env python3
"""Regenerate the verifier-owned provenance manifest after final docs and evidence freeze."""
import hashlib
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[3]
OWNED = ROOT / 'tests/integration/LocalPlanningTasks.Tests'
BROWSER = ROOT / 'tests/e2e/planning-tasks'
OUTPUT = OWNED / 'artifact-bindings.json'
items = {}
links = {}
def bind(path, kind):
    path = pathlib.Path(path)
    if path.is_file() and path != OUTPUT:
        if path.is_symlink(): links[str(path)] = {'path':str(path),'linkTarget':str(path.readlink()),'resolvedPath':str(path.resolve())}
        items[str(path)] = {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'bytes': path.stat().st_size, 'kind': kind}
def tree(path, kind):
    path = pathlib.Path(path)
    assert path.exists(), 'binding_input_missing'
    for file in sorted(path.rglob('*')):
        if file.is_file(): bind(file, kind)
# Every tracked consumed server/frontend/migration input; no transitive source import omitted.
tracked = subprocess.check_output(['git', 'ls-files', 'src/server', 'src/web', 'src/contracts', 'migrations', 'Directory.Build.props', 'Directory.Build.targets', 'global.json', 'IgaMigrationTool.slnx', 'specs/003-health-assessment/local-planning-task-implementation-contract.md', 'specs/003-health-assessment/local-planning-task-approval.md'], cwd=ROOT, text=True).splitlines()
for name in tracked: bind(ROOT/name, 'consumed-source-or-build-contract')
for folder in (OWNED, BROWSER):
    for file in folder.iterdir():
        if file.is_file(): bind(file, 'owned-source-or-final-evidence')
    for subdir in ('preauthor','reviews','.native','.frozen','.host'):
        if (folder/subdir).exists(): tree(folder/subdir, 'original-executed-or-frozen-evidence')
tree(OWNED/'bin/Release/net10.0', 'original-integration-release')
tree(ROOT/'src/server/hosts/LocalConsultantDemo/bin/Release/net10.0', 'original-host-release')
tree(ROOT/'src/web/dist', 'original-served-frontend-assets')
# Exact consumed expected fixture copies, rather than only their source counterparts.
copies=[]
for source in sorted(OWNED.glob('*.json')):
    if source.name in ('execution.json','artifact-bindings.json'): continue
    copied=OWNED/'bin/Release/net10.0'/source.name
    assert copied.is_file(), 'consumed_fixture_missing'
    assert source.read_bytes()==copied.read_bytes(), 'consumed_fixture_bytes_differ'
    copies.append({'source':str(source),'consumedCopy':str(copied),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
# Executed SDK/hostfxr/runtime/compiler/formatter and test-tool input closures are external,
# not newly installed packages; the coordinator preserves their existing runtime roots separately.
sdk=pathlib.Path('/private/tmp/iga-dotnet-10.0.401')
for path in (sdk/'dotnet', *sorted((sdk/'sdk/10.0.401').glob('*'))):
    if path.is_file(): bind(path,'external-existing-dotnet-sdk')
for path in (sdk/'host/fxr', sdk/'shared', sdk/'sdk/10.0.401/Roslyn', sdk/'sdk/10.0.401/Sdks/Microsoft.NET.Sdk', sdk/'sdk/10.0.401/DotnetTools/dotnet-format'):
    tree(path,'external-existing-dotnet-runtime-or-build-tool')
bind('/private/tmp/node-v24.21.0-darwin-arm64/bin/node','external-existing-node-runtime')
frontendDependencies = ROOT/'src/web/node_modules'
assert frontendDependencies.is_symlink(), 'approved_readonly_dependency_alias_missing'
links[str(frontendDependencies)] = {'path':str(frontendDependencies),'linkTarget':str(frontendDependencies.readlink()),'resolvedPath':str(frontendDependencies.resolve()),'mode':'approved read-only dependency borrow; no install/mutation'}
tree(frontendDependencies.resolve(), 'external-existing-full-frontend-build-toolchain')
tree('/private/tmp/iga-cycle14-v14-final-secret-scope','immutable-original-scanner-input-copy')
for path in ('/private/tmp/iga-cycle05-preserved-browser/node_modules/playwright','/private/tmp/iga-cycle05-preserved-browser/node_modules/playwright-core','/private/tmp/iga-cycle05-preserved-browser/node_modules/axe-core','/Users/pedrovolu/Library/Caches/ms-playwright/chromium_headless_shell-1208/chrome-headless-shell-mac-arm64'):
    tree(path,'external-existing-browser-test-runtime')
manifest={'schema':'v14-original-artifact-bindings-v1','sourceHeadCheckpoint':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'selfBindingExcluded':True,'observedValuesAreNeverExpectedOracles':True,'externalRuntimePreservation':'Existing absolute runtime roots bound below; verifier does not claim a newly copied SDK/browser archive. Coordinator must preserve these roots or their verified existing bundle independently.','bindings':[items[k] for k in sorted(items)],'consumedCopyEqualities':copies,'linkTopology':[links[k] for k in sorted(links)]}
OUTPUT.write_text(json.dumps(manifest,indent=2)+'\n')
for item in manifest['bindings']:
    file=pathlib.Path(item['path']);assert file.stat().st_size==item['bytes'] and hashlib.sha256(file.read_bytes()).hexdigest()==item['sha256']
print(json.dumps({'bindings':len(items),'consumedCopies':len(copies),'manifest':str(OUTPUT),'sha256':hashlib.sha256(OUTPUT.read_bytes()).hexdigest()}))
