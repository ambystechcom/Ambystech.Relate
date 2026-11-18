namespace Ambystech.Relate;

/// <summary>
/// A lossless temporal value, used when no BCL type holds it exactly: every <c>TIME</c>
/// (with offset), any value with sub-100ns precision, a <c>DATETIME</c> carrying an IANA zone
/// id, or an offset that is not a whole minute. Everything else decodes to
/// <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="DateTime"/> or <see cref="DateTimeOffset"/>.
/// </summary>
public sealed record RelateTemporal(
    RelateTemporalKind Kind,
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    int Second,
    int Nanosecond,
    int? OffsetSeconds = null,
    string? Zone = null)
{
    /// <summary>A zoned date-time (the offset is resolved by the engine from the zone at write time).</summary>
    public static RelateTemporal ZonedDateTime(DateTime local, string zone, int nanosecond = -1) =>
        new(RelateTemporalKind.DateTime, local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second,
            nanosecond >= 0 ? nanosecond : (int)(local.Ticks % TimeSpan.TicksPerSecond) * 100, null, zone);

    /// <summary>A time of day with an offset (Cypher <c>TIME</c>).</summary>
    public static RelateTemporal OffsetTime(TimeOnly time, TimeSpan offset) =>
        new(RelateTemporalKind.Time, 0, 0, 0, time.Hour, time.Minute, time.Second,
            (int)(time.Ticks % TimeSpan.TicksPerSecond) * 100, (int)offset.TotalSeconds);

    /// <summary>Convert to <see cref="DateTimeOffset"/>, truncating sub-100ns precision. Not valid for time-only kinds.</summary>
    public DateTimeOffset ToDateTimeOffset()
    {
        if (Kind is RelateTemporalKind.Time or RelateTemporalKind.LocalTime)
        {
            throw new InvalidOperationException($"A {Kind} has no date.");
        }

        var local = new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified).AddTicks(Nanosecond / 100);
        return new DateTimeOffset(local, TimeSpan.FromSeconds(OffsetSeconds ?? 0));
    }
}
