"""Build a standalone platform mockup from the preserved original prototype."""
from pathlib import Path
import re
import shutil

HERE = Path(__file__).resolve().parent
source = HERE.parent / 'recovered-graphical-prototype/risk-drilldown-source.html'
html = source.read_text()
groups = [
    ('Workspace', [('home','◈','Overview'),('assessments','▤','Assessments'),('risk','◴','Risk analysis'),('evidence','⌘','Evidence'),('outcomes','◎','Outcomes & maturity'),('ai','✧','AI workspace'),('fixes','↗','Recommendations'),('tasks','☷','Tasks & reviews'),('reports','▧','Reports'),('compare','⇄','Compare runs')]),
    ('Manage', [('projects','▦','Projects'),('sources','◫','Sources & baselines'),('rules','≡','Rule catalog'),('settings','⚙','Settings'),('audit','◷','Audit history')]),
    ('Explore', [('migration','⇢','Migration'),('portfolio','▥','Portfolio'),('gallery','▣','Design archive')]),
]
nav = '<nav class="navigation" aria-label="Product areas">'
for label, entries in groups:
    nav += f'<div class="nav-label">{label}</div>'
    for key, glyph, name in entries:
        nav += f'<button type="button" data-page="{key}"><span class="nav-glyph" aria-hidden="true">{glyph}</span>{name}</button>'
nav += '</nav>'
html = re.sub(r'<nav class="navigation".*?</nav>', nav, html, flags=re.S)
html = html.replace('Assessment studio</span></span></div>', 'Assessment studio</span></span><button type="button" class="menu-close" data-menu="true" aria-label="Close navigation">✕</button></div>')
html = html.replace('One environment in view<br><br>Read-only design review', 'Production · sample workspace<br><br>Interactive design review<br>No live operations')
html = html.replace('<header class="topbar">', '<header class="topbar"><button type="button" class="action quiet menu-toggle" data-menu="true" aria-label="Toggle navigation">☰</button>')
html = html.replace('<div class="crumb">Project / Sample organization</div>', '<div class="crumb project-crumb">Sample organization / Production</div>')
html = html.replace('<span class="small muted">02 Oct 2026</span>', '<span class="small muted date">02 Oct 2026</span>')
html = html.replace('<span class="pill">Sample data</span></div></header>', '<span class="pill">Sample data</span><button type="button" class="action quiet notif" data-action="notifications" aria-label="Open notifications"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/></svg></button></div></header>')
html = re.sub(r'<section id="iga-mock-settings".*?</section>', '', html, flags=re.S)
html = html.replace("page.innerHTML=state.page==='home'?home():state.page==='risk'?risk():evidence();", "page.innerHTML=extraPages[state.page]?extraPages[state.page]():state.page==='home'?home():state.page==='risk'?risk():evidence();page.querySelectorAll('button:not([type])').forEach(b=>b.type='button');")
html = html.replace('function evidence(){','function relationshipWorkspace(){')
html = html.replace("function save(){state.revision++;", "function save(){if(location.hash!=='#'+state.page)history.pushState(null,'','#'+state.page);state.revision++;")
html = html.replace("apply(window.openai?.widgetState);", (HERE / 'platform.js').read_text() + "\nif(names[location.hash.slice(1)])state.page=location.hash.slice(1);else if(mock.settings['default-area'])state.page=Object.keys(names).find(k=>names[k]===mock.settings['default-area'])||'home';\napply(window.openai?.widgetState);")
html = html.replace('<div class="shell">', '<link rel="stylesheet" href="platform.css"><div class="shell">', 1)
html = html.replace('<script>', '<script>\n"use strict";\n')
document = '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>IGA workspace · complete platform mockup</title><style>@media print{.side,.topbar,.footer,.toolbar,.tabs,.pagehead,.savebar{display:none!important}.shell{display:block!important}.content{padding:0!important}.report-paper{border:0!important;box-shadow:none!important}body{background:white}}</style></head><body>'
document += '<dialog id="mock-dialog" class="mock-dialog" aria-label="Sample flow details"></dialog><div id="mock-toast" class="toast" role="status" aria-live="polite"></div>' + html + '</body></html>'
(HERE / 'index.html').write_text(document)
shots = HERE / 'screenshots'
shots.mkdir(exist_ok=True)
for filename in ['overview.png','risk-analysis.png','relationships-overlay-desktop.png','overlay-no-blur-desktop.png','relationships-overlay-mobile.png','drilldown-evidence.png','evidence.png','drilldown-desktop.png','drilldown-mobile.png','overlay-no-blur-mobile.png']:
    shutil.copy2(HERE.parents[1] / 'visual-concepts' / filename, shots / filename)
print(f'Built {HERE / "index.html"}; preserved original prototype untouched.')
