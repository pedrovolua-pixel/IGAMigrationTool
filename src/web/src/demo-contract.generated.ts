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
