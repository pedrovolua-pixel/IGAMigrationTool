import type { MaturityDetail } from './demo-contract.generated';

export function MaturityView({
  maturity,
  headingId = 'maturity-heading',
}: {
  maturity: MaturityDetail;
  headingId?: string;
}) {
  if (maturity.status !== 'Ready')
    return (
      <p className="warning-note">Maturity is unavailable. No capability level is inferred.</p>
    );
  return (
    <section className="subsection" aria-labelledby={headingId}>
      <p className="eyebrow">Separate evidence-based capability maturity</p>
      <h4 id={headingId}>Maturity: {maturity.level}</h4>
      <p className="field-note">
        Maturity is independent of health. Every one of the {maturity.mandatoryDomains} declared
        mandatory domains stays in the denominator, including missing or partial evidence. Higher
        levels require every preceding level and evidenced governance ownership for Defined and
        above.
      </p>
      <div className="warning-note" role="note">
        <strong>Evidence limitations</strong>
        <p>
          {maturity.insufficientIndicators} insufficient indicators across{' '}
          {maturity.insufficientDomains} domains. {maturity.improvementMissingDistinctAssessments}{' '}
          improvement indicators lack two distinct assessments. Partial or insufficient evidence
          never counts as met.
        </p>
        <p>{maturity.authorityBoundary}</p>
      </div>
      <div className="table-scroll" tabIndex={0} role="region" aria-label="Maturity thresholds">
        <table>
          <caption>Cumulative maturity evidence thresholds</caption>
          <thead>
            <tr>
              <th scope="col">Level</th>
              <th scope="col">Met domains</th>
              <th scope="col">Required</th>
              <th scope="col">Threshold</th>
            </tr>
          </thead>
          <tbody>
            {maturity.gates.map((gate) => (
              <tr key={gate.level}>
                <th scope="row">{gate.level}</th>
                <td>
                  {gate.metDomains} / {gate.mandatoryDomains}
                </td>
                <td>{gate.requiredPercent}%</td>
                <td>{gate.isMet ? 'Met' : 'Not met'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="field-note">
        A met threshold alone does not grant a higher level. Governance ownership:{' '}
        {maturity.governanceOwnershipEvidenced ? 'Evidenced' : 'Not evidenced'}.
      </p>
      <p className="field-note">
        Developing and Defined count domains with both documented design and repeatable
        implementation. Managed counts measured operation with regular review. Optimized counts
        validated improvement across at least two distinct assessments.
      </p>
      {maturity.ownership && (
        <details>
          <summary>Governance ownership evidence</summary>
          <p>
            {maturity.ownership.state} · {maturity.ownership.ownerId ?? 'No evidenced owner'}
          </p>
          <p>{maturity.ownership.reasonCode}</p>
          <ul>
            {maturity.ownership.evidenceReferences.map((reference) => (
              <li key={reference}>{reference}</li>
            ))}
          </ul>
        </details>
      )}
      {maturity.domains.map((domain) => (
        <details key={domain.id} className="maturity-domain">
          <summary>
            {domain.name} · {domain.insufficientIndicators} insufficient indicators
          </summary>
          <p>
            Design and implementation: {domain.baseMet ? 'Met' : 'Not met'} · Operation and regular
            review: {domain.operationAndReviewMet ? 'Met' : 'Not met'} · Validated improvement:{' '}
            {domain.improvementMet ? 'Met' : 'Not met'}
          </p>
          <ul>
            {domain.indicators.map((indicator) => (
              <li key={indicator.kind}>
                <strong>{indicator.kind}</strong>: {indicator.state}
                {indicator.reasonCode ? ` · ${indicator.reasonCode}` : ''}
                <p>Evidence: {indicator.evidenceReferences.join(', ') || 'Unavailable'}</p>
                {indicator.assessmentReferences.length > 0 && (
                  <p>
                    Assessment references: {indicator.assessmentReferences.join(', ')} · Validation
                    premise:{' '}
                    {indicator.hasValidatedImprovementEvidence
                      ? 'Explicit synthetic evidence'
                      : 'Not established'}
                  </p>
                )}
              </li>
            ))}
          </ul>
        </details>
      ))}
      <details className="locked-inputs">
        <summary>Frozen maturity versions and digests</summary>
        <p>
          {maturity.algorithmVersion} / {maturity.catalogVersion}
        </p>
        <p>
          Frozen input: <code>{maturity.inputDigest}</code>
        </p>
        <p>
          Projection: <code>{maturity.contentDigest}</code>
        </p>
      </details>
    </section>
  );
}
