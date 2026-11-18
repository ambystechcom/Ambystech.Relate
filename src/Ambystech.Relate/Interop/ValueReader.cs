namespace Ambystech.Relate.Interop;

/// <summary>
/// The typed value walker (ADR-0502 §1): one switch over the kind, recursion for containers.
/// Every handle belongs to the result; this only copies into managed values.
/// </summary>
internal static unsafe class ValueReader
{
    private const int KNull = 0, KBool = 1, KInt = 2, KFloat = 3, KString = 4, KNode = 5, KRelationship = 6,
        KPath = 7, KList = 8, KMap = 9, KPoint = 10, KDate = 11, KTime = 12, KLocalTime = 13, KDateTime = 14,
        KLocalDateTime = 15, KDuration = 16, KVector = 17;

    internal static object? Read(nint v)
    {
        var kind = RelateNative.relate_value_kind(v);
        return kind switch
        {
            -1 => throw new RelateException("ABI contract violated: the walker returned a NULL value handle."),
            KNull => null,
            KBool => ReadBool(v),
            KInt => ReadInt64(v, &RelateNative.relate_value_int, "value_int"),
            KFloat => ReadFloat(v),
            KString => ReadString(v),
            KNode => new RelateNode(
                Required(RelateNative.relate_value_element_id(v), "value_element_id"),
                ReadInt64(v, &RelateNative.relate_value_id, "value_id"),
                ReadLabels(v),
                ReadMap(v)),
            KRelationship => new RelateRelationship(
                Required(RelateNative.relate_value_element_id(v), "value_element_id"),
                ReadInt64(v, &RelateNative.relate_value_id, "value_id"),
                Required(RelateNative.relate_value_start_element_id(v), "value_start_element_id"),
                ReadInt64(v, &RelateNative.relate_value_start_id, "value_start_id"),
                Required(RelateNative.relate_value_end_element_id(v), "value_end_element_id"),
                ReadInt64(v, &RelateNative.relate_value_end_id, "value_end_id"),
                ReadString(v),
                ReadMap(v)),
            KPath => ReadPath(v),
            KList => ReadList(v),
            KMap => ReadMap(v),
            KPoint => ReadPoint(v),
            KDate => new DateOnly(Component(v, &RelateNative.relate_value_year), Component(v, &RelateNative.relate_value_month), Component(v, &RelateNative.relate_value_day)),
            KTime => ReadTemporal(v, RelateTemporalKind.Time, hasDate: false, hasOffset: true),
            KLocalTime => ReadLocalTime(v),
            KDateTime => ReadDateTime(v),
            KLocalDateTime => ReadLocalDateTime(v),
            KDuration => ReadDuration(v),
            KVector => ReadVector(v),
            _ => throw new RelateException($"Unsupported value kind {kind}: the native library is newer than this binding."),
        };
    }

    private static bool ReadBool(nint v)
    {
        Check(RelateNative.relate_value_bool(v, out var b), "value_bool");
        return b != 0;
    }

    private static double ReadFloat(nint v)
    {
        Check(RelateNative.relate_value_float(v, out var d), "value_float");
        return d;
    }

    internal static string ReadString(nint v)
    {
        var p = RelateNative.relate_value_string(v, out var len);
        if (p == null)
        {
            throw Refused("value_string");
        }

        return NativeStrings.FromBorrowed(p, len);
    }

    private static List<string> ReadLabels(nint v)
    {
        var n = Count(RelateNative.relate_value_list_len(v), "value_list_len");
        var labels = new List<string>((int)n);
        for (long i = 0; i < n; i++)
        {
            labels.Add(ReadString(RelateNative.relate_value_list_get(v, i)));
        }

        return labels;
    }

    private static List<object?> ReadList(nint v)
    {
        var n = Count(RelateNative.relate_value_list_len(v), "value_list_len");
        var items = new List<object?>((int)n);
        for (long i = 0; i < n; i++)
        {
            items.Add(Read(RelateNative.relate_value_list_get(v, i)));
        }

        return items;
    }

    private static Dictionary<string, object?> ReadMap(nint v)
    {
        var n = Count(RelateNative.relate_value_map_len(v), "value_map_len");
        var map = new Dictionary<string, object?>((int)n, StringComparer.Ordinal);
        for (long i = 0; i < n; i++)
        {
            var key = RelateNative.relate_value_map_key(v, i, out var len);
            if (key == null)
            {
                throw Refused("value_map_key");
            }

            var name = NativeStrings.FromBorrowed(key, len);
            if (name.Contains('\0', StringComparison.Ordinal))
            {
                // map_get reads its key as a C string and would stop at the NUL (the upstream clients refuse too).
                throw new RelateException($"Map key \"{name}\" holds an interior NUL and cannot be read through the walker.");
            }

            // Reuse the engine's own key bytes for the lookup: exact and NUL-terminated.
            var child = RelateNative.relate_value_map_get(v, key);
            if (child == 0)
            {
                throw new RelateException($"ABI contract violated: value_map_get refused the present key \"{name}\".");
            }

            map[name] = Read(child);
        }

        return map;
    }

