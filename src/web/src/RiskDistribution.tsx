import { useId } from 'react';
import type { AnalysisFinding } from './demo-contract.generated';
import './RiskDistribution.css';

const severities = ['Critical', 'High', 'Medium', 'Low', 'Informational'] as const;

export function RiskDistribution({
  findings,
  category,
  severity,
  onFilter,
}: {
  findings: readonly AnalysisFinding[];
  category: string;
  severity: string;
  onFilter: (value: { category?: string; severity?: string }) => void;
}) {
  const prefix = useId();
  const selectedCategory = category === 'All' ? '' : category;
  const categories = [...new Set(findings.map((finding) => finding.category))].sort((a, b) =>
    a.localeCompare(b),
  );
  const categoryFindings = selectedCategory
    ? findings.filter((finding) => finding.category === selectedCategory)
    : findings;
  const severityCounts = severities.map(
    (level) => categoryFindings.filter((finding) => finding.severity === level).length,
  );
  const maximum = Math.max(0, ...severityCounts);

  return (
    <div className="risk-distribution">
      <section className="risk-distribution-panel" aria-labelledby={`${prefix}-severity`}>
        <h3 id={`${prefix}-severity`}>Severity distribution</h3>
        <div className="risk-distribution-bars" aria-label="Counts by selected category">
          {severities.map((level, index) => {
            const count = severityCounts[index] ?? 0;
            return (
              <button
                type="button"
                className={`risk-distribution-bar risk-severity-${level.toLowerCase()}`}
                key={level}
                aria-label={`${level} findings, ${count}${selectedCategory ? ` in ${selectedCategory}` : ''}`}
                aria-pressed={severity === level}
                onClick={() => onFilter({ severity: level })}
              >
                <span>{level}</span>
                <span className="risk-distribution-track" aria-hidden="true">
                  <span style={{ width: `${maximum ? (count / maximum) * 100 : 0}%` }} />
                </span>
                <b>{count}</b>
              </button>
            );
          })}
        </div>
        <p className="risk-distribution-caption">Count by selected category · scale 0–{maximum}</p>
        {!findings.length && (
          <p className="risk-distribution-empty">No findings in this assessment.</p>
        )}
      </section>
      <section className="risk-distribution-panel" aria-labelledby={`${prefix}-hotspots`}>
        <h3 id={`${prefix}-hotspots`}>Category hotspots</h3>
        {categories.length ? (
          <div
            className="risk-distribution-heat-scroll"
            tabIndex={0}
            role="region"
            aria-label="Finding counts by category and severity; scroll horizontally when needed"
          >
            <table className="risk-distribution-heat">
              <caption className="risk-distribution-sr-only">
                Finding counts by category and severity
              </caption>
              <thead>
                <tr>
                  <th scope="col">
                    <span className="risk-distribution-sr-only">Category</span>
                  </th>
                  {severities.map((level) => (
                    <th key={level} scope="col">
                      {level}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {categories.map((name) => (
                  <tr key={name}>
                    <th scope="row">{name}</th>
                    {severities.map((level) => {
                      const count = findings.filter(
                        (finding) => finding.category === name && finding.severity === level,
                      ).length;
                      return (
                        <td key={level}>
                          <button
                            type="button"
                            className={`risk-distribution-cell risk-severity-${level.toLowerCase()}`}
                            aria-label={`${name}, ${level}, ${count} findings`}
                            aria-pressed={selectedCategory === name && severity === level}
                            disabled={count === 0}
                            onClick={() => onFilter({ category: name, severity: level })}
                          >
                            {count}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="risk-distribution-empty">No category hotspots to display.</p>
        )}
        <p className="risk-distribution-caption">Select a cell to filter category + severity</p>
      </section>
    </div>
  );
}
