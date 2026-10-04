"""Bind original V13 source/consumed inputs/runtime evidence; never generate expected fixtures."""
import hashlib,json,os,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
WEB=ROOT/'tests/e2e/artifact-review'
OUT=HERE/'artifact-bindings.json'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def add(path,kind):
    path=path.resolve()
    if path.is_file(): entries[str(path)]=dict(path=str(path),kind=kind,bytes=path.stat().st_size,sha256=sha(path))
entries={}
copies=[]
for folder in (HERE,WEB):
    for p in folder.rglob('*'):
        if not p.is_file() or any(x in p.parts for x in ('bin','obj','__pycache__','.native','.host','.frozen')) or p==OUT: continue
        if p.name.endswith('.log'):continue
        add(p,'owned source or fictional evidence')
for folder,kind in [(HERE/'.native','original native transcripts/prior generation'),(WEB/'.host','original failed browser generations/guarded recovery'),(HERE/'bin/Release/net10.0','original exact integration Release closure'),(ROOT/'src/server/hosts/LocalConsultantDemo/bin/Release/net10.0','original exact owned host Release closure'),(ROOT/'src/web/dist','actual frontend assets'),(HERE/'.frozen','original preserved execution closure')]:
    if folder.exists():
        for p in folder.rglob('*'):add(p,kind)
for p in WEB.glob('*.log'): add(p,'original browser transcript')
paths=subprocess.check_output(['git','ls-files','src/server','src/web','contracts','migrations','.gitattributes','Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config'],cwd=ROOT,text=True).splitlines()
for p in paths:
    if '/bin/' not in p and '/obj/' not in p:add(ROOT/p,'consumed source/config')
for p in ('src/web/src/App.tsx','src/web/src/AnalysisView.tsx','src/web/src/useArtifactReview.ts','src/web/src/ArtifactReviewPanel.tsx','src/web/src/ArtifactReviewPanel.css','src/web/src/FixPackagePreview.tsx','src/web/src/FixPackagePreview.css','src/web/package.json','src/web/package-lock.json','src/web/tsconfig.json','src/web/vite.config.ts'):
    add(ROOT/p,'consumed frontend source/config')
# Full executed external tool/runtime dependencies are retained at these shared
# immutable paths; the own-runtime archive excludes their shared originals.
for root,kind in [
(Path('/private/tmp/iga-dotnet-10.0.401/shared'),'executed .NET shared runtime closure'),
(Path('/private/tmp/iga-dotnet-10.0.401/host/fxr'),'executed .NET hostfxr closure'),
(Path('/private/tmp/iga-cycle05-preserved-browser/node_modules/playwright'),'executed Playwright package'),
(Path('/private/tmp/iga-cycle05-preserved-browser/node_modules/playwright-core'),'executed Playwright core package'),
(Path('/private/tmp/iga-cycle05-preserved-browser/node_modules/axe-core'),'executed axe package'),
(Path('/Users/pedrovolu/Library/Caches/ms-playwright/chromium_headless_shell-1208/chrome-headless-shell-mac-arm64'),'executed Chromium closure')]:
    for p in root.rglob('*'):add(p,kind)
for p in ('/private/tmp/iga-dotnet-10.0.401/dotnet','/private/tmp/node-v24.21.0-darwin-arm64/bin/node','/private/tmp/iga-parallel-tools/gitleaks'):
    add(Path(p),'executed pinned tool binary')
for p in ROOT.joinpath('src/web').rglob('*'):
    if p.is_file() and not any(part in p.parts for part in ('node_modules','dist')):add(p,'complete consumed frontend input closure')
for p in HERE.glob('*.json'):
    if p.name in ('execution.json','artifact-bindings.json'):continue
    original=HERE/'bin/Release/net10.0'/p.name
    if original.exists() and sha(original)!=sha(p):raise SystemExit('FAIL consumed fixture copy mismatch: '+p.name)
    if original.exists():copies.append(dict(source=str(p),consumed=str(original),sha256=sha(p)))
proof=dict(schema='v13-original-artifact-bindings-v1',sourceCheckpoint=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),workingDirectory=str(ROOT),bindings=list(entries.values()),consumedFixtureCopyEqualities=copies,limitations=['sourceCheckpoint is the immutable predecessor checkpoint; final owned Git seal recorded externally to avoid self reference','Original failed generations remain separately bound; functional oracles never derive from observed captures'])
OUT.write_text(json.dumps(proof,indent=2)+'\n')
for item in proof['bindings']:
    if sha(Path(item['path']))!=item['sha256']:raise SystemExit('FAIL binding changed')
print('PASS V13 original artifact bindings; files='+str(len(entries))+'; copied-fixtures='+str(len(copies)))
