namespace Ambystech.Relate;

/// <summary>A spatial point. <see cref="Z"/> is set for 3D SRIDs (9157, 4979).</summary>
public sealed record RelatePoint(int Srid, double X, double Y, double? Z = null)
{
    /// <summary>SRID 7203.</summary>
    public const int CartesianSrid = 7203;
    /// <summary>SRID 9157.</summary>
    public const int Cartesian3DSrid = 9157;
    /// <summary>SRID 4326: X is longitude, Y is latitude.</summary>
    public const int Wgs84Srid = 4326;
    /// <summary>SRID 4979.</summary>
    public const int Wgs843DSrid = 4979;

    /// <summary>A 2D cartesian point.</summary>
    public static RelatePoint Cartesian(double x, double y) => new(CartesianSrid, x, y);

    /// <summary>A 3D cartesian point.</summary>
    public static RelatePoint Cartesian(double x, double y, double z) => new(Cartesian3DSrid, x, y, z);

    /// <summary>A WGS-84 geographic point.</summary>
    public static RelatePoint Wgs84(double longitude, double latitude) => new(Wgs84Srid, longitude, latitude);

    /// <summary>A WGS-84 3D geographic point.</summary>
    public static RelatePoint Wgs84(double longitude, double latitude, double height) => new(Wgs843DSrid, longitude, latitude, height);
}
