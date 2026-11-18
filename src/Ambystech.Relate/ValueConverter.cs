using System.Globalization;

namespace Ambystech.Relate;

/// <summary>Converts decoded Relate values to the type a caller asks for.</summary>
internal static class ValueConverter
{
    internal static T? Convert<T>(object? value)
    {
        if (value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T?)ConvertTo(value, target);
    }

    internal static object? ConvertTo(object? value, Type target)
    {
        if (value is null || target.IsInstanceOfType(value))
        {
            return value;
        }

        target = Nullable.GetUnderlyingType(target) ?? target;

        return value switch
        {
            RelateDuration d when target == typeof(TimeSpan) => d.ToTimeSpan(),
            RelateTemporal t when target == typeof(DateTimeOffset) => t.ToDateTimeOffset(),
            RelateTemporal t when target == typeof(DateTime) => t.ToDateTimeOffset().DateTime,
            DateTimeOffset dto when target == typeof(DateTime) => dto.UtcDateTime,
            DateTime dt when target == typeof(DateTimeOffset) => new DateTimeOffset(dt),
            string s when target == typeof(Guid) => Guid.Parse(s),
            RelateVector { Floats: not null } v when target == typeof(float[]) => v.Floats.Select(x => (float)x).ToArray(),
            RelateVector { Floats: not null } v when target == typeof(double[]) => v.Floats.ToArray(),
            RelateVector { Integers: not null } v when target == typeof(long[]) => v.Integers.ToArray(),
            List<object?> list when target == typeof(byte[]) => list.Select(x => checked((byte)(long)x!)).ToArray(),
            List<object?> list when target == typeof(double[]) => list.Select(x => System.Convert.ToDouble(x, CultureInfo.InvariantCulture)).ToArray(),
            List<object?> list when target == typeof(float[]) => list.Select(x => System.Convert.ToSingle(x, CultureInfo.InvariantCulture)).ToArray(),
            List<object?> list when target == typeof(long[]) => list.Select(x => System.Convert.ToInt64(x, CultureInfo.InvariantCulture)).ToArray(),
            List<object?> list when target == typeof(string[]) => list.Select(x => (string?)x).ToArray(),
            List<object?> list when target == typeof(List<string>) => list.Select(x => (string)x!).ToList(),
            IConvertible when typeof(IConvertible).IsAssignableFrom(target) => System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"Cannot convert a {value.GetType().Name} value to {target.Name}."),
        };
    }
}
