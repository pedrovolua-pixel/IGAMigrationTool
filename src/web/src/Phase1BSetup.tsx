import { useEffect, useId, useRef, useState } from 'react';
import './Phase1BSetup.css';

export type Phase1BCategory = 'SECURITY' | 'OPERATIONS';
export type Phase1BOutcomeState =
  'Draft' | 'ConsultantReviewed' | 'CustomerApproved' | 'Superseded' | 'Retired';
/** Exact scorer identity is inventoryId/evidenceCategory. categoryId is trusted display/filter metadata only. */
export interface Phase1BCoverageKey {
  inventoryId: string;
  evidenceCategory: string;
  categoryId: Phase1BCategory;
}
export interface Phase1BHistoryEvent {
  eventId: string;
  kind: string;
  actorId: string;
  recordedAtUtc: string;
  reason: string;
}
export interface Phase1BOutcomeVersion {
  outcomeId: string;
  version: number;
  categoryId: Phase1BCategory;
  title: string;
  behavior: string;
  origin: 'Documented' | 'Inferred';
  unitLinks: readonly Phase1BCoverageKey[];
  referenceIds: readonly string[];
  assumptions: readonly string[];
  predecessorVersion: number | null;
  contentDigest: string;
  state: Phase1BOutcomeState;
  revision: number;
  reviewEventId: string | null;
  approvalEventId: string | null;
  history: readonly Phase1BHistoryEvent[];
}
export interface Phase1BOutcomeSelection {
  outcomeId: string;
  version: number;
  contentDigest: string;
  revision: number;
  approvalEventId: string;
}
export interface Phase1BDraftContent {
  outcomeId: string;
  version: number;
  categoryId: Phase1BCategory;
  title: string;
  behavior: string;
  origin: 'Documented' | 'Inferred';
  unitLinks: readonly Phase1BCoverageKey[];
  referenceIds: readonly string[];
  assumptions: readonly string[];
  predecessorVersion: number | null;
}
export interface Phase1BOutcomeCommand {
  eventId: string;
  kind: 'CreateDraft' | 'Review' | 'Approve' | 'Retire';
  outcomeId: string;
  version: number;
  expectedRevision: number;
  expectedContentDigest: string;
  expectedRegistryRevision: number;
  expectedReviewEventId: string | null;
  content: Phase1BDraftContent | null;
  reason: string;
}
export interface Phase1BStartCommand {
  eventId: string;
  expectedRegistryRevision: number;
  selections: readonly Phase1BOutcomeSelection[];
}
export type Phase1BActionResult = {
  status: 'committed' | 'rejected' | 'uncertain';
  eventId: string;
  contextKey: string;
  message?: string;
};
export type Phase1BAction<C> = (command: C, signal: AbortSignal) => Promise<Phase1BActionResult>;
export interface Phase1BSetupProps {
  contextKey: string;
  actorLabel: string;
  registryRevision: number;
  entries: readonly Phase1BOutcomeVersion[];
  availableCoverageKeys: readonly Phase1BCoverageKey[];
  canManage: boolean;
  canApprove: boolean;
  canStart: boolean;
  unavailableReason?: string;
  onOutcomeCommand: Phase1BAction<Phase1BOutcomeCommand>;
  onStartRun: Phase1BAction<Phase1BStartCommand>;
}
export function phase1BValidText(value: string, maximum = 2000): boolean {
  if (!value.trim() || value.length > maximum) return false;
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code === 0) return false;
    if (code >= 0xd800 && code <= 0xdbff) {
      const next = value.charCodeAt(++i);
      if (!(next >= 0xdc00 && next <= 0xdfff)) return false;
    } else if (code >= 0xdc00 && code <= 0xdfff) return false;
  }
  return true;
}
/**
 * Local UI integration only: adapters validate owning server receipts and refresh the
 * verified source before returning committed. contextKey binds scope + actor (and
 * the workspace appends runId); do not include source/revision in it. A CreateDraft
 * adapter seals the unmodified content and fills its expected digest server-side.
 * Adapters must not mutate the retained command. No authority comes from these props.
 */
