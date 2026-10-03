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
  readonly originalTitle: string;
  readonly initialState: string;
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
  readonly review: ReviewDetail | null;
  readonly maturity: MaturityDetail | null;
  readonly reviewSnapshotDigest: string | null;
  readonly reportDraft: ReportDraft | null;
  readonly recommendationGuidance: RecommendationGuidance | null;
  readonly aiPreview: AiPreviewDetail | null;
  readonly fixPackages: FixPackageDetail | null;
};

export type ReviewKind = "Confirm" | "Reject" | "Defer" | "Comment" | "EditPresentation";

export type ReviewCommand = {
  readonly eventId: string;
  readonly expectedRevision: number;
  readonly kind: ReviewKind;
  readonly reason: string | null;
  readonly text: string | null;
  readonly title: string | null;
  readonly businessContext: string | null;
};

export type ReviewActions = {
  readonly confirm: boolean;
  readonly reject: boolean;
  readonly defer: boolean;
  readonly comment: boolean;
  readonly editPresentation: boolean;
};

export type ReviewEvent = {
  readonly eventId: string;
  readonly actorId: string;
  readonly actorRoles: ReadonlyArray<string>;
  readonly kind: ReviewKind;
  readonly recordedAtUtc: string;
  readonly revision: number;
  readonly state: string;
  readonly reason: string | null;
  readonly text: string | null;
  readonly title: string | null;
  readonly businessContext: string | null;
};

export type ReviewFinding = {
  readonly id: string;
  readonly revision: number;
  readonly category: string;
  readonly state: string;
  readonly initialState: string;
  readonly originalTitle: string;
  readonly title: string;
  readonly businessContext: string;
  readonly originalDigests: ReadonlyArray<string>;
  readonly occurrenceIds: ReadonlyArray<string>;
  readonly actions: ReviewActions;
  readonly history: ReadonlyArray<ReviewEvent>;
};

export type ReviewDetail = {
  readonly schemaVersion: 1;
  readonly demoOnly: true;
  readonly runId: string;
  readonly runRevision: number;
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly snapshotDigest: string | null;
  readonly actor: string | null;
  readonly findings: ReadonlyArray<ReviewFinding>;
};

export type MaturityGate = {
  readonly level: string;
  readonly metDomains: number;
  readonly mandatoryDomains: number;
  readonly requiredPercent: number;
  readonly isMet: boolean;
};

export type MaturityIndicator = {
  readonly kind: string;
  readonly state: string;
  readonly reasonCode: string | null;
  readonly evidenceReferences: ReadonlyArray<string>;
  readonly assessmentReferences: ReadonlyArray<string>;
  readonly hasValidatedImprovementEvidence: boolean;
};

export type MaturityDomain = {
  readonly id: string;
  readonly name: string;
  readonly baseMet: boolean;
  readonly operationAndReviewMet: boolean;
  readonly improvementMet: boolean;
  readonly insufficientIndicators: number;
  readonly indicators: ReadonlyArray<MaturityIndicator>;
};

export type MaturityOwnership = {
  readonly state: string;
  readonly ownerId: string | null;
  readonly evidenceReferences: ReadonlyArray<string>;
  readonly reasonCode: string | null;
};

export type MaturityDetail = {
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly level: "Initial" | "Developing" | "Defined" | "Managed" | "Optimized" | null;
  readonly algorithmVersion: string | null;
  readonly catalogVersion: string | null;
  readonly inputDigest: string | null;
  readonly contentDigest: string | null;
  readonly authorityBoundary: string | null;
  readonly mandatoryDomains: number;
  readonly insufficientIndicators: number;
  readonly insufficientDomains: number;
  readonly improvementMissingDistinctAssessments: number;
  readonly governanceOwnershipEvidenced: boolean;
  readonly gates: ReadonlyArray<MaturityGate>;
  readonly domains: ReadonlyArray<MaturityDomain>;
  readonly ownership: MaturityOwnership | null;
};

export type DraftScope = {
  readonly customerId: string;
  readonly projectId: string;
  readonly environmentId: string;
};

export type DraftFrozenVersions = {
  readonly profileVersion: string;
  readonly scoringAlgorithmVersion: string;
  readonly aiPolicyVersion: string;
  readonly promptVersion: string;
  readonly modelVersion: string;
  readonly applicationVersion: string;
  readonly workSchemaVersion: string;
  readonly scriptedResultsDigest: string;
  readonly analysisFixtureDigest: string;
  readonly maturityFixtureDigest: string;
  readonly desiredOutcomeVersion: string | null;
  readonly fixPackageTemplateDigest?: "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
};

export type DraftModuleVersion = {
  readonly id: string;
  readonly version: string;
};

export type DraftCapabilityLock = {
  readonly matrixVersion: string;
  readonly stateAtLock: string;
  readonly productBuild: string;
  readonly databaseSchemaBuild: string;
  readonly hotfixSetDigest: string;
  readonly sqlServerBuild: string;
  readonly queryPackVersion: string;
  readonly normalizationSchemaVersion: string;
  readonly ruleCatalogVersion: string;
  readonly lockDigest: string;
  readonly compatibilityLevel: number;
  readonly modules: ReadonlyArray<DraftModuleVersion>;
};

