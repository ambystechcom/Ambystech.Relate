using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>One row, addressable by column name or position.</summary>
public sealed class RelateRecord : IReadOnlyDictionary<string, object?>
{
    private readonly IReadOnlyList<string> _keys;
    private readonly Dictionary<string, int> _index;
    private readonly object?[] _values;

    internal RelateRecord(IReadOnlyList<string> keys, Dictionary<string, int> index, object?[] values)
    {
        _keys = keys;
        _index = index;
        _values = values;
    }

    /// <summary>The column names.</summary>
    public IEnumerable<string> Keys => _keys;

    /// <summary>The values, in column order.</summary>
    public IEnumerable<object?> Values => _values;

    /// <inheritdoc />
    public int Count => _values.Length;

    /// <summary>The value of column <paramref name="key"/>.</summary>
    public object? this[string key] => _index.TryGetValue(key, out var i)
        ? _values[i]
        : throw new KeyNotFoundException($"The record has no column '{key}'. Columns: {string.Join(", ", _keys)}.");

    /// <summary>The value at column <paramref name="index"/>.</summary>
    public object? this[int index] => _values[index];

    /// <summary>The value of column <paramref name="key"/> converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(this[key]);

    /// <summary>The value at column <paramref name="index"/> converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(int index) => ValueConverter.Convert<T>(_values[index]);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _index.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        if (_index.TryGetValue(key, out var i))
        {
            value = _values[i];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>A copy as a dictionary.</summary>
    public Dictionary<string, object?> ToDictionary() =>
        _keys.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => _values[x.i], StringComparer.Ordinal);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < _values.Length; i++)
        {
            yield return new KeyValuePair<string, object?>(_keys[i], _values[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() =>
        "{" + string.Join(", ", this.Select(kv => $"{kv.Key}: {kv.Value}")) + "}";
}
