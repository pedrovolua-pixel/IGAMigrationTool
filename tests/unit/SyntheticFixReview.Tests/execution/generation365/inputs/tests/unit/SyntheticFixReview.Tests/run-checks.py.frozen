"""Owned hermetic launcher. No provider/customer/PG environment is inherited."""
import os,pathlib,subprocess,sys
root=pathlib.Path(__file__).resolve().parents[3]
project='tests/unit/SyntheticFixReview.Tests/SyntheticFixReview.Tests.csproj'
sdk='/private/tmp/iga-dotnet-10.0.401/dotnet'
env={key:os.environ[key] for key in ('PATH','HOME','TMPDIR','USER','LANG','LC_ALL') if key in os.environ}
env.update(DOTNET_ROOT='/private/tmp/iga-dotnet-10.0.401',DOTNET_CLI_HOME='/private/tmp/iga-cycle13-a-cli',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_hostBuilder__reloadConfigOnChange='false')
commands={
 'restore':[sdk,'restore',project,'--locked-mode','-p:NuGetAudit=true','-p:NuGetAuditMode=all','--disable-parallel'],
 'audit':[sdk,'list',project,'package','--vulnerable','--include-transitive','--no-restore'],
 'build':[sdk,'build',project,'-c','Release','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-m:1'],
 'portable':[sdk,str(root/'tests/unit/SyntheticFixReview.Tests/bin/Release/net10.0/SyntheticFixReview.Tests.dll')],
 'database':[sdk,str(root/'tests/unit/SyntheticFixReview.Tests/bin/Release/net10.0/SyntheticFixReview.Tests.dll'),'--database'],
 'format':[sdk,'format',project,'--no-restore','--verify-no-changes','--include','src/server/modules/SyntheticFixReview','tests/unit/SyntheticFixReview.Tests','--verbosity','minimal'],
 'format-apply':[sdk,'format',project,'--no-restore','--include','src/server/modules/SyntheticFixReview','tests/unit/SyntheticFixReview.Tests','--verbosity','minimal'],
 'oracle':['python3','tests/unit/SyntheticFixReview.Tests/independent-oracle.py'],
}
name=sys.argv[1]
if name=='database':
 for key in ('IGA_A13_DATABASE','IGA_A13_ORIGINAL_DATABASE'):
  if key in os.environ:env[key]=os.environ[key]
path=root/'tests/unit/SyntheticFixReview.Tests/execution'/f'{name}.txt'
with path.open('w') as output:
 result=subprocess.run(commands[name],cwd=root,env=env,stdout=output,stderr=subprocess.STDOUT)
print(f'{name}: exit {result.returncode}; exact transcript {path}')
raise SystemExit(result.returncode)
