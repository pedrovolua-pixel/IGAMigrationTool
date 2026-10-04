using CollectorSafety;

internal static class LocalRetentionWindowChecks
{
    internal static int Run()
    {
        var count = 0;
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        Check("active", now.AddMinutes(-59), now, 1, LocalRetentionDecision.Active);
        Check("expires at exact boundary", now.AddHours(-1), now, 1, LocalRetentionDecision.Expired);
        Check("expired after boundary", now.AddHours(-2), now, 1, LocalRetentionDecision.Expired);
        Check("future start rejected", now.AddMinutes(1), now, 1, LocalRetentionDecision.InvalidInput);
        Check("zero retention rejected", now, now, 0, LocalRetentionDecision.InvalidInput);
        Check("retention beyond configured range rejected", now, now, 721,
            LocalRetentionDecision.InvalidInput);
        return count;

        void Check(string name, DateTimeOffset started, DateTimeOffset current, int hours,
            LocalRetentionDecision expected)
        {
            if (LocalRetentionWindow.Evaluate(started, current, hours) != expected)
            {
                throw new Exception($"{name}: unexpected local retention result.");
            }

            count++;
        }
    }
}
