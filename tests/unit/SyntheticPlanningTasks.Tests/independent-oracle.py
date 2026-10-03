# A14: attributed independent A13/Cycle11 primitive oracle, extended with settled literal TC14 identity/vector/command recipes before production output is read.
import json,hashlib,pathlib,sys,copy
p=pathlib.Path(__file__).resolve().parent
a='a'*64;b='b'*64;c='c'*64
scope=dict(customerId='synthetic-customer',projectId='synthetic-project',environmentId='synthetic-environment')
run='7eedaf91-6e83-4e82-8b2b-deefc910b27b'
contractDigest='a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f'
versions=dict(fixReviewContractDigest=contractDigest,profileVersion='synthetic-profile-v1',desiredOutcomeVersion=None,scoringAlgorithmVersion='pilot-health-v1',aiPolicyVersion='synthetic-ai-disabled-v1',promptVersion='synthetic-prompt-disabled-v1',modelVersion='synthetic-model-disabled-v1',applicationVersion='synthetic-planning-tasks-app-v1',planningTaskContractDigest='f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2',fixPackageTemplateDigest='a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669',workSchemaVersion='synthetic-run-work-v1',scriptedResultsDigest=a,analysisFixtureDigest=a,maturityFixtureDigest=a)
cap=dict(matrixVersion='synthetic-analysis-matrix-v1',stateAtLock='FixtureVerified',productBuild='fixture-product-v1',databaseSchemaBuild='fixture-facts-v1',hotfixSetDigest='synthetic-hotfix-digest',sqlServerBuild='synthetic-sql-build',compatibilityLevel=160,modules=[dict(id='SyntheticOperations',version='synthetic-module-v1'),dict(id='SyntheticSecurity',version='synthetic-module-v1')],queryPackVersion='synthetic-query-pack-v1',normalizationSchemaVersion='synthetic-normalization-v1',ruleCatalogVersion='synthetic-analysis-catalog-v1',lockDigest=a)
analysis=dict(scope=scope,packVersion='synthetic-analysis-pack-v1',packDigest=a,presetId='synthetic-analysis-findings-v1',presetVersion='synthetic-evidence-v1',evidenceDigest=a,catalogVersion='synthetic-analysis-catalog-v1',catalogDigest=a,profileId='synthetic-analysis-equal-v1',profileVersion='synthetic-profile-v1',profileDigest=a,compatibility=dict(sourceProduct='SYNTHETIC-ONLY',productVersion='fixture-product-v1',evidenceSchemaVersion='fixture-facts-v1',ruleLanguageVersion='count-predicate-v1'))
source=dict(scope=scope,runId=run,runRevision=13,runState='Scoring',runInputDigest=a,baselineId='synthetic-analysis-findings-v1',profileId='synthetic-review-maturity-planning-tasks-equal-v1',frozenVersions=versions,capabilityLock=cap,analysisLock=analysis,analysisFixtureDigest=a,analysisContentDigest=a,savedCoverageDigest=a,reviewRunId=run,reviewRunRevision=13,reviewSnapshotDigest=a)
occ=dict(occurrenceId=c,objectId='OBJECT-1',objectType='SyntheticControl',moduleId='SyntheticSecurity',originalDigest=a,evidenceReference='fixture-evidence:OBJECT-1')
opt=dict(optionId='inspect-fixture',text='Inspect the original fixture.',prerequisites='Consultant review required.',risk='Unverified; do not execute.',recoveryGuidance='Retain the prior baseline.')
finding=dict(findingId=b,ruleId='SYN-GUARD',ruleVersion='synthetic-rule-v1',categoryId='SECURITY',severity='Critical',originalTitle='Original fictional title',presentationTitle='Current fictional title',businessContext='Synthetic business context',initialState='Proposed',currentState='Confirmed',findingRevision=1,rootCause='Fictional root cause',occurrences=[occ],options=[opt],validationGuidance=['Verify exact rule and baseline.','New evidence is required.'],guidanceReferences=['fixture-guidance:SYN-GUARD:v1'],assumptions=['Fictional fixture premise.'],limitations=['No customer validation.'])
payload=dict(schemaVersion='synthetic-recommendation-guidance-v1',status='SyntheticUnverified',source=source,findings=[finding],warnings=['Synthetic fixture guidance only; no actual One Identity defect, supported remediation or customer approval is established.','Every option is review-only and unverified. Finding confirmation, rejection or deferral does not review guidance or validate remediation.','Stable identity ordering is not a priority calculation. Recovery guidance does not establish executed or verified restoration.','This is a current detached value, not durable recommendation history. The run remains Scoring and unpublished.'],unavailableSections=['Priority calculation, effort estimates and consultant overrides are unavailable.','Customer-approved objectives, roles and approvals are unavailable.','Fix artifacts, recommendation review, task conversion/workflows and CSV export are unavailable.','Customer risk acceptance, validated remediation, reassessment and actual report publication/sharing are unavailable.','No SQL, script, configuration artifact, customer-system execution, external task connector or ROI calculation exists.'])
# Independently authored literal data and specified canonical recipes only.
# No production code, canonical helper, supplied snapshot, binary or execution output is read.
def string(value):
    out='"'
    short={'\b':'\\b','\t':'\\t','\n':'\\n','\f':'\\f','\r':'\\r','\\':'\\\\'}
    for char in value:
        code=ord(char)
        if char in short: out+=short[char]
        elif 0x20<=code<0x7f and char not in '\"\'&<>+`': out+=char
        elif code<=0xffff: out+='\\u%04X'%code
        else:
            code-=0x10000;out+='\\u%04X\\u%04X'%(0xd800+(code>>10),0xdc00+(code&1023))
    return out+'"'
