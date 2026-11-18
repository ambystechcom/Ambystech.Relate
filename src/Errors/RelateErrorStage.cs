using System.Text.Json;

namespace Ambystech.Relate;

/// <summary>The engine phase a failure came from (ADR-0502 §2).</summary>
public enum RelateErrorStage
{
    /// <summary>The failure was not classified by the engine (a binding-side failure).</summary>
    Unknown = 0,
    /// <summary>Parse, semantic or planning failure: the statement never ran.</summary>
    Compile,
    /// <summary>The statement failed while running.</summary>
    Runtime,
    /// <summary>An engine-internal, IO, storage or locked-store failure.</summary>
    Internal,
}
