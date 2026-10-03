import {
  MAX_COMMAND_BYTES,
  MAX_RESPONSE_BYTES,
  array,
  parseHistory,
  parseReceipt,
  parseVersion,
  parseWorkspace,
  parseWrapper,
  parseCorrection,
  reference,
  revision,
  strictJson,
  string,
  uuid,
  verifyVersion,
} from './contracts.ts';
import type {
  Command,
  Correction,
  History,
  Issue,
  Kind,
  Member,
  Origin,
  Outcome,
  Receipt,
  VerifiedVersion,
  Version,
  Workspace,
  Wrapper,
} from './contracts.ts';
export interface Client {
  workspace(signal: AbortSignal): Promise<Wrapper<Workspace>>;
  version(n: number, signal: AbortSignal): Promise<Wrapper<Version>>;
  history(id: string, after: number, signal: AbortSignal): Promise<Wrapper<History>>;
  apply(body: string, csrf: string, signal: AbortSignal): Promise<Wrapper<Receipt>>;
}
export class FetchClient implements Client {
  private async request<T>(
    route: string,
    operation: 'workspace' | 'version' | 'history' | 'apply',
    parse: (v: unknown) => T,
    signal: AbortSignal,
    body?: string,
    csrf?: string,
  ): Promise<Wrapper<T>> {
    const response = await fetch(`/local-evaluation/v1${route}`, {
      method: body === undefined ? 'GET' : 'POST',
      credentials: 'same-origin',
      cache: 'no-store',
      redirect: 'error',
      signal,
      headers:
        body === undefined
          ? { Accept: 'application/json' }
          : {
              Accept: 'application/json',
              'Content-Type': 'application/json',
              'X-CSRF-TOKEN': csrf ?? '',
            },
      ...(body === undefined ? {} : { body }),
    });
    if (
      !/^application\/json(?:\s*;\s*charset=utf-8)?$/i.test(
        response.headers.get('content-type') ?? '',
      ) ||
      !response.body
    )
      throw new Error('Unavailable');
    const length = response.headers.get('content-length');
    if (length !== null && (!/^\d+$/.test(length) || Number(length) > MAX_RESPONSE_BYTES))
      throw new Error('Unavailable');
    const reader = response.body.getReader();
    const decoder = new TextDecoder('utf-8', { fatal: true });
    let count = 0;
    let raw = '';
    try {
      while (true) {
        const part = await reader.read();
        if (part.done) break;
        count += part.value.byteLength;
        if (count > MAX_RESPONSE_BYTES) throw new Error('Unavailable');
        raw += decoder.decode(part.value, { stream: true });
      }
      raw += decoder.decode();
    } catch {
      await reader.cancel().catch(() => undefined);
      throw new Error('Unavailable');
    }
    const wrapper = parseWrapper(strictJson(raw), parse, operation);
    const status: Partial<Record<Issue, number>> = {
      InvalidInput: 400,
      Denied: 403,
      NotFound: 404,
      RevisionConflict: 409,
      RegistryConflict: 409,
      EventConflict: 409,
    };
    if (
      wrapper.issue === null
        ? response.status !== 200
        : response.status !== (status[wrapper.issue] ?? 503)
    )
      throw new Error('Unavailable');
    return wrapper;
  }
  workspace(signal: AbortSignal) {
    return this.request('/workspace', 'workspace', parseWorkspace, signal);
  }
  version(n: number, signal: AbortSignal) {
    revision(n);
    return this.request(`/versions/${n}`, 'version', parseVersion, signal);
  }
  history(id: string, after: number, signal: AbortSignal) {
    reference(id);
    revision(after);
    return this.request(
      `/members/${encodeURIComponent(id)}/history?afterSequence=${after}`,
      'history',
      parseHistory,
      signal,
    );
  }
  apply(body: string, csrf: string, signal: AbortSignal) {
    return this.request('/events', 'apply', parseReceipt, signal, body, csrf);
  }
}
export type Draft = {
  kind: Kind;
  outcome: Exclude<Outcome, 'Unreviewed'>;
  origin: Origin;
  reason: string;
  evidenceReferenceIds: string[];
  correction: Correction;
};
export type Prepared = {
  readonly command: Readonly<Command>;
  readonly body: string;
  readonly csrf: string;
  readonly memberId: string;
  readonly sourceDigest: string;
  readonly epoch: number;
};
export function prepare(
  w: Workspace,
  m: Member,
  d: Draft,
  csrf: string,
  eventId: string,
  epoch: number,
): Prepared {
  const isReview = d.kind === 'Review';
  if (isReview ? !m.canReview : !m.canCorrectPresentation)
    throw new Error('Review unavailable under the current assignment.');
  const reason = string(d.reason);
  const refs = array(d.evidenceReferenceIds, 16, reference).sort();
  if (refs.some((r, i) => r === refs[i - 1] || !m.original.evidenceReferenceIds.includes(r)))
    throw new Error('Choose permitted evidence references only.');
  const correction =
    d.kind === 'PresentationCorrection' || d.outcome === 'Corrected'
      ? parseCorrection(d.correction)
      : null;
  if (correction === null && (d.kind === 'PresentationCorrection' || d.outcome === 'Corrected'))
    throw new Error('Choose at least one correction dimension.');
  if (isReview && (d.outcome === 'Indeterminate') === m.authorizedContextSufficient)
    throw new Error('Outcome is unavailable for the current permitted context.');
  if (isReview && d.outcome !== 'Indeterminate' && refs.length === 0)
    throw new Error('Choose at least one permitted evidence reference.');
  const command: Command = {
    eventId: uuid(eventId),
    memberId: m.original.memberId,
    kind: d.kind,
    expectedAggregateRevision: w.aggregateRevision,
    expectedMemberRevision: m.revision,
    expectedRegistryVersionId: w.registryVersionId,
    expectedSourceDigest: w.sourceDigest,
    expectedSampleDigest: w.sampleDigest,
    outcome: isReview ? d.outcome : null,
    originatingClassification: isReview && d.outcome === 'Corrected' ? d.origin : null,
    reason,
    evidenceReferenceIds: refs,
    correction,
  };
  const body = JSON.stringify(command);
  if (new TextEncoder().encode(body).length > MAX_COMMAND_BYTES)
    throw new Error('Review exceeds the local request bound.');
  Object.freeze(refs);
  if (correction) Object.freeze(correction);
  Object.freeze(command);
  return Object.freeze({
    command,
    body,
    csrf: string(csrf, 4096),
    memberId: m.original.memberId,
    sourceDigest: w.sourceDigest,
    epoch,
  });
}
export type State = {
  mode: 'loading' | 'ready' | 'sending' | 'unknown' | 'conflict' | 'unavailable';
  message: string;
  issue: Issue | null;
  workspace: Workspace | null;
  version: VerifiedVersion | null;
  memberId: string | null;
  csrf: string | null;
  prepared: Prepared | null;
  history: History | null;
  historyLoading: boolean;
  receipt: Receipt | null;
};
const empty = (mode: State['mode'], message: string, issue: Issue | null = null): State => ({
  mode,
  message,
  issue,
  workspace: null,
  version: null,
  memberId: null,
  csrf: null,
  prepared: null,
  history: null,
  historyLoading: false,
  receipt: null,
});
export class Workflow {
  state: State = empty('loading', 'Loading the current workspace…');
  private listeners = new Set<() => void>();
  private epoch = 0;
  private historyEpoch = 0;
  private readAbort = new AbortController();
  private historyAbort = new AbortController();
  private applyAbort = new AbortController();
  private destroyed = false;
  private client: Client;
  constructor(client: Client = new FetchClient()) {
    this.client = client;
  }
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  snapshot = () => this.state;
  private set(state: State) {
    if (this.destroyed) return;
    this.state = state;
    this.listeners.forEach((l) => l());
  }
  private invalidate() {
    this.epoch++;
    this.historyEpoch++;
    this.readAbort.abort();
    this.historyAbort.abort();
    this.readAbort = new AbortController();
    this.historyAbort = new AbortController();
  }
  private fail(issue: Issue | null = null) {
    this.invalidate();
    this.set(
      empty(
        'unavailable',
        issue === 'Denied'
          ? 'Review unavailable under the current assignment.'
          : 'Workspace unavailable. Refresh before reviewing.',
        issue,
      ),
    );
  }
  async refresh() {
    if (this.state.mode === 'sending') return;
    this.invalidate();
    const epoch = this.epoch;
    this.set(empty('loading', 'Loading the current workspace…'));
    try {
      const result = await this.client.workspace(this.readAbort.signal);
      if (epoch !== this.epoch) return;
      if (result.issue !== null || !result.payload || !result.csrfToken) {
        this.fail(result.issue);
        return;
      }
      const w = result.payload;
      const response = await this.client.version(w.aggregateRevision, this.readAbort.signal);
      if (epoch !== this.epoch) return;
      if (response.issue !== null || !response.payload) {
        this.fail(response.issue);
        return;
      }
      const version = await verifyVersion(response.payload, w);
      if (epoch !== this.epoch) return;
      this.set({
        ...empty('ready', 'Current workspace loaded'),
        workspace: w,
        version,
        memberId: w.members[0]?.original.memberId ?? null,
        csrf: result.csrfToken,
      });
    } catch {
      if (epoch === this.epoch) this.fail();
    }
  }
  selectMember(id: string) {
    if (this.state.mode === 'sending' || this.state.mode === 'unknown') return;
    const w = this.state.workspace;
    if (!w?.members.some((m) => m.original.memberId === id)) return;
    this.historyEpoch++;
    this.historyAbort.abort();
    this.historyAbort = new AbortController();
    this.epoch++;
    this.set({
      ...this.state,
      memberId: id,
      prepared: null,
      history: null,
      historyLoading: false,
      message: 'Member selected.',
    });
  }
  async selectVersion(n: number) {
    if (this.state.mode === 'sending' || this.state.mode === 'unknown') return;
    const w = this.state.workspace;
    if (!w?.versions.some((v) => v.version === n)) return;
    this.invalidate();
    const epoch = this.epoch;
    this.set({
      ...this.state,
      mode: 'loading',
      version: null,
      prepared: null,
      history: null,
      historyLoading: false,
      message: 'Checking current permission for this immutable version…',
    });
    try {
      const current = await this.client.workspace(this.readAbort.signal);
      if (epoch !== this.epoch) return;
      if (current.issue !== null || !current.payload || !current.csrfToken) {
        this.fail(current.issue);
        return;
      }
      const fresh = current.payload;
      if (fresh.sourceDigest !== w.sourceDigest) throw new Error('Unavailable');
      const response = await this.client.version(n, this.readAbort.signal);
      if (epoch !== this.epoch) return;
      if (response.issue !== null || !response.payload) {
        this.fail(response.issue);
        return;
      }
      const version = await verifyVersion(response.payload, fresh);
      if (epoch !== this.epoch) return;
      this.set({
        ...this.state,
        mode: 'ready',
        workspace: fresh,
        csrf: current.csrfToken,
        version,
        message:
          n === fresh.aggregateRevision
            ? 'Current workspace loaded'
            : 'Historical outcome version — review actions are unavailable.',
      });
    } catch {
      if (epoch === this.epoch) this.fail();
    }
  }
  stage(draft: Draft) {
    const { workspace: w, memberId, csrf, version } = this.state;
    if (
      this.state.mode !== 'ready' ||
      !w ||
      !version ||
      version.dto.version !== w.aggregateRevision ||
      !csrf
    )
      return;
    const m = w.members.find((m) => m.original.memberId === memberId);
    if (!m) return;
    try {
      const prepared = prepare(w, m, draft, csrf, crypto.randomUUID(), this.epoch);
      this.set({
        ...this.state,
        prepared,
        message: 'Prepared review — inspect this summary before recording.',
      });
    } catch (error) {
      this.set({
        ...this.state,
        prepared: null,
        message:
          error instanceof Error && error.message !== 'Invalid workspace response'
            ? error.message
            : 'Check the outcome, rationale, references and correction fields.',
      });
    }
  }
  clearPrepared() {
    if (this.state.mode !== 'ready') return;
    this.set({ ...this.state, prepared: null, message: 'Prepared review cleared.' });
  }
  async record() {
    const p = this.state.prepared;
    if (
      !p ||
      !['ready', 'unknown'].includes(this.state.mode) ||
      p.epoch !== this.epoch ||
      p.memberId !== this.state.memberId ||
      p.sourceDigest !== this.state.workspace?.sourceDigest
    )
      return;
    const epoch = this.epoch;
    this.set({ ...this.state, mode: 'sending', message: 'Recording this exact prepared review…' });
    try {
      const response = await this.client.apply(p.body, p.csrf, this.applyAbort.signal);
      if (epoch !== this.epoch) return;
      if (response.issue !== null || !response.payload) {
        if (response.issue === 'Denied') {
          this.fail('Denied');
          return;
        }
        const issue = response.issue;
        this.invalidate();
        this.set(
          empty(
            ['RevisionConflict', 'RegistryConflict', 'SourceConflict', 'EventConflict'].includes(
              issue ?? '',
            )
              ? 'conflict'
              : 'unavailable',
            'Workspace changed. Refresh and prepare a new explicit review.',
            issue,
          ),
        );
        return;
      }
      const r = response.payload;
      if (
        r.eventId !== p.command.eventId ||
        r.memberId !== p.memberId ||
        r.actorId !== this.state.workspace?.actorId ||
        (!response.alreadyApplied &&
          (r.aggregateRevision !== p.command.expectedAggregateRevision + 1 ||
            r.memberRevision !== p.command.expectedMemberRevision + 1))
      )
        throw new Error('Unavailable');
      await this.refreshAfterRecord(r);
    } catch {
      if (epoch === this.epoch)
        this.set({
          ...this.state,
          mode: 'unknown',
          message:
            'Result unknown. Refresh to inspect the current version, or retry this exact prepared review.',
        });
    }
  }
  private async refreshAfterRecord(receipt: Receipt) {
    this.set({ ...this.state, mode: 'ready', prepared: null });
    await this.refresh();
    if (this.state.mode === 'ready')
      this.set({
        ...this.state,
        message: `Review recorded in outcome version ${receipt.version}.`,
        receipt,
      });
  }
  async loadHistory(more = false) {
    const { workspace: w, memberId, history } = this.state;
    if (this.state.mode !== 'ready' || !w || !memberId || this.state.historyLoading) return;
    const after = more ? (history?.nextAfterSequence ?? null) : 0;
    if (after === null) return;
    const epoch = this.epoch;
    const hEpoch = ++this.historyEpoch;
    this.historyAbort.abort();
    this.historyAbort = new AbortController();
    const priorVersion = this.state.version;
    this.set({
      ...this.state,
      mode: 'loading',
      version: null,
      prepared: null,
      historyLoading: true,
      history: null,
    });
    try {
      const current = await this.client.workspace(this.historyAbort.signal);
      if (epoch !== this.epoch || hEpoch !== this.historyEpoch) return;
      if (current.issue !== null || !current.payload) {
        this.fail(current.issue);
        return;
      }
      if (
        current.payload.sourceDigest !== w.sourceDigest ||
        current.payload.aggregateRevision !== w.aggregateRevision ||
        current.payload.registryVersionId !== w.registryVersionId
      )
        throw new Error('Unavailable');
      const result = await this.client.history(memberId, after, this.historyAbort.signal);
      if (epoch !== this.epoch || hEpoch !== this.historyEpoch) return;
      if (result.issue !== null || !result.payload) {
        this.fail(result.issue);
        return;
      }
      const h = result.payload;
      if (
        h.memberId !== memberId ||
        h.aggregateRevision !== w.aggregateRevision ||
        h.registryVersionId !== w.registryVersionId ||
        h.memberRevision !== w.members.find((m) => m.original.memberId === memberId)?.revision ||
        h.events.some((e) => e.sequence <= after)
      )
        throw new Error('Unavailable');
      this.set({
        ...this.state,
        mode: 'ready',
        version: priorVersion,
        csrf: current.csrfToken,
        historyLoading: false,
        history: { ...h, events: more ? [...(history?.events ?? []), ...h.events] : h.events },
      });
    } catch {
      if (epoch === this.epoch && hEpoch === this.historyEpoch) this.fail();
    }
  }
  destroy() {
    this.invalidate();
    this.applyAbort.abort();
    this.destroyed = true;
    this.listeners.clear();
  }
}
