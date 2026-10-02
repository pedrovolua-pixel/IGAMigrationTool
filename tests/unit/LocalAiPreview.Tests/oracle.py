"""Independent fixed primitive oracle. No implementation/template/helper reads or executable imports."""
import hashlib,json
from pathlib import Path
BASE=Path(__file__).parent

def string(s):
 out='"'
 for c in s:
  n=ord(c)
  if c=='\\':out+='\\\\'
  elif c in '\b\f\n\r\t':out+=dict(zip('\b\f\n\r\t',['\\b','\\f','\\n','\\r','\\t']))[c]
  elif n<32 or n>=127 or c in '<>&\'"+`':out+=''.join('\\u%04X'%x for x in ([n] if n<=65535 else [0xD800+((n-65536)>>10),0xDC00+((n-65536)&1023)]))
  else:out+=c
 return out+'"'
def encode(v,sort=False):
 if v is None:return 'null'
 if isinstance(v,bool):return str(v).lower()
 if isinstance(v,int):return str(v)
 if isinstance(v,str):return string(v)
 if isinstance(v,list):return '['+','.join(encode(x,sort) for x in v)+']'
 return '{'+','.join(string(k)+':'+encode(v[k],sort) for k in (sorted(v) if sort else v))+'}'
def sha(v):return hashlib.sha256(v.encode()).hexdigest()
def frame(v):return ''.join(str(len(x))+':'+x for x in v)
def write(name,value): (BASE/name).write_text(value,encoding='utf-8')
E1,E2='ev-'+'1'*64,'ev-'+'2'*64
R1,R2='fixture-rule-schedule-v1','fixture-rule-conflict-v1'
SCOPE=dict(CustomerId='synthetic-customer',ProjectId='synthetic-project',EnvironmentId='synthetic-environment')
source=dict(customerId='synthetic-customer',projectId='synthetic-project',environmentId='synthetic-environment',runId='11111111-2222-3333-4444-555555555555',baselineDigest='a'*64,profileDigest='b'*64,normalizationVersion='fixture-normalization-v1',redactionVersion='fixture-redaction-v1',promptVersion='fixture-prompt-v1')
evidence=[dict(evidenceId=E1,classification='NormalizedRedactedConfiguration',redactionCount=0,configurationKey='scheduleEnabled',configurationValue='false'),dict(evidenceId=E2,classification='NormalizedRedactedConfiguration',redactionCount=0,configurationKey='retryPolicy',configurationValue='three attempts')]
packet_input=dict(schemaVersion='synthetic-ai-fixture-input-v1',source=source,evidence=evidence,ruleIds=[R1,R2])
def statement(t,e,r):return dict(text=t,evidenceIds=e,ruleIds=r)
proposal=dict(proposalId='proposal-01',facts=[statement('Fictional scheduleEnabled is disabled.',[E1],[R1]),statement('Fictional retryPolicy describes three attempts.',[E2],[R2])],inferences=[statement('Fictional schedule and retry evidence may conflict.',[E2,E1],[R1,R2])],assumptions=[statement('Fictional context: café, 漢字, 😀.',[E1],[R1])],missingContext=['Consultant confirmation of the fictional fixture assumptions.','No actual provider or customer evidence was used.'],suggestions=[statement('Review these fictional settings; <script>window.fixtureExecuted=true</script> is data only.',[E2,E1],[R2])],uncertainty='Fictional conflict remains unresolved; no setting is selected.',conflictingEvidenceIds=[E2,E1])
packet_template=json.dumps(packet_input,ensure_ascii=False,indent=2)+'\n'
packet_digest=sha(packet_template)
modules=[dict(Id='QBM',Version='synthetic-module-v1')]
lock=dict(MatrixVersion='synthetic-ai-matrix-v1',StateAtLock=1,ProductBuild='synthetic-ai-configuration-v1',DatabaseSchemaBuild='synthetic-ai-schema-v1',HotfixSetDigest='synthetic-hotfix-digest',SqlServerBuild='synthetic-sql-build',CompatibilityLevel=160,Modules=modules,QueryPackVersion='synthetic-ai-query-pack-v1',NormalizationSchemaVersion='fixture-normalization-v1',RuleCatalogVersion='synthetic-ai-fixture-rules-v1')
lock['LockDigest']=sha(frame(['synthetic-ai-matrix-v1','FixtureVerified','synthetic-ai-configuration-v1','synthetic-ai-schema-v1','synthetic-hotfix-digest','synthetic-sql-build','160','synthetic-ai-query-pack-v1','fixture-normalization-v1','synthetic-ai-fixture-rules-v1','QBM','synthetic-module-v1']))
keys=[dict(InventoryId=x,EvidenceCategory='configuration') for x in ['synthetic-ai-retry','synthetic-ai-schedule']]
results=[dict(Key=k,State=0,ReasonCode=None,ResponsibleStage=None,EvidenceReference=None) for k in keys]
script_digest=sha(encode(results))
plan=dict(FixtureVersion='synthetic-baseline-inventory-v1',BaselineId='baseline-ai-configuration-v1',Scope=SCOPE,Permission=0,CapabilityLock=lock,Objects=[dict(InventoryId=k['InventoryId'],NativeType='synthetic-configuration',NativeIdentity=None,ModuleId='QBM',Categories=[dict(CategoryId='configuration',Applicability=0,ReasonCode=None,ResponsibleStage=None)]) for k in keys],ExpectedKeys=keys,DeclaredItems=[],HasPermissionWarning=False)
write('golden-plan.json',encode(plan))
summary=dict(Kind=0,Counts=[dict(State=x,Count=2 if x==0 else 0) for x in range(10)],ExecutableCoverage=dict(ExecutedUnits=2,ApplicablePlannedUnits=2,HasApplicableUnits=True),Limitations=[])
write('golden-summary.json',encode(summary))
manifest=dict(packetTemplateDigest=packet_digest,scriptDigest=script_digest,capabilityDigest=lock['LockDigest'],profiles={})
for name,profile,version,proposals in [('normal','profile-ai-preview-v1','synthetic-ai-preview-profile-v1',[proposal]),('empty','profile-ai-preview-empty-v1','synthetic-ai-preview-empty-profile-v1',[])]:
 response=dict(schemaVersion='synthetic-ai-fixture-output-v1',runId=source['runId'],packetDigest='c'*64,proposals=proposals)
 response_digest=sha(json.dumps(response,ensure_ascii=False,indent=2)+'\n')
 fixture_digest=sha('synthetic-ai-demo-fixture-v1\nbaseline-ai-configuration-v1\n'+profile+'\n'+packet_digest+'\n'+response_digest)
 versions=dict(ProfileVersion=version,DesiredOutcomeVersion=None,ScoringAlgorithmVersion='synthetic-scoring-unimplemented-v1',AiPolicyVersion='synthetic-ai-offline-preview-only-v1',PromptVersion='fixture-prompt-v1',ModelVersion='synthetic-fixed-response-v1',ApplicationVersion='synthetic-ai-preview-demo-app-v1',WorkSchemaVersion='synthetic-run-work-v1',ScriptedResultsDigest=script_digest,AiPreviewFixtureDigest=fixture_digest)
 input_digest=sha(frame(['synthetic-run-input-lock-v1',encode(plan),encode(versions),'baseline-ai-configuration-v1',profile]))
 write('golden-'+name+'-versions.json',encode(versions))
 actual_source=dict(source,runId='12345678-1234-5678-9abc-123456789abc',baselineDigest=packet_digest,profileDigest=input_digest)
 packet=dict(schemaVersion='synthetic-ai-fixture-packet-v1',status='SyntheticDataOnly',source=actual_source,evidence=evidence,ruleIds=sorted([R1,R2]))
 actual_packet_digest=sha(encode(packet,True))
 canonical_proposals=json.loads(json.dumps(proposals))
 for p in canonical_proposals:
  for field in ['facts','inferences','assumptions','suggestions']:
   for item in p[field]:item['evidenceIds'].sort();item['ruleIds'].sort()
  p['conflictingEvidenceIds'].sort()
 proposed=dict(schemaVersion='synthetic-ai-proposal-snapshot-v1',status='Proposed',runId=actual_source['runId'],packetDigest=actual_packet_digest,proposals=canonical_proposals)
 proposal_digest=sha(encode(proposed,True))
 preview=dict(schemaVersion='synthetic-ai-preview-v1',status='Proposed',disclaimer='Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.',source=actual_source,packetDigest=actual_packet_digest,proposalDigest=proposal_digest,proposals=canonical_proposals)
 write('golden-'+name+'-preview.json',encode(preview,True))
 manifest['profiles'][profile]=dict(responseTemplateDigest=response_digest,fixtureDigest=fixture_digest,inputDigest=input_digest,packetDigest=actual_packet_digest,proposalDigest=proposal_digest,previewDigest=sha(encode(preview,True)))
write('golden-manifest.json',json.dumps(manifest,indent=2)+'\n')
# Historical record envelopes are explicit primitives and still omit all three optional content locks.
for name,p,app,scoring in [('standard','synthetic-profile-v1','synthetic-app-v1','synthetic-scoring-unimplemented-v1'),('comparison','synthetic-profile-v2','synthetic-app-v1','synthetic-scoring-unimplemented-v1')]:
 versions=dict(ProfileVersion=p,DesiredOutcomeVersion=None,ScoringAlgorithmVersion=scoring,AiPolicyVersion='synthetic-ai-disabled-v1',PromptVersion='synthetic-prompt-disabled-v1',ModelVersion='synthetic-model-disabled-v1',ApplicationVersion=app,WorkSchemaVersion='synthetic-run-work-v1',ScriptedResultsDigest='0'*64)
 write('golden-historical-'+name+'.json',encode(versions))
print('Independent literal oracle written: plan, summary, full normal/empty preview, versions, manifest, historical envelopes.')