export type DraftCompatibility = {
  readonly sourceProduct: string;
  readonly productVersion: string;
  readonly evidenceSchemaVersion: string;
  readonly ruleLanguageVersion: string;
};

export type DraftAnalysisLock = {
  readonly packVersion: string;
  readonly packDigest: string;
  readonly presetId: string;
  readonly presetVersion: string;
  readonly evidenceDigest: string;
  readonly catalogVersion: string;
  readonly catalogDigest: string;
  readonly profileId: string;
  readonly profileVersion: string;
  readonly profileDigest: string;
  readonly scope: DraftScope;
  readonly compatibility: DraftCompatibility;
};

export type DraftSourceBinding = {
  readonly runInputDigest: string;
  readonly analysisFixtureDigest: string;
  readonly analysisContentDigest: string;
  readonly scoringContentDigest: string;
  readonly savedCoverageDigest: string;
  readonly reviewSnapshotDigest: string;
  readonly maturityFixtureDigest: string;
  readonly maturityInputDigest: string;
  readonly maturityContentDigest: string;
  readonly scope: DraftScope;
  readonly runId: string;
  readonly runRevision: number;
  readonly runState: "Scoring";
  readonly baselineId: string;
  readonly profileId: string;
  readonly frozenVersions: DraftFrozenVersions;
  readonly capabilityLock: DraftCapabilityLock;
  readonly analysisLock: DraftAnalysisLock;
  readonly reviewRunId: string;
  readonly reviewRunRevision: number;
};

export type DraftFinding = {
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
  readonly originalTitle: string;
  readonly initialState: string;
  readonly revision: number;
  readonly businessContext: string;
  readonly occurrenceIds: ReadonlyArray<string>;
};

export type DraftReviewHistory = {
  readonly findingId: string;
  readonly revision: number;
  readonly originalTitle: string;
  readonly businessContext: string;
  readonly events: ReadonlyArray<ReviewEvent>;
};

export type DraftHealthyControl = {
  readonly objectId: string;
  readonly ruleId: string;
  readonly ruleVersion: string;
  readonly state: "Pass";
};

export type DraftLimitation = {
  readonly objectId: string;
  readonly ruleId: string;
  readonly state: CoverageState;
  readonly reasonCode: string;
};

export type DraftContent = {
  readonly provisional: AnalysisScore;
  readonly publishableCurrent: AnalysisScore;
  readonly categories: ReadonlyArray<AnalysisCategoryRow>;
  readonly objectTypes: ReadonlyArray<AnalysisScoreRow>;
  readonly modules: ReadonlyArray<AnalysisScoreRow>;
  readonly outcomes: ReadonlyArray<AnalysisScoreRow>;
  readonly quality: AnalysisQuality;
  readonly findings: ReadonlyArray<DraftFinding>;
  readonly warnings: ReadonlyArray<string>;
  readonly maturity: MaturityDetail;
  readonly reviewHistory: ReadonlyArray<DraftReviewHistory>;
  readonly healthyControls: ReadonlyArray<DraftHealthyControl>;
  readonly limitations: ReadonlyArray<DraftLimitation>;
  readonly methodology: ReadonlyArray<string>;
  readonly unavailableSections: ReadonlyArray<string>;
};

export type DraftSnapshot = {
  readonly schemaVersion: "synthetic-draft-report-v1";
  readonly status: "SyntheticDraft";
  readonly source: DraftSourceBinding;
  readonly canonicalContentDigest: string;
  readonly content: DraftContent;
};

export type DraftMarkdown = {
  readonly version: "synthetic-draft-markdown-v1";
  readonly canonicalContentDigest: string;
  readonly markdownText: string;
  readonly markdownSha256: string;
};

export type ReportDraft = {
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly snapshot: DraftSnapshot | null;
  readonly markdown: DraftMarkdown | null;
};

export type GuidanceSourceBinding = {
  readonly runInputDigest: string;
  readonly analysisFixtureDigest: string;
  readonly analysisContentDigest: string;
  readonly savedCoverageDigest: string;
  readonly reviewSnapshotDigest: string;
  readonly scope: DraftScope;
  readonly runId: string;
  readonly runRevision: number;
  readonly runState: "Scoring";
  readonly baselineId: string;
  readonly profileId: string;
  readonly frozenVersions: DraftFrozenVersions;
  readonly capabilityLock: DraftCapabilityLock;
  readonly analysisLock: DraftAnalysisLock;
  readonly reviewRunId: string;
  readonly reviewRunRevision: number;
};

export type GuidanceOccurrence = {
  readonly occurrenceId: string;
  readonly objectId: string;
  readonly objectType: string;
  readonly moduleId: string;
  readonly originalDigest: string;
  readonly evidenceReference: string;
};

export type GuidanceOption = {
  readonly scopedOptionId: string;
  readonly optionId: string;
  readonly status: "Unverified";
  readonly text: string;
  readonly prerequisites: string;
  readonly risk: string;
  readonly recoveryGuidance: string;
};

