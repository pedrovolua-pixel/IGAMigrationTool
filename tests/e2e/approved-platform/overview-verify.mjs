import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

// Usage: pinned-node iga-ui-w4-overview-verify.mjs /absolute/path/to/src/web [evidence.json]
const webRoot = path.resolve(process.argv[2] ?? process.cwd());
const require = createRequire(path.join(webRoot, 'package.json'));
const { build } = await import(pathToFileURL(require.resolve('vite')));
const output = fs.mkdtempSync(path.join(os.tmpdir(), 'iga-overview-ssr-'));
try {
  await build({ configFile: false, root: webRoot, build: {
    ssr: 'src/PlatformOverview.tsx', outDir: output, emptyOutDir: true,
    rollupOptions: { output: { entryFileNames: 'overview.mjs' } },
  } });
  fs.symlinkSync(path.join(webRoot, 'node_modules'), path.join(output, 'node_modules'));
  const React = require('react');
  const { renderToStaticMarkup } = require('react-dom/server');
  const { PlatformOverview } = await import(pathToFileURL(path.join(output, 'overview.mjs')));
  const score = { raw: '41.25', display: '41', status: 'Red', eligibleUnits: 2 };
  const run = { runId: 'test-run', revision: 4, selection: { scopeLabel: '<script>unsafe</script>' }, progress: { plannedUnits: 8 }, executableCoverage: { hasApplicableUnits: true, numerator: 3, denominator: 8 } };
  const analysis = { status: 'Ready', runId: run.runId, runRevision: run.revision,
    provisional: score, publishableCurrent: { ...score, raw: '52', display: '52', status: 'Yellow' },
    categories: [{ id: 'SECURITY', provisional: score, publishableCurrent: score }, { id: 'OPERATIONS', provisional: { raw: null, display: null, status: 'Unavailable', eligibleUnits: 0 }, publishableCurrent: score }],
    findings: [{ id: 'finding-a', category: 'SECURITY', severity: 'Critical', state: 'Proposed' }, { id: 'finding-b', category: 'OPERATIONS', severity: 'Low', state: 'Proposed' }],
    quality: { plannedUnits: 8, executedUnits: 3, gapUnits: 4, notApplicableUnits: 1, totalFindingUnits: 2, proposedReviewUnits: 0 },
    review: { status: 'Ready', findings: [{ id: 'finding-a', state: 'Confirmed' }, { id: 'finding-b', state: 'Deferred' }] }, maturity: null };
  const render = (analysis, run) => renderToStaticMarkup(React.createElement(PlatformOverview, { analysis, run, onNavigate: () => {} }));
  let assertions = 0;
  const has = (markup, pattern) => { assert.match(markup, pattern); assertions++; };
  const lacks = (markup, pattern) => { assert.doesNotMatch(markup, pattern); assertions++; };
  const ready = render(analysis, run);
  has(ready, /stroke-dasharray="41.25 100"/);
  has(ready, /Current publishable score: 52/);
  has(ready, /Proposed findings<\/dt><dd>0<\/dd>/);
  has(ready, /Critical \/ high proposed<\/dt><dd>0<\/dd>/);
  has(ready, /Unavailable categories have no score or bar/);
  for (const severity of ['Critical', 'High', 'Medium', 'Low', 'Informational']) has(ready, new RegExp('<span>' + severity + '<\\/span>'));
  has(ready, /Explained gap units<\/dt><dd>4<\/dd>/);
  has(ready, /Executable coverage<\/dt><dd>3 \/ 8<\/dd>/);
  has(ready, /Historical health is unavailable/);
  has(ready, /&lt;script&gt;unsafe&lt;\/script&gt;/);
  lacks(ready, /<script>/);
  for (const unavailable of [render(null, null), render(null, run), render({ ...analysis, runRevision: 3 }, run), render({ ...analysis, status: 'Unavailable' }, run)]) {
    has(unavailable, /Score unavailable/);
    lacks(unavailable, /stroke-dasharray=/);
    lacks(unavailable, /finding-a/);
  }
  const zero = render({ ...analysis, provisional: { ...score, raw: '0', display: '0' } }, run);
  has(zero, /stroke-dasharray="0 100"/);
  has(zero, /<strong>0<\/strong>/);
  has(render({ ...analysis, review: null }, run), /Proposed findings<\/dt><dd>2<\/dd>/);
  has(render({ ...analysis, categories: [] }, run), /Category scores are unavailable/);
  has(render({ ...analysis, quality: null }, run), /Explained gap units<\/dt><dd>Unavailable<\/dd>/);
  const categories = ['SECURITY', 'OPERATIONS', 'CORRECTNESS'].map(id => ({ id, provisional: score, publishableCurrent: score }));
  const radar = render({ ...analysis, categories }, run);
  has(radar, /class="platform-overview-radar"/);
  has(radar, /class="platform-overview-radar-value"/);
  lacks(radar, /NaN/);
  has(radar, /Correctness/);
  const missing = render({ ...analysis, categories: [...categories, { id: 'MISSING', provisional: { ...score, status: 'Unavailable' }, publishableCurrent: score }] }, run);
  lacks(missing, /class="platform-overview-radar"/);
  has(missing, /class="platform-overview-category-chart"/);
  has(missing, /Missing/);
  const evidence = { result: 'pass', assertions, scope: 'Pure PlatformOverview SSR rendering; no backend or browser',
    scenarios: ['actual precision and publishable separation', 'current review overrides original state', 'five severity levels', 'quality separate from score', 'untrusted title escaping', 'no run', 'no admitted analysis', 'stale run revision', 'unavailable analysis', 'zero score', 'review fallback', 'missing categories', 'missing quality', 'actual category radar', 'unavailable category bars fallback'],
    unavailable: ['browser/mobile interactions', 'integrated application acceptance'],
    files: ['PlatformOverview.tsx', 'PlatformOverview.css'].map(name => ({ name, sha256: createHash('sha256').update(fs.readFileSync(path.join(webRoot, 'src', name))).digest('hex') })) };
  if (process.argv[3]) fs.writeFileSync(process.argv[3], JSON.stringify(evidence, null, 2) + '\n');
  console.log(JSON.stringify(evidence));
} finally {
  fs.rmSync(output, { recursive: true, force: true });
}
