import { useState } from 'react';
import { MaturityView } from './MaturityView';
import type {
  AnalysisScore,
  DraftContent,
  DraftFinding,
  ReportDraft,
} from './demo-contract.generated';

const score = (value: AnalysisScore) =>
  value.display === null ? 'Unavailable' : `${value.display} / 100 · ${value.status}`;

export function DraftReportView({ report }: { report: ReportDraft | null }) {
  const [view, setView] = useState<'summary' | 'technical' | 'markdown'>('summary');
  if (report === null) return null;
  const snapshot = report.snapshot;
  const markdown = report.markdown;
  if (
    report.status !== 'Ready' ||
    !snapshot ||
    !markdown ||
    snapshot.canonicalContentDigest !== markdown.canonicalContentDigest
  )
    return (
      <section className="draft-report" aria-label="Draft report preview">
        <h3>Draft report preview</h3>
        <p role="status">
          A coherent draft could not be verified. Refresh the saved analysis to retry.
        </p>
      </section>
    );
  const content = snapshot.content;
  const source = snapshot.source;
  return (
    <section className="draft-report" aria-labelledby={`draft-${source.runId}`}>
      <h3 id={`draft-${source.runId}`}>Draft report preview</h3>
      <p className="draft-warning">
        <strong>Synthetic draft · Unpublished</strong>
      </p>
      <p>
        This preview captures one saved review snapshot. Refresh after a review action to read a new
        current draft. The assessment remains at coverage readiness; this preview does not complete
        or publish it.
      </p>
      <p className="draft-digest">
        Canonical draft digest: <code>{snapshot.canonicalContentDigest}</code>
      </p>
      <div className="draft-view-controls" aria-label="Draft view">
        {(
          [
            ['summary', 'Summary draft'],
            ['technical', 'Technical draft'],
            ['markdown', 'Markdown preview'],
          ] as const
        ).map(([id, label]) => (
          <button key={id} aria-pressed={view === id} onClick={() => setView(id)}>
            {label}
          </button>
        ))}
      </div>
      {view === 'markdown' ? (
        <div className="draft-markdown">
          <h4>Structured Markdown text preview</h4>
          <p>Inert text from this canonical draft; supplied text cannot open links or run code.</p>
          <p className="draft-digest">
            Markdown byte digest: <code>{markdown.markdownSha256}</code>
          </p>
          <pre tabIndex={0} aria-label="Structured Markdown text">
            {markdown.markdownText}
          </pre>
        </div>
      ) : (
        <div className="draft-projection" data-draft-view={view}>
          <h4>
            {view === 'summary' ? 'Synthetic assessment summary' : 'Synthetic technical report'}
          </h4>
          <dl className="draft-kpis">
            <div>
              <dt>Current reviewed health</dt>
              <dd>{score(content.publishableCurrent)}</dd>
            </div>
            <div>
              <dt>Provisional health</dt>
              <dd>{score(content.provisional)}</dd>
            </div>
            <div>
              <dt>Independent maturity</dt>
              <dd>{content.maturity.level ?? 'Unavailable'}</dd>
            </div>
            <div>
              <dt>Mandatory review pending</dt>
              <dd>{content.quality.proposedReviewUnits} finding occurrences</dd>
            </div>
          </dl>
          <p className="field-note">
            {content.quality.gapUnits} explained gap units remain in the quality report. Unavailable
            health is neither zero nor 100. Maturity follows its own evidence gates.
          </p>
          <h4>Draft warnings</h4>
          <ul>
            {content.warnings.map((warning, index) => (
              <li key={index}>{warning}</li>
            ))}
          </ul>
          <h4>{view === 'summary' ? 'Critical and High findings' : 'All findings'}</h4>
          <DraftFindings
            findings={
              view === 'summary'
                ? content.findings.filter(
                    (finding) => finding.severity === 'Critical' || finding.severity === 'High',
                  )
                : content.findings
            }
            technical={view === 'technical'}
          />
          <h4>Category health</h4>
          <ScoreTable rows={content.categories} label="Category health" />
          {view === 'technical' && <TechnicalSections content={content} />}
          <details className="draft-quality">
            <summary>Separate assessment-quality details</summary>
            <dl>
              <dt>Planned units</dt>
              <dd>{content.quality.plannedUnits}</dd>
              <dt>Executed units</dt>
              <dd>{content.quality.executedUnits}</dd>
              <dt>Gap units</dt>
              <dd>{content.quality.gapUnits}</dd>
              <dt>Not applicable units</dt>
              <dd>{content.quality.notApplicableUnits}</dd>
              <dt>Finding units</dt>
              <dd>{content.quality.totalFindingUnits}</dd>
            </dl>
            <ul>
              {content.limitations.map((item) => (
                <li key={`${item.objectId}-${item.ruleId}`}>
                  {item.objectId} · {item.ruleId}: {item.state} · {item.reasonCode}
                </li>
              ))}
            </ul>
          </details>
          <h4>Methodology</h4>
          <ul>
            {content.methodology.map((item, index) => (
              <li key={index}>{item}</li>
            ))}
          </ul>
          <h4>Unavailable sections</h4>
          <ul>
            {content.unavailableSections.map((item, index) => (
              <li key={index}>{item}</li>
            ))}
          </ul>
        </div>
      )}
      <details className="draft-source">
        <summary>Captured source and reproducibility</summary>
        <dl>
          <dt>Run</dt>
          <dd>
            {source.runId} · revision {source.runRevision}
          </dd>
          <dt>Baseline</dt>
          <dd>
            {source.baselineId} · {source.analysisLock.presetVersion}
          </dd>
          <dt>Catalog</dt>
          <dd>{source.analysisLock.catalogVersion}</dd>
          <dt>Scoring profile</dt>
          <dd>
            {source.profileId} · {source.frozenVersions.profileVersion}
          </dd>
          <dt>Application</dt>
          <dd>{source.frozenVersions.applicationVersion}</dd>
          <dt>Scoring algorithm</dt>
          <dd>{source.frozenVersions.scoringAlgorithmVersion}</dd>
          <dt>Run input digest</dt>
          <dd>
            <code>{source.runInputDigest}</code>
          </dd>
          <dt>Review snapshot digest</dt>
          <dd>
            <code>{source.reviewSnapshotDigest}</code>
          </dd>
          <dt>Current score digest</dt>
          <dd>
            <code>{source.scoringContentDigest}</code>
          </dd>
          <dt>Maturity digest</dt>
          <dd>
            <code>{source.maturityContentDigest}</code>
          </dd>
        </dl>
      </details>
    </section>
  );
}

