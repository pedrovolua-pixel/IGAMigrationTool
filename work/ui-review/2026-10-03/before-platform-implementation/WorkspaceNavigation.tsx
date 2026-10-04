import './workspace.css';

const destinations = [
  { label: 'Assessments', target: '#run-heading', symbol: '▦' },
  { label: 'Findings', target: '#findings-heading', symbol: '≡' },
  {
    label: 'Evidence',
    target: '#finding-evidence-heading',
    fallback: '#evidence-heading',
    symbol: '▤',
  },
  { label: 'Reports', target: '.draft-report', symbol: '▧' },
  { label: 'Settings', target: '#configuration-heading', symbol: '⚙' },
] as const;

export type WorkspaceView = 'Assessments' | 'Findings' | 'Evidence' | 'Reports' | 'Settings';

export function WorkspaceNavigation({
  active,
  onNavigate,
  onUnavailable,
}: {
  active: WorkspaceView;
  onNavigate: (view: WorkspaceView) => void;
  onUnavailable: (message: string) => void;
}) {
  function navigate(destination: (typeof destinations)[number]) {
    const target =
      document.querySelector<HTMLElement>(destination.target) ??
      ('fallback' in destination
        ? document.querySelector<HTMLElement>(destination.fallback)
        : null);
    if (!target) {
      onNavigate('Assessments');
      onUnavailable(
        `${destination.label} is not available in the selected run yet. Open or start a synthetic run; findings and reports require an analysis-enabled profile and verified local results.`,
      );
      requestAnimationFrame(() => {
        const fallback = document.querySelector<HTMLElement>('#run-heading');
        fallback?.focus({ preventScroll: true });
        fallback?.scrollIntoView({ block: 'start' });
      });
      return;
    }
    onUnavailable('');
    onNavigate(destination.label);
    // Existing report sections have no focus stop; make the section programmatically focusable.
    requestAnimationFrame(() => {
      target.tabIndex = -1;
      target.focus({ preventScroll: true });
      target.scrollIntoView({ block: 'start' });
    });
  }

  return (
    <aside className="workspace-sidebar">
      <div className="workspace-identity">
        <span className="workspace-monogram" aria-hidden="true">
          I
        </span>
        <div>
          <strong>IGA workspace</strong>
          <span>Health assessment pilot</span>
        </div>
      </div>
      <nav aria-label="Workspace sections">
        <p className="sidebar-label">Workspace views</p>
        <div className="workspace-destinations">
          {destinations.map((destination) => (
            <button
              key={destination.label}
              type="button"
              aria-current={active === destination.label ? 'location' : undefined}
              onClick={() => navigate(destination)}
            >
              <span aria-hidden="true">{destination.symbol}</span>
              {destination.label}
            </button>
          ))}
        </div>
      </nav>
      <div className="sidebar-context">
        <strong>Local pilot</strong>
        <span>Fixed synthetic evidence</span>
        <span>Drafts stay unpublished</span>
      </div>
    </aside>
  );
}
