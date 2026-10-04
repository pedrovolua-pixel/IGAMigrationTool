"""Independent V9 primitive/canonical oracle; never calls application code."""
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).parent
DIS = 'Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.'
RUN = '99999999-8888-4777-8666-555555555555'
A = 'ev-' + 'c' * 64
B = 'ev-' + 'd' * 64
R = 'fixture-rule-schedule-v1'
C = 'fixture-rule-conflict-v1'
def canon(v):
    return json.dumps(v, sort_keys=True, ensure_ascii=True, separators=(',', ':'))
def sha(s): return hashlib.sha256(s.encode()).hexdigest()
def statement(text, evidence=None, rules=None):
    return dict(text=text, evidenceIds=evidence or [A], ruleIds=rules or [R])
source = dict(customerId='synthetic-customer', projectId='synthetic-project', environmentId='synthetic-environment', runId=RUN, baselineDigest='a'*64, profileDigest='b'*64, normalizationVersion='fixture-normalization-v1', redactionVersion='fixture-redaction-v1', promptVersion='fixture-prompt-v1')
records = [dict(evidenceId=x, classification='NormalizedRedactedConfiguration', redactionCount=2, configurationKey=k, configurationValue=v) for x,k,v in [(B,'retryPolicy','Fictional alternate retry policy'),(A,'scheduleEnabled','Fictional disabled schedule')]]
input_ = dict(schemaVersion='synthetic-ai-fixture-input-v1', source=source, evidence=records, ruleIds=[R,C])
packet = dict(schemaVersion='synthetic-ai-fixture-packet-v1', status='SyntheticDataOnly', source=source, evidence=sorted(records,key=lambda r:r['evidenceId']), ruleIds=sorted([R,C]))
proposals = [dict(proposalId='proposal-09', facts=[statement('Fictional schedule observation')], inferences=[statement('Fictional timing inference',[B,A],[R,C])], assumptions=[statement('Fictional operating assumption',[B],[C])], suggestions=[statement('Fictional review suggestion',[A,B],[C,R])], missingContext=['Fictional context one','Fictional context two'], uncertainty='Fictional uncertainty remains', conflictingEvidenceIds=[B,A]), dict(proposalId='proposal-03',facts=[statement('Fictional alternative observation',[B],[C])],inferences=[],assumptions=[],suggestions=[],missingContext=[],uncertainty='',conflictingEvidenceIds=[])]
output = dict(schemaVersion='synthetic-ai-fixture-output-v1', runId=RUN, packetDigest=sha(canon(packet)), proposals=proposals)
ordered = json.loads(json.dumps(proposals))
for p in ordered:
    p['conflictingEvidenceIds'].sort()
    for field in ['facts','inferences','assumptions','suggestions']:
        for s in p[field]: s['evidenceIds'].sort(); s['ruleIds'].sort()
ordered.sort(key=lambda p:p['proposalId'])
proposal = dict(schemaVersion='synthetic-ai-proposal-snapshot-v1', status='Proposed', runId=RUN, packetDigest=output['packetDigest'], proposals=ordered)
preview = dict(schemaVersion='synthetic-ai-preview-v1',status='Proposed',source=source,packetDigest=output['packetDigest'],proposalDigest=sha(canon(proposal)),proposals=ordered,disclaimer=DIS)
values={'packet-input.json': input_,'provider-output.json':output,'packet-golden.json':packet,'proposal-golden.json':proposal,'preview-golden.json':preview}
for name,value in values.items(): (ROOT/name).write_text(canon(value),encoding='utf8')
(ROOT/'golden-provenance.json').write_text(json.dumps({'authority':'Independent Python standard-library primitive oracle; no application implementation invoked','digests':{name:sha(canon(value)) for name,value in values.items()}},indent=2)+'\n')
print('PASS V9 independent primitive/canonical oracle; ' + str(len(values)) + ' files')
