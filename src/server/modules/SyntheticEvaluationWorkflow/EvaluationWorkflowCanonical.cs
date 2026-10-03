using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SyntheticEvaluation;

namespace SyntheticEvaluationWorkflow;

internal static class EvaluationWorkflowCanonical
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new UtcConverter());
        return options;
    }
    private sealed class UtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            DateTimeOffset.ParseExact(reader.GetString()!, "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value, Options);
    internal static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch);
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static IReadOnlyList<T> List<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());
    internal static EvaluationReviewerRegistryInput Registry(EvaluationReviewerRegistryInput input)
    {
        var captured = EvaluationReviewerPolicy.Capture(input).Registry ??
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.InvalidInput);
        return new(captured.VersionId, captured.PolicyVersionId, captured.InstructionVersionId,
            List(captured.Identities.OrderBy(i => i.Id, StringComparer.Ordinal)),
            List(captured.Assignments.OrderBy(a => a.Id, StringComparer.Ordinal).Select(a => a with
            {
                ReviewCategoryIds = List(a.ReviewCategoryIds.Order(StringComparer.Ordinal)),
                HistoryCategoryIds = List(a.HistoryCategoryIds.Order(StringComparer.Ordinal))
            })),
            List(captured.Members.OrderBy(m => m.Id, StringComparer.Ordinal)),
            List(captured.Conflicts.OrderBy(c => c.AssignmentId, StringComparer.Ordinal)
                .ThenBy(c => c.MemberId, StringComparer.Ordinal)));
    }
    internal static string EventJson(EvaluationWorkflowEvent e) => Json(new
    {
        e.EventId, e.Sequence, e.AggregateRevision, e.MemberRevision, e.MemberId, e.Kind, e.ActorId,
        e.AssignmentId, e.RegistryVersionId, e.RegistryDigest, e.RecordedAtUtc, e.RecordedOutcome,
        e.OriginatingClassification, e.Reason, e.EvidenceReferenceIds, e.Correction, e.CommandDigest,
        e.PreviousEventDigest
    });
    internal static EvaluationWorkflowVersion Version(WorkflowSource source, SamplingProjection sample,
        string sourceDigest, long revision, WorkflowRegistry registry, IReadOnlyList<WorkflowStoredEvent> events,
        IReadOnlyDictionary<string, WorkflowCurrent> members, DateTimeOffset cutoff, string? predecessor)
    {
        var manifest = Json(new
        {
            schemaVersion = "synthetic-evaluation-workflow-version-v1", version = revision, sourceDigest,
            populationDigest = sample.PopulationDigest, sampleDigest = sample.ContentDigest,
            originalSampleVersionManifestDigest = sample.VersionManifestDigest,
            originalSampleCorrectionCutoffUtc = sample.Versions.CorrectionCutoffUtc,
            registryVersionId = registry.Input.VersionId, registryDigest = registry.Digest,
            lastEventSequence = (long)events.Count,
            eventsDigest = Hash(Json(events.Select(e => e.Event.ContentDigest).ToArray())),
            correctionCutoffUtc = cutoff, predecessorDigest = predecessor
        });
        var accuracy = EvaluationAccuracyBuilder.Build(new(new("synthetic-workflow-version-" + revision.ToString(CultureInfo.InvariantCulture),
                source.ScopeId, sample.PopulationDigest, sample.ContentDigest, Hash(manifest), cutoff),
            sample.Selected.Select(s => new EvaluationMember(s.Member.Id, EvaluationTrack.GeneralAi)).ToArray(),
            sample.Selected.Select(s => members[s.Member.Id]).Select(m =>
                new EvaluationReview(m.MemberId, m.Outcome, m.OriginatingClassification)).ToArray())).Projection
            ?? throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch);
        var warning = EvaluationWarningBuilder.Build(new(accuracy, Metadata(sample))).Projection
            ?? throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch);
        var json = Json(new
        {
            schemaVersion = "synthetic-evaluation-workflow-snapshot-v1",
            versionManifestJson = manifest, accuracyCanonicalJson = accuracy.CanonicalJson,
            warningCanonicalJson = warning.CanonicalJson
        });
        return new(revision, cutoff, events.Count, registry.Input.VersionId, registry.Digest, manifest, Hash(manifest),
            predecessor, accuracy.CanonicalJson, accuracy.ContentDigest, warning.CanonicalJson,
            warning.ContentDigest, json, Hash(json));
    }
    internal static EvaluationMemberMetadata[] Metadata(SamplingProjection sample) => sample.Selected.Select(s =>
        new EvaluationMemberMetadata(s.Member.Id, Enum.Parse<EvaluationOriginSeverity>(s.Member.Severity.ToString()),
            s.Member.EnvironmentId, s.Member.PrimaryModuleId, s.Member.CategoryId, s.Member.RuleVersion,
            s.Member.ModelPromptVersion, s.Member.ConfidenceBandId)).ToArray();
}
