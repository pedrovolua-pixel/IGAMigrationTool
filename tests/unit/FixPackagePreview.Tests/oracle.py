"""B12 independently authored fixture/canonical/React text oracle; no production code/output reads."""
from pathlib import Path
import json,hashlib,copy,sys
BASE=Path(__file__).parent
PROFILE='synthetic-review-maturity-fix-packages-equal-v1'
TD='a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669'
D='1'*64
SCOPE=dict(customerId='synthetic-customer',projectId='synthetic-project',environmentId='synthetic-environment')
RUN='11111111-2222-4333-8444-555555555555'
def js(s):
 out='"'
 for c in s:
  n=ord(c)
  if c=='\\':out+='\\\\'
  elif c in '\b\f\n\r\t':out+=dict(zip('\b\f\n\r\t',['\\b','\\f','\\n','\\r','\\t']))[c]
  elif n<32 or n>=127 or c in '<>&\'"+`':out+=''.join('\\u%04X'%v for v in ([n] if n<=65535 else [0xd800+((n-65536)>>10),0xdc00+((n-65536)&1023)]))
  else:out+=c
 return out+'"'
def canon(v):
 if isinstance(v,str):return js(v)
 if isinstance(v,dict):return '{'+','.join(js(k)+':'+canon(v[k]) for k in sorted(v))+'}'
 if isinstance(v,list):return '['+','.join(canon(x) for x in v)+']'
 if v is None:return 'null'
 if v is True:return 'true'
 if v is False:return 'false'
 if isinstance(v,int):return str(v)
 raise ValueError('unsupported fixture primitive')
