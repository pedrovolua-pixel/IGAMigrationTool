import type {
  AiPreviewDetail,
  AiPreviewProposal,
  AiPreviewSnapshot,
  AiPreviewStatement,
  AnalysisDetail,
  RunDetail,
} from './demo-contract.generated';
import './AiProposalPreview.css';

const baselineId = 'baseline-ai-configuration-v1';
const profileId = 'profile-ai-preview-v1';
const emptyProfileId = 'profile-ai-preview-empty-v1';
const disclaimer =
  'Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.';
const maximumBytes = 4 * 1024 * 1024;
const digest = (value: unknown): value is string =>
  typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);
const uuid = (value: unknown): value is string =>
  typeof value === 'string' &&
  /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(value) &&
  value !== '00000000-0000-0000-0000-000000000000';
function shape(value: unknown, keys: ReadonlyArray<string>): value is Record<string, unknown> {
  return (
    value !== null &&
    typeof value === 'object' &&
    !Array.isArray(value) &&
    Object.keys(value).length === keys.length &&
    keys.every((key) => Object.hasOwn(value, key))
  );
}
function text(value: unknown, nonblank = false): value is string {
  if (
    typeof value !== 'string' ||
    value.length > 4096 ||
    (nonblank && /^\p{White_Space}*$/u.test(value))
  )
    return false;
  for (let index = 0; index < value.length; index++) {
    const unit = value.charCodeAt(index);
    if (unit >= 0xd800 && unit <= 0xdbff) {
      const next = value.charCodeAt(++index);
      if (!(next >= 0xdc00 && next <= 0xdfff)) return false;
    } else if (unit >= 0xdc00 && unit <= 0xdfff) return false;
  }
  return true;
}
function array(value: unknown, maximum: number): value is ReadonlyArray<unknown> {
  return Array.isArray(value) && value.length <= maximum;
}
function ids(value: unknown, evidence: boolean, minimum: number): value is ReadonlyArray<string> {
  return (
    array(value, evidence ? 16 : 2) &&
    value.length >= minimum &&
    value.every(
      (item, index) =>
        typeof item === 'string' &&
        (evidence
          ? /^ev-[a-f0-9]{64}$/.test(item)
          : item === 'fixture-rule-conflict-v1' || item === 'fixture-rule-schedule-v1') &&
        (index === 0 || (value[index - 1] as string) < item),
    )
  );
}
function statements(value: unknown): value is ReadonlyArray<AiPreviewStatement> {
  return (
    array(value, 16) &&
    value.every(
      (item) =>
        shape(item, ['text', 'evidenceIds', 'ruleIds']) &&
        text(item.text, true) &&
        ids(item.evidenceIds, true, 1) &&
        ids(item.ruleIds, false, 1),
    )
  );
}
function proposal(value: unknown): value is AiPreviewProposal {
  if (
    !shape(value, [
      'proposalId',
      'facts',
      'inferences',
      'assumptions',
      'missingContext',
      'suggestions',
      'uncertainty',
      'conflictingEvidenceIds',
    ]) ||
    typeof value.proposalId !== 'string' ||
    !/^proposal-[0-9]{2}$/.test(value.proposalId) ||
    !statements(value.facts) ||
    !statements(value.inferences) ||
    !statements(value.assumptions) ||
    !statements(value.suggestions) ||
    value.facts.length +
      value.inferences.length +
      value.assumptions.length +
      value.suggestions.length ===
      0 ||
    !array(value.missingContext, 16) ||
    !value.missingContext.every((item) => text(item, true)) ||
    !text(value.uncertainty) ||
    !ids(value.conflictingEvidenceIds, true, 0)
  )
    return false;
  return (
    value.conflictingEvidenceIds.length === 0 ||
    (value.conflictingEvidenceIds.length >= 2 &&
      !/^\p{White_Space}*$/u.test(value.uncertainty) &&
      value.missingContext.length > 0)
  );
}

