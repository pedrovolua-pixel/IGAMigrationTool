"""Verify immutable B12 source, execution and runtime bindings without executing application code."""
from pathlib import Path
import hashlib,json,sys
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
meta=json.loads((HERE/'execution-metadata.json').read_text())
count=0
for b in meta['bindings']:
 if '--source-only' in sys.argv and b['kind'] in ['runtime','artifact','worker-build']:continue
 p=Path(b['path']) if b['path'].startswith('/') else ROOT/b['path']
 data=p.read_bytes()
 assert len(data)==b['bytes'],b['path']+' byte count'
 assert hashlib.sha256(data).hexdigest()==b['sha256'],b['path']+' SHA256'
 count+=1
print('B12 exact evidence verification PASS: %d bindings'%count)
