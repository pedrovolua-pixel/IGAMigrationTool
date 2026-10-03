import type {
  AnalysisDetail,
  RunDetail,
  ArtifactReviewDetail,
  ArtifactReviewSourceBinding,
  ArtifactReviewEntry,
  ArtifactReviewCommand,
  ArtifactReviewKind,
} from './demo-contract.generated';
import { coherentFixPackages } from './FixPackagePreview';
import './ArtifactReviewPanel.css';

const profile = 'synthetic-review-maturity-fix-review-equal-v1';
const contractDigest = 'a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f';
const templateDigest = 'a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669';
const limit = 100000;
const shape = (v: unknown, keys: readonly string[]) =>
  v !== null &&
  typeof v === 'object' &&
  !Array.isArray(v) &&
  Object.keys(v).length === keys.length &&
  keys.every((k) => Object.hasOwn(v, k));
const digest = (v: unknown): v is string =>
  typeof v === 'string' && v.length === 64 && /^[a-f0-9]{64}$/.test(v);
const revision = (v: unknown): v is number => Number.isSafeInteger(v) && (v as number) >= 0;
const uuid = (v: unknown): v is string =>
  typeof v === 'string' &&
  v.length === 36 &&
  /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(v) &&
  v !== '00000000-0000-0000-0000-000000000000';
const array = (v: unknown): v is readonly unknown[] => Array.isArray(v) && v.length <= limit;
function text(v: unknown, max = 16384): v is string {
  if (typeof v !== 'string' || v.length > max || /^\p{White_Space}*$/u.test(v)) return false;
  for (let i = 0; i < v.length; i++) {
    const c = v.charCodeAt(i);
    if (c >= 0xd800 && c <= 0xdbff) {
      const n = v.charCodeAt(++i);
      if (!(n >= 0xdc00 && n <= 0xdfff)) return false;
    } else if (c >= 0xdc00 && c <= 0xdfff) return false;
  }
  return true;
}
const same = (a: unknown, b: unknown): boolean => {
  if (a === b) return true;
  if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object') return false;
  const x = a as Record<string, unknown>,
    y = b as Record<string, unknown>;
  return (
    Array.isArray(a) === Array.isArray(b) &&
    Object.keys(x).length === Object.keys(y).length &&
    Object.keys(x).every((k) => Object.hasOwn(y, k) && same(x[k], y[k]))
  );
};
const ordered = <T,>(v: readonly T[], key: (x: T) => string) =>
  v.every((x, i) => i === 0 || key(v[i - 1]!) < key(x));
function source(s: ArtifactReviewSourceBinding): boolean {
  return (
    shape(s, [
      'scope',
      'runId',
      'runRevision',
      'runInputDigest',
      'baselineId',
      'profileId',
      'applicationVersion',
      'contractDigest',
      'sourceDigest',
      'guidanceDigest',
      'findingReviewDigest',
      'templateVersion',
      'templateDigest',
      'findingRevisions',
    ]) &&
    shape(s.scope, ['customerId', 'projectId', 'environmentId']) &&
    s.scope.customerId === 'synthetic-customer' &&
    s.scope.projectId === 'synthetic-project' &&
    s.scope.environmentId === 'synthetic-environment' &&
    uuid(s.runId) &&
    revision(s.runRevision) &&
    s.runRevision >= 1 &&
    digest(s.runInputDigest) &&
    text(s.baselineId) &&
    s.profileId === profile &&
    s.applicationVersion === 'synthetic-fix-review-app-v1' &&
    s.contractDigest === contractDigest &&
    digest(s.sourceDigest) &&
    digest(s.guidanceDigest) &&
    digest(s.findingReviewDigest) &&
    s.templateVersion === 'fictional-fix-templates-v1' &&
    s.templateDigest === templateDigest &&
    array(s.findingRevisions) &&
    ordered(s.findingRevisions, (x) => x.findingId) &&
    s.findingRevisions.every(
      (x) => shape(x, ['findingId', 'revision']) && digest(x.findingId) && revision(x.revision),
    )
  );
}
function compatible(
  old: ArtifactReviewSourceBinding,
  current: ArtifactReviewSourceBinding,
): boolean {
  const {
    runRevision: _oldRevision,
    sourceDigest: _oldDigest,
    guidanceDigest: _oldGuidance,
    findingReviewDigest: _oldReview,
    findingRevisions: _oldFindings,
    ...a
  } = old;
  const {
    runRevision: _revision,
    sourceDigest: _digest,
    guidanceDigest: _guidance,
    findingReviewDigest: _review,
    findingRevisions: _findings,
    ...b
  } = current;
  return (
    same(a, b) &&
    old.runRevision <= current.runRevision &&
    old.findingRevisions.length === current.findingRevisions.length &&
    old.findingRevisions.every(
      (f, i) =>
        f.findingId === current.findingRevisions[i]!.findingId &&
        f.revision <= current.findingRevisions[i]!.revision,
    ) &&
    (!(
      old.runRevision === current.runRevision &&
      same(old.findingRevisions, current.findingRevisions)
    ) ||
      (old.sourceDigest === current.sourceDigest &&
        old.guidanceDigest === current.guidanceDigest &&
        old.findingReviewDigest === current.findingReviewDigest))
  );
}
const stamp = (s: unknown): s is string =>
  typeof s === 'string' &&
  /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(s) &&
  Number.isFinite(Date.parse(s)) &&
  new Date(s).toISOString().slice(0, 19) === s.slice(0, 19);
