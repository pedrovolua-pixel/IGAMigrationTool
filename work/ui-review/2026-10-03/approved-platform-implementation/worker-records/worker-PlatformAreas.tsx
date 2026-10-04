import { useEffect, useId, useState } from 'react';
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
    <div className="platform-areas-table-wrap">
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
function Settings({ run, analysis, onNavigate }: Pick<Props, 'run' | 'analysis' | 'onNavigate'>) {
  const [tab, setTab] = useState<SettingsTab>('General');
  const [appearance, setAppearance] = useState<Appearance>(readAppearance);
  const [storageStatus, setStorageStatus] = useState(
    'Appearance preferences apply only to this browser.',
  );
  const tabId = useId();
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
  return (
    <>
      <div className="platform-areas-tabs" role="tablist" aria-label="Settings sections">
        {settingsTabs.map((item, index) => (
          <button
            key={item}
            id={`${tabId}-${item}`}
            type="button"
            role="tab"
            aria-selected={tab === item}
            aria-controls={`${tabId}-panel`}
            tabIndex={tab === item ? 0 : -1}
            onClick={() => setTab(item)}
            onKeyDown={(event) => {
              if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
              event.preventDefault();
              const nextIndex =
                event.key === 'Home'
                  ? 0
                  : event.key === 'End'
                    ? settingsTabs.length - 1
                    : (index + (event.key === 'ArrowRight' ? 1 : -1) + settingsTabs.length) %
                      settingsTabs.length;
              const next = settingsTabs[nextIndex];
              if (next) {
                setTab(next);
                document.getElementById(`${tabId}-${next}`)?.focus();
              }
            }}
          >
            {item}
          </button>
        ))}
      </div>
      <section
        id={`${tabId}-panel`}
        role="tabpanel"
        aria-labelledby={`${tabId}-${tab}`}
        className="platform-areas-section"
        tabIndex={0}
      >
        {tab === 'General' && (
          <>
            <h3>Project context</h3>
            <Summary
              entries={[
                ['Scope', run?.selection.scopeLabel],
                ['Baseline', run?.selection.baselineLabel],
                ['Profile', run?.selection.profileLabel],
                ['Run', run?.runId],
              ]}
            />
            <UnavailableControl
              label="Project administration"
              reason="Project names, ownership and environment configuration are not editable in this local pilot."
            />
          </>
        )}
        {tab === 'Assessment' && (
          <>
            <h3>Assessment profile</h3>
            <Summary
              entries={[
                ['Profile', run?.selection.profileLabel],
                ['Profile version', frozen?.profileVersion],
                ['Scoring algorithm', analysis?.algorithmVersion],
              ]}
            />
            <p className="platform-areas-muted">
              Inputs are frozen for the selected run. Use the existing run configuration form to
              select inputs for a new assessment.
            </p>
            <Locks run={run} />
            <UnavailableControl
              label="Edit category weights or rule overrides"
              reason="This host does not supply an authorized profile or rule editing action."
            />
          </>
        )}
        {tab === 'AI' && (
          <>
            <h3>AI analysis controls</h3>
            <Summary
              entries={[
                ['Offline preview', ai?.status],
                [
                  'Preview limitation',
                  ai?.reasonCode ?? (ai?.status === 'Ready' ? 'Proposed synthetic output' : null),
                ],
                ['AI policy version', frozen?.aiPolicyVersion],
                ['Prompt version', ai?.snapshot?.source.promptVersion ?? frozen?.promptVersion],
                ['Model version', frozen?.modelVersion],
              ]}
            />
            <UnavailableControl
              label="Enable automatic AI or change budgets"
              reason="Run, period and user budgets are not supplied as editable settings by this host."
            />
            <UnavailableControl
              label="On-demand deep analysis"
              reason="Phase 2 capability. No provider connection or protected raw-evidence retrieval is available here."
            />
            <button
              type="button"
              className="platform-areas-link"
              onClick={() => onNavigate('AI workspace')}
            >
              Open AI workspace →
            </button>
          </>
        )}
        {tab === 'Access' && (
          <>
            <h3>Access and roles</h3>
            <Summary
              entries={[
                ['Current review actor', analysis?.review?.actor],
                ['Review availability', analysis?.review?.status],
              ]}
            />
            <p className="platform-areas-muted">
              The current actor is supplied by the local host. This view does not establish customer
              roles or grant access.
            </p>
            <UnavailableControl
              label="Assign users or roles"
              reason="No project membership or role-assignment adapter is supplied."
            />
            <UnavailableControl
              label="Authorize protected evidence"
              reason="Customer source-access authorization is handled separately; appearance settings cannot grant it."
            />
          </>
        )}
        {tab === 'Data policy' && (
          <>
            <h3>Customer data policy</h3>
            <Empty>
              No customer residency, retention duration or deletion policy is supplied by this local
              host.
            </Empty>
            <UnavailableControl
              label="Change residency, retention or deletion"
              reason="A customer policy and authorized policy controller are required. No sample defaults are applied."
            />
            <UnavailableControl
              label="Change redaction or export policy"
              reason="Immutable evidence and existing protected-reference routes remain governed by their current host controls."
            />
          </>
        )}
        {tab === 'Notifications' && (
          <>
            <h3>Notifications</h3>
            <Empty>
              Notification preferences and delivery status are unavailable in this local pilot.
            </Empty>
            <UnavailableControl
              label="Assessment and review notifications"
              reason="No notification preference or delivery adapter is connected."
            />
            <UnavailableControl
              label="External channels"
              reason="External notification channels are deferred beyond the pilot."
            />
          </>
        )}
        {tab === 'Integrations' && (
          <>
            <h3>Integration availability</h3>
            <UnavailableControl
              label="Read-only MCP identities"
              reason="Phase 1D defines read-only access. This screen has no connected identity or authorization controller."
            />
            <UnavailableControl
              label="Jira, GitHub and ServiceNow"
              reason="External task creation is deferred. Existing in-product planning tasks use their own verified local controller."
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
            <div className="platform-areas-preferences">
              <label>
                Theme
                <select
                  value={appearance.theme}
                  onChange={(event) =>
                    changeAppearance({
                      ...appearance,
                      theme: event.target.value as Appearance['theme'],
                    })
                  }
                >
                  <option value="light">Light</option>
                  <option value="dark">Dark</option>
                  <option value="system">System</option>
                </select>
              </label>
              <label>
                Information density
                <select
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
              </label>
              <label className="platform-areas-checkbox">
                <input
                  type="checkbox"
                  checked={appearance.reducedMotion}
                  onChange={(event) =>
                    changeAppearance({ ...appearance, reducedMotion: event.target.checked })
                  }
                />
                Reduce motion
              </label>
            </div>
            <p role="status" className="platform-areas-muted">
              {storageStatus}
            </p>
            <button type="button" onClick={() => changeAppearance(defaultAppearance)}>
              Reset appearance
            </button>
          </>
        )}
      </section>
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
    description = 'Available baseline versions and the selected run’s frozen evidence inputs.';
    content = (
      <>
        <section className="platform-areas-section">
          <h3>Evidence baselines</h3>
          {!catalog?.baselines.length ? (
            <Empty>No evidence baselines are available.</Empty>
          ) : (
            <div className="platform-areas-table-wrap">
              <table>
                <thead>
                  <tr>
                    <th scope="col">Baseline</th>
                    <th scope="col">Scope</th>
                    <th scope="col">Version</th>
                    <th scope="col">Warnings</th>
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
          )}
          <UnavailableControl
            label="Connect or collect a customer source"
            reason="This local host supplies synthetic baselines. No source connection or credential entry is available."
          />
        </section>
        <section className="platform-areas-section">
          <h3>Selected run input locks</h3>
          <Locks run={run} />
        </section>
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
            <div className="platform-areas-table-wrap">
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
    description =
      'Rule versions referenced by the selected analysis, with immutable catalog inputs.';
    const visible = rules.filter((rule) =>
      `${rule.id} ${rule.version}`.toLowerCase().includes(match),
    );
    content = (
      <>
        <section className="platform-areas-section">
          <div className="platform-areas-toolbar">
            <h3>Referenced rules</h3>
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
            <Empty>
              {rules.length
                ? 'No referenced rules match your search.'
                : 'No finding rule references were supplied.'}
            </Empty>
          ) : (
            <div className="platform-areas-table-wrap">
              <table>
                <thead>
                  <tr>
                    <th scope="col">Rule ID</th>
                    <th scope="col">Version</th>
                    <th scope="col">Referenced findings</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((rule) => (
                    <tr key={`${rule.id}@${rule.version}`}>
                      <th scope="row">{rule.id}</th>
                      <td>{rule.version}</td>
                      <td>
                        {
                          admitted.findings.filter(
                            (finding) =>
                              finding.ruleId === rule.id && finding.ruleVersion === rule.version,
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
            Finding references are not the full rule catalog. Rule logic, applicability and quality
            evidence are not supplied by this endpoint.
          </p>
          <UnavailableControl
            label="Author, disable or override a rule"
            reason="No rule administration action is connected. Custom-rule authoring is a Phase 3 capability."
          />
        </section>
        <section className="platform-areas-section">
          <h3>Frozen input versions</h3>
          <Locks run={run} />
        </section>
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
          <ol className="platform-areas-timeline">
            {visible.map(({ event, findingId, findingTitle }) => (
              <li key={`${findingId}-${event.eventId}`}>
                <div className="platform-areas-event-head">
                  <strong>
                    {event.kind} · {findingTitle}
                  </strong>
                  <time dateTime={event.recordedAtUtc}>{displayTime(event.recordedAtUtc)}</time>
                </div>
                <p>
                  {event.actorId} · Finding {findingId} · Revision {event.revision} · {event.state}
                </p>
                {event.reason && <p>{event.reason}</p>}
                {event.text && <blockquote>{event.text}</blockquote>}
                {event.title && <p>Presentation title: {event.title}</p>}
                {event.businessContext && <p>Business context: {event.businessContext}</p>}
                <small>Event {event.eventId}</small>
              </li>
            ))}
          </ol>
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
    description = 'Phase 4 planning boundary · execution is unavailable in the pilot.';
    content = (
      <>
        <section className="platform-areas-section">
          <span className="platform-areas-phase">Future phase · Phase 4</span>
          <h3>Migration workspace</h3>
          <p>
            Production migration execution requires a separately specified execution workflow,
            security controls, validation and operation-specific rollback or compensating recovery.
          </p>
          <ol className="platform-areas-steps">
            <li>
              <strong>Understand the source</strong>
              <span>Use current assessment findings and evidence to identify constraints.</span>
            </li>
            <li>
              <strong>Specify the migration</strong>
              <span>
                Define the target model, object mapping and transformation contracts before
                execution.
              </span>
            </li>
            <li>
              <strong>Validate and recover</strong>
              <span>
                Require tested recovery and operation-level validation for each authorized change.
              </span>
            </li>
          </ol>
          <UnavailableControl
            label="Run or configure a production migration"
            reason="No migration engine or execution controls are connected to this pilot."
          />
          <button
            type="button"
            className="platform-areas-link"
            onClick={() => onNavigate('Assessments')}
          >
            Return to current assessment →
          </button>
        </section>
      </>
    );
  } else if (is('Portfolio')) {
    description = 'Post-pilot Phase 2 · read-only vendor health summaries.';
    content = (
      <section className="platform-areas-section">
        <span className="platform-areas-phase">Future phase · Phase 2</span>
        <h3>Customer health portfolio</h3>
        <Empty>
          No authorized customer portfolio feed is connected. Customer entries and health scores are
          unavailable.
        </Empty>
        <div className="platform-areas-two-column">
          <div>
            <h4>Summary scope</h4>
            <p>
              The planned dashboard shows the latest completed assessment’s health, completion time,
              assessed modules, and finding titles with severity and review status.
            </p>
          </div>
          <div>
            <h4>Customer control</h4>
            <p>
              Customer opt-in and scoped access are required. The dashboard excludes evidence,
              object identifiers, detailed configurations and links into customer assessments.
            </p>
          </div>
        </div>
        <UnavailableControl
          label="View customer summaries or manage opt-in"
          reason="Portfolio authorization, opt-in and retention controllers are not part of this local host."
        />
      </section>
    );
  } else if (is('Gallery', 'Design archive')) {
    description = 'Preserved approved design and original screenshots for comparison.';
    content = (
      <section className="platform-areas-section">
        <h3>Approved graphical platform design</h3>
        <p>
          Browse the complete sample-data mockup and the recovered screenshot gallery alongside the
          implemented local workspace.
        </p>
        <div className="platform-areas-records">
          <div className="platform-areas-record">
            <div>
              <strong>Complete platform mockup</strong>
              <p>Approved design reference with scripted sample interactions.</p>
            </div>
            <a href="/design-review/" target="_blank" rel="noopener noreferrer">
              Open mockup ↗
            </a>
          </div>
          <div className="platform-areas-record">
            <div>
              <strong>Original design screenshots</strong>
              <p>Chart-led overview, evidence views and unblurred relationship overlays.</p>
            </div>
            <a href="/design-review/#gallery" target="_blank" rel="noopener noreferrer">
              Browse archive ↗
            </a>
          </div>
        </div>
        <p className="platform-areas-muted">
          The design reference contains fictional sample records. Those records are not assessment
          evidence or implemented permissions.
        </p>
      </section>
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
