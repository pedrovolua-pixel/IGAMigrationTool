"""Independent contract oracle; authored before implementation and uses no module code."""
import hashlib, json
from pathlib import Path
ROOT = Path(__file__).parent
KINDS = ['PublishedStatus', 'Coverage', 'Scores', 'Findings', 'Recommendations', 'ProtectedReferences']
def canonical(obj):
    return json.dumps(obj, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
def digest(blob):
    return hashlib.sha256(blob).hexdigest()
def payloads():
    values = [('PublishedStatus', dict(itemId='syn-status', category='Summary', assessmentState='CompletedWithGaps', approvalState='SyntheticApproved', limitations=['DeclaredGap'])),
              ('Coverage', dict(itemId='syn-coverage-a', category='Summary', assessed=7, gap=2, unavailable=1, label='Fictional coverage.', limitations=['DeclaredGap', 'Unavailable'])),
              ('Coverage', dict(itemId='syn-coverage-b', category='Operations', assessed=3, gap=0, unavailable=0, label='Fictional coverage.', limitations=[])),
              ('Scores', dict(itemId='syn-score', category='Summary', health=87.25, quality=91.125, maturity=3, reason='None'))]
    for suffix, category in [('a', 'Summary'), ('b', 'Configuration'), ('c', 'Identity')]:
        values.append(('Findings', dict(itemId='syn-finding-'+suffix, category=category, title='Fictional finding.', summary='Fictional summary.', severity='High' if suffix == 'a' else 'Low', reviewState='Confirmed', confidence='Medium', mandatoryReview=True, referenceIds=['syn-ref-a', 'syn-ref-b'])))
        values.append(('Recommendations', dict(itemId='syn-rec-'+suffix, category='Summary', findingId='syn-finding-'+suffix, summary='Fictional recommendation.', options=['Fictional option.'], priority='Fictional priority.', effort='Fictional effort.', reviewLabel='Unverified')))
    values.extend([('ProtectedReferences', dict(itemId='syn-ref-a', category='Configuration', availability='Available', reason='None')), ('ProtectedReferences', dict(itemId='syn-ref-b', category='Identity', availability='Unavailable', reason='Expired'))])
    return sorted(values, key=lambda pair: (KINDS.index(pair[0]), pair[1]['itemId']))
def manifest(items):
    return dict(contractVersion='synthetic-published-health-read-v1', fixtureKind='SyntheticPublishedFixture', customerId='syn-customer-a', projectId='syn-project-a', environmentId='syn-environment-a', assessmentId='syn-assessment-a', reportVersionId='syn-report-a', baselineVersion='syn-baseline-v1', catalogVersion='syn-catalog-v1', scoringProfileVersion='syn-scoring-v1', maturityProfileVersion='syn-maturity-v1', applicationVersion='syn-application-v1', assessmentState='CompletedWithGaps', approvalState='SyntheticApproved', collections=KINDS, items=items)
def corpus():
    requests = []
    base = dict(contractVersion='synthetic-published-health-read-v1', resourceKind='Findings', assessmentId='syn-assessment-a', reportVersionId='syn-report-a')
    verbs = ['Start', 'Run', 'Cancel', 'Resume', 'Comment', 'Edit', 'Disposition', 'AcceptRisk', 'Publish', 'Acknowledge', 'Export', 'CreateLink', 'CreateTask', 'Remediate', 'Migrate', 'RawEvidence', 'ArbitraryTool', 'findings', ' Findings', 'Findings ', 'FINDINGS', 'Findings.read', '../Findings', '\u202eFindings']
    for verb in verbs:
        requests.append(canonical(base | {'resourceKind': verb}).decode())
    for field in ['customerId', 'projectId', 'environmentId', 'role', 'roles', '$type', 'storageLocator', 'path', 'url', 'filter', 'rawEvidence', 'method']:
        requests.append(canonical(base | {field:'PROTECTED-SENTINEL'}).decode())
    requests.extend(['{"contractVersion":"synthetic-published-health-read-v1","resourceKind":"Findings","resourceKind":"Scores","assessmentId":"syn-assessment-a","reportVersionId":"syn-report-a"}', canonical(base | {'assessmentId':'../../secret'}).decode(), canonical(base | {'reportVersionId':'https://fictional.invalid/secret'}).decode(), 'null', '{}', '[]', '{', canonical(base | {'pageSize':0}).decode(), canonical(base | {'pageSize':101}).decode(), canonical(base | {'pageSize':1.5}).decode(), canonical(base | {'pageSize':'25'}).decode()])
    return requests
if __name__ == '__main__':
    import sys
    blobs = {}
    descriptors = []
    for kind, obj in payloads():
        name = obj['itemId']+'.json'
        blob = canonical(obj)
        blobs[name] = blob
        descriptors.append(dict(kind=kind, itemId=obj['itemId'], category=obj['category'], payloadDigest=digest(blob)))
    blobs['manifest.json'] = canonical(manifest(descriptors))
    hashes = {name:digest(blob) for name, blob in blobs.items()}
    blobs['denial-corpus.json'] = canonical(corpus())
    hashes['denial-corpus.json'] = digest(blobs['denial-corpus.json'])
    if '--verify' in sys.argv:
        expected = json.loads((ROOT/'golden-hashes.json').read_text())
        assert hashes == expected, 'Authored oracle digest drift'
        for name, blob in blobs.items():
            assert (ROOT/name).read_bytes() == blob, name+' authored byte drift'
        print('PASS independent Python oracle: '+str(len(blobs))+' exact byte/hash artifacts; '+str(len(corpus()))+' denial requests')
    else:
        for name, blob in blobs.items():
            (ROOT/name).write_bytes(blob)
        (ROOT/'golden-hashes.json').write_bytes(canonical(hashes))
        print(hashes['manifest.json'])
