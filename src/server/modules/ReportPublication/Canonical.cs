using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ReportPublication;

/// <summary>Native typed commitments; no arbitrary JSON source admission or authority grant.</summary>
public static partial class NativePublicationCanonicalV1
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public const int MaximumProjectionBytes = 32 * 1024 * 1024;
    public const int MaximumScoreBytes = 1024 * 1024;
    public const int MaximumCommandBytes = 64 * 1024;

    public static byte[] ProjectionBytes(PublicationProjectionV1 projection)
    {
        Require(PublicationValidationV1.Projection(projection));
        return Encode(Projection(projection), MaximumProjectionBytes);
    }

    public static byte[] ScoreBytes(PublicationScoreV1 score)
    {
        Require(ValidMetric(score?.Provisional) && ValidMetric(score?.Publishable) && ValidMetric(score?.Quality));
        return Encode(Score(score!), MaximumScoreBytes);
    }

    public static byte[] SourceBytes(SourceCaptureV1 source)
    {
        Require(PublicationValidationV1.Source(source));
        var p = source.Projection;
        return Encode(Map(("scope", Scope(p.Scope, true)), ("runId", Id(p.RunId)), ("runRevision", Counter(p.RunRevision)),
            ("assessmentState", source.AssessmentState.ToString()), ("inputs", Inputs(p.Inputs)), ("projection", Projection(p)),
            ("score", Score(p.Scores)), ("warnings", Warnings(p.Warnings)), ("retention", Retention(source.Retention)),
            ("requiredCategories", Tokens(source.RequiredCategories)), ("requiredFields", Tokens(source.RequiredFields)),
            ("provenance", source.Provenance.OrderBy(v => v.Kind, StringComparer.Ordinal).ThenBy(v => Id(v.OpaqueRecordId), StringComparer.Ordinal)
                .Select(v => (object?)Map(("kind", v.Kind), ("opaqueRecordId", Id(v.OpaqueRecordId)), ("digest", v.Digest))).ToArray())),
            MaximumProjectionBytes + MaximumScoreBytes + 1024 * 1024);
    }

    public static byte[] CommandBytes(PublicationActorV1 actor, PublishCommandV1 command)
    {
        Require(PublicationValidationV1.Command(actor, command));
        return Encode(Map(("actor", Actor(actor)), ("operationId", Id(command.OperationId)), ("scope", Scope(command.Scope, true)),
            ("runId", Id(command.RunId)), ("expectedRunRevision", Counter(command.ExpectedRunRevision)),
            ("expectedSourceDigest", command.ExpectedSourceDigest), ("acknowledgedWarnings", Warnings(command.AcknowledgedWarnings))), MaximumCommandBytes);
    }

    public static byte[] ReadRequestBytes(PublicationActorV1 actor, ExactReportRequestV1 request)
    {
        Require(PublicationValidationV1.ExactRequest(actor, request));
        return Encode(Map(("schemaVersion", "report-exact-read-request-v1"), ("actor", Actor(actor)), ("scope", Scope(request.Scope, true)),
            ("reportVersionId", Id(request.ReportVersionId)), ("expectedManifestDigest", request.ExpectedManifestDigest),
            ("invocationId", Id(request.InvocationId))), MaximumCommandBytes);
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static SortedDictionary<string, object?> Projection(PublicationProjectionV1 p) => Map(
        ("schemaVersion", "health-report-projection-v1"), ("scope", Scope(p.Scope, false)), ("assessmentId", Id(p.Scope.AssessmentId)),
        ("runId", Id(p.RunId)), ("runRevision", Counter(p.RunRevision)), ("inputs", Inputs(p.Inputs)),
        ("executiveSummary", Displays(p.ExecutiveSummary)), ("environmentScope", Displays(p.EnvironmentScope)), ("scores", Score(p.Scores)),
        ("maturity", Map(("availability", p.Maturity.Availability.ToString()), ("level", p.Maturity.Level?.ToString()),
            ("algorithmVersion", p.Maturity.AlgorithmVersion), ("inputDigest", p.Maturity.InputDigest),
            ("contentDigest", p.Maturity.ContentDigest), ("reason", p.Maturity.Reason.ToString()))),
        ("dimensions", p.Dimensions.OrderBy(v => v.Kind.ToString(), StringComparer.Ordinal).ThenBy(v => Id(v.Id), StringComparer.Ordinal)
            .Select(v => (object?)Map(("kind", v.Kind.ToString()), ("id", Id(v.Id)), ("category", v.Category),
                ("provisional", Metric(v.Provisional)), ("publishable", Metric(v.Publishable)))).ToArray()),
        ("coverage", Map(("items", ById(p.Coverage.Items, v => v.Id, v => Map(("id", Id(v.Id)), ("category", v.Category),
                ("state", v.State.ToString()), ("reason", v.Reason.ToString())))),
            ("counts", Map(("planned", Counter(p.Coverage.Counts.Planned)), ("executed", Counter(p.Coverage.Counts.Executed)),
                ("gap", Counter(p.Coverage.Counts.Gap)), ("notApplicable", Counter(p.Coverage.Counts.NotApplicable)))))),
        ("findings", ById(p.Findings, v => v.Id, v => Map(("id", Id(v.Id)), ("category", v.Category), ("title", v.Title), ("summary", v.Summary),
            ("severity", v.Severity.ToString()), ("state", v.State.ToString()), ("method", v.Method.ToString()),
            ("confidencePercent", Decimal(v.ConfidencePercent)), ("confidenceBand", v.ConfidenceBand),
            ("confidenceAvailability", v.ConfidenceAvailability.ToString()), ("confidenceReason", v.ConfidenceReason.ToString()),
            ("mandatoryReview", v.MandatoryReview), ("referenceIds", Ids(v.ReferenceIds)), ("rootCauseIds", Ids(v.RootCauseIds)),
            ("originalDigest", v.OriginalDigest), ("reviewRevision", Counter(v.ReviewRevision))))),
        ("rootCauses", ById(p.RootCauses, v => v.Id, v => Map(("id", Id(v.Id)), ("category", v.Category), ("summary", v.Summary),
            ("findingIds", Ids(v.FindingIds)), ("availability", v.Availability.ToString()), ("reason", v.Reason.ToString())))),
        ("healthyControls", ById(p.HealthyControls, v => v.Id, v => Map(("id", Id(v.Id)), ("category", v.Category),
            ("ruleId", v.RuleId), ("ruleVersion", v.RuleVersion), ("summary", v.Summary)))),
        ("recommendations", ById(p.Recommendations, v => v.Id, v => Map(("id", Id(v.Id)), ("findingId", Id(v.FindingId)), ("category", v.Category),
            ("summary", v.Summary), ("priority", SourceDisplay(v.Priority)), ("effort", SourceDisplay(v.Effort)), ("reviewState", v.ReviewState.ToString())))),
        ("acceptedRisks", ById(p.AcceptedRisks, v => v.Id, v => Map(("id", Id(v.Id)), ("findingId", Id(v.FindingId)), ("category", v.Category),
            ("decisionReference", Id(v.DecisionReference)), ("reviewAtUtc", Time(v.ReviewAtUtc)), ("status", v.Status.ToString())))),
        ("warnings", Warnings(p.Warnings)), ("methodology", Displays(p.Methodology)),
        ("technicalAppendices", Map(("notes", Displays(p.TechnicalAppendices.Notes)),
            ("protectedReferences", ById(p.TechnicalAppendices.ProtectedReferences, v => v.Id, v => Map(("id", Id(v.Id)), ("category", v.Category),
                ("availability", v.Availability.ToString()), ("reason", v.Reason.ToString())))))),
        ("redactionMarkers", p.RedactionMarkers.OrderBy(v => v.Section, StringComparer.Ordinal).ThenBy(v => Id(v.Id), StringComparer.Ordinal)
            .ThenBy(v => v.Field, StringComparer.Ordinal).ThenBy(v => v.Reason.ToString(), StringComparer.Ordinal)
            .Select(v => (object?)Map(("section", v.Section), ("id", Id(v.Id)), ("field", v.Field), ("reason", v.Reason.ToString()))).ToArray()));

    private static object Inputs(PublicationInputsV1 v) => Map(("baselineId", Id(v.BaselineId)), ("baselineDigest", v.BaselineDigest),
        ("capabilityLockDigest", v.CapabilityLockDigest), ("ruleCatalogVersion", v.RuleCatalogVersion), ("ruleCatalogDigest", v.RuleCatalogDigest),
        ("scoringProfileVersion", v.ScoringProfileVersion), ("scoringProfileDigest", v.ScoringProfileDigest),
        ("maturityProfileVersion", v.MaturityProfileVersion), ("maturityProfileDigest", v.MaturityProfileDigest),
        ("desiredOutcomeVersion", v.DesiredOutcomeVersion), ("desiredOutcomeDigest", v.DesiredOutcomeDigest),
        ("scoringAlgorithmVersion", v.ScoringAlgorithmVersion), ("maturityAlgorithmVersion", v.MaturityAlgorithmVersion),
        ("applicationVersion", v.ApplicationVersion), ("reviewSnapshotDigest", v.ReviewSnapshotDigest),
        ("coverageSnapshotDigest", v.CoverageSnapshotDigest), ("runInputDigest", v.RunInputDigest),
        ("aiPolicyVersion", v.AiPolicyVersion), ("modelVersion", v.ModelVersion), ("promptVersion", v.PromptVersion));
    private static object Score(PublicationScoreV1 v) => Map(("schemaVersion", "health-report-score-v1"), ("provisional", Metric(v.Provisional)),
        ("publishable", Metric(v.Publishable)), ("quality", Metric(v.Quality)));
    private static object Metric(PublicationScoreValueV1 v) => Map(("availability", v.Availability.ToString()), ("value", Decimal(v.Value)), ("reason", v.Reason.ToString()));
    private static object SourceDisplay(PublicationSourceDisplayValueV1 v) => Map(("availability", v.Availability.ToString()),
        ("value", v.Value), ("reason", v.Reason.ToString()), ("sourceReference", v.SourceReference is { } id ? Id(id) : null));
    private static object Retention(PublicationRetentionV1 v) => Map(("policyId", Id(v.PolicyId)), ("policyVersion", v.PolicyVersion),
        ("class", "PublishedArtifact"), ("clockStartUtc", Time(v.ClockStartUtc)), ("expiresAtUtc", Time(v.ExpiresAtUtc)),
        ("holdReference", v.HoldReference is { } id ? Id(id) : null));
    private static object[] Displays(IEnumerable<PublicationDisplayRecordV1> values) => values.Select(v => (object?)Map(("id", Id(v.Id)),
        ("category", v.Category), ("title", v.Title), ("text", v.Text), ("availability", v.Availability.ToString()), ("reason", v.Reason.ToString()))).ToArray()!;
    private static object[] Warnings(IEnumerable<PublicationWarningV1> values) => values.OrderBy(v => v.Kind.ToString(), StringComparer.Ordinal)
        .ThenBy(v => Id(v.RecordId), StringComparer.Ordinal).ThenBy(v => v.Category, StringComparer.Ordinal)
        .Select(v => (object?)Map(("kind", v.Kind.ToString()), ("recordId", Id(v.RecordId)), ("category", v.Category))).ToArray()!;
    private static object[] Tokens(IEnumerable<string> values) => values.Order(StringComparer.Ordinal).Select(v => (object)v).ToArray();
    private static object[] Ids(IEnumerable<Guid> values) => values.Select(Id).Order(StringComparer.Ordinal).Select(v => (object)v).ToArray();
    private static object[] ById<T>(IEnumerable<T> values, Func<T, Guid> id, Func<T, object> map) => values.OrderBy(v => Id(id(v)), StringComparer.Ordinal).Select(map).ToArray();
    private static object Actor(PublicationActorV1 v) => Map(("tenantId", Id(v.TenantId)), ("objectId", Id(v.ObjectId)),
        ("sessionId", Id(v.SessionId)), ("securityVersion", Counter(v.SecurityVersion)));
    private static object Scope(PublicationScopeV1 v, bool includeAssessment) => includeAssessment
        ? Map(("customerId", Id(v.CustomerId)), ("projectId", Id(v.ProjectId)), ("environmentId", Id(v.EnvironmentId)), ("assessmentId", Id(v.AssessmentId)))
        : Map(("customerId", Id(v.CustomerId)), ("projectId", Id(v.ProjectId)), ("environmentId", Id(v.EnvironmentId)));
    private static SortedDictionary<string, object?> Map(params (string Key, object? Value)[] fields)
        => new(fields.ToDictionary(v => v.Key, v => v.Value), StringComparer.Ordinal);
    private static string Id(Guid id) => id.ToString("D");
    private static string Counter(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string? Decimal(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string Time(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    private static bool ValidMetric(PublicationScoreValueV1? v) => v is not null && (v.Availability == PublicationAvailabilityV1.Available
        ? v.Value is >= 0 and <= 100 && v.Reason == PublicationReasonV1.None
        : v.Availability == PublicationAvailabilityV1.Unavailable && v.Value is null && Enum.IsDefined(v.Reason) && v.Reason != PublicationReasonV1.None);
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Invalid native publication value."); }

    private static byte[] Encode(object value, int maximumBytes)
    {
        var writer = new CanonicalWriter(maximumBytes); writer.Write(value, 0); return writer.Bytes();
    }

    private sealed class CanonicalWriter(int maximumBytes)
    {
        private readonly StringBuilder builder = new();
        private int byteCount;
        internal byte[] Bytes() => StrictUtf8.GetBytes(builder.ToString());
        private void Append(string text)
        {
            byteCount = checked(byteCount + StrictUtf8.GetByteCount(text));
            Require(byteCount <= maximumBytes); builder.Append(text);
        }
        internal void Write(object? value, int depth)
        {
            Require(depth <= 32);
            switch (value)
            {
                case null: Append("null"); break;
                case bool b: Append(b ? "true" : "false"); break;
                case string s: String(s); break;
                case SortedDictionary<string, object?> map:
                    Append("{"); var firstProperty = true;
                    foreach (var field in map)
                    { if (!firstProperty) Append(","); firstProperty = false; String(field.Key); Append(":"); Write(field.Value, depth + 1); }
                    Append("}"); break;
                case object?[] array:
                    Append("["); var firstItem = true;
                    foreach (var item in array)
                    { if (!firstItem) Append(","); firstItem = false; Write(item, depth + 1); }
                    Append("]"); break;
                default: throw new InvalidOperationException("Invalid native publication scalar.");
            }
        }
        private void String(string value)
        {
            _ = StrictUtf8.GetByteCount(value); Append("\"");
            foreach (var rune in value.EnumerateRunes())
            {
                Append(rune.Value switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\b' => "\\b",
                    '\f' => "\\f",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    < 32 => "\\u" + rune.Value.ToString("x4", CultureInfo.InvariantCulture),
                    _ => rune.ToString()
                });
            }
            Append("\"");
        }
    }
}
