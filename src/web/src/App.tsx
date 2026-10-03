import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import type {
  Catalog,
  RunDetail,
  RunHistory,
  RunState,
  Selection,
} from './demo-contract.generated';
import { DemoRequestError, request } from './api';
import { AnalysisView } from './AnalysisView';
import {
  Phase1BSetupController,
  Phase1BRunController,
  Phase1BProtectedNavigation,
} from './Phase1BController';
import type { ArtifactReviewDraft } from './ArtifactReviewPanel';
import type { PlanningTaskDraft } from './PlanningTasksPanel';

const states: Record<RunState, string> = {
  Planned: 'Planned',
  Running: 'Running',
  Scoring: 'Coverage ready · scoring pending',
  Cancelled: 'Cancelled',
  Failed: 'Failed',
};
const stateLabel = (state: RunState, profileId: string) =>
  state === 'Scoring' &&
  (profileId.startsWith('synthetic-analysis-') ||
    profileId.startsWith('synthetic-review-maturity-'))
    ? 'Coverage ready · local analysis'
    : states[state];
const storageKey = 'iga.synthetic.selected-run';
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const formatNumber = (value: number) => value.toLocaleString();
const formatTime = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? 'Time unavailable'
    : date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
};
const stateName = (state: string) => state.replace(/([a-z])([A-Z])/g, '$1 $2');
const selectionName = (selection: Selection) =>
  `${selection.baselineLabel} / ${selection.profileLabel}`;

function rememberedRun(): string | null {
  const queryRun = new URLSearchParams(window.location.search).get('run');
  if (queryRun && uuid.test(queryRun)) return queryRun;
  try {
    const value = sessionStorage.getItem(storageKey);
    return value && uuid.test(value) ? value : null;
  } catch {
    return null;
  }
}

