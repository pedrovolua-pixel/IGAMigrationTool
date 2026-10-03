using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using SyntheticEvaluation;

internal static class Program
{
    private static int checks;
    private const string GoldenDigest = "0f3488d40029bc7073e03073b20f0f570f0a3fd3e19528dc80d56f56e7a1dfdc";
    // Entire independent literal: authored before inspecting domain implementation.
    private const string GoldenJson = """
        {"schema":"synthetic-evaluation-accuracy-v1","locks":{"evaluationId":"synthetic-evaluation","scopeId":"synthetic-scope","populationDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","sampleDigest":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","versionManifestDigest":"cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc","correctionCutoffUtc":"2026-10-03T00:00:00.0000000\u002B00:00"},"members":[{"id":"synthetic-a","track":"GeneralAi","desiredOutcomeVersion":null,"desiredOutcomeApproval":null},{"id":"synthetic-b","track":"GeneralAi","desiredOutcomeVersion":null,"desiredOutcomeApproval":null},{"id":"synthetic-c","track":"GeneralAi","desiredOutcomeVersion":null,"desiredOutcomeApproval":null},{"id":"synthetic-d","track":"GeneralAi","desiredOutcomeVersion":null,"desiredOutcomeApproval":null},{"id":"synthetic-e","track":"ApprovedDesiredOutcome","desiredOutcomeVersion":"synthetic-goal-v1","desiredOutcomeApproval":"CustomerApproved"},{"id":"synthetic-f","track":"ApprovedDesiredOutcome","desiredOutcomeVersion":"synthetic-goal-v1","desiredOutcomeApproval":"CustomerApproved"}],"reviews":[{"memberId":"synthetic-a","outcome":"Confirmed","originatingClassification":null},{"memberId":"synthetic-b","outcome":"Corrected","originatingClassification":"Rejected"},{"memberId":"synthetic-c","outcome":"Indeterminate","originatingClassification":null},{"memberId":"synthetic-d","outcome":"Unreviewed","originatingClassification":null},{"memberId":"synthetic-e","outcome":"Corrected","originatingClassification":"Confirmed"},{"memberId":"synthetic-f","outcome":"Rejected","originatingClassification":null}]}
        """;

    private static int Main()
    {
        try
        {
            GoldenAndCapture();
            OutcomeMatrix();
            ArithmeticProperties();
            MembershipAndNulls();
            ReferenceAndVersionBoundaries();
            DesiredOutcomeAdmission();
            CanonicalSensitivityAndCulture();
            SizeBoundaries();
            Console.WriteLine($"PASS: {checks} independent synthetic evaluation assertions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL after {checks} assertions: {ex}");
            return 1;
        }
    }

