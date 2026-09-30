namespace CollectorSafety;

public enum FieldClassification
{
    ApprovedReference,
    Redacted,
    Prohibited,
    Unknown
}

public enum FieldDisposition
{
    Included,
    Redacted,
    Prohibited,
    Excluded,
    Unclassified
}

public sealed record FieldKey(string CategoryId, string FieldId);

public sealed record FieldCandidate(FieldKey Key, FieldClassification Classification, string? Value)
{
    public override string ToString() => "FieldCandidate";
}

public sealed record FieldPolicySnapshot(
    string PolicyId,
    string Version,
    string Sha256,
    IReadOnlySet<FieldKey> IncludedFields,
    IReadOnlySet<string> ExcludedCategories);

public sealed record MinimizedField(FieldKey? Key, FieldDisposition Disposition, string? IncludedValue)
{
    public bool MayStageValue => Disposition == FieldDisposition.Included;

    public override string ToString() => $"MinimizedField: {Disposition}";
}

/// <summary>
/// Pure policy enforcement after a separate signature and exact-field-dictionary gate.
/// No caller may treat a self-supplied policy snapshot as signed or promoted.
/// </summary>
public static class FieldMinimizer
{
    public static MinimizedField Evaluate(FieldPolicySnapshot? policy, FieldCandidate? candidate)
    {
        if (!ValidPolicy(policy) || !ValidKey(candidate?.Key))
        {
            return new MinimizedField(null, FieldDisposition.Unclassified, null);
        }

        var key = candidate!.Key;
        var disposition = candidate.Classification switch
        {
            FieldClassification.Prohibited => FieldDisposition.Prohibited,
            FieldClassification.Redacted => FieldDisposition.Redacted,
            FieldClassification.Unknown => FieldDisposition.Unclassified,
            FieldClassification.ApprovedReference when policy!.ExcludedCategories.Contains(key.CategoryId) =>
                FieldDisposition.Excluded,
            FieldClassification.ApprovedReference when !policy!.IncludedFields.Contains(key) =>
                FieldDisposition.Excluded,
            FieldClassification.ApprovedReference => FieldDisposition.Included,
            _ => FieldDisposition.Unclassified
        };
        return new MinimizedField(key, disposition,
            disposition == FieldDisposition.Included ? candidate.Value : null);
    }

    private static bool ValidPolicy(FieldPolicySnapshot? policy) =>
        policy is not null &&
        !string.IsNullOrWhiteSpace(policy.PolicyId) &&
        !string.IsNullOrWhiteSpace(policy.Version) &&
        policy.Sha256 is { Length: 64 } digest &&
        digest.All(Uri.IsHexDigit) &&
        policy.IncludedFields is not null &&
        policy.ExcludedCategories is not null &&
        policy.IncludedFields.All(ValidKey) &&
        policy.ExcludedCategories.All(ValidId);

    private static bool ValidKey(FieldKey? key) =>
        key is not null && ValidId(key.CategoryId) && ValidId(key.FieldId);

    private static bool ValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);
}
