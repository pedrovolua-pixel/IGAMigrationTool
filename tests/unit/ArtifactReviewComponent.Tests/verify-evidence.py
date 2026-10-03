"""Read-only exact B13 source/tool/runtime/output binding verification."""
from pathlib import Path
import hashlib,json,sys
here=Path(__file__).resolve().parent
root=here.parents[2]
meta=json.loads((here/'execution-metadata.json').read_text())
count=0
for binding in meta['bindings']:
    if '--source-only' in sys.argv and binding['kind'] in ('runtime','tool','artifact','transcript','snapshot','worker-build'):
        continue
    path=Path(binding['path']) if binding['path'].startswith('/') else root/binding['path']
    data=path.read_bytes()
    assert len(data)==binding['bytes'],binding['path']+' byte count'
    assert hashlib.sha256(data).hexdigest()==binding['sha256'],binding['path']+' SHA256'
    if 'symlink' in binding:assert path.is_symlink() and str(path.readlink())==binding['symlink'],binding['path']+' link'
    count+=1
print('B13 evidence PASS: '+str(count)+' bindings')
