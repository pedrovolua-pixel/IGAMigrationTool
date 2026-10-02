import { useEffect, useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import type {
  ReviewCommand,
  ReviewDetail,
  ReviewFinding,
  ReviewKind,
} from './demo-contract.generated';
import { DemoRequestError, request } from './api';

const labels: Record<ReviewKind, string> = {
  Confirm: 'Confirm',
  Reject: 'Reject',
  Defer: 'Defer',
  Comment: 'Add comment',
  EditPresentation: 'Save presentation',
};

export function ReviewPanel({
  review,
  csrfToken,
  onReload,
}: {
  review: ReviewDetail;
  csrfToken: string;
  onReload: (message?: string) => void;
}) {
  if (review.status !== 'Ready')
    return (
      <p className="warning-note">
        Review is unavailable for this saved run. No review action is inferred.
      </p>
    );
  return (
    <section className="subsection" aria-labelledby="review-heading">
      <h4 id="review-heading">Consultant review and history</h4>
      <p className="field-note">
        Local synthetic actor: {review.actor ?? 'Unavailable'}. Generated originals remain
        immutable. Confirm and Defer retain the penalty; Reject removes the current penalty. No risk
        acceptance or closure is enabled.
      </p>
      {!review.findings.length && <p>No generated findings need review in this fixture.</p>}
      {review.findings.map((finding) => (
        <FindingEditor
          key={`${review.runId}:${finding.id}`}
          finding={finding}
          runId={review.runId}
          csrfToken={csrfToken}
          onReload={onReload}
        />
      ))}
      <details>
        <summary>Coherent review snapshot digest</summary>
        <code>{review.snapshotDigest}</code>
      </details>
    </section>
  );
}

function FindingEditor({
  finding,
  runId,
  csrfToken,
  onReload,
}: {
  finding: ReviewFinding;
  runId: string;
  csrfToken: string;
  onReload: (message?: string) => void;
}) {
  const prefix = useId();
  const allowed: ReviewKind[] = [];
  if (finding.actions.confirm) allowed.push('Confirm');
  if (finding.actions.reject) allowed.push('Reject');
  if (finding.actions.defer) allowed.push('Defer');
  if (finding.actions.comment) allowed.push('Comment');
  if (finding.actions.editPresentation) allowed.push('EditPresentation');
  const [kind, setKind] = useState<ReviewKind>(allowed[0] ?? 'Comment');
  const [reason, setReason] = useState('');
  const [text, setText] = useState('');
  const [title, setTitle] = useState(finding.title);
  const [context, setContext] = useState(finding.businessContext);
  const [pending, setPending] = useState<ReviewCommand | null>(null);
  const [busy, setBusy] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const alive = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const errorFocus = useRef<HTMLDivElement>(null);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      controller.current?.abort();
    };
  }, [runId]);
  useEffect(() => {
    if (error) errorFocus.current?.focus();
  }, [error]);
  async function submit(command: ReviewCommand) {
    if (busy || refreshing) return;
    setBusy(true);
    setError(null);
    setPending(command);
    const attempt = new AbortController();
    controller.current = attempt;
    try {
      await request<unknown>(
        `/runs/${encodeURIComponent(runId)}/findings/${encodeURIComponent(finding.id)}/events`,
        attempt.signal,
        command,
        csrfToken,
      );
      if (!alive.current || attempt.signal.aborted) return;
      // A replay outcome may be an old revision. Always reread the entire analysis snapshot.
      setPending(null);
      setRefreshing(true);
      onReload('Review event saved. Findings, scores and history were reloaded together.');
    } catch (caught) {
      if (!alive.current || attempt.signal.aborted) return;
      if (caught instanceof DemoRequestError && caught.status < 500) {
        setPending(null);
        setRefreshing(false);
        setError(`${caught.message} Refresh the saved analysis before attempting another action.`);
      } else
        setError(
          'The response was lost or could not be verified. The event may already be saved. Retry the same event once you are ready, or refresh the saved analysis.',
        );
    } finally {
      if (alive.current && !attempt.signal.aborted) setBusy(false);
    }
  }
  function create(event: FormEvent) {
    event.preventDefault();
    if (pending || error || busy || refreshing || !allowed.includes(kind)) return;
    void submit({
      eventId: crypto.randomUUID(),
      expectedRevision: finding.revision,
      kind,
      reason: ['Confirm', 'Reject', 'Defer'].includes(kind) ? reason.trim() || null : null,
      text: kind === 'Comment' ? text : null,
      title: kind === 'EditPresentation' ? title : null,
      businessContext: kind === 'EditPresentation' ? context : null,
    });
  }
  const disabled = busy || refreshing || pending !== null || error !== null;
  return (
    <article className="review-finding">
      <h5>{finding.title}</h5>
      <p>
        {finding.category} · {finding.state} · Revision {finding.revision} ·{' '}
        {finding.occurrenceIds.length} affected occurrences
      </p>
      {finding.businessContext && <p className="review-text">{finding.businessContext}</p>}
      <details>
        <summary>Generated original</summary>
        <p>
          {finding.originalTitle} · Initial state: {finding.initialState}
        </p>
        <p className="record-meta">{finding.originalDigests.join(', ')}</p>
      </details>
      {!!allowed.length && (
        <form onSubmit={create} className="review-form">
          <label htmlFor={`${prefix}-action`}>Review action</label>
          <select
            id={`${prefix}-action`}
            value={kind}
            onChange={(event) => setKind(event.target.value as ReviewKind)}
            disabled={disabled}
          >
            {allowed.map((action) => (
              <option key={action} value={action}>
                {action === 'EditPresentation' ? 'Edit presentation' : labels[action]}
              </option>
            ))}
          </select>
          {['Confirm', 'Reject', 'Defer'].includes(kind) && (
            <>
              <label htmlFor={`${prefix}-reason`}>
                Review reason {kind === 'Reject' ? '(required)' : '(optional)'}
              </label>
              <textarea
                id={`${prefix}-reason`}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                maxLength={2000}
                required={kind === 'Reject'}
                disabled={disabled}
              />
            </>
          )}
          {kind === 'Comment' && (
            <>
              <label htmlFor={`${prefix}-comment`}>Comment</label>
              <textarea
                id={`${prefix}-comment`}
                value={text}
                onChange={(event) => setText(event.target.value)}
                maxLength={2000}
                required
                disabled={disabled}
              />
            </>
          )}
          {kind === 'EditPresentation' && (
            <>
              <label htmlFor={`${prefix}-title`}>Presentation title</label>
              <input
                id={`${prefix}-title`}
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                maxLength={250}
                required
                disabled={disabled}
              />
              <label htmlFor={`${prefix}-context`}>Business context</label>
              <textarea
                id={`${prefix}-context`}
                value={context}
                onChange={(event) => setContext(event.target.value)}
                maxLength={2000}
                disabled={disabled}
              />
            </>
          )}
          <button
            type="submit"
            disabled={
              disabled ||
              (kind === 'Reject' && !reason.trim()) ||
              (kind === 'Comment' && !text.trim()) ||
              (kind === 'EditPresentation' && !title.trim())
            }
          >
            {busy ? 'Saving…' : refreshing ? 'Refreshing analysis…' : labels[kind]}
          </button>
        </form>
      )}
      {error && (
        <div className="error-summary" role="alert" tabIndex={-1} ref={errorFocus}>
          <p>{error}</p>
          <div className="run-actions">
            {pending && (
              <button disabled={busy} onClick={() => void submit(pending)}>
                Retry same review event
              </button>
            )}
            <button
              disabled={busy}
              onClick={() => {
                setRefreshing(true);
                onReload();
              }}
            >
              Refresh saved analysis
            </button>
          </div>
        </div>
      )}
      <details className="review-history">
        <summary>Append-only history ({finding.history.length} events)</summary>
        {!finding.history.length ? (
          <p>No review events yet. The generated original is unchanged.</p>
        ) : (
          <ol>
            {finding.history.map((event) => (
              <li key={event.eventId}>
                <p>
                  <strong>{event.kind}</strong> · {event.state} · Revision {event.revision}
                </p>
                <p>
                  {event.actorId} ({event.actorRoles.join(', ')}) ·{' '}
                  <time dateTime={event.recordedAtUtc}>
                    {new Date(event.recordedAtUtc).toLocaleString()}
                  </time>
                </p>
                {event.reason !== null && <p className="review-text">Reason: {event.reason}</p>}
                {event.text !== null && <p className="review-text">Comment: {event.text}</p>}
                {event.title !== null && <p className="review-text">Title: {event.title}</p>}
                {event.businessContext !== null && (
                  <p className="review-text">Business context: {event.businessContext}</p>
                )}
              </li>
            ))}
          </ol>
        )}
      </details>
    </article>
  );
}
