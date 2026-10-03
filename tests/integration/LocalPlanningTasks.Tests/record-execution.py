#!/usr/bin/env python3
"""Regenerate owned execution metadata from original payload-free result logs."""
import hashlib,json,pathlib,subprocess
root=pathlib.Path(__file__).resolve().parents[3]
owned=root/'tests/integration/LocalPlanningTasks.Tests';browser=root/'tests/e2e/planning-tasks'
for name,count in [('portable-final-corrected.log',702),('postgres-final-corrected.log',2542)]:
    assert ('PASS V14 independent planning tasks; checks='+str(count)) in (owned/'.native'/name).read_text()
report=json.loads((browser/'execution.json').read_text());assert report['status']=='PASS' and report['checks']==13626
assert len(report['accessibility'])==3 and all(not x['violations'] and not x['incomplete'] for x in report['accessibility'])
assert '0 Warning(s)' in (owned/'.native/build-final-corrected.log').read_text() and '0 Error(s)' in (owned/'.native/build-final-corrected.log').read_text()
assert 'Format complete' in (owned/'.native/format-final-corrected.log').read_text()
audit=json.loads((owned/'.native/package-audit-final-corrected.json').read_text());assert not any(f.get('topLevelPackages') or f.get('transitivePackages') for p in audit['projects'] for f in p.get('frameworks',[]))
launcher='python3 tests/integration/LocalPlanningTasks.Tests/run-isolated.py /private/tmp/iga-dotnet-10.0.401/dotnet '
project='tests/integration/LocalPlanningTasks.Tests/LocalPlanningTasks.Tests.csproj'
dll='tests/integration/LocalPlanningTasks.Tests/bin/Release/net10.0/LocalPlanningTasks.IntegrationTests.dll'
value={
 'schema':'v14-independent-execution-v1','status':'PASS scoped local synthetic verification',
 'executedSourceCommit':subprocess.check_output(['git','rev-parse','5072dbe'],cwd=root,text=True).strip(),
 'domainDependency':'cacb42b53cb83c24cc3ef118788aa31555edf76b','presentationDependency':'66e0071','coordinatorRuntimeReference':'9ede79f',
 'engineeringContractSha256':'f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2','workingDirectory':str(root),
 'checks':{'portable':702,'portablePlusDedicatedPostgres':2542,'inclusiveCountsAreNotAdditive':True,'independentPythonExpectedFiles':41,'independentJavaScriptFullSourceIdentityCommand':56,'actualChromiumBrowser':13626,'axeViewportRecords':3,'axeViolations':0,'axeIncomplete':0,'originalScreenshots':9},
 'commands':{
  'restore':launcher+'restore '+project+' --locked-mode --disable-parallel --verbosity minimal',
  'format':launcher+'format '+project+' --verify-no-changes --no-restore --verbosity normal',
  'build':launcher+'build '+project+' --no-restore --configuration Release --disable-build-servers -p:UseSharedCompilation=false -m:1',
  'portable':launcher+dll+' --portable','postgres':launcher+dll,
  'browser':'/private/tmp/node-v24.21.0-darwin-arm64/bin/node tests/e2e/planning-tasks/verify.mjs',
  'pythonOracle':'python3 tests/integration/LocalPlanningTasks.Tests/oracle.py',
  'javascriptOracle':'/private/tmp/node-v24.21.0-darwin-arm64/bin/node tests/e2e/planning-tasks/verify-oracles.mjs'},
 'environment':{'strategy':'closed allowlist before execution/tool loading; no inherited bypass marker','domainDatabase':'iga_synthetic_cycle14_v14_domain','browserDatabase':'iga_synthetic_cycle14_v14_browser','databaseBoundary':'only127.0.0.1:55433/existing role iga_synthetic/duplicate-free exact keys; no credentials, role/HBA/cluster changes or older database mutations','hostPort':5183,'hostLeaseReleased':True,'temporaryConfigurationWatchOverride':True,'defaultMacStartupVerified':False},
 'requirementEvidence':{
  'TC14-T01/T03/T08':'four real saved presets/full source/three selected artifacts plus ten historical profiles omitted additive member',
  'TC14-T02/T07/T12':'actual HTTP closed body/authority/invalid UTF16 and UTF8/route16KiB versus historical4096; native forged source/current/history/integrity and source-metadata suppression',
  'TC14-T03/T04/T05/T09':'independent full byte recipes, stable scoped identities, separate workflow/freshness, withdrawal/rereview/unchanged-run finding refresh, read-only versus successful-command highwater',
  'TC14-T04/T05/T06':'real concurrent Create/revision/UUID replay, shared run writer fences, failed/cancelled transaction rollback, corrupt rows/schema denial/restoration, reconnect and host restart',
  'TC14-T06/T10':'lost/malformed committed receipts exact frozen retry, same-run review epoch and run-switch held response completion fences, retained volatile drafts, actual Enter/Space eight actions and exact heading/error focus',
  'TC14-T09/T10/T11':'complete current/history UI parity, literal hostile controls, strict inert nodes/network/noeffects,1440/390/320 reflow, three axe subset checks and original artifacts/health invariance'},
 'sourceSecurityScan':{'tool':'gitleaks8.30.1 default rules, no custom config/ignores','result':'PASS zero findings','scope':'90 immutable verifier-owned source/fictional fixture/metadata/screenshot input copies; later binder/recorder metadata-only hash delta is separately covered by coordinator final full tracked-source scan. Raw ignored observations are never staged.','scopeProof':'tests/integration/LocalPlanningTasks.Tests/.native/scanner-source-scope.json','report':'tests/integration/LocalPlanningTasks.Tests/.native/secrets-final.json'},
 'postSealTelemetrySupplement':{'predecessorSeal':'db90b057dc46ec8f0fa1e969ec2b07573de99c85','change':'Only escaped telemetry absence and portable scope prose; unchanged original native/browser/runtime source and outputs.','proof':'tests/e2e/planning-tasks/telemetry.json','predecessorMapping':'tests/integration/LocalPlanningTasks.Tests/.frozen/telemetry-followup-predecessor/predecessor.json'},
 'earlierGenerations':[],
 'provenanceLimitations':[
  'Initial audited restore and first invalid Vite option/Python-cache errors had tool output but no original raw log; no transcript reconstruction claim.',
  'Interim portable-final/postgres-final logs preceded helper rebuild; that interim original test DLL was not frozen and is unavailable. Final corrected source5072 and full Release binaries are independently frozen.',
  'First two failed browser generations have source/runtime/native summary/report, but initial partial owned-host stdout was not written by those harness versions. Later originals retain raw owned host telemetry.',
  'A historical-prefix correction is independently source-reviewed and final real corpus executes it; the author-owned original forked-prefix negative is separately owned evidence, not relabeled as a V test.',
  'An initial metadata assembly failed at an unescaped apostrophe before writing any execution metadata; corrected recorder independently validates actual logs.'],
 'limitations':[
  'Chromium145 macOS automated engineering subset only; Windows/manual screen-reader/deployed sandbox/production identity/full product gates NOT VERIFIED.',
  'No customer/provider/execution/export/apply/import actions; planning completion remains review-only and never health/remediation verification.',
  'Intercepted source-unavailable UI is separately identified; actual persisted malformed JSON metadata recovery is native host-service execution.',
  'Observed captures are ignored executed evidence and never expected oracles; no dependency/security policy/ignore changes.']}
for folder in (owned/'.frozen',browser/'.frozen'):
    for file in sorted(folder.glob('*/closure.json')):
        data=json.loads(file.read_text());value['earlierGenerations'].append({'path':str(file),'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),'bindings':len(data['bindings'])})
value['nativeLogBindings']=[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size} for p in sorted((owned/'.native').iterdir()) if p.is_file()]
(owned/'execution.json').write_text(json.dumps(value,indent=2)+'\n')
print('PASS V14 actual-log execution metadata; portable=702; inclusivePG=2542; browser=13626')
