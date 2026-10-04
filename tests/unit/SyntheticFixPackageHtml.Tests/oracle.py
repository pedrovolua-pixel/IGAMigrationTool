"""Independent literal contract/primitive oracle. Does not read/call implementation."""
import hashlib
import json
from pathlib import Path
import sys

BASE = Path(__file__).parent

def jstr(text):
    out = '"'
    for c in text:
        n = ord(c)
        if c == '\\': out += '\\\\'
        elif c in '\b\f\n\r\t': out += dict(zip('\b\f\n\r\t', ['\\b','\\f','\\n','\\r','\\t']))[c]
        elif n < 32 or n >= 127 or c in '<>&\'"+`':
            out += ''.join('\\u%04X' % unit for unit in ([n] if n <= 65535 else [0xD800+((n-65536)>>10), 0xDC00+((n-65536)&1023)]))
        else: out += c
    return out + '"'

def canonical(v):
    if isinstance(v, str): return jstr(v)
    if isinstance(v, dict): return '{'+','.join(jstr(k)+':'+canonical(v[k]) for k in sorted(v))+'}'
    if isinstance(v, list): return '['+','.join(canonical(x) for x in v)+']'
    if v is None: return 'null'
    if v is True: return 'true'
    if v is False: return 'false'
    if isinstance(v, int): return str(v)
    raise ValueError('fixture primitive unsupported')

def sha(v): return hashlib.sha256(v.encode()).hexdigest()
def identity(v): return sha(canonical(v))
def enc(s):
    out = ''
    for c in s:
        n = ord(c)
        if c in '<>&"': out += {'<':'&lt;','>':'&gt;','&':'&amp;','"':'&quot;'}[c]
        elif n < 32 or n >= 127 or c in "'+`": out += '&#x%X;' % n
        else: out += c
    return out

def field(k,v): return '<dl><dt>'+enc(k)+'</dt><dd>'+enc(v)+'</dd></dl>'
def code(v): return '<pre><code>'+enc(v)+'</code></pre>'
def listing(v): return '<ul>'+''.join('<li>'+enc(x)+'</li>' for x in v)+'</ul>'
def tree(v):
    if isinstance(v,dict): return '<dl>'+''.join('<dt>'+enc(k)+'</dt><dd>'+tree(x)+'</dd>' for k,x in v.items())+'</dl>'
    if isinstance(v,list): return '<ol>'+''.join('<li>'+tree(x)+'</li>' for x in v)+'</ol>'
    if isinstance(v,str): return enc(v)
    if v is None: return 'Not supplied'
    return canonical(v)

D='1'*64
scope=dict(customerId='synthetic-customer',projectId='synthetic-project',environmentId='synthetic-environment')
run='11111111-2222-4333-8444-555555555555'
source=dict(scope=scope,runId=run,runRevision=7,runState='Scoring',runInputDigest=D,baselineId='synthetic-analysis-findings-v1',profileId='synthetic-review-maturity-equal-v1',
 frozenVersions=dict(profileVersion='synthetic-profile-v1',desiredOutcomeVersion=None,scoringAlgorithmVersion='pilot-health-v1',aiPolicyVersion='synthetic-ai-disabled-v1',promptVersion='synthetic-prompt-disabled-v1',modelVersion='synthetic-model-disabled-v1',applicationVersion='synthetic-review-maturity-app-v1',workSchemaVersion='synthetic-run-work-v1',scriptedResultsDigest=D,analysisFixtureDigest=D,maturityFixtureDigest=D),
 capabilityLock=dict(matrixVersion='synthetic-analysis-matrix-v1',stateAtLock='FixtureVerified',productBuild='fixture-product-v1',databaseSchemaBuild='fixture-facts-v1',hotfixSetDigest='synthetic-hotfix-digest',sqlServerBuild='synthetic-sql-build',compatibilityLevel=160,modules=[dict(id='SyntheticOperations',version='synthetic-module-v1'),dict(id='SyntheticSecurity',version='synthetic-module-v1')],queryPackVersion='synthetic-query-pack-v1',normalizationSchemaVersion='synthetic-normalization-v1',ruleCatalogVersion='synthetic-analysis-catalog-v1',lockDigest=D),
 analysisLock=dict(scope=scope,packVersion='synthetic-analysis-pack-v1',packDigest=D,presetId='synthetic-analysis-findings-v1',presetVersion='synthetic-evidence-v1',evidenceDigest=D,catalogVersion='synthetic-analysis-catalog-v1',catalogDigest=D,profileId='synthetic-analysis-equal-v1',profileVersion='synthetic-profile-v1',profileDigest=D,compatibility=dict(sourceProduct='SYNTHETIC-ONLY',productVersion='fixture-product-v1',evidenceSchemaVersion='fixture-facts-v1',ruleLanguageVersion='count-predicate-v1')),
 analysisFixtureDigest=D,analysisContentDigest='2'*64,savedCoverageDigest='3'*64,reviewRunId=run,reviewRunRevision=7,reviewSnapshotDigest='4'*64)
