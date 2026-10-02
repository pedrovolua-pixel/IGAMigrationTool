// Generated from contracts/local-demo/demo-v1.schema.json. Do not edit.
// Private synthetic demo only; does not establish production HTTP approval.

export type RunState = "Planned" | "Running" | "Scoring" | "Cancelled" | "Failed";

export type CompletionKind = "Complete" | "CompleteWithGaps";

export type CoverageState = "Pass" | "Finding" | "NotApplicable" | "NotAssessed" | "InsufficientEvidence" | "Excluded" | "Inaccessible" | "Redacted" | "Unsupported" | "Error";

export type ScopeOption = {
  readonly id: string;
  readonly label: string;
};

export type Warning = {
  readonly code: string;
  readonly message: string;
};

export type BaselineOption = {
  readonly id: string;
  readonly label: string;
  readonly scopeId: string;
  readonly version: string;
  readonly warnings: ReadonlyArray<Warning>;
};

export type ProfileOption = {
  readonly id: string;
  readonly label: string;
  readonly version: string;
  readonly baselineIds: ReadonlyArray<string>;
};

export type Catalog = {
  readonly schemaVersion: 1;
  readonly demoOnly: true;
  readonly scopes: ReadonlyArray<ScopeOption>;
  readonly baselines: ReadonlyArray<BaselineOption>;
  readonly profiles: ReadonlyArray<ProfileOption>;
  readonly csrfToken: string;
};

export type StartRunRequest = {
  readonly scopeId: string;
  readonly baselineId: string;
  readonly profileId: string;
  readonly requestId: string;
};

export type RunMutationRequest = {
  readonly expectedRevision: number;
  readonly requestId: string;
};

export type Selection = {
  readonly scopeId: string;
  readonly scopeLabel: string;
  readonly baselineId: string;
  readonly baselineLabel: string;
  readonly profileId: string;
  readonly profileLabel: string;
};

export type Progress = {
  readonly plannedUnits: number;
  readonly terminalUnits: number;
  readonly remainingUnits: number;
  readonly allTerminal: boolean;
};

export type LockedInput = {
  readonly name: string;
  readonly version: string;
  readonly sha256: string;
};

export type StateCount = {
  readonly state: CoverageState;
  readonly count: number;
};

export type Limitation = {
  readonly state: CoverageState;
  readonly reasonCode: string;
  readonly responsibleStage: string;
  readonly count: number;
};

export type ExecutableCoverage = {
  readonly numerator: number;
  readonly denominator: number;
  readonly hasApplicableUnits: boolean;
};

export type RunActions = {
  readonly canCancel: boolean;
  readonly canResume: boolean;
  readonly resumeAvailableAtUtc: string | null;
  readonly reasonCode: string | null;
};

export type RunSummary = {
  readonly runId: string;
  readonly revision: number;
  readonly state: RunState;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  readonly selection: Selection;
  readonly progress: Progress;
  readonly coverageCompletionKind: CompletionKind | null;
  readonly cancelRequested: boolean;
};

export type RunDetail = {
  readonly schemaVersion: 1;
  readonly demoOnly: true;
  readonly runId: string;
  readonly revision: number;
  readonly state: RunState;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  readonly selection: Selection;
  readonly progress: Progress;
  readonly coverageCompletionKind: CompletionKind | null;
  readonly lockedInputs: ReadonlyArray<LockedInput>;
  readonly warnings: ReadonlyArray<Warning>;
  readonly stateCounts: ReadonlyArray<StateCount>;
  readonly executableCoverage: ExecutableCoverage | null;
  readonly limitations: ReadonlyArray<Limitation>;
  readonly actions: RunActions;
  readonly cancelRequested: boolean;
};

export type RunHistory = {
  readonly schemaVersion: 1;
  readonly demoOnly: true;
  readonly runs: ReadonlyArray<RunSummary>;
};

export type DemoError = {
  readonly schemaVersion: 1;
  readonly code: string;
  readonly message: string;
  readonly correlationId: string | null;
  readonly currentRevision: number | null;
};

export type AnalysisScore = {
  readonly raw: string | null;
  readonly display: string | null;
  readonly status: "Unavailable" | "Red" | "Yellow" | "Green";
  readonly eligibleUnits: number;
};

export type AnalysisScoreRow = {
  readonly id: string;
  readonly provisional: AnalysisScore;
  readonly publishableCurrent: AnalysisScore;
};

export type AnalysisCategoryRow = {
  readonly id: string;
  readonly provisional: AnalysisScore;
  readonly publishableCurrent: AnalysisScore;
  readonly provisionalWeight: string | null;
  readonly publishableWeight: string | null;
};

export type AnalysisFinding = {
  readonly id: string;
  readonly title: string;
  readonly category: string;
  readonly severity: "Critical" | "High" | "Medium" | "Low" | "Informational";
  readonly confidencePercent: string;
  readonly state: string;
  readonly reviewRequired: boolean;
  readonly ruleId: string;
  readonly ruleVersion: string;
  readonly baselineId: string;
  readonly rootCauseKey: string;
  readonly objectIds: ReadonlyArray<string>;
  readonly evidenceReferences: ReadonlyArray<string>;
  readonly facts: ReadonlyArray<string>;
  readonly inferences: ReadonlyArray<string>;
  readonly assumptions: ReadonlyArray<string>;
  readonly impact: string;
  readonly recommendations: ReadonlyArray<string>;
  readonly validationGuidance: string;
  readonly sources: ReadonlyArray<string>;
  readonly confidenceBand: string;
  readonly method: "Deterministic";
  readonly likelihood: string;
  readonly limitations: ReadonlyArray<string>;
  readonly originalDigests: ReadonlyArray<string>;
  readonly outcomeIds: ReadonlyArray<string>;
  readonly rootCause: string;
};

export type AnalysisQuality = {
  readonly plannedUnits: number;
  readonly executedUnits: number;
  readonly gapUnits: number;
  readonly notApplicableUnits: number;
  readonly proposedReviewUnits: number;
  readonly totalFindingUnits: number;
};

export type AnalysisDetail = {
  readonly schemaVersion: 1;
  readonly demoOnly: true;
  readonly runId: string;
  readonly runRevision: number;
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly algorithmVersion: string | null;
  readonly fixtureDigest: string | null;
  readonly contentDigest: string | null;
  readonly provisional: AnalysisScore | null;
  readonly publishableCurrent: AnalysisScore | null;
  readonly categories: ReadonlyArray<AnalysisCategoryRow>;
  readonly objectTypes: ReadonlyArray<AnalysisScoreRow>;
  readonly modules: ReadonlyArray<AnalysisScoreRow>;
  readonly outcomes: ReadonlyArray<AnalysisScoreRow>;
  readonly quality: AnalysisQuality | null;
  readonly findings: ReadonlyArray<AnalysisFinding>;
  readonly warnings: ReadonlyArray<string>;
};