    private static EvaluationLocks Locks() => new("synthetic-evaluation", "synthetic-scope", new('a', 64), new('b', 64), new('c', 64),
        new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    private static EvaluationMember Member(string id, EvaluationTrack track = EvaluationTrack.GeneralAi) =>
        track == EvaluationTrack.ApprovedDesiredOutcome
            ? new(id, track, "synthetic-goal-v1", EvaluationOutcomeApproval.CustomerApproved)
            : new(id, track);

    private static EvaluationInput One(EvaluationReviewOutcome outcome = EvaluationReviewOutcome.Confirmed,
        EvaluationOriginClassification? origin = null, EvaluationTrack track = EvaluationTrack.GeneralAi) =>
        new(Locks(), [Member("synthetic-item", track)], [new("synthetic-item", outcome, origin)]);

    private static EvaluationInput Fixture() => new(Locks(),
        [Member("synthetic-f", EvaluationTrack.ApprovedDesiredOutcome), Member("synthetic-b"), Member("synthetic-d"),
            Member("synthetic-e", EvaluationTrack.ApprovedDesiredOutcome), Member("synthetic-c"), Member("synthetic-a")],
        [new("synthetic-c", EvaluationReviewOutcome.Indeterminate), new("synthetic-e", EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Confirmed),
            new("synthetic-b", EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Rejected), new("synthetic-f", EvaluationReviewOutcome.Rejected),
            new("synthetic-a", EvaluationReviewOutcome.Confirmed), new("synthetic-d", EvaluationReviewOutcome.Unreviewed)]);

    private static EvaluationProjection Success(EvaluationInput input)
    {
        var result = EvaluationAccuracyBuilder.Build(input);
        Check(result.HasProjection && result.Issue is null && result.Projection is not null, "accepted fixture projection");
        return result.Projection!;
    }

    private static void Denied(EvaluationInput? input, EvaluationIssue expected)
    {
        var result = EvaluationAccuracyBuilder.Build(input);
        Check(!result.HasProjection && result.Projection is null, "denial returns no source payload/projection");
        Equal(expected, result.Issue, "single-invalid typed denial");
    }

    private static void GoldenAndCapture()
    {
        // This independently checks the framework encoder prerequisite, not the builder.
        Equal("2026-10-03T00:00:00.0000000\\u002B00:00", JavaScriptEncoder.Default.Encode("2026-10-03T00:00:00.0000000+00:00"), "default encoder plus escaping");
        Equal(1660, Encoding.UTF8.GetByteCount(GoldenJson), "independent literal byte length");
        Equal(GoldenDigest, Hash(GoldenJson), "independently Python-hashed complete literal");
        var input = Fixture();
        var members = input.Members.ToList();
        var reviews = input.Reviews.ToList();
        var mutable = input with { Members = members, Reviews = reviews };
        var projection = Success(mutable);
        Equal(GoldenJson, projection.CanonicalJson, "entire canonical envelope literal golden");
        Equal(GoldenDigest, projection.ContentDigest, "entire canonical envelope digest golden");
        Equal(input.Locks, projection.Locks, "all source locks preserved");
        Equal("synthetic-a,synthetic-b,synthetic-c,synthetic-d,synthetic-e,synthetic-f", string.Join(',', projection.Members.Select(m => m.Id)), "ordinal member ordering");
        Equal("synthetic-a,synthetic-b,synthetic-c,synthetic-d,synthetic-e,synthetic-f", string.Join(',', projection.Reviews.Select(r => r.MemberId)), "ordinal review ordering");
        Counts(projection.GeneralAi, 4, 1, 1, 1, 1, 1, 50m, false);
        Counts(projection.DesiredOutcome, 2, 1, 1, 0, 0, 1, 50m, null);
        members[0] = Member("synthetic-other", EvaluationTrack.ApprovedDesiredOutcome);
        reviews[0] = new("synthetic-other", EvaluationReviewOutcome.Rejected);
        members.Clear();
        reviews.Clear();
        Equal(6, projection.Members.Count(), "members detached from mutable input");
        Equal(6, projection.Reviews.Count(), "reviews detached from mutable input");
        Equal(GoldenJson, projection.CanonicalJson, "original capture survives input mutation");
        Equal(GoldenDigest, projection.ContentDigest, "original capture digest survives input mutation");
        AssertImmutable((object)projection.Members, "member collection");
        AssertImmutable((object)projection.Reviews, "review collection");
        Equal(GoldenJson, projection.CanonicalJson, "failed output mutation retains canonical bytes");
        var reordered = Success(input with { Members = input.Members.Reverse().ToArray(), Reviews = input.Reviews.Reverse().ToArray() });
        Equal(GoldenJson, reordered.CanonicalJson, "reordering preserves complete bytes");
        Equal(GoldenDigest, reordered.ContentDigest, "reordering preserves digest");
    }

    private static void AssertImmutable(object values, string label)
    {
        Check(values is not Array && values is not IDictionary, $"{label} exposes no mutable array/dictionary");
        if (values is IList list && list.Count > 0)
        {
            var threw = false;
            try { list[0] = list[0]; }
            catch (NotSupportedException) { threw = true; }
            Check(threw, $"{label} non-generic mutation rejected");
        }
        if (values is IList<EvaluationMember> members && members.Count > 0)
        {
            var threw = false;
            try { members[0] = Member("synthetic-write"); }
            catch (NotSupportedException) { threw = true; }
            Check(threw, "generic member mutation rejected");
        }
        if (values is IList<EvaluationReview> reviews && reviews.Count > 0)
        {
            var threw = false;
            try { reviews[0] = new("synthetic-write", EvaluationReviewOutcome.Rejected); }
            catch (NotSupportedException) { threw = true; }
            Check(threw, "generic review mutation rejected");
        }
    }

    private static void OutcomeMatrix()
    {
        EvaluationOriginClassification?[] origins = [null, EvaluationOriginClassification.Confirmed, EvaluationOriginClassification.Rejected,
            (EvaluationOriginClassification)(-1), (EvaluationOriginClassification)99];
        foreach (var track in Enum.GetValues<EvaluationTrack>())
            foreach (var outcome in Enum.GetValues<EvaluationReviewOutcome>())
                foreach (var origin in origins)
                {
                    var input = One(outcome, origin, track);
                    var valid = outcome == EvaluationReviewOutcome.Corrected
                        ? origin is EvaluationOriginClassification.Confirmed or EvaluationOriginClassification.Rejected
                        : origin is null;
                    if (!valid) { Denied(input, EvaluationIssue.InvalidReview); continue; }
                    var projection = Success(input);
                    var a = track == EvaluationTrack.GeneralAi ? projection.GeneralAi : projection.DesiredOutcome;
                    var confirmed = outcome == EvaluationReviewOutcome.Confirmed || outcome == EvaluationReviewOutcome.Corrected && origin == EvaluationOriginClassification.Confirmed ? 1 : 0;
                    var rejected = outcome == EvaluationReviewOutcome.Rejected || outcome == EvaluationReviewOutcome.Corrected && origin == EvaluationOriginClassification.Rejected ? 1 : 0;
                    var denominator = confirmed + rejected;
                    Counts(a, 1, confirmed, rejected, outcome == EvaluationReviewOutcome.Indeterminate ? 1 : 0, outcome == EvaluationReviewOutcome.Unreviewed ? 1 : 0,
                        outcome == EvaluationReviewOutcome.Corrected ? 1 : 0, denominator == 0 ? null : 100m * confirmed,
                        track == EvaluationTrack.GeneralAi && denominator != 0 ? confirmed == 1 : null);
                    var empty = track == EvaluationTrack.GeneralAi ? projection.DesiredOutcome : projection.GeneralAi;
                    Counts(empty, 0, 0, 0, 0, 0, 0, null, null);
                }
        foreach (var value in new[] { -1, 99, int.MaxValue })
        {
            Denied(One((EvaluationReviewOutcome)value), EvaluationIssue.InvalidReview);
            Denied(new(Locks(), [new("synthetic-item", (EvaluationTrack)value)], [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidMember);
        }
    }

    private static void ArithmeticProperties()
    {
        var empty = Success(new(Locks(), [], []));
        Counts(empty.GeneralAi, 0, 0, 0, 0, 0, 0, null, null);
        Counts(empty.DesiredOutcome, 0, 0, 0, 0, 0, 0, null, null);
        // Exhaustive small denominators: independent rational cross-multiplication, no builder-derived expectations.
        for (var denominator = 1; denominator <= 60; denominator++)
            for (var confirmed = 0; confirmed <= denominator; confirmed++)
            {
                var members = Enumerable.Range(0, denominator).Select(i => Member($"synthetic-p{i:D3}")).ToArray();
                var reviews = members.Select((member, i) => new EvaluationReview(member.Id,
                    i < confirmed ? EvaluationReviewOutcome.Confirmed : EvaluationReviewOutcome.Rejected)).ToArray();
                var actual = Success(new(Locks(), members, reviews));
                Counts(actual.GeneralAi, denominator, confirmed, denominator - confirmed, 0, 0, 0, 100m * confirmed / denominator,
                    (long)confirmed * 100 > (long)denominator * 80);
                Equal(null, actual.DesiredOutcome.ExceedsGeneralAiThreshold, "no desired threshold on arithmetic fixtures");
            }
        // Large exact boundary, both neighbors, and repeating-decimal display.
        foreach (var confirmed in new[] { 79999, 80000, 80001 })
        {
            var members = Enumerable.Range(0, 100000).Select(i => Member($"synthetic-scale{i:D6}")).ToArray();
            var reviews = members.Select((member, i) => new EvaluationReview(member.Id,
                i < confirmed ? EvaluationReviewOutcome.Corrected : EvaluationReviewOutcome.Rejected,
                i < confirmed ? EvaluationOriginClassification.Confirmed : null)).ToArray();
            var actual = Success(new(Locks(), members, reviews));
            Counts(actual.GeneralAi, 100000, confirmed, 100000 - confirmed, 0, 0, confirmed, confirmed / 1000m, confirmed > 80000);
        }
        var mixed = Success(new(Locks(), [Member("synthetic-yes"), Member("synthetic-no"), Member("synthetic-incomplete"), Member("synthetic-unavailable")],
            [new("synthetic-yes", EvaluationReviewOutcome.Confirmed), new("synthetic-no", EvaluationReviewOutcome.Rejected),
                new("synthetic-incomplete", EvaluationReviewOutcome.Indeterminate), new("synthetic-unavailable", EvaluationReviewOutcome.Unreviewed)]));
        Counts(mixed.GeneralAi, 4, 1, 1, 1, 1, 0, 50m, false);
    }

    private static void MembershipAndNulls()
    {
        Denied(null, EvaluationIssue.InvalidInput);
        Denied(new(null!, [], []), EvaluationIssue.InvalidInput);
        Denied(new(Locks(), null!, []), EvaluationIssue.InvalidInput);
        Denied(new(Locks(), [], null!), EvaluationIssue.InvalidInput);
        Denied(new(Locks(), [null!], []), EvaluationIssue.InvalidInput);
        Denied(new(Locks(), [Member("synthetic-item")], [null!]), EvaluationIssue.InvalidInput);
        Denied(One() with { Reviews = [] }, EvaluationIssue.MissingReview);
        Denied(new(Locks(), [], [new("synthetic-item", EvaluationReviewOutcome.Unreviewed)]), EvaluationIssue.UnexpectedReview);
        Denied(One() with { Reviews = [new("synthetic-foreign", EvaluationReviewOutcome.Confirmed)] }, EvaluationIssue.UnexpectedReview);
        Denied(One() with { Members = [Member("synthetic-item"), Member("synthetic-item")] }, EvaluationIssue.DuplicateMember);
        Denied(One() with { Members = [Member("synthetic-item"), Member("synthetic-item", EvaluationTrack.ApprovedDesiredOutcome)] }, EvaluationIssue.DuplicateMember);
        Denied(One() with { Reviews = [new("synthetic-item", EvaluationReviewOutcome.Confirmed), new("synthetic-item", EvaluationReviewOutcome.Rejected)] }, EvaluationIssue.DuplicateReview);
        Denied(One() with { Members = [Member("synthetic-item"), Member("synthetic-case")], Reviews = [new("synthetic-item", EvaluationReviewOutcome.Confirmed)] }, EvaluationIssue.MissingReview);
        // More reviews than members, each independently valid, cannot be discarded.
        Denied(One() with { Reviews = [new("synthetic-item", EvaluationReviewOutcome.Confirmed), new("synthetic-extra", EvaluationReviewOutcome.Unreviewed)] }, EvaluationIssue.UnexpectedReview);
    }

    private static void ReferenceAndVersionBoundaries()
    {
        string?[] invalid = [null, "", "synthetic-", "Synthetic-item", "synthetic-A", "synthetic-item ", " synthetic-item", "synthetic-item\n", "synthetic-\0item",
            "synthetic-é", "synthetic-中", "synthetic-../path", "synthetic-http://x", "https://secret.example", "synthetic-a+b", "synthetic-a@b", "synthetic-a\\b", "synthetic-a\"b",
            "synthetic-" + new string('a', 119)];
        foreach (var value in invalid)
        {
            Denied(One() with { Locks = Locks() with { EvaluationId = value! } }, EvaluationIssue.InvalidReference);
            Denied(One() with { Locks = Locks() with { ScopeId = value! } }, EvaluationIssue.InvalidReference);
            Denied(new(Locks(), [new(value!, EvaluationTrack.GeneralAi)], [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidReference);
            Denied(One() with { Reviews = [new(value!, EvaluationReviewOutcome.Confirmed)] }, EvaluationIssue.InvalidReference);
            // Null desired version is a missing required field, not malformed reference.
            if (value is not null)
                Denied(new(Locks(), [new("synthetic-item", EvaluationTrack.ApprovedDesiredOutcome, value, EvaluationOutcomeApproval.CustomerApproved)],
                    [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidReference);
        }
        foreach (var id in new[] { "synthetic-a", "synthetic-0", "synthetic-.", "synthetic-_", "synthetic--", "synthetic-a0._-", "synthetic-" + new string('z', 118) })
        {
            var p = Success(new(Locks() with { EvaluationId = id, ScopeId = id }, [Member(id)], [new(id, EvaluationReviewOutcome.Confirmed)]));
            Equal(id, p.Members.Single().Id, "allowed ID preserved without normalization");
            Success(new(Locks(), [new(id, EvaluationTrack.ApprovedDesiredOutcome, id, EvaluationOutcomeApproval.CustomerApproved)], [new(id, EvaluationReviewOutcome.Confirmed)]));
        }
        string?[] badDigests = [null, "", new('a', 63), new('a', 65), new('A', 64), new('g', 64), " " + new string('a', 63), new string('a', 63) + "\n", new string('a', 63) + "é"];
        foreach (var digest in badDigests)
        {
            Denied(One() with { Locks = Locks() with { PopulationDigest = digest! } }, EvaluationIssue.InvalidVersion);
            Denied(One() with { Locks = Locks() with { SampleDigest = digest! } }, EvaluationIssue.InvalidVersion);
            Denied(One() with { Locks = Locks() with { VersionManifestDigest = digest! } }, EvaluationIssue.InvalidVersion);
        }
        Success(One() with { Locks = Locks() with { PopulationDigest = new('0', 64), SampleDigest = new('f', 64), VersionManifestDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"[..64] } });
        foreach (var offset in new[] { TimeSpan.FromHours(1), TimeSpan.FromHours(-1), TimeSpan.FromMinutes(30) })
            Denied(One() with { Locks = Locks() with { CorrectionCutoffUtc = Locks().CorrectionCutoffUtc.ToOffset(offset) } }, EvaluationIssue.InvalidVersion);
        Success(One() with { Locks = Locks() with { CorrectionCutoffUtc = DateTimeOffset.MinValue } });
        Success(One() with { Locks = Locks() with { CorrectionCutoffUtc = DateTimeOffset.MaxValue } });
    }

    private static void DesiredOutcomeAdmission()
    {
        foreach (var approval in new EvaluationOutcomeApproval?[] { null, EvaluationOutcomeApproval.Draft, EvaluationOutcomeApproval.Inferred,
            EvaluationOutcomeApproval.ConsultantReviewed, (EvaluationOutcomeApproval)(-1), (EvaluationOutcomeApproval)99 })
            Denied(new(Locks(), [new("synthetic-item", EvaluationTrack.ApprovedDesiredOutcome, "synthetic-goal", approval)],
                [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidDesiredOutcome);
        Denied(new(Locks(), [new("synthetic-item", EvaluationTrack.ApprovedDesiredOutcome, null, EvaluationOutcomeApproval.CustomerApproved)],
            [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidDesiredOutcome);
        foreach (var member in new[] { new EvaluationMember("synthetic-item", EvaluationTrack.GeneralAi, "synthetic-goal"),
            new EvaluationMember("synthetic-item", EvaluationTrack.GeneralAi, null, EvaluationOutcomeApproval.CustomerApproved),
            new EvaluationMember("synthetic-item", EvaluationTrack.GeneralAi, "synthetic-goal", EvaluationOutcomeApproval.CustomerApproved) })
            Denied(new(Locks(), [member], [new("synthetic-item", EvaluationReviewOutcome.Confirmed)]), EvaluationIssue.InvalidDesiredOutcome);
        var onlyDesired = Success(One(track: EvaluationTrack.ApprovedDesiredOutcome));
        Counts(onlyDesired.GeneralAi, 0, 0, 0, 0, 0, 0, null, null);
        Counts(onlyDesired.DesiredOutcome, 1, 1, 0, 0, 0, 0, 100m, null);
        var separated = Success(new(Locks(), [Member("synthetic-general"), Member("synthetic-desired", EvaluationTrack.ApprovedDesiredOutcome)],
            [new("synthetic-general", EvaluationReviewOutcome.Rejected), new("synthetic-desired", EvaluationReviewOutcome.Confirmed)]));
        Counts(separated.GeneralAi, 1, 0, 1, 0, 0, 0, 0m, false);
        Counts(separated.DesiredOutcome, 1, 1, 0, 0, 0, 0, 100m, null);
    }

    private static void CanonicalSensitivityAndCulture()
    {
        var original = Success(Fixture());
        EvaluationLocks[] variants = [Locks() with { EvaluationId = "synthetic-other" }, Locks() with { ScopeId = "synthetic-other" },
            Locks() with { PopulationDigest = new('d', 64) }, Locks() with { SampleDigest = new('d', 64) }, Locks() with { VersionManifestDigest = new('d', 64) },
            Locks() with { CorrectionCutoffUtc = Locks().CorrectionCutoffUtc.AddTicks(1) }];
        foreach (var locks in variants)
        {
            var changed = Success(Fixture() with { Locks = locks });
            Check(original.CanonicalJson != changed.CanonicalJson && original.ContentDigest != changed.ContentDigest, "each lock change binds new bytes/digest");
            Equal(Hash(changed.CanonicalJson), changed.ContentDigest, "changed canonical digest independently rehashed");
        }
        var fixture = Fixture();
        var changedOutcome = Success(fixture with { Reviews = fixture.Reviews.Select(r => r.MemberId == "synthetic-a" ? r with { Outcome = EvaluationReviewOutcome.Rejected } : r).ToArray() });
        var changedOrigin = Success(fixture with { Reviews = fixture.Reviews.Select(r => r.MemberId == "synthetic-b" ? r with { OriginatingClassification = EvaluationOriginClassification.Confirmed } : r).ToArray() });
        var changedGoal = Success(fixture with { Members = fixture.Members.Select(m => m.Id == "synthetic-e" ? m with { DesiredOutcomeVersion = "synthetic-goal-v2" } : m).ToArray() });
        var changedId = Success(fixture with
        {
            Members = fixture.Members.Select(m => m.Id == "synthetic-a" ? m with { Id = "synthetic-renamed" } : m).ToArray(),
            Reviews = fixture.Reviews.Select(r => r.MemberId == "synthetic-a" ? r with { MemberId = "synthetic-renamed" } : r).ToArray()
        });
        foreach (var changed in new[] { changedOutcome, changedOrigin, changedGoal, changedId })
        {
            Check(original.ContentDigest != changed.ContentDigest && original.CanonicalJson != changed.CanonicalJson, "material input change binds new snapshot");
            Equal(Hash(changed.CanonicalJson), changed.ContentDigest, "changed snapshot hash independently verified");
        }
        Equal(GoldenJson, original.CanonicalJson, "old snapshot unchanged after revised builds");
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "ar-SA", "fr-FR", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                var p = Success(Fixture());
                Equal(GoldenJson, p.CanonicalJson, "culture-invariant timestamp/enums/canonical bytes");
                Equal(GoldenDigest, p.ContentDigest, "culture-invariant digest");
                var ids = new[] { "synthetic-a_", "synthetic-a0", "synthetic-a.", "synthetic-a-" };
                var sorted = Success(new(Locks(), ids.Select(id => Member(id)).ToArray(), ids.Select(id => new EvaluationReview(id, EvaluationReviewOutcome.Unreviewed)).ToArray()));
                Equal("synthetic-a-,synthetic-a.,synthetic-a0,synthetic-a_", string.Join(',', sorted.Members.Select(m => m.Id)), "ordinal punctuation ordering");
                Equal("synthetic-a-,synthetic-a.,synthetic-a0,synthetic-a_", string.Join(',', sorted.Reviews.Select(r => r.MemberId)), "ordinal review punctuation ordering");
            }
        }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    private static void SizeBoundaries()
    {
        var member = Member("synthetic-item");
        var review = new EvaluationReview("synthetic-item", EvaluationReviewOutcome.Unreviewed);
        Denied(new(Locks(), Enumerable.Repeat(member, 100001).ToArray(), []), EvaluationIssue.InvalidInput);
        Denied(new(Locks(), [], Enumerable.Repeat(review, 100001).ToArray()), EvaluationIssue.InvalidInput);
    }

    private static void Counts(EvaluationAccuracy a, int selected, int confirmed, int rejected, int indeterminate, int unreviewed, int corrected, decimal? percentage, bool? threshold)
    {
        Equal(selected, a.Selected, "selected count");
        Equal(confirmed, a.Confirmed, "original confirmed count");
        Equal(rejected, a.Rejected, "original rejected count");
        Equal(indeterminate, a.Indeterminate, "indeterminate excluded count");
        Equal(unreviewed, a.Unreviewed, "unreviewed excluded count");
        Equal(corrected, a.Corrected, "overlapping correction count");
        Equal(confirmed + rejected, a.Denominator, "origin-only denominator");
        Equal(percentage, a.ConfirmedAccuracyPercent, "unrounded exact decimal accuracy");
        Equal(threshold, a.ExceedsGeneralAiThreshold, "strict general-only exact threshold");
        Equal(selected, a.Denominator + a.Indeterminate + a.Unreviewed, "conservation without correction double count");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(reason);
    }
    private static void Equal<T>(T expected, T actual, string reason) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{reason}: expected {expected}, actual {actual}");
}
