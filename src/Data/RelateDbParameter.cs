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
