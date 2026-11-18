using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class TemporalTests
{
    [Fact]
    public void Round_trips_temporals_durations_and_points()
    {
        using var db = RelateDatabase.Open();
        var dto = new DateTimeOffset(2026, 11, 16, 18, 30, 15, 123, TimeSpan.FromHours(-6));
        var parameters = new Dictionary<string, object?>
        {
            ["date"] = new DateOnly(2026, 11, 16),
            ["ltime"] = new TimeOnly(18, 30, 15, 250),
            ["ldt"] = new DateTime(2026, 11, 16, 18, 30, 15, DateTimeKind.Unspecified),
            ["dto"] = dto,
            ["span"] = TimeSpan.FromMinutes(90),
            ["months"] = new RelateDuration(14, 3, 10, 5),
            ["pt"] = RelatePoint.Wgs84(-100.3161, 25.6866),
            ["pt3"] = RelatePoint.Cartesian(1, 2, 3),
            ["zoned"] = RelateTemporal.ZonedDateTime(new DateTime(2026, 7, 1, 12, 0, 0), "America/Monterrey"),
            ["otime"] = RelateTemporal.OffsetTime(new TimeOnly(9, 15), TimeSpan.FromHours(2)),
        };

        var row = db.Run("""
            RETURN $date AS date, $ltime AS ltime, $ldt AS ldt, $dto AS dto, $span AS span, $months AS months,
                   $pt AS pt, $pt3 AS pt3, $zoned AS zoned, $otime AS otime,
                   date('2026-11-16') + duration('P1D') AS tomorrow
            """, parameters).Single();

        Assert.Equal(new DateOnly(2026, 11, 16), row["date"]);
        Assert.Equal(new TimeOnly(18, 30, 15, 250), row["ltime"]);
        Assert.Equal(new DateTime(2026, 11, 16, 18, 30, 15), row["ldt"]);
        Assert.Equal(dto, row["dto"]);
        Assert.Equal(TimeSpan.FromMinutes(90), row.Get<TimeSpan>("span"));
        Assert.Equal(new RelateDuration(14, 3, 10, 5), row["months"]);
        Assert.Equal(RelatePoint.Wgs84(-100.3161, 25.6866), row["pt"]);
        Assert.Equal(RelatePoint.Cartesian(1, 2, 3), row["pt3"]);
        Assert.Equal(new DateOnly(2026, 11, 17), row["tomorrow"]);

        var zoned = Assert.IsType<RelateTemporal>(row["zoned"]);
        Assert.Equal("America/Monterrey", zoned.Zone);
        Assert.Equal(-6 * 3600, zoned.OffsetSeconds);

        var otime = Assert.IsType<RelateTemporal>(row["otime"]);
        Assert.Equal(RelateTemporalKind.Time, otime.Kind);
        Assert.Equal(9, otime.Hour);
        Assert.Equal(7200, otime.OffsetSeconds);
    }

    [Fact]
    public void Sub_100ns_precision_is_kept_losslessly()
    {
        using var db = RelateDatabase.Open();
        var value = db.Run("RETURN localtime('12:00:00.123456789') AS t").Scalar<object>();

        var t = Assert.IsType<RelateTemporal>(value);
        Assert.Equal(123456789, t.Nanosecond);
    }
}
