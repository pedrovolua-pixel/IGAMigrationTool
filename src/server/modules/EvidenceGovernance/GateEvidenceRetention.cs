namespace EvidenceGovernance;

/// <summary>
/// Calculates the default engineering gate-evidence lifecycle. An approved hold
/// requires a separate authorized decision and is not represented by this clock.
/// </summary>
public static class GateEvidenceRetention
{
    public static GateEvidenceRetentionWindow Calculate(DateTimeOffset decisionAt)
    {
        if (decisionAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The gate decision time must be UTC.", nameof(decisionAt));
        }

        try
        {
            var ordinaryAccessEndsAt = decisionAt.AddMonths(12);
            return new GateEvidenceRetentionWindow(
                ordinaryAccessEndsAt,
                ordinaryAccessEndsAt.AddDays(30));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException("The gate decision time cannot support the retention window.",
                nameof(decisionAt), exception);
        }
    }

    public static GateEvidenceRetentionPhase PhaseAt(DateTimeOffset decisionAt, DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || now < decisionAt)
        {
            throw new ArgumentException("The evaluation time must be UTC and no earlier than the decision.",
                nameof(now));
        }

        var window = Calculate(decisionAt);
        return now >= window.ActiveStorePurgeDueAt
            ? GateEvidenceRetentionPhase.PurgeDue
            : now >= window.OrdinaryAccessEndsAt
                ? GateEvidenceRetentionPhase.OrdinaryAccessExpired
                : GateEvidenceRetentionPhase.OrdinaryAccessAllowed;
    }
}

public readonly record struct GateEvidenceRetentionWindow(
    DateTimeOffset OrdinaryAccessEndsAt,
    DateTimeOffset ActiveStorePurgeDueAt);

public enum GateEvidenceRetentionPhase
{
    OrdinaryAccessAllowed,
    OrdinaryAccessExpired,
    PurgeDue
}