export function App() {
  const query = new URLSearchParams(window.location.search);
  if (query.has('view') || query.has('task') || query.has('finding')) {
    const runId = query.get('run') ?? '',
      view = query.get('view') ?? '',
      id = query.get(view === 'tasks' ? 'task' : 'finding') ?? '';
    const expected = ['run', 'view', view === 'tasks' ? 'task' : 'finding'];
    if (
      !uuid.test(runId) ||
      !['tasks', 'findings'].includes(view) ||
      !/^[a-f0-9]{64}$/.test(id) ||
      [...query.keys()].length !== 3 ||
      expected.some((k) => query.getAll(k).length !== 1)
    )
      return (
        <main>
          <h1>Protected fictional planning reference</h1>
          <p role="alert">This protected link is unavailable.</p>
        </main>
      );
    return <Phase1BProtectedNavigation runId={runId} view={view} id={id} />;
  }
  return <ConsultantApp />;
}
function ConsultantApp() {
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [scopeId, setScopeId] = useState('');
  const [baselineId, setBaselineId] = useState('');
  const [profileId, setProfileId] = useState('');
  const [history, setHistory] = useState<RunHistory | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(rememberedRun);
  const [run, setRun] = useState<RunDetail | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ message: string; focus: boolean } | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const [connected, setConnected] = useState(false);
  const artifactDrafts = useRef<Record<string, ArtifactReviewDraft>>({});
  const planningTaskDrafts = useRef<Record<string, PlanningTaskDraft>>({});
  const errorRef = useRef<HTMLDivElement>(null);
  const detailRef = useRef<HTMLElement>(null);
  const currentSelection = useRef<string | null>(selectedId);
  const lastState = useRef('');
  const startKey = useRef<{ selection: string; id: string } | null>(null);
  currentSelection.current = selectedId;

  const baselines = catalog?.baselines.filter((item) => item.scopeId === scopeId) ?? [];
  const profiles = catalog?.profiles.filter((item) => item.baselineIds.includes(baselineId)) ?? [];
  const selectedBaseline = baselines.find((item) => item.id === baselineId);
  const canStart =
    !!catalog &&
    profileId !== 'synthetic-phase1b-combined-v1' &&
    baselines.some((item) => item.id === baselineId) &&
    profiles.some((item) => item.id === profileId);

  useEffect(() => {
    if (error?.focus) errorRef.current?.focus();
  }, [error]);

  useEffect(() => {
    try {
      if (selectedId) sessionStorage.setItem(storageKey, selectedId);
      else sessionStorage.removeItem(storageKey);
    } catch {
      /* Browser persistence is optional; the server owns run history. */
    }
  }, [selectedId]);

  useEffect(() => {
    const controller = new AbortController();
    async function initialize() {
      try {
        const [nextCatalog, nextHistory] = await Promise.all([
          request<Catalog>('/catalog', controller.signal),
          request<RunHistory>('/runs', controller.signal),
        ]);
        if (controller.signal.aborted) return;
        setCatalog(nextCatalog);
        setHistory(nextHistory);
        setConnected(true);
        const scope = nextCatalog.scopes[0]?.id ?? '';
        const baseline = nextCatalog.baselines.find((item) => item.scopeId === scope)?.id ?? '';
        setScopeId(scope);
        setBaselineId(baseline);
        setProfileId(
          nextCatalog.profiles.find((item) => item.baselineIds.includes(baseline))?.id ?? '',
        );
      } catch (caught) {
        if (!controller.signal.aborted) setError({ message: errorMessage(caught), focus: false });
      }
    }
    void initialize();
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!selectedId) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    let retryDelay = 1000;
    let needsBootstrap = false;
    async function poll() {
      try {
        const nextRun = await request<RunDetail>(
          `/runs/${encodeURIComponent(selectedId!)}`,
          controller.signal,
        );
        if (controller.signal.aborted || currentSelection.current !== nextRun.runId) return;
        setRun(nextRun);
        setHistory((previous) =>
          previous
            ? {
                ...previous,
                runs: previous.runs.map((item) => (item.runId === nextRun.runId ? nextRun : item)),
              }
            : previous,
        );
        if (needsBootstrap) {
          const nextCatalog = await request<Catalog>('/catalog', controller.signal);
          if (controller.signal.aborted) return;
          setCatalog(nextCatalog);
          needsBootstrap = false;
        }
        setConnected(true);
        setError((previous) => (previous?.focus ? previous : null));
        const nextState = `${nextRun.runId}:${nextRun.state}:${nextRun.cancelRequested}`;
        if (lastState.current !== nextState) {
          lastState.current = nextState;
          setAnnouncement(
            nextRun.cancelRequested && nextRun.state !== 'Cancelled'
              ? 'Cancellation requested. Completed work is preserved.'
              : `Run state: ${states[nextRun.state]}.`,
          );
          const nextHistory = await request<RunHistory>('/runs', controller.signal);
          if (!controller.signal.aborted) setHistory(nextHistory);
        }
        retryDelay = nextRun.state === 'Running' || nextRun.state === 'Planned' ? 1000 : 5000;
      } catch (caught) {
        if (controller.signal.aborted) return;
        needsBootstrap = true;
        setConnected(false);
        setError((previous) =>
          previous?.focus ? previous : { message: errorMessage(caught), focus: false },
        );
        retryDelay = Math.min(retryDelay * 2, 10000);
      } finally {
        if (!controller.signal.aborted) timer = setTimeout(() => void poll(), retryDelay);
      }
    }
    void poll();
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [selectedId]);

  function chooseScope(id: string) {
    const baseline = catalog?.baselines.find((item) => item.scopeId === id)?.id ?? '';
    setScopeId(id);
    chooseBaseline(baseline);
  }

  function chooseBaseline(id: string) {
    setBaselineId(id);
    setProfileId(catalog?.profiles.find((item) => item.baselineIds.includes(id))?.id ?? '');
  }

  async function refresh() {
    setBusy(true);
    setError(null);
    try {
      const [nextCatalog, nextHistory] = await Promise.all([
        request<Catalog>('/catalog'),
        request<RunHistory>('/runs'),
      ]);
      setCatalog(nextCatalog);
      setHistory(nextHistory);
      setConnected(true);
      if (!scopeId) {
        const scope = nextCatalog.scopes[0]?.id ?? '';
        const baseline = nextCatalog.baselines.find((item) => item.scopeId === scope)?.id ?? '';
        setScopeId(scope);
        setBaselineId(baseline);
        setProfileId(
          nextCatalog.profiles.find((item) => item.baselineIds.includes(baseline))?.id ?? '',
        );
      }
      setAnnouncement('Saved run history refreshed.');
    } catch (caught) {
      setError({ message: errorMessage(caught), focus: true });
    } finally {
      setBusy(false);
    }
  }

  async function start(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canStart || busy || !catalog) return;
    const selection = JSON.stringify({ scopeId, baselineId, profileId });
    if (startKey.current?.selection !== selection)
      startKey.current = { selection, id: crypto.randomUUID() };
    setBusy(true);
    setError(null);
    try {
      const next = await request<RunDetail>(
        '/runs',
        undefined,
        { scopeId, baselineId, profileId, requestId: startKey.current.id },
        catalog.csrfToken,
      );
      setRun(next);
      setSelectedId(next.runId);
      startKey.current = null;
      await refreshAcceptedHistory();
      setAnnouncement('Synthetic run started. The server has locked its inputs.');
      requestAnimationFrame(() => detailRef.current?.focus());
    } catch (caught) {
      setError({ message: errorMessage(caught), focus: true });
      if (caught instanceof DemoRequestError && caught.status === 403) void refreshToken();
    } finally {
      setBusy(false);
    }
  }

  async function refreshToken() {
    try {
      setCatalog(await request<Catalog>('/catalog'));
    } catch {
      /* The visible error remains actionable; Refresh retries bootstrap. */
    }
  }

  async function refreshAcceptedHistory() {
    try {
      setHistory(await request<RunHistory>('/runs'));
    } catch {
      setError({
        message:
          'The run request was accepted, but saved history could not be refreshed. Reconnect to read its latest state.',
        focus: false,
      });
    }
  }

  async function mutate(action: 'cancel' | 'resume') {
    if (
      !run ||
      !catalog ||
      busy ||
      !(action === 'cancel' ? run.actions.canCancel : run.actions.canResume)
    )
      return;
    const runId = run.runId;
    setBusy(true);
    setError(null);
    try {
      const fresh = await request<RunDetail>(`/runs/${encodeURIComponent(runId)}`);
      if (currentSelection.current === runId) setRun(fresh);
      if (!(action === 'cancel' ? fresh.actions.canCancel : fresh.actions.canResume)) {
        setError({
          message:
            action === 'cancel'
              ? `This run can no longer be cancelled. Current state: ${states[fresh.state]}. The latest saved facts are shown; refresh before another action.`
              : `This run cannot resume now. Current state: ${states[fresh.state]}. The latest saved facts are shown; wait for the server to permit recovery.`,
          focus: true,
        });
        return;
      }
      const next = await request<RunDetail>(
        `/runs/${encodeURIComponent(runId)}/${action}`,
        undefined,
        { expectedRevision: fresh.revision, requestId: crypto.randomUUID() },
        catalog.csrfToken,
      );
      if (currentSelection.current === runId) setRun(next);
      await refreshAcceptedHistory();
      setAnnouncement(
        action === 'cancel'
          ? next.state === 'Cancelled'
            ? 'Run cancelled. Completed work is preserved.'
            : 'Cancellation requested. Completed work is preserved.'
          : 'Recovery requested. The server resumes missing work.',
      );
    } catch (caught) {
      setError({ message: errorMessage(caught), focus: true });
      if (caught instanceof DemoRequestError && caught.status === 403) void refreshToken();
      if (caught instanceof DemoRequestError && caught.status === 409) {
        try {
          const next = await request<RunDetail>(`/runs/${encodeURIComponent(runId)}`);
          if (currentSelection.current === runId) setRun(next);
        } catch {
          /* Keep the error visible until polling recovers. */
        }
      }
    } finally {
      setBusy(false);
    }
  }

  function selectRun(id: string) {
    setError(null);
    if (id !== selectedId) setRun(null);
    setSelectedId(id);
    requestAnimationFrame(() => detailRef.current?.focus());
  }

  return (
    <>
      <a className="skip-link" href="#workspace">
        Skip to workspace
      </a>
      <header className="masthead">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            I
          </span>
          <span>
            IGA Migration Tool<span className="brand-subtitle">Consultant workspace</span>
          </span>
        </div>
        <span className="demo-badge">Synthetic local demo</span>
      </header>
      <main id="workspace" className="workspace">
        <div className="page-title">
          <div>
            <p className="eyebrow">Assessment foundation</p>
            <h1>Understand coverage. Keep every result.</h1>
            <p className="intro">
              Start a fixture run, follow its progress and return to the work saved by the local
              host.
            </p>
          </div>
          <div className={`connection ${connected ? 'connected' : ''}`}>
            <span aria-hidden="true" />
            {connected ? 'Local host connected' : 'Waiting for local host'}
          </div>
        </div>
        <aside className="boundary-note" aria-label="Demo scope">
          <strong>Synthetic evidence only.</strong> Coverage is demonstrated with fixed fixtures.
          Named analysis presets calculate health from fixed synthetic facts. Live customer
          assessment remains pending.
        </aside>
        <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">
          {announcement}
        </div>
        {error && (
          <div className="error-summary" role="alert" tabIndex={-1} ref={errorRef}>
            <h2>Request needs attention</h2>
            <p>{error.message}</p>
            <p>
              Saved run facts stay on the host. Use Refresh to reconnect or select a run to read its
              latest state.
            </p>
            <button onClick={() => void refresh()} disabled={busy}>
              Refresh connection
            </button>
          </div>
        )}
        <div className="work-grid">
          <section className="panel configuration" aria-labelledby="configuration-heading">
            <p className="eyebrow">01 / Configure</p>
            <h2 id="configuration-heading">Start a synthetic run</h2>
            <p className="muted">Choose a named baseline and a fixed assessment profile.</p>
            <form onSubmit={(event) => void start(event)} aria-busy={busy}>
              <label htmlFor="scope">Demo scope</label>
              <select
                id="scope"
                value={scopeId}
                onChange={(event) => chooseScope(event.target.value)}
                disabled={busy || !catalog}
              >
                <option value="" disabled>
                  Select scope
                </option>
                {catalog?.scopes.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.label}
                  </option>
                ))}
              </select>
              <label htmlFor="baseline">Evidence baseline</label>
              <select
                id="baseline"
                value={baselineId}
                onChange={(event) => chooseBaseline(event.target.value)}
                disabled={busy || !catalog}
              >
                <option value="" disabled>
                  Select baseline
                </option>
                {baselines.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.label}
                  </option>
                ))}
              </select>
              {selectedBaseline && (
                <p className="field-note">Fixture version {selectedBaseline.version}</p>
              )}
              <label htmlFor="profile">Assessment profile</label>
              <select
                id="profile"
                value={profileId}
                onChange={(event) => setProfileId(event.target.value)}
                disabled={busy || !catalog}
              >
                <option value="" disabled>
                  Select profile
                </option>
                {profiles.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.label} · {item.version}
                  </option>
                ))}
              </select>
              {!!selectedBaseline?.warnings.length && (
                <div className="warning-note">
                  <h3>Before you start</h3>
                  <ul>
                    {selectedBaseline.warnings.map((item, index) => (
                      <li key={`${item.code}-${index}`}>{item.message}</li>
                    ))}
                  </ul>
                </div>
              )}
              <button className="primary" type="submit" disabled={busy || !canStart}>
                {busy ? 'Request in progress…' : 'Start synthetic run'}
              </button>
              <p className="field-note">
                Inputs lock when the run starts. Completed results are preserved during cancellation
                and recovery.
              </p>
            </form>
            {profileId === 'synthetic-phase1b-combined-v1' && catalog && (
              <Phase1BSetupController
                csrfToken={catalog.csrfToken}
                onStarted={(next) => {
                  setRun(next);
                  setSelectedId(next.runId);
                  setHistory((previous) =>
                    previous
                      ? {
                          ...previous,
                          runs: [next, ...previous.runs.filter((r) => r.runId !== next.runId)],
                        }
                      : previous,
                  );
                  setAnnouncement(
                    'The approved fictional Phase 1B run is saved. Automatic local work is underway.',
                  );
                }}
              />
            )}
          </section>
          <section
            className="panel run-detail"
            ref={detailRef}
            tabIndex={-1}
            aria-labelledby="run-heading"
          >
            <div className="section-heading">
              <div>
                <p className="eyebrow">02 / Follow the work</p>
                <h2 id="run-heading">{run ? 'Selected run' : 'Run progress'}</h2>
              </div>
              {run && (
                <span className={`state-pill state-${run.state.toLowerCase()}`}>
                  {stateLabel(run.state, run.selection.profileId)}
                </span>
              )}
            </div>
            {!run ? (
              <div className="empty-state">
                <span className="empty-symbol" aria-hidden="true">
                  ↗
                </span>
                <h3>{selectedId ? 'Reading the saved run…' : 'Your next run starts here'}</h3>
                <p>
                  {selectedId
                    ? 'Progress comes from the local host’s durable record.'
                    : 'Choose fixtures on the left, or open a saved run below.'}
                </p>
              </div>
            ) : (
              <>
                <p className="run-selection">{selectionName(run.selection)}</p>
                <p className="muted small">
                  {run.selection.scopeLabel} · Started{' '}
                  <time dateTime={run.createdAtUtc}>{formatTime(run.createdAtUtc)}</time>
                </p>
                {run.cancelRequested && run.state !== 'Cancelled' && (
                  <p className="warning-note">
                    <strong>Cancellation requested.</strong> No new work starts; in-flight read-only
                    work may finish within its lease. Completed results remain saved.
                  </p>
                )}
                {run.state === 'Cancelled' && (
                  <p className="warning-note">
                    <strong>Run cancelled.</strong> Completed work is preserved in this saved
                    record.
                  </p>
                )}
                <div className="metrics">
                  <div>
                    <span>Planned units</span>
                    <strong>{formatNumber(run.progress.plannedUnits)}</strong>
                  </div>
                  <div>
                    <span>Terminal results</span>
                    <strong>{formatNumber(run.progress.terminalUnits)}</strong>
                  </div>
                  <div>
                    <span>Still outstanding</span>
                    <strong>{formatNumber(run.progress.remainingUnits)}</strong>
                  </div>
                </div>
                <label className="progress-label" htmlFor="run-progress">
                  Terminal coverage results
                </label>
                <progress
                  id="run-progress"
                  value={run.progress.terminalUnits}
                  max={Math.max(1, run.progress.plannedUnits)}
                  aria-describedby="progress-description"
                />
                <p id="progress-description" className="field-note">
                  {formatNumber(run.progress.terminalUnits)} of{' '}
                  {formatNumber(run.progress.plannedUnits)} planned units have a terminal state.
                  Gaps count as terminal results; they do not mean successful assessment.
                </p>
                {run.state === 'Scoring' && (
                  <div className="coverage-ready">
                    <strong>
                      {run.selection.profileId.startsWith('synthetic-analysis-') ||
                      run.selection.profileId.startsWith('synthetic-review-maturity-')
                        ? 'Coverage ready. Inspect local analysis below.'
                        : 'Coverage ready. Scoring is pending.'}
                    </strong>
                    <p>
                      {run.coverageCompletionKind === 'CompleteWithGaps'
                        ? 'Coverage finished with explicit gaps.'
                        : 'Every planned coverage unit has a terminal result.'}{' '}
                      This is not a completed health assessment.
                    </p>
                  </div>
                )}
                <div className="run-actions">
                  <button
                    onClick={() => void mutate('cancel')}
                    disabled={busy || !run.actions.canCancel}
                  >
                    Cancel run
                  </button>
                  <button
                    onClick={() => void mutate('resume')}
                    disabled={busy || !run.actions.canResume}
                  >
                    Resume expired work
                  </button>
                </div>
                <p className="field-note">
                  {run.actions.canResume
                    ? 'The server has confirmed that this run can resume its missing work.'
                    : run.actions.resumeAvailableAtUtc
                      ? `Recovery waits for the server lease to expire (${formatTime(run.actions.resumeAvailableAtUtc)}).`
                      : 'Recovery is available only when the server permits it.'}
                </p>
                {!!run.warnings.length && (
                  <div className="warning-note">
                    <h3>Run warnings</h3>
                    <ul>
                      {run.warnings.map((item, index) => (
                        <li key={`${item.code}-${index}`}>{item.message}</li>
                      ))}
                    </ul>
                  </div>
                )}
                <div className="details-grid">
                  <section>
                    <h3>Coverage states</h3>
                    {run.stateCounts.length ? (
                      <dl className="count-list">
                        {run.stateCounts.map((item) => (
                          <div key={item.state}>
                            <dt>{stateName(item.state)}</dt>
                            <dd>{formatNumber(item.count)}</dd>
                          </div>
                        ))}
                      </dl>
                    ) : (
                      <p className="muted">No terminal results yet.</p>
                    )}
                  </section>
                  <section>
                    <h3>Executable coverage</h3>
                    <p className="coverage-measure">
                      {run.executableCoverage?.hasApplicableUnits
                        ? `${formatNumber(run.executableCoverage.numerator)} / ${formatNumber(run.executableCoverage.denominator)}`
                        : 'Unavailable'}
                    </p>
                    <p className="field-note">
                      Executed applicable units / all applicable planned units. No applicable units
                      cannot be shown as perfect coverage.
                    </p>
                  </section>
                </div>
                <section className="subsection">
                  <h3>Gaps and limitations</h3>
                  {run.limitations.length ? (
                    <div
                      className="table-scroll"
                      tabIndex={0}
                      role="region"
                      aria-label="Coverage limitations table"
                    >
                      <table>
                        <caption className="sr-only">
                          Grouped coverage limitations for selected run
                        </caption>
                        <thead>
                          <tr>
                            <th scope="col">State</th>
                            <th scope="col">Reason</th>
                            <th scope="col">Stage</th>
                            <th scope="col">Units</th>
                          </tr>
                        </thead>
                        <tbody>
                          {run.limitations.map((item, index) => (
                            <tr
                              key={`${item.state}-${item.reasonCode}-${item.responsibleStage}-${index}`}
                            >
                              <td>{stateName(item.state)}</td>
                              <td className="code-cell">{item.reasonCode}</td>
                              <td>{item.responsibleStage}</td>
                              <td>{formatNumber(item.count)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <p className="muted">
                      No declared limitations in the current saved results. Outstanding work is
                      still shown above.
                    </p>
                  )}
                </section>
                {run.selection.profileId === 'synthetic-phase1b-combined-v1' ? (
                  <Phase1BRunController
                    key={run.runId}
                    run={run}
                    csrfToken={catalog?.csrfToken ?? ''}
                  />
                ) : (
                  <AnalysisView
                    key={run.runId}
                    run={run}
                    csrfToken={catalog?.csrfToken ?? ''}
                    artifactDrafts={artifactDrafts}
                    planningTaskDrafts={planningTaskDrafts}
                  />
                )}
                <details className="locked-inputs">
                  <summary>Locked input versions and digests</summary>
                  <p className="field-note">
                    These immutable fixture inputs remain fixed across restart and recovery.
                  </p>
                  <dl>
                    {run.lockedInputs.map((item, index) => (
                      <div key={`${item.name}-${index}`}>
                        <dt>{item.name}</dt>
                        <dd>
                          <span>{item.version}</span>
                          <code>{item.sha256}</code>
                        </dd>
                      </div>
                    ))}
                  </dl>
                </details>
                <p className="record-meta">
                  Run <code>{run.runId}</code> · Revision {run.revision} · Updated{' '}
                  <time dateTime={run.updatedAtUtc}>{formatTime(run.updatedAtUtc)}</time>
                </p>
              </>
            )}
          </section>
        </div>
        <section className="panel history" aria-labelledby="history-heading">
          <div className="section-heading">
            <div>
              <p className="eyebrow">03 / Return to saved work</p>
              <h2 id="history-heading">Run history</h2>
              <p className="muted">Recent synthetic runs persist across local host restarts.</p>
            </div>
            <button onClick={() => void refresh()} disabled={busy}>
              Refresh history
            </button>
          </div>
          {!history ? (
            <p className="muted">Reading saved runs…</p>
          ) : !history.runs.length ? (
            <p className="history-empty">
              No saved runs yet. Start a synthetic run to create the first durable record.
            </p>
          ) : (
            <div
              className="table-scroll"
              tabIndex={0}
              role="region"
              aria-label="Saved run history table"
            >
              <table>
                <caption className="sr-only">
                  Recent saved synthetic runs; open a run to inspect its progress and locked inputs
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Baseline and profile</th>
                    <th scope="col">Scope</th>
                    <th scope="col">State</th>
                    <th scope="col">Terminal / planned</th>
                    <th scope="col">Started</th>
                    <th scope="col">Open</th>
                  </tr>
                </thead>
                <tbody>
                  {history.runs.map((item) => (
                    <tr
                      key={item.runId}
                      className={selectedId === item.runId ? 'selected-row' : undefined}
                    >
                      <th scope="row">
                        <span>{item.selection.baselineLabel}</span>
                        <span className="table-subtitle">{item.selection.profileLabel}</span>
                      </th>
                      <td>{item.selection.scopeLabel}</td>
                      <td>
                        {item.cancelRequested && item.state !== 'Cancelled'
                          ? 'Cancellation requested'
                          : stateLabel(item.state, item.selection.profileId)}
                      </td>
                      <td>
                        {formatNumber(item.progress.terminalUnits)} /{' '}
                        {formatNumber(item.progress.plannedUnits)}
                      </td>
                      <td>
                        <time dateTime={item.createdAtUtc}>{formatTime(item.createdAtUtc)}</time>
                      </td>
                      <td>
                        <button
                          className="text-button"
                          onClick={() => selectRun(item.runId)}
                          aria-label={`Open ${item.selection.baselineLabel} run started ${formatTime(item.createdAtUtc)}`}
                          aria-current={selectedId === item.runId ? 'true' : undefined}
                        >
                          Open run <span aria-hidden="true">↗</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>
      <footer className="footer">
        Local demonstration · Fixed synthetic fixtures · Live source, identity, AI and publication
        remain disabled
      </footer>
    </>
  );
}

function errorMessage(caught: unknown): string {
  return caught instanceof DemoRequestError
    ? caught.message
    : 'The local host is unavailable or its response could not be read. Reconnect and refresh the saved run.';
}
