using EvidenceGovernance;

internal static class GateEvidenceRetentionChecks
{
    public static int Run()
    {
        var count = 0;
        var decisionAt = new DateTimeOffset(2024, 2, 29, 18, 30, 0, TimeSpan.Zero);
        var window = GateEvidenceRetention.Calculate(decisionAt);
        Expect("leap-day anniversary", new DateTimeOffset(2025, 2, 28, 18, 30, 0, TimeSpan.Zero),
            window.OrdinaryAccessEndsAt, ref count);
        Expect("active purge deadline", window.OrdinaryAccessEndsAt.AddDays(30),
            window.ActiveStorePurgeDueAt, ref count);
        Expect("last instant of ordinary access", GateEvidenceRetentionPhase.OrdinaryAccessAllowed,
            GateEvidenceRetention.PhaseAt(decisionAt, window.OrdinaryAccessEndsAt.AddTicks(-1)), ref count);
        Expect("anniversary closes ordinary access", GateEvidenceRetentionPhase.OrdinaryAccessExpired,
            GateEvidenceRetention.PhaseAt(decisionAt, window.OrdinaryAccessEndsAt), ref count);
        Expect("last instant before purge deadline", GateEvidenceRetentionPhase.OrdinaryAccessExpired,
            GateEvidenceRetention.PhaseAt(decisionAt, window.ActiveStorePurgeDueAt.AddTicks(-1)), ref count);
        Expect("purge deadline", GateEvidenceRetentionPhase.PurgeDue,
            GateEvidenceRetention.PhaseAt(decisionAt, window.ActiveStorePurgeDueAt), ref count);
        Reject("non-UTC decision", () => GateEvidenceRetention.Calculate(decisionAt.ToOffset(TimeSpan.FromHours(-4))), ref count);
        Reject("non-UTC evaluation", () => GateEvidenceRetention.PhaseAt(decisionAt,
            decisionAt.ToOffset(TimeSpan.FromHours(-4))), ref count);
        Reject("future decision", () => GateEvidenceRetention.PhaseAt(decisionAt, decisionAt.AddTicks(-1)), ref count);
        Reject("unrepresentable retention", () => GateEvidenceRetention.Calculate(DateTimeOffset.MaxValue), ref count);
        return count;
    }

    private static void Expect<T>(string name, T expected, T actual, ref int count)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{name}: expected {expected}, received {actual}");
        }

        count++;
    }

    private static void Reject(string name, Action action, ref int count)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            count++;
            return;
        }

        throw new Exception($"{name}: invalid time was accepted.");
    }
}
