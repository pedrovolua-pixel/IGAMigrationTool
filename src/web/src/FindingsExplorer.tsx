import { useLayoutEffect, useId, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import type {
  AnalysisFinding,
  RecommendationGuidance,
  ReviewDetail,
  ReviewFinding,
} from './demo-contract.generated';
import './FindingsExplorer.css';
import { RiskDistribution } from './RiskDistribution';

export type ExplorationState = {
  query: string;
  severity: string;
  category: string;
  state: string;
  confidence: string;
  mandatory: boolean;
  selectedId: string | null;
};
export const initialExploration: ExplorationState = {
  query: '',
  severity: '',
  category: '',
  state: '',
  confidence: '',
  mandatory: false,
  selectedId: null,
};

export function FindingsExplorer({
  findings,
  review,
  guidance,
  runId,
  renderOriginal,
  exploration,
  onChange,
  dedicated = false,
  evidenceView = false,
  overlay = false,
}: {
  dedicated?: boolean;
  evidenceView?: boolean;
  overlay?: boolean;
  findings: readonly AnalysisFinding[];
  review: ReviewDetail | null;
  guidance: RecommendationGuidance | null;
  runId: string;
  renderOriginal: (finding: AnalysisFinding) => ReactNode;
  exploration: ExplorationState;
  onChange: (value: ExplorationState) => void;
}) {
  const prefix = useId();
  const [detailView, setDetailView] = useState<{
    findingId: string | null;
    view: 'Overview' | 'Evidence' | 'Relationships' | 'Review' | 'Recommendations' | 'Original';
  }>({ findingId: null, view: overlay ? 'Overview' : 'Evidence' });
  const { query, severity, category, state, confidence, mandatory, selectedId } = exploration;
  useLayoutEffect(() => {
    if (evidenceView) setDetailView({ findingId: selectedId, view: 'Evidence' });
  }, [evidenceView, selectedId]);
  const update = (value: Partial<ExplorationState>) => onChange({ ...exploration, ...value });
  const inspectId = (findingId: string) => `inspect-finding-${runId}-${findingId}`;
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [visibleLimit, setVisibleLimit] = useState(8);
  const [selectedObject, setSelectedObject] = useState<string | null>(null);
  const resultsHeading = useRef<HTMLHeadingElement>(null);
  const detailHeading = useRef<HTMLHeadingElement>(null);
  const current = (finding: AnalysisFinding): ReviewFinding | undefined =>
    review?.status === 'Ready' ? review.findings.find((item) => item.id === finding.id) : undefined;
  const findingState = (finding: AnalysisFinding) => current(finding)?.state ?? finding.state;
  const needsReview = (finding: AnalysisFinding) =>
    finding.reviewRequired && findingState(finding) === 'Proposed';
  const unique = (values: readonly string[]) => [...new Set(values)].sort();
  const visible = findings.filter((finding) => {
    const presentation = current(finding);
    const haystack = [
      finding.originalTitle,
      presentation?.title ?? finding.title,
      presentation?.businessContext ?? '',
      finding.ruleId,
      finding.rootCause,
      ...finding.objectIds,
      ...finding.evidenceReferences,
    ]
      .join(' ')
      .toLocaleLowerCase();
    return (
      (!query.trim() || haystack.includes(query.trim().toLocaleLowerCase())) &&
      (!severity || finding.severity === severity) &&
      (!category || finding.category === category) &&
      (!state || findingState(finding) === state) &&
      (!confidence || finding.confidenceBand === confidence) &&
      (!mandatory || needsReview(finding))
    );
  });
  const selected = findings.find((finding) => finding.id === selectedId);
  const selectedReview = selected ? current(selected) : undefined;
  useLayoutEffect(() => {
    const dialog = dialogRef.current;
    if (overlay && selected && dialog && !dialog.open) dialog.showModal();
    else if (dialog?.open && (!overlay || !selected)) dialog.close();
    return () => {
      if (dialog?.open) dialog.close();
    };
  }, [overlay, selected?.id]);
  useLayoutEffect(() => {
    setSelectedObject(null);
  }, [selected?.id]);
  const activeDetailView =
    detailView.findingId === selectedId ? detailView.view : overlay ? 'Overview' : 'Evidence';
  const selectedGuidance =
    guidance?.status === 'Ready'
      ? guidance.snapshot?.findings.find((finding) => finding.findingId === selectedId)
      : undefined;
  const select = (finding: AnalysisFinding) => {
    setDetailView({ findingId: finding.id, view: overlay ? 'Overview' : 'Evidence' });
    update({ selectedId: finding.id });
    requestAnimationFrame(() => {
      detailHeading.current?.focus({ preventScroll: true });
      if (overlay) dialogRef.current?.scrollTo({ top: 0 });
      else detailHeading.current?.scrollIntoView({ block: 'start' });
    });
  };
  const close = () => {
    dialogRef.current?.close();
    update({ selectedId: null });
    const target = selectedId
      ? (document.getElementById(inspectId(selectedId)) ?? resultsHeading.current)
      : resultsHeading.current;
    const restoreFocus = () => {
      target?.focus({ preventScroll: true });
      target?.scrollIntoView({ block: 'nearest' });
    };
    if (dedicated && !target?.getClientRects().length) requestAnimationFrame(restoreFocus);
    else restoreFocus();
  };
  const clear = () => {
    onChange({ ...initialExploration, selectedId });
  };
  const filtered = !!(query || severity || category || state || confidence || mandatory);
  useLayoutEffect(() => {
    setVisibleLimit(8);
  }, [query, severity, category, state, confidence, mandatory]);
  const labelCategory = (value: string) =>
    value.toLocaleLowerCase().replace(/(^|[ _-])\w/g, (letter) => letter.toLocaleUpperCase());
  const reviewFinding = () => {
    if (!selected) return;
    dialogRef.current?.close();
    update({ selectedId: null });
    const controls = document.getElementById('finding-review-controls');
    if (controls instanceof HTMLDetailsElement) controls.open = true;
    requestAnimationFrame(() => {
      const target = document.getElementById(`review-finding-${runId}-${selected.id}`);
      target?.focus({ preventScroll: true });
      target?.scrollIntoView({ block: 'start' });
    });
  };

  const filters = (
    <>
      <div className="finding-filters" role="search" aria-label="Filter findings">
        <div className="finding-search">
          <label htmlFor={`${prefix}-search`}>Search findings</label>
          <input
            id={`${prefix}-search`}
            type="search"
            maxLength={250}
            value={query}
            onChange={(event) => update({ query: event.target.value })}
            placeholder="Title, rule, object or evidence reference"
          />
        </div>
        <Filter
          id={`${prefix}-severity`}
          label="Finding severity"
          value={severity}
          values={unique(findings.map((finding) => finding.severity))}
          onChange={(value) => update({ severity: value })}
        />
        {!overlay && (
          <Filter
            id={`${prefix}-category`}
            label="Finding category"
            value={category}
            values={unique(findings.map((finding) => finding.category))}
            onChange={(value) => update({ category: value })}
          />
        )}
        <Filter
          id={`${prefix}-state`}
          label="Finding state"
          value={state}
          values={unique(findings.map(findingState))}
          onChange={(value) => update({ state: value })}
        />
        <Filter
          id={`${prefix}-confidence`}
          label="Confidence band"
          value={confidence}
          values={unique(findings.map((finding) => finding.confidenceBand))}
          onChange={(value) => update({ confidence: value })}
        />
      </div>
      <div className="finding-filter-actions">
        <button
          type="button"
          aria-pressed={mandatory}
          onClick={() => update({ mandatory: !mandatory })}
        >
          Mandatory review pending
        </button>
        <button type="button" onClick={clear} disabled={!filtered}>
          Clear finding filters
        </button>
        <p role="status" aria-live="polite" aria-atomic="true">
          Showing {visible.length} of {findings.length} findings{filtered ? ' · Filtered' : ''}
        </p>
      </div>
    </>
  );
  const detail = (
    <aside
      className="finding-detail"
      aria-labelledby={`${prefix}-detail`}
      onKeyDown={(event) => {
        if (event.key === 'Escape' && selected) {
          event.preventDefault();
          close();
        }
      }}
    >
      <div className="finding-detail-title">
        <h2 className="risk-drawer-heading">{overlay ? 'Risk details' : 'Finding details'}</h2>
        {selected && (
          <button type="button" onClick={close}>
            <span className="finding-close-label">Close finding</span>
            {dedicated && <span className="finding-back-label">Back to findings</span>}
          </button>
        )}
      </div>
      <div className="risk-detail-badges">
        {selected && (
          <>
            <span className={`risk-pill ${selected.severity.toLowerCase()}`}>
              {selected.severity}
            </span>
            <span className="risk-pill">{selectedReview?.state ?? selected.state}</span>
          </>
        )}
      </div>
      <h3 id={`${prefix}-detail`} tabIndex={-1} ref={detailHeading}>
        {selected ? (selectedReview?.title ?? selected.title) : 'Finding and evidence'}
      </h3>
      {!selected ? (
        <p>
          Select Inspect on a finding to see its explanation, evidence and original recommendations
          here.
        </p>
      ) : (
        <>
          {!visible.some((finding) => finding.id === selected.id) && (
            <p className="warning-note">
              This selected finding is outside the current filters. Its details remain open.
            </p>
          )}
          <div className="risk-summary-facts">
            <div>
              Category<strong>{labelCategory(selected.category)}</strong>
            </div>
            <div>
              Confidence
              <strong>
                {selected.confidencePercent}% · {selected.method}
              </strong>
            </div>
          </div>
          {dedicated && (
            <div className="finding-detail-tabs" role="group" aria-label="Finding detail views">
              {(overlay
                ? (['Overview', 'Relationships', 'Evidence', 'Review'] as const)
                : (['Evidence', 'Relationships', 'Recommendations', 'Original'] as const)
              ).map((view) => (
                <button
                  type="button"
                  key={view}
                  aria-pressed={activeDetailView === view}
                  aria-controls={`${prefix}-${view.toLowerCase()}`}
                  onClick={() => setDetailView({ findingId: selectedId, view })}
                >
                  {view}
                </button>
              ))}
            </div>
          )}
          <section id={`${prefix}-overview`} hidden={!overlay || activeDetailView !== 'Overview'}>
            <h4>Why this matters</h4>
            <p>{selected.rootCause}</p>
            <h4>Potential impact</h4>
            <p>{selected.impact}</p>
            <h4>Assessment method</h4>
            <p>
              {selected.method} · {selected.ruleId} / {selected.ruleVersion}
            </p>
            <h4>Review-only guidance</h4>
            <p>{selected.validationGuidance || 'No validation guidance supplied.'}</p>
            <details>
              <summary>Original and limitations</summary>
              {renderOriginal(selected)}
              <List values={selected.limitations} empty="No limitations supplied." />
            </details>
            <details>
              <summary>Original recommendation options · Unverified</summary>
              <List
                values={selected.recommendations}
                empty="No original recommendations supplied."
              />
            </details>
          </section>
          <section id={`${prefix}-review`} hidden={!overlay || activeDetailView !== 'Review'}>
            <h4>Review state</h4>
            <p>{selectedReview?.state ?? selected.state}</p>
            <p className="field-note">Original generated state: {selected.initialState}</p>
            <p>{selectedReview?.businessContext || 'No business context supplied.'}</p>
            {selectedReview ? (
              <>
                <button type="button" onClick={reviewFinding}>
                  Review this finding
                </button>
                <details>
                  <summary>Review history ({selectedReview.history.length} events)</summary>
                  {selectedReview.history.map((event) => (
                    <p key={event.eventId}>
                      {event.kind} · {event.actorId} · {event.recordedAtUtc}
                      <br />
                      {event.text}
                    </p>
                  ))}
                </details>
              </>
            ) : (
              <p>No admitted review record is supplied.</p>
            )}
          </section>
          <section
            id={`${prefix}-relationships`}
            hidden={activeDetailView !== 'Relationships'}
            className="finding-relationships"
          >
            <h5>Finding relationships</h5>
            <p className="field-note">
              Supplied finding-to-object associations only. Object inheritance and permission edges
              are not provided by this fixture.
            </p>
            <div className="finding-relationship-map">
              <div className="relationship-finding">{selectedReview?.title ?? selected.title}</div>
              <span aria-hidden="true">↓ affects</span>
              <div className="relationship-objects">
                {selected.objectIds.map((id) => (
                  <button
                    key={id}
                    type="button"
                    aria-pressed={selectedObject === id}
                    onClick={() => setSelectedObject(id)}
                  >
                    {id}
                  </button>
                ))}
              </div>
            </div>
            <h6>
              {selectedObject
                ? `Selected object: ${selectedObject}`
                : 'Affected objects and provenance'}
            </h6>
            <p>Baseline: {selected.baselineId}</p>
            <List values={selected.evidenceReferences} empty="No supplied evidence references." />
            <details>
              <summary>Relationship table alternative</summary>
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th scope="col">Finding</th>
                      <th scope="col">Relationship</th>
                      <th scope="col">Object</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selected.objectIds.map((id) => (
                      <tr key={id}>
                        <td>{selectedReview?.title ?? selected.title}</td>
                        <td>Affects · supplied association</td>
                        <td>{id}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
          </section>
          <div id={`${prefix}-evidence`} hidden={dedicated && activeDetailView !== 'Evidence'}>
            <section>
              <h5>Explanation and impact</h5>
              <p>{selected.impact}</p>
              <p>
                <strong>Root cause:</strong> {selected.rootCause}
              </p>
              <p>
                <strong>Likelihood:</strong> {selected.likelihood}
              </p>
            </section>
            <section>
              <h5 id="finding-evidence-heading" tabIndex={-1}>
                Evidence and provenance
              </h5>
              <p className="field-note">
                These are already supplied synthetic facts and references. Protected raw evidence is
                not retrieved by this view.
              </p>
              <p>
                <strong>Evidence baseline:</strong> {selected.baselineId}
              </p>
              <List values={selected.facts} empty="No observed facts supplied." />
              <h6>Evidence references</h6>
              <List values={selected.evidenceReferences} empty="No evidence references supplied." />
              <h6>Affected objects</h6>
              <List values={selected.objectIds} empty="No affected objects supplied." />
            </section>
          </div>
          <section
            id={`${prefix}-recommendations`}
            hidden={dedicated && activeDetailView !== 'Recommendations'}
          >
            <h5>Original recommendation options</h5>
            <p className="warning-note">
              Synthetic · Review-only · Unverified. Finding review does not approve these options or
              validate remediation.
            </p>
            {selectedGuidance ? (
              selectedGuidance.options.map((option) => (
                <details key={option.scopedOptionId}>
                  <summary>{option.optionId} · Unverified</summary>
                  <p>{option.text}</p>
                  <dl className="finding-detail-facts">
                    <div>
                      <dt>Prerequisites</dt>
                      <dd>{option.prerequisites}</dd>
                    </div>
                    <div>
                      <dt>Risk</dt>
                      <dd>{option.risk}</dd>
                    </div>
                    <div>
                      <dt>Recovery guidance</dt>
                      <dd>{option.recoveryGuidance}</dd>
                    </div>
                  </dl>
                </details>
              ))
            ) : (
              <List
                values={selected.recommendations}
                empty="No original recommendation options supplied. No advice is inferred."
              />
            )}
            {selectedGuidance && !selectedGuidance.options.length && (
              <p>No original recommendation options supplied. No advice is inferred.</p>
            )}
            <h6>Validation guidance</h6>
            {selectedGuidance ? (
              <List
                values={selectedGuidance.validationGuidance}
                empty="No validation guidance supplied."
              />
            ) : (
              <p>{selected.validationGuidance || 'No validation guidance supplied.'}</p>
            )}
          </section>
          <div id={`${prefix}-original`} hidden={dedicated && activeDetailView !== 'Original'}>
            {dedicated && renderOriginal(selected)}
            <details>
              <summary>Inference, assumptions and limitations</summary>
              <h6>Inference</h6>
              <List
                values={selected.inferences}
                empty="None supplied by this deterministic fixture."
              />
              <h6>Assumptions</h6>
              <List values={selected.assumptions} empty="None supplied." />
              <h6>Limitations</h6>
              <List values={selected.limitations} empty="None supplied." />
            </details>
            <details>
              <summary>Original identity and fixture sources</summary>
              <p>
                <strong>Root-cause key:</strong> {selected.rootCauseKey}
              </p>
              <h6>Generated original digests</h6>
              <List values={selected.originalDigests} empty="None supplied." />
              <h6>Fixture rule sources</h6>
              <List values={selected.sources} empty="None supplied." />
              <h6>Approved fixture outcome links</h6>
              <List
                values={selected.outcomeIds}
                empty="None; no customer outcomes are approved by this demo."
              />
            </details>
          </div>
        </>
      )}
    </aside>
  );

  return (
    <section
      className={`findings-explorer subsection ${dedicated ? 'findings-dedicated' : ''} ${selected ? 'finding-is-open' : ''}`}
      aria-labelledby="findings-heading"
    >
      {!overlay && (
        <>
          <p className="eyebrow">Investigate / Fixed synthetic evidence</p>
          <p className="field-note">Filter the saved snapshot and inspect its supplied evidence.</p>
        </>
      )}
      {overlay ? (
        <div className="risk-toolbar">
          <div className="risk-severity-filters" role="group" aria-label="Severity filters">
            {['', 'Critical', 'High', 'Medium', 'Low', 'Informational'].map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={severity === value}
                onClick={() => update({ severity: value })}
              >
                {value || 'All findings'}
              </button>
            ))}
          </div>
          <div className="risk-category-row">
            <Filter
              id={`${prefix}-category`}
              label="Category"
              value={category}
              values={unique(findings.map((f) => f.category))}
              onChange={(value) => update({ category: value })}
            />
            <span role="status" aria-live="polite">
              {visible.length} of {findings.length} findings
            </span>
            {filtered && (
              <button type="button" className="risk-clear" onClick={clear}>
                Clear filters
              </button>
            )}
            <details className="risk-advanced-filters">
              <summary>More filters</summary>
              {filters}
            </details>
          </div>
        </div>
      ) : (
        filters
      )}
      {overlay && (
        <RiskDistribution
          findings={findings}
          category={category}
          severity={severity}
          onFilter={(value) => update(value)}
        />
      )}
      <div className={`finding-investigation ${selected ? 'has-selection' : ''}`}>
        <div className={`finding-results ${overlay ? 'risk-findings-panel' : ''}`}>
          <div className="risk-list-heading">
            <h4 id="findings-heading" tabIndex={-1} ref={resultsHeading}>
              Findings
            </h4>
            <span>{visible.length} matching</span>
          </div>
          {!findings.length ? (
            <p>
              No findings were generated by the executed fixture rules. Check coverage and
              limitations before interpreting this result.
            </p>
          ) : !visible.length ? (
            <p className="finding-empty">
              No findings match these filters. Clear finding filters to see the full saved snapshot.
            </p>
          ) : (
            (overlay ? visible.slice(0, visibleLimit) : visible).map((finding) => (
              <article
                className={`analysis-finding severity-${finding.severity.toLowerCase()}`}
                key={finding.id}
              >
                {!dedicated && <h4>{current(finding)?.title ?? finding.title}</h4>}
                {dedicated && (
                  <button
                    type="button"
                    className="inspect-finding finding-row-title"
                    id={inspectId(finding.id)}
                    aria-expanded={selectedId === finding.id}
                    aria-controls={`${prefix}-detail`}
                    onClick={() => select(finding)}
                  >
                    <span className="sr-only">Inspect </span>
                    <span className="risk-row-copy">
                      <span className={`risk-pill ${finding.severity.toLowerCase()}`}>
                        {finding.severity}
                      </span>
                      <strong>{current(finding)?.title ?? finding.title}</strong>
                      <small>
                        {findingState(finding)} · {labelCategory(finding.category)} ·{' '}
                        {finding.method}
                      </small>
                    </span>
                    <span aria-hidden="true">›</span>
                  </button>
                )}
                {!dedicated && (
                  <>
                    <p className="finding-row-meta">
                      <strong>{finding.severity}</strong> · {finding.method} · Confidence{' '}
                      {finding.confidencePercent}% · {finding.confidenceBand} ·{' '}
                      {findingState(finding)}
                      {needsReview(finding) ? ' · Mandatory review pending' : ''}
                    </p>
                    <p className="field-note">
                      {finding.category} · {finding.objectIds.length} affected synthetic objects ·{' '}
                      {finding.ruleId} / {finding.ruleVersion}
                    </p>
                  </>
                )}
                {!dedicated && (
                  <button
                    type="button"
                    className="inspect-finding"
                    id={inspectId(finding.id)}
                    aria-pressed={selectedId === finding.id}
                    aria-controls={`${prefix}-detail`}
                    onClick={() => select(finding)}
                  >
                    Inspect {current(finding)?.title ?? finding.title}
                  </button>
                )}
                <div hidden={dedicated}>{renderOriginal(finding)}</div>
              </article>
            ))
          )}
          {overlay && visible.length > visibleLimit && (
            <button
              type="button"
              className="risk-show-more"
              onClick={() => setVisibleLimit((value) => value + 8)}
            >
              Show more findings
            </button>
          )}
        </div>
        {overlay ? (
          <dialog
            ref={dialogRef}
            className="finding-overlay"
            aria-labelledby={`${prefix}-detail`}
            onCancel={(event) => {
              event.preventDefault();
              close();
            }}
            onClick={(event) => {
              if (event.target === event.currentTarget) close();
            }}
          >
            {detail}
            {selected && (
              <div className="risk-drawer-actions">
                <button
                  type="button"
                  onClick={() => setDetailView({ findingId: selected.id, view: 'Relationships' })}
                >
                  Explore relationships
                </button>
                <button
                  type="button"
                  onClick={() => setDetailView({ findingId: selected.id, view: 'Evidence' })}
                >
                  View evidence references
                </button>
                <p className="field-note">
                  Synthetic finding · supplied references · no live operations
                </p>
              </div>
            )}
          </dialog>
        ) : (
          detail
        )}
      </div>
    </section>
  );
}

function Filter({
  id,
  label,
  value,
  values,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  values: string[];
  onChange: (value: string) => void;
}) {
  return (
    <div>
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">All</option>
        {[...new Set([...values, ...(value ? [value] : [])])].map((item) => (
          <option key={item} value={item}>
            {item}
          </option>
        ))}
      </select>
    </div>
  );
}
function List({ values, empty }: { values: readonly string[]; empty: string }) {
  return values.length ? (
    <ul>
      {values.map((value, index) => (
        <li key={index}>{value}</li>
      ))}
    </ul>
  ) : (
    <p>{empty}</p>
  );
}
