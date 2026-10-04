import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import {
  MAX_RESPONSE_BYTES,
  POPULATION_DIGEST,
  SAMPLE_DIGEST,
  parseWorkspace,
  parseVersion,
  parseWrapper,
  parseHistory,
  strictJson,
  verifyVersion,
  utc,
  string,
} from './contracts.ts';
import type { Workspace, Version, Wrapper, Receipt, History, Member } from './contracts.ts';
import { FetchClient, ResponseAdmissionError, Workflow, prepare } from './workflow.ts';
import type { Client, Draft } from './workflow.ts';
import { selectedMemberIds } from './selected-members.ts';
const hash = (s: string) => createHash('sha256').update(s, 'utf8').digest('hex');
const time = '2026-10-03T12:00:00.0000000+00:00';
const emptyCounts = {
  selected: 100,
  confirmed: 0,
  rejected: 0,
  indeterminate: 0,
  unreviewed: 100,
  corrected: 0,
  denominator: 0,
  lowSampleWarning: true,
};
const desiredCounts = { ...emptyCounts, selected: 0, unreviewed: 0 };
function fixture(): { w: Workspace; v: Version } {
  const members: Member[] = selectedMemberIds.map((id) => {
    const n = Number(id.slice(-6));
    const severity = n < 20 ? 'Critical' : n < 40 ? 'High' : 'Medium';
    const env = n < 60 ? 'synthetic-env-a' : 'synthetic-env-b';
    return {
      original: {
        memberId: id,
        title: 'Fictional original <script>window.bad=true</script>',
        severity,
        category: 'Fictional category',
        rootCause: 'Fictional root cause',
        recommendation: 'Fictional recommendation',
        evidenceReferenceIds: [`${id}-evidence`],
      },
      originMetadata: {
        memberId: id,
        originSeverity: severity,
        environmentId: env,
        moduleId: 'synthetic-module',
        categoryId: 'synthetic-category',
        ruleId: 'synthetic-rule',
        modelPromptId: 'synthetic-model',
        confidenceBandId: 'synthetic-confidence',
      },
      assignmentId: `${env}-assignment`,
      revision: 0,
      outcome: 'Unreviewed',
      originatingClassification: null,
      canReview: true,
      canCorrectPresentation: true,
      authorizedContextSufficient: true,
      currentCorrection: null,
    };
  });
  const manifest = {
    schemaVersion: 'synthetic-evaluation-workflow-version-v1',
    version: 0,
    sourceDigest: 'a'.repeat(64),
    populationDigest: POPULATION_DIGEST,
    sampleDigest: SAMPLE_DIGEST,
    originalSampleVersionManifestDigest: 'b'.repeat(64),
    originalSampleCorrectionCutoffUtc: time,
    registryVersionId: 'synthetic-registry-v1',
    registryDigest: 'c'.repeat(64),
    lastEventSequence: 0,
    eventsDigest: hash('[]'),
    correctionCutoffUtc: time,
    predecessorDigest: null,
  };
  const versionManifestJson = JSON.stringify(manifest);
  const versionManifestDigest = hash(versionManifestJson);
  const accuracy = {
    schema: 'synthetic-evaluation-accuracy-v1',
    locks: {
      evaluationId: 'synthetic-workflow-version-0',
      scopeId: 'synthetic-scope',
      populationDigest: POPULATION_DIGEST,
      sampleDigest: SAMPLE_DIGEST,
      versionManifestDigest,
      correctionCutoffUtc: time,
    },
    members: members.map((m) => ({
      id: m.original.memberId,
      track: 'GeneralAi',
      desiredOutcomeVersion: null,
      desiredOutcomeApproval: null,
    })),
    reviews: members.map((m) => ({
      memberId: m.original.memberId,
      outcome: 'Unreviewed',
      originatingClassification: null,
    })),
  };
  const accuracyCanonicalJson = JSON.stringify(accuracy);
  const accuracyDigest = hash(accuracyCanonicalJson);
  const warning = {
    schema: 'synthetic-evaluation-warning-v1',
    accuracyDigest,
    metadata: members.map((m) => m.originMetadata),
    summaries: { generalAi: emptyCounts, desiredOutcome: desiredCounts },
    breakdowns: [],
  };
  const warningCanonicalJson = JSON.stringify(warning);
  const warningDigest = hash(warningCanonicalJson);
  const snapshotCanonicalJson = JSON.stringify({
    schemaVersion: 'synthetic-evaluation-workflow-snapshot-v1',
    versionManifestJson,
    accuracyCanonicalJson,
    warningCanonicalJson,
  });
  const v = {
    version: 0,
    correctionCutoffUtc: time,
    lastEventSequence: 0,
    registryVersionId: 'synthetic-registry-v1',
    registryDigest: 'c'.repeat(64),
    versionManifestJson,
    versionManifestDigest,
    predecessorDigest: null,
    accuracyCanonicalJson,
    accuracyDigest,
    warningCanonicalJson,
    warningDigest,
    snapshotCanonicalJson,
    contentDigest: hash(snapshotCanonicalJson),
  };
  const w = {
    schemaVersion: 'synthetic-evaluation-workflow-v1',
    actorId: 'synthetic-reviewer',
    aggregateRevision: 0,
    registryVersionId: 'synthetic-registry-v1',
    sourceDigest: 'a'.repeat(64),
    populationDigest: POPULATION_DIGEST,
    sampleDigest: SAMPLE_DIGEST,
    sampleCorrectionCutoffUtc: time,
    members,
    versions: [
      {
        version: 0,
        correctionCutoffUtc: time,
        lastEventSequence: 0,
        registryVersionId: 'synthetic-registry-v1',
        contentDigest: v.contentDigest,
        predecessorDigest: null,
      },
    ],
  };
  return { w, v };
}
const success = <T>(
  payload: T,
  csrfToken: string | null = null,
  alreadyApplied: boolean | null = null,
): Wrapper<T> => ({
  schemaVersion: 1,
  demoOnly: true,
  csrfToken,
  issue: null,
  payload,
  alreadyApplied,
});
const denied = <T>(): Wrapper<T> => ({
  schemaVersion: 1,
  demoOnly: true,
  csrfToken: null,
  issue: 'Denied',
  payload: null,
  alreadyApplied: null,
});
const draft: Draft = {
  kind: 'Review',
  outcome: 'Confirmed',
  origin: 'Confirmed',
  reason: 'Fictional evidence confirms the original.',
  evidenceReferenceIds: [`${selectedMemberIds[0]}-evidence`],
  correction: { severity: null, category: null, rootCause: null, recommendation: null },
};
const eventId = '12345678-1234-1234-1234-123456789012';
class Mock implements Client {
  f = fixture();
  bodies: string[] = [];
  nextWorkspace: Promise<Wrapper<Workspace>> | null = null;
  workspaceDenied = false;
  applyResponse: Promise<Wrapper<Receipt>> | null = null;
  historyResponse: Promise<Wrapper<History>> | null = null;
  async workspace() {
    return (
      this.nextWorkspace ?? (this.workspaceDenied ? denied() : success(this.f.w, 'fictional-csrf'))
    );
  }
  async version() {
    return success(this.f.v);
  }
  async apply(body: string) {
    this.bodies.push(body);
    return this.applyResponse ?? Promise.reject(new TypeError('Network failure'));
  }
  async history() {
    return (
      this.historyResponse ??
      success({
        memberId: selectedMemberIds[0],
        aggregateRevision: 0,
        memberRevision: 0,
        registryVersionId: 'synthetic-registry-v1',
        events: [],
        nextAfterSequence: null,
      })
    );
  }
}
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

