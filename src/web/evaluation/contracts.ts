export const POPULATION_DIGEST = '069dd6f848250dfa46be775b382d563f7979e15e6349a29f59d4770a55955aa1';
export const SAMPLE_DIGEST = '5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110';
export const MAX_RESPONSE_BYTES = 16 * 1024 * 1024;
export const MAX_COMMAND_BYTES = 256 * 1024;
export const issues = [
  'InvalidInput',
  'Denied',
  'NotFound',
  'RevisionConflict',
  'RegistryConflict',
  'SourceConflict',
  'EventConflict',
  'IntegrityMismatch',
  'MigrationDrift',
  'NotInitialized',
  'SeedConflict',
  'RevisionOverflow',
  'ClockConflict',
  'WorkflowLimit',
  'Unavailable',
] as const;
export type Issue = (typeof issues)[number];
export type Outcome = 'Confirmed' | 'Rejected' | 'Indeterminate' | 'Unreviewed' | 'Corrected';
export type Origin = 'Confirmed' | 'Rejected';
export type Kind = 'Review' | 'PresentationCorrection';
export type Correction = {
  severity: string | null;
  category: string | null;
  rootCause: string | null;
  recommendation: string | null;
};
export type Original = {
  memberId: string;
  title: string;
  severity: string;
  category: string;
  rootCause: string;
  recommendation: string;
  evidenceReferenceIds: string[];
};
export type Metadata = {
  memberId: string;
  originSeverity: 'Critical' | 'High' | 'Medium' | 'Low' | 'Informational';
  environmentId: string;
  moduleId: string;
  categoryId: string;
  ruleId: string;
  modelPromptId: string;
  confidenceBandId: string;
};
export type Member = {
  original: Original;
  originMetadata: Metadata;
  assignmentId: string;
  revision: number;
  outcome: Outcome;
  originatingClassification: Origin | null;
  canReview: boolean;
  canCorrectPresentation: boolean;
  authorizedContextSufficient: boolean;
  currentCorrection: Correction | null;
};
export type Summary = {
  version: number;
  correctionCutoffUtc: string;
  lastEventSequence: number;
  registryVersionId: string;
  contentDigest: string;
  predecessorDigest: string | null;
};
export type Workspace = {
  schemaVersion: string;
  actorId: string;
  aggregateRevision: number;
  registryVersionId: string;
  sourceDigest: string;
  populationDigest: string;
  sampleDigest: string;
  sampleCorrectionCutoffUtc: string;
  members: Member[];
  versions: Summary[];
};
export type Version = {
  version: number;
  correctionCutoffUtc: string;
  lastEventSequence: number;
  registryVersionId: string;
  registryDigest: string;
  versionManifestJson: string;
  versionManifestDigest: string;
  predecessorDigest: string | null;
  accuracyCanonicalJson: string;
  accuracyDigest: string;
  warningCanonicalJson: string;
  warningDigest: string;
  snapshotCanonicalJson: string;
  contentDigest: string;
};
export type Command = {
  eventId: string;
  memberId: string;
  kind: Kind;
  expectedAggregateRevision: number;
  expectedMemberRevision: number;
  expectedRegistryVersionId: string;
  expectedSourceDigest: string;
  expectedSampleDigest: string;
  outcome: Exclude<Outcome, 'Unreviewed'> | null;
  originatingClassification: Origin | null;
  reason: string;
  evidenceReferenceIds: string[];
  correction: Correction | null;
};
export type Receipt = {
  eventId: string;
  memberId: string;
  actorId: string;
  aggregateRevision: number;
  memberRevision: number;
  version: number;
  snapshotDigest: string;
  recordedAtUtc: string;
};
export type Event = {
  eventId: string;
  sequence: number;
  aggregateRevision: number;
  memberRevision: number;
  memberId: string;
  kind: Kind;
  actorId: string;
  assignmentId: string;
  registryVersionId: string;
  registryDigest: string;
  recordedAtUtc: string;
  recordedOutcome: Outcome;
  originatingClassification: Origin | null;
  reason: string;
  evidenceReferenceIds: string[];
  correction: Correction | null;
  commandDigest: string;
  previousEventDigest: string;
  contentDigest: string;
};
export type History = {
  memberId: string;
  aggregateRevision: number;
  memberRevision: number;
  registryVersionId: string;
  events: Event[];
  nextAfterSequence: number | null;
};
export type Counts = {
  selected: number;
  confirmed: number;
  rejected: number;
  indeterminate: number;
  unreviewed: number;
  corrected: number;
  denominator: number;
  lowSampleWarning: boolean;
};
export type Warning = {
  schema: string;
  accuracyDigest: string;
  metadata: Metadata[];
  summaries: { generalAi: Counts; desiredOutcome: Counts };
  breakdowns: { dimension: string; key: string; summary: Counts }[];
};
export type VerifiedVersion = {
  dto: Version;
  counts: Counts;
  desired: Counts;
  breakdowns: Warning['breakdowns'];
  reviews: { memberId: string; outcome: Outcome; originatingClassification: Origin | null }[];
  manifest: Record<string, unknown>;
};
export type Wrapper<T> = {
  schemaVersion: 1;
  demoOnly: true;
  csrfToken: string | null;
  issue: Issue | null;
  payload: T | null;
  alreadyApplied: boolean | null;
};
const outcomes = ['Confirmed', 'Rejected', 'Indeterminate', 'Unreviewed', 'Corrected'] as const;
const origins = ['Confirmed', 'Rejected'] as const;
const kinds = ['Review', 'PresentationCorrection'] as const;
export function reject(): never {
  throw new Error('Invalid workspace response');
}
export function object(value: unknown, keys: string[]): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return reject();
  const o = value as Record<string, unknown>;
  const actual = Object.keys(o);
  if (actual.length !== keys.length || actual.some((k) => !keys.includes(k))) return reject();
  return o;
}
export function string(value: unknown, max = 2000): string {
  if (typeof value !== 'string' || value.length > max || !value.trim() || value.includes('\0'))
    return reject();
  return value;
}
export function reference(value: unknown): string {
  const s = string(value, 128);
  if (!/^synthetic-[a-z0-9._-]{1,118}$/.test(s)) return reject();
  return s;
}
export function digest(value: unknown): string {
  const s = string(value, 64);
  if (!/^[0-9a-f]{64}$/.test(s)) return reject();
  return s;
}
export function revision(value: unknown): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) return reject();
  return value;
}
function boolean(value: unknown): boolean {
  if (typeof value !== 'boolean') return reject();
  return value;
}
export function utc(value: unknown): string {
  const s = string(value, 40);
  if (
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:\+00:00|Z)$/.test(s) ||
    !Number.isFinite(Date.parse(s))
  )
    return reject();
  return s;
}
export function uuid(value: unknown): string {
  const s = string(value, 36);
  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(s) ||
    s === '00000000-0000-0000-0000-000000000000'
  )
    return reject();
  return s;
}
function enumeration<T extends string>(value: unknown, values: readonly T[]): T {
  if (typeof value !== 'string' || !values.includes(value as T)) return reject();
  return value as T;
}
function nullable<T>(value: unknown, parse: (value: unknown) => T): T | null {
  return value === null ? null : parse(value);
}
export function array<T>(value: unknown, max: number, parse: (value: unknown) => T): T[] {
  if (!Array.isArray(value) || value.length > max) return reject();
  return value.map(parse);
}
function ordered(values: string[]): string[] {
  if (values.some((v, i) => i > 0 && v <= (values[i - 1] ?? ''))) return reject();
  return values;
}
function refs(value: unknown): string[] {
  return ordered(array(value, 16, reference));
}
export function parseCorrection(value: unknown): Correction | null {
  if (value === null) return null;
  const o = object(value, ['severity', 'category', 'rootCause', 'recommendation']);
  const c = {
    severity: nullable(o.severity, string),
    category: nullable(o.category, string),
    rootCause: nullable(o.rootCause, string),
    recommendation: nullable(o.recommendation, string),
  };
  if (Object.values(c).every((v) => v === null)) return reject();
  return c;
}
export function parseMetadata(value: unknown): Metadata {
  const o = object(value, [
    'memberId',
    'originSeverity',
    'environmentId',
    'moduleId',
    'categoryId',
    'ruleId',
    'modelPromptId',
    'confidenceBandId',
  ]);
  return {
    memberId: reference(o.memberId),
    originSeverity: enumeration(o.originSeverity, [
      'Critical',
      'High',
      'Medium',
      'Low',
      'Informational',
    ]),
    environmentId: reference(o.environmentId),
    moduleId: reference(o.moduleId),
    categoryId: reference(o.categoryId),
    ruleId: reference(o.ruleId),
    modelPromptId: reference(o.modelPromptId),
    confidenceBandId: reference(o.confidenceBandId),
  };
}
function classification(outcome: Outcome, origin: Origin | null): void {
  if ((outcome === 'Corrected') !== (origin !== null)) reject();
}
export function parseMember(value: unknown): Member {
  const o = object(value, [
    'original',
    'originMetadata',
    'assignmentId',
    'revision',
    'outcome',
    'originatingClassification',
    'canReview',
    'canCorrectPresentation',
    'authorizedContextSufficient',
    'currentCorrection',
  ]);
  const x = object(o.original, [
    'memberId',
    'title',
    'severity',
    'category',
    'rootCause',
    'recommendation',
    'evidenceReferenceIds',
  ]);
  const original = {
    memberId: reference(x.memberId),
    title: string(x.title),
    severity: string(x.severity),
    category: string(x.category),
    rootCause: string(x.rootCause),
    recommendation: string(x.recommendation),
    evidenceReferenceIds: refs(x.evidenceReferenceIds),
  };
  const m = {
    original,
    originMetadata: parseMetadata(o.originMetadata),
    assignmentId: reference(o.assignmentId),
    revision: revision(o.revision),
    outcome: enumeration(o.outcome, outcomes),
    originatingClassification: nullable(o.originatingClassification, (v) =>
      enumeration(v, origins),
    ),
    canReview: boolean(o.canReview),
    canCorrectPresentation: boolean(o.canCorrectPresentation),
    authorizedContextSufficient: boolean(o.authorizedContextSufficient),
    currentCorrection: parseCorrection(o.currentCorrection),
  };
  classification(m.outcome, m.originatingClassification);
  if (
    m.original.memberId !== m.originMetadata.memberId ||
    (!m.canReview && m.authorizedContextSufficient)
  )
    reject();
  return m;
}
function parseSummary(value: unknown): Summary {
  const o = object(value, [
    'version',
    'correctionCutoffUtc',
    'lastEventSequence',
    'registryVersionId',
    'contentDigest',
    'predecessorDigest',
  ]);
  return {
    version: revision(o.version),
    correctionCutoffUtc: utc(o.correctionCutoffUtc),
    lastEventSequence: revision(o.lastEventSequence),
    registryVersionId: reference(o.registryVersionId),
    contentDigest: digest(o.contentDigest),
    predecessorDigest: nullable(o.predecessorDigest, digest),
  };
}
export function parseWorkspace(value: unknown): Workspace {
  const o = object(value, [
    'schemaVersion',
    'actorId',
    'aggregateRevision',
    'registryVersionId',
    'sourceDigest',
    'populationDigest',
    'sampleDigest',
    'sampleCorrectionCutoffUtc',
    'members',
    'versions',
  ]);
  const w = {
    schemaVersion: string(o.schemaVersion, 64),
    actorId: reference(o.actorId),
    aggregateRevision: revision(o.aggregateRevision),
    registryVersionId: reference(o.registryVersionId),
    sourceDigest: digest(o.sourceDigest),
    populationDigest: digest(o.populationDigest),
    sampleDigest: digest(o.sampleDigest),
    sampleCorrectionCutoffUtc: utc(o.sampleCorrectionCutoffUtc),
    members: array(o.members, 100, parseMember),
    versions: array(o.versions, 1001, parseSummary),
  };
  if (
    w.schemaVersion !== 'synthetic-evaluation-workflow-v1' ||
    w.actorId !== 'synthetic-reviewer' ||
    w.members.length !== 100 ||
    w.populationDigest !== POPULATION_DIGEST ||
    w.sampleDigest !== SAMPLE_DIGEST ||
    w.versions.length !== w.aggregateRevision + 1 ||
    w.aggregateRevision > 1000
  )
    reject();
  ordered(w.members.map((m) => m.original.memberId));
  w.versions.forEach((v, i) => {
    if (
      v.version !== i ||
      v.lastEventSequence > i ||
      v.predecessorDigest !== (i === 0 ? null : w.versions[i - 1]?.contentDigest)
    )
      reject();
    if (
      i > 0 &&
      Date.parse(v.correctionCutoffUtc) < Date.parse(w.versions[i - 1]?.correctionCutoffUtc ?? '')
    )
      reject();
  });
  if (w.versions.at(-1)?.registryVersionId !== w.registryVersionId) reject();
  return w;
}
export function parseVersion(value: unknown): Version {
  const o = object(value, [
    'version',
    'correctionCutoffUtc',
    'lastEventSequence',
    'registryVersionId',
    'registryDigest',
    'versionManifestJson',
    'versionManifestDigest',
    'predecessorDigest',
    'accuracyCanonicalJson',
    'accuracyDigest',
    'warningCanonicalJson',
    'warningDigest',
    'snapshotCanonicalJson',
    'contentDigest',
  ]);
  return {
    version: revision(o.version),
    correctionCutoffUtc: utc(o.correctionCutoffUtc),
    lastEventSequence: revision(o.lastEventSequence),
    registryVersionId: reference(o.registryVersionId),
    registryDigest: digest(o.registryDigest),
    versionManifestJson: string(o.versionManifestJson, MAX_RESPONSE_BYTES),
    versionManifestDigest: digest(o.versionManifestDigest),
    predecessorDigest: nullable(o.predecessorDigest, digest),
    accuracyCanonicalJson: string(o.accuracyCanonicalJson, MAX_RESPONSE_BYTES),
    accuracyDigest: digest(o.accuracyDigest),
    warningCanonicalJson: string(o.warningCanonicalJson, MAX_RESPONSE_BYTES),
    warningDigest: digest(o.warningDigest),
    snapshotCanonicalJson: string(o.snapshotCanonicalJson, MAX_RESPONSE_BYTES),
    contentDigest: digest(o.contentDigest),
  };
}
export function parseReceipt(value: unknown): Receipt {
  const o = object(value, [
    'eventId',
    'memberId',
    'actorId',
    'aggregateRevision',
    'memberRevision',
    'version',
    'snapshotDigest',
    'recordedAtUtc',
  ]);
  const r = {
    eventId: uuid(o.eventId),
    memberId: reference(o.memberId),
    actorId: reference(o.actorId),
    aggregateRevision: revision(o.aggregateRevision),
    memberRevision: revision(o.memberRevision),
    version: revision(o.version),
    snapshotDigest: digest(o.snapshotDigest),
    recordedAtUtc: utc(o.recordedAtUtc),
  };
  if (
    r.version !== r.aggregateRevision ||
    r.memberRevision > r.aggregateRevision ||
    r.actorId !== 'synthetic-reviewer'
  )
    reject();
  return r;
}
function parseEvent(value: unknown): Event {
  const o = object(value, [
    'eventId',
    'sequence',
    'aggregateRevision',
    'memberRevision',
    'memberId',
    'kind',
    'actorId',
    'assignmentId',
    'registryVersionId',
    'registryDigest',
    'recordedAtUtc',
    'recordedOutcome',
    'originatingClassification',
    'reason',
    'evidenceReferenceIds',
    'correction',
    'commandDigest',
    'previousEventDigest',
    'contentDigest',
  ]);
  const e = {
    eventId: uuid(o.eventId),
    sequence: revision(o.sequence),
    aggregateRevision: revision(o.aggregateRevision),
    memberRevision: revision(o.memberRevision),
    memberId: reference(o.memberId),
    kind: enumeration(o.kind, kinds),
    actorId: reference(o.actorId),
    assignmentId: reference(o.assignmentId),
    registryVersionId: reference(o.registryVersionId),
    registryDigest: digest(o.registryDigest),
    recordedAtUtc: utc(o.recordedAtUtc),
    recordedOutcome: enumeration(o.recordedOutcome, outcomes),
    originatingClassification: nullable(o.originatingClassification, (v) =>
      enumeration(v, origins),
    ),
    reason: string(o.reason),
    evidenceReferenceIds: refs(o.evidenceReferenceIds),
    correction: parseCorrection(o.correction),
    commandDigest: digest(o.commandDigest),
    previousEventDigest: digest(o.previousEventDigest),
    contentDigest: digest(o.contentDigest),
  };
  classification(e.recordedOutcome, e.originatingClassification);
  if (
    e.sequence < 1 ||
    e.aggregateRevision < e.sequence ||
    e.memberRevision < 1 ||
    e.memberRevision > e.sequence
  )
    reject();
  return e;
}
export function parseHistory(value: unknown): History {
  const o = object(value, [
    'memberId',
    'aggregateRevision',
    'memberRevision',
    'registryVersionId',
    'events',
    'nextAfterSequence',
  ]);
  const h = {
    memberId: reference(o.memberId),
    aggregateRevision: revision(o.aggregateRevision),
    memberRevision: revision(o.memberRevision),
    registryVersionId: reference(o.registryVersionId),
    events: array(o.events, 50, parseEvent),
    nextAfterSequence: nullable(o.nextAfterSequence, revision),
  };
  h.events.forEach((e, i) => {
    if (
      e.memberId !== h.memberId ||
      e.aggregateRevision > h.aggregateRevision ||
      e.memberRevision > h.memberRevision ||
      (i > 0 && e.sequence <= (h.events[i - 1]?.sequence ?? 0))
    )
      reject();
  });
  if (
    h.nextAfterSequence !== null &&
    (h.events.length !== 50 || h.nextAfterSequence !== h.events.at(-1)?.sequence)
  )
    reject();
  return h;
}
export function parseWrapper<T>(
  value: unknown,
  parse: (value: unknown) => T,
  operation: 'workspace' | 'version' | 'history' | 'apply',
): Wrapper<T> {
  const o = object(value, [
    'schemaVersion',
    'demoOnly',
    'csrfToken',
    'issue',
    'payload',
    'alreadyApplied',
  ]);
  if (o.schemaVersion !== 1 || o.demoOnly !== true) reject();
  const issue = nullable(o.issue, (v) => enumeration(v, issues));
  if (issue !== null) {
    if (o.payload !== null || o.csrfToken !== null || o.alreadyApplied !== null) reject();
    return {
      schemaVersion: 1,
      demoOnly: true,
      csrfToken: null,
      issue,
      payload: null,
      alreadyApplied: null,
    };
  }
  if (o.payload === null) reject();
  const csrfToken = operation === 'workspace' ? string(o.csrfToken, 4096) : null;
  if (operation !== 'workspace' && o.csrfToken !== null) reject();
  const alreadyApplied = operation === 'apply' ? boolean(o.alreadyApplied) : null;
  if (operation !== 'apply' && o.alreadyApplied !== null) reject();
  return {
    schemaVersion: 1,
    demoOnly: true,
    csrfToken,
    issue: null,
    payload: parse(o.payload),
    alreadyApplied,
  };
}
// Native JSON.parse accepts duplicate keys. Scan grammar independently before parsing.
export function strictJson(text: string): unknown {
  if (new TextEncoder().encode(text).length > MAX_RESPONSE_BYTES) reject();
  let i = 0;
  const ws = () => {
    while (/\s/.test(text[i] ?? '') && i < text.length) i++;
  };
  const str = (): string => {
    const start = i;
    if (text[i++] !== '"') reject();
    while (i < text.length) {
      const c = text[i++];
      if (c === '"') {
        try {
          return JSON.parse(text.slice(start, i)) as string;
        } catch {
          return reject();
        }
      }
      if (c === '\\') i++;
    }
    return reject();
  };
  const read = (depth: number): void => {
    if (depth > 32) reject();
    ws();
    const c = text[i];
    if (c === '"') {
      str();
      return;
    }
    if (c === '{') {
      i++;
      ws();
      const seen = new Set<string>();
      if (text[i] === '}') {
        i++;
        return;
      }
      while (i < text.length) {
        ws();
        const k = str();
        if (seen.has(k)) reject();
        seen.add(k);
        ws();
        if (text[i++] !== ':') reject();
        read(depth + 1);
        ws();
        if (text[i] === '}') {
          i++;
          return;
        }
        if (text[i++] !== ',') reject();
      }
      reject();
    }
    if (c === '[') {
      i++;
      ws();
      if (text[i] === ']') {
        i++;
        return;
      }
      while (i < text.length) {
        read(depth + 1);
        ws();
        if (text[i] === ']') {
          i++;
          return;
        }
        if (text[i++] !== ',') reject();
      }
      reject();
    }
    const token = /^(?:null|true|false|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)/.exec(
      text.slice(i),
    );
    if (!token) reject();
    i += token[0].length;
  };
  read(0);
  ws();
  if (i !== text.length) reject();
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return reject();
  }
}
export async function sha256(raw: string): Promise<string> {
  const bytes = new TextEncoder().encode(raw);
  const hash = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(hash), (b) => b.toString(16).padStart(2, '0')).join('');
}
function counts(value: unknown): Counts {
  const o = object(value, [
    'selected',
    'confirmed',
    'rejected',
    'indeterminate',
    'unreviewed',
    'corrected',
    'denominator',
    'lowSampleWarning',
  ]);
  const c = {
    selected: revision(o.selected),
    confirmed: revision(o.confirmed),
    rejected: revision(o.rejected),
    indeterminate: revision(o.indeterminate),
    unreviewed: revision(o.unreviewed),
    corrected: revision(o.corrected),
    denominator: revision(o.denominator),
    lowSampleWarning: boolean(o.lowSampleWarning),
  };
  if (
    c.selected > 100 ||
    c.denominator !== c.confirmed + c.rejected ||
    c.selected !== c.denominator + c.indeterminate + c.unreviewed ||
    c.corrected > c.denominator ||
    c.lowSampleWarning !== c.denominator < 30
  )
    reject();
  return c;
}
export async function verifyVersion(dto: Version, w: Workspace): Promise<VerifiedVersion> {
  const summary = w.versions.find((v) => v.version === dto.version);
  if (
    !summary ||
    (
      [
        'correctionCutoffUtc',
        'lastEventSequence',
        'registryVersionId',
        'contentDigest',
        'predecessorDigest',
      ] as const
    ).some((k) => summary[k] !== dto[k])
  )
    reject();
  for (const [raw, expected] of [
    [dto.versionManifestJson, dto.versionManifestDigest],
    [dto.accuracyCanonicalJson, dto.accuracyDigest],
    [dto.warningCanonicalJson, dto.warningDigest],
    [dto.snapshotCanonicalJson, dto.contentDigest],
  ])
    if ((await sha256(raw ?? '')) !== expected) reject();
  const snapshot = object(strictJson(dto.snapshotCanonicalJson), [
    'schemaVersion',
    'versionManifestJson',
    'accuracyCanonicalJson',
    'warningCanonicalJson',
  ]);
  if (
    snapshot.schemaVersion !== 'synthetic-evaluation-workflow-snapshot-v1' ||
    snapshot.versionManifestJson !== dto.versionManifestJson ||
    snapshot.accuracyCanonicalJson !== dto.accuracyCanonicalJson ||
    snapshot.warningCanonicalJson !== dto.warningCanonicalJson
  )
    reject();
  const manifest = object(strictJson(dto.versionManifestJson), [
    'schemaVersion',
    'version',
    'sourceDigest',
    'populationDigest',
    'sampleDigest',
    'originalSampleVersionManifestDigest',
    'originalSampleCorrectionCutoffUtc',
    'registryVersionId',
    'registryDigest',
    'lastEventSequence',
    'eventsDigest',
    'correctionCutoffUtc',
    'predecessorDigest',
  ]);
  if (
    manifest.schemaVersion !== 'synthetic-evaluation-workflow-version-v1' ||
    manifest.version !== dto.version ||
    manifest.sourceDigest !== w.sourceDigest ||
    manifest.populationDigest !== w.populationDigest ||
    manifest.sampleDigest !== w.sampleDigest ||
    manifest.originalSampleCorrectionCutoffUtc !== w.sampleCorrectionCutoffUtc ||
    manifest.registryVersionId !== dto.registryVersionId ||
    manifest.registryDigest !== dto.registryDigest ||
    manifest.lastEventSequence !== dto.lastEventSequence ||
    manifest.correctionCutoffUtc !== dto.correctionCutoffUtc ||
    manifest.predecessorDigest !== dto.predecessorDigest
  )
    reject();
  digest(manifest.originalSampleVersionManifestDigest);
  digest(manifest.eventsDigest);
  const accuracy = object(strictJson(dto.accuracyCanonicalJson), [
    'schema',
    'locks',
    'members',
    'reviews',
  ]);
  if (accuracy.schema !== 'synthetic-evaluation-accuracy-v1') reject();
  const locks = object(accuracy.locks, [
    'evaluationId',
    'scopeId',
    'populationDigest',
    'sampleDigest',
    'versionManifestDigest',
    'correctionCutoffUtc',
  ]);
  if (
    locks.evaluationId !== `synthetic-workflow-version-${dto.version}` ||
    locks.scopeId !== 'synthetic-scope' ||
    locks.populationDigest !== w.populationDigest ||
    locks.sampleDigest !== w.sampleDigest ||
    locks.versionManifestDigest !== dto.versionManifestDigest ||
    locks.correctionCutoffUtc !== dto.correctionCutoffUtc
  )
    reject();
  const members = array(accuracy.members, 100, (v) => {
    const m = object(v, ['id', 'track', 'desiredOutcomeVersion', 'desiredOutcomeApproval']);
    if (
      m.track !== 'GeneralAi' ||
      m.desiredOutcomeVersion !== null ||
      m.desiredOutcomeApproval !== null
    )
      reject();
    return reference(m.id);
  });
  if (members.length !== 100 || members.some((id, i) => id !== w.members[i]?.original.memberId))
    reject();
  const reviews = array(accuracy.reviews, 100, (v) => {
    const r = object(v, ['memberId', 'outcome', 'originatingClassification']);
    const x = {
      memberId: reference(r.memberId),
      outcome: enumeration(r.outcome, outcomes),
      originatingClassification: nullable(r.originatingClassification, (v) =>
        enumeration(v, origins),
      ),
    };
    classification(x.outcome, x.originatingClassification);
    return x;
  });
  if (
    reviews.length !== 100 ||
    reviews.some(
      (r, i) =>
        r.memberId !== members[i] ||
        (dto.version === w.aggregateRevision &&
          (r.outcome !== w.members[i]?.outcome ||
            r.originatingClassification !== w.members[i]?.originatingClassification)),
    )
  )
    reject();
  const warning = object(strictJson(dto.warningCanonicalJson), [
    'schema',
    'accuracyDigest',
    'metadata',
    'summaries',
    'breakdowns',
  ]);
  if (
    warning.schema !== 'synthetic-evaluation-warning-v1' ||
    warning.accuracyDigest !== dto.accuracyDigest
  )
    reject();
  const metadata = array(warning.metadata, 100, parseMetadata);
  if (
    metadata.length !== 100 ||
    metadata.some((m, i) => JSON.stringify(m) !== JSON.stringify(w.members[i]?.originMetadata))
  )
    reject();
  const summaries = object(warning.summaries, ['generalAi', 'desiredOutcome']);
  const general = counts(summaries.generalAi),
    desired = counts(summaries.desiredOutcome);
  if (general.selected !== 100 || desired.selected !== 0) reject();
  const dimensions = [
    'Environment',
    'Category',
    'Severity',
    'Module',
    'Rule',
    'ModelPrompt',
    'ConfidenceBand',
    'DesiredOutcomeVersion',
  ];
  const breakdowns = array(warning.breakdowns, 1000, (v) => {
    const b = object(v, ['dimension', 'key', 'summary']);
    return {
      dimension: enumeration(b.dimension, dimensions),
      key: string(b.key, 1000),
      summary: counts(b.summary),
    };
  });
  return { dto, counts: general, desired, breakdowns, reviews, manifest };
}
