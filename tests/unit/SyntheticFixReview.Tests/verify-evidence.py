"""Read-only recomputation of original worker source/output bindings; never executes the store."""
import hashlib,json,pathlib,sys
p=pathlib.Path(__file__).resolve().parent
record=json.loads((p/'execution.json').read_text())
root=p.parents[2]
for binding in record['bindings']:
 path=pathlib.Path(binding['path'])
 if not path.is_absolute():path=root/path
 data=path.read_bytes()
 assert len(data)==binding['bytes'] and hashlib.sha256(data).hexdigest()==binding['sha256'],str(path)
for equality in record['copyEqualities']:
 left=root/equality['copy'];right=root/equality['original']
 assert left.read_bytes()==right.read_bytes(),equality
print('PASS '+str(len(record['bindings']))+' exact original source/output bindings and '+str(len(record['copyEqualities']))+' immutable copy equalities')