test('literal initial fixture admits closed100-member source and raw4envelopes', async () => {
  const { w, v } = fixture();
  const parsed = parseWorkspace(w);
  const verified = await verifyVersion(parseVersion(v), parsed);
  assert.deepEqual(verified.counts, emptyCounts);
  assert.equal(verified.counts.denominator, 0);
  assert.equal(
    parsed.members.filter((m) => ['Critical', 'High'].includes(m.originMetadata.originSeverity))
      .length,
    40,
  );
  assert.equal(
    parsed.members.filter(
      (m) =>
        m.originMetadata.originSeverity === 'Medium' &&
        m.originMetadata.environmentId === 'synthetic-env-a',
    ).length,
    15,
  );
  assert.equal(
    parsed.members.filter((m) => m.originMetadata.environmentId === 'synthetic-env-b').length,
    45,
  );
});

test('closedworkspace rejects missing/extra keys and malformed scalars without normalizing', () => {
  for (const key of Object.keys(fixture().w)) {
    const w: Record<string, unknown> = { ...fixture().w };
    delete w[key];
    assert.throws(() => parseWorkspace(w));
  }
  for (const value of [
    null,
    {},
    [],
    { ...fixture().w, role: 'Consultant' },
    { ...fixture().w, aggregateRevision: -1 },
    { ...fixture().w, aggregateRevision: 9007199254740992 },
    { ...fixture().w, sampleDigest: SAMPLE_DIGEST.toUpperCase() },
    { ...fixture().w, actorId: 'customer-secret' },
  ])
    assert.throws(() => parseWorkspace(value));
});

