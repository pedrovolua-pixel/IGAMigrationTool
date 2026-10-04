namespace CollectorSafety;

public enum LocalRetentionDecision
{
    Active,
    Expired,
    InvalidInput
}

/// <summary>
/// Checks a trusted extraction start time against the customer-configured local expiry.
/// The caller must persist and authenticate that start time across restarts.
/// </summary>
public static class LocalRetentionWindow
{
    public static LocalRetentionDecision Evaluate(DateTimeOffset extractionStartedAtUtc,
        DateTimeOffset nowUtc, int retentionHours)
    {
        if (retentionHours is < 1 or > 720 || extractionStartedAtUtc < DateTimeOffset.UnixEpoch ||
            extractionStartedAtUtc > nowUtc)
        {
            return LocalRetentionDecision.InvalidInput;
        }

        return nowUtc - extractionStartedAtUtc >= TimeSpan.FromHours(retentionHours)
            ? LocalRetentionDecision.Expired
            : LocalRetentionDecision.Active;
    }
}