function structural(analysis: AnalysisDetail, run: RunDetail): boolean {
  if (
    analysis.schemaVersion !== 1 ||
    analysis.demoOnly !== true ||
    run.schemaVersion !== 1 ||
    run.demoOnly !== true ||
    !uuid(run.runId) ||
    !revision(run.revision) ||
    analysis.runId !== run.runId ||
    analysis.runRevision !== run.revision
  )
    return false;
  const d = analysis.artifactReview;
  if (run.selection.profileId !== profile)
    return (
      [
        'profile-standard',
        'profile-comparison',
        'synthetic-analysis-equal-v1',
        'synthetic-analysis-operations-v1',
        'synthetic-review-maturity-equal-v1',
        'synthetic-review-maturity-operations-v1',
        'profile-ai-preview-v1',
        'profile-ai-preview-empty-v1',
        'synthetic-review-maturity-fix-packages-equal-v1',
      ].includes(run.selection.profileId) &&
      d === null &&
      !run.lockedInputs.some((l) => l.name === 'Artifact review contract')
    );
  const lock = run.lockedInputs.find((l) => l.name === 'Artifact review contract');
  if (
    run.lockedInputs.length !== 17 ||
    new Set(run.lockedInputs.map((l) => l.name)).size !== 17 ||
    !lock ||
    !shape(lock, ['name', 'version', 'sha256']) ||
    lock.version !== 'synthetic-fix-review-contract-v1' ||
    lock.sha256 !== contractDigest ||
    !d ||
    !shape(d, [
      'schemaVersion',
      'demoOnly',
      'status',
      'reasonCode',
      'source',
      'actorId',
      'artifacts',
    ]) ||
    d.schemaVersion !== 1 ||
    d.demoOnly !== true ||
    !array(d.artifacts)
  )
    return false;
  if (d.status === 'Unavailable')
    return (
      [
        'artifact_review_source_unavailable',
        'artifact_review_integrity_denied',
        'artifact_review_denied',
      ].includes(d.reasonCode!) &&
      d.source === null &&
      d.actorId === null &&
      d.artifacts.length === 0
    );
  if (
    d.status !== 'Ready' ||
    d.reasonCode !== null ||
    !text(d.actorId) ||
    !d.source ||
    !source(d.source) ||
    !ordered(d.artifacts, (x) => x.artifactId)
  )
    return false;
  const s = d.source,
    packageSnapshot = analysis.fixPackages?.snapshot,
    g = packageSnapshot?.guidance,
    review = analysis.review;
  if (
    analysis.fixPackages?.status !== 'Ready' ||
    !packageSnapshot ||
    !g ||
    !review ||
    review.status !== 'Ready' ||
    analysis.recommendationGuidance?.status !== 'Ready' ||
    !same(g, analysis.recommendationGuidance.snapshot) ||
    s.runId !== run.runId ||
    s.runRevision !== run.revision ||
    s.baselineId !== run.selection.baselineId ||
    s.runInputDigest !== g.source.runInputDigest ||
    s.runInputDigest !== run.lockedInputs.find((l) => l.name === 'Complete frozen input')?.sha256 ||
    s.sourceDigest !== packageSnapshot.contentDigest ||
    s.guidanceDigest !== g.contentDigest ||
    s.findingReviewDigest !== analysis.reviewSnapshotDigest ||
    s.findingReviewDigest !== review.snapshotDigest ||
    d.actorId !== review.actor ||
    s.templateVersion !== packageSnapshot.templateVersion ||
    s.templateDigest !== packageSnapshot.templateDigest ||
    !same(s.scope, g.source.scope) ||
    g.source.profileId !== profile ||
    g.source.frozenVersions.applicationVersion !== s.applicationVersion ||
    g.source.frozenVersions.fixReviewContractDigest !== contractDigest ||
    !same(
      s.findingRevisions,
      [...g.findings]
        .sort((a, b) => (a.findingId < b.findingId ? -1 : 1))
        .map((f) => ({ findingId: f.findingId, revision: f.findingRevision })),
    ) ||
    !same(
      s.findingRevisions,
      [...review.findings]
        .sort((a, b) => (a.id < b.id ? -1 : 1))
        .map((f) => ({ findingId: f.id, revision: f.revision })),
    )
  )
    return false;
  const members = packageSnapshot.packages.flatMap((p) =>
    p.options.flatMap((o) =>
      o.artifacts.map((a) => ({
        findingId: p.findingId,
        packageId: p.packageId,
        scopedOptionId: o.scopedOptionId,
        ...a,
      })),
    ),
  );
  if (members.length !== d.artifacts.length || members.length > limit) return false;
  const recordedSources = new Map<string, ArtifactReviewSourceBinding>([[s.sourceDigest, s]]);
  const byArtifact = new Map(members.map((member) => [member.artifactId, member]));
  for (const entry of d.artifacts) {
    if (
      !shape(entry, [
        'findingId',
        'categoryId',
        'packageId',
        'scopedOptionId',
        'artifactId',
        'templateId',
        'kind',
        'artifactTextDigest',
        'revision',
        'state',
        'canReview',
        'canWithdraw',
        'history',
      ]) ||
      ![
        entry.findingId,
        entry.packageId,
        entry.scopedOptionId,
        entry.artifactId,
        entry.artifactTextDigest,
      ].every(digest) ||
      !text(entry.categoryId) ||
      !text(entry.templateId) ||
      !['Configuration', 'Script', 'Sql'].includes(entry.kind) ||
      !revision(entry.revision) ||
      !Array.isArray(entry.history) ||
      entry.revision !== entry.history.length
    )
      return false;
    const member = byArtifact.get(entry.artifactId),
      finding = g.findings.find((f) => f.findingId === entry.findingId);
    if (
      !member ||
      !finding ||
      entry.categoryId !== finding.categoryId ||
      entry.findingId !== member.findingId ||
      entry.packageId !== member.packageId ||
      entry.scopedOptionId !== member.scopedOptionId ||
      entry.templateId !== member.templateId ||
      entry.kind !== member.kind
    )
      return false;
    const ids = new Set<string>();
    let previous: (typeof entry.history)[number] | undefined;
    for (const [index, e] of entry.history.entries()) {
      if (
        !shape(e, [
          'eventId',
          'revision',
          'kind',
          'actorId',
          'actorRoles',
          'recordedAtUtc',
          'reason',
          'source',
          'recordedState',
        ]) ||
        !uuid(e.eventId) ||
        ids.has(e.eventId) ||
        e.revision !== index + 1 ||
        !text(e.actorId) ||
        !same(e.actorRoles, ['Consultant']) ||
        !stamp(e.recordedAtUtc) ||
        !text(e.reason, 2000) ||
        !e.source ||
        !source(e.source) ||
        !compatible(e.source, s) ||
        (previous && !compatible(previous.source, e.source))
      )
        return false;
      const existingSource = recordedSources.get(e.source.sourceDigest);
      if (existingSource && !same(existingSource, e.source)) return false;
      recordedSources.set(e.source.sourceDigest, e.source);
      ids.add(e.eventId);
      if (e.kind === 'ReviewForPlanning') {
        if (
          e.recordedState !== 'ReviewedForPlanning' ||
          (previous?.kind === 'ReviewForPlanning' &&
            previous.source.sourceDigest === e.source.sourceDigest)
        )
          return false;
      } else if (e.kind === 'WithdrawReview') {
        if (
          e.recordedState !== 'Unverified' ||
          previous?.kind !== 'ReviewForPlanning' ||
          previous.source.sourceDigest !== e.source.sourceDigest
        )
          return false;
      } else return false;
      previous = e;
    }
    const state =
      !previous || previous.kind === 'WithdrawReview'
        ? 'Unverified'
        : previous.source.sourceDigest === s.sourceDigest
          ? 'ReviewedForPlanning'
          : 'NeedsReview';
    if (
      entry.state !== state ||
      entry.canReview !== (state !== 'ReviewedForPlanning') ||
      entry.canWithdraw !== (state === 'ReviewedForPlanning')
    )
      return false;
  }
  return true;
}
async function hash(s: string): Promise<string> {
  return Array.from(
    new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(s))),
    (b) => b.toString(16).padStart(2, '0'),
  ).join('');
}
export async function coherentArtifactReview(
  analysis: AnalysisDetail,
  run: RunDetail,
): Promise<boolean> {
  try {
    if (!structural(analysis, run)) return false;
    const before = JSON.stringify({ analysis, run });
    if (!(await coherentFixPackages(analysis, run))) return false;
    if (analysis.artifactReview?.status === 'Ready') {
      const artifacts = analysis.fixPackages!.snapshot!.packages.flatMap((p) =>
        p.options.flatMap((o) => o.artifacts),
      );
      if (
        !(
          await Promise.all(
            analysis.artifactReview.artifacts.map(
              async (e) =>
                e.artifactTextDigest ===
                (await hash(artifacts.find((a) => a.artifactId === e.artifactId)!.text)),
            ),
          )
        ).every(Boolean)
      )
        return false;
    }
    return before === JSON.stringify({ analysis, run }) && structural(analysis, run);
  } catch {
    return false;
  }
}
export type ArtifactReviewDraft = {
  readonly reason: string;
  readonly pending: ArtifactReviewCommand | null;
  readonly busy: boolean;
  readonly error: string | null;
  readonly requiresRefresh: boolean;
};
export type ArtifactReviewPanelProps = {
  analysis: AnalysisDetail;
  run: RunDetail;
  drafts: Readonly<Record<string, ArtifactReviewDraft | undefined>>;
  onReasonChange: (artifactId: string, reason: string) => void;
  onAction: (artifactId: string, kind: ArtifactReviewKind) => void;
  onRetry: (artifactId: string) => void;
  onRefresh: (artifactId: string) => void;
};
const label = (state: ArtifactReviewEntry['state']) =>
  state === 'ReviewedForPlanning'
    ? 'Reviewed for planning — fictional, review-only; correctness and remediation unverified'
    : state === 'NeedsReview'
      ? 'Needs review — source changed'
      : 'Unverified — no current planning attestation';
