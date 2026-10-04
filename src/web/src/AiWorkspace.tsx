import { useId, useRef, useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import type { AnalysisDetail, RunDetail } from './demo-contract.generated';
import './AiWorkspace.css';

const tabs = ['Analysis', 'Proposed findings', 'Analysis history', 'Deep analysis'] as const;
function assessmentDate(value: string): string {
  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        timeZone: 'UTC',
      }).format(date)
    : value;
}

const unavailable = 'Interactive AI is unavailable in this pilot. No provider request can be sent.';

/** Read-only composition. The supplied proposal controller stays mounted in its tab panel. */
export function AiWorkspace({
  run,
  analysis,
  preview,
  onSettings,
  initialProposalView = false,
}: {
  run: RunDetail | null;
  analysis: AnalysisDetail | null;
  preview: ReactNode;
  onSettings?: () => void;
  initialProposalView?: boolean;
}) {
  const prefix = useId();
  const [active, setActive] = useState<number>(() => (initialProposalView ? 1 : 0));
  const tablist = useRef<HTMLDivElement>(null);
  const boundResponse =
    run && analysis?.runId === run.runId && analysis.runRevision === run.revision ? analysis : null;
  const current = boundResponse?.status === 'Ready' ? boundResponse : null;
  const reviewStates = new Map(
    current?.review?.status === 'Ready'
      ? current.review.findings.map((finding) => [finding.id, finding.state])
      : [],
  );
  const proposed = current?.findings.filter(
    (finding) => (reviewStates.get(finding.id) ?? finding.state).toLowerCase() === 'proposed',
  ).length;
  const aiPreview = boundResponse?.aiPreview;
  const proposalCount =
    aiPreview?.status === 'Ready' &&
    aiPreview.snapshot &&
    aiPreview.runId === run?.runId &&
    aiPreview.runRevision === run?.revision &&
    aiPreview.baselineId === run.selection.baselineId &&
    aiPreview.profileId === run.selection.profileId
      ? aiPreview.snapshot.proposals.length
      : null;
  const activateByKeyboard = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let next: number;
    if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
    else if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = tabs.length - 1;
    else return;
    event.preventDefault();
    setActive(next);
    tablist.current?.querySelector<HTMLButtonElement>(`[data-ai-tab="${next}"]`)?.focus();
  };

  return (
    <section className="ai-workspace" aria-label="AI analysis workspace">
      <div
        className="ai-workspace-tabs"
        role="tablist"
        aria-label="AI workspace sections"
        ref={tablist}
      >
        {tabs.map((tab, index) => (
          <button
            type="button"
            role="tab"
            key={tab}
            id={`${prefix}-tab-${index}`}
            data-ai-tab={index}
            aria-controls={`${prefix}-panel-${index}`}
            aria-selected={active === index}
            tabIndex={active === index ? 0 : -1}
            onClick={() => setActive(index)}
            onKeyDown={(event) => activateByKeyboard(event, index)}
          >
            {tab}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        tabIndex={0}
        id={`${prefix}-panel-0`}
        aria-labelledby={`${prefix}-tab-0`}
        hidden={active !== 0}
      >
        <div className="ai-workspace-split">
          <section
            className="ai-workspace-panel ai-workspace-chat"
            aria-labelledby={`${prefix}-assistant`}
          >
            <div className="ai-workspace-panel-heading">
              <h3 id={`${prefix}-assistant`}>Assessment assistant</h3>
              <span className="ai-workspace-pill">Unavailable</span>
            </div>
            <div className="ai-workspace-message">
              <b>Assessment assistant is unavailable</b>
              <p id={`${prefix}-unavailable`}>{unavailable}</p>
              <p>
                Existing offline proposals are available separately when admitted for the selected
                assessment.
              </p>
              <button
                className="ai-workspace-text-button"
                type="button"
                onClick={() => setActive(1)}
              >
                Review proposed findings →
              </button>
            </div>
            <div className="ai-workspace-compose-area">
              <div
                className="ai-workspace-suggestions"
                aria-label="Suggested questions unavailable"
              >
                {['Explain queue delays', 'What should we review first?', 'Show evidence gaps'].map(
                  (question) => (
                    <button
                      type="button"
                      disabled
                      aria-describedby={`${prefix}-unavailable`}
                      key={question}
                    >
                      {question}
                    </button>
                  ),
                )}
              </div>
              <div className="ai-workspace-composer">
                <label className="ai-workspace-sr-only" htmlFor={`${prefix}-question`}>
                  Ask about this assessment
                </label>
                <textarea
                  id={`${prefix}-question`}
                  disabled
                  aria-describedby={`${prefix}-unavailable`}
                  placeholder="Assistant unavailable in this pilot"
                />
                <button
                  type="button"
                  className="ai-workspace-action"
                  disabled
                  aria-describedby={`${prefix}-unavailable`}
                >
                  Send question ↑
                </button>
              </div>
            </div>
          </section>
          <div className="ai-workspace-stack">
            <section className="ai-workspace-panel" aria-labelledby={`${prefix}-context`}>
              <h3 id={`${prefix}-context`}>Analysis context</h3>
              <div className="ai-workspace-item">
                <b>Current assessment</b>
                <p>
                  {run
                    ? `${assessmentDate(run.createdAtUtc)} · ${run.selection.baselineLabel}`
                    : 'No assessment selected.'}
                </p>
                {run && (
                  <p className="ai-workspace-small">
                    {run.selection.scopeLabel} · {run.selection.profileLabel}
                  </p>
                )}
              </div>
              <div className="ai-workspace-item">
                <b>Evidence boundary</b>
                <p>
                  Interactive evidence context is unavailable. Protected raw evidence is outside
                  pilot AI scope.
                </p>
              </div>
              <div className="ai-workspace-item">
                <b>Proposal state</b>
                <p>
                  {current
                    ? `${current.findings.length} assessment findings · ${proposed} currently proposed.`
                    : 'Admitted assessment findings unavailable.'}
                </p>
                <p className="ai-workspace-small">
                  {proposalCount === null
                    ? 'Offline AI proposal count unavailable.'
                    : `${proposalCount} offline AI proposals; separate from deterministic findings.`}{' '}
                  Qualified review remains required.
                </p>
                <button
                  className="ai-workspace-text-button"
                  type="button"
                  onClick={() => setActive(1)}
                >
                  Review proposals →
                </button>
              </div>
            </section>
            <section className="ai-workspace-panel" aria-labelledby={`${prefix}-usage`}>
              <h3 id={`${prefix}-usage`}>Usage budget</h3>
              <div className="ai-workspace-panel-heading">
                <b>Unavailable</b>
                <span className="ai-workspace-small">No usage record</span>
              </div>
              <div className="ai-workspace-usage-track" aria-hidden="true" />
              <p className="ai-workspace-small">
                Provider usage and budget limits are not supplied by the saved pilot records.
              </p>
              <button
                type="button"
                className="ai-workspace-text-button"
                disabled={!onSettings}
                onClick={onSettings}
              >
                Open settings →
              </button>
            </section>
          </div>
        </div>
      </div>

      <div
        role="tabpanel"
        tabIndex={0}
        id={`${prefix}-panel-1`}
        aria-labelledby={`${prefix}-tab-1`}
        hidden={active !== 1}
        className="ai-workspace-panel ai-workspace-proposals"
      >
        <h3>AI proposals awaiting review</h3>
        {proposalCount === null && (
          <p className="ai-workspace-small">
            An admitted offline AI proposal record is unavailable for this assessment.
          </p>
        )}
        <div className="ai-workspace-preview-content">
          {preview ?? (
            <p className="ai-workspace-small">
              No offline AI proposal preview is available for this assessment.
            </p>
          )}
        </div>
      </div>

      <div
        role="tabpanel"
        tabIndex={0}
        id={`${prefix}-panel-2`}
        aria-labelledby={`${prefix}-tab-2`}
        hidden={active !== 2}
        className="ai-workspace-panel"
      >
        <div
          className="ai-workspace-table-wrap"
          tabIndex={0}
          role="region"
          aria-label="Selected assessment context"
        >
          <table>
            <caption>Selected assessment context · AI execution history unavailable</caption>
            <thead>
              <tr>
                <th scope="col">Assessment</th>
                <th scope="col">Scope</th>
                <th scope="col">Findings</th>
                <th scope="col">AI history</th>
              </tr>
            </thead>
            <tbody>
              {run ? (
                <tr>
                  <td>{assessmentDate(run.createdAtUtc)}</td>
                  <td>
                    {run.selection.scopeLabel}
                    <br />
                    {run.selection.baselineLabel}
                  </td>
                  <td>{current ? current.findings.length : 'Unavailable'}</td>
                  <td>Unavailable</td>
                </tr>
              ) : (
                <tr>
                  <td colSpan={4}>No assessment selected.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <p className="ai-workspace-note">
          Saved assessment context does not establish an AI execution. The pilot does not supply
          historical provider calls, model usage or budget measurements.
        </p>
      </div>

      <div
        role="tabpanel"
        tabIndex={0}
        id={`${prefix}-panel-3`}
        aria-labelledby={`${prefix}-tab-3`}
        hidden={active !== 3}
      >
        <p className="ai-workspace-phase">
          Phase 2 · Scoped consultant-initiated deep analysis is unavailable in this pilot.
        </p>
        <div className="ai-workspace-split">
          <section className="ai-workspace-panel" aria-labelledby={`${prefix}-scope`}>
            <h3 id={`${prefix}-scope`}>Prepare analysis scope</h3>
            <div className="ai-workspace-field">
              <label htmlFor={`${prefix}-deep-scope`}>Scope</label>
              <input
                id={`${prefix}-deep-scope`}
                disabled
                placeholder="No admitted deep analysis scope"
              />
            </div>
            <div className="ai-workspace-field">
              <label htmlFor={`${prefix}-deep-class`}>Data classes</label>
              <select id={`${prefix}-deep-class`} disabled>
                <option>Not available</option>
              </select>
            </div>
            <div className="ai-workspace-field">
              <label htmlFor={`${prefix}-deep-outcome`}>Desired outcome</label>
              <input
                id={`${prefix}-deep-outcome`}
                disabled
                placeholder="Deep analysis unavailable"
              />
            </div>
            <button type="button" className="ai-workspace-action" disabled>
              Preview scope and usage
            </button>
          </section>
          <section className="ai-workspace-panel" aria-labelledby={`${prefix}-before`}>
            <h3 id={`${prefix}-before`}>Before analysis</h3>
            <div className="ai-workspace-item">
              <b>Scope and usage</b>
              <p>
                A future review would show evidence classes, estimated usage and expected duration.
                These values are unavailable here.
              </p>
            </div>
            <div className="ai-workspace-item">
              <b>Protected evidence</b>
              <p>Raw retrieval requires separate customer authorization when necessary.</p>
            </div>
            <div className="ai-workspace-item">
              <b>Review remains required</b>
              <p>
                Deep analysis would produce proposals for review. No analysis or customer changes
                can be started from this page.
              </p>
            </div>
          </section>
        </div>
      </div>
    </section>
  );
}
