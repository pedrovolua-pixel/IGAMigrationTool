namespace SyntheticEvaluation;

public enum EvaluationTrack
{
    GeneralAi,
    ApprovedDesiredOutcome
}

public enum EvaluationReviewOutcome
{
    Confirmed,
    Rejected,
    Indeterminate,
    Unreviewed,
    Corrected
}

public enum EvaluationOriginClassification
{
    Confirmed,
    Rejected
}

public enum EvaluationOutcomeApproval
{
    CustomerApproved,
    Draft,
    Inferred,
    ConsultantReviewed
}

public enum EvaluationIssue
{
    InvalidInput,
    InvalidReference,
    InvalidVersion,
    InvalidMember,
    DuplicateMember,
    MissingReview,
    UnexpectedReview,
    DuplicateReview,
    InvalidReview,
    InvalidDesiredOutcome
}

public sealed record EvaluationLocks(
    string EvaluationId,
    string ScopeId,
    string PopulationDigest,
    string SampleDigest,
    string VersionManifestDigest,
    DateTimeOffset CorrectionCutoffUtc);

public sealed record EvaluationMember(
    string Id,
    EvaluationTrack Track,
    string? DesiredOutcomeVersion = null,
    EvaluationOutcomeApproval? DesiredOutcomeApproval = null);

public sealed record EvaluationReview(
    string MemberId,
    EvaluationReviewOutcome Outcome,
    EvaluationOriginClassification? OriginatingClassification = null);

public sealed record EvaluationInput(
    EvaluationLocks Locks,
    IReadOnlyList<EvaluationMember> Members,
    IReadOnlyList<EvaluationReview> Reviews);

public sealed record EvaluationResult(EvaluationIssue? Issue, EvaluationProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}

public sealed class EvaluationAccuracy
{
    internal EvaluationAccuracy(int selected, int confirmed, int rejected, int indeterminate,
        int unreviewed, int corrected, EvaluationTrack track)
    {
        Selected = selected;
        Confirmed = confirmed;
        Rejected = rejected;
        Indeterminate = indeterminate;
        Unreviewed = unreviewed;
        Corrected = corrected;
        Denominator = confirmed + rejected;
        ConfirmedAccuracyPercent = Denominator == 0 ? null : 100m * confirmed / Denominator;
        ExceedsGeneralAiThreshold = track == EvaluationTrack.GeneralAi && Denominator > 0
            ? 5L * confirmed > 4L * Denominator
            : null;
    }

    public int Selected { get; }
    public int Confirmed { get; }
    public int Rejected { get; }
    public int Indeterminate { get; }
    public int Unreviewed { get; }
    public int Corrected { get; }
    public int Denominator { get; }
    public decimal? ConfirmedAccuracyPercent { get; }
    public bool? ExceedsGeneralAiThreshold { get; }
}

public sealed class EvaluationProjection
{
    internal EvaluationProjection(EvaluationLocks locks, EvaluationMember[] members,
        EvaluationReview[] reviews, EvaluationAccuracy generalAi, EvaluationAccuracy desiredOutcome,
        string canonicalJson, string contentDigest)
    {
        Locks = locks with { };
        Members = Array.AsReadOnly((EvaluationMember[])members.Clone());
        Reviews = Array.AsReadOnly((EvaluationReview[])reviews.Clone());
        GeneralAi = generalAi;
        DesiredOutcome = desiredOutcome;
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
    }

    public EvaluationLocks Locks { get; }
    public IReadOnlyList<EvaluationMember> Members { get; }
    public IReadOnlyList<EvaluationReview> Reviews { get; }
    public EvaluationAccuracy GeneralAi { get; }
    public EvaluationAccuracy DesiredOutcome { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
