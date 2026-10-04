"""Independent V14 literal fixture/identity/command/full-byte oracle. Existing literals descend from V13 verifier-authored recipes; new task recipes are authored from frozen f4d2 contract before A14/B14 implementation or expected-output inspection. No production imports.

Default mode verifies frozen files; --write creates the initial reviewed fixture bytes.
The escaping recipe is explicitly authored for the System.Text.Json default encoder.
"""
import copy
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
TASK_CONTRACT = 'f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2'
RUN = '12345678-1234-4234-8234-123456789abc'
DIGEST = 'a' * 64
SCOPE = dict(customerId='synthetic-customer', projectId='synthetic-project', environmentId='synthetic-environment')

def string(value):
    parts = ['"']
    escapes = {'\\': '\\\\', '\b': '\\b', '\f': '\\f', '\n': '\\n', '\r': '\\r', '\t': '\\t'}
    for char in value:
        number = ord(char)
        if char in escapes:
            parts.append(escapes[char])
        elif number < 32 or number > 126 or char in '"\'&<>+`':
            for i in range(0, len(char.encode('utf-16-be')), 2):
                parts.append('\\u' + char.encode('utf-16-be')[i:i + 2].hex().upper())
        else:
            parts.append(char)
    return ''.join(parts) + '"'

def canonical(value):
    if isinstance(value, str): return string(value)
    if value is None: return 'null'
    if value is True: return 'true'
    if value is False: return 'false'
    if isinstance(value, int): return str(value)
    if isinstance(value, list): return '[' + ','.join(map(canonical, value)) + ']'
    if isinstance(value, dict): return '{' + ','.join(string(k) + ':' + canonical(value[k]) for k in sorted(value)) + '}'
    raise TypeError(type(value))

def digest(value): return hashlib.sha256(canonical(value).encode()).hexdigest()

VERSIONS = dict(profileVersion='synthetic-profile-v1', desiredOutcomeVersion=None,
    scoringAlgorithmVersion='pilot-health-v1', aiPolicyVersion='synthetic-ai-disabled-v1',
    promptVersion='synthetic-prompt-disabled-v1', modelVersion='synthetic-model-disabled-v1',
    applicationVersion='synthetic-planning-tasks-app-v1', workSchemaVersion='synthetic-run-work-v1',
    scriptedResultsDigest=DIGEST, analysisFixtureDigest=DIGEST, maturityFixtureDigest=DIGEST,
    fixPackageTemplateDigest='a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669',
    fixReviewContractDigest='a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f',
    planningTaskContractDigest=TASK_CONTRACT)
CAPABILITY = dict(matrixVersion='synthetic-analysis-matrix-v1', stateAtLock='FixtureVerified',
    productBuild='fixture-product-v1', databaseSchemaBuild='fixture-facts-v1', hotfixSetDigest='synthetic-hotfix-digest',
    sqlServerBuild='synthetic-sql-build', compatibilityLevel=160,
    modules=[dict(id='SyntheticOperations', version='synthetic-module-v1'), dict(id='SyntheticSecurity', version='synthetic-module-v1')],
    queryPackVersion='synthetic-query-pack-v1', normalizationSchemaVersion='synthetic-normalization-v1',
    ruleCatalogVersion='synthetic-analysis-catalog-v1', lockDigest=DIGEST)
ANALYSIS = dict(scope=SCOPE, packVersion='synthetic-analysis-pack-v1', packDigest=DIGEST,
    presetId='synthetic-analysis-mixed-v1', presetVersion='synthetic-evidence-v1', evidenceDigest=DIGEST,
    catalogVersion='synthetic-analysis-catalog-v1', catalogDigest=DIGEST, profileId='synthetic-analysis-equal-v1',
    profileVersion='synthetic-profile-v1', profileDigest=DIGEST,
    compatibility=dict(sourceProduct='SYNTHETIC-ONLY', productVersion='fixture-product-v1', evidenceSchemaVersion='fixture-facts-v1', ruleLanguageVersion='count-predicate-v1'))
