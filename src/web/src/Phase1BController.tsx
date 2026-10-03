import { useEffect, useRef, useState } from 'react';
import { request, DemoRequestError } from './api';
import { Phase1BSetup } from './Phase1BSetup';
import type {
  Phase1BActionResult,
  Phase1BOutcomeVersion,
  Phase1BOutcomeCommand,
  Phase1BStartCommand,
  Phase1BCoverageKey,
} from './Phase1BSetup';
import { Phase1BWorkspace } from './Phase1BWorkspace';
import type {
  Phase1BWorkspaceProps,
  Phase1BPlanningCommand,
  Phase1BBudgetCommand,
  Phase1BBudgetEvent,
} from './Phase1BWorkspace';
import type {
  RunDetail,
  ArtifactReviewCommand,
  PlanningTaskCommand,
  PlanningTaskKind,
  ArtifactReviewKind,
} from './demo-contract.generated';
import { ReviewPanel } from './ReviewPanel';
import { RecommendationGuidanceView } from './RecommendationGuidanceView';
import { MaturityView } from './MaturityView';
import { verifyPhase1BAnalysis } from './phase1b-contract';
import type { Phase1BAnalysisDetail } from './phase1b-contract';

const prefix = '/phase1b';
const contextKey =
  'synthetic-customer/synthetic-project/synthetic-environment/synthetic-consultant';