def sha(s):return hashlib.sha256(s.encode()).hexdigest()
def ident(v):return sha(canon(v))
VERSIONS=dict(profileVersion='synthetic-profile-v1',desiredOutcomeVersion=None,scoringAlgorithmVersion='pilot-health-v1',aiPolicyVersion='synthetic-ai-disabled-v1',promptVersion='synthetic-prompt-disabled-v1',modelVersion='synthetic-model-disabled-v1',applicationVersion='synthetic-fix-packages-app-v1',workSchemaVersion='synthetic-run-work-v1',scriptedResultsDigest=D,analysisFixtureDigest=D,maturityFixtureDigest=D,fixPackageTemplateDigest=TD)
CAP=dict(matrixVersion='synthetic-analysis-matrix-v1',stateAtLock='FixtureVerified',productBuild='fixture-product-v1',databaseSchemaBuild='fixture-facts-v1',hotfixSetDigest='synthetic-hotfix-digest',sqlServerBuild='synthetic-sql-build',compatibilityLevel=160,modules=[dict(id='SyntheticOperations',version='synthetic-module-v1'),dict(id='SyntheticSecurity',version='synthetic-module-v1')],queryPackVersion='synthetic-query-pack-v1',normalizationSchemaVersion='synthetic-normalization-v1',ruleCatalogVersion='synthetic-analysis-catalog-v1',lockDigest=D)
AL=dict(scope=SCOPE,packVersion='synthetic-analysis-pack-v1',packDigest=D,presetId='synthetic-analysis-findings-v1',presetVersion='synthetic-evidence-v1',evidenceDigest=D,catalogVersion='synthetic-analysis-catalog-v1',catalogDigest=D,profileId='synthetic-analysis-equal-v1',profileVersion='synthetic-profile-v1',profileDigest=D,compatibility=dict(sourceProduct='SYNTHETIC-ONLY',productVersion='fixture-product-v1',evidenceSchemaVersion='fixture-facts-v1',ruleLanguageVersion='count-predicate-v1'))
SOURCE=dict(scope=SCOPE,runId=RUN,runRevision=7,runState='Scoring',runInputDigest=D,baselineId=AL['presetId'],profileId=PROFILE,frozenVersions=VERSIONS,capabilityLock=CAP,analysisLock=AL,analysisFixtureDigest=D,analysisContentDigest='2'*64,savedCoverageDigest='3'*64,reviewRunId=RUN,reviewRunRevision=7,reviewSnapshotDigest='4'*64)
GW=['Synthetic fixture guidance only; no actual One Identity defect, supported remediation or customer approval is established.','Every option is review-only and unverified. Finding confirmation, rejection or deferral does not review guidance or validate remediation.','Stable identity ordering is not a priority calculation. Recovery guidance does not establish executed or verified restoration.','This is a current detached value, not durable recommendation history. The run remains Scoring and unpublished.']
GU=['Priority calculation, effort estimates and consultant overrides are unavailable.','Customer-approved objectives, roles and approvals are unavailable.','Fix artifacts, recommendation review, task conversion/workflows and CSV export are unavailable.','Customer risk acceptance, validated remediation, reassessment and actual report publication/sharing are unavailable.','No SQL, script, configuration artifact, customer-system execution, external task connector or ROI calculation exists.']
DISC='Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.'
WARN=['Finding confirmation, rejection or deferral does not review artifacts or validate remediation.','Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.','Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.']
UNAVAILABLE=['Consultant artifact review, approval history and content invalidation are unavailable.','Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.','Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.']
TEMPLATES=[dict(templateId='fictional-config-v1',kind='Configuration',text='{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}'),dict(templateId='fictional-script-v1',kind='Script',text="# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),dict(templateId='fictional-sql-v1',kind='Sql',text="-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")]
assert ident(dict(templateVersion='fictional-fix-templates-v1',templates=TEMPLATES))==TD
F=dict(findingId='5'*64,ruleId='FIXTURE-B12',ruleVersion='synthetic-rule-v1',categoryId='SECURITY',severity='Critical',originalTitle='Original <script>never()</script>',presentationTitle='Current café Ω 中文 😀',businessContext='Business "context" & <untrusted>',initialState='Proposed',currentState='Confirmed',findingRevision=2,rootCause='Fictional root =SUM(A1)',occurrences=[],options=[],validationGuidance=['Validate fictional baseline.','No customer validation.'],guidanceReferences=['javascript:alert(1) is inert reference text'],assumptions=['Fictional premise.'],limitations=['No customer evidence.'])
O=dict(occurrenceId=ident(dict(EvidenceDigest=D,Id=F['ruleId'],ObjectId='OBJECT-B12',PresetId=SOURCE['baselineId'],Scope=dict(CustomerId=SCOPE['customerId'],EnvironmentId=SCOPE['environmentId'],ProjectId=SCOPE['projectId']),Version=F['ruleVersion'],runId=RUN)),objectId='OBJECT-B12',objectType='SyntheticControl',moduleId='SyntheticSecurity',originalDigest='7'*64,evidenceReference='https://example.invalid/evidence?x=1&y=2')
F['occurrences']=[O]
for oid in ['compare-new-fixture','inspect-fixture']:
 F['options'].append(dict(scopedOptionId=ident(dict(findingId=F['findingId'],optionId=oid,runId=RUN,scope=SCOPE)),optionId=oid,status='Unverified',text=oid+' $(curl https://example.invalid) only.',prerequisites='Review <img src=x onerror=alert(1)> inert text.',risk='Unverified "risk" & warning.',recoveryGuidance='No restore was attempted.'))

def fixture(empty=False):
 g=dict(schemaVersion='synthetic-recommendation-guidance-v1',status='SyntheticUnverified',source=copy.deepcopy(SOURCE),findings=[] if empty else [copy.deepcopy(F)],warnings=GW,unavailableSections=GU)
 g['contentDigest']=ident(g)
 packages=[]
 for f in g['findings']:
  pid=ident(dict(findingId=f['findingId'],runId=RUN,scope=SCOPE));options=[]
  for o in sorted(f['options'],key=lambda x:x['scopedOptionId']):
   arts=[dict(artifactId=ident(dict(packageId=pid,scopedOptionId=o['scopedOptionId'],templateId=t['templateId'],templateVersion='fictional-fix-templates-v1')),templateId=t['templateId'],kind=t['kind'],status='Unverified',text=t['text']) for t in TEMPLATES]
   options.append(dict(scopedOptionId=o['scopedOptionId'],artifacts=arts))
  packages.append(dict(packageId=pid,findingId=f['findingId'],options=options))
 s=dict(schemaVersion='synthetic-fix-package-preview-v1',status='Unverified',disclaimer=DISC,guidance=g,templateVersion='fictional-fix-templates-v1',templateDigest=TD,templates=TEMPLATES,packages=packages,warnings=WARN+(['No findings were supplied; no fix packages or actions are available.'] if empty else []),unavailableSections=UNAVAILABLE)
 s['canonicalJson']=canon(s);s['contentDigest']=sha(s['canonicalJson'])
 locks=[]
 for name,ver in [('Baseline',SOURCE['baselineId']),('Profile',VERSIONS['profileVersion']),('Rule catalog',CAP['ruleCatalogVersion']),('Capability',CAP['matrixVersion']),('Desired outcomes','disabled'),('Scoring algorithm',VERSIONS['scoringAlgorithmVersion']),('AI policy',VERSIONS['aiPolicyVersion']),('Prompt',VERSIONS['promptVersion']),('Model',VERSIONS['modelVersion']),('Application',VERSIONS['applicationVersion']),('Work schema',VERSIONS['workSchemaVersion'])]:locks.append(dict(name=name,version=ver,sha256=sha(ver)))
 for name,ver,h in [('Complete frozen input','synthetic-input-lock-v1',D),('Exact capability tuple',CAP['matrixVersion'],D),('Scripted result fixture','synthetic-outcomes-v1',D),('Frozen analysis contents','synthetic-analysis-lock-v1',D),('Fictional fix-package templates','fictional-fix-templates-v1',TD)]:locks.append(dict(name=name,version=ver,sha256=h))
 run=dict(schemaVersion=1,demoOnly=True,runId=RUN,revision=7,state='Scoring',createdAtUtc='2026-10-03T00:00:00Z',updatedAtUtc='2026-10-03T00:00:01Z',selection=dict(scopeId='demo-scope',scopeLabel='Synthetic One Identity environment',baselineId=SOURCE['baselineId'],baselineLabel='Fictional findings',profileId=PROFILE,profileLabel='Synthetic consultant review + fictional fix packages · equal weights'),progress=dict(plannedUnits=1,terminalUnits=1,remainingUnits=0,allTerminal=True),coverageCompletionKind='Complete',lockedInputs=locks,warnings=[],stateCounts=[dict(state='Pass' if empty else 'Finding',count=1)],executableCoverage=None,limitations=[],actions=dict(cancel=False,resume=False),cancelRequested=False)
 analysisfindings=[];reviewfindings=[]
 for f in g['findings']:
  analysisfindings.append(dict(id=f['findingId'],title=f['presentationTitle'],category=f['categoryId'],severity=f['severity'],confidencePercent='100',state=f['currentState'],reviewRequired=True,ruleId=f['ruleId'],ruleVersion=f['ruleVersion'],baselineId=SOURCE['baselineId'],rootCauseKey=f['findingId'],objectIds=[O['objectId']],evidenceReferences=[O['evidenceReference']],facts=['Explicit fictional result.'],inferences=[],assumptions=f['assumptions'],impact='Fictional impact.',recommendations=[o['text']+' Prerequisites: '+o['prerequisites']+' Risk: '+o['risk']+' Recovery: '+o['recoveryGuidance'] for o in [f['options'][1],f['options'][0]]],validationGuidance=' '.join(f['validationGuidance']),sources=f['guidanceReferences'],confidenceBand='High',method='Deterministic',likelihood='Fictional likelihood.',limitations=f['limitations'],originalDigests=[O['originalDigest']],outcomeIds=[],rootCause=f['rootCause'],originalTitle=f['originalTitle'],initialState=f['initialState']))
  reviewfindings.append(dict(id=f['findingId'],revision=f['findingRevision'],category=f['categoryId'],state=f['currentState'],initialState=f['initialState'],originalTitle=f['originalTitle'],title=f['presentationTitle'],businessContext=f['businessContext'],originalDigests=[O['originalDigest']],occurrenceIds=[O['occurrenceId']],actions=dict(confirm=True,reject=True,defer=True,comment=True,editPresentation=True),history=[]))
 detail=dict(schemaVersion='synthetic-fix-package-demo-v1',runId=RUN,runRevision=7,runInputDigest=D,baselineId=SOURCE['baselineId'],profileId=PROFILE,status='Ready',reasonCode=None,snapshot=s)
 analysis=dict(schemaVersion=1,demoOnly=True,runId=RUN,runRevision=7,status='Ready',reasonCode=None,algorithmVersion='pilot-health-v1',fixtureDigest=D,contentDigest='8'*64,provisional=None,publishableCurrent=None,categories=[],objectTypes=[],modules=[],outcomes=[],quality=None,findings=analysisfindings,warnings=[],review=dict(schemaVersion=1,demoOnly=True,runId=RUN,runRevision=7,status='Ready',reasonCode=None,snapshotDigest='4'*64,actor='synthetic-consultant',findings=reviewfindings),maturity=None,reviewSnapshotDigest='4'*64,reportDraft=None,recommendationGuidance=dict(status='Ready',reasonCode=None,snapshot=g),aiPreview=None,fixPackages=detail)
 return dict(run=run,analysis=analysis)

def esc(text):
 return str(text).replace('&','&amp;').replace('<','&lt;').replace('>','&gt;').replace('"','&quot;').replace("'",'&#x27;')
def rows(values):return '<dl>'+''.join('<div><dt>'+esc(k)+'</dt><dd>'+esc(v)+'</dd></div>' for k,v in values)+'</dl>'
def listing(values):return '<ul>'+''.join('<li>'+esc(v)+'</li>' for v in values)+'</ul>' if values else '<p>No values supplied.</p>'
def source_rows(value,path=''):
 result=[]
 if isinstance(value,dict):
  for k in sorted(value):result+=source_rows(value[k],path+'.'+k if path else k)
 elif isinstance(value,list):
  for i,v in enumerate(value):result+=source_rows(v,path+'['+str(i)+']')
 else:result.append((path,'Not supplied' if value is None else str(value)))
 return result

def render(fixture):
 d=fixture['analysis']['fixPackages'];s=d['snapshot'];g=s['guidance']
 out='<section class="fix-package-preview" aria-label="Fictional fix packages"><p class="eyebrow">Fictional review-only examples</p><h3 id="fix-package-preview-heading" tabindex="-1">Fictional fix packages</h3><div class="fix-package-warning" role="note"><strong>Unverified · Review-only</strong><p>'+esc(DISC)+'</p><p>Finding decisions do not review artifacts or validate remediation. Stable identity order is not priority or effort.</p></div>'
 if not s['packages']:out+='<p class="fix-package-empty" role="status">No findings were supplied; no fix packages or actions are available. Empty input does not establish healthy coverage or validated remediation.</p>'
 for p in s['packages']:
  f=next(v for v in g['findings'] if v['findingId']==p['findingId'])
  out+='<details class="fix-package-group"><summary>'+esc(f['presentationTitle'])+' · '+esc(f['currentState'])+' · Unverified artifacts</summary><h4>Current finding and immutable original</h4>'+rows([('Package ID',p['packageId']),('Finding ID',f['findingId']),('Current title',f['presentationTitle']),('Current finding state',f['currentState']),('Finding revision',f['findingRevision']),('Business context',f['businessContext'] or 'No business context supplied.'),('Original title',f['originalTitle']),('Original finding state',f['initialState']),('Rule ID',f['ruleId']),('Rule version',f['ruleVersion']),('Category',f['categoryId']),('Severity',f['severity']),('Root cause',f['rootCause'])])
  out+='<details><summary>Original occurrences and evidence provenance</summary><p>Evidence references are inert identifiers; no evidence is resolved or opened.</p>'
  for o in f['occurrences']:out+='<article>'+rows([('Occurrence ID',o['occurrenceId']),('Object ID',o['objectId']),('Object type',o['objectType']),('Module',o['moduleId']),('Original digest',o['originalDigest']),('Evidence reference',o['evidenceReference'])])+'</article>'
  out+='</details>'
  for o in p['options']:
   original=next(v for v in f['options'] if v['scopedOptionId']==o['scopedOptionId'])
   out+='<details class="fix-package-option"><summary>'+esc(original['optionId'])+' · Unverified</summary>'+rows([('Scoped option ID',original['scopedOptionId']),('Original option ID',original['optionId']),('Recommendation status',original['status']),('Original option guidance',original['text']),('Prerequisites',original['prerequisites']),('Risk',original['risk']),('Recovery guidance',original['recoveryGuidance'])])
   for a in o['artifacts']:out+='<article class="fix-package-artifact"><h5>'+esc(a['kind'])+' · Unverified</h5>'+rows([('Artifact ID',a['artifactId']),('Template ID',a['templateId']),('Artifact kind',a['kind']),('Artifact status',a['status'])])+'<pre><code>'+esc(a['text'])+'</code></pre></article>'
   out+='</details>'
  out+='<details><summary>Validation, references, assumptions and limitations</summary>'
  for label,key in [('Validation guidance','validationGuidance'),('Guidance references','guidanceReferences'),('Assumptions','assumptions'),('Finding limitations','limitations')]:out+='<h5>'+label+'</h5>'+listing(f[key])
  out+='</details></details>'
 out+='<details><summary>Current package warnings and unavailable capabilities</summary><h4>Current warnings</h4>'+listing(s['warnings'])+'<h4>Current unavailable capabilities</h4>'+listing(s['unavailableSections'])+'</details>'
 out+='<details><summary>Historical upstream guidance boundary</summary><p>These retained statements describe the earlier guidance projection. Current fictional package limitations are stated separately above.</p>'+rows([('Guidance schema',g['schemaVersion']),('Guidance status',g['status'])])+'<h4>Historical guidance warnings</h4>'+listing(g['warnings'])+'<h4>Historical upstream unavailable capabilities</h4>'+listing(g['unavailableSections'])+'</details>'
 out+='<details><summary>Fixed fictional templates</summary>'+rows([('Template version',s['templateVersion']),('Template catalog digest',s['templateDigest'])])
 for t in s['templates']:out+='<article><h4>'+esc(t['kind'])+' template · Unverified</h4>'+rows([('Template ID',t['templateId']),('Template kind',t['kind'])])+'<pre><code>'+esc(t['text'])+'</code></pre></article>'
 out+='</details><details class="fix-package-source"><summary>Captured source, versions and digests</summary><p>Digests identify this captured fictional value and grant no access. This view does not establish durable artifact review history or a completed assessment.</p>'+rows([('Preview schema',s['schemaVersion']),('Preview status',s['status']),('Package content digest',s['contentDigest']),('Guidance content digest',g['contentDigest']),('Detail schema',d['schemaVersion']),('Detail status',d['status']),('Run ID',d['runId']),('Run revision',d['runRevision']),('Complete frozen input digest',d['runInputDigest']),('Baseline',d['baselineId']),('Profile',d['profileId'])])+rows(source_rows(g['source']))+'</details></section>'
 return out

if __name__=='__main__':
 outputs={};hashes={}
 for name,empty in [('complete',False),('empty',True)]:
  f=fixture(empty);outputs[name+'-fixture.json']=json.dumps(f,ensure_ascii=False,indent=2)+'\n';outputs[name+'-component.html']=render(f)
  hashes[name]=dict(html=sha(outputs[name+'-component.html']),package=f['analysis']['fixPackages']['snapshot']['contentDigest'],guidance=f['analysis']['fixPackages']['snapshot']['guidance']['contentDigest'])
 outputs['oracle-hashes.json']=json.dumps(hashes,indent=2)+'\n'
 for name,text in outputs.items():
  if '--verify' in sys.argv:assert (BASE/name).read_bytes()==text.encode(),name
  else:(BASE/name).write_bytes(text.encode())
 print('B12 independent literal oracle: five exact UTF-8 artifacts '+('verified' if '--verify' in sys.argv else 'authored before actual component execution'))
