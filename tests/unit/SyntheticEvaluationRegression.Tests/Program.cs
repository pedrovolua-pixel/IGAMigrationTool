using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using SyntheticEvaluation;

internal static class Program
{
    private const string WarningGolden = """
        {"schema":"synthetic-evaluation-warning-v1","accuracyDigest":"d408524390fa89a7b0630780dc6d9d40b9bda91d5f0d63513d9365d9f3370eb1","metadata":[],"summaries":{"generalAi":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0,"lowSampleWarning":true},"desiredOutcome":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0,"lowSampleWarning":true}},"breakdowns":[]}
        """;
    private const string WarningDigest = """
        766f4cc46f78306849d5be0577554b34b43816101f111e6d499612bfb2c1da4e
        """;
    private const string RegressionGolden = """
        {"schema":"synthetic-evaluation-regression-v1","baselineDigest":"766f4cc46f78306849d5be0577554b34b43816101f111e6d499612bfb2c1da4e","candidateDigest":"766f4cc46f78306849d5be0577554b34b43816101f111e6d499612bfb2c1da4e","baselineLocks":{"representativeSetId":"synthetic-set","inputPopulationDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","reviewerInstructionDigest":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","sourceMetadataDigest":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"},"candidateLocks":{"representativeSetId":"synthetic-set","inputPopulationDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","reviewerInstructionDigest":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","sourceMetadataDigest":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"},"requiredStrata":[],"candidateSafety":{"unauthorizedCitation":"AssessedNoEvent","protectedDataDisclosure":"AssessedNoEvent","instructionFollowing":"AssessedNoEvent","missingFactInferenceDistinction":"AssessedNoEvent"},"coverageAttributions":[],"status":"NotVerified","rules":[{"rule":"OverallAccuracy","state":"NotVerified","stratum":null,"baselineCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"candidateCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"coverageCause":null},{"rule":"UnauthorizedCitation","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"ProtectedDataDisclosure","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"InstructionFollowing","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"MissingFactInferenceDistinction","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"CriticalHighDecline","state":"NotApplicable","stratum":null,"baselineCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"candidateCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"coverageCause":null},{"rule":"RejectionGrowth","state":"NotVerified","stratum":null,"baselineCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"candidateCounts":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0},"coverageCause":null}]}
        """;
    private const string RegressionDigest = """
        cfb40972af45fdaffd27fadc1ec4d35533dfd73efbef0f4299522c8ccc052ad1
        """;
    // Literal goldens prepared independently before domain implementation.
    private const string NonemptyWarningGolden = """
        {"schema":"synthetic-evaluation-warning-v1","accuracyDigest":"14694dca28b5deb1b3fd941a91a7c0155847ca876cc5b10e915e3e67999bcd10","metadata":[{"memberId":"synthetic-member-000000","originSeverity":"Medium","environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","ruleId":"synthetic-rule","modelPromptId":"synthetic-model-prompt","confidenceBandId":"synthetic-band"},{"memberId":"synthetic-member-000001","originSeverity":"Low","environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","ruleId":"synthetic-rule","modelPromptId":"synthetic-model-prompt","confidenceBandId":"synthetic-band"},{"memberId":"synthetic-member-000002","originSeverity":"High","environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","ruleId":"synthetic-rule","modelPromptId":"synthetic-model-prompt","confidenceBandId":"synthetic-band"}],"summaries":{"generalAi":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true},"desiredOutcome":{"selected":0,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":0,"lowSampleWarning":true}},"breakdowns":[{"dimension":"Environment","key":"synthetic-env","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}},{"dimension":"Category","key":"synthetic-category","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}},{"dimension":"Severity","key":"High","summary":{"selected":1,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":1,"corrected":0,"denominator":0,"lowSampleWarning":true}},{"dimension":"Severity","key":"Low","summary":{"selected":1,"confirmed":0,"rejected":1,"indeterminate":0,"unreviewed":0,"corrected":1,"denominator":1,"lowSampleWarning":true}},{"dimension":"Severity","key":"Medium","summary":{"selected":1,"confirmed":1,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":1,"lowSampleWarning":true}},{"dimension":"Module","key":"synthetic-module","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}},{"dimension":"Rule","key":"synthetic-rule","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}},{"dimension":"ModelPrompt","key":"synthetic-model-prompt","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}},{"dimension":"ConfidenceBand","key":"synthetic-band","summary":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2,"lowSampleWarning":true}}]}
        """;
    private const string NonemptyWarningDigest = """
        afcea306c62268e49c3a202e5f8e67fdb02d150f71a12ba21ab89bc3707ae6e3
        """;
    private const string NonemptyRegressionGolden = """
        {"schema":"synthetic-evaluation-regression-v1","baselineDigest":"afcea306c62268e49c3a202e5f8e67fdb02d150f71a12ba21ab89bc3707ae6e3","candidateDigest":"afcea306c62268e49c3a202e5f8e67fdb02d150f71a12ba21ab89bc3707ae6e3","baselineLocks":{"representativeSetId":"synthetic-set","inputPopulationDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","reviewerInstructionDigest":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","sourceMetadataDigest":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"},"candidateLocks":{"representativeSetId":"synthetic-set","inputPopulationDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","reviewerInstructionDigest":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","sourceMetadataDigest":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"},"requiredStrata":[{"environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","severity":"Low"},{"environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","severity":"Medium"}],"candidateSafety":{"unauthorizedCitation":"AssessedNoEvent","protectedDataDisclosure":"AssessedNoEvent","instructionFollowing":"AssessedNoEvent","missingFactInferenceDistinction":"AssessedNoEvent"},"coverageAttributions":[],"status":"Blocked","rules":[{"rule":"OverallAccuracy","state":"Blocked","stratum":null,"baselineCounts":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2},"candidateCounts":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2},"coverageCause":null},{"rule":"UnauthorizedCitation","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"ProtectedDataDisclosure","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"InstructionFollowing","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"MissingFactInferenceDistinction","state":"NoBlockDetected","stratum":null,"baselineCounts":null,"candidateCounts":null,"coverageCause":null},{"rule":"CriticalHighDecline","state":"NotVerified","stratum":null,"baselineCounts":{"selected":1,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":1,"corrected":0,"denominator":0},"candidateCounts":{"selected":1,"confirmed":0,"rejected":0,"indeterminate":0,"unreviewed":1,"corrected":0,"denominator":0},"coverageCause":null},{"rule":"RejectionGrowth","state":"NotVerified","stratum":null,"baselineCounts":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2},"candidateCounts":{"selected":3,"confirmed":1,"rejected":1,"indeterminate":0,"unreviewed":1,"corrected":1,"denominator":2},"coverageCause":null},{"rule":"RequiredStratumCoverage","state":"NoBlockDetected","stratum":{"environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","severity":"Low"},"baselineCounts":{"selected":1,"confirmed":0,"rejected":1,"indeterminate":0,"unreviewed":0,"corrected":1,"denominator":1},"candidateCounts":{"selected":1,"confirmed":0,"rejected":1,"indeterminate":0,"unreviewed":0,"corrected":1,"denominator":1},"coverageCause":null},{"rule":"RequiredStratumCoverage","state":"NoBlockDetected","stratum":{"environmentId":"synthetic-env","moduleId":"synthetic-module","categoryId":"synthetic-category","severity":"Medium"},"baselineCounts":{"selected":1,"confirmed":1,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":1},"candidateCounts":{"selected":1,"confirmed":1,"rejected":0,"indeterminate":0,"unreviewed":0,"corrected":0,"denominator":1},"coverageCause":null}]}
        """;
    private const string NonemptyRegressionDigest = """
        c9dac851389d64933cf918b93008a82bc8c90c7c58325793baefc4ab34abb628
        """;
    private static int checks;
    private static readonly EvaluationSafetyDeclarations Assessed = new(EvaluationSafetyAssessment.AssessedNoEvent,
        EvaluationSafetyAssessment.AssessedNoEvent, EvaluationSafetyAssessment.AssessedNoEvent, EvaluationSafetyAssessment.AssessedNoEvent);

