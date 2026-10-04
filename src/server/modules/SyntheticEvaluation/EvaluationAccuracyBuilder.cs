using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticEvaluation;

/// <summary>Captures supplied synthetic outcomes; does not select or authorize a sample.</summary>
public static class EvaluationAccuracyBuilder
{
    private const int MaximumEntries = 100_000;

    public static EvaluationResult Build(EvaluationInput? input)
    {
        if (input?.Locks is null || input.Members is null || input.Reviews is null
            || input.Members.Count > MaximumEntries || input.Reviews.Count > MaximumEntries)
        {
            return Deny(EvaluationIssue.InvalidInput);
        }

        // Detach both collections before validating or deriving any result.
        var members = input.Members.ToArray();
        var reviews = input.Reviews.ToArray();
        var locks = input.Locks with { };
        if (members.Length > MaximumEntries || reviews.Length > MaximumEntries
            || members.Any(member => member is null) || reviews.Any(review => review is null))
        {
            return Deny(EvaluationIssue.InvalidInput);
        }

        if (!IsSyntheticReference(locks.EvaluationId) || !IsSyntheticReference(locks.ScopeId))
        {
            return Deny(EvaluationIssue.InvalidReference);
        }

        if (!IsDigest(locks.PopulationDigest) || !IsDigest(locks.SampleDigest)
            || !IsDigest(locks.VersionManifestDigest) || locks.CorrectionCutoffUtc.Offset != TimeSpan.Zero)
        {
            return Deny(EvaluationIssue.InvalidVersion);
        }

        var membersById = new Dictionary<string, EvaluationMember>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (!IsSyntheticReference(member.Id)
                || member.DesiredOutcomeVersion is not null && !IsSyntheticReference(member.DesiredOutcomeVersion))
            {
                return Deny(EvaluationIssue.InvalidReference);
            }

            if (!Enum.IsDefined(member.Track))
            {
                return Deny(EvaluationIssue.InvalidMember);
            }

            if (member.Track == EvaluationTrack.GeneralAi
                ? member.DesiredOutcomeVersion is not null || member.DesiredOutcomeApproval is not null
                : member.DesiredOutcomeVersion is null
                    || member.DesiredOutcomeApproval != EvaluationOutcomeApproval.CustomerApproved)
            {
                return Deny(EvaluationIssue.InvalidDesiredOutcome);
            }

            if (!membersById.TryAdd(member.Id, member))
            {
                return Deny(EvaluationIssue.DuplicateMember);
            }
        }

        var reviewedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var review in reviews)
        {
            if (!IsSyntheticReference(review.MemberId))
            {
                return Deny(EvaluationIssue.InvalidReference);
            }

            if (!Enum.IsDefined(review.Outcome)
                || review.OriginatingClassification is { } origin && !Enum.IsDefined(origin)
                || (review.Outcome == EvaluationReviewOutcome.Corrected
                    ? review.OriginatingClassification is null
                    : review.OriginatingClassification is not null))
            {
                return Deny(EvaluationIssue.InvalidReview);
            }

            if (!membersById.ContainsKey(review.MemberId))
            {
                return Deny(EvaluationIssue.UnexpectedReview);
            }

            if (!reviewedIds.Add(review.MemberId))
            {
                return Deny(EvaluationIssue.DuplicateReview);
            }
        }

        if (reviewedIds.Count != membersById.Count)
        {
            return Deny(EvaluationIssue.MissingReview);
        }

        Array.Sort(members, (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        Array.Sort(reviews, (left, right) => StringComparer.Ordinal.Compare(left.MemberId, right.MemberId));
        var generalAi = Count(EvaluationTrack.GeneralAi, reviews, membersById);
        var desiredOutcome = Count(EvaluationTrack.ApprovedDesiredOutcome, reviews, membersById);
        var canonicalBytes = Canonicalize(locks, members, reviews);
        var projection = new EvaluationProjection(locks, members, reviews, generalAi, desiredOutcome,
            Encoding.UTF8.GetString(canonicalBytes), Convert.ToHexStringLower(SHA256.HashData(canonicalBytes)));
        return new EvaluationResult(null, projection);
    }

    private static EvaluationResult Deny(EvaluationIssue issue) => new(issue, null);

    private static bool IsSyntheticReference(string? value)
    {
        const string prefix = "synthetic-";
        if (value is null || value.Length <= prefix.Length || value.Length > 128
            || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in value.AsSpan(prefix.Length))
        {
            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static EvaluationAccuracy Count(EvaluationTrack track, EvaluationReview[] reviews,
        Dictionary<string, EvaluationMember> membersById)
    {
        var selected = 0;
        var confirmed = 0;
        var rejected = 0;
        var indeterminate = 0;
        var unreviewed = 0;
        var corrected = 0;
        foreach (var review in reviews)
        {
            if (membersById[review.MemberId].Track != track)
            {
                continue;
            }

            selected++;
            switch (review.Outcome)
            {
                case EvaluationReviewOutcome.Confirmed:
                    confirmed++;
                    break;
                case EvaluationReviewOutcome.Rejected:
                    rejected++;
                    break;
                case EvaluationReviewOutcome.Indeterminate:
                    indeterminate++;
                    break;
                case EvaluationReviewOutcome.Unreviewed:
                    unreviewed++;
                    break;
                case EvaluationReviewOutcome.Corrected:
                    corrected++;
                    if (review.OriginatingClassification == EvaluationOriginClassification.Confirmed)
                    {
                        confirmed++;
                    }
                    else
                    {
                        rejected++;
                    }

                    break;
            }
        }

        return new EvaluationAccuracy(selected, confirmed, rejected, indeterminate, unreviewed, corrected, track);
    }

    private static byte[] Canonicalize(EvaluationLocks locks, EvaluationMember[] members, EvaluationReview[] reviews)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "synthetic-evaluation-accuracy-v1");
            writer.WriteStartObject("locks");
            writer.WriteString("evaluationId", locks.EvaluationId);
            writer.WriteString("scopeId", locks.ScopeId);
            writer.WriteString("populationDigest", locks.PopulationDigest);
            writer.WriteString("sampleDigest", locks.SampleDigest);
            writer.WriteString("versionManifestDigest", locks.VersionManifestDigest);
            writer.WriteString("correctionCutoffUtc", locks.CorrectionCutoffUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
            writer.WriteStartArray("members");
            foreach (var member in members)
            {
                writer.WriteStartObject();
                writer.WriteString("id", member.Id);
                writer.WriteString("track", member.Track.ToString());
                writer.WriteString("desiredOutcomeVersion", member.DesiredOutcomeVersion);
                writer.WriteString("desiredOutcomeApproval", member.DesiredOutcomeApproval?.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("reviews");
            foreach (var review in reviews)
            {
                writer.WriteStartObject();
                writer.WriteString("memberId", review.MemberId);
                writer.WriteString("outcome", review.Outcome.ToString());
                writer.WriteString("originatingClassification", review.OriginatingClassification?.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