    private static RelatePath ReadPath(nint v)
    {
        // node, rel, node, ... (2n + 1 elements for n hops)
        var elements = ReadList(v);
        var nodes = new List<RelateNode>((elements.Count / 2) + 1);
        var rels = new List<RelateRelationship>(elements.Count / 2);
        for (var i = 0; i < elements.Count; i++)
        {
            if (i % 2 == 0)
            {
                nodes.Add(elements[i] as RelateNode ?? throw new RelateException("Malformed path: expected a node."));
            }
            else
            {
                rels.Add(elements[i] as RelateRelationship ?? throw new RelateException("Malformed path: expected a relationship."));
            }
        }

        return new RelatePath(nodes, rels);
    }

    private static RelatePoint ReadPoint(nint v)
    {
        Check(RelateNative.relate_value_point(v, out var srid, out var dim, out var x, out var y, out var z), "value_point");
        return new RelatePoint(srid, x, y, dim == 3 ? z : null);
    }

    private static RelateDuration ReadDuration(nint v)
    {
        Check(RelateNative.relate_value_duration(v, out var months, out var days, out var seconds, out var nanos), "value_duration");
        return new RelateDuration(months, days, seconds, nanos);
    }

    private static RelateVector ReadVector(nint v)
    {
        var dtype = NativeStrings.FromBorrowed(RelateNative.relate_value_vector_dtype(v)) ?? throw Refused("value_vector_dtype");
        var dim = Count(RelateNative.relate_value_vector_dim(v), "value_vector_dim");
        if (RelateVector.IsIntegerDtype(dtype))
        {
            var ints = new long[dim];
            for (long i = 0; i < dim; i++)
            {
                Check(RelateNative.relate_value_vector_int(v, i, out ints[i]), "value_vector_int");
            }

            return RelateVector.FromNative(dtype, null, ints);
        }

        if (!RelateVector.IsFloatDtype(dtype))
        {
            throw new RelateException($"Unsupported vector dtype {dtype}.");
        }

        var floats = new double[dim];
        for (long i = 0; i < dim; i++)
        {
            Check(RelateNative.relate_value_vector_float(v, i, out floats[i]), "value_vector_float");
        }

        return RelateVector.FromNative(dtype, floats, null);
    }

    private static object ReadLocalTime(nint v)
    {
        var t = ReadTemporal(v, RelateTemporalKind.LocalTime, hasDate: false, hasOffset: false);
        return t.Nanosecond % 100 == 0
            ? new TimeOnly(t.Hour, t.Minute, t.Second).Add(TimeSpan.FromTicks(t.Nanosecond / 100))
            : t;
    }

    private static object ReadLocalDateTime(nint v)
    {
        var t = ReadTemporal(v, RelateTemporalKind.LocalDateTime, hasDate: true, hasOffset: false);
        return t.Nanosecond % 100 == 0 && t.Year is >= 1 and <= 9999
            ? new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Unspecified).AddTicks(t.Nanosecond / 100)
            : t;
    }

    private static object ReadDateTime(nint v)
    {
        var zone = NativeStrings.FromBorrowed(RelateNative.relate_value_zone(v)) ?? throw Refused("value_zone");
        var t = ReadTemporal(v, RelateTemporalKind.DateTime, hasDate: true, hasOffset: true) with
        {
            Zone = zone.Length == 0 ? null : zone,
        };

        var lossless = t.Zone is null
            && t.Nanosecond % 100 == 0
            && t.OffsetSeconds % 60 == 0
            && t.Year is >= 1 and <= 9999;
        return lossless ? t.ToDateTimeOffset() : t;
    }

    private static RelateTemporal ReadTemporal(nint v, RelateTemporalKind kind, bool hasDate, bool hasOffset) =>
        new(
            kind,
            hasDate ? Component(v, &RelateNative.relate_value_year) : 0,
            hasDate ? Component(v, &RelateNative.relate_value_month) : 0,
            hasDate ? Component(v, &RelateNative.relate_value_day) : 0,
            Component(v, &RelateNative.relate_value_hour),
            Component(v, &RelateNative.relate_value_minute),
            Component(v, &RelateNative.relate_value_second),
            Component(v, &RelateNative.relate_value_nanosecond),
            hasOffset ? Component(v, &RelateNative.relate_value_offset_seconds) : null);

    private static int Component(nint v, delegate*<nint, out long, int> read) =>
        checked((int)ReadInt64(v, read, "temporal component"));

    private static long ReadInt64(nint v, delegate*<nint, out long, int> read, string what)
    {
        Check(read(v, out var value), what);
        return value;
    }

    private static string Required(byte* p, string what) => NativeStrings.FromBorrowed(p) ?? throw Refused(what);

    private static long Count(long n, string what) => n >= 0 ? n : throw Refused(what);

    private static void Check(int status, string what)
    {
        if (status != RelateNative.RELATE_OK)
        {
            throw Refused(what);
        }
    }

    private static RelateException Refused(string what) => new($"ABI contract violated: {what} refused an in-kind read.");
}