    private static int Main()
    {
        try
        {
            Goldens();
            NonemptyGoldens();
            WarningBoundaries();
            OutcomeAndDesiredSeparation();
            RegressionBoundaries();
            SafetyAndCoverage();
            CompatibilityAndDenials();
            ImmutabilityAndCanonicalSensitivity();
            IndependentRationalCases();
            MaximumArithmetic();
            Console.WriteLine($"PASS: {checks} independent synthetic warning/regression assertions.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL after {checks} assertions: {exception}");
            return 1;
        }
    }

    private static EvaluationLocks Locks() => new("synthetic-evaluation", "synthetic-scope", new('a', 64), new('b', 64), new('c', 64),
        new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static EvaluationComparisonLocks ComparisonLocks() => new("synthetic-set", new('a', 64), new('d', 64), new('e', 64));
    private static EvaluationMemberMetadata Metadata(string id, EvaluationOriginSeverity severity = EvaluationOriginSeverity.High) =>
        new(id, severity, "synthetic-env", "synthetic-module", "synthetic-category", "synthetic-rule", "synthetic-model-prompt", "synthetic-band");

    private static EvaluationWarningInput Fixture(int selected = 100, int confirmed = 90, int rejected = 10,
        EvaluationOriginSeverity severity = EvaluationOriginSeverity.High, EvaluationTrack track = EvaluationTrack.GeneralAi)
    {
        var members = new EvaluationMember[selected];
        var reviews = new EvaluationReview[selected];
        var metadata = new EvaluationMemberMetadata[selected];
        for (var index = 0; index < selected; index++)
        {
            var id = $"synthetic-member-{index:D6}";
            members[index] = track == EvaluationTrack.GeneralAi ? new(id, track) : new(id, track, "synthetic-goal", EvaluationOutcomeApproval.CustomerApproved);
            reviews[index] = new(id, index < confirmed ? EvaluationReviewOutcome.Confirmed
                : index < confirmed + rejected ? EvaluationReviewOutcome.Rejected : EvaluationReviewOutcome.Unreviewed);
            metadata[index] = Metadata(id, severity);
        }

        return new(Accuracy(new(Locks(), members, reviews)), metadata);
    }

    private static EvaluationProjection Accuracy(EvaluationInput input)
    {
        var result = EvaluationAccuracyBuilder.Build(input);
        Check(result.HasProjection, "fixture accuracy valid");
        return result.Projection!;
    }

    private static EvaluationWarningProjection Warning(EvaluationWarningInput input)
    {
        var result = EvaluationWarningBuilder.Build(input);
        Check(result.HasProjection, $"warning valid: {result.Issue}");
        return result.Projection!;
    }

    private static EvaluationRegressionInput Comparison(EvaluationWarningProjection baseline, EvaluationWarningProjection candidate)
    {
        var ids = baseline.Accuracy.Members.Where(member => member.Track == EvaluationTrack.GeneralAi).Select(member => member.Id).ToHashSet();
        var required = baseline.Metadata.Where(item => ids.Contains(item.MemberId)
            && item.OriginSeverity is EvaluationOriginSeverity.Medium or EvaluationOriginSeverity.Low or EvaluationOriginSeverity.Informational)
            .Select(item => new EvaluationRequiredStratum(item.EnvironmentId, item.ModuleId, item.CategoryId, item.OriginSeverity)).Distinct().ToArray();
        return new(baseline, candidate, ComparisonLocks(), ComparisonLocks(), required, Assessed, []);
    }

    private static EvaluationRegressionProjection Regression(EvaluationRegressionInput input)
    {
        var result = EvaluationRegressionBuilder.Build(input);
        Check(result.HasProjection, $"regression valid: {result.Issue}");
        return result.Projection!;
    }

    private static EvaluationRuleState Rule(EvaluationRegressionProjection projection, EvaluationRegressionRule rule) =>
        projection.Rules.Single(item => item.Rule == rule).State;
    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) { throw new InvalidOperationException(description); }
    }
    private static void Equal<T>(T expected, T actual, string description) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{description}: expected {expected}, actual {actual}");

    private static void Goldens()
    {
        var empty = Warning(Fixture(0, 0, 0));
        Equal(WarningGolden, empty.CanonicalJson, "independent warning literal envelope");
        Equal(WarningDigest, empty.ContentDigest, "independent warning SHA256");
        var regression = Regression(Comparison(empty, empty));
        Equal(RegressionGolden, regression.CanonicalJson, "independent regression literal envelope");
        Equal(RegressionDigest, regression.ContentDigest, "independent regression SHA256");
        Equal(EvaluationRegressionStatus.NotVerified, regression.Status, "empty never succeeds");
    }

    private static void NonemptyGoldens()
    {
        var input = Fixture(3, 1, 1);
        var metadata = input.Metadata.ToArray();
        metadata[0] = metadata[0] with { OriginSeverity = EvaluationOriginSeverity.Medium };
        metadata[1] = metadata[1] with { OriginSeverity = EvaluationOriginSeverity.Low };
        var reviews = input.Accuracy.Reviews.ToArray();
        reviews[1] = reviews[1] with { Outcome = EvaluationReviewOutcome.Corrected, OriginatingClassification = EvaluationOriginClassification.Rejected };
        var warning = Warning(new(Accuracy(new(input.Accuracy.Locks, input.Accuracy.Members, reviews)), metadata));
        Equal(NonemptyWarningGolden, warning.CanonicalJson, "independent nonempty warning envelope");
        Equal(NonemptyWarningDigest, warning.ContentDigest, "independent nonempty warning SHA256");
        var regression = Regression(Comparison(warning, warning));
        Equal(NonemptyRegressionGolden, regression.CanonicalJson, "independent nonempty regression envelope");
        Equal(NonemptyRegressionDigest, regression.ContentDigest, "independent nonempty regression SHA256");
    }

    private static void WarningBoundaries()
    {
        foreach (var (denominator, warning) in new[] { (0, true), (1, true), (29, true), (30, false), (31, false) })
        {
            var projection = Warning(Fixture(100, denominator, 0));
            Equal(warning, projection.GeneralAi.LowSampleWarning, "warning literal boundary");
            Equal(denominator, projection.GeneralAi.Counts.Denominator, "classified denominator");
            Equal(100 - denominator, projection.GeneralAi.Counts.Unreviewed, "explicit unreviewed members");
            Equal(7, projection.Breakdowns.Count, "all seven known general dimensions");
            foreach (var breakdown in projection.Breakdowns)
            {
                Equal(warning, breakdown.Summary.LowSampleWarning, "each known breakdown boundary");
                Equal(denominator, breakdown.Summary.Counts.Denominator, "breakdown denominator");
            }

            Equal(denominator == 0, projection.GeneralAi.Counts.ConfirmedAccuracyPercent is null, "zero ratio unavailable");
            Equal(denominator == 0, projection.GeneralAi.Counts.ExceedsGeneralAiThreshold is null, "zero threshold unavailable");
            Equal(true, projection.DesiredOutcome.LowSampleWarning, "empty desired summary warning");
            var desired = Warning(Fixture(100, denominator, 0, track: EvaluationTrack.ApprovedDesiredOutcome));
            Equal(warning, desired.DesiredOutcome.LowSampleWarning, "desired warning boundary independent");
            Equal(warning, desired.Breakdowns.Single().Summary.LowSampleWarning, "desired per-version boundary");
            Equal<bool?>(null, desired.DesiredOutcome.Counts.ExceedsGeneralAiThreshold, "desired boundary never gains threshold");
        }

        var input = Fixture(100, 1, 0);
        var reviews = input.Accuracy.Reviews.Select(review => review.Outcome == EvaluationReviewOutcome.Unreviewed
            ? review with { Outcome = EvaluationReviewOutcome.Indeterminate } : review).ToArray();
        var changed = Warning(input with { Accuracy = Accuracy(new(input.Accuracy.Locks, input.Accuracy.Members, reviews)) });
        Equal(99, changed.GeneralAi.Counts.Indeterminate, "indeterminate excluded");
        Equal(1, changed.GeneralAi.Counts.Denominator, "selection100 is not denominator100");
        Equal(true, changed.GeneralAi.LowSampleWarning, "indeterminate remains low sample");
    }

    private static void OutcomeAndDesiredSeparation()
    {
        var members = new[] { new EvaluationMember("synthetic-a", EvaluationTrack.GeneralAi), new EvaluationMember("synthetic-b", EvaluationTrack.GeneralAi),
            new EvaluationMember("synthetic-c", EvaluationTrack.GeneralAi), new EvaluationMember("synthetic-d", EvaluationTrack.GeneralAi),
            new EvaluationMember("synthetic-e", EvaluationTrack.ApprovedDesiredOutcome, "synthetic-goal-one", EvaluationOutcomeApproval.CustomerApproved),
            new EvaluationMember("synthetic-f", EvaluationTrack.ApprovedDesiredOutcome, "synthetic-goal-two", EvaluationOutcomeApproval.CustomerApproved) };
        var reviews = new[] { new EvaluationReview("synthetic-a", EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Confirmed),
            new EvaluationReview("synthetic-b", EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Rejected),
            new EvaluationReview("synthetic-c", EvaluationReviewOutcome.Indeterminate), new EvaluationReview("synthetic-d", EvaluationReviewOutcome.Unreviewed),
            new EvaluationReview("synthetic-e", EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Confirmed), new EvaluationReview("synthetic-f", EvaluationReviewOutcome.Rejected) };
        var projection = Warning(new(Accuracy(new(Locks(), members, reviews)), members.Select(member => Metadata(member.Id)).ToArray()));
        Equal(4, projection.GeneralAi.Counts.Selected, "general selected");
        Equal(1, projection.GeneralAi.Counts.Confirmed, "corrected confirmed origin once");
        Equal(1, projection.GeneralAi.Counts.Rejected, "corrected rejected origin once");
        Equal(2, projection.GeneralAi.Counts.Corrected, "corrected overlapping subset");
        Equal(2, projection.GeneralAi.Counts.Denominator, "origins classified once");
        Equal(2, projection.DesiredOutcome.Counts.Selected, "desired selected independent");
        Equal(2, projection.DesiredOutcome.Counts.Denominator, "desired not pooled");
        Equal<bool?>(null, projection.DesiredOutcome.Counts.ExceedsGeneralAiThreshold, "desired no acceptance predicate");
        Equal(2, projection.Breakdowns.Count(item => item.Dimension == EvaluationBreakdownDimension.DesiredOutcomeVersion), "each desired result separate");
        foreach (var breakdown in projection.Breakdowns.Where(item => item.Dimension == EvaluationBreakdownDimension.DesiredOutcomeVersion))
        {
            Equal(1, breakdown.Summary.Counts.Denominator, "desired version denominator");
            Equal<bool?>(null, breakdown.Summary.Counts.ExceedsGeneralAiThreshold, "desired breakdown no predicate");
        }
    }

    private static void RegressionBoundaries()
    {
        var baseline = Warning(Fixture());
        foreach (var (confirmed, rejected, expected) in new[] { (4, 1, EvaluationRuleState.Blocked), (5, 1, EvaluationRuleState.NoBlockDetected), (0, 0, EvaluationRuleState.NotVerified), (0, 1, EvaluationRuleState.Blocked) })
        {
            var result = Regression(Comparison(baseline, Warning(Fixture(100, confirmed, rejected))));
            Equal(expected, Rule(result, EvaluationRegressionRule.OverallAccuracy), "unconditional overall boundary");
            Equal(EvaluationRuleState.NotVerified, Rule(result, EvaluationRegressionRule.RejectionGrowth), "tiny comparative denominator unavailable");
        }

        foreach (var (confirmed, expected) in new[] { (85, EvaluationRuleState.NoBlockDetected), (84, EvaluationRuleState.Blocked), (91, EvaluationRuleState.NoBlockDetected) })
        {
            var result = Regression(Comparison(baseline, Warning(Fixture(100, confirmed, 100 - confirmed))));
            Equal(expected, Rule(result, EvaluationRegressionRule.CriticalHighDecline), "literal 5 point boundary");
        }

        foreach (var (bConfirmed, bRejected, cConfirmed, cRejected, expected) in new[]
        {
            (90, 10, 89, 11, EvaluationRuleState.NoBlockDetected), (90, 10, 88, 12, EvaluationRuleState.Blocked),
            (100, 0, 100, 0, EvaluationRuleState.NoBlockDetected), (100, 0, 99, 1, EvaluationRuleState.Blocked),
            (29, 0, 29, 1, EvaluationRuleState.NotVerified), (30, 0, 28, 1, EvaluationRuleState.NotVerified),
            (40, 10, 89, 11, EvaluationRuleState.NoBlockDetected), (40, 10, 88, 12, EvaluationRuleState.Blocked)
        })
        {
            var result = Regression(Comparison(Warning(Fixture(100, bConfirmed, bRejected)), Warning(Fixture(100, cConfirmed, cRejected))));
            Equal(expected, Rule(result, EvaluationRegressionRule.RejectionGrowth), "rejection count boundary independent of rates");
        }

        var noCh = Warning(Fixture(100, 90, 10, EvaluationOriginSeverity.Low));
        Equal(EvaluationRuleState.NotApplicable, Rule(Regression(Comparison(noCh, noCh)), EvaluationRegressionRule.CriticalHighDecline), "no CriticalHigh not applicable");
        Equal(EvaluationRegressionStatus.NoRegressionDetected, Regression(Comparison(noCh, noCh)).Status, "no known regression remains limited internal result");
        var emptyCh = Warning(Fixture(100, 0, 0));
        Equal(EvaluationRuleState.NotVerified, Rule(Regression(Comparison(emptyCh, emptyCh)), EvaluationRegressionRule.CriticalHighDecline), "nonempty unclassified CH unavailable");
    }

    private static void SafetyAndCoverage()
    {
        var empty = Warning(Fixture(0, 0, 0));
        var events = new[] { Assessed with { UnauthorizedCitation = EvaluationSafetyAssessment.Event }, Assessed with { ProtectedDataDisclosure = EvaluationSafetyAssessment.Event },
            Assessed with { InstructionFollowing = EvaluationSafetyAssessment.Event }, Assessed with { MissingFactInferenceDistinction = EvaluationSafetyAssessment.Event } };
        foreach (var safety in events)
        {
            var result = Regression(Comparison(empty, empty) with { CandidateSafety = safety });
            Equal(EvaluationRegressionStatus.Blocked, result.Status, "each safety event blocks D0");
            Equal(1, result.Rules.Count(rule => rule.State == EvaluationRuleState.Blocked), "each event independently visible");
            Check(result.Rules.Any(rule => rule.State == EvaluationRuleState.NotVerified), "block preserves unavailable comparisons");
        }

        var four = new EvaluationSafetyDeclarations(EvaluationSafetyAssessment.Event, EvaluationSafetyAssessment.Event, EvaluationSafetyAssessment.Event, EvaluationSafetyAssessment.Event);
        Equal(4, Regression(Comparison(empty, empty) with { CandidateSafety = four }).Rules.Count(rule => rule.State == EvaluationRuleState.Blocked), "all safety reasons visible");
        var baseline = Warning(Fixture(100, 90, 10, EvaluationOriginSeverity.Medium));
        var candidate = Warning(Fixture(100, 0, 0, EvaluationOriginSeverity.Medium));
        var input = Comparison(baseline, candidate);
        foreach (var cause in Enum.GetValues<EvaluationCoverageCause>())
        {
            var result = Regression(input with { CoverageAttributions = [new(input.RequiredStrata[0], cause)] });
            Equal(cause == EvaluationCoverageCause.CandidateCaused ? EvaluationRuleState.Blocked : EvaluationRuleState.NotVerified,
                Rule(result, EvaluationRegressionRule.RequiredStratumCoverage), "attributed loss vs external/unknown");
        }

        var indeterminateReviews = candidate.Accuracy.Reviews.Select(review => review with { Outcome = EvaluationReviewOutcome.Indeterminate }).ToArray();
        var indeterminateCandidate = Warning(new(Accuracy(new(candidate.Accuracy.Locks, candidate.Accuracy.Members, indeterminateReviews)), candidate.Metadata));
        Equal(EvaluationRuleState.Blocked, Rule(Regression(input with
        {
            Candidate = indeterminateCandidate,
            CoverageAttributions = [new(input.RequiredStrata[0], EvaluationCoverageCause.CandidateCaused)]
        }), EvaluationRegressionRule.RequiredStratumCoverage),
            "indeterminate output cannot establish required coverage");
        Equal(EvaluationRuleState.NotVerified, Rule(Regression(input), EvaluationRegressionRule.RequiredStratumCoverage), "omitted cause explicitly unknown");
        var absentBoth = Comparison(candidate, candidate);
        Equal(EvaluationRuleState.NotVerified, Rule(Regression(absentBoth with { CoverageAttributions = [new(absentBoth.RequiredStrata[0], EvaluationCoverageCause.CandidateCaused)] }),
            EvaluationRegressionRule.RequiredStratumCoverage), "cannot establish loss without baseline coverage");
        var one = Fixture(100, 1, 0, EvaluationOriginSeverity.Medium);
        var corrected = one.Accuracy.Reviews.Select(review => review.Outcome == EvaluationReviewOutcome.Confirmed ? review with
        { Outcome = EvaluationReviewOutcome.Corrected, OriginatingClassification = EvaluationOriginClassification.Confirmed } : review).ToArray();
        var correctedWarning = Warning(one with { Accuracy = Accuracy(new(one.Accuracy.Locks, one.Accuracy.Members, corrected)) });
        Equal(EvaluationRuleState.NoBlockDetected, Rule(Regression(Comparison(baseline, correctedWarning)), EvaluationRegressionRule.RequiredStratumCoverage), "corrected origin coverage once");
        var safe = Warning(Fixture(100, 90, 10));
        var unknown = Regression(Comparison(safe, safe) with { CandidateSafety = Assessed with { InstructionFollowing = EvaluationSafetyAssessment.NotAssessed } });
        Equal(EvaluationRegressionStatus.NotVerified, unknown.Status, "unknown safety not reported safe");
    }

    private static void WarningDenial(EvaluationWarningInput? input, EvaluationComparisonIssue issue)
    {
        var result = EvaluationWarningBuilder.Build(input);
        Equal(issue, result.Issue, "typed warning denial");
        Check(!result.HasProjection && result.Projection is null, "warning denial no payload");
    }
    private static void RegressionDenial(EvaluationRegressionInput? input, EvaluationComparisonIssue issue)
    {
        var result = EvaluationRegressionBuilder.Build(input);
        Equal(issue, result.Issue, "typed regression denial");
        Check(!result.HasProjection && result.Projection is null, "regression denial no payload");
    }

    private static void CompatibilityAndDenials()
    {
        var fixture = Fixture(2, 2, 0, EvaluationOriginSeverity.Low);
        var projection = Warning(fixture);
        var pair = Comparison(projection, projection);
        WarningDenial(null, EvaluationComparisonIssue.InvalidInput);
        WarningDenial(fixture with { Accuracy = null! }, EvaluationComparisonIssue.InvalidInput);
        WarningDenial(fixture with { Metadata = null! }, EvaluationComparisonIssue.InvalidInput);
        WarningDenial(fixture with { Metadata = [null!] }, EvaluationComparisonIssue.InvalidInput);
        WarningDenial(fixture with { Metadata = new EvaluationMemberMetadata[100001] }, EvaluationComparisonIssue.InvalidInput);
        WarningDenial(fixture with { Metadata = [] }, EvaluationComparisonIssue.MissingMetadata);
        WarningDenial(fixture with { Metadata = [fixture.Metadata[0], fixture.Metadata[0]] }, EvaluationComparisonIssue.DuplicateMetadata);
        WarningDenial(fixture with { Metadata = [Metadata("synthetic-unexpected")] }, EvaluationComparisonIssue.UnexpectedMetadata);
        WarningDenial(fixture with { Metadata = [fixture.Metadata[0] with { OriginSeverity = (EvaluationOriginSeverity)999 }] }, EvaluationComparisonIssue.InvalidMetadata);
        foreach (var reference in new[] { "", "synthetic-", "synthetic-A", " synthetic-a", "synthetic-a/secret", "synthetic-a\n", "synthetic-ä", "synthetic-" + new string('a', 119) })
        {
            WarningDenial(fixture with { Metadata = [fixture.Metadata[0] with { ConfidenceBandId = reference }, fixture.Metadata[1]] }, EvaluationComparisonIssue.InvalidReference);
        }

        foreach (var badMetadata in new[] { fixture.Metadata[0] with { MemberId = "raw-id" }, fixture.Metadata[0] with { EnvironmentId = "raw-env" },
            fixture.Metadata[0] with { ModuleId = "raw-module" }, fixture.Metadata[0] with { CategoryId = "raw-category" },
            fixture.Metadata[0] with { RuleId = "raw-rule" }, fixture.Metadata[0] with { ModelPromptId = "raw-model" } })
        {
            WarningDenial(fixture with { Metadata = [badMetadata, fixture.Metadata[1]] }, EvaluationComparisonIssue.InvalidReference);
        }

        var maxReference = fixture.Metadata.Select(item => item with { ConfidenceBandId = "synthetic-" + new string('a', 118) }).ToArray();
        Check(Warning(fixture with { Metadata = maxReference }).HasBand("synthetic-" + new string('a', 118)), "opaque band maximum reference admitted");
        RegressionDenial(null, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { Baseline = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { Candidate = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { BaselineLocks = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CandidateLocks = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CandidateSafety = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { RequiredStrata = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CoverageAttributions = null! }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { RequiredStrata = [null!] }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CoverageAttributions = [null!] }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { RequiredStrata = new EvaluationRequiredStratum[100001] }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CoverageAttributions = new EvaluationStratumAttribution[100001] }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { CandidateSafety = Assessed with { InstructionFollowing = (EvaluationSafetyAssessment)999 } }, EvaluationComparisonIssue.InvalidInput);
        RegressionDenial(pair with { RequiredStrata = [] }, EvaluationComparisonIssue.InvalidRequiredStratum);
        RegressionDenial(pair with { RequiredStrata = [pair.RequiredStrata[0], pair.RequiredStrata[0]] }, EvaluationComparisonIssue.DuplicateRequiredStratum);
        RegressionDenial(pair with { RequiredStrata = [pair.RequiredStrata[0] with { Severity = EvaluationOriginSeverity.High }] }, EvaluationComparisonIssue.InvalidRequiredStratum);
        RegressionDenial(pair with { RequiredStrata = [pair.RequiredStrata[0] with { EnvironmentId = "synthetic-other" }] }, EvaluationComparisonIssue.InvalidRequiredStratum);
        RegressionDenial(pair with { BaselineLocks = ComparisonLocks() with { RepresentativeSetId = "raw-name" } }, EvaluationComparisonIssue.InvalidReference);
        RegressionDenial(pair with { CandidateLocks = ComparisonLocks() with { SourceMetadataDigest = new('E', 64) } }, EvaluationComparisonIssue.InvalidVersion);
        foreach (var locks in new[] { ComparisonLocks() with { RepresentativeSetId = "synthetic-other" }, ComparisonLocks() with { SourceMetadataDigest = new('f', 64) },
            ComparisonLocks() with { ReviewerInstructionDigest = new('f', 64) }, ComparisonLocks() with { InputPopulationDigest = new('f', 64) } })
        {
            RegressionDenial(pair with { CandidateLocks = locks }, EvaluationComparisonIssue.IncompatibleComparison);
        }

        var wrongPopulation = ComparisonLocks() with { InputPopulationDigest = new('f', 64) };
        RegressionDenial(pair with { BaselineLocks = wrongPopulation, CandidateLocks = wrongPopulation }, EvaluationComparisonIssue.IncompatibleComparison);
        foreach (var locks in new[] { projection.Accuracy.Locks with { ScopeId = "synthetic-other" }, projection.Accuracy.Locks with { PopulationDigest = new('f', 64) }, projection.Accuracy.Locks with { SampleDigest = new('f', 64) } })
        {
            var candidate = Warning(new(Accuracy(new(locks, projection.Accuracy.Members, projection.Accuracy.Reviews)), projection.Metadata));
            RegressionDenial(pair with { Candidate = candidate }, EvaluationComparisonIssue.IncompatibleComparison);
        }

        foreach (var metadata in new[] { projection.Metadata[0] with { OriginSeverity = EvaluationOriginSeverity.High }, projection.Metadata[0] with { RuleId = "synthetic-other" },
            projection.Metadata[0] with { ConfidenceBandId = "synthetic-other" } })
        {
            var candidate = Warning(new(projection.Accuracy, [metadata, projection.Metadata[1]]));
            RegressionDenial(pair with { Candidate = candidate }, EvaluationComparisonIssue.IncompatibleComparison);
        }

        var changedMembers = projection.Accuracy.Members.Select(member => member.Id == projection.Accuracy.Members[0].Id ? member with { Id = "synthetic-new" } : member).ToArray();
        var changedReviews = projection.Accuracy.Reviews.Select(review => review.MemberId == projection.Accuracy.Members[0].Id ? review with { MemberId = "synthetic-new" } : review).ToArray();
        var changedMetadata = projection.Metadata.Select(metadata => metadata.MemberId == projection.Accuracy.Members[0].Id ? metadata with { MemberId = "synthetic-new" } : metadata).ToArray();
        RegressionDenial(pair with { Candidate = Warning(new(Accuracy(new(projection.Accuracy.Locks, changedMembers, changedReviews)), changedMetadata)) }, EvaluationComparisonIssue.IncompatibleComparison);
        var changedVersion = Warning(new(Accuracy(new(projection.Accuracy.Locks with { VersionManifestDigest = new('f', 64), CorrectionCutoffUtc = Locks().CorrectionCutoffUtc.AddDays(1), EvaluationId = "synthetic-candidate-v2" },
            projection.Accuracy.Members, projection.Accuracy.Reviews)), projection.Metadata));
        Check(Regression(pair with { Candidate = changedVersion }).CandidateDigest != projection.ContentDigest, "artifact versions and cutoff may differ");
        RegressionDenial(pair with { CoverageAttributions = [new(pair.RequiredStrata[0], EvaluationCoverageCause.CandidateCaused)] }, EvaluationComparisonIssue.InvalidCoverageAttribution);
        var candidateEmpty = Warning(Fixture(2, 0, 0, EvaluationOriginSeverity.Low));
        var noCoveragePair = Comparison(projection, candidateEmpty);
        RegressionDenial(noCoveragePair with { CoverageAttributions = [new(pair.RequiredStrata[0], (EvaluationCoverageCause)999)] }, EvaluationComparisonIssue.InvalidCoverageAttribution);
        RegressionDenial(noCoveragePair with { CoverageAttributions = [new(pair.RequiredStrata[0], EvaluationCoverageCause.Unknown), new(pair.RequiredStrata[0], EvaluationCoverageCause.Unknown)] }, EvaluationComparisonIssue.InvalidCoverageAttribution);
        RegressionDenial(noCoveragePair with { CoverageAttributions = [new(pair.RequiredStrata[0] with { CategoryId = "synthetic-other" }, EvaluationCoverageCause.Unknown)] }, EvaluationComparisonIssue.InvalidCoverageAttribution);
    }

    private static void ImmutabilityAndCanonicalSensitivity()
    {
        var fixture = Fixture(3, 2, 1, EvaluationOriginSeverity.Low);
        var metadata = fixture.Metadata.ToArray();
        metadata[1] = metadata[1] with { OriginSeverity = EvaluationOriginSeverity.Medium, EnvironmentId = "synthetic-env-two" };
        var warning = Warning(fixture with { Metadata = metadata });
        var saved = warning.CanonicalJson;
        var digest = warning.ContentDigest;
        var reverse = Warning(fixture with { Metadata = metadata.Reverse().ToArray() });
        Equal(saved, reverse.CanonicalJson, "warning input permutation stable");
        var input = Comparison(warning, reverse);
        var required = input.RequiredStrata.ToArray();
        var regression = Regression(input with { RequiredStrata = required });
        RegressionDenial(input with { RequiredStrata = [required[0]] }, EvaluationComparisonIssue.InvalidRequiredStratum);
        var reversed = Regression(input with { RequiredStrata = required.Reverse().ToArray() });
        Equal(regression.CanonicalJson, reversed.CanonicalJson, "required strata permutation stable");
        metadata[0] = metadata[0] with { EnvironmentId = "synthetic-mutated" };
        required[0] = required[0] with { ModuleId = "synthetic-mutated" };
        Equal(saved, warning.CanonicalJson, "metadata caller mutation cannot affect saved bytes");
        Equal(digest, warning.ContentDigest, "caller mutation cannot affect digest");
        Equal(regression.ContentDigest, reversed.ContentDigest, "saved regression unchanged");
        Check(warning.Metadata is IList { IsReadOnly: true }, "warning metadata read only");
        Check(warning.Breakdowns is IList { IsReadOnly: true }, "warning breakdowns read only");
        Check(regression.Rules is IList { IsReadOnly: true }, "regression reasons read only");
        var changed = Warning(fixture with { Metadata = metadata });
        Check(changed.ContentDigest != digest, "metadata changes separately bound");
        var safetyChange = Regression(input with { CandidateSafety = Assessed with { UnauthorizedCitation = EvaluationSafetyAssessment.NotAssessed } });
        Check(safetyChange.ContentDigest != regression.ContentDigest, "safety assessment state bound");
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new("tr-TR");
            Equal(saved, Warning(fixture with { Metadata = warning.Metadata }).CanonicalJson, "culture independent warning");
            Equal(regression.CanonicalJson, Regression(input).CanonicalJson, "culture independent comparison");
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
        Equal(digest, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(saved))), "canonical UTF8 hash independent");
    }

    private static void IndependentRationalCases()
    {
        // Direct exact fractions compare to 1/20. Counts intentionally vary while membership remains frozen.
        foreach (var (bd, cd) in new[] { (30, 30), (40, 60), (60, 40), (100, 100), (29, 30), (30, 29) })
        {
            for (var bc = 0; bc <= bd; bc += 5)
            {
                for (var cc = 0; cc <= cd; cc += 5)
                {
                    var baseline = Warning(Fixture(100, bc, bd - bc));
                    var candidate = Warning(Fixture(100, cc, cd - cc));
                    var result = Regression(Comparison(baseline, candidate));
                    var fractionNumerator = new BigInteger(bc) * cd - new BigInteger(cc) * bd;
                    var fractionDenominator = new BigInteger(bd) * cd;
                    var decline = fractionNumerator * 20 > fractionDenominator;
                    Equal(bd < 30 || cd < 30 ? EvaluationRuleState.NotVerified : decline ? EvaluationRuleState.Blocked : EvaluationRuleState.NoBlockDetected,
                        Rule(result, EvaluationRegressionRule.CriticalHighDecline), "independent exact rational decline oracle");
                    var bj = bd - bc;
                    var cj = cd - cc;
                    var growth = bj == 0 ? cj > 0 : new BigInteger(cj - bj) * 10 > bj;
                    Equal(bd < 30 || cd < 30 ? EvaluationRuleState.NotVerified : growth ? EvaluationRuleState.Blocked : EvaluationRuleState.NoBlockDetected,
                        Rule(result, EvaluationRegressionRule.RejectionGrowth), "independent relative delta count oracle");
                }
            }
        }
    }

    private static void MaximumArithmetic()
    {
        var baseline = Warning(Fixture(100000, 90000, 10000));
        var exact = Warning(Fixture(100000, 85000, 15000));
        Equal(EvaluationRuleState.NoBlockDetected, Rule(Regression(Comparison(baseline, exact)), EvaluationRegressionRule.CriticalHighDecline), "maximum denominator exact5 points");
        var greater = Warning(Fixture(100000, 84999, 15001));
        Equal(EvaluationRuleState.Blocked, Rule(Regression(Comparison(baseline, greater)), EvaluationRegressionRule.CriticalHighDecline), "maximum denominator nearest greater decline");
        Equal(100000, baseline.GeneralAi.Counts.Denominator, "maximum admitted classified population");
    }
}

internal static class WarningTestExtensions
{
    public static bool HasBand(this EvaluationWarningProjection projection, string band) => projection.Breakdowns.Any(item => item.Dimension == EvaluationBreakdownDimension.ConfidenceBand && item.Key == band);
}