export function usePhase1BAction<C extends { eventId: string }>(contextKey: string) {
  const [pending, setPending] = useState<{
    context: string;
    command: C;
    action: Phase1BAction<C>;
  } | null>(null);
  const [busy, setBusy] = useState(false),
    [message, setMessage] = useState('');
  const active = useRef<{ context: string; controller: AbortController } | null>(null);
  const currentContext = useRef(contextKey);
  currentContext.current = contextKey;
  useEffect(() => {
    active.current?.controller.abort();
    active.current = null;
    setPending(null);
    setBusy(false);
    setMessage('');
    return () => {
      active.current?.controller.abort();
      active.current = null;
    };
  }, [contextKey]);
  async function invoke(command: C, action: Phase1BAction<C>) {
    if (active.current || currentContext.current !== contextKey) return;
    const controller = new AbortController(),
      operation = { context: contextKey, controller };
    active.current = operation;
    setBusy(true);
    setPending({ context: contextKey, command, action });
    setMessage('Saving…');
    try {
      const result = await action(command, controller.signal);
      if (
        controller.signal.aborted ||
        currentContext.current !== contextKey ||
        active.current !== operation
      )
        return;
      if (
        result.eventId !== command.eventId ||
        result.contextKey !== contextKey ||
        result.status === 'uncertain'
      ) {
        setMessage('The response is uncertain. Retry the same event to resolve it.');
      } else {
        setPending(null);
        setMessage(
          result.message ??
            (result.status === 'committed'
              ? 'Saved. Refreshing the verified source.'
              : 'The server rejected the action. Refresh the source before a new action.'),
        );
      }
    } catch {
      if (
        !controller.signal.aborted &&
        currentContext.current === contextKey &&
        active.current === operation
      )
        setMessage('The response is uncertain. Retry the same event to resolve it.');
    } finally {
      if (active.current === operation) {
        active.current = null;
        setBusy(false);
      }
    }
  }
  return {
    busy,
    message,
    pending: pending?.context === contextKey ? pending.command : null,
    send: (command: C, action: Phase1BAction<C>) => {
      if (!pending) void invoke(command, action);
    },
    retry: () => {
      if (pending?.context === contextKey) void invoke(pending.command, pending.action);
    },
  };
}
export function Phase1BHistory({ events }: { events: readonly Phase1BHistoryEvent[] }) {
  return (
    <details>
      <summary>Immutable history ({events.length})</summary>
      <ol>
        {events.map((event) => (
          <li key={event.eventId}>
            <strong>{event.kind}</strong> · {event.actorId} ·{' '}
            <time dateTime={event.recordedAtUtc}>{event.recordedAtUtc}</time>
            <p className="phase1b-text">{event.reason}</p>
            <small>Event {event.eventId}</small>
          </li>
        ))}
      </ol>
    </details>
  );
}
export function Phase1BSetup(props: Phase1BSetupProps) {
  const id = useId(),
    action = usePhase1BAction<Phase1BOutcomeCommand | Phase1BStartCommand>(props.contextKey);
  const [outcomeId, setOutcomeId] = useState(''),
    [title, setTitle] = useState(''),
    [behavior, setBehavior] = useState('');
  const [category, setCategory] = useState<Phase1BCategory>('SECURITY'),
    [origin, setOrigin] = useState<'Documented' | 'Inferred'>('Documented');
  const [references, setReferences] = useState(''),
    [assumptions, setAssumptions] = useState(''),
    [reason, setReason] = useState('');
  const [links, setLinks] = useState<string[]>([]),
    [selected, setSelected] = useState<Phase1BOutcomeSelection[]>([]);
  const [selectionRevision, setSelectionRevision] = useState(props.registryRevision);
  const key = (entry: { outcomeId: string; version: number }) =>
    `${entry.outcomeId}/${entry.version}`;
  const coverageKey = (entry: Phase1BCoverageKey) =>
    `${entry.inventoryId}/${entry.evidenceCategory}`;
  const availableKeys = props.availableCoverageKeys.filter(
    (link, index, values) =>
      values.findIndex((value) => coverageKey(value) === coverageKey(link)) === index,
  );
  const lines = (value: string) =>
    value
      .split('\n')
      .map((line) => line.trim())
      .filter(Boolean);
  const predecessors = props.entries.filter((entry) => entry.outcomeId === outcomeId);
  const previous = predecessors.reduce<Phase1BOutcomeVersion | null>(
    (best, entry) => (!best || entry.version > best.version ? entry : best),
    null,
  );
  const locked = action.busy || action.pending !== null;
  const selectionsValid =
    (selected.length === 0 || selectionRevision === props.registryRevision) &&
    selected.every((selection) =>
      props.entries.some(
        (entry) =>
          key(entry) === key(selection) &&
          entry.state === 'CustomerApproved' &&
          entry.contentDigest === selection.contentDigest &&
          entry.revision === selection.revision &&
          entry.approvalEventId === selection.approvalEventId,
      ),
    );
  useEffect(() => {
    setSelected([]);
    setLinks([]);
    setReason('');
  }, [props.contextKey]);
  const command = (entry: Phase1BOutcomeVersion, kind: Phase1BOutcomeCommand['kind']) =>
    action.send(
      {
        eventId: crypto.randomUUID(),
        kind,
        outcomeId: entry.outcomeId,
        version: entry.version,
        expectedRevision: entry.revision,
        expectedContentDigest: entry.contentDigest,
        expectedRegistryRevision: props.registryRevision,
        expectedReviewEventId: entry.reviewEventId,
        content: null,
        reason,
      },
      props.onOutcomeCommand as Phase1BAction<Phase1BOutcomeCommand | Phase1BStartCommand>,
    );
  const validDraft =
    /^[A-Za-z0-9_.-]{1,128}$/.test(outcomeId) &&
    phase1BValidText(title, 256) &&
    phase1BValidText(behavior) &&
    phase1BValidText(reason) &&
    links.length <= 256 &&
    availableKeys.some(
      (link) => link.categoryId === category && links.includes(coverageKey(link)),
    ) &&
    (previous !== null || new Set(props.entries.map((entry) => entry.outcomeId)).size < 64) &&
    (previous?.version ?? 0) < Number.MAX_SAFE_INTEGER &&
    lines(references).length <= 256 &&
    new Set(lines(references)).size === lines(references).length &&
    lines(references).every((value) => /^[A-Za-z0-9_.-]{1,128}$/.test(value)) &&
    lines(assumptions).length <= 64 &&
    lines(assumptions).every((value) => phase1BValidText(value));
  return (
    <section className="phase1b-panel" aria-labelledby={`${id}-heading`}>
      <h2 id={`${id}-heading`}>Phase 1B outcome setup</h2>
      <p>
        Local fictional pilot · Actor simulation: <strong>{props.actorLabel}</strong>. Customer
        approval here is a fictional scope-specific capability.
      </p>
      {props.unavailableReason && <p role="alert">{props.unavailableReason}</p>}
      <p role="status" aria-live="polite">
        {action.message}
      </p>
      {action.pending && (
        <div className="phase1b-pending">
          <p>
            Pending event: <code>{action.pending.eventId}</code>. The exact payload is retained.
          </p>
          <button type="button" disabled={action.busy} onClick={action.retry}>
            Retry same event
          </button>
        </div>
      )}
      <label htmlFor={`${id}-reason`}>Reason for the next lifecycle action</label>
      <textarea
        id={`${id}-reason`}
        value={reason}
        maxLength={2000}
        disabled={locked}
        onChange={(event) => setReason(event.target.value)}
      />
      <details>
        <summary>Create an immutable draft version</summary>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (!validDraft || locked || !props.canManage) return;
            const content: Phase1BDraftContent = {
              outcomeId,
              version: (previous?.version ?? 0) + 1,
              categoryId: category,
              title,
              behavior,
              origin,
              unitLinks: availableKeys.filter(
                (link) => link.categoryId === category && links.includes(coverageKey(link)),
              ),
              referenceIds: lines(references),
              assumptions: lines(assumptions),
              predecessorVersion: previous?.version ?? null,
            };
            action.send(
              {
                eventId: crypto.randomUUID(),
                kind: 'CreateDraft',
                outcomeId,
                version: content.version,
                expectedRevision: 0,
                expectedContentDigest: '',
                expectedRegistryRevision: props.registryRevision,
                expectedReviewEventId: null,
                content,
                reason,
              },
              props.onOutcomeCommand as Phase1BAction<Phase1BOutcomeCommand | Phase1BStartCommand>,
            );
          }}
        >
          <div className="phase1b-grid">
            <label>
              Stable outcome ID
              <input
                value={outcomeId}
                maxLength={128}
                disabled={locked}
                onChange={(event) => setOutcomeId(event.target.value)}
                placeholder="access-governance"
              />
            </label>
            <label>
              Category
              <select
                value={category}
                disabled={locked}
                onChange={(event) => {
                  setCategory(event.target.value as Phase1BCategory);
                  setLinks([]);
                }}
              >
                <option>SECURITY</option>
                <option>OPERATIONS</option>
              </select>
            </label>
            <label>
              Title
              <input
                value={title}
                maxLength={256}
                disabled={locked}
                onChange={(event) => setTitle(event.target.value)}
              />
            </label>
            <label>
              Origin
              <select
                value={origin}
                disabled={locked}
                onChange={(event) => setOrigin(event.target.value as 'Documented' | 'Inferred')}
              >
                <option>Documented</option>
                <option>Inferred</option>
              </select>
            </label>
          </div>
          <p>
            New version {(previous?.version ?? 0) + 1}
            {previous ? `, successor to version ${previous.version}` : ''}. Existing content stays
            immutable.
          </p>
          <label>
            Desired behavior
            <textarea
              value={behavior}
              maxLength={2000}
              disabled={locked}
              onChange={(event) => setBehavior(event.target.value)}
            />
          </label>
          <fieldset disabled={locked}>
            <legend>Explicit applicable coverage units</legend>
            {availableKeys
              .filter((link) => link.categoryId === category)
              .map((link) => (
                <label className="phase1b-checkbox" key={coverageKey(link)}>
                  <input
                    type="checkbox"
                    checked={links.includes(coverageKey(link))}
                    onChange={(event) =>
                      setLinks((values) =>
                        event.target.checked
                          ? [...values, coverageKey(link)]
                          : values.filter((value) => value !== coverageKey(link)),
                      )
                    }
                  />
                  {link.inventoryId} · {link.evidenceCategory} · {link.categoryId}
                </label>
              ))}
          </fieldset>
          <label>
            Reference IDs (one per line)
            <textarea
              value={references}
              maxLength={2000}
              disabled={locked}
              onChange={(event) => setReferences(event.target.value)}
            />
          </label>
          <label>
            Assumptions (one per line)
            <textarea
              value={assumptions}
              maxLength={2000}
              disabled={locked}
              onChange={(event) => setAssumptions(event.target.value)}
            />
          </label>
          <button disabled={locked || !props.canManage || !validDraft}>Create draft version</button>
        </form>
      </details>
      <h3>Registry · revision {props.registryRevision}</h3>
      {!props.entries.length && <p>No outcome versions are registered.</p>}
      {props.entries.map((entry) => (
        <article className="phase1b-card" key={key(entry)}>
          <h4>
            {entry.title} · version {entry.version}
          </h4>
          <p>
            {entry.outcomeId} · {entry.categoryId} · <strong>{entry.state}</strong> · revision{' '}
            {entry.revision}
          </p>
          <p className="phase1b-text">{entry.behavior}</p>
          <p>
            Origin: {entry.origin}. Applicability:{' '}
            {entry.unitLinks.map((link) => coverageKey(link)).join(', ') || 'None'}.
          </p>
          <p className="phase1b-digest">
            Content: <code>{entry.contentDigest}</code>
          </p>
          {entry.referenceIds.length > 0 && (
            <p className="phase1b-text">References: {entry.referenceIds.join('\n')}</p>
          )}
          {entry.assumptions.length > 0 && (
            <p className="phase1b-text">Assumptions: {entry.assumptions.join('\n')}</p>
          )}
          <div className="phase1b-actions">
            {entry.state === 'Draft' && (
              <button
                disabled={locked || !props.canManage || !phase1BValidText(reason)}
                onClick={() => command(entry, 'Review')}
              >
                Consultant review {entry.outcomeId} v{entry.version}
              </button>
            )}
            {entry.state === 'ConsultantReviewed' && (
              <button
                disabled={
                  locked || !props.canApprove || !entry.reviewEventId || !phase1BValidText(reason)
                }
                onClick={() => command(entry, 'Approve')}
              >
                Fictional customer approval {entry.outcomeId} v{entry.version}
              </button>
            )}
            {['Draft', 'ConsultantReviewed', 'CustomerApproved'].includes(entry.state) && (
              <button
                disabled={
                  locked ||
                  !(entry.state === 'CustomerApproved' ? props.canApprove : props.canManage) ||
                  !phase1BValidText(reason)
                }
                onClick={() => command(entry, 'Retire')}
              >
                Retire {entry.outcomeId} v{entry.version}
              </button>
            )}
          </div>
          <Phase1BHistory events={entry.history} />
        </article>
      ))}
      <fieldset disabled={locked || !props.canStart}>
        <legend>Explicit approved versions for a new combined Phase 1B run</legend>
        {props.entries
          .filter((entry) => entry.state === 'CustomerApproved' && entry.approvalEventId)
          .map((entry) => (
            <label className="phase1b-checkbox" key={key(entry)}>
              <input
                type="checkbox"
                checked={selected.some((selection) => key(selection) === key(entry))}
                onChange={(event) => (
                  setSelectionRevision(props.registryRevision),
                  setSelected((values) =>
                    event.target.checked
                      ? [
                          ...values.filter((value) => value.outcomeId !== entry.outcomeId),
                          {
                            outcomeId: entry.outcomeId,
                            version: entry.version,
                            contentDigest: entry.contentDigest,
                            revision: entry.revision,
                            approvalEventId: entry.approvalEventId!,
                          },
                        ]
                      : values.filter((value) => key(value) !== key(entry)),
                  )
                )}
              />
              {entry.outcomeId} v{entry.version} · approval {entry.approvalEventId}
            </label>
          ))}
        <p>
          {selected.length} explicit selections. A registry change requires fresh selection.
          Existing run locks keep their original versions.
        </p>
        <button
          type="button"
          disabled={locked || selected.length === 0}
          onClick={() => {
            setSelected([]);
            setSelectionRevision(props.registryRevision);
          }}
        >
          Clear version selections
        </button>
        {!selectionsValid && (
          <p role="alert">
            A selected version changed. Clear it and select its current approved proof.
          </p>
        )}
        <button
          disabled={locked || !props.canStart || !selectionsValid}
          onClick={() =>
            action.send(
              {
                eventId: crypto.randomUUID(),
                expectedRegistryRevision: props.registryRevision,
                selections: selected,
              },
              props.onStartRun as Phase1BAction<Phase1BOutcomeCommand | Phase1BStartCommand>,
            )
          }
        >
          Start combined Phase 1B run with selected versions
        </button>
      </fieldset>
    </section>
  );
}
