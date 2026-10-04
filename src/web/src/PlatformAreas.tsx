import { useEffect, useId, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import type { AnalysisDetail, Catalog, RunDetail, RunHistory } from './demo-contract.generated';
import './PlatformAreas.css';

type Props = {
  view: string;
  run: RunDetail | null;
  catalog: Catalog | null;
  history: RunHistory | null;
  analysis: AnalysisDetail | null;
  onNavigate: (view: string) => void;
  onSelectRun: (id: string) => void;
};
type Appearance = {
  theme: 'light' | 'dark' | 'system';
  density: 'comfortable' | 'compact';
  reducedMotion: boolean;
};
const appearanceKey = 'iga.workspace.appearance';
const defaultAppearance: Appearance = {
  theme: 'light',
  density: 'comfortable',
  reducedMotion: false,
};
const settingsTabs = [
  'General',
  'Assessment',
  'AI',
  'Access',
  'Data policy',
  'Notifications',
  'Integrations',
  'Appearance',
] as const;
type SettingsTab = (typeof settingsTabs)[number];

function readAppearance(): Appearance {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(appearanceKey) ?? 'null');
    if (value && typeof value === 'object') {
      const candidate = value as Partial<Appearance>;
      return {
        theme:
          candidate.theme === 'dark' || candidate.theme === 'system' ? candidate.theme : 'light',
        density: candidate.density === 'compact' ? 'compact' : 'comfortable',
        reducedMotion: candidate.reducedMotion === true,
      };
    }
  } catch {
    // Unavailable browser storage does not prevent using local appearance controls.
  }
  return defaultAppearance;
}
function displayTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
function Empty({ children }: { children: ReactNode }) {
  return <p className="platform-areas-empty">{children}</p>;
}
function Summary({ entries }: { entries: ReadonlyArray<readonly [string, ReactNode]> }) {
  return (
    <dl className="platform-areas-summary">
      {entries.map(([name, value]) => (
        <div key={name}>
          <dt>{name}</dt>
          <dd>{value ?? 'Unavailable'}</dd>
        </div>
      ))}
    </dl>
  );
}
function UnavailableControl({ label, reason }: { label: string; reason: string }) {
  return (
    <div className="platform-areas-control">
      <div>
        <strong>{label}</strong>
        <p>{reason}</p>
      </div>
      <button type="button" disabled>
        Unavailable
      </button>
    </div>
  );
}
function Locks({ run }: { run: RunDetail | null }) {
  if (!run) return <Empty>Select a run to inspect its immutable inputs.</Empty>;
  if (!run.lockedInputs.length) return <Empty>No input versions were supplied for this run.</Empty>;
  return (
    <div
      className="platform-areas-table-wrap"
      tabIndex={0}
      role="region"
      aria-label="Scrollable records table"
    >
      <table>
        <caption className="platform-areas-sr-only">Selected run input locks</caption>
        <thead>
          <tr>
            <th scope="col">Input</th>
            <th scope="col">Version</th>
            <th scope="col">SHA-256</th>
          </tr>
        </thead>
        <tbody>
          {run.lockedInputs.map((lock) => (
            <tr key={`${lock.name}-${lock.sha256}`}>
              <th scope="row">{lock.name}</th>
              <td>{lock.version}</td>
              <td>
                <code>{lock.sha256}</code>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
function Tabs({
  label,
  items,
  active,
  onChange,
  children,
}: {
  label: string;
  items: readonly string[];
  active: string;
  onChange: (value: string) => void;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <>
      <div className="platform-areas-tabs" role="tablist" aria-label={label}>
        {items.map((item, index) => (
          <button
            key={item}
            id={`${id}-${index}`}
            type="button"
            role="tab"
            aria-selected={item === active}
            aria-controls={`${id}-panel`}
            tabIndex={item === active ? 0 : -1}
            onClick={() => onChange(item)}
            onKeyDown={(event) => {
              if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
              event.preventDefault();
              const next =
                event.key === 'Home'
                  ? 0
                  : event.key === 'End'
                    ? items.length - 1
                    : (index + (event.key === 'ArrowRight' ? 1 : -1) + items.length) % items.length;
              onChange(items[next]!);
              document.getElementById(`${id}-${next}`)?.focus();
            }}
          >
            {item}
          </button>
        ))}
      </div>
      <div
        id={`${id}-panel`}
        role="tabpanel"
        aria-labelledby={`${id}-${items.indexOf(active)}`}
        tabIndex={0}
      >
        {children}
      </div>
    </>
  );
}
function Field({
  label,
  value,
  kind = 'text',
  help,
}: {
  label: string;
  value?: string | null;
  kind?: 'text' | 'select' | 'number' | 'checkbox';
  help?: string;
}) {
  const id = useId();
  const displayed = value ?? 'Unavailable';
  const checkbox = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (checkbox.current) checkbox.current.indeterminate = true;
  }, [kind]);
  return (
    <div className="platform-areas-form-field">
      <label htmlFor={id}>
        {label}
        {help && <small>{help}</small>}
      </label>
      {kind === 'checkbox' ? (
        <div className="platform-areas-unknown-toggle">
          <input
            id={id}
            ref={checkbox}
            type="checkbox"
            aria-checked="mixed"
            aria-describedby={`${id}-status`}
            disabled
          />
          <span id={`${id}-status`}>{displayed}</span>
        </div>
      ) : kind === 'select' ? (
        <select id={id} disabled value={displayed}>
          <option>{displayed}</option>
        </select>
      ) : (
        <input
          id={id}
          type="text"
          disabled
          value={displayed}
          inputMode={kind === 'number' ? 'numeric' : undefined}
        />
      )}
    </div>
  );
}
function Settings({ run, analysis, onNavigate }: Pick<Props, 'run' | 'analysis' | 'onNavigate'>) {
  const [tab, setTab] = useState<SettingsTab>('General');
  const [appearance, setAppearance] = useState<Appearance>(readAppearance);
  const [storageStatus, setStorageStatus] = useState(
    'Appearance preferences apply only to this browser.',
  );
  useEffect(() => {
    document.documentElement.dataset.workspaceTheme = appearance.theme;
    document.documentElement.dataset.workspaceDensity = appearance.density;
    document.documentElement.dataset.workspaceReducedMotion = String(appearance.reducedMotion);
  }, [appearance]);
  function changeAppearance(next: Appearance) {
    setAppearance(next);
    try {
      localStorage.setItem(appearanceKey, JSON.stringify(next));
      setStorageStatus('Appearance preferences saved in this browser.');
    } catch {
      setStorageStatus('Appearance applied for this session. Browser storage is unavailable.');
    }
  }
  const frozen = analysis?.reportDraft?.snapshot?.source.frozenVersions;
  const ai = analysis?.aiPreview;
  const preferencesId = useId();
  return (
    <>
      <Tabs
        label="Settings sections"
        items={settingsTabs}
        active={tab}
        onChange={(value) => setTab(value as SettingsTab)}
      >
        <section
          className="platform-areas-section"
          data-settings-tab={tab}
          aria-label={`${tab} settings`}
        >
          {tab === 'General' && (
            <>
              <h3>Project settings</h3>
              <Field label="Project name" value={run?.selection.scopeLabel} />
              <Field label="Customer" />
              <Field label="Environment" kind="select" />
              <Field label="Source product" kind="select" />
              <Field label="Engagement owner" />
              <p className="platform-areas-muted">
                Project names, ownership and environment administration are not editable in this
                local pilot.
              </p>
            </>
          )}
          {tab === 'Assessment' && (
            <>
              <h3>Assessment profile</h3>
              <Field label="Profile" value={run?.selection.profileLabel} kind="select" />
              <h4 className="platform-areas-subheading">Category weights</h4>
              {analysis?.categories.length ? (
                analysis.categories.map((category) => (
                  <Field
                    key={category.id}
                    label={`${category.id} weight`}
                    value={category.provisionalWeight}
                    kind="number"
                  />
                ))
              ) : (
                <Field label="Category weights" />
              )}
              <p className="platform-areas-muted">
                Values are frozen for this run. Unknown weights are not assigned sample defaults.
              </p>
              <Field label="Operational lookback (days)" kind="number" />
              <Field
                label="Rule catalog"
                value={run?.lockedInputs.find((lock) => lock.name === 'Rule catalog')?.version}
                kind="select"
              />
              <Field
                label="Include desired outcomes"
                value={
                  analysis?.outcomes.length
                    ? 'Saved outcome rows supplied · enabled state unavailable'
                    : null
                }
                kind="checkbox"
              />
              <details>
                <summary>Selected run input locks</summary>
                <Locks run={run} />
              </details>
              <p className="platform-areas-muted">
                Use the existing run configuration below to select inputs for a new assessment. Rule
                and weight editing is unavailable.
              </p>
            </>
          )}
          {tab === 'AI' && (
            <>
              <h3>AI analysis controls</h3>
              <Field
                label="Automatic general AI"
                kind="checkbox"
                help="Eligible redacted evidence only"
              />
              <Field label="Category scope" kind="select" />
              <Field label="Run budget" kind="number" />
              <Field label="Period budget" kind="number" />
              <Field label="Per-user budget" kind="number" />
              <Empty>
                AI policy and budgets are not supplied as editable settings by this host. No sample
                limits or enabled state are applied.
              </Empty>
              <Summary
                entries={[
                  ['Offline preview', ai?.status],
                  ['AI policy version', frozen?.aiPolicyVersion],
                  ['Prompt version', ai?.snapshot?.source.promptVersion ?? frozen?.promptVersion],
                ]}
              />
              <UnavailableControl
                label="Deep analysis"
                reason="Phase 2 · separate scope and protected raw-evidence authorization are required."
              />
              <button
                type="button"
                className="platform-areas-link"
                onClick={() => onNavigate('AI workspace')}
              >
                Review AI workspace →
              </button>
            </>
          )}
          {tab === 'Access' && (
            <>
              <h3>Project roles</h3>
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Current actor</th>
                      <th>Role</th>
                      <th>Authority</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>{analysis?.review?.actor ?? 'Unavailable'}</td>
                      <td>Unavailable</td>
                      <td>Supplied local review actor; customer authority unavailable</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <UnavailableControl
                label="Assign users or roles"
                reason="No membership adapter is supplied. This view grants no customer access."
              />
              <Empty>
                Roles must be scoped by customer, project, environment, action and evidence
                category. Protected evidence authorization is handled separately.
              </Empty>
            </>
          )}
          {tab === 'Data policy' && (
            <>
              <h3>Customer data policy</h3>
              <Field label="Residency" kind="select" />
              <Field label="Retention duration (days)" kind="number" />
              <Field label="Retention start event" kind="select" />
              <Field label="Raw-evidence processing" kind="select" />
              <Field label="Export review" kind="select" />
              <Empty>
                No customer policy is supplied. Credentials are prohibited evidence; appearance
                preferences cannot change evidence controls.
              </Empty>
              <button type="button" disabled>
                Review deletion impact · unavailable
              </button>
            </>
          )}
          {tab === 'Notifications' && (
            <>
              <h3>In-product notifications</h3>
              {[
                'Assessment completion',
                'Collection or assessment failure',
                'Required reviews',
                'New or worsened Critical / High findings',
                'New rule catalog available',
              ].map((label) => (
                <Field key={label} label={label} kind="checkbox" />
              ))}
              <p className="platform-areas-muted">
                No preference or delivery adapter is connected. External notification channels are
                deferred beyond the pilot.
              </p>
            </>
          )}
          {tab === 'Integrations' && (
            <>
              <h3>Read-only MCP</h3>
              <Field label="Authorized health-results access" kind="checkbox" />
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Planned read-only scope</th>
                      <th>Excluded actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>Status, coverage, scores</td>
                      <td>Starting analysis or changing dispositions</td>
                    </tr>
                    <tr>
                      <td>Findings and recommendations</td>
                      <td>Risk acceptance and publication</td>
                    </tr>
                    <tr>
                      <td>Protected evidence references</td>
                      <td>Raw-evidence access and task creation</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <UnavailableControl
                label="Identity scope"
                reason="No connected read-only identity or authorization controller is supplied."
              />
              <h3 className="platform-areas-subheading">Task handoff</h3>
              <UnavailableControl
                label="Jira · GitHub · ServiceNow"
                reason="External task creation is deferred beyond the pilot."
              />
              <button
                type="button"
                className="platform-areas-link"
                onClick={() => onNavigate('Tasks & reviews')}
              >
                Open tasks & reviews →
              </button>
            </>
          )}
          {tab === 'Appearance' && (
            <>
              <h3>Workspace preferences</h3>
              <div className="platform-areas-form-field">
                <label htmlFor={`${preferencesId}-theme`}>Theme</label>
                <select
                  id={`${preferencesId}-theme`}
                  value={appearance.theme}
                  onChange={(event) =>
                    changeAppearance({
                      ...appearance,
                      theme: event.target.value as Appearance['theme'],
                    })
                  }
                >
                  <option value="system">System</option>
                  <option value="light">Light</option>
                  <option value="dark">Dark</option>
                </select>
              </div>
              <Field label="Default area" kind="select" />
              <div className="platform-areas-form-field">
                <label htmlFor={`${preferencesId}-density`}>Information density</label>
                <select
                  id={`${preferencesId}-density`}
                  value={appearance.density}
                  onChange={(event) =>
                    changeAppearance({
                      ...appearance,
                      density: event.target.value as Appearance['density'],
                    })
                  }
                >
                  <option value="comfortable">Comfortable</option>
                  <option value="compact">Compact</option>
                </select>
              </div>
              <div className="platform-areas-form-field">
                <label htmlFor={`${preferencesId}-motion`}>Reduced motion</label>
                <input
                  id={`${preferencesId}-motion`}
                  type="checkbox"
                  checked={appearance.reducedMotion}
                  onChange={(event) =>
                    changeAppearance({ ...appearance, reducedMotion: event.target.checked })
                  }
                />
              </div>
              <Field label="Saved view" kind="select" />
              <Empty>
                Theme, density and reduced motion are display preferences saved only in this
                browser.
              </Empty>
            </>
          )}
          <div className="platform-areas-savebar">
            <span role="status">
              {tab === 'Appearance'
                ? storageStatus
                : 'Server settings unavailable · saved assessment inputs remain unchanged'}
            </span>
            {tab === 'Appearance' ? (
              <button type="button" onClick={() => changeAppearance(defaultAppearance)}>
                Reset appearance
              </button>
            ) : (
              <button type="button" disabled>
                Save settings · unavailable
              </button>
            )}
          </div>
        </section>
      </Tabs>
    </>
  );
}

export function PlatformAreas({
  view,
  run,
  catalog,
  history,
  analysis,
  onNavigate,
  onSelectRun,
}: Props) {
  useEffect(() => {
    const saved = readAppearance();
    document.documentElement.dataset.workspaceTheme = saved.theme;
    document.documentElement.dataset.workspaceDensity = saved.density;
    document.documentElement.dataset.workspaceReducedMotion = String(saved.reducedMotion);
  }, []);
  const [scope, setScope] = useState('');
  const [priorId, setPriorId] = useState('');
  const [query, setQuery] = useState('');
  const [areaTabs, setAreaTabs] = useState<Record<string, string>>({});
  const activeTab = (name: string, fallback: string) => areaTabs[name] ?? fallback;
  const selectTab = (name: string, value: string) =>
    setAreaTabs((previous) => ({ ...previous, [name]: value }));
  const selectedScope = catalog?.scopes.find((item) => item.id === scope);
  const activeScope = selectedScope?.id ?? run?.selection.scopeId ?? catalog?.scopes[0]?.id;
  const relevantHistory =
    history?.runs.filter((item) => !activeScope || item.selection.scopeId === activeScope) ?? [];
  const comparable =
    history?.runs.filter(
      (item) => item.runId !== run?.runId && item.selection.scopeId === run?.selection.scopeId,
    ) ?? [];
  const prior = comparable.find((item) => item.runId === priorId) ?? comparable[0];
  const normalized = view.toLowerCase();
  // Parent admission protects coherence; guard against accidental cross-run presentation as well.
  const admitted =
    analysis?.status === 'Ready' &&
    analysis.runId === run?.runId &&
    analysis.runRevision === run?.revision
      ? analysis
      : null;
  const rules = [
    ...new Map(
      (admitted?.findings ?? []).map((finding) => [
        `${finding.ruleId}@${finding.ruleVersion}`,
        { id: finding.ruleId, version: finding.ruleVersion },
      ]),
    ).values(),
  ];
  const events = (admitted?.review?.status === 'Ready' ? admitted.review.findings : [])
    .flatMap((finding) =>
      finding.history.map((event) => ({
        findingId: finding.id,
        findingTitle: finding.title,
        event,
      })),
    )
    .sort((left, right) => right.event.recordedAtUtc.localeCompare(left.event.recordedAtUtc));
  const match = query.trim().toLowerCase();
  const is = (...names: string[]) => names.some((name) => normalized === name.toLowerCase());
  let content: ReactNode;
  let description = '';
  if (is('Projects')) {
    description = 'Available assessment scopes and their saved local runs.';
    content = (
      <>
        <div className="platform-areas-section">
          <h3>Assessment scopes</h3>
          {!catalog?.scopes.length ? (
            <Empty>No assessment scopes are available. Load the catalog to view projects.</Empty>
          ) : (
            <div className="platform-areas-projects">
              {catalog.scopes.map((item) => (
                <button
                  type="button"
                  key={item.id}
                  className="platform-areas-project"
                  aria-pressed={activeScope === item.id}
                  onClick={() => setScope(item.id)}
                >
                  <span className="platform-areas-project-mark" aria-hidden="true">
                    ▦
                  </span>
                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.id}</small>
                  </span>
                  <span aria-hidden="true">→</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <section className="platform-areas-section">
          <h3>Runs in selected scope</h3>
          {!relevantHistory.length ? (
            <Empty>No saved runs were supplied for this scope.</Empty>
          ) : (
            <div className="platform-areas-records">
              {relevantHistory.map((item) => (
                <div className="platform-areas-record" key={item.runId}>
                  <div>
                    <strong>{item.selection.baselineLabel}</strong>
                    <p>
                      {item.selection.profileLabel} · {displayTime(item.createdAtUtc)}
                    </p>
                    <small>
                      {item.state}
                      {item.coverageCompletionKind ? ` · ${item.coverageCompletionKind}` : ''}
                    </small>
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      onSelectRun(item.runId);
                      onNavigate('Assessments');
                    }}
                  >
                    Open run
                  </button>
                </div>
              ))}
            </div>
          )}
          <p className="platform-areas-muted">
            Catalog scopes do not provide customer ownership or source topology.
          </p>
        </section>
      </>
    );
  } else if (is('Sources', 'Sources & baselines')) {
    description = 'Trace supplied configuration to immutable evidence baselines.';
    const tab = activeTab('sources', 'Connection');
    const objects = [
      ...new Set((admitted?.findings ?? []).flatMap((finding) => finding.objectIds)),
    ];
    content = (
      <>
        <Tabs
          label="Source sections"
          items={['Connection', 'Object inventory', 'Baselines', 'Collection history']}
          active={tab}
          onChange={(value) => selectTab('sources', value)}
        >
          {tab === 'Connection' && (
            <div className="platform-areas-split">
              <section className="platform-areas-section">
                <h3>
                  One Identity Manager{' '}
                  <span className="platform-areas-pill">Synthetic read-only context</span>
                </h3>
                <div className="platform-areas-diagram">
                  <div>
                    <strong>Saved scope</strong>
                    {run?.selection.scopeLabel ?? 'Unavailable'}
                  </div>
                  <span aria-hidden="true">→</span>
                  <div>
                    <strong>Evidence baseline</strong>
                    {run?.selection.baselineLabel ?? 'Unavailable'}
                  </div>
                </div>
                <table>
                  <thead>
                    <tr>
                      <th>Connection property</th>
                      <th>Supplied configuration</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>Method</td>
                      <td>Synthetic local host · customer source unavailable</td>
                    </tr>
                    <tr>
                      <td>Permission check</td>
                      <td>Unavailable</td>
                    </tr>
                    <tr>
                      <td>Operational lookback</td>
                      <td>Unavailable</td>
                    </tr>
                    <tr>
                      <td>Current baseline</td>
                      <td>{run?.selection.baselineLabel ?? 'Unavailable'}</td>
                    </tr>
                    <tr>
                      <td>Sensitive content</td>
                      <td>No customer source connected</td>
                    </tr>
                  </tbody>
                </table>
                <div className="platform-areas-actions">
                  <button disabled type="button">
                    Review connection
                  </button>
                  <button disabled type="button">
                    View collection scope
                  </button>
                </div>
              </section>
              <section className="platform-areas-section">
                <h3>Collection coverage</h3>
                {run?.stateCounts.length ? (
                  run.stateCounts.map((count) => (
                    <div className="platform-areas-coverage" key={count.state}>
                      <strong>{count.state}</strong>
                      <span>{count.count}</span>
                      <div className="platform-areas-track">
                        <span
                          style={{
                            width: `${run.progress.plannedUnits ? Math.min(100, (count.count / run.progress.plannedUnits) * 100) : 0}%`,
                          }}
                        />
                      </div>
                    </div>
                  ))
                ) : (
                  <Empty>No saved coverage counts are supplied.</Empty>
                )}
                <Empty>
                  Coverage counts are saved synthetic run units, not customer object inventory. Gaps
                  remain separate from health scoring.
                </Empty>
                <button
                  type="button"
                  className="platform-areas-link"
                  onClick={() => onNavigate('Evidence')}
                >
                  Open evidence →
                </button>
              </section>
            </div>
          )}
          {tab === 'Object inventory' && (
            <section className="platform-areas-section">
              <h3>Referenced objects</h3>
              <Empty>
                The host does not provide a complete collected object inventory. These identifiers
                appear in admitted finding records only.
              </Empty>
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Object reference</th>
                      <th>Linked findings</th>
                      <th>Evidence state</th>
                    </tr>
                  </thead>
                  <tbody>
                    {objects.map((id) => (
                      <tr key={id}>
                        <th scope="row">{id}</th>
                        <td>{admitted?.findings.filter((f) => f.objectIds.includes(id)).length}</td>
                        <td>Referenced by saved findings</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {!objects.length && <Empty>No object references are supplied.</Empty>}
            </section>
          )}
          {tab === 'Baselines' && (
            <>
              <section className="platform-areas-section">
                <h3>Evidence baselines</h3>
                {catalog?.baselines.length ? (
                  <div className="platform-areas-table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Baseline</th>
                          <th>Scope</th>
                          <th>Version</th>
                          <th>Warnings</th>
                        </tr>
                      </thead>
                      <tbody>
                        {catalog.baselines.map((baseline) => (
                          <tr key={baseline.id}>
                            <th scope="row">
                              {baseline.label}
                              <small>{baseline.id}</small>
                            </th>
                            <td>
                              {catalog.scopes.find((item) => item.id === baseline.scopeId)?.label ??
                                baseline.scopeId}
                            </td>
                            <td>{baseline.version}</td>
                            <td>
                              {baseline.warnings.length
                                ? baseline.warnings.map((warning) => (
                                    <p key={`${warning.code}-${warning.message}`}>
                                      {warning.code}: {warning.message}
                                    </p>
                                  ))
                                : 'None reported'}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <Empty>No evidence baselines are available.</Empty>
                )}
              </section>
              <section className="platform-areas-section">
                <h3>Selected run input locks</h3>
                <Locks run={run} />
              </section>
            </>
          )}
          {tab === 'Collection history' && (
            <section className="platform-areas-section">
              <h3>Collection history</h3>
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Collection</th>
                      <th>Baseline</th>
                      <th>State</th>
                      <th>Completed</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td colSpan={4}>
                        Collection event history is unavailable. Assessment run history does not
                        establish collection history.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <button
                type="button"
                className="platform-areas-link"
                onClick={() => onNavigate('Assessments')}
              >
                View saved assessments →
              </button>
            </section>
          )}
        </Tabs>
      </>
    );
  } else if (is('Compare', 'Compare runs')) {
    description = 'Compare saved run metadata without substituting unprovided historical scores.';
    content = (
      <section className="platform-areas-section">
        <h3>Run comparison</h3>
        {!run ? (
          <Empty>Select a current run to compare its history.</Empty>
        ) : !prior ? (
          <Empty>No other run in this scope was supplied for comparison.</Empty>
        ) : (
          <>
            <label className="platform-areas-select">
              Compare with
              <select value={prior.runId} onChange={(event) => setPriorId(event.target.value)}>
                {comparable.map((item) => (
                  <option key={item.runId} value={item.runId}>
                    {displayTime(item.createdAtUtc)} · {item.selection.baselineLabel} · {item.runId}
                  </option>
                ))}
              </select>
            </label>
            <div
              className="platform-areas-table-wrap"
              tabIndex={0}
              role="region"
              aria-label="Scrollable records table"
            >
              <table>
                <thead>
                  <tr>
                    <th scope="col">Assessment record</th>
                    <th scope="col">Current</th>
                    <th scope="col">Comparison</th>
                  </tr>
                </thead>
                <tbody>
                  {(
                    [
                      ['Run', run.runId, prior.runId],
                      ['Baseline', run.selection.baselineLabel, prior.selection.baselineLabel],
                      ['Profile', run.selection.profileLabel, prior.selection.profileLabel],
                      ['State', run.state, prior.state],
                      [
                        'Coverage completion',
                        run.coverageCompletionKind ?? 'Not complete',
                        prior.coverageCompletionKind ?? 'Not complete',
                      ],
                      [
                        'Terminal units',
                        `${run.progress.terminalUnits} / ${run.progress.plannedUnits}`,
                        `${prior.progress.terminalUnits} / ${prior.progress.plannedUnits}`,
                      ],
                      ['Created', displayTime(run.createdAtUtc), displayTime(prior.createdAtUtc)],
                      [
                        'Publishable current health',
                        admitted?.publishableCurrent?.display ?? 'Unavailable',
                        'Unavailable',
                      ],
                    ] as const
                  ).map(([label, current, previous]) => (
                    <tr key={label}>
                      <th scope="row">{label}</th>
                      <td>{current}</td>
                      <td>{previous}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Empty>
              History supplies run metadata only. Historical scores, finding changes and validated
              improvement are unavailable; no health trend is inferred.
            </Empty>
            <button
              type="button"
              onClick={() => {
                onSelectRun(prior.runId);
                onNavigate('Assessments');
              }}
            >
              Open comparison run
            </button>
          </>
        )}
      </section>
    );
  } else if (is('Rules', 'Rule catalog')) {
    description = 'Trace supplied rules to immutable catalog inputs.';
    const tab = activeTab('rules', 'Catalog');
    const visible = rules.filter((rule) =>
      `${rule.id} ${rule.version}`.toLowerCase().includes(match),
    );
    content = (
      <>
        <Tabs
          label="Rule catalog sections"
          items={['Catalog', 'Version history', 'Custom rules']}
          active={tab}
          onChange={(value) => selectTab('rules', value)}
        >
          {tab === 'Catalog' && (
            <section className="platform-areas-section">
              <div className="platform-areas-toolbar">
                <h3>Referenced catalog rules</h3>
                <label>
                  Search rules
                  <input
                    type="search"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                    placeholder="Rule ID or version"
                  />
                </label>
              </div>
              {!admitted ? (
                <Empty>Verified analysis is unavailable for the selected run.</Empty>
              ) : !visible.length ? (
                <Empty>No referenced rules match your search.</Empty>
              ) : (
                <div className="platform-areas-table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Rule</th>
                        <th>Category</th>
                        <th>Version</th>
                        <th>Referenced findings</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visible.map((rule) => (
                        <tr key={`${rule.id}@${rule.version}`}>
                          <th scope="row">{rule.id}</th>
                          <td>
                            {[
                              ...new Set(
                                admitted.findings
                                  .filter(
                                    (f) => f.ruleId === rule.id && f.ruleVersion === rule.version,
                                  )
                                  .map((f) => f.category),
                              ),
                            ].join(' · ')}
                          </td>
                          <td>{rule.version}</td>
                          <td>
                            {
                              admitted.findings.filter(
                                (f) => f.ruleId === rule.id && f.ruleVersion === rule.version,
                              ).length
                            }
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <p className="platform-areas-muted">
                Finding references are not the full catalog. Rule logic, method and applicability
                are not supplied by this endpoint.
              </p>
            </section>
          )}
          {tab === 'Version history' && (
            <section className="platform-areas-section">
              <h3>Frozen input versions</h3>
              <Locks run={run} />
              <Empty>
                These are current-run input locks. Catalog release dates, changes and historical
                versions are unavailable. Historical assessments are not reassessed automatically.
              </Empty>
            </section>
          )}
          {tab === 'Custom rules' && (
            <>
              <div className="platform-areas-banner">
                <strong>Phase 3 · future capability</strong>
                <p>Custom-rule authoring is unavailable in this pilot.</p>
              </div>
              <section className="platform-areas-section">
                <h3>Draft customer rule</h3>
                <Field label="Rule name" />
                <Field label="Category" kind="select" />
                <Field label="Condition preview" />
                <div className="platform-areas-actions">
                  <button disabled type="button">
                    Preview rule validation
                  </button>
                </div>
                <Empty>
                  No authoring controller is connected. No rule is added to the catalog.
                </Empty>
              </section>
            </>
          )}
        </Tabs>
      </>
    );
  } else if (is('Audit', 'Audit history')) {
    description = 'Actual review events supplied for the selected run.';
    const visible = events.filter(({ event, findingId, findingTitle }) =>
      `${event.actorId} ${event.kind} ${findingId} ${findingTitle}`.toLowerCase().includes(match),
    );
    content = (
      <section className="platform-areas-section">
        <div className="platform-areas-toolbar">
          <h3>Review activity</h3>
          <label>
            Search review events
            <input
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Actor, finding or action"
            />
          </label>
        </div>
        {!visible.length ? (
          <Empty>
            {events.length
              ? 'No review events match your search.'
              : 'No review history was supplied for the selected run.'}
          </Empty>
        ) : (
          <div className="platform-areas-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Actor</th>
                  <th>Event</th>
                  <th>Reference</th>
                  <th>Details</th>
                </tr>
              </thead>
              <tbody>
                {visible.map(({ event, findingId, findingTitle }) => (
                  <tr key={`${findingId}-${event.eventId}`}>
                    <td>
                      <time dateTime={event.recordedAtUtc}>{displayTime(event.recordedAtUtc)}</time>
                    </td>
                    <td>{event.actorId}</td>
                    <td>
                      {event.kind}
                      <small>
                        {event.state} · Revision {event.revision}
                      </small>
                    </td>
                    <td>
                      {findingTitle}
                      <small>Finding {findingId}</small>
                    </td>
                    <td>
                      <details>
                        <summary>Details</summary>
                        {event.reason && <p>{event.reason}</p>}
                        {event.text && <blockquote>{event.text}</blockquote>}
                        {event.title && <p>Presentation title: {event.title}</p>}
                        {event.businessContext && <p>Business context: {event.businessContext}</p>}
                        <small>Event {event.eventId}</small>
                      </details>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="platform-areas-muted">
          This timeline contains finding review events only. It does not represent source-access,
          policy, publication or platform-wide audit history.
        </p>
      </section>
    );
  } else if (is('Settings')) {
    description = 'Inspect current configuration and adjust local appearance.';
    content = <Settings run={run} analysis={admitted} onNavigate={onNavigate} />;
  } else if (is('Migration')) {
    description = 'Review source-to-destination intent, dependencies and validation.';
    const tab = activeTab('migration', 'Readiness');
    content = (
      <>
        <div className="platform-areas-banner">
          <strong>Phase 4 · future capability</strong>
          <p>
            Migration execution is outside the health pilot. No migration engine or execution
            controls are connected.
          </p>
        </div>
        <Tabs
          label="Migration sections"
          items={['Readiness', 'Mappings', 'Scope decisions', 'Validation']}
          active={tab}
          onChange={(value) => selectTab('migration', value)}
        >
          {tab === 'Readiness' && (
            <div className="platform-areas-split">
              <section className="platform-areas-section">
                <h3>Source and destination</h3>
                <Field label="Source" value={run?.selection.scopeLabel} />
                <Field label="Destination" kind="select" />
                <Field label="Assessment baseline" value={run?.selection.baselineLabel} />
                <button type="button" disabled>
                  Review prerequisites
                </button>
              </section>
              <section className="platform-areas-section">
                <h3>Migration readiness</h3>
                <Empty>No migration readiness record is supplied.</Empty>
                <p className="platform-areas-muted">
                  Current assessment findings identify constraints; they do not establish migration
                  readiness.
                </p>
                <button
                  type="button"
                  className="platform-areas-link"
                  onClick={() => onNavigate('Findings')}
                >
                  Review source findings →
                </button>
              </section>
            </div>
          )}
          {tab === 'Mappings' && (
            <section className="platform-areas-section">
              <h3>Source-to-destination mappings</h3>
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Use-case group</th>
                      <th>Source</th>
                      <th>Destination</th>
                      <th>Validation</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td colSpan={4}>No authorized mapping contract is supplied.</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <button type="button" disabled>
                Preview matching
              </button>
            </section>
          )}
          {tab === 'Scope decisions' && (
            <section className="platform-areas-section">
              <h3>Migration scope decisions</h3>
              <div className="platform-areas-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Object group</th>
                      <th>Decision</th>
                      <th>Dependency</th>
                      <th>Owner</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td colSpan={4}>No scope decisions or owners are supplied.</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <Empty>Define the target model and transformation contracts before execution.</Empty>
            </section>
          )}
          {tab === 'Validation' && (
            <div className="platform-areas-split">
              <section className="platform-areas-section">
                <h3>Validation and recovery</h3>
                <ol className="platform-areas-steps">
                  <li>
                    <strong>Specify operation-level checks</strong>
                    <span>Validation results are unavailable.</span>
                  </li>
                  <li>
                    <strong>Require tested recovery</strong>
                    <span>
                      Rollback or compensating recovery is required before authorized execution.
                    </span>
                  </li>
                </ol>
                <button type="button" disabled>
                  Preview validation
                </button>
              </section>
              <section className="platform-areas-section">
                <h3>Execution handoff</h3>
                <Empty>
                  No execution or recovery controller is supplied. This workspace starts no
                  migration.
                </Empty>
                <button type="button" disabled>
                  Preview handoff
                </button>
              </section>
            </div>
          )}
        </Tabs>
      </>
    );
  } else if (is('Portfolio')) {
    description = 'Read-only summaries of customers who opted in.';
    content = (
      <>
        <div className="platform-areas-banner">
          <strong>Phase 2 · future capability</strong>
          <p>
            Portfolio authorization and customer opt-in are not connected. Customer evidence and
            assessment drilldowns are excluded.
          </p>
        </div>
        <div className="platform-areas-stats">
          {[
            'Opted-in customers',
            'Mean health',
            'Assessments in progress',
            'Critical findings',
          ].map((label) => (
            <div key={label}>
              <strong>—</strong>
              <span>{label}</span>
              <small>Unavailable</small>
            </div>
          ))}
        </div>
        <section className="platform-areas-section">
          <div className="platform-areas-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Customer</th>
                  <th>Latest completed assessment</th>
                  <th>Health</th>
                  <th>Modules</th>
                  <th>Findings</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td colSpan={5}>
                    No authorized customer portfolio feed is connected. Current local scope:{' '}
                    {run?.selection.scopeLabel ?? 'Unavailable'}. This is not a customer portfolio
                    entry.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <Empty>
            Summary-only boundary · no customer-detail links or cross-customer exports. Customer
            opt-in, scoped access and retention controls require separate authorization.
          </Empty>
        </section>
      </>
    );
  } else if (is('Gallery', 'Design archive')) {
    description = 'Preserved approved design and original screenshots for comparison.';
    const shots = [
      ['overview.png', 'Original graphical overview'],
      ['risk-analysis.png', 'Risk analysis'],
      ['relationships-overlay-desktop.png', 'Relationships overlay'],
      ['overlay-no-blur-desktop.png', 'Overlay without blur'],
      ['relationships-overlay-mobile.png', 'Mobile relationships'],
      ['drilldown-evidence.png', 'Evidence workspace'],
      ['evidence.png', 'Original evidence concept'],
      ['drilldown-desktop.png', 'Initial finding drilldown'],
      ['drilldown-mobile.png', 'Initial mobile drilldown'],
      ['overlay-no-blur-mobile.png', 'Mobile overlay without blur'],
    ];
    content = (
      <>
        <div className="platform-areas-actions">
          <a href="/design-review/" target="_blank" rel="noopener noreferrer">
            Open approved mockup ↗
          </a>
          <a href="http://127.0.0.1:5210/" target="_blank" rel="noopener noreferrer">
            Open recovered original ↗
          </a>
        </div>
        <div className="platform-areas-shots">
          {shots.map(([file, title]) => (
            <article key={file} className="platform-areas-shot">
              <a
                href={`/design-review/screenshots/${file}`}
                target="_blank"
                rel="noopener noreferrer"
              >
                <img src={`/design-review/screenshots/${file}`} alt={title} loading="lazy" />
                <h3>{title}</h3>
                <p className="platform-areas-muted">Open original screenshot ↗</p>
              </a>
            </article>
          ))}
        </div>
        <p className="platform-areas-muted">
          The approved design contains fictional sample records; these are not evidence or
          implemented permissions.
        </p>
      </>
    );
  } else {
    description = 'Select a workspace area to inspect its available records.';
    content = <Empty>This view is unavailable. Open Assessments to select a run.</Empty>;
  }
  return (
    <section className="platform-areas" aria-labelledby="platform-area-heading">
      <header className="platform-areas-header">
        <h2 id="platform-area-heading" tabIndex={-1}>
          {view}
        </h2>
        <p>{description}</p>
      </header>
      {content}
    </section>
  );
}
