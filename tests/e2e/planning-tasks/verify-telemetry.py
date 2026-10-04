#!/usr/bin/env python3
"""Inspect original owned telemetry and regenerate literal/serialized-payload absence proof."""
import hashlib,json,pathlib,re
root=pathlib.Path(__file__).resolve().parent
observed=root/'.host/observed/final-saved-task-source.json';log=root/'.host/host-runtime.log'
data=json.loads(observed.read_text());text=log.read_text()
reasons={entry['command']['reason'] for entry in data['taskPosts']}
reasons.update(event['reason'] for entry in data['finalAnalysis']['planningTasks']['entries'] for event in entry['history'])
artifacts={artifact['text'] for package in data['finalAnalysis']['fixPackages']['snapshot']['packages'] for option in package['options'] for artifact in option['artifacts']}
def default_web_string(value):
    # Independent V14 oracle's explicitly authored System.Text.Json default encoder recipe.
    escapes={'\\':'\\\\','\b':'\\b','\f':'\\f','\n':'\\n','\r':'\\r','\t':'\\t'}
    parts=[]
    for char in value:
        if char in escapes:parts.append(escapes[char])
        elif ord(char)<32 or ord(char)>126 or char in '\"\'&<>+`':
            encoded=char.encode('utf-16-be')
            parts.extend('\\u'+encoded[i:i+2].hex().upper() for i in range(0,len(encoded),2))
        else:parts.append(char)
    return ''.join(parts)
def variants(value):
    result={value,json.dumps(value,ensure_ascii=True)[1:-1],json.dumps(value,ensure_ascii=False)[1:-1],default_web_string(value)}
    result.update(re.sub(r'\\u[0-9a-fA-F]{4}',lambda m:m.group().lower(),v) for v in list(result))
    result.update(v.replace('/','\\/') for v in list(result))
    return result
reason_variants={v for value in reasons for v in variants(value)}
artifact_variants={v for value in artifacts for v in variants(value)}
assert all(v and v not in text for v in reason_variants),'literal_or_serialized_planning_reason_in_telemetry'
assert all(v and v not in text for v in artifact_variants),'literal_or_serialized_artifact_text_in_telemetry'
output=root/'telemetry.json';output.write_text(json.dumps({'schema':'v14-payload-free-telemetry-proof-v2','result':'PASS','reasonCount':len(reasons),'artifactTextCount':len(artifacts),'reasonVariantCount':len(reason_variants),'artifactVariantCount':len(artifact_variants),'encodings':['literal','JSON ensure_ascii True','JSON ensure_ascii False','independent System.Text.Json default web encoding','Unicode escape hex case variants','escaped slash variants'],'reasonPayloadOccurrences':0,'artifactPayloadOccurrences':0,'bindings':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size} for p in (observed,log)]},indent=2)+'\n')
print('PASS V14 original telemetry literal/serialized absence; reasons='+str(len(reasons))+'; artifacts='+str(len(artifacts))+'; variants='+str(len(reason_variants)+len(artifact_variants)))
