import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, symlink, rm } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Dedicated isolated component harness. It never contacts the application or database.
const source = dirname(fileURLToPath(import.meta.url));
const web = resolve(source, '..');
const browserDependencies =
  process.env.IGA_BROWSER_DEPENDENCIES ??
  resolve(web, '../../tests/e2e/consultant-demo/node_modules');
const { createServer } = await import(
  pathToFileURL(resolve(web, 'node_modules/vite/dist/node/index.js'))
);
const { chromium } = await import(
  pathToFileURL(resolve(browserDependencies, 'playwright/index.mjs'))
);
const temporary = await mkdtemp(resolve(tmpdir(), 'iga-phase1b-components-'));
let server, browser;
let checks = 0;
const check = (condition, message) => {
  assert.ok(condition, message);
  checks++;
  console.log(`PASS ${message}`);
};
try {
  await symlink(resolve(web, 'node_modules'), resolve(temporary, 'node_modules'));
  await writeFile(
    resolve(temporary, 'index.html'),
    '<!doctype html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Phase 1B isolated components</title></head><body><main id="root"></main><script type="module" src="/fixture.tsx"></script></body></html>',
  );
  await writeFile(
    resolve(temporary, 'fixture.tsx'),
    `
import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Phase1BSetup, phase1BValidText } from '/@fs/${source}/Phase1BSetup.tsx';
import { Phase1BWorkspace } from '/@fs/${source}/Phase1BWorkspace.tsx';
import '/@fs/${source}/styles.css';
const digest = 'a'.repeat(64), approval = '10000000-0000-4000-8000-000000000001';
const approved = { outcomeId:'access-governance',version:1,categoryId:'SECURITY',title:'Approved fictional goal',behavior:'<img src=x onerror="window.injection=true">',origin:'Documented',unitLinks:[{inventoryId:'det-guard',evidenceCategory:'SYN-GUARD',categoryId:'SECURITY'}],referenceIds:[],assumptions:[],predecessorVersion:null,contentDigest:digest,state:'CustomerApproved',revision:3,reviewEventId:'10000000-0000-4000-8000-000000000002',approvalEventId:approval,history:[{eventId:approval,kind:'Approve',actorId:'fictional-customer',recordedAtUtc:'2026-10-03T12:00:00Z',reason:'<script>window.injection=true</script>'}] };
const draft = {...approved,outcomeId:'pending-goal',title:'Draft fictional goal',state:'Draft',revision:1,reviewEventId:null,approvalEventId:null,history:[]};
const reviewed={...approved,outcomeId:'reviewed-goal',title:'Reviewed fictional goal',state:'ConsultantReviewed',revision:2,approvalEventId:null,history:[]};
const planning = {optionId:'option-a',findingId:'finding-a',categoryId:'OPERATIONS',revision:2,originalContext:{guidanceDigest:digest,assumptions:['Fictional internal exposure'],prerequisites:['Fixture prerequisite'],sourceReferences:['guidance-option-a'],objectiveMapVersion:'synthetic-objectives-v1',objectiveMapDigest:'c'.repeat(64),objectives:[{objectiveId:'access-governance',weight:1}],matchedObjectiveIds:['access-governance']},originalPriority:{policyVersion:'synthetic-priority-policy-v1',rawPriority:68,displayPriority:68,originalBand:'High',missingInputs:[],contributions:[{factor:'severity',normalized:.64,weight:.3,points:19.2}]},effectivePriority:'High',originalEffort:{size:'M',minimumPersonHours:8,maximumPersonHours:24},effectiveEffort:{size:'M',minimumPersonHours:8,maximumPersonHours:24},effortApproval:'Proposed',hasPriorityOverride:false,hasEffortOverride:false,history:[]};
const unavailable={...planning,optionId:'option-missing',findingId:'finding-missing',originalContext:undefined,originalPriority:{policyVersion:'synthetic-priority-policy-v1',rawPriority:null,displayPriority:null,originalBand:null,missingInputs:['exposure','effort'],contributions:[]},effectivePriority:null,originalEffort:null,effectiveEffort:null};
window.probe={commands:[],mode:'uncertain',aborted:0,injection:false,validText:phase1BValidText};
function Harness(){
 const [run,setRun]=useState('run-a'),[actor,setActor]=useState('Consultant'),[registry,setRegistry]=useState(6),[denied,setDenied]=useState(false),[terminal,setTerminal]=useState(false),[sourceDigest,setSourceDigest]=useState(digest);
 window.probe.changeRun=()=>setRun('run-b'); window.probe.changeActor=()=>setActor('FictionalCustomer'); window.probe.changeRegistry=()=>setRegistry(7); window.probe.deny=()=>setDenied(true); window.probe.terminal=()=>setTerminal(true); window.probe.changeSource=()=>setSourceDigest('b'.repeat(64));
 const perform=(kind,context)=>async(command,signal)=>{window.probe.commands.push({kind,command:JSON.parse(JSON.stringify(command)),context});
  if(window.probe.mode==='delay')return await new Promise((resolve,reject)=>{signal.addEventListener('abort',()=>{window.probe.aborted++;reject(Error('abort'));});window.probe.late=()=>resolve({status:'committed',eventId:command.eventId,contextKey:context,message:'LATE OLD RUN'});});
  const status=window.probe.mode==='uncertain'?'uncertain':'committed';return {status,eventId:window.probe.mode==='mismatch'?'wrong-event':command.eventId,contextKey:context};};
 const workspaceContext='workspace/'+actor+'/'+run;
 return <><h1>Phase 1B component verification</h1><Phase1BSetup contextKey={'setup/'+actor} actorLabel={actor} registryRevision={registry} entries={[approved,draft,reviewed]} availableCoverageKeys={[{inventoryId:'det-guard',evidenceCategory:'SYN-GUARD',categoryId:'SECURITY'},{inventoryId:'det-guard',evidenceCategory:'SYN-GUARD',categoryId:'SECURITY'},{inventoryId:'det-guard',evidenceCategory:'SYN-OTHER',categoryId:'SECURITY'},{inventoryId:'synthetic-ai-retry',evidenceCategory:'configuration',categoryId:'OPERATIONS'}]} canManage={!denied&&actor==='Consultant'} canApprove={!denied&&actor==='FictionalCustomer'} canStart={!denied} onOutcomeCommand={perform('outcome','setup/'+actor)} onStartRun={perform('start','setup/'+actor)}/>
 <Phase1BWorkspace runId={run} contextKey={'workspace/'+actor} actorLabel={actor} lockedOutcomes={[{outcomeId:'access-governance',version:1,contentDigest:digest,revision:3,approvalEventId:approval}]} outcomeLockDigest={digest} sourceDigest={sourceDigest} entries={[planning,unavailable]} canPlan={!denied&&actor==='Consultant'} canOverrideBudget={!denied&&actor==='Consultant'} budget={{revision:1,runAllowance:600,categoryAllowances:{SECURITY:600,OPERATIONS:600},counters:[{key:'run',charged:480,held:0,allowance:600}],history:[]}} works={[{workId:'ai-retry',category:'OPERATIONS',state:terminal?'Failed':'Pending',reasonCodes:[],attempts:[{attemptId:'20000000-0000-4000-8000-000000000001',ordinal:1,state:'Unknown',held:true,inputUnits:null,outputUnits:null,receiptId:null}]}]} onPlanningCommand={perform('planning',workspaceContext)} onBudgetOverride={perform('budget',workspaceContext)}/></>;
}
createRoot(document.getElementById('root')).render(<Harness/>);
`,
  );
  server = await createServer({
    configFile: false,
    root: temporary,
    server: {
      host: '127.0.0.1',
      port: 0,
      fs: { allow: [temporary, source, resolve(web, 'node_modules')] },
    },
  });
  await server.listen();
  browser = await chromium.launch({
    headless: true,
    ...(process.env.IGA_CHROMIUM ? { executablePath: process.env.IGA_CHROMIUM } : {}),
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const address = server.httpServer.address();
  await page.goto(`http://127.0.0.1:${address.port}`);
  await page.getByRole('heading', { name: 'Phase 1B outcome setup' }).waitFor();
  await page.getByText('Original estimate context and provenance', { exact: true }).first().click();
  check(
    (await page.getByText('Prerequisites: Fixture prerequisite', { exact: true }).count()) === 1 &&
      (await page
        .getByText('Fictional objective map: synthetic-objectives-v1.', { exact: true })
        .count()) === 1 &&
      (await page
        .getByText('Exact option matches: access-governance.', { exact: true })
        .count()) === 1,
    'bound original assumptions prerequisites source references and objective proof render read-only',
  );
  await page.getByText('Six-factor calculation', { exact: true }).first().click();
  check(
    (await page.getByText('Unrounded priority: 68.', { exact: true }).count()) === 1,
    'original unrounded priority and policy remain visible',
  );
  check(
    await page.evaluate(
      () =>
        !window.probe.validText(' ') &&
        !window.probe.validText('x'.repeat(2001)) &&
        !window.probe.validText(String.fromCharCode(0xd800)) &&
        !window.probe.validText(String.fromCharCode(0)) &&
        window.probe.validText('Fictional ✓'),
    ),
    'text validation rejects blank oversized NUL and malformed Unicode',
  );
  check(
    (await page.getByText('Actor simulation:', { exact: false }).count()) === 2,
    'fictional actor simulation is visible in setup and workspace',
  );
  check(
    (await page.locator('img, script:not([type="module"])').count()) === 0,
    'model and outcome markup renders as inert text',
  );
  check(
    (await page.getByRole('checkbox', { name: /access-governance v1/ }).isChecked()) === false,
    'approved versions are never silently selected',
  );
  await page.getByRole('checkbox', { name: /access-governance v1/ }).check();
  const startButton = page.getByRole('button', {
    name: 'Start combined Phase 1B run with selected versions',
  });
  await startButton.hover();
  check(
    await startButton.evaluate((button) => {
      const style = getComputedStyle(button);
      return style.backgroundColor === 'rgb(18, 57, 85)' && style.color === 'rgb(255, 255, 255)';
    }),
    'enabled hovered button preserves high contrast with the real global application stylesheet',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page
    .getByRole('button', { name: 'Start combined Phase 1B run with selected versions' })
    .click();
  const start = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    start.selections.length === 1 &&
      start.selections[0].contentDigest === 'a'.repeat(64) &&
      start.selections[0].revision === 3 &&
      start.selections[0].version === 1 &&
      start.selections[0].approvalEventId.endsWith('0001'),
    'new run sends exact selected version digest revision and approval proof',
  );
  await page.evaluate(() => window.probe.changeRegistry());
  await page.getByRole('heading', { name: 'Registry · revision 7' }).waitFor();
  check(
    await page
      .getByRole('button', { name: 'Start combined Phase 1B run with selected versions' })
      .isDisabled(),
    'registry change invalidates old selection revision',
  );
  await page.getByRole('button', { name: 'Clear version selections' }).click();
  check(
    (await page.getByRole('checkbox', { name: /access-governance v1/ }).isChecked()) === false,
    'stale selection can be explicitly cleared',
  );
  await page
    .getByLabel('Reason for the next lifecycle action')
    .fill('Fictional Consultant review reason');
  await page.evaluate(() => {
    window.probe.mode = 'uncertain';
  });
  await page.getByRole('button', { name: 'Consultant review pending-goal v1' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  const first = await page.evaluate(() => window.probe.commands.at(-1));
  check(
    first.command.expectedReviewEventId === null &&
      first.command.expectedRevision === 1 &&
      first.command.expectedRegistryRevision === 7 &&
      first.command.expectedContentDigest === 'a'.repeat(64),
    'lifecycle command binds exact content and both revisions',
  );
  check(
    await page.getByLabel('Reason for the next lifecycle action').isDisabled(),
    'uncertain event locks edits and competing actions',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  const retry = await page.evaluate(() => window.probe.commands.at(-1));
  check(
    JSON.stringify(first) === JSON.stringify(retry),
    'uncertain lifecycle retry preserves UUID actor and exact payload',
  );
  await page.evaluate(() => {
    window.probe.mode = 'uncertain';
  });
  await page.getByRole('button', { name: 'Retire reviewed-goal v1' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  const reviewedRetirement = await page.evaluate(() => window.probe.commands.at(-1));
  check(
    reviewedRetirement.command.kind === 'Retire' &&
      reviewedRetirement.command.expectedReviewEventId === null &&
      reviewedRetirement.command.expectedRevision === 2,
    'Consultant retirement of a reviewed version carries no approval review-event argument',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  check(
    JSON.stringify(reviewedRetirement) ===
      JSON.stringify(await page.evaluate(() => window.probe.commands.at(-1))),
    'reviewed retirement retry retains exact UUID actor and null review-event payload',
  );
  await page.getByText('Create an immutable draft version', { exact: true }).click();
  check(
    (await page.getByRole('checkbox', { name: 'det-guard · SYN-GUARD · SECURITY' }).count()) === 1,
    'applicability choices deduplicate the exact inventory/evidence pair only',
  );
  await page.getByLabel('Stable outcome ID').fill('new-fictional-goal');
  await page.getByLabel('Title', { exact: true }).fill('Fictional documented behavior');
  await page.getByLabel('Desired behavior').fill('<svg onload="window.injection=true">');
  await page.getByRole('checkbox', { name: 'det-guard · SYN-GUARD · SECURITY' }).check();
  await page.getByRole('checkbox', { name: 'det-guard · SYN-OTHER · SECURITY' }).check();
  await page.getByLabel('Applicability display filter').selectOption('OPERATIONS');
  await page
    .getByRole('checkbox', { name: 'synthetic-ai-retry · configuration · OPERATIONS' })
    .check();
  await page.getByLabel('Applicability display filter').selectOption('SECURITY');
  check(
    (await page.getByRole('checkbox', { name: 'det-guard · SYN-GUARD · SECURITY' }).isChecked()) &&
      (await page.getByRole('checkbox', { name: 'det-guard · SYN-OTHER · SECURITY' }).isChecked()),
    'display filter preserves explicit selections across both health categories',
  );
  await page
    .getByLabel('Reference IDs (one per line)')
    .fill('fixture-reference\nfixture-reference');
  check(
    await page.getByRole('button', { name: 'Create draft version', exact: true }).isDisabled(),
    'duplicate immutable reference IDs cannot be submitted',
  );
  await page.getByLabel('Reference IDs (one per line)').fill('fixture-reference');
  await page.getByRole('button', { name: 'Create draft version', exact: true }).click();
  const draft = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    draft.kind === 'CreateDraft' &&
      draft.version === 1 &&
      draft.content.predecessorVersion === null &&
      draft.content.unitLinks.length === 3 &&
      draft.content.unitLinks.slice(0, 2).every((link) => link.inventoryId === 'det-guard') &&
      draft.content.unitLinks[2].inventoryId === 'synthetic-ai-retry' &&
      draft.content.unitLinks[2].evidenceCategory === 'configuration' &&
      draft.content.unitLinks[0].evidenceCategory === 'SYN-GUARD' &&
      draft.content.unitLinks[1].evidenceCategory === 'SYN-OTHER',
    'draft preserves two distinct inventory/evidence keys and immutable successor metadata',
  );
  check(
    draft.content.behavior.startsWith('<svg') && !('contentDigest' in draft.content),
    'untrusted text preserved as data; owning server seals new content',
  );
  await page.getByLabel('Budget override reason').fill('Fictional additional work justified');
  await page.getByRole('button', { name: 'Apply reasoned AI allowance override' }).click();
  const budget = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    budget.runTarget === 900 &&
      budget.categoryTarget === 900 &&
      budget.category === 'OPERATIONS' &&
      budget.expectedRevision === 1,
    'budget override uses approved literal targets and exact revision',
  );
  await page.evaluate(() => {
    window.probe.mode = 'uncertain';
  });
  await page.getByRole('button', { name: 'Apply reasoned AI allowance override' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  const uncertainBudget = await page.evaluate(() => window.probe.commands.at(-1));
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  check(
    JSON.stringify(uncertainBudget) ===
      JSON.stringify(await page.evaluate(() => window.probe.commands.at(-1))),
    'budget override retry preserves literal allowance payload and event UUID',
  );
  await page.evaluate(() => window.probe.terminal());
  await page.getByText('ai-retry · OPERATIONS · Failed', { exact: true }).waitFor();
  check(
    await page.getByRole('button', { name: 'Apply reasoned AI allowance override' }).isDisabled(),
    'terminal work cannot be reopened by allowance override',
  );
  await page
    .getByLabel('Reason for option-missing', { exact: true })
    .fill('Explicit reasoned label despite unavailable formula');
  await page.getByRole('button', { name: 'Override priority option-missing', exact: true }).click();
  const missingOverride = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    missingOverride.kind === 'OverridePriority' &&
      missingOverride.optionId === 'option-missing' &&
      missingOverride.priorityOverride === 'Medium',
    'verified source with unavailable original formula permits a reasoned label without fake score',
  );
  check(
    await page.getByRole('button', { name: 'Approve original effort option-missing' }).isDisabled(),
    'unavailable original effort cannot be approved as original',
  );
  await page
    .getByLabel('Effort assumptions for option-missing (one per line)')
    .fill('Explicit fictional estimate prerequisite');
  await page.getByRole('button', { name: 'Approve replacement effort option-missing' }).click();
  const missingReplacement = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    missingReplacement.kind === 'ReplaceEffort' &&
      missingReplacement.replacementSize === 'M' &&
      missingReplacement.assumptions.length === 1,
    'verified source with unavailable original effort permits explicit assumed replacement',
  );
  await page
    .getByLabel('Reason for option-a', { exact: true })
    .fill('Fictional source-bound planning decision');
  await page.getByLabel('Priority override for option-a').selectOption('Immediate');
  await page.getByRole('button', { name: 'Override priority option-a', exact: true }).click();
  const planning = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    planning.kind === 'OverridePriority' &&
      planning.priorityOverride === 'Immediate' &&
      planning.expectedSourceDigest === 'a'.repeat(64) &&
      planning.expectedRevision === 2,
    'priority override is exact source-bound sidecar command',
  );
  check(
    (await page.getByText('68 · High', { exact: true }).count()) === 1,
    'original calculated priority remains visible after a decision',
  );
  check(
    await page.getByRole('button', { name: 'Approve replacement effort option-a' }).isDisabled(),
    'replacement effort requires an explicit assumption',
  );
  await page
    .getByLabel('Effort assumptions for option-a (one per line)')
    .fill('Fictional scoped estimate');
  await page.getByLabel('Replacement relative effort for option-a').selectOption('S');
  await page.getByRole('button', { name: 'Approve replacement effort option-a' }).click();
  const replacement = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    replacement.replacementSize === 'S' &&
      replacement.assumptions[0] === 'Fictional scoped estimate' &&
      replacement.priorityOverride === null,
    'relative effort replacement requires explicit Consultant reason and assumptions',
  );
  check(
    await page.getByRole('button', { name: 'Withdraw effort override option-a' }).isDisabled(),
    'redundant withdrawal disabled when no override exists',
  );
  await page.evaluate(() => {
    window.probe.mode = 'uncertain';
  });
  await page.getByRole('button', { name: 'Approve original effort option-a' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  const uncertainPlanning = await page.evaluate(() => window.probe.commands.at(-1));
  check(
    uncertainPlanning.command.assumptions.length === 0,
    'original effort approval preserves its original assumptions contract',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  check(
    JSON.stringify(uncertainPlanning) ===
      JSON.stringify(await page.evaluate(() => window.probe.commands.at(-1))),
    'planning retry preserves event UUID and old exact source revision',
  );
  await page.evaluate(() => {
    window.probe.mode = 'mismatch';
  });
  await page.getByRole('button', { name: 'Approve original effort option-a' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  check(
    (await page
      .getByText('The response is uncertain. Retry the same event to resolve it.', { exact: true })
      .count()) === 1,
    'mismatched successful receipt cannot clear the pending exact event',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  await page.evaluate(() => window.probe.changeSource());
  await page.waitForFunction(() => document.body.textContent.includes('b'.repeat(64)));
  check(
    (await page.getByLabel('Reason for option-a', { exact: true }).inputValue()) === '',
    'source change clears stale planning draft before a fresh approval',
  );
  await page
    .getByLabel('Reason for option-a', { exact: true })
    .fill('Fresh explicit source decision');
  await page.evaluate(() => {
    window.probe.mode = 'delay';
  });
  await page.getByRole('button', { name: 'Approve original effort option-a' }).click();
  await page.evaluate(() => window.probe.changeRun());
  await page.waitForFunction(() => window.probe.aborted === 1);
  await page.evaluate(() => window.probe.late());
  check(
    (await page.getByText('LATE OLD RUN', { exact: true }).count()) === 0,
    'run switch aborts pending action and ignores old completion',
  );
  check(
    (await page.getByLabel('Reason for option-a', { exact: true }).inputValue()) === '',
    'run switch clears planning drafts',
  );
  await page.getByLabel('Reason for option-a', { exact: true }).fill('Old Consultant draft');
  await page
    .getByLabel('Effort assumptions for option-a (one per line)')
    .fill('Old Consultant assumptions');
  await page.evaluate(() => {
    window.probe.mode = 'committed';
    window.probe.changeActor();
  });
  await page.getByText('FictionalCustomer', { exact: true }).first().waitFor();
  check(
    (await page.getByLabel('Reason for option-a', { exact: true }).inputValue()) === '' &&
      (await page.getByLabel('Effort assumptions for option-a (one per line)').inputValue()) === '',
    'actor context change clears prior actor planning drafts',
  );
  check(
    (await page.getByLabel('Stable outcome ID').inputValue()) === '' &&
      (await page.getByLabel('Desired behavior').inputValue()) === '',
    'actor context change clears prior actor outcome draft content',
  );
  await page.getByText('FictionalCustomer', { exact: true }).first().waitFor();
  check(
    await page.getByRole('button', { name: 'Consultant review pending-goal v1' }).isDisabled(),
    'server capability props disable Consultant commands for fictional customer actor',
  );
  await page.getByLabel('Reason for the next lifecycle action').fill('Fictional explicit approval');
  check(
    await page.getByRole('button', { name: 'Approve original effort option-a' }).isDisabled(),
    'customer approval actor has no planning authority',
  );
  await page.getByRole('button', { name: 'Fictional customer approval reviewed-goal v1' }).click();
  const customerApproval = await page.evaluate(() => window.probe.commands.at(-1).command);
  check(
    customerApproval.kind === 'Approve' &&
      customerApproval.expectedReviewEventId.endsWith('0002') &&
      customerApproval.expectedRevision === 2,
    'fictional customer approval binds exact Consultant review and content revision',
  );
  await page.evaluate(() => {
    window.probe.mode = 'uncertain';
  });
  await page.getByRole('button', { name: 'Retire access-governance v1' }).click();
  await page.getByRole('button', { name: 'Retry same event' }).waitFor();
  const approvedRetirement = await page.evaluate(() => window.probe.commands.at(-1));
  check(
    approvedRetirement.command.kind === 'Retire' &&
      approvedRetirement.command.expectedReviewEventId === null &&
      approvedRetirement.command.expectedRevision === 3,
    'fictional customer approved retirement preserves exact content revision with null review-event argument',
  );
  await page.evaluate(() => {
    window.probe.mode = 'committed';
  });
  await page.getByRole('button', { name: 'Retry same event' }).click();
  check(
    JSON.stringify(approvedRetirement) ===
      JSON.stringify(await page.evaluate(() => window.probe.commands.at(-1))),
    'approved retirement retry retains exact UUID fictional customer and null review-event payload',
  );
  await page.evaluate(() => window.probe.deny());
  await page.waitForFunction(
    () =>
      [...document.querySelectorAll('button')].find(
        (button) => button.textContent === 'Start combined Phase 1B run with selected versions',
      )?.disabled,
  );
  check(
    await page
      .getByRole('button', { name: 'Start combined Phase 1B run with selected versions' })
      .isDisabled(),
    'denied start authority disables new run',
  );
  await page.addScriptTag({
    content: await readFile(resolve(browserDependencies, 'axe-core/axe.min.js'), 'utf8'),
  });
  const accessibility = await page.evaluate(
    async () =>
      (
        await window.axe.run(document, {
          runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] },
        })
      ).violations,
  );
  check(
    accessibility.length === 0,
    `WCAG axe component scan has zero violations (${JSON.stringify(accessibility.map((value) => value.id))})`,
  );
  await page.setViewportSize({ width: 390, height: 844 });
  check(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    'mobile layout fits viewport without horizontal page overflow',
  );
  await page.getByText('Exact locked outcome versions (1)', { exact: true }).click();
  check(
    (await page
      .getByText('access-governance v1 · revision 3 · approval', { exact: false })
      .count()) > 0,
    'historical exact approval proof remains visible after run switch',
  );
  check(errors.length === 0, `browser reports no runtime errors (${errors.join('; ')})`);
  if (process.env.IGA_PHASE1B_UI_SCREENSHOT)
    await page.screenshot({ path: process.env.IGA_PHASE1B_UI_SCREENSHOT, fullPage: true });
  console.log(`PASS ${checks} Phase1B isolated browser assertions`);
} finally {
  await browser?.close();
  await server?.close();
  await rm(temporary, { recursive: true, force: true });
}
