"""Independent primitive fixture and HTML byte oracle; never calls or reads renderer."""
import hashlib
import json
from pathlib import Path

BASE = Path(__file__).parent
DISCLAIMER = 'Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.'

def json_string(value):
    result = '"'
    for char in value:
        code = ord(char)
        if char == '\\': result += '\\\\'
        elif char in '\b\f\n\r\t': result += {'\b': '\\b', '\f': '\\f', '\n': '\\n', '\r': '\\r', '\t': '\\t'}[char]
        elif code < 32 or code >= 127 or char in '<>&\'"+`':
            result += ''.join('\\u%04X' % unit for unit in ([code] if code <= 65535 else [0xD800 + ((code - 65536) >> 10), 0xDC00 + ((code - 65536) & 1023)]))
        else: result += char
    return result + '"'

def canonical(value):
    if isinstance(value, str): return json_string(value)
    if isinstance(value, dict): return '{' + ','.join(json_string(key) + ':' + canonical(value[key]) for key in sorted(value)) + '}'
    if isinstance(value, list): return '[' + ','.join(map(canonical, value)) + ']'
    raise ValueError('Only fixture strings, objects and arrays are used')

def encoded(value):
    result = ''
    for char in value:
        if char in '&<>"\'': result += {'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[char]
        elif 160 <= ord(char) <= 255 or ord(char) > 65535: result += '&#%d;' % ord(char)
        else: result += char
    return result

STYLE = 'html{color:#172b4d;background:#fff;font-family:system-ui,sans-serif;line-height:1.6}body{margin:0;padding:1rem}main{max-width:70rem;margin:auto}h1,h2,h3,h4,p,dt,dd,li{overflow-wrap:anywhere}h1{font-size:2rem}h2{font-size:1.5rem}h3{font-size:1.2rem}h4{font-size:1rem}section,article{margin-block:1.5rem}article{border-top:2px solid #526174;padding-top:1rem}dl{display:grid;grid-template-columns:minmax(0,12rem) minmax(0,1fr);gap:.5rem 1rem}dt{font-weight:700}dd{margin:0;min-width:0}ol,ul{padding-left:1.5rem}li{margin-block:.5rem}.disclaimer{border:2px solid #526174;padding:1rem;font-weight:600}a{color:#0645ad}a:focus-visible,[tabindex]:focus{outline:3px solid #814600;outline-offset:3px}.skip{position:absolute;left:1rem;top:-8rem;background:#fff;padding:.5rem}.skip:focus{top:1rem}nav{display:flex;flex-wrap:wrap;gap:1rem}@media(max-width:30rem){dl{grid-template-columns:minmax(0,1fr)}dd{margin-bottom:.5rem}h1{font-size:1.6rem}}'

def list_html(values, empty):
    return '<ul>\n' + ''.join('<li>' + encoded(value) + '</li>\n' for value in values) + '</ul>\n' if values else '<p>' + empty + '</p>\n'

def render(value):
    preview_digest = hashlib.sha256(canonical(value).encode()).hexdigest()
    lines = ['<!doctype html>', '<html lang="en">', '<head>', '<meta charset="utf-8">', '<meta name="viewport" content="width=device-width, initial-scale=1">', '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'; base-uri \'none\'; form-action \'none\'">', '<title>Synthetic AI proposal preview</title>', '<style>' + STYLE + '</style>', '</head>', '<body>', '<a class="skip" href="#preview-content">Skip to preview content</a>', '<main id="preview-content" tabindex="-1">', '<h1>Synthetic AI proposal preview</h1>', '<p class="disclaimer">' + encoded(DISCLAIMER) + '</p>', '<p>Status: Proposed</p>', '<nav aria-label="Preview sections"><a href="#source">Source</a><a href="#proposals">Proposals</a></nav>', '<section id="source">', '<h2>Source</h2>', '<dl>']
    source = value['source']
    rows = [('Customer',source['customerId']),('Project',source['projectId']),('Environment',source['environmentId']),('Run ID',source['runId']),('Baseline digest',source['baselineDigest']),('Profile digest',source['profileDigest']),('Normalization version',source['normalizationVersion']),('Redaction version',source['redactionVersion']),('Prompt version',source['promptVersion']),('Packet digest',value['packetDigest']),('Proposal digest',value['proposalDigest']),('Preview digest',preview_digest)]
    lines += ['<dt>' + label + '</dt><dd>' + encoded(text) + '</dd>' for label,text in rows]
    output = '\n'.join(lines) + '\n</dl>\n</section>\n<section id="proposals">\n<h2>Proposals</h2>\n'
    if not value['proposals']: output += '<p>No proposals were returned. This does not establish healthy or complete assessment coverage.</p>\n'
    for proposal in value['proposals']:
        output += '<article>\n<h3>Proposal ' + encoded(proposal['proposalId']) + '</h3>\n<p>Proposed and untrusted. Cited statements are not verified facts.</p>\n'
        for key,label in [('facts','Facts'),('inferences','Inferences'),('assumptions','Assumptions'),('suggestions','Suggestions')]:
            output += '<section>\n<h4>' + label + '</h4>\n'
            if not proposal[key]: output += '<p>No statements were supplied in this category.</p>\n'
            else:
                output += '<ol>\n'
                for statement in proposal[key]:
                    output += '<li>\n<p>' + encoded(statement['text']) + '</p>\n<p>Evidence IDs</p>\n'
                    output += list_html(statement['evidenceIds'], 'No evidence IDs were supplied.') + '<p>Rule IDs</p>\n'
                    output += list_html(statement['ruleIds'], 'No rule IDs were supplied.') + '</li>\n'
                output += '</ol>\n'
            output += '</section>\n'
        output += '<section>\n<h4>Missing context</h4>\n' + list_html(proposal['missingContext'], 'No missing context was declared.')
        output += '</section>\n<section>\n<h4>Uncertainty</h4>\n<p>' + encoded(proposal['uncertainty'] or 'No uncertainty was declared.') + '</p>\n</section>\n<section>\n<h4>Conflicting evidence IDs</h4>\n'
        output += list_html(proposal['conflictingEvidenceIds'], 'No conflicting evidence was declared.') + '</section>\n</article>\n'
    return output + '</section>\n</main>\n</body>\n</html>'

E1,E2 = 'ev-' + '1'*64, 'ev-' + '2'*64
R1,R2 = 'fixture-rule-conflict-v1', 'fixture-rule-schedule-v1'
def statement(text): return {'text':text,'evidenceIds':[E1,E2],'ruleIds':[R1,R2]}
fixture = {'schemaVersion':'synthetic-ai-preview-v1','status':'Proposed','disclaimer':DISCLAIMER,'source':{'customerId':'synthetic-customer','projectId':'synthetic-project','environmentId':'synthetic-environment','runId':'12345678-1234-5678-9abc-123456789abc','baselineDigest':'a'*64,'profileDigest':'b'*64,'normalizationVersion':'fixture-normalization-v1','redactionVersion':'fixture-redaction-v1','promptVersion':'fixture-prompt-v1'},'packetDigest':'c'*64,'proposalDigest':'d'*64,'proposals':[{'proposalId':'proposal-01','facts':[statement('<script>alert(\'x\' & "y")</script>'),statement('Unicode café 中文 😀')],'inferences':[statement('<img src=x onerror=alert(1)>')],'assumptions':[statement('A < B & B > C')],'suggestions':[statement('javascript:alert(1) remains text')],'missingContext':['First context <iframe src="https://example.invalid">','Second context & details'],'uncertainty':'Uncertain <style>body{display:none}</style>','conflictingEvidenceIds':[E1,E2]}]}

if __name__ == '__main__':
    expected = {'golden-preview.json':canonical(fixture),'golden-preview.html':render(fixture)}
    expected['golden-preview.sha256'] = hashlib.sha256(expected['golden-preview.html'].encode()).hexdigest()
    import sys
    for name,content in expected.items():
        if '--verify' in sys.argv:
            assert (BASE / name).read_bytes() == content.encode(), name
        else: (BASE / name).write_bytes(content.encode())
    print('B9 independent Python oracle: 3 exact UTF-8 artifacts verified' if '--verify' in sys.argv else 'B9 independent Python oracle: 3 artifacts authored')
