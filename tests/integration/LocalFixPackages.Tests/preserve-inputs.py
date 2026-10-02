#!/usr/bin/env python3
"""Freeze original actual dependency source/assets/Release copies before replay."""
import hashlib,json,pathlib,subprocess,sys,re
root=pathlib.Path(__file__).resolve().parents[3]
owned=pathlib.Path(__file__).resolve().parent
execution=owned/'execution'
label=sys.argv[1] if len(sys.argv)>1 else 'first-passing'
assert re.fullmatch(r'[a-z0-9-]+',label)
sourceCopy=execution/('consumed-source-'+label)
paths=subprocess.check_output(['git','ls-files','src','contracts/local-demo','global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config'],cwd=root,text=True).splitlines()
paths=[root/p for p in paths if (root/p).is_file()]
paths.extend(p for p in owned.glob('*') if p.is_file() and p.suffix in ('.cs','.py','.json','.md'))
paths.extend(p for p in (root/'tests/e2e/local-fix-packages').glob('*.mjs'))
for base in [root/'src/web/dist',root/'src/server/hosts/LocalConsultantDemo/bin/Release/net10.0',owned/'bin/Release/net10.0']:
 if base.exists():paths.extend(p for p in base.rglob('*') if p.is_file())
def hash(path):return hashlib.sha256(path.read_bytes()).hexdigest()
records=[]
for original in sorted(set(paths)):
 relative=original.relative_to(root)
 copy=sourceCopy/relative
 copy=copy.with_name(copy.name+'.frozen')
 copy.parent.mkdir(parents=True,exist_ok=True)
 data=original.read_bytes()
 if copy.exists():assert copy.read_bytes()==data,'original input changed; preserve separately, never overwrite'
 else:copy.write_bytes(data)
 assert hash(original)==hash(copy)
 records.append({'original':str(original),'preserved':str(copy),'sha256':hash(copy),'bytes':len(data)})
(execution/('consumed-inputs-'+label+'.json')).write_text(json.dumps({'schema':'v12-original-consumed-inputs-v1','dependencyHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'copies':records},indent=2)+'\n')
print('PASS original consumed copies='+str(len(records)))