def canonical(value):
    if isinstance(value,dict): return '{'+','.join(string(k)+':'+canonical(value[k]) for k in sorted(value))+'}'
    if isinstance(value,list): return '['+','.join(canonical(x) for x in value)+']'
    if isinstance(value,str): return string(value)
    if value is None:return 'null'
    if value is True:return 'true'
    if value is False:return 'false'
    return str(value)
def digest(value):return hashlib.sha256(canonical(value).encode()).hexdigest()
templates=[dict(templateId='fictional-config-v1',kind='Configuration',text='{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}'),dict(templateId='fictional-script-v1',kind='Script',text="# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),dict(templateId='fictional-sql-v1',kind='Sql',text="-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")]
templateVersion='fictional-fix-templates-v1'
templateDigest=digest(dict(templateVersion=templateVersion,templates=templates))
disclaimer='Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.'
warnings=['Finding confirmation, rejection or deferral does not review artifacts or validate remediation.','Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.','Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.']
unavailable=['Consultant artifact review, approval history and content invalidation are unavailable.','Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.','Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.']
hostile="<script>alert('x')</script> \0\r\né e\u0301 😀 \" + & ` =SUM(A1)\nhttps://invalid.example/evil"
scenarios={}
for name in ['normal','empty','multi','hostile']:
    g=copy.deepcopy(payload)
    if name=='empty':g['findings']=[]
    if name=='multi':
        g['findings'][0]['options'].append(dict(optionId='compare-fixture',text='Compare fictional evidence.',prerequisites='A separate fixture run.',risk='No closure claim.',recoveryGuidance='Retain originals.'))
        g['findings'][0]['occurrences'].append(dict(occurrenceId='f'*64,objectId='OBJECT-2',objectType='SyntheticControl',moduleId='SyntheticOperations',originalDigest=a,evidenceReference='fixture-evidence:OBJECT-2'))
        other=copy.deepcopy(finding);other['findingId']='d'*64;other['occurrences']=[dict(occurrenceId='e'*64,objectId='OBJECT-3',objectType='SyntheticControl',moduleId='SyntheticSecurity',originalDigest=a,evidenceReference='fixture-evidence:OBJECT-3')];g['findings'].append(other)
    if name=='hostile':
        g['findings'][0]['originalTitle']=hostile;g['findings'][0]['presentationTitle']=hostile;g['findings'][0]['businessContext']=hostile;g['findings'][0]['options'][0]['text']=hostile
    for f in g['findings']:
        f['occurrences'].sort(key=lambda x:x['occurrenceId']);f['options'].sort(key=lambda x:x['optionId'])
        for o in f['options']:
            o['status']='Unverified';o['scopedOptionId']=digest(dict(findingId=f['findingId'],optionId=o['optionId'],runId=run,scope=scope))
    g['findings'].sort(key=lambda x:x['findingId'])
    guidanceDigest=digest(g);g['contentDigest']=guidanceDigest
    packages=[]
    for f in g['findings']:
        packageId=digest(dict(findingId=f['findingId'],runId=run,scope=scope));opts=[]
        for o in sorted(f['options'],key=lambda x:x['scopedOptionId']):
            artifacts=[]
            for t in templates:
                artifactId=digest(dict(packageId=packageId,scopedOptionId=o['scopedOptionId'],templateId=t['templateId'],templateVersion=templateVersion))
                artifacts.append(dict(artifactId=artifactId,templateId=t['templateId'],kind=t['kind'],status='Unverified',text=t['text']))
            opts.append(dict(scopedOptionId=o['scopedOptionId'],artifacts=artifacts))
        packages.append(dict(packageId=packageId,findingId=f['findingId'],options=opts))
    w=warnings+(['No findings were supplied; no fix packages or actions are available.'] if name=='empty' else [])
    value=dict(schemaVersion='synthetic-fix-package-preview-v1',status='Unverified',disclaimer=disclaimer,guidance=g,templateVersion=templateVersion,templateDigest=templateDigest,templates=templates,packages=packages,warnings=w,unavailableSections=unavailable)
    encoded=canonical(value).encode();summary=dict(contentDigest=hashlib.sha256(encoded).hexdigest(),guidanceDigest=guidanceDigest,templateDigest=templateDigest,canonicalBytes=len(encoded))
    scenarios[name]=summary
    binding=dict(scope=scope,runId=run,runRevision=13,runInputDigest=a,baselineId=source['baselineId'],profileId=source['profileId'],applicationVersion=versions['applicationVersion'],contractDigest=contractDigest,sourceDigest=summary['contentDigest'],guidanceDigest=guidanceDigest,findingReviewDigest=a,templateVersion=templateVersion,templateDigest=templateDigest,findingRevisions=[dict(findingId=f['findingId'],revision=f['findingRevision']) for f in g['findings']])
    identities=[]
    for package in packages:
        category=next(f['categoryId'] for f in g['findings'] if f['findingId']==package['findingId'])
        for option in package['options']:
            for artifact in option['artifacts']:
                identities.append(dict(findingId=package['findingId'],categoryId=category,packageId=package['packageId'],scopedOptionId=option['scopedOptionId'],artifactId=artifact['artifactId'],templateId=artifact['templateId'],kind=artifact['kind'],artifactTextDigest=hashlib.sha256(artifact['text'].encode()).hexdigest()))
    identities.sort(key=lambda x:(x['findingId'],x['artifactId']))
    taskbinding=dict(artifactSource=binding,planningTaskContractDigest=versions['planningTaskContractDigest'])
    taskoptions=[]
    for package in packages:
        f=next(f for f in g['findings'] if f['findingId']==package['findingId'])
        for option in package['options']:
            artifactids=sorted(a['artifactId'] for a in option['artifacts'])
            taskid=digest(dict(schemaVersion='synthetic-planning-task-identity-v1',scope=scope,runId=run,findingId=f['findingId'],scopedOptionId=option['scopedOptionId']))
            identity=dict(taskId=taskid,findingId=f['findingId'],categoryId=f['categoryId'],packageId=package['packageId'],scopedOptionId=option['scopedOptionId'],artifactIds=artifactids)
            vector=[dict(artifactId=a,revision=0,eventId=None,kind=None,state='Unverified',sourceDigest=None) for a in artifactids]
            taskoptions.append(dict(identity=identity,findingState=f['currentState'],currentAttestations=vector,canCreate=False))
    taskoptions.sort(key=lambda o:o['identity']['taskId'])
    extra=[('task-binding',taskbinding),('task-options',taskoptions)]
    if name=='normal':
        vector=[dict(a,revision=1,eventId='11111111-1111-4111-8111-%012d'%(i+1),kind='ReviewForPlanning',state='ReviewedForPlanning',sourceDigest=summary['contentDigest']) for i,a in enumerate(taskoptions[0]['currentAttestations'])]
        command=dict(eventId='42daef99-443e-4bf3-ace2-b44e6e088207',kind='Create',expectedRevision=0,expectedSourceDigest=summary['contentDigest'],expectedAttestations=vector,reason='  fictional plan \0\r\n é e\u0301 😀  ')
        semantic=dict(schemaVersion='synthetic-planning-task-command-v1',scope=scope,runId=run,taskId=taskoptions[0]['identity']['taskId'],actorId='synthetic-consultant',command=command)
        extra.extend([('reviewed-vector',vector),('command',command),('semantic-command',semantic)])
    for suffix,data in [('binding',binding),('artifacts',identities)]+extra:
        expected=p/(name+'-'+suffix+'.json')
        if '--write-new' in sys.argv:
            assert not expected.exists(),'Never replace an existing oracle';expected.write_text(canonical(data))
        else:assert expected.read_text()==canonical(data),name+' '+suffix+' independently expected mismatch'

    path=p/(name+'-golden.json')
    if '--write-new' in sys.argv:
        assert not path.exists(),'Never replace an existing golden';path.write_bytes(encoded)
    else:assert path.read_bytes()==encoded,name+' complete canonical literal mismatch'
metadata=json.dumps(scenarios,indent=2,sort_keys=True)+'\n';path=p/'golden-digests.json'
if '--write-new' in sys.argv:
    assert not path.exists(),'Never replace an existing digest oracle';path.write_text(metadata)
else:assert path.read_text()==metadata,'independent literal digest mismatch'
assert templateDigest=='a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669'
print('PASS four complete independently authored canonical fixtures and full SHA256 chains')