type Registry = {
  revision: number;
  entries: {
    content: Omit<
      Phase1BOutcomeVersion,
      'state' | 'revision' | 'reviewEventId' | 'approvalEventId' | 'history'
    > & { unitLinks: { inventoryId: string; evidenceCategory: string }[] };
    state: Phase1BOutcomeVersion['state'];
    revision: number;
    history: (Phase1BOutcomeVersion['history'][number] & {
      state: string;
      contentDigest: string;
    })[];
  }[];
};
type Fixture = {
  actor: string;
  canManageOutcomes: boolean;
  canSimulateCustomerApproval: boolean;
  canStart: boolean;
  availableCoverageKeys: Phase1BCoverageKey[];
};
type Receipt = {
  receipt?: Record<string, unknown>;
  value?: Record<string, unknown>;
  runId?: string;
};
function actionFailure(error: unknown, eventId: string, context: string): Phase1BActionResult {
  return {
    status: error instanceof DemoRequestError && error.status < 500 ? 'rejected' : 'uncertain',
    eventId,
    contextKey: context,
    message:
      error instanceof Error
        ? error.message
        : 'Response unavailable; refresh and explicitly retry the same saved event.',
  };
}
function verifyReceipt(value: Receipt, eventId: string, expected: Record<string, string | number>) {
  const receipt = value.receipt ?? value.value;
  if (
    !receipt ||
    receipt.eventId !== eventId ||
    !Number.isSafeInteger(receipt.revision) ||
    (receipt.revision as number) < 1 ||
    Object.entries(expected).some(([key, wanted]) => receipt[key] !== wanted)
  )
    throw new Error('The exact event receipt could not be verified.');
}
export function Phase1BSetupController({
  csrfToken,
  onStarted,
}: {
  csrfToken: string;
  onStarted: (run: RunDetail) => void;
}) {
  const [data, setData] = useState<{ registry: Registry; fixture: Fixture } | null>(null),
    [failure, setFailure] = useState(''),
    [retry, setRetry] = useState(0);
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    const c = new AbortController();
    void refresh(c.signal).catch((e) => {
      if (!c.signal.aborted) setFailure(e.message);
    });
    return () => {
      active.current = false;
      c.abort();
    };
  }, [retry]);
  async function refresh(signal: AbortSignal) {
    const [registry, fixture] = await Promise.all([
      request<Registry>(prefix + '/registry', signal),
      request<Fixture>(prefix + '/fixture', signal),
    ]);
    if (signal.aborted || !active.current) return;
    if (
      !Number.isSafeInteger(registry.revision) ||
      registry.revision < 0 ||
      registry.entries.length > 256 ||
      fixture.actor !== 'synthetic-consultant' ||
      fixture.availableCoverageKeys.length !== 12 ||
      registry.entries.some((e) =>
        e.content.unitLinks.some(
          (k) =>
            !fixture.availableCoverageKeys.some(
              (v) => v.inventoryId === k.inventoryId && v.evidenceCategory === k.evidenceCategory,
            ),
        ),
      )
    )
      throw new Error('The fictional registry could not be verified.');
    setData({ registry, fixture });
    setFailure('');
    return { registry, fixture };
  }
  async function outcome(
    command: Phase1BOutcomeCommand,
    signal: AbortSignal,
  ): Promise<Phase1BActionResult> {
    try {
      const body = {
        ...command,
        content: command.content
          ? {
              ...command.content,
              unitLinks: command.content.unitLinks.map((k) => ({
                inventoryId: k.inventoryId,
                evidenceCategory: k.evidenceCategory,
              })),
              contentDigest: '',
            }
          : null,
      };
      const value = await request<Receipt>(prefix + '/outcomes/events', signal, body, csrfToken);
      const original = data?.registry.entries.find(
        (e) => e.content.outcomeId === command.outcomeId && e.content.version === command.version,
      );
      const actor =
        command.kind === 'Approve' ||
        (command.kind === 'Retire' && original?.history.some((h) => h.kind === 'Approve'))
          ? 'synthetic-customer-outcome-approver-v1'
          : 'synthetic-consultant';
      verifyReceipt(value, command.eventId, {
        schemaVersion: 'synthetic-outcome-receipt-v1',
        actorId: actor,
        outcomeId: command.outcomeId,
        version: command.version,
        revision: command.expectedRevision + 1,
      });
      const refreshed = await refresh(signal);
      const saved = refreshed?.registry.entries.find(
        (e) => e.content.outcomeId === command.outcomeId && e.content.version === command.version,
      );
      const event = saved?.history.find((h) => h.eventId === command.eventId);
      if (
        !saved ||
        !event ||
        event.kind !== command.kind ||
        event.actorId !== actor ||
        value.receipt?.contentDigest !== saved.content.contentDigest ||
        event.contentDigest !== saved.content.contentDigest
      )
        throw new Error('The saved immutable outcome event could not be verified.');
      return { status: 'committed', eventId: command.eventId, contextKey };
    } catch (e) {
      return actionFailure(e, command.eventId, contextKey);
    }
  }
  async function start(
    command: Phase1BStartCommand,
    signal: AbortSignal,
  ): Promise<Phase1BActionResult> {
    try {
      const run = await request<RunDetail>(
        prefix + '/runs',
        signal,
        {
          requestId: command.eventId,
          selections: command.selections,
          explicitlyNoOutcomes: command.selections.length === 0,
        },
        csrfToken,
      );
      if (
        run.runId !== command.eventId ||
        run.selection.profileId !== 'synthetic-phase1b-combined-v1'
      )
        throw new Error('The saved run receipt could not be verified.');
      if (!signal.aborted && active.current) onStarted(run);
      return { status: 'committed', eventId: command.eventId, contextKey };
    } catch (e) {
      return actionFailure(e, command.eventId, contextKey);
    }
  }
  if (!data)
    return (
      <section aria-label="Phase 1B setup">
        <p role={failure ? 'alert' : 'status'}>
          {failure || 'Loading the approved fictional outcome registry…'}
        </p>
        {failure && (
          <button type="button" onClick={() => setRetry((x) => x + 1)}>
            Refresh outcome registry
          </button>
        )}
      </section>
    );
  const entries = data.registry.entries.map((e) => ({
    ...e.content,
    state: e.state,
    revision: e.revision,
    history: e.history,
    unitLinks: e.content.unitLinks.map((k) => {
      const key = data.fixture.availableCoverageKeys.find(
        (v) => v.inventoryId === k.inventoryId && v.evidenceCategory === k.evidenceCategory,
      );
      if (!key) throw new Error('Unverifiable applicability key.');
      return key;
    }),
    reviewEventId: [...e.history].reverse().find((h) => h.kind === 'Review')?.eventId ?? null,
    approvalEventId: [...e.history].reverse().find((h) => h.kind === 'Approve')?.eventId ?? null,
  }));
  return (
    <Phase1BSetup
      contextKey={contextKey}
      actorLabel={data.fixture.actor}
      registryRevision={data.registry.revision}
      entries={entries}
      availableCoverageKeys={data.fixture.availableCoverageKeys}
      canManage={data.fixture.canManageOutcomes && !failure}
      canApprove={data.fixture.canSimulateCustomerApproval && !failure}
      canStart={data.fixture.canStart && !failure}
      unavailableReason={failure || undefined}
      onOutcomeCommand={outcome}
      onStartRun={start}
    />
  );
}
type Workspace = {
  runId: string;
  actor: string;
  canPlan: boolean;
  canOverrideBudget: boolean;
  analysis: Phase1BAnalysisDetail;
  fictionalDefaults: string;
  outcomes: {
    contentDigest: string;
    outcomes: {
      content: { outcomeId: string; version: number; contentDigest: string };
      approval: { revision: number; eventId: string };
    }[];
  };
  ai: {
    budget: Omit<Phase1BWorkspaceProps['budget'], 'categoryAllowances' | 'history'> & {
      categoryAllowances: { key: string; value: number }[];
      history: Omit<Phase1BBudgetEvent, 'kind'>[];
    };
    works: {
      work: { workId: string; category: 'SECURITY' | 'OPERATIONS' };
      state: string;
      outcomes: { reasonCode: string | null }[];
      attempts: {
        key: { attemptId: string; ordinal: number };
        state: string;
        held: boolean;
        receipt: { inputUse: number; outputUse: number; receiptId: string } | null;
      }[];
    }[];
  };
  priority: {
    source: { sourceDigest: string };
    entries: (Omit<
      Phase1BWorkspaceProps['entries'][number],
      'optionId' | 'findingId' | 'categoryId'
    > & {
      original: { optionId: string; findingId: string; categoryId: 'SECURITY' | 'OPERATIONS' };
    })[];
  };
};
export function Phase1BRunController({ run, csrfToken }: { run: RunDetail; csrfToken: string }) {
  const [ledger, setLedger] = useState<Pick<
    Workspace,
    'ai' | 'outcomes' | 'actor' | 'canOverrideBudget'
  > | null>(null);
  const [value, setValue] = useState<Workspace | null>(null),
    [failure, setFailure] = useState(''),
    [retry, setRetry] = useState(0),
    [notice, setNotice] = useState('');
  const runToken = `${run.runId}/${run.revision}/${run.state}`;
  const current = useRef(runToken);
  current.current = runToken;
  const readGeneration = useRef(0);
  const [refreshing, setRefreshing] = useState(false);
  useEffect(() => {
    const c = new AbortController();
    const generation = readGeneration.current + 1;
    void refresh(c.signal).catch((e) => {
      if (
        !c.signal.aborted &&
        current.current === runToken &&
        generation === readGeneration.current
      ) {
        setFailure(e.message);
        setRefreshing(false);
      }
    });
    return () => c.abort();
  }, [run.runId, run.revision, run.state, retry]);
  async function refresh(signal: AbortSignal) {
    const generation = ++readGeneration.current;
    setRefreshing(true);
    if (run.state !== 'Scoring') {
      const next = await request<
        Pick<Workspace, 'ai' | 'outcomes' | 'actor' | 'canOverrideBudget'> & {
          runId: string;
          runRevision: number;
        }
      >(`${prefix}/runs/${run.runId}/ledger`, signal);
      if (signal.aborted || current.current !== runToken || generation !== readGeneration.current)
        return;
      if (
        next.runId !== run.runId ||
        next.actor !== 'synthetic-consultant' ||
        !Number.isSafeInteger(next.runRevision) ||
        next.runRevision < run.revision
      )
        throw new Error('The saved automatic AI ledger could not be verified.');
      setLedger(next);
      setFailure('');
      setRefreshing(false);
      return next;
    }
    const next = await request<Workspace>(`${prefix}/runs/${run.runId}/workspace`, signal);
    if (signal.aborted || current.current !== runToken || generation !== readGeneration.current)
      return;
    if (
      next.runId !== run.runId ||
      next.analysis.runId !== run.runId ||
      next.analysis.runRevision !== run.revision ||
      next.analysis.status !== 'Ready' ||
      next.actor !== 'synthetic-consultant' ||
      next.analysis.review?.snapshotDigest !== next.analysis.reviewSnapshotDigest
    )
      throw new Error('The combined saved source could not be verified.');
    verifyPhase1BAnalysis(next.analysis, run.runId, run.revision);
    setValue(next);
    setFailure('');
    setRefreshing(false);
    return next;
  }
  async function event(
    command: Phase1BPlanningCommand | Phase1BBudgetCommand,
    signal: AbortSignal,
    budget: boolean,
  ): Promise<Phase1BActionResult> {
    const context = `${contextKey}/${run.runId}`;
    const operationToken = runToken;
    try {
      const receipt = await request<Receipt>(
        `${prefix}/runs/${run.runId}/${budget ? 'ai/budget' : 'planning'}/events`,
        signal,
        command,
        csrfToken,
      );
      if (budget) {
        const c = command as Phase1BBudgetCommand;
        verifyReceipt(receipt, c.eventId, {
          actorId: 'synthetic-consultant',
          revision: c.expectedRevision + 1,
          runTarget: c.runTarget,
          categoryTarget: c.categoryTarget,
          category: c.category,
        });
        if (receipt.runId !== run.runId)
          throw new Error('Budget run binding could not be verified.');
      } else {
        const c = command as Phase1BPlanningCommand;
        verifyReceipt(receipt, c.eventId, {
          schemaVersion: 'synthetic-planning-receipt-v1',
          actorId: 'synthetic-consultant',
          runId: run.runId,
          optionId: c.optionId,
          revision: c.expectedRevision + 1,
          sourceDigest: c.expectedSourceDigest,
        });
      }
      if (signal.aborted || current.current !== operationToken)
        throw new Error('Response context changed; refresh before retrying the same event.');
      const updated = await refresh(signal);
      if (!updated)
        throw new Error('A newer source must be verified before accepting the response.');
      if (!budget) {
        if (!('priority' in updated))
          throw new Error('The current planning source could not be verified.');
        const c = command as Phase1BPlanningCommand;
        const event = updated.priority.entries
          .find((e) => e.original.optionId === c.optionId)
          ?.history.find((h) => h.eventId === c.eventId);
        if (!event || event.kind !== c.kind || event.sourceDigest !== c.expectedSourceDigest)
          throw new Error('Saved planning event could not be verified.');
      }
      return { status: 'committed', eventId: command.eventId, contextKey: context };
    } catch (e) {
      return actionFailure(e, command.eventId, context);
    }
  }
  const visible = value ?? null,
    execution = run.state === 'Scoring' ? value : ledger;
  const budget = execution
    ? {
        ...execution.ai.budget,
        history: execution.ai.budget.history.map((e) => ({ ...e, kind: 'BudgetOverride' })),
        categoryAllowances: Object.fromEntries(
          execution.ai.budget.categoryAllowances.map((x) => [x.key, x.value]),
        ) as Phase1BWorkspaceProps['budget']['categoryAllowances'],
      }
    : null;
  return (
    <section aria-label="Phase 1B combined assessment">
      <p role="status">{notice}</p>
      <h2>Combined fictional health assessment</h2>
      <p role={failure ? 'alert' : 'status'}>
        {failure ||
          (refreshing
            ? 'Verifying the current source; new actions are disabled.'
            : run.state !== 'Scoring'
              ? 'Automatic fictional AI runs after deterministic work. Charges and holds are saved durably.'
              : '')}
      </p>
      <button type="button" onClick={() => setRetry((x) => x + 1)}>
        Refresh combined workspace
      </button>
      {execution && budget && (
        <Phase1BWorkspace
          runId={run.runId}
          actorLabel={execution.actor}
          contextKey={contextKey}
          lockedOutcomes={execution.outcomes.outcomes.map((o) => ({
            ...o.content,
            revision: o.approval.revision,
            approvalEventId: o.approval.eventId,
          }))}
          outcomeLockDigest={execution.outcomes.contentDigest}
          sourceDigest={visible?.priority.source.sourceDigest ?? ''}
          entries={visible?.priority.entries.map((e) => ({ ...e, ...e.original })) ?? []}
          budget={budget}
          works={execution.ai.works.map((w) => ({
            ...w.work,
            state: w.state,
            reasonCodes: w.outcomes.flatMap((o) => (o.reasonCode ? [o.reasonCode] : [])),
            attempts: w.attempts.map((a) => ({
              ...a.key,
              state: a.state,
              held: a.held,
              inputUnits: a.receipt?.inputUse ?? null,
              outputUnits: a.receipt?.outputUse ?? null,
              receiptId: a.receipt?.receiptId ?? null,
            })),
          }))}
          canPlan={!!visible?.canPlan && !refreshing && !failure}
          canOverrideBudget={execution.canOverrideBudget && !refreshing && !failure}
          unavailableReason={failure || (refreshing ? 'Verifying current source.' : undefined)}
          onPlanningCommand={(c, s) => event(c, s, false)}
          onBudgetOverride={(c, s) => event(c, s, true)}
        />
      )}
      {visible && (
        <div hidden={!!failure}>
          <p>
            Provisional health: {visible.analysis.provisional?.display ?? 'Unavailable'} ·
            Publishable-current health:{' '}
            {visible.analysis.publishableCurrent?.display ?? 'Unavailable'}
          </p>
          <p>
            Executable units: {visible.analysis.quality?.executedUnits} /{' '}
            {visible.analysis.quality?.plannedUnits}; explained gaps:{' '}
            {visible.analysis.quality?.gapUnits}. Health and independent maturity stay separate.
          </p>
          <p>{visible.fictionalDefaults}</p>
          <ul>
            {visible.analysis.warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
          {visible.analysis.outcomes.map((o) => (
            <p key={o.id}>
              Locked outcome {o.id}: provisional {o.provisional.display ?? 'Unavailable'};
              publishable-current {o.publishableCurrent.display ?? 'Unavailable'}
            </p>
          ))}
          <div>
            {visible.analysis.findings.map((f) => (
              <article key={f.id} id={`phase1b-finding-${f.id}`} tabIndex={-1}>
                <h3>{f.title}</h3>
                <p>
                  {f.method} · {f.severity} · {f.state} · {f.confidencePercent}% confidence
                </p>
                <p>{f.inferences.join(' ')}</p>
                <p>Object references: {f.objectIds.join(', ')}</p>
                <details>
                  <summary>Original provenance and limitations</summary>
                  <p>{f.originalDigests.join(' ')}</p>
                  <p>{f.evidenceReferences.join(' ')}</p>
                  <p>{f.limitations.join(' ')}</p>
                </details>
              </article>
            ))}
          </div>
          {visible.analysis.maturity && <MaturityView maturity={visible.analysis.maturity} />}
          {visible.analysis.review && (
            <div inert={refreshing}>
              <ReviewPanel
                review={visible.analysis.review}
                csrfToken={csrfToken}
                onReload={(message) => {
                  setNotice(message ?? 'Review saved; verifying current source.');
                  setRetry((x) => x + 1);
                }}
              />
            </div>
          )}
          <RecommendationGuidanceView guidance={visible.analysis.recommendationGuidance} />
          <Phase1BPlanningFlow
            refreshing={refreshing || !!failure}
            runId={run.runId}
            csrfToken={csrfToken}
            analysis={visible.analysis}
            onReload={(message) => {
              setNotice(message);
              setRetry((x) => x + 1);
            }}
          />
        </div>
      )}
    </section>
  );
}
function Phase1BPlanningFlow({
  runId,
  csrfToken,
  analysis,
  onReload,
  refreshing,
}: {
  refreshing: boolean;
  runId: string;
  csrfToken: string;
  analysis: Phase1BAnalysisDetail;
  onReload: (message: string) => void;
}) {
  const [reason, setReason] = useState(''),
    [pending, setPending] = useState<{
      path: string;
      body: ArtifactReviewCommand | PlanningTaskCommand;
    } | null>(null),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState('');
  const active = useRef<AbortController | null>(null);
  const current = useRef(runId);
  current.current = runId;
  useEffect(() => {
    setPending(null);
    setBusy(false);
    setMessage('');
    return () => {
      active.current?.abort();
    };
  }, [runId]);
  async function send(command: {
    path: string;
    body: ArtifactReviewCommand | PlanningTaskCommand;
  }) {
    if (busy) return;
    setPending(command);
    setBusy(true);
    const c = new AbortController();
    active.current = c;
    try {
      const result = await request<Receipt>(command.path, c.signal, command.body, csrfToken);
      const artifact = command.path.includes('/artifacts/');
      const target = command.path.split('/')[command.path.split('/').length - 2]!;
      verifyReceipt(result, command.body.eventId, {
        schemaVersion: artifact
          ? 'synthetic-fix-review-receipt-v1'
          : 'synthetic-planning-task-receipt-v1',
        actorId: 'synthetic-consultant',
        runId,
        [artifact ? 'artifactId' : 'taskId']: target,
        kind: command.body.kind,
        revision: command.body.expectedRevision + 1,
        sourceDigest: command.body.expectedSourceDigest,
      });
      if (c.signal.aborted || active.current !== c || current.current !== runId) return;
      setPending(null);
      onReload('Planning event saved. Current source is being verified.');
    } catch (e) {
      if (c.signal.aborted || active.current !== c || current.current !== runId) return;
      setMessage(e instanceof Error ? e.message : 'Response unavailable.');
      if (e instanceof DemoRequestError && e.status < 500) setPending(null);
    } finally {
      if (!c.signal.aborted && active.current === c && current.current === runId) setBusy(false);
    }
  }
  const artifacts = analysis.artifactReview,
    tasks = analysis.planningTasks;
  function artifact(id: string, kind: ArtifactReviewKind, revision: number) {
    if (!artifacts?.source) return;
    void send({
      path: `${prefix}/runs/${runId}/artifacts/${id}/events`,
      body: {
        eventId: crypto.randomUUID(),
        kind,
        expectedRevision: revision,
        expectedSourceDigest: artifacts.source.sourceDigest,
        reason,
      },
    });
  }
  function task(
    id: string,
    kind: PlanningTaskKind,
    revision: number,
    attestations: PlanningTaskCommand['expectedAttestations'],
  ) {
    if (!tasks?.source) return;
    void send({
      path: `${prefix}/runs/${runId}/tasks/${id}/events`,
      body: {
        eventId: crypto.randomUUID(),
        kind,
        expectedRevision: revision,
        expectedSourceDigest: tasks.source.artifactSource.sourceDigest,
        expectedAttestations: attestations,
        reason,
      },
    });
  }
  return (
    <section aria-label="Fictional artifact and task planning">
      <h2>Artifact attestations and planning tasks</h2>
      <p>
        Planning attestations and task completion do not execute or validate remediation. Priority
        changes do not alter task freshness.
      </p>
      <label htmlFor="phase1b-planning-reason">Reason for artifact or task event</label>
      <textarea
        id="phase1b-planning-reason"
        maxLength={2000}
        value={reason}
        disabled={busy || pending !== null}
        onChange={(e) => setReason(e.target.value)}
      />
      <p role="status">{message}</p>
      {pending && !busy && (
        <button type="button" disabled={refreshing} onClick={() => void send(pending)}>
          Retry the same planning event
        </button>
      )}
      {analysis.fixPackages?.snapshot && (
        <details>
          <summary>Fictional unverified fix artifact examples</summary>
          <p>{analysis.fixPackages.snapshot.disclaimer}</p>
          {analysis.fixPackages.snapshot.packages.map((p) => (
            <article key={p.packageId}>
              <h3>Finding {p.findingId}</h3>
              {p.options.map((o) => (
                <div key={o.scopedOptionId}>
                  <h4>Option {o.scopedOptionId}</h4>
                  {o.artifacts.map((a) => (
                    <figure key={a.artifactId}>
                      <figcaption>
                        {a.kind} · {a.status} · {a.artifactId}
                      </figcaption>
                      <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                        {a.text}
                      </pre>
                    </figure>
                  ))}
                </div>
              ))}
            </article>
          ))}
        </details>
      )}
      {artifacts?.artifacts.map((e) => (
        <article key={e.artifactId}>
          <h3>Fictional artifact · {e.kind}</h3>
          <p>
            {e.state} · Revision {e.revision}
          </p>
          <p>Finding {e.findingId}</p>
          <details>
            <summary>Inert artifact reference and history</summary>
            <code>{e.artifactId}</code>
            <ul>
              {e.history.map((h) => (
                <li key={h.eventId}>
                  {h.kind} · {h.actorId} · {h.reason}
                </li>
              ))}
            </ul>
          </details>
          <button
            type="button"
            disabled={refreshing || busy || !!pending || !reason.trim() || !e.canReview}
            onClick={() => artifact(e.artifactId, 'ReviewForPlanning', e.revision)}
          >
            Review for fictional planning
          </button>
          <button
            type="button"
            disabled={refreshing || busy || !!pending || !reason.trim() || !e.canWithdraw}
            onClick={() => artifact(e.artifactId, 'WithdrawReview', e.revision)}
          >
            Withdraw artifact review
          </button>
        </article>
      ))}
      {tasks?.options.map((o) => (
        <article key={o.identity.taskId}>
          <h3>Task option · {o.identity.scopedOptionId}</h3>
          <p>Finding state: {o.findingState}</p>
          <button
            type="button"
            disabled={refreshing || busy || !!pending || !reason.trim() || !o.canCreate}
            onClick={() => task(o.identity.taskId, 'Create', 0, o.currentAttestations)}
          >
            Create fictional planning task
          </button>
        </article>
      ))}
      {tasks?.entries.map((e) => (
        <article key={e.identity.taskId} id={`phase1b-task-${e.identity.taskId}`} tabIndex={-1}>
          <h3>Planning task · {e.identity.taskId}</h3>
          <p>
            {e.status} · {e.freshness} · Revision {e.revision} · Assignee {e.assigneeId}
          </p>
          {(
            [
              ['ReconfirmPlan', e.canReconfirm],
              ['StartProgress', e.canStart],
              ['ReturnToPlanned', e.canReturnToPlanned],
              ['Complete', e.canComplete],
              ['Cancel', e.canCancel],
              ['Reopen', e.canReopen],
              ['Comment', e.canComment],
            ] as const
          ).map(([kind, can]) => (
            <button
              key={kind}
              type="button"
              disabled={refreshing || busy || !!pending || !reason.trim() || !can}
              onClick={() =>
                task(
                  e.identity.taskId,
                  kind,
                  e.revision,
                  tasks.options.find((o) => o.identity.taskId === e.identity.taskId)
                    ?.currentAttestations ?? [],
                )
              }
            >
              {kind}
            </button>
          ))}
          <details>
            <summary>Saved task provenance and history</summary>
            <ul>
              {e.history.map((h) => (
                <li key={h.eventId}>
                  {h.kind} · {h.actorId} · {h.reason} · {h.source.artifactSource.sourceDigest}
                </li>
              ))}
            </ul>
          </details>
        </article>
      ))}
      <Phase1BCsv runId={runId} csrfToken={csrfToken} />
    </section>
  );
}
type CsvInspection = {
  runId: string;
  snapshotDigest: string;
  rows: {
    taskId: string;
    findingId: string;
    taskStatus: string;
    planFreshness: string;
    taskLink: string;
    findingLink: string;
  }[];
};
const hex = /^[a-f0-9]{64}$/;
function validInspection(value: CsvInspection, runId: string) {
  return (
    value.runId === runId &&
    hex.test(value.snapshotDigest) &&
    Array.isArray(value.rows) &&
    value.rows.length <= 1000 &&
    value.rows.every(
      (r) =>
        hex.test(r.taskId) &&
        hex.test(r.findingId) &&
        ['Planned', 'InProgress', 'Completed', 'Cancelled'].includes(r.taskStatus) &&
        ['CurrentPlan', 'NeedsReconfirmation'].includes(r.planFreshness) &&
        r.taskLink === `http://localhost:5183/?run=${runId}&view=tasks&task=${r.taskId}` &&
        r.findingLink ===
          `http://localhost:5183/?run=${runId}&view=findings&finding=${r.findingId}`,
    ) &&
    new Set(value.rows.map((r) => r.taskId)).size === value.rows.length
  );
}
export function Phase1BCsv({ runId, csrfToken }: { runId: string; csrfToken: string }) {
  const [capture, setCapture] = useState<CsvInspection | null>(null),
    [accepted, setAccepted] = useState(false),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState('');
  const current = useRef(runId);
  current.current = runId;
  const generation = useRef(0);
  const active = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current?.abort();
    ++generation.current;
    setCapture(null);
    setAccepted(false);
    setBusy(false);
    setMessage('');
    return () => {
      active.current?.abort();
      ++generation.current;
    };
  }, [runId]);
  function begin() {
    active.current?.abort();
    const c = new AbortController();
    active.current = c;
    const epoch = ++generation.current;
    setBusy(true);
    return {
      c,
      epoch,
      valid: () =>
        !c.signal.aborted &&
        current.current === runId &&
        generation.current === epoch &&
        active.current === c,
    };
  }
  async function inspect() {
    const operation = begin();
    setCapture(null);
    setAccepted(false);
    setMessage('');
    try {
      const value = await request<CsvInspection>(`${prefix}/runs/${runId}/csv`, operation.c.signal);
      if (!validInspection(value, runId))
        throw new Error('The exact export snapshot and trusted links could not be verified.');
      if (operation.valid()) setCapture(value);
    } catch (e) {
      if (operation.valid()) setMessage(e instanceof Error ? e.message : 'Inspection unavailable.');
    } finally {
      if (operation.valid()) setBusy(false);
    }
  }
  async function download() {
    if (!capture || !accepted || busy) return;
    const selected = capture;
    const operation = begin();
    try {
      const response = await fetch(`/local-demo/v1${prefix}/runs/${runId}/csv`, {
        method: 'POST',
        credentials: 'same-origin',
        mode: 'same-origin',
        redirect: 'error',
        cache: 'no-store',
        signal: operation.c.signal,
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        body: JSON.stringify({
          requestId: crypto.randomUUID(),
          snapshotDigest: selected.snapshotDigest,
        }),
      });
      if (
        !response.ok ||
        response.headers.get('Content-Type') !== 'text/csv; charset=utf-8' ||
        !response.headers.get('Cache-Control')?.includes('no-store')
      )
        throw new Error(
          'The snapshot changed or export was denied. Inspect again before another download.',
        );
      const blob = await response.blob();
      if (!operation.valid()) return;
      if (blob.size > 4 * 1024 * 1024) throw new Error('Export exceeded the approved bound.');
      const url = URL.createObjectURL(blob);
      try {
        const link = document.createElement('a');
        link.href = url;
        link.download = 'synthetic-phase1b-tasks.csv';
        link.click();
      } finally {
        URL.revokeObjectURL(url);
      }
      setMessage(
        'CSV delivered. Handle your downloaded copy according to the stated export policy.',
      );
      setCapture(null);
      setAccepted(false);
    } catch (e) {
      if (operation.valid()) {
        setMessage(e instanceof Error ? e.message : 'Delivery unavailable.');
        setCapture(null);
        setAccepted(false);
      }
    } finally {
      if (operation.valid()) setBusy(false);
    }
  }
  return (
    <section aria-label="Task CSV export">
      <h2>Inspect and export task metadata</h2>
      <p>
        CSV contains protected fictional identifiers and freshly authorized localhost links. Your
        downloaded copy remains on your device; deletion, revocation and expiry cannot recall it. No
        reusable server download is retained.
      </p>
      <p role="status">{message}</p>
      <button type="button" disabled={busy} onClick={() => void inspect()}>
        Inspect current CSV snapshot
      </button>
      {capture && (
        <>
          <p>
            {capture.rows.length} task rows · Snapshot <code>{capture.snapshotDigest}</code>
          </p>
          <ul>
            {capture.rows.map((r) => (
              <li key={r.taskId}>
                {r.taskStatus} · {r.planFreshness} · <a href={r.taskLink}>Open authorized task</a> ·{' '}
                <a href={r.findingLink}>Open authorized finding</a>
              </li>
            ))}
          </ul>
          <label>
            <input
              type="checkbox"
              checked={accepted}
              onChange={(e) => setAccepted(e.target.checked)}
              disabled={busy}
            />
            I understand downloaded-copy handling and the protected-link warning.
          </label>
          <button type="button" disabled={busy || !accepted} onClick={() => void download()}>
            Download this CSV snapshot
          </button>
        </>
      )}
    </section>
  );
}
type Navigation = {
  runId: string;
  view: string;
  readOnly: boolean;
  task?: CsvInspection['rows'][number];
  findingId?: string;
};
export function Phase1BProtectedNavigation({
  runId,
  view,
  id,
}: {
  runId: string;
  view: string;
  id: string;
}) {
  const [value, setValue] = useState<Navigation | null>(null),
    [message, setMessage] = useState('Verifying the current link authority and saved source…'),
    [token, setToken] = useState('');
  const context = `${runId}/${view}/${id}`,
    current = useRef(context);
  current.current = context;
  useEffect(() => {
    const c = new AbortController();
    setValue(null);
    setToken('');
    setMessage('Verifying the current link authority and saved source…');
    void (async () => {
      try {
        const catalog = await request<{ csrfToken: string }>('/catalog', c.signal);
        const next = await request<Navigation>(
          `${prefix}/runs/${runId}/navigation?view=${encodeURIComponent(view)}&id=${encodeURIComponent(id)}`,
          c.signal,
        );
        if (
          next.runId !== runId ||
          next.view !== view ||
          typeof next.readOnly !== 'boolean' ||
          (view === 'tasks' &&
            (!next.task || next.task.taskId !== id || !hex.test(next.task.findingId))) ||
          (view === 'findings' && (next.findingId !== id || next.task !== undefined))
        )
          throw new Error('Unverifiable reference binding.');
        if (!c.signal.aborted && current.current === context) {
          setValue(next);
          setToken(catalog.csrfToken);
          setMessage('');
        }
      } catch {
        if (!c.signal.aborted && current.current === context) {
          setValue(null);
          setToken('');
          setMessage('This protected link is unavailable for the current authority or source.');
        }
      }
    })();
    return () => c.abort();
  }, [context, runId, view, id]);
  return (
    <main className="app phase1b-protected-reference">
      <h1>Protected fictional planning reference</h1>
      <p role={value ? 'status' : 'alert'}>{message}</p>
      {value && (
        <>
          <p>Current role: {value.readOnly ? 'Auditor · metadata only' : 'Consultant'}</p>
          {value.task ? (
            <p>
              Task <code>{value.task.taskId}</code> · {value.task.taskStatus} ·{' '}
              {value.task.planFreshness}
            </p>
          ) : (
            <p>
              Finding reference <code>{value.findingId}</code>
            </p>
          )}
          <p>
            Run <code>{runId}</code>
          </p>
          {!value.readOnly && <a href={`/?run=${runId}`}>Open combined assessment workspace</a>}
          <Phase1BCsv runId={runId} csrfToken={token} />
        </>
      )}
    </main>
  );
}
