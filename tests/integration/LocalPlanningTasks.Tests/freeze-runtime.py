#!/usr/bin/env python3
"""Preserve an executed, verifier-owned original closure before any rebuild."""
import pathlib, shutil, subprocess, tarfile, io, json, hashlib, sys
root=pathlib.Path(__file__).resolve().parents[3]
owned=root/'tests/integration/LocalPlanningTasks.Tests'
name=sys.argv[1]
assert name and name.replace('-','').isalnum()
dest=owned/'.frozen'/name
dest.mkdir(parents=True,exist_ok=False)
shutil.copytree(owned/'bin/Release/net10.0',dest/'runtime')
(dest/'logs').mkdir()
for name in sys.argv[2:]:
    assert pathlib.Path(name).name==name
    shutil.copy2(owned/'.native'/name,dest/'logs'/name)
source=dest/'source';source.mkdir()
paths=['src/server','src/web','migrations','Directory.Build.props','global.json','tests/integration/LocalPlanningTasks.Tests']
data=subprocess.check_output(['git','archive','HEAD',*paths],cwd=root)
with tarfile.open(fileobj=io.BytesIO(data)) as archive:
    for member in archive.getmembers():
        assert not pathlib.Path(member.name).is_absolute() and '..' not in pathlib.Path(member.name).parts
    archive.extractall(source)
for p in owned.glob('*.cs'):
    target=source/p.relative_to(root);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
records=[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size} for p in sorted(dest.rglob('*')) if p.is_file()]
manifest=dest/'closure.json'
manifest.write_text(json.dumps({'sourceHeadCheckpoint':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'bindings':records},indent=2)+'\n')
print(json.dumps({'bindings':len(records),'path':str(manifest),'sha256':hashlib.sha256(manifest.read_bytes()).hexdigest()}))
