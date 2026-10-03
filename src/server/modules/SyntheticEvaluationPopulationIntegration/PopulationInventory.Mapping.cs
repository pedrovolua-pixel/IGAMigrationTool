using SyntheticEvaluation;

namespace SyntheticEvaluationPopulationIntegration;

internal static partial class PopulationInventory
{
    // Frozen engineering inventory, not runtime repository configuration.
    internal static readonly PopulationBindingDefinition[] Definitions =
    [
        new(SamplingVersionKind.SourceBuild, ["P.$.CapabilityLock.ProductBuild", "P.$.CapabilityLock.DatabaseSchemaBuild", "P.$.CapabilityLock.HotfixSetDigest", "P.$.CapabilityLock.SqlServerBuild", "P.$.CapabilityLock.CompatibilityLevel", "P.$.CapabilityLock.Modules[*].Id", "P.$.CapabilityLock.Modules[*].Version"], ["G.$.Provenance.Compatibility"], null),
        new(SamplingVersionKind.EnvironmentEvidence, ["C.$.scope.{customerId,projectId,environmentId}", "L.$.scope.{customerId,projectId,environmentId}", "I.$.source.{customerId,projectId,environmentId,runId,baselineDigest,profileDigest}", "I.$.evidence[*].{evidenceId,classification,redactionCount,configurationKey,configurationValue}", "O.$.evidenceIds[*]", "O.$.packetDigest", "F.$.phase1bLocks.aiPacketDigest", "L.$.sourceDigest"], ["A.$.works[*].attempts[*].packetInputJson"], null),
        new(SamplingVersionKind.Capability, ["P.$.CapabilityLock.{MatrixVersion,StateAtLock,ProductBuild,DatabaseSchemaBuild,HotfixSetDigest,SqlServerBuild,CompatibilityLevel,Modules,QueryPackVersion,NormalizationSchemaVersion,RuleCatalogVersion,LockDigest}"], [], null),
        new(SamplingVersionKind.Baseline, ["C.$.baselineId", "P.$.BaselineId", "P.$.FixtureVersion", "L.$.baselineDigest", "I.$.source.baselineDigest"], ["G.$.Provenance.{BaselineId,BaselineVersion,EvidenceDigest,EvidenceReference,PackVersion,PackDigest}"], null),
        new(SamplingVersionKind.ReassessmentBaseline, [], [], "No saved predecessor/reassessment baseline ID/version/digest in these owning records."),
        new(SamplingVersionKind.Collection, [], ["P.$.FixtureVersion", "F.$.workSchemaVersion"], "No source collection ID/version/manifest."),
        new(SamplingVersionKind.Query, ["P.$.CapabilityLock.QueryPackVersion"], [], null),
        new(SamplingVersionKind.Normalization, ["P.$.CapabilityLock.NormalizationSchemaVersion", "I.$.source.normalizationVersion", "I.$.source.redactionVersion"], [], null),
        new(SamplingVersionKind.Catalog, ["P.$.CapabilityLock.RuleCatalogVersion", "L.$.mappingDigest", "F.$.phase1bLocks.aiMappingDigest", "A.$.works[*].work.units[*].{ruleId,ruleVersion,proposalId,key,moduleId,severity,evidenceIds}"], ["G.$.Provenance.{CatalogVersion,CatalogDigest,RuleId,RuleVersion,PackVersion,PackDigest}"], null),
        new(SamplingVersionKind.Profile, ["C.$.profileId", "F.$.profileVersion", "L.$.profileId", "L.$.profileDigest", "I.$.source.profileDigest"], [], null),
        new(SamplingVersionKind.Scoring, ["F.$.scoringAlgorithmVersion"], [], null),
        new(SamplingVersionKind.Maturity, [], ["F.$.maturityFixtureDigest"], "MaturityFixtureDigest identifies a locked artifact but no actual saved maturity algorithm/schema/catalog is returned by either owner read."),
        new(SamplingVersionKind.AiProvider, ["L.$.providerVersion"], [], null),
        new(SamplingVersionKind.AiModel, ["F.$.modelVersion"], [], null),
        new(SamplingVersionKind.AiPrompt, ["L.$.promptVersion", "F.$.promptVersion", "I.$.source.promptVersion"], [], null),
        new(SamplingVersionKind.AiSchema, [], ["I.$.schemaVersion", "A.$.works[*].attempts[*].receipt.{receiptId,outputDigest}"], "AcceptedOutputSchemaNotSupplied"),
        new(SamplingVersionKind.AiSettings, [], ["L.$.{policyVersion,fixtureVersion,mappingDigest,epoch}", "F.$.aiPolicyVersion", "F.$.phase1bLocks.{aiFixtureDigest,aiMappingDigest,fixtureEpoch,aiContractDigest}", "A.$.works[*].work.{workId,category,scenario,units}", "A.$.budget"], "No versioned relevant AI settings record is saved; policy/fixture/mapping and budget ledger cannot stand in for it."),
        new(SamplingVersionKind.Application, ["L.$.applicationVersion", "F.$.applicationVersion"], [], null),
        new(SamplingVersionKind.Reviewer, [], ["L.$.initiatingConsultantId"], "No independent evaluation reviewer identity/assignment record."),
        new(SamplingVersionKind.ReviewerEligibility, [], [], "No source-backed qualification, independence, assignment, lifecycle or review grant record."),
        new(SamplingVersionKind.Instruction, [], ["G.$.ReviewPolicy"], "No versioned evaluation review instruction record."),
        new(SamplingVersionKind.Conflict, [], [], "No evaluation conflict disclosure/clearance record."),
        new(SamplingVersionKind.ReviewEvents, [], [], "No source-backed quality-review events/version/cutoff record."),
    ];
}
