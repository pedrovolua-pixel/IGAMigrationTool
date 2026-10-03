"""Pinned, isolated B14 checks. No host/database/network/audit workaround."""
import datetime,json,os,pathlib,subprocess,time
here=pathlib.Path(__file__).resolve().parent
root=here.parents[2]
node='/private/tmp/node-v24.21.0-darwin-arm64/bin/node'
dotnet='/private/tmp/iga-dotnet-10.0.401/dotnet'
npm='/private/tmp/iga-npm-11.20.0/node_modules/npm/bin/npm-cli.js'
project='tests/unit/PlanningTasksComponent.Tests/PlanningTasksComponent.Tests.csproj'
env={'PATH':str(pathlib.Path(node).parent)+':/usr/bin:/bin:/usr/sbin:/sbin','HOME':'/private/tmp/iga-cycle14-b-cli','DOTNET_ROOT':'/private/tmp/iga-dotnet-10.0.401','DOTNET_CLI_HOME':'/private/tmp/iga-cycle14-b-cli','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','IGA_NODE_EXECUTABLE':node,'IGA_B14_FICTIONAL_INPUT_GUARD':'SYNTHETIC-CANARY'}
commands=[
 ('oracle',['python3','tests/unit/PlanningTasksComponent.Tests/oracle.py','--verify'],None),
 ('restore',[dotnet,'restore',project,'--locked-mode','-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=low'],None),
 ('dotnet-format',[dotnet,'format',project,'--no-restore','--verify-no-changes'],{'DOTNET_hostBuilder__reloadConfigOnChange':'false'}),
 ('dotnet-build',[dotnet,'build',project,'--configuration','Release','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-m:1'],None),
 ('frontend-format',[node,'src/web/node_modules/prettier/bin/prettier.cjs','--check','src/web/src/PlanningTasksPanel.tsx','src/web/src/PlanningTasksPanel.css','tests/unit/PlanningTasksComponent.Tests/verify.mjs'],None),
 ('contract',[node,'contracts/local-demo/generate-types.mjs','--check'],None),
 ('typecheck',[node,'src/web/node_modules/typescript/bin/tsc','--noEmit','-p','src/web/tsconfig.json'],None),
 ('frontend-build',[node,npm,'run','build'],None),
 ('component',[dotnet,'tests/unit/PlanningTasksComponent.Tests/bin/Release/net10.0/PlanningTasksComponent.Tests.dll'],None),
 ('secret-frontend',['/private/tmp/iga-parallel-tools/gitleaks','dir','--no-banner','--redact=100','src/web/src'],None),
 ('secret-owned-tests',['/private/tmp/iga-parallel-tools/gitleaks','dir','--no-banner','--redact=100','tests/unit/PlanningTasksComponent.Tests'],None),
 ('diff',['git','diff','--check'],None),
]
records=[]
for name,args,override in commands:
 cwd=root/'src/web' if name=='frontend-build' else root
 actual=args if name!='frontend-build' else [node,npm,'run','build']
 start=datetime.datetime.now(datetime.timezone.utc).isoformat();t=time.monotonic();log=here/('final-'+name+'.log')
 checkenv=env|dict(override or {})
 with log.open('wb') as out:result=subprocess.run(actual,cwd=cwd,env=checkenv,stdout=out,stderr=subprocess.STDOUT,timeout=55)
 record={'name':name,'argv':actual,'cwd':str(cwd),'startedAtUtc':start,'elapsedSeconds':round(time.monotonic()-t,3),'exitCode':result.returncode,'log':str(log.relative_to(root)),'environment':'Explicit isolated OS paths/pinned Node+SDK+own CLI HOME; no inherited provider/PG values. Fictional canary supplied for guard.','overrides':override}
 records.append(record);(here/'executed-checks.json').write_text(json.dumps(records,indent=2)+'\n')
 print(name+': '+str(result.returncode),flush=True)
 if result.returncode:raise SystemExit(result.returncode)