// Complete default-escaped, ordinal JSON avoids accepting duplicate members or
// a noncanonical byte string after JSON.parse. Only the closed snapshot graph is serialized.
function canonical(value: unknown, depth = 0): string {
  if (depth > 16) throw new Error('Invalid preview depth');
  if (typeof value === 'string') {
    let result = '"';
    for (let index = 0; index < value.length; index++) {
      const unit = value.charCodeAt(index);
      const character = value[index]!;
      const simple: Record<string, string> = {
        '\\': '\\\\',
        '\b': '\\b',
        '\f': '\\f',
        '\n': '\\n',
        '\r': '\\r',
        '\t': '\\t',
      };
      if (Object.hasOwn(simple, character)) result += simple[character];
      else if (unit < 32 || unit >= 127 || '<>&\'"+`'.includes(character))
        result += `\\u${unit.toString(16).toUpperCase().padStart(4, '0')}`;
      else result += character;
    }
    return result + '"';
  }
  if (Array.isArray(value))
    return '[' + value.map((item) => canonical(item, depth + 1)).join(',') + ']';
  if (value !== null && typeof value === 'object') {
    const object = value as Record<string, unknown>;
    return (
      '{' +
      Object.keys(object)
        .sort()
        .map((key) => canonical(key, depth + 1) + ':' + canonical(object[key], depth + 1))
        .join(',') +
      '}'
    );
  }
  throw new Error('Invalid preview value');
}
function snapshotShape(
  value: unknown,
  run: RunDetail,
  templateDigest: string,
): value is AiPreviewSnapshot {
  if (
    !shape(value, [
      'schemaVersion',
      'status',
      'disclaimer',
      'canonicalJson',
      'contentDigest',
      'source',
      'packetDigest',
      'proposalDigest',
      'proposals',
    ]) ||
    value.schemaVersion !== 'synthetic-ai-preview-v1' ||
    value.status !== 'Proposed' ||
    value.disclaimer !== disclaimer ||
    typeof value.canonicalJson !== 'string' ||
    new TextEncoder().encode(value.canonicalJson).length > maximumBytes ||
    !digest(value.contentDigest) ||
    !digest(value.packetDigest) ||
    !digest(value.proposalDigest) ||
    !shape(value.source, [
      'customerId',
      'projectId',
      'environmentId',
      'runId',
      'baselineDigest',
      'profileDigest',
      'normalizationVersion',
      'redactionVersion',
      'promptVersion',
    ]) ||
    value.source.customerId !== 'synthetic-customer' ||
    value.source.projectId !== 'synthetic-project' ||
    value.source.environmentId !== 'synthetic-environment' ||
    value.source.runId !== run.runId ||
    value.source.baselineDigest !== templateDigest ||
    value.source.profileDigest !==
      run.lockedInputs.find((item) => item.name === 'Complete frozen input')?.sha256 ||
    value.source.normalizationVersion !== 'fixture-normalization-v1' ||
    value.source.redactionVersion !== 'fixture-redaction-v1' ||
    value.source.promptVersion !== 'fixture-prompt-v1' ||
    !array(value.proposals, 16) ||
    !value.proposals.every(proposal) ||
    !value.proposals.every(
      (item, index, items) =>
        index === 0 ||
        (items[index - 1] as AiPreviewProposal).proposalId < (item as AiPreviewProposal).proposalId,
    )
  )
    return false;
  const payload = {
    schemaVersion: value.schemaVersion,
    status: value.status,
    disclaimer: value.disclaimer,
    source: value.source,
    packetDigest: value.packetDigest,
    proposalDigest: value.proposalDigest,
    proposals: value.proposals,
  };
  const parsed: unknown = JSON.parse(value.canonicalJson);
  return canonical(parsed) === value.canonicalJson && canonical(payload) === value.canonicalJson;
}
function bound(preview: unknown, run: RunDetail): preview is AiPreviewDetail {
  if (
    !shape(preview, [
      'schemaVersion',
      'runId',
      'runRevision',
      'runInputDigest',
      'baselineId',
      'profileId',
      'fixtureDigest',
      'status',
      'reasonCode',
      'snapshot',
    ]) ||
    run.schemaVersion !== 1 ||
    run.demoOnly !== true ||
    !uuid(run.runId) ||
    !Number.isSafeInteger(run.revision) ||
    run.revision < 1 ||
    (run.selection.profileId !== profileId && run.selection.profileId !== emptyProfileId) ||
    run.selection.baselineId !== baselineId ||
    preview.schemaVersion !== 'synthetic-ai-demo-preview-v1' ||
    preview.runId !== run.runId ||
    preview.runRevision !== run.revision ||
    preview.baselineId !== baselineId ||
    preview.profileId !== run.selection.profileId ||
    !digest(preview.runInputDigest) ||
    !digest(preview.fixtureDigest) ||
    !Array.isArray(run.lockedInputs) ||
    new Set(run.lockedInputs.map((item) => item.name)).size !== run.lockedInputs.length ||
    !run.lockedInputs.every(
      (item) =>
        shape(item, ['name', 'version', 'sha256']) &&
        typeof item.name === 'string' &&
        typeof item.version === 'string' &&
        digest(item.sha256),
    )
  )
    return false;
  const lock = (name: string) => run.lockedInputs.find((item) => item.name === name);
  const versions: ReadonlyArray<readonly [string, string]> = [
    ['Baseline', 'baseline-ai-configuration-v1'],
    ['Rule catalog', 'synthetic-ai-fixture-rules-v1'],
    ['Capability', 'synthetic-ai-matrix-v1'],
    ['Desired outcomes', 'disabled'],
    ['Exact capability tuple', 'synthetic-ai-matrix-v1'],
    ['Scripted result fixture', 'synthetic-outcomes-v1'],
    [
      'Profile',
      run.selection.profileId === profileId
        ? 'synthetic-ai-preview-profile-v1'
        : 'synthetic-ai-preview-empty-profile-v1',
    ],
    ['AI policy', 'synthetic-ai-offline-preview-only-v1'],
    ['Prompt', 'fixture-prompt-v1'],
    ['Model', 'synthetic-fixed-response-v1'],
    ['Application', 'synthetic-ai-preview-demo-app-v1'],
    ['Scoring algorithm', 'synthetic-scoring-unimplemented-v1'],
    ['Work schema', 'synthetic-run-work-v1'],
    ['Complete frozen input', 'synthetic-input-lock-v1'],
    ['Frozen offline AI contents', 'synthetic-ai-demo-fixture-v1'],
    ['Offline AI configuration template', 'synthetic-ai-configuration-v1'],
  ];
  if (
    run.lockedInputs.length !== versions.length ||
    !versions.every(([name, version]) => lock(name)?.version === version) ||
    preview.runInputDigest !== lock('Complete frozen input')?.sha256 ||
    preview.fixtureDigest !== lock('Frozen offline AI contents')?.sha256
  )
    return false;
  if (preview.status === 'Unavailable')
    return preview.snapshot === null && text(preview.reasonCode, true);
  return (
    preview.status === 'Ready' &&
    preview.reasonCode === null &&
    run.state === 'Scoring' &&
    run.cancelRequested === false &&
    run.coverageCompletionKind === 'Complete' &&
    shape(run.progress, ['plannedUnits', 'terminalUnits', 'remainingUnits', 'allTerminal']) &&
    Number.isSafeInteger(run.progress.plannedUnits) &&
    Number.isSafeInteger(run.progress.terminalUnits) &&
    Number.isSafeInteger(run.progress.remainingUnits) &&
    run.progress.allTerminal === true &&
    run.progress.plannedUnits > 0 &&
    run.progress.terminalUnits === run.progress.plannedUnits &&
    run.progress.remainingUnits === 0 &&
    snapshotShape(preview.snapshot, run, lock('Offline AI configuration template')!.sha256) &&
    (run.selection.profileId !== emptyProfileId || preview.snapshot.proposals.length === 0)
  );
}

