import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {AiWorkspace} from './AiWorkspace.js';
const base='/Users/pedrovolu/Documents/ChatGPT/IGAMigrationTool';
const fixture=new Map(JSON.parse(readFileSync(`${base}/work/ui-review/2026-10-03/implemented/preview-snapshot.json`,'utf8')));
const id=fixture.get('/local-demo/v1/runs').runs[0].runId, run=fixture.get(`/local-demo/v1/runs/${id}`),analysis=fixture.get(`/local-demo/v1/runs/${id}/analysis`);
const render=(data,selection=run)=>renderToStaticMarkup(React.createElement(AiWorkspace,{run:selection,analysis:data,preview:React.createElement('div',{'data-preview-retained':'yes'},'retained-preview'),onSettings:()=>{}}));
let checks=0;const check=(truth,label)=>{assert.ok(truth,label);checks++;};
const html=render(analysis);check((html.match(/role="tab"/g)||[]).length===4,'four tabs');check((html.match(/role="tabpanel"/g)||[]).length===4,'four panels');check((html.match(/tabindex="0"/g)||[]).length>=5,'active tab and panel focus');check(html.includes(`${analysis.findings.length} assessment findings`),'real finding count');check(html.includes('disabled=""'),'inactive provider controls');check(html.includes('retained-preview'),'preview remains mounted');check(html.includes('No usage record'),'unknown budget retained');
const availableAi={...analysis,status:'Unavailable',aiPreview:{status:'Ready',runId:run.runId,runRevision:run.revision,baselineId:run.selection.baselineId,profileId:run.selection.profileId,snapshot:{proposals:[{id:'one'},{id:'two'}]}}};
const offline=render(availableAi);check(offline.includes('2 offline AI proposals'),'AI count does not depend on health Ready');check(offline.includes('Admitted assessment findings unavailable'),'health remains unavailable');
for(const patch of [{runId:'other'},{runRevision:run.revision+1}]){const stale=render({...availableAi,...patch});check(stale.includes('Offline AI proposal count unavailable.'),'stale bound count unavailable');check(!stale.includes('2 offline AI proposals'),'stale count absent');}
for(const patch of [{baselineId:'other'},{profileId:'other'},{runId:'other'},{runRevision:run.revision+1}]){const stale=render({...availableAi,aiPreview:{...availableAi.aiPreview,...patch}});check(!stale.includes('2 offline AI proposals'),'mismatched preview absent');}
const hostile=render(analysis,{...run,selection:{...run.selection,scopeLabel:'<script>alert(1)</script>'}});check(hostile.includes('&lt;script&gt;'),'hostile scope escaped');check(!hostile.includes('<script>'),'no hostile executable markup');check(render(null,null).includes('No assessment selected.'),'empty context supported');console.log(JSON.stringify({status:'PASS',checks,review:'Independent source and SSR; no browser actions'},null,2));
