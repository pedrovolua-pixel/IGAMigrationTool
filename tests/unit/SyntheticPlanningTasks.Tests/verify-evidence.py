"""Read-only verification of the sealed original A14 packet; no test execution."""
import hashlib
import json
import pathlib
import sys

packet = pathlib.Path(__file__).resolve().parent
root = packet.parents[2]
record = json.loads((packet / 'execution.json').read_text())
checks = 0
for binding in record['bindings']:
    relative = pathlib.PurePosixPath(binding['path'])
    if relative.is_absolute() or '..' in relative.parts:
        raise SystemExit('FAIL unsafe evidence path')
    path = root.joinpath(*relative.parts)
    data = path.read_bytes()
    if len(data) != binding['bytes'] or hashlib.sha256(data).hexdigest() != binding['sha256']:
        raise SystemExit('FAIL binding ' + str(relative))
    checks += 1
for pair in record['finalExecutedSourceEqualities']:
    actual = root / pair['actual']
    frozen = root / pair['frozen']
    if actual.read_bytes() != frozen.read_bytes():
        raise SystemExit('FAIL final executed source equality ' + pair['actual'])
    checks += 1
for generation in record['generationChecks']:
    metadata = json.loads((root / generation['metadata']).read_text())
    if len(metadata['inputs']) != len(generation['inputFiles']):
        raise SystemExit('FAIL generation input inventory')
    for item, location in zip(metadata['inputs'], generation['inputFiles']):
        frozen = root / location
        data = frozen.read_bytes()
        if len(data) != item['bytes'] or hashlib.sha256(data).hexdigest() != item['sha256']:
            raise SystemExit('FAIL frozen generation input ' + str(frozen))
        checks += 1
    for command in metadata['commands']:
        if hashlib.sha256((root / command['log']).read_bytes()).hexdigest() != command['logSha256']:
            raise SystemExit('FAIL original command log')
        checks += 1
for equality in record['coordinatorLockEqualities']:
    original = json.loads((root / equality['coordinatorMetadata']).read_text())
    matched = [item for item in original['source'] if item['path'] == equality['path']]
    if len(matched) != 1 or matched[0]['sha256'] != equality['sha256']:
        raise SystemExit('FAIL coordinator audited lock binding')
    if hashlib.sha256((root / equality['path']).read_bytes()).hexdigest() != equality['sha256']:
        raise SystemExit('FAIL consumed lock equality')
    checks += 1
print('PASS', checks, 'original A14 bindings/equalities/generation source/log checks; no runtime or database execution')
sys.exit(0)
