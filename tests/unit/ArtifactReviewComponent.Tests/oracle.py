"""Independent B13 primitive/state/markup oracle; never reads renderer output."""
import copy, hashlib, html, json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[3]
directory=pathlib.Path(__file__).resolve().parent
contract='a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f'
def sha(v):return hashlib.sha256(v.encode()).hexdigest()
def canonical(v):
    if isinstance(v,str):
        out='"'
        for c in v:
            n=ord(c)
            simple={'\\':'\\\\','\b':'\\b','\f':'\\f','\n':'\\n','\r':'\\r','\t':'\\t'}
            if c in simple:out+=simple[c]
            elif n<32 or n>=127 or c in '<>&\'"+`':
                out+=''.join('\\u%04X'%int.from_bytes(v,'big') for v in [c.encode('utf-16-be')[i:i+2] for i in range(0,len(c.encode('utf-16-be')),2)])
            else:out+=c
        return out+'"'
    if v is None:return 'null'
    if isinstance(v,bool):return 'true' if v else 'false'
    if isinstance(v,int):return str(v)
    if isinstance(v,list):return '['+','.join(canonical(x) for x in v)+']'
    if isinstance(v,dict):return '{'+','.join(canonical(k)+':'+canonical(v[k]) for k in sorted(v))+'}'
    raise ValueError(v)
def fixture(empty=False):
    # Existing independently authored B12 primitives are input only, not runtime captures.
    x=json.loads((root/'tests/unit/FixPackagePreview.Tests'/('empty-fixture.json' if empty else 'complete-fixture.json')).read_text())
    a,r=x['analysis'],x['run'];s=a['fixPackages']['snapshot'];g=s['guidance'];src=g['source']
    profile='synthetic-review-maturity-fix-review-equal-v1'
    r['selection']['profileId']=profile; a['fixPackages']['profileId']=profile;src['profileId']=profile
    src['frozenVersions']['applicationVersion']='synthetic-fix-review-app-v1';src['frozenVersions']['fixReviewContractDigest']=contract
    for lock in r['lockedInputs']:
        if lock['name']=='Application':lock.update(version='synthetic-fix-review-app-v1',sha256=sha('synthetic-fix-review-app-v1'))
    r['lockedInputs'].append(dict(name='Artifact review contract',version='synthetic-fix-review-contract-v1',sha256=contract))
    g['contentDigest']=sha(canonical({k:v for k,v in g.items() if k!='contentDigest'}))
    a['recommendationGuidance']['snapshot']=copy.deepcopy(g)
    s['canonicalJson']=canonical({k:v for k,v in s.items() if k not in ['canonicalJson','contentDigest']});s['contentDigest']=sha(s['canonicalJson'])
    binding=dict(scope=src['scope'],runId=r['runId'],runRevision=r['revision'],runInputDigest=src['runInputDigest'],baselineId=r['selection']['baselineId'],profileId=profile,applicationVersion='synthetic-fix-review-app-v1',contractDigest=contract,sourceDigest=s['contentDigest'],guidanceDigest=g['contentDigest'],findingReviewDigest=a['reviewSnapshotDigest'],templateVersion=s['templateVersion'],templateDigest=s['templateDigest'],findingRevisions=[dict(findingId=f['findingId'],revision=f['findingRevision']) for f in sorted(g['findings'],key=lambda f:f['findingId'])])
    entries=[]
    for p in s['packages']:
        f=next(f for f in g['findings'] if f['findingId']==p['findingId'])
        for o in p['options']:
            for artifact in o['artifacts']:
                entries.append(dict(findingId=p['findingId'],packageId=p['packageId'],scopedOptionId=o['scopedOptionId'],artifactId=artifact['artifactId'],categoryId=f['categoryId'],templateId=artifact['templateId'],kind=artifact['kind'],artifactTextDigest=sha(artifact['text']),revision=0,state='Unverified',canReview=True,canWithdraw=False,history=[]))
    a['artifactReview']=dict(schemaVersion=1,demoOnly=True,status='Ready',reasonCode=None,source=binding,actorId=a['review']['actor'],artifacts=sorted(entries,key=lambda e:e['artifactId']))
    return x
def esc(s):return str(s).replace('&','&amp;').replace('<','&lt;').replace('>','&gt;').replace('"','&quot;').replace("'",'&#x27;')
def rows(v):return ''.join('<div><dt>'+esc(k)+'</dt><dd>'+esc(str(x).lower() if isinstance(x,bool) else x)+'</dd></div>' for k,x in v.items())
def source(v):
    fields={k:x for k,x in v.items() if k not in ['scope','findingRevisions']}
    return '<dl class="artifact-review-fields">'+rows(fields)+rows(v['scope'])+'<div><dt>Finding revisions</dt><dd><ul>'+''.join('<li>'+f['findingId']+': '+str(f['revision'])+'</li>' for f in v['findingRevisions'])+'</ul></dd></div></dl>'
def markup(x):
    d=x['analysis']['artifactReview']
    out='<section aria-labelledby="artifact-review-heading" class="artifact-review"><h2 id="artifact-review-heading" tabindex="-1">Consultant artifact review</h2><p>Local fictional planning attestation only. Correctness, supported remediation and execution safety remain unverified.</p><p>Generated originals stay Unverified. Their historical unavailable sections describe the original generation layer; this separate overlay records current review history.</p>'
    out+='<p>Current reviewer: '+esc(d['actorId'])+'</p><details><summary>Current review source</summary>'+source(d['source'])+'</details>'
    if not d['artifacts']:return out+'<p>No artifacts are available to review. Empty results do not establish health or remediation.</p></section>'
    members={a['artifactId']:a for p in x['analysis']['fixPackages']['snapshot']['packages'] for o in p['options'] for a in o['artifacts']}
    for e in d['artifacts']:
        id=e['artifactId'];kind=e['kind']
        out+='<article><h3 id="artifact-review-'+id+'" tabindex="-1">'+kind+' artifact · '+id+'</h3><p role="status">Unverified — no current planning attestation</p><p>Original generated status: Unverified</p><dl class="artifact-review-fields">'+rows({k:v for k,v in e.items() if k not in ['history','state']})+'</dl><details><summary>Original fictional artifact text</summary><pre><code>'+esc(members[id]['text'])+'</code></pre></details><details><summary>Attributed review history (0)</summary><p>No review events recorded.</p></details><label for="artifact-review-reason-'+id+'">Reason for '+kind+' artifact '+id+' (required, up to 2000 characters)</label><textarea id="artifact-review-reason-'+id+'" maxLength="2000"></textarea><button type="button" disabled="">Review for planning</button></article>'
    return out+'</section>'
outputs={}
for name,empty in [('complete',False),('empty',True)]:
    x=fixture(empty);outputs[name+'-fixture.json']=json.dumps(x,ensure_ascii=False,indent=2)+'\n';outputs[name+'-expected.html']=markup(x)
outputs['oracle-hashes.json']=json.dumps({k:sha(v) for k,v in outputs.items()},indent=2)+'\n'
if '--verify' in sys.argv:
    for name,v in outputs.items():
        assert (directory/name).read_bytes()==v.encode(),name
    print('PASS independent B13 oracle: '+str(len(outputs))+' files')
else:
    for name,v in outputs.items():(directory/name).write_text(v)
