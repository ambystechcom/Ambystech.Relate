using System.Collections;
using System.Text;

namespace Ambystech.Relate.Interop;

/// <summary>
/// Builds a <c>relate_params</c> tree from .NET values (ADR-0502 §4). Mapping:
/// null → NULL; bool → BOOLEAN; integral types → INTEGER; float/double/decimal → FLOAT;
/// string/char/Guid/enum → STRING; DateOnly → DATE; TimeOnly → LOCAL TIME;
/// DateTime (Utc → DATETIME at +00:00, otherwise LOCAL DATETIME); DateTimeOffset → DATETIME;
/// TimeSpan / <see cref="RelateDuration"/> → DURATION; <see cref="RelatePoint"/> → POINT;
/// <see cref="RelateVector"/> → VECTOR; <see cref="RelateTemporal"/> → its kind;
/// byte[] → LIST of INTEGER; dictionaries → MAP; other enumerables (incl. float[]) → LIST.
/// </summary>
internal static unsafe class ParamsWriter
{
    private const int MaxDepth = 64;

    internal static ParamsHandle? Build(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var root = RelateNative.relate_params_new();
        if (root == 0)
        {
            throw new RelateException("relate_params_new returned NULL.");
        }

        var handle = new ParamsHandle(root);
        try
        {
            foreach (var (name, value) in parameters)
            {
                Put(root, Normalize(name), value, 0);
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Accept <c>$name</c>, <c>@name</c> or <c>:name</c> spellings (ADO.NET habits) and bind <c>name</c>.</summary>
    internal static string Normalize(string name) =>
        name.Length > 0 && name[0] is '$' or '@' or ':' ? name[1..] : name;

    private static void Put(nint p, string? name, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new RelateException("Parameter nesting is too deep.");
        }

        nint err;
        int status;
        switch (value)
        {
            case null or DBNull:
                status = RelateNative.relate_params_put_null(p, name, out err);
                break;
            case bool b:
                status = RelateNative.relate_params_put_bool(p, name, b ? (byte)1 : (byte)0, out err);
                break;
            case sbyte or byte or short or ushort or int or uint or long:
                status = RelateNative.relate_params_put_int(p, name, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture), out err);
                break;
            case ulong ul:
                status = RelateNative.relate_params_put_int(p, name, checked((long)ul), out err);
                break;
            case float f:
                status = RelateNative.relate_params_put_float(p, name, f, out err);
                break;
            case double d:
                status = RelateNative.relate_params_put_float(p, name, d, out err);
                break;
            case decimal m:
                status = RelateNative.relate_params_put_float(p, name, (double)m, out err);
                break;
            case string s:
                status = PutString(p, name, s, out err);
                break;
            case char c:
                status = PutString(p, name, c.ToString(), out err);
                break;
            case Guid g:
                status = PutString(p, name, g.ToString(), out err);
                break;
            case Enum e:
                status = PutString(p, name, e.ToString(), out err);
                break;
            case DateOnly date:
                status = RelateNative.relate_params_put_date(p, name, date.Year, date.Month, date.Day, out err);
                break;
            case TimeOnly time:
                status = RelateNative.relate_params_put_local_time(p, name, time.Hour, time.Minute, time.Second, SubSecondNanos(time.Ticks), out err);
                break;
            case DateTime dt when dt.Kind == DateTimeKind.Utc:
                status = RelateNative.relate_params_put_datetime(p, name, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, SubSecondNanos(dt.Ticks), 0, null, out err);
                break;
            case DateTime dt:
                status = RelateNative.relate_params_put_local_datetime(p, name, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, SubSecondNanos(dt.Ticks), out err);
                break;
            case DateTimeOffset dto:
                status = RelateNative.relate_params_put_datetime(p, name, dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second,
                    SubSecondNanos(dto.Ticks), (long)dto.Offset.TotalSeconds, null, out err);
                break;
            case RelateTemporal t:
                status = PutTemporal(p, name, t, out err);
                break;
            case TimeSpan ts:
                var fromSpan = RelateDuration.FromTimeSpan(ts);
                status = RelateNative.relate_params_put_duration(p, name, 0, fromSpan.Days, fromSpan.Seconds, fromSpan.Nanoseconds, out err);
                break;
            case RelateDuration dur:
                status = RelateNative.relate_params_put_duration(p, name, dur.Months, dur.Days, dur.Seconds, dur.Nanoseconds, out err);
                break;
            case RelatePoint pt:
                status = PutPoint(p, name, pt, out err);
                break;
            case RelateVector vec:
                status = PutVector(p, name, vec, out err);
                break;
            case byte[] bytes:
                PutList(p, name, bytes.Select(x => (object?)(long)x), depth);
                return;
            case IDictionary dict:
                PutMap(p, name, EnumerateDictionary(dict), depth);
                return;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                PutMap(p, name, pairs, depth);
                return;
            case IEnumerable seq:
                PutList(p, name, seq.Cast<object?>(), depth);
                return;
            default:
                throw new ArgumentException($"Parameter '{name}' has unsupported type {value.GetType().FullName}.");
        }

        NativeCall.Check(status, err, $"binding parameter '{name}'");
    }

    private static void PutList(nint p, string? name, IEnumerable<object?> items, int depth)
    {
        NativeCall.Check(RelateNative.relate_params_put_list(p, name, out var child, out var err), err, $"binding list '{name}'");
        foreach (var item in items)
        {
            Put(child, null, item, depth + 1);
        }
    }

    private static void PutMap(nint p, string? name, IEnumerable<KeyValuePair<string, object?>> entries, int depth)
    {
        NativeCall.Check(RelateNative.relate_params_put_map(p, name, out var child, out var err), err, $"binding map '{name}'");
        foreach (var (key, item) in entries)
        {
            Put(child, key, item, depth + 1);
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateDictionary(IDictionary dict)
    {
        foreach (DictionaryEntry entry in dict)
        {
            yield return new KeyValuePair<string, object?>(
                entry.Key as string ?? throw new ArgumentException("Map parameter keys must be strings."),
                entry.Value);
        }
    }

    private static int PutString(nint p, string? name, string s, out nint err)
    {
        var max = Encoding.UTF8.GetMaxByteCount(s.Length);
        var rented = max <= 512 ? null : System.Buffers.ArrayPool<byte>.Shared.Rent(max);
        try
        {
            Span<byte> buffer = rented is null ? stackalloc byte[max] : rented;
            var written = Encoding.UTF8.GetBytes(s, buffer);
            fixed (byte* ptr = buffer)
            {
                return RelateNative.relate_params_put_string(p, name, ptr, (nuint)written, out err);
            }
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int PutPoint(nint p, string? name, RelatePoint pt, out nint err)
    {
        if (pt.Z is double z)
        {
            return RelateNative.relate_params_put_point(p, name, pt.Srid, pt.X, pt.Y, &z, out err);
        }

        return RelateNative.relate_params_put_point(p, name, pt.Srid, pt.X, pt.Y, null, out err);
    }

    private static int PutVector(nint p, string? name, RelateVector vec, out nint err)
    {
        if (vec.Integers is { } ints)
        {
            var arr = ints as long[] ?? ints.ToArray();
            fixed (long* ptr = arr)
            {
                return RelateNative.relate_params_put_vector_int(p, name, vec.Dtype, ptr, (nuint)arr.Length, out err);
            }
        }

        var floats = vec.Floats as double[] ?? vec.Floats!.ToArray();
        fixed (double* ptr = floats)
        {
            return RelateNative.relate_params_put_vector_float(p, name, vec.Dtype, ptr, (nuint)floats.Length, out err);
        }
    }

    private static int PutTemporal(nint p, string? name, RelateTemporal t, out nint err) => t.Kind switch
    {
        RelateTemporalKind.Time => RelateNative.relate_params_put_time(p, name, t.Hour, t.Minute, t.Second, t.Nanosecond, t.OffsetSeconds ?? 0, t.Zone, out err),
        RelateTemporalKind.LocalTime => RelateNative.relate_params_put_local_time(p, name, t.Hour, t.Minute, t.Second, t.Nanosecond, out err),
        RelateTemporalKind.LocalDateTime => RelateNative.relate_params_put_local_datetime(p, name, t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Nanosecond, out err),
        _ => RelateNative.relate_params_put_datetime(p, name, t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Nanosecond, t.OffsetSeconds ?? 0, t.Zone, out err),
    };

    private static long SubSecondNanos(long ticks) => ticks % TimeSpan.TicksPerSecond * 100;
}
