"""Independent full markup oracle from the B9 fixed presentation blueprint.
Reads independently authored V9 preview primitives; never loads application output.
"""
import hashlib
import html
import json
from pathlib import Path
ROOT = Path(__file__).parent
p = json.loads((ROOT/'preview-golden.json').read_text())
digest = hashlib.sha256((ROOT/'preview-golden.json').read_bytes()).hexdigest()
CSS = 'html{color:#172b4d;background:#fff;font-family:system-ui,sans-serif;line-height:1.6}body{margin:0;padding:1rem}main{max-width:70rem;margin:auto}h1,h2,h3,h4,p,dt,dd,li{overflow-wrap:anywhere}h1{font-size:2rem}h2{font-size:1.5rem}h3{font-size:1.2rem}h4{font-size:1rem}section,article{margin-block:1.5rem}article{border-top:2px solid #526174;padding-top:1rem}dl{display:grid;grid-template-columns:minmax(0,12rem) minmax(0,1fr);gap:.5rem 1rem}dt{font-weight:700}dd{margin:0;min-width:0}ol,ul{padding-left:1.5rem}li{margin-block:.5rem}.disclaimer{border:2px solid #526174;padding:1rem;font-weight:600}a{color:#0645ad}a:focus-visible,[tabindex]:focus{outline:3px solid #814600;outline-offset:3px}.skip{position:absolute;left:1rem;top:-8rem;background:#fff;padding:.5rem}.skip:focus{top:1rem}nav{display:flex;flex-wrap:wrap;gap:1rem}@media(max-width:30rem){dl{grid-template-columns:minmax(0,1fr)}dd{margin-bottom:.5rem}h1{font-size:1.6rem}}'
# Benign independent oracle strings are ASCII and do not contain apostrophes.
def text(value): return html.escape(value,quote=True)
lines=['<!doctype html>','<html lang="en">','<head>','<meta charset="utf-8">','<meta name="viewport" content="width=device-width, initial-scale=1">', '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'; base-uri \'none\'; form-action \'none\'">','<title>Synthetic AI proposal preview</title>','<style>'+CSS+'</style>','</head>','<body>','<a class="skip" href="#preview-content">Skip to preview content</a>','<main id="preview-content" tabindex="-1">','<h1>Synthetic AI proposal preview</h1>','<p class="disclaimer">'+text(p['disclaimer'])+'</p>','<p>Status: Proposed</p>','<nav aria-label="Preview sections"><a href="#source">Source</a><a href="#proposals">Proposals</a></nav>','<section id="source">','<h2>Source</h2>','<dl>']
for label,key in [('Customer','customerId'),('Project','projectId'),('Environment','environmentId'),('Run ID','runId'),('Baseline digest','baselineDigest'),('Profile digest','profileDigest'),('Normalization version','normalizationVersion'),('Redaction version','redactionVersion'),('Prompt version','promptVersion')]: lines.append('<dt>'+label+'</dt><dd>'+text(p['source'][key])+'</dd>')
for label,value in [('Packet digest',p['packetDigest']),('Proposal digest',p['proposalDigest']),('Preview digest',digest)]: lines.append('<dt>'+label+'</dt><dd>'+text(value)+'</dd>')
lines += ['</dl>','</section>','<section id="proposals">','<h2>Proposals</h2>']
def items(values, empty):
    return ['<ul>']+['<li>'+text(v)+'</li>' for v in values]+['</ul>'] if values else ['<p>'+empty+'</p>']
for proposal in p['proposals']:
    lines += ['<article>','<h3>Proposal '+text(proposal['proposalId'])+'</h3>','<p>Proposed and untrusted. Cited statements are not verified facts.</p>']
    for label,key in [('Facts','facts'),('Inferences','inferences'),('Assumptions','assumptions'),('Suggestions','suggestions')]:
        lines += ['<section>','<h4>'+label+'</h4>']
        if not proposal[key]: lines += ['<p>No statements were supplied in this category.</p>']
        else:
            lines += ['<ol>']
            for statement in proposal[key]:
                lines += ['<li>','<p>'+text(statement['text'])+'</p>','<p>Evidence IDs</p>']+items(statement['evidenceIds'],'No evidence IDs were supplied.')+['<p>Rule IDs</p>']+items(statement['ruleIds'],'No rule IDs were supplied.')+['</li>']
            lines += ['</ol>']
        lines += ['</section>']
    lines += ['<section>','<h4>Missing context</h4>']+items(proposal['missingContext'],'No missing context was declared.')+['</section>','<section>','<h4>Uncertainty</h4>','<p>'+text(proposal['uncertainty'] or 'No uncertainty was declared.')+'</p>','</section>','<section>','<h4>Conflicting evidence IDs</h4>']+items(proposal['conflictingEvidenceIds'],'No conflicting evidence was declared.')+['</section>','</article>']
lines += ['</section>','</main>','</body>','</html>']
result='\n'.join(lines)
(ROOT/'preview-golden.html').write_text(result,encoding='utf8')
metadata={'authority':'Independent Python standard-library HTML serializer of V9 primitive golden; B9 explicit fixed markup blueprint only, no application execution/output','previewDigest':digest,'htmlDigest':hashlib.sha256(result.encode()).hexdigest(),'htmlBytes':len(result.encode())}
(ROOT/'html-golden-provenance.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('PASS V9 independently authored full HTML oracle; bytes='+str(metadata['htmlBytes'])+' sha256='+metadata['htmlDigest'])