export async function coherentAiPreview(value: AnalysisDetail, run: RunDetail): Promise<boolean> {
  try {
    if (
      value.schemaVersion !== 1 ||
      value.demoOnly !== true ||
      value.runId !== run.runId ||
      value.runRevision !== run.revision
    )
      return false;
    const dedicated =
      run.selection.profileId === profileId || run.selection.profileId === emptyProfileId;
    if (!dedicated) return run.selection.baselineId !== baselineId && value.aiPreview === null;
    if (!bound(value.aiPreview, run)) return false;
    if (value.aiPreview.status === 'Unavailable') return true;
    const snapshot = value.aiPreview.snapshot!;
    const hash = await crypto.subtle.digest(
      'SHA-256',
      new TextEncoder().encode(snapshot.canonicalJson),
    );
    return (
      Array.from(new Uint8Array(hash), (byte) => byte.toString(16).padStart(2, '0')).join('') ===
      snapshot.contentDigest
    );
  } catch {
    return false;
  }
}

export function AiProposalPreview({
  preview,
  run,
}: {
  preview: AiPreviewDetail | null;
  run: RunDetail;
}) {
  if (preview === null) return null;
  let coherent = false;
  try {
    coherent = bound(preview, run);
  } catch {
    /* Malformed reads have no partial fallback. */
  }
  if (!coherent || preview.status !== 'Ready' || !preview.snapshot)
    return (
      <section
        className="ai-proposal-preview"
        aria-label="Offline simulated configuration response"
      >
        <h3 id="ai-proposal-preview-heading" tabIndex={-1}>
          Offline simulated configuration response
        </h3>
        <p role="status">
          Offline AI preview is unavailable for these saved inputs. No proposal or health result is
          inferred.
        </p>
      </section>
    );
  const snapshot = preview.snapshot;
  return (
    <section className="ai-proposal-preview" aria-label="Offline simulated configuration response">
      <p className="eyebrow">Fictional configuration · fixed offline response</p>
      <h3 id="ai-proposal-preview-heading" tabIndex={-1}>
        Offline simulated configuration response
      </h3>
      <div className="ai-preview-warning" role="note">
        <strong>Proposed · Untrusted</strong>
        <p>{snapshot.disclaimer}</p>
        <p>
          This response is separate from deterministic findings and health calculations. Its
          citations are identifiers only; no evidence or action is opened.
        </p>
      </div>
      {snapshot.proposals.length === 0 ? (
        <p className="ai-preview-empty" role="status">
          No proposals were returned. This does not establish healthy or complete assessment
          coverage.
        </p>
      ) : (
        snapshot.proposals.map((item) => <Proposal key={item.proposalId} proposal={item} />)
      )}
      <details className="ai-preview-source">
        <summary>Captured preview source and digests</summary>
        <p>Digests identify this fictional frozen value and grant no access.</p>
        <dl>
          {[
            ['Run ID', snapshot.source.runId],
            ['Run revision', String(preview.runRevision)],
            ['Baseline', preview.baselineId],
            ['Profile', preview.profileId],
            ['Complete frozen input digest', preview.runInputDigest],
            ['Complete offline fixture digest', preview.fixtureDigest],
            ['Customer', snapshot.source.customerId],
            ['Project', snapshot.source.projectId],
            ['Environment', snapshot.source.environmentId],
            ['Configuration template digest', snapshot.source.baselineDigest],
            ['Source profile digest', snapshot.source.profileDigest],
            ['Normalization version', snapshot.source.normalizationVersion],
            ['Redaction version', snapshot.source.redactionVersion],
            ['Prompt version', snapshot.source.promptVersion],
            ['Packet digest', snapshot.packetDigest],
            ['Proposal digest', snapshot.proposalDigest],
            ['Canonical preview content digest', snapshot.contentDigest],
            ['Preview schema', snapshot.schemaVersion],
            ['Preview status', snapshot.status],
          ].map(([label, value]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      </details>
    </section>
  );
}
function Proposal({ proposal }: { proposal: AiPreviewProposal }) {
  return (
    <article className="ai-preview-proposal">
      <h4>Proposal {proposal.proposalId}</h4>
      <Statements label="Facts" values={proposal.facts} />
      <Statements label="Inferences" values={proposal.inferences} />
      <Statements label="Assumptions" values={proposal.assumptions} />
      <Statements label="Suggestions" values={proposal.suggestions} />
      <section>
        <h5>Missing context</h5>
        <TextList values={proposal.missingContext} empty="No missing context was declared." />
      </section>
      <section>
        <h5>Uncertainty</h5>
        <p>{proposal.uncertainty || 'No uncertainty was declared.'}</p>
      </section>
      <section>
        <h5>Conflicting evidence IDs</h5>
        <TextList
          values={proposal.conflictingEvidenceIds}
          empty="No conflicting evidence was declared."
        />
      </section>
    </article>
  );
}
function Statements({
  label,
  values,
}: {
  label: string;
  values: ReadonlyArray<AiPreviewStatement>;
}) {
  return (
    <section>
      <h5>{label}</h5>
      {values.length === 0 ? (
        <p>No statements were supplied in this category.</p>
      ) : (
        <ol>
          {values.map((item, index) => (
            <li key={index}>
              <p>{item.text}</p>
              <h6>Evidence IDs</h6>
              <TextList values={item.evidenceIds} empty="No evidence IDs were supplied." />
              <h6>Rule IDs</h6>
              <TextList values={item.ruleIds} empty="No rule IDs were supplied." />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
function TextList({ values, empty }: { values: ReadonlyArray<string>; empty: string }) {
  return values.length === 0 ? (
    <p>{empty}</p>
  ) : (
    <ul>
      {values.map((value, index) => (
        <li key={index}>{value}</li>
      ))}
    </ul>
  );
}
