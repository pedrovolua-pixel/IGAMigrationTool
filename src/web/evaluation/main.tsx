import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { createRoot } from 'react-dom/client';
import { Workflow } from './workflow.ts';
import type { Draft } from './workflow.ts';
import type { Correction, Member } from './contracts.ts';
import './styles.css';
const blankCorrection = (): Correction => ({
  severity: null,
  category: null,
  rootCause: null,
  recommendation: null,
});
const blankDraft = (m?: Member): Draft => ({
  kind: m?.canReview === false ? 'PresentationCorrection' : 'Review',
  outcome: m?.authorizedContextSufficient === false ? 'Indeterminate' : 'Confirmed',
  origin: 'Confirmed',
  reason: '',
  evidenceReferenceIds: [],
  correction: blankCorrection(),
});
function App() {
  const [controller] = useState(() => new Workflow());
  const state = useSyncExternalStore(controller.subscribe, controller.snapshot);
  const [draft, setDraft] = useState<Draft>(blankDraft());
  const detailHeading = useRef<HTMLHeadingElement>(null);
  const status = useRef<HTMLDivElement>(null);
  const validation = useRef<HTMLDivElement>(null);
  const w = state.workspace;
  const version = state.version;
  const member = w?.members.find((m) => m.original.memberId === state.memberId);
  const historical = Boolean(w && version && version.dto.version !== w.aggregateRevision);
  const frozenReview = version?.reviews.find((r) => r.memberId === state.memberId);
  const locked = state.mode === 'sending' || state.mode === 'unknown' || state.mode === 'loading';
  useEffect(() => {
    void controller.refresh();
    return () => controller.destroy();
  }, [controller]);
  useEffect(() => {
    setDraft(blankDraft(member));
  }, [member, version?.dto.version]);
  useEffect(() => {
    if (state.message.startsWith('Review recorded')) status.current?.focus();
    if (state.mode === 'conflict') status.current?.focus();
  }, [state.message, state.mode]);
  const change = (next: Draft) => {
    controller.clearPrepared();
    setDraft(next);
  };
  const chooseMember = (id: string) => {
    controller.selectMember(id);
    queueMicrotask(() => detailHeading.current?.focus());
  };
  const stage = () => {
    controller.stage(draft);
    queueMicrotask(() => {
      if (!controller.state.prepared) validation.current?.focus();
      else status.current?.focus();
    });
  };
  return (
    <>
      <a href="#main" className="skip-link">
        Skip to evaluation
      </a>
      <header>
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            i
          </span>
          <span>
            IGA<span className="brand-subtitle">Consultant workspace</span>
          </span>
        </div>
        <span className="badge">Local synthetic fixture</span>
      </header>
      <main id="main">
        <div className="intro">
          <p className="eyebrow">Phase 1C · Synthetic review</p>
          <h1>Evaluation review</h1>
          <p>
            Review this frozen cohort. Original conclusions and prior outcome versions remain
            unchanged.
          </p>
        </div>
        <aside className="notice">
          Full pilot acceptance is not verified. This workspace uses fictional evidence and
          server-seeded assignments.
        </aside>
        <div className="toolbar">
          <button disabled={state.mode === 'sending'} onClick={() => void controller.refresh()}>
            Refresh workspace
          </button>
          <div
            ref={status}
            tabIndex={-1}
            role={state.mode === 'unavailable' || state.mode === 'conflict' ? 'alert' : 'status'}
            aria-live="polite"
            className="status"
          >
            {state.message}
          </div>
        </div>
        {w && version && state.mode !== 'unavailable' && state.mode !== 'conflict' ? (
          <>
            <section aria-labelledby="cohort">
              <div className="section-heading">
                <div>
                  <p className="eyebrow">Immutable membership</p>
                  <h2 id="cohort">Frozen cohort</h2>
                </div>
                <span className="pill">100 selected · 120 source members</span>
              </div>
              <p>
                All 40 Critical and High members are mandatory. The lower-severity sample contains
                60 members: 15 from environment A and 45 from environment B.
              </p>
              <details>
                <summary>Source locks</summary>
                <dl className="locks">
                  <dt>Source digest</dt>
                  <dd>{w.sourceDigest}</dd>
                  <dt>Population digest</dt>
                  <dd>{w.populationDigest}</dd>
                  <dt>Sample digest</dt>
                  <dd>{w.sampleDigest}</dd>
                  <dt>Original sample correction cutoff</dt>
                  <dd>{w.sampleCorrectionCutoffUtc}</dd>
                  <dt>Outcome manifest digest</dt>
                  <dd>{version.dto.versionManifestDigest}</dd>
                </dl>
                <p className="small">
                  Source anchors are verified by the server. This view verifies supplied outcome
                  canonical bytes and their hash relationships; it does not receive or hash the full
                  source population.
                </p>
              </details>
            </section>
            <section aria-labelledby="accuracy">
              <div className="section-heading">
                <div>
                  <p className="eyebrow">Server-derived immutable outcome</p>
                  <h2 id="accuracy">Accuracy and sample limits</h2>
                </div>
                <span className="pill">Outcome version {version.dto.version}</span>
              </div>
              <div className="metrics">
                <div>
                  <span className="metric">
                    {version.counts.denominator === 0
                      ? 'No reviewed denominator'
                      : `${version.counts.confirmed} / ${version.counts.denominator} confirmed`}
                  </span>
                  <span className="small">
                    Reviewed non-indeterminate denominator: {version.counts.denominator}
                  </span>
                </div>
                <dl className="counts">
                  <div>
                    <dt>Confirmed</dt>
                    <dd>{version.counts.confirmed}</dd>
                  </div>
                  <div>
                    <dt>Rejected</dt>
                    <dd>{version.counts.rejected}</dd>
                  </div>
                  <div>
                    <dt>Indeterminate</dt>
                    <dd>{version.counts.indeterminate}</dd>
                  </div>
                  <div>
                    <dt>Unreviewed</dt>
                    <dd>{version.counts.unreviewed}</dd>
                  </div>
                  <div>
                    <dt>Corrected</dt>
                    <dd>{version.counts.corrected}</dd>
                  </div>
                </dl>
              </div>
              <p className="small">
                Corrected outcomes overlap their originating classification; they are not added
                again to the denominator.
              </p>
              {version.counts.lowSampleWarning && (
                <p className="warning">
                  Fewer than 30 reviewed outcomes: interpret this ratio cautiously.
                </p>
              )}
              <p className="small">
                Desired-outcome track: {version.desired.selected} selected;{' '}
                {version.desired.denominator === 0
                  ? 'no reviewed denominator'
                  : `${version.desired.confirmed} / ${version.desired.denominator} confirmed`}
                . General findings and desired outcomes remain separate.
              </p>
              <details>
                <summary>Sample breakdowns</summary>
                <div
                  className="table-scroll"
                  role="region"
                  aria-label="Server sample breakdowns"
                  tabIndex={0}
                >
                  <table>
                    <caption>Exact server-supplied counts and warnings</caption>
                    <thead>
                      <tr>
                        <th>Dimension</th>
                        <th>Key</th>
                        <th>Confirmed / denominator</th>
                        <th>Sample limit</th>
                      </tr>
                    </thead>
                    <tbody>
                      {version.breakdowns.map((b, i) => (
                        <tr key={`${b.dimension}-${b.key}-${i}`}>
                          <th scope="row">{b.dimension}</th>
                          <td>{b.key}</td>
                          <td>
                            {b.summary.denominator === 0
                              ? 'No reviewed denominator'
                              : `${b.summary.confirmed} / ${b.summary.denominator}`}
                          </td>
                          <td>
                            {b.summary.lowSampleWarning
                              ? 'Fewer than 30 reviewed'
                              : 'At least 30 reviewed'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
            </section>
            <section aria-labelledby="versions">
              <h2 id="versions">Outcome versions</h2>
              <label className="field" htmlFor="version">
                Immutable outcome version
                <select
                  id="version"
                  value={version.dto.version}
                  disabled={locked}
                  onChange={(e) => void controller.selectVersion(Number(e.target.value))}
                >
                  {w.versions.map((v) => (
                    <option key={v.version} value={v.version}>
                      Version {v.version}
                      {v.version === w.aggregateRevision ? ' · current' : ' · historical'}
                    </option>
                  ))}
                </select>
              </label>
              {historical && (
                <p className="warning">
                  Historical outcome version — review actions are unavailable.
                </p>
              )}
              <dl className="locks">
                <dt>Correction cutoff</dt>
                <dd>{version.dto.correctionCutoffUtc}</dd>
                <dt>Predecessor</dt>
                <dd>{version.dto.predecessorDigest ?? 'Initial version — no predecessor'}</dd>
                <dt>Snapshot digest</dt>
                <dd>{version.dto.contentDigest}</dd>
              </dl>
            </section>
            <section aria-labelledby="members">
              <h2 id="members">Selected members</h2>
              <label className="field" htmlFor="member">
                Choose one selected member
                <select
                  id="member"
                  disabled={locked}
                  value={state.memberId ?? ''}
                  onChange={(e) => chooseMember(e.target.value)}
                >
                  {w.members.map((m) => (
                    <option key={m.original.memberId} value={m.original.memberId}>
                      {m.original.memberId} · {m.originMetadata.originSeverity} ·{' '}
                      {m.originMetadata.environmentId}
                    </option>
                  ))}
                </select>
              </label>
              {member && (
                <div className="review-grid">
                  <article>
                    <h3 ref={detailHeading} tabIndex={-1}>
                      Original conclusion
                    </h3>
                    <h4>{member.original.title}</h4>
                    <p>{member.original.rootCause}</p>
                    <p>{member.original.recommendation}</p>
                    <dl className="locks">
                      <dt>Original severity</dt>
                      <dd>{member.original.severity}</dd>
                      <dt>Original category</dt>
                      <dd>{member.original.category}</dd>
                      <dt>Frozen module</dt>
                      <dd>{member.originMetadata.moduleId}</dd>
                      <dt>Originating rule</dt>
                      <dd>{member.originMetadata.ruleId}</dd>
                      <dt>Originating model/prompt</dt>
                      <dd>{member.originMetadata.modelPromptId}</dd>
                      <dt>Evidence references</dt>
                      <dd>
                        {member.original.evidenceReferenceIds.join(', ') ||
                          'No permitted evidence references'}
                      </dd>
                    </dl>
                  </article>
                  <article>
                    <h3>Current review</h3>
                    <p className="outcome">
                      {historical ? frozenReview?.outcome : member.outcome}
                      {(historical
                        ? frozenReview?.originatingClassification
                        : member.originatingClassification) &&
                        ` · origin ${historical ? frozenReview?.originatingClassification : member.originatingClassification}`}
                    </p>
                    {historical && (
                      <p className="small">
                        Displayed classification belongs to version {version.dto.version}. Original
                        source presentation was separately authorized by the current workspace read.
                      </p>
                    )}
                    <h3>Current assignment</h3>
                    <dl className="locks">
                      <dt>Server-trusted actor</dt>
                      <dd>{w.actorId}</dd>
                      <dt>Exact assignment</dt>
                      <dd>{member.assignmentId}</dd>
                      <dt>Environment</dt>
                      <dd>{member.originMetadata.environmentId}</dd>
                      <dt>Current member revision</dt>
                      <dd>{member.revision}</dd>
                    </dl>
                    <p className="small">
                      Assignment and eligibility are read-only server projections. No browser role
                      or qualification change is available.
                    </p>
                    {member.currentCorrection && !historical && (
                      <>
                        <h3>Current presentation correction</h3>
                        <dl className="locks">
                          {Object.entries(member.currentCorrection)
                            .filter(([, v]) => v !== null)
                            .map(([k, v]) => (
                              <div key={k}>
                                <dt>{k}</dt>
                                <dd>{v}</dd>
                              </div>
                            ))}
                        </dl>
                      </>
                    )}
                    {!historical && (member.canReview || member.canCorrectPresentation) ? (
                      <form
                        onSubmit={(e) => {
                          e.preventDefault();
                          stage();
                        }}
                      >
                        <fieldset disabled={locked || Boolean(state.prepared)}>
                          <legend>Review action</legend>
                          <label className="field" htmlFor="action">
                            Action
                            <select
                              id="action"
                              value={draft.kind}
                              onChange={(e) =>
                                change({ ...draft, kind: e.target.value as Draft['kind'] })
                              }
                            >
                              {member.canReview && <option value="Review">Scored review</option>}
                              {member.canCorrectPresentation && (
                                <option value="PresentationCorrection">
                                  Presentation correction
                                </option>
                              )}
                            </select>
                          </label>
                          {draft.kind === 'Review' && (
                            <label className="field" htmlFor="outcome">
                              Outcome
                              <select
                                id="outcome"
                                value={draft.outcome}
                                onChange={(e) =>
                                  change({ ...draft, outcome: e.target.value as Draft['outcome'] })
                                }
                              >
                                {(member.authorizedContextSufficient
                                  ? ['Confirmed', 'Rejected', 'Corrected']
                                  : ['Indeterminate']
                                ).map((o) => (
                                  <option key={o}>{o}</option>
                                ))}
                              </select>
                            </label>
                          )}
                          {draft.kind === 'Review' && draft.outcome === 'Corrected' && (
                            <label className="field" htmlFor="origin">
                              Originating classification
                              <select
                                id="origin"
                                value={draft.origin}
                                onChange={(e) =>
                                  change({ ...draft, origin: e.target.value as Draft['origin'] })
                                }
                              >
                                <option>Confirmed</option>
                                <option>Rejected</option>
                              </select>
                            </label>
                          )}
                          <label className="field" htmlFor="reason">
                            Rationale
                            <textarea
                              id="reason"
                              value={draft.reason}
                              maxLength={2000}
                              required
                              onChange={(e) => change({ ...draft, reason: e.target.value })}
                              aria-describedby="reason-bound"
                            />
                            <span id="reason-bound" className="small">
                              Required, up to 2,000 characters. Keep evidence fictional.
                            </span>
                          </label>
                          <fieldset>
                            <legend>Permitted evidence references</legend>
                            {member.original.evidenceReferenceIds.map((ref) => (
                              <label className="checkbox" key={ref}>
                                <input
                                  type="checkbox"
                                  checked={draft.evidenceReferenceIds.includes(ref)}
                                  onChange={(e) =>
                                    change({
                                      ...draft,
                                      evidenceReferenceIds: e.target.checked
                                        ? [...draft.evidenceReferenceIds, ref]
                                        : draft.evidenceReferenceIds.filter((r) => r !== ref),
                                    })
                                  }
                                />
                                <span>{ref}</span>
                              </label>
                            ))}
                          </fieldset>
                          {(draft.kind === 'PresentationCorrection' ||
                            draft.outcome === 'Corrected') && (
                            <fieldset>
                              <legend>Correction dimensions</legend>
                              <p className="small">
                                Choose at least one dimension. Presentation text does not change
                                frozen classification or authorization categories.
                              </p>
                              {(
                                ['severity', 'category', 'rootCause', 'recommendation'] as const
                              ).map((k) => (
                                <label className="field" key={k} htmlFor={`correction-${k}`}>
                                  {
                                    {
                                      severity: 'Severity presentation',
                                      category: 'Category presentation',
                                      rootCause: 'Root cause presentation',
                                      recommendation: 'Recommendation presentation',
                                    }[k]
                                  }
                                  <textarea
                                    id={`correction-${k}`}
                                    value={draft.correction[k] ?? ''}
                                    maxLength={2000}
                                    onChange={(e) =>
                                      change({
                                        ...draft,
                                        correction: {
                                          ...draft.correction,
                                          [k]: e.target.value === '' ? null : e.target.value,
                                        },
                                      })
                                    }
                                  />
                                </label>
                              ))}
                            </fieldset>
                          )}
                          <button type="submit">Prepare review</button>
                        </fieldset>
                        <div ref={validation} tabIndex={-1} className="small">
                          {!state.prepared && state.message.startsWith('Check')
                            ? state.message
                            : ''}
                        </div>
                      </form>
                    ) : historical ? null : (
                      <p className="warning">Review unavailable under the current assignment.</p>
                    )}
                  </article>
                </div>
              )}
              {state.prepared && (
                <aside className="prepared" aria-labelledby="prepared">
                  <h3 id="prepared">Prepared review</h3>
                  <dl className="locks">
                    <dt>Selected member</dt>
                    <dd>{state.prepared.memberId}</dd>
                    <dt>Action</dt>
                    <dd>
                      {state.prepared.command.kind} {state.prepared.command.outcome}
                    </dd>
                    <dt>Expected version</dt>
                    <dd>{state.prepared.command.expectedAggregateRevision}</dd>
                    <dt>Origin</dt>
                    <dd>{state.prepared.command.originatingClassification ?? 'Not applicable'}</dd>
                    <dt>Rationale</dt>
                    <dd>{state.prepared.command.reason}</dd>
                    <dt>Evidence references</dt>
                    <dd>{state.prepared.command.evidenceReferenceIds.join(', ') || 'None'}</dd>
                    <dt>Event ID</dt>
                    <dd>{state.prepared.command.eventId}</dd>
                    {state.prepared.command.correction &&
                      Object.entries(state.prepared.command.correction)
                        .filter(([, v]) => v !== null)
                        .map(([k, v]) => (
                          <div key={k}>
                            <dt>{k}</dt>
                            <dd>{v}</dd>
                          </div>
                        ))}
                  </dl>
                  <div className="buttons">
                    <button
                      disabled={state.mode === 'sending'}
                      onClick={() => void controller.record()}
                    >
                      {state.mode === 'unknown' ? 'Retry same review' : 'Record review'}
                    </button>
                    {state.mode === 'ready' && (
                      <button onClick={() => controller.clearPrepared()}>Edit draft</button>
                    )}
                  </div>
                  {state.mode === 'unknown' && (
                    <p className="warning">
                      This retry preserves the original event ID and byte-identical body. Refresh
                      discards the prepared command and checks current recorded state.
                    </p>
                  )}
                </aside>
              )}
            </section>
            <section aria-labelledby="history">
              <h2 id="history">Related history</h2>
              <p className="small">
                History is checked under its separate current scope grant. Evidence references do
                not grant source retrieval.
              </p>
              <button
                disabled={state.mode !== 'ready' || state.historyLoading}
                onClick={() => void controller.loadHistory()}
              >
                Open related history
              </button>
              {state.historyLoading && <p role="status">Checking current history permission…</p>}
              {state.history && (
                <>
                  <p>
                    {state.history.events.length === 0
                      ? 'No recorded events for this member.'
                      : `${state.history.events.length} attributed events loaded.`}
                  </p>
                  <ol className="events">
                    {state.history.events.map((e) => (
                      <li key={e.eventId}>
                        <h3>
                          {e.kind} · {e.recordedOutcome}
                          {e.originatingClassification &&
                            ` · origin ${e.originatingClassification}`}
                        </h3>
                        <p>{e.reason}</p>
                        <p className="small">
                          {e.recordedAtUtc} · {e.actorId} · {e.assignmentId} · outcome version{' '}
                          {e.aggregateRevision}
                        </p>
                        {e.correction && (
                          <dl className="locks">
                            {Object.entries(e.correction)
                              .filter(([, v]) => v !== null)
                              .map(([k, v]) => (
                                <div key={k}>
                                  <dt>{k}</dt>
                                  <dd>{v}</dd>
                                </div>
                              ))}
                          </dl>
                        )}
                        <p className="small">
                          Evidence refs: {e.evidenceReferenceIds.join(', ') || 'None'}
                        </p>
                      </li>
                    ))}
                  </ol>
                  {state.history.nextAfterSequence !== null && (
                    <button
                      disabled={state.historyLoading}
                      onClick={() => void controller.loadHistory(true)}
                    >
                      Load more history
                    </button>
                  )}
                </>
              )}
            </section>
          </>
        ) : null}
        <footer>
          Local synthetic review only. Live reviewers, operational evaluation and full Milestone 08
          acceptance remain unverified.
        </footer>
      </main>
    </>
  );
}
const root = document.getElementById('root');
if (!root) throw new Error('Missing workspace mount');
createRoot(root).render(<App />);
