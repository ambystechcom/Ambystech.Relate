namespace Ambystech.Relate;

/// <summary>The Cypher temporal kinds that can come back as <see cref="RelateTemporal"/>.</summary>
public enum RelateTemporalKind
{
    /// <summary>A time of day with an offset.</summary>
    Time,
    /// <summary>A time of day without an offset.</summary>
    LocalTime,
    /// <summary>A date and time without an offset.</summary>
    LocalDateTime,
    /// <summary>A date and time with an offset and possibly an IANA zone.</summary>
    DateTime,
}
