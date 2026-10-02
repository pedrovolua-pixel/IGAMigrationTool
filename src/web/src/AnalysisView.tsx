import { useEffect, useRef, useState } from 'react';
import type {
  AnalysisDetail,
  AnalysisScore,
  AnalysisScoreRow,
  RunDetail,
} from './demo-contract.generated';
import { request } from './api';
import { ReviewPanel } from './ReviewPanel';
import { MaturityView } from './MaturityView';

export function AnalysisView({ run, csrfToken }: { run: RunDetail; csrfToken: string }) {
  const [response, setResponse] = useState<AnalysisDetail | null>(null);
  const [failed, setFailed] = useState(false);
  const [retry, setRetry] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const retryFocus = useRef<string | null>(null);
  useEffect(() => {
    retryFocus.current = null;
    setNotice(null);
  }, [run.runId]);
  useEffect(() => {
    const controller = new AbortController();
    setResponse(null);
    setFailed(false);
    if (run.state === 'Scoring') {
      void request<AnalysisDetail>(
        `/runs/${encodeURIComponent(run.runId)}/analysis`,
        controller.signal,
      )
        .then((value) => {
          if (
            !controller.signal.aborted &&
            value.runId === run.runId &&
            value.runRevision === run.revision &&
            (!value.review ||
              (value.review.runId === value.runId &&
                value.review.runRevision === value.runRevision &&
                value.review.snapshotDigest === value.reviewSnapshotDigest))
          )
            setResponse(value);
          else if (!controller.signal.aborted) {
            retryFocus.current = null;
            setFailed(true);
          }
        })
        .catch(() => {
          if (!controller.signal.aborted) {
            retryFocus.current = null;
            setFailed(true);
          }
        });
    }
    return () => controller.abort();
  }, [run.runId, run.revision, run.state, retry]);
  useEffect(() => {
    if (response && retryFocus.current) {
      if (response.status === 'Ready' && response.runId === retryFocus.current)
        heading.current?.focus();
      retryFocus.current = null;
    }
  }, [response]);
  if (run.state !== 'Scoring') return null;
  if (!response || response.runId !== run.runId || response.runRevision !== run.revision) {
    return (
      <div className="field-note" role="status">
        <p>
          {failed
            ? 'Analysis could not be verified. Saved run inputs and results are unchanged.'
            : 'Reading analysis from the saved run…'}
        </p>
        {failed && (
          <button
            onClick={() => {
              retryFocus.current = run.runId;
              setRetry((value) => value + 1);
            }}
          >
            Retry analysis
          </button>
        )}
      </div>
    );
  }
  if (response.status !== 'Ready') {
    return (
      <p className="field-note">
        {response.reasonCode === 'coverage_only_fixture'
          ? 'This saved fixture demonstrates coverage only; health scoring remains unavailable.'
          : 'Analysis is unavailable for this saved run. No health result is inferred.'}
      </p>
    );
  }
  return (
    <section className="analysis-view subsection" aria-labelledby="analysis-heading">
      <p className="eyebrow">Deterministic analysis · synthetic fixtures</p>
      <h3 id="analysis-heading" tabIndex={-1} ref={heading}>
        Findings and reproducible health calculations
      </h3>
      {notice && (
        <p role="status" className="field-note">
          {notice}
        </p>
      )}
      <p className="field-note">
        These are local calculations from fixed synthetic evidence. The publishable-current
        calculation is not a published report. Review and maturity, when available, use frozen
        fictional fixtures; live assessment remains pending.
      </p>
      <div className="analysis-score-grid">
        <Score label="Provisional health" score={response.provisional!} />
        <Score label="Publishable-current health" score={response.publishableCurrent!} />
      </div>
      {!!response.warnings.length && (
        <div className="warning-note" role="note">
          <h4>Review and interpretation warnings</h4>
          <ul>
            {response.warnings.map((warning, index) => (
              <li key={index}>{warning}</li>
            ))}
          </ul>
        </div>
      )}
      <p className="field-note">
        Pass earns its full rule weight. Findings reduce health using the saved severity policy.
        Proposed Critical/High findings affect only provisional health until reviewed. Gaps reduce
        coverage, not default health. Categories without eligible units are not assessed; remaining
        category weights are renormalized separately for each calculation.
      </p>
      <div
        className="table-scroll"
        tabIndex={0}
        role="region"
        aria-label="Category health calculations"
      >
        <table>
          <caption>Category health and effective weights</caption>
          <thead>
            <tr>
              <th scope="col">Category</th>
              <th scope="col">Provisional</th>
              <th scope="col">Publishable-current</th>
              <th scope="col">Effective weights</th>
            </tr>
          </thead>
          <tbody>
            {response.categories.map((item) => (
              <tr key={item.id}>
                <th scope="row">{item.id}</th>
                <td>{scoreText(item.provisional)}</td>
                <td>{scoreText(item.publishableCurrent)}</td>
                <td>
                  {item.provisionalWeight ?? 'Not assessed'} /{' '}
                  {item.publishableWeight ?? 'Not assessed'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <details className="locked-inputs">
        <summary>Health by module, object type and approved fixture outcome</summary>
        <Dimension label="Installed modules" rows={response.modules} />
        <Dimension label="Object types" rows={response.objectTypes} />
        <Dimension label="Approved synthetic outcome fixtures" rows={response.outcomes} />
        <p className="field-note">
          An outcome fixture flag demonstrates the approved scoring boundary; it does not record
          customer approval. The same unit may appear in these views without being duplicated in
          overall health.
        </p>
      </details>
      {response.quality && (
        <section className="subsection">
          <h4>Separate assessment quality</h4>
          <dl className="count-list">
            <div>
              <dt>Executed applicable units</dt>
              <dd>{response.quality.executedUnits}</dd>
            </div>
            <div>
              <dt>Explained gaps</dt>
              <dd>{response.quality.gapUnits}</dd>
            </div>
            <div>
              <dt>Not applicable</dt>
              <dd>{response.quality.notApplicableUnits}</dd>
            </div>
            <div>
              <dt>Finding occurrences</dt>
              <dd>{response.quality.totalFindingUnits}</dd>
            </div>
            <div>
              <dt>Occurrences awaiting mandatory review</dt>
              <dd>{response.quality.proposedReviewUnits}</dd>
            </div>
          </dl>
          <p className="field-note">
            Quality is not blended into the health score. Gap reasons and executable coverage remain
            visible above.
          </p>
        </section>
      )}
      <section className="subsection">
        <h4>Generated findings</h4>
        {!response.findings.length ? (
          <p>
            No findings were generated by the executed fixture rules. Check coverage and limitations
            before interpreting this result.
          </p>
        ) : (
          response.findings.map((finding) => (
            <article
              className={`analysis-finding severity-${finding.severity.toLowerCase()}`}
              key={finding.id}
            >
              <h4>{finding.title}</h4>
              <p>
                <strong>{finding.severity}</strong> · Deterministic · Confidence{' '}
                {finding.confidencePercent}% · {finding.confidenceBand} · {finding.state}
                {finding.reviewRequired ? ' · Mandatory review pending' : ''}
              </p>
              <p className="field-note">
                {finding.category} · {finding.objectIds.length} affected synthetic objects ·{' '}
                {finding.ruleId} / {finding.ruleVersion}
              </p>
              <details>
                <summary>Read generated original and provenance</summary>
                <p>
                  <strong>Original title</strong>: {finding.originalTitle} · Initial state:{' '}
                  {finding.initialState}
                </p>
                <p>
                  <strong>Observed facts</strong>
                </p>
                <ul>
                  {finding.facts.map((fact, index) => (
                    <li key={index}>{fact}</li>
                  ))}
                </ul>
                <p>
                  <strong>Inference</strong>:{' '}
                  {finding.inferences.join('; ') || 'None supplied by this deterministic fixture.'}
                </p>
                <p>
                  <strong>Assumptions</strong>: {finding.assumptions.join('; ') || 'None supplied.'}
                </p>
                <p>
                  <strong>Impact</strong>: {finding.impact}
                </p>
                <p>
                  <strong>Root cause</strong>: {finding.rootCause}
                </p>
                <p>
                  <strong>Root-cause key</strong>: {finding.rootCauseKey}
                </p>
                <p>
                  <strong>Affected objects</strong>: {finding.objectIds.join(', ')}
                </p>
                <p>
                  <strong>Evidence baseline</strong>: {finding.baselineId}
                </p>
                <p>
                  <strong>Evidence references</strong>: {finding.evidenceReferences.join(', ')}
                </p>
                <p>
                  <strong>Recommendation options</strong>
                </p>
                <ul>
                  {finding.recommendations.map((item, index) => (
                    <li key={index}>{item}</li>
                  ))}
                </ul>
                <p>
                  <strong>Validation guidance</strong>: {finding.validationGuidance}
                </p>
                <p>
                  <strong>Fixture rule sources</strong>: {finding.sources.join('; ')}
                </p>
                <p>
                  <strong>Likelihood</strong>: {finding.likelihood}
                </p>
                <p>
                  <strong>Limitations</strong>: {finding.limitations.join('; ')}
                </p>
                <p>
                  <strong>Approved outcome links</strong>:{' '}
                  {finding.outcomeIds.join(', ') ||
                    'None; no customer outcomes are approved by this demo.'}
                </p>
                <p>
                  <strong>Generated original digests</strong>: {finding.originalDigests.join(', ')}
                </p>

                <p className="field-note">
                  The generated original is read-only. Available synthetic review actions appear
                  below. Risk acceptance and closure remain unavailable.
                </p>
              </details>
            </article>
          ))
        )}
      </section>
      {response.review && (
        <ReviewPanel
          review={response.review}
          csrfToken={csrfToken}
          onReload={(message) => {
            setNotice(message ?? 'Saved analysis refreshed.');
            retryFocus.current = run.runId;
            setResponse(null);
            setRetry((value) => value + 1);
          }}
        />
      )}
      {response.maturity && <MaturityView maturity={response.maturity} />}
      <details className="locked-inputs">
        <summary>Analysis versions and content digests</summary>
        <dl>
          <div>
            <dt>Health algorithm</dt>
            <dd>{response.algorithmVersion}</dd>
          </div>
          <div>
            <dt>Frozen fixture contents</dt>
            <dd>
              <code>{response.fixtureDigest}</code>
            </dd>
          </div>
          <div>
            <dt>Reproducible analysis result</dt>
            <dd>
              <code>{response.contentDigest}</code>
            </dd>
          </div>
          {response.reviewSnapshotDigest && (
            <div>
              <dt>Reviewed snapshot</dt>
              <dd>
                <code>{response.reviewSnapshotDigest}</code>
              </dd>
            </div>
          )}
        </dl>
        <p className="field-note">
          Exact saved input locks and canonical coverage results determine this read-only analysis.
          Later fixture changes are refused rather than substituted.
        </p>
      </details>
    </section>
  );
}
function scoreText(score: AnalysisScore) {
  return score.display === null ? 'Not assessed' : `${score.display} / 100 · ${score.status}`;
}
function Score({ label, score }: { label: string; score: AnalysisScore }) {
  return (
    <div className={`analysis-score score-${score.status.toLowerCase()}`}>
      <h4>{label}</h4>
      <strong>{score.display ?? 'Unavailable'}</strong>
      <p>
        {score.display === null
          ? 'No eligible units; not 100.'
          : `${score.status} · ${score.eligibleUnits} eligible units`}
      </p>
    </div>
  );
}
function Dimension({ label, rows }: { label: string; rows: ReadonlyArray<AnalysisScoreRow> }) {
  return (
    <section>
      <h4>{label}</h4>
      {rows.length ? (
        <dl className="count-list">
          {rows.map((row) => (
            <div key={row.id}>
              <dt>{row.id}</dt>
              <dd>
                {scoreText(row.provisional)} / {scoreText(row.publishableCurrent)}
              </dd>
            </div>
          ))}
        </dl>
      ) : (
        <p>Not assessed; no eligible fixture units.</p>
      )}
    </section>
  );
}
