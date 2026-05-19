namespace MapVerifier.Server.Contracts;

public sealed record VersionResponse(string Name, string Version);

public sealed record LoadRequest(string? RouterDbPath);

public sealed record CoordinateDto(double Latitude, double Longitude);

public sealed record StatsResponse(int VertexCount, int EdgeCount, CoordinateDto SouthWest, CoordinateDto NorthEast);

public sealed record SnapRequest(double Lat, double Lon, string? Profile, float? SearchDistanceM);

public sealed record SnapResponse(CoordinateDto? Snapped);

public sealed record RouteRequest(double FromLat, double FromLon, double ToLat, double ToLon, string? Profile);

public sealed record RouteResponse(bool Found, double DistanceM, double DurationSec, GeoJsonLineString? Geometry);

public sealed record GeoJsonLineString(string Type, double[][] Coordinates);

public sealed record ErrorResponse(string Error, string Message);

public sealed record PolygonRestrictionRequest(
    string Kind,
    string? DifficultyType,
    CoordinateDto[] OuterBoundary,
    string? Tag);

public sealed record MeshRestrictionRequest(
    string Kind,
    string? DifficultyType,
    long[] MeshCodes,
    string? Tag);

public sealed record RestrictionIdResponse(Guid Id);

public sealed record FilePickRequest(string? Title, string? Filter, string? InitialDirectory);

public sealed record FilePickResponse(string? Path);

public sealed record GmlImportRequest(
    string FilePath,
    string Kind,
    string? DifficultyType,
    bool UseMapBounds,
    CoordinateDto? MapBoundsSouthWest,
    CoordinateDto? MapBoundsNorthEast,
    string? Tag);

public sealed record GmlImportResponse(Guid[] Ids, int AcceptedCount);