export type GuidanceFinding = {
  readonly findingId: string;
  readonly ruleId: string;
  readonly ruleVersion: string;
  readonly categoryId: string;
  readonly originalTitle: string;
  readonly presentationTitle: string;
  readonly businessContext: string;
  readonly rootCause: string;
  readonly severity: "Critical" | "High" | "Medium" | "Low" | "Informational";
  readonly initialState: "Proposed" | "AutoConfirmed";
  readonly currentState: "Proposed" | "AutoConfirmed" | "Confirmed" | "Rejected" | "Deferred";
  readonly findingRevision: number;
  readonly occurrences: ReadonlyArray<GuidanceOccurrence>;
  readonly options: ReadonlyArray<GuidanceOption>;
  readonly validationGuidance: ReadonlyArray<string>;
  readonly guidanceReferences: ReadonlyArray<string>;
  readonly assumptions: ReadonlyArray<string>;
  readonly limitations: ReadonlyArray<string>;
};

export type GuidanceSnapshot = {
  readonly schemaVersion: "synthetic-recommendation-guidance-v1";
  readonly status: "SyntheticUnverified";
  readonly source: GuidanceSourceBinding;
  readonly contentDigest: string;
  readonly findings: ReadonlyArray<GuidanceFinding>;
  readonly warnings: ReadonlyArray<string>;
  readonly unavailableSections: ReadonlyArray<string>;
};

export type RecommendationGuidance = {
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly snapshot: GuidanceSnapshot | null;
};

export type AiPreviewSource = {
  readonly customerId: string;
  readonly projectId: string;
  readonly environmentId: string;
  readonly runId: string;
  readonly baselineDigest: string;
  readonly profileDigest: string;
  readonly normalizationVersion: string;
  readonly redactionVersion: string;
  readonly promptVersion: string;
};

export type AiPreviewStatement = {
  readonly text: string;
  readonly evidenceIds: ReadonlyArray<string>;
  readonly ruleIds: ReadonlyArray<string>;
};

export type AiPreviewProposal = {
  readonly proposalId: string;
  readonly facts: ReadonlyArray<AiPreviewStatement>;
  readonly inferences: ReadonlyArray<AiPreviewStatement>;
  readonly assumptions: ReadonlyArray<AiPreviewStatement>;
  readonly missingContext: ReadonlyArray<string>;
  readonly suggestions: ReadonlyArray<AiPreviewStatement>;
  readonly uncertainty: string;
  readonly conflictingEvidenceIds: ReadonlyArray<string>;
};

export type AiPreviewSnapshot = {
  readonly schemaVersion: "synthetic-ai-preview-v1";
  readonly status: "Proposed";
  readonly disclaimer: "Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.";
  readonly canonicalJson: string;
  readonly contentDigest: string;
  readonly source: AiPreviewSource;
  readonly packetDigest: string;
  readonly proposalDigest: string;
  readonly proposals: ReadonlyArray<AiPreviewProposal>;
};

export type AiPreviewDetail = {
  readonly schemaVersion: "synthetic-ai-demo-preview-v1";
  readonly runId: string;
  readonly runRevision: number;
  readonly runInputDigest: string;
  readonly baselineId: string;
  readonly profileId: string;
  readonly fixtureDigest: string;
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly snapshot: AiPreviewSnapshot | null;
};

export type FixTemplate = {
  readonly templateId: string;
  readonly kind: "Configuration" | "Script" | "Sql";
  readonly text: string;
};

export type FixArtifact = {
  readonly artifactId: string;
  readonly templateId: string;
  readonly kind: "Configuration" | "Script" | "Sql";
  readonly status: "Unverified";
  readonly text: string;
};

export type FixOption = {
  readonly scopedOptionId: string;
  readonly artifacts: ReadonlyArray<FixArtifact>;
};

export type FixPackage = {
  readonly packageId: string;
  readonly findingId: string;
  readonly options: ReadonlyArray<FixOption>;
};

export type FixPackageSnapshot = {
  readonly schemaVersion: "synthetic-fix-package-preview-v1";
  readonly status: "Unverified";
  readonly disclaimer: "Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.";
  readonly guidance: GuidanceSnapshot;
  readonly templateVersion: "fictional-fix-templates-v1";
  readonly templateDigest: "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
  readonly templates: ReadonlyArray<FixTemplate>;
  readonly packages: ReadonlyArray<FixPackage>;
  readonly warnings: ReadonlyArray<string>;
  readonly unavailableSections: ReadonlyArray<string>;
  readonly canonicalJson: string;
  readonly contentDigest: string;
};

export type FixPackageDetail = {
  readonly schemaVersion: "synthetic-fix-package-demo-v1";
  readonly runId: string;
  readonly runRevision: number;
  readonly runInputDigest: string;
  readonly baselineId: string;
  readonly profileId: "synthetic-review-maturity-fix-packages-equal-v1";
  readonly status: "Ready" | "Unavailable";
  readonly reasonCode: string | null;
  readonly snapshot: FixPackageSnapshot | null;
};