test('every membermissing/unknown key, selection replacement, origintaxonomy/env/assignment drift denied', () => {
  for (const key of Object.keys(fixture().w.members[0]!)) {
    const f = fixture();
    delete (f.w.members[0] as unknown as Record<string, unknown>)[key];
    assert.throws(() => parseWorkspace(f.w));
  }
  const mutate = [
    (m: Member) => {
      m.original.memberId = 'synthetic-other';
    },
    (m: Member) => {
      m.originMetadata.environmentId = 'synthetic-env-b';
    },
    (m: Member) => {
      m.originMetadata.originSeverity = 'Low';
    },
    (m: Member) => {
      m.assignmentId = 'synthetic-other';
    },
    (m: Member) => {
      m.originatingClassification = 'Rejected';
    },
    (m: Member) => {
      m.revision = 1;
    },
    (m: Member) => {
      m.original.evidenceReferenceIds = ['synthetic-z', 'synthetic-z'];
    },
  ];
  for (const change of mutate) {
    const f = fixture();
    change(f.w.members[0]!);
    assert.throws(() => parseWorkspace(f.w));
  }
  const f = fixture();
  f.w.members[1] = f.w.members[0]!;
  assert.throws(() => parseWorkspace(f.w));
});

test('strictJSON rejects duplicates atnesteddepth, escapesalias, trailinggrammar, invaliddates', () => {
  assert.deepEqual(strictJson('{"a":[true,null,1,"safe"]}'), { a: [true, null, 1, 'safe'] });
  for (const raw of [
    '{"a":1,"a":2}',
    '{"a":{"x":1,"\\u0078":2}}',
    '{"a":1}false',
    '{"a":01}',
    '[1,]',
    '{"a":"bad\ntext"}',
    '['.repeat(40) + '0' + ']'.repeat(40),
  ])
    assert.throws(() => strictJson(raw));
  assert.throws(() => utc('2026-02-31T12:00:00+00:00'));
  assert.throws(() => utc('2026-10-03T12:00:00-04:00'));
});

test('closedwrapper CSRF andreceipt modes withmetadataonlydenials', () => {
  const f = fixture();
  assert.equal(
    parseWrapper(success(f.w, 'fictional-csrf'), parseWorkspace, 'workspace').payload?.members
      .length,
    100,
  );
  assert.equal(parseWrapper(denied(), parseWorkspace, 'workspace').payload, null);
  for (const w of [
    { ...denied(), payload: f.w },
    { ...denied(), csrfToken: 'no' },
    { ...success(f.w), alreadyApplied: true },
    { ...success(f.w, 'fictional-csrf'), actor: 'fake' },
    { ...denied(), issue: 'NewIssue' },
  ])
    assert.throws(() => parseWrapper(w, parseWorkspace, 'workspace'));
  assert.throws(() => parseWrapper(success(f.v, 'fictional-csrf'), parseVersion, 'version'));
});

