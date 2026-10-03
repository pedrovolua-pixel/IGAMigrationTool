namespace SyntheticEvaluation;

public enum SamplingSeverity { Critical, High, Medium, Low, Informational }

public enum SamplingVersionKind
{
    SourceBuild, EnvironmentEvidence, Capability, Baseline, ReassessmentBaseline,
    Collection, Query, Normalization, Catalog, Profile, Scoring, Maturity,
    AiProvider, AiModel, AiPrompt, AiSchema, AiSettings, Application, Reviewer,
    ReviewerEligibility, Instruction, Conflict, ReviewEvents
}

public enum SamplingIssue
{
    InvalidInput, InvalidReference, InvalidVersion, InvalidMember, DuplicateMember,
    AmbiguousPrimary, InvalidBudget, InsufficientBudget
}

public sealed record SamplingVersionBinding(SamplingVersionKind Kind, string Version);

public sealed record SamplingVersionManifest(IReadOnlyList<SamplingVersionBinding> Bindings,
    DateTimeOffset CorrectionCutoffUtc, DateTimeOffset EvaluationDateUtc,
    string? PredecessorSampleDigest = null);

public sealed record SamplingMember(string Id, string ScopeId, string EnvironmentId,
    string PrimaryModuleId, string CategoryId, string RuleVersion, string ModelPromptVersion,
    string ConfidenceBandId, SamplingSeverity Severity, bool PrimarySettled, bool Available,
    string? UnavailableReason, IReadOnlyList<string> SecondaryModuleIds);

public sealed record SamplingInput(string SampleId, string PopulationId, string ScopeId,
    string Seed, SamplingVersionManifest Versions, IReadOnlyList<SamplingMember> Members,
    int? HardReviewBudget = null);

public sealed record SamplingResult(SamplingIssue? Issue, SamplingProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}

public sealed class SamplingStratum
{
    internal SamplingStratum(string environmentId, string primaryModuleId, string categoryId,
        SamplingSeverity severity, int populationCount, int allocation)
    {
        EnvironmentId = environmentId;
        PrimaryModuleId = primaryModuleId;
        CategoryId = categoryId;
        Severity = severity;
        PopulationCount = populationCount;
        Allocation = allocation;
    }

    public string EnvironmentId { get; }
    public string PrimaryModuleId { get; }
    public string CategoryId { get; }
    public SamplingSeverity Severity { get; }
    public int PopulationCount { get; }
    public int Allocation { get; }
}

public sealed class SamplingRank
{
    internal SamplingRank(string memberId, string rankDigest)
    {
        MemberId = memberId;
        RankDigest = rankDigest;
    }

    public string MemberId { get; }
    public string RankDigest { get; }
}

public sealed class SamplingSelection
{
    internal SamplingSelection(SamplingMember member, bool mandatory, string? rankDigest)
    {
        Member = member;
        Mandatory = mandatory;
        RankDigest = rankDigest;
    }

    public SamplingMember Member { get; }
    public bool Mandatory { get; }
    public string? RankDigest { get; }
}

public sealed class SamplingProjection
{
    internal SamplingProjection(SamplingInput input, SamplingStratum[] strata, SamplingRank[] ranks,
        SamplingSelection[] selected, string versionJson, string versionDigest,
        string populationJson, string populationDigest, string canonicalJson, string contentDigest)
    {
        SampleId = input.SampleId;
        PopulationId = input.PopulationId;
        ScopeId = input.ScopeId;
        Seed = input.Seed;
        HardReviewBudget = input.HardReviewBudget;
        Versions = input.Versions;
        Population = Array.AsReadOnly(input.Members.ToArray());
        Strata = Array.AsReadOnly((SamplingStratum[])strata.Clone());
        Ranks = Array.AsReadOnly((SamplingRank[])ranks.Clone());
        Selected = Array.AsReadOnly((SamplingSelection[])selected.Clone());
        VersionManifestJson = versionJson;
        VersionManifestDigest = versionDigest;
        PopulationJson = populationJson;
        PopulationDigest = populationDigest;
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
    }

    public string SampleId { get; }
    public string PopulationId { get; }
    public string ScopeId { get; }
    public string Seed { get; }
    public int? HardReviewBudget { get; }
    public SamplingVersionManifest Versions { get; }
    public IReadOnlyList<SamplingMember> Population { get; }
    public IReadOnlyList<SamplingStratum> Strata { get; }
    public IReadOnlyList<SamplingRank> Ranks { get; }
    public IReadOnlyList<SamplingSelection> Selected { get; }
    public string VersionManifestJson { get; }
    public string VersionManifestDigest { get; }
    public string PopulationJson { get; }
    public string PopulationDigest { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
