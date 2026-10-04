import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';
import type {
  AnalysisDetail,
  ArtifactReviewCommand,
  ArtifactReviewKind,
  ArtifactReviewResult,
  RunDetail,
} from './demo-contract.generated';
import type { ArtifactReviewDraft } from './ArtifactReviewPanel';
import { DemoRequestError, request } from './api';

const emptyDraft = (): ArtifactReviewDraft => ({
  reason: '',
  pending: null,
  busy: false,
  error: null,
  requiresRefresh: false,
});
function closed(value: unknown, keys: string[]): value is Record<string, unknown> {
  return (
    value !== null &&
    typeof value === 'object' &&
    !Array.isArray(value) &&
    Object.keys(value).length === keys.length &&
    keys.every((key) => Object.hasOwn(value, key))
  );
}
function utcTimestamp(value: unknown): boolean {
  if (
    typeof value !== 'string' ||
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(value)
  )
    return false;
  const milliseconds = Date.parse(value);
  if (!Number.isFinite(milliseconds)) return false;
  const normalized = new Date(milliseconds).toISOString();
  const expected = value.replace(
    /(?:\.(\d{1,7}))?Z$/,
    (_, fraction: string | undefined) => `.${(fraction ?? '').padEnd(3, '0').slice(0, 3)}Z`,
  );
  return normalized === expected;
}
function receiptAgrees(
  value: unknown,
  runId: string,
  artifactId: string,
  actorId: string,
  command: ArtifactReviewCommand,
): value is ArtifactReviewResult {
  if (
    !closed(value, ['schemaVersion', 'demoOnly', 'issue', 'alreadyApplied', 'receipt']) ||
    value.schemaVersion !== 1 ||
    value.demoOnly !== true ||
    value.issue !== null ||
    typeof value.alreadyApplied !== 'boolean' ||
    !closed(value.receipt, [
      'schemaVersion',
      'eventId',
      'runId',
      'artifactId',
      'kind',
      'revision',
      'actorId',
      'recordedAtUtc',
      'sourceDigest',
    ])
  )
    return false;
  const receipt = value.receipt;
  return (
    receipt.schemaVersion === 'synthetic-fix-review-receipt-v1' &&
    receipt.eventId === command.eventId &&
    receipt.runId === runId &&
    receipt.artifactId === artifactId &&
    receipt.kind === command.kind &&
    receipt.revision === command.expectedRevision + 1 &&
    Number.isSafeInteger(receipt.revision) &&
    receipt.actorId === actorId &&
    receipt.sourceDigest === command.expectedSourceDigest &&
    utcTimestamp(receipt.recordedAtUtc)
  );
}

