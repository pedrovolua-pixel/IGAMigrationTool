import type { AnalysisDetail, AnalysisScore, RunDetail } from './demo-contract.generated';
import './PlatformOverview.css';

const severities = ['Critical', 'High', 'Medium', 'Low', 'Informational'] as const;

function scoreValue(score: AnalysisScore | null | undefined): number | null {
  if (!score || score.status === 'Unavailable') return null;
  const source = score.raw ?? score.display;
  if (source === null || source.trim() === '') return null;
  const value = Number(source);
  return Number.isFinite(value) && value >= 0 && value <= 100 ? value : null;
}

function scoreLabel(score: AnalysisScore | null | undefined): string {
  return scoreValue(score) === null
    ? 'Unavailable'
    : (score?.display ?? score?.raw ?? 'Unavailable');
}

function categoryLabel(id: string): string {
  return id
    .replace(/[_-]/g, ' ')
    .replace(/\b\w+/g, (word) => word[0]!.toUpperCase() + word.slice(1).toLowerCase());
}

function radarPoint(index: number, total: number, fraction: number) {
  const angle = -Math.PI / 2 + (index * Math.PI * 2) / total;
  return {
    x: 160 + Math.cos(angle) * 75 * fraction,
    y: 130 + Math.sin(angle) * 75 * fraction,
  };
}

