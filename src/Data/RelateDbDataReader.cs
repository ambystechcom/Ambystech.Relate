using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Ambystech.Relate.Data;

/// <summary>
/// Forward-only reader over a materialized <see cref="RelateResult"/>. Cypher nulls read as
/// <see cref="DBNull"/>; graph values come back as <see cref="RelateNode"/>,
/// <see cref="RelateRelationship"/>, <see cref="RelatePath"/>, lists and dictionaries.
/// </summary>
public sealed class RelateDbDataReader : DbDataReader
{
    private readonly RelateResult _result;
    private readonly RelateDbConnection? _closeOnDispose;
    private int _row = -1;
    private bool _closed;

    internal RelateDbDataReader(RelateResult result, RelateDbConnection? closeOnDispose)
    {
        _result = result;
        _closeOnDispose = closeOnDispose;
    }

    /// <summary>The driver result behind this reader.</summary>
    public RelateResult Result => _result;

    /// <inheritdoc />
    public override int FieldCount => _result.Keys.Count;

    /// <inheritdoc />
    public override bool HasRows => _result.Count > 0;

    /// <inheritdoc />
    public override bool IsClosed => _closed;

    /// <summary>Graph changes (see <see cref="RelateDbCommand.ExecuteNonQuery"/>), or -1.</summary>
    public override int RecordsAffected
    {
        get
        {
            var c = _result.Summary.Counters;
            var n = c.NodesCreated + c.NodesDeleted + c.RelationshipsCreated + c.RelationshipsDeleted + c.PropertiesSet;
            return n > 0 ? (int)Math.Min(n, int.MaxValue) : -1;
        }
    }

    /// <inheritdoc />
    public override int Depth => 0;

    /// <inheritdoc />
    public override object this[int ordinal] => GetValue(ordinal);

    /// <inheritdoc />
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc />
    public override bool Read() => !_closed && ++_row < _result.Count;

    /// <inheritdoc />
    public override bool NextResult() => false;

    /// <inheritdoc />
    public override string GetName(int ordinal) => _result.Keys[ordinal];

    /// <inheritdoc />
    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _result.Keys.Count; i++)
        {
            if (string.Equals(_result.Keys[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        for (var i = 0; i < _result.Keys.Count; i++)
        {
            if (string.Equals(_result.Keys[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"No column named '{name}'.");
    }

    /// <summary>The CLR type of the column's first non-null value (<see cref="object"/> when all are null).</summary>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "Values come from the walker's closed set of BCL and Ambystech.Relate types, which are always preserved.")]
    public override Type GetFieldType(int ordinal)
    {
        foreach (var record in _result.Records)
        {
            if (record[ordinal] is { } value)
            {
                return value.GetType();
            }
        }

        return typeof(object);
    }

    /// <inheritdoc />
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <inheritdoc />
    public override object GetValue(int ordinal) => Current[ordinal] ?? DBNull.Value;

    /// <inheritdoc />
    public override int GetValues(object[] values)
    {
        var n = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < n; i++)
        {
            values[i] = GetValue(i);
        }

        return n;
    }

    /// <inheritdoc />
    public override bool IsDBNull(int ordinal) => Current[ordinal] is null;

    /// <summary>The value converted to <typeparamref name="T"/>.</summary>
    public override T GetFieldValue<T>(int ordinal) => Current.Get<T>(ordinal)!;

    /// <inheritdoc />
    public override bool GetBoolean(int ordinal) => GetFieldValue<bool>(ordinal);

    /// <inheritdoc />
    public override byte GetByte(int ordinal) => GetFieldValue<byte>(ordinal);

    /// <inheritdoc />
    public override char GetChar(int ordinal) => GetString(ordinal)[0];

    /// <inheritdoc />
    public override DateTime GetDateTime(int ordinal) => Current[ordinal] switch
    {
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        var other => (DateTime)ValueConverter.ConvertTo(other, typeof(DateTime))!,
    };

    /// <inheritdoc />
    public override decimal GetDecimal(int ordinal) => GetFieldValue<decimal>(ordinal);

    /// <inheritdoc />
    public override double GetDouble(int ordinal) => GetFieldValue<double>(ordinal);

    /// <inheritdoc />
    public override float GetFloat(int ordinal) => GetFieldValue<float>(ordinal);

    /// <inheritdoc />
    public override Guid GetGuid(int ordinal) => GetFieldValue<Guid>(ordinal);

    /// <inheritdoc />
    public override short GetInt16(int ordinal) => GetFieldValue<short>(ordinal);

    /// <inheritdoc />
    public override int GetInt32(int ordinal) => GetFieldValue<int>(ordinal);

    /// <inheritdoc />
    public override long GetInt64(int ordinal) => GetFieldValue<long>(ordinal);

    /// <inheritdoc />
    public override string GetString(int ordinal) =>
        Current[ordinal] as string ?? Convert.ToString(Current[ordinal], CultureInfo.InvariantCulture) ?? string.Empty;

    /// <inheritdoc />
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        CopyOut(GetFieldValue<byte[]>(ordinal), dataOffset, buffer, bufferOffset, length);

    /// <inheritdoc />
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        CopyOut(GetString(ordinal).ToCharArray(), dataOffset, buffer, bufferOffset, length);

    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

    /// <inheritdoc />
    public override void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _closeOnDispose?.Close();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    private RelateRecord Current => !_closed && _row >= 0 && _row < _result.Count
        ? _result[_row]
        : throw new InvalidOperationException("No current row: call Read() first.");

    private static long CopyOut<T>(T[] source, long dataOffset, T[]? buffer, int bufferOffset, int length)
    {
        if (buffer is null)
        {
            return source.Length;
        }

        var count = (int)Math.Max(0, Math.Min(length, source.Length - dataOffset));
        Array.Copy(source, dataOffset, buffer, bufferOffset, count);
        return count;
    }
}
