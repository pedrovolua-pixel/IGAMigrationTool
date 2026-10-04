import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const web='/Users/pedrovolu/Documents/ChatGPT/IGAMigrationTool/src/web';
const require=createRequire(`${web}/package.json`);
const React=require('react');const {renderToStaticMarkup}=require('react-dom/server');
const {build}=await import(`${web}/node_modules/vite/dist/node/index.js`);
const result=await build({root:web,configFile:false,logLevel:'silent',build:{ssr:`${web}/src/PlatformOverview.tsx`,write:false,minify:false,rollupOptions:{external:['react','react/jsx-runtime']}}});
const compiled=result.output.find(chunk=>chunk.type==='chunk').code.replace(/from "react\/jsx-runtime"/g,`from '${web}/node_modules/react/jsx-runtime.js'`).replace(/from "react"/g,`from '${web}/node_modules/react/index.js'`);
await fs.writeFile('/private/tmp/iga-ui-w5-overview-review-component.mjs',compiled);
const {PlatformOverview}=await import('/private/tmp/iga-ui-w5-overview-review-component.mjs');
let assertions=0;function ok(condition,message){assert.ok(condition,message);assertions++;}
const score=value=>({raw:String(value),display:String(value),status:'Green',eligibleUnits:1});
const categories=['security','operations','correctness'].map((id,i)=>({id,provisional:score(60+i*10),publishableCurrent:score(55+i*10)}));
const findings=[{id:'a',category:'security',severity:'Critical',state:'Proposed'},{id:'b',category:'security',severity:'High',state:'Proposed'},{id:'c',category:'operations',severity:'Informational',state:'Confirmed'}];
const run={runId:'run1',revision:3,selection:{scopeLabel:'<script>hostile</script>'},executableCoverage:{hasApplicableUnits:true,numerator:7,denominator:9},progress:{plannedUnits:12}};
const analysis={runId:'run1',runRevision:3,status:'Ready',provisional:score(83.4),publishableCurrent:score(77.2),categories,findings,review:{status:'Ready',findings:[{id:'a',state:'Confirmed'}]},quality:{plannedUnits:12,executedUnits:7,gapUnits:2,notApplicableUnits:3,proposedReviewUnits:1,totalFindingUnits:3},maturity:null};
function render(run,analysis){return renderToStaticMarkup(React.createElement(PlatformOverview,{run,analysis,onNavigate:()=>{throw Error('render executed navigation');},onInspectCategory:()=>{throw Error('render executed filter');}}));}
const current=render(run,analysis);
for(const heading of ['Overall health','Health by category','Health over time','Category hotspots','Review focus'])ok(current.includes(`>${heading}</h3>`),heading+' approved panel exists');
ok(current.includes('83.4'),'actual score');ok(!current.includes('>72</strong>'),'no mock score');ok(current.includes('1 proposed findings await review · 1 are Critical or High.'),'current review overrides original finding state');
ok(current.includes('7 / 9 executed applicable units'),'actual separate coverage');ok(current.includes('Historical health is unavailable.'),'no fabricated historical series');
ok(current.includes('platform-overview-radar-value'),'all available categories render polygon');ok(current.includes('Category score table'),'accessible equivalent');ok(!current.includes('<script>'),'hostile scope is inert');ok(!current.includes('NaN')&&!current.includes('Infinity'),'finite geometry');
for(const [label,modified] of [['other ID',{...analysis,runId:'other'}],['other revision',{...analysis,runRevision:4}],['unavailable',{...analysis,status:'Unavailable'}]]){const html=render(run,modified);ok(!html.includes('83.4'),label+' score suppressed');ok(html.includes('Health unavailable'),label+' unavailability explicit');ok(html.includes('Verified review results are unavailable.'),label+' review suppressed');}
for(const raw of ['NaN','Infinity','-1','101','']){const html=render(run,{...analysis,provisional:{...analysis.provisional,raw}});ok(html.includes('Score unavailable'),'invalid score '+raw);ok(!html.includes('stroke-dasharray="NaN'),'invalid score finite geometry '+raw);}
for(const altered of [categories.slice(0,2),categories.map((category,i)=>i===0?{...category,provisional:{raw:null,display:null,status:'Unavailable',eligibleUnits:0}}:category)]){const html=render(run,{...analysis,categories:altered});ok(!html.includes('platform-overview-radar-value'),'incomplete category fallback avoids misleading polygon');ok(html.includes('platform-overview-category-chart'),'incomplete category retains truthful chart');}
const empty=render(null,null);ok(empty.includes('Open or start a synthetic assessment'),'empty current state');ok(!empty.includes('83.4'),'empty has no score');
const css=await fs.readFile(`${web}/src/PlatformOverview.css`,'utf8');ok(css.includes('grid-template-columns: repeat(2, minmax(0, 1fr))'),'equal overview columns');ok(css.includes('grid-column: 1 / -1'),'full width review strip');ok(css.includes('@media (prefers-reduced-motion: reduce)'),'reduced motion');
console.log(JSON.stringify({result:'pass',assertions,review:'nonauthor independent Overview source and rendering',limits:'browser/mobile contrast and navigation execution remain coordinator checks'},null,2));
