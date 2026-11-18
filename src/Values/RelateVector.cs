namespace Ambystech.Relate;

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
