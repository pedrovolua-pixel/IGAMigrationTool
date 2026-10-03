"""Independent literal B14 identity/source/state/markup expectations. Never reads production outputs."""
import copy,hashlib,json,pathlib,sys
here=pathlib.Path(__file__).resolve().parent
PROFILE='synthetic-review-maturity-planning-tasks-equal-v1'
APP='synthetic-planning-tasks-app-v1'
CONTRACT='f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2'
REASON='  Plan \0\r\n<script>window.injected=1</script> https://invalid.example/ 😀 e\u0301  '
def sha(v):return hashlib.sha256(v.encode()).hexdigest()
def canonical(v):
    if isinstance(v,str):
        out='"'
        for c in v:
            n=ord(c);simple={'\\':'\\\\','\b':'\\b','\f':'\\f','\n':'\\n','\r':'\\r','\t':'\\t'}
            if c in simple:out+=simple[c]
            elif n<32 or n>=127 or c in '<>&\'"+`':out+=''.join('\\u%04X'%int.from_bytes(c.encode('utf-16-be')[i:i+2],'big') for i in range(0,len(c.encode('utf-16-be')),2))
            else:out+=c
        return out+'"'
    if v is None:return 'null'
    if isinstance(v,bool):return 'true' if v else 'false'
    if isinstance(v,int):return str(v)
    if isinstance(v,list):return '['+','.join(canonical(x) for x in v)+']'
    if isinstance(v,dict):return '{'+','.join(canonical(k)+':'+canonical(v[k]) for k in sorted(v))+'}'
    raise ValueError(v)
def taskid(x, finding,option):return sha(canonical(dict(schemaVersion='synthetic-planning-task-identity-v1',scope=x['analysis']['artifactReview']['source']['scope'],runId=x['run']['runId'],findingId=finding,scopedOptionId=option)))
def current_vector(x,ids):
    out=[]
    for id in ids:
        a=next(a for a in x['analysis']['artifactReview']['artifacts'] if a['artifactId']==id);e=a['history'][-1] if a['history'] else None
        out.append(dict(artifactId=id,revision=a['revision'],eventId=e['eventId'] if e else None,kind=e['kind'] if e else None,state=a['state'],sourceDigest=e['source']['sourceDigest'] if e else None))
    return out

def fixture(empty=False):
    x=json.loads((here/('original-empty-artifact-fixture.json' if empty else 'original-artifact-fixture.json')).read_text())
    a=x['analysis'];r=x['run'];p=a['fixPackages']['snapshot'];g=p['guidance'];s=g['source'];ar=a['artifactReview']
    r['selection']['profileId']=PROFILE;r['selection']['profileLabel']='Synthetic consultant planning tasks · equal weights';a['fixPackages']['profileId']=PROFILE;s['profileId']=PROFILE
    s['frozenVersions']['applicationVersion']=APP;s['frozenVersions']['planningTaskContractDigest']=CONTRACT
    for l in r['lockedInputs']:
        if l['name']=='Application':l.update(version=APP,sha256=sha(APP))
    r['lockedInputs'].append(dict(name='Planning task contract',version='synthetic-planning-task-contract-v1',sha256=CONTRACT))
    g['contentDigest']=sha(canonical({k:v for k,v in g.items() if k!='contentDigest'}));a['recommendationGuidance']['snapshot']=copy.deepcopy(g)
    p['canonicalJson']=canonical({k:v for k,v in p.items() if k not in ['canonicalJson','contentDigest']});p['contentDigest']=sha(p['canonicalJson'])
    ar['source'].update(profileId=PROFILE,applicationVersion=APP,guidanceDigest=g['contentDigest'],sourceDigest=p['contentDigest'])
    for i,e in enumerate(ar['artifacts']):
        h=dict(eventId=f'10000000-2222-4333-8444-{i+1:012d}',revision=1,kind='ReviewForPlanning',actorId=ar['actorId'],actorRoles=['Consultant'],recordedAtUtc='2026-10-03T01:02:03.1234567Z',reason='Independent fictional artifact review',source=copy.deepcopy(ar['source']),recordedState='ReviewedForPlanning')
        e.update(revision=1,state='ReviewedForPlanning',canReview=False,canWithdraw=True,history=[h])
    binding=dict(artifactSource=copy.deepcopy(ar['source']),planningTaskContractDigest=CONTRACT);options=[]
    for package in p['packages']:
        f=next(f for f in g['findings'] if f['findingId']==package['findingId'])
        for option in package['options']:
            ids=sorted(a['artifactId'] for a in option['artifacts'])
            id=dict(taskId=taskid(x,package['findingId'],option['scopedOptionId']),findingId=package['findingId'],categoryId=f['categoryId'],packageId=package['packageId'],scopedOptionId=option['scopedOptionId'],artifactIds=ids)
            options.append(dict(identity=id,findingState=f['currentState'],currentAttestations=current_vector(x,ids),canCreate=True))
    options.sort(key=lambda o:o['identity']['taskId']);entries=[]
    if options:
        o=options[0];o['canCreate']=False
        h=dict(eventId='20000000-2222-4333-8444-000000000001',revision=1,kind='Create',actorId=ar['actorId'],actorRoles=['Consultant'],recordedAtUtc='2026-10-03T01:02:04.1234567Z',reason=REASON,source=copy.deepcopy(binding),attestations=copy.deepcopy(o['currentAttestations']),recordedStatus='Planned',planningEventId='20000000-2222-4333-8444-000000000001')
        entries.append(dict(identity=copy.deepcopy(o['identity']),assigneeId=ar['actorId'],revision=1,status='Planned',freshness='CurrentPlan',creation=copy.deepcopy(h),plan=copy.deepcopy(h),history=[h],canReconfirm=False,canStart=True,canReturnToPlanned=False,canComplete=False,canCancel=True,canReopen=False,canComment=True))
    a['planningTasks']=dict(schemaVersion=1,demoOnly=True,status='Ready',reasonCode=None,source=binding,actorId=ar['actorId'],options=options,entries=entries,unavailableEntries=[])
    return x