finding=dict(findingId='5'*64,ruleId='FICTIONAL-B11',ruleVersion='synthetic-rule-v1',categoryId='SECURITY',severity='Critical',originalTitle='Original <script>never()</script>',presentationTitle='Current café Ω 中文 😀',businessContext='Context "quoted" & <untrusted>',initialState='Proposed',currentState='Confirmed',findingRevision=2,rootCause='Fictional root cause =SUM(A1:A2)',
 occurrences=[dict(occurrenceId='6'*64,objectId='OBJECT-B11',objectType='SyntheticControl',moduleId='SyntheticSecurity',originalDigest='7'*64,evidenceReference='https://example.invalid/evidence?x=1&y=2')],
 options=[dict(optionId='fixture-option',text='Review $(curl https://example.invalid) only.',prerequisites='No execution; review required.',risk='Unverified <img src=x onerror=alert(1)>',recoveryGuidance='No restore was attempted.')],
 validationGuidance=['Validate fictional baseline.','No customer validation.'],guidanceReferences=['javascript:alert(1) is a reference string'],assumptions=['Fictional premise + context.'],limitations=['No customer evidence.'])
INPUT=dict(source=source,findings=[finding])
GW=['Synthetic fixture guidance only; no actual One Identity defect, supported remediation or customer approval is established.','Every option is review-only and unverified. Finding confirmation, rejection or deferral does not review guidance or validate remediation.','Stable identity ordering is not a priority calculation. Recovery guidance does not establish executed or verified restoration.','This is a current detached value, not durable recommendation history. The run remains Scoring and unpublished.']
GU=['Priority calculation, effort estimates and consultant overrides are unavailable.','Customer-approved objectives, roles and approvals are unavailable.','Fix artifacts, recommendation review, task conversion/workflows and CSV export are unavailable.','Customer risk acceptance, validated remediation, reassessment and actual report publication/sharing are unavailable.','No SQL, script, configuration artifact, customer-system execution, external task connector or ROI calculation exists.']
DISCLAIMER='Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.'
WARN=['Finding confirmation, rejection or deferral does not review artifacts or validate remediation.','Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.','Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.']
UNAVAILABLE=['Consultant artifact review, approval history and content invalidation are unavailable.','Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.','Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.']
TEMPLATES=[dict(templateId='fictional-config-v1',kind='Configuration',text='{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}'),dict(templateId='fictional-script-v1',kind='Script',text="# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),dict(templateId='fictional-sql-v1',kind='Sql',text="-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")]

def package_fixture(inp):
    src=dict(inp['source'])
    for k in ['frozenVersions','capabilityLock','analysisLock']: src[k]=json.loads(canonical(src[k]))
    fs=[]
    for f in sorted(inp['findings'],key=lambda f:f['findingId']):
        f=dict(f)
        f['occurrences']=sorted(f['occurrences'],key=lambda x:x['occurrenceId'])
        f['options']=[dict(scopedOptionId=identity(dict(findingId=f['findingId'],optionId=o['optionId'],runId=run,scope=scope)),optionId=o['optionId'],status='Unverified',**{k:v for k,v in o.items() if k!='optionId'}) for o in sorted(f['options'],key=lambda x:x['optionId'])]
        fs.append(f)
    g=dict(schemaVersion='synthetic-recommendation-guidance-v1',status='SyntheticUnverified',source=src,contentDigest='',findings=fs,warnings=GW,unavailableSections=GU)
    g['contentDigest']=identity({k:v for k,v in g.items() if k!='contentDigest'})
    packages=[]
    for f in fs:
        pid=identity(dict(findingId=f['findingId'],runId=run,scope=scope))
        opts=[]
        for o in sorted(f['options'],key=lambda x:x['scopedOptionId']):
            artifacts=[dict(artifactId=identity(dict(packageId=pid,scopedOptionId=o['scopedOptionId'],templateId=t['templateId'],templateVersion='fictional-fix-templates-v1')),templateId=t['templateId'],kind=t['kind'],status='Unverified',text=t['text']) for t in TEMPLATES]
            opts.append(dict(scopedOptionId=o['scopedOptionId'],artifacts=artifacts))
        packages.append(dict(packageId=pid,findingId=f['findingId'],options=opts))
    return dict(schemaVersion='synthetic-fix-package-preview-v1',status='Unverified',disclaimer=DISCLAIMER,guidance=g,templateVersion='fictional-fix-templates-v1',templateDigest=identity(dict(templateVersion='fictional-fix-templates-v1',templates=TEMPLATES)),templates=TEMPLATES,packages=packages,warnings=WARN+(['No findings were supplied; no fix packages or actions are available.'] if not fs else []),unavailableSections=UNAVAILABLE)

START='''<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'"><title>Fictional fix-package preview</title><style>html{color:#17212b;background:#fff;font:1rem/1.6 system-ui,sans-serif}body{margin:0}main,header,nav{max-width:70rem;margin:auto;padding:1rem}*{box-sizing:border-box;min-width:0}h1,h2,h3,h4,p,li,dt,dd,summary,code{overflow-wrap:anywhere}a{color:#003f86}a:focus-visible,summary:focus-visible{outline:3px solid #003f86;outline-offset:4px}details{margin:1rem 0;border:1px solid #687787;padding:.75rem}summary{cursor:pointer;font-weight:700}dl{margin:.5rem 0}dt{font-weight:700}dd{margin:0 0 .75rem 1rem}ol,ul{padding-left:1.5rem}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f4f7;padding:.75rem}code{font-size:.9rem}.warning{border-left:.3rem solid #8a3500;padding:.75rem;background:#fff5e8}.skip{display:inline-block;padding:.75rem}</style></head><body><a class="skip" href="#main">Skip to preview</a><header><h1>Fictional fix-package preview</h1>'''
MAIN='</header><nav aria-label="Preview sections"><a href="#packages">Packages</a> · <a href="#historical">Historical guidance</a> · <a href="#provenance">Source and integrity</a></nav><main id="main" tabindex="-1">'
def render(v):
    out=START+'<p class="warning">'+enc(v['disclaimer'])+'</p><p>Status: '+enc(v['status'])+'</p>'+MAIN
    out+='<section><h2>Current preview boundary</h2>'+listing(v['warnings'])+listing(v['unavailableSections'])+'</section><section id="packages"><h2>Fictional packages — Unverified</h2>'
    if not v['packages']: out+='<p class="warning">No findings were supplied; no fix packages or actions are available. Empty input does not establish a healthy environment.</p>'
    for p in v['packages']:
        out+='<details><summary>Package for finding '+enc(p['findingId'])+'</summary>'+field('Package ID',p['packageId'])+field('Finding ID',p['findingId'])
        for o in p['options']:
            out+='<details><summary>Option '+enc(o['scopedOptionId'])+' — Unverified artifacts</summary>'+field('Scoped option ID',o['scopedOptionId'])
            for a in o['artifacts']:
                out+='<article><h3>'+enc(a['kind'])+' — '+enc(a['status'])+'</h3>'+field('Artifact ID',a['artifactId'])+field('Template ID',a['templateId'])+field('Kind',a['kind'])+field('Artifact status',a['status'])+code(a['text'])+'</article>'
            out+='</details>'
        out+='</details>'
    out+='</section><section><h2>Fixed fictional templates</h2>'
    for t in v['templates']: out+='<details><summary>'+enc(t['kind'])+' template — Unverified</summary>'+field('Template ID',t['templateId'])+field('Kind',t['kind'])+code(t['text'])+'</details>'
    g=v['guidance']
    out+='</section><section id="historical"><h2>Historical upstream guidance</h2><p>These source warnings and unavailable sections describe the earlier guidance projection. The current fictional preview boundary is stated above. Finding decisions do not review artifacts.</p>'+field('Guidance schema',g['schemaVersion'])+field('Guidance status',g['status'])+'<details><summary>Historical upstream warnings and unavailable sections</summary>'+listing(g['warnings'])+listing(g['unavailableSections'])+'</details>'
    for f in g['findings']: out+='<details><summary>'+enc(f['presentationTitle'])+' — finding state: '+enc(f['currentState'])+'</summary>'+tree(f)+'</details>'
    out+='</section><section id="provenance"><h2>Source and integrity</h2><details><summary>Source bindings, frozen versions and locks</summary>'+tree(g['source'])+'</details><details><summary>Preview versions and digests</summary>'
    for k,x in [('Preview schema',v['schemaVersion']),('Template version',v['templateVersion']),('Template digest',v['templateDigest']),('Guidance digest',g['contentDigest']),('Package digest',identity(v))]: out+=field(k,x)
    return out+'</details></section></main></body></html>\n'

if __name__=='__main__':
    expected={'fixture-input.json':json.dumps(INPUT,ensure_ascii=False,indent=2)}
    hashes={}
    for name,inp in [('complete',INPUT),('empty',dict(source=source,findings=[]))]:
        v=package_fixture(inp)
        expected[name+'-package.json']=canonical(v)
        expected[name+'.html']=render(v)
        hashes[name]=dict(package=identity(v),html=sha(expected[name+'.html']),guidance=v['guidance']['contentDigest'])
    expected['oracle-hashes.json']=json.dumps(hashes,indent=2)+'\n'
    for name,value in expected.items():
        if '--verify' in sys.argv: assert (BASE/name).read_bytes()==value.encode(),name
        else: (BASE/name).write_bytes(value.encode())
    print('B11 independent literal oracle: 6 exact UTF-8 artifacts '+('verified' if '--verify' in sys.argv else 'authored'))
