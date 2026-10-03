#!/usr/bin/env python3
"""Independent literal encoder/framer parity. No product imports, DB or provider access."""
from pathlib import Path
import hashlib,json,struct
base=Path(__file__).resolve().parent
def string(s):
 out='"'
 for ch in s:
  n=ord(ch)
  if ch=='\\':out+='\\\\'
  elif ch=='"':out+='\\u0022'
  elif ch in "<>&'+\x60":out+='\\u%04X'%n
  elif ch in '\b\f\n\r\t':out+={'\b':'\\b','\f':'\\f','\n':'\\n','\r':'\\r','\t':'\\t'}[ch]
  elif n<32 or n>126:
   if n<=65535:out+='\\u%04X'%n
   else:n-=65536;out+='\\u%04X\\u%04X'%(55296+(n>>10),56320+(n&1023))
  else:out+=ch
 return out+'"'
def canonical(x):
 if x is None:return 'null'
 if x is True:return 'true'
 if x is False:return 'false'
 if isinstance(x,str):return string(x)
 if isinstance(x,(int,float)):return str(x).removesuffix('.0')
 if isinstance(x,list):return '['+','.join(canonical(v) for v in x)+']'
 return '{'+','.join(string(k)+':'+canonical(x[k]) for k in sorted(x))+'}'
l=json.loads((base/'canonical-literals.json').read_text())
for file,key in [('normal-proof-golden.json','proofDigest'),('normal-readiness-representative-golden.json','readinessDigest')]:
 raw=(base/file).read_text()
 assert canonical(json.loads(raw))==raw
 assert hashlib.sha256(raw.encode()).hexdigest()==l[key]
value=l['aiSchemaValueJson'];assert len(json.loads(value))==17
def frame(s):
 b=s.encode('utf-8',errors='strict');return struct.pack('>I',len(b))+b
data=b'iga.synthetic-evaluation.native-reference.v1\0'
data+=b''.join(frame(s) for s in ['version-binding','synthetic-customer','synthetic-project','synthetic-environment'])
data+=struct.pack('>I',2)+frame('AiSchema')+frame(value)
assert data.hex()==l['referenceFrameHex']
assert 'synthetic-ref-'+hashlib.sha256(data).hexdigest()==l['reference']
assert l['originPaths']==sorted(json.loads(value))
print('PASS independent literal full proof/readiness canonical hashes,17-key schema alias bytes/hash/origins; representative second observation only.')