def unavailable(x):
    d=x['analysis']['planningTasks'];entries=[]
    for e in d['entries']:
        history=[{k:v for k,v in h.items() if k not in ['reason','source','attestations']} for h in e['history']]
        entries.append(dict(identity=e['identity'],assigneeId=e['assigneeId'],revision=e['revision'],status=e['status'],freshness='SourceUnavailable',history=history))
    return dict(schemaVersion=1,demoOnly=True,status='SourceUnavailable',reasonCode='planning_task_source_unavailable',source=None,actorId=d['actorId'],options=[],entries=[],unavailableEntries=entries)

def esc(v):return str(v).replace('&','&amp;').replace('<','&lt;').replace('>','&gt;').replace('"','&quot;').replace("'",'&#x27;')
def rows(d):return ''.join('<div><dt>'+esc(k)+'</dt><dd>'+esc(v)+'</dd></div>' for k,v in d.items())
def binding(v):
    a=v['artifactSource'];out='<dl class="planning-task-fields">'+rows({k:x for k,x in a.items() if k not in ['scope','findingRevisions']})+rows(a['scope'])
    return out+'<div><dt>Planning task contract</dt><dd>'+v['planningTaskContractDigest']+'</dd></div><div><dt>Finding revisions</dt><dd><ul>'+''.join('<li>'+f['findingId']+': '+str(f['revision'])+'</li>' for f in a['findingRevisions'])+'</ul></dd></div></dl>'
def identity(v):return '<dl class="planning-task-fields">'+rows({{'taskId':'Task','findingId':'Finding','categoryId':'Category','packageId':'Package','scopedOptionId':'Recommendation option'}[k]:x for k,x in v.items() if k!='artifactIds'})+'<div><dt>Selected artifacts</dt><dd><ul>'+''.join('<li>'+a+'</li>' for a in v['artifactIds'])+'</ul></dd></div></dl>'
def vector(v):return '<ul>'+''.join('<li>'+a['artifactId']+' · Revision '+str(a['revision'])+' · '+a['state']+' · Event '+str(a['eventId'] or 'None')+' · '+str(a['kind'] or 'None')+' · Source '+str(a['sourceDigest'] or 'None')+'</li>' for a in v)+'</ul>'
def status(v):return {'InProgress':'In progress'}.get(v,v)
def event(h,full=True):
    out='<li><h4>Revision '+str(h['revision'])+' · '+{'Create':'Create planning task','ReconfirmPlan':'Reconfirm plan','StartProgress':'Start work','ReturnToPlanned':'Return to planned','Complete':'Complete','Cancel':'Cancel','Reopen':'Reopen','Comment':'Comment'}[h['kind']]+'</h4><p>Event: '+h['eventId']+'</p><p>Actor: '+esc(h['actorId'])+' · Roles: '+', '.join(h['actorRoles'])+'</p><p>Recorded: <time>'+h['recordedAtUtc']+'</time></p><p>Recorded status: '+status(h['recordedStatus'])+'</p><p>Planning event: '+h['planningEventId']+'</p>'
    if full:out+='<p class="planning-task-reason">Reason or comment: '+esc(h['reason'])+'</p><details><summary>Recorded source · server-verified historical reference</summary>'+binding(h['source'])+vector(h['attestations'])+'</details>'
    return out+'</li>'
