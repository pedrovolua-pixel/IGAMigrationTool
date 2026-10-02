"""Independently authored UI primitives, default JSON bytes and complete React text markup oracle."""
from pathlib import Path
import hashlib,json,sys
base=Path(__file__).parent
disclaimer='Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.'
hashof=lambda s:hashlib.sha256(s.encode()).hexdigest()
def escaped(value):
 result='"'
 for ch in value:
  code=ord(ch)
  if ch=='\\':result+='\\\\'
  elif ch in '\b\f\n\r\t':result+={'\b':'\\b','\f':'\\f','\n':'\\n','\r':'\\r','\t':'\\t'}[ch]
  elif code<32 or code>=127 or ch in '<>&\'"+`':
   units=[code] if code<=65535 else [0xd800+((code-65536)>>10),0xdc00+((code-65536)&1023)]
   result+=''.join('\\u%04X'%u for u in units)
  else:result+=ch
 return result+'"'
def canonical(v):
 if isinstance(v,str):return escaped(v)
 if isinstance(v,list):return '['+','.join(canonical(a) for a in v)+']'
 if isinstance(v,dict):return '{'+','.join(escaped(k)+':'+canonical(v[k]) for k in sorted(v))+'}'
 raise ValueError('Unsupported primitive')
def html(v):
 return str(v).replace('&','&amp;').replace('<','&lt;').replace('>','&gt;').replace('"','&quot;').replace("'",'&#x27;')
E1,E2='ev-'+'1'*64,'ev-'+'2'*64
R1,R2='fixture-rule-conflict-v1','fixture-rule-schedule-v1'
def statement(text):return {'text':text,'evidenceIds':[E1,E2],'ruleIds':[R1,R2]}
source={'customerId':'synthetic-customer','projectId':'synthetic-project','environmentId':'synthetic-environment','runId':'12345678-1234-5678-9abc-123456789abc','baselineDigest':'b'*64,'profileDigest':'a'*64,'normalizationVersion':'fixture-normalization-v1','redactionVersion':'fixture-redaction-v1','promptVersion':'fixture-prompt-v1'}
proposal={'proposalId':'proposal-01','facts':[statement('<script>alert(\'x\' & "y")</script>'),statement('Unicode café 中文 😀')],'inferences':[statement('<img src=x onerror=alert(1)>')],'assumptions':[statement('A < B & B > C')],'suggestions':[statement('javascript:alert(1) remains text')],'missingContext':['First missing context <iframe>','Second missing context & detail'],'uncertainty':'Conflicting configuration <style>body{display:none}</style>','conflictingEvidenceIds':[E1,E2]}
payload={'schemaVersion':'synthetic-ai-preview-v1','status':'Proposed','disclaimer':disclaimer,'source':source,'packetDigest':'e'*64,'proposalDigest':'f'*64,'proposals':[proposal]}
snapshot={**payload,'canonicalJson':canonical(payload),'contentDigest':hashof(canonical(payload))}
versions=[('Baseline','baseline-ai-configuration-v1'),('Profile','synthetic-ai-preview-profile-v1'),('Rule catalog','synthetic-ai-fixture-rules-v1'),('Capability','synthetic-ai-matrix-v1'),('Desired outcomes','disabled'),('Scoring algorithm','synthetic-scoring-unimplemented-v1'),('AI policy','synthetic-ai-offline-preview-only-v1'),('Prompt','fixture-prompt-v1'),('Model','synthetic-fixed-response-v1'),('Application','synthetic-ai-preview-demo-app-v1'),('Work schema','synthetic-run-work-v1'),('Complete frozen input','synthetic-input-lock-v1'),('Exact capability tuple','synthetic-ai-matrix-v1'),('Scripted result fixture','synthetic-outcomes-v1'),('Frozen offline AI contents','synthetic-ai-demo-fixture-v1'),('Offline AI configuration template','synthetic-ai-configuration-v1')]
locks=[{'name':n,'version':v,'sha256':hashof(v)} for n,v in versions]
for l in locks:
 if l['name']=='Complete frozen input':l['sha256']='a'*64
 if l['name']=='Frozen offline AI contents':l['sha256']='d'*64
 if l['name']=='Offline AI configuration template':l['sha256']='b'*64
