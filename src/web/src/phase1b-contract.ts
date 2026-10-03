import type { AnalysisDetail, AnalysisFinding, FixPackageDetail } from './demo-contract.generated';

// Additive opt-in DTO. Historical deterministic DTOs and generated envelopes remain unchanged.
export type Phase1BAnalysisDetail = Omit<AnalysisDetail, 'findings' | 'fixPackages'> & {
  readonly findings: ReadonlyArray<
    Omit<AnalysisFinding, 'method'> & { readonly method: 'Deterministic' | 'AI' }
  >;
  readonly fixPackages:
    | (Omit<FixPackageDetail, 'profileId'> & {
        readonly profileId: 'synthetic-phase1b-combined-v1';
      })
    | null;
};
export function verifyPhase1BAnalysis(
  value: Phase1BAnalysisDetail,
  runId: string,
  revision: number,
): void {
  const hex = (s: unknown) => typeof s === 'string' && /^[a-f0-9]{64}$/.test(s);
  const strings = (a: unknown) =>
    Array.isArray(a) &&
    a.length <= 256 &&
    a.every((s) => typeof s === 'string' && s.length <= 16384);
  if (
    value.schemaVersion !== 1 ||
    value.demoOnly !== true ||
    value.runId !== runId ||
    value.runRevision !== revision ||
    value.status !== 'Ready' ||
    value.algorithmVersion !== 'pilot-health-v1' ||
    !hex(value.contentDigest) ||
    !hex(value.fixtureDigest) ||
    !hex(value.reviewSnapshotDigest) ||
    value.review?.snapshotDigest !== value.reviewSnapshotDigest ||
    !value.quality ||
    value.quality.plannedUnits !== 12 ||
    ![value.quality.executedUnits, value.quality.gapUnits].every(
      (n) => Number.isSafeInteger(n) && n >= 0 && n <= 12,
    ) ||
    value.quality.executedUnits + value.quality.gapUnits !== 12 ||
    value.reportDraft !== null ||
    value.aiPreview !== null ||
    !Array.isArray(value.findings) ||
    value.findings.length > 12 ||
    new Set(value.findings.map((f) => f.id)).size !== value.findings.length ||
    value.findings.some(
      (f) =>
        !hex(f.id) ||
        !['Deterministic', 'AI'].includes(f.method) ||
        !['Critical', 'High', 'Medium', 'Low', 'Informational'].includes(f.severity) ||
        !['AutoConfirmed', 'Proposed', 'Confirmed', 'Rejected', 'Deferred'].includes(f.state) ||
        !strings(f.facts) ||
        !strings(f.inferences) ||
        !strings(f.assumptions) ||
        !strings(f.limitations) ||
        !strings(f.evidenceReferences) ||
        !strings(f.originalDigests) ||
        !f.originalDigests.every(hex) ||
        !strings(f.objectIds) ||
        (f.method === 'AI' &&
          (f.confidencePercent !== '80' ||
            f.initialState !== 'Proposed' ||
            f.state === 'AutoConfirmed' ||
            (f.state === 'Proposed' && !f.reviewRequired))),
    ) ||
    value.recommendationGuidance?.snapshot?.source.runId !== runId ||
    value.recommendationGuidance.snapshot.source.profileId !== 'synthetic-phase1b-combined-v1' ||
    value.fixPackages?.profileId !== 'synthetic-phase1b-combined-v1' ||
    value.fixPackages.snapshot?.status !== 'Unverified'
  )
    throw new Error(
      'The combined findings, coverage and original provenance could not be verified.',
    );
}
