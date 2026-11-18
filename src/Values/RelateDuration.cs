namespace Ambystech.Relate;

/// <summary>A Cypher duration, lossless: months, days, seconds and nanoseconds are kept apart.</summary>
public readonly record struct RelateDuration(long Months, long Days, long Seconds, long Nanoseconds)
{
    /// <summary>A duration of exactly <paramref name="value"/> (days, seconds and nanoseconds; no months).</summary>
    public static RelateDuration FromTimeSpan(TimeSpan value)
    {
        var days = value.Days;
        var remainder = value - TimeSpan.FromDays(days);
        var seconds = remainder.Ticks / TimeSpan.TicksPerSecond;
        var nanos = (remainder.Ticks % TimeSpan.TicksPerSecond) * 100;
        return new RelateDuration(0, days, seconds, nanos);
    }

    /// <summary>
    /// Convert to <see cref="TimeSpan"/>. Throws when the duration has months (their length depends
    /// on the calendar) or sub-100ns precision that a <see cref="TimeSpan"/> cannot hold.
    /// </summary>
    public TimeSpan ToTimeSpan()
    {
        if (Months != 0)
        {
            throw new InvalidOperationException("A duration with months has no fixed TimeSpan length.");
        }

        if (Nanoseconds % 100 != 0)
        {
            throw new InvalidOperationException("The duration has sub-100ns precision that TimeSpan cannot represent.");
        }

        return TimeSpan.FromDays(Days) + TimeSpan.FromSeconds(Seconds) + TimeSpan.FromTicks(Nanoseconds / 100);
    }
}
