namespace CollectorHost;

public static class DailySchedule
{
    public static DateTimeOffset Next(DateTimeOffset now, TimeZoneInfo zone, TimeOnly localTime)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        for (var day = 0; day <= 366; day++)
        {
            var local = today.AddDays(day).ToDateTime(localTime, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local))
            {
                continue;
            }

            var offset = zone.IsAmbiguousTime(local)
                ? zone.GetAmbiguousTimeOffsets(local).Min()
                : zone.GetUtcOffset(local);
            var candidate = new DateTimeOffset(local, offset);
            if (candidate > now)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No valid daily occurrence was found.");
    }
}
