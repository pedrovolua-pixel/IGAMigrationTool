import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';
import type { ArtifactReviewDraft } from './ArtifactReviewPanel';
import type {
  AnalysisDetail,
  AnalysisScore,
  AnalysisScoreRow,
  RunDetail,
} from './demo-contract.generated';
import { request } from './api';
import { ReviewPanel } from './ReviewPanel';
import { MaturityView } from './MaturityView';
import { DraftReportView } from './DraftReportView';
import { RecommendationGuidanceView } from './RecommendationGuidanceView';
import { AiProposalPreview, coherentAiPreview } from './AiProposalPreview';
import { FixPackagePreview, coherentFixPackages } from './FixPackagePreview';
import { ArtifactReviewPanel, coherentArtifactReview } from './ArtifactReviewPanel';
import { useArtifactReview } from './useArtifactReview';

export function AnalysisView({
  run,
  csrfToken,
  artifactDrafts,
}: {
  run: RunDetail;
  csrfToken: string;
  artifactDrafts: RefObject<Record<string, ArtifactReviewDraft>>;
}) {
  const [response, setResponse] = useState<AnalysisDetail | null>(null);
  const [failed, setFailed] = useState(false);
  const [retry, setRetry] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const retryFocus = useRef<string | null>(null);
  const readEpoch = useRef(0);
  const artifactFocus = useRef<string | null>(null);
  const artifactControls = useArtifactReview(
    run,
    csrfToken,
    response,
    readEpoch,
    (message, artifactId) => {
      ++readEpoch.current;
      setNotice(message);
      artifactFocus.current = artifactId;
      setResponse(null);
      setRetry((value) => value + 1);
    },
    artifactDrafts,
  );
  useEffect(() => {
    retryFocus.current = null;
    artifactFocus.current = null;
    setNotice(null);
  }, [run.runId]);
  useEffect(() => {
    const controller = new AbortController();
    const epoch = ++readEpoch.current;
    setResponse(null);
    setFailed(false);
    if (run.state === 'Scoring') {
      void request<AnalysisDetail>(
        `/runs/${encodeURIComponent(run.runId)}/analysis`,
        controller.signal,
      )
        .then(async (value) => {
          if (
            !controller.signal.aborted &&
            epoch === readEpoch.current &&
            value.runId === run.runId &&
            value.runRevision === run.revision &&
            (!value.review ||
              (value.review.runId === value.runId &&
                value.review.runRevision === value.runRevision &&
                value.review.snapshotDigest === value.reviewSnapshotDigest)) &&
            coherentDraft(value, run) &&
            (await coherentGuidance(value, run)) &&
            (await coherentAiPreview(value, run)) &&
            (await coherentFixPackages(value, run)) &&
            (await coherentArtifactReview(value, run)) &&
            epoch === readEpoch.current &&
            !controller.signal.aborted
          )
            setResponse(value);
          else if (!controller.signal.aborted && epoch === readEpoch.current) {
            retryFocus.current = null;
            setFailed(true);
          }
        })
        .catch(() => {
          if (!controller.signal.aborted && epoch === readEpoch.current) {
            retryFocus.current = null;
            setFailed(true);
          }
        });
    }
    return () => controller.abort();
  }, [run.runId, run.revision, run.state, retry]);
  useEffect(() => {
    if (response && artifactFocus.current) {
      const target = document.getElementById(`artifact-review-${artifactFocus.current}`);
      const error = document.getElementById(`artifact-review-error-${artifactFocus.current}`);
      (error ?? target ?? document.getElementById('artifact-review-heading'))?.focus();
      artifactFocus.current = null;
    }
    if (response && retryFocus.current) {
      if (response.runId === retryFocus.current) {
        if (response.status === 'Ready') heading.current?.focus();
        else if (response.aiPreview)
          document.getElementById('ai-proposal-preview-heading')?.focus();
        else if (response.fixPackages)
          document.getElementById('fix-package-preview-heading')?.focus();
      }
      retryFocus.current = null;
    }
  }, [response]);
  if (run.state !== 'Scoring') return null;
  if (!response || response.runId !== run.runId || response.runRevision !== run.revision) {
    return (
      <div className="field-note" role="status">
        <p>
          {failed
            ? 'Analysis could not be verified. Saved run inputs and results are unchanged.'
            : 'Reading analysis from the saved run…'}
        </p>
        {failed && (
          <button
            onClick={() => {
              retryFocus.current = run.runId;
              ++readEpoch.current;
              setResponse(null);
              setRetry((value) => value + 1);
            }}
          >
            Retry analysis
          </button>
        )}
      </div>
    );
  }
  if (response.status !== 'Ready') {
    return (
      <>
        <p className="field-note">
          {response.reasonCode === 'coverage_only_fixture'
            ? 'This saved fixture demonstrates coverage only; health scoring remains unavailable.'
            : 'Analysis is unavailable for this saved run. No health result is inferred.'}
        </p>
        <AiProposalPreview preview={response.aiPreview} run={run} />
        <FixPackagePreview preview={response.fixPackages} run={run} analysis={response} />
        <ArtifactReviewPanel analysis={response} run={run} {...artifactControls} />
      </>
    );
  }
  return (
    <section className="analysis-view subsection" aria-labelledby="analysis-heading">
      <p className="eyebrow">Deterministic analysis · synthetic fixtures</p>
      <h3 id="analysis-heading" tabIndex={-1} ref={heading}>
        Findings and reproducible health calculations
      </h3>
      {notice && (
        <p role="status" className="field-note">
          {notice}
        </p>
      )}
      <p className="field-note">
        These are local calculations from fixed synthetic evidence. The publishable-current
        calculation is not a published report. Review and maturity, when available, use frozen
        fictional fixtures; live assessment remains pending.
      </p>
      <div className="analysis-score-grid">
        <Score label="Provisional health" score={response.provisional!} />
        <Score label="Publishable-current health" score={response.publishableCurrent!} />
      </div>
      {!!response.warnings.length && (
        <div className="warning-note" role="note">
          <h4>Review and interpretation warnings</h4>
          <ul>
            {response.warnings.map((warning, index) => (
              <li key={index}>{warning}</li>
            ))}
          </ul>
        </div>
      )}
      <p className="field-note">
        Pass earns its full rule weight. Findings reduce health using the saved severity policy.
        Proposed Critical/High findings affect only provisional health until reviewed. Gaps reduce
        coverage, not default health. Categories without eligible units are not assessed; remaining
        category weights are renormalized separately for each calculation.
      </p>
      <div
        className="table-scroll"
        tabIndex={0}
        role="region"
        aria-label="Category health calculations"
      >
        <table>
          <caption>Category health and effective weights</caption>
          <thead>
            <tr>
              <th scope="col">Category</th>
              <th scope="col">Provisional</th>
              <th scope="col">Publishable-current</th>
              <th scope="col">Effective weights</th>
            </tr>
          </thead>
          <tbody>
            {response.categories.map((item) => (
              <tr key={item.id}>
                <th scope="row">{item.id}</th>
                <td>{scoreText(item.provisional)}</td>
                <td>{scoreText(item.publishableCurrent)}</td>
                <td>
                  {item.provisionalWeight ?? 'Not assessed'} /{' '}
                  {item.publishableWeight ?? 'Not assessed'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <details className="locked-inputs">
        <summary>Health by module, object type and approved fixture outcome</summary>
        <Dimension label="Installed modules" rows={response.modules} />
        <Dimension label="Object types" rows={response.objectTypes} />
        <Dimension label="Approved synthetic outcome fixtures" rows={response.outcomes} />
        <p className="field-note">
          An outcome fixture flag demonstrates the approved scoring boundary; it does not record
          customer approval. The same unit may appear in these views without being duplicated in
          overall health.
        </p>
      </details>
      {response.quality && (
        <section className="subsection">
          <h4>Separate assessment quality</h4>
          <dl className="count-list">
            <div>
              <dt>Executed applicable units</dt>
              <dd>{response.quality.executedUnits}</dd>
            </div>
            <div>
              <dt>Explained gaps</dt>
              <dd>{response.quality.gapUnits}</dd>
            </div>
            <div>
              <dt>Not applicable</dt>
              <dd>{response.quality.notApplicableUnits}</dd>
            </div>
            <div>
              <dt>Finding occurrences</dt>
              <dd>{response.quality.totalFindingUnits}</dd>
            </div>
            <div>
              <dt>Occurrences awaiting mandatory review</dt>
              <dd>{response.quality.proposedReviewUnits}</dd>
            </div>
          </dl>
          <p className="field-note">
            Quality is not blended into the health score. Gap reasons and executable coverage remain
            visible above.
          </p>
        </section>
      )}
      <section className="subsection">
        <h4>Generated findings</h4>
        {!response.findings.length ? (
          <p>
            No findings were generated by the executed fixture rules. Check coverage and limitations
            before interpreting this result.
          </p>
        ) : (
          response.findings.map((finding) => (
            <article
              className={`analysis-finding severity-${finding.severity.toLowerCase()}`}
              key={finding.id}
            >
              <h4>{finding.title}</h4>
              <p>
                <strong>{finding.severity}</strong> · Deterministic · Confidence{' '}
                {finding.confidencePercent}% · {finding.confidenceBand} · {finding.state}
                {finding.reviewRequired ? ' · Mandatory review pending' : ''}
              </p>
              <p className="field-note">
                {finding.category} · {finding.objectIds.length} affected synthetic objects ·{' '}
                {finding.ruleId} / {finding.ruleVersion}
              </p>
              <details>
                <summary>Read generated original and provenance</summary>
                <p>
                  <strong>Original title</strong>: {finding.originalTitle} · Initial state:{' '}
                  {finding.initialState}
                </p>
                <p>
                  <strong>Observed facts</strong>
                </p>
                <ul>
                  {finding.facts.map((fact, index) => (
                    <li key={index}>{fact}</li>
                  ))}
                </ul>
                <p>
                  <strong>Inference</strong>:{' '}
                  {finding.inferences.join('; ') || 'None supplied by this deterministic fixture.'}
                </p>
                <p>
                  <strong>Assumptions</strong>: {finding.assumptions.join('; ') || 'None supplied.'}
                </p>
                <p>
                  <strong>Impact</strong>: {finding.impact}
                </p>
                <p>
                  <strong>Root cause</strong>: {finding.rootCause}
                </p>
                <p>
                  <strong>Root-cause key</strong>: {finding.rootCauseKey}
                </p>
                <p>
                  <strong>Affected objects</strong>: {finding.objectIds.join(', ')}
                </p>
                <p>
                  <strong>Evidence baseline</strong>: {finding.baselineId}
                </p>
                <p>
                  <strong>Evidence references</strong>: {finding.evidenceReferences.join(', ')}
                </p>
                <p>
                  <strong>Recommendation options</strong>
                </p>
                <ul>
                  {finding.recommendations.map((item, index) => (
                    <li key={index}>{item}</li>
                  ))}
                </ul>
                <p>
                  <strong>Validation guidance</strong>: {finding.validationGuidance}
                </p>
                <p>
                  <strong>Fixture rule sources</strong>: {finding.sources.join('; ')}
                </p>
                <p>
                  <strong>Likelihood</strong>: {finding.likelihood}
                </p>
                <p>
                  <strong>Limitations</strong>: {finding.limitations.join('; ')}
                </p>
                <p>
                  <strong>Approved outcome links</strong>:{' '}
                  {finding.outcomeIds.join(', ') ||
                    'None; no customer outcomes are approved by this demo.'}
                </p>
                <p>
                  <strong>Generated original digests</strong>: {finding.originalDigests.join(', ')}
                </p>

                <p className="field-note">
                  The generated original is read-only. Available synthetic review actions appear
                  below. Risk acceptance and closure remain unavailable.
                </p>
              </details>
            </article>
          ))
        )}
      </section>
      {response.review && (
        <ReviewPanel
          review={response.review}
          csrfToken={csrfToken}
          onReload={(message) => {
            ++readEpoch.current;
            setNotice(message ?? 'Saved analysis refreshed.');
            retryFocus.current = run.runId;
            setResponse(null);
            setRetry((value) => value + 1);
          }}
        />
      )}
      <DraftReportView key={response.runId} report={response.reportDraft} />
      <RecommendationGuidanceView
        key={`guidance-${response.runId}`}
        guidance={response.recommendationGuidance}
      />
      <AiProposalPreview preview={response.aiPreview} run={run} />
      <FixPackagePreview preview={response.fixPackages} run={run} analysis={response} />
      <ArtifactReviewPanel analysis={response} run={run} {...artifactControls} />
      {response.maturity && <MaturityView maturity={response.maturity} />}
      <details className="locked-inputs">
        <summary>Analysis versions and content digests</summary>
        <dl>
          <div>
            <dt>Health algorithm</dt>
            <dd>{response.algorithmVersion}</dd>
          </div>
          <div>
            <dt>Frozen fixture contents</dt>
            <dd>
              <code>{response.fixtureDigest}</code>
            </dd>
          </div>
          <div>
            <dt>Reproducible analysis result</dt>
            <dd>
              <code>{response.contentDigest}</code>
            </dd>
          </div>
          {response.reviewSnapshotDigest && (
            <div>
              <dt>Reviewed snapshot</dt>
              <dd>
                <code>{response.reviewSnapshotDigest}</code>
              </dd>
            </div>
          )}
        </dl>
        <p className="field-note">
          Exact saved input locks and canonical coverage results determine this read-only analysis.
          Later fixture changes are refused rather than substituted.
        </p>
      </details>
    </section>
  );
}

function coherentDraft(value: AnalysisDetail, run: RunDetail): boolean {
  const draft = value.reportDraft;
  if (!draft || draft.status !== 'Ready') return true;
  const snapshot = draft.snapshot;
  const markdown = draft.markdown;
  const maturity = value.maturity;
  if (!snapshot || !markdown || !maturity || maturity.status !== 'Ready') return false;
  const source = snapshot.source;
  const versions = source.frozenVersions;
  const locked = (name: string) => run.lockedInputs.find((input) => input.name === name);
  const versionBindings: ReadonlyArray<readonly [string, string]> = [
    ['Profile', versions.profileVersion],
    ['Desired outcomes', versions.desiredOutcomeVersion ?? 'disabled'],
    ['Scoring algorithm', versions.scoringAlgorithmVersion],
    ['AI policy', versions.aiPolicyVersion],
    ['Prompt', versions.promptVersion],
    ['Model', versions.modelVersion],
    ['Application', versions.applicationVersion],
    ['Work schema', versions.workSchemaVersion],
    ['Rule catalog', source.analysisLock.catalogVersion],
    ['Capability', source.capabilityLock.matrixVersion],
  ];
  return (
    snapshot.schemaVersion === 'synthetic-draft-report-v1' &&
    snapshot.status === 'SyntheticDraft' &&
    markdown.version === 'synthetic-draft-markdown-v1' &&
    snapshot.canonicalContentDigest === markdown.canonicalContentDigest &&
    source.runId === value.runId &&
    source.runRevision === value.runRevision &&
    source.runState === 'Scoring' &&
    source.baselineId === run.selection.baselineId &&
    source.profileId === run.selection.profileId &&
    source.runInputDigest === locked('Complete frozen input')?.sha256 &&
    source.capabilityLock.lockDigest === locked('Exact capability tuple')?.sha256 &&
    versions.scriptedResultsDigest === locked('Scripted result fixture')?.sha256 &&
    versionBindings.every(([name, version]) => locked(name)?.version === version) &&
    source.reviewRunId === value.runId &&
    source.reviewRunRevision === value.runRevision &&
    source.reviewSnapshotDigest === value.reviewSnapshotDigest &&
    source.analysisFixtureDigest === value.fixtureDigest &&
    source.analysisFixtureDigest === locked('Frozen analysis contents')?.sha256 &&
    source.frozenVersions.scoringAlgorithmVersion === value.algorithmVersion &&
    source.scoringContentDigest === value.contentDigest &&
    source.maturityInputDigest === maturity.inputDigest &&
    source.maturityContentDigest === maturity.contentDigest &&
    source.scope.customerId === 'synthetic-customer' &&
    source.scope.projectId === 'synthetic-project' &&
    source.scope.environmentId === 'synthetic-environment' &&
    snapshot.content.maturity.inputDigest === maturity.inputDigest &&
    snapshot.content.maturity.contentDigest === maturity.contentDigest
  );
}
async function coherentGuidance(value: AnalysisDetail, run: RunDetail): Promise<boolean> {
  const guidance = value.recommendationGuidance;
  if (guidance === undefined) return false;
  if (guidance === null) return true;
  if (guidance.status === 'Unavailable') return guidance.snapshot === null;
  if (guidance.status !== 'Ready' || guidance.reasonCode !== null) return false;
  const snapshot = guidance.snapshot;
  const review = value.review;
  if (!snapshot || !review || review.status !== 'Ready') return false;
  const source = snapshot.source;
  const versions = source.frozenVersions;
  const locked = (name: string) => run.lockedInputs.find((input) => input.name === name);
  const versionBindings: ReadonlyArray<readonly [string, string]> = [
    ['Profile', versions.profileVersion],
    ['Desired outcomes', versions.desiredOutcomeVersion ?? 'disabled'],
    ['Scoring algorithm', versions.scoringAlgorithmVersion],
    ['AI policy', versions.aiPolicyVersion],
    ['Prompt', versions.promptVersion],
    ['Model', versions.modelVersion],
    ['Application', versions.applicationVersion],
    ['Work schema', versions.workSchemaVersion],
    ['Rule catalog', source.analysisLock.catalogVersion],
    ['Capability', source.capabilityLock.matrixVersion],
  ];
  const sameSet = (a: ReadonlyArray<string>, b: ReadonlyArray<string>) => {
    const sortedB = [...b].sort();
    return a.length === b.length && [...a].sort().every((item, index) => item === sortedB[index]);
  };
  const digest = (text: string) => /^[a-f0-9]{64}$/.test(text);
  const draftSource =
    value.reportDraft?.status === 'Ready' ? value.reportDraft.snapshot?.source : null;
  if (
    snapshot.schemaVersion !== 'synthetic-recommendation-guidance-v1' ||
    snapshot.status !== 'SyntheticUnverified' ||
    !digest(snapshot.contentDigest) ||
    source.runId !== run.runId ||
    source.runId !== value.runId ||
    source.runRevision !== run.revision ||
    source.runRevision !== value.runRevision ||
    source.runState !== 'Scoring' ||
    source.baselineId !== run.selection.baselineId ||
    source.profileId !== run.selection.profileId ||
    source.runInputDigest !== locked('Complete frozen input')?.sha256 ||
    source.capabilityLock.lockDigest !== locked('Exact capability tuple')?.sha256 ||
    versions.scriptedResultsDigest !== locked('Scripted result fixture')?.sha256 ||
    !versionBindings.every(([name, version]) => locked(name)?.version === version) ||
    source.analysisFixtureDigest !== value.fixtureDigest ||
    source.analysisFixtureDigest !== locked('Frozen analysis contents')?.sha256 ||
    source.frozenVersions.scoringAlgorithmVersion !== value.algorithmVersion ||
    source.scope.customerId !== 'synthetic-customer' ||
    source.scope.projectId !== 'synthetic-project' ||
    source.scope.environmentId !== 'synthetic-environment' ||
    source.reviewRunId !== value.runId ||
    source.reviewRunRevision !== value.runRevision ||
    source.reviewSnapshotDigest !== value.reviewSnapshotDigest ||
    source.reviewSnapshotDigest !== review.snapshotDigest ||
    !digest(source.analysisContentDigest) ||
    !digest(source.savedCoverageDigest) ||
    (draftSource &&
      (source.analysisContentDigest !== draftSource.analysisContentDigest ||
        source.savedCoverageDigest !== draftSource.savedCoverageDigest ||
        JSON.stringify(source.analysisLock) !== JSON.stringify(draftSource.analysisLock) ||
        JSON.stringify(source.capabilityLock) !== JSON.stringify(draftSource.capabilityLock) ||
        JSON.stringify(source.frozenVersions) !== JSON.stringify(draftSource.frozenVersions))) ||
    snapshot.findings.length !== value.findings.length ||
    snapshot.findings.length !== review.findings.length ||
    new Set(snapshot.findings.map((finding) => finding.findingId)).size !== snapshot.findings.length
  )
    return false;
  const results = await Promise.all(
    snapshot.findings.map(async (finding) => {
      const original = value.findings.find((item) => item.id === finding.findingId);
      const current = review.findings.find((item) => item.id === finding.findingId);
      if (!original || !current) return false;
      const optionIds = finding.options.map((option) => option.optionId);
      if (!sameSet(optionIds, ['inspect-fixture', 'compare-new-fixture'])) return false;
      const scopedIds = await Promise.all(
        finding.options.map((option) =>
          fixtureIdentityDigest({
            findingId: finding.findingId,
            optionId: option.optionId,
            runId: source.runId,
            scope: {
              customerId: source.scope.customerId,
              environmentId: source.scope.environmentId,
              projectId: source.scope.projectId,
            },
          }),
        ),
      );
      const occurrenceIds = await Promise.all(
        finding.occurrences.map((occurrence) =>
          fixtureIdentityDigest({
            EvidenceDigest: source.analysisLock.evidenceDigest,
            Id: finding.ruleId,
            ObjectId: occurrence.objectId,
            PresetId: source.analysisLock.presetId,
            Scope: {
              CustomerId: source.scope.customerId,
              EnvironmentId: source.scope.environmentId,
              ProjectId: source.scope.projectId,
            },
            Version: finding.ruleVersion,
            runId: source.runId,
          }),
        ),
      );
      const rowsMatch = finding.occurrences.every((occurrence, index) => {
        const originalIndex = original.objectIds.indexOf(occurrence.objectId);
        return (
          originalIndex >= 0 &&
          occurrence.occurrenceId === occurrenceIds[index] &&
          occurrence.originalDigest === original.originalDigests[originalIndex] &&
          occurrence.evidenceReference === original.evidenceReferences[originalIndex] &&
          occurrence.objectType === 'SyntheticControl' &&
          occurrence.moduleId ===
            (finding.categoryId === 'SECURITY' ? 'SyntheticSecurity' : 'SyntheticOperations')
        );
      });
      return (
        finding.ruleId === original.ruleId &&
        finding.ruleVersion === original.ruleVersion &&
        finding.categoryId === original.category &&
        finding.categoryId === current.category &&
        finding.severity === original.severity &&
        finding.originalTitle === original.originalTitle &&
        finding.originalTitle === current.originalTitle &&
        finding.presentationTitle === original.title &&
        finding.presentationTitle === current.title &&
        finding.businessContext === current.businessContext &&
        finding.initialState === original.initialState &&
        finding.initialState === current.initialState &&
        finding.currentState === original.state &&
        finding.currentState === current.state &&
        finding.findingRevision === current.revision &&
        finding.rootCause === original.rootCause &&
        sameSet(
          finding.occurrences.map((item) => item.occurrenceId),
          current.occurrenceIds,
        ) &&
        sameSet(
          finding.occurrences.map((item) => item.originalDigest),
          original.originalDigests,
        ) &&
        sameSet(
          finding.occurrences.map((item) => item.objectId),
          original.objectIds,
        ) &&
        sameSet(
          finding.occurrences.map((item) => item.evidenceReference),
          original.evidenceReferences,
        ) &&
        sameSet(finding.guidanceReferences, original.sources) &&
        finding.validationGuidance.join(' ') === original.validationGuidance &&
        JSON.stringify(finding.assumptions) === JSON.stringify(original.assumptions) &&
        JSON.stringify(finding.limitations) === JSON.stringify(original.limitations) &&
        finding.options.length === original.recommendations.length &&
        new Set(finding.options.map((option) => option.optionId)).size === finding.options.length &&
        new Set(finding.options.map((option) => option.scopedOptionId)).size ===
          finding.options.length &&
        rowsMatch &&
        finding.options.every(
          (option, index) =>
            option.status === 'Unverified' &&
            option.scopedOptionId === scopedIds[index] &&
            `${option.text} Prerequisites: ${option.prerequisites} Risk: ${option.risk} Recovery: ${option.recoveryGuidance}` ===
              original.recommendations[option.optionId === 'inspect-fixture' ? 0 : 1],
        ) &&
        sameSet(
          finding.options.map(
            (option) =>
              `${option.text} Prerequisites: ${option.prerequisites} Risk: ${option.risk} Recovery: ${option.recoveryGuidance}`,
          ),
          original.recommendations,
        )
      );
    }),
  );
  return results.every(Boolean);
}
// Exact already-frozen fictional identities contain only ASCII IDs. This read-consistency check grants no authority.
async function fixtureIdentityDigest(value: object): Promise<string> {
  const bytes = new TextEncoder().encode(JSON.stringify(value));
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
}
function scoreText(score: AnalysisScore) {
  return score.display === null ? 'Not assessed' : `${score.display} / 100 · ${score.status}`;
}
function Score({ label, score }: { label: string; score: AnalysisScore }) {
  return (
    <div className={`analysis-score score-${score.status.toLowerCase()}`}>
      <h4>{label}</h4>
      <strong>{score.display ?? 'Unavailable'}</strong>
      <p>
        {score.display === null
          ? 'No eligible units; not 100.'
          : `${score.status} · ${score.eligibleUnits} eligible units`}
      </p>
    </div>
  );
}
function Dimension({ label, rows }: { label: string; rows: ReadonlyArray<AnalysisScoreRow> }) {
  return (
    <section>
      <h4>{label}</h4>
      {rows.length ? (
        <dl className="count-list">
          {rows.map((row) => (
            <div key={row.id}>
              <dt>{row.id}</dt>
              <dd>
                {scoreText(row.provisional)} / {scoreText(row.publishableCurrent)}
              </dd>
            </div>
          ))}
        </dl>
      ) : (
        <p>Not assessed; no eligible fixture units.</p>
      )}
    </section>
  );
}
