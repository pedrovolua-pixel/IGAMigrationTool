#!/usr/bin/env python3
"""Independent composed fixture population/rank/manifest bytes, from approved contract only."""
import json,hashlib,struct
KINDS='SourceBuild EnvironmentEvidence Capability Baseline ReassessmentBaseline Collection Query Normalization Catalog Profile Scoring Maturity AiProvider AiModel AiPrompt AiSchema AiSettings Application Reviewer ReviewerEligibility Instruction Conflict ReviewEvents'.split()
def enc(v):return json.dumps(v,separators=(',',':')).replace('+',chr(92)+'u002B').encode()
def sha(v):return hashlib.sha256(v).hexdigest()
versions=dict(schema='synthetic-evaluation-sampling-versions-v1',bindings=[dict(kind=k,version='synthetic-'+k.lower()) for k in KINDS],correctionCutoffUtc='2026-10-03T12:00:00.0000000+00:00',evaluationDateUtc='2026-10-03T13:00:00.0000000+00:00',predecessorSampleDigest=None)
members=[dict(id=f'synthetic-item-{i:06d}',scopeId='synthetic-scope',environmentId='synthetic-env-a' if i<60 else 'synthetic-env-b',primaryModuleId='synthetic-module',categoryId='synthetic-category',ruleVersion='synthetic-rule',modelPromptVersion='synthetic-model',confidenceBandId='synthetic-confidence',severity='Critical' if i<20 else 'High' if i<40 else 'Medium',primarySettled=True,available=True,unavailableReason=None,secondaryModuleIds=[]) for i in range(120)]
pop=dict(schema='synthetic-evaluation-sampling-population-v1',populationId='synthetic-population',scopeId='synthetic-scope',versionManifestDigest=sha(enc(versions)),members=members);pd=sha(enc(pop));seed='11'*32
def rank(m):
 b=b'iga.synthetic-evaluation.sample-rank.v1\0'+bytes.fromhex(seed)
 for x in ['synthetic-scope',pd,m['environmentId'],m['primaryModuleId'],m['categoryId'],m['severity'],m['id']]:
  v=x.encode();b+=struct.pack('>I',len(v))+v
 return sha(b)
lower=members[40:];ranks={m['id']:rank(m) for m in lower};chosen=set(m['id'] for m in members[:40]);strata=[]
for env,n,allocation in [('synthetic-env-a',20,15),('synthetic-env-b',60,45)]:
 group=[m for m in lower if m['environmentId']==env]
 chosen.update(m['id'] for m in sorted(group,key=lambda m:(ranks[m['id']],m['id']))[:allocation])
 strata.append(dict(environmentId=env,primaryModuleId='synthetic-module',categoryId='synthetic-category',severity='Medium',populationCount=n,allocation=allocation))
sample=dict(schema='synthetic-evaluation-sampling-v1',sampleId='synthetic-sample',populationId='synthetic-population',scopeId='synthetic-scope',populationDigest=pd,versionManifestDigest=sha(enc(versions)),seed=seed,hardReviewBudget=None,strata=strata,ranks=[dict(memberId=m['id'],rankDigest=ranks[m['id']]) for m in lower],selected=[dict(memberId=m['id'],mandatory=i<40,rankDigest=None if i<40 else ranks[m['id']]) for i,m in enumerate(members) if m['id'] in chosen])
assert len(chosen)==100
result={'populationDigest':pd,'sampleDigest':sha(enc(sample)),'expectedSelectedIds':sorted(chosen),'lowerAllocations':[15,45]}
from pathlib import Path
p=Path(__file__).with_name('expected-v1.json')
if p.exists():assert json.loads(p.read_text())==result
else:p.write_text(json.dumps(result,indent=2)+'\n')
print('PASS independent composed population/rank/manifest fixture',result['sampleDigest'])