test('tampered rawbytes/hash/link/summary/source/lock relations fail closed', async () => {
  for (const field of [
    'versionManifestJson',
    'accuracyCanonicalJson',
    'warningCanonicalJson',
    'snapshotCanonicalJson',
  ] as const) {
    const f = fixture();
    f.v[field] += ' ';
    await assert.rejects(() => verifyVersion(f.v, f.w));
  }
  const f = fixture();
  f.w.sourceDigest = 'd'.repeat(64);
  await assert.rejects(() => verifyVersion(f.v, f.w));
  const g = fixture();
  g.w.versions[0]!.contentDigest = 'd'.repeat(64);
  await assert.rejects(() => verifyVersion(g.v, g.w));
});

test('prepared review exactly frozen noauthority fields and stableUUIDbody', () => {
  const { w } = fixture();
  const p = prepare(w, w.members[0]!, draft, 'fictional-csrf', eventId, 4);
  assert.deepEqual(Object.keys(JSON.parse(p.body)), [
    'eventId',
    'memberId',
    'kind',
    'expectedAggregateRevision',
    'expectedMemberRevision',
    'expectedRegistryVersionId',
    'expectedSourceDigest',
    'expectedSampleDigest',
    'outcome',
    'originatingClassification',
    'reason',
    'evidenceReferenceIds',
    'correction',
  ]);
  assert.equal(p.command.eventId, eventId);
  assert.equal(p.command.expectedAggregateRevision, 0);
  assert.equal(p.command.outcome, 'Confirmed');
  assert.equal(p.command.correction, null);
  assert.ok(
    Object.isFrozen(p) &&
      Object.isFrozen(p.command) &&
      Object.isFrozen(p.command.evidenceReferenceIds),
  );
  assert.throws(() => {
    p.command.evidenceReferenceIds.push('synthetic-other');
  });
  draft.evidenceReferenceIds[0] = 'synthetic-change';
  assert.equal(p.command.evidenceReferenceIds[0], `${selectedMemberIds[0]}-evidence`);
  draft.evidenceReferenceIds[0] = `${selectedMemberIds[0]}-evidence`;
});

test('draftreason/evidence/context/correctionbounds denyinvalidcommands', () => {
  const { w } = fixture();
  const m = w.members[0]!;
  for (const d of [
    { ...draft, reason: '' },
    { ...draft, reason: '  ' },
    { ...draft, reason: 'a\0b' },
    { ...draft, reason: 'x'.repeat(2001) },
    { ...draft, evidenceReferenceIds: [] },
    { ...draft, evidenceReferenceIds: ['synthetic-forbidden'] },
    {
      ...draft,
      evidenceReferenceIds: [...draft.evidenceReferenceIds, ...draft.evidenceReferenceIds],
    },
    { ...draft, outcome: 'Indeterminate' },
    { ...draft, outcome: 'Corrected' },
    { ...draft, kind: 'PresentationCorrection' },
    { ...draft, kind: 'Forged' },
  ] as Draft[])
    assert.throws(() => prepare(w, m, d, 'fictional-csrf', eventId, 0));
  assert.doesNotThrow(() =>
    prepare(w, m, { ...draft, reason: 'x'.repeat(2000) }, 'fictional-csrf', eventId, 0),
  );
  m.authorizedContextSufficient = false;
  assert.throws(() => prepare(w, m, draft, 'fictional-csrf', eventId, 0));
  assert.equal(
    prepare(
      w,
      m,
      { ...draft, outcome: 'Indeterminate', evidenceReferenceIds: [] },
      'fictional-csrf',
      eventId,
      0,
    ).command.outcome,
    'Indeterminate',
  );
});