function ScoreTable({ rows, label }: { rows: DraftContent['objectTypes']; label: string }) {
  return rows.length ? (
    <div className="draft-table-wrap" tabIndex={0} role="region" aria-label={label}>
      <table>
        <caption>{label}</caption>
        <thead>
          <tr>
            <th scope="col">Scope</th>
            <th scope="col">Current reviewed</th>
            <th scope="col">Provisional</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id}>
              <th scope="row">{row.id}</th>
              <td>{score(row.publishableCurrent)}</td>
              <td>{score(row.provisional)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <p>No assessed results are available for this dimension.</p>
  );
}
function DraftFindings({
  findings,
  technical,
}: {
  findings: ReadonlyArray<DraftFinding>;
  technical: boolean;
}) {
  return findings.length ? (
    findings.map((finding) => (
      <article className="draft-finding" key={finding.id}>
        <h5>{finding.title}</h5>
        <p>
          {finding.severity} · {finding.state} · Confidence {finding.confidencePercent}% ·{' '}
          {finding.confidenceBand}
        </p>
        <p>
          {finding.objectIds.length} affected objects · captured revision {finding.revision}
        </p>
        {finding.reviewRequired && <p>Mandatory review pending</p>}
        {finding.businessContext && <p>Business context: {finding.businessContext}</p>}
        {technical && (
          <details>
            <summary>Original finding and technical provenance</summary>
            <p>
              Stable finding ID: <code>{finding.id}</code>
            </p>
            <p>
              Generated original: {finding.originalTitle} · {finding.initialState}
            </p>
            <p>
              Rule: {finding.ruleId} / {finding.ruleVersion} · Baseline: {finding.baselineId}
            </p>
            <p>
              Method: {finding.method} · Root cause: {finding.rootCause}
            </p>
            <p>
              Impact: {finding.impact} · Likelihood: {finding.likelihood}
            </p>
            <p>Inference: {finding.inferences.join('; ')}</p>
            <p>Assumptions: {finding.assumptions.join('; ')}</p>
            <ul>
              {finding.facts.map((fact, index) => (
                <li key={index}>{fact}</li>
              ))}
            </ul>
            <p>Affected objects: {finding.objectIds.join(', ')}</p>
            <p>Evidence references: {finding.evidenceReferences.join(', ')}</p>
            <p>Occurrence IDs: {finding.occurrenceIds.join(', ')}</p>
            <p>Original digests: {finding.originalDigests.join(', ')}</p>
            <p>Validation guidance: {finding.validationGuidance}</p>
            <p>Limitations: {finding.limitations.join('; ')}</p>
            <p>Fictional guidance sources: {finding.sources.join('; ')}</p>
            <p>Unverified fixture guidance; no execution:</p>
            <ul>
              {finding.recommendations.map((item, index) => (
                <li key={index}>{item}</li>
              ))}
            </ul>
          </details>
        )}
      </article>
    ))
  ) : (
    <p>No findings are available in this view. Review coverage before interpreting the result.</p>
  );
}
function TechnicalSections({ content }: { content: DraftContent }) {
  return (
    <>
      <h4>Object-type health</h4>
      <ScoreTable rows={content.objectTypes} label="Object-type health" />
      <h4>Module health</h4>
      <ScoreTable rows={content.modules} label="Module health" />
      <h4>Customer-approved outcome adherence</h4>
      <ScoreTable rows={content.outcomes} label="Customer-approved outcome adherence" />
      <MaturityView maturity={content.maturity} headingId="draft-maturity-heading" />
      <details>
        <summary>Healthy synthetic controls</summary>
        <ul>
          {content.healthyControls.map((item) => (
            <li key={`${item.objectId}-${item.ruleId}`}>
              {item.objectId} · {item.ruleId} / {item.ruleVersion} · {item.state}
            </li>
          ))}
        </ul>
      </details>
      <details>
        <summary>Captured finding review history</summary>
        {content.reviewHistory.map((finding) => (
          <article key={finding.findingId}>
            <h5>{finding.originalTitle}</h5>
            <p>
              Finding {finding.findingId} · revision {finding.revision}
            </p>
            {!finding.events.length ? (
              <p>No review events at this captured revision.</p>
            ) : (
              <ol>
                {finding.events.map((event) => (
                  <li key={event.eventId}>
                    {event.kind} · {event.state} · revision {event.revision} · {event.actorId} ·{' '}
                    {event.recordedAtUtc}
                    {event.reason && <p>Reason: {event.reason}</p>}
                    {event.text && <p>Comment: {event.text}</p>}
                    {event.title && <p>Presentation title: {event.title}</p>}
                    {event.businessContext && <p>Business context: {event.businessContext}</p>}
                  </li>
                ))}
              </ol>
            )}
          </article>
        ))}
      </details>
    </>
  );
}
