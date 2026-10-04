using System.Collections.Immutable;
using System.Text;

namespace ReportPublication;

/// <summary>Structural guards only. They do not prove source eligibility, content classification or authority.</summary>
public static class PublicationValidationV1
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static ImmutableArray<string> RequiredFields { get; } =
    ["acceptedRisks", "coverage", "dimensions", "environmentScope", "executiveSummary", "findings", "healthyControls", "inputs",
        "maturity", "methodology", "recommendations", "redactionMarkers", "rootCauses", "scores", "technicalAppendices.notes",
        "technicalAppendices.protectedReferences", "warnings"];

    public static bool Actor(PublicationActorV1? actor) => actor is not null && Id(actor.TenantId) && Id(actor.ObjectId)
        && Id(actor.SessionId) && actor.SecurityVersion > 0;
    public static bool Scope(PublicationScopeV1? scope) => scope is not null && Id(scope.CustomerId) && Id(scope.ProjectId)
        && Id(scope.EnvironmentId) && Id(scope.AssessmentId);
    public static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool Command(PublicationActorV1? actor, PublishCommandV1? command) => Actor(actor) && command is not null
        && Id(command.OperationId) && Id(command.InvocationId) && Id(command.CorrelationId) && Scope(command.Scope)
        && Id(command.RunId) && command.ExpectedRunRevision > 0 && Digest(command.ExpectedSourceDigest)
        && !command.AcknowledgedWarnings.IsDefault && command.AcknowledgedWarnings.Length <= 100000
        && command.AcknowledgedWarnings.All(Warning)
        && Unique(command.AcknowledgedWarnings.Select(w => (w.Kind, w.RecordId, w.Category)));

    public static bool ExactRequest(PublicationActorV1? actor, ExactReportRequestV1? request) => Actor(actor) && request is not null
        && Scope(request.Scope) && Id(request.ReportVersionId) && Id(request.InvocationId) && Id(request.CorrelationId)
        && Digest(request.ExpectedManifestDigest);

    public static bool Source(SourceCaptureV1? source)
    {
        if (source is null || !Enum.IsDefined(source.AssessmentState) || !Projection(source.Projection)
            || source.Retention is not { } retention || !Id(retention.PolicyId) || !Token(retention.PolicyVersion)
            || !Utc(retention.ClockStartUtc) || !Utc(retention.ExpiresAtUtc) || retention.ClockStartUtc > retention.ExpiresAtUtc
            || retention.HoldReference == Guid.Empty || source.RequiredCategories.IsDefaultOrEmpty
            || source.RequiredCategories.Length > 100000 || !source.RequiredCategories.All(Token) || !Unique(source.RequiredCategories)
            || source.RequiredFields.IsDefault || !source.RequiredFields.Order(StringComparer.Ordinal).SequenceEqual(RequiredFields)
            || source.Provenance.IsDefault || source.Provenance.Length > 100000
            || source.Provenance.Any(p => p is null || !Token(p.Kind) || !Id(p.OpaqueRecordId) || !Digest(p.Digest))
            || !Unique(source.Provenance.Select(p => (p.Kind, p.OpaqueRecordId)))) return false;
        var projection = source.Projection;
        if (!Categories(projection).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .SequenceEqual(source.RequiredCategories.Order(StringComparer.Ordinal))) return false;
        var references = source.Provenance.Select(p => p.OpaqueRecordId).ToHashSet();
        return projection.Recommendations.All(r => (r.Priority.SourceReference is not { } priority || references.Contains(priority))
            && (r.Effort.SourceReference is not { } effort || references.Contains(effort)))
            && projection.Warnings.Where(w => w.Kind == PublicationWarningKindV1.SourceLimitation).All(w => references.Contains(w.RecordId));
    }

    public static bool Projection(PublicationProjectionV1? value)
    {
        if (value is null || !CollectionBounds(value) || !Scope(value.Scope) || !Id(value.RunId) || value.RunRevision < 1 || !Inputs(value.Inputs)
            || !Displays(value.ExecutiveSummary) || value.ExecutiveSummary.IsEmpty || !Displays(value.EnvironmentScope) || value.EnvironmentScope.IsEmpty
            || !Displays(value.Methodology) || value.Methodology.IsEmpty || !Score(value.Scores) || !Maturity(value.Maturity)
            || value.Dimensions.IsDefault || value.Dimensions.Any(d => d is null || !Enum.IsDefined(d.Kind) || !Id(d.Id) || !Token(d.Category) || !Metric(d.Provisional) || !Metric(d.Publishable))
            || !Coverage(value.Coverage) || value.Findings.IsDefault || value.Findings.Any(f => !Finding(f))
            || value.RootCauses.IsDefault || value.RootCauses.Any(r => r is null || !Id(r.Id) || !Token(r.Category)
                || !DisplayAvailability(r.Availability, r.Reason, r.Summary) || !Ids(r.FindingIds) || r.FindingIds.IsEmpty)
            || value.HealthyControls.IsDefault || value.HealthyControls.Any(h => h is null || !Id(h.Id) || !Token(h.Category) || !Token(h.RuleId) || !Token(h.RuleVersion) || !Text(h.Summary))
            || value.Recommendations.IsDefault || value.Recommendations.Any(r => r is null || !Id(r.Id) || !Id(r.FindingId) || !Token(r.Category)
                || !Text(r.Summary) || !SourceDisplay(r.Priority) || !SourceDisplay(r.Effort) || !Enum.IsDefined(r.ReviewState))
            || value.AcceptedRisks.IsDefault || value.AcceptedRisks.Any(r => r is null || !Id(r.Id) || !Id(r.FindingId) || !Token(r.Category)
                || !Id(r.DecisionReference) || !Utc(r.ReviewAtUtc) || !Enum.IsDefined(r.Status))
            || value.Warnings.IsDefault || !value.Warnings.All(Warning)
            || value.TechnicalAppendices is not { } appendix || !Displays(appendix.Notes)
            || appendix.ProtectedReferences.IsDefault || appendix.ProtectedReferences.Any(r => r is null || !Id(r.Id) || !Token(r.Category)
                || !ReferenceAvailability(r.Availability, r.Reason)) || value.RedactionMarkers.IsDefault
            || value.RedactionMarkers.Any(r => r is null || !RequiredFields.Contains(r.Section) || !Id(r.Id) || !MarkerField(r.Section, r.Field)
                || r.Reason is PublicationReasonV1.None or PublicationReasonV1.NotApplicable || !Enum.IsDefined(r.Reason))) return false;

        var allIds = RecordIds(value).ToArray();
        if (allIds.Length > 100000 || !Unique(value.ExecutiveSummary.Select(v => v.Id)) || !Unique(value.EnvironmentScope.Select(v => v.Id))
            || !Unique(value.Methodology.Select(v => v.Id)) || !Unique(value.Dimensions.Select(v => (v.Kind, v.Id)))
            || !Unique(value.Coverage.Items.Select(v => v.Id)) || !Unique(value.Findings.Select(v => v.Id)) || !Unique(value.RootCauses.Select(v => v.Id))
            || !Unique(value.HealthyControls.Select(v => v.Id)) || !Unique(value.Recommendations.Select(v => v.Id)) || !Unique(value.AcceptedRisks.Select(v => v.Id))
            || !Unique(appendix.Notes.Select(v => v.Id)) || !Unique(appendix.ProtectedReferences.Select(v => v.Id))
            || !Unique(value.Warnings.Select(w => (w.Kind, w.RecordId, w.Category)))
            || !Unique(value.RedactionMarkers.Select(r => (r.Section, r.Id, r.Field, r.Reason)))) return false;
        if (value.RedactionMarkers.Any(r => !MarkerExists(value, r))) return false;
        var findings = value.Findings.ToDictionary(f => f.Id);
        var causes = value.RootCauses.ToDictionary(r => r.Id);
        var references = appendix.ProtectedReferences.Select(r => r.Id).ToHashSet();
        if (value.Findings.Any(f => f.ReferenceIds.Any(r => !references.Contains(r)) || f.RootCauseIds.Any(r => !causes.ContainsKey(r)
                || !causes[r].FindingIds.Contains(f.Id)))
            || value.RootCauses.Any(r => r.FindingIds.Any(f => !findings.ContainsKey(f) || !findings[f].RootCauseIds.Contains(r.Id)))
            || value.Recommendations.Any(r => !findings.ContainsKey(r.FindingId)) || value.AcceptedRisks.Any(r => !findings.ContainsKey(r.FindingId))) return false;
        foreach (var warning in value.Warnings)
        {
            if (warning.Kind == PublicationWarningKindV1.MandatoryReviewIncomplete
                && (!findings.TryGetValue(warning.RecordId, out var referenced) || referenced.Category != warning.Category
                    || !referenced.MandatoryReview || referenced.Severity is not (PublicationSeverityV1.High or PublicationSeverityV1.Critical)
                    || referenced.State is not (PublicationFindingStateV1.Proposed or PublicationFindingStateV1.AutoConfirmed))) return false;
            if (warning.Kind == PublicationWarningKindV1.CoverageIncomplete
                && !value.Coverage.Items.Any(i => i.Id == warning.RecordId && i.Category == warning.Category
                    && i.State is not (PublicationCoverageStateV1.Pass or PublicationCoverageStateV1.Finding or PublicationCoverageStateV1.NotApplicable))) return false;
        }
        foreach (var finding in value.Findings.Where(f => f.Severity is PublicationSeverityV1.Critical or PublicationSeverityV1.High
                     && f.State is PublicationFindingStateV1.Proposed or PublicationFindingStateV1.AutoConfirmed))
            if (!finding.MandatoryReview || !value.Warnings.Contains(new(PublicationWarningKindV1.MandatoryReviewIncomplete, finding.Id, finding.Category))) return false;
        foreach (var item in value.Coverage.Items.Where(i => i.State is not (PublicationCoverageStateV1.Pass or PublicationCoverageStateV1.Finding or PublicationCoverageStateV1.NotApplicable)))
            if (!value.Warnings.Contains(new(PublicationWarningKindV1.CoverageIncomplete, item.Id, item.Category))) return false;
        return true;
    }

    private static bool Inputs(PublicationInputsV1? value) => value is not null && Id(value.BaselineId)
        && new[] { value.BaselineDigest, value.CapabilityLockDigest, value.RuleCatalogDigest, value.ScoringProfileDigest, value.MaturityProfileDigest,
            value.ReviewSnapshotDigest, value.CoverageSnapshotDigest, value.RunInputDigest }.All(Digest)
        && new[] { value.RuleCatalogVersion, value.ScoringProfileVersion, value.MaturityProfileVersion, value.ScoringAlgorithmVersion,
            value.MaturityAlgorithmVersion, value.ApplicationVersion, value.AiPolicyVersion }.All(Token)
        && (value.DesiredOutcomeVersion is null ? value.DesiredOutcomeDigest is null : Token(value.DesiredOutcomeVersion) && Digest(value.DesiredOutcomeDigest))
        && (value.ModelVersion is null || Token(value.ModelVersion)) && (value.PromptVersion is null || Token(value.PromptVersion));

    private static bool Finding(PublicationFindingV1? value) => value is not null && Id(value.Id) && Token(value.Category) && Text(value.Title)
        && Text(value.Summary) && Enum.IsDefined(value.Severity) && Enum.IsDefined(value.State) && Enum.IsDefined(value.Method)
        && (value.ConfidenceAvailability == PublicationAvailabilityV1.Available ? value.ConfidencePercent is >= 0 and <= 100
            && Token(value.ConfidenceBand) && value.ConfidenceReason == PublicationReasonV1.None
            : value.ConfidenceAvailability == PublicationAvailabilityV1.Unavailable && value.ConfidencePercent is null && value.ConfidenceBand is null
              && UnavailableReason(value.ConfidenceReason))
        && Ids(value.ReferenceIds) && Ids(value.RootCauseIds) && !value.RootCauseIds.IsEmpty && Digest(value.OriginalDigest) && value.ReviewRevision >= 0;

    private static bool Coverage(PublicationCoverageV1? value)
    {
        if (value is null || value.Items.IsDefault || value.Items.Length > 100000 || value.Counts is not { } counts
            || counts.Planned < 0 || counts.Executed < 0 || counts.Gap < 0 || counts.NotApplicable < 0
            || counts.Planned != value.Items.Length || value.Items.Any(i => i is null || !Id(i.Id) || !Token(i.Category)
                || !Enum.IsDefined(i.State) || !Enum.IsDefined(i.Reason)
                || (i.State is PublicationCoverageStateV1.Pass or PublicationCoverageStateV1.Finding
                    ? i.Reason != PublicationReasonV1.None : i.Reason.ToString() != i.State.ToString()))) return false;
        return counts.Executed == value.Items.Count(i => i.State is PublicationCoverageStateV1.Pass or PublicationCoverageStateV1.Finding)
            && counts.NotApplicable == value.Items.Count(i => i.State == PublicationCoverageStateV1.NotApplicable)
            && counts.Gap == value.Items.Count(i => i.State is not (PublicationCoverageStateV1.Pass or PublicationCoverageStateV1.Finding or PublicationCoverageStateV1.NotApplicable));
    }

    private static bool Metric(PublicationScoreValueV1? value) => value is not null
        && (value.Availability == PublicationAvailabilityV1.Available ? value.Value is >= 0 and <= 100 && value.Reason == PublicationReasonV1.None
            : value.Availability == PublicationAvailabilityV1.Unavailable && value.Value is null && UnavailableReason(value.Reason));
    public static bool Score(PublicationScoreV1? value) => value is not null && Metric(value.Provisional) && Metric(value.Publishable) && Metric(value.Quality);
    private static bool Maturity(PublicationMaturityV1? value) => value is not null && Token(value.AlgorithmVersion) && Digest(value.InputDigest) && Digest(value.ContentDigest)
        && (value.Availability == PublicationAvailabilityV1.Available ? value.Level is { } level && Enum.IsDefined(level) && value.Reason == PublicationReasonV1.None
            : value.Availability == PublicationAvailabilityV1.Unavailable && value.Level is null && UnavailableReason(value.Reason));
    private static bool SourceDisplay(PublicationSourceDisplayValueV1? value) => value is not null && DisplayAvailability(value.Availability, value.Reason, value.Value)
        && (value.Availability == PublicationAvailabilityV1.Available ? value.SourceReference is { } reference && Id(reference) : value.SourceReference is null);
    private static bool Displays(ImmutableArray<PublicationDisplayRecordV1> values) => !values.IsDefault && values.Length <= 100000
        && values.All(v => v is not null && Id(v.Id) && Token(v.Category) && Text(v.Title) && DisplayAvailability(v.Availability, v.Reason, v.Text));
    private static bool DisplayAvailability(PublicationAvailabilityV1 availability, PublicationReasonV1 reason, string? value)
        => availability == PublicationAvailabilityV1.Available ? Text(value) && reason == PublicationReasonV1.None
            : availability == PublicationAvailabilityV1.Unavailable && value is null && UnavailableReason(reason);
    private static bool ReferenceAvailability(PublicationAvailabilityV1 availability, PublicationReasonV1 reason)
        => availability == PublicationAvailabilityV1.Available ? reason == PublicationReasonV1.None
            : availability == PublicationAvailabilityV1.Redacted ? reason == PublicationReasonV1.Redacted
            : availability == PublicationAvailabilityV1.Unavailable && UnavailableReason(reason);
    private static bool Warning(PublicationWarningV1? warning) => warning is not null && Enum.IsDefined(warning.Kind) && Id(warning.RecordId) && Token(warning.Category);
    private static bool CollectionBounds(PublicationProjectionV1 value)
    {
        if (value.Coverage is null || value.TechnicalAppendices is null) return false;
        long records = Count(value.ExecutiveSummary) + Count(value.EnvironmentScope) + Count(value.Methodology)
            + Count(value.Dimensions) + Count(value.Coverage.Items) + Count(value.Findings) + Count(value.RootCauses)
            + Count(value.HealthyControls) + Count(value.Recommendations) + Count(value.AcceptedRisks) + Count(value.Warnings)
            + Count(value.TechnicalAppendices.Notes) + Count(value.TechnicalAppendices.ProtectedReferences) + Count(value.RedactionMarkers);
        if (records > 100000) return false;
        long links = 0;
        foreach (var finding in value.Findings)
        {
            if (finding is null) return false;
            links += Count(finding.ReferenceIds) + Count(finding.RootCauseIds);
            if (links > 100000) return false;
        }
        foreach (var cause in value.RootCauses)
        {
            if (cause is null) return false;
            links += Count(cause.FindingIds);
            if (links > 100000) return false;
        }
        return true;
    }
    private static long Count<T>(ImmutableArray<T> values) => values.IsDefault ? 100001 : values.Length;
    private static bool MarkerExists(PublicationProjectionV1 projection, PublicationRedactionMarkerV1 marker) => marker.Section switch
    {
        "executiveSummary" => projection.ExecutiveSummary.Any(v => v.Id == marker.Id),
        "environmentScope" => projection.EnvironmentScope.Any(v => v.Id == marker.Id),
        "methodology" => projection.Methodology.Any(v => v.Id == marker.Id),
        "dimensions" => projection.Dimensions.Any(v => v.Id == marker.Id),
        "coverage" => projection.Coverage.Items.Any(v => v.Id == marker.Id),
        "findings" => projection.Findings.Any(v => v.Id == marker.Id),
        "rootCauses" => projection.RootCauses.Any(v => v.Id == marker.Id),
        "healthyControls" => projection.HealthyControls.Any(v => v.Id == marker.Id),
        "recommendations" => projection.Recommendations.Any(v => v.Id == marker.Id),
        "acceptedRisks" => projection.AcceptedRisks.Any(v => v.Id == marker.Id),
        "technicalAppendices.notes" => projection.TechnicalAppendices.Notes.Any(v => v.Id == marker.Id),
        "technicalAppendices.protectedReferences" => projection.TechnicalAppendices.ProtectedReferences.Any(v => v.Id == marker.Id),
        _ => false
    };
    internal static bool MarkerField(string section, string field)
    {
        string[] fields = section switch
        {
            "executiveSummary" or "environmentScope" or "methodology" or "technicalAppendices.notes" => ["id", "category", "title", "text", "availability", "reason"],
            "dimensions" => ["kind", "id", "category", "provisional", "publishable"],
            "coverage" => ["id", "category", "state", "reason"],
            "findings" => ["id", "category", "title", "summary", "severity", "state", "method", "confidencePercent", "confidenceBand", "confidenceAvailability", "confidenceReason", "mandatoryReview", "referenceIds", "rootCauseIds", "originalDigest", "reviewRevision"],
            "rootCauses" => ["id", "category", "summary", "findingIds", "availability", "reason"],
            "healthyControls" => ["id", "category", "ruleId", "ruleVersion", "summary"],
            "recommendations" => ["id", "findingId", "category", "summary", "priority", "effort", "reviewState"],
            "acceptedRisks" => ["id", "findingId", "category", "decisionReference", "reviewAtUtc", "status"],
            "technicalAppendices.protectedReferences" => ["id", "category", "availability", "reason"],
            _ => []
        };
        return fields.Contains(field, StringComparer.Ordinal);
    }
    private static bool UnavailableReason(PublicationReasonV1 reason) => Enum.IsDefined(reason) && reason != PublicationReasonV1.None;
    private static bool Ids(ImmutableArray<Guid> values) => !values.IsDefault && values.Length <= 100000 && values.All(Id) && Unique(values);
    private static bool Id(Guid value) => value != Guid.Empty;
    private static bool Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
    private static bool Unique<T>(IEnumerable<T> values) => !values.GroupBy(v => v).Any(g => g.Skip(1).Any());
    private static bool Token(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');
    private static bool Text(string? value)
    {
        if (value is null || value.Length > 8192) return false;
        try { _ = StrictUtf8.GetByteCount(value); return value.EnumerateRunes().Take(4097).Count() <= 4096; }
        catch (EncoderFallbackException) { return false; }
    }
    private static IEnumerable<string> Categories(PublicationProjectionV1 p) => p.ExecutiveSummary.Select(v => v.Category)
        .Concat(p.EnvironmentScope.Select(v => v.Category)).Concat(p.Methodology.Select(v => v.Category)).Concat(p.Dimensions.Select(v => v.Category))
        .Concat(p.Coverage.Items.Select(v => v.Category)).Concat(p.Findings.Select(v => v.Category)).Concat(p.RootCauses.Select(v => v.Category))
        .Concat(p.HealthyControls.Select(v => v.Category)).Concat(p.Recommendations.Select(v => v.Category)).Concat(p.AcceptedRisks.Select(v => v.Category))
        .Concat(p.Warnings.Select(v => v.Category)).Concat(p.TechnicalAppendices.Notes.Select(v => v.Category)).Concat(p.TechnicalAppendices.ProtectedReferences.Select(v => v.Category));
    private static IEnumerable<Guid> RecordIds(PublicationProjectionV1 p) => p.ExecutiveSummary.Select(v => v.Id)
        .Concat(p.EnvironmentScope.Select(v => v.Id)).Concat(p.Methodology.Select(v => v.Id)).Concat(p.Dimensions.Select(v => v.Id))
        .Concat(p.Coverage.Items.Select(v => v.Id)).Concat(p.Findings.Select(v => v.Id)).Concat(p.RootCauses.Select(v => v.Id))
        .Concat(p.HealthyControls.Select(v => v.Id)).Concat(p.Recommendations.Select(v => v.Id)).Concat(p.AcceptedRisks.Select(v => v.Id))
        .Concat(p.TechnicalAppendices.Notes.Select(v => v.Id)).Concat(p.TechnicalAppendices.ProtectedReferences.Select(v => v.Id));
}