PREAMBLE='<section class="planning-tasks" aria-labelledby="planning-tasks-heading"><h2 id="planning-tasks-heading" tabindex="-1">Consultant planning tasks</h2><p>Fictional planning work only. Completion does not confirm findings, validate remediation or change health, maturity or artifact review.</p>'
def markup(x):
    d=x['analysis']['planningTasks'];out=PREAMBLE+'<p>Current consultant: '+d['actorId']+'</p><details><summary>Current planning source</summary>'+binding(d['source'])+'</details>'
    if not d['options']:return out+'<p>No recommendation options are available. Empty results do not establish health or remediation.</p></section>'
    p=x['analysis']['fixPackages']['snapshot']
    for o in d['options']:
        id=o['identity']['taskId'];e=next((e for e in d['entries'] if e['identity']['taskId']==id),None);package=next(v for v in p['packages'] if v['packageId']==o['identity']['packageId']);option=next(v for v in package['options'] if v['scopedOptionId']==o['identity']['scopedOptionId']);f=next(v for v in p['guidance']['findings'] if v['findingId']==o['identity']['findingId']);original=next(v for v in f['options'] if v['scopedOptionId']==o['identity']['scopedOptionId'])
        out+='<article><h3 id="planning-task-'+id+'" tabindex="-1">'+('Planning task' if e else 'Recommendation option')+' · '+id+'</h3>'
        if e:out+='<p role="status">Status: Planned · Current plan</p><p>Assignee: '+e['assigneeId']+' · Revision 1</p>'
        else:out+='<p role="status">No task created. Explicit conversion requires all three current artifacts reviewed for planning.</p>'
        out+=identity(o['identity'])+'<p>Current finding: '+esc(f['presentationTitle'])+' · '+o['findingState']+'</p><details><summary>Current original recommendation and fictional artifacts</summary><dl class="planning-task-fields">'
        out+=rows({'Original finding title':f['originalTitle'],'Rule':f['ruleId']+' · '+f['ruleVersion'],'Recommendation':original['text'],'Prerequisites':original['prerequisites'],'Risk':original['risk'],'Recovery guidance':original['recoveryGuidance']})+'</dl><p>Validation guidance</p><ul>'+''.join('<li>'+esc(v)+'</li>' for v in f['validationGuidance'])+'</ul>'
        for a in option['artifacts']:out+='<div><h4>'+a['kind']+' · '+a['artifactId']+'</h4><p>Original generated status: '+a['status']+' · Template '+a['templateId']+'</p><pre><code>'+esc(a['text'])+'</code></pre></div>'
        out+='</details><details><summary>Current selected planning attestations</summary>'+vector(o['currentAttestations'])+'</details>'
        if e:
            out+='<p>Creation and latest plan are immutable historical metadata references verified by the server. Current recommendation text may differ from those earlier versions.</p><details><summary>Creation binding · Event '+e['creation']['eventId']+'</summary>'+binding(e['creation']['source'])+vector(e['creation']['attestations'])+'</details><details><summary>Latest plan binding · Event '+e['plan']['eventId']+'</summary>'+binding(e['plan']['source'])+vector(e['plan']['attestations'])+'</details><details><summary>Attributed task history (1)</summary><ol>'+event(e['history'][0])+'</ol></details>'
        out+='<label for="planning-task-reason-'+id+'">Reason or comment for '+id+' (required, up to 2000 characters)</label><textarea id="planning-task-reason-'+id+'" maxLength="2000"></textarea>'
        for label in (['Start work','Cancel task','Add comment'] if e else ['Create planning task']):out+='<button type="button" disabled="">'+label+'</button>'
        out+='</article>'
    return out+'</section>'
def unavailable_markup(x,d):
    out=PREAMBLE+'<p role="status">Current source unavailable. Historical task metadata is shown without content or actions.</p>'
    for e in d['unavailableEntries']:
        out+='<article><h3 id="planning-task-'+e['identity']['taskId']+'" tabindex="-1">Planning task · '+e['identity']['taskId']+'</h3><p>Status: '+status(e['status'])+' · Source unavailable</p><p>Assignee: '+e['assigneeId']+' · Revision '+str(e['revision'])+'</p>'+identity(e['identity'])+'<details><summary>Verified task metadata history ('+str(len(e['history']))+')</summary><ol>'+''.join(event(h,False) for h in e['history'])+'</ol></details></article>'
    return out+'</section>'
outputs={}
for name,empty in [('complete',False),('empty',True)]:
    x=fixture(empty);outputs[name+'-fixture.json']=json.dumps(x,ensure_ascii=False,indent=2)+'\n';outputs[name+'-expected.html']=markup(x)
x=fixture();d=unavailable(x);outputs['unavailable-fixture.json']=json.dumps(dict(run=x['run'],detail=d),ensure_ascii=False,indent=2)+'\n';outputs['unavailable-expected.html']=unavailable_markup(x,d)
outputs['oracle-hashes.json']=json.dumps({k:sha(v) for k,v in outputs.items()},indent=2)+'\n'
if '--verify' in sys.argv:
    for name,v in outputs.items():assert (here/name).read_bytes()==v.encode(),name
    print('PASS independent B14 oracle: '+str(len(outputs))+' files')
else:
    for name,v in outputs.items():(here/name).write_text(v)