test('correctedreview explicitorigin/dimensions and presentationcorrection cannotinventclassification', () => {
  const { w } = fixture();
  const m = w.members[0]!;
  const correction = {
    severity: 'Fictional presentation',
    category: 'Fictional category',
    rootCause: '<img src=x onerror=alert(1)>',
    recommendation: null,
  };
  const p = prepare(
    w,
    m,
    { ...draft, outcome: 'Corrected', origin: 'Rejected', correction },
    'fictional-csrf',
    eventId,
    0,
  );
  assert.equal(p.command.originatingClassification, 'Rejected');
  assert.deepEqual(p.command.correction, correction);
  const q = prepare(
    w,
    m,
    { ...draft, kind: 'PresentationCorrection', evidenceReferenceIds: [], correction },
    'fictional-csrf',
    eventId,
    0,
  );
  assert.equal(q.command.outcome, null);
  assert.equal(q.command.originatingClassification, null);
  assert.equal(m.outcome, 'Unreviewed');
  m.canReview = false;
  assert.throws(() => prepare(w, m, draft, 'fictional-csrf', eventId, 0));
  assert.doesNotThrow(() =>
    prepare(
      w,
      m,
      { ...draft, kind: 'PresentationCorrection', correction },
      'fictional-csrf',
      eventId,
      0,
    ),
  );
});

test('transportunknown then explicitretry is exactbody/UUID; no automaticretry', async () => {
  const client = new Mock();
  const flow = new Workflow(client);
  await flow.refresh();
  flow.stage(draft);
  const body = flow.state.prepared!.body;
  await flow.record();
  assert.equal(flow.state.mode, 'unknown');
  assert.deepEqual(client.bodies, [body]);
  await flow.record();
  assert.deepEqual(client.bodies, [body, body]);
  flow.destroy();
});

test('metadataDenied and malformedreceipt clearallprotectedstate and cannot retry', async () => {
  for (const malformed of [false, true]) {
    const client = new Mock();
    const flow = new Workflow(client);
    await flow.refresh();
    client.applyResponse = malformed
      ? Promise.reject(new ResponseAdmissionError('Unavailable'))
      : Promise.resolve(denied<Receipt>());
    flow.stage(draft);
    await flow.record();
    assert.equal(flow.state.mode, 'unavailable');
    assert.equal(flow.state.workspace, null);
    assert.equal(flow.state.version, null);
    assert.equal(flow.state.prepared, null);
    await flow.record();
    assert.equal(client.bodies.length, 1);
    flow.destroy();
  }
});

test('staleconflict clearsdraft and refresh requiresnewexplicitaction', async () => {
  const client = new Mock();
  client.applyResponse = Promise.resolve({ ...denied<Receipt>(), issue: 'RevisionConflict' });
  const flow = new Workflow(client);
  await flow.refresh();
  flow.stage(draft);
  await flow.record();
  assert.equal(flow.state.mode, 'conflict');
  assert.equal(flow.state.workspace, null);
  assert.equal(flow.state.prepared, null);
  await flow.refresh();
  assert.equal(flow.state.mode, 'ready');
  assert.equal(flow.state.prepared, null);
  assert.equal(client.bodies.length, 1);
  flow.destroy();
});

test('revokedrefresh hasnocachedcohort/countdisclosure and invalidatespriorresponseepochs', async () => {
  const client = new Mock();
  const flow = new Workflow(client);
  await flow.refresh();
  const delayed = deferred<Wrapper<Workspace>>();
  client.nextWorkspace = delayed.promise;
  const old = flow.refresh();
  assert.equal(flow.state.workspace, null);
  client.nextWorkspace = null;
  client.workspaceDenied = true;
  await flow.refresh();
  delayed.resolve(success(client.f.w, 'fictional-csrf'));
  await old;
  assert.equal(flow.state.mode, 'unavailable');
  assert.equal(flow.state.workspace, null);
  assert.equal(flow.state.version, null);
  flow.destroy();
});

test('memberselection clearsdraft/history and oldhistoryresponse cannotbindtoothermember', async () => {
  const client = new Mock();
  const flow = new Workflow(client);
  await flow.refresh();
  const d = deferred<Wrapper<History>>();
  client.historyResponse = d.promise;
  const pending = flow.loadHistory();
  await Promise.resolve();
  flow.selectMember(selectedMemberIds[1]);
  d.resolve(
    success({
      memberId: selectedMemberIds[0],
      aggregateRevision: 0,
      memberRevision: 0,
      registryVersionId: 'synthetic-registry-v1',
      events: [],
      nextAfterSequence: null,
    }),
  );
  await pending;
  assert.equal(flow.state.memberId, selectedMemberIds[1]);
  assert.equal(flow.state.history, null);
  assert.equal(flow.state.prepared, null);
  flow.destroy();
});

