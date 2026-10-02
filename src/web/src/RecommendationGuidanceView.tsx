import type {
  GuidanceFinding,
  GuidanceOption,
  GuidanceSourceBinding,
  RecommendationGuidance,
} from './demo-contract.generated';
import './RecommendationGuidanceView.css';

export function RecommendationGuidanceView({
  guidance,
}: {
  guidance: RecommendationGuidance | null;
}) {
  if (guidance === null) return null;
  const snapshot = guidance.snapshot;
  if (
    guidance.status !== 'Ready' ||
    guidance.reasonCode !== null ||
    !snapshot ||
    snapshot.schemaVersion !== 'synthetic-recommendation-guidance-v1' ||
    snapshot.status !== 'SyntheticUnverified' ||
    snapshot.findings.some((finding) =>
      finding.options.some((option) => option.status !== 'Unverified'),
    )
  )
    return (
      <section className="recommendation-guidance" aria-label="Recommendation guidance">
        <h3>Recommendation guidance</h3>
        <p role="status">
          Coherent recommendation guidance is unavailable. Refresh the saved analysis to retry. No
          advice is inferred or substituted.
        </p>
        {guidance.reasonCode !== null && (
          <p>
            Reason: <code>{guidance.reasonCode}</code>
          </p>
        )}
      </section>
    );

  const optionCount = snapshot.findings.reduce(
    (count, finding) => count + finding.options.length,
    0,
  );
  const headingId = `guidance-${snapshot.source.runId}`;
  return (
    <section className="recommendation-guidance" aria-labelledby={headingId}>
      <p className="eyebrow">Structured fictional options</p>
      <h3 id={headingId}>Recommendation guidance</h3>
      <div className="guidance-warning" role="note">
        <strong>Synthetic · Review-only · Unverified</strong>
        <p>
          These original fixture options are inert guidance. Confirming, rejecting or deferring a
          finding does not review its recommendations or validate remediation. This guidance does
          not represent an executed or verified customer-system restore.
        </p>
      </div>
      <p className="guidance-count">
        {optionCount} unverified {optionCount === 1 ? 'option' : 'options'} across{' '}
        {snapshot.findings.length} {snapshot.findings.length === 1 ? 'finding' : 'findings'}.{' '}
        Options follow stable identity order; this is not a priority or effort ranking.
      </p>
      {snapshot.findings.length === 0 ? (
        <p className="guidance-empty" role="status">
          No finding recommendation options exist in this captured synthetic snapshot. Healthy
          controls and evidence gaps do not generate advice; absent evidence is not proof of health.
        </p>
      ) : (
        snapshot.findings.map((finding) => (
          <FindingGuidance
            key={finding.findingId}
            finding={finding}
            runId={snapshot.source.runId}
          />
        ))
      )}
      <details className="guidance-disclosure">
        <summary>Guidance warnings and unavailable capabilities</summary>
        <h4>Captured warnings</h4>
        <GuidanceList values={snapshot.warnings} empty="No additional captured warnings." />
        <h4>Unavailable capabilities</h4>
        <GuidanceList
          values={snapshot.unavailableSections}
          empty="No additional sections supplied."
        />
      </details>
      <details className="guidance-disclosure guidance-source">
        <summary>Captured guidance source, versions and digest</summary>
        <p>
          One saved analysis and review snapshot supplies these options. Refresh after a review
          action to read a new current value. This snapshot does not establish durable
          recommendation history, a completed assessment or publication. Digests identify content
          and grant no access.
        </p>
        <dl>
          <div>
            <dt>Guidance schema</dt>
            <dd>{snapshot.schemaVersion}</dd>
          </div>
          <div>
            <dt>Guidance status</dt>
            <dd>{snapshot.status}</dd>
          </div>
          <div>
            <dt>Canonical guidance content digest</dt>
            <dd>
              <code>{snapshot.contentDigest}</code>
            </dd>
          </div>
        </dl>
        <SourceBindings source={snapshot.source} />
      </details>
    </section>
  );
}