run={'schemaVersion':1,'demoOnly':True,'runId':source['runId'],'revision':5,'state':'Scoring','createdAtUtc':'2026-10-02T00:00:00Z','updatedAtUtc':'2026-10-02T00:00:00Z','selection':{'scopeId':'scope-synthetic','scopeLabel':'Synthetic fixture scope','baselineId':'baseline-ai-configuration-v1','baselineLabel':'Fictional offline configuration','profileId':'profile-ai-preview-v1','profileLabel':'Fixed offline preview'},'progress':{'plannedUnits':2,'terminalUnits':2,'remainingUnits':0,'allTerminal':True},'coverageCompletionKind':'Complete','lockedInputs':locks,'warnings':[],'stateCounts':[{'state':'Pass','count':2}],'executableCoverage':{'numerator':2,'denominator':2,'hasApplicableUnits':True},'limitations':[],'actions':{'canCancel':False,'canResume':False,'resumeAvailableAtUtc':None,'reasonCode':'scoring_paused'},'cancelRequested':False}
detail={'schemaVersion':'synthetic-ai-demo-preview-v1','runId':run['runId'],'runRevision':5,'runInputDigest':'a'*64,'baselineId':'baseline-ai-configuration-v1','profileId':'profile-ai-preview-v1','fixtureDigest':'d'*64,'status':'Ready','reasonCode':None,'snapshot':snapshot}
analysis={'schemaVersion':1,'demoOnly':True,'runId':run['runId'],'runRevision':5,'status':'Unavailable','reasonCode':'coverage_only_fixture','algorithmVersion':None,'fixtureDigest':None,'contentDigest':None,'provisional':None,'publishableCurrent':None,'categories':[],'objectTypes':[],'modules':[],'outcomes':[],'quality':None,'findings':[],'warnings':[],'review':None,'maturity':None,'reviewSnapshotDigest':None,'reportDraft':None,'recommendationGuidance':None,'aiPreview':detail}
def listing(values,empty):return '<ul>'+''.join('<li>'+html(s)+'</li>' for s in values)+'</ul>' if values else '<p>'+empty+'</p>'
def render():
 result='<section class="ai-proposal-preview" aria-label="Offline simulated configuration response"><p class="eyebrow">Fictional configuration · fixed offline response</p><h3 id="ai-proposal-preview-heading" tabindex="-1">Offline simulated configuration response</h3><div class="ai-preview-warning" role="note"><strong>Proposed · Untrusted</strong><p>'+html(disclaimer)+'</p><p>This response is separate from deterministic findings and health calculations. Its citations are identifiers only; no evidence or action is opened.</p></div>'
 for pr in payload['proposals']:
  result+='<article class="ai-preview-proposal"><h4>Proposal '+html(pr['proposalId'])+'</h4>'
  for field,label in [('facts','Facts'),('inferences','Inferences'),('assumptions','Assumptions'),('suggestions','Suggestions')]:
   result+='<section><h5>'+label+'</h5><ol>'
   for st in pr[field]:result+='<li><p>'+html(st['text'])+'</p><h6>Evidence IDs</h6>'+listing(st['evidenceIds'],'No evidence IDs were supplied.')+'<h6>Rule IDs</h6>'+listing(st['ruleIds'],'No rule IDs were supplied.')+'</li>'
   result+='</ol></section>'
  result+='<section><h5>Missing context</h5>'+listing(pr['missingContext'],'No missing context was declared.')+'</section><section><h5>Uncertainty</h5><p>'+html(pr['uncertainty'])+'</p></section><section><h5>Conflicting evidence IDs</h5>'+listing(pr['conflictingEvidenceIds'],'No conflicting evidence was declared.')+'</section></article>'
 result+='<details class="ai-preview-source"><summary>Captured preview source and digests</summary><p>Digests identify this fictional frozen value and grant no access.</p><dl>'
 rows=[('Run ID',source['runId']),('Run revision','5'),('Baseline',detail['baselineId']),('Profile',detail['profileId']),('Complete frozen input digest',detail['runInputDigest']),('Complete offline fixture digest',detail['fixtureDigest']),('Customer',source['customerId']),('Project',source['projectId']),('Environment',source['environmentId']),('Configuration template digest',source['baselineDigest']),('Source profile digest',source['profileDigest']),('Normalization version',source['normalizationVersion']),('Redaction version',source['redactionVersion']),('Prompt version',source['promptVersion']),('Packet digest',payload['packetDigest']),('Proposal digest',payload['proposalDigest']),('Canonical preview content digest',snapshot['contentDigest']),('Preview schema',snapshot['schemaVersion']),('Preview status',snapshot['status'])]
 for label,value in rows:result+='<div><dt>'+label+'</dt><dd>'+html(value)+'</dd></div>'
 return result+'</dl></details></section>'
if __name__=='__main__':
 expected={'fixture.json':json.dumps({'run':run,'analysis':analysis},indent=2,ensure_ascii=False)+'\n','golden-component.html':render(),'golden-component.sha256':hashof(render())}
 for name,value in expected.items():
  if '--verify' in sys.argv:assert (base/name).read_bytes()==value.encode(),name
  else:(base/name).write_bytes(value.encode())
 print('B10 independent primitive/component oracle: three exact artifacts verified' if '--verify' in sys.argv else 'B10 independent primitive/component oracle: three artifacts authored')
