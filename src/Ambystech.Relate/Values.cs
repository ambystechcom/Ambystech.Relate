namespace Ambystech.Relate;

/// <summary>A graph node as returned by a query.</summary>
/// <param name="ElementId">The engine's <c>elementId()</c> string. Prefer it over <paramref name="Id"/>.</param>
/// <param name="Id">The legacy numeric id, as Cypher <c>id()</c> returns it.</param>
/// <param name="Labels">The node's labels.</param>
/// <param name="Properties">The node's properties, in engine order.</param>
public sealed record RelateNode(
    string ElementId,
    long Id,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, object?> Properties)
{
    /// <summary>Read a property converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(Properties[key]);

    /// <summary>True when the node carries <paramref name="label"/>.</summary>
    public bool HasLabel(string label) => Labels.Contains(label, StringComparer.Ordinal);
}

/// <summary>A graph relationship as returned by a query.</summary>
public sealed record RelateRelationship(
    string ElementId,
    long Id,
    string StartElementId,
    long StartId,
    string EndElementId,
    long EndId,
    string Type,
    IReadOnlyDictionary<string, object?> Properties)
{
    /// <summary>Read a property converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(Properties[key]);
}

/// <summary>A path: <c>n</c> hops have <c>n + 1</c> nodes and <c>n</c> relationships.</summary>
public sealed record RelatePath(IReadOnlyList<RelateNode> Nodes, IReadOnlyList<RelateRelationship> Relationships)
{
    /// <summary>The first node.</summary>
    public RelateNode Start => Nodes[0];

    /// <summary>The last node.</summary>
    public RelateNode End => Nodes[^1];

    /// <summary>The number of hops.</summary>
    public int Length => Relationships.Count;
}

/// <summary>A spatial point. <see cref="Z"/> is set for 3D SRIDs (9157, 4979).</summary>
public sealed record RelatePoint(int Srid, double X, double Y, double? Z = null)
{
    /// <summary>SRID 7203.</summary>
    public const int CartesianSrid = 7203;
    /// <summary>SRID 9157.</summary>
    public const int Cartesian3DSrid = 9157;
    /// <summary>SRID 4326: X is longitude, Y is latitude.</summary>
    public const int Wgs84Srid = 4326;
    /// <summary>SRID 4979.</summary>
    public const int Wgs843DSrid = 4979;

    /// <summary>A 2D cartesian point.</summary>
    public static RelatePoint Cartesian(double x, double y) => new(CartesianSrid, x, y);

    /// <summary>A 3D cartesian point.</summary>
    public static RelatePoint Cartesian(double x, double y, double z) => new(Cartesian3DSrid, x, y, z);

    /// <summary>A WGS-84 geographic point.</summary>
    public static RelatePoint Wgs84(double longitude, double latitude) => new(Wgs84Srid, longitude, latitude);

    /// <summary>A WGS-84 3D geographic point.</summary>
    public static RelatePoint Wgs84(double longitude, double latitude, double height) => new(Wgs843DSrid, longitude, latitude, height);
}

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

/// <summary>A typed vector value (<c>VECTOR</c>). Float dtypes fill <see cref="Floats"/>; integer dtypes fill <see cref="Integers"/>.</summary>
public sealed class RelateVector
{
    private RelateVector(string dtype, double[]? floats, long[]? integers)
    {
        Dtype = dtype;
        Floats = floats;
        Integers = integers;
    }

    /// <summary>The engine dtype: FLOAT16, BFLOAT16, FLOAT32, FLOAT, INTEGER8, INTEGER16, INTEGER32 or INTEGER.</summary>
    public string Dtype { get; }

    /// <summary>Coordinates of a float-family vector, otherwise <c>null</c>.</summary>
    public IReadOnlyList<double>? Floats { get; }

    /// <summary>Coordinates of an integer-family vector, otherwise <c>null</c>.</summary>
    public IReadOnlyList<long>? Integers { get; }

    /// <summary>True for the INTEGER* dtypes.</summary>
    public bool IsInteger => Integers is not null;

    /// <summary>The number of coordinates.</summary>
    public int Dimensions => Floats?.Count ?? Integers!.Count;

    /// <summary>A <c>FLOAT32</c> vector (the usual embedding dtype).</summary>
    public static RelateVector Float32(ReadOnlySpan<float> values)
    {
        var copy = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            copy[i] = values[i];
        }

        return new RelateVector("FLOAT32", copy, null);
    }

    /// <summary>A float-family vector of the given dtype (default <c>FLOAT</c>, 64-bit).</summary>
    public static RelateVector Float(ReadOnlySpan<double> values, string dtype = "FLOAT") =>
        IsFloatDtype(dtype)
            ? new RelateVector(dtype, values.ToArray(), null)
            : throw new ArgumentException($"'{dtype}' is not a float vector dtype.", nameof(dtype));

    /// <summary>An integer-family vector of the given dtype (default <c>INTEGER</c>, 64-bit).</summary>
    public static RelateVector Integer(ReadOnlySpan<long> values, string dtype = "INTEGER") =>
        IsIntegerDtype(dtype)
            ? new RelateVector(dtype, null, values.ToArray())
            : throw new ArgumentException($"'{dtype}' is not an integer vector dtype.", nameof(dtype));

    internal static RelateVector FromNative(string dtype, double[]? floats, long[]? integers) => new(dtype, floats, integers);

    internal static bool IsFloatDtype(string dtype) => dtype is "FLOAT16" or "BFLOAT16" or "FLOAT32" or "FLOAT";

    internal static bool IsIntegerDtype(string dtype) => dtype is "INTEGER8" or "INTEGER16" or "INTEGER32" or "INTEGER";

    /// <inheritdoc />
    public override string ToString() =>
        $"vector<{Dtype}>[{(Floats is not null ? string.Join(", ", Floats) : string.Join(", ", Integers!))}]";
}

/// <summary>The Cypher temporal kinds that can come back as <see cref="RelateTemporal"/>.</summary>
public enum RelateTemporalKind
{
    /// <summary>A time of day with an offset.</summary>
    Time,
    /// <summary>A time of day without an offset.</summary>
    LocalTime,
    /// <summary>A date and time without an offset.</summary>
    LocalDateTime,
    /// <summary>A date and time with an offset and possibly an IANA zone.</summary>
    DateTime,
}

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