function FindingGuidance({ finding, runId }: { finding: GuidanceFinding; runId: string }) {
  const currentHeading = `guidance-current-${runId}-${finding.findingId}`;
  const originalHeading = `guidance-original-${runId}-${finding.findingId}`;
  return (
    <details className="guidance-finding">
      <summary>
        {finding.presentationTitle} · {finding.severity} · {finding.currentState} ·{' '}
        {finding.options.length} unverified {finding.options.length === 1 ? 'option' : 'options'}
      </summary>
      <section className="guidance-current" aria-labelledby={currentHeading}>
        <h4 id={currentHeading}>Current finding presentation</h4>
        <dl>
          <div>
            <dt>Current title</dt>
            <dd className="guidance-text">{finding.presentationTitle}</dd>
          </div>
          <div>
            <dt>Current review state</dt>
            <dd>{finding.currentState}</dd>
          </div>
          <div>
            <dt>Finding revision</dt>
            <dd>{finding.findingRevision}</dd>
          </div>
          <div>
            <dt>Business context</dt>
            <dd className="guidance-text">
              {finding.businessContext || 'No business context supplied.'}
            </dd>
          </div>
        </dl>
        {finding.currentState === 'Rejected' && (
          <p className="field-note">
            This finding is rejected. Its original recommendation options remain visible and
            unverified; rejection does not rewrite or remove them.
          </p>
        )}
      </section>
      <section className="guidance-original" aria-labelledby={originalHeading}>
        <h4 id={originalHeading}>Immutable original and affected scope</h4>
        <dl>
          <div>
            <dt>Original title</dt>
            <dd className="guidance-text">{finding.originalTitle}</dd>
          </div>
          <div>
            <dt>Finding identity</dt>
            <dd>
              <code>{finding.findingId}</code>
            </dd>
          </div>
          <div>
            <dt>Original state</dt>
            <dd>{finding.initialState}</dd>
          </div>
          <div>
            <dt>Rule and version</dt>
            <dd>
              {finding.ruleId} · {finding.ruleVersion}
            </dd>
          </div>
          <div>
            <dt>Category</dt>
            <dd>{finding.categoryId}</dd>
          </div>
          <div>
            <dt>Severity</dt>
            <dd>{finding.severity}</dd>
          </div>
          <div>
            <dt>Root cause</dt>
            <dd className="guidance-text">{finding.rootCause}</dd>
          </div>
        </dl>
        <details className="guidance-disclosure">
          <summary>
            Original occurrences and evidence provenance ({finding.occurrences.length})
          </summary>
          <p>
            Protected references are inert identifiers; this view cannot resolve or open evidence.
          </p>
          <div
            className="guidance-table-scroll"
            tabIndex={0}
            role="region"
            aria-label={`Guidance original occurrences ${finding.findingId}`}
          >
            <table>
              <caption>Original occurrence, object and evidence bindings</caption>
              <thead>
                <tr>
                  <th scope="col">Occurrence</th>
                  <th scope="col">Affected scope</th>
                  <th scope="col">Original provenance</th>
                </tr>
              </thead>
              <tbody>
                {finding.occurrences.map((occurrence) => (
                  <tr key={occurrence.occurrenceId}>
                    <th scope="row">
                      <code>{occurrence.occurrenceId}</code>
                    </th>
                    <td>
                      <dl>
                        <dt>Object</dt>
                        <dd className="guidance-text">{occurrence.objectId}</dd>
                        <dt>Object type</dt>
                        <dd className="guidance-text">{occurrence.objectType}</dd>
                        <dt>Module</dt>
                        <dd className="guidance-text">{occurrence.moduleId}</dd>
                      </dl>
                    </td>
                    <td>
                      <dl>
                        <dt>Generated original digest</dt>
                        <dd>
                          <code>{occurrence.originalDigest}</code>
                        </dd>
                        <dt>Evidence reference</dt>
                        <dd className="guidance-text">{occurrence.evidenceReference}</dd>
                      </dl>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      </section>
      <h4>Original unverified recommendation options</h4>
      {finding.options.length === 0 ? (
        <p>No recommendation options were supplied for this finding. No advice is inferred.</p>
      ) : (
        finding.options.map((option) => (
          <OptionGuidance key={option.scopedOptionId} option={option} />
        ))
      )}
      <details className="guidance-disclosure">
        <summary>Validation, fixture references, assumptions and limitations</summary>
        <h4>Validation guidance</h4>
        <GuidanceList
          values={finding.validationGuidance}
          empty="No validation guidance supplied."
        />
        <h4>Authoritative fixture references</h4>
        <GuidanceList values={finding.guidanceReferences} empty="No fixture references supplied." />
        <h4>Assumptions</h4>
        <GuidanceList values={finding.assumptions} empty="No assumptions supplied." />
        <h4>Limitations</h4>
        <GuidanceList values={finding.limitations} empty="No limitations supplied." />
      </details>
    </details>
  );
}

function OptionGuidance({ option }: { option: GuidanceOption }) {
  return (
    <details className="guidance-option">
      <summary>{option.optionId} · Unverified</summary>
      <dl>
        <div>
          <dt>Scoped option identity</dt>
          <dd>
            <code>{option.scopedOptionId}</code>
          </dd>
        </div>
        <div>
          <dt>Original option ID</dt>
          <dd className="guidance-text">{option.optionId}</dd>
        </div>
        <div>
          <dt>Recommendation status</dt>
          <dd>{option.status}</dd>
        </div>
        <div>
          <dt>Option guidance</dt>
          <dd className="guidance-text">{option.text}</dd>
        </div>
        <div>
          <dt>Prerequisites</dt>
          <dd className="guidance-text">{option.prerequisites}</dd>
        </div>
        <div>
          <dt>Risk</dt>
          <dd className="guidance-text">{option.risk}</dd>
        </div>
        <div>
          <dt>Recovery guidance</dt>
          <dd className="guidance-text">{option.recoveryGuidance}</dd>
        </div>
      </dl>
    </details>
  );
}

function GuidanceList({ values, empty }: { values: ReadonlyArray<string>; empty: string }) {
  return values.length === 0 ? (
    <p>{empty}</p>
  ) : (
    <ul className="guidance-text">
      {values.map((value, index) => (
        <li key={index}>{value}</li>
      ))}
    </ul>
  );
}

function SourceBindings({ source }: { source: GuidanceSourceBinding }) {
  const rows: Array<[string, string]> = [];
  const collect = (value: unknown, path: string) => {
    if (Array.isArray(value)) {
      if (value.length === 0) rows.push([path, 'No values supplied.']);
      else value.forEach((item, index) => collect(item, `${path}[${index}]`));
    } else if (value !== null && typeof value === 'object') {
      Object.entries(value).forEach(([key, item]) => collect(item, path ? `${path}.${key}` : key));
    } else rows.push([path, value === null ? 'Not set' : String(value)]);
  };
  collect(source, '');
  return (
    <dl className="guidance-bindings">
      {rows.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>
            <code>{value}</code>
          </dd>
        </div>
      ))}
    </dl>
  );
}
