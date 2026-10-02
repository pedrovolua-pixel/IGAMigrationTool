"""Bind exact V9 definitions, reviewed implementation, executed DLLs and transcripts."""
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
log=ROOT/'execution.log'
if not log.exists() or 'independent V9 composition assertions passed.' not in log.read_text() or 'FAIL' in log.read_text(): raise SystemExit('FAIL V9 successful composition transcript required')
files=list(ROOT.glob('*.cs'))+list(ROOT.glob('*oracle.py'))+list(ROOT.glob('*golden*'))+[ROOT/'packet-input.json',ROOT/'provider-output.json',ROOT/'SyntheticAiPreview.Tests.csproj',ROOT/'packages.lock.json',ROOT/'bind-artifacts.py',ROOT/'execution.json',ROOT/'restore.log',ROOT/'build.log',ROOT/'format.log',ROOT/'format-write.log',log]
files += list((REPO/'src/server/modules/SyntheticAiValidation').glob('*.cs'))+list((REPO/'src/server/modules/SyntheticAiPreview').glob('*.cs'))+[REPO/'specs/003-health-assessment/synthetic-ai-preview-contract.md']
files += list((ROOT/'bin/Release/net10.0').glob('*.dll'))
files += [REPO/'src/server/modules/SyntheticAiPreview/SyntheticAiPreview.csproj', REPO/'src/server/modules/SyntheticAiPreview/packages.lock.json', REPO/'src/server/modules/SyntheticAiValidation/SyntheticAiValidation.csproj', REPO/'src/server/modules/SyntheticAiValidation/packages.lock.json']
files += list((REPO/'tests/e2e/synthetic-ai-preview').glob('*.mjs'))+list((REPO/'tests/e2e/synthetic-ai-preview').glob('*.png'))+[REPO/'tests/e2e/synthetic-ai-preview/execution.json',REPO/'tests/e2e/synthetic-ai-preview/execution.log',REPO/'tests/e2e/synthetic-ai-preview/format.log']
files=sorted(set(files),key=lambda p:str(p))
(ROOT/'artifacts.json').write_text(json.dumps({'schemaVersion':'v9-execution-bindings-v1','status':'PASS','files':[{'path':str(p.relative_to(REPO)),'sha256':sha(p)} for p in files]},indent=2)+'\n')
print('PASS V9 exact definition/source/execution bindings; files='+str(len(files)))
