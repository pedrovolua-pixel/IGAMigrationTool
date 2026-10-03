"""Capture actual scoped Phase1B AI checks and bind exact source/binary/log bytes."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

owned = Path(__file__).resolve().parent
root = owned.parents[2]
dotnet = '/private/tmp/iga-dotnet-10.0.401/dotnet'
unit = 'tests/unit/SyntheticAiExecution.Tests/SyntheticAiExecution.Tests.csproj'
integration = 'tests/integration/SyntheticAiExecution.Tests/SyntheticAiExecution.Tests.csproj'
module = 'src/server/modules/SyntheticAiExecution/SyntheticAiExecution.csproj'
env = {key: value for key, value in os.environ.items() if key in ['PATH', 'HOME', 'TMPDIR', 'LANG', 'LC_ALL', 'DOTNET_ROOT']}
env.update({'DOTNET_CLI_TELEMETRY_OPTOUT':'1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE':'1', 'COMPlus_DbgEnableMiniDump':'0'})
commands = []
for project in [unit, integration]:
    commands.append(('restore-' + Path(project).parent.parts[-2], [dotnet, 'restore', project, '--locked-mode', '-p:NuGetAudit=true', '-p:NuGetAuditMode=all', '-p:NuGetAuditLevel=low']))
for project in [module, unit, integration]:
    include = [str(p.relative_to(root)) for p in sorted((root / project).parent.glob('*.cs'))]
    commands.append(('format-' + str(len(commands)), [dotnet, 'format', project, '--verify-no-changes', '--no-restore', '--include', *include]))
for project in [unit, integration]:
    commands.append(('build-' + Path(project).parent.parts[-2], [dotnet, 'build', project, '--no-restore', '--configuration', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false', '-m:1']))
commands += [('unit', [dotnet, 'tests/unit/SyntheticAiExecution.Tests/bin/Release/net10.0/SyntheticAiExecution.Tests.dll']), ('integration', [dotnet, 'tests/integration/SyntheticAiExecution.Tests/bin/Release/net10.0/SyntheticAiExecution.Tests.dll']), ('diff', ['git', 'diff', '--check'])]
for path in ['src/server/modules/SyntheticAiExecution', 'migrations/synthetic-ai-execution', 'tests/unit/SyntheticAiExecution.Tests', 'tests/integration/SyntheticAiExecution.Tests']:
    commands.append(('secrets-' + str(len(commands)), ['/private/tmp/iga-parallel-tools/gitleaks', 'dir', path, '--no-banner', '--redact']))
results = []
log = owned / 'verification.log'
with log.open('wb') as transcript:
    for name, command in commands:
        transcript.write(('COMMAND ' + json.dumps(command) + '\n').encode()); transcript.flush()
        result = subprocess.run(command, cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        transcript.write(result.stdout); transcript.write(('EXIT ' + str(result.returncode) + '\n').encode()); transcript.flush()
        results.append({'name':name, 'command':command, 'exitCode':result.returncode})
        print(name + ': exit ' + str(result.returncode), flush=True)
        if result.returncode:
            sys.stdout.buffer.write(result.stdout); sys.stdout.flush(); raise SystemExit(result.returncode)

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def bind(paths): return [{'path':str(p.relative_to(root)), 'sha256':digest(p)} for p in sorted(set(paths))]
source=[]
for name in ['SyntheticAiExecution','SyntheticAiValidation','AssessmentCoverage','AssessmentScoring','SyntheticSourceFence']:
    source.extend(p for p in (root/'src/server/modules'/name).glob('*') if p.suffix in ['.cs','.csproj','.json','.md'])
for name in ['unit','integration']:
    source.extend(p for p in (root/'tests'/name/'SyntheticAiExecution.Tests').glob('*') if p.suffix in ['.cs','.csproj','.py','.md'] or p.name=='packages.lock.json')
source.extend((root/'migrations/synthetic-ai-execution').glob('*.sql'))
source.extend(root/p for p in ['Directory.Build.props','global.json','specs/003-health-assessment/local-phase1b-approval.md','specs/003-health-assessment/local-phase1b-engineering-contract.md','specs/003-health-assessment/local-phase1b-ai-contract-proposal.md','specs/003-health-assessment/local-phase1b-ai-test-plan.md'])
binaries=[]
for name in ['unit','integration']:
    binaries.extend(p for p in (root/'tests'/name/'SyntheticAiExecution.Tests/bin/Release/net10.0').glob('*') if p.suffix=='.dll' or p.name.endswith(('.deps.json','.runtimeconfig.json')))
metadata={'schemaVersion':'synthetic-phase1b-ai-executed-evidence-v1','status':'PASS','implementationCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'sdk':subprocess.check_output([dotnet,'--version'],text=True,env=env).strip(),'database':'iga_synthetic_phase1b_ai_tests','postgres':'existing loopback127.0.0.1:55433, useriga_synthetic; no cluster/role/config changes','commands':results,'sourceBinding':bind(source),'executedBinaryBinding':bind(binaries),'transcriptBinding':bind([log]),'limitations':['Bounded local synthetic fake provider only; no network/provider credentials/customer evidence','Coordinator owns combined host/checkpoint/UI/export integration and all historical profile closure','G1–G9 and production identity/scale/provider/accessibility/latency remain separately unverified']}
(owned/'artifacts.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('Bound exact source, binaries and actual transcript.',flush=True)