SOURCE = dict(scope=SCOPE, runId=RUN, runRevision=17, runState='Scoring', runInputDigest=DIGEST,
    baselineId='synthetic-analysis-mixed-v1', profileId='synthetic-review-maturity-planning-tasks-equal-v1',
    frozenVersions=VERSIONS, capabilityLock=CAPABILITY, analysisLock=ANALYSIS,
    analysisFixtureDigest=DIGEST, analysisContentDigest=DIGEST, savedCoverageDigest=DIGEST,
    reviewRunId=RUN, reviewRunRevision=17, reviewSnapshotDigest=DIGEST)
GUIDANCE_WARNINGS = [
    'Synthetic fixture guidance only; no actual One Identity defect, supported remediation or customer approval is established.',
    'Every option is review-only and unverified. Finding confirmation, rejection or deferral does not review guidance or validate remediation.',
    'Stable identity ordering is not a priority calculation. Recovery guidance does not establish executed or verified restoration.',
    'This is a current detached value, not durable recommendation history. The run remains Scoring and unpublished.']
GUIDANCE_UNAVAILABLE = [
    'Priority calculation, effort estimates and consultant overrides are unavailable.',
    'Customer-approved objectives, roles and approvals are unavailable.',
    'Fix artifacts, recommendation review, task conversion/workflows and CSV export are unavailable.',
    'Customer risk acceptance, validated remediation, reassessment and actual report publication/sharing are unavailable.',
    'No SQL, script, configuration artifact, customer-system execution, external task connector or ROI calculation exists.']