test('sourcepermissionrevoked beforehistory clearsoriginalsource, notjustminimalhistory', async () => {
  const client = new Mock();
  const flow = new Workflow(client);
  await flow.refresh();
  client.workspaceDenied = true;
  await flow.loadHistory();
  assert.equal(flow.state.workspace, null);
  assert.equal(flow.state.version, null);
  assert.equal(flow.state.history, null);
  flow.destroy();
});

test('minimalhistory parser denies original/actionfields and wrongmember/eventpage', () => {
  const h = {
    memberId: selectedMemberIds[0],
    aggregateRevision: 0,
    memberRevision: 0,
    registryVersionId: 'synthetic-registry-v1',
    events: [],
    nextAfterSequence: null,
  };
  assert.equal(parseHistory(h).events.length, 0);
  assert.throws(() => parseHistory({ ...h, original: { secret: 'no' } }));
  assert.throws(() => parseHistory({ ...h, canReview: true }));
  assert.throws(() => parseHistory({ ...h, nextAfterSequence: 1 }));
});

test('fetchadmission rejectsunknownstatus/type/UTF8/duplicates/headerboundbeforeprotectedparse', async () => {
  const old = globalThis.fetch;
  const client = new FetchClient();
  try {
    const raw = JSON.stringify(success(fixture().w, 'fictional-csrf'));
    for (const response of [
      new Response(raw, { status: 201, headers: { 'content-type': 'application/json' } }),
      new Response(raw, { status: 200, headers: { 'content-type': 'text/html' } }),
      new Response(raw, {
        status: 200,
        headers: {
          'content-type': 'application/json',
          'content-length': String(MAX_RESPONSE_BYTES + 1),
        },
      }),
      new Response('{"schemaVersion":1,"schemaVersion":1}', {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
      new Response(new Uint8Array([0xc0, 0xaf]), {
        headers: { 'content-type': 'application/json' },
      }),
    ]) {
      globalThis.fetch = async () => response;
      await assert.rejects(
        () => client.workspace(new AbortController().signal),
        ResponseAdmissionError,
      );
    }
    let options: RequestInit | undefined;
    globalThis.fetch = async (_url, init) => {
      options = init;
      return new Response(JSON.stringify(denied()), {
        status: 403,
        headers: { 'content-type': 'application/json' },
      });
    };
    assert.equal(
      (await client.apply('{}', 'fictional-csrf', new AbortController().signal)).issue,
      'Denied',
    );
    assert.equal((options?.headers as Record<string, string>)['X-CSRF-TOKEN'], 'fictional-csrf');
    assert.equal(options?.credentials, 'same-origin');
    assert.equal(options?.cache, 'no-store');
  } finally {
    globalThis.fetch = old;
  }
});

test('textscalar admission preserves validpairs and replacementchar, rejects loneUTF16', () => {
  assert.equal(string('fictional 😀'), 'fictional 😀');
  assert.equal(string('fictional �'), 'fictional �');
  for (const raw of ['\ud800', '\udc00', 'a\ud800b', 'a\udc00b']) assert.throws(() => string(raw));
  const { w } = fixture();
  assert.throws(() =>
    prepare(w, w.members[0]!, { ...draft, reason: 'reason\ud800' }, 'fictional-csrf', eventId, 0),
  );
});

test('literal95/100servercounts with correctedRejected overlap stays100denominator', async () => {
  const f = fixture();
  f.w.aggregateRevision = 100;
  f.w.members.forEach((m, i) => {
    m.revision = 1;
    m.outcome = i < 95 ? 'Confirmed' : 'Rejected';
  });
  f.w.members[95]!.outcome = 'Corrected';
  f.w.members[95]!.originatingClassification = 'Rejected';
  f.w.members[95]!.currentCorrection = {
    severity: 'Presentation only',
    category: null,
    rootCause: null,
    recommendation: null,
  };
  const manifest = JSON.parse(f.v.versionManifestJson);
  manifest.version = 100;
  manifest.lastEventSequence = 100;
  manifest.eventsDigest = hash('independent-fictional100events');
  manifest.predecessorDigest = hash('independent-version99');
  f.v.version = 100;
  f.v.lastEventSequence = 100;
  f.v.predecessorDigest = manifest.predecessorDigest;
  f.v.versionManifestJson = JSON.stringify(manifest);
  f.v.versionManifestDigest = hash(f.v.versionManifestJson);
  const accuracy = JSON.parse(f.v.accuracyCanonicalJson);
  accuracy.locks.evaluationId = 'synthetic-workflow-version-100';
  accuracy.locks.versionManifestDigest = f.v.versionManifestDigest;
  accuracy.reviews = f.w.members.map((m) => ({
    memberId: m.original.memberId,
    outcome: m.outcome,
    originatingClassification: m.originatingClassification,
  }));
  f.v.accuracyCanonicalJson = JSON.stringify(accuracy);
  f.v.accuracyDigest = hash(f.v.accuracyCanonicalJson);
  const warning = JSON.parse(f.v.warningCanonicalJson);
  warning.accuracyDigest = f.v.accuracyDigest;
  warning.summaries.generalAi = {
    selected: 100,
    confirmed: 95,
    rejected: 5,
    indeterminate: 0,
    unreviewed: 0,
    corrected: 1,
    denominator: 100,
    lowSampleWarning: false,
  };
  f.v.warningCanonicalJson = JSON.stringify(warning);
  f.v.warningDigest = hash(f.v.warningCanonicalJson);
  f.v.snapshotCanonicalJson = JSON.stringify({
    schemaVersion: 'synthetic-evaluation-workflow-snapshot-v1',
    versionManifestJson: f.v.versionManifestJson,
    accuracyCanonicalJson: f.v.accuracyCanonicalJson,
    warningCanonicalJson: f.v.warningCanonicalJson,
  });
  f.v.contentDigest = hash(f.v.snapshotCanonicalJson);
  f.w.versions = Array.from({ length: 101 }, (_, i) => ({
    version: i,
    correctionCutoffUtc: time,
    lastEventSequence: i,
    registryVersionId: 'synthetic-registry-v1',
    contentDigest: i === 100 ? f.v.contentDigest : hash(`independent-version${i}`),
    predecessorDigest: i === 0 ? null : hash(`independent-version${i - 1}`),
  }));
  const result = await verifyVersion(parseVersion(f.v), parseWorkspace(f.w));
  assert.deepEqual(result.counts, {
    selected: 100,
    confirmed: 95,
    rejected: 5,
    indeterminate: 0,
    unreviewed: 0,
    corrected: 1,
    denominator: 100,
    lowSampleWarning: false,
  });
  assert.equal(
    `${result.counts.confirmed} / ${result.counts.denominator} confirmed`,
    '95 / 100 confirmed',
  );
  assert.equal(f.w.members[95]!.originMetadata.originSeverity, 'Medium');
});

test('successfulreceiptrequires matchingfreshversionchain; oldread cannot masqueradeasrecorded', async () => {
  const client = new Mock();
  const flow = new Workflow(client);
  await flow.refresh();
  flow.stage(draft);
  const id = flow.state.prepared!.command.eventId;
  client.applyResponse = Promise.resolve(
    success(
      {
        eventId: id,
        memberId: selectedMemberIds[0],
        actorId: 'synthetic-reviewer',
        aggregateRevision: 1,
        memberRevision: 1,
        version: 1,
        snapshotDigest: 'd'.repeat(64),
        recordedAtUtc: time,
      },
      null,
      false,
    ),
  );
  await flow.record();
  assert.equal(flow.state.mode, 'unavailable');
  assert.equal(flow.state.receipt, null);
  assert.equal(flow.state.workspace, null);
  flow.destroy();
});

test('dedicatedbuildroot uses platformfilepath ratherthanrawURLpathname', async () => {
  const config = (await import('./vite.config.ts')).default;
  const { fileURLToPath } = await import('node:url');
  assert.equal(config.root, fileURLToPath(new URL('.', import.meta.url)));
  assert.equal(config.base, '/');
  assert.equal(config.build.outDir, 'dist');
});