/** Presentation only. The controller admits the DTO; this view never resolves evidence. */
export function PlatformOverview({
  analysis,
  run,
  onNavigate,
  onInspectCategory,
}: {
  analysis: AnalysisDetail | null;
  run: RunDetail | null;
  onNavigate: (view: string) => void;
  onInspectCategory?: (category: string) => void;
}) {
  const current =
    run &&
    analysis?.status === 'Ready' &&
    analysis.runId === run.runId &&
    analysis.runRevision === run.revision
      ? analysis
      : null;
  const value = scoreValue(current?.provisional);
  const findings = current?.findings ?? [];
  const review = current?.review?.status === 'Ready' ? current.review : null;
  const reviewedStates = new Map(review?.findings.map((finding) => [finding.id, finding.state]));
  const proposed = findings.filter(
    (finding) => (reviewedStates.get(finding.id) ?? finding.state).toLowerCase() === 'proposed',
  );
  const priorityProposed = proposed.filter(
    (finding) => finding.severity === 'Critical' || finding.severity === 'High',
  );
  const severityCounts = severities.map((severity) => ({
    severity,
    count: findings.filter((finding) => finding.severity === severity).length,
  }));
  const maximumSeverity = Math.max(1, ...severityCounts.map((item) => item.count));
  const hotspots = [...new Set(findings.map((finding) => finding.category))]
    .map((category) => ({
      category,
      count: findings.filter((finding) => finding.category === category).length,
    }))
    .sort((a, b) => b.count - a.count || a.category.localeCompare(b.category));
  const quality = current?.quality;
  const coverage = run?.executableCoverage;
  const maturity = current?.maturity?.status === 'Ready' ? current.maturity.level : null;
  const categories = current?.categories ?? [];
  const radarAvailable =
    categories.length >= 3 &&
    categories.every((category) => scoreValue(category.provisional) !== null);
  const radarPoints = (fraction: number) =>
    categories
      .map((_, index) => {
        const point = radarPoint(index, categories.length, fraction);
        return `${point.x},${point.y}`;
      })
      .join(' ');

  return (
    <section className="platform-overview" aria-labelledby="platform-overview-heading">
      <header className="platform-overview-heading">
        <div>
          <h2 id="platform-overview-heading" tabIndex={-1}>
            Your assessment at a glance
          </h2>
          <p>Health, priority risks, and what needs review next.</p>
        </div>
        {run && <span className="platform-overview-tag">{run.selection.scopeLabel}</span>}
      </header>

      {!current && (
        <p className="platform-overview-notice" role="status">
          {run
            ? 'Verified health analysis is unavailable for this run. Saved run coverage remains separate.'
            : 'Open or start a synthetic assessment to see its verified results.'}
        </p>
      )}

      <div className="platform-overview-grid">
        <section className="platform-overview-panel" aria-labelledby="overview-health-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-health-heading">Overall health</h3>
            <span className="platform-overview-tag">Provisional</span>
          </div>
          <div className="platform-overview-health">
            <div className="platform-overview-gauge">
              <svg viewBox="0 0 160 160" aria-hidden="true" focusable="false">
                <circle cx="80" cy="80" r="66" className="platform-overview-gauge-track" />
                {value !== null && (
                  <circle
                    cx="80"
                    cy="80"
                    r="66"
                    pathLength="100"
                    strokeDasharray={`${value} 100`}
                    transform="rotate(-90 80 80)"
                    className="platform-overview-gauge-value"
                  />
                )}
              </svg>
              <div className="platform-overview-gauge-label">
                <strong>{value === null ? '—' : scoreLabel(current?.provisional)}</strong>
                <span>{value === null ? 'Score unavailable' : 'out of 100'}</span>
              </div>
            </div>
            <div className="platform-overview-health-context">
              <strong>
                {value === null ? 'Health unavailable' : `${current?.provisional?.status} status`}
              </strong>
              <p>Current publishable score: {scoreLabel(current?.publishableCurrent)}</p>
              <button type="button" onClick={() => onNavigate('Outcomes & maturity')}>
                {maturity ? `Maturity: ${maturity}` : 'Explore outcomes & maturity'}
                <span aria-hidden="true"> →</span>
              </button>
            </div>
          </div>
          <p className="platform-overview-footnote">
            Provisional and publishable scores stay separate from assessment quality.
          </p>
        </section>

        <section className="platform-overview-panel" aria-labelledby="overview-category-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-category-heading">Health by category</h3>
            <span className="platform-overview-caption">0–100 scale</span>
          </div>
          {current && current.categories.length > 0 ? (
            <>
              {radarAvailable ? (
                <svg
                  className="platform-overview-radar"
                  viewBox="0 0 320 260"
                  aria-hidden="true"
                  focusable="false"
                >
                  {[0.25, 0.5, 0.75, 1].map((fraction) => (
                    <polygon
                      key={fraction}
                      points={radarPoints(fraction)}
                      className="platform-overview-radar-grid"
                    />
                  ))}
                  {categories.map((category, index) => {
                    const point = radarPoint(index, categories.length, 1);
                    return (
                      <line
                        key={category.id}
                        x1="160"
                        y1="130"
                        x2={point.x}
                        y2={point.y}
                        className="platform-overview-radar-grid"
                      />
                    );
                  })}
                  <polygon
                    className="platform-overview-radar-value"
                    points={categories
                      .map((category, index) => {
                        const point = radarPoint(
                          index,
                          categories.length,
                          scoreValue(category.provisional)! / 100,
                        );
                        return `${point.x},${point.y}`;
                      })
                      .join(' ')}
                  />
                  {categories.map((category, index) => {
                    const point = radarPoint(
                      index,
                      categories.length,
                      scoreValue(category.provisional)! / 100,
                    );
                    const label = radarPoint(index, categories.length, 1.45);
                    const name = categoryLabel(category.id);
                    return (
                      <g key={category.id}>
                        <circle
                          cx={point.x}
                          cy={point.y}
                          r="3"
                          className="platform-overview-radar-point"
                        />
                        <text
                          x={label.x}
                          y={label.y - 3}
                          textAnchor="middle"
                          className="platform-overview-radar-label"
                        >
                          <tspan x={label.x}>
                            {name.length > 20 ? `${name.slice(0, 18)}…` : name}
                          </tspan>
                          <tspan x={label.x} dy="14">
                            {scoreLabel(category.provisional)}
                          </tspan>
                        </text>
                      </g>
                    );
                  })}
                </svg>
              ) : (
                <div className="platform-overview-category-chart" aria-hidden="true">
                  {current.categories.map((category) => {
                    const categoryValue = scoreValue(category.provisional);
                    return (
                      <div className="platform-overview-category-row" key={category.id}>
                        <span>{categoryLabel(category.id)}</span>
                        <div className="platform-overview-bar-track">
                          {categoryValue !== null && (
                            <span style={{ width: `${categoryValue}%` }} />
                          )}
                        </div>
                        <strong>
                          {categoryValue === null ? '—' : scoreLabel(category.provisional)}
                        </strong>
                      </div>
                    );
                  })}
                </div>
              )}
              <details className="platform-overview-chart-details">
                <summary>Category score table</summary>
                <div
                  className="platform-overview-table-scroll"
                  tabIndex={0}
                  role="region"
                  aria-label="Category scores"
                >
                  <table>
                    <caption>Verified category results</caption>
                    <thead>
                      <tr>
                        <th scope="col">Category</th>
                        <th scope="col">Provisional</th>
                        <th scope="col">Publishable</th>
                      </tr>
                    </thead>
                    <tbody>
                      {current.categories.map((category) => (
                        <tr key={category.id}>
                          <th scope="row">{categoryLabel(category.id)}</th>
                          <td>{scoreLabel(category.provisional)}</td>
                          <td>{scoreLabel(category.publishableCurrent)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
              <p className="platform-overview-footnote">
                Unavailable categories have no score or bar.
              </p>
            </>
          ) : (
            <p className="platform-overview-empty">Category scores are unavailable for this run.</p>
          )}
        </section>

        <section className="platform-overview-panel" aria-labelledby="overview-history-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-history-heading">Health over time</h3>
            <span className="platform-overview-caption">Comparable runs</span>
          </div>
          <div className="platform-overview-history-empty">
            <span aria-hidden="true">↗</span>
            <p>Historical health is unavailable.</p>
            <p>A saved run list does not establish comparable health scores.</p>
          </div>
          <button
            className="platform-overview-link"
            type="button"
            onClick={() => onNavigate('Compare runs')}
          >
            Open run comparison <span aria-hidden="true">→</span>
          </button>
        </section>

        <section className="platform-overview-panel" aria-labelledby="overview-hotspots-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-hotspots-heading">Category hotspots</h3>
            {current && <span className="platform-overview-tag">{findings.length} findings</span>}
          </div>
          {current && hotspots.length > 0 ? (
            <ul className="platform-overview-hotspots">
              {hotspots.map((item) => (
                <li key={item.category}>
                  <button
                    type="button"
                    onClick={() =>
                      onInspectCategory ? onInspectCategory(item.category) : onNavigate('Findings')
                    }
                  >
                    <span>
                      <strong>{categoryLabel(item.category)}</strong>
                      <small>
                        {onInspectCategory ? 'View category findings' : 'Open risk analysis'}
                      </small>
                    </span>
                    <span className="platform-overview-hotspot-count">
                      {item.count} <span aria-hidden="true">→</span>
                      <span className="platform-overview-screen-reader"> findings</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="platform-overview-empty">
              {current
                ? 'No findings were recorded in this analysis.'
                : 'Verified category findings are unavailable.'}
            </p>
          )}
          <p className="platform-overview-footnote">
            Recorded findings include retained review history.
          </p>
        </section>

        <section className="platform-overview-panel" aria-labelledby="overview-severity-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-severity-heading">Findings by severity</h3>
          </div>
          {current ? (
            <ul className="platform-overview-severities">
              {severityCounts.map(({ severity, count }) => (
                <li key={severity}>
                  <span>{severity}</span>
                  <div className="platform-overview-bar-track" aria-hidden="true">
                    <span style={{ width: `${(count / maximumSeverity) * 100}%` }} />
                  </div>
                  <strong>
                    {count}
                    <span className="platform-overview-screen-reader"> findings</span>
                  </strong>
                </li>
              ))}
            </ul>
          ) : (
            <p className="platform-overview-empty">Finding severity counts are unavailable.</p>
          )}
          <button
            className="platform-overview-link"
            type="button"
            onClick={() => onNavigate('Findings')}
          >
            Explore priority risks <span aria-hidden="true">→</span>
          </button>
        </section>

        <section className="platform-overview-panel" aria-labelledby="overview-review-heading">
          <div className="platform-overview-panel-heading">
            <h3 id="overview-review-heading">Review focus</h3>
          </div>
          {current ? (
            <dl className="platform-overview-metrics">
              <div>
                <dt>Proposed findings</dt>
                <dd>{proposed.length}</dd>
              </div>
              <div>
                <dt>Critical / high proposed</dt>
                <dd>{priorityProposed.length}</dd>
              </div>
              <div>
                <dt>Occurrences awaiting mandatory review</dt>
                <dd>{quality?.proposedReviewUnits ?? 'Unavailable'}</dd>
              </div>
            </dl>
          ) : (
            <p className="platform-overview-empty">Verified review results are unavailable.</p>
          )}
          <p className="platform-overview-footnote">
            {review
              ? 'Uses the current verified review snapshot.'
              : current
                ? 'Uses finding states; a current review snapshot is unavailable.'
                : 'Review does not change historical evidence.'}
          </p>
          <button
            className="platform-overview-link"
            type="button"
            onClick={() => onNavigate('Tasks & reviews')}
          >
            Open tasks & reviews <span aria-hidden="true">→</span>
          </button>
        </section>

        <section
          className="platform-overview-panel platform-overview-quality"
          aria-labelledby="overview-quality-heading"
        >
          <div className="platform-overview-panel-heading">
            <h3 id="overview-quality-heading">Separate assessment quality</h3>
            <span className="platform-overview-caption">Not part of health</span>
          </div>
          <dl className="platform-overview-metrics">
            <div>
              <dt>Executable coverage</dt>
              <dd>
                {coverage?.hasApplicableUnits && coverage.denominator > 0
                  ? `${coverage.numerator} / ${coverage.denominator}`
                  : 'Unavailable'}
              </dd>
            </div>
            <div>
              <dt>Planned units</dt>
              <dd>{quality?.plannedUnits ?? run?.progress.plannedUnits ?? 'Unavailable'}</dd>
            </div>
            <div>
              <dt>Executed applicable units</dt>
              <dd>{quality?.executedUnits ?? 'Unavailable'}</dd>
            </div>
            <div>
              <dt>Explained gap units</dt>
              <dd>{quality?.gapUnits ?? 'Unavailable'}</dd>
            </div>
            <div>
              <dt>Not applicable units</dt>
              <dd>{quality?.notApplicableUnits ?? 'Unavailable'}</dd>
            </div>
            <div>
              <dt>Finding occurrences</dt>
              <dd>{quality?.totalFindingUnits ?? 'Unavailable'}</dd>
            </div>
          </dl>
          <button
            className="platform-overview-link"
            type="button"
            onClick={() => onNavigate('Evidence')}
          >
            Explore evidence & coverage <span aria-hidden="true">→</span>
          </button>
        </section>
      </div>
    </section>
  );
}
