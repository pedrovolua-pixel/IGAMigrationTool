#!/usr/bin/env python3
"""Bind original V12 source, consumed Release copies, outputs and browser evidence."""
import hashlib,json,pathlib,subprocess
root=pathlib.Path(__file__).resolve().parents[3]
owned=pathlib.Path(__file__).resolve().parent
browser=root/'tests/e2e/local-fix-packages'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
files=set()
for base in (owned,browser):
 for p in base.rglob('*'):
  if p.is_file() and 'obj' not in p.parts and p != owned/'artifact-bindings.json':files.add(p)
# Dependencies are explicitly supplied by the coordinator. Preserve original
# consumed bytes before a subsequent coordinator rebuild can overwrite them.
consumed=owned/'execution/consumed-source'
if consumed.exists():
 for p in consumed.rglob('*'):
  if p.is_file():files.add(p)
for base in (root/'src/web/dist',root/'src/server/hosts/LocalConsultantDemo/bin/Release/net10.0'):
 if base.exists():
  for p in base.rglob('*'):
   if p.is_file():files.add(p)
# The already installed pinned toolchain is read-only; bind its actual browser
# library closure and selected executable/tool bytes used by these checks.
for base in [pathlib.Path('/private/tmp/iga-cycle05-preserved-browser/node_modules')/name for name in ('playwright','playwright-core','axe-core')]:
 if base.exists():
  for p in base.rglob('*'):
   if p.is_file():files.add(p)
for p in map(pathlib.Path, ['/private/tmp/node-v24.21.0-darwin-arm64/bin/node','/private/tmp/iga-dotnet-10.0.401/dotnet','/private/tmp/iga-parallel-tools/gitleaks','/Users/pedrovolu/Library/Caches/ms-playwright/chromium_headless_shell-1208/chrome-headless-shell-mac-arm64/chrome-headless-shell','/private/tmp/iga-cycle12-coordinator/src/web/node_modules/prettier/bin/prettier.cjs']):
 if p.exists():files.add(p)
records=[{'path':str(p),'sha256':digest(p),'bytes':p.stat().st_size} for p in sorted(files)]
result={'schema':'v12-original-artifact-bindings-v1','originalCheckout':str(root),'sourceHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'bindings':records}
output=owned/'artifact-bindings.json'
output.write_text(json.dumps(result,indent=2)+'\n')
for record in records:
 p=pathlib.Path(record['path']);assert digest(p)==record['sha256']
print('PASS V12 bindings='+str(len(records)))
