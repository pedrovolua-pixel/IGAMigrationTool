using CollectorSafety;

internal static class FieldMinimizerChecks
{
    public static int Run()
    {
        var count = 0;
        var key = new FieldKey("authorization", "role-reference");
        var policy = new FieldPolicySnapshot("policy", "1", new string('a', 64),
            new HashSet<FieldKey> { key }, new HashSet<string>());
        Check("explicit include", policy, new FieldCandidate(key, FieldClassification.ApprovedReference, "synthetic-value"),
            FieldDisposition.Included, "synthetic-value");
        Check("allowed null marker", policy, new FieldCandidate(key, FieldClassification.ApprovedReference, null),
            FieldDisposition.Included, null);
        Check("unlisted field", policy, new FieldCandidate(new FieldKey("authorization", "profile"),
            FieldClassification.ApprovedReference, "must-not-stage"), FieldDisposition.Excluded, null);
        Check("prohibited overrides policy", policy, new FieldCandidate(key, FieldClassification.Prohibited, "must-not-stage"),
            FieldDisposition.Prohibited, null);
        Check("redacted overrides policy", policy, new FieldCandidate(key, FieldClassification.Redacted, "must-not-stage"),
            FieldDisposition.Redacted, null);
        Check("unknown classification", policy, new FieldCandidate(key, FieldClassification.Unknown, "must-not-stage"),
            FieldDisposition.Unclassified, null);
        Check("unrecognized classification", policy, new FieldCandidate(key, (FieldClassification)999, "must-not-stage"),
            FieldDisposition.Unclassified, null);
        Check("category excluded", policy with { ExcludedCategories = new HashSet<string> { "authorization" } },
            new FieldCandidate(key, FieldClassification.ApprovedReference, "must-not-stage"),
            FieldDisposition.Excluded, null);
        Check("missing policy", null, new FieldCandidate(key, FieldClassification.ApprovedReference, "must-not-stage"),
            FieldDisposition.Unclassified, null);
        Check("bad policy digest", policy with { Sha256 = "bad" },
            new FieldCandidate(key, FieldClassification.ApprovedReference, "must-not-stage"),
            FieldDisposition.Unclassified, null);
        Check("malformed field ID", policy, new FieldCandidate(new FieldKey("authorization", "\n"),
            FieldClassification.ApprovedReference, "must-not-stage"), FieldDisposition.Unclassified, null);
        var candidateText = new FieldCandidate(key, FieldClassification.ApprovedReference, "must-not-log").ToString();
        var resultText = FieldMinimizer.Evaluate(policy,
            new FieldCandidate(key, FieldClassification.ApprovedReference, "must-not-log")).ToString();
        if (candidateText.Contains("must-not-log", StringComparison.Ordinal) ||
            resultText.Contains("must-not-log", StringComparison.Ordinal))
        {
            throw new Exception("Field object formatting exposed a value.");
        }

        count++;
        return count;

        void Check(string name, FieldPolicySnapshot? snapshot, FieldCandidate candidate,
            FieldDisposition disposition, string? value)
        {
            var result = FieldMinimizer.Evaluate(snapshot, candidate);
            if (result.Disposition != disposition || result.IncludedValue != value ||
                result.MayStageValue != (disposition == FieldDisposition.Included))
            {
                throw new Exception($"{name}: unexpected field minimization.");
            }

            count++;
        }
    }
}
