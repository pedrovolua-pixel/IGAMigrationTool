using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticEvaluation;

/// <summary>Derives supplied synthetic cohort warnings; does not qualify evidence or reviewers.</summary>
public static class EvaluationWarningBuilder
{
    internal const int MaximumEntries = 100_000;

    public static EvaluationWarningResult Build(EvaluationWarningInput? input)
    {
        if (input?.Accuracy is null || input.Metadata is null || input.Metadata.Count > MaximumEntries)
        {
            return new(EvaluationComparisonIssue.InvalidInput, null);
        }

        var metadata = input.Metadata.ToArray();
        if (metadata.Length > MaximumEntries || metadata.Any(item => item is null))
        {
            return new(EvaluationComparisonIssue.InvalidInput, null);
        }

        var rebuilt = EvaluationAccuracyBuilder.Build(new(input.Accuracy.Locks, input.Accuracy.Members, input.Accuracy.Reviews));
        if (!rebuilt.HasProjection)
        {
            return new(EvaluationComparisonIssue.InvalidInput, null);
        }

        var accuracy = rebuilt.Projection!;
        var memberIds = accuracy.Members.Select(member => member.Id).ToHashSet(StringComparer.Ordinal);
        var metadataIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in metadata)
        {
            if (!IsReference(item.MemberId) || !IsReference(item.EnvironmentId) || !IsReference(item.ModuleId)
                || !IsReference(item.CategoryId) || !IsReference(item.RuleId) || !IsReference(item.ModelPromptId)
                || !IsReference(item.ConfidenceBandId))
            {
                return new(EvaluationComparisonIssue.InvalidReference, null);
            }

            if (!Enum.IsDefined(item.OriginSeverity))
            {
                return new(EvaluationComparisonIssue.InvalidMetadata, null);
            }

            if (!memberIds.Contains(item.MemberId))
            {
                return new(EvaluationComparisonIssue.UnexpectedMetadata, null);
            }

            if (!metadataIds.Add(item.MemberId))
            {
                return new(EvaluationComparisonIssue.DuplicateMetadata, null);
            }
        }

        if (metadataIds.Count != memberIds.Count)
        {
            return new(EvaluationComparisonIssue.MissingMetadata, null);
        }

