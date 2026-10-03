"""Read-only recomputation of original B14 execution bindings."""
import hashlib,json,pathlib,sys
here=pathlib.Path(__file__).resolve().parent;root=here.parents[2]
meta=json.loads((here/'execution-metadata.json').read_text())
for item in meta['bindings']:
    path=pathlib.Path(item['path']) if item['kind']=='external' else root/item['path']
    assert path.is_file(),str(path)
    assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256'],str(path)
    assert path.stat().st_size==item['bytes'],str(path)
for pair in meta['equalities']:
    a=root/pair['left'];b=root/pair['right'];assert a.read_bytes()==b.read_bytes(),pair
print('PASS B14 '+str(len(meta['bindings']))+' exact bindings / '+str(len(meta['equalities']))+' original copy equalities')
