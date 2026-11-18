using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Data;

/// <summary>A named Cypher parameter. The value maps to Relate types the same way as the driver API.</summary>
public sealed class RelateDbParameter : DbParameter
{
    private string _name = string.Empty;

    /// <summary>An unnamed parameter.</summary>
    public RelateDbParameter() { }

    /// <summary>A parameter named <paramref name="name"/> (with or without <c>$</c> / <c>@</c>).</summary>
    public RelateDbParameter(string name, object? value)
    {
        _name = name;
        Value = value;
    }

    /// <inheritdoc />
    public override DbType DbType { get; set; } = DbType.Object;

    /// <inheritdoc />
    public override ParameterDirection Direction
    {
        get => ParameterDirection.Input;
        set
        {
            if (value != ParameterDirection.Input)
            {
                throw new NotSupportedException("Relate parameters are input-only.");
            }
        }
    }

    /// <inheritdoc />
    public override bool IsNullable { get; set; } = true;

    /// <inheritdoc />
    [AllowNull]
    public override string ParameterName
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    /// <inheritdoc />
    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool SourceColumnNullMapping { get; set; }

    /// <inheritdoc />
    public override object? Value { get; set; }

    /// <inheritdoc />
    public override int Size { get; set; }

    /// <inheritdoc />
    public override void ResetDbType() => DbType = DbType.Object;
}

/// <summary>The parameters of a <see cref="RelateDbCommand"/>.</summary>
public sealed class RelateDbParameterCollection : DbParameterCollection
{
    private readonly List<RelateDbParameter> _items = [];

    /// <inheritdoc />
    public override int Count => _items.Count;

    /// <inheritdoc />
    public override object SyncRoot => ((ICollection)_items).SyncRoot;

    /// <summary>Add a named value.</summary>
    public RelateDbParameter AddWithValue(string parameterName, object? value)
    {
        var p = new RelateDbParameter(parameterName, value);
        _items.Add(p);
        return p;
    }

    /// <inheritdoc />
    public override int Add(object value)
    {
        _items.Add(Cast(value));
        return _items.Count - 1;
    }

    /// <inheritdoc />
    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    /// <inheritdoc />
    public override void Clear() => _items.Clear();

    /// <inheritdoc />
    public override bool Contains(object value) => value is RelateDbParameter p && _items.Contains(p);

    /// <inheritdoc />
    public override bool Contains(string value) => IndexOf(value) >= 0;

    /// <inheritdoc />
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    public override int IndexOf(object value) => value is RelateDbParameter p ? _items.IndexOf(p) : -1;

    /// <inheritdoc />
    public override int IndexOf(string parameterName)
    {
        var name = ParamsWriter.Normalize(parameterName);
        return _items.FindIndex(p => ParamsWriter.Normalize(p.ParameterName) == name);
    }

    /// <inheritdoc />
    public override void Insert(int index, object value) => _items.Insert(index, Cast(value));

    /// <inheritdoc />
    public override void Remove(object value) => _items.Remove(Cast(value));

    /// <inheritdoc />
    public override void RemoveAt(int index) => _items.RemoveAt(index);

    /// <inheritdoc />
    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

    /// <inheritdoc />
    protected override DbParameter GetParameter(int index) => _items[index];

    /// <inheritdoc />
    protected override DbParameter GetParameter(string parameterName) =>
        IndexOf(parameterName) is var i and >= 0 ? _items[i] : throw new IndexOutOfRangeException($"No parameter named '{parameterName}'.");

    /// <inheritdoc />
    protected override void SetParameter(int index, DbParameter value) => _items[index] = Cast(value);

    /// <inheritdoc />
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var i = IndexOf(parameterName);
        if (i >= 0)
        {
            _items[i] = Cast(value);
        }
        else
        {
            _items.Add(Cast(value));
        }
    }

    internal Dictionary<string, object?> ToDictionary()
    {
        var dict = new Dictionary<string, object?>(_items.Count, StringComparer.Ordinal);
        foreach (var p in _items)
        {
            var name = ParamsWriter.Normalize(p.ParameterName);
            if (name.Length == 0)
            {
                throw new InvalidOperationException("Relate parameters must be named.");
            }

            dict[name] = p.Value is DBNull ? null : p.Value;
        }

        return dict;
    }

    private static RelateDbParameter Cast(object value) =>
        value as RelateDbParameter ?? throw new InvalidCastException($"Expected a {nameof(RelateDbParameter)}.");
}