        Array.Sort(metadata, (left, right) => StringComparer.Ordinal.Compare(left.MemberId, right.MemberId));
        var reviewsById = accuracy.Reviews.ToDictionary(review => review.MemberId, StringComparer.Ordinal);
        var generalIds = accuracy.Members.Where(member => member.Track == EvaluationTrack.GeneralAi)
            .Select(member => member.Id).ToHashSet(StringComparer.Ordinal);
        var generalMetadata = metadata.Where(item => generalIds.Contains(item.MemberId)).ToArray();
        var breakdowns = new List<EvaluationBreakdown>();
        foreach (var dimension in Enum.GetValues<EvaluationBreakdownDimension>().Where(value => value != EvaluationBreakdownDimension.DesiredOutcomeVersion))
        {
            foreach (var group in generalMetadata.GroupBy(item => Key(item, dimension), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var counts = Count(reviewsById, group.Select(item => item.MemberId), EvaluationTrack.GeneralAi);
                breakdowns.Add(new(dimension, group.Key, new(counts, counts.Denominator < 30)));
            }
        }

        foreach (var group in accuracy.Members.Where(member => member.Track == EvaluationTrack.ApprovedDesiredOutcome)
            .GroupBy(member => member.DesiredOutcomeVersion!, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var counts = Count(reviewsById, group.Select(member => member.Id), EvaluationTrack.ApprovedDesiredOutcome);
            breakdowns.Add(new(EvaluationBreakdownDimension.DesiredOutcomeVersion, group.Key, new(counts, counts.Denominator < 30)));
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "synthetic-evaluation-warning-v1");
            writer.WriteString("accuracyDigest", accuracy.ContentDigest);
            writer.WriteStartArray("metadata");
            foreach (var item in metadata)
            {
                writer.WriteStartObject();
                writer.WriteString("memberId", item.MemberId);
                writer.WriteString("originSeverity", item.OriginSeverity.ToString());
                writer.WriteString("environmentId", item.EnvironmentId);
                writer.WriteString("moduleId", item.ModuleId);
                writer.WriteString("categoryId", item.CategoryId);
                writer.WriteString("ruleId", item.RuleId);
                writer.WriteString("modelPromptId", item.ModelPromptId);
                writer.WriteString("confidenceBandId", item.ConfidenceBandId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartObject("summaries");
            writer.WritePropertyName("generalAi");
            WriteSummary(writer, accuracy.GeneralAi);
            writer.WritePropertyName("desiredOutcome");
            WriteSummary(writer, accuracy.DesiredOutcome);
            writer.WriteEndObject();
            writer.WriteStartArray("breakdowns");
            foreach (var item in breakdowns)
            {
                writer.WriteStartObject();
                writer.WriteString("dimension", item.Dimension.ToString());
                writer.WriteString("key", item.Key);
                writer.WritePropertyName("summary");
                WriteSummary(writer, item.Summary.Counts);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = buffer.ToArray();
        return new(null, new(accuracy, metadata, breakdowns.ToArray(), Encoding.UTF8.GetString(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    internal static bool IsReference(string? value)
    {
        const string prefix = "synthetic-";
        return value is not null && value.Length > prefix.Length && value.Length <= 128
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && value.AsSpan(prefix.Length).ToArray().All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');
    }

    internal static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static EvaluationAccuracy Count(IReadOnlyDictionary<string, EvaluationReview> reviewsById, IEnumerable<string> ids, EvaluationTrack track)
    {
        var selected = 0;
        var confirmed = 0;
        var rejected = 0;
        var indeterminate = 0;
        var unreviewed = 0;
        var corrected = 0;
        foreach (var id in ids)
        {
            var review = reviewsById[id];
            selected++;
            switch (review.Outcome)
            {
                case EvaluationReviewOutcome.Confirmed: confirmed++; break;
                case EvaluationReviewOutcome.Rejected: rejected++; break;
                case EvaluationReviewOutcome.Indeterminate: indeterminate++; break;
                case EvaluationReviewOutcome.Unreviewed: unreviewed++; break;
                case EvaluationReviewOutcome.Corrected:
                    corrected++;
                    if (review.OriginatingClassification == EvaluationOriginClassification.Confirmed) { confirmed++; }
                    else { rejected++; }
                    break;
            }
        }

        return new(selected, confirmed, rejected, indeterminate, unreviewed, corrected, track);
    }

    private static string Key(EvaluationMemberMetadata item, EvaluationBreakdownDimension dimension) => dimension switch
    {
        EvaluationBreakdownDimension.Environment => item.EnvironmentId,
        EvaluationBreakdownDimension.Category => item.CategoryId,
        EvaluationBreakdownDimension.Severity => item.OriginSeverity.ToString(),
        EvaluationBreakdownDimension.Module => item.ModuleId,
        EvaluationBreakdownDimension.Rule => item.RuleId,
        EvaluationBreakdownDimension.ModelPrompt => item.ModelPromptId,
        EvaluationBreakdownDimension.ConfidenceBand => item.ConfidenceBandId,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    private static void WriteSummary(Utf8JsonWriter writer, EvaluationAccuracy counts)
    {
        writer.WriteStartObject();
        WriteCountProperties(writer, counts);
        writer.WriteBoolean("lowSampleWarning", counts.Denominator < 30);
        writer.WriteEndObject();
    }

    internal static void WriteCounts(Utf8JsonWriter writer, EvaluationAccuracy? counts)
    {
        if (counts is null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        WriteCountProperties(writer, counts);
        writer.WriteEndObject();
    }

    private static void WriteCountProperties(Utf8JsonWriter writer, EvaluationAccuracy counts)
    {
        writer.WriteNumber("selected", counts.Selected);
        writer.WriteNumber("confirmed", counts.Confirmed);
        writer.WriteNumber("rejected", counts.Rejected);
        writer.WriteNumber("indeterminate", counts.Indeterminate);
        writer.WriteNumber("unreviewed", counts.Unreviewed);
        writer.WriteNumber("corrected", counts.Corrected);
        writer.WriteNumber("denominator", counts.Denominator);
    }
}
