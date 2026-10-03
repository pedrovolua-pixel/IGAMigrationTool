#!/usr/bin/env python3
"""Independent offline canonical/ranking oracle; no product assembly imported."""
import hashlib
import json
import struct
from pathlib import Path
from fractions import Fraction

KINDS = 'SourceBuild EnvironmentEvidence Capability Baseline ReassessmentBaseline Collection Query Normalization Catalog Profile Scoring Maturity AiProvider AiModel AiPrompt AiSchema AiSettings Application Reviewer ReviewerEligibility Instruction Conflict ReviewEvents'.split()
SEED = '01' * 32

def canonical(value):
    # Utf8JsonWriter default encoder escapes '+' in UTC offset dates.
    return json.dumps(value, separators=(',', ':'), ensure_ascii=True).replace('+', chr(92) + 'u002B').encode('utf-8')

def digest(value):
    return hashlib.sha256(value).hexdigest()

def versions():
    return dict(schema='synthetic-evaluation-sampling-versions-v1', bindings=[dict(kind=k, version='synthetic-' + k.lower()) for k in KINDS], correctionCutoffUtc='2026-10-03T00:00:00.0000000+00:00', evaluationDateUtc='2026-10-03T01:00:00.0000000+00:00', predecessorSampleDigest=None)

def member(i, environment='synthetic-env', severity='Medium'):
    return dict(id=f'synthetic-member-{i:06d}', scopeId='synthetic-scope', environmentId=environment, primaryModuleId='synthetic-module', categoryId='synthetic-category', ruleVersion='synthetic-rule', modelPromptVersion='synthetic-model-prompt', confidenceBandId='synthetic-confidence', severity=severity, primarySettled=True, available=True, unavailableReason=None, secondaryModuleIds=['synthetic-secondary'])

def population(members):
    return dict(schema='synthetic-evaluation-sampling-population-v1', populationId='synthetic-population', scopeId='synthetic-scope', versionManifestDigest=digest(canonical(versions())), members=sorted(members, key=lambda m: m['id']))

def rank_bytes(m, population_digest):
    result = b'iga.synthetic-evaluation.sample-rank.v1\0' + bytes.fromhex(SEED)
    for text in ['synthetic-scope', population_digest, m['environmentId'], m['primaryModuleId'], m['categoryId'], m['severity'], m['id']]:
        value = text.encode('utf-8')
        result += struct.pack('>I', len(value)) + value
    return result

def allocate(sizes, k):
    if k == sum(sizes):
        return list(sizes)
    remaining = k - len(sizes)
    capacities = [s - 1 for s in sizes]
    if not remaining:
        return [1] * len(sizes)
    exact = [Fraction(remaining * capacity, sum(capacities)) for capacity in capacities]
    floors = [int(x) for x in exact]
    extras = remaining - sum(floors)
    order = sorted(range(len(sizes)), key=lambda i: (-(exact[i] - floors[i]), i))
    for i in order[:extras]:
        floors[i] += 1
    return [1 + x for x in floors]

if __name__ == '__main__':
    members = [member(1), member(2, 'synthetic-env-b', 'Low')]
    p = canonical(population(members))
    rank = rank_bytes(members[0], digest(p))
    big = [member(i) for i in range(120)]
    big_digest = digest(canonical(population(big)))
    selected = sorted(m['id'] for m in sorted(big, key=lambda m: (digest(rank_bytes(m, big_digest)), m['id']))[:100])
    sample = dict(schema='synthetic-evaluation-sampling-v1', sampleId='synthetic-sample', populationId='synthetic-population', scopeId='synthetic-scope', populationDigest=digest(p), versionManifestDigest=digest(canonical(versions())), seed=SEED, hardReviewBudget=None, strata=[dict(environmentId=m['environmentId'], primaryModuleId=m['primaryModuleId'], categoryId=m['categoryId'], severity=m['severity'], populationCount=1, allocation=1) for m in members], ranks=[dict(memberId=m['id'], rankDigest=digest(rank_bytes(m, digest(p)))) for m in members], selected=[dict(memberId=m['id'], mandatory=False, rankDigest=digest(rank_bytes(m, digest(p)))) for m in members])
    output = dict(sampleJson=canonical(sample).decode(), sampleDigest=digest(canonical(sample)), versionJson=canonical(versions()).decode(), versionDigest=digest(canonical(versions())), populationJson=p.decode(), populationDigest=digest(p), rankHex=rank.hex(), rankDigest=digest(rank), selectedExcludedIds=sorted(set(m['id'] for m in big) - set(selected)), allocation2345=allocate([2,4,5],7))
    assert output['allocation2345'] == [2,2,3]
    # Independent calculations must match the committed literal contract, not just print.
    contract = Path(__file__).resolve().parents[3] / 'specs/003-health-assessment/synthetic-evaluation-sampling-v1-contract.md'
    fenced = contract.read_text().split('### Literal Python goldens', 1)[1].split('```json', 1)[1].split('```', 1)[0]
    expected = json.loads(fenced)
    assert output == expected, 'calculated canonical/ranking/allocation values differ from committed literal goldens'
    print(json.dumps(output, indent=2))