/** Parent-owned drafts survive analysis child unmounts. A receipt always causes a whole-source reread. */
export function useArtifactReview(
  run: RunDetail,
  csrfToken: string,
  analysis: AnalysisDetail | null,
  readEpoch: RefObject<number>,
  reload: (message: string, artifactId: string) => void,
  retainedDrafts: RefObject<Record<string, ArtifactReviewDraft>>,
) {
  const [saved, setSaved] = useState<Record<string, ArtifactReviewDraft>>(
    () => retainedDrafts.current,
  );
  const savedRef = useRef(saved);
  savedRef.current = saved;
  retainedDrafts.current = saved;
  const currentRun = useRef(run.runId);
  currentRun.current = run.runId;
  const operations = useRef(new Map<string, { controller: AbortController; epoch: number }>());
  const generation = useRef(0);
  const key = (artifactId: string) => `${run.runId}/${artifactId}`;
  const change = (draftKey: string, patch: Partial<ArtifactReviewDraft>) =>
    setSaved((values) => ({
      ...values,
      [draftKey]: { ...(values[draftKey] ?? emptyDraft()), ...patch },
    }));
  useEffect(() => {
    ++generation.current;
    for (const operation of operations.current.values()) operation.controller.abort();
    operations.current.clear();
    setSaved((values) =>
      Object.fromEntries(
        Object.entries(values).map(([draftKey, draft]) => [
          draftKey,
          draft.busy
            ? {
                ...draft,
                busy: false,
                error:
                  'The response is uncertain. Refresh the source or explicitly retry the same event.',
                requiresRefresh: true,
              }
            : draft,
        ]),
      ),
    );
    return () => {
      retainedDrafts.current = Object.fromEntries(
        Object.entries(savedRef.current).map(([draftKey, draft]) => [
          draftKey,
          draft.busy
            ? {
                ...draft,
                busy: false,
                error:
                  'The response is uncertain. Refresh the source or explicitly retry the same event.',
                requiresRefresh: true,
              }
            : draft,
        ]),
      );
      ++generation.current;
      for (const operation of operations.current.values()) operation.controller.abort();
      operations.current.clear();
    };
  }, [run.runId, run.revision]);

  async function send(artifactId: string, command: ArtifactReviewCommand) {
    const selectedRun = run.runId,
      draftKey = key(artifactId),
      actorId = analysis?.artifactReview?.actorId;
    if (!actorId || operations.current.has(draftKey)) return;
    const controller = new AbortController(),
      epoch = generation.current,
      sourceEpoch = readEpoch.current;
    operations.current.set(draftKey, { controller, epoch });
    change(draftKey, { pending: command, busy: true, error: null });
    const current = () =>
      !controller.signal.aborted &&
      currentRun.current === selectedRun &&
      generation.current === epoch;
    try {
      const result: unknown = await request<ArtifactReviewResult>(
        `/runs/${encodeURIComponent(selectedRun)}/artifacts/${encodeURIComponent(artifactId)}/review`,
        controller.signal,
        command,
        csrfToken,
      );
      if (!current()) return;
      if (
        readEpoch.current !== sourceEpoch ||
        !receiptAgrees(result, selectedRun, artifactId, actorId, command)
      )
        throw new Error('Unverifiable response');
      change(draftKey, { pending: null, busy: false, error: null, requiresRefresh: false });
      reload(
        result.alreadyApplied
          ? 'The original historical receipt was returned. Current source and history were refreshed.'
          : 'Artifact review event saved. Current source and history were refreshed.',
        artifactId,
      );
    } catch (caught) {
      if (!current()) return;
      if (caught instanceof DemoRequestError && caught.status < 500) {
        change(draftKey, {
          pending: null,
          busy: false,
          requiresRefresh: true,
          error: `${caught.message} Inspect and refresh the source before a new action.`,
        });
      } else {
        change(draftKey, {
          pending: command,
          busy: false,
          requiresRefresh: false,
          error:
            'The response was lost or could not be verified. The event may already be saved. Explicitly retry the same event or refresh the source.',
        });
      }
      reload(
        'The artifact response could not be applied. Saved source and history were reread.',
        artifactId,
      );
    } finally {
      if (operations.current.get(draftKey)?.controller === controller)
        operations.current.delete(draftKey);
    }
  }
  return {
    drafts: Object.fromEntries(
      Object.entries(saved)
        .filter(([draftKey]) => draftKey.startsWith(`${run.runId}/`))
        .map(([draftKey, draft]) => [draftKey.slice(run.runId.length + 1), draft]),
    ),
    onReasonChange: (artifactId: string, reason: string) => {
      const draft = saved[key(artifactId)];
      if (!draft?.busy && !draft?.pending) change(key(artifactId), { reason });
    },
    onAction: (artifactId: string, kind: ArtifactReviewKind) => {
      const detail = analysis?.artifactReview,
        entry = detail?.artifacts.find((item) => item.artifactId === artifactId),
        draft = saved[key(artifactId)] ?? emptyDraft();
      if (
        !detail?.source ||
        !entry ||
        draft.busy ||
        draft.pending ||
        draft.requiresRefresh ||
        (kind === 'ReviewForPlanning' ? !entry.canReview : !entry.canWithdraw)
      )
        return;
      if (draft.reason.length > 2000 || /^\p{White_Space}*$/u.test(draft.reason)) {
        change(key(artifactId), { error: 'Enter a nonblank reason of at most 2,000 characters.' });
        return;
      }
      const command: ArtifactReviewCommand = Object.freeze({
        eventId: crypto.randomUUID(),
        kind,
        expectedRevision: entry.revision,
        expectedSourceDigest: detail.source.sourceDigest,
        reason: draft.reason,
      });
      void send(artifactId, command);
    },
    onRetry: (artifactId: string) => {
      const draft = saved[key(artifactId)];
      if (draft?.pending && !draft.busy) void send(artifactId, draft.pending);
    },
    onRefresh: (artifactId: string) => {
      if (saved[key(artifactId)]?.busy) return;
      change(key(artifactId), { requiresRefresh: false });
      reload('Inspect the refreshed source before selecting another artifact action.', artifactId);
    },
  };
}
