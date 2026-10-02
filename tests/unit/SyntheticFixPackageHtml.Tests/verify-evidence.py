"""Verify sealed B11 source/binary/transcript bindings; optional --source-only after relocation."""
import hashlib
import json
from pathlib import Path
import sys

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
metadata=json.loads((HERE/'execution-metadata.json').read_text())
count=0
for binding in metadata['bindings']:
    if '--source-only' in sys.argv and binding['kind']=='binary': continue
    path=ROOT/binding['path']
    content=path.read_bytes()
    assert len(content)==binding['bytes'],binding['path']+' byte count'
    assert hashlib.sha256(content).hexdigest()==binding['sha256'],binding['path']+' SHA256'
    count+=1
print('B11 evidence verification PASS: %d exact source/binary/transcript bindings'%count)
