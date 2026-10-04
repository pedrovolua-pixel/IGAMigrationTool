import { useEffect, useRef, useState } from 'react';
import './workspace.css';
export const workspaceGroups = [
  {
    name: 'Workspace',
    destinations: [
      ['Overview', 'Overview', '◈'],
      ['Assessments', 'Assessments', '▤'],
      ['Findings', 'Risk analysis', '◴'],
      ['Evidence', 'Evidence', '⌘'],
      ['Outcomes & maturity', 'Outcomes & maturity', '◎'],
      ['AI workspace', 'AI workspace', '✧'],
      ['Recommendations', 'Recommendations', '↗'],
      ['Tasks & reviews', 'Tasks & reviews', '☷'],
      ['Reports', 'Reports', '▧'],
      ['Compare runs', 'Compare runs', '⇄'],
    ],
  },
  {
    name: 'Manage',
    destinations: [
      ['Projects', 'Projects', '▦'],
      ['Sources & baselines', 'Sources & baselines', '◫'],
      ['Rule catalog', 'Rule catalog', '≡'],
      ['Settings', 'Settings', '⚙'],
      ['Audit history', 'Audit history', '◷'],
    ],
  },
  {
    name: 'Explore',
    destinations: [
      ['Migration', 'Migration', '⇢'],
      ['Portfolio', 'Portfolio', '▥'],
      ['Design archive', 'Design archive', '▣'],
    ],
  },
] as const;
export type WorkspaceView = (typeof workspaceGroups)[number]['destinations'][number][0];
export const isWorkspaceView = (value: string): value is WorkspaceView =>
  workspaceGroups.some((group) => group.destinations.some((item) => item[0] === value));
export function WorkspaceNavigation({
  active,
  onNavigate,
  onUnavailable,
}: {
  active: WorkspaceView;
  onNavigate: (view: WorkspaceView) => void;
  onUnavailable: (message: string) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const toggle = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    setExpanded(false);
  }, [active]);
  function navigate(view: WorkspaceView) {
    onUnavailable('');
    onNavigate(view);
    setExpanded(false);
    requestAnimationFrame(() => {
      document.getElementById('workspace-title')?.focus({ preventScroll: true });
      document.getElementById('workspace')?.scrollIntoView({ block: 'start' });
    });
  }
  return (
    <aside
      className="workspace-sidebar"
      data-menu-open={expanded}
      onKeyDown={(event) => {
        if (event.key === 'Escape' && expanded) {
          setExpanded(false);
          toggle.current?.focus();
        }
      }}
    >
      <div className="workspace-identity">
        <span className="workspace-monogram" aria-hidden="true">
          I
        </span>
        <div>
          <strong>IGA workspace</strong>
          <span>Assessment studio</span>
        </div>
        <button
          type="button"
          ref={toggle}
          className="workspace-menu-toggle"
          aria-expanded={expanded}
          aria-controls="platform-navigation"
          onClick={() => setExpanded((value) => !value)}
        >
          {expanded ? 'Close menu' : 'Menu'}
        </button>
      </div>
      <nav id="platform-navigation" aria-label="Workspace sections">
        {workspaceGroups.map((group) => (
          <div key={group.name}>
            <p className="sidebar-label">{group.name}</p>
            <div className="workspace-destinations">
              {group.destinations.map(([view, label, symbol]) => (
                <button
                  key={view}
                  type="button"
                  aria-current={active === view ? 'page' : undefined}
                  onClick={() => navigate(view)}
                >
                  <span aria-hidden="true">{symbol}</span>
                  {label}
                </button>
              ))}
            </div>
          </div>
        ))}
      </nav>
      <div className="sidebar-context">
        <strong>One Identity Manager</strong>
        <span>Fixed synthetic evidence</span>
        <span>Live operations remain disabled</span>
        <a href="/design-review/">Approved design preview ↗</a>
      </div>
    </aside>
  );
}
