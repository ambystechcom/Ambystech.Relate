using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>The statement class, classified from the plan (ADR-0162).</summary>
public enum RelateQueryType
{
    /// <summary>Not reported.</summary>
    Unknown = 0,
    /// <summary><c>r</c>: reads only.</summary>
    Read,
    /// <summary><c>w</c>: writes only.</summary>
    Write,
    /// <summary><c>rw</c>: reads and writes.</summary>
    ReadWrite,
    /// <summary><c>s</c>: schema change (index, constraint).</summary>
    Schema,
}
