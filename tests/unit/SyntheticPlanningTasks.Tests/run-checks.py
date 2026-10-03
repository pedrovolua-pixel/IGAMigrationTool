# Pinned, owned execution recorder. Network audit is intentionally separate coordinator evidence.
import pathlib,os,sys,subprocess,datetime,time,json,hashlib,shutil
root=pathlib.Path(__file__).resolve().parents[3]
p=pathlib.Path(__file__).resolve().parent
name='generation-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S')+'-'+str(time.time_ns())[-7:]
out=p/'execution'/name;out.mkdir(parents=True)
sdk='/private/tmp/iga-dotnet-10.0.401/dotnet'
env={'PATH':'/usr/bin:/bin:/opt/homebrew/bin','HOME':os.environ['HOME'],'DOTNET_ROOT':'/private/tmp/iga-dotnet-10.0.401','DOTNET_CLI_HOME':'/private/tmp/iga-cycle14-a14-cli','NUGET_PACKAGES':'/private/tmp/iga-coordinator-cli/.nuget/packages','DOTNET_hostBuilder__reloadConfigOnChange':'false','DOTNET_NOLOGO':'true','DOTNET_CLI_TELEMETRY_OPTOUT':'1'}
if 'IGA_A14_DATABASE' in os.environ:env['IGA_A14_DATABASE']=os.environ['IGA_A14_DATABASE']
project='tests/unit/SyntheticPlanningTasks.Tests/SyntheticPlanningTasks.Tests.csproj'
commands=[['python3',str(p/'independent-oracle.py')],[sdk,'restore',project,'--locked-mode','--source',env['NUGET_PACKAGES'],'-p:NuGetAudit=false'],[sdk,'build',project,'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-m:1','-c','Release'],[sdk,str(p/'bin/Release/net10.0/SyntheticPlanningTasks.Tests.dll')]+(['--database'] if '--database' in sys.argv else [])]
metadata={'startUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'cwd':str(root),'commands':[],'limits':['Cache-only locked restore: author dependency auditing NOT VERIFIED; bind coordinator actual audited unchanged-lock report separately.','Test-only macOS configuration-watch override; no default startup, host/browser/hard-kill or production claim.'],'inputs':[],'buildSourceHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'dirtyTrackedSource':subprocess.check_output(['git','diff','--name-only'],cwd=root,text=True).splitlines()}
for folder in ['src/server/modules/SyntheticPlanningTasks','src/server/modules/SyntheticFixReview','src/server/modules/SyntheticFixPackages','src/server/modules/RecommendationGuidance','src/server/modules/SyntheticSourceFence','tests/unit/SyntheticPlanningTasks.Tests']:
 for f in sorted((root/folder).glob('*')):
  if f.is_file() and f.suffix in ['.cs','.csproj','.json','.py','.md']:
   data=f.read_bytes();dest=out/'inputs'/pathlib.Path(str(f.relative_to(root))+'.frozen');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data);metadata['inputs'].append({'path':str(f.relative_to(root)),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
for relative in ['Directory.Build.props','.editorconfig','global.json','NuGet.Config','Directory.Build.targets','specs/003-health-assessment/local-planning-task-implementation-contract.md','specs/003-health-assessment/local-planning-task-approval.md']:
 f=root/relative
 if f.is_file():
  data=f.read_bytes();dest=out/'inputs'/pathlib.Path(relative+'.frozen');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data);metadata['inputs'].append({'path':relative,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
f=root/'migrations/planning-tasks/001-initial.sql';dest=out/'inputs'/pathlib.Path(str(f.relative_to(root))+'.frozen');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(f.read_bytes());metadata['inputs'].append({'path':str(f.relative_to(root)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size})
metadata['dotnetBinarySha256']=hashlib.sha256(pathlib.Path(sdk).read_bytes()).hexdigest()
for i,command in enumerate(commands):
 start=time.monotonic();log=out/(str(i)+'.log')
 with log.open('wb') as stream:
  try:run=subprocess.run(command,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=180);code=run.returncode
  except subprocess.TimeoutExpired:code='TIMEOUT'
 entry={'argv':command,'exitCode':code,'seconds':round(time.monotonic()-start,3),'log':str(log.relative_to(root)),'logSha256':hashlib.sha256(log.read_bytes()).hexdigest()};metadata['commands'].append(entry)
 (out/'execution.json').write_text(json.dumps(metadata,indent=2)+'\n')
 print(log.read_text(errors='replace')[-5000:],end='');print('RECORDED',code,str(log))
 if i==2 and code==0:shutil.copytree(p/'bin/Release/net10.0',out/'native',dirs_exist_ok=False)
 if code!=0:sys.exit(1)
closure=p/'bin/Release/net10.0'
if closure.exists() and not (out/'native').exists():shutil.copytree(closure,out/'native',dirs_exist_ok=False)
print('PASS original owned execution packet',out)
