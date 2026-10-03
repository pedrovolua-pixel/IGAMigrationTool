import { useEffect, useId, useState } from 'react';
import { Phase1BHistory, phase1BValidText, usePhase1BAction } from './Phase1BSetup';
import type {
  Phase1BAction,
  Phase1BCategory,
  Phase1BHistoryEvent,
  Phase1BOutcomeSelection,
} from './Phase1BSetup';
import './Phase1BWorkspace.css';

export type Phase1BPriorityBand = 'Low' | 'Medium' | 'High' | 'Immediate';
export type Phase1BEffortSize = 'XS' | 'S' | 'M' | 'L' | 'XL';
export interface Phase1BEffort {
  size: Phase1BEffortSize;
  minimumPersonHours: number;
  maximumPersonHours: number;
  policyVersion?: string;
}
export interface Phase1BOriginalContext {
  guidanceDigest: string;
  assumptions: readonly string[];
  prerequisites: readonly string[];
  sourceReferences: readonly string[];
  objectiveMapVersion: string;
  objectiveMapDigest: string;
  objectives: readonly { objectiveId: string; weight: number }[];
  matchedObjectiveIds: readonly string[];
}
export interface Phase1BPlanningEntry {
  optionId: string;
  findingId: string;
  categoryId: Phase1BCategory;
  revision: number;
  originalContext?: Phase1BOriginalContext;
  originalPriority: {
    policyVersion?: string;
    rawPriority?: number | null;
    displayPriority: number | null;
    originalBand: Phase1BPriorityBand | null;
    missingInputs: readonly string[];
    contributions: readonly {
      factor: string;
      normalized: number;
      weight: number;
      points: number;
    }[];
  };
  effectivePriority: Phase1BPriorityBand | null;
  originalEffort: Phase1BEffort | null;
  effectiveEffort: Phase1BEffort | null;
  effortApproval: 'Proposed' | 'ApprovedOriginal' | 'ApprovedReplacement';
  hasPriorityOverride: boolean;
  hasEffortOverride: boolean;
  history: readonly (Phase1BHistoryEvent & {
    sourceDigest: string;
    priorityOverride: Phase1BPriorityBand | null;
    replacementSize: Phase1BEffortSize | null;
    assumptions: readonly string[];
  })[];
}
export interface Phase1BPlanningCommand {
  eventId: string;
  kind:
    | 'ApproveOriginalEffort'
    | 'ReplaceEffort'
    | 'WithdrawEffortOverride'
    | 'OverridePriority'
    | 'WithdrawPriorityOverride';
  optionId: string;
  expectedRevision: number;
  expectedSourceDigest: string;
  priorityOverride: Phase1BPriorityBand | null;
  replacementSize: Phase1BEffortSize | null;
  assumptions: readonly string[];
  reason: string;
}
export interface Phase1BBudgetCommand {
  eventId: string;
  expectedRevision: number;
  runTarget: 900 | 1200;
  categoryTarget: 900 | 1200;
  category: Phase1BCategory;
  reason: string;
}
export interface Phase1BBudgetCounter {
  key: string;
  charged: number;
  held: number;
  allowance: number;
}
export interface Phase1BAttempt {
  attemptId: string;
  ordinal: number;
  state: string;
  held: boolean;
  inputUnits: number | null;
  outputUnits: number | null;
  receiptId: string | null;
}
export interface Phase1BWork {
  workId: string;
  category: Phase1BCategory;
  state: string;
  reasonCodes: readonly string[];
  attempts: readonly Phase1BAttempt[];
}
export interface Phase1BBudgetEvent extends Phase1BHistoryEvent {
  runTarget: number;
  categoryTarget: number;
  category: Phase1BCategory;
}
export interface Phase1BWorkspaceProps {
  runId: string;
  actorLabel: string;
  contextKey: string;
  lockedOutcomes: readonly Phase1BOutcomeSelection[];
  outcomeLockDigest: string;
  sourceDigest: string;
  entries: readonly Phase1BPlanningEntry[];
  canPlan: boolean;
  canOverrideBudget: boolean;
  unavailableReason?: string;
  budget: {
    revision: number;
    runAllowance: number;
    categoryAllowances: Readonly<Record<Phase1BCategory, number>>;
    counters: readonly Phase1BBudgetCounter[];
    history: readonly Phase1BBudgetEvent[];
  };
  works: readonly Phase1BWork[];
  onPlanningCommand: Phase1BAction<Phase1BPlanningCommand>;
  onBudgetOverride: Phase1BAction<Phase1BBudgetCommand>;
}
function effort(value: Phase1BEffort | null) {
  return value
    ? `${value.size} · ${value.minimumPersonHours}–${value.maximumPersonHours} fictional person-hours`
    : 'Unavailable';
}
function PlanningCard({
  entry,
  sourceDigest,
  disabled,
  send,
}: {
  entry: Phase1BPlanningEntry;
  sourceDigest: string;
  disabled: boolean;
  send: (command: Phase1BPlanningCommand) => void;
}) {
  const [reason, setReason] = useState(''),
    [assumptions, setAssumptions] = useState('');
  const [band, setBand] = useState<Phase1BPriorityBand>('Medium'),
    [size, setSize] = useState<Phase1BEffortSize>('M');
  const valid = phase1BValidText(reason),
    lines = assumptions
      .split('\n')
      .map((value) => value.trim())
      .filter(Boolean);
  const issue = (kind: Phase1BPlanningCommand['kind']) => {
    if (
      !valid ||
      disabled ||
      (kind === 'ReplaceEffort' &&
        (lines.length === 0 ||
          lines.length > 64 ||
          !lines.every((value) => phase1BValidText(value))))
    )
      return;
    send({
      eventId: crypto.randomUUID(),
      kind,
      optionId: entry.optionId,
      expectedRevision: entry.revision,
      expectedSourceDigest: sourceDigest,
      priorityOverride: kind === 'OverridePriority' ? band : null,
      replacementSize: kind === 'ReplaceEffort' ? size : null,
      assumptions: kind === 'ReplaceEffort' ? lines : [],
      reason,
    });
  };
  return (
    <article className="phase1b-card">
      <h4>Option {entry.optionId}</h4>
      <p>
        Finding {entry.findingId} · {entry.categoryId} · revision {entry.revision}
      </p>
      <dl className="phase1b-values">
        <div>
          <dt>Original calculated priority</dt>
          <dd>
            {entry.originalPriority.displayPriority ?? 'Unavailable'} ·{' '}
            {entry.originalPriority.originalBand ?? 'Unavailable'}
          </dd>
        </div>
        <div>
          <dt>Effective priority</dt>
          <dd>
            {entry.effectivePriority ?? 'Unavailable'}
            {entry.hasPriorityOverride ? ' · Consultant override' : ''}
          </dd>
        </div>
        <div>
          <dt>Original effort</dt>
          <dd>{effort(entry.originalEffort)}</dd>
        </div>
        <div>
          <dt>Effective effort</dt>
          <dd>
            {effort(entry.effectiveEffort)} · {entry.effortApproval}
          </dd>
        </div>
      </dl>
      {entry.originalPriority.missingInputs.length > 0 && (
        <p role="status">
          Priority inputs unavailable: {entry.originalPriority.missingInputs.join(', ')}.
        </p>
      )}
      <details>
        <summary>Original estimate context and provenance</summary>
        <p>
          Effort policy: {entry.originalEffort?.policyVersion ?? 'synthetic-effort-policy-v1'}.
          Priority policy: {entry.originalPriority.policyVersion ?? 'synthetic-priority-policy-v1'}.
        </p>
        {entry.originalContext ? (
          <>
            <p className="phase1b-digest">
              Original guidance proof: <code>{entry.originalContext.guidanceDigest}</code>
            </p>
            <p className="phase1b-text">
              Assumptions: {entry.originalContext.assumptions.join('\n') || 'None recorded'}
            </p>
            <p className="phase1b-text">
              Prerequisites: {entry.originalContext.prerequisites.join('\n') || 'None recorded'}
            </p>
            <p className="phase1b-text">
              Source references:{' '}
              {entry.originalContext.sourceReferences.join('\n') || 'None recorded'}
            </p>
            <p>Fictional objective map: {entry.originalContext.objectiveMapVersion}.</p>
            <p className="phase1b-digest">
              Map proof: <code>{entry.originalContext.objectiveMapDigest}</code>
            </p>
            <ul>
              {entry.originalContext.objectives.map((objective) => (
                <li key={objective.objectiveId}>
                  {objective.objectiveId} · weight {objective.weight}
                </li>
              ))}
            </ul>
            <p>
              Exact option matches: {entry.originalContext.matchedObjectiveIds.join(', ') || 'None'}
              .
            </p>
          </>
        ) : (
          <p>
            Resolve the original exact option and finding against its verified guidance source.
            Current guidance must not replace historical context.
          </p>
        )}
      </details>
      <details>
        <summary>Six-factor calculation</summary>
        <p>Unrounded priority: {entry.originalPriority.rawPriority ?? 'Unavailable'}.</p>
        <table>
          <caption>Versioned fictional priority contributions</caption>
          <thead>
            <tr>
              <th scope="col">Factor</th>
              <th scope="col">Normalized</th>
              <th scope="col">Weight</th>
              <th scope="col">Points</th>
            </tr>
          </thead>
          <tbody>
            {entry.originalPriority.contributions.map((value) => (
              <tr key={value.factor}>
                <th scope="row">{value.factor}</th>
                <td>{value.normalized}</td>
                <td>{value.weight}</td>
                <td>{value.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
      <fieldset disabled={disabled}>
        <legend>Explicit Consultant planning decision</legend>
        <label>
          Reason for {entry.optionId}
          <textarea
            value={reason}
            maxLength={2000}
            onChange={(event) => setReason(event.target.value)}
          />
        </label>
        <div className="phase1b-grid">
          <label>
            Priority override for {entry.optionId}
            <select
              value={band}
              onChange={(event) => setBand(event.target.value as Phase1BPriorityBand)}
            >
              {['Low', 'Medium', 'High', 'Immediate'].map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <label>
            Replacement relative effort for {entry.optionId}
            <select
              value={size}
              onChange={(event) => setSize(event.target.value as Phase1BEffortSize)}
            >
              {['XS', 'S', 'M', 'L', 'XL'].map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
        </div>
        <label>
          Effort assumptions for {entry.optionId} (one per line)
          <textarea
            value={assumptions}
            maxLength={2000}
            onChange={(event) => setAssumptions(event.target.value)}
          />
        </label>
        <div className="phase1b-actions">
          <button
            disabled={!valid || entry.originalPriority.originalBand === null}
            onClick={() => issue('OverridePriority')}
          >
            Override priority {entry.optionId}
          </button>
          <button
            disabled={!valid || !entry.hasPriorityOverride}
            onClick={() => issue('WithdrawPriorityOverride')}
          >
            Withdraw priority override {entry.optionId}
          </button>
          <button
            disabled={
              !valid ||
              !entry.originalEffort ||
              entry.hasEffortOverride ||
              entry.effortApproval === 'ApprovedOriginal'
            }
            onClick={() => issue('ApproveOriginalEffort')}
          >
            Approve original effort {entry.optionId}
          </button>
          <button
            disabled={
              !valid ||
              !entry.originalEffort ||
              lines.length === 0 ||
              lines.length > 64 ||
              !lines.every((value) => phase1BValidText(value))
            }
            onClick={() => issue('ReplaceEffort')}
          >
            Approve replacement effort {entry.optionId}
          </button>
          <button
            disabled={!valid || !entry.hasEffortOverride}
            onClick={() => issue('WithdrawEffortOverride')}
          >
            Withdraw effort override {entry.optionId}
          </button>
        </div>
      </fieldset>
      <Phase1BHistory events={entry.history} />
      {entry.history.length > 0 && (
        <details>
          <summary>Historic planning values and source bindings</summary>
          <ol>
            {entry.history.map((event) => (
              <li key={event.eventId}>
                <strong>{event.kind}</strong> · priority {event.priorityOverride ?? 'unchanged'} ·
                effort {event.replacementSize ?? 'unchanged'}
                <p className="phase1b-text">{event.assumptions.join('\n')}</p>
                <code className="phase1b-digest">{event.sourceDigest}</code>
              </li>
            ))}
          </ol>
        </details>
      )}
    </article>
  );
}
export function Phase1BWorkspace(props: Phase1BWorkspaceProps) {
  const id = useId(),
    context = `${props.contextKey}/${props.runId}`,
    action = usePhase1BAction<Phase1BPlanningCommand | Phase1BBudgetCommand>(context);
  const [category, setCategory] = useState<Phase1BCategory>('OPERATIONS'),
    [runTarget, setRunTarget] = useState<900 | 1200>(900),
    [categoryTarget, setCategoryTarget] = useState<900 | 1200>(900),
    [reason, setReason] = useState('');
  useEffect(() => {
    setReason('');
  }, [context]);
  const disabled = action.busy || action.pending !== null || !!props.unavailableReason;
  const eligible = props.works.some(
    (work) =>
      work.category === category && (work.state === 'Pending' || work.state === 'Retryable'),
  );
  const targetsValid =
    runTarget >= props.budget.runAllowance &&
    categoryTarget >= props.budget.categoryAllowances[category] &&
    (runTarget > props.budget.runAllowance ||
      categoryTarget > props.budget.categoryAllowances[category]);
  return (
    <section className="phase1b-panel" aria-labelledby={`${id}-heading`}>
      <h2 id={`${id}-heading`}>Combined Phase 1B workspace</h2>
      <p>
        Local fictional pilot · Actor simulation: <strong>{props.actorLabel}</strong> · Run{' '}
        <code>{props.runId}</code>.
      </p>
      <p>
        Original run inputs and approval history remain locked. Current planning decisions are a
        separate history. Relative effort ranges are fictional planning estimates.
      </p>
      {props.unavailableReason && <p role="alert">{props.unavailableReason}</p>}
      <p role="status" aria-live="polite">
        {action.message}
      </p>
      {action.pending && (
        <div className="phase1b-pending">
          <p>
            Pending event: <code>{action.pending.eventId}</code>. Retry retains its exact run,
            source and revision.
          </p>
          <button disabled={action.busy} onClick={action.retry}>
            Retry same event
          </button>
        </div>
      )}
      <details>
        <summary>Exact locked outcome versions ({props.lockedOutcomes.length})</summary>
        <p className="phase1b-digest">
          Lock: <code>{props.outcomeLockDigest}</code>
        </p>
        <ul>
          {props.lockedOutcomes.map((outcome) => (
            <li key={`${outcome.outcomeId}/${outcome.version}`}>
              {outcome.outcomeId} v{outcome.version} · revision {outcome.revision} · approval{' '}
              {outcome.approvalEventId}
              <p className="phase1b-digest">
                <code>{outcome.contentDigest}</code>
              </p>
            </li>
          ))}
        </ul>
      </details>
      <h3>AI usage ledger</h3>
      <p>
        Fictional units: reservation 300 per attempt; known actual input ≤100 and output ≤200.
        Unknown attempts keep their reservation.
      </p>
      <div className="phase1b-table-scroll">
        <table>
          <caption>Verified charged and held usage</caption>
          <thead>
            <tr>
              <th scope="col">Scope</th>
              <th scope="col">Charged</th>
              <th scope="col">Held</th>
              <th scope="col">Allowance</th>
            </tr>
          </thead>
          <tbody>
            {props.budget.counters.map((counter) => (
              <tr key={counter.key}>
                <th scope="row">{counter.key}</th>
                <td>{counter.charged}</td>
                <td>{counter.held}</td>
                <td>{counter.allowance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {props.works.map((work) => (
        <details key={work.workId}>
          <summary>
            {work.workId} · {work.category} · {work.state}
          </summary>
          <p>{work.reasonCodes.join(', ') || 'No terminal gap reason'}</p>
          <ol>
            {work.attempts.map((attempt) => (
              <li key={attempt.attemptId}>
                Attempt {attempt.ordinal} · {attempt.state} ·{' '}
                {attempt.held ? 'reservation held' : 'no reservation held'}
                <p>
                  Input {attempt.inputUnits ?? 'unknown'} · output{' '}
                  {attempt.outputUnits ?? 'unknown'} · receipt {attempt.receiptId ?? 'unavailable'}
                </p>
                <code>{attempt.attemptId}</code>
              </li>
            ))}
          </ol>
        </details>
      ))}
      <fieldset disabled={disabled || !props.canOverrideBudget}>
        <legend>Reasoned Consultant AI allowance override</legend>
        <div className="phase1b-grid">
          <label>
            Budget category
            <select
              value={category}
              onChange={(event) => setCategory(event.target.value as Phase1BCategory)}
            >
              <option>SECURITY</option>
              <option>OPERATIONS</option>
            </select>
          </label>
          <label>
            Run allowance target
            <select
              value={runTarget}
              onChange={(event) => setRunTarget(Number(event.target.value) as 900 | 1200)}
            >
              <option value={900}>900</option>
              <option value={1200}>1200</option>
            </select>
          </label>
          <label>
            Category allowance target
            <select
              value={categoryTarget}
              onChange={(event) => setCategoryTarget(Number(event.target.value) as 900 | 1200)}
            >
              <option value={900}>900</option>
              <option value={1200}>1200</option>
            </select>
          </label>
        </div>
        <label>
          Budget override reason
          <textarea
            value={reason}
            maxLength={2000}
            onChange={(event) => setReason(event.target.value)}
          />
        </label>
        <p>
          Overrides preserve all charged and held usage and the original allowance. They apply only
          while selected-category work is pending or retryable.
        </p>
        <button
          disabled={!eligible || !targetsValid || !phase1BValidText(reason)}
          onClick={() =>
            action.send(
              {
                eventId: crypto.randomUUID(),
                expectedRevision: props.budget.revision,
                runTarget,
                categoryTarget,
                category,
                reason,
              },
              props.onBudgetOverride as Phase1BAction<
                Phase1BPlanningCommand | Phase1BBudgetCommand
              >,
            )
          }
        >
          Apply reasoned AI allowance override
        </button>
      </fieldset>
      <Phase1BHistory events={props.budget.history} />
      {props.budget.history.length > 0 && (
        <ul>
          {props.budget.history.map((event) => (
            <li key={event.eventId}>
              Revision event {event.eventId} · run {event.runTarget} · {event.category}{' '}
              {event.categoryTarget}
            </li>
          ))}
        </ul>
      )}
      <h3>Priority and effort planning</h3>
      <p className="phase1b-digest">
        Current verified source: <code>{props.sourceDigest}</code>
      </p>
      {!props.entries.length && <p>No verified planning options are available.</p>}
      {props.entries.map((entry) => (
        <PlanningCard
          key={`${props.runId}/${entry.optionId}/${props.sourceDigest}`}
          entry={entry}
          sourceDigest={props.sourceDigest}
          disabled={disabled || !props.canPlan}
          send={(command) =>
            action.send(
              command,
              props.onPlanningCommand as Phase1BAction<
                Phase1BPlanningCommand | Phase1BBudgetCommand
              >,
            )
          }
        />
      ))}
    </section>
  );
}