TEMPLATE_VERSION = 'fictional-fix-templates-v1'
TEMPLATES = [
    dict(templateId='fictional-config-v1', kind='Configuration', text='{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}'),
    dict(templateId='fictional-script-v1', kind='Script', text="# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),
    dict(templateId='fictional-sql-v1', kind='Sql', text="-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")]
DISCLAIMER = 'Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.'
WARNINGS = [
    'Finding confirmation, rejection or deferral does not review artifacts or validate remediation.',
    'Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.',
    'Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.']
EMPTY_WARNING = 'No findings were supplied; no fix packages or actions are available.'
UNAVAILABLE = [
    'Consultant artifact review, approval history and content invalidation are unavailable.',
    'Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.',
    'Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.']
HOSTILE = '<script>globalThis.v11Injected=1</script><img src="https://fixture.invalid/x" onerror="globalThis.v11Injected=2"><form action="https://fixture.invalid/post"></form><svg onload="globalThis.v11Injected=3"></svg> javascript:alert(1) =HYPERLINK("https://fixture.invalid") $(curl https://fixture.invalid) `shell` & café 中文 😀\0NUL sentinel\r\nCRLF sentinel\rCR sentinel'

def input_fixture(hostile=False, empty=False):
    findings = []
    for n in range(2):
        finding_id = ('b' if n == 0 else 'c') * 64
        occurrences = [dict(occurrenceId=('d' if n == 0 else 'e') * 64,
            objectId='FIXTURE-OBJECT-' + str(n + 1), objectType='SyntheticControl',
            moduleId='SyntheticSecurity' if n == 0 else 'SyntheticOperations', originalDigest=('f' if n == 0 else '1') * 64,
            evidenceReference='fixture-evidence:V11-object-' + str(n + 1))]
        if n == 0:
            occurrences.append(dict(occurrenceId='2' * 64, objectId='FIXTURE-OBJECT-3', objectType='SyntheticControl',
                moduleId='SyntheticSecurity', originalDigest='3' * 64, evidenceReference='fixture-evidence:V11-object-3'))
        options = [dict(optionId=id, text='Fictional option ' + id + ' for group ' + str(n + 1),
            prerequisites='Fictional prerequisites ' + id, risk='Fictional risk ' + id,
            recoveryGuidance='Fictional recovery ' + id) for id in ['inspect-v11', 'retain-v11']]
        finding = dict(findingId=finding_id, ruleId='SYN-V11-' + str(n + 1), ruleVersion='synthetic-rule-v1',
            categoryId='SECURITY' if n == 0 else 'OPERATIONS', severity='High' if n == 0 else 'Low',
            originalTitle='Original fictional V11 title ' + str(n + 1), presentationTitle='Current fictional V11 title ' + str(n + 1),
            businessContext='Fictional V11 business context ' + str(n + 1), initialState='Proposed',
            currentState='Confirmed' if n == 0 else 'Deferred', findingRevision=2, rootCause='Same fictional root cause',
            occurrences=occurrences, options=options,
            validationGuidance=['Validate fictional original ' + str(n + 1), 'Collect fictional validation ' + str(n + 1)],
            guidanceReferences=['fixture-guidance:V11-' + str(n + 1)],
            assumptions=['Fictional V11 premise ' + str(n + 1)], limitations=['Fictional V11 limitation ' + str(n + 1)])
        if hostile:
            for key in ['originalTitle', 'presentationTitle', 'businessContext', 'rootCause']:
                finding[key] += ' ' + HOSTILE
            for occurrence in occurrences:
                occurrence['evidenceReference'] += ' ' + HOSTILE
            for option in options:
                for key in ['text', 'prerequisites', 'risk', 'recoveryGuidance']:
                    option[key] += ' ' + HOSTILE
            for key in ['validationGuidance', 'guidanceReferences', 'assumptions', 'limitations']:
                finding[key] = [value + ' ' + HOSTILE for value in finding[key]]
        findings.append(finding)
    return dict(source=copy.deepcopy(SOURCE), findings=[] if empty else findings)

def expected(input):
    findings = copy.deepcopy(input['findings'])
    for finding in findings:
        finding['occurrences'].sort(key=lambda x: x['occurrenceId'])
        finding['options'].sort(key=lambda x: x['optionId'])
        for option in finding['options']:
            option['status'] = 'Unverified'
            option['scopedOptionId'] = digest(dict(findingId=finding['findingId'], optionId=option['optionId'], runId=RUN, scope=SCOPE))
    guidance = dict(schemaVersion='synthetic-recommendation-guidance-v1', status='SyntheticUnverified',
        source=copy.deepcopy(input['source']), findings=sorted(findings, key=lambda x: x['findingId']),
        warnings=GUIDANCE_WARNINGS, unavailableSections=GUIDANCE_UNAVAILABLE)
    guidance['contentDigest'] = digest(guidance)
    packages = []
    for finding in guidance['findings']:
        package_id = digest(dict(findingId=finding['findingId'], runId=RUN, scope=SCOPE))
        options = []
        for option in sorted(finding['options'], key=lambda x: x['scopedOptionId']):
            artifacts = [dict(artifactId=digest(dict(packageId=package_id, scopedOptionId=option['scopedOptionId'],
                templateId=t['templateId'], templateVersion=TEMPLATE_VERSION)), templateId=t['templateId'],
                kind=t['kind'], status='Unverified', text=t['text']) for t in TEMPLATES]
            options.append(dict(scopedOptionId=option['scopedOptionId'], artifacts=artifacts))
        packages.append(dict(packageId=package_id, findingId=finding['findingId'], options=options))
    fix = dict(schemaVersion='synthetic-fix-package-preview-v1', status='Unverified', disclaimer=DISCLAIMER,
        guidance=guidance, templateVersion=TEMPLATE_VERSION, templateDigest=digest(dict(templateVersion=TEMPLATE_VERSION, templates=TEMPLATES)),
        templates=TEMPLATES, packages=packages, warnings=WARNINGS + ([EMPTY_WARNING] if not packages else []), unavailableSections=UNAVAILABLE)
    return guidance, fix

def source_binding(fix):
    source = fix['guidance']['source']
    return dict(scope=copy.deepcopy(source['scope']), runId=source['runId'],
        runRevision=source['runRevision'], runInputDigest=source['runInputDigest'],
        baselineId=source['baselineId'], profileId=source['profileId'],
        applicationVersion=source['frozenVersions']['applicationVersion'],
        contractDigest=source['frozenVersions']['fixReviewContractDigest'],
        sourceDigest=digest(fix), guidanceDigest=fix['guidance']['contentDigest'],
        findingReviewDigest=source['reviewSnapshotDigest'], templateVersion=TEMPLATE_VERSION,
        templateDigest=digest(dict(templateVersion=TEMPLATE_VERSION, templates=TEMPLATES)),
        findingRevisions=[dict(findingId=f['findingId'], revision=f['findingRevision']) for f in fix['guidance']['findings']])

def source_artifacts(fix):
    categories={f['findingId']:f['categoryId'] for f in fix['guidance']['findings']}
    return sorted([dict(findingId=p['findingId'],categoryId=categories[p['findingId']],packageId=p['packageId'],
        scopedOptionId=o['scopedOptionId'],artifactId=a['artifactId'],templateId=a['templateId'],
        kind=a['kind'],artifactTextDigest=hashlib.sha256(a['text'].encode()).hexdigest())
        for p in fix['packages'] for o in p['options'] for a in o['artifacts']], key=lambda a:(a['findingId'],a['artifactId']))

def task_identity(binding, finding_id, option_id):
    return digest(dict(schemaVersion='synthetic-planning-task-identity-v1', scope=copy.deepcopy(binding['scope']),
        runId=binding['runId'], findingId=finding_id, scopedOptionId=option_id))

def task_options(fix):
    binding=source_binding(fix)
    findings={f['findingId']:f for f in fix['guidance']['findings']}
    options=[]
    for package in fix['packages']:
        for option in package['options']:
            ids=sorted(a['artifactId'] for a in option['artifacts'])
            identity=dict(taskId=task_identity(binding,package['findingId'],option['scopedOptionId']),
                findingId=package['findingId'],categoryId=findings[package['findingId']]['categoryId'],
                packageId=package['packageId'],scopedOptionId=option['scopedOptionId'],artifactIds=ids)
            vector=[dict(artifactId=a,revision=0,eventId=None,kind=None,state='Unverified',sourceDigest=None) for a in ids]
            options.append(dict(identity=identity,findingState=findings[package['findingId']]['currentState'],
                currentAttestations=vector,canCreate=False))
    return sorted(options,key=lambda o:o['identity']['taskId'])

def reviewed_vector(option, source_digest):
    return [dict(artifactId=a,revision=1,eventId='aaaaaaaa-0000-4000-8000-00000000000'+str(n+1),
        kind='ReviewForPlanning',state='ReviewedForPlanning',sourceDigest=source_digest)
        for n,a in enumerate(option['identity']['artifactIds'])]

def command_payload(binding, task_id, actor, command):
    return dict(schemaVersion='synthetic-planning-task-command-v1',scope=copy.deepcopy(binding['scope']),
        runId=binding['runId'],taskId=task_id,actorId=actor,command=copy.deepcopy(command))

def generated():
    files={}; cases={}
    for name in ['normal','hostile','empty','source-b','source-return','run-later']:
        fixture=input_fixture(hostile=name=='hostile',empty=name=='empty')
        if name in ['source-b','source-return']:
            fixture['source']['reviewSnapshotDigest']=('4' if name=='source-b' else '5')*64
            for finding in fixture['findings']: finding['findingRevision']=3 if name=='source-b' else 4
            if name=='source-b': fixture['findings'][0]['presentationTitle']='Changed fictional presentation B'
        if name=='run-later':
            fixture['source']['runRevision']=18;fixture['source']['reviewRunRevision']=18
        guidance,fix=expected(fixture)
        artifact_binding=source_binding(fix)
        task_binding=dict(artifactSource=artifact_binding,planningTaskContractDigest=TASK_CONTRACT)
        options=task_options(fix)
        files[name+'-input.json']=(json.dumps(fixture,ensure_ascii=True,indent=2)+'\n').encode()
        files[name+'-guidance-golden.json']=canonical({k:v for k,v in guidance.items() if k!='contentDigest'}).encode()
        files[name+'-fix-golden.json']=canonical(fix).encode()
        files[name+'-artifact-binding-golden.json']=canonical(artifact_binding).encode()
        files[name+'-binding-golden.json']=canonical(task_binding).encode()
        files[name+'-options-golden.json']=canonical(options).encode()
        cases[name]=dict(guidanceDigest=guidance['contentDigest'],packageDigest=digest(fix),snapshot=fix,
            artifactBinding=artifact_binding,binding=task_binding,options=options,artifacts=source_artifacts(fix))
    commands=[]
    normal=cases['normal']; option=normal['options'][0]
    vector=reviewed_vector(option,normal['packageDigest'])
    for n,kind in enumerate(['Create','ReconfirmPlan','StartProgress','ReturnToPlanned','Complete','Cancel','Reopen','Comment']):
        command=dict(eventId='abcdef01-2345-4567-89ab-cdef0123456'+str(n),kind=kind,
            expectedRevision=0 if kind=='Create' else n+1,expectedSourceDigest=normal['packageDigest'],
            expectedAttestations=copy.deepcopy(vector),reason='  Fictional V14 reason café 中文 😀\0NUL\r\nCRLF\rCR  ')
        payload=command_payload(normal['artifactBinding'],option['identity']['taskId'],'synthetic-consultant',command)
        commands.append(dict(payload=payload,canonical=canonical(payload),digest=digest(payload)))
    variants={}
    variants['unreviewed']=copy.deepcopy(option['currentAttestations'])
    variants['reviewed']=copy.deepcopy(vector)
    variants['withdrawn']=copy.deepcopy(vector)
    variants['withdrawn'][0].update(revision=2,eventId='bbbbbbbb-0000-4000-8000-000000000001',kind='WithdrawReview',state='Unverified')
    variants['rereviewed']=copy.deepcopy(vector)
    variants['rereviewed'][0].update(revision=3,eventId='cccccccc-0000-4000-8000-000000000001')
    variants['old-source-needs-review']=copy.deepcopy(vector)
    for entry in variants['old-source-needs-review']:entry['state']='NeedsReview'
    files['selected-vector-golden.json']=(json.dumps(variants,ensure_ascii=True,indent=2)+'\n').encode()
    files['command-golden.json']=(json.dumps(commands,ensure_ascii=True,indent=2)+'\n').encode()
    identity_variants=[]
    binding=normal['artifactBinding'];finding=option['identity']['findingId'];opt=option['identity']['scopedOptionId']
    for coordinate in ['unchanged','customerId','projectId','environmentId','runId','findingId','scopedOptionId']:
        b=copy.deepcopy(binding);f=finding;o=opt
        if coordinate in ['customerId','projectId','environmentId']:b['scope'][coordinate]+='-other'
        elif coordinate=='runId':b['runId']='87654321-4321-4321-8321-cba987654321'
        elif coordinate=='findingId':f='9'*64
        elif coordinate=='scopedOptionId':o='8'*64
        identity_variants.append(dict(coordinate=coordinate,scope=b['scope'],runId=b['runId'],findingId=f,scopedOptionId=o,
            canonical=canonical(dict(schemaVersion='synthetic-planning-task-identity-v1',scope=b['scope'],runId=b['runId'],findingId=f,scopedOptionId=o)),taskId=task_identity(b,f,o)))
    files['identity-golden.json']=(json.dumps(identity_variants,ensure_ascii=True,indent=2)+'\n').encode()
    manifest=dict(schemaVersion='v14-independent-expectations-v1',contractDigest=TASK_CONTRACT,disclaimer=DISCLAIMER,
        templateDigest=digest(dict(templateVersion=TEMPLATE_VERSION,templates=TEMPLATES)),cases=cases)
    files['expected-manifest.json']=(json.dumps(manifest,ensure_ascii=True,indent=2)+'\n').encode()
    files['oracle-provenance.json']=(json.dumps(dict(schemaVersion='v14-independent-oracle-v1',
        provenance='V13 independent verifier-authored template/guidance primitives plus explicit frozen f4d2 task identity/source/command recipes; no A14/B14 imports or output captures',
        files=[dict(path=k,sha256=hashlib.sha256(v).hexdigest(),bytes=len(v)) for k,v in sorted(files.items())]),indent=2)+'\n').encode()
    return files

if __name__=='__main__':
    files=generated()
    if sys.argv[1:]==['--write']:
        for name,value in files.items():(HERE/name).write_bytes(value)
    elif sys.argv[1:]:raise SystemExit('FAIL invalid oracle arguments')
    else:
        for name,value in files.items():
            if (HERE/name).read_bytes()!=value:raise SystemExit('FAIL independent oracle mismatch: '+name)
    print('PASS V14 independent literal oracle; files='+str(len(files))+'; normal-bytes='+str(len(files['normal-fix-golden.json']))+'; normal-sha256='+hashlib.sha256(files['normal-fix-golden.json']).hexdigest())