function Source({ value }: { value: ArtifactReviewSourceBinding }) {
  return (
    <dl className="artifact-review-fields">
      {Object.entries(value)
        .filter(([key]) => key !== 'scope' && key !== 'findingRevisions')
        .map(([key, v]) => (
          <div key={key}>
            <dt>{key}</dt>
            <dd>{v as string | number}</dd>
          </div>
        ))}
      {Object.entries(value.scope).map(([key, v]) => (
        <div key={key}>
          <dt>{key}</dt>
          <dd>{v}</dd>
        </div>
      ))}
      <div>
        <dt>Finding revisions</dt>
        <dd>
          <ul>
            {value.findingRevisions.map((f) => (
              <li key={f.findingId}>
                {f.findingId}: {f.revision}
              </li>
            ))}
          </ul>
        </dd>
      </div>
    </dl>
  );
}
export function ArtifactReviewPanel({
  analysis,
  run,
  drafts,
  onReasonChange,
  onAction,
  onRetry,
  onRefresh,
}: ArtifactReviewPanelProps) {
  let valid = false;
  try {
    valid = structural(analysis, run);
  } catch {
    /* Fail closed for malformed transport data. */
  }
  if (run.selection.profileId !== profile && valid) return null;
  if (!valid)
    return (
      <section aria-labelledby="artifact-review-heading" className="artifact-review">
        <h2 id="artifact-review-heading" tabIndex={-1}>
          Consultant artifact review
        </h2>
        <p role="alert">
          Artifact review data is inconsistent. Refresh the analysis before reviewing.
        </p>
      </section>
    );
  const review = analysis.artifactReview as ArtifactReviewDetail;
  return (
    <section aria-labelledby="artifact-review-heading" className="artifact-review">
      <h2 id="artifact-review-heading" tabIndex={-1}>
        Consultant artifact review
      </h2>
      <p>
        Local fictional planning attestation only. Correctness, supported remediation and execution
        safety remain unverified.
      </p>
      <p>
        Generated originals stay Unverified. Their historical unavailable sections describe the
        original generation layer; this separate overlay records current review history.
      </p>
      {review.status === 'Unavailable' ? (
        <p role="status">Artifact review unavailable: {review.reasonCode}</p>
      ) : (
        <>
          <p>Current reviewer: {review.actorId}</p>
          <details>
            <summary>Current review source</summary>
            <Source value={review.source!} />
          </details>
          {review.artifacts.length === 0 ? (
            <p>
              No artifacts are available to review. Empty results do not establish health or
              remediation.
            </p>
          ) : (
            review.artifacts.map((entry) => {
              const draft = drafts[entry.artifactId],
                busy = draft?.busy === true,
                pending = draft?.pending !== null && draft?.pending !== undefined,
                blocked = busy || pending || draft?.requiresRefresh === true;
              const reason = draft?.reason ?? '',
                reasonValid = text(reason, 2000),
                original = analysis
                  .fixPackages!.snapshot!.packages.flatMap((p) =>
                    p.options.flatMap((o) => o.artifacts),
                  )
                  .find((a) => a.artifactId === entry.artifactId)!;
              return (
                <article key={entry.artifactId}>
                  <h3 id={'artifact-review-' + entry.artifactId} tabIndex={-1}>
                    {entry.kind} artifact · {entry.artifactId}
                  </h3>
                  <p role="status">
                    {blocked
                      ? 'Current attestation withheld while this command or refresh is unresolved.'
                      : label(entry.state)}
                  </p>
                  <p>Original generated status: Unverified</p>
                  <dl className="artifact-review-fields">
                    {Object.entries(entry)
                      .filter(([key]) => key !== 'history' && key !== 'state')
                      .map(([key, v]) => (
                        <div key={key}>
                          <dt>{key}</dt>
                          <dd>{typeof v === 'boolean' ? String(v) : (v as string | number)}</dd>
                        </div>
                      ))}
                  </dl>
                  <details>
                    <summary>Original fictional artifact text</summary>
                    <pre>
                      <code>{original.text}</code>
                    </pre>
                  </details>
                  <details>
                    <summary>Attributed review history ({entry.history.length})</summary>
                    {entry.history.length === 0 ? (
                      <p>No review events recorded.</p>
                    ) : (
                      <ol>
                        {entry.history.map((e) => (
                          <li key={e.eventId}>
                            <h4>
                              Revision {e.revision} · {e.kind}
                            </h4>
                            <p>Event: {e.eventId}</p>
                            <p>
                              Actor: {e.actorId} · Roles: {e.actorRoles.join(', ')}
                            </p>
                            <p>
                              Recorded: <time>{e.recordedAtUtc}</time>
                            </p>
                            <p>Recorded outcome: {e.recordedState}</p>
                            <p className="artifact-review-reason">Reason: {e.reason}</p>
                            <details>
                              <summary>
                                Recorded source · server-verified historical binding
                              </summary>
                              <Source value={e.source} />
                            </details>
                          </li>
                        ))}
                      </ol>
                    )}
                  </details>
                  {draft?.error && (
                    <p role="alert" tabIndex={-1} id={'artifact-review-error-' + entry.artifactId}>
                      {draft.error}
                    </p>
                  )}
                  <label htmlFor={'artifact-review-reason-' + entry.artifactId}>
                    Reason for {entry.kind} artifact {entry.artifactId} (required, up to 2000
                    characters)
                  </label>
                  <textarea
                    id={'artifact-review-reason-' + entry.artifactId}
                    value={reason}
                    maxLength={2000}
                    disabled={blocked}
                    onChange={(e) => onReasonChange(entry.artifactId, e.target.value)}
                  />
                  {busy ? (
                    <p role="status">Saving review command…</p>
                  ) : pending ? (
                    <>
                      <p>
                        Outcome uncertain. Retry sends the exact original command; it does not
                        rebind to the current source.
                      </p>
                      <button type="button" onClick={() => onRetry(entry.artifactId)}>
                        Retry same artifact command
                      </button>
                      <button type="button" onClick={() => onRefresh(entry.artifactId)}>
                        Refresh artifact source
                      </button>
                    </>
                  ) : draft?.requiresRefresh ? (
                    <button type="button" onClick={() => onRefresh(entry.artifactId)}>
                      Refresh artifact source
                    </button>
                  ) : (
                    <button
                      type="button"
                      disabled={!reasonValid}
                      onClick={() =>
                        onAction(
                          entry.artifactId,
                          entry.canWithdraw ? 'WithdrawReview' : 'ReviewForPlanning',
                        )
                      }
                    >
                      {entry.canWithdraw ? 'Withdraw planning review' : 'Review for planning'}
                    </button>
                  )}
                </article>
              );
            })
          )}
        </>
      )}
    </section>
  );
}
